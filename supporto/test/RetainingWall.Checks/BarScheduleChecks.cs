using System.Text.Json.Nodes;
using Anthea.Calculations;
using X.Core;
using Anthea.Application.Documents;

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
        // Tavola 1.16.0: fusto monte e valle, fondazione inferiore e superiore (barre uniche), forcella, quattro file di longitudinali.
        Check(one.Bars.Count == 9 && one.Bars.Select(b => b.Mark).SequenceEqual(Enumerable.Range(1, 9).Select(i => i.ToString())), "Nove posizioni numerate nell'ordine del disegno");
        var p = one.Bars.Single(b => b.Mark == "1"); Check(p.Kind == "Principale" && p.Face == "monte", "pos. 1 fusto monte");
        Near(p.Spacing, 1000.0 / 6, "Passo 1000/n del muro continuo");
        // Tratto 1 m, copriferri d'estremità 50 mm: (1000-100-16)/166,67 = 5,30 -> 6 intervalli, 7 barre (una a ogni estremità).
        Check(p.Quantity == 7, "Un metro: sette barre al passo 166,7 mm");
        // Piede sulla rete inferiore: 50 + Ø16 trasversale + Ø10 longitudinale + 8 = 84 mm; r = (160+16)/2 = 88 mm.
        // Verticale 4,542-0,172 = 4,370 m; arco π/2·0,088 = 0,1382 m; coda 1,000-0,428-0,1382 = 0,4338 m. Tratti al cm: 437 + 14 + 43.
        Near(p.CutLength!.Value, 4.94, "Sviluppo somma dei tratti al cm (437 + 14 + 43)");
        Check(p.Legs.Select(l => Math.Round(l.Length * 100)).SequenceEqual(new double[] { 437, 14, 43 }) && p.Legs[1].Arc, "Tratti all'asse e arco sviluppato");
        Check(p.Note.Contains("verso valle") && one.Bars.Single(b => b.Mark == "2").Note.Contains("verso monte"), "Piedi incrociati: monte verso valle, valle verso monte");
        Near(RetainingWall.CalculateReinforcementDetails(d).Bars[0].Length, 4.942, "Dettagli: sviluppo esatto 4,370 + 0,1382 + 0,4338");
        Near(p.Weight!.Value, 7 * 4.94 * Math.PI * 16 * 16 / 4 * .00785, "Peso indipendente con densità 7850 kg/m³");
        Check(one.Diameters.Sum(x => x.Weight) is var total && Math.Abs(total - one.KnownWeight) < 1e-9, "Riepilogo per diametro somma al peso totale");
        // Calcestruzzo: (0,8+0,4+1,8)·0,6 + 0,4·4 = 3,4 m² per 1 m.
        Near(one.ConcreteVolume, 3.4, "Volume del calcestruzzo del tratto"); Near(one.Incidence, one.KnownWeight / 3.4, "Incidenza kg/m³");
        Check(one.Bars.Count(b => b.Note.StartsWith("Barra unica")) == 2, "Fondazione: barre uniche inferiore e superiore");
        Check(!one.Warnings.Any(w => w.Contains("preliminare") || w.Contains("dettagliare")), "Nessuna nota preliminare");
        d["bar_schedule"]!["panel_length"] = 3;
        var three = RetainingWall.CalculateBarSchedule(d); p = three.Bars[0];
        Check(p.Quantity == 19, "Tre metri: (3000-116)/166,67 = 17,3 -> 18 intervalli, 19 barre");
        Check((3000 - 100 - 16) / (p.Quantity - 1.0) <= p.Spacing, "Passo reale non superiore a quello verificato");
        var longitudinal = three.Bars.First(b => b.Kind == "Longitudinale");
        Near(longitudinal.CutLength!.Value, 2.9, "Longitudinali al netto dei copriferri di estremità");
        Check(longitudinal.Pieces == 1 && longitudinal.Quantity == longitudinal.Lines, "Un pezzo per filo entro la barra commerciale");
        d["bar_schedule"]!["stock_length"] = 2;
        var stock = RetainingWall.CalculateBarSchedule(d);
        Check(stock.Warnings.Any(w => w.StartsWith("pos. 1:") && w.Contains("giunto")), "Barre principali oltre la commerciale segnalate senza tagli fittizi");
        var spliced = stock.Bars.First(b => b.Kind == "Longitudinale");
        Check(spliced.Pieces == 2 && spliced.Quantity == 2 * spliced.Lines, "Longitudinali oltre la commerciale: due pezzi per filo");
        Near(spliced.CutLength!.Value, Math.Ceiling((2.9 + spliced.Lap / 1000) / 2 * 100 - 1e-9) / 100, "Pezzi uguali (L + l₀)/2 arrotondati al cm");
        d["bar_schedule"]!["stock_length"] = 12; d["reinforcement"]!["two_zones"] = true; d["reinforcement"]!["lower_height"] = 2;
        var two = RetainingWall.CalculateBarSchedule(d);
        Check(two.Bars.Count == 14 && two.Bars.Count(b => b.Lap > 0 && b.Kind == "Principale") == 4, "Due zone: quattro principali giuntate, fondazione, forcella, spilli e sei file di longitudinali");
        var tie = two.Bars.Single(b => b.Kind == "Spillo");
        Check(two.CompleteQuantities && tie.CutLength > 0 && tie.Note.Contains("135°"), "Spilli con sagoma e sviluppo calcolati");
        d["bar_schedule"]!["tie_cut_length"] = 800;
        var defined = RetainingWall.CalculateBarSchedule(d); Check(defined.CompleteQuantities, "Sviluppo assegnato completa il computo");
        Near(defined.Bars.Single(b => b.Kind == "Spillo").CutLength!.Value, .8, "Sviluppo assegnato (forzatura) convertito da mm a m");
        string csv = ReportRetainingWall.BarScheduleCsv(defined); Check(csv.Contains("\"Spilli della giunzione\"") && csv.Contains("Tratto muro m") && csv.Contains("\"RIEPILOGO\"") && csv.Contains("\"INCIDENZA kg/m³\""), "CSV con posizioni, riepilogo e incidenza");
        var doc = Archivio.Documento(RetainingWall.Module); doc["dati"] = d.DeepClone(); string path = Path.Combine(directory, "distinta-due-zone.anthea"); DocumentService.Save(path, doc);
        Check(J.Equivalent(DocumentService.Open(path)["dati"]!["bar_schedule"], d["bar_schedule"]), "Archivio conserva impostazioni distinta");
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
