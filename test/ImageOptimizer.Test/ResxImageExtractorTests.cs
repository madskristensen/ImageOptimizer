using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MadsKristensen.ImageOptimizer;
using MadsKristensen.ImageOptimizer.Resx;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Test
{
    [TestClass]
    public class ResxImageExtractorTests
    {
        private string _tempDir;
        private Compressor _compressor;

        [TestInitialize]
        public void Initialize()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "resx-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _compressor = new Compressor();
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenResxContainsPngThenImageIsDetected()
        {
            var pngPath = Path.Combine("artifacts", "png", "logo.png");
            var resxPath = CreateResxWithByteArrayImage("TestPng", pngPath, "System.Drawing.Bitmap, System.Drawing");

            var extractor = new ResxImageExtractor();
            IReadOnlyList<ResxCompressionResult> results = extractor.OptimizeResxImages(resxPath, _compressor, CompressionType.Lossless);

            Assert.IsTrue(results.Count > 0, "Should find at least one image in .resx");
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenResxContainsJpegThenImageIsDetected()
        {
            var jpgPath = Path.Combine("artifacts", "jpg", "dog1.jpg");
            var resxPath = CreateResxWithByteArrayImage("TestJpg", jpgPath, "System.Drawing.Bitmap, System.Drawing");

            var extractor = new ResxImageExtractor();
            IReadOnlyList<ResxCompressionResult> results = extractor.OptimizeResxImages(resxPath, _compressor, CompressionType.Lossless);

            Assert.IsTrue(results.Count > 0, "Should find at least one JPEG image in .resx");
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenResxContainsNoImagesThenReturnsEmpty()
        {
            var resxPath = CreateResxWithStringOnly();

            var extractor = new ResxImageExtractor();
            IReadOnlyList<ResxCompressionResult> results = extractor.OptimizeResxImages(resxPath, _compressor, CompressionType.Lossless);

            Assert.AreEqual(0, results.Count, "Should return empty for .resx with no images");
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenFileDoesNotExistThenReturnsEmpty()
        {
            var fakePath = Path.Combine(_tempDir, "nonexistent.resx");

            var extractor = new ResxImageExtractor();
            IReadOnlyList<ResxCompressionResult> results = extractor.OptimizeResxImages(fakePath, _compressor, CompressionType.Lossless);

            Assert.AreEqual(0, results.Count);
        }

        [TestMethod, TestCategory("Resx")]
        [ExpectedException(typeof(ArgumentNullException))]
        public void WhenNullCompressorThenThrowsArgumentNullException()
        {
            var resxPath = Path.Combine(_tempDir, "test.resx");
            File.WriteAllText(resxPath, "<root></root>");

            var extractor = new ResxImageExtractor();
            extractor.OptimizeResxImages(resxPath, null, CompressionType.Lossless);
        }

        [TestMethod, TestCategory("Resx")]
        public void DetectImageExtensionReturnsPngForPngBytes()
        {
            byte[] pngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            var ext = ResxImageExtractor.DetectImageExtension(pngHeader);
            Assert.AreEqual(".png", ext);
        }

        [TestMethod, TestCategory("Resx")]
        public void DetectImageExtensionReturnsJpgForJpegBytes()
        {
            byte[] jpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0 };
            var ext = ResxImageExtractor.DetectImageExtension(jpegHeader);
            Assert.AreEqual(".jpg", ext);
        }

        [TestMethod, TestCategory("Resx")]
        public void DetectImageExtensionReturnsGifForGifBytes()
        {
            byte[] gifHeader = { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 };
            var ext = ResxImageExtractor.DetectImageExtension(gifHeader);
            Assert.AreEqual(".gif", ext);
        }

        [TestMethod, TestCategory("Resx")]
        public void DetectImageExtensionReturnsNullForUnknownBytes()
        {
            byte[] unknown = { 0x00, 0x00, 0x00, 0x00 };
            var ext = ResxImageExtractor.DetectImageExtension(unknown);
            Assert.IsNull(ext);
        }

        [TestMethod, TestCategory("Resx")]
        public void DetectImageExtensionReturnsNullForNullInput()
        {
            var ext = ResxImageExtractor.DetectImageExtension(null);
            Assert.IsNull(ext);
        }

        [TestMethod, TestCategory("Resx")]
        public void DetectImageExtensionReturnsNullForTooShortInput()
        {
            byte[] tooShort = { 0x89, 0x50 };
            var ext = ResxImageExtractor.DetectImageExtension(tooShort);
            Assert.IsNull(ext);
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenResxContainsMultipleImagesThenAllAreProcessed()
        {
            var pngPath = Path.Combine("artifacts", "png", "logo.png");
            var jpgPath = Path.Combine("artifacts", "jpg", "dog1.jpg");
            var resxPath = CreateResxWithMultipleByteArrayImages(
                new[] { ("Image1", pngPath), ("Image2", jpgPath) },
                "System.Drawing.Bitmap, System.Drawing");

            var extractor = new ResxImageExtractor();
            IReadOnlyList<ResxCompressionResult> results = extractor.OptimizeResxImages(resxPath, _compressor, CompressionType.Lossless);

            Assert.AreEqual(2, results.Count, "Should find both images");
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenOptimizedThenResxFileIsModified()
        {
            var pngPath = Path.Combine("artifacts", "png", "logo.png");
            var resxPath = CreateResxWithByteArrayImage("TestPng", pngPath, "System.Drawing.Bitmap, System.Drawing");

            var originalContent = File.ReadAllText(resxPath);

            var extractor = new ResxImageExtractor();
            IReadOnlyList<ResxCompressionResult> results = extractor.OptimizeResxImages(resxPath, _compressor, CompressionType.Lossless);

            var optimizedResults = results.Where(r => r.Saving > 0).ToList();
            if (optimizedResults.Count > 0)
            {
                var newContent = File.ReadAllText(resxPath);
                Assert.AreNotEqual(originalContent, newContent, "Resx file should be modified after optimization");
            }
        }

        /// <summary>
        /// Creates a .resx file with a single image embedded as a bytearray.base64 data node.
        /// </summary>
        private string CreateResxWithByteArrayImage(string resourceName, string imageFilePath, string typeName)
        {
            byte[] imageBytes = File.ReadAllBytes(imageFilePath);
            var base64 = Convert.ToBase64String(imageBytes);

            var doc = new XDocument(
                new XElement("root",
                    new XElement("data",
                        new XAttribute("name", resourceName),
                        new XAttribute("type", typeName),
                        new XAttribute("mimetype", "application/x-microsoft.net.object.bytearray.base64"),
                        new XElement("value", base64))));

            var resxPath = Path.Combine(_tempDir, resourceName + ".resx");
            doc.Save(resxPath);
            return resxPath;
        }

        /// <summary>
        /// Creates a .resx file with multiple images.
        /// </summary>
        private string CreateResxWithMultipleByteArrayImages(
            (string name, string path)[] entries, string typeName)
        {
            var root = new XElement("root");

            foreach (var (name, path) in entries)
            {
                byte[] imageBytes = File.ReadAllBytes(path);
                var base64 = Convert.ToBase64String(imageBytes);

                root.Add(new XElement("data",
                    new XAttribute("name", name),
                    new XAttribute("type", typeName),
                    new XAttribute("mimetype", "application/x-microsoft.net.object.bytearray.base64"),
                    new XElement("value", base64)));
            }

            var resxPath = Path.Combine(_tempDir, "multi.resx");
            new XDocument(root).Save(resxPath);
            return resxPath;
        }

        /// <summary>
        /// Creates a .resx file with only string resources (no images).
        /// </summary>
        private string CreateResxWithStringOnly()
        {
            var doc = new XDocument(
                new XElement("root",
                    new XElement("data",
                        new XAttribute("name", "HelloWorld"),
                        new XAttribute(XNamespace.Xml + "space", "preserve"),
                        new XElement("value", "Hello World!"))));

            var resxPath = Path.Combine(_tempDir, "strings.resx");
            doc.Save(resxPath);
            return resxPath;
        }
    }
}
