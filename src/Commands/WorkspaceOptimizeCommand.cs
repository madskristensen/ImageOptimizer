using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
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
            return WorkspaceNodePathResolver.TryGetNodePath(parentNode, out _, out _) ? _handler : null;
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
                var selectedFolderPath = GetSelectedFolderPath(selection);
                CompressionType compressionType = nCmdID == PackageIds.cmdWorkspaceOptimizelossless ? CompressionType.Lossless : CompressionType.Lossy;

                if (files.Count > 0)
                {
                    CompressionHandler optimizer = new();
                    optimizer.OptimizeImagesAsync(files, compressionType, selectedFolderPath: selectedFolderPath).FireAndForget();
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
                if (!WorkspaceNodePathResolver.TryGetNodePath(selection, out var path, out var isFolder))
                {
                    continue;
                }

                if (isFolder)
                {
                    foreach (var image in FileDiscovery.EnumerateFiles(path, Compressor.IsFileSupported))
                    {
                        _ = processedFiles.Add(image);
                    }
                }
                else if (Compressor.IsFileSupported(path))
                {
                    _ = processedFiles.Add(path);
                }
            }

            return processedFiles;
        }

        private static string GetSelectedFolderPath(List<WorkspaceVisualNodeBase> selectedNodes)
        {
            if (WorkspaceNodePathResolver.TryGetWorkspaceFilesSelectedFolderPath(out var workspaceFilesFolderPath))
            {
                return workspaceFilesFolderPath;
            }

            foreach (WorkspaceVisualNodeBase selection in selectedNodes)
            {
                if (!WorkspaceNodePathResolver.TryGetNodePath(selection, out var path, out var isFolder))
                {
                    continue;
                }

                if (isFolder || Directory.Exists(path) || (!Path.IsPathRooted(path) && !Path.HasExtension(path)))
                {
                    return path;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetConvertibleFiles(List<WorkspaceVisualNodeBase> selectedNodes, Func<string, bool> isConvertible)
        {
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (WorkspaceVisualNodeBase selection in selectedNodes)
            {
                if (!WorkspaceNodePathResolver.TryGetNodePath(selection, out var path, out var isFolder))
                {
                    continue;
                }

                if (isFolder)
                {
                    foreach (var image in FileDiscovery.EnumerateFiles(path, isConvertible))
                    {
                        _ = processedFiles.Add(image);
                    }
                }
                else if (isConvertible(path))
                {
                    _ = processedFiles.Add(path);
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
                    if (!WorkspaceNodePathResolver.TryGetNodePath(item, out var path, out var isFolder))
                    {
                        continue;
                    }

                    if (isFolder)
                    {
                        hasFolders = true;
                    }
                    else if (Compressor.IsFileSupported(path))
                    {
                        hasImageFiles = true;
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
                var isConvertible = IsConvertToWebpCommand(pguidCmdGroup, nCmdID)
                    ? (Func<string, bool>)Compressor.IsConvertibleToWebp
                    : Compressor.IsConvertibleToAvif;

                var hasConvertibleFiles = false;
                var hasFolders = false;

                foreach (WorkspaceVisualNodeBase item in selection)
                {
                    if (!WorkspaceNodePathResolver.TryGetNodePath(item, out var path, out var isFolder))
                    {
                        continue;
                    }

                    if (isFolder)
                    {
                        hasFolders = true;
                    }
                    else if (isConvertible(path))
                    {
                        hasConvertibleFiles = true;
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

    internal static class WorkspaceNodePathResolver
    {
        private static readonly string[] PathPropertyNames = ["FullPath", "Path", "FilePath", "FolderPath"];
        private static readonly string[] NestedNodePropertyNames = ["Info", "SourceItem", "Item", "DataItem", "Model", "Node", "Value"];

        public static bool TryGetNodePath(WorkspaceVisualNodeBase node, out string path, out bool isFolder)
        {
            path = null;
            isFolder = false;

            if (node is null)
            {
                return false;
            }

            if (node is IFileNode file && TryNormalizePath(file.FullPath, out path))
            {
                return true;
            }

            if (node is IFolderNode folder && TryNormalizePath(folder.FullPath, out path))
            {
                isFolder = true;
                return true;
            }

            return TryGetNodePathFromObject(node, out path, out isFolder, 0);
        }

        public static bool TryGetFilePath(WorkspaceVisualNodeBase node, out string filePath)
        {
            filePath = null;

            if (TryGetNodePath(node, out var path, out var isFolder) && !isFolder)
            {
                filePath = path;
                return true;
            }

            return false;
        }

        public static bool TryGetWorkspaceFilesSelectedFolderPath(out string folderPath)
        {
            folderPath = null;

            var contextMenuControllerType = GetWorkspaceFilesContextMenuControllerType();
            if (contextMenuControllerType is null)
            {
                return false;
            }

            if (TryGetCurrentItemsSelectedFolderPath(contextMenuControllerType, out folderPath))
            {
                return true;
            }

            var currentItem = contextMenuControllerType
                .GetProperty("CurrentItem", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);

            if (currentItem is not null && TryGetPathFromCandidate(currentItem, out var path, out var isFolder) && isFolder)
            {
                folderPath = path;
                return true;
            }

            return false;
        }

        private static Type GetWorkspaceFilesContextMenuControllerType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("WorkspaceFiles.WorkspaceItemContextMenuController", throwOnError: false);
                if (type is not null)
                {
                    return type;
                }
            }

            return null;
        }

        private static bool TryGetCurrentItemsSelectedFolderPath(Type contextMenuControllerType, out string folderPath)
        {
            folderPath = null;

            var currentItems = contextMenuControllerType
                .GetProperty("CurrentItems", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null) as IEnumerable;

            if (currentItems is null)
            {
                return false;
            }

            foreach (var item in currentItems)
            {
                if (item is not null && TryGetPathFromCandidate(item, out var path, out var isFolder) && isFolder)
                {
                    folderPath = path;
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetNodePathFromObject(object node, out string path, out bool isFolder, int depth)
        {
            path = null;
            isFolder = false;

            if (node is null || depth > 3)
            {
                return false;
            }

            if (TryGetPathFromCandidate(node, out path, out isFolder))
            {
                return true;
            }

            foreach (var propertyName in NestedNodePropertyNames)
            {
                var nested = GetPropertyValue(node, propertyName);
                if (nested is null)
                {
                    continue;
                }

                if (TryGetNodePathFromObject(nested, out path, out isFolder, depth + 1))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetPathFromCandidate(object candidate, out string path, out bool isFolder)
        {
            path = null;
            isFolder = false;

            if (candidate is FileSystemInfo info)
            {
                if (!TryNormalizePath(info.FullName, out path))
                {
                    return false;
                }

                isFolder = info is DirectoryInfo;
                return true;
            }

            var infoObject = GetPropertyValue(candidate, "Info");
            if (infoObject is FileSystemInfo fileSystemInfo)
            {
                if (!TryNormalizePath(fileSystemInfo.FullName, out path))
                {
                    return false;
                }

                isFolder = fileSystemInfo is DirectoryInfo;
                return true;
            }

            foreach (var propertyName in PathPropertyNames)
            {
                if (!(GetPropertyValue(candidate, propertyName) is string value) || !TryNormalizePath(value, out var normalizedPath))
                {
                    continue;
                }

                path = normalizedPath;
                if (!TryGetFolderHint(candidate, out isFolder))
                {
                    isFolder = IsDirectoryPath(normalizedPath);
                }
                return true;
            }

            return false;
        }

        private static object GetPropertyValue(object instance, string propertyName)
        {
            var type = instance.GetType();
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (property?.CanRead == true && property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(instance);
            }

            var field = type.GetField(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(instance);
        }

        private static bool TryNormalizePath(string path, out string normalizedPath)
        {
            normalizedPath = null;

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            normalizedPath = path.Trim();
            return true;
        }

        private static bool IsDirectoryPath(string path)
        {
            if (Directory.Exists(path))
            {
                return true;
            }

            if (File.Exists(path))
            {
                return false;
            }

            if (!Path.IsPathRooted(path))
            {
                if (!path.Contains(Path.DirectorySeparatorChar.ToString())
                    && !path.Contains(Path.AltDirectorySeparatorChar.ToString())
                    && path.StartsWith(".", StringComparison.Ordinal))
                {
                    return false;
                }

                return !Path.HasExtension(path);
            }

            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal);
        }

        private static bool TryGetFolderHint(object candidate, out bool isFolder)
        {
            isFolder = false;

            foreach (var propertyName in new[] { "IsFolder", "IsDirectory" })
            {
                if (GetPropertyValue(candidate, propertyName) is bool boolValue)
                {
                    isFolder = boolValue;
                    return true;
                }
            }

            foreach (var propertyName in new[] { "Type", "NodeType", "Kind", "ItemType" })
            {
                var value = GetPropertyValue(candidate, propertyName);
                if (value is null)
                {
                    continue;
                }

                var text = value.ToString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (text.IndexOf("Folder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("Directory", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isFolder = true;
                    return true;
                }

                if (text.IndexOf("File", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    isFolder = false;
                    return true;
                }
            }

            return false;
        }
    }
}