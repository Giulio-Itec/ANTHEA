using System.Windows;
using X.Desktop.Services;

namespace X.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // One font resolver for the PDF of reports and model reports, installed before any of them (PDFsharp fixes it at first use)
        Anthea.Reports.Pdf.PdfFonts.Ensure();
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
        // Composition root (F5.1, decision Q2): services created by hand; the main window keeps its own services (F1.4) and
        // receives the editors of the sheets (F5.2).
        Composition = new DesktopComposition(ActiveWindow);
        var window = new MainWindow(Composition.WindowServices, Composition.SheetEditors);
        MainWindow = window;
        if (e.Args.Length == 2 && e.Args[0] == "--wiki")
            window.Loaded += (_, _) => window.Safe(() => window.ShowWiki(e.Args[1]));
        else if (e.Args.FirstOrDefault(a => !a.StartsWith("--")) is string path)
            window.Loaded += (_, _) => window.Safe(() => { window.LoadFile(path); if (e.Args.Contains("--models")) window.ShowModelsHub(); });
        window.Show();
    }

    /// <summary>Services of the presentation layer, created at startup by the composition root; null while a WPF check runs.</summary>
    internal DesktopComposition? Composition { get; private set; }

    /// <summary>Owner of the application-wide dialogs: the active window, otherwise the main window.</summary>
    private Window? ActiveWindow() => Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? MainWindow;

    /// <summary>
    /// Implemented only in the UiTests configuration (supporto/test/Desktop/AppHarness.cs), which runs the WPF checks
    /// requested on the command line and sets <paramref name="handled"/>. In Release the compiler removes the call.
    /// </summary>
    partial void RunTestHarness(StartupEventArgs e, ref bool handled);
}
