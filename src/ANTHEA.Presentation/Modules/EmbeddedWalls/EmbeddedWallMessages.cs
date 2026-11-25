using System.Globalization;
using Anthea.Presentation.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Anthea.Presentation.Modules.EmbeddedWalls;

/// <summary>Level of a line of the panel Messaggi.</summary>
public enum EmbeddedWallMessageLevel { Information, Warning, Error }

/// <summary>Where a line of the panel leads: a case, a phase of the case, a row of a table (or «general», the settings of the wall).
/// Built from structured data only: the case and the phase of the progress (<c>EmbeddedWallProgress.CaseId/StageId</c>), the results
/// (case, phase of the filtration), the rows of the drawing; never read back from a text.</summary>
public sealed record EmbeddedWallMessageTarget(string? CaseId, string? StageId, string? Table, EmbeddedWallRow? Row);

/// <summary>A message before it enters the panel: level, origin («calcolo», «fase», «dato»), the text already produced by ANTHEA or GPC, its
/// provenance (request, results or data it comes from) and where it leads.</summary>
public sealed record EmbeddedWallMessageDraft(EmbeddedWallMessageLevel Level, string Origin, string Text, string Provenance, EmbeddedWallMessageTarget? Target);

/// <summary>A line of the panel Messaggi: the draft and the time it first appeared.</summary>
public sealed class EmbeddedWallMessage
{
    internal EmbeddedWallMessage(EmbeddedWallMessageDraft draft, DateTimeOffset time) { Draft = draft; Time = time; }
    internal EmbeddedWallMessageDraft Draft { get; }
    public DateTimeOffset Time { get; }
    public EmbeddedWallMessageLevel Level => Draft.Level;
    public string Origin => Draft.Origin;
    public string Text => Draft.Text;
    public string Provenance => Draft.Provenance;
    public EmbeddedWallMessageTarget? Target => Draft.Target;
    public string TimeText => Time.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);
    /// <summary>The level in words: the colour of the view is never the only sign of it.</summary>
    public string LevelText => Level switch { EmbeddedWallMessageLevel.Error => "Errore", EmbeddedWallMessageLevel.Warning => "Avviso", _ => "Info" };
    public bool IsError => Level == EmbeddedWallMessageLevel.Error;
    public bool IsWarning => Level == EmbeddedWallMessageLevel.Warning;
    /// <summary>Case, phase and table the line refers to, empty without a target.</summary>
    public string Place => Target is not { } t ? "" : string.Join(" · ", new[]
    {
        t.CaseId, t.StageId is null ? null : "fase " + t.StageId,
        t.Table is null ? null : t.Table == EmbeddedWallMessages.GeneralTarget ? "Impostazioni" : "«" + EmbeddedWallFields.Title(t.Table) + "»"
    }.Where(p => !string.IsNullOrWhiteSpace(p)));
    public bool CanOpen => Target is not null;
    public string Hint => Provenance + (CanOpen ? Environment.NewLine + "Clic: apre l'elemento del modello." : "");
}

/// <summary>
/// The panel Messaggi at the bottom of the paratie sheet (organizzazione del modulo paratie, progetto del 16/11, step 11, S16). It lists the
/// warnings and errors already produced by ANTHEA and GPC: the progress and the state of the last request, the error of the engine, the
/// diagnostics of the results on screen and the notices of the data drawn. No message is written here: the sheet passes them with their
/// provenance (R06: the lines of a request superseded by a change of the data do not enter). Filter by level, «Pulisci» (state of the view
/// only: a line cleared comes back if it disappears and is produced again), the time a line first appeared, a click that opens its element.
/// </summary>
public sealed class EmbeddedWallMessages : ObservableObject
{
    public const string Calculation = "calcolo", Stage = "fase", Data = "dato", GeneralTarget = "general";
    public const string AllLevels = "Tutti", WarningsAndErrors = "Avvisi ed errori", ErrorsOnly = "Solo errori";

