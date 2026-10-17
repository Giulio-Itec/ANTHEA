using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Anthea.Application.Modules;

namespace Anthea.Presentation.Tests;

/// <summary>
/// One step of a scenario of the tab of the elastic response of the horizontal pile and micropile («Risposta elastica · trave su
/// molle») with the reinforcement of the pile (F5.8), applied in the same way to the historic view (ElasticPileWorkspace and
/// PileReinforcementEditor inside HorizontalWorkspace, tests/Desktop/ElasticPileHistoricTrace.cs) and to its ViewModel
/// (tests/ANTHEA.Presentation.Tests/ElasticPileSheetChecks.cs). A step is what the user does with a control: a control that the user
/// cannot use (disabled, read-only, hidden) does nothing. <see cref="Key"/> names the field or the option, <see cref="Index"/> the
/// tab, the stratigraphy or the segment, <see cref="Text"/> what the user types or chooses; <see cref="Capacity"/> is a step on the
/// tab of the capacity (F5.7), which acts on the shared data.
/// </summary>
internal sealed record ElasticPileStep(string Kind, string? Key = null, string? Text = null, int Index = -1, HorizontalPileStep? Capacity = null)
{
    /// <summary>Commit of the sheet (navigation, save).</summary>
    internal const string Commit = "conferma";
    /// <summary>The «Calcola» command of the window (commit, then the calculation of the tab shown), awaited.</summary>
    internal const string Calculate = "calcola";
    /// <summary>Starts the «Calcola» command of the window without awaiting it.</summary>
    internal const string Start = "avvia-calcolo";
    /// <summary>Waits for the last calculation of the elastic response requested (automatic, 300 ms after the last edit, or started).</summary>
    internal const string Wait = "attendi";
    /// <summary>Closes the sheet and opens its data again (a new sheet on a copy, as the window does).</summary>
    internal const string Reopen = "riapri";
    /// <summary>The sheet becomes a read-only revision.</summary>
    internal const string ReadOnly = "sola-lettura";
    /// <summary>The calculations of the elastic response that start wait for <see cref="Release"/>.</summary>
    internal const string Hold = "trattieni";
    /// <summary>Waits until one more calculation of the elastic response is held.</summary>
    internal const string Entered = "attendi-motore";
    /// <summary>Releases the held calculations; they are no longer held.</summary>
    internal const string Release = "rilascia";
    /// <summary>Selects the tab <see cref="Index"/> of the analyses: 0 capacity, 1 elastic response.</summary>
    internal const string Analysis = "scheda";
    /// <summary>A step of the tab of the capacity (<see cref="Capacity"/>).</summary>
    internal const string CapacityStep = "capacita";
    /// <summary>Chooses «Stratigrafia <see cref="Index"/>+1» for the elastic response.</summary>
    internal const string Survey = "stratigrafia-elastica";
    /// <summary>Types (passo) or chooses (punta) <see cref="Text"/> in the field <see cref="Key"/> of the options of the response.</summary>
    internal const string Option = "opzione-elastica";
    /// <summary>Clicks the check box <see cref="Key"/> of what the drawing shows.</summary>
    internal const string Toggle = "vista";
    /// <summary>«Modifica dati e strati».</summary>
    internal const string EditInputs = "modifica-dati";
    /// <summary>The comparison with an archive of the old elastic input: «Usa dati comuni…» (<see cref="Text"/> «comuni») or «Importa legacy…» («legacy»).</summary>
    internal const string Migration = "migrazione";
    /// <summary>A click on the drawing at the depth <see cref="Text"/> [m] from the head (invariant number).</summary>
    internal const string Depth = "quota";
    /// <summary>Types <see cref="Text"/> in «Fine tratto / barre» of the segment <see cref="Index"/>.</summary>
    internal const string SegmentEnd = "tratto-fine";
    /// <summary>Clicks «Usa principale» of the segment <see cref="Index"/>.</summary>
    internal const string SegmentLinked = "tratto-collegato";
    /// <summary>Types <see cref="Text"/> in the reinforcement field <see cref="Key"/> of the segment <see cref="Index"/>.</summary>
    internal const string SegmentField = "tratto-campo";
    /// <summary>Clicks «Secondo anello interno» of the segment <see cref="Index"/>.</summary>
    internal const string SegmentRing = "tratto-anello";
    /// <summary>Chooses <see cref="Text"/> in «Tipo trasversale» of the segment <see cref="Index"/>.</summary>
    internal const string SegmentKind = "tratto-tipo";
    /// <summary>Chooses <see cref="Text"/> in «Armatura trasversale principale».</summary>
    internal const string MainTransverse = "trasversale";
    /// <summary>
    /// A field <see cref="Key"/> of the assumptions of the reinforcement (verification, seismic head, laps and covers): types or chooses
    /// <see cref="Text"/>, or clicks the check box when <see cref="Text"/> is null.
    /// </summary>
    internal const string Detail = "dettaglio";
    /// <summary>Clicks «Controlla minimi pali NTC §7.2.5…».</summary>
    internal const string Minimum = "minimi";
    /// <summary>«Approva tutto · ipotesi» (<see cref="Text"/> «ipotesi») or «Approva tutto · sisma» («sisma»).</summary>
    internal const string Approve = "approva";
    /// <summary>«Dividi…» of the segment <see cref="Index"/>, then types <see cref="Text"/> and «Dividi» in the dialog; the dialog is closed.</summary>
    internal const string Split = "dividi";
    /// <summary>«Unisci ←» of the segment <see cref="Index"/>.</summary>
    internal const string Merge = "unisci";
    /// <summary>«Verifiche →» of the segment <see cref="Index"/>.</summary>
    internal const string Verify = "verifiche";
    /// <summary>Chooses the segment <see cref="Index"/> in the summary «Sezione e verifiche».</summary>
    internal const string SummarySegment = "riepilogo-tratto";
    /// <summary>Chooses the view <see cref="Text"/> of the summary.</summary>
    internal const string SummaryMode = "riepilogo-modo";
    /// <summary>«Distinta ferri» of the segment <see cref="Index"/>.</summary>
    internal const string Bill = "distinta";
    /// <summary>«Proponi suddivisione…», then «Applica e archivia i tratti precedenti» in the dialog.</summary>
    internal const string Propose = "proponi";
    /// <summary>«Apri verificatore c.a.» of the segment <see cref="Index"/>; with <see cref="Text"/> «applica» the reinforcement is applied, then the dialog is closed.</summary>
    internal const string Concrete = "verificatore";
    /// <summary>«Dimensiona…» of the segment <see cref="Index"/>: catalogues <see cref="Text"/> (counts|diameters|links|spacings), «Trova proposta», then «Applica…» and close.</summary>
    internal const string Design = "dimensiona";

