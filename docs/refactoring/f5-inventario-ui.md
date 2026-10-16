# F5 — Inventario dell'interfaccia (sola lettura)

Agente `f5-inventario`, 16/10/2026. Albero letto: ramo
`refactoring/integrazione-1016` (punta `71fd52b7`, ANTHEA 1.9.0, `X.Desktop/X.Desktop.csproj:12`), poi unito in `main` con ANTHEA 1.10.0. Nessuna build, nessuna modifica, nessun
commit. Riferimenti `file:riga` relativi alla radice del worktree.

Metodo delle misure:
- «righe» = righe fisiche (`wc -l`); il codice è denso (media circa 77 byte per riga, righe fino a 672 caratteri,
  `SheetEditor.cs`), quindi si riportano anche i byte;
- conteggi fatti con `grep`/script Python conservati fuori dal repository in `supporto/artefatti/refactoring/f5/inventario` (`areas.py`, `logic.py`, `members.py`,
  `baseline.py`, `transitions.py`, `diffjson.py`); i conteggi «per nome» sono approssimati e lo si dice dove valgono;
- nessuna decisione architetturale: dove serve si riportano solo fatti e vincoli già scritti nei documenti.

## 0. Numeri principali

| Grandezza | Valore |
| --- | --- |
| X.Desktop: file sorgente | 158 `.cs` + 4 `.xaml` (solo dizionari di risorse) |
| X.Desktop: righe / byte | 18.462 righe `.cs` (1.427.518 B) + 451 righe `.xaml` (34.047 B) |
| Viste definite in XAML | 0 in X.Desktop (18 XAML solo in `src/ANTHEA.ModelViewer.Wpf`) |
| Classi con `DataContext` assegnato | 3 file su 158 (`BridgePricesView.cs:83`, `ConcreteTimeEffectsView.cs:27`, `SectionActionsView.cs:53`) |
| File con `Binding`/`SetBinding` | 15 |
| Controlli creati da codice (indicatore `new TextBlock/Button/…`, `Ui.Text/Button/…`) | 2.046 occorrenze |
| Letture/scritture dirette del JSON del documento nelle viste | 1.686 letture `.S/.D/.B/.Array("…")`, 296 assegnazioni `["chiave"] =` |
| Residui del censimento F4 (R1–R6) attribuiti a X.Desktop | 2.154 nella `tests/ANTHEA.Architecture.Tests/f4-domain-baseline.json` |
| TypeRef della Release verso motori (`limits.json`) | 0 verso ANTHEA.Calculations e GPC; `AsyncVoid` 2; `DispatcherTimer` 4 |
| Suite del runner nello stadio `ui` | 48 (vedi 3.1) |
| Sorgenti di prova WPF compilati dentro ANTHEA.exe (UiTests) | 71 file, 9.477 righe (`supporto/test/Desktop` 58/8.492, `tests/Desktop` 3/161, `tests/ANTHEA.Desktop.Equivalence` 9/711, `supporto/test/Materiali` 1/113), più 3 condivisi di `tests/Shared` e `supporto/test/Shared` |

## 1. X.Desktop oggi

### 1.1 Progetto, riferimenti e configurazioni

- `X.Desktop/X.Desktop.csproj`: WinExe net8.0-windows, WPF, `AssemblyName` ANTHEA, versione 1.9.0.
  Riferimenti di produzione: `ANTHEA.Application`, `ANTHEA.ModelViewer.Wpf`, `ANTHEA.Wiki`,
  `ANTHEA.DesignTools.Presentation` (righe 18-19); pacchetto `WpfMath` 2.1.0 (riga 41).
- `ANTHEA.Application` referenzia ancora `X.Calculations` e `ANTHEA.Reports`; X.Desktop vede quindi i tipi legacy in
  compilazione (`X.Desktop/CalculationImports.cs:1` = `global using Anthea.Calculations;`). L'architettura vieta però
  qualunque riferimento della Release verso `ANTHEA.Calculations` e GPC (`tests/ANTHEA.Architecture.Tests/Program.cs:124-131`,
  `limits.json`). Resta l'uso di costanti inlined, per esempio `MicropaloOrizzontale.Module` (6 occorrenze).
- Configurazione `UiTests` (`X.Desktop.csproj:27-39`): compila dentro l'exe `tests/Desktop/*.cs`,
  `supporto/test/Desktop/*.cs`, `tests/ANTHEA.Desktop.Equivalence/*.cs`, `supporto/test/Materiali/*.cs`, i condivisi di
  `tests/Shared` e referenzia `tests/ANTHEA.Testing` e `tests/ANTHEA.Legacy`. Simbolo `UITESTS`
  (`Directory.Build.props`), mai usato con `#if` nel codice di produzione.
- `GpcRuntimeOnly` vero fuori da UiTests (`X.Desktop.csproj:6`): in Release il Desktop non compila contro le DLL GPC.
- Nessun `InternalsVisibleTo` su X.Desktop (esiste solo su Application, ModelViewer.Wpf e X.Calculations). Nessun
  analyzer configurato (`Directory.Build.props` lo esclude esplicitamente; i pacchetti di F5 sono solo elencati in
  `docs/refactoring/decisioni.md:449-452`).

### 1.2 Cartelle

| Cartella | Contenuto |
| --- | --- |
| radice | `App.xaml` (132 righe), 3 file di `global using`, `app.manifest` |
| `Services/` | `DesktopServices.cs` (record), `IConfirmationService.cs`, `IMessageService.cs` (42 righe in tutto, F1.4) |
| `Wpf/` | 144 `.cs` in una sola cartella + 2 `.xaml` (`AppearanceControls.xaml` 170, `ProjectStyles.xaml` 87) |
| `Wpf/Materials/` | 8 `.cs` + `Styles.xaml` 62 (ex progetto Materiali, namespace `Materiali`) |
| `Assets/`, `Assets/Wiki` (38 file), `Assets/Materials` | PNG, ICO, SVG incorporati come risorse |
| `Wiki/Licenses` | licenze WpfMath copiate in uscita |

### 1.3 Righe per area

Mappatura per nome di file (`areas.py`); un file appartiene a una sola area.

| Area / modulo | File | Righe | KB | Classi principali | Tecnologia UI | Passo del piano |
| --- | ---: | ---: | ---: | --- | --- | --- |
| Shell (MainWindow, menu, App, servizi, adattamento schermo) | 13 | 888 | 61 | `MainWindow` (parte), `App`, `ErrorLog`, `DisplayAdaptation`, `WpfDesktopServices` | code-behind | F5.1, F5.13 |
| Progetti (albero, condivisione, revisioni, report, modelli) | 12 | 1.680 | 115 | partial `MainWindow` in `Project*.cs`, `RevisionInspection` | code-behind, `ProjectStyles.xaml` | F5.13 |
| Infrastruttura comune (Ui, InputForm, temi, Plot, disegni base) | 14 | 1.706 | 118 | `Ui`, `InputForm`, `Field`, `JsonGrid`, `JsonRow`, `Appearance`, `DrawingView`, `Plot`, `SoilTransferDialog` | helper imperativi | F5.2, F5.3 |
| SheetEditor (contenitore) | 1 | 390 | 28 | `SheetEditor` | code-behind | F5.13 |
| Palo e micropalo verticale | 1 | 306 | 27 | partial `SheetEditor` (`GeoEditor.cs`) | SheetEditor generico: `InputForm` + `JsonGrid` + schede su `Canvas` | F5.6 |
| Palo/micropalo orizzontale | 9 | 959 | 74 | `HorizontalWorkspace`, `HorizontalSoilEditor`, `HorizontalDiagrams`, `ChsDrawing` | workspace su misura con `InputForm`/`JsonGrid` | F5.7 |
| Palificata orizzontale | 2 | 225 | 23 | `HorizontalPileGroupWorkspace`, `PileGroupPlan` | workspace su misura, campi scritti a mano, `ObservableCollection` + `DataGrid` | F5.4 (pilota) |
| Palo elastico e armature del palo | 5 | 764 | 83 | `ElasticPileWorkspace`, `PileReinforcementEditor`, `ElasticPileDrawing`, `PileReinforcementDrawing` | sotto-workspace di `HorizontalWorkspace` | F5.8 |
| Muri e stabilità globale | 21 | 2.146 | 179 | `RetainingWallWorkspace` (12 file partial), `RetainingWallDrawing`, `GlobalStabilityDrawing` | workspace su misura, `InputForm`/`JsonGrid`, disegni OnRender | F5.9 |
| Sezione c.a. | 26 | 3.313 | 262 | `ConcreteWorkspace` (21 file), `ConcreteSectionViewport`, `DomainViewport3D`, `SectionActionsView` | workspace su misura; un'isola MVVM (`SectionActionsView`) | F5.10 |
| Sezione composta da ponte | 25 | 2.526 | 195 | `BridgeWorkspace` (16 file), `BridgeDrawing` (5 file) | workspace su misura; vista staccabile in finestra | F5.11 |
| Bridge Design | 8 | 1.388 | 112 | `BridgeDesignWorkspace`, `BridgeDesignDrawing`, `BridgeOptimizationPlot`, `BridgePricesView` | workspace su misura; un'isola MVVM (`BridgePricesView`) | F5.12 |
| Fondazioni superficiali | 4 | 460 | 34 | `FoundationWorkspace`, `FoundationViews` | workspace su misura | nessun passo esplicito |
| Stratigrafia (modulo del 15/10) | 4 | 517 | 35 | `StratigraphyWorkspace`, `StratigraphyTable`, `StratigraphyImportWindow` | workspace su misura, `JsonGrid` con binding | nessun passo esplicito |
| Materiali (calcestruzzo, acciaio) | 10 | 690 | 45 | `Materiali.MaterialView` (8 file partial), `RebarMaterialView` | due viste su misura, `Styles.xaml` | F5.5 |
| Wiki | 6 | 804 | 55 | `WikiView`, `WikiEditorial`, `WikiBookLayout`, `WikiVector`, `WikiContextHelp` | code-behind, WpfMath | F5.13 (shell) |
| Strumenti (reologia) | 1 | 152 | 13 | `ConcreteTimeEffectsView` | MVVM (ViewModel in `ANTHEA.DesignTools.Presentation`) | già MVVM |
| **Totale** | **162** | **18.914** | **1.462** | | | |

