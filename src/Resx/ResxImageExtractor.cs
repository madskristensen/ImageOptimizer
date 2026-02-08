using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MadsKristensen.ImageOptimizer.Common;

namespace MadsKristensen.ImageOptimizer.Resx
{
    /// <summary>
    /// Extracts embedded images from .resx files, optimizes them, and re-embeds the results.
    /// </summary>
    internal class ResxImageExtractor
    {
        // Magic byte signatures for supported image formats
        private static readonly byte[] _pngSignature = { 0x89, 0x50, 0x4E, 0x47 };
        private static readonly byte[] _jpegSignature = { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] _gifSignature = { 0x47, 0x49, 0x46 };

        // BinaryFormatter header: the serialized stream starts with 0x00 0x01 0x00 0x00 0x00 ...
        private static readonly byte[] _binaryFormatterSignature = { 0x00, 0x01, 0x00, 0x00, 0x00 };

        /// <summary>
        /// Optimizes all embedded images in a .resx file and returns per-resource results.
        /// </summary>
        /// <param name="resxPath">Full path to the .resx file.</param>
        /// <param name="compressor">The compressor to use for optimization.</param>
        /// <param name="compressionType">Lossless or lossy compression.</param>
        /// <returns>List of compression results, one per optimized resource entry.</returns>
        public IReadOnlyList<ResxCompressionResult> OptimizeResxImages(
            string resxPath, Compressor compressor, CompressionType compressionType)
        {
            if (compressor == null)
            {
                throw new ArgumentNullException(nameof(compressor));
            }

            ValidationResult validation = InputValidator.ValidateFilePath(resxPath);
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.ErrorMessage, nameof(resxPath));
            }

            var validatedPath = validation.GetValue<string>();
            if (!File.Exists(validatedPath))
            {
                return [];
            }

            XDocument doc = XDocument.Load(validatedPath, LoadOptions.PreserveWhitespace);
            var imageNodes = FindImageDataNodes(doc);

            if (imageNodes.Count == 0)
            {
                return [];
            }

            var results = new List<ResxCompressionResult>();
            var modified = false;

            foreach (ImageDataNode node in imageNodes)
            {
                ResxCompressionResult result = OptimizeSingleEntry(node, compressor, compressionType, validatedPath);
                results.Add(result);

                if (result.Saving > 0)
                {
                    node.ValueElement.Value = result.OptimizedBase64;
                    modified = true;
                }
            }

            if (modified)
            {
                // Preserve the original encoding and formatting
                doc.Save(validatedPath);
            }

