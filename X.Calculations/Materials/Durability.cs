using System.Collections.Immutable;

namespace Materiali;

/// <summary>
/// Facciata della durabilità EC2 e del catalogo delle esposizioni (refactoring F2.9): stessi nomi e firme di prima, nessun calcolo.
/// I corpi sono in <see cref="DurabilityLegacy.Durability"/>. Il catalogo è immutabile (prima un array pubblico).
/// </summary>
public static class Durability
{
    public static ImmutableArray<Exposure> Exposures => DurabilityLegacy.Durability.Exposures;
    public static string Strength(int fck) => DurabilityLegacy.Durability.Strength(fck);
    public static void ValidateExposure(Exposure[] values) => DurabilityLegacy.Durability.ValidateExposure(values);
    public static int StructuralClass(Exposure e, double fck, CoverInput p) => DurabilityLegacy.Durability.StructuralClass(e, fck, p);
    public static CoverResult Cover(Exposure[] values, double fck, CoverInput p) => DurabilityLegacy.Durability.Cover(values, fck, p);
    public static double EffectiveWater(double total, double absorbed) => DurabilityLegacy.Durability.EffectiveWater(total, absorbed);
}