    private readonly Func<DateTimeOffset> clock;
    private readonly Dictionary<EmbeddedWallMessageDraft, EmbeddedWallMessage> known = [];
    private readonly HashSet<EmbeddedWallMessageDraft> cleared = [];
    private IReadOnlyList<EmbeddedWallMessage> all = [];
    private string filter = AllLevels;
    private bool collapsed;

    internal EmbeddedWallMessages(CommandGuard guard, Action<EmbeddedWallMessage> open, Func<DateTimeOffset>? clock = null)
    {
        this.clock = clock ?? (() => DateTimeOffset.Now);
        OpenCommand = new RelayCommand<EmbeddedWallMessage>(m => guard.Run(() => { if (m is { CanOpen: true }) open(m); }));
        ClearCommand = GuardedCommands.Create(guard, Clear, () => Items.Count > 0);
        ToggleCommand = GuardedCommands.Create(guard, () => IsCollapsed = !IsCollapsed);
    }

    public IReadOnlyList<string> Filters { get; } = Array.AsReadOnly(new[] { AllLevels, WarningsAndErrors, ErrorsOnly });

    /// <summary>The levels shown; an unknown value is ignored.</summary>
    public string Filter
    {
        get => filter;
        set { if (value is not null && Filters.Contains(value) && SetProperty(ref filter, value)) Show(); }
    }

    /// <summary>The lines shown, newest first and, at the same time, errors before warnings and information.</summary>
    public IReadOnlyList<EmbeddedWallMessage> Items { get; private set; } = [];

    /// <summary>Every current line, also those filtered out or cleared.</summary>
    public IReadOnlyList<EmbeddedWallMessage> All => all;

    public string Summary
    {
        get
        {
            int errors = all.Count(m => m.IsError), warnings = all.Count(m => m.IsWarning);
            return $"{errors} {(errors == 1 ? "errore" : "errori")} · {warnings} {(warnings == 1 ? "avviso" : "avvisi")}"
                + (Items.Count < all.Count ? $" · {all.Count - Items.Count} non mostrati" : "");
        }
    }
    public bool IsEmpty => Items.Count == 0;
    public bool IsCollapsed { get => collapsed; set { if (SetProperty(ref collapsed, value)) OnPropertyChanged(nameof(ToggleText)); } }
    public string ToggleText => IsCollapsed ? "Mostra" : "Nascondi";

    public IRelayCommand<EmbeddedWallMessage> OpenCommand { get; }
    public IRelayCommand ClearCommand { get; }
    public IRelayCommand ToggleCommand { get; }

    /// <summary>The messages the sheet has now: a line already present keeps its time; the lines no longer produced leave the panel.</summary>
    internal void Update(IEnumerable<EmbeddedWallMessageDraft> drafts)
    {
        var current = drafts.Where(d => !string.IsNullOrWhiteSpace(d.Text)).Distinct().ToArray();
        var now = clock();
        foreach (var draft in known.Keys.Except(current).ToArray()) known.Remove(draft);
        cleared.IntersectWith(current);
        all = current.Select((d, order) => (Message: known.TryGetValue(d, out var m) ? m : known[d] = new(d, now), Order: order))
            .OrderByDescending(x => x.Message.Time).ThenByDescending(x => x.Message.Level).ThenBy(x => x.Order).Select(x => x.Message).ToArray();
        Show();
    }

    private void Clear()
    {
        cleared.UnionWith(all.Select(m => m.Draft));
        Show();
    }

    private void Show()
    {
        var shown = all.Where(m => !cleared.Contains(m.Draft) && filter switch
        {
            ErrorsOnly => m.IsError,
            WarningsAndErrors => m.IsError || m.IsWarning,
            _ => true
        }).ToArray();
        if (!shown.SequenceEqual(Items)) { Items = shown; OnPropertyChanged(nameof(Items)); OnPropertyChanged(nameof(IsEmpty)); }
        OnPropertyChanged(nameof(All)); OnPropertyChanged(nameof(Summary));
        ClearCommand.NotifyCanExecuteChanged();
    }
}
