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
        // Tab selects the content of the input box reached, in the product and in the WPF checks.
        TabSelection.Register();
        bool handled = false;
        RunTestHarness(e, ref handled);
        if (handled) return;
        Appearance.Initialize(true);
        // F5.15, V-a: the decimal separator chosen in «Aspetto» (Sistema by default, the culture of Windows), before the first window.
        WpfNumberSeparatorService.Apply(WpfNumberSeparatorService.Read(WpfNumberSeparatorService.PreferencePath));
        ErrorLog.Attach();
        DispatcherUnhandledException += (_, error) =>
        {
            ErrorLog.Write("Errore non gestito nell'interfaccia", error.Exception);
            MessageBox.Show(error.Exception.Message, "ANTHEA — errore", MessageBoxButton.OK, MessageBoxImage.Error);
            error.Handled = true;
        };
        // Composition root (F5.1, decision Q2): services created by hand. Since F5.13 g the window of the application is the new shell.
        Composition = new DesktopComposition(ActiveWindow);
        StartShell(e.Args);
    }

    /// <summary>
    /// The new shell (F5.13 g, in place of MainWindow) from its composition root (<see cref="ShellComposition"/>): the services of
    /// <see cref="Composition"/> with the Wiki on the tab of the shell, the modules as <paramref name="implementations"/> choose them (by default the
    /// Release, <see cref="ModuleImplementations.Release"/>), the command line as MainWindow read it (<see cref="Anthea.Presentation.Shell.StartupRequest"/>:
    /// «--wiki articolo», a document opened from Explorer, «--models»), handled by the guard of the shell after the window is loaded. The window asks
    /// before closing with sheets not saved (<see cref="Anthea.Presentation.Shell.ShellViewModel.ConfirmClose"/>). The UiTests build reuses it for --interactive-shell.
    /// </summary>
    internal void StartShell(IReadOnlyList<string> args, ModuleImplementations? implementations = null)
    {
        var services = ShellComposition.WithShellWiki(Composition!.Services);
        var parts = ShellComposition.Create(services, Composition.Modules, Composition.Limiter, ShellComposition.ExamplesFolder, implementations: implementations);
        MainWindow = parts.Window;
        var request = Anthea.Presentation.Shell.StartupRequest.Parse(args);
        parts.Window.Loaded += (_, _) => parts.Shell.Start(request);
        parts.Window.Closed += (_, _) => parts.Shell.Dispose();
        parts.Window.Show();
    }

    /// <summary>Services of the presentation layer, created at startup by the composition root; null while a WPF check runs.</summary>
    internal DesktopComposition? Composition { get; private set; }

    /// <summary>Owner of the application-wide dialogs: the active window, otherwise the main window.</summary>
    private Window? ActiveWindow() => Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? MainWindow;

    /// <summary>
    /// Implemented only in the UiTests configuration (tests/ANTHEA.Desktop.UiTests/AppHarness.cs), which runs the WPF checks
    /// requested on the command line and sets <paramref name="handled"/>. In Release the compiler removes the call.
    /// </summary>
    partial void RunTestHarness(StartupEventArgs e, ref bool handled);
}
