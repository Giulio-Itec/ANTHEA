using System.Text.Json.Nodes;
using ANTHEA.ModelWorkspace;
using Anthea.Application.Presentation;
using Anthea.Calculations;
using Anthea.Presentation.Mvvm;
using Anthea.Presentation.Projects;
using Anthea.Presentation.Services;
using CommunityToolkit.Mvvm.Input;
using X.Core;

namespace Anthea.Presentation.Shell.Projects;

/// <summary>
/// The status line above a sheet of a project (F5.13 b, proposal 4.4; MainWindow.RefreshSharedStatus): the common data different from
/// the other sheets of the section and the checks of the cover, computed on the data being edited (PreviewData) without confirming them.
/// </summary>
public sealed class SharedStatusViewModel : ViewModelBase
{
    /// <summary>Properties named in the line before «e altre n proprietà».</summary>
    public const int ListedProperties = 5;

    private bool isVisible, isWarning;
    private string text = "", toolTip = "";

    public bool IsVisible { get => isVisible; private set => SetProperty(ref isVisible, value); }

    /// <summary>One message per line: the differences first, then the checks.</summary>
    public string Text { get => text; private set => SetProperty(ref text, value); }

    public string ToolTip { get => toolTip; private set => SetProperty(ref toolTip, value); }

    /// <summary>Orange with a difference or a failed check, green otherwise.</summary>
    public bool IsWarning { get => isWarning; private set => SetProperty(ref isWarning, value); }

    /// <summary>Recomputes the line for <paramref name="sheet"/> (null or outside a project: hidden).</summary>
    public void Update(SheetSession? sheet)
    {
        IsVisible = false;
        if (sheet?.Project is null || sheet.IsDisposed || sheet.Sheet.Parent?.Parent is not JsonObject section) return;
        var (temporary, active) = ProjectSharedData.PreviewSection(section, sheet.Sheet, sheet.ViewModel.PreviewData);
        var checks = ProjectValidation.CoverChecks(temporary, sheet.Module == "mat_calcestruzzo" ? null : active);
        var conflicts = ProjectSharedData.Differences(temporary).Where(d => ReferenceEquals(d.First, active) || ReferenceEquals(d.Second, active)).ToArray();
        var messages = checks.Select(c => c.Text).ToList();
        var properties = conflicts.Select(d => SharedDataCoordinator.FieldLabel(d.Key)).Distinct().ToArray();
        if (properties.Length > 0)
            messages.Insert(0, "⚠ Dati diversi tra i fogli: " + string.Join(", ", properties.Take(ListedProperties)) +
                (properties.Length > ListedProperties ? $" e altre {properties.Length - ListedProperties} proprietà" : "") + ". Apri il confronto nella pagina del progetto per uniformare.");
        if (messages.Count == 0) return;
        Text = string.Join("\n", messages);
        ToolTip = string.Join("\n", properties.Concat(checks.Select(c => c.Text)));
        IsWarning = !(conflicts.Length == 0 && checks.All(c => c.Passed == true));
        IsVisible = true;
    }
}

/// <summary>A sheet in a conflict of the comparison: name, location, value and the button «Uniforma a questo» or «Dato collegato».</summary>
public sealed class ConflictSheetViewModel
{
    internal ConflictSheetViewModel(JsonObject sheet, string key, string label, string group, bool readOnly, IRelayCommand<ConflictSheetViewModel> align)
    {
        Sheet = sheet; Key = key; Group = group;
        Name = ShellTexts.SheetName(sheet);
        Location = ProjectSharedData.Location(sheet);
        Value = label + ": " + ProjectSharedData.Text(ProjectSharedData.Fields(sheet)[key].Value);
        bool linked = ProjectSharedData.AncestorReferences(sheet, key).Length > 0;
        if (key.StartsWith("Strato · ", StringComparison.Ordinal) && key.EndsWith("/tipologia", StringComparison.Ordinal))
        {
            SoilTypes = ["Granulare", "Coesivo"];
            SoilType = ProjectSharedData.Fields(sheet)[key].Value?.ToString();
            SoilTypeAutomationName = "Tipologia del terreno · " + sheet.S("nome");
        }
        CanAlign = !readOnly && !linked;
        AlignLabel = linked ? "Dato collegato" : "Uniforma a questo";
        AlignToolTip = !linked ? "Uniforma " + label + " nella sezione di questo foglio e nelle sue sottosezioni." : "Questo dato è guidato dal livello superiore. Usa il pulsante sul foglio di riferimento.";
        AlignCommand = align;
    }