Classi partial più grandi (somma dei file che le dichiarano): `ConcreteWorkspace` 21 file / 2.472 righe / 198 KB;
`MainWindow` 14 file / 2.140 / 147 KB; `BridgeWorkspace` 16 / 1.640 / 136 KB; `RetainingWallWorkspace` 12 / 1.337 /
118 KB; `HorizontalWorkspace` 6 / 811 / 63 KB; `BridgeDesignWorkspace` 4 / 789 / 66 KB; `SheetEditor` 2 / 696 / 55 KB.

Moduli che il piano non assegna a un passo F5.4–F5.12 (`docs/refactoring/piano.md:530-531`): fondazioni superficiali,
stratigrafia (aggiunta il 15/10, dopo la stesura del piano), Wiki e progetti (rientrano solo nella shell F5.13). Il
micropalo verticale condivide `GeoEditor.cs` con il palo verticale; il micropalo orizzontale condivide
`HorizontalWorkspace`. Il modulo Modelli (`src/ANTHEA.ModelViewer.*`) è già MVVM e ospitato dalla shell.

Moduli del catalogo: 12 in `X.Calculations/ModuleCatalog.cs:17-28` più la stratigrafia aggiunta in
`src/ANTHEA.Application/Modules/ModuleDirectory.cs:11-13`.

### 1.4 Tecnologia UI per modulo (dettaglio)

- **Comune a tutti**: nessun XAML di vista, nessun ViewModel. Le viste costruiscono i controlli in C# con gli helper di
  `Wpf/Ui.cs` e scrivono direttamente nel `JsonObject` del foglio:
  - `Field` (record di descrizione dei campi, `Ui.cs:138`) e `InputForm` (`Ui.cs:174-348`): una riga per campo; a ogni
    `TextChanged` il testo grezzo va nel JSON come stringa (`Ui.cs:219`, `Store` a 284, `J.Node` serializza la stringa);
    presentazione numerica separata dal testo grezzo (`Ui.cs:222-235`, `NumericPresentation`);
  - `JsonRow`/`JsonRows`/`JsonGrid` (`Ui.cs:351-441`): adattatore `INotifyPropertyChanged` sopra un `JsonObject`, con
    indicizzatore `this[string]` e colonne create dai `Field`;
  - grafici e disegni: `DrawingView` astratta e `Plot` (`Drawings.cs:13,45`), 19 file con `OnRender`; 3D solo in
    `DomainViewport3D` (`ConcreteViewports.cs:271`, WPF `Viewport3D` nativo);
  - impaginazione delle schede manuale su `Canvas` (`SheetEditor.LayoutCards`, `SheetEditor.cs:174-235`;
    `HorizontalWorkspace.LayoutCards`).
- **Palo/micropalo verticale**: il ramo «generico» di `SheetEditor` (`SheetEditor.cs:141-151` più `GeoEditor.cs`):
  tre `InputForm` (generali, efficienza, normativa), `JsonGrid` per gli strati per sondaggio, `Plot` e
  `StratigraphyDrawing`, schede espandibili; ricalcolo automatico a 450 ms (`SheetEditor.cs:248`).
- **Palo/micropalo orizzontale**: `HorizontalWorkspace` con `InputForm`, `JsonGrid`, schede su `Canvas`, sotto-vista
  elastica `ElasticPileWorkspace` creata a `HorizontalWorkspace.cs:148`.
- **Palificata**: vedi sezione 4.
- **Palo elastico**: `ElasticPileWorkspace` + `PileReinforcementEditor` (`ContentControl`); il verificatore dei tratti apre
  in una finestra modale un `ConcreteWorkspace` completo sulla scheda c.a. del tratto (`PileReinforcementEditor.cs:146-160`).
- **Muri**: `RetainingWallWorkspace` (12 file partial), finestra non modale dei parametri con uno scheduler per finestra
  (`RetainingWallWorkspace.Parameters.cs:12-18`), stabilità globale, distinta ferri con PDF (`DrawingPdf`), richiesta di
  apertura di una sezione c.a. (`RetainingWallWorkspace.cs:24,116`).
- **Sezione c.a.**: `ConcreteWorkspace` (21 file), schede e pannelli, viewport 2D/3D, tabelle filtrabili, report breve,
  isola MVVM per le sollecitazioni (`SectionActionsView` + `SectionActionsViewModel` in Application).
- **Sezione composta**: `BridgeWorkspace` (16 file), disegni `BridgeDrawing`, curve di risposta, vista staccabile
  (`BridgeDetachedView.cs:13-50`).
- **Bridge Design**: `BridgeDesignWorkspace` (4 file), ottimizzazione con progresso e annullamento, prezzi in MVVM
  (`BridgePricesView` + `PriceCatalogueViewModel`).
- **Fondazioni**: `FoundationWorkspace` (2 file) con calcolo sincrono (`FoundationWorkspace.cs:139-144`); i tipi non WPF
  stanno in `src/ANTHEA.FoundationWorkspace` (nome fuorviante: non contiene WPF).
- **Stratigrafia**: `StratigraphyWorkspace`, tabella con binding a `JsonRow` (`StratigraphyTable.cs:18-21`), importazione
  PDF via OpenAI in `StratigraphyImportWindow` (`HttpClient` creato nella finestra, `StratigraphyImportWindow.cs:62`).
- **Materiali**: `Materiali.MaterialView` (stato catturato/restaurato come JSON, `SheetEditor.cs:125,163`) e
  `RebarMaterialView` (calcolo sincrono, `RebarMaterialView.cs:101`); stili propri caricati da codice
  (`Materials/MaterialView.cs:42`).
- **Wiki**: `WikiView` costruita da codice; `WikiEditorial` traduce in WPF i blocchi tipizzati di `ANTHEA.Wiki`
  (`WikiMarkup`), formule con `WpfMath.Controls.FormulaControl` (`WikiEditorial.cs:52,75`).
- **Modelli**: `ANTHEA.ModelViewer.Wpf` (18 XAML, 46 `.cs`, 2.521 righe `.cs` + 1.360 XAML) sopra
  `ANTHEA.ModelViewer.Presentation` (36 file, 3.453 righe, `ModelViewerViewModel` con 267 `[ObservableProperty]` e 111
  `[RelayCommand]`), ospitato da `MainWindow.ShowContainerModel` (`ProjectModels.cs:81-126`).

### 1.5 MainWindow

`public sealed partial class MainWindow : Window`, 14 file, 2.140 righe. Responsabilità, con riferimenti:

1. **Composizione e servizi**: costruttore con fabbrica di `DesktopServices` (`MainWindow.cs:40-59`); stile, dimensioni,
   `DisplayAdaptation.Attach`, `Appearance.Watch`; chiusura confermata tramite `IConfirmationService.ConfirmClose`
   (riga 56); `Safe` (riga 60) passa gli errori a `IMessageService`.
2. **Menu e scorciatoie**: `BuildMenu` (righe 62-97): 10 voci «Nuovo …», Apri/Salva/Salva con nome (Ctrl+O/S/Shift+S),
   esportazione del foglio, report Word, report short, JSON, menu Aspetto (tre temi), Strumenti → Reologia (finestra non
   modale, riga 93).
3. **Shell e navigazione**: `BuildShell` (98-121: barra laterale Home/Progetti/Modelli/Moduli singoli/Wiki, barra del
   foglio con ritorno, conferma dati condivisi, Wiki del modulo, approfondimenti); `SelectNavigation` (151-165),
   `ShowHome` (166-183), `ShowModules` (184-211, catalogo costruito da `ModuleDirectory.All`).
