using System.Collections.Frozen;
using System.Text.Json.Nodes;
namespace Materiali;

/// <summary>Texts of the exposure menu of the concrete sheet, for one state of the menu (<see cref="MaterialExposurePresentation.Texts"/>).</summary>
/// <param name="Shown">The exposure the menu shows: the selected one when it is active, otherwise the first active one; null without active exposures.</param>
/// <param name="Details">Description of the environment of <paramref name="Shown"/>.</param>
/// <param name="Examples">Examples of typical elements of <paramref name="Shown"/>.</param>
/// <param name="Aggressiveness">«Aggressività: …» of <paramref name="Shown"/>.</param>
/// <param name="Combined">The active classes when they are more than one, or the exposures of an archive of a previous version.</param>
/// <param name="CombinedVisible">The text of <paramref name="Combined"/> is shown.</param>
public sealed record MaterialExposureTexts(string? Shown, string Details, string Examples, string Aggressiveness, string Combined, bool CombinedVisible);

/// <summary>
/// Texts of the exposure classes of the concrete sheet: ANTHEA's descriptions and examples of the classes of UNI EN 206,
/// prospetto 1, and the aggressiveness of NTC 2018, tab. 4.1.III (decision U5 of 14/10/2026, entry F4-47). Moved without changes
/// from the historic view Materiali.MaterialView (ExposureSelector.cs) in F5.5, so that the view and its ViewModel show the same
/// texts.
/// </summary>
public static class MaterialExposurePresentation
{
    /// <summary>Description of the environment of each exposure class.</summary>
    public static IReadOnlyDictionary<string, string> Descriptions { get; } = new Dictionary<string, string>
    {
        ["X0"]="Nessun rischio di corrosione o di attacco. Con armature o inserti metallici: solo ambiente molto asciutto. Senza armature: qualsiasi ambiente privo di gelo, abrasione e attacco chimico.",
        ["XC1"]="Corrosione da carbonatazione: ambiente sempre asciutto oppure sempre bagnato.",
        ["XC2"]="Corrosione da carbonatazione: ambiente bagnato, raramente asciutto.",
        ["XC3"]="Corrosione da carbonatazione: umidità dell’aria moderata o elevata.",
        ["XC4"]="Corrosione da carbonatazione: alternanza di periodi asciutti e bagnati.",
        ["XD1"]="Corrosione da cloruri non marini: umidità dell’aria moderata.",
        ["XD2"]="Corrosione da cloruri non marini: ambiente bagnato, raramente asciutto.",
        ["XD3"]="Corrosione da cloruri non marini: alternanza di periodi asciutti e bagnati.",
        ["XS1"]="Corrosione da cloruri marini: salsedine portata dall’aria, senza contatto con l’acqua di mare.",
        ["XS2"]="Corrosione da cloruri marini: immersione permanente in acqua di mare.",
        ["XS3"]="Corrosione da cloruri marini: zone di marea, di onda e di spruzzi.",
        ["XF1"]="Gelo e disgelo: saturazione d’acqua moderata, senza sali disgelanti.",
        ["XF2"]="Gelo e disgelo: saturazione d’acqua moderata, con sali disgelanti.",
        ["XF3"]="Gelo e disgelo: saturazione d’acqua elevata, senza sali disgelanti.",
        ["XF4"]="Gelo e disgelo: saturazione d’acqua elevata, con sali disgelanti o acqua di mare.",
        ["XA1"]="Attacco chimico debole da terreni o acque.",
        ["XA2"]="Attacco chimico moderato da terreni o acque.",
        ["XA3"]="Attacco chimico forte da terreni o acque."
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>ANTHEA's examples of typical elements of each exposure class, to be confirmed on the project.</summary>
    public static IReadOnlyDictionary<string, string> Examples { get; } = new Dictionary<string, string>
    {
        ["X0"]="Elementi armati in interni molto asciutti.\nElementi non armati in interni, interrati o immersi in terreni e acque non aggressivi.",
        ["XC1"]="Elementi in interni con bassa umidità.\nElementi sempre immersi in acqua.",
        ["XC2"]="Fondazioni e superfici a contatto con l’acqua per lunghi periodi.\nVasche e serbatoi d’acqua.",
        ["XC3"]="Elementi in interni con umidità da moderata a elevata.\nSuperfici esterne riparate dalla pioggia.",
        ["XC4"]="Superfici esterne bagnate dalla pioggia che si asciugano fra un evento e l’altro, fuori dai casi XC2.",
        ["XD1"]="Elementi raggiunti dai cloruri portati dall’aria, per esempio vicino a strade trattate con sali disgelanti.",
        ["XD2"]="Vasche di impianti di trattamento delle acque, piscine, elementi a contatto con acque industriali contenenti cloruri.",
        ["XD3"]="Elementi raggiunti da spruzzi di acque con sali disgelanti: parti di ponti, muri di sostegno stradali, pavimentazioni di strade e parcheggi.",
        ["XS1"]="Strutture vicine alla costa senza contatto con l’acqua di mare.",
        ["XS2"]="Parti di opere marittime sempre sommerse.",
        ["XS3"]="Banchine, moli, pontili e opere di difesa costiera nella zona di marea e di spruzzi.",
        ["XF1"]="Superfici verticali esposte alla pioggia e al gelo, senza sali disgelanti.",
        ["XF2"]="Superfici verticali di opere stradali esposte al gelo e ai sali disgelanti portati dall’aria.",
        ["XF3"]="Superfici orizzontali esposte alla pioggia e al gelo dove l’acqua può ristagnare, senza sali disgelanti.",
        ["XF4"]="Impalcati, strade e pavimentazioni esposti al gelo e ai sali disgelanti.\nSuperfici esposte al gelo in zona costiera.",
        ["XA1"]="Elementi a contatto con terreni o acque di falda debolmente aggressivi: grado di attacco dall’analisi chimica, UNI EN 206 prospetto 2.",
        ["XA2"]="Elementi a contatto con terreni o acque di falda moderatamente aggressivi: grado di attacco dall’analisi chimica, UNI EN 206 prospetto 2.",
        ["XA3"]="Elementi a contatto con terreni o acque di falda fortemente aggressivi: grado di attacco dall’analisi chimica, UNI EN 206 prospetto 2."
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Aggressiveness of the environment from the severity of NTC 2018, tab. 4.1.III.</summary>
    public static string Aggressiveness(int severity)=>severity switch{0=>"ordinaria",1=>"aggressiva",2=>"molto aggressiva",_=>throw new ArgumentOutOfRangeException(nameof(severity))};

    /// <summary>
    /// The texts of the exposure menu: the shown exposure (the selected one when active, otherwise the first active one), its
    /// description, examples and aggressiveness, and the line of the active classes or of the exposures of an archive of a previous
    /// version (<paramref name="legacy"/>, the «esposizioni_precedenti» of the sheet).
    /// </summary>
    public static MaterialExposureTexts Texts(string? selected, IReadOnlyList<MaterialExposureChoice> active, JsonArray? legacy)
    {
        if(!active.Any(e=>e.Code==selected))selected=active.FirstOrDefault()?.Code;
        string combined=active.Count>1?"Classi attive: "+string.Join(" + ",active.Select(e=>e.Code))+
            (active.Any(e=>e.Code=="X0")?" · combinazione non valida: X0 deve essere isolata.":"\nAggressività complessiva: "+Aggressiveness(active.Max(e=>e.Severity))):"";
        if (legacy is not null && active.Count == 1)
            combined="Archivio precedente con più esposizioni: " + string.Join(" + ", legacy.Select(v=>v?.ToString())) + ". Classe attiva unica: " + selected + ".";
        return new(selected,
            selected is null?"Selezionare una classe di esposizione.":Descriptions[selected],
            selected is null?"":Examples[selected],
            selected is null?"Aggressività: da definire":"Aggressività: "+Aggressiveness(MaterialSheetInput.ExposureChoices.Single(e=>e.Code==selected).Severity),
            combined, active.Count>1 || legacy is not null);
    }
}
