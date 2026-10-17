using Anthea.Application.Modules;

namespace Anthea.Presentation.Modules.RetainingWall;

/// <summary>
/// The retaining walls as a module migrated to MVVM (F5.9): identity from <see cref="ModuleDirectory"/>, guide of the module, no
/// interface state saved in the data, and its sheet, <see cref="RetainingWallSheetViewModel"/>, with the figures of the report drawn by
/// the desktop. Registered in the composition root of the desktop; the window opens it only when ModuleImplementations chooses the
/// MVVM implementation, which before the F5.9 checkpoint (and before its view exists) never happens: the Release keeps the historic view.
/// </summary>
public sealed class RetainingWallSheetModule : ISheetModule
{
    private readonly IRetainingWallFigureRenderer figures;

    /// <param name="figures">Draws the figures of the report and of «Word globale».</param>
    public RetainingWallSheetModule(IRetainingWallFigureRenderer figures)
    {
        ArgumentNullException.ThrowIfNull(figures);
        this.figures = figures;
    }

    public ModuleInfo Info { get; } = ModuleDirectory.Get(RetainingWallSheetViewModel.ModuleKey);

    /// <summary>Read when asked: building the registry at startup does not load the Wiki catalogue.</summary>
    public ModuleHelp Help => ModuleHelp.For(Info.Id);

    /// <summary>No interface state in the data; the lazy migration is WallModel.Upgrade at every opening.</summary>
    public ModulePersistence Persistence { get; } = new(RetainingWallSheetViewModel.UiStateKeys);

    /// <summary>One report (the existing generator, Word with the PDF beside it), no short report.</summary>
    public ModuleReportCapabilities Reports { get; } = new(false);

    public ISheetViewModel CreateSheet(SheetContext context) => new RetainingWallSheetViewModel(context, figures);
}
