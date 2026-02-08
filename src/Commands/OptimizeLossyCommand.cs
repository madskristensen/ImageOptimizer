using System.Collections.Generic;
using System.Linq;

namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Command to optimize images using lossy compression.
    /// Supports single files, folders, projects, and solutions.
    /// Also handles .resx files containing embedded images.
    /// </summary>
    [Command(PackageGuids.guidImageOptimizerCmdSetString, PackageIds.cmdOptimizelossy)]
    internal class OptimizeLossyCommand : BaseCommand<OptimizeLossyCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            IEnumerable<string> images = await OptimizeLosslessCommand.GetImageFilesAsync(e);
            IEnumerable<string> resxFiles = await OptimizeLosslessCommand.GetResxFilesAsync(e);

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
                optimizer.OptimizeImagesAsync(images, CompressionType.Lossy, solution?.FullPath).FireAndForget();
            }

            if (hasResx)
            {
                optimizer.OptimizeResxImagesAsync(resxFiles, CompressionType.Lossy, solution?.FullPath).FireAndForget();
            }
        }
    }
}
