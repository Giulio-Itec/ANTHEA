using Anthea.Application.Concrete;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Presentation.Forms;
using Anthea.Presentation.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.ConcreteSection;

/// <summary>
/// The reinforcement design of the RC section (tab 08 «Calcola armature», F5.10 sub-step S8: ConcreteDesign.cs of the historic view), without WPF:
/// <list type="bullet">
/// <item>the form of the search on <c>calcola_armature</c> (<see cref="Form"/>, its fields shown as the section, the element and the options
/// ask), the limits of every check (<see cref="Limits"/>) and «Mostra le prime» (<see cref="Shown"/>);</item>
/// <item>«Calcola armature» (<see cref="RunCommand"/>): an asynchronous search on a copy of the data, with its progress and «Ferma ricerca»
/// (<see cref="StopCommand"/>, the cancellation), then the outcome, the exclusions and the alternatives (<see cref="Ranking"/>);</item>
/// <item>the selected proposal: its description, metrics, scope, checks and section (<see cref="Preview"/>, plain data, decisions Q3 and C8);</item>
/// <item>«Applica configurazione» (<see cref="ApplyCommand"/>): the proposal written into the data of the sheet
/// (<see cref="ConcreteDesignPresentation.Apply"/>), the forms of the inputs, of the detailing and of the shear shown again, the bars cleared and
/// the sheet invalidated, as the historic view.</item>
/// </list>
/// A change of the data of a search (every Modified of the sheet, as the historic view) marks it stale: «Applica» is refused and a search in
/// progress is cancelled. The three commands are disabled in a read-only revision, as the historic buttons.
/// </summary>
public sealed class ConcreteDesignViewModel : ObservableObject, IDisposable, IViewTabMemory
{
    private readonly ConcreteSheetViewModel sheet;

