using Anthea.Application.Modules;

namespace Anthea.Presentation.Modules.BridgeDesign;

/// <summary>
/// Bridge Design as a module migrated to MVVM (F5.12): identity from <see cref="ModuleDirectory"/>, guide of the module, the interface state
/// its sheets save (<c>scene</c>, the scenery of the drawing), one report, and its sheet, <see cref="BridgeDesignSheetViewModel"/>, with the
/// drawings of the bridge rendered by the desktop. Registered in the composition root of the desktop; until the checkpoint of F5.12 the
/// Release keeps the historic view and the sheet is opened only by the checks.
/// </summary>
public sealed class BridgeDesignSheetModule : ISheetModule
{
    private readonly IBridgeDesignFigureRenderer figures;

    /// <param name="figures">Draws the bridge for the PNG export and the report.</param>
    public BridgeDesignSheetModule(IBridgeDesignFigureRenderer figures)
    {
        ArgumentNullException.ThrowIfNull(figures);
        this.figures = figures;
    }

    public ModuleInfo Info { get; } = ModuleDirectory.Get(BridgeDesignSheetViewModel.ModuleKey);

    /// <summary>Read when asked: building the registry at startup does not load the Wiki catalogue.</summary>
    public ModuleHelp Help => ModuleHelp.For(Info.Id);

    /// <summary>The scenery of the drawing («Nuovo paesaggio», «Ponte casuale»), written as an edit of the data.</summary>
    public ModulePersistence Persistence { get; } = new(BridgeDesignSheetViewModel.UiStateKeys);

    /// <summary>One report (BridgeConceptExport with the elevation and the section), no short report.</summary>
    public ModuleReportCapabilities Reports { get; } = new(false);

    public ISheetViewModel CreateSheet(SheetContext context) => new BridgeDesignSheetViewModel(context, figures);
}