    public override string ToString() => Capacity is not null ? "capacità " + Capacity : Kind + (Key is null ? "" : " " + Key)
        + (Index < 0 ? "" : " [" + Index.ToString(CultureInfo.InvariantCulture) + "]") + (Text is null ? "" : " «" + Text + "»");
}

/// <summary>A scenario: the data of an F3 fixture of the module, then the steps.</summary>
internal sealed record ElasticPileScenario(string Name, string Module, string Fixture, IReadOnlyList<ElasticPileStep> Steps);

/// <summary>
/// The scenarios of the tab of the elastic response (F5.8): for every F3 fixture of the two modules that opens on it the path of the F3
/// capture (opening, the visit of the tabs of the analyses, calculation, reopening); then the options of the response and of the drawing,
/// the selection of a depth, the segments and the assumptions of the reinforcement, the summary, the dialogs (split, proposal, c.a.
/// verifier, design), the comparison with an old archive, a read-only revision, a micropile, and the scheduling of the background
/// calculation with a held engine.
/// </summary>
internal static class ElasticPileScenarios
{
    internal const string Pile = HorizontalPileScenarios.Pile;
    internal const string Micro = HorizontalPileScenarios.Micro;

    /// <summary>Folder of the traces of the historic view, frozen before the migration (proposal 7.2, capture first).</summary>
    internal static string Reference(string root) => Path.Combine(root, "tests", "fixtures", "f5-elastico", "storica");

    /// <summary>Folder of the display lists of the drawings of the historic view (ElasticPileDrawing, PileReinforcementDrawing).</summary>
    internal static string DrawingsReference(string root) => Path.Combine(root, "tests", "fixtures", "f5-elastico", "disegni");

    /// <summary>The F3 fixtures of <paramref name="module"/> that open on the tab of the elastic response, in ordinal order.</summary>
    internal static IReadOnlyList<string> Fixtures(string root, string module) => HorizontalPileScenarios.Fixtures(root, module)
        .Where(id => HorizontalPileScenarios.OpensElastic(HorizontalPileScenarios.FixtureData(root, module, id))).ToArray();

