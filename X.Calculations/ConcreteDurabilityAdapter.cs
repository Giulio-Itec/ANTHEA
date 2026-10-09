using System.Collections.Concurrent;
using System.Collections.Immutable;
using GPC.Checkers.Concrete.Durability;
using Materiali;
using CoverInput = Materiali.CoverInput;
using CoverLine = Materiali.CoverLine;
using CoverResult = Materiali.CoverResult;
using LibraryCoverInput = GPC.Checkers.Concrete.Durability.CoverInput;

namespace Anthea.Calculations;

/// <summary>Motore della durabilità e dei copriferri: il nucleo legacy di X.Calculations (DurabilityLegacy) o GPCChecker.Concrete.</summary>
public enum DurabilityEngine { Legacy, Library }

/// <summary>
/// Adattatore della durabilità e dei copriferri verso GPC.Checkers.Concrete.Durability (refactoring F2.9, docs/refactoring/piano.md).
/// Stesso contratto del legacy (record di Materials/DurabilityContracts.cs, testi e rifiuti italiani identici tramite
/// <see cref="ConcreteLibraryMapping"/>); con lo stesso ingresso i due motori danno gli stessi numeri bit per bit (stesse espressioni nello
/// stesso ordine). Regole: riceve valori, mai nodi JSON; nel ramo della libreria passa solo i codici delle esposizioni (gli altri campi
/// dei record Exposure non si usano: la libreria li prende dal proprio catalogo) e la Cmin pertinente come la riceve; profilo EN1992p11
/// per il copriferro EC2 della scheda Materiali, Ntc2018 per quello NTC. Nessuna formula né costante normativa (prova 11a).
/// Le facciate Materiali.Durability, NtcCover, MinimumConcrete e AtecapMix delegano qui. Il motore legacy resta raggiungibile con
/// <see cref="DurabilityEngine.Legacy"/> (confronti, cattura densa con '--motore-durabilita legacy') fino alla decisione di F2.11.
/// </summary>
public static class ConcreteDurabilityAdapter
{
    /// <summary>Interruttore unico del motore usato quando il chiamante non ne indica uno (F2.9-D8): la libreria dal passo F2.9 E5
    /// (uscite identiche al legacy bit per bit, registro F2-13, misurate contro la cattura densa F2-pre-m4-v2 e la baseline headless B6,
    /// catturate con il legacy); il legacy resta raggiungibile con <see cref="DurabilityEngine.Legacy"/> fino a F2.11.</summary>
    public static DurabilityEngine Default => DurabilityEngine.Library;

    // ------------------------------------------------------------------ sonda (F2.9-D14)
    /// <summary>Voce della sonda: adattatore, operazione, motore (forma della sonda generalizzata chiesta da F2.8 §13.2).</summary>
    public sealed record ProbeEntry(string Adapter, string Operation, DurabilityEngine Engine);

    /// <summary>Registro diagnostico delle chiamate dell'adattatore, thread-safe; segue Task.Run e Parallel.ForEach (ExecutionContext).</summary>
    public sealed class Probe
    {
        readonly ConcurrentQueue<ProbeEntry> entries = new();
        internal void Add(ProbeEntry entry) => entries.Enqueue(entry);
        public IReadOnlyList<ProbeEntry> Entries => entries.ToArray();
    }

    // Diagnostico, nullo in produzione: eccezione dichiarata della prova 11g. Da unificare con la sonda di F2.7 (A4) in F2.11.
    static readonly AsyncLocal<Probe?> CurrentProbe = new();

    /// <summary>Avvia una sonda nel contesto di esecuzione corrente (solo prove e diagnosi).</summary>
    public static Probe StartProbe() { var probe = new Probe(); CurrentProbe.Value = probe; return probe; }
    public static void StopProbe() => CurrentProbe.Value = null;
    static void Record(string operation, DurabilityEngine engine) => CurrentProbe.Value?.Add(new(nameof(ConcreteDurabilityAdapter), operation, engine));

    static bool UseLegacy(DurabilityEngine? engine) => (engine ?? Default) == DurabilityEngine.Legacy;

    // ------------------------------------------------------------------ catalogo e funzioni per codice
    /// <summary>Catalogo delle 18 esposizioni della scheda Materiali, come Durability.Exposures.</summary>
    public static ImmutableArray<Exposure> Exposures(DurabilityEngine? engine = null)
    {
        if (UseLegacy(engine)) { Record(nameof(Exposures), DurabilityEngine.Legacy); return DurabilityLegacy.Durability.Exposures; }
        Record(nameof(Exposures), DurabilityEngine.Library);
        return ExposureClasses.All.Select(ConcreteLibraryMapping.DurabilityExposure).ToImmutableArray();
    }

