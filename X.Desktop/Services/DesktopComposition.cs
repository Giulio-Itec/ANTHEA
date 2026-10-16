using System.Windows;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.PileGroup;

namespace X.Desktop.Services;

/// <summary>
/// Composition root of the desktop (refactoring F5.1, decision Q2 of 16/10/2026): every service of the presentation layer is
/// created here, by hand, without a container, and reaches the ViewModels through their constructors. The dialogs of the
/// application-wide services are owned by the active window, read at every call; the services of a main window
/// (<see cref="WindowServices"/>) stay owned by that window, as in F1.4.
/// </summary>
internal sealed class DesktopComposition
{
    internal DesktopComposition(Func<Window?> activeWindow)
    {
        ArgumentNullException.ThrowIfNull(activeWindow);
        Services = new PresentationServices(
            Messages: new WpfMessageService(activeWindow),
            Confirmations: new WpfConfirmationService(activeWindow),
            Files: new WpfFileDialogService(activeWindow),
            Dialogs: new WpfDialogService(activeWindow),
            Clipboard: new WpfClipboardService(),
            Links: new WpfExternalLinkService(),
            Wiki: new WpfWikiNavigator(activeWindow));
        Limiter = new CalculationLimiter(CalculationLimiter.DefaultMaximum);
        Modules = CreateModules();
        SheetEditors = new SheetEditorFactory(ModuleImplementations.Release, Modules, Services, Limiter);
    }

    /// <summary>Application-wide services; the sheets of the modules migrated to MVVM receive them (the Release opens none yet).</summary>
    internal PresentationServices Services { get; }

    /// <summary>Limit of the calculations running at the same time, shared by the sheets (proposal 4.6).</summary>
    internal CalculationLimiter Limiter { get; }

    /// <summary>The modules of the interface, from ModuleDirectory, with their MVVM implementations.</summary>
    internal ModuleRegistry Modules { get; }

    /// <summary>The editors of the sheets of the main windows, as <see cref="ModuleImplementations.Release"/> chooses them.</summary>
    internal SheetEditorFactory SheetEditors { get; }

    /// <summary>
    /// The modules migrated to MVVM, created by hand (decision Q2): the horizontal pile group (F5.4 pilot). The window opens it
    /// only when <see cref="ModuleImplementations"/> says so: until the F5.4 checkpoint the Release keeps the historic view and the
    /// MVVM sheet is chosen only in the UiTests bench (--module-impl). The WPF checks build the same registry
    /// (tests/Desktop/ModuleHostBench.cs).
    /// </summary>
    internal static ModuleRegistry CreateModules() => new([new PileGroupSheetModule()]);

    /// <summary>Factory of the services of a main window, unchanged since F1.4 (the WPF checks pass TestServices.Create instead).</summary>
    internal DesktopServices WindowServices(Window owner) => WpfDesktopServices.Create(owner);
}
