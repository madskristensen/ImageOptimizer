using System.IO;
using MadsKristensen.ImageOptimizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Test
{
    [TestClass]
    public class ConvertToWebpTests
    {
        [TestMethod]
        public void IsConvertibleToWebp_PngFile_ReturnsTrue()
        {
            Assert.IsTrue(Compressor.IsConvertibleToWebp("image.png"));
        }

        [TestMethod]
        public void IsConvertibleToWebp_JpgFile_ReturnsTrue()
        {
            Assert.IsTrue(Compressor.IsConvertibleToWebp("photo.jpg"));
        }

        [TestMethod]
        public void IsConvertibleToWebp_JpegFile_ReturnsTrue()
        {
            Assert.IsTrue(Compressor.IsConvertibleToWebp("photo.jpeg"));
        }

        [TestMethod]
        public void IsConvertibleToWebp_UppercasePng_ReturnsTrue()
        {
            Assert.IsTrue(Compressor.IsConvertibleToWebp("IMAGE.PNG"));
        }

        [TestMethod]
        public void IsConvertibleToWebp_WebpFile_ReturnsFalse()
        {
            Assert.IsFalse(Compressor.IsConvertibleToWebp("image.webp"));
        }

        [TestMethod]
        public void IsConvertibleToWebp_GifFile_ReturnsFalse()
        {
            Assert.IsFalse(Compressor.IsConvertibleToWebp("animation.gif"));
        }

        [TestMethod]
        public void IsConvertibleToWebp_SvgFile_ReturnsFalse()
        {
            Assert.IsFalse(Compressor.IsConvertibleToWebp("icon.svg"));
        }

        [TestMethod]
        public void IsConvertibleToWebp_NullPath_ReturnsFalse()
        {
            Assert.IsFalse(Compressor.IsConvertibleToWebp(null));
        }

        [TestMethod]
        public void IsConvertibleToWebp_EmptyPath_ReturnsFalse()
        {
            Assert.IsFalse(Compressor.IsConvertibleToWebp(""));
        }

        [TestMethod]
        public void IsConvertibleToWebp_WhitespacePath_ReturnsFalse()
        {
            Assert.IsFalse(Compressor.IsConvertibleToWebp("   "));
        }

        [TestMethod]
        public void IsConvertibleToWebp_NoExtension_ReturnsFalse()
        {
            Assert.IsFalse(Compressor.IsConvertibleToWebp("filename"));
        }

        [TestMethod]
        public void IsConvertibleToWebp_FullPath_ReturnsTrue()
        {
            Assert.IsTrue(Compressor.IsConvertibleToWebp(@"C:\images\photo.jpg"));
        }

        [TestMethod, TestCategory("WebP")]
        public void ConvertToWebp_PngFile_ProducesWebpResult()
        {
            var compressor = new Compressor();
            var sourceDir = Path.Combine("artifacts", "png");

            if (!Directory.Exists(sourceDir))
            {
                Assert.Inconclusive("Test artifact directory not found: " + sourceDir);
            }

            var testFile = Directory.GetFiles(sourceDir, "*.png")[0];
            // Work on a temp copy to avoid modifying test artifacts
            var tempFile = Path.Combine(Path.GetTempPath(), "webptest_" + Path.GetFileName(testFile));
            File.Copy(testFile, tempFile, true);

            try
            {
                var result = compressor.ConvertToWebp(tempFile);

                Assert.IsNotNull(result);
                Assert.IsTrue(result.ResultFileSize > 0, "WebP result should have non-zero size");
                Assert.IsTrue(result.Saving > 0, "WebP conversion should produce savings");

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

        [TestMethod, TestCategory("WebP")]
        public void ConvertToWebp_JpgFile_ProducesWebpResult()
        {
            var compressor = new Compressor();
            var sourceDir = Path.Combine("artifacts", "jpg");

            if (!Directory.Exists(sourceDir))
            {
                Assert.Inconclusive("Test artifact directory not found: " + sourceDir);
            }

            var testFile = Directory.GetFiles(sourceDir, "*.jpg")[0];
            var tempFile = Path.Combine(Path.GetTempPath(), "webptest_" + Path.GetFileName(testFile));
            File.Copy(testFile, tempFile, true);

            try
            {
                var result = compressor.ConvertToWebp(tempFile);

                Assert.IsNotNull(result);
                Assert.IsTrue(result.ResultFileSize > 0, "WebP result should have non-zero size");

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
    }
}
