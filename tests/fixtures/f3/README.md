# Riferimento F3

Input sintetici e acquisizioni del comportamento di ANTHEA 1.5.0, set V, prima degli spostamenti. Non sono attesi ingegneristici indipendenti: questi restano nelle suite originali e non vengono sovrascritti.

36 fixture coprono i 12 moduli, default incompleti, esempi numerici, migrazioni Nq/γb, peso del micropalo, esposizioni multiple, segni CA, coefficienti e staffe, ricerca globale, palificata, ponte con altezza d’anima precedente e accessori, Bridge Design, fondazioni v1 e scelta interattiva del palo elastico. I due pali orizzontali hanno anche risposta elastica valida. Le fixture dei singoli fogli vengono scritte una sola volta dal comando esplicito `fixtures-f3` del toolkit; il comando rifiuta destinazioni non vuote e non produce attesi.

La suite `ui/f3-equivalence` nel runner unico cattura apertura, visita delle schede e dei pannelli, calcolo, esportazione e riapertura. I report disponibili sono confrontati per testo, tabelle, stili applicati e dimensioni delle figure; i byte delle immagini restano negli artefatti e non entrano nel confronto numerico. Compresi report brevi CA, progetto con 12 fogli, 71 articoli e 30 ricerche Wiki. Il progetto sintetico usa identificativi GUID fissi: generarli a ogni esecuzione cambiava l’ordine dei conflitti a parità di contenuto. Non sono stati aggiunti ordinamenti o esclusioni ai confronti per nasconderlo.

Doppia cattura D/E: 458 file uguali, salvo il solo `tempi_ms` già escluso dal toolkit. Riferimento immutabile in `expected`, input e librerie identificati da `manifest.json`; soglie iniziali in `architecture.json`. I DOCX e le immagini restano in supporto/artefatti/ci. Il banco confronta automaticamente le catture future con questo riferimento e non lo aggiorna.

## Transizioni di F4

Decisione D2 del 15/10/2026 (`docs/refactoring/decisioni.md`). I risultati e i report del riferimento non cambiano: le
differenze introdotte da F4 sono ammesse una per una in `transitions-f4.json`. Ogni voce indica file, percorso, classe,
valore prima, valore dopo e voce del registro. Il confronto fallisce in due casi:
- una differenza che non è nell'elenco;
- una voce dell'elenco che non si presenta.

Non ci sono tolleranze nuove. Le prove negative sono in `tests/ANTHEA.Testing.Tests`.

Wiki e ricerca seguono invece la regola editoriale di F3-06: i file cambiati sono sostituiti nel riferimento.
- Rev40: `editorial_updates` del manifest.
- Rev41–Rev44: `editorial_updates_f4`, voce F4-48.
- Rev45: `editorial_updates_rev45`, frase d'installazione della guida pratica senza numero di versione.
- Rev46 e Rev47: `editorial_updates_rev47`, voce F4-51: articoli della stratigrafia (Rev46), frasi delle correzioni del 15/10/2026 e dei seguiti del 16/10 (Rev47). Impronte «prima» del riferimento della Rev45; le differenze sono lette dal confronto completo di `tools/qa/f3_confronto_completo.py`, perché confronto.json è troncato.

Le impronte prima e dopo sono calcolate sulla forma conservata da git (fine riga LF).

L'elenco e l'aggiornamento della Wiki si generano con `tools/qa/f3_transizioni.py`. Il programma parte dal confronto di
una corsa e dalla classificazione puntuale delle differenze, in `docs/refactoring/f4-transizioni-ui-classificazione.md`.
Le differenze senza voce lo fermano.

Il 16/10/2026 l'elenco è stato rigenerato per le correzioni dei bug del 15-16/10, con il set candidato Z
(cattura `20261016-005646-f3-bug1016`, commit `e34f176c`).
- Contiene 22126 voci:
  - 5442 transizioni F4 invariate;
  - 736 transizioni F4 spostate o con il valore dopo cambiato, con voce combinata come «F4-35 / BUG-…»;
  - le differenze nuove con la loro voce `BUG-*`; per SLU-2 la voce è F2-17.
- La classificazione è in `docs/refactoring/bug-1015-transizioni-ui-classificazione.md`, con le transizioni F4
  non più presenti e il motivo.
