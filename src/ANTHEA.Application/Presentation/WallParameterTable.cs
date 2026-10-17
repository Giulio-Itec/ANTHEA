using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Modules;
namespace Anthea.Application.Presentation;

/// <summary>Editable parameter projection over an owned wall snapshot. Applying values never
/// changes the source document; validation uses the existing module service.</summary>
public sealed class WallParameterTable
{
    private sealed record Cell(JsonObject Row, string Path, JsonNode? Original);
    private readonly JsonObject source;
    private readonly List<Cell> cells = [];
    public IReadOnlyList<JsonObject> Rows => cells.Select(c => c.Row).ToArray();
    public WallParameterTable(JsonObject source)
    {
        this.source = (JsonObject)source.DeepClone();
        var known = RetainingWall.GeometryFields.Concat(RetainingWall.FoundationFields).Concat(RetainingWall.MaterialFields).Concat(RetainingWall.SeismicFields).GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.First());
        var labels = new Dictionary<string, string> { ["thickness"] = "Spessore strato", ["phi"] = "Angolo di attrito terreno", ["free_height"] = "Altezza libera dalla sommità a valle", ["mobilization"] = "Frazione passiva mobilitata 0–0,5", ["wall_delta"] = "Attrito muro assegnato", ["wall_phi_cv"] = "φcv,k contatto muro", ["base_phi_cv"] = "φcv,k contatto fondazione", ["wall_mode"] = "Attrito muro: " + string.Join(" / ", RetainingWall.FrictionModes), ["base_mode"] = "Attrito base: " + string.Join(" / ", RetainingWall.FrictionModes), ["height_mode"] = "Interamente libero / Assegnato", ["mphi"] = "γM sulla tangente di φ", ["rslide"] = "γR scorrimento", ["rover"] = "γR ribaltamento", ["rbearing"] = "γR portanza", ["rpassive"] = "γR,e passiva a valle", ["heel_surcharge"] = "q variabile sulla mensola (true/false)" };
        string GroupName(string path)
        {
            var parts = path.Split('/'); string root = parts[0] switch { "geometry" => "Geometria", "layers" => "Strati monte", "valley" => "Terreno valle", "interfaces" => "Attriti", "foundation" => "Terreno di posa", "materials" => "Materiali", "water" => "Falda", "seismic" => "Sisma", "reinforcement" => "Armature", "actions" => "Azioni", "combinations" => "Combinazioni", "global_stability" => "Stabilità globale", _ => parts[0] };
            return root + (parts.Length > 1 ? " · " + string.Join(" · ", parts.Skip(1).Select(p => int.TryParse(p, out int n) ? (n+1).ToString() : p)) : "");
        }
        foreach (var item in new[] { ("name", "Descrizione"), ("linked", "Modifica insieme le colonne (true/false)"), ("passive", "Usa passiva (true/false)"), ("enabled", "Attivo (true/false)"), ("visible", "Disegna (true/false)"), ("gamma_sat", "Peso specifico saturo"), ("soil", "Fattore del peso terreno monte"), ("valley_soil", "Fattore del peso terreno valle"), ("wall", "Fattore del peso muro"), ("water", "Fattore delle pressioni idrauliche"), ("bottom", "Quota del fondo strato"), ("soil_mode", "Profilo unico / Due colonne"), ("soil_split_x", "Ascissa del confine valle / monte"), ("profile_confirmed", "Profilo confermato (true/false)"), ("front_head", "Battente a valle dal piano di posa"), ("depth", "Profondità della falda da monte"), ("source", "Origine dei coefficienti sismici"), ("method", "Metodo delle spinte sismiche"), ("value", "Intensità dell’azione"), ("type", "Tipo di azione"), ("category", "Categoria dell’azione"), ("state", "Stato limite"), ("approach", "Approccio di verifica"), ("condition", "Condizione drenata / non drenata"), ("two_zones", "Due zone armature (true/false)"), ("lower_height", "Altezza armatura inferiore"), ("diameter", "Diametro barre"), ("count", "Barre per metro e per faccia") }) labels[item.Item1] = item.Item2;
        void Walk(JsonNode? node, string path)
        {
            if (node is JsonArray array) { for (int i = 0; i < array.Count; i++) Walk(array[i], path + "/" + i); return; }
            if (node is not JsonObject obj) return;
            foreach (var pair in obj)
            {
                if (pair.Key.StartsWith("__") || pair.Key is "id" or "combination_signature") continue;
                string key = path + "/" + pair.Key;
                if (pair.Value is JsonObject or JsonArray) { Walk(pair.Value, key); continue; }
                var p = known.GetValueOrDefault(pair.Key); var row = J.Obj(("path", key), ("group", GroupName(path)), ("meaning", labels.GetValueOrDefault(pair.Key, p?.Label ?? pair.Key)), ("value", pair.Value?.ToString() ?? ""), ("unit", p?.Unit ?? ""));
                cells.Add(new(row, key, pair.Value?.DeepClone()));
            }
        }
        foreach (string group in new[] { "geometry", "layers", "valley", "interfaces", "foundation", "materials", "water", "seismic", "reinforcement", "actions", "combinations", "global_stability" }) Walk(this.source[group], group);
    }
    public JsonObject CreateSnapshot()
    {
        var snapshot = (JsonObject)source.DeepClone();
        bool comboEdit = false, globalEdit = false, rearEdit = false, frontEdit = false;
        foreach (var b in cells)
        {
            var parts = b.Path.Split('/'); JsonNode owner = snapshot;
            foreach (string part in parts[..^1]) owner = owner is JsonArray array ? array[int.Parse(part, CultureInfo.InvariantCulture)]! : owner[part]!;
            string key = parts[^1];
            string value = b.Row.S("value"); if (value == (b.Original?.ToString() ?? "")) { owner[key] = b.Original?.DeepClone(); continue; }
            rearEdit |= b.Path.StartsWith("layers/"); frontEdit |= b.Path.StartsWith("valley/layers/");
            if (b.Original is JsonValue flag && flag.TryGetValue<bool>(out _)) owner[key] = bool.TryParse(value, out bool enabled) ? enabled : throw new ArgumentException(b.Path + ": true o false.");
            else if (double.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) { if (!double.IsFinite(number)) throw new ArgumentException("Valore non finito."); owner[key] = number; }
            else owner[key] = value;
            comboEdit |= b.Path.StartsWith("combinations/"); globalEdit |= b.Path.StartsWith("global_stability/combinations/");
        }
        if (snapshot["valley"].B("linked")) { if (frontEdit && rearEdit) throw new ArgumentException("Colonne collegate: modificare una sola colonna, oppure disattivare il collegamento."); RetainingWall.CopySoilColumn(snapshot, frontEdit); }
        if (comboEdit) { snapshot["combination_mode"] = "Personalizzate"; snapshot["combination_signature"] = RetainingWall.CombinationSignature(snapshot); }
        if (globalEdit) { snapshot["global_stability"]!["combination_mode"] = "Personalizzate"; snapshot["global_stability"]!["combination_signature"] = RetainingWall.GlobalSignature(snapshot); }
        return snapshot;
    }
    /// <summary>
    /// The check of <see cref="ValidateAsync"/> on a snapshot already created (<see cref="CreateSnapshot"/>), on the calling thread: the
    /// engine of the module calculates it; missing data or an error of the global stability are thrown as ArgumentException. Used by the
    /// ViewModel of the window of the values (F5.9), which runs it on a background thread with its own calculation.
    /// </summary>
    public static JsonObject Validate(IModuleService service, JsonObject snapshot, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (service.ModuleId != RetainingWall.Module) throw new ArgumentException("Servizio del muro richiesto.");
        var calculation = service.Calculate(snapshot, cancellation);
        cancellation.ThrowIfCancellationRequested();
        if (calculation.HasError) throw new ArgumentException(calculation.Values.S("errore"));
        if (calculation.NativeAs<RetainingWall.Result>().GlobalError is { } error) throw new ArgumentException(error);
        return snapshot;
    }
    public async Task<JsonObject> ValidateAsync(IModuleService service, CancellationToken cancellation = default)
    {
        if (service.ModuleId != RetainingWall.Module) throw new ArgumentException("Servizio del muro richiesto.");
        var snapshot = CreateSnapshot(); cancellation.ThrowIfCancellationRequested();
        var calculation = await Task.Run(() => service.Calculate(snapshot, cancellation), cancellation);
        cancellation.ThrowIfCancellationRequested();
        if (calculation.HasError) throw new ArgumentException(calculation.Values.S("errore"));
        if (calculation.NativeAs<RetainingWall.Result>().GlobalError is { } error) throw new ArgumentException(error);
        return snapshot;
    }
}
