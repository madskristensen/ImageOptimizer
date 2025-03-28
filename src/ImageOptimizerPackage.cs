global using System;
global using System.Threading.Tasks;
global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using System.Threading;

namespace MadsKristensen.ImageOptimizer
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("#110", "#112", Vsix.Version, IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.guidImageOptimizerPkgString)]
    [ProvideUIContextRule(PackageGuids.guidAutoloadImagesString,
        name: "Image files",
        expression: "Images",
        termNames: ["Images"],
        termValues: ["HierSingleSelectionName:.(png|jpg|jpeg|gif|svg)$"])]
    [ProvideUIContextRule(PackageGuids.guidBitmapOnlyUiContextString,
        name: "Image files",
        expression: "Images",
        termNames: ["Images"],
        termValues: ["HierSingleSelectionName:.(png|jpg|jpeg|gif)$"])]
    public sealed class ImageOptimizerPackage : ToolkitPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            _ = await this.RegisterCommandsAsync();
        }
    }
}