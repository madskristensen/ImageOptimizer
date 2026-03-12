using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using MadsKristensen.ImageOptimizer.Resizing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Workspace.VSIntegration.UI;

namespace MadsKristensen.ImageOptimizer.Commands
{
    /// <summary>
    /// Provides image resize command for the Open Folder workspace context.
    /// </summary>
    [Export(typeof(INodeExtender))]
    public class WorkspaceResizeCommandProvider : INodeExtender
    {
        private readonly IWorkspaceCommandHandler _handler = new WorkspaceResizeCommand();

        /// <inheritdoc/>
        public IChildrenSource ProvideChildren(WorkspaceVisualNodeBase parentNode)
        {
            return null;
        }

        /// <inheritdoc/>
        public IWorkspaceCommandHandler ProvideCommandHandler(WorkspaceVisualNodeBase parentNode)
        {
            return WorkspaceNodePathResolver.TryGetFilePath(parentNode, out _) ? _handler : null;
        }
    }

    /// <summary>
    /// Handles image resize command in the Open Folder workspace.
    /// </summary>
    public class WorkspaceResizeCommand : IWorkspaceCommandHandler
    {
        /// <inheritdoc/>
        public bool IgnoreOnMultiselect => true;

        /// <inheritdoc/>
        public int Priority => 100;

        /// <inheritdoc/>
        public int Exec(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (IsSupportedCommand(pguidCmdGroup, nCmdID)
                && selection.Count > 0
                && WorkspaceNodePathResolver.TryGetFilePath(selection[0], out var filePath))
            {
                ResizingDialog resizer = new(filePath);
                resizer.ShowModal();

                return VSConstants.S_OK;
            }

            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        /// <inheritdoc/>
        public bool QueryStatus(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, ref uint cmdf, ref string customTitle)
        {
            if (IsSupportedCommand(pguidCmdGroup, nCmdID))
            {
                if (selection.Any(s => WorkspaceNodePathResolver.TryGetFilePath(s, out var filePath) && Compressor.IsFileSupported(filePath)))
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
            return pguidCmdGroup == PackageGuids.guidImageOptimizerCmdSet && nCmdID == PackageIds.cmdWorkspaceResize;
        }
    }
}