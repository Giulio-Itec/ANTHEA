using System.Text.Json.Nodes;
using X.Core;

/// <summary>
/// Testa del palo e del micropalo verticale sotto il piano campagna (richiesta dell'utente del 19/10/2026, 1.14.3): valori indipendenti calcolati a
/// mano dal peso del ricoprimento, equivalenze con la testa al piano campagna e dati non validi.
/// </summary>
internal static class PileHeadDepthChecks
{
    internal static void Run()
    {
        int passed = 0;
        void Assert(bool ok, string message) { if (!ok) throw new Exception(message); passed++; }
        void Near(double actual, double expected, string message) => Assert(Math.Abs(actual - expected) <= 1e-6 * Math.Max(1, Math.Abs(expected)), $"{message}: {actual} invece di {expected}");

        // Palo trivellato D 0,6 m, L 10 m in uno strato granulare sciolto di 20 m, γ 18 kN/m³, φ′ 30°, senza falda.
        JsonObject Pile(string head, double thickness = 20)
        {
            var data = Archivio.NuovoFoglio("geo_palo_verticale"); var g = data["generali"]!;
            g["diametro"] = "0.6"; g["lunghezza"] = "10"; if (head != "") g["profondita_testa"] = head; else g.AsObject().Remove("profondita_testa");
            var layer = data.Array("stratigrafie")[0]!.AsArray()[0]!.AsObject();
            layer["spessore"] = thickness.ToString(System.Globalization.CultureInfo.InvariantCulture); layer["tipologia"] = "Granulare"; layer["addensamento"] = "Sciolto";
            layer["angolo_attrito"] = "30"; layer["peso_specifico"] = "18";
            return data;
        }
        JsonNode Tip(JsonObject result) => result.Array("dettagli")[^1]!.Array("sondaggi")[0]!;

        // d = 0 e chiave assente: il calcolo di prima, identico.
        var plain = Calcolo.Calcola(Pile("")); var zero = Calcolo.Calcola(Pile("0"));
        Assert(plain.S("errore") == "", "Caso di riferimento non calcolabile: " + plain.S("errore"));
        Assert(JsonNode.DeepEquals(plain, zero), "d = 0 diverso dal foglio senza la chiave");
        Assert(JsonNode.DeepEquals(plain, Calcolo.Calcola(Pile(" "))), "d vuoto diverso dal foglio senza la chiave");
        Assert(Archivio.NuovoFoglio("geo_palo_verticale")["generali"]!["profondita_testa"] is null, "I fogli nuovi non scrivono d: l'editor mostra 0");
        Assert(zero["profondita_testa"] is null, "d = 0 non deve aggiungere chiavi al risultato");

        // d = 2 m: σ′v alla testa 18 · 2 = 36 kPa (esempio della richiesta), alla punta 18 · 12 = 216 kPa, media del fusto 18 · 7 = 126 kPa;
        // Nq della punta a (d + L)/D = 12/0,6 = 20; le quote dei risultati partono dalla testa (ultima z = L = 10 m).
        var head = Calcolo.Calcola(Pile("2"));
        Assert(head.S("errore") == "", "Testa a 2 m non calcolabile: " + head.S("errore"));
        Near(head.D("profondita_testa"), 2, "Profondità della testa nel risultato");
        Near(head.Array("dettagli")[^1]!.D("z"), 10, "Ultima quota dalla testa");
        Near(head.D("lunghezza_palo"), 10, "Lunghezza del palo");
        Near(Tip(head).D("sigma_punta"), 216, "σ′v alla punta");
        var shaft = Tip(head).Array("tratti").Single()!;
        Near(shaft.D("sigma_media"), 126, "σ′v media del fusto");
        Near(shaft.D("sigma_fondo"), 216, "σ′v al fondo del fusto");
        Near(Tip(head)["dettaglio_nq"].D("rapporto"), 20, "(d + L)/D di Nq");
        // Laterale drenata: π D L k μ σ′media con k e μ del tratto (stessi della testa al piano campagna, che li ha a σ′media 90 kPa).
        double k = shaft.D("k"), mu = shaft.D("mu");
        Near(shaft.D("laterale_d"), Math.PI * 0.6 * 10 * k * mu * 126, "Resistenza laterale drenata");
        var shaftZero = Tip(zero).Array("tratti").Single()!;
        Near(shaftZero.D("sigma_media"), 90, "σ′v media con la testa al piano campagna");
        Near(shaft.D("laterale_d") / shaftZero.D("laterale_d"), 126.0 / 90, "Laterale proporzionale a σ′media");
        Assert(head.Array("avvisi").Any(w => w!.ToString().StartsWith("Testa del palo a d = 2 m sotto il piano campagna")), "Avviso della testa interrata");

        // Falda dentro il ricoprimento, a 1 m: σ′v alla testa 18 · 1 + (20 − γw) · 1, alla punta 18 · 1 + (20 − γw) · 11.
        var wet = Pile("2"); wet["generali"]!["presenza_falda"] = true; wet["generali"]!["profondita_falda"] = "1";
        wet.Array("stratigrafie")[0]!.AsArray()[0]!["peso_specifico_saturo"] = "20";
        var wetResult = Calcolo.Calcola(wet);
        Assert(wetResult.S("errore") == "", "Falda nel ricoprimento non calcolabile: " + wetResult.S("errore"));
        double gw = 9.81;
        Near(Tip(wetResult).D("sigma_punta"), 18 + (20 - gw) * 11, "σ′v alla punta con la falda nel ricoprimento");
        Near(Tip(wetResult).Array("tratti").Single()!.D("sigma_media"), 18 + (20 - gw) * 6, "σ′v media del fusto con la falda nel ricoprimento");

        // Dati non validi: d negativo, stratigrafia che non arriva sotto la testa.
        Assert(Calcolo.Calcola(Pile("-1")).S("errore").StartsWith("Profondità della testa d"), "d negativo non rifiutato");
        Assert(Calcolo.Calcola(Pile("abc")).S("errore").StartsWith("Profondità della testa d"), "d non numerico non rifiutato");
        Assert(Calcolo.Calcola(Pile("2", 1.5)).S("errore").Contains("non arriva sotto la testa"), "Stratigrafia sopra la testa non rifiutata");

        // Micropalo: Bustamante–Doix non usa σ′v, quindi la testa a 3 m sotto uno strato di 3 m dà lo stesso micropalo che la testa al piano
        // campagna nel solo strato inferiore (stessa aderenza, stesse quote dall'asse).
        JsonObject Micro(string head, params (string Soil, string Thickness, string Alpha)[] layers)
        {
            var data = Archivio.NuovoFoglio("geo_micropalo_verticale"); var g = data["generali"]!;
            g["lunghezza"] = "12"; g["pressione_iniezione"] = "3"; g["profilo_chs"] = "CHS 114.3 × 6.3"; g["profondita_testa"] = head;
            var rows = new JsonArray();
            foreach (var (soil, thickness, alpha) in layers) { var r = Archivio.NuovoStratoMicropalo(); r["terreno"] = soil; r["spessore"] = thickness; r["alpha"] = alpha; rows.Add(r); }
            data["stratigrafie"] = new JsonArray(rows);
            return data;
        }
        var buried = Calcolo.Calcola(Micro("3", ("Sabbia media", "3", "1.4"), ("Ghiaia", "20", "1.8")), true);
        var surface = Calcolo.Calcola(Micro("0", ("Ghiaia", "20", "1.8")), true);
        Assert(buried.S("errore") == "" && surface.S("errore") == "", "Micropali di riferimento non calcolabili: " + buried.S("errore") + surface.S("errore"));
        Assert(JsonNode.DeepEquals(buried["curve"], surface["curve"]), "Micropalo con la testa sotto lo strato superiore diverso da quello nel solo strato inferiore");
        Near(buried.D("profondita_testa"), 3, "Profondità della testa del micropalo");
        Assert(Calcolo.Calcola(Micro("4", ("Ghiaia", "3", "1.8")), true).S("errore").Contains("non arriva sotto la testa"), "Micropalo: stratigrafia sopra la testa non rifiutata");
        Console.WriteLine($"Testa interrata dei pali verticali: {passed} controlli superati.");
    }
}
