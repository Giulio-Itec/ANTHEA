# F5 — Interfaccia MVVM: proposta di architettura (checkpoint F5.0)

Proposta per il checkpoint **F5.0 [CP]** di `docs/refactoring/piano.md`.
Analisi in sola lettura del ramo `refactoring/integrazione-1016` (worktree `Temp\aw-int`, punta `71fd52b7`), che stanotte
diventa `main`. Nessun file del repository è stato modificato. Il documento non decide al posto dell'utente: le scelte
aperte sono raccolte, con opzioni e raccomandazione, nella sezione 10. Sintesi per il checkpoint in
[f5-ripianificazione.md](f5-ripianificazione.md); fatti e conteggi della UI in [f5-inventario-ui.md](f5-inventario-ui.md)
(confronto nella sezione 11).

Fonti lette: `AGENTS.md`; `docs/refactoring/piano.md` (fase 5, coda, requisito F5.13); `docs/refactoring/decisioni.md`
(architettura L1–L5, dipendenze esterne, Modelli/MVVM, tema scuro, D1–D5 di F4, E5); `f3-strato-applicativo.md`;
`f4-architettura.md`; `progetti-di-test.md`; i progetti di `src/`, `X.Desktop/`, `tests/` e `build/ci.ps1`;
l'inventario della UI.

---

## 0. Sintesi

