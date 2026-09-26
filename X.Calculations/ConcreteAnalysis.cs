using System.Text.Json.Nodes;

namespace Anthea.Calculations;

/// <summary>Headless execution of the same domain, stress, shear and torsion services used by WPF.
/// Optional curves/details keep their explicit entry points; no legacy sign convention is used here.</summary>
public static class ConcreteAnalysis
{
    public static JsonObject Calculate(JsonObject source, CancellationToken token = default)
    {
        ModuleCatalog.ValidateData("str_palo", source);
        var data = (JsonObject)source.DeepClone();
        var settings = SectionWorkspace.Prepare(data);
        var input = data["input"]!.AsObject();
        var session = new ConcreteAnalysisSession();
        token.ThrowIfCancellationRequested();
        var prepared = CheckerSection.PrepareModel(input, settings);
        var domains = new JsonObject(); var stresses = new JsonObject();
        var shears = new JsonObject(); var torsions = new JsonObject(); var errors = new JsonObject();
        void Step(string name, Action work)
        {
            token.ThrowIfCancellationRequested();
            try { work(); }
            catch (Exception ex) when (ex is not OperationCanceledException) { errors[name] = ex.Message; }
        }
        JsonObject[] Rows(string key) => data["combinazioni"]!.Array(key).OfType<JsonObject>()
            .Select(row => J.Obj(("id", row.S("id")), ("nome", row.S("nome")),
                ("N", row["azioni"]![0]), ("Mx", row["azioni"]![1]), ("My", row["azioni"]![2]))).ToArray();
        foreach (string key in new[] { "SLU", "SLV" })
            foreach (bool three in new[] { true, false })
            {
                string label = (three ? "3D:" : "2D:") + key;
                Step(label, () =>
                {
                    var result = session.Domain(input, settings, settings[three ? "dominio3d" : "dominio2d"]!.AsObject(), key, three, Rows(key), token, prepared);
                    domains[label] = J.Node(result.Checks);
                    foreach (var (id, check) in result.Checks.Where(r => r.Value.Utilization is null))
                        errors[label + "/" + id] = check.Status;
                });
            }
        foreach (string key in SectionWorkspace.Sets.Skip(2))
            Step(key, () =>
            {
                var result = session.Stress(input, settings, settings["sle"]![key]!.AsObject(), key, Rows(key), token, prepared);
                stresses[key] = ConcreteAnalysisSession.ExportStress(result);
                foreach (var (id, row) in result.Where(r => r.Value.State is null)) errors[key + "/" + id] = row.Status;
            });
        var shear = settings["taglio"]!.AsObject();
        foreach (var row in shear.Array("azioni").OfType<JsonObject>().Select((value, index) => (value, index)))
        {
            string id = row.value.S("id", "riga-" + (row.index + 1));
            Step("Taglio/" + id, () =>
            {
                var result = ConcreteShearAnalysis.Calculate(input, settings, shear, row.value);
                shears[id] = J.Node(result.Shear); if (result.Torsion is not null) torsions[id] = J.Node(result.Torsion);
            });
        }
        token.ThrowIfCancellationRequested();
        return new JsonObject
        {
            ["errore"] = errors.Count == 0 ? "" : "Calcolo parziale: consultare errori_calcolo.",
            ["motore"] = "GPCChecker.Concrete.dll",
            ["normativa_riferimento"] = settings.S("normativa"),
            ["verifica_normativa_completa"] = false,
            ["calcoli_inclusi"] = J.Node(new[] { "Domini 3D e 2D SLU/SLV", "Tensioni e fessurazione SLE", "Taglio e torsione" }),
            ["domini"] = domains,
            ["tensioni"] = stresses,
            ["taglio"] = shears,
            ["torsione"] = torsions,
            ["errori_calcolo"] = errors,
            ["dati"] = data
        };
    }
}
