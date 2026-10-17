using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using Anthea.Application.Stratigraphy;
using Microsoft.Win32;

namespace X.Desktop;

/// <summary>
/// The window of the AI import (STRAT-2, disabled in the Release). Since F5.6b the import itself is in Application
/// (<see cref="StratigraphyImport"/>: the extraction with its HTTP client, texts, errors, application of the draft); the window keeps only
/// the state of its controls. The extraction is injectable (<see cref="IStratigraphyDraftSource"/>): the checks never call the network.
/// </summary>
internal sealed class StratigraphyImportWindow : Window
{
    private readonly IStratigraphyDraftSource source;
    private readonly TextBox file = new() { IsReadOnly = true, MinWidth = 250 };
    private readonly TextBox model = new() { Text = StratigraphyImport.EnvironmentDefaults().Model, MinWidth = 180 };
    private readonly PasswordBox key = new() { Password = StratigraphyImport.EnvironmentDefaults().ApiKey, MinWidth = 180 };
    private readonly ComboBox profiles = new() { MinWidth = 200 };
    private readonly ContentControl preview = new();
    private readonly TextBlock message = Ui.Text(StratigraphyImport.Ready, 12);
    private readonly CheckBox reviewed = new() { Content = "Ho confrontato dati, unità e pagine della bozza con la relazione", Margin = new Thickness(6) };
    private readonly Button extract;
    private readonly Button apply;
    private readonly Button browse;
    private readonly Button cancel;
    private IReadOnlyList<JsonObject> drafts = [];
    private CancellationTokenSource? running;
    internal JsonObject? Result { get; private set; }

    internal StratigraphyImportWindow() : this(StratigraphyImport.OpenAi()) { }

    internal StratigraphyImportWindow(IStratigraphyDraftSource source)
    {
        this.source = source;
        Title = StratigraphyImport.Title; Width = 1200; Height = 850;
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
        message.Text = StratigraphyImport.Reading;
        try
        {
            drafts = await source.ExtractAsync(file.Text, key.Password, model.Text, running.Token);
            if (!IsVisible || running.IsCancellationRequested) return;
            profiles.ItemsSource = StratigraphyImport.ProfileNames(drafts); profiles.SelectedIndex = 0;
        }
        catch (Exception ex) when (StratigraphyImport.Shown(ex)) { message.Text = StratigraphyImport.Failure(ex); }
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
        message.Text = StratigraphyImport.DraftMessage(draft);
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
            Result = StratigraphyImport.Apply(drafts[profiles.SelectedIndex]);
            DialogResult = true;
        }
        catch (ArgumentException ex) { message.Text = StratigraphyImport.ApplyFailure(ex); }
    }
}
