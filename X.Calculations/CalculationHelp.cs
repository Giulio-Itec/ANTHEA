namespace Anthea.Calculations;

/// <summary>Calculation meanings shared by input forms and standalone documentation.</summary>
public static class CalculationHelp
{
    public static string? Field(string key) => key switch
    {
        "phi" => "Coefficiente di viscosità φ. Nel modello a modulo efficace riduce la rigidezza del calcestruzzo; non è un coefficiente delle azioni. Nel ponte interviene anche ψL: n = Es/Ec · (1 + ψL·φ).",
        "n" or "n_armature" or "n_trefoli" => "Rapporto fra modulo dell’acciaio e modulo efficace del calcestruzzo. È collegato a φ: modificare n aggiorna φ secondo il modello scelto; n non può essere inferiore al rapporto dei moduli istantanei.",
        "alpha_cc" => "Coefficiente della resistenza del calcestruzzo: fcd = αcc·fck/γc. È condiviso con le schede compatibili del progetto; non moltiplica le azioni inserite.",
        "gamma_c" => "Coefficiente parziale della resistenza del calcestruzzo. Il valore del progetto deve essere coerente con normativa e situazione di verifica.",
        "gamma_s" => "Coefficiente parziale della resistenza dell’armatura: fyd = fyk/γs. Non coincide con i coefficienti geotecnici della resistenza laterale del palo.",
        "azione_assiale" => "Nel modulo geotecnico N è positivo a compressione. Per la sezione c.a. ANTHEA converte il segno verso Checker, che usa N negativo a compressione. N rimane costante mentre cresce H.",
        "azione_orizzontale" => "Azione orizzontale di progetto HEd. Il diagramma di Broms descrive lo stato alla capacità ultima, non gli spostamenti o le tensioni in esercizio sotto HEd.",
        "verticali_indagate" => "Numero di verticali d’indagine rappresentative usato per ξ3 e ξ4. È un dato progettuale distinto dal numero di righe o di stratigrafie inserite.",
        "passo" => "Campionamento in profondità dei diagrammi. Un passo minore aggiunge punti e aumenta memoria e tempo di esportazione; non aumenta la precisione della ricerca della capacità di Broms.",
        "circular_sides" => "Numero di lati del contorno circolare usato da GPC.Geometry e Checker. Il poligono è inscritto: aumentarne i lati avvicina area e resistenza a quelle del cerchio, con maggior costo di calcolo.",
        "cls_diagramma" => "Legame costitutivo del calcestruzzo fornito da GPC.Model e usato nell’analisi non lineare. Cambiarlo può modificare il dominio resistente.",
        "steel_diagramma" => "Legame dell’acciaio da GPC.Model: plateau elastoplastico oppure incrudimento fino a fu ed εu. È usato anche per la resistenza della sezione del palo.",
        "steel_eps_u" => "Deformazione ultima a trazione dell’acciaio, in per mille. Deve superare la deformazione di snervamento fyk/Es; il limite di progetto dipende dai coefficienti del modello.",
        "steel_fu_mpa" => "Resistenza ultima dell’acciaio, in MPa. Deve essere almeno pari a fyk. I vecchi fogli privi di questo campo sono inizializzati con fu = fyk; per descrivere l’incrudimento occorre assegnare anche εu. Un campo esplicitamente vuoto deve essere completato.",
        "angolo_attrito" => "Angolo di attrito efficace del terreno φ′, in gradi. È distinto dal coefficiente di viscosità φ del calcestruzzo. Il motore lo usa per attrito laterale e fattori di capacità portante.",
        "coesione_efficace" => "Coesione efficace c′, in kPa, per il ramo drenato del palo verticale. Il modello attuale trascura questo contributo negli strati classificati granulari; non confonderla con Cu.",
        "coesione_non_drenata" => "Resistenza non drenata Cu, in kPa. Il ramo non drenato del palo verticale e la reazione limite coesiva di Broms hanno ipotesi distinte: consultare le note del modello.",
        "sicurezza_laterale_compressione" => "Divisore γs della resistenza geotecnica laterale a compressione. È distinto dal γs delle armature e dai coefficienti ξ di correlazione delle indagini.",
        "sicurezza_laterale_trazione" => "Divisore γt della resistenza geotecnica laterale a trazione. Il contributo resistente della punta a trazione è nullo.",
        "sicurezza_base" => "Divisore γb della resistenza geotecnica di punta. Non modifica la resistenza laterale né le azioni di progetto.",
        "azione_compressione" or "azione_trazione" => "Modulo positivo dell’azione assiale di progetto, già combinata. Il peso proprio del palo è gestito separatamente dal motore con i coefficienti favorevole/sfavorevole.",
        "pressione_iniezione" => "Pressione in MPa utilizzata nell’abaco Bustamante–Doix secondo la convenzione p_i = p_l adottata dal modulo. I valori fuori dall’intervallo documentato vengono rifiutati.",
        "inizio_aderenza" => "Distanza lungo l’asse del micropalo dall’origine al primo tratto aderente. Gli spessori degli strati sono invece verticali e vengono convertiti usando l’inclinazione.",
        "cover_mm" => "Copriferro netto dal bordo del calcestruzzo alla superficie esterna della staffa. Il confronto con Materiali usa anche il diametro effettivo delle barre della sezione.",
        "origine_momento" => "La scelta automatica usa la sezione e i materiali inseriti. Con momento manuale indicare valore e provenienza: la verifica geotecnica non lo ricava dalla geometria della sezione.",
        _ => null
    };