- Nell'elenco «a» resta il valore del riferimento e «b» il valore nuovo.
- Il `confronto.json` della corsa è troncato oltre 20000 dettagli, comprese le transizioni ammesse. Per rigenerare
  l'elenco servono:
  - il confronto completo senza transizioni, da `tools/qa/f3_confronto_completo.py`;
  - `f3_transizioni.py --voci`, con una voce per ogni differenza, nello stesso ordine.
- Nel manifest cambiano solo impronta e numero delle voci di `transitions`.

Comando: `build/ci.ps1 -Stage build,ui -Only '^ui/f3-equivalence$' -Tag f3-equivalence`.

## Fixture aggiunte in F5.4 (palificata orizzontale)

Proposta F5, sezione 7.2: prima di migrare un modulo con poche fixture se ne aggiungono altre, catturate con la vista
storica. Il 16/10/2026 la palificata è passata da 2 a 8 fixture (42 in tutto); gli attesi delle 36 esistenti e le
transizioni F4 non cambiano.

| Fixture | Cosa copre |
| --- | --- |
| `quinconce-direzioni` | disposizione a quinconce ruotata, D = 0,8 m; direzioni +Y, −X, −Y e personalizzata (30°), +X spenta; basamento rettangolare; Caltrans in −Y, non applicabile |
| `triangolare-avvisi` | disposizione triangolare sotto 3D; estrapolazioni e file proiettate con i due interassi di +X; avvisi di estrapolazione e di estensione geometrica («Approssimazione esplicita») |
| `esagonale-interassi` | disposizione esagonale a due anelli; file proiettate; interassi di +X completi, di +Y con il solo S∥ (chiave di S⊥ tolta); AASHTO nella direzione personalizzata (60°) |
| `pentagonale-senza-basamento` | disposizione pentagonale; archivio senza le direzioni negative (chiavi e interassi); asse–bordo 0,4D: basamento senza contorno, con il suo errore |
| `generica-coordinate` | coordinate generiche scritte come testi con la virgola, rese numeri dalla vista; basamento rettangolare; Davisson nella direzione personalizzata (135°), non applicabile |
| `generica-sovrapposti` | due pali sovrapposti: calcolo fermato con «Dati da completare», nessun risultato e nessun report |

- Gli input vengono da `fixtures-f3` (`F3Fixtures.PileGroupF54`). Sono archivi validi, cioè si aprono con
  `DocumentService.Open`. Nessun identificativo finisce con `default`, perché il report di progetto della cattura raccoglie
  solo quelle fixture.
- Gli attesi (52 file) vengono dalla cattura `20261016-104530-f54-f3-cattura-d2`, fatta sul codice di produzione di
  `5f29067b`. La seconda cattura (`…-104728-f54-f3-cattura-e2`) dà gli stessi 52 file. Con queste fixture passano
  `ui/f3-equivalence`, con le stesse transizioni F4, e `ui/f3-report-chapters`.
- Gli interassi della direzione mostrata all'apertura (+X) restano nell'archivio. Se mancano, la vista storica crea un
  oggetto vuoto, mentre la preparazione del capitolo autonomo (`ModuleSheetPreparation.PreparePileGroup`) non lo crea.
  In quel caso `ui/f3-report-chapters` segnala dati preparati diversi; il report non cambia.
- In `expected/esito.txt` è cambiato solo il numero delle fixture (36 → 42). Il numero degli articoli resta quello del
  riferimento, con la transizione F4-51.
- Impronte nel manifest, sezione `fixture_aggiunte_f5_4`, sulla forma conservata da git (fine riga LF).

## Fixture aggiunte in F5.5 (materiali)

Proposta F5, sezione 7.2: prima di migrare i materiali si aggiungono fixture catturate con le viste storiche
(`Materiali.MaterialView` e `RebarMaterialView`). Il 16/10/2026 il calcestruzzo è passato da 2 a 8 fixture e l'acciaio per
armature da 1 a 7 (54 in tutto); gli attesi delle 42 esistenti e le transizioni F4 non cambiano.

