using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class SheetEditor
{
    private void BuildGeo()
    {
        var defaults = Archivio.NuovoFoglio(Module); if (Data["generali"] is not JsonObject) Data["generali"] = new JsonObject(); var g = Data["generali"]!.AsObject();
        string previous = Micro ? "" : Nq.MetodoPrecedente(g);
        foreach (var (k, v) in defaults["generali"]!.AsObject()) if (!g.ContainsKey(k) && k != "metodo_micropalo") g[k] = v?.DeepClone();
        if (!Micro) { g["metodo_nq"] = "Parametrizzata"; if (previous != "") g["metodo_nq_precedente"] = previous; }
        if (Data["efficienza"] is not JsonObject) Data["efficienza"] = defaults["efficienza"]!.DeepClone();
        var fields = new List<Field>();
        if (!Micro) fields.AddRange([new("tipo_palo", "Tipo di palo", Choices: ["Trivellato", "Elica continua", "Battuto"]), new("sottotipo_palo_battuto", "Tipo battuto", Choices: Calcolo.Parametri.Keys.Where(k => k is not ("Trivellato" or "Elica continua")).ToArray())]);
        fields.AddRange([new("diametro", Micro ? "Diametro perforazione Db" : "Diametro palo D", "m"), new("lunghezza", "Lunghezza palo L", "m"), new("peso_specifico_palo", "Peso specifico CLS / palo", "kN/m³"), new("azione_compressione", "Azione assiale di progetto — Compressione", "kN"), new("azione_trazione", "Azione assiale di progetto — Trazione", "kN")]);
        if (Micro) fields.AddRange([new("metodo_micropalo", "Metodo (richiesto)", Choices: [BustamanteDoix.Versione]), new("tipo_iniezione", "Iniezione", Choices: ["IGU", "IRS"]), new("profilo_chs", "Profilo CHS", Choices: Chs.Catalogo.Keys.ToArray()), new("pressione_iniezione", "Pressione p_i = p_l", "MPa"), new("inclinazione", "Inclinazione dalla verticale θ", "°"), new("inizio_aderenza", "Inizio aderenza sb lungo asse", "m"), new("considera_punta", "Considera punta", Bool: true), new("percentuale_punta", "Punta rispetto alla laterale", "%")]);
        else fields.AddRange([new("presenza_falda", "Presenza falda", Bool: true), new("profondita_falda", "Profondità falda zf", "m"), new("considera_sottospinta", "Considera sottospinta", Bool: true), new("metodo_nq", "Metodo Nq", Choices: ["Parametrizzata"])]);
        string[] order = Micro ? ["tipo_iniezione", "diametro", "lunghezza", "inizio_aderenza", "considera_punta", "percentuale_punta", "pressione_iniezione", "profilo_chs", "peso_specifico_palo", "azione_compressione", "azione_trazione", "inclinazione", "metodo_micropalo"] : ["tipo_palo", "sottotipo_palo_battuto", "diametro", "lunghezza", "peso_specifico_palo", "presenza_falda", "profondita_falda", "considera_sottospinta", "azione_compressione", "azione_trazione", "metodo_nq"];
        generalForm = new(g, fields.OrderBy(f => Array.IndexOf(order, f.Key)), key => { if (Micro && key == "tipo_iniezione") ResetAlphas(); Changed(); });
        generalForm.ShowField(Micro ? "metodo_micropalo" : "metodo_nq", false); AddCard("Dati generali", generalForm);
        efficiencyForm = new(Data["efficienza"]!.AsObject(), [new("metodo", "Metodo", Choices: ["Nessuna riduzione", "Converse-Labarre", "Feld", "Definita dall'utente"]), new("numero_pali_x", "Pali in X"), new("numero_pali_y", "Pali in Y"), new("interasse_x", "Interasse X", "m"), new("interasse_y", "Interasse Y", "m"), new("eta_compressione", "ηg,c manuale"), new("eta_trazione", "ηg,t manuale")], _ => Changed(), true);
        AddCard("Efficienza", Ui.Dock(efficiencyForm, bottom: effLabel));
        Field[] norm = [new("verticali_indagate", "Verticali indagate", Choices: Calcolo.Verticali.Keys.ToArray()), new("__xi3", "Correlazione ξ3", ReadOnly: true), new("__xi4", "Correlazione ξ4", ReadOnly: true), new("sicurezza_laterale_compressione", "Sicurezza laterale — Compressione γs"), new("sicurezza_laterale_trazione", "Sicurezza laterale — Trazione γt"), new("sicurezza_base", "Sicurezza di base γb"), new("peso_palo_sfavorevole", "Peso proprio palo — Sfavorevole γG"), new("peso_palo_favorevole", "Peso proprio palo — Favorevole γG")];
        normativeForm = new(g, norm, _ => Changed(), true);
        AddCard("Coefficienti normativa", normativeForm, action: Ui.Button("Reset", () => { foreach (var f in norm.Where(f => !f.ReadOnly)) normativeForm.Set(f.Key, defaults["generali"].S(f.Key)); }));
        AddCard("Verifica", Ui.Dock(verification, bottom: Ui.Text("Valori in kN · Util. = NEd / Rd", 11, color: Ui.Muted)));
        if (Data["stratigrafie"] is not JsonArray) Data["stratigrafie"] = new JsonArray(); RebuildSondages();
        sondages.SelectionChanged += (_, e) => { if (e.Source == sondages) { UpdateProfile(); BuildReferencePlot(); } };
        var surveyActions = Ui.Bar(Ui.Button("+", () => { Commit(); Data.Array("stratigrafie").Add(new JsonArray()); RebuildSondages(Data.Array("stratigrafie").Count - 1); Changed(); }), Ui.Button("−", () =>
        {
            if (sondages.SelectedIndex < 0) return;
            if (MessageBox.Show(Window.GetWindow(this), "Eliminare il sondaggio selezionato dal foglio?", "Sondaggio", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { Commit(); Data.Array("stratigrafie").RemoveAt(sondages.SelectedIndex); RebuildSondages(); Changed(); }
        }));
        AddCard("Stratigrafia", sondages, true, surveyActions); stratigraphy.Data = Data; stratigraphy.Micro = Micro; AddCard("Profilo stratigrafico", stratigraphy, true);
        capacityView.ItemsSource = Micro ? new[] { "Tutte - progetto", "Compressione", "Trazione" } : ["Tutte - progetto", "Drenante · Compressione", "Drenante · Trazione", "Non drenante · Compressione", "Non drenante · Trazione"];
        capacityView.SelectedIndex = 0; capacityView.SelectionChanged += (_, _) => { UpdateVisible(); RebuildCurveChoices(); };
        var actions = Ui.Bar(endValues, Ui.Button("Tutte", () => SetVisible(true)), Ui.Button("Nessuna", () => SetVisible(false)));
        var footer = Ui.Stack(actions, new ScrollViewer { Content = curveChoices, MaxHeight = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        outputs.Items.Insert(0, new TabItem { Header = "Grafico", Content = Ui.Dock(plot, bottom: footer) });
        outputs.Items.Insert(2, new TabItem { Header = Micro ? "Abachi" : "Nq", Content = reference }); outputs.SelectedIndex = 0;
        var context = new ContextMenu(); var fit = new MenuItem { Header = "Adatta" }; fit.Click += (_, _) => plot.ResetView(); var save = new MenuItem { Header = "Salva PNG…" }; save.Click += (_, _) => SavePlot(); context.Items.Add(fit); context.Items.Add(save); plot.ContextMenu = context;
        AddCard("Grafici capacità portante", Ui.Dock(outputs, capacityView), true); UpdateVerification();
    }
    private void ResetAlphas()
    {
        foreach (var rows in Data.Array("stratigrafie")) foreach (var row in rows!.AsArray())
            if (BustamanteDoix.Terreni.ContainsKey(row.S("terreno")) && Data["generali"].S("tipo_iniezione") is "IGU" or "IRS") row!["alpha"] = BustamanteDoix.IntervalloAlpha(row.S("terreno"), Data["generali"].S("tipo_iniezione"))[0].ToString(System.Globalization.CultureInfo.InvariantCulture);
        RebuildSondages();
    }
    private void RebuildSondages(int? selection = null)
    {
        int selected = selection ?? sondages.SelectedIndex; sondages.Items.Clear(); layerGrids.Clear(); int index = 0;
        foreach (var rowsNode in Data.Array("stratigrafie"))
        {
            var rows = rowsNode!.AsArray();
            var fields = Micro ? new List<Field> { new("laterale_attiva", "Laterale", Bool: true), new("terreno", "Terreno", Choices: BustamanteDoix.Terreni.Keys.ToArray()), new("spessore", "Spessore verticale [m]"), new("alpha", "α adottato") } :
                [new("__strato", "Strato", ReadOnly: true), new("laterale_attiva", "Laterale attiva", Bool: true), new("tipologia", "Tipologia", Choices: ["Granulare", "Coesivo"]), new("addensamento", "Addensamento", Choices: ["Sciolto", "Denso"]), new("spessore", "Spessore S [m]"), new("peso_specifico", "γ [kN/m³]"), new("peso_specifico_saturo", "γsat [kN/m³]"), new("angolo_attrito", "φ′ [°]"), new("coesione_efficace", "c′ [kPa]"), new("coesione_non_drenata", "Cu [kPa]"), new("__k", "Coeff. k [-]", ReadOnly: true), new("__mu", "Coeff. μ [-]", ReadOnly: true), new("__alfa", "Coeff. α [-]", ReadOnly: true), new("__nq", "Fattore Nq [-]", ReadOnly: true), new("nc", "Fattore Nc [-]")];
            // Include legacy values in selectors without silently replacing user data.
            for (int i = 0; i < fields.Count; i++) if (fields[i].Choices is not null) fields[i] = fields[i] with { Choices = fields[i].Choices!.Concat(rows.Select(r => r.S(fields[i].Key))).Append("").Distinct().ToArray() };
            var grid = new JsonGrid(fields); layerGrids.Add(grid);
            JsonRow Bind(JsonObject data)
            {
                var display = (JsonObject)data.DeepClone(); if (!display.ContainsKey("laterale_attiva")) display["laterale_attiva"] = true;
                if (!Micro && !display.ContainsKey("nc")) display["nc"] = "9";
                return new JsonRow(display, key =>
                {
                    if (Micro && key == "terreno" && BustamanteDoix.Terreni.ContainsKey(display.S("terreno")) && Data["generali"].S("tipo_iniezione") is "IGU" or "IRS")
                        display["alpha"] = BustamanteDoix.IntervalloAlpha(display.S("terreno"), Data["generali"].S("tipo_iniezione"))[0].ToString(System.Globalization.CultureInfo.InvariantCulture);
                    foreach (var f in fields.Where(f => !f.ReadOnly)) data[f.Key] = display[f.Key]?.DeepClone();
                    Changed();
                });
            }
            foreach (var r in rows.OfType<JsonObject>()) grid.Rows.Add(Bind(r));
            var buttons = Ui.Bar(Ui.Button(Micro ? "+ Strato" : "Aggiungi strato", () =>
            {
                grid.Commit(); var row = Micro ? J.Obj(("spessore", ""), ("terreno", ""), ("alpha", ""), ("laterale_attiva", true)) : J.Obj(("spessore", ""), ("tipologia", "Granulare"), ("addensamento", "Sciolto"), ("peso_specifico", ""), ("peso_specifico_saturo", ""), ("angolo_attrito", ""), ("coesione_efficace", ""), ("coesione_non_drenata", ""), ("nc", "9"), ("laterale_attiva", true));
                rows.Add(row); grid.Rows.Add(Bind(row)); Changed();
            }), Ui.Button(Micro ? "− Strato" : "Elimina ultimo strato", () =>
            { grid.Commit(); int i = Micro ? grid.SelectedIndex : grid.Rows.Count - 1; if (i < 0) return; rows.RemoveAt(i); grid.Rows.RemoveAt(i); Changed(); }));
            Ui.Tab(sondages, Micro ? $"Sondaggio {++index}" : $"{++index}", Ui.Dock(grid, bottom: buttons));
        }
        if (sondages.Items.Count > 0) sondages.SelectedIndex = Math.Clamp(selected, 0, sondages.Items.Count - 1);
    }
    private void Preview()
    {
        if (Section) { PreviewSection(); return; }
        var g = Data["generali"]!; var efficiency = Calcolo.Efficienza(Data); effLabel.Text = efficiency.S("errore") != "" ? efficiency.S("errore") : $"ηg,c = {efficiency.D("eta_compressione"):F3}\nηg,t = {efficiency.D("eta_trazione"):F3}";
        if (Calcolo.Verticali.TryGetValue(g.S("verticali_indagate"), out var xi)) { normativeForm.Set("__xi3", xi.Xi3.ToString("F2"), true); normativeForm.Set("__xi4", xi.Xi4.ToString("F2"), true); }
        if (Micro) generalForm.Enable("percentuale_punta", g.B("considera_punta"));
        else { generalForm.Enable("sottotipo_palo_battuto", g.S("tipo_palo") == "Battuto"); generalForm.Enable("profondita_falda", g.B("presenza_falda")); generalForm.Enable("considera_sottospinta", g.B("presenza_falda")); }
        string method = Data["efficienza"].S("metodo"); foreach (string key in efficiencyForm.Editors.Keys.Where(k => k != "metodo")) efficiencyForm.ShowField(key, key.StartsWith("numero_") ? method is "Feld" or "Converse-Labarre" : key.StartsWith("interasse") ? method == "Converse-Labarre" : method == "Definita dall'utente");
        plot.EmptyMessage = Pile ? g.D("lunghezza") > 0 ? "Completare i dati · calcolo automatico" : "Lunghezza: inserire un numero valido" : "Premere Calcola";
        UpdateProfile(); UpdatePileCoefficients(); BuildReferencePlot();
    }
    private void UpdateProfile()
    { if (Section) return; stratigraphy.SelectedIndex = sondages.SelectedIndex; stratigraphy.ShowAll = Micro || expanded == 5; stratigraphy.InvalidateVisual(); }
    private void UpdatePileCoefficients()
    {
        if (!Pile) return; var g = Data["generali"]!; double diameter = g.D("diametro");
        foreach (var grid in layerGrids)
        {
            double depth = 0; bool valid = true; int i = 0;
            foreach (var row in grid.Rows)
            {
                var d = row.Values; var (k, mu) = Calcolo.CoefficientiLaterali(g, d); double? cu = J.Number(d["coesione_non_drenata"]), thickness = J.Number(d["spessore"]), phi = J.Number(d["angolo_attrito"]);
                row.Output("__strato", ((char)('A' + i++ % 26)).ToString()); row.Output("__k", k?.ToString("0.###") ?? "—"); row.Output("__mu", mu?.ToString("0.###") ?? "—"); row.Output("__alfa", d.S("tipologia") == "Coesivo" && cu >= 0 ? Calcolo.CoefficienteAlfa(g, cu.Value).ToString("0.###") : "—");
                valid &= thickness > 0; if (valid) depth += thickness!.Value;
                row.Output("__nq", valid && double.IsFinite(depth) && diameter > 0 && phi is not null ? Nq.Dettaglio(phi.Value, depth / diameter, diameter > .8).D("nq").ToString("0.###") : "—");
            }
        }
    }
    private void ShowGeoResults()
    {
        allSeries.Clear(); visibility.Clear(); int index = 0; Brush[] colors = [Ui.Navy, Ui.Navy, Ui.Brush("#111827"), Ui.Brush("#111827")];
        foreach (var (name, curve) in Result!["curve"]!.AsObject())
        {
            foreach (string branch in new[] { "progetto", "media", "minima" })
            {
                string key = branch + "_" + name, label = name.Replace("non_drenante", "Non dren.").Replace("drenante", "Dren.").Replace("compressione", "C").Replace("trazione", "T").Replace('_', ' ') + " · " + branch;
                var series = new Serie(label, curve!.Array(branch).Select(p => new[] { p![1]!.GetValue<double>(), p[0]!.GetValue<double>() }).ToList(), colors[index % colors.Length], branch != "progetto");
                allSeries.Add((key, series)); visibility[key] = Data["visibilita_grafici"].B(key, branch == "progetto");
            }
            index++;
        }
        foreach (var (name, points) in Result["azioni"]!.AsObject()) if (points!.AsArray().Count > 0)
        { string key = "azione_" + name; allSeries.Add((key, new Serie("Ed " + name, points.AsArray().Select(p => new[] { p![1]!.GetValue<double>(), p[0]!.GetValue<double>() }).ToList(), Brushes.Black, true))); visibility[key] = Data["visibilita_grafici"].B(key, true); }
        plot.Title = Micro ? "Capacità portante del micropalo" : "Capacità portante del palo"; plot.YLabel = Micro ? "Lungo asse s [m]" : "Profondità z [m]"; plot.CapacityDepth = Result.D("profondita_massima"); plot.ResetView(); UpdateVisible(); RebuildCurveChoices();
        tables = Tabelle.Crea(Result, Micro); var summary = new List<string[]>();
        foreach (var (key, c) in Result["curve"]!.AsObject())
        {
            var p = c!.Array("progetto")[^1]!; double rd = p[1]!.GetValue<double>(); var a = Result["azioni"]!.Array(key.Contains("trazione") ? "trazione" : "compressione").LastOrDefault(); double? ed = J.Number(a?[1]);
            summary.Add([key.Replace('_', ' '), Tabelle.F(p[0]), Tabelle.F(rd), ed.HasValue ? Tabelle.F(ed.Value) : "—", ed.HasValue ? ed <= rd ? "Verificato" : "Non verificato" : "Azione non inserita"]);
        }
        tables.Insert(0, new("Riepilogo alla quota disponibile", ["Verifica", Micro ? "s [m]" : "z [m]", "Rd [kN]", "Ed [kN]", "Esito"], summary)); PopulateTables(); UpdateVerification(); BuildReferencePlot();
        if (!Result.B("copertura_completa")) SetWarnings("ATTENZIONE: stratigrafia insufficiente; risultati limitati alla quota disponibile, non alla punta richiesta.\n" + warnings.Text);
    }
    private void UpdateVerification()
    {
        if (Section) return; var rows = new List<string[]>();
        if (Result?.B("copertura_completa") == true)
        {
            foreach (var (key, curve) in Result["curve"]!.AsObject())
            {
                double rd = curve!.Array("progetto")[^1]![1]!.GetValue<double>(); var action = Result["azioni"]!.Array(key.Contains("trazione") ? "trazione" : "compressione").LastOrDefault(); double? ed = J.Number(action?[1]);
                rows.Add([key.Replace("non_drenante", "N.dr.").Replace("drenante", "Dr.").Replace("compressione", "C").Replace("trazione", "T").Replace('_', ' '), ed?.ToString("N1") ?? "—", rd.ToString("N1"), ed.HasValue && rd > 0 ? (100 * ed.Value / rd).ToString("N1") + "%" : "—"]);
            }
        }
        else foreach (string name in Micro ? new[] { "C", "T" } : ["Dr. C", "Dr. T", "N.dr. C", "N.dr. T"]) rows.Add([name, "—", "—", "—"]);
        var table = Ui.Table(["Cond.", "NEd [kN]", "Rd [kN]", "Util."], rows); foreach (var col in table.Columns) col.Width = new DataGridLength(1, DataGridLengthUnitType.Star); verification.Content = table;
    }
    private bool CapacityIncludes(string key)
    {
        int selected = capacityView.SelectedIndex; if (selected <= 0) return key.StartsWith("progetto_") || key.StartsWith("azione_");
        string condition = Micro ? selected == 1 ? "compressione" : "trazione" : new[] { "", "drenante_compressione", "drenante_trazione", "non_drenante_compressione", "non_drenante_trazione" }[selected];
        return key.StartsWith("azione_") ? key.EndsWith(condition.EndsWith("trazione") ? "trazione" : "compressione") : key[(key.IndexOf('_') + 1)..] == condition;
    }
    private void UpdateVisible()
    { plot.Series = allSeries.Where(p => visibility.GetValueOrDefault(p.Key) && CapacityIncludes(p.Key)).Select(p => p.Series).ToList(); if (Result is not null) plot.EmptyMessage = "Nessuna curva selezionata"; plot.InvalidateVisual(); }
    private void SetVisible(bool show)
    { foreach (var p in allSeries) visibility[p.Key] = show; StoreVisibility(); UpdateVisible(); RebuildCurveChoices(); }
    private void StoreVisibility()
    {
        if (Result is null) return; if (Data["visibilita_grafici"] is not JsonObject) Data["visibilita_grafici"] = new JsonObject(); bool changed = false;
        foreach (var (key, value) in visibility) { if (Data["visibilita_grafici"]![key]?.ToString() != value.ToString().ToLowerInvariant()) changed = true; Data["visibilita_grafici"]![key] = value; }
        if (changed) Modified?.Invoke();
    }
    private void RebuildCurveChoices()
    {
        curveChoices.Children.Clear(); endValues.Text = Result?.B("copertura_completa") == true ? $"Valori a L={Data["generali"].S("lunghezza")} m [kN]" : "Valori a L non disponibili";
        foreach (var (key, series) in allSeries.Where(p => CapacityIncludes(p.Key)))
        {
            string value = Result?.B("copertura_completa") == true && series.Points.Count > 0 ? series.Points[^1][0].ToString("N1") : "—";
            var check = new CheckBox { Content = series.Name + ": " + value, Foreground = series.Color, IsChecked = visibility[key], Margin = new Thickness(2, 4, 8, 4), FontSize = 11 };
            void Change() { visibility[key] = check.IsChecked == true; StoreVisibility(); UpdateVisible(); }
            check.Checked += (_, _) => Change(); check.Unchecked += (_, _) => Change(); curveChoices.Children.Add(check);
        }
    }
    private void BuildReferencePlot()
    {
        if (Section) return;
        var series = new List<Serie>(); Brush[] colors = [Ui.Blue, Brushes.SeaGreen, Brushes.DarkOrange, Brushes.Purple, Brushes.Brown, Brushes.Teal, Brushes.Crimson, Brushes.Gray]; int i = 0;
        reference.Title = Micro ? "Abachi Bustamante–Doix" : "Nq parametrizzato"; reference.XLabel = Micro ? "p_l [MPa]" : "φ [°]"; reference.YLabel = Micro ? "s [kPa]" : "Nq / Nq*"; reference.Markers = []; reference.Note = "";
        if (Micro) foreach (var (name, pts) in BustamanteDoix.Curve) series.Add(new(name, pts.Select(p => new[] { p.X, 1000 * p.Y }).ToList(), colors[i++ % colors.Length]));
        else
        {
            bool big = Data["generali"].D("diametro", 1) > .8; reference.XMinimum = 25;
            foreach (double ratio in big ? Nq.RapportiGrande : Nq.RapportiMedio)
            { double low = big ? 26 : 25, high = big ? 42 : ratio == 5 ? 38.8 : ratio == 10 ? 40 : ratio == 20 ? 41 : 41.6; series.Add(new("z/D=" + ratio, Enumerable.Range(0, 161).Select(k => { double phi = low + (high - low) * k / 160; return new[] { phi, Nq.Dettaglio(phi, ratio, big).D("nq") }; }).ToList(), colors[i++ % colors.Length])); }
            double length = Data["generali"].D("lunghezza"), diameter = Data["generali"].D("diametro");
            if (length > 0 && diameter > 0 && double.IsFinite(length / diameter))
            {
                double ratio = length / diameter, low = 25, high = big ? 42 : 41.6; int selected = sondages.SelectedIndex;
                var surveys = Result?.B("copertura_completa") == true ? Result.Array("dettagli").LastOrDefault(d => Math.Abs(d.D("z") - length) < 1e-8)?.Array("sondaggi") : null;
                if (selected >= 0 && surveys is not null && selected < surveys.Count && surveys[selected]?["dettaglio_nq"] is { } detail)
                {
                    double phi = detail.D("phi"), nq = detail.D("nq"); low = Math.Min(low, phi); high = Math.Max(high, phi);
                    reference.Markers.Add(new(phi, nq, $"S{selected + 1}: φ={phi:0.##}° · {(big ? "Nq*" : "Nq")}={nq:0.###}", Brushes.Crimson));
                    reference.Note = $"Stratigrafia {selected + 1} · punta L={length:0.###} m · L/D={ratio:0.###}" + (detail.B("limite_phi") || detail.B("limite_rapporto") ? " · valore limitato al bordo dell'abaco" : "");
                }
                else reference.Note = $"L/D effettivo = {ratio:0.###} · punto disponibile con dati completi fino alla punta";
                reference.XMinimum = low; series.Add(new($"L/D effettivo = {ratio:0.###}", Enumerable.Range(0, 201).Select(k => { double phi = low + (high - low) * k / 200; return new[] { phi, Nq.Dettaglio(phi, ratio, big).D("nq") }; }).ToList(), Brushes.Crimson, Highlighted: true));
            }
        }
        reference.Series = series; reference.InvalidateVisual();
    }
}
