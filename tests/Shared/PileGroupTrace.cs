using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Anthea.Presentation.Tests;

/// <summary>
/// One step of a scenario of the pile group sheet (F5.4), applied in the same way to the historic view
/// (HorizontalPileGroupWorkspace, tests/Desktop/PileGroupHistoricTrace.cs, archived at the F5.4 switch in
/// supporto/SUPERATI/ui-storica/palificata), to its ViewModel (PileGroupSheetViewModel, tests/ANTHEA.Presentation.Tests/PileGroupSheetChecks.cs)
/// and to the XAML view (tests/Desktop/PileGroupViewChecks.cs). <see cref="Key"/> names the field, the option or the column of the
/// coordinates; <see cref="Index"/> the row; <see cref="Text"/> what the user types or chooses.
/// </summary>
internal sealed record PileGroupStep(string Kind, string? Key = null, string? Text = null, int Index = -1)
{
    /// <summary>Commit of the sheet (navigation, save).</summary>
    internal const string Commit = "conferma";
    /// <summary>The «Calcola» command of the window.</summary>
    internal const string Calculate = "calcola";
    /// <summary>Waits for the automatic recalculation (180 ms after the last edit).</summary>
    internal const string Wait = "attendi";
    /// <summary>Closes the sheet and opens its data again (a new sheet on a copy, as the window does).</summary>
    internal const string Reopen = "riapri";
    /// <summary>The sheet becomes a read-only revision.</summary>
    internal const string ReadOnly = "sola-lettura";
    /// <summary>Types <see cref="Text"/> in the text field <see cref="Key"/>.</summary>
    internal const string Field = "campo";
    /// <summary>Clicks the check box <see cref="Key"/>.</summary>
    internal const string Option = "opzione";
    /// <summary>Chooses the layout <see cref="Text"/>.</summary>
    internal const string Layout = "disposizione";
    /// <summary>Chooses the shape of the cap <see cref="Text"/>.</summary>
    internal const string Cap = "basamento";
    /// <summary>Shows the representative spacings of the direction number <see cref="Index"/> (+X, +Y, −X, −Y, custom).</summary>
    internal const string Spacing = "interassi";
    /// <summary>Types <see cref="Text"/> in the column <see cref="Key"/> (Id, X, Y) of the coordinate row <see cref="Index"/>.</summary>
    internal const string Pile = "palo";
    /// <summary>Adds a row to the coordinates (the new row of the table).</summary>
    internal const string AddPile = "aggiungi-palo";
    /// <summary>Deletes the coordinate row <see cref="Index"/>.</summary>
    internal const string RemovePile = "togli-palo";
    /// <summary>Cancels the edit of a coordinate cell (Esc).</summary>
    internal const string CancelEdit = "annulla-modifica";
    /// <summary>Selects the row <see cref="Index"/> of the comparison.</summary>
    internal const string Select = "seleziona";

    public override string ToString() => Kind + (Key is null ? "" : " " + Key) + (Index < 0 ? "" : " [" + Index.ToString(CultureInfo.InvariantCulture) + "]")
        + (Text is null ? "" : " «" + Text + "»");
}

/// <summary>A scenario: the data of an F3 fixture of the module, then the steps.</summary>
internal sealed record PileGroupScenario(string Name, string Fixture, IReadOnlyList<PileGroupStep> Steps);

/// <summary>
/// The scenarios of the pile group sheet (F5.4): for every F3 fixture of the module the path of the F3 capture (opening, commit,
/// calculation, reopening); then edits of every kind on the default and on the migrated archive; then a read-only revision.
/// </summary>
internal static class PileGroupScenarios
{
    internal const string Module = "geo_efficienza_orizzontale";

    /// <summary>Folder of the traces of the historic view, frozen before the migration (proposal 7.2, capture first).</summary>
    internal static string Reference(string root) => Path.Combine(root, "tests", "fixtures", "f5-palificata", "storica");

