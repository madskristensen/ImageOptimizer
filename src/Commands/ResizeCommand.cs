using System.Collections.Generic;
using System.Linq;
using System.Windows;
using EnvDTE;
using MadsKristensen.ImageOptimizer.Resizing;

namespace MadsKristensen.ImageOptimizer
{
    [Command(PackageGuids.guidImageOptimizerCmdSetString, PackageIds.cmdResize)]
    public class ResizeCommand : BaseCommand<ResizeCommand>
    {
        protected override Task InitializeCompletedAsync()
        {
            Command.Supported = false;
            return base.InitializeCompletedAsync();
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            IEnumerable<SolutionItem> items = await VS.Solutions.GetActiveItemsAsync();

            if (items.FirstOrDefault() is PhysicalFile file && Compressor.IsFileSupported(file.FullPath))
            {
                ResizingDialog resizer = new(file.FullPath);
                resizer.ShowModal();
            }
        }
    }
}
