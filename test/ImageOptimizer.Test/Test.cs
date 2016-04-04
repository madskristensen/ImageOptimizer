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
            string cwd = new DirectoryInfo("../../../../src/resources/tools").FullName;
            _compressor = new Compressor(cwd);
            Telemetry.Enabled = false;
        }

        [TestMethod, TestCategory("JPG")]
        public void Jpg_LossLess()
        {
            long savings = ExecuteTest("*.jpg", false);

            Assert.IsTrue(savings == 104895, "Don't compress enough");
        }

        [TestMethod, TestCategory("JPG")]
        public void Jpg_Lossy()
        {
            long savings = ExecuteTest("*.jpg", true);

            Assert.IsTrue(savings == 223692, "Don't compress enough");
        }

        [TestMethod, TestCategory("PNG")]
        public void Png_LossLess()
        {
            long savings = ExecuteTest("*.png", false);

            Assert.IsTrue(savings == 28850, "Don't compress enough");
        }

        [TestMethod, TestCategory("PNG")]
        public void Png_Lossy()
        {
            long savings = ExecuteTest("*.png", true);

            Assert.IsTrue(savings == 135319, "Don't compress enough");
        }

        [TestMethod, TestCategory("GIF")]
        public void Gif_Lossless()
        {
            long savings = ExecuteTest("*.gif", false);

            Assert.IsTrue(savings == 5455, "Don't compress enough");
        }

        private long ExecuteTest(string searchFilter, bool lossy)
        {
            var files = _folder.GetFiles(searchFilter, SearchOption.AllDirectories);
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
                var result = _compressor.CompressFileAsync(file, lossy);

                list.Add(result);

                if (File.Exists(result.ResultFileName))
                {
                    File.Copy(result.ResultFileName, result.OriginalFileName, true);
                    File.Delete(result.ResultFileName);
                }
            }

            var grouped = list.GroupBy(r => Path.GetExtension(r.OriginalFileName).ToLowerInvariant());

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Type\t#\tSavings\tTime");
            sb.AppendLine();

            foreach (var group in grouped)
            {
                var sum = group.Sum(g => g.Saving);
                var time = group.Average(g => g.Elapsed.TotalSeconds);
                sb.AppendLine(group.Key + "\t" + group.Count() + "\t" + sum + "\t" + Math.Round(time, 2));
            }

            string testName = searchFilter.Replace("*.*", "all").Trim('.', '*');

            File.WriteAllText("../../" + testName + "-" + (lossy ? "lossy" : "lossless") + ".txt", sb.ToString());

            return list.Sum(r => r.Saving);
        }

        void CopyFiles(IEnumerable<FileInfo> files)
        {
            Directory.CreateDirectory(_temp);

            var oldFiles = Directory.GetFiles(_temp, "*.*");
            foreach (var file in oldFiles)
            {
                File.Delete(file);
            }

            foreach (var file in files)
            {
                file.CopyTo(Path.Combine(_temp, file.Name), true);
            }
        }
    }
}
