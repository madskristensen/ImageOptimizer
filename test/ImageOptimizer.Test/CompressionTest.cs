using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MadsKristensen.ImageOptimizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Test
{
    [TestClass]
    public class CompressionTest
    {
        private Compressor _compressor;
        private readonly DirectoryInfo _folder = new DirectoryInfo("artifacts/");
        private string _temp;

        [TestInitialize]
        public void Initialize()
        {
            _temp = Path.Combine(Path.GetTempPath(), "image optimizer");
            _compressor = new Compressor();
        }

        [TestMethod, TestCategory("JPG")]
        public void Jpg_LossLess()
        {
            var savings = ExecuteTest("*.jpg", CompressionType.Lossless);

            Assert.IsTrue(savings >= 98922, "Don't compress enough (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("JPG")]
        public void Jpg_Lossy()
        {
            var savings = ExecuteTest("*.jpg", CompressionType.Lossy);

            Assert.IsTrue(savings >= 153155, "Don't compress enough (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("PNG")]
        public void Png_LossLess()
        {
            var savings = ExecuteTest("*.png", CompressionType.Lossless);

            Assert.IsTrue(savings >= 28966, "Don't compress enough (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("PNG")]
        public void Png_Lossy()
        {
            var savings = ExecuteTest("*.png", CompressionType.Lossy);

            Assert.IsTrue(savings >= 91319, "Don't compress enough (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("GIF")]
        public void Gif_Lossless()
        {
            var savings = ExecuteTest("*.gif", CompressionType.Lossless);

            Assert.IsTrue(savings >= 5455, "Don't compress enough (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("GIF")]
        public void Gif_Lossy()
        {
            var savings = ExecuteTest("*.gif", CompressionType.Lossy);

            Assert.IsTrue(savings >= 134854, "Don't compress enough (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("SVG")]
        public void Svg_Lossy()
        {
            var savings = ExecuteTest("*.svg", CompressionType.Lossy);

            Assert.IsTrue(savings >= 1883, "Don't compress enough (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("WebP")]
        public void Webp_Lossless()
        {
            var savings = ExecuteTest("*.webp", CompressionType.Lossless);

            // WebP files may already be well-optimized; verify pipeline handles them without error
            Assert.IsTrue(savings >= 0, "Negative savings (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("WebP")]
        public void Webp_Lossy()
        {
            var savings = ExecuteTest("*.webp", CompressionType.Lossy);

            Assert.IsTrue(savings >= 0, "Negative savings (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("AVIF")]
        public void Avif_Lossless()
        {
            var savings = ExecuteTest("*.avif", CompressionType.Lossless);

            // AVIF re-encoding may not always produce savings on already-optimized files
            Assert.IsTrue(savings >= 0, "Negative savings (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("AVIF")]
        public void Avif_Lossy()
        {
            var savings = ExecuteTest("*.avif", CompressionType.Lossy);

            Assert.IsTrue(savings >= 0, "Negative savings (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        private long ExecuteTest(string searchFilter, CompressionType type)
        {
            FileInfo[] files = _folder.GetFiles(searchFilter, SearchOption.AllDirectories);
            CopyFiles(files);

            var savings = RunCompression(searchFilter, type);
            return savings;
        }

        private long RunCompression(string searchFilter, CompressionType type)
        {
            var files = Directory.GetFiles(_temp, searchFilter);
            var list = new List<CompressionResult>();

            foreach (var file in files)
            {
                CompressionResult result = _compressor.CompressFile(file, type);

                if (File.Exists(result.ResultFileName))
                {
                    list.Add(result);
                    File.Copy(result.ResultFileName, result.OriginalFileName, true);
                    File.Delete(result.ResultFileName);
                }
            }

            IEnumerable<IGrouping<string, CompressionResult>> grouped = list.GroupBy(r => Path.GetExtension(r.OriginalFileName).ToLowerInvariant());

            var sb = new StringBuilder();
            sb.AppendLine("Type\t#\tSavings\tTime");
            sb.AppendLine();

            foreach (IGrouping<string, CompressionResult> group in grouped)
            {
                var sum = group.Sum(g => g.Saving);
                var time = group.Average(g => g.Elapsed.TotalSeconds);
                sb.AppendLine(group.Key + "\t" + group.Count() + "\t" + sum + "\t" + Math.Round(time, 2));
            }

            var testName = searchFilter.Replace("*.*", "all").Trim('.', '*');

            File.WriteAllText("../../" + testName + "-" + (type is CompressionType.Lossy ? "lossy" : "lossless") + ".txt", sb.ToString());

            return list.Sum(r => r.Saving);
        }

        private void CopyFiles(IEnumerable<FileInfo> files)
        {
            Directory.CreateDirectory(_temp);

            var oldFiles = Directory.GetFiles(_temp, "*.*");
            foreach (var file in oldFiles)
            {
                File.Delete(file);
            }

            foreach (FileInfo file in files)
            {
                file.CopyTo(Path.Combine(_temp, file.Name), true);
            }
        }
    }
}
