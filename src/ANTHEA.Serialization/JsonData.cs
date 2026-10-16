using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>Confine JSON compatibile con gli archivi X; i numeri mantengono la precisione double.</summary>
public static class J
{
    public static bool Equivalent(JsonNode? a, JsonNode? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is JsonObject ao && b is JsonObject bo)
            return ao.Count == bo.Count && ao.All(p => bo.TryGetPropertyValue(p.Key, out var value) && Equivalent(p.Value, value));
        if (a is JsonArray aa && b is JsonArray ba)
            return aa.Count == ba.Count && aa.Select((v, i) => Equivalent(v, ba[i])).All(v => v);
        if (a is JsonObject or JsonArray || b is JsonObject or JsonArray) return false;
        return Number(a) is double x && Number(b) is double y ? x == y : JsonNode.DeepEquals(a, b);
    }
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    public static JsonNode? Node(object? value) => value is JsonNode n ? n.DeepClone() : JsonSerializer.SerializeToNode(value, Options);
    public static JsonObject Obj(params (string Key, object? Value)[] pairs)
    {
        var result = new JsonObject();
        foreach (var (key, value) in pairs) result[key] = Node(value);
        return result;
    }
    public static string S(this JsonNode? n, string key, string fallback = "")
    {
        var value = n?[key];
        if (value is not null) return value.ToString();
        if (JsonFallbackHook.Enabled) JsonFallbackHook.Record("S", n, key, fallback);
        return fallback;
    }
    public static double? Number(JsonNode? n)
    {
        return double.TryParse(n?.ToString().Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;
    }
    public static double D(this JsonNode? n, string key, double fallback = 0)
    {
        if (Number(n?[key]) is double value) return value;
        if (JsonFallbackHook.Enabled) JsonFallbackHook.Record("D", n, key, fallback);
        return fallback;
    }
    public static bool B(this JsonNode? n, string key, bool fallback = false)
    {
        if (n?[key] is JsonValue v && v.TryGetValue<bool>(out var b)) return b;
        if (JsonFallbackHook.Enabled) JsonFallbackHook.Record("B", n, key, fallback);
        return fallback;
    }
    public static double Required(this JsonNode? n, string key, double minimum = 0, bool strict = false)
    {
        var value = Number(n?[key]);
        if (value is null || value < minimum || (strict && value == minimum))
            throw new ArgumentException($"{key}: inserire un numero finito {(strict ? "maggiore di" : "non inferiore a")} {minimum}.");
        return value.Value;
    }
    public static JsonArray Array(this JsonNode? n, string key) => n?[key] as JsonArray ?? new JsonArray();
    public static JsonObject Error(string message) => Obj(("errore", message));

}

/// <summary>
/// Punto d'osservazione dei ripieghi restituiti da <see cref="J.S"/>, <see cref="J.D"/> e <see cref="J.B"/> quando una chiave manca o non
/// è valida (decisione D3 del 15/10/2026): nessuna logica, nessuna variabile d'ambiente, nessun file. In produzione non c'è osservatore e
/// non si registra nulla. Lo registra soltanto lo strumento delle catture (tests/ANTHEA.Testing, JsonFallbackTrace), che scrive
/// fallbacks.json. I valori restituiti non cambiano mai.
/// Dalla decisione E5 del 15/10/2026 (F2.11c) l'aggancio è interno (visibile solo a ANTHEA.Testing e alle sue prove) e per contesto: un
/// AsyncLocal, ereditato dai thread e dalle attività avviati dopo la registrazione, così due catture in parallelo non si mescolano.
/// </summary>
internal static class JsonFallbackHook
{
    static readonly AsyncLocal<Action<string, JsonNode?, string, object>?> observer = new();

    /// <summary>Osservatore del contesto corrente: tipo ("S", "D", "B"), oggetto genitore, chiave e ripiego restituito; null in produzione.</summary>
    internal static Action<string, JsonNode?, string, object>? Observer { get => observer.Value; set => observer.Value = value; }

    internal static bool Enabled => observer.Value is not null;

    // Chiamato solo con un osservatore: il ripiego viene incapsulato (boxing) solo allora.
    internal static void Record(string kind, JsonNode? parent, string key, object fallback) => observer.Value?.Invoke(kind, parent, key, fallback);
}
