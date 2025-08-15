using System.IO;
using System.Runtime.CompilerServices;

namespace MadsKristensen.ImageOptimizer.Common
{
    /// <summary>
    /// Enhanced error handling and logging utilities for ImageOptimizer
    /// </summary>
    internal static class ErrorHandler
    {
        /// <summary>
        /// Safely executes an action with comprehensive error handling
        /// </summary>
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
        /// Safely executes an async action with comprehensive error handling
        /// </summary>
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
        /// Safely executes a function with comprehensive error handling and returns a result
        /// </summary>
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
        /// Validates file path and ensures it exists
        /// </summary>
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

        private static void LogError(Exception ex, string memberName, string sourceFilePath, int sourceLineNumber)
        {
            var fileName = Path.GetFileName(sourceFilePath);
            var message = $"Error in {fileName}.{memberName} (Line {sourceLineNumber}): {ex.Message}";

            // Use VS error logging
            ex.LogAsync().FireAndForget();

            // Also log to output window if available
            _ = Task.Run(async () =>
            {
                try
                {
                    OutputWindowPane outputWindow = await VS.Windows.CreateOutputWindowPaneAsync(Vsix.Name);
                    await outputWindow?.WriteLineAsync($"[ERROR] {message}");
                }
                catch
                {
                    // Fail silently if output window is not available
                }
            });
        }
    }
}