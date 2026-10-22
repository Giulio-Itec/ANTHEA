using System.IO;
using Anthea.Application.Examples;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Services;
using Anthea.Presentation.Shell;
using Anthea.Presentation.Shell.Commands;
using Anthea.Presentation.Shell.Tabs;
using X.Desktop.Views.Shell;

namespace X.Desktop.Services;

/// <summary>The parts of the new shell built by <see cref="ShellComposition"/>: the main window (not shown yet), its ViewModel and the tracker of the detached windows.</summary>
internal sealed record ShellParts(ShellWindow Window, ShellViewModel Shell, ShellWindowTracker Tracker);

/// <summary>
/// Composition root of the new shell (F5.13 c, after the checkpoint of 22/10; decision Q2: by hand, no container). It is NOT the startup of the
/// Release, which still opens MainWindow (App.OnStartup) until the switch of parts (f)–(g): only the UiTests build launches it, by hand with
/// <c>--interactive-shell</c> and in the check of the draft (ui/check-shell-draft).
/// <list type="bullet">
/// <item>the sheets of the modules of the registry through <see cref="RegistrySheetFactory"/>, with the limiter of the calculations;</item>
/// <item>the files of the documents with the installed examples, the exports of the sheets (<see cref="SheetFileExports"/>), the menu «Aspetto»
/// (<see cref="WpfAppearanceService"/>) and the page «Esempi»;</item>
/// <item>the detached windows: a <see cref="DetachedShellWindow"/> for each detached host, through <see cref="DetachedWindowHandle"/> for
/// <see cref="ShellWindowTracker"/> (the checks wrap them in windows that are never activated);</item>
/// <item>the Wiki of the services that opens the tab of the shell (<see cref="ShellWikiNavigator"/>), when the services carry one.</item>
/// </list>
/// </summary>
internal static class ShellComposition
{
    /// <summary>The folder of the installed examples, as MainWindow reads it.</summary>
    internal static string ExamplesFolder => Path.Combine(AppContext.BaseDirectory, "Esempi");

    /// <summary>
    /// The same services with the Wiki on the tab of the shell (<see cref="ShellWikiNavigator"/>); the Wiki of <paramref name="services"/> stays
    /// for the requests made before the shell exists or after it closes.
    /// </summary>
    internal static PresentationServices WithShellWiki(PresentationServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return new PresentationServices(services.Messages, services.Confirmations, services.Files, services.Dialogs, services.Clipboard, services.Links,
            new ShellWikiNavigator(services.Wiki));
    }

    /// <param name="services">The services of the product (DesktopComposition, through <see cref="WithShellWiki"/>) or of a check.</param>
    /// <param name="detached">The detached window as the tracker sees it; by default the WPF window itself (<see cref="DetachedWindowHandle"/>).</param>
    internal static ShellParts Create(PresentationServices services, ModuleRegistry modules, CalculationLimiter limiter, string examples,
        Func<DetachedShellWindow, IShellWindow>? detached = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentNullException.ThrowIfNull(examples);
        detached ??= window => new DetachedWindowHandle(window);
        ShellViewModel? shell = null;
        var tracker = new ShellWindowTracker(host => detached(new DetachedShellWindow(host, shell!.Windows, shell)));
        shell = new ShellViewModel(services, new RegistrySheetFactory(modules, services, limiter), new DocumentFiles(examples), tracker, new SheetFileExports(),
            new ShellOptions(new WpfAppearanceService(), () => ExampleCatalog.Load(examples)));
        tracker.Attach(shell.Windows);
        (services.Wiki as ShellWikiNavigator)?.Attach(shell);
        return new ShellParts(new ShellWindow(shell, tracker), shell, tracker);
    }
}
