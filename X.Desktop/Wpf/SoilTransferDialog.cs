using Anthea.Application.Modules;
using Anthea.Application.Walls;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using X.Core;

namespace X.Desktop;

internal sealed class SoilTransferDialog : Window
{
    internal readonly ComboBox Destination;
    internal readonly Button SendButton;
    internal JsonObject? Output;
    internal string? DestinationModule;
    internal string OutputName => "Terreno da " + (profile?["source_sheet"]?.ToString() ?? profile?["stratigraphy"]?["name"]?.ToString() ?? ModuleDirectory.Get(module).Element)
        + (profile?["stratigraphy"] is not null && DestinationModule != Anthea.Application.Stratigraphy.StratigraphyDocument.Module ? " · " + selectedScenario : "");
    private string selectedScenario = "Medi";
    internal bool Replace;
    private readonly ComboBox survey;
    private readonly ContentControl preview = new();
    private readonly TextBlock status = Ui.Text("", 12), description = Ui.Text("", 12);
    private readonly JsonObject original;
    private readonly string module;
    private JsonObject? profile;
    internal SoilTransferDialog(Window owner, string module, JsonObject data,
        IEnumerable<(string Label, string Module, JsonObject Data, string Id)>? sources = null)
    {
        Owner = owner; Title = "Terreno · invio e riutilizzo"; Width = 940; Height = 660; MinWidth = 720; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Appearance.Surface; this.module = module; original = (JsonObject)data.DeepClone();
        Appearance.Watch(this);
        survey = Ui.Choice(module == WallModel.Module ? new[] { "Monte", "Valle" } : Enumerable.Range(1, SoilProfileTransfer.SurveyCount(module, data)).Select(i => "Profilo " + i), module == WallModel.Module ? "Monte" : "Profilo 1");
        Destination = new ComboBox { MinWidth = 300, SelectedValuePath = "Tag" };
        foreach (var target in SoilProfileTransfer.Modules) { var info = ModuleDirectory.Get(target); Destination.Items.Add(new ComboBoxItem { Content = info.Element + " · " + info.Description, Tag = target }); }
        Destination.SelectedValue = module == "geo_palo_verticale" ? WallModel.Module : "geo_palo_verticale";
        var scenario = Ui.Choice(new[] { "Medi", "Minimi" }, "Medi");
        scenario.SelectionChanged += (_, _) => selectedScenario = scenario.SelectedItem?.ToString() ?? "Medi";
        SendButton = Ui.Button("Invia a un nuovo foglio", () => Try(() => { DestinationModule = (string)Destination.SelectedValue; Output = SoilProfileTransfer.Create(DestinationModule, profile!, scenario.Text); DialogResult = true; }), true);
        var apply = Ui.Button("Sostituisci il terreno del foglio corrente", () => Try(() =>
        {
            var candidate = SoilProfileTransfer.Apply(module, original, profile!, survey.SelectedIndex, scenario.Text);
            if (MessageBox.Show(this, "Sostituire il profilo selezionato e i dati di falda del foglio corrente con una copia indipendente?", "Importa terreno", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            Output = candidate; Replace = true; DialogResult = true;
        }));
        void Refresh()
        {
            SendButton.IsEnabled = apply.IsEnabled = profile is not null;
            if (profile is null) { preview.Content = null; return; }
            if (profile["stratigraphy"] is JsonObject document)
            {
                var rows = new List<string[]>(); double top = 0;
                foreach (var layer in Anthea.Application.Stratigraphy.StratigraphyDocument.Layers(document).OfType<JsonObject>())
                {
                    bool infinite = Anthea.Application.Stratigraphy.StratigraphyDocument.Infinite(layer);
                    double bottom = top + (Anthea.Application.Stratigraphy.StratigraphyDocument.Number(layer["thickness"], "Spessore") ?? 0);
                    string[] Values(JsonObject values, string set) => new[] { layer.S("name") + " · " + set, layer.S("soil_type"), RetainingWallWorkspace.F(top), infinite ? "Indefinito" : RetainingWallWorkspace.F(bottom), values.S("gamma"), values.S("gamma_sat"), values.S("phi"), values.S("c"), values.S("cu") };
                    rows.Add(Values(layer, "Medi"));
                    if (layer["minimum"] is JsonObject minimum) rows.Add(Values(minimum, "Minimi"));
                    top = bottom;
                }
                var summary = Ui.Table(["Strato / insieme", "Famiglia", "Da z [m]", "A z [m]", "γ", "γsat", "φ′", "c′", "cu"], rows.ToArray());
                summary.Columns[0].Width = new DataGridLength(220);
                summary.Columns[1].Width = new DataGridLength(100);
                summary.MinHeight = 130; summary.MaxHeight = 240; preview.Content = summary;
                description.Text = "Stratigrafia completa: copia con medi, minimi, fonti e retini. Per i moduli di verifica scegliere un insieme; per entrambi inviare due fogli distinti. Falda e spessori devono essere definiti; nessuna sostituzione di dati mancanti.";
                description.Text += "\nFalda: " + document.S("water_state") + (document.S("water_state") == "Assegnata" ? " a " + document.S("water_depth") + " m" : "") + ". Pesi in kN/m³; angoli in gradi; coesioni in kPa. Stesso piano di riferimento, senza traslazione di quota.";
                return;
            }
            double depth = 0;
            var table = Ui.Table(["Strato", "Tipo", "Da z [m]", "A z [m]", "γ", "γsat", "φ′", "c′", "cu"], profile.Array("layers").Select((r, i) =>
            { double top = depth; depth += r.D("spessore"); return new[] { r.S("name", "Strato " + (i + 1)), r.S("tipologia"), RetainingWallWorkspace.F(top), RetainingWallWorkspace.F(depth), r.S("peso_specifico"), r.S("peso_specifico_saturo"), r.S("angolo_attrito"), r.S("coesione_efficace"), r.S("coesione_non_drenata") }; }).ToArray());
            table.MinHeight = 130; table.MaxHeight = 240; preview.Content = table;
            description.Text = profile.S("datum") + $". Profondità indagata: {depth:0.##} m. " + (profile["water"].B("enabled") ? $"Falda a z={profile["water"].D("depth"):0.##} m." : "Falda assente.")
                + "\nPesi in kN/m³; angoli in gradi; coesioni in kPa. Le profondità vengono copiate senza traslazione: usare lo stesso riferimento geometrico nel foglio di destinazione.";
        }
        void Current()
        {
            profile = null; Try(() => { profile = SoilProfileTransfer.Extract(module, original, survey.SelectedIndex); status.Text = "Profilo del foglio corrente. Puoi salvarlo oppure inviarlo a un altro modulo."; }); Refresh();
        }
        var load = Ui.Button("Carica terreno…", () => Try(() =>
        {
            var dialog = new OpenFileDialog { Filter = "Profilo terreno ANTHEA|*.anthea-terreno.json|JSON|*.json" };
            if (dialog.ShowDialog(this) != true) return;
            var candidate = JsonNode.Parse(File.ReadAllText(dialog.FileName, Encoding.UTF8)) as JsonObject ?? throw new ArgumentException("File terreno non valido.");
            SoilProfileTransfer.Validate(candidate); profile = candidate; status.Text = "Caricato: " + Path.GetFileName(dialog.FileName) + ". Controlla l’anteprima, poi scegli invio o sostituzione del profilo corrente."; Refresh();
        }));
        var save = Ui.Button("Salva terreno…", () => Try(() =>
        {
            if (profile is null) throw new ArgumentException("Completare o caricare prima un profilo terreno.");
            var dialog = new SaveFileDialog { Filter = "Profilo terreno ANTHEA|*.anthea-terreno.json", FileName = "Terreno.anthea-terreno.json" };
            if (dialog.ShowDialog(this) == true) { Archivio.ScriviAtomico(dialog.FileName, Encoding.UTF8.GetBytes(profile.ToJsonString(J.Options))); status.Text = "Profilo salvato e riutilizzabile negli altri moduli compatibili."; }
        }));
        var guidance = Ui.Text("", 12, color: Ui.Muted);
        void Target() => guidance.Text = (Destination.SelectedValue?.ToString() == Anthea.Application.Stratigraphy.StratigraphyDocument.Module
            ? "Copia indipendente completa fra stratigrafie. Dai moduli di verifica l’insieme unico viene riportato nei campi principali: verificarne la qualificazione come medi; minimi e litologia non vengono ricostruiti."
            : SoilProfileTransfer.Guidance(Destination.SelectedValue?.ToString() ?? module)) + "\nMicropalo verticale Bustamante–Doix: schema specifico, escluso da questo trasferimento.";
        Destination.SelectionChanged += (_, _) => Target(); survey.SelectionChanged += (_, _) => Current();
        var sourceList = (sources ?? []).ToArray();
        var sourceChoice = new ComboBox { ItemsSource = sourceList.Select((s, i) => $"{i + 1}. {s.Label}").ToArray(), MinWidth = 280, SelectedIndex = sourceList.Length > 0 ? 0 : -1 };
        var sourceSurvey = new ComboBox { MinWidth = 100 };
        void SourceChanged()
        {
            if (sourceChoice.SelectedIndex < 0) return;
            var source = sourceList[sourceChoice.SelectedIndex];
            sourceSurvey.ItemsSource = source.Module == WallModel.Module ? new[] { "Monte", "Valle" } : Enumerable.Range(1, SoilProfileTransfer.SurveyCount(source.Module, source.Data)).Select(i => "Profilo " + i).ToArray();
            sourceSurvey.SelectedIndex = 0;
        }
        sourceChoice.SelectionChanged += (_, _) => SourceChanged(); SourceChanged();
        var importSource = Ui.Button("Importa dal foglio selezionato", () => Try(() =>
        {
            if (sourceChoice.SelectedIndex < 0) throw new ArgumentException("Nel progetto non ci sono altri fogli terreno compatibili.");
            var source = sourceList[sourceChoice.SelectedIndex];
            var candidate = SoilProfileTransfer.Extract(source.Module, source.Data, sourceSurvey.SelectedIndex);
            candidate["source_sheet"] = source.Label; candidate["source_id"] = source.Id;
            profile = candidate; status.Text = "Sorgente: " + source.Label + ". Verificare l’anteprima prima di confermare la sostituzione o l’invio."; Refresh();
        }));
        importSource.IsEnabled = sourceList.Length > 0;
        var body = Ui.Stack(Ui.Text("Terreno condivisibile fra i moduli", 19, true), Ui.Bar(survey, Ui.Button("Usa il profilo corrente", Current), load, save), description, preview,
            Ui.Text("Da un altro foglio del progetto", 14, true), Ui.Bar(sourceChoice, sourceSurvey, importSource),
            Ui.Text("Parametri per il modulo di verifica (la copia fra stratigrafie conserva entrambi)", 12), scenario,
            Ui.Text("Invio a un nuovo foglio / una nuova finestra", 14, true), Ui.Bar(Destination, SendButton), guidance,
            Ui.Text("Importazione nel foglio corrente", 14, true), apply, status);
        body.Margin = new Thickness(20); Content = new ScrollViewer { Content = body, Background = Appearance.Surface, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Current(); Target();
    }
    private void Try(Action action)
    {
        try { status.Foreground = Appearance.Ink; action(); }
        catch (Exception ex) { status.Text = ex.Message; status.Foreground = Appearance.Foreground(Brushes.Firebrick); }
    }
}
