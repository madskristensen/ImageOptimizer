using System.Collections.Generic;
using System.Threading;

namespace MadsKristensen.ImageOptimizer
{
    internal static class OrderedParallelProcessor
    {
        internal static T[] Process<T>(
            IReadOnlyList<string> filePaths,
            int maxDegreeOfParallelism,
            CancellationToken cancellationToken,
            Func<string, CancellationToken, T> processFile,
            Func<string, T> createCancelledResult)
            where T : class
        {
            if (filePaths == null)
            {
                throw new ArgumentNullException(nameof(filePaths));
            }

            if (processFile == null)
            {
                throw new ArgumentNullException(nameof(processFile));
            }

            if (createCancelledResult == null)
            {
                throw new ArgumentNullException(nameof(createCancelledResult));
            }

            var results = new T[filePaths.Count];
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, Math.Min(maxDegreeOfParallelism, filePaths.Count)),
                TaskScheduler = TaskScheduler.Default,
                CancellationToken = cancellationToken
            };

            try
            {
                Parallel.For(0, filePaths.Count, parallelOptions, index =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    results[index] = processFile(filePaths[index], cancellationToken);
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                for (var index = 0; index < results.Length; index++)
                {
                    results[index] ??= createCancelledResult(filePaths[index]);
                }
            }

            return results;
        }
    }
}
