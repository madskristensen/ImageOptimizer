using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MadsKristensen.ImageOptimizer;

namespace ImageOptimizer.Test
{
    [TestClass]
    public class CompressorTests
    {
        [TestMethod]
        [DeploymentItem(@"TestData\preview.png")]
        [DeploymentItem(@"Resources\Tools\pngout.exe", @"Resources\Tools")]
        public void TestPngCompression()
        {
            // Arrange
            Compressor compressor = new Compressor();
            string inputFile = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location), "preview.png");

            Assert.IsTrue(File.Exists(inputFile));

            // Act
            CompressionResult result = compressor.CompressFile(inputFile);

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(result.ResultFileName));
            Assert.AreNotEqual(0, result.ResultFileSize);
        }

        [TestMethod]
        [DeploymentItem(@"TestData\Test.jpg")]
        [DeploymentItem(@"Resources\Tools\jpegtran.exe", @"Resources\Tools")]
        public void TestJpgCompression()
        {
            // Arrange
            Compressor compressor = new Compressor();
            string inputFile = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location), "Test.jpg");

            Assert.IsTrue(File.Exists(inputFile));

            // Act
            CompressionResult result = compressor.CompressFile(inputFile);

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(result.ResultFileName));
            Assert.AreNotEqual(0, result.ResultFileSize);
        }

        [TestMethod]
        [DeploymentItem(@"TestData\Test.gif")]
        [DeploymentItem(@"Resources\Tools\gifsicle.exe", @"Resources\Tools")]
        public void TestGifCompression()
        {
            // Arrange
            Compressor compressor = new Compressor();
            string inputFile = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location), "Test.gif");

            Assert.IsTrue(File.Exists(inputFile));

            // Act
            CompressionResult result = compressor.CompressFile(inputFile);

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(result.ResultFileName));
            Assert.AreNotEqual(0, result.ResultFileSize);
        }

        [TestMethod]
        [DeploymentItem(@"TestData\NotAnImage.txt")]
        public void TestNoneImageFile()
        {
            // Arrange
            Compressor compressor = new Compressor();
            string inputFile = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location), "NotAnImage.txt");

            Assert.IsTrue(File.Exists(inputFile));

            // Act
            CompressionResult result = compressor.CompressFile(inputFile);

            // Assert
            Assert.IsTrue(string.IsNullOrEmpty(result.ResultFileName));
            Assert.AreEqual(0, result.Saving);
        }
    }
}
