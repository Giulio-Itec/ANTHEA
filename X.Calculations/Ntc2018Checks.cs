namespace Anthea.Calculations;

/// <summary>
/// Risultati della fessurazione e del taglio della sezione c.a. (DTO degli adattatori di libreria) e regola di k₂. Il nucleo storico che li
/// calcolava (port di Rhino2Midas, con le correzioni NTC 2018 / Circolare 2019) è LegacyNtc2018Checks in tests/ANTHEA.Legacy (decisione D3 del
/// 15/10/2026).
/// </summary>
public static class Ntc2018Checks
{
    public sealed record CrackResult(double? Width, double? Limit, double? Ratio, bool? Passed, string Status, double? EffectiveArea = null, double? EffectiveSteel = null, double? BarSpacing = null, string? SpacingSource = null)
    {
        public CrackCalculationDetail[] Details { get; init; } = [];
        public ConcreteEffectiveRegion[] Regions { get; init; } = [];
    }
    /// <summary>
    /// k₂ of the NTC 2018 branch chosen from the bar stresses (0,50 with at least one compressed ordinary bar, 1,00 otherwise)
    /// instead of the neutral axis: false since 7/10/2026 (decision D7-b); true reproduces the earlier rule, kept only for the
    /// checks dedicated to the legacy rule (X.Verifiche --checker: CrackK2 and the bar-rule comparisons of CrackK2Checks) and for the
    /// historical comparisons with 733a77c (rebuilt with true), not for the fixtures: the concrete fixtures of Checker (06d97733) are
    /// captures of the current behaviour (d2d3225, false).
    /// With false k₂ = 0,50 for every standard when the neutral axis crosses the section
    /// (Circolare 2019 C4.1.2.2.4.5, EN 1992-1-1 7.3.4(3)); a fully compressed section returns wk = 0 before k₂ is chosen,
    /// a fully tensioned one keeps (εmax + εmin)/(2 εmax).
    /// True reproduces the numbers, the verdicts, the trace and the summary of 733a77c: every text added by D7-b depends on this
    /// switch (Cracking, ConcreteTensionCracking, ConcreteInnerCracking "k₂ della fascia", ConcreteCodeChecks k₂ of MC/DIN,
    /// CrackCalculationSummary), and the bar stresses are checked, with the "k₂:" message, before the compression return.
    /// Not reproduced in either case: the bound on the tensile depth h − x of the inner bands of hollow sections (ConcreteInnerCracking,
    /// R15: d2d3225, height along the gradient confirmed by the user on 7/10/2026), which was unbounded in 733a77c with a noise strain gradient.
    /// </summary>
    public const bool NtcK2FromCompressedBars = false;

    public sealed record ShearResult(double VRsd, double VRcd, double VRd, double? Ratio, double CotTheta, string Status)
    {
        public string Reference { get; init; } = "NTC 2018";
        public string Model { get; init; } = "NTC 2018";
        public CrackCalculationDetail[] Details { get; init; } = [];
    }
}
