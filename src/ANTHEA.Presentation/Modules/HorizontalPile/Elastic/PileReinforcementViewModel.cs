using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Calculations;
using Anthea.Application.Presentation;
using Anthea.Presentation.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using X.Core;

namespace Anthea.Presentation.Modules.HorizontalPile.Elastic;

/// <summary>A command of the editor of the reinforcement: its label and whether the user can use it now.</summary>
public sealed record ElasticCommand(string Label, bool IsEnabled);

/// <summary>A reinforcement field of a segment: key, title, text (typing writes it), read-only and tool tip.</summary>
public sealed class ElasticSegmentFieldViewModel : ObservableObject
{
    private readonly ElasticSegmentViewModel segment;
    private string text;

    internal ElasticSegmentFieldViewModel(ElasticSegmentViewModel segment, string key, string title, string text, bool linked, string toolTip)
    {
        this.segment = segment; Key = key; Title = title; this.text = text; Linked = linked; ToolTip = toolTip;
    }

    public string Key { get; }

    public string Title { get; }

    /// <summary>The value of the main section (linked segment): shown, never written.</summary>
    public bool Linked { get; }

    public bool IsReadOnly => Linked || !segment.CanEdit;

    public string ToolTip { get; }

    /// <summary>The text: typing writes the raw text into the segment (refused when read-only or linked).</summary>
    public string Text
    {
        get => text;
        set
        {
            value ??= "";
            if (value == text || IsReadOnly) { OnPropertyChanged(); return; }
            text = value;
            OnPropertyChanged();
            segment.FieldTyped(Key, value);
        }
    }

    internal void Refresh() => OnPropertyChanged(nameof(IsReadOnly));
}

/// <summary>
/// The card of a segment of the reinforcement (historic PileReinforcementEditor): the heading, the end, «Usa principale», the fields of the
/// section and of the second ring, the transverse kind, the commands and the bill. Built from the data when the editor is built; the texts
/// typed write into the segment at once, as the historic text boxes.
/// </summary>
public sealed class ElasticSegmentViewModel : ObservableObject
{
    private readonly PileReinforcementViewModel owner;
    private readonly JsonObject row, adopted;
    private string end, kind, bill;
    private bool billExpanded;

    internal ElasticSegmentViewModel(PileReinforcementViewModel owner, JsonObject root, JsonObject row, int index, string header, string bill, bool billExpanded)
    {
        this.owner = owner; this.row = row; Index = index; Header = header; this.bill = bill; this.billExpanded = billExpanded;
        Id = row.S("id");
        end = row.S("fine");
        Linked = row.B("collegato");
        adopted = ElasticPileEditor.SegmentSection(root, row);
        var fields = new List<ElasticSegmentFieldViewModel>();
        foreach (var (key, title) in ElasticPileFields.SegmentFields.Take(4))
            fields.Add(new(this, key, title, adopted.S(key), Linked, ElasticPilePresentation.SegmentFieldToolTip(row, key, title)));
        Ring = adopted.B("second_inner_enabled");
        RingToolTip = Linked ? ElasticPilePresentation.SharedValue : ElasticPilePresentation.InnerRingToolTip;
        if (Ring)
            foreach (var (key, title) in ElasticPileFields.SegmentFields.Skip(4))
                fields.Add(new(this, key, title, adopted.S(key), Linked, Linked ? ElasticPilePresentation.SharedValue : title));
        Fields = fields;
        kind = ElasticPileEditor.TransverseKind(root, row);
    }

    public int Index { get; }

    public string Id { get; }

    /// <summary>The heading (identifier, start → end, length) of the data when the editor was built.</summary>
    public string Header { get; }

    internal bool CanEdit => owner.CanEdit;

    /// <summary>«Fine tratto / barre [m]»: typing writes the end (empty: the tip).</summary>
    public string End
    {
        get => end;
        set
        {
            value ??= "";
            if (value == end || !CanEdit) { OnPropertyChanged(); return; }
            end = value;
            OnPropertyChanged();
            row["fine"] = string.IsNullOrWhiteSpace(value) ? null : JsonValue.Create(value);
            owner.Changed();
        }
    }

    public string EndToolTip => ElasticPilePresentation.SegmentEndToolTip;

    /// <summary>«Usa principale».</summary>
    public bool Linked { get; }

    /// <summary>«Usa principale» can be clicked: not on the first segment.</summary>
    public bool CanChangeLinked => Index > 0 && CanEdit;

