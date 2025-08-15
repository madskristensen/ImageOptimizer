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
        private const int _processTimeoutMs = 60000; // 60 seconds timeout

        public CompressionResult CompressFile(string fileName, CompressionType type)
        {
            var fileExtension = Path.GetExtension(fileName);
            var targetFile = Path.ChangeExtension(Path.GetTempFileName(), fileExtension);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (fileExtension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    CompressSvgFile(fileName, targetFile);
                }
                else
                {
                    CompressImageFile(fileName, targetFile, type);
                }
            }
            catch (Exception)
            {
                // Clean up temp file on error
                SafeDeleteFile(targetFile);
                return new CompressionResult(fileName, targetFile, stopwatch.Elapsed);
            }
            finally
            {
                stopwatch.Stop();
            }

            return new CompressionResult(fileName, targetFile, stopwatch.Elapsed);
        }

        private static void CompressSvgFile(string sourceFile, string targetFile)
        {
            var source = File.ReadAllText(sourceFile);
            string minified = Html.Minify(source);
            File.WriteAllText(targetFile, minified);
        }

        private static void CompressImageFile(string sourceFile, string targetFile, CompressionType type)
        {
            var arguments = GetArguments(sourceFile, targetFile, type);
            if (string.IsNullOrEmpty(arguments))
            {
                return;
            }

            var processStartInfo = new ProcessStartInfo("cmd")
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = _cwd,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true, // Capture errors for better debugging
                RedirectStandardOutput = true
            };

            using var process = Process.Start(processStartInfo);
            if (process != null)
            {
                // Add timeout to prevent hanging processes
                if (!process.WaitForExit(_processTimeoutMs))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                        // Process already exited
                    }
                    throw new TimeoutException($"Process timed out after {_processTimeoutMs}ms while compressing {sourceFile}");
                }
            }
        }

        private static void SafeDeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        private static string GetArguments(string sourceFile, string targetFile, CompressionType type)
        {
            if (!File.Exists(sourceFile))
            {
                return null;
            }

            var ext = Path.GetExtension(sourceFile).ToLowerInvariant();

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
