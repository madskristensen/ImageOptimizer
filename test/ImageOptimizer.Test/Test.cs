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
    public class Test
    {
        private Compressor _compressor;
        private readonly DirectoryInfo _folder = new DirectoryInfo("../../artifacts/");
        private string _temp;

        [TestInitialize]
        public void Initialize()
        {
            _temp = Path.Combine(Path.GetTempPath(), "image optimizer");
            var cwd = new DirectoryInfo("../../../../src/resources/tools").FullName;
            _compressor = new Compressor(cwd);
        }

        [TestMethod, TestCategory("JPG")]
        public void Jpg_LossLess()
        {
            var savings = ExecuteTest("*.jpg", false);

            Assert.IsTrue(savings >= 98665, "Don't compress enough (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("JPG")]
        public void Jpg_Lossy()
        {
            var savings = ExecuteTest("*.jpg", true);

            Assert.IsTrue(savings >= 223838, "Don't compress enough (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("PNG")]
        public void Png_LossLess()
        {
            var savings = ExecuteTest("*.png", false);

            Assert.IsTrue(savings >= 29025, "Don't compress enough (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("PNG")]
        public void Png_Lossy()
        {
            var savings = ExecuteTest("*.png", true);

            Assert.IsTrue(savings >= 140321, "Don't compress enough (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("GIF")]
        public void Gif_Lossless()
        {
            var savings = ExecuteTest("*.gif", false);

            Assert.IsTrue(savings >= 5455, "Don't compress enough (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("GIF")]
        public void Gif_Lossy()
        {
            var savings = ExecuteTest("*.gif", true);

            Assert.IsTrue(savings >= 134854, "Don't compress enough (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        private long ExecuteTest(string searchFilter, bool lossy)
        {
            FileInfo[] files = _folder.GetFiles(searchFilter, SearchOption.AllDirectories);
            CopyFiles(files);

            var savings = RunCompression(searchFilter, lossy);
            return savings;
        }

        private long RunCompression(string searchFilter, bool lossy)
        {
            var files = Directory.GetFiles(_temp, searchFilter);
            var list = new List<CompressionResult>();

            foreach (var file in files)
            {
                CompressionResult result = _compressor.CompressFile(file, lossy);

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

            File.WriteAllText("../../" + testName + "-" + (lossy ? "lossy" : "lossless") + ".txt", sb.ToString());

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
