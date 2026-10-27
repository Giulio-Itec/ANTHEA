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

Rev48, Spettri NTC e isolamento (19/10/2026): `editorial_updates_rev48` conserva le impronte di quattro nuovi file dei due articoli e della ricerca. Registro SPETTRI-04. Il set candidato Geotechnics 0.4.0.1 cambia solo la versione esposta in otto risultati e otto report: cinque transizioni precedenti (quattro versioni e il numero degli articoli) sono sostituite e dodici aggiunte, per 22138 voci. Nessun atteso numerico, risultato o report storico è sostituito e nessuna tolleranza cambia.

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

Il 22/10/2026, con il set candidato del 22/10 (Model 45c3976c, Checker f6916d8e) e con l'ok dell'utente, l'elenco è stato
esteso senza toccare le voci esistenti (28977 voci):
- 3941 voci PARATIE-19 (taglio dei pali circolari, anello continuo): VRd, rapporti e lunghezze della distinta di
  `elastico-sisma-testa-impedita`, `elastico-tratti-anello-spirale` e `single-soil-elastic` del palo orizzontale;
- 2898 voci PARATIE-20 (cerchi di Bishop con φ = 0): la stabilità globale di `globale-non-drenata-sisma` dei muri.

«a» e «b» vengono dal confronto del ViewModel con il riferimento in `Presentation.Tests`, uguale alla vista storica (tracce
F5). Le aggiunte sono descritte nelle chiavi `aggiornamento_paratie_s2` e `aggiornamento_paratie_phi0` dell'elenco. La prova
`ui/f3-equivalence` sulla vista storica con questo set non è ancora stata eseguita.

Il 24/10/2026, con il set AG (Model 2d59bbc6, Checker f6d87d28, Concrete 0.20.0.0: parabola-rettangolo analitica come legge
predefinita) e con la decisione dell'utente («registra e aggiorna»), l'elenco è stato esteso per la sezione c.a. (29358 voci):
- 381 voci AG-CA-01 nuove: punti resistenti dei domini SLU/SLV 2D e 3D (resistenze, risposte, utilizzi, rapporti e i loro
  numeri nei report) di dieci fixture `str_palo`;
- 19 transizioni F2-17 (SLU-2) di `legacy-calculation-settings` sostituite in posto: stesso «a», «b» nuovo, voce
  «F2-17 (SLU-2) / AG-CA-01».

«a» e «b» vengono dal comparatore del toolkit sui 121 file della sezione catturati da `Presentation.Tests/sezione-ca`. La fixture
`rettangolare-materiali-utente` non ha voci: due punti resistenti della strategia «Intersezione» ora rifiutati dalla libreria
cambiano il verdetto (AG-CA-03, da decidere), quindi la suite resta rossa su quella fixture. Chiave `aggiornamento_ag_ca`
dell'elenco. `ui/f3-equivalence` con questo set non è ancora stata eseguita.

Il 24/10/2026 (sera), con la release AG ufficiale (4dd8018a: Model 2d59bbc6, Checker 22dabe69, GPCChecker.Concrete 0.20.0.1, che
non rifiuta più i punti della strategia «Intersezione», AG-CA-03 corretta), gli 11 file F3 di `rettangolare-materiali-utente` sono
uguali byte per byte al riferimento (valori AF): nessuna transizione da aggiungere, elenco invariato. Le figure CA congelate di
`tests/fixtures/f3-concrete-figures` (dati dei grafici del dominio 2D, `ui/f3-concrete-figures`) sono ricatturate con
`-CaptureConcreteFigures` dalla vista XAML (corsa `20261024-225640-ag-ca2-cattura`, voce AG-CA-05): 10 file su 11
(`rettangolare-materiali-utente` invariato), stessa struttura e stesso numero di segmenti, linee e marcatori; cambiano solo le
coordinate, al massimo lo 0,27 % della scala del grafico (i marcatori dei punti resistenti fino allo 0,21 % in modulo), e i residui
numerici dei segmenti sugli assi (per esempio −1,19 → 2e-12). Nel manifest, sezione `fixture_aggiunte_f5_10`, cambiano le impronte
delle 6 figure elencate, con una voce `updated` ciascuna; le 4 figure precedenti a F5.10 non hanno impronta nel manifest.

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

## Fixture aggiunte in F5.9 (muri di sostegno)

