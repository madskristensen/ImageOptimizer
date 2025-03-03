using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MadsKristensen.ImageOptimizer
{
    internal class CompressionHandler
    {
        private static readonly RatingPrompt _ratingPrompt = new("MadsKristensen.ImageOptimizer64bit", Vsix.Name, General.Instance);
        private static OutputWindowPane _outputWindowPane;

        public async Task OptimizeImagesAsync(IEnumerable<string> imageFilePaths, CompressionType type, string solutionFullName = null)
        {
            var compressor = new Compressor();
            var imageCount = imageFilePaths.Count();
            var cacheRoot = string.IsNullOrEmpty(solutionFullName) ? imageFilePaths.First() : solutionFullName;
            var cache = new Cache(cacheRoot, type);

            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, TaskScheduler = TaskScheduler.Default };
            var compressionResults = new ConcurrentBag<CompressionResult>();

            await VS.StatusBar.StartAnimationAsync(StatusAnimation.General);
            await VS.StatusBar.ShowMessageAsync("Optimizing selected images...");

            await Task.Run(() =>
            {
                _ = Parallel.For(0, imageCount, parallelOptions, (i, state) =>
                {
                    try
                    {
                        var filePath = imageFilePaths.ElementAt(i);
                        var fileName = Path.GetFileName(filePath);

                        // Check if the file is already optimized, otherwise compress it
                        CompressionResult compressionResult = cache.IsFullyOptimized(filePath) ? CompressionResult.Zero(filePath) : compressor.CompressFile(filePath, type);

                        if (compressionResult.Saving > 0 && compressionResult.ResultFileSize > 0 && File.Exists(compressionResult.ResultFileName))
                        {
                            // Replace the original file with the optimized file
                            File.Copy(compressionResult.ResultFileName, compressionResult.OriginalFileName, true);
                            File.Delete(compressionResult.ResultFileName);

                            // Calculate the percentage of size reduction
                            var maxLength = imageFilePaths.Max(r => Path.GetFileName(compressionResult.OriginalFileName).Length);
                            var percentageReduction = Math.Round(100 - (compressionResult.ResultFileSize / (double)compressionResult.OriginalFileSize * 100), 1, MidpointRounding.AwayFromZero);
                        }
                        else
                        {
                            // Add the file to the cache if it is already fully optimized
                            cache.AddToCache(filePath);
                        }

                        compressionResults.Add(compressionResult);
                    }
                    catch (Exception ex)
                    {
                        ex.LogAsync().FireAndForget();
                    }
                });
            });

            await cache.SaveToDiskAsync();
            await VS.StatusBar.EndAnimationAsync(StatusAnimation.General);
            DisplayOptimizationResultsAsync(compressionResults).FireAndForget();

            _ratingPrompt.RegisterSuccessfulUsage();
        }

        private async Task DisplayOptimizationResultsAsync(IEnumerable<CompressionResult> compressionResults)
        {
            var validResults = compressionResults.Where(r => r != null).ToList();
            var totalSavings = validResults.Sum(r => r.Saving);
            var totalOriginalSize = validResults.Sum(r => r.OriginalFileSize);
            var totalResultSize = validResults.Sum(r => r.ResultFileSize);

            _outputWindowPane ??= await VS.Windows.CreateOutputWindowPaneAsync(Vsix.Name);

            if (totalSavings > 0)
            {
                var maxLength = validResults.Max(r => Path.GetFileName(r.OriginalFileName).Length);
                var stringBuilder = new StringBuilder(validResults.Count * 100); // Estimate capacity

                foreach (CompressionResult result in validResults)
                {
                    _ = stringBuilder.AppendLine(result.ToString());
                }

                var successfulOptimizations = validResults.Count;
                var totalPercentageReduction = Math.Round(100 - (totalResultSize / (double)totalOriginalSize * 100), 1, MidpointRounding.AwayFromZero);
                var imageLabel = successfulOptimizations == 1 ? "image" : "images";
                var message = $"{successfulOptimizations} {imageLabel} optimized. Total saving of {CompressionResult.ToFileSize(totalSavings)} / {totalPercentageReduction}%";

                await VS.StatusBar.ShowMessageAsync(message);
                await _outputWindowPane.WriteLineAsync(stringBuilder.ToString() + message + Environment.NewLine);
            }
            else
            {
                const string alreadyOptimizedMessage = "The images were already optimized";
                await VS.StatusBar.ShowMessageAsync(alreadyOptimizedMessage);
                await _outputWindowPane.WriteLineAsync(alreadyOptimizedMessage);
            }

            await _outputWindowPane.ActivateAsync();
        }
    }
}