| Fixture | Cosa copre |
| --- | --- |
| `mat_calcestruzzo--ec2-riduzioni-tolleranza` | criterio EC2 2004 con le tre riduzioni della classe strutturale, vita utile di 100 anni, misura dei copriferri (5 mm), terreno preparato, XM2, superficie irregolare, Dmax 40 mm, aderenza buona, S4, CEM III 42,5 R con designazione; XD3, C45/55 |
| `mat_calcestruzzo--ntc-piastra-controllo` | NTC + Circolare per piastra con controllo dei copriferri, misura accurata con scarto (0 mm), getto direttamente su terra, XS3 con classe sotto il minimo, Dmax 16 mm, S2, CEM II 52,5 |
| `mat_calcestruzzo--gelo-aria-xf4` | XF4 con aria inglobata per Dmax 16 mm, S3, cemento da specificare, XM1, coefficienti di aderenza digitati |
| `mat_calcestruzzo--x0-classe-fuori-catalogo` | X0 (nessuna prescrizione della miscela) con C16/20, classe aggiunta fuori dal catalogo della libreria |
| `mat_calcestruzzo--dati-incompleti` | diametro, Dmax e γc non validi con EC2 e XA2: aderenza, copriferro e Dmax da completare, report con l'errore |
| `mat_calcestruzzo--legacy-tre-esposizioni` | archivio precedente con tre esposizioni e la principale non prima, numeri, scelte e opzioni parziali, tolleranza mancante per un controllo con misura, chiave sconosciuta tolta al salvataggio |
| `mat_acciaio_armatura--b450a` | acciaio B450A del catalogo, con riferimento |
| `mat_acciaio_armatura--storico-feb38k-eps` | acciaio storico con εu definito e diagramma incrudente |
| `mat_acciaio_armatura--storico-feb22k-incompleto` | acciaio storico senza εu: proprietà ferme con il messaggio, report con l'errore |
| `mat_acciaio_armatura--personalizzato-incrudente` | acciaio personalizzato incrudente, valori con la virgola, riferimento su due righe |
| `mat_acciaio_armatura--catalogo-precedente-b450a` | B450A salvato con il catalogo precedente (εu = 30 ‰, DET-3): non è più il preset e lo si dichiara senza riscrivere i valori |
| `mat_acciaio_armatura--personalizzato-non-valido` | fu minore di fyk: proprietà ferme con il messaggio, report con l'errore |

- Gli input vengono da `fixtures-f3` (`F3Fixtures.MaterialsF55`). Sono archivi validi per la scheda (versione e forma dei
  dati) e nessun identificativo finisce con `default`, perché il report di progetto della cattura raccoglie solo quelle
  fixture.
- Gli attesi (108 file) vengono dalla cattura `20261016-134413-f55-f3-cattura-1`, fatta sul codice di produzione di
  `3c336992`: senza attesi, le sole differenze erano i 108 file aggiunti e il numero di `esito.txt`. La seconda cattura
  (`…-134751-f55-f3-cattura-2`) dà gli stessi 108 file e passa con le transizioni F4 invariate; con queste fixture passa
  anche `ui/f3-report-chapters`.
- I materiali non hanno risultati di calcolo (`result.json` nullo, `hasResults` falso): «Calcola» conferma solo la scheda.
- In `expected/esito.txt` è cambiato solo il numero delle fixture (42 → 54). Il numero degli articoli resta quello del
  riferimento, con la transizione F4-51.
- Impronte nel manifest, sezione `fixture_aggiunte_f5_5`, sulla forma conservata da git (fine riga LF).

## Fixture aggiunte in F5.6 (palo e micropalo verticale)

Stessa regola di F5.4 (proposta F5, sezione 7.2): prima della preparazione di F5.6 il palo e il micropalo verticale sono
passati da 3 + 3 a 7 + 6 fixture (49 in tutto), catturate con la vista storica (ramo generico di `SheetEditor` e
`GeoEditor`). Gli attesi delle 42 fixture esistenti, le figure congelate esistenti e le transizioni F4 non cambiano.

