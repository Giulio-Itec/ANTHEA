using System.Text.Json.Nodes;
using Anthea.Calculations;
using Anthea.Calculations.Geotechnics;
using X.Core;

internal static class AdvancedChecks
{
    internal static int Run(string directory)
    {
        var log = new List<string>();
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); log.Add("OK " + name); }
        void Near(double actual, double expected, string name, double eps = 1e-7) => Check(Math.Abs(actual - expected) < eps * Math.Max(1, Math.Abs(expected)), name + $" ({actual:G10})");
        void Reject(Action action, string name) { try { action(); } catch (ArgumentException) { Check(true, name); return; } throw new Exception(name); }
        try
        {
            double z = 3, half = 1.5, q = 100;
            Near(FoundationSettlement.Stress(half, z, 0, 3, q, q), 2 * q / Math.PI * (Math.Atan(half / z) + half * z / (half * half + z * z)), "Boussinesq striscia uniforme formula indipendente");
            Near(FoundationSettlement.Stress(1.5, 2, 0, 3, 0, 200), FoundationSettlement.Stress(1.5, 2, 0, 3, 100, 100), "Carico triangolare al centro uguale alla media");
            Near(FoundationSettlement.Stress(.7, 2, 0, 3, 20, 100), FoundationSettlement.Stress(2.3, 2, 0, 3, 100, 20), "Simmetria pressione lineare");
            var layers = new[] { new FoundationSettlement.Layer("A", 5, 20000), new FoundationSettlement.Layer("B", 15, 40000) };
            var settlement = FoundationSettlement.Calculate(layers, 1.5, 0, 3, 100, 100, 0, 3);
            Near(FoundationSettlement.Calculate(layers, 1.5, 0, 3, 200, 200, 0, 3).SettlementMm, 2 * settlement.SettlementMm, "Linearità cedimenti");
            Near(FoundationSettlement.Calculate(layers.Select(l => l with { Modulus = l.Modulus * 2 }).ToArray(), 1.5, 0, 3, 100, 100, 0, 3).SettlementMm, settlement.SettlementMm / 2, "Moduli doppi dimezzano cedimento");
            Near(FoundationSettlement.Calculate(layers, 1.5, 0, 3, 100, 100, 100, 3).SettlementMm, 0, "Carico netto nullo");
            Reject(() => FoundationSettlement.Calculate([], 0, 0, 3, 100, 100, 0, 3), "Nessun modulo inventato");
            Reject(() => FoundationSettlement.Calculate(layers, 1, 0, 3, 100, 100, 110, 3), "Scarico non confuso con cedimento positivo");
            var shape = RetainingWall.IntegrateCurvature([new(0, .001), new(3, .001)], 3);
            Near(shape[^1].DisplacementMm, 4.5, "Integrale curvatura costante κH²/2");
            Near(RetainingWall.IntegrateCurvature([new(0, .003), new(3, 0)], 3)[^1].DisplacementMm, 9, "Integrale curvatura triangolare κ0H²/3");
            Near(NewmarkSliding.Calculate([new(0, .2), new(1, .2)], .1).DisplacementMm, 981, "Newmark impulso rettangolare più arresto analitico");
            Near(NewmarkSliding.Calculate([new(0, .05), new(1, -.4), new(2, .1)], .1).DisplacementMm, 0, "Newmark sotto soglia nessuno scorrimento");
            var triangle = NewmarkSliding.Calculate([new(0, 0), new(1, .2), new(2, 0)], .1);
            Check(triangle.DisplacementMm > 0 && triangle.Points.Zip(triangle.Points.Skip(1)).All(p => p.Second.DisplacementMm >= p.First.DisplacementMm), "Newmark arresti e irreversibilità");
            Near(NewmarkSliding.Calculate([new(0, 0), new(.5, .1), new(1, .2), new(1.5, .1), new(2, 0)], .1).DisplacementMm, triangle.DisplacementMm, "Newmark indipendente da suddivisione lineare");
            Reject(() => NewmarkSliding.Calculate([new(0, 0), new(0, .2)], .1), "Tempi duplicati rifiutati");
            var capacity = ShallowFoundationSeismic.Calculate(3, 19, 34, 100, 0, 0, 0, 0, 1, 1);
            double phi = 34 * Math.PI / 180, nq = Math.Exp(Math.PI * Math.Tan(phi)) * Math.Pow(Math.Tan(Math.PI / 4 + phi / 2), 2);
            Near(capacity.Capacity, 19 * 9 * (nq - 1) * Math.Tan(phi), "Portanza Annex F limite verticale statico");
            var seismic = ShallowFoundationSeismic.Calculate(3, 19, 34, 100, 0, 0, .2, 0, 1.15, 1.2);
            Near(seismic.Capacity, capacity.NMax * Math.Pow(1 - .96 * 1.15 * .2 / Math.Tan(phi), .39) / (1.15 * 1.2), "Portanza sismica pura verifica chiusa");
            var interaction = ShallowFoundationSeismic.Calculate(3, 19, 34, 200, 40, 30, .2, .1, 1.15, 1.2);
            var atLimit = ShallowFoundationSeismic.Calculate(3, 19, 34, interaction.Capacity, 40 * interaction.Capacity / 200, 30 * interaction.Capacity / 200, .2, .1, 1.15, 1.2);
            Near(atLimit.Interaction!.Value, 1, "Portanza sul confine N-V-M");
            Check(interaction.Capacity < seismic.Capacity, "Interazione e inerzia verticale riducono la capacità");
            var d = RetainingWall.Defaults(); string initial = d.ToJsonString();
            var detail = RetainingWall.CalculateReinforcementDetails(d); Check(d.ToJsonString() == initial, "Dettagli non mutano input");
            Check(detail.Bars.Count == 6 && detail.Bars.Where(b => b.Zone == "stem").All(b => b.Mandrel > 0 && b.Anchor >= b.RequiredAnchor && b.Path.Count > 10), "Percorsi con pieghe e ancoraggi verificabili");
            d["reinforcement"]!["two_zones"] = true; d["reinforcement"]!["lower_height"] = 1.5;
            d["reinforcement"]!["stem_upper"]!["diameter"] = 12;
            detail = RetainingWall.CalculateReinforcementDetails(d); Check(detail.Bars.Count == 8 && detail.Bars.Where(b => b.Zone.StartsWith("stem")).All(b => b.Lap > 0), "Due zone con sovrapposizioni");
            d["reinforcement"]!["stem"]!["symmetric"] = false; d["reinforcement"]!["stem"]!["opposite_diameter"] = 12; d["reinforcement"]!["stem"]!["opposite_count"] = 5;
            var section = RetainingWall.SectionInput(d, "Fusto", .4, 3);
            Check(section.D("top_bar_diameter_mm") == 12 && section.D("bottom_bar_diameter_mm") == 16 && section.D("top_bar_count") == 5, "Sezione GPC con facce indipendenti");
            var asymmetric = RetainingWall.Defaults(); var bars = asymmetric["reinforcement"]!["stem"]!;
            bars["symmetric"] = false; bars["diameter"] = 20; bars["opposite_diameter"] = 10;
            double PositiveCapacity(JsonObject model) => RetainingWall.Calculate(model).Structural.First(c => c.Member == "Fusto" && c.Position == 3 && c.Name.Contains("N–M")).Resistance!.Value;
            double strongMonte = PositiveCapacity(asymmetric); bars["diameter"] = 10; bars["opposite_diameter"] = 20;
            Check(strongMonte > PositiveCapacity(asymmetric), "Convenzione M positivo: armatura monte effettivamente tesa nel modello GPC");
            d["serviceability"]!["settlement"] = true; d["serviceability"]!["displacement"] = true;
            d["serviceability"]!["removed_pressure"] = 0; d["serviceability"]!["horizontal_stiffness"] = 100000;
            d["serviceability"]!["layers"] = new JsonArray(J.Obj(("name", "Sabbia"), ("thickness", 25), ("modulus", 30000)));
            var result = RetainingWall.Calculate(d);
            Check(result.Serviceability!.Cases.Count > 0 && result.Serviceability.Cases.All(c => c.HeadDisplacement is not null), "Cedimenti e spostamenti integrati per tutte le SLE");
            Check(result.Serviceability.Cases.All(c => c.StemDisplacement is > .1 and < 100), "Curvatura GPC lungo Y: flessione del fusto non nulla e scala in mm");
            Check(result.Checks.Any(c => c.Name.Contains("Cedimento") && c.Ratio is not null), "Cedimenti nei tassi di lavoro");
            d["serviceability"]!["layers"]![0]!["thickness"] = .2;
            var shallow = RetainingWall.Calculate(d);
            Check(shallow.Serviceability!.Cases.All(c => c.HeadDisplacement is null) && shallow.Checks.Any(c => c.Name.Contains("Rotazione") && c.Ratio is null), "Indagine poco profonda blocca esito favorevole e spostamento totale");
            var gravity = RetainingWall.Example("gravity"); gravity["gravity_design"]!["type"] = "Calcestruzzo non armato";
            var plain = RetainingWall.Calculate(gravity);
            Check(plain.Structural.Any(c => c.Name.Contains("blocco compresso") && c.Resistance > 0), "Gravità CLS con proprietà GPC");
            gravity["gravity_design"]!["type"] = "Muratura";
            Check(RetainingWall.Calculate(gravity).Structural.Any(c => c.Ratio is null && c.Name.Contains("Materiale")), "Muratura senza resistenze: nessun esito inventato");
            gravity["gravity_design"]!["fk"] = 10; gravity["gravity_design"]!["fvk0"] = .3; gravity["gravity_design"]!["fvk_limit"] = 1; gravity["gravity_design"]!["elastic_modulus"] = 5000;
            Check(RetainingWall.Calculate(gravity).Structural.Any(c => c.Name.Contains("scorrimento giunto")), "Muratura con scorrimento giunti");
            var designInput = RetainingWall.Defaults(); initial = designInput.ToJsonString();
            var proposal = RetainingWall.DesignReinforcement(designInput);
            Check(designInput.ToJsonString() == initial, "Predimensionamento conserva l’originale");
            Check(proposal.Input["detailing"].B("enabled") && proposal.Checks.Any(c => c.Name.Contains("N–M GPC")), "Proposta ricalcolata con GPC");
            Check(proposal.Input["reinforcement"]!["stem"].D("diameter") != designInput["reinforcement"]!["stem"].D("diameter"), "Ricerca produce un predimensionamento effettivo");
            Check(proposal.Passed == proposal.Checks.All(c => c.Ratio is double v && v <= (c.Combination == "Dettagli" ? 1 : .95)), "Proposta distingue resistenza e dettagli non soddisfatti");
            var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            try { RetainingWall.DesignReinforcement(designInput, cancellation.Token); throw new Exception("Cancellazione"); } catch (OperationCanceledException) { Check(true, "Ricerca interrompibile"); }
            File.WriteAllText(Path.Combine(directory, "proposta-armature.json"), proposal.Input.ToJsonString(J.Options));
            File.WriteAllText(Path.Combine(directory, "avanzate-risultati.json"), result.Json().ToJsonString(J.Options));
            File.WriteAllBytes(Path.Combine(directory, "avanzate-report.docx"), ReportRetainingWall.Create("Controllo muri cedimenti e armature", result, []));
            return log.Count;
        }
        finally { File.WriteAllLines(Path.Combine(directory, "advanced.txt"), log); }
    }
}
