using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
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

            Assert.IsTrue(results.Any(r => r.Saving > 0), "The test image should produce measurable savings.");
            var newContent = File.ReadAllText(resxPath);
            Assert.AreNotEqual(originalContent, newContent, "Resx file should be modified after optimization");
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenResxContainsGifThenImageIsDetected()
        {
            var gifPath = Path.Combine("artifacts", "gif", "logo.gif");
            var resxPath = CreateResxWithByteArrayImage("TestGif", gifPath, "System.Drawing.Bitmap, System.Drawing");

            var extractor = new ResxImageExtractor();
            IReadOnlyList<ResxCompressionResult> results = extractor.OptimizeResxImages(resxPath, _compressor, CompressionType.Lossless);

            Assert.IsTrue(results.Count > 0, "Should find at least one GIF image in .resx");
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenLossyCompressionThenImageIsOptimized()
        {
            var pngPath = Path.Combine("artifacts", "png", "logo.png");
            var resxPath = CreateResxWithByteArrayImage("TestLossy", pngPath, "System.Drawing.Bitmap, System.Drawing");

            var extractor = new ResxImageExtractor();
            IReadOnlyList<ResxCompressionResult> results = extractor.OptimizeResxImages(resxPath, _compressor, CompressionType.Lossy);

            Assert.IsTrue(results.Any(result => result.Saving > 0), "Lossy compression should optimize the test image.");
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenBinaryPayloadContainsPngThenLengthAndTrailerArePreserved()
        {
            byte[] imageBytes = File.ReadAllBytes(Path.Combine("artifacts", "png", "logo.png"));
            byte[] header = new byte[9];
            header[0] = 0x00;
            header[1] = 0x01;
            Buffer.BlockCopy(BitConverter.GetBytes(imageBytes.Length), 0, header, 5, 4);
            byte[] trailer = { 0x0B, 0x0C, 0x0D };
            byte[] payload = header.Concat(imageBytes).Concat(trailer).ToArray();
            string resxPath = CreateBinaryPayloadResx("BinaryPng", payload);

            var extractor = new ResxImageExtractor();
            IReadOnlyList<ResxCompressionResult> results = extractor.OptimizeResxImages(
                resxPath,
                _compressor,
                CompressionType.Lossless);

            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].Saving > 0);

            byte[] optimizedPayload = ReadResourcePayload(resxPath);
            CollectionAssert.AreEqual(trailer, optimizedPayload.Skip(optimizedPayload.Length - trailer.Length).ToArray());

            int optimizedImageLength = optimizedPayload.Length - header.Length - trailer.Length;
            Assert.AreEqual(optimizedImageLength, BitConverter.ToInt32(optimizedPayload, 5));
            CollectionAssert.AreEqual(
                new byte[] { 0x89, 0x50, 0x4E, 0x47 },
                optimizedPayload.Skip(header.Length).Take(4).ToArray());
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenCancellationIsAlreadyRequestedThenResxIsNotModified()
        {
            string resxPath = CreateResxWithByteArrayImage(
                "CancelledImage",
                Path.Combine("artifacts", "png", "logo.png"),
                "System.Drawing.Bitmap, System.Drawing");
            string originalContent = File.ReadAllText(resxPath);

            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var extractor = new ResxImageExtractor();

                Assert.ThrowsException<OperationCanceledException>(() =>
                    extractor.OptimizeResxImages(resxPath, _compressor, CompressionType.Lossless, cancellation.Token));
            }

            Assert.AreEqual(originalContent, File.ReadAllText(resxPath));
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenTypeIsIconThenImageIsDetected()
        {
            var pngPath = Path.Combine("artifacts", "png", "logo.png");
            var resxPath = CreateResxWithByteArrayImage("TestIcon", pngPath, "System.Drawing.Icon, System.Drawing");

            var extractor = new ResxImageExtractor();
            IReadOnlyList<ResxCompressionResult> results = extractor.OptimizeResxImages(resxPath, _compressor, CompressionType.Lossless);

            Assert.IsTrue(results.Count > 0, "Should detect Icon type entries");
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenResxContainsFileRefThenItIsSkipped()
        {
            var resxPath = CreateResxWithFileRef();

            var extractor = new ResxImageExtractor();
            IReadOnlyList<ResxCompressionResult> results = extractor.OptimizeResxImages(resxPath, _compressor, CompressionType.Lossless);

            Assert.AreEqual(0, results.Count, "FileRef entries should not be processed");
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenResxContainsInvalidBase64ThenEntryIsSkipped()
        {
            var resxPath = CreateResxWithInvalidBase64();

            var extractor = new ResxImageExtractor();
            IReadOnlyList<ResxCompressionResult> results = extractor.OptimizeResxImages(resxPath, _compressor, CompressionType.Lossless);

            // The entry is detected but produces zero savings since the base64 is invalid
            Assert.AreEqual(1, results.Count, "Entry should be reported");
            Assert.AreEqual(0, results[0].Saving, "Invalid base64 should produce zero savings");
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenResxHasEmptyValueThenEntryIsSkipped()
        {
            var resxPath = CreateResxWithEmptyValue();

            var extractor = new ResxImageExtractor();
            IReadOnlyList<ResxCompressionResult> results = extractor.OptimizeResxImages(resxPath, _compressor, CompressionType.Lossless);

            Assert.AreEqual(0, results.Count, "Empty value elements should be skipped");
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenResxHasMixedContentThenOnlyImagesAreProcessed()
        {
            var pngPath = Path.Combine("artifacts", "png", "logo.png");
            byte[] imageBytes = File.ReadAllBytes(pngPath);
            var base64 = Convert.ToBase64String(imageBytes);

            var doc = new XDocument(
                new XElement("root",
                    new XElement("data",
                        new XAttribute("name", "MyString"),
                        new XAttribute(XNamespace.Xml + "space", "preserve"),
                        new XElement("value", "just a string")),
                    new XElement("data",
                        new XAttribute("name", "MyImage"),
                        new XAttribute("type", "System.Drawing.Bitmap, System.Drawing"),
                        new XAttribute("mimetype", "application/x-microsoft.net.object.bytearray.base64"),
                        new XElement("value", base64)),
                    new XElement("data",
                        new XAttribute("name", "FileRef"),
                        new XAttribute("type", "System.Resources.ResXFileRef, System.Windows.Forms"),
                        new XElement("value", @"Resources\icon.png;System.Byte[], mscorlib"))));

            var resxPath = Path.Combine(_tempDir, "mixed.resx");
            doc.Save(resxPath);

            var extractor = new ResxImageExtractor();
            IReadOnlyList<ResxCompressionResult> results = extractor.OptimizeResxImages(resxPath, _compressor, CompressionType.Lossless);

            Assert.AreEqual(1, results.Count, "Should only process the embedded image, not strings or file refs");
        }

        [TestMethod, TestCategory("Resx")]
        [ExpectedException(typeof(ArgumentException))]
        public void WhenResxPathIsEmptyThenThrowsArgumentException()
        {
            var extractor = new ResxImageExtractor();
            extractor.OptimizeResxImages("", _compressor, CompressionType.Lossless);
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

        /// <summary>
        /// Creates a .resx file with a ResXFileRef entry (external file reference, not embedded).
        /// </summary>
        private string CreateResxWithFileRef()
        {
            var doc = new XDocument(
                new XElement("root",
                    new XElement("data",
                        new XAttribute("name", "icon"),
                        new XAttribute("type", "System.Resources.ResXFileRef, System.Windows.Forms"),
                        new XElement("value", @"Resources\icon.png;System.Drawing.Bitmap, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"))));

            var resxPath = Path.Combine(_tempDir, "fileref.resx");
            doc.Save(resxPath);
            return resxPath;
        }

        /// <summary>
        /// Creates a .resx file with an image entry containing invalid base64 data.
        /// </summary>
        private string CreateResxWithInvalidBase64()
        {
            var doc = new XDocument(
                new XElement("root",
                    new XElement("data",
                        new XAttribute("name", "BadImage"),
                        new XAttribute("type", "System.Drawing.Bitmap, System.Drawing"),
                        new XAttribute("mimetype", "application/x-microsoft.net.object.bytearray.base64"),
                        new XElement("value", "this-is-not-valid-base64!!!"))));

            var resxPath = Path.Combine(_tempDir, "badbase64.resx");
            doc.Save(resxPath);
            return resxPath;
        }

        /// <summary>
        /// Creates a .resx file with an image entry that has an empty value element.
        /// </summary>
        private string CreateResxWithEmptyValue()
        {
            var doc = new XDocument(
                new XElement("root",
                    new XElement("data",
                        new XAttribute("name", "EmptyImage"),
                        new XAttribute("type", "System.Drawing.Bitmap, System.Drawing"),
                        new XAttribute("mimetype", "application/x-microsoft.net.object.bytearray.base64"),
                        new XElement("value", ""))));

            var resxPath = Path.Combine(_tempDir, "empty.resx");
            doc.Save(resxPath);
            return resxPath;
        }

        private string CreateBinaryPayloadResx(string resourceName, byte[] payload)
        {
            var doc = new XDocument(
                new XElement("root",
                    new XElement("data",
                        new XAttribute("name", resourceName),
                        new XAttribute("mimetype", "application/x-microsoft.net.object.binary.base64"),
                        new XElement("value", Convert.ToBase64String(payload)))));

            string resxPath = Path.Combine(_tempDir, resourceName + ".resx");
            doc.Save(resxPath);
            return resxPath;
        }

        private static byte[] ReadResourcePayload(string resxPath)
        {
            XDocument doc = XDocument.Load(resxPath);
            string base64 = doc.Root.Element("data").Element("value").Value;
            return Convert.FromBase64String(base64);
        }
    }
}
