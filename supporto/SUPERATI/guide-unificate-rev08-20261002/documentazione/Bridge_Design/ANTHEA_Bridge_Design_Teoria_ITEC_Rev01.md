# Teoria e validazione di Bridge Design
Edizione 30 settembre 2026 · Revisione 01 · ANTHEA BD T01

Questo rapporto descrive come ANTHEA costruisce, confronta e ordina le alternative di Bridge Design. È destinato a chi deve usare il predimensionamento per scegliere soluzioni da approfondire e vuole ricostruire le formule, la provenienza dei dati e il significato delle prove. Il contenuto è ricavato dalle sorgenti e dalle esecuzioni indicate in appendice, non da una ricostruzione ipotetica del sito di riferimento.

La conclusione è precisa: il software individua il minimo del proprio criterio fra le combinazioni ammesse ed effettivamente esplorate. Sono stati verificati calcoli ideali, quantità, ordinamento e vincoli; una prova indipendente ha confrontato 100 travi con un modello FEM separato e un intero computo con una griglia di 26 geometrie. Queste prove non attestano la sicurezza né l’ottimo globale di un ponte reale. Le regole di snellezza, le incidenze di armatura e i parametri geotecnici convenzionali richiedono una valutazione progettuale specifica.

## Ambito del motore e significato di ottimizzazione

Bridge Design usa il motore parametrico BridgeConcept di ANTHEA. I moduli Sezione in c.a. e Sezione composta da ponte impiegano invece le librerie indicate nell’interfaccia come GPC Engine. L’ottimizzazione di Bridge Design non richiama automaticamente quelle verifiche di sezione. Il nuovo riferimento GPC Engine non trasforma il predimensionamento in una verifica strutturale completa.

L’ottimizzatore è una ricerca discreta deterministica: genera combinazioni, calcola ciascuna, applica filtri e ordina quelle rimaste. Non usa apprendimento automatico, un modello addestrato sui risultati del sito, un algoritmo genetico o una ricerca continua per gradiente. A parità di dati, opzioni e versione del motore produce gli stessi risultati. Non promette di trovare una soluzione compresa fra due valori della griglia o appartenente a una tipologia non modellata.

La soluzione ammissibile è una soluzione che supera i filtri interni elencati nel seguito. Il termine non equivale a verificata secondo NTC, Eurocodici o AASHTO. Nel modello non esiste un filtro generale che confronti il momento flettente con la resistenza dell’impalcato, né un limite automatico di freccia per tutte le tipologie. Una graduatoria economica può quindi favorire una sezione che richiederà un aumento di materiale nel progetto strutturale.

## Dati esterni e loro ingresso nel calcolo

I dati esterni entrano attraverso i campi del progetto e il listino. Durante la ricerca non vengono interrogati il sito, ANAS, un servizio geologico o una banca dati di ponti. I valori iniziali sono contenuti nel codice e restano modificabili; un archivio riaperto conserva i propri prezzi e coefficienti. L’utente deve aggiornare consapevolmente le ipotesi quando cambia sito, anno economico, fornitore o prestazione richiesta.

| Dato | Origine prevista | Effetto nel modello |
| --- | --- | --- |
| Lunghezza quota e larghezza | Rilievo e requisiti funzionali | Geometria campate volumi e carichi |
| Ostacolo e sua larghezza | Vincoli del sito | Spostamento o esclusione degli appoggi interferenti |
| Classe del terreno e resistenze | Valori convenzionali oppure studio geotecnico | Fondazione automatica e soglie assiali |
| Carichi equivalenti e moltiplicatori | Ipotesi dell’utente | Sollecitazioni reazioni e dimensioni automatiche |
| Prezzi unitari | Prezzari e preventivi | Costo a parità di quantità |
| Fattori ambientali | Convenzioni oppure EPD coerenti | Indicatore parziale di CO₂ |

Per dati geotecnici reali non basta scegliere la classe più simile al terreno. Pressione di riferimento, resistenza laterale e resistenza di punta devono essere coerenti con il tipo di fondazione, le profondità, la falda e il livello di cautela adottato. Il modello usa valori uniformi e non ricostruisce una stratigrafia. Non applica automaticamente i coefficienti geotecnici di una combinazione normativa: i valori di classe sono già trattati come riferimenti convenzionali ridotti, senza una tracciabilità normativa completa.

I prezzari servono a verificare l’ordine di grandezza delle tariffe e le inclusioni delle lavorazioni. Non forniscono la geometria ottima. Il sito TheBridgeEng è stato un riferimento di interfaccia e un termine di confronto numerico; i suoi risultati non vengono utilizzati come obiettivi da inseguire durante una ricerca.

## Variabili libere e grandezze conservate

Restano sempre costanti la lunghezza totale, la quota sul terreno, la composizione della larghezza, l’ostacolo, il terreno, le resistenze dei materiali, i carichi, i prezzi, i fattori ambientali e gli estremi su spalla o pila. I parametri delle strutture speciali, come freccia dell’arco, altezza antenna, freccia dei cavi e canalette, sono ereditati dal progetto: non sono assi autonomi della griglia.

Si possono liberare tipologia, numero di campate, altezza, sezione standard, schema di pila, fondazione e continuità. La continuità è bloccata per impostazione iniziale. Liberare la sezione significa adottare i parametri standard della famiglia per i campi ordinari della sezione; non significa ottimizzare individualmente ogni spessore metallico o ogni interasse. I parametri avanzati rimangono quelli del progetto anche quando si libera la sezione.

Bloccare la sezione richiede di bloccare la tipologia. Il programma congela le quote ordinarie adottate, inclusi soletta, interasse, anima e fondo ove pertinenti. Bloccare l’altezza congela quella in campata; per il cassone variabile l’altezza sulle pile continua a dipendere dalla luce. Bloccare il numero delle campate non introduce una scelta libera delle singole luci: queste seguono la distribuzione prevista dal modello e l’eventuale ostacolo.

Il blocco di pile e fondazioni conserva le quote manuali. Le quote lasciate a zero rimangono regole automatiche e possono cambiare se cambiano le reazioni. Il blocco della fondazione conserva il tipo effettivamente adottato e la lunghezza dei pali; numero dei pali e dimensione del plinto restano automatici se lo erano all’origine. Questo comportamento va distinto dal congelamento di un intero progetto esecutivo delle fondazioni.

