using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
using Anthea.Application.Presentation;

namespace Anthea.Presentation.Tests;

/// <summary>
/// One step of a scenario of the vertical pile and micropile sheet (F5.6), applied in the same way to the historic view (the
/// generic branch of SheetEditor and GeoEditor, tests/Desktop/VerticalPileHistoricTrace.cs) and to its ViewModel
/// (tests/ANTHEA.Presentation.Tests/VerticalPileSheetChecks.cs). A step is what the user does with a control: a control that the
/// user cannot use (disabled, read-only revision) does nothing. <see cref="Key"/> names the field, the column or the curve;
/// <see cref="Index"/> the stratigraphy, the capacity view or the table; <see cref="Row"/> the layer; <see cref="Text"/> what the
/// user types or chooses.
/// </summary>
internal sealed record VerticalPileStep(string Kind, string? Key = null, string? Text = null, int Index = -1, int Row = -1)
{
    /// <summary>Commit of the sheet (navigation, save).</summary>
    internal const string Commit = "conferma";
    /// <summary>The «Calcola» command of the window (commit, then calculation), awaited.</summary>
    internal const string Calculate = "calcola";
    /// <summary>Starts the «Calcola» command of the window without awaiting it.</summary>
    internal const string Start = "avvia-calcolo";
    /// <summary>Waits for the last calculation requested: the automatic one (450 ms after the last edit) or the started one.</summary>
    internal const string Wait = "attendi";
    /// <summary>Closes the sheet and opens its data again (a new sheet on a copy, as the window does).</summary>
    internal const string Reopen = "riapri";
    /// <summary>The sheet becomes a read-only revision.</summary>
    internal const string ReadOnly = "sola-lettura";
    /// <summary>Types <see cref="Text"/> in the text field <see cref="Key"/> of the general, efficiency or normative form.</summary>
    internal const string Field = "campo";
    /// <summary>Chooses <see cref="Text"/> in the choice <see cref="Key"/> of a form.</summary>
    internal const string Choice = "scelta";
    /// <summary>Clicks the check box <see cref="Key"/> of a form.</summary>
    internal const string Option = "opzione";
    /// <summary>The «Reset» button of the normative coefficients.</summary>
    internal const string Reset = "reset";
    /// <summary>Selects the tab of the stratigraphy <see cref="Index"/>.</summary>
    internal const string Survey = "stratigrafia";
    /// <summary>The «+» button: a new stratigraphy.</summary>
    internal const string AddSurvey = "aggiungi-stratigrafia";
    /// <summary>The [−] button of the tab of the stratigraphy <see cref="Index"/>, confirmed.</summary>
    internal const string RemoveSurvey = "togli-stratigrafia";
    /// <summary>The «−» button: removes the selected stratigraphy, confirmed.</summary>
    internal const string RemoveSelectedSurvey = "togli-selezionata";
    /// <summary>
    /// Selects the stratigraphy <see cref="Index"/>, then «Copia in…» and the destination <see cref="Text"/> (a number, or «nuova»
    /// for a new stratigraphy), confirmed.
    /// </summary>
    internal const string CopySurvey = "copia-stratigrafia";
    /// <summary>
    /// Selects the stratigraphy <see cref="Index"/>, then edits the cell <see cref="Key"/> of the layer <see cref="Row"/>: types or
    /// chooses <see cref="Text"/>, or clicks the check box when <see cref="Text"/> is null.
    /// </summary>
    internal const string Layer = "strato";
    /// <summary>Selects the stratigraphy <see cref="Index"/>, then «Aggiungi strato».</summary>
    internal const string AddLayer = "aggiungi-strato";
    /// <summary>Selects the stratigraphy <see cref="Index"/>, then the [−] button of the layer <see cref="Row"/>.</summary>
    internal const string RemoveLayer = "togli-strato";
    /// <summary>Chooses the capacity view <see cref="Index"/> (all the design curves, then one condition at a time).</summary>
    internal const string CapacityView = "vista";
    /// <summary>Clicks the check box of the curve <see cref="Key"/>.</summary>
    internal const string Curve = "curva";
    /// <summary>The «Tutte» button of the curves.</summary>
    internal const string AllCurves = "tutte";
    /// <summary>The «Nessuna» button of the curves.</summary>
    internal const string NoCurves = "nessuna";
    /// <summary>Chooses the table <see cref="Index"/> of «Tabelle e dettagli».</summary>
    internal const string Table = "tabella";
    /// <summary>The engine holds every calculation that enters it until <see cref="Release"/>.</summary>
    internal const string Hold = "trattieni";
    /// <summary>Waits until one more calculation is inside the held engine.</summary>
    internal const string Entered = "attendi-motore";
    /// <summary>Releases the held calculations; the engine stops holding.</summary>
    internal const string Release = "rilascia";

