using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthea.Application.Presentation;

namespace Anthea.Presentation.Tests;

/// <summary>
/// One step of a scenario of the horizontal pile and micropile sheet (F5.7), applied in the same way to the historic view
/// (HorizontalWorkspace, tests/Desktop/HorizontalPileHistoricTrace.cs) and to its ViewModel (tests/ANTHEA.Presentation.Tests/
/// HorizontalPileSheetChecks.cs). A step is what the user does with a control: a control that the user cannot use (disabled,
/// hidden, read-only revision) does nothing. <see cref="Key"/> names the field, the column or the item of the soil editor;
/// <see cref="Index"/> the tab of the analyses or the stratigraphy; <see cref="Row"/> the layer; <see cref="Text"/> what the user
/// types or chooses.
/// </summary>
internal sealed record HorizontalPileStep(string Kind, string? Key = null, string? Text = null, int Index = -1, int Row = -1)
{
    /// <summary>Commit of the sheet (navigation, save).</summary>
    internal const string Commit = "conferma";
    /// <summary>The «Calcola» command of the window (commit, then calculation), awaited.</summary>
    internal const string Calculate = "calcola";
    /// <summary>Starts the «Calcola» command of the window without awaiting it.</summary>
    internal const string Start = "avvia-calcolo";
    /// <summary>Waits for the last calculation of the capacity requested: the automatic one (450 ms after the last edit) or the started one.</summary>
    internal const string Wait = "attendi";
    /// <summary>Closes the sheet and opens its data again (a new sheet on a copy, as the window does).</summary>
    internal const string Reopen = "riapri";
    /// <summary>The sheet becomes a read-only revision.</summary>
    internal const string ReadOnly = "sola-lettura";
    /// <summary>Types <see cref="Text"/> in the text field <see cref="Key"/> of a form.</summary>
    internal const string Field = "campo";
    /// <summary>Chooses <see cref="Text"/> in the choice <see cref="Key"/> of a form.</summary>
    internal const string Choice = "scelta";
    /// <summary>Clicks the check box <see cref="Key"/> of a form.</summary>
    internal const string Option = "opzione";
    /// <summary>Selects the tab <see cref="Index"/> of the analyses: 0 capacity (Broms / stratified), 1 elastic response.</summary>
    internal const string Analysis = "scheda";
    /// <summary>Selects the tab of the stratigraphy <see cref="Index"/>.</summary>
    internal const string Survey = "stratigrafia";
    /// <summary>«+ Stratigrafia»: a new stratigraphy with one layer, selected.</summary>
    internal const string AddSurvey = "aggiungi-stratigrafia";
    /// <summary>The [−] button of the tab of the stratigraphy <see cref="Index"/>, confirmed.</summary>
    internal const string RemoveSurvey = "togli-stratigrafia";
    /// <summary>«− Stratigrafia»: removes the selected stratigraphy, confirmed.</summary>
    internal const string RemoveSelectedSurvey = "togli-selezionata";
    /// <summary>Selects the stratigraphy <see cref="Index"/>, then «Copia in…» and the destination <see cref="Text"/> (an index, or «nuova»), confirmed.</summary>
    internal const string CopySurvey = "copia-in";
    /// <summary>Selects the stratigraphy <see cref="Index"/>, then «Copia da…» and the source <see cref="Text"/> (an index), confirmed.</summary>
    internal const string CopyFrom = "copia-da";
    /// <summary>Selects the stratigraphy <see cref="Index"/>, then types or chooses <see cref="Text"/> in the cell <see cref="Key"/> of the layer <see cref="Row"/>.</summary>
    internal const string Layer = "strato";
    /// <summary>Selects the stratigraphy <see cref="Index"/>, then «Aggiungi strato».</summary>
    internal const string AddLayer = "aggiungi-strato";
    /// <summary>Selects the stratigraphy <see cref="Index"/>, then the [−] button of the layer <see cref="Row"/>.</summary>
    internal const string RemoveLayer = "togli-strato";
    /// <summary>
    /// Selects the stratigraphy <see cref="Index"/>, then uses the soil editor of the layer <see cref="Row"/> (horizontal soil reaction):
    /// <see cref="SoilCategory"/> or <see cref="SoilLaw"/> choose <see cref="Text"/>, <see cref="SoilValue"/> and <see cref="SoilNh"/> type
    /// <see cref="Text"/>, <see cref="SoilOverride"/> clicks the «nh*» check box.
    /// </summary>
    internal const string Soil = "terreno";
    internal const string SoilCategory = "categoria";
    internal const string SoilLaw = "legge";
    internal const string SoilValue = "valore";
    internal const string SoilOverride = "override";
    internal const string SoilNh = "nh_override";
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
internal sealed record HorizontalPileScenario(string Name, string Module, string Fixture, IReadOnlyList<HorizontalPileStep> Steps);

/// <summary>
/// The scenarios of the horizontal pile and micropile sheet (F5.7), on the tab of the capacity (the elastic response is F5.8): for
/// every F3 fixture of the two modules the path of the F3 capture (opening, commit, the visit of the tabs of the analyses,
/// calculation, reopening); then edits of every kind on the pile and on the micropile, the stratigraphies and the soil editor, a
/// read-only revision, the tab of the elastic response, and the scheduling of the background calculation with a held engine
/// (cancellation, publication of the current revision only, «Calcola» and closing during a calculation).
/// </summary>
internal static class HorizontalPileScenarios
{
    internal const string Pile = "geo_palo_orizzontale";
    internal const string Micro = "geo_micropalo_orizzontale";