## Costruzione della griglia di ricerca

Il numero di campate esplorabile va da 1 a 30, entro minimo e massimo impostati. Per ciascuna famiglia si fa un primo controllo sulla luce media L/n; successivamente vengono controllate le luci effettive, che possono essere diverse dalla media. Strallato e sospeso usano soltanto tre campate, con luce centrale pari a metà della lunghezza totale. La griglia completa preliminare non può superare 50.000 tentativi.

Le griglie percentuali dell’altezza e della lunghezza dei pali ammettono valori da 100% a 200%, con passo da 1 a 100 punti percentuali e al massimo 11 valori per griglia. L’estremo superiore è incluso anche quando il passo non divide l’intervallo: 100–115 con passo 10 genera 100, 110 e 115%. Il riferimento dell’altezza è il predimensionamento della singola combinazione; quello dei pali è la lunghezza convenzionale della classe del terreno.

L’impostazione iniziale esplora altezze 100 e 115%, pali 100, 125 e 150%, campate da 1 a 12. Per i pali la lunghezza generata non supera 80 m. Le fondazioni libere sono plinto diretto, pali da 1,0 m e pali da 1,5 m; la voce Automatica viene risolta e non costituisce un quarto tipo da classificare.

Le altezze aumentate vengono arrotondate verso l’alto a passi di 0,05 m; il valore al 100% conserva la quota automatica non arrotondata. Nella revisione documentata è stata corretta la sensibilità dell’arrotondamento al rumore numerico. Prima della correzione, una quota teorica esatta di 0,80 m poteva essere portata a 0,85 m perché rappresentata internamente come un numero appena superiore a 0,80.

La configurazione corrente viene aggiunta come riferimento prima della griglia, anche se fuori dagli intervalli percentuali. Deve comunque soddisfare i filtri, compresi i limiti assoluti di campate e altezza. Il numero di tentativi previsto è un limite superiore: salti di combinazioni incompatibili e rimozione di duplicati possono ridurlo. I tentativi ammessi possono essere più delle geometrie distinte, perché la stessa geometria può essere rappresentata con quote automatiche o esplicite.

## Geometria longitudinale e larghezza

Indicando con nc il numero delle corsie, bc la larghezza della corsia, b la banchina per lato, m lo spartitraffico e bb l’ingombro della barriera per lato, la larghezza complessiva W è:

$$ W = nc × bc + 2b + m + 2bb

Per le famiglie ordinarie, n campate semplicemente appoggiate sono uguali. Se l’impalcato è continuo e n è maggiore di 2, le due campate terminali hanno peso 0,8 e le interne peso 1. La lunghezza di ciascuna campata è la lunghezza totale moltiplicata per il proprio peso e divisa per la somma dei pesi. I ponti con antenne adottano invece L/4, L/2, L/4. Arco con catena e reticolare hanno campate indipendenti.

L’ostacolo è centrato sulla lunghezza del ponte e viene ampliato di 1 m per lato. Il programma prova a spostare gli appoggi interni ai bordi di questa fascia, senza creare campate inferiori a 2 m. Se non può farlo, conserva o segnala la disposizione incompatibile e la ricerca la esclude. Non vengono letti un tracciato planimetrico, una curva d’alveo, il franco idraulico o l’erosione. Il margine di 1 m è geometrico e non costituisce un franco di progetto.

## Regole di altezza per le quattordici famiglie

Per le famiglie senza struttura superiore l’altezza automatica d è il maggiore fra un minimo e Lmax/r moltiplicato per 0,95 in continuità o 1,10 con campate indipendenti. Per arco, reticolare, strallato e sospeso il moltiplicatore è 1. Per strallato e sospeso Lmax indica la luce centrale. Sono regole convenzionali interne, non risultati di una verifica resistente né rapporti prescritti universalmente dalle norme.

$$ d = max(dmin ; k × Lmax / r)

| Tipologia | Campo luce m | Rapporto r | Minimo d m |
| --- | --- | --- | --- |
| Soletta piena in c.a. | 6–15 | 18 | 0,35 |
| Travi a T in c.a. | 12–30 | 17 | 0,70 |
| Travi a I in c.a.p. | 20–50 | 22,22 | 1,00 |
| Travi a U in c.a.p. | 25–50 | 22,22 | 1,10 |
| Cassone in c.a.p. | 35–80 | 22,22 | 1,30 |
| Cassone a conci variabile | 80–200 | 45 | 2,00 |
| Travi a I acciaio cls | 30–90 | 25 | 1,00 |
| Cassone acciaio cls | 40–150 | 25 | 1,20 |
| Travi incorporate | 8–40 | 28 | 0,45 |
| Piastra ortotropa | 40–200 | 30 | 1,20 |
| Arco con catena | 40–250 | 120 | 0,80 |
| Strallato | 100–700 centrale | 150 | 1,00 |
| Sospeso | 200–1200 centrale | 200 | 1,20 |
| Reticolare | 30–150 | 100 | 0,70 |

Nel cassone variabile l’altezza sulle pile vale max(d; Lmax/18). Quantità e inerzia sono calcolate con altezza equivalente deq = d + (dpila − d)/3. Il motore non integra una legge reale di variazione lungo l’asse e non simula la costruzione a sbalzo. Il limite massimo di altezza nell’ottimizzazione si applica anche a dpila, cioè all’altezza dell’impalcato in corrispondenza delle pile, non all’altezza del fusto sul terreno.

## Sezioni ideali quantità e inerzie

Il calcolo usa metri per le dimensioni generali, m² per le aree, m³ per i volumi, m⁴ per le inerzie e tonnellate per gli acciai. Gli spessori metallici inseriti in mm sono divisi per 1.000. Il numero ordinario di travi è max(2; parte intera di W/interasse); soletta e cassone in c.a.p. hanno un elemento longitudinale equivalente, mentre i cassoni metallici usano il numero impostato o una regola sulla larghezza.