4. **Stato unico documento/foglio**: campi `document`, `currentSheet`, `path`, `dirty`, `editor` (righe 22-26).
   `Commit` (219-228) copia `editor.Data` nel solo `currentSheet` e lancia il controllo dei dati condivisi;
   `LoadSheetEditor` (236-256) distrugge l'editor precedente e ne crea uno nuovo (`editor?.Dispose(); currentSheet =
   sheet;` riga 238, `new SheetEditor` riga 239), collega 6 eventi/callback (239-248) e decide se avvolgere l'editor in
   un viewport scalato con un elenco di moduli scritto a mano (251-252).
   **Verifica di `MainWindow.cs:220-229` citato dal piano**: confermato con lo scarto di una riga. Oggi 219-228 è
   `Commit()`, 229 l'inizio di `ShowSheet`. La prova più diretta del «un foglio, un documento» sono i campi 22-26 e la
   sostituzione dell'editor a 238-239. Il documento si sostituisce in otto punti: `NewCalculation` (312), `NewProjects`
   (337), `LoadFile` (395), `OpenWikiModule` (`MainWindow.Wiki.cs:67`), annulla/ripristina (`ProjectWorkspace.cs:57`) e
   navigazione fra revisioni (`ProjectRevisionNavigation.cs:105,139,161`, con documento di lavoro separato
   `revisionWorkingDocument`, righe 12-17).
5. **File**: `Open` (390, `OpenFileDialog`), `LoadFile` (391-397, `DocumentService.Open`), `Save` (398-404,
   `DocumentService.Save`, `MessageBox` diretto a 403), `ExportSheet` (405-409), `ExportJson` (410-415).
6. **Report**: `ExportReport` (416-457) contiene la logica per modulo: nomi dei file, controlli `HasResults`/`Busy`,
   finestra «Contenuti del report Word» con le sezioni di CA, ponte e pali verticali (449-456). Report short in
   `MainWindow.ShortReport.cs` (29 righe), report di sezione in `ProjectReport.cs` (71 righe).
7. **Progetti** (partial `Project*.cs`, 1.680 righe): albero con drag&drop e rinomina (`ProjectHierarchy.cs`, timer del
   doppio clic a riga 16/30), pagina del progetto e catalogo (`ProjectPresentation.cs`, `ProjectWorkspace.cs`),
   annulla/ripristina su istantanee del documento, massimo 30 (`ProjectWorkspace.cs:29,38-51`), confronto e dati condivisi
   (`ProjectSharing.cs`), anteprima degli spostamenti (`ProjectMovePreview.cs`), revisioni (`ProjectRevisionNavigation.cs`),
   stato del copriferro (`ProjectCoverStatus.cs`), modelli (`ProjectModels.cs`).
8. **Wiki**: `ShowWiki`, `ShowModuleWikiTopics`, esempi della Wiki che aprono un modulo (`MainWindow.Wiki.cs`, 96 righe).
   `WikiExamples` è una classe annidata in `MainWindow` usata anche da `WikiEditorial.cs:127`.
9. **Seconda finestra**: `OpenModuleCopy` per un foglio autonomo crea una nuova `MainWindow` con un documento proprio
   (`MainWindow.RetainingWall.cs:21-23`). È l'unico caso attuale di più documenti aperti insieme.

### 1.6 SheetEditor

`internal sealed partial class SheetEditor : UserControl, IDisposable` (`SheetEditor.cs` 390 righe + `GeoEditor.cs` 306).
Costruito in produzione solo da `MainWindow.cs:239`; nei test in 16 punti (`F3EquivalenceCapture`, `F3ReportChapterChecks`,
`F3FigureChecks`, `F3SchedulingChecks`, `ReportAppearanceChecks` ×6 e altre smoke).

1. **Smistamento per modulo**: il costruttore (79-152) crea il workspace del modulo (stratigrafia 92-96, fondazioni 97-101,
   palificata 102, muri 103-110, Bridge Design 111-114, ponte 115-118, orizzontale 119-122, calcestruzzo 123-127,
   acciaio 128-131, c.a. 132-140); altrimenti costruisce da sé la UI dei pali verticali (141-151).
2. **Facciata verso la shell e i test**: `Module`, `Data` (copia profonda dell'ingresso, riga 83), `Result` (catena di
   operatori su tutti i workspace, riga 35), `HasResults` (36), `Busy` (37), eventi `Modified`,
   `ConcreteSectionRequested`, `ReportRequested`, `ModuleCopyRequested`, `SoilReplacementRequested`, callback
   `SoilSources` (38-45).
3. **Comandi delegati**: `Commit` (163), `CalculateAsync` (250-263), `Dispose` (162), `ExportResult` (336-343),
   `ExportReport` (344-368), `BuildReport`/`BuildReportDocument` (373-387, sceglie il generatore per modulo),
   `MaterialReportContent` (369-372), `BuildShortReport` (41-42), trasferimento del terreno (153-159), sola lettura
   delle revisioni (160-161).
4. **UI propria dei pali verticali**: schede (164-235), invalidazione (236-243), scheduler a 450 ms (244-289), tabelle
   (291-335), salvataggio PNG (388-389).

### 1.7 Stato statico o globale nella UI

- `Appearance` (`Wpf/Appearance.cs`): `Current` e l'evento statico `Changed` (19-20), `initialized` (34), cache statiche
  dei pennelli (22-25), gestori di classe globali su `Loaded` e `SizeChanged` per tutti i `FrameworkElement`
  (101-108); `Set` ripassa tutte le finestre dell'applicazione (111-124); `Document` cambia temporaneamente la tavolozza
  globale per le immagini dei report (132 e seguenti). Preferenza in `%LOCALAPPDATA%\ANTHEA\appearance.json` (21).
- `ErrorLog.attached` (`ErrorLog.cs:15`).
- `Application.Current.Resources` usato come registro dei pennelli (`Appearance.Initialize`, 77-110) e 17 `FindResource`.
- `Materials/ExposureSelector.cs:14,36`: dizionari statici di testi delle esposizioni.
- Nei livelli sotto la UI: `ModuleServices.Services` (dizionario statico di servizi per modulo,
  `src/ANTHEA.Application/Modules/ModuleServices.cs:7-8`); `ConditionalWeakTable` statiche in `BridgeResultView`,
  `BridgeDesignModel`, `WallModel`; `WikiCatalog.Aliases`; array `public static readonly string[]` modificabili in
  `X.Calculations` (per esempio `HorizontalPileGroup.LayoutNames`, `BridgeSection.*`, `RetainingWall.*`). La decisione E5
  del 15/10 rimanda questo stato statico (ponte, muri, pali, fondazioni, Wiki) a F5.13
  (`docs/refactoring/decisioni.md:1299`, `piano.md:256`).

### 1.8 Servizi chiamati direttamente dalla UI

| Servizio | Occorrenze | File | Note |
| --- | ---: | ---: | --- |
| `ANTHEA.Application` (namespace citato) | — | 96 | moduli, sessioni, presentazioni, progetti |
| `ANTHEA.Reports` (generatori, `ReportFiles`, `DocxWriter`) | — | 11 | `ReportFiles.Write` 5 occ. in 4 file; `DocxWriter` 6 occ. in 5 file |
| `Archivio.ScriviAtomico` (scrittura file) | 20 | 15 | PNG, CSV, JSON, DOCX/PDF dalle viste |
| `DocumentService` | 3 | 1 | solo `MainWindow.cs` |
| `SaveFileDialog` / `OpenFileDialog` | 26 / 7 | 17 / 7 | nessuna astrazione, salvo `IModelViewerServices` nei Modelli |
| `MessageBox.Show` | 41 | 21 | `IMessageService` copre solo `Safe`; `ConfirmDiscard` (`MainWindow.cs:308`) e `Delete` (382) usano `MessageBox` |
| `ShowDialog()` (modali) / `Ui.Dialog` | 72 / 32 | 36 / 23 | `Ui.Dialog` crea una `Window` con `Owner` (`Ui.cs:68-75`) |
| `IConfirmationService` | 3 domande | — | dati condivisi, spostamento, chiusura (`Services/IConfirmationService.cs`) |
| Appunti (`Clipboard`) | 7 | 4 | CA, proprietà, Wiki |
| `Process.Start` (link esterni) | 2 | 2 | `WikiEditorial.cs:62`, `HorizontalDiagrams.cs:161` |
| `HttpClient` | 1 | 1 | `StratigraphyImportWindow.cs:62` (OpenAI) |
| `RecalculationScheduler` (campi) | 12 | 11 | più uno per finestra dei parametri dei muri |
| `Task.Run` verso motori/servizi | 15 | 13 | elenco in 1.11 |
| `Dispatcher.Invoke/BeginInvoke/InvokeAsync/Yield` | 16 | 12 | |
| `CancellationTokenSource` | 8 | 5 | ottimizzazione Bridge Design, curve del ponte, armature CA, report di progetto, importazione AI |
| `IProgress`/`Progress<T>` | 4 | 4 | |
| `IFigureRenderer` (implementato dalla UI) | 1 classe | 5 file | `ModuleFigureRenderer` disegna con controlli WPF per i report, anche di progetto (`ProjectReport.cs:68-70`) |
| Word | 0 | 0 | Word non è più usato: DOCX e PDF da `ANTHEA.Reports` (F4-46) |

### 1.9 Temi e risorse

- Tre aspetti (`AppAppearance` Light, Dark, VeryDark, `Appearance.cs:14`). Non sono token: i colori sono scritti nel
  codice e la tavolozza li trasforma per ruolo (sfondo, primo piano, bordo, selezione, calcolato, serie, campione,
  disattivato) con `Appearance.Background("#…")`, `Foreground`, ecc.; i controlli aggiunti dopo il caricamento sono
  raggiunti dai gestori di classe globali.
- Nel C# della UI: 365 letterali esadecimali (243 distinti), 160 `Brushes.*`, 29 `Color.FromRgb/FromArgb`, 423 chiamate
  `Ui.Text(…)` con la dimensione del carattere scritta nel codice.
- XAML: `App.xaml` (stili impliciti con trigger su `local:Appearance.Dark`), `AppearanceControls.xaml` (14 chiavi, modelli
  dei controlli scuri), `ProjectStyles.xaml` (6 chiavi), `Materials/Styles.xaml` (stili impliciti, caricati da codice);
  71 `DynamicResource`.
- Immagini di report e stampe sempre chiare (`Appearance.Document`, `Ui.DocumentSnapshot`, `Ui.cs:106-111`).
- Verifiche: `--check-appearance` (nel runner), `--check-contrast` (56 viste in tre aspetti, `ContrastAudit.cs`, 1.937
  righe) e `--check-report-appearance-offscreen`, questi due fuori dal runner. Esito e seguiti in `decisioni.md`, sezione
  «Tema scuro» (righe 266-296): 719 difetti di contrasto della sola Chiara lasciati, testo in `OnRender` non misurato.

### 1.10 Gestione degli errori

- `App.OnStartup` (`Wpf/App.cs:7-29`): font PDF, `ErrorLog.Attach` (task non osservati e eccezioni di altri thread),
  `DispatcherUnhandledException` che scrive nel registro e mostra un `MessageBox`.
- `MainWindow.Safe` (riga 60) avvolge menu, navigazione e comandi di progetto: 71 occorrenze in 12 file (shell, progetti,
  Wiki, guida contestuale).
- Nelle viste dei moduli: 76 `catch` in 45 file; gli errori diventano testo di stato («Dati da completare: …»), avvisi o
  `MessageBox` diretti. Il messaggio dipende dalla vista, non da un servizio.
- `RecalculationScheduler` restituisce l'errore come valore (`Error`) e pubblica solo la revisione corrente
  (`src/ANTHEA.Application/Scheduling/RecalculationScheduler.cs:36-37, 45-52`).
- Nessun `#if UITESTS` in produzione; i test sostituiscono i servizi con `TestServices` (`supporto/test/Desktop/TestServices.cs`).

