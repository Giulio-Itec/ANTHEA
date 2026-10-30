using NativeDetailing = GPC.Checkers.Concrete.Detailing;

namespace Anthea.Calculations;

/// <summary>UI-independent contracts, mm/MPa/kN. Reusable by section and future anchorage modules.</summary>
public enum ConcreteMemberKind { Beam, Column, Slab, Wall }
public sealed record DetailingCheck(string Name, double? Actual, double? Limit, string Unit, bool? Passed, string Reference, string Explanation);
/// <summary>RibbedBars: barre ad aderenza migliorata (predefinito); con barre lisce la libreria non ha regole e l'ancoraggio è rifiutato
/// con <see cref="ConcreteAnchorageCalculator.PlainBarsMessage"/> (analisi dei bug del 15/10/2026, DET-2).</summary>
public sealed record AnchorageInput(double Diameter, double Stress, double Fctk05, double GammaC, bool GoodBond,
    double AvailableLength, bool Lap, double LapPercent, double LapClearDistance, bool RibbedBars = true);
public sealed record AnchorageResult(double Fbd, double BasicLength, double RequiredLength, bool Passed, string Expression)
{
    public double Eta1 { get; init; }
    public double Eta2 { get; init; }
    public double Alpha6 { get; init; }
    public double MaximumLapClearDistance { get; init; }
    public bool LengthPassed { get; init; }
    public bool LapClearDistancePassed { get; init; }
}
// L'interfaccia IConcreteAnchorageCalculator, senza utilizzatori, è in tests/ANTHEA.Legacy dalla decisione E5 del 15/10/2026 (F2.11c).
public sealed class ConcreteAnchorageCalculator
{
    /// <summary>Rifiuto delle barre lisce: la verifica resta da completare, mai soddisfatta.</summary>
    public const string PlainBarsMessage = "Ancoraggi: regole implementate per barre ad aderenza migliorata; con barre lisce la verifica resta da completare.";
    public AnchorageResult Calculate(AnchorageInput p)
    {
        NativeDetailing.AnchorageResult r;
        try
        {
            r = NativeDetailing.AnchorageCalculator.Calculate(NativeDetailing.DetailingProfile.Ntc2018,
                new(p.Diameter, p.Stress, p.Fctk05, p.GammaC, p.GoodBond, p.AvailableLength, p.Lap, p.LapPercent, p.LapClearDistance, ribbedBars: p.RibbedBars));
        }
        catch (NotSupportedException) when (!p.RibbedBars) { throw new ArgumentException(PlainBarsMessage); }
        catch (ArgumentException e)
        {
            throw new ArgumentException(e.Message switch
            {
                "Anchorage: check diameter, stress, materials and lengths." => "Ancoraggio: controllare diametro, tensione, materiali e lunghezze.",
                "Bar diameter out of range for bond." => "Diametro fuori campo per l’aderenza.",
                _ => e.Message
            });
        }
        return new(r.Fbd,r.BasicLength,r.RequiredLength,r.Passed,
            p.Lap?"l0 = max[α6·lb,rqd; 0,3·α6·lb,rqd; 20Ø; 200 mm]; α6 = clamp(√(%/25),1,1,5). Interferro ≤ 4Ø. Barre rettilinee, α1…α5 = 1."
            :"lb,rqd = Ø·σsd/(4fbd); lbd = max(lb,rqd; 20Ø; 150 mm). Barra rettilinea, nessuna riduzione favorevole.")
        { Eta1 = r.Eta1, Eta2 = r.Eta2, Alpha6 = r.Alpha6, MaximumLapClearDistance = r.MaximumLapClearDistance,
            LengthPassed = r.LengthPassed, LapClearDistancePassed = r.LapClearDistancePassed };
    }
}
public sealed record ConcreteDetailingInput(ConcreteMemberKind Kind, SezioneCA Section, double CompressionKn,
    bool HasStirrups, double StirrupDiameter, double StirrupSpacing, int StirrupLegs,
    double Aggregate, double MinimumDurabilityCover, double CoverDeviation, bool LapZone,
    double SecondarySteelPerMetre, double SecondarySpacing, bool CriticalSlabRegion,
    bool CompressionBarsRestrained, bool EndSupportAnchorageConfirmed)
{
    /// <summary>fyd delle regole dei dettagli quando differisce da fyk/γs dell'input della sezione: quello della norma effettiva con i coefficienti
    /// unitari del foglio c.a. del palo (fyd = fyk). Null: SezioneCA.Fyd, come prima.</summary>
    public double? DesignYieldStrength { get; init; }
}
public interface IConcreteDetailingCalculator { IReadOnlyList<DetailingCheck> Calculate(ConcreteDetailingInput input); }
public sealed class ConcreteDetailingCalculator : IConcreteDetailingCalculator
{
    public IReadOnlyList<DetailingCheck> Calculate(ConcreteDetailingInput p)
    {
        var s = p.Section;
        Anthea.ConcreteWorkspace.ConcreteBarMaterials.RequireSheetProperties(s.Input, "Dettagli costruttivi", "fyk_mpa");
        var capsulePath = s.Shape == "Capsula" && p.Kind == ConcreteMemberKind.Column ? s.Template?.ColumnBarPath : null;
        if (s.Template is not null && capsulePath is null)
            throw new ArgumentException("Dettagli della tipologia da wizard: verificare separatamente pareti, ali, ancoraggi e trattenimento delle barre. Le regole automatiche delle sezioni usuali non sono applicate a questa disposizione.");
        int legs = p.Kind == ConcreteMemberKind.Beam && p.HasStirrups
            ? SectionWorkspace.Subdivisions(p.StirrupLegs.ToString(), "rami_y", 2, 100) : 0;
        if (p.Kind == ConcreteMemberKind.Slab && (s.Shape != "Rettangolare" || s.Holes.Count > 0))
            throw new ArgumentException("Soletta piena: usare una striscia rettangolare senza foro, b = larghezza e h = spessore.");
        double fck = s.Input.D("fck_mpa"), fyk = s.Input.D("fyk_mpa");
        var kind = p.Kind switch
        {
            ConcreteMemberKind.Beam => NativeDetailing.MemberDetailingKind.Beam,
            ConcreteMemberKind.Column => NativeDetailing.MemberDetailingKind.Column,
            ConcreteMemberKind.Slab => NativeDetailing.MemberDetailingKind.Slab,
            _ => NativeDetailing.MemberDetailingKind.Wall
        };
        var input = new NativeDetailing.MemberDetailingInput(kind, ConcreteLibraryMapping.CrackGeometry(s),
            s.AreaCls, fck, ConcreteMaterialCatalog.Material(fck).Fctm, fyk, p.DesignYieldStrength ?? s.Fyd,
            s.Shape == "A T" ? s.Input.D("flange_width_mm") : s.Width,
            s.Shape == "A T" ? s.Input.D("web_width_mm") : s.Width,
            s.Shape == "A T" ? s.Input.D("web_width_mm") : s.Width,
            p.CompressionKn * 1000, p.HasStirrups, p.StirrupDiameter, p.StirrupSpacing, legs,
            p.Aggregate, s.Input.D("cover_mm"), double.IsFinite(p.MinimumDurabilityCover) ? p.MinimumDurabilityCover : null,
            p.CoverDeviation, p.LapZone, p.CompressionBarsRestrained, p.EndSupportAnchorageConfirmed, 0, 0,
            // F5.15: interasse dei pilastri lungo il perimetro delle barre (R12); riduzione 0,6 delle staffe nelle giunzioni EC solo con Ømax > 14 mm (R13).
            new(new(p.SecondarySteelPerMetre, p.SecondarySpacing, p.CriticalSlabRegion), legacyNegativeLinkLegs: false, legacyPlateSpacingAcrossLayers: false,
                columnSpacingAlongPerimeter: true, lapLinkReductionAboveFourteen: true));
        try
        {
            var result = capsulePath is null
                ? NativeDetailing.MemberDetailingCalculator.Calculate(NativeDetailing.DetailingProfile.Ntc2018, input)
                : NativeDetailing.MemberDetailingCalculator.CalculateColumnOnPerimeter(NativeDetailing.DetailingProfile.Ntc2018, input, capsulePath);
            return result.Checks.Select(c => {
                var translated = ConcreteDetailingTexts.Translate(c);
                if (capsulePath is not null && c.Key == "LongitudinalSpacing")
                    translated = translated with { Explanation = c.Passed.HasValue
                        ? "Interasse lungo il percorso chiuso degli assi barra, inclusi i tratti curvi discretizzati e la chiusura, ≤ 300 mm."
                        : "Una o più barre sono fuori dal percorso perimetrale: verificare separatamente l’interasse longitudinale." };
                return translated;
            }).ToArray();
        }
        catch (ArgumentException e)
        {
            throw new ArgumentException(e.Message switch
            {
                "Detailing: numerical data must be finite and non negative." => "Dettagli: i parametri numerici devono essere finiti e non negativi.",
                "Detailing: solid slab: use a rectangular strip without holes, b = width and h = thickness." => "Soletta piena: usare una striscia rettangolare senza foro, b = larghezza e h = spessore.",
                _ => e.Message
            });
        }
    }
}
