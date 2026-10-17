# F5.4 — Pilota palificata orizzontale: preparazione e ricetta

## Scopo

Questa nota chiude la **preparazione** del pilota F5.4: modulo `geo_efficienza_orizzontale`, palificata orizzontale.
Contiene:
- che cosa esiste dopo la preparazione;
- la ricetta per finire F5.4, che diventa la regola dei moduli successivi;
- i punti dove pesa la domanda Q8;
- il materiale per il punto di controllo dell'utente.

La vista XAML, il passaggio in `ModuleImplementations` e il ritiro della vista storica vengono **dopo** il punto di
controllo F5.3 (token dei temi): non fanno parte di questa preparazione.

Riferimenti:
- [proposta](f5-proposta-architettura.md), sezioni 3.1, 3.2, 5, 7.1, 7.2 e riga F5.4 della sezione 8;
- [inventario](f5-inventario-ui.md), sezione 4;
- `tests/fixtures/f3/README.md` e `tests/fixtures/f5-palificata/README.md`.

Ramo `refactoring/f5-4-pilota`, da `refactoring/f5` `5f29067b`, 16/10/2026. Si lavora sulla raccomandazione Q8 (a):
il calcolo breve resta sincrono sul thread dell'interfaccia, come oggi.

**Aggiornamento del 16/10, ramo `refactoring/f5-4-vista`.** Dopo l'unione di F5.3 sono stati eseguiti i passi 1–4 della
sezione 2: vista XAML, banco, confronto diretto e `HorizontalPileGroup.Checks`. La sezione 4 contiene gli esiti e il materiale
per il punto di controllo; i passi 5–7 vengono dopo.

**Chiusura del 17/10, ramo `refactoring/f5-4-chiusura`.** Punto di controllo accettato dall'utente; passi 6 e 7 eseguiti:
la Release apre la palificata con la vista MVVM e la vista storica è archiviata. Esiti nella sezione 5.

## 1. Che cosa esiste dopo la preparazione

| Commit | Contenuto |
| --- | --- |
| `f8ac25d3` | 6 fixture F3 nuove della palificata (8 in tutto, 42 nel riferimento), attese catturate con la vista storica |
| `b71a5221` | tracce congelate della vista storica: 11 scenari, suite `ui/check-pile-group-historic` |
| `ceb344d0` | derivazioni di presentazione in Application (`PileGroupPresentation`), baseline F4 3353 → 3351 |
| `a13c58bb` | modalità sincrona di `SheetCalculation` e `SheetViewModelBase` (Q8 a) |
| `b90d7bc0` | ViewModel `src/ANTHEA.Presentation/Modules/PileGroup` e prove senza WPF |

### 1.1 Fixture F3

Le 6 fixture nuove coprono:
- tutte le disposizioni;
- tutte le direzioni di carico;
- i due basamenti e un basamento senza contorno;
- chiavi opzionali assenti;
- estrapolazioni e file proiettate con interassi per direzione;
- avvisi;
- un calcolo fermato con «Dati da completare».

L'elenco è in `tests/fixtures/f3/README.md`. Gli attesi delle 36 fixture esistenti e `transitions-f4.json` non cambiano.
`ui/f3-equivalence` e `ui/f3-report-chapters` passano con 42 fixture.

### 1.2 Tracce della vista storica

Il riferimento F3 confronta i dati in forma canonica, con le chiavi ordinate, e non vede la scheda. Le tracce
(`tests/fixtures/f5-palificata/storica`) registrano, dopo l'apertura e dopo ogni passo:
- i byte dei dati nell'ordine in memoria;
- `Modified`;
- il risultato;
- testi, tabelle e campi;
- il disegno.

Sono 11 scenari:
- le 8 fixture sul percorso della cattura F3;
- le modifiche di ogni tipo, sul default e sull'archivio generico;
- la sola lettura.

La stessa definizione (`tests/Shared/PileGroupTrace.cs`) guida due prove:
- la vista storica, in `tests/Desktop/PileGroupHistoricTrace.cs`, stadio `ui`, finché la vista esiste;
- il ViewModel, in `tests/ANTHEA.Presentation.Tests/PileGroupSheetChecks.cs`, stadio `fast`.

Le tracce sono la prova di equivalenza che resta dopo l'archiviazione della vista storica.

### 1.3 Derivazioni in Application

`src/ANTHEA.Application/Presentation/PileGroupPresentation.cs` contiene il codice che la vista calcolava da sé, spostato
senza modifiche:
- `CapDistances`: distanza asse–bordo `bordo_d × D` e margine libero `(bordo_d − 0,5) × D`;
- `Reduction`: riduzione del palo `(1 − fattore) × 100`;
- `Applicability`: regola del testo dell'applicabilità (errore del metodo, «Approssimazione esplicita», «Disponibile»);
- letture di metodo, angolo, fila e disponibilità;
- riga selezionata;
- grandezza ridotta (Rg o Pm);
- β e α per metodo;
- formato dei numeri.

La vista storica chiama queste funzioni e dà un'uscita identica: tracce, F3 e capitoli autonomi lo confermano.

La baseline F4 cambia solo per spostamenti e rimozioni:
- 7 voci della vista tolte (8 occorrenze);
- 6 voci spostate in Application.

### 1.4 Calcolo sincrono (Q8 a)

`SheetViewModelBase` riceve `SheetCalculationMode.Synchronous`:
- «Calcola» torna con il risultato pubblicato;
- il ricalcolo automatico parte dopo il ritardo del modulo, sul thread dell'interfaccia.

Il modulo prepara i dati e legge il risultato in `CalculateSynchronously`, come `CalculateCurrent` della vista storica.
Altri punti nuovi:
- `CalculateModule`: servizio del contesto con la cache del foglio;
- `OnCalculationPublished`;
- `ChangedKeepingResults`: stato dell'interfaccia salvato che non invalida i risultati.

