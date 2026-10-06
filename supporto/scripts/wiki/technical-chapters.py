"""Original technical chapters, kept here as reproducible inputs to the global manual."""
import sys as _sys
# W0.5: the external corpus (adapted articles, reading lists, links) was removed on 6/10/2026.
_sys.exit('Script di migrazione superato, non rieseguire: rigenererebbe contenuti esterni eliminati il 6/10/2026.')
CHAPTERS=[]
def add(key,title,chapter,area,modules,related,body):
    CHAPTERS.append(dict(key=key,title=title,chapterId=chapter,area=area,modules=modules,related=related,status='reviewed',body='## '+title+'\n\n'+body.strip()+'\n'))

add('geotecnica-parametri','Dalle indagini geotecniche ai parametri del modello','geotecnica','Fondazioni e geotecnica',['geo_palo_verticale','geo_micropalo_verticale','geo_palo_orizzontale','geo_muri_sostegno'],['bearing-capacity','mdp-89','mdp-194','mdp-201'],r'''
Un parametro geotecnico è significativo soltanto insieme a stato tensionale, drenaggio, deformazione e volume di terreno rappresentato. Una correlazione può dare un numero plausibile senza rappresentare il meccanismo che si sta verificando. Le indagini devono quindi essere lette come informazioni su un modello geologico e geotecnico, non come un catalogo automatico di resistenze.

### Tensioni efficaci e condizioni di drenaggio

Per il terreno saturo, σ′ = σ − u separa tensione totale e pressione interstiziale. In condizioni drenate si impiegano parametri efficaci coerenti, ad esempio τf = c′ + σ′n tan φ′. Una descrizione non drenata in tensioni totali può usare su per il problema e percorso di carico considerati: non si deve sommare arbitrariamente su alla resistenza efficace. La scelta dipende dal rapporto fra durata dell’azione e tempi di dissipazione, non soltanto dal nome litologico.

Per una falda idrostatica a piano campagna, γsat = 20 kN/m³, γw = 9,81 kN/m³ e z = 6 m, σv = 120 kPa, u = 58,86 kPa e σ′v = 61,14 kPa. Con c′ = 0 e φ′ = 30 gradi, una superficie orizzontale soggetta a tale tensione normale efficace avrebbe τf ≈ 35,30 kPa nel criterio di Mohr-Coulomb. Usare 120 kPa al posto di 61,14 kPa quasi raddoppierebbe il contributo di attrito. L’esempio riguarda il criterio locale, non la capacità portante di una fondazione.

### Che cosa misurano CPT e SPT

La CPT distingue resistenza alla punta e attrito laterale; una CPTu aggiunge pressione interstiziale e può richiedere correzioni della resistenza di punta. I rapporti normalizzati dipendono dalle tensioni di riferimento. Lo SPT restituisce un conteggio di colpi nelle condizioni della prova: energia, attrezzatura, diametro del foro, aste e sovraccarico possono richiedere correzioni prima dell’uso in una specifica correlazione. Il valore grezzo non è intercambiabile con N60 o con un valore normalizzato alla pressione di riferimento.

Una discontinuità nei segnali può suggerire un cambio di unità geotecnica, ma l’interpretazione va confrontata con sondaggi, campioni, falda e storia geologica. Strati sottili e terreni cementati richiedono particolare attenzione. Una correlazione empirica deve conservare autore, campo di calibrazione e unità.

### Dal profilo alle verifiche ANTHEA

Per pali e micropali distinguere strati attraversati e tratto resistente, resistenza di base e laterale, compressione e trazione. Nel palo elastico il modulo di reazione è una rigidezza del modello d’interazione: non è il modulo di Young del terreno. Per i muri, il modello locale attuale richiede riempimento granulare con c′ = 0; inserire una stratigrafia non estende automaticamente il modello a un’argilla non drenata.

Letture integrative: [GeoStru, drenaggio e verifiche geotecniche](https://blog.geostru.eu/condizioni-drenate-e-non-drenate-nelle-verifiche-geotecniche/); [GeoStru, prova CPT](https://blog.geostru.eu/la-prova-penetrometrica-statica-cpt-cone-penetration-test/); [FHWA, raccolta sulle fondazioni](https://www.fhwa.dot.gov/engineering/geotech/foundations/). Il testo e l’esempio sono una trattazione originale; i collegamenti permettono il confronto con le fonti.
''')

