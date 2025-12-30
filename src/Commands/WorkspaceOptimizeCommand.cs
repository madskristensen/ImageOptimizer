using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
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
            if (IsSupportedCommand(pguidCmdGroup, nCmdID))
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

            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        private static IEnumerable<string> GetImageFiles(List<WorkspaceVisualNodeBase> selectedNodes)
        {
            // Use thread-safe ConcurrentDictionary as a set for deduplication
            var processedFiles = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            var resultFiles = new List<string>();

            foreach (WorkspaceVisualNodeBase selection in selectedNodes)
            {
                switch (selection)
                {
                    case IFolderNode folder:
                        try
                        {
                            // Sequential enumeration is more efficient for I/O-bound operations
                            IEnumerable<string> images = Directory.EnumerateFiles(folder.FullPath, Constants.AllFilesPattern, SearchOption.AllDirectories)
                                .Where(Compressor.IsFileSupported);

                            foreach (var image in images)
                            {
                                if (processedFiles.TryAdd(image, 0))
                                {
                                    resultFiles.Add(image);
                                }
                            }
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            ex.LogAsync().FireAndForget();
                        }
                        catch (IOException ex)
                        {
                            ex.LogAsync().FireAndForget();
                        }
                        break;

                    case IFileNode file when Compressor.IsFileSupported(file.FullPath):
                        if (processedFiles.TryAdd(file.FullPath, 0))
                        {
                            resultFiles.Add(file.FullPath);
                        }
                        break;
                }
            }

            return resultFiles;
        }

        /// <inheritdoc/>
        public bool QueryStatus(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, ref uint cmdf, ref string customTitle)
        {
            if (IsSupportedCommand(pguidCmdGroup, nCmdID))
            {
                // Optimize the check to avoid multiple LINQ evaluations
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

                    // Early exit if we found what we need
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

            return false;
        }

        private static bool IsSupportedCommand(Guid pguidCmdGroup, uint nCmdID)
        {
            return pguidCmdGroup == PackageGuids.guidImageOptimizerCmdSet &&
                   (nCmdID == PackageIds.cmdWorkspaceOptimizelossless || nCmdID == PackageIds.cmdWorkspaceOptimizelossy);
        }
    }
}