Sotto c'è `SheetCalculation.CalculateNow/ScheduleNowAsync`, con gli stessi stati e la stessa regola dell'ultima richiesta.
Non usa copie, thread di sfondo né limitatore. La modalità sullo sfondo non cambia.

### 1.5 ViewModel

`PileGroupSheetViewModel` (`src/ANTHEA.Presentation/Modules/PileGroup`) riproduce `HorizontalPileGroupWorkspace`:

| Vista storica | ViewModel |
| --- | --- |
| costruttore: `Upgrade`, campi, `LoadPiles`, `BuildSpacing`, `TryCalculate` | costruttore: `Build`, poi `CalculateNow` |
| `Field(...)` con `Value`/`optional` | `FormViewModel` compatto + `FieldSpec` (`NumberOrText`, `NumberOrTextRemoveEmpty`) |
| `Option(...)` | `FieldSpec(Bool: true)`; «custom» abilita l'angolo (`Enable`) |
| combo della disposizione, `BuildGenerator` | `SelectedLayout`, `GeneratorForm`, `GeneratorNote`, `RotationForm` (la rotazione dopo la nota degli anelli) |
| direzioni in due righe (+X, +Y / −X, −Y), direzione personalizzata e angolo | `XDirectionsForm` e `YDirectionsForm` (due colonne), `CustomDirectionForm` |
| opzioni con la loro nota | `ExtrapolationsForm`, `ProjectedRowsForm` (una nota sotto ciascuna) |
| `PileRow`, `piles`, `CapturePiles`, `LoadPiles` | `PileRowViewModel`, `Piles`, stesse regole |
| `CellEditEnding` annullato | `CoordinateEditCancelled()` (chiamato dalla vista) |
| combo del basamento, campo `bordo_d` | `SelectedCapShape`, `CapForm` |
| combo degli interassi, `spacingDirection` | `SpacingDirectionIndex`, `SpacingDirection` (non salvato), `SpacingForm` |
| `Queue`, `DelayedCalculation` (180 ms) | `Queue` → `Changed` (ritardo `RecalculationDelay`, `ScheduleNowAsync`) |
| `CalculateCurrent` | `CalculateSynchronously` + `OnCalculationPublished` |
| `ComparisonRow`, griglia del confronto | `ComparisonRowViewModel`, `Comparison`, `SelectedComparison` |
| righe anonime dei dettagli | `PileDetailRowViewModel`, `Details` |
| testi e indicatori, `Clear`, `Present` | `Title`, `State`, `Minimum`…`Notes`, stessi `Clear` e `Present` |
| `plan.Set(data, selected, cap)` | `Plan` (`PileGroupPlanInput`) |
| `Commit`, `Dispose` | `OnCommit`, `OnDisposed` |
| `RevisionInspection.Protect` (sola lettura) | `IsReadOnly`, `CanEdit`, `CanEditCoordinates`, `FormViewModel.IsReadOnly` dei form; selezione e calcolo sempre ammessi |
| `WikiContextHelp.Label` («?»), F1 di `SheetEditor` | `FormViewModel.WikiNavigator` (servizi del contesto); F1 fuori dai campi in `MvvmSheetEditor` |

I form per blocco visivo (rotazione, direzioni, opzioni) e la forma compatta sono stati introdotti con la vista XAML
(commit `1f3899d6`), per conservare la disposizione di oggi: `FieldForm` mostra un form intero, in colonna. `Forms` e
`Field(chiave)` elencano i form e trovano un campo per le prove.

Il modulo `PileGroupSheetModule` dichiara le chiavi di stato `metodo`, `direzione_selezionata` e `angolo`. È registrato
in `DesktopComposition.CreateModules`. La Release resta storica: `ModuleImplementations` non cambia.

Restano espliciti nel ViewModel, perché i componenti di F5.2 non li esprimono:
- **La tabella delle coordinate.** `JsonRowViewModel` scrive il testo grezzo cella per cella. La vista storica, invece,
  tiene righe di testo e scrive l'intero elenco normalizzato, solo con «Generica» e solo se cambia.
- **La sola lettura dei campi** (fino a `c21466e8`). `FormViewModel` non aveva uno stato di sola lettura e il ViewModel
  ricostruiva i form su copie staccate. Ora i form hanno `IsReadOnly` (domanda D4) e il ViewModel lo imposta.

### 1.6 Prove

- `Presentation.Tests` (461 controlli; 120 della palificata, 23 del calcolo sincrono):
  - 11 tracce uguali riga per riga;
  - 8 fixture uguali al riferimento F3: aperto, visitato, calcolato, risultato, testo e struttura del report, esportato,
    stato, riaperto;
  - chiavi di stato;
  - sola lettura;
  - `Reload`;
  - chiusura durante il ritardo;
  - servizio di calcolo iniettato e thread.
- `Application.Tests`: `PileGroupPresentationChecks`, con valori scritti a mano.
- Stadio `ui`: `ui/check-pile-group-historic`, `ui/f3-equivalence`, `ui/f3-report-chapters`,
  `ui/check-module-host-offscreen`.

## 2. Ricetta per finire F5.4

L'ordine è vincolante: ogni passo parte con le prove del precedente verdi.

Stato al 16/10: passi 1–4 fatti (sezione 4.1); passo 5 in corso; passi 6 e 7 dopo l'ok dell'utente. Stato al 17/10: tutti i
passi fatti (sezione 5).