add('cedimenti-consolidazione','Cedimenti edometrici tempi di consolidazione e drenaggi','geotecnica','Fondazioni e geotecnica',['geo_muri_sostegno'],['geotecnica-parametri','bearing-capacity'],r'''
La verifica di resistenza non determina il cedimento. Il cedimento dipende da distribuzione dell’incremento di tensione, compressibilità, storia di carico e tempo. La prova edometrica riproduce una compressione con deformazione laterale impedita: è coerente con una schematizzazione monodimensionale, non con ogni configurazione tridimensionale della fondazione.

### Integrazione per strati

Assumendo un modulo edometrico Eoed costante nell’intervallo di tensione del singolo strato, la deformazione è Δσ′/Eoed e il contributo al cedimento è H Δσ′/Eoed. Per uno strato di 2 m con incremento efficace medio 60 kPa ed Eoed = 12000 kPa, s = 0,010 m, cioè 10 mm. Usare lo stesso modulo su un percorso che supera la tensione di preconsolidazione può sottostimare la compressibilità: occorre seguire la curva di prova o un modello appropriato.

Gli incrementi di tensione non sono uniformi sotto una fondazione finita. Suddividere il terreno in strati serve anche a rappresentarne l’attenuazione con la profondità. La distorsione angolare dipende dai cedimenti differenziali e dalla distanza, mentre una traslazione uniforme ha effetti diversi sulla sovrastruttura.

### Il ruolo della lunghezza drenante

Nel modello classico monodimensionale con coefficiente cv costante, il fattore di tempo è Tv = cv t/Hdr². Hdr è il massimo percorso di drenaggio: per uno strato di spessore H con drenaggio sopra e sotto vale H/2, mentre con una sola faccia drenante vale H. A parità di grado medio di consolidazione, raddoppiare Hdr quadruplica il tempo. Per un incremento iniziale uniforme, al 90% si usa il valore classico Tv ≈ 0,848.

Esempio: strato H = 4 m, cv = 10⁻⁷ m²/s. Con doppio drenaggio Hdr = 2 m, t90 ≈ 33,92 milioni di secondi, circa 393 giorni. Con drenaggio da una sola faccia si passa a circa 1570 giorni. La costruzione per fasi e cv variabile modificano questa stima; il cedimento secondario non è incluso nella soluzione di consolidazione primaria.

### Acqua e opere di sostegno

La previsione del drenaggio deve restare distinta dal comportamento idraulico reale: intasamento, recapito insufficiente o guasto possono cambiare la falda. La spinta dell’acqua non si elimina scegliendo una tensione efficace del terreno. In ANTHEA i cedimenti del muro richiedono l’attivazione del calcolo e dati propri; non sono dedotti dall’esito favorevole a portanza.

Approfondimenti: [GeoStru, prova edometrica](https://blog.geostru.eu/la-prova-edometrica/), [GeoStru, piezometro](https://blog.geostru.eu/il-piezometro/), [FHWA GEC 6, fondazioni superficiali](https://www.fhwa.dot.gov/engineering/geotech/pubs/010943.pdf). Esempi originali con ipotesi esplicite.
''')

add('geofisica-liquefazione','Profilo di velocità risposta locale e liquefazione','geotecnica','Fondazioni e geotecnica',[],['dinamica-e-sisma-del-modello','mdp-193','mdp-152'],r'''
Una misura geofisica vincola un modello del sottosuolo, ma raramente ne identifica uno solo. Un profilo di Vs compatibile con la dispersione delle onde superficiali non è automaticamente l’unica stratigrafia possibile. Profondità esplorata, intervallo delle frequenze, modi interpretati e informazioni indipendenti determinano la qualità dell’inversione.

### MASW e HVSR rispondono a domande diverse

La MASW utilizza la dispersione; l’HVSR descrive un rapporto spettrale fra componenti del moto registrato. La presenza di un picco può aiutare a individuare contrasti, ma la sua interpretazione non equivale senza ulteriori ipotesi a una funzione di amplificazione del sito. La combinazione con sondaggi e altre misure restringe le soluzioni ammissibili. Un buon adattamento numerico ai dati non elimina la non unicità.

Per un singolo strato omogeneo sopra un substrato molto più rigido, il modello ideale a propagazione verticale suggerisce f0 ≈ Vs/(4H). Con Vs = 200 m/s e H = 20 m si ottiene 2,5 Hz. Anche Vs = 300 m/s e H = 30 m producono lo stesso valore: una sola frequenza non determina separatamente velocità e spessore. La stratificazione reale, lo smorzamento e i contrasti finiti richiedono modelli più completi.

### Dal piccolo al grande livello deformativo

Vs e densità permettono di stimare Gmax = ρVs² nel campo delle piccole deformazioni. Con ρ = 1900 kg/m³ e Vs = 200 m/s, Gmax = 76 MPa. Non è il modulo secante da usare indiscriminatamente sotto un carico statico importante: rigidezza e smorzamento cambiano con la deformazione. Un’analisi equivalente lineare o non lineare deve documentare curve, input e trattamento del segnale.

### Liquefazione e sovrappressioni

Il problema riguarda la perdita di tensione efficace conseguente all’accumulo di sovrappressioni in condizioni cicliche suscettibili. Nel confronto semplificato domanda–resistenza, CSR e CRR devono appartenere alla stessa procedura e alle stesse condizioni di riferimento; magnitudo, pressione di confinamento, contenuto di fini e normalizzazioni non si possono combinare prendendo fattori da correlazioni diverse. La granulometria da sola non chiude la verifica.

ANTHEA non esegue automaticamente inversione MASW/HVSR, risposta sismica locale o liquefazione. Il loro esito può condizionare l’applicabilità dei moduli geotecnici. Letture: [GeoStru, non unicità nell’analisi MASW e HVSR](https://blog.geostru.eu/analisi-congiunta-masw-hvsr-non-unicita/), [GeoStru, liquefazione](https://blog.geostru.eu/liquefazione-dei-terreni-cose-e-come-intervenire/). L’esempio a un solo strato è redatto per illustrare il limite dell’identificazione, non per interpretare una prova reale.
''')