    static ElasticPileStep Step(string kind) => new(kind);
    static ElasticPileStep Tab(int index) => new(ElasticPileStep.Analysis, Index: index);
    static ElasticPileStep Keyed(string kind, string key, string? text = null) => new(kind, key, text);
    static ElasticPileStep Indexed(string kind, int index, string? text = null, string? key = null) => new(kind, key, text, index);
    static ElasticPileStep Typed(string kind, string text) => new(kind, Text: text);
    static ElasticPileStep Capacity(string key, string text) => new(ElasticPileStep.CapacityStep, Capacity: new HorizontalPileStep(HorizontalPileStep.Field, key, text));

    internal static IReadOnlyList<ElasticPileScenario> All(string root)
    {
        ElasticPileStep commit = Step(ElasticPileStep.Commit), calculate = Step(ElasticPileStep.Calculate), wait = Step(ElasticPileStep.Wait),
            reopen = Step(ElasticPileStep.Reopen), hold = Step(ElasticPileStep.Hold), entered = Step(ElasticPileStep.Entered), release = Step(ElasticPileStep.Release);
        var list = new List<ElasticPileScenario>();
        foreach (var (module, prefix) in new[] { (Pile, "palo"), (Micro, "micropalo") })
            foreach (string id in Fixtures(root, module))
                // The F3 visit selects every tab of the analyses and comes back to the elastic response.
                list.Add(new(prefix + "-" + id, module, id, [commit, Tab(0), Tab(1), wait, calculate, commit, reopen, commit, wait]));
        list.Add(new("modifiche-risposta", Pile, "elastico-tratti-anello-spirale", [
            wait,
            Typed(ElasticPileStep.Depth, "5.5"), Typed(ElasticPileStep.Depth, "5.5"),
            Indexed(ElasticPileStep.Survey, 0), wait,
            Keyed(ElasticPileStep.Option, "passo", "0,5"), wait,
            Keyed(ElasticPileStep.Option, "punta", "Libera"), wait,
            Typed(ElasticPileStep.Depth, "3"),
            Keyed(ElasticPileStep.Toggle, "mesh"), Keyed(ElasticPileStep.Toggle, "molle"), Keyed(ElasticPileStep.Toggle, "N"), Keyed(ElasticPileStep.Toggle, "sisma"),
            Capacity("azione_orizzontale", "320"), wait,
            Keyed(ElasticPileStep.Option, "passo", "abc"), wait,
            Keyed(ElasticPileStep.Option, "passo", "0,25"), wait,
            Step(ElasticPileStep.EditInputs), Tab(1), wait,
            calculate, commit, reopen, commit, wait]));
        list.Add(new("armature-tratti", Pile, "elastico-tratti-anello-spirale", [
            wait,
            Indexed(ElasticPileStep.SegmentField, 1, "14", "longitudinal_bar_count"), wait,
            Indexed(ElasticPileStep.SegmentEnd, 0, "6"), wait,
            Indexed(ElasticPileStep.SegmentLinked, 1), wait,
            Indexed(ElasticPileStep.SegmentLinked, 1), wait,
            Indexed(ElasticPileStep.SegmentRing, 1), Indexed(ElasticPileStep.SegmentField, 1, "8", "second_inner_count"), wait,
            Indexed(ElasticPileStep.SegmentRing, 2), wait,
            Indexed(ElasticPileStep.SegmentKind, 2, "Spirale"), wait,
            Typed(ElasticPileStep.MainTransverse, "Spirale"), wait,
            Keyed(ElasticPileStep.Detail, "z_d", "0,85"), Keyed(ElasticPileStep.Detail, "barre_trattenute"), Keyed(ElasticPileStep.Detail, "elemento", "Trave"), wait,
            Keyed(ElasticPileStep.Detail, "sisma_testa"), Keyed(ElasticPileStep.Detail, "sisma_lunghezza", "12"), wait,
            Keyed(ElasticPileStep.Detail, "copriferro_override"), Keyed(ElasticPileStep.Detail, "cmin_dur", "40"), wait,
            Step(ElasticPileStep.Minimum), wait,
            Typed(ElasticPileStep.Approve, "ipotesi"), wait,
            Typed(ElasticPileStep.Approve, "sisma"), wait,
            Indexed(ElasticPileStep.Verify, 1),
            Typed(ElasticPileStep.SummaryMode, "N–M"), Typed(ElasticPileStep.SummaryMode, "Taglio"), Typed(ElasticPileStep.SummaryMode, "Interferro"),
            Typed(ElasticPileStep.SummaryMode, "Minimi armatura"), Typed(ElasticPileStep.SummaryMode, "Dettagli pilastro / trave"),
            Typed(ElasticPileStep.SummaryMode, "Sisma · testa palo"), Typed(ElasticPileStep.SummaryMode, "Copriferro"),
            Typed(ElasticPileStep.SummaryMode, "Sviluppo / sovrapposizioni"), Typed(ElasticPileStep.SummaryMode, "Distinta ferri"),
            Indexed(ElasticPileStep.SummarySegment, 2), Indexed(ElasticPileStep.Bill, 0),
            Indexed(ElasticPileStep.Split, 2, "abc"), Indexed(ElasticPileStep.Split, 2, "12"), wait,
            Indexed(ElasticPileStep.Merge, 3), wait,
            calculate, commit, reopen, commit, wait]));
        list.Add(new("armature-dialoghi", Pile, "single-soil-elastic", [
            wait,
            Step(ElasticPileStep.Propose), wait,
            Indexed(ElasticPileStep.Concrete, 0, "applica"), wait,
            Indexed(ElasticPileStep.Concrete, 1, "chiudi"), wait,
            Indexed(ElasticPileStep.Design, 1, "16|24|10|150"),
            Typed(ElasticPileStep.Approve, "ipotesi"), wait,
            Indexed(ElasticPileStep.Design, 1, "16|24|10|150"), wait,
            calculate, commit, reopen, commit, wait]));
        list.Add(new("sola-lettura", Pile, "elastico-sisma-testa-impedita", [
            wait, Step(ElasticPileStep.ReadOnly),
            Keyed(ElasticPileStep.Option, "passo", "1"), Keyed(ElasticPileStep.Option, "punta", "Libera"), Keyed(ElasticPileStep.Toggle, "y"), Step(ElasticPileStep.EditInputs),
            Indexed(ElasticPileStep.SegmentEnd, 0, "7"), Indexed(ElasticPileStep.SegmentLinked, 1), Indexed(ElasticPileStep.SegmentField, 1, "20", "longitudinal_bar_count"),
            Indexed(ElasticPileStep.SegmentRing, 1), Indexed(ElasticPileStep.SegmentKind, 1, "Spirale"), Typed(ElasticPileStep.MainTransverse, "Spirale"),
            Keyed(ElasticPileStep.Detail, "z_d", "0,7"), Keyed(ElasticPileStep.Detail, "sisma_azioni"), Step(ElasticPileStep.Minimum), Typed(ElasticPileStep.Approve, "ipotesi"),
            Indexed(ElasticPileStep.Verify, 1), Typed(ElasticPileStep.SummaryMode, "Taglio"), Indexed(ElasticPileStep.Bill, 1),
            Indexed(ElasticPileStep.Split, 0, "4"), Indexed(ElasticPileStep.Merge, 1), Step(ElasticPileStep.Propose), Indexed(ElasticPileStep.Concrete, 0, "applica"),
            Typed(ElasticPileStep.Depth, "2"),
            Tab(0), Tab(1), wait,
            calculate, commit]));
        list.Add(new("migrazione-comuni", Pile, "legacy-elastic-pending", [
            commit, Typed(ElasticPileStep.Migration, "comuni"), wait, calculate, commit, reopen, commit, wait]));
        list.Add(new("migrazione-legacy", Micro, "legacy-elastic-pending", [
            commit, Typed(ElasticPileStep.Migration, "legacy"), wait, calculate, commit, reopen, commit, wait]));
        list.Add(new("scelta-stratigrafia", Pile, "elastico-stratigrafia-da-scegliere", [
            wait, Indexed(ElasticPileStep.Survey, 1), wait, Indexed(ElasticPileStep.Survey, 0), wait,
            new(ElasticPileStep.CapacityStep, Capacity: new HorizontalPileStep(HorizontalPileStep.AddSurvey)), wait,
            Indexed(ElasticPileStep.Survey, 2), wait,
            calculate, commit, reopen, commit, wait]));
        list.Add(new("micropalo", Micro, "elastico-chs-stratificato", [
            wait, Indexed(ElasticPileStep.Concrete, 0, "chiudi"), Step(ElasticPileStep.Propose), Indexed(ElasticPileStep.Verify, 0),
            Indexed(ElasticPileStep.SegmentEnd, 0, "4"), wait,
            Keyed(ElasticPileStep.Toggle, "tratti"), Typed(ElasticPileStep.Depth, "3"),
            Capacity("profondita_falda", "2"), wait,
            calculate, commit, reopen, commit, wait]));
        list.Add(new("calcolo-sullo-sfondo", Pile, "single-soil-elastic", [
            wait,
            hold, Capacity("azione_orizzontale", "120"), entered, Capacity("azione_orizzontale", "130"), release, wait,
            hold, Keyed(ElasticPileStep.Detail, "z_d", "0,8"), entered, Step(ElasticPileStep.Start), release, wait,
            hold, Capacity("azione_orizzontale", "140"), entered, Tab(0), release, wait, Tab(1), wait,
            hold, Capacity("azione_orizzontale", "150"), entered, reopen, release, wait, commit]));
        return list;
    }
}

