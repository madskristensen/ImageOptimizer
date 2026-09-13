using System.IO;
using MadsKristensen.ImageOptimizer.Common;

namespace MadsKristensen.ImageOptimizer
{
    internal static class CompressionResultProcessor
    {
        internal static CompressionResult Process(CompressionResult compressionResult, Cache cache, bool createBackup)
        {
            if (compressionResult.Outcome == CompressionOutcome.Optimized &&
                compressionResult.ResultFileSize > 0 &&
                !string.IsNullOrEmpty(compressionResult.ResultFileName) &&
                File.Exists(compressionResult.ResultFileName))
            {
                try
                {
                    if (createBackup)
                    {
                        CreateBackup(compressionResult.OriginalFileName);
                    }

                    File.Copy(compressionResult.ResultFileName, compressionResult.OriginalFileName, true);
                    File.Delete(compressionResult.ResultFileName);
                    cache?.AddToCache(compressionResult.OriginalFileName);
                    return compressionResult;
                }
                catch (Exception ex)
                {
                    ex.LogAsync().FireAndForget();
                    FileUtilities.SafeDeleteFile(compressionResult.ResultFileName);
                    return CompressionResult.Failed(compressionResult.OriginalFileName, ex.Message, compressionResult.Elapsed);
                }
            }

            if (compressionResult.Outcome == CompressionOutcome.Unchanged)
            {
                if (!string.Equals(compressionResult.ResultFileName, compressionResult.OriginalFileName, StringComparison.OrdinalIgnoreCase))
                {
                    FileUtilities.SafeDeleteFile(compressionResult.ResultFileName);
                }

                cache?.AddToCache(compressionResult.OriginalFileName);
            }
            else if (compressionResult.Outcome == CompressionOutcome.Optimized)
            {
                FileUtilities.SafeDeleteFile(compressionResult.ResultFileName);
                return CompressionResult.Failed(
                    compressionResult.OriginalFileName,
                    "The optimizer did not produce a usable output file.",
                    compressionResult.Elapsed);
            }

            return compressionResult;
        }

        private static void CreateBackup(string originalFilePath)
        {
            var directory = Path.GetDirectoryName(originalFilePath);
            var vsDir = FindVsDirectory(directory);
            if (vsDir == null)
            {
                return;
            }

            var backupDir = Path.Combine(vsDir, Vsix.Name, "backups");
            Directory.CreateDirectory(backupDir);

            var backupFileName = $"{Path.GetFileNameWithoutExtension(originalFilePath)}_{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{Path.GetExtension(originalFilePath)}";
            File.Copy(originalFilePath, Path.Combine(backupDir, backupFileName), false);
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
    }
}
