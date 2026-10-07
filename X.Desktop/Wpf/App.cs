using System.Windows;

namespace X.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool handled = false;
        RunTestHarness(e, ref handled);
        if (handled) return;
        Appearance.Initialize(true);
        ErrorLog.Attach();
        DispatcherUnhandledException += (_, error) =>
        {
            ErrorLog.Write("Errore non gestito nell'interfaccia", error.Exception);
            MessageBox.Show(error.Exception.Message, "ANTHEA — errore", MessageBoxButton.OK, MessageBoxImage.Error);
            error.Handled = true;
        };
        var window = new MainWindow();
        MainWindow = window;
        if (e.Args.Length == 2 && e.Args[0] == "--wiki")
            window.Loaded += (_, _) => window.Safe(() => window.ShowWiki(e.Args[1]));
        else if (e.Args.FirstOrDefault(a => !a.StartsWith("--")) is string path)
            window.Loaded += (_, _) => window.Safe(() => window.LoadFile(path));
        window.Show();
    }

    /// <summary>
    /// Implemented only in the UiTests configuration (supporto/test/Desktop/AppHarness.cs), which runs the WPF checks
    /// requested on the command line and sets <paramref name="handled"/>. In Release the compiler removes the call.
    /// </summary>
    partial void RunTestHarness(StartupEventArgs e, ref bool handled);
}
