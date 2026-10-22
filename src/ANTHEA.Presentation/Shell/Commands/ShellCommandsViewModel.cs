using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;
using X.Core;

namespace Anthea.Presentation.Shell.Commands;

/// <summary>
/// Writing of the files of a sheet (results, report, short report) for the commands of the shell: the code of the reports does not change
/// (frozen on 8/10) and stays where MvvmSheetEditor calls it today; the desktop implements this in (c). The checks record the calls.
/// </summary>
public interface ISheetExports
{
    /// <summary>The JSON of the result of the sheet (ISheetEditor.ExportResult).</summary>
    void ExportResult(SheetSession sheet, string path);

    /// <summary>The DOCX of the report and the PDF beside it, with the chosen sections (ISheetEditor.ExportReport).</summary>
    void ExportReport(SheetSession sheet, string path, string title, IReadOnlySet<string> options);

    /// <summary>The short report of a concrete section (ISheetEditor.BuildShortReport).</summary>
    byte[] BuildShortReport(SheetSession sheet, string title);

    /// <summary>Writes the short report, Word and PDF within two pages, away from the UI thread (ReportFiles.Write).</summary>
    Task WriteShortReportAsync(string path, byte[] report, string title);
}

/// <summary>A keyboard shortcut of a menu item: Ctrl plus <paramref name="Key"/>, with Shift when asked.</summary>
public sealed record ShellShortcut(string Key, bool Shift = false);

/// <summary>An item of the menu «File»: title, command and shortcut; a separator has no command.</summary>
public sealed record ShellMenuItem(string Title, IRelayCommand? Command = null, ShellShortcut? Shortcut = null)
{
    public static ShellMenuItem Separator { get; } = new("");

    public bool IsSeparator => Command is null;
}

/// <summary>
/// The commands of the shell (F5.13 b, proposal 4.2 and 4.3; MainWindow.BuildMenu, FileCommands, Save, ExportSheet, ExportJson, ExportReport,
/// ExportShortReport, ProjectWorkspace): the menu «File» with the order, the titles and the shortcuts of today; open, save, export, the reports
/// with the file names, the conditions and the messages of today for each module; undo and redo of the project; the requests that only the
/// view can satisfy (examples page, rheology window, exit) as events. They act on the sheet with the focus and on its document (SH21).
/// </summary>
public sealed class ShellCommandsViewModel : ViewModelBase
{
    public const string WordFilter = "Word e PDF affiancati";
    public const string ShortTitle = "Report short";
    public const string RheologyTitle = "Reologia del calcestruzzo…";
    public const string UndoLabel = "↶ Annulla";
    public const string RedoLabel = "↷ Ripristina";
    public const string UndoToolTip = "Annulla l’ultima operazione · Ctrl+Z";
    public const string RedoToolTip = "Ripristina · Ctrl+Y";
    public const string ShortToolTip = "Sezioni CLS · sette verifiche governanti · Word e PDF · massimo due pagine";
    /// <summary>Messages of the reports and results (MainWindow.ExportReport, ExportJson).</summary>
    public const string CompleteFirst = "Completare il calcolo prima di esportare il report.";
    /// <inheritdoc cref="CompleteFirst"/>
    public const string WaitConcrete = "Attendere l’aggiornamento automatico e correggere gli eventuali dati non validi.";
    /// <inheritdoc cref="CompleteFirst"/>
    public const string CalculateFirst = "Premere Calcola prima di esportare i risultati.";
    /// <inheritdoc cref="CompleteFirst"/>
    public const string MaterialResults = "Per conservare la scheda materiali usa Salva o Esporta foglio selezionato.";

    private readonly WorkspaceSession workspace;
    private readonly PresentationServices services;
    private readonly ISheetExports exports;
    private readonly IDocumentFiles files;
    private readonly CommandGuard guard;
    private ProjectSession? project;
    private bool shortRunning;