La soletta piena ha area Wd. Le travi a T sommano soletta e anime rettangolari. Le travi a I in c.a.p. sono profili ideali: piattabande di spessore min(0,18; h/4), larghezza min(0,70; 0,70W/ng), anima min(0,20; bf/2) e rialzo configurabile. Non sono sezioni di catalogo del produttore. Il cassone in c.a.p. somma soletta, fondo di larghezza W per il rapporto impostato e numero di anime pari al numero delle celle più uno.

Per una trave metallica a I, tf e tw sono gli spessori di piattabanda e anima, bf è la larghezza delle piattabande e h l’altezza sotto soletta. L’area per trave è:

$$ Aa = 2bf × tf + tw × (h − 2tf)

Per il cassone metallico ordinario il fondo per cassone vale 0,40W/ng; le due piattabande superiori e le due anime sono conteggiate separatamente. Se s è il parametro H per 4V, lo scarto orizzontale di ogni anima è hw × s/4. Lo sviluppo reale dell’anima inclinata è:

$$ lw = √(hw² + (hw × s/4)²)

L’area delle anime è 2ng tw lw. L’inerzia verticale locale delle anime sottili usa A hw²/12: lo sviluppo reale determina l’area, mentre la proiezione verticale determina la distribuzione delle quote. Il medesimo criterio è usato per le anime delle U, con scarto orizzontale pari a metà della differenza fra larghezza superiore e inferiore. Non si aggiunge l’inerzia microscopica nello spessore della parete: resta una schematizzazione a parete sottile.

Il calcestruzzo usa Ec = 22.000((fc + 8)/10)^0,3 MPa, senza viscosità. L’acciaio usa Es = 200.000 MPa. Il baricentro e l’inerzia sono omogeneizzati a calcestruzzo, con rapporto nj = Ej/Ec. Per ogni componente si somma l’inerzia locale e il termine di trasporto. La rigidezza per l’analisi longitudinale è Ec × 1.000 × I, in kN m².

$$ yG = Σ(nj Aj yj) / Σ(nj Aj)
$$ Ieq = Σ[nj × (Ij + Aj × (yj − yG)²)]

Per le travi incorporate, Ac = Wd − Aa: il volume d’acciaio sostituisce calcestruzzo. L’inerzia usa il rettangolo lordo in cls più l’apporto dell’acciaio con coefficiente Es/Ec − 1, evitando di conteggiare due volte la stessa area. Si assume collaborazione perfetta; adesione, fasi di getto e verifiche dei profili non sono risolte.

La piastra ortotropa somma lamiera superiore, canalette, fondi e due anime verticali per cassone. La lunghezza dei lati inclinati delle canalette è calcolata geometricamente. Il numero delle canalette deriva dalla parte intera di W/interasse e il prospetto espone l’interasse adottato W/n. La massa riceve un’aggiunta per traversi e accessori, ma il modello non esegue una verifica locale ortotropa o delle saldature. Il manuale FHWA [R4] documenta la necessità di trattare distintamente flessione locale, distorsione e fatica; citarlo non significa che queste verifiche siano implementate.

## Acciai e carichi equivalenti

Il volume di calcestruzzo dell’impalcato è l’area della sezione per la lunghezza. La massa di carpenteria è 7,85 t/m³ per il volume geometrico, maggiorata del 15% per traversi, irrigidimenti e connessioni; nelle travi incorporate l’aggiunta è 5%. Questa maggiorazione entra in massa, costo e peso proprio, ma non nell’inerzia flessionale.

Le armature sono stimate per incidenza: 140 kg/m³ per impalcati in c.a., 110 per c.a.p., 120 per soletta mista e famiglie estese; 150 per sottostrutture e 120 per fondazioni. La precompressione vale inizialmente 30 kg/m³ di impalcato precompresso. I parametri editabili sono riportati nell’interfaccia; l’incidenza 120 delle solette miste è una convenzione del motore. Non vengono ricavati numero, tracciato, tesatura o perdite dei cavi di precompressione.

Indicando con ma la massa totale di carpenteria dell’impalcato in t, con g2 il carico permanente portato in kN/m² e con nb il numero convenzionale di barriere, i carichi sull’intera larghezza sono:

$$ G = 25Ac + 9,81ma/L + g2W + 8nb
$$ Q = qtraffico × W
$$ qs = G + Q
$$ qd = γG G + γQ Q

I valori iniziali sono g2 = 2,5 kN/m², qtraffico = 9 kN/m², γG = 1,35 e γQ = 1,50. nb vale 2, oppure 3 in presenza di spartitraffico. Il traffico è uniforme e contemporaneo su tutte le campate: non è un inviluppo di assi mobili né un modello di corsie caricate alternativamente. L’impalcato è rappresentato come un’unica trave equivalente per tutta la larghezza.

## Analisi della trave equivalente

Per le famiglie senza struttura superiore, ANTHEA risolve una trave di Euler Bernoulli con EI costante, appoggi verticali e rotazioni libere alle estremità. Nella continuità i momenti sugli appoggi interni sono ottenuti con il teorema dei tre momenti. Per due campate adiacenti a e b, con momenti Ml, Mi e Mr e carico uniforme q:

$$ Ml a + 2Mi(a + b) + Mr b = −q(a³ + b³)/4

Una volta noti i momenti agli estremi della campata di lunghezza l, la reazione locale sinistra, il taglio e il momento sono:

$$ Rl = ql/2 + (Mr − Ml)/l
$$ V(x) = Rl − qx
$$ M(x) = Ml + Rl x − qx²/2

La freccia è ricavata integrando M/EI due volte e imponendo spostamento nullo ai due appoggi. Il motore campiona 41 punti per campata e aggiunge il punto di taglio nullo per individuare l’estremo del momento. L’estremo della freccia resta campionato: il valore visualizzato non è sempre il massimo analitico esatto. I diagrammi delle azioni usano qd; la freccia indicativa e le reazioni usate nel dimensionamento ordinario delle fondazioni sono riferite a qs.

Per una campata appoggiata valgono R = ql/2, Mmax = ql²/8 e vmax = 5ql⁴/(384EI). Per due campate uguali continue, il momento centrale è −ql²/8, la reazione esterna 3ql/8, quella centrale 5ql/4 e il massimo positivo 9ql²/128. Questi casi sono controllati direttamente dalla suite. La formulazione FEM adottata nella nuova verifica indipendente è documentata da TU Delft [R5].