/// <summary>A command of the tab (a button): its label and whether the user can use it.</summary>
internal sealed record ElasticCommandState(string Label, bool Enabled)
{
    internal string Line() => "comando " + ElasticPileTrace.Quote(Label) + (Enabled ? " attivo" : " spento");
}

/// <summary>A check box of what the drawing shows.</summary>
internal sealed record ElasticToggleState(string Key, string Label, bool Checked, bool Enabled)
{
    internal string Item() => Key + "=" + ElasticPileTrace.YesNo(Checked) + (Enabled ? "" : "!");
}

/// <summary>A field of the card of a segment: key, title, text, read-only and its tool tip.</summary>
internal sealed record ElasticSegmentFieldState(string Key, string Title, string Text, bool ReadOnly, string? ToolTip)
{
    internal string Line() => "campo " + Key + " " + ElasticPileTrace.Quote(Title) + " = " + ElasticPileTrace.Quote(Text) + (ReadOnly ? " · sola lettura" : "")
        + " · nota " + ElasticPileTrace.Quote(ToolTip);
}

/// <summary>The card of a segment of the reinforcement.</summary>
internal sealed class ElasticSegmentState
{
    public string Header = "", End = "";
    public bool EndEditable, Linked, LinkedEnabled, Ring, RingEnabled, KindEnabled, BillExpanded;
    public string? EndToolTip, RingToolTip;
    public string Kind = "";
    public IReadOnlyList<string> Kinds = [];
    public List<ElasticSegmentFieldState> Fields = [];
    public List<ElasticCommandState> Commands = [];
    public string Bill = "";

