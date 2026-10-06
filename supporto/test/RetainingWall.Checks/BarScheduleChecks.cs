using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;

internal static class BarScheduleChecks
{
    internal static int Run(string directory)
    {
        var log = new List<string>();
        void Check(bool value, string label) { if (!value) throw new Exception(label); log.Add("OK " + label); }
        void Near(double actual, double expected, string label) => Check(Math.Abs(actual - expected) < 1e-9 * Math.Max(1, Math.Abs(expected)), label);
        var d = RetainingWall.Defaults(); d["geometry"]!["height"] = 4; d["geometry"]!["slab"] = .6;
        d["geometry"]!["stem_base"] = .4; d["geometry"]!["stem_top"] = .4; d["materials"]!["cover"] = 50;
        d["bar_schedule"]!["end_cover"] = 50; var stem = d["reinforcement"]!["stem"]!;
        stem["diameter"] = 16; stem["count"] = 6; stem["bend_diameter"] = 160; stem["anchor_length"] = 1000;
        string before = d.ToJsonString(); var one = RetainingWall.CalculateBarSchedule(d);
        Check(before == d.ToJsonString(), "Distinta non modifica il documento");
        Check(one.Bars.Count == 12 && one.Bars.Select(b => b.Mark).Distinct().Count() == 12, "Sei principali e sei secondarie con marche uniche");
        var p = one.Bars.Single(b => b.Mark == "P1"); Check(p.Quantity == 6, "Un metro conserva sei barre");
        double radius = .088, vertical = .6 - .058 - radius, arc = radius * Math.PI / 2, tail = 1 - vertical - arc;
        Near(p.CutLength!.Value, 4 - .058 + vertical + arc + tail, "Sviluppo indipendente tratto verticale più arco esatto più coda");
        Near(p.Legs.Single(x => x.Name == "arco 90°").Length, arc, "Arco sviluppato al raggio asse");
        Near(RetainingWall.CalculateReinforcementDetails(d).Bars[0].Length, p.CutLength.Value, "Stima e distinta usano lo stesso sviluppo esatto");
        Near(p.Weight!.Value, 6 * p.CutLength.Value * Math.PI * 16 * 16 / 4 * .00785, "Peso indipendente con densità 7850 kg/m³");
        d["bar_schedule"]!["panel_length"] = 3;
        var three = RetainingWall.CalculateBarSchedule(d); p = three.Bars[0];
        Check(p.Quantity == 18, "Tre metri: arrotondamento dei pezzi alle estremità");
        Check(p.Spacing <= one.Bars[0].Spacing, "Passo reale non superiore a quello verificato");
        Near(three.Bars.Single(b => b.Mark == "S1").CutLength!.Value, 2.9, "Secondarie al netto dei copriferri di estremità");
        d["bar_schedule"]!["stock_length"] = 2;
        Check(RetainingWall.CalculateBarSchedule(d).Warnings.Any(w => w.Contains("S1:") && w.Contains("giunto")), "Barre oltre commerciale segnalate senza tagli fittizi");
        d["bar_schedule"]!["stock_length"] = 12; d["reinforcement"]!["two_zones"] = true; d["reinforcement"]!["lower_height"] = 2;
        var two = RetainingWall.CalculateBarSchedule(d);
        Check(two.Bars.Count == 17 && two.Bars.Count(b => b.Lap > 0) == 4, "Due zone con principali giuntate, otto secondarie e collegamenti");
        Check(!two.CompleteQuantities && two.Bars.Single(b => b.Mark == "C1").Weight is null && two.Warnings.Any(w => w.Contains("Peso parziale")), "Collegamenti senza sagoma non inventano peso o sviluppo");
        d["bar_schedule"]!["tie_cut_length"] = 800;
        var defined = RetainingWall.CalculateBarSchedule(d); Check(defined.CompleteQuantities, "Sviluppo collegamenti assegnato completa il computo");
        Near(defined.Bars.Single(b => b.Mark == "C1").CutLength!.Value, .8, "Sviluppo assegnato convertito da mm a m");
        string csv = ReportRetainingWall.BarScheduleCsv(defined); Check(csv.Contains("\"C1\"") && csv.Contains("Tratto muro m") && csv.Contains("\"NOTA\""), "CSV include collegamenti, impostazioni e limiti");
        var doc = Archivio.Documento(RetainingWall.Module); doc["dati"] = d.DeepClone(); string path = Path.Combine(directory, "distinta-due-zone.anthea"); Archivio.Scrivi(path, doc);
        Check(J.Equivalent(Archivio.Leggi(path)["dati"]!["bar_schedule"], d["bar_schedule"]), "Archivio conserva impostazioni distinta");
        File.WriteAllText(Path.Combine(directory, "distinta-due-zone.csv"), csv);
        var invalid = (JsonObject)d.DeepClone(); invalid["bar_schedule"]!["panel_length"] = 0;
        try { RetainingWall.CalculateBarSchedule(invalid); throw new Exception("Tratto nullo accettato"); } catch (ArgumentException) { Check(true, "Tratto nullo rifiutato"); }
        invalid = (JsonObject)d.DeepClone(); invalid["geometry"]!["height"] = 0;
        try { RetainingWall.CalculateBarSchedule(invalid); throw new Exception("Altezza nulla accettata"); } catch (ArgumentException) { Check(true, "Geometria degenere rifiutata anche dal motore della distinta"); }
        invalid = (JsonObject)d.DeepClone(); invalid["reinforcement"]!["lower_height"] = 5;
        try { RetainingWall.CalculateBarSchedule(invalid); throw new Exception("Cambio armatura esterno accettato"); } catch (ArgumentException) { Check(true, "Cambio armatura fuori dal fusto rifiutato"); }
        invalid = (JsonObject)d.DeepClone(); invalid["family"] = "gravity";
        try { RetainingWall.CalculateBarSchedule(invalid); throw new Exception("Distinta gravità accettata"); } catch (ArgumentException) { Check(true, "Nessun acciaio inventato per il muro a gravità"); }
        File.WriteAllLines(Path.Combine(directory, "distinta-test.txt"), log.Append($"PASS {log.Count} controlli distinta")); return log.Count;
    }
}