    /// <summary>Folder of the traces of the historic view, frozen before the migration (proposal 7.2, capture first).</summary>
    internal static string Reference(string root) => Path.Combine(root, "tests", "fixtures", "f5-palo-orizzontale", "storica");

    internal static string FixturePath(string root, string module, string id) => Path.Combine(root, "tests", "fixtures", "f3", "inputs", module + "--" + id + ".json");

    /// <summary>The data of an F3 fixture, as the F3 capture reads them.</summary>
    internal static JsonObject FixtureData(string root, string module, string id) => JsonNode.Parse(File.ReadAllText(FixturePath(root, module, id)))!["data"]!.AsObject();

    /// <summary>Identifiers of the F3 fixtures of <paramref name="module"/>, in ordinal order.</summary>
    internal static IReadOnlyList<string> Fixtures(string root, string module) => Directory.EnumerateFiles(Path.Combine(root, "tests", "fixtures", "f3", "inputs"), module + "--*.json")
        .Select(f => Path.GetFileNameWithoutExtension(f)[(module.Length + 2)..]).Order(StringComparer.Ordinal).ToArray();

    /// <summary>True when the fixture opens on the tab of the elastic response (F5.8): its F3 result is the elastic one.</summary>
    internal static bool OpensElastic(JsonObject data) => data["vista_orizzontale"]?.ToString() == "elastico";

    static HorizontalPileStep Step(string kind) => new(kind);
    static HorizontalPileStep Field(string key, string text) => new(HorizontalPileStep.Field, key, text);
    static HorizontalPileStep Choice(string key, string text) => new(HorizontalPileStep.Choice, key, text);
    static HorizontalPileStep Option(string key) => new(HorizontalPileStep.Option, key);
    static HorizontalPileStep Tab(int index) => new(HorizontalPileStep.Analysis, Index: index);
    static HorizontalPileStep Layer(int survey, int row, string key, string text) => new(HorizontalPileStep.Layer, key, text, survey, row);
    static HorizontalPileStep Soil(int survey, int row, string key, string? text = null) => new(HorizontalPileStep.Soil, key, text, survey, row);
    static HorizontalPileStep Indexed(string kind, int index, int row = -1) => new(kind, Index: index, Row: row);