    /// <summary>Inner tabs chosen in the view of the design (<see cref="IViewTabMemory"/>, SH49 reduced): in memory, not saved.</summary>
    public IDictionary<string, int> SelectedTabs { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

    private ConcreteResults.DesignResult? result;
    private ConcreteResults.DesignCandidate? selection;
    private CancellationTokenSource? cancellation;
    private string? signature;
    private bool running, stale, disposed;
    private double progress;
    private string status = ConcreteDesignPresentation.Idle, description = ConcreteDesignPresentation.NoProposal, scope = "", diagnostics = "";

    internal ConcreteDesignViewModel(ConcreteSheetViewModel sheet)
    {
        this.sheet = sheet;
        var options = sheet.DesignOptions;
        // BuildDesignPanel of the historic view, in its order; nothing is written (the options were prepared by the sheet).
        Form = new FormViewModel(options,
        [new("obiettivo", "Ottimizza", Choices: ["Peso", "Numero barre", "Numero diametri", "Compromesso"]),
         new("simmetrica", "Armatura superiore e inferiore uguale", Bool: true),
         new("diametri", "Ø longitudinali ammessi · separati da ;", "mm", Wide: true),
         new("n_superiori", "Numeri barre superiori", Wide: true), new("n_inferiori", "Numeri barre inferiori", Wide: true),
         new("n_laterali", "Numeri barre laterali per lato", Wide: true), new("n_circolari", "Numeri barre circolari", Wide: true),
         new("staffe", "Staffe", Choices: ["Ottimizza", "Mantieni", "Senza staffe", "Con e senza staffe"]),
         new("diametri_staffe", "Ø staffe ammessi", "mm", Wide: true), new("passi_staffe", "Passi staffe ammessi", "mm", Wide: true),
         new("diametri_ortogonali", "Ø ortogonali · entrambe le facce", "mm", Wide: true), new("passi_ortogonali", "Passi ortogonali ammessi", "mm", Wide: true),
         new("max_diametri", "Massimo Ø distinti"), new("ricerca_completa", "Esamina tutte le configurazioni ammesse", Bool: true), new("max_tentativi", "Massimo tentativi (campione)"),
         new("parallelismo", "Calcoli contemporanei", Choices: ["Automatico", "1", "2", "4", "8"]),
         new("ganci_phi", "Extra sviluppo staffa / legatura", "Ø"), new("riserva_torsione", "Area riservata a torsione per barra", "%"),
         new("peso_peso", "Peso obiettivo · kg/m"), new("peso_barre", "Peso obiettivo · pezzi/m"), new("peso_diametri", "Peso obiettivo · Ø distinti"),
         new("ancoraggio_confermato", "Confermo Asl ancorata per proposte senza staffe", Bool: true),
         new("torsione_confermata", "Confermo schema periferico chiuso per torsione", Bool: true)], FormChanged, compact: true, wideChoices: true)
        {
            CommitOnFocusLoss = true, FormatNumbers = true, WikiNavigator = sheet.Wiki, IsReadOnly = sheet.IsReadOnly
        };
        Limits = new FormViewModel(options, ConcreteEditor.DesignTargets.Select(t => new FieldSpec(t.Key, t.Name, "η max")), LimitChanged, compact: true)
        {
            CommitOnFocusLoss = true, FormatNumbers = true, WikiNavigator = sheet.Wiki, IsReadOnly = sheet.IsReadOnly
        };
        Shown = new FormViewModel(options, [new("mostra", "Mostra le prime", Choices: [.. ConcreteDesignPresentation.ShownChoices])], ShownChanged, compact: true, wideChoices: true)
        {
            CommitOnFocusLoss = true, FormatNumbers = true, WikiNavigator = sheet.Wiki, IsReadOnly = sheet.IsReadOnly
        };
        Ranking = new ConcreteTableViewModel([new("rank", "#", readOnly: true), new("config", "Configurazione", readOnly: true), new("peso", "kg/m", readOnly: true),
            new("barre", "Barre long.", readOnly: true), new("pezzi", "Pezzi/m", readOnly: true), new("diametri", "Ø diversi", readOnly: true), new("pareto", "Pareto", readOnly: true),
            new("esito", "Dettagli esecutivi", readOnly: true)], [], () => sheet.IsReadOnly);
        Ranking.SelectionChanged += (_, _) => RankingSelected();
        RunCommand = new AsyncRelayCommand(RunAsync, () => sheet.CanEdit && !running, AsyncRelayCommandOptions.AllowConcurrentExecutions);
        StopCommand = new RelayCommand(() => cancellation?.Cancel(), () => sheet.CanEdit && running);
        ApplyCommand = new RelayCommand(Apply, () => sheet.CanEdit && applicable);
        ShowTabCommand = new RelayCommand<int>(tab => sheet.SelectedTab = tab);
        sheet.Modified += OnSheetModified;
        RefreshInputs();
    }

    /// <summary>«Ricerca dell’armatura».</summary>
    public FormViewModel Form { get; }
    /// <summary>«Limiti per ciascuna verifica» (η max of every check).</summary>
    public FormViewModel Limits { get; }
    /// <summary>«Mostra le prime».</summary>
    public FormViewModel Shown { get; }
    /// <summary>«Alternative»: the first proposals, their metrics; the selection is the proposal shown.</summary>
    public ConcreteTableViewModel Ranking { get; }
    /// <summary>The value cards of the proposal.</summary>
    public ConcreteCardsViewModel Metrics { get; } = new(2);
    /// <summary>«Verifiche della proposta».</summary>
    public ConcreteCardsViewModel Checks { get; } = new(2);
    /// <summary>«Configurazione selezionata»: the section of the proposal with its links, as plain data.</summary>
    public ConcreteSectionDrawingViewModel Preview { get; } = new();

    /// <summary>The status above the results (progress, outcome, stale data, errors).</summary>
    public string Status { get => status; private set => SetProperty(ref status, value); }
    /// <summary>The description of the proposal shown.</summary>
    public string Description { get => description; private set => SetProperty(ref description, value); }
    /// <summary>The scope of the proposal shown.</summary>
    public string Scope { get => scope; private set => SetProperty(ref scope, value); }
    /// <summary>«Ricerca e scarti».</summary>
    public string Diagnostics { get => diagnostics; private set => SetProperty(ref diagnostics, value); }
    /// <summary>The progress bar of the search (0–1).</summary>
    public double Progress { get => progress; private set => SetProperty(ref progress, value); }
    /// <summary>The notes of the tab.</summary>
    public string Notice => ConcreteDesignPresentation.Notice;
    public string LimitsNotice => ConcreteDesignPresentation.LimitsNotice;

    /// <summary>A search is running.</summary>
    public bool IsRunning => running;
    /// <summary>The data changed after the search: its proposals cannot be applied.</summary>
    public bool IsStale => stale;

    /// <summary>«Calcola armature».</summary>
    public IAsyncRelayCommand RunCommand { get; }
    /// <summary>«Ferma ricerca».</summary>
    public IRelayCommand StopCommand { get; }
    /// <summary>«Applica configurazione».</summary>
    public IRelayCommand ApplyCommand { get; }
    /// <summary>
    /// «Sezione →» (0), «Dettagli →» (5), «SLE / esposizione →» (3): the tab of the sheet. F5.15, S8-b (F5-46, rule T6): allowed in a read-only
    /// revision, since it only changes the tab.
    /// </summary>
    public IRelayCommand<int> ShowTabCommand { get; }

    // The state of «Applica configurazione» (the IsEnabled of the historic button).
    private bool applicable;
    private void SetApplicable(bool value) { applicable = value; ApplyCommand.NotifyCanExecuteChanged(); }
    private void SetRunning(bool value)
    {
        running = value; OnPropertyChanged(nameof(IsRunning));
        RunCommand.NotifyCanExecuteChanged(); StopCommand.NotifyCanExecuteChanged();
    }

    // ---------------------------------------------------------------- options and freshness

    private void FormChanged(string key) { MarkStale(); RefreshInputs(); sheet.RaiseModified(); }
    private void LimitChanged(string key) { MarkStale(); sheet.RaiseModified(); }
    private void ShownChanged(string key) { RefreshRanking(); sheet.RaiseModified(); }

    /// <summary>RefreshDesignInputs: the fields of the search shown for the section, the element and the options.</summary>
    private void RefreshInputs()
    {
        foreach (var (key, shown) in ConcreteDesignPresentation.FieldsShown(sheet.Input, sheet.Settings)) Form.ShowField(key, shown);
    }

    /// <summary>MarkDesignStale: a search done or running is out of date; the running one is cancelled.</summary>
    private void MarkStale()
    {
        if (result is null && !running) return;
        stale = true; OnPropertyChanged(nameof(IsStale));
        cancellation?.Cancel(); SetApplicable(false);
        Status = ConcreteDesignPresentation.Stale;
    }

    // Every Modified of the sheet (RefreshDesignFreshness, subscribed to the Modified of the historic view).
    private void OnSheetModified(object? sender, EventArgs e) => RefreshFreshness();

    private void RefreshFreshness()
    {
        if (disposed) return;
        RefreshInputs();
        if (signature is not null && signature != ConcreteDesignPresentation.Fingerprint(sheet.Data)) MarkStale();
    }

    /// <summary>The read-only revision changed: the forms, the table and the commands follow.</summary>
    internal void RefreshEditable(bool readOnly)
    {
        Form.IsReadOnly = readOnly; Limits.IsReadOnly = readOnly; Shown.IsReadOnly = readOnly; Ranking.RefreshReadOnly();
        RunCommand.NotifyCanExecuteChanged(); StopCommand.NotifyCanExecuteChanged(); ApplyCommand.NotifyCanExecuteChanged();
    }

    // ---------------------------------------------------------------- search

    // RunDesignAsync of the historic view: the sheet confirmed, the results cleared, the search on a copy of the data on a worker thread with its
    // progress (only while it is current), then the outcome; a cancellation or an error in the status.
    private async Task RunAsync()
    {
        if (running || disposed || sheet.IsDisposed) return;
        sheet.CommitSheet();
        result = null; selection = null; Ranking.Rows.Clear(); Checks.Start(); Metrics.Start();
        SetApplicable(false); stale = false; OnPropertyChanged(nameof(IsStale)); Progress = 0;
        Diagnostics = "";
        Description = ConcreteDesignPresentation.Searching; Scope = ""; Preview.Section = null; Preview.Invalidate();
        try
        {
            var snapshot = (System.Text.Json.Nodes.JsonObject)sheet.Data.DeepClone();
            signature = ConcreteDesignPresentation.Fingerprint(sheet.Data); SetRunning(true);
            cancellation?.Dispose(); cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var updates = new Progress<ConcreteResults.DesignProgress>(p =>
            {
                if (disposed || stale || !running || token.IsCancellationRequested || cancellation?.Token != token) return;
                Progress = ConcreteDesignPresentation.Fraction(p.Evaluated, p.Planned);
                Status = ConcreteDesignPresentation.ProgressText(p);
                if (p.Best is not null && selection?.Id != p.Best.Id) Select(p.Best);
            });
            // 1.16.0, D-5: in a place of the limiter of the application («Interrompi» stops also the wait for it).
            var calculated = await sheet.Limiter.RunAsync(() => StructuralQueries.ConcreteReinforcement(snapshot, token, updates), token);
            if (disposed || sheet.IsDisposed) return;
            result = calculated;
            SetRunning(false); RefreshFreshness();
            RefreshRanking();
            var r = calculated;
            Progress = ConcreteDesignPresentation.Fraction(r.Evaluated, r.Planned);
            if (!stale) Status = ConcreteDesignPresentation.Status(r);
            if (r.Candidates.Count == 0) (Description, Scope) = ConcreteDesignPresentation.NoCandidates(r);
            Diagnostics = ConcreteDesignPresentation.Diagnostics(r);
        }
        catch (OperationCanceledException) { if (!disposed && !sheet.IsDisposed) Status = ConcreteDesignPresentation.Interrupted; }
        // The historic view shows every failure of the search in its status.
        catch (Exception failure)
        {
            if (!disposed && !sheet.IsDisposed) { Status = ConcreteDesignPresentation.NotStarted(failure.Message); Description = ConcreteDesignPresentation.CompleteTheData; }
        }
        finally
        {
            if (running) SetRunning(false);
            cancellation?.Dispose(); cancellation = null;
            if (!disposed && !sheet.IsDisposed) { StopCommand.NotifyCanExecuteChanged(); SetApplicable(selection is not null && result is not null && !stale); }
        }
    }

    /// <summary>RefreshDesignRanking: the alternatives again (after a search, or «Mostra le prime»), the selected proposal kept when it is shown, otherwise the first.</summary>
    private void RefreshRanking()
    {
        if (result is null) return;
        int? selectedId = selection?.Id;
        Ranking.Rows.Clear();
        foreach (var row in ConcreteDesignPresentation.RankingRows(result, sheet.DesignOptions)) Ranking.Rows.Add(new JsonRowViewModel(row, _ => { }));
        if (Ranking.Rows.Count > 0) Ranking.Selected = Ranking.Rows.FirstOrDefault(r => r.Values.S("id") == selectedId?.ToString()) ?? Ranking.Rows[0];
    }

    // The selection of «Alternative»: its proposal shown.
    private void RankingSelected()
    {
        if (Ranking.Selected is not { } row || result is null) return;
        var candidate = result.Candidates.FirstOrDefault(c => c.Id.ToString() == row.Values.S("id"));
        if (candidate is not null) Select(candidate);
    }

    /// <summary>SelectDesign: the proposal shown with its section, description, metrics, scope and checks; «Applica» available when the search is current.</summary>
    private void Select(ConcreteResults.DesignCandidate c)
    {
        selection = c;
        Preview.Section = ConcretePreview.PrepareGeometry(c.Input); Preview.Stirrups = c.Shear; Preview.Invalidate();
        Description = c.Description;
        Metrics.Start();
        foreach (var value in ConcreteDesignPresentation.Metrics(c, result)) Metrics.AddValue(value.Title, value.Value, value.Explanation);
        Scope = ConcreteDesignPresentation.Scope(c);
        Checks.Start();
        foreach (var check in ConcreteDesignPresentation.Checks(c)) Checks.AddDetail(check.Title, check.Passed, check.Values, check.Explanation, check.Reference);
        SetApplicable(!running && !stale && result is not null);
    }

    // ---------------------------------------------------------------- «Applica configurazione»

    // ApplyDesign of the historic view: the sheet confirmed and the search checked again; the proposal written into the data, the forms of the
    // inputs, of the detailing and of the shear shown again, the bars cleared, the sheet invalidated; the status says what was applied.
    private void Apply()
    {
        sheet.CommitSheet(); RefreshFreshness();
        if (selection is not { } candidate || stale || running || disposed || sheet.IsDisposed) return;
        string notice = ConcreteDesignPresentation.Apply(sheet.Input, sheet.Settings, candidate, result!.Completed);
        sheet.DesignApplied();
        SetApplicable(false);
        Status = ConcreteDesignPresentation.Applied(notice);
    }

    /// <summary>The sheet was opened again or closed: the search in progress is cancelled, nothing is published.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        sheet.Modified -= OnSheetModified;
        cancellation?.Cancel();
    }
}
