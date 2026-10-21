using System.Text.Json.Nodes;
using Anthea.Application.Bridge;
using Anthea.Application.Modules;

namespace Anthea.Presentation.Modules.CompositeSection;

/// <summary>
/// The composite bridge section as a module migrated to MVVM (F5.11): identity from <see cref="ModuleDirectory"/>, guide of the module, the
/// interface state its sheets save (<c>ui_mista</c> and <c>curve_sezione</c>), the report with the sections of ReportBridge, and its sheet,
/// <see cref="CompositeSectionSheetViewModel"/>. Registered in the composition root of the desktop with the WPF figures of the report; until
/// the checkpoint of F5.11 the Release keeps the historic view and the sheet is opened only by the checks.
/// </summary>
public sealed class CompositeSectionSheetModule : ISheetModule
{
    private readonly ICompositeSectionFigureRenderer figures;
    private readonly Func<JsonObject, JsonObject, CancellationToken, SectionResponseResult>? response;

    /// <param name="figures">Draws the figures of the report.</param>
    /// <param name="response">The calculation of the curves of the section of every sheet; null (production) uses StructuralQueries.BridgeResponse.</param>
    public CompositeSectionSheetModule(ICompositeSectionFigureRenderer figures, Func<JsonObject, JsonObject, CancellationToken, SectionResponseResult>? response = null)
    {
        ArgumentNullException.ThrowIfNull(figures);
        Info = ModuleDirectory.Get(CompositeSectionSheetViewModel.ModuleKey);
        this.figures = figures; this.response = response;
    }

    public ModuleInfo Info { get; }

    /// <summary>Read when asked: building the registry at startup does not load the Wiki catalogue.</summary>
    public ModuleHelp Help => ModuleHelp.For(Info.Id);

    /// <summary>ui_mista (pages, displays, switches, scale, sketch, slab properties, splitters, report sections) and curve_sezione (the path of the curves).</summary>
    public ModulePersistence Persistence { get; } = new(CompositeSectionSheetViewModel.UiStateKeys);

    /// <summary>One report (ReportBridge with its sections and the figures of the drawings), no short report.</summary>
    public ModuleReportCapabilities Reports { get; } = new(false);

    public ISheetViewModel CreateSheet(SheetContext context) => new CompositeSectionSheetViewModel(context, figures, response);
}