            return results;
        }

        /// <summary>
        /// Finds all data nodes in the .resx that contain embedded images.
        /// </summary>
        private static List<ImageDataNode> FindImageDataNodes(XDocument doc)
        {
            var results = new List<ImageDataNode>();
            IEnumerable<XElement> dataElements = doc.Root?.Elements("data") ?? Enumerable.Empty<XElement>();

            foreach (XElement dataElement in dataElements)
            {
                var typeName = (string)dataElement.Attribute("type");
                var mimetype = (string)dataElement.Attribute("mimetype");
                XElement valueElement = dataElement.Element("value");

                if (valueElement == null || string.IsNullOrWhiteSpace(valueElement.Value))
                {
                    continue;
                }

                var resourceName = (string)dataElement.Attribute("name") ?? "unknown";

                // Case 1: Raw byte array (mimetype = application/x-microsoft.net.object.bytearray.base64)
                // with a type indicating an image (Bitmap, Icon, etc.)
                if (IsImageByteArrayEntry(typeName, mimetype))
                {
                    results.Add(new ImageDataNode(resourceName, valueElement, ImageDataFormat.ByteArray));
                }
                // Case 2: BinaryFormatter-serialized (mimetype = application/x-microsoft.net.object.binary.base64)
                // We probe the decoded bytes for image magic bytes inside the serialized payload
                else if (IsBinaryFormatterEntry(mimetype))
                {
                    byte[] rawBytes = TryDecodeBase64(valueElement.Value);
                    if (rawBytes != null && ContainsEmbeddedImage(rawBytes))
                    {
                        results.Add(new ImageDataNode(resourceName, valueElement, ImageDataFormat.BinaryFormatter));
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Optimizes a single .resx image entry by extracting, compressing, and re-encoding.
        /// </summary>
        private static ResxCompressionResult OptimizeSingleEntry(
            ImageDataNode node, Compressor compressor, CompressionType compressionType, string resxPath)
        {
            byte[] originalBytes = TryDecodeBase64(node.ValueElement.Value);
            if (originalBytes == null || originalBytes.Length == 0)
            {
                return ResxCompressionResult.Zero(node.ResourceName, resxPath);
            }

            byte[] imageBytes;
            int imageOffset;
            int imageLength;

            if (node.Format == ImageDataFormat.BinaryFormatter)
            {
                // Locate the raw image payload inside the BinaryFormatter wrapper
                if (!TryFindImagePayload(originalBytes, out imageOffset, out imageLength))
                {
                    return ResxCompressionResult.Zero(node.ResourceName, resxPath);
                }
                imageBytes = new byte[imageLength];
                Buffer.BlockCopy(originalBytes, imageOffset, imageBytes, 0, imageLength);
            }
            else
            {
                imageBytes = originalBytes;
                imageOffset = 0;
                imageLength = originalBytes.Length;
            }

            var extension = DetectImageExtension(imageBytes);
            if (extension == null)
            {
                return ResxCompressionResult.Zero(node.ResourceName, resxPath);
            }

            // Write to temp file, compress, read back
            string tempInput = null;
            try
            {
                tempInput = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + extension);
                File.WriteAllBytes(tempInput, imageBytes);

                CompressionResult compResult = compressor.CompressFile(tempInput, compressionType);

                if (compResult.Saving <= 0 || !File.Exists(compResult.ResultFileName))
                {
                    return ResxCompressionResult.Zero(node.ResourceName, resxPath);
                }

                byte[] optimizedImageBytes = File.ReadAllBytes(compResult.ResultFileName);
                FileUtilities.SafeDeleteFile(compResult.ResultFileName);

                // Re-assemble the full payload
                byte[] finalBytes;
                if (node.Format == ImageDataFormat.BinaryFormatter)
                {
                    finalBytes = ReassembleBinaryFormatterPayload(originalBytes, imageOffset, imageLength, optimizedImageBytes);
                }
                else
                {
                    finalBytes = optimizedImageBytes;
                }

                var optimizedBase64 = Convert.ToBase64String(finalBytes);
                long originalSize = originalBytes.Length;
                long optimizedSize = finalBytes.Length;

                return new ResxCompressionResult(
                    node.ResourceName,
                    resxPath,
                    originalSize,
                    optimizedSize,
                    optimizedBase64);
            }
            finally
            {
                if (tempInput != null)
                {
                    FileUtilities.SafeDeleteFile(tempInput);
                }
            }
        }

        /// <summary>
        /// Checks if the data entry represents a raw image byte array.
        /// </summary>
        private static bool IsImageByteArrayEntry(string typeName, string mimetype)
        {
            if (!string.Equals(mimetype, "application/x-microsoft.net.object.bytearray.base64", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            // Match common .NET image types
            return typeName.IndexOf("System.Drawing.Bitmap", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("System.Drawing.Icon", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("System.Drawing.Image", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Checks if the data entry uses BinaryFormatter serialization.
        /// </summary>
        private static bool IsBinaryFormatterEntry(string mimetype)
        {
            return string.Equals(mimetype, "application/x-microsoft.net.object.binary.base64", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if a BinaryFormatter-serialized blob contains an embedded image by scanning for magic bytes.
        /// </summary>
        private static bool ContainsEmbeddedImage(byte[] data)
        {
            return FindSignatureOffset(data, _pngSignature) >= 0
                || FindSignatureOffset(data, _jpegSignature) >= 0
                || FindSignatureOffset(data, _gifSignature) >= 0;
        }

        /// <summary>
        /// Locates the raw image payload within a BinaryFormatter-serialized blob.
        /// Returns the offset and length of the image data.
        /// </summary>
        private static bool TryFindImagePayload(byte[] data, out int offset, out int length)
        {
            offset = -1;
            length = 0;

            // Try each image format signature
            int pngOffset = FindSignatureOffset(data, _pngSignature);
            int jpegOffset = FindSignatureOffset(data, _jpegSignature);
            int gifOffset = FindSignatureOffset(data, _gifSignature);

            // Pick the earliest valid match
            var candidates = new[] { pngOffset, jpegOffset, gifOffset };
            offset = candidates.Where(o => o >= 0).DefaultIfEmpty(-1).Min();

            if (offset < 0)
            {
                return false;
            }

            // For BinaryFormatter, the image bytes typically run to near the end.
            // The serialization trailer is minimal (a few bytes), so we scan backward
            // to find the actual image end. For safety, take everything from the offset
            // to the end of the meaningful data.
            length = FindImageEnd(data, offset);
            return length > 0;
        }

        /// <summary>
        /// Determines the length of the image data starting at the given offset.
        /// For PNG: scans for IEND chunk. For JPEG: scans for FFD9 marker. For GIF: scans for 3B trailer.
        /// Falls back to (total - offset) minus a small BinaryFormatter epilogue.
        /// </summary>
        private static int FindImageEnd(byte[] data, int offset)
        {
            if (offset < 0 || offset >= data.Length)
            {
                return 0;
            }

            // Detect format at offset
            if (MatchesSignature(data, offset, _pngSignature))
            {
                return FindPngEnd(data, offset);
            }

            if (MatchesSignature(data, offset, _jpegSignature))
            {
                return FindJpegEnd(data, offset);
            }

            if (MatchesSignature(data, offset, _gifSignature))
            {
                return FindGifEnd(data, offset);
            }

            // Unknown format — take the rest minus BinaryFormatter epilogue (typically ≤11 bytes)
            return Math.Max(0, data.Length - offset - 11);
        }

        /// <summary>
        /// Finds the end of a PNG stream by locating the IEND chunk.
        /// </summary>
        private static int FindPngEnd(byte[] data, int offset)
        {
            // IEND chunk: 00 00 00 00 49 45 4E 44 AE 42 60 82
            byte[] iendMarker = { 0x49, 0x45, 0x4E, 0x44 };

            for (int i = offset + 8; i < data.Length - 7; i++)
            {
                if (data[i] == 0x49 && MatchesSignature(data, i, iendMarker))
                {
                    // IEND chunk: 4 bytes length + 4 bytes "IEND" + 4 bytes CRC
                    // The length field is 4 bytes before the "IEND" marker
                    return (i + 4 + 4) - offset; // IEND marker + CRC
                }
            }

            // Fallback: take everything from offset
            return data.Length - offset;
        }

        /// <summary>
        /// Finds the end of a JPEG stream by locating the EOI marker (FF D9).
        /// </summary>
        private static int FindJpegEnd(byte[] data, int offset)
        {
            for (int i = data.Length - 1; i > offset; i--)
            {
                if (data[i] == 0xD9 && data[i - 1] == 0xFF)
                {
                    return (i + 1) - offset;
                }
            }

            return data.Length - offset;
        }

        /// <summary>
        /// Finds the end of a GIF stream by locating the trailer byte (0x3B).
        /// </summary>
        private static int FindGifEnd(byte[] data, int offset)
        {
            // GIF trailer is a single 0x3B byte — scan from end backward
            for (int i = data.Length - 1; i > offset; i--)
            {
                if (data[i] == 0x3B)
                {
                    return (i + 1) - offset;
                }
            }

            return data.Length - offset;
        }

        /// <summary>
        /// Reassembles a BinaryFormatter payload with the optimized image bytes replacing the original.
        /// </summary>
        private static byte[] ReassembleBinaryFormatterPayload(byte[] original, int imageOffset, int imageLength, byte[] newImageBytes)
        {
            // The BinaryFormatter payload structure:
            //   [header...][image bytes][trailer...]
            // We need to update the byte-array length field that precedes the image data.
            // In BinaryFormatter's serialization of byte[], the length is stored as a 4-byte little-endian int
            // immediately before the raw byte data.

            int headerLength = imageOffset;
            int trailerStart = imageOffset + imageLength;
            int trailerLength = original.Length - trailerStart;

            // Update the length prefix (4 bytes before image data) if it matches the original image length
            byte[] header = new byte[headerLength];
            Buffer.BlockCopy(original, 0, header, 0, headerLength);

            if (headerLength >= 4)
            {
                int storedLength = BitConverter.ToInt32(header, headerLength - 4);
                if (storedLength == imageLength)
                {
                    byte[] newLengthBytes = BitConverter.GetBytes(newImageBytes.Length);
                    Buffer.BlockCopy(newLengthBytes, 0, header, headerLength - 4, 4);
                }
            }

            byte[] result = new byte[headerLength + newImageBytes.Length + trailerLength];
            Buffer.BlockCopy(header, 0, result, 0, headerLength);
            Buffer.BlockCopy(newImageBytes, 0, result, headerLength, newImageBytes.Length);

            if (trailerLength > 0)
            {
                Buffer.BlockCopy(original, trailerStart, result, headerLength + newImageBytes.Length, trailerLength);
            }

            return result;
        }

        /// <summary>
        /// Detects image file extension from magic bytes.
        /// </summary>
        internal static string DetectImageExtension(byte[] data)
        {
            if (data == null || data.Length < 4)
            {
                return null;
            }

            if (MatchesSignature(data, 0, _pngSignature))
            {
                return ".png";
            }

            if (MatchesSignature(data, 0, _jpegSignature))
            {
                return ".jpg";
            }

            if (MatchesSignature(data, 0, _gifSignature))
            {
                return ".gif";
            }

            return null;
        }

        /// <summary>
        /// Safely decodes a base64 string, returning null on failure.
        /// </summary>
        private static byte[] TryDecodeBase64(string base64)
        {
            try
            {
                // .resx base64 values often contain whitespace/newlines
                var cleaned = base64.Replace("\r", "").Replace("\n", "").Replace(" ", "").Trim();
                return Convert.FromBase64String(cleaned);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>
        /// Finds the first occurrence of a byte signature within a byte array.
        /// </summary>
        private static int FindSignatureOffset(byte[] data, byte[] signature)
        {
            if (data == null || signature == null || data.Length < signature.Length)
            {
                return -1;
            }

            int limit = data.Length - signature.Length;
            for (int i = 0; i <= limit; i++)
            {
                if (MatchesSignature(data, i, signature))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Checks if data at the given offset matches the specified signature.
        /// </summary>
        private static bool MatchesSignature(byte[] data, int offset, byte[] signature)
        {
            if (offset + signature.Length > data.Length)
            {
                return false;
            }

            for (int i = 0; i < signature.Length; i++)
            {
                if (data[offset + i] != signature[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Identifies the format of a .resx image data entry.
        /// </summary>
        internal enum ImageDataFormat
        {
            /// <summary>Raw image bytes encoded as base64.</summary>
            ByteArray,
            /// <summary>BinaryFormatter-serialized object containing image bytes.</summary>
            BinaryFormatter
        }

        /// <summary>
        /// Represents a .resx data node that contains an embedded image.
        /// </summary>
        private sealed class ImageDataNode
        {
            public string ResourceName { get; }
            public XElement ValueElement { get; }
            public ImageDataFormat Format { get; }

            public ImageDataNode(string resourceName, XElement valueElement, ImageDataFormat format)
            {
                ResourceName = resourceName;
                ValueElement = valueElement;
                Format = format;
            }
        }
    }
}