    internal static IReadOnlyList<HorizontalPileScenario> All(string root)
    {
        HorizontalPileStep commit = Step(HorizontalPileStep.Commit), calculate = Step(HorizontalPileStep.Calculate), wait = Step(HorizontalPileStep.Wait),
            reopen = Step(HorizontalPileStep.Reopen), hold = Step(HorizontalPileStep.Hold), entered = Step(HorizontalPileStep.Entered), release = Step(HorizontalPileStep.Release);
        var list = new List<HorizontalPileScenario>();
        foreach (var (module, prefix) in new[] { (Pile, "palo"), (Micro, "micropalo") })
            foreach (string id in Fixtures(root, module))
            {
                // The F3 visit selects every tab of the analyses and comes back; a fixture on the elastic tab (F5.8) is brought to the
                // capacity, where the trace follows it.
                IReadOnlyList<HorizontalPileStep> steps = OpensElastic(FixtureData(root, module, id))
                    ? [commit, Tab(0), wait, calculate, commit, reopen, commit, wait]
                    : [commit, Tab(1), Tab(0), wait, calculate, commit, reopen, commit, wait];
                list.Add(new HorizontalPileScenario(prefix + "-" + id, module, id, steps));
            }
        list.Add(new("modifiche-palo", Pile, "single-soil-layer", [
            wait,
            Field("diametro", "1,2"), wait,
            Field("lunghezza", "8"), wait,
            Field("eccentricita", "0,5"), wait,
            Choice("vincolo", "Impedita"), wait,
            Field("eccentricita", "0"), wait,
            Field("azione_orizzontale", "250"), Field("azione_assiale", "300"), wait,
            Option("presenza_falda"), wait,
            Field("profondita_falda", "2"), Field("gamma_acqua", "10"), wait,
            Choice("metodo_calcolo", "Stratificato"), Field("passo", "0,2"), Field("tolleranza", "1e-6"), wait,
            Choice("metodo_calcolo", "Broms"), wait,
            Choice("verticali_indagate", "4"), wait,
            Choice("efficienza_metodo", "Reese & Van Impe (foglio)"), wait,
            Field("interasse_anteriore", "3"), Field("interasse_posteriore", "3"), Field("interasse_sinistro", "2,5"), Field("interasse_destro", "3"), wait,
            Choice("efficienza_metodo", "Manuale"), Field("efficienza_eta", "0,9"), wait,
            Choice("origine_momento", "Manuale"), Field("momento_resistente", "800"), Field("provenienza_momento", "Prova di carico"), wait,
            Choice("origine_momento", "Sezione c.a."), wait,
            Choice("classe_cls", "C30/37"), wait,
            Choice("classe_acciaio", "B450A"), wait,
            Option("coefficienti_unitari"), wait,
            Choice("longitudinal_bar_diameter_mm", "20"), Field("longitudinal_bar_count", "20"), wait,
            Option("second_inner_enabled"), wait,
            Field("second_inner_count", "10"), wait,
            Option("second_inner_enabled"), wait,
            Choice("steel_diagramma", "Incrudente"), Field("steel_fu_mpa", "560"), wait,
            Field("cover_mm", "50"), Choice("circular_sides", "64"), Choice("vita_durabilita", "100"), wait,
            Option("ej_override"), Field("ej_assegnato", "250000"), Field("ej_motivo", "Prova"), wait,
            Option("ej_override"), wait,
            Field("diametro", "abc"), wait,
            Field("diametro", "1"), Field("azione_assiale", ""), wait,
            calculate, commit, reopen, commit, wait]));
        list.Add(new("modifiche-micropalo", Micro, "single-soil-layer", [
            wait,
            Choice("modo_chs", "Manuale"), wait,
            Field("diametro_chs_mm", "168,3"), Field("spessore_chs_mm", "8"), wait,
            Field("fy_chs_mpa", "275"), Field("gamma_m0", "1,1"), wait,
            Field("modulo_chs_mpa", "200000"), Field("gamma_acciaio", "77"), Field("gamma_iniezione", "20"), wait,
            Field("spessore_chs_mm", ""), wait,
            Choice("modo_chs", "Catalogo"), wait,
            Choice("profilo_chs", "CHS 219.1 × 10"), wait,
            Choice("vincolo", "Impedita"), Field("diametro", "0,3"), wait,
            Choice("origine_momento", "Manuale"), Field("momento_resistente", "60"), wait,
            Choice("origine_momento", "Sezione CHS"), wait,
            Choice("metodo_calcolo", "Stratificato"), wait,
            calculate, commit, reopen, commit, wait]));
        var clay = HorizontalSoilPresentation.Prepare(new JsonObject { ["tipologia"] = "Coesivo" });
        var sand = HorizontalSoilPresentation.Prepare(new JsonObject { ["tipologia"] = "Granulare" });
        list.Add(new("stratigrafie-palo", Pile, "broms-coesivo-impedita", [
            wait,
            Indexed(HorizontalPileStep.Survey, 1),
            Layer(1, 0, "spessore", "3,5"), wait,
            Layer(0, 0, "tipologia", "Granulare"), wait,
            Layer(0, 0, "angolo_attrito", "30"), wait,
            Layer(0, 0, "tipologia", "Coesivo"), wait,
            Layer(0, 1, "coesione_non_drenata", ""), wait,
            Layer(0, 1, "coesione_non_drenata", "85"), wait,
            Soil(0, 0, HorizontalPileStep.SoilCategory, clay.Categories[^1]), wait,
            Soil(0, 0, HorizontalPileStep.SoilLaw, clay.Choices[^1].Label), wait,
            Soil(0, 0, HorizontalPileStep.SoilValue, "25000"), wait,
            Soil(0, 1, HorizontalPileStep.SoilLaw, clay.Choices[^2].Label), wait,
            Indexed(HorizontalPileStep.AddLayer, 0), wait,
            Layer(0, 2, "tipologia", "Coesivo"), Layer(0, 2, "spessore", "4"), Layer(0, 2, "coesione_non_drenata", "120"), wait,
            Indexed(HorizontalPileStep.RemoveLayer, 0, 1), wait,
            Step(HorizontalPileStep.AddSurvey), wait,
            Soil(2, 0, HorizontalPileStep.SoilCategory, sand.Categories[^1]), wait,
            Soil(2, 0, HorizontalPileStep.SoilLaw, sand.Choices[1].Label), wait,
            Soil(2, 0, HorizontalPileStep.SoilOverride), wait,
            Soil(2, 0, HorizontalPileStep.SoilNh, "4500"), wait,
            Soil(2, 0, HorizontalPileStep.SoilOverride), wait,
            Layer(2, 0, "spessore", "12"), Layer(2, 0, "peso_specifico", "18"), Layer(2, 0, "peso_specifico_saturo", "20"), Layer(2, 0, "angolo_attrito", "31"), wait,
            new(HorizontalPileStep.CopySurvey, Text: "1", Index: 0), wait,
            new(HorizontalPileStep.CopySurvey, Text: "nuova", Index: 1), wait,
            new(HorizontalPileStep.CopyFrom, Text: "0", Index: 3), wait,
            Indexed(HorizontalPileStep.RemoveSurvey, 3), wait,
            Indexed(HorizontalPileStep.Survey, 2), Step(HorizontalPileStep.RemoveSelectedSurvey), wait,
            Indexed(HorizontalPileStep.Survey, 0),
            calculate, commit, reopen, commit, wait]));
        list.Add(new("sola-lettura", Pile, "stratificato-misto-manuale", [
            wait, Step(HorizontalPileStep.ReadOnly),
            Field("diametro", "2"), Choice("vincolo", "Impedita"), Option("presenza_falda"), Choice("metodo_calcolo", "Broms"),
            Choice("efficienza_metodo", "Reese & Van Impe (foglio)"), Field("momento_resistente", "100"), Choice("classe_cls", "C30/37"),
            Option("ej_override"),
            Layer(0, 0, "spessore", "5"), Indexed(HorizontalPileStep.AddLayer, 0), Indexed(HorizontalPileStep.RemoveLayer, 0, 1),
            Soil(0, 0, HorizontalPileStep.SoilCategory, sand.Categories[0]), Soil(0, 1, HorizontalPileStep.SoilValue, "1"),
            Step(HorizontalPileStep.AddSurvey), Indexed(HorizontalPileStep.RemoveSurvey, 0), new(HorizontalPileStep.CopySurvey, Text: "nuova", Index: 0),
            Tab(1), Tab(0), wait,
            calculate, commit]));
        list.Add(new("scheda-elastica", Micro, "stratificato-chs-catalogo", [
            wait,
            Tab(1), Tab(0),
            Field("azione_orizzontale", "45"), Tab(1), wait, Tab(0), wait,
            reopen, wait]));
        list.Add(new("calcolo-sullo-sfondo", Pile, "single-soil-layer", [
            wait,
            hold, Field("lunghezza", "11"), entered, Field("lunghezza", "10"), release, wait,
            hold, Field("diametro", "1,05"), entered, Step(HorizontalPileStep.Start), release, wait,
            hold, Field("diametro", "1,1"), entered, Tab(1), release, wait, Tab(0), wait,
            hold, Field("diametro", "1,15"), entered, reopen, release, wait, commit]));
        return list;
    }
}

/// <summary>A group of fields of a form (the GroupFields of InputForm): title, keys and whether it is open.</summary>
internal sealed record HorizontalPileGroupState(string Title, IReadOnlyList<string> Keys, bool Expanded)
{
    internal string Line() => "gruppo " + HorizontalPileTrace.Quote(Title) + " " + string.Join("|", Keys) + (Expanded ? " · aperto" : "");
}

/// <summary>A form: its fields as the user sees them (<see cref="VerticalPileFieldState"/>, the same line) and its groups.</summary>
internal sealed record HorizontalPileFormState(string Name, IReadOnlyList<VerticalPileFieldState> Fields, IReadOnlyList<HorizontalPileGroupState> Groups);

/// <summary>The soil editor of a layer (horizontal soil reaction), as the cells of the table show it.</summary>
internal sealed record HorizontalSoilState(IReadOnlyList<string> Categories, string Category, IReadOnlyList<string> Laws, int Law, string Parameter,
    string Minimum, string Maximum, string Value, bool ValueEditable, bool OverrideShown, bool Override, string? Nh, string Description)
{
    internal string Line() => "terreno " + HorizontalPileTrace.Quote(Category) + " di " + HorizontalPileTrace.Quote(string.Join("|", Categories))
        + " · legge " + Law.ToString(CultureInfo.InvariantCulture) + " di " + HorizontalPileTrace.Quote(string.Join("|", Laws))
        + " · " + HorizontalPileTrace.Quote(Parameter) + " " + HorizontalPileTrace.Quote(Minimum) + " " + HorizontalPileTrace.Quote(Maximum)
        + " · valore " + HorizontalPileTrace.Quote(Value) + (ValueEditable ? "" : " tabellato")
        + (OverrideShown ? " · nh* " + HorizontalPileTrace.YesNo(Override) + (Nh is null ? "" : " " + HorizontalPileTrace.Quote(Nh)) : "")
        + " · info " + HorizontalPileTrace.Hash((object)Description);
}

/// <summary>A table of the results (title, columns and rows already formatted).</summary>
internal sealed record HorizontalPileTableState(string Title, IReadOnlyList<string> Columns, IReadOnlyList<string[]> Rows)
{
    internal IEnumerable<string> Lines()
    {
        yield return "  tabella " + HorizontalPileTrace.Quote(Title) + " colonne " + HorizontalPileTrace.Quote(string.Join("|", Columns))
            + " righe " + Rows.Count.ToString(CultureInfo.InvariantCulture) + " " + HorizontalPileTrace.Hash(Rows);
        foreach (var row in Rows.Take(2)) yield return "    " + string.Join(" | ", row.Select(HorizontalPileTrace.Quote));
    }
}

/// <summary>What «Diagrammi e dettagli» shows for one survey: its tables and the line under them.</summary>
internal sealed record HorizontalPileSurveyResultState(IReadOnlyList<HorizontalPileTableState> Tables, string Info);

/// <summary>
/// The state of the sheet seen by the user after a step, on the tab of the capacity: data in memory order, Modified, the tab of the
/// analyses, the result of the capacity, status, summary, moment, model and warnings, the seven forms, the verification, the
/// stratigraphies (rows as the table shows them, layer colour excluded: a token of the view) with the soil editors of the selected one,
/// the section drawn, and what «Diagrammi e dettagli» shows. Each side fills it from its own objects; <see cref="Lines"/> writes it in
/// one format for both. The diagrams are compared elsewhere (tests/fixtures/f5-palo-orizzontale/README.md).
/// </summary>
internal sealed class HorizontalPileTraceState
{
    public JsonObject Data = new();
    public int Modified;
    public bool Editable, Exportable, ResultsShown;
    public int Analysis;
    public JsonObject? Result;
    public string Status = "", Summary = "", Moment = "", Model = "", Warnings = "";
    public string? MomentToolTip, ModelHint;
    public List<HorizontalPileFormState> Forms = [];
    public List<string[]> Verification = [];
    public string Outcome = "", OutcomeKind = "";
    public int SelectedSurvey = -1, ProfileSurvey = -1;
    /// <summary>The rows of every stratigraphy, as the table shows them (the display copy of each layer, colour excluded).</summary>
    public List<List<JsonObject>> Surveys = [];
    /// <summary>The soil editors of the rows of the selected stratigraphy, null where the row has none.</summary>
    public List<HorizontalSoilState?> Soils = [];
    /// <summary>The c.a. section drawn: outline, bars, link radius and diameter, axes, caption; null for the micropile.</summary>
    public string? ConcreteSection;
    /// <summary>The CHS drawn (its properties), null without or for the pile.</summary>
    public JsonObject? Chs;
    public List<HorizontalPileSurveyResultState> Results = [];

