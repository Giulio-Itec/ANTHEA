using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class RetainingWallWorkspace
{
    internal void GenerateMatrix()
    {
        Commit();
        try { Data["combinations"] = RetainingWall.GenerateCombinations(Data); Data["combination_mode"] = "Automatiche"; Data["combination_signature"] = RetainingWall.CombinationSignature(Data); BuildMatrix(); Changed(); }
        catch (Exception ex) { matrixStatus.Text = ex.Message; }
    }
    private void ConfirmMatrix()
    {
        Commit(); Data["combination_mode"] = "Personalizzate"; Data["combination_signature"] = RetainingWall.CombinationSignature(Data);
        try { RetainingWall.ValidateActions(Data); matrixStatus.Text = "Matrice personalizzata confermata"; Changed(); }
        catch (Exception ex) { matrixStatus.Text = ex.Message; }
    }
    private void CopyCombination()
    {
        if (MatrixGrid?.SelectedItem is not JsonRow row) return; int index = MatrixGrid.Rows.IndexOf(row);
        var copy = (JsonObject)Data.Array("combinations")[index]!.DeepClone(); copy["name"] = copy.S("name") + " copia " + (Data.Array("combinations").Count + 1); copy["approach"] = "Personalizzato";
        Data.Array("combinations").Add(copy); Data["combination_mode"] = "Personalizzate"; BuildMatrix(); Changed();
    }
    private void RemoveCombination()
    {
        if (MatrixGrid?.SelectedItem is not JsonRow row) return; Data.Array("combinations").RemoveAt(MatrixGrid.Rows.IndexOf(row)); Data["combination_mode"] = "Personalizzate"; BuildMatrix(); Changed();
    }
    private void BuildMatrix()
    {
        if (MatrixGrid is not null) grids.Remove(MatrixGrid);
        var fields = new List<Field> { new("enabled", "Usa", Bool: true), new("name", "Combinazione"), new("state", "Stato", Choices: RetainingWall.States), new("approach", "A / M / R", ReadOnly: true), new("wall", "γ muro"), new("soil", "γ terra"), new("water", "γ acqua") };
        foreach (var (a, i) in Data.Array("actions").Select((a, i) => (a, i))) fields.Add(new("a_" + a.S("id"), $"{i + 1} · {a.S("name")}\nγ × ψ"));
        fields.AddRange([new("mphi", "γMφ"), new("rslide", "γR\nscorr."), new("rover", "γR\nribalt."), new("rbearing", "γR\nport."), new("kh", "kh"), new("kv", "kv")]);
        if (Data.Array("combinations").Any(c => c.S("purpose") != "")) fields.Add(new("purpose", "Uso sisma", Choices: ["", "Generale", "Ribaltamento"]));
        MatrixGrid = new JsonGrid(fields) { Height = 205, RowHeight = 30, ColumnHeaderHeight = 43, AlternatingRowBackground = Ui.Brush("#F3F6FA") };
        CompactHeaders(MatrixGrid);
        grids.Add(MatrixGrid);
        for (int i = 0; i < MatrixGrid.Columns.Count; i++) MatrixGrid.Columns[i].Width = i switch { 0 => 55, 1 => 185, 2 => 140, 3 => 150, _ => 85 };
        foreach (var c in Data.Array("combinations").OfType<JsonObject>())
        {
            var flat = (JsonObject)c.DeepClone(); flat.Remove("coefficients"); foreach (var a in Data.Array("actions")) flat["a_" + a.S("id")] = c["coefficients"]?[a.S("id")]?.DeepClone();
            var row = new JsonRow(flat, key =>
            {
                if (Data.S("combination_mode") == "Automatiche") Data["combination_signature"] = RetainingWall.CombinationSignature(Data);
                Data["combination_mode"] = "Personalizzate";
                if (key.StartsWith("a_")) c["coefficients"]![key[2..]] = flat[key]?.DeepClone(); else c[key] = flat[key]?.DeepClone();
                c["approach"] = "Personalizzato"; flat["approach"] = "Personalizzato";
                matrixStatus.Text = "Personalizzate · modifiche conservate · i fattori delle singole azioni includono γ e ψ"; Changed();
            }); MatrixGrid.Rows.Add(row);
        }
        MatrixGrid.SelectionChanged += (_, _) => { if (MatrixGrid.SelectedItem is JsonRow row && Calculation is not null) Combination.SelectedItem = row.Values.S("name"); };
        matrixHost.Content = MatrixGrid;
        matrixStatus.Text = $"{Data.S("combination_mode")} · {Data.Array("combinations").Count} righe · ogni colonna azione contiene il prodotto γ×ψ. Modificare una cella passa a Personalizzate. Genera ripristina il preset NTC.";
    }
    private void ShowResults()
    {
        var r = Calculation!; string? name = Combination.SelectedItem as string; independentGlobal = null; independentGlobalInput = null; independentGlobalError = null;
        string? globalName = GlobalCombination.SelectedItem as string;
        if (Data["global_stability"].B("enabled") && Data["global_stability"].S("combination_mode") == "Automatiche")
        {
            try { Data["global_stability"]!["combinations"] = RetainingWall.GenerateGlobalCombinations(Data); BuildGlobalMatrix(); } catch (ArgumentException ex) { globalMessage.Text = ex.Message; }
        }
        GlobalCombination.ItemsSource = r.GlobalStability?.Cases.Select(c => c.Factors.Name).ToArray();
        GlobalCombination.SelectedItem = r.GlobalStability?.Cases.FirstOrDefault(c => c.Factors.Name == globalName)?.Factors.Name ?? r.GlobalStability?.Cases.OrderBy(c => c.Critical?.Factor ?? double.PositiveInfinity).FirstOrDefault()?.Factors.Name;
        if (Data.S("combination_mode") == "Automatiche")
        {
            Data["combinations"] = r.Input["combinations"]!.DeepClone(); Data["combination_signature"] = RetainingWall.CombinationSignature(Data); BuildMatrix();
        }
        else if (MatrixGrid is null) BuildMatrix();
        Combination.ItemsSource = r.Cases.Select(c => c.Name).ToArray(); Combination.SelectedItem = name is not null && r.Cases.Any(c => c.Name == name) ? name : r.Cases[0].Name;
        ShowCombination();
        UpdateSeismicStatus();
    }
    private void ShowCombination()
    {
        if (Calculation is not { } r || Combination.SelectedItem is not string name) return; var c = r.Cases.FirstOrDefault(c => c.Name == name); if (c is null) return;
        foreach (var draw in new[] { Drawing, Diagrams }) { draw.Data = Data; draw.Calculation = r; draw.Case = c; }
        summary.Text = $"B {F(r.Width)} m · cls {F(r.Volume)} m³/m" + (Data.S("family") == "cantilever" ? $" · acciaio principale {F(r.SteelKg)} kg/m" : "") + $"\nH {F(c.Horizontal)} kN/m · V′ {F(c.Vertical)} kN/m · e {F(c.Eccentricity)} m";
        pressures.Content = PressurePanel(c);
        ShowCheckTable(); RefreshViews();
    }
    private FrameworkElement PressurePanel(RetainingWall.LoadCase c)
    {
        var table = Table(["z₀ [m]", "z₁ [m]", "φk [°]", "φd [°]", "Ka / K₀", "Kae", "σ′v₀ [kPa]", "σ′v₁ [kPa]", "p terra₀", "p terra₁", "p q", "u₀", "u₁", "Δp sisma", "p tot₀", "p tot₁"],
            c.PressureDetails.Select(p => new[] { F(p.Z0), F(p.Z1), F(p.Phi), F(p.PhiDesign), p.K.ToString("0.0000", It), p.Ke.ToString("0.0000", It), F(p.Sigma0), F(p.Sigma1), F(p.Soil0), F(p.Soil1), F(p.Surcharge), F(p.Water0), F(p.Water1), F(p.Dynamic), F(p.Total0), F(p.Total1) }));
        table.Height = 175;
        table.SelectionChanged += (_, _) => { Drawing.SelectedPressure = table.SelectedIndex; Drawing.InvalidateVisual(); };
        bool wood = Data["seismic"].B("enabled") && Data["seismic"].S("method") == "Wood semplificato";
        var thrust = RetainingWall.Integrate(c.Pressures, 0, Data["geometry"].D("height") + Data["geometry"].D("slab"), Data["geometry"].D("height") + Data["geometry"].D("slab"));
        string formula = wood ? "K₀=1−sinφd; Δp=kh·γ·Ht (Wood semplificato, uniforme); p=K₀σ′v+K₀Σ(fᵢqᵢ)+u+Δp." : "Ka=tan²(45°−φd/2); θ=atan[kh/(1−kv)]; Kae=cos²(φd−θ)/{cos²θ·[1+√(sinφd·sin(φd−θ)/cosθ)]²}. Δp=(Kae−Ka)(1−kv)γHt/2; pq=Kae(1−kv)Σ(fᵢqᵢ) in sisma, KaΣ(fᵢqᵢ) in statica.";
        return Ui.Stack(Ui.Text(formula, 11), Ui.Text($"{c.Name} · kh={c.Kh:0.###}, kv={c.Kv:0.###}; θ={Math.Atan2(c.Kh, 1 - c.Kv) * 180 / Math.PI:0.###}°. σ′v incorpora γG e (1−kv). Tutte le pressioni in kPa. Azioni dirette trattate separatamente.", 11), table,
            Ui.Text($"Integrale delle pressioni (incluse pressioni laterali aggiunte): P={F(thrust.Force)} kN/m; momento al piano di posa={F(-thrust.Moment)} kNm/m; quota risultante={(thrust.Force > 0 ? F(-thrust.Moment / thrust.Force) : "—")} m.", 11));
    }
    private void RefreshViews()
    {
        bool global = (string?)ViewMode.SelectedItem == "Stabilità globale";
        GlobalDrawing.Visibility = GlobalCombination.Visibility = globalFullProfile.Visibility = global ? Visibility.Visible : Visibility.Collapsed; Diagrams.Visibility = global ? Visibility.Collapsed : Visibility.Visible;
        if (global) CheckFilter.SelectedItem = "Stabilità globale";
        RefreshGlobal();
        Diagrams.Mode = ViewMode.SelectedItem as string ?? "Geometria e carichi"; Diagrams.Diagrams = Diagrams.Mode == "Sollecitazioni"; Diagrams.Member = Member.SelectedItem as string ?? "Fusto";
        Diagrams.ShowRebar = Diagrams.Mode is "Armature" or "Geometria e carichi"; Member.Visibility = Diagrams.Diagrams ? Visibility.Visible : Visibility.Collapsed;
        Diagrams.ShowLoads = Diagrams.Mode != "Armature"; Diagrams.MaxWidth = Diagrams.Diagrams ? double.PositiveInfinity : 1050;
        Drawing.InvalidateVisual(); Diagrams.InvalidateVisual();
    }
    private void ShowCheckTable()
    {
        SelectSection(null); SelectedResultsGrid = null;
        if ((string?)CheckFilter.SelectedItem == "Stabilità globale") { checks.Content = GlobalResultsPanel(); return; }
        if (Calculation is not { } r || Combination.SelectedItem is not string name) return; var c = r.Cases.FirstOrDefault(c => c.Name == name); if (c is null) return;
        if ((string)CheckFilter.SelectedItem == "Calcolo delle spinte") { checks.Content = PressurePanel(c); return; }
        if ((string)CheckFilter.SelectedItem == "Sollecitazioni numeriche")
        {
            var table = Table(["Elemento", "z/l [m]", "s [m]", "N [kN/m]", "M [kNm/m]", "V [kN/m]", "Armatura"], c.Sections.Select(s => new[] { s.Name, F(s.Position), F(s.Thickness), F(s.N), F(s.M), F(s.V), RebarLabel(Data, RetainingWall.ReinforcementKey(Data, s.Name, s.Position)) })); table.Height = 285;
            table.SelectionChanged += (_, _) => { var f = table.SelectedIndex >= 0 ? c.Sections[table.SelectedIndex] : null; SelectSection(f is not null && (f.Name == "Fusto" || c.Contact.Valid) ? (f.Name, f.Position, c.Name) : null); };
            SelectedResultsGrid = table; checks.Content = table; return;
        }
        var all = r.Checks.Concat(r.Structural);
        var selected = (string)CheckFilter.SelectedItem switch { "Combinazione selezionata" => all.Where(x => x.Combination == name || x.Combination == "Dettagli"), "Tutti i controlli" => all, _ => Envelope(all) };
        var rows = selected.ToArray(); var grid = CheckTable(rows); grid.Height = 285;
        grid.SelectionChanged += (_, _) => { var check = grid.SelectedIndex >= 0 ? rows[grid.SelectedIndex] : null; SelectSection(check is { Member: not null, Position: double z } ? (check.Member, z, check.Combination == "Dettagli" ? c.Name : check.Combination) : null); };
        SelectedResultsGrid = grid;
        var factor = c.Factors;
        var text = Ui.Text($"{c.Name} · {factor.S("approach")} · γMφ={factor.D("mphi", 1):0.##}; γR scorr./ribalt./port.={factor.D("rslide", 1):0.##}/{factor.D("rover", 1):0.##}/{factor.D("rbearing", 1):0.##}" + $"\nRd scorr.={F(c.SlidingResistance)} kN/m · MRd ribalt.={F(c.OverturningResistance)} kNm/m · Rd port.={(c.BearingResistance is double b ? F(b) : "da verificare")} kN/m · U={F(c.Uplift)} kN/m", 12);
        checks.Content = Ui.Stack(text, grid, Ui.Text("Riepilogo: inviluppo delle verifiche locali e della stabilità globale, se attivata. Un controllo non disponibile resta visibile. Per la globale selezionare la vista dedicata e la sua combinazione. Cedimenti da valutare separatamente.", 11, color: Ui.Muted));
    }
    internal static string RebarLabel(JsonObject d, string key)
    {
        if (d.S("family") != "cantilever") return "non armato"; var r = d["reinforcement"]?[key]; return $"{r.D("count"):0} Ø{r.D("diameter"):0}/m/faccia · As={r.D("count") * Math.PI * Math.Pow(r.D("diameter"), 2) / 4:0} mm²/m";
    }
}
