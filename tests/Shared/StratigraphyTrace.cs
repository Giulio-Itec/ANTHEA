using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Anthea.Presentation.Tests;

/// <summary>
/// One step of a scenario of the stratigraphy sheet (F5.6b, geo_stratigrafia), applied in the same way to the historic view
/// (StratigraphyWorkspace in SheetEditor, tests/Desktop/StratigraphyHistoricTrace.cs) and to its ViewModel
/// (tests/ANTHEA.Presentation.Tests/StratigraphySheetChecks.cs). A step is what the user does with a control: a control that the user
/// cannot use (disabled, read-only revision) does nothing. <see cref="Key"/> names the field (the key of the data) or the destination
/// module; <see cref="Index"/> the layer; <see cref="Text"/> what the user types or chooses, or the answer to a question.
/// </summary>
internal sealed record StratigraphyStep(string Kind, string? Key = null, string? Text = null, int Index = -1)
{
    /// <summary>Commit of the sheet (navigation, save): the historic view validates the document and throws when it is not valid.</summary>
    internal const string Commit = "conferma";
    /// <summary>The «Calcola» command of the window: the stratigraphy only commits.</summary>
    internal const string Calculate = "calcola";
    /// <summary>Closes the sheet and opens its data again (a new sheet on a copy, as the window does).</summary>
    internal const string Reopen = "riapri";
    /// <summary>The sheet becomes a read-only revision.</summary>
    internal const string ReadOnly = "sola-lettura";
    /// <summary>Types <see cref="Text"/> in the general field <see cref="Key"/> (name, description, ground_elevation, water_depth).</summary>
    internal const string Field = "campo";
    /// <summary>Chooses <see cref="Text"/> in the general choice <see cref="Key"/> (scenario, water_state).</summary>
    internal const string Choice = "scelta";
    /// <summary>Selects the layer <see cref="Index"/> in the list.</summary>
    internal const string Select = "strato";
    /// <summary>Types <see cref="Text"/> in the field <see cref="Key"/> of the selected layer.</summary>
    internal const string LayerField = "campo-strato";
    /// <summary>Chooses <see cref="Text"/> in the choice <see cref="Key"/> of the selected layer (soil_type, behavior, qualification).</summary>
    internal const string LayerChoice = "scelta-strato";
    /// <summary>Types <see cref="Text"/> in the field <see cref="Key"/> of the minima of the selected layer.</summary>
    internal const string MinimumField = "campo-minimo";
    /// <summary>Chooses <see cref="Text"/> in the qualification of the minima of the selected layer.</summary>
    internal const string MinimumChoice = "scelta-minimo";
    /// <summary>Clicks «Ultimo strato esteso indefinitamente».</summary>
    internal const string Infinite = "indefinito";
    /// <summary>Clicks «Gestisci anche i valori minimi (opzionali)»; when it removes them, <see cref="Text"/> answers the question («sì», «no»).</summary>
    internal const string Minima = "minimi";
    /// <summary>The «+ Strato» button.</summary>
    internal const string Add = "aggiungi";
    /// <summary>The «Duplica» button.</summary>
    internal const string Duplicate = "duplica";
    /// <summary>The «Elimina» button.</summary>
    internal const string Remove = "elimina";
    /// <summary>The «↑» button.</summary>
    internal const string Up = "su";
    /// <summary>The «↓» button.</summary>
    internal const string Down = "giù";
    /// <summary>The «Importa relazione PDF con AI…» button (disabled in the Release: it does nothing).</summary>
    internal const string Import = "importa";
    /// <summary>
    /// «Invia / importa terreno da ANTHEA…», then in the dialog the destination module <see cref="Key"/>, the choice <see cref="Text"/>
    /// («Medi», «Minimi») and «Invia a un nuovo foglio»; when the dialog shows an error, the dialog is closed.
    /// </summary>
    internal const string Send = "invia";
    /// <summary>
    /// «Invia / importa terreno da ANTHEA…», then in the dialog the profile <see cref="Key"/> («corrente»: the profile of the sheet;
    /// «progetto»: the first sheet of the project, «Importa dal foglio selezionato»), the choice <see cref="Text"/> and «Sostituisci il
    /// terreno del foglio corrente», confirmed; when the dialog shows an error, the dialog is closed.
    /// </summary>
    internal const string Replace = "sostituisci";

