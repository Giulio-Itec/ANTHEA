using System.Text.Json.Nodes;
using Anthea.Application.Stratigraphy;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.Stratigraphy;

/// <summary>
/// The window of the AI import (StratigraphyImportWindow) without WPF, F5.6b: the same states of its controls on the import of Application
/// (<see cref="StratigraphyImport"/>), with the source of the drafts injected (<see cref="IStratigraphyDraftSource"/>: the checks never call
/// the network).
/// <list type="bullet">
/// <item>«Scegli PDF…» through <see cref="IFileDialogService"/>; a new file forgets the drafts.</item>
/// <item>«Invia PDF a OpenAI e prepara bozze» (<see cref="ExtractCommand"/>): asynchronous, one at a time; file, model, key and the
/// button are disabled while it runs; «Interrompi» (<see cref="InterruptCommand"/>), the closing of the dialog and the closing of the sheet
/// cancel it. The drafts arrive in <see cref="Profiles"/>, the first is shown; an error is shown in <see cref="Message"/>.</item>
/// <item>A draft is shown in <see cref="Preview"/>, an editor of the stratigraphy without the import button: every change of the draft
/// clears <see cref="Reviewed"/>.</item>
/// <item>«Sostituisci la stratigrafia con il profilo revisionato» (<see cref="ApplyCommand"/>), only after <see cref="Reviewed"/>: the
/// reviewed draft is the result of the dialog; a draft that is not valid stays with its error in <see cref="Message"/>.</item>
/// <item>The key stays in memory only, and is cleared when the dialog is closed (disposed).</item>
/// </list>
/// </summary>
public sealed class StratigraphyImportDialogViewModel : DialogViewModel<JsonObject>
{
    private readonly IStratigraphyDraftSource source;
    private readonly PresentationServices services;
    private readonly CancellationToken outer;
    private CancellationTokenSource? running;
    private IReadOnlyList<JsonObject> drafts = [];
    private IReadOnlyList<string> profiles = [];
    private string file = "", model, apiKey, message = StratigraphyImport.Ready;
    private int selectedProfile = -1;
    private bool reviewed;
    private StratigraphyEditorViewModel? preview;

