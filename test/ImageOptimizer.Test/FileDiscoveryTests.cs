using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MadsKristensen.ImageOptimizer;
using MadsKristensen.ImageOptimizer.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Test
{
    [TestClass]
    public class FileDiscoveryTests
    {
        private string _testFolder;

        [TestInitialize]
        public void Setup()
        {
            _testFolder = Path.Combine(Path.GetTempPath(), "ImageOptimizer_FileDiscoveryTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testFolder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_testFolder))
                {
                    Directory.Delete(_testFolder, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [TestMethod]
        public void EnumerateFiles_NullRoot_ReturnsEmpty()
        {
            List<string> files = FileDiscovery.EnumerateFiles(null, _ => true).ToList();

            Assert.AreEqual(0, files.Count);
        }

        [TestMethod]
        public void EnumerateFiles_PredicateFiltersMatches()
        {
            var pngFile = Path.Combine(_testFolder, "a.png");
            var jpgFile = Path.Combine(_testFolder, "b.jpg");
            File.WriteAllText(pngFile, "png");
            File.WriteAllText(jpgFile, "jpg");

            List<string> files = FileDiscovery.EnumerateFiles(_testFolder, path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).ToList();

            Assert.AreEqual(1, files.Count);
        }

        [TestMethod]
        public void EnumerateFiles_ExcludedDirectories_AreSkipped()
        {
            var includedDir = Path.Combine(_testFolder, "images");
            Directory.CreateDirectory(includedDir);
            var includedFile = Path.Combine(includedDir, "included.png");
            File.WriteAllText(includedFile, "included");

            foreach (var excludedDirectoryName in Constants.ExcludedDirectoryNames)
            {
                var excludedDir = Path.Combine(_testFolder, excludedDirectoryName);
                Directory.CreateDirectory(excludedDir);
                File.WriteAllText(Path.Combine(excludedDir, $"{excludedDirectoryName}.png"), "excluded");
            }

            List<string> files = FileDiscovery.EnumerateFiles(_testFolder, Compressor.IsFileSupported).ToList();

            Assert.AreEqual(1, files.Count);
        }
    }
}
