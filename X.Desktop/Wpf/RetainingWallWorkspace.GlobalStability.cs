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
    private GlobalStabilityDrawing? globalPreview;
    private SlopeResult? independentGlobal;
    private JsonObject? independentGlobalInput;
    private string? independentGlobalError;
    internal SlopeResult? GlobalResult => independentGlobal ?? Calculation?.GlobalStability;
    private SlopeCaseResult? GlobalCase => GlobalResult?.Cases.FirstOrDefault(c => c.Factors.Name == GlobalCombination.SelectedItem as string);
    private FrameworkElement GlobalCard() { var card = Group("Stabilità globale · profilo, strati e ricerca", BuildGlobalCard(), false); Cards["Stabilità globale"] = card; return card; }

    internal void PrepareGlobal()
    {
        Commit(); RetainingWall.PrepareGlobalProfile(Data); BuildInputs(); UpdateFields(); Changed();
        Cards["Stabilità globale"].IsExpanded = true; Cards["Stabilità globale"].BringIntoView();
    }
    private FrameworkElement BuildGlobalCard()
    {
        foreach (FrameworkElement shared in new FrameworkElement[] { globalMatrixHost, globalMessage }) if (shared.Parent is Panel parent) parent.Children.Remove(shared);
        var g = Data["global_stability"]!.AsObject();
        var form = Form("global", [new("enabled", "Calcola stabilità globale", Bool: true), new("condition", "Resistenza del terreno", Choices: ["Drenata", "Non drenata"]),
            new("profile_confirmed", "Profilo e strati controllati per il sito", Bool: true), new("water_enabled", "Usa linea di falda globale", Bool: true),
            new("seismic", "Includi sisma globale SLV", Bool: true), new("seismic_source", "Coefficienti globali", Choices: ["Da sito · βs=0,38", "kh e kv assegnati"]),
            new("kh", "Coefficiente orizzontale globale", "−"), new("kv", "Modulo coefficiente verticale globale", "−")], g);
        FrameworkElement Points(string key, string title)
        {
            var array = g.Array(key); var grid = GridFor([new("x", "x [m]"), new("y", "y [m]")], array, height: 120);
            return Group(title, Ui.Stack(grid, Ui.Bar(Ui.Button("+ Punto", () => { AddRow(grid, array, J.Obj(("x", ""), ("y", ""))); Changed(); }),
                Ui.Button("− Punto", () => { if (grid.SelectedItem is not JsonRow row) return; array.Remove(row.Values); grid.Rows.Remove(row); Changed(); }))), false);
        }
        GlobalLayers = GridFor([new("name", "Terreno"), new("bottom", "Quota fondo\n[m]"), new("gamma", "γ\n[kN/m³]"), new("gamma_sat", "γsat\n[kN/m³]"), new("phi", "φ′k\n[°]"), new("c", "c′k\n[kPa]"), new("cu", "cu,k\n[kPa]")], g.Array("layers"), height: 170);
        var deepLayers = Ui.Stack(Ui.Text("Quote y dal piano di posa, positive verso l’alto. Fondi degli strati in ordine decrescente. L’ultimo fondo limita la profondità indagata; il primo strato arriva al terreno. Stratigrafia globale indipendente dalla scheda delle spinte.", 11, color: Ui.Muted), GlobalLayers,
            Ui.Bar(Ui.Button("+ Strato profondo", () => { AddRow(GlobalLayers, g.Array("layers"), J.Obj(("name", "Nuovo terreno"), ("bottom", ""), ("gamma", ""), ("gamma_sat", ""), ("phi", ""), ("c", ""), ("cu", ""))); Changed(); }),
            Ui.Button("− Strato", () => { if (GlobalLayers.SelectedItem is not JsonRow row) return; g.Array("layers").Remove(row.Values); GlobalLayers.Rows.Remove(row); Changed(); })));
        var search = Form("global_search", [new("exit_min", "Uscita a valle · x minima", "m"), new("exit_max", "Uscita a valle · x massima", "m"), new("entry_min", "Ingresso a monte · x minima", "m"), new("entry_max", "Ingresso a monte · x massima", "m"),
            new("depth_min", "Profondità minima sotto y=0", "m"), new("depth_max", "Profondità massima sotto y=0", "m"), new("grid", "Nodi per direzione (n³ superfici)"), new("slices", "Conci iniziali"), new("refinements", "Raffinamenti locali")], g);
        globalPreview = new() { Data = Data, Height = 240, FocusCritical = false };
        BuildGlobalMatrix();
        return Ui.Stack(Ui.Text(RetainingWall.GlobalHelp, 11), form, Ui.Bar(Ui.Button("Precompila da muro e terreno", PrepareGlobal), Ui.Button("Calcola solo globale", async () => await CalculateGlobalAsync(), inspection: true), Ui.Button("Word globale", ExportGlobalWord, inspection: true)),
            Ui.Text("La precompilazione propone un profilo piano da adattare al rilievo. x=0 al bordo di valle della fondazione; y=0 al piano di posa. Dopo modifiche alla geometria aggiornare gli estremi del profilo. Nessuna estensione automatica degli strati in profondità.", 11, color: Ui.Muted), globalPreview,
            Points("valley", "Profilo a valle · da sinistra fino a (0;0)"), Points("uphill", "Profilo a monte · da (a+s₀;H+t) verso destra"),
            Group("Strati e resistenze della stabilità globale", deepLayers), Points("water", "Linea di falda · quote piezometriche da sinistra a destra"), Group("Dominio di ricerca e precisione", search, false),
            Group("Combinazioni globali · indipendenti dalle verifiche locali", Ui.Stack(Ui.Bar(Ui.Button("Genera globali", GenerateGlobalMatrix), Ui.Button("Conferma matrice globale", () => { Commit(); g["combination_mode"] = "Personalizzate"; g["combination_signature"] = RetainingWall.GlobalSignature(Data); Changed(); })), globalMessage, globalMatrixHost), false));
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
        GlobalDrawing.Data = Data; GlobalDrawing.Result = GlobalResult; GlobalDrawing.Case = GlobalCase; GlobalDrawing.InvalidateVisual();
        if (globalPreview is not null) { globalPreview.Data = Data; globalPreview.Result = GlobalResult; globalPreview.Case = GlobalCase; globalPreview.InvalidateVisual(); }
        if (Forms.TryGetValue("global", out var form))
        {
            bool manual = Data["global_stability"].B("seismic") && Data["global_stability"].S("seismic_source") == "kh e kv assegnati";
            form.ShowField("seismic_source", Data["global_stability"].B("seismic")); form.ShowField("kh", manual); form.ShowField("kv", manual);
        }
    }
    private FrameworkElement GlobalResultsPanel()
    {
        if (GlobalResult is not { } result) return Ui.Text(independentGlobalError ?? Calculation?.GlobalError ?? "Attivare la stabilità globale in Input → Terreno e completare profilo, strati e ricerca.", 13);
        var table = Table(["Caso", "F", "γR", "η=γR/F", "xc [m]", "yc [m]", "R [m]", "Risolte / provate", "Esito"], result.Cases.Select(c => new[] { c.Factors.Name, c.Critical is { } s ? F(s.Factor) : "—", F(c.Factors.R), c.Critical is { } q ? q.Ratio.ToString("0.000", It) : "—", c.Critical is { } a ? F(a.Circle.X) : "—", c.Critical is { } b ? F(b.Circle.Y) : "—", c.Critical is { } e ? F(e.Circle.Radius) : "—", $"{c.Solved}/{c.Tried}", c.Status }));
        table.Height = 155; table.SelectionChanged += (_, _) => { if (table.SelectedIndex >= 0) GlobalCombination.SelectedItem = result.Cases[table.SelectedIndex].Factors.Name; };
        var panel = Ui.Stack(Ui.Text(RetainingWall.GlobalHelp, 11), table, Ui.Text(BishopSolver.Formula, 11));
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
                GlobalCombination.ItemsSource = result.Cases.Select(c => c.Factors.Name).ToArray(); GlobalCombination.SelectedItem = result.Cases.OrderBy(c => c.Critical?.Factor ?? double.PositiveInfinity).First().Factors.Name;
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