1. **Vista XAML**, dopo l'unione di F5.3: `X.Desktop/Views/PileGroup/PileGroupSheetView.xaml` più un DataTemplate per
   `PileGroupSheetViewModel`.
   - **Risorse.** Solo i token di F5.3 (regola P7: niente esadecimali, `Brushes.*`, `Color.From*`). Il punto in cui si
     registrano i DataTemplate lo stabilisce F5.3, perché `App.xaml` e `Views/Common` sono la sua area.
   - **Disposizione.** Si conserva quella di oggi: colonna di ingresso di 355 px e risultati a destra. La guida pratica
     (`supporto/docs/guida-pratica-anthea.md`, riga 809) la descrive.
     - Campi con `FieldForm`.
     - Gli espansori «Basamento e distanze dai bordi» (aperto) e «Opzioni di calcolo e applicabilità» restano `Expander`:
       così la visita di `ui/f3-equivalence` non cambia.
     - Le tre griglie hanno le stesse colonne e gli stessi binding, con `UpdateSourceTrigger=PropertyChanged` sulle celle.
     - I testi fissi sotto i campi si copiano dalla vista storica.
     - Le etichette delle direzioni non hanno più gli spazi di allineamento: la disposizione la fa il layout.
   - **Disegno.** `PileGroupPlan` resta invariato. Il code-behind chiama `plan.Set(Plan.Data, Plan.Selected, Plan.Cap)` a
     ogni cambio di `Plan`.
   - **Code-behind ammesso** (Q3), solo per:
     - il disegno;
     - `CellEditEnding` annullato → `CoordinateEditCancelled()`, accodato al dispatcher con priorità Background come oggi.
   - **Sola lettura.**
     - Combo, caselle e griglie legano `IsEnabled`/`IsReadOnly` a `CanEdit` e `CanEditCoordinates`.
     - La griglia del confronto resta selezionabile.
     - Per i campi `FieldForm` serve uno stato di sola lettura del form (domanda D4).
   - **Impegno delle celle.** Con `UpdateSourceTrigger=PropertyChanged` il testo raggiunge il ViewModel a ogni tasto,
     quindi a `Commit` resta solo la modalità di modifica della griglia. Va verificato con la cattura del passo 2 (file
     `visited` e `calculated-data`).
   - **Prove della vista** (`tests/ANTHEA.Desktop.UiTests`, proposta 7.3), fuori schermo:
     - schermate nei tre aspetti;
     - `--check-contrast` con 0 difetti in Scuro e Molto scuro;
     - nessuna scrittura nel documento dovuta alla sola visita;
     - F1 e «?» dei campi;
     - `AutomationProperties.Name` uguali a quelli di oggi dove le prove li usano.
2. **Passaggio nel banco.** Cattura F3 con il modulo MVVM, solo nella build UiTests:

   ```
   dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-f3-equivalence <radice> <uscita> --module-impl geo_efficienza_orizzontale=mvvm
   ```

   `F3ReportChapterChecks` apre i fogli con lo stesso banco, che legge `--module-impl` da tutta la riga di comando. Oggi però
   `AppHarness` accetta `--check-f3-report-chapters` solo con 3 argomenti: va aggiunto lo stesso `WithoutBench` usato per
   `--check-f3-equivalence` (file condiviso: si aggiunge solo quella riga).
3. **Confronto diretto** fra la cattura storica e quella MVVM dello stesso codice, a differenza zero:

   ```
   python -I tools/qa/f5_confronto_diretto.py --a <cattura storica> --b <cattura mvvm> --out <confronto-diretto.json>
   ```

   Poi il confronto normale con `expected` e `transitions-f4.json`: per la palificata non ci sono transizioni. Ogni
   differenza va in `transitions-f5.json` con l'ok dell'utente (Q15).
4. **`HorizontalPileGroup.Checks` riscritto.** Le parti su motore, adattatore e archivio (`Program.cs:26-76`) non
   cambiano. La parte UI (`77-88`) oggi usa per riflessione il costruttore, `Result` e il campo `comparison` della vista
   storica. Diventa:
   - la vista XAML ospitata da `MvvmSheetEditor` fuori schermo (PNG 1400×1050);
   - la selezione tramite `SelectedComparison`.

   La suite passa in `tests/ANTHEA.Desktop.UiTests`, come previsto da `progetti-di-test.md` («UI archiviata dopo F5.4»).
   Il conteggio delle suite `ui` si riporta.

   Fatto diversamente (D13): la parte UI è nella suite `ui/check-pile-group-view` dell'harness UiTests, che raggiunge
   `MvvmSheetEditor` senza riflessione; `tests/ANTHEA.Desktop.UiTests` non è stato creato.
5. **Punto di controllo dell'utente** con il materiale della sezione 4.
6. **Passaggio della Release e ritiro della vista storica (Q11)**, nello stesso commit, dopo l'ok:
   - `ModuleImplementations.Declared`: `("geo_efficienza_orizzontale", SheetImplementation.Mvvm)`, e
     `ModuleHostWpfChecks` aggiornato (la Release non è più tutta storica).
   - `X.Desktop/Wpf/HorizontalPileGroupWorkspace.cs` passa in `supporto/SUPERATI/ui-storica/palificata/`, non compilato,
     con origine, motivo, sostituto e impronta. `tests/Desktop/PileGroupHistoricTrace.cs` e la suite
     `ui/check-pile-group-historic` lo seguono.
   - Le tracce congelate restano: `Presentation.Tests` continua a usarle.
   - Si tolgono i rami `pileGroup` di `SheetEditor`: campo 29, `Result` 35, costruttore 105, `Dispose` 165, `Commit` 166,
     `CalculateAsync` 257, `BuildReportDocument` 380.
   - In `MainWindow.ExportReport` (righe 440-447) il nome proposto resta `Relazione_palificata_orizzontale.docx`, perché
     il ramo dipende dal modulo e non dalla vista. Resta anche l'adattamento `DisplayAdaptation.Viewport(…, 1120, 600)`
     della riga 253.
   - `PileGroupPlan` resta con le sue 37 voci F4 (disegno).
   - Contrasto: il passo `modulo-geo_efficienza_orizzontale` mostra la vista nuova; le 4 voci della Chiara della baseline
     passano a quel passo con la chiave della vista nuova (D12), il passo `mvvm-geo_efficienza_orizzontale` si toglie.
   - `ui/check-pile-group-view` confronta ancora le tracce attraverso la vista; le parti che aprono la vista storica
     (esportazioni, schermate, aiuto, nomi, sola lettura) passano al confronto con i valori attesi.
   - Le viste storiche e le partial di prova devono compilare senza la classe archiviata.
