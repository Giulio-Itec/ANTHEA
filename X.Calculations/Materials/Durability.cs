using System.Collections.Immutable;
using Anthea.Calculations;

namespace Materiali;

/// <summary>
/// Facciata della durabilità EC2 e del catalogo delle esposizioni (refactoring F2.9): stessi nomi e firme di prima; nessun calcolo,
/// delega a <see cref="ConcreteDurabilityAdapter"/>. Il motore facoltativo finale (F2.9-D2) è uscito dal prodotto con la decisione D3
/// del 15/10/2026: le prove agganciano il legacy da tests/ANTHEA.Legacy. Il catalogo è immutabile (prima un array pubblico).
/// Strength ed EffectiveWater, senza chiamanti di produzione (F2.9-D5), sono passate con la stessa decisione in
/// tests/ANTHEA.Legacy (LegacyDurability), insieme al nucleo legacy. ValidateExposure, usata solo dalle prove, vi è passata con la
/// decisione E5 del 15/10/2026 (F2.11c); il prodotto valida con <see cref="ConcreteDurabilityAdapter.Validate"/>.
/// </summary>
public static class Durability
{
    public static ImmutableArray<Exposure> Exposures => ConcreteDurabilityAdapter.Exposures();
    public static int StructuralClass(Exposure e, double fck, CoverInput p) => ConcreteDurabilityAdapter.StructuralClass(e, fck, p);
    public static CoverResult Cover(Exposure[] values, double fck, CoverInput p) => ConcreteDurabilityAdapter.Cover(values, fck, p);
}