    internal IEnumerable<string> Lines(string step)
    {
        yield return "== " + step;
        yield return "dati " + HorizontalPileTrace.Compact(Data);
        yield return "modificato " + Modified.ToString(CultureInfo.InvariantCulture) + " · modificabile " + HorizontalPileTrace.YesNo(Editable)
            + " · esportabile " + HorizontalPileTrace.YesNo(Exportable) + " · dettagli " + HorizontalPileTrace.YesNo(ResultsShown)
            + " · scheda " + Analysis.ToString(CultureInfo.InvariantCulture);
        yield return "risultato " + (Result is null ? "-" : HorizontalPileTrace.Hash(Result));
        yield return "stato " + HorizontalPileTrace.Quote(Status);
        yield return "sintesi " + HorizontalPileTrace.Quote(Summary);
        yield return "momento " + HorizontalPileTrace.Quote(Moment) + " · nota " + HorizontalPileTrace.Quote(MomentToolTip);
        yield return "modello " + HorizontalPileTrace.Quote(Model) + " · nota " + HorizontalPileTrace.Quote(ModelHint);
        yield return "avvisi " + HorizontalPileTrace.Quote(Warnings);
        foreach (var form in Forms)
        {
            yield return "form " + form.Name;
            foreach (var field in form.Fields) yield return "  " + field.Line();
            foreach (var group in form.Groups) yield return "  " + group.Line();
        }
        yield return "verifica " + Verification.Count.ToString(CultureInfo.InvariantCulture) + " · " + HorizontalPileTrace.Quote(Outcome) + " " + OutcomeKind;
        foreach (var row in Verification) yield return "  " + string.Join(" | ", row.Select(HorizontalPileTrace.Quote));
        yield return "stratigrafie " + Surveys.Count.ToString(CultureInfo.InvariantCulture) + " · selezionata " + SelectedSurvey.ToString(CultureInfo.InvariantCulture)
            + " · profilo " + ProfileSurvey.ToString(CultureInfo.InvariantCulture);
        for (int i = 0; i < Surveys.Count; i++)
        {
            yield return "  stratigrafia " + (i + 1).ToString(CultureInfo.InvariantCulture) + " · strati " + Surveys[i].Count.ToString(CultureInfo.InvariantCulture);
            for (int r = 0; r < Surveys[i].Count; r++)
            {
                yield return "    " + HorizontalPileTrace.Compact(HorizontalPileTrace.WithoutColour(Surveys[i][r]));
                if (i == SelectedSurvey && r < Soils.Count && Soils[r] is { } soil) yield return "      " + soil.Line();
            }
        }
        yield return "sezione " + (ConcreteSection ?? "-") + " · chs " + (Chs is null ? "-" : HorizontalPileTrace.Hash(Chs));
        for (int i = 0; i < Results.Count; i++)
        {
            yield return "risultati sondaggio " + (i + 1).ToString(CultureInfo.InvariantCulture) + " · " + HorizontalPileTrace.Quote(Results[i].Info);
            foreach (var table in Results[i].Tables) foreach (string line in table.Lines()) yield return line;
        }
    }
}

/// <summary>Formats of the trace, shared by the two sides (the compact JSON, hashes and quotes of the pile group traces).</summary>
internal static class HorizontalPileTrace
{
    internal static string Compact(JsonNode? node) => PileGroupTrace.Compact(node);

