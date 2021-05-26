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
        Compressor _compressor;
        DirectoryInfo _folder = new DirectoryInfo("../../artifacts/");
        string _temp;

        [TestInitialize]
        public void Initialize()
        {
            _temp = Path.Combine(Path.GetTempPath(), "image optimizer");
            string cwd = new DirectoryInfo("../../../../SharedFiles/Tools").FullName;
            _compressor = new Compressor(cwd);
        }

        [TestMethod, TestCategory("JPG")]
        public void Jpg_LossLess()
        {
            long savings = ExecuteTest("*.jpg", false);

            Assert.IsTrue(savings >= 104895, "Don't compress enough (" + savings + ")");
        }

        [TestMethod, TestCategory("JPG")]
        public void Jpg_Lossy()
        {
            long savings = ExecuteTest("*.jpg", true);

            Assert.IsTrue(savings == 223692, "Don't compress enough (" + savings + ")");
        }

        [TestMethod, TestCategory("PNG")]
        public void Png_LossLess()
        {
            long savings = ExecuteTest("*.png", false);

            Assert.IsTrue(savings >= 65742, "Don't compress enough (" + savings + ")");
        }

        [TestMethod, TestCategory("PNG")]
        public void Png_Lossy()
        {
            long savings = ExecuteTest("*.png", true);

            Assert.IsTrue(savings >= 139309, "Don't compress enough (" + savings + ")");
        }

        [TestMethod, TestCategory("GIF")]
        public void Gif_Lossless()
        {
            long savings = ExecuteTest("*.gif", false);

            Assert.IsTrue(savings == 5455, "Don't compress enough (" + savings + ")");
        }

        private long ExecuteTest(string searchFilter, bool lossy)
        {
            FileInfo[] files = _folder.GetFiles(searchFilter, SearchOption.AllDirectories);
            CopyFiles(files);

            long savings = RunCompression(searchFilter, lossy);
            return savings;
        }

        private long RunCompression(string searchFilter, bool lossy)
        {
            string[] files = Directory.GetFiles(_temp, searchFilter);
            var list = new List<CompressionResult>();

            foreach (string file in files)
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
                long sum = group.Sum(g => g.Saving);
                double time = group.Average(g => g.Elapsed.TotalSeconds);
                sb.AppendLine(group.Key + "\t" + group.Count() + "\t" + sum + "\t" + Math.Round(time, 2));
            }

            string testName = searchFilter.Replace("*.*", "all").Trim('.', '*');

            File.WriteAllText("../../" + testName + "-" + (lossy ? "lossy" : "lossless") + ".txt", sb.ToString());

            return list.Sum(r => r.Saving);
        }

        void CopyFiles(IEnumerable<FileInfo> files)
        {
            Directory.CreateDirectory(_temp);

            string[] oldFiles = Directory.GetFiles(_temp, "*.*");
            foreach (string file in oldFiles)
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
