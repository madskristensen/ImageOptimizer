using System;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;
using Interop = Microsoft.VisualStudio.Shell.Interop;

internal static class Logger
{
    private static string _name;
    private static Interop.IVsOutputWindowPane _pane;
    private static Interop.IVsOutputWindow _output;

    public static async Task InitializeAsync(IAsyncServiceProvider provider, string name)
    {
        _output = await provider.GetServiceAsync(typeof(Interop.SVsOutputWindow)) as Interop.IVsOutputWindow;
        _name = name;
    }

    public static async Task LogToOutputWindowAsync(object message)
    {
        try
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (EnsurePane())
            {
                _pane.OutputStringThreadSafe(message + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.Write(ex);
        }
    }

    private static bool EnsurePane()
    {
        if (_pane == null)
        {
            var guid = Guid.NewGuid();
            _output.CreatePane(ref guid, _name, 1, 1);
            _output.GetPane(ref guid, out _pane);
        }

        return _pane != null;
    }
}