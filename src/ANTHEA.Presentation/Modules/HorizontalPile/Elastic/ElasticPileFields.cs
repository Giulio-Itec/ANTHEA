using Anthea.Application.Modules;
using Anthea.Presentation.Forms;

namespace Anthea.Presentation.Modules.HorizontalPile.Elastic;

/// <summary>
/// The fields and the fixed texts of the tab of the elastic response of the horizontal pile and of the editor of its reinforcement (F5.8),
/// one definition for the historic view (ElasticPileWorkspace and PileReinforcementEditor, until they are archived) and for
/// <see cref="ElasticPileViewModel"/>: the options of the response, the three groups of assumptions of the reinforcement, the titles of
/// the fields of the segments and the headings. Moved without changes from the historic view. Every field keeps the rule of InputForm:
/// the raw text goes into the document.
/// </summary>
public static class ElasticPileFields
{
    /// <summary>The options of the response under the header: the initial FEM step and the tip constraint.</summary>
    public static FieldSpec[] Options() => [new("passo", "Passo iniziale FEM", "m"), new("punta", "Vincolo alla punta", Choices: ["Libera", "Cerniera", "Incastro"])];

    /// <summary>«Ipotesi di verifica e vincoli per la proposta».</summary>
    public static FieldSpec[] Details() => [new("elemento", "Regole di dettaglio adottate", Choices: ["Pilastro", "Trave", "Solo controlli comuni"]),
        new("barre_trattenute", "Confermo disposizione staffe a trattenimento delle barre compresse", Bool: true), new("zone_estremita", "Confermo dettagli nelle zone di estremità", Bool: true),
        new("azioni_progetto", "H e N sono azioni di progetto; peso unitario adottato coerente", Bool: true), new("taglio_confermato", "Confermo taglio circolare equivalente, staffe chiuse a 90°", Bool: true),
        new("z_d", "Braccio resistente z/d (≤0,9)"), new("aderenza_buona", "Condizioni di buona aderenza", Bool: true), new("percentuale_sovrapposta", "Barre sovrapposte nella sezione", "%"),
        new("distanza_barre", "Distanza libera barre sovrapposte", "mm"), new("lunghezza_barra", "Lunghezza commerciale delle barre", "m"),
        new("lunghezza_gabbia", "Lunghezza massima gabbia da proporre", "m"), new("lunghezza_minima", "Lunghezza minima desiderata tratto", "m")];

    /// <summary>«Sisma · testa del palo · NTC §7.2.5».</summary>
    public static FieldSpec[] Seismic() => [new("sisma_testa", "Controlla zona dissipativa presso la testa · capacità non esclusa", Bool: true), new("sisma_lunghezza", "Estensione dalla testa · vuoto = 10D", "m"),
        new("sisma_azioni", "N e V assegnati appartengono alla combinazione sismica da verificare", Bool: true), new("sisma_elastico", "M assegnato è elastico non ridotto (q=1), con N sismico concomitante", Bool: true)];

    /// <summary>The fields of the seismic group shown only while the seismic head is checked.</summary>
    public static IReadOnlyList<string> SeismicConditional { get; } = ["sisma_lunghezza", "sisma_azioni", "sisma_elastico"];

    /// <summary>«Sovrapposizioni, interferro e copriferro».</summary>
    public static FieldSpec[] Extra() => [new("fattore_sovrapposizione", "Sovrapposizione iniziale: moltiplicatore φ", "φ"), new("aggregato", "Diametro massimo aggregato", "mm"),
        new("copriferro_override", "Override esplicito cmin,dur (altrimenti usa esposizione principale)", Bool: true), new("cmin_dur", "cmin,dur manuale · solo se override attivo", "mm"),
        new("delta_c", "Tolleranza di copriferro Δcdev", "mm")];

    /// <summary>The titles of the reinforcement fields of a segment, by key (the four of the section, then the three of the second ring).</summary>
    public static IReadOnlyList<(string Key, string Title)> SegmentFields { get; } = [(ElasticPileEditor.ReinforcementKeys[0], "Numero barre"), (ElasticPileEditor.ReinforcementKeys[1], "φ barre [mm]"),
        (ElasticPileEditor.ReinforcementKeys[2], "φ staffe [mm]"), (ElasticPileEditor.ReinforcementKeys[3], "Passo staffe [mm]"), ("second_inner_count", "Barre anello interno"),
        ("second_inner_diameter", "φ anello interno [mm]"), ("second_inner_gap", "Distanza libera [mm]")];