    internal IEnumerable<string> Lines()
    {
        yield return "  tratto " + ElasticPileTrace.Quote(Header);
        yield return "    fine " + ElasticPileTrace.Quote(End) + (EndEditable ? "" : " · sola lettura") + " · nota " + ElasticPileTrace.Quote(EndToolTip);
        yield return "    principale " + ElasticPileTrace.YesNo(Linked) + (LinkedEnabled ? " · attiva" : " · spenta");
        foreach (var field in Fields) yield return "    " + field.Line();
        yield return "    anello " + ElasticPileTrace.YesNo(Ring) + (RingEnabled ? " · attivo" : " · spento") + " · nota " + ElasticPileTrace.Quote(RingToolTip);
        yield return "    tipo " + ElasticPileTrace.Quote(Kind) + " di " + ElasticPileTrace.Quote(string.Join("|", Kinds)) + (KindEnabled ? " · attivo" : " · spento");
        foreach (var command in Commands) yield return "    " + command.Line();
        yield return "    distinta" + (BillExpanded ? " aperta " : " chiusa ") + ElasticPileTrace.Quote(Bill);
    }
}

/// <summary>The editor of the reinforcement of the pile (PileReinforcementEditor), as the user sees it.</summary>
internal sealed class ElasticReinforcementState
{
    public string Phase = "", Progress = "", Message = "";
    public List<ElasticCommandState> Commands = [];
    public string MainTransverse = "";
    public IReadOnlyList<string> MainTransverseChoices = [];
    public bool MainTransverseEnabled;
    public List<ElasticSegmentState> Segments = [];
    public bool DetailsExpanded, SeismicExpanded, ExtraExpanded;
    public List<VerticalPileFieldState> Details = [], Seismic = [], Extra = [];
    public List<ElasticCommandState> SeismicCommands = [];
    public bool Minimum, MinimumEnabled;
    /// <summary>The panel «Sezione e verifiche», one line per element (<see cref="ElasticPileTrace"/> formats).</summary>
    public List<string> Summary = [];

