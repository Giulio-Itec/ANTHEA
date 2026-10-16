using System.Collections.Immutable;
using GPC.Checkers.Concrete.Durability;
using Materiali;
using CoverInput = Materiali.CoverInput;
using CoverLine = Materiali.CoverLine;
using CoverResult = Materiali.CoverResult;
using LibraryCoverInput = GPC.Checkers.Concrete.Durability.CoverInput;

namespace Anthea.Calculations;

/// <summary>
/// Adattatore della durabilità e dei copriferri verso GPC.Checkers.Concrete.Durability (refactoring F2.9, docs/refactoring/piano.md).
/// Stesso contratto del legacy (record di Materials/DurabilityContracts.cs, testi e rifiuti italiani identici tramite
/// <see cref="ConcreteLibraryMapping"/>); con lo stesso ingresso i due motori danno gli stessi numeri bit per bit (stesse espressioni nello
/// stesso ordine). Regole: riceve valori, mai nodi JSON; nel ramo della libreria passa solo i codici delle esposizioni (gli altri campi
/// dei record Exposure non si usano: la libreria li prende dal proprio catalogo) e la Cmin pertinente come la riceve; profilo EN1992p11
/// per il copriferro EC2 della scheda Materiali, Ntc2018 per quello NTC. Nessuna formula né costante normativa (prova 11a).
/// Le facciate Materiali.Durability, NtcCover, MinimumConcrete e AtecapMix delegano qui. Il motore legacy non è più nel prodotto
/// (decisione D3 del 15/10/2026, modo b): le prove lo agganciano per la durata di un ambito tramite <see cref="ILegacyDurability"/>
/// (implementazione e selettore DurabilityEngine in tests/ANTHEA.Legacy; confronti, cattura densa con '--motore-durabilita legacy');
/// senza aggancio, quindi sempre in produzione, ogni operazione usa la libreria.
/// </summary>
public static class ConcreteDurabilityAdapter
{
    // Aggancio delle prove (decisione D3 del 15/10/2026, modo b) e sonda (F2.9-D14; vista tipizzata DurabilityProbe in tests/ANTHEA.Legacy):
    // restituisce il motore legacy agganciato nel flusso corrente, null in produzione, e registra l'operazione con il motore che la
    // esegue, «Legacy» con l'aggancio, altrimenti «Library». Ogni operazione la chiama una sola volta, in testa.
    static ILegacyDurability? Hook(string operation)
    {
        var legacy = EngineHook.Get<ILegacyDurability>();
        EngineHook.Record(nameof(ConcreteDurabilityAdapter), operation, EngineHook.EngineName(legacy));
        return legacy;
    }

    // ------------------------------------------------------------------ catalogo e funzioni per codice
    /// <summary>Catalogo delle 18 esposizioni della scheda Materiali, come Durability.Exposures.</summary>
    public static ImmutableArray<Exposure> Exposures()
    {
        if (Hook(nameof(Exposures)) is { } legacy) return legacy.Exposures();
        return ExposureClasses.All.Select(ConcreteLibraryMapping.DurabilityExposure).ToImmutableArray();
    }

    /// <summary>Gruppo ambientale NTC di un'esposizione, come NtcCover.Severity.</summary>
    public static int Severity(string code)
    {
        if (Hook(nameof(Severity)) is { } legacy) return legacy.Severity(code);
        return Library(() => ExposureClasses.Get(code).NtcEnvironment, ConcreteLibraryMapping.DurabilityUnknownExposureMessage);
    }

    /// <summary>Classe minima UNI 11104 di un'esposizione (fck, MPa), come MinimumConcrete.Fck.</summary>
    public static int MinimumFck(string code)
    {
        if (Hook(nameof(MinimumFck)) is { } legacy) return legacy.MinimumFck(code);
        return Library(() => ExposureClasses.Get(code).Uni11104MinStrength, ConcreteLibraryMapping.DurabilityUnknownClassMessage);
    }

    /// <summary>Limiti di composizione UNI 11104 di un'esposizione (A/C massimo, cemento minimo), come AtecapMix.Limits.</summary>
    public static (double? Ratio, int? Cement) MixLimits(string code)
    {
        if (Hook(nameof(MixLimits)) is { } legacy) return legacy.MixLimits(code);
        return Library(() => { var e = ExposureClasses.Get(code); return (e.Uni11104MaxWaterCement, e.Uni11104MinCement); }, ConcreteLibraryMapping.DurabilityUnknownExposureMessage);
    }

