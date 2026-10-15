using System.Net.Http;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.Application.Stratigraphy;
using Microsoft.Win32;

namespace X.Desktop;

internal sealed class StratigraphyImportWindow : Window
{
    private readonly TextBox file = new() { IsReadOnly = true, MinWidth = 250 };
    private readonly TextBox model = new() { Text = Environment.GetEnvironmentVariable("ANTHEA_AI_MODEL") ?? "", MinWidth = 180 };
    private readonly PasswordBox key = new() { Password = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "", MinWidth = 180 };
    private readonly ComboBox profiles = new() { MinWidth = 200 };
    private readonly ContentControl preview = new();
    private readonly TextBlock message = Ui.Text("Selezionare il PDF, indicare il modello e la propria chiave API.", 12);
    private readonly CheckBox reviewed = new() { Content = "Ho confrontato dati, unità e pagine della bozza con la relazione", Margin = new Thickness(6) };
    private readonly Button extract;
    private readonly Button apply;
    private readonly Button browse;
    private readonly Button cancel;
    private IReadOnlyList<JsonObject> drafts = [];
    private CancellationTokenSource? running;
    internal JsonObject? Result { get; private set; }

    internal StratigraphyImportWindow()
    {
        Title = "Importa relazione geologica · bozza AI"; Width = 1200; Height = 850;
        MinWidth = 850; MinHeight = 600; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Appearance.Paper; Foreground = Appearance.Ink;
        browse = Ui.Button("Scegli PDF…", () =>
        {
            var dialog = new OpenFileDialog { Filter = "Relazione PDF|*.pdf", CheckFileExists = true };
            if (dialog.ShowDialog(this) == true) { file.Text = dialog.FileName; ResetDrafts(); }
        });
        extract = Ui.Button("Invia PDF a OpenAI e prepara bozze", () => _ = Extract());
        cancel = Ui.Button("Interrompi", () => running?.Cancel()); cancel.IsEnabled = false;
        apply = Ui.Button("Sostituisci la stratigrafia con il profilo revisionato", Apply); apply.IsEnabled = false;
        var header = Ui.Stack(Ui.Text("Importazione assistita da relazione PDF", 21, true),
            Ui.Text("Il pulsante di invio trasmette l’intero PDF a OpenAI tramite la tua chiave API; il servizio può comportare costi. Massimo 20 MiB. La chiave non viene salvata nel progetto.", 12),
            Ui.Bar(browse, file), Ui.Bar(Ui.Text("Modello API", 12), model, Ui.Text("Chiave API", 12), key),
            Ui.Bar(extract, cancel, Ui.Text("Profilo da revisionare", 12), profiles), message);
        var footer = Ui.Stack(reviewed, apply, Ui.Text("L’applicazione sostituisce i dati della scheda aperta. Chiudere questa finestra per conservare la stratigrafia attuale.", 12));
        Content = Ui.Dock(preview, header, footer); ((FrameworkElement)Content).Margin = new Thickness(12);
        profiles.SelectionChanged += (_, _) => ShowProfile();
        reviewed.Click += (_, _) => UpdateApply();
        Closed += (_, _) => { running?.Cancel(); key.Clear(); };
    }
    private void ResetDrafts()
    {
        drafts = []; profiles.ItemsSource = null; preview.Content = null; reviewed.IsChecked = false; apply.IsEnabled = false;
    }
    private async Task Extract()
    {
        if (running is not null) return;
        ResetDrafts(); running = new CancellationTokenSource();
        extract.IsEnabled = browse.IsEnabled = model.IsEnabled = key.IsEnabled = false; cancel.IsEnabled = true;
        message.Text = "Lettura della relazione in corso…";
        try
        {
            using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
            drafts = await new StratigraphyAiImporter(http).ExtractAsync(file.Text, key.Password, model.Text, running.Token);
            if (!IsVisible || running.IsCancellationRequested) return;
            profiles.ItemsSource = drafts.Select((d, i) => $"{i + 1} · {d["name"]}").ToArray(); profiles.SelectedIndex = 0;
        }
        catch (OperationCanceledException) { message.Text = "Importazione interrotta o tempo massimo raggiunto. Nessun dato applicato."; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException or System.IO.IOException or UnauthorizedAccessException or FormatException)
        { message.Text = ex is HttpRequestException ? "Connessione al servizio non riuscita. Nessun dato applicato." : ex is FormatException ? "Formato della chiave o della configurazione non valido." : ex.Message; }
        finally
        {
            running.Dispose(); running = null; extract.IsEnabled = browse.IsEnabled = model.IsEnabled = key.IsEnabled = true; cancel.IsEnabled = false;
        }
    }
    private void ShowProfile()
    {
        reviewed.IsChecked = false; apply.IsEnabled = false;
        if (profiles.SelectedIndex < 0 || profiles.SelectedIndex >= drafts.Count) return;
        var draft = drafts[profiles.SelectedIndex];
        message.Text = "Bozza AI da controllare · " + draft["import_source"]!["warnings"];
        var editor = new StratigraphyWorkspace(draft, allowImport: false);
        editor.Modified += () => { reviewed.IsChecked = false; UpdateApply(); };
        preview.Content = editor;
    }
    private void UpdateApply() => apply.IsEnabled = running is null && reviewed.IsChecked == true && profiles.SelectedIndex >= 0;
    private void Apply()
    {
        if (reviewed.IsChecked != true || profiles.SelectedIndex < 0) return;
        try
        {
            var draft = drafts[profiles.SelectedIndex]; StratigraphyDocument.Validate(draft);
            Result = draft.DeepClone().AsObject(); Result["import_source"]!["reviewed"] = true;
            DialogResult = true;
        }
        catch (ArgumentException ex) { message.Text = "Correggere la bozza prima di applicarla: " + ex.Message; }
    }
}
