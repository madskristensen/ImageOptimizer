using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TaskStatusCenter;
using MadsKristensen.ImageOptimizer.Common;
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
        private const int _statusWidth = 10;

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
            IReadOnlyList<string> imageFilesList = FileUtilities.GetDistinctPaths(imageFilePaths);
            if (imageFilesList.Count == 0)
            {
                return;
            }

            if (!ImageOperationCoordinator.TryStart(out IDisposable operationLease))
            {
                await VS.StatusBar.ShowMessageAsync(Constants.OptimizationAlreadyRunningMessage);
                return;
            }

            using (operationLease)
            {
                await OptimizeImagesExclusiveAsync(imageFilesList, type, solutionFullName, selectedFolderPath, cancellationToken);
            }
        }

        private async Task OptimizeImagesExclusiveAsync(
            IReadOnlyList<string> imageFilesList,
            CompressionType type,
            string solutionFullName,
            string selectedFolderPath,
            CancellationToken cancellationToken)
        {
            var imageCount = imageFilesList.Count;
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
            IReadOnlyList<string> imageFilesList,
            CompressionType type,
            string solutionFullName,
            string selectedFolderPath,
            CancellationToken cancellationToken,
            IProgress<TaskProgressData> taskProgressReporter = null)
        {
            var imageCount = imageFilesList.Count;
            var stopwatch = Stopwatch.StartNew();

            // Load options
            General options = await General.GetLiveInstanceAsync();
            var compressor = new Compressor(options.ProcessTimeoutMs, options.EffectiveLossyQuality);
            var cacheRoot = string.IsNullOrEmpty(solutionFullName) ? imageFilesList[0] : solutionFullName;
            Cache cache = options.EnableCaching ? new Cache(cacheRoot, type, options.ValidateCachedFiles) : null;

            // Each file is compressed by an external, mostly I/O-bound process, so use the
            // full configured thread budget (capped by the number of images to process).
            var maxDegreeOfParallelism = Math.Max(1, Math.Min(options.EffectiveMaxParallelThreads, imageCount));

            CompressionResult[] compressionResults = null;
            _processedCount = 0;

            // Initialize output pane (reuse existing static instance) and activate it
            _outputWindowPane ??= await VS.Windows.CreateOutputWindowPaneAsync(Vsix.Name);
            await _outputWindowPane.ActivateAsync();
            if (options.ShowProgressInStatusBar)
            {
                await VS.StatusBar.StartAnimationAsync(StatusAnimation.General);
                await VS.StatusBar.ShowMessageAsync(string.Format(Constants.OptimizingMessageFormat, 1, imageCount));
            }

            try
            {
                compressionResults = await Task.Run(() =>
                    OrderedParallelProcessor.Process(
                        imageFilesList,
                        maxDegreeOfParallelism,
                        cancellationToken,
                        (filePath, token) =>
                    {
                        try
                        {
                            CompressionResult compressionResult = cache?.IsFullyOptimized(filePath) == true
                                ? CompressionResult.Cached(filePath)
                                : compressor.CompressFile(filePath, type, token);

                            return CompressionResultProcessor.Process(compressionResult, cache, options.CreateBackup);
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

                            return CompressionResult.Failed(filePath, ex.Message, TimeSpan.Zero);
                        }
                        finally
                        {
                            if (!token.IsCancellationRequested)
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
                    },
                        filePath => CompressionResult.Cancelled(filePath, TimeSpan.Zero)));
            }
            finally
            {
                stopwatch.Stop();
                await VS.StatusBar.EndAnimationAsync(StatusAnimation.General);
            }

            if (cache != null)
            {
                await cache.SaveToDiskAsync();
            }

            await DisplayOptimizationSummaryAsync(compressionResults, options, selectedFolderPath, stopwatch.Elapsed);

            if (compressionResults.Any(result => result?.Outcome == CompressionOutcome.Optimized))
            {
                _ratingPrompt.RegisterSuccessfulUsage();
            }
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

        private async Task DisplayOptimizationSummaryAsync(IEnumerable<CompressionResult> compressionResults, General options, string selectedFolderPath, TimeSpan elapsed)
        {
            CompressionSummary summary = CompressionSummary.Create(compressionResults, elapsed);
            if (summary.Results.Count == 0)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(selectedFolderPath))
            {
                await _outputWindowPane.WriteLineAsync(string.Format(Constants.SelectedFolderForOptimizationFormat, selectedFolderPath));
                await _outputWindowPane.WriteLineAsync(string.Empty);
            }

            if (options.ShowDetailedResults)
            {
                await _outputWindowPane.WriteLineAsync(GetTableHeader());

                foreach (CompressionResult result in summary.Results)
                {
                    await _outputWindowPane.WriteLineAsync(FormatResultRow(result));

                    if (options.LogErrorsToOutput && !string.IsNullOrWhiteSpace(result.ErrorMessage))
                    {
                        await _outputWindowPane.WriteLineAsync($"  {result.ErrorMessage}");
                    }
                }

                await _outputWindowPane.WriteLineAsync(GetTableSeparator());
            }

            string message = summary.ToDisplayString();
            await VS.StatusBar.ShowMessageAsync(message);
            await _outputWindowPane.WriteLineAsync(message + Environment.NewLine);

            if (summary.Optimized > 0)
            {
                await options.UpdateStatisticsAsync(summary.TotalSavings, summary.Optimized);
            }

            await _outputWindowPane.ActivateAsync();
        }

        /// <summary>
        /// Gets the table header line with fixed column widths.
        /// </summary>
        private static string GetTableHeader()
        {
            var header = $"{"File",-_fileNameWidth}  {"Status",-_statusWidth}  {"Before",_sizeWidth}  {"After",_sizeWidth}  {"Saved",_sizeWidth}  {"%",_percentWidth}";
            var separator = new string('-', _fileNameWidth + _statusWidth + _sizeWidth * 3 + _percentWidth + 10);
            return header + Environment.NewLine + separator;
        }

        /// <summary>
        /// Gets the table separator line.
        /// </summary>
        private static string GetTableSeparator()
        {
            return new string('-', _fileNameWidth + _statusWidth + _sizeWidth * 3 + _percentWidth + 10);
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

            return $"{fileName,-_fileNameWidth}  {result.Outcome,-_statusWidth}  {before,_sizeWidth}  {after,_sizeWidth}  {saved,_sizeWidth}  {percent,_percentWidth}";
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
            IReadOnlyList<string> resxList = FileUtilities.GetDistinctPaths(resxFilePaths);
            if (resxList.Count == 0)
            {
                return;
            }

            if (!ImageOperationCoordinator.TryStart(out IDisposable operationLease))
            {
                await VS.StatusBar.ShowMessageAsync(Constants.OptimizationAlreadyRunningMessage);
                return;
            }

            using (operationLease)
            {
                await OptimizeResxImagesCoreAsync(resxList, type, cancellationToken);
            }
        }

        private async Task OptimizeResxImagesCoreAsync(
            IReadOnlyList<string> resxList,
            CompressionType type,
            CancellationToken cancellationToken)
        {
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
                            extractor.OptimizeResxImages(resxPath, compressor, type, cancellationToken);

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
                    catch (OperationCanceledException)
                    {
                        throw;
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
