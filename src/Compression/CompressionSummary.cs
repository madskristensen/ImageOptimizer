using System.Collections.Generic;
using System.Linq;

namespace MadsKristensen.ImageOptimizer
{
    internal sealed class CompressionSummary
    {
        private CompressionSummary(IReadOnlyList<CompressionResult> results, TimeSpan elapsed)
        {
            Results = results;
            Elapsed = elapsed;
            Optimized = results.Count(result => result.Outcome == CompressionOutcome.Optimized);
            Unchanged = results.Count(result => result.Outcome == CompressionOutcome.Unchanged);
            Cached = results.Count(result => result.Outcome == CompressionOutcome.Cached);
            Failed = results.Count(result => result.Outcome == CompressionOutcome.Failed);
            TimedOut = results.Count(result => result.Outcome == CompressionOutcome.TimedOut);
            Cancelled = results.Count(result => result.Outcome == CompressionOutcome.Cancelled);
            TotalSavings = results.Where(result => result.Outcome == CompressionOutcome.Optimized).Sum(result => result.Saving);
            long originalSize = results.Where(result => result.Outcome == CompressionOutcome.Optimized).Sum(result => result.OriginalFileSize);
            PercentageSaved = originalSize > 0
                ? Math.Round(TotalSavings / (double)originalSize * 100, 1, MidpointRounding.AwayFromZero)
                : 0;
        }

        internal IReadOnlyList<CompressionResult> Results { get; }
        internal TimeSpan Elapsed { get; }
        internal int Optimized { get; }
        internal int Unchanged { get; }
        internal int Cached { get; }
        internal int Failed { get; }
        internal int TimedOut { get; }
        internal int Cancelled { get; }
        internal long TotalSavings { get; }
        internal double PercentageSaved { get; }

        internal static CompressionSummary Create(IEnumerable<CompressionResult> results, TimeSpan elapsed)
        {
            IReadOnlyList<CompressionResult> validResults = results?
                .Where(result => result?.OriginalFileName != null)
                .ToList() ?? [];

            return new CompressionSummary(validResults, elapsed);
        }

        internal string ToDisplayString(string successfulOutcome = "optimized")
        {
            return $"{Optimized} {successfulOutcome}, {Unchanged} unchanged, {Cached} cached, {Failed} failed, " +
                   $"{TimedOut} timed out, {Cancelled} cancelled. Saved {CompressionResult.ToFileSize(TotalSavings)} " +
                   $"/ {PercentageSaved:F1}% in {Elapsed.TotalSeconds:F1}s.";
        }
    }
}
