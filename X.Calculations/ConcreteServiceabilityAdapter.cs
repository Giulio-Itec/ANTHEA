using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Cracking;
using GPC.Checkers.Concrete.Results;
using GPC.Checkers.Concrete.Serviceability;
using GPC.Model.Materials;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace Anthea.Calculations;

/// <summary>Motore delle verifiche SLE della sezione c.a. (limiti tensionali e fessurazione): il nucleo legacy di X.Calculations o GPCChecker.Concrete.</summary>
public enum ServiceabilityEngine { Legacy, Library }

/// <summary>Voce della sonda degli adattatori: adattatore, operazione e motore usato.</summary>
public sealed record ServiceabilityProbeEntry(string Adapter, string Operation, string Engine);

/// <summary>
/// Sonda diagnostica degli adattatori di libreria (refactoring F2.7b, progetto F2.7 §6.2; servirà anche agli adattatori di F2.8): registra
/// adattatore, operazione e motore di ogni chiamata del flusso in cui è attiva. Il valore corrente è un <see cref="AsyncLocal{T}"/>: segue
/// <see cref="Task.Run(Action)"/> e <see cref="Parallel"/> (ExecutionContext) e in produzione è nullo, quindi nessuna registrazione e nessun
/// costo. Il registro è thread-safe. È l'unica eccezione dichiarata della prova 5l (stato statico) di tests/ConcreteLibraryAdapter.Checks.
/// Uso: <c>using var probe = ServiceabilityProbe.Start();</c>, poi <see cref="Entries"/>; Dispose ripristina la sonda precedente.
/// </summary>
public sealed class ServiceabilityProbe : IDisposable
{
    static readonly AsyncLocal<ServiceabilityProbe?> current = new();
    readonly ConcurrentQueue<ServiceabilityProbeEntry> entries = new();
    readonly ConcurrentQueue<object> results = new();
    readonly ServiceabilityProbe? previous;

    ServiceabilityProbe(ServiceabilityProbe? previous) => this.previous = previous;

    /// <summary>Attiva una sonda nuova per il flusso corrente.</summary>
    public static ServiceabilityProbe Start()
    {
        var probe = new ServiceabilityProbe(current.Value);
        current.Value = probe;
        return probe;
    }

    /// <summary>Chiamate registrate, nell'ordine di arrivo.</summary>
    public IReadOnlyList<ServiceabilityProbeEntry> Entries => entries.ToArray();

    /// <summary>Registra una chiamata se una sonda è attiva nel flusso; altrimenti non fa nulla.</summary>
    public static void Record(string adapter, string operation, Enum engine)
    {
        if (current.Value is { } probe) probe.entries.Enqueue(new(adapter, operation, engine.ToString()));
    }

    /// <summary>
    /// Risultati e rifiuti della libreria ricevuti dagli adattatori nel flusso, prima della mappatura, nell'ordine di arrivo (ciclo di prototipo di F2.7:
    /// copertura di codici, flag, argomenti, motivi ed esiti nella prova 5c).
    /// </summary>
    public IReadOnlyList<object> Results => results.ToArray();

    /// <summary>Registra un risultato o un rifiuto della libreria se una sonda è attiva nel flusso; altrimenti non fa nulla.</summary>
    public static void RecordResult(object result)
    {
        if (current.Value is { } probe) probe.results.Enqueue(result);
    }

    public void Dispose()
    {
        if (ReferenceEquals(current.Value, this)) current.Value = previous;
    }
}

/// <summary>
/// Adattatore delle verifiche SLE della sezione c.a. verso GPCChecker.Concrete (refactoring F2.7b, commit A4; progetto F2.7 §5 e §6): limiti
/// tensionali (<see cref="StressLimitCheck"/>) e fessurazione (<see cref="SectionCrackCheck"/>, <see cref="CrackRequirements"/>). Stesso contratto
/// del legacy (Ntc2018Checks.Cracking, ConcreteCodeChecks.CrackRequirement, LegacyServiceability): DTO, testi, traccia e rifiuti italiani identici,
/// tramite <see cref="ConcreteLibraryMapping"/>. Nessuna formula: i calcoli sono della libreria o del legacy.
/// Regole (progetto F2.7 §5, riga A4):
/// - tensioni: standard effettivo dell'analisi (coefficienti personalizzati compresi), mai <see cref="ConcreteLibraryMapping.StandardFor"/> né
///   <see cref="StressLimitCheck.NotApplicableReason"/> (W5, decisione U4 dell'utente: CS-TR34 conserva i limiti); <see cref="StressLimitCheck.Evaluate"/>
///   solo per le tre combinazioni SLE, per gli altri insiemi (ED, CURVA, LIMITE, BENCH) solo il limite dell'acciaio;
/// - fessurazione: norma per nome con <see cref="ConcreteLibraryMapping.StandardFor"/> (rifiuto del legacy per le norme escluse), opzioni
///   ValidateAtUse e Trace, rifiuti differiti (il dato non valido entra come NaN e, se la libreria lo rifiuta al punto d'uso, si rilancia
///   l'eccezione del legacy conservata), sezione non fessurata calcolata solo quando serve e con lo stesso motore.
/// Il legacy resta raggiungibile con <see cref="ServiceabilityEngine.Legacy"/> fino a F2.11.
/// </summary>
public static class ConcreteServiceabilityAdapter
{
    /// <summary>Nome dell'adattatore nella sonda.</summary>
    public const string ProbeName = "ConcreteServiceabilityAdapter";