Il modello trascura deformazione a taglio, fessurazione, viscosità, ritiro, effetti reali della precompressione, rigidezza variabile, cedimenti degli appoggi e fasi costruttive. La buona concordanza fra due solutori con queste ipotesi dimostra la corretta soluzione del problema ideale, non la validità delle ipotesi per qualsiasi ponte.

## Pile spalle e fondazioni

La quota del terreno è schematica. Per una pila ordinaria l’altezza H è la quota dell’impalcato meno l’altezza della sezione sull’appoggio; per una spalla si usa min(7 m; quota meno altezza impalcato). Sono richiesti almeno 1 m di spazio verticale. Una pila a telaio ha max(2; arrotondamento superiore di W/7) colonne, una pila circolare o a martello ha un fusto, il setto ha lunghezza trasversale max(1; W − 2).

Il pulvino ordinario vale W × 1,5 × 1,4 m³; la testa a martello W × 2 × 1,8 m³. La spalla ha volume convenzionale W[H max(0,6; H/7) + 3]. Il termine 3 è un’area equivalente in m² per metro di larghezza, non una misura completa di paraghiaia, muri d’ala e mensole. La spalla non è verificata per spinta del terreno o stabilità.

Il dimensionamento automatico della pila ordinaria soddisfa una snellezza convenzionale non maggiore di 90 e una compressione media non maggiore di 0,30fc,sub. Considerando il peso proprio del fusto, l’area minima si ricava da:

$$ Areq = max(0 ; R + 25Vpulvino) / (300fc,sub − 25H)

R è la reazione di servizio in kN e fc,sub è in MPa. Per colonne circolari il diametro è almeno max(1,2; 8H/90; √(4Areq/(πnc))) m; per il setto lo spessore è almeno max(1; 2H√12/90; Areq/max(1; W − 2)). La misura automatica è arrotondata verso l’alto a 0,05 m. La snellezza diagnostica è 2H/r, con r = D/4 per la colonna o t/√12 per il setto. La ricerca esclude valori maggiori di 100: la soglia 90 è un margine della regola automatica, 100 è la soglia di esclusione.

| Classe convenzionale | Pressione kPa | Attrito palo kPa | Punta palo kPa | Lunghezza palo m |
| --- | --- | --- | --- | --- |
| Roccia | 1.000 | 150 | 8.000 | 10 |
| Sabbia o ghiaia densa | 400 | 70 | 2.500 | 15 |
| Terreno medio | 200 | 45 | 1.500 | 22 |
| Argilla soffice | 100 | 25 | 500 | 30 |

La fondazione Automatica delle otto famiglie ordinarie sceglie il plinto in roccia, oppure in terreno denso se la quota è inferiore a 15 m; negli altri casi sceglie pali da 1,0 m. Le sei famiglie estese usano plinto su roccia o terreno denso con quota inferiore a 25 m, altrimenti pali da 1,5 m. Sono convenzioni differenti del software: per confronti controllati conviene imporre esplicitamente il tipo di fondazione.

Per un palo di diametro D e lunghezza Lp, la resistenza assiale di riferimento è:

$$ Rpal = π D Lp qs,palo + πD² qb/4

L’azione comprende reazione dell’impalcato, peso della sottostruttura e peso del plinto. Nel plinto diretto il rapporto indicativo è N/(B T p); su pali è N/(np Rpal). B e T sono i lati della fondazione, p la pressione di riferimento e np il numero dei pali. Il peso del plinto viene aggiornato durante il dimensionamento; non è trascurato nel numeratore.

Per il plinto diretto si parte da una dimensione basata su √(N/(0,85p)), con minimo geometrico e arrotondamento a 0,25 m; lo spessore è max(0,60; B/6). La dimensione viene aumentata a passi di 0,25 m finché il rapporto è non maggiore di 1. Per i pali il numero automatico è pari e non inferiore a 4; aumenta di almeno due unità quando necessario. Il plinto su pali ha spessore 1,5D e deve contenere una griglia a interasse 3D con ingombro minimo [ceil(√np) − 1]3D + 2D. Il lato trasversale tiene conto anche della larghezza della sottostruttura.

Il ciclo di fondazione ha un limite di 1.024 aggiornamenti e rifiuta casi non convergenti. Quote e numeri imposti dall’utente vengono rispettati, esponendo l’eventuale superamento del rapporto. Restano esclusi eccentricità, pressoflessione dei pali, carichi orizzontali, effetto di gruppo, cedimenti, attrito negativo, liquefazione e scalzamento. Il quadro delle verifiche reali è distinto da queste formule e va ricondotto alle norme applicabili [R6].

## Archi reticolari stralli e sospensioni

Le strutture superiori sono predimensionate con equilibri ideali e aree pari alla forza assiale divisa per una tensione di riferimento. I valori iniziali sono 180 MPa per elementi tesi di carpenteria, 100 MPa per archi e aste compresse, 600 MPa per cavi e 6 MPa per antenne in cls. Non sono resistenze di progetto derivanti da una verifica completa. La carpenteria superiore riceve un’aggiunta iniziale del 20% per collegamenti; cavi e pendini sono conteggiati a parte.

$$ A = max(0,00001 ; |N|/σrif)

N e σrif devono essere in unità coerenti: nel codice σrif in MPa viene moltiplicata per 1.000 per ottenere kN/m². La massa superiore aumenta il peso proprio; il motore aggiorna massa e carico fino a variazione relativa non maggiore di 10⁻⁸, con massimo 80 iterazioni. Un mancato equilibrio del ciclo produce un rifiuto esplicito. Queste sono iterazioni interne di un singolo candidato, diverse dai tentativi dell’ottimizzazione.

Nell’arco con catena, due archi parabolici hanno freccia f = rapporto impostato × luce. Ogni piano porta metà del carico totale. La componente orizzontale per arco è H = qd l²/(16f); la compressione adottata per l’intero arco è √[H² + (qd l/4)²], la catena porta H e ogni pendino porta qd Δx/2. Gli archi sono divisi in 80 segmenti per stimarne lo sviluppo. Si assume la forza massima lungo ciascun arco; instabilità e pressoflessione reale non sono calcolate.

