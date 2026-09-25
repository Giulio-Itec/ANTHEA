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
