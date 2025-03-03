using System;
using System.IO;
using System.Text;
using MadsKristensen.ImageOptimizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Test
{
    [TestClass]
    public class CompressionResultTests
    {
        private readonly string _originalFilePath = new FileInfo("original.jpg").FullName;
        private readonly string _resultFilePath = new FileInfo("result.jpg").FullName;

        [TestInitialize]
        public void Setup()
        {
            File.WriteAllText(_originalFilePath, new string('a', 1000)); // 1000 bytes
            File.WriteAllText(_resultFilePath, new string('a', 800)); // 800 bytes
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(_originalFilePath))
            {
                File.Delete(_originalFilePath);
            }
            if (File.Exists(_resultFilePath))
            {
                File.Delete(_resultFilePath);
            }
        }

        [TestMethod]
        public void Zero_CreatesUnprocessedResult()
        {
            var result = CompressionResult.Zero(_originalFilePath);

            Assert.AreEqual(_originalFilePath, result.OriginalFileName);
            Assert.AreEqual(_originalFilePath, result.ResultFileName);
            Assert.AreEqual(1000, result.OriginalFileSize);
            Assert.AreEqual(1000, result.ResultFileSize);
            Assert.AreEqual(TimeSpan.Zero, result.Elapsed);
            Assert.IsFalse(result.Processed);
        }

        [TestMethod]
        public void Constructor_InitializesPropertiesCorrectly()
        {
            var elapsed = TimeSpan.FromSeconds(1);
            var result = new CompressionResult(_originalFilePath, _resultFilePath, elapsed);

            Assert.AreEqual(_originalFilePath, result.OriginalFileName);
            Assert.AreEqual(_resultFilePath, result.ResultFileName);
            Assert.AreEqual(1000, result.OriginalFileSize);
            Assert.AreEqual(800, result.ResultFileSize);
            Assert.AreEqual(elapsed, result.Elapsed);
            Assert.IsTrue(result.Processed);
        }

        [TestMethod]
        public void Saving_CalculatesCorrectly()
        {
            var result = new CompressionResult(_originalFilePath, _resultFilePath, TimeSpan.Zero);

            Assert.AreEqual(200, result.Saving);
        }

        [TestMethod]
        public void Percent_CalculatesCorrectly()
        {
            var result = new CompressionResult(_originalFilePath, _resultFilePath, TimeSpan.Zero);

            Assert.AreEqual(20.0, result.Percent);
        }

        [TestMethod]
        public void ToString_ReturnsCorrectString()
        {
            var elapsed = TimeSpan.FromSeconds(1);
            var result = new CompressionResult(_originalFilePath, _resultFilePath, elapsed);

            var expected = new StringBuilder()
                .AppendLine("Optimized original.jpg in 1 seconds")
                .AppendLine("Before: 1 KB")
                .AppendLine("After: 800 bytes")
                .AppendLine("Saving: 200 bytes / 20%")
                .ToString();

            Assert.AreEqual(expected, result.ToString());
        }
    }
}
