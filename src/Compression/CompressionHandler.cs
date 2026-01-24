using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Handles batch image optimization operations with caching and progress reporting.
    /// </summary>
    internal class CompressionHandler
    {
        private static readonly RatingPrompt _ratingPrompt = new("MadsKristensen.ImageOptimizer64bit", Vsix.Name, General.Instance);
        private static readonly object _saveLock = new();
        private static OutputWindowPane _outputWindowPane;
        private int _processedCount;

        // Fixed column widths for table output
        private const int _fileNameWidth = 40;
        private const int _sizeWidth = 10;
        private const int _percentWidth = 7;

        /// <summary>
        /// Optimizes a collection of images using the specified compression type.
        /// </summary>
        /// <param name="imageFilePaths">Paths to the image files to optimize.</param>
        /// <param name="type">The type of compression to apply.</param>
        /// <param name="solutionFullName">Optional solution path for cache location.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task OptimizeImagesAsync(
            IEnumerable<string> imageFilePaths,
            CompressionType type,
            string solutionFullName = null,
            CancellationToken cancellationToken = default)
        {
            var imageFilesList = imageFilePaths.ToList();
            var imageCount = imageFilesList.Count;

            if (imageCount == 0)
            {
                return;
            }

            // Load options
            General options = await General.GetLiveInstanceAsync();
            var compressor = new Compressor(options.ProcessTimeoutMs, options.EffectiveLossyQuality);
            var cacheRoot = string.IsNullOrEmpty(solutionFullName) ? imageFilesList[0] : solutionFullName;
            Cache cache = options.EnableCaching ? new Cache(cacheRoot, type) : null;

            // Calculate parallelism based on options
            var maxDegreeOfParallelism = Math.Min(
                options.EffectiveMaxParallelThreads,
                Math.Max(1, imageCount / Constants.MinParallelismDivider));

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                TaskScheduler = TaskScheduler.Default,
                CancellationToken = cancellationToken
            };

            var compressionResults = new ConcurrentBag<CompressionResult>();
            _processedCount = 0;

            // Initialize output pane (reuse existing static instance) and activate it
            _outputWindowPane ??= await VS.Windows.CreateOutputWindowPaneAsync(Vsix.Name);
            await _outputWindowPane.ActivateAsync();
            var showDetails = options.ShowDetailedResults;
            var headerWritten = false;

            if (options.ShowProgressInStatusBar)
            {
                await VS.StatusBar.StartAnimationAsync(StatusAnimation.General);
                await VS.StatusBar.ShowMessageAsync(string.Format(Constants.OptimizingMessageFormat, 1, imageCount));
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
                            // Check if the file is already optimized (if caching is enabled)
                            CompressionResult compressionResult = cache?.IsFullyOptimized(filePath) == true
                                ? CompressionResult.Zero(filePath)
                                : compressor.CompressFile(filePath, type);

                            ProcessCompressionResult(compressionResult, cache, options.CreateBackup);
                            compressionResults.Add(compressionResult);

                            // Write result to output window in real-time
                            if (showDetails && compressionResult.Saving > 0)
                            {
                                // Write header on first result (thread-safe)
                                if (!headerWritten)
                                {
                                    lock (_saveLock)
                                    {
                                        if (!headerWritten)
                                        {
                                            _outputWindowPane.WriteLineAsync(GetTableHeader()).FireAndForget();
                                            headerWritten = true;
                                        }
                                    }
                                }
                                _outputWindowPane.WriteLineAsync(FormatResultRow(compressionResult)).FireAndForget();
                            }

                            // Update progress after completion
                            if (options.ShowProgressInStatusBar)
                            {
                                var processed = Interlocked.Increment(ref _processedCount);
                                // Show next item being processed (processed + 1), capped at total
                                var currentItem = Math.Min(processed + 1, imageCount);
                                VS.StatusBar.ShowMessageAsync(string.Format(Constants.OptimizingMessageFormat, currentItem, imageCount)).FireAndForget();
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

                            compressionResults.Add(CompressionResult.Zero(filePath));

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
                await VS.StatusBar.ShowMessageAsync("Image optimization cancelled");
                return;
            }
            finally
            {
                await VS.StatusBar.EndAnimationAsync(StatusAnimation.General);
            }

            if (cache != null)
            {
                await cache.SaveToDiskAsync();
            }

            await DisplayOptimizationSummaryAsync(compressionResults, options, headerWritten);

            _ratingPrompt.RegisterSuccessfulUsage();
        }

        private static void ProcessCompressionResult(CompressionResult compressionResult, Cache cache, bool createBackup)
        {
            if (compressionResult.Saving > 0 &&
                compressionResult.ResultFileSize > 0 &&
                !string.IsNullOrEmpty(compressionResult.ResultFileName) &&
                File.Exists(compressionResult.ResultFileName))
            {
                try
                {
                    // Create backup if enabled
                    if (createBackup)
                    {
                        CreateBackup(compressionResult.OriginalFileName);
                    }

                    // Replace the original file with the optimized file
                    File.Copy(compressionResult.ResultFileName, compressionResult.OriginalFileName, true);
                    File.Delete(compressionResult.ResultFileName);
                }
                catch (Exception ex)
                {
                    ex.LogAsync().FireAndForget();
                }
            }
            else if (compressionResult.OriginalFileName != null)
            {
                // Add the file to the cache if it is already fully optimized
                cache?.AddToCache(compressionResult.OriginalFileName);
            }
        }

        private static void CreateBackup(string originalFilePath)
        {
            try
            {
                var directory = Path.GetDirectoryName(originalFilePath);
                var vsDir = FindVsDirectory(directory);

                if (vsDir != null)
                {
                    var backupDir = Path.Combine(vsDir, Vsix.Name, "backups");
                    if (!Directory.Exists(backupDir))
                    {
                        Directory.CreateDirectory(backupDir);
                    }

                    var backupFileName = $"{Path.GetFileNameWithoutExtension(originalFilePath)}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(originalFilePath)}";
                    var backupPath = Path.Combine(backupDir, backupFileName);
                    File.Copy(originalFilePath, backupPath, false);
                }
            }
            catch (Exception ex)
            {
                ex.LogAsync().FireAndForget();
            }
        }

        private static string FindVsDirectory(string startDirectory)
        {
            var directory = new DirectoryInfo(startDirectory);
            while (directory != null)
            {
                var vsPath = Path.Combine(directory.FullName, Constants.VsDirectoryName);
                if (Directory.Exists(vsPath))
                {
                    return vsPath;
                }
                directory = directory.Parent;
            }
            return null;
        }

        private async Task DisplayOptimizationSummaryAsync(IEnumerable<CompressionResult> compressionResults, General options, bool headerWritten)
        {
            var validResults = compressionResults.Where(r => r?.OriginalFileName != null).ToList();
            if (validResults.Count == 0)
            {
                return;
            }

            var totalSavings = validResults.Sum(r => r.Saving);
            var totalOriginalSize = validResults.Sum(r => r.OriginalFileSize);
            var totalResultSize = validResults.Sum(r => r.ResultFileSize);
            var successfulOptimizations = validResults.Count(r => r.Saving > 0);

            if (totalSavings > 0)
            {
                // Write separator line if we wrote details
                if (options.ShowDetailedResults && headerWritten)
                {
                    await _outputWindowPane.WriteLineAsync(GetTableSeparator());
                }

                if (successfulOptimizations > 0)
                {
                    var totalPercentageReduction = totalOriginalSize > 0
                        ? Math.Round(100 - (totalResultSize / (double)totalOriginalSize * 100), 1, MidpointRounding.AwayFromZero)
                        : 0;

                    var imageLabel = successfulOptimizations == 1 ? "image" : "images";
                    var message = string.Format(Constants.OptimizationCompleteFormat,
                        successfulOptimizations, imageLabel,
                        CompressionResult.ToFileSize(totalSavings), totalPercentageReduction);

                    await VS.StatusBar.ShowMessageAsync(message);
                    await _outputWindowPane.WriteLineAsync(message + Environment.NewLine);

                    // Update statistics
                    await options.UpdateStatisticsAsync(totalSavings, successfulOptimizations);
                }
                else
                {
                    await ShowAlreadyOptimizedMessageAsync();
                }
            }
            else
            {
                await ShowAlreadyOptimizedMessageAsync();
            }

            await _outputWindowPane.ActivateAsync();
        }


        private async Task ShowAlreadyOptimizedMessageAsync()
        {
            await VS.StatusBar.ShowMessageAsync(Constants.AlreadyOptimizedMessage);
            await _outputWindowPane.WriteLineAsync(Constants.AlreadyOptimizedMessage);
        }

        /// <summary>
        /// Gets the table header line with fixed column widths.
        /// </summary>
        private static string GetTableHeader()
        {
            var header = $"{"File",-_fileNameWidth}  {"Before",_sizeWidth}  {"After",_sizeWidth}  {"Saved",_sizeWidth}  {"%",_percentWidth}";
            var separator = new string('-', _fileNameWidth + _sizeWidth * 3 + _percentWidth + 8);
            return header + Environment.NewLine + separator;
        }

        /// <summary>
        /// Gets the table separator line.
        /// </summary>
        private static string GetTableSeparator()
        {
            return new string('-', _fileNameWidth + _sizeWidth * 3 + _percentWidth + 8);
        }

        /// <summary>
        /// Formats a single compression result as an aligned table row.
        /// </summary>
        private static string FormatResultRow(CompressionResult result)
        {
            var fileName = Path.GetFileName(result.OriginalFileName);

            // Truncate long filenames with ellipsis
            if (fileName.Length > _fileNameWidth)
            {
                fileName = fileName.Substring(0, _fileNameWidth - 3) + "...";
            }

            var before = CompressionResult.ToFileSize(result.OriginalFileSize);
            var after = CompressionResult.ToFileSize(result.ResultFileSize);
            var saved = CompressionResult.ToFileSize(result.Saving);
            var percent = result.Percent.ToString("F1") + "%";

            return $"{fileName,-_fileNameWidth}  {before,_sizeWidth}  {after,_sizeWidth}  {saved,_sizeWidth}  {percent,_percentWidth}";
        }
    }
}
