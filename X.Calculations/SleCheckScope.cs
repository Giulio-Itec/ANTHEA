using System.Text.Json.Nodes;

namespace Anthea.Calculations;
/// <summary>Presentation scope of the checks currently implemented; missing/unsupported checks stay visible.</summary>
public static class SleCheckScope
{
    public static bool Stress(string set) => set != "SLE_FREQ";
    public static bool Cracking(string set, JsonNode workspace)
    {
        try { return !ConcreteCodeChecks.CrackRequirement(workspace.S("normativa", "NTC 2018"), set,
            workspace["sle"]?[set] as JsonObject ?? new()).Kind.StartsWith("Non richiesta"); }
        catch (ArgumentException) { return true; }
    }
}
