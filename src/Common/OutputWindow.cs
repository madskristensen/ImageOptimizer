using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;

namespace MadsKristensen.ImageOptimizer.Common
{
    internal enum OutputColor
    {
        Default = 0,
        Black = 30,
        Red = 31,
        Green = 32,
        Yellow = 33,
        Blue = 34,
        Magenta = 35,
        Cyan = 36,
        White = 37,
        BrightBlack = 90,
        BrightRed = 91,
        BrightGreen = 92,
        BrightYellow = 93,
        BrightBlue = 94,
        BrightMagenta = 95,
        BrightCyan = 96,
        BrightWhite = 97
    }

    internal static class OutputWindow
    {
        private const int VSAPROPID_ReleaseVersion = -8597;
        private const string EnableColorizedOutputOptionName = "EnableColorizedOutput";
        private static readonly Version _minimumAnsiVersion = new(18, 12);
        private static readonly SemaphoreSlim _initializationLock = new(1, 1);
        private static readonly EditorOptionKey<bool> _enableColorizedOutputOption = new(EnableColorizedOutputOptionName);
        private static OutputWindowPane _pane;
        private static IEditorOptions _globalEditorOptions;
        private static bool _supportsAnsiVersion;

        internal static async Task<OutputWindowWriter> BeginWriteAsync()
        {
            await EnsureInitializedAsync();
            return new OutputWindowWriter(_pane, _supportsAnsiVersion && IsColorizedOutputEnabled());
        }

        internal static OutputColor GetColor(CompressionOutcome outcome)
        {
            return outcome switch
            {
                CompressionOutcome.Optimized => OutputColor.BrightGreen,
                CompressionOutcome.Failed => OutputColor.BrightRed,
                CompressionOutcome.TimedOut => OutputColor.BrightRed,
                CompressionOutcome.Cancelled => OutputColor.BrightYellow,
                CompressionOutcome.Unchanged => OutputColor.BrightBlack,
                CompressionOutcome.Cached => OutputColor.BrightBlack,
                _ => OutputColor.Default
            };
        }

        private static async Task EnsureInitializedAsync()
        {
            if (_pane != null)
            {
                return;
            }

            await _initializationLock.WaitAsync();

            try
            {
                if (_pane == null)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    _supportsAnsiVersion = SupportsAnsiVersion();

                    if (_supportsAnsiVersion)
                    {
                        IEditorOptionsFactoryService optionsFactory = await VS.GetMefServiceAsync<IEditorOptionsFactoryService>();
                        _globalEditorOptions = optionsFactory.GlobalOptions;
                    }

                    _pane = await VS.Windows.CreateOutputWindowPaneAsync(Vsix.Name);
                }
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        private static bool SupportsAnsiVersion()
        {
            var appId = ServiceProvider.GlobalProvider.GetService(typeof(SVsAppId)) as IVsAppId;
            if (appId == null ||
                appId.GetProperty(VSAPROPID_ReleaseVersion, out object value) != VSConstants.S_OK ||
                value is not string releaseVersion)
            {
                return false;
            }

            string versionText = releaseVersion.Split(' ')[0];
            return Version.TryParse(versionText, out Version version) && version >= _minimumAnsiVersion;
        }

        private static bool IsColorizedOutputEnabled()
        {
            return _globalEditorOptions?.IsOptionDefined(_enableColorizedOutputOption, localScopeOnly: false) == true &&
                _globalEditorOptions.GetOptionValue(_enableColorizedOutputOption);
        }

        [ComImport]
        [Guid("1EAA526A-0898-11d3-B868-00C04F79F802")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface SVsAppId
        {
        }

        [ComImport]
        [Guid("1EAA526A-0898-11d3-B868-00C04F79F802")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IVsAppId
        {
            [MethodImpl(MethodImplOptions.PreserveSig)]
            int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider serviceProvider);

            [MethodImpl(MethodImplOptions.PreserveSig)]
            int GetProperty(int propertyId, [MarshalAs(UnmanagedType.Struct)] out object value);

            [MethodImpl(MethodImplOptions.PreserveSig)]
            int SetProperty(int propertyId, [MarshalAs(UnmanagedType.Struct)] object value);

            [MethodImpl(MethodImplOptions.PreserveSig)]
            int GetGuidProperty(int propertyId, out Guid value);

            [MethodImpl(MethodImplOptions.PreserveSig)]
            int SetGuidProperty(int propertyId, ref Guid value);

            [MethodImpl(MethodImplOptions.PreserveSig)]
            int Initialize();
        }
    }

    internal sealed class OutputWindowWriter
    {
        private readonly OutputWindowPane _pane;
        private readonly bool _useAnsiColors;

        internal OutputWindowWriter(OutputWindowPane pane, bool useAnsiColors)
        {
            _pane = pane;
            _useAnsiColors = useAnsiColors;
        }

        internal async Task WriteLineAsync(string text, OutputColor color = OutputColor.Default)
        {
            if (_useAnsiColors && color != OutputColor.Default)
            {
                text = $"\u001b[{(int)color}m{text}\u001b[0m";
            }

            await _pane.WriteLineAsync(text);
        }

        internal Task ActivateAsync()
        {
            return _pane.ActivateAsync();
        }
    }
}