Stessa regola di F5.4 (proposta F5, sezione 7.2): prima della preparazione di F5.9 i muri di sostegno sono passati da 4 a 16
fixture (81 in tutto), catturate con la vista storica (`RetainingWallWorkspace`). Gli attesi delle 69 fixture esistenti e le
transizioni F4 non cambiano: le 14 441 transizioni dei muri riguardano solo le 4 fixture precedenti.

| Fixture | Cosa copre |
| --- | --- |
| `mensola-sisma-sito-pendio` | sisma dai parametri del sito allo SLV (Mononobe–Okabe, categoria C, pendio di 20° alto 40 m con il muro a 30 m: St > 1, kh e kv ricavati come li scrive la scheda); due zone di armatura con facce diverse nel fusto e zona superiore più leggera; gemelle «b» della matrice automatica |
| `mensola-wood-azioni` | sisma assegnato con Wood; azioni di ogni tipo e natura: forza orizzontale e pressione laterale G2 correlate, forza verticale e momento Q nel gruppo del sovraccarico (il momento non disegnato), urto eccezionale, un sovraccarico disattivato |
| `gravita-cls-passiva` | gravità in calcestruzzo non armato (C25/30); due strati diversi a monte e uno a valle, passiva a valle con frazione mobilitata e altezza libera assegnata, attrito automatico sul fusto (gettato in opera) e alla base (prefabbricato) |
| `gravita-muratura-avvisi` | gravità in muratura troppo snella: verifiche non soddisfatte e fuori campo; lunghezza efficace ed eccentricità assegnate |
| `matrice-personalizzata` | matrice confermata: sovraccarico variabile escluso dalla mensola nella prima riga SLU, fattore del terreno modificato, γR,e della passiva assegnato, riga copiata, una riga SLE disattivata; passiva a valle |
| `globale-drenata-due-colonne` | stabilità globale drenata a due colonne, linea di falda propria sotto la fondazione e in salita a monte, ricerca assegnata con la griglia predefinita: una combinazione soddisfatta e una no |
| `globale-non-drenata-sisma` | stabilità globale non drenata su profilo unico (cu degli strati profondi) con il sisma dello SLV dal sito, lo stesso delle verifiche locali; ricerca grossolana: l'esito chiede più conci |
| `opzioni-avanzate` | cedimenti e spostamenti con due strati deformabili, portanza sismica con accelerazioni assegnate, accelerogramma di Newmark, dettagli delle armature con buona aderenza, copriferro EC2 con le riduzioni, calcestruzzo personalizzato e acciaio B450A, tratto di 6 m per la distinta |
| `mensola-avvisi` | mensola a valle assente e mensola a monte corta (scorrimento e ribaltamento non soddisfatti, perdita di equilibrio), acciaio salvato con il nome del catalogo e valori diversi (DET-3), copriferro sotto il minimo per XC4 |
| `stratigrafia-incompleta` | stratigrafia di monte che non raggiunge il piano di posa e φ′ vuoto a valle: calcolo fermato, nessun risultato e nessun report |
| `matrice-da-rigenerare` | matrice personalizzata confermata prima di una modifica delle azioni: il calcolo chiede di rigenerare le combinazioni |
| `tipologia-prevista` | tipologia «Semigravità» predisposta per sviluppi futuri: il foglio si apre, nessun calcolo |

- Gli input vengono da `fixtures-f3` (`F3Fixtures.RetainingWallF59`). Sono documenti come li scrive la scheda (numeri per i
  valori, booleani JSON per le opzioni, kh e kv ricavati dal sito quando il sisma è dai parametri del sito) e archivi validi.
  Nessun identificativo finisce con `default`. Prima della cattura ogni input è stato provato con la vista storica in una radice
  temporanea, per verificare l'esito descritto e contenere la dimensione dei risultati (le azioni correlate e la falda
  moltiplicano le combinazioni).