    public override string ToString() => Kind + (Key is null ? "" : " " + Key)
        + (Index < 0 ? "" : " [" + Index.ToString(CultureInfo.InvariantCulture) + (Row < 0 ? "" : "," + Row.ToString(CultureInfo.InvariantCulture)) + "]")
        + (Text is null ? "" : " «" + Text + "»");
}

/// <summary>A scenario: the data of an F3 fixture of the module, then the steps.</summary>
internal sealed record VerticalPileScenario(string Name, string Module, string Fixture, IReadOnlyList<VerticalPileStep> Steps);

/// <summary>
/// The scenarios of the vertical pile and micropile sheet (F5.6): for every F3 fixture of the two modules the path of the F3
/// capture (opening, commit, calculation, reopening); then edits of every kind on the pile and on the micropile, the
/// stratigraphies, a read-only revision, and the scheduling of the background calculation with a held engine (cancellation,
/// publication of the current revision only, closing during a calculation).
/// </summary>
internal static class VerticalPileScenarios
{
    internal const string Pile = "geo_palo_verticale";
    internal const string Micro = "geo_micropalo_verticale";

    /// <summary>Folder of the traces of the historic view, frozen before the migration (proposal 7.2, capture first).</summary>
    internal static string Reference(string root) => Path.Combine(root, "tests", "fixtures", "f5-palo-verticale", "storica");

    internal static string FixturePath(string root, string module, string id) => Path.Combine(root, "tests", "fixtures", "f3", "inputs", module + "--" + id + ".json");

    /// <summary>The data of an F3 fixture, as the F3 capture reads them.</summary>
    internal static JsonObject FixtureData(string root, string module, string id) => JsonNode.Parse(File.ReadAllText(FixturePath(root, module, id)))!["data"]!.AsObject();

    /// <summary>Identifiers of the F3 fixtures of <paramref name="module"/>, in ordinal order.</summary>
    internal static IReadOnlyList<string> Fixtures(string root, string module) => Directory.EnumerateFiles(Path.Combine(root, "tests", "fixtures", "f3", "inputs"), module + "--*.json")
        .Select(f => Path.GetFileNameWithoutExtension(f)[(module.Length + 2)..]).Order(StringComparer.Ordinal).ToArray();

    static VerticalPileStep Step(string kind) => new(kind);
    static VerticalPileStep Field(string key, string text) => new(VerticalPileStep.Field, key, text);
    static VerticalPileStep Choice(string key, string text) => new(VerticalPileStep.Choice, key, text);
    static VerticalPileStep Option(string key) => new(VerticalPileStep.Option, key);
    static VerticalPileStep Layer(int survey, int row, string key, string? text) => new(VerticalPileStep.Layer, key, text, survey, row);
    static VerticalPileStep Indexed(string kind, int index, int row = -1) => new(kind, Index: index, Row: row);

