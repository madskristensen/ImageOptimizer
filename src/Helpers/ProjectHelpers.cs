using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;

namespace MadsKristensen.ImageOptimizer
{
    public static class ProjectHelpers
    {
        private static DTE2 _dte = ImageOptimizerPackage.Instance._dte;

        public static IEnumerable<string> GetSelectedFilePaths()
        {
            return GetSelectedItemPaths()
                .SelectMany(p => Directory.Exists(p)
                                 ? Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories)
                                 : new[] { p }
                           );
        }

        public static IEnumerable<string> GetSelectedItemPaths(DTE2 dte = null)
        {
            var items = (Array)_dte.ToolWindows.SolutionExplorer.SelectedItems;

            foreach (UIHierarchyItem selItem in items)
            {
                var item = selItem.Object as ProjectItem;
                var proj = selItem.Object as Project;
                var sol = selItem.Object as Solution;

                if (item != null && item.Properties != null)
                {
                    yield return item.Properties.Item("FullPath").Value.ToString();
                }
                else if (proj != null && proj.Kind != ProjectKinds.vsProjectKindSolutionFolder)
                {
                    yield return proj.GetRootFolder();
                }
                else if (sol != null && !string.IsNullOrEmpty(sol.FullName))
                {
                    yield return Path.GetDirectoryName(sol.FullName);
                }
            }
        }

        public static string GetRootFolder(this Project project)
        {
            if (string.IsNullOrEmpty(project.FullName))
                return null;

            string fullPath;

            try
            {
                fullPath = project.Properties.Item("FullPath").Value as string;
            }
            catch (ArgumentException)
            {
                try
                {
                    // MFC projects don't have FullPath, and there seems to be no way to query existence
                    fullPath = project.Properties.Item("ProjectDirectory").Value as string;
                }
                catch (ArgumentException)
                {
                    // Installer projects have a ProjectPath.
                    fullPath = project.Properties.Item("ProjectPath").Value as string;
                }
            }

            if (string.IsNullOrEmpty(fullPath))
                return File.Exists(project.FullName) ? Path.GetDirectoryName(project.FullName) : null;

            if (Directory.Exists(fullPath))
                return fullPath;

            if (File.Exists(fullPath))
                return Path.GetDirectoryName(fullPath);

            return null;
        }

        public static Task WaitForExitAsync(this System.Diagnostics.Process process, CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<object>();
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.TrySetResult(null);

            if (cancellationToken != default(CancellationToken))
                cancellationToken.Register(tcs.SetCanceled);

            return tcs.Task;
        }
    }
}