- Gli attesi (102 file) vengono dalla cattura `20261017-005656-f59m-f3-cattura-1`, sul codice di produzione di `6ea62a0f`:
  senza attesi, le sole differenze erano i file aggiunti e il numero di `esito.txt`. La seconda cattura
  (`…-005933-f59m-f3-cattura-2`) dà gli stessi file byte per byte. Le due fixture con il sisma del sito sono state rigenerate
  con kh e kv già ricavati, come li salva la scheda: con i valori del catalogo (0,1 e 0,05) `ui/f3-report-chapters` segnalava
  dati preparati diversi, perché la vista li ricava all'apertura e il capitolo autonomo del report di progetto no (il calcolo li
  ricava comunque; vedi `docs/refactoring/f5-9-muri.md`). I loro attesi vengono dalla cattura `…-010331-f59m-f3-cattura-3`,
  uguale byte per byte alla `…-010604-f59m-f3-cattura-4`, che passa con le transizioni F4 invariate; con queste fixture passano
  anche `ui/f3-report-chapters` (82 capitoli) e `ui/f3-scheduling`.
- Il generatore `fixtures-f3` crea identificativi delle azioni nuovi a ogni esecuzione e oggi produce input diversi da quelli
  congelati per dieci fixture più vecchie: si sono copiate solo le 12 fixture nuove, come in F5.4–F5.9b.
- In `expected/esito.txt` è cambiato solo il numero delle fixture (69 → 81).
- Impronte nel manifest, sezione `fixture_aggiunte_f5_9` (input, attesi ed `esito.txt` prima e dopo), sulla forma conservata
  da git (fine riga LF).

## Fixture aggiunte in F5.8 (risposta elastica del palo e armature)

Stessa regola di F5.4 (proposta F5, sezione 7.2): prima della preparazione di F5.8 le fixture aperte sulla scheda «Risposta
elastica · trave su molle» (`vista_orizzontale` = `elastico`) sono passate da 6 a 11 (80 in tutto), catturate con la vista
storica (`ElasticPileWorkspace` ospitata da `HorizontalWorkspace`). Delle 6 esistenti davano un risultato elastico solo le due
`single-soil-elastic` (un solo strato, un solo tratto collegato alla sezione principale; per il micropalo le armature non si
applicano al CHS); le altre sono default incompleti o archivi da migrare. Gli attesi delle 75 fixture esistenti e le
transizioni F4 non cambiano: le 1 117 transizioni dell'elastico restano tutte su `geo_palo_orizzontale/single-soil-elastic`.

| Fixture | Cosa copre |
| --- | --- |
| `geo_palo_orizzontale--elastico-tratti-anello-spirale` | tre tratti: il primo con la sezione principale, uno personalizzato con la spirale, uno personalizzato con il secondo anello interno; tratto libero 1,5 m con eccentricità, due stratigrafie con la seconda scelta (coesivo–granulare), punta a cerniera, passo con la virgola, ipotesi approvate («Approva tutto · ipotesi»), barre commerciali di 8 m, opzioni del disegno (molle sì, nodi e q no); distinta con due giunti |
| `geo_palo_orizzontale--elastico-sisma-testa-impedita` | zona sismica di testa con estensione assegnata di 9 m (10D = 8 m) e azioni confermate («Approva tutto · sisma»), testa impedita, punta incastrata, falda con γw = 10, strati con kh assegnato, tabella 14.6 dei coesivi e nh assegnato, secondo tratto con armatura trasversale «Da definire», regole di dettaglio Trave, copriferro esplicito, minimi dei pali non controllati; zona sismica e tratti mostrati, kh nascosto |
| `geo_palo_orizzontale--elastico-stratigrafia-da-scegliere` | due stratigrafie e nessuna scelta per la risposta elastica: calcolo fermato («Selezionare la stratigrafia della risposta elastica.»), nessun risultato e nessun report |
| `geo_micropalo_orizzontale--elastico-chs-stratificato` | CHS 168.3 × 10 del catalogo con peso dell'iniezione, testa impedita, tratto libero 1 m, falda a 1,5 m, strato coesivo (tabella 14.6) e granulare (correlazione delle sabbie con override di nh e sua motivazione), punta a cerniera, passo fine; tratti, zona sismica e N nascosti |
| `geo_micropalo_orizzontale--elastico-chs-ej-assegnato` | tubo CHS manuale 139,7 × 8 con EJ assegnato e motivazione, eccentricità 0,2 m, due stratigrafie con la prima scelta, k distribuito assegnato e tabella 14.5 delle sabbie, punta libera |

