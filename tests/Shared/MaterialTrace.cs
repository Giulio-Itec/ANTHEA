using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Anthea.Presentation.Tests;

/// <summary>
/// One step of a scenario of the material sheets (F5.5), applied in the same way to the historic views (Materiali.MaterialView and
/// RebarMaterialView, tests/Desktop/MaterialHistoricTrace.cs) and to their ViewModels (tests/ANTHEA.Presentation.Tests).
/// <see cref="Key"/> names the number, the choice, the option or the field; <see cref="Text"/> is what the user types or chooses.
/// </summary>
internal sealed record MaterialStep(string Kind, string? Key = null, string? Text = null)
{
    /// <summary>Commit of the sheet (navigation, save).</summary>
    internal const string Commit = "conferma";
    /// <summary>The «Calcola» command of the window.</summary>
    internal const string Calculate = "calcola";
    /// <summary>Closes the sheet and opens its data again (a new sheet on a copy, as the window does).</summary>
    internal const string Reopen = "riapri";
    /// <summary>The sheet becomes a read-only revision.</summary>
    internal const string ReadOnly = "sola-lettura";
    /// <summary>Concrete: chooses the class <see cref="Text"/>.</summary>
    internal const string Class = "classe";
    /// <summary>Concrete: types <see cref="Text"/> in the number <see cref="Key"/>.</summary>
    internal const string Number = "numero";
    /// <summary>Chooses <see cref="Text"/> in the choice <see cref="Key"/> (the choices of the concrete, the diagram of the steel).</summary>
    internal const string Choice = "scelta";
    /// <summary>Concrete: clicks the check box <see cref="Key"/>.</summary>
    internal const string Option = "opzione";
    /// <summary>Concrete: chooses the exposure <see cref="Text"/> in the menu.</summary>
    internal const string Exposure = "esposizione";
    /// <summary>Steel: chooses the type <see cref="Text"/>.</summary>
    internal const string Steel = "tipo";
    /// <summary>Steel: types <see cref="Text"/> in the field <see cref="Key"/>.</summary>
    internal const string Field = "campo";
    /// <summary>Steel: types <see cref="Text"/> in the certificate or reference.</summary>
    internal const string Reference = "riferimento";

    public override string ToString() => Kind + (Key is null ? "" : " " + Key) + (Text is null ? "" : " «" + Text + "»");
}

/// <summary>A scenario: the data of an F3 fixture of the module, then the steps. The trace is stored as <see cref="File"/>.</summary>
internal sealed record MaterialScenario(string Module, string Name, string Fixture, IReadOnlyList<MaterialStep> Steps)
{
    internal string File => (Module == MaterialScenarios.Concrete ? "calcestruzzo-" : "acciaio-") + Name + ".txt";
}

/// <summary>
/// The scenarios of the material sheets (F5.5): for every F3 fixture of the two modules the path of the F3 capture (opening, commit,
/// calculation, reopening); then edits of every kind, with the data and the preview of the sheet after each one; then a read-only
/// revision.
/// </summary>
internal static class MaterialScenarios
{
    internal const string Concrete = "mat_calcestruzzo";
    internal const string Steel = "mat_acciaio_armatura";

    /// <summary>Folder of the traces of the historic views, frozen before the migration (proposal 7.2, capture first).</summary>
    internal static string Reference(string root) => Path.Combine(root, "tests", "fixtures", "f5-materiali", "storica");

    internal static string FixturePath(string root, string module, string id) => Path.Combine(root, "tests", "fixtures", "f3", "inputs", module + "--" + id + ".json");

    /// <summary>The data of an F3 fixture, as the F3 capture reads them.</summary>
    internal static JsonObject FixtureData(string root, string module, string id) => JsonNode.Parse(File.ReadAllText(FixturePath(root, module, id)))!["data"]!.AsObject();

    /// <summary>Identifiers of the F3 fixtures of <paramref name="module"/>, in ordinal order.</summary>
    internal static IReadOnlyList<string> Fixtures(string root, string module) => Directory.EnumerateFiles(Path.Combine(root, "tests", "fixtures", "f3", "inputs"), module + "--*.json")
        .Select(f => Path.GetFileNameWithoutExtension(f)[(module.Length + 2)..]).Order(StringComparer.Ordinal).ToArray();