    /// <summary>Gruppo ambientale NTC di un'esposizione, come NtcCover.Severity.</summary>
    public static int Severity(string code, DurabilityEngine? engine = null)
    {
        if (UseLegacy(engine)) { Record(nameof(Severity), DurabilityEngine.Legacy); return DurabilityLegacy.NtcCover.Severity(code); }
        Record(nameof(Severity), DurabilityEngine.Library);
        return Library(() => ExposureClasses.Get(code).NtcEnvironment, ConcreteLibraryMapping.DurabilityUnknownExposureMessage);
    }

    /// <summary>Classe minima UNI 11104 di un'esposizione (fck, MPa), come MinimumConcrete.Fck.</summary>
    public static int MinimumFck(string code, DurabilityEngine? engine = null)
    {
        if (UseLegacy(engine)) { Record(nameof(MinimumFck), DurabilityEngine.Legacy); return DurabilityLegacy.MinimumConcrete.Fck(code); }
        Record(nameof(MinimumFck), DurabilityEngine.Library);
        return Library(() => ExposureClasses.Get(code).Uni11104MinStrength, ConcreteLibraryMapping.DurabilityUnknownClassMessage);
    }

    /// <summary>Limiti di composizione UNI 11104 di un'esposizione (A/C massimo, cemento minimo), come AtecapMix.Limits.</summary>
    public static (double? Ratio, int? Cement) MixLimits(string code, DurabilityEngine? engine = null)
    {
        if (UseLegacy(engine)) { Record(nameof(MixLimits), DurabilityEngine.Legacy); return DurabilityLegacy.AtecapMix.Limits(code); }
        Record(nameof(MixLimits), DurabilityEngine.Library);
        return Library(() => { var e = ExposureClasses.Get(code); return (e.Uni11104MaxWaterCement, e.Uni11104MinCement); }, ConcreteLibraryMapping.DurabilityUnknownExposureMessage);
    }

    /// <summary>Etichetta della classe minima, come MinimumConcrete.Label (libreria: F2.9-D13).</summary>
    public static string Label(int fck, DurabilityEngine? engine = null)
    {
        if (UseLegacy(engine)) { Record(nameof(Label), DurabilityEngine.Legacy); return DurabilityLegacy.MinimumConcrete.Label(fck); }
        Record(nameof(Label), DurabilityEngine.Library);
        return ConcreteLibraryMapping.DurabilityStrengthLabel(fck);
    }

    // ------------------------------------------------------------------ combinazioni di esposizioni
    /// <summary>Almeno una esposizione, X0 non combinabile: come Durability.ValidateExposure.</summary>
    public static void Validate(Exposure[] values, DurabilityEngine? engine = null)
    {
        if (UseLegacy(engine)) { Record(nameof(Validate), DurabilityEngine.Legacy); DurabilityLegacy.Durability.ValidateExposure(values); return; }
        Record(nameof(Validate), DurabilityEngine.Library);
        Library(() => ExposureClasses.Resolve(Codes(values)), ConcreteLibraryMapping.DurabilityUnknownExposureMessage);
    }

    /// <summary>Classe minima UNI 11104 della combinazione, come MinimumConcrete.Required.</summary>
    public static int MinimumStrength(Exposure[] exposures, DurabilityEngine? engine = null)
    {
        if (UseLegacy(engine)) { Record(nameof(MinimumStrength), DurabilityEngine.Legacy); return DurabilityLegacy.MinimumConcrete.Required(exposures); }
        Record(nameof(MinimumStrength), DurabilityEngine.Library);
        return Library(() => ExposureClasses.Uni11104MinimumStrength(Codes(exposures)), ConcreteLibraryMapping.DurabilityUnknownClassMessage);
    }

    /// <summary>Limiti di composizione UNI 11104 della combinazione (il minore A/C e il maggiore cemento), come AtecapMix.Required.</summary>
    public static (double? Ratio, int? Cement) Mix(Exposure[] active, DurabilityEngine? engine = null)
    {
        if (UseLegacy(engine)) { Record(nameof(Mix), DurabilityEngine.Legacy); return DurabilityLegacy.AtecapMix.Required(active); }
        Record(nameof(Mix), DurabilityEngine.Library);
        return Library(() => { var limits = ExposureClasses.Uni11104Mix(Codes(active)); return (limits.Item1, limits.Item2); }, ConcreteLibraryMapping.DurabilityUnknownExposureMessage);
    }

