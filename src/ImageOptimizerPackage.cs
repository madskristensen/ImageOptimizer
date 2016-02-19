using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

            Telemetry.Initialize(_dte, Vsix.Version, "367cd134-ade0-4111-a928-c7a1e3b0bb00");
            Logger.Initialize(this, "Image Optimizer");

            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            CommandID cmdOptimize = new CommandID(PackageGuids.guidImageOptimizerCmdSet, PackageIds.cmdOptimizeImage);
            OleMenuCommand menuOptimize = new OleMenuCommand(async (s, e) => { await OptimizeImage(); }, cmdOptimize);
            menuOptimize.BeforeQueryStatus += MenuOptimizeBeforeQueryStatus;
            mcs.AddCommand(menuOptimize);

            CommandID cmdCopy = new CommandID(PackageGuids.guidImageOptimizerCmdSet, PackageIds.cmdCopyDataUri);
            OleMenuCommand menuCopy = new OleMenuCommand(CopyAsBase64, cmdCopy);
            menuCopy.BeforeQueryStatus += CopyBeforeQueryStatus;
            mcs.AddCommand(menuCopy);
        }

        void CopyBeforeQueryStatus(object sender, EventArgs e)
        {
            OleMenuCommand button = (OleMenuCommand)sender;
            _copyPath = ProjectHelpers.GetSelectedFilePaths().FirstOrDefault();

            button.Visible = !string.IsNullOrEmpty(_copyPath) && Compressor.IsFileSupported(_copyPath);
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

        void MenuOptimizeBeforeQueryStatus(object sender, EventArgs e)
        {
            OleMenuCommand button = (OleMenuCommand)sender;
            _selectedPaths = ProjectHelpers.GetSelectedFilePaths().Where(file => Compressor.IsFileSupported(file)).ToList();

            int items = _selectedPaths.Count;

            button.Text = items == 1 ? "Optimize image" : "Optimize images";
            button.Visible = items > 0;
            button.Enabled = true;

            if (button.Visible && _isProcessing)
            {
                button.Enabled = false;
                button.Text += " (running)";
            }
        }

        async System.Threading.Tasks.Task OptimizeImage()
        {
            _isProcessing = true;
            List<CompressionResult> list = new List<CompressionResult>();

            try
            {
                string text = _selectedPaths.Count == 1 ? " image" : " images";
                _dte.StatusBar.Progress(true, "Optimizing " + _selectedPaths.Count + text + "...", AmountCompleted: 1, Total: _selectedPaths.Count + 1);

                Compressor compressor = new Compressor();
                Cache cache = new Cache(_dte.Solution);

                for (int i = 0; i < _selectedPaths.Count; i++)
                {
                    string file = _selectedPaths[i];

                    // Don't process if file has been fully optimized already
                    if (cache.IsFullyOptimized(file))
                    {
                        var bogus = new CompressionResult(file, file) { Processed = false };
                        HandleResult(bogus, i + 1);
                    }
                    else
                    {
                        var result = await compressor.CompressFileAsync(file);
                        HandleResult(result, i + 1);

                        if (result.Saving > 0 && !string.IsNullOrEmpty(result.ResultFileName))
                            list.Add(result);
                        else
                            await cache.AddToCache(file);
                    }
                }
            }
            finally
            {
                _dte.StatusBar.Progress(false);
                DisplayEndResult(list);
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

        void DisplayEndResult(List<CompressionResult> list)
        {
            long savings = list.Sum(r => r.Saving);
            long originals = list.Sum(r => r.OriginalFileSize);
            long results = list.Sum(r => r.ResultFileSize);

            if (savings > 0)
            {
                double percent = Math.Round(100 - ((double)results / (double)originals * 100), 1, MidpointRounding.AwayFromZero);
                string image = list.Count == 1 ? "image" : "images";
                _dte.StatusBar.Text = list.Count + " " + image + " optimized. Total saving of " + savings + " bytes / " + percent + "%";
            }
            else
            {
                _dte.StatusBar.Text = "The images were already optimized";
            }
        }
    }
}