    /// <summary>The click on «Usa principale»: a segment unlinked takes the missing values of the main section; the editor is built again.</summary>
    public void ToggleLinked()
    {
        if (!CanChangeLinked) return;
        owner.ToggleLinked(row, !Linked);
    }

    public IReadOnlyList<ElasticSegmentFieldViewModel> Fields { get; }

    /// <summary>«Secondo anello interno».</summary>
    public bool Ring { get; }

    public bool CanChangeRing => !Linked && CanEdit;

    public string RingToolTip { get; }

    /// <summary>The click on «Secondo anello interno»: activated, it starts from the bars of the first ring; the editor is built again.</summary>
    public void ToggleRing()
    {
        if (!CanChangeRing) return;
        owner.ToggleRing(row, !Ring, adopted);
    }

    public IReadOnlyList<string> Kinds => ElasticPilePresentation.TransverseKinds;

    /// <summary>«Tipo trasversale»: choosing writes it into the segment.</summary>
    public string Kind
    {
        get => kind;
        set
        {
            if (value == kind || !CanChangeKind || value is null || !Kinds.Contains(value)) { OnPropertyChanged(); return; }
            kind = value;
            OnPropertyChanged();
            row["tipo_trasversale"] = value;
            owner.Changed();
        }
    }

    public bool CanChangeKind => !Linked && CanEdit;

    /// <summary>The commands of the card, in their order.</summary>
    public IReadOnlyList<ElasticCommand> Commands
    {
        get
        {
            bool edit = CanEdit;
            var list = new List<ElasticCommand> { new("Verifiche →", edit), new("Apri verificatore c.a.", edit), new("Dimensiona…", edit && owner.ResultCommandsEnabled), new("Dividi…", edit) };
            if (Index > 0) list.Add(new("Unisci ←", edit));
            list.Add(new("Distinta ferri", edit));
            return list;
        }
    }

    /// <summary>The bill of the segment.</summary>
    public string Bill { get => bill; internal set => SetProperty(ref bill, value); }

    /// <summary>«Distinta ferri · barre e staffe» open (kept for the segment while the editor is built again).</summary>
    public bool BillExpanded
    {
        get => billExpanded;
        set { if (SetProperty(ref billExpanded, value)) owner.BillToggled(Id, value); }
    }

    internal void FieldTyped(string key, string value)
    {
        row[key] = value;
        owner.Changed();
    }

    internal void RefreshEditable()
    {
        foreach (var field in Fields) field.Refresh();
        foreach (string name in new[] { nameof(CanChangeLinked), nameof(CanChangeRing), nameof(CanChangeKind), nameof(Commands) }) OnPropertyChanged(name);
    }
}

/// <summary>
/// The c.a. verifier of a segment (historic: a dialog with a ConcreteWorkspace on the sheet of the segment, until F5.10). The sheet is
/// requested through <see cref="Modules.ISheetHost.OpenModuleCopy"/>; the host that shows it as the historic dialog reports
/// «Applica armatura…» (<see cref="Apply"/>) and the closing (<see cref="Close"/>), which write into the pile as the historic dialog.
/// </summary>
public sealed class PileConcreteVerifier
{
    private readonly PileReinforcementViewModel owner;
    private readonly JsonObject root, row;
    private bool closed;

    internal PileConcreteVerifier(PileReinforcementViewModel owner, JsonObject root, JsonObject row, int index, JsonObject sheet, string title, string note)
    {
        this.owner = owner; this.root = root; this.row = row; Index = index; Sheet = sheet; Title = title; Note = note;
    }

    /// <summary>The c.a. module of the verifier.</summary>
    public const string ConcreteModule = "str_palo";

    public int Index { get; }

    /// <summary>The sheet of the segment (data of <see cref="ConcreteModule"/>): the host edits this object.</summary>
    public JsonObject Sheet { get; }

    public string Title { get; }

    /// <summary>The note above the command: what the verifier does, then the outcome of «Applica».</summary>
    public string Note { get; private set; }

    public string ApplyLabel => Index == 0 ? "Applica armatura alla sezione principale" : "Applica armatura al tratto";