    public ShellCommandsViewModel(WorkspaceSession workspace, PresentationServices services, ISheetExports exports, IDocumentFiles files)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(exports);
        ArgumentNullException.ThrowIfNull(files);
        this.workspace = workspace; this.services = services; this.exports = exports; this.files = files;
        guard = new CommandGuard(services.Messages);
        IRelayCommand New(string module) => GuardedCommands.Create(guard, () => workspace.NewCalculation(module));
        NewProjectCommand = GuardedCommands.Create(guard, () => workspace.NewProject());
        OpenCommand = GuardedCommands.Create(guard, Open);
        OpenExampleCommand = GuardedCommands.Create(guard, () => ExamplesRequested?.Invoke(this, EventArgs.Empty));
        SaveCommand = GuardedCommands.Create(guard, () => Save(false));
        SaveAsCommand = GuardedCommands.Create(guard, () => Save(true));
        ExportSheetCommand = GuardedCommands.Create(guard, ExportSheet);
        ReportCommand = new AsyncRelayCommand(() => guard.RunAsync(_ => ExportReportAsync()));
        ShortReportCommand = new AsyncRelayCommand(ExportShortReportAsync, () => !shortRunning);
        ResultsCommand = GuardedCommands.Create(guard, ExportResults);
        ExitCommand = GuardedCommands.Create(guard, () => ExitRequested?.Invoke(this, EventArgs.Empty));
        RheologyCommand = GuardedCommands.Create(guard, () => RheologyRequested?.Invoke(this, EventArgs.Empty));
        UndoCommand = GuardedCommands.Create(guard, () => workspace.Project?.Restore(false), () => workspace.Project?.CanUndo == true);
        RedoCommand = GuardedCommands.Create(guard, () => workspace.Project?.Restore(true), () => workspace.Project?.CanRedo == true);
        File =
        [
            new("Nuovo palo", New("geo_palo_verticale"), new("N")), new("Nuovo micropalo", New("geo_micropalo_verticale")),
            new("Nuova sezione in c.a.", New("str_palo")), new("Nuovo archivio progetti", NewProjectCommand),
            new("Nuova sezione composta da ponte", New("str_mista_ponte")), new("Nuovo Bridge Design", New("str_bridge_design")),
            new("Nuovo muro di sostegno", New("geo_muri_sostegno")), new("Nuovo palo orizzontale", New("geo_palo_orizzontale")),
            new("Nuova palificata orizzontale", New("geo_efficienza_orizzontale")), new("Nuovo micropalo orizzontale", New("geo_micropalo_orizzontale")),
            ShellMenuItem.Separator,
            new("Apri…", OpenCommand, new("O")), new("Apri esempio…", OpenExampleCommand), new("Salva", SaveCommand, new("S")),
            new("Salva con nome…", SaveAsCommand, new("S", true)), new("Esporta foglio selezionato…", ExportSheetCommand),
            ShellMenuItem.Separator,
            new("Report Word…", ReportCommand), new("Report short CLS…", ShortReportCommand), new("Risultati JSON…", ResultsCommand), new("Esci", ExitCommand)
        ];
        workspace.ActiveChanged += OnWorkspaceChanged;
        workspace.DocumentsChanged += OnWorkspaceChanged;
        Attach();
    }

    /// <summary>The menu «File», in the order of today (MainWindow.BuildMenu).</summary>
    public IReadOnlyList<ShellMenuItem> File { get; }

    /// <summary>The choices of the menu «Aspetto», applied by the view (the palette is WPF).</summary>
    public IReadOnlyList<string> Appearances { get; } = ["Chiaro", "Scuro", "Molto scuro"];

    public IRelayCommand NewProjectCommand { get; }
    public IRelayCommand OpenCommand { get; }
    public IRelayCommand OpenExampleCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand SaveAsCommand { get; }
    public IRelayCommand ExportSheetCommand { get; }
    public IAsyncRelayCommand ReportCommand { get; }
    public IAsyncRelayCommand ShortReportCommand { get; }
    public IRelayCommand ResultsCommand { get; }
    public IRelayCommand ExitCommand { get; }
    public IRelayCommand RheologyCommand { get; }
    public IRelayCommand UndoCommand { get; }
    public IRelayCommand RedoCommand { get; }

    /// <summary>«Report short» shows in the bar of a concrete section only (MainWindow.LoadSheetEditor).</summary>
    public bool ShortReportVisible => workspace.Active?.Module == "str_palo";

    /// <summary>«Apri esempio…»: the page of the examples.</summary>
    public event EventHandler? ExamplesRequested;

    /// <summary>«Esci»: the main window closes (with the questions of the coordinator).</summary>
    public event EventHandler? ExitRequested;

    /// <summary>«Reologia del calcestruzzo…»: the window of the tool (not modal).</summary>
    public event EventHandler? RheologyRequested;

    /// <summary>«Apri…» (MainWindow.Open): the file chosen opens, or is activated when it is open already.</summary>
    public void Open()
    {
        if (services.Files.OpenFile(new OpenFileRequest(WorkspaceSession.OpenFilters)) is { } path) workspace.OpenFile(path);
    }

    /// <summary>«Salva» and «Salva con nome…»: the document of the sheet with the focus, otherwise the project.</summary>
    public bool Save(bool asNew) => workspace.CurrentDocument is { } document && workspace.Save(document, asNew);

    /// <summary>«Esporta foglio selezionato…» (MainWindow.ExportSheet): the sheet with the focus as a single file «Foglio.programma».</summary>
    public void ExportSheet()
    {
        workspace.CommitActive();
        if (workspace.Active is not { } sheet) return;
        var path = services.Files.SaveFile(new SaveFileRequest(WorkspaceSession.SaveFilters, "Foglio.programma"));
        if (path is null) return;
        files.Save(path, J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"), ("modulo_id", sheet.Sheet["modulo_id"]), ("dati", sheet.Sheet["dati"])));
    }

    /// <summary>«Risultati JSON…» (MainWindow.ExportJson): not for the materials; only with results; «Risultati.json».</summary>
    public void ExportResults()
    {
        if (workspace.Active?.Module is "mat_calcestruzzo" or "mat_acciaio_armatura") { services.Messages.Inform("", MaterialResults); return; }
        workspace.CommitActive();
        if (workspace.Active is not { ViewModel.HasResults: true } sheet)
        {
            services.Messages.Inform("", workspace.Active?.Module == "str_palo" ? WaitConcrete : CalculateFirst);
            return;
        }
        if (services.Files.SaveFile(new SaveFileRequest([new FileFilter("Risultati JSON", "*.json")], "Risultati.json")) is { } path) exports.ExportResult(sheet, path);
    }

    /// <summary>
    /// «Report Word…» (MainWindow.ExportReport): stratigraphy, foundations and materials with a fixed name; the other modules only with
    /// results not being calculated; the horizontal piles and pile groups, Bridge Design and walls with a fixed name; the others after the
    /// choice of the contents («Contenuti del report Word»), «Relazione.docx».
    /// </summary>
    public async Task ExportReportAsync()
    {
        var sheet = workspace.Active;
        string? module = sheet?.Module;
        if (module is "geo_stratigrafia" or "geo_fondazioni_superficiali" or "mat_calcestruzzo" or "mat_acciaio_armatura")
        {
            workspace.CommitActive();
            if (module == "geo_fondazioni_superficiali" && !sheet!.ViewModel.HasResults) { services.Messages.Inform("", CompleteFirst); return; }
            Export(sheet!, module switch
            {
                "geo_stratigrafia" => "Stratigrafia.docx",
                "geo_fondazioni_superficiali" => "Fondazioni_superficiali.docx",
                "mat_calcestruzzo" => "Scheda_calcestruzzo.docx",
                _ => "Scheda_acciaio_armature.docx"
            }, new HashSet<string>());
            return;
        }
        workspace.CommitActive();
        if (sheet is not { ViewModel.HasResults: true } || sheet.ViewModel.IsBusy)
        {
            services.Messages.Inform("", module == "str_palo" ? WaitConcrete : CompleteFirst);
            return;
        }
        if (module is "geo_efficienza_orizzontale" or "geo_palo_orizzontale" or "geo_micropalo_orizzontale" or "str_bridge_design" or "geo_muri_sostegno")
        {
            Export(sheet, module switch
            {
                "geo_efficienza_orizzontale" => "Relazione_palificata_orizzontale.docx",
                "geo_muri_sostegno" => "Relazione_muro_sostegno.docx",
                "str_bridge_design" => "BridgeDesign.docx",
                "geo_micropalo_orizzontale" => "Relazione_micropalo_orizzontale.docx",
                _ => "Relazione_palo_orizzontale.docx"
            }, new HashSet<string>());
            return;
        }
        var choice = await services.Dialogs.ShowDialogAsync(new ReportSectionsDialogViewModel(sheet.Module, sheet.ViewModel.Data));
        if (choice is null) return;
        Export(sheet, "Relazione.docx", choice);
    }

    private void Export(SheetSession sheet, string name, IReadOnlySet<string> options)
    {
        if (services.Files.SaveFile(new SaveFileRequest([new FileFilter(WordFilter, "*.docx")], name)) is { } path)
            exports.ExportReport(sheet, path, sheet.Heading, options);
    }

    /// <summary>
    /// «Report short» (MainWindow.ExportShortReport): only for a concrete section; Word and PDF within two pages written away from the UI
    /// thread, the command disabled meanwhile; the outcome and the errors as the notes of today («Report short»).
    /// </summary>
    public async Task ExportShortReportAsync()
    {
        if (shortRunning) return;
        try
        {
            workspace.CommitActive();
            if (workspace.Active is not { Module: "str_palo" } sheet) throw new InvalidOperationException("Selezionare una sezione in calcestruzzo armato.");
            string title = sheet.Heading;
            var bytes = exports.BuildShortReport(sheet, title);
            var path = services.Files.SaveFile(new SaveFileRequest([new FileFilter("Report short Word e PDF", "*.docx")], "Relazione_short.docx"));
            if (path is null) return;
            SetShortRunning(true);
            await exports.WriteShortReportAsync(path, bytes, title);
            services.Messages.Inform(ShortTitle, "Report short salvato in Word e PDF, entro due pagine.");
        }
        catch (Exception error) { services.Messages.Warn(ShortTitle, error.Message); }
        finally { SetShortRunning(false); }
    }

    protected override void OnDisposed()
    {
        workspace.ActiveChanged -= OnWorkspaceChanged;
        workspace.DocumentsChanged -= OnWorkspaceChanged;
        if (project is not null) { project.Changed -= OnProjectChanged; project.StateChanged -= OnProjectChanged; }
    }

    private void SetShortRunning(bool running)
    {
        shortRunning = running;
        ShortReportCommand.NotifyCanExecuteChanged();
    }

    private void Attach()
    {
        if (ReferenceEquals(project, workspace.Project)) return;
        if (project is not null) { project.Changed -= OnProjectChanged; project.StateChanged -= OnProjectChanged; }
        project = workspace.Project;
        if (project is not null) { project.Changed += OnProjectChanged; project.StateChanged += OnProjectChanged; }
    }

    private void OnWorkspaceChanged(object? sender, EventArgs e)
    {
        Attach();
        OnPropertyChanged(nameof(ShortReportVisible));
        UndoCommand.NotifyCanExecuteChanged(); RedoCommand.NotifyCanExecuteChanged();
    }

    private void OnProjectChanged(object? sender, EventArgs e) { UndoCommand.NotifyCanExecuteChanged(); RedoCommand.NotifyCanExecuteChanged(); }
}

