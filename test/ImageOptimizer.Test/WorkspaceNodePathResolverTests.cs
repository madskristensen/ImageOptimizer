using System;
using System.IO;
using MadsKristensen.ImageOptimizer.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Test
{
    [TestClass]
    public class WorkspaceNodePathResolverTests
    {
        private string _testFolder;

        [TestInitialize]
        public void Initialize()
        {
            _testFolder = Path.Combine(Path.GetTempPath(), "ImageOptimizer_Workspace_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testFolder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testFolder))
            {
                Directory.Delete(_testFolder, true);
            }
        }

        [TestMethod]
        public void TryGetPathFromCandidate_UsesFileSystemInfoType()
        {
            var candidate = new FileInfo(Path.Combine(_testFolder, "image.png"));

            bool resolved = WorkspaceNodePathResolver.TryGetPathFromCandidate(candidate, out var path, out var isFolder);

            Assert.IsTrue(resolved);
            Assert.AreEqual(candidate.FullName, path);
            Assert.IsFalse(isFolder);
        }

        [TestMethod]
        public void TryGetPathFromCandidate_UsesExplicitFolderHint()
        {
            var candidate = new Candidate { FullPath = Path.Combine(_testFolder, "virtual.item"), IsFolder = true };

            bool resolved = WorkspaceNodePathResolver.TryGetPathFromCandidate(candidate, out var path, out var isFolder);

            Assert.IsTrue(resolved);
            Assert.AreEqual(candidate.FullPath, path);
            Assert.IsTrue(isFolder);
        }

        [TestMethod]
        public void TryGetPathFromCandidate_InfersExistingDirectory()
        {
            var candidate = new CandidateWithoutHint { Path = _testFolder };

            bool resolved = WorkspaceNodePathResolver.TryGetPathFromCandidate(candidate, out var path, out var isFolder);

            Assert.IsTrue(resolved);
            Assert.AreEqual(_testFolder, path);
            Assert.IsTrue(isFolder);
        }

        [TestMethod]
        public void TryGetPathFromCandidate_RejectsWhitespacePath()
        {
            var candidate = new CandidateWithoutHint { Path = "   " };

            Assert.IsFalse(WorkspaceNodePathResolver.TryGetPathFromCandidate(candidate, out _, out _));
        }

        private sealed class Candidate
        {
            public string FullPath { get; set; }
            public bool IsFolder { get; set; }
        }

        private sealed class CandidateWithoutHint
        {
            public string Path { get; set; }
        }
    }
}