    /// <summary>«Applica armatura…»: the reinforcement of the sheet into the pile (the main section for the first segment).</summary>
    public void Apply()
    {
        if (closed) return;
        try
        {
            ElasticPileEditor.ApplyConcreteSheetReinforcement(root, Sheet, Index);
            if (Index == 0) owner.SharedSectionChanged();
            owner.Changed();
            Note = ElasticPileFields.ConcreteApplied;
        }
        catch (ArgumentException error) { Note = error.Message; }
    }

    /// <summary>The closing: the sheet is kept in the segment, then the editor is built again.</summary>
    public void Close()
    {
        if (closed) return;
        closed = true;
        owner.ConcreteClosed(row, Sheet);
    }
}

/// <summary>
/// «Dimensionamento del tratto · proposta da applicare» (historic: CreateSegmentDesignWindow): the catalogues, the search on the existing
/// PileSegmentDesignSession (latest request wins, a proposal of other data is refused), the proposal and «Applica».
/// </summary>
public sealed class PileSegmentDesignViewModel : ObservableObject
{
    private readonly PileReinforcementViewModel owner;
    private readonly JsonObject root;
    private readonly PileSegmentDesignSession session;
    private JsonObject? proposal;
    private bool closed, canRun = true, canApply, progressVisible, progressIndeterminate;
    private double progressValue;
    private int generation;
    private string status = ElasticPileFields.DesignIntro;

    internal PileSegmentDesignViewModel(PileReinforcementViewModel owner, JsonObject root, JsonObject result, int index)
    {
        this.owner = owner; this.root = root; Index = index;
        session = new PileSegmentDesignSession(root, result, index);
    }

    public int Index { get; }

    public string ApplyLabel => Index == 0 ? "Applica alla sezione principale" : "Applica al tratto";

    public string Counts { get; set; } = "6;8;10;12;14;16;20;24";

    public string Diameters { get; set; } = "16;20;24;28;32";

    public string Links { get; set; } = "8;10;12";

    public string Spacings { get; set; } = "100;150;200";

    public string Status { get => status; private set => SetProperty(ref status, value); }

    public bool CanRun { get => canRun; private set => SetProperty(ref canRun, value); }

    public bool CanApply { get => canApply; private set => SetProperty(ref canApply, value); }

    public bool ProgressVisible { get => progressVisible; private set => SetProperty(ref progressVisible, value); }

    public bool ProgressIndeterminate { get => progressIndeterminate; private set => SetProperty(ref progressIndeterminate, value); }

    public double ProgressValue { get => progressValue; private set => SetProperty(ref progressValue, value); }

    /// <summary>The last search started.</summary>
    public Task Search { get; private set; } = Task.CompletedTask;

    /// <summary>«Trova proposta».</summary>
    public Task FindAsync() => Search = RunAsync();

    private async Task RunAsync()
    {
        if (closed || !CanRun) return;
        CanRun = false; CanApply = false; proposal = null; int requested = ++generation;
        try
        {
            static double[] Values(string text) => text.Split(';').Select(s => J.Number(JsonValue.Create(s)) ?? throw new ArgumentException("Catalogo non numerico.")).ToArray();
            var ns = Counts.Split(';').Select(int.Parse).ToArray(); var ss = Values(Spacings); var ds = Values(Diameters); var ts = Values(Links);
            Status = ElasticPileFields.DesignSearching; ProgressVisible = true; ProgressIndeterminate = true;
            var updates = new Progress<PileProgress>(p =>
            {
                if (closed || CanRun || requested != generation) return;
                Status = p.Phase; ProgressIndeterminate = p.Total == 0;
                ProgressValue = p.Total > 0 ? 100d * p.Completed / p.Total : 0;
            });
            var calculation = await session.CalculateAsync(ns, ds, ts, ss, updates);
            calculation.TryPublish(value =>
            {
                if (closed) return;
                if (!session.Matches(root)) { Status = PileReinforcementOperations.RevisionChangedMessage; return; }
                if (value.Error is not null) { Status = value.Error.Message; return; }
                proposal = value.Value; Status = proposal!.ToJsonString(J.Options); CanApply = true;
            });
        }
        catch (Exception error) { if (!closed) Status = error.Message; }
        finally { if (!closed) { CanRun = true; ProgressVisible = false; } }
    }

    /// <summary>«Applica…»: the proposal into the segment (the main section for the first one); the editor is built again.</summary>
    public void Apply()
    {
        if (closed || proposal is null) return;
        try
        {
            session.Apply(root, proposal);
            if (Index == 0) owner.SharedSectionChanged();
            owner.Changed(); owner.Refresh();
        }
        catch (ArgumentException error) { Status = error.Message; }
    }