- Gli input vengono da `fixtures-f3` (`F3Fixtures.HorizontalPileF58`): dati preparati come li prepara la vista
  (`ElasticHorizontalPile.PrepareShared`), poi le scelte dell'utente; testi per i valori digitati, anche con la virgola;
  booleani JSON per le opzioni. Nessun identificativo finisce con `default`. Si sono copiate solo le 5 fixture nuove: il
  generatore produce input diversi da quelli congelati per alcune fixture più vecchie, come già in F5.4–F5.9b.
- Gli attesi (43 file) vengono dalla cattura `20261017-013441-f58-f3-cattura-a`, sul codice di produzione di `4b6a59de`:
  senza attesi, le sole differenze erano i 43 file aggiunti e il numero di `esito.txt`. La seconda cattura
  (`…-013654-f58-f3-cattura-b`) dà gli stessi file byte per byte tranne i tempi misurati `tempi_ms` dei due `result.json` del
  palo con armature, chiave volatile di `tests/ANTHEA.Testing/tolerances.json` (come per `single-soil-elastic`); senza quella
  chiave i due JSON sono uguali.
- Con queste fixture passano `ui/f3-equivalence` (stesse transizioni F4) e `ui/f3-report-chapters` (81 capitoli).
- In `expected/esito.txt` è cambiato solo il numero delle fixture (75 → 80).
- Impronte nel manifest, sezione `fixture_aggiunte_f5_8` (input, attesi ed `esito.txt`), sulla forma conservata da git (fine
  riga LF).
- Le tracce F5.7 della scheda della capacità (`tests/fixtures/f5-palo-orizzontale/storica`) hanno uno scenario per fixture dei
  due moduli: si sono aggiunte, catturate con la vista storica, le tracce delle 5 fixture nuove (27 scenari).

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

## Fixture aggiunte in F5.11 (sezione composta da ponte)

Stessa regola di F5.4 (proposta F5, sezione 7.2). Prima della preparazione di F5.11 la sezione composta da ponte (`str_mista_ponte`)
aveva due fixture: la trave a H predefinita con il metodo cumulativo, senza accessori, e la stessa in un archivio precedente
(`legacy-web-and-accessories`). Il 21/10/2026 ne ha ricevute sette (111 in tutto), catturate con la vista storica (`BridgeWorkspace`
in `SheetEditor`). Gli attesi delle 104 fixture esistenti e le transizioni F4 non cambiano: le 12 transizioni della sezione composta
(`cls_teso_nl` aggiunto, BUG-PON-2) restano sulle due fixture precedenti.

| Fixture | Cosa copre |
| --- | --- |
| `anima-inclinata-ec4-sle` | H con anima inclinata (scostamento 300 mm) con EN 1994-2 (coefficienti della normativa come li scrive la scheda), SLE rara, C40/50, fy assegnato; predalle con i ferri inferiori riferiti all'intradosso della soletta; fasi di solo acciaio, composta con n assegnato (modo «Da n»), ritiro, soletta esclusa con N alla quota comune (tension stiffening delle barre tese), una fase non attiva; la scheda si apre su «Fasi e tensioni» con i contributi, la scheda delle tensioni, la seconda situazione scelta, l'asse neutro spento, il contorno della sezione, le rette dei limiti e la scala del CLS manuale |
| `cassoncino-torsione-pioli` | cassoncino con le verifiche a torsione: cella chiusa dal controvento superiore nelle fasi di solo acciaio, distorsione sulla campata con diaframmi a controvento a X, torcente d'appoggio con il diaframma d'appoggio; ΔT, ΔV e Δq sulle fasi; pioli su due piattabande con l'armatura trasversale, le forcine di bordo e la fatica di pioli e flangia; la scheda si apre sulle verifiche |
| `irrigidimenti-appoggio-piastra2` | H con la seconda piattabanda inferiore; irrigidimenti intermedi con piatti diversi sui due lati, pannelli diversi, compressione esterna e saldature; irrigidimento d'appoggio all'estremità sinistra con montante terminale rigido; ΔV sulle fasi; la scheda si apre sulla geometria con il prospetto all'appoggio, le sole informazioni delle armature, un divisore spostato e le proprietà della soletta esplorate con φ |
| `storico-lineare-ritiro` | storico lineare con una discretizzazione più grossolana: solo acciaio, due ritiri, una fase composta con N al baricentro efficace iterativo, una fase non attiva; la terza situazione scelta e conservata, la scheda delle fasi |
| `storico-non-lineare-curve` | storico non lineare con il CLS teso (tension stiffening) e φ = 0 richiesto negli ingressi; la scheda si apre sulla pagina delle curve con un percorso N–ε da una fase dello storico (la cattura F3 non calcola le curve) |
| `dati-incompleti` | spessore dell'anima vuoto (nessuna geometria: il disegno resta quello precedente, il calcolo si ferma) e classe del calcestruzzo fuori dal catalogo del Model: nessun risultato e nessun report |
| `legacy-viste-metodo` | archivio delle prime disposizioni della vista: quattro pagine (la terza scelta) con le loro viste e schede dei risultati, il nome precedente del metodo cumulativo, nessuna curva e nessuna proprietà della soletta: la scheda li migra all'apertura |

