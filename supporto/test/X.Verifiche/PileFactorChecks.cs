using System.Text.Json.Nodes;
using X.Core;

/// <summary>γb of NTC 2018 Tab. 6.4.II (R3) by pile technology, read from Model through Calcolo.SicurezzaBaseNormativa (D7-d).</summary>
internal static class PileFactorChecks
{
    internal static void Run()
    {
        int passed = 0;
        void Assert(bool ok, string message) { if (!ok) throw new Exception(message); passed++; }
        double Base(string type, string? driven = null, bool micro = false)
        {
            var g = Archivio.NuovoFoglio(micro ? "geo_micropalo_verticale" : "geo_palo_verticale")["generali"]!.AsObject();
            g["tipo_palo"] = type; if (driven is not null) g["sottotipo_palo_battuto"] = driven;
            return Calcolo.SicurezzaBaseNormativa(g);
        }
        Assert(Base("Trivellato") == 1.35, "γb trivellato 1,35");
        Assert(Base("Elica continua") == 1.30, "γb elica continua 1,30");
        foreach (string driven in new[] { "Profilato d'acciaio", "Tubo d'acciaio chiuso", "Calcestruzzo prefabbricato", "Calcestruzzo gettato in opera" })
            Assert(Base("Battuto", driven) == 1.15, "γb battuto 1,15: " + driven);
        Assert(Base("Trivellato", micro: true) == 1.35, "γb micropalo come trivellato");
        var fresh = Archivio.NuovoFoglio("geo_palo_verticale")["generali"]!;
        Assert(fresh.D("sicurezza_base") == Calcolo.SicurezzaBaseNormativa(fresh), "Foglio nuovo: γb della tecnologia predefinita");

        // The sheet keeps its γb (archives included) and the calculation declares a value different from the technology's.
        var data = Archivio.NuovoFoglio("geo_palo_verticale"); var g = data["generali"]!;
        g["lunghezza"] = "20"; g["azione_compressione"] = "1000"; g["azione_trazione"] = "200";
        var layer = data.Array("stratigrafie")[0]!.AsArray()[0]!.AsObject();
        layer["spessore"] = "25"; layer["tipologia"] = "Granulare"; layer["addensamento"] = "Denso"; layer["angolo_attrito"] = "32"; layer["peso_specifico"] = "19";
        bool Declared(JsonObject result) => result.Array("avvisi").Any(a => a!.GetValue<string>().StartsWith("γb = "));
        var bored = Calcolo.Calcola(data);
        Assert(bored.S("errore") == "" && !Declared(bored), "Trivellato con 1,35: nessun avviso");
        g["tipo_palo"] = "Battuto"; var stored = Calcolo.Calcola(data);
        Assert(stored.S("errore") == "" && Declared(stored), "Battuto con 1,35 memorizzato: avviso");
        Assert(stored.Array("avvisi").Any(a => a!.GetValue<string>() == "γb = 1,35 diverso dal valore della NTC 2018 Tab. 6.4.II per la tecnologia del palo (1,15): il calcolo usa il valore del foglio."), "Testo dell'avviso γb");
        g["sicurezza_base"] = "1.15"; var normative = Calcolo.Calcola(data);
        Assert(normative.S("errore") == "" && !Declared(normative), "Battuto con 1,15: nessun avviso");
        Assert(g.S("sicurezza_base") == "1.15" && stored["curve"]!.ToJsonString() != normative["curve"]!.ToJsonString(), "Il calcolo usa il γb del foglio");
        Console.WriteLine($"Coefficienti dei pali: {passed} controlli superati.");
    }
}
