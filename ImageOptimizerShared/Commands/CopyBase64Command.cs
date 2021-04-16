using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace MadsKristensen.ImageOptimizer
{
    public class CopyBase64Command
    {
        private static DTE2 _dte;

        public static async Task InitializeAsync(IAsyncServiceProvider provider)
        {
            var commandService = await provider.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            Assumes.Present(commandService);

            _dte = await provider.GetServiceAsync(typeof(DTE)) as DTE2;
            Assumes.Present(_dte);

            var cmdId = new CommandID(PackageGuids.guidImageOptimizerCmdSet, PackageIds.cmdCopyDataUri);
            var menuCmd = new OleMenuCommand(Execute, cmdId);
            menuCmd.BeforeQueryStatus += CopyBeforeQueryStatus;
            commandService.AddCommand(menuCmd);
        }

        private static void CopyBeforeQueryStatus(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var button = (OleMenuCommand)sender;
            button.Visible = false;

            IEnumerable<string> files = ProjectHelpers.GetSelectedFilePaths(_dte);

            if (files.Count() == 1)
            {
                string copyPath = files.FirstOrDefault();
                button.Visible = !string.IsNullOrEmpty(copyPath) && Compressor.IsFileSupported(copyPath);
            }
        }

        private static void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IEnumerable<string> files = ProjectHelpers.GetSelectedFilePaths(_dte);
            string copyPath = files.FirstOrDefault();

            string base64 = "data:"
                        + GetMimeTypeFromFileExtension(copyPath)
                        + ";base64,"
                        + Convert.ToBase64String(File.ReadAllBytes(copyPath));

            Clipboard.SetText(base64);

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
                default:
                    return "image/" + ext;
            }
        }
    }
}