    internal static string FixturePath(string root, string id) => Path.Combine(root, "tests", "fixtures", "f3", "inputs", Module + "--" + id + ".json");

    /// <summary>The data of an F3 fixture of the module, as the F3 capture reads them.</summary>
    internal static JsonObject FixtureData(string root, string id) => JsonNode.Parse(File.ReadAllText(FixturePath(root, id)))!["data"]!.AsObject();

    /// <summary>Identifiers of the F3 fixtures of the module, in ordinal order.</summary>
    internal static IReadOnlyList<string> Fixtures(string root) => Directory.EnumerateFiles(Path.Combine(root, "tests", "fixtures", "f3", "inputs"), Module + "--*.json")
        .Select(f => Path.GetFileNameWithoutExtension(f)[(Module.Length + 2)..]).Order(StringComparer.Ordinal).ToArray();

    static PileGroupStep Step(string kind) => new(kind);
    static PileGroupStep Field(string key, string text) => new(PileGroupStep.Field, key, text);
    static PileGroupStep Option(string key) => new(PileGroupStep.Option, key);
    static PileGroupStep Pile(int index, string column, string text) => new(PileGroupStep.Pile, column, text, index);

    internal static IReadOnlyList<PileGroupScenario> All(string root)
    {
        PileGroupStep commit = Step(PileGroupStep.Commit), calculate = Step(PileGroupStep.Calculate), wait = Step(PileGroupStep.Wait), reopen = Step(PileGroupStep.Reopen);
        var list = Fixtures(root).Select(id => new PileGroupScenario("fixture-" + id, id, [commit, calculate, commit, reopen, commit])).ToList();
        list.Add(new("modifiche-default", "default", [
            commit,
            Field("diametro", "1,2"), wait,
            Field("nx", "4"), wait,
            Field("sx_d", "abc"), wait,
            commit,
            Field("sx_d", "3,5"), wait,
            new(PileGroupStep.Layout, Text: "Quinconce"), wait,
            new(PileGroupStep.Layout, Text: "Esagonale"), wait,
            Option("centro"), wait,
            Field("rotazione", ""), wait,
            Field("rotazione", "10"), commit,
            Option("X-"), wait,
            Option("custom"), Field("angolo_custom", "22,5"), wait,
            Option("X"), wait,
            new(PileGroupStep.Cap, Text: "Rettangolare"), wait,
            Field("bordo_d", "0,4"), wait,
            Field("bordo_d", "1,5"), wait,
            Option("estrapolazioni"), wait,
            Option("file_proiettate"), wait,
            new(PileGroupStep.Spacing, Index: 1),
            Field("interasse_parallelo", "3"), wait,
            Field("interasse_trasversale", "2,5"), wait,
            Field("interasse_parallelo", ""), wait,
            new(PileGroupStep.Spacing, Index: 4), new(PileGroupStep.Spacing, Index: 0),
            new(PileGroupStep.Select, Index: 3), new(PileGroupStep.Select, Index: 0),
            calculate, commit, reopen, commit]));
        list.Add(new("modifiche-generica", "legacy-pile-group", [
            commit,
            Pile(0, "X", "-3,5"), wait,
            Step(PileGroupStep.AddPile), wait,
            Pile(9, "Id", "P10"), Pile(9, "X", "6"), Pile(9, "Y", "6"), wait,
            new(PileGroupStep.RemovePile, Index: 1), wait,
            Step(PileGroupStep.CancelEdit), wait,
            Pile(2, "Y", "abc"), wait,
            Pile(2, "Y", "-3"), commit,
            new(PileGroupStep.Select, Index: 7),
            new(PileGroupStep.Layout, Text: "Rettangolare"), wait,
            new(PileGroupStep.Layout, Text: "Generica"), wait,
            Pile(0, "X", "-4"), commit, reopen, commit]));
        list.Add(new("sola-lettura", "quinconce-direzioni", [
            Step(PileGroupStep.ReadOnly), new(PileGroupStep.Select, Index: 4), calculate, commit, new(PileGroupStep.Select, Index: 22), commit]));
        return list;
    }
}

