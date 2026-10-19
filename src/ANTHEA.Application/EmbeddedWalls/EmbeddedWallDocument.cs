using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using Anthea.Application.Stratigraphy;
using Anthea.Reports;

namespace Anthea.Application.EmbeddedWalls;

/// <summary>Versioned editable input. Elevations m; forces kN; moments kNm; EI kNm²;
/// pressures kPa; kh N/mm³. Numerical validation belongs to the GPC adapter/engine.</summary>
public static class EmbeddedWallDocument
{
    public const string Module = "geo_paratie";
    public static readonly string[] Tables = ["segments", "left_soils", "right_soils", "supports", "stages", "activations", "loads", "rc_sections"];
    public static JsonObject Create() => new()
    {
        ["schema"] = 1, ["name"] = "Paratia", ["case_id"] = "Caso 1", ["action_basis"] = "Da definire",
        ["top"] = 0, ["bottom"] = -12, ["width"] = 1, ["mesh"] = .25,
        ["fix_top_u"] = false, ["fix_top_rotation"] = false, ["fix_bottom_u"] = false, ["fix_bottom_rotation"] = false,
        ["check_kind"] = "Analisi", ["substeps"] = 8, ["segments"] = new JsonArray(NewRow("segments")),
        ["left_soils"] = new JsonArray(NewRow("left_soils")), ["right_soils"] = new JsonArray(NewRow("right_soils")),
        ["supports"] = new JsonArray(), ["stages"] = new JsonArray(NewRow("stages")),
        ["activations"] = new JsonArray(), ["loads"] = new JsonArray(), ["rc_sections"] = new JsonArray()
    };
    public static JsonObject NewRow(string table) => table switch
    {
        "segments" => new() { ["section"] = "S1", ["top"] = 0, ["bottom"] = -12, ["ei"] = null, ["weight"] = 0, ["source"] = "" },
        "left_soils" or "right_soils" => new() { ["name"] = "Strato", ["top"] = 0, ["bottom"] = -20, ["gamma"] = null, ["gamma_sat"] = null,
            ["phi"] = null, ["c"] = null, ["cu"] = null, ["k0"] = null, ["kh"] = null, ["drainage"] = "Drained", ["source"] = "", ["stiffness_source"] = "" },
        "supports" => new() { ["id"] = "T1", ["elevation"] = -1, ["kind"] = "Anchor", ["stiffness"] = null, ["angle"] = 0, ["direction"] = 1, ["spacing"] = 1, ["capacity"] = null },
        "stages" => new() { ["id"] = "F1", ["left_ground"] = 0, ["right_ground"] = 0, ["left_water"] = null, ["right_water"] = null,
            ["left_surcharge"] = 0, ["right_surcharge"] = 0, ["distributed_load"] = 0, ["stiffness_factor"] = 1 },
        "activations" => new() { ["stage"] = "F1", ["support"] = "T1", ["prestress"] = 0 },
        "loads" => new() { ["stage"] = "F1", ["elevation"] = 0, ["horizontal"] = 0, ["moment"] = 0, ["downward"] = 0 },
        "rc_sections" => new() { ["id"] = "S1", ["width"] = 1000, ["thickness"] = 600, ["cover"] = 50,
            ["back_bars"] = 6, ["back_diameter"] = 20, ["front_bars"] = 6, ["front_diameter"] = 20,
            ["fck"] = 30, ["fyk"] = 450, ["es"] = 200000, ["eps_uk"] = .075, ["creep"] = 2, ["exposure"] = "XC2" },
        _ => throw new ArgumentException("Tabella paratie sconosciuta: " + table)
    };
    public static JsonObject Snapshot(JsonObject source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var copy = source.DeepClone().AsObject(); Validate(copy); return copy;
    }
    public static void Validate(JsonObject data)
    {
        if (StratigraphyDocument.Number(data["schema"], "Versione paratie") != 1) throw new ArgumentException("Versione del documento paratie non supportata; nessuna migrazione implicita.");
        foreach (var name in Tables)
        {
            if (data[name] is not JsonArray rows || rows.Any(r => r is not JsonObject)) throw new ArgumentException("Tabella mancante o non valida: " + name);
            if (rows.Count > 10000) throw new ArgumentException("Tabella troppo estesa: " + name);
        }
    }
    public static string Fingerprint(JsonObject data)
    {
        var input = data.DeepClone().AsObject(); input.Remove("result_snapshot");
        string engine = typeof(GPC.Checkers.Geotechnics.EmbeddedWalls.EmbeddedWallAnalysis).Assembly.GetName().Version + "/"
            + typeof(GPC.Design.EmbeddedWalls.EmbeddedWallConcrete).Assembly.GetName().Version + "/"
            + typeof(GPC.Checkers.Concrete.Walls.WallSectionSession).Assembly.GetName().Version + "/"
            + typeof(GPC.Model.Geotechnics.EmbeddedWalls.EmbeddedWallModel).Assembly.GetName().Version;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(engine + "\n" + input.ToJsonString())));
    }
    public static void StoreResult(JsonObject data, EmbeddedWallReportData result)
    {
        if (result.Fingerprint != Fingerprint(data)) throw new ArgumentException("Il risultato appartiene a input o librerie diversi.");
        data["result_snapshot"] = new JsonObject { ["schema"] = 1, ["value"] = JsonSerializer.SerializeToNode(result) };
    }
    public static JsonObject ResultValues(EmbeddedWallReportData result) => new()
    {
        ["stato"] = result.Status, ["impronta"] = result.Fingerprint, ["fasi_complete"] = result.Stages.Count,
        ["errore"] = result.Complete ? "" : "Analisi incompleta: " + result.Diagnostic,
        ["risultato"] = JsonSerializer.SerializeToNode(result)
    };
    public static EmbeddedWallReportData? ReadCurrentResult(JsonObject data)
    {
        if (data["result_snapshot"] is not JsonObject saved || saved["schema"]?.ToString() != "1") return null;
        try
        {
            var result = saved["value"]?.Deserialize<EmbeddedWallReportData>();
            return result is not null && result.Fingerprint == Fingerprint(data) && result.Stages is not null && result.Checks is not null && result.Scope is not null ? result : null;
        }
        catch (JsonException) { return null; }
    }
    public static IEnumerable<JsonObject> Rows(JsonObject data, string table) => data[table]!.AsArray().Cast<JsonObject>();

    /// <summary>Copy a qualified soil profile, retaining provenance and explicit elevations.
    /// K0 and kh remain unassigned: neither lithology nor transferred strength determines them.</summary>
    public static JsonObject WithStratigraphy(JsonObject source, JsonObject stratigraphy, string side, string scenario)
    {
        if (side is not ("left_soils" or "right_soils")) throw new ArgumentException("Scegliere il lato sinistro o destro.");
        var result = Snapshot(source); var selected = StratigraphyDocument.Snapshot(stratigraphy).Read(); selected["scenario"] = scenario;
        if (scenario is not ("Medi" or "Minimi")) throw new ArgumentException("Scegliere un solo insieme di parametri per ciascuna analisi.");
        var profile = StratigraphyDocument.ForVerification(selected, "gamma", "gamma_sat", "phi", "c")[scenario];
        double top = StratigraphyDocument.Number(profile["ground_elevation"], "Quota della stratigrafia") ?? throw new ArgumentException("Assegnare la quota della stratigrafia.");
        double toe = StratigraphyDocument.Number(result["bottom"], "Piede paratia") ?? throw new ArgumentException("Assegnare il piede paratia.");
        var rows = new JsonArray();
        foreach (var layer in StratigraphyDocument.Layers(profile).Cast<JsonObject>())
        {
            var row = NewRow(side); row["top"] = top;
            double bottom = StratigraphyDocument.Infinite(layer) ? Math.Min(toe, top - 1) : top - (StratigraphyDocument.Number(layer["thickness"], "Spessore") ?? throw new ArgumentException("Spessore mancante."));
            row["bottom"] = bottom;
            foreach (string key in new[] { "name", "gamma", "gamma_sat", "phi", "c", "cu", "source" }) row[key] = layer[key]?.DeepClone();
            rows.Add(row); top = bottom;
        }
        result[side] = rows;
        result["soil_transfer_" + side] = new JsonObject { ["source"] = profile.DeepClone(), ["scenario"] = scenario };
        return result;
    }
}
