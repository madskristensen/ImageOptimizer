using System.Collections.Generic;
using System.IO;
using EnvDTE;

namespace MadsKristensen.ImageOptimizer
{
    class Cache
    {
        public Dictionary<string, long> _cache;
        readonly bool _lossy;
        readonly string _solutionFullName;
        private static readonly object _syncRoot = new object();

        public Cache(string solution, bool lossy)
        {
            _solutionFullName = solution;
            _lossy = lossy;
            _cache = GetCacheFromDisk();
        }

        public bool IsFullyOptimized(string file)
        {
            var info = new FileInfo(file);

            return _cache.ContainsKey(file) && _cache[file] == info.Length;
        }

        public void AddToCache(string file)
        {
            if (string.IsNullOrEmpty(_solutionFullName))
                return;

            var info = new FileInfo(file);
            _cache[file] = info.Length;

            FileInfo cacheFile = GetCacheFileName();
            cacheFile.Directory.Create();

            lock (_syncRoot)
            {
                using (var writer = new StreamWriter(cacheFile.FullName, false))
                {
                    foreach (string key in _cache.Keys)
                    {
                        writer.WriteLine(key + "|" + _cache[key]);
                    }
                }
            }
        }

        Dictionary<string, long> GetCacheFromDisk()
        {
            if (string.IsNullOrEmpty(_solutionFullName))
                return new Dictionary<string, long>();

            FileInfo file = GetCacheFileName();
            var dic = new Dictionary<string, long>();

            if (!file.Exists)
                return dic;

            string[] lines = File.ReadAllLines(file.FullName);

            foreach (string line in lines)
            {
                string[] args = line.Split('|');

                if (args.Length != 2)
                    continue;

                if (long.TryParse(args[1], out long length))
                {
                    dic.Add(args[0], length);
                }
            }

            return dic;
        }

        FileInfo GetCacheFileName()
        {
            string file = _solutionFullName;

            if (string.IsNullOrEmpty(file))
                return null;

            string solutionDir = Path.GetDirectoryName(file);
            string vsDir = Path.Combine(solutionDir, ".vs");

            if (!Directory.Exists(vsDir))
            {
                DirectoryInfo info = Directory.CreateDirectory(vsDir);
                info.Attributes = FileAttributes.Hidden;
            }

            string fileName = _lossy ? "cache-lossy.txt" : "cache-lossless.txt";

            return new FileInfo(Path.Combine(vsDir, Vsix.Name, fileName));
        }
    }
}
