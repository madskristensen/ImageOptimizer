using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using MadsKristensen.ImageOptimizer.Common;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Workspace.VSIntegration.UI;

namespace MadsKristensen.ImageOptimizer.Commands
{
    /// <summary>
    /// Provides image optimization commands for the Open Folder workspace context.
    /// </summary>
    [Export(typeof(INodeExtender))]
    public class WorkspaceOptimizeCommandProvider : INodeExtender
    {
        private readonly IWorkspaceCommandHandler _handler = new WorkspaceOptimizeCommand();

        /// <inheritdoc/>
        public IChildrenSource ProvideChildren(WorkspaceVisualNodeBase parentNode)
        {
            return null;
        }

        /// <inheritdoc/>
        public IWorkspaceCommandHandler ProvideCommandHandler(WorkspaceVisualNodeBase parentNode)
        {
            return parentNode is IFileNode or IFolderNode ? _handler : null;
        }
    }

    /// <summary>
    /// Handles image optimization commands in the Open Folder workspace.
    /// </summary>
    public class WorkspaceOptimizeCommand : IWorkspaceCommandHandler
    {
        /// <inheritdoc/>
        public bool IgnoreOnMultiselect => false;

        /// <inheritdoc/>
        public int Priority => 100;

        /// <inheritdoc/>
        public int Exec(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (IsOptimizeCommand(pguidCmdGroup, nCmdID))
            {
                var files = GetImageFiles(selection).ToList();
                CompressionType compressionType = nCmdID == PackageIds.cmdWorkspaceOptimizelossless ? CompressionType.Lossless : CompressionType.Lossy;

                if (files.Count > 0)
                {
                    CompressionHandler optimizer = new();
                    optimizer.OptimizeImagesAsync(files, compressionType).FireAndForget();
                    return VSConstants.S_OK;
                }
                else
                {
                    VS.StatusBar.ShowMessageAsync(Constants.NoImagesSelectedMessage).FireAndForget();
                }
            }
            else if (IsConvertToWebpCommand(pguidCmdGroup, nCmdID))
            {
                var files = GetConvertibleFiles(selection, Compressor.IsConvertibleToWebp).ToList();

                if (files.Count > 0)
                {
                    ConversionHandler handler = new();
                    handler.ConvertToWebpAsync(files).FireAndForget();
                    return VSConstants.S_OK;
                }
                else
                {
                    VS.StatusBar.ShowMessageAsync(Constants.NoConvertibleImagesMessage).FireAndForget();
                }
            }
            else if (IsConvertToAvifCommand(pguidCmdGroup, nCmdID))
            {
                var files = GetConvertibleFiles(selection, Compressor.IsConvertibleToAvif).ToList();

                if (files.Count > 0)
                {
                    ConversionHandler handler = new();
                    handler.ConvertToAvifAsync(files).FireAndForget();
                    return VSConstants.S_OK;
                }
                else
                {
                    VS.StatusBar.ShowMessageAsync(Constants.NoConvertibleImagesMessage).FireAndForget();
                }
            }

            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        private static IEnumerable<string> GetImageFiles(List<WorkspaceVisualNodeBase> selectedNodes)
        {
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (WorkspaceVisualNodeBase selection in selectedNodes)
            {
                switch (selection)
                {
                    case IFolderNode folder:
                        foreach (var image in FileDiscovery.EnumerateFiles(folder.FullPath, Compressor.IsFileSupported))
                        {
                            _ = processedFiles.Add(image);
                        }
                        break;

                    case IFileNode file when Compressor.IsFileSupported(file.FullPath):
                        _ = processedFiles.Add(file.FullPath);
                        break;
                }
            }

            return processedFiles;
        }

        private static IEnumerable<string> GetConvertibleFiles(List<WorkspaceVisualNodeBase> selectedNodes, Func<string, bool> isConvertible)
        {
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (WorkspaceVisualNodeBase selection in selectedNodes)
            {
                switch (selection)
                {
                    case IFolderNode folder:
                        foreach (var image in FileDiscovery.EnumerateFiles(folder.FullPath, isConvertible))
                        {
                            _ = processedFiles.Add(image);
                        }
                        break;

                    case IFileNode file when isConvertible(file.FullPath):
                        _ = processedFiles.Add(file.FullPath);
                        break;
                }
            }

            return processedFiles;
        }

        /// <inheritdoc/>
        public bool QueryStatus(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, ref uint cmdf, ref string customTitle)
        {
            if (IsOptimizeCommand(pguidCmdGroup, nCmdID))
            {
                var hasImageFiles = false;
                var hasFolders = false;

                foreach (WorkspaceVisualNodeBase item in selection)
                {
                    switch (item)
                    {
                        case IFileNode file when Compressor.IsFileSupported(file.FullPath):
                            hasImageFiles = true;
                            break;
                        case IFolderNode:
                            hasFolders = true;
                            break;
                    }

                    if (hasImageFiles || hasFolders)
                    {
                        break;
                    }
                }

                if (hasImageFiles || hasFolders)
                {
                    cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                    return true;
                }
            }
            else if (IsConvertToWebpCommand(pguidCmdGroup, nCmdID) || IsConvertToAvifCommand(pguidCmdGroup, nCmdID))
            {
                var hasConvertibleFiles = false;
                var hasFolders = false;

                foreach (WorkspaceVisualNodeBase item in selection)
                {
                    switch (item)
                    {
                        case IFileNode file when Compressor.IsConvertibleToWebp(file.FullPath):
                            hasConvertibleFiles = true;
                            break;
                        case IFolderNode:
                            hasFolders = true;
                            break;
                    }

                    if (hasConvertibleFiles || hasFolders)
                    {
                        break;
                    }
                }

                if (hasConvertibleFiles || hasFolders)
                {
                    cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                    return true;
                }
            }

            return false;
        }

        private static bool IsOptimizeCommand(Guid pguidCmdGroup, uint nCmdID)
        {
            return pguidCmdGroup == PackageGuids.guidImageOptimizerCmdSet &&
                   (nCmdID == PackageIds.cmdWorkspaceOptimizelossless || nCmdID == PackageIds.cmdWorkspaceOptimizelossy);
        }

        private static bool IsConvertToWebpCommand(Guid pguidCmdGroup, uint nCmdID)
        {
            return pguidCmdGroup == PackageGuids.guidImageOptimizerCmdSet &&
                   nCmdID == PackageIds.cmdWorkspaceConvertToWebp;
        }

        private static bool IsConvertToAvifCommand(Guid pguidCmdGroup, uint nCmdID)
        {
            return pguidCmdGroup == PackageGuids.guidImageOptimizerCmdSet &&
                   nCmdID == PackageIds.cmdWorkspaceConvertToAvif;
        }
    }
}