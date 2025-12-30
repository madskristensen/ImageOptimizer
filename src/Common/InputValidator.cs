using System.IO;
using System.Text.RegularExpressions;

namespace MadsKristensen.ImageOptimizer.Common
{
    /// <summary>
    /// Input validation utilities for ImageOptimizer.
    /// </summary>
    internal static class InputValidator
    {
        private static readonly Regex _unsafePathCharsRegex = new(@"[<>""|?\*]", RegexOptions.Compiled);

        /// <summary>
        /// Validates and sanitizes a file path.
        /// </summary>
        /// <param name="filePath">The file path to validate.</param>
        /// <returns>A ValidationResult indicating success or failure with the validated path.</returns>
        public static ValidationResult ValidateFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ValidationResult.Invalid("File path cannot be null or empty");
            }

            // Check for path length limits (Windows MAX_PATH limitation)
            if (filePath.Length > Constants.MaxPathLength)
            {
                return ValidationResult.Invalid($"File path exceeds maximum length ({Constants.MaxPathLength} characters)");
            }

            // Check for path traversal attempts
            if (filePath.Contains(".."))
            {
                return ValidationResult.Invalid("Path traversal sequences are not allowed");
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
        /// Validates numeric input for resizing operations.
        /// </summary>
        /// <param name="input">The string input to validate.</param>
        /// <param name="minValue">Minimum allowed value.</param>
        /// <param name="maxValue">Maximum allowed value.</param>
        /// <returns>A ValidationResult with the parsed integer value.</returns>
        public static ValidationResult ValidateNumericInput(string input, int minValue = Constants.MinDimension, int maxValue = Constants.MaxDimension)
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
        /// Validates DPI input.
        /// </summary>
        /// <param name="input">The string input to validate.</param>
        /// <returns>A ValidationResult with the parsed DPI value.</returns>
        public static ValidationResult ValidateDpiInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return ValidationResult.Invalid("DPI cannot be empty");
            }

            return !float.TryParse(input.Trim(), out var dpi)
                ? ValidationResult.Invalid("DPI must be a valid number")
                : dpi < Constants.MinDpi || dpi > Constants.MaxDpi 
                    ? ValidationResult.Invalid($"DPI must be between {Constants.MinDpi} and {Constants.MaxDpi}") 
                    : ValidationResult.Valid(dpi);
        }

        /// <summary>
        /// Sanitizes a file name by removing or replacing invalid characters.
        /// </summary>
        /// <param name="fileName">The file name to sanitize.</param>
        /// <returns>A sanitized file name safe for use in file paths.</returns>
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