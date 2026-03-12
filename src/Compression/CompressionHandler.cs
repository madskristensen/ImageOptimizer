using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TaskStatusCenter;
using MadsKristensen.ImageOptimizer.Resx;

namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Handles batch image optimization operations with caching and progress reporting.
    /// </summary>
    internal class CompressionHandler
    {
        private static readonly RatingPrompt _ratingPrompt = new("MadsKristensen.ImageOptimizer64bit", Vsix.Name, General.Instance);
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
            string selectedFolderPath = null,
            CancellationToken cancellationToken = default)
        {
            var imageFilesList = imageFilePaths.ToList();
            var imageCount = imageFilesList.Count;

            if (imageCount == 0)
            {
                return;
            }

            IVsTaskStatusCenterService taskStatusCenter = await GetTaskStatusCenterServiceAsync(cancellationToken);
            if (taskStatusCenter == null)
            {
                await OptimizeImagesCoreAsync(imageFilesList, type, solutionFullName, selectedFolderPath, cancellationToken);
                return;
            }

            using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var initialProgress = new TaskProgressData
            {
                CanBeCanceled = true,
                PercentComplete = 0,
                ProgressText = string.Format(Constants.TaskStatusCenterOptimizingProgressFormat, 0, imageCount)
            };

            TaskHandlerOptions options = default;
            options.Title = Constants.TaskStatusCenterOptimizingTitle;
            options.ActionsAfterCompletion = CompletionActions.None;

            ITaskHandler taskHandler = taskStatusCenter.PreRegister(options, initialProgress);
            using CancellationTokenRegistration cancellationRegistration = taskHandler.UserCancellation.Register(() => linkedCancellation.Cancel());

            Task optimizationTask = OptimizeImagesCoreAsync(imageFilesList, type, solutionFullName, selectedFolderPath, linkedCancellation.Token, taskHandler.Progress);
            taskHandler.RegisterTask(optimizationTask);
            var taskStatusCenterVisible = await TryToggleTaskStatusCenterAsync(CancellationToken.None);

            try
            {
                await optimizationTask;
            }
            finally
            {
                if (taskStatusCenterVisible)
                {
                    _ = TryToggleTaskStatusCenterAsync(CancellationToken.None);
                }
            }
        }

        private async Task OptimizeImagesCoreAsync(
            List<string> imageFilesList,
            CompressionType type,
            string solutionFullName,
            string selectedFolderPath,
            CancellationToken cancellationToken,
            IProgress<TaskProgressData> taskProgressReporter = null)
        {
            var imageCount = imageFilesList.Count;

            // Load options
            General options = await General.GetLiveInstanceAsync();
            var compressor = new Compressor(options.ProcessTimeoutMs, options.EffectiveLossyQuality);
            var cacheRoot = string.IsNullOrEmpty(solutionFullName) ? imageFilesList[0] : solutionFullName;
            Cache cache = options.EnableCaching ? new Cache(cacheRoot, type, options.ValidateCachedFiles) : null;

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
            var detailRows = new ConcurrentQueue<string>();
            _processedCount = 0;

            // Initialize output pane (reuse existing static instance) and activate it
            _outputWindowPane ??= await VS.Windows.CreateOutputWindowPaneAsync(Vsix.Name);
            await _outputWindowPane.ActivateAsync();
            var showDetails = options.ShowDetailedResults;

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

                            if (showDetails && compressionResult.Saving > 0)
                            {
                                detailRows.Enqueue(FormatResultRow(compressionResult));
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
                        finally
                        {
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                var processed = Interlocked.Increment(ref _processedCount);

                                if (options.ShowProgressInStatusBar)
                                {
                                    if (processed == imageCount || processed % Constants.ProgressUpdateBatchSize == 0)
                                    {
                                        VS.StatusBar.ShowMessageAsync(string.Format(Constants.OptimizingMessageFormat, processed, imageCount)).FireAndForget();
                                    }
                                }

                                if (taskProgressReporter != null)
                                {
                                    var currentFileName = Path.GetFileName(filePath);
                                    var percentComplete = Math.Min(100, Math.Max(0, (int)Math.Round((processed / (double)imageCount) * 100, MidpointRounding.AwayFromZero)));

                                    taskProgressReporter.Report(new TaskProgressData
                                    {
                                        CanBeCanceled = true,
                                        PercentComplete = percentComplete,
                                        ProgressText = string.Format(Constants.TaskStatusCenterOptimizingFileProgressFormat, processed, imageCount, currentFileName)
                                    });
                                }
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

            await DisplayOptimizationSummaryAsync(compressionResults, options, detailRows, selectedFolderPath);

            _ratingPrompt.RegisterSuccessfulUsage();
        }

        private static async Task<IVsTaskStatusCenterService> GetTaskStatusCenterServiceAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            return ServiceProvider.GlobalProvider.GetService(typeof(SVsTaskStatusCenterService)) as IVsTaskStatusCenterService;
        }

        private static async Task<bool> TryToggleTaskStatusCenterAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            try
            {
                await VS.Commands.ExecuteAsync("View.ShowTaskStatusCenter");
                return true;
            }
            catch (Exception)
            {
                // Best effort only; optimization should continue even if the UI command is unavailable.
                return false;
            }
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

        private async Task DisplayOptimizationSummaryAsync(IEnumerable<CompressionResult> compressionResults, General options, IEnumerable<string> detailRows, string selectedFolderPath)
        {
            var validResults = compressionResults.Where(r => r?.OriginalFileName != null).ToList();
            if (validResults.Count == 0)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(selectedFolderPath))
            {
                await _outputWindowPane.WriteLineAsync(string.Format(Constants.SelectedFolderForOptimizationFormat, selectedFolderPath));
                await _outputWindowPane.WriteLineAsync(string.Empty);
            }

            var detailRowsList = detailRows.ToList();

            var totalSavings = validResults.Sum(r => r.Saving);
            var totalOriginalSize = validResults.Sum(r => r.OriginalFileSize);
            var totalResultSize = validResults.Sum(r => r.ResultFileSize);
            var successfulOptimizations = validResults.Count(r => r.Saving > 0);

            if (totalSavings > 0)
            {
                if (options.ShowDetailedResults && detailRowsList.Count > 0)
                {
                    await _outputWindowPane.WriteLineAsync(GetTableHeader());

                    foreach (var row in detailRowsList)
                    {
                        await _outputWindowPane.WriteLineAsync(row);
                    }

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

        /// <summary>
        /// Optimizes embedded images within .resx resource files.
        /// </summary>
        /// <param name="resxFilePaths">Paths to the .resx files to process.</param>
        /// <param name="type">The type of compression to apply.</param>
        /// <param name="solutionFullName">Optional solution path for context.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        public async Task OptimizeResxImagesAsync(
            IEnumerable<string> resxFilePaths,
            CompressionType type,
            string solutionFullName = null,
            CancellationToken cancellationToken = default)
        {
            var resxList = resxFilePaths.ToList();
            if (resxList.Count == 0)
            {
                return;
            }

            General options = await General.GetLiveInstanceAsync();
            var compressor = new Compressor(options.ProcessTimeoutMs, options.EffectiveLossyQuality);
            var extractor = new ResxImageExtractor();

            _outputWindowPane ??= await VS.Windows.CreateOutputWindowPaneAsync(Vsix.Name);
            await _outputWindowPane.ActivateAsync();

            if (options.ShowProgressInStatusBar)
            {
                await VS.StatusBar.StartAnimationAsync(StatusAnimation.General);
            }

            var allResults = new List<ResxCompressionResult>();
            var resxFileCount = 0;

            try
            {
                foreach (var resxPath in resxList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (options.ShowProgressInStatusBar)
                    {
                        await VS.StatusBar.ShowMessageAsync(
                            string.Format(Constants.ResxOptimizingMessageFormat, Path.GetFileName(resxPath)));
                    }

                    try
                    {
                        IReadOnlyList<ResxCompressionResult> results =
                            extractor.OptimizeResxImages(resxPath, compressor, type);

                        if (results.Count > 0)
                        {
                            resxFileCount++;
                            allResults.AddRange(results);

                            foreach (ResxCompressionResult result in results.Where(r => r.Saving > 0))
                            {
                                await _outputWindowPane.WriteLineAsync(FormatResxResultRow(result));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ex.LogAsync().FireAndForget();
                        if (!options.ContinueOnError)
                        {
                            throw;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                await VS.StatusBar.ShowMessageAsync("Resx image optimization cancelled");
                return;
            }
            finally
            {
                await VS.StatusBar.EndAnimationAsync(StatusAnimation.General);
            }

            await DisplayResxSummaryAsync(allResults, resxFileCount);
        }

        private async Task DisplayResxSummaryAsync(List<ResxCompressionResult> results, int resxFileCount)
        {
            var optimized = results.Where(r => r.Saving > 0).ToList();

            if (optimized.Count == 0)
            {
                await VS.StatusBar.ShowMessageAsync(Constants.NoResxImagesFoundMessage);
                await _outputWindowPane.WriteLineAsync(Constants.NoResxImagesFoundMessage);
                return;
            }

            var totalSavings = optimized.Sum(r => r.Saving);
            var totalOriginal = optimized.Sum(r => r.OriginalSize);
            var totalPercent = totalOriginal > 0
                ? Math.Round((1.0 - (double)(totalOriginal - totalSavings) / totalOriginal) * 100, 1, MidpointRounding.AwayFromZero)
                : 0;

            var imageLabel = optimized.Count == 1 ? "image" : "images";
            var fileLabel = resxFileCount == 1 ? "file" : "files";
            var message = string.Format(Constants.ResxOptimizationCompleteFormat,
                optimized.Count, imageLabel, resxFileCount, fileLabel,
                CompressionResult.ToFileSize(totalSavings), totalPercent);

            await VS.StatusBar.ShowMessageAsync(message);
            await _outputWindowPane.WriteLineAsync(message + Environment.NewLine);
            await _outputWindowPane.ActivateAsync();
        }

        /// <summary>
        /// Formats a single .resx compression result for the output window.
        /// </summary>
        private static string FormatResxResultRow(ResxCompressionResult result)
        {
            var resxName = Path.GetFileName(result.ResxFilePath);
            var label = $"{resxName}/{result.ResourceName}";

            if (label.Length > _fileNameWidth)
            {
                label = label.Substring(0, _fileNameWidth - 3) + "...";
            }

            var before = CompressionResult.ToFileSize(result.OriginalSize);
            var after = CompressionResult.ToFileSize(result.OptimizedSize);
            var saved = CompressionResult.ToFileSize(result.Saving);
            var percent = result.PercentSaved.ToString("F1") + "%";

            return $"{label,-_fileNameWidth}  {before,_sizeWidth}  {after,_sizeWidth}  {saved,_sizeWidth}  {percent,_percentWidth}";
        }
    }
}
