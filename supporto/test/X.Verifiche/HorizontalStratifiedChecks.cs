using System.IO.Compression;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using X.Core;

internal static class HorizontalStratifiedChecks
{
    public static int Run()
    {
        int count = 0;
        void Check(bool value, string reason) { count++; if (!value) throw new Exception(reason); }
        void Near(double value, double expected, string reason, double rel = 2e-7)
            => Check(double.IsFinite(value) && Math.Abs(value - expected) <= rel * Math.Max(1, Math.Abs(expected)), $"{reason}: {value:R} != {expected:R}");

        JsonObject Layer(string kind, double thickness, double cu = 50, double phi = 30, double gamma = 18)
        {
            var layer = PaloOrizzontale.Layer(); layer["tipologia"] = kind; layer["spessore"] = thickness;
            layer["coesione_non_drenata"] = cu; layer["angolo_attrito"] = phi; layer["peso_specifico"] = gamma;
            // The soils of Model have γsat ≥ γ (20 kN/m³ of the default layer, or γ when heavier).
            layer["peso_specifico_saturo"] = Math.Max(20, gamma);
            return layer;
        }
        JsonObject Data(bool fixedHead, double moment, params JsonObject[] layers)
        {
            var data = PaloOrizzontale.Defaults(); var g = data["generali"]!;
            g["lunghezza"] = layers.Sum(r => r.D("spessore")); g["vincolo"] = fixedHead ? "Impedita" : "Libera";
            g["metodo_calcolo"] = PaloOrizzontale.StratifiedMethod; g["origine_momento"] = "Manuale";
            g["momento_resistente"] = moment; g["provenienza_momento"] = "Benchmark di equilibrio";
            data["stratigrafie"] = new JsonArray(new JsonArray(layers.Select(r => (JsonNode)r).ToArray()));
            return data;
        }
        JsonObject Calc(JsonObject data)
        {
            string before = data.ToJsonString(); var r = PaloOrizzontale.Calculate(data);
            Check(r.S("errore") == "", r.S("errore")); Check(data.ToJsonString() == before, "Input modificato");
            Check(r.S("metodo_calcolo") == PaloOrizzontale.StratifiedMethod && r.B("sperimentale"), "Attribuzione metodo");
            Check(PaloOrizzontale.ModelloAutomatico(data) == r.S("modello_adottato"), "Anteprima metodo incoerente");
            var s = r.Array("sondaggi")[0]!;
            Near(s.D("risultante_concentrata_kn"), 0, "Nessuna forza fittizia al piede");
            Near(s.D("residuo_forza_kn"), 0, "Residuo forze", 1e-5);
            Near(s.D("residuo_momento_knm"), 0, "Residuo momenti", 1e-4);
            Check(s.D("fine_reazioni_m") <= data["generali"].D("lunghezza"), "Chiusura fuori palo");
            Check(s.Array("diagrammi").All(p => Math.Abs(p.D("m_knm")) <= r.D("momento_resistente_knm") * (1 + 1e-6)), "Limite My");
            // Independent integration of the signed diagram by 2-point Gauss on
            // each physical interval: exact for p and z*p (linear/quadratic).
            var diagram = s.Array("diagrammi"); double force = 0, firstMoment = 0;
            for (int i = 1; i < diagram.Count; i++)
            {
                var a = diagram[i - 1]!; var b = diagram[i]!; double dz = b.D("z") - a.D("z");
                if (dz == 0)
                {
                    Near(b.D("v_kn"), a.D("v_kn"), "Continuità V all'interfaccia", 1e-5);
                    Near(b.D("m_knm"), a.D("m_knm"), "Continuità M all'interfaccia", 1e-5);
                    continue;
                }
                foreach (double t in new[] { .5 - .5 / Math.Sqrt(3), .5 + .5 / Math.Sqrt(3) })
                {
                    double p = a.D("p_kn_m") * (1 - t) + b.D("p_kn_m") * t;
                    double z = a.D("z") + t * dz;
                    force += p * dz / 2; firstMoment += p * z * dz / 2;
                }
            }
            Near(force, r.D("capacita_kn"), "Integrale indipendente delle reazioni");
            Near(firstMoment, -s.D("momento_testa_knm"), "Momento globale indipendente", 2e-6);
            return r;
        }

        // Exact cohesive benchmarks, including the original one-layer limit.
        foreach (var (fix, length, moment, h, mechanism) in new[] {
            (true, 2.5, 10000d, 450d, "Corto"), (true, 5.5, 1800d, 900d, "Intermedio"),
            (true, 10d, 450d, 450d, "Lungo"), (false, 2.5 + Math.Sqrt(8), 10000d, 450d, "Corto"),
            (false, 10d, 900d, 450d, "Lungo") })
        {
            var data = Data(fix, moment, Layer("Coesivo", length)); var r = Calc(data);
            Near(r.D("capacita_kn"), h, "Recupero coesivo omogeneo"); Check(r.S("meccanismo") == mechanism, "Meccanismo coesivo");
        }
        // Mixed short pile, independently integrated by hand:
        // p=360 on [1.5,2], p=324+162(z-2) on [2,4].
        var mixed = Data(true, 10000, Layer("Coesivo", 2, 40), Layer("Granulare", 2));
        var mixedResult = Calc(mixed);
        Near(mixedResult.D("capacita_kn"), 1152, "Misto corto, forza esatta");
        Near(mixedResult["sondaggi"]![0].D("momento_testa_knm"), -3339, "Misto corto, momento esatto");
        mixed["generali"]!["presenza_falda"] = true; mixed["generali"]!["profondita_falda"] = 1;
        mixedResult = Calc(mixed);
        Near(mixedResult.D("capacita_kn"), 870.84, "Falda dentro lo strato coesivo sovrastante");
        Near(mixedResult["sondaggi"]![0].D("momento_testa_knm"), -2448.66, "Momento con falda");
        var stresses = mixedResult["sondaggi"]![0].Array("diagramma_terreno");
        var interfaceRows = stresses.Where(p => p.D("z") == 2).ToArray();
        Check(interfaceRows.Length == 2, "Due lati dell'interfaccia nel terreno");
        foreach (var p in interfaceRows)
        {
            Near(p.D("sigma_v_kpa"), 38, "Tensione verticale totale attraverso coesivo");
            Near(p.D("u_kpa"), 9.81, "Pressione idrostatica");
            Near(p.D("sigma_eff_kpa"), 28.19, "Tensione efficace continua all'interfaccia");
        }
        Near(interfaceRows[0].D("p_lim_kn_m"), 360, "Limite coesivo prima interfaccia");
        Near(interfaceRows[1].D("p_lim_kn_m"), 253.71, "Limite granulare dopo interfaccia");
        Check(mixedResult.Array("riferimenti").Any(r => r.S("id") == "NZGS" && r.S("url").Contains("nzgs.org")), "Riferimento NZGS");
        Check(PaloOrizzontale.Csv(mixedResult).Contains("sigma_eff_kPa;Q_kN"), "Diagnostica nel CSV");
        var clayStress = Data(true, 450, Layer("Coesivo", 10));
        clayStress["generali"]!["presenza_falda"] = true; clayStress["generali"]!["profondita_falda"] = 1.13;
        var clayWithWeights = Calc(clayStress);
        Near(clayWithWeights["sondaggi"]![0].Array("diagramma_terreno").Last().D("sigma_v_kpa"), 18 * 1.13 + 20 * 8.87, "Tensioni nel coesivo omogeneo");
        Check(clayWithWeights["sondaggi"]![0].Array("diagramma_terreno").Any(p => p.D("z") == 1.13), "Falda presente nella diagnostica coesiva");
        clayStress["stratigrafie"]![0]![0]!["peso_specifico"] = null;
        var clayWithoutWeights = Calc(clayStress);
        Near(clayWithoutWeights.D("capacita_kn"), clayWithWeights.D("capacita_kn"), "Diagnostica non cambia la capacità coesiva");
        Check(!clayWithoutWeights["sondaggi"]![0].B("tensioni_disponibili") && clayWithoutWeights["sondaggi"]![0].Array("diagramma_terreno").All(p => p!["sigma_eff_kpa"] is null), "Tensioni mancanti non sostituite con zero");
        var widePile = Data(true, 10000, Layer("Granulare", 6)); widePile["generali"]!["diametro"] = 2;
        var wide = Calc(widePile)["sondaggi"]![0]!;
        Near(wide.Array("diagramma_terreno").Last().D("p_lim_kn_m"), 2 * wide.Array("diagramma_terreno").Last().D("q_lim_kpa"), "Conversione pressione / reazione D=2");

        // Independent mixed long benchmark: z_f=3.
        // Granular [0,2] p=162z; cohesive [2,10] p=450 (Cu=50).
        // Q(3)=324+450=774; S(3)=432+1125=1557 -> My=778.5.
        var longMixed = Data(true, 778.5, Layer("Granulare", 2), Layer("Coesivo", 8));
        var longResult = Calc(longMixed);
        Near(longResult.D("capacita_kn"), 774, "Misto lungo esatto");
        Near(longResult["sondaggi"]![0].D("quota_taglio_nullo_m"), 3, "Quota cerniera mista esatta");
        Check(longResult.S("meccanismo") == "Lungo", "Misto lungo");
        longMixed["generali"]!["vincolo"] = "Libera"; longMixed["generali"]!["eccentricita"] = 1;
        longMixed["generali"]!["momento_resistente"] = 1557 + 774;
        Near(Calc(longMixed).D("capacita_kn"), 774, "Misto lungo eccentrico");

        // Finite distributed closure in sand intentionally differs from Broms' tip force.
        // Free short, p=kz: b=L/2^(1/3), H=k*b²-k*L²/2.
        double lengthSand = 2, k = 162;
        var sand = Data(false, 10000, Layer("Granulare", lengthSand));
        Near(Calc(sand).D("capacita_kn"), k * lengthSand * lengthSand * (Math.Pow(2, -2d / 3) - .5), "Granulare corto distribuito analitico");
        sand = Data(true, 216, Layer("Granulare", 10));
        Near(Calc(sand).D("capacita_kn"), 324, "Granulare lungo recupera Broms");

        var mechanisms = new HashSet<string>();
        foreach (bool fix in new[] { false, true })
        foreach (double my in new[] { 20d, 150d, 1000d, 3000d, 1e6 })
        foreach (bool water in new[] { false, true })
        {
            var data = Data(fix, my, Layer("Granulare", .4), Layer("Coesivo", .6, 25),
                Layer("Coesivo", 1.3, 40), Layer("Granulare", 2.1, phi: 35, gamma: 21), Layer("Coesivo", 3.6, 80));
            data["generali"]!["presenza_falda"] = water; data["generali"]!["profondita_falda"] = .7;
            if (!fix) data["generali"]!["eccentricita"] = .8;
            var result = Calc(data); mechanisms.Add(result.S("meccanismo"));
            // Split every layer, including layers inside the surface gap and across groundwater.
            var split = new JsonArray();
            foreach (var layer in data["stratigrafie"]![0]!.AsArray())
                foreach (double fraction in new[] { .2, .3, .5 })
                { var part = layer!.DeepClone(); part["spessore"] = layer.D("spessore") * fraction; split.Add(part); }
            data["stratigrafie"] = new JsonArray(split);
            Near(Calc(data).D("capacita_kn"), result.D("capacita_kn"), "Interfacce fittizie nei profili misti");
            data["generali"]!["passo"] = .037; data["generali"]!["tolleranza"] = 1e-5;
            Near(Calc(data).D("capacita_kn"), result.D("capacita_kn"), "Indipendenza campionamento");
        }
        Check(mechanisms.SetEquals(new[] { "Corto", "Intermedio", "Lungo" }), "Copertura dei tre meccanismi");

        // Thin cohesive surface layer: interfaces remain at their original depths.
        var thin = Data(true, 1e6, Layer("Coesivo", .2), Layer("Granulare", 3.8));
        var thinResult = Calc(thin); var limits = thinResult["sondaggi"]![0]!.Array("diagramma_limite");
        Near(limits[1].D("da_m"), .2, "Interfaccia non traslata da 1.5D");
        Near(limits[1].D("sigma_eff_iniziale_kpa"), 3.6, "Peso dello strato coesivo superficiale");
        // Clipping a final layer at the toe must not change p or the capacity.
        thin["stratigrafie"]![0]![1]!["spessore"] = 100;
        Near(Calc(thin).D("capacita_kn"), thinResult.D("capacita_kn"), "Troncamento al piede");
        var helperInput = (JsonObject)thin.DeepClone(); helperInput["generali"]!.AsObject().Remove("metodo_calcolo");
        string original = helperInput.ToJsonString(); var helperResult = PaloOrizzontale.CalculateStratified(helperInput);
        Check(helperResult.S("errore") == "" && helperInput.ToJsonString() == original, "API diretta immutabile");
        Check(PaloOrizzontale.Calculate(helperInput).S("errore").Contains("Sequenze miste"), "Broms storico non cambia implicitamente modello");
        var legacyInput = (JsonObject)helperInput.DeepClone(); legacyInput["generali"]!["metodo_calcolo"] = "Stratificato (PileChecker)";
        string legacyOriginal = legacyInput.ToJsonString(); var migrated = PaloOrizzontale.Calculate(legacyInput);
        Near(migrated.D("capacita_kn"), helperResult.D("capacita_kn"), "Capacità conservata nei file con vecchia denominazione");
        Check(legacyInput.ToJsonString() == legacyOriginal && !migrated.ToJsonString().Contains("PileChecker"), "Migrazione compatibile senza modificare l'input");
        helperInput["generali"]!["metodo_calcolo"] = "inventato";
        Check(PaloOrizzontale.Calculate(helperInput).S("errore") != "", "Metodo sconosciuto rifiutato");
        thin["generali"]!["presenza_falda"] = true; thin["stratigrafie"]![0]![0]!["peso_specifico_saturo"] = 8;
        Check(PaloOrizzontale.Calculate(thin).S("errore") != "", "Peso efficace negativo nel coesivo sovrastante rifiutato");

        using var stream = new MemoryStream(ReportOrizzontale.Create("Stratificato", longResult));
        using var zip = new ZipArchive(stream); using var xmlStream = zip.GetEntry("word/document.xml")!.Open();
        string xml = XDocument.Load(xmlStream).ToString();
        Check(xml.Contains("Ammesse sequenze miste") && xml.Contains("Chiusura distribuita"), "Relazione descrive il modello adottato");
        Check(!xml.Contains("sequenza mista.") && xml.Contains("ipotesi del modello") && xml.Contains("validazione sperimentale"), "Relazione distingue ipotesi e validazione");
        Check(!xml.Contains("PileChecker"), "Relazione con denominazione aggiornata");
        Console.WriteLine($"Stratificato: {count} controlli superati.");
        return count;
    }
}
