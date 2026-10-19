using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;

namespace Anthea.Presentation.Tests;

/// <summary>
/// One step of a scenario of the shallow foundation sheet (F5.9b), applied in the same way to the historic view
/// (FoundationWorkspace, tests/Desktop/ShallowFoundationHistoricTrace.cs) and to its ViewModel
/// (tests/ANTHEA.Presentation.Tests/ShallowFoundationSheetChecks.cs). A step is what the user does with a control: a control that
/// the user cannot use (hidden, disabled, read-only revision) does nothing. <see cref="Key"/> names the field, the option, the group
/// or the column of the layers; <see cref="Index"/> the survey or the section of the failure mechanism; <see cref="Row"/> the layer;
/// <see cref="Text"/> what the user types or chooses.
/// </summary>
internal sealed record ShallowFoundationStep(string Kind, string? Key = null, string? Text = null, int Index = -1, int Row = -1)
{
    /// <summary>Commit of the sheet (navigation, save).</summary>
    internal const string Commit = "conferma";
    /// <summary>The «Calcola» command (synchronous, as the historic view).</summary>
    internal const string Calculate = "calcola";
    /// <summary>Closes the sheet and opens its data again (a new sheet on a copy, as the window does).</summary>
    internal const string Reopen = "riapri";
    /// <summary>The sheet becomes a read-only revision.</summary>
    internal const string ReadOnly = "sola-lettura";
    /// <summary>Types <see cref="Text"/> in the text field <see cref="Key"/>.</summary>
    internal const string Field = "campo";
    /// <summary>Chooses the item <see cref="Text"/> of the choice <see cref="Key"/>.</summary>
    internal const string Choice = "scelta";
    /// <summary>Clicks the check box <see cref="Key"/>.</summary>
    internal const string Option = "opzione";
    /// <summary>Opens or closes the group of inputs <see cref="Key"/> (metodo, Geometria, Azioni, stratigrafia).</summary>
    internal const string Group = "gruppo";
    /// <summary>The «Raccogli input» / «Mostra input» button.</summary>
    internal const string Collect = "raccogli";
    /// <summary>Chooses the survey <see cref="Index"/>.</summary>
    internal const string Survey = "sondaggio";
    /// <summary>The «+ Sondaggio» button.</summary>
    internal const string AddSurvey = "aggiungi-sondaggio";
    /// <summary>The «− Sondaggio» button; the question is answered yes.</summary>
    internal const string RemoveSurvey = "togli-sondaggio";
    /// <summary>Edits the cell <see cref="Key"/> of the layer <see cref="Row"/>: types or chooses <see cref="Text"/>.</summary>
    internal const string Layer = "strato";
    /// <summary>The «Aggiungi strato» button.</summary>
    internal const string AddLayer = "aggiungi-strato";
    /// <summary>The [−] button of the layer <see cref="Row"/>.</summary>
    internal const string RemoveLayer = "togli-strato";
    /// <summary>Chooses the section <see cref="Index"/> (0 X, 1 Y) of the failure mechanism.</summary>
    internal const string Mechanism = "meccanismo";
    /// <summary>The «Report Word e PDF» button of the sheet.</summary>
    internal const string Report = "report";

    public override string ToString() => Kind + (Key is null ? "" : " " + Key)
        + (Index < 0 && Row < 0 ? "" : " [" + (Index >= 0 ? Index.ToString(CultureInfo.InvariantCulture) : Row.ToString(CultureInfo.InvariantCulture)) + "]")
        + (Text is null ? "" : " «" + Text + "»");
}

/// <summary>A scenario: the data of an F3 fixture of the module, then the steps.</summary>
internal sealed record ShallowFoundationScenario(string Name, string Fixture, IReadOnlyList<ShallowFoundationStep> Steps);

/// <summary>
/// The scenarios of the shallow foundation sheet (F5.9b): for every F3 fixture of the module the path of the F3 capture (opening,
/// commit, calculation, reopening); then edits of every kind (fields, choices, options, concrete, visibility rules, errors of the
/// calculation), the groups and the collapsed inputs that keep the result, the surveys and the layers, and a read-only revision.
/// </summary>
internal static class ShallowFoundationScenarios
{
    internal const string Module = "geo_fondazioni_superficiali";

    /// <summary>Folder of the traces of the historic view, frozen before the migration (proposal 7.2, capture first).</summary>
    internal static string Reference(string root) => Path.Combine(root, "tests", "fixtures", "f5-fondazioni", "storica");

    internal static string FixturePath(string root, string id) => Path.Combine(root, "tests", "fixtures", "f3", "inputs", Module + "--" + id + ".json");

    /// <summary>The data of an F3 fixture of the module, as the F3 capture reads them.</summary>
    internal static JsonObject FixtureData(string root, string id) => JsonNode.Parse(File.ReadAllText(FixturePath(root, id)))!["data"]!.AsObject();

