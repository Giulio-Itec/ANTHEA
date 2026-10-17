# F5.6 — Palo e micropalo verticale: preparazione del secondo pilota

## Scopo

Questa nota chiude la **preparazione** di F5.6, il secondo pilota scelto con Q20: moduli `geo_palo_verticale` e
`geo_micropalo_verticale`. Oggi entrambi passano dal ramo generico di `SheetEditor` e da `X.Desktop/Wpf/GeoEditor.cs`:
- `InputForm` e `JsonGrid`;
- schede impaginate su un `Canvas`;
- tre form;
- strati per sondaggio;
- grafici della capacità;
- stratigrafia disegnata;
- calcolo sullo sfondo con `RecalculationScheduler`.

La nota segue i passi 1–5 della «Ricetta per i moduli successivi» di [f5-4-pilota.md](f5-4-pilota.md). Contiene:
- che cosa esiste;
- i difetti della vista storica trovati con le tracce;
- che cosa resta da fare;
- le domande per il punto di controllo F5.6.

La vista XAML, il passaggio in `ModuleImplementations` e l'archiviazione vengono **dopo** il punto di controllo. La
stratigrafia (F5.6b) non fa parte di questo passo.

Riferimenti:
- [proposta](f5-proposta-architettura.md), sezioni 3, 4.5, 5, 7 e riga F5.6 della sezione 8;
- [inventario](f5-inventario-ui.md), sezioni 1.6 e 3.5;
- `tests/fixtures/f3/README.md`, `tests/fixtures/f5-palo-verticale/README.md`.

Ramo `refactoring/f5-6-palo`, da `refactoring/f5` `3c336992`, 16/10/2026.

## 1. Che cosa esiste dopo la preparazione

| Commit | Contenuto |
| --- | --- |
| `0abe1ac2` | 7 fixture F3 nuove (4 palo, 3 micropalo; 13 in tutto, 49 nel riferimento) e 7 figure nuove di `f3-figures`, catturate con la vista storica |
| `db1a0b44` | tracce congelate della vista storica: 18 scenari, suite `ui/check-palo-verticale-historic` |
| `8f970183` | derivazioni in Application (`VerticalPilePresentation`), campi condivisi in Presentation (`VerticalPileFields`), baseline F4 3351 → 3349 |
| `2ba01555` | ViewModel `src/ANTHEA.Presentation/Modules/VerticalPile`, moduli registrati, figure WPF del report, prove senza WPF |

### 1.1 Fixture F3

