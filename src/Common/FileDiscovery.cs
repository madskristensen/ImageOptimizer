using System.Collections.Generic;
using System.IO;

namespace MadsKristensen.ImageOptimizer.Common
{
    /// <summary>
    /// Provides file discovery helpers with directory exclusion support.
    /// </summary>
    internal static class FileDiscovery
    {
        /// <summary>
        /// Recursively enumerates files from a root directory while skipping excluded directories.
        /// </summary>
        internal static IEnumerable<string> EnumerateFiles(string rootDirectory, Func<string, bool> filePredicate)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory) || filePredicate is null || !Directory.Exists(rootDirectory))
            {
                yield break;
            }

            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(rootDirectory);

            while (pendingDirectories.Count > 0)
            {
                var currentDirectory = pendingDirectories.Pop();

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(currentDirectory, Constants.AllFilesPattern, SearchOption.TopDirectoryOnly);
                }
                catch (UnauthorizedAccessException ex)
                {
                    ex.LogAsync().FireAndForget();
                    continue;
                }
                catch (IOException ex)
                {
                    ex.LogAsync().FireAndForget();
                    continue;
                }

                foreach (var file in files)
                {
                    if (filePredicate(file))
                    {
                        yield return file;
                    }
                }

                IEnumerable<string> directories;
                try
                {
                    directories = Directory.EnumerateDirectories(currentDirectory, "*", SearchOption.TopDirectoryOnly);
                }
                catch (UnauthorizedAccessException ex)
                {
                    ex.LogAsync().FireAndForget();
                    continue;
                }
                catch (IOException ex)
                {
                    ex.LogAsync().FireAndForget();
                    continue;
                }

                foreach (var directory in directories)
                {
                    var directoryName = Path.GetFileName(directory);
                    if (Constants.ExcludedDirectoryNames.Contains(directoryName))
                    {
                        continue;
                    }

                    pendingDirectories.Push(directory);
                }
            }
        }
    }
}
