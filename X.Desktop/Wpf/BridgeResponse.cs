using Anthea.Application.Presentation;
using Anthea.Application.Scheduling;
using Anthea.Application.Modules;
using Anthea.Presentation.Modules.CompositeSection;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

internal sealed partial class BridgeWorkspace
{
    private readonly ContentControl workspacePage = new();
    private UIElement mainWorkspace = null!, responsePage = null!;
    internal JsonObject ResponseData = null!;
    internal SectionResponseResult? ResponseCalculation;
    internal InputForm ResponseForm = null!;
    internal readonly ComboBox ResponsePhase = new(), ResponseFiber = new();
    internal readonly Slider ResponsePoint = new() { Minimum = 0, IsSnapToTickEnabled = true, TickFrequency = 1, Margin = new Thickness(12, 3, 12, 3) };
    private readonly BridgeResponsePlot responsePlot = new(), fiberPlot = new() { CurveBrush = Ui.Brush("#A35320") };
    private readonly ContentControl responseTable = new();
    private readonly TextBlock responseNotice = Ui.Text(BridgeResponsePresentation.NoCurve, 12, true), responsePointInfo = Ui.Text("", 12);
    private readonly RecalculationScheduler responseRecalculation = new();
    private CancellationTokenSource? responseCancellation;
    private Button responseRun = null!, responseExport = null!;
    private int responseRevision;
    private bool responseBuilding, responseBusy;

