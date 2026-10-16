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
  riga LF).