    public override string ToString() => Kind + (Key is null ? "" : " " + Key)
        + (Index < 0 ? "" : " [" + Index.ToString(CultureInfo.InvariantCulture) + "]")
        + (Text is null ? "" : " «" + Text.Replace("\n", "\\n", StringComparison.Ordinal) + "»");
}

/// <summary>
/// A scenario: the data of an F3 fixture of the module, then the steps. <see cref="Source"/>: the F3 fixture of the module given as the
/// other soil sheet of the project (ISheetEditor.SoilSources), labelled <see cref="SourceLabel"/>; null outside a project.
/// </summary>
internal sealed record StratigraphyScenario(string Name, string Fixture, IReadOnlyList<StratigraphyStep> Steps, string? Source = null, string SourceLabel = "")
{
    internal string File => Name + ".txt";
}

/// <summary>
/// The scenarios of the stratigraphy sheet (F5.6b): for every F3 fixture of the module the path of the F3 capture (opening, commit,
/// calculation, reopening); then edits of every kind of the general data, of the layers and of the minima, the actions on the layers
/// with their errors, the transfer of the soil to other sheets and from the project, and a read-only revision.
/// </summary>
internal static class StratigraphyScenarios
{
    internal const string Module = "geo_stratigrafia";

    /// <summary>Identifier of the other soil sheet of the project in the scenarios with a source.</summary>
    internal const string SourceId = "foglio-sorgente";

    /// <summary>Folder of the traces of the historic view, frozen before the migration (proposal 7.2, capture first).</summary>
    internal static string Reference(string root) => Path.Combine(root, "tests", "fixtures", "f5-stratigrafia", "storica");

    internal static string FixturePath(string root, string id) => Path.Combine(root, "tests", "fixtures", "f3", "inputs", Module + "--" + id + ".json");

    /// <summary>The data of an F3 fixture, as the F3 capture reads them.</summary>
    internal static JsonObject FixtureData(string root, string id) => JsonNode.Parse(File.ReadAllText(FixturePath(root, id)))!["data"]!.AsObject();

    /// <summary>Identifiers of the F3 fixtures of the module, in ordinal order.</summary>
    internal static IReadOnlyList<string> Fixtures(string root) => Directory.EnumerateFiles(Path.Combine(root, "tests", "fixtures", "f3", "inputs"), Module + "--*.json")
        .Select(f => Path.GetFileNameWithoutExtension(f)[(Module.Length + 2)..]).Order(StringComparer.Ordinal).ToArray();

    static StratigraphyStep Step(string kind) => new(kind);
    static StratigraphyStep Field(string key, string text) => new(StratigraphyStep.Field, key, text);
    static StratigraphyStep Choice(string key, string text) => new(StratigraphyStep.Choice, key, text);
    static StratigraphyStep Select(int index) => new(StratigraphyStep.Select, Index: index);
    static StratigraphyStep LayerField(string key, string text) => new(StratigraphyStep.LayerField, key, text);
    static StratigraphyStep LayerChoice(string key, string text) => new(StratigraphyStep.LayerChoice, key, text);
    static StratigraphyStep MinimumField(string key, string text) => new(StratigraphyStep.MinimumField, key, text);
    static StratigraphyStep Minima(string? answer = null) => new(StratigraphyStep.Minima, Text: answer);
    static StratigraphyStep Send(string module, string choice) => new(StratigraphyStep.Send, module, choice);
    static StratigraphyStep Replace(string profile, string choice) => new(StratigraphyStep.Replace, profile, choice);

