using System.Globalization;
using System.Text.Json.Nodes;
using Anthea.Calculations;
using GPC.Model.Materials;

/// <summary>
/// Prova 4d: «CLS teso nelle risposte» del M–χ della sezione c.a. (16/10/2026), opzione SectionOptions.StressAnalysisConcreteTension =
/// TensionStiffening di GPCChecker.Concrete 0.11 (pagina ca.momento-curvatura, riquadro M-3). (a) Il predefinito e i fogli senza la chiave
/// danno la curva di prima, bit per bit uguale a quella con «Trascurato» esplicito; (b) con «Tension stiffening» i punti della curva seguono
/// il modello a fibre indipendente attesi/cls_teso_risposte.py e il punto limite resta quello del dominio, identico al predefinito;
/// (c) l'opzione raggiunge la libreria solo dal M–χ: le verifiche SLE e SLU costruiscono il verificatore senza.
/// </summary>
internal static class ClsTesoRisposte
{
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    static JsonObject Workspace() => new() { ["normativa"] = "NTC 2018", ["trefoli"] = new JsonArray() };

    /// <summary>Rettangolo 300×500 C25/30, 2Ø16 sopra e 2Ø16 sotto (copriferro 30, staffe Ø8: y = ±204), B450C elastico-perfettamente plastico.</summary>
    static JsonObject Section()
    {
        var input = SezioneCA.DefaultCatalogInput();
        input["shape"] = "Rettangolare"; input["width_mm"] = "300"; input["height_mm"] = "500";
        input["top_bar_count"] = "2"; input["top_bar_diameter_mm"] = "16"; input["bottom_bar_count"] = "2"; input["bottom_bar_diameter_mm"] = "16";
        input["side_bar_count_per_side"] = "0"; input["cover_mm"] = "30"; input["transverse_bar_diameter_mm"] = "8";
        input["fck_mpa"] = "25"; input["cls_diagramma"] = "Parabola-rettangolo";
        input["fyk_mpa"] = "450"; input["steel_modulus_mpa"] = "200000"; input["steel_eps_u"] = "75"; input["steel_diagramma"] = "Elastoplastico";
        return input;
    }

    /// <summary>Opzioni del M–χ: N = 0, θ = 0, 10 passi uniformi fino al limite (punti a k/10 di MRd); <paramref name="tension"/> null = chiave assente.</summary>
    static JsonObject Options(string? tension)
    {
        var o = J.Obj(("N", "0"), ("theta", "0"), ("passi", "10"), ("frazione", "1"), ("campionamento", "Uniforme"), ("trazione_cls", "No"),
            ("angoli", "64"), ("tolleranza_n", "1"), ("raffina_snervamento", "12"));
        if (tension is not null) o[ConcreteCurvatureAnalysis.ResponseConcreteTensionKey] = tension;
        return o;
    }