/// <summary>A content of the report with its choice.</summary>
public sealed class ReportSectionChoice(string key, string label, bool isChecked) : ViewModelBase
{
    private bool isChecked = isChecked;

    public string Key { get; } = key;
    public string Label { get; } = label;
    public bool IsChecked { get => isChecked; set { if (SetProperty(ref isChecked, value)) Changed?.Invoke(this, EventArgs.Empty); } }

    internal event EventHandler? Changed;
}

/// <summary>
/// «Contenuti del report Word» (MainWindow.ExportReport): the sections of the report of the module (ReportBridge for the composite section,
/// ReportConcrete for the RC section, ReportWord for the vertical piles and the others), checked as saved in the sheet («report_sezioni») or
/// with the defaults of today, the note of the module; «Esporta» needs a content other than the options «sle_tutte», «dettagli», «grafici».
/// </summary>
public sealed class ReportSectionsDialogViewModel : DialogViewModel<IReadOnlySet<string>>
{
    public const string BridgeNote = "Stampa tutte le situazioni e i rispettivi contributi, indipendentemente dalla fase visualizzata. Ambito, riepilogo e avvisi sono sempre inclusi. I grafici riportano la geometria e la somma dei contributi di ogni situazione.";
    public const string ConcreteNote = "SLE: di default inviluppo degli estremi con combinazione di origine e casi governanti distinti per tensioni e fessurazione. Nessun esito non determinato viene escluso. In modalità completa si stampano tutte le combinazioni; i grafici SLE restano riferiti ai casi governanti. Ambito, avvisi ed errori sono sempre inclusi.";