    /// <summary>The dialog is closed: a late search is never shown.</summary>
    public void Close()
    {
        if (closed) return;
        closed = true;
        session.Dispose();
    }
}

/// <summary>
/// The editor of the reinforcement of the pile (historic PileReinforcementEditor) without WPF, on the same data and with the same writes, in
/// the same order: the segments with their cards, the main transverse reinforcement, the three groups of assumptions, the pile minima, the
/// approvals, the proposal of the segments, the split and the merge, the c.a. verifier and the design of a segment, the bills and the panel
/// «Sezione e verifiche». Every change goes to the elastic response (InvalidateShared); the cards are built again where the historic editor
/// builds them again (a result, a click on a check box of a card, a command).
/// </summary>
public sealed class PileReinforcementViewModel : ObservableObject, IDisposable
{
    private readonly JsonObject root;
    private readonly ElasticPileViewModel elastic;
    private readonly ElasticPileSheet sheet;
    private readonly Dictionary<string, bool> expandedBills = new(StringComparer.Ordinal);
    private string calculationState = ElasticPilePresentation.ReinforcementInitial, phase = "", message = "", checkMode = "Riepilogo", mainTransverse = "";
    private bool progressVisible, progressIndeterminate, resultCommandsEnabled, isBuilt, seismicExpanded, detailsExpanded, extraExpanded, minimum;
    private double progressValue;
    private int selected;
    private IReadOnlyList<ElasticSegmentViewModel> segments = [];
    private FormViewModel? details, seismic, extra;
    private ElasticPileSummary? summary;
    private bool disposed;

    internal PileReinforcementViewModel(JsonObject root, ElasticPileViewModel elastic, ElasticPileSheet sheet)
    {
        this.root = root; this.elastic = elastic; this.sheet = sheet;
        Build();
    }

    internal bool CanEdit => elastic.CanEdit;

    /// <summary>The editor shows its segments (nothing while the comparison with an old archive is pending).</summary>
    public bool IsBuilt { get => isBuilt; private set => SetProperty(ref isBuilt, value); }

    /// <summary>The state of the calculation above the segments.</summary>
    public string Phase { get => phase; private set => SetProperty(ref phase, value); }

    public bool ProgressVisible { get => progressVisible; private set => SetProperty(ref progressVisible, value); }

    public bool ProgressIndeterminate { get => progressIndeterminate; private set => SetProperty(ref progressIndeterminate, value); }

    public double ProgressValue { get => progressValue; private set => SetProperty(ref progressValue, value); }

    /// <summary>The message under the groups: the error of a command.</summary>
    public string Message { get => message; private set => SetProperty(ref message, value); }

    /// <summary>The commands that need the result («Proponi suddivisione…», «Dimensiona…»).</summary>
    public bool ResultCommandsEnabled { get => resultCommandsEnabled; private set => SetProperty(ref resultCommandsEnabled, value); }

    /// <summary>The commands above the segments, in their order.</summary>
    public IReadOnlyList<ElasticCommand> Commands => [new("Approva tutto · ipotesi", CanEdit), new("NTC §7.2.5", CanEdit), new("Proponi suddivisione…", CanEdit && ResultCommandsEnabled), new("Info sezioni", CanEdit)];

    public IReadOnlyList<string> TransverseKinds => ElasticPilePresentation.TransverseKinds;

    /// <summary>«Armatura trasversale principale»: choosing writes it and builds the editor again.</summary>
    public string MainTransverse
    {
        get => mainTransverse;
        set
        {
            if (value == mainTransverse || !CanEdit || value is null || !TransverseKinds.Contains(value)) { OnPropertyChanged(); return; }
            mainTransverse = value;
            OnPropertyChanged();
            root["elastico"]!["dettagli"]!["sisma_staffe"] = value;
            Changed(); Build();
        }
    }

    public IReadOnlyList<ElasticSegmentViewModel> Segments { get => segments; private set => SetProperty(ref segments, value); }

    /// <summary>«Ipotesi di verifica e vincoli per la proposta».</summary>
    public FormViewModel? Details { get => details; private set => SetProperty(ref details, value); }

    public bool DetailsExpanded { get => detailsExpanded; set => SetProperty(ref detailsExpanded, value); }