### 1.11 Operazioni lunghe, thread e annullamento

- Schedulatore comune senza WPF (`RecalculationScheduler`): ultima richiesta vince, annullamento cooperativo, esecuzione
  seriale, pubblicazione solo della revisione corrente. Ritardi conservati da F3: 450 ms pali verticali
  (`SheetEditor.cs:248`), 180 ms palificata (`HorizontalPileGroupWorkspace.cs:155`), 450/300 ms orizzontale ed elastico,
  500 e 450 ms ponte e muri, 450 ms Bridge Design (`docs/refactoring/f3-strato-applicativo.md:70-122`).
- `Task.Run` verso i motori in: `SheetEditor.cs:273`, `HorizontalWorkspace.cs:258`, `BridgeWorkspace.cs:261`,
  `BridgeResponse.cs:90`, `BridgeDesignWorkspace.cs:323`, `BridgeDesignOptimization.cs:120`, `ConcreteCurvature.cs:41`,
  `ConcreteDesign.cs:130`, `ConcreteQuickResistance.cs:38`, `ConcreteRefinements.cs:69`, `RetainingWallWorkspace.cs:69`,
  `RetainingWallWorkspace.Advanced.cs:79`, `RetainingWallWorkspace.GlobalStability.cs:139`,
  `MainWindow.ShortReport.cs:23` (scrittura DOCX/PDF).
- Calcoli sincroni sul thread dell'interfaccia: palificata (`HorizontalPileGroupWorkspace.cs:171`), fondazioni
  (`FoundationWorkspace.cs:144`), acciaio (`RebarMaterialView.cs:101`); report dei singoli fogli
  (`SheetEditor.ExportReport`, scrittura sincrona).
- Report di progetto: finestra modale con annullamento e `async void Run` (`ProjectReport.cs:44-60`, `Run` a 53); il servizio
  `ProjectReportService.WriteSnapshotAsync` è in Application.
- `async void` dichiarati: 2 (`ConcreteRefinements.cs:60`, `ProjectReport.cs:53`), pari al limite. Lambda asincrone
  passate come `Action` (stessa semantica di `async void`, non contate dalla regola): 8 in 7 file, per esempio
  `SheetEditor.cs:90`. Scarti `_ = …Async()`: 15.
- `DispatcherTimer`: 3 timer, 4 occorrenze testuali (limite 4): doppio clic nell'albero (`ProjectHierarchy.cs:16,30`),
  ricerca e salvataggio dei progressi della Wiki (`WikiView.cs:22-23`). Nessun timer di calcolo.

## 2. Cosa è già indipendente dalla UI

### 2.1 Librerie senza WPF del repository

| Progetto | File | Righe | Contenuto utile per i ViewModel |
| --- | ---: | ---: | --- |
| `src/ANTHEA.Application` | 97 | 9.157 | servizi dei moduli, sessioni, preparazioni, presentazioni, progetti, report, scheduler |
| `src/ANTHEA.Reports` | 30 | 3.015 | generatori DOCX/PDF, `DocxWriter`, `ReportFiles` |
| `src/ANTHEA.Wiki` | 2 (+ contenuti) | 206 | `WikiCatalog` (catalogo, ricerca, alias), `WikiMarkup` (blocchi tipizzati) |
| `src/ANTHEA.Serialization` | 4 | 198 | `J` (accesso al JSON), `EngineeringFormat` |
| `src/ANTHEA.FoundationWorkspace` | 3 | 334 | documento, materiali e presentazione delle fondazioni |
| `src/ANTHEA.DesignTools.Presentation` | 1 | 174 | `ConcreteTimeEffectsViewModel` (CommunityToolkit.Mvvm) |
| `src/ANTHEA.ModelViewer.Presentation` | 36 | 3.453 | `ModelViewerViewModel` e servizi astratti `IModelViewerServices` |

`ANTHEA.Application` per cartella: Bridge 925 righe, BridgeDesign 1.008, Concrete 1.823, Documents 226, Materials 392,
Modules 577, Presentation 943, Projects 1.613, Reporting 175, Scheduling 83, Stratigraphy 399, Walls 992.
`ANTHEA.Application` referenzia già `CommunityToolkit.Mvvm`.

### 2.2 Tipi riusabili dai ViewModel, per modulo

- **Infrastruttura**: `IModuleService`/`ModuleCalculation`/`ModuleServices`/`ModuleCalculationSession` (cache LRU per
  documento, 4 esiti, 8 MiB), `RecalculationScheduler`/`ScheduledCalculation<T>`, `ModuleDirectory`/`ModuleInfo`
  (catalogo per la navigazione), `DocumentService`, `Archivio`, `ModuleSheetPreparation` (migrazioni pigre condivise fra
  vista e report), `StructuralQueries` (curve, resistenze, armature), `PileProgress`, `PresentationTable`, `TableValues`,
  `VerificationDetail(s)`, `VerificationSummary`, `UtilizationColors`, `CalculationHelp`.
- **Pali verticali**: `VerticalPilePreview`, `VerticalPileCharts` (serie, filtri, abachi; `PlotPresentation` in UI
  converte solo colori e stili), `PileInputCatalog`.
- **Orizzontali/elastico/palificata**: `HorizontalPreview`, `HorizontalSheetPresentation`, `HorizontalSoilPresentation`,
  `HorizontalPileAnalysis`, `ElasticPileAnalysisSession`, `ElasticPileEditor`, `PileReinforcementOperations`,
  `PileSegmentDesignSession`, `HorizontalPileEditor`/`HorizontalPileGroupEditor`.
- **Muri**: `WallModel` (proiezione del risultato), `WallSheetPresentation`, `WallParameterTable`, `WallMaterialPreview`,
  `WallReportPresentation`, `WallExports`.
- **Sezione c.a.**: `ConcreteAnalysis(Session)`, `ConcretePropertySession`, `ConcreteInspectionSession`,
  `ConcreteSheetPreparation`, `ConcreteEditor`, `ConcreteResults` (domini, tensioni, dettagli), `ConcreteDomainChart`,
  `ConcreteStrainDiagram`, `ConcreteDetailingPresentation`, `ConcreteVerificationSummaries`, `ConcreteResultExport`,
  `ConcreteGeometry`, `NeutralAxisPresentation`, `SectionActionsViewModel` (già MVVM).
- **Ponte e Bridge Design**: `BridgeEditor`, `BridgeResultView`, `BridgePresentationData`, `BridgeSheetPresentation`,
  `BridgePropertyPresentation`, `BridgePreview`; `BridgeDesignModel`, `BridgeDesignAnalysis`, `PriceCatalogue`,
  `PriceCatalogueViewModel` (già MVVM).
- **Materiali**: `MaterialSheetInput`, `MaterialSheetPresentation`, `MaterialInputCatalog`, `MaterialDiagram`,
  `MaterialEditorPreview`, `MaterialReportChapter`, `RebarEditor`.
- **Progetti e report**: `ProjectDocuments`, `ProjectSharedData` (9 file partial: campi condivisi, confronto, gerarchia,
  terreni), `ProjectValidation`, `ProjectRevisions`, `ProjectComparison`, `ProjectReportPlan`, `ProjectReportService`,
  `ModuleReportService` + `IFigureRenderer` (capitoli autonomi dei 12 moduli, `chiusura F3.5b.2`), `SoilProfileTransfer`.
