using System.Text.Json.Nodes;
using X.Core;

internal static class MicropileChecks
{
    internal static void Run(JsonArray cases)
    {
        int passed = 0, fixtures = 0;
        void Assert(bool value, string message) { if (!value) throw new Exception(message); passed++; }
        void Compare(JsonNode? expected, JsonNode? actual, string path)
        {
            if (expected is JsonObject obj)
            {
                Assert(actual is JsonObject, path);
                foreach (var (key, value) in obj) Compare(value, actual![key], path + "." + key);
            }
            else if (expected is JsonArray list)
            {
                Assert(actual is JsonArray a && a.Count == list.Count, path);
                for (int i = 0; i < list.Count; i++) Compare(list[i], actual![i], path + "[" + i + "]");
            }
            else if (J.Number(expected) is double e && J.Number(actual) is double a)
                Assert(Math.Abs(e - a) <= 1e-8 + 1e-10 * Math.Abs(e), path + ": risultato cambiato");
            else Assert(JsonNode.DeepEquals(expected, actual), path + ": risultato cambiato");
        }
        foreach (var test in cases.Where(c => c.S("tipo") == "micropalo"))
        {
            var result = Calcolo.Calcola(test!["input"]!, true); fixtures++;
            if (test.B("atteso_errore")) { Assert(result.S("errore") != "", "Errore atteso"); continue; }
            foreach (string key in new[] { "curve", "azioni", "profondita_massima", "copertura_completa", "peso_sezione" })
                Compare(test["atteso"]![key], result[key], test.S("nome") + "." + key);
            var summary = Tabelle.CapacitaPalo(result, true);
            Assert(summary.Count == 1 && summary[0].Righe.Count == 3, "Riepilogo micropalo");
            var final = result.Array("dettagli")[^1]!;
            var tension = result["curve"]!["trazione"]!;
            string branch = tension.Array("media")[^1]![1]!.GetValue<double>() <= tension.Array("minima")[^1]![1]!.GetValue<double>() ? "Media" : "Minimo";
            Compare(tension.Array("progetto")[^1]![1], final["componenti"]!["trazione"]![branch]!["d"]![0], "Componente laterale trazione");
            Assert(Tabelle.Crea(result, true).Any(t => t.Titolo.StartsWith("trazione —") && t.Colonne.Contains("L d [kN]")), "Dettaglio laterale trazione mancante");
        }
        Assert(fixtures > 0, "Nessun caso micropalo");
        // PV-4 (analisi dei bug del 15/10/2026): α richiesto solo negli strati attivi che intersecano la zona iniettata [sb, L]. Micropalo IGU,
        // L 12, sb 3, CHS 88,9 × 8, p_i 1,5 MPa: lo strato 0–3 m (non iniettato) e uno strato sotto la punta con α = 0 non contribuiscono, quindi
        // il calcolo coincide con quello con α = 1,15 negli stessi strati; nella zona iniettata α = 0 resta un rifiuto italiano con strato e
        // stratigrafia.
        {
            JsonObject Micro(params (string Thickness, string Alpha)[] layers)
            {
                var sheet = CalculationDefaults.Vertical(true); var g = sheet["generali"]!.AsObject();
                g["lunghezza"] = "12"; g["profilo_chs"] = "CHS 88.9 × 8"; g["pressione_iniezione"] = "1.5"; g["tipo_iniezione"] = "IGU"; g["inizio_aderenza"] = "3";
                g["azione_compressione"] = "300"; g["azione_trazione"] = "200";
                var rows = new JsonArray();
                foreach (var (h, alpha) in layers) rows.Add(new JsonObject { ["spessore"] = h, ["terreno"] = "Sabbia media", ["alpha"] = alpha, ["laterale_attiva"] = true });
                sheet["stratigrafie"] = new JsonArray(rows); return sheet;
            }
            var reference = Calcolo.Calcola(Micro(("3", "1.15"), ("12", "1.15")), true);
            Assert(reference.S("errore") == "", "PV-4: micropalo di riferimento non calcolato: " + reference.S("errore"));
            foreach (var (label, sheet, same) in new[] { ("strato non iniettato con α = 0", Micro(("3", "0"), ("12", "1.15")), Micro(("3", "1.15"), ("12", "1.15"))),
                ("strato sotto la punta con α = 0", Micro(("3", "1.15"), ("9", "1.15"), ("5", "0")), Micro(("3", "1.15"), ("9", "1.15"), ("5", "1.15"))),
                ("strato non iniettato con α vuoto", Micro(("3", ""), ("12", "1.15")), Micro(("3", "1.15"), ("12", "1.15"))) })
            {
                var actual = Calcolo.Calcola(sheet, true); var expected = Calcolo.Calcola(same, true);
                Assert(actual.S("errore") == "", "PV-4: " + label + " rifiutato: " + actual.S("errore"));
                Assert(JsonNode.DeepEquals(actual["curve"], expected["curve"]) && JsonNode.DeepEquals(actual["dettagli"], expected["dettagli"]), "PV-4: " + label + " cambia il calcolo");
            }
            var injected = Calcolo.Calcola(Micro(("3", "1.15"), ("12", "0")), true);
            Assert(injected.S("errore").StartsWith("Stratigrafia 1: Strato 2: alpha: inserire un numero finito maggiore di 0"), "PV-4: α nullo nella zona iniettata senza messaggio italiano: " + injected.S("errore"));
        }
        var defaults=Archivio.NuovoFoglio("geo_micropalo_verticale")["generali"]!;
        Assert(defaults.D("diametro")==.24&&defaults.S("tipo_iniezione")=="IGU"&&defaults.D("peso_specifico_palo")==25, "Default micropalo");
        var linear=cases.First(c=>c.S("nome")=="micropalo_IRS_45_Feld")!["input"]!.DeepClone().AsObject();
        var oldResult=Calcolo.Calcola(linear,true);
        var general=linear["generali"]!.AsObject();
        general["peso_lineare_micropalo"]=999;
        Compare(oldResult["azioni"],Calcolo.Calcola(linear,true)["azioni"],"Peso manuale obsoleto ignorato");
        foreach(double angle in new[]{0.0,45.0})
        {
            general["inclinazione"]=angle;
            double q=Chs.Peso(general.S("profilo_chs"),general.D("diametro"),general.D("peso_specifico_palo",25)).D("q_totale");
            var result=Calcolo.Calcola(linear,true);
            Assert(result.S("errore")=="", "Calcolo peso lineare");
            foreach(var detail in result.Array("dettagli"))
                Assert(Math.Abs(detail.D("peso")-q*detail.D("z")*Math.Cos(angle*Math.PI/180))<1e-10,"Peso lineare proiettato sull’asse");
        }
        foreach(string invalid in new[]{"","abc","-1","0"})
        {
            general["peso_lineare_micropalo"]=invalid;
            Assert(Calcolo.Calcola(linear,true).S("errore")=="","Peso manuale obsoleto non ignorato");
        }
        var initial = Archivio.NuovoFoglio("geo_micropalo_verticale").Array("stratigrafie")[0]!.AsArray();
        Assert(initial.Count == 1 && initial[0].D("spessore") == 0 && initial[0].D("alpha") == 0 && initial[0]!["nc"] is null, "Primo strato micropalo");
        Console.WriteLine($"Micropalo: {fixtures} casi di riferimento, {passed} controlli superati; curve e azioni invariate.");
    }
}
