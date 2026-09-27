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
        Console.WriteLine($"Esempi guide H inclinata e cassoncino: {count} controlli superati.");
        if (Environment.GetEnvironmentVariable("BRIDGE_INCLINED_OUTPUT") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(new { Checks = count, Rows = evidence }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
