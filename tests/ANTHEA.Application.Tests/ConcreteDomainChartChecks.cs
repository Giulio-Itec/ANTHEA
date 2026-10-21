using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Calculations;

/// <summary>
/// Derivations of the domain panels of the RC section moved from the historic view in sub-step S3 of F5.10 (ConcreteDomainChart: options of
/// the panel, window «Scala…», plane through the selected action, triangles, detail of the selected row), with values written by hand. The
/// identity of the historic view with these functions is proved by ui/check-sezione-historic (traces frozen before the move).
/// </summary>
internal static class ConcreteDomainChartChecks
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        string Failure(Action action) { try { action(); return ""; } catch (ArgumentException error) { return error.Message; } }
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            var empty = new JsonObject();
            Check(ConcreteDomainChart.PointSize(empty, "dimensione_ed") == 5 && ConcreteDomainChart.PointSize(J.Obj(("dimensione_rd", 8)), "dimensione_rd") == 8,
                "Dimensione dei punti: 5 senza valore");
            Check(ConcreteDomainChart.Transparency(empty) == 35 && ConcreteDomainChart.Transparency(J.Obj(("trasparenza", 60))) == 60, "Trasparenza: 35 senza valore");
            Check(ConcreteDomainChart.AxisScale(empty, "scala_x") == 1 && ConcreteDomainChart.AxisScale(J.Obj(("scala_n", 2.5)), "scala_n") == 2.5, "Fattori degli assi: 1 senza valore");
            Check(ConcreteDomainChart.TriangleCount(null) == 0, "Triangoli senza mesh");
            Check(string.Join("|", ConcreteDomainChart.ScaleKeys(true)) == "scala_mx|scala_n|scala_my" && string.Join("|", ConcreteDomainChart.ScaleKeys(false)) == "scala_x|scala_y",
                "Fattori della finestra «Scala…» per 3D e 2D");
            var (values, zoom) = ConcreteDomainChart.ReadScale(J.Obj(("scala_x", "2"), ("scala_y", "0.5"), ("zoom", "150")), ConcreteDomainChart.ScaleKeys(false));
            Check(values["scala_x"] == 2 && values["scala_y"] == .5 && zoom == 1.5, "«Scala…»: fattori e zoom letti");
            string range = "Fattori ammessi 0,25–4; zoom 10–2000%.";
            Check(Failure(() => ConcreteDomainChart.ReadScale(J.Obj(("scala_x", "5"), ("scala_y", "1"), ("zoom", "100")), ConcreteDomainChart.ScaleKeys(false))) == range
                && Failure(() => ConcreteDomainChart.ReadScale(J.Obj(("scala_x", "1"), ("scala_y", "1"), ("zoom", "5")), ConcreteDomainChart.ScaleKeys(false))) == range
                && Failure(() => ConcreteDomainChart.ReadScale(J.Obj(("scala_x", "abc"), ("scala_y", "1"), ("zoom", "100")), ConcreteDomainChart.ScaleKeys(false))).Length > 0,
                "«Scala…»: fattore e zoom fuori intervallo, testo non numerico rifiutati");
            var action = new ConcreteResults.Action(-1000, 100, 100);
            Check(ConcreteDomainChart.PlaneThrough(J.Obj(("tipo", "Mx–My")), action) == ("N", "-1000")
                && ConcreteDomainChart.PlaneThrough(J.Obj(("tipo", "N–M")), action) == ("theta", "45"), "Sezione sull'azione: N per Mx–My, θ per N–M");
            var row = J.Obj(("nome", "Combo 1"), ("N", "-2500"), ("Mx", "500"), ("My", "250"));
            var options = J.Obj(("criterio", "N costante"), ("strategia", "Iterativo"), ("assi", "Locali"));
            Check(ConcreteDomainChart.Detail("Plastico", true, options, row, null, null, null)
                == "Plastico · N costante / Iterativo\n\nAZIONI · Locali\nCombo 1\n\nNEd = -2500,00 kN\nMx,Ed = 500,00 kNm\nMy,Ed = 250,00 kNm\n\nDa calcolare",
                "Dettaglio della riga senza esito");
            string detail = ConcreteDomainChart.Detail("Elastico", false, J.Obj(("tipo", "N–M"), ("proietta", "Sì")), row, null,
                [new ConcreteResults.Action(-100, -5, 0), new ConcreteResults.Action(300, 8, 2)], "direzione rifiutata");
            Check(detail.StartsWith("Elastico · N–M · proiezione attiva\n\nAZIONI · Locali\n", StringComparison.Ordinal)
                && detail.Contains("\n\nESTREMI CURVA\nN: -100,00 … 300,00 kN\nMx: -5,00 … 8,00 kNm\nMy: 0,00 … 2,00 kNm\n", StringComparison.Ordinal)
                && detail.EndsWith("\n\nDa correggere: direzione rifiutata", StringComparison.Ordinal), "Dettaglio con estremi della curva ed errore");
            Check(ConcreteDomainChart.Detail("Plastico", true, options, J.Obj(("nome", ""), ("N", "1")), null, null, null) == "Assegnare un nome alla combinazione.",
                "Dettaglio di una riga senza nome: il messaggio");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        return checks;
    }
}
