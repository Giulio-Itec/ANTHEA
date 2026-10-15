using System.Text;
using System.Text.Json.Nodes;
using Anthea.Calculations;

namespace Anthea.Testing.Capture;

/// <summary>Creates frozen input files, never expected results. Run explicitly once before moving production code.</summary>
public static class F3Fixtures
{
    public static void Write(string root, string output)
    {
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new IOException("La cartella delle fixture deve essere vuota.");
        Directory.CreateDirectory(output);
        var corpus = Corpus.Build(root);
        var cases = corpus.Where(c => c.Data is not null && (c.Id.StartsWith("default") || c.Id.StartsWith("elastic") || c.Id.StartsWith("example-"))).ToList();
        foreach (string module in new[] { "geo_palo_verticale", "geo_micropalo_verticale", "str_palo" })
        {
            var sample = corpus.FirstOrDefault(c => c.Module == module && c.Data is not null &&
                (c.Id.StartsWith("regressione-") || c.Id.StartsWith("corpus-")));
            if (sample is not null) cases.Add(sample);
        }
        void Legacy(string module, string id, Action<JsonObject> alter)
        {
            var data = ModuleCatalog.CreateData(module); alter(data);
            cases.Add(new(module, id, "Fixture sintetica di migrazione F3", null, data));
        }
        Legacy("geo_palo_verticale", "legacy-nq-gamma", d => {
            d["versione"] = 1; var g = d["generali"]!.AsObject();
            g["metodo_nq"] = "Raffinata"; g.Remove("sicurezza_base"); d.Remove("efficienza");
        });
        Legacy("geo_micropalo_verticale", "legacy-peso-lineare", d => {
            d["generali"]!["peso_lineare_micropalo"] = "12.34"; d.Remove("efficienza");
        });
        Legacy("mat_calcestruzzo", "legacy-esposizioni", d => {
            d["classe"] = "C35/45"; d["esposizioni"] = new JsonArray("XC3", "XD1");
        });
        Legacy("str_palo", "legacy-workspace-segni", d => {
            // ANTHEA has always written the standard into the workspace (decision of 14/10/2026, F4-31)
            d["workspace_ca"] = new JsonObject { ["versione"] = 1, ["normativa"] = "NTC 2018" };
            d["combinazioni"] = new JsonObject { ["SLU"] = new JsonArray(new JsonObject {
                ["nome"] = "compressione storica", ["azioni"] = new JsonArray("-350", "45", "-12") }) };
        });
        Legacy(RetainingWall.Module, "legacy-global-defaults", d => {
            foreach (string key in new[] { "valley_layers", "soil_mode", "soil_split_x", "search_mode" })
                d["global_stability"]?.AsObject().Remove(key);
        });
        Legacy(HorizontalPileGroup.Module, "legacy-pile-group", d => d["versione"] = 1);
        foreach (string module in new[] { PaloOrizzontale.Module, MicropaloOrizzontale.Module })
            Legacy(module, "legacy-elastic-pending", d => {
                d["elastico"] = new JsonObject { ["EI"] = "25000", ["strati"] = new JsonArray() };
                d["vista_orizzontale"] = "elastico";
            });
        Legacy(BridgeSection.Module, "legacy-web-and-accessories", d => {
            d.Remove("h_trave"); d["h_web"] = "1800";
            foreach (string key in BridgeSection.AccessoryDefaults().Select(p => p.Key).Concat(new[] { "predalle", "h_predalle", "rif_ferri_inf" })) d.Remove(key);
        });
        Legacy(BridgeConcept.Module, "legacy-advanced-defaults", d => {
            foreach (string key in new[] { "arch_rise", "tower_height", "cable_sag", "suspender_spacing", "truss_ratio" }) d["input"]!.AsObject().Remove(key);
            foreach (string key in new[] { "concept_tension", "concept_compression", "co2_cable" }) d["assumptions"]!.AsObject().Remove(key);
        });
        Legacy(Anthea.FoundationWorkspace.FoundationDocument.Module, "legacy-soil-profile", d => {
            d.Remove("stratigrafie"); d.Remove("generali"); d.Remove("sezione");
            d["versione_fondazioni"] = 1; d["input"]!["CoverUnitWeight"] = 17; d["azioni_confermate"] = true;
        });
        Legacy(Anthea.FoundationWorkspace.FoundationDocument.Module, "confirmed-actions", d => d["azioni_confermate"] = true);
        foreach (string module in new[] { PaloOrizzontale.Module, MicropaloOrizzontale.Module })
            Legacy(module, "single-soil-layer", d => d["stratigrafie"]![0]!.AsArray().Add(PaloOrizzontale.Layer()));
        foreach (string module in new[] { PaloOrizzontale.Module, MicropaloOrizzontale.Module })
            Legacy(module, "single-soil-elastic", d => {
                d["stratigrafie"]![0]!.AsArray().Add(PaloOrizzontale.Layer());
                ElasticHorizontalPile.PrepareShared(d); d["vista_orizzontale"] = "elastico";
            });
        Legacy("str_palo", "legacy-calculation-settings", d => {
            foreach (string key in new[] { "staffe_presenti", "coefficienti_unitari", "cls_diagramma" }) d["input"]!.AsObject().Remove(key);
            d["workspace_ca"] = new JsonObject { ["versione"] = 2, ["normativa"] = "NTC 2018", ["coefficienti"] = new JsonObject() };
        });
        foreach (var c in cases.DistinctBy(c => c.Key))
        {
            var item = new JsonObject { ["module"] = c.Module, ["id"] = c.Id,
                ["origin"] = c.Origin, ["source"] = c.SourceFile, ["data"] = c.Data!.DeepClone() };
            File.WriteAllText(Path.Combine(output, c.Module + "--" + c.Id + ".json"), item.ToJsonString(J.Options), new UTF8Encoding(false));
        }
        Console.WriteLine($"Fixture F3: {Directory.GetFiles(output, "*.json").Length}, moduli: {cases.Select(c => c.Module).Distinct().Count()}.");
    }
}
