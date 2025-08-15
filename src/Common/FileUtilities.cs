using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MadsKristensen.ImageOptimizer.Common
{
    /// <summary>
    /// Utility methods for file operations and validation
    /// </summary>
    internal static class FileUtilities
    {
        /// <summary>
        /// Checks if a file is supported for image optimization
        /// </summary>
        public static bool IsImageFileSupported(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var ext = Path.GetExtension(fileName);
            return Constants.SupportedImageExtensions.Any(s =>
                s.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Safely deletes a file with error handling
        /// </summary>
        public static bool SafeDeleteFile(string filePath)
        {
            return ErrorHandler.SafeExecute(() =>
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }, false);
        }

        /// <summary>
        /// Gets MIME type from file extension
        /// </summary>
        public static string GetMimeTypeFromExtension(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return "application/octet-stream";
            }

            var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();

            return Constants.MimeTypeMappings.TryGetValue(ext, out var mimeType)
                ? mimeType
                : $"image/{ext}";
        }

        /// <summary>
        /// Safely copies a file with error handling and validation
        /// </summary>
        public static bool SafeCopyFile(string sourceFile, string destinationFile, bool overwrite = true)
        {
            return ErrorHandler.SafeExecute(() =>
            {
                if (!ErrorHandler.ValidateFilePath(sourceFile, out var error))
                {
                    throw new FileNotFoundException(error);
                }

                var destDir = Path.GetDirectoryName(destinationFile);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    _ = Directory.CreateDirectory(destDir);
                }

                File.Copy(sourceFile, destinationFile, overwrite);
                return true;
            }, false);
        }

        /// <summary>
        /// Gets all supported image files from a directory with parallel processing
        /// </summary>
        public static IEnumerable<string> GetSupportedImageFiles(string directoryPath,
            SearchOption searchOption = SearchOption.AllDirectories)
        {
            return string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath)
                ? []
                : ErrorHandler.SafeExecute(() =>
            {
                return Directory.EnumerateFiles(directoryPath, Constants.AllFilesPattern, searchOption)
                                .AsParallel()
                                .Where(IsImageFileSupported)
                                .ToList();
            }, Enumerable.Empty<string>());
        }

        /// <summary>
        /// Creates a unique temporary file with the same extension as the source
        /// </summary>
        public static string CreateTempFileWithExtension(string sourceFile)
        {
            var extension = Path.GetExtension(sourceFile);
            return Path.ChangeExtension(Path.GetTempFileName(), extension);
        }

        /// <summary>
        /// Safely gets file size in bytes
        /// </summary>
        public static long GetFileSizeBytes(string filePath)
        {
            return ErrorHandler.SafeExecute(() =>
            {
                return ErrorHandler.ValidateFilePath(filePath, out _) ? new FileInfo(filePath).Length : 0L;
            }, 0L);
        }
    }
}