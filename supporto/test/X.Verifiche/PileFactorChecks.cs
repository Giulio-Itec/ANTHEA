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
        Console.WriteLine($"Coefficienti dei pali: {passed} controlli superati.");
    }
}