    internal IEnumerable<string> Lines()
    {
        yield return "  fase " + ElasticPileTrace.Quote(Phase) + " · avanzamento " + Progress;
        yield return "  messaggio " + ElasticPileTrace.Quote(Message);
        foreach (var command in Commands) yield return "  " + command.Line();
        yield return "  trasversale " + ElasticPileTrace.Quote(MainTransverse) + " di " + ElasticPileTrace.Quote(string.Join("|", MainTransverseChoices))
            + (MainTransverseEnabled ? " · attiva" : " · spenta");
        foreach (var segment in Segments) foreach (string line in segment.Lines()) yield return line;
        yield return "  form ipotesi" + (DetailsExpanded ? " · aperto" : " · chiuso");
        foreach (var field in Details) yield return "    " + field.Line();
        yield return "  form sisma" + (SeismicExpanded ? " · aperto" : " · chiuso");
        foreach (var field in Seismic) yield return "    " + field.Line();
        foreach (var command in SeismicCommands) yield return "    " + command.Line();
        yield return "  form sovrapposizioni" + (ExtraExpanded ? " · aperto" : " · chiuso");
        foreach (var field in Extra) yield return "    " + field.Line();
        yield return "  minimi " + ElasticPileTrace.YesNo(Minimum) + (MinimumEnabled ? " · attivo" : " · spento");
        yield return "  riepilogo";
        foreach (string line in Summary) yield return "    " + line;
    }
}

/// <summary>
/// The state of the tab of the elastic response seen by the user after a step: data in memory order, Modified, the tab of the analyses,
/// the response and the result (hashes), status and progress, the stiffness, the comparison with an old archive or the header (the
/// stratigraphy, the options, what the drawing shows), what the drawing shows and the selected depth with its section, the extremes and
/// the table, the reinforcement drawn and summarized, and the editor of the reinforcement. Each side fills it from its own objects;
/// <see cref="Lines"/> writes it in one format for both, with the random identifiers made stable (<see cref="ElasticPileIds"/>).
/// </summary>
internal sealed class ElasticPileTraceState
{
    public JsonObject Data = new();
    public int Modified, Analysis;
    public bool Editable, Exportable;
    public JsonObject? Response, Result;
    public string Status = "", Progress = "", Stiffness = "";
    public string? Migration;
    public List<ElasticCommandState> MigrationCommands = [];
    public bool Header;
    public IReadOnlyList<string> SurveyChoices = [];
    public int SurveyChoice = -1;
    public bool SurveyEnabled;
    public List<ElasticCommandState> HeaderCommands = [];
    public List<VerticalPileFieldState> Options = [];
    public List<ElasticToggleState> Toggles = [];
    /// <summary>What the drawing shows: «risposta» or «risultato» with the hash, or «-».</summary>
    public JsonObject? Drawing;
    public JsonNode? Selected;
    public string Selection = "", SectionPreview = "", SectionDetail = "", Summary = "";
    public List<string[]> Table = [];
    public bool Csv;
    public string ReinforcementSummary = "", SheetMessage = "";
    public int SheetPieces;
    public JsonObject? Sheet;
    public ElasticReinforcementState? Reinforcement;
    /// <summary>What the dialog of the step showed when the step left it: the error of a split, the proposal, the note of the c.a. verifier, the status of the design.</summary>
    public string? Dialog;

    internal IEnumerable<string> Lines(string step, ElasticPileIds ids)
    {
        IEnumerable<string> Raw()
        {
            yield return "== " + step;
            yield return "dati " + ElasticPileTrace.Compact(Data);
            yield return "modificato " + Modified.ToString(CultureInfo.InvariantCulture) + " · modificabile " + ElasticPileTrace.YesNo(Editable)
                + " · scheda " + Analysis.ToString(CultureInfo.InvariantCulture) + " · esportabile " + ElasticPileTrace.YesNo(Exportable);
            yield return "risposta " + ElasticPileTrace.Hash(Response, ids) + " · risultato " + ElasticPileTrace.Hash(Result, ids);
            yield return "stato " + ElasticPileTrace.Quote(Status) + " · avanzamento " + Progress;
            yield return "rigidezza " + ElasticPileTrace.Quote(Stiffness);
            if (Migration is null) yield return "migrazione assente";
            else
            {
                yield return "migrazione " + ElasticPileTrace.Quote(Migration);
                foreach (var command in MigrationCommands) yield return "  " + command.Line();
            }
            if (!Header) yield return "intestazione assente";
            else
            {
                yield return "intestazione stratigrafie " + ElasticPileTrace.Quote(string.Join("|", SurveyChoices)) + " scelta " + SurveyChoice.ToString(CultureInfo.InvariantCulture)
                    + (SurveyEnabled ? " · attiva" : " · spenta");
                foreach (var command in HeaderCommands) yield return "  " + command.Line();
                yield return "  opzioni";
                foreach (var field in Options) yield return "    " + field.Line();
                yield return "  viste " + string.Join(",", Toggles.Select(t => t.Item()));
            }
            yield return "disegno " + (Drawing is null ? "-" : ReferenceEquals(Drawing, Result) ? "risultato" : ReferenceEquals(Drawing, Response) ? "risposta" : "altro "
                + ElasticPileTrace.Hash(Drawing, ids)) + " · quota " + (Selected is null ? "-" : ElasticPileTrace.Point(Selected));
            yield return "selezione " + ElasticPileTrace.Quote(Selection);
            yield return "sezione " + SectionPreview;
            yield return "dettaglio " + ElasticPileTrace.Quote(SectionDetail);
            yield return "estremi " + ElasticPileTrace.Quote(Summary);
            yield return "tabella righe " + Table.Count.ToString(CultureInfo.InvariantCulture) + " " + ElasticPileTrace.HashOf(Table) + " · csv " + ElasticPileTrace.YesNo(Csv);
            foreach (var row in Table.Take(2)) yield return "  " + string.Join(" | ", row);
            yield return "verifiche " + ElasticPileTrace.Quote(ReinforcementSummary);
            yield return "tavola " + ElasticPileTrace.Quote(SheetMessage) + " · pezzi " + SheetPieces.ToString(CultureInfo.InvariantCulture) + " · "
                + (Sheet is null ? "-" : ReferenceEquals(Sheet, Result) ? "risultato" : "altro " + ElasticPileTrace.Hash(Sheet, ids));
            if (Dialog is not null) yield return "dialogo " + ElasticPileTrace.Quote(Dialog);
            if (Reinforcement is null) yield return "armature assenti";
            else
            {
                yield return "armature";
                foreach (string line in Reinforcement.Lines()) yield return line;
            }
        }
        return Raw().Select(ids.Stable);
    }
}

