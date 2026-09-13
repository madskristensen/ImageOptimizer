using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using MadsKristensen.ImageOptimizer.Common;

namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Handles batch format conversion operations (WebP) with progress reporting.
    /// </summary>
    internal class ConversionHandler
    {
        private static OutputWindowPane _outputWindowPane;
        private int _processedCount;

        private const int _fileNameWidth = 40;
        private const int _sizeWidth = 10;
        private const int _percentWidth = 7;
        private const int _statusWidth = 10;

        /// <summary>
        /// Converts a collection of images to WebP format.
        /// </summary>
        public async Task ConvertToWebpAsync(
            IEnumerable<string> imageFilePaths,
            CancellationToken cancellationToken = default)
        {
            if (!ImageOperationCoordinator.TryStart(out IDisposable operationLease))
            {
                await VS.StatusBar.ShowMessageAsync(Constants.OptimizationAlreadyRunningMessage);
                return;
            }

            using (operationLease)
            {
                await ConvertAsync(imageFilePaths, "WebP", ".webp",
                    (compressor, file, token) => compressor.ConvertToWebp(file, token),
                    Compressor.IsConvertibleToWebp,
                    cancellationToken);
            }
        }

        private async Task ConvertAsync(
            IEnumerable<string> imageFilePaths,
            string formatName,
            string targetExtension,
            Func<Compressor, string, CancellationToken, CompressionResult> convertFunc,
            Func<string, bool> isConvertible,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<string> imageFilesList = FileUtilities.GetDistinctPaths(imageFilePaths).Where(isConvertible).ToList();
            var imageCount = imageFilesList.Count;
            var stopwatch = Stopwatch.StartNew();

            if (imageCount == 0)
            {
                return;
            }

            General options = await General.GetLiveInstanceAsync();
            var compressor = new Compressor(options.ProcessTimeoutMs, options.EffectiveLossyQuality);

            // Each file is converted by an external, mostly I/O-bound process, so use the
            // full configured thread budget (capped by the number of images to process).
            var maxDegreeOfParallelism = Math.Max(1, Math.Min(options.EffectiveMaxParallelThreads, imageCount));

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                TaskScheduler = TaskScheduler.Default,
                CancellationToken = cancellationToken
            };

            var conversionResults = new CompressionResult[imageCount];
            _processedCount = 0;

            _outputWindowPane ??= await VS.Windows.CreateOutputWindowPaneAsync(Vsix.Name);
            await _outputWindowPane.ActivateAsync();
            if (options.ShowProgressInStatusBar)
            {
                await VS.StatusBar.StartAnimationAsync(StatusAnimation.General);
                await VS.StatusBar.ShowMessageAsync(string.Format(Constants.ConvertingMessageFormat, 1, imageCount, formatName));
            }

            try
            {
                await Task.Run(() =>
                {
                    Parallel.For(0, imageCount, parallelOptions, index =>
                    {
                        var filePath = imageFilesList[index];
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            CompressionResult result = convertFunc(compressor, filePath, cancellationToken);
                            conversionResults[index] = ProcessConversionResult(result, targetExtension);

                            if (options.ShowProgressInStatusBar)
                            {
                                var processed = Interlocked.Increment(ref _processedCount);
                                if (processed == imageCount || processed % Constants.ProgressUpdateBatchSize == 0)
                                {
                                    VS.StatusBar.ShowMessageAsync(string.Format(Constants.ConvertingMessageFormat, processed, imageCount, formatName)).FireAndForget();
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            if (options.LogErrorsToOutput)
                            {
                                ex.LogAsync().FireAndForget();
                            }

                            conversionResults[index] = CompressionResult.Failed(filePath, ex.Message, TimeSpan.Zero);

                            if (!options.ContinueOnError)
                            {
                                throw;
                            }
                        }
                    });
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                for (var index = 0; index < conversionResults.Length; index++)
                {
                    conversionResults[index] ??= CompressionResult.Cancelled(imageFilesList[index], TimeSpan.Zero);
                }
            }
            finally
            {
                stopwatch.Stop();
                await VS.StatusBar.EndAnimationAsync(StatusAnimation.General);
            }

            await DisplayConversionSummaryAsync(conversionResults, options, formatName, targetExtension, stopwatch.Elapsed);
        }

        internal static CompressionResult ProcessConversionResult(CompressionResult result, string targetExtension)
        {
            if (result.Outcome == CompressionOutcome.Optimized &&
                result.ResultFileSize > 0 &&
                !string.IsNullOrEmpty(result.ResultFileName) &&
                File.Exists(result.ResultFileName))
            {
                try
                {
                    var destination = Path.ChangeExtension(result.OriginalFileName, targetExtension);
                    File.Copy(result.ResultFileName, destination, true);
                    File.Delete(result.ResultFileName);

                    AddFileToProjectAsync(destination, result.OriginalFileName).FireAndForget();
                    return result;
                }
                catch (Exception ex)
                {
                    ex.LogAsync().FireAndForget();
                    FileUtilities.SafeDeleteFile(result.ResultFileName);
                    return CompressionResult.Failed(result.OriginalFileName, ex.Message, result.Elapsed);
                }
            }

            if (result.Outcome == CompressionOutcome.Unchanged)
            {
                if (!string.IsNullOrEmpty(result.ResultFileName) &&
                    !string.Equals(result.ResultFileName, result.OriginalFileName, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(result.ResultFileName))
                {
                    try { File.Delete(result.ResultFileName); } catch { }
                }
            }
            else if (result.Outcome == CompressionOutcome.Optimized)
            {
                FileUtilities.SafeDeleteFile(result.ResultFileName);
                return CompressionResult.Failed(
                    result.OriginalFileName,
                    "The converter did not produce a usable output file.",
                    result.Elapsed);
            }

            return result;
        }

        private static async Task AddFileToProjectAsync(string newFilePath, string originalFilePath)
        {
            try
            {
                PhysicalFile original = await PhysicalFile.FromFileAsync(originalFilePath);
                if (original?.ContainingProject != null)
                {
                    await original.ContainingProject.AddExistingFilesAsync(newFilePath);
                }
            }
            catch (Exception ex)
            {
                ex.LogAsync().FireAndForget();
            }
        }

        private async Task DisplayConversionSummaryAsync(IEnumerable<CompressionResult> results, General options, string formatName, string targetExtension, TimeSpan elapsed)
        {
            CompressionSummary summary = CompressionSummary.Create(results, elapsed);
            if (summary.Results.Count == 0)
            {
                return;
            }

            if (options.ShowDetailedResults)
            {
                await _outputWindowPane.WriteLineAsync(GetTableHeader());

                foreach (CompressionResult result in summary.Results)
                {
                    await _outputWindowPane.WriteLineAsync(FormatResultRow(result, targetExtension));

                    if (options.LogErrorsToOutput && !string.IsNullOrWhiteSpace(result.ErrorMessage))
                    {
                        await _outputWindowPane.WriteLineAsync($"  {result.ErrorMessage}");
                    }
                }

                await _outputWindowPane.WriteLineAsync(GetTableSeparator());
            }

            string message = summary.ToDisplayString($"converted to {formatName}");
            await VS.StatusBar.ShowMessageAsync(message);
            await _outputWindowPane.WriteLineAsync(message + Environment.NewLine);

            await _outputWindowPane.ActivateAsync();
        }

        private static string GetTableHeader()
        {
            var header = $"{"File",-_fileNameWidth}  {"Status",-_statusWidth}  {"Before",_sizeWidth}  {"After",_sizeWidth}  {"Saved",_sizeWidth}  {"%",_percentWidth}";
            var separator = new string('-', _fileNameWidth + _statusWidth + _sizeWidth * 3 + _percentWidth + 10);
            return header + Environment.NewLine + separator;
        }

        private static string GetTableSeparator()
        {
            return new string('-', _fileNameWidth + _statusWidth + _sizeWidth * 3 + _percentWidth + 10);
        }

        private static string FormatResultRow(CompressionResult result, string targetExtension)
        {
            var fileName = Path.GetFileName(result.OriginalFileName);
            var targetName = Path.ChangeExtension(fileName, targetExtension);
            var displayName = $"{fileName} → {targetName}";

            if (displayName.Length > _fileNameWidth)
            {
                displayName = displayName.Substring(0, _fileNameWidth - 1) + "…";
            }

            return $"{displayName,-_fileNameWidth}  {result.Outcome,-_statusWidth}  {CompressionResult.ToFileSize(result.OriginalFileSize),_sizeWidth}  {CompressionResult.ToFileSize(result.ResultFileSize),_sizeWidth}  {CompressionResult.ToFileSize(result.Saving),_sizeWidth}  {result.Percent,_percentWidth:F1}%";
        }
    }
}
