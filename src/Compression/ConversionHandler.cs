using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Handles batch WebP conversion operations with progress reporting.
    /// </summary>
    internal class ConversionHandler
    {
        private static OutputWindowPane _outputWindowPane;
        private int _processedCount;

        private const int _fileNameWidth = 40;
        private const int _sizeWidth = 10;
        private const int _percentWidth = 7;

        /// <summary>
        /// Converts a collection of images to WebP format.
        /// </summary>
        /// <param name="imageFilePaths">Paths to the image files to convert.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task ConvertToWebpAsync(
            IEnumerable<string> imageFilePaths,
            CancellationToken cancellationToken = default)
        {
            var imageFilesList = imageFilePaths.ToList();
            var imageCount = imageFilesList.Count;

            if (imageCount == 0)
            {
                return;
            }

            General options = await General.GetLiveInstanceAsync();
            var compressor = new Compressor(options.ProcessTimeoutMs, options.EffectiveLossyQuality);

            var maxDegreeOfParallelism = Math.Min(
                options.EffectiveMaxParallelThreads,
                Math.Max(1, imageCount / Constants.MinParallelismDivider));

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                TaskScheduler = TaskScheduler.Default,
                CancellationToken = cancellationToken
            };

            var conversionResults = new ConcurrentBag<CompressionResult>();
            _processedCount = 0;

            _outputWindowPane ??= await VS.Windows.CreateOutputWindowPaneAsync(Vsix.Name);
            await _outputWindowPane.ActivateAsync();
            var showDetails = options.ShowDetailedResults;
            var headerWritten = false;

            if (options.ShowProgressInStatusBar)
            {
                await VS.StatusBar.StartAnimationAsync(StatusAnimation.General);
                await VS.StatusBar.ShowMessageAsync(string.Format(Constants.ConvertingToWebpMessageFormat, 1, imageCount));
            }

            try
            {
                await Task.Run(() =>
                {
                    Parallel.ForEach(imageFilesList, parallelOptions, filePath =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            CompressionResult result = compressor.ConvertToWebp(filePath);
                            ProcessConversionResult(result, options.CreateBackup);
                            conversionResults.Add(result);

                            if (showDetails && result.Saving > 0)
                            {
                                if (!headerWritten)
                                {
                                    lock (this)
                                    {
                                        if (!headerWritten)
                                        {
                                            _outputWindowPane.WriteLineAsync(GetTableHeader()).FireAndForget();
                                            headerWritten = true;
                                        }
                                    }
                                }
                                _outputWindowPane.WriteLineAsync(FormatResultRow(result)).FireAndForget();
                            }

                            if (options.ShowProgressInStatusBar)
                            {
                                var processed = Interlocked.Increment(ref _processedCount);
                                var currentItem = Math.Min(processed + 1, imageCount);
                                VS.StatusBar.ShowMessageAsync(string.Format(Constants.ConvertingToWebpMessageFormat, currentItem, imageCount)).FireAndForget();
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
                await VS.StatusBar.ShowMessageAsync("WebP conversion cancelled");
                return;
            }
            finally
            {
                await VS.StatusBar.EndAnimationAsync(StatusAnimation.General);
            }

            await DisplayConversionSummaryAsync(conversionResults, options, headerWritten);
        }

        private static void ProcessConversionResult(CompressionResult result, bool createBackup)
        {
            if (result.Saving > 0 &&
                result.ResultFileSize > 0 &&
                !string.IsNullOrEmpty(result.ResultFileName) &&
                File.Exists(result.ResultFileName))
            {
                try
                {
                    // Place the .webp file next to the original
                    var webpDestination = Path.ChangeExtension(result.OriginalFileName, ".webp");
                    File.Copy(result.ResultFileName, webpDestination, true);
                    File.Delete(result.ResultFileName);

                    // Add the new file to the project if possible
                    AddFileToProjectAsync(webpDestination, result.OriginalFileName).FireAndForget();
                }
                catch (Exception ex)
                {
                    ex.LogAsync().FireAndForget();
                }
            }
            else
            {
                // Clean up temp file if conversion didn't produce savings
                if (!string.IsNullOrEmpty(result.ResultFileName) &&
                    !string.Equals(result.ResultFileName, result.OriginalFileName, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(result.ResultFileName))
                {
                    try { File.Delete(result.ResultFileName); } catch { }
                }
            }
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

        private async Task DisplayConversionSummaryAsync(IEnumerable<CompressionResult> results, General options, bool headerWritten)
        {
            var validResults = results.Where(r => r?.OriginalFileName != null).ToList();
            if (validResults.Count == 0)
            {
                return;
            }

            var totalSavings = validResults.Sum(r => r.Saving);
            var totalOriginalSize = validResults.Sum(r => r.OriginalFileSize);
            var totalResultSize = validResults.Sum(r => r.ResultFileSize);
            var successfulConversions = validResults.Count(r => r.Saving > 0);

            if (totalSavings > 0)
            {
                if (options.ShowDetailedResults && headerWritten)
                {
                    await _outputWindowPane.WriteLineAsync(GetTableSeparator());
                }

                if (successfulConversions > 0)
                {
                    var totalPercentageReduction = totalOriginalSize > 0
                        ? Math.Round(100 - (totalResultSize / (double)totalOriginalSize * 100), 1, MidpointRounding.AwayFromZero)
                        : 0;

                    var imageLabel = successfulConversions == 1 ? "image" : "images";
                    var message = string.Format(Constants.ConversionCompleteFormat,
                        successfulConversions, imageLabel,
                        CompressionResult.ToFileSize(totalSavings), totalPercentageReduction);

                    await VS.StatusBar.ShowMessageAsync(message);
                    await _outputWindowPane.WriteLineAsync(message + Environment.NewLine);
                }
                else
                {
                    await VS.StatusBar.ShowMessageAsync(Constants.AlreadyWebpMessage);
                    await _outputWindowPane.WriteLineAsync(Constants.AlreadyWebpMessage);
                }
            }
            else
            {
                await VS.StatusBar.ShowMessageAsync(Constants.AlreadyWebpMessage);
                await _outputWindowPane.WriteLineAsync(Constants.AlreadyWebpMessage);
            }

            await _outputWindowPane.ActivateAsync();
        }

        private static string GetTableHeader()
        {
            var header = $"{"File",-_fileNameWidth}  {"Before",_sizeWidth}  {"After",_sizeWidth}  {"Saved",_sizeWidth}  {"%",_percentWidth}";
            var separator = new string('-', _fileNameWidth + _sizeWidth * 3 + _percentWidth + 8);
            return header + Environment.NewLine + separator;
        }

        private static string GetTableSeparator()
        {
            return new string('-', _fileNameWidth + _sizeWidth * 3 + _percentWidth + 8);
        }

        private static string FormatResultRow(CompressionResult result)
        {
            var fileName = Path.GetFileName(result.OriginalFileName);
            var webpName = Path.ChangeExtension(fileName, ".webp");
            var displayName = $"{fileName} → {webpName}";

            if (displayName.Length > _fileNameWidth)
            {
                displayName = displayName.Substring(0, _fileNameWidth - 1) + "…";
            }

            return $"{displayName,-_fileNameWidth}  {CompressionResult.ToFileSize(result.OriginalFileSize),_sizeWidth}  {CompressionResult.ToFileSize(result.ResultFileSize),_sizeWidth}  {CompressionResult.ToFileSize(result.Saving),_sizeWidth}  {result.Percent,_percentWidth:F1}%";
        }
    }
}
