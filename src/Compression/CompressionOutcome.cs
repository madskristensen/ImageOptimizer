namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Describes the outcome of an image compression or conversion attempt.
    /// </summary>
    public enum CompressionOutcome
    {
        Optimized,
        Unchanged,
        Cached,
        Failed,
        TimedOut,
        Cancelled
    }
}
