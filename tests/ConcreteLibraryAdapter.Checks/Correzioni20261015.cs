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

        // ------------------------------------------------------------ TAG-2: coefficienti unitari del foglio c.a. del palo
        // Senza coefficienti unitari γc e fyd della norma effettiva coincidono bit per bit con gamma_c e fyk/γs dell'input usati prima:
        // taglio e torsione restano identici per tutte le norme ordinarie, anche con fyk e γ non arrotondati.
        foreach (string norm in ConcreteStandards.OrdinaryNames)
            foreach (var (fyk, gammaS, gammaC) in new[] { ("450", "1.15", "1.5"), ("500", "1.15", "1.5"), ("413.7", "1.13", "1.45") })
            {
                var (input, settings, _) = Module(norm);
                input["fyk_mpa"] = fyk; input["gamma_s"] = gammaS; input["gamma_c"] = gammaC;
                if (input.ContainsKey("steel_fu_mpa") && input.D("steel_fu_mpa") < double.Parse(fyk, System.Globalization.CultureInfo.InvariantCulture)) input["steel_fu_mpa"] = fyk;
                bool same = BitConverter.DoubleToInt64Bits(ConcreteMaterials.DesignValues(input, settings).Fyd) == BitConverter.DoubleToInt64Bits(new SezioneCA(input).Fyd)
                    && BitConverter.DoubleToInt64Bits(ConcreteStandards.Effective(input, settings).GammaC) == BitConverter.DoubleToInt64Bits(input.Required("gamma_c"));
                check(same, $"TAG-2 {norm} fyk {fyk} γs {gammaS} γc {gammaC}: fyd o γc della norma effettiva diversi da quelli dell'input"); count++;
            }
        // Sezione predefinita 600×800 (C35/45, B450C, Ø10/200, copriferro 70), NTC 2018, coefficienti della norma come li assegna
        // ElasticHorizontalPile.CreateConcreteSheet (ConcreteStandards.PileWorkspace). N = −500 kN, Vy = 300 kN.
        foreach (bool unit in new[] { false, true })
        {
            foreach (string model in new[] { "Senza staffe", "Con staffe" })
            {
                var (input, settings, options) = Module("NTC 2018");
                input["coefficienti_unitari"] = unit;
                if (unit)
                {
                    var custom = ConcreteStandards.PileWorkspace(input);
                    settings["coefficienti"] = custom["coefficienti"]!.DeepClone(); settings["normativa_custom"] = custom["normativa_custom"]!.DeepClone();
                }
                options["modello"] = model; options["cot_torsione"] = "1.5"; options["as_torsione"] = "1000";
                string tag = $"TAG-2 {(unit ? "unitari" : "ordinari")} {model}";
                check(ConcreteStandards.Effective(input, settings).GammaC == (unit ? 1 : 1.5), tag + ": γc effettivo"); count++;
                // Dati derivati dalla sezione (premesse dell'atteso): bw = 600, d = 800 − 70 − 10 − 24/2 = 708, Asl = 4Ø20.
                check(options.D("bw_y") == 600 && options.D("d_y") == 708 && Math.Abs(options.D("asl_y") - 4 * Math.PI * 100) < 1e-9, tag + ": bw, d, Asl derivati"); count++;
                var r = ConcreteShearAnalysis.Calculate(input, settings, options, Row("-500", "0", "0", "0", "300", model == "Con staffe" ? "40" : "0"));
                if (model == "Senza staffe")
                {
                    // NTC 2018 [4.1.23]: VRd = [0,18 k (100 ρl fck)^(1/3)/γc + 0,15 σcp] bw d, con γc = 1 (unitari) o 1,5 (correzioni_20261015.py).
                    double expected = unit ? 321.607973804162 : 236.530315869441;
                    Near(r.Shear[1].VRd, expected, 1e-9, tag + ": VRd senza staffe");
                    Near(r.Shear[1].Ratio!.Value, 300 / expected, 1e-9, tag + ": η");
                    check((r.Shear[1].Ratio <= 1) == unit, tag + ": esito"); count++;
                }
                else
                {
                    // Profilo resistente: asse delle barre a 70 + 10 + 24/2 = 92 mm dal bordo, Ak = 416 × 616 = 256 256 mm², uk = 2064 mm.
                    var t = r.Torsion!;
                    check(Math.Abs(t.Geometry.Area - 256256) < 1e-6 && Math.Abs(t.Geometry.Perimeter - 2064) < 1e-9, tag + ": profilo resistente"); count++;
                    // NTC 2018 [4.1.41]-[4.1.42]: TRsd = 2 Ak (As/s) fyd cot θ, TRld = 2 Ak (ΣAl/uk) fyd / cot θ, con fyd = fyk = 450 MPa
                    // (unitari) o 450/1,15 (correzioni_20261015.py); lo stesso fyd del VRsd del taglio della riga.
                    Near(t.TRsd, unit ? 135.852519437714 : 118.132625598012, 1e-9, tag + ": TRsd");
                    Near(t.TRld, unit ? 74.493023255814 : 64.776541961577, 1e-9, tag + ": TRld");
                }
            }
        }
        return count;
    }
}