    internal static IReadOnlyList<VerticalPileScenario> All(string root)
    {
        VerticalPileStep commit = Step(VerticalPileStep.Commit), calculate = Step(VerticalPileStep.Calculate), wait = Step(VerticalPileStep.Wait),
            reopen = Step(VerticalPileStep.Reopen), hold = Step(VerticalPileStep.Hold), entered = Step(VerticalPileStep.Entered), release = Step(VerticalPileStep.Release);
        var list = new List<VerticalPileScenario>();
        foreach (var (module, prefix) in new[] { (Pile, "palo"), (Micro, "micropalo") })
            list.AddRange(Fixtures(root, module).Select(id => new VerticalPileScenario(prefix + "-" + id, module, id, [commit, calculate, commit, reopen, commit, wait])));
        list.Add(new("modifiche-palo", Pile, "regressione-palo_storico_0", [
            wait,
            Field("lunghezza", "14"), wait,
            Choice("tipo_palo", "Battuto"), wait,
            Choice("sottotipo_palo_battuto", "Tubo d'acciaio chiuso"), wait,
            Field("sicurezza_base", "1,40"), wait,
            Option("presenza_falda"), wait,
            Field("profondita_falda", "4"), wait,
            Option("considera_sottospinta"), wait,
            Choice("verticali_indagate", "4"), wait,
            Choice("metodo", "Converse-Labarre"), Field("numero_pali_x", "3"), Field("interasse_x", "2,5"), wait,
            Choice("metodo", "Definita dall'utente"), Field("eta_compressione", "0,8"), wait,
            Step(VerticalPileStep.Reset), wait,
            Field("azione_trazione", ""), wait,
            Field("diametro", "abc"), wait,
            Field("diametro", "1"), wait,
            Layer(0, 0, "angolo_attrito", "32"), wait,
            Indexed(VerticalPileStep.CapacityView, 1), Indexed(VerticalPileStep.CapacityView, 3), Indexed(VerticalPileStep.CapacityView, 0),
            new(VerticalPileStep.Curve, "progetto_drenante_trazione"), new(VerticalPileStep.Curve, "azione_compressione"),
            Step(VerticalPileStep.NoCurves), Step(VerticalPileStep.AllCurves),
            Indexed(VerticalPileStep.Table, 1), Indexed(VerticalPileStep.Table, 2), Indexed(VerticalPileStep.Table, 0),
            calculate, commit, reopen, commit, wait]));
        list.Add(new("stratigrafie-palo", Pile, "battuto-falda-sondaggi", [
            wait,
            Indexed(VerticalPileStep.Survey, 1),
            Layer(1, 1, "tipologia", "Granulare"), wait,
            Layer(0, 0, "laterale_attiva", null), wait,
            Layer(0, 2, "spessore", "12,5"), wait,
            Layer(0, 1, "coesione_non_drenata", ""), wait,
            Layer(0, 1, "coesione_non_drenata", "75"), wait,
            Indexed(VerticalPileStep.AddLayer, 1), wait,
            Indexed(VerticalPileStep.RemoveLayer, 1, 2), wait,
            Step(VerticalPileStep.AddSurvey), wait,
            new(VerticalPileStep.CopySurvey, Text: "2", Index: 0), wait,
            new(VerticalPileStep.CopySurvey, Text: "nuova", Index: 1), wait,
            Indexed(VerticalPileStep.RemoveSurvey, 3), wait,
            Indexed(VerticalPileStep.Survey, 2), Step(VerticalPileStep.RemoveSelectedSurvey), wait,
            calculate, commit, reopen, commit, wait]));
        string[] soils = PileInputCatalog.MicropileSoils;
        list.Add(new("modifiche-micropalo", Micro, "regressione-micropalo_igu_0_nessuna-riduzione", [
            wait,
            // With a result the historic view fails when the injection changes (VerticalPileCharts.Reference with the result of the
            // other injection, while it rebuilds the stratigraphies): the injection is changed when there is no result.
            Field("pressione_iniezione", ""), wait,
            Choice("tipo_iniezione", "IRS"), wait,
            Field("pressione_iniezione", "2,8"), wait,
            Choice("profilo_chs", "CHS 168.3 × 10"), wait,
            Option("considera_punta"), wait,
            Field("inclinazione", "10"), wait,
            Field("inizio_aderenza", "12"), wait,
            Field("inizio_aderenza", "1"), wait,
            Layer(0, 0, "terreno", soils[3]), wait,
            Layer(0, 1, "alpha", "1,5"), wait,
            Layer(0, 1, "laterale_attiva", null), wait,
            Indexed(VerticalPileStep.AddLayer, 0), wait,
            Indexed(VerticalPileStep.RemoveLayer, 0, 2), wait,
            Step(VerticalPileStep.AddSurvey), wait,
            Indexed(VerticalPileStep.RemoveSurvey, 1), wait,
            Indexed(VerticalPileStep.CapacityView, 1), Indexed(VerticalPileStep.CapacityView, 2),
            new(VerticalPileStep.Curve, "progetto_compressione"), Indexed(VerticalPileStep.Table, 3),
            calculate, commit, reopen, commit, wait]));
        list.Add(new("sola-lettura", Pile, "elica-coesivo-avvisi", [
            wait, Step(VerticalPileStep.ReadOnly),
            Field("diametro", "2"), Choice("tipo_palo", "Battuto"), Option("presenza_falda"), Step(VerticalPileStep.Reset),
            Layer(0, 0, "spessore", "5"), Indexed(VerticalPileStep.AddLayer, 0), Indexed(VerticalPileStep.RemoveLayer, 0, 1),
            Step(VerticalPileStep.AddSurvey), Indexed(VerticalPileStep.RemoveSurvey, 0), new(VerticalPileStep.CopySurvey, Text: "nuova", Index: 0),
            new(VerticalPileStep.Curve, "progetto_drenante_compressione"), Step(VerticalPileStep.NoCurves),
            Indexed(VerticalPileStep.CapacityView, 2), Indexed(VerticalPileStep.Table, 1),
            calculate, commit]));
        list.Add(new("calcolo-sullo-sfondo", Pile, "regressione-palo_storico_0", [
            wait,
            hold, Field("lunghezza", "13"), entered, Field("lunghezza", "14"), release, wait,
            hold, Step(VerticalPileStep.Start), wait,
            Field("diametro", "1,05"), entered, Step(VerticalPileStep.Start), release, wait,
            hold, Field("diametro", "1,1"), entered, reopen, release, wait, commit]));
        return list;
    }
}