/// <summary>
/// The random identifiers of the data (segments split by the user, actions of the sheets of the c.a. verifier: Guid.NewGuid) replaced by
/// placeholders in the order in which they first appear in a trace, so that two runs give the same lines.
/// </summary>
internal sealed partial class ElasticPileIds
{
    private readonly Dictionary<string, string> names = new(StringComparer.Ordinal);

    // A split segment is «T-» and six hexadecimal digits, also after the escaped new line of a quoted text; an action is 32 digits or a Guid.
    [GeneratedRegex(@"T-[0-9a-f]{6}(?![0-9A-Za-z])|(?<![0-9A-Za-z])([0-9a-f]{32}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})(?![0-9A-Za-z])")]
    private static partial Regex Random();

    internal string Stable(string line) => Random().Replace(line, m =>
    {
        if (!names.TryGetValue(m.Value, out var name)) names[m.Value] = name = (m.Value.StartsWith("T-", StringComparison.Ordinal) ? "T-" : "") + "<id" + (names.Count + 1).ToString(CultureInfo.InvariantCulture) + ">";
        return name;
    });
}

/// <summary>Formats of the trace, shared by the two sides (the compact JSON, hashes and quotes of the pile group traces).</summary>
internal static class ElasticPileTrace
{
    internal static string Compact(JsonNode? node) => PileGroupTrace.Compact(node);

    internal static string Quote(string? text) => PileGroupTrace.Quote(text);

    internal static string YesNo(bool value) => PileGroupTrace.YesNo(value);

    internal static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>Hash of a response or result without the times measured with a stopwatch (tempi_ms) and with the identifiers made stable.</summary>
    internal static string Hash(JsonNode? node, ElasticPileIds ids)
    {
        if (node is null) return "-";
        var copy = node.DeepClone();
        Strip(copy);
        return PileGroupTrace.Hash(JsonValue.Create(ids.Stable(Compact(copy))));
    }

    static void Strip(JsonNode? node)
    {
        if (node is JsonObject o) { o.Remove("tempi_ms"); foreach (var (_, value) in o) Strip(value); }
        else if (node is JsonArray a) foreach (var item in a) Strip(item);
    }

    /// <summary>Hash of texts or numbers, serialized as JSON with the invariant culture.</summary>
    internal static string HashOf(object value) => PileGroupTrace.Hash(JsonSerializer.SerializeToNode(value, PileGroupTrace.Json)!);

    /// <summary>A point of the response: depth and side.</summary>
    internal static string Point(JsonNode point) => Number(point["Depth"]!.GetValue<double>()) + "/" + point["Side"]?.ToString();

    /// <summary>A row of the table «Valori per quota»: the values in the order of the columns, invariant.</summary>
    internal static string[] Row(IEnumerable<object?> values) => values.Select(v => v switch
    {
        double d => Number(d),
        string s => Quote(s),
        null => "null",
        _ => Quote(Convert.ToString(v, CultureInfo.InvariantCulture))
    }).ToArray();

