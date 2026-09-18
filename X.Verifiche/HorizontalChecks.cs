using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using X.Core;

public static class HorizontalChecks
{
    public static int Run()
    {
        int count = 0;
        void Assert(bool yes, string message) { if (!yes) throw new Exception(message); count++; }
        void Near(double actual, double expected, string name, double rel = 1e-6) => Assert(Math.Abs(actual - expected) <= rel * Math.Max(1, Math.Abs(expected)), $"{name}: {actual:R} != {expected:R}");
        JsonObject Data(bool clay, bool fixedHead, double length, double moment, double eccentricity = 0)
        {
            var data = PaloOrizzontale.Defaults(); var g = data["generali"]!;
            g["origine_momento"] = "Manuale"; g["momento_resistente"] = moment; g["provenienza_momento"] = "Momento assegnato per benchmark di equilibrio";
            g["lunghezza"] = length; g["vincolo"] = fixedHead ? "Impedita" : "Libera"; g["eccentricita"] = eccentricity;
            var layer = PaloOrizzontale.Layer(); layer["spessore"] = length; layer["tipologia"] = clay ? "Coesivo" : "Granulare";
            data["stratigrafie"] = new JsonArray(new JsonArray(layer)); return data;
        }
        JsonObject Calc(JsonObject data)
        {
            string before = data.ToJsonString(); var r = PaloOrizzontale.Calculate(data);
            Assert(r.S("errore") == "", r.S("errore")); Assert(before == data.ToJsonString(), "Calcolo muta input");
            foreach (var s in r.Array("sondaggi"))
            {
                Near(s.D("residuo_forza_kn"), 0, "Equilibrio forze", 1e-3);
                Near(s.D("residuo_momento_knm"), 0, "Equilibrio momenti", 1e-3);
                Assert(s.D("momento_massimo_knm") <= r.D("momento_resistente_knm") * 1.00001, "Superato My");
                Assert(s.Array("diagrammi").All(p => Math.Abs(p.D("m_knm")) <= r.D("momento_resistente_knm") * 1.00001), "Diagramma supera My");
                Near(s.Array("diagrammi")[^1].D("v_kn"), 0, "Taglio piede"); Near(s.Array("diagrammi")[^1].D("m_knm"), 0, "Momento piede");
            }
            return r;
        }
        // Independently chosen exact equilibria; Viggiani equations identified in docs.
        foreach (var (clay, fix, l, my, expected, mechanism) in new[] {
            (true, false, 2.5 + Math.Sqrt(8), 10000d, 450d, "Corto"),
            (true, false, 10d, 900d, 450d, "Lungo"),
            (true, true, 2.5, 10000d, 450d, "Corto"),
            (true, true, 5.5, 1800d, 900d, "Intermedio"),
            (true, true, 10d, 450d, 450d, "Lungo"),
            (false, false, 2d, 1000d, 108d, "Corto"),
            (false, false, 10d, 432d, 324d, "Lungo"),
            (false, true, 2d, 1000d, 324d, "Corto"),
            (false, true, 2d, 108d, 162d, "Intermedio"),
            (false, true, 10d, 216d, 324d, "Lungo") })
        {
            var data = Data(clay, fix, l, my); var result = Calc(data);
            Near(result.D("capacita_kn"), expected, "Benchmark " + mechanism); Assert(result.S("meccanismo") == mechanism, "Meccanismo errato");
            // Identical interfaces, including one within the 1.5D surface zone.
            var layers = new JsonArray(); foreach (double thickness in new[] { .5, .7, l - 1.2 })
            { var layer = (JsonObject)data["stratigrafie"]![0]![0]!.DeepClone(); layer["spessore"] = thickness; layers.Add(layer); }
            data["stratigrafie"] = new JsonArray(layers); Near(Calc(data).D("capacita_kn"), expected, "Interfacce fittizie");
            data["generali"]!["modalita"] = "Multistrato sperimentale"; Near(Calc(data).D("capacita_kn"), expected, "Recupero omogeneo");
        }
        Near(Calc(Data(false, false, 2, 1000, 1)).D("capacita_kn"), 72, "Eccentricità sabbia");
        // Clay f=1, e=1, My=450*(1+1.5+0.5)=1350.
        Near(Calc(Data(true, false, 10, 1350, 1)).D("capacita_kn"), 450, "Eccentricità argilla");
        var water = Data(false, false, 2, 1000); water["generali"]!["presenza_falda"] = true;
        Near(Calc(water).D("capacita_kn"), .5 * 3 * (20 - 9.81) * 4, "Falda superficie");
        water["generali"]!["profondita_falda"] = 1; water["generali"]!["modalita"] = "Multistrato sperimentale";
        // Integrate 9*18*z*(2-z) on [0,1], and 9*(18+10.19*(z-1))*(2-z) on [1,2], then divide by L.
        Near(Calc(water).D("capacita_kn"), (108 + 81 + 9 * 10.19 / 6) / 2, "Falda interna", 1e-6);
        var multi = Data(false, true, 6, 1000); multi["generali"]!["modalita"] = "Multistrato sperimentale";
        var first = multi["stratigrafie"]![0]![0]!; first["spessore"] = 2;
        var second = (JsonObject)first.DeepClone(); second["spessore"] = 4; second["angolo_attrito"] = 35; second["peso_specifico"] = 21;
        multi["stratigrafie"]![0]!.AsArray().Add(second); var baseline = Calc(multi);
        multi["generali"]!["passo"] = .025; Near(Calc(multi).D("capacita_kn"), baseline.D("capacita_kn"), "Passo diagrammi indipendente dal solutore");
        second["spessore"] = 1; var third = (JsonObject)second.DeepClone(); third["spessore"] = 3; multi["stratigrafie"]![0]!.AsArray().Add(third);
        Near(Calc(multi).D("capacita_kn"), baseline.D("capacita_kn"), "Interfaccia fittizia multistrato");
        foreach (var bad in new Action<JsonObject>[] {
            a => a["generali"]!["lunghezza"] = "NaN", a => a["generali"]!["diametro"] = 0,
            a => a["generali"]!["passo"] = 0, a => a["generali"]!["tolleranza"] = 0,
            a => a["generali"]!["vincolo"] = "altro", a => a["generali"]!["momento_applicato"] = 10,
            a => a["generali"]!["origine_momento"] = "altro", a => a["generali"]!["provenienza_momento"] = "",
            a => a["stratigrafie"]![0]![0]!["spessore"] = .5, a => a["stratigrafie"]![0]![0]!["coesione_efficace"] = 10,
            a => a["stratigrafie"]![0]![0]!["angolo_attrito"] = 90,
            a => { a["generali"]!["vincolo"] = "Impedita"; a["generali"]!["eccentricita"] = 1; },
            a => { var row = PaloOrizzontale.Layer(); row["tipologia"] = "Coesivo"; a["stratigrafie"]![0]![0]!["spessore"] = 1; a["stratigrafie"]![0]!.AsArray().Add(row); },
            a => { a["verifica"]!["applica_fattori"] = true; a["verifica"]!["xi"] = .5; }
        }) { var a = Data(false, false, 10, 1000); bad(a); Assert(PaloOrizzontale.Calculate(a).S("errore") != "", "Input invalido accettato"); }
        Assert(PaloOrizzontale.Calculate(Data(true, false, 1, 1000)).S("errore") != "", "Argilla L<1.5D accettata");
        var factored = Data(false, false, 2, 1000); factored["verifica"]!["applica_fattori"] = true;
        factored["verifica"]!["xi"] = 1.5; factored["verifica"]!["gamma_r"] = 1.2; factored["verifica"]!["riferimento"] = "Solo test, non normativo";
        var fr = Calc(factored); Near(fr.D("resistenza_progetto_manuale_kn"), 60, "Fattori singola applicazione");
        Assert(fr.S("verifica_normativa") == "Incompleta", "Conformità attribuita senza percorso normativo");
        var section = Data(false, false, 10, 1000); section["generali"]!["origine_momento"] = "Sezione c.a.";
        var sec = PaloOrizzontale.Section(section); Near(sec.D("residuo_n_kn"), 0, "Equilibrio assiale sezione", 1e-6);
        Assert(sec.D("scarto_mesh") < .02, "Sezione non converge");
        Near(sec.D("area_acciaio_mm2"), 16 * Math.PI * 24 * 24 / 4, "Area barre e unità mm");
        // Independent circular strip integration reference for the same constitutive model.
        Near(sec.D("momento_knm"), StripMoment(section), "Momento riferimento strisce", .004);
        var sr = Calc(section); Near(sr.D("momento_resistente_knm"), sec.D("momento_knm"), "Collegamento My-Broms");
        section["generali"]!["azione_assiale"] = 2500;
        Near(PaloOrizzontale.Section(section).D("momento_knm"), StripMoment(section), "Sezione con N positivo", .004);
        section["generali"]!["azione_assiale"] = 1e8;
        Assert(PaloOrizzontale.Calculate(section).S("errore") != "", "N fuori campo accettato");
        section["generali"]!["azione_assiale"] = 0;
        section["sezione"]!["longitudinal_bar_count"] = 5;
        Assert(PaloOrizzontale.Calculate(section).S("errore") != "", "Armatura non bisimmetrica accettata");
        section["sezione"]!["longitudinal_bar_count"] = 16;
        var temp = Path.Combine(Path.GetTempPath(), "ANTHEA_horizontal_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
        try
        {
            var doc = J.Obj(("formato", "X"), ("versione", 1), ("tipo", "calcolo"), ("modulo_id", PaloOrizzontale.Module), ("dati", section));
            string file = Path.Combine(temp, "roundtrip.programma"); Archivio.Scrivi(file, doc); Assert(JsonNode.DeepEquals(doc, Archivio.Leggi(file)), "Roundtrip orizzontale");
            Assert(PaloOrizzontale.Csv(sr).Contains("p_kN_m"), "CSV mancante");
            string report = Path.Combine(temp, "report.docx"); ReportOrizzontale.Write(report, "Test", sr);
            using var zip = ZipFile.OpenRead(report); using var stream = zip.GetEntry("word/document.xml")!.Open();
            string xml = XDocument.Load(stream).ToString(); Assert(xml.Contains("Residui di equilibrio") && xml.Contains("Incompleta") && xml.Contains("Diagrammi tabellari"), "Relazione incompleta");
        }
        finally { Directory.Delete(temp, true); }
        return count;
    }

    // Simpson integration by horizontal strips, independent of SezioneCA's polar mesh.
    private static double StripMoment(JsonObject data)
    {
        var p = data["sezione"]!; double r = data["generali"].D("diametro") * 500, rb = r - p.D("cover_mm") - p.D("transverse_bar_diameter_mm") - p.D("longitudinal_bar_diameter_mm") / 2;
        double fcd = p.D("alpha_cc") * p.D("fck_mpa") / p.D("gamma_c"), fyd = p.D("fyk_mpa") / p.D("gamma_s"), es = p.D("steel_modulus_mpa");
        double area = Math.PI * Math.Pow(p.D("longitudinal_bar_diameter_mm"), 2) / 4; int bars = (int)p.D("longitudinal_bar_count");
        (double N, double M) Integrate(double x)
        {
            double C(double y) { double strain = .0035 * (y - r + x) / x; return strain <= 0 ? 0 : strain >= .002 ? fcd : fcd * (1 - Math.Pow(1 - strain / .002, 2)); }
            double n = 0, m = 0; const int divisions = 16000; double dy = 2 * r / divisions;
            for (int i = 0; i <= divisions; i++) { double y = -r + i * dy, force = 2 * Math.Sqrt(Math.Max(0, r * r - y * y)) * C(y) * dy / 3 * (i == 0 || i == divisions ? 1 : i % 2 == 0 ? 2 : 4); n += force; m += force * y; }
            for (int i = 0; i < bars; i++) { double y = rb * Math.Sin(2 * Math.PI * i / bars), force = (Math.Clamp(es * .0035 * (y - r + x) / x, -fyd, fyd) - C(y)) * area; n += force; m += force * y; }
            return (n / 1000, m / 1e6);
        }
        double lo = .001, hi = 2 * r, target = data["generali"].D("azione_assiale");
        for (int i = 0; i < 55; i++) { double mid = (lo + hi) / 2; if (Integrate(mid).N < target) lo = mid; else hi = mid; }
        return Integrate((lo + hi) / 2).M;
    }
}