7. **Guida pratica e PDF**, se la disposizione descritta cambia (regola di `AGENTS.md`).

### Ricetta per i moduli successivi (F5.5–F5.12)

1. Fixture F3 aggiuntive catturate con la vista storica dove la copertura è scarsa:
   - input dal generatore `fixtures-f3`;
   - due catture uguali;
   - sezione del manifest;
   - README;
   - `ui/f3-report-chapters` verde. Una differenza fra la vista e il capitolo autonomo va spiegata, non nascosta.
2. Tracce della vista storica:
   - scenari condivisi in `tests/Shared`;
   - comando UiTests `--check-<modulo>-historic[-capture]`;
   - riferimento in `tests/fixtures/f5-<modulo>/storica`;
   - suite `ui` fino all'archiviazione.

   Le tracce coprono quello che F3 non vede: ordine delle scritture, `Modified`, testi, tabelle, campi, sola lettura e
   ricalcoli automatici.
3. Derivazioni rimaste nella vista spostate in Application, con la vista storica che le chiama. Uscita identica,
   provata da tracce e F3. Baseline F4 solo per spostamenti e rimozioni, motivati nel commit.
4. ViewModel in `src/ANTHEA.Presentation/Modules/<Modulo>` su `SheetViewModelBase`:
   - modalità `Background`, oppure `Synchronous` se la vista storica calcola sul thread dell'interfaccia e l'utente lo
     conferma;
   - `FormViewModel`/`FieldSpec` dove le regole di scrittura coincidono, il resto esplicito e documentato;
   - `ModulePersistence`;
   - `ISheetModule` registrato, Release storica.
5. Prove senza WPF:
   - tracce uguali alla vista storica;
   - fixture F3 uguali al riferimento;
   - chiavi di stato, sola lettura, `Reload`, chiusura durante il calcolo o il ritardo;
   - servizio di calcolo iniettato, con barriere per i moduli sullo sfondo.
6. Vista XAML, banco, confronto diretto, suite UI riscritte, punto di controllo, passaggio della Release e archiviazione
   (passi 1–7 della ricetta di F5.4).

## 3. Dove conta Q8

Q8 è la domanda sul calcolo della palificata: sincrono (a) o sullo sfondo (b). Con (a) i punti seguenti valgono come oggi:
- **Riferimento F3.** La cattura salva `opened.json` subito dopo `Commit`. Il calcolo sincrono all'apertura ha già
  scritto `direzione_selezionata` e `angolo` (`ApplyPileGroupSelection`). Con (b) quei file cambierebbero, e servirebbero
  voci in `transitions-f5.json` con l'ok dell'utente.
- **`CalculateAsync`.** Torna con il risultato, sia in `SheetEditor` sia in `MvvmSheetEditor`. La cattura F3,
  `ui/f3-report-chapters` e `HorizontalPileGroup.Checks` leggono `Result` subito dopo.
- **`Commit`.** Ricalcola subito se c'è un ricalcolo in attesa o non c'è risultato. `MainWindow.ExportReport` (riga 439)
  fa `Commit()` e poi controlla `HasResults` e `Busy`: con (a) il report si esporta anche subito dopo una modifica; con
  (b) l'utente vedrebbe «Completare il calcolo prima di esportare il report».
- **`Busy`/`IsBusy`.** Sono veri solo durante l'esecuzione sincrona, quindi mai osservabili dall'esterno.
- **Revisioni in sola lettura.** Il calcolo all'apertura scrive nella copia di lavoro prima della protezione, come oggi.
- **Selezione nel confronto.** Agisce sul risultato pubblicato e ha senso solo quando c'è un risultato. Con (b) andrebbe
  disabilitata durante il calcolo.
- **Più fogli (F5.13).** Il calcolo breve blocca il thread dell'interfaccia per la sua durata e non passa dal limitatore
  dei calcoli. Il passaggio allo sfondo si potrebbe valutare in F5.13 come differenza dichiarata.
- **Moduli successivi.** Fondazioni (F5.9b) e acciaio (F5.5) calcolano anch'essi in modo sincrono (F3.6b). La modalità
  `Synchronous` serve anche a loro, se l'utente conferma (a) ai rispettivi punti di controllo.

Se l'utente scegliesse (b), il ViewModel passa a `SheetCalculationMode.Background`:
- la preparazione (`CapturePiles`, `Regenerate`) va prima della copia;
- `ApplyPileGroupSelection` e le righe del confronto vanno alla pubblicazione;
- le differenze di F3 diventano voci di `transitions-f5.json`;
- servono prove con barriera al posto di quelle sincrone.

## 4. Materiale per il punto di controllo F5.4

Passi 1–4 della sezione 2 eseguiti sul ramo `refactoring/f5-4-vista` (da `refactoring/f5` `1626e776`, 16/10/2026), con la
raccomandazione Q8 (a). La Release resta storica: `ModuleImplementations` non cambia e la vista storica non è archiviata.

### 4.1 Esiti

