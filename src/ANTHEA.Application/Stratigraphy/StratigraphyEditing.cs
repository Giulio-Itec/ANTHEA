using System.Text.Json.Nodes;
using System.Collections.Immutable;

namespace Anthea.Application.Stratigraphy;

/// <summary>Document editing conventions; no soil correlations or verification calculations.</summary>
public static class StratigraphyEditing
{
    private const int AlphabetSize = 26, PastelBase = 160, PastelRange = 80;
    private static readonly ImmutableArray<string> Colors = ["#F4C95D", "#DFA06E", "#A8C686", "#8FB8DE", "#C6A0D5", "#C9B79C", "#8DCEC3", "#E8A9BD", "#C3CE77", "#ACAEE0"];
    public static string LayerLabel(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        string label = "";
        do { label = (char)('A' + index % AlphabetSize) + label; index = index / AlphabetSize - 1; } while (index >= 0);
        return label;
    }

    public static string NewColor(IEnumerable<JsonObject> layers)
    {
        var used = layers.Select(l => l["color"]?.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string color in Colors) if (!used.Contains(color)) return color;
        // Further identification colours, deterministic and distinct, for long profiles.
        for (int i = 0; ; i++)
        {
            string color = $"#{PastelBase + i % PastelRange:X2}{PastelBase + i / PastelRange % PastelRange:X2}{PastelBase + i / (PastelRange * PastelRange) % PastelRange:X2}";
            if (!used.Contains(color)) return color;
        }
    }

    /// <summary>Apply the editable default after typing, never while opening an existing document.</summary>
    public static bool ParameterEdited(JsonObject values, string key)
    {
        if (key is not ("gamma" or "gamma_sat")) return false;
        bool empty = string.IsNullOrWhiteSpace(values["gamma_sat"]?.ToString());
        bool linked = values["gamma_sat_auto"] is JsonValue flag && flag.TryGetValue<bool>(out var state) ? state : empty;
        if (key == "gamma_sat") linked = empty;
        if (!linked && values["gamma_sat_auto"] is null) return false;
        values["gamma_sat_auto"] = linked;
        if (!linked) return false;
        values["gamma_sat"] = values["gamma"]?.DeepClone();
        return true;
    }
}
