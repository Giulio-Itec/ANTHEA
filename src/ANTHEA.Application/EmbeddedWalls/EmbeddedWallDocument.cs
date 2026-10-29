using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using Anthea.Application.Stratigraphy;
using Anthea.Reports;

namespace Anthea.Application.EmbeddedWalls;

/// <summary>Versioned editable input. Elevations m; forces kN; moments kNm; EI kNm²;
/// pressures kPa; kh N/mm³. Numerical validation belongs to the GPC adapter/engine.
/// An empty activation prestress keeps the lock-off of the preceding stage (<see cref="LockOffs"/>).</summary>
public static partial class EmbeddedWallDocument
{
    public const string Module = "geo_paratie";
    /// <summary>Choices of hydraulic_left_boundary and hydraulic_right_boundary: the head of the stage on the outer vertical boundary, or no flow
    /// across it (the axis of a symmetric excavation).</summary>
    public const string FixedHeadBoundary = "Carico imposto", NoFlowBoundary = "Flusso nullo";
    public static IReadOnlyList<string> Tables { get; } = Array.AsReadOnly(new[] { "segments", "left_soils", "right_soils", "supports", "stages", "activations", "loads", "rc_sections", "hydraulic_left", "hydraulic_right", "hydraulic_upl", "hydraulic_actions", "hydraulic_hyd", "global_checks", "global_actions", "steel_members", "anchor_checks", "anchor_layers", "anchor_tests", "steel_tubes", "rc_piles", "distributed_loads", "cases" });
    public static JsonObject Create() => Upgrade(new()
    {
        ["schema"] = 1, ["name"] = "Paratia", ["case_id"] = "Caso 1", ["action_basis"] = "Da definire",
        ["top"] = 0, ["bottom"] = -12, ["width"] = 1, ["mesh"] = .25,
        ["fix_top_u"] = false, ["fix_top_rotation"] = false, ["fix_bottom_u"] = false, ["fix_bottom_rotation"] = false,
        ["check_kind"] = "Analisi", ["substeps"] = 8, ["segments"] = new JsonArray(NewRow("segments")),
        ["left_soils"] = new JsonArray(NewRow("left_soils")), ["right_soils"] = new JsonArray(NewRow("right_soils")),
        ["supports"] = new JsonArray(), ["stages"] = new JsonArray(NewRow("stages")),
        ["activations"] = new JsonArray(), ["loads"] = new JsonArray(), ["rc_sections"] = new JsonArray()
    });
    public static JsonObject NewRow(string table) => table switch
    {
        "cases" => NewCase(),
        "segments" => new() { ["section"] = "S1", ["top"] = 0, ["bottom"] = -12, ["ei_mode"] = "Assegnata", ["ei"] = null, ["weight"] = 0, ["source"] = "" },
        "left_soils" or "right_soils" => new() { ["name"] = "Strato", ["top"] = 0, ["bottom"] = -20, ["gamma"] = null, ["gamma_sat"] = null,
            ["phi"] = null, ["c"] = null, ["cu"] = null, ["k0"] = null, ["kh"] = null, ["drainage"] = "Drained", ["source"] = "", ["stiffness_source"] = "" },
        "supports" => new() { ["id"] = "T1", ["elevation"] = -1, ["kind"] = "Anchor", ["stiffness"] = null, ["angle"] = 0, ["direction"] = 1, ["spacing"] = 1, ["capacity"] = null },
        "stages" => new() { ["id"] = "F1", ["left_ground"] = 0, ["right_ground"] = 0, ["left_water"] = null, ["right_water"] = null,
            ["left_surcharge"] = 0, ["right_surcharge"] = 0, ["distributed_load"] = 0, ["stiffness_factor"] = 1 },
        "activations" => new() { ["stage"] = "F1", ["support"] = "T1", ["prestress"] = null },
        "loads" => new() { ["stage"] = "F1", ["elevation"] = 0, ["horizontal"] = 0, ["moment"] = 0, ["downward"] = 0 },
        "distributed_loads" => new() { ["stage"] = "F1", ["id"] = "Q1", ["top"] = 0, ["bottom"] = -12, ["q_top"] = null, ["q_bottom"] = null, ["source"] = "" },
        "rc_sections" => new() { ["id"] = "S1", ["width"] = 1000, ["thickness"] = 600, ["cover"] = 50,
            ["back_bars"] = 6, ["back_diameter"] = 20, ["front_bars"] = 6, ["front_diameter"] = 20,
            ["fck"] = 30, ["fyk"] = 450, ["es"] = 200000, ["eps_uk"] = .075, ["creep"] = 2, ["exposure"] = "XC2" },
        "hydraulic_left" or "hydraulic_right" => new() { ["name"] = "Strato idraulico", ["top"] = 0, ["bottom"] = -20,
            ["kxx"] = null, ["kzz"] = null, ["kxz"] = 0, ["alpha"] = null, ["n"] = null, ["source"] = "" },
        "hydraulic_upl" => new() { ["id"] = "UPL1", ["stage"] = "F1", ["water_resultant"] = "Assegnata nelle azioni", ["x_start"] = null, ["x_end"] = null,
            ["z"] = null, ["body_width"] = null, ["top_pressure"] = null, ["water_category"] = "PermanentStructural", ["source"] = "",
            ["ignore_suction"] = false, ["rd"] = 0, ["rd_source"] = "" },
        "hydraulic_actions" => new() { ["check"] = "UPL1", ["stage"] = "F1", ["id"] = "Peso", ["category"] = "PermanentStructural", ["upward_force"] = null, ["source"] = "" },
        "hydraulic_hyd" => new() { ["id"] = "HYD1", ["stage"] = "F1", ["method"] = "Mean", ["field_source"] = "FEM 2D", ["x"] = null, ["z"] = null, ["z_bottom"] = null,
            ["gradient"] = null, ["gamma_sat"] = null, ["pressure"] = null, ["hydrostatic_pressure"] = null, ["effective_stress"] = null,
            ["vertical_flow"] = false, ["outflow_confirmed"] = false, ["source"] = "" },
        "global_checks" => new() { ["id"] = "GLOB1", ["stage"] = "F1", ["standard"] = "NTC 2018", ["situation"] = "PersistentTransient",
            ["global_thickness"] = null, ["left_extent"] = null, ["right_extent"] = null, ["valley_direction"] = 1,
            ["characteristic_confirmed"] = false, ["source"] = "", ["undrained"] = false, ["ignore_suction"] = false,
            ["exit_min"] = null, ["exit_max"] = null, ["entry_min"] = null, ["entry_max"] = null,
            ["depth_min"] = null, ["depth_max"] = null, ["grid"] = 9, ["slices"] = 60, ["refinements"] = 4 },
        "global_actions" => new() { ["check"] = "GLOB1", ["stage"] = "F1", ["id"] = "LeftSurcharge", ["category"] = "Variable",
            ["unfavourable"] = true, ["psi"] = null, ["source"] = "" },
        "steel_members" => new() { ["support"] = "T1", ["family"] = "CHS", ["diameter"] = null, ["thickness"] = null,
            ["formation"] = "HotFinished", ["nominal_grade"] = null, ["fy"] = null, ["fu"] = null, ["elastic_modulus"] = null,
            ["material_source"] = "", ["net_area"] = null, ["net_area_source"] = "", ["length_1"] = null, ["length_2"] = null,
            ["length_source"] = "", ["axial_only"] = false, ["is_bridge"] = false },
        "anchor_checks" => new() { ["support"] = "T1", ["permanent"] = true, ["evidence"] = "CalculatedProfiles",
            ["free_length"] = null, ["bond_length"] = null, ["drill_diameter"] = null, ["minimum_cover"] = null, ["injection"] = "IRS",
            ["execution_source"] = "", ["applicability_source"] = "", ["experimental_source"] = "", ["stage_source"] = "" },
        "anchor_layers" => new() { ["support"] = "T1", ["profile"] = "P1", ["axis_start"] = null, ["axis_end"] = null,
            ["soil_kind"] = "MediumSand", ["limit_pressure"] = null, ["injection_pressure"] = null, ["diameter_factor"] = null, ["pressure_source"] = "" },
        "anchor_tests" => new() { ["support"] = "T1", ["test_id"] = "Prova 1", ["resistance"] = null, ["source"] = "" },
        "steel_tubes" => new() { ["id"] = "S1", ["diameter"] = null, ["thickness"] = null, ["spacing"] = null,
            ["nominal_grade"] = null, ["fy"] = null, ["fu"] = null, ["elastic_modulus"] = null, ["material_source"] = "",
            ["unperforated"] = false, ["single_row"] = false, ["representation_source"] = "", ["is_bridge"] = false },
        "rc_piles" => new() { ["id"] = "S1", ["diameter"] = null, ["spacing"] = null, ["cover"] = null,
            ["bar_count"] = null, ["bar_diameter"] = null, ["link_diameter"] = null, ["link_spacing"] = null,
            ["fck"] = null, ["fyk"] = null, ["es"] = 200000, ["eps_uk"] = .075, ["material_source"] = "",
            ["outline_divisions"] = 64, ["single_row"] = false, ["representation_source"] = "", ["shear_confirmed"] = false,
            ["lever_factor"] = .9, ["creep"] = 2, ["exposure"] = "XC2", ["aggregate"] = 20,
            ["durability_cover"] = null, ["cover_deviation"] = 10, ["detailing_mode"] = "CommonOnly",
            ["compression_restrained"] = false, ["end_zones_confirmed"] = false },
        _ => throw new ArgumentException("Tabella paratie sconosciuta: " + table)
    };
    public static JsonObject Snapshot(JsonObject source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Upgrade(source.DeepClone().AsObject());
    }
    /// <summary>Migrations add inactive analyses/checks without inventing engineering data. Existing saturation convention is retained.</summary>
    public static JsonObject Upgrade(JsonObject data)
    {
        Validate(data);
        if (StratigraphyDocument.Number(data["schema"], "Versione paratie") == 1)
        {
            var added = new JsonObject { ["hydraulic_mode"] = "Idrostatica", ["hydraulic_left_extent"] = null, ["hydraulic_right_extent"] = null,
                ["hydraulic_bottom"] = null, ["hydraulic_thickness"] = null, ["hydraulic_mesh"] = null, ["ignore_suction"] = false,
                ["hydraulic_iterations"] = new GPC.Checkers.Geotechnics.Seepage.SeepageOptions().MaximumIterations,
                ["hydraulic_left"] = new JsonArray(), ["hydraulic_right"] = new JsonArray() };
            foreach (var entry in added) if (!data.ContainsKey(entry.Key)) data[entry.Key] = entry.Value?.DeepClone();
            data["schema"] = 2;
        }
        if (StratigraphyDocument.Number(data["schema"], "Versione paratie") == 2)
        {
            var added = new JsonObject { ["hydraulic_weight_mode"] = "Quota di falda assegnata", ["hydraulic_standard"] = "NTC 2018",
                ["hydraulic_situation"] = "PersistentTransient", ["hydraulic_upl"] = new JsonArray(), ["hydraulic_actions"] = new JsonArray(), ["hydraulic_hyd"] = new JsonArray() };
            foreach (var entry in added) if (!data.ContainsKey(entry.Key)) data[entry.Key] = entry.Value?.DeepClone();
            data["schema"] = 3;
        }
        if (StratigraphyDocument.Number(data["schema"], "Versione paratie") == 3)
        {
            foreach (string key in new[] { "global_checks", "global_actions" }) if (!data.ContainsKey(key)) data[key] = new JsonArray();
            data["schema"] = 4;
        }
        if (StratigraphyDocument.Number(data["schema"], "Versione paratie") == 4)
        {
            if (!data.ContainsKey("steel_members")) data["steel_members"] = new JsonArray();
            if (!data.ContainsKey("steel_standard")) data["steel_standard"] = "Non richieste";
            if (!data.ContainsKey("steel_check_kind")) data["steel_check_kind"] = "Analysis";
            data["schema"] = 5;
        }
        if (StratigraphyDocument.Number(data["schema"], "Versione paratie") == 5)
        {
            foreach (string table in new[] { "anchor_checks", "anchor_layers", "anchor_tests" }) if (!data.ContainsKey(table)) data[table] = new JsonArray();
            if (!data.ContainsKey("anchor_standard")) data["anchor_standard"] = "Non richieste";
            if (!data.ContainsKey("anchor_situation")) data["anchor_situation"] = "PersistentTransient";
            if (!data.ContainsKey("anchor_a1m1")) data["anchor_a1m1"] = false;
            data["schema"] = 6;
        }
        if (StratigraphyDocument.Number(data["schema"], "Versione paratie") == 6)
        {
            if (!data.ContainsKey("steel_tubes")) data["steel_tubes"] = new JsonArray();
            if (!data.ContainsKey("tube_standard")) data["tube_standard"] = "Non richieste";
            if (!data.ContainsKey("tube_check_kind")) data["tube_check_kind"] = "Analysis";
            foreach (var row in Rows(data, "segments")) if (!row.ContainsKey("ei_mode")) row["ei_mode"] = "Assegnata";
            data["schema"] = 7;
        }
        if (StratigraphyDocument.Number(data["schema"], "Versione paratie") == 7)
        {
            if (!data.ContainsKey("rc_piles")) data["rc_piles"] = new JsonArray();
            if (!data.ContainsKey("rc_pile_standard")) data["rc_pile_standard"] = "Non richieste";
            if (!data.ContainsKey("rc_pile_check_kind")) data["rc_pile_check_kind"] = "Analisi";
            data["schema"] = 8;
        }
        if (StratigraphyDocument.Number(data["schema"], "Versione paratie") == 8)
        {
            if (!data.ContainsKey("distributed_loads")) data["distributed_loads"] = new JsonArray();
            data["schema"] = 9;
        }
        if (StratigraphyDocument.Number(data["schema"], "Versione paratie") == 9)
        {
            foreach (string key in new[] { "embedment_reference", "embedment_minimum", "embedment_maximum", "embedment_increment", "embedment_head_limit" })
                if (!data.ContainsKey(key)) data[key] = null;
            if (!data.ContainsKey("embedment_source")) data["embedment_source"] = "";
            data["schema"] = 10;
        }
        if (StratigraphyDocument.Number(data["schema"], "Versione paratie") == 10)
        {
            if (!data.ContainsKey("cases")) data["cases"] = new JsonArray();
            data["schema"] = 11;
        }
        // Lateral boundaries of the FEM seepage (P5 of the paratie review, 22/10/2026): the documents before kept the fixed head on both sides.
        if (StratigraphyDocument.Number(data["schema"], "Versione paratie") == 11)
        {
            foreach (string key in new[] { "hydraulic_left_boundary", "hydraulic_right_boundary" }) if (!data.ContainsKey(key)) data[key] = FixedHeadBoundary;
            data["schema"] = 12;
        }
        Validate(data);
        return data;
    }
    public static void Validate(JsonObject data)
    {
        var schema = StratigraphyDocument.Number(data["schema"], "Versione paratie");
        if (schema is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12)) throw new ArgumentException("Versione del documento paratie non supportata.");
        foreach (var name in Tables)
        {
            if (schema < 11 && name == "cases") continue;
            if (schema < 9 && name == "distributed_loads") continue;
            if (schema < 8 && name == "rc_piles") continue;
            if (schema < 7 && name == "steel_tubes") continue;
            if (schema < 6 && name is ("anchor_checks" or "anchor_layers" or "anchor_tests")) continue;
            if (schema < 5 && name == "steel_members") continue;
            if (schema < 4 && name is ("global_checks" or "global_actions")) continue;
            if (schema == 1 && name is ("hydraulic_left" or "hydraulic_right")) continue;
            if (schema < 3 && name is ("hydraulic_upl" or "hydraulic_actions" or "hydraulic_hyd")) continue;
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
            + typeof(GPC.Checkers.Steel.Members.Ntc2018AxialSteel).Assembly.GetName().Version + "/"
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
        ["stato"] = result.Status, ["impronta"] = result.Fingerprint, ["fasi_complete"] = result.Cases?.Sum(c => c.Stages.Count) ?? result.Stages.Count,
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
