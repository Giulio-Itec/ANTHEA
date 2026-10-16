using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Calculations;
namespace Materiali;

/// <summary>Persisted material-sheet choices and parsing, shared by the view and report service.</summary>
public sealed record MaterialExposureChoice(string Code, string Description, int Severity);

public sealed class MaterialSheetInput
{
    public static IReadOnlyList<MaterialExposureChoice> ExposureChoices { get; } = Array.AsReadOnly(Durability.Exposures
        .Select(e => new MaterialExposureChoice(e.Code, e.Description, NtcCover.Severity(e.Code))).ToArray());
    // Options and initial values: MaterialDefaults, shared with new sheets and the cover of the project (F2.9-U4)
    private static readonly IReadOnlyDictionary<string, (System.Collections.Immutable.ImmutableArray<string> Values, int Initial)> ChoiceCatalog = MaterialDefaults.Choices;
    public static IReadOnlyList<string> Options(string key) => ChoiceCatalog[key].Values;
    public static IReadOnlyList<string> DeviationOptions(int mode) => mode switch
    {
        0 => new[] { "10 mm" },
        1 => Enumerable.Range(5,6).Reverse().Select(x=>x+" mm").ToArray(),
        _ => Enumerable.Range(0,11).Reverse().Select(x=>x+" mm").ToArray()
    };
    public static int InitialSelection(string key) => ChoiceCatalog[key].Initial;
    public static string InitialNumber(string key) => MaterialDefaults.Number(key);
    /// <summary>Keys of the typed numbers, in the order of the persisted state (the order of the controls of the sheet).</summary>
    public static IReadOnlyList<string> NumberKeys { get; } = MaterialDefaults.Numbers.Select(n => n.Key).ToArray();
    /// <summary>Keys of the choices, deviation value included, in the order of the persisted state.</summary>
    public static IReadOnlyList<string> ChoiceKeys { get; } = MaterialDefaults.ChoiceOrder.ToArray();
    /// <summary>Keys of the options, in the order of the persisted state.</summary>
    public static IReadOnlyList<string> FlagKeys { get; } = MaterialDefaults.Flags.ToArray();
    /// <summary>The deviation value can be chosen for the deviation control <paramref name="mode"/> (index of the menu): not with the ordinary execution (moved from the historic view, F5.5).</summary>
    public static bool DeviationChoosable(int mode) => mode != 0;
    /// <summary>The cover follows NTC + Circolare, the first criterion of the menu; otherwise EC2 (moved from the historic view, F5.5).</summary>
    public static bool IsNtcCover(string? method) => method == Options("coverMethod")[0];
    public JsonObject State { get; }
    public string ClassName { get; }
    public double Fck { get; }
    public Exposure[] Exposures { get; }
    public MaterialSheetInput(JsonObject source, IEnumerable<string>? activeExposures = null)
    {
        ModuleCatalog.ValidateData("mat_calcestruzzo", source);
        State = (JsonObject)source.DeepClone();
        ClassName = source.S("classe", MaterialDefaults.ClassName);
        var selected = ConcreteMaterialCatalog.MaterialSheetClasses().SingleOrDefault(m => m.Name == ClassName);
        if (selected.Name is null) throw new ArgumentException("Classe del calcestruzzo non riconosciuta.");
        Fck = selected.Fck;
        string first = source.S("esposizione_principale", (source["esposizioni"] as JsonArray)?.FirstOrDefault()?.ToString() ?? MaterialDefaults.Exposure);
        var codes = (activeExposures ?? new[] { first }).ToHashSet(StringComparer.Ordinal);
        if (codes.Any(c => !Durability.Exposures.Any(e => e.Code == c))) throw new ArgumentException("Classe di esposizione non riconosciuta.");
        Exposures = Durability.Exposures.Where(e => codes.Contains(e.Code)).ToArray();
        foreach (string key in ChoiceCatalog.Keys)
            if (key != "deviationValue") _ = Index(key);
        if (!DeviationOptions(Index("deviationControl")).Contains(Selected("deviationValue")))
            throw new ArgumentException("Scelta materiali non valida: deviationValue");
    }
    /// <summary>Canonical persisted state formerly assembled by the material view. Unknown UI keys are not copied.</summary>
    public static JsonObject NormalizeState(JsonObject source)
    {
        var input = new MaterialSheetInput(source);
        string exposure = source.S("esposizione_principale", (source["esposizioni"] as JsonArray)?.FirstOrDefault()?.ToString() ?? MaterialDefaults.Exposure);
        if (source["esposizioni"] is JsonArray oldExposures && oldExposures.Any(e => !ExposureChoices.Any(c => c.Code == e!.GetValue<string>())))
            throw new ArgumentException("Classe di esposizione non riconosciuta.");
        var values = new JsonObject(); foreach (var (key, _) in MaterialDefaults.Numbers) values[key] = source["numeri"]?[key]?.GetValue<string>() ?? InitialNumber(key);
        var choices = new JsonObject(); foreach (string key in MaterialDefaults.ChoiceOrder) choices[key] = input.Selected(key);
        var flags = new JsonObject(); foreach (string key in MaterialDefaults.Flags) flags[key] = source["opzioni"]?[key]?.GetValue<bool>() ?? false;
        var state = new JsonObject { ["versione_materiali"] = MaterialDefaults.Version, ["classe"] = input.ClassName, ["esposizione_principale"] = exposure,
            ["numeri"] = values, ["scelte"] = choices, ["opzioni"] = flags, ["esposizioni"] = new JsonArray(exposure) };
        var legacy = source["esposizioni"] is JsonArray old && old.Count > 1 ? old : source["esposizioni_precedenti"] as JsonArray;
        if (legacy is not null) state["esposizioni_precedenti"] = legacy.DeepClone();
        return state;
    }
    public string Selected(string key) => State["scelte"]?.S(key, ChoiceCatalog[key].Values[ChoiceCatalog[key].Initial])
        ?? ChoiceCatalog[key].Values[ChoiceCatalog[key].Initial];
    public int Index(string key)
    {
        int index = ChoiceCatalog[key].Values.IndexOf(Selected(key));
        return index >= 0 ? index : throw new ArgumentException("Scelta materiali non valida: " + key);
    }
    public bool Flag(string key) => State["opzioni"]?.B(key) == true;
    public double Number(string key, string label, double min = 0, double max = double.MaxValue)
    {
        var raw = (State["numeri"]?.S(key, InitialNumber(key)) ?? InitialNumber(key)).Trim().Replace(',', '.');
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value) || value < min || value > max)
            throw new ArgumentException($"{label}: inserire un numero tra {min:g} e {(max == double.MaxValue ? "un valore finito" : max.ToString("g"))}.");
        return value;
    }
}
