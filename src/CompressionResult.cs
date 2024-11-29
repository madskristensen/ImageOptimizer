using System.IO;
using System.Text;

namespace MadsKristensen.ImageOptimizer
{
    public class CompressionResult
    {
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
            _ = sb.AppendLine("Before: " + OriginalFileSize + " bytes");
            _ = sb.AppendLine("After: " + ResultFileSize + " bytes");
            _ = sb.AppendLine("Saving: " + Saving + " bytes / " + Percent + "%");

            return sb.ToString();
        }
    }
}
