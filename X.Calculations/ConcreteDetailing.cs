using NativeDetailing = GPC.Checkers.Concrete.Detailing;

namespace Anthea.Calculations;

/// <summary>UI-independent contracts, mm/MPa/kN. Reusable by section and future anchorage modules.</summary>
public enum ConcreteMemberKind { Beam, Column, Slab, Wall }
public sealed record DetailingCheck(string Name, double? Actual, double? Limit, string Unit, bool? Passed, string Reference, string Explanation);
public sealed record AnchorageInput(double Diameter, double Stress, double Fctk05, double GammaC, bool GoodBond,
    double AvailableLength, bool Lap, double LapPercent, double LapClearDistance);
public sealed record AnchorageResult(double Fbd, double BasicLength, double RequiredLength, bool Passed, string Expression)
{
    public double Eta1 { get; init; }
    public double Eta2 { get; init; }
    public double Alpha6 { get; init; }
    public double MaximumLapClearDistance { get; init; }
    public bool LengthPassed { get; init; }
    public bool LapClearDistancePassed { get; init; }
}
public interface IConcreteAnchorageCalculator { AnchorageResult Calculate(AnchorageInput input); }
public sealed class ConcreteAnchorageCalculator : IConcreteAnchorageCalculator
{
    public AnchorageResult Calculate(AnchorageInput p)
    {
        NativeDetailing.AnchorageResult r;
        try
        {
            r = NativeDetailing.AnchorageCalculator.Calculate(NativeDetailing.DetailingProfile.Ntc2018,
                new(p.Diameter, p.Stress, p.Fctk05, p.GammaC, p.GoodBond, p.AvailableLength, p.Lap, p.LapPercent, p.LapClearDistance));
        }
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
    bool CompressionBarsRestrained, bool EndSupportAnchorageConfirmed);
public interface IConcreteDetailingCalculator { IReadOnlyList<DetailingCheck> Calculate(ConcreteDetailingInput input); }
public sealed class ConcreteDetailingCalculator : IConcreteDetailingCalculator
{
    public IReadOnlyList<DetailingCheck> Calculate(ConcreteDetailingInput p)
    {
        var s = p.Section;
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
            s.AreaCls, fck, ConcreteMaterialCatalog.Material(fck).Fctm, fyk, s.Fyd,
            s.Shape == "A T" ? s.Input.D("flange_width_mm") : s.Width,
            s.Shape == "A T" ? s.Input.D("web_width_mm") : s.Width,
            s.Shape == "A T" ? s.Input.D("web_width_mm") : s.Width,
            p.CompressionKn * 1000, p.HasStirrups, p.StirrupDiameter, p.StirrupSpacing, p.StirrupLegs,
            p.Aggregate, s.Input.D("cover_mm"), double.IsFinite(p.MinimumDurabilityCover) ? p.MinimumDurabilityCover : null,
            p.CoverDeviation, p.LapZone, p.CompressionBarsRestrained, p.EndSupportAnchorageConfirmed, 0, 0,
            new(new(p.SecondarySteelPerMetre, p.SecondarySpacing, p.CriticalSlabRegion), legacyNegativeLinkLegs: true));
        try
        {
            return NativeDetailing.MemberDetailingCalculator.Calculate(NativeDetailing.DetailingProfile.Ntc2018, input)
                .Checks.Select(ConcreteDetailingTexts.Translate).ToArray();
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