/// <summary>A field of a form as the user sees it: kind, label, value, choices and state of the row.</summary>
internal sealed record VerticalPileFieldState(string Key, string Kind, string Label, string Value, IReadOnlyList<string>? Choices, bool Enabled, bool Dimmed, bool Visible)
{
    internal string Line() => Kind + " " + Key + " " + VerticalPileTrace.Quote(Label) + " = " + VerticalPileTrace.Quote(Value)
        + (Choices is null ? "" : " voci " + VerticalPileTrace.Quote(string.Join("|", Choices)))
        + (Enabled ? "" : " · spento") + (Dimmed ? " · attenuato" : "") + (Visible ? "" : " · nascosto");
}

/// <summary>A series of a chart: name, colour (#AARRGGBB), dashes, emphasis and the points (through their hash).</summary>
internal sealed record VerticalPileSeriesState(string Name, string Color, bool Dashed, bool Highlighted, string? Dash, IReadOnlyList<double[]> Points)
{
    internal string Line() => VerticalPileTrace.Quote(Name) + " " + Color + (Dashed ? " tratteggio" : "") + (Highlighted ? " evidenza" : "")
        + (Dash is null ? "" : " " + Dash) + " punti " + Points.Count.ToString(CultureInfo.InvariantCulture) + " " + VerticalPileTrace.Hash(Points);
}

/// <summary>A marker of a chart.</summary>
internal sealed record VerticalPileMarkerState(double X, double Y, string Label, string Color, bool ProjectToAxes, string? XCaption, string? YCaption, double Radius)
{
    internal string Line() => VerticalPileTrace.Number(X) + " " + VerticalPileTrace.Number(Y) + " " + VerticalPileTrace.Quote(Label) + " " + Color
        + (ProjectToAxes ? " proiettato" : "") + " " + VerticalPileTrace.Quote(XCaption) + " " + VerticalPileTrace.Quote(YCaption) + " r " + VerticalPileTrace.Number(Radius);
}

/// <summary>What a chart shows: texts, axis limits, series and markers.</summary>
internal sealed class VerticalPileChartState
{
    public string Title = "", XLabel = "", YLabel = "", Note = "", EmptyMessage = "";
    public double CapacityDepth;
    public double? XMinimum;
    public List<VerticalPileSeriesState> Series = [];
    public List<VerticalPileMarkerState> Markers = [];

    internal IEnumerable<string> Lines(string name)
    {
        yield return name + " " + VerticalPileTrace.Quote(Title) + " · x " + VerticalPileTrace.Quote(XLabel) + " · y " + VerticalPileTrace.Quote(YLabel)
            + " · nota " + VerticalPileTrace.Quote(Note) + " · vuoto " + VerticalPileTrace.Quote(EmptyMessage)
            + " · quota " + VerticalPileTrace.Number(CapacityDepth) + " · xmin " + (XMinimum is double x ? VerticalPileTrace.Number(x) : "-");
        foreach (var series in Series) yield return "  serie " + series.Line();
        foreach (var marker in Markers) yield return "  punto " + marker.Line();
    }
}

/// <summary>The table shown in «Tabelle e dettagli»: a note, tables (title, columns, rows already formatted) and a footnote.</summary>
internal sealed class VerticalPileTableState
{
    public string? Note, Footnote;
    public List<(string Title, IReadOnlyList<string> Columns, IReadOnlyList<string[]> Rows)> Tables = [];