Nel reticolare l’altezza è il rapporto impostato per la luce. Due piani resistenti sono stimati con forza nei correnti qd l²/(16h). Le diagonali usano una forza convenzionale ottenuta dal massimo taglio e dall’inclinazione, applicata a tutti i pannelli. Questa regola dà quantità orientative e non risolve un reticolo con carichi mobili, nodi e controventi reali.

Nello strallato lo schema è simmetrico, L/4–L/2–L/4, con due antenne. L’altezza sopra impalcato è quella imposta oppure max(10 m; 0,20 della luce centrale). Gli stralli a ventaglio sostengono le fasce di lunghezza Δx e, per ciascun piano, hanno forza T = qd Δx/(2 sin α). Il modello assegna a ciascuna antenna metà del carico verticale complessivo e reazioni verticali nulle alle spalle per il solo impalcato. Non risolve la rigidezza relativa impalcato stralli antenne né la tesatura.

Nel sospeso la freccia del cavo principale è un rapporto della luce centrale; l’antenna deve superarla di almeno 2 m. Per ciascuno dei due cavi H = qd l²/(16f), T = √[H² + (qd l/4)²]. I cavi di riva sono anch’essi parabolici e i pendini sostengono anche le campate laterali. Il computo include i cavi di riva, non soltanto la campata centrale.

Per le reazioni del sospeso, Htot è la componente orizzontale dei due cavi calcolata con il carico pertinente; indicando con a la campata di riva e ht l’altezza antenna sopra impalcato, la reazione all’estremo è qa/2 − Htot ht/a. Può risultare negativa. La stima dei due blocchi è Vanc = 2[max(0; −Restremo,d) + Htot,d/μ]/25, con μ iniziale 0,5. Si tratta di peso stabilizzante convenzionale: non comprende una verifica di ribaltamento, pressioni eccentriche o stabilità geotecnica dell’ancoraggio.

Le antenne sono due fusti quadrati con traverso. La loro area tiene conto della reazione amplificata, del peso proprio amplificato e della tensione di riferimento; si applica anche una regola di snellezza. L’inerzia mostrata resta quella del solo impalcato. Per arco, reticolare, strallato e sospeso non vengono prodotti diagrammi globali di momento e freccia, perché richiederebbero un modello diverso. Non vanno sostituiti mentalmente con i diagrammi della trave ordinaria.

## Computo prezzi e indicatore ambientale

Il costo diretto è la somma delle quantità per i prezzi. Al risultato vengono applicati in successione oneri aggiuntivi e imprevisti. Con i valori iniziali 12% e 15%, il fattore complessivo è 1,288, non 1,27. L’intervallo iniziale ±30% è una fascia convenzionale scelta dall’utente, non un intervallo statistico di confidenza e non un vincolo dell’ottimizzazione.

$$ Cdiretto = Σ(Qj pj)
$$ Ctotale = Cdiretto × (1 + oneri/100) × (1 + imprevisti/100)

I valori correnti sono stati confrontati con ANAS NC MP 2026 Rev 1 [R1] e con Emilia Romagna 2026 [R2]. Le tariffe ANTHEA sono aggregate e arrotondate: si devono leggere inclusioni, esclusioni e unità. Il confronto non rende automatico l’adeguamento alla classe del calcestruzzo, all’esposizione, al varo o alla corsa degli appoggi.

| Voce ANTHEA | Prezzo iniziale | Natura del riferimento |
| --- | --- | --- |
| Cls impalcato | 260 €/m³ | Confronto C45/55 ANAS |
| Cls sottostrutture | 240 €/m³ | Aggregato fondazioni ed elevazioni |
| Armatura ordinaria | 1.660 €/t | ANAS 1,66 €/kg |
| Precompressione | 3.600 €/t | Riserva di sistema con accessori |
| Carpenteria ordinaria | 3.500 €/t | Base ANAS con varo ordinario |
| Carpenteria cassoni | 4.000 €/t | Maggiorazione convenzionale |
| Casseforme | 50 €/m² | Superfici equivalenti e prezzo medio |
| Pali da 1,0 e 1,5 m | 300 e 550 €/m | Perforazione e cls armatura separata |
| Appoggi | 5.000 €/cad | Indennità media non dimensionamento |
| Giunti | 2.400 €/m | Corsa moderata da confermare |
| Barriere | 360 €/m | Confronto bordo ponte H4 |
| Pavimentazione | 32 €/m² | Pacchetto convenzionale |
| Carpenteria ortotropa | 5.000 €/t | Aggregato non singola voce ANAS |
| Cavi e pendini | 16.000 €/t | Sistema installato da preventivare |
| Montaggio speciale | 1.000 €/t | Aggiunta per complessità speciale |

Il cls dei pali è incluso nella tariffa al metro e non viene nuovamente addebitato a volume; il volume resta conteggiato per l’impronta ambientale e per l’armatura. Gli appoggi ANAS sono articolati per forza e movimento, mentre qui si usa un importo medio a dispositivo. Le casseforme sono equivalenti; scavi, rinterri, drenaggi, impermeabilizzazione, protezioni, accessi, centine alte e sicurezza specifica non sono computati analiticamente. Per la carpenteria ortotropa la maggiorazione ANAS riguarda la lamiera interessata, mentre ANTHEA adotta un prezzo aggregato del sistema.

Il listino ANAS dichiara spese generali 15% e utile 10% già inclusi e tratta la sicurezza specifica separatamente [R1]. Per questo il 12% ANTHEA deve coprire soltanto oneri aggiuntivi non computati; aggiungerlo come nuova percentuale generale di impresa può produrre un doppio conteggio. Il montaggio speciale va azzerato o adattato quando già compreso nel preventivo.

La CO₂ somma cls e acciai e applica una maggiorazione convenzionale di trasporti e cantiere. Con masse in tonnellate e fattori dell’acciaio in kg/kg, il prodotto restituisce tonnellate di CO₂ equivalente. Il cls usa kg/m³ e richiede divisione per 1.000. I fattori iniziali sono 320 kg/m³ per cls, 1,4 kg/kg per armatura, 2 per carpenteria, 2,5 per precompressione e cavi; il cantiere aggiunge 15%.

