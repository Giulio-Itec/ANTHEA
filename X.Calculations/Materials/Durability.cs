using System.Collections.Immutable;
using Anthea.Calculations;

namespace Materiali;

/// <summary>
/// Facciata della durabilità EC2 e del catalogo delle esposizioni (refactoring F2.9): stessi nomi e firme di prima; nessun calcolo,
/// delega a <see cref="ConcreteDurabilityAdapter"/>. Il motore facoltativo finale (F2.9-D2) è uscito dal prodotto con la decisione D3
/// del 15/10/2026: le prove agganciano il legacy da tests/ANTHEA.Legacy. Il catalogo è immutabile (prima un array pubblico).
/// </summary>
public static class Durability
{
    public static ImmutableArray<Exposure> Exposures => ConcreteDurabilityAdapter.Exposures();
    // F2.9-D5: senza chiamanti di produzione, resta nel nucleo legacy fino a F2.11 (elenco ammesso della prova 11i).
    public static string Strength(int fck) => DurabilityLegacy.Durability.Strength(fck);
    public static void ValidateExposure(Exposure[] values) => ConcreteDurabilityAdapter.Validate(values);
    public static int StructuralClass(Exposure e, double fck, CoverInput p) => ConcreteDurabilityAdapter.StructuralClass(e, fck, p);
    public static CoverResult Cover(Exposure[] values, double fck, CoverInput p) => ConcreteDurabilityAdapter.Cover(values, fck, p);
    // F2.9-D5: usata solo dai controlli di riferimento (supporto/test/Shared/DurabilityReferenceChecks.cs); resta nel nucleo legacy fino a F2.11.
    public static double EffectiveWater(double total, double absorbed) => DurabilityLegacy.Durability.EffectiveWater(total, absorbed);
}
