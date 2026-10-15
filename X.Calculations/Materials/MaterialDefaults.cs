using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace Materiali;

/// <summary>
/// Initial values of the concrete material sheet, in one place (proposal F2.9-U4, user decision of 14/10/2026). A new sheet receives
/// them explicitly; the keys missing from sheets saved before are read with the same values, so no number changes.
/// </summary>
public static class MaterialDefaults
{
    public const int Version = 1;
    public const string ClassName = "C30/37", Exposure = "XC1";

    /// <summary>Options of each choice and index of the initial one, in the order of the persisted state.</summary>
    public static readonly FrozenDictionary<string, (ImmutableArray<string> Values, int Initial)> Choices = new Dictionary<string, (ImmutableArray<string>, int)>
    {
        ["ntcElement"] = (["Trave / pilastro", "Piastra / soletta / parete"], 0),
        ["coverMethod"] = (["NTC + Circ. 2019", "EC2 2004"], 0),
        ["life"] = (["50 anni", "100 anni"], 0),
        ["deviationControl"] = (["Ordinario", "Misura copriferri", "Misura accurata + scarto"], 0),
        ["deviationValue"] = (["10 mm"], 0),
        ["ground"] = (["Casseratura", "Terreno preparato", "Direttamente su terra"], 0),
        ["abrasion"] = (["Nessuno", "XM1 · +5 mm", "XM2 · +10 mm", "XM3 · +15 mm"], 0),
        ["bondCondition"] = (["Buone · η₁ = 1,0", "Altre · η₁ = 0,7"], 1),
        ["consistency"] = (["Da definire", "S1", "S2", "S3", "S4", "S5"], 0),
        ["cement"] = (["CEM I", "CEM II", "CEM III", "CEM IV", "CEM V", "Altro / da specificare"], 0),
        ["cementClass"] = (["32,5", "42,5", "52,5"], 0),
        ["cementEarly"] = (["N", "R"], 0),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Persisted order of the choices: the catalogue order with the bond condition last.</summary>
    public static readonly ImmutableArray<string> ChoiceOrder = ["ntcElement", "coverMethod", "life", "deviationControl", "deviationValue",
        "ground", "abrasion", "consistency", "cement", "cementClass", "cementEarly", "bondCondition"];

    /// <summary>Numeric fields (as typed text) in the persisted order.</summary>
    public static readonly ImmutableArray<(string Key, string Value)> Numbers = [("diameter", "16"), ("aggregate", "20"), ("cementName", ""),
        ("bondAlpha", "1,0"), ("bondGamma", "1,5")];

    public static readonly ImmutableArray<string> Flags = ["highStrength", "slab", "quality", "rough", "ntcQuality"];

    public static string Choice(string key) => Choices[key].Values[Choices[key].Initial];
    public static string Number(string key) => Numbers.Single(n => n.Key == key).Value;

    /// <summary>Complete persisted state of a new sheet, equal to the canonical state the sheet writes when it is saved.</summary>
    public static JsonObject NewState()
    {
        var numbers = new JsonObject(); foreach (var (key, value) in Numbers) numbers[key] = value;
        var choices = new JsonObject(); foreach (string key in ChoiceOrder) choices[key] = Choice(key);
        var flags = new JsonObject(); foreach (string key in Flags) flags[key] = false;
        return new JsonObject { ["versione_materiali"] = Version, ["classe"] = ClassName, ["esposizione_principale"] = Exposure,
            ["numeri"] = numbers, ["scelte"] = choices, ["opzioni"] = flags, ["esposizioni"] = new JsonArray(Exposure) };
    }
}
