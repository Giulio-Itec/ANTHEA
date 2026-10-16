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
| `Field(...)` con `Value`/`optional` | `FormViewModel` + `FieldSpec` (`NumberOrText`, `NumberOrTextRemoveEmpty`) |
| `Option(...)` | `FieldSpec(Bool: true)`; «custom» abilita l'angolo (`Enable`) |
| combo della disposizione, `BuildGenerator` | `SelectedLayout`, `GeneratorForm`, `GeneratorNote` |
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
| `RevisionInspection.Protect` (sola lettura) | `IsReadOnly`, `CanEdit`, `CanEditCoordinates`; selezione e calcolo sempre ammessi |

Il modulo `PileGroupSheetModule` dichiara le chiavi di stato `metodo`, `direzione_selezionata` e `angolo`. È registrato
in `DesktopComposition.CreateModules`. La Release resta storica: `ModuleImplementations` non cambia.

Restano espliciti nel ViewModel, perché i componenti di F5.2 non li esprimono:
- **La tabella delle coordinate.** `JsonRowViewModel` scrive il testo grezzo cella per cella. La vista storica, invece,
  tiene righe di testo e scrive l'intero elenco normalizzato, solo con «Generica» e solo se cambia.
- **La sola lettura dei campi.** `FormViewModel` non ha uno stato di sola lettura. In revisione il ViewModel ricostruisce
  i form su copie staccate, quindi la scrittura non raggiunge mai i dati.

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

**Punti da confermare dall'utente:**

- **D1 · Q8.** Si conferma la raccomandazione (a): calcolo breve sincrono e scritture della selezione nel documento come
  oggi (sezione 3).
- **D2 · Riferimento F3.** Con l'aggiunta delle fixture cambia solo il numero in `expected/esito.txt` (36 → 42). La
  modifica è registrata nel manifest (`fixture_aggiunte_f5_4`, impronte prima e dopo); in alternativa si userebbe una voce
  in `transitions-f5.json`.
- **D3 · Interassi mancanti.** Se un archivio non ha l'oggetto degli interassi della direzione mostrata all'apertura, la
  vista lo crea vuoto, mentre la preparazione del capitolo autonomo no: dati preparati diversi, report uguale. Oggi nessuna
  fixture lo mostra. Il ViewModel riproduce la vista. Da decidere se allineare `ModuleSheetPreparation.PreparePileGroup`
  (modifica dell'Application, non dei report).
- **D4 · Sola lettura dei form.** `FormViewModel` e `FieldForm` non hanno uno stato di sola lettura del foglio. Il
  ViewModel usa copie staccate. Si propone uno stato `IsReadOnly` nel form e il legame in `FieldForm` (area F5.3), da
  usare in tutti i moduli.
- **D5 · Modalità sincrona.** Si conferma `SheetCalculationMode.Synchronous` nel componente comune, come regola per i
  moduli che oggi calcolano sul thread dell'interfaccia.
- **D6 · `Reload`.** Il foglio si ricostruisce come all'apertura (migrazioni comprese) e ricalcola dopo il ritardo, senza
  `Modified`. La direzione degli interassi mostrata resta quella scelta.
- **D7 · `Calcola` da `MvvmSheetEditor`.** Prima conferma le bozze (`Commit`), mentre la palificata storica calcola
  senza `Commit`. Il risultato è lo stesso: `Commit` ricalcola solo se un ricalcolo è in attesa, e ricalcolare gli stessi
  dati non cambia nulla.
- **D8 · Etichette delle direzioni.** Nella vista nuova non hanno più gli spazi di allineamento («+X · 0°     »).

**Materiale da mostrare** (dopo i passi 1–3 della sezione 2):
- schermate fuori schermo nei tre aspetti;
- esito di `--check-contrast`;
- confronto diretto a zero;
- `ui/f3-equivalence` con il modulo MVVM;
- conteggio delle suite `ui`;
- questa nota con le risposte.
