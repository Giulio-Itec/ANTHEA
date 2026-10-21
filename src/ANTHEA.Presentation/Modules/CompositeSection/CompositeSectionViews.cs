using System.Text.Json.Nodes;
using Anthea.Application.Bridge;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;
using Anthea.Application.Reporting;
using Anthea.Presentation.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.CompositeSection;

/// <summary>Draws the figures of the report of the composite bridge section (in the desktop: ModuleFigureRenderer.BridgeFigures).</summary>
public interface ICompositeSectionFigureRenderer
{
    /// <summary>The figures that <see cref="BridgeReportPresentation.Figures"/> plans for the sections <paramref name="options"/>, in its order, with its captions and categories.</summary>
    IReadOnlyList<ReportFigure> Render(BridgeResultView result, JsonObject viewSettings, IReadOnlySet<string> options);
}

/// <summary>
/// The card of a phase on the page of the phases (F5.11): header, form, homogenization (with its text n₀ · φeff) and the notes, as the
/// historic view builds them; the commands «↑», «↓» and «Elimina» (refused when read-only).
/// </summary>
public sealed class CompositeSectionPhaseCard : ObservableObject
{
    private string homogenizationInfo = "";

    internal CompositeSectionPhaseCard(int index, JsonObject phase, string header, FormViewModel form, FormViewModel? homogenization, IReadOnlyList<string> notes,
        IRelayCommand moveUp, IRelayCommand moveDown, IRelayCommand delete)
    {
        Index = index; Phase = phase; Header = header; Form = form; Homogenization = homogenization; Notes = notes;
        MoveUpCommand = moveUp; MoveDownCommand = moveDown; DeleteCommand = delete;
    }

    /// <summary>Position of the phase, from 0.</summary>
    public int Index { get; }

    /// <summary>The phase of the document.</summary>
    public JsonObject Phase { get; }

    /// <summary>«01  G1 …».</summary>
    public string Header { get; }

    /// <summary>Name, inclusion, kind, actions and reference of N of the phase.</summary>
    public FormViewModel Form { get; }

    /// <summary>φ, ψL and n of the phase, when it homogenizes the concrete; null otherwise (a note says why).</summary>
    public FormViewModel? Homogenization { get; }

    /// <summary>The text under the homogenization (n₀ · φeff, or the error of the homogenization); empty without homogenization.</summary>
    public string HomogenizationInfo { get => homogenizationInfo; internal set => SetProperty(ref homogenizationInfo, value); }

    /// <summary>The note under the homogenization (<see cref="BridgeInputPresentation.HomogenizationNote"/>), with the homogenization only.</summary>
    public string HomogenizationNote => BridgeInputPresentation.HomogenizationNote;

    /// <summary>The notes of the card, in the order of the view: the note of the shrinkage, the note of a phase without homogenization.</summary>
    public IReadOnlyList<string> Notes { get; }

    public IRelayCommand MoveUpCommand { get; }
    public IRelayCommand MoveDownCommand { get; }
    public IRelayCommand DeleteCommand { get; }
}

/// <summary>A tab of the properties of the section: «Acciaio», «Soletta» or a phase (<see cref="Phase"/>) with its properties or the error that stopped them.</summary>
public sealed record CompositeSectionPropertyTab(string Header, JsonObject? Phase, BridgePhaseProperties? Properties, string? Error);

/// <summary>
/// What the drawing of the section shows (the BridgeDrawing of the historic view, as plain data): geometry and input of the preview or of the
/// result, the situation, the display (0 total stresses, 1 contributions of the phases, 2 geometry), the switches, the amplification of the
/// concrete diagram, the points of N, the section of the local sketch. A new value at every change; the view redraws.
/// </summary>
public sealed record CompositeSectionDrawing(BridgeGeometry? Geometry, JsonObject? Input, BridgeStage? Stage, int Mode, bool Stale, bool GeometryLabels,
    bool RebarLabels, bool NeutralAxis, bool ContourSection, bool ContourDiagram, bool StressLimits, double ConcreteAmplification, bool DetailAtSupport,
    IReadOnlyList<BridgeLoadPoint> LoadPoints)
{
    internal static CompositeSectionDrawing Initial { get; } = new(null, null, null, 0, false, false, false, true, false, false, false, 1, false, []);
}

/// <summary>A chart of the page of the curves: the labels of the axes, the points and the point selected by the slider.</summary>
public sealed record CompositeSectionChart(string XLabel, string YLabel, IReadOnlyList<BridgeChartPoint> Points, int Selected)
{
    internal static CompositeSectionChart Empty { get; } = new("", "", [], 0);
}

/// <summary>
/// The calculation service of the sheet with the end of every engine run reported on the thread of the sheet (F5.11, as WallEngineWatch
/// of F5.9): the historic view stays «busy» until the engine returns, also when its request was overtaken by an edit. The end is posted to
/// the synchronization context of the sheet, after the result and before its publication, as the finally of the historic work.
/// </summary>
internal sealed class CompositeSectionEngineWatch(IModuleService inner) : IModuleService
{
    private readonly SynchronizationContext? context = SynchronizationContext.Current;

    /// <summary>Raised on the thread of the sheet when an engine run ends (completed, failed or cancelled).</summary>
    internal event Action? Ended;

    public string ModuleId => inner.ModuleId;

    public JsonObject Prepare(JsonObject source) => inner.Prepare(source);

    public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
    {
        try { return inner.Calculate(source, cancellation); }
        finally
        {
            if (context is null) Ended?.Invoke();
            else context.Post(_ => Ended?.Invoke(), null);
        }
    }
}