    /// <summary>Identifiers of the F3 fixtures of the module, in ordinal order.</summary>
    internal static IReadOnlyList<string> Fixtures(string root) => Directory.EnumerateFiles(Path.Combine(root, "tests", "fixtures", "f3", "inputs"), Module + "--*.json")
        .Select(f => Path.GetFileNameWithoutExtension(f)[(Module.Length + 2)..]).Order(StringComparer.Ordinal).ToArray();

    /// <summary>The fields of the sheet in the order of the view: the trace writes one line for each.</summary>
    internal static IReadOnlyList<string> FieldKeys { get; } = ["Method", "Shape", "Condition", "Profile", "Width", "Length", "Depth", "Normal",
        "HorizontalX", "HorizontalY", "MomentX", "MomentY", "Surcharge", "presenza_falda", "profondita_falda", "nome_combinazione", "azioni_confermate",
        "Compression", "RelativeDensity", "IncludeDepthFactors", "RoughBase", "WaterUnitWeight", "classe_cls", "fck_mpa", "cls_diagramma"];

    /// <summary>The groups of inputs whose state the sheet saves in «vista_fondazioni», in the order of the view.</summary>
    internal static IReadOnlyList<string> GroupKeys { get; } = ["metodo", "Geometria", "Azioni", "stratigrafia"];

    static ShallowFoundationStep Step(string kind) => new(kind);
    static ShallowFoundationStep Field(string key, string text) => new(ShallowFoundationStep.Field, key, text);
    static ShallowFoundationStep Choice(string key, string text) => new(ShallowFoundationStep.Choice, key, text);
    static ShallowFoundationStep Option(string key) => new(ShallowFoundationStep.Option, key);
    static ShallowFoundationStep Group(string key) => new(ShallowFoundationStep.Group, key);
    static ShallowFoundationStep Layer(int row, string key, string text) => new(ShallowFoundationStep.Layer, key, text, Row: row);
    static ShallowFoundationStep Indexed(string kind, int index) => new(kind, Index: index);

    internal static IReadOnlyList<ShallowFoundationScenario> All(string root)
    {
        ShallowFoundationStep commit = Step(ShallowFoundationStep.Commit), calculate = Step(ShallowFoundationStep.Calculate), reopen = Step(ShallowFoundationStep.Reopen),
            collect = Step(ShallowFoundationStep.Collect), report = Step(ShallowFoundationStep.Report);
        var list = Fixtures(root).Select(id => new ShallowFoundationScenario("fixture-" + id, id, [commit, calculate, commit, reopen, commit])).ToList();
        list.Add(new("modifiche", "confirmed-actions", [
            commit, calculate,
            Field("Width", "2,5"), calculate,
            Field("MomentX", "15"), Field("HorizontalY", "abc"), calculate,
            Field("HorizontalY", "0"),
            Choice("Shape", "Nastriforme · azioni per metro"),
            Field("Length", "4"),
            Choice("Method", "Meyerhof"),
            Option("IncludeDepthFactors"),
            Choice("Compression", "Riduzione Vesić · densità relativa"), Field("RelativeDensity", "0,4"),
            Option("presenza_falda"), Field("profondita_falda", "0,7"), Field("WaterUnitWeight", "10"),
            Field("nome_combinazione", ""), calculate,
            Field("nome_combinazione", "SLU · prova"),
            Option("azioni_confermate"), calculate,
            Choice("Profile", "Confronto caratteristico"), calculate,
            Choice("Condition", "Non drenata"),
            Choice("Shape", "Circolare · carico centrato"),
            Field("MomentX", "0"), Field("Surcharge", "12"),
            Choice("classe_cls", "C35/45"), Field("fck_mpa", "33"), Choice("cls_diagramma", "Bilineare"), Choice("classe_cls", "C20/25"),
            Choice("Condition", "Drenata"), Choice("Method", "Eurocodice 7 · allegato D"), Option("RoughBase"), calculate, Option("RoughBase"),
            calculate, commit, reopen, commit]));
        list.Add(new("gruppi-e-raccolta", "meyerhof-eccentrica-input-raccolti", [
            calculate, collect, Group("metodo"), Group("Geometria"), Group("stratigrafia"),
            Indexed(ShallowFoundationStep.Mechanism, 1), Indexed(ShallowFoundationStep.Mechanism, 0), report,
            collect, Group("Azioni"), Field("Normal", "750"), Group("Azioni"), collect, calculate, commit, reopen, commit]));
        list.Add(new("stratigrafia", "vesic-circolare-due-sondaggi", [
            calculate,
            Indexed(ShallowFoundationStep.Survey, 0),
            Layer(1, "angolo_attrito", "31"),
            Layer(0, "tipologia", "Coesivo"),
            Layer(1, "peso_specifico_saturo", ""),
            Step(ShallowFoundationStep.AddLayer), Layer(2, "spessore", "5"),
            new(ShallowFoundationStep.RemoveLayer, Row: 1),
            Option("strato_portante_semiinfinito"), calculate, Option("strato_portante_semiinfinito"),
            Step(ShallowFoundationStep.AddSurvey),
            Step(ShallowFoundationStep.RemoveSurvey),
            Indexed(ShallowFoundationStep.Survey, 0), Step(ShallowFoundationStep.RemoveSurvey),
            Step(ShallowFoundationStep.RemoveSurvey),
            calculate, commit, reopen, commit]));
        list.Add(new("sola-lettura", "hansen-non-drenato-caratteristico", [
            calculate, Step(ShallowFoundationStep.ReadOnly),
            Field("Width", "3"), Choice("Method", "Vesić"), Option("IncludeDepthFactors"), Option("presenza_falda"), Field("profondita_falda", "1"),
            Field("nome_combinazione", "x"), Choice("classe_cls", "C20/25"), Field("fck_mpa", "40"),
            Step(ShallowFoundationStep.AddSurvey), Step(ShallowFoundationStep.RemoveSurvey), Indexed(ShallowFoundationStep.Survey, 0),
            Layer(1, "spessore", "8"), Step(ShallowFoundationStep.AddLayer), new(ShallowFoundationStep.RemoveLayer, Row: 0),
            Option("strato_portante_semiinfinito"),
            Group("Azioni"), collect, collect,
            Indexed(ShallowFoundationStep.Mechanism, 1), report,
            calculate, commit]));
        return list;
    }
}