    /// <summary>Aria minima per XF2-XF4 secondo Dmax, come AtecapMix.Air.</summary>
    public static double? Air(Exposure[] active, double dmax, DurabilityEngine? engine = null)
    {
        if (UseLegacy(engine)) { Record(nameof(Air), DurabilityEngine.Legacy); return DurabilityLegacy.AtecapMix.Air(active, dmax); }
        Record(nameof(Air), DurabilityEngine.Library);
        return Library(() => ExposureClasses.Uni11104Air(Codes(active), dmax), ConcreteLibraryMapping.DurabilityUnknownExposureMessage);
    }

    // ------------------------------------------------------------------ copriferri
    /// <summary>Classe strutturale del prospetto 4.3N di un'esposizione, come Durability.StructuralClass.</summary>
    public static int StructuralClass(Exposure e, double fck, CoverInput p, DurabilityEngine? engine = null)
    {
        if (UseLegacy(engine)) { Record(nameof(StructuralClass), DurabilityEngine.Legacy); return DurabilityLegacy.Durability.StructuralClass(e, fck, p); }
        Record(nameof(StructuralClass), DurabilityEngine.Library);
        return Library(() => CoverRequirements.StructuralClass(ExposureClasses.Get(e.Code), Input([e.Code], fck, p, false, false, null)),
            ConcreteLibraryMapping.DurabilityUnknownExposureMessage);
    }

    /// <summary>Copriferro EC2 della scheda Materiali (EN 1992-1-1:2004, prospetti 4.3N e 4.4N), come Durability.Cover.</summary>
    public static CoverResult Cover(Exposure[] values, double fck, CoverInput p, DurabilityEngine? engine = null)
    {
        if (UseLegacy(engine)) { Record(nameof(Cover), DurabilityEngine.Legacy); return DurabilityLegacy.Durability.Cover(values, fck, p); }
        Record(nameof(Cover), DurabilityEngine.Library);
        var r = Library(() => CoverRequirements.Calculate(DurabilityProfile.EN1992p11, Input(Codes(values), fck, p, false, false, null)),
            ConcreteLibraryMapping.DurabilityUnknownExposureMessage);
        return new(r.Bond, r.Durability, r.Minimum, r.Nominal, r.Lines.Select(l => new CoverLine(l.Exposure, l.StructuralClass!.Value, l.Durability)).ToArray());
    }

    /// <summary>Copriferro NTC 2018 con la Circolare 2019, come NtcCover.Calculate.</summary>
    public static NtcCoverResult NtcCover(Exposure[] values, double fck, CoverInput p, bool plate, bool coverQuality, double? pertinentCmin = null, DurabilityEngine? engine = null)
    {
        if (UseLegacy(engine)) { Record(nameof(NtcCover), DurabilityEngine.Legacy); return DurabilityLegacy.NtcCover.Calculate(values, fck, p, plate, coverQuality, pertinentCmin); }
        Record(nameof(NtcCover), DurabilityEngine.Library);
        var r = Library(() => CoverRequirements.Calculate(DurabilityProfile.Ntc2018, Input(Codes(values), fck, p, plate, coverQuality, pertinentCmin)),
            ConcreteLibraryMapping.DurabilityUnknownExposureMessage);
        int group = r.NtcEnvironment!.Value;
        return new(ConcreteLibraryMapping.DurabilityEnvironment(group), group, r.NtcCmin!.Value, r.NtcC0!.Value, r.NtcTable!.Value, r.NtcLifeExtra!.Value,
            r.NtcLowStrengthExtra!.Value, r.NtcQualityReduction!.Value, new CoverResult(r.Bond, r.Durability, r.Minimum, r.Nominal, []));
    }

    // ------------------------------------------------------------------ conversioni
    static string[] Codes(Exposure[] values) => values.Select(e => e.Code).ToArray();

    static LibraryCoverInput Input(IEnumerable<string> codes, double fck, CoverInput p, bool plate, bool ntcQuality, double? pertinentCmin)
        => new(codes, fck, p.Life, p.StrengthReduction, p.Slab, p.Quality, p.Diameter, p.Aggregate, p.Deviation, p.Rough, p.Abrasion, p.Ground,
            plate, ntcQuality, pertinentCmin);

    // I rifiuti della libreria diventano quelli del legacy; un argomento nullo resta ArgumentNullException (registro F2-14).
    static T Library<T>(Func<T> call, string unknownExposure)
    {
        try { return call(); }
        catch (ArgumentException e) when (e is not ArgumentNullException) { throw ConcreteLibraryMapping.DurabilityError(e, unknownExposure); }
    }
}
