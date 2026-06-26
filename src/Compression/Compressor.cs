using System.Diagnostics;
using System.IO;
using BracketPipe;
using MadsKristensen.ImageOptimizer.Common;

namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Handles image compression using external tools (pingo, gifsicle) and built-in SVG minification.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Compressor"/> class with specified timeout and quality.
    /// </remarks>
    /// <param name="processTimeoutMs">The process timeout in milliseconds.</param>
    /// <param name="lossyQuality">The quality level for lossy compression (60-100).</param>
    public class Compressor(int processTimeoutMs, int lossyQuality)
    {
        private static readonly string _cwd = Path.Combine(Path.GetDirectoryName(typeof(Compressor).Assembly.Location), @"Resources\Tools\");
        private readonly int _processTimeoutMs = processTimeoutMs > 0 ? processTimeoutMs : Constants.DefaultProcessTimeoutMs;
        private readonly int _lossyQuality = Math.Max(60, Math.Min(lossyQuality, 100));

        /// <summary>
        /// Initializes a new instance of the <see cref="Compressor"/> class with default timeout and quality.
        /// </summary>
        public Compressor() : this(Constants.DefaultProcessTimeoutMs, Constants.DefaultLossyQuality)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Compressor"/> class with specified timeout.
        /// </summary>
        /// <param name="processTimeoutMs">The process timeout in milliseconds.</param>
        public Compressor(int processTimeoutMs) : this(processTimeoutMs, Constants.DefaultLossyQuality)
        {
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
            if (!TryGetCompressionCommand(sourceFile, targetFile, type, out var executablePath, out var arguments))
            {
                return;
            }

            RunTool(executablePath, arguments, sourceFile);
        }

        /// <summary>
        /// Runs an external command-line tool, enforcing the configured timeout and
        /// capturing standard error / exit code for diagnostics.
        /// </summary>
        /// <param name="executablePath">The full path to the tool executable.</param>
        /// <param name="arguments">The command-line arguments to pass to the tool.</param>
        /// <param name="sourceFile">The source image file being processed (used for error messages).</param>
        /// <exception cref="TimeoutException">Thrown when the tool does not exit within the configured timeout.</exception>
        private void RunTool(string executablePath, string arguments, string sourceFile)
        {
            var processStartInfo = new ProcessStartInfo(executablePath)
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = _cwd,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                return;
            }

            // Read the output streams asynchronously so the child process never blocks
            // when its stdout/stderr buffers fill up while we wait for it to exit.
            Task<string> stdErrTask = process.StandardError.ReadToEndAsync();
            Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync();

            if (!process.WaitForExit(_processTimeoutMs))
            {
                KillProcessSafely(process, sourceFile);
            }

            if (process.ExitCode != 0)
            {
                var error = SafeGetResult(stdErrTask);
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = SafeGetResult(stdOutTask);
                }

                var message = $"{Path.GetFileName(executablePath)} exited with code {process.ExitCode} while processing {Path.GetFileName(sourceFile)}.";
                if (!string.IsNullOrWhiteSpace(error))
                {
                    message += $" {error.Trim()}";
                }

                new Exception(message).LogAsync().FireAndForget();
            }
        }

        private static string SafeGetResult(Task<string> task)
        {
            try
            {
                return task?.Result ?? string.Empty;
            }
            catch
            {
                return string.Empty;
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

        private bool TryGetCompressionCommand(string sourceFile, string targetFile, CompressionType type, out string executablePath, out string arguments)
        {
            executablePath = null;
            arguments = null;

            if (!ErrorHandler.ValidateFilePath(sourceFile, out _))
            {
                return false;
            }

            var ext = Path.GetExtension(sourceFile).ToLowerInvariant();

            switch (ext)
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".webp":
                    arguments = GetPingoArguments(sourceFile, targetFile, type, ext);
                    executablePath = GetToolPath("pingo.exe");
                    break;

                case ".gif":
                    arguments = GetGifsicleArguments(sourceFile, targetFile, type);
                    executablePath = GetToolPath("gifsicle.exe");
                    break;

                default:
                    return false;
            }

            return !string.IsNullOrEmpty(arguments);
        }

        private string GetPingoArguments(string sourceFile, string targetFile, CompressionType type, string extension)
        {
            if (!FileUtilities.SafeCopyFile(sourceFile, targetFile))
            {
                return null;
            }

            if (type is CompressionType.Lossy)
            {
                // Lossy: -s4 (max effort) + explicit quality for best compression.
                return $"-s4 -quality={_lossyQuality} \"{targetFile}\"";
            }

            // Lossless: -s4 gives the smallest PNG/WebP output. JPEG output is
            // identical at -s3 and -s4, so -s3 is used for JPEG (slightly faster).
            var optimizationLevel = (extension == ".jpg" || extension == ".jpeg") ? "s3" : "s4";
            return $"-lossless -{optimizationLevel} \"{targetFile}\"";
        }

        private string GetGifsicleArguments(string sourceFile, string targetFile, CompressionType type)
        {
            // -O3 is gifsicle's highest optimization level (used for both modes).
            if (type is CompressionType.Lossy)
            {
                // Map the 60-100 quality scale to gifsicle's lossiness value
                // (0 = no loss, higher = more loss). quality 100 -> 0, quality 60 -> 80.
                var lossiness = (Constants.MaxLossyQuality - _lossyQuality) * 2;
                return $"-O3 --lossy={lossiness} \"{sourceFile}\" --output=\"{targetFile}\"";
            }

            return $"-O3 \"{sourceFile}\" --output=\"{targetFile}\"";
        }

        private static string GetToolPath(string executableName)
        {
            return Path.Combine(_cwd, executableName);
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

        /// <summary>
        /// Checks if a file can be converted to WebP (PNG and JPEG only).
        /// </summary>
        /// <param name="fileName">The file path to check.</param>
        /// <returns>True if the file can be converted to WebP; otherwise, false.</returns>
        public static bool IsConvertibleToWebp(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var ext = Path.GetExtension(fileName);
            return !string.IsNullOrEmpty(ext) && Constants.ConvertibleToWebpExtensions.Contains(ext);
        }

        /// <summary>
        /// Converts an image file to WebP format using pingo.
        /// </summary>
        /// <param name="fileName">The path to the source image file (PNG or JPEG).</param>
        /// <returns>A <see cref="CompressionResult"/> with the WebP file as the result.</returns>
        /// <exception cref="ArgumentException">Thrown when the file path is invalid or not convertible.</exception>
        public CompressionResult ConvertToWebp(string fileName)
        {
            ValidationResult validation = InputValidator.ValidateFilePath(fileName);
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage, nameof(fileName));
            }

            var validatedPath = validation.GetValue<string>();
            if (!IsConvertibleToWebp(validatedPath))
            {
                throw new ArgumentException($"File type not supported for WebP conversion: {Path.GetExtension(validatedPath)}", nameof(fileName));
            }

            // pingo -webp creates a .webp file alongside the input, so we work on a temp copy
            var tempSource = FileUtilities.CreateTempFileWithExtension(validatedPath);
            var expectedWebpFile = Path.ChangeExtension(tempSource, ".webp");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (!FileUtilities.SafeCopyFile(validatedPath, tempSource))
                {
                    return new CompressionResult(validatedPath, validatedPath, stopwatch.Elapsed);
                }

                // -s4 (max effort) yields smaller WebP than the default for JPEG sources.
                var arguments = $"-webp -s4 -quality={_lossyQuality} \"{tempSource}\"";

                RunTool(GetToolPath("pingo.exe"), arguments, validatedPath);
            }
            catch (TimeoutException ex)
            {
                FileUtilities.SafeDeleteFile(tempSource);
                FileUtilities.SafeDeleteFile(expectedWebpFile);
                ex.LogAsync().FireAndForget();
                return new CompressionResult(validatedPath, validatedPath, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                FileUtilities.SafeDeleteFile(tempSource);
                FileUtilities.SafeDeleteFile(expectedWebpFile);
                ex.LogAsync().FireAndForget();
                return new CompressionResult(validatedPath, validatedPath, stopwatch.Elapsed);
            }
            finally
            {
                // Always clean up the temp source copy
                FileUtilities.SafeDeleteFile(tempSource);
                stopwatch.Stop();
            }

            // pingo creates the .webp file next to the source
            return new CompressionResult(validatedPath, expectedWebpFile, stopwatch.Elapsed);
        }
    }
}
