using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace MadsKristensen.ImageOptimizer
{
    public static class ProjectHelpers
    {
        public static IEnumerable<string> GetSelectedFilePaths(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return GetSelectedItemPaths(dte)
                .SelectMany(p => Directory.Exists(p)
                                 ? Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories)
                                 : new[] { p }
                           );
        }

        public static IEnumerable<string> GetSelectedItemPaths(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var items = (Array)dte.ToolWindows.SolutionExplorer.SelectedItems;

            foreach (UIHierarchyItem selItem in items)
            {
                var proj = selItem.Object as Project;
                var sol = selItem.Object as Solution;

                if (selItem.Object is ProjectItem item && item.Properties != null)
                {
                    yield return item.Properties.Item("FullPath").Value.ToString();
                }
                else if (proj != null && proj.Kind != "{66A26720-8FB5-11D2-AA7E-00C04F688DDE}")//ProjectKinds.vsProjectKindSolutionFolder)
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
            ThreadHelper.ThrowIfNotOnUIThread();

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
    }
}
