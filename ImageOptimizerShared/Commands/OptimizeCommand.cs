using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace MadsKristensen.ImageOptimizer
{
    internal class OptimizeCommand
    {
        private DTE2 _dte;
        private bool _isProcessing;
        private readonly Dictionary<string, DateTime> _fullyOptimized = new Dictionary<string, DateTime>();
        private IMenuCommandService _commandService;

        private OptimizeCommand(DTE2 dte, IMenuCommandService commandService)
        {
            _dte = dte;
            _commandService = commandService;

            AddCommand(PackageIds.cmdOptimizelossless, (s, e) => OptimizeImageAsync(false, e).ConfigureAwait(false), (s, e) => { OptimizeBeforeQueryStatus(s, false); });
            AddCommand(PackageIds.cmdOptimizelossy, (s, e) => OptimizeImageAsync(true, e).ConfigureAwait(false), (s, e) => { OptimizeBeforeQueryStatus(s, true); });
        }

        public static async Task InitializeAsync(IAsyncServiceProvider package)
        {
            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            var dte = await package.GetServiceAsync(typeof(DTE)) as DTE2;

            new OptimizeCommand(dte, commandService);
        }

        private void AddCommand(int commandId, EventHandler invokeHandler, EventHandler beforeQueryStatus)
        {
            var cmdId = new CommandID(PackageGuids.guidImageOptimizerCmdSet, commandId);
            var menuCmd = new OleMenuCommand(invokeHandler, cmdId);
            menuCmd.BeforeQueryStatus += beforeQueryStatus;
            menuCmd.ParametersDescription = "*";
            _commandService.AddCommand(menuCmd);
        }

        void OptimizeBeforeQueryStatus(object sender, bool lossy)
        {
            var button = (OleMenuCommand)sender;
            IEnumerable<string> paths = ProjectHelpers.GetSelectedItemPaths(_dte);

            button.Visible = paths.Any();
            button.Enabled = true;

            if (button.Visible && _isProcessing)
            {
                button.Enabled = false;
            }
        }

        private async Task OptimizeImageAsync(bool lossy, EventArgs e)
        {
            _isProcessing = true;

            IEnumerable<string> files = null;

            // Check command parameters first
            if (e is OleMenuCmdEventArgs cmdArgs && cmdArgs.InValue is string arg)
            {
                string filePath = arg.Trim('"', '\'');

                if (Compressor.IsFileSupported(filePath) && File.Exists(filePath))
                {
                    files = new[] { filePath };
                }
            }

            // Then check selected items
            if (files == null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                files = ProjectHelpers.GetSelectedFilePaths(_dte).Where(f => Compressor.IsFileSupported(f)).ToArray();
            }

            if (!files.Any())
            {
                _isProcessing = false;

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _dte.StatusBar.Text = "No images found to optimize";
                return;
            }

            var list = new CompressionResult[files.Count()];
            var stopwatch = Stopwatch.StartNew();
            int count = files.Count();

            await Task.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                try
                {
                    string text = count == 1 ? " image" : " images";
                    _dte.StatusBar.Progress(true, "Optimizing " + count + text + "...", AmountCompleted: 1, Total: count + 1);

                    var compressor = new Compressor();
                    var cache = new Cache(_dte.Solution.FullName, lossy);
                    int nCompleted = 0;
                    var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

                    Parallel.For(0, count, options, i =>
                    {
                        string file = files.ElementAt(i);

                        // Don't process if file has been fully optimized already
                        if (cache.IsFullyOptimized(file))
                        {
                            var bogus = new CompressionResult(file, file, TimeSpan.Zero) { Processed = false };
                            HandleResult(bogus, nCompleted + 1);
                        }
                        else
                        {
                            CompressionResult result = compressor.CompressFile(file, lossy);
                            HandleResult(result, nCompleted + 1);

                            if (result.Saving > 0 && result.ResultFileSize > 0 && !string.IsNullOrEmpty(result.ResultFileName))
                                list[i] = result;
                            else
                                cache.AddToCache(file);
                        }

                        Interlocked.Increment(ref nCompleted);
                    });
                }
                finally
                {
                    _dte.StatusBar.Progress(false);
                    stopwatch.Stop();
                    await DisplayEndResultAsync(list, stopwatch.Elapsed);
                    _isProcessing = false;
                }
            });
        }

        void HandleResult(CompressionResult result, int count)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string name = Path.GetFileName(result.OriginalFileName);

            if (result.Saving > 0 && result.ResultFileSize > 0 && File.Exists(result.ResultFileName))
            {
                if (_dte.SourceControl.IsItemUnderSCC(result.OriginalFileName) && !_dte.SourceControl.IsItemCheckedOut(result.OriginalFileName))
                    _dte.SourceControl.CheckOutItem(result.OriginalFileName);

                File.Copy(result.ResultFileName, result.OriginalFileName, true);
                File.Delete(result.ResultFileName);

                string text = "Compressed " + name + " by " + result.Saving + " bytes / " + result.Percent + "%";
                _dte.StatusBar.Progress(true, text, count, count + 1);
            }
            else
            {
                _dte.StatusBar.Progress(true, name + " is already optimized", AmountCompleted: count, Total: count + 1);
            }
        }

        private async Task DisplayEndResultAsync(IList<CompressionResult> list, TimeSpan elapsed)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            long savings = list.Where(r => r != null).Sum(r => r.Saving);
            long originals = list.Where(r => r != null).Sum(r => r.OriginalFileSize);
            long results = list.Where(r => r != null).Sum(r => r.ResultFileSize);

            if (savings > 0)
            {
                int successfulOptimizations = list.Count(x => x != null);
                double percent = Math.Round(100 - ((double)results / (double)originals * 100), 1, MidpointRounding.AwayFromZero);
                string image = successfulOptimizations == 1 ? "image" : "images";
                string msg = successfulOptimizations + " " + image + " optimized in " + Math.Round(elapsed.TotalMilliseconds / 1000, 2) + " seconds. Total saving of " + savings + " bytes / " + percent + "%";

                _dte.StatusBar.Text = msg;
                await Logger.LogToOutputWindowAsync(msg + Environment.NewLine);

                IEnumerable<CompressionResult> filesOptimized = list.Where(r => r != null && r.Saving > 0);
                int maxLength = filesOptimized.Max(r => Path.GetFileName(r.OriginalFileName).Length);

                foreach (CompressionResult result in filesOptimized)
                {
                    string name = Path.GetFileName(result.OriginalFileName).PadRight(maxLength);
                    double p = Math.Round(100 - ((double)result.ResultFileSize / (double)result.OriginalFileSize * 100), 1, MidpointRounding.AwayFromZero);
                    await Logger.LogToOutputWindowAsync("  " + name + "\t  optimized by " + result.Saving + " bytes / " + p + "%");
                }
            }
            else
            {
                _dte.StatusBar.Text = "The images were already optimized";
                await Logger.LogToOutputWindowAsync("The images were already optimized");
            }
        }
    }
}
