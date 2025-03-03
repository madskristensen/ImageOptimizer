using System.IO;
using MadsKristensen.ImageOptimizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Test
{
    [TestClass]
    public class CacheTests
    {
        private readonly string _rootFolder = "test_cache";
        private readonly CompressionType _type = CompressionType.Lossy;
        private Cache _cache;

        [TestInitialize]
        public void Setup()
        {
            if (Directory.Exists(_rootFolder))
            {
                Directory.Delete(_rootFolder, true);
            }
            _ = Directory.CreateDirectory(_rootFolder);
            _cache = new Cache(_rootFolder, _type);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_rootFolder))
            {
                Directory.Delete(_rootFolder, true);
            }
        }

        [TestMethod]
        public void IsFullyOptimized_FileNotInCache_ReturnsFalse()
        {
            var result = _cache.IsFullyOptimized("nonexistentfile.jpg");
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsFullyOptimized_FileInCacheWithDifferentLength_ReturnsFalse()
        {
            var filePath = Path.Combine(_rootFolder, "testfile.jpg");
            File.WriteAllText(filePath, "test content");
            _cache.AddToCache(filePath);
            File.WriteAllText(filePath, "modified content");

            var result = _cache.IsFullyOptimized(filePath);
            Assert.IsFalse(result);
        }

        [TestMethod, Ignore]
        public void IsFullyOptimized_FileInCacheWithSameLength_ReturnsTrue()
        {
            var filePath = Path.Combine(_rootFolder, "testfile.jpg");
            File.WriteAllText(filePath, "test content");
            _cache.AddToCache(filePath);

            var result = _cache.IsFullyOptimized(filePath);
            Assert.IsTrue(result);
        }

        [TestMethod, Ignore]
        public void AddToCache_FileAddedToCache()
        {
            var filePath = Path.Combine(_rootFolder, "testfile.jpg");
            File.WriteAllText(filePath, "test content");

            _cache.AddToCache(filePath);

            Assert.IsTrue(_cache._cache.ContainsKey(filePath));
            Assert.AreEqual(new FileInfo(filePath).Length, _cache._cache[filePath]);
        }
    }
}
