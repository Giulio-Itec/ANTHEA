// Decisione D3 del 15/10/2026 (modo b): il selettore del motore SLE esce dal prodotto (prima ConcreteServiceabilityAdapter.cs, CheckerSection,
// ConcreteAnalysisSession, CalculationService, ConcreteDesignOptions, RetainingWall). Le prove scelgono il motore con un ambito.
using System.Text.Json.Nodes;
using GPC.Checkers.Concrete.Results;
using GPC.Model.Sections.Concrete;
using GPC.Model.Standards;

namespace Anthea.Calculations;

/// <summary>Motore delle verifiche SLE della sezione c.a. (limiti tensionali e fessurazione): il nucleo legacy di X.Calculations o GPCChecker.Concrete.</summary>
public enum ServiceabilityEngine { Legacy, Library }

/// <summary>
/// Aggancio del motore SLE legacy all'adattatore (<see cref="ConcreteServiceabilityAdapter"/>): per la durata dell'ambito ogni verifica SLE del
/// flusso usa il motore indicato, come faceva il parametro del motore prima della decisione D3.
/// Uso: <c>using (ServiceabilityEngines.Use(ServiceabilityEngine.Legacy)) { … }</c>.
/// </summary>
public static class ServiceabilityEngines
{
    /// <summary>Motore del prodotto, l'unico senza aggancio.</summary>
    public const ServiceabilityEngine Default = ServiceabilityEngine.Library;

    /// <summary>Legacy aggancia il nucleo storico; Library o null tolgono l'aggancio (libreria) fino alla fine dell'ambito.</summary>
    public static IDisposable Use(ServiceabilityEngine? engine)
        => EngineScope.Set<ILegacyServiceability>(engine == ServiceabilityEngine.Legacy ? LegacyServiceabilityEngine.Instance : null);

    /// <summary>Motore dell'aggancio corrente («Legacy» o «Library», come nella sonda).</summary>
    public static ServiceabilityEngine Current => EngineHook.Get<ILegacyServiceability>() is null ? ServiceabilityEngine.Library : ServiceabilityEngine.Legacy;

    /// <summary>
    /// Motore SLE di un modulo di CalculationService come prima della decisione D3 ('--motore-sle' delle catture): il motore arrivava solo
    /// alla sezione c.a. (str_palo) e ai muri, dove un motore esplicito, anche Library, portava le sezioni in c.a. sul percorso storico.
    /// Senza motore, o per gli altri moduli, nessun aggancio.
    /// </summary>
    public static IDisposable ForModule(string module, ServiceabilityEngine? engine)
        => engine is null ? EngineScope.Combine() : module == RetainingWall.Module ? EngineScope.Combine(LegacyRetainingWall.UseExplicitServiceability(), Use(engine))
            : module == "str_palo" ? Use(engine) : EngineScope.Combine();

    /// <summary>Esegue <paramref name="call"/> con il motore indicato (null: la libreria).</summary>
    public static T Run<T>(ServiceabilityEngine? engine, Func<T> call)
    {
        using (Use(engine)) return call();
    }

    // Le firme di prima della decisione D3, con il motore come ultimo argomento: l'adattatore del prodotto chiamato dentro l'ambito.
    public static ServiceabilityStressLimits StressLimits(StressAnalysisResult result, string set, StandardModelCode2010 standard, ReinforcedConcreteSection section,
        double psiRebar, double psiTendon, double thinCastingFactor, ServiceabilityEngine? engine)
        => Run(engine, () => ConcreteServiceabilityAdapter.StressLimits(result, set, standard, section, psiRebar, psiTendon, thinCastingFactor));

    public static (string Kind, double? Limit) CrackRequirement(string standard, string set, JsonObject options, ServiceabilityEngine? engine)
        => Run(engine, () => ConcreteServiceabilityAdapter.CrackRequirement(standard, set, options));

    public static bool CrackingRequired(string set, JsonNode workspace, ServiceabilityEngine? engine)
        => Run(engine, () => ConcreteServiceabilityAdapter.CrackingRequired(set, workspace));

    public static Ntc2018Checks.CrackResult Cracking(CheckerSection section, CheckerStressState state, ActionPoint force, JsonObject input, JsonObject workspace,
        JsonObject options, string set, ServiceabilityEngine? engine)
        => Run(engine, () => ConcreteServiceabilityAdapter.Cracking(section, state, force, input, workspace, options, set));

    /// <summary>Stato tensionale con i limiti SLE del motore indicato (prima: CheckerSection costruita con il motore).</summary>
    public static CheckerStressState Stress(CheckerSection section, ActionPoint action, string set, ServiceabilityEngine? engine)
        => Run(engine, () => section.Stress(action, set));

    sealed class LegacyServiceabilityEngine : ILegacyServiceability
    {
        internal static readonly LegacyServiceabilityEngine Instance = new();

        public ServiceabilityStressLimits StressLimits(StressAnalysisResult result, string set, StandardModelCode2010 standard, ReinforcedConcreteSection section,
            double psiRebar, double psiTendon, double thinCastingFactor)
            => LegacyServiceability.StressLimits(result, set, standard, section, psiRebar, psiTendon, thinCastingFactor);

        public (string Kind, double? Limit) CrackRequirement(string standard, string set, JsonObject options)
            => LegacyConcreteCodeChecks.CrackRequirement(standard, set, options);

        public bool CrackingRequired(string set, JsonNode workspace) => LegacyServiceability.CrackingRequired(set, workspace);

        public Ntc2018Checks.CrackResult Cracking(CheckerSection section, CheckerStressState state, ActionPoint force, JsonObject input, JsonObject workspace,
            JsonObject options, string set)
            => LegacyNtc2018Checks.Cracking(section, state, force, input, workspace, options, set);
    }
}
