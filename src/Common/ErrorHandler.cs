using System.IO;
using System.Runtime.CompilerServices;

namespace MadsKristensen.ImageOptimizer.Common
{
    /// <summary>
    /// Enhanced error handling and logging utilities for ImageOptimizer.
    /// </summary>
    internal static class ErrorHandler
    {
        /// <summary>
        /// Safely executes an action with comprehensive error handling.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="memberName">The calling member name (auto-populated).</param>
        /// <param name="sourceFilePath">The source file path (auto-populated).</param>
        /// <param name="sourceLineNumber">The source line number (auto-populated).</param>
        public static void SafeExecute(Action action,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                LogError(ex, memberName, sourceFilePath, sourceLineNumber);
            }
        }

        /// <summary>
        /// Safely executes an async action with comprehensive error handling.
        /// </summary>
        /// <param name="action">The async action to execute.</param>
        /// <param name="memberName">The calling member name (auto-populated).</param>
        /// <param name="sourceFilePath">The source file path (auto-populated).</param>
        /// <param name="sourceLineNumber">The source line number (auto-populated).</param>
        public static async Task SafeExecuteAsync(Func<Task> action,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                LogError(ex, memberName, sourceFilePath, sourceLineNumber);
            }
        }

        /// <summary>
        /// Safely executes a function with comprehensive error handling and returns a result.
        /// </summary>
        /// <typeparam name="T">The return type.</typeparam>
        /// <param name="func">The function to execute.</param>
        /// <param name="defaultValue">The value to return on failure.</param>
        /// <param name="memberName">The calling member name (auto-populated).</param>
        /// <param name="sourceFilePath">The source file path (auto-populated).</param>
        /// <param name="sourceLineNumber">The source line number (auto-populated).</param>
        /// <returns>The function result, or defaultValue on failure.</returns>
        public static T SafeExecute<T>(Func<T> func, T defaultValue = default,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                LogError(ex, memberName, sourceFilePath, sourceLineNumber);
                return defaultValue;
            }
        }

        /// <summary>
        /// Validates that a file path exists and is accessible.
        /// </summary>
        /// <param name="filePath">The file path to validate.</param>
        /// <param name="errorMessage">Error message if validation fails.</param>
        /// <returns>True if the file is valid and accessible; otherwise, false.</returns>
        public static bool ValidateFilePath(string filePath, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                errorMessage = "File path cannot be null or empty";
                return false;
            }

            try
            {
                if (!File.Exists(filePath))
                {
                    errorMessage = $"File does not exist: {filePath}";
                    return false;
                }

                // Check if file is accessible
                using FileStream stream = File.OpenRead(filePath);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                errorMessage = $"Access denied to file: {filePath}";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"Error accessing file {filePath}: {ex.Message}";
                return false;
            }
        }

        private static OutputWindowPane _outputWindowPane;

        private static void LogError(Exception ex, string memberName, string sourceFilePath, int sourceLineNumber)
        {
            var fileName = Path.GetFileName(sourceFilePath);
            var message = $"Error in {fileName}.{memberName} (Line {sourceLineNumber}): {ex.Message}";

            // Use VS error logging
            ex.LogAsync().FireAndForget();

            // Also log to output window if available
            Task.Run(async () =>
            {
                try
                {
                    _outputWindowPane ??= await VS.Windows.CreateOutputWindowPaneAsync(Vsix.Name);
                    await _outputWindowPane?.WriteLineAsync($"[ERROR] {message}");
                }
                catch
                {
                    // Fail silently if output window is not available
                }
            }).FireAndForget();
        }
    }
}