using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using EnvDTE;

namespace MadsKristensen.ImageOptimizer
{
    internal class Cache
    {
        public Dictionary<string, long> _cache;
        Solution _solution;

        public Cache(Solution solution)
        {
            _solution = solution;
            _cache = GetCacheFromDisk();
        }

        public bool IsFullyOptimized(string file)
        {
            var info = new FileInfo(file);

            return _cache.ContainsKey(file) && _cache[file] == info.Length;
        }

        public async Task AddToCache(string file)
        {
            var info = new FileInfo(file);
            _cache[file] = info.Length;

            var cacheFile = GetCacheFileName();
            cacheFile.Directory.Create();

            using (var writer = new StreamWriter(cacheFile.FullName, false))
            {
                foreach (var key in _cache.Keys)
                {
                    await writer.WriteLineAsync(key + "|" + _cache[key]);
                }
            }
        }

        Dictionary<string, long> GetCacheFromDisk()
        {
            var file = GetCacheFileName();
            var dic = new Dictionary<string, long>();

            if (!file.Exists)
                return dic;

            string[] lines = File.ReadAllLines(file.FullName);

            foreach (string line in lines)
            {
                string[] args = line.Split('|');

                if (args.Length != 2)
                    continue;

                long length;

                if (long.TryParse(args[1], out length))
                {
                    dic.Add(args[0], length);
                }
            }

            return dic;
        }

        FileInfo GetCacheFileName()
        {
            string file = _solution.FullName;

            if (string.IsNullOrEmpty(file))
                return null;

            string solutionDir = Path.GetDirectoryName(file);
            string vsDir = Path.Combine(solutionDir, ".vs");

            if (!Directory.Exists(vsDir))
            {
                var info = Directory.CreateDirectory(vsDir);
                info.Attributes = FileAttributes.Hidden;
            }

            return new FileInfo(Path.Combine(vsDir, Vsix.Name, "cache.txt"));
        }
    }
}
