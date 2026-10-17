# F5.8 — Risposta elastica del palo orizzontale e armature: preparazione

## Scopo

Questa nota chiude la **preparazione** di F5.8: la seconda scheda dei moduli `geo_palo_orizzontale` e
`geo_micropalo_orizzontale`, «Risposta elastica · trave su molle», con l'editor delle armature del palo. Oggi passa da
`X.Desktop/Wpf/ElasticPileWorkspace.cs` e dai file collegati (5 file, 764 righe all'inizio del passo):
- `ElasticPileWorkspace`, la scheda ospitata da `HorizontalWorkspace`: confronto con un archivio elastico precedente,
  intestazione (stratigrafia, opzioni, caselle del disegno), disegno della risposta con la quota selezionata, sezione alla
  quota, estremi, tabella «Valori per quota», tavola delle armature, CSV;
- `PileReinforcementEditor` (+ `Clarity`): i tratti con le loro schede, l'armatura trasversale principale, tre gruppi di
  ipotesi, i minimi, le approvazioni, la proposta, la divisione e l'unione dei tratti, il dimensionamento, il verificatore c.a.
  (una `ConcreteWorkspace` completa in un dialogo, fino a F5.10), le distinte e il pannello «Sezione e verifiche»;
- `ElasticPileDrawing` e `PileReinforcementDrawing`, i due disegni;
- il calcolo sullo sfondo con `RecalculationScheduler` e `ElasticPileAnalysisSession` (FEM, poi verifiche di sezione in
  parallelo), ritardo di 300 ms.

La nota segue i passi 1–5 della «Ricetta per i moduli successivi» di [f5-4-pilota.md](f5-4-pilota.md) e parte dalla
preparazione di F5.7 ([f5-7-palo-orizzontale.md](f5-7-palo-orizzontale.md)), di cui risolve la domanda O2 dal lato del
ViewModel. Contiene che cosa esiste, i comportamenti della vista storica trovati con le tracce e i difetti, che cosa resta e
le domande per il punto di controllo F5.8.

La vista XAML, il passaggio in `ModuleImplementations` e l'archiviazione vengono **dopo** il punto di controllo. La sezione c.a.
storica resta ospitata dal verificatore dei tratti fino a F5.10.

Riferimenti:
- [proposta](f5-proposta-architettura.md), sezioni 3.3, 5, 7 e riga F5.8 della sezione 8; rischio R7;
- [inventario](f5-inventario-ui.md), sezioni 1.3, 1.4, 2.5, 3.2 e 3.5;
- `tests/fixtures/f3/README.md`, `tests/fixtures/f5-elastico/README.md`.

Ramo `refactoring/f5-8-elastico`, da `refactoring/f5` `4b6a59de` (con la preparazione F5.7), 17/10/2026.

## 1. Che cosa esiste dopo la preparazione

| Commit | Contenuto |
| --- | --- |
| `50b0a547` | 5 fixture F3 nuove aperte sulla risposta elastica (11 in tutto, 80 nel riferimento), catturate con la vista storica; 5 tracce F5.7 della capacità aggiunte |
| `b5bf8040` | tracce congelate della vista storica (20 scenari) ed elenchi di disegno (49), suite `ui/check-palo-elastico-historic` |
| `74d926fe` | derivazioni in Application (`ElasticPilePresentation`), campi condivisi in Presentation (`ElasticPileFields`), baseline F4 3342 → 3334 |
| `8fcf13ac` | ViewModel `Modules/HorizontalPile/Elastic`, integrato nel ViewModel F5.7; due derivazioni ancora spostate (P2) |
| `7b1946c8` | prove senza WPF: `ElasticPileSheetChecks`; le fixture F3 elastiche confrontate anche su risultato, stato e report con le transizioni F4 |

### 1.1 Fixture F3

Delle 6 fixture aperte sulla risposta elastica davano un risultato solo le due `single-soil-elastic` (un solo strato, un solo
tratto collegato alla sezione principale; per il micropalo le armature non si applicano al CHS); le altre sono default
incompleti o archivi da migrare. Le 5 nuove (`F3Fixtures.HorizontalPileF58`) coprono:
- tre tratti (collegato, personalizzato con spirale, personalizzato con secondo anello interno) e un tratto con l'armatura
  trasversale «Da definire»; distinta con giunti;
- la zona sismica di testa con estensione assegnata più lunga di 10D e azioni confermate, le ipotesi approvate, le regole di
  dettaglio Trave, il copriferro esplicito, i minimi dei pali non controllati;
- testa impedita e i tre vincoli alla punta, tratto libero con eccentricità, falda, più stratigrafie con quella della risposta
  scelta, tutte le leggi della reazione orizzontale (kh, nh e k assegnati, tabelle 14.5 e 14.6, correlazione con override di nh);
- le opzioni del disegno salvate nei dati;
- due micropali (CHS del catalogo con peso dell'iniezione; tubo manuale con EJ assegnato);
- un calcolo fermato perché la stratigrafia della risposta non è scelta.

Gli attesi sono 43 file (circa 5 MB, risultati elastici completi). Le due catture sono uguali byte per byte tranne i tempi
misurati `tempi_ms` dei due `result.json` del palo con armature, chiave volatile di `tolerances.json` come per
`single-soil-elastic`. Passano `ui/f3-equivalence` (80 fixture, `transitions-f4.json` invariato) e `ui/f3-report-chapters`
(81 capitoli). In `expected/esito.txt` cambia solo il numero delle fixture, 75 → 80, registrato nel manifest
(`fixture_aggiunte_f5_8`, impronte prima e dopo, `before_sha256` di «PASS 75 fixture»).

Le tracce F5.7 della capacità hanno uno scenario per fixture dei due moduli: si sono aggiunte, catturate con la vista storica,
le 5 tracce nuove (27 scenari; le 22 esistenti invariate).

### 1.2 Tracce e disegni della vista storica

Le tracce (`tests/fixtures/f5-elastico/storica`) registrano, dopo l'apertura e dopo ogni passo, la scheda elastica come la vede
l'utente: i byte dei dati in memoria, `Modified`, la sola lettura, la scheda, l'esportabilità; risposta e risultato (impronte
senza `tempi_ms`); stato con la barra; rigidezza; confronto con l'archivio precedente o intestazione con opzioni e caselle;
quello che il disegno mostra e la quota selezionata con la sezione e il dettaglio; estremi; tabella; riepilogo per tratto;
tavola; l'editor delle armature campo per campo, comando per comando, con le distinte e il pannello «Sezione e verifiche» un
elemento per riga (testi con il tono del colore, scelte, sezione, esito, gruppi); dopo un dialogo, quello che il dialogo
mostrava. Gli identificativi casuali (tratti divisi, azioni dei fogli c.a.) sono resi stabili (`ElasticPileIds`).

Gli scenari sono 20: il percorso F3 delle 11 fixture elastiche; le opzioni della risposta e del disegno con la quota
selezionata all'interfaccia di due strati; i tratti e le ipotesi con le dieci viste del riepilogo, la divisione e l'unione; i
dialoghi (proposta, verificatore c.a. con e senza «Applica», dimensionamento senza e con le ipotesi approvate); la sola
lettura; i due comandi del confronto con l'archivio precedente; la stratigrafia da scegliere; un micropalo; il calcolo sullo
sfondo trattenuto da `HeldElasticAnalyses` (superato da una modifica, «Calcola» durante un calcolo con il FEM conservato,
passaggio alla capacità durante un calcolo, chiusura e riapertura). I dialoghi sono le finestre della vista, create dai metodi
`Create…Window` e mostrate fuori schermo. Quattro catture uguali byte per byte (anche con `ANTHEA.exe`): 28884 righe.

Gli **elenchi di disegno** (`disegni/elenco.txt`) di `ElasticPileDrawing` (opzioni della fixture, sola risposta, tutte le
opzioni, due dimensioni, una quota selezionata) e di `PileReinforcementDrawing` (alla sua dimensione e vuota) sono stati
congelati **prima** di toccare i due disegni e sono invariati dopo (49 disegni).

Produzione cambiata per le prove, senza cambiare il comportamento: l'interfaccia `IElasticPileAnalysis` (Application),
implementata da `ElasticPileAnalysisSession`; il parametro facoltativo che la porta da `SheetEditor` a `ElasticPileWorkspace`
(nullo in produzione); `partial` su `ElasticPileWorkspace`; `CreateSplitWindow`, `CreateProposalWindow` e
`CreateConcreteWindow`, che creano i dialoghi come prima (come il già esistente `CreateSegmentDesignWindow`).

### 1.3 Derivazioni in Application e campi condivisi

`src/ANTHEA.Application/Presentation/ElasticPilePresentation.cs` contiene il codice spostato senza modifiche da
`ElasticPileWorkspace`, `PileReinforcementEditor` e `PileReinforcementDrawing.Set`: testi di stato e di avanzamento, riga della
quota, estremi, righe della tabella (`ElasticPilePointRow`, stesse colonne), riepilogo per tratto, sezione e dettaglio alla
quota, testo del confronto con l'archivio precedente, caselle del disegno, Info, intestazioni dei tratti, note dei campi,
distinte, esito di un tratto e pannello «Sezione e verifiche» come modello (`ElasticPileSummary`), messaggio e pezzi della
tavola, scelta della stratigrafia, punto alla quota cliccata.

`src/ANTHEA.Presentation/Modules/HorizontalPile/Elastic/ElasticPileFields.cs` è l'unica definizione dei campi delle opzioni e dei
tre gruppi di ipotesi, dei titoli dei campi dei tratti e dei testi fissi.

La vista storica chiama queste funzioni e dà un'uscita identica: tracce, elenchi di disegno, F3, capitoli autonomi,
`ElasticPile.UiChecks` con le due varianti, `ui/f3-scheduling` (con `F3PileSegmentChecks`). I testi spostati che le tracce non
leggono (Info, etichette delle caselle, note fisse) sono stati confrontati carattere per carattere con quelli storici.

La baseline F4 scende da 3342 a 3334 occorrenze (voci 2203 → 2196), solo per spostamenti e rimozioni: le letture R4 dei file
storici passano in `ElasticPilePresentation`; otto spariscono perché lette una volta sola (sezione condivisa fra la quota e il
riepilogo, profondità e forze dell'azione critica, titolo di un gruppo di barre). Presentation resta senza voci F4 (P2); nel
commit del ViewModel tre voci passano ancora dalla vista storica ad Application (scelta della stratigrafia, punto alla quota),
totale invariato.

### 1.4 ViewModel

`ElasticPileViewModel` e `PileReinforcementViewModel` (`src/ANTHEA.Presentation/Modules/HorizontalPile/Elastic`) riproducono la
scheda storica sugli stessi dati, con le stesse scritture nello stesso ordine:

| Vista storica | ViewModel |
| --- | --- |
| costruttore: `PrepareShared`, editor delle armature (`Build`), `BuildHeader`, `InvalidateShared` senza `Modified` | costruttore nello stesso ordine, creato da `HorizontalPileSheetViewModel.Build` dove `HorizontalWorkspace` crea la seconda scheda |
| `migration` (testo e tre comandi) / `header` (stratigrafia, `options`, caselle, «Modifica dati e strati», «Info») | `Migration`, `ResolveMigration`, `MigrationComparison` / `HasHeader`, `SurveyChoices`, `SurveyChoice`, `Options` (`FormViewModel`), `Toggles`, `EditInputs`, `Info` |
| `InvalidateShared` (da ogni modifica, anche della capacità) | `InvalidateShared`: risultati azzerati, FEM conservato se la chiave non cambia, `elastico.stratigrafia = -1` con un altro numero di stratigrafie, stato, calcolo in coda, `Modified` |
| `CalculateScheduledAsync` (300 ms, `RecalculationScheduler`, `ElasticPileAnalysisSession`, risposta intermedia, fasi di MRd) | lo stesso schema con `IElasticPileAnalysis` iniettata (`ElasticPileSheet` porta `CanEdit`, `Modified`, `EditInputs`, `Migrated`, servizi e ospite); `PendingCalculation` |
| `status`, `calculationProgress`, `section` | `Status`, `ProgressVisible/Indeterminate/Value`, `Stiffness` |
| `drawing` (`Set`, `SelectDepth`, `Options`), `selection`, `sectionPreview`, `sectionDetail` | `DrawingSource`, `SelectedPoint`, `SelectDepth`, `DrawingOptions`, `Selection`, `Section` (`ElasticPileSectionView`), `SectionDetail` |
| `summary`, `table`, `csv`, `reinforcementSummary`, `reinforcementDrawing` | `Extremes`, `Points`, `CanExportCsv`/`ExportCsv` (`IFileDialogService`), `ReinforcementSummary`, `SheetResult`/`SheetMessage`/`SheetPieces` |
| `PileReinforcementEditor.Build` (schede dei tratti, trasversale principale, gruppi, minimi) | `PileReinforcementViewModel` con `Segments` (`ElasticSegmentViewModel`: fine, «Usa principale», campi, anello, tipo, comandi, distinta), `MainTransverse`, `Details`/`Seismic`/`Extra` con il loro stato aperto, `Minimum`, `Commands`, `SeismicCommands` |
| `UpdateSummary` | `Summary` (`ElasticPileSummary`), `ChooseSegment`, `ChooseMode`, `Verify`, `ShowBill` |
| dialoghi «Dividi il tratto», «Proposta di suddivisione», «Dimensionamento del tratto» | `Split`, `Propose`/`ProposalText`/`ApplyProposal`, `PileSegmentDesignViewModel` (cataloghi, `FindAsync`, `Apply`, `Close`) |
| verificatore c.a. (dialogo modale con `ConcreteWorkspace`) | `OpenConcreteVerifier`: il foglio del tratto chiesto all'ospite con `ISheetHost.OpenModuleCopy("str_palo", …)`; `PileConcreteVerifier.Apply`/`Close` scrivono nel palo come il dialogo storico (domanda E1) |
| `RevisionInspection.Protect` | `CanEdit`: campi, schede, comandi e scelte del riepilogo rifiutati; quota, schede delle analisi, «Info» e «Calcola» ammessi |

Nel ViewModel F5.7 (`HorizontalPileSheetViewModel`) la scheda elastica sostituisce la preparazione emulata:
- ogni modifica della capacità chiama `InvalidateShared` (con il suo `Modified`, come oggi due `Modified` per modifica);
- `RefreshAfterMigration`, come la vista storica dopo il confronto con l'archivio precedente o dopo l'armatura applicata alla
  sezione principale dal verificatore o dal dimensionamento;
- **O2 dal lato del ViewModel**: `ActiveResult` e la reimplementazione di `ISheetViewModel.Result`, `HasResults` e
  `CalculateAsync`, come `HorizontalWorkspace.ActiveResult` e `CalculateActiveAsync`; il report è `ReportElasticPile` per un
  risultato elastico, come `SheetEditor.BuildReportDocument`. `IsBusy` resta quello della capacità, come `SheetEditor.Busy`;
- i ganci protetti di F5.7 (`PublishedCalculation`, `CancelCalculation`) restano invariati.

`HorizontalPileSheetModule` accetta una fabbrica dell'analisi elastica (nulla in produzione). La Release resta storica:
`ModuleImplementations` non cambia.

### 1.5 Prove

`Presentation.Tests` aggiunge `ElasticPileSheetChecks` (70 controlli, senza WPF, un solo thread come il dispatcher):
- le 20 tracce uguali riga per riga a quelle della vista storica, dialoghi compresi; per il verificatore c.a. il foglio lasciato
  dall'ospite è quello della traccia storica (fino a F5.10 l'ospite è la `ConcreteWorkspace` storica, che prepara e conferma il
  foglio: vedi E1), e il ViewModel lo scrive nel palo come il dialogo;
- la finestra vede il risultato, «Calcola» e il report della scheda mostrata;
- con la barriera: solo la revisione corrente, chiusura durante il calcolo, la scheda della capacità che non annulla la risposta
  elastica; sola lettura; `Reload`; la richiesta del verificatore all'ospite e il rifiuto per il CHS; derivazioni con valori
  scritti a mano.

`HorizontalPileSheetChecks` (F5.7) confronta ora **ogni** file delle 11 fixture F3 elastiche (89 file: dati, risultato, stato,
testo e struttura del report) come li confronta `ui/f3-equivalence`, con le tolleranze e con esattamente le 1 117 transizioni F4
dei due moduli ammesse (tutte su `geo_palo_orizzontale/single-soil-elastic`): 0 differenze non ammesse, 6 tempi volatili. Le
transizioni F4 dell'elastico sono quindi identiche con il nuovo ViewModel. Le fixture della capacità restano confrontate byte
per byte. I controlli per file delle fixture elastiche sono diventati un confronto unico, quindi `Presentation.Tests` passa da
1513 controlli (dopo le fixture nuove) a 1485, con i 70 nuovi. I calcoli elastici dei fogli chiusi si attendono prima della fine
del thread delle prove (`HeldElasticAnalyses.DrainAsync`): una continuazione tardiva su un contesto concluso faceva cadere il
processo.

Stadio `ui`: `ui/check-palo-elastico-historic` (nuova), più le suite già esistenti del modulo. L'esito della corsa finale è nella
sezione 5.

## 2. Comportamenti della vista storica trovati con le tracce

Riprodotti dal ViewModel, perché la vista storica li ha; da decidere al punto di controllo.

1. **Due `Modified` per modifica della capacità** (già O4 di F5.7) e un `Modified` per ogni modifica della scheda elastica, che
   non invalida il risultato della capacità.
2. **Il calcolo elastico gira sempre**: ogni modifica dei dati comuni, anche sulla scheda della capacità, mette in coda FEM e
   verifiche di sezione; il passaggio alla scheda della capacità non lo annulla.
3. **Le schede dei tratti si ricostruiscono a ogni risultato**: l'intestazione di un tratto («T1 · 0 → 5 m») resta quella della
   costruzione fino al risultato successivo, anche dopo una modifica della fine; la vista storica salta la ricostruzione se il
   fuoco è nell'editor. Il ViewModel ricostruisce sempre: la vista nuova dovrà conservare il campo con il fuoco (E6).
4. **Gruppi chiusi alla ricostruzione**: «Ipotesi di verifica…» e «Sovrapposizioni…» tornano chiusi a ogni ricostruzione,
   «Sisma · testa del palo» aperto solo se il controllo sismico è attivo.
5. **Selezione persa**: una nuova risposta FEM toglie la quota selezionata; un risultato con la stessa risposta la conserva.

**Difetti della vista storica** (non corretti, comportamento storico invariato):
6. **Sola lettura e gruppi chiusi.** Dopo un calcolo in una revisione in sola lettura le schede si ricostruiscono; i campi dei
   gruppi chiusi non sono protetti finché il gruppo non si apre (la protezione segue i controlli visibili). Aprendolo l'utente li
   trova protetti, quindi nessuna scrittura è possibile; la traccia li legge come l'utente li trova aprendo il gruppo, e il
   ViewModel li dà protetti (E3).
7. **Stato fuorviante di un archivio da migrare.** Con il confronto con l'archivio precedente in sospeso lo stato dice «Dati
   modificati · analisi FEM e convergenza in coda» con la barra indeterminata, ma nessun calcolo è in coda; dopo un errore
   dell'importazione lo stato è il messaggio dell'errore e la barra resta indeterminata (E4).
8. **Didascalia della sezione che resta.** Quando la quota selezionata si perde, la sezione alla quota perde contorno, barre e
   staffa ma conserva didascalia, diametro della staffa e assi (E5).

## 3. Che cosa resta

L'ordine segue il passo 6 della ricetta di F5.4, dopo il punto di controllo F5.8.

1. **Vista XAML** della scheda elastica, solo token di F5.3: intestazione con `FieldForm` delle opzioni e caselle; disegno
   `ElasticPileDrawing` in code-behind sui dati del ViewModel (`DrawingSource`, `DrawingOptions`, clic → `SelectDepth`); sezione
   alla quota con `SectionDrawing`; i gruppi «Estremi…» e «Valori per quota»; la tavola `PileReinforcementDrawing` e il suo
   dialogo con zoom ed esportazione PNG; l'editor delle armature (schede dei tratti con i colori dei tratti, tre `FieldForm` nei
   gruppi, pannello «Sezione e verifiche» dal modello); i dialoghi (divisione, proposta, dimensionamento, NTC §7.2.5, «Info
   sezioni», «Info», «Mostra confronto completo»); il verificatore c.a. secondo la risposta a E1. La scheda va nella seconda
   scheda della vista F5.7.
2. **Passaggio insieme con F5.7**: il ViewModel copre ora le due schede, quindi i due moduli si possono passare insieme dopo le
   due viste (O2 di F5.7, E2).
3. **Banco e confronto diretto**: `--module-impl geo_palo_orizzontale=mvvm` e `geo_micropalo_orizzontale=mvvm`, cattura F3,
   `tools/qa/f5_confronto_diretto.py` a differenza zero sulle 21 fixture dei due moduli, elastiche comprese.
4. **Suite UI riscritte** sul ViewModel o sulla vista ospitata da `MvvmSheetEditor`, prima del passaggio della Release:
   - `ElasticPile.UiChecks` (e le varianti `anello-interno` e `nota-stress-block`): 83 chiamate per riflessione su
     `HorizontalWorkspace`, `PileReinforcementEditor`, `ConcreteWorkspace`, `PileReinforcementDrawing`;
   - `F3PileSegmentChecks` (in `ui/f3-scheduling`): la ricerca superata e la finestra chiusa durante la ricerca, da riscrivere su
     `PileSegmentDesignViewModel`;
   - il passo del modulo in `--check-contrast` e `ContrastAudit`;
   - `ui/check-palo-elastico-historic` resta finché la vista storica esiste; le tracce restano per `Presentation.Tests`.
5. **Punto di controllo dell'utente** con il materiale della sezione 4.
6. **Passaggio della Release e archiviazione (Q11)**, nello stesso commit di F5.7: `ElasticPileWorkspace`,
   `PileReinforcementEditor` (+ `Clarity`) e la traccia storica in `supporto/SUPERATI/ui-storica/palo-elastico/`; i due disegni
   restano (Q3). L'aggancio finale del verificatore alla sezione c.a. nuova dopo F5.10.
7. **Guida pratica e PDF**, se la disposizione descritta cambia.

## 4. Domande per il punto di controllo F5.8

- **E1 · Verificatore c.a. del tratto.** La vista storica apre un dialogo modale con una `ConcreteWorkspace`, che prepara e
  conferma il foglio; «Applica armatura…» porta l'armatura nel palo (nella sezione principale per il primo tratto) e la chiusura
  conserva il foglio in `foglio_cls`. Il ViewModel chiede il foglio all'ospite con `ISheetHost.OpenModuleCopy("str_palo", foglio,
  «Verificatore c.a. · T1»)`, che oggi apre una copia in un'altra finestra o un altro foglio del progetto, **senza canale di
  ritorno**; `PileConcreteVerifier.Apply`/`Close` scrivono come il dialogo storico se l'ospite li chiama. Proposta: fino a F5.10
  l'ospite desktop mostra il foglio nel dialogo storico e chiama `Apply`/`Close` (un membro nuovo di `ISheetHost` con i due
  ritorni, oppure un dialogo di `IDialogService` che ospita la `ConcreteWorkspace`); in alternativa si accetta la copia senza
  ritorno come differenza dichiarata. Quale?
- **E2 · Passaggio insieme.** Il ViewModel F5.7 con la scheda elastica copre i due moduli interi: si conferma il passaggio dei due
  moduli insieme, dopo le viste XAML delle due schede?
- **E3 · Sola lettura e gruppi chiusi** (difetto 6): il ViewModel protegge sempre i campi; la differenza non è visibile (l'utente li
  trova protetti aprendo il gruppo). Si accetta?
- **E4 · Stato di un archivio da migrare** (difetto 7): correggere nella vista nuova (stato e barra coerenti con l'assenza di
  calcolo, differenza dichiarata) o conservarlo?
- **E5 · Didascalia della sezione che resta** (difetto 8): correggere nella vista nuova o conservarla?
- **E6 · Ricostruzione delle schede dei tratti.** Il ViewModel ricostruisce le schede a ogni risultato; la vista nuova dovrà
  conservare il campo con il fuoco (o il ViewModel aggiornare le schede sul posto). Proposta: aggiornamento sul posto nella vista
  nuova, stesse scritture.
- **E7 · Calcolo elastico sempre in coda** (comportamento 2): ogni modifica dei dati comuni costa FEM e verifiche di sezione anche
  sulla scheda della capacità. Si conserva fino a F5.13?
- **E8 · Reimplementazione di `ISheetViewModel`.** Il ViewModel F5.7 reimplementa `Result`, `HasResults` e `CalculateAsync` per la
  scheda mostrata, senza toccare `SheetViewModelBase`; in alternativa membri virtuali nella base. Si conferma?
- **E9 · Riferimento F3.** Come D2 di F5.4 e O8 di F5.7: cambia solo il numero delle fixture in `expected/esito.txt` (75 → 80),
  registrato nel manifest; gli attesi nuovi pesano circa 5 MB e le tracce circa 3,9 MB.
- **E10 · Elenchi di disegno.** Come O10 di F5.7: dipendono dai font installati; la cattura con `ANTHEA.exe` e con `dotnet
  ANTHEA.dll` è uguale sulla macchina del banco (schermo al 150%), da ripetere a 96 DPI.

**Materiale da mostrare** (dopo i passi 1–3 della sezione 3): schermate fuori schermo nei tre aspetti, `--check-contrast`, confronto
diretto a zero, `ui/f3-equivalence` con i due moduli MVVM, conteggio delle suite `ui`, questa nota con le risposte.

## 5. Corsa finale

`-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` su `837026cb`: `supporto/artefatti/ci/20261017-035316-f58-finale`,
PASS 116 (build 25, fast 31, wiki 2, ui 58), KNOWN 0, NEW-FAIL 0, nessun file tracciato modificato, 26 minuti.
- `ui/f3-equivalence` PASS con 80 fixture e `transitions-f4.json` invariato; `ui/f3-report-chapters` PASS con 81 capitoli;
- `ui/check-palo-elastico-historic` (nuova) 20 scenari, 28884 righe, 49 disegni uguali; `ui/check-palo-orizzontale-historic` 27
  scenari; `ui/f3-scheduling` (con `F3PileSegmentChecks`), `ui/smoke-horizontal`, `ElasticPile.UiChecks` e le due varianti PASS;
- `Presentation.Tests` PASS 1485, `Architecture.Tests` PASS (baseline F4 3334).

`ui/check-contrast` è escluso: lo schermo è al 150% (144 DPI) e il controllo non può confrontarsi con la sua baseline a 96 DPI
(decisione D6); il coordinatore lo ripete a 96 DPI. Nessuna vista nuova è entrata nel controllo del contrasto.