    internal static IReadOnlyList<StratigraphyScenario> All(string root)
    {
        StratigraphyStep commit = Step(StratigraphyStep.Commit), calculate = Step(StratigraphyStep.Calculate), reopen = Step(StratigraphyStep.Reopen),
            readOnly = Step(StratigraphyStep.ReadOnly), infinite = Step(StratigraphyStep.Infinite), add = Step(StratigraphyStep.Add),
            duplicate = Step(StratigraphyStep.Duplicate), remove = Step(StratigraphyStep.Remove), up = Step(StratigraphyStep.Up), down = Step(StratigraphyStep.Down),
            import = Step(StratigraphyStep.Import);
        var list = new List<StratigraphyScenario>();
        list.AddRange(Fixtures(root).Select(id => new StratigraphyScenario("fixture-" + id, id, [commit, calculate, commit, reopen, commit])));
        // The general data: texts, the choice of the parameters, the water table with its errors, then commit and calculation.
        list.Add(new("modifiche-generali", "profilo-medi-falda", [
            import,
            Field("name", "Sondaggio S1 bis"), Field("name", ""), commit, Field("name", "Sondaggio S1 bis"),
            Field("description", "Riga 1\nRiga 2"), Field("ground_elevation", "100,5"), Field("ground_elevation", "abc"), Field("ground_elevation", ""),
            Choice("scenario", "Minimi"), Choice("scenario", "Entrambi"), Choice("scenario", "Medi"),
            Choice("water_state", "Non nota"), Choice("water_state", "Non considerata"), Field("water_depth", ""), Choice("water_state", "Assegnata"),
            commit, Field("water_depth", "-1"), Field("water_depth", "3,2"), Choice("water_state", "Assegnata"),
            commit, calculate, reopen, commit]));
        // The fields of the layers: every text, family, behaviour, qualification, colour, thickness and parameter, the infinite extent of
        // the last layer, and the errors they show.
        list.Add(new("modifiche-strati", "profilo-medi-falda", [
            Select(1), LayerField("name", "Sabbia"), LayerChoice("soil_type", "Ghiaia"), LayerChoice("behavior", "Da definire"),
            LayerField("description", "Sabbia grossa\ncon ciottoli"), LayerField("color", "#zzzzzz"), LayerField("color", "#C0C0C0"),
            LayerField("thickness", "abc"), LayerField("thickness", "2,5"), LayerField("gamma", ""), LayerField("gamma", "18"), LayerField("gamma_sat", "17"),
            LayerField("phi", "95"), LayerField("phi", "33"), LayerField("c", "1"), LayerField("cu", ""),
            LayerChoice("qualification", "Interpretato"), LayerField("source", "Prove CPT"), LayerField("notes", "Nota dello strato"),
            infinite, Select(3), LayerField("name", "Argilla di base"), infinite, LayerField("thickness", "8"), infinite, Select(0), Select(3), infinite,
            commit, Select(2), LayerChoice("soil_type", "Roccia"), reopen, Select(3), commit]));
        // The buttons of the layers, with the errors shown in a message: first and last layer, the layer of indefinite extent, one layer left.
        list.Add(new("azioni-strati", "profilo-medi-falda", [
            add, duplicate, Select(2), up, down, down, Select(5), down, Select(0), up, remove, Select(4), remove, duplicate,
            remove, remove, remove, remove, remove, add, commit]));
        list.Add(new("azioni-indefinito", "minimi-entrambi-indefinito", [
            Select(2), duplicate, up, Select(1), down, add, Select(3), up, down, Select(0), duplicate, Select(4), remove, commit]));
        // The minima: assigned to a layer, typed, a removal refused and one confirmed; the minima of another layer.
        list.Add(new("minimi", "minimi-entrambi-indefinito", [
            Select(2), Minima(), MinimumField("gamma", "23"), MinimumField("phi", "41"), new(StratigraphyStep.MinimumChoice, "qualification", "Misurato"),
            MinimumField("source", "Prove"), MinimumField("notes", "Minimi del substrato"), Minima("no"), Minima("sì"),
            Select(1), MinimumField("gamma_sat", "12,5"), MinimumField("cu", "20"), Minima("sì"), Select(0), MinimumField("c", "1"),
            commit, reopen, Select(1), commit]));
        // The values as typed: a behaviour chosen in a layer that has no such key, numbers with the comma.
        list.Add(new("testi", "testi-digitati", [
            LayerChoice("behavior", "Granulare"), LayerField("gamma_sat", "20,5"), LayerField("cu", "0"), Select(1), LayerField("thickness", "2,25"),
            Choice("water_state", "Assegnata"), Field("water_depth", "1,5"), commit]));
        // The transfer of the soil: to new sheets of the verification modules (with the choice of the parameters) and to a new stratigraphy;
        // the errors of the dialog; the replacement of the soil of the sheet with its own profile; the commit that fails before the dialog.
        list.Add(new("trasferimento", "profilo-medi-falda", [
            Send("geo_palo_verticale", "Medi"), Send("geo_palo_verticale", "Minimi"), Send("geo_stratigrafia", "Medi"), Send("geo_muri_sostegno", "Medi"),
            Send("geo_palo_orizzontale", "Medi"), Send("geo_fondazioni_superficiali", "Medi"), Replace("corrente", "Medi"),
            Select(1), LayerField("thickness", "abc"), Send("geo_palo_verticale", "Medi"), LayerField("thickness", "3,8"), Send("geo_palo_verticale", "Medi")]));
        list.Add(new("trasferimento-minimi", "minimi-entrambi-indefinito", [
            Send("geo_palo_verticale", "Medi"), Choice("water_state", "Assegnata"), Field("water_depth", "1"), Send("geo_palo_verticale", "Minimi"),
            Send("geo_stratigrafia", "Minimi")]));
        // The soil of another sheet of the project replaces the soil of this one.
        list.Add(new("trasferimento-progetto", "testi-digitati", [Replace("progetto", "Medi"), Send("geo_stratigrafia", "Medi")],
            Source: "profilo-medi-falda", SourceLabel: "Sondaggio S1"));
        // A previous revision: every edit is refused, the selection of the layers stays allowed.
        list.Add(new("sola-lettura", "minimi-entrambi-indefinito", [
            readOnly, Field("name", "Altro"), Choice("scenario", "Medi"), Select(1), LayerField("name", "Altro"), LayerChoice("soil_type", "Limo"),
            MinimumField("gamma", "1"), Minima("sì"), Select(2), infinite, add, duplicate, remove, up, down, import,
            Send("geo_stratigrafia", "Medi"), commit, calculate, commit]));
        return list;
    }
}

