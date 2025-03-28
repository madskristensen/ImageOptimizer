using System.Diagnostics;
using System.IO;
using System.Linq;
using BracketPipe;

namespace MadsKristensen.ImageOptimizer
{
    public class Compressor
    {
        private static readonly string[] _supported = [".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp"];
        private static readonly string _cwd = Path.Combine(Path.GetDirectoryName(typeof(Compressor).Assembly.Location), @"Resources\Tools\");

        public CompressionResult CompressFile(string fileName, CompressionType type)
        {
            var fileExtension = Path.GetExtension(fileName);
            var targetFile = Path.ChangeExtension(Path.GetTempFileName(), fileExtension);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (fileExtension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    var source = File.ReadAllText(fileName);
                    string minified = Html.Minify(source);
                    File.WriteAllText(targetFile, minified);
                }
                else
                {
                    var start = new ProcessStartInfo("cmd")
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        WorkingDirectory = _cwd,
                        Arguments = GetArguments(fileName, targetFile, type),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };

                    using (var process = Process.Start(start))
                    {
                        process.WaitForExit();
                    }
                }
            }
            catch (Exception)
            {
                return new CompressionResult(fileName, targetFile, stopwatch.Elapsed);
            }
            finally
            {
                stopwatch.Stop();
            }

            return new CompressionResult(fileName, targetFile, stopwatch.Elapsed);
        }

        private static string GetArguments(string sourceFile, string targetFile, CompressionType type)
        {
            if (!Uri.IsWellFormedUriString(sourceFile, UriKind.RelativeOrAbsolute) && !File.Exists(sourceFile))
            {
                return null;
            }

            var ext = Path.GetExtension(sourceFile).ToLowerInvariant(); ;

            switch (ext)
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".webp":
                    File.Copy(sourceFile, targetFile, true);
                    return type is CompressionType.Lossy ? $"/c pingo -s4 -q \"{targetFile}\"" : $"/c pingo -lossless -s4 -q \"{targetFile}\"";

                case ".gif":
                    return type is CompressionType.Lossy
                        ? $"/c gifsicle -O3 --lossy \"{sourceFile}\" --output=\"{targetFile}\""
                        : $"/c gifsicle -O3 \"{sourceFile}\" --output=\"{targetFile}\"";

                default:
                    return null;
            }
        }

        public static bool IsFileSupported(string fileName)
        {
            var ext = Path.GetExtension(fileName);
            return _supported.Any(s => s.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }
    }
}
