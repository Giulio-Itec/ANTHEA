using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.Calculations.Geotechnics;

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
        Commit(); RetainingWall.PrepareGlobalProfile(Data); Data["global_stability"]!["enabled"] = true; BuildInputs(); UpdateFields(); Changed();
        Cards["Stabilità globale"].IsExpanded = true; ScrollGlobalSetup();
    }
    private FrameworkElement BuildGlobalCard()
    {
        foreach (FrameworkElement shared in new FrameworkElement[] { globalMatrixHost, globalMessage, globalReadiness, globalSearchSummary }) if (shared.Parent is Panel parent) parent.Children.Remove(shared);
        var g = Data["global_stability"]!.AsObject();
        g["valley_layers"] ??= new JsonArray(); g["soil_mode"] ??= "Profilo unico";
        g["soil_split_x"] ??= Data["geometry"].D("toe") + Data["geometry"].D("stem_base");
        // Existing documents retain their exact search limits until the user chooses automatic mode.
        g["search_mode"] ??= "Assegnata";
        var form = Form("global", [new("enabled", "Attiva la verifica globale", Bool: true),
            new("condition", "Condizione del terreno", Choices: ["Drenata", "Non drenata"]),
            new("profile_confirmed", "Ho controllato profilo, strati e falda del sito", Bool: true),
            new("water_enabled", "Considera la falda", Bool: true), new("seismic", "Includi il sisma SLV", Bool: true),
            new("seismic_source", "Coefficienti sismici", Choices: ["Da sito · βs=0,38", "kh e kv assegnati"]),
            new("kh", "Coefficiente orizzontale", "−"), new("kv", "Modulo coefficiente verticale", "−"),
            new("soil_mode", "Stratigrafia globale", Choices: ["Profilo unico", "Due colonne"]), new("soil_split_x", "Confine valle / monte", "m")], g,
            key => { if (key != "profile_confirmed" && key != "enabled") InvalidateGlobalConfirmation(); });
        form.GroupFields("Falda e sisma · ripresi dal muro, modificabili", ["water_enabled", "seismic", "seismic_source", "kh", "kv"], g.B("water_enabled") || g.B("seismic"));
        form.GroupFields("Modello del terreno · dettagli", ["soil_mode", "soil_split_x"], false);
        FrameworkElement Points(string key, string title)
        {
            var array = g.Array(key); var grid = GridFor([new("x", "x [m]"), new("y", "y [m]")], array, _ => { InvalidateGlobalConfirmation(); RefreshGlobalLayerDisplays(); }, height: 120);
            return Group(title, Ui.Stack(grid, Ui.Bar(Ui.Button("+ Punto", () => { AddRow(grid, array, J.Obj(("x", ""), ("y", "")), _ => { InvalidateGlobalConfirmation(); RefreshGlobalLayerDisplays(); }); InvalidateGlobalConfirmation(); Changed(); }),
                Ui.Button("− Punto", () => { if (grid.SelectedItem is not JsonRow row) return; array.Remove(row.Values); grid.Rows.Remove(row); InvalidateGlobalConfirmation(); Changed(); }))), false);
        }
        var rearEditor = BuildGlobalSoilEditor(false); var valleyEditor = BuildGlobalSoilEditor(true);
        var searchMode = Form("global_search_mode", [new("search_mode", "Area di ricerca", Choices: ["Automatica", "Assegnata"])], g);
        var search = Form("global_search", [new("exit_min", "Uscita a valle · x minima", "m"), new("exit_max", "Uscita a valle · x massima", "m"), new("entry_min", "Ingresso a monte · x minima", "m"), new("entry_max", "Ingresso a monte · x massima", "m"),
            new("depth_min", "Profondità minima sotto la fondazione", "m"), new("depth_max", "Profondità massima sotto la fondazione", "m"), new("grid", "Nodi per direzione"), new("slices", "Conci iniziali"), new("refinements", "Raffinamenti locali")], g);
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
            Group("Combinazioni globali · A2–M2–R2 in statica", Ui.Stack(Ui.Bar(Ui.Button("Genera globali", GenerateGlobalMatrix), Ui.Button("Conferma matrice globale", () => { Commit(); g["combination_mode"] = "Personalizzate"; g["combination_signature"] = RetainingWall.GlobalSignature(Data); Changed(); })), globalMessage, globalMatrixHost), false),
            Ui.Text("Dopo il controllo, spunta la conferma dei dati in alto e calcola. Il risultato si apre in Verifiche → Stabilità globale.", 11),
            Ui.Bar(Ui.Button("Calcola globale", async () => await CalculateGlobalAsync(), inspection: true), Ui.Button("Word globale", ExportGlobalWord, inspection: true)),
            Group("Metodo e coefficienti utilizzati", Ui.Text(RetainingWall.GlobalHelp, 11), false));
    }
    internal void GenerateGlobalMatrix()
    {
        Commit();
        try { var g = Data["global_stability"]!; g["combinations"] = RetainingWall.GenerateGlobalCombinations(Data); g["combination_mode"] = "Automatiche"; BuildGlobalMatrix(); Changed(); }
        catch (ArgumentException ex) { globalMessage.Text = ex.Message; }
    }
    private void BuildGlobalMatrix()
    {
        if (GlobalMatrix is not null) grids.Remove(GlobalMatrix);
        var g = Data["global_stability"]!;
        var fields = new List<Field> { new("enabled", "Usa", Bool: true), new("name", "Combinazione"), new("state", "Stato", Choices: ["SLU", "SISMA", "ECCEZIONALE"]), new("soil", "γ terra"), new("wall", "γ muro"), new("mphi", "γMφ"), new("mc", "γMc′"), new("mcu", "γMcu"), new("r", "γR"), new("kh", "kh"), new("kv", "kv") };
        foreach (var action in Data.Array("actions")) fields.Add(new("a_" + action.S("id"), action.S("name")));
        GlobalMatrix = new JsonGrid(fields) { Height = 180, RowHeight = 31 }; grids.Add(GlobalMatrix); CompactHeaders(GlobalMatrix);
        foreach (var row in g.Array("combinations"))
        {
            var flat = (JsonObject)row!.DeepClone(); flat.Remove("coefficients"); foreach (var action in Data.Array("actions")) flat["a_" + action.S("id")] = row["coefficients"]?[action.S("id")]?.DeepClone();
            GlobalMatrix.Rows.Add(new JsonRow(flat, key =>
            {
                if (g.S("combination_mode") == "Automatiche") g["combination_signature"] = RetainingWall.GlobalSignature(Data);
                g["combination_mode"] = "Personalizzate";
                if (key.StartsWith("a_")) row["coefficients"]![key[2..]] = flat[key]?.DeepClone(); else row[key] = flat[key]?.DeepClone();
                globalMessage.Text = "Matrice globale personalizzata: fanno fede i coefficienti di ogni riga."; Changed();
            }));
        }
        globalMatrixHost.Content = GlobalMatrix; globalMessage.Text = g.S("combination_mode") + " · coefficienti indipendenti dalle verifiche locali";
    }
    private void RefreshGlobal()
    {
        RefreshGlobalSearch();
        RefreshGlobalGuidance();
        RefreshGlobalSoilDetails();
        if (Cards.TryGetValue("Strati globali valle", out var valleyCard)) valleyCard.Visibility = Data["global_stability"].S("soil_mode") == "Due colonne" ? Visibility.Visible : Visibility.Collapsed;
        GlobalDrawing.Data = Data; GlobalDrawing.Result = GlobalResult; GlobalDrawing.Case = GlobalCase; GlobalDrawing.InvalidateVisual();
        if (globalPreview is not null) { globalPreview.Data = Data; globalPreview.Result = GlobalResult; globalPreview.Case = GlobalCase; globalPreview.InvalidateVisual(); }
        if (Forms.TryGetValue("global", out var form))
        {
            form.ShowField("soil_split_x", Data["global_stability"].S("soil_mode") == "Due colonne");
            bool manual = Data["global_stability"].B("seismic") && Data["global_stability"].S("seismic_source") == "kh e kv assegnati";
            form.ShowField("seismic_source", Data["global_stability"].B("seismic")); form.ShowField("kh", manual); form.ShowField("kv", manual);
        }
    }
    private FrameworkElement GlobalResultsPanel()
    {
        if (GlobalResult is not { } result) return Ui.Text(independentGlobalError ?? Calculation?.GlobalError ?? "Attivare la stabilità globale in Input → Terreno e completare profilo, strati e ricerca.", 13);
        var table = Table(["Caso", "F", "γR", "η=γR/F", "xc [m]", "yc [m]", "R [m]", "Risolte / provate", "Esito"], result.Cases.Select(c => new[] { c.Factors.Name, c.Critical is { } s ? F(s.Factor) : "—", F(c.Factors.R), c.Critical is { } q ? q.Ratio.ToString("0.000", It) : "—", c.Critical is { } a ? F(a.Circle.X) : "—", c.Critical is { } b ? F(b.Circle.Y) : "—", c.Critical is { } e ? F(e.Circle.Radius) : "—", $"{c.Solved}/{c.Tried}", c.Status }));
        table.Height = 155; table.SelectionChanged += (_, _) => { if (table.SelectedIndex >= 0) GlobalCombination.SelectedItem = result.Cases[table.SelectedIndex].Factors.Name; };
        var panel = Ui.Stack(Ui.Text(GlobalResultExplanation, 12), Ui.Button("Modifica profilo, strati e ricerca", ShowGlobalSetup, inspection: true), table, Ui.Text(Slope.Formula, 11));
        if (GlobalCase?.Critical is { } critical)
        {
            var c = GlobalCase;
            panel.Children.Add(Ui.Text($"{c.Factors.Name} · kh={c.Factors.Kh:0.#####}, kv={c.Factors.Kv:0.#####} · γMφ/c′/cu={c.Factors.MPhi}/{c.Factors.MC}/{c.Factors.MCu} · ΣR={critical.Resistance:0.###} kN/m · D={critical.Driving:0.###} kN/m · iterazioni {critical.Iterations}, residuo {critical.Residual:G3}. Superfici senza soluzione: {c.NumericalFailures}. {c.Status}", 11));
            var slices = Table(["Concio", "x₀ / x₁ [m]", "y base [m]", "α [°]", "Terreno", "W terra / muro", "Vext / Hext", "u [kPa]", "φd / cd", "N′ [kN/m]", "R / T [kN/m]", "mα"], critical.Slices.Select(s => new[] { s.Index.ToString(), F(s.Left) + " / " + F(s.Right), F(s.BaseY), F(s.Alpha), s.Soil, F(s.SoilWeight) + " / " + F(s.BodyWeight), F(s.VerticalLoad) + " / " + F(s.HorizontalLoad), F(s.U), F(s.Phi) + " / " + F(s.Cohesion), F(s.NormalEffective), F(s.Resistance) + " / " + F(s.Mobilized), s.MAlpha.ToString("0.0000", It) }));
            slices.Height = 240; slices.SelectionChanged += (_, _) => { GlobalDrawing.SelectedSlice = slices.SelectedIndex; GlobalDrawing.InvalidateVisual(); }; panel.Children.Add(slices);
        }
        panel.Children.Add(Ui.Text(string.Join("\n", result.Notes.Skip(1)), 11, color: Ui.Muted)); return panel;
    }
    internal async Task CalculateGlobalAsync()
    {
        if (disposed) return; Commit(); timer.Stop();
        while (Busy) await running;
        if (disposed) return;
        int request = revision; var snapshot = (JsonObject)Data.DeepClone(); cancellation?.Dispose(); cancellation = new(); var token = cancellation.Token;
        Busy = true; status.Text = "Ricerca delle superfici globali…"; independentGlobal = null; independentGlobalInput = null; independentGlobalError = null;
        running = Run(); await running;
        async Task Run()
        {
            try
            {
                var result = await Task.Run(() => RetainingWall.CalculateGlobal(snapshot, token));
                if (disposed || request != revision) return;
                independentGlobal = result; independentGlobalInput = snapshot;
                GlobalCombination.ItemsSource = result.Cases.Select(c => c.Factors.Name).ToArray(); GlobalCombination.SelectedItem = result.Cases.OrderByDescending(c => c.Critical?.Ratio ?? double.PositiveInfinity).First().Factors.Name;
                ViewMode.SelectedItem = "Stabilità globale"; CheckFilter.SelectedItem = "Stabilità globale"; Pages.SelectedIndex = 1; RefreshGlobal(); ShowCheckTable();
                status.Text = "Stabilità globale aggiornata · " + result.Cases.Length + " combinazioni. Le verifiche locali hanno un calcolo indipendente.";
            }
            catch (OperationCanceledException) { }
            catch (ArgumentException ex) { if (!disposed && request == revision) { independentGlobalError = ex.Message; status.Text = "Stabilità globale: " + ex.Message; RefreshGlobal(); ShowCheckTable(); } }
            finally { Busy = false; if (!disposed && request != revision) timer.Start(); }
        }
    }
    private void ExportGlobalWord()
    {
        if (GlobalResult is not { } result || Busy) { MessageBox.Show(Window.GetWindow(this), "Completare prima il calcolo globale."); return; }
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Relazione Word|*.docx", FileName = "Muro_stabilita_globale.docx" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        var input = independentGlobalInput ?? Calculation!.Input;
        var drawing = new GlobalStabilityDrawing { Data = input, Result = result, Case = GlobalCase, Width = 1100, Height = 460 };
        drawing.Measure(new Size(1100, 460)); drawing.Arrange(new Rect(0, 0, 1100, 460)); drawing.UpdateLayout();
        X.Core.Archivio.ScriviAtomico(dialog.FileName, X.Core.ReportRetainingWall.CreateGlobal(input, result, new("Stabilità globale · superficie visualizzata", Ui.Snapshot(drawing), 1100d / 460)));
    }
}
