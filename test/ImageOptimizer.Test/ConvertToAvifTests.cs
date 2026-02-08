using System.IO;
using MadsKristensen.ImageOptimizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Test
{
    [TestClass]
    public class ConvertToAvifTests
    {
        [TestMethod]
        public void IsConvertibleToAvif_PngFile_ReturnsTrue()
        {
            Assert.IsTrue(Compressor.IsConvertibleToAvif("image.png"));
        }

        [TestMethod]
        public void IsConvertibleToAvif_JpgFile_ReturnsTrue()
        {
            Assert.IsTrue(Compressor.IsConvertibleToAvif("photo.jpg"));
        }

        [TestMethod]
        public void IsConvertibleToAvif_JpegFile_ReturnsTrue()
        {
            Assert.IsTrue(Compressor.IsConvertibleToAvif("photo.jpeg"));
        }

        [TestMethod]
        public void IsConvertibleToAvif_UppercasePng_ReturnsTrue()
        {
            Assert.IsTrue(Compressor.IsConvertibleToAvif("IMAGE.PNG"));
        }

        [TestMethod]
        public void IsConvertibleToAvif_AvifFile_ReturnsFalse()
        {
            Assert.IsFalse(Compressor.IsConvertibleToAvif("image.avif"));
        }

        [TestMethod]
        public void IsConvertibleToAvif_WebpFile_ReturnsFalse()
        {
            Assert.IsFalse(Compressor.IsConvertibleToAvif("image.webp"));
        }

        [TestMethod]
        public void IsConvertibleToAvif_GifFile_ReturnsFalse()
        {
            Assert.IsFalse(Compressor.IsConvertibleToAvif("animation.gif"));
        }

        [TestMethod]
        public void IsConvertibleToAvif_SvgFile_ReturnsFalse()
        {
            Assert.IsFalse(Compressor.IsConvertibleToAvif("icon.svg"));
        }

        [TestMethod]
        public void IsConvertibleToAvif_NullPath_ReturnsFalse()
        {
            Assert.IsFalse(Compressor.IsConvertibleToAvif(null));
        }

        [TestMethod]
        public void IsConvertibleToAvif_EmptyPath_ReturnsFalse()
        {
            Assert.IsFalse(Compressor.IsConvertibleToAvif(""));
        }

        [TestMethod]
        public void IsConvertibleToAvif_WhitespacePath_ReturnsFalse()
        {
            Assert.IsFalse(Compressor.IsConvertibleToAvif("   "));
        }

        [TestMethod]
        public void IsConvertibleToAvif_NoExtension_ReturnsFalse()
        {
            Assert.IsFalse(Compressor.IsConvertibleToAvif("filename"));
        }

        [TestMethod]
        public void IsConvertibleToAvif_FullPath_ReturnsTrue()
        {
            Assert.IsTrue(Compressor.IsConvertibleToAvif(@"C:\images\photo.jpg"));
        }

        [TestMethod, TestCategory("AVIF")]
        public void ConvertToAvif_PngFile_ProducesAvifResult()
        {
            var compressor = new Compressor();
            var sourceDir = Path.Combine("artifacts", "png");

            if (!Directory.Exists(sourceDir))
            {
                Assert.Inconclusive("Test artifact directory not found: " + sourceDir);
            }

            var testFile = Directory.GetFiles(sourceDir, "*.png")[0];
            var tempFile = Path.Combine(Path.GetTempPath(), "aviftest_" + Path.GetFileName(testFile));
            File.Copy(testFile, tempFile, true);

            try
            {
                CompressionResult result = compressor.ConvertToAvif(tempFile);

                Assert.IsNotNull(result);
                Assert.IsTrue(result.ResultFileSize > 0, "AVIF result should have non-zero size");

                if (File.Exists(result.ResultFileName))
                {
                    File.Delete(result.ResultFileName);
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [TestMethod, TestCategory("AVIF")]
        public void ConvertToAvif_JpgFile_ProducesAvifResult()
        {
            var compressor = new Compressor();
            var sourceDir = Path.Combine("artifacts", "jpg");

            if (!Directory.Exists(sourceDir))
            {
                Assert.Inconclusive("Test artifact directory not found: " + sourceDir);
            }

            var testFile = Directory.GetFiles(sourceDir, "*.jpg")[0];
            var tempFile = Path.Combine(Path.GetTempPath(), "aviftest_" + Path.GetFileName(testFile));
            File.Copy(testFile, tempFile, true);

            try
            {
                CompressionResult result = compressor.ConvertToAvif(tempFile);

                Assert.IsNotNull(result);
                Assert.IsTrue(result.ResultFileSize > 0, "AVIF result should have non-zero size");

                if (File.Exists(result.ResultFileName))
                {
                    File.Delete(result.ResultFileName);
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [TestMethod]
        public void IsFileSupported_AvifFile_ReturnsTrue()
        {
            Assert.IsTrue(Compressor.IsFileSupported("image.avif"));
        }
    }
}
