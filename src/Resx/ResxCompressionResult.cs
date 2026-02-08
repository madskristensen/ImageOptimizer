namespace MadsKristensen.ImageOptimizer.Resx
{
    /// <summary>
    /// Represents the result of optimizing a single image resource within a .resx file.
    /// </summary>
    internal sealed class ResxCompressionResult
    {
        /// <summary>
        /// The name of the resource entry in the .resx file.
        /// </summary>
        public string ResourceName { get; }

        /// <summary>
        /// The path to the .resx file that contains this resource.
        /// </summary>
        public string ResxFilePath { get; }

        /// <summary>
        /// Original size of the base64-decoded bytes.
        /// </summary>
        public long OriginalSize { get; }

        /// <summary>
        /// Size after optimization.
        /// </summary>
        public long OptimizedSize { get; }

        /// <summary>
        /// Bytes saved by optimization.
        /// </summary>
        public long Saving => OriginalSize - OptimizedSize;

        /// <summary>
        /// The optimized base64 string to re-embed. Null if no optimization was achieved.
        /// </summary>
        public string OptimizedBase64 { get; }

        /// <summary>
        /// Percentage reduction, e.g. 12.5 means 12.5% smaller.
        /// </summary>
        public double PercentSaved => OriginalSize > 0
            ? Math.Round((1.0 - (double)OptimizedSize / OriginalSize) * 100, 1, MidpointRounding.AwayFromZero)
            : 0;

        public ResxCompressionResult(
            string resourceName,
            string resxFilePath,
            long originalSize,
            long optimizedSize,
            string optimizedBase64)
        {
            ResourceName = resourceName;
            ResxFilePath = resxFilePath;
            OriginalSize = originalSize;
            OptimizedSize = optimizedSize;
            OptimizedBase64 = optimizedBase64;
        }

        /// <summary>
        /// Creates a zero-savings result for a resource that could not be optimized.
        /// </summary>
        public static ResxCompressionResult Zero(string resourceName, string resxFilePath)
        {
            return new ResxCompressionResult(resourceName, resxFilePath, 0, 0, null);
        }
    }
}
