using System;
using MadsKristensen.ImageOptimizer.Resx;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Test
{
    [TestClass]
    public class ResxCompressionResultTests
    {
        [TestMethod, TestCategory("Resx")]
        public void WhenOptimizedThenSavingIsPositive()
        {
            var result = new ResxCompressionResult("img", "test.resx", 1000, 800, "base64data");

            Assert.AreEqual(200, result.Saving);
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenNoSavingThenSavingIsZero()
        {
            var result = new ResxCompressionResult("img", "test.resx", 1000, 1000, "base64data");

            Assert.AreEqual(0, result.Saving);
        }

        [TestMethod, TestCategory("Resx")]
        public void PercentSavedCalculatesCorrectly()
        {
            var result = new ResxCompressionResult("img", "test.resx", 1000, 750, "base64data");

            Assert.AreEqual(25.0, result.PercentSaved);
        }

        [TestMethod, TestCategory("Resx")]
        public void WhenOriginalSizeIsZeroThenPercentSavedIsZero()
        {
            var result = new ResxCompressionResult("img", "test.resx", 0, 0, null);

            Assert.AreEqual(0.0, result.PercentSaved);
        }

        [TestMethod, TestCategory("Resx")]
        public void ZeroReturnsNoSavings()
        {
            ResxCompressionResult result = ResxCompressionResult.Zero("myResource", "path.resx");

            Assert.AreEqual("myResource", result.ResourceName);
            Assert.AreEqual("path.resx", result.ResxFilePath);
            Assert.AreEqual(0, result.OriginalSize);
            Assert.AreEqual(0, result.OptimizedSize);
            Assert.AreEqual(0, result.Saving);
            Assert.IsNull(result.OptimizedBase64);
        }

        [TestMethod, TestCategory("Resx")]
        public void ConstructorSetsAllProperties()
        {
            var result = new ResxCompressionResult("Logo", "Resources.resx", 5000, 3500, "abc123");

            Assert.AreEqual("Logo", result.ResourceName);
            Assert.AreEqual("Resources.resx", result.ResxFilePath);
            Assert.AreEqual(5000, result.OriginalSize);
            Assert.AreEqual(3500, result.OptimizedSize);
            Assert.AreEqual("abc123", result.OptimizedBase64);
            Assert.AreEqual(1500, result.Saving);
            Assert.AreEqual(30.0, result.PercentSaved);
        }
    }
}
