using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using GPC.Checkers.Concrete.Durability;
using Exposure = Materiali.Exposure;

namespace Anthea.Calculations;

/// <summary>
/// Strato di mappatura della durabilità (refactoring F2.9, docs/refactoring/piano.md): testi italiani di ANTHEA per il catalogo delle
/// esposizioni, i gruppi ambientali NTC, le etichette delle classi minime e i rifiuti di GPC.Checkers.Concrete.Durability (0.0.17.0).
/// Solo testi e trasferimenti di valori: nessuna costante normativa, nessuna formula (prova 11a di tests/ConcreteLibraryAdapter.Checks).
/// Un testo della libreria senza traduzione è un errore di programma (<see cref="InvalidOperationException"/>), come per taglio e torsione.
/// </summary>
public static partial class ConcreteLibraryMapping
{
    // ------------------------------------------------------------------ durabilità: catalogo delle esposizioni
    /// <summary>Descrizioni italiane delle 18 classi di esposizione, mostrate dalla scheda Materiali (prima Materials/Durability.cs:13-30).</summary>
    static readonly FrozenDictionary<string, string> DurabilityDescriptions = new Dictionary<string, string>
    {
        ["X0"] = "Assenza di rischio; per armature: ambiente molto asciutto",
        ["XC1"] = "Carbonatazione · asciutto o sempre bagnato",
        ["XC2"] = "Carbonatazione · bagnato, raramente asciutto",
        ["XC3"] = "Carbonatazione · umidità moderata",
        ["XC4"] = "Carbonatazione · alternanza bagnato/asciutto",
        ["XD1"] = "Cloruri non marini · umidità moderata",
        ["XD2"] = "Cloruri non marini · bagnato, raramente asciutto",
        ["XD3"] = "Cloruri non marini · alternanza bagnato/asciutto",
        ["XS1"] = "Cloruri marini · aerosol, senza contatto diretto",
        ["XS2"] = "Cloruri marini · immersione permanente",
        ["XS3"] = "Cloruri marini · maree, spruzzi e onde",
        ["XF1"] = "Gelo · saturazione moderata, senza disgelanti",
        ["XF2"] = "Gelo · saturazione moderata, con disgelanti",
        ["XF3"] = "Gelo · saturazione elevata, senza disgelanti",
        ["XF4"] = "Gelo · saturazione elevata, disgelanti o mare",
        ["XA1"] = "Attacco chimico · debole",
        ["XA2"] = "Attacco chimico · moderato",
        ["XA3"] = "Attacco chimico · forte"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Classe del catalogo della libreria → record della scheda Materiali: valori di EN 206 F.1 e colonna del prospetto 4.4N
    /// dalla libreria, descrizione italiana dalla mappatura.</summary>
    public static Exposure DurabilityExposure(ExposureClass e)
        => new(e.Code, DurabilityDescriptions.TryGetValue(e.Code, out var text) ? text : throw Unmapped("descrizione dell'esposizione", e.Code),
            e.En206MaxWaterCement, e.En206MinStrength, e.En206MinCement, e.En206MinAir, e.CoverColumn);

    // ------------------------------------------------------------------ durabilità: gruppi ambientali NTC
    /// <summary>Nomi dei gruppi ambientali NTC (ordinario, aggressivo, molto aggressivo: NTC 2018 Tab. 4.1.III) nell'ordine dei gruppi di
    /// <see cref="ExposureClass.NtcEnvironment"/> (prima un array creato a ogni chiamata, Materials/NtcCover.cs:32).</summary>
    static readonly ImmutableArray<string> DurabilityEnvironmentNames = ["Ordinario", "Aggressivo", "Molto aggressivo"];

    public static string DurabilityEnvironment(int group)
        => (uint)group < (uint)DurabilityEnvironmentNames.Length ? DurabilityEnvironmentNames[group]
            : throw Unmapped("gruppo ambientale NTC", group.ToString(CultureInfo.InvariantCulture));

    // ------------------------------------------------------------------ durabilità: etichette delle classi minime
    /// <summary>Rifiuto di un'etichetta di classe minima sconosciuta, come il legacy (Materials/MinimumConcrete.cs:21).</summary>
    public const string DurabilityLabelMessage = "Classe minima non riconosciuta.";

    /// <summary>
    /// Nome della classe minima di resistenza (F2.9-D13): dal catalogo dei calcestruzzi della scheda Materiali
    /// (<see cref="ConcreteMaterialCatalog.MaterialSheetClasses"/>), solo per i valori che la libreria può restituire, cioè le classi
    /// minime UNI 11104 del suo catalogo delle esposizioni; per gli altri valori lo stesso rifiuto del legacy.
    /// </summary>
    public static string DurabilityStrengthLabel(int fck)
    {
        if (!ExposureClasses.All.Any(e => e.Uni11104MinStrength == fck)) throw new ArgumentException(DurabilityLabelMessage);
        return ConcreteMaterialCatalog.MaterialSheetClasses().Where(c => c.Fck == fck).Select(c => c.Name).FirstOrDefault()
            ?? throw new ArgumentException(DurabilityLabelMessage);
    }

    // ------------------------------------------------------------------ durabilità: rifiuti
    /// <summary>Testo del legacy per un codice di esposizione fuori catalogo (NtcCover.Severity, AtecapMix.Limits).</summary>
    public const string DurabilityUnknownExposureMessage = "Esposizione non riconosciuta.";
    /// <summary>Testo del legacy per un codice di esposizione fuori catalogo nella classe minima (MinimumConcrete.Fck).</summary>
    public const string DurabilityUnknownClassMessage = "Classe di esposizione non riconosciuta.";

    static readonly FrozenDictionary<string, string> DurabilityMessages = new Dictionary<string, string>
    {
        ["Select at least one exposure class."] = "Selezionare almeno una classe di esposizione.",
        ["X0 cannot be combined with other exposure classes."] = "X0 non è combinabile con altre esposizioni.",
        ["Invalid concrete strength."] = "Resistenza del calcestruzzo non valida.",
        ["Check bar diameter, maximum aggregate, deviation, design life, abrasion and casting conditions."] = "Controllare diametro, Dmax, tolleranza e condizioni di getto.",
        ["Add the corrosion exposure (XC, XD or XS): XF/XA alone do not define cmin,dur."] = "Per il copriferro aggiungere l'esposizione alla corrosione (XC, XD o XS); XF/XA da sole non definiscono cmin,dur.",
        ["Invalid maximum aggregate size."] = "Dmax non valido."
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // Il codice è quello ricevuto dalla libreria (anche vuoto per un codice nullo); il testo di ANTHEA dipende dall'operazione.
    static readonly Regex DurabilityUnknownExposure = new(@"^Unknown exposure class: .*$", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    // C0 è già formattato dalla libreria con la cultura corrente, come nel messaggio del legacy.
    static readonly Regex DurabilityPertinentCmin = new(@"^Pertinent Cmin: give fck between 12 MPa and C0 = (?<c0>[^ ]+) MPa\.$", RegexOptions.CultureInvariant);

    /// <summary>Rifiuto della libreria → stesso rifiuto con il messaggio del legacy. <paramref name="unknownExposure"/> è il testo del
    /// legacy per un codice fuori catalogo nell'operazione chiamata.</summary>
    public static ArgumentException DurabilityError(ArgumentException e, string unknownExposure)
    {
        if (DurabilityMessages.TryGetValue(e.Message, out var text)) return new ArgumentException(text);
        if (DurabilityUnknownExposure.IsMatch(e.Message)) return new ArgumentException(unknownExposure);
        var m = DurabilityPertinentCmin.Match(e.Message);
        if (m.Success) return new ArgumentException("Cmin pertinente: indicare fck tra 12 MPa e C0 = " + m.Groups["c0"].Value + " MPa.");
        throw Unmapped("rifiuto della durabilità", e.Message);
    }
}