- **Stratigrafia**: `StratigraphyDocument`, `StratigraphyTransfer`, `StratigraphyAiImporter(HttpClient)`.

### 2.3 Precedenti MVVM già presenti

- Modelli: ViewModel senza WPF + servizi astratti (`IModelViewerServices`, `ModelViewerViewModel.cs:13,46-49`) con
  implementazione WPF (`src/ANTHEA.ModelViewer.Wpf/WpfModelViewerServices.cs`); controllo ospitato dalla shell con
  callback verso il documento (`ProjectModels.cs:109-122`).
- Tre isole dentro X.Desktop, viste costruite da codice con binding a ViewModel del toolkit: `ConcreteTimeEffectsView`,
  `SectionActionsView`, `BridgePricesView` (tutte e tre in finestre o dialoghi).
- Servizi dialogo/messaggi senza WPF nelle firme: `IConfirmationService`, `IMessageService`, `DesktopServices`
  (record, commento «F5 composes them with DI», `Services/DesktopServices.cs:3-4`).

### 2.4 Cosa resta solo nella UI

- Il «modello» dei form è il `JsonObject` del documento: `Field`, `InputForm`, `JsonRow`, `JsonGrid` stanno in `Ui.cs`
  e dipendono da WPF. Ogni modulo ha la propria regola di scrittura: `InputForm` salva il testo grezzo come stringa, la
  palificata converte in numero quando possibile (`ModuleSheetPreparation.NumericText`), alcuni campi vuoti vengono tolti
  (`HorizontalPileGroupWorkspace.cs:64`). Il formato salvato fa parte dell'equivalenza F3 (`opened/visited/calculated/
  exported/reopened.json`).
- Disegni e grafici: `Plot`, `DrawingView` e 19 file `OnRender`; sono anche il renderer delle figure dei report
  (`ModuleFigureRenderer`, `IFigureRenderer`). Nel censimento F4 la maggior parte dei 2.154 residui della UI è nei disegni:
  `BridgeDesignDrawing.cs` 224, `PileReinforcementDrawing.cs` 169, `FoundationViews.cs` 145, `ConcreteViewports.cs`
  132, `Drawings.cs` 118, `ElasticPileDrawing.cs` 108, `RetainingWallDrawing.cs` 97 (per regola: R2 1.335, R4 619, R6
  140, R1 60).
- Impaginazione, sola lettura delle revisioni (`RevisionInspection.Protect` cammina l'albero visuale e rende i controlli
  di sola lettura, `RevisionInspection.cs:18`), adattamento allo schermo (`DisplayAdaptation`).

### 2.5 Logica rimasta nel code-behind: file più grandi

Indicatori da `logic.py` (scritture `["k"] =` nel JSON, letture `.S/.D/.B/.Array`, `if`, LINQ) e residui F4 del file.

| File | Righe | KB | Scritture JSON | Letture JSON | `if` | LINQ | Residui F4 | Contenuto non grafico prevalente |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| `ConcreteViewports.cs` | 554 | 42,9 | 0 | 11 | 81 | 64 | 132 | proiezioni, clipping 2D/3D, scale |
| `BridgeWorkspace.cs` | 414 | 37,7 | 3 | 31 | 30 | 33 | — | stato delle fasi, selezioni, ricalcolo |
| `MainWindow.cs` | 458 | 36,7 | 3 | 30 | 70 | 16 | — | navigazione, documento, report per modulo |
| `BridgeDesignWorkspace.cs` | 476 | 36,2 | 30 | 15 | 58 | 18 | 20 | editing del documento, tabelle, esportazioni |
| `PileReinforcementEditor.cs` | 233 | 31,4 | 10 | 129 | 38 | 16 | 40 | tratti, letture del risultato, dialoghi |
| `ConcreteWorkspace.cs` | 360 | 30,9 | 5 | 24 | 37 | 11 | — | orchestrazione delle analisi |
| `HorizontalWorkspace.cs` | 324 | 29,4 | — | — | — | — | 33 | layout, analisi, elastico |
| `Ui.cs` | 441 | 29,2 | 0 | 1 | 58 | 6 | — | form e griglie generiche |
| `SheetEditor.cs` | 390 | 28,2 | 2 | 8 | 69 | 13 | 27 | smistamento, layout, pali verticali |
| `GeoEditor.cs` | 306 | 27,0 | 4 | 27 | 47 | 13 | — | sondaggi, curve, verifiche dei pali |
| `ConcreteDomains.cs` | 314 | 25,2 | 5 | 36 | 50 | 14 | 6 | domini, filtri |
| `RetainingWallWorkspace.Results.cs` | 137 | 13,9 | 14 | 32 | 21 | 10 | 8 | applicazione dei risultati al documento |
| `Materials/ExposureSelector.cs` | 98 | 7,8 | 36 | 0 | 3 | 6 | — | stato delle esposizioni, testi statici |
| `ElasticPileWorkspace.cs` | 139 | 18,0 | 2 | 93 | 22 | 8 | 60 | letture del risultato elastico |
| `HorizontalPileGroupWorkspace.cs` | 188 | 20,3 | 6 | 30 | 21 | 7 | 8 | vedi sezione 4 |

(«—» = non tra i primi 40 dell'indicatore o nessun residuo F4 per il file.) Totali UI: 296 scritture e 1.686 letture del
JSON, 1.902 `if`, 998 chiamate LINQ, 373 iscrizioni a eventi.

## 3. Test che esercitano la UI

### 3.1 Suite del runner (`build/ci.ps1`, stadio `ui`)

48 suite, eseguite nel profilo `full` (`AGENTS.md`: quasi un'ora di desktop occupato):
- 23 smoke `--smoke-*` (`build/ci.ps1:311-317`): smoke (pali verticali e orizzontali), bridge, bridge-curves,
  bridge-design, bridge-predalle, bridge-prices, bridge-traffic, ca-extensions, ca-features, display, global-stability,
  hierarchy, horizontal, material-report, materials, neutral-axis, project-report, projects, project-workspace,
  retaining-wall, section-actions, sharing, steel;
- 4 check fuori schermo o a schermo (318-320): appearance, global-guidance-offscreen, wall-advanced-offscreen,
  wall-materials-offscreen; più `check-error-log-offscreen` (321);
- 5 suite F3 (322-327): `f3-report-chapters`, `f3-figures`, `f3-concrete-figures`, `f3-scheduling`, `f3-equivalence`;
  più `check-wiki-offscreen` (328);
- progetti esterni contro X.Desktop: `HorizontalPileGroup.Checks`, `ElasticPile.UiChecks` (+2 varianti),
  `ConcreteShort.UiChecks` (329-335), `ui/check-concrete-design` (337), `FoundationWorkspace.UiChecks`,
  `ConcreteTimeEffects.UiChecks`, `ConcreteInput.UiChecks`, `ModelViewer.UiChecks` e quattro varianti fra cui
  `Stratigraphy.UiChecks` (138-154).

Comandi gestiti da `AppHarness.cs` ma fuori dal runner: `--check-contrast`, `--check-report-appearance-offscreen`,
`--capture-validation`, le acquisizioni `--check-f3-*-capture`. `build/known-failures.json` è vuoto.

### 3.2 Come le prove raggiungono WPF

1. **Partial class compilate nell'exe (UiTests)**. Le prove dichiarano `partial class MainWindow` (41 file) e
   `partial class SheetEditor` (10 file), oltre a partial di `ConcreteWorkspace` (12), `HorizontalWorkspace` (3),
   `BridgeWorkspace` (3), `RetainingWallWorkspace` (2), `BridgeDesignWorkspace` (2), `WikiView` (1). Accedono quindi ai
   membri privati senza riflessione. Membri di produzione nominati dalle prove (conteggio per nome, approssimato per
   eccesso): `MainWindow` 100 su 190, `SheetEditor` 47 su 100, `ConcreteWorkspace` 149 su 257, `HorizontalWorkspace` 35
   su 71. Esempi: le prove assegnano direttamente `document`, `dirty`, `currentSheet` e chiamano `ShowSheet` (circa 24 file;
   `tests/Desktop/BridgeTrafficSmokeChecks.cs:19`); leggono `editor.materials`, `editor.bridge`, `editor.retainingWall`,
   `editor.generalForm`. `--check-wiki-offscreen` usa 16 membri privati di `MainWindow`
   (`docs/refactoring/progetti-di-test.md`, F1.5).
2. **Riflessione dai progetti esterni** (referenziano `X.Desktop.csproj` Release): `ElasticPile.UiChecks` 83 chiamate
   (`HorizontalWorkspace`, `PileReinforcementEditor`, `ConcreteWorkspace`, campi `sectionFields`, `status`,
   `calculationProgress`…), `ModelViewer.UiChecks` 49 (anche `SheetEditor`, `StratigraphyImportWindow`,
   `SoilTransferDialog`, `Appearance`), `FoundationWorkspace.Checks` 14, `ConcreteTimeEffects.Checks` 12,
   `ConcreteInput.Checks` 7, `HorizontalPileGroup.Checks` 4, `ConcreteShort.UiChecks` 2. Nomi di tipi e campi sono stringhe
   (`GetType("X.Desktop.…")`, `GetField("comparison")`).
3. **Navigazione dell'albero visuale e dei dialoghi**: 173 `Ui.Descendants`/`Descendants<>`, 73 click simulati con
   `RaiseEvent`, 26 confronti sul testo dei pulsanti, 30 ricerche di finestre per titolo in 16 file
   (`Application.Current.Windows…Title.StartsWith(…)`), 202 istantanee PNG.
4. **Risorse**: i progetti esterni caricano gli stili leggendo `X.Desktop/App.xaml` dalla cartella corrente
   (`tests/ANTHEA.Desktop.Testing/WpfTestResources.cs:10`, copia in `HorizontalPileGroup.Checks/Program.cs:97`): vanno
   lanciati dalla radice.
5. **Servizi di prova**: `MainWindow(TestServices.Create)` sostituisce conferme e messaggi (risposte programmate,
   rilancio dell'eccezione), `supporto/test/Desktop/TestServices.cs`.

### 3.3 `ui/f3-equivalence`

- Codice: `tests/ANTHEA.Desktop.Equivalence/F3EquivalenceCapture.cs`. Per ognuna delle 36 fixture
  (`tests/fixtures/f3/inputs`): `new SheetEditor(module, data)` → `Commit` → `opened.json`; visita (espande tutti gli
  `Expander`, scorre tutte le schede dei `TabControl`, righe 117-132) → `visited.json`; `CalculateAsync` →
  `calculated-data.json`, `result.json`; `ExportReport` (+ report breve per `str_palo`) → testo e struttura del DOCX;
  `exported-data.json`, `status.json`; nuovo `SheetEditor` sui dati esportati → `reopened.json` (righe 30-71). Poi tutti
  gli articoli Wiki resi come testo da `WikiEditorial.Render` (71 nel riferimento, 73 oggi: transizione F4-51), 30
  ricerche, report di progetto tramite i metodi privati statici `MainWindow.SectionReportSnapshot`/`GenerateSectionReport`
  (righe 154-175).
- Il riferimento `tests/fixtures/f3/expected` (462 file) non si aggiorna; le differenze di F4 e dei bug del 15-16/10 sono
  ammesse una per una in `transitions-f4.json` (22.126 voci; fallisce sia una differenza non elencata sia una voce che
  non si presenta; `tests/fixtures/f3/README.md`).
- Le voci ammesse sono concentrate su progetto (4.994), muri (14.441 sui 4 fixture), `geo_palo_orizzontale/single-soil-
  elastic` (1.117), Bridge Design (994), `str_palo` (555). **Palificata: 0 voci**: per quel modulo il confronto è esatto
  rispetto alla cattura di F3.
- Dipendenze dell'equivalenza dall'attuale UI: il costruttore e i metodi di `SheetEditor`, il fatto che la visita sia
  fatta di `Expander`/`TabControl`, i metodi privati di `MainWindow` per il report di progetto.

### 3.4 Altre prove di equivalenza

- `ui/f3-report-chapters` (`F3ReportChapterChecks.cs:20-60`): per ogni fixture confronta dati preparati
  (`JsonNode.DeepEquals(editor.Data, sheet["dati"])`) e DOCX della vista con il capitolo autonomo di
  `ModuleReportService`. È il controllo che lega le migrazioni pigre della vista a quelle senza WPF.
- `ui/f3-figures` e `ui/f3-concrete-figures`: serie numeriche, filtri, colori e assi dei grafici dei pali verticali e dei
  domini CA confrontati esattamente con `tests/fixtures/f3-figures` e `f3-concrete-figures`.
- `ui/f3-scheduling`: barriere su `IModuleService` iniettato nel costruttore di `SheetEditor` (solo pali verticali) e
  sonde dentro i workspace di ponte, muri, Bridge Design e CA (`F3SchedulingChecks.cs`, `F3TypedSchedulingChecks.cs`).
- Immagini: le catture di finestre non sono identiche byte per byte fra corse (focus, hover, barre di scorrimento,
  dimensioni; registro F3-07 e F3-08). L'equivalenza si appoggia su dati, testi dei report e serie numeriche.

### 3.5 Copertura per modulo

| Modulo | Prove UI |
| --- | --- |
| Pali verticali/micropali | `smoke`, `smoke-display`, `f3-figures`, `f3-scheduling`, f3 (6 fixture) |
| Orizzontali/micropali orizzontali | `smoke-horizontal` (4 file), `ElasticPile.UiChecks` ×3, f3 (10 fixture) |
| Palificata | `HorizontalPileGroup.Checks`, f3 (2 fixture), `f3-report-chapters`; fuori dal runner `--check-contrast` |
| Elastico | `ElasticPile.UiChecks`, `F3PileSegmentChecks` |
| Muri | `smoke-retaining-wall`, `smoke-global-stability`, 3 check fuori schermo, `F3WallParameterChecks`, f3 (4) |
| Sezione c.a. | `smoke-neutral-axis`, `smoke-ca-features`, `smoke-ca-extensions`, `smoke-section-actions`, `check-concrete-design`, `ConcreteShort.UiChecks`, `ConcreteInput.UiChecks`, `f3-concrete-figures`, f3 (4) |
| Sezione composta | `smoke-bridge`, `smoke-bridge-predalle`, `smoke-bridge-curves`, f3 (2) |
| Bridge Design | `smoke-bridge-design`, `smoke-bridge-prices`, `smoke-bridge-traffic`, f3 (2) |
| Fondazioni | `FoundationWorkspace.UiChecks`, f3 (3) |
| Stratigrafia | `Stratigraphy.UiChecks` (dentro `ModelViewer.UiChecks`); nessuna fixture f3 |
| Materiali | `smoke-materials` (+ `MaterialViewChecks`), `smoke-material-report`, `smoke-steel`, f3 (3) |
| Progetti | `smoke-projects`, `smoke-project-workspace`, `smoke-project-report`, `smoke-hierarchy`, `smoke-sharing`, report di progetto in f3 |
| Wiki | `check-wiki-offscreen`, articoli e ricerche in f3 |
| Shell e temi | `check-appearance`, `smoke-display`, `check-error-log-offscreen` |

Destino già scritto (`progetti-di-test.md`): le suite WPF passano a `tests/ANTHEA.Desktop.UiTests` modulo per modulo
durante F5; a fine F5 spariscono la configurazione UiTests e `supporto/test/Desktop`; `HorizontalPileGroup.Checks`:
«UI archiviata dopo F5.4»; `ElasticPile.UiChecks`, `ConcreteShort.UiChecks`: «Archiviare dopo F5».

### 3.6 Vincoli dei test di architettura

- Controlli sul testo di file del Desktop indicati per percorso (`tests/ANTHEA.Architecture.Tests/Program.cs:114-122`):
  `ProjectReport.cs` non deve contenere `SheetEditor`; `ModuleFigureRenderer*.cs` non deve costruire editor o motori;
  regole su `BridgeWorkspace.cs`, `RetainingWallWorkspace.Advanced.cs`, `RetainingWallWorkspace.Results.cs`.
- Regole senza WPF verificate solo su quattro progetti elencati (`Program.cs:21`: Application, Reports, Wiki,
  Serialization).
- Censimento F4 (`F4DomainRules.cs`, `f4-domain-baseline.json`): scandisce `X.*` e `src`; ogni residuo è legato a file,
  regola, proprietario (`Tipo.Metodo`) e impronta. Una segnalazione nuova fallisce, anche a conteggio invariato (righe
  36-41); una rimossa richiede di ridurre la baseline nello stesso commit (43-45). Spostare codice da un file o da un
  metodo a un altro produce quindi sia una rimozione sia un'aggiunta.
- `limits.json`: `AsyncVoid` 2 e `DispatcherTimer` 4 contati con regex sui sorgenti di `X.Desktop` (e `X.Materiali`).

## 4. Modulo pilota F5.4: palificata orizzontale

Identificativo `geo_efficienza_orizzontale` (`X.Calculations/HorizontalPileGroup.cs:10`), nome «Palificata orizzontale»,
«Effetto di gruppo · sei teorie» (`ModuleCatalog.cs:18`).

### 4.1 File e dimensioni

| Livello | File | Righe | Byte |
| --- | --- | ---: | ---: |
| Vista | `X.Desktop/Wpf/HorizontalPileGroupWorkspace.cs` | 188 | 20.274 |
| Disegno | `X.Desktop/Wpf/PileGroupPlan.cs` | 37 | 3.265 |
| Facciata Application | `src/ANTHEA.Application/Modules/HorizontalPileEditor.cs:15-23` (`HorizontalPileGroupEditor`) | 9 | — |
| Preparazione | `src/ANTHEA.Application/Modules/ModuleSheetPreparation.cs:9-21` (`NumericText`, `PreparePileGroup`, `ApplyPileGroupSelection`) | 13 | — |
| Adattatore d'archivio (legacy) | `X.Calculations/HorizontalPileGroup.cs` (calcolo in `GPC.Checkers.Geotechnics.Piles`) | 134 | 10.422 |
| Report | `src/ANTHEA.Reports/Generators/ReportPileGroup.cs` | 46 | 4.293 |
| Prova dedicata | `supporto/test/HorizontalPileGroup.Checks/Program.cs` | 97 | 12.609 |
| Fixture F3 | `tests/fixtures/f3/inputs/geo_efficienza_orizzontale--{default,legacy-pile-group}.json` + 2×9 file attesi | — | — |

### 4.2 Struttura della vista (`HorizontalPileGroupWorkspace.cs`)

- Campi: `data` (il `JsonObject` passato da `SheetEditor`, già copia profonda), `ObservableCollection<PileRow>` (19),
  `DataGrid` coordinate/confronto/dettagli (20, 26), testi di stato e dei quattro indicatori (22-24), `PileGroupPlan`
  (25), `RecalculationScheduler` (27), `ModuleCalculationSession` creato con `ModuleServices.Get(...)`, quindi non
  iniettabile (28), flag `loading/selecting/calculating/disposed` (30), `spacingDirection` (31, stato della vista non
  salvato), `Result` (32), evento `Modified` (33).
- Classi annidate pubbliche: `PileRow` (34-41, proprietà testuali `Id/X/Y` con callback `Changed`), `ComparisonRow`
  (42-54, proprietà calcolate per la griglia con formattazione e una regola sul testo degli avvisi a 53).
- Helper locali di campo e opzione che scrivono subito nel JSON e accodano il calcolo (59-71); i campi opzionali vuoti
  vengono rimossi (64).
- Costruttore (73-124): `HorizontalPileGroupEditor.Upgrade(data)` (75, migrazione dalla versione 1), scelta della
  disposizione (78-79), diametro e generatore (80, 125-135), griglia delle coordinate (81-85, annullamento dell'edit
  gestito con `Dispatcher.BeginInvoke` a 84), direzioni di carico ±X, ±Y e personalizzata (86-90), basamento (91-96),
  opzioni avanzate e interassi rappresentativi per direzione (97-105, 136-140), pannello dei risultati (106-119),
  due colonne 355 px + resto (120-122), primo calcolo immediato (123).
- Calcolo: `Queue` (147-152) azzera il risultato, scrive lo stato e lancia `Modified` a ogni modifica;
  `DelayedCalculation` (153-157) usa lo scheduler a 180 ms ma esegue `CalculateCurrent` dentro la richiesta, sul thread
  dell'interfaccia; `TryCalculate` (160-164) invalida e calcola subito; `CalculateCurrent` (165-176): commit della
  griglia, `CapturePiles`, `Regenerate(data)`, ricarica le righe se il generatore non è «Generica», calcolo sincrono con
  la sessione, `ApplyPileGroupSelection(data, Result)` (scrive `direzione_selezionata` e `angolo` nel documento), righe
  del confronto e selezione.
- Selezione nel confronto (114): scrive `metodo`, `direzione_selezionata`, `angolo` nel documento, chiama
  `HorizontalPileGroupEditor.Select(Result, …)` che modifica `Result["selezionato"]`, ridisegna e lancia `Modified`.
- `Present` (177-186): testi, indicatori, aritmetica di presentazione (183: distanza asse–bordo = `bordo_d × D`, margine
  `(bordo_d − 0,5) × D`; 185: riduzione `(1 − Factor) × 100`), righe dei dettagli come oggetti anonimi, `plan.Set`.
- `Commit` (159), `Dispose` (187).
- `PileGroupPlan`: `FrameworkElement` con `OnRender` (16-35): basamento, pali colorati per coefficiente, freccia della
  direzione, scala dei colori (`ColorAt`, 36); fondo bianco negli aspetti scuri (20). Non entra nei report.

### 4.3 Flusso dei dati

1. Apertura: `MainWindow.LoadSheetEditor` (`MainWindow.cs:239`) → `SheetEditor` copia i dati (`SheetEditor.cs:83`) →
   `new HorizontalPileGroupWorkspace(Data)` (102) → `Upgrade` → costruzione → calcolo immediato →
   `ApplyPileGroupSelection`. La fixture `legacy-pile-group` mostra l'effetto all'apertura: `versione` 1 → 2,
   `generatore.tipo` → «Generica» (confronto fra input e `opened.json`).
2. Modifica: controllo → scrittura nel `JsonObject` → `Queue` → `Modified` → `SheetEditor.Modified` →
   `MainWindow.MarkDirty` e `RefreshSharedStatus` (`MainWindow.cs:239`) → dopo 180 ms calcolo sincrono → `Present`.
3. Conferma: `MainWindow.Commit` (219-228) → `SheetEditor.Commit` (163) → `pileGroup.Commit` (159: chiude l'edit della
   griglia, eventualmente ricalcola) → copia di `editor.Data` in `currentSheet["dati"]` → controllo dati condivisi.
4. Uscite: `SheetEditor.Result` (35) e `HasResults` (36); `Busy` non consulta la palificata (37). Report interattivo:
   `MainWindow.ExportReport` (439-447, file `Relazione_palificata_orizzontale.docx`, riga 443) →
   `SheetEditor.ExportReport` (367) → `BuildReportDocument` (377) → `ReportPileGroup.CreateDocument(title, Result)`.
   JSON: `SheetEditor.ExportResult` (336-343). Report di progetto senza vista: `ModuleReportService`
   (`ModuleReportService.cs:45,71,81,99`: `PreparePileGroup`, calcolo, `ApplyPileGroupSelection`, nessuna figura).
5. Guida contestuale: etichette con «?»/F1 legate alle chiavi (`WikiContextHelp.Label`, riga 62); argomento
   «Effetto di gruppo e applicabilità» (`WikiContextHelp.cs:42`); guida in `supporto/docs/guida-pratica-anthea.md` da
   riga 801, che descrive la disposizione dei controlli («Scegliere in alto a sinistra…», riga 809), senza schermate.
6. Revisioni in sola lettura: la griglia del confronto resta selezionabile (`RevisionInspection.Allow`, 115).
7. Layout: il modulo non è nell'elenco di `MainWindow.cs:251`, quindi l'editor è avvolto in
   `DisplayAdaptation.Viewport(editor, 1120, 600)`.

### 4.4 Dipendenze

- Application: `HorizontalPileGroupEditor` (`MethodNames`, `LayoutNames`, `Upgrade`, `Regenerate`, `Select`),
  `ModuleSheetPreparation`, `ModuleCalculationSession`, `ModuleServices`, `RecalculationScheduler`.
- Serialization: `J` e le estensioni `S/D/B/Array/Number` (namespace storico `X.Core`).
- UI comune: `Ui`, `Appearance`, `WikiContextHelp`, `RevisionInspection`.
- Motore: `Anthea.Calculations.HorizontalPileGroup` (adattatore) → `GPC.Checkers.Geotechnics.Piles`
  (`LateralPileGroup`, `PileGroupLayout`).
- Dati di progetto: la palificata non compare fra i moduli dei campi condivisi né del profilo terreno
  (`ProjectSharedFields.cs:28-32`, `ProjectSharedSoils.cs:102`, `SoilProfileTransfer.Modules`); non ha trasferimento
  del terreno né collegamenti verso altri fogli.

### 4.5 Prove che la coprono oggi

- `HorizontalPileGroup.Checks` (runner `ui`): controlli del motore, dell'adattatore e dell'archivio (`Program.cs:26-76`),
  poi la vista fuori schermo per riflessione: costruttore non pubblico `(JsonObject)`, proprietà `Result`, campo privato
  `comparison` (`Program.cs:77-88`), PNG 1400×1050.
- `ui/f3-equivalence`: due fixture, 9 file attesi ciascuna, 0 transizioni ammesse.
- `ui/f3-report-chapters`: dati preparati e DOCX della vista uguali al capitolo autonomo.
- `ANTHEA.Application.Tests/EditorContractChecks.cs:41-42`: copie indipendenti di `MethodNames` e `LayoutNames`.
- `--check-contrast` (fuori dal runner) e `smoke` delle revisioni di progetto (`ProjectRevisionNavigationSmokeChecks.cs:29`,
  crea un foglio per ogni modulo di `Archivio.Moduli`).
- Non coperta da `f3-scheduling` (sessione non iniettabile).

### 4.6 Cosa toccherebbe una migrazione (inventario dei punti, senza scelte)

- I due file della vista (225 righe) e i loro 45 residui F4 censiti: `HorizontalPileGroupWorkspace.cs` 8 (R4 su
  `CalculateCurrent`, `ComparisonRow.Method`, costruttore, `Present`; R2 `.5` in `Present`), `PileGroupPlan.cs` 37
  (`OnRender`, `OnRender.P`, `ColorAt`).
- I punti di `SheetEditor` che conoscono il tipo: campo (29), `Result` (35), costruttore (102), `Dispose` (162), `Commit`
  (163), `CalculateAsync` (254), `BuildReportDocument` (377).
- I punti di `MainWindow`: menu (75), elenco dei moduli non scalati (251-252), ramo di `ExportReport` (439-447).
- Le prove: `HorizontalPileGroup.Checks` (nomi privati per riflessione), le catture F3 basate su `SheetEditor`.
- La guida pratica e il suo PDF, se cambia la disposizione descritta (regola di `AGENTS.md` sulle guide).

### 4.7 Rischi specifici del pilota

1. Il comportamento osservabile è sincrono: `CalculateAsync` di `SheetEditor` ritorna con il risultato pronto
   (`SheetEditor.cs:254`); la prova per riflessione legge `Result` subito dopo il costruttore. Un calcolo in background
   cambierebbe tempi e prove.
2. La vista modifica il documento in quattro punti oltre all'input dell'utente: `Upgrade` all'apertura,
   `Regenerate`/`LoadPiles` prima del calcolo, `ApplyPileGroupSelection` dopo, selezione nel confronto. Tutti finiscono
   in `opened/visited/calculated/exported/reopened.json`, confrontati senza transizioni ammesse.
3. Regola di salvataggio propria: numero se interpretabile, altrimenti testo; campi opzionali tolti se vuoti; coordinate
   salvate solo con generatore «Generica».
4. `Select` modifica in place il risultato mostrato (`Result["selezionato"]`), che è anche l'ingresso del report
   interattivo.
5. Stato della vista non salvato (`spacingDirection`, espansori) e callback `Changed` delle righe da sganciare in `Dispose`.
6. Nessuna iniezione del servizio: le prove con barriera usate per gli altri moduli non si applicano senza cambiare il
   costruttore.
7. Il censimento F4 lega i 45 residui a file e metodi attuali (3.6).
8. Il modulo è piccolo e isolato (nessun dato condiviso, nessuna figura nei report, nessun collegamento): copre poco dei
   punti difficili elencati in 5 (tabelle collegate, dialoghi, report con opzioni, Wiki, revisioni).

## 5. Rischi e punti difficili, in ordine

1. **Shell con un solo foglio e un solo documento, contro il requisito di F5.13.** Stato in cinque campi
   (`MainWindow.cs:22-26`); l'editor è distrutto a ogni cambio (238); il documento è sostituito in otto punti
   (1.5, punto 4), anche nelle revisioni con un documento di lavoro parallelo. Annulla/ripristina lavora su istantanee
   dell'intero documento (`ProjectWorkspace.cs:29-51`). `MainWindow` ha 14 file e 2.140 righe e 41 file di prova la
   estendono come partial. Oggi esistono già due varianti di più finestre: la copia di un foglio autonomo in una seconda
   `MainWindow` (`MainWindow.RetainingWall.cs:21-23`) e la vista staccata del ponte (`BridgeDetachedView.cs`).
2. **Dati condivisi fra fogli e fogli CLS collegati.** `ConfirmSharedChanges` (`ProjectSharing.cs:14-34`) confronta la
   base del foglio aperto con la proposta e, dopo la domanda, `ProjectSharedData.ApplyHierarchy` modifica gli altri fogli
   del documento. `RefreshSharedStatus` gira a ogni `Modified` e ricalcola anteprima di sezione e copriferri con le schede
   CLS antenate (`ProjectCoverStatus.cs:15-33`, `ProjectValidation.cs:14-30`). Ogni `SheetEditor` lavora su una copia
   profonda (`SheetEditor.cs:83`): con più fogli aperti le copie degli altri editor non vedono gli aggiornamenti.
3. **`SheetEditor` come contratto delle prove di equivalenza.** `f3-equivalence`, `f3-report-chapters`, `f3-figures`,
   `f3-scheduling` e diverse smoke costruiscono `SheetEditor` e usano `Data`, `Result`, `HasResults`, `Commit`,
   `CalculateAsync`, `ExportReport`, `BuildReport`, `BuildShortReport`, `MaterialReportContent` e campi privati (47 membri
   nominati). La visita della cattura dipende da `Expander` e `TabControl`. Il piano elimina `SheetEditor` in F5.13.
4. **Sezione c.a.** È la parte più grande (26 file, 3.313 righe, 262 KB; `ConcreteWorkspace` 21 file), con viewport 2D e
   3D, quattro schedulatori e sessioni, tabelle filtrabili, report breve e report con opzioni, l'isola MVVM delle
   sollecitazioni, 149 membri nominati dalle prove, 555 transizioni F4 ammesse nelle fixture. È anche annidata in altri
   moduli: il verificatore dei tratti del palo elastico apre un `ConcreteWorkspace` completo in un dialogo
   (`PileReinforcementEditor.cs:146-160`) e i muri chiedono l'apertura di una sezione c.a. (`RetainingWallWorkspace.cs:116`,
   `MainWindow.RetainingWall.cs:9-24`). F5.8 (elastico), F5.9 (muri) e F5.10 (c.a.) sono quindi accoppiati.
5. **Report.** Il percorso interattivo passa dalle viste: `SheetEditor.BuildReportDocument` chiama metodi dei workspace
   di muri, Bridge Design, ponte e c.a. (`SheetEditor.cs:378-381`), che leggono lo stato della vista (fase, selezioni,
   grafici correnti); `MainWindow.ExportReport` contiene i nomi dei file e la finestra delle sezioni per modulo (416-457).
   Le figure, anche dei report di progetto, sono disegnate da controlli WPF (`ModuleFigureRenderer`, con
   `Dispatcher.Yield`, `ModuleFigureRenderer.cs:14`). `f3-report-chapters` pretende vista e capitolo autonomo identici.
6. **Disegni, grafici e 3D.** 19 file `OnRender`, base `DrawingView`/`Plot`, `DomainViewport3D` (554 righe), disegni del
   ponte (645 righe in 5 file), dei muri (451 in 4), di Bridge Design (361). Concentrano la maggior parte dei 2.154
   residui F4 della UI. Le immagini di finestra non sono confrontabili byte per byte (F3-07, F3-08); quelle confrontate
   esattamente sono solo le serie dei grafici dei pali verticali e dei domini CA.
7. **Prove legate ai dettagli interni.** Partial class compilate nell'exe (100 membri di `MainWindow` nominati, prove che
   assegnano i campi privati), 171 chiamate di riflessione con nomi in stringa nei progetti esterni (83 nel solo
   `ElasticPile.UiChecks`), ricerca dei dialoghi per titolo, click simulati sul testo dei pulsanti, `App.xaml` letto dal
   disco. Un rinomina o un cambio di struttura rompe la prova senza che cambi il comportamento.
8. **Dialoghi e messaggi fuori dai servizi.** 72 `ShowDialog` in 36 file, 41 `MessageBox.Show` in 21 file, 33 dialoghi di
   file in 18 file; `IConfirmationService` copre tre domande e `IMessageService` solo `Safe`. Finestre non modali proprie:
   parametri dei muri (con scheduler per finestra), reologia, vista staccata del ponte, importazione della stratigrafia.
9. **Guida contestuale e Wiki legate al tipo `MainWindow`.** `WikiContextHelp.Open` risale la catena degli `Owner` fino a
   una `MainWindow` (`WikiContextHelp.cs:53-57`), come `CalculationHelpView.cs:10`; `WikiEditorial.cs:127` usa
   `MainWindow.WikiExamples`. In una finestra senza `Owner` verso la `MainWindow` la guida non si apre. `WikiView` ha due
   `DispatcherTimer` e salva i progressi; `--check-wiki-offscreen` usa 16 membri privati della shell.
10. **Temi non a token.** Tavolozza calcolata a runtime con gestori di classe globali (`Appearance.cs:101-108`), 365
    colori esadecimali e 423 dimensioni di carattere nel codice, immagini dei report forzate in chiaro; misura del
    contrasto fuori dal runner, testo in `OnRender` non misurato. F5.3 sostituisce questo meccanismo.
11. **Threading e annullamento eterogenei.** Calcoli sincroni sul thread dell'interfaccia (palificata, fondazioni, acciaio,
    report dei singoli fogli), 15 `Task.Run`, 2 `async void` dichiarati più 8 lambda asincrone usate come `Action`, 15
    scarti `_ =`, annullamento esplicito solo in 5 file. Il requisito «calcoli in parallelo senza stato statico» di F5.13
    incontra lo stato statico ancora nei motori (1.7, decisione E5).
12. **Sola lettura delle revisioni e adattamento allo schermo come operazioni sull'albero visuale.**
    `RevisionInspection.Protect` rende di sola lettura i controlli trovati nell'albero, con eccezioni marcate a mano
    (`RevisionInspection.Allow` 22 volte in 11 file, più 74 pulsanti creati con `inspection: true`); l'elenco dei moduli
    non scalati è scritto in `MainWindow.cs:251-252`.
13. **Vincoli dei test di architettura.** Controlli per percorso su file del Desktop (`Program.cs:113-125`) e censimento F4
    per file e metodo: ogni spostamento di codice con residui richiede la riduzione della baseline e produce nuove
    segnalazioni nel file di destinazione. Le regole «senza WPF» sono applicate solo ai quattro progetti elencati.
14. **Moduli senza passo F5 e isole MVVM esistenti.** Fondazioni (460 righe) e stratigrafia (517) non hanno un passo
    F5.5–F5.12; Modelli e le tre isole (`ConcreteTimeEffectsView`, `SectionActionsView`, `BridgePricesView`) seguono già
    il toolkit MVVM con viste costruite da codice, mentre i Modelli usano XAML: due stili di vista nello stesso prodotto.

## Appendice: script usati

In `supporto/artefatti/refactoring/f5/inventario` (non versionata): `areas.py` (righe per area), `logic.py` (indicatori di logica), `members.py` (membri
nominati dalle partial di prova), `baseline.py` e `baseline_files.py` (censimento F4), `transitions.py` (transizioni F3),
`diffjson.py` (input contro catture della palificata). Uscite intermedie: `xd-loc.txt`, `areas.json`, `logic.txt`.
