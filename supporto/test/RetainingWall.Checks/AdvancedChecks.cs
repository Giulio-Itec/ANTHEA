using System.Text.Json.Nodes;
using Anthea.Calculations;
using GPC.Checkers.Geotechnics.Foundations;
using GPC.Checkers.Geotechnics.Seismic;
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
            // The methods are GPCChecker.Geotechnics (mm, N/mm, MPa, rad): 1 kPa = 0.001 MPa, 1 kN/m = 1 N/mm, 1 kNm/m = 1000 N·mm/mm.
            double z = 3000, half = 1500, q = .1;
            Near(FoundationSettlement.Stress(half, z, 0, 3000, q, q), 2 * q / Math.PI * (Math.Atan(half / z) + half * z / (half * half + z * z)), "Boussinesq striscia uniforme formula indipendente (libreria)");
            Near(FoundationSettlement.Stress(1500, 2000, 0, 3000, 0, .2), FoundationSettlement.Stress(1500, 2000, 0, 3000, .1, .1), "Carico triangolare al centro uguale alla media");
            Near(FoundationSettlement.Stress(700, 2000, 0, 3000, .02, .1), FoundationSettlement.Stress(2300, 2000, 0, 3000, .1, .02), "Simmetria pressione lineare");
            SettlementLayer[] Layers(double scale) => [new("A", 5000, 20 * scale), new("B", 15000, 40 * scale)];
            var settlement = FoundationSettlement.Calculate(Layers(1), 1500, 0, 3000, .1, .1, 0, 3000);
            Near(FoundationSettlement.Calculate(Layers(1), 1500, 0, 3000, .2, .2, 0, 3000).Settlement, 2 * settlement.Settlement, "Linearità cedimenti");
            Near(FoundationSettlement.Calculate(Layers(2), 1500, 0, 3000, .1, .1, 0, 3000).Settlement, settlement.Settlement / 2, "Moduli doppi dimezzano cedimento");
            Near(FoundationSettlement.Calculate(Layers(1), 1500, 0, 3000, .1, .1, .1, 3000).Settlement, 0, "Carico netto nullo");
            Reject(() => FoundationSettlement.Calculate([], 0, 0, 3000, .1, .1, 0, 3000), "Nessun modulo inventato");
            Reject(() => FoundationSettlement.Calculate(Layers(1), 1000, 0, 3000, .1, .1, .11, 3000), "Scarico non confuso con cedimento positivo");
            var shape = RetainingWall.IntegrateCurvature([new(0, .001), new(3, .001)], 3);
            Near(shape[^1].DisplacementMm, 4.5, "Integrale curvatura costante κH²/2");
            Near(RetainingWall.IntegrateCurvature([new(0, .003), new(3, 0)], 3)[^1].DisplacementMm, 9, "Integrale curvatura triangolare κ0H²/3");
            Near(NewmarkSliding.Calculate([new(0, .2), new(1, .2)], .1).Displacement, 981, "Newmark impulso rettangolare più arresto analitico");
            Near(NewmarkSliding.Calculate([new(0, .05), new(1, -.4), new(2, .1)], .1).Displacement, 0, "Newmark sotto soglia nessuno scorrimento");
            var triangle = NewmarkSliding.Calculate([new(0, 0), new(1, .2), new(2, 0)], .1);
            Check(triangle.Displacement > 0 && triangle.Points.Zip(triangle.Points.Skip(1)).All(p => p.Second.Displacement >= p.First.Displacement), "Newmark arresti e irreversibilità");
            Near(NewmarkSliding.Calculate([new(0, 0), new(.5, .1), new(1, .2), new(1.5, .1), new(2, 0)], .1).Displacement, triangle.Displacement, "Newmark indipendente da suddivisione lineare");
            Reject(() => NewmarkSliding.Calculate([new(0, 0), new(0, .2)], .1), "Tempi duplicati rifiutati");
            // ANTHEA keeps γRd also on the soil inertia (RetainingWall.ModelFactorOnSoilInertia).
            double phi = 34 * Math.PI / 180, gamma = 19e-6, nq = Math.Exp(Math.PI * Math.Tan(phi)) * Math.Pow(Math.Tan(Math.PI / 4 + phi / 2), 2);
            SeismicBearingResult Bearing(double n, double v, double m, double kh, double kv, double model, double r) => ShallowFoundationSeismic.Calculate(3000, gamma, phi, n, v, m * 1000, kh, kv, model, r, RetainingWall.ModelFactorOnSoilInertia);
            var capacity = Bearing(100, 0, 0, 0, 0, 1, 1);
            Near(capacity.Capacity, 19 * 9 * (nq - 1) * Math.Tan(phi), "Portanza Annex F limite verticale statico");
            var seismic = Bearing(100, 0, 0, .2, 0, 1.15, 1.2);
            Near(seismic.Capacity, capacity.NMax * Math.Pow(1 - .96 * 1.15 * .2 / Math.Tan(phi), .39) / (1.15 * 1.2), "Portanza sismica pura verifica chiusa");
            var interaction = Bearing(200, 40, 30, .2, .1, 1.15, 1.2);
            var atLimit = Bearing(interaction.Capacity, 40 * interaction.Capacity / 200, 30 * interaction.Capacity / 200, .2, .1, 1.15, 1.2);
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
