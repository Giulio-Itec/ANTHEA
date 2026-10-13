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
    private static readonly IReadOnlyDictionary<string, (string[] Values, int Initial)> ChoiceCatalog = new Dictionary<string, (string[], int)>
    {
        ["ntcElement"] = (["Trave / pilastro","Piastra / soletta / parete"], 0),
        ["coverMethod"] = (["NTC + Circ. 2019","EC2 2004"], 0),
        ["life"] = (["50 anni","100 anni"], 0),
        ["deviationControl"] = (["Ordinario","Misura copriferri","Misura accurata + scarto"], 0),
        ["deviationValue"] = (["10 mm"], 0),
        ["ground"] = (["Casseratura","Terreno preparato","Direttamente su terra"], 0),
        ["abrasion"] = (["Nessuno","XM1 · +5 mm","XM2 · +10 mm","XM3 · +15 mm"], 0),
        ["bondCondition"] = (["Buone · η₁ = 1,0","Altre · η₁ = 0,7"], 1),
        ["consistency"] = (["Da definire","S1","S2","S3","S4","S5"], 0),
        ["cement"] = (["CEM I","CEM II","CEM III","CEM IV","CEM V","Altro / da specificare"], 0),
        ["cementClass"] = (["32,5","42,5","52,5"], 0),
        ["cementEarly"] = (["N","R"], 0),
    };
    private static readonly IReadOnlyDictionary<string, string> NumericCatalog = new Dictionary<string, string>
    {
        ["diameter"] = "16",
        ["aggregate"] = "20",
        ["bondAlpha"] = "1,0",
        ["bondGamma"] = "1,5",
        ["cementName"] = "",
    };
    public static IReadOnlyList<string> Options(string key) => Array.AsReadOnly(ChoiceCatalog[key].Values);
    public static IReadOnlyList<string> DeviationOptions(int mode) => mode switch
    {
        0 => new[] { "10 mm" },
        1 => Enumerable.Range(5,6).Reverse().Select(x=>x+" mm").ToArray(),
        _ => Enumerable.Range(0,11).Reverse().Select(x=>x+" mm").ToArray()
    };
    public static int InitialSelection(string key) => ChoiceCatalog[key].Initial;
    public static string InitialNumber(string key) => NumericCatalog[key];
    public JsonObject State { get; }
    public string ClassName { get; }
    public double Fck { get; }
    public Exposure[] Exposures { get; }
    public MaterialSheetInput(JsonObject source, IEnumerable<string>? activeExposures = null)
    {
        ModuleCatalog.ValidateData("mat_calcestruzzo", source);
        State = (JsonObject)source.DeepClone();
        ClassName = source.S("classe", "C30/37");
        var selected = ConcreteMaterialCatalog.MaterialSheetClasses().SingleOrDefault(m => m.Name == ClassName);
        if (selected.Name is null) throw new ArgumentException("Classe del calcestruzzo non riconosciuta.");
        Fck = selected.Fck;
        string first = source.S("esposizione_principale", (source["esposizioni"] as JsonArray)?.FirstOrDefault()?.ToString() ?? "XC1");
        var codes = (activeExposures ?? new[] { first }).ToHashSet(StringComparer.Ordinal);
        if (codes.Any(c => !Durability.Exposures.Any(e => e.Code == c))) throw new ArgumentException("Classe di esposizione non riconosciuta.");
        Exposures = Durability.Exposures.Where(e => codes.Contains(e.Code)).ToArray();
        foreach (string key in ChoiceCatalog.Keys)
            if (key != "deviationValue") _ = Index(key);
        if (!DeviationOptions(Index("deviationControl")).Contains(Selected("deviationValue")))
            throw new ArgumentException("Scelta materiali non valida: deviationValue");
    }
    public string Selected(string key) => State["scelte"]?.S(key, ChoiceCatalog[key].Values[ChoiceCatalog[key].Initial])
        ?? ChoiceCatalog[key].Values[ChoiceCatalog[key].Initial];
    public int Index(string key)
    {
        int index = Array.IndexOf(ChoiceCatalog[key].Values, Selected(key));
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
