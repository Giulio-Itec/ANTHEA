# ANTHEA — Audit generale di Bridge Design

**Data:** 27 settembre 2026. **Ambito:** modulo `str_bridge_design`, motore, computo, geometrie, finestra WPF ed esplorazione delle alternative. Revisione di partenza del repository: `d889d9f`; riferimento precedente all'estensione: `48c431c`.

## 1. Esito e significato dei controlli

Il controllo ha individuato e corretto difetti reali nel predimensionamento automatico, nelle quantità e nell'interfaccia. La suite finale supera **44.259 asserzioni**. Questo numero comprende verifiche ripetute su casi diversi, non 44.259 progetti indipendenti o verifiche normative.

La campagna geometrica comprende **1.008 configurazioni**: **874 calcolate e controllate**, **134 rifiutate esplicitamente** perché il dimensionamento automatico non è compatibile con i parametri adottati. A questi casi si aggiungono le prove analitiche, 224 combinazioni famiglia/terreno/pila, le prove delle famiglie speciali e le prove di ottimizzazione.

Sono state eseguite **27 ricerche di campagna**, per **10.491 valutazioni complessive**, più una ricerca limite con prezzi e CO₂ nulli, oltre ai test di ricerca già presenti e alle prove attraverso la finestra. Sono superati anche build e smoke test WPF, inclusi 100 ponti casuali con semi riproducibili, caricamento del listino, salvataggio, riapertura, export e applicazione delle alternative.

**Giudizio tecnico:** le quantità sono ora più coerenti con le geometrie dichiarate e gli automatismi rispettano le proprie soglie nei casi verificati. Il modulo rimane adatto a confronti preliminari. Non dimostra che impalcato, spalle, pile o pali siano verificati strutturalmente: alcune dimensioni sono regole geometriche, altre derivano da soli equilibri ideali o carichi verticali centrati. Le limitazioni sono descritte per componente nei paragrafi successivi.

## 2. Procedura seguita

1. Lettura del motore, dei parametri, della ricerca e dei prospetti tecnici; controllo della separazione fra calcolo e interfaccia.
2. Conservazione del precedente assembly di calcolo in `baseline/` e delle evidenze originali dei 1.000 casi del sito. Nessuna nuova acquisizione del sito è stata presentata come parte di questo audit.
3. Acquisizione dei PDF ufficiali dei prezzari, estrazione delle voci pertinenti, verifica visiva delle tabelle e conservazione degli hash SHA-256.
4. Ricostruzione di carichi, reazioni, aree, volumi, peso delle sottostrutture, griglie dei pali e costo totale mediante controlli separati dal codice di produzione.
5. Correzione dei difetti individuati e aggiunta di test che esercitano i comportamenti corretti.
6. Esecuzione della campagna parametrica e riesecuzione dei 2.000 input della precedente campagna di confronto col sito.
7. Prove della finestra, dei grafici e delle esportazioni; ispezione delle immagini di prospetto, sezione e graduatoria.
8. Prove finali della ricerca: vincoli, enumerazione diretta di piccole griglie, graduatorie, frontiera Pareto, riproducibilità, cancellazione e applicazione della soluzione.

Gli output finali sono in `supporto/artefatti/bridge_design_general_audit/`. Le cartelle `calcoli_finali/` e `ui_finalissima/` contengono gli esiti validi conclusivi. Le cartelle precedenti conservano anche i tentativi intermedi, compresi gli errori successivamente corretti. L’ultima build usa `app_verificata/`: un tentativo nella cartella ordinaria aveva incontrato DLL occupate da altre sessioni. La build isolata è riuscita con zero errori e zero avvisi.

## 3. Prezzi: fonti, unità e decisioni

