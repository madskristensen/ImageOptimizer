using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MadsKristensen.ImageOptimizer
{
    /// <summary>
    /// Caches optimization results to avoid reprocessing unchanged files.
    /// The cache is stored in the .vs folder of the solution/folder.
    /// </summary>
    internal class Cache
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _cache;
        private readonly FileInfo _cacheFile;
        private readonly CompressionType _type;
        private readonly bool _validateCachedFiles;

        /// <summary>
        /// Initializes a new instance of the <see cref="Cache"/> class.
        /// </summary>
        /// <param name="rootFolder">The root folder to locate the cache file.</param>
        /// <param name="type">The compression type for this cache.</param>
        public Cache(string rootFolder, CompressionType type, bool validateCachedFiles = true)
        {
            _type = type;
            _validateCachedFiles = validateCachedFiles;
            _cacheFile = LoadCacheFileName(rootFolder);
            _cache = ReadCacheFromDisk();
        }

        /// <summary>
        /// Checks if a file is already fully optimized (exists in cache with matching size).
        /// </summary>
        /// <param name="file">The file path to check.</param>
        /// <returns>True if the file is fully optimized; otherwise, false.</returns>
        public bool IsFullyOptimized(string file)
        {
            if (!_cache.TryGetValue(file, out var entry))
            {
                return false;
            }

            if (!_validateCachedFiles)
            {
                return true;
            }

            try
            {
                var fileInfo = new FileInfo(file);
                if (!fileInfo.Exists)
                {
                    return false;
                }

                if (entry.LastWriteTimeUtcTicks == 0)
                {
                    return entry.FileSize == fileInfo.Length;
                }

                return entry.FileSize == fileInfo.Length && entry.LastWriteTimeUtcTicks == fileInfo.LastWriteTimeUtc.Ticks;
            }
            catch (Exception ex)
            {
                ex.LogAsync().FireAndForget();
                return false;
            }
        }

        /// <summary>
        /// Adds a file to the cache with its current size.
        /// </summary>
        /// <param name="file">The file path to cache.</param>
        public void AddToCache(string file)
        {
            if (_cacheFile?.FullName == null)
            {
                return;
            }

            try
            {
                var info = new FileInfo(file);
                if (info.Exists)
                {
                    _cache[file] = new CacheEntry(info.Length, info.LastWriteTimeUtc.Ticks);
                }
            }
            catch (Exception ex)
            {
                ex.LogAsync().FireAndForget();
            }
        }

        /// <summary>
        /// Checks if a file path exists in the cache.
        /// </summary>
        internal bool ContainsFile(string filePath)
        {
            return _cache.ContainsKey(filePath);
        }

        /// <summary>
        /// Gets the cached file size for a path, if it exists.
        /// </summary>
        internal long? GetCachedFileSize(string filePath)
        {
            return _cache.TryGetValue(filePath, out var entry) ? entry.FileSize : null;
        }

        /// <summary>
        /// Saves the cache to disk asynchronously using async file I/O.
        /// </summary>
        public async Task SaveToDiskAsync()
        {
            if (_cacheFile?.FullName == null)
            {
                return;
            }

            // Use async file I/O for better performance
            await SaveToDiskInternalAsync();
        }

        private void SaveToDiskInternal()
        {
            try
            {
                if (!_cacheFile.Directory.Exists)
                {
                    _cacheFile.Directory.Create();
                }

                // Use StringBuilder for better performance with large caches
                var sb = new StringBuilder(_cache.Count * 50); // Estimate average line length
                foreach (KeyValuePair<string, CacheEntry> kvp in _cache)
                {
                    _ = sb.AppendLine($"{kvp.Key}|{kvp.Value.FileSize}|{kvp.Value.LastWriteTimeUtcTicks}");
                }

                // Write all content at once using async I/O
                File.WriteAllText(_cacheFile.FullName, sb.ToString());
            }
            catch (Exception ex)
            {
                ex.LogAsync().FireAndForget();
            }
        }


        /// <summary>
        /// Saves the cache to disk using async file I/O.
        /// </summary>
        private async Task SaveToDiskInternalAsync()
        {
            try
            {
                if (!_cacheFile.Directory.Exists)
                {
                    _cacheFile.Directory.Create();
                }

                // Use StringBuilder for better performance with large caches
                var sb = new StringBuilder(_cache.Count * 50);
                foreach (KeyValuePair<string, CacheEntry> kvp in _cache)
                {
                    _ = sb.AppendLine($"{kvp.Key}|{kvp.Value.FileSize}|{kvp.Value.LastWriteTimeUtcTicks}");
                }

                // Use async StreamWriter for .NET Framework compatibility
                using (var writer = new StreamWriter(_cacheFile.FullName, false, Encoding.UTF8))
                {
                    await writer.WriteAsync(sb.ToString()).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                ex.LogAsync().FireAndForget();
            }
        }

        private ConcurrentDictionary<string, CacheEntry> ReadCacheFromDisk()
        {
            var dic = new ConcurrentDictionary<string, CacheEntry>();

            if (_cacheFile?.FullName == null || !_cacheFile.Exists)
            {
                return dic;
            }

            try
            {
                var content = File.ReadAllText(_cacheFile.FullName);
                if (string.IsNullOrEmpty(content))
                {
                    return dic;
                }

                ParseCacheContent(content, dic);
            }
            catch (Exception ex)
            {
                // If cache is corrupted, start fresh and log the error
                ex.LogAsync().FireAndForget();
                return new ConcurrentDictionary<string, CacheEntry>();
            }

            return dic;
        }

        /// <summary>
        /// Reads the cache from disk using async file I/O.
        /// </summary>
        internal async Task<ConcurrentDictionary<string, CacheEntry>> ReadCacheFromDiskAsync()
        {
            var dic = new ConcurrentDictionary<string, CacheEntry>();

            if (_cacheFile?.FullName == null || !_cacheFile.Exists)
            {
                return dic;
            }

            try
            {
                // Use async StreamReader for .NET Framework compatibility
                string content;
                using (var reader = new StreamReader(_cacheFile.FullName, Encoding.UTF8))
                {
                    content = await reader.ReadToEndAsync().ConfigureAwait(false);
                }
                
                if (string.IsNullOrEmpty(content))
                {
                    return dic;
                }

                ParseCacheContent(content, dic);
            }
            catch (Exception ex)
            {
                ex.LogAsync().FireAndForget();
                return new ConcurrentDictionary<string, CacheEntry>();
            }



            return dic;
        }

        /// <summary>
        /// Parses cache content into a dictionary.
        /// </summary>
        private static void ParseCacheContent(string content, ConcurrentDictionary<string, CacheEntry> dic)
        {
            var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var lastSeparatorIndex = line.LastIndexOf('|');
                if (lastSeparatorIndex <= 0 || lastSeparatorIndex == line.Length - 1)
                {
                    continue;
                }

                var secondLastSeparatorIndex = line.LastIndexOf('|', lastSeparatorIndex - 1);
                if (secondLastSeparatorIndex <= 0)
                {
                    var legacyFilePath = line.Substring(0, lastSeparatorIndex);
                    var legacyLength = line.Substring(lastSeparatorIndex + 1);
                    if (long.TryParse(legacyLength, out var legacySize))
                    {
                        dic[legacyFilePath] = new CacheEntry(legacySize, 0);
                    }

                    continue;
                }

                var filePath = line.Substring(0, secondLastSeparatorIndex);
                var lengthStr = line.Substring(secondLastSeparatorIndex + 1, lastSeparatorIndex - secondLastSeparatorIndex - 1);
                var timestampStr = line.Substring(lastSeparatorIndex + 1);

                if (long.TryParse(lengthStr, out var length) && long.TryParse(timestampStr, out var lastWriteTicks))
                {
                    dic[filePath] = new CacheEntry(length, lastWriteTicks);
                }
            }
        }

        internal readonly struct CacheEntry
        {
            internal CacheEntry(long fileSize, long lastWriteTimeUtcTicks)
            {
                FileSize = fileSize;
                LastWriteTimeUtcTicks = lastWriteTimeUtcTicks;
            }

            internal long FileSize { get; }

            internal long LastWriteTimeUtcTicks { get; }
        }

        /// <summary>
        /// Determines the cache file path based on the solution/folder structure.
        /// </summary>
        internal FileInfo LoadCacheFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            try
            {
                var file = new FileInfo(fileName);
                DirectoryInfo directory = file.Directory;

                while (directory != null)
                {
                    var vsDirPath = Path.Combine(directory.FullName, Constants.VsDirectoryName);

                    if (Directory.Exists(vsDirPath))
                    {
                        var cacheFileName = _type is CompressionType.Lossy 
                            ? Constants.LossyCacheFileName 
                            : Constants.LosslessCacheFileName;
                        return new FileInfo(Path.Combine(vsDirPath, Vsix.Name, cacheFileName));
                    }

                    directory = directory.Parent;
                }
            }
            catch (Exception ex)
            {
                ex.LogAsync().FireAndForget();
            }

            return null;
        }
    }
}