add('geotecnica-gruppi','Pali in gruppo interazione e rigidezza della fondazione','geotecnica','Fondazioni e geotecnica',['geo_palo_verticale','geo_palo_orizzontale','geo_efficienza_orizzontale'],['palo-elastico','guida-palificata-orizzontale'],r'''
Il comportamento di una palificata non si ricava moltiplicando ogni risultato del palo singolo per il numero di pali. Capacità ultima, cedimenti, rigidezza orizzontale e ripartizione delle azioni sono problemi distinti. Un coefficiente di efficienza relativo alla resistenza non è automaticamente un moltiplicatore della rigidezza.

### Equilibrio del plinto e compatibilità

Nel modello ideale di plinto rigido con pali verticali di uguale rigidezza assiale, disposti simmetricamente rispetto agli assi principali, la forza sul palo i può essere scritta come N/n più contributi proporzionali alle coordinate. Per flessione attorno a un solo asse, Ni = N/n + M yi/Σyj². La formula presuppone la compatibilità rigida del plinto, linearità e uguale rigidezza; non descrive da sola non linearità, pali inclinati, terreno a contatto o interazione fra pali.

Quattro pali a y = ±1,5 m, due per lato, con N = 4000 kN e M = 1200 kNm hanno Σy² = 9 m². Le azioni diventano 1200 kN sui due pali da un lato e 800 kN sugli altri. La somma è 4000 kN e il momento è 1200 kNm: questi due controlli devono riuscire prima di interpretare le resistenze. Se una combinazione produce trazione, vanno verificati sia il palo sia il collegamento al plinto.

### Carichi orizzontali e fila schermata

Lo spostamento della testa e la rotazione del plinto controllano la deformata. L’interazione tra pali modifica la reazione del terreno; direzione del carico, interassi e posizione nella fila possono rendere differenti i pali interni e di bordo. Un’efficienza complessiva non restituisce necessariamente momenti e tagli di ciascun palo.

Nel modulo elastico di ANTHEA controllare EJ, modulo di reazione, discretizzazione e vincoli. Nel modulo di efficienza orizzontale leggere il campo della correlazione selezionata; nel palo verticale distinguere riduzioni di gruppo da coefficienti parziali e fattori di correlazione. L’uso successivo di questi strumenti richiede un modello coerente di ripartizione.

Letture: [GeoStru, pali in gruppo](https://blog.geostru.eu/pali-in-gruppo/), [GeoStru, calcolo dei pali in gruppo](https://blog.geostru.eu/calcolo-dei-pali-in-gruppo/), [FHWA GEC 9, fondazioni profonde caricate lateralmente](https://www.fhwa.dot.gov/engineering/geotech/pubs/hif18031.pdf). Esempio di equilibrio originale.
''')

add('muri-metodi-perimetro','Muri di sostegno metodi disponibili e confronto funzionale','geotecnica','Fondazioni e geotecnica',['geo_muri_sostegno'],['guida-muri-di-sostegno-con-stratigrafie-di-monte-e-valle','geotecnica-parametri','cedimenti-consolidazione'],r'''
Nel modulo muri di ANTHEA le verifiche locali, la stabilità globale, le resistenze strutturali e gli spostamenti sono valutazioni separate. Un esito favorevole allo scorrimento non dimostra l’assenza di una superficie di scivolamento profonda; una fessura accettabile nel fusto non garantisce la capacità della fondazione. Questa distinzione serve anche per confrontare correttamente programmi diversi.

### Il campo geometrico del modello locale

La configurazione attuale è una sezione piana per metro di sviluppo, con paramento di monte verticale e riempimento orizzontale granulare c′ = 0. Sono disponibili muro a mensola in c.a. e muro a gravità trapezoidale. Le stratigrafie di monte e valle descrivono terreni e falda; non abilitano automaticamente ogni geometria della superficie di spinta. Le formulazioni pseudostatiche operano con le ulteriori restrizioni indicate nel modulo.

In Rankine, per terreno granulare orizzontale e parete liscia, Ka = (1 − sin φ)/(1 + sin φ). Con φ = 30 gradi vale 1/3. Per H = 4 m e γ = 18 kN/m³, la risultante triangolare vale 48 kN/m ed è applicata a H/3 dal piede. Una falda alta 4 m aggiungerebbe, nell’idealizzazione idrostatica, circa 78,48 kN/m di pressione dell’acqua, mentre la componente del terreno andrebbe ricalcolata in tensioni efficaci. Non è corretto mantenere il peso saturo nella spinta efficace e poi aggiungere l’acqua.

### Bishop e spostamento permanente

La ricerca Bishop usa superfici circolari e un equilibrio a conci semplificato: numero dei conci, dominio di ricerca, falda e stratigrafia influenzano il risultato. Allargare il dominio e controllare superfici prossime al bordo aiuta a riconoscere un minimo condizionato dalla ricerca. Non è una verifica di cinematismi non circolari o di ogni possibile rottura progressiva.

Il Newmark implementato richiede accelerogrammi e soglia di scorrimento assegnata. Lo spostamento si accumula durante gli intervalli di moto del blocco idealizzato, con arresto coerente; non si ottiene integrando due volte l’intero accelerogramma senza condizioni di scorrimento. Soglia, orientamento, scala e stato limite del segnale vanno documentati. Questo metodo non coincide con una formula empirica di spostamento basata su parametri sintetici.

### Confronto con il prodotto Madosoft consultato il 6 ottobre 2026

| Ambito dichiarato dal prodotto esterno | Riscontro in ANTHEA |
| --- | --- |
| Muri in c.a. e a gravità, verifiche locali e globali | Presenti entro il campo del modello; Bishop richiede i propri dati e attivazione |
| Pressoflessione, taglio e fessurazione | Presenti; leggere completezza di materiali, armature e combinazioni |
| Terrapieno inclinato, geometria e parametri di spinta più generali | Copertura parziale: il nostro modello locale resta a monte verticale, riempimento orizzontale e c′ = 0 |
| Parametri sismici ricavati dalla località e spettro | Il modulo muri usa kh e kv assegnati oppure ag/g e F0 inseriti; non integra la selezione geografica e lo spettro completo |
| Spostamento secondo Richards ed Elms | Non implementato; è disponibile Newmark con input propri |
| Esecutivi DXF e DWG | Non disponibili; tavola e distinta attuali sono un predimensionamento con esportazioni documentali e immagine |
| Documentazione di progetto e manutenzione | Report e distinta presenti; manca un pacchetto equivalente completo di relazioni specialistiche e piano di manutenzione |

Il confronto riguarda funzionalità dichiarate pubblicamente e riscontri nel codice locale, non una validazione numerica del programma esterno. Non si conclude quindi che i due software siano equivalenti. I metodi mancanti non sono stati aggiunti ai calcoli con questa revisione documentale.

Fonti: [Madosoft, muri di sostegno](https://www.madosoft.it/prodotti/software-tecnico/cemento-armato/muri-di-sostegno), [verifica SLD](https://www.madosoft.it/prodotti/software-tecnico/cemento-armato/muri-di-sostegno/verifica-sld), [relazioni prodotte](https://www.madosoft.it/prodotti/software-tecnico/cemento-armato/muri-di-sostegno/relazioni). In ANTHEA il campo è dichiarato nei modelli RetainingWall; leggere anche gli avvisi del foglio corrente.
''')

