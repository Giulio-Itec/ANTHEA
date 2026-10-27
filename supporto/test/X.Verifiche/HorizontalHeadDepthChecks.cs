using System.Text.Json.Nodes;
using Anthea.Application.Presentation;
using X.Core;

/// <summary>
/// Testa del palo orizzontale sotto il piano campagna (richiesta dell'utente del 19/10/2026, 1.14.3): capacità di Broms calcolate a mano con il
/// diagramma limite che parte dalla testa, equivalenze, dati non validi, diagrammi; risposta elastica con gli strati sopra la testa esclusi e
/// kh = nh (d + x)/D.
/// </summary>
internal static class HorizontalHeadDepthChecks
{
    internal static void Run()
    {
        int passed = 0;
        void Assert(bool ok, string message) { if (!ok) throw new Exception(message); passed++; }
        void Near(double actual, double expected, string message, double tolerance = 1e-9) =>
            Assert(Math.Abs(actual - expected) <= tolerance * Math.Max(1, Math.Abs(expected)), $"{message}: {actual} invece di {expected}");
        string Error(Func<JsonObject> calculation) { try { return calculation().S("errore"); } catch (ArgumentException error) { return error.Message; } }

        // Palo D 0,6 m, L infissa, testa libera, e = 0, My manuale molto grande (governa il meccanismo corto, senza cerniere).
        JsonObject Pile(string head, double length, params JsonObject[] layers)
        {
            var data = PaloOrizzontale.Defaults(); var g = data["generali"]!;
            g["diametro"] = "0.6"; g["lunghezza"] = length.ToString(System.Globalization.CultureInfo.InvariantCulture);
            g["origine_momento"] = "Manuale"; g["momento_resistente"] = "100000"; g["provenienza_momento"] = "Valore di prova";
            if (head != "") g["profondita_testa"] = head;
            data["stratigrafie"] = new JsonArray(new JsonArray(layers));
            return data;
        }
        JsonObject Sand(double thickness = 20)
        {
            var layer = PaloOrizzontale.Layer(); layer["spessore"] = thickness.ToString(System.Globalization.CultureInfo.InvariantCulture); return layer;
        }
        JsonObject Clay(double thickness)
        {
            var layer = PaloOrizzontale.Layer(); layer["tipologia"] = "Coesivo"; layer["spessore"] = thickness.ToString(System.Globalization.CultureInfo.InvariantCulture); return layer;
        }
        JsonNode Survey(JsonObject result) => result.Array("sondaggi")[0]!;

        // Granulare γ 18 kN/m³, φ′ 30° (Kp = 3): p(z) = 3 Kp D γ (d + z) = a + b z dalla testa. Testa libera, e = 0, corto (Broms, chiusura
        // concentrata): Hu = ∫(L − s) p ds / L = (a L²/2 + b L³/6)/L. L = 5 m: d = 0 → a = 0, Hu = 405 kN; d = 2 m → a = 194,4 kN/m, Hu = 891 kN.
        // Il risultato riporta i dati («input»), che con d = "0" hanno la chiave: il confronto è sul resto.
        JsonObject Output(JsonObject result) { var copy = (JsonObject)result.DeepClone(); copy.Remove("input"); return copy; }
        var plain = PaloOrizzontale.Calculate(Pile("", 5, Sand()));
        Assert(plain.S("errore") == "", "Palo orizzontale di riferimento non calcolabile: " + plain.S("errore"));
        Assert(JsonNode.DeepEquals(Output(plain), Output(PaloOrizzontale.Calculate(Pile("0", 5, Sand())))), "d = 0 diverso dal foglio senza la chiave");
        Assert(JsonNode.DeepEquals(Output(plain), Output(PaloOrizzontale.Calculate(Pile(" ", 5, Sand())))), "d vuoto diverso dal foglio senza la chiave");
        Assert(plain["profondita_testa"] is null, "d = 0 non deve aggiungere chiavi al risultato");
        Near(Survey(plain).D("capacita_kn"), 405, "Hu con la testa al piano campagna");
        var buried = PaloOrizzontale.Calculate(Pile("2", 5, Sand()));
        Assert(buried.S("errore") == "", "Testa a 2 m non calcolabile: " + buried.S("errore"));
        Assert(Survey(buried).S("meccanismo") == "Corto", "Meccanismo della testa a 2 m: " + Survey(buried).S("meccanismo"));
        Near(Survey(buried).D("capacita_kn"), (194.4 * 25 / 2 + 97.2 * 125 / 6) / 5, "Hu con la testa a 2 m");
        Near(buried.D("profondita_testa"), 2, "Profondità della testa nel risultato");
        var first = Survey(buried).Array("diagramma_limite")[0]!;
        Near(first.D("p_iniziale_kn_m"), 194.4, "p_lim alla testa = 3 Kp D σ′v");
        Near(first.D("sigma_eff_iniziale_kpa"), 36, "σ′v alla testa = γ d");
        Near(Survey(buried).Array("diagrammi")[^1]!.D("z"), 5, "Ultima quota dei diagrammi dalla testa");
        Assert(buried.Array("avvisi").Any(w => w!.ToString().StartsWith("Testa del palo a d = 2 m sotto il piano campagna")), "Avviso della testa interrata");

        // Falda a 1 m, sopra la testa: σ′v alla testa 18 · 1 + (20 − γw) · 1, poi γ′ = 20 − γw lungo tutto il palo.
        var wet = Pile("2", 5, Sand()); wet["generali"]!["presenza_falda"] = true; wet["generali"]!["profondita_falda"] = "1";
        var wetResult = PaloOrizzontale.Calculate(wet);
        Assert(wetResult.S("errore") == "", "Falda sopra la testa non calcolabile: " + wetResult.S("errore"));
        double gw = 9.81, a = 9 * 0.6 * (18 + 20 - gw), b = 9 * 0.6 * (20 - gw);
        Near(Survey(wetResult).D("capacita_kn"), (a * 25 / 2 + b * 125 / 6) / 5, "Hu con la falda sopra la testa");

        // Ricoprimento coesivo con lo stesso γ sopra un palo tutto nel granulare: Broms omogeneo (non una sequenza mista) e stessa Hu.
        var covered = Pile("2", 5, Clay(2), Sand());
        Assert(PaloOrizzontale.ModelloAutomatico(covered) == "Omogeneo", "Modello con il ricoprimento coesivo: " + PaloOrizzontale.ModelloAutomatico(covered));
        var coveredResult = PaloOrizzontale.Calculate(covered);
        Assert(coveredResult.S("errore") == "" && coveredResult.S("modello_adottato") == "Omogeneo", "Ricoprimento coesivo: " + coveredResult.S("errore") + coveredResult.S("modello_adottato"));
        Near(Survey(coveredResult).D("capacita_kn"), Survey(buried).D("capacita_kn"), "Hu con il ricoprimento coesivo dello stesso peso");

        // Coesivo cu 50 kPa (p = 9 cu D = 270 kN/m), L = 6 m, d = 0,5 m: p = 0 fino a f0 = 1,5D − d = 0,4 m dalla testa. Corto a testa libera,
        // e = 0: H (f0 + f/2) = p g²/4 con f = H/p e g = L − f0 − f, cioè f² + (4 f0 + 2 L′) f − L′² = 0 con L′ = L − f0.
        var clay = PaloOrizzontale.Calculate(Pile("0.5", 6, Clay(20)));
        Assert(clay.S("errore") == "", "Palo nel coesivo non calcolabile: " + clay.S("errore"));
        double f0 = 0.4, rest = 6 - f0, f = (-(4 * f0 + 2 * rest) + Math.Sqrt(Math.Pow(4 * f0 + 2 * rest, 2) + 4 * rest * rest)) / 2;
        Near(Survey(clay).D("capacita_kn"), 270 * f, "Hu nel coesivo con la testa a 0,5 m", 1e-7);
        Near(Survey(clay).Array("diagramma_limite")[0]!.D("a_m"), f0, "Zona coesiva trascurata dalla testa");
        var clayDiagram = HorizontalPileDiagrams.Build(clay, 0, true);
        Assert(clayDiagram.ShearDepth is double shear && Math.Abs(shear - f0) < 1e-12, "Quota 1,5D − d nei diagrammi: " + clayDiagram.ShearDepth);
        var sandDiagram = HorizontalPileDiagrams.Build(PaloOrizzontale.Calculate(Pile("2", 5, Clay(2), Sand())), 0, false);
        Assert(sandDiagram.Layers.Count == 1 && sandDiagram.Layers[0].Index == 1 && sandDiagram.Layers[0].Top == 0 && sandDiagram.Layers[0].End == 5,
            "Diagrammi: solo lo strato del palo, dalla testa");
        Assert(sandDiagram.Basis.Contains("z dalla testa"), "Diagrammi: quote dalla testa");

        // Dati non validi.
        var free = Pile("2", 5, Sand()); free["generali"]!["tratto_libero"] = "1";
        Assert(Error(() => PaloOrizzontale.Calculate(free)).StartsWith("Testa sotto il piano campagna (d = 2 m) con tratto libero"), "Testa interrata con tratto libero non rifiutata");
        var restrained = Pile("2", 5, Sand()); restrained["generali"]!["vincolo"] = "Impedita"; restrained["generali"]!["eccentricita"] = "1";
        Assert(Error(() => PaloOrizzontale.Calculate(restrained)) == "Testa impedita: il modello richiede vincolo e forza alla testa (e = 0).", "Testa impedita con e: " + Error(() => PaloOrizzontale.Calculate(restrained)));
        Assert(Error(() => PaloOrizzontale.Calculate(Pile("2", 5, Sand(6)))) == "La stratigrafia deve coprire tutta la lunghezza infissa.", "Stratigrafia che non arriva alla punta d + L");
        Assert(Error(() => PaloOrizzontale.Calculate(Pile("-1", 5, Sand()))).StartsWith("Profondità della testa d"), "d negativo non rifiutato");

        // Risposta elastica. kh costante: con la testa a 3 m sotto uno strato di 3 m il palo è lo stesso del palo con la testa al piano campagna
        // nel solo strato inferiore; con nh assegnato kh = nh (d + x)/D (Reese–Matlock, z dal piano campagna).
        JsonObject Elastic(string head, params (string Law, double Thickness, double Value)[] layers)
        {
            var root = PaloOrizzontale.Defaults(); var rows = new JsonArray();
            foreach (var (law, thickness, value) in layers) { var layer = PaloOrizzontale.Layer(); layer["spessore"] = thickness; rows.Add(layer); }
            root["stratigrafie"] = new JsonArray(rows); root["generali"]!["diametro"] = "0.6"; root["generali"]!["lunghezza"] = "8";
            if (head != "") root["generali"]!["profondita_testa"] = head;
            ElasticHorizontalPile.PrepareShared(root);
            for (int i = 0; i < layers.Length; i++) { var soil = ElasticHorizontalPile.EnsureSoil(rows[i]!.AsObject()); soil["legge"] = layers[i].Law; soil["valore"] = layers[i].Value; }
            root["elastico"]!["passo"] = 0.25; root["vista_orizzontale"] = "elastico";
            return root;
        }
        string kh = ElasticHorizontalPile.Laws[0], nh = ElasticHorizontalPile.Laws[1];
        var surface = ElasticHorizontalPile.CalculateResponse(Elastic("", (kh, 20, 8000)));
        Assert(ElasticHorizontalPile.ResponseKey(Elastic("", (kh, 20, 8000))) == ElasticHorizontalPile.ResponseKey(Elastic("0", (kh, 20, 8000))) && surface["input"]!["testa"] is null,
            "Elastico: d = 0 non cambia la chiave della risposta");
        var below = ElasticHorizontalPile.CalculateResponse(Elastic("3", (kh, 3, 1000), (kh, 20, 8000)));
        Assert(below["input"].D("testa") == 3 && below.D("profondita_testa") == 3, "Elastico: profondità della testa nei dati e nel risultato");
        foreach (string key in new[] { "HeadDisplacement", "HeadRotation" })
            Near(below["risposta"].D(key), surface["risposta"].D(key), "Elastico con kh costante: " + key + " come il solo strato inferiore", 1e-12);
        var pointsBelow = below["risposta"]!.Array("Points"); var pointsSurface = surface["risposta"]!.Array("Points");
        Assert(pointsBelow.Count == pointsSurface.Count && pointsBelow.Zip(pointsSurface).All(p => p.First.D("Moment") == p.Second.D("Moment") && p.First.D("GroundDepth") == p.Second.D("GroundDepth") + 3),
            "Elastico con kh costante: stessi momenti, z = d + x");
        var linear = ElasticHorizontalPile.CalculateResponse(Elastic("2", (nh, 20, 6000)))["risposta"]!.Array("Points");
        Near(linear[0]!.D("Kh"), 6000 * 2 / 0.6, "Elastico: kh alla testa = nh d/D");
        Near(linear[^1]!.D("Kh"), 6000 * 10 / 0.6, "Elastico: kh alla punta = nh (d + L)/D");
        var elasticFree = Elastic("2", (kh, 20, 8000)); elasticFree["generali"]!["tratto_libero"] = 1;
        Assert(Error(() => ElasticHorizontalPile.CalculateResponse(elasticFree)).StartsWith("Testa sotto il piano campagna (d = 2 m) con tratto libero"), "Elastico: testa interrata con tratto libero non rifiutata");
        Console.WriteLine($"Testa interrata del palo orizzontale: {passed} controlli superati.");
    }
}
