using Anthea.Application.Modules;

namespace Anthea.Presentation.Modules.HorizontalPile;

/// <summary>
/// The horizontal pile or the horizontal micropile as a module migrated to MVVM (F5.7): identity from <see cref="ModuleDirectory"/>,
/// guide of the module, the interface state its sheets save (the tab of the analyses) and its sheet,
/// <see cref="HorizontalPileSheetViewModel"/> (the tab of the capacity; the elastic response is F5.8). One instance for each of the two
/// modules, registered in the composition root of the desktop with the WPF figures of the report; the window opens it only when
/// ModuleImplementations chooses the MVVM implementation, which before the F5.7 checkpoint happens only in the UiTests bench
/// (--module-impl), once the view exists.
/// </summary>
public sealed class HorizontalPileSheetModule : ISheetModule
{
    private readonly IHorizontalPileFigureRenderer figures;

    /// <param name="moduleId"><see cref="HorizontalPileSheetViewModel.PileModule"/> or <see cref="HorizontalPileSheetViewModel.MicropileModule"/>.</param>
    /// <param name="figures">Draws the figures of the report.</param>
    public HorizontalPileSheetModule(string moduleId, IHorizontalPileFigureRenderer figures)
    {
        if (moduleId is not (HorizontalPileSheetViewModel.PileModule or HorizontalPileSheetViewModel.MicropileModule))
            throw new ArgumentException("Modulo dei pali orizzontali atteso: " + moduleId, nameof(moduleId));
        ArgumentNullException.ThrowIfNull(figures);
        Info = ModuleDirectory.Get(moduleId);
        this.figures = figures;
    }

    public ModuleInfo Info { get; }

    /// <summary>Read when asked: building the registry at startup does not load the Wiki catalogue.</summary>
    public ModuleHelp Help => ModuleHelp.For(Info.Id);

    /// <summary>vista_orizzontale; the lazy migrations are HorizontalSheetPresentation.PrepareGeneral, PrepareFactors, PrepareSection and the shared elastic data.</summary>
    public ModulePersistence Persistence { get; } = new(HorizontalPileSheetViewModel.UiStateKeys);

    /// <summary>One report (ReportOrizzontale with the figures of the diagrams), no short report.</summary>
    public ModuleReportCapabilities Reports { get; } = new(false);

    public ISheetViewModel CreateSheet(SheetContext context) => new HorizontalPileSheetViewModel(Info.Id, context, figures);
}
