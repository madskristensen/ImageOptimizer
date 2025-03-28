using System.Collections.Concurrent;
using System.IO;

namespace MadsKristensen.ImageOptimizer
{
    internal class Cache
    {
        public ConcurrentDictionary<string, long> _cache;
        private FileInfo _cacheFile;
        private readonly CompressionType _type;

        public Cache(string rootFolder, CompressionType type)
        {
            _type = type;
            _cache = ReadCacheFromDisk(rootFolder);
        }

        public bool IsFullyOptimized(string file)
        {
            return _cache.ContainsKey(file) && _cache[file] == new FileInfo(file).Length;
        }

        public void AddToCache(string file)
        {
            if (string.IsNullOrEmpty(_cacheFile?.FullName))
            {
                return;
            }

            var info = new FileInfo(file);
            _cache[file] = info.Length;
        }

        public async Task SaveToDiskAsync()
        {
            if (!_cacheFile.Directory.Exists)
            {
                _cacheFile.Directory.Create();
            }

            using (var writer = new StreamWriter(_cacheFile.FullName, false))
            {
                foreach (var key in _cache.Keys)
                {
                    await writer.WriteLineAsync(key + "|" + _cache[key]);
                }
            }
        }

        private ConcurrentDictionary<string, long> ReadCacheFromDisk(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return [];
            }

            _cacheFile = LoadCacheFileName(fileName);
            var dic = new ConcurrentDictionary<string, long>();

            if (_cacheFile == null || !_cacheFile.Exists)
            {
                return dic;
            }

            var lines = File.ReadAllLines(_cacheFile.FullName);

            foreach (var line in lines)
            {
                var args = line.Split('|');

                if (args.Length != 2)
                {
                    continue;
                }

                if (long.TryParse(args[1], out var length))
                {
                    dic[args[0]] = length;
                }
            }

            return dic;
        }

        internal FileInfo LoadCacheFileName(string fileName)
        {
            FileInfo file = new(fileName);
            DirectoryInfo directory = file.Directory;

            while (directory != null)
            {
                var vsDirPath = Path.Combine(directory.FullName, ".vs");

                if (Directory.Exists(vsDirPath))
                {
                    var cacheFileName = _type is CompressionType.Lossy ? "cache-lossy.txt" : "cache-lossless.txt";
                    return new FileInfo(Path.Combine(vsDirPath, Vsix.Name, cacheFileName));
                }

                directory = directory.Parent;
            }

            return null;
        }
    }
}