| Fixture | Cosa copre |
| --- | --- |
| `geo_palo_verticale--battuto-falda-sondaggi` | palo battuto prefabbricato, falda a 2,5 m con sottospinta, due stratigrafie con strati granulari e coesivi (ramo non drenato), ξ di tre verticali, Converse–Labarre, γb della tecnologia; compressione non verificata |
| `geo_palo_verticale--elica-coesivo-avvisi` | elica continua in terreni coesivi, falda al piano campagna senza sottospinta, primo strato escluso dalla laterale, Feld, γb del foglio diverso da quello della tecnologia (avviso), trazione non inserita |
| `geo_palo_verticale--copertura-insufficiente` | palo trivellato più lungo delle stratigrafie (risultati limitati alla quota disponibile, avviso della scheda), tre stratigrafie, efficienza definita dall'utente con la virgola, visibilità dei grafici salvata nel foglio |
| `geo_palo_verticale--dati-incompleti` | strato coesivo sotto falda senza Cu: calcolo fermato con «Dati da completare», nessun risultato e nessun report |
| `geo_micropalo_verticale--irs-punta-inclinato` | micropalo IRS con punta al 10%, inclinato di 15°, aderenza da 2 m lungo l'asse, tubo CHS, due stratigrafie (uno strato escluso), Converse–Labarre |
| `geo_micropalo_verticale--igu-copertura-avvisi` | micropalo IGU senza punta, stratigrafie più corte del micropalo, Feld, tre stratigrafie, visibilità dei grafici salvata nel foglio |
| `geo_micropalo_verticale--dati-incompleti` | contributo di punta oltre il 15%: calcolo fermato con «Dati da completare» |

- Gli input vengono da `fixtures-f3` (`F3Fixtures.VerticalPileF56`). Sono documenti come li scrive la scheda (testi per i
  valori digitati, anche con la virgola; booleani JSON per le opzioni) e archivi validi. Nessun identificativo finisce con
  `default`.
- Gli attesi (59 file) vengono dalla cattura `20261016-134647-f56-f3-cattura-d`, sul codice di produzione di `3c336992`;
  la seconda cattura (`…-134953-f56-f3-cattura-e`) dà gli stessi 59 file.
- `ui/f3-figures` confronta le serie dei grafici di ogni fixture dei pali verticali: le 7 figure nuove di
  `tests/fixtures/f3-figures` vengono da due catture uguali di `--check-f3-figures-capture`, nelle quali le 6 esistenti sono
  uguali al riferimento congelato.
- Con queste fixture passano `ui/f3-equivalence` (stesse transizioni F4), `ui/f3-report-chapters`, `ui/f3-figures` e
  `ui/f3-scheduling`.
- In `expected/esito.txt` è cambiato solo il numero delle fixture (42 → 49).
- Impronte nel manifest, sezione `fixture_aggiunte_f5_6` (input, attesi, figure ed `esito.txt`), sulla forma conservata da
  git (fine riga LF).

## Fixture aggiunte in F5.9b (fondazioni superficiali)

Stessa regola di F5.4 (proposta F5, sezione 7.2): prima della preparazione di F5.9b le fondazioni superficiali sono passate da
3 a 11 fixture (69 in tutto), catturate con la vista storica (`FoundationWorkspace`). Gli attesi delle 61 fixture esistenti e
le transizioni F4 non cambiano.

| Fixture | Cosa copre |
| --- | --- |
| `terzaghi-nastriforme-falda` | Terzaghi su fondazione nastriforme (azioni per metro), riduzione per taglio locale, base ruvida, falda sotto la base, valori con la virgola; non verificata |
| `hansen-non-drenato-caratteristico` | Brinch Hansen non drenato su strato coesivo: correzioni additive; confronto caratteristico senza conferma A1 (casella nascosta); momenti sui due lati; calcestruzzo personalizzato scritto con fck |
| `meyerhof-eccentrica-input-raccolti` | Meyerhof con grande eccentricità lungo B (area compressa minore della base), sovraccarico esterno, fattori di profondità esclusi; input raccolti e tre gruppi chiusi in `vista_fondazioni`: la visita li riapre e li scrive |
| `vesic-circolare-due-sondaggi` | Vesić su cerchio centrato, riduzione per densità relativa (sabbia drenata, c′ = 0), falda sopra la base, due sondaggi con il secondo scelto, γsat vuoto che segue γ, calcestruzzo C25/30 del catalogo; non verificata |
| `ec7-fuori-dominio` | EC7 con risultante esterna alla base: resistenza nulla, rapporti indefiniti, viste dei risultati non disponibili; report disponibile con l'esito negativo |
| `strato-incompleto` | spessore di uno strato non numerico: calcolo fermato, nessun risultato e nessun report, profilo non disegnato |
| `semispazio-non-assunto` | strato portante non assunto come semispazio: la libreria rifiuta il calcolo, nessun risultato e nessun report |
| `legacy-falda-piano-campagna` | archivio di versione 1 con falda e base al piano campagna (D = 0 scritto come testo): la migrazione crea il solo strato portante e porta la falda nei dati comuni |

