using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
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
        public bool IgnoreOnMultiselect => false;

        public int Priority => 100;

        public int Exec(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (IsSupportedCommand(pguidCmdGroup, nCmdID))
            {
                IEnumerable<string> files = GetImageFiles(selection);
                CompressionType compressionType = nCmdID == PackageIds.cmdWorkspaceOptimizelossless ? CompressionType.Lossless : CompressionType.Lossy;

                if (files.Any())
                {
                    CompressionHandler optimizer = new();
                    optimizer.OptimizeImagesAsync(files, compressionType).FireAndForget();

                    return VSConstants.S_OK;
                }
                else
                {
                    VS.StatusBar.ShowMessageAsync("No images selected").FireAndForget();
                }
            }

            return (int)Constants.OLECMDERR_E_NOTSUPPORTED;
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