add('esempi-caffe-lettura','Come ripercorrere gli esempi strutturali di Simone Caffè','meccanica','Fondamenti di ingegneria strutturale',[],['beam','euler','sezione-composta-da-ponte'],r'''
La raccolta di Simone Caffè comprende esempi e dispense utili per ricostruire un calcolo completo. Nella Wiki le risorse sono distribuite per argomento e collegate ai capitoli pertinenti; i PDF originali restano sul sito dell’autore. Il testo di questa pagina propone un metodo di lettura e controlli indipendenti, senza riprodurre le dispense.

### Ricostruire un risultato senza copiare le sole formule

Prima di seguire il procedimento, annotare geometria, vincoli, combinazioni, convenzioni e norma. Poi riprodurre un passaggio d’equilibrio e uno di compatibilità. Per una trave, verificare somma delle forze e dei momenti; per una sezione, ricostruire risultanti delle tensioni; per un telaio, controllare il numero e il significato dei gradi di libertà. Il confronto è valido solo se modello e input coincidono.

In un esempio di trave composta, separare acciaio solo, getto, collaborazione, ritiro e viscosità. In un esempio di dominio N–M, controllare il segno della compressione, le deformazioni limite e la discretizzazione. In una connessione, identificare l’intero percorso del carico: bulloni, piatti, saldature e parti collegate possono governare in modi diversi.

### Due controlli rapidi utili

Per una mensola elastica con carico P in punta, L = 3 m, E = 210 GPa e I = 8 × 10⁻⁵ m⁴, con P = 10 kN si hanno momento all’incastro di 30 kNm e spostamento PL³/(3EI) ≈ 5,36 mm. Una differenza di tre ordini di grandezza suggerisce prima di tutto un errore fra m e mm o fra Pa e MPa.

Per l’asta compressa ideale con vincoli incernierati, Ncr = π²EI/L²: con gli stessi E, I e L risulta circa 18423 kN. Questo è un carico critico elastico ideale, non la resistenza di progetto della membratura. Sostituire la lunghezza libera d’inflessione con metà del valore quadruplica Ncr: la scelta dei vincoli pesa più di molti arrotondamenti.

### Norme e strumenti citati nelle dispense

La raccolta contiene anche riferimenti a NTC2008, ACI318, Eurocodici e software esterni. Il nome dell’argomento non rende equivalenti coefficienti, classi di sezione, domini e criteri di verifica. Il rimando a un modulo ANTHEA segnala un tema comune; non promette la riproduzione automatica di ogni esempio. Le risorse su punzonamento, connessioni, legno o macchine vibranti ampliano la formazione anche quando non esiste un modulo corrispondente.

Fonte: [Simone Caffè, documenti di ingegneria](https://www.simonecaffe.it/index.php/ingegneria/documenti) e [dispense didattiche](https://www.simonecaffe.it/index.php/didattica/dispense). Gli esempi numerici di questa pagina sono originali e indipendenti dai PDF collegati.
''')

