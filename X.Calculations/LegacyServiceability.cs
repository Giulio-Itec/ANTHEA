using GPC.Checkers.Concrete.Results;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace Anthea.Calculations;

/// <summary>Tasso, stato e limiti tensionali SLE di uno stato tensionale (MPa, valori assoluti dei limiti).</summary>
/// <param name="Ratio">Tasso delle combinazioni con limiti (rara: CLS e acciaio; quasi permanente: CLS); null per le altre.</param>
/// <param name="Status">«Entro limiti tensionali», «Oltre limiti tensionali» o, senza tasso, «Stato tensionale calcolato».</param>
/// <param name="ConcreteStressLimit">Limite del calcestruzzo della combinazione, con il fattore dei getti sottili; null senza limite.</param>
/// <param name="SteelStressLimit">Limite dell'armatura k3·fyk della prima barra, mostrato per ogni stato.</param>
public sealed record ServiceabilityStressLimits(double? Ratio, string Status, double? ConcreteStressLimit, double SteelStressLimit);

/// <summary>
/// Limiti tensionali SLE del motore legacy, estratti senza modifiche da CheckerSection.DescribeStress (refactoring F2.7b, commit A3;
/// progetto F2.7 §5, rilievo M5): stessi controlli nativi, stesse espressioni e stesso ordine dei rifiuti. Dal commit A4 li chiama solo
/// l'adattatore SLE con il motore Legacy (prova 5k); resta raggiungibile fino a F2.11 (progetto F2.7 §10).
/// Insiemi con limiti: «SLE» (combinazione rara: CLS k1·fck, armature k3·fyk) e «SLE_QP» (quasi permanente: CLS k2·fck); per gli altri
/// (SLE_FREQ, ED, LIMITE, CURVA, BENCH) nessun tasso e nessun limite del calcestruzzo. Il fattore dei getti sottili riduce il limite del
/// calcestruzzo e divide i suoi tassi nativi; non tocca l'acciaio.
/// </summary>
public static class LegacyServiceability
{
    /// <param name="result">Stato tensionale nativo già calcolato (nessun nuovo equilibrio).</param>
    /// <param name="set">Insieme di combinazioni di ANTHEA (SLE, SLE_QP, SLE_FREQ, ED, LIMITE…).</param>
    /// <param name="standard">Norma effettiva dell'analisi, con i coefficienti personalizzati (ConcreteStandards.Effective).</param>
    /// <param name="section">Sezione dell'analisi: calcestruzzo e prima barra.</param>
    /// <param name="psiRebar">φ delle armature dell'analisi lineare.</param>
    /// <param name="psiTendon">φ dei trefoli dell'analisi lineare.</param>
    /// <param name="thinCastingFactor">Fattore dei getti sottili della sezione (1 senza riduzione).</param>
    public static ServiceabilityStressLimits StressLimits(StressAnalysisResult result, string set, StandardModelCode2010 standard, ReinforcedConcreteSection section,
        double psiRebar, double psiTendon, double thinCastingFactor)
    {
        bool linear = result.LinearElasticAnalysis; double phi = psiRebar, phiT = psiTendon;
        double? ratio = null;
        if (set == "SLE")
        {
            var c = linear ? result.ConcreteServiceabilityCharacteristicCheck(phi) : result.ConcreteServiceabilityCharacteristicCheck();
            var s = linear ? result.SteelServiceabilityCharacteristicCheck(phi, phiT) : result.SteelServiceabilityCharacteristicCheck();
            ratio = Math.Max(c.Where(p => p.tension < 0).Select(p => p.workingRatio / thinCastingFactor).DefaultIfEmpty(0).Max(), s.Max(p => p.workingRatio));
        }
        if (set == "SLE_QP") ratio = (linear ? result.ConcreteServiceabilityQuasiPermanentCheck(phi) : result.ConcreteServiceabilityQuasiPermanentCheck()).Where(p => p.tension < 0).Select(p => p.workingRatio / thinCastingFactor).DefaultIfEmpty(0).Max();
        if (ratio.HasValue && !double.IsFinite(ratio.Value)) throw new ArgumentException("Checker: tasso tensionale non valido.");
        var material = (ConcreteMaterialEuropeanCommon)section.ConcreteMaterial;
        return new(ratio, ratio is null ? "Stato tensionale calcolato" : ratio <= 1 ? "Entro limiti tensionali" : "Oltre limiti tensionali",
            set == "SLE" ? standard.ServiceabilityStressConcreteCoefficientForCharacteristicCombination * Math.Abs(material.Fck) * thinCastingFactor : set == "SLE_QP" ? standard.ServiceabilityStressConcreteCoefficientForQuasiPermanentCombination * Math.Abs(material.Fck) * thinCastingFactor : null,
            standard.ServiceabilityStressSteelCoefficientForCharacteristicCombination * Math.Abs(section.Rebars.First().RebarMaterial.Fyk));
    }
}