/// <summary>A control of the sheet seen by the user: kind («testo», «scelta», «opzione»), key of the data, label, value, usable, items.</summary>
internal sealed record StratigraphyControl(string Kind, string Key, string Label, string Value, bool Usable, IReadOnlyList<string>? Items = null);

/// <summary>
/// The state of the stratigraphy sheet seen by the user after a step: data in memory order, Modified, the messages and the questions of
/// the step, the error of the step, the import button, the general fields, the actions, the list of the layers, the fields of the
/// selected layer, the status, the profile and the transfer. Each side fills it from its own objects; <see cref="Lines"/> writes it in
/// one format for both.
/// </summary>
internal sealed class StratigraphyTraceState
{
    public JsonObject Data = new();
    public int Modified;
    /// <summary>Messages and questions of the step, already formatted (<see cref="StratigraphyTrace.Message"/>, <see cref="StratigraphyTrace.Question"/>).</summary>
    public List<string> Messages = [];
    /// <summary>The exception of the step (Commit of a document that is not valid), already formatted.</summary>
    public string? Error;
    public bool ImportVisible, ImportEnabled;
    public string ImportLabel = "", ImportToolTip = "";
    public List<StratigraphyControl> General = [];
    /// <summary>The buttons of the layers, in their order, and whether they can be used.</summary>
    public List<(string Label, bool Usable)> Actions = [];
    /// <summary>The names in the list of the layers.</summary>
    public List<string> Layers = [];
    public int Selected = -1;
    /// <summary>The controls of the selected layer, in their order; empty without a selected layer.</summary>
    public List<StratigraphyControl> Layer = [];
    /// <summary>The note shown in place of the minima when the layer has none; null when the minima are shown.</summary>
    public string? MinimaNote;
    public string Status = "";
    public bool Profile;
    public int ProfileSelection;
    public string TransferLabel = "";
    public bool TransferUsable;
    /// <summary>Lines of the transfer started by the step (<see cref="StratigraphyTrace.Transfer"/>).</summary>
    public List<string> Transfer = [];

    /// <summary>The lines of the state, with the identifiers created during the scenario named by <paramref name="guids"/>.</summary>
    internal IEnumerable<string> Lines(string step, StratigraphyGuids guids) => Raw(step).Select(guids.Normalize);

