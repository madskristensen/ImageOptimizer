using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace MadsKristensen.ImageOptimizer
{

    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", Vsix.Version, IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]
    [Guid(PackageGuids.guidImageOptimizerPkgString)]
    public sealed class ImageOptimizerPackage : Package
    {
        public DTE2 _dte;
        public static ImageOptimizerPackage Instance;

        List<string> _selectedPaths;
        string _copyPath;
        static bool _isProcessing;
        static Dictionary<string, DateTime> _fullyOptimized = new Dictionary<string, DateTime>();

        protected override void Initialize()
        {
            base.Initialize();
            _dte = GetService(typeof(DTE)) as DTE2;
            Instance = this;
            
            Logger.Initialize(this, Vsix.Name, Vsix.Version, "367cd134-ade0-4111-a928-c7a1e3b0bb00");

            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

            CommandID cmdLossless = new CommandID(PackageGuids.guidImageOptimizerCmdSet, PackageIds.cmdOptimizelossless);
            OleMenuCommand menuLossless = new OleMenuCommand((s, e) => { System.Threading.Tasks.Task.Run(() => OptimizeImage(false)); }, cmdLossless);
            menuLossless.BeforeQueryStatus += (s, e) => { OptimizeBeforeQueryStatus(s, false); };
            mcs.AddCommand(menuLossless);

            CommandID cmdLossy = new CommandID(PackageGuids.guidImageOptimizerCmdSet, PackageIds.cmdOptimizelossy);
            OleMenuCommand menuLossy = new OleMenuCommand((s, e) => { System.Threading.Tasks.Task.Run(() => OptimizeImage(true)); }, cmdLossy);
            menuLossy.BeforeQueryStatus += (s, e) => { OptimizeBeforeQueryStatus(s, true); };
            mcs.AddCommand(menuLossy);

            CommandID cmdCopy = new CommandID(PackageGuids.guidImageOptimizerCmdSet, PackageIds.cmdCopyDataUri);
            OleMenuCommand menuCopy = new OleMenuCommand(CopyAsBase64, cmdCopy);
            menuCopy.BeforeQueryStatus += CopyBeforeQueryStatus;
            mcs.AddCommand(menuCopy);
        }

        void CopyBeforeQueryStatus(object sender, EventArgs e)
        {
            OleMenuCommand button = (OleMenuCommand)sender;
            button.Visible = false;

            var files = ProjectHelpers.GetSelectedFilePaths();

            if (files.Count() == 1)
            {
                _copyPath = files.FirstOrDefault();
                button.Visible = !string.IsNullOrEmpty(_copyPath) && Compressor.IsFileSupported(_copyPath);
            }
        }

        void CopyAsBase64(object sender, EventArgs e)
        {
            string base64 = "data:"
                        + GetMimeTypeFromFileExtension(_copyPath)
                        + ";base64,"
                        + Convert.ToBase64String(File.ReadAllBytes(_copyPath));

            Clipboard.SetText(base64);
            Telemetry.TrackEvent("Copy as DataURI");

            _dte.StatusBar.Text = "DataURI copied to clipboard (" + base64.Length + " characters)";
        }

        static string GetMimeTypeFromFileExtension(string file)
        {
            string ext = Path.GetExtension(file).TrimStart('.');

            switch (ext)
            {
                case "jpg":
                case "jpeg":
                    return "image/jpeg";
                case "svg":
                    return "image/svg+xml";
                case "png":
                case "gif":
                case "tiff":
                case "webp":
                case "bmp":
                    return "image/" + ext;

                case "woff":
                    return "font/x-woff";

                case "otf":
                    return "font/otf";

                case "eot":
                    return "application/vnd.ms-fontobject";

                case "ttf":
                    return "application/octet-stream";

                default:
                    return "text/plain";
            }
        }

        void OptimizeBeforeQueryStatus(object sender, bool lossy)
        {
            OleMenuCommand button = (OleMenuCommand)sender;
            _selectedPaths = ProjectHelpers.GetSelectedFilePaths().Where(file => Compressor.IsFileSupported(file)).ToList();

            int items = _selectedPaths.Count;

            var text = items == 1 ? " Optimize Image" : " Optimize Images";
            button.Text = (lossy ? "Lossy" : "Lossless") + text;
            button.Visible = items > 0;
            button.Enabled = true;

            if (button.Visible && _isProcessing)
            {
                button.Enabled = false;
                button.Text += " (running)";
            }
        }

        void OptimizeImage(bool lossy)
        {
            _isProcessing = true;
            CompressionResult[] list = new CompressionResult[_selectedPaths.Count];
            var stopwatch = Stopwatch.StartNew();

            try
            {
                string text = _selectedPaths.Count == 1 ? " image" : " images";
                _dte.StatusBar.Progress(true, "Optimizing " + _selectedPaths.Count + text + "...", AmountCompleted: 1, Total: _selectedPaths.Count + 1);

                Compressor compressor = new Compressor();
                Cache cache = new Cache(_dte.Solution, lossy);
                int nCompleted = 0;
                var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

                Parallel.For(0, _selectedPaths.Count, options, i =>
                {
                    string file = _selectedPaths[i];

                    // Don't process if file has been fully optimized already
                    if (cache.IsFullyOptimized(file))
                    {
                        var bogus = new CompressionResult(file, file, TimeSpan.Zero) { Processed = false };
                        HandleResult(bogus, nCompleted + 1);
                    }
                    else
                    {
                        var result = compressor.CompressFileAsync(file, lossy);
                        HandleResult(result, nCompleted + 1);

                        if (result.Saving > 0 && !string.IsNullOrEmpty(result.ResultFileName))
                            list[i] = result;
                        else
                            cache.AddToCache(file);
                    }

                    Interlocked.Increment(ref nCompleted);
                });
            }
            finally
            {
                _dte.StatusBar.Progress(false);
                stopwatch.Stop();
                DisplayEndResult(list, stopwatch.Elapsed);
                _isProcessing = false;
            }
        }

        void HandleResult(CompressionResult result, int count)
        {
            string name = Path.GetFileName(result.OriginalFileName);

            if (result.Saving > 0 && File.Exists(result.ResultFileName))
            {
                if (_dte.SourceControl.IsItemUnderSCC(result.OriginalFileName) && !_dte.SourceControl.IsItemCheckedOut(result.OriginalFileName))
                    _dte.SourceControl.CheckOutItem(result.OriginalFileName);

                File.Copy(result.ResultFileName, result.OriginalFileName, true);
                File.Delete(result.ResultFileName);

                string text = "Compressed " + name + " by " + result.Saving + " bytes / " + result.Percent + "%";
                _dte.StatusBar.Progress(true, text, count, _selectedPaths.Count + 1);

                Logger.Log(result.ToString());
                string ext = Path.GetExtension(result.OriginalFileName).ToLowerInvariant().Replace(".jpeg", ".jpg");
                var metrics = new Dictionary<string, double> { { "saving", result.Saving } };
                Telemetry.TrackEvent(ext, metrics: metrics);
            }
            else
            {
                _dte.StatusBar.Progress(true, name + " is already optimized", AmountCompleted: count, Total: _selectedPaths.Count + 1);
                Logger.Log(name + " is already optimized");

                if (result.Processed)
                    Telemetry.TrackEvent("Already optimized");
            }
        }

        void DisplayEndResult(IList<CompressionResult> list, TimeSpan elapsed)
        {
            long savings = list.Where(r => r != null).Sum(r => r.Saving);
            long originals = list.Where(r => r != null).Sum(r => r.OriginalFileSize);
            long results = list.Where(r => r != null).Sum(r => r.ResultFileSize);

            if (savings > 0)
            {
                int successfulOptimizations = list.Count(x => x != null);
                double percent = Math.Round(100 - ((double)results / (double)originals * 100), 1, MidpointRounding.AwayFromZero);
                string image = successfulOptimizations == 1 ? "image" : "images";
                _dte.StatusBar.Text = successfulOptimizations + " " + image + " optimized in " + Math.Round(elapsed.TotalMilliseconds / 1000, 2) + " seconds. Total saving of " + savings + " bytes / " + percent + "%";
            }
            else
            {
                _dte.StatusBar.Text = "The images were already optimized";
            }
        }
    }
}