/// <summary>A field as the user sees it: kind (campo, scelta, opzione), label, unit, value, items and visibility.</summary>
internal sealed record ShallowFoundationFieldState(string Key, string Kind, string Label, string Unit, string Value, IReadOnlyList<string>? Choices, bool Visible)
{
    internal string Line() => Kind + " " + Key + " " + PileGroupTrace.Quote(Label) + (Unit.Length == 0 ? "" : " [" + Unit + "]") + " = " + PileGroupTrace.Quote(Value)
        + (Choices is null ? "" : " voci " + PileGroupTrace.Quote(string.Join("|", Choices))) + (Visible ? "" : " · nascosto");
}

/// <summary>
/// The state of the sheet seen by the user after a step: data in memory order, Modified, result, status, fields, groups, surveys
/// and layers, results (headline, summary, tables, notes) and the six graphic tabs as data. Each side fills it from its own
/// objects; <see cref="Lines"/> writes it in one format for both.
/// </summary>
internal sealed class ShallowFoundationTraceState
{
    public JsonObject Data = new();
    public int Modified, ReportRequests;
    public bool Editable;
    public JsonObject? Result;
    public string State = "";
    public List<ShallowFoundationFieldState> Fields = [];
    /// <summary>Groups of inputs (key, open) in the order of <see cref="ShallowFoundationScenarios.GroupKeys"/>.</summary>
    public List<(string Key, bool Open)> Groups = [];
    public bool InputsCollapsed;
    public string CollectText = "";
    public IReadOnlyList<string> Surveys = [];
    public int SelectedSurvey = -1;
    /// <summary>The rows of the layers as the table shows them (the display copy of each layer, colour excluded).</summary>
    public List<JsonObject> Layers = [];
    public bool HalfSpace;
    /// <summary>The text of the results without a result («Verifica da calcolare…»), or the headline of the result.</summary>
    public string Headline = "";
    public bool HasDetails;
    public List<string> Summary = [];
    public IReadOnlyList<string> CoefficientColumns = [], AdditiveColumns = [];
    public List<string[]> Coefficients = [];
    public string AdditiveTitle = "";
    public List<string[]> Additive = [];
    public List<string> Notes = [];
    /// <summary>The drawings of the «Geometria» tab and the «Stratigrafia» tab show the data of the sheet.</summary>
    public bool InputDrawings, SoilDrawing;
    /// <summary>The message of the four tabs of the results when they show no drawing; null when they show the result.</summary>
    public string? GraphicsMessage;
    public JsonObject? Graphics;
    public string PressureText = "", ContactText = "", MechanismText = "";
    public IReadOnlyList<string> MechanismAxes = [];
    public int MechanismAxis = -1;
    public JsonObject? Mechanism;