$$ ECO2 = (Vcls fcls/1000 + Σ(ms fs)) × (1 + cantiere/100)

Le opzioni cls a ridotta CO₂ e acciaio riciclato moltiplicano rispettivamente il fattore del cls per 0,60 e quello della carpenteria per 0,35. Non riducono automaticamente armature, cavi o prezzi. I fattori non provengono da EPD specifiche; finiture, manutenzione, esercizio e fine vita restano esclusi. Una scelta a CO₂ minima è quindi minima per questo indicatore parziale, non per un’analisi completa del ciclo di vita.

La durata ordinaria è ceil(avvio + n × coefficiente campata + npile × coefficiente pila + aggiunte). I valori iniziali sono 4 mesi, 1,2 mesi/campata e 0,4 mesi/pila; i pali aggiungono 1,5 mesi, il cassone a conci altri 1,5 mesi/campata, le strutture superiori 6 + luce principale/50 mesi. È una stima parametrica, non un cronoprogramma e non è un obiettivo selezionabile della ricerca.

## Filtri di ammissibilità

Una combinazione viene esclusa se non è calcolabile o supera uno dei filtri seguenti. Le motivazioni sono registrate; una combinazione può avere più motivi, quindi la somma delle occorrenze per motivo può superare il numero delle combinazioni escluse.

1. Numero di campate fuori dai limiti, luci effettive fuori dal campo della famiglia, altezza sotto il minimo richiesto o la regola di predimensionamento, oppure altezza massima dell’impalcato superata.
2. Appoggi interni dentro l’ostacolo ampliato di 1 m per lato; schemi di continuità incompatibili con la tipologia; geometrie impossibili.
3. Rapporto assiale della fondazione maggiore di 1, snellezza delle pile maggiore di 100, compressione media maggiore di 0,30fc,sub o antenna oltre la tensione di riferimento.
4. Fusto non contenuto nel plinto, più di 64 pali per appoggio o griglia a interasse 3D non contenuta nel plinto.
5. Reazione negativa per tipologie diverse dal sospeso, quando servirebbero dispositivi antisollevamento non dimensionati; risultante di fondazione non compressa dopo i pesi propri.
6. Sovrapposizione delle travi a U, cassoni metallici ordinari oltre la larghezza disponibile, costo o CO₂ negativi o non finiti.

I controlli applicano piccole tolleranze numeriche ai confronti. Non sono filtri di resistenza a flessione o taglio dell’impalcato, fatica, instabilità locale, dinamica, sisma, vento, comfort, montaggio, trasporto o manutenzione. Allargare i range aumenta le alternative esplorate ma non aggiunge queste verifiche.

## Punteggi graduatoria e frontiera Pareto

Con Costo minimo il punteggio coincide con C; con CO₂ minima coincide con E. Il compromesso usa i minimi Cmin ed Emin delle soluzioni ammesse della stessa ricerca:

$$ S = 0,5 C/max(1 ; Cmin) + 0,5 E/max(10⁻⁹ ; Emin)

Il punteggio di compromesso è adimensionale e va minimizzato. I denominatori sono limitati inferiormente per gestire anche indicatori nulli. Non è una monetizzazione della CO₂ e non attribuisce un prezzo in euro a una tonnellata emessa. Il punteggio non va confrontato direttamente fra ricerche con minimi diversi. A parità di punteggio non arrotondato, l’ordinamento usa costo, CO₂, identificativo della famiglia e numero di campate; l’enumerazione è deterministica.

Una soluzione appartiene alla frontiera Pareto quando nessun’altra soluzione distinta ammessa ha costo e CO₂ entrambi non maggiori e almeno uno strettamente minore. Una soluzione dominata può comunque interessare per aspetti non rappresentati nei due indicatori, ad esempio costruibilità o minor interferenza idraulica; tali motivazioni devono essere valutate esternamente.

Il motore conserva l’elenco completo delle soluzioni distinte e una selezione iniziale delle prime alternative. La casella Mostra le prime N dell’interfaccia agisce sull’elenco completo, da 1 a 50.000 righe, e non ripete la ricerca. Il limite 1–50 dell’opzione tecnica Alternatives riguarda soltanto la selezione restituita dall’API, non il numero dei punti disponibili nella nuvola della finestra.

## Come sono stati validati i risultati

La verifica è articolata in quattro livelli. Il primo controlla le formule ideali con soluzioni note; il secondo la gestione del problema di ricerca; il terzo usa un’implementazione indipendente; il quarto confronta dati esterni o risultati storici. Tenere distinti questi livelli impedisce di scambiare un test software superato con una validazione fisica sperimentale.

### Suite del motore rieseguita

Il 30 settembre 2026, dopo la correzione dell’arrotondamento, la suite BridgeDesign.Checks ha superato 44.259 asserzioni. Comprende formule chiuse per una e due campate, 200 travi diseguali con equilibrio globale, 224 combinazioni famiglia terreno pila, quantità, fondazioni, prezzi, salvataggio, export e ottimizzazione. Un’asserzione è un singolo confronto: il numero non indica altrettanti progetti distinti.

La campagna geometrica dell’audit comprende 1.008 configurazioni, 874 calcolate e 134 rifiutate esplicitamente. I rifiuti comprendono 116 fondazioni automatiche non convergenti e 18 antenne incompatibili con la tensione di riferimento. Il rifiuto atteso non è un risultato strutturale favorevole: dimostra che il programma segnala il limite anziché produrre un numero non utilizzabile.

Per l’ottimizzazione sono state rieseguite 27 ricerche di campagna, pari a 10.491 valutazioni, oltre a una prova limite con prezzi e CO₂ nulli. Sette famiglie sono state esplorate con tipologia bloccata e tre obiettivi; due lunghezze, 120 e 480 m, sono state esplorate liberamente raggiungendo tutte le 14 famiglie. Le prove verificano riproducibilità, immutabilità dell’input, vincoli, traccia dei tentativi, graduatoria, numero di alternative e Pareto.

L’enumerazione diretta delle piccole griglie nella suite C# è esterna alla funzione Optimize, ma riusa Calculate e i filtri. Verifica quindi il meccanismo di ricerca, non costituisce un secondo modello strutturale indipendente. Questa distinzione è essenziale per interpretarne correttamente il risultato.

