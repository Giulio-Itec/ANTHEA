using System.Text.Json.Nodes;
using X.Core;
namespace Anthea.Application.Concrete;

/// <summary>Single persisted action set. Projections route inputs, never combine or factor forces.</summary>
public static class SectionActionInputs
{
    public const string VersionKey = "sollecitazioni_versione";
    public static readonly string[] Components = ["N", "Mx", "My", "Vx", "Vy", "T"];
    public static bool Unified(JsonObject data) => data["workspace_ca"]?[VersionKey]?.ToString() == "1";
    public static string StateLabel(string key) => key switch { "SLE" => "Rara", "SLE_FREQ" => "Frequente", "SLE_QP" => "Quasi permanente", _ => key };
    public static string Label(string key, JsonObject settings) => settings[VersionKey]?.ToString() == "1" ? StateLabel(key) : SectionWorkspace.Label(key);
    public static string ActionLabel(JsonObject row) => row.S("elemento") is { Length: > 0 } element ? element + " · " + row.S("nome") : row.S("nome");
    public static JsonObject[] ShearRows(JsonObject data)
    {
        if (!Unified(data)) return data["workspace_ca"]!["taglio"]!.Array("azioni").OfType<JsonObject>().Select(r => (JsonObject)r.DeepClone()).ToArray();
        var actions = ConcreteSheetPreparation.ReadActions(data);
        return new[] { "SLU", "SLV" }.SelectMany(key => actions[key].Select(row => { row["stato"] = key; return row; })).ToArray();
    }
    // The native reinforcement optimizer still consumes its historical shear contract.
    // Materialize that adapter only in a private calculation copy, never in saved input.
    public static JsonObject CalculationCopy(JsonObject source)
    {
        var copy = (JsonObject)source.DeepClone();
        if (Unified(copy)) copy["workspace_ca"]!["taglio"]!["azioni"] = new JsonArray(ShearRows(copy).Select(r => (JsonNode)r).ToArray());
        return copy;
    }
    public static JsonObject Initialize(JsonObject data)
    {
        SectionWorkspace.Prepare(data);
        Replace(data, []);
        return data;
    }
    public static void Replace(JsonObject data, IEnumerable<JsonObject> source)
    {
        var rows = source.Select(r => (JsonObject)r.DeepClone()).ToArray();
        if (rows.Length > 10000) throw new ArgumentException("Massimo 10.000 combinazioni.");
        var ids = new HashSet<string>();
        foreach (var row in rows)
        {
            if (!SectionWorkspace.Sets.Contains(row.S("stato"))) throw new ArgumentException("Scegliere lo stato limite per " + row.S("nome") + ".");
            if (string.IsNullOrWhiteSpace(row.S("nome"))) throw new ArgumentException("Nome combinazione mancante.");
            foreach (string key in Components) SectionWorkspace.Number(row.S(key), row.S("nome") + " · " + key);
            if (row.S("id").Length == 0) row["id"] = Guid.NewGuid().ToString("N");
            if (!ids.Add(row.S("id"))) throw new ArgumentException("Identificativo combinazione duplicato.");
        }
        // All validation precedes mutation; same names for different elements remain distinct rows.
        var sets = new JsonObject();
        foreach (string key in SectionWorkspace.Sets) sets[key] = ConcreteSheetPreparation.StoreActions(rows.Where(r => r.S("stato") == key));
        var settings = data["workspace_ca"]!.AsObject();
        data["combinazioni"] = sets;
        settings[VersionKey] = 1;
        settings["taglio"]!["azioni"] = new JsonArray();
        foreach (string name in new[] { "dominio3d", "dominio2d" })
        {
            var options = settings[name]!;
            if (options["modello_resistente"] is null) options["modello_resistente"] = options.S("stato") == "SLV" ? "Elastico" : "Plastico";
        }
    }
}