    /// <summary>Etichetta della classe minima, come MinimumConcrete.Label (libreria: F2.9-D13).</summary>
    public static string Label(int fck)
    {
        if (Hook(nameof(Label)) is { } legacy) return legacy.Label(fck);
        return ConcreteLibraryMapping.DurabilityStrengthLabel(fck);
    }

    // ------------------------------------------------------------------ combinazioni di esposizioni
    /// <summary>Almeno una esposizione, X0 non combinabile: come Durability.ValidateExposure, che dalla decisione E5 (F2.11c) sta nelle prove.</summary>
    public static void Validate(Exposure[] values)
    {
        if (Hook(nameof(Validate)) is { } legacy) { legacy.Validate(values); return; }
        Library(() => ExposureClasses.Resolve(Codes(values)), ConcreteLibraryMapping.DurabilityUnknownExposureMessage);
    }

    /// <summary>Classe minima UNI 11104 della combinazione, come MinimumConcrete.Required.</summary>
    public static int MinimumStrength(Exposure[] exposures)
    {
        if (Hook(nameof(MinimumStrength)) is { } legacy) return legacy.MinimumStrength(exposures);
        return Library(() => ExposureClasses.Uni11104MinimumStrength(Codes(exposures)), ConcreteLibraryMapping.DurabilityUnknownClassMessage);
    }

    /// <summary>Limiti di composizione UNI 11104 della combinazione (il minore A/C e il maggiore cemento), come AtecapMix.Required.</summary>
    public static (double? Ratio, int? Cement) Mix(Exposure[] active)
    {
        if (Hook(nameof(Mix)) is { } legacy) return legacy.Mix(active);
        return Library(() => { var limits = ExposureClasses.Uni11104Mix(Codes(active)); return (limits.Item1, limits.Item2); }, ConcreteLibraryMapping.DurabilityUnknownExposureMessage);
    }

    /// <summary>Aria minima per XF2-XF4 secondo Dmax, come AtecapMix.Air.</summary>
    public static double? Air(Exposure[] active, double dmax)
    {
        if (Hook(nameof(Air)) is { } legacy) return legacy.Air(active, dmax);
        return Library(() => ExposureClasses.Uni11104Air(Codes(active), dmax), ConcreteLibraryMapping.DurabilityUnknownExposureMessage);
    }

    // ------------------------------------------------------------------ copriferri
    /// <summary>Classe strutturale del prospetto 4.3N di un'esposizione, come Durability.StructuralClass.</summary>
    public static int StructuralClass(Exposure e, double fck, CoverInput p)
    {
        if (Hook(nameof(StructuralClass)) is { } legacy) return legacy.StructuralClass(e, fck, p);
        return Library(() => CoverRequirements.StructuralClass(ExposureClasses.Get(e.Code), Input([e.Code], fck, p, false, false, null)),
            ConcreteLibraryMapping.DurabilityUnknownExposureMessage);
    }

    /// <summary>Copriferro EC2 della scheda Materiali (EN 1992-1-1:2004, prospetti 4.3N e 4.4N), come Durability.Cover.</summary>
    public static CoverResult Cover(Exposure[] values, double fck, CoverInput p)
    {
        if (Hook(nameof(Cover)) is { } legacy) return legacy.Cover(values, fck, p);
        var r = Library(() => CoverRequirements.Calculate(DurabilityProfile.EN1992p11, Input(Codes(values), fck, p, false, false, null)),
            ConcreteLibraryMapping.DurabilityUnknownExposureMessage);
        return new(r.Bond, r.Durability, r.Minimum, r.Nominal, r.Lines.Select(l => new CoverLine(l.Exposure, l.StructuralClass!.Value, l.Durability)).ToArray());
    }

