using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.Application.Walls;
using Anthea.Application.Presentation;
using Anthea.Presentation.Modules.RetainingWall;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    internal readonly GlobalStabilityDrawing GlobalDrawing = new() { Height = 390 };
    internal readonly ComboBox GlobalCombination = new() { MinWidth = 250 };
    private CheckBox globalFullProfile = null!;
    internal JsonGrid GlobalLayers = null!, GlobalMatrix = null!;
    private readonly ContentControl globalMatrixHost = new();
    private readonly TextBlock globalMessage = Ui.Text("", 11);
    internal GlobalStabilityDrawing? globalPreview;
    private SlopeResult? independentGlobal;
    private JsonObject? independentGlobalInput;
    private string? independentGlobalError;
    internal SlopeResult? GlobalResult => independentGlobal ?? Calculation?.GlobalStability;
    private SlopeCaseResult? GlobalCase => GlobalResult?.Cases.FirstOrDefault(c => c.Factors.Name == GlobalCombination.SelectedItem as string);
    private FrameworkElement GlobalCard() { var card = Group("Stabilità globale · profilo, strati e ricerca", BuildGlobalCard(), false); Cards["Stabilità globale"] = card; return card; }

    internal void PrepareGlobal()
    {
        Commit(); WallModel.PrepareGlobalProfile(Data); Data["global_stability"]!["enabled"] = true; BuildInputs(); UpdateFields(); Changed();
        Cards["Stabilità globale"].IsExpanded = true; ScrollGlobalSetup();
    }
    private FrameworkElement BuildGlobalCard()
    {
        foreach (FrameworkElement shared in new FrameworkElement[] { globalMatrixHost, globalMessage, globalReadiness, globalSearchSummary }) if (shared.Parent is Panel parent) parent.Children.Remove(shared);
        var g = Data["global_stability"]!.AsObject();
        Anthea.Application.Presentation.WallSheetPresentation.PrepareGlobalSettings(Data);
        var form = Form("global", RetainingWallFields.Global, g, key => { if (key != "profile_confirmed" && key != "enabled") InvalidateGlobalConfirmation(); });
        foreach (var group in RetainingWallFields.GlobalGroups(g)) form.GroupFields(group.Title, [.. group.Keys], group.Expanded);
        FrameworkElement Points(string key, string title)
        {
            var array = g.Array(key); var grid = GridFor(RetainingWallFields.PointColumns, array, _ => { InvalidateGlobalConfirmation(); RefreshGlobalLayerDisplays(); }, height: 120);
            return Group(title, Ui.Stack(grid, Ui.Bar(Ui.Button("+ Punto", () => { AddRow(grid, array, RetainingWallPresentation.NewPoint(), _ => { InvalidateGlobalConfirmation(); RefreshGlobalLayerDisplays(); }); InvalidateGlobalConfirmation(); Changed(); }),
                Ui.Button("− Punto", () => { if (grid.SelectedItem is not JsonRow row) return; array.Remove(row.Values); grid.Rows.Remove(row); InvalidateGlobalConfirmation(); Changed(); }))), false);
        }
        var rearEditor = BuildGlobalSoilEditor(false); var valleyEditor = BuildGlobalSoilEditor(true);
        var searchMode = Form("global_search_mode", RetainingWallFields.GlobalSearchMode, g);
        var search = Form("global_search", RetainingWallFields.GlobalSearch, g);
        globalPreview = new() { Data = Data, Height = 320, FocusCritical = false, ShowSearch = true };
        BuildGlobalMatrix();
        var valleyCard = Group("Colonna di VALLE", valleyEditor); Cards["Strati globali valle"] = valleyCard;
        return Ui.Stack(Ui.Text("Verifica il possibile scivolamento del muro insieme al terreno sottostante.", 12), globalReadiness,
            Ui.Bar(Ui.Button("Prepara dal muro", PrepareGlobal), Ui.Button("Calcola globale", async () => await CalculateGlobalAsync(), inspection: true)),
            Ui.Text("Prepara dal muro sostituisce la proposta con i dati locali. Non aggiunge terreni profondi non conosciuti.", 10, color: Ui.Muted),
            form, globalPreview,
            Ui.Text("1 · Controlla gli strati, anche sotto la fondazione", 13, true),
            Ui.Text("Inserisci gli spessori dall’alto verso il basso. Il fondo y si calcola da solo: 0 = piano di posa; −5 = 5 m sotto. Seleziona uno strato per modificarne le proprietà.", 11, color: Ui.Muted),
            Group("Colonna di MONTE / profilo unico", rearEditor), valleyCard,
            Group("2 · Rilievo e falda · modifica se diversi dalla proposta", Ui.Stack(
                Ui.Text("Origine (0;0) al bordo di valle del piano di posa. x cresce verso monte, y verso l’alto. Il profilo proposto è orizzontale sui due lati; adattarlo al rilievo.", 11),
                Points("valley", "Superficie a valle · punti x, y"), Points("uphill", "Superficie a monte · punti x, y"), Points("water", "Linea di falda · quote y, non profondità")), false),
            Ui.Text("3 · Ricerca delle superfici", 13, true), searchMode, globalSearchSummary,
            Group("Limiti e precisione · dettagli modificabili", Ui.Stack(
                Ui.Text("In Automatica i limiti seguono il profilo e gli strati noti, fino a 2(H+t). Passa ad Assegnata per modificarli. Il minimo sul bordo richiede una ricerca più ampia, sempre coperta dalle indagini.", 11, color: Ui.Muted), search), false),
            Group("Combinazioni globali · A2–M2–R2 in statica", Ui.Stack(Ui.Bar(Ui.Button("Genera globali", GenerateGlobalMatrix), Ui.Button("Conferma matrice globale", () => { Commit(); RetainingWallPresentation.ConfirmGlobalMatrix(Data); Changed(); })), globalMessage, globalMatrixHost), false),
            Ui.Text("Dopo il controllo, spunta la conferma dei dati in alto e calcola. Il risultato si apre in Verifiche → Stabilità globale.", 11),
            Ui.Bar(Ui.Button("Calcola globale", async () => await CalculateGlobalAsync(), inspection: true), Ui.Button("Word globale", ExportGlobalWord, inspection: true)),
            Group("Metodo e coefficienti utilizzati", Ui.Text(WallModel.GlobalHelp, 11), false));
    }
    internal void GenerateGlobalMatrix()
    {
        Commit();
        try { RetainingWallPresentation.GenerateGlobalMatrix(Data); BuildGlobalMatrix(); Changed(); }
        catch (ArgumentException ex) { globalMessage.Text = ex.Message; }
    }
    private void BuildGlobalMatrix()
    {
        if (GlobalMatrix is not null) grids.Remove(GlobalMatrix);
        var g = Data["global_stability"]!;
        GlobalMatrix = new JsonGrid(RetainingWallFields.GlobalMatrix(Data)) { Height = 180, RowHeight = 31 }; grids.Add(GlobalMatrix); CompactHeaders(GlobalMatrix);
        foreach (var row in g.Array("combinations"))
        {
            var flat = RetainingWallPresentation.GlobalMatrixRow(Data, row!);
            GlobalMatrix.Rows.Add(new JsonRow(flat, key =>
            {
                RetainingWallPresentation.GlobalMatrixEdit(Data, row!, flat, key);
                globalMessage.Text = RetainingWallPresentation.GlobalMatrixEdited; Changed();
            }));
        }
        globalMatrixHost.Content = GlobalMatrix; globalMessage.Text = RetainingWallPresentation.GlobalMatrixStatus(Data);
    }
    private void RefreshGlobal()
    {
        RefreshGlobalSearch();
        RefreshGlobalGuidance();
        RefreshGlobalSoilDetails();
        // The rules of the global stability (RetainingWallPresentation.GlobalRules, F5.9): search limits, deep layers, valley column, global form.
        ApplyRules(RetainingWallPresentation.GlobalRules(Data));
        GlobalDrawing.Data = Data; GlobalDrawing.Result = GlobalResult; GlobalDrawing.Case = GlobalCase; GlobalDrawing.InvalidateVisual();
        if (globalPreview is not null) { globalPreview.Data = Data; globalPreview.Result = GlobalResult; globalPreview.Case = GlobalCase; globalPreview.InvalidateVisual(); }
    }
    private FrameworkElement GlobalResultsPanel()
    {
        if (GlobalResult is not { } result) return Ui.Text(independentGlobalError ?? Calculation?.GlobalError ?? RetainingWallPresentation.GlobalNoResult, 13);
        var table = Table([.. RetainingWallPresentation.GlobalColumns], RetainingWallPresentation.GlobalRows(result));
        table.Height = 155; table.SelectionChanged += (_, _) => { if (table.SelectedIndex >= 0) GlobalCombination.SelectedItem = result.Cases[table.SelectedIndex].Factors.Name; };
        var panel = Ui.Stack(Ui.Text(GlobalResultExplanation, 12), Ui.Button("Modifica profilo, strati e ricerca", ShowGlobalSetup, inspection: true), table, Ui.Text(Slope.Formula, 11));
        if (GlobalCase?.Critical is { } critical)
        {
            panel.Children.Add(Ui.Text(RetainingWallPresentation.GlobalCriticalText(GlobalCase, critical), 11));
            var slices = Table([.. RetainingWallPresentation.SliceColumns], RetainingWallPresentation.SliceRows(critical));
            slices.Height = 240; slices.SelectionChanged += (_, _) => { GlobalDrawing.SelectedSlice = slices.SelectedIndex; GlobalDrawing.InvalidateVisual(); }; panel.Children.Add(slices);
        }
        panel.Children.Add(Ui.Text(RetainingWallPresentation.GlobalNotes(result), 11, color: Ui.Muted)); return panel;
    }
    internal Task CalculateGlobalAsync() => scheduledCalculation = CalculateGlobalScheduledAsync();
    private async Task CalculateGlobalScheduledAsync()
    {
        if (disposed) return; Commit();
        var snapshot = (JsonObject)Data.DeepClone();
        var completed = await recalculation.ScheduleAsync(async token =>
        {
            Busy = true; status.Text = RetainingWallPresentation.GlobalRunning; independentGlobal = null; independentGlobalInput = null; independentGlobalError = null;
            try { return await Task.Run(() => Anthea.Application.Modules.StructuralQueries.WallGlobal(snapshot, token), token); }
            finally { Busy = false; }
        });
        completed.TryPublish(current =>
        {
            try
            {
                if (current.Error is not null) throw current.Error;
                var result = current.Value!; independentGlobal = result; independentGlobalInput = snapshot;
                GlobalCombination.ItemsSource = result.Cases.Select(c => c.Factors.Name).ToArray(); GlobalCombination.SelectedItem = RetainingWallPresentation.WorstGlobalCombination(result);
                ViewMode.SelectedItem = "Stabilità globale"; CheckFilter.SelectedItem = "Stabilità globale"; Pages.SelectedIndex = 1; RefreshGlobal(); ShowCheckTable();
                status.Text = RetainingWallPresentation.GlobalUpdated(result.Cases.Length);
            }
            catch (Exception ex) { independentGlobalError = ex.Message; status.Text = RetainingWallPresentation.GlobalFailed(ex.Message); RefreshGlobal(); ShowCheckTable(); }
        });
    }
    private void ExportGlobalWord()
    {
        if (GlobalResult is not { } result || Busy) { MessageBox.Show(Window.GetWindow(this), RetainingWallPresentation.WaitBeforeGlobalWord); return; }
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Relazione Word e PDF affiancati|*.docx", FileName = "Muro_stabilita_globale.docx" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        var input = independentGlobalInput ?? Calculation!.Input;
        var drawing = new GlobalStabilityDrawing { Data = input, Result = result, Case = GlobalCase, Width = 1100, Height = 460 };
        drawing.Measure(new Size(1100, 460)); drawing.Arrange(new Rect(0, 0, 1100, 460)); drawing.UpdateLayout();
        Anthea.Reports.ReportFiles.Write(dialog.FileName, WallExports.CreateGlobal(input, result, new("Stabilità globale · superficie visualizzata", Ui.DocumentSnapshot(drawing), 1100d / 460)), "Stabilità globale");
    }
}