add('taglio-traliccio','Taglio nel calcestruzzo armato e scelta del traliccio','calcestruzzo','Calcestruzzo armato',['str_palo','geo_palo_orizzontale','geo_muri_sostegno'],['sezione-in-calcestruzzo-armato','mdp-265','mdp-202'],r'''
L’inclinazione del puntone cambia contemporaneamente la forza nelle staffe e la compressione nel calcestruzzo. Aumentare cot θ aiuta il ramo delle staffe, ma riduce quello del puntone: il valore più favorevole dipende da quale meccanismo governa. Questo approfondimento sviluppa il caso con armatura trasversale verticale, z = 0,9d e modello a inclinazione variabile; non estende automaticamente il risultato a torsione, sisma o sezioni prive di staffe.

### Due meccanismi che devono resistere insieme

Posto x = cot θ, Asw è l’area dei rami efficaci della staffa e s il passo. Le espressioni delle NTC 2018 §4.1.2.3.5.2, specializzate a staffe verticali, possono essere scritte come segue. Nella notazione adottata ν = 0,5 è il fattore della resistenza del calcestruzzo usato dalla formulazione NTC citata; αc dipende dallo stato di compressione e non va assunto sempre unitario.

```math
A=z\frac{A_{sw}}{s}f_{yd}
B=z b_w\alpha_c\nu f_{cd}
V_{Rsd}=Ax
V_{Rcd}=B\frac{x}{1+x^2}
V_{Rd}=\min(V_{Rsd},V_{Rcd})
```

Con le grandezze geometriche in mm e le tensioni in MPa, A e B sono in N. Il campo della formulazione citata è 1 ≤ x ≤ 2,5. Non basta verificare le staffe: occorre controllare anche il puntone e i dettagli costruttivi, compresi ancoraggio e armatura longitudinale richiesta dal meccanismo resistente.

### Perché la scelta ottimale non è sempre quarantacinque gradi

Nel campo x ≥ 1 il primo ramo cresce e il secondo non cresce. L’intersezione, se reale, è data da x² = B/A − 1. Se B/A ≤ 2 governa il puntone già per x = 1, quindi il massimo si trova all’estremo x = 1. Se 2 < B/A < 7,25 il massimo coincide con l’intersezione. Se B/A ≥ 7,25 il ramo delle staffe governa fino a x = 2,5, che diventa l’estremo ottimale. Vale per A > 0 e B > 0: con staffe assenti questo problema non descrive la verifica senza armatura a taglio.

La massimizzazione è un confronto fra modelli di resistenza nello stesso stato limite. Non autorizza a cambiare θ separatamente in verifiche che richiedono un traliccio coerente, né a ignorare le prescrizioni del comportamento dissipativo.

### Esempio numerico con due quantità di staffe

Si assumono bw = 300 mm, d = 500 mm, z = 450 mm, fck = 30 MPa, αcc = 0,85, γc = 1,5, fyk = 450 MPa, γs = 1,15, αc = 1. Risultano fcd = 17 MPa, fyd = 391,30 MPa e B = 1147,50 kN.

| Staffatura | Asw | A | x ottimale | VRd |
| --- | --- | --- | --- | --- |
| Due rami Ø8 ogni 200 mm | 100,53 mm² | 88,51 kN | 2,50 | 221,28 kN |
| Due rami Ø10 ogni 100 mm | 157,08 mm² | 276,60 kN | 1,774 | circa 490,80 kN |

Nel primo caso l’intersezione sarebbe oltre x = 2,5: non è ammissibile usarla. A x = 2,5 il puntone porta circa 395,69 kN, ma le staffe limitano la resistenza a 221,28 kN. Nel secondo caso l’intersezione cade nel campo ammesso. Imporre sempre x = 2,5 darebbe ancora circa 395,69 kN, inferiore al massimo compatibile: più staffe non implica che convenga mantenere l’inclinazione più bassa del puntone.

![Resistenze di staffe e puntone al variare di cotangente theta per i due esempi](../../X.Desktop/Assets/Wiki/taglio-traliccio.png)

### Come leggere il risultato in ANTHEA

Nel modulo della sezione in c.a. controllare separatamente bw, d, Asw, passo, inclinazione delle staffe, criterio e cot θ per ciascuna direzione. Un valore geometrico suggerito deve essere coerente con la disposizione effettiva delle barre. Confrontare il taglio richiesto con entrambi i rami resistenti e leggere gli avvisi del profilo normativo scelto. Il calcolo di una sezione non sostituisce il progetto del dettaglio lungo l’elemento.

La fessura inclinata attraversa un tratto finito di trave: è per questo che la resistenza coinvolge un insieme di staffe e un flusso di compressione, mentre la flessione ordinaria viene spesso ricondotta all’equilibrio di una sola sezione. Vicino a carichi concentrati, appoggi o brusche discontinuità geometriche può essere necessario un modello a puntoni e tiranti dedicato.

Fonti: [NTC 2018, §4.1.2.3.5](https://www.gazzettaufficiale.it/eli/gu/2018/02/20/42/so/8/sg/pdf); [articolo di Marco De Pisapia sull’inclinazione dei puntoni](https://www.marcodepisapia.com/verifica-a-taglio-cemento-armato/). Sviluppo algebrico, grafico ed esempi numerici di questa pagina sono redatti per ANTHEA.
''')

add('ponti-solette','Solette da ponte e percorso locale delle azioni','ponti','Ponti',['str_palo','str_bridge_design'],['bridge','taglio-traliccio'],r'''
La soletta distribuisce le azioni delle ruote e trasferisce le reazioni alle travi. Il suo comportamento locale non coincide con quello della trave principale: direzione dell’armatura, interasse degli appoggi, sbalzi e area caricata controllano modelli differenti. Una striscia di un metro è utile per capire l’equilibrio, ma non rappresenta automaticamente la distribuzione bidimensionale di una ruota.

### Piastra continua e mensola laterale

Tra le travi, la soletta lavora come piastra con rigidezze e vincoli da identificare. Sullo sbalzo il momento negativo tende la faccia superiore: l’armatura principale deve attraversare il collegamento con la parte interna e avere ancoraggio adeguato. La barriera può introdurre azioni concentrate, momento e torsione; la sua classe e il modo di trasferimento al cordolo determinano una verifica locale distinta dal traffico ordinario.

Le azioni da impalcato vanno separate secondo natura e fase: peso del getto, pavimentazione, cordoli, veicoli, frenatura, temperatura e azioni eccezionali non sono intercambiabili. Un modello a graticcio deve riprodurre anche la rigidezza trasversale; assegnare tutta la rigidezza flessionale alle travi longitudinali può falsare la ripartizione.

### Esempio di controllo manuale dello sbalzo

Per una striscia idealizzata larga 1 m, sbalzo a = 1,20 m, carico lineare q = 8 kN/m e forza al bordo P = 30 kN, già riferiti alla stessa combinazione, l’equilibrio al vincolo fornisce:

```math
V=qa+P
M=\frac{qa^2}{2}+Pa
```

Si ottengono V = 39,60 kN e |M| = 41,76 kNm. Con h = 250 mm, copriferro al ferro più esterno 45 mm e barra principale Ø16, se questa è effettivamente la barra esterna, d = 250 − 45 − 8 = 197 mm. Assumendo preliminarmente z = 0,9d e fyd = 391,30 MPa, As = M/(z fyd) risulta circa 602 mm²/m. La formula è una stima di armatura a flessione semplice; vanno poi verificati equilibrio della sezione, minimi, fessure, taglio, dettagli e disposizione reale dei diversi strati.

Questi carichi sono inventati per il controllo manuale e non costituiscono un modello di traffico normativo. In particolare P non rappresenta una forza di urto della barriera. La larghezza efficace di diffusione di una ruota deve provenire dal modello adottato.

### Durabilità e dettagli che cambiano il calcolo

Il copriferro maggiore può migliorare la protezione delle armature ma riduce d a spessore costante. Lo spessore del pacchetto impermeabilizzazione–pavimentazione modifica peso e quote. Ristagni presso giunti, scarichi e cordoli aumentano l’importanza del dettaglio di drenaggio. Il disegno deve mostrare continuità dell’armatura superiore, interferenze e possibilità concreta di getto.

ANTHEA consente di verificare la sezione in c.a. soggetta alle azioni ricavate dal modello. Bridge Design fornisce un’impostazione concettuale dell’impalcato: non sostituisce una verifica locale completa della piastra sotto ruote o barriera.

Lettura tecnica: [FHWA, esempio di progetto della soletta](https://www.fhwa.dot.gov/bridge/lrfd/us_ds2.cfm). Le procedure FHWA fanno riferimento al quadro statunitense: coefficienti e carichi non si trasferiscono automaticamente alle NTC.
''')