    public JsonObject Sheet { get; }
    public string Key { get; }
    internal string Group { get; }
    public string Name { get; }
    public string Location { get; }
    public string Value { get; }

    /// <summary>The choice of the soil type for a «tipologia» conflict, null for the other fields.</summary>
    public IReadOnlyList<string>? SoilTypes { get; }

    /// <summary>The soil type chosen, applied by «Uniforma a questo».</summary>
    public string? SoilType { get; set; }

    public string? SoilTypeAutomationName { get; }
    public bool CanAlign { get; }
    public string AlignLabel { get; }
    public string AlignToolTip { get; }
    public IRelayCommand<ConflictSheetViewModel> AlignCommand { get; }
}

/// <summary>A conflict of the comparison: «Conflitto n: campo» and the sheets in it, in the order of the project.</summary>
public sealed record ConflictViewModel(string Title, string Label, IReadOnlyList<ConflictSheetViewModel> Sheets);

/// <summary>A sheet that has a layer missing in the others: its data, the soil type to choose and «Uniforma a questo».</summary>
public sealed class MissingLayerSourceViewModel
{
    internal MissingLayerSourceViewModel(JsonObject sheet, int survey, int layer, bool readOnly, IRelayCommand<MissingLayerSourceViewModel> copy)
    {
        Sheet = sheet; Survey = survey; Layer = layer;
        var row = sheet["dati"]!["stratigrafie"]![survey]![layer]!;
        Name = sheet.S("nome");
        Location = ProjectSharedData.Location(sheet);
        Details = "Tipo: " + (string.IsNullOrWhiteSpace(row.S("tipologia")) ? "Non compilato" : row.S("tipologia")) + " · Spessore: " + row.S("spessore") + " m";
        TypeKey = $"Strato · {survey + 1}/{layer + 1}/tipologia";
        SoilType = row.S("tipologia");
        SoilTypeAutomationName = "Tipologia del terreno · " + sheet.S("nome");
        CanCopy = !readOnly && ProjectSharedData.AncestorReferences(sheet, TypeKey).Length == 0;
        CopyCommand = copy;
    }

    public JsonObject Sheet { get; }
    public int Survey { get; }
    public int Layer { get; }
    public string Name { get; }
    public string Location { get; }
    public string Details { get; }
    internal string TypeKey { get; }
    public IReadOnlyList<string> SoilTypes { get; } = ["Granulare", "Coesivo"];
    public string? SoilType { get; set; }
    public string SoilTypeAutomationName { get; }
    public bool CanCopy { get; }
    public string CopyLabel => "Uniforma a questo";
    public string CopyToolTip => "Aggiunge lo strato e gli eventuali strati precedenti mancanti, copiando i soli dati comuni. Gli strati già presenti restano invariati.";
    public IRelayCommand<MissingLayerSourceViewModel> CopyCommand { get; }
}

/// <summary>«Conflitto n: Sondaggio s · Strato l assente»: the sheets that have the layer and those without it.</summary>
public sealed record MissingLayerViewModel(string Title, IReadOnlyList<MissingLayerSourceViewModel> Sources, IReadOnlyList<string> Targets)
{
    public string Note => "Uniformando vengono aggiunti anche gli eventuali strati precedenti mancanti, nello stesso ordine.";
}