    private UIElement BuildResponsePage()
    {
        ResponseData = BridgeSheetPresentation.PrepareResponse(Data);
        ResponseForm = new InputForm(ResponseData, CompositeSectionFields.Curve, _ => { RefreshResponseFields(); InvalidateResponse(); Modified?.Invoke(); }, true, true);
        foreach (string key in CompositeSectionFields.CurveRawKeys) ResponseForm.Editors[key].SetValue(NumericPresentation.EnabledProperty, false);
        ResponsePhase.Margin = new Thickness(2, 4, 2, 8); ResponsePhase.SelectionChanged += (_, _) => { if (responseBuilding) return; ResponseData["fase"] = Math.Max(0, ResponsePhase.SelectedIndex); InvalidateResponse(); Modified?.Invoke(); };
        ResponseFiber.Margin = new Thickness(8, 3, 8, 8); ResponseFiber.SelectionChanged += (_, _) => RefreshResponsePlots();
        ResponsePoint.ValueChanged += (_, _) => RefreshResponsePlots();
        responseRun = Ui.Button("Calcola curva", async () => await CalculateResponseAsync(), primary: true);
        responseExport = Ui.Button("Esporta punti e fibre CSV…", ExportResponse, inspection: true); responseExport.IsEnabled = false;
        var inputs = Panel("Percorso di deformazione", Scroll(Ui.Stack(ResponseForm, ResponsePhase,
            Ui.Text("Rampa con segno a partire dallo stato iniziale: il punto finale è iniziale + Δ. N positivo a trazione. κ positivo comprime le fibre a y maggiore. ε è misurata alla quota di riferimento; M alla stessa quota.", 11, color: Ui.Muted),
            Ui.Bar(responseRun, Ui.Button("Annulla", () => responseCancellation?.Cancel())),
            Group("Ipotesi e limiti", Ui.Text("Legami non lineari caratteristici, sezione lorda e perfetta aderenza. Lo storico, se richiesto, viene ricalcolato con questi legami: non si converte uno stato cumulativo o efficace in stato plastico. Usa discretizzazione, opzione di viscosità e «CLS teso (non lineare)» del modulo. Non è una verifica SLU di classe 4. L'arresto può indicare un limite costitutivo o un problema di equilibrio: non certifica il collasso.\n\nPer N–ε la curvatura resta fissata: il momento risultante è una reazione. Per M–κ resta fissato N. Le curve σ–ε usano la deformazione meccanica della fibra, con memoria plastica.", 11), true))));
        var graphs = new Grid(); graphs.ColumnDefinitions.Add(new()); graphs.ColumnDefinitions.Add(new());
        var leftPlot = Panel("Risposta della sezione", responsePlot); leftPlot.Margin = new Thickness(0, 0, 6, 0);
        var rightPlot = Panel("Tensione e deformazione della fibra", Ui.Dock(fiberPlot, ResponseFiber)); Grid.SetColumn(rightPlot, 1);
        graphs.Children.Add(leftPlot); graphs.Children.Add(rightPlot);
        var upper = Ui.Dock(graphs, Ui.Stack(responseNotice), Ui.Stack(ResponsePoint, responsePointInfo));
        var lower = Panel("Punti accettati del percorso", Ui.Dock(responseTable, bottom: Ui.Bar(responseExport)), "ε in µε · κ in 1/m · forze in kN · momenti in kNm · tensioni in MPa");
        var right = Split(upper, lower, "curve_righe", true, .65, 300, 170);
        var body = Split(inputs, right, "curve_colonne", false, .27, 300, 720); body.Margin = new Thickness(12, 10, 12, 0);
        RefreshResponseFields(); return body;
    }
    private void RefreshResponseFields()
    {
        foreach (string key in ResponseForm.Editors.Keys) ResponseForm.ShowField(key, BridgeResponsePresentation.FieldShown(key, ResponseData));
        ResponsePhase.Visibility = BridgeResponsePresentation.PhaseShown(ResponseData) ? Visibility.Visible : Visibility.Collapsed;
    }
    private void EnterResponsePage()
    {
        responseBuilding = true;
        ResponsePhase.ItemsSource = BridgeResponsePresentation.Phases(Data);
        ResponsePhase.SelectedIndex = Math.Clamp((int)ResponseData.D("fase"), 0, Math.Max(0, ResponsePhase.Items.Count - 1));
        ResponseData["fase"] = Math.Max(0, ResponsePhase.SelectedIndex); responseBuilding = false;
    }
    private void InvalidateResponse()
    {
        responseRevision++; responseRecalculation.Invalidate(); responseCancellation?.Cancel();
        if (responseExport is not null) responseExport.IsEnabled = false;
        if (ResponseCalculation is not null) responseNotice.Text = BridgeResponsePresentation.Stale;
    }
    internal async Task CalculateResponseAsync()
    {
        if (disposed || responseBusy) return;
        Commit(); ResponseForm.Commit();
        var data = (JsonObject)Data.DeepClone(); var request = (JsonObject)ResponseData.DeepClone();
        int version = responseRevision; responseCancellation?.Dispose(); responseCancellation = new(); var token = responseCancellation.Token;
        var completed = await responseRecalculation.ScheduleAsync(async currentToken =>
        {
            responseBusy = true; responseRun.IsEnabled = false; responseNotice.Text = BridgeResponsePresentation.Running;
            try { return await Task.Run(() => StructuralQueries.BridgeResponse(data, request, currentToken), currentToken); }
            finally { responseBusy = false; if (!disposed) responseRun.IsEnabled = true; }
        }, cancellation: token);
        if (completed.Cancelled && !disposed && version == responseRevision)
            responseNotice.Text = BridgeResponsePresentation.Cancelled;
        completed.TryPublish(current =>
        {
            try
            {
                if (current.Error is not null) throw current.Error;
                var result = current.Value!;
            ResponseCalculation = result; responseExport.IsEnabled = true;
            responseNotice.Text = BridgeResponsePresentation.Calculated(result);
            ResponsePoint.Maximum = Math.Max(0, result.Points.Count - 1); ResponsePoint.Value = ResponsePoint.Maximum;
            var (fibers, selected) = BridgeResponsePresentation.Fibers(result);
            ResponseFiber.ItemsSource = fibers;
            ResponseFiber.SelectedIndex = selected;
            responseTable.Content = Render(BridgeResponsePresentation.Table(result));
            RefreshResponsePlots();
            }
            catch (Exception ex) { responseNotice.Text = BridgeResponsePresentation.Failed(ex.Message); responseExport.IsEnabled = false; }
        });
    }
    private void RefreshResponsePlots()
    {
        if (ResponseCalculation is not { } r || r.Points.Count == 0)
        {
            responsePlot.Points = []; fiberPlot.Points = []; responsePlot.InvalidateVisual(); fiberPlot.InvalidateVisual();
            responsePointInfo.Text = BridgeResponsePresentation.NoPoints; return;
        }
        int selected = Math.Clamp((int)ResponsePoint.Value, 0, r.Points.Count - 1);
        var section = BridgeResponsePresentation.Section(r);
        responsePlot.XLabel = section.XLabel; responsePlot.YLabel = section.YLabel;
        responsePlot.Points = section.Points.Select(p => new Point(p.X, p.Y)).ToArray();
        responsePlot.Selected = selected; responsePlot.InvalidateVisual();
        string? fiber = (ResponseFiber.SelectedItem as BridgeFiberChoice)?.Id;
        responsePointInfo.Text = BridgeResponsePresentation.PointInfo(r, selected, fiber);
        fiberPlot.Points = []; fiberPlot.XLabel = BridgeResponsePresentation.FiberXLabel; fiberPlot.YLabel = BridgeResponsePresentation.FiberYLabel;
        if (fiber is not null && BridgeResponsePresentation.Fiber(r, selected, fiber) is { } chart)
            fiberPlot.Points = chart.Points.Select(p => new Point(p.X, p.Y)).ToArray();
        fiberPlot.Selected = selected; fiberPlot.InvalidateVisual();
    }
    private void ExportResponse()
    {
        if (ResponseCalculation is not { } result || !responseExport.IsEnabled) return;
        var dialog = new SaveFileDialog { Filter = "CSV|*.csv", FileName = BridgeResponsePresentation.CsvFileName };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) File.WriteAllText(dialog.FileName, BridgeEditor.ResponseCsv(result), new UTF8Encoding(true));
    }
}