- Gli input vengono da `fixtures-f3` (`F3Fixtures.ShallowFoundationF59b`). Sono documenti come li scrive la scheda (testi per
  i valori digitati, anche con la virgola; indici per le scelte; booleani JSON per le opzioni) e archivi validi. Nessun
  identificativo finisce con `default`. Prima della cattura ogni input è stato calcolato con il servizio del modulo per
  verificare l'esito descritto.
- Gli attesi (68 file) vengono dalla cattura `20261016-221404-f59b-f3-cattura-1`, sul codice di produzione di `92613e60`:
  senza attesi, le sole differenze erano i 68 file aggiunti e il numero di `esito.txt`. La seconda cattura
  (`…-221601-f59b-f3-cattura-2`) dà gli stessi file byte per byte e passa con le transizioni F4 invariate; con queste fixture
  passa anche `ui/f3-report-chapters` (70 capitoli).
- Il generatore `fixtures-f3` oggi produce input diversi da quelli congelati per dieci fixture più vecchie (muri,
  calcestruzzo, Bridge Design, sezione c.a.), per l'evoluzione dei default dei cataloghi: si sono copiate solo le 8 fixture
  nuove, come in F5.4–F5.6.
- In `expected/esito.txt` è cambiato solo il numero delle fixture (61 → 69).
- Impronte nel manifest, sezione `fixture_aggiunte_f5_9b` (input, attesi ed `esito.txt` prima e dopo), sulla forma conservata
  da git (fine riga LF).

## Fixture aggiunte in F5.7 (palo e micropalo orizzontale)

Stessa regola di F5.4 (proposta F5, sezione 7.2): prima della preparazione di F5.7 il palo e il micropalo orizzontale sono
passati da 5 + 5 a 8 + 8 fixture (67 in tutto), catturate con la vista storica (`HorizontalWorkspace`). Fra le 10 esistenti
calcolavano la capacità solo le due `single-soil-layer` (una stratigrafia granulare, Broms, testa libera); le altre sono
default incompleti o fixture della risposta elastica. Le 6 nuove restano sulla scheda della capacità (`vista_orizzontale`
assente): la risposta elastica è F5.8, quindi nessuna tocca le transizioni F4 dell'elastico (BUG-PO-4, BUG-PO-5, F4-15).
Gli attesi delle 61 fixture esistenti e le transizioni F4 non cambiano.

| Fixture | Cosa copre |
| --- | --- |
| `geo_palo_orizzontale--broms-coesivo-impedita` | palo corto (L = 3 m) in terreni coesivi, Broms con testa impedita: meccanismo corto nella prima stratigrafia, intermedio nella seconda; falda con γw = 10, forza assiale, ξ di tre verticali, efficienza di Reese e Van Impe con gli interassi (anche con la virgola), momento della sezione c.a. con secondo anello interno |
| `geo_palo_orizzontale--stratificato-misto-manuale` | metodo stratificato su sequenza granulare–coesivo–granulare (modello sperimentale), falda a 3,5 m, momento manuale con la sua fonte, coefficienti unitari della sezione, efficienza manuale 0,85, ξ di almeno dieci verticali |
| `geo_palo_orizzontale--dati-incompleti` | Broms con una stratigrafia più corta del palo e una mista: calcolo fermato con «Dati da completare», momento della sezione mostrato, nessun report |
| `geo_micropalo_orizzontale--chs-manuale-impedita` | tubo CHS manuale con peso dell'iniezione, testa impedita, falda a 1,5 m, due stratigrafie granulari (forza concentrata di Broms), ξ di due verticali, efficienza di Reese e Van Impe |
| `geo_micropalo_orizzontale--stratificato-chs-catalogo` | metodo stratificato su sequenza coesivo–granulare, eccentricità 0,3 m, momento del CHS 193.7 × 12.5 del catalogo, efficienza manuale |
| `geo_micropalo_orizzontale--dati-incompleti` | tubo CHS manuale senza spessore: sezione e capacità fermate con «Dati da completare» |