Le 7 fixture nuove coprono:
- palo battuto (prefabbricato), trivellato ed elica continua;
- falda con e senza sottospinta, falda al piano campagna;
- più stratigrafie, strati granulari e coesivi (ramo non drenato), strati esclusi dalla laterale;
- i tre metodi di efficienza (Converse–Labarre, Feld, definita dall'utente) e i ξ di 2, 3 e ≥10 verticali;
- una stratigrafia più corta del palo (risultati alla quota disponibile, avviso della scheda);
- la visibilità dei grafici salvata nel foglio;
- avvisi: γb del foglio diverso da quello della tecnologia, efficienza;
- micropali IRS (punta al 10%, inclinazione, inizio dell'aderenza, tubo CHS) e IGU (copertura incompleta);
- due calcoli fermati con «Dati da completare»: Cu mancante sotto falda, punta oltre il 15%.

Gli input vengono dal generatore `F3Fixtures.VerticalPileF56`. Gli attesi sono 59 file, da due catture uguali. Con queste
fixture passano `ui/f3-equivalence`, `ui/f3-report-chapters`, `ui/f3-figures` e `ui/f3-scheduling`, con le stesse
transizioni F4.

Due modifiche al riferimento, entrambe registrate nel manifest (`fixture_aggiunte_f5_6`):
- `ui/f3-figures` richiede la figura congelata di ogni fixture dei pali verticali: le 7 nuove vengono da due catture
  uguali, nelle quali le 6 esistenti risultano invariate;
- in `expected/esito.txt` cambia solo il numero delle fixture, 42 → 49 (come D2 di F5.4).

### 1.2 Tracce della vista storica

Le tracce (`tests/fixtures/f5-palo-verticale/storica`) registrano, dopo l'apertura e dopo ogni passo:
- i byte dei dati nell'ordine in memoria;
- `Modified`, la sola lettura e la possibilità di esportare (risultato presente e nessun calcolo in corso);
- risultato, stato e avvisi;
- i tre form, campo per campo: etichetta, valore, voci, attivo, attenuato, visibile;
- verifica, stratigrafie (righe come le mostra la tabella), profilo;
- grafico della capacità con le curve, grafico di riferimento;
- tabelle e JSON dei risultati.

Gli scenari sono 18:
- il percorso F3 per ognuna delle 13 fixture, più il ricalcolo dopo la riapertura;
- modifiche di ogni tipo sul palo, sulle stratigrafie e sul micropalo;
- la sola lettura;
- il calcolo sullo sfondo con il motore trattenuto da una barriera (`HeldModuleService`):
  - calcolo superato da una modifica e mai pubblicato;
  - pubblicazione della sola revisione corrente;
  - «Calcola» durante un calcolo;
  - chiusura e riapertura durante un calcolo.

Ogni passo è fatto con i controlli, come l'utente: un controllo spento o protetto dalla revisione non fa nulla.

La stessa definizione (`tests/Shared/VerticalPileTrace.cs`) guida due prove:
- la vista storica, stadio `ui`, finché esiste;
- il ViewModel, stadio `fast`.

### 1.3 Derivazioni in Application e campi condivisi

`src/ANTHEA.Application/Presentation/VerticalPilePresentation.cs` contiene il codice spostato senza modifiche da
`GeoEditor`, `SheetEditor` e `ModuleFigureRenderer`:
- testi di stato e del JSON dopo una modifica; avvisi con la copertura incompleta;
- campi attivi per opzione, campi dell'efficienza per metodo, valori del «Reset» della normativa;
- righe e colonne della verifica;
- riepilogo alla quota disponibile, tabelle, nota e piede della capacità, formato delle celle;
- titolo, asse e messaggi del grafico della capacità, quota disponibile, valori a L, etichette delle curve;
- piano delle figure del report: ordine, didascalie, categorie, dimensioni. `ModuleFigureRenderer.VerticalFigures` ora
  disegna soltanto.

`src/ANTHEA.Presentation/Modules/VerticalPile/VerticalPileFields.cs` è l'unica definizione dei campi dei tre form, delle
colonne delle stratigrafie (con le voci dei fogli precedenti) e delle viste della capacità. La usano `GeoEditor` e il
ViewModel.

La vista storica chiama queste funzioni e dà un'uscita identica: tracce, F3, figure, capitoli autonomi e `ui/smoke`.

La baseline F4 cambia solo per spostamenti e rimozioni, da 3351 a 3349:
- 4 voci tolte: le tre letture della quota disponibile (`GeoEditor`, `SheetEditor`, `ModuleFigureRenderer`) e la
  lunghezza in `Preview`;
- 2 voci spostate in `VerticalPilePresentation`.

### 1.4 ViewModel

`VerticalPileSheetViewModel` (`src/ANTHEA.Presentation/Modules/VerticalPile`) riproduce il ramo generico di `SheetEditor`
con `GeoEditor`:

| Vista storica | ViewModel |
| --- | --- |
| costruttore e `BuildGeo`: `PrepareVertical`, form, `stratigrafie` creato se manca, `RebuildSondages`, `Preview`, `QueueCalculation` | costruttore: stessa sequenza, poi `ScheduleCalculationAsync` |
| `generalForm`, `efficiencyForm`, `normativeForm` (`InputForm`) | `GeneralForm`, `EfficiencyForm`, `NormativeForm` (`FormViewModel` + `VerticalPileFields`, testo grezzo, nessun formato dei numeri) |
| callback del form generale: α ripristinati (iniezione), γb della tecnologia, `Changed` | `OnGeneralChanged`: stessa sequenza, due `Modified` quando cambia il tipo di palo |
| «Reset» | `ResetNormative` |
| `effLabel` | `EfficiencyText` |
| `UpdatePileVerification` | `Verification`, `VerificationColumns` |
| `sondages`, `layerGrids`, `Bind` (copia di presentazione), `RebuildSondages` | `Surveys` (`VerticalPileSurveyViewModel`: `Columns`, `Rows` come `JsonRowCollection`), `SelectedSurveyIndex`, stessa copia |
| «+», [−] della scheda, «−», `CopySurvey`, `ChooseSurveyCopy` | `AddSurvey`, `RemoveSurvey`, `RemoveSelectedSurvey`, `CopySurvey`, `CopySurveyIntoAsync`/`CopySurveyFromAsync` (`SurveyCopyDialogViewModel` con `IDialogService`, conferme con `ConfirmDelete`, messaggi con `Inform`) |
| «Aggiungi strato», [−] dello strato | `AddLayer`, `RemoveLayer` |
| `UpdatePileCoefficients` (colonne calcolate e colore) | colonne calcolate nella copia di presentazione; il colore dello strato è un token della vista (`Swatch.Soil1`–`6`) |
| `capacityView`, `plot`, `allSeries`, `visibility`, `UpdateVisible`, `RebuildCurveChoices`, `StoreVisibility`, «Tutte»/«Nessuna» | `CapacityViews`, `SelectedCapacityView`, `CapacityChart` (dati semplici), `Curves`, `CurveChoices`, `EndValues`, `ShowAllCurves`/`HideAllCurves`; `visibilita_grafici` scritto con `ChangedKeepingResults` |
| `reference`, `BuildReferencePlot` | `ReferenceChart` (`PlotData`) |
| `tableSelect`, `tableHost`, `ShowTable`, `DetailTable` | `TableTitles`, `SelectedTableIndex`, `ShownTable` (blocchi con le celle già formattate) |
| `raw` | `RawResult` |
| `status`, `warnings` | `Status`, `WarningText`: stessi testi, «Calcolo in corso…» quando il motore parte |
| `Changed` | `UserChanged`: risultati azzerati, anteprima, verifica, poi `Changed` della base |
| `CalculateScheduledAsync` (450 ms, `Task.Run`) | `SheetCalculationMode.Background`, ritardo 450 ms, `IModuleService` del contesto |
| `BuildReportDocument` con i `Plot` della vista | `Report`: `ReportWord` con le figure di `IVerticalPileFigureRenderer` (WPF nel desktop: `VerticalPileFigureRenderer`) |
| `InspectRevision` (`RevisionInspection.Protect`) | `IsReadOnly`: form su copie staccate; righe, stratigrafie e «Reset» rifiutati; curve, viste, tabelle e calcolo ammessi |

`VerticalPileSheetModule` dichiara la chiave di stato `visibilita_grafici`. Ne esiste un'istanza per ognuno dei due
moduli, registrata in `DesktopComposition.CreateModules` con le figure WPF del report. La Release resta storica:
`ModuleImplementations` non cambia.

### 1.5 Prove

`Presentation.Tests` passa da 461 a 696 controlli; i 235 nuovi sono di `VerticalPileSheetChecks`:
- le 18 tracce uguali riga per riga a quelle della vista storica;
- le 13 fixture F3: aperto, visitato, calcolato, risultato, testo e struttura del report, esportato, stato, riaperto.
  Sono uguali byte per byte al riferimento, salvo i 3 file con transizioni F4. Questi si confrontano come in
  `ui/f3-equivalence`, con le 4 transizioni ammesse;
- le serie dei grafici uguali a `f3-figures`: tutte le curve, ogni vista della capacità e il grafico di riferimento di
  ogni stratigrafia;
- il modulo e le chiavi di stato; le derivazioni di Application con valori scritti a mano;
- con la barriera:
  - pubblicazione della sola revisione corrente;
  - chiusura durante il calcolo;
  - sola lettura e `Reload`;
- il cambio dell'iniezione con un risultato (sezione 2);
- conferme e dialogo «Copia in…»/«Copia da…».

Stadio `ui`:
- `ui/check-palo-verticale-historic`;
- `ui/check-module-host-offscreen`: registro con la palificata e i due moduli verticali; la Release apre la vista storica;
- le suite F3.

## 2. Difetti della vista storica trovati

1. **Cambio dell'iniezione del micropalo con un risultato.** La vista ricostruisce le stratigrafie mentre il risultato
   dell'altra iniezione è ancora pubblicato. `VerticalPileCharts.Reference` cerca allora la curva dell'abaco della nuova
   iniezione e fallisce («Sequence contains no matching element»). In produzione l'errore va nel registro e in un
   messaggio. Il foglio resta con gli α già riscritti, senza `Modified` e senza ricalcolo, e il risultato mostrato è
   quello dell'iniezione precedente. Lo scenario delle tracce cambia l'iniezione quando il risultato non c'è.
2. **Probabile, non provato**: lo stesso errore nel micropalo con un risultato quando si copia una stratigrafia su
   un'altra con terreni di un'altra famiglia, o quando l'eliminazione sposta la selezione su una stratigrafia diversa. Il
   meccanismo è lo stesso: il grafico di riferimento è ricalcolato con il risultato vecchio e i terreni nuovi.

Il ViewModel ricalcola il grafico di riferimento solo:
- con il risultato dei dati mostrati (selezione di una stratigrafia, pubblicazione);
- senza risultato dopo una modifica.

Quindi non fallisce: la prova «cambio dell'iniezione con un risultato» lo verifica. Rispetto alla vista storica, la
differenza c'è solo dove la vista storica fallisce.

## 3. Che cosa resta

L'ordine segue i passi 6 della ricetta di F5.4, dopo il punto di controllo F5.6.

Stato al 17/10: punti 1 e 2 fatti, con i dialoghi condivisi di V9/T5 (sezione 5). Il punto 3 resta per il passaggio della Release (V12).

1. **Vista XAML** `X.Desktop/Views/VerticalPile/VerticalPileSheetView.xaml` e il suo DataTemplate. Solo token di F5.3.
   - Tre `FieldForm`.
   - Le stratigrafie in un `TabControl` (la visita di `ui/f3-equivalence` le scorre). Ogni intestazione ha il suo [−];
     la tabella ha le colonne di `StratigraphyTable`, con legami `UpdateSourceTrigger=PropertyChanged`.
   - Il colore dello strato viene dai token `Swatch.Soil1`–`6` per indice.
   - Le schede si impaginano come `LayoutCards`, con «Estendi»/«Riduci». `ShowAll` del profilo dipende dalla scheda
     estesa: è stato della vista.
   - Il code-behind ammesso è solo il disegno:
     - `Plot` della capacità e del riferimento dai dati semplici, con «Adatta» e «Salva PNG…» (`IFileDialogService`);
     - `StratigraphyDrawing`.
   - Sola lettura: legami a `CanEdit`, tranne curve, viste, tabelle e calcolo.
   - Piede: stato, avvisi, «Modello e dati comuni…» e, solo per il palo, «Invia / carica terreno…» (vedi V9).
2. **Banco e confronto diretto**: `--module-impl geo_palo_verticale=mvvm` e `geo_micropalo_verticale=mvvm`, cattura F3,
   poi `tools/qa/f5_confronto_diretto.py` a differenza zero. Infine il confronto normale con `expected` e le 4 transizioni
   F4 dei due file `regressione`.
3. **Suite UI riscritte** sul ViewModel o sulla vista ospitata da `MvvmSheetEditor`, prima del passaggio della Release.
   Il palo verticale è il documento predefinito della finestra (`Archivio.Documento("geo_palo_verticale")`), quindi molte
   suite aprono un foglio verticale:
   - `ui/smoke`: `SmokeChecks` (`VerifyWpf`, `WaitForAutomatic`, `SetGeometryForSmoke`);
   - `ui/smoke-display`: `DisplaySmokeChecks`, la partial più grande (41 accessi ai membri dei pali verticali: layout,
     scorrimento, digitazione nella tabella durante il ricalcolo, esiti, presentazione, report, navigazione, copia ed
     eliminazione delle stratigrafie);
   - `ui/f3-figures`: `F3FigureChecks` legge `allSeries`, `capacityView`, `plot`, `sondages` e `reference` della vista
     storica. La parte di contratto passa a `Curves`, `CapacityChart`, `SelectedSurveyIndex` e `ReferenceChart`; le figure
     congelate restano;
   - `ui/f3-scheduling`: `F3SchedulingChecks` modifica il diametro con `generalForm`, fuori dal contratto `ISheetEditor`.
     Passa a `GeneralForm`, con la barriera già prevista dal `SheetContext`;
   - le suite di progetto che chiamano `historicEditor.SetGeometryForSmoke` o `WaitForAutomatic` su un foglio verticale:
     `ProjectSmokeChecks`, `ProjectRevisionNavigationSmokeChecks`, `ProjectRevisionDeletionSmokeChecks`,
     `AppearanceChecks`;
   - `ContrastAudit`, che legge il foglio attivo;
   - le suite che usano solo la superficie di `ISheetEditor` (dati, risultato, report, copia del modulo) non vanno
     riscritte, ma girano con il foglio MVVM e vanno ripetute: `ReportAppearanceChecks`, `RetainingWallSmokeChecks`
     (invio del terreno a un palo), `ProjectSharingSmokeChecks`, `ProjectComparisonChecks`, `ProjectReportSmokeChecks`;
   - le partial di prova di `SheetEditor` che toccano i pali verticali: `SmokeChecks`, `DisplaySmokeChecks`,
     `F3FigureChecks`, `F3SchedulingChecks` e `VerticalPileHistoricTrace`. Le altre sei (`ConcreteExtensionSmokeChecks`,
     `ConcreteFeatureChecks`, `HorizontalSmokeChecks`, `ReportAppearanceChecks`, `F3ReportChapterChecks`,
     `SectionActionsSmokeChecks`) e `PileGroupHistoricTrace` non leggono i membri del ramo verticale.
4. **Punto di controllo dell'utente** con il materiale della sezione 4.
5. **Passaggio della Release e ritiro della vista storica (Q11)**, nello stesso commit:
   - `ModuleImplementations.Declared` per i due moduli;
   - `GeoEditor.cs` e `tests/Desktop/VerticalPileHistoricTrace.cs` in `supporto/SUPERATI/ui-storica/palo-verticale/`;
     la suite `ui/check-palo-verticale-historic` si toglie, le tracce restano per `Presentation.Tests`.
6. **`SheetEditor` ridotto allo smistamento**: dopo questo modulo nessun altro usa il ramo generico. Si tolgono:
   - le schede su `Canvas` (`AddCard`, `LayoutCards`, rami `Section` ormai morti);
   - `Changed`, `QueueCalculation`, `CalculateScheduledAsync`, scheduler e sessione propri;
   - tabelle, verifica, `DetailTable` e `SavePlot`;
   - i campi `plot`, `reference`, `stratigraphy`, `sondages`, `layerGrids`, `outputs`;
   - il ramo `ModuleFigureRenderer.VerticalFigures` di `BuildReportDocument`.

   Le figure del report di progetto restano a `ModuleFigureRenderer` e alla pianificazione in Application.
7. **Guida pratica e PDF**, se la disposizione descritta cambia.

## 4. Domande per il punto di controllo F5.6

- **V1 · Calcolo sullo sfondo.** Il ViewModel calcola sullo sfondo (`SheetCalculationMode.Background`), con il ritardo di
  450 ms e il limitatore dell'applicazione, come oggi. Da confermare.
- **V2 · Riferimento F3.** Due modifiche, registrate nel manifest: il numero delle fixture in `expected/esito.txt`
  (42 → 49) e 7 figure congelate nuove in `f3-figures`. Si risponde come per D2 di F5.4.
- **V3 · Difetti della sezione 2.**
  - Correggerli nella vista storica, che sarà archiviata? La correzione toccherebbe il comportamento storico e le tracce.
  - Oppure registrarli come differenza dichiarata del ViewModel nel registro (voce F5-…)?
  - Proposta: la seconda, con una prova sulla vista nuova.
- **V4 · Sola lettura dei form.** Come D4 di F5.4: `FormViewModel` non ha uno stato di sola lettura e il ViewModel usa copie
  staccate. In sola lettura le celle rifiutate si ripristinano ricostruendo le righe. Lo stato `IsReadOnly` del form
  (area della vista della palificata) andrebbe usato anche qui.
- **V5 · Curve in sola lettura.** Oggi, in una revisione in sola lettura, le caselle delle curve e «Tutte»/«Nessuna»
  scrivono `visibilita_grafici` e sollevano `Modified`. Il ViewModel fa lo stesso. Da confermare o da cambiare (sarebbe
  una differenza dichiarata).
- **V6 · `Reload`.** Il foglio si ricostruisce come all'apertura (migrazioni comprese), senza `Modified`, e ricalcola dopo
  il ritardo. Restano la stratigrafia selezionata (nei limiti), la vista della capacità e il grafico, con il titolo
  iniziale. Come D6 di F5.4.
- **V7 · «Occupato».** Dopo una modifica durante un calcolo, `Busy` della vista storica resta vero finché il motore non
  torna; nel ViewModel `IsBusy` diventa falso subito, perché lo stato passa a «in attesa». Dall'esterno non si vede: senza
  risultato l'esportazione è rifiutata in entrambi i casi. C'è un solo caso osservabile: un secondo «Calcola» con gli
  stessi dati mentre il primo è in corso. Il ViewModel ammette allora l'esportazione del risultato già pubblicato, che è
  quello degli stessi dati. Da confermare che va bene.
- **V8 · Figura della capacità nel report.** Il report storico copia il grafico come appare, compresi zoom e
  spostamento. Il ViewModel passa le serie mostrate (vista, curve, titolo, quota) e la figura si disegna a vista intera.
  Cambia solo l'immagine, che il confronto F3 non vede. Da confermare.
- **V9 · Dialoghi condivisi.** «Invia / carica terreno…» (`SoilTransferDialog`, solo palo) e «Modello e dati comuni…»
  (`CalculationHelpView`) servono anche ad altri moduli; il ViewModel non li ha ancora. Proposta:
  - un ViewModel del trasferimento del terreno con `IDialogService`, quando si scrive la vista (o con F5.6b);
  - la guida comune con `IWikiNavigator` o un dialogo.
- **V10 · Colore degli strati.** Il ViewModel non scrive più `__color` nella copia di presentazione: la vista usa i token
  `Swatch.Soil1`–`6` per indice, già uguali ai colori di oggi.
- **V11 · Sezioni del report.** La finestra «Contenuti del report Word» resta in `MainWindow`, che sceglie
  `ReportWord.Sezioni` per modulo. `ModuleReportCapabilities` dichiara ancora solo il report short. Proposta: le sezioni
  nel contratto con la shell (F5.13).
- **V12 · Ampiezza del passaggio.** Il palo verticale è il documento predefinito della finestra: il passaggio della
  Release richiede le suite UI riscritte della sezione 3 (punto 3). Lo si conferma come lavoro dello stesso passo, prima
  del passaggio.

**Materiale da mostrare** (dopo i passi 1–3 della sezione 3):
- schermate fuori schermo nei tre aspetti e `--check-contrast`;
- confronto diretto a zero;
- `ui/f3-equivalence` con i due moduli MVVM;
- conteggio delle suite `ui`;
- questa nota con le risposte.

## 5. Vista XAML e banco (17/10/2026)

Ramo `refactoring/f5-6-vista`, da `refactoring/f5` `7b4a2d9a`. Decisioni applicate: quelle del 17/10 (mattina), cioè tutte le
proposte della sezione 4 e di [f5-punti-di-controllo.md](f5-punti-di-controllo.md), più le scelte prudenti del coordinatore
in [decisioni.md](decisioni.md). La Release resta **storica** per i due moduli: la vista nuova si sceglie solo nel banco UiTests
(`--module-impl geo_palo_verticale=mvvm --module-impl geo_micropalo_verticale=mvvm`) e con `ANTHEA.exe --interactive` più gli
stessi argomenti, finché l'utente non prova una versione portatile (V12: il passaggio richiede le suite riscritte della
sezione 3, punto 3).

### 5.1 Che cosa è stato fatto

| Commit | Contenuto |
| --- | --- |
| `87d2a25e` | ViewModel allineato alle decisioni; dialoghi condivisi (T5) in `src/ANTHEA.Presentation/Modules/Shared`; voce F5-01 del registro |
| `f6258b51` | vista XAML `X.Desktop/Views/VerticalPile`, dialoghi «Copia in…»/«Copia da…» e «Terreno · invio e riutilizzo»; `SoilTransferPresentation` in Application |
| `4c6e37cb` | `ui/check-palo-verticale-view` (`tests/Desktop/VerticalPileViewChecks.cs`) e correzioni trovate dalle prove |
| (questo commit) | varianti MVVM di `ui/f3-figures` e `ui/f3-scheduling`, `IsBusy` di un foglio chiuso, questa sezione |

**ViewModel.**
- V1: calcolo sullo sfondo con il ritardo di 450 ms, invariato.
- T2/V4: sola lettura con `FormViewModel.IsReadOnly` sugli stessi form, senza copie staccate. I comandi che cambiano i dati
  sono spenti, come i pulsanti della vista storica protetti da `RevisionInspection`.
- Comandi per la vista (Q3): Reset, «+», «−», [−] delle schede e degli strati, «Copia in…»/«Copia da…», «Aggiungi strato»,
  «Tutte»/«Nessuna», «Estendi»/«Riduci» (`ExpandedCard`, stato della vista), «Salva PNG…», «Modello e dati comuni…»,
  «Invia / carica terreno…». Gli errori passano dal guard del foglio (Q19).
- V3: il cambio dell'iniezione del micropalo con un risultato non fallisce. È la voce **F5-01** di
  `registro-differenze.json`, con la prova sulla vista nuova.
- V5: le caselle delle curve e «Tutte»/«Nessuna» scrivono `visibilita_grafici` anche in sola lettura, come oggi (T6).
- V7, V8 accettate; V10: colore degli strati dai token `Swatch.Soil1`–`6` per riga; V11: le sezioni del report restano a
  `MainWindow`.
- Un foglio chiuso non è mai «occupato», come la vista storica (prova di `ui/f3-scheduling`).

**T5, dialoghi condivisi** (`src/ANTHEA.Presentation/Modules/Shared`).
- `ISoilTransferDialog` e `SoilTransferChoice` spostati da `Modules/Stratigraphy`. La stratigrafia e `WpfSoilTransferDialog`
  cambiano solo lo `using`: la radice di composizione dà ancora alla stratigrafia la finestra storica.
- `SoilTransferDialogViewModel`: la finestra storica `SoilTransferDialog` senza WPF, mostrata con `IDialogService`
  (`SoilTransferDialogService`). Usa `IFileDialogService` per «Carica terreno…» e «Salva terreno…», e la domanda di
  sostituzione `IConfirmationService.ConfirmReplace` (membro nuovo con implementazione predefinita; in WPF Sì/No senza icona,
  come oggi). L'anteprima viene da `SoilTransferPresentation` (Application), spostata senza modifiche dalla finestra storica,
  che ora la chiama.
- `CalculationGuide`: «Modello e dati comuni…» apre con `IWikiNavigator` la stessa pagina di
  `MainWindow.ShowContextualWikiTheory`.
- Prove: il dialogo nuovo e la finestra storica sugli stessi dati (palo, stratigrafia, muro, palo orizzontale, fondazioni;
  con e senza fogli del progetto) danno gli stessi profili, stato, descrizione, anteprima, destinazioni, guida, invii e
  sostituzioni. Le tracce della stratigrafia in `Presentation.Tests` guidano ora il ViewModel condiviso al posto della replica:
  stesse tracce congelate.

**Vista** (`X.Desktop/Views/VerticalPile`).
- Sette schede come oggi, impaginate da `VerticalPileCardPanel`, che è `LayoutCards` spostato in un pannello: larga oltre
  1450 px, compatta (due colonne, una sotto i 1000 px), scheda estesa.
- Form `FieldForm` (compatti per «Efficienza» e «Coefficienti normativa»), tabelle degli strati con le colonne, i nomi di
  automazione e i suggerimenti di `StratigraphyTable`, verifica, «Tabelle e dettagli», JSON, piede con stato e avvisi.
- Solo token (P7). Il code-behind disegna soltanto: `Plot` della capacità e del riferimento dai dati semplici del ViewModel,
  `StratigraphyDrawing` sui dati del foglio.

### 5.2 Risultati

| Prova | Esito |
| --- | --- |
| `ui/check-palo-verticale-view` | PASS 377 controlli: 18 tracce attraverso i controlli uguali a quelle congelate; 13 fixture con visita senza scritture e esportazioni (JSON, Word, PDF, «Salva PNG…») uguali alla vista storica; F5-01; schermate; aiuto; nomi di automazione; sola lettura; dialogo del terreno |
| Cattura F3 con i due moduli MVVM | PASS 104 fixture, uguale al riferimento con le transizioni F4 di sempre |
| Confronto diretto (`tools/qa/f5_confronto_diretto.py`) fra cattura storica e MVVM | **0 differenze** su 1066 file (1063 uguali byte per byte, 3 JSON uguali in forma canonica; ignorati solo i 6 tempi del palo elastico) |
| `ui/f3-figures` con MVVM (`ui/f3-figures-palo-verticale-mvvm`) | PASS 13 fixture, serie della capacità confrontate esattamente |
| `ui/f3-scheduling` con MVVM (`ui/f3-scheduling-palo-verticale-mvvm`) | PASS 47 controlli |
| `ui/f3-report-chapters` con MVVM | PASS 105 capitoli autonomi, 246 immagini e tutte le parti XML confrontate con la vista |
| Corse del runner | `quick` e corsa finale `-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'`: CORSE-DA-COMPLETARE |

Le schermate sono a 144 DPI (schermo al 150%), in `vista/schermate` della suite: tre aspetti per palo e micropalo,
impaginazione larga, grafici estesi, stratigrafie del micropalo estese. `ui/check-contrast` non è stata eseguita (T10: va
ripetuta a 96 DPI).

Baseline architetturali: F4 invariata nel totale (2185). Le 2 voci `R4-implicit-number` di `SoilTransferDialog.Refresh`
passano a `SoilTransferPresentation.Preview` (codice spostato). Le altre baseline non cambiano.

### 5.3 Scelte da confermare

- **Colori dell'esito della verifica.** La vista storica usa #16703B e #B42318, che non hanno un token. La vista nuova usa
  `Status.Ok` e `Status.Fail`: i colori cambiano di poco.
- **Corpi 15, 18 e 10** della vista storica portati ai token vicini: `Font.Subheading` (14), `Font.Heading` (16),
  `Font.Caption` (11).
- **Finestre dei dialoghi.** `WpfDialogService` adatta la finestra al contenuto. La vista del dialogo ha quindi la dimensione
  della finestra storica (940×660 e 430×210, bordi esclusi), invece di una finestra ridimensionabile con minimo 720×520.
- **«Modello e dati comuni…» fuori dalla finestra principale.** La vista storica mostrava un testo in un dialogo;
  `IWikiNavigator` apre la Wiki della finestra principale. I fogli del palo verticale sono sempre nella finestra principale.

### 5.4 Da provare a mano (versione portatile con le viste attive)

1. Aprire un palo e un micropalo verticale di un progetto reale: forme, stratigrafie, profilo, grafici e verifica come oggi;
   ridimensionare la finestra (impaginazione larga e compatta) e provare «Estendi»/«Riduci» delle tre schede.
2. Modificare campi, strati e stratigrafie (anche «Copia in…»/«Copia da…» e le eliminazioni con conferma): il ricalcolo parte
   dopo mezzo secondo e lo stato passa da «in attesa» a «in corso» a «completato».
3. Grafico della capacità: viste, caselle delle curve, «Tutte»/«Nessuna», rotella e trascinamento, menu «Adatta» e
   «Salva PNG…».
4. «Tabelle e dettagli», «Nq»/«Abachi» e «Risultati JSON».
5. «Invia / carica terreno…» (solo palo): profilo corrente, carica e salva un file `.anthea-terreno.json`, importa da un altro
   foglio del progetto, invia a un nuovo foglio, sostituisci il terreno.
6. «Modello e dati comuni…», i «?» dei campi e F1.
7. Una revisione precedente del progetto (sola lettura): campi e strati bloccati; viste, tabelle, curve e aiuto disponibili.
8. Report Word/PDF ed esportazione del risultato.
9. Micropalo con un risultato: cambiare l'iniezione IGU↔IRS (oggi dà un errore; con la vista nuova ricalcola, F5-01).
10. Aspetti Scuro e Molto scuro.
