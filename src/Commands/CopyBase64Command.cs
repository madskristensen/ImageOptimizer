using System.IO;
using System.Windows;

namespace MadsKristensen.ImageOptimizer
{
    [Command(PackageGuids.guidImageOptimizerCmdSetString, PackageIds.cmdCopyDataUri)]
    public class CopyBase64Command : BaseCommand<CopyBase64Command>
    {
        protected override Task InitializeCompletedAsync()
        {
            Command.Supported = false;
            return base.InitializeCompletedAsync();
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            if (await VS.Solutions.GetActiveItemAsync() is PhysicalFile file)
            {
                var base64 = Base64Helpers.CreateBase64ImageString(file.FullPath);
                Clipboard.SetText(base64);

                await VS.StatusBar.ShowMessageAsync("Base64 DataURI copied to clipboard (" + base64.Length + " characters)");
            }
        }
    }

    public static class Base64Helpers
    {
        public static string CreateBase64ImageString(string imageFile)
        {
            return "data:"
                        + GetMimeTypeFromFileExtension(imageFile)
                        + ";base64,"
                        + Convert.ToBase64String(File.ReadAllBytes(imageFile));
        }

        private static string GetMimeTypeFromFileExtension(string file)
        {
            var ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();

            return ext switch
            {
                "jpg" or "jpeg" => "image/jpeg",
                "svg" => "image/svg+xml",
                _ => "image/" + ext,
            };
        }
    }
}
