using System;
using System.Diagnostics;
using Microsoft;
using Shell = Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

internal static class Logger
{
    private static string _name;
    private static IVsOutputWindowPane _pane;
    private static IVsOutputWindow _output;

    public static async Task InitializeAsync(Shell.IAsyncServiceProvider provider, string name)
    {
        await Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        _output = await provider.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
        Assumes.Present(_output);
        _name = name;
    }

    public static async Task LogToOutputWindowAsync(object message)
    {
        try
        {
            await Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (EnsurePane())
            {
                _pane.OutputStringThreadSafe(message + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            Debug.Write(ex);
        }
    }

    private static bool EnsurePane()
    {
        Shell.ThreadHelper.ThrowIfNotOnUIThread();
        if (_pane == null)
        {
            var guid = Guid.NewGuid();
            _output.CreatePane(ref guid, _name, 1, 1);
            _output.GetPane(ref guid, out _pane);
        }

        return _pane != null;
    }
}