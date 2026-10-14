using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Anthea.Serialization;

/// <summary>Versioned document data, independent of price catalogues. No engineering calculations.</summary>
public static class BridgeEnvironmentData
{
    private static readonly string[] Keys = ["co2_concrete", "co2_rebar", "co2_steel", "co2_pt", "co2_site", "co2_cable", "low_carbon_factor", "recycled_factor"];
    public static JsonObject Factors(JsonObject data)
    {
        var factors = new JsonObject();
        foreach (string key in Keys)
        {
            var node = data["assumptions"]?[key];
            if (node is null || !double.TryParse(node.ToString().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
                throw new ArgumentException("Fattore ambientale richiesto: " + key);
            factors[key] = value;
        }
        return factors;
    }
    public static string Revision(JsonObject data) => Fingerprint(Factors(data));
    private static string Fingerprint(JsonNode node) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(node.ToJsonString())));

    /// <summary>Archives the previous factor set only when the effective data changes.</summary>
    public static void Synchronize(JsonObject data)
    {
        var factors = Factors(data); string revision = Fingerprint(factors);
        if (data["environment"] is JsonObject current && current["revision"]?.GetValue<string>() == revision) return;
        var history = data["environment"]?["history"]?.DeepClone() as JsonArray ?? [];
        if (data["environment"] is JsonObject previous)
        {
            var entry = (JsonObject)previous.DeepClone(); entry.Remove("history"); history.Add(entry);
        }
        data["environment"] = new JsonObject { ["schema"] = 1, ["revision"] = revision, ["factors"] = factors,
            ["date"] = DateTimeOffset.UtcNow.ToString("O"), ["source"] = "Fattori espliciti del progetto", ["history"] = history };
    }
}
