using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Presentation.Calculation;
using Anthea.Presentation.Services;

namespace Anthea.Presentation.Modules;

/// <summary>
/// What a sheet receives when it is created (proposal F5, section 5.1). Built by the host: in the current window by
/// MvvmSheetEditor, with the services and the limiter of the composition root of the desktop.
/// </summary>
/// <param name="Data">Working copy owned by the sheet: the host passes a copy, never the JsonObject of the document.</param>
/// <param name="ReadOnly">Read-only at creation (a previous revision of the project).</param>
/// <param name="Host">Requests of the sheet to the window.</param>
/// <param name="Services">Services of the presentation layer (messages, questions, dialogs, Wiki…).</param>
/// <param name="Calculation">Calculation service of the module (ModuleServices, or the barrier of a check).</param>
/// <param name="Limiter">Limit of the calculations of the application, shared by the sheets.</param>
public sealed record SheetContext(JsonObject Data, bool ReadOnly, ISheetHost Host, PresentationServices Services, IModuleService Calculation,
    CalculationLimiter Limiter)
{
    public JsonObject Data { get; } = Data ?? throw new ArgumentNullException(nameof(Data));
    public ISheetHost Host { get; } = Host ?? throw new ArgumentNullException(nameof(Host));
    public PresentationServices Services { get; } = Services ?? throw new ArgumentNullException(nameof(Services));
    public IModuleService Calculation { get; } = Calculation ?? throw new ArgumentNullException(nameof(Calculation));
    public CalculationLimiter Limiter { get; } = Limiter ?? throw new ArgumentNullException(nameof(Limiter));
}