    static MaterialStep Step(string kind) => new(kind);
    static MaterialStep Number(string key, string text) => new(MaterialStep.Number, key, text);
    static MaterialStep Choice(string key, string text) => new(MaterialStep.Choice, key, text);
    static MaterialStep Option(string key) => new(MaterialStep.Option, key);
    static MaterialStep Exposure(string code) => new(MaterialStep.Exposure, Text: code);
    static MaterialStep Field(string key, string text) => new(MaterialStep.Field, key, text);
    static MaterialStep Type(string name) => new(MaterialStep.Steel, Text: name);

    internal static IReadOnlyList<MaterialScenario> All(string root)
    {
        MaterialStep commit = Step(MaterialStep.Commit), calculate = Step(MaterialStep.Calculate), reopen = Step(MaterialStep.Reopen), readOnly = Step(MaterialStep.ReadOnly);
        var list = new List<MaterialScenario>();
        foreach (string module in new[] { Concrete, Steel })
            list.AddRange(Fixtures(root, module).Select(id => new MaterialScenario(module, "fixture-" + id, id, [commit, calculate, commit, reopen, commit])));
        list.Add(new(Concrete, "modifiche", "default", [
            commit,
            new(MaterialStep.Class, Text: "C40/50"),
            Number("diameter", "18"), Number("diameter", "abc"), Number("diameter", "16"),
            Choice("coverMethod", "EC2 2004"), Option("highStrength"), Option("slab"),
            Choice("deviationControl", "Misura copriferri"), Choice("deviationValue", "6 mm"),
            Choice("deviationControl", "Misura accurata + scarto"), Choice("deviationValue", "0 mm"),
            Choice("deviationControl", "Ordinario"),
            Choice("coverMethod", "NTC + Circ. 2019"), Choice("ntcElement", "Piastra / soletta / parete"), Option("ntcQuality"),
            Exposure("XF3"), Number("aggregate", "12"), Number("aggregate", "25"), Exposure("XS2"),
            Choice("life", "100 anni"), Choice("ground", "Terreno preparato"), Choice("abrasion", "XM3 · +15 mm"), Option("rough"),
            Choice("bondCondition", "Buone · η₁ = 1,0"), Number("bondAlpha", "0,8"), Number("bondGamma", "1"),
            Choice("consistency", "S5"), Choice("cement", "CEM IV"), Number("cementName", "Prova"), Choice("cementClass", "52,5"), Choice("cementEarly", "R"),
            commit,
            Number("diameter", "20"), Option("quality"), Option("quality"),
            calculate,
            Number("cementName", "Prova"), new(MaterialStep.Class, Text: "C40/50"), Exposure("XS2"),
            Exposure("X0"), Option("ntcQuality"),
            reopen, commit]));
        list.Add(new(Concrete, "modifiche-archivio", "legacy-tre-esposizioni", [
            Exposure("XC4"), Number("diameter", "14"), commit,
            Exposure("XD2"), calculate, reopen, commit]));
        list.Add(new(Concrete, "sola-lettura", "legacy-tre-esposizioni", [readOnly, commit, calculate, commit]));
        list.Add(new(Steel, "modifiche", "default", [
            commit,
            Type("B450A"), Type("FeB44k"), Field("steel_eps_u", "15"), Choice("steel_diagramma", "Incrudente"), Field("gamma_s", "1,2"),
            Type("Personalizzato"),
            Field("fyk_mpa", "455"), Field("steel_fu_mpa", "abc"), Field("steel_fu_mpa", "560"), Field("steel_modulus_mpa", "200000"),
            Field("steel_eps_u", "67,5"), Field("materiale_acciaio_nome", "Acciaio prova"), Choice("steel_diagramma", "Elastoplastico"),
            new(MaterialStep.Reference, Text: "Certificato 1"), new(MaterialStep.Reference, Text: "Certificato 12"),
            commit,
            Field("materiale_acciaio_nome", "FeB22k"), Field("fyk_mpa", "215"), Field("steel_fu_mpa", "335"),
            Type("B450C"), Field("gamma_s", "1.15"),
            calculate,
            Type("Personalizzato"), Field("steel_eps_u", "1"),
            reopen, commit]));
        list.Add(new(Steel, "modifiche-catalogo-precedente", "catalogo-precedente-b450a", [
            Field("gamma_s", "1,15"), commit, Type("B450A"), Field("gamma_s", "1.15"), reopen, commit]));
        list.Add(new(Steel, "sola-lettura", "personalizzato-incrudente", [readOnly, commit, calculate, commit]));
        return list;
    }
}

