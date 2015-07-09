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
    [InstalledProductRegistration("#110", "#112", Version, IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]
    [Guid(GuidList.guidImageOptimizerPkgString)]
    public sealed class ImageOptimizerPackage : Package
    {
        public const string Version = "1.0";
        public DTE2 _dte;
        public static ImageOptimizerPackage Instance;
        private List<string> _selectedPaths;
        private string _copyPath;

        protected override void Initialize()
        {
            base.Initialize();
            _dte = GetService(typeof(DTE)) as DTE2;
            Instance = this;

            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            CommandID cmdOptimize = new CommandID(GuidList.guidImageOptimizerCmdSet, (int)PackageCommands.cmdOptimizeImage);
            OleMenuCommand menuOptimize = new OleMenuCommand(OptimizeImage, cmdOptimize);
            menuOptimize.BeforeQueryStatus += MenuOptimizeBeforeQueryStatus;
            mcs.AddCommand(menuOptimize);

            CommandID cmdCopy = new CommandID(GuidList.guidImageOptimizerCmdSet, (int)PackageCommands.cmdCopyDataUri);
            OleMenuCommand menuCopy = new OleMenuCommand(CopyAsBase64, cmdCopy);
            menuCopy.BeforeQueryStatus += CopyBeforeQueryStatus;
            mcs.AddCommand(menuCopy);
        }

        void CopyBeforeQueryStatus(object sender, EventArgs e)
        {
            OleMenuCommand button = (OleMenuCommand)sender;
            _copyPath = GetSelectedFilePaths().FirstOrDefault();

            button.Visible = !string.IsNullOrEmpty(_copyPath) && Compressor.IsFileSupported(_copyPath);
        }

        private void CopyAsBase64(object sender, EventArgs e)
        {
            string base64 = "data:"
                        + GetMimeTypeFromFileExtension(_copyPath)
                        + ";base64,"
                        + Convert.ToBase64String(File.ReadAllBytes(_copyPath));

            Clipboard.SetText(base64);

            _dte.StatusBar.Text = "DataURI copied to clipboard (" + base64.Length + " characters)";
        }

        private static string GetMimeTypeFromFileExtension(string file)
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
            _selectedPaths = GetSelectedFilePaths().Where(file => Compressor.IsFileSupported(file)).ToList();

            int items = _selectedPaths.Count;

            button.Text = items == 1 ? "Optimize image" : "Optimize images";
            button.Visible = items > 0;
        }

        private void OptimizeImage(object sender, EventArgs e)
        {
            string text = _selectedPaths.Count == 1 ? " image" : " images";
            _dte.StatusBar.Progress(true, "Optimizing " + _selectedPaths.Count + text + "...", AmountCompleted: 0, Total: _selectedPaths.Count);

            Compressor compressor = new Compressor();
            List<CompressionResult> list = new List<CompressionResult>();

            System.Threading.ThreadPool.QueueUserWorkItem((o) =>
            {
                for (int i = 0; i < _selectedPaths.Count; i++)
                {
                    string file = _selectedPaths[i];

                    var result = compressor.CompressFile(file);
                    HandleResult(result, i + 1);

                    if (File.Exists(result.ResultFileName))
                        File.Delete(result.ResultFileName);

                    if (result.Saving > 0 && !string.IsNullOrEmpty(result.ResultFileName))
                        list.Add(result);
                }

                _dte.StatusBar.Progress(false);
                DisplayEndResult(list);
            });
        }

        public IEnumerable<string> GetSelectedFilePaths()
        {
            return GetSelectedItemPaths()
                .SelectMany(p => Directory.Exists(p)
                                 ? Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories)
                                 : new[] { p }
                           );
        }

        public IEnumerable<string> GetSelectedItemPaths(DTE2 dte = null)
        {
            var items = (Array)_dte.ToolWindows.SolutionExplorer.SelectedItems;
            foreach (UIHierarchyItem selItem in items)
            {
                var item = selItem.Object as ProjectItem;

                if (item != null && item.Properties != null)
                    yield return item.Properties.Item("FullPath").Value.ToString();
            }
        }

        private void HandleResult(CompressionResult result, int count)
        {
            string name = Path.GetFileName(result.OriginalFileName);

            if (result.Saving > 0 && File.Exists(result.ResultFileName))
            {
                if (_dte.SourceControl.IsItemUnderSCC(result.OriginalFileName) && !_dte.SourceControl.IsItemCheckedOut(result.OriginalFileName))
                    _dte.SourceControl.CheckOutItem(result.OriginalFileName);

                File.Copy(result.ResultFileName, result.OriginalFileName, true);

                string text = "Compressed " + name + " by " + result.Saving + " bytes / " + result.Percent + "%";
                _dte.StatusBar.Progress(true, text, AmountCompleted: count, Total: _selectedPaths.Count);
            }
            else
            {
                _dte.StatusBar.Progress(true, name + " is already optimized", AmountCompleted: count, Total: _selectedPaths.Count);
            }

            if (result.Saving > 0)
                Logger.Log(result.ToString());
        }

        private void DisplayEndResult(List<CompressionResult> list)
        {
            long savings = list.Sum(r => r.Saving);
            long originals = list.Sum(r => r.OriginalFileSize);
            long results = list.Sum(r => r.ResultFileSize);

            if (savings > 0)
            {
                double percent = Math.Round(100 - ((double)results / (double)originals * 100), 1, MidpointRounding.AwayFromZero);
                string image = list.Count == 1 ? "image" : "images";
                _dte.StatusBar.Text = list.Count + " " + image + " optimized. Total saving of " + savings + " bytes / " + percent + "%"; ;
            }
            else
            {
                _dte.StatusBar.Text = "The images were already optimized";
            }
        }
    }
}