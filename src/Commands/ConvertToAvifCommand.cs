using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Command to convert PNG and JPEG images to AVIF format.
    /// Supports single files, folders, projects, and solutions.
    /// </summary>
    [Command(PackageGuids.guidImageOptimizerCmdSetString, PackageIds.cmdConvertToAvif)]
    internal class ConvertToAvifCommand : BaseCommand<ConvertToAvifCommand>
    {
        protected override Task InitializeCompletedAsync()
        {
            Command.Supported = false;
            return base.InitializeCompletedAsync();
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            IEnumerable<string> files = await GetConvertibleFilesAsync();

            if (!files.Any())
            {
                await VS.StatusBar.ShowMessageAsync(Constants.NoConvertibleImagesMessage);
            }
            else
            {
                ConversionHandler handler = new();
                handler.ConvertToAvifAsync(files).FireAndForget();
            }
        }

        /// <summary>
        /// Gets all AVIF-convertible image files from the selected Solution Explorer items.
        /// </summary>
        private static async Task<IEnumerable<string>> GetConvertibleFilesAsync()
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IEnumerable<SolutionItem> items = await VS.Solutions.GetActiveItemsAsync();

            foreach (SolutionItem item in items)
            {
                switch (item.Type)
                {
                    case SolutionItemType.PhysicalFile:
                        if (Compressor.IsConvertibleToAvif(item.FullPath))
                        {
                            files.Add(item.FullPath);
                        }
                        break;

                    case SolutionItemType.PhysicalFolder:
                        AddConvertibleFilesFromDirectory(item.FullPath, files);
                        break;

                    case SolutionItemType.Project:
                    case SolutionItemType.Solution:
                        var dir = Path.GetDirectoryName(item.FullPath);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            AddConvertibleFilesFromDirectory(dir, files);
                        }
                        break;
                }
            }

            return files;
        }

        private static void AddConvertibleFilesFromDirectory(string directoryPath, HashSet<string> files)
        {
            try
            {
                IEnumerable<string> convertibleFiles = Directory.EnumerateFiles(directoryPath, Constants.AllFilesPattern, SearchOption.AllDirectories)
                    .Where(Compressor.IsConvertibleToAvif);

                foreach (var file in convertibleFiles)
                {
                    files.Add(file);
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
