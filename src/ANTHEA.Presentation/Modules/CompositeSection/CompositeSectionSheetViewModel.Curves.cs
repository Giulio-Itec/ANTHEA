using System.Text;
using System.Text.Json.Nodes;
using Anthea.Application.Bridge;
using Anthea.Application.Presentation;
using Anthea.Application.Scheduling;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;
using X.Core;

namespace Anthea.Presentation.Modules.CompositeSection;

/// <summary>
/// The page of the curves of the section (BridgeResponse of the historic view): the path of the deformation saved in <c>curve_sezione</c>,
/// the starting phase, the calculation on request on a background thread (one at a time, cancelled by an edit), the chosen point and fibre,
/// the charts, the table and the CSV of the points.
/// </summary>
public sealed partial class CompositeSectionSheetViewModel
{
    private readonly Func<JsonObject, JsonObject, CancellationToken, SectionResponseResult> response;
    private readonly RecalculationScheduler responseRecalculation = new();
    private CancellationTokenSource? responseCancellation;
    private JsonObject responseData = null!;
    private FormViewModel? curveForm;
    private SectionResponseResult? responseCalculation;
    private int responseRevision, curvePhaseIndex = -1, fiberIndex = -1;
    private double pointValue, pointMaximum = DefaultPointMaximum;
    private bool responseBuilding, responseBusy, curveExportEnabled, curvePhaseShown = true;
    private string curveNotice = BridgeResponsePresentation.NoCurve, pointInfo = "";
    private IReadOnlyList<string> curvePhases = [];
    private IReadOnlyList<BridgeFiberChoice> fiberChoices = [];
    private BridgePanel? curveTable;
    private CompositeSectionChart sectionChart = CompositeSectionChart.Empty, fiberChart = CompositeSectionChart.Empty;

    // The maximum of the slider of the points before the first curve (the default of the historic Slider).
    private const double DefaultPointMaximum = 10;

    /// <summary>The path of the deformation (<c>curve_sezione</c>).</summary>
    public JsonObject ResponseData => responseData;

    /// <summary>The form of the path; κ and Δκ without the presentation of the numbers.</summary>
    public FormViewModel? CurveForm => curveForm;

    /// <summary>The situations of the phases, the starting state «Dopo una fase dello storico».</summary>
    public IReadOnlyList<string> CurvePhases { get => curvePhases; private set => SetProperty(ref curvePhases, value); }

    /// <summary>The starting phase (refused when read-only): saved in the path, the curve stale, with Modified.</summary>
    public int CurvePhaseIndex
    {
        get => curvePhaseIndex;
        set
        {
            if (!CanEdit || value == curvePhaseIndex || value < 0 || value >= curvePhases.Count || IsDisposed) return;
            curvePhaseIndex = value; OnPropertyChanged();
            if (responseBuilding) return;
            responseData["fase"] = Math.Max(0, value); InvalidateResponse(); StateChanged();
        }
    }

    /// <summary>The starting phase is asked (the path starts after a phase of the history).</summary>
    public bool CurvePhaseShown { get => curvePhaseShown; private set => SetProperty(ref curvePhaseShown, value); }

    /// <summary>The curve is being calculated («Calcola curva» is disabled meanwhile).</summary>
    public bool CurveBusy { get => responseBusy; private set { if (SetProperty(ref responseBusy, value)) NotifyResponseCommands(); } }

    /// <summary>The points and fibres of the last curve can be exported (until the data change).</summary>
    public bool CurveExportEnabled { get => curveExportEnabled; private set { if (SetProperty(ref curveExportEnabled, value)) ExportCurveCommand?.NotifyCanExecuteChanged(); } }

    /// <summary>The last curve calculated, null without one.</summary>
    public SectionResponseResult? ResponseCalculation => responseCalculation;

    /// <summary>The state of the curve: none, calculating, calculated, cancelled, failed or stale.</summary>
    public string CurveNotice { get => curveNotice; private set => SetProperty(ref curveNotice, value); }

    /// <summary>The text of the chosen point (and of the chosen fibre).</summary>
    public string PointInfo { get => pointInfo; private set => SetProperty(ref pointInfo, value); }

    /// <summary>The chosen point of the curve (the slider; refused when read-only), between 0 and <see cref="PointMaximum"/>.</summary>
    public double PointValue
    {
        get => pointValue;
        set
        {
            if (!CanEdit || IsDisposed) return;
            SetPoint(value);
        }
    }

    /// <summary>The last point of the curve.</summary>
    public double PointMaximum { get => pointMaximum; private set => SetProperty(ref pointMaximum, value); }

