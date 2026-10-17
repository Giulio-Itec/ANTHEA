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
            // F5.6: the rows and the description come from Application (SoilTransferPresentation), shared with SoilTransferDialogViewModel.
            var shown = Anthea.Application.Presentation.SoilTransferPresentation.Preview(profile);
            var table = Ui.Table([.. shown.Columns], shown.Rows.ToArray());
            if (shown.Stratigraphy) { table.Columns[0].Width = new DataGridLength(220); table.Columns[1].Width = new DataGridLength(100); }
            table.MinHeight = 130; table.MaxHeight = 240; preview.Content = table;
            description.Text = shown.Description;
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
