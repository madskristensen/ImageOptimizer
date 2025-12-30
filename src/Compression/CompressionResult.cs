using System.IO;
using System.Text;

namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Represents the result of a compression operation on a single file.
    /// </summary>
    public class CompressionResult
    {
        private static readonly string[] _sizeSuffixes = ["bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];

        /// <summary>
        /// Creates a zero-savings result for a file (already optimized or failed).
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <returns>A CompressionResult with no savings.</returns>
        public static CompressionResult Zero(string filePath) =>
            new(filePath, filePath, TimeSpan.Zero) { Processed = false };

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressionResult"/> class.
        /// </summary>
        /// <param name="originalFileName">The path to the original file.</param>
        /// <param name="resultFileName">The path to the compressed result file.</param>
        /// <param name="elapsed">The time taken for compression.</param>
        public CompressionResult(string originalFileName, string resultFileName, TimeSpan elapsed)
        {
            Elapsed = elapsed;

            if (!string.IsNullOrEmpty(originalFileName))
            {
                OriginalFileName = originalFileName;
                if (File.Exists(originalFileName))
                {
                    try
                    {
                        var originalInfo = new FileInfo(originalFileName);
                        OriginalFileSize = originalInfo.Length;
                    }
                    catch (Exception ex)
                    {
                        ex.LogAsync().FireAndForget();
                        OriginalFileSize = 0;
                    }
                }
            }

            if (!string.IsNullOrEmpty(resultFileName) && !string.Equals(originalFileName, resultFileName, StringComparison.OrdinalIgnoreCase))
            {
                ResultFileName = resultFileName;
                if (File.Exists(resultFileName))
                {
                    try
                    {
                        var resultInfo = new FileInfo(resultFileName);
                        ResultFileSize = resultInfo.Length;
                    }
                    catch (Exception ex)
                    {
                        ex.LogAsync().FireAndForget();
                        ResultFileSize = OriginalFileSize;
                    }
                }
                else
                {
                    ResultFileSize = OriginalFileSize;
                }
            }
            else
            {
                ResultFileSize = OriginalFileSize;
            }

            Processed = true;
        }

        /// <summary>
        /// Gets or sets the original file size in bytes.
        /// </summary>
        public long OriginalFileSize { get; set; }

        /// <summary>
        /// Gets or sets the path to the original file.
        /// </summary>
        public string OriginalFileName { get; set; }

        /// <summary>
        /// Gets or sets the result file size in bytes.
        /// </summary>
        public long ResultFileSize { get; set; }

        /// <summary>
        /// Gets or sets the path to the result file.
        /// </summary>
        public string ResultFileName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the file was processed.
        /// </summary>
        public bool Processed { get; set; }

        /// <summary>
        /// Gets or sets the time elapsed during compression.
        /// </summary>
        public TimeSpan Elapsed { get; set; }

        /// <summary>
        /// Gets the total bytes saved by compression.
        /// </summary>
        public long Saving => Math.Max(OriginalFileSize - ResultFileSize, 0);

        /// <summary>
        /// Gets the percentage reduction achieved by compression.
        /// </summary>
        public double Percent
        {
            get
            {
                return OriginalFileSize == 0 ? 0 : Math.Round(100 - (ResultFileSize / (double)OriginalFileSize * 100), 1);
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var fileName = Path.GetFileName(OriginalFileName);
            var originalSizeStr = ToFileSize(OriginalFileSize);
            var resultSizeStr = ToFileSize(ResultFileSize);
            var savingStr = ToFileSize(Saving);
            var percentStr = Percent.ToString("F1");

            // Compact single-line format for easier scanning
            // Example: "logo.png: 32.3 KB → 11.0 KB (saved 21.3 KB / 65.3%)"
            return $"{fileName}: {originalSizeStr} → {resultSizeStr} (saved {savingStr} / {percentStr}%)";
        }

        /// <summary>
        /// Converts a byte count to a human-readable file size string.
        /// </summary>
        /// <param name="value">The size in bytes.</param>
        /// <param name="decimalPlaces">Number of decimal places to display.</param>
        /// <returns>A formatted file size string (e.g., "1.5 MB").</returns>
        public static string ToFileSize(long value, int decimalPlaces = 1)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException(nameof(decimalPlaces)); }
            if (value < 0) { return "-" + ToFileSize(-value, decimalPlaces); }
            if (value == 0) { return $"0 bytes"; }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            var magnitude = (int)Math.Log(value, 1024);

            // Clamp magnitude to prevent index out of bounds
            magnitude = Math.Min(magnitude, _sizeSuffixes.Length - 1);

            // 1L << (magnitude * 10) == 2 ^ (10 * magnitude)
            // [i.e. the number of bytes in the unit corresponding to magnitude]
            var adjustedSize = (decimal)value / (1L << (magnitude * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000 && magnitude < _sizeSuffixes.Length - 1)
            {
                magnitude += 1;
                adjustedSize /= 1024;
            }

            return value < 1024
                ? $"{adjustedSize:n0} {_sizeSuffixes[magnitude]}"
                : string.Format($"{{0:n{decimalPlaces}}} {{1}}", adjustedSize, _sizeSuffixes[magnitude]);
        }
    }
}
