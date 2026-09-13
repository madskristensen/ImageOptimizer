using System;
using System.IO;
using System.Linq;
using MadsKristensen.ImageOptimizer;
using MadsKristensen.ImageOptimizer.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Test
{
    [TestClass]
    public class FileUtilitiesTests
    {
        [TestMethod]
        public void GetDistinctPaths_RemovesDuplicatesAndPreservesOrder()
        {
            string[] paths =
            {
                @"C:\images\first.png",
                @"C:\images\second.png",
                @"c:\IMAGES\FIRST.PNG",
                @"C:\images\third.png",
                @"C:\images\second.png"
            };

            var result = FileUtilities.GetDistinctPaths(paths);

            CollectionAssert.AreEqual(paths.Take(2).Concat(paths.Skip(3).Take(1)).ToArray(), result.ToArray());
        }

        [DataTestMethod]
        [DataRow("resources.resx", true)]
        [DataRow("RESOURCES.RESX", true)]
        [DataRow("image.png", false)]
        [DataRow(null, false)]
        public void IsResxFile_ReturnsExpectedResult(string path, bool expected)
        {
            Assert.AreEqual(expected, FileUtilities.IsResxFile(path));
        }

        private string _testFolder;

        [TestInitialize]
        public void Setup()
        {
            _testFolder = Path.Combine(Path.GetTempPath(), "ImageOptimizer_FileUtilitiesTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testFolder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testFolder))
            {
                Directory.Delete(_testFolder, true);
            }
        }

        #region IsImageFileSupported Tests

        [TestMethod]
        public void IsImageFileSupported_NullPath_ReturnsFalse()
        {
            var result = FileUtilities.IsImageFileSupported(null);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsImageFileSupported_EmptyPath_ReturnsFalse()
        {
            var result = FileUtilities.IsImageFileSupported("");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsImageFileSupported_WhitespacePath_ReturnsFalse()
        {
            var result = FileUtilities.IsImageFileSupported("   ");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsImageFileSupported_PngFile_ReturnsTrue()
        {
            var result = FileUtilities.IsImageFileSupported("image.png");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsImageFileSupported_JpgFile_ReturnsTrue()
        {
            var result = FileUtilities.IsImageFileSupported("image.jpg");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsImageFileSupported_JpegFile_ReturnsTrue()
        {
            var result = FileUtilities.IsImageFileSupported("image.jpeg");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsImageFileSupported_GifFile_ReturnsTrue()
        {
            var result = FileUtilities.IsImageFileSupported("image.gif");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsImageFileSupported_SvgFile_ReturnsTrue()
        {
            var result = FileUtilities.IsImageFileSupported("image.svg");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsImageFileSupported_WebpFile_ReturnsTrue()
        {
            var result = FileUtilities.IsImageFileSupported("image.webp");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsImageFileSupported_UppercaseExtension_ReturnsTrue()
        {
            var result = FileUtilities.IsImageFileSupported("image.PNG");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsImageFileSupported_MixedCaseExtension_ReturnsTrue()
        {
            var result = FileUtilities.IsImageFileSupported("image.JpG");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsImageFileSupported_UnsupportedExtension_ReturnsFalse()
        {
            var result = FileUtilities.IsImageFileSupported("document.pdf");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsImageFileSupported_TextFile_ReturnsFalse()
        {
            var result = FileUtilities.IsImageFileSupported("readme.txt");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsImageFileSupported_NoExtension_ReturnsFalse()
        {
            var result = FileUtilities.IsImageFileSupported("filename");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsImageFileSupported_FullPath_ReturnsTrue()
        {
            var result = FileUtilities.IsImageFileSupported(@"C:\folder\subfolder\image.png");

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsImageFileSupported_BmpFile_ReturnsFalse()
        {
            // BMP is not in the supported list
            var result = FileUtilities.IsImageFileSupported("image.bmp");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsImageFileSupported_TiffFile_ReturnsFalse()
        {
            // TIFF is not in the supported list
            var result = FileUtilities.IsImageFileSupported("image.tiff");

            Assert.IsFalse(result);
        }

        #endregion

        [TestMethod]
        public void GetSupportedImageFiles_FiltersUnsupportedFiles()
        {
            File.WriteAllText(Path.Combine(_testFolder, "image.png"), "png");
            File.WriteAllText(Path.Combine(_testFolder, "notes.txt"), "text");

            string[] files = FileUtilities.GetSupportedImageFiles(_testFolder).ToArray();

            Assert.AreEqual(1, files.Length);
            Assert.AreEqual("image.png", Path.GetFileName(files[0]));
        }

        [TestMethod]
        public void CreateTempFileWithExtension_ReturnsUniqueNonexistentPaths()
        {
            string first = FileUtilities.CreateTempFileWithExtension("image.png");
            string second = FileUtilities.CreateTempFileWithExtension("image.png");

            Assert.AreEqual(".png", Path.GetExtension(first));
            Assert.AreNotEqual(first, second);
            Assert.IsFalse(File.Exists(first));
            Assert.IsFalse(File.Exists(second));
        }

        [TestMethod]
        public void GetFileSizeBytes_ReturnsSizeOrZero()
        {
            string file = Path.Combine(_testFolder, "size.txt");
            File.WriteAllText(file, "12345");

            Assert.AreEqual(5, FileUtilities.GetFileSizeBytes(file));
            Assert.AreEqual(0, FileUtilities.GetFileSizeBytes(Path.Combine(_testFolder, "missing.txt")));
        }

        #region SafeDeleteFile Tests

        [TestMethod]
        public void SafeDeleteFile_ExistingFile_DeletesAndReturnsTrue()
        {
            var filePath = Path.Combine(_testFolder, "test.txt");
            File.WriteAllText(filePath, "test content");

            var result = FileUtilities.SafeDeleteFile(filePath);

            Assert.IsTrue(result);
            Assert.IsFalse(File.Exists(filePath));
        }

        [TestMethod]
        public void SafeDeleteFile_NonExistentFile_ReturnsFalse()
        {
            var filePath = Path.Combine(_testFolder, "nonexistent.txt");

            var result = FileUtilities.SafeDeleteFile(filePath);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void SafeDeleteFile_NullPath_ReturnsFalse()
        {
            var result = FileUtilities.SafeDeleteFile(null);

            Assert.IsFalse(result);
        }

        #endregion

        #region SafeCopyFile Tests

        [TestMethod]
        public void SafeCopyFile_ValidSourceAndDest_CopiesAndReturnsTrue()
        {
            var sourceFile = Path.Combine(_testFolder, "source.txt");
            var destFile = Path.Combine(_testFolder, "dest.txt");
            File.WriteAllText(sourceFile, "test content");

            var result = FileUtilities.SafeCopyFile(sourceFile, destFile);

            Assert.IsTrue(result);
            Assert.IsTrue(File.Exists(destFile));
            Assert.AreEqual("test content", File.ReadAllText(destFile));
        }

        [TestMethod]
        public void SafeCopyFile_NonExistentSource_ReturnsFalse()
        {
            var sourceFile = Path.Combine(_testFolder, "nonexistent.txt");
            var destFile = Path.Combine(_testFolder, "dest.txt");

            var result = FileUtilities.SafeCopyFile(sourceFile, destFile);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void SafeCopyFile_OverwriteExisting_Succeeds()
        {
            var sourceFile = Path.Combine(_testFolder, "source.txt");
            var destFile = Path.Combine(_testFolder, "dest.txt");
            File.WriteAllText(sourceFile, "new content");
            File.WriteAllText(destFile, "old content");

            var result = FileUtilities.SafeCopyFile(sourceFile, destFile, overwrite: true);

            Assert.IsTrue(result);
            Assert.AreEqual("new content", File.ReadAllText(destFile));
        }

        [TestMethod]
        public void SafeCopyFile_CreatesDestinationDirectory()
        {
            var sourceFile = Path.Combine(_testFolder, "source.txt");
            var destFile = Path.Combine(_testFolder, "newdir", "dest.txt");
            File.WriteAllText(sourceFile, "test content");

            var result = FileUtilities.SafeCopyFile(sourceFile, destFile);

            Assert.IsTrue(result);
            Assert.IsTrue(File.Exists(destFile));
        }

        #endregion

        #region GetMimeTypeFromExtension Tests

        [TestMethod]
        public void GetMimeTypeFromExtension_Png_ReturnsCorrectMime()
        {
            var result = FileUtilities.GetMimeTypeFromExtension("image.png");

            Assert.AreEqual("image/png", result);
        }

        [TestMethod]
        public void GetMimeTypeFromExtension_Jpg_ReturnsCorrectMime()
        {
            var result = FileUtilities.GetMimeTypeFromExtension("image.jpg");

            Assert.AreEqual("image/jpeg", result);
        }

        [TestMethod]
        public void GetMimeTypeFromExtension_Jpeg_ReturnsCorrectMime()
        {
            var result = FileUtilities.GetMimeTypeFromExtension("image.jpeg");

            Assert.AreEqual("image/jpeg", result);
        }

        [TestMethod]
        public void GetMimeTypeFromExtension_Gif_ReturnsCorrectMime()
        {
            var result = FileUtilities.GetMimeTypeFromExtension("image.gif");

            Assert.AreEqual("image/gif", result);
        }

        [TestMethod]
        public void GetMimeTypeFromExtension_Svg_ReturnsCorrectMime()
        {
            var result = FileUtilities.GetMimeTypeFromExtension("image.svg");

            Assert.AreEqual("image/svg+xml", result);
        }

        [TestMethod]
        public void GetMimeTypeFromExtension_Webp_ReturnsCorrectMime()
        {
            var result = FileUtilities.GetMimeTypeFromExtension("image.webp");

            Assert.AreEqual("image/webp", result);
        }

        [TestMethod]
        public void GetMimeTypeFromExtension_UppercaseExtension_ReturnsCorrectMime()
        {
            var result = FileUtilities.GetMimeTypeFromExtension("image.PNG");

            Assert.AreEqual("image/png", result);
        }

        [TestMethod]
        public void GetMimeTypeFromExtension_UnknownExtension_ReturnsFallback()
        {
            var result = FileUtilities.GetMimeTypeFromExtension("image.xyz");

            Assert.AreEqual("image/xyz", result);
        }

        [TestMethod]
        public void GetMimeTypeFromExtension_NullPath_ReturnsOctetStream()
        {
            var result = FileUtilities.GetMimeTypeFromExtension(null);

            Assert.AreEqual("application/octet-stream", result);
        }

        [TestMethod]
        public void GetMimeTypeFromExtension_EmptyPath_ReturnsOctetStream()
        {
            var result = FileUtilities.GetMimeTypeFromExtension("");

            Assert.AreEqual("application/octet-stream", result);
        }

        #endregion
    }
}