    public const string Title = "Risposta alla forza assegnata";
    public const string ToggleNote = "Selezionare una quota; ripetere il clic alle interfacce per leggere i due lati. K* è illustrativa: il FEM usa la matrice di fondazione consistente.";
    public const string MigrationTitle = "Confronto archivio precedente";
    public const string DetailsHeader = "Ipotesi di verifica e vincoli per la proposta";
    public const string SeismicHeader = "Sisma · testa del palo · NTC §7.2.5";
    public const string ExtraHeader = "Sovrapposizioni, interferro e copriferro";
    public const string SegmentsTitle = "Tratti di armatura";
    public const string SegmentsNote = "Quote dalla testa [m]. Le barre terminano alla fine del proprio tratto. Dal secondo tratto l’inizio arretra di l₀; nessuna fusione automatica. Il primo tratto usa la sezione principale.";
    public const string MainTransverseLabel = "Armatura trasversale principale";
    public const string MinimumLabel = "Controlla minimi pali NTC §7.2.5 (As 0,3%, φst 8 mm, s≤8φL; zone dissipative escluse)";
    public const string SeismicIntro = "Zona minima 10D; As ≥ 1% Ac; staffe singole, φ ≥ 8 mm, s ≤ 6φL. Controlla ogni tratto interessato, anche se la zona attraversa più armature. Le quote e le armature assegnate non vengono cambiate.";
    public const string SeismicNote = "Senza valutazione specifica di duttilità: VRd ≥ 1,3|VEd|; in zona dissipativa σc < 0,45fcd; |Mel| < 1,5MRd(N). Confermare la provenienza delle azioni: un FEM elastico sotto un carico qualsiasi non è automaticamente un'analisi sismica q=1. Risultati nel selettore «Sisma · testa palo» a destra. Zone profonde, nodo palo-plinto, interazione cinematica e dettagli esecutivi restano separati.";
    /// <summary>The text above the laps and covers, with the exposure of the main section.</summary>
    public static string ExtraNote(string exposure) => "Esposizione condivisa: " + exposure + ". Dettagli Pilastro per impostazione iniziale modificabile. Default l₀=60φ arrotondato al decimetro superiore, almeno pari alla lunghezza richiesta. Primo gruppo: 0 → fine. Successivi: inizio tratto − l₀ → fine. I gruppi sono spezzati solo oltre la lunghezza commerciale disponibile (6 / 8 / 10 / 12 m). Ancoraggio lbd e traslazione aₗ non allungano automaticamente i pezzi; sviluppi esterni del vecchio schema conservati ma non utilizzati.";
    public const string ConcreteNote = "Scheda di approfondimento: puoi modificare le azioni (N negativo a compressione). Alla chiusura vengono conservate per questo tratto. L'applicazione trasferisce solo l'armatura al palo; geometria, materiali e carichi del foglio restano indipendenti.";
    public const string ConcreteSpiralNote = " Il foglio c.a. usa il modello di taglio a staffe singole: non certifica la resistenza della spirale.";
    public const string ConcreteApplied = "Armatura applicata. FEM conservato; verifiche del palo in aggiornamento.";
    public const string ProposalNote = "Quote su griglia di 0,5 m (punta esatta), tratti ≥3 m. Preferenza per barre 6/8/10/12 m comprensive dei giunti; arretramento per una sovrapposizione nei tratti successivi al primo, con domanda N–M–V. Il cambio di sezione cerca il primo attraversamento discendente di |M|max/2 dopo il massimo; oltre il cambio l’armatura principale è copiata come primo tentativo personalizzabile, da dimensionare. Le quote finali sono quote di taglio; l’inizio dei gruppi successivi arretra di l₀. Dettagli ancora preliminari.";
    public const string DesignIntro = "Ricerca nel catalogo esplicito: As crescente, poi staffe per metro. Sono controllati N–M–V, interferro e minimi disponibili. Dettagli costruttivi preliminari.";
    public const string DesignSearching = "Ricerca con Checker…";
}