add('ponti-acciaio-fasi','Travi metalliche da ponte fra montaggio fatica e sezione composta','ponti','Ponti',['str_mista_ponte','str_bridge_design'],['sezione-composta-da-ponte','euler'],r'''
Una trave metallica da ponte attraversa più sistemi resistenti durante la costruzione. Prima della maturazione della soletta porta carichi con la sola sezione in acciaio; dopo l’attivazione della connessione può rispondere come sezione composta. La verifica finale deve conservare memoria della fase in cui ciascun carico è stato applicato.

### Sommare le tensioni delle fasi

In un esempio elastico semplificato, la tensione nella stessa fibra d’acciaio si ricava sommando i contributi. W1 e W2 sono i moduli resistenti riferiti proprio a quella fibra e allo stesso materiale, con omogeneizzazione coerente.

```math
\sigma_s=\frac{M_1}{W_1}+\frac{M_2}{W_2}
```

Per L = 30 m, q1 = 25 kN/m applicato prima della collaborazione e q2 = 15 kN/m dopo la collaborazione, si hanno M1 = 2812,50 kNm e M2 = 1687,50 kNm. Con W1 = 0,020 m³ e W2 = 0,040 m³ risulta σs = 182,81 MPa. Applicare erroneamente tutto il carico alla sezione composta darebbe 112,50 MPa: una sottostima di circa il 38%. Si tratta di un esempio lineare senza puntellazione, ritiro o viscosità, non di una sezione verificata.

### Stabilità durante il getto

Il vincolo laterale della piattabanda compressa dipende da controventi e collegamenti effettivamente presenti in ciascuna fase. La soletta fresca non garantisce da sola il ritegno della trave. La sequenza di getto può modificare torsione, imperfezioni e lunghezze non controventate; il montaggio richiede quindi verifiche proprie. Anche gli irrigidimenti d’anima e la diffusione delle reazioni agli appoggi hanno un ruolo locale che il solo controllo della tensione normale non risolve.

### La fatica è sensibile al dettaglio

Una connessione saldata e una lamiera continua con la stessa tensione nominale non hanno necessariamente la stessa resistenza a fatica. Contano categoria del dettaglio, intervallo di tensione, numero di cicli e modo di valutare le tensioni. Su un ramo idealizzato della curva S–N con esponente m, N è proporzionale a Δσ elevato a −m. Con m = 3, passare da 60 a 80 MPa riduce il numero di cicli a (60/80)³ = 0,422 del precedente. Il valore m = 3 è qui un’ipotesi illustrativa, non una scelta valida per ogni dettaglio e regime.

Il controllo richiede lo spettro delle escursioni dovute al traffico e le regole della norma adottata. Non basta sostituire il carico massimo SLU nella formula della fatica. In ANTHEA la costruzione delle fasi della sezione composta serve a separare i contributi; non equivale a una verifica completa di fatica del ponte o di stabilità durante il montaggio.

Fonti: [FHWA, progetto della trave metallica](https://www.fhwa.dot.gov/bridge/lrfd/us_ds3.cfm); [SCI, Fatigue design of bridges](https://steelconstruction.info/sectors/bridges/fatigue-design-of-bridges). Esempio numerico originale.
''')

