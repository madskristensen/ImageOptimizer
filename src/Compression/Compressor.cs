using System.Diagnostics;
using System.IO;
using BracketPipe;
using MadsKristensen.ImageOptimizer.Common;

namespace MadsKristensen.ImageOptimizer
{
    public class Compressor
    {
        private static readonly string _cwd = Path.Combine(Path.GetDirectoryName(typeof(Compressor).Assembly.Location), @"Resources\Tools\");

        public CompressionResult CompressFile(string fileName, CompressionType type)
        {
            // Validate input
            ValidationResult validation = InputValidator.ValidateFilePath(fileName);
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage, nameof(fileName));
            }

            var validatedPath = validation.GetValue<string>();
            var fileExtension = Path.GetExtension(validatedPath);
            var targetFile = FileUtilities.CreateTempFileWithExtension(validatedPath);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (fileExtension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    CompressSvgFile(validatedPath, targetFile);
                }
                else
                {
                    CompressImageFile(validatedPath, targetFile, type);
                }
            }
            catch (Exception)
            {
                // Clean up temp file on error
                _ = FileUtilities.SafeDeleteFile(targetFile);
                return new CompressionResult(validatedPath, targetFile, stopwatch.Elapsed);
            }
            finally
            {
                stopwatch.Stop();
            }

            return new CompressionResult(validatedPath, targetFile, stopwatch.Elapsed);
        }

        private static void CompressSvgFile(string sourceFile, string targetFile)
        {
            ErrorHandler.SafeExecute(() =>
            {
                var source = File.ReadAllText(sourceFile);
                string minified = Html.Minify(source);
                File.WriteAllText(targetFile, minified);
            });
        }

        private static void CompressImageFile(string sourceFile, string targetFile, CompressionType type)
        {
            var arguments = GetCompressionArguments(sourceFile, targetFile, type);
            if (string.IsNullOrEmpty(arguments))
            {
                return;
            }

            var processStartInfo = new ProcessStartInfo(Constants.CommandExecutor)
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = _cwd,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var process = Process.Start(processStartInfo);
            if (process != null && !process.WaitForExit(Constants.ProcessTimeoutMs))
            {
                KillProcessSafely(process, sourceFile);
            }
        }

        private static void KillProcessSafely(Process process, string sourceFile)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    _ = process.WaitForExit(5000); // Give it 5 seconds to die gracefully
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited
            }
            catch (Exception ex)
            {
                ex.LogAsync().FireAndForget();
            }

            throw new TimeoutException($"Process timed out after {Constants.ProcessTimeoutMs}ms while compressing {sourceFile}");
        }

        private static string GetCompressionArguments(string sourceFile, string targetFile, CompressionType type)
        {
            if (!ErrorHandler.ValidateFilePath(sourceFile, out _))
            {
                return null;
            }

            var ext = Path.GetExtension(sourceFile).ToLowerInvariant();

            return ext switch
            {
                ".png" or ".jpg" or ".jpeg" or ".webp" => GetPingoArguments(sourceFile, targetFile, type),
                ".gif" => GetGifsicleArguments(sourceFile, targetFile, type),
                _ => null
            };
        }

        private static string GetPingoArguments(string sourceFile, string targetFile, CompressionType type)
        {
            return !FileUtilities.SafeCopyFile(sourceFile, targetFile)
                ? null
                : type is CompressionType.Lossy
                ? $"/c pingo -s4 -q \"{targetFile}\""
                : $"/c pingo -lossless -s4 -q \"{targetFile}\"";
        }

        private static string GetGifsicleArguments(string sourceFile, string targetFile, CompressionType type)
        {
            return type is CompressionType.Lossy
                ? $"/c gifsicle -O3 --lossy \"{sourceFile}\" --output=\"{targetFile}\""
                : $"/c gifsicle -O3 \"{sourceFile}\" --output=\"{targetFile}\"";
        }

        public static bool IsFileSupported(string fileName)
        {
            return !string.IsNullOrWhiteSpace(fileName) && FileUtilities.IsImageFileSupported(fileName);
        }
    }
}
