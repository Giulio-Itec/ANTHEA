using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Attributes;
using GPC.Checkers.Concrete.Checkers;
using GPC.Checkers.Concrete.SectionSolvers;
using GPC.Geometry;
using GPC.Model.Results;
using GPC.Model.Standards;
using X.Core;

internal static class BridgeSectionChecks
{
    internal static void Run()
    {
        int count = 0;
        void Assert(bool condition, string message) { if (!condition) throw new Exception("Sezione mista: " + message); count++; }
        void Near(double actual, double expected, double relative, string message) => Assert(Math.Abs(actual - expected) <= relative * Math.Max(1, Math.Abs(expected)), $"{message}: {actual:R} / {expected:R}");
        void Reject(JsonObject d, string message) { try { BridgeSection.Calculate(d); } catch (ArgumentException) { count++; return; } throw new Exception(message); }

        var d = BridgeSection.Defaults(); var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = BridgeSection.Calculate(d); Console.WriteLine($"Default: {watch.ElapsedMilliseconds} ms; η={result.Stages[^1].MaxUtilization:0.000}");
        Assert(result.Stages.Count == 3, "tre situazioni");
        Assert(result.Stages.All(s => s.Residual < 1e-7), "convergenza");
        Assert(result.Stages.SelectMany(s => s.Contributions).All(c => c.EquilibriumResidual < 1e-5), "equilibrio indipendente delle fasi");
        Assert(result.Stages.All(s => s.EffectiveSteel.Area <= result.Geometry.SteelArea * (1 + 1e-9)), "area efficace non maggiore della lorda");
        Assert(result.Stages[0].Effective.Web.Rho < 1, "classe 4 effettivamente attivata sul caso iniziale");
        Assert(result.Materials.Fck == 35, "fck nominale positivo (Model conserva la compressione negativa)");
        Assert(result.Stages.SelectMany(s => s.Points).All(p => p.Limit > 0 && (p.Utilization is null || p.Utilization >= 0)), "limiti e rapporti tensionali positivi");
        var overloaded = (JsonObject)d.DeepClone(); overloaded["fasi"] = new JsonArray(BridgeSection.Phase("Compressione", "Composta", -200000));
        Assert(BridgeSection.Calculate(overloaded).Stages[0].Points.Any(p => p.Material == "CLS" && p.Utilization > 1), "superamento compressione CLS rilevato");
        Assert(result.Stages[0].Points.Where(p => p.Material != "Acciaio").All(p => !p.Active && p.Utilization is null), "materiali inattivi");
        foreach (var stage in result.Stages) foreach (var point in stage.Points) Near(point.Stress, point.Contributions.Sum(), 1e-12, "somma contributi");
        var phase = d.Array("fasi")[1]!.AsObject(); var hom = BridgeSection.Homogenization(d, phase);
        var clone = (JsonObject)d.DeepClone(); var cp = clone.Array("fasi")[1]!.AsObject(); cp["modo"] = "Da n"; cp["n"] = hom.N;
        var inverse = BridgeSection.Homogenization(clone, cp); Near(inverse.Phi, hom.Phi, 1e-12, "inversa phi/n");
        var manual = BridgeSection.Calculate(clone);
        foreach (var (a, b) in result.Stages[^1].Points.Zip(manual.Stages[^1].Points)) Near(a.Stress, b.Stress, 1e-9, "identità ingresso phi/n");

        // Published JRC EN1993-1-5 worked example, web subpanel 2, p. 200: lambda=.912, rho=.871.
        var plate = BridgeSection.InternalPlate(492, 8, -100, -40.6, 235);
        Near(plate.KSigma, 5.632, .001, "JRC ksigma"); Near(plate.Lambda, .912, .002, "JRC lambda"); Near(plate.Rho, .871, .002, "JRC rho");
        var reverse = BridgeSection.InternalPlate(492, 8, -40.6, -100, 235);
        Near(plate.EffectiveAtStart, reverse.EffectiveAtEnd, 1e-12, "simmetria compressione anima");
        var tension = BridgeSection.InternalPlate(1800, 8, 100, 20, 355); Near(tension.Rho, 1, 0, "anima tesa interamente efficace");
        var compression = BridgeSection.InternalPlate(1800, 8, -100, -100, 355); Assert(compression.Rho < .3, "anima snella compressa ridotta");
        Near(compression.EffectiveAtStart, compression.EffectiveAtEnd, 1e-12, "compressione uniforme simmetrica");

        foreach (bool top in new[] { true, false }) foreach (bool bottom in new[] { true, false })
        {
            var bare = (JsonObject)d.DeepClone(); bare["rebars_top"] = top; bare["rebars_bottom"] = bottom;
            var g = BridgeSection.Geometry(bare); var actual = BridgeSection.Calculate(bare);
            Assert(g.Bars.Length == (top ? 20 : 0) + (bottom ? 20 : 0), "file opzionali e distribuzione Model");
            Assert(actual.Stages[^1].Points.Count(p => p.Material == "Armatura") == (top ? 1 : 0) + (bottom ? 1 : 0), "tensioni sole file presenti");
        }
        var two = (JsonObject)d.DeepClone(); two["plate2"] = true;
        var geometry = BridgeSection.Geometry(two); Near(geometry.BottomArea, 31000, 0, "area due piastre"); Near(geometry.BottomEquivalentWidth, 620, 0, "larghezza equivalente");
        Assert(BridgeSection.Calculate(two).Stages[^1].Warnings.Any(w => w.Contains("Due piastre")), "approssimazione esplicita");
        var cracked = (JsonObject)d.DeepClone(); cracked.Array("fasi")[1]!["tipo"] = "Soletta esclusa"; cracked.Array("fasi")[2]!["tipo"] = "Soletta esclusa";
        Assert(BridgeSection.Calculate(cracked).Stages[^1].Points.Where(p => p.Material == "CLS").All(p => !p.Active), "soletta completamente esclusa");
        var inactive = (JsonObject)d.DeepClone(); inactive.Array("fasi")[1]!["attiva"] = false;
        Assert(BridgeSection.Calculate(inactive).Stages.Count == 2, "fasi disattive");
        var zero = (JsonObject)d.DeepClone(); foreach (var p in zero.Array("fasi")) { p!["N"] = 0; p["Mx"] = 0; }
        Assert(BridgeSection.Calculate(zero).Stages[^1].Points.All(p => p.Stress == 0), "azioni nulle");

        // The section used by Checker MixedSectionTest.TensionCheck03 (Bridge).
        var benchmark = BridgeSection.Defaults(); benchmark["b_cls"] = 1200; benchmark["h_cls"] = 200; benchmark["classe_cls"] = "C40/50";
        benchmark["h_web"] = 600 - 2 * 32.4; benchmark["t_web"] = 21.6; benchmark["b_top"] = 215; benchmark["b_bottom"] = 215;
        benchmark["t_top"] = 32.4; benchmark["t_bottom"] = 32.4; benchmark["d_top"] = 12; benchmark["pitch_top"] = 150; benchmark["cover_top"] = 60;
        benchmark["rebars_bottom"] = false; benchmark["classe4"] = false;
        benchmark["fasi"] = new JsonArray(BridgeSection.Phase("Checker Bridge", "Composta", 0, 2000));
        var br = BridgeSection.Calculate(benchmark).Stages[0];
        Near(br.Points.Where(p => p.Material == "Acciaio").Max(p => p.Stress), 270.88, .006, "benchmark Checker acciaio");
        Near(br.Points.Where(p => p.Material == "CLS").Min(p => p.Stress), -20.84, .006, "benchmark Checker CLS");
        var section = BridgeSection.NativeSection(benchmark);
        var local = new CoordinateSystem(new Point2d(600, 0), new Vector3d(-1, 0, 0), new Vector3d(0, -1, 0));
        foreach (double phi in new[] { 0d, 2d })
        {
            benchmark.Array("fasi")[0]!["phi"] = phi;
            benchmark.Array("fasi")[0]!["N"] = -200;
            var options = new SectionCheckerModelCode2010.SectionOptionsModelCode2010(local, SectionSolver.FailureAnalysisTypes.ConstantN,
                SectionSolver.FailureDomainTypes.Elastic, SectionSolver.StressAnalysisTypes.Linear, phi, 0, true, 16);
            var checker = new SectionCheckerModelCode2010(new SectionCheckerAttribute(section), options, new StandardNTC2018Concrete(), true, -1, new StandardEN1993p11());
            var native = checker.SectionSolver.GetLinearStressAnalysisResult(new ResultBeamForces(-200000, 0, 0, 0, 2e9, 0, local), phi, 0);
            var adapted = BridgeSection.Calculate(benchmark).Stages[0].Contributions[0];
            var nativeProps = section.GetHomogeneizedMechanicalProperties(phi); Near(adapted.Area, nativeProps.areaH / adapted.HomogenizationN, 1e-12, "area Model"); Near(adapted.Inertia, nativeProps.JxxH / adapted.HomogenizationN, 1e-12, "inerzia Model"); Near(adapted.Centroid, nativeProps.centroidH.Y, 1e-12, "baricentro Model");
            foreach (var point in native.GetStructuralSteelVerticesTension(phi)) Near(adapted.SteelStress(point.point.Y), point.tension, .00001, "confronto API Checker steel phi=" + phi);
            foreach (var point in native.GetConcreteVerticesTension(phi)) Near(adapted.Stress("CLS", point.point.Y), point.tension, .00001, "confronto API Checker cls phi=" + phi);
        }
        // A shift in reference must leave stresses unchanged when M is transported consistently.
        var at0 = BridgeSection.Calculate(benchmark).Stages[0]; benchmark["y_ref"] = 500; benchmark.Array("fasi")[0]!["Mx"] = 1900;
        var shifted = BridgeSection.Calculate(benchmark).Stages[0];
        foreach (var (a, b) in at0.Points.Zip(shifted.Points)) Near(a.Stress, b.Stress, 1e-10, "trasporto N-M");
        var archive = Archivio.Documento(BridgeSection.Module); archive["dati"] = two.DeepClone(); Archivio.Valida(archive);
        Assert(JsonNode.DeepEquals(two, JsonNode.Parse(archive.ToJsonString())!["dati"]), "roundtrip archivio");
        Assert(result.Json()["Stages"]!.AsArray().Count == 3, "export JSON completo");
        var invalid = (JsonObject)d.DeepClone(); invalid["t_web"] = "abc"; Reject(invalid, "spessore invalido accettato");
        invalid = (JsonObject)d.DeepClone(); invalid["plate2"] = true; invalid["b_bottom2"] = 900; Reject(invalid, "piastra sporgente accettata");
        invalid = (JsonObject)d.DeepClone(); invalid.Array("fasi")[1]!["modo"] = "Da n"; invalid.Array("fasi")[1]!["n"] = 1; Reject(invalid, "n inferiore a n0 accettato");
        invalid = (JsonObject)d.DeepClone(); invalid.Array("fasi")[2]!["tipo"] = "Solo acciaio"; Reject(invalid, "sequenza errata accettata");
        invalid = (JsonObject)d.DeepClone(); invalid["cover_bottom"] = 205; Reject(invalid, "file sovrapposte accettate");
        Console.WriteLine($"Sezione mista: {count} controlli superati.");
    }
}