    /// <summary>«Sisma · testa del palo · NTC §7.2.5».</summary>
    public FormViewModel? Seismic { get => seismic; private set => SetProperty(ref seismic, value); }

    /// <summary>Open when the editor is built with the seismic head checked.</summary>
    public bool SeismicExpanded { get => seismicExpanded; set => SetProperty(ref seismicExpanded, value); }

    /// <summary>The commands of the seismic group.</summary>
    public IReadOnlyList<ElasticCommand> SeismicCommands => [new("Approva tutto · sisma", CanEdit && root["elastico"]!["dettagli"].B("sisma_testa")), new("NTC §7.2.5 · finestra interna", CanEdit)];

    /// <summary>«Sovrapposizioni, interferro e copriferro».</summary>
    public FormViewModel? Extra { get => extra; private set => SetProperty(ref extra, value); }

    public bool ExtraExpanded { get => extraExpanded; set => SetProperty(ref extraExpanded, value); }

    /// <summary>The text above the group of laps and covers (with the exposure of the main section).</summary>
    public string ExtraNote => ElasticPileFields.ExtraNote(root["sezione"].S("esposizione"));

    /// <summary>«Controlla minimi pali NTC §7.2.5…»: the click writes it.</summary>
    public bool Minimum
    {
        get => minimum;
        set
        {
            if (value == minimum || !CanEdit) { OnPropertyChanged(); return; }
            minimum = value;
            OnPropertyChanged();
            root["elastico"]!["dettagli"]!["minimi_pali"] = value;
            Changed();
        }
    }

    // ---------------------------------------------------------------- summary

    /// <summary>The panel «Sezione e verifiche».</summary>
    public ElasticPileSummary? Summary { get => summary; private set => SetProperty(ref summary, value); }

    /// <summary>The choices of the panel can be used (the historic view protects them in a read-only revision).</summary>
    public bool SummaryEnabled => CanEdit;

    /// <summary>The segment chosen in the panel.</summary>
    public void ChooseSegment(int index)
    {
        if (!SummaryEnabled || Summary is null) return;
        selected = index; UpdateSummary();
    }

    /// <summary>The view chosen in the panel.</summary>
    public void ChooseMode(string? mode)
    {
        if (!SummaryEnabled || Summary is null || mode is not null && !ElasticPilePresentation.SummaryModes.Contains(mode)) return;
        checkMode = mode ?? "N–M"; UpdateSummary();
    }

    // ---------------------------------------------------------------- commands

    /// <summary>«Verifiche →» of a segment.</summary>
    public void Verify(int index)
    {
        if (!CanEdit) return;
        selected = index; UpdateSummary();
    }

    /// <summary>«Distinta ferri» of a segment: its bill open and shown in the panel.</summary>
    public void ShowBill(int index)
    {
        if (!CanEdit || index < 0 || index >= Segments.Count) return;
        selected = index; checkMode = "Distinta ferri"; Segments[index].BillExpanded = true; UpdateResults();
    }

    /// <summary>«Unisci ←»: the segment joins the previous one; the editor is built again.</summary>
    public void Merge(int index)
    {
        var rows = root["elastico"]!.Array("tratti");
        if (!CanEdit || index <= 0 || index >= rows.Count) return;
        rows[index - 1]!["fine"] = rows[index]!["fine"]?.DeepClone(); rows.RemoveAt(index); Changed(); Build();
    }

    /// <summary>«Approva tutto · ipotesi» or «Approva tutto · sisma».</summary>
    public void Approve(bool seismicAssumptions)
    {
        if (seismicAssumptions ? !SeismicCommands[0].IsEnabled : !CanEdit) return;
        Commit(); ElasticPileEditor.ApproveReinforcementAssumptions(root, seismicAssumptions); Changed(); Build();
    }

    /// <summary>«Dividi» of the dialog of a segment: the cut at the text typed; null when done, otherwise the error (also the message).</summary>
    public string? Split(int index, string text)
    {
        if (!CanEdit) return null;
        try
        {
            var cut = J.Number(JsonValue.Create(text)) ?? throw new ArgumentException("Quota numerica richiesta.");
            root["elastico"]!["tratti"] = PileReinforcementOperations.Split(root, index, cut); Changed(); Build();
            return null;
        }
        catch (ArgumentException error) { Message = error.Message; return error.Message; }
    }

