using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MadsKristensen.ImageOptimizer.Common;

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
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            IEnumerable<string> images = await GetImageFilesAsync(e);
            IEnumerable<string> resxFiles = await GetResxFilesAsync(e);

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
                optimizer.OptimizeImagesAsync(images, CompressionType.Lossless, solution?.FullPath).FireAndForget();
            }

            if (hasResx)
            {
                optimizer.OptimizeResxImagesAsync(resxFiles, CompressionType.Lossless, solution?.FullPath).FireAndForget();
            }
        }

        /// <summary>
        /// Gets all supported image files from the command arguments or selected Solution Explorer items.
        /// </summary>
        public static async Task<IEnumerable<string>> GetImageFilesAsync(OleMenuCmdEventArgs e)
        {
            // Use thread-safe ConcurrentDictionary as a set (value is ignored)
            var files = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

            // Check command parameters first
            if (e.InValue is string arg)
            {
                var filePath = arg.Trim('"', '\'');

                if (Compressor.IsFileSupported(filePath) && File.Exists(filePath))
                {
                    files.TryAdd(filePath, 0);
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
                                files.TryAdd(item.FullPath, 0);
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

            return files.Keys;
        }

        private static void AddSupportedFilesFromDirectory(string directoryPath, ConcurrentDictionary<string, byte> files)
        {
            try
            {
                // Sequential enumeration is more efficient for I/O-bound operations
                // as PLINQ adds overhead without benefit for file system access
                IEnumerable<string> supportedFiles = Directory.EnumerateFiles(directoryPath, Constants.AllFilesPattern, SearchOption.AllDirectories)
                    .Where(Compressor.IsFileSupported);

                foreach (var file in supportedFiles)
                {
                    files.TryAdd(file, 0);
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
        }

        /// <summary>
        /// Gets all .resx files from the command arguments or selected Solution Explorer items.
        /// </summary>
        public static async Task<IEnumerable<string>> GetResxFilesAsync(OleMenuCmdEventArgs e)
        {
            var files = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

            if (e.InValue is string arg)
            {
                var filePath = arg.Trim('"', '\'');
                if (FileUtilities.IsResxFile(filePath) && File.Exists(filePath))
                {
                    files.TryAdd(filePath, 0);
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
                                files.TryAdd(item.FullPath, 0);
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

            return files.Keys;
        }

        private static void AddResxFilesFromDirectory(string directoryPath, ConcurrentDictionary<string, byte> files)
        {
            try
            {
                IEnumerable<string> resxFiles = Directory.EnumerateFiles(directoryPath, "*" + Constants.ResxExtension, SearchOption.AllDirectories);

                foreach (var file in resxFiles)
                {
                    files.TryAdd(file, 0);
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
        }
    }
}
