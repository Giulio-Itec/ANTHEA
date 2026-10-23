using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;
using X.Core;

namespace Anthea.Presentation.Shell.Projects;

/// <summary>A button of the revision bar: «Rev. n» or «Rev. n · attuale», selected, with its automation name (UpdateProjectRevisionBar).</summary>
public sealed record RevisionTab(int Number, bool IsCurrent, string Label, bool IsSelected, string AutomationName);

/// <summary>
/// The revision bar of a section (F5.13 b, proposal 4.3; MainWindow.UpdateProjectRevisionBar): the section of the revisions of the node on
/// screen, its archived revisions and the current one, the selected one, «Elimina revisione» and «+ Nuova revisione» with the enabled state
/// and the tooltips of today, the line of the details; the commands go to the project session (<see cref="ProjectSession.SwitchRevision"/>,
/// <see cref="ProjectSession.DeleteRevision"/>, <see cref="ProjectSession.NewRevision"/>, the note asked by <see cref="NewRevisionDialogViewModel"/>).
/// </summary>
public sealed class RevisionBarViewModel : ViewModelBase
{
    public const string AddLabel = "+ Nuova revisione";
    public const string DeleteLabel = "Elimina revisione";

    private readonly ProjectSession project;
    private readonly IDialogService dialogs;
    private readonly CommandGuard guard;
    private JsonObject? scope;

