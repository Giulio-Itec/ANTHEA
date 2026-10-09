using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

/// <summary>Adaptive geotechnical workspace with automatic, revision-safe calculations.</summary>
internal sealed partial class HorizontalWorkspace : UserControl, IDisposable
{
    internal JsonObject Data { get; }
    private bool MicroHorizontal => Data.S("tipo_sezione") == "CHS";
    private readonly ChsDrawing chsDrawing = new() { Width = 400, Height = 400 };
    internal JsonObject? Result { get; private set; }
    private readonly ElasticPileWorkspace elastic;
    private readonly TabControl analyses = new();
    internal JsonObject? ActiveResult => analyses.SelectedIndex == 1 ? elastic.Result : Result;
    internal Task CalculateActiveAsync() => analyses.SelectedIndex == 1 ? elastic.CalculateAsync() : CalculateAsync();
    internal bool Busy { get; private set; }
    internal event Action? Modified;
    private bool disposed, building = true;
    private int revision;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly List<JsonGrid> grids = [];
    private readonly List<HorizontalSoilEditor> soilEditors = [];
    private readonly InputForm ejForm;
    private readonly TabControl surveys = new();
    private readonly Grid sectionLayout = new();
    private readonly Viewbox sectionPreview = new() { Stretch = Stretch.Uniform, Margin = new Thickness(8, 0, 0, 0) };
    private readonly StratigraphyDrawing profile = new() { ShowAll = false, HorizontalForces = true };
    private readonly SectionDrawing sectionDrawing = new();
    private readonly TextBlock summary = Ui.Text("Completare i dati · calcolo automatico", 14);
    private readonly TextBlock momentValue = Ui.Text("Momento da calcolare", 12, true);
    private readonly TextBlock status = Ui.Text("Dati da verificare", 12, color: Ui.Muted);
    private readonly TextBlock warnings = Ui.Text("", 12, color: Ui.Muted);
    private readonly Canvas layout = new();
    private readonly Button details, csv;
    private readonly InputForm general, model, factors, efficiency, moment, sectionFields;
    private readonly Expander advanced = new() { Header = "Opzioni avanzate", IsExpanded = false };
    private readonly TextBlock modelLabel = Ui.Text("Modello automatico · dati da completare", 13, true);