/// <summary>
/// The state of the sheet seen by the user after a step: data in memory order, Modified, result, texts, tables, fields and
/// drawing. Each side fills it from its own objects; <see cref="Lines"/> writes it in one format for both.
/// </summary>
internal sealed class PileGroupTraceState
{
    public JsonObject Data = new();
    public int Modified;
    public JsonObject? Result;
    public bool Editable;
    public string Title = "", State = "", Minimum = "", Mean = "", Maximum = "", Reduction = "", Geometry = "", Source = "", Notes = "";
    public List<string[]> Comparison = [];
    public int SelectedComparison = -1;
    public List<object?[]> Details = [];
    public List<string[]> Piles = [];
    public bool CoordinatesEditable, CanAddPiles;
    public string CoordinateHelp = "";
    public string? Layout;
    /// <summary>Fields of the generator in their order: text fields and options (<see cref="PileGroupTrace.FieldLine"/>, <see cref="PileGroupTrace.OptionLine"/>).</summary>
    public List<string> Generator = [];
    public string GeneratorNote = "";
    public string Diameter = "";
    public List<string> Directions = [];
    public string CustomAngle = "";
    public bool CustomAngleEnabled;
    public string? CapShape;
    public string CapEdge = "";
    public bool Extrapolations, ProjectedRows;
    public string SpacingDirection = "";
    public int SpacingIndex;
    public string ParallelSpacing = "", TransverseSpacing = "";
    /// <summary>The drawing received its data (PileGroupPlan.Set).</summary>
    public bool PlanShown;
    public JsonNode? PlanSelected;
    public JsonArray? PlanCap;

    internal IEnumerable<string> Lines(string step)
    {
        yield return "== " + step;
        yield return "dati " + PileGroupTrace.Compact(Data);
        yield return "modificato " + Modified.ToString(CultureInfo.InvariantCulture) + " · modificabile " + PileGroupTrace.YesNo(Editable);
        yield return "risultato " + (Result is null ? "-" : PileGroupTrace.Hash(Result) + " · selezionato " + PileGroupTrace.Selection(Result["selezionato"]));
        yield return "titolo " + PileGroupTrace.Quote(Title);
        yield return "stato " + PileGroupTrace.Quote(State);
        yield return "indicatori " + string.Join(" ", new[] { Minimum, Mean, Maximum, Reduction }.Select(PileGroupTrace.Quote));
        yield return "geometria " + PileGroupTrace.Quote(Geometry);
        yield return "fonte " + PileGroupTrace.Quote(Source);
        yield return "note " + PileGroupTrace.Quote(Notes);
        yield return "confronto " + Comparison.Count.ToString(CultureInfo.InvariantCulture) + " · selezionata " + SelectedComparison.ToString(CultureInfo.InvariantCulture);
        foreach (var row in Comparison) yield return "  " + string.Join(" | ", row.Select(PileGroupTrace.Quote));
        yield return "dettagli " + Details.Count.ToString(CultureInfo.InvariantCulture);
        foreach (var row in Details) yield return "  " + string.Join(" | ", row.Select(PileGroupTrace.Cell));
        yield return "pali " + Piles.Count.ToString(CultureInfo.InvariantCulture) + " · modificabili " + PileGroupTrace.YesNo(CoordinatesEditable)
            + " · righe nuove " + PileGroupTrace.YesNo(CanAddPiles) + " · " + PileGroupTrace.Quote(CoordinateHelp);
        foreach (var row in Piles) yield return "  " + string.Join(" | ", row.Select(PileGroupTrace.Quote));
        yield return "disposizione " + PileGroupTrace.Quote(Layout ?? "-") + " · nota " + PileGroupTrace.Quote(GeneratorNote);
        foreach (string field in Generator) yield return "  " + field;
        yield return "diametro " + PileGroupTrace.Quote(Diameter);
        yield return "direzioni " + string.Join(" ", Directions) + " · angolo " + PileGroupTrace.Quote(CustomAngle) + " " + (CustomAngleEnabled ? "attivo" : "spento");
        yield return "basamento " + PileGroupTrace.Quote(CapShape ?? "-") + " · asse-bordo " + PileGroupTrace.Quote(CapEdge);
        yield return "opzioni estrapolazioni " + PileGroupTrace.YesNo(Extrapolations) + " · file proiettate " + PileGroupTrace.YesNo(ProjectedRows);
        yield return "interassi " + SpacingDirection + " [" + SpacingIndex.ToString(CultureInfo.InvariantCulture) + "] " + PileGroupTrace.Quote(ParallelSpacing) + " " + PileGroupTrace.Quote(TransverseSpacing);
        yield return "disegno " + (!PlanShown ? "-" : (PlanSelected is null ? "senza risultato" : PileGroupTrace.Selection(PlanSelected))
            + " · basamento " + (PlanCap is null ? "-" : PlanCap.Count.ToString(CultureInfo.InvariantCulture)));
    }
}