/// <summary>A notice of the comparison, ok (green) or warning (orange).</summary>
public sealed record CoherenceNotice(string Text, bool Warning);

/// <summary>A sheet that can link its stratigraphies (the choices of «Collegamento stratigrafie»).</summary>
public sealed record SoilLinkSheet(JsonObject Sheet)
{
    public override string ToString() => ShellTexts.SheetName(Sheet);
}

/// <summary>
/// The comparison of a section (F5.13 b, proposal 4.3 «coerenza dei dati comuni»; MainWindow.ShowCoherence): conflicts of the common
/// data with the sheets in them, layers missing in some sheets, notices (limitations and cover), the link of the stratigraphies with its
/// preview. «Uniforma a questo», the copy of the layers and the link go through <see cref="ProjectSession.ChangeData"/>: the open sheet
/// is confirmed, the document is modified, the open sheets reconcile, and the comparison is rebuilt.
/// </summary>
public sealed class CoherenceViewModel : ViewModelBase
{
    public const string Heading = "Confronto tra fogli";
    public const string Intro = "Confronta i valori presenti nei fogli e scegli quale mantenere. Le proprietà compatibili saranno uniformate.";
    public const string TheoryTitle = "Come funzionano dati comuni e incongruenze";
    public const string NoConflicts = "Nessun conflitto tra i dati comuni dei fogli.";
    public const string NoticesTitle = "Avvisi";
    public const string NoNotices = "Nessun avviso.";
    public const string LinkTitle = "Collegamento stratigrafie";

    private readonly ProjectSession project;
    private readonly IDialogService dialogs;
    private readonly CommandGuard guard;
    private readonly IRelayCommand<ConflictSheetViewModel> align;
    private readonly IRelayCommand<MissingLayerSourceViewModel> copy;
    private SoilLinkSheet? linkSource, linkTarget;

