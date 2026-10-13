using System.Text.Json.Nodes;
using Anthea.Application.Bridge;
using Anthea.Application.Presentation;
using Anthea.Calculations;

internal static class FinalUiRuleChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); checks++; }
        void Reject(string text, string expected)
        {
            try { WallSheetPresentation.ParseHistory(text); }
            catch (ArgumentException ex) { Check(ex.Message == expected, "Messaggio parser diverso: " + ex.Message); return; }
            throw new InvalidOperationException("Storia non valida accettata");
        }
        foreach (var standard in BridgeEditor.Standards)
        {
            var input = J.Obj(("normativa", standard), ("gamma_c", "1.47"), ("gamma_s", "1.16"));
            BridgeEditor.ApplyStandardDefaults(input);
            Check(input.S("gamma_m0") == (standard.StartsWith("NTC") ? "1.05" : "1.0") && input.S("gamma_m1") == "1.1"
                && input.S("alpha_cc") == (standard.StartsWith("NTC") ? "0.85" : "1.0"), "Cambio norma alterato");
            Check(input.S("gamma_c") == "1.47" && input.S("gamma_s") == "1.16", "Campo utente riscritto");
        }
        var parsed = WallSheetPresentation.ParseHistory("# accelerogramma\nt;g\n0;0\n0,5\t-0,15\n1;0.2\n");
        Check(parsed.Count == 3 && parsed[1].D("t") == .5 && parsed[1].D("a_g") == -.15 && parsed[2].D("a_g") == .2, "Segni/virgola/tab/header parser");
        Reject("0;0", "Inserire almeno due campioni.");
        Reject("0;0\nx;1", "Riga 2: usare due colonne separate da ; (secondi; g).");
        foreach (string row in new[] { "0;0", "-1;0", "1;NaN", "1;Infinity", "1;10.1", "1;-10.1" })
            Reject("0;0\n" + row, "Riga 2: tempi crescenti e accelerazioni finite richiesti.");
        Check(WallSheetPresentation.ParseHistory("0;-10\n1;10").Count == 2, "Estremi accelerazione rifiutati");
        string longHistory = string.Join('\n', Enumerable.Range(0, 200000).Select(i => i + ";0"));
        Check(WallSheetPresentation.ParseHistory(longHistory).Count == 200000, "Limite campioni cambiato");
        Reject(longHistory + "\n200000;0", "Massimo 200000 campioni.");
        foreach (double kh in new[] { 0.0, -0.0, .1, .4 })
            foreach (double kv in new[] { -.2, 0, .2 })
                Check(BitConverter.DoubleToInt64Bits(Math.Atan2(kh, 1 - kv) * 180 / Math.PI) ==
                    BitConverter.DoubleToInt64Bits(WallSheetPresentation.SeismicAngleDegrees(kh, kv)), "Angolo sismico alterato");
        var wall = RetainingWall.Defaults(); wall["geometry"]!["height"] = 3; wall["geometry"]!["slab"] = .45;
        wall["valley"]!["height_mode"] = "Assegnata"; wall["valley"]!["free_height"] = 2.45;
        JsonObject[] valley = [J.Obj(("thickness", 1))], mountain = [J.Obj(("thickness", 1.25)), J.Obj(("thickness", 2.20))];
        var summary = WallSheetPresentation.DescribeColumnDepths(wall, valley, mountain);
        Check(summary.Valid && summary.Text == "Valle: 1,00 m / 1,00 m richiesti · Monte: 3,45 m / 3,45 m richiesti\nHlib=2,45 m · terreno di valle Dv=1,00 m sopra il piano di posa", "Testo stratigrafia alterato");
        Check(mountain[1].D("__index") == 2 && mountain[1].S("__from") == "1,25" && mountain[1].S("__to") == "3,45", "Quote stratigrafia alterate");
        mountain[1]["thickness"] = 2.20 - 5e-10;
        Check(WallSheetPresentation.DescribeColumnDepths(wall, valley, mountain).Valid, "Tolleranza storica stratigrafia persa");
        mountain[1]["thickness"] = 2.19;
        Check(!WallSheetPresentation.DescribeColumnDepths(wall, valley, mountain).Valid, "Colonna corta accettata");
        return checks;
    }
}
