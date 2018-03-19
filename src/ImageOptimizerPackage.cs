using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace MadsKristensen.ImageOptimizer
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", Vsix.Version, IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(UIContextGuids80.SolutionHasSingleProject)]
    [ProvideAutoLoad(UIContextGuids80.SolutionHasMultipleProjects)]
    [Guid(PackageGuids.guidImageOptimizerPkgString)]
    public sealed class ImageOptimizerPackage : Package
    {
        protected override void Initialize()
        {
            Logger.Initialize(this, Vsix.Name);
            OptimizeCommand.Initialize(this);
        }
    }
}