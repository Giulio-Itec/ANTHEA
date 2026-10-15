// Decisione D3 del 15/10/2026 (modo b): il selettore del motore di taglio e torsione esce dal prodotto (prima ConcreteShearTorsionAdapter.cs e
// ConcreteShearAnalysis.cs). Le prove e la cattura densa scelgono il motore con un ambito.
namespace Anthea.Calculations;

/// <summary>Motore delle verifiche di taglio e torsione della sezione c.a.: il nucleo legacy di X.Calculations o GPCChecker.Concrete.</summary>
public enum ShearTorsionEngine { Legacy, Library }

/// <summary>
/// Aggancio del taglio e della torsione legacy all'adattatore (<see cref="ConcreteShearTorsionAdapter"/>): per la durata dell'ambito taglio,
/// profilo resistente e torsione del flusso usano il motore indicato, come il parametro del motore prima della decisione D3.
/// Uso: <c>using (ShearTorsionEngines.Use(ShearTorsionEngine.Legacy)) { … }</c>.
/// </summary>
public static class ShearTorsionEngines
{
    /// <summary>Motore del prodotto, l'unico senza aggancio.</summary>
    public const ShearTorsionEngine Default = ShearTorsionEngine.Library;

    /// <summary>Legacy aggancia il nucleo storico; Library o null tolgono l'aggancio (libreria) fino alla fine dell'ambito.</summary>
    public static IDisposable Use(ShearTorsionEngine? engine)
        => EngineScope.Set<ILegacyShearTorsion>(engine == ShearTorsionEngine.Legacy ? LegacyShearTorsion.Instance : null);

    /// <summary>Esegue <paramref name="call"/> con il motore indicato (null: la libreria).</summary>
    public static T Run<T>(ShearTorsionEngine? engine, Func<T> call)
    {
        using (Use(engine)) return call();
    }

    // Le firme di prima della decisione D3, con il motore come ultimo argomento: il prodotto chiamato dentro l'ambito.
    public static Ntc2018Checks.ShearResult Shear(ConcreteCodeChecks.ShearInput p, ShearTorsionEngine? engine)
        => Run(engine, () => ConcreteShearTorsionAdapter.Shear(p));

    public static TorsionResult Torsion(TorsionInput p, double fck, double gammaC, ShearTorsionEngine? engine)
        => Run(engine, () => ConcreteShearTorsionAdapter.Torsion(p, fck, gammaC));

    public static TorsionGeometry TorsionGeometryOf(SezioneCA s, ShearTorsionEngine? engine)
        => Run(engine, () => ConcreteShearTorsionAdapter.TorsionGeometryOf(s));

    /// <summary>ConcreteShearAnalysis.Calculate con il motore indicato.</summary>
    public static ConcreteShearResult ShearAnalysis(System.Text.Json.Nodes.JsonObject input, System.Text.Json.Nodes.JsonObject settings,
        System.Text.Json.Nodes.JsonObject options, System.Text.Json.Nodes.JsonObject row, ShearTorsionEngine? engine)
        => Run(engine, () => ConcreteShearAnalysis.Calculate(input, settings, options, row));

    sealed class LegacyShearTorsion : ILegacyShearTorsion
    {
        internal static readonly LegacyShearTorsion Instance = new();
        public Ntc2018Checks.ShearResult Shear(ConcreteCodeChecks.ShearInput input) => LegacyConcreteCodeChecks.Shear(input);
        public TorsionResult Torsion(TorsionInput input) => new ConcreteTorsionCalculator().Calculate(input);
        public TorsionGeometry TorsionGeometry(SezioneCA section) => ConcreteTorsionCalculator.Geometry(section);
    }
}