| Commit | Contenuto |
| --- | --- |
| `c21466e8` | `FormViewModel.IsReadOnly` e `WikiNavigator`, legati in `FieldForm` (D4); parità FieldForm/InputForm estesa alla sola lettura |
| `1f3899d6` | vista XAML `X.Desktop/Views/PileGroup`, DataTemplate in `Views/PileGroup/PileGroupTemplates.xaml` unito da App.xaml; ViewModel con form per blocco visivo; F1 della guida del modulo in `MvvmSheetEditor` |
| `1332b7cb` | banco anche per `--check-f3-report-chapters`; suite `ui/check-pile-group-view`; parte UI di `HorizontalPileGroup.Checks` spostata lì |
| `c6859882` | passo `mvvm-geo_efficienza_orizzontale` di `--check-contrast` |
| `1b28324f` | banco interattivo per le prove a mano: `ANTHEA.exe --interactive --module-impl geo_efficienza_orizzontale=mvvm` (solo UiTests) |

| Criterio | Esito |
| --- | --- |
| Vista XAML con la disposizione di oggi, solo token (P7) | sì; `Architecture.Tests` PASS 507, P6 e P7 attive su `Views/PileGroup`, baseline F4 senza aggiunte, P3 130 invariata |
| Tracce della vista storica (11 scenari) | uguali riga per riga sia dal ViewModel (`Presentation.Tests`, PASS 470) sia **attraverso i controlli della vista XAML** (`ui/check-pile-group-view`) |
| Cattura F3 con il modulo MVVM contro `expected` + `transitions-f4.json` | PASS 42 fixture, 514 file, 0 differenze non ammesse; transizioni F4 invariate (nessuna per la palificata); nessuna voce nuova |
| Cattura F3 storica dello stesso codice | PASS 42 fixture, 0 differenze non ammesse |
| Confronto diretto storica/MVVM (`tools/qa/f5_confronto_diretto.py`) | **identiche**: 514 file, 513 uguali byte per byte, 1 JSON uguale in forma canonica (un tempo misurato del palo elastico, chiave volatile); 0 differenze, quindi anche sulle 8 fixture della palificata |
| `ui/f3-report-chapters` con il modulo MVVM | PASS 43 capitoli autonomi, 89 immagini |
| Esportazioni | per le 8 fixture: JSON del risultato uguale byte per byte, report Word uguale in ogni parte e PDF affiancato uguale (date, identificativi e sigle dei sottoinsiemi dei font normalizzati); nome proposto invariato (`MainWindow.ExportReport` sceglie per modulo) |
| `HorizontalPileGroup.Checks` | motore, adattatore, archivio e report: PASS 92; la parte UI è sulla vista XAML in `ui/check-pile-group-view` (PNG 1400×1050, «UI calculated», selezione con `SelectedComparison`) |
| Visita senza scritture | per le 8 fixture: espansori aperti e tre aspetti, senza scritture e senza `Modified` |
| F1 e «?» | dal navigatore Wiki dei servizi, anche in sola lettura |
| `AutomationProperties.Name` | quelli della vista storica (l'unico è «Wiki: Diametro comune D [m]») conservati; nessuna prova del runner usa nomi della palificata |
| Contrasto | Scuro 0, Molto scuro 0; Chiara 4 difetti, gli stessi della vista storica (sezione 4.3) |
| Parità FieldForm/InputForm | PASS 2072 controlli (2025 prima), con la sola lettura |
| Profilo quick | PASS 52 |
| Corsa `-Stage build,fast,wiki,ui` | PASS 112, KNOWN 0, NEW-FAIL 0 (build 25, fast 31, wiki 2, ui 54) su `1b28324f`, `supporto/artefatti/ci/20261016-132935-f54v-finale`; nessun file tracciato modificato; `ui/f3-equivalence` con la vista storica PASS 42 fixture e lo stesso `transitions-f4.json` |

**Suite dello stadio ui.** Sono 54: 53 sulla base `1626e776` (51 del pilota e 2 di F5.3) più una. Una sola suite nuova, `ui/check-pile-group-view`; nessuna tolta:
`HorizontalPileGroup.Checks` resta nello stadio ui senza la parte WPF, `ui/check-pile-group-historic` resta finché la vista
storica esiste. Non è stato creato `tests/ANTHEA.Desktop.UiTests`: l'harness UiTests raggiunge `MvvmSheetEditor` (internal)
senza riflessione, mentre un progetto separato avrebbe richiesto riflessione o `InternalsVisibleTo` (D13).

### 4.2 Schermate

Cartella non versionata `supporto/artefatti/refactoring/f5/f5-4-vista/vista/schermate/` del worktree
`C:\Users\g.pacini\AppData\Local\Temp\aw-f5-4-vista`. Le produce `ui/check-pile-group-view` a ogni corsa.

| Dati | Chiara | Scuro | Molto scuro |
| --- | --- | --- | --- |
| default, vista nuova | `vista-default-chiara.png` | `vista-default-scuro.png` | `vista-default-molto-scuro.png` |
| default, vista storica | `storica-default-chiara.png` | `storica-default-scuro.png` | `storica-default-molto-scuro.png` |
| quinconce con quattro direzioni, vista nuova | `vista-quinconce-direzioni-chiara.png` | `vista-quinconce-direzioni-scuro.png` | `vista-quinconce-direzioni-molto-scuro.png` |
| quinconce con quattro direzioni, vista storica | `storica-quinconce-direzioni-chiara.png` | `storica-quinconce-direzioni-scuro.png` | `storica-quinconce-direzioni-molto-scuro.png` |

Gli espansori sono aperti. Nella cartella `vista/` c'è anche `ui-4.png`, la schermata 1400×1050 di
`HorizontalPileGroup.Checks`. Le catture del contrasto sono in `contrasto-mvvm-tre-aspetti/` (con la Chiara) e
`contrasto-mvvm-baseline/` (come il runner).

Differenze visibili fra le due viste, le stesse in tutti gli aspetti:
- **Campi.** `FieldForm` compatto: etichette di 12 punti come oggi; editor di 82 px invece di 94, con la colonna dell'unità
  (42 px) vuota a destra, perché le etichette della palificata contengono già l'unità. È la disposizione di `InputForm` negli
  altri moduli.
- **Direzioni.** Due colonne (+X/−X, +Y/−Y) invece di due righe allineate con spazi; etichette senza spazi (D8).
- **Titolo.** `Font.Title` (22 punti) invece di 20: accorpamento Q6 (c). Le altre dimensioni (22, 16, 12, 11) hanno un token
  uguale; la 14 non è usata.
- **Bordo delle schede.** `#D8E0EB` di `Ui.Paper` diventa `Token.Border` (`#DCE2E9`, ΔE 2,3) in Chiara; negli aspetti scuri i
  due colori coincidono.

### 4.3 Contrasto

`--check-contrast` ha il passo nuovo `mvvm-geo_efficienza_orizzontale`: la stessa esplorazione dei passi dei moduli, in una
finestra principale che apre la palificata MVVM.
- Con `--baseline` (runner) il passo misura solo Scuro e Molto scuro. Finché la Release apre la vista storica, la Chiara del
  modulo è quella storica, i cui difetti sono già nella baseline. La Chiara della vista MVVM è dichiarata fra le finestre non
  coperte.
- Senza `--baseline` il passo misura anche la Chiara.

| Corsa (ANTHEA.exe, 96 DPI) | Catture | Chiara | Scuro | Molto scuro |
| --- | --- | --- | --- | --- |
| `contrasto-mvvm-tre-aspetti` (`--viste ^mvvm-`) | 15 | 4: etichette MINIMO, MEDIA, MASSIMO e RIDUZIONE MEDIA, `#64748B` su `#F3F5F8`, 4,36:1 | 0 | 0 |
| `contrasto-mvvm-baseline` (`--viste ^mvvm- --baseline`) | 10 | non misurata | 0 | 0 |
| runner, `ui/check-contrast` (corsa finale) | 887, di cui 10 del passo MVVM | 699 in 187 voci, uguali alla baseline; Chiara della vista MVVM non misurata | 0 | 0 |

I 4 difetti della Chiara sono gli stessi della vista storica (voce `modulo-geo_efficienza_orizzontale` della baseline, 4
occorrenze): stessi colori, stessi elementi.

### 4.4 Domande D1–D8 con quello che si sa ora, e domande nuove

- **D1 · Q8 (a).** Calcolo breve sincrono e scritture della selezione nel documento come oggi (sezione 3). Confermata dai
  fatti: con il calcolo sincrono la cattura F3 MVVM è uguale al riferimento senza transizioni e identica alla cattura
  storica. Resta la conferma dell'utente.
- **D2 · `expected/esito.txt` 36 → 42.** Con l'aggiunta delle fixture cambia solo il numero; la modifica è registrata nel
  manifest (`fixture_aggiunte_f5_4`, impronte prima e dopo). L'alternativa era una voce in `transitions-f5.json`. Invariata,
  da confermare.
- **D3 · Interassi mancanti.** Se un archivio non ha l'oggetto degli interassi della direzione mostrata all'apertura, la vista
  lo crea vuoto (senza `Modified`), mentre la preparazione del capitolo autonomo no: dati preparati diversi, report uguale.
  Nessuna fixture lo mostra. Il ViewModel e la vista nuova riproducono la vista storica. Invariata: da decidere se allineare
  `ModuleSheetPreparation.PreparePileGroup` (modifica dell'Application, non dei report).
- **D4 · Sola lettura dei form.** Fatto.
  - `FormViewModel.IsReadOnly` rifiuta digitazione, scelta e spunta.
  - `FieldForm` mette in sola lettura le caselle e disattiva scelte e spunte. Il fondo cambia solo per i campi calcolati e le
    «?» restano utilizzabili.
  - La parità con `InputForm` sotto `RevisionInspection` è provata.
  - Il ViewModel della palificata non usa più copie staccate. Nella vista nuova i controlli bloccati sono gli stessi della
    storica.

  Da confermare come regola per tutti i moduli.
- **D5 · Modalità sincrona.** `SheetCalculationMode.Synchronous` nel componente comune, come regola per i moduli che oggi
  calcolano sul thread dell'interfaccia. Invariata, da confermare.
- **D6 · `Reload`.** Il foglio si ricostruisce come all'apertura (migrazioni comprese) e ricalcola dopo il ritardo, senza
  `Modified`; la direzione degli interassi mostrata resta quella scelta. Invariata, da confermare.
- **D7 · `Calcola` da `MvvmSheetEditor`.** Prima conferma le bozze (`Commit`), mentre la palificata storica calcola senza
  `Commit`. `Commit` ricalcola solo se un ricalcolo è in attesa, e ricalcolare gli stessi dati non cambia nulla. Verificato:
  catture F3 e tracce identiche. Da confermare.
- **D8 · Etichette delle direzioni.** Senza gli spazi di allineamento di oggi («+X · 0°     »); le due righe sono rese con due
  colonne.
- **D9 · Dimensione del titolo.** 20 → 22 punti (`Font.Title`), unico accorpamento tipografico della vista. L'alternativa è un
  token da 20, che oggi non esiste.
- **D10 · Disposizione dei campi.** `FieldForm` compatto come negli altri moduli (editor più stretti, colonna dell'unità
  vuota). Per conservare la disposizione di oggi il ViewModel ha form per blocco visivo: rotazione dopo la nota degli anelli,
  direzioni in due colonne, ogni opzione con la sua nota. Si accetta?
- **D11 · F1 sul diametro.** La vista storica apriva la guida del modulo con F1 su qualunque campo. La nuova, come
  `FieldForm`/`InputForm`, apre la sezione del campo quando esiste («Effetto di gruppo e applicabilità», solo per il diametro),
  altrimenti la guida. Le «?» sono le stesse.
- **D12 · Contrasto della Chiara.** La vista nuova conserva i colori di oggi, quindi anche i 4 difetti delle etichette degli
  indicatori. Al passaggio della Release (passo 6) le 4 voci passano dal passo storico al passo del modulo con la vista nuova,
  a totale invariato. L'alternativa è correggere il contrasto nella vista nuova, cambiando la Chiara.
- **D13 · Suite UI.** La parte UI di `HorizontalPileGroup.Checks` è in `ui/check-pile-group-view` dell'harness UiTests, non in
  un progetto `tests/ANTHEA.Desktop.UiTests`. Si conferma per i moduli successivi?
- **D14 · Banco interattivo.** `ANTHEA.exe --interactive --module-impl <modulo>=mvvm` esiste solo nella build UiTests, per le
  prove a mano prima del punto di controllo. Si tiene fino a F5.14?

Osservazioni senza decisione:
- Le operazioni che WPF accoda al dispatcher, per esempio la modifica di cella annullata, usano la cultura del thread e non
  quella di un flusso asincrono. Per questo le prove che impostano `it-IT` lo fanno anche in un'operazione del dispatcher. Su
  questa macchina la cultura dell'utente usa il punto come separatore decimale. Nel prodotto tutte le operazioni usano la
  stessa cultura, nella vista storica come nella nuova.
- La guida pratica (riga 819) cita l'espansore «Applicabilità e interassi rappresentativi»; nelle due viste si chiama
  «Opzioni di calcolo e applicabilità». È così anche oggi.

### 4.5 Da provare a mano in una build di prova

```
dotnet build X.Desktop/X.Desktop.csproj -c UiTests
X.Desktop\bin\UiTests\net8.0-windows\ANTHEA.exe --interactive --module-impl geo_efficienza_orizzontale=mvvm
```

1. File → Nuova palificata orizzontale: disposizione, campi, tabelle e disegno come oggi. Cambiare disposizione (anelli con
   «Palo al centro»), diametro e interassi: il ricalcolo parte dopo una breve pausa.
2. Generica: modificare una coordinata, aggiungere una riga, eliminarne una con Canc, premere Esc durante una modifica.
3. Fare clic su più righe del confronto: titolo, indicatori, dettagli e pianta seguono, e il documento risulta modificato.
4. «?» del diametro, F1 su un campo e F1 fuori dai campi: si apre la sezione giusta della Wiki.
5. Report Word: nome proposto `Relazione_palificata_orizzontale.docx`; esportazione dei risultati in JSON.
6. Aspetto Scuro e Molto scuro dal menu, con la scheda aperta.
7. In un progetto, aprire una revisione precedente: campi e scelte bloccati, confronto selezionabile.
8. Salvare, chiudere e riaprire l'archivio: stessi dati e stesso risultato selezionato.

## 5. Chiusura (17/10/2026, passi 5–7)

Ramo `refactoring/f5-4-chiusura`, da `refactoring/f5` `7979f79d`. Il punto di controllo è chiuso: l'utente ha accettato tutte le
proposte ([decisioni](decisioni.md), sezione «17 ottobre (mattina)»), compresi D8–D12 e il bordo `Token.Border`.

| Commit | Contenuto |
| --- | --- |
| `832af117` | `ui/check-pile-group-view` non apre più la vista storica: confronta con i valori che dava (sotto) |
| `e8855058` | passaggio della Release e archiviazione della vista storica (passo 6) |
| commit dei documenti | piano e questa nota |

### 5.1 Passaggio e archiviazione

- **Release.** `ModuleImplementations.Declared` ha `geo_efficienza_orizzontale` = `Mvvm`; gli altri moduli restano `Historic`.
  `ui/check-module-host-offscreen` e il banco interattivo di `ui/check-pile-group-view` lo provano: la radice di composizione
  apre la palificata con `MvvmSheetEditor` e gli altri moduli con `SheetEditor`.
- **Vista storica (Q11).** `HorizontalPileGroupWorkspace.cs` e `PileGroupHistoricTrace.cs` sono in
  `supporto/SUPERATI/ui-storica/palificata/`, con il percorso originale sotto la cartella, non compilati. `README.md` e
  `registro.json` riportano origine, ultimo commit, SHA-256, motivo e sostituzione. Le tracce congelate restano in
  `tests/fixtures/f5-palificata`.
- **Tolti:**
  - i rami `pileGroup` di `SheetEditor`: campo, `Result`, costruttore, `Dispose`, `Commit`, `CalculateAsync`, `BuildReportDocument`;
  - il comando `--check-pile-group-historic[-capture]` dell'harness;
  - la suite `ui/check-pile-group-historic`.
- **Invariati.** `MainWindow.ExportReport` propone `Relazione_palificata_orizzontale.docx` e `LoadSheetEditor` usa ancora
  `DisplayAdaptation.Viewport(…, 1120, 600)`: entrambi dipendono dal modulo. `PileGroupPlan` resta con le sue 32 voci F4 (37 occorrenze).
- **Banco.** `--module-impl geo_efficienza_orizzontale=historic` è rifiutato con un messaggio chiaro: la vista storica non
  esiste più e `SheetEditor` aprirebbe il ramo generico.
- **Prove della vista** (`ui/check-pile-group-view`). Le parti che aprivano la vista storica ora confrontano con i valori che
  essa dava:
  - dati all'apertura, dati e risultato dopo il calcolo uguali alla traccia congelata `fixture-<id>`;
  - JSON del risultato uguale byte per byte a quello di `SheetEditor.ExportResult`;
  - report Word e PDF uguali a quelli della ricetta storica (`ReportPileGroup` sul risultato, `ReportFiles`);
  - messaggio senza risultato, etichette «?», nomi di automazione e controlli bloccati in sola lettura registrati nella prova,
    uguali in tutte le corse fino a `20261017-060301-f5-int13`;
  - schermate della sola vista nuova.

  Le tracce attraverso i controlli della vista non cambiano.

### 5.2 Baseline

- **F4.** Nessuna voce da togliere: il file archiviato non ne aveva più dopo `ceb344d0`. Restano 3320 occorrenze.
- **P3 e `limits.json`.** Invariati (AsyncVoid 2, DispatcherTimer 4).
- **`tests/fixtures/f3/architecture.json`.** Sono le soglie iniziali di F3.1 e non sono state toccate. Elencano ancora un
  DispatcherTimer di `HorizontalPileGroupWorkspace.cs`, già assente prima dell'archiviazione: il tetto resta rispettato.
- **Contrasto (D12).** Nella baseline della Chiara la voce del passo `modulo-geo_efficienza_orizzontale` (4 occorrenze) passa
  dalla chiave della vista storica a quella della vista nuova:

  ```
  contrasto-testo|TextBlock|MainWindow > PileGroupSheetView|TextBlock #64748B Style|Border #F3F5F8 Style
  ```

  La chiave è quella misurata a 96 DPI dalla corsa `contrasto-mvvm-tre-aspetti` di f5-4-vista (sezione 4.3). Il totale resta 699
  in 187 voci. Il passo `mvvm-geo_efficienza_orizzontale` è tolto: il passo del modulo mostra ora la vista nuova.

**Da fare alla prima corsa a 96 DPI** (schermo al 100%). `ui/check-contrast` non è stata eseguita: lo schermo è al 150%.

```
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,ui -Only '^ui/check-contrast$' -Tag contrasto-96"
```

Atteso: Scuro 0, Molto scuro 0, Chiara uguale alla baseline (699 occorrenze in 187 voci), con le 4 occorrenze di
`modulo-geo_efficienza_orizzontale` sulla chiave nuova.

Se `ui/check-contrast` segnala per quel passo solo una chiave diversa, a parità di occorrenze, si sostituisce quella voce con
quella di `baseline-chiara.json` della corsa, a totale invariato, in un commit dedicato. Qualunque altra differenza è una
regressione da spiegare.

### 5.3 Equivalenza

| Corsa | Esito |
| --- | --- |
| `ui/f3-equivalence` con la Release MVVM (`20261017-095320-f54c-b-mirata`) | PASS 104 fixture, 1066 file, 0 differenze non ammesse, `transitions-f4.json` invariato, nessuna voce nuova |
| confronto diretto con la cattura di `20261017-060301-f5-int13` (Release storica), `tools/qa/f5_confronto_diretto.py` | identiche: 1063 file uguali byte per byte, 3 JSON uguali in forma canonica (tempi del palo elastico) |
| `ui/f3-report-chapters` | PASS 105 capitoli autonomi |
| `ui/check-pile-group-view` | PASS 97 controlli |
| `HorizontalPileGroup.Checks` | PASS |
| corsa finale `build,fast,wiki,ui` senza `ui/check-contrast` | PASS 118, KNOWN 0, NEW-FAIL 0 su `e8855058` (`20261017-095952-f54c-finale`): build 25, fast 31, wiki 2, ui 60; nessun file tracciato modificato; `qa/no-test-code` (Release senza codice di prova) PASS |

### 5.4 Guida pratica (passo 7)

La sezione «Efficienza orizzontale della palificata» di `supporto/docs/guida-pratica-anthea.md` non descrive le differenze
accettate (titoli a 22 punti, form compatto, direzioni in due colonne). Per F1, la frase generale della sezione sull'aiuto
contestuale descrive già il comportamento nuovo: F1 apre l'aiuto del dato quando esiste, altrimenti la guida del modulo. Il
passaggio non richiede quindi modifiche alla guida, e Word e PDF non vanno rigenerati.

Resta l'errore già presente prima del passaggio, nel paragrafo «Metodo e applicabilità». La guida dice «Aprire Applicabilità e
interassi rappresentativi», ma l'espansore si chiama «Opzioni di calcolo e applicabilità», nella vista storica come nella nuova.
Non è stato corretto in questo passo.

Il motivo: la guida alimenta la Wiki, e il testo della Wiki è nel riferimento F3 (`expected/wiki/_wiki_guide_palificata-orizzontale.md`
e `.txt`; `wiki/search.json` va ricontrollato nella cattura). La correzione richiede quindi un aggiornamento editoriale F3-06: Wiki rigenerata con
`build-wiki-index.py`, file del riferimento copiati dalla cattura, chiave `editorial_updates` del manifest e voce di registro.
Sono modifiche del riferimento F3 escluse da questo passo.

Proposta per un passo documentale:
1. sostituire «Aprire Applicabilità e interassi rappresentativi e,» con «Aprire Opzioni di calcolo e applicabilità e,»;
2. aggiornare la revisione delle guide e rigenerare Word e PDF;
3. aggiornare il riferimento della Wiki come in `64b7a16c`.

### 5.5 Suite dello stadio ui

La suite `ui/check-pile-group-historic` è tolta, senza suite nuove. `HorizontalPileGroup.Checks` e `ui/check-pile-group-view`
restano.
