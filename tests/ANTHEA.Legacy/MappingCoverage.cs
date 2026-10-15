// Spostato da X.Calculations/ConcreteLibraryMapping.Serviceability.cs, ConcreteLibraryMapping.Resistance.cs e ConcreteLibraryMapping.cs
// (commit 4144fe9e) con la decisione D3 del 15/10/2026: viste delle tabelle della mappatura e conversione usate solo dalle prove 5a e 12 e dal
// controllo delle unità di tests/ConcreteLibraryAdapter.Checks. Le tabelle restano nel prodotto (internal); qui le stesse espressioni di prima.
using System.Collections.Frozen;
using GPC.Checkers.Concrete.Cracking;

namespace Anthea.Calculations;

/// <summary>Copertura dello strato di mappatura per le prove: valori della libreria che la mappatura traduce.</summary>
public static class MappingCoverage
{
    /// <summary>Criteri della libreria tradotti da <see cref="ConcreteLibraryMapping.CrackRequirementText"/> (prova 5a).</summary>
    public static IReadOnlyCollection<CrackCriterion> TranslatedCriteria { get; } = new[]
    {
        CrackCriterion.NotRequired, CrackCriterion.ExposureRequired, CrackCriterion.DesignLimitRequired, CrackCriterion.Decompression,
        CrackCriterion.CrackFormation, CrackCriterion.CrackWidth
    }.ToFrozenSet();

    /// <summary>Profili con un nome in ANTHEA (prova 5a).</summary>
    public static IReadOnlyCollection<CrackProfile> TranslatedProfiles => ConcreteLibraryMapping.ProfileNames.Keys;

    /// <summary>Esiti della libreria tradotti (prova 5a): stati fissi, superfici interne, requisiti, verifiche con ampiezza e rami composti.</summary>
    public static IReadOnlyCollection<CrackOutcome> TranslatedOutcomes { get; } = ConcreteLibraryMapping.OuterStops.Keys.Concat(ConcreteLibraryMapping.InnerMissing.Keys)
        .Concat(new[] { CrackOutcome.Evaluated, CrackOutcome.NotRequired, CrackOutcome.MissingExposure, CrackOutcome.MissingDesignLimit, CrackOutcome.NoEffectiveArea })
        .ToFrozenSet();

    /// <summary>Motivi della libreria tradotti (prova 5a); None non ha un testo proprio.</summary>
    public static IReadOnlyCollection<CrackReason> TranslatedReasons { get; } = ConcreteLibraryMapping.NoEffectiveAreaStops.Keys
        .Concat(ConcreteLibraryMapping.EvaluatedReasons.Keys).Append(CrackReason.None).ToFrozenSet();

    /// <summary>Codici di rifiuto tradotti (prova 5a).</summary>
    public static IReadOnlyCollection<string> TranslatedRejections => ConcreteLibraryMapping.CrackRejections.Keys;

    /// <summary>Codici della traccia tradotti (prova 5a).</summary>
    public static IReadOnlyCollection<string> TranslatedTraceCodes => ConcreteLibraryMapping.TraceTexts.Keys;

    /// <summary>Testi della libreria tradotti (per le prove della mappatura del punto resistente).</summary>
    public static IReadOnlyCollection<string> ResistanceRejectionTexts => ConcreteLibraryMapping.ResistanceRejections.Keys;

    /// <summary>Conversione inversa di ConcreteLibraryMapping.MillimetresFromMetres, che il prodotto non usa (controllo dei confini delle unità).</summary>
    public static double MetresFromMillimetres(double millimetres) => millimetres / ConcreteLibraryMapping.MillimetresPerMetre;
}
