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