    /// <summary>Copriferro NTC 2018 con la Circolare 2019, come NtcCover.Calculate.</summary>
    public static NtcCoverResult NtcCover(Exposure[] values, double fck, CoverInput p, bool plate, bool coverQuality, double? pertinentCmin = null)
    {
        if (Hook(nameof(NtcCover)) is { } legacy) return legacy.NtcCover(values, fck, p, plate, coverQuality, pertinentCmin);
        var r = Library(() => CoverRequirements.Calculate(DurabilityProfile.Ntc2018, Input(Codes(values), fck, p, plate, coverQuality, pertinentCmin)),
            ConcreteLibraryMapping.DurabilityUnknownExposureMessage);
        int group = r.NtcEnvironment!.Value;
        return new(ConcreteLibraryMapping.DurabilityEnvironment(group), group, r.NtcCmin!.Value, r.NtcC0!.Value, r.NtcTable!.Value, r.NtcLifeExtra!.Value,
            r.NtcLowStrengthExtra!.Value, r.NtcQualityReduction!.Value, new CoverResult(r.Bond, r.Durability, r.Minimum, r.Nominal, []));
    }

    /// <summary>Shared outcome for requirements already resolved by the selected durability engine.</summary>
    public static (double Required, bool? Passed) AssessCover(double adopted, double materialMinimum, double? actualBarsMinimum)
    {
        var assessment = CoverAssessment.Evaluate(adopted, materialMinimum, actualBarsMinimum);
        return (assessment.Required, assessment.Passed);
    }

    /// <summary>Document values for the native wall detail service; reuse the instrumented material catalogue and requirement.</summary>
    internal static (DurabilityProfile Profile, LibraryCoverInput Input) WallCoverInput(string method, string code,
        double fck, CoverInput cover, bool plate, bool quality)
    {
        var exposure = Exposures().FirstOrDefault(e => e.Code == code)
            ?? throw new ArgumentException("Classe di esposizione non riconosciuta: " + code);
        var input = Input([code], fck, cover, plate, quality, MinimumStrength([exposure]));
        var profile = method switch { "EC2 2004" => DurabilityProfile.EN1992p11, "NTC + Circ. 2019" => DurabilityProfile.Ntc2018,
            _ => throw new ArgumentException("Metodo di copriferro non riconosciuto.") };
        return (profile, input);
    }

    // ------------------------------------------------------------------ conversioni
    static string[] Codes(Exposure[] values) => values.Select(e => e.Code).ToArray();

    internal static LibraryCoverInput Input(IEnumerable<string> codes, double fck, CoverInput p, bool plate, bool ntcQuality, double? pertinentCmin)
        => new(codes, fck, p.Life, p.StrengthReduction, p.Slab, p.Quality, p.Diameter, p.Aggregate, p.Deviation, p.Rough, p.Abrasion, p.Ground,
            plate, ntcQuality, pertinentCmin);

    // I rifiuti della libreria diventano quelli del legacy; un argomento nullo resta ArgumentNullException (registro F2-14).
    static T Library<T>(Func<T> call, string unknownExposure)
    {
        try { return call(); }
        catch (ArgumentException e) when (e is not ArgumentNullException) { throw ConcreteLibraryMapping.DurabilityError(e, unknownExposure); }
    }
}

/// <summary>
/// Motore legacy della durabilità agganciabile dalle prove (decisione D3 del 15/10/2026, modo b): le firme delle 12 operazioni di
/// <see cref="ConcreteDurabilityAdapter"/>, con gli stessi nomi e parametri. Qui solo le firme, nessuna implementazione né formula:
/// l'implementazione (nucleo legacy) e l'ambito che la aggancia stanno in tests/ANTHEA.Legacy, fuori dall'installer.
/// </summary>
internal interface ILegacyDurability
{
    ImmutableArray<Exposure> Exposures();
    int Severity(string code);
    int MinimumFck(string code);
    (double? Ratio, int? Cement) MixLimits(string code);
    string Label(int fck);
    void Validate(Exposure[] values);
    int MinimumStrength(Exposure[] exposures);
    (double? Ratio, int? Cement) Mix(Exposure[] active);
    double? Air(Exposure[] active, double dmax);
    int StructuralClass(Exposure e, double fck, CoverInput p);
    CoverResult Cover(Exposure[] values, double fck, CoverInput p);
    NtcCoverResult NtcCover(Exposure[] values, double fck, CoverInput p, bool plate, bool coverQuality, double? pertinentCmin);
}
