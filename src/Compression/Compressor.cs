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

            var processStartInfo = new ProcessStartInfo(executablePath)
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = _cwd,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
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

                case ".avif":
                    arguments = GetAvifencOptimizeArguments(sourceFile, targetFile, type);
                    executablePath = GetToolPath("avifenc.exe");
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
                // Lossy: use quality parameter for better control
                return $"-s4 -quality={_lossyQuality} -q \"{targetFile}\"";
            }

            // Lossless: use -s3 for JPEG (optimal per pingo docs), -s4 for others
            var optimizationLevel = (extension == ".jpg" || extension == ".jpeg") ? "s3" : "s4";
            return $"-lossless -{optimizationLevel} -q \"{targetFile}\"";
        }

        private static string GetGifsicleArguments(string sourceFile, string targetFile, CompressionType type)
        {
            return type is CompressionType.Lossy
                ? $"-O3 --lossy \"{sourceFile}\" --output=\"{targetFile}\""
                : $"-O3 \"{sourceFile}\" --output=\"{targetFile}\"";
        }

        private string GetAvifencOptimizeArguments(string sourceFile, string targetFile, CompressionType type)
        {
            // avifenc reads the source and writes to a separate output file
            if (type is CompressionType.Lossy)
            {
                return $"-q {_lossyQuality} -s 6 -j all \"{sourceFile}\" \"{targetFile}\"";
            }

            return $"--lossless -s 6 -j all \"{sourceFile}\" \"{targetFile}\"";
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

                var arguments = $"-webp -quality={_lossyQuality} -q \"{tempSource}\"";

                var processStartInfo = new ProcessStartInfo(GetToolPath("pingo.exe"))
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = _cwd,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process != null && !process.WaitForExit(_processTimeoutMs))
                {
                    KillProcessSafely(process, validatedPath);
                }
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

        /// <summary>
        /// Checks if a file can be converted to AVIF (PNG and JPEG only).
        /// </summary>
        /// <param name="fileName">The file path to check.</param>
        /// <returns>True if the file can be converted to AVIF; otherwise, false.</returns>
        public static bool IsConvertibleToAvif(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var ext = Path.GetExtension(fileName);
            return !string.IsNullOrEmpty(ext) && Constants.ConvertibleToAvifExtensions.Contains(ext);
        }

        /// <summary>
        /// Converts an image file to AVIF format using avifenc.
        /// </summary>
        /// <param name="fileName">The path to the source image file (PNG or JPEG).</param>
        /// <returns>A <see cref="CompressionResult"/> with the AVIF file as the result.</returns>
        /// <exception cref="ArgumentException">Thrown when the file path is invalid or not convertible.</exception>
        public CompressionResult ConvertToAvif(string fileName)
        {
            ValidationResult validation = InputValidator.ValidateFilePath(fileName);
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage, nameof(fileName));
            }

            var validatedPath = validation.GetValue<string>();
            if (!IsConvertibleToAvif(validatedPath))
            {
                throw new ArgumentException($"File type not supported for AVIF conversion: {Path.GetExtension(validatedPath)}", nameof(fileName));
            }

            // avifenc writes to a specified output file, so use a temp .avif path
            var targetAvifFile = Path.ChangeExtension(Path.GetTempFileName(), ".avif");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var arguments = $"-q {_lossyQuality} -s 6 -j all \"{validatedPath}\" \"{targetAvifFile}\"";

                var processStartInfo = new ProcessStartInfo(GetToolPath("avifenc.exe"))
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = _cwd,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process != null && !process.WaitForExit(_processTimeoutMs))
                {
                    KillProcessSafely(process, validatedPath);
                }
            }
            catch (TimeoutException ex)
            {
                FileUtilities.SafeDeleteFile(targetAvifFile);
                ex.LogAsync().FireAndForget();
                return new CompressionResult(validatedPath, validatedPath, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                FileUtilities.SafeDeleteFile(targetAvifFile);
                ex.LogAsync().FireAndForget();
                return new CompressionResult(validatedPath, validatedPath, stopwatch.Elapsed);
            }
            finally
            {
                stopwatch.Stop();
            }

            return new CompressionResult(validatedPath, targetAvifFile, stopwatch.Elapsed);
        }
    }
}