1. Un solo progetto nuovo di produzione, **`src/ANTHEA.Presentation`** (net8.0, senza WPF): ViewModel, servizi
   astratti, sessioni di documento e di foglio, `ModuleHost` e modello dei campi. Le viste nuove restano in `X.Desktop`
   (XAML con DataTemplate), che a F5.14 diventa `ANTHEA.Desktop`. Nuovi progetti di prova `tests/ANTHEA.Presentation.Tests`
   (senza WPF) e `tests/ANTHEA.Desktop.UiTests` (fuori schermo, fuori dall'eseguibile, come previsto da F1.5).
2. Base MVVM: **CommunityToolkit.Mvvm 8.3.2**, già usato da tre progetti e già approvato per F5 nella tabella delle
   dipendenze, più un piccolo strato nostro (base dei fogli, calcolo con scheduler, campi su `JsonObject`, guardia dei
   comandi). Nessun contenitore DI: radice di composizione scritta a mano.
3. **Il formato dei documenti non cambia**: i ViewModel lavorano sul `JsonObject` del foglio (come `InputForm` oggi),
   così l'equivalenza resta dimostrabile con `ui/f3-equivalence` senza tolleranze nuove.
4. Convivenza: si estrae da `SheetEditor` l'interfaccia `ISheetEditor`; la vecchia `MainWindow` ottiene ogni foglio dal
   `ModuleHost`, che restituisce il `SheetEditor` storico o un ospite MVVM. Ogni modulo migrato è subito usabile nella
   finestra attuale; la shell nuova (F5.13) arriva quando tutti i moduli sono MVVM.
5. Shell F5.13: un progetto alla volta, più file di calcolo singoli, ogni foglio in una scheda, schede spostabili in
   finestre separate. Il conflitto fra fogli aperti si evita con la stessa regola di oggi: **un foglio si conferma quando
   la sua scheda perde il fuoco**, quindi al più un foglio ha modifiche non confermate e i dati condivisi si propagano
   come oggi, con lo stesso dialogo.
6. Temi a token: i token sono nomi semantici dei pennelli della tavolozza attuale (`Appearance.Colour`), quindi Scuro e
   Molto scuro restano identici per costruzione; le viste nuove usano solo token.
7. Equivalenza: ogni modulo migrato passa `ui/f3-equivalence` contro il riferimento attuale e, in più, un confronto
   diretto vecchia/nuova implementazione a differenza zero. Ogni differenza va in `transitions-f5.json` con voce del
   registro e ok dell'utente (D2).
8. Analyzer di F5.1: estensione di `ANTHEA.Architecture.Tests` (stessa tecnica di F4.2, nessun pacchetto nuovo) con
   regole su WPF, stato statico mutabile, calcoli e messaggistica statica.
9. Pilota: la palificata (225 righe, confronto esatto) prova la catena a basso rischio ma copre poco dei punti difficili e
   non usa `InputForm`; si propone un secondo pilota (palo e micropalo verticale) prima delle tracce parallele (Q20).

---

## 1. Stato di partenza (rilevato sul codice)

| Aspetto | Situazione al 16/10 |
| --- | --- |
| Interfaccia | `X.Desktop`: 158 file C# (18 462 righe, 1,43 MB) e 4 XAML di sole risorse; nessuna vista XAML, nessun ViewModel salvo tre isole; 2 046 controlli creati da codice; 1 686 letture e 296 scritture dirette del JSON del foglio nelle viste. Precedente XAML/MVVM: `ANTHEA.ModelViewer.Wpf` (18 XAML). |
| Finestra principale | `MainWindow` è una partial in 14 file e 2 140 righe (4 `MainWindow*.cs` + 10 `Project*.cs`): un solo documento, un solo foglio (`document`, `currentSheet`, `editor`), documento sostituito in otto punti, `dirty` di documento, storia annulla/ripeti del progetto (30 copie del documento), anteprima delle revisioni in sola lettura, dati condivisi, Wiki, Modelli. |
| Editor dei fogli | `SheetEditor` (`SheetEditor.cs` 390 righe + `GeoEditor.cs` 306) smista 13 moduli verso i `*Workspace` e ospita direttamente palo e micropalo verticale. Superficie usata da fuori: `Data`, `Commit`, `CalculateAsync`, `Result`, `HasResults`, `Busy`, `ExportResult`, `ExportReport`, `BuildReport`, `BuildReportDocument`, `BuildShortReport`, `MaterialReportContent`, `InspectRevision`, eventi `Modified`, `ConcreteSectionRequested`, `ReportRequested`, `ModuleCopyRequested`, `SoilReplacementRequested`, `SoilSources`. È anche **il contratto delle prove**: costruito in 16 punti dei test (`f3-equivalence`, `f3-report-chapters`, `f3-figures`, `f3-scheduling`, smoke), che nominano 47 suoi membri. |
| Campi | `Field` (record in `Ui.cs`, WPF per via di `WikiContextHelp.Topic`) e `InputForm`/`JsonGrid`/`JsonRow` scrivono testo grezzo nel `JsonObject` del foglio; presentazione numerica solo a video (`NumericPresentation`), conferma alla perdita del fuoco, note, aiuto F1. |
| Servizi | `IConfirmationService` (tre domande) e `IMessageService` (solo `Safe`), F1.4, già senza tipi WPF e pensati per essere spostati. Il resto è diretto: 41 `MessageBox.Show` in 21 file, 33 dialoghi di file in 18 file, 72 `ShowDialog` in 36 file, 7 usi degli appunti, 2 `Process.Start`, un `HttpClient` nella finestra di importazione della stratigrafia, 20 `Archivio.ScriviAtomico` dalle viste. |
| Strato applicativo | `IModuleService`, `ModuleCalculationSession` (cache per documento), `RecalculationScheduler` (annullamento, revisione corrente), `ModuleDirectory`, `IFigureRenderer`, `ModuleReportService`, `ProjectReportService`, `ProjectSharedData` (dati condivisi), `ProjectRevisions`, `DocumentService`. Nessun calcolo nella UI (TypeRef Desktop verso Calculations e GPC = 0). |
| ViewModel esistenti | `ModelViewerViewModel` (con `IModelViewerServices`), `ConcreteTimeEffectsViewModel`, `SectionActionsViewModel` e `PriceCatalogueViewModel` (questi due dentro `ANTHEA.Application`). Tutti su CommunityToolkit.Mvvm 8.3.2. |
| Tema | `Appearance`: tavolozza algoritmica (colore della Chiara + ruolo → colore del modo), pennelli vivi legati a `SourceColour`, ripittura dell'albero visuale dei colori locali, proprietà `Dark` per i trigger, `Document()` per le immagini dei report sempre chiare. Nel C# della UI 365 letterali esadecimali (243 distinti), 160 `Brushes.*`, 29 `Color.From*`, 423 `Ui.Text` con la dimensione del carattere scritta nel codice; 19 chiavi `Appearance.<ruolo>.<HEX>` più 5 ruoli nei dizionari. `--check-contrast` è fuori dal runner. |
| Prove WPF | 48 suite nello stadio `ui`. Configurazione `UiTests`: 71 file di prova (9 477 righe) compilati dentro `ANTHEA.exe`; 41 file sono partial di `MainWindow`, 10 di `SheetEditor`, 22 dei `*Workspace` (`ConcreteWorkspace` 12) e 1 di `WikiView`; `AppHarness` ha 42 opzioni. Sette progetti esterni usano la riflessione sulle viste (171 chiamate con nomi in stringa: `ElasticPile.UiChecks` 83, `ModelViewer.UiChecks` 49, `FoundationWorkspace.Checks` 14, `ConcreteTimeEffects.Checks` 12, `ConcreteInput.Checks` 7, `HorizontalPileGroup.Checks` 4, `ConcreteShort.UiChecks` 2) e leggono `X.Desktop/App.xaml` dal disco. |
| Equivalenza UI | `ui/f3-equivalence`: 36 fixture per 12 moduli (palificata orizzontale solo 2, **stratigrafia nessuna**), report di progetto, Wiki; confronto con `tests/fixtures/f3/expected` (462 file) e 22 126 transizioni ammesse in `transitions-f4.json` (D2), concentrate su muri (14 441), progetto (4 994), palo orizzontale elastico (1 117), Bridge Design (994) e sezione c.a. (555); **palificata 0**. La cattura usa `new SheetEditor(...)`, una visita fatta di `Expander`/`TabControl` e i metodi privati `MainWindow.SectionReportSnapshot`/`GenerateSectionReport` per il report di progetto. |
| Baseline F4 | 2 154 residui R1–R6 censiti in `X.Desktop`, soprattutto nei disegni (`BridgeDesignDrawing` 224, `PileReinforcementDrawing` 169, `FoundationViews` 145, `ConcreteViewports` 132, `Drawings` 118); ogni spostamento di codice con residui è una rimozione più un'aggiunta per il censimento. |
| Stato statico | `Appearance` (UI), `ErrorLog.attached`, `JsonData.observer` (E5: da rendere interno e per contesto), cataloghi `public static readonly Dictionary` in `X.Calculations` (`Calcolo`, `Micropali`, `BridgeConcept`, `BridgeSection`, `RetainingWall`, …), `WikiCatalog.Aliases`. E5 assegna a F5.13 lo stato statico di ponte, muri, pali, fondazioni e Wiki. |
| Thread e timer | 3 `DispatcherTimer` (4 occorrenze: doppio clic nell'albero, ricerca e progressi della Wiki), nessuno di calcolo; 2 `async void` (`ConcreteRefinements.RefreshDisplayMesh`, `ProjectReport.Run`) più 8 lambda asincrone passate come `Action`; 15 `Task.Run`; calcoli sincroni sul thread dell'interfaccia per palificata, fondazioni e acciaio. |
| Pacchetti | CommunityToolkit.Mvvm 8.3.2, HelixToolkit.Wpf.SharpDX 3.1.2, WpfMath 2.1.0, PDFsharp-MigraDoc 6.2.4, MathNet.Numerics 5.0.0. Nessun DI, nessun MSTest (le prove sono eseguibili con `Check`). |

Accoppiamenti che pesano sull'ordine dei passi:
- il palo elastico è una vista del modulo orizzontale (`HorizontalWorkspace` crea `ElasticPileWorkspace`): F5.7 e F5.8
  sono in sequenza sullo stesso modulo;
- `PileReinforcementEditor` (elastico) apre una `ConcreteWorkspace` in un dialogo e i muri aprono una copia della
  sezione c.a. (`OpenWallConcreteSection`): fino a F5.10 l'elastico e i muri MVVM devono poter ospitare la sezione c.a.
  storica;
- `SheetEditor` contiene il palo verticale: si elimina solo dopo F5.6;
- la guida contestuale cerca la `MainWindow` risalendo gli `Owner` (`WikiContextHelp.Open`, `CalculationHelpView`) e
  `WikiEditorial` usa `MainWindow.WikiExamples`: in una finestra staccata la guida oggi non si aprirebbe;
- i moduli Fondazioni superficiali e Stratigrafia, e le viste Wiki e Progetti, non sono nell'elenco F5.5–F5.12 del piano;
  Modelli è già MVVM e va solo ospitato (sezione 8 e domanda Q12).

Dimensioni per area (dall'inventario, righe `.cs`; servono alle stime della sezione 8):

| Area | File | Righe | Passo proposto |
| --- | ---: | ---: | --- |
| Shell (MainWindow, menu, App, servizi, adattamento) | 13 | 888 | F5.1, F5.13 |
| Progetti (albero, condivisione, revisioni, report, modelli) | 12 | 1 680 | F5.13 |
| Infrastruttura comune (Ui, InputForm, temi, Plot, disegni base) | 14 | 1 706 | F5.2, F5.3 |
| SheetEditor (contenitore) | 1 | 390 | F5.13 (ritiro) |
| Palificata orizzontale | 2 | 225 | F5.4 |
| Materiali (calcestruzzo, acciaio) | 10 | 690 | F5.5 |
| Palo e micropalo verticale (`GeoEditor`) | 1 | 306 | F5.6 |
| Stratigrafia | 4 | 517 | F5.6b (nuovo) |
| Palo e micropalo orizzontale | 9 | 959 | F5.7 |
| Palo elastico e armature del palo | 5 | 764 | F5.8 |
| Muri e stabilità globale | 21 | 2 146 | F5.9 |
| Fondazioni superficiali | 4 | 460 | F5.9b (nuovo) |
| Sezione c.a. | 26 | 3 313 | F5.10 |
| Sezione composta da ponte | 25 | 2 526 | F5.11 |
| Bridge Design | 8 | 1 388 | F5.12 |
| Wiki | 6 | 804 | F5.13 |
| Strumenti (reologia, già MVVM) | 1 | 152 | ospitata in F5.5 |
| Modelli (`ANTHEA.ModelViewer.*`, già MVVM) | — | — | ospitato in F5.13 |

---

## 2. Stratificazione e progetti

### 2.1 Progetti

| Progetto | Tipo | Riferimenti | Contenuto |
| --- | --- | --- | --- |
| `src/ANTHEA.Presentation` **(nuovo)** | net8.0, senza WPF | Application, Wiki, ModelViewer.Presentation, DesignTools.Presentation; CommunityToolkit.Mvvm | Base MVVM, servizi astratti, sessioni (app, area di lavoro, documento, foglio), `ModuleHost`, campi e form, ViewModel della shell e dei moduli migrati, catalogo degli argomenti di aiuto, preferenze. Namespace `Anthea.Presentation`. |
| `X.Desktop` → `src/ANTHEA.Desktop` a F5.14 | net8.0-windows, WPF, eseguibile `ANTHEA` | Presentation, ModelViewer.Wpf | Radice di composizione, implementazioni WPF dei servizi, `Shell/` (F5.13), `Views/<modulo>/` (XAML), `Themes/` (token), `Hosting/` (`ISheetEditor`, ospite MVVM, adattatore storico), disegni tecnici e `IFigureRenderer`. Fino a F5.13 contiene anche `MainWindow` e le viste storiche. |
| `src/ANTHEA.Application` | invariato | Reports, X.Calculations | Resta senza ViewModel alla fine di F5 (domanda Q17): `SectionActionsViewModel` e `PriceCatalogueViewModel` passano in Presentation con i loro moduli. Riceve le derivazioni di presentazione ancora nelle viste (es. riduzione percentuale e margine libero della palificata), spostate con aggiornamento motivato della baseline F4. |
| `src/ANTHEA.ModelViewer.Presentation`, `src/ANTHEA.DesignTools.Presentation` | invariati | — | Restano separati (domanda Q5); adottano le convenzioni e le regole di architettura di Presentation. |
| `tests/ANTHEA.Presentation.Tests` **(nuovo)** | net8.0, eseguibile con `Check` | Presentation, ANTHEA.Testing | Prove dei ViewModel senza WPF, servizi scritti a copione, barriere sullo scheduler. Stadio `fast`. |
| `tests/ANTHEA.Desktop.UiTests` **(nuovo)** | net8.0-windows, eseguibile | X.Desktop (`InternalsVisibleTo`) | Prove fuori schermo delle viste migrate, temi e contrasto. Riceve modulo per modulo le suite di `supporto/test/Desktop` (piano di `progetti-di-test.md`). Stadio `ui`. |

Spostamenti:
- `X.Desktop/Services/IConfirmationService`, `IMessageService`, `DesktopServices` → `Presentation/Services` (firme
  invariate, già prive di tipi WPF per questo scopo);
- da `Ui.cs`: `Field` → `FieldSpec` in Presentation (stessi parametri; l'aiuto diventa un identificativo), `FieldNotes`
  e la tabella dei simboli di `InputForm.WithSymbol`, `JsonRow`/`JsonRows` (già senza WPF); `InputForm` e `JsonGrid`
  restano per le viste storiche e spariscono con esse;
- da `WikiContextHelp`: il record `Topic` e la tabella `Topics` → `Presentation/Help`; etichetta «?» e apertura
  restano WPF;
- da `NumericPresentation`: la funzione pura `Format` → Presentation (il convertitore resta WPF);
- da `MainWindow.ProjectSharing`/`ProjectWorkspace`: la logica di conferma dei dati condivisi e la storia del progetto
  → `SharedDataCoordinator` e `ProjectHistory` in Presentation (passo F5.2, così anche la finestra vecchia le usa e le
  prove F1.4 diventano prove senza WPF);
- preferenza dell'aspetto (`%LOCALAPPDATA%\ANTHEA\appearance.json`, stessi valori) → `ThemePreference` in Presentation;
  i pennelli restano WPF.

### 2.2 Diagramma delle dipendenze

```
 lib/Checker (GPC: Utilities, Geometry, Model, Checker.*, GPC.Design)      L1–L3
        ▲
 ANTHEA.Serialization ◄── X.Calculations (residuo) ──► ANTHEA.FoundationWorkspace
        ▲                       ▲
        └──── ANTHEA.Reports ───┘
                  ▲
          ANTHEA.Application  (servizi, documenti, report, sessioni di calcolo)    L4
                  ▲
 ANTHEA.Wiki ◄─ ANTHEA.Presentation (NUOVO, net8.0, senza WPF) ─► ModelViewer.Presentation ─► ModelWorkspace, ModelReports
                  ▲                                             └► DesignTools.Presentation
                  │
 X.Desktop → ANTHEA.Desktop (WPF: shell, viste, temi, ospiti) ─► ModelViewer.Wpf (Helix)                  L5

 tests/ANTHEA.Presentation.Tests ─► Presentation        tests/ANTHEA.Desktop.UiTests ─► X.Desktop
```

Regole (verificate in F5.1, sezione 7.4): Presentation non vede WPF né i tipi di `ANTHEA.Calculations` e GPC
(TypeRef = 0, come già il Desktop); le viste non chiamano servizi di calcolo, solo ViewModel; nessuna freccia da
Application o Reports verso Presentation.

---

## 3. Elementi MVVM e convenzioni

### 3.1 Base

- **CommunityToolkit.Mvvm** per `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]` e `AsyncRelayCommand`
  con `CancellationToken` e `IncludeCancelCommand` (già il modello di `ModelViewerViewModel`). Vietati
  `WeakReferenceMessenger.Default`, `StrongReferenceMessenger.Default` e `Ioc.Default` (stato statico).
- Strato nostro, piccolo:
  - `ViewModelBase`: `IDisposable`, token di vita annullato alla chiusura, `IsDisposed`; nessun riferimento a
    `Dispatcher` o `Application.Current`;
  - `CommandGuard`: equivalente di `MainWindow.Safe`; un errore di comando va a `IMessageService.ReportFailure`
    («Operazione non completata», testo dell'eccezione), l'annullamento è silenzioso; `errori.log` continua a
    registrare solo gli errori non gestiti, come stabilito in F1.4 (domanda Q19);
  - `SheetCalculation`: componente che unisce `RecalculationScheduler` e `ModuleCalculationSession` esistenti, con
    ritardo del modulo (450, 300, 180 ms… invariati), stato «in attesa/in corso/completato/dati da completare», avvisi,
    pubblicazione solo della revisione corrente, esecuzione sullo sfondo attraverso un limitatore di concorrenza di
    istanza (sezione 4.6). Nessuna logica di calcolo nuova: chiama `IModuleService` o le sessioni applicative già
    esistenti (`ConcreteAnalysisSession`, `ElasticPileAnalysisSession`, …);
  - `SheetViewModelBase`: implementazione comune di `ISheetViewModel` (sezione 5).
- Contesto di sincronizzazione: i ViewModel nascono sul thread dell'interfaccia; lo scheduler conserva già il contesto
  del chiamante, quindi le pubblicazioni tornano su quel thread senza `Dispatcher` nei ViewModel.

### 3.2 Campi, form e validazione

- `FieldSpec` (ex `Field`): `Key`, `Label`, `Unit`, `Choices`, `Bool`, `ReadOnly`, `Symbol`, `Wide`, `HelpTopic`,
  `Note` (`Func<JsonObject,string?>`), `Default`. Le liste dei campi di un modulo diventano dati del suo ViewModel
  (una sola definizione).
- `FormViewModel` su `JsonObject` + `FieldViewModel` per campo: `Text` grezzo (scritto nel documento con la stessa
  regola di `InputForm.Store`: `values[key] = J.Node(text)`), `Display` formattato con la funzione di
  `NumericPresentation` quando il campo non ha il fuoco, `IsEnabled`, `IsVisible`, `IsReadOnly`, `Note`, `Error`,
  `HelpUri`, scelta predefinita mostrata ma non scritta finché l'utente non sceglie. Gruppi espandibili come
  `GroupFields`. La conferma alla perdita del fuoco (`CommitOnFocusLoss`) diventa una proprietà del form e la vista
  usa il trigger di aggiornamento corrispondente.
- Regola di scrittura per campo, perché oggi ogni vista ha la sua e il formato salvato fa parte dell'equivalenza:
  testo grezzo (`InputForm`), numero se interpretabile altrimenti testo (`ModuleSheetPreparation.NumericText`, palificata),
  chiave tolta se il campo opzionale è vuoto (palificata). Il modulo la dichiara nel `FieldSpec`; non si uniformano le
  regole durante F5.
- `JsonRowViewModel`/`JsonRowCollection` (da `JsonRow`/`JsonRows`, con differimento delle notifiche) per le tabelle.
- Validazione: `FormViewModel` implementa `INotifyDataErrorInfo`; gli errori per campo vengono dai validatori già
  esistenti (`ModuleCatalog.ValidateData`, `ConcreteEditor`, `WallParameterTable`, validazioni φ/n di F2.7-U5, …)
  attraverso un `IFieldValidator` del modulo. Nessuna regola nuova nella UI; il testo non valido resta nel campo e
  nel documento come oggi.
- Vista: un controllo `FieldForm` (XAML, DataTemplate su `FormViewModel`) che riproduce l'impaginazione di
  `InputForm` (colonne compatte, simboli, scelte larghe, note, F1). La parità con `InputForm` si prova in F5.2 con
  le stesse sequenze di modifiche: stessi byte del `JsonObject` risultante.

### 3.3 Servizi (interfacce in Presentation, implementazioni WPF in Desktop, implementazioni a copione nelle prove)

| Servizio | Uso | Note |
| --- | --- | --- |
| `IMessageService` | errori, avvisi, informazioni | esistente; si aggiungono `Inform` e `Warn` al posto dei `MessageBox.Show` sparsi |
| `IConfirmationService` | dati condivisi, spostamento, chiusura | esistente; si aggiungono `AskSaveChanges(documento, fogli)` (Salva/Non salvare/Annulla) e `ConfirmDelete` |
| `IFileDialogService` | apri, salva, esporta, scegli cartella | filtri tipizzati (`FileFilter`), nome proposto; nessun percorso nei ViewModel oltre al risultato |
| `IDialogService` | finestre modali del modulo | `ShowDialogAsync<T>(IDialogViewModel<T>)`: trasferimento terreni, opzioni del report, materiali personalizzati, rinomina |
| `IWikiNavigator` | guida del modulo, argomenti, esempi, F1 | apre per URI della Wiki; nella shell nuova apre una scheda Wiki. Sostituisce la ricerca della `MainWindow` lungo gli `Owner` (`WikiContextHelp.Open`, `CalculationHelpView`) e `MainWindow.WikiExamples`, così la guida funziona anche nelle finestre staccate |
| `IClipboardService`, `IExternalLinkService` | appunti (7 usi), collegamenti esterni (2 `Process.Start`) | sottili, per provare i ViewModel senza WPF |
| `ISheetHost` | comandi verso l'ospite | `OpenModuleCopy`, `ReplaceSoil`, `SoilSources`, `RequestReport`: sostituisce gli eventi di `SheetEditor` |
| `IPreferenceStore` | aspetto, presentazione numerica, viste salvate | stessi file e formati di oggi |
| `IWindowService` | solo shell | stacca/riaggancia una scheda, porta in primo piano |

### 3.4 Messaggi fra fogli aperti

Nessun bus globale. La sessione del progetto espone eventi tipizzati di istanza: `SheetDataReplaced(sheetId)`,
`SharedDataApplied(sourceId, targetIds, keys)`, `StructureChanged`, `RevisionPreviewChanged`, `DocumentRestored`
(annulla/ripeti). Le schede si iscrivono alla creazione e si disiscrivono alla chiusura (dispose). La sequenza dei dati
condivisi è in 4.4.

### 3.5 Vita dei documenti e dei fogli

```
AppSession (processo): servizi, ModuleRegistry, preferenze, tema, limitatore dei calcoli
 └─ WorkspaceSession (shell): 0..1 ProjectSession + 0..n CalculationDocumentSession, finestre e schede
     └─ DocumentSession: JsonObject del documento, percorso, impronta all'ultimo salvataggio
         └─ SheetSession (una per scheda aperta): copia di lavoro, baseline confermata, SheetCalculation, ViewModel
```

- Il `JsonObject` del documento si modifica solo sul thread dell'interfaccia e solo dalla sessione del documento; i
  calcoli lavorano sempre su copie (`DeepClone`), come già oggi.
- Chiudere una scheda conferma il foglio nel documento (come oggi la navigazione), annulla i calcoli e libera
  scheduler e cache (come `SheetEditor.Dispose`). Chiudere un documento singolo con modifiche chiede di salvarlo.

### 3.6 Modifiche non confermate e non salvate

Tre livelli, ciascuno visibile:
1. **non confermate**: bozze dei campi e copia di lavoro diversa dalla baseline del foglio; esistono solo nella scheda
   attiva (4.4);
2. **foglio non salvato**: impronta canonica dei dati confermati diversa da quella all'apertura o all'ultimo
   salvataggio; marcatore «•» sulla scheda e nell'albero del progetto;
3. **documento non salvato**: un foglio non salvato o una modifica della struttura (titolo con «*», come oggi).

L'impronta si calcola alla conferma e al salvataggio, non a ogni tasto. Selezioni persistite nel foglio (per esempio
metodo e direzione della palificata) contano come modifiche, come oggi.

### 3.7 Annulla

Si conserva l'annulla/ripeti del progetto di oggi (30 copie del documento) dentro `ProjectHistory`. Con più schede:
prima si conferma la scheda attiva, poi si ripristina il documento e si ricaricano le schede del progetto per
identificativo; una scheda il cui foglio non esiste più si chiude con un avviso. Nessun annulla per campo in F5 (oggi
esiste solo quello nativo delle caselle di testo): sarebbe una funzionalità nuova (domanda Q13).

---

## 4. Shell (F5.13)

### 4.1 Modello

- **Un progetto alla volta**: aprire o creare un altro archivio progetti chiede di chiudere quello aperto (con la
  domanda di salvataggio); i file singoli restano aperti.
- **Più file di calcolo singoli**: ognuno è un `CalculationDocumentSession` con il suo percorso e la sua scheda.
  «Nuovo …» apre una scheda non salvata; aprire un file già aperto attiva la sua scheda. La copia di un foglio verso un
  altro modulo fuori da un progetto (oggi una nuova `MainWindow`, `OpenModuleCopy`) apre una scheda singola non salvata.
- **Più fogli del progetto in schede**: ogni foglio aperto dall'albero ha la sua scheda; un foglio è aperto al più in
  una scheda. Sono schede anche la panoramica di una sezione, un modello (visualizzatore esistente) e la Wiki
  (domanda Q18).
- **Schede staccabili**: ogni finestra ha la sua barra delle schede (`TabHostViewModel`); «Sposta in una nuova
  finestra» e il trascinamento fuori dalla barra spostano lo stesso ViewModel in una finestra nuova, che si può portare
  su un altro monitor. «Riaggancia» lo riporta nella principale; chiudere una finestra secondaria riporta le sue schede
  nella principale, senza chiuderle. Lo stato è nel ViewModel, quindi staccare non perde dati né risultati; le viste
  pesanti (Helix, grafici) si ricreano. Nessuna libreria di docking (domanda Q10).

### 4.2 Disposizione proposta (da confermare con una bozza fuori schermo all'inizio di F5.13)

- Barra dei comandi e menu (File, Aspetto, Strumenti) come oggi, stesse scorciatoie (Ctrl+N, Ctrl+O, Ctrl+S,
  Ctrl+Maiusc+S).
- A sinistra la barra di navigazione (Home, Progetti, Modelli, Moduli singoli, Wiki) e, con un progetto aperto,
  l'albero del progetto riducibile (oggi «sheetProjectTree»).
- Al centro le schede; la Home e il catalogo dei moduli sono la pagina iniziale quando non c'è nessuna scheda.
- Sopra il foglio, la barra del foglio di oggi: torna, Wiki del modulo, approfondimenti, report, report short,
  «Conferma modifiche» nei progetti, avvisi dei dati condivisi e del modello collegato.

### 4.3 Funzioni della finestra attuale da portare

Navigazione Home/Moduli/Progetti/Modelli/Wiki; albero del progetto (rinomina, duplica, elimina, trascina, riordina,
sposta con conferma); revisioni (barra, anteprima in sola lettura, eliminazione); coerenza dei dati comuni (badge e
confronto); stato del copriferro; report di sezione con avanzamento; condivisione dei terreni; riferimento al modello;
annulla/ripeti; titolo con file, «*» e revisione; argomenti di avvio (percorso, `--wiki`, `--models`); adattamento ai
piccoli schermi (`DisplayAdaptation`, oggi con l'elenco dei moduli non scalati scritto in `MainWindow.cs:251-252`, che
diventa un dato del modulo); report Word/PDF, report short, risultati JSON, esporta foglio; la finestra non modale della
Reologia. La sola lettura delle revisioni passa da `RevisionInspection` (che percorre l'albero visuale, con 22
`RevisionInspection.Allow` e 74 pulsanti `inspection: true` come eccezioni) a `IsReadOnly` dei ViewModel e a
`CanExecute` dei comandi: ogni comando dichiara se è consentito in sola lettura, così viste di risultato e scelte di
visualizzazione restano attive come oggi.

### 4.4 Dati condivisi con più fogli aperti

Oggi esiste un solo editor: tutti gli altri fogli sono sempre confermati nel documento. La shell conserva questa
proprietà:
1. una scheda conferma il suo foglio quando perde il fuoco (cambio di scheda, passaggio a un'altra finestra, comandi
   di progetto, salvataggio, report, chiusura), oltre che con «Conferma modifiche»; quindi al più un foglio ha
   modifiche non confermate;
2. la conferma passa da `SharedDataCoordinator`: stessa logica di `ConfirmSharedChanges` (gruppi cambiati, fogli
   collegati, dialogo «Aggiorna tutti i fogli collegati» / «Solo questo foglio», propagazione automatica quando la
   sezione controlla sezioni discendenti), poi `ProjectSharedData.ApplyHierarchy`;
3. la sessione del progetto pubblica `SharedDataApplied`; ogni scheda aperta su un foglio aggiornato, che per il punto
   1 non ha modifiche proprie, ricarica la copia di lavoro dal documento, invalida il risultato e ricalcola con lo
   scheduler (le richieste in corso vengono scartate dalla revisione), mostrando quali dati sono cambiati e da quale
   foglio;
4. stessa sequenza per spostamenti di fogli, sostituzione dei terreni, annulla/ripeti, eliminazioni (che chiudono le
   schede interessate dopo la conferma).

Alternativa con propagazione per campo a schede con bozze: domanda Q9.

### 4.5 Ritiro di `MainWindow` e `SheetEditor`

1. F5.2: `MainWindow` crea i fogli solo attraverso il `ModuleHost` (`ISheetEditor`). Nello stesso passo il **contratto
   delle prove** smette di essere `SheetEditor`: `f3-equivalence`, `f3-report-chapters`, `f3-figures`, `f3-scheduling`
   e le smoke passano dall'adattatore `IEquivalenceSheet` ottenuto dal `ModuleHost` (stesse fasi, stessi file di
   uscita); il report di progetto della cattura passa dalla sessione del progetto invece che dai metodi privati di
   `MainWindow`. Prima la cattura si ripete identica sul codice nuovo.
2. F5.4–F5.12: un modulo alla volta passa all'ospite MVVM dentro la finestra vecchia. Dopo F5.6 `SheetEditor` contiene
   solo lo smistamento; dopo F5.12 nessun modulo lo usa. Le partial di prova di `SheetEditor` (10 file) e dei
   `*Workspace` (22) si trasferiscono con il loro modulo.
3. F5.13: shell nuova sviluppata anche in parallelo ai moduli (traccia E della sezione 8), avviabile nella build di
   prova con un argomento; **sessione di prova con l'utente [CP]**; dopo l'ok l'avvio usa la shell nuova e si ritirano
   `MainWindow` (14 file, 2 140 righe) e `SheetEditor` (domanda Q11 sul destino del codice). Prima del ritiro: i 41 file
   di prova partial di `MainWindow` (100 membri nominati, `--check-wiki-offscreen` compreso) riscritti sui ViewModel
   della shell in Presentation.Tests o fuori schermo in Desktop.UiTests; i controlli per percorso di
   `ANTHEA.Architecture.Tests` su file del Desktop (`Program.cs:113-125`: `ProjectReport.cs`, `ModuleFigureRenderer*.cs`,
   `BridgeWorkspace.cs`, file dei muri) riscritti sui file nuovi, senza perdere la regola.

### 4.6 Calcoli in parallelo senza stato statico

- Ogni `SheetSession` ha scheduler e cache propri (già di istanza). Fra fogli diversi i calcoli sono concorrenti; un
  `CalculationLimiter` di istanza in `AppSession` limita i lavori contemporanei (per esempio processori − 1) per non
  bloccare l'interfaccia con molte schede.
- F5.13 chiude lo stato statico residuo che E5 le assegna (ponte, muri, pali, fondazioni, Wiki): cataloghi pubblici
  mutabili resi immutabili (come A12), nessun campo statico scritto durante un calcolo, `JsonData.observer` interno e
  per contesto se non già fatto in F2.11c.
- Prova di uscita: gli stessi fogli calcolati in serie e in parallelo (moduli diversi e stesso modulo in due schede)
  danno catture identiche bit per bit, come la prova delle mesh di F2.3b.

---

## 5. ModuleHost

### 5.1 Contratto (Presentation)

```csharp
public interface IModuleDescriptor
{
    ModuleInfo Info { get; }                      // da ModuleDirectory: nessun secondo catalogo
    ModuleHelp Help { get; }                      // guida (WikiCatalog.ForModule), argomenti per campo (ex WikiContextHelp.Topics)
    ModulePersistence Persistence { get; }        // chiavi di stato UI salvate nel foglio, migrazioni pigre esistenti
    ModuleReportCapabilities Reports { get; }     // sezioni e default (ReportWord/ReportConcrete/ReportBridge), short, JSON
}

public interface ISheetModule : IModuleDescriptor
{
    ISheetViewModel CreateSheet(SheetContext context);
}

public sealed record SheetContext(JsonObject Data, bool ReadOnly, ISheetHost Host, IServices Services,
    IModuleService Calculation, CalculationLimiter Limiter);   // Data: copia posseduta dal foglio

public interface ISheetViewModel : IDisposable
{
    string ModuleId { get; }
    JsonObject Data { get; }                      // copia di lavoro, stesso formato di oggi
    long Revision { get; }
    bool IsBusy { get; }
    bool HasResults { get; }
    JsonObject? Result { get; }
    bool IsReadOnly { get; set; }                 // revisione in sola lettura
    event EventHandler? Modified;
    void Commit();                                // svuota le bozze dei campi nella copia di lavoro
    Task CalculateAsync(CancellationToken cancellation = default);
    void Reload(JsonObject data);                 // dati sostituiti dall'esterno (condivisi, terreni, annulla)
    IModuleReportSource Report { get; }           // documento del report e report short, sopra ModuleReportService/generatori
}
```

- **Metadati**: `ModuleInfo` di `ModuleDirectory` (identità, area, nome, descrizione, icona). Nessun elenco parallelo.
- **Fabbrica del ViewModel**: `CreateSheet`; la vista si trova per tipo del ViewModel (DataTemplate in `Views/`).
- **Chiavi di persistenza**: elenco delle chiavi di stato dell'interfaccia salvate nel foglio (per esempio `metodo`,
  `direzione_selezionata`, `vista_orizzontale`, `ui_mista`, `workspace_ca`, `report_sezioni`) e richiamo delle migrazioni
  pigre già esistenti (`ModuleSheetPreparation`, `*Editor.Upgrade`, `ConcreteSheetPreparation`). Serve a tre cose:
  documentare cosa il ViewModel scrive, escludere queste chiavi dai dati condivisi (già fuori da
  `ProjectSharedData.Fields`) e controllare nelle prove che la vista non scriva altre chiavi.
- **Report**: il foglio fornisce il documento (`DocxDocument`) attraverso i generatori e `ModuleReportService` esistenti;
  scelta delle sezioni, nome proposto e Word/PDF passano da `IDialogService` e `IFileDialogService`. Il report di
  progetto resta `ProjectReportService` + `ModuleReportService` + `IFigureRenderer` (già indipendente dalle viste).
- **Wiki**: guida del modulo, approfondimenti, aiuto per campo e F1 da `ModuleHelp`, aperti con `IWikiNavigator`.

### 5.2 Convivenza dei moduli storici e nuovi

- Lato WPF, `ISheetEditor` è la superficie di `SheetEditor` elencata nella sezione 1 (con `UIElement View`). Lo
  implementano `SheetEditor` (invariato) e `MvvmSheetEditor` (ospita `ISheetViewModel` + vista e traduce i comandi di
  `ISheetHost` negli eventi che `MainWindow` già gestisce).
- `ModuleImplementations` (Desktop, sorgente versionato) dice per ogni modulo quale implementazione usa la Release.
  Prima del suo [CP] un modulo nuovo è selezionabile solo nella build `UiTests` (argomento del banco), mai con una
  variabile d'ambiente nel prodotto (come per la traccia dei ripieghi, D3). Dopo il [CP] la Release passa al modulo
  nuovo e la vista storica si ritira (Q11).
- Moduli annidati: un ViewModel nuovo che deve aprire un modulo non ancora migrato (sezione c.a. dall'elastico o dai
  muri) lo chiede a `ISheetHost`, che restituisce l'editor storico in un dialogo finché quel modulo non è migrato.
- Nella shell nuova non serve un adattatore storico se tutti i moduli sono migrati; si tiene come riserva solo se un
  modulo restasse indietro (rischio R5).

---

## 6. Temi a token (F5.3)

### 6.1 Meccanismo

- Un catalogo di token in `X.Desktop/Themes` associa a ogni nome semantico il colore della Chiara e il ruolo. Ogni
  token si registra nelle risorse dell'applicazione con **lo stesso pennello** di `Appearance.Colour(colore, ruolo)`:
  stesso oggetto, stessa trasformazione, stesso aggiornamento al cambio di aspetto. Scuro e Molto scuro restano
  identici per costruzione; una prova confronta, per ogni token e per i tre aspetti, il colore con la trasformazione
  attuale (uguaglianza esatta).
- Le viste nuove usano solo `{DynamicResource Token…}`: niente esadecimali, `Brushes.*` o `Color.FromRgb` (regola P7).
  I disegni tecnici restano su fondo bianco in ogni aspetto (eccezione già accettata) con token del gruppo `Drawing`
  di ruolo `swatch` (identità).
- Le immagini dei report delle viste nuove si producono nell'ambito chiaro, come `Appearance.Document()`.
- Token tipografici oltre ai colori (oggi 423 `Ui.Text` con la dimensione scritta nel codice): `Font.Display` (30),
  `Font.Title` (21/20), `Font.Heading` (17/16), `Font.Body` (13), `Font.Small` (12), `Font.Caption` (11); i valori
  sono quelli più usati oggi e si fissano in F5.3 dal censimento delle dimensioni effettive.
- I disegni tecnici (`Plot`, `DrawingView`, 19 file `OnRender`) restano dove sono e come sono: concentrano la maggior
  parte dei 2 154 residui F4 della UI e spostarli costringerebbe a riscrivere la baseline; le viste nuove li ospitano.
- I dizionari di `App.xaml` si separano in `Themes/*.xaml` caricabili da un eseguibile di prova (punto 3 di F1.5), con
  verifica `--check-appearance` e `--smoke-display`. `--check-contrast` (oggi fuori dal runner) entra nello stadio `ui`.
- Quando l'ultima vista storica è ritirata (F5.14) si può togliere la ripittura dell'albero visuale; i valori dei tre
  aspetti si possono allora congelare in tre dizionari, con la stessa prova di uguaglianza.

### 6.2 Corrispondenza con le risorse attuali

| Token proposto | Risorsa o colore attuale (Chiara, ruolo) | Uso |
| --- | --- | --- |
| `Surface` | `AppearanceSurface`, `Appearance.background.F3F5F8`, `Ui.Bg` | fondo delle pagine |
| `Paper` | `AppearancePaper`, `Appearance.background.FFFFFF` | schede, pannelli, pulsanti |
| `PaperAlt` / `Field.Background` | `Appearance.background.F8FAFC` | righe alterne, campi modificabili |
| `Ink` | `AppearanceInk`, `Appearance.foreground.0B2A4A`, `Ui.Navy` (testo) | testo |
| `InkSecondary` | `Appearance.foreground.234767` | frecce e glifi dell'albero |
| `Muted` | `#64748B` foreground (`Ui.Muted`) | testo secondario |
| `Accent` | `Appearance.Accent` (`#0B5CAD`, `Ui.Blue`) | collegamenti, enfasi |
| `Border` | `Appearance.border.DCE2E9` | bordi dei controlli |
| `Border.Strong` | `Appearance.border.CAD7E5` | pulsanti di progetto |
| `Border.Default` | `AppearanceBorder` (LightGray, border) | bordi di sistema |
| `Divider` | `Appearance.border.E4EAF1` | righe delle griglie |
| `TreeGuide` | `Appearance.border.DCE5EE` | guide dell'albero |
| `Focus` | `Appearance.border.3986CB` | fuoco da tastiera |
| `Selection.Border` | `Appearance.border.2582D2` | riga selezionata dell'albero |
| `CommandBar` | `Appearance.background.0B2A4A` | barre Navy |
| `CommandBar.Hover` | `Appearance.background.244665` | passaggio del mouse sulle barre |
| `OnCommandBar` / `OnCommandBar.Muted` | `Appearance.foreground.FFFFFF`, `#B9C8D8` | testo sulle barre |
| `Hover` | `Appearance.background.E3EFFA` | passaggio del mouse su pulsanti e menu |
| `Hover.Row` | `Appearance.background.F0F6FC` | passaggio del mouse sulle righe dell'albero |
| `Header` | `Appearance.background.E8EFF7` | intestazioni delle griglie |
| `Selected` | `Appearance.Selected` (ruolo `selected`) | pulsante primario, scelta attiva |
| `Selection` | `AppearanceSelection`, `Appearance.selection.E2EFFC` | riga e scheda selezionate |
| `Calculated` | `Appearance.Calculated` (`#EAF2FA`, `calculated`) | campi calcolati o collegati |
| `Note.Background` / `Note.Foreground` / `Note.Border` | `#FFF6DD` background, `#865D16`, `#EEDCAF` | note dei campi |
| `Warning.Background` / `Warning.Foreground` | `#FFFAEB` background, `Appearance.foreground.9A4D0A` | avvisi |
| `Status.Ok` / `Status.Fail` / `Status.Warn` | `DarkGreen`/`ForestGreen`, `Firebrick`, `DarkOrange` (foreground) | esiti |
| `Badge.Ok` / `Badge.Warn` | `#EAF5ED`, `#FFF0D8` background | badge di coerenza |
| `Disabled` | `Appearance.Dim` (ruolo `disabled`) | testo disattivato |
| `Series.*` | `Appearance.Series` (ruolo `series`) | curve e legende |
| `Swatch.*`, `Drawing.*` | `Appearance.Swatch` (ruolo `swatch`), bianco e grigi dei disegni | strati del terreno, disegni tecnici |
| `UtilizationPalette` | `UtilizationColors` (Application) | colori del tasso di lavoro |

Gli altri ~200 esadecimali del codice storico sono in gran parte varianti vicine di questi (es. `#DCE2E9`, `#DCE3EB`,
`#DCE5EF`, `#E2E8F0`, `#E3EAF1`). Si accorpano solo nelle viste nuove, modulo per modulo, con schermate prima/dopo nei
tre aspetti alla revisione del modulo (domanda Q6). La Chiara resta invariata fuori dalle viste migrate e i suoi 719
difetti di contrasto noti restano un seguito separato.

---

## 7. Strategia di prova

### 7.1 ViewModel senza WPF (`tests/ANTHEA.Presentation.Tests`, stadio `fast`)

- Servizi a copione (riuso di `ScriptedConfirmations`/`RethrowingMessages` delle prove WPF) e motori di prova
  trattenuti da barriere, come le prove di scheduling di F3.6.
- Per ogni modulo: apertura, modifica dei campi e scrittura nel `JsonObject` (byte uguali a `InputForm`), comandi,
  calcolo e pubblicazione solo della revisione corrente, annullamento e chiusura durante il calcolo, sola lettura,
  `Reload` dopo dati condivisi, chiavi persistite dichiarate.
- Shell: scenari F1.4 (dati condivisi con le due risposte, spostamento, chiusura con modifiche, errore di comando)
  senza WPF; un progetto alla volta; stesso file aperto due volte; schede staccate e riagganciate; conferma alla
  perdita del fuoco; annulla/ripeti con schede aperte; calcoli concorrenti.

### 7.2 `ui/f3-equivalence` verde per ogni modulo migrato

- La cattura passa da `new SheetEditor(...)` a un adattatore `IEquivalenceSheet` ottenuto dal `ModuleHost`, con le
  stesse fasi (aperto, visitato, calcolato, risultato, report, esportato, riaperto) e la stessa `Visit` sulla vista
  (`Expander` e `TabControl`: le viste nuove usano gli stessi controlli per le sottoschede, oppure la visita si estende
  in modo dichiarato). Il report di progetto passa da `MainWindow.SectionReportSnapshot`/`GenerateSectionReport` alla
  sessione del progetto. Lo stesso adattatore serve a `f3-report-chapters` (vista uguale al capitolo autonomo),
  `f3-figures` e `f3-scheduling`; con l'iniezione di `IModuleService` nel `SheetContext` le prove con barriera, oggi
  possibili solo per i pali verticali, valgono per ogni modulo.
- Nei moduli con molte transizioni F4 (muri 14 441, progetto 4 994, palo orizzontale elastico 1 117, Bridge Design 994,
  c.a. 555) il confronto diretto vecchia/nuova vista è la prova principale: le transizioni elencate devono presentarsi
  identiche anche con la vista nuova.
- Per ogni modulo migrato due prove:
  1. confronto **diretto** fra cattura con la vista storica e cattura con la vista nuova, a differenza zero, finché la
     storica esiste;
  2. il confronto normale con `expected` + `transitions-f4.json`. Ogni differenza nuova va in `transitions-f5.json`
     (stesso formato D2: file, percorso, valore prima e dopo, voce `F5-…` del registro) e richiede l'ok dell'utente;
     una voce elencata che non si presenta fa fallire la prova. Nessuna tolleranza nuova.
- Prima di migrare un modulo con poche fixture (palificata 2, stratigrafia 0) si aggiungono fixture catturate **con la
  vista storica** (cattura prima), in un commit separato, senza toccare il riferimento esistente.

### 7.3 Prove fuori schermo (`tests/ANTHEA.Desktop.UiTests`, stadio `ui`)

- Le suite di `supporto/test/Desktop` (71 file compilati nell'exe) e le parti di interfaccia dei sette progetti che usano
  la riflessione (`ElasticPile.UiChecks`, `ModelViewer.UiChecks` con `Stratigraphy.UiChecks`, `FoundationWorkspace.Checks`,
  `ConcreteTimeEffects.Checks`, `ConcreteInput.Checks`, `HorizontalPileGroup.Checks`, `ConcreteShort.UiChecks`) passano
  qui insieme al loro modulo, riscritte sui ViewModel; resta fuori schermo solo ciò che riguarda la vista (rendering,
  temi, contrasto, layout). Le risorse si caricano da `Themes/*.xaml`, non più leggendo `X.Desktop/App.xaml` dal disco.
  Nessuna delle 48 suite dello stadio `ui` si toglie senza la sostituta; il conteggio si riporta a ogni passo.
- Per ogni vista nuova: schermate nei tre aspetti, `--check-contrast` (0 difetti in Scuro e Molto scuro), assenza di
  scritture nel documento dovute alla sola visita, aiuto F1.
- Le prove con lo schermo restano quelle del profilo `full`; schermo e mouse solo se inevitabili.

### 7.4 Regole di architettura (F5.1, in `ANTHEA.Architecture.Tests`)

| Regola | Controllo |
| --- | --- |
| P1 Presentation senza WPF | net8.0, niente `UseWPF`, niente PresentationFramework/WindowsBase/System.Xaml nei metadati, niente `System.Windows` nei sorgenti. Si estende anche a `ModelViewer.Presentation` e `DesignTools.Presentation`, oggi non controllati. |
| P2 Presentation senza calcolo | TypeRef verso `ANTHEA.Calculations` e GPC = 0; regole R1–R7 di F4.2 senza voci di baseline per Presentation e per le viste nuove. |
| P3 Stato statico mutabile | campi statici non readonly, proprietà statiche con setter, eventi statici, `static readonly` di collezioni o array mutabili. Baseline puntuale su tutto il codice di produzione, che può solo scendere; zero in Presentation; zero per ponte, muri, pali, fondazioni e Wiki a F5.13. |
| P4 Messaggistica e servizi statici | vietati `*.Messenger.Default`, `Ioc.Default`, `Application.Current`, `Dispatcher` nei ViewModel. |
| P5 async | zero `async void` in Presentation; contatori del Desktop solo in discesa (già in `limits.json`). |
| P6 Viste senza calcolo | le viste nuove non chiamano `IModuleService`, sessioni di calcolo o `Task.Run`. |
| P7 Token | nelle viste nuove niente esadecimali, `Brushes.*`, `Color.From*` (attiva da F5.3). |

Ogni regola ha prove positive e negative del rilevatore, come le 26 di F4.2. Facoltativo:
Microsoft.VisualStudio.Threading.Analyzers (già approvato per F5) solo su Presentation (domanda Q16).

---

## 8. Piano F5.1–F5.14 raffinato

Dimensioni: S ≤ 1 sessione di lavoro di un agente, M 1–2, L 2–4, XL oltre 4 (da suddividere). Ogni passo segue le
regole del piano: un ramo, commit piccoli, cattura prima e confronto dopo, registro, nessuna tolleranza nuova.

| Passo | Contenuto | Ingresso | Uscita | CP | Dim. |
| --- | --- | --- | --- | --- | --- |
| **F5.0** | Questa proposta e l'inventario della UI; risposte Q1–Q20; aggiornamento di `piano.md` e `decisioni.md` | F4 chiusa, integrazione 1016 in `main` | approvazione; ramo `refactoring/f5-mvvm` dall'ultimo `main` | **[CP]** | — |
| **F5.1** | Presentation e Presentation.Tests nella soluzione e nel runner; base MVVM, `CommandGuard`, `SheetCalculation`; servizi spostati e nuovi con implementazioni WPF; radice di composizione; regole P1–P6 | F5.0 | build, quick e `ui/f3-equivalence` invariati; prove dei rilevatori; nessun cambiamento visibile | — | M |
| **F5.2** | `ISheetEditor` estratto e usato da `MainWindow` e dalla cattura; `ModuleRegistry`, `ISheetModule`, `ISheetViewModel`, `ISheetHost`; `ModuleImplementations`; `FieldSpec`, `FormViewModel`, `FieldForm`, tabelle; `SharedDataCoordinator` e `ProjectHistory` usati dalla finestra vecchia; adattatore di cattura e confronto diretto | F5.1 | equivalenza invariata; parità `FieldForm`/`InputForm` sulle stesse sequenze; scenari F1.4 senza WPF | — | L |
| **F5.3** | Catalogo dei token, dizionari in `Themes/`, risorse di `App.xaml` separate, prova di uguaglianza dei colori, ambito chiaro dei report, regola P7 | F5.2 (componenti dei form) | `--check-contrast` 0 difetti in Scuro e Molto scuro, Chiara uguale al riferimento; `--check-appearance`, `--smoke-display`; schermate del form nei tre aspetti | **[CP]** elenco dei token e accorpamenti | M |
| **F5.4** | Pilota palificata orizzontale (vista 225 righe, 2 fixture, 0 transizioni): fixture aggiuntive (cattura prima); ViewModel, vista XAML, `PileGroupPlan` invariato; calcolo sincrono e scritture nel documento come oggi (Q8); derivazioni di presentazione in Application con i residui F4 della vista; prova UI di `HorizontalPileGroup.Checks` riscritta; vista storica ritirata | F5.1–F5.3; scelta del pilota (Q20) | equivalenza (diretta a zero e col riferimento, che per questo modulo non ha transizioni), prove VM, schermate e contrasto; resoconto per l'utente; la ricetta di migrazione diventa la regola dei passi seguenti | **[CP]** modulo e convenzioni | S |
| **F5.5** | Materiali (10 file, 690 righe): calcestruzzo (`Materials/*`), acciaio per armature; la finestra Reologia (già MVVM) ospitata | F5.4 | come F5.4 | solo se transizioni | M |
| **F5.6** | Palo e micropalo verticale (ramo generico di `SheetEditor` + `GeoEditor` 306 righe: tre form, strati per sondaggio, grafici di capacità con serie confrontate esattamente da `f3-figures`, stratigrafia disegnata); **F5.6b** Stratigrafia (4 file, 517 righe: tabella, importazione PDF con l'`HttpClient` tolto dalla finestra e lasciato al servizio applicativo esistente), con le prime fixture F3 create prima | F5.4 | come F5.4; `SheetEditor` ridotto allo smistamento | solo se transizioni | M + M |
| **F5.7** | Palo e micropalo orizzontale (9 file, 959 righe; `HorizontalWorkspace` e collegati) | F5.4 | come F5.4 | solo se transizioni | L |
| **F5.8** | Palo elastico (5 file, 764 righe; `ElasticPileWorkspace`, `PileReinforcementEditor`); sezione c.a. storica ospitata fino a F5.10; `ElasticPile.UiChecks` (83 chiamate per riflessione) riscritta | F5.7 | come F5.4; anello interno e nota dello stress block | solo se transizioni | L |
| **F5.9** | Muri (21 file, 2 146 righe; `RetainingWallWorkspace.*`, finestra dei parametri, disegni, stabilità globale); **F5.9b** Fondazioni superficiali (4 file, 460 righe, calcolo sincrono) | F5.4 (F5.6b per i terreni) | come F5.4; prove avanzate dei muri fuori schermo; le 14 441 transizioni F4 dei muri identiche anche con la vista nuova | solo se transizioni | XL (muri) + M |
| **F5.10** | Sezione c.a. (26 file, 3 313 righe, 262 kB; 149 membri nominati dalle prove), in sottopassi: ingressi e materiali, azioni (`SectionActionsViewModel`), domini e viste 3D, SLE e tensioni, taglio e torsione, dettagli e ancoraggi, progetto delle armature, ispezione, report e report short | F5.5 | come F5.4 per ogni sottopasso; poi l'elastico e i muri usano la sezione nuova | solo se transizioni | XL |
| **F5.11** | Sezione composta da ponte (25 file, 2 526 righe) con vista staccabile | F5.4 | come F5.4 | solo se transizioni | L |
| **F5.12** | Bridge Design e prezzari (8 file, 1 388 righe; `PriceCatalogueViewModel` spostato) | F5.4 | come F5.4 | solo se transizioni | L |
| **F5.13** | Shell (shell 888 + progetti 1 680 + Wiki 804 righe; Modelli ospitato): (a) sessioni, coordinatore, storia; (b) ViewModel di navigazione, albero, revisioni, coerenza, schede, comandi; (c) viste e finestre staccate; (d) stato statico E5 e prova dei calcoli paralleli; (e) prove di `MainWindow` trasferite, cattura del progetto; (f) sessione di prova; (g) ritiro di `MainWindow` e `SheetEditor` | (a)–(c) dopo F5.2; (d)–(g) dopo F5.12 | profilo full senza nuovi fallimenti; equivalenza; prova parallela bit per bit; guida pratica e PDF aggiornati; installer di prova | **[CP]** bozza della disposizione all'inizio; **[CP]** sessione di prova prima del ritiro | XL |
| **F5.14** | `X.Desktop` → `src/ANTHEA.Desktop` (nome dell'eseguibile invariato); via la configurazione `UiTests` e `supporto/test/Desktop`; ripittura dell'albero di `Appearance` tolta se non serve più; terza revisione di `AGENTS.md`; registro, full, installer | F5.13 | full conforme, installer verificato, nessun codice di prova in Release | (merge su `main` con ok, come ogni fase) | M |
| F5.15 | Voci R4–R21 del registro con l'utente (invariato) | — | — | **[CP]** | — |

**Ricetta per F5.5–F5.12** (ogni modulo): inventario della vista (campi, comandi, dialoghi, chiavi scritte, stato
statico, calcoli residui); fixture aggiuntive catturate con la vista storica dove la copertura è scarsa; ViewModel e
viste; derivazioni residue spostate in Application con aggiornamento motivato della baseline F4 (mai rigenerata);
registrazione in `ModuleImplementations` solo per `UiTests`; equivalenza diretta e col riferimento; suite del modulo
trasferite in `ANTHEA.Desktop.UiTests`; schermate e contrasto; guide se cambia l'aspetto; passaggio della Release e
ritiro della vista storica.

**Parallelismo** (dopo il [CP] del pilota: F5.4, oppure F5.6 se l'utente sceglie il secondo pilota con Q20; rami
propri e unione in un ramo di integrazione con `ui/f3-equivalence` a ogni unione):
- traccia A: F5.5 → F5.10 (materiali, poi sezione c.a.);
- traccia B: F5.6 → F5.6b → F5.9 → F5.9b (geotecnica e terreni);
- traccia C: F5.7 → F5.8 (stesso modulo; l'aggancio finale alla sezione c.a. dopo F5.10);
- traccia D: F5.11 e F5.12 (indipendenti fra loro);
- traccia E: F5.13 (a)–(c), dopo F5.2.

I componenti comuni (form, tabelle, grafici, token) cambiano solo su un ramo dedicato e si uniscono prima dei moduli che
li usano; il file `ModuleImplementations` è l'unico punto di contatto fra le tracce.

**Scelta del pilota (domanda Q20).** L'inventario mostra che la palificata è piccola (vista 225 righe) e isolata: non usa
`InputForm` né `JsonGrid` (campi scritti a mano), non ha dati condivisi, figure nei report, opzioni del report,
collegamenti ad altri fogli né dialoghi, e calcola in modo sincrono. Prova bene la catena ospite MVVM → equivalenza
(confronto esatto, 0 transizioni) a basso rischio, ma **mette alla prova poco dei punti difficili**: soprattutto non
convalida `FieldForm`, il componente che useranno quasi tutti gli altri moduli. Per questo si propone di aggiungere, prima
di aprire le tracce parallele, un secondo pilota rappresentativo: palo e micropalo verticale (F5.6 anticipato: tre form
`InputForm`, `JsonGrid` per sondaggio, grafici con serie confrontate esattamente, report con finestra delle sezioni, dati
condivisi del terreno, scheduler a 450 ms, 6 fixture), con il suo [CP]. In alternativa si può sostituire la palificata con
il palo verticale o con le fondazioni superficiali (460 righe, dati condivisi, report Word/PDF).

---

## 9. Rischi

| # | Rischio | Mitigazione |
| --- | --- | --- |
| R1 | La cattura «visitato» dipende da scritture fatte dalle viste storiche durante la costruzione e la visita (valori iniziali, migrazioni pigre) | chiavi persistite dichiarate dal modulo; confronto diretto a zero prima del passaggio |
| R2 | Comportamenti di tempo diversi: palificata, fondazioni e acciaio calcolano in modo sincrono e la cattura salva `opened.json` subito dopo `Commit`, che per la palificata calcola e scrive la selezione nel documento | calcolo sincrono conservato dove lo è oggi (Q8); ritardi invariati |
| R3 | Prove WPF legate per partial e riflessione (71 file nell'exe, 41 partial di `MainWindow` con 100 membri nominati, 10 di `SheetEditor`, 22 dei `*Workspace`; 171 chiamate di riflessione in sette progetti; dialoghi trovati per titolo, click sul testo dei pulsanti) | trasferimento modulo per modulo; nessuna suite tolta senza sostituta; conteggio delle 48 suite `ui` a ogni passo |
| R4 | Spostare residui di presentazione fa fallire la baseline F4 (gli spostamenti sono vietati) | commit dedicati con motivo e revisione dell'impronta, mai rigenerazione |
| R5 | Un modulo grande (c.a., muri) blocca F5.13 | sottopassi; adattatore storico nella shell nuova come riserva |
| R6 | Più schede = più memoria e calcoli concorrenti (Helix, domini 3D) | limitatore di concorrenza; viste ricreate solo per la scheda visibile; misura in F5.13 |
| R7 | Moduli annidati (elastico → c.a., muri → c.a.) durante la convivenza | richiesta attraverso `ISheetHost`; aggancio finale dopo F5.10 |
| R8 | Differenze visive nella Chiara dovute agli accorpamenti dei token | solo nelle viste migrate, con schermate e ok al modulo |
| R9 | Accessibilità e automazione: alcune prove usano i nomi di automazione dei controlli | stessi `AutomationProperties.Name` nelle viste nuove |
| R10 | Guide e PDF da aggiornare (procedure e immagini della shell) | aggiornamento in F5.13 e dove un modulo cambia aspetto, con PDF controllato |
| R11 | Trascinamento delle schede fra finestre e monitor con DPI diversi | `DisplayAdaptation` per finestra; prove sul doppio monitor nella sessione di prova |
| R12 | Divergenza fra `ModelViewer.Presentation` e le convenzioni nuove | stesse regole P1–P6 su quel progetto da F5.1 |
| R13 | Guida contestuale ed esempi della Wiki legati al tipo `MainWindow` (catena degli `Owner`, `MainWindow.WikiExamples`) | `IWikiNavigator` da F5.1; prova della guida da una finestra staccata |
| R14 | Controlli di architettura scritti per percorso di file del Desktop (`Program.cs:113-125`) | riscritti sui file nuovi nello stesso commit che ritira i vecchi |
| R15 | Regole di scrittura nel documento diverse per modulo (testo grezzo, numero se interpretabile, chiave tolta se vuota) | dichiarate nel `FieldSpec`, non uniformate in F5 |
| R16 | Pilota poco rappresentativo (palificata: nessun `InputForm`, nessun dato condiviso, nessun dialogo) | secondo pilota prima delle tracce parallele (Q20) |

---

## 10. Domande per l'utente (checkpoint F5.0)

Bloccanti per iniziare F5.1–F5.2: Q1–Q5, Q7, Q11, Q12, Q15, Q16; Q20 entro la fine di F5.2 (decide le priorità dei
componenti dei form). Le altre si possono decidere al checkpoint del passo indicato.

**Q1 — Base MVVM.**
(a) CommunityToolkit.Mvvm 8.3.2, già usato da tre progetti, richiesto da Helix e già approvato per F5, più uno strato
nostro piccolo; (b) solo classi nostre (ObservableObject, RelayCommand, comandi asincroni: circa 300 righe), togliendo
il pacchetto da Application e Presentation. *Raccomandazione: (a)*, con il divieto dei membri statici
`Messenger.Default`/`Ioc.Default`.

**Q2 — Iniezione delle dipendenze.**
(a) radice di composizione scritta a mano, costruttori espliciti; (b) Microsoft.Extensions.DependencyInjection (in
tabella per F5, non ancora presente). *Raccomandazione: (a)*: il grafo è piccolo (una decina di servizi, una sessione
per documento e per foglio); (b) solo se la radice diventasse difficile da leggere.

**Q3 — Forma delle viste nuove.**
(a) XAML con DataTemplate per tipo di ViewModel, code-behind solo per disegno e interazioni grafiche; (b) viste in
codice come oggi, con binding impostati da codice. *Raccomandazione: (a)*, come `ModelViewer.Wpf`; i disegni tecnici
(`OnRender`) restano in codice.

**Q4 — Dove vivono le viste nuove.**
(a) in `X.Desktop` (poi `ANTHEA.Desktop`), con `tests/ANTHEA.Desktop.UiTests` che lo referenzia come già fa
`ModelViewer.UiChecks`; (b) una libreria WPF separata (`ANTHEA.Desktop.Ui`) in cui spostare anche disegni e stili.
*Raccomandazione: (a)*: le viste nuove e storiche convivono nella stessa finestra e condividono disegni e tema; (b) obbliga
a spostare `Appearance`, `Plot` e i disegni prima del tempo.

**Q5 — Un solo progetto Presentation.**
(a) `ANTHEA.Presentation` unico con cartelle per modulo, mantenendo separati `ModelViewer.Presentation` e
`DesignTools.Presentation`; (b) un progetto di presentazione per modulo; (c) accorpare anche Modelli e Reologia.
*Raccomandazione: (a)*.

**Q6 — Token e Chiara.** (al [CP] di F5.3)
(a) un token per ognuno dei ~240 colori attuali, nessuna differenza visiva; (b) insieme semantico di circa 35 token con
accorpamento subito di tutte le varianti vicine (piccole differenze nella Chiara ovunque); (c) insieme semantico
usato solo dalle viste nuove, con accorpamenti modulo per modulo e schermate prima/dopo. *Raccomandazione: (c)*.

**Q7 — Ordine fra moduli e shell.**
(a) moduli nuovi ospitati nella finestra attuale fino a F5.13, shell nuova sviluppata in parallelo e attivata dopo la
sessione di prova; (b) shell nuova subito dopo il pilota, che ospita i moduli storici in schede. *Raccomandazione:
(a)*: ogni modulo migrato è subito in produzione e non si mettono le viste storiche in più schede contemporanee.

**Q8 — Calcolo della palificata (pilota).** (al [CP] di F5.4)
(a) mantenere il calcolo breve sincrono sul thread dell'interfaccia, come deciso in F3.6b (vale anche per fondazioni e
acciaio); (b) calcolo sullo sfondo con lo stesso ritardo di 180 ms, come gli altri moduli. *Raccomandazione: (a)* in
F5.4: oggi `Commit` della palificata calcola e scrive metodo e direzione selezionati nel documento, e la cattura salva
`opened.json` subito dopo, con 0 transizioni ammesse; (b) cambierebbe quei file e le prove. Il calcolo breve sincrono
non impedisce i calcoli in parallelo degli altri fogli; l'eventuale passaggio allo sfondo si valuta in F5.13 come
differenza dichiarata.

**Q9 — Dati condivisi con più schede.** (al [CP] di F5.13, da sapere prima)
(a) conferma del foglio quando la scheda perde il fuoco, come oggi alla navigazione: al più un foglio con modifiche non
confermate, nessun conflitto; (b) propagazione per campo anche verso schede con bozze, con gestione dei conflitti.
*Raccomandazione: (a)*.

**Q10 — Schede staccabili.**
(a) realizzazione nostra: comando «Sposta in una nuova finestra», trascinamento fuori dalla barra, «Riaggancia»;
(b) libreria di docking (per esempio AvalonDock, licenza MS-PL), da aggiungere alla tabella delle dipendenze.
*Raccomandazione: (a)*: il requisito è staccare schede, non comporre pannelli agganciati.

**Q11 — Codice delle viste storiche dopo il passaggio di un modulo.**
(a) eliminazione con tag git prima della rimozione; (b) spostamento in `supporto/SUPERATI/ui-storica/<modulo>`, non
compilato, con registro e impronte, come le guide superate; (c) progetto non distribuito che le compila (pesante: dipende
da tutto `X.Desktop`). Le prove di equivalenza non ne hanno bisogno: confrontano con catture congelate.
*Raccomandazione: (b)*, coerente con la scelta D3 di conservare il vecchio codice fuori dall'installer.

**Q12 — Moduli e viste fuori dall'elenco F5.5–F5.12.**
Proposta: Stratigrafia come F5.6b (con le prime fixture F3), Fondazioni superficiali come F5.9b, Wiki, Progetti e
Modelli dentro F5.13. *Raccomandazione: approvare l'estensione dell'elenco.*

**Q13 — Annulla per campo.**
(a) no in F5: si conserva l'annulla/ripeti del progetto di oggi; (b) annulla/ripeti per foglio sulle modifiche dei
campi (funzionalità nuova). *Raccomandazione: (a)*, eventualmente in coda dopo il refactoring.

**Q14 — Nomi a F5.14.**
(a) cartella e progetto `ANTHEA.Desktop`, eseguibile sempre `ANTHEA.exe`, namespace `Anthea.Desktop` per il codice nuovo
(il namespace `X.Desktop` sparisce con il codice storico); (b) rinomina completa anche dei namespace residui.
*Raccomandazione: (a)*.

**Q15 — Riferimento di equivalenza.**
(a) mantenere `expected` + `transitions-f4.json` per tutta F5, aggiungendo solo `transitions-f5.json`; (b) ricatturare il
riferimento all'inizio di F5 (il confronto parte da zero transizioni). *Raccomandazione: (a)*: la catena delle prove resta
continua; la ricattura di tutte le fotografie è già prevista a fine lavoro (decisione del 7/10).

**Q16 — Forma degli «analyzer» di F5.1.**
(a) regole in `ANTHEA.Architecture.Tests` con il compilatore dell'SDK, come F4.2, nessun pacchetto; (b) un analyzer
Roslyn nel repository che segnala già in compilazione; (c) come (a) più Microsoft.VisualStudio.Threading.Analyzers
(già approvato) sul solo Presentation. *Raccomandazione: (c)*.

**Q17 — ViewModel in Application.**
(a) `SectionActionsViewModel` e `PriceCatalogueViewModel` passano in Presentation con i loro moduli (F5.10, F5.12) e
Application resta senza CommunityToolkit; (b) restano dove sono. *Raccomandazione: (a)*.

**Q18 — Wiki nella shell.** (al [CP] di F5.13)
(a) scheda (staccabile come le altre); (b) pagina della navigazione come oggi; (c) pannello laterale accanto al foglio.
*Raccomandazione: (a)*.

**Q19 — Errori dei comandi.**
(a) come oggi: messaggio all'utente, `errori.log` solo per gli errori non gestiti (F1.4); (b) registrare anche gli errori
dei comandi. *Raccomandazione: (a)*; (b) solo se serve alla diagnosi in esercizio.

**Q20 — Modulo pilota.** (entro F5.2; nuova dopo l'inventario)
(a) solo la palificata orizzontale, come nel piano: piccola (225 righe), confronto esatto, rischio basso, ma prova poco
dei punti difficili e non usa `InputForm`/`JsonGrid`; (b) palificata come primo pilota tecnico, poi palo e micropalo
verticale come secondo pilota con [CP] prima delle tracce parallele (F5.6 anticipato; convalida `FieldForm`, tabelle,
grafici, report con opzioni, dati condivisi del terreno); (c) sostituire la palificata con il palo verticale;
(d) sostituirla con le fondazioni superficiali. *Raccomandazione: (b)*: costa un [CP] in più e riduce il rischio di
scoprire problemi dei componenti comuni quando le tracce parallele sono già aperte.

---

## 11. Confronto con l'inventario della UI

Confronto con [f5-inventario-ui.md](f5-inventario-ui.md) (688 righe, stesso albero). Le due analisi concordano su
struttura, rischi e ordine dei passi. Correzioni fatte in questa proposta dopo il confronto:

| Punto | Prima | Dopo il confronto |
| --- | --- | --- |
| Partial di prova | «41 di `MainWindow`, 18 di `SheetEditor` o dei `*Workspace`» | 41 di `MainWindow` (100 membri nominati), 10 di `SheetEditor`, 22 dei `*Workspace`, 1 di `WikiView`; 71 file e 9 477 righe compilati nell'exe (sezioni 1, 4.5, 9) |
| Riflessione | tre progetti | sette progetti, 171 chiamate (sezioni 1, 7.3) |
| `SheetEditor` | superficie verso la shell | anche contratto delle prove (16 costruzioni, 47 membri; `ExportResult`, `BuildReport`, `MaterialReportContent`): passa all'adattatore in F5.2 (sezioni 1, 4.5, 7.2) |
| Timer | 4 `DispatcherTimer` | 3 timer, 4 occorrenze; più 8 lambda asincrone come `Action`, 15 `Task.Run` (sezione 1) |
| Dimensioni dei moduli | stime in kB parziali e in parte errate (c.a. 25 file, ponte 18) | righe per area dall'inventario (c.a. 26 file/3 313 righe, ponte 25/2 526, …) nella sezione 1 e nella tabella del piano |
| Moduli fuori dal piano | Fondazioni, Stratigrafia, Wiki, Progetti, Modelli | confermati; Modelli e Reologia già MVVM, solo da ospitare (sezioni 1, 8, Q12) |
| Pilota | dimensione M, calcolo sullo sfondo raccomandato | dimensione S (225 righe); calcolo sincrono conservato (Q8 rivista); scelta del pilota posta come domanda (Q20, sezione 8) |
| Servizi | file, dialoghi, messaggi | in più appunti, collegamenti esterni e guida indipendente dalla `MainWindow` (sezione 3.3, R13) |
| Regole di scrittura | una sola regola (`InputForm`) | tre regole per modulo, dichiarate nel `FieldSpec` (sezione 3.2, R15) |
| Equivalenza | confronto diretto per tutti i moduli | prova principale dove le transizioni F4 sono molte (muri 14 441); palificata con confronto esatto (sezione 7.2) |
| Baseline F4 | spostamenti con commit motivati | 2 154 residui della UI, soprattutto nei disegni: i disegni restano dove sono (sezione 6.1) |
| Temi | colori | anche dimensioni dei caratteri (423 `Ui.Text`); `--check-contrast` nel runner (sezione 6.1) |
| Architettura | regole nuove | anche riscrittura dei controlli per percorso di file del Desktop (sezione 4.5, R14) |

Punti dell'inventario che questa proposta non tratta e lascia ai passi: le 17 `FindResource` e i 71 `DynamicResource`
attuali (inventario 1.9) si censiscono in F5.3; il nome fuorviante di `src/ANTHEA.FoundationWorkspace`, che non contiene
WPF (inventario 1.4), si può correggere a F5.14 se l'utente lo chiede.
