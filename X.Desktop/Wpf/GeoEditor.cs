using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using X.Core;

namespace X.Desktop;

internal sealed partial class SheetEditor
{
    private void BuildGeo()
    {
        var defaults = Archivio.NuovoFoglio(Module); if (Data["generali"] is not JsonObject) Data["generali"] = new JsonObject(); var g = Data["generali"]!.AsObject();
        string previous = Micro ? "" : Nq.MetodoPrecedente(g);
        if (Micro) g.Remove("peso_lineare_micropalo"); // Superseded manual input; weight is always derived from geometry.
        foreach (var (k, v) in defaults["generali"]!.AsObject()) if (!g.ContainsKey(k) && k != "metodo_micropalo") g[k] = v?.DeepClone();
        if (!Micro) { g["metodo_nq"] = "Parametrizzata"; if (previous != "") g["metodo_nq_precedente"] = previous; }
        if (Data["efficienza"] is not JsonObject) Data["efficienza"] = defaults["efficienza"]!.DeepClone();
        var fields = new List<Field>();
        if (!Micro) fields.AddRange([new("tipo_palo", "Tipo di palo", Choices: ["Trivellato", "Elica continua", "Battuto"]), new("sottotipo_palo_battuto", "Tipo battuto", Choices: Calcolo.Parametri.Keys.Where(k => k is not ("Trivellato" or "Elica continua")).ToArray())]);
        fields.AddRange([new("diametro", Micro ? "Diametro perforazione Db" : "Diametro palo D", "m"), new("lunghezza", "Lunghezza palo L", "m"), new("peso_specifico_palo", "Peso specifico CLS / palo", "kN/m³"), new("azione_compressione", "Azione assiale di progetto — Compressione", "kN"), new("azione_trazione", "Azione assiale di progetto — Trazione", "kN")]);
        if (Micro) fields.AddRange([new("metodo_micropalo", "Metodo (richiesto)", Choices: [BustamanteDoix.Versione]), new("tipo_iniezione", "Iniezione", Choices: ["IGU", "IRS"]), new("profilo_chs", "Profilo CHS", Choices: Chs.Catalogo.Keys.ToArray()), new("pressione_iniezione", "Pressione p_i = p_l", "MPa"), new("inclinazione", "Inclinazione dalla verticale θ", "°"), new("inizio_aderenza", "Inizio aderenza sb lungo asse", "m"), new("considera_punta", "Considera punta", Bool: true), new("percentuale_punta", "Punta rispetto alla laterale", "%")]);
        else fields.AddRange([new("presenza_falda", "Presenza falda", Bool: true), new("profondita_falda", "Profondità falda zf", "m"), new("considera_sottospinta", "Considera sottospinta", Bool: true), new("metodo_nq", "Metodo Nq", Choices: ["Parametrizzata"])]);
        string[] order = Micro ? ["tipo_iniezione", "diametro", "lunghezza", "inizio_aderenza", "considera_punta", "percentuale_punta", "pressione_iniezione", "profilo_chs", "peso_specifico_palo", "azione_compressione", "azione_trazione", "inclinazione", "metodo_micropalo"] : ["tipo_palo", "sottotipo_palo_battuto", "diametro", "lunghezza", "peso_specifico_palo", "presenza_falda", "profondita_falda", "considera_sottospinta", "azione_compressione", "azione_trazione", "metodo_nq"];
        if (Micro)
        {
            fields[fields.FindIndex(f => f.Key == "diametro")] = new("diametro", "Diametro perforazione", "m", Symbol: "Db");
            fields[fields.FindIndex(f => f.Key == "peso_specifico_palo")] = new("__peso_lineare", "Peso al metro del micropalo", "kN/m", ReadOnly: true, Symbol: "qk");
            order[Array.IndexOf(order, "peso_specifico_palo")] = "__peso_lineare";
        }
        generalForm = new(g, fields.OrderBy(f => Array.IndexOf(order, f.Key)), key => { if (Micro && key == "tipo_iniezione") ResetAlphas(); Changed(); }, symbolColumns: true);
        generalForm.ShowField(Micro ? "metodo_micropalo" : "metodo_nq", false); AddCard("Dati generali", generalForm);
        efficiencyForm = new(Data["efficienza"]!.AsObject(), [new("metodo", "Metodo", Choices: ["Nessuna riduzione", "Converse-Labarre", "Feld", "Definita dall'utente"]), new("numero_pali_x", "Pali in X"), new("numero_pali_y", "Pali in Y"), new("interasse_x", "Interasse X", "m"), new("interasse_y", "Interasse Y", "m"), new("eta_compressione", "ηg,c manuale"), new("eta_trazione", "ηg,t manuale")], _ => Changed(), true, symbolColumns: true);
        AddCard("Efficienza", Ui.Dock(efficiencyForm, bottom: effLabel));
        Field[] norm = [new("verticali_indagate", "Verticali indagate", Choices: Calcolo.Verticali.Keys.ToArray()), new("__xi3", "Correlazione ξ3", ReadOnly: true), new("__xi4", "Correlazione ξ4", ReadOnly: true), new("sicurezza_laterale_compressione", "Sicurezza laterale — Compressione γs"), new("sicurezza_laterale_trazione", "Sicurezza laterale — Trazione γt"), new("sicurezza_base", "Sicurezza di base γb"), new("peso_palo_sfavorevole", "Peso proprio palo — Sfavorevole γG"), new("peso_palo_favorevole", "Peso proprio palo — Favorevole γG")];
        normativeForm = new(g, norm, _ => Changed(), true, symbolColumns: true);
        AddCard("Coefficienti normativa", normativeForm, action: Ui.Button("Reset", () => { foreach (var f in norm.Where(f => !f.ReadOnly)) normativeForm.Set(f.Key, defaults["generali"].S(f.Key)); }));
        AddCard("Verifica", Ui.Dock(verification, bottom: Ui.Text("Valori in kN · Util. = NEd / Rd", 13, color: Ui.Muted)));
        if (Data["stratigrafie"] is not JsonArray) Data["stratigrafie"] = new JsonArray(); RebuildSondages();
        sondages.SelectionChanged += (_, e) => { if (e.Source == sondages) { UpdateProfile(); BuildReferencePlot(); } };
        var surveyActions = Ui.Bar(Ui.Button("+", () => { Commit(); Data.Array("stratigrafie").Add(new JsonArray(NewLayer())); RebuildSondages(Data.Array("stratigrafie").Count - 1); Changed(); }), Ui.Button("−", () =>
        {
            if (sondages.SelectedIndex < 0) return;
            DeleteSurvey(Data.Array("stratigrafie")[sondages.SelectedIndex]!.AsArray());
        }));
        if (Geo)
        {
            surveyActions.Children.Add(Ui.Button("Copia in…", () => ChooseSurveyCopy(copyInto: true)));
            surveyActions.Children.Add(Ui.Button("Copia da…", () => ChooseSurveyCopy(copyInto: false)));
        }
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
            var fields = Micro ? new List<Field> { new("laterale_attiva", "Laterale", Bool: true), new("terreno", "Terreno", Choices: BustamanteDoix.Terreni.Keys.ToArray()), new("spessore", "Spessore verticale [m]"), new("alpha", "α adottato"), new("__tau", "Aderenza τ [kPa]", ReadOnly: true) } :
                [new("__strato", "Strato", ReadOnly: true), new("laterale_attiva", "Laterale attiva", Bool: true), new("tipologia", "Tipologia", Choices: ["Granulare", "Coesivo"]), new("addensamento", "Addensamento", Choices: ["Sciolto", "Denso"]), new("spessore", "Spessore S [m]"), new("peso_specifico", "γ [kN/m³]"), new("peso_specifico_saturo", "γsat [kN/m³]"), new("angolo_attrito", "φ′ [°]"), new("coesione_efficace", "c′ [kPa]"), new("coesione_non_drenata", "Cu [kPa]"), new("__k", "Coeff. k [-]", ReadOnly: true), new("__mu", "Coeff. μ [-]", ReadOnly: true), new("__alfa", "Coeff. α [-]", ReadOnly: true), new("__nq", "Fattore Nq [-]", ReadOnly: true), new("nc", "Fattore Nc [-]")];
            // Include legacy values in selectors without silently replacing user data.
            for (int i = 0; i < fields.Count; i++) if (fields[i].Choices is not null) fields[i] = fields[i] with { Choices = fields[i].Choices!.Concat(rows.Select(r => r.S(fields[i].Key))).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray() };
            var grid = new JsonGrid(fields); layerGrids.Add(grid);
            JsonRow Bind(JsonObject data)
            {
                var display = (JsonObject)data.DeepClone(); if (!display.ContainsKey("laterale_attiva")) display["laterale_attiva"] = true;
                if (!Micro && !display.ContainsKey("nc")) display["nc"] = "9";
                JsonRow? bound = null;
                bound = new JsonRow(display, key =>
                {
                    if (Micro && key == "terreno" && BustamanteDoix.Terreni.ContainsKey(display.S("terreno")) && Data["generali"].S("tipo_iniezione") is "IGU" or "IRS")
                        bound!.Output("alpha", BustamanteDoix.IntervalloAlpha(display.S("terreno"), Data["generali"].S("tipo_iniezione"))[0].ToString(System.Globalization.CultureInfo.InvariantCulture));
                    foreach (var f in fields.Where(f => !f.ReadOnly)) data[f.Key] = display[f.Key]?.DeepClone();
                    Changed();
                });
                return bound;
            }
            foreach (var r in rows.OfType<JsonObject>()) grid.Rows.Add(Bind(r));
            if (Geo)
            {
                void AddLayer() { var row = NewLayer(); rows.Add(row); grid.Rows.Add(Bind(row)); Changed(); }
                void DeleteLayer(JsonRow row)
                {
                    int position = grid.Rows.IndexOf(row); if (position < 0) return;
                    rows.RemoveAt(position); grid.Rows.RemoveAt(position); Changed();
                }
                var tab = Ui.Tab(sondages, $"{++index}", StratigraphyTable.Build(grid, fields, AddLayer, DeleteLayer, Micro));
                var removeSurvey = Ui.Button("[−]", () => DeleteSurvey(rows));
                removeSurvey.FontSize = 11; removeSurvey.MinHeight = 20; removeSurvey.Padding = new Thickness(3, 0, 3, 0);
                removeSurvey.ToolTip = $"Elimina stratigrafia {index}";
                tab.Header = Ui.Bar(Ui.Text($"{index}", 12), removeSurvey);
                continue;
            }
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
    private JsonObject NewLayer() => Micro ? Archivio.NuovoStratoMicropalo() : Archivio.NuovoStratoPalo();
    private void DeleteSurvey(JsonArray survey, bool confirm = true)
    {
        var surveys = Data.Array("stratigrafie"); int index = surveys.IndexOf(survey);
        if (index < 0) return;
        if (confirm && MessageBox.Show(Window.GetWindow(this), $"Eliminare la stratigrafia {index + 1} e tutti i suoi strati?", "Stratigrafia", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Commit(); int selected = sondages.SelectedIndex;
        surveys.RemoveAt(index);
        RebuildSondages(index < selected ? selected - 1 : selected); Changed();
    }
    private void ChooseSurveyCopy(bool copyInto)
    {
        int current = sondages.SelectedIndex; var surveys = Data.Array("stratigrafie");
        if (current < 0) { MessageBox.Show(Window.GetWindow(this), "Selezionare una stratigrafia."); return; }
        var choices = Enumerable.Range(0, surveys.Count).Where(i => i != current)
            .Select(i => new KeyValuePair<int, string>(i, $"Stratigrafia {i + 1}")).ToList();
        if (copyInto) choices.Add(new(-1, "Nuova stratigrafia"));
        if (choices.Count == 0) { MessageBox.Show(Window.GetWindow(this), "Non ci sono altre stratigrafie da cui copiare."); return; }
        var selection = new ComboBox { ItemsSource = choices, DisplayMemberPath = "Value", SelectedValuePath = "Key", SelectedIndex = 0, Margin = new Thickness(8) };
        var dialog = Ui.Dialog(this, copyInto ? "Copia stratigrafia in…" : "Copia stratigrafia da…", new Grid(), 430, 210);
        var ok = Ui.Button("Copia", () => dialog.DialogResult = true, true); ok.IsDefault = true;
        var cancel = Ui.Button("Annulla", () => dialog.DialogResult = false); cancel.IsCancel = true;
        dialog.Content = Ui.Paper(Ui.Dock(selection, Ui.Text(copyInto ? $"Copia gli strati della stratigrafia {current + 1} in:" : $"Sostituisci gli strati della stratigrafia {current + 1} copiando da:"), Ui.Bar(ok, cancel)));
        if (dialog.ShowDialog() == true && selection.SelectedValue is int other)
            CopySurvey(copyInto ? current : other, copyInto ? other : current);
    }
    private bool CopySurvey(int source, int destination, bool confirm = true)
    {
        var surveys = Data.Array("stratigrafie");
        if (source < 0 || source >= surveys.Count || destination < -1 || destination >= surveys.Count || source == destination) return false;
        if (destination >= 0 && confirm && MessageBox.Show(Window.GetWindow(this),
            $"Sostituire tutti gli strati della stratigrafia {destination + 1} con una copia della stratigrafia {source + 1}?", "Copia stratigrafia", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return false;
        Commit(); var copy = surveys[source]!.DeepClone();
        if (destination == -1) { surveys.Add(copy); destination = surveys.Count - 1; }
        else surveys[destination] = copy;
        RebuildSondages(destination); Changed(); return true;
    }
    private void Preview()
    {
        if (Section) return;
        if (Micro)
        {
            string weight = "—";
            try { weight = Chs.Peso(Data["generali"].S("profilo_chs"), Data["generali"].D("diametro"), Data["generali"]!.Required("peso_specifico_palo", strict: true)).D("q_totale").ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("it-IT")); }
            catch (ArgumentException) { }
            generalForm.Set("__peso_lineare", weight, display: true);
            generalForm.Editors["__peso_lineare"].ToolTip = "Peso calcolato da diametro, profilo CHS e peso specifico del riempimento. La visualizzazione è arrotondata; il calcolo usa la precisione completa.";
        }
        var g = Data["generali"]!; var efficiency = Calcolo.Efficienza(Data); effLabel.Text = efficiency.S("errore") != "" ? efficiency.S("errore") : $"ηg,c = {efficiency.D("eta_compressione"):F3}\nηg,t = {efficiency.D("eta_trazione"):F3}";
        if (Calcolo.Verticali.TryGetValue(g.S("verticali_indagate"), out var xi)) { normativeForm.Set("__xi3", xi.Xi3.ToString("F2"), true); normativeForm.Set("__xi4", xi.Xi4.ToString("F2"), true); }
        if (Micro) { generalForm.Enable("percentuale_punta", g.B("considera_punta"), dim: true); normativeForm.Enable("sicurezza_base", g.B("considera_punta"), dim: true); }
        else { generalForm.Enable("sottotipo_palo_battuto", g.S("tipo_palo") == "Battuto", dim: true); generalForm.Enable("profondita_falda", g.B("presenza_falda"), dim: true); generalForm.Enable("considera_sottospinta", g.B("presenza_falda"), dim: true); }
        string method = Data["efficienza"].S("metodo"); foreach (string key in efficiencyForm.Editors.Keys.Where(k => k != "metodo")) efficiencyForm.ShowField(key, key.StartsWith("numero_") ? method is "Feld" or "Converse-Labarre" : key.StartsWith("interasse") ? method == "Converse-Labarre" : method == "Definita dall'utente");
        plot.EmptyMessage = g.D("lunghezza") > 0 ? "Completare i dati · calcolo automatico" : "Lunghezza: inserire un numero valido";
        UpdateProfile(); UpdatePileCoefficients(); BuildReferencePlot();
    }
    private void UpdateProfile()
    { if (Section) return; stratigraphy.SelectedIndex = sondages.SelectedIndex; stratigraphy.ShowAll = expanded == 5; stratigraphy.InvalidateVisual(); }
    private void UpdatePileCoefficients()
    {
        if (!Geo) return; var g = Data["generali"]!; double diameter = g.D("diametro");
        foreach (var grid in layerGrids)
        {
            double depth = 0; bool valid = true; int i = 0;
            foreach (var row in grid.Rows)
            {
                var d = row.Values;
                row.Output("__color", StratigraphyDrawing.LayerColors[i % StratigraphyDrawing.LayerColors.Length]);
                row.Output("__strato", d.S("strato", ((char)('A' + i++ % 26)).ToString()));
                if (Micro)
                {
                    string tau = "—";
                    if (!d.B("laterale_attiva", true)) tau = "0,0";
                    else
                    {
                        try
                        {
                            var parameter = BustamanteDoix.Parametro(d.S("terreno"), g.S("tipo_iniezione"), g.Required("pressione_iniezione", strict: true), d.Required("alpha", strict: true));
                            tau = parameter.D("s").ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
                        }
                        catch (ArgumentException) { /* Dati incompleti o fuori abaco: nessun valore applicabile. */ }
                    }
                    row.Output("__tau", tau);
                    continue;
                }
                var (k, mu) = Calcolo.CoefficientiLaterali(g, d); double? cu = J.Number(d["coesione_non_drenata"]), thickness = J.Number(d["spessore"]), phi = J.Number(d["angolo_attrito"]);
                row.Output("__k", k?.ToString("0.###") ?? "—"); row.Output("__mu", mu?.ToString("0.###") ?? "—"); row.Output("__alfa", d.S("tipologia") == "Coesivo" && cu >= 0 ? Calcolo.CoefficienteAlfa(g, cu.Value).ToString("0.###") : "—");
                valid &= thickness > 0; if (valid) depth += thickness!.Value;
                row.Output("__nq", valid && double.IsFinite(depth) && diameter > 0 && phi is not null ? Nq.Dettaglio(phi.Value, depth / diameter, diameter > .8).D("nq").ToString("0.###") : "—");
            }
        }
    }
    private void ShowGeoResults()
    {
        allSeries.Clear(); visibility.Clear();
        foreach (var (name, curve) in Result!["curve"]!.AsObject())
        {
            foreach (string branch in new[] { "progetto", "media", "minima" })
            {
                string key = branch + "_" + name, label = name.Replace("non_drenante", "Non dren.").Replace("drenante", "Dren.").Replace("compressione", "C").Replace("trazione", "T").Replace('_', ' ') + " · " + branch;
                string color = Micro ? name.EndsWith("trazione") ? "#9B59B6" : "#16703B" : name.Contains("non_drenante")
                    ? name.EndsWith("trazione") ? "#B779D6" : "#6A1B9A"
                    : name.EndsWith("trazione") ? "#73B84C" : "#16703B";
                var series = new Serie(label, curve!.Array(branch).Select(p => new[] { p![1]!.GetValue<double>(), p[0]!.GetValue<double>() }).ToList(), Ui.Brush(color), branch != "progetto", DashPattern: branch == "media" ? DashStyles.Dash : branch == "minima" ? DashStyles.Dot : null);
                allSeries.Add((key, series)); visibility[key] = Data["visibilita_grafici"].B(key, branch == "progetto");
            }
        }
        foreach (var (name, points) in Result["azioni"]!.AsObject()) if (points!.AsArray().Count > 0)
        { string key = "azione_" + name; allSeries.Add((key, new Serie("Ed " + name, points.AsArray().Select(p => new[] { p![1]!.GetValue<double>(), p[0]!.GetValue<double>() }).ToList(), Ui.Brush(name == "compressione" ? "#D32F2F" : "#29A9E0"), true))); visibility[key] = Data["visibilita_grafici"].B(key, true); }
        plot.Title = Micro ? "Capacità portante del micropalo" : "Capacità portante del palo"; plot.YLabel = Micro ? "Lungo asse s [m]" : "Profondità z [m]"; plot.CapacityDepth = Result.D("profondita_massima"); plot.ResetView(); UpdateVisible(); RebuildCurveChoices();
        tables = Tabelle.Crea(Result, Micro); var summary = new List<string[]>();
        foreach (var (key, c) in Result["curve"]!.AsObject())
        {
            var p = c!.Array("progetto")[^1]!; double rd = p[1]!.GetValue<double>(); var a = Result["azioni"]!.Array(key.Contains("trazione") ? "trazione" : "compressione").LastOrDefault(); double? ed = J.Number(a?[1]);
            summary.Add([key.Replace('_', ' '), Tabelle.F(p[0]), Tabelle.F(rd), ed.HasValue ? Tabelle.F(ed.Value) : "—", !Result.B("copertura_completa") ? "Verifica incompleta: stratigrafia insufficiente" : ed.HasValue ? ed <= rd ? "Verifica soddisfatta" : "Verifica non soddisfatta" : "Azione non inserita"]);
        }
        tables.Insert(0, new("Riepilogo alla quota disponibile", ["Verifica", Micro ? "s [m]" : "z [m]", "Rd [kN]", "Ed [kN]", "Esito"], summary));
        PopulateTables(); UpdateVerification(); BuildReferencePlot();
        if (!Result.B("copertura_completa")) SetWarnings("ATTENZIONE: stratigrafia insufficiente; risultati limitati alla quota disponibile, non alla punta richiesta.\n" + warnings.Text);
    }
    private void UpdateVerification()
    {
        if (Geo) { UpdatePileVerification(); return; }
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
        if (Pile) { table.FontSize = 15; table.RowHeight = 35; }
    }
    private void UpdatePileVerification()
    {
        var rows = new List<string[]>();
        foreach (var (key, label) in Micro ? new[] { ("compressione", "Compressione"), ("trazione", "Trazione") } : new[]
        {
            ("drenante_compressione", "Comp – Dre"), ("non_drenante_compressione", "Comp – Non Dre"),
            ("drenante_trazione", "Tra – Dre"), ("non_drenante_trazione", "Tra – Non Dre")
        })
        {
            double? rd = null, ed = null;
            if (Result?.B("copertura_completa") == true)
            {
                var resistance = Result["curve"]?[key]?["progetto"] as JsonArray;
                var actions = Result["azioni"]?[key.EndsWith("trazione") ? "trazione" : "compressione"] as JsonArray;
                rd = J.Number((resistance?.LastOrDefault() as JsonArray)?.ElementAtOrDefault(1));
                ed = J.Number((actions?.LastOrDefault() as JsonArray)?.ElementAtOrDefault(1));
            }
            bool valid = rd is double r && double.IsFinite(r) && r >= 0 && ed is double n && double.IsFinite(n) && n >= 0;
            string outcome = Micro && Result is not null && !Result.B("copertura_completa") ? "Verifica incompleta: stratigrafia insufficiente" : Micro && rd.HasValue && !ed.HasValue ? "Azione non inserita" : !valid ? "Da verificare" : ed <= rd ? "Verifica soddisfatta" : "Verifica non soddisfatta";
            rows.Add([label, ed?.ToString("N1") ?? "—", rd?.ToString("N1") ?? "—",
                valid && rd > 0 ? (100 * ed / rd)?.ToString("N1") + "%" : "—", outcome]);
        }
        var table = Ui.Table(["Condizione", "NEd [kN]", "Rd [kN]", "Util.", "Esito"], rows);
        table.FontSize = 15; table.RowHeight = double.NaN; table.MinRowHeight = 52;
        double[] weights = [1.4, .9, .9, .8, 1.8], minimums = [85, 52, 52, 48, 100];
        if (Micro) { weights = [1.8, .85, .85, .75, 1.9]; minimums = [100, 48, 48, 44, 96]; }
        for (int i = 0; i < table.Columns.Count; i++)
        {
            var column = (DataGridTextColumn)table.Columns[i];
            column.Width = new DataGridLength(weights[i], DataGridLengthUnitType.Star); column.MinWidth = minimums[i];
            var header = Ui.Text((string)column.Header, 12, true); header.TextAlignment = TextAlignment.Center; column.Header = header;
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, i is >= 1 and <= 3 || Micro && i == 0 ? TextWrapping.NoWrap : TextWrapping.Wrap));
            if (Micro && i == 0) style.Setters.Add(new Setter(TextBlock.FontSizeProperty, 13.0));
            if (i is >= 1 and <= 3) style.Setters.Add(new Setter(TextBlock.FontSizeProperty, 14.0));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(3, 5, 3, 5)));
            if (i == 4)
            {
                style.Setters.Add(new Setter(TextBlock.ForegroundProperty, Ui.Muted));
                style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
                foreach (var (value, color) in new[] { ("Verifica soddisfatta", "#16703B"), ("Verifica non soddisfatta", "#B42318") })
                {
                    var trigger = new DataTrigger { Binding = new Binding("[4]"), Value = value };
                    trigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, Ui.Brush(color))); style.Triggers.Add(trigger);
                }
            }
            column.ElementStyle = style;
        }
        verification.Content = table;
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
    private void BuildReferencePlot(int? selectedSurvey = null)
    {
        if (Section) return;
        var series = new List<Serie>(); Brush[] colors = [Ui.Blue, Brushes.SeaGreen, Brushes.DarkOrange, Brushes.Purple, Brushes.Brown, Brushes.Teal, Brushes.Crimson, Brushes.Gray]; int i = 0;
        reference.Title = Micro ? "Abachi Bustamante–Doix" : "Nq parametrizzato"; reference.XLabel = Micro ? "p_l [MPa]" : "φ [°]"; reference.YLabel = Micro ? "s [kPa]" : "Nq / Nq*"; reference.Markers = []; reference.Note = "";
        if (Micro)
        {
            int selected = selectedSurvey ?? sondages.SelectedIndex;
            var layers = selected >= 0 && selected < Data.Array("stratigrafie").Count ? Data.Array("stratigrafie")[selected]!.AsArray() : new JsonArray();
            string injection = Data["generali"].S("tipo_iniezione");
            var codes = layers.Where(l => l.B("laterale_attiva", true) && BustamanteDoix.Terreni.ContainsKey(l.S("terreno")))
                .Select(l => BustamanteDoix.Terreni[l.S("terreno")].Family + (injection == "IGU" ? "2" : "1")).ToHashSet();
            foreach (var (name, pts) in BustamanteDoix.Curve.Where(c => codes.Count == 0 || codes.Contains(c.Key)))
                series.Add(new(name, pts.Select(p => new[] { p.X, 1000 * p.Y }).ToList(), colors[i++ % colors.Length]));
            var segments = Result?.Array("dettagli").LastOrDefault()?.Array("sondaggi");
            if (segments is not null && selected >= 0 && selected < segments.Count)
                foreach (var segment in segments[selected]!.Array("tratti").Where(t => t.B("laterale_attiva", true) && t.S("curva") != "").DistinctBy(t => t.S("curva")))
                {
                    string code = segment.S("curva"); var color = series.First(s => s.Name == code).Color;
                    reference.Markers.Add(new(segment.D("pl"), segment.D("s"), code, color, ProjectToAxes: true,
                        XCaption: $"pl={segment.D("pl"):0.#}", YCaption: $"s\n{segment.D("s"):0.#}"));
                }
            reference.Note = $"Stratigrafia {selected + 1} · {injection} · {string.Join(", ", series.Select(s => s.Name))} · p_l = p_i" + (reference.Markers.Count == 0 ? " · punti disponibili dopo il calcolo" : "");
        }
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
                    reference.Markers.Add(new(phi, nq, $"S{selected + 1}: φ={phi:0.##}° · {(big ? "Nq*" : "Nq")}={nq:0.###}", Brushes.Crimson, ProjectToAxes: true));
                    reference.Note = $"Stratigrafia {selected + 1} · punta L={length:0.###} m · L/D={ratio:0.###}" + (detail.B("limite_phi") || detail.B("limite_rapporto") ? " · valore limitato al bordo dell'abaco" : "");
                }
                else reference.Note = $"L/D effettivo = {ratio:0.###} · punto disponibile con dati completi fino alla punta";
                reference.XMinimum = low; series.Add(new($"L/D effettivo = {ratio:0.###}", Enumerable.Range(0, 201).Select(k => { double phi = low + (high - low) * k / 200; return new[] { phi, Nq.Dettaglio(phi, ratio, big).D("nq") }; }).ToList(), Brushes.Crimson, Highlighted: true));
            }
        }
        reference.Series = series; reference.InvalidateVisual();
    }
}