    /// <summary>The section drawn at a depth or in the summary: outline, bars, link, axes, caption and the name of the link.</summary>
    internal static string Section(IReadOnlyList<double[]> outline, IEnumerable<(double X, double Y, double Area, double Diameter)> bars, double linkRadius,
        double linkDiameter, bool axes, string? caption, string? linkCaption) =>
        HorizontalPileTrace.ConcreteSection(outline, bars, linkRadius, linkDiameter, axes, caption) + " · " + Quote(linkCaption);

    /// <summary>The progress bar: hidden, indeterminate or its value.</summary>
    internal static string Progress(bool visible, bool indeterminate, double value) =>
        !visible ? "nascosto" : indeterminate ? "indeterminato" : Number(value);

    // Lines of the panel «Sezione e verifiche», one per element in its order.
    internal static string SummaryText(string tone, string text) => "testo " + tone + " " + Quote(text);
    internal static string SummaryChoice(IEnumerable<string> items, string? selected, bool enabled) =>
        "scelta " + Quote(selected) + " di " + Quote(string.Join("|", items)) + (enabled ? " · attiva" : " · spenta");
    internal static string SummarySection(string section) => "sezione " + section;
    internal static string SummaryVerdict(string tone, string text) => "esito " + tone + " " + Quote(text);
    internal static string SummaryGroup(string header, bool expanded, string tone, string text) =>
        "gruppo " + Quote(header) + (expanded ? " aperto " : " chiuso ") + tone + " " + Quote(text);
    internal static string SummaryCommand(string label, bool enabled) => new ElasticCommandState(label, enabled).Line();

    /// <summary>The name of a colour of the texts of the tab (the tone of a verdict), from its #RRGGBB.</summary>
    internal static string Tone(string rgb) => rgb.ToUpperInvariant() switch
    {
        "#0B2A4A" => "blu",
        "#64748B" => "attenuato",
        "#B22222" => "rosso",
        "#B8860B" => "ambra",
        "#006400" => "verde",
        "#708090" => "grigio",
        var other => "colore " + other
    };

    internal static void Write(string path, IEnumerable<string> lines) => PileGroupTrace.Write(path, lines);

    internal static string[] Read(string path) => PileGroupTrace.Read(path);

    internal static string? FirstDifference(IReadOnlyList<string> expected, IReadOnlyList<string> actual) => PileGroupTrace.FirstDifference(expected, actual);
}

/// <summary>
/// The calculations of the elastic response of the traces and of the checks: each sheet gets its own <see cref="ElasticPileAnalysisSession"/>
/// (<see cref="Create"/>), all with one barrier. While held, every calculation that starts signals its entry and waits for
/// <see cref="Release"/> (at most 30 s) without blocking the UI thread, then runs the session.
/// </summary>
internal sealed class HeldElasticAnalyses
{
    private readonly object sync = new();
    private readonly SemaphoreSlim entered = new(0);
    private TaskCompletionSource gate = Open();
    private int calls;

    internal int Calls => Volatile.Read(ref calls);

    static TaskCompletionSource Open() { var open = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); open.SetResult(); return open; }

    /// <summary>A new calculation for a sheet, behind the barrier.</summary>
    internal IElasticPileAnalysis Create() => new Held(this, new ElasticPileAnalysisSession());

    /// <summary>The next calculations wait for <see cref="Release"/>.</summary>
    internal void Hold() { lock (sync) if (gate.Task.IsCompleted) gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); }

    /// <summary>Releases the held calculations; they are no longer held.</summary>
    internal void Release() { lock (sync) gate.TrySetResult(); }

    /// <summary>Completes when one more calculation is held (at most 30 s).</summary>
    internal async Task EnteredAsync()
    {
        if (!await entered.WaitAsync(TimeSpan.FromSeconds(30))) throw new TimeoutException("Nessun calcolo elastico è entrato nel motore trattenuto.");
    }

    private async Task PassAsync()
    {
        Interlocked.Increment(ref calls);
        Task current;
        lock (sync) current = gate.Task;
        if (current.IsCompleted) return;
        entered.Release();
        await current.WaitAsync(TimeSpan.FromSeconds(30));
    }

    private sealed class Held(HeldElasticAnalyses owner, ElasticPileAnalysisSession inner) : IElasticPileAnalysis
    {
        public int ResponseCalculations => inner.ResponseCalculations;

        public async Task<JsonObject> CalculateAsync(JsonObject source, Action<JsonObject> responseReady, IProgress<PileProgress>? progress = null, CancellationToken cancellation = default)
        {
            await owner.PassAsync();
            return await inner.CalculateAsync(source, responseReady, progress, cancellation);
        }

        public void Dispose() => inner.Dispose();
    }
}
