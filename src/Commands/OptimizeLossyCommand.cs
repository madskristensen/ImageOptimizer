using System.Collections.Generic;
using System.Linq;

namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Command to optimize images using lossy compression.
    /// Supports single files, folders, projects, and solutions.
    /// </summary>
    [Command(PackageGuids.guidImageOptimizerCmdSetString, PackageIds.cmdOptimizelossy)]
    internal class OptimizeLossyCommand : BaseCommand<OptimizeLossyCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            IEnumerable<string> images = await OptimizeLosslessCommand.GetImageFilesAsync(e);

            if (!images.Any())
            {
                await VS.StatusBar.ShowMessageAsync(Constants.NoImagesFoundMessage);
            }
            else
            {
                Solution solution = await VS.Solutions.GetCurrentSolutionAsync();
                CompressionHandler optimizer = new();
                optimizer.OptimizeImagesAsync(images, CompressionType.Lossy, solution?.FullPath).FireAndForget();
            }
        }
    }
}