add('ponti-precompressione','Precompressione delle travi da ponte e perdite nel tempo','ponti','Ponti',[],['bridge','calcestruzzo-armature-e-copriferro'],r'''
La precompressione introduce una forza e, quando il cavo è eccentrico, un momento che modifica lo stato tensionale prima dell’arrivo di una parte dei carichi esterni. La forza iniziale non coincide con quella al trasferimento né con quella efficace a lungo termine. Il progetto deve seguire queste fasi e le corrispondenti proprietà del calcestruzzo.

### Equilibrio della sezione non fessurata

Si considera una sezione simmetrica, compressione positiva, cavo sotto il baricentro con eccentricità e positiva e momento esterno M positivo che comprime la fibra superiore. Nell’ipotesi elastica non fessurata:

```math
\sigma_{sup}=\frac{P}{A}-\frac{Pe}{W}+\frac{M}{W}
\sigma_{inf}=\frac{P}{A}+\frac{Pe}{W}-\frac{M}{W}
```

Una sezione rettangolare b = 0,60 m e h = 1,20 m ha A = 0,72 m² e W = 0,144 m³. Con P = 3000 kN, e = 0,35 m e M = 1000 kNm, si ottengono σsup = 3,82 MPa e σinf = 4,51 MPa, entrambe di compressione. Se P scende a 2400 kN a parità delle altre condizioni, le tensioni diventano circa 4,44 e 2,22 MPa. Le perdite riducono la compressione media ma non necessariamente quella di ogni singola fibra, perché diminuisce anche il momento Pe.

### Perdite immediate e differite

L’accorciamento elastico al trasferimento interagisce con il calcestruzzo. Ritiro, viscosità e rilassamento modificano la forza nel tempo e possono richiedere un’analisi accoppiata. Nei sistemi post-tesi intervengono inoltre attrito lungo il tracciato e rientro degli ancoraggi; il numero e la sequenza delle tesature influenzano i contributi. Applicare una percentuale globale senza documentarne il campo non consente di ricostruire la distribuzione lungo la trave.

### Controlli oltre la tensione longitudinale

Servono verifiche al trasferimento, durante sollevamento e montaggio, in esercizio e allo SLU. Le zone di ancoraggio possono presentare trazioni trasversali; taglio e torsione interagiscono con lo stato di compressione. Freccia e controfreccia dipendono dal tempo, dalla sequenza e dai vincoli. Una controfreccia geometrica non elimina le tensioni che l’hanno prodotta.

ANTHEA non dispone di un modulo generale di progetto dei cavi, delle perdite o delle zone di ancoraggio dei ponti precompressi. Questa pagina amplia la teoria del ponte e ne distingue il perimetro dalle funzioni della sezione composta e dal predimensionamento concettuale.

Fonte di approfondimento: [FHWA, sequenza di valutazione delle perdite di precompressione](https://www.fhwa.dot.gov/bridge/lrfd/pscus03ft.cfm). I coefficienti delle procedure statunitensi richiedono un confronto separato con le norme del progetto.
''')

add('ponti-cassoni','Cassoni sottili torsione uniforme e distorsione','ponti','Ponti',[],['bridge','beam'],r'''
Una sezione chiusa è efficiente nel trasmettere torsione perché può sviluppare un flusso tangenziale continuo lungo il contorno. Questa proprietà non elimina distorsione, instabilità locale delle lamiere o effetti di estremità. Occorre distinguere la torsione uniforme di Saint-Venant dalle deformazioni che cambiano la forma della sezione.

### Modello di una cella chiusa sottile

Per una cella unica, contorno chiuso, materiale elastico uniforme e torsione sufficientemente lontana dalle discontinuità, le relazioni di Bredt-Batho collegano momento torcente T, area racchiusa dalla linea media Am, spessore t e flusso q. L’integrale segue il perimetro della linea media.

```math
q=\frac{T}{2A_m}
\tau=\frac{q}{t}
J=\frac{4A_m^2}{\oint ds/t}
\frac{d\theta}{dx}=\frac{T}{GJ}
```

J è la costante torsionale, non il momento polare di inerzia utilizzabile indistintamente per ogni forma. Per celle multiple occorre imporre anche la compatibilità delle rotazioni e trattare i flussi sulle pareti condivise.

### Esempio con unità coerenti

Per un rettangolo sulla linea media di 4 × 2 m, spessore uniforme t = 20 mm, Am = 8 m² e perimetro 12 m. Con T = 1000 kNm e G = 80 GPa si ottengono q = 62,50 kN/m, τ = 3,125 MPa e J = 0,4267 m⁴. La rotazione unitaria è circa 2,93 × 10⁻⁵ rad/m; su 30 m con T costante vale circa 0,0504 gradi.

Non è la rotazione di un ponte reale: l’esempio trascura variazione di T, irrigidimenti, diaframmi, vincoli di estremità, variazioni di spessore e distorsione. Il valore contenuto della tensione tangenziale non dimostra che la lamiera sia stabile.

### Che cosa fanno diaframmi e irrigidimenti

Carichi eccentrici possono tendere a deformare il rettangolo trasversale in una forma romboidale. I diaframmi contrastano questa distorsione e distribuiscono le reazioni. Gli irrigidimenti longitudinali e trasversali governano campi locali di lamiera e lunghezze efficaci. Un elemento beam con sola rigidezza GJ non rappresenta tutti questi meccanismi; un modello a guscio richiede mesh, collegamenti e vincoli capaci di riprodurli.

Una curiosità utile: aprire una fessura longitudinale ideale nel contorno può ridurre drasticamente la rigidezza torsionale, anche se area di acciaio e ingombro cambiano poco. La chiusura del flusso è una proprietà topologica del percorso resistente, non soltanto una questione di quantità di materiale.

In ANTHEA questa trattazione è documentazione tecnica. I moduli attuali non forniscono una verifica completa di cassoni multicellulari, distorsione o instabilità delle piastre. Letture: [Steel Bridge Design Handbook, AISC e NSBA](https://www.aisc.org/bridges/bridge-resource-center/steel-bridge-design-handbook/); [materiali FHWA sui ponti metallici](https://www.fhwa.dot.gov/bridge/steel.cfm).
''')

add('ponti-stralli','Stralli equilibrio rigidezza geometrica e vibrazioni','ponti','Ponti',[],['bridge','dinamica-e-sisma-del-modello'],r'''
Uno strallo trasmette trazione lungo il proprio asse e introduce simultaneamente forze verticali e orizzontali. La geometria decide quanto sforzo serve per sostenere una data componente verticale e quanta compressione viene trasferita all’impalcato. La configurazione di equilibrio e lo stato di tesatura fanno parte del modello.

### Inclinazione e azioni sull’impalcato

Per un cavo rettilineo ideale inclinato di α rispetto all’orizzontale:

```math
T=\frac{V}{\sin\alpha}
H=V\cot\alpha
```

Con V = 1000 kN, a 30 gradi servono T = 2000 kN e H = 1732 kN. A 60 gradi T scende a circa 1155 kN e H a 577 kN. Per confrontare due configurazioni progettuali occorre però tenere conto anche di lunghezza, altezza dell’antenna, posizione degli ancoraggi e distribuzione delle forze negli altri stralli.

Se gli altri spostamenti dell’ancoraggio sono impediti e si considera soltanto l’allungamento assiale lineare di una barra tesa, la rigidezza verticale materiale è (EA/L) sin²α. Un cavo reale aggiunge effetti di freccia, pretensione e rigidezza geometrica; può perdere tensione e non lavora come un puntone compresso. Usare soltanto la proiezione di EA/L è quindi una semplificazione circoscritta.

### Un ordine di grandezza dinamico

Per la corda tesa ideale con massa lineare μ e trazione uniforme T, la prima frequenza è:

```math
f_1=\frac{1}{2L}\sqrt{\frac{T}{\mu}}
```

Con L = 100 m, T = 2 MN e μ = 50 kg/m risulta f1 = 1 Hz. Una variazione della tensione cambia la frequenza: a parità del resto, raddoppiare T la moltiplica per √2. Curvatura del cavo, rigidezza flessionale, condizioni degli ancoraggi e dispositivi esterni possono modificare questa stima.

### Vibrazioni e manutenzione

Pioggia e vento possono attivare meccanismi aeroelastici che la sola frequenza propria non descrive. Trattamenti della superficie, smorzatori e collegamenti fra cavi affrontano fenomeni differenti; un collegamento può anche creare nuovi modi locali. Ispezionabilità degli ancoraggi, protezione dalla corrosione e possibilità di sostituzione devono essere considerate insieme alla risposta resistente.

ANTHEA non calcola attualmente l’equilibrio non lineare o la dinamica di un sistema di stralli. Fonte primaria di approfondimento: [FHWA, Wind-Induced Vibration of Stay Cables, capitolo 4](https://www.fhwa.dot.gov/publications/research/infrastructure/bridge/05083/chap4.cfm), rapporto di ricerca storico da leggere nel proprio contesto, non prescrizione normativa corrente.
''')

add('ponti-deformazioni','Freccia controfreccia temperatura e appoggi dei ponti','ponti','Ponti',['str_mista_ponte','str_bridge_design'],['bridge','ponti-acciaio-fasi'],r'''
La deformabilità influenza comfort, quote della pavimentazione, pendenze di drenaggio, giunti e funzionamento degli appoggi. Il superamento di una resistenza e l’incompatibilità di uno spostamento sono problemi distinti. Occorre inoltre separare la deformazione istantanea da viscosità, ritiro, assestamenti e sequenza costruttiva.

### Sensibilità alla luce

Per una trave prismatica semplicemente appoggiata, piccole deformazioni, EI costante e carico uniforme q, la freccia in mezzeria è:

```math
w_{max}=\frac{5qL^4}{384EI}
```

Con q = 30 kN/m, L = 30 m, E = 210 GPa e I = 0,30 m⁴ si ottengono circa 5,02 mm. Aumentando la luce del 10%, senza cambiare le altre grandezze, la freccia diventa circa 7,35 mm: cresce del 46,4% perché dipende da L alla quarta potenza. La formula non descrive automaticamente una trave composta a rigidezza variabile o una sezione fessurata.

### Movimento termico e schema dei vincoli

Una variazione uniforme della temperatura produce, in assenza di impedimenti:

```math
\Delta L=\alpha L\Delta T
```

Con α = 12 × 10⁻⁶ /K, L = 120 m e ΔT = 35 K, il movimento relativo fra estremità è 50,4 mm. Se il punto fisso è al centro e il comportamento è simmetrico, ciascuna estremità si muove di circa 25,2 mm rispetto a esso. Questo valore non comprende tolleranze, rotazioni, ritiro, viscosità e altre azioni da includere nel dimensionamento effettivo del giunto.

Nel caso ideale di impedimento assiale completo e risposta elastica, la tensione termica ha modulo EαΔT, pari a 88,2 MPa con E = 210 GPa. Un gradiente termico nello spessore introduce invece curvatura e possibili azioni da vincolo. Confondere temperatura uniforme e gradiente porta a errori di schema.

### Appoggi e costruzione

Un appoggio deve trasmettere le azioni previste e permettere i movimenti assegnati. Attrito, rotazione, spostamento, stabilità e sostituibilità vanno coordinati; il punto fisso influenza la distribuzione di frenatura e temperatura. Le quote di posa e la temperatura al montaggio determinano la posizione iniziale nel campo di escursione.

Le NTC 2018 §5.1.4.7 richiedono: «Le verifiche di sicurezza vanno svolte anche per le singole fasi di costruzione dell’opera». La fase di getto e quella di rimozione dei sostegni temporanei possono governare anche quando la configurazione definitiva è soddisfacente. La controfreccia compensa una quota attesa, non annulla lo stato tensionale.

In ANTHEA le fasi della sezione composta aiutano a organizzare azioni e rigidezze. L’esempio di Bridge Design va letto con i propri limiti: non rappresenta un progetto completo di appoggi o giunti. Fonti: [NTC 2018, §§5.1.4.5 e 5.1.4.7](https://www.gazzettaufficiale.it/eli/gu/2018/02/20/42/so/8/sg/pdf); [FHWA, progetto degli appoggi](https://www.fhwa.dot.gov/bridge/lrfd/us_ds6.cfm).
''')