### Secondo solutore indipendente dal sito

È stato aggiunto un verificatore Python che non carica librerie ANTHEA e non chiama il sito. Per 100 travi riproducibili, da 1 a 12 campate, costruisce la matrice di rigidezza di elementi Euler Bernoulli, applica i carichi nodali consistenti, blocca gli spostamenti sugli appoggi e risolve le rotazioni. Le lunghezze sono fra 5 e 70 m, q fra 2 e 500 kN/m ed EI fra 10⁵ e 10⁹ kN m²; sono presenti schemi continui e campate indipendenti, con seme 30092026.

Le reazioni si ottengono dal residuo della matrice globale. Taglio e momento sono ricostruiti dalle forze di estremità. La deformata usa funzioni di Hermite con il termine particolare del carico uniforme, qx²(l − x)²/(24EI), che ha spostamento e rotazione nulli agli estremi. Questo evita di confrontare la soluzione esatta con una sola interpolazione cubica approssimata. I segni e le unità sono allineati prima del confronto.

| Grandezza | Scarto assoluto massimo |
| --- | --- |
| Reazione | 1,46 × 10⁻¹¹ kN |
| Momento | 4,88 × 10⁻¹⁰ kNm |
| Taglio | 3,10 × 10⁻¹¹ kN |
| Freccia nei punti confrontati | 3,06 × 10⁻¹⁰ mm |

Gli scarti sono compatibili con l’aritmetica in virgola mobile. La soglia usata è 10⁻⁷ in unità della grandezza più 2 × 10⁻⁸ volte il massimo valore assoluto dei due risultati. La prova riguarda le equazioni elastiche ideali e i punti confrontati, non l’accuratezza della freccia reale di un impalcato fessurato o precompresso.

### Computo e ottimo ricostruiti separatamente

Il secondo controllo indipendente considera una soletta piena lunga 120 m e larga 11,30 m, quota 12 m, campate indipendenti, due spalle, pile circolari di diametro imposto 2 m, plinti diretti con lato longitudinale imposto 6 m e terreno Roccia. Le campate esplorate sono 8–16; per ogni numero si esaminano 100, 110 e 120% dell’altezza di riferimento. Le quote di riferimento e gli arrotondamenti sono costruiti in aritmetica razionale nel verificatore, prima della conversione in decimali.

Il verificatore deriva autonomamente volumi di soletta, fusti, pulvini, spalle e plinti; armature per incidenza; casseforme; appoggi, giunti, barriere e pavimentazione. Ricostruisce il costo con i prezzi esplicitati e la CO₂ con i fattori documentati. Le fondazioni sono imposte per evitare che il controllo riproduca il ciclo automatico del motore. Sono confrontati tutti i costi, le quantità principali, l’ordine e le condizioni di dominanza delle 26 geometrie distinte, ottenute da 27 combinazioni nominali.

Il minimo indipendente è una soletta con 8 campate da 15 m, altezza 0,916667 m, costo 2.270.714,97 euro, CO₂ 1.342,665 t, cls totale 2.266,445 m³ e armatura 315,909 t. Il massimo scarto di costo sull’intera griglia è 2,33 × 10⁻⁹ euro e quello della CO₂ 2,05 × 10⁻¹² t. Complessivamente il nuovo verificatore esegue 80.133 confronti numerici, oltre ai controlli di cardinalità, quote, quantità, ordine e Pareto.

Questo test ha rilevato il difetto 0,80 → 0,85 m relativo alle 11 campate al 120%. È stato corretto l’arrotondamento a 5 cm e sono state ripetute sia la prova indipendente sia la suite generale. Il difetto alterava alcune alternative; nel caso controllato non cambiava il vincitore. La prova certifica il minimo economico della griglia del caso imposto, senza estendere automaticamente tale conclusione a ogni famiglia e ogni combinazione possibile.

### Confronto storico con il sito

La campagna storica contiene 1.000 input unici realmente acquisiti dalla UI, 125 per ciascuna delle otto famiglie originali: 600 casi automatici, 160 variazioni d’altezza, 120 del numero di campate e 120 della resistenza del cls. Ogni caso è stato eseguito nel nostro motore in modalità native e resolved, per 2.000 esecuzioni. Nella seconda modalità alcune dimensioni adottate dal sito sono state imposte in ANTHEA, per separare l’effetto delle regole automatiche dalle altre differenze.

L’esito storico è stato 939 casi calcolati con differenze e 61 rifiutati in native; 932 calcolati con differenze e 68 rifiutati in resolved. Nessun caso coincideva su tutti gli indicatori confrontati. Non era quindi una prova di equivalenza. La mediana assoluta dello scarto relativo sul costo native era 33,28%, ma i listini e i perimetri erano diversi: quel valore non misura da solo l’errore di un solutore.

Una successiva regressione del 27 settembre sugli stessi input congelati ha prodotto 1.872 calcoli e 128 rifiuti, senza risultati non finiti. Questa è una riesecuzione su dati salvati, non una nuova acquisizione del sito e non una nuova dichiarazione di parità. Le sei famiglie aggiunte non appartengono al campione originario. Nel lavoro del 30 settembre la validazione nuova è il confronto indipendente descritto sopra; non sono stati acquisiti altri 1.000 casi live.

## Esempio operativo e lettura critica del vincitore

Un esempio distinto, utile per l’interfaccia, considera 120 m di lunghezza, 20,20 m di larghezza, quota 12 m, terreno Roccia, nessun ostacolo, tre campate iniziali in c.a.p. a I, continuità, pila a setto e fondazione automatica che risolve a plinto. La ricerca conserva pila, fondazione e continuità; libera tipologia e campate fra 1 e 8 e usa altezze 100–120% con passo 10.

Nel calcolo documentato il riferimento costa 2.332.503,86 euro ed emette 1.371,939 t di CO₂. La ricerca produce 91 tentativi, 70 ammessi e 69 geometrie distinte. Vince un cassone in c.a.p. con due campate da 60 m e altezza 2,565257 m: 2.049.525,73 euro e 1.287,035 t. In questo esempio lo stesso candidato minimizza costo e CO₂, quindi coincide anche con il compromesso. Non è un risultato generale della ricerca multiobiettivo.