    private IEnumerable<string> Raw(string step)
    {
        static string Q(string? text) => StratigraphyTrace.Quote(text);
        yield return "== " + step;
        yield return "dati " + StratigraphyTrace.Compact(Data);
        yield return "modificato " + Modified.ToString(CultureInfo.InvariantCulture);
        foreach (string message in Messages) yield return message;
        if (Error is not null) yield return "errore " + Error;
        yield return ImportVisible ? "importa " + Q(ImportLabel) + (ImportEnabled ? "" : " · spento") + " · " + Q(ImportToolTip) : "importa assente";
        yield return "generali";
        foreach (var control in General) yield return "  " + StratigraphyTrace.Control(control);
        yield return "azioni " + string.Join(" ", Actions.Select(a => Q(a.Label) + (a.Usable ? "" : " spento")));
        yield return "strati " + Layers.Count.ToString(CultureInfo.InvariantCulture) + " · selezionato " + Selected.ToString(CultureInfo.InvariantCulture);
        foreach (string name in Layers) yield return "  " + Q(name);
        yield return "strato";
        foreach (var control in Layer) yield return "  " + StratigraphyTrace.Control(control);
        if (MinimaNote is not null) yield return "  minimi assenti " + Q(MinimaNote);
        yield return "stato " + Q(Status);
        yield return "profilo " + (Profile ? "disegnato" : "assente") + " · selezione " + ProfileSelection.ToString(CultureInfo.InvariantCulture);
        yield return "trasferimento " + Q(TransferLabel) + (TransferUsable ? "" : " · spento");
        foreach (string line in Transfer) yield return "  " + line;
    }
}

/// <summary>Formats of the traces of the stratigraphy sheet, shared by the two sides.</summary>
internal static class StratigraphyTrace
{
    /// <summary>Compact JSON in memory order, without escaping the accented letters and the symbols of the texts.</summary>
    internal static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    internal static string Compact(JsonNode? node) => node is null ? "null" : node.ToJsonString(Json);

    internal static string Quote(string? text) => JsonSerializer.Serialize(text ?? "", Json);

    internal static string YesNo(bool value) => value ? "sì" : "no";

    internal static string Control(StratigraphyControl control) => control.Kind + " " + control.Key + " " + Quote(control.Label) + " " + Quote(control.Value)
        + (control.Usable ? "" : " · spento") + (control.Items is null ? "" : " · voci " + string.Join(" ", control.Items.Select(Quote)));

    /// <summary>A message with the OK button (MessageBox.Show of the historic view, IMessageService.Inform of the ViewModel).</summary>
    internal static string Message(string caption, string text) => "messaggio " + Quote(caption) + " " + Quote(text);

    /// <summary>A question with Yes and No and the answer of the step.</summary>
    internal static string Question(string caption, string text, bool yes) => "domanda " + Quote(caption) + " " + Quote(text) + " → " + YesNo(yes);

    /// <summary>The exception of a step: type and message.</summary>
    internal static string Error(Exception error) => error.GetType().Name + ": " + error.Message;

    /// <summary>
    /// The lines of a transfer: what the sheet passed to the dialog (module, whether the data are those of the sheet, number of sources) and
    /// what came back to the window: a copy in a new sheet, the replacement of the soil, or nothing (with the text of the dialog).
    /// </summary>
    internal static IEnumerable<string> Transfer(string module, bool sheetData, int sources, string? copyModule, string? copyName, JsonNode? output, bool replace, string status)
    {
        yield return "dialogo " + Quote(module) + " · dati del foglio " + YesNo(sheetData) + " · fonti " + sources.ToString(CultureInfo.InvariantCulture);
        if (output is null) yield return "esito nessuno · " + Quote(status);
        else if (replace) yield return "esito sostituzione " + Compact(output);
        else yield return "esito copia " + Quote(copyModule) + " " + Quote(copyName) + " " + Compact(output);
    }

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

/// <summary>
/// The identifiers of a scenario: those of its input (the F3 fixture and the source sheet) stay as they are; those created during the
/// scenario (a new or duplicated layer, StratigraphyTransfer.ToDocument) differ at every run and become «&lt;nuovo-n&gt;», numbered by
/// their first appearance in the trace. The same behaviour gives the same names on the two sides.
/// </summary>
internal sealed class StratigraphyGuids
{
    private static readonly Regex Guid = new(@"\b[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}\b", RegexOptions.Compiled);
    private readonly HashSet<string> known;
    private readonly Dictionary<string, string> created = new(StringComparer.Ordinal);

    internal StratigraphyGuids(params JsonNode?[] inputs) =>
        known = inputs.SelectMany(i => Guid.Matches(StratigraphyTrace.Compact(i)).Select(m => Key(m.Value))).ToHashSet(StringComparer.Ordinal);

    private static string Key(string guid) => guid.Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();

    internal string Normalize(string line) => Guid.Replace(line, m =>
    {
        string key = Key(m.Value);
        if (known.Contains(key)) return m.Value;
        if (!created.TryGetValue(key, out var name)) created[key] = name = "<nuovo-" + (created.Count + 1).ToString(CultureInfo.InvariantCulture) + ">";
        return name;
    });
}