    /// <summary>The fibres of the last curve.</summary>
    public IReadOnlyList<BridgeFiberChoice> FiberChoices { get => fiberChoices; private set => SetProperty(ref fiberChoices, value); }

    /// <summary>The chosen fibre (refused when read-only).</summary>
    public int FiberIndex
    {
        get => fiberIndex;
        set
        {
            if (!CanEdit || value == fiberIndex || value < 0 || value >= fiberChoices.Count || IsDisposed) return;
            fiberIndex = value; OnPropertyChanged();
            RefreshResponsePlots();
        }
    }

    /// <summary>«Risposta della sezione».</summary>
    public CompositeSectionChart SectionChart { get => sectionChart; private set => SetProperty(ref sectionChart, value); }

    /// <summary>«Tensione e deformazione della fibra».</summary>
    public CompositeSectionChart FiberChart { get => fiberChart; private set => SetProperty(ref fiberChart, value); }

    /// <summary>«Punti accettati del percorso».</summary>
    public BridgePanel? CurveTable { get => curveTable; private set => SetProperty(ref curveTable, value); }

    /// <summary>«Calcola curva» (refused when read-only and while a curve is calculated).</summary>
    public IAsyncRelayCommand CalculateCurveCommand { get; private set; } = null!;

    /// <summary>«Annulla» of the curve (refused when read-only).</summary>
    public IRelayCommand CancelCurveCommand { get; private set; } = null!;

    /// <summary>«Esporta punti e fibre CSV…» (also when read-only).</summary>
    public IRelayCommand ExportCurveCommand { get; private set; } = null!;

    private void CreateResponseCommands()
    {
        CalculateCurveCommand = GuardedCommands.CreateAsync(Guard, _ => CalculateResponseAsync(), Lifetime, () => CanEdit && !responseBusy);
        CancelCurveCommand = EditCommand(CancelResponse);
        ExportCurveCommand = GuardedCommands.Create(Guard, ExportResponse, () => curveExportEnabled);
    }

    private void NotifyResponseCommands()
    {
        CalculateCurveCommand?.NotifyCanExecuteChanged(); ExportCurveCommand?.NotifyCanExecuteChanged();
    }

    /// <summary>BuildResponsePage of the historic view: the path completed in the data, its form and its fields.</summary>
    private void BuildResponsePage()
    {
        responseData = BridgeSheetPresentation.PrepareResponse(Data);
        curveForm = Configure(new FormViewModel(responseData, CompositeSectionFields.Curve, _ => { RefreshResponseFields(); InvalidateResponse(); StateChanged(); },
            compact: true, wideChoices: true));
        foreach (string key in CompositeSectionFields.CurveRawKeys) curveForm[key].NumberFormatting = false;
        OnPropertyChanged(nameof(CurveForm)); OnPropertyChanged(nameof(ResponseData));
        RefreshResponseFields();
    }

    /// <summary>The page of the curves before an opening (Reload): as the controls of a new historic view.</summary>
    private void ResetResponse()
    {
        curveForm = null; responseCalculation = null; curvePhaseIndex = -1; fiberIndex = -1; pointValue = 0; pointMaximum = DefaultPointMaximum;
        CurveExportEnabled = false; CurvePhaseShown = true; CurveNotice = BridgeResponsePresentation.NoCurve; PointInfo = "";
        CurvePhases = []; FiberChoices = []; CurveTable = null; SectionChart = CompositeSectionChart.Empty; FiberChart = CompositeSectionChart.Empty;
        foreach (string name in new[] { nameof(CurveForm), nameof(ResponseCalculation), nameof(CurvePhaseIndex), nameof(FiberIndex), nameof(PointValue), nameof(PointMaximum) })
            OnPropertyChanged(name);
    }

    private void RefreshResponseFields()
    {
        if (curveForm is null) return;
        foreach (var field in curveForm.Fields) curveForm.ShowField(field.Key, BridgeResponsePresentation.FieldShown(field.Key, responseData));
        CurvePhaseShown = BridgeResponsePresentation.PhaseShown(responseData);
    }

    /// <summary>The page of the curves entered: the situations of the active phases, the starting phase kept within them (saved in the path).</summary>
    private void EnterResponsePage()
    {
        responseBuilding = true;
        CurvePhases = BridgeResponsePresentation.Phases(Data);
        curvePhaseIndex = curvePhases.Count == 0 ? -1 : Math.Clamp(Index(responseData, "fase"), 0, curvePhases.Count - 1);
        OnPropertyChanged(nameof(CurvePhaseIndex));
        responseData["fase"] = Math.Max(0, curvePhaseIndex); responseBuilding = false;
    }

