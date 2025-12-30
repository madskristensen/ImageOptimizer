using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Command to optimize images using lossless compression.
    /// Supports single files, folders, projects, and solutions.
    /// </summary>
    [Command(PackageGuids.guidImageOptimizerCmdSetString, PackageIds.cmdOptimizelossless)]
    internal class OptimizeLosslessCommand : BaseCommand<OptimizeLosslessCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            IEnumerable<string> images = await GetImageFilesAsync(e);

            if (!images.Any())
            {
                await VS.StatusBar.ShowMessageAsync(Constants.NoImagesFoundMessage);
            }
            else
            {
                Solution solution = await VS.Solutions.GetCurrentSolutionAsync();
                CompressionHandler optimizer = new();
                optimizer.OptimizeImagesAsync(images, CompressionType.Lossless, solution.FullPath).FireAndForget();
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
    }
}