Imponendo almeno cinque campate, il riferimento a tre campate non è più ammesso. Il nuovo minimo è una soluzione a T in c.a. a cinque campate, con costo 2.657.209,43 euro. Il costo superiore al vecchio riferimento non è un fallimento dell’ottimizzatore: è cambiato l’insieme delle soluzioni consentite. Moltiplicando tutti i prezzi per 0,8 o 1,2, la geometria vincente resta uguale e il costo scala della stessa quantità, mentre la CO₂ resta invariata.

![Costo e CO₂ delle geometrie ammesse nell’esempio da 120 m](figure/costo-co2.png)

La nuvola evidenzia alternative spesso vicine. Uno scarto di costo del 2% non basta per scegliere con sicurezza quando alcune voci aggregate hanno incertezza del 20–30% o maggiore. Per decidere serve ripetere la ricerca con ipotesi geotecniche e prezzi plausibili, approfondire i primi candidati e verificare se la scelta resta stabile. La fascia ±30% mostrata dal software non esegue questa analisi: è necessario variare realmente gli input e ricalcolare.

## Cosa rimane da validare prima dell’uso progettuale

Non sono disponibili, in questa attività, confronti sperimentali, consuntivi economici di ponti costruiti, una calibrazione statistica dei rapporti luce altezza, verifiche normative complete di tutte le alternative o una validazione indipendente completa delle strutture speciali. I test delle famiglie speciali controllano geometrie, equilibri ideali, masse, filtri e riproducibilità, ma non sostituiscono un’analisi strutturale dedicata.

Per portare un’alternativa allo studio di fattibilità occorre almeno definire il reale schema statico e costruttivo, l’inviluppo di traffico, i materiali, i dettagli di impalcato, il modello geotecnico, i vincoli territoriali e le lavorazioni mancanti. I controlli di sezioni, fasi, fatica, stabilità, fondazioni e appoggi devono essere eseguiti con modelli adeguati. Se queste verifiche aumentano le quantità, vanno riportate nel computo e nel confronto delle alternative.

Un buon uso del modulo consiste nel restringere un insieme di idee a poche alternative leggibili e riproducibili. Il risultato da conservare è l’insieme dati ipotesi soluzione quantità avvisi, insieme al motivo per cui è stata scelta un’alternativa. La sola etichetta ottimo non è una giustificazione progettuale.

## Tracciabilità e ripetizione delle prove

Le sorgenti principali sono X.Calculations/BridgeConcept.Optimization.cs, BridgeConcept.Calculation.cs, BridgeConcept.Foundation.cs, BridgeConcept.AdvancedDeck.cs, BridgeConcept.AdvancedStructure.cs e BridgeConcept.AdvancedCalculation.cs. La descrizione dei prezzi è in BridgeConcept.Pricing.cs; i prospetti tecnici in BridgeConcept.Technical.cs. La UI della ricerca è separata nei file BridgeDesignOptimization.cs e BridgeDesignOptimizationPanel.cs.

La nuova prova indipendente è in supporto/test/BridgeDesign.IndependentChecks. Program.cs esporta i risultati osservati del motore; verify.py costruisce i risultati attesi separatamente. Le evidenze finali sono in supporto/artefatti/bridge-design-guide-20260930/indipendente-corretto, con osservati.json e independent-summary.json. Il corpus precedente alla correzione è conservato nella sottocartella indipendente. La suite generale finale è nella sottocartella calcoli-corretti; gli esempi operativi sono nella sottocartella esempi.

Per ripetere dalla radice del repository, eseguire nell’ordine i comandi seguenti. Per Python usare un interprete con NumPy; nel lavoro descritto è stato usato il runtime Python fornito da Codex. La destinazione può essere sostituita con una nuova cartella di artefatti per conservare gli esiti precedenti.

dotnet run --project supporto/test/BridgeDesign.Checks -c Release -- supporto/artefatti/nuova-verifica/calcoli

dotnet run --project supporto/test/BridgeDesign.IndependentChecks -c Release -- supporto/artefatti/nuova-verifica/indipendente

python supporto/test/BridgeDesign.IndependentChecks/verify.py supporto/artefatti/nuova-verifica/indipendente

I file sorgente, i risultati e le versioni dei documenti sono identificati nel manifest JSON degli artefatti di questa attività. Il rapporto va riletto quando cambiano formule, filtri, listini o funzioni dell’ottimizzatore: i risultati di una vecchia campagna non validano automaticamente una revisione successiva.

## Riferimenti

[R1] ANAS, Elenco prezzi 2026 Rev 1, Nuove costruzioni e manutenzione programmata, giugno 2026. Consultato il 30 settembre 2026. Usato per il riscontro delle tariffe e delle inclusioni, non per le regole di dimensionamento. https://www.stradeanas.it/it/elenco-prezzi

[R2] Regione Emilia Romagna, Elenco regionale prezzi 2026 e correzioni 2026. Riscontro territoriale delle tariffe; non attribuisce una localizzazione al progetto. https://territorio.regione.emilia-romagna.it/osservatorio/elenco_regionale_prezzi/prezzario-2026

[R3] TheBridgeEng, Bridge Design. Riferimento della campagna storica conservata in bridge_design_site_1000. Non usato nel nuovo verificatore indipendente. https://thebridgeeng.com/design

[R4] FHWA IF 12 027, Manual for Design Construction and Maintenance of Orthotropic Steel Deck Bridges, febbraio 2012. Riferimento tecnico per l’estensione reale delle verifiche ortotrope. https://www.fhwa.dot.gov/bridge/pubs/if12027/if12027.pdf

[R5] TU Delft, Computational Modelling, Euler Bernoulli beam elements, capitolo 4.1. Riferimento della formulazione per rigidezze e funzioni di Hermite del verificatore indipendente. https://teachbooks.tudelft.nl/computational-modelling/structural_linear/euler_bernouilli.html

[R6] Decreto 17 gennaio 2018, Aggiornamento delle Norme tecniche per le costruzioni, pubblicazione in GU 20 febbraio 2018. Riferimento del quadro normativo, non certificazione del predimensionamento. https://www.gazzettaufficiale.it/eli/id/2018/2/20/18A00716/sg
