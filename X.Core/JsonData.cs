using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace X.Core;

/// <summary>Confine JSON compatibile con gli archivi X; i numeri mantengono la precisione double.</summary>
public static class J
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public static JsonNode? Node(object? value) => value is JsonNode n ? n.DeepClone() : JsonSerializer.SerializeToNode(value, Options);
    public static JsonObject Obj(params (string Key, object? Value)[] pairs)
    {
        var result = new JsonObject();
        foreach (var (key, value) in pairs) result[key] = Node(value);
        return result;
    }
    public static string S(this JsonNode? n, string key, string fallback = "") => n?[key]?.ToString() ?? fallback;
    public static double? Number(JsonNode? n)
    {
        return double.TryParse(n?.ToString().Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;
    }
    public static double D(this JsonNode? n, string key, double fallback = 0) => Number(n?[key]) ?? fallback;
    public static bool B(this JsonNode? n, string key, bool fallback = false) => n?[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;
    public static double Required(this JsonNode? n, string key, double minimum = 0, bool strict = false)
    {
        var value = Number(n?[key]);
        if (value is null || value < minimum || (strict && value == minimum))
            throw new ArgumentException($"{key}: inserire un numero finito {(strict ? "maggiore di" : "non inferiore a")} {minimum}.");
        return value.Value;
    }
    public static JsonArray Array(this JsonNode? n, string key) => n?[key] as JsonArray ?? new JsonArray();
    public static JsonObject Error(string message) => Obj(("errore", message));
    public static double Sum(IEnumerable<double> values)
    {
        // Somma compensata: corrisponde alla somma accurata delle versioni Python recenti.
        double sum = 0, correction = 0;
        foreach (var value in values) { var next = sum + value; correction += Math.Abs(sum) >= Math.Abs(value) ? (sum - next) + value : (value - next) + sum; sum = next; }
        return sum + correction;
    }
}
