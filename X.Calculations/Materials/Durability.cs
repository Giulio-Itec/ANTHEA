using System.Collections.Immutable;
using Anthea.Calculations;

namespace Materiali;

/// <summary>
/// Facciata della durabilità EC2 e del catalogo delle esposizioni (refactoring F2.9): stessi nomi e firme di prima, più il motore
/// facoltativo finale; nessun calcolo, delega a <see cref="ConcreteDurabilityAdapter"/> (motore predefinito senza argomento).
/// Il catalogo è immutabile (prima un array pubblico).
/// </summary>
public static class Durability
{
    public static ImmutableArray<Exposure> Exposures => ConcreteDurabilityAdapter.Exposures();
    // F2.9-D5: senza chiamanti di produzione, resta nel nucleo legacy fino a F2.11 (elenco ammesso della prova 11i).
    public static string Strength(int fck) => DurabilityLegacy.Durability.Strength(fck);
    public static void ValidateExposure(Exposure[] values, DurabilityEngine? engine = null) => ConcreteDurabilityAdapter.Validate(values, engine);
    public static int StructuralClass(Exposure e, double fck, CoverInput p, DurabilityEngine? engine = null) => ConcreteDurabilityAdapter.StructuralClass(e, fck, p, engine);
    public static CoverResult Cover(Exposure[] values, double fck, CoverInput p, DurabilityEngine? engine = null) => ConcreteDurabilityAdapter.Cover(values, fck, p, engine);
    // F2.9-D5: usata solo dai controlli di riferimento (supporto/test/Shared/DurabilityReferenceChecks.cs); resta nel nucleo legacy fino a F2.11.
    public static double EffectiveWater(double total, double absorbed) => DurabilityLegacy.Durability.EffectiveWater(total, absorbed);
}