    internal HorizontalWorkspace(JsonObject data)
    {
        PaloOrizzontale.ValidateShape(data); ElasticHorizontalPile.PrepareShared(data); Data = data; profile.Data = data; SetValue(InputForm.CommitOnFocusLossProperty, false);
        foreach (var (key, value) in MicroHorizontal ? new JsonObject() : SezioneCA.DefaultInput())
            if (!data["sezione"]!.AsObject().ContainsKey(key)) data["sezione"]![key] = value?.DeepClone();
        var g = data["generali"]!.AsObject();
        if (!g.ContainsKey("metodo_calcolo")) g["metodo_calcolo"] = PaloOrizzontale.BromsMethod;
        g["metodo_calcolo"] = PaloOrizzontale.NormalizeMethod(g.S("metodo_calcolo"));
        g["modalita"] = "Automatica";
        details = Ui.Button("Diagrammi e dettagli", ShowResults, inspection: true); details.IsEnabled = false;
        csv = Ui.Button("Esporta CSV", ExportCsv, inspection: true); csv.IsEnabled = false;
        general = new(g, [new("diametro", MicroHorizontal ? "Diametro geotecnico micropalo" : "Diametro palo", "m", Symbol: "D"), new("lunghezza", "Lunghezza infissa", "m", Symbol: "L"),
            new("eccentricita", "Eccentricità forza sopra il piano campagna", "m", Symbol: "e"), new("tratto_libero", "Tratto libero sopra il terreno", "m"), new("vincolo", "Rotazione in testa", Choices: ["Libera", "Impedita"]),
            new("azione_orizzontale", "Forza orizzontale", "kN", Symbol: "HEd"), new("azione_assiale", "Forza assiale (+ compressione)", "kN", Symbol: "N"),
            new("presenza_falda", "Presenza falda", Bool: true), new("profondita_falda", "Profondità falda", "m"), new("gamma_acqua", "Peso unitario acqua", "kN/m³")], _ => Changed(), compact: true, symbolColumns: true);
        general.GroupFields("Dati opzionali · peso acqua",["gamma_acqua"]);
        model = new(g, [new("metodo_calcolo", "Metodo di calcolo", Choices: [PaloOrizzontale.BromsMethod, PaloOrizzontale.StratifiedMethod]),
            new("passo", "Passo diagrammi", "m", Symbol: "Δz"), new("tolleranza", "Tolleranza radici", Symbol: "ε")], _ => Changed(), compact: true, symbolColumns: true);
        ((Grid)model.Content).ColumnDefinitions[2].Width = new GridLength(150);
        advanced.Content = model; advanced.Expanded += (_, _) => LayoutCards(); advanced.Collapsed += (_, _) => LayoutCards();
        var coefficients = data["verifica"]!.AsObject();
        if (!coefficients.ContainsKey("verticali_indagate")) coefficients["verticali_indagate"] = "1";
        foreach (var (key, value) in new Dictionary<string, string> { ["efficienza_metodo"] = "Manuale", ["efficienza_eta"] = "1",
            ["interasse_anteriore"] = "", ["interasse_posteriore"] = "", ["interasse_sinistro"] = "", ["interasse_destro"] = "" })
            if (!coefficients.ContainsKey(key)) coefficients[key] = value;
        factors = new(coefficients, [new("verticali_indagate", "Verticali indagate", Choices: Calcolo.Verticali.Keys.ToArray()),
            new("__xi3", "Correlazione", Symbol: "ξ3", ReadOnly: true), new("__xi4", "Correlazione", Symbol: "ξ4", ReadOnly: true),
            new("__gamma_r", "Sicurezza resistenza", Symbol: "γR", ReadOnly: true)], _ => Changed(), compact: true, symbolColumns: true);
        efficiency = new(coefficients, [
            new("efficienza_metodo", "Efficienza", Choices: ["Manuale", "Reese & Van Impe (foglio)"]),
            new("efficienza_eta", "Efficienza manuale", Symbol: "η"),
            new("interasse_anteriore", "Interasse anteriore", "m"), new("interasse_posteriore", "Interasse posteriore", "m"),
            new("interasse_sinistro", "Interasse sinistro", "m"), new("interasse_destro", "Interasse destro", "m"),
            new("__eta", "Efficienza adottata", Symbol: "η", ReadOnly: true)], _ => Changed(), compact: true, symbolColumns: true);
        AddCard("Dati generali", Ui.Dock(general, bottom: advanced));
        AddCard("Efficienza", Ui.Dock(new ChainedScrollViewer { Content = efficiency, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, bottom: Ui.Text("Interassi riferiti alla direzione di H.\nMetodo del foglio: schema a 8 vicini.", 12, color: Ui.Muted)));
        AddCard("Coefficienti normativa", Ui.Dock(factors, bottom: Ui.Text("Rd = η · min(Hu,media / ξ3; Hu,min / ξ4) / 1,3\nRestano escluse le altre verifiche NTC/EC2.", 12, color: Ui.Muted)));
        modelLabel.Margin = new Thickness(0, 0, 0, 8);
        AddCard("Verifica", Ui.Dock(new ChainedScrollViewer { Content = Ui.Stack(modelLabel, verification, summary, CalculationDescription()), VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, bottom: Ui.Bar(details, csv)));
        RebuildSurveys(); surveys.SelectionChanged += (_, e) => { if (e.Source == surveys) { profile.SelectedIndex = surveys.SelectedIndex; profile.InvalidateVisual(); } };
        var surveyButtons = Ui.Bar(Ui.Button("+ Stratigrafia", () => { Commit(); Data.Array("stratigrafie").Add(new JsonArray(NewLayer())); RebuildSurveys(Data.Array("stratigrafie").Count - 1); Changed(); }),
            Ui.Button("− Stratigrafia", () => { if (surveys.SelectedIndex >= 0) DeleteSurvey(Data.Array("stratigrafie")[surveys.SelectedIndex]!.AsArray()); }),
            Ui.Button("Copia in…", () => ChooseSurveyCopy(true)), Ui.Button("Copia da…", () => ChooseSurveyCopy(false)));
        moment = new(g, [new("origine_momento", "Momento adottato da", Choices: [MicroHorizontal ? "Sezione CHS" : "Sezione c.a.", "Manuale"]),
            new("momento_resistente", "Momento manuale", "kNm", Symbol: "My"), new("provenienza_momento", "Natura / fonte My manuale")], _ => Changed(), compact: true, symbolColumns: true);
        var sectionData = data["sezione"]!.AsObject();
        if (MicroHorizontal) sectionFields = CreateChsForm(sectionData);
        else {
        // Preserve the previous perfect-plastic material unless a shared material already supplies its full law.
        RebarMaterial.CompleteLegacyInput(sectionData);
        if (!sectionData.ContainsKey("cls_diagramma")) sectionData["cls_diagramma"] = "Parabola-rettangolo";
        sectionData["shape"] = "Circolare";
        sectionData["diameter_mm"] = g.D("diametro", 1) * 1000;
        sectionData["classe_cls"] = ConcreteClass(sectionData.D("fck_mpa"));
        if(sectionData["coefficienti_unitari"]==null)sectionData["coefficienti_unitari"]=false;
        sectionFields = new InputForm(sectionData, [
            new("classe_cls", "Calcestruzzo", Choices: ConcreteMaterialCatalog.Concrete().Select(m=>m.S("nome")).Append(sectionData.S("classe_cls")).Append("Personalizzato").Distinct().ToArray()),
            new("fck_mpa", "fck del materiale", "MPa", ReadOnly:true),
            new("alpha_cc", "αcc archivio",ReadOnly:true), new("gamma_c", "γc archivio",ReadOnly:true),
            new("__fcd", "Resistenza di progetto a compressione", "MPa", Symbol: "fcd", ReadOnly: true),
            new("classe_acciaio","Acciaio", Choices:ConcreteMaterialCatalog.Steel(false,"NTC 2018").Select(m=>m.S("nome")).Append(sectionData.S("classe_acciaio")).Append("Personalizzato").Distinct().ToArray()),
            new("fyk_mpa", "fyk del materiale", "MPa", ReadOnly:true),
            new("gamma_s", "γs archivio",ReadOnly:true),
            new("coefficienti_unitari","Coefficienti unitari",Bool:true),
            new("__fyd", "Resistenza a snervamento di progetto", "MPa", Symbol: "fyd", ReadOnly: true),
            new("longitudinal_bar_diameter_mm", "Diametro barre", "mm", Symbol: "φL", Choices: ["8", "10", "12", "14", "16", "18", "20", "22", "24", "25", "26", "28", "30", "32", "36", "40"]),
            new("longitudinal_bar_count", "Numero barre", Symbol: "n"),
            new("second_inner_enabled", "Secondo anello interno", Bool: true),
            new("second_inner_count", "Numero barre dell'anello interno", Symbol: "n₂"),
            new("second_inner_diameter", "Diametro barre dell'anello interno", "mm", Symbol: "φL,2", Choices: ["8", "10", "12", "14", "16", "18", "20", "22", "24", "25", "26", "28", "30", "32", "36", "40"]),
            new("second_inner_gap", "Distanza libera dalla prima fila", "mm"),
            new("transverse_bar_diameter_mm", "Diametro staffa", "mm", Symbol: "φst", Choices: ["6", "8", "10", "12", "14", "16", "18", "20"]),
            new("transverse_spacing_mm", "Passo staffe", "mm"),
            new("gamma_ca", "Peso unitario c.a. adottato (acciaio incluso)", "kN/m³"),
            new("cover_mm", "Copriferro netto", "mm", Symbol: "c"),
            new("esposizione", "Esposizione", Choices: Ntc2018Checks.Exposures),
            new("vita_durabilita", "Vita utile per durabilità", "anni", Choices:["50","100"]),
            new("qualita_copriferro", "Controllo qualità copriferro confermato", Bool:true),
            new("steel_modulus_mpa", "Modulo elastico acciaio", "MPa", Symbol: "Es"),
            new("cls_diagramma", "Legame calcestruzzo", Choices: ConcreteMaterials.ConcreteDiagrams, Note: FieldNotes.StressBlock),
            new("steel_diagramma", "Legame acciaio", Choices: ["Elastoplastico", "Incrudente"]),
            new("steel_fu_mpa", "Resistenza ultima", "MPa", Symbol: "fu"),
            new("steel_eps_u", "Deformazione ultima", "‰", Symbol: "εu"),
            new("circular_sides", "Lati del contorno", Choices: ["16", "32", "64", "96", "128", "256"])], SectionMaterialChanged, compact: true, symbolColumns: true);
        sectionFields.GroupFields("Dati opzionali e proprietà materiali", ["fck_mpa","fyk_mpa","__fcd","__fyd","gamma_ca","esposizione","vita_durabilita","qualita_copriferro","steel_modulus_mpa","steel_fu_mpa","steel_eps_u","cls_diagramma","steel_diagramma","circular_sides"]);
        foreach(string key in new[]{"alpha_cc","gamma_c","gamma_s"})sectionFields.ShowField(key,false);
        sectionFields.Editors["coefficienti_unitari"].ToolTip="Disattivato: coefficienti NTC/archivio. Attivato: normativa custom di sezione con tutti i gamma e alpha_cc pari a 1; non modifica i coefficienti geotecnici di Broms. I valori ordinari restano conservati.";
        sectionFields.Editors["cover_mm"].ToolTip = "Distanza netta dal bordo del calcestruzzo alla superficie esterna della staffa, in mm.";
        sectionFields.Editors["second_inner_gap"].ToolTip = "Distanza libera tra le superfici delle barre dei due anelli, in mm.";
        ShowInnerRing(false);
        }
        moment.MaxWidth = sectionFields.MaxWidth = 650;
        moment.HorizontalAlignment = sectionFields.HorizontalAlignment = HorizontalAlignment.Left;
        sectionLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        sectionLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sectionDrawing.Width = 400; sectionDrawing.Height = 400;
        sectionPreview.Child = MicroHorizontal ? chsDrawing : sectionDrawing;
        Grid.SetColumn(sectionPreview, 1);
        var materialScroll=new ChainedScrollViewer{Content=sectionFields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var sectionAndResults=Ui.Dock(sectionPreview,bottom:momentValue);Grid.SetColumn(sectionAndResults,1);
        sectionLayout.Children.Add(materialScroll); sectionLayout.Children.Add(sectionAndResults);
        ejForm = new InputForm(sectionData, [new("ej_override", "EJ manuale motivato", Bool:true),new("ej_assegnato", "EJ assegnato", "kN m²"),new("ej_motivo", "Motivazione EJ", Wide:true)], _ => Changed(), compact:true);
        var sectionPane = Ui.Dock(sectionLayout, bottom:new Expander{Header="Opzioni avanzate · resistenza manuale ed EJ",Content=Ui.Stack(moment,ejForm)});
        AlignSurveyToolbar(surveyButtons);
        AddCard("Stratigrafia", surveys, true);
        AddCard("Profilo stratigrafico", profile, true);
        AddCard(MicroHorizontal ? "Sezione resistente CHS" : "Momento plastico / resistente", sectionPane, true);
        scroll.Content = layout;
        var warningPane = new Expander { Header = "Ipotesi e limiti del calcolo", Content = new ChainedScrollViewer { Content = warnings, MaxHeight = 90, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        var footer = Ui.Stack(Ui.Bar(status, Ui.Button("Modello e dati comuni…", () => CalculationHelpView.Show(this, true), inspection: true)), warningPane); footer.Margin = new Thickness(16, 3, 16, 8);
        elastic = new ElasticPileWorkspace(data, () => { analyses.SelectedIndex=0; expanded=4; LayoutCards(); }, RefreshAfterMigration); elastic.Modified += () => Modified?.Invoke();
        Ui.Tab(analyses, "Dati comuni e capacità · Broms / stratificato", Ui.Dock(scroll, bottom: footer));
        Ui.Tab(analyses, "Risposta elastica · trave su molle", elastic);
        analyses.SelectedIndex = data.S("vista_orizzontale") == "elastico" ? 1 : 0;
        analyses.SelectionChanged += (_, e) => { if(e.Source != analyses || building) return; Commit(); data["vista_orizzontale"] = analyses.SelectedIndex == 1 ? "elastico" : "capacita"; if(analyses.SelectedIndex == 1) timer.Stop(); else { LayoutCards(); if(Result is null) QueueCalculation(); } Modified?.Invoke(); };
        var engineReference = Ui.Text("Motore di calcolo: GPC Engine", 12, true, Ui.Muted);
        engineReference.Margin = new Thickness(16, 8, 16, 0); engineReference.HorizontalAlignment = HorizontalAlignment.Right;
        Content = Ui.Dock(analyses, top: engineReference);
        scroll.SizeChanged += (_, _) => LayoutCards();
        scroll.ScrollChanged += (_, e) => { if (e.Source == scroll && (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)) LayoutCards(); };
        timer.Tick += TimerTick;
        building = false; Preview(); UpdateVerification(); LayoutCards(); QueueCalculation();
    }
    private void RebuildSurveys(int selected = 0)
    {
        surveys.Items.Clear(); grids.Clear(); soilEditors.Clear(); int index = 0;
        foreach (var node in Data.Array("stratigrafie"))
        {
            var rows = node!.AsArray();
            Field[] fields = [new("tipologia", "Terreno", Choices: ["Granulare", "Coesivo"]),
                new("__kp", "Kp [−]", ReadOnly: true),
                new("spessore", "Spessore [m]"), new("peso_specifico", "γ [kN/m³]"), new("peso_specifico_saturo", "γsat [kN/m³]"),
                new("angolo_attrito", "φ′ [°]"), new("coesione_non_drenata", "Cu [kPa]"), new("coesione_efficace", "c′ [kPa]")];
            var grid = new JsonGrid(fields); grids.Add(grid);
            JsonRow Bind(JsonObject data)
            {
                var display = (JsonObject)data.DeepClone();
                return new(display, key => { data[key] = display[key]?.DeepClone();Changed();grid.Rows.FirstOrDefault(r=>ReferenceEquals((r.Context as SoilRowContext)?.Layer,data))?.Refresh(); }){Context=new SoilRowContext(data,Changed)};
            }
            foreach (var row in rows.OfType<JsonObject>()) grid.Rows.Add(Bind(row));
            void AddLayer() { grid.Commit(); var row = NewLayer(); rows.Add(row); grid.Rows.Add(Bind(row)); Changed(); }
            void DeleteLayer(JsonRow row) { grid.Commit(); int i = grid.Rows.IndexOf(row); if (i < 0) return; rows.RemoveAt(i); grid.Rows.RemoveAt(i); Changed(); }
            var table = StratigraphyTable.Build(grid, fields, AddLayer, DeleteLayer, gammaFallback: false);
            grid.FrozenColumnCount=2;grid.EnableColumnVirtualization=false;
            grid.Columns.Insert(2,new DataGridTemplateColumn{Header=HorizontalSoilEditor.Header(),Width=HorizontalSoilEditor.Widths.Sum(),CellTemplate=new DataTemplate{VisualTree=new FrameworkElementFactory(typeof(HorizontalSoilEditor))}});
            var tab = Ui.Tab(surveys, $"{++index}", table);
            var remove = Ui.Button("[−]", () => DeleteSurvey(rows)); remove.FontSize = 11; remove.Padding = new Thickness(3, 0, 3, 0); remove.MinHeight = 20;
            remove.ToolTip = $"Elimina stratigrafia {index}";
            tab.Header = Ui.Bar(Ui.Text($"{index}", 12), remove);
        }
        surveys.SelectedIndex = Math.Clamp(selected, 0, surveys.Items.Count - 1); profile.SelectedIndex = surveys.SelectedIndex;
    }
    private void RefreshAfterMigration()
    {
        building=true;
        try{foreach(var form in new[]{general,sectionFields,ejForm}){var source=ReferenceEquals(form,general)?Data["generali"]!:Data["sezione"]!;foreach(string key in form.Editors.Keys.ToArray()){if(form.Editors[key] is CheckBox box)box.IsChecked=source.B(key);else form.Set(key,source.S(key),true);}}RebuildSurveys();}
        finally{building=false;}Changed();
    }
    private void Preview()
    {
        if (MicroHorizontal) UpdateChsPreview(); else UpdateSectionMaterialValues();
        try { modelLabel.Text = "Modello automatico: " + PaloOrizzontale.ModelloAutomatico(Data); modelLabel.ToolTip = "Scelta dalle proprietà effettive lungo il palo e dalla falda, non dal numero di righe."; }
        catch (ArgumentException ex) { modelLabel.Text = "Modello automatico · dati da completare"; modelLabel.ToolTip = ex.Message; }
        var g = Data["generali"]!; general.Enable("profondita_falda", g.B("presenza_falda"), dim: true);
        // Keep a legacy nonzero eccentricity editable so it can be corrected, never silently overwrite it.
        general.Enable("eccentricita", true);
        bool manual = g.S("origine_momento") == "Manuale";
        moment.Enable("momento_resistente", manual, dim: true); moment.Enable("provenienza_momento", manual, dim: true);
        sectionFields.IsEnabled = true; sectionFields.Opacity = 1; ejForm?.ShowField("ej_assegnato",Data["sezione"].B("ej_override")); ejForm?.ShowField("ej_motivo",Data["sezione"].B("ej_override"));
        if (Calcolo.Verticali.TryGetValue(Data["verifica"].S("verticali_indagate", "1"), out var xi))
        { factors.Set("__xi3", xi.Xi3.ToString("F2"), true); factors.Set("__xi4", xi.Xi4.ToString("F2"), true); }
        factors.Set("__gamma_r", "1,30", true);
        bool manualEfficiency = Data["verifica"].S("efficienza_metodo") == "Manuale";
        efficiency.Enable("efficienza_eta", manualEfficiency, dim: true);
        foreach (string key in new[] { "interasse_anteriore", "interasse_posteriore", "interasse_sinistro", "interasse_destro" })
            efficiency.Enable(key, !manualEfficiency, dim: true);
        try { efficiency.Set("__eta", PaloOrizzontale.Efficiency(Data).D("eta").ToString("F3"), true); }
        catch (ArgumentException) { efficiency.Set("__eta", "—", true); }
        foreach (var grid in grids) for (int i = 0; i < grid.Rows.Count; i++)
        {
            grid.Rows[i].Output("__strato", ((char)('A' + i % 26)).ToString());
            grid.Rows[i].Output("__color", StratigraphyDrawing.LayerColors[i % StratigraphyDrawing.LayerColors.Length]);
            var row = grid.Rows[i].Values;
            double? phi = J.Number(row["angolo_attrito"]);
            grid.Rows[i].Output("__kp", row.S("tipologia") == "Granulare" && phi is >= 0 and < 90
                ? PaloOrizzontale.PassivePressureCoefficient(phi.Value).ToString("F1") : "—");
        }
        profile.InvalidateVisual();
        if (!MicroHorizontal) try
        {
            var input = (JsonObject)Data["sezione"]!.DeepClone(); input["shape"] = "Circolare"; input["diameter_mm"] = g.Required("diametro", strict: true) * 1000;
            if (input.D("longitudinal_bar_count") > 512) throw new ArgumentException("Numero barre fuori campo");
            var section = new SezioneCA(input, 12, 36); sectionDrawing.Outline = section.Outline; sectionDrawing.Bars = section.Bars;sectionDrawing.CircularLinkRadius=GPC.Checkers.Concrete.Piles.PileReinforcement.CircularLinkRadius(input.D("diameter_mm"),input.D("cover_mm"),input.D("transverse_bar_diameter_mm"));sectionDrawing.LinkDiameter=input.D("transverse_bar_diameter_mm");sectionDrawing.ShowAxes=true;sectionDrawing.Caption="Sezione principale · staffa verde";
        }
        catch (ArgumentException) { sectionDrawing.Outline = []; sectionDrawing.Bars = [];sectionDrawing.CircularLinkRadius=0; }
        sectionDrawing.InvalidateVisual();
    }
    private void Changed()
    {
        if (building || disposed) return;
        revision++; Result = null; summary.Text = "Dati modificati · aggiornamento automatico"; momentValue.Text = "Aggiornamento automatico del momento…";
        details.IsEnabled = csv.IsEnabled = false; warnings.Text = ""; elastic?.InvalidateShared(); Preview(); UpdateVerification(); QueueCalculation(); Modified?.Invoke();
    }
    internal void Commit() { general.Commit(); model.Commit(); factors.Commit(); efficiency.Commit(); moment.Commit(); sectionFields.Commit(); ejForm?.Commit(); foreach(var editor in soilEditors)editor.Commit(); foreach (var grid in grids) grid.Commit(); elastic?.Commit(); }
    private void QueueCalculation()
    {
        if (building || disposed || analyses.SelectedIndex == 1) return;
        timer.Stop(); timer.Start(); status.Text = "Aggiornamento automatico in attesa…";
    }
    private async void TimerTick(object? sender, EventArgs args)
    {
        if (Busy) return;
        timer.Stop(); if (!disposed) await CalculateAsync(commitEdits: false);
    }
    private void DisplayMoment(JsonObject result)
    {
        if (MicroHorizontal) { momentValue.Text = $"My(N) = {result.D("momento_knm"):N1} kNm · N = {result.D("n_kn"):N1} kN\nCHS classe {result.D("classe")} · solo acciaio · interazione N–M lineare"; return; }
        momentValue.Text = $"MRd = {result.D("momento_knm"):N1} kNm · N = {result.D("n_kn"):N1} kN\nChecker · {result.D("lati_contorno"):0} lati · residuo N = {result.D("residuo_n_kn"):G3} kN (tol. {result.D("tolleranza_n_kn"):G3})";
        momentValue.Text+="\n"+result.S("normativa","NTC 2018");momentValue.ToolTip = result.S("modello");
    }
    internal async Task CalculateAsync(bool commitEdits = true)
    {
        if (Busy || disposed) return;
        if (commitEdits) Commit(); timer.Stop();
        var snapshot = (JsonObject)Data.DeepClone(); int requested = revision; Busy = true;
        snapshot.Remove("vista_orizzontale");
        status.Text = "Calcolo di sezione ed equilibri in corso…";
        try
        {
            var computed = await Task.Run(() =>
            {
                var capacity = CalculationService.Calculate(MicroHorizontal ? MicropaloOrizzontale.Module : PaloOrizzontale.Module, snapshot);
                JsonObject? section = capacity["sezione"] as JsonObject; string sectionError = "";
                // Show a valid section moment even while the stratigraphy is still incomplete.
                if (section is null && snapshot["generali"].S("origine_momento") is "Sezione c.a." or "Sezione CHS")
                    try { section = PaloOrizzontale.Section(snapshot); } catch (ArgumentException ex) { sectionError = ex.Message; }
                return (capacity, section, sectionError);
            });
            if (disposed || requested != revision) return;
            var result = computed.capacity;
            if (computed.section is not null) DisplayMoment(computed.section);
            else if (snapshot["generali"].S("origine_momento") == "Manuale" && J.Number(snapshot["generali"]!["momento_resistente"]) is double manual && manual > 0)
                momentValue.Text = $"My inserito manualmente = {manual:N1} kNm";
            else momentValue.Text = "Momento non disponibile" + (computed.sectionError == "" ? "" : ": " + computed.sectionError);
            Result = result.S("errore") == "" ? result : null;
            if (Result is null) { summary.Text = "Calcolo non disponibile: " + result.S("errore"); warnings.Text = result.S("errore"); status.Text = "Correggere i dati indicati"; details.IsEnabled = csv.IsEnabled = false; UpdateVerification(); return; }
            if (result["sezione"] is JsonObject section) DisplayMoment(section);
            else momentValue.Text = $"My adottato manualmente = {result.D("momento_resistente_knm"):N1} kNm";
            summary.Text = $"{result.S("meccanismo")} · stratigrafia {result.D("sondaggio_governante")}\nMy = {result.D("momento_resistente_knm"):N1} kNm\nHEd / Hu = {100 * result.D("rapporto_meccanico"):0.0}%\nVerifica normativa incompleta." +
                (result.B("sperimentale") ? "\nMODELLO SPERIMENTALE" : "");
            UpdateVerification();
            warnings.Text = string.Join("\n", result.Array("avvisi").Select(v => v!.ToString()));
            status.Text = "Calcolo completato · diagrammi alla capacità ultima"; details.IsEnabled = csv.IsEnabled = true;
        }
        catch (Exception ex) { if (!disposed && requested == revision) { Result = null; summary.Text = "Calcolo fallito"; momentValue.Text = "Momento non disponibile"; status.Text = ex.Message; details.IsEnabled = csv.IsEnabled = false; UpdateVerification(); } }
        finally { Busy = false; if (!disposed && requested != revision) QueueCalculation(); }
    }
    private TabControl ResultsTabs()
    {
        var tabs = new TabControl(); int index = 0;
        foreach (var result in Result!.Array("sondaggi"))
        {
            var panel = new TabControl();
            bool truncated = StratigraphyDrawing.ReactionLimit(result!) is not null;
            var diagram = result!.Array("diagrammi");
            Ui.Tab(panel, "Terreno e tensioni", new ScrollViewer { Content = new HorizontalDiagrams(Result!, index, true) { MinWidth = 1000, MinHeight = 620 }, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Ui.Tab(panel, "Equilibrio del palo", new ScrollViewer { Content = new HorizontalDiagrams(Result!, index, false) { MinWidth = 1000, MinHeight = 620 }, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            var terrainRows = result.Array("diagramma_terreno").Select(r => new[] { r.D("z").ToString("0.000"), r.S("lato"), r.D("strato").ToString("0"),
                r!["sigma_v_kpa"] is null ? "—" : r.D("sigma_v_kpa").ToString("0.00"), r.D("u_kpa").ToString("0.00"),
                r["sigma_eff_kpa"] is null ? "—" : r.D("sigma_eff_kpa").ToString("0.00"), r.D("q_lim_kpa").ToString("0.00"), r.D("p_lim_kn_m").ToString("0.00"), r.D("q_integrale_kn").ToString("0.00") });
            Ui.Tab(panel, "Tabella terreno", CenteredTable(["z [m]", "Lato", "Strato", "σv [kPa]", "u [kPa]", "σ′v [kPa]", "q_lim [kPa]", "p_lim [kN/m]", "Q [kN]"], terrainRows));
            var rows = diagram.Select(r => new[] { r.D("z").ToString("0.0"), r.S("lato"), r.D("p_kn_m").ToString("0.0"), r.D("v_kn").ToString("0.0"), r.D("m_knm").ToString("0.0") });
            Ui.Tab(panel, "Tabella", CenteredTable(["z [m]", "Lato", "p [kN/m]", "V [kN]", "M [kNm]"], rows));
            var candidates = CenteredTable(["Meccanismo", "H [kN]", "Stato"], result.Array("candidati").Select(r => new[] { r.S("meccanismo"), r!["capacita_kn"] is null ? "—" : r.D("capacita_kn").ToString("N1"), r.S("stato") }));
            Ui.Tab(panel, "Meccanismi", candidates);
            var hinges = string.Join("; ", result.Array("cerniere_m").Select(v => (J.Number(v) ?? 0).ToString("0.0")));
            var info = Ui.Text($"{result.S("meccanismo")} · Hu {result.D("capacita_kn"):N1} kN · |M|max {result.D("momento_massimo_knm"):N1} kNm a z={result.D("quota_momento_massimo_m"):0.0} m\n" +
                $"Residui: H {result.D("residuo_forza_kn"):0.0} kN · M {result.D("residuo_momento_knm"):0.0} kNm · {result.S("convergenza")}\n" +
                $"Cerniere z [m]: {hinges} · " + (truncated ? "Tratto sotto cerniera mostrato tratteggiato: completamento idealizzato dell'equilibrio." : $"F concentrata {result.D("risultante_concentrata_kn"):N1} kN a z={result.D("quota_risultante_m"):0.0} m"), 12);
            info.Margin = new Thickness(12);
            Ui.Tab(tabs, $"Sondaggio {++index}", Ui.Dock(panel, info));
        }
        Ui.Tab(tabs, "Riferimenti", ReferencesPanel());
        Ui.Tab(tabs, "JSON", new TextBox { Text = Result!.ToJsonString(J.Options), IsReadOnly = true, AcceptsReturn = true, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        return tabs;
    }
    private void ShowResults() { if (Result is not null) Ui.Dialog(this, "Palo orizzontale · risultati", ResultsTabs(), 1280, 900).ShowDialog(); }
    private void ExportCsv()
    {
        if (Result is null) return;
        var save = new SaveFileDialog { Filter = "Tabelle CSV|*.csv", FileName = "Palo_orizzontale.csv" };
        if (save.ShowDialog(Window.GetWindow(this)) != true) return;
        try { Archivio.ScriviAtomico(save.FileName, Encoding.UTF8.GetBytes(PaloOrizzontale.Csv(Result))); }
        catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, "Esportazione non riuscita"); }
    }
    public void Dispose() { disposed = true; timer.Stop(); timer.Tick -= TimerTick; elastic.Dispose(); }
}
