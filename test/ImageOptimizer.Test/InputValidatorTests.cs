using MadsKristensen.ImageOptimizer.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageOptimizer.Test
{
    [TestClass]
    public class InputValidatorTests
    {
        #region ValidateFilePath Tests

        [TestMethod]
        public void ValidateFilePath_NullPath_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateFilePath(null);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("File path cannot be null or empty", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateFilePath_EmptyPath_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateFilePath("");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("File path cannot be null or empty", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateFilePath_WhitespacePath_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateFilePath("   ");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("File path cannot be null or empty", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateFilePath_PathTraversal_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateFilePath(@"C:\folder\..\secret\file.png");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Path traversal sequences are not allowed", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateFilePath_PathTraversalUnix_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateFilePath("folder/../secret/file.png");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Path traversal sequences are not allowed", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateFilePath_InvalidCharacters_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateFilePath(@"C:\folder\file<name>.png");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("File path contains invalid characters", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateFilePath_InvalidCharactersPipe_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateFilePath(@"C:\folder\file|name.png");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("File path contains invalid characters", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateFilePath_InvalidCharactersQuote_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateFilePath("C:\\folder\\file\"name.png");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("File path contains invalid characters", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateFilePath_ValidAbsolutePath_ReturnsValid()
        {
            ValidationResult result = InputValidator.ValidateFilePath(@"C:\folder\image.png");

            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
            Assert.IsNotNull(result.GetValue<string>());
        }

        [TestMethod]
        public void ValidateFilePath_ValidRelativePath_ReturnsValid()
        {
            ValidationResult result = InputValidator.ValidateFilePath(@"folder\image.png");

            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateFilePath_PathTooLong_ReturnsInvalid()
        {
            var longPath = @"C:\" + new string('a', 260) + ".png";
            ValidationResult result = InputValidator.ValidateFilePath(longPath);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ErrorMessage.Contains("maximum length"));
        }

        [TestMethod]
        public void ValidateFilePath_ExactlyMaxLength_ReturnsValid()
        {
            // 260 chars is the limit, path under that should be valid
            var path = @"C:\test.png";
            ValidationResult result = InputValidator.ValidateFilePath(path);

            Assert.IsTrue(result.IsValid);
        }

        #endregion

        #region ValidateNumericInput Tests

        [TestMethod]
        public void ValidateNumericInput_NullInput_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateNumericInput(null);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Input cannot be empty", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateNumericInput_EmptyInput_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateNumericInput("");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Input cannot be empty", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateNumericInput_NonNumeric_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateNumericInput("abc");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("Input must be a valid integer", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateNumericInput_BelowMinimum_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateNumericInput("0", minValue: 1);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ErrorMessage.Contains("between"));
        }

        [TestMethod]
        public void ValidateNumericInput_AboveMaximum_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateNumericInput("10001", maxValue: 10000);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ErrorMessage.Contains("between"));
        }

        [TestMethod]
        public void ValidateNumericInput_ValidNumber_ReturnsValid()
        {
            ValidationResult result = InputValidator.ValidateNumericInput("500");

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(500, result.GetValue<int>());
        }

        [TestMethod]
        public void ValidateNumericInput_ValidNumberWithWhitespace_ReturnsValid()
        {
            ValidationResult result = InputValidator.ValidateNumericInput("  500  ");

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(500, result.GetValue<int>());
        }

        [TestMethod]
        public void ValidateNumericInput_MinimumBoundary_ReturnsValid()
        {
            ValidationResult result = InputValidator.ValidateNumericInput("1", minValue: 1);

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(1, result.GetValue<int>());
        }

        [TestMethod]
        public void ValidateNumericInput_MaximumBoundary_ReturnsValid()
        {
            ValidationResult result = InputValidator.ValidateNumericInput("10000", maxValue: 10000);

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(10000, result.GetValue<int>());
        }

        #endregion

        #region ValidateDpiInput Tests

        [TestMethod]
        public void ValidateDpiInput_NullInput_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateDpiInput(null);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("DPI cannot be empty", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateDpiInput_NonNumeric_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateDpiInput("abc");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual("DPI must be a valid number", result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateDpiInput_BelowMinimum_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateDpiInput("0");

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ErrorMessage.Contains("between"));
        }

        [TestMethod]
        public void ValidateDpiInput_AboveMaximum_ReturnsInvalid()
        {
            ValidationResult result = InputValidator.ValidateDpiInput("2500");

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.ErrorMessage.Contains("between"));
        }

        [TestMethod]
        public void ValidateDpiInput_ValidDpi_ReturnsValid()
        {
            ValidationResult result = InputValidator.ValidateDpiInput("96");

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(96f, result.GetValue<float>());
        }

        [TestMethod]
        public void ValidateDpiInput_ValidDecimalDpi_ReturnsValid()
        {
            ValidationResult result = InputValidator.ValidateDpiInput("72.5");

            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(72.5f, result.GetValue<float>());
        }

        #endregion

        #region SanitizeFileName Tests

        [TestMethod]
        public void SanitizeFileName_NullInput_ReturnsUnnamed()
        {
            var result = InputValidator.SanitizeFileName(null);

            Assert.AreEqual("unnamed", result);
        }

        [TestMethod]
        public void SanitizeFileName_EmptyInput_ReturnsUnnamed()
        {
            var result = InputValidator.SanitizeFileName("");

            Assert.AreEqual("unnamed", result);
        }

        [TestMethod]
        public void SanitizeFileName_ValidFileName_ReturnsSame()
        {
            var result = InputValidator.SanitizeFileName("image.png");

            Assert.AreEqual("image.png", result);
        }

        [TestMethod]
        public void SanitizeFileName_InvalidCharacters_ReplacesWithUnderscore()
        {
            var result = InputValidator.SanitizeFileName("file<name>.png");

            Assert.IsFalse(result.Contains("<"));
            Assert.IsFalse(result.Contains(">"));
        }

        [TestMethod]
        public void SanitizeFileName_TrailingDots_Trimmed()
        {
            var result = InputValidator.SanitizeFileName("filename...");

            Assert.IsFalse(result.EndsWith("."));
        }

        [TestMethod]
        public void SanitizeFileName_TrailingWhitespace_Trimmed()
        {
            var result = InputValidator.SanitizeFileName("filename   ");

            Assert.IsFalse(result.EndsWith(" "));
        }

        #endregion
    }
}