    public static int Run(Action<bool, string> check)
    {
        int count = 0;
        void Is(bool condition, string message) { check(condition, message); count++; }
        void Near(double? value, double expected, double relative, string message)
        {
            check(value is double v && double.IsFinite(v) && Math.Abs(v - expected) <= relative * Math.Abs(expected),
                $"{message}: {value?.ToString("G17", Invariant) ?? "null"} invece di {expected.ToString("G17", Invariant)} (±{relative:P1})");
            count++;
        }
        string modes = string.Join(" / ", ConcreteCurvatureAnalysis.ResponseConcreteTensionModes);
        Is(modes == "Trascurato / Tension stiffening", "4d scelte di «CLS teso nelle risposte»: " + modes);

        // ---------------------------------------------------------------- (a) predefinito e fogli senza la chiave
        var input = Section();
        var absent = ConcreteCurvatureAnalysis.Calculate(input, Workspace(), Options(null));
        var neglected = ConcreteCurvatureAnalysis.Calculate(input, Workspace(), Options("Trascurato"));
        Is(JsonNode.DeepEquals(J.Node(absent), J.Node(neglected)), "4d foglio senza la chiave diverso da «Trascurato» esplicito");
        Is(!absent.Status.Contains("CLS teso nelle risposte"), "4d stato del predefinito con la frase del tension stiffening");
        Is(ConcreteCurvatureAnalysis.ResponseConcreteTension(Options(null)) == ConcreteMaterial.TensionResponseDiagrams.Default
            && ConcreteCurvatureAnalysis.ResponseConcreteTension(Options("Trascurato")) == ConcreteMaterial.TensionResponseDiagrams.Default
            && ConcreteCurvatureAnalysis.ResponseConcreteTension(Options("Tension stiffening")) == ConcreteMaterial.TensionResponseDiagrams.TensionStiffening,
            "4d mappatura della scelta sul diagramma della libreria");
        string? rejection = null;
        try { ConcreteCurvatureAnalysis.Calculate(input, Workspace(), Options("Resistente")); } catch (ArgumentException ex) { rejection = ex.Message; }
        Is(rejection == "CLS teso nelle risposte: scegliere «Trascurato» o «Tension stiffening».", "4d valore sconosciuto: " + (rejection ?? "accettato"));

        // Attesi indipendenti (attesi/cls_teso_risposte.py, modello a fibre, CLS teso nullo): MRd(N = 0) = 68,4462 kNm, χu = 7,619404e-2 1/m;
        // a 0,3 MRd χ = 1,771717e-3 1/m e |εs|max = 0,61321 ‰; a 0,6 MRd χ = 3,554760e-3 1/m e |εs|max = 1,22799 ‰. Tolleranze: 0,2% sul punto
        // limite e 0,5% sui punti (scarti osservati sotto lo 0,05%: integrazione sulla mesh della libreria; d ln εs / d ln M = 1,00 dello script).
        Near(absent.LimitMoment, 68.4462, .002, "4d MRd(N = 0) contro il modello a fibre");
        Near(absent.UltimateCurvature, 7.619404e-2, .002, "4d χu contro il modello a fibre");
        MomentCurvaturePoint At(MomentCurvatureResult curve, double fraction) => curve.Points.Single(p => Math.Abs(p.Moment / curve.LimitMoment - fraction) < 1e-9);
        Near(At(absent, .3).Curvature, 1.771717e-3, .005, "4d Trascurato, 0,3 MRd: χ");
        Near(At(absent, .3).SteelStrain, 0.61321, .005, "4d Trascurato, 0,3 MRd: |εs|max");
        Near(At(absent, .6).Curvature, 3.554760e-3, .005, "4d Trascurato, 0,6 MRd: χ");
        Near(At(absent, .6).SteelStrain, 1.22799, .005, "4d Trascurato, 0,6 MRd: |εs|max");

        // ---------------------------------------------------------------- (b) «Tension stiffening»
        var stiffening = ConcreteCurvatureAnalysis.Calculate(input, Workspace(), Options("Tension stiffening"));
        Is(stiffening.Status.EndsWith(" " + ConcreteCurvatureAnalysis.ResponseConcreteTensionStatus, StringComparison.Ordinal), "4d stato senza la frase del tension stiffening: " + stiffening.Status);
        // Punto limite dal dominio, che non legge l'opzione: identico bit per bit al predefinito (momento, curvatura, N e residuo, riga limite).
        Is(BitConverter.DoubleToInt64Bits(stiffening.LimitMoment) == BitConverter.DoubleToInt64Bits(absent.LimitMoment)
            && stiffening.UltimateCurvature is double u && absent.UltimateCurvature is double u0 && BitConverter.DoubleToInt64Bits(u) == BitConverter.DoubleToInt64Bits(u0)
            && BitConverter.DoubleToInt64Bits(stiffening.LimitAxialKn) == BitConverter.DoubleToInt64Bits(absent.LimitAxialKn)
            && BitConverter.DoubleToInt64Bits(stiffening.AxialResidualKn) == BitConverter.DoubleToInt64Bits(absent.AxialResidualKn),
            "4d punto limite diverso con il tension stiffening");
        Is(JsonNode.DeepEquals(J.Node(stiffening.Points.Single(p => p.Limit)), J.Node(absent.Points.Single(p => p.Limit))), "4d riga limite diversa con il tension stiffening");
        // Stessi campioni k/10 · MRd; il predefinito ha in più il punto del primo snervamento raffinato per bisezione fra 0,9 MRd e il limite.
        string Moments(MomentCurvatureResult curve) => string.Join("; ", curve.Points.Select(p => p.Moment.ToString("G9", Invariant)));
        Is(stiffening.Points.Count == 11 && stiffening.Points.All(p => absent.Points.Any(q => q.Moment == p.Moment)),
            "4d campioni diversi: tension stiffening " + Moments(stiffening) + " · predefinito " + Moments(absent));
        // Primo snervamento (attesi/cls_teso_risposte.py, εs = fyd/Es): senza trazione My = 65,2917 kNm e χy = 5,688458e-3 1/m; con il tension
        // stiffening My = 104,29 kNm > MRd, quindi nessun punto della curva prima del limite è snervato (riquadro M-3: snervamento al limite).
        Near(absent.Points.Where(p => p.Yielded).Min(p => p.Moment), 65.2917, .002, "4d Trascurato: momento del primo snervamento raffinato");
        Near(absent.YieldCurvature, 5.688458e-3, .005, "4d Trascurato: χy");
        Is(stiffening.Points.Where(p => !p.Limit).All(p => !p.Yielded) && stiffening.YieldCurvature == stiffening.UltimateCurvature,
            "4d tension stiffening: snervamento prima del limite, χy = " + stiffening.YieldCurvature?.ToString("G9", Invariant));
        // Attesi indipendenti (attesi/cls_teso_risposte.py, Collins e Mitchell 1991 con Ecm = 31475,8 MPa e fctm = 2,56496 MPa): a 0,3 MRd la
        // sezione non è fessurata, χ = 3,042456e-4 1/m e |εs|max = 0,07737 ‰; a 0,6 MRd χ = 7,815565e-4 1/m e |εs|max = 0,17578 ‰ (1,22799 ‰
        // senza trazione; a 40,2 kNm lo script dà 0,1703 ‰ contro 1,2019 ‰, come il riquadro M-3). Tolleranze: 0,5% a 0,3 MRd; 1% a 0,6 MRd, dove
        // d ln εs / d ln M = 1,5 e la legge ha il salto a εcr fra i punti di Gauss della libreria (circa 0,18 fctm, riquadro M-3); scarti
        // osservati sotto lo 0,05%.
        Near(At(stiffening, .3).Curvature, 3.042456e-4, .005, "4d tension stiffening, 0,3 MRd: χ");
        Near(At(stiffening, .3).SteelStrain, 0.07737, .005, "4d tension stiffening, 0,3 MRd: |εs|max");
        Near(At(stiffening, .6).Curvature, 7.815565e-4, .01, "4d tension stiffening, 0,6 MRd: χ");
        Near(At(stiffening, .6).SteelStrain, 0.17578, .01, "4d tension stiffening, 0,6 MRd: |εs|max");

        // ---------------------------------------------------------------- (c) solo il M–χ porta l'opzione alla libreria
        // Opzioni SLE con la chiave (copiata per errore o da un foglio futuro): il verificatore resta senza tension stiffening.
        var sle = J.Obj(("modello", "Non lineare"), ("phi", "0"), ("trazione_cls", "No"), ("assi", "Locali"),
            (ConcreteCurvatureAnalysis.ResponseConcreteTensionKey, "Tension stiffening"));
        Is(new CheckerSection(input, Workspace(), sle, "SLU").Checker.SectionCheckerOptions.StressAnalysisConcreteTension == ConcreteMaterial.TensionResponseDiagrams.Default,
            "4d opzione letta dalle opzioni SLE");
        Is(new CheckerSection(input, Workspace(), sle, "SLU", ConcreteMaterial.TensionResponseDiagrams.TensionStiffening).Checker.SectionCheckerOptions.StressAnalysisConcreteTension
            == ConcreteMaterial.TensionResponseDiagrams.TensionStiffening, "4d opzione del M–χ non passata al verificatore");
        return count;
    }
}