- Gli input vengono da `fixtures-f3` (`F3Fixtures.BridgeSectionF511`). Sono documenti come li scrive la scheda: testi per i valori
  digitati, anche con la virgola; booleani JSON per le opzioni; coefficienti della normativa da `BridgeSection.ApplyStandardDefaults`.
  Nessun identificativo finisce con `default`. Prima della cattura ogni input è stato calcolato con la vista storica in una radice
  temporanea, per verificare l'esito descritto e contenere la dimensione dei risultati dello storico (le fibre di ogni situazione
  sono nel risultato: per questo le due fixture dello storico hanno meno strisce e sottopassi dei valori predefiniti).
- Si sono copiate solo le 7 fixture nuove: il generatore produce input diversi da quelli congelati per alcune fixture più vecchie
  (muri, materiali, Bridge Design, sezione c.a.), come già in F5.4–F5.10; le due fixture della sezione composta sono uguali.
- Gli attesi (61 file: la fixture `dati-incompleti` non ha report) vengono dalla cattura `20261021-081225-f511-f3-cattura-1`, sul
  codice di produzione di `c7fe03dd`: senza attesi, le sole differenze erano i file aggiunti e il numero di `esito.txt`. La
  seconda cattura (`…-081713-f511-f3-cattura-2`) dà gli stessi file byte per byte e passa con le transizioni F4 invariate.
- Con queste fixture passano `ui/f3-equivalence`, `ui/f3-report-chapters` (112 capitoli autonomi uguali alla vista, anche per lo
  stato della vista migrato e per la pagina delle curve), `ui/f3-scheduling` e `ui/f3-figures`.
- In `expected/esito.txt` è cambiato solo il numero delle fixture (104 → 111).
- Impronte nel manifest, sezione `fixture_aggiunte_f5_11` (input, attesi ed `esito.txt`), sulla forma conservata da git (fine
  riga LF).
- Le tracce della vista storica (`tests/fixtures/f5-sezione-mista/storica`) hanno uno scenario per ognuna delle 9 fixture del modulo.
- Vista XAML (F5.11, agente f5-11-vista): con il banco `--module-impl str_mista_ponte=mvvm` la cattura dà gli stessi file della vista
  storica (confronto diretto a differenza zero). Le prove preliminari di nuovi input della sezione composta si fanno con la cattura F3
  in una radice temporanea, con la vista della Release o con il banco (commento di `F3Fixtures.BridgeSectionF511`).

## Fixture aggiunte in F5.12 (Bridge Design)

