using System.IO;
using System.Text;

namespace MadsKristensen.ImageOptimizer
{
    public class CompressionResult
    {
        private static readonly string[] _sizeSuffixes = ["bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];

        public static CompressionResult Zero(string filePath) =>
            new(filePath, filePath, TimeSpan.Zero) { Processed = false };

        public CompressionResult(string originalFileName, string resultFileName, TimeSpan elapsed)
        {
            Elapsed = elapsed;
            var original = new FileInfo(originalFileName);
            var result = new FileInfo(resultFileName);

            if (original.Exists)
            {
                OriginalFileName = original.FullName;
                OriginalFileSize = original.Length;
            }

            if (result.Exists)
            {
                ResultFileName = result.FullName;
                ResultFileSize = result.Length;
            }

            Processed = true;
        }

        public long OriginalFileSize { get; set; }
        public string OriginalFileName { get; set; }
        public long ResultFileSize { get; set; }
        public string ResultFileName { get; set; }
        public bool Processed { get; set; }
        public TimeSpan Elapsed { get; set; }

        public long Saving
        {
            get { return Math.Max(OriginalFileSize - ResultFileSize, 0); }
        }

        public double Percent
        {
            get
            {
                return Math.Round(100 - (ResultFileSize / (double)OriginalFileSize * 100), 1);
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            _ = sb.AppendLine("Optimized " + Path.GetFileName(OriginalFileName) + " in " + Math.Round(Elapsed.TotalMilliseconds / 1000, 2) + " seconds");
            _ = sb.AppendLine("Before: " + ToFileSize(OriginalFileSize));
            _ = sb.AppendLine("After: " + ResultFileSize + " bytes");
            _ = sb.AppendLine("Saving: " + Saving + " bytes / " + Percent + "%");

            return sb.ToString();
        }

        // From https://stackoverflow.com/a/14488941
        public static string ToFileSize(long value, int decimalPlaces = 1)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException(nameof(decimalPlaces)); }
            if (value < 0) { return "-" + ToFileSize(-value, decimalPlaces); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            var magnitude = (int)Math.Log(value, 1024);

            // 1L << (magnitude * 10) == 2 ^ (10 * magnitude)
            // [i.e. the number of bytes in the unit corresponding to magnitude]
            var adjustedSize = (decimal)value / (1L << (magnitude * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                magnitude += 1;
                adjustedSize /= 1024;
            }

            return value < 1024
                ? string.Format("{0:n0} {1}", adjustedSize, _sizeSuffixes[magnitude])
                : string.Format("{0:n" + decimalPlaces + "} {1}", adjustedSize, _sizeSuffixes[magnitude]);
        }
    }
}
