using System.IO;
using System.Threading.Tasks;
using MadsKristensen.ImageOptimizer;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Test
{
    [TestClass]
    public class CacheTests
    {
        private string _rootFolder;
        private readonly CompressionType _type = CompressionType.Lossy;
        private Cache _cache;

        [TestInitialize]
        public void Setup()
        {
            // Use unique folder per test to avoid conflicts
            _rootFolder = Path.Combine(Path.GetTempPath(), "ImageOptimizer_CacheTest_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootFolder);

            // Create a .vs directory structure to enable caching
            var vsDir = Path.Combine(_rootFolder, ".vs");
            var vsixDir = Path.Combine(vsDir, Vsix.Name);
            Directory.CreateDirectory(vsixDir);

            // Cache constructor expects a file path, not a directory path
            // It walks up from the file's directory looking for .vs folder
            var dummyFilePath = Path.Combine(_rootFolder, "dummy.txt");
            File.WriteAllText(dummyFilePath, "");
            _cache = new Cache(dummyFilePath, _type);
        }

        [TestCleanup]
        public void Cleanup()
        {
            CleanupTestFolder();
        }

        private void CleanupTestFolder()
        {
            if (Directory.Exists(_rootFolder))
            {
                try
                {
                    Directory.Delete(_rootFolder, true);
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
        }

        #region IsFullyOptimized Tests

        [TestMethod]
        public void IsFullyOptimized_FileNotInCache_ReturnsFalse()
        {
            var result = _cache.IsFullyOptimized("nonexistentfile.jpg");
            Assert.IsFalse(result, "Non-existent file should not be marked as optimized");
        }

        [TestMethod]
        public void IsFullyOptimized_FileInCacheWithDifferentLength_ReturnsFalse()
        {
            var filePath = Path.Combine(_rootFolder, "testfile.jpg");
            File.WriteAllText(filePath, "test content");
            _cache.AddToCache(filePath);
            File.WriteAllText(filePath, "modified content with different length");

            var result = _cache.IsFullyOptimized(filePath);
            Assert.IsFalse(result, "File with different length should not be marked as optimized");
        }

        [TestMethod]
        public void IsFullyOptimized_FileInCacheWithSameLength_ReturnsTrue()
        {
            var filePath = Path.Combine(_rootFolder, "testfile.jpg");
            var content = "test content";
            File.WriteAllText(filePath, content);
            _cache.AddToCache(filePath);

            var result = _cache.IsFullyOptimized(filePath);
            Assert.IsTrue(result, "File with same length should be marked as optimized");
        }

        [TestMethod]
        public void IsFullyOptimized_FileDeletedAfterCaching_ReturnsFalse()
        {
            var filePath = Path.Combine(_rootFolder, "testfile.jpg");
            File.WriteAllText(filePath, "test content");
            _cache.AddToCache(filePath);
            File.Delete(filePath);

            var result = _cache.IsFullyOptimized(filePath);
            Assert.IsFalse(result, "Deleted file should not be marked as optimized");
        }

        #endregion

        #region AddToCache Tests

        [TestMethod]
        public void AddToCache_ValidFile_FileAddedToCache()
        {
            var filePath = Path.Combine(_rootFolder, "testfile.jpg");
            var content = "test content";
            File.WriteAllText(filePath, content);

            _cache.AddToCache(filePath);

            Assert.IsTrue(_cache.ContainsFile(filePath), "File should be added to cache");
            Assert.AreEqual(new FileInfo(filePath).Length, _cache.GetCachedFileSize(filePath),
                "Cached file size should match actual file size");
        }

        [TestMethod]
        public void AddToCache_NonExistentFile_NoException()
        {
            var filePath = Path.Combine(_rootFolder, "nonexistent.jpg");

            // Should not throw exception
            _cache.AddToCache(filePath);

            Assert.IsFalse(_cache.ContainsFile(filePath), "Non-existent file should not be in cache");
        }

        [TestMethod]
        public void AddToCache_SameFileTwice_UpdatesSize()
        {
            var filePath = Path.Combine(_rootFolder, "testfile.jpg");
            File.WriteAllText(filePath, "short");
            _cache.AddToCache(filePath);
            var firstSize = _cache.GetCachedFileSize(filePath);

            File.WriteAllText(filePath, "longer content here");
            _cache.AddToCache(filePath);
            var secondSize = _cache.GetCachedFileSize(filePath);

            Assert.AreNotEqual(firstSize, secondSize, "Cache should update with new file size");
        }

        #endregion

        #region ContainsFile Tests

        [TestMethod]
        public void ContainsFile_EmptyPath_ReturnsFalse()
        {
            Assert.IsFalse(_cache.ContainsFile(""), "Empty path should return false");
        }

        [TestMethod]
        public void ContainsFile_WhitespacePath_ReturnsFalse()
        {
            Assert.IsFalse(_cache.ContainsFile("   "), "Whitespace path should return false");
        }

        [TestMethod]
        public void ContainsFile_CachedFile_ReturnsTrue()
        {
            var filePath = Path.Combine(_rootFolder, "testfile.jpg");
            File.WriteAllText(filePath, "test content");
            _cache.AddToCache(filePath);

            Assert.IsTrue(_cache.ContainsFile(filePath), "Cached file should return true");
        }

        #endregion

        #region GetCachedFileSize Tests

        [TestMethod]
        public void GetCachedFileSize_NonExistentFile_ReturnsNull()
        {
            var result = _cache.GetCachedFileSize("nonexistent.jpg");
            Assert.IsNull(result, "Non-existent file should return null size");
        }

        [TestMethod]
        public void GetCachedFileSize_CachedFile_ReturnsCorrectSize()
        {
            var filePath = Path.Combine(_rootFolder, "testfile.jpg");
            var content = "test content with known length";
            File.WriteAllText(filePath, content);
            _cache.AddToCache(filePath);

            var result = _cache.GetCachedFileSize(filePath);
            var expectedSize = new FileInfo(filePath).Length;

            Assert.AreEqual(expectedSize, result, "Cached size should match file size");
        }

        #endregion

        #region SaveToDiskAsync Tests

        [TestMethod]
        public async Task SaveToDiskAsync_WithCachedFiles_PersistsCache()
        {
            var filePath1 = Path.Combine(_rootFolder, "file1.jpg");
            var filePath2 = Path.Combine(_rootFolder, "file2.png");
            File.WriteAllText(filePath1, "content 1");
            File.WriteAllText(filePath2, "content 2");

            _cache.AddToCache(filePath1);
            _cache.AddToCache(filePath2);

            await _cache.SaveToDiskAsync();

            // Create new cache instance to verify persistence (use file path, not directory)
            var newCache = new Cache(filePath1, _type);
            Assert.IsTrue(newCache.ContainsFile(filePath1), "File 1 should be persisted");
            Assert.IsTrue(newCache.ContainsFile(filePath2), "File 2 should be persisted");
        }

        [TestMethod]
        public async Task SaveToDiskAsync_EmptyCache_DoesNotThrow()
        {
            // Should not throw with empty cache
            await _cache.SaveToDiskAsync();
        }

        [TestMethod]
        public async Task SaveToDiskAsync_PreservesFileSizes()
        {
            var filePath = Path.Combine(_rootFolder, "testfile.jpg");
            File.WriteAllText(filePath, "test content");
            var originalSize = new FileInfo(filePath).Length;

            _cache.AddToCache(filePath);
            await _cache.SaveToDiskAsync();

            // Create new cache instance to verify persistence (use file path, not directory)
            var newCache = new Cache(filePath, _type);
            Assert.AreEqual(originalSize, newCache.GetCachedFileSize(filePath), "File size should be preserved after save");
        }

        #endregion

        #region LoadCacheFileName Tests

        [TestMethod]
        public void LoadCacheFileName_NullPath_ReturnsNull()
        {
            FileInfo result = _cache.LoadCacheFileName(null);
            Assert.IsNull(result, "Null path should return null");
        }

        [TestMethod]
        public void LoadCacheFileName_EmptyPath_ReturnsNull()
        {
            FileInfo result = _cache.LoadCacheFileName("");
            Assert.IsNull(result, "Empty path should return null");
        }

        [TestMethod]
        public void LoadCacheFileName_ValidPath_ReturnsFileInfo()
        {
            var filePath = Path.Combine(_rootFolder, "testfile.txt");
            File.WriteAllText(filePath, "test");
            FileInfo result = _cache.LoadCacheFileName(filePath);
            Assert.IsNotNull(result, "Valid path should return FileInfo");
            Assert.IsTrue(result.Name.Contains("cache"), "Cache file name should contain 'cache'");
        }

        #endregion

        #region CompressionType Tests

        [TestMethod]
        public void Cache_LossyType_UsesLossyCacheFile()
        {
            var filePath = Path.Combine(_rootFolder, "testfile.txt");
            File.WriteAllText(filePath, "test");
            var lossyCache = new Cache(filePath, CompressionType.Lossy);
            FileInfo cacheFile = lossyCache.LoadCacheFileName(filePath);

            Assert.IsTrue(cacheFile.Name.Contains("lossy"), "Lossy cache should use lossy cache file");
        }

        [TestMethod]
        public void Cache_LosslessType_UsesLosslessCacheFile()
        {
            var filePath = Path.Combine(_rootFolder, "testfile.txt");
            File.WriteAllText(filePath, "test");
            var losslessCache = new Cache(filePath, CompressionType.Lossless);
            FileInfo cacheFile = losslessCache.LoadCacheFileName(filePath);

            Assert.IsTrue(cacheFile.Name.Contains("lossless"), "Lossless cache should use lossless cache file");
        }

        [TestMethod]
        public async Task Cache_SeparateCachesForTypes_DoNotInterfere()
        {
            var filePath = Path.Combine(_rootFolder, "testfile.jpg");
            File.WriteAllText(filePath, "test content");

            var lossyCache = new Cache(filePath, CompressionType.Lossy);
            var losslessCache = new Cache(filePath, CompressionType.Lossless);

            lossyCache.AddToCache(filePath);
            await lossyCache.SaveToDiskAsync();

            // Lossless cache should not see the file
            var newLosslessCache = new Cache(filePath, CompressionType.Lossless);
            Assert.IsFalse(newLosslessCache.ContainsFile(filePath), "Lossless cache should not contain lossy cached files");

            // Lossy cache should see the file
            var newLossyCache = new Cache(filePath, CompressionType.Lossy);
            Assert.IsTrue(newLossyCache.ContainsFile(filePath), "Lossy cache should contain lossy cached files");
        }

        #endregion
    }
}