    /// <param name="source">The extraction of the drafts.</param>
    /// <param name="defaults">Model and key proposed when the dialog opens.</param>
    /// <param name="services">File dialog, messages and questions of the preview.</param>
    /// <param name="cancellation">Cancelled when the sheet that opened the dialog closes.</param>
    public StratigraphyImportDialogViewModel(IStratigraphyDraftSource source, StratigraphyImportDefaults defaults, PresentationServices services,
        CancellationToken cancellation = default) : base(StratigraphyImport.Title)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(services);
        this.source = source; this.services = services; outer = cancellation;
        model = defaults.Model; apiKey = defaults.ApiKey;
        BrowseCommand = new RelayCommand(Browse, () => !IsRunning);
        // The errors that the import does not show in its message go through the guard of the commands (Q19).
        ExtractCommand = GuardedCommands.CreateAsync(new CommandGuard(services.Messages), ExtractAsync, Lifetime, () => !IsRunning);
        InterruptCommand = new RelayCommand(Interrupt, () => IsRunning);
        ApplyCommand = new RelayCommand(Apply, () => CanApply);
    }

    /// <summary>The PDF chosen (shown, not typed).</summary>
    public string File { get => file; private set => SetProperty(ref file, value); }

    /// <summary>The model of the service; not editable while the extraction runs.</summary>
    public string Model { get => model; set { if (!IsRunning) SetProperty(ref model, value ?? ""); } }

    /// <summary>The key of the service: in memory only, never saved; not editable while the extraction runs.</summary>
    public string ApiKey { get => apiKey; set { if (!IsRunning) SetProperty(ref apiKey, value ?? ""); } }

    /// <summary>The state of the import, the error of the extraction or the warnings of the draft shown.</summary>
    public string Message { get => message; private set => SetProperty(ref message, value); }

    /// <summary>An extraction is running.</summary>
    public bool IsRunning => running is not null;

    /// <summary>«Scegli PDF…».</summary>
    public IRelayCommand BrowseCommand { get; }

    /// <summary>«Invia PDF a OpenAI e prepara bozze».</summary>
    public IAsyncRelayCommand ExtractCommand { get; }

    /// <summary>«Interrompi».</summary>
    public IRelayCommand InterruptCommand { get; }

    /// <summary>«Sostituisci la stratigrafia con il profilo revisionato».</summary>
    public IRelayCommand ApplyCommand { get; }

    /// <summary>The drafts, «n · name».</summary>
    public IReadOnlyList<string> Profiles { get => profiles; private set => SetProperty(ref profiles, value); }

    /// <summary>The draft shown (−1: none).</summary>
    public int SelectedProfileIndex
    {
        get => selectedProfile;
        set
        {
            if (value == selectedProfile || value < -1 || value >= Profiles.Count) return;
            selectedProfile = value;
            OnPropertyChanged();
            ShowProfile();
        }
    }

    /// <summary>The editor of the draft shown, without the import button; null without a draft.</summary>
    public StratigraphyEditorViewModel? Preview { get => preview; private set => SetProperty(ref preview, value); }

    /// <summary>«Ho confrontato dati, unità e pagine della bozza con la relazione»: cleared by every change of the draft or of the profile.</summary>
    public bool Reviewed
    {
        get => reviewed;
        set { if (SetProperty(ref reviewed, value)) UpdateApply(); }
    }

    /// <summary>The draft can be applied: reviewed, a profile shown, no extraction running.</summary>
    public bool CanApply => running is null && reviewed && selectedProfile >= 0;

    /// <summary>Closes the dialog without a result (the closing of the window: the stratigraphy stays as it is).</summary>
    public void Dismiss() => Cancel();

    protected override void OnDisposed()
    {
        running?.Cancel();
        apiKey = "";
        OnPropertyChanged(nameof(ApiKey));
    }

    private void Browse()
    {
        string? path = services.Files.OpenFile(new OpenFileRequest([new FileFilter("Relazione PDF", "*.pdf")]));
        if (path is null) return;
        File = path;
        ResetDrafts();
    }

    private void ResetDrafts()
    {
        drafts = []; Profiles = [];
        selectedProfile = -1; OnPropertyChanged(nameof(SelectedProfileIndex));
        Preview = null;
        reviewed = false; OnPropertyChanged(nameof(Reviewed));
        UpdateApply();
    }

    private async Task ExtractAsync(CancellationToken command)
    {
        if (running is not null || IsDisposed) return;
        ResetDrafts();
        running = CancellationTokenSource.CreateLinkedTokenSource(command, outer);
        RunningChanged();
        Message = StratigraphyImport.Reading;
        try
        {
            drafts = await source.ExtractAsync(File, ApiKey, Model, running.Token);
            if (IsDisposed || running.IsCancellationRequested) return;
            Profiles = StratigraphyImport.ProfileNames(drafts);
            SelectedProfileIndex = 0;
        }
        catch (Exception error) when (StratigraphyImport.Shown(error)) { Message = StratigraphyImport.Failure(error); }
        finally
        {
            running.Dispose(); running = null;
            RunningChanged();
        }
    }

    private void Interrupt() => running?.Cancel();

    private void ShowProfile()
    {
        reviewed = false; OnPropertyChanged(nameof(Reviewed));
        UpdateApply();
        if (selectedProfile < 0 || selectedProfile >= drafts.Count) return;
        var draft = drafts[selectedProfile];
        Message = StratigraphyImport.DraftMessage(draft);
        Preview = new StratigraphyEditorViewModel(draft, services, () => { reviewed = false; OnPropertyChanged(nameof(Reviewed)); UpdateApply(); }, importVisible: false);
    }

    private void Apply()
    {
        if (!reviewed || selectedProfile < 0) return;
        try { Close(StratigraphyImport.Apply(drafts[selectedProfile])); }
        catch (ArgumentException error) { Message = StratigraphyImport.ApplyFailure(error); }
    }

    private void UpdateApply()
    {
        OnPropertyChanged(nameof(CanApply));
        ApplyCommand?.NotifyCanExecuteChanged();
    }

    private void RunningChanged()
    {
        OnPropertyChanged(nameof(IsRunning));
        BrowseCommand.NotifyCanExecuteChanged(); ExtractCommand.NotifyCanExecuteChanged(); InterruptCommand.NotifyCanExecuteChanged();
        UpdateApply();
    }
}
