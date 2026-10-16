using Anthea.Application.Modules;

namespace Anthea.Presentation.Modules.VerticalPile;

/// <summary>
/// The vertical pile or the vertical micropile as a module migrated to MVVM (F5.6, second pilot): identity from
/// <see cref="ModuleDirectory"/>, guide of the module, the interface state its sheets save (the visibility of the curves) and its
/// sheet, <see cref="VerticalPileSheetViewModel"/>. One instance for each of the two modules, registered in the composition root of
/// the desktop with the WPF figures of the report; the window opens it only when ModuleImplementations chooses the MVVM
/// implementation, which before the F5.6 checkpoint happens only in the UiTests bench (--module-impl), once the view exists.
/// </summary>
public sealed class VerticalPileSheetModule : ISheetModule
{
    private readonly IVerticalPileFigureRenderer figures;

    /// <param name="moduleId"><see cref="VerticalPileSheetViewModel.PileModule"/> or <see cref="VerticalPileSheetViewModel.MicropileModule"/>.</param>
    /// <param name="figures">Draws the figures of the report.</param>
    public VerticalPileSheetModule(string moduleId, IVerticalPileFigureRenderer figures)
    {
        if (moduleId is not (VerticalPileSheetViewModel.PileModule or VerticalPileSheetViewModel.MicropileModule))
            throw new ArgumentException("Modulo dei pali verticali atteso: " + moduleId, nameof(moduleId));
        ArgumentNullException.ThrowIfNull(figures);
        Info = ModuleDirectory.Get(moduleId);
        this.figures = figures;
    }

    public ModuleInfo Info { get; }

    /// <summary>Read when asked: building the registry at startup does not load the Wiki catalogue.</summary>
    public ModuleHelp Help => ModuleHelp.For(Info.Id);

    /// <summary>visibilita_grafici; the lazy migrations are ModuleSheetPreparation.PrepareVertical.</summary>
    public ModulePersistence Persistence { get; } = new(VerticalPileSheetViewModel.UiStateKeys);

    /// <summary>One report (ReportWord, sections chosen by the window), no short report.</summary>
    public ModuleReportCapabilities Reports { get; } = new(false);

    public ISheetViewModel CreateSheet(SheetContext context) => new VerticalPileSheetViewModel(Info.Id, context, figures);
}
