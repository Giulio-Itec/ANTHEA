// Spostato da X.Calculations/LegacyServiceability.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: motore SLE legacy, raggiungibile
// solo con l'aggancio delle prove. Codice storico conservato per le prove, fuori dall'installer. Il record ServiceabilityStressLimits resta nel
// prodotto (X.Calculations/ServiceabilityStressLimits.cs), senza il marcatore del motore.
using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Results;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace Anthea.Calculations;

/// <summary>
/// Limiti tensionali SLE del motore legacy, estratti senza modifiche da CheckerSection.DescribeStress (refactoring F2.7b, commit A3;
/// progetto F2.7 §5, rilievo M5): stessi controlli nativi, stesse espressioni e stesso ordine dei rifiuti. Dal commit A4 li chiama solo
/// l'adattatore SLE (ConcreteServiceabilityAdapter) con il motore Legacy (prova 5k); resta raggiungibile fino a F2.11 (progetto F2.7 §10).
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

    /// <summary>
    /// Ambito della fessurazione del motore legacy, spostato senza modifiche da SleCheckScope.Cracking (refactoring F2.7b, commit A4): falso
    /// soltanto se il requisito della norma per l'insieme è «Non richiesta…»; norme escluse e dati non validi danno vero (verifica visibile).
    /// Dal commit A4 lo chiama solo l'adattatore SLE con il motore Legacy (prova 5k).
    /// </summary>
    public static bool CrackingRequired(string set, JsonNode workspace)
    {
        try { return !LegacyConcreteCodeChecks.CrackRequirement(workspace.S("normativa", "NTC 2018"), set,
            workspace["sle"]?[set] as JsonObject ?? new()).Kind.StartsWith("Non richiesta"); }
        catch (ArgumentException) { return true; }
    }
}