/// <summary>Formats of the trace, shared by the two sides.</summary>
internal static class PileGroupTrace
{
    /// <summary>Compact JSON in memory order, without escaping the accented letters and the symbols of the texts.</summary>
    internal static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    internal static string Compact(JsonNode? node) => node is null ? "null" : node.ToJsonString(Json);

    /// <summary>First 16 hexadecimal digits of the SHA-256 of the compact JSON: the results are large, their bytes are compared through it.</summary>
    internal static string Hash(JsonNode node) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Compact(node))))[..16];

    internal static string Quote(string? text) => JsonSerializer.Serialize(text ?? "", Json);

    internal static string YesNo(bool value) => value ? "sì" : "no";

    /// <summary>A cell of the table of the details: numbers with the invariant culture, texts quoted.</summary>
    internal static string Cell(object? value) => value switch
    {
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        string text => Quote(text),
        null => "null",
        _ => Quote(Convert.ToString(value, CultureInfo.InvariantCulture))
    };

    internal static string Selection(JsonNode? selected) => selected is null ? "-"
        : Compact(selected["Method"]) + "/" + Compact(selected["DirectionId"]) + "/" + Compact(selected["Angle"]);

    internal static string FieldLine(string key, string label, string text, bool enabled) =>
        "campo " + key + " " + Quote(label.Trim()) + " = " + Quote(text) + (enabled ? "" : " · spento");

    internal static string OptionLine(string key, string label, bool isChecked) => "opzione " + key + " " + Quote(label.Trim()) + " = " + YesNo(isChecked);

    internal static string DirectionLine(string key, bool isChecked) => key + "=" + YesNo(isChecked);

    /// <summary>Writes the lines with LF endings and without BOM, as they are stored.</summary>
    internal static void Write(string path, IEnumerable<string> lines) => File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));

    /// <summary>The lines of a stored trace (any line ending).</summary>
    internal static string[] Read(string path) => File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

    /// <summary>First line where two traces differ, or null when they are equal.</summary>
    internal static string? FirstDifference(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        int count = Math.Min(expected.Count, actual.Count);
        string step = "";
        for (int i = 0; i < count; i++)
        {
            if (expected[i].StartsWith("== ", StringComparison.Ordinal)) step = expected[i];
            if (expected[i] != actual[i]) return $"riga {i + 1} ({step}):\n  attesa:  {expected[i]}\n  ottenuta: {actual[i]}";
        }
        return expected.Count == actual.Count ? null : $"righe {expected.Count} attese, {actual.Count} ottenute";
    }
}