    internal IEnumerable<string> Lines()
    {
        if (Note is not null) yield return "  nota " + VerticalPileTrace.Quote(Note);
        foreach (var (title, columns, rows) in Tables)
        {
            yield return "  tabella " + VerticalPileTrace.Quote(title) + " colonne " + VerticalPileTrace.Quote(string.Join("|", columns))
                + " righe " + rows.Count.ToString(CultureInfo.InvariantCulture) + " " + VerticalPileTrace.Hash(rows);
            foreach (var row in rows.Take(3)) yield return "    " + string.Join(" | ", row.Select(VerticalPileTrace.Quote));
        }
        if (Footnote is not null) yield return "  piede " + VerticalPileTrace.Quote(Footnote);
    }
}

/// <summary>
/// The state of the sheet seen by the user after a step: data in memory order, Modified, result, status and warnings, the three
/// forms, the verification, the stratigraphies (rows as the table shows them, layer colour excluded: a token of the view), the
/// profile drawn, the capacity chart with its curves, the reference chart (Nq or abacus), the tables and the JSON of the results.
/// Each side fills it from its own objects; <see cref="Lines"/> writes it in one format for both.
/// </summary>
internal sealed class VerticalPileTraceState
{
    public JsonObject Data = new();
    public int Modified;
    public bool Editable, Exportable;
    public JsonObject? Result;
    public string Status = "", Warnings = "";
    public List<VerticalPileFieldState> General = [], Efficiency = [], Normative = [];
    public string EfficiencyText = "";
    public List<string[]> Verification = [];
    public int SelectedSurvey = -1, ProfileSurvey = -1;
    /// <summary>The rows of every stratigraphy, as the table shows them (the display copy of each layer, colour excluded).</summary>
    public List<List<JsonObject>> Surveys = [];
    public IReadOnlyList<string> CapacityViews = [];
    public int CapacityView = -1;
    public VerticalPileChartState Capacity = new(), Reference = new();
    public string EndValues = "";
    public List<(string Label, bool Checked)> Curves = [];
    public IReadOnlyList<string> Tables = [];
    public int SelectedTable = -1;
    public VerticalPileTableState? Shown;
    public string Raw = "";

    internal IEnumerable<string> Lines(string step)
    {
        yield return "== " + step;
        yield return "dati " + VerticalPileTrace.Compact(Data);
        yield return "modificato " + Modified.ToString(CultureInfo.InvariantCulture) + " · modificabile " + VerticalPileTrace.YesNo(Editable)
            + " · esportabile " + VerticalPileTrace.YesNo(Exportable);
        yield return "risultato " + (Result is null ? "-" : VerticalPileTrace.Hash(Result));
        yield return "stato " + VerticalPileTrace.Quote(Status);
        yield return "avvisi " + VerticalPileTrace.Quote(Warnings);
        yield return "generali";
        foreach (var field in General) yield return "  " + field.Line();
        yield return "efficienza " + VerticalPileTrace.Quote(EfficiencyText);
        foreach (var field in Efficiency) yield return "  " + field.Line();
        yield return "normativa";
        foreach (var field in Normative) yield return "  " + field.Line();
        yield return "verifica " + Verification.Count.ToString(CultureInfo.InvariantCulture);
        foreach (var row in Verification) yield return "  " + string.Join(" | ", row.Select(VerticalPileTrace.Quote));
        yield return "stratigrafie " + Surveys.Count.ToString(CultureInfo.InvariantCulture) + " · selezionata " + SelectedSurvey.ToString(CultureInfo.InvariantCulture)
            + " · profilo " + ProfileSurvey.ToString(CultureInfo.InvariantCulture);
        for (int i = 0; i < Surveys.Count; i++)
        {
            yield return "  stratigrafia " + (i + 1).ToString(CultureInfo.InvariantCulture) + " · strati " + Surveys[i].Count.ToString(CultureInfo.InvariantCulture);
            foreach (var row in Surveys[i]) yield return "    " + VerticalPileTrace.Compact(VerticalPileTrace.WithoutColour(row));
        }
        yield return "viste " + VerticalPileTrace.Quote(string.Join("|", CapacityViews)) + " · scelta " + CapacityView.ToString(CultureInfo.InvariantCulture);
        foreach (string line in Capacity.Lines("grafico")) yield return line;
        yield return "curve " + VerticalPileTrace.Quote(EndValues);
        foreach (var (label, isChecked) in Curves) yield return "  " + VerticalPileTrace.Quote(label) + " = " + VerticalPileTrace.YesNo(isChecked);
        foreach (string line in Reference.Lines("riferimento")) yield return line;
        yield return "tabelle " + VerticalPileTrace.Quote(string.Join("|", Tables)) + " · scelta " + SelectedTable.ToString(CultureInfo.InvariantCulture);
        if (Shown is not null) foreach (string line in Shown.Lines()) yield return line;
        yield return "json " + (Raw.Length <= 60 ? VerticalPileTrace.Quote(Raw) : Raw.Length.ToString(CultureInfo.InvariantCulture) + " " + VerticalPileTrace.Hash(Raw));
    }
}

