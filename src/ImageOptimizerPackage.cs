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
    [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Image Optimizer", "General", 0, 0, true, SupportsProfiles = true)]
    [ProvideUIContextRule(PackageGuids.guidAutoloadImagesString,
        name: "Image files",
        expression: "Images",
        termNames: ["Images"],
        termValues: ["HierSingleSelectionName:.(png|jpg|jpeg|gif|svg|webp|avif)$"])]
    [ProvideUIContextRule(PackageGuids.guidBitmapOnlyUiContextString,
        name: "Image files",
        expression: "Images",
        termNames: ["Images"],
        termValues: ["HierSingleSelectionName:.(png|jpg|jpeg|gif|avif)$"])]
    [ProvideUIContextRule(PackageGuids.guidConvertibleToWebpUiContextString,
        name: "Convertible to WebP",
        expression: "Images",
        termNames: ["Images"],
        termValues: ["HierSingleSelectionName:.(png|jpg|jpeg)$"])]
    [ProvideUIContextRule(PackageGuids.guidConvertibleToAvifUiContextString,
        name: "Convertible to AVIF",
        expression: "Images",
        termNames: ["Images"],
        termValues: ["HierSingleSelectionName:.(png|jpg|jpeg)$"])]

    [ProvideFileIcon(".webp", "KnownMonikers.Image")]
    [ProvideFileIcon(".avif", "KnownMonikers.Image")]
    public sealed class ImageOptimizerPackage : ToolkitPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.RegisterCommandsAsync();
        }
    }
}