    public ReportSectionsDialogViewModel(string module, JsonObject data) : base("Contenuti del report Word")
    {
        ArgumentNullException.ThrowIfNull(data);
        bool bridge = module == "str_mista_ponte", concrete = module == "str_palo";
        var saved = data[bridge ? "ui_mista" : "workspace_ca"]?["report_sezioni"] as JsonArray;
        IEnumerable<(string Key, string Label)> sections = bridge ? ReportBridge.Sections : concrete ? ReportConcrete.Sections : ReportWord.Sezioni;
        Sections = sections.Select(s => new ReportSectionChoice(s.Key, s.Label, saved is not null ? saved.Any(v => v?.ToString() == s.Key)
            : bridge || (concrete ? s.Key is not ("dettagli" or "sle_tutte") : !s.Key.StartsWith("grafico", StringComparison.Ordinal)))).ToArray();
        Note = bridge ? BridgeNote : concrete ? ConcreteNote : null;
        ExportCommand = new RelayCommand(() => Close(Sections.Where(s => s.IsChecked).Select(s => s.Key).ToHashSet()), () => CanExport);
        CancelCommand = new RelayCommand(Cancel);
        foreach (var section in Sections) section.Changed += (_, _) => ExportCommand.NotifyCanExecuteChanged();
    }

    public IReadOnlyList<ReportSectionChoice> Sections { get; }

    /// <summary>The note under the sections, null for the modules without one.</summary>
    public string? Note { get; }

    /// <summary>A content of the report is chosen, not only options.</summary>
    public bool CanExport => Sections.Any(s => s.IsChecked && s.Key is not ("sle_tutte" or "dettagli" or "grafici"));

    public string ExportLabel => "Esporta";
    public IRelayCommand ExportCommand { get; }
    public IRelayCommand CancelCommand { get; }
}