    public RevisionBarViewModel(ProjectSession project, PresentationServices services)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(services);
        this.project = project;
        dialogs = services.Dialogs;
        guard = new CommandGuard(services.Messages);
        SwitchCommand = new RelayCommand<RevisionTab>(tab => guard.Run(() => Switch(tab!)), tab => tab is not null);
        DeleteCommand = GuardedCommands.Create(guard, Delete, () => CanDelete);
        AddCommand = new AsyncRelayCommand(() => guard.RunAsync(_ => AddAsync()), () => CanAdd);
    }

    /// <summary>The section of the revisions; null hides the bar.</summary>
    public JsonObject? Scope => scope;

    public bool IsVisible => scope is not null;

    /// <summary>«Revisioni · nome», with the name as tooltip.</summary>
    public string Caption { get; private set; } = "";

    public string ScopeName { get; private set; } = "";

    public IReadOnlyList<RevisionTab> Tabs { get; private set; } = [];

    public bool CanDelete { get; private set; }

    public string DeleteToolTip { get; private set; } = "";

    public bool CanAdd { get; private set; }

    public string AddToolTip { get; private set; } = "";

    /// <summary>«Versione archiviata · sola lettura» or «Versione attuale · modificabile», with date and note.</summary>
    public string Details { get; private set; } = "";

    /// <summary>The details in the colour of the archived versions.</summary>
    public bool DetailsArchived { get; private set; }

    /// <summary>Number of the revision selected: the one on screen, otherwise the current one.</summary>
    public int SelectedNumber { get; private set; }

    public IRelayCommand<RevisionTab> SwitchCommand { get; }

    public IRelayCommand DeleteCommand { get; }

    public IAsyncRelayCommand AddCommand { get; }

    /// <summary>Raised after a command changed the revision on screen or the revisions: the shell refreshes the page.</summary>
    public event EventHandler? RevisionsChanged;

    /// <summary>
    /// Shows the bar of <paramref name="node"/> (the selected section or the sheet with the focus), through
    /// <see cref="ProjectSession.RevisionScope"/>; with <paramref name="afterSwitch"/> the section just switched to, as the bar during
    /// MainWindow.SwitchProjectRevision.
    /// </summary>
    public void Show(JsonObject? node, bool afterSwitch = false)
    {
        scope = afterSwitch ? project.CurrentRevisionScope : project.RevisionScope(node);
        if (scope is null) { Tabs = []; CanDelete = CanAdd = false; Notify(); return; }
        bool readOnly = project.IsReadOnly;
        int? displayed = project.DisplayedRevision;
        int current = ShellTexts.RevisionNumber(scope["revisione"] as JsonObject);
        int selectedNumber = SelectedNumber = displayed ?? current;
        var archived = scope.Array("revisioni").OfType<JsonObject>().ToArray();
        ScopeName = scope.S("nome");
        Caption = "Revisioni · " + ScopeName;
        CanDelete = archived.Length > 0;
        DeleteToolTip = !CanDelete ? "L’unica versione rimasta non può essere eliminata." :
            selectedNumber == current ? $"Elimina Rev. {current} e rende modificabile la precedente. Puoi annullare l’operazione." :
            $"Elimina Rev. {selectedNumber} dallo storico. La versione attuale resta invariata. Puoi annullare l’operazione.";
        CanAdd = !readOnly;
        AddToolTip = readOnly ? "Torna alla revisione attuale per crearne una nuova." : "Conserva la versione attuale e continua nella nuova revisione.";
        RevisionTab Tab(int number, bool live) => new(number, live, $"Rev. {number}" + (live ? " · attuale" : ""),
            live ? !readOnly : displayed == number, $"Revisione {number}" + (live ? " attuale" : " archiviata"));
        Tabs = archived.Select(r => Tab(ShellTexts.RevisionNumber(r), false)).Append(Tab(current, true)).ToArray();
        var metadata = displayed is int number ? archived.FirstOrDefault(r => ShellTexts.RevisionNumber(r) == number) : scope["revisione"] as JsonObject;
        string details = readOnly ? "Versione archiviata · sola lettura" : "Versione attuale · modificabile";
        if (metadata is not null) details += "  ·  " + ShellTexts.RevisionDate(metadata) + (metadata.S("nota").Length > 0 ? "  ·  " + metadata.S("nota") : "");
        Details = details;
        DetailsArchived = readOnly;
        Notify();
    }

    /// <summary>A button «Rev. n»: the archived revision read-only, or the current version.</summary>
    public void Switch(RevisionTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (scope is null) return;
        string id = scope.S("id");
        if (!project.SwitchRevision(id, tab.IsCurrent ? null : tab.Number)) return;
        Show(null, afterSwitch: true);
        RevisionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>«Elimina revisione»: the selected revision (the current one makes the previous editable); it can be undone.</summary>
    public void Delete()
    {
        if (scope is null) return;
        string id = scope.S("id");
        if (!project.DeleteRevision(id, SelectedNumber)) return;
        Show(null, afterSwitch: true);
        RevisionsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>«+ Nuova revisione» (MainWindow.CreateProjectRevision): the note in the dialog, then the revision; «Annulla» changes nothing.</summary>
    public async Task AddAsync()
    {
        if (scope is null || project.IsReadOnly) return;
        var section = scope;
        var note = await dialogs.ShowDialogAsync(new NewRevisionDialogViewModel(section.S("nome")));
        if (note is null) return;
        if (!project.NewRevision(section, note.Trim())) return;
        Show(section);
        RevisionsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Notify()
    {
        foreach (string name in new[] { nameof(Scope), nameof(IsVisible), nameof(Caption), nameof(ScopeName), nameof(Tabs), nameof(CanDelete), nameof(DeleteToolTip),
            nameof(CanAdd), nameof(AddToolTip), nameof(Details), nameof(DetailsArchived), nameof(SelectedNumber) }) OnPropertyChanged(name);
        DeleteCommand.NotifyCanExecuteChanged(); AddCommand.NotifyCanExecuteChanged();
    }
}

/// <summary>
/// The dialog «Nuova revisione · nome» (MainWindow.CreateProjectRevision): the text and the optional note of today; «Crea revisione» gives
/// the note (empty allowed), «Annulla» gives nothing.
/// </summary>
public sealed class NewRevisionDialogViewModel : DialogViewModel<string>
{
    public const string Text = "La versione attuale verrà archiviata. Continuerai a lavorare nella nuova revisione.";
    public const string NoteLabel = "Nota della nuova revisione (facoltativa)";
    public const string CreateLabel = "Crea revisione";
    public const string CancelLabel = "Annulla";

    private string note = "";

    public NewRevisionDialogViewModel(string sectionName) : base("Nuova revisione · " + sectionName)
    {
        CreateCommand = new RelayCommand(() => Close(Note));
        CancelCommand = new RelayCommand(Cancel);
    }

    public string Note { get => note; set => SetProperty(ref note, value ?? ""); }

    public IRelayCommand CreateCommand { get; }

    public IRelayCommand CancelCommand { get; }
}

/// <summary>An archived revision in the summary: header, note, changes and the button «Mostra questa revisione».</summary>
public sealed record RevisionSummaryEntry(int Number, string Header, string Note, IReadOnlyList<string> Changes);

/// <summary>
/// «Riepilogo modifiche» of a section (MainWindow.ShowProjectRevisions): the current revision with its date, note and changes from the last
/// archived one, then the archived revisions from the newest; «Mostra questa revisione» switches to it. The shell shows it as the modal dialog of
/// MainWindow (F5.13 c, after the checkpoint of 22/10): «Revisioni · sezione», closed by «Mostra questa revisione» with the result true.
/// </summary>
public sealed class RevisionSummaryViewModel : ViewModelBase, IDialogViewModel<bool>
{
    private readonly ProjectSession project;
    private readonly string sectionId;

    /// <param name="project">The project, whose working document gives the changes of the current version.</param>
    /// <param name="section">A section with a current revision («revisione»).</param>
    public RevisionSummaryViewModel(ProjectSession project, JsonObject section, IMessageService messages)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(messages);
        this.project = project;
        sectionId = section.S("id");
        Title = "Revisioni · " + section.S("nome");
        var current = section["revisione"]?.AsObject() ?? throw new ArgumentException("Sezione senza revisione corrente.", nameof(section));
        CurrentHeader = $"Rev. {ShellTexts.RevisionNumber(current)} · corrente · {ShellTexts.RevisionDate(current)}";
        CurrentNote = current.S("nota");
        CurrentChanges = ProjectRevisions.Changes(project.WorkingDocument, section).Select(c => "• " + c).ToArray();
        Entries = section.Array("revisioni").OfType<JsonObject>().Reverse().Select(entry => new RevisionSummaryEntry(ShellTexts.RevisionNumber(entry),
            $"Rev. {ShellTexts.RevisionNumber(entry)} · {ShellTexts.RevisionDate(entry)}", entry.S("nota"),
            entry.Array("modifiche").Select(c => "• " + c).ToArray())).ToArray();
        var guard = new CommandGuard(messages);
        ShowCommand = new RelayCommand<RevisionSummaryEntry>(entry => guard.Run(() => Show(entry!)), entry => entry is not null);
    }

    /// <summary>
    /// «Mostra questa revisione»: the dialog closes first and the revision is shown, as MainWindow (dialog.Close, then SwitchProjectRevision);
    /// <see cref="Shown"/> follows when the revision is on screen.
    /// </summary>
    public void Show(RevisionSummaryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        CloseRequested?.Invoke(this, EventArgs.Empty);
        if (!project.SwitchRevision(sectionId, entry.Number)) return;
        Result = true;
        Shown?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The result of the dialog: true when a revision was shown.</summary>
    public bool Result { get; private set; }

    /// <summary>The window closes the summary («Mostra questa revisione»).</summary>
    public event EventHandler? CloseRequested;

    public string Title { get; }
    public string CurrentHeader { get; }
    public string CurrentNote { get; }
    public IReadOnlyList<string> CurrentChanges { get; }
    public IReadOnlyList<RevisionSummaryEntry> Entries { get; }
    public string ShowLabel => "Mostra questa revisione";
    public IRelayCommand<RevisionSummaryEntry> ShowCommand { get; }

    /// <summary>A revision was shown: the window closes the summary.</summary>
    public event EventHandler? Shown;
}
