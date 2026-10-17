using System.Windows;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Modules;
using Anthea.Presentation.Modules.ConcreteSection;
using Anthea.Presentation.Modules.HorizontalPile;
using Anthea.Presentation.Modules.Materials;
using Anthea.Presentation.Modules.PileGroup;
using Anthea.Presentation.Modules.RetainingWall;
using Anthea.Presentation.Modules.ShallowFoundation;
using Anthea.Presentation.Modules.Stratigraphy;
using Anthea.Presentation.Modules.VerticalPile;

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

    /// <summary>Application-wide services; the sheets of the modules migrated to MVVM receive them (in the Release the pile group since F5.4).</summary>
    internal PresentationServices Services { get; }

    /// <summary>Limit of the calculations running at the same time, shared by the sheets (proposal 4.6).</summary>
    internal CalculationLimiter Limiter { get; }

    /// <summary>The modules of the interface, from ModuleDirectory, with their MVVM implementations.</summary>
    internal ModuleRegistry Modules { get; }

    /// <summary>The editors of the sheets of the main windows, as <see cref="ModuleImplementations.Release"/> chooses them.</summary>
    internal SheetEditorFactory SheetEditors { get; }

    /// <summary>
    /// The modules migrated to MVVM, created by hand (decision Q2): the horizontal pile group (F5.4 pilot), the materials, concrete
    /// and reinforcing steel (F5.5), the vertical pile and micropile (F5.6, second pilot, with the WPF figures of their report) and the
    /// horizontal pile and micropile (F5.7, the tab of the capacity, with the WPF diagrams of their report), the shallow
    /// foundations (F5.9b), the stratigraphy (F5.6b) and the retaining walls (F5.9, with the WPF figures of their report).
    /// The window opens them only when <see cref="ModuleImplementations"/> says so (the pile group since F5.4): until their checkpoints the Release keeps the
    /// historic views and the MVVM sheets are chosen only in the UiTests bench (--module-impl). The WPF checks build the same registry
    /// (tests/Desktop/ModuleHostBench.cs).
    /// </summary>
    internal static ModuleRegistry CreateModules()
    {
        var verticalFigures = new VerticalPileFigureRenderer();
        var horizontalFigures = new HorizontalPileFigureRenderer();
        return new([new PileGroupSheetModule(), new VerticalPileSheetModule(VerticalPileSheetViewModel.PileModule, verticalFigures),
            new VerticalPileSheetModule(VerticalPileSheetViewModel.MicropileModule, verticalFigures), new ConcreteMaterialSheetModule(),
            new RebarMaterialSheetModule(), new HorizontalPileSheetModule(HorizontalPileSheetViewModel.PileModule, horizontalFigures),
            new HorizontalPileSheetModule(HorizontalPileSheetViewModel.MicropileModule, horizontalFigures), new ShallowFoundationSheetModule(transfer: new WpfSoilTransferDialog()),
            // F5.6b: the stratigraphy, with the AI import of Application (disabled, as the historic button) and, since its XAML view, the shared
            // soil transfer dialog on the dialog service (no dialog in the options: SoilTransferDialogService, decision T5).
            new StratigraphySheetModule(new(Anthea.Application.Stratigraphy.StratigraphyImport.OpenAi(), Anthea.Application.Stratigraphy.StratigraphyImport.EnvironmentDefaults)),
            new RetainingWallSheetModule(new RetainingWallFigureRenderer()),
            // F5.10 S1: the RC section (shell, calculation, inputs); the Release keeps the historic view until S9 (C10).
            new ConcreteSectionSheetModule()]);
    }

    /// <summary>Factory of the services of a main window, unchanged since F1.4 (the WPF checks pass TestServices.Create instead).</summary>
    internal DesktopServices WindowServices(Window owner) => WpfDesktopServices.Create(owner);
}