Stessa regola di F5.4 (proposta F5, sezione 7.2). Prima della preparazione di F5.12 Bridge Design (`str_bridge_design`) aveva due
fixture: il ponte predefinito (travi a I in c.a.p., cinque campate continue, Schema 1) e un archivio senza i valori avanzati
(`legacy-advanced-defaults`). Il 21/10/2026 ne ha ricevute otto (119 in tutto), catturate con la vista storica (`BridgeDesignWorkspace`
in `SheetEditor`). Gli attesi delle 111 fixture esistenti e le transizioni F4 non cambiano: le transizioni di Bridge Design (fattori
ambientali aggiunti all'apertura, valori dei risultati e stili del report) restano sulle due fixture precedenti.

| Fixture | Cosa copre |
| --- | --- |
| `cassone-acciaio-prezzario` | cassone in acciaio–cls su strada o ferrovia, pile a martello su pali da 1,5 m in terreno medio; un documento prezzi scritto dall'editor dei prezzari: catalogo proprio (nome, edizione, un prezzo cambiato, i pali da 1,5 m che comprendono la loro armatura), un'analisi composta del calcestruzzo d'impalcato con spese generali e utile, un prezzo personalizzato motivato dell'armatura, la cronologia con le tariffe archiviate |
| `strallato-ortotropo-fiume` | ponte strallato a due antenne con la piastra ortotropa su un fiume largo, sabbia densa, γQ consigliato dello Schema 1 applicato |
| `sospeso-pali-imposti` | ponte sospeso su roccia con le sottostrutture imposte: pali da 1,0 m lunghi 30 m, dodici per appoggio, calcestruzzo delle sottostrutture, altezza delle antenne e freccia dei cavi |
| `arco-traffico-uniforme` | arco con catena a campata unica semplicemente appoggiata, soletta in calcestruzzo, telaio a colonne; modello di traffico uniforme dell'archivio con il carico scritto con la virgola |
| `confronto-alternativa-a` | cassone in c.a.p. a una cella, quattro campate indipendenti che iniziano e finiscono su pila, pile circolari su plinti diretti con dimensioni imposte, calcestruzzo a ridotta impronta e acciaio riciclato, banchine da 1 m, paesaggio 7; il ponte predefinito fissato come alternativa A |
| `alternativa-a-non-calcolabile` | soletta piena con l'alternativa A fissata e la lunghezza svuotata: il confronto non è calcolabile |
| `reticolare-testi-ambiente` | ponte reticolare con valori scritti con la virgola (lunghezza, corsia, una tariffa), argilla soffice, setti su fondazioni automatiche; fattori ambientali cambiati dopo un insieme salvato: la scheda archivia il precedente all'apertura |
| `dati-incompleti` | lunghezza svuotata: nessun risultato e nessun report |

- Gli input vengono da `fixtures-f3` (`F3Fixtures.BridgeDesignF512`). Sono documenti come li scrive la scheda: valori digitati come testi,
  anche con la virgola; il documento prezzi dall'editor dei prezzari (`PriceCatalogueViewModel`, «Applica prezzario»); i documenti dei
  fattori ambientali e dei prezzi con una data fissa (15/01/2026), lontana dalle catture, perché il normalizzatore delle date del giorno
  non li tocchi. Nessun identificativo finisce con `default`.
- Si sono copiate solo le 8 fixture nuove: il generatore produce input diversi da quelli congelati per le due fixture più vecchie di
  Bridge Design, come già per altri moduli in F5.4–F5.11.
- Gli attesi (70 file: la fixture `dati-incompleti` non ha report) vengono dalla cattura `20261021-134741-f512-f3-cattura-1`, sul
  codice di produzione di `d1f0c4d6`: senza attesi, le sole differenze erano i file aggiunti e il numero di `esito.txt`. La seconda
  cattura (`…-135322-f512-f3-cattura-2`) dà gli stessi file byte per byte e passa con le transizioni F4 invariate.
- Con queste fixture passano `ui/f3-equivalence`, `ui/f3-report-chapters`, `ui/f3-scheduling` e `ui/f3-figures`.
- In `expected/esito.txt` è cambiato solo il numero delle fixture (111 → 119).
- Impronte nel manifest, sezione `fixture_aggiunte_f5_12` (input, attesi ed `esito.txt`), sulla forma conservata da git (fine riga
  LF).
- Le tracce della vista storica (`tests/fixtures/f5-bridge-design/storica`) hanno uno scenario per ognuna delle 10 fixture del modulo.
Il 27/10/2026, librerie della 1.14.2 (Model 2acc60ef, Checker 4a1b0090: correzioni decise dall'utente il 26/10 sera), l'elenco è
stato esteso senza toccare le altre voci (56949 voci, chiave ggiornamento_1142; registro AJ-LIB-01, da approvare dall'utente):
- 1939 voci BUG-PO-5b: chiave DevelopedFraction, nulla, nelle armature delle tre fixture elastiche del palo orizzontale;
- 112 voci BUG-MUR-2b (passiva fuori dal ribaltamento: numeri di gravita-cls-passiva, testi del metodo e della passiva) e 18 voci
  elencate con il valore dopo nuovo e la voce estesa con « / BUG-MUR-2b»;
- 2 voci R9: dettaglio VEd,max di 	aglio-senza-staffe-manuali.
«a» e «b» vengono dal confronto completo (	ools/qa/f3_confronto_completo.py) della cattura supporto/artefatti/lib-1142r/full-master.
