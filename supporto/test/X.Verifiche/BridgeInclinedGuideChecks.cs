using System.Text.Json.Nodes;
using X.Core;

/// <summary>Independent hand references for the inclined-section examples in the guides.
/// Geometric integrals and elastic stress references do not call the section solver.</summary>
internal static class BridgeInclinedGuideChecks
{
    internal static void Run()
    {
        int count = 0;
        var evidence = new List<object>();
        string caseId = "";
        void Check(bool ok, string name) { count++; if (!ok) throw new Exception($"Guide sezioni inclinate {caseId}: {name}"); }
        void Near(double actual, double expected, double abs, string name)
        {
            Check(double.IsFinite(actual) && Math.Abs(actual - expected) <= abs, $"{name}: {actual:R} / {expected:R}");
            evidence.Add(new { Case = caseId, Name = name, Expected = expected, Actual = actual, Tolerance = abs });
        }
        JsonObject Data(bool box, double offset)
        {
            var d = BridgeSection.Defaults();
            d["sezione"] = BridgeSection.SectionTypes[box ? 2 : 1];
            d["offset_anima"] = offset; d["interasse_anime"] = 1800;
            d["b_top"] = box ? 450 : 500; d["b_bottom"] = box ? 1400 : 700;
            d["t_bottom"] = box ? 25 : 30; d["classe4"] = false;
            d["rebars_top"] = false; d["rebars_bottom"] = false;
            d["fibre_anima"] = 32; d["fibre_flange"] = 4; d["fibre_cls"] = 16; d["sottopassi"] = 4;
            return d;
        }
        foreach (var sample in new (bool box, double offset)[] { (false, 300d), (false, -300d), (false, 0d), (true, 250d) })
        {
            bool box = sample.box; double offset = sample.offset;
            caseId = (box ? "BOX" : "H") + offset;
            var d = Data(box, offset);
            double hw = 1800, tw = 14, tt = 25, bt = box ? 450 : 500, bb = box ? 1400 : 700, tb = box ? 25 : 30;
            int webs = box ? 2 : 1;
            double length = Math.Sqrt(hw * hw + offset * offset), cos = hw / length;
            double at = webs * bt * tt, aw = webs * tw * length, ab = bb * tb;
            double yt = -tt / 2, yw = -tt - hw / 2, yb = -tt - hw - tb / 2;
            double area = at + aw + ab, yc = (at * yt + aw * yw + ab * yb) / area;
            double ix = at * (tt * tt / 12 + Math.Pow(yt - yc, 2))
                + aw * (hw * hw / 12 + Math.Pow(yw - yc, 2))
                + ab * (tb * tb / 12 + Math.Pow(yb - yc, 2));
            var g = BridgeSection.Geometry(d);
            Near(g.PlateLength, length, 1e-9, "lunghezza reale lamiera");
            Near(g.WebAngle, Math.Atan(offset / hw), 1e-12, "angolo con segno");
            Near(g.WebThickness, webs * tw / cos, 1e-10, "larghezza totale equivalente");
            Near(g.SteelArea, area, 1e-7, "area da integrali geometrici");
            Near(g.SteelCentroid, yc, 1e-8, "baricentro da momenti statici");
            Near(g.SteelInertia, ix, .01, "inerzia orizzontale da integrali");
            if (box)
            {
                Near(g.WebSpacingBottom, 1300, 1e-10, "interasse al piede");
                Near(g.BottomInternalWidth, 1300 - tw / cos, 1e-10, "fondo interno netto");
                Near(g.BottomOutstandWidth, (1400 - 1300 - tw / cos) / 2, 1e-10, "sbalzo del fondo");
                Near(BridgeSection.EffectiveWidths(g, _ => -100, 355).Bottom.KSigma, 4, 1e-12, "fondo interno in compressione uniforme");
            }
            foreach (int method in new[] { 0, 1, 2 })
            {
                d["metodo_analisi"] = BridgeSection.CalculationMethods[method];
                var phase = BridgeSection.Phase("Riferimento elastico", "Solo acciaio", -200, 100); phase["V"] = 600;
                d["fasi"] = new JsonArray(phase);
                string before = d.ToJsonString(); var result = BridgeSection.Calculate(d); var stage = result.Stages.Single();
                Check(before == d.ToJsonString(), "calcolo senza modifica dell'archivio");
                // Moment specified at y=0, compression negative; sigma=N/A-Mc*(y-yc)/I.
                double mc = 100e6 - 200000 * yc;
                foreach (var p in stage.Points.Where(p => p.Active && p.Material == "Acciaio"))
                    Near(p.Stress, -200000 / area - mc * (p.Y - yc) / ix, 1e-4, "tensione analitica metodo " + method);
                if (method == 0)
                {
                    var s = stage.Shear!;
                    Near(s.TauAverage, 600000 / (webs * hw * tw), 1e-9, "taglio medio sul piano delle anime");
                    double tauCr = 5.34 * Math.PI * Math.PI * 210000 / (12 * (1 - .3 * .3)) * Math.Pow(tw / length, 2);
                    Near(s.Web.TauCritical, tauCr, 1e-7, "instabilita a taglio sulla lunghezza reale");
                }
                else Check(stage.Shear is null, "metodo storico senza certificazione dei controlli locali");
            }
            var archived = Archivio.Documento(BridgeSection.Module); archived["dati"] = d.DeepClone(); Archivio.Valida(archived);
            var restored = JsonNode.Parse(archived.ToJsonString())!["dati"]!.AsObject();
            Check(JsonNode.DeepEquals(restored, d), "salvataggio e riapertura tipo e scostamento");
            Near(BridgeSection.Geometry(restored).SteelInertia, ix, .01, "geometria dopo riapertura");
        }
        caseId = "LIMITI";
        foreach (double offset in new[] { -1800d, 1800d })
            Near(Math.Abs(BridgeSection.Geometry(Data(false, offset)).WebAngle), Math.PI / 4, 1e-12, "45 gradi ammessi");
        foreach (double offset in new[] { -1800.1, 1800.1 })
        {
            bool rejected = false;
            try { BridgeSection.Geometry(Data(false, offset)); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "oltre 45 gradi respinto");
        }
        TorsionExample(Near, Check, id => caseId = id);
        Console.WriteLine($"Esempi guide H inclinata e cassoncino: {count} controlli superati.");
        if (Environment.GetEnvironmentVariable("BRIDGE_INCLINED_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(new { Checks = count, Rows = evidence }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }

    /// <summary>
    /// The torsion example of the guides (revision 04): the box of the examples with the default slab and phases, torques 200, 300 and
    /// 1000 kNm, bracing t* = 4 mm. Cell areas, flows, J, τ and the support diaphragm by hand; distortion as printed in the guides
    /// </summary>
    private static void TorsionExample(Action<double, double, double, string> near, Action<bool, string> check, Action<string> setCase)
    {
        setCase("TORSIONE");
        var d = BridgeSection.Defaults();
        d["sezione"] = BridgeSection.SectionTypes[2]; d["offset_anima"] = 250; d["interasse_anime"] = 1800;
        d["b_top"] = 450; d["b_bottom"] = 1400; d["t_bottom"] = 25;
        d["torsione_cassoncino"] = true; d["t_controvento"] = 4;
        double[] torques = [200, 300, 1000];
        for (int i = 0; i < 3; i++) d.Array("fasi")[i]!["T"] = torques[i];
        // axis of the webs extended: half distance at the level y (0 at the top of the steel)
        double Half(double y) => 900 + (y + 25) * 250 / 1800.0;
        double yb = -25 - 1800 - 12.5, ys = -12.5, yc = 125;
        double bs = 2 * Half(ys), bc = 2 * Half(yc), bb = 2 * Half(yb), hs = ys - yb, hc = yc - yb;
        double a0s = (bs + bb) / 2 * hs, a0c = (bc + bb) / 2 * hc;
        double ws = Math.Sqrt(Math.Pow((bs - bb) / 2, 2) + hs * hs), wc = Math.Sqrt(Math.Pow((bc - bb) / 2, 2) + hc * hc);
        var result = BridgeSection.Calculate(d);
        var torsion = result.Stages[^1].Torsion!;
        double n0 = result.Materials.Ea / result.Materials.Ec;
        double[] n = [0, n0 * (1 + 1.1 * 2) * 1.2 / 1.3, n0 * 1.2 / 1.3];
        double[] area = [a0s, a0c, a0c];
        for (int i = 0; i < 3; i++)
        {
            var f = torsion.Flows[i];
            near(f.CellArea, area[i], 1e-6, "A0 fase " + (i + 1));
            near(f.Flow, torques[i] * 1e6 / (2 * area[i]), 1e-9, "q = T/(2A0) fase " + (i + 1));
            double j = i == 0 ? 4 * a0s * a0s / (bs / 4 + 2 * ws / 14 + bb / 25) : 4 * a0c * a0c / (bc / (250 / n[i]) + 2 * wc / 14 + bb / 25);
            near(f.TorsionConstant, j, 1e-6 * j, "J fase " + (i + 1));
        }
        double q = torques.Select((t, i) => t * 1e6 / (2 * area[i])).Sum();
        near(torsion.WebFlow, q, 1e-9, "q anime e fondo");
        near(torsion.Details.Single(x => x.Name == "Torsione · τ anime").Value, q / 14, 1e-9, "τ anime");
        near(torsion.Details.Single(x => x.Name == "Torsione · τ fondo").Value, q / 25, 1e-9, "τ fondo");
        check(result.Stages[^1].Shear!.Checks[0].Note.Contains("torsione"), "taglio dell'anima con la torsione");
        // distortion and support diaphragm
        d["L_campata"] = 40000; d["passo_diaframmi"] = 5000; d["m_t_dist"] = 60; d["T_c_dist"] = 600;
        d["T_app"] = 1500; d["e_appoggi"] = 1300; d["t_diaframma_app"] = 15;
        var full = BridgeSection.Calculate(d).Stages[^1].Torsion!;
        var dw = full.Distortion!;
        near(full.Checks.Single(c => c.Name == "Diaframma d'appoggio · taglio da torsione").Demand, 1500e6 / (2 * a0s * 15), 1e-9, "τ diaframma d'appoggio");
        near(full.Details.Single(x => x.Name == "Appoggio · coppia degli apparecchi T/e_b").Value, 1500e3 / 1300, 1e-9, "coppia degli apparecchi");
        check(dw.Diaphragms == 7, "7 diaframmi intermedi");
        // the values printed in the guides (rounded as printed)
        near(dw.WarpingInertia / 1e18, .010042, 5e-7, "I_Dw della guida");
        near(dw.FrameStiffness / 1000, 607.168, 5e-4, "K della guida");
        near(dw.DiaphragmStiffness / 1e9, 2872.26, 5e-3, "K_D della guida");
        near(Math.Abs(dw.TorqueLoad), .341377, 5e-7, "carico generalizzato della guida");
        near(dw.Amplitude * 1e4, 2.762, 5e-4, "ψ massimo della guida");
        near(dw.WarpingStressBottom, 14.946, 5e-4, "σdw della guida");
        near(dw.BendingRatio * 100, 20.2, .05, "σdw / σ flessione della guida");
        near(dw.CornerMomentBottom / 1000, .0434, 5e-5, "momento trasversale della guida");
        near(full.Details.Single(x => x.Name == "Diaframma intermedio · τ").Value, 8.394, 5e-4, "τ del diaframma della guida");
        Console.WriteLine($"Guida torsione: A0 {a0s / 1e6:F6} / {a0c / 1e6:F6} m²; q {string.Join(" / ", torsion.Flows.Select(f => f.Flow.ToString("F4")))} kN/m; " +
            $"J {string.Join(" / ", torsion.Flows.Select(f => (f.TorsionConstant / 1e12).ToString("F6")))} m⁴; I_Dw {dw.WarpingInertia / 1e18:F6} m⁶; K {dw.FrameStiffness / 1000:F3} kN·m/m; " +
            $"K_D {dw.DiaphragmStiffness / 1e9:F2} MN·m; carico {Math.Abs(dw.TorqueLoad):F6}; ψ {dw.Amplitude:E4}; ψD {dw.DiaphragmAmplitude:E4}; σdw {dw.WarpingStressBottom:F3} / {dw.WarpingStressTop:F3} MPa; " +
            $"rapporto {dw.BendingRatio:F4}; m {dw.CornerMomentBottom:F3} / {dw.CornerMomentTop:F3} N·mm/mm; " +
            $"τD {full.Details.Single(x => x.Name == "Diaframma intermedio · τ").Value:F4} MPa");
    }
}