    /// <summary>«Proponi suddivisione…»: the proposal for the dialog, or null (the message says why).</summary>
    public PileSegmentProposal? Propose()
    {
        if (!CanEdit || !ResultCommandsEnabled) return null;
        if (elastic.Result is not JsonObject r) { Message = calculationState; return null; }
        try { return PileReinforcementOperations.Propose(root, r); }
        catch (ArgumentException error) { Message = error.Message; return null; }
    }

    /// <summary>The text of a proposal in its dialog.</summary>
    public static string ProposalText(PileSegmentProposal proposal) => proposal.Segments.ToJsonString(J.Options);

    /// <summary>«Applica e archivia i tratti precedenti»; false when the data changed (the message and a notice say it).</summary>
    public bool ApplyProposal(PileSegmentProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        try { PileReinforcementOperations.ApplyProposal(root, proposal); Changed(); Build(); return true; }
        catch (ArgumentException error) { Message = error.Message; sheet.Services.Messages.Inform("Proposta da aggiornare", error.Message); return false; }
    }

    /// <summary>
    /// «Apri verificatore c.a.» of a segment: the sheet of the segment, requested through the host (<see cref="Modules.ISheetHost.OpenModuleCopy"/>);
    /// null when the segment cannot have one (a CHS), with the message.
    /// </summary>
    public PileConcreteVerifier? OpenConcreteVerifier(int index)
    {
        if (!CanEdit) return null;
        try
        {
            Commit();
            var sheetData = ElasticPileEditor.CreateConcreteSheet(root, elastic.Result, index);
            var row = root["elastico"]!.Array("tratti")[index]!.AsObject();
            string note = ElasticPileFields.ConcreteNote + (ElasticPileEditor.TransverseKind(root, row) == "Spirale" ? ElasticPileFields.ConcreteSpiralNote : "");
            var verifier = new PileConcreteVerifier(this, root, row, index, sheetData, $"Verificatore c.a. · {row.S("id")}", note);
            sheet.Host.OpenModuleCopy(PileConcreteVerifier.ConcreteModule, sheetData, verifier.Title);
            return verifier;
        }
        catch (ArgumentException error) { Message = error.Message; return null; }
    }

    /// <summary>«Dimensiona…» of a segment: the dialog, or null without result (the message says why).</summary>
    public PileSegmentDesignViewModel? Design(int index)
    {
        if (!CanEdit || !ResultCommandsEnabled) return null;
        if (elastic.Result is not JsonObject r) { Message = calculationState; return null; }
        return new PileSegmentDesignViewModel(this, root, r, index);
    }

    // ---------------------------------------------------------------- calls of the elastic response

    internal void SetCalculationState(string text, bool running, int completed = 0, int total = 0)
    {
        calculationState = text; Phase = text; ProgressVisible = running; ProgressIndeterminate = running && total == 0; ProgressValue = total > 0 ? 100d * completed / total : 0;
        ResultCommandsEnabled = elastic.Result != null; RaiseCommands();
        if (!running && elastic.Result == null) UpdateResults();
    }

    internal void Commit()
    {
        Details?.Commit(); Seismic?.Commit(); Extra?.Commit();
    }

    /// <summary>The editor built again (a result).</summary>
    internal void Refresh() => Build();

    /// <summary>The bills while the result is being updated.</summary>
    internal void InvalidateResults()
    {
        foreach (var segment in Segments) segment.Bill = ElasticPilePresentation.BillPending;
        UpdateSummary();
    }

    /// <summary>The bills and the panel of the current result, without building the editor again.</summary>
    internal void UpdateResults()
    {
        foreach (var segment in Segments) segment.Bill = ElasticPilePresentation.BillFor(elastic.Result, calculationState, segment.Id);
        UpdateSummary();
    }

    internal void Changed() => elastic.InvalidateShared();

    internal void SharedSectionChanged() => sheet.Migrated();

    internal void BillToggled(string id, bool open) => expandedBills[id] = open;

    internal void ToggleLinked(JsonObject row, bool linked)
    {
        if (!linked)
        {
            foreach (string key in ElasticPileEditor.ReinforcementKeys) if (row[key] == null) row[key] = root["sezione"]![key]?.DeepClone();
            foreach (string key in ElasticPileEditor.InnerRingKeys) if (row[key] == null && root["sezione"]![key] is JsonNode ring) row[key] = ring.DeepClone();
        }
        row["collegato"] = linked; Changed(); Build();
    }

