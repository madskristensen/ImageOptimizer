using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using MadsKristensen.ImageOptimizer.Common;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Workspace.VSIntegration.UI;

namespace MadsKristensen.ImageOptimizer.Commands
{
    /// <summary>
    /// Provides the Copy base64 DataURI command for the Open Folder workspace context.
    /// </summary>
    [Export(typeof(INodeExtender))]
    public class WorkspaceCopyBase64CommandProvider : INodeExtender
    {
        private readonly IWorkspaceCommandHandler _handler = new WorkspaceCopyBase64Command();

        /// <inheritdoc/>
        public IChildrenSource ProvideChildren(WorkspaceVisualNodeBase parentNode)
        {
            return null;
        }

        /// <inheritdoc/>
        public IWorkspaceCommandHandler ProvideCommandHandler(WorkspaceVisualNodeBase parentNode)
        {
            return parentNode is IFileNode ? _handler : null;
        }
    }

    /// <summary>
    /// Handles the Copy base64 DataURI command in the Open Folder workspace.
    /// </summary>
    public class WorkspaceCopyBase64Command : IWorkspaceCommandHandler
    {
        /// <inheritdoc/>
        public bool IgnoreOnMultiselect => true;

        /// <inheritdoc/>
        public int Priority => 100;

        /// <inheritdoc/>
        public int Exec(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (IsSupportedCommand(pguidCmdGroup, nCmdID) && selection[0] is IFileNode fileNode)
            {
                ValidationResult validation = InputValidator.ValidateFilePath(fileNode.FullPath);
                if (!validation.IsValid)
                {
                    VS.StatusBar.ShowMessageAsync(string.Format(Constants.InvalidFileFormat, validation.ErrorMessage)).FireAndForget();
                    return VSConstants.S_OK;
                }

                var base64 = Base64Helpers.CreateBase64ImageString(fileNode.FullPath);
                if (!string.IsNullOrEmpty(base64))
                {
                    Clipboard.SetText(base64);
                    VS.StatusBar.ShowMessageAsync(string.Format(Constants.Base64CopiedFormat, base64.Length)).FireAndForget();
                }
                else
                {
                    VS.StatusBar.ShowMessageAsync(Constants.Base64FailedMessage).FireAndForget();
                }

                return VSConstants.S_OK;
            }

            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        /// <inheritdoc/>
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
            return pguidCmdGroup == PackageGuids.guidImageOptimizerCmdSet && nCmdID == PackageIds.cmdWorkspaceCopyDataUri;
        }
    }
}
