using EnvDTE;

using EnvDTE80;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Workspace.VSIntegration.UI;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MadsKristensen.ImageOptimizer.Commands
{
    [Export(typeof(INodeExtender))]
    public class WorkspaceOptimizeCommandProvider : INodeExtender
    {
        private readonly IWorkspaceCommandHandler _handler = new WorkspaceOptimizeCommand();
        public IChildrenSource ProvideChildren(WorkspaceVisualNodeBase parentNode)
        {
            return null;
        }

        public IWorkspaceCommandHandler ProvideCommandHandler(WorkspaceVisualNodeBase parentNode)
        {
            return parentNode is IFileNode || parentNode is IFolderNode ? _handler : null;
        }
    }
    public class WorkspaceOptimizeCommand : IWorkspaceCommandHandler
    {
        private static readonly string[] _sizeSuffixes = new[] { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        public bool IgnoreOnMultiselect => true;

        public int Priority => 100;

        private static DTE2 _dte;

        public int Exec(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            bool isLossy = nCmdID == PackageIds.cmdWorkspaceOptimizelossy;
            _dte = _dte ?? ServiceProvider.GlobalProvider.GetService<DTE, DTE2>();

            if (IsSupportedCommand(pguidCmdGroup, nCmdID))
            {
                var files = GetImageFiles(selection[0]);
                int total = files.Count();

                if (files.Any())
                {
                    _dte.StatusBar.Animate(true, vsStatusAnimation.vsStatusAnimationGeneral);
                    _dte.StatusBar.Text = $"Optimizing {total} images...";
                    Compressor compressor = new Compressor();
                    long saving = 0;

                    _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        await TaskScheduler.Default; // move to a background thread

                        var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount  };

                        Parallel.For(0, total, options, i =>
                        {
                            var file = files.ElementAt(i);
                            try
                            {
                                var result = compressor.CompressFile(file, isLossy);

                                File.Copy(result.ResultFileName, result.OriginalFileName, true);
                                File.Delete(result.ResultFileName);

                                saving += result.Saving;
                            }
                            catch
                            {
                                // TODO: Don't ignore
                            }
                        });
                    }).Task.ContinueWith(async t =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                        _dte.StatusBar.Animate(false, vsStatusAnimation.vsStatusAnimationGeneral);
                        _dte.StatusBar.Text = $"Optimized {files.Count()} images. Saved {ToFileSize(saving)}";

                    }, TaskScheduler.Current);

                    return VSConstants.S_OK;
                }
                else
                {
                    _dte.StatusBar.Text = "No images selected";
                }
            }

            return (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        private static IEnumerable<string> GetImageFiles(WorkspaceVisualNodeBase selection)
        {
            if (selection is IFolderNode folder)
            {
                return Directory.EnumerateFiles(folder.FullPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => Compressor.IsFileSupported(f));
            }
            else if (selection is IFileNode file && Compressor.IsFileSupported(file.FullPath))
            {
                return new[] { file.FullPath };
            }

            return Enumerable.Empty<string>();
        }

        public static string ToFileSize(long value, int decimalPlaces = 1)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (value < 0) { return "-" + ToFileSize(-value, decimalPlaces); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return value < 1024
                ? string.Format("{0:n0} {1}", adjustedSize, _sizeSuffixes[mag])
                : string.Format("{0:n" + decimalPlaces + "} {1}", adjustedSize, _sizeSuffixes[mag]);
        }

        public bool QueryStatus(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, ref uint cmdf, ref string customTitle)
        {
            if (selection.Count != 1)
            {
                return false;
            }

            if (IsSupportedCommand(pguidCmdGroup, nCmdID))
            {
                if ((selection[0] is IFileNode file && Compressor.IsFileSupported(file.FullPath)) || selection[0] is IFolderNode)
                {
                    cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
                    return true;
                }
                else
                {
                    cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE);
                }
            }

            return false;
        }

        private static bool IsSupportedCommand(Guid pguidCmdGroup, uint nCmdID)
        {
            return pguidCmdGroup == PackageGuids.guidImageOptimizerCmdSet && (nCmdID == PackageIds.cmdWorkspaceOptimizelossless || nCmdID == PackageIds.cmdWorkspaceOptimizelossy);
        }
    }
}