    /// <summary>The data or the path changed: the curve calculated or running is stale, its export disabled.</summary>
    private void InvalidateResponse()
    {
        responseRevision++; responseRecalculation.Invalidate(); responseCancellation?.Cancel();
        CurveExportEnabled = false;
        if (responseCalculation is not null) CurveNotice = BridgeResponsePresentation.Stale;
    }

    /// <summary>«Annulla»: the curve running is cancelled (its notice says so).</summary>
    public void CancelResponse()
    {
        if (!CanEdit) return;
        responseCancellation?.Cancel();
    }

    /// <summary>
    /// «Calcola curva» (refused when read-only and while a curve is calculated): the sheet committed, the curve of the data and of the path of
    /// now on a background thread; published unless the data changed meanwhile or it was cancelled.
    /// </summary>
    public async Task CalculateResponseAsync()
    {
        if (IsDisposed || responseBusy || !CanEdit) return;
        Commit(); curveForm?.Commit();
        var data = (JsonObject)Data.DeepClone(); var request = (JsonObject)responseData.DeepClone();
        int version = responseRevision; responseCancellation?.Dispose(); responseCancellation = new(); var token = responseCancellation.Token;
        var completed = await responseRecalculation.ScheduleAsync(async currentToken =>
        {
            CurveBusy = true; CurveNotice = BridgeResponsePresentation.Running;
            try { return await Task.Run(() => response(data, request, currentToken), currentToken); }
            finally { CurveBusy = false; }
        }, cancellation: token);
        if (completed.Cancelled && !IsDisposed && version == responseRevision) CurveNotice = BridgeResponsePresentation.Cancelled;
        completed.TryPublish(current =>
        {
            try
            {
                if (current.Error is not null) throw current.Error;
                var result = current.Value!;
                responseCalculation = result; OnPropertyChanged(nameof(ResponseCalculation)); CurveExportEnabled = true;
                CurveNotice = BridgeResponsePresentation.Calculated(result);
                PointMaximum = Math.Max(0, result.Points.Count - 1);
                if (pointValue > pointMaximum) { pointValue = pointMaximum; OnPropertyChanged(nameof(PointValue)); }
                SetPoint(pointMaximum);
                var (choices, selected) = BridgeResponsePresentation.Fibers(result);
                FiberChoices = choices;
                fiberIndex = selected; OnPropertyChanged(nameof(FiberIndex));
                CurveTable = BridgeResponsePresentation.Table(result);
                RefreshResponsePlots();
            }
            catch (Exception error) when (error is not OutOfMemoryException) { CurveNotice = BridgeResponsePresentation.Failed(error.Message); CurveExportEnabled = false; }
        });
    }

    private void SetPoint(double value)
    {
        value = Math.Clamp(value, 0, pointMaximum);
        if (value == pointValue) return;
        pointValue = value; OnPropertyChanged(nameof(PointValue));
        RefreshResponsePlots();
    }

    private void RefreshResponsePlots()
    {
        if (responseCalculation is not { } r || r.Points.Count == 0)
        {
            SectionChart = sectionChart with { Points = [] }; FiberChart = fiberChart with { Points = [] };
            PointInfo = BridgeResponsePresentation.NoPoints; return;
        }
        int selected = Math.Clamp((int)pointValue, 0, r.Points.Count - 1);
        var section = BridgeResponsePresentation.Section(r);
        SectionChart = new(section.XLabel, section.YLabel, section.Points, selected);
        string? fiber = fiberIndex >= 0 && fiberIndex < fiberChoices.Count ? fiberChoices[fiberIndex].Id : null;
        PointInfo = BridgeResponsePresentation.PointInfo(r, selected, fiber);
        var chart = fiber is null ? null : BridgeResponsePresentation.Fiber(r, selected, fiber);
        FiberChart = new(BridgeResponsePresentation.FiberXLabel, BridgeResponsePresentation.FiberYLabel, chart?.Points ?? [], selected);
    }

    /// <summary>«Esporta punti e fibre CSV…» (also when read-only): the points and the fibres of the last curve, until the data change.</summary>
    public void ExportResponse()
    {
        if (responseCalculation is not { } result || !curveExportEnabled) return;
        string? path = Context.Services.Files.SaveFile(new SaveFileRequest([new FileFilter("CSV", "*.csv")], BridgeResponsePresentation.CsvFileName));
        if (path is null) return;
        Archivio.ScriviAtomico(path, Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(BridgeEditor.ResponseCsv(result))).ToArray());
    }
}
