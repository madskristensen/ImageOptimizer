using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using MadsKristensen.ImageOptimizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Text;

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
        }

        [TestMethod]
        public async Task LossLess()
        {
            var files = _folder.GetFiles("*.*", SearchOption.AllDirectories);
            CopyFiles(files);

            var tempFiles = Directory.GetFiles(_temp, "*.*");
            var savings = await RunCompression(tempFiles, false);

            Assert.IsTrue(savings >= 46686);
        }

        [TestMethod]
        public async Task Lossy()
        {
            var files = _folder.GetFiles("*.*", SearchOption.AllDirectories);
            CopyFiles(files);

            var tempFiles = Directory.GetFiles(_temp, "*.*");

            var savings = await RunCompression(tempFiles, true);

            Assert.IsTrue(savings >= 135226);
        }

        private async Task<long> RunCompression(IEnumerable<string> files, bool lossy)
        {
            var list = new List<CompressionResult>();

            foreach (var file in files)
            {
                var result = await _compressor.CompressFileAsync(file, lossy);

                if (result.Saving > 0)
                    list.Add(result);
            }

            var grouped = list.GroupBy(r => Path.GetExtension(r.OriginalFileName).ToLowerInvariant());

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Type\tCount\tSavings");
            sb.AppendLine();

            long total = 0;

            foreach (var group in grouped)
            {
                var sum = group.Sum(g => g.Saving);
                total += sum;
                sb.AppendLine(group.Key + "\t" + group.Count() + "\t" + sum);
            }

            sb.AppendLine();
            sb.AppendLine("Total\t" + grouped.Sum(g => g.Count()) + "\t" + total);

            File.WriteAllText("../../" + (lossy ? "lossy":  "lossless") + ".txt", sb.ToString());

            return list.Sum(r => r.Saving);
        }

        void CopyFiles(IEnumerable<FileInfo> files)
        {
            if (Directory.Exists(_temp))
                Directory.Delete(_temp, true);

            Directory.CreateDirectory(_temp);

            foreach (var file in files)
            {
                file.CopyTo(Path.Combine(_temp, file.Name), true);
            }
        }
    }
}
