using GPC.Checkers.Concrete.Shear;
using GPC.Checkers.Concrete.Torsion;
using LibraryTorsionGeometry = GPC.Checkers.Concrete.Torsion.TorsionGeometry;

namespace Anthea.Calculations;

/// <summary>Motore delle verifiche di taglio e torsione della sezione c.a.: il nucleo legacy di X.Calculations o GPCChecker.Concrete.</summary>
public enum ShearTorsionEngine { Legacy, Library }

/// <summary>
/// Adattatore di taglio e torsione della sezione verso GPCChecker.Concrete (refactoring F2.5-F2.6, docs/refactoring/piano.md).
/// Stesso contratto del legacy (ConcreteCodeChecks.Shear, ConcreteTorsionCalculator): ingressi e DTO in kN e kNm, testi e rifiuti
/// italiani identici, tramite <see cref="ConcreteLibraryMapping"/>. Nessuna formula: i calcoli sono della libreria o del legacy.
/// Il motore legacy resta raggiungibile passando <see cref="ShearTorsionEngine.Legacy"/> (confronti, cattura densa con
/// '--motore legacy') finché l'utente non decide la sua eliminazione (F2.11).
/// </summary>
public static class ConcreteShearTorsionAdapter
{
    /// <summary>Interruttore del motore usato da ANTHEA quando il chiamante non ne indica uno: la libreria dal passo F2.6 per
    /// taglio, profilo resistente e torsione (equivalenza misurata nel registro F2-1); il legacy resta raggiungibile con
    /// <see cref="ShearTorsionEngine.Legacy"/>.</summary>
    public static ShearTorsionEngine Default => ShearTorsionEngine.Library;

    /// <summary>Taglio di una direzione, come ConcreteCodeChecks.Shear.</summary>
    public static Ntc2018Checks.ShearResult Shear(ConcreteCodeChecks.ShearInput p, ShearTorsionEngine? engine = null)
    {
        if ((engine ?? Default) == ShearTorsionEngine.Legacy) return ConcreteCodeChecks.Shear(p);
        var standard = ConcreteLibraryMapping.StandardFor(p.Standard);
        SectionShearResult result;
        try
        {
            result = SectionShearCalculator.Calculate(new SectionShearInput(standard,
                ConcreteLibraryMapping.NewtonsFromKilonewtons(p.N), ConcreteLibraryMapping.NewtonsFromKilonewtons(p.V),
                ConcreteLibraryMapping.NewtonMillimetresFromKilonewtonMetres(p.M), p.Area, p.Bw, p.D, p.Asl, p.Fck, p.Fcd, p.Fyd, p.GammaC, p.Es,
                p.Asw, p.Spacing, p.Alpha, p.CotTheta, p.LeverFactor, p.Aggregate, p.AxialEccentricity));
        }
        catch (ArgumentException e) { throw ConcreteLibraryMapping.ShearError(e, p.Standard); }
        return ConcreteLibraryMapping.ToShearResult(result, p.Standard);
    }

    /// <summary>
    /// Torsione NTC 2018 con l'interazione del taglio delle due direzioni, come ConcreteTorsionCalculator.Calculate.
    /// fck e γc servono alla libreria (validazione del campo delle classi; per NTC non entrano nelle resistenze); il legacy non li usa.
    /// </summary>
    public static TorsionResult Torsion(TorsionInput p, double fck, double gammaC, ShearTorsionEngine? engine = null)
    {
        if ((engine ?? Default) == ShearTorsionEngine.Legacy) return new ConcreteTorsionCalculator().Calculate(p);
        // Contratto di ANTHEA: senza staffe chiuse la torsione è un dato non valido, con il messaggio del legacy. La libreria darebbe
        // invece una verifica non soddisfatta con resistenza nulla (registro delle differenze, F2-3).
        if (p.StirrupLegArea == 0) throw new ArgumentException(ConcreteLibraryMapping.TorsionInputMessage);
        SectionTorsionResult result;
        try
        {
            var geometry = new LibraryTorsionGeometry(p.Geometry.Area, p.Geometry.Perimeter, p.Geometry.Thickness);
            var input = new SectionTorsionInput(ConcreteLibraryMapping.StandardFor(ConcreteLibraryMapping.TorsionStandardName),
                ConcreteLibraryMapping.NewtonMillimetresFromKilonewtonMetres(p.TorqueKnM), geometry, fck, p.Fcd, gammaC, p.Fyd, p.Fyd,
                p.StirrupLegArea, p.Spacing, p.AvailableLongitudinalArea, p.CotTheta);
            result = SectionTorsionCalculator.Evaluate(input, Component(p.VxKn, p.ShearX), Component(p.VyKn, p.ShearY));
        }
        catch (ArgumentException e) { throw ConcreteLibraryMapping.TorsionError(e); }
        return ConcreteLibraryMapping.ToTorsionResult(result, p.Geometry);
    }

    static TorsionShearComponent Component(double demandKn, Ntc2018Checks.ShearResult shear)
        => new(ConcreteLibraryMapping.NewtonsFromKilonewtons(demandKn), ConcreteLibraryMapping.NewtonsFromKilonewtons(shear.VRsd),
            ConcreteLibraryMapping.NewtonsFromKilonewtons(shear.VRcd), shear.CotTheta);

    /// <summary>
    /// Profilo resistente proposto dal contorno della sezione, come ConcreteTorsionCalculator.Geometry: rettangolo o cerchio, pieni o
    /// con il foro centrato. La distanza dell'asse delle barre dal bordo (copriferro + Ø staffa + metà della barra più grossa) e
    /// l'area di calcestruzzo vengono dalla sezione di ANTHEA.
    /// </summary>
    public static TorsionGeometry TorsionGeometryOf(SezioneCA s, ShearTorsionEngine? engine = null)
    {
        if ((engine ?? Default) == ShearTorsionEngine.Legacy) return ConcreteTorsionCalculator.Geometry(s);
        if (s.Shape is not ("Rettangolare" or "Circolare")) throw new ArgumentException(ConcreteLibraryMapping.TorsionShapeMessage);
        double axis = s.Input.D("cover_mm") + s.Input.D("transverse_bar_diameter_mm") + s.Bars.Max(b => b.Diametro) / 2;
        bool hollow = s.Input.B("foro_presente");
        LibraryTorsionGeometry g;
        try
        {
            g = s.Shape == "Circolare"
                ? LibraryTorsionGeometry.Circle(s.Width, s.AreaCls, axis, hollow ? s.Input.D("inner_diameter_mm") : null)
                : LibraryTorsionGeometry.Rectangle(s.Width, s.Height, s.AreaCls, axis, hollow ? s.Input.D("inner_width_mm") : null, hollow ? s.Input.D("inner_height_mm") : null);
        }
        catch (ArgumentException e) { throw ConcreteLibraryMapping.TorsionGeometryError(e); }
        return new(g.EnclosedArea, g.Perimeter, g.Thickness);
    }
}
