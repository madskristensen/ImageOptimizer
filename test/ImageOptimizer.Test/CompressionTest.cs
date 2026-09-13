using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
            _temp = Path.Combine(Path.GetTempPath(), "ImageOptimizer_Compression_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temp);
            _compressor = new Compressor();
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_temp))
            {
                Directory.Delete(_temp, true);
            }
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

            Assert.IsTrue(savings >= 29051, "Don't compress enough (" + savings + ")");
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

            Assert.IsTrue(savings >= 167030, "Don't compress enough (" + savings + ")");
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

            Assert.IsTrue(savings >= 74, "Don't compress enough (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("WebP")]
        public void Webp_Lossy()
        {
            var savings = ExecuteTest("*.webp", CompressionType.Lossy);

            Assert.IsTrue(savings >= 20, "Don't compress enough (" + savings + ")");
            Console.Write($"Savings: {savings}");
        }

        [TestMethod, TestCategory("PNG")]
        public void Png_Lossless_PreservesDecodedPixels()
        {
            string source = Path.Combine("artifacts", "png", "logo.png");
            string testFile = Path.Combine(_temp, "logo.png");
            File.Copy(source, testFile);

            CompressionResult result = _compressor.CompressFile(testFile, CompressionType.Lossless);

            Assert.AreEqual(CompressionOutcome.Optimized, result.Outcome);
            AssertDecodedPixelsEqual(source, result.ResultFileName);
            File.Delete(result.ResultFileName);
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

                Assert.AreNotEqual(CompressionOutcome.TimedOut, result.Outcome, result.ErrorMessage);
                Assert.AreNotEqual(CompressionOutcome.Cancelled, result.Outcome);

                if (result.Outcome == CompressionOutcome.Failed)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage));
                    continue;
                }

                Assert.IsTrue(File.Exists(result.ResultFileName), $"{Path.GetFileName(file)} did not produce an output file.");

                AssertValidOutput(result.ResultFileName);
                list.Add(result);
                File.Copy(result.ResultFileName, result.OriginalFileName, true);
                File.Delete(result.ResultFileName);
            }

            return list.Sum(r => r.Saving);
        }

        private static void AssertValidOutput(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            byte[] bytes = File.ReadAllBytes(filePath);

            if (extension == ".webp")
            {
                Assert.IsTrue(bytes.Length >= 12);
                Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
                Assert.AreEqual("WEBP", System.Text.Encoding.ASCII.GetString(bytes, 8, 4));
                return;
            }

            if (extension == ".svg")
            {
                StringAssert.Contains(File.ReadAllText(filePath), "<svg");
                return;
            }

            using (Image image = Image.FromFile(filePath))
            {
                Assert.IsTrue(image.Width > 0);
                Assert.IsTrue(image.Height > 0);
            }
        }

        private static void AssertDecodedPixelsEqual(string expectedFile, string actualFile)
        {
            using (var expected = new Bitmap(expectedFile))
            using (var actual = new Bitmap(actualFile))
            {
                Assert.AreEqual(expected.Width, actual.Width);
                Assert.AreEqual(expected.Height, actual.Height);

                for (var y = 0; y < expected.Height; y++)
                {
                    for (var x = 0; x < expected.Width; x++)
                    {
                        Assert.AreEqual(expected.GetPixel(x, y), actual.GetPixel(x, y), $"Pixel mismatch at ({x}, {y}).");
                    }
                }
            }
        }

        private void CopyFiles(IEnumerable<FileInfo> files)
        {
            foreach (FileInfo file in files)
            {
                file.CopyTo(Path.Combine(_temp, file.Name), true);
            }
        }
    }
}
