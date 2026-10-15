using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Anthea.Application.Stratigraphy;

/// <summary>Editable ANTHEA document, not a constitutive soil model or a calculation engine.
/// Lengths are metres, unit weights kN/m³, strengths kPa and angles degrees.</summary>
public static class StratigraphyDocument
{
    public const string Module = "geo_stratigrafia";
    public static readonly string[] Parameters = ["gamma", "gamma_sat", "phi", "c", "cu"];
    public static readonly string[] WaterStates = ["Non nota", "Non considerata", "Assegnata"];
    public static readonly string[] Qualifications = ["Non qualificato", "Misurato", "Interpretato", "Caratteristico"];
    public static readonly string[] Scenarios = ["Medi", "Minimi", "Entrambi"];
    // Descriptive families for graphic identification only, not a granulometric classification or material catalogue.
    public static readonly string[] SoilTypes = ["Non classificato", "Riporto", "Ghiaia", "Sabbia", "Limo", "Argilla", "Terreno organico", "Roccia", "Altro"];
    public static string SoilType(JsonObject layer) => layer["soil_type"]?.ToString() ?? "Non classificato";
    public static JsonObject NewMinimum() => new()
    {
        ["gamma"] = null, ["gamma_sat"] = null, ["phi"] = null, ["c"] = null, ["cu"] = null,
        ["source"] = "", ["qualification"] = "Non qualificato", ["notes"] = ""
    };
    public static JsonObject NewLayer() => new()
    {
        ["id"] = Guid.NewGuid().ToString("N"), ["name"] = "Nuovo strato", ["description"] = "",
        ["color"] = "#D5BE92", ["soil_type"] = "Non classificato", ["behavior"] = "Da definire", ["thickness"] = 1.0, ["infinite"] = false,
        ["gamma"] = null, ["gamma_sat"] = null, ["phi"] = null, ["c"] = null, ["cu"] = null,
        ["source"] = "", ["qualification"] = "Non qualificato", ["notes"] = "", ["minimum"] = null
    };
    public static JsonObject Create() => new()
    {
        ["schema"] = 2, ["scenario"] = "Medi", ["id"] = Guid.NewGuid().ToString("N"), ["name"] = "Stratigrafia",
        ["description"] = "", ["ground_elevation"] = null, ["water_state"] = "Non nota",
        ["water_depth"] = null, ["layers"] = new JsonArray(NewLayer())
    };
    public static JsonArray Layers(JsonObject data) => data["layers"] as JsonArray
        ?? throw new ArgumentException("Elenco degli strati mancante.");
    public static double? Number(JsonNode? value, string label)
    {
        if (value is null || value is JsonValue text && text.TryGetValue<string>(out var s) && string.IsNullOrWhiteSpace(s)) return null;
        if (value is not JsonValue || !double.TryParse(value.ToString().Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
            throw new ArgumentException(label + ": inserire un numero finito.");
        return number;
    }
    public static bool Infinite(JsonObject layer) => layer["infinite"] is JsonValue value && value.TryGetValue<bool>(out bool flag)
        ? flag : throw new ArgumentException("Estensione dello strato non valida.");
    private static string Text(JsonObject data, string key) => data[key] is JsonValue value && value.TryGetValue<string>(out var text)
        ? text : throw new ArgumentException("Testo mancante o non valido: " + key);
    public static void Validate(JsonObject data)
    {
        if (Number(data["schema"], "Versione") is not (1 or 2)) throw new ArgumentException("Versione della stratigrafia non supportata.");
        if (!Scenarios.Contains(Scenario(data))) throw new ArgumentException("Scelta dei parametri non valida.");
        if (!Guid.TryParse(Text(data, "id"), out _)) throw new ArgumentException("Identificativo della stratigrafia non valido.");
        if (string.IsNullOrWhiteSpace(Text(data, "name"))) throw new ArgumentException("Assegnare un nome alla stratigrafia.");
        Text(data, "description"); Number(data["ground_elevation"], "Quota piano campagna");
        string state = Text(data, "water_state");
        if (!WaterStates.Contains(state)) throw new ArgumentException("Stato della falda non valido.");
        double? depth = Number(data["water_depth"], "Profondità falda");
        if (state == "Assegnata" && depth is null) throw new ArgumentException("Assegnare la profondità della falda.");
        if (depth < 0) throw new ArgumentException("Il modello semplice ammette la falda al piano campagna o sotto di esso.");
        var layers = Layers(data);
        if (layers.Count is < 1 or > 100) throw new ArgumentException("La stratigrafia deve contenere da 1 a 100 strati.");
        var ids = new HashSet<Guid>(); double total = 0;
        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i] as JsonObject ?? throw new ArgumentException("Strato non valido.");
            if (!Guid.TryParse(Text(layer, "id"), out var id) || !ids.Add(id)) throw new ArgumentException("Identificativo dello strato mancante o duplicato.");
            if (string.IsNullOrWhiteSpace(Text(layer, "name"))) throw new ArgumentException($"Assegnare un nome allo strato {i + 1}.");
            if (!SoilTypes.Contains(SoilType(layer))) throw new ArgumentException($"Strato {i + 1}: famiglia di terreno non valida.");
            if (layer["behavior"] is not null && layer["behavior"]!.ToString() is not ("Da definire" or "Granulare" or "Coesivo"))
                throw new ArgumentException($"Strato {i + 1}: comportamento per il trasferimento non valido.");
            foreach (string key in new[] { "description", "source", "notes" }) Text(layer, key);
            if (!Qualifications.Contains(Text(layer, "qualification"))) throw new ArgumentException("Qualificazione dei dati non valida.");
            string color = Text(layer, "color");
            if (color.Length != 7 || color[0] != '#' || !color.Skip(1).All(Uri.IsHexDigit)) throw new ArgumentException("Colore: usare #RRGGBB.");
            bool infinite = Infinite(layer);
            if (infinite && i != layers.Count - 1) throw new ArgumentException("Solo l’ultimo strato può essere indefinito.");
            double? thickness = Number(layer["thickness"], "Spessore");
            if (!infinite && (thickness is null || thickness <= 0)) throw new ArgumentException($"Strato {i + 1}: lo spessore deve essere positivo.");
            if (infinite && thickness is not null) throw new ArgumentException("Uno strato indefinito non ha spessore finito.");
            if (!infinite) total += thickness!.Value;
            if (!double.IsFinite(total)) throw new ArgumentException("Profondità totale fuori campo numerico.");
            foreach (string key in Parameters)
            {
                double? number = Number(layer[key], key);
                if (number < 0 || (key is "gamma" or "gamma_sat") && number == 0 || key == "phi" && number >= 90)
                    throw new ArgumentException($"Strato {i + 1}: parametro {key} fuori campo fisico.");
            }
            if (layer["minimum"] is not null)
            {
                var minimum = layer["minimum"] as JsonObject ?? throw new ArgumentException("Parametri minimi non validi.");
                foreach (string key in new[] { "source", "notes" }) Text(minimum, key);
                if (!Qualifications.Contains(Text(minimum, "qualification"))) throw new ArgumentException("Qualificazione dei minimi non valida.");
                foreach (string key in Parameters)
                {
                    double? number = Number(minimum[key], key);
                    if (number < 0 || (key is "gamma" or "gamma_sat") && number == 0 || key == "phi" && number >= 90)
                        throw new ArgumentException($"Strato {i + 1}: minimo {key} fuori campo fisico.");
                }
            }
        }
    }
    public static IReadOnlyList<string> Warnings(JsonObject data)
    {
        Validate(data); var messages = new List<string>();
        if (data["water_state"]!.ToString() == "Non nota") messages.Add("Livello della falda non noto.");
        foreach (var layer in Layers(data).OfType<JsonObject>())
        {
            if (Parameters.Any(key => Number(layer[key], key) is null)) messages.Add(layer["name"] + ": parametri incompleti; salvabile, da completare per la verifica richiesta.");
            if (Number(layer["gamma_sat"], "γsat") < Number(layer["gamma"], "γ")) messages.Add(layer["name"] + ": γsat minore di γ; controllare i dati e le condizioni di misura.");
            if (Scenario(data) != "Medi" && (layer["minimum"] is not JsonObject minima || Parameters.Any(p => Number(minima[p], p) is null)))
                messages.Add(layer["name"] + ": minimi selezionati ma incompleti; nessuna sostituzione con i medi.");
            if (layer["minimum"] is JsonObject minimum)
                foreach (string key in Parameters)
                    if (Number(minimum[key], key) > Number(layer[key], key)) messages.Add(layer["name"] + $": minimo {key} maggiore del medio; verificare fonte e significato.");
        }
        return messages;
    }
    public static string Scenario(JsonObject data) => data["scenario"]?.ToString() ?? "Medi";