    /// <summary>Interruttore del motore usato da ANTHEA quando il chiamante non ne indica uno: legacy fino al commit A8 di F2.7b
    /// (proprietà calcolata, senza stato).</summary>
    public static ServiceabilityEngine Default => ServiceabilityEngine.Legacy;

    // Nomi dei parametri della libreria rifiutati al punto d'uso (ArgumentOutOfRangeException.ParamName di SectionCrackInput e CrackRequirements).
    const string DesignLimitParameter = "designLimit", NominalCoverParameter = "nominalCover", CoverOverrideParameter = "coverOverride",
        SpacingOverrideParameter = "spacingOverride";

    // ------------------------------------------------------------------ limiti tensionali
    /// <summary>
    /// Tasso, stato e limiti tensionali SLE di uno stato tensionale già calcolato (come LegacyServiceability.StressLimits), con il marcatore
    /// del motore. <paramref name="standard"/> è la norma effettiva dell'analisi; <paramref name="thinCastingFactor"/> il fattore dei getti sottili.
    /// </summary>
    public static ServiceabilityStressLimits StressLimits(StressAnalysisResult result, string set, StandardModelCode2010 standard, ReinforcedConcreteSection section,
        double psiRebar, double psiTendon, double thinCastingFactor, ServiceabilityEngine? engine = null)
    {
        var chosen = engine ?? Default;
        var combination = ConcreteLibraryMapping.CombinationOf(set);
        ServiceabilityProbe.Record(ProbeName, combination is null ? "SteelLimit" : "StressLimits", chosen);
        if (chosen == ServiceabilityEngine.Legacy)
            return LegacyServiceability.StressLimits(result, set, standard, section, psiRebar, psiTendon, thinCastingFactor) with { Engine = chosen };
        // Limite dell'acciaio della prima barra per ogni stato, come il legacy (rilievo W6: in ANTHEA le barre ordinarie precedono i trefoli).
        double steel = StressLimitCheck.SteelLimit(standard, section.Rebars.First().RebarMaterial);
        if (combination is not ServiceabilityCombination c)
            return new(null, ConcreteLibraryMapping.StressStatus(null), null, steel) { Engine = chosen };
        StressLimitResult limits;
        try { limits = StressLimitCheck.Evaluate(result, c, thinCastingFactor); }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException) { throw ConcreteLibraryMapping.StressLimitError(e); }
        return new(limits.Ratio, ConcreteLibraryMapping.StressStatus(limits.Satisfied), limits.ConcreteLimit, steel) { Engine = chosen };
    }

    // ------------------------------------------------------------------ requisiti
    /// <summary>Requisito della fessurazione (testo del criterio e wlim), come ConcreteCodeChecks.CrackRequirement.</summary>
    public static (string Kind, double? Limit) CrackRequirement(string standard, string set, JsonObject options, ServiceabilityEngine? engine = null)
    {
        var chosen = engine ?? Default;
        ServiceabilityProbe.Record(ProbeName, "CrackRequirement", chosen);
        if (chosen == ServiceabilityEngine.Legacy) return ConcreteCodeChecks.CrackRequirement(standard, set, options);
        var (requirement, profile) = LibraryRequirement(standard, set, options);
        return (ConcreteLibraryMapping.CrackRequirementText(requirement, profile), requirement.Limit);
    }

    /// <summary>
    /// Vero se la fessurazione è da mostrare per l'insieme (come SleCheckScope.Cracking prima di F2.7): falso solo quando la norma non la richiede
    /// in questa combinazione. Totale: norme escluse e valori non validi danno vero, come oggi (prova 5i).
    /// </summary>
    public static bool CrackingRequired(string set, JsonNode workspace, ServiceabilityEngine? engine = null)
    {
        var chosen = engine ?? Default;
        ServiceabilityProbe.Record(ProbeName, "CrackingRequired", chosen);
        if (chosen == ServiceabilityEngine.Legacy) return LegacyServiceability.CrackingRequired(set, workspace);
        try
        {
            var (requirement, _) = LibraryRequirement(workspace.S("normativa", "NTC 2018"), set, workspace["sle"]?[set] as JsonObject ?? new());
            return requirement.Criterion != CrackCriterion.NotRequired;
        }
        catch (ArgumentException) { return true; }
    }

    static (CrackRequirement Requirement, CrackProfile Profile) LibraryRequirement(string standardName, string set, JsonObject options)
    {
        var profile = CrackProfiles.Resolve(ConcreteLibraryMapping.StandardFor(standardName));
        var combination = ConcreteLibraryMapping.RequireCombination(set);
        var deferred = new DeferredRejections();
        var (exposure, sensitive, designLimit) = RequirementData(profile, options, deferred);
        try { return (CrackRequirements.For(profile, combination, exposure, sensitive, designLimit, Options(profile, null)), profile); }
        catch (ArgumentOutOfRangeException e) when (deferred.For(e.ParamName) is { } legacy) { throw legacy; }
    }

    /// <summary>
    /// Esposizione, sensibilità e wlim di progetto come li legge il legacy: wlim solo per i profili il cui requisito lo usa (famiglia Eurocodice e
    /// Model Code 2010, <see cref="CrackProfiles.UsesDesignLimit"/>).
    /// </summary>
    static (string? Exposure, bool Sensitive, double? DesignLimit) RequirementData(CrackProfile profile, JsonObject options, DeferredRejections deferred)
    {
        string? exposure = ConcreteLibraryMapping.ExposureOf(options.S("esposizione"));
        bool sensitive = options.S("sensibilita") == "Sensibile";
        double? designLimit = null;
        if (CrackProfiles.UsesDesignLimit(profile) && options.S("limite_fessure").Trim() != "")
            designLimit = deferred.Read(DesignLimitParameter, () => options.Required("limite_fessure", strict: true));
        return (exposure, sensitive, designLimit);
    }

    /// <summary>Opzioni della verifica: validazione al punto d'uso, traccia e, per il profilo la cui hc,eff legge il copriferro (DIN,
    /// <see cref="CrackProfiles.EffectiveDepthReadsCover"/>), il copriferro della condizione.</summary>
    static SectionCrackOptions Options(CrackProfile profile, double? effectiveDepthCover)
    {
        var options = SectionCrackOptions.Default.WithValidateAtUse(true).WithTrace(true);
        return CrackProfiles.EffectiveDepthReadsCover(profile) ? options.WithEffectiveDepthCover(effectiveDepthCover) : options;
    }

    // ------------------------------------------------------------------ fessurazione
    /// <summary>
    /// Fessurazione di uno stato SLE (come Ntc2018Checks.Cracking): DTO, traccia, regioni, stati e rifiuti del legacy, con il marcatore del motore.
    /// </summary>
    public static Ntc2018Checks.CrackResult Cracking(CheckerSection section, CheckerStressState state, ActionPoint force, JsonObject input, JsonObject workspace,
        JsonObject options, string set, ServiceabilityEngine? engine = null)
    {
        var chosen = engine ?? Default;
        ServiceabilityProbe.Record(ProbeName, "Cracking", chosen);
        if (chosen == ServiceabilityEngine.Legacy) return Ntc2018Checks.Cracking(section, state, force, input, workspace, options, set) with { Engine = chosen };
        return LibraryCracking(section, state, force, input, workspace, options, set) with { Engine = chosen };
    }

    static Ntc2018Checks.CrackResult LibraryCracking(CheckerSection section, CheckerStressState state, ActionPoint force, JsonObject input, JsonObject workspace,
        JsonObject options, string set)
    {
        var native = state.Native;
        // Voci che ripetono gli ingressi, lette come il legacy (Ntc2018Checks.cs:60-67).
        var header = ConcreteLibraryMapping.CrackInputDetails(set, options.S("esposizione"), options.S("sensibilita"), options.S("durata"), options.S("aderenza"),
            native.LinearElasticAnalysis, options.S("trazione_cls"), options.S("assi", "Locali"), force.N, force.Mx, force.My, native.PsiRebar ?? 0,
            input.D("gamma_c"), input.D("gamma_s"));
        string code = workspace.S("normativa", "NTC 2018");
        var standard = ConcreteLibraryMapping.StandardFor(code);
        var profile = CrackProfiles.Resolve(standard);
        var combination = ConcreteLibraryMapping.RequireCombination(set);
        var deferred = new DeferredRejections();
        var (exposure, sensitive, designLimit) = RequirementData(profile, options, deferred);
        // Requisito prima dei dati della sezione, nell'ordine dei rifiuti del legacy (Ntc2018Checks.cs:75): un wlim non valido si rifiuta qui. Il
        // requisito della mappatura è poi quello del risultato, che la libreria con la traccia riporta in ogni ramo (ciclo di prototipo di F2.7).
        try { _ = CrackRequirements.For(profile, combination, exposure, sensitive, designLimit, Options(profile, null)); }
        catch (ArgumentOutOfRangeException e) when (deferred.For(e.ParamName) is { } legacy) { throw legacy; }
        var geometry = section.Geometry;
        var concrete = (ConcreteMaterialEuropeanCommon)section.Section.ConcreteMaterial;
        double es = section.Section.Rebars.First().RebarMaterial.E;
        // Copriferro della formula: override per la fessurazione oppure copriferro netto + Ø staffa (Ntc2018Checks.cs:222), letti solo se servono.
        bool assignedCover = options.S("copriferro_fessure").Trim() != "";
        double? coverOverride = assignedCover ? deferred.Read(CoverOverrideParameter, () => options.Required("copriferro_fessure")) : null;
        double nominalCover = assignedCover ? double.NaN
            : deferred.Read(NominalCoverParameter, () => input.Required("cover_mm") + (input.S("staffe_presenti", "Sì") == "No" ? 0 : input.Required("transverse_bar_diameter_mm")));
        double? spacingOverride = options.S("spaziatura_fessure").Trim() == "" ? null
            : deferred.Read(SpacingOverrideParameter, () => options.Required("spaziatura_fessure", strict: true));
        // Copriferro della condizione DIN di hc,eff (ConcreteCodeChecks.EffectiveCrackDepth), con i ripieghi della sezione, solo se il profilo lo legge.
        double? effectiveDepthCover = CrackProfiles.EffectiveDepthReadsCover(profile)
            ? geometry.Input.D("cover_mm") + (geometry.Input.S("staffe_presenti", "Sì") == "Sì" ? geometry.Input.D("transverse_bar_diameter_mm") : 0) : null;
        // Sezione non fessurata per decompressione e formazione (Ntc2018Checks.cs:79-82): solo quando la libreria la chiede, con lo stesso motore.
        Exception? callbackError = null;
        double Uncracked()
        {
            try
            {
                var uncracked = (JsonObject)options.DeepClone(); uncracked["modello"] = "Lineare"; uncracked["trazione_cls"] = "Sì";
                var check = new CheckerSection(section.Model, input, workspace, uncracked, "SLU", ServiceabilityEngine.Library).Stress(force, "SLE_FREQ");
                return check.Native.GetConcreteVerticesTension(check.Native.PsiRebar ?? 0).Max(p => p.tension);
            }
            catch (Exception e) { callbackError = e; throw; }
        }
        SectionCrackResult result;
        try
        {
            var crackGeometry = ConcreteLibraryMapping.CrackGeometry(geometry);
            var crackOptions = Options(profile, effectiveDepthCover).WithAnalysisContext(concrete.E, native.PsiRebar ?? 0);
            var crackInput = new SectionCrackInput(standard, combination, exposure, sensitive, designLimit, crackGeometry, native.StrainPlane,
                state.tensioni_barre.Take(geometry.Bars.Count), native.LinearElasticAnalysis, options.S("trazione_cls") == "Sì", workspace.Array("trefoli").Any(),
                es, concrete.Ecm, concrete.Fctm, options.S("durata", "Lunga") == "Breve", options.S("aderenza", "Migliorata") == "Migliorata",
                nominalCover, coverOverride, spacingOverride, Uncracked, Ntc2018Checks.NtcK2FromCompressedBars, crackOptions);
            result = SectionCrackCheck.Evaluate(crackInput);
        }
        catch (ArgumentException e) when (!ReferenceEquals(e, callbackError))
        {
            ServiceabilityProbe.RecordResult(e);
            if (e is ArgumentOutOfRangeException && deferred.For(e.ParamName) is { } legacy) throw legacy;
            throw ConcreteLibraryMapping.CrackError(e, profile, Ntc2018Checks.NtcK2FromCompressedBars);
        }
        ServiceabilityProbe.RecordResult(result);
        return ConcreteLibraryMapping.ToCrackResult(result, profile, header, geometry.Holes.Count > 0);
    }

    /// <summary>
    /// Rifiuti differiti (progetto F2.7 §4.2 K2): un dato letto con le regole del legacy che le rifiuta entra nella libreria come NaN; se la
    /// libreria arriva al punto d'uso e lo rifiuta, si rilancia l'eccezione del legacy, così testo e posizione del rifiuto sono quelli di oggi.
    /// </summary>
    sealed class DeferredRejections
    {
        readonly Dictionary<string, ArgumentException> saved = new(StringComparer.Ordinal);

        public double Read(string parameter, Func<double> read)
        {
            try { return read(); }
            catch (ArgumentException e) { saved[parameter] = e; return double.NaN; }
        }

        public ArgumentException? For(string? parameter) => parameter is not null && saved.TryGetValue(parameter, out var e) ? e : null;
    }
}