    internal IEnumerable<string> Lines(string step, bool compareFrozenVersion = false)
    {
        static string Q(string? text) => PileGroupTrace.Quote(text);
        static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
        yield return "== " + step;
        yield return "dati " + PileGroupTrace.Compact(Data);
        yield return "modificato " + N(Modified) + " · modificabile " + PileGroupTrace.YesNo(Editable) + " · richieste report " + N(ReportRequests);
        yield return "risultato " + (Result is null ? "-" : PileGroupTrace.Hash(compareFrozenVersion ? ShallowFoundationVersionProjection.Result(Result)! : Result));
        yield return "stato " + Q(State);
        yield return "campi";
        foreach (var field in Fields) yield return "  " + field.Line();
        yield return "gruppi " + string.Join(" ", Groups.Select(g => g.Key + "=" + (g.Open ? "aperto" : "chiuso")))
            + " · input raccolti " + PileGroupTrace.YesNo(InputsCollapsed) + " · pulsante " + Q(CollectText);
        yield return "sondaggi " + Q(string.Join("|", Surveys)) + " · scelto " + N(SelectedSurvey) + " · semispazio " + PileGroupTrace.YesNo(HalfSpace);
        foreach (var row in Layers) yield return "  " + PileGroupTrace.Compact(row);
        yield return "risultati " + Q(Headline) + (HasDetails ? "" : " · senza dettagli");
        if (HasDetails)
        {
            foreach (string line in Summary) yield return "  riepilogo " + Q(line);
            yield return "  coefficienti " + Q(string.Join("|", CoefficientColumns)) + " " + N(Coefficients.Count);
            foreach (var row in Coefficients) yield return "    " + string.Join(" | ", row.Select(Q));
            if (AdditiveTitle.Length > 0 || Additive.Count > 0)
            {
                yield return "  additive " + Q(AdditiveTitle) + " " + Q(string.Join("|", AdditiveColumns)) + " " + N(Additive.Count);
                foreach (var row in Additive) yield return "    " + string.Join(" | ", row.Select(Q));
            }
            foreach (string note in Notes) yield return "  nota " + Q(compareFrozenVersion ? ShallowFoundationVersionProjection.Text(note) : note);
        }
        yield return "grafici geometria " + PileGroupTrace.YesNo(InputDrawings) + " · stratigrafia " + PileGroupTrace.YesNo(SoilDrawing);
        if (GraphicsMessage is not null) yield return "  risultati " + Q(GraphicsMessage);
        else
        {
            yield return "  pianta " + (Graphics is null ? "-" : PileGroupTrace.Hash(Graphics));
            yield return "  tensioni " + Q(PressureText);
            yield return "  andamenti " + Q(ContactText);
            yield return "  rottura " + Q(string.Join("|", MechanismAxes)) + " [" + N(MechanismAxis) + "] " + (Mechanism is null ? "-" : PileGroupTrace.Hash(Mechanism)) + " " + Q(MechanismText);
        }
    }

    /// <summary>SPETTRI-04: Geotechnics 0.4.0.1 changes only assembly metadata, already checked by the full F3 comparison.
    /// Preserve the frozen F5 traces by mapping that exact version to their 0.4.0.0. Every other field, and every other
    /// version, still contributes unchanged to the fingerprint. Never mutate the actual result.</summary>
    internal static string ResultFingerprint(JsonObject result)
    {
        if (result["library_version"]?.GetValue<string>() != "0.4.0.1") return PileGroupTrace.Hash(result);
        var comparable = (JsonObject)result.DeepClone();
        comparable["library_version"] = "0.4.0.0";
        return PileGroupTrace.Hash(comparable);
    }

    /// <summary>The display row without «__color»: the colour of the layer is a token of the view.</summary>
    internal static JsonObject WithoutColour(JsonObject row)
    {
        var copy = new JsonObject();
        foreach (var (key, value) in row) if (key != "__color") copy[key] = value?.DeepClone();
        return copy;
    }
}

/// <summary>PARATIE-02: compare numerical/UI content against the immutable F5 capture while checking
/// runtime provenance separately. Only the declared library_version field and its exact label change.
/// Historic captures use Lines without this projection and retain their real versions.</summary>
internal static class ShallowFoundationVersionProjection
{
    internal const string FrozenVersion = "0.4.0.0";
    private static string RuntimeVersion => typeof(GPC.Checkers.Geotechnics.Foundations.ShallowFoundationSeismic).Assembly.GetName().Version!.ToString();
    internal static JsonObject? Result(JsonObject? value)
    {
        if (value is null) return null;
        if (value["library_version"]?.ToString() != RuntimeVersion) throw new InvalidOperationException("Provenienza del risultato Fondazioni diversa dalla libreria caricata.");
        var copy = value.DeepClone().AsObject(); copy["library_version"] = FrozenVersion; return copy;
    }
    internal static string Text(string value) => value.Replace("Libreria GPCChecker.Geotechnics " + RuntimeVersion,
        "Libreria GPCChecker.Geotechnics " + FrozenVersion, StringComparison.Ordinal);
}