/// <summary>Formats of the trace, shared by the two sides (the compact JSON, hashes and quotes of the pile group traces).</summary>
internal static class VerticalPileTrace
{
    internal static string Compact(JsonNode? node) => PileGroupTrace.Compact(node);

    internal static string Hash(JsonNode node) => PileGroupTrace.Hash(node);

    /// <summary>Hash of texts or of points, serialized as JSON with the invariant culture.</summary>
    internal static string Hash(object value) => PileGroupTrace.Hash(JsonSerializer.SerializeToNode(value, PileGroupTrace.Json)!);

    internal static string Quote(string? text) => PileGroupTrace.Quote(text);

    internal static string YesNo(bool value) => PileGroupTrace.YesNo(value);

    internal static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>A colour as WPF writes a brush: #AARRGGBB in capitals (the plain series write #RRGGBB).</summary>
    internal static string Colour(string colour) => colour.Length == 7 && colour[0] == '#' ? "#FF" + colour[1..].ToUpperInvariant() : colour.ToUpperInvariant();

    /// <summary>The display row without «__color»: the colour of the layer is a token of the view (Swatch.Soil1–6).</summary>
    internal static JsonObject WithoutColour(JsonObject row)
    {
        var copy = new JsonObject();
        foreach (var (key, value) in row) if (key != "__color") copy[key] = value?.DeepClone();
        return copy;
    }

    internal static void Write(string path, IEnumerable<string> lines) => PileGroupTrace.Write(path, lines);

    internal static string[] Read(string path) => PileGroupTrace.Read(path);

    internal static string? FirstDifference(IReadOnlyList<string> expected, IReadOnlyList<string> actual) => PileGroupTrace.FirstDifference(expected, actual);
}

/// <summary>
/// The calculation service of the traces and of the scheduling checks: the module service, with a barrier. While held, every
/// calculation signals its entry and waits for <see cref="Release"/> (at most 30 s), then completes without observing the
/// cancellation, as the gated service of the F3 scheduling checks.
/// </summary>
internal sealed class HeldModuleService : IModuleService
{
    private readonly IModuleService inner;
    private readonly object sync = new();
    private readonly SemaphoreSlim entered = new(0);
    private ManualResetEventSlim gate = new(true);
    private int calls;

    internal HeldModuleService(string module) => inner = ModuleServices.Get(module);

    public string ModuleId => inner.ModuleId;

    internal int Calls => Volatile.Read(ref calls);

    public JsonObject Prepare(JsonObject source) => inner.Prepare(source);

    public ModuleCalculation Calculate(JsonObject source, CancellationToken cancellation = default)
    {
        Interlocked.Increment(ref calls);
        ManualResetEventSlim current;
        lock (sync) current = gate;
        if (!current.IsSet)
        {
            entered.Release();
            if (!current.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Motore trattenuto dalla traccia e mai rilasciato.");
        }
        return inner.Calculate(source);
    }

    /// <summary>The next calculations wait for <see cref="Release"/>.</summary>
    internal void Hold() { lock (sync) if (gate.IsSet) gate = new ManualResetEventSlim(false); }

    /// <summary>Releases the held calculations; the engine stops holding.</summary>
    internal void Release() { lock (sync) gate.Set(); }

    /// <summary>Completes when one more calculation has entered the held engine (at most 30 s).</summary>
    internal async Task EnteredAsync()
    {
        if (!await entered.WaitAsync(TimeSpan.FromSeconds(30))) throw new TimeoutException("Nessun calcolo è entrato nel motore trattenuto.");
    }
}