    public CoherenceViewModel(ProjectSession project, JsonObject section, PresentationServices services)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(services);
        this.project = project; Section = section; dialogs = services.Dialogs;
        guard = new CommandGuard(services.Messages);
        align = new RelayCommand<ConflictSheetViewModel>(item => guard.Run(() => Align(item!)), item => item is { CanAlign: true });
        copy = new RelayCommand<MissingLayerSourceViewModel>(item => guard.Run(() => CopyLayers(item!)), item => item is { CanCopy: true });
        LinkCommand = new AsyncRelayCommand(() => guard.RunAsync(_ => LinkAsync()), () => CanLink);
        Title = "Confronto · " + section.S("nome");
        Theory = CalculationHelp.ProjectTheory;
        Rebuild();
    }

    public JsonObject Section { get; }

    /// <summary>Caption of the dialog, «Confronto · sezione».</summary>
    public string Title { get; }

    /// <summary>Text of «Come funzionano dati comuni e incongruenze».</summary>
    public string Theory { get; }

    public IReadOnlyList<ConflictViewModel> Conflicts { get; private set; } = [];

    public IReadOnlyList<MissingLayerViewModel> MissingLayers { get; private set; } = [];

    /// <summary>«Nessun conflitto tra i dati comuni dei fogli.» shows when true.</summary>
    public bool HasNoConflicts { get; private set; }

    /// <summary>Limitations (warnings) and checks of the cover; «Nessun avviso.» when empty.</summary>
    public IReadOnlyList<CoherenceNotice> Notices { get; private set; } = [];

    /// <summary>The sheets of the section that share the soil: the link shows with two of them, outside read-only.</summary>
    public IReadOnlyList<SoilLinkSheet> LinkSheets { get; private set; } = [];

    public bool CanLink => LinkSheets.Count > 1 && !project.IsReadOnly;

    /// <summary>«Sondaggio di riferimento», the first sheet at the start.</summary>
    public SoilLinkSheet? LinkSource { get => linkSource; set => SetProperty(ref linkSource, value); }

    /// <summary>«Foglio da collegare», the second sheet at the start.</summary>
    public SoilLinkSheet? LinkTarget { get => linkTarget; set => SetProperty(ref linkTarget, value); }

    /// <summary>«Mostra collegamento stratigrafie…»: the preview in a dialog, then the link.</summary>
    public IAsyncRelayCommand LinkCommand { get; }

    /// <summary>The document changed through the comparison (the window reloads its pages; the coordinator its sheets).</summary>
    public event EventHandler? Changed;

    /// <summary>The link was applied: the window closes the comparison, as today.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Rebuilds conflicts, layers and notices from the section (the Render of MainWindow.ShowCoherence).</summary>
    public void Rebuild()
    {
        bool readOnly = project.IsReadOnly;
        var differences = ProjectSharedData.Differences(Section);
        var missing = ProjectSharedData.MissingSoilLayers(Section);
        HasNoConflicts = differences.Count == 0 && missing.Count == 0;
        int number = 0;
        var conflicts = new List<ConflictViewModel>();
        foreach (var conflict in differences.GroupBy(d => d.Key))
        {
            var participants = conflict.SelectMany(d => new[] { d.First, d.Second }).ToHashSet();
            foreach (var participant in participants.ToArray()) participants.UnionWith(ProjectSharedData.AncestorReferences(participant, conflict.Key));
            string label = SharedDataCoordinator.FieldLabel(conflict.Key);
            conflicts.Add(new($"Conflitto {++number}: {label}", label, ProjectSharedData.ContextSheets(Section).Where(participants.Contains)
                .Select(sheet => new ConflictSheetViewModel(sheet, conflict.Key, label, conflict.First().Group, readOnly, align)).ToArray()));
        }
        Conflicts = conflicts;
        MissingLayers = missing.GroupBy(m => (m.Survey, m.Layer)).Select(group => new MissingLayerViewModel(
            $"Conflitto {++number}: Sondaggio {group.Key.Survey + 1} · Strato {group.Key.Layer + 1} assente",
            group.Select(m => m.Source).Distinct().Select(sheet => new MissingLayerSourceViewModel(sheet, group.Key.Survey, group.Key.Layer, readOnly, copy)).ToArray(),
            group.Select(m => m.Target).Distinct().Select(target => target.S("nome") + ": strato assente").ToArray())).ToArray();
        Notices = ProjectSharedData.Limitations(Section).Select(l => new CoherenceNotice("• " + l, true))
            .Concat(ProjectValidation.CoverChecks(Section).Select(c => new CoherenceNotice("• " + c.Text, c.Passed != true))).ToArray();
        LinkSheets = Section.Array("fogli").OfType<JsonObject>().Where(ProjectSharedData.SupportsSharedSoils).Select(s => new SoilLinkSheet(s)).ToArray();
        LinkSource = LinkSheets.ElementAtOrDefault(0);
        LinkTarget = LinkSheets.ElementAtOrDefault(1);
        foreach (string name in new[] { nameof(HasNoConflicts), nameof(Conflicts), nameof(MissingLayers), nameof(Notices), nameof(LinkSheets), nameof(CanLink) }) OnPropertyChanged(name);
        LinkCommand.NotifyCanExecuteChanged();
    }

    /// <summary>«Uniforma a questo» on a conflict (SharedDataCoordinator.Align), with the soil type chosen.</summary>
    public void Align(ConflictSheetViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.CanAlign) return;
        if (project.ChangeData(() => SharedDataCoordinator.Align(item.Sheet, item.Key, item.Group, item.SoilType), item.Sheet)) Changed?.Invoke(this, EventArgs.Empty);
        Rebuild();
    }

    /// <summary>«Uniforma a questo» on a missing layer (SharedDataCoordinator.CopyMissingLayers).</summary>
    public void CopyLayers(MissingLayerSourceViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.CanCopy) return;
        if (project.ChangeData(() => SharedDataCoordinator.CopyMissingLayers(item.Sheet, item.TypeKey, item.Survey, item.Layer, item.SoilType), item.Sheet))
            Changed?.Invoke(this, EventArgs.Empty);
        Rebuild();
    }

    /// <summary>The preview of the link of the stratigraphies (<see cref="SoilLink"/>); on confirmation the two sheets are linked and the comparison closes.</summary>
    public async Task LinkAsync()
    {
        if (!CanLink || LinkSource is not { } from || LinkTarget is not { } to || ReferenceEquals(from.Sheet, to.Sheet)) return;
        var link = SoilLink.Stage(from.Sheet, to.Sheet);
        if (await dialogs.ShowDialogAsync(new SoilLinkDialogViewModel(from + " → " + to, link)) != true) return;
        if (project.ChangeData(() => { link.Apply(); return true; }, to.Sheet)) Changed?.Invoke(this, EventArgs.Empty);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// «Collegamento dei sondaggi» (MainWindow.ShowCoherence): the sheets, the explanation and the stratigraphies of the linked sheet; «Conferma
/// stesso sondaggio e collega» or «Annulla».
/// </summary>
public sealed class SoilLinkDialogViewModel : DialogViewModel<bool>
{
    public const string Text = "Conferma che i sondaggi corrispondano, nello stesso ordine e con la stessa origine delle quote. Saranno collegati gli strati e copiati i parametri fisici indicati sotto. I parametri specifici del metodo restano nel destinatario.";
    public const string ConfirmLabel = "Conferma stesso sondaggio e collega";

    public SoilLinkDialogViewModel(string heading, SoilLink link) : base("Collegamento dei sondaggi")
    {
        ArgumentNullException.ThrowIfNull(link);
        Heading = heading;
        Preview = link.StagedTarget["dati"]!["stratigrafie"]!.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        ConfirmCommand = new RelayCommand(() => Close(true));
        CancelCommand = new RelayCommand(Cancel);
    }

    public string Heading { get; }
    public string Preview { get; }
    public IRelayCommand ConfirmCommand { get; }
    public IRelayCommand CancelCommand { get; }
}

/// <summary>A row of the common data of the overview: label and the values of the sheets, joined by « / ».</summary>
public sealed record CommonDataRow(string Key, string Label, string Value);

/// <summary>
/// The overview of a section (F5.13 b; MainWindow.BuildSectionOverview and AddModelsOverview): controls (conflicts and notices),
/// common data with the priority of today (five, then «Tutti i dati comuni»), revisions, actions, sheets and subsections, details of the
/// notices and models; or the empty overview when the archive has no project.
/// </summary>
public sealed class SectionOverviewViewModel : ViewModelBase
{
    public const string EmptyCaption = "INFORMAZIONI";
    public const string EmptyTitle = "Tutto sotto controllo";
    public const string Caption = "SEZIONE SELEZIONATA";
    /// <summary>Common data shown before «Tutti i dati comuni».</summary>
    public const int FirstRows = 5;

    private static readonly IReadOnlyList<string> Priority = ["CLS · fck [MPa]", "classe_acciaio", "esposizione", "diameter_mm", "cover_mm", "width_mm", "height_mm", "fyk_mpa"];

    public SectionOverviewViewModel(ProjectSession project, JsonObject? section)
    {
        ArgumentNullException.ThrowIfNull(project);
        Section = section;
        if (section is null) return;
        Name = section.S("nome");
        int conflicts = ProjectSharedData.Differences(section).Select(d => d.Key).Distinct().Count() + ProjectSharedData.MissingSoilLayers(section).Count;
        Notices = ProjectValidation.Warnings(section);
        ConflictsText = $"{conflicts} {(conflicts == 1 ? "conflitto" : "conflitti")}";
        HasConflicts = conflicts > 0;
        NoticesText = $"{Notices.Count} {(Notices.Count == 1 ? "avviso" : "avvisi")}";
        var fields = ProjectSharedData.ContextSheets(section).SelectMany(s => ProjectSharedData.Fields(s).Values)
            .Where(f => f.Group is "Materiali" or "Geometria" or "Armatura" or "Normativa" or "Coefficienti").GroupBy(f => f.Key)
            .Select(g => new CommonDataRow(g.Key, ProjectReportPlan.Label(g.Key), string.Join(" / ", g.Select(f => ProjectSharedData.Text(f.Value)).Distinct()))).ToArray();
        CommonData = fields.OrderBy(f => PriorityOf(f.Key)).ToArray();
        var scope = project.RevisionScope(section) ?? section;
        var revision = project.DisplayedRevision is int number
            ? scope.Array("revisioni").OfType<JsonObject>().FirstOrDefault(r => ShellTexts.RevisionNumber(r) == number)
            : scope["revisione"] as JsonObject;
        RevisionScope = scope;
        RevisionTitle = revision is null ? "Versione corrente" : $"Revisione {ShellTexts.RevisionNumber(revision):00}";
        RevisionDate = revision is null ? null : ShellTexts.RevisionDate(revision);
        RevisionNote = revision is null || revision.S("nota").Length == 0 ? null : revision.S("nota");
        HasRevisionSummary = scope.Array("revisioni").Count > 0;
        CanDuplicate = !project.IsReadOnly;
        CanReport = ProjectSharedData.SubtreeSheets(section).Any();
        Links = section.Array("fogli").Concat(section.Array("strutture")).OfType<JsonObject>().ToArray();
        var models = ProjectModelStore.Models(section).ToArray();
        ModelsText = $"{models.Length} modelli in questa sezione. I fogli possono riferirsi a una revisione del modello.";
        Models = models.Select(m => (m.S("id"), "▧  " + m.S("nome"))).ToArray();
        CanImportModel = !project.IsReadOnly;
    }

    /// <summary>Position of a common field in the order of today: the priority fields first, then the others in their order.</summary>
    private static int PriorityOf(string key)
    {
        for (int i = 0; i < Priority.Count; i++) if (Priority[i] == key) return i;
        return Priority.Count;
    }

    /// <summary>The section; null for the empty overview («Tutto sotto controllo»).</summary>
    public JsonObject? Section { get; }

    public string Name { get; } = "";
    public string ConflictsText { get; } = "";
    public bool HasConflicts { get; }
    public IReadOnlyList<string> Notices { get; } = [];
    public string NoticesText { get; } = "";
    public bool HasNotices => Notices.Count > 0;
    public string CompareLabel => "Confronto e avvisi";

    /// <summary>All the common data in the order of today; the view shows the first <see cref="FirstRows"/>, the rest in a group.</summary>
    public IReadOnlyList<CommonDataRow> CommonData { get; } = [];

    public string CommonDataEmpty => "Aggiungi le schede per visualizzare materiali, geometria e armature.";

    /// <summary>The section of the revisions of the card «Revisioni».</summary>
    public JsonObject? RevisionScope { get; }

    public string RevisionTitle { get; } = "";
    public string? RevisionDate { get; }
    public string? RevisionNote { get; }

    /// <summary>«Riepilogo modifiche» shows when the section has archived revisions.</summary>
    public bool HasRevisionSummary { get; }

    public bool CanDuplicate { get; }
    public bool CanReport { get; }

    /// <summary>The sheets and the subsections of the group «Fogli e sottosezioni», in this order.</summary>
    public IReadOnlyList<JsonObject> Links { get; } = [];

    public string ModelsText { get; } = "";
    public IReadOnlyList<(string Id, string Label)> Models { get; } = [];
    public bool CanImportModel { get; }

    /// <summary>The texts of the empty overview.</summary>
    public IReadOnlyList<(string Title, string Text)> EmptyCards { get; } =
    [
        ("La sezione selezionata", "Qui trovi i dati comuni, i controlli e le revisioni della sezione su cui stai lavorando."),
        ("Come iniziare", "1. Crea il progetto\n\n2. Organizza le sezioni\n\n3. Aggiungi le schede")
    ];
}
