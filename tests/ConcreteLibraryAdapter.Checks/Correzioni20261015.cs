using System.Text.Json.Nodes;
using Anthea.Calculations;

/// <summary>
/// Correzioni dell'analisi dei bug del 15/10/2026 (docs/refactoring/analisi-bug-20261015.md) sul percorso del modulo della sezione c.a.
/// (ConcreteShearAnalysis), con attesi indipendenti: script Python attesi/correzioni_20261015.py, riportati accanto a
/// ogni valore con la formula di norma da cui vengono.
/// </summary>
internal static class Correzioni20261015
{
    static (JsonObject Input, JsonObject Settings, JsonObject Options) Module(string norm)
    {
        var data = SezioneCA.DefaultData(); var input = data["input"]!.AsObject(); var settings = SectionWorkspace.Prepare(data);
        settings["normativa"] = norm; settings["coefficienti"] = ConcreteStandards.Defaults(norm);
        foreach (var (k, v) in ConcreteCalculationSettings.CommonCoefficients) input[k] = settings["coefficienti"]![v]!.DeepClone();
        ConcreteCalculationSettings.Prepare(input, settings);
        var options = settings["taglio"]!.AsObject();
        ConcreteCalculationSettings.UpdateAutomaticShear(input, options);
        options["ancoraggio"] = "Confermato"; options["chiusura_torsione"] = "Confermato"; options["as_torsione"] = "1000";
        return (input, settings, options);
    }

    static JsonObject Row(string n, string mx, string my, string vx, string vy, string t)
        => J.Obj(("id", "c"), ("nome", "C"), ("N", n), ("Mx", mx), ("My", my), ("Vx", vx), ("Vy", vy), ("T", t));

    public static int Run(Action<bool, string> check)
    {
        int count = 0;
        void Near(double value, double expected, double relative, string message)
        {
            check(double.IsFinite(value) && Math.Abs(value - expected) <= relative * Math.Max(1, Math.Abs(expected)), $"{message}: {value:G17} invece di {expected:G17}");
            count++;
        }

        // ------------------------------------------------------------ TAG-1: Δe di Model Code 2010 con segno
        // Sezione con parametri manuali bw = 300, d = 460 (z = 0,9 d = 414), Asl = 4Ø20 = 1256,64 mm², C30, γc = 1,5, dg = 20, Es = 200 000;
        // N = −500 kN, Mx = 100 kNm, Vy = 100 kN, senza staffe. Atteso dalla statica delle due corde (attesi/correzioni_20261015.py): con la risultante a 50 mm
        // dalla metà di z verso il lembo compresso la corda tesa riceve M/z + V + N(1/2 − 50/z), cioè Δe = −50 (positiva verso il lembo teso).
        {
            var (input, settings, options) = Module("Model Code 2010");
            input["fck_mpa"] = "30"; input["gamma_c"] = "1.5"; input["steel_modulus_mpa"] = "200000";
            settings["dettagli_costruttivi"]!["aggregato"] = "20";
            options["modello"] = "Senza staffe"; options["parametri"] = "Manuali";
            options["bw_x"] = "500"; options["d_x"] = "260"; options["asl_x"] = "1256.6370614359173";
            options["bw_y"] = "300"; options["d_y"] = "460"; options["asl_y"] = "1256.6370614359173";
            foreach (var (eccentricity, expected) in new[] { ("-50", 118.611313178149), ("50", 157.722694982148), ("0", 135.399157667030) })
            {
                // correzioni_20261015.py: VRd,c = kv √fck z bw / γc, kv = 0,4/(1+1500 εx) · 1300/(1000 + kdg z), εx = [|M|/z + |V| + N(1/2 + Δe/z)]/(2 Es Asl)
                options["eccentricita_mc_y"] = eccentricity;
                var r = ConcreteShearAnalysis.Calculate(input, settings, options, Row("-500", "100", "0", "0", "100", "0"));
                Near(r.Shear[1].VRd, expected, 1e-9, "TAG-1: VRd,c MC2010 con Δe = " + eccentricity + " mm");
            }
        }
        return count;
    }
}
