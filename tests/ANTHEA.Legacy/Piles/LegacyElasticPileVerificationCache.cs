// Spostato da X.Calculations/ElasticPileVerificationCache.cs (commit 4144fe9e) con la decisione D3 del 15/10/2026: SectionCount e
// ResistanceCount (T21) non avevano usi, nemmeno nelle prove. Codice storico conservato per le prove, fuori dall'installer.
namespace Anthea.Calculations;

/// <summary>
/// Conteggi della cache dei pali elastici, prima proprietà pubbliche di <see cref="ElasticPileVerificationCache"/>: stesse letture del
/// dizionario delle sezioni, come metodi di estensione (<c>cache.SectionCount()</c>, <c>cache.ResistanceCount()</c>).
/// </summary>
public static class LegacyElasticPileVerificationCache
{
    public static int SectionCount(this ElasticPileVerificationCache cache)=>cache.sections.Count;
    public static int ResistanceCount(this ElasticPileVerificationCache cache)=>cache.sections.Values.Sum(t=>t.Count);
}
