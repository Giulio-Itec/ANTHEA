using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Application.Concrete;
using Anthea.Application.Modules;
using Anthea.Calculations;

/// <summary>
/// Derivations of the section σ/ε and of the moment–curvature tab of the RC section moved from the historic view in sub-step S7 of F5.10
/// (ConcreteInspectionPresentation, ConcreteCurvaturePresentation), with values written by hand: the curve of the fixture «default» with its
/// options is that of its frozen historic trace (tests/fixtures/f5-sezione, ispezione/momento-curvatura.txt, the wait after the first «Calcola
/// curva»: MRd, χy, χu, μχ and 62 points). The identity of the historic view with these functions is proved by ui/check-sezione-historic.
/// </summary>
internal static class ConcreteInspectionPresentationChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks++; }
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
        try
        {
            Check(string.Join("|", ConcreteInspectionPresentation.States) == "Azione inserita|Punto limite"
                && ConcreteInspectionPresentation.States[ConcreteInspectionPresentation.DefaultState] == "Punto limite", "Soluzioni della sezione σ/ε, punto limite all'apertura");
            Check(ConcreteInspectionPresentation.Pending(true) == "Lettura del piano già calcolato al limite…" && ConcreteInspectionPresentation.Pending(false) == "Analisi dell’azione inserita…"
                && ConcreteInspectionPresentation.Completed(true) == "Piano nativo del punto resistente · nessuna nuova ricerca di Rd."
                && ConcreteInspectionPresentation.Completed(false) == "Azione inserita completa · leggi dei materiali della sezione; compressione negativa."
                && ConcreteInspectionPresentation.Unavailable("x") == "Soluzione non disponibile: x", "Messaggi della sezione σ/ε");

            var data = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "tests/fixtures/f3/inputs/str_palo--default.json")))!["data"]!.AsObject();
            ConcreteSheetPreparation.PrepareReport(data);
            var input = data["input"]!.AsObject(); var workspace = data["workspace_ca"]!.AsObject(); var options = workspace["momento_curvatura"]!.AsObject();
            string signature = ConcreteCurvaturePresentation.Signature(input, workspace);
            Check(signature.StartsWith(input.ToJsonString(), StringComparison.Ordinal) && signature.EndsWith(options.ToJsonString(), StringComparison.Ordinal),
                "Firma della curva: ingressi, trefoli, coefficienti e opzioni della curva");
            var curve = StructuralQueries.ConcreteCurvature(input, workspace, options);
            string text = ConcreteCurvaturePresentation.Text(curve);
            Check(text.StartsWith(curve.Status + "\n\nMRd = 800,027 kNm\nχy = 0,00406697 1/m\nχu = 0,0288128 1/m\r\nμχ = 7,085\r\n\nM [kNm]   χ [1/m]   ε0 [‰]   εc,comp [‰]   |εs|max [‰]\r\n0   0   0   0   0\r\n",
                StringComparison.Ordinal) && text.EndsWith(" · limite\r\n", StringComparison.Ordinal), "Testo della curva della fixture default (traccia storica congelata)");
            var points = ConcreteCurvaturePresentation.Points(curve);
            Check(points.Count == 62 && points[0][0] == 0 && points[0][1] == 0 && points[^1][1] == curve.Points[^1].Moment, "Serie della curva: 62 punti (χ, M)");
            string[] csv = ConcreteCurvaturePresentation.Csv(curve).Split('\n');
            Check(csv[0] == "M_kNm;Mx_kNm;My_kNm;chi_1_m;gx_1_m;gy_1_m;epsilon0_permille;epsilon_c_permille;epsilon_s_permille;limite" && csv[1].StartsWith("0;0;0;0;", StringComparison.Ordinal)
                && csv[1].EndsWith(";False\r", StringComparison.Ordinal) && csv.Length == 64, "CSV della curva: intestazione, un punto per riga con numeri invarianti");
            Check(ConcreteCurvaturePresentation.Unavailable("Passi: inserire un numero finito.") == "Curva non disponibile: Passi: inserire un numero finito.", "Curva non disponibile");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        return checks;
    }
}
