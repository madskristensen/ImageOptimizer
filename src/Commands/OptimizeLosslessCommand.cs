using System.Collections.Generic;
using System.IO;
using System.Linq;
using MadsKristensen.ImageOptimizer.Common;
using MadsKristensen.ImageOptimizer.Commands;

namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Command to optimize images using lossless compression.
    /// Supports single files, folders, projects, and solutions.
    /// Also handles .resx files containing embedded images.
    /// </summary>
    [Command(PackageGuids.guidImageOptimizerCmdSetString, PackageIds.cmdOptimizelossless)]
    internal class OptimizeLosslessCommand : BaseCommand<OptimizeLosslessCommand>
    {
        protected override Task InitializeCompletedAsync()
        {
            Command.Supported = false;
            return base.InitializeCompletedAsync();
        }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                Command.Visible = await ShouldShowOptimizeCommandAsync();
                Command.Enabled = Command.Visible;
            });
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            IEnumerable<string> images = await GetImageFilesAsync(e);
            IEnumerable<string> resxFiles = await GetResxFilesAsync(e);
            string selectedFolderPath = await GetSelectedFolderPathAsync(e);

            var hasImages = images.Any();
            var hasResx = resxFiles.Any();

            if (!hasImages && !hasResx)
            {
                await VS.StatusBar.ShowMessageAsync(Constants.NoImagesFoundMessage);
                return;
            }

            Solution solution = await VS.Solutions.GetCurrentSolutionAsync();
            CompressionHandler optimizer = new();

            if (hasImages)
            {
                optimizer.OptimizeImagesAsync(images, CompressionType.Lossless, solution?.FullPath, selectedFolderPath).FireAndForget();
            }

            if (hasResx)
            {
                optimizer.OptimizeResxImagesAsync(resxFiles, CompressionType.Lossless, solution?.FullPath).FireAndForget();
            }
        }

        public static async Task<string> GetSelectedFolderPathAsync(OleMenuCmdEventArgs e)
        {
            if (e.InValue is string arg)
            {
                var path = arg.Trim('"', '\'');

                if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile)
                {
                    path = uri.LocalPath;
                }

                if (Directory.Exists(path))
                {
                    return path;
                }

                if (!Path.IsPathRooted(path) && !Path.HasExtension(path))
                {
                    return path;
                }

                return null;
            }

            if (WorkspaceNodePathResolver.TryGetWorkspaceFilesSelectedFolderPath(out var workspaceFilesFolderPath))
            {
                return workspaceFilesFolderPath;
            }

            IEnumerable<SolutionItem> items = await VS.Solutions.GetActiveItemsAsync();

            foreach (SolutionItem item in items)
            {
                if (item.Type == SolutionItemType.PhysicalFolder)
                {
                    return item.FullPath;
                }

                if (item.Type == SolutionItemType.Project || item.Type == SolutionItemType.Solution)
                {
                    var directory = Path.GetDirectoryName(item.FullPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        return directory;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets all supported image files from the command arguments or selected Solution Explorer items.
        /// </summary>
        public static async Task<IEnumerable<string>> GetImageFilesAsync(OleMenuCmdEventArgs e)
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Check command parameters first
            if (e.InValue is string arg)
            {
                var filePath = arg.Trim('"', '\'');

                if (Compressor.IsFileSupported(filePath) && File.Exists(filePath))
                {
                    _ = files.Add(filePath);
                }
                else if (Directory.Exists(filePath))
                {
                    AddSupportedFilesFromDirectory(filePath, files);
                }
            }
            // Then check selected nodes in Solution Explorer
            else
            {
                IEnumerable<SolutionItem> items = await VS.Solutions.GetActiveItemsAsync();

                foreach (SolutionItem item in items)
                {
                    switch (item.Type)
                    {
                        case SolutionItemType.PhysicalFile:
                            if (Compressor.IsFileSupported(item.FullPath))
                            {
                                _ = files.Add(item.FullPath);
                            }
                            break;

                        case SolutionItemType.PhysicalFolder:
                            AddSupportedFilesFromDirectory(item.FullPath, files);
                            break;

                        case SolutionItemType.Project:
                        case SolutionItemType.Solution:
                            var dir = Path.GetDirectoryName(item.FullPath);
                            if (!string.IsNullOrEmpty(dir))
                            {
                                AddSupportedFilesFromDirectory(dir, files);
                            }
                            break;
                    }
                }
            }

            return files;
        }

        private static void AddSupportedFilesFromDirectory(string directoryPath, HashSet<string> files)
        {
            foreach (var file in FileDiscovery.EnumerateFiles(directoryPath, Compressor.IsFileSupported))
            {
                _ = files.Add(file);
            }
        }

        /// <summary>
        /// Gets all .resx files from the command arguments or selected Solution Explorer items.
        /// </summary>
        public static async Task<IEnumerable<string>> GetResxFilesAsync(OleMenuCmdEventArgs e)
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (e.InValue is string arg)
            {
                var filePath = arg.Trim('"', '\'');
                if (FileUtilities.IsResxFile(filePath) && File.Exists(filePath))
                {
                    _ = files.Add(filePath);
                }
                else if (Directory.Exists(filePath))
                {
                    AddResxFilesFromDirectory(filePath, files);
                }
            }
            else
            {
                IEnumerable<SolutionItem> items = await VS.Solutions.GetActiveItemsAsync();

                foreach (SolutionItem item in items)
                {
                    switch (item.Type)
                    {
                        case SolutionItemType.PhysicalFile:
                            if (FileUtilities.IsResxFile(item.FullPath))
                            {
                                _ = files.Add(item.FullPath);
                            }
                            break;

                        case SolutionItemType.PhysicalFolder:
                            AddResxFilesFromDirectory(item.FullPath, files);
                            break;

                        case SolutionItemType.Project:
                        case SolutionItemType.Solution:
                            var dir = Path.GetDirectoryName(item.FullPath);
                            if (!string.IsNullOrEmpty(dir))
                            {
                                AddResxFilesFromDirectory(dir, files);
                            }
                            break;
                    }
                }
            }

            return files;
        }

        private static void AddResxFilesFromDirectory(string directoryPath, HashSet<string> files)
        {
            foreach (var file in FileDiscovery.EnumerateFiles(directoryPath, FileUtilities.IsResxFile))
            {
                _ = files.Add(file);
            }
        }

        internal static async Task<bool> ShouldShowOptimizeCommandAsync()
        {
            IEnumerable<SolutionItem> items = await VS.Solutions.GetActiveItemsAsync();

            var hasFileSelection = false;
            var hasSupportedFileSelection = false;
            var hasFolderLikeSelection = false;

            foreach (SolutionItem item in items)
            {
                switch (item.Type)
                {
                    case SolutionItemType.PhysicalFile:
                        hasFileSelection = true;
                        if (Compressor.IsFileSupported(item.FullPath) || FileUtilities.IsResxFile(item.FullPath))
                        {
                            hasSupportedFileSelection = true;
                        }
                        break;

                    case SolutionItemType.PhysicalFolder:
                        hasFolderLikeSelection = true;
                        break;

                    case SolutionItemType.Project:
                    case SolutionItemType.Solution:
                        if (!hasFileSelection)
                        {
                            hasFolderLikeSelection = true;
                        }
                        break;
                }
            }

            if (hasFileSelection)
            {
                return hasSupportedFileSelection;
            }

            return hasFolderLikeSelection;
        }
    }
}
