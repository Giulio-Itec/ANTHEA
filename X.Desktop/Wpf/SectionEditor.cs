using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using X.Core;

namespace X.Desktop;

internal sealed partial class SheetEditor
{
    private void BuildSection()
    {
        var defaults = SezioneCA.DefaultInput(); if (Data["input"] is not JsonObject) Data["input"] = defaults.DeepClone(); var input = Data["input"]!.AsObject();
        foreach (var (key, value) in defaults) if (!input.ContainsKey(key)) input[key] = value?.DeepClone();
        foreach (string key in new[] { "apply_pile_requirements", "apply_minimum_eccentricity", "dissipative_zone" }) input[key] = false;
        input["minimum_eccentricity_mm"] = "0"; Data["versione_sezione"] = 2;
        generalForm = new(input, [new("shape", "Sezione", Choices: ["Circolare", "Rettangolare", "A T"]), new("diameter_mm", "Diametro D", "mm"), new("width_mm", "Larghezza b", "mm"), new("height_mm", "Altezza h", "mm"), new("flange_width_mm", "Larghezza ala bf", "mm"), new("web_width_mm", "Larghezza anima bw", "mm"), new("flange_thickness_mm", "Spessore ala hf", "mm"), new("cover_mm", "Copriferro netto alla staffa", "mm")], _ => Changed(), true);
        var settings = Ui.Button("Coefficienti materiali…", () =>
        {
            var form = new InputForm(input, [new("fyk_mpa", "Resistenza fyk", "MPa"), new("alpha_cc", "αcc"), new("gamma_c", "γc"), new("gamma_s", "γs")], _ => Changed());
            Ui.Dialog(this, "Coefficienti materiali", Ui.Paper(form, 15), 440, 275).ShowDialog();
        });
        AddCard("Geometria", Ui.Dock(generalForm, bottom: settings));
        Field[] materials = [new("classe_cls", "Classe CLS", Choices: ["C12/15", "C16/20", "C20/25", "C25/30", "C30/37", "C35/45", "C40/50", "C45/55", "C50/60", "C55/67", "C60/75", "C70/85", "C80/95", "C90/105", "Personalizzato"]), new("__ec2", "Deformazione εc2", "‰", ReadOnly: true), new("__ecu", "Ultima εcu", "‰", ReadOnly: true), new("fck_mpa", "Resistenza fck", "MPa"), new("__fcd", "Progetto fcd", "MPa", ReadOnly: true), new("__ecm", "Modulo Ecm", "MPa", ReadOnly: true), new("__esu", "Ultima εsu (inform.)", "‰", ReadOnly: true), new("__fyd", "Progetto fyd", "MPa", ReadOnly: true), new("steel_modulus_mpa", "Modulo Es", "MPa"), new("__esyd", "Snervamento εsyd", "‰", ReadOnly: true), new("n", "Omogeneizzazione n")];
        materialForm = new(input, materials, key =>
        {
            if (key == "classe_cls" && input.S(key).StartsWith('C')) materialForm?.Set("fck_mpa", input.S(key)[1..].Split('/')[0]);
            if (key == "n" && !building) Data["n_automatico"] = false; Changed();
        }, true);
        AddCard("Materiali", Ui.Dock(materialForm, bottom: Ui.Button("Ripristina n = Es/Ecm", () => { Data["n_automatico"] = true; Changed(); })));
        string[] diameters = ["6", "8", "10", "12", "14", "16", "18", "20", "22", "24", "26", "28", "30", "32", "36", "40"];
        barForm = new(input, [new("longitudinal_bar_count", "Barre circolari: quantità"), new("longitudinal_bar_diameter_mm", "Diametro circolari", "mm", diameters), new("top_bar_count", "Barre superiori: quantità"), new("top_bar_diameter_mm", "Diametro superiori", "mm", diameters), new("bottom_bar_count", "Barre inferiori: quantità"), new("bottom_bar_diameter_mm", "Diametro inferiori", "mm", diameters), new("side_bar_count_per_side", "Barre laterali per lato"), new("side_bar_diameter_mm", "Diametro laterali", "mm", diameters), new("transverse_bar_diameter_mm", "Diametro staffa", "mm", diameters), new("transverse_spacing_mm", "Passo staffe", "mm")], _ => Changed(), true);
        AddCard("Armatura", barForm);
        resultSelect.SelectionChanged += (_, _) => ShowSectionState(); AddCard("Disegno sezione", Ui.Dock(sectionDrawing, resultSelect));
        void DomainChanged() { domain.Series = []; domain.InvalidateVisual(); status.Text = "Dominio selezionato da calcolare · premere Calcola dominio"; }
        domainType.SelectionChanged += (_, _) => DomainChanged(); domainMode.SelectionChanged += (_, _) => DomainChanged(); domainN.TextChanged += (_, _) => DomainChanged();
        var domainBar = Ui.Bar(domainType, domainMode, Ui.Text("N [kN]"), domainN, Ui.Button("Calcola dominio", async () => await CalculateDomainAsync()), Ui.Button("Adatta", domain.ResetView));
        AddCard("Dominio", Ui.Dock(domain, domainBar));
        if (Data["combinazioni"] is not JsonObject) Data["combinazioni"] = J.Obj(("SLU", new JsonArray(J.Obj(("nome", "Combo 1"), ("azioni", new[] { input.S("axial_force_kn", "0"), input.S("moment_x_knm", "0"), input.S("moment_y_knm", "0") })))));
        foreach (string limit in new[] { "SLU", "SLV", "SLE" })
        {
            if (Data["combinazioni"]![limit] is not JsonArray) Data["combinazioni"]![limit] = new JsonArray();
            var fields = new List<Field> { new("nome", "Combo"), new("N", "N [kN]"), new("Mx", "Mx [kNm]"), new("My", "My [kNm]") };
            fields.AddRange(limit == "SLE" ? [new("sigma_s", "σs [MPa]", ReadOnly: true), new("sigma_c", "σc [MPa]", ReadOnly: true)] : new Field[] { new("R", "R", ReadOnly: true) });
            var grid = new JsonGrid(fields, true); combos[limit] = grid;
            void Sync()
            {
                var rows = Data["combinazioni"]![limit]!.AsArray(); rows.Clear();
                foreach (var r in grid.Rows) rows.Add(J.Obj(("nome", r.Values.S("nome")), ("azioni", new[] { r.Values.S("N"), r.Values.S("Mx"), r.Values.S("My") }))); Changed();
            }
            foreach (var row in Data["combinazioni"]![limit]!.AsArray())
            { var a = row!.Array("azioni"); grid.Rows.Add(new JsonRow(J.Obj(("nome", row.S("nome")), ("N", a.ElementAtOrDefault(0)?.ToString() ?? ""), ("Mx", a.ElementAtOrDefault(1)?.ToString() ?? ""), ("My", a.ElementAtOrDefault(2)?.ToString() ?? "")), _ => Sync())); }
            var buttons = Ui.Bar(Ui.Button("+ Combinazione", () => { grid.Commit(); grid.Rows.Add(new JsonRow(J.Obj(("nome", "Combo " + (grid.Rows.Count + 1)), ("N", "0"), ("Mx", "0"), ("My", "0")), _ => Sync())); Sync(); }), Ui.Button("− Combinazione", () => { grid.Commit(); if (grid.SelectedItem is JsonRow row) { grid.Rows.Remove(row); Sync(); } }));
            if (limit == "SLE") buttons.Children.Add(Ui.Button("Tabelle e dettagli", ShowDetails));
            AddCard(limit, Ui.Dock(grid, bottom: buttons));
        }
        generalForm.ToolTip = effLabel;
    }
    private void PreviewSection()
    {
        var input = Data["input"]!.AsObject(); string shape = input.S("shape");
        foreach (string key in new[] { "diameter_mm", "width_mm", "height_mm", "flange_width_mm", "web_width_mm", "flange_thickness_mm" }) generalForm.ShowField(key, key switch { "diameter_mm" => shape == "Circolare", "width_mm" => shape == "Rettangolare", "height_mm" => shape != "Circolare", _ => shape == "A T" });
        foreach (string key in barForm.Editors.Keys.Where(k => !k.StartsWith("transverse"))) barForm.ShowField(key, key.StartsWith("longitudinal") ? shape == "Circolare" : shape != "Circolare");
        try
        {
            var preview = (JsonObject)input.DeepClone(); foreach (string key in new[] { "fck_mpa", "fyk_mpa", "alpha_cc", "gamma_c", "gamma_s", "steel_modulus_mpa", "transverse_spacing_mm" }) preview[key] = SezioneCA.DefaultInput()[key]!.DeepClone();
            var engine = new SezioneCA(preview, 12, 36); sectionDrawing.Outline = engine.Outline; sectionDrawing.Bars = engine.Bars;
        }
        catch (ArgumentException) { sectionDrawing.Outline = []; sectionDrawing.Bars = []; }
        sectionDrawing.InvalidateVisual();
        try
        {
            var engine = new SezioneCA(input, 12, 36); if (Data.B("n_automatico", true)) materialForm.Set("n", engine.NAutomatico.ToString("G6"), true);
            foreach (var (key, value) in new[] { ("__ec2", (engine.EpsC2 * 1000).ToString("G5")), ("__ecu", (engine.EpsCu * 1000).ToString("G5")), ("__fcd", engine.Fcd.ToString("F2")), ("__fyd", engine.Fyd.ToString("F2")), ("__ecm", (engine.Es / engine.NAutomatico).ToString("F0")), ("__esyd", (engine.Fyd / engine.Es * 1000).ToString("F3")), ("__esu", "67,5") }) materialForm.Set(key, value, true);
            effLabel.Text = "SLE: " + (Data.B("n_automatico", true) ? "n automatico" : "n manuale") + " · εsu = 67,5‰ informativo\nLegame SLU: acciaio elastico-perfettamente plastico.";
        }
        catch (ArgumentException) { foreach (string key in materialForm.Editors.Keys.Where(k => k.StartsWith("__"))) materialForm.Set(key, "—", true); }
    }
    private void ShowSectionResults()
    {
        var rows = new List<string[]>(); resultSelect.Items.Clear();
        foreach (var (name, result) in Result!["risultati"]!.AsObject())
        {
            resultSelect.Items.Add(name); rows.Add([name, result.S("errore"), Tabelle.F(result?["utilization"]), Tabelle.F(result?["resistance_moment_knm"]), Tabelle.F(result?["resistance_elastic_knm"]), Tabelle.F(result?["stato"]?["sigma_acciaio"]), Tabelle.F(result?["stato"]?["sigma_cls"])]);
        }
        foreach (var (limit, grid) in combos) for (int i = 0; i < grid.Rows.Count; i++)
        {
            var row = grid.Rows[i]; string name = limit + " · " + row.Values.S("nome"); var result = Result["risultati"]?[name] ?? Result["risultati"]?[name + " [" + (i + 1) + "]"];
            row.Output("R", result.S("errore") != "" ? "Errore" : Tabelle.F(result?["utilization"]));
            row.Output("sigma_s", result.S("errore") != "" ? "Errore" : Tabelle.F(result?["stato"]?["sigma_acciaio"])); row.Output("sigma_c", Tabelle.F(result?["stato"]?["sigma_cls"]));
        }
        tables = [new("Combinazioni — risultati", ["Combinazione", "Errore", "R [-]", "MRd plastico [kNm]", "MRd elastico [kNm]", "σs [MPa]", "σc [MPa]"], rows)]; PopulateTables(); if (resultSelect.Items.Count > 0) resultSelect.SelectedIndex = 0;
    }
    private void ShowSectionState()
    {
        sectionDrawing.Plane = null;
        if (Result is not null && resultSelect.SelectedItem is string key)
        {
            var r = Result["risultati"]?[key];
            if (r?["direction_rad"] is not null)
            {
                var engine = new SezioneCA(Data["input"]!.AsObject(), 12, 36); double angle = r.D("direction_rad"); double? depth = J.Number(r["neutral_axis_depth_mm"]);
                sectionDrawing.Plane = depth is null ? [1, 0, 0] : [-(engine.Supports(angle).Max - depth.Value), Math.Cos(angle), Math.Sin(angle)];
            }
            else if (r?["stato"]?["piano"] is JsonArray plane) { double length = r["stato"].D("lunghezza_mm"); sectionDrawing.Plane = [plane[0]!.GetValue<double>(), plane[1]!.GetValue<double>() / length, plane[2]!.GetValue<double>() / length]; }
        }
        sectionDrawing.InvalidateVisual();
    }
    private async Task CalculateDomainAsync()
    {
        if (Busy || disposed) return; Commit(); var data = (JsonObject)Data.DeepClone(); string type = domainType.Text, mode = domainMode.Text;
        double? axial = J.Number(JsonValue.Create(domainN.Text)); if (axial is null) { status.Text = "N del dominio: inserire un numero finito."; return; }
        Busy = true; canvas.IsEnabled = false; calculate.IsEnabled = false; status.Text = "Calcolo del dominio in corso…";
        try
        {
            var points = await Task.Run(() => { var engine = new SezioneCA(data["input"]!.AsObject()); return type == "N–Mx" ? Domini.NM(engine, mode, engine.NAutomatico) : Domini.MM(engine, mode, engine.NAutomatico, axial.Value); });
            if (disposed) return; if (points.Count > 0) points.Add(points[0]); domain.Title = mode + " · " + type + (type == "Mx–My" ? $" · N={axial} kN" : ""); domain.XLabel = "Mx [kNm]"; domain.YLabel = type == "N–Mx" ? "N [kN]" : "My [kNm]"; domain.Series = [new("Dominio", points, Ui.Blue)]; domain.ResetView(); status.Text = "Dominio calcolato · interpolazione tra profili senza estrapolazione";
        }
        catch (Exception ex) { status.Text = ex.Message; }
        finally { Busy = false; canvas.IsEnabled = true; calculate.IsEnabled = true; }
    }
}
