using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MadsKristensen.ImageOptimizer
{

    /// <summary>
    /// Provides the options page for the Image Optimizer extension.
    /// </summary>
    public class OptionsProvider
    {
        /// <summary>
        /// Provides the General options page in Tools > Options > Image Optimizer.
        /// </summary>
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General> { }
    }

    /// <summary>
    /// Configuration options for ImageOptimizer.
    /// Provides user-configurable settings for performance, caching, UI, and error handling.
    /// </summary>
    [ComVisible(true)]
    public class General : BaseOptionModel<General>, IRatingConfig
    {
        // Performance settings
        [Category("Performance")]
        [DisplayName("Process Timeout (seconds)")]
        [Description("Maximum time to wait for compression processes before timing out. Range: 10-300 seconds.")]
        [DefaultValue(60)]
        public int ProcessTimeoutSeconds { get; set; } = 60;

        [Category("Performance")]
        [DisplayName("Max Parallel Threads")]
        [Description("Maximum number of parallel threads for image processing. 0 = automatic (uses processor count).")]
        [DefaultValue(0)]
        public int MaxParallelThreads { get; set; } = 0;

        // Cache settings
        [Category("Cache")]
        [DisplayName("Enable Caching")]
        [Description("Cache optimization results to avoid reprocessing unchanged files. Significantly improves performance for repeated operations.")]
        [DefaultValue(true)]
        public bool EnableCaching { get; set; } = true;

        [Category("Cache")]
        [DisplayName("Cache Validation")]
        [Description("Validate cached files by checking file size. Disable to skip validation (faster but may miss changed files).")]
        [DefaultValue(true)]
        public bool ValidateCachedFiles { get; set; } = true;

        // User Interface settings
        [Category("User Interface")]
        [DisplayName("Show Progress in Status Bar")]
        [Description("Display real-time optimization progress in Visual Studio status bar.")]
        [DefaultValue(true)]
        public bool ShowProgressInStatusBar { get; set; } = true;

        [Category("User Interface")]
        [DisplayName("Show Detailed Results")]
        [Description("Show detailed compression results for each file in the output window.")]
        [DefaultValue(true)]
        public bool ShowDetailedResults { get; set; } = true;

        // Error Handling settings
        [Category("Error Handling")]
        [DisplayName("Continue on Error")]
        [Description("Continue processing other images if one fails. Disable to stop on first error.")]
        [DefaultValue(true)]
        public bool ContinueOnError { get; set; } = true;

        [Category("Error Handling")]
        [DisplayName("Log Errors to Output")]
        [Description("Log detailed error information to the output window for troubleshooting.")]
        [DefaultValue(true)]
        public bool LogErrorsToOutput { get; set; } = true;

        // Compression settings
        [Category("Compression")]
        [DisplayName("Lossy Quality (60-100)")]
        [Description("Quality level for lossy compression. Higher values preserve more quality but reduce savings. Range: 60-100. Default: 85.")]
        [DefaultValue(85)]
        public int LossyQuality { get; set; } = 85;

        // Safety settings
        [Category("Safety")]
        [DisplayName("Create Backup Before Optimization")]
        [Description("Create a backup copy of files before optimization. Backups are stored in the .vs folder.")]
        [DefaultValue(false)]
        public bool CreateBackup { get; set; } = false;

        // Statistics (not shown in UI)
        [Browsable(false)]
        public int RatingRequests { get; set; }

        [Browsable(false)]
        [Description("Total bytes saved across all optimization sessions.")]
        public long TotalBytesSaved { get; set; }

        [Browsable(false)]
        [Description("Total number of images optimized.")]
        public int TotalImagesOptimized { get; set; }

        /// <summary>
        /// Gets the process timeout in milliseconds, clamped to valid range.
        /// </summary>
        public int ProcessTimeoutMs => Math.Max(10, Math.Min(ProcessTimeoutSeconds, 300)) * 1000;

        /// <summary>
        /// Gets the effective max parallel threads, defaulting to processor count if set to 0.
        /// </summary>
        public int EffectiveMaxParallelThreads => MaxParallelThreads <= 0
            ? Environment.ProcessorCount
            : Math.Min(MaxParallelThreads, Environment.ProcessorCount * 2);

        /// <summary>
        /// Gets the lossy quality clamped to valid range (60-100).
        /// </summary>
        public int EffectiveLossyQuality => Math.Max(60, Math.Min(LossyQuality, 100));

        /// <summary>
        /// Updates statistics after a successful optimization.
        /// </summary>
        /// <param name="bytesSaved">Number of bytes saved.</param>
        /// <param name="imagesOptimized">Number of images optimized.</param>
        public async Task UpdateStatisticsAsync(long bytesSaved, int imagesOptimized)
        {
            TotalBytesSaved += bytesSaved;
            TotalImagesOptimized += imagesOptimized;
            await SaveAsync();
        }
    }
}