- Gli input vengono da `fixtures-f3` (`F3Fixtures.HorizontalPileF57`). Sono documenti come li scrive la scheda (testi per i
  valori digitati, anche con la virgola; booleani JSON per le opzioni) e archivi validi. Nessun identificativo finisce con
  `default`.
- Gli attesi (50 file) vengono dalla cattura `20261016-221302-f57-f3-cattura-d`, sul codice di produzione di `92613e60`;
  la seconda cattura (`…-f57-f3-cattura-e`) dà gli stessi 50 file.
- Con queste fixture passano `ui/f3-equivalence` (stesse transizioni F4) e `ui/f3-report-chapters`. I pali orizzontali non
  hanno figure congelate in `f3-figures`.
- In `expected/esito.txt` è cambiato solo il numero delle fixture (61 → 67).
- Impronte nel manifest, sezione `fixture_aggiunte_f5_7` (input, attesi ed `esito.txt`), sulla forma conservata da git (fine

## Fixture aggiunte in F5.6b (stratigrafia)

Stessa regola di F5.4 (proposta F5, sezione 7.2). Prima di F5.6b la stratigrafia (`geo_stratigrafia`, modulo del 15/10) non
aveva fixture. Il 17/10/2026 ne ha ricevute cinque (66 in tutto), catturate con la vista storica (`StratigraphyWorkspace` in
`SheetEditor`). Gli attesi delle 61 fixture esistenti e le transizioni F4 non cambiano.

| Fixture | Cosa copre |
| --- | --- |
| `profilo-medi-falda` | profilo completo con quota e falda a 2,5 m, quattro strati finiti (riporto, sabbia, limo, argilla) granulari e coesivi, soli medi, qualificazioni diverse; nessun avviso, trasferibile ai moduli di verifica |
| `minimi-entrambi-indefinito` | «Entrambi»; minimi completi, minimi incompleti e maggiori dei medi, γsat minore di γ, ultimo strato indefinito senza cu e senza minimi, falda non nota: tutti gli avvisi della scheda |
| `testi-digitati` | valori come li scrive la scheda: testi con la virgola, testi vuoti (dato assente), «Minimi», falda non considerata, strato senza la chiave del comportamento, colore in minuscolo, descrizione su due righe |
| `schema1-importata` | documento del primo schema, senza scelta, famiglia, comportamento e minimi; falda al piano campagna, ultimo strato indefinito, provenienza di un'importazione AI revisionata (data fissa, nessuna chiave) |
| `trasferita-da-palo` | stratigrafia ricevuta da un foglio del palo verticale (`StratigraphyTransfer.ToDocument`): testi del palo, comportamento dal tipo, note del trasferimento, γsat vuoto, provenienza del trasferimento |

- Gli input vengono da `fixtures-f3` (`F3Fixtures.StratigraphyF56b`). Gli identificativi sono fissi e i documenti sono validi
  (`StratigraphyDocument.Validate`): la scheda valida il documento a ogni conferma, quindi un documento non valido non si può
  catturare. Nessun identificativo finisce con `default`.
- Gli attesi (45 file) vengono dalla cattura `20261017-005645-f56b-f3-cattura-1`, sul codice di produzione di `92613e60`; la
  seconda cattura (`…-005858-f56b-f3-cattura-2`) dà gli stessi 45 file.
- **Report.** La stratigrafia non calcola: `result.json` è nullo e `hasResults` falso. Il report è la scheda dati che la vista
  esporta (`SheetEditor.ExportReport`), catturata come quella degli altri moduli (`report.txt`, `report-structure.json`).
  `ui/f3-report-chapters` la confronta parte per parte con il capitolo autonomo di `ModuleReportService`; il ramo della
  stratigrafia in `F3ReportChapterChecks` esporta il report della vista, perché `BuildReport` della vista storica chiede un
  calcolo.
- Con queste fixture passano `ui/f3-equivalence` (stesse transizioni F4) e `ui/f3-report-chapters`.
- In `expected/esito.txt` è cambiato solo il numero delle fixture (61 → 66).
- Impronte nel manifest, sezione `fixture_aggiunte_f5_6b` (input, attesi ed `esito.txt`), sulla forma conservata da git (fine
  riga LF).

## Fixture aggiunte in F5.10 (sezione c.a.)

Stessa regola di F5.4 (proposta F5, sezione 7.2). Prima della preparazione di F5.10 la sezione c.a. (`str_palo`) aveva quattro
fixture, tutte rettangolari, con le azioni nel formato storico e le opzioni predefinite. Il 17/10/2026 ne ha ricevute sette (87 in
tutto), catturate con la vista storica (`ConcreteWorkspace` in `SheetEditor`), che coprono i sottopassi di F5.10. Gli attesi delle 80
fixture esistenti, le figure CA congelate e le transizioni F4 non cambiano.

| Fixture | Cosa copre |
| --- | --- |
| `circolare-cava` | sezione circolare cava (NTC 2018, C40/50 di catalogo) con secondo anello interno; spirale con due staffe interne sovrapposte ruotate di 30°; modello di taglio circolare delle pile, due righe di taglio (una con torsione e staffe chiuse da confermare); dominio 2D N–M a θ = 30° con le azioni proiettate; resistenze rapide elastiche; pilastro con le barre compresse trattenute; quota dell'interferro spenta; la scheda si apre sul dominio 2D |
| `sezione-a-t-sollecitazioni-uniche` | sezione a T con le barre sotto l'ala (EN 1992-1-1, C30/37 e B500B di catalogo); sollecitazioni uniche con stato limite ed elemento per riga, taglio e torsione nella stessa riga (le righe con T si fermano con il messaggio del modello NTC), una riga nascosta; criterio resistente per dominio (plastico 3D a eccentricità costante, elastico 2D su SLV); trave con appoggi e barre trattenute confermati; sovrapposizione del 50 % delle barre; la scheda si apre sul dominio 3D |
| `rettangolare-materiali-utente` | materiali definiti dall'utente (DIN EN 1992-1-1): calcestruzzo con legge bilineare, acciaio incrudente, elenco salvato (calcestruzzo, acciaio, trefolo) e legge scelta per un trefolo di catalogo; αcc e γc cambiati, piano gettato sottile; barre per coordinate; criterio Mx–My costanti con il metodo delle intersezioni; SLE non lineare; percorso M–χ a 45° con tension stiffening; opzioni delle proprietà, nota del foglio e percorso del file delle sollecitazioni |
| `sle-fessurazione-assi` | SLE con Model Code 2010: esposizione XD3, armatura sensibile, carico breve, copriferro e interasse per l'apertura e limite di progetto; assi personalizzati con origine e rotazione, φ assegnato; contorni ed etichette delle tre mappe, asse neutro spento; una riga frequente in trazione; dominio 2D sugli assi principali con le azioni proiettate; la scheda si apre sulle SLE |
| `taglio-senza-staffe-manuali` | taglio senza staffe (UNI EN 1992-1-1): sezione senza staffe, modello senza staffe, parametri geometrici manuali con Asl ancorata confermata; tre righe di taglio storiche (completa, senza Vy, con torsione); pilastro |
| `soletta-dettagli-ricerca` | striscia di soletta (NTC 2018): dettagli della soletta con zona critica, armatura ortogonale, zona di sovrapposizione, 100 anni, controllo dei copriferri, sovrapposizione di metà delle barre; una riga SLU nel dominio e una fuori, una rara oltre i limiti tensionali; opzioni della ricerca delle armature (compromesso con i pesi, senza staffe, barre ortogonali, ricerca a campione, due calcoli contemporanei, prime cinque, limite SLU); la scheda si apre sulla ricerca |
| `dati-incompleti-avvisi` | dati incompleti che fermano singole righe o le resistenze rapide, con gli avvisi della scheda: archivio senza due valori di ingresso (la scheda li integra e li mostra), un'azione senza N e una con testo in Mx, una riga di taglio senza Vx, N delle resistenze rapide non numerico; esposizione, elemento e ancoraggio da scegliere; meno direzioni del dominio 3D; una riga di taglio con torsione e staffe chiuse confermate |

- Gli input vengono da `fixtures-f3` (`F3Fixtures.ConcreteSectionF510`). Sono documenti come li scrive la scheda prima della sua
  preparazione: area di lavoro di versione 2 con la normativa, le chiavi che la scheda scriverebbe, testi per i valori digitati;
  identificativi fissi; archivi validi. Nessun identificativo finisce con `default`. Prima della cattura ogni input è stato calcolato
  con le sessioni di Application per verificare l'esito descritto.
- **Limiti voluti.** Nessuna fixture ha trefoli: la curva M–χ con trefoli non è disponibile («definire prima il criterio di
  snervamento/predeformazione») e `ui/f3-report-chapters` la calcola per ogni fixture c.a. con risultati. Nessuna ferma l'intero
  calcolo: `ConcreteDomainContractChecks` (Application.Tests) calcola la sezione di ogni fixture `str_palo`, e una geometria o un
  materiale non validi la fermerebbero. Trefoli e calcolo bloccato sono nelle tracce di F5.10 (`tests/fixtures/f5-sezione`).
- **Ordine delle righe.** Le azioni di ogni stato limite sono scritte nell'ordine dei nomi. Con un ordine diverso
  `ui/f3-report-chapters` falliva (prima versione di `rettangolare-materiali-utente`): le figure dei domini del report della vista
  evidenziano la combinazione selezionata nella tabella, mentre il capitolo autonomo evidenzia la prima per nome. La differenza è
  della vista storica ed è descritta in `docs/refactoring/f5-10-sezione.md`; non è stata corretta.
- **Nessuna verifica fermata per intero.** La prima versione di `dati-incompleti-avvisi` aveva direzioni del dominio 3D rifiutate
  dal motore: le due verifiche 3D (SLU e SLV) si fermavano insieme e l'ordine dei loro errori nel risultato e nel report
  dipendeva dal thread che finiva prima (catture `…-f510-f3-cattura-3` e `-4` diverse, `ui/f3-report-chapters` «Errori parziali
  diversi»). Anche questa differenza è della vista storica (descritta nello stesso documento); la fixture ferma solo singole righe.
- Gli attesi (77 file) vengono dalle catture `20261017-030549-f510-f3-cattura-3` (sei fixture) e `…-031445-f510-f3-cattura-5`
  (`dati-incompleti-avvisi`), sul codice di produzione di `743d7aec`: senza attesi, le sole differenze erano i file aggiunti. La
  corsa di controllo `…-031805-f510-f3-cattura-6` dà gli stessi 77 file byte per byte (le sei fixture sono uguali anche nelle catture
  3 e 5) e passa con le transizioni F4 invariate. Le catture `…-025535-f510-f3-cattura-1`, `…-f510-f3-cattura-2` e
  `…-f510-f3-cattura-4` sono delle prime versioni degli input, scartate (punti seguenti).
- `ui/f3-concrete-figures` confronta i dati dei grafici del dominio 2D di ogni fixture c.a.: le 7 figure nuove di
  `tests/fixtures/f3-concrete-figures` vengono dalle catture 3 e 5 (`-CaptureConcreteFigures`), nelle quali le 4 esistenti sono
  uguali al riferimento congelato; la corsa 6 le confronta. `F3ConcreteFigureChecks` conta ora 11 figure invece di 4.
- Con queste fixture passano `ui/f3-equivalence` (stesse transizioni F4), `ui/f3-report-chapters` e `ui/f3-concrete-figures`.
- Prove di Application che leggono ogni fixture `str_palo`: `ConcreteDomainContractChecks` conta 11 fixture invece di 4;
  `ConcretePresentationChecks` ricalcola i dettagli costruttivi con le righe di tutti gli stati limite, come la scheda e il report
  (`ConcreteSheetPreparation.PrepareReport`), e non con la sola N delle righe SLU e SLV: con un elemento scelto il riepilogo nomina la
  riga della compressione più gravosa, e prima di F5.10 nessuna fixture sceglieva l'elemento.
- In `expected/esito.txt` è cambiato solo il numero delle fixture (80 → 87).
- Impronte nel manifest, sezione `fixture_aggiunte_f5_10` (input, attesi, figure ed `esito.txt`), sulla forma conservata da git
  (fine riga LF).
