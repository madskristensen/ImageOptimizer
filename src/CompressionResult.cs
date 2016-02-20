using System;
using System.IO;
using System.Text;

namespace MadsKristensen.ImageOptimizer
{
    public class CompressionResult
    {
        public CompressionResult(string originalFileName, string resultFileName)
        {
            FileInfo original = new FileInfo(originalFileName);
            FileInfo result = new FileInfo(resultFileName);

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

        public long Saving
        {
            get { return OriginalFileSize - ResultFileSize; }
        }

        public double Percent
        {
            get
            {
                return Math.Round(100 - (double)ResultFileSize / (double)OriginalFileSize * 100, 1);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Optimized " + Path.GetFileName(OriginalFileName));
            sb.AppendLine("Before: " + OriginalFileSize + " bytes");
            sb.AppendLine("After: " + ResultFileSize + " bytes");
            sb.AppendLine("Saving: " + Saving + " bytes / " + Percent + "%");

            return sb.ToString();
        }
    }
}
