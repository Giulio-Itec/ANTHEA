using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

/// <summary>Same seven-panel organization as the vertical pile; section replaces capacity charts.</summary>
internal sealed class HorizontalWorkspace : UserControl, IDisposable
{
    internal JsonObject Data { get; }
    internal JsonObject? Result { get; private set; }
    internal bool Busy { get; private set; }
    internal event Action? Modified;
    private bool disposed, building = true;
    private readonly List<JsonGrid> grids = [];
    private readonly TabControl surveys = new(), momentTabs = new();
    private readonly StratigraphyDrawing profile = new() { ShowAll = false };
    private readonly SectionDrawing sectionDrawing = new();
    private readonly TextBlock summary = Ui.Text("Inserire i dati e premere Calcola", 14);
    private readonly TextBlock momentValue = Ui.Text("Momento da calcolare", 14, true);
    private readonly TextBlock status = Ui.Text("Dati da verificare", 12, color: Ui.Muted);
    private readonly TextBlock warnings = Ui.Text("", 12, color: Ui.Muted);
    private readonly Grid layout = new();
    private readonly Button calculate, sectionCalculate, details, csv;
    private readonly InputForm general, model, factors, moment;

    internal HorizontalWorkspace(JsonObject data)
    {
        PaloOrizzontale.ValidateShape(data); Data = data; profile.Data = data;
        foreach (var (key, value) in SezioneCA.DefaultInput())
            if (!data["sezione"]!.AsObject().ContainsKey(key)) data["sezione"]![key] = value?.DeepClone();
        var g = data["generali"]!.AsObject();
        calculate = Ui.Button("Calcola capacità orizzontale", async () => await CalculateAsync(), true); calculate.VerticalAlignment = VerticalAlignment.Center;
        sectionCalculate = Ui.Button("Calcola momento", async () => await CalculateSectionAsync(), true);
        details = Ui.Button("Diagrammi e dettagli", ShowResults); details.IsEnabled = false;
        csv = Ui.Button("Esporta CSV", ExportCsv); csv.IsEnabled = false;
        general = new(g, [new("diametro", "Diametro palo D", "m"), new("lunghezza", "Lunghezza infissa L", "m"),
            new("eccentricita", "Quota forza sopra terreno e", "m"), new("vincolo", "Rotazione in testa", Choices: ["Libera", "Impedita"]),
            new("azione_orizzontale", "Forza orizzontale HEd", "kN"), new("azione_assiale", "N costante (+ compressione)", "kN"),
            new("presenza_falda", "Presenza falda", Bool: true), new("profondita_falda", "Profondità falda", "m")], _ => Changed());
        model = new(g, [new("modalita", "Modello terreno", Choices: ["Omogeneo", "Multistrato sperimentale"]),
            new("passo", "Passo diagrammi", "m"), new("tolleranza", "Tolleranza radici")], _ => Changed(), compact: true, wideChoices: true);
        factors = new(data["verifica"]!.AsObject(), [new("applica_fattori", "Applica fattori manuali", Bool: true),
            new("xi", "Divisore Hu → Rk"), new("gamma_r", "Divisore Rk → Rd"), new("riferimento", "Fonte / criterio adottato")], _ => Changed(), compact: true);
        var top = Columns([.32, .19, .24, .25]); top.Height = 345;
        Add(top, 0, "Dati generali", general);
        Add(top, 1, "Modello di calcolo", Ui.Dock(model, bottom: Ui.Text("Palo singolo · Broms\nH crescente, e e N costanti.\nTesta impedita: e = 0.\nNessun momento indipendente.", 12, color: Ui.Muted)));
        Add(top, 2, "Coefficienti / normativa", Ui.Dock(factors, bottom: Ui.Text("Rk e Rd solo con fattori documentati dall'utente.\nVerifica normativa NTC/EC2 incompleta; nessun esito di conformità automatica.", 12, color: Ui.Muted)));
        Add(top, 3, "Verifica", Ui.Dock(new ScrollViewer { Content = summary, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, bottom: Ui.Stack(details, csv)));
        RebuildSurveys(); surveys.SelectionChanged += (_, e) => { if (e.Source == surveys) { profile.SelectedIndex = surveys.SelectedIndex; profile.InvalidateVisual(); } };
        var surveyButtons = Ui.Bar(Ui.Button("+ Sondaggio", () => { Commit(); Data.Array("stratigrafie").Add(new JsonArray()); RebuildSurveys(Data.Array("stratigrafie").Count - 1); Changed(); }),
            Ui.Button("− Sondaggio", () =>
            {
                if (surveys.SelectedIndex < 0 || Data.Array("stratigrafie").Count == 1) return;
                if (MessageBox.Show(Window.GetWindow(this), "Eliminare il sondaggio selezionato?", "Palo orizzontale", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                Commit(); Data.Array("stratigrafie").RemoveAt(surveys.SelectedIndex); RebuildSurveys(); Changed();
            }));
        moment = new(g, [new("origine_momento", "Momento adottato da", Choices: ["Sezione c.a.", "Manuale"]),
            new("momento_resistente", "Momento manuale My", "kNm"), new("provenienza_momento", "Natura / fonte My manuale")], _ => Changed(), compact: true, wideChoices: true);
        var sectionFields = new InputForm(data["sezione"]!.AsObject(), [new("cover_mm", "Copriferro esterno staffa", "mm"),
            new("transverse_bar_diameter_mm", "Diametro staffa", "mm"), new("longitudinal_bar_count", "Numero barre"),
            new("longitudinal_bar_diameter_mm", "Diametro barre", "mm"), new("fck_mpa", "fck", "MPa"), new("fyk_mpa", "fyk", "MPa"),
            new("alpha_cc", "αcc"), new("gamma_c", "γc"), new("gamma_s", "γs"), new("steel_modulus_mpa", "Es", "MPa")], _ => Changed(), compact: true);
        Ui.Tab(momentTabs, "Materiali e armature", sectionFields); Ui.Tab(momentTabs, "Sezione", sectionDrawing);
        var sectionPane = Ui.Dock(momentTabs, moment, Ui.Stack(sectionCalculate, momentValue));
        var bottom = Columns([.44, .19, .37]); bottom.Height = 385;
        Add(bottom, 0, "Stratigrafia", Ui.Dock(surveys, surveyButtons));
        Add(bottom, 1, "Profilo stratigrafico", profile);
        Add(bottom, 2, "Momento plastico / resistente", sectionPane);
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(top); Grid.SetRow(bottom, 1); layout.Children.Add(bottom); layout.MinWidth = 1330; layout.Margin = new Thickness(14);
        var scroll = new ScrollViewer { Content = layout, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var footer = Ui.Dock(status, bottom: new ScrollViewer { Content = warnings, MaxHeight = 90, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        DockPanel.SetDock(calculate, Dock.Right); footer.Children.Insert(0, calculate); footer.Margin = new Thickness(22, 3, 22, 8);
        Content = Ui.Dock(scroll, bottom: footer); building = false; Preview();
    }
    private static Grid Columns(double[] widths)
    {
        var grid = new Grid(); foreach (double w in widths) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) }); return grid;
    }
    private static void Add(Grid grid, int column, string title, UIElement content)
    {
        var heading = Ui.Text(title, 16, true); heading.Margin = new Thickness(0, 0, 0, 12);
        var card = Ui.Paper(Ui.Dock(content, heading), 14); card.Margin = new Thickness(6); Grid.SetColumn(card, column); grid.Children.Add(card);
    }
    private void RebuildSurveys(int selected = 0)
    {
        surveys.Items.Clear(); grids.Clear(); int index = 0;
        foreach (var node in Data.Array("stratigrafie"))
        {
            var rows = node!.AsArray(); var grid = new JsonGrid([new("tipologia", "Terreno", Choices: ["Granulare", "Coesivo"]),
                new("spessore", "Spessore [m]"), new("peso_specifico", "γ [kN/m³]"), new("peso_specifico_saturo", "γsat [kN/m³]"),
                new("angolo_attrito", "φ′ [°]"), new("coesione_non_drenata", "Cu [kPa]"), new("coesione_efficace", "c′ [kPa]")]);
            grids.Add(grid); foreach (var row in rows.OfType<JsonObject>()) grid.Rows.Add(new(row, _ => Changed()));
            var buttons = Ui.Bar(Ui.Button("+ Strato", () => { grid.Commit(); var row = PaloOrizzontale.Layer(); rows.Add(row); grid.Rows.Add(new(row, _ => Changed())); Changed(); }),
                Ui.Button("− Strato", () => { grid.Commit(); int i = grid.SelectedIndex; if (i < 0) return; rows.RemoveAt(i); grid.Rows.RemoveAt(i); Changed(); }));
            Ui.Tab(surveys, $"Sondaggio {++index}", Ui.Dock(grid, bottom: Ui.Stack(buttons, Ui.Text("Granulare: φ′, γ, γsat; c′ = 0. Coesivo non drenato: Cu.\nSequenze miste non supportate. Ogni sondaggio deve coprire L.", 11, color: Ui.Muted))));
        }
        surveys.SelectedIndex = Math.Clamp(selected, 0, surveys.Items.Count - 1); profile.SelectedIndex = surveys.SelectedIndex;
    }
    private void Preview()
    {
        var g = Data["generali"]!; general.Enable("profondita_falda", g.B("presenza_falda"));
        bool manual = g.S("origine_momento") == "Manuale";
        moment.ShowField("momento_resistente", manual); moment.ShowField("provenienza_momento", manual);
        foreach (string key in new[] { "xi", "gamma_r", "riferimento" }) factors.Enable(key, Data["verifica"].B("applica_fattori"));
        profile.InvalidateVisual();
        try
        {
            var input = (JsonObject)Data["sezione"]!.DeepClone(); input["shape"] = "Circolare"; input["diameter_mm"] = g.Required("diametro", strict: true) * 1000;
            if (input.D("longitudinal_bar_count") > 512) throw new ArgumentException("Numero barre fuori campo");
            var section = new SezioneCA(input, 12, 36); sectionDrawing.Outline = section.Outline; sectionDrawing.Bars = section.Bars;
        }
        catch (ArgumentException) { sectionDrawing.Outline = []; sectionDrawing.Bars = []; }
        sectionDrawing.InvalidateVisual();
    }
    private void Changed()
    {
        if (building || disposed) return;
        Result = null; summary.Text = "Dati modificati · risultati da ricalcolare"; momentValue.Text = "Momento da ricalcolare";
        details.IsEnabled = csv.IsEnabled = false; warnings.Text = ""; status.Text = "Dati modificati · premere Calcola"; Preview(); Modified?.Invoke();
    }
    internal void Commit() { foreach (var grid in grids) grid.Commit(); }
    private void SetBusy(bool value) { Busy = value; layout.IsEnabled = !value; calculate.IsEnabled = !value; }
    private void DisplayMoment(JsonObject result)
    {
        momentValue.Text = $"My = {result.D("momento_knm"):N2} kNm · N = {result.D("n_kn"):N1} kN\nx = {result.D("asse_neutro_mm"):N1} mm · scarto mesh = {100 * result.D("scarto_mesh"):0.000}%";
    }
    private async Task CalculateSectionAsync()
    {
        if (Busy || disposed) return; Commit(); var snapshot = (JsonObject)Data.DeepClone(); SetBusy(true);
        try { var section = await Task.Run(() => PaloOrizzontale.Section(snapshot)); if (!disposed) { DisplayMoment(section); status.Text = "Momento calcolato; premere Calcola capacità per aggiornare la verifica"; } }
        catch (Exception ex) { if (!disposed) { momentValue.Text = "Calcolo sezione non disponibile"; status.Text = ex.Message; } }
        finally { if (!disposed) SetBusy(false); }
    }
    internal async Task CalculateAsync()
    {
        if (Busy || disposed) return; Commit(); var snapshot = (JsonObject)Data.DeepClone(); SetBusy(true);
        status.Text = "Calcolo di sezione ed equilibri in corso…";
        try
        {
            var result = await Task.Run(() => PaloOrizzontale.Calculate(snapshot)); if (disposed) return;
            Result = result.S("errore") == "" ? result : null;
            if (Result is null) { summary.Text = "Calcolo non disponibile"; warnings.Text = result.S("errore"); status.Text = "Correggere i dati indicati"; details.IsEnabled = csv.IsEnabled = false; return; }
            if (result["sezione"] is JsonObject section) DisplayMoment(section);
            else momentValue.Text = $"My adottato manualmente = {result.D("momento_resistente_knm"):N2} kNm";
            summary.Text = $"Hu = {result.D("capacita_kn"):N2} kN\n{result.S("meccanismo")} · sondaggio {result.D("sondaggio_governante")}\nMy = {result.D("momento_resistente_knm"):N2} kNm\nHEd / Hu = {result.D("rapporto_meccanico"):0.000}\n" +
                (result["resistenza_progetto_manuale_kn"] is null ? "\nRd non determinata\nVerifica normativa incompleta" : $"\nRk manuale = {result.D("resistenza_caratteristica_manuale_kn"):N2} kN\nRd manuale = {result.D("resistenza_progetto_manuale_kn"):N2} kN\n{result.S("esito_manuale")}") +
                (result.B("sperimentale") ? "\n\nMULTISTRATO SPERIMENTALE" : "");
            warnings.Text = string.Join("\n", result.Array("avvisi").Select(v => v!.ToString()));
            status.Text = "Calcolo completato · diagrammi alla capacità ultima"; details.IsEnabled = csv.IsEnabled = true;
        }
        catch (Exception ex) { if (!disposed) { Result = null; summary.Text = "Calcolo fallito"; status.Text = ex.Message; details.IsEnabled = csv.IsEnabled = false; } }
        finally { if (!disposed) SetBusy(false); }
    }
    private TabControl ResultsTabs()
    {
        var tabs = new TabControl(); int index = 0;
        foreach (var result in Result!.Array("sondaggi"))
        {
            var panel = new TabControl();
            foreach (var (key, title, unit) in new[] { ("p_kn_m", "Reazione resistente p", "kN/m"), ("v_kn", "Taglio V", "kN"), ("m_knm", "Momento M", "kNm") })
            {
                var plot = new Plot { Title = title + " alla capacità ultima", XLabel = $"{title} [{unit}]", Note = "p positiva opposta a H; risultante concentrata F indicata nel riepilogo." };
                plot.Series = [new(title, result.Array("diagrammi").Select(r => new[] { r.D(key), r.D("z") }).ToList(), Ui.Blue)];
                Ui.Tab(panel, title, plot);
            }
            var rows = result.Array("diagrammi").Select(r => new[] { r.D("z").ToString("0.###"), r.S("lato"), r.D("p_kn_m").ToString("0.###"), r.D("v_kn").ToString("0.###"), r.D("m_knm").ToString("0.###") });
            Ui.Tab(panel, "Tabella", Ui.Table(["z [m]", "Lato", "p [kN/m]", "V [kN]", "M [kNm]"], rows));
            var candidates = Ui.Table(["Meccanismo", "H [kN]", "Stato"], result.Array("candidati").Select(r => new[] { r.S("meccanismo"), r!["capacita_kn"] is null ? "—" : r.D("capacita_kn").ToString("N2"), r.S("stato") }));
            Ui.Tab(panel, "Meccanismi", candidates);
            var info = Ui.Text($"{result.S("meccanismo")} · Hu {result.D("capacita_kn"):N2} kN · |M|max {result.D("momento_massimo_knm"):N2} kNm a z={result.D("quota_momento_massimo_m"):0.###} m\n" +
                $"Residui: H {result.D("residuo_forza_kn"):G3} kN · M {result.D("residuo_momento_knm"):G3} kNm · {result.S("convergenza")}\n" +
                $"Cerniere z [m]: {result!["cerniere_m"]} · F concentrata {result.D("risultante_concentrata_kn"):N2} kN a z={result.D("quota_risultante_m"):0.###} m", 12);
            info.Margin = new Thickness(12);
            Ui.Tab(tabs, $"Sondaggio {++index}", Ui.Dock(panel, info));
        }
        Ui.Tab(tabs, "JSON", new TextBox { Text = Result!.ToJsonString(J.Options), IsReadOnly = true, AcceptsReturn = true, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        return tabs;
    }
    private void ShowResults() { if (Result is not null) Ui.Dialog(this, "Palo orizzontale · risultati", ResultsTabs(), 1120, 760).ShowDialog(); }
    private void ExportCsv()
    {
        if (Result is null) return;
        var save = new SaveFileDialog { Filter = "Tabelle CSV|*.csv", FileName = "Palo_orizzontale.csv" };
        if (save.ShowDialog(Window.GetWindow(this)) != true) return;
        try { Archivio.ScriviAtomico(save.FileName, Encoding.UTF8.GetBytes(PaloOrizzontale.Csv(Result))); }
        catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, "Esportazione non riuscita"); }
    }
    internal async Task Smoke(string directory)
    {
        await CalculateAsync(); if (Result is null) throw new Exception("Orizzontale WPF: calcolo non disponibile");
        string previous = general.Get("diametro"); general.Set("diametro", "1.01");
        if (Result is not null || details.IsEnabled) throw new Exception("Orizzontale: risultati obsoleti non invalidati");
        general.Set("diametro", previous); await CalculateAsync();
        var resultTabs = ResultsTabs(); var dialog = Ui.Dialog(this, "Verifica diagrammi", resultTabs, 1120, 760); dialog.Show();
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        File.WriteAllBytes(Path.Combine(directory, "orizzontale_diagrammi.png"), Ui.Snapshot(dialog)); dialog.Close();
        momentTabs.SelectedIndex = 1;
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        File.WriteAllBytes(Path.Combine(directory, "orizzontale_sezione.png"), Ui.Snapshot(this)); momentTabs.SelectedIndex = 0;
    }
    public void Dispose() { disposed = true; }
}
