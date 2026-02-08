using System.Collections.Generic;

namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Central location for all constants used throughout the ImageOptimizer.
    /// </summary>
    internal static class Constants
    {
        // Process settings
        public const int DefaultProcessTimeoutMs = 60000; // 60 seconds
        public const int ProcessKillGracePeriodMs = 5000; // 5 seconds for graceful termination
        public const string CommandExecutor = "cmd";

        // Compression settings
        public const int DefaultLossyQuality = 85; // Quality level for lossy compression (60-100)
        public const int MinLossyQuality = 60;
        public const int MaxLossyQuality = 100;

        // Path validation
        public const int MaxPathLength = 260; // Windows MAX_PATH limitation

        // Cache settings
        public const string VsDirectoryName = ".vs";
        public const string LossyCacheFileName = "cache-lossy.txt";
        public const string LosslessCacheFileName = "cache-lossless.txt";
        public const int EstimatedCacheLineLength = 50;

        // File search patterns
        public const string AllFilesPattern = "*.*";

        // StringBuilder capacity estimates
        public const int EstimatedResultStringLength = 80; // Single-line format
        public const int EstimatedCompressionResultLength = 100;
        public const int EstimatedFilePathCapacity = 100;

        // Parallel processing
        public const int MinParallelismDivider = 4; // imageCount / 4 for optimal thread count
        public const int DefaultMaxParallelThreads = 0; // 0 = auto

        // User messages
        public const string NoImagesFoundMessage = "No images found to optimize";
        public const string NoImagesSelectedMessage = "No images selected";
        public const string OptimizingMessageFormat = "Optimizing {0} of {1} images...";
        public const string AlreadyOptimizedMessage = "The images were already optimized";
        public const string OnlyNumericValuesMessage = "Only numeric values are allowed";
        public const string OptimizationCompleteFormat = "{0} {1} optimized. Total saving of {2} / {3}%";
        public const string ResizingMessageFormat = "Resizing {0}...";
        public const string ResizedMessageFormat = "{0} was resized to {1}x{2} at {3} DPI";
        public const string Base64CopiedFormat = "Base64 DataURI copied to clipboard ({0:N0} characters)";
        public const string Base64FailedMessage = "Failed to create Base64 string";
        public const string InvalidFileFormat = "Invalid file: {0}";
        public const string ConvertingToWebpMessageFormat = "Converting {0} of {1} to WebP...";
        public const string ConvertedToWebpFormat = "Converted {0} → {1} ({2} → {3}, {4}% reduction)";
        public const string ConversionCompleteFormat = "{0} {1} converted to WebP. Total saving of {2} / {3}%";
        public const string NoConvertibleImagesMessage = "No images found to convert (PNG and JPG only)";
        public const string AlreadyWebpMessage = "Selected files are already in WebP format";

        // DPI validation
        public const float MinDpi = 1f;
        public const float MaxDpi = 2400f;

        // Dimension validation
        public const int MinDimension = 1;
        public const int MaxDimension = 10000;

        // File extensions - use HashSet for O(1) lookup
        public static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp"
        };

        // Bitmap-only extensions (no SVG for resize operations)
        public static readonly HashSet<string> BitmapOnlyExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".webp"
        };

        // Extensions that can be converted to WebP (pingo supports PNG/JPEG → WebP)
        public static readonly HashSet<string> ConvertibleToWebpExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg"
        };

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