Fonti consultate: [ANAS, elenco prezzi ufficiale](https://www.stradeanas.it/it/elenco-prezzi), [PDF NC-MP 2026 Rev.1, giugno 2026](https://www.stradeanas.it/sites/default/files/fornitori/doc/elenco%20prezzi/NC-MP_LISTINO-PREZZI-2026-Rev1.pdf), [Emilia-Romagna, prezzario 2026 corretto](https://territorio.regione.emilia-romagna.it/osservatorio/elenco_regionale_prezzi). Il secondo serve da riscontro territoriale; non implica che il ponte sia localizzato in quella regione.

Tabella di riscontro numerico ANAS; le pagine sono quelle del file PDF, contando la copertina:

| Riferimento | Unità | Prezzo | Pagina PDF |
|---|---:|---:|---:|
| B.03.040.b, C45/55 | €/m³ | 260,21 | 38 |
| B.03.031.d, C35/45 fondazioni | €/m³ | 226,38 | 37 |
| B.03.035.d, C35/45 elevazione | €/m³ | 241,07 | 38 |
| B.05.030, B450C | €/kg | 1,66 | 47 |
| B.05.057, trefoli | €/kg | 2,02 | 48 |
| B.05.000.01.8.b, carpenteria | €/kg | 3,50 | 43 |
| B.05.000.01.9.b, reticolari/archi | €/kg | 4,24 | 44 |
| B.05.000.06.1, extra ortotropo | €/kg | 0,46 | 44 |
| B.04.001, casseri piani | €/m² | 40,34 | 40 |
| B.02.040.b, palo 1.000 mm | €/m | 280,67 | 28 |
| B.02.040.d, palo 1.500 mm | €/m | 542,04 | 28 |
| B.07.006.a/b, appoggi multidirezionali | €/kN | 3,66 / 2,93 | 56 |
| B.07.050.b.1, giunto | €/m | 2.245,44 | 64 |
| G.02.005.3.a, barriera H4 ponte | €/m | 362,73 | 203 |
| B.05.080.1.a/b, funi pendini | €/kg | 14,40 / 15,53 | 48–49 |

ANAS comprende SG 15% e utile 10%; sicurezza specifica esclusa. Pali: armatura separata. Calcestruzzo: casseri e armatura separati. Trefoli: ancoraggi separati. Carpenteria: varo ordinario incluso. Sovrapprezzo ortotropo: sola massa della lastra. [Fonte ANAS](https://www.stradeanas.it/sites/default/files/fornitori/doc/elenco%20prezzi/NC-MP_LISTINO-PREZZI-2026-Rev1.pdf).

Riscontro RER: A02.046.050, gabbia B450C dei pali, **1,59 €/kg**, pagina PDF 132; A03.007.015.d, fondazione C35/45 XC1-XC2, **257,28 €/m³**, pagina 145. Sono lavorazioni poste in opera; il calcestruzzo esclude ponteggi, casseri e armatura. La differenza dal riferimento nazionale non è di per sé un errore: cambiano analisi e ambiti della voce. [Fonte regionale](https://territorio.regione.emilia-romagna.it/osservatorio/elenco_regionale_prezzi).

### Valori ANTHEA aggiornati

| Parametro | Prima | Nuovo iniziale | Valutazione dell'audit |
|---|---:|---:|---|
| Cls impalcato, €/m³ | 240 | **260** | Allineamento al materiale iniziale C45/55; altre classi richiedono adeguamento manuale. |
| Cls sottostrutture/plinti, €/m³ | 200 | **240** | Prezzo medio aggregato; distinguere elevazione/fondazione in un computo di progetto. |
| Armatura ordinaria, €/t | 1.100 | **1.660** | Il vecchio valore era basso per fornitura e posa; conversione kg/t verificata. |
| Precompressione, €/t | 3.600 | 3.600 | Riserva di sistema; non assimilabile al solo materiale. Testate e configurazione non sono computate analiticamente. |
| Carpenteria ordinaria, €/t | 3.500 | 3.500 | Riferimento plausibile; controllare protezione e modalità costruttiva. |
| Cassoni, €/t | 4.000 | 4.000 | Maggiorazione convenzionale, non voce ANAS esatta. |
| Casseforme, €/m² | 50 | 50 | Plausibile ordine di grandezza; sostegni alti e centine non risolti. |
| Pali Ø1 / Ø1,5, €/m | 300 / 550 | 300 / 550 | Vicini ai riferimenti; non includono automaticamente tutte le condizioni di perforazione. |
| Appoggi, €/cad | 1.600 | **5.000** | Indennità media, non dimensionamento né conversione universale dal prezzario. |
| Giunti, €/m | 2.400 | 2.400 | Utilizzabile solo come stima per movimenti moderati; grandi ponti richiedono prezzi specifici. |
| Barriere, €/m | 240 | **360** | Il precedente valore era debole per bordo ponte ad alta capacità. |
| Pavimentazione, €/m² | 32 | 32 | **Non validata come pacchetto completo**: mancano stratigrafia e impermeabilizzazione. |
| Ortotropo, €/t | 5.000 | 5.000 | Prezzo aggregato prudenziale, non somma analitica delle sole masse interessate dal sovrapprezzo. |
| Cavi/pendini installati, €/t | 12.000 | **16.000** | Calibrazione preliminare; grandi stralli/cavi e terminali richiedono offerta specialistica. |
| Complessità speciale, €/t | 1.000 | 1.000 | Riserva aggiuntiva. Azzerare quando già compresa nel prezzo di carpenteria/offerta. |

La tabella dei nuovi valori è una scelta di calibrazione del software, non un nuovo prezzario ufficiale. Non è corretto dichiarare tutti i costi unitari “validati ANAS”. Le note in **Prezzi unitari** rendono visibile questa distinzione. Il pulsante **Applica valori orientativi 2026** carica i nuovi valori; **Annulla** ripristina quelli personali. Gli archivi esistenti mantengono il listino salvato.

La maggiorazione iniziale del 12% è ora denominata **Oneri aggiuntivi non computati**. Non deve essere intesa come nuova applicazione di spese generali e utile ai prezzi ufficiali. Il 15% di imprevisti è una riserva distinta. Entrambe sono modificabili e non sostituiscono il computo delle lavorazioni mancanti.

### Omissioni del computo corrette e residue

Sono state aggiunte le superfici equivalenti di casseratura di pile/spalle, le facce laterali dei plinti e la casseratura della soletta negli impalcati misti. Per i plinti si usa `2(B + L)t`; per i fusti, perimetro per altezza, con fondo e fianchi del pulvino. Le spalle usano una superficie equivalente del paramento. Non si aggiunge il costo del calcestruzzo dei pali una seconda volta: è già nel prezzo al metro; il suo volume entra comunque nelle quantità fisiche e nella CO₂.

Restano non computati analiticamente scavi, rinterri, drenaggi, accessi, protezioni, impermeabilizzazione, centine alte, sicurezza specifica, logistica eccezionale e dettagli degli ancoraggi. L'applicazione ora lo segnala. L'intervallo iniziale ±30% è un'ipotesi modificabile, non un intervallo statistico dimostrato, e può essere insufficiente per opere speciali.

## 4. Impalcati: ricostruzione e limiti

La larghezza è la somma di corsie, due banchine, spartitraffico e due fasce laterali. Le lunghezze sono ricostruite fra assi degli appoggi; la somma deve coincidere con la lunghezza impostata. Nei ponti ordinari continui con più di due campate, quelle terminali hanno peso geometrico 0,8 rispetto alle interne. Gli spostamenti dovuti all'ostacolo non possono produrre campate inferiori a 2 m.

| Famiglia | Regola di altezza automatica | Minimo, m | Campo orientativo luce, m |
|---|---|---:|---:|
| Soletta c.a. | Lmax/18 | 0,35 | 6–15 |
| T c.a. | Lmax/17 | 0,70 | 12–30 |
| I c.a.p. | Lmax/22,22 | 1,00 | 20–50 |
| U c.a.p. | Lmax/22,22 | 1,10 | 25–50 |
| Cassone c.a.p. | Lmax/22,22 | 1,30 | 35–80 |
| Conci variabile | Lmax/45; sulle pile almeno Lmax/18 | 2,00 | 80–200 |
| I acciaio-cls | Lmax/25 | 1,00 | 30–90 |
| Cassone acciaio-cls | Lmax/25 | 1,20 | 40–150 |
| Travi incorporate | Lmax/28 | 0,45 | 8–40 |
| Cassone ortotropo | Lmax/30 | 1,20 | 40–200 |
| Arco con catena | Lmax/120 per il solo impalcato | 0,80 | 40–250 |
| Strallato | Lcentrale/150 per il solo impalcato | 1,00 | 100–700 |
| Sospeso | Lcentrale/200 per il solo impalcato | 1,20 | 200–1.200 |
| Reticolare | Lmax/100 per il solo impalcato | 0,70 | 30–150 |

Per i primi dieci schemi si applica 0,95 al rapporto nelle configurazioni continue e 1,10 nelle indipendenti, prima del minimo. Le quattro strutture superiori non usano questi fattori. Questi numeri sono **regole del modello**, non limiti normativi dimostrati dall'audit. Il limite di lunghezza totale della scheda rimane 2.000 m: non tutte le combinazioni del catalogo sono quindi raggiungibili.

Le quantità derivano dalla somma delle aree dei componenti moltiplicata per la lunghezza. L'acciaio longitudinale usa densità 7,85 t/m³ e un'aggiunta di massa per accessori del 15%, ridotta al 5% nelle travi incorporate. La maggiorazione di massa non incrementa artificialmente la rigidezza. Nelle travi incorporate il calcestruzzo è netto dell'acciaio; nell'ortotropo non compare una soletta di cls inesistente.

**Anime inclinate:** il cassone metallico usa lo sviluppo reale dell'anima per area, massa e contributo all'inerzia. Nella U in c.a.p. era rimasta una discordanza: volume con sviluppo inclinato, rigidezza con altezza verticale. È stata corretta introducendo la stessa area inclinata anche nella sezione resistente equivalente, con contributo lungo l'altezza reale. Si tratta di una schematizzazione a pareti sottili, non della discretizzazione di ogni raccordo del prefabbricato.

Per i ponti ordinari il calcolo longitudinale è Euler-Bernoulli, EI lordo costante, carico uniforme contemporaneo su tutte le campate. Sono stati confrontati reazioni, momento e freccia della campata semplice con `qL/2`, `qL²/8` e `5qL⁴/(384EI)`; per due campate uguali sono controllati reazioni e momenti da soluzione chiusa. Altre 200 travi diseguali verificano equilibrio verticale, momento globale e spostamenti nulli agli appoggi.

Non sono verificati armatura necessaria, pressoflessione, taglio, instabilità locale delle lamiere, fatica, fessurazione, viscosità, precompressione nelle deformazioni, fasi di getto/varo o carichi mobili. Nel cassone a conci variabile la rigidezza equivalente non rappresenta le fasi a sbalzo. **Una freccia piccola o un'altezza conforme al rapporto L/d non certificano l'impalcato.**

## 5. Spalle, pile e fondazioni

### Spalle

L'altezza equivalente è `min(7 m, quota ponte − altezza impalcato)`. Lo spessore equivalente del paramento è `max(0,60 m, H/7)`. Il volume è `W × [H × spessore + 3 m²]`; il termine aggiuntivo rappresenta una riserva geometrica aggregata, non ali e muri definiti in pianta.

Il test ricostruisce esattamente questo volume, ma **la spalla non è dimensionata come opera di sostegno**. Mancano altezza effettiva del rilevato, spinta del terreno, sovraccarichi, acqua, azioni degli appoggi e geometria delle ali. Per quote elevate il limite di 7 m presume una sistemazione del terreno da definire: non dimostra che una spalla alta 7 m sia sufficiente. È uno dei punti da affinare per primi sul progetto reale.

### Pile ordinarie

Il fusto può essere circolare, a setto, a colonne o con testa a martello. Si ricostruiscono area per altezza e volume del pulvino. L'altezza libera tiene conto dell'impalcato, compresa la maggiore altezza del cassone variabile sulla pila.

Prima dell'audit la dimensione automatica dipendeva prevalentemente dall'altezza, e poteva superare le soglie del proprio filtro. Ora soddisfa contestualmente una snellezza convenzionale non superiore a 90 e una compressione media non superiore a `0,30 fc_sub`, includendo il peso del fusto e del pulvino. La misura viene arrotondata a 5 cm. Con `r=Ø/4` o `r=t/√12` e lunghezza efficace convenzionale `2H`, si controlla `λ=2H/r`. Per il carico assiale si ricava l'area da `(R + 25 Vpulvino)/(0,30 fc_sub × 1000 − 25H)`.

Il filtro verifica anche che il fusto sia contenuto nel lato del plinto. Il calcolo tratta fusti pieni e carico centrato. Una pila automatica più grande corregge la coerenza con queste soglie; non equivale a una verifica di secondo ordine, pressoflessione, sisma, vento o urto. Le dimensioni manuali restano quelle richieste, con segnalazione/esclusione quando incompatibili.

### Antenne

Il modello impiega due fusti quadrati pieni e un traverso. L'altezza comprende tratto sotto e sopra impalcato. La sezione automatica considera reazione amplificata e peso proprio amplificato; se il peso proprio per unità di area esaurisce la tensione di riferimento, il calcolo restituisce un'incompatibilità esplicita.

Per le antenne manuali si verifica anche il rapporto con la tensione di riferimento e si escludono quelle oltre soglia dalla ricerca. Questo ha prodotto 18 rifiuti nella campagna, principalmente verso gli estremi delle grandi luci. Non sono crash: un'antenna molto alta non viene resa artificiosamente ammissibile aumentando senza limite una sezione piena. Sezioni cave, rastremazioni e modelli più raffinati richiedono un'estensione specifica.

### Plinti diretti

La pressione di riferimento viene impostata dall'utente o dalla classe convenzionale di terreno: 1.000, 400, 200 o 100 kPa. Sono ipotesi, non risultati di indagini. Il lato iniziale dipende dal carico, poi il ciclo ricalcola peso proprio e pressione. Lo spessore resta `max(0,60 m, lato/6)`; la larghezza deve contenere il sostegno trasversale.

Il controllo è `η=(R + peso sottostruttura + peso plinto + eventuale blocco)/(B L p_rif)`. Il lato automatico cresce a passi di 0,25 m finché `η≤1`. Un aumento del lato aumenta anche il peso: con carichi molto grandi e terreno debole può non esistere una soluzione nello schema assunto. Il ciclo è limitato e segnala la mancata convergenza.

Non vengono controllati eccentricità, pressioni parzializzate, punzonamento, flessione/armatura, scorrimento, ribaltamento, cedimenti o interazione con fondazioni vicine. Lo spessore L/6 resta indicativo.

### Pali

Diametri disponibili: 1,0 e 1,5 m. Lunghezze automatiche per classe: 10, 15, 22, 30 m, modificabili. Il riferimento assiale è `π D L qs + π D² qb/4`; attrito e punta possono essere sostituiti dall'utente. La formula è stata verificata dimensionalmente e nel computo, ma non integra stratigrafia, falda, attrito negativo o coefficienti di una specifica procedura normativa.

Ora il numero automatico comprende il carico aggiuntivo del plinto, cresce per coppie da un minimo di quattro, e ricalcola contemporaneamente la griglia. Si assume una disposizione quadrata con passo 3D e lato minimo `(ceil(√n)−1)3D+2D`. La lunghezza computata è `Σn × Lpalo`; il volume è tale lunghezza per `πD²/4`. La capienza geometrica del plinto è verificata anche quando l'utente impone il numero di pali.

Esempio tratto dal corpus, caso **0004/native**: 20 pali e `η=1,108` prima; 24 pali e `η=0,963` dopo. È la correzione del peso trascurato nella scelta iniziale, non una calibrazione per imitare il sito.

Nella campagna geometrica 116 configurazioni sono state rifiutate per mancata convergenza della fondazione automatica; i dettagli sono registrati. Effetti di gruppo, cedimenti, carichi trasversali e flessione dei pali restano fuori dal modello. Le verifiche di un progetto appartengono a un livello diverso da queste soglie: riferimento generale [NTC, DM 17 gennaio 2018](https://www.gazzettaufficiale.it/eli/id/2018/2/20/18A00716/sg).

## 6. Famiglie speciali

**Arco:** arco metallico con catena, due piani e campate indipendenti. La spinta totale ideale è `H=qL²/(8f)` e viene equilibrata dalle catene. Le aree delle aste derivano dalla forza divisa per tensioni convenzionali modificabili. Non è un arco che scarica liberamente la spinta nel terreno.

**Reticolare:** due piani, correnti, diagonali e montanti ideali. Il predimensionamento dei correnti usa il momento diviso per l'altezza; le diagonali usano un riferimento cautelativo al taglio massimo. Il modello non sceglie profili di catalogo e non controlla instabilità o nodi.

**Strallato:** tre campate simmetriche L/4 + L/2 + L/4, due antenne e due piani di stralli. Per ogni strallo si controlla la componente verticale della trazione rispetto al carico tributario. La somma di tutte le componenti restituisce il carico dell'impalcato.

**Sospeso:** stesso schema di campate. Cavo centrale e cavi di riva sono parabolici sotto carico uniforme; sono presenti pendini anche sulle rive. Il precedente cavo di riva rettilineo non rappresentava correttamente il sostegno di quei tratti ed è stato sostituito. Le reazioni globali soddisfano equilibrio verticale e dei momenti. Il blocco di ancoraggio è stimato per peso mediante `(sollevamento + H/attrito)/25`, per ciascun estremo. Non è verificato a ribaltamento o per pressioni eccentriche.

Il peso delle strutture superiori viene aggiornato iterativamente nel carico fino a tolleranza relativa 10⁻⁸, con limite di 80 iterazioni. Le masse sono distribuite uniformemente per il calcolo ideale. Non si rappresentano carichi mobili asimmetrici, deformazioni dei cavi, redistribuzioni elastiche, fasi costruttive o aerodinamica. Per queste famiglie **non vengono mostrati falsi diagrammi globali di una trave ordinaria**.

**Travi incorporate:** profili ideali completamente inglobati; area cls netta, omogeneizzazione con `(Es/Ec−1)` per non duplicare il volume spostato. **Ortotropo:** lamiera, canalette e cassoni sono computati dalle loro dimensioni, con sviluppo reale delle pareti inclinate. Traversi e connessioni entrano come incidenza di massa; fatica e risposta locale della piastra non sono calcolate. Il ruolo di lamiera, irrigidimenti e fatica è documentato anche nel [manuale FHWA sugli impalcati ortotropi](https://www.fhwa.dot.gov/bridge/pubs/if12027/if12027.pdf); questo audit non ne implementa le verifiche.

## 7. Regressione sui dati del sito

Sono stati riutilizzati i 1.000 casi già acquisiti, ognuno nei due modi `native` e `resolved`. La riesecuzione è una **regressione del motore su dati congelati**, non una nuova prova live né una dimostrazione di equivalenza col sito.

| Esito | Numero |
|---|---:|
| Valutazioni | 2.000 |
| Calcolate prima e dopo | 1.871 |
| Rifiutate prima e dopo | 128 |
| Prima rifiutata, ora calcolabile | 1 |
| NaN / infinito attuali | 0 |
| Larghezza, altezze impalcato, luci o massa acciaio cambiate nei casi comuni | 0 |
| Inerzie cambiate | 250, tutte U in c.a.p. |
| Volume cls cambiato | 1.190 |

Nei 1.871 casi comuni il costo cresce fra 1,13% e 51,48%, mediana 7,66%, usando **gli stessi prezzi salvati nel corpus**. Quindi questi scostamenti non dipendono dai nuovi valori iniziali: derivano dalle sottostrutture/fondazioni corrette e dalle casseforme aggiunte. Le differenze rispetto al sito già documentate restano; non è corretto dire che ANTHEA ne replichi tutti i risultati.

## 8. Test di ottimizzazione

La ricerca confronta solo le combinazioni della griglia scelta. Non è un algoritmo di progetto strutturale e non garantisce l'ottimo al di fuori della griglia. Non ottimizza automaticamente ogni parametro delle strutture speciali: freccia dell'arco, altezza antenna, geometria dei cavi e scelta cls/ortotropo restano parametri del caso. Per confrontarli si modificano gli input e si ripete la ricerca.

L'audit esegue 21 ricerche vincolate su sette famiglie — I c.a.p., arco, reticolare, strallato, sospeso, travi incorporate, ortotropo — e tre obiettivi. Per ciascuna viene enumerata separatamente la griglia di tre altezze e confrontato il minimo. Altre sei ricerche libere, su L=120 e 480 m e tre obiettivi, raggiungono complessivamente tutte le 14 famiglie.

| Ricerca libera, piattaforma del test | Valutazioni per obiettivo | Soluzioni distinte | Costo dell'ottimo costo | CO₂ dell'ottimo costo |
|---|---:|---:|---:|---:|
| L=120 m | 1.308 | 1.074 | 1.359.105 € | 800,13 t |
| L=480 m | 2.161 | 1.932 | 4.723.017 € | 2.590,07 t |

Questi sono risultati riproducibili del test, non stime trasferibili a un altro ponte. A L=120 m l'ottimo CO₂ costa 1.403.834 € e produce 751,53 t; mostra che gli obiettivi possono scegliere configurazioni differenti.

Sono controllati: immutabilità del progetto; permanenza di prezzi/carichi/terreno/larghezza; vincoli di famiglia, campate, sezione e fondazioni; determinismo; ordinamento dei punteggi non arrotondati; normalizzazione dell'obiettivo misto, compreso il caso limite con costo e CO₂ entrambi nulli; frontiera Pareto verificata per dominanza diretta; traccia completa dei tentativi; riproducibilità delle alternative; top N indipendente dalla ricerca; griglie invalide o vuote; rifiuto dei sollevamenti ordinari; contenimento dei pali; annullamento prima e durante la ricerca.

Attraverso WPF sono controllati selezione dal grafico, preview senza modifica del progetto, scelta oltre le prime N righe, applicazione della soluzione, ritorno alle quote tecniche, undo, invalidazione al cambio dei prezzi e interruzione. La preview viene aggiornata a frequenza limitata quando cambia il migliore provvisorio. La graduatoria resta una famiglia di **soluzioni ammesse dalle soglie del modello**, non di progetti già verificati.

## 9. Ripetibilità e file di prova

Sorgenti principali: `supporto/test/BridgeDesign.Checks/AuditChecks.cs`, `OptimizationChecks.cs`, `ExplorationChecks.cs`, `Program.cs`; smoke test in `supporto/test/Desktop/BridgeDesignSmokeChecks.cs`. Il riepilogo della regressione è prodotto da `supporto/test/BridgeDesign.Checks/audit_evidence.py`.

```powershell
dotnet run --project supporto/test/BridgeDesign.Checks -c Release --no-restore -- supporto/artefatti/bridge_design_general_audit/calcoli_finali
dotnet build X.Desktop/X.Desktop.csproj -c Release --no-restore -o supporto/artefatti/bridge_design_general_audit/app_verificata
dotnet supporto/artefatti/bridge_design_general_audit/app_verificata/ANTHEA.dll --smoke-bridge-design supporto/artefatti/bridge_design_general_audit/ui_finalissima
dotnet run --project supporto/test/BridgeDesign.SiteComparison -c Release --no-restore -- --run supporto/artefatti/bridge_design_site_1000/engine-inputs.jsonl supporto/artefatti/bridge_design_general_audit/regression-2000.jsonl
python supporto/test/BridgeDesign.Checks/audit_evidence.py
```

Evidenze: `calcoli_finali/checks.txt`, `audit-cases.json`, `audit-dimensions.txt`, `audit-optimization.json`, `audit-optimization.txt`; `ui_finalissima/smoke.txt` e immagini; `audit-summary.json` con hash dei prezzari e dei corpus. Gli hash identificano esattamente i documenti e i risultati usati, senza dipendere da eventuali aggiornamenti successivi delle pagine online.

## 10. Cosa affinare sul ponte reale

Prima di usare la soluzione come base progettuale servono almeno: geometria reale di impalcato e spalle, schema statico e fasi, inviluppi di traffico, azioni orizzontali, verifiche di sezioni/collegamenti, modello geotecnico e cedimenti, prestazioni di appoggi/giunti, dettagli costruttivi e computo territoriale completo. Per i ponti speciali occorrono analisi dedicate di stabilità, deformabilità, fatica e vento. Il modulo può aiutare a scegliere quali alternative approfondire; non sostituisce questi passaggi.