/// <summary>
/// The state of the concrete sheet seen by the user after a step: data and preview in memory order, Modified, the inputs and
/// every text, table and indicator. Each side fills it from its own objects; <see cref="Lines"/> writes it in one format for both.
/// </summary>
internal sealed class ConcreteTraceState
{
    public JsonObject Data = new(), Preview = new();
    public int Modified;
    public bool Editable;
    public string? ClassName;
    public int Classes;
    public List<(string Key, string Text)> Numbers = [];
    /// <summary>Selected item and enabled state of every choice, in the order of the sheet.</summary>
    public List<(string Key, string? Selected, bool Enabled)> Choices = [];
    /// <summary>Items of the choice of the deviation value, which depend on the deviation control.</summary>
    public List<string> DeviationValues = [];
    public List<(string Key, bool Checked)> Options = [];
    public string? Exposure;
    public int Exposures;
    /// <summary>Exposures taken by the calculation (the check boxes of the historic view), in the order of the catalogue.</summary>
    public List<string> Active = [];
    public string ExposureDetails = "", ExposureExamples = "", Aggressiveness = "", Combined = "";
    public bool CombinedVisible;
    public string Strength = "", Tension = "", Modulus = "", Status = "";
    public List<string[]> Properties = [];
    public string BondValue = "", BondDetails = "";
    public string MinimumClass = "", MinimumNote = "";
    public string CoverHeadline = "", CoverSteps = "", CoverNotes = "";
    public bool CoverError;
    public bool NtcVisible, Ec2Visible;
    public string DeviationNote = "", DeviationToolTip = "";
    public List<(string Key, string Text)> Composition = [];
    public string CompositionSource = "", AirNote = "", ChlorideNote = "", Requirements = "", Consistency = "";

    internal IEnumerable<string> Lines(string step)
    {
        static string Q(string? text) => MaterialTrace.Quote(text);
        yield return "== " + step;
        yield return "dati " + MaterialTrace.Compact(Data);
        yield return "anteprima " + MaterialTrace.Compact(Preview);
        yield return "modificato " + Modified.ToString(CultureInfo.InvariantCulture) + " · modificabile " + MaterialTrace.YesNo(Editable);
        yield return "classe " + Q(ClassName ?? "-") + " · voci " + Classes.ToString(CultureInfo.InvariantCulture);
        yield return "numeri " + string.Join(" ", Numbers.Select(n => n.Key + "=" + Q(n.Text)));
        yield return "scelte";
        foreach (var (key, selected, enabled) in Choices)
            yield return "  " + key + " " + Q(selected ?? "-") + (enabled ? "" : " · spento")
                + (key == "deviationValue" ? " · voci " + string.Join(" ", DeviationValues.Select(Q)) : "");
        yield return "opzioni " + string.Join(" ", Options.Select(o => o.Key + "=" + MaterialTrace.YesNo(o.Checked)));
        yield return "esposizione " + Q(Exposure ?? "-") + " · attive " + string.Join(" ", Active) + " · voci " + Exposures.ToString(CultureInfo.InvariantCulture);
        yield return "  descrizione " + Q(ExposureDetails);
        yield return "  esempi " + Q(ExposureExamples);
        yield return "  aggressività " + Q(Aggressiveness);
        yield return "  combinata " + Q(Combined) + (CombinedVisible ? "" : " · nascosta");
        yield return "proprietà " + string.Join(" ", new[] { Strength, Tension, Modulus }.Select(Q)) + " · stato " + Q(Status);
        yield return "tabella " + Properties.Count.ToString(CultureInfo.InvariantCulture);
        foreach (var row in Properties) yield return "  " + string.Join(" | ", row.Select(Q));
        yield return "aderenza " + Q(BondValue) + " · " + Q(BondDetails);
        yield return "classe minima " + Q(MinimumClass) + " · " + Q(MinimumNote);
        yield return "copriferro " + Q(CoverHeadline) + (CoverError ? " · errore" : "");
        yield return "  passi " + Q(CoverSteps);
        yield return "  note " + Q(CoverNotes);
        yield return "criterio ntc " + (NtcVisible ? "visibile" : "nascosto") + " · ec2 " + (Ec2Visible ? "visibile" : "nascosto");
        yield return "tolleranza " + Q(DeviationNote) + " · " + Q(DeviationToolTip);
        yield return "composizione " + string.Join(" ", Composition.Select(c => c.Key + "=" + Q(c.Text)));
        yield return "  fonte " + Q(CompositionSource);
        yield return "  aria " + Q(AirNote);
        yield return "  cloruri " + Q(ChlorideNote);
        yield return "  requisiti " + Q(Requirements);
        yield return "  consistenza " + Q(Consistency);
    }
}

