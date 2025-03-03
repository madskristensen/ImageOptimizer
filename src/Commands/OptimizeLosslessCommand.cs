using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MadsKristensen.ImageOptimizer
{
    [Command(PackageGuids.guidImageOptimizerCmdSetString, PackageIds.cmdOptimizelossless)]
    internal class OptimizeLosslessCommand : BaseCommand<OptimizeLosslessCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            IEnumerable<string> images = await GetImageFilesAsync(e);

            if (!images.Any())
            {
                await VS.StatusBar.ShowMessageAsync("No images found to optimize");
            }
            else
            {
                Solution solution = await VS.Solutions.GetCurrentSolutionAsync();
                CompressionHandler optimizer = new();
                optimizer.OptimizeImagesAsync(images, CompressionType.Lossless, solution.FullPath).FireAndForget();
            }
        }

        public static async Task<IEnumerable<string>> GetImageFilesAsync(OleMenuCmdEventArgs e)
        {
            List<string> files = [];

            // Check command parameters first
            if (e.InValue is string arg)
            {
                var filePath = arg.Trim('"', '\'');

                if (Compressor.IsFileSupported(filePath) && File.Exists(filePath))
                {
                    files.Add(filePath);
                }
            }

            // Then check selected nodes in Solution Explorer
            else
            {
                IEnumerable<SolutionItem> items = await VS.Solutions.GetActiveItemsAsync();

                foreach (SolutionItem item in items)
                {
                    if (item.Type == SolutionItemType.PhysicalFile)
                    {
                        files.Add(item.FullPath);
                    }
                    else if (item.Type == SolutionItemType.PhysicalFolder)
                    {
                        IEnumerable<string> supportedFiles = Directory.EnumerateFiles(item.FullPath, "*.*", SearchOption.AllDirectories).Where(Compressor.IsFileSupported);
                        files.AddRange(supportedFiles);
                    }
                    else if (item.Type is SolutionItemType.Project or SolutionItemType.Solution)
                    {
                        var dir = Path.GetDirectoryName(item.FullPath);
                        IEnumerable<string> supportedFiles = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories).Where(Compressor.IsFileSupported);
                        files.AddRange(supportedFiles);
                    }
                }
            }

            return files;
        }
    }
}
