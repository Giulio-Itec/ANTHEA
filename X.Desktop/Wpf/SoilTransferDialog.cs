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
    internal bool Replace;
    private readonly ComboBox survey;
    private readonly ContentControl preview = new();
    private readonly TextBlock status = Ui.Text("", 12), description = Ui.Text("", 12);
    private readonly JsonObject original;
    private readonly string module;
    private JsonObject? profile;
    internal SoilTransferDialog(Window owner, string module, JsonObject data)
    {
        Owner = owner; Title = "Terreno · invio e riutilizzo"; Width = 940; Height = 660; MinWidth = 720; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Appearance.Surface; this.module = module; original = (JsonObject)data.DeepClone();
        Appearance.Watch(this);
        survey = Ui.Choice(module == RetainingWall.Module ? new[] { "Monte", "Valle" } : Enumerable.Range(1, SoilProfileTransfer.SurveyCount(module, data)).Select(i => "Profilo " + i), module == RetainingWall.Module ? "Monte" : "Profilo 1");
        Destination = new ComboBox { MinWidth = 300, SelectedValuePath = "Tag" };
        foreach (var target in SoilProfileTransfer.Modules) { var info = ModuleCatalog.Get(target); Destination.Items.Add(new ComboBoxItem { Content = info.Element + " · " + info.Description, Tag = target }); }
        Destination.SelectedValue = module == "geo_palo_verticale" ? RetainingWall.Module : "geo_palo_verticale";
        SendButton = Ui.Button("Invia a un nuovo foglio", () => Try(() => { DestinationModule = (string)Destination.SelectedValue; Output = SoilProfileTransfer.Create(DestinationModule, profile!); DialogResult = true; }), true);
        var apply = Ui.Button("Sostituisci il terreno del foglio corrente", () => Try(() => { Output = SoilProfileTransfer.Apply(module, original, profile!, survey.SelectedIndex); Replace = true; DialogResult = true; }));
        void Refresh()
        {
            SendButton.IsEnabled = apply.IsEnabled = profile is not null;
            if (profile is null) { preview.Content = null; return; }
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
        void Target() => guidance.Text = SoilProfileTransfer.Guidance((string)Destination.SelectedValue) + "\nMicropalo verticale Bustamante–Doix: schema specifico, escluso da questo trasferimento.";
        Destination.SelectionChanged += (_, _) => Target(); survey.SelectionChanged += (_, _) => Current();
        var body = Ui.Stack(Ui.Text("Terreno condivisibile fra i moduli", 19, true), Ui.Bar(survey, Ui.Button("Usa il profilo corrente", Current), load, save), description, preview,
            Ui.Text("Invio a un nuovo foglio / una nuova finestra", 14, true), Ui.Bar(Destination, SendButton), guidance,
            Ui.Text("Importazione nel foglio corrente", 14, true), apply, status);
        body.Margin = new Thickness(20); Content = new ScrollViewer { Content = body, Background = Appearance.Surface, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Current(); Target();
    }
    private void Try(Action action)
    {
        try { status.Foreground = Ui.Navy; action(); }
        catch (Exception ex) { status.Text = ex.Message; status.Foreground = Brushes.Firebrick; }
    }
}