/// <summary>The state of the reinforcing steel sheet seen by the user after a step, in one format for the two sides.</summary>
internal sealed class SteelTraceState
{
    public JsonObject Data = new(), Preview = new();
    public int Modified;
    public bool Editable;
    public string? Type;
    public List<string> Types = [];
    /// <summary>Text fields in the order of the sheet: text and whether the user can type in it.</summary>
    public List<(string Key, string Text, bool ReadOnly)> Fields = [];
    public string? Diagram;
    public List<string> Diagrams = [];
    public bool DiagramEnabled;
    public string Reference = "";
    public string Description = "", Source = "", Results = "", Status = "";
    /// <summary>The status is shown as coherent (green) rather than as a warning.</summary>
    public bool StatusCoherent;
    /// <summary>Series of the drawing of the material: name and points (ε ‰, σ MPa).</summary>
    public List<(string Name, IReadOnlyList<double[]> Points)> Series = [];

    internal IEnumerable<string> Lines(string step)
    {
        static string Q(string? text) => MaterialTrace.Quote(text);
        yield return "== " + step;
        yield return "dati " + MaterialTrace.Compact(Data);
        yield return "anteprima " + MaterialTrace.Compact(Preview);
        yield return "modificato " + Modified.ToString(CultureInfo.InvariantCulture) + " · modificabile " + MaterialTrace.YesNo(Editable);
        yield return "tipo " + Q(Type ?? "-") + " · voci " + string.Join(" ", Types.Select(Q));
        yield return "campi";
        foreach (var (key, text, readOnly) in Fields) yield return "  " + key + " " + Q(text) + (readOnly ? " · bloccato" : "");
        yield return "  steel_diagramma " + Q(Diagram ?? "-") + (DiagramEnabled ? "" : " · spento") + " · voci " + string.Join(" ", Diagrams.Select(Q));
        yield return "riferimento " + Q(Reference);
        yield return "descrizione " + Q(Description);
        yield return "fonte " + Q(Source);
        yield return "risultati " + Q(Results);
        yield return "stato " + Q(Status) + " · " + (StatusCoherent ? "coerente" : "avviso");
        yield return "diagramma " + Series.Count.ToString(CultureInfo.InvariantCulture);
        foreach (var (name, points) in Series)
            yield return "  " + Q(name) + " · " + points.Count.ToString(CultureInfo.InvariantCulture) + " punti · " + MaterialTrace.Points(points);
    }
}

/// <summary>Formats of the traces of the material sheets, shared by the two sides.</summary>
internal static class MaterialTrace
{
    /// <summary>Compact JSON in memory order, without escaping the accented letters and the symbols of the texts.</summary>
    internal static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    internal static string Compact(JsonNode? node) => node is null ? "null" : node.ToJsonString(Json);

    internal static string Quote(string? text) => JsonSerializer.Serialize(text ?? "", Json);

    internal static string YesNo(bool value) => value ? "sì" : "no";

    /// <summary>First 16 hexadecimal digits of the SHA-256 of the points written with the round-trip format: the curves are long.</summary>
    internal static string Points(IEnumerable<double[]> points)
    {
        string text = string.Join(";", points.Select(p => string.Join(",", p.Select(v => v.ToString("R", CultureInfo.InvariantCulture)))));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];
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
