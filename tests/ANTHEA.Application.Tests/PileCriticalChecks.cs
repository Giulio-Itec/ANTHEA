using System.Text.Json.Nodes;
using Anthea.Calculations;

/// <summary>
/// Palo elastico in c.a. (analisi dei bug del 15/10/2026): sezione critica del tratto e foglio c.a. di approfondimento (PO-4).
/// Foglio orizzontale predefinito in vista elastica: D 1 m, L 10 m, H 100 kN, N 500 kN, granulare, staffe a 150 mm, ipotesi approvate.
/// </summary>
internal static class PileCriticalChecks
{
    internal static JsonObject DefaultElasticPile()
    {
        var data = PaloOrizzontale.Defaults(); data["vista_orizzontale"] = "elastico";
        var layer = PaloOrizzontale.Layer(); layer["tipologia"] = "Granulare"; layer["spessore"] = "20";
        data["stratigrafie"] = new JsonArray(new JsonArray(layer));
        var g = data["generali"]!; g["azione_orizzontale"] = "100"; g["lunghezza"] = "10"; g["azione_assiale"] = "500";
        ElasticHorizontalPile.PrepareShared(data);
        ElasticHorizontalPile.ApproveReinforcementAssumptions(data, false);
        data["sezione"]!["esposizione"] = "XC2"; data["sezione"]!["transverse_spacing_mm"] = "150";
        return data;
    }

    internal static int Run()
    {
        int count = 0;
        void Check(bool value, string message) { if (!value) throw new Exception("Palo elastico, sezione critica: " + message); count++; }
        var data = DefaultElasticPile();
        var result = PaloOrizzontale.Calculate(data);
        Check(result.S("errore") == "", result.S("errore"));
        var segment = result["armature"]!.Array("tratti")[0]!;
        var checks = segment.Array("verifiche").OfType<JsonObject>().ToArray();
        Check(checks.Any(c => c["DevelopmentAvailable"] is JsonValue v && !v.GetValue<bool>()), "caso senza quote prive di sviluppo: la prova non rappresenta la segnalazione");
        double Ratio(JsonObject c) => J.Number(c["GoverningRatio"]) ?? J.Number(c["BendingRatio"]) ?? double.PositiveInfinity;
        // PO-4: la critica è la quota con il rapporto massimo (prima la prima quota senza sviluppo, x = 0 con M ≈ 0), a parità il |M| maggiore.
        var critical = segment["critica"]!.AsObject();
        double best = checks.Max(Ratio);
        Check(Ratio(critical) == best, $"critica con rapporto {Ratio(critical)} invece del massimo {best}");
        Check(checks.Where(c => Ratio(c) == best).All(c => Math.Abs(c["Action"].D("M")) <= Math.Abs(critical["Action"].D("M"))), "a parità di rapporto la critica non ha il |M| maggiore");
        Check(Math.Abs(critical["Action"].D("M")) > 1, $"critica con M ≈ 0 alla quota {critical["Action"].D("Depth")} m");
        // Il foglio c.a. di approfondimento riceve almeno l'azione della critica e quella con |M| massimo del tratto, nelle SLU e nel taglio.
        var points = result["risposta"]!.Array("Points").Where(p => p.D("Depth") >= segment.D("inizio") - 1e-9 && p.D("Depth") <= segment.D("fine") + 1e-9).ToArray();
        var peak = points.MaxBy(p => Math.Abs(p.D("Moment")))!;
        var sheet = ElasticHorizontalPile.CreateConcreteSheet(data, result, 0);
        var slu = sheet["combinazioni"]!.Array("SLU");
        Check(slu.Any(r => r!["azioni"]![1]!.GetValue<double>() == critical["Action"].D("M")), "foglio c.a. senza l'azione della critica");
        Check(slu.Any(r => Math.Abs(r!["azioni"]![1]!.GetValue<double>()) == Math.Abs(peak.D("Moment"))), $"foglio c.a. senza l'azione con |M| massimo ({peak.D("Moment")} kNm a {peak.D("Depth")} m)");
        var shear = sheet["workspace_ca"]!["taglio"]!.Array("azioni");
        Check(shear.Count == slu.Count && shear.Any(r => Math.Abs(r.D("Mx")) == Math.Abs(peak.D("Moment"))), "righe di taglio del foglio c.a. senza l'azione con |M| massimo");
        return count;
    }
}