    internal static string Hash(JsonNode node) => PileGroupTrace.Hash(node);

    /// <summary>Hash of texts or of numbers, serialized as JSON with the invariant culture.</summary>
    internal static string Hash(object value) => PileGroupTrace.Hash(JsonSerializer.SerializeToNode(value, PileGroupTrace.Json)!);

    internal static string Quote(string? text) => PileGroupTrace.Quote(text);

    internal static string YesNo(bool value) => PileGroupTrace.YesNo(value);

    internal static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>The display row without «__color»: the colour of the layer is a token of the view (Swatch.Soil1–6).</summary>
    internal static JsonObject WithoutColour(JsonObject row)
    {
        var copy = new JsonObject();
        foreach (var (key, value) in row) if (key != "__color") copy[key] = value?.DeepClone();
        return copy;
    }

    /// <summary>The c.a. section as the drawing shows it: outline, bars, link radius and diameter, axes and caption.</summary>
    internal static string ConcreteSection(IReadOnlyList<double[]> outline, IEnumerable<(double X, double Y, double Area, double Diameter)> bars,
        double linkRadius, double linkDiameter, bool axes, string? caption)
    {
        var barList = bars.Select(b => new[] { b.X, b.Y, b.Area, b.Diameter }).ToArray();
        return "contorno " + outline.Count.ToString(CultureInfo.InvariantCulture) + " " + Hash(outline)
            + " · barre " + barList.Length.ToString(CultureInfo.InvariantCulture) + " " + Hash(barList)
            + " · staffa " + Number(linkRadius) + " " + Number(linkDiameter) + (axes ? " · assi" : "") + " · " + Quote(caption);
    }

    internal static void Write(string path, IEnumerable<string> lines) => PileGroupTrace.Write(path, lines);

    internal static string[] Read(string path) => PileGroupTrace.Read(path);

    internal static string? FirstDifference(IReadOnlyList<string> expected, IReadOnlyList<string> actual) => PileGroupTrace.FirstDifference(expected, actual);
}
