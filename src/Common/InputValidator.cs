using System.IO;
using System.Text.RegularExpressions;

namespace MadsKristensen.ImageOptimizer.Common
{
    /// <summary>
    /// Input validation utilities for ImageOptimizer
    /// </summary>
    internal static class InputValidator
    {
        private static readonly Regex _unsafePathCharsRegex = new(@"[<>""|?\*]", RegexOptions.Compiled);

        /// <summary>
        /// Validates and sanitizes a file path
        /// </summary>
        public static ValidationResult ValidateFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ValidationResult.Invalid("File path cannot be null or empty");
            }

            // Check for path length limits
            if (filePath.Length > 260) // Windows MAX_PATH limitation
            {
                return ValidationResult.Invalid("File path exceeds maximum length (260 characters)");
            }

            // Check for invalid characters
            if (_unsafePathCharsRegex.IsMatch(filePath))
            {
                return ValidationResult.Invalid("File path contains invalid characters");
            }

            // Check if path is rooted and valid
            try
            {
                var fullPath = Path.GetFullPath(filePath);
                return ValidationResult.Valid(fullPath);
            }
            catch (Exception ex)
            {
                return ValidationResult.Invalid($"Invalid file path: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates numeric input for resizing operations
        /// </summary>
        public static ValidationResult ValidateNumericInput(string input, int minValue = 1, int maxValue = 10000)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return ValidationResult.Invalid("Input cannot be empty");
            }

            return !int.TryParse(input.Trim(), out var value)
                ? ValidationResult.Invalid("Input must be a valid integer")
                : value < minValue || value > maxValue
                ? ValidationResult.Invalid($"Value must be between {minValue} and {maxValue}")
                : ValidationResult.Valid(value);
        }

        /// <summary>
        /// Validates DPI input
        /// </summary>
        public static ValidationResult ValidateDpiInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return ValidationResult.Invalid("DPI cannot be empty");
            }

            return !float.TryParse(input.Trim(), out var dpi)
                ? ValidationResult.Invalid("DPI must be a valid number")
                : dpi is < 1 or > 2400 ? ValidationResult.Invalid("DPI must be between 1 and 2400") : ValidationResult.Valid(dpi);
        }

        /// <summary>
        /// Sanitizes a file name by removing or replacing invalid characters
        /// </summary>
        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "unnamed";
            }

            // Replace invalid characters with underscores
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            // Trim whitespace and dots from the end
            fileName = fileName.Trim().TrimEnd('.');

            // Ensure we have a non-empty name
            return string.IsNullOrEmpty(fileName) ? "unnamed" : fileName;
        }
    }
}