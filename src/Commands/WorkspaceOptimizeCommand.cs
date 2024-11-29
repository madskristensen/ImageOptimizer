using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Workspace.VSIntegration.UI;

namespace MadsKristensen.ImageOptimizer.Commands
{
    [Export(typeof(INodeExtender))]
    public class WorkspaceOptimizeCommandProvider : INodeExtender
    {
        private readonly IWorkspaceCommandHandler _handler = new WorkspaceOptimizeCommand();

        public IChildrenSource ProvideChildren(WorkspaceVisualNodeBase parentNode)
        {
            return null;
        }

        public IWorkspaceCommandHandler ProvideCommandHandler(WorkspaceVisualNodeBase parentNode)
        {
            return parentNode is IFileNode or IFolderNode ? _handler : null;
        }
    }
    public class WorkspaceOptimizeCommand : IWorkspaceCommandHandler
    {
        private static readonly string[] _sizeSuffixes = ["bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];

        public bool IgnoreOnMultiselect => false;

        public int Priority => 100;

        private static DTE2 _dte;

        public int Exec(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _ = nCmdID == PackageIds.cmdWorkspaceOptimizelossy;
            _dte ??= ServiceProvider.GlobalProvider.GetService<DTE, DTE2>();

            if (IsSupportedCommand(pguidCmdGroup, nCmdID))
            {
                IEnumerable<string> files = GetImageFiles(selection);
                _ = files.Count();

                if (files.Any())
                {
                    CompressionHandler optimizer = new();
                    optimizer.OptimizeImagesAsync(files, CompressionType.Lossless).FireAndForget();

                    return VSConstants.S_OK;
                }
                else
                {
                    _dte.StatusBar.Text = "No images selected";
                }
            }

            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        private static IEnumerable<string> GetImageFiles(List<WorkspaceVisualNodeBase> selectedNodes)
        {
            foreach (WorkspaceVisualNodeBase selection in selectedNodes)
            {
                if (selection is IFolderNode folder)
                {
                    IEnumerable<string> images = Directory.EnumerateFiles(folder.FullPath, "*.*", SearchOption.AllDirectories)
                            .Where(Compressor.IsFileSupported);

                    foreach (var image in images)
                    {
                        yield return image;
                    }
                }
                else if (selection is IFileNode file && Compressor.IsFileSupported(file.FullPath))
                {
                    yield return file.FullPath;
                }
            }
        }

        public static string ToFileSize(long value, int decimalPlaces = 1)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (value < 0) { return "-" + ToFileSize(-value, decimalPlaces); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            var mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag)
            // [i.e. the number of bytes in the unit corresponding to mag]
            var adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return value < 1024
                ? string.Format("{0:n0} {1}", adjustedSize, _sizeSuffixes[mag])
                : string.Format("{0:n" + decimalPlaces + "} {1}", adjustedSize, _sizeSuffixes[mag]);
        }

        public bool QueryStatus(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, ref uint cmdf, ref string customTitle)
        {
            if (IsSupportedCommand(pguidCmdGroup, nCmdID))
            {
                if (selection.Any(s => s is IFileNode file && Compressor.IsFileSupported(file.FullPath)) || selection.Any(s => s is IFolderNode))
                {
                    cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
                    return true;
                }
                else
                {
                    cmdf = (uint)OLECMDF.OLECMDF_INVISIBLE;
                }
            }

            return false;
        }

        private static bool IsSupportedCommand(Guid pguidCmdGroup, uint nCmdID)
        {
            return pguidCmdGroup == PackageGuids.guidImageOptimizerCmdSet && (nCmdID == PackageIds.cmdWorkspaceOptimizelossless || nCmdID == PackageIds.cmdWorkspaceOptimizelossy);
        }
    }
}