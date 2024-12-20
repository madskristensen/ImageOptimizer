using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using MadsKristensen.ImageOptimizer.Resizing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Workspace.VSIntegration.UI;

namespace MadsKristensen.ImageOptimizer.Commands
{
    [Export(typeof(INodeExtender))]
    public class WorkspaceResizeCommandProvider : INodeExtender
    {
        private readonly IWorkspaceCommandHandler _handler = new WorkspaceResizeCommand();

        public IChildrenSource ProvideChildren(WorkspaceVisualNodeBase parentNode)
        {
            return null;
        }

        public IWorkspaceCommandHandler ProvideCommandHandler(WorkspaceVisualNodeBase parentNode)
        {
            return parentNode is IFileNode ? _handler : null;
        }
    }

    public class WorkspaceResizeCommand : IWorkspaceCommandHandler
    {
        public bool IgnoreOnMultiselect => true;

        public int Priority => 100;


        public int Exec(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (IsSupportedCommand(pguidCmdGroup, nCmdID) && selection[0] is IFileNode fileNode)
            {
                ResizingDialog resizer = new(fileNode.FullPath);
                _ = resizer.ShowModal();

                return VSConstants.S_OK;
            }

            return (int)Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        public bool QueryStatus(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, ref uint cmdf, ref string customTitle)
        {
            if (IsSupportedCommand(pguidCmdGroup, nCmdID))
            {
                if (selection.Any(s => s is IFileNode file && Compressor.IsFileSupported(file.FullPath)))
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