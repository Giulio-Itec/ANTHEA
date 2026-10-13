using System.Reflection;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Application.Concrete;
using Anthea.Application.Modules;

internal static class ConcreteAuxiliaryContractChecks
{
    // Inspect the retained original only in this test; the application API never exposes it.
    private static object Original(object value) => value.GetType().GetProperty("Source", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;
    internal static int Run(string root)
    {
        int checks = 0, shearRows = 0, torsionRows = 0, mutableDetails = 0;
        void Check(bool ok, string text) { if (!ok) throw new InvalidOperationException(text); checks++; }
        var comparison = new OwnedProjectionComparison();
        void Same(object value) { comparison.Compare(Original(value), value, value.GetType().Name); checks++; }
        var inputs = Directory.GetFiles(Path.Combine(root, "tests/fixtures/f3/inputs"), "str_palo--*.json")
            .Select(file => JsonNode.Parse(File.ReadAllText(file))!["data"]!.AsObject()).ToList();
        var torqueInput = SezioneCA.DefaultData(); var torqueSettings = SectionWorkspace.Prepare(torqueInput);
        torqueSettings["taglio"]!["chiusura_torsione"] = "Confermato"; torqueSettings["taglio"]!["as_torsione"] = "400";
        torqueSettings["taglio"]!["azioni"] = new JsonArray(J.Obj(("id", "VT"), ("nome", "VT"), ("N", "-100"), ("Mx", "0"), ("My", "0"), ("Vx", "10"), ("Vy", "20"), ("T", "5")));
        inputs.Add(torqueInput);
        foreach (var data in inputs)
        {
            ConcreteSheetPreparation.PrepareReport(data);
            var settings = data["workspace_ca"]!.AsObject(); var input = data["input"]!.AsObject();
            var rows = settings["taglio"]!.Array("azioni").OfType<JsonObject>().ToArray();
            ConcreteShearBatch.Calculate(input, settings, rows, (index, value, error) =>
            {
                if (value is null)
                {
                    string nativeError = "";
                    try { ConcreteShearAnalysis.Calculate(input, settings, settings["taglio"]!.AsObject(), rows[index]); }
                    catch (ArgumentException ex) { nativeError = ex.Message; }
                    Check(error == nativeError && error is not null, "Errore per riga cambiato"); return;
                }
                Same(value); shearRows++; if (value.Torsion is not null) torsionRows++;
                var native = (ConcreteShearResult)Original(value);
                Check(value.Shear.SelectMany(v => v.Details).Select(v => v.Format()).SequenceEqual(native.Shear.SelectMany(v => v.Details).Select(v => v.Format())), "Dettagli di calcolo cambiati");
                if (value.Shear[0].Details.Length > 0)
                {
                    value.Shear[0].Details[0] = null!; mutableDetails++;
                    Check(native.Shear[0].Details[0] is not null, "Dettagli condivisi col motore");
                }
            });
        }
        // A small explicit design space also exercises progress and partial cancellation.
        var source = SezioneCA.DefaultData(); var ws = SectionWorkspace.Prepare(source); var section = source["input"]!.AsObject();
        section["cover_mm"] = "40"; section["width_mm"] = "400"; section["height_mm"] = "600";
        ws["dettagli_costruttivi"]!["elemento"] = "Trave"; ws["sle_comuni"]!["esposizione"] = "XC1"; ws["ancoraggi"]!["lunghezza"] = "2000";
        foreach (string set in SectionWorkspace.Sets) source["combinazioni"]![set] = new JsonArray();
        source["combinazioni"]!["SLU"] = new JsonArray(J.Obj(("id", "u1"), ("nome", "Flessione"), ("azioni", new[] { "0", "100", "20" })));
        var options = ConcreteReinforcementDesign.Prepare(ws);
        foreach (var (key, value) in new[] { ("diametri", "16;20"), ("n_superiori", "3;4"), ("n_inferiori", "3;4"), ("n_laterali", "2"), ("diametri_staffe", "8"), ("passi_staffe", "100;150"), ("parallelismo", "1") }) options[key] = value;
        string before = source.ToJsonString(); int notifications = 0;
        var design = StructuralQueries.ConcreteReinforcement(source, progress: new InlineProgress(p => { Same(p); notifications++; })); Same(design);
        Check(design.Candidates.Count > 0 && design.Completed && notifications > 0 && source.ToJsonString() == before, "Ricerca incompleta, progressi persi o input mutato");
        design.Candidates[0].Input["only_view"] = true;
        Check(((ConcreteDesignResult)Original(design)).Candidates[0].Input["only_view"] is null, "Proposta condivide input nativo");
        using var stop = new CancellationTokenSource();
        var partial = StructuralQueries.ConcreteReinforcement(source, stop.Token, new InlineProgress(_ => stop.Cancel())); Same(partial);
        Check(!partial.Completed, "Annullamento non conserva lo stato parziale");
        foreach (bool elastic in new[] { false, true })
            foreach (var value in StructuralQueries.ConcreteQuickResistance(section, ws, -100, elastic)) Same(value);
        var curveOptions = ws["momento_curvatura"]!.AsObject(); curveOptions["passi"] = "10";
        var curve = StructuralQueries.ConcreteCurvature(section, ws, curveOptions); Same(curve);
        Check(curve.Points.Count >= 11 && shearRows > 0 && torsionRows > 0 && mutableDetails > 0 && comparison.Values > 1000, $"Copertura dei contratti CA insufficiente: curve {curve.Points.Count}, taglio {shearRows}, torsione {torsionRows}, valori {comparison.Values}, stato {curve.Status}");
        bool Native(Type t) => t.Assembly == typeof(SezioneCA).Assembly || t.Assembly.GetName().Name!.StartsWith("GPC")
            || t.HasElementType && Native(t.GetElementType()!) || t.IsGenericType && t.GenericTypeArguments.Any(Native);
        foreach (var type in typeof(ConcreteResults).GetNestedTypes(BindingFlags.Public))
            Check(type.GetProperties().All(p => !Native(p.PropertyType)), "Contratto CA espone il motore: " + type.Name);
        Console.WriteLine($"Contratti CA: {comparison.Values} valori esatti, {shearRows} righe taglio, {torsionRows} torsioni, {design.Candidates.Count} proposte e {curve.Points.Count} punti curva");
        return checks;
    }
    private sealed class InlineProgress(Action<ConcreteResults.DesignProgress> callback) : IProgress<ConcreteResults.DesignProgress>
    { public void Report(ConcreteResults.DesignProgress value) => callback(value); }
}