    public const string ConcreteTheory = "La sezione resta piana: ε(x,y) = ε0 + χx·(x−x0) + χy·(y−y0). " +
        "Checker ricerca il piano che equilibra N, Mx e My integrando le tensioni dei materiali Model. " +
        "Nel dominio resistente il piano raggiunge un limite di deformazione; nell’analisi tensionale si equilibra l’azione assegnata. " +
        "Le combinazioni sono già combinate: i coefficienti di resistenza non coefficientano nuovamente N e M. " +
        "N negativo indica compressione. I contorni circolari sono poligoni inscritti. " +
        "Il modulo riutilizza i risultati quando cambiano solo rappresentazione o dati di fessurazione; la geometria, i materiali e le azioni richiedono un aggiornamento pertinente. " +
        "Le verifiche di taglio, torsione, ancoraggio e durabilità hanno ingressi e ipotesi propri: il solo esito del dominio non le sostituisce.";
    public const string PileTheory = "Palo verticale: la resistenza laterale è integrata per strato e combinata con la resistenza di punta; " +
        "falda, condizioni drenate/non drenate, correlazione delle indagini e coefficienti di resistenza restano distinti. " +
        "Palo orizzontale: Broms equilibra la reazione limite del terreno con H e il momento H·e, individuando il meccanismo corto, intermedio o lungo. " +
        "Il momento resistente della sezione c.a. viene da Checker a N costante, con lo stesso modello del modulo strutturale e prendendo il minore dei due versi Mx. " +
        "Il residuo assiale è mostrato con la tolleranza di accettazione, senza confonderlo con un confronto fra mesh. " +
        "Broms è un modello di capacità ultima: spostamenti SLE, ciclicità, secondo ordine e duttilità delle cerniere richiedono verifiche dedicate. " +
        "L’estensione a terreni multistrato è sperimentale. Per micropali iniettati valgono gli abachi e i limiti Bustamante–Doix indicati nel foglio.";
    public const string ProjectTheory = "Materiali, geometrie, terreni e coefficienti sono comuni solo tra schede compatibili. " +
        "La gerarchia considera i fogli dello stesso livello e gli antenati; due rami fratelli non condividono automaticamente i dati. " +
        "Il livello più alto che definisce una proprietà ne governa il riferimento. Se più riferimenti discordano, il programma conserva il conflitto. " +
        "Uniforma applica solo la proprietà selezionata ai destinatari compatibili e invalida i relativi risultati. " +
        "Azioni, fasi e combinazioni restano locali. I valori inattivi vengono conservati nel file ma esclusi dai confronti pertinenti. " +
        "Coefficienti con significati o normative diverse non vengono equiparati solo perché hanno lo stesso simbolo.";
}
