using System.Diagnostics;
using System.IO;
using BracketPipe;
using MadsKristensen.ImageOptimizer.Common;

namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Handles image compression using external tools (pingo, gifsicle) and built-in SVG minification.
    /// </summary>
    public class Compressor
    {
        private static readonly string _cwd = Path.Combine(Path.GetDirectoryName(typeof(Compressor).Assembly.Location), @"Resources\Tools\");
        private readonly int _processTimeoutMs;

        /// <summary>
        /// Initializes a new instance of the <see cref="Compressor"/> class with default timeout.
        /// </summary>
        public Compressor() : this(Constants.DefaultProcessTimeoutMs)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Compressor"/> class with specified timeout.
        /// </summary>
        /// <param name="processTimeoutMs">The process timeout in milliseconds.</param>
        public Compressor(int processTimeoutMs)
        {
            _processTimeoutMs = processTimeoutMs > 0 ? processTimeoutMs : Constants.DefaultProcessTimeoutMs;
        }

        /// <summary>
        /// Compresses a single image file.
        /// </summary>
        /// <param name="fileName">The path to the image file.</param>
        /// <param name="type">The type of compression to apply.</param>
        /// <returns>A <see cref="CompressionResult"/> containing the compression outcome.</returns>
        /// <exception cref="ArgumentException">Thrown when the file path is invalid.</exception>
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
            catch (TimeoutException ex)
            {
                // Clean up temp file on timeout
                FileUtilities.SafeDeleteFile(targetFile);
                ex.LogAsync().FireAndForget();
                return new CompressionResult(validatedPath, targetFile, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                // Clean up temp file on error
                FileUtilities.SafeDeleteFile(targetFile);
                ex.LogAsync().FireAndForget();
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

        private void CompressImageFile(string sourceFile, string targetFile, CompressionType type)
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
            if (process != null && !process.WaitForExit(_processTimeoutMs))
            {
                KillProcessSafely(process, sourceFile);
            }
        }

        private void KillProcessSafely(Process process, string sourceFile)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(Constants.ProcessKillGracePeriodMs);
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited - this is fine
            }
            catch (Exception ex)
            {
                ex.LogAsync().FireAndForget();
            }

            throw new TimeoutException($"Process timed out after {_processTimeoutMs}ms while compressing {Path.GetFileName(sourceFile)}");
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

        /// <summary>
        /// Checks if a file is supported for optimization.
        /// </summary>
        /// <param name="fileName">The file path to check.</param>
        /// <returns>True if the file is supported; otherwise, false.</returns>
        public static bool IsFileSupported(string fileName)
        {
            return !string.IsNullOrWhiteSpace(fileName) && FileUtilities.IsImageFileSupported(fileName);
        }
    }
}