    /// <summary>Future consumers specify their required parameters; means remain mandatory even for minimum-only use.</summary>
    public static IReadOnlyDictionary<string, JsonObject> ForVerification(JsonObject data, params string[] required)
    {
        Validate(data);
        if (required.Length == 0 || required.Any(p => !Parameters.Contains(p))) throw new ArgumentException("Specificare i parametri necessari alla verifica.");
        string scenario = Scenario(data);
        foreach (var layer in Layers(data).OfType<JsonObject>())
            foreach (string key in required)
            {
                if (Number(layer[key], key) is null) throw new ArgumentException(layer["name"] + $": medio {key} obbligatorio.");
                if (scenario != "Medi" && (layer["minimum"] is not JsonObject minimum || Number(minimum[key], key) is null))
                    throw new ArgumentException(layer["name"] + $": minimo {key} obbligatorio per la scelta {scenario}.");
            }
        var result = new Dictionary<string, JsonObject>();
        foreach (string selected in scenario == "Entrambi" ? new[] { "Medi", "Minimi" } : new[] { scenario })
        {
            var copy = data.DeepClone().AsObject(); copy["scenario"] = selected;
            if (selected == "Minimi")
                foreach (var layer in Layers(copy).OfType<JsonObject>())
                    foreach (string key in Parameters.Concat(new[] { "source", "qualification", "notes" }))
                        layer[key] = layer["minimum"]![key]?.DeepClone();
            result.Add(selected, copy);
        }
        return result;
    }
    public static void Add(JsonObject data)
    {
        var layers = Layers(data);
        if (layers.Count >= 100) throw new ArgumentException("Massimo 100 strati.");
        int index = layers.Count > 0 && Infinite(layers[^1]!.AsObject()) ? layers.Count - 1 : layers.Count;
        layers.Insert(index, NewLayer());
    }
    public static void Duplicate(JsonObject data, int index)
    {
        var layers = Layers(data);
        if (layers.Count >= 100) throw new ArgumentException("Massimo 100 strati.");
        var copy = layers[index]!.DeepClone().AsObject();
        if (Infinite(copy)) throw new ArgumentException("Rendere finito lo strato prima di duplicarlo.");
        copy["id"] = Guid.NewGuid().ToString("N"); copy["name"] = copy["name"] + " (copia)";
        layers.Insert(index + 1, copy);
    }
    public static void Remove(JsonObject data, int index)
    {
        var layers = Layers(data);
        if (layers.Count == 1) throw new ArgumentException("Conservare almeno uno strato.");
        layers.RemoveAt(index);
    }
    public static void Move(JsonObject data, int index, int destination)
    {
        var layers = Layers(data);
        if (index < 0 || destination < 0 || index >= layers.Count || destination >= layers.Count) throw new ArgumentException("Posizione dello strato non valida.");
        if (index == destination) return;
        if (Infinite(layers[index]!.AsObject()) || Infinite(layers[destination]!.AsObject())) throw new ArgumentException("Lo strato indefinito deve restare in fondo.");
        var layer = layers[index]; layers.RemoveAt(index); layers.Insert(destination, layer);
    }
    /// <summary>Owned snapshot for future consumers. No hidden defaults, factors or copies into calculation modules.</summary>
    public static StratigraphySnapshot Snapshot(JsonObject data)
    {
        Validate(data);
        var json = data.ToJsonString();
        return new(data["id"]!.ToString(), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))), json);
    }
}

/// <summary>Immutable document contract. Consumers retain Id and Fingerprint with their results.</summary>
public sealed record StratigraphySnapshot(string Id, string Fingerprint, string Json)
{
    public JsonObject Read() => JsonNode.Parse(Json)!.AsObject();
}
