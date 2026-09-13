using System;
using System.IO;
using System.Threading;
using MadsKristensen.ImageOptimizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Test
{
    [TestClass]
    public class ReliabilityTests
    {
        private string _testFolder;
        private string _sourceFile;

        [TestInitialize]
        public void Initialize()
        {
            _testFolder = Path.Combine(Path.GetTempPath(), "ImageOptimizer_Reliability_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_testFolder, ".vs", Vsix.Name));
            _sourceFile = Path.Combine(_testFolder, "source.png");
            File.WriteAllText(_sourceFile, new string('a', 100));
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testFolder))
            {
                Directory.Delete(_testFolder, true);
            }
        }

        [TestMethod]
        public void ProcessCompressionResult_SuccessfullyReplacesAndCachesFile()
        {
            var resultFile = Path.Combine(_testFolder, "result.png");
            File.WriteAllText(resultFile, new string('b', 50));
            var result = new CompressionResult(_sourceFile, resultFile, TimeSpan.FromSeconds(1));
            var cache = new Cache(_sourceFile, CompressionType.Lossless);

            CompressionResult processed = CompressionResultProcessor.Process(result, cache, false);

            Assert.AreEqual(CompressionOutcome.Optimized, processed.Outcome);
            Assert.AreEqual(50, new FileInfo(_sourceFile).Length);
            Assert.IsFalse(File.Exists(resultFile));
            Assert.IsTrue(cache.IsFullyOptimized(_sourceFile));
        }

        [TestMethod]
        public void ProcessCompressionResult_BackupsDoNotCollide()
        {
            var firstResultFile = Path.Combine(_testFolder, "first-result.png");
            File.WriteAllText(firstResultFile, new string('b', 80));
            CompressionResultProcessor.Process(
                new CompressionResult(_sourceFile, firstResultFile, TimeSpan.Zero),
                null,
                true);

            var secondResultFile = Path.Combine(_testFolder, "second-result.png");
            File.WriteAllText(secondResultFile, new string('c', 60));
            CompressionResultProcessor.Process(
                new CompressionResult(_sourceFile, secondResultFile, TimeSpan.Zero),
                null,
                true);

            string backupDirectory = Path.Combine(_testFolder, ".vs", Vsix.Name, "backups");
            Assert.AreEqual(2, Directory.GetFiles(backupDirectory).Length);
        }

        [TestMethod]
        public void ProcessCompressionResult_FailureIsNotCached()
        {
            var cache = new Cache(_sourceFile, CompressionType.Lossless);
            CompressionResult failed = CompressionResult.Failed(_sourceFile, "failure", TimeSpan.Zero);

            CompressionResult processed = CompressionResultProcessor.Process(failed, cache, false);

            Assert.AreEqual(CompressionOutcome.Failed, processed.Outcome);
            Assert.IsFalse(cache.ContainsFile(_sourceFile));
        }

        [TestMethod]
        public void ProcessConversionResult_MissingOutputIsReportedAsFailure()
        {
            var resultFile = Path.Combine(_testFolder, "missing-result.webp");
            File.WriteAllText(resultFile, new string('b', 50));
            var result = new CompressionResult(_sourceFile, resultFile, TimeSpan.Zero);
            File.Delete(resultFile);

            CompressionResult processed = ConversionHandler.ProcessConversionResult(result, ".webp");

            Assert.AreEqual(CompressionOutcome.Failed, processed.Outcome);
            StringAssert.Contains(processed.ErrorMessage, "usable output");
        }

        [TestMethod]
        public void CompressionSummary_CountsEveryOutcome()
        {
            var optimizedFile = Path.Combine(_testFolder, "optimized.png");
            File.WriteAllText(optimizedFile, new string('b', 50));

            CompressionResult[] results =
            {
                new CompressionResult(_sourceFile, optimizedFile, TimeSpan.Zero),
                new CompressionResult(_sourceFile, _sourceFile, TimeSpan.Zero),
                CompressionResult.Cached(_sourceFile),
                CompressionResult.Failed(_sourceFile, "failed", TimeSpan.Zero),
                CompressionResult.TimedOut(_sourceFile, "timeout", TimeSpan.Zero),
                CompressionResult.Cancelled(_sourceFile, TimeSpan.Zero)
            };

            CompressionSummary summary = CompressionSummary.Create(results, TimeSpan.FromSeconds(2));

            Assert.AreEqual(1, summary.Optimized);
            Assert.AreEqual(1, summary.Unchanged);
            Assert.AreEqual(1, summary.Cached);
            Assert.AreEqual(1, summary.Failed);
            Assert.AreEqual(1, summary.TimedOut);
            Assert.AreEqual(1, summary.Cancelled);
            StringAssert.Contains(summary.ToDisplayString(), "1 timed out");
        }

        [TestMethod]
        public void OperationCoordinator_RejectsOverlapAndAllowsNextOperation()
        {
            IDisposable firstLease;
            IDisposable secondLease;

            Assert.IsTrue(ImageOperationCoordinator.TryStart(out firstLease));
            Assert.IsFalse(ImageOperationCoordinator.TryStart(out secondLease));

            firstLease.Dispose();

            Assert.IsTrue(ImageOperationCoordinator.TryStart(out secondLease));
            secondLease.Dispose();
        }

        [TestMethod]
        public void RunTool_NonZeroExitCodeThrows()
        {
            var compressor = new Compressor();

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() =>
                compressor.RunTool(Environment.GetEnvironmentVariable("ComSpec"), "/c exit 7", _sourceFile, CancellationToken.None));

            StringAssert.Contains(exception.Message, "code 7");
        }

        [TestMethod]
        public void RunTool_TimeoutThrows()
        {
            var compressor = new Compressor(25);

            Assert.ThrowsException<TimeoutException>(() =>
                compressor.RunTool(Environment.GetEnvironmentVariable("ComSpec"), "/c ping 127.0.0.1 -n 6 > nul", _sourceFile, CancellationToken.None));
        }

        [TestMethod]
        public void RunTool_CancellationStopsProcess()
        {
            var compressor = new Compressor(10000);
            using (var cancellation = new CancellationTokenSource(100))
            {
                Assert.ThrowsException<OperationCanceledException>(() =>
                    compressor.RunTool(Environment.GetEnvironmentVariable("ComSpec"), "/c ping 127.0.0.1 -n 6 > nul", _sourceFile, cancellation.Token));
            }
        }
    }
}