    internal void ToggleRing(JsonObject row, bool ring, JsonObject adopted)
    {
        row["second_inner_enabled"] = ring;
        if (ring)
            foreach (var (key, value) in new[] { ("second_inner_count", adopted.S("longitudinal_bar_count")), ("second_inner_diameter", adopted.S("longitudinal_bar_diameter_mm")), ("second_inner_gap", "30") })
                if (row[key] == null && string.IsNullOrWhiteSpace(adopted.S(key))) row[key] = value;
        Changed(); Build();
    }

    internal void ConcreteClosed(JsonObject row, JsonObject sheetData)
    {
        row["foglio_cls"] = sheetData.DeepClone(); Changed(); Build();
    }

    internal void RefreshEditable()
    {
        foreach (var form in new[] { Details, Seismic, Extra }) if (form is not null) form.IsReadOnly = !CanEdit;
        foreach (var segment in Segments) segment.RefreshEditable();
        foreach (string name in new[] { nameof(SummaryEnabled), nameof(Minimum), nameof(MainTransverse) }) OnPropertyChanged(name);
        RaiseCommands();
    }

    public void Dispose() => disposed = true;

    // ---------------------------------------------------------------- build

    private void RaiseCommands()
    {
        OnPropertyChanged(nameof(Commands)); OnPropertyChanged(nameof(SeismicCommands));
        foreach (var segment in Segments) segment.RefreshEditable();
    }

    /// <summary>Build of the historic editor: nothing while the comparison with an old archive is pending; otherwise the cards from the data.</summary>
    private void Build()
    {
        if (disposed || ElasticPileEditor.NeedsMigration(root)) return;
        ElasticPileEditor.PrepareReinforcement(root);
        var e = root["elastico"]!.AsObject(); var rows = e.Array("tratti"); selected = Math.Clamp(selected, 0, Math.Max(0, rows.Count - 1));
        Phase = calculationState;
        ResultCommandsEnabled = elastic.Result != null;
        mainTransverse = e["dettagli"].S("sisma_staffe"); OnPropertyChanged(nameof(MainTransverse));
        var headers = ElasticPilePresentation.SegmentHeaders(root);
        var list = new List<ElasticSegmentViewModel>();
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i]!.AsObject(); string id = row.S("id");
            list.Add(new ElasticSegmentViewModel(this, root, row, i, headers[i], ElasticPilePresentation.BillFor(elastic.Result, calculationState, id),
                !expandedBills.TryGetValue(id, out bool open) || open));
        }
        Segments = list;
        var settings = e["dettagli"]!.AsObject();
        // The groups are built again closed (the seismic one open while the seismic head is checked), as the historic expanders.
        DetailsExpanded = false; ExtraExpanded = false;
        Details = new FormViewModel(settings, ElasticPileFields.Details(), _ => Changed(), compact: true) { InheritedWikiModule = sheet.ModuleId, IsReadOnly = !CanEdit };
        Seismic = new FormViewModel(settings, ElasticPileFields.Seismic(), _ => { SeismicVisibility(); Changed(); }, compact: true) { InheritedWikiModule = sheet.ModuleId, IsReadOnly = !CanEdit };
        SeismicVisibility();
        SeismicExpanded = settings.B("sisma_testa");
        Extra = new FormViewModel(settings, ElasticPileFields.Extra(), key => { Changed(); if (key == "copriferro_override") Extra!.ShowField("cmin_dur", root["elastico"]!["dettagli"].B("copriferro_override")); }, compact: true) { InheritedWikiModule = sheet.ModuleId, IsReadOnly = !CanEdit };
        Extra.ShowField("cmin_dur", settings.B("copriferro_override"));
        OnPropertyChanged(nameof(ExtraNote));
        UpdateSummary();
        minimum = settings.B("minimi_pali"); OnPropertyChanged(nameof(Minimum));
        IsBuilt = true;
        RaiseCommands();
    }

    private void SeismicVisibility()
    {
        bool active = root["elastico"]!["dettagli"].B("sisma_testa");
        if (Seismic is { } form) foreach (string key in ElasticPileFields.SeismicConditional) form.ShowField(key, active);
        OnPropertyChanged(nameof(SeismicCommands));
    }

    private void UpdateSummary()
    {
        if (ElasticPilePresentation.Summary(root, elastic.Result, selected, checkMode, calculationState) is not { } shown) return;
        selected = shown.Selected;
        Summary = shown;
    }
}
