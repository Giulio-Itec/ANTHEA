using System.Text.Json.Nodes;

namespace Anthea.Calculations;
/// <summary>Presentation scope of the checks currently implemented; missing/unsupported checks stay visible.</summary>
public static class SleCheckScope
{
    public static bool Stress(string set) => set != "SLE_FREQ";
    public static bool Cracking(string set, JsonNode workspace) => workspace.S("normativa") != "NTC 2018" ||
        !Ntc2018Checks.CrackRequirement(set, (workspace["sle"]?[set]).S("esposizione"), (workspace["sle"]?[set]).S("sensibilita") == "Sensibile").Kind.StartsWith("Non richiesta");
}
