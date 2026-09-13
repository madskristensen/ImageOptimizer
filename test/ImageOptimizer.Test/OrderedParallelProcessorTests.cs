using System;
using System.Linq;
using System.Threading;
using MadsKristensen.ImageOptimizer;
using MadsKristensen.ImageOptimizer.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Test
{
    [TestClass]
    public class OrderedParallelProcessorTests
    {
        [TestMethod]
        public void Process_PreservesDistinctInputOrderWithOutOfOrderCompletion()
        {
            string[] input = { "first.png", "second.png", "FIRST.PNG", "third.png" };
            var distinct = FileUtilities.GetDistinctPaths(input);

            string[] results = OrderedParallelProcessor.Process(
                distinct,
                distinct.Count,
                CancellationToken.None,
                (file, _) =>
                {
                    Thread.Sleep(file.StartsWith("first", StringComparison.OrdinalIgnoreCase) ? 50 : 1);
                    return file;
                },
                file => file);

            CollectionAssert.AreEqual(new[] { "first.png", "second.png", "third.png" }, results);
        }

        [TestMethod]
        public void Process_PreservesMixedOutcomesAtTheirInputIndexes()
        {
            string[] input = { "optimized.png", "failed.png", "cached.png" };

            CompressionResult[] results = OrderedParallelProcessor.Process(
                input,
                input.Length,
                CancellationToken.None,
                (file, _) => file.StartsWith("failed", StringComparison.Ordinal)
                    ? CompressionResult.Failed(file, "failure", TimeSpan.Zero)
                    : file.StartsWith("cached", StringComparison.Ordinal)
                        ? CompressionResult.Cached(file)
                        : new CompressionResult(file, file, TimeSpan.Zero),
                file => CompressionResult.Cancelled(file, TimeSpan.Zero));

            CollectionAssert.AreEqual(input, results.Select(result => result.OriginalFileName).ToArray());
            Assert.AreEqual(CompressionOutcome.Unchanged, results[0].Outcome);
            Assert.AreEqual(CompressionOutcome.Failed, results[1].Outcome);
            Assert.AreEqual(CompressionOutcome.Cached, results[2].Outcome);
        }

        [TestMethod]
        public void Process_PreCancelledOperationReturnsCancelledResultForEveryInput()
        {
            string[] input = { "first.png", "second.png" };
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                CompressionResult[] results = OrderedParallelProcessor.Process(
                    input,
                    input.Length,
                    cancellation.Token,
                    (file, token) => throw new AssertFailedException("Processing should not start."),
                    file => CompressionResult.Cancelled(file, TimeSpan.Zero));

                Assert.IsTrue(results.All(result => result.Outcome == CompressionOutcome.Cancelled));
                CollectionAssert.AreEqual(input, results.Select(result => result.OriginalFileName).ToArray());
            }
        }
    }
}
