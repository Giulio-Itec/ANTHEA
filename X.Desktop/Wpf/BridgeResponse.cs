using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using GPC.Checkers.CompositeBridge.History;
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
    private readonly TextBlock responseNotice = Ui.Text("Nessuna curva calcolata", 12, true), responsePointInfo = Ui.Text("", 12);
    private CancellationTokenSource? responseCancellation;
    private Button responseRun = null!, responseExport = null!;
    private int responseRevision;
    private bool responseBuilding, responseBusy;

    private UIElement BuildResponsePage()
    {
        if (Data["curve_sezione"] is not JsonObject) Data["curve_sezione"] = BridgeSection.ResponseDefaults();
        ResponseData = Data["curve_sezione"]!.AsObject();
        foreach (var pair in BridgeSection.ResponseDefaults()) if (!ResponseData.ContainsKey(pair.Key)) ResponseData[pair.Key] = pair.Value?.DeepClone();
        ResponseForm = new InputForm(ResponseData, [new("tipo", "Curva", Choices: BridgeSection.ResponseModes, Wide: true),
            new("origine", "Stato di partenza", Choices: BridgeSection.ResponseOrigins, Wide: true),
            new("y", "Quota di riferimento", "mm"), new("n_storico", "Mantieni N dello stato iniziale", Bool: true, Wide: true), new("N", "N costante", "kN"),
            new("k_storico", "Mantieni κ dello stato iniziale", Bool: true, Wide: true), new("k", "κ costante", "1/m"),
            new("incremento_k", "Incremento finale Δκ", "1/m"), new("incremento_e", "Incremento finale Δε", "µε"),
            new("punti", "Punti della curva"), new("sottopassi", "Sottopassi per punto")], _ => { RefreshResponseFields(); InvalidateResponse(); Modified?.Invoke(); }, true, true);
        foreach (string key in new[] { "k", "incremento_k" }) ResponseForm.Editors[key].SetValue(NumericPresentation.EnabledProperty, false);
        ResponsePhase.Margin = new Thickness(2, 4, 2, 8); ResponsePhase.SelectionChanged += (_, _) => { if (responseBuilding) return; ResponseData["fase"] = Math.Max(0, ResponsePhase.SelectedIndex); InvalidateResponse(); Modified?.Invoke(); };
        ResponseFiber.Margin = new Thickness(8, 3, 8, 8); ResponseFiber.SelectionChanged += (_, _) => RefreshResponsePlots();
        ResponsePoint.ValueChanged += (_, _) => RefreshResponsePlots();
        responseRun = Ui.Button("Calcola curva", async () => await CalculateResponseAsync(), primary: true);
        responseExport = Ui.Button("Esporta punti e fibre CSV…", ExportResponse, inspection: true); responseExport.IsEnabled = false;
        var inputs = Panel("Percorso di deformazione", Scroll(Ui.Stack(ResponseForm, ResponsePhase,
            Ui.Text("Rampa con segno a partire dallo stato iniziale: il punto finale è iniziale + Δ. N positivo a trazione. κ positivo comprime le fibre a y maggiore. ε è misurata alla quota di riferimento; M alla stessa quota.", 11, color: Ui.Muted),
            Ui.Bar(responseRun, Ui.Button("Annulla", () => responseCancellation?.Cancel())),
            Group("Ipotesi e limiti", Ui.Text("Legami non lineari caratteristici, sezione lorda e perfetta aderenza. Lo storico, se richiesto, viene ricalcolato con questi legami: non si converte uno stato cumulativo o efficace in stato plastico. Usa discretizzazione e opzione di viscosità del modulo. Non è una verifica SLU di classe 4. L'arresto può indicare un limite costitutivo o un problema di equilibrio: non certifica il collasso.\n\nPer N–ε la curvatura resta fissata: il momento risultante è una reazione. Per M–κ resta fissato N. Le curve σ–ε usano la deformazione meccanica della fibra, con memoria plastica.", 11), true))));
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
        bool mc = ResponseData.S("tipo") == BridgeSection.ResponseModes[0], history = ResponseData.S("origine") == BridgeSection.ResponseOrigins[1];
        ResponseForm.ShowField("n_storico", mc); ResponseForm.ShowField("N", mc && !ResponseData.B("n_storico"));
        ResponseForm.ShowField("k_storico", !mc); ResponseForm.ShowField("k", !mc && !ResponseData.B("k_storico"));
        ResponseForm.ShowField("incremento_k", mc); ResponseForm.ShowField("incremento_e", !mc);
        ResponsePhase.Visibility = history ? Visibility.Visible : Visibility.Collapsed;
    }
    private void EnterResponsePage()
    {
        responseBuilding = true;
        ResponsePhase.ItemsSource = Data.Array("fasi").OfType<JsonObject>().Where(p => p.B("attiva")).Select((p, i) => $"{i + 1:00} · Dopo {p.S("nome")}").ToArray();
        ResponsePhase.SelectedIndex = Math.Clamp((int)ResponseData.D("fase"), 0, Math.Max(0, ResponsePhase.Items.Count - 1));
        ResponseData["fase"] = Math.Max(0, ResponsePhase.SelectedIndex); responseBuilding = false;
    }
    private void InvalidateResponse()
    {
        responseRevision++; responseCancellation?.Cancel();
        if (responseExport is not null) responseExport.IsEnabled = false;
        if (ResponseCalculation is not null) responseNotice.Text = "Dati modificati · curva precedente da ricalcolare";
    }
    internal async Task CalculateResponseAsync()
    {
        if (disposed || responseBusy) return;
        Commit(); ResponseForm.Commit();
        var data = (JsonObject)Data.DeepClone(); var request = (JsonObject)ResponseData.DeepClone();
        int version = responseRevision; responseCancellation?.Dispose(); responseCancellation = new(); var token = responseCancellation.Token;
        responseBusy = true; responseRun.IsEnabled = false; responseNotice.Text = "Calcolo del percorso e dello storico di partenza…";
        try
        {
            var result = await Task.Run(() => BridgeSection.CalculateResponse(data, request, token), token);
            if (disposed || version != responseRevision) return;
            ResponseCalculation = result; responseExport.IsEnabled = true;
            responseNotice.Text = $"{result.Points.Count} stati accettati · " + result.Message;
            ResponsePoint.Maximum = Math.Max(0, result.Points.Count - 1); ResponsePoint.Value = ResponsePoint.Maximum;
            var fibers = result.Points.FirstOrDefault()?.State.Fibers.Where(f => f.Active).ToArray() ?? [];
            ResponseFiber.ItemsSource = fibers.Select(f => new FiberChoice(f.Fiber.Id, $"{ComponentName(f.ComponentId)} · x = {f.Fiber.X:F1} · y = {f.Fiber.Y:F2} mm")).ToArray();
            ResponseFiber.SelectedIndex = fibers.Length == 0 ? -1 : Array.FindIndex(fibers, f => f.Fiber.Y == fibers.Max(a => a.Fiber.Y));
            responseTable.Content = ResultTable(["Punto", "ε rif", "κ", "N", "M rif", "εmecc min", "εmecc max", "σ min", "σ max", "Residuo N [N]"],
                result.Points.Select(p => new[] { p.Index.ToString(), F(p.ReferenceStrain * 1e6), E(p.State.TotalPlane.Curvature * 1000), F(p.State.N / 1000), F(p.MomentAtReference / 1e6),
                    F(p.State.Fibers.Where(f => f.Active).Min(f => f.MechanicalStrain) * 1e6), F(p.State.Fibers.Where(f => f.Active).Max(f => f.MechanicalStrain) * 1e6),
                    F(p.State.Fibers.Where(f => f.Active).Min(f => f.Stress)), F(p.State.Fibers.Where(f => f.Active).Max(f => f.Stress)), E(p.State.ForceResidual) }));
            RefreshResponsePlots();
        }
        catch (OperationCanceledException) { if (!disposed) responseNotice.Text = "Calcolo annullato · eventuale curva precedente da ricalcolare"; }
        catch (Exception ex) { responseNotice.Text = "Curva non calcolata: " + ex.Message; responseExport.IsEnabled = false; }
        finally { responseBusy = false; responseRun.IsEnabled = true; }
    }
    private sealed record FiberChoice(string Id, string Label) { public override string ToString() => Label; }
    private static string ComponentName(string id) => id switch { HBridgeHistoryAnalysis.Concrete => "Calcestruzzo", HBridgeHistoryAnalysis.Rebars => "Armatura", HBridgeHistoryAnalysis.Top => "Piattabanda superiore", HBridgeHistoryAnalysis.Web => "Anima", HBridgeHistoryAnalysis.Bottom => "Piattabanda inferiore", _ => id };
    private void RefreshResponsePlots()
    {
        if (ResponseCalculation is not { } r || r.Points.Count == 0)
        {
            responsePlot.Points = []; fiberPlot.Points = []; responsePlot.InvalidateVisual(); fiberPlot.InvalidateVisual();
            responsePointInfo.Text = "Nessuno stato accettato per questa curva."; return;
        }
        bool mc = r.Options.Control == SectionResponseControl.MomentCurvature; int selected = Math.Clamp((int)ResponsePoint.Value, 0, r.Points.Count - 1);
        responsePlot.XLabel = mc ? "κ [1/m]" : "ε alla quota di riferimento [µε]"; responsePlot.YLabel = mc ? "M rif [kNm]" : "N [kN]";
        responsePlot.Points = r.Points.Select(p => mc ? new Point(p.State.TotalPlane.Curvature * 1000, p.MomentAtReference / 1e6) : new Point(p.ReferenceStrain * 1e6, p.State.N / 1000)).ToArray();
        responsePlot.Selected = selected; responsePlot.InvalidateVisual();
        var p = r.Points[selected]; responsePointInfo.Text = $"Punto {selected} · ε rif {F(p.ReferenceStrain * 1e6)} µε · κ {E(p.State.TotalPlane.Curvature * 1000)} 1/m · N {F(p.State.N / 1000)} kN · M rif {F(p.MomentAtReference / 1e6)} kNm";
        fiberPlot.Points = []; fiberPlot.XLabel = "ε meccanica [µε]"; fiberPlot.YLabel = "σ [MPa]";
        if (ResponseFiber.SelectedItem is FiberChoice choice && p.State.Fibers.Any(f => f.Fiber.Id == choice.Id))
        {
            fiberPlot.Points = r.Points.Select(s => s.State.Fibers.Single(f => f.Fiber.Id == choice.Id)).Select(f => new Point(f.MechanicalStrain * 1e6, f.Stress)).ToArray();
            var f = p.State.Fibers.Single(f => f.Fiber.Id == choice.Id); responsePointInfo.Text += $"\nFibra: σ {F(f.Stress)} MPa · εtot {F(f.TotalStrain * 1e6)} · εgetto {F(f.ActivationStrain * 1e6)} · εimposta {F(f.ImposedStrain * 1e6)} · εmecc {F(f.MechanicalStrain * 1e6)} µε";
            if (f.MaterialState is HistoryPlasticState plastic) responsePointInfo.Text += $" · εpl {F(plastic.PlasticStrain * 1e6)} µε";
        }
        fiberPlot.Selected = selected; fiberPlot.InvalidateVisual();
    }
    private void ExportResponse()
    {
        if (ResponseCalculation is not { } result || !responseExport.IsEnabled) return;
        var dialog = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "Curve_sezione_ponte.csv" };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) File.WriteAllText(dialog.FileName, BridgeSection.ResponseCsv(result), new UTF8Encoding(true));
    }
}
