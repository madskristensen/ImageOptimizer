using System;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

internal static class Logger
{
    private static string _name;
    private static IVsOutputWindowPane _pane;
    private static IServiceProvider _provider;

    public static void Initialize(IServiceProvider provider, string name)
    {
        _provider = provider;
        _name = name;
    }

    public static void Log(object message)
    {
        try
        {
            if (EnsurePane())
            {
                ThreadHelper.Generic.BeginInvoke(() =>
                {
                    _pane.OutputStringThreadSafe(DateTime.Now + ": " + message + Environment.NewLine);
                });
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
            ThreadHelper.Generic.Invoke(() =>
            {
                if (_pane == null)
                {
                    Guid guid = Guid.NewGuid();
                    IVsOutputWindow output = (IVsOutputWindow)_provider.GetService(typeof(SVsOutputWindow));
                    output.CreatePane(ref guid, _name, 1, 1);
                    output.GetPane(ref guid, out _pane);
                }
            });
        }

        return _pane != null;
    }
}