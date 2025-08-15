using System.Collections.Generic;

namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Central location for all constants used throughout the ImageOptimizer
    /// </summary>
    internal static class Constants
    {
        // Process settings
        public const int ProcessTimeoutMs = 60000; // 60 seconds
        public const string CommandExecutor = "cmd";

        // Cache settings
        public const string VsDirectoryName = ".vs";
        public const string LossyCacheFileName = "cache-lossy.txt";
        public const string LosslessCacheFileName = "cache-lossless.txt";
        public const int EstimatedCacheLineLength = 50;

        // File search patterns
        public const string AllFilesPattern = "*.*";

        // StringBuilder capacity estimates
        public const int EstimatedResultStringLength = 200;
        public const int EstimatedCompressionResultLength = 150;
        public const int EstimatedFilePathCapacity = 100;

        // Parallel processing
        public const int MinParallelismDivider = 4; // imageCount / 4 for optimal thread count

        // User messages
        public const string NoImagesFoundMessage = "No images found to optimize";
        public const string NoImagesSelectedMessage = "No images selected";
        public const string OptimizingMessage = "Optimizing {0} images...";
        public const string AlreadyOptimizedMessage = "The images were already optimized";
        public const string OnlyNumericValuesMessage = "Only numeric values are allowed";

        // File extensions
        public static readonly string[] SupportedImageExtensions =
        [
            ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp"
        ];

        // MIME types
        public static readonly Dictionary<string, string> MimeTypeMappings = new(StringComparer.OrdinalIgnoreCase)
        {
            { "jpg", "image/jpeg" },
            { "jpeg", "image/jpeg" },
            { "svg", "image/svg+xml" },
            { "png", "image/png" },
            { "gif", "image/gif" },
            { "webp", "image/webp" }
        };
    }
}