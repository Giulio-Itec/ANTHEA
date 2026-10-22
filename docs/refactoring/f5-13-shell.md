# F5.13 shell: parti (a) e (b), sessioni e ViewModel

Agente `f5-13-ab`, 22/10/2026, ramo `refactoring/f5-13-ab` da `refactoring/f5` a 5451a8cc. Fonti: [proposta](f5-proposta-architettura.md)
(sezioni 3.4–3.7, 4 e riga F5.13 della sezione 8), [inventario](f5-inventario-ui.md) (1.5, 1.6), [piano](piano.md) (F5.13, requisito
dell'utente dell'8/10), [decisioni](decisioni.md) (16 e 17 ottobre), codice di `X.Desktop` a 5451a8cc.

Requisiti dell'utente dell'8/10: fogli del progetto in schede; un progetto alla volta; più file di calcolo singoli aperti
insieme; schede staccabili in finestre separate per più monitor.

Confini di questo passo:
- **dentro:** (a) sessioni del progetto e dei file singoli, coordinatore dei documenti aperti, storia annulla/ripeti e
  revisioni, coordinamento dei dati condivisi; (b) ViewModel di navigazione, albero del progetto, revisioni, coerenza fra i
  fogli, schede e comandi. Tutto in `src/ANTHEA.Presentation` (senza WPF) e, per il codice non grafico che non è
  presentazione, in `src/ANTHEA.Application`; prove in `tests/ANTHEA.Presentation.Tests`;
- **fuori:** (c) viste e finestre staccate (la bozza della disposizione è un punto di controllo dell'utente), (d)–(g), il ritiro
  di `MainWindow` e `SheetEditor`, qualunque cambiamento della Release. La shell storica continua a funzionare com'è.

Regola diurna del 22/10: niente stadio `ui` né banco WPF. Le verifiche sono build, `dotnet <dll>` delle prove senza WPF e i
profili `quick` e `standard`.

## 1. Inventario della shell storica

### 1.1 File e membri (`X.Desktop`, a 5451a8cc)

«Membri» conta le dichiarazioni di metodo e costruttore (approssimato, `grep`). `MainWindow` è una partial in 15 file e
2 134 righe.

| File | Righe | Membri | Contenuto | Parte |
| --- | ---: | ---: | --- | --- |
| `Wpf/MainWindow.cs` | 468 | 40 | campi del documento unico (`document`, `currentSheet`, `path`, `dirty`, `editor`); menu File/Strumenti/Aspetto con le scorciatoie; navigazione (Home, Progetti, Modelli, Moduli singoli, Esempi, Wiki); `Commit`, `ShowSheet`, `LoadSheetEditor`; albero (`RefreshTree`, memoria degli espansi); `ConfirmDiscard`; `NewCalculation`, `OpenModule`, `NewProjects`, `AddProject`, `AddStructure`, `AddSheetTo`, `Delete`; `Open`, `LoadFile`, `Save`, `ExportSheet`, `ExportJson`, `ExportReport` (nomi dei file e sezioni del report per modulo) | a, b |
| `Wpf/MainWindow.Examples.cs` | 42 | 4 | esempi installati: cartella, copia di un esempio aperto, pagina Esempi | a, b |
| `Wpf/MainWindow.RetainingWall.cs` | 31 | 3 | `OpenModuleCopy` (nel progetto un foglio nuovo, fuori una seconda `MainWindow`), `ReplaceCurrentSoil` | a |
| `Wpf/MainWindow.ShortReport.cs` | 29 | 1 | report short CLS | b |
| `Wpf/MainWindow.Wiki.cs` | 96 | 6 | Wiki, approfondimenti del modulo, esempi della Wiki che aprono un modulo | b |
| `Wpf/ProjectWorkspace.cs` | 243 | 15 | storia del progetto (su `ProjectHistory` dal F5.2), barra dei comandi, disposizione a tre colonne, panoramica della sezione, intestazione del foglio (`SheetHeading`), duplica sezione, dialoghi della nuova revisione e del riepilogo | a, b |
| `Wpf/ProjectRevisionNavigation.cs` | 167 | 7 | anteprima delle revisioni: documento di lavoro, revisione mostrata, ambito, barra delle revisioni, nuova, passa a, elimina, esci dall'anteprima | a, b |
| `Wpf/ProjectHierarchy.cs` | 256 | 9 | rinomina nell'albero, doppio clic, trascinamento di moduli, fogli e sezioni, spostamento di un foglio | b |
| `Wpf/ProjectSectionReorder.cs` | 89 | 8 | riordino delle sezioni, indicatore di inserimento | b |
| `Wpf/ProjectMovePreview.cs` | 33 | 4 | anteprima dello spostamento (su `SharedDataCoordinator`), annidamento di una sezione, ricarica dell'editor spostato | a, b |
| `Wpf/ProjectSharing.cs` | 205 | 6 | conferma dei dati condivisi (su `SharedDataCoordinator`), badge di coerenza, dialogo «Confronto» con «Uniforma a questo», strati mancanti, avvisi, collegamento delle stratigrafie | a, b |
| `Wpf/ProjectCoverStatus.cs` | 34 | 2 | riga di stato dei dati condivisi e del copriferro sopra il foglio | b |
| `Wpf/ProjectPresentation.cs` | 232 | 14 | catalogo delle schede da aggiungere con la ricerca, albero vuoto, panoramica della sezione (controlli, dati comuni, revisioni, azioni, fogli e sottosezioni, avvisi), glifi | b |
| `Wpf/ProjectReport.cs` | 71 | 4 | report di sezione con avanzamento e annullamento | fuori (report congelati; passa con (e)) |
| `Wpf/ProjectModels.cs` | 138 | 6 | pagina Modelli, modelli della sezione e dell'albero, visualizzatore, avviso del modello collegato | b (solo dati), vista in (c) |
| `Wpf/RevisionInspection.cs` | 67 | 4 | sola lettura delle revisioni percorrendo l'albero visuale | sostituita da `IsReadOnly` (già nei ViewModel dei moduli) |
| `Wpf/DisplayAdaptation.cs` | 92 | 2 | adattamento ai piccoli schermi | (c) |
| `Wpf/WikiView.cs`, `Wpf/WikiContextHelp.cs` | 297 + 46 | 30 | vista della Wiki, aiuto contestuale | (c); la scheda Wiki in (b) |
| `Wpf/SheetEditor.cs` + `Wpf/GeoEditor.cs` | 384 + 268 | 45 | editor storico dei fogli | resta; ritiro in (g) |
| `Hosting/ISheetEditor.cs`, `SheetEditorFactory.cs`, `MvvmSheetEditor.cs`, `ModuleImplementations.cs` | 79 + 41 + 192 + 58 | 23 | contratto dei fogli nella finestra storica, scelta storica/MVVM | resta |
| `Services/DesktopComposition.cs`, `DesktopServices.cs`, `WpfPresentationServices.cs`, `Wpf/WpfDesktopServices.cs`, `Wpf/App.cs` | 84 + 7 + 96 + 78 + 47 | 32 | radice di composizione, servizi WPF, avvio con gli argomenti | resta; la shell nuova si aggancia in (c) |

Già in Presentation dal F5.2: `Projects/SharedDataCoordinator.cs` (205 righe: conferma dei dati condivisi, anteprima dello
spostamento, «Uniforma a questo», strati mancanti, badge) e `Projects/ProjectHistory.cs` (95 righe: annulla e ripristina, 30
copie). `MainWindow` li usa già con gli stessi testi.

Stato statico della shell storica: nessuno nuovo. `MainWindow` tiene tutto in campi di istanza; il timer del doppio clic è di
istanza.

### 1.2 Prove che toccano la shell

Sono tutte WPF, nello stadio `ui`, compilate dentro `ANTHEA.exe` (configurazione UiTests) come partial di `MainWindow`.

| Suite | File (righe) | Cosa copre |
| --- | --- | --- |
| `ui/smoke-projects` (e `-mvvm`) | `ProjectSmokeChecks.cs` (203), `ProjectDragLayoutSmokeChecks.cs` (76) | albero, fogli, dati condivisi, trascinamento |
| `ui/smoke-project-workspace` (e `-mvvm`) | `ProjectWorkspaceSmokeChecks.cs` (163), `ProjectRevisionNavigationSmokeChecks.cs` (125), `ProjectRevisionDeletionSmokeChecks.cs` (112) | disposizione, annulla/ripristina, revisioni |
| `ui/smoke-hierarchy` (e `-mvvm`) | `ProjectHierarchySmokeChecks.cs` (149) | sezioni annidate, spostamenti, eredità dei dati |
| `ui/smoke-sharing` (e `-mvvm`) | `ProjectSharingSmokeChecks.cs` (296), `ProjectCoverWarningSmokeChecks.cs` (127), `ProjectComparisonChecks.cs` (95) | conferma dei dati condivisi, copriferro, confronto |
| `ui/smoke-project-report` | `ProjectReportSmokeChecks.cs` (121) | report di sezione |
| `ui/smoke` (e `-mvvm`), `ui/smoke-display` (e `-mvvm`) | `SmokeChecks.cs` (119), `DisplaySmokeChecks.cs` (294) | apertura dei moduli, adattamento |
| `ui/check-wiki-offscreen`, `ui/check-examples` | `WikiChecks.cs` (226), `tests/Desktop/ExampleLibraryChecks.cs` (67) | Wiki ed esempi dalla finestra |
| `ui/check-module-host-offscreen` | `tests/Desktop/ModuleHostWpfChecks.cs` (243), `ModuleHostBench.cs` (175) | ospite MVVM nella finestra |
| `ui/f3-equivalence` | `tests/ANTHEA.Desktop.Equivalence/F3EquivalenceCapture.cs` (192) | report di progetto con i metodi privati di `MainWindow` |
| `ui/check-error-log-offscreen` | `MainWindow.Tests.cs` (15), `TestServices.cs` (61) | errori dei comandi (F1.4) |

Senza WPF, nello stadio `fast`: `SharedDataCoordinatorChecks` e `ProjectHistoryChecks` in `tests/ANTHEA.Presentation.Tests`.

## 2. Progetto della parte (a): sessioni, coordinatore, storia

Cartella `src/ANTHEA.Presentation/Shell`, namespace `Anthea.Presentation.Shell`. Le sessioni sono classi normali con eventi di
istanza (proposta 3.4), non ViewModel: i ViewModel della parte (b) le osservano. Nessuno stato statico, nessun bus.

| Elemento | Proposta | Contenuto | Da dove viene |
| --- | --- | --- | --- |
| `IDocumentFiles`, `DocumentFiles` | 3.3, 3.5 | apri, salva, cartella degli esempi, copia di un esempio installato | `DocumentService.Open/Save`, `MainWindow.Examples` |
| `ShellTexts` | 4.3 | titolo della finestra («ANTHEA — file *», revisione in sola lettura), intestazione del foglio, nome di un foglio, testi fissi | `UpdateTitle`, `SheetHeading`, `ModuleName` |
| `DocumentSession` | 3.5, 3.6 | documento, percorso, «non salvato», sola lettura, documento da salvare | campi `document`, `path`, `dirty`; `MarkDirty` |
| `CalculationDocumentSession` | 4.1 | file di calcolo singolo: il documento è il foglio | ramo `tipo = "calcolo"` di `MainWindow` |
| `ProjectSession` | 3.7, 4.1, 4.3 | archivio progetti: struttura (progetti, sezioni, fogli, rinomina, duplica, elimina, sposta, riordina, annida), storia (`ProjectHistory`), revisioni (anteprima in sola lettura, nuova, elimina), dati condivisi (`SharedDataCoordinator`) | `Project*.cs`, parti di `MainWindow.cs` |
| `SheetSession` | 3.5, 3.6 | foglio aperto in una scheda: ViewModel del foglio, baseline dei dati condivisi, conferma (la sequenza di `MainWindow.Commit`), ricarica, impronta per «foglio non salvato» | `Commit`, `LoadSheetEditor`, `sharedBaseline` |
| `ISheetFactory`, `RegistrySheetFactory` | 5.1 | crea il `ISheetViewModel` di un foglio dal `ModuleRegistry` con `SheetContext` | `SheetEditorFactory` (solo il ramo MVVM) |
| `WorkspaceSession` | 3.5, 4.1, 4.4 | coordinatore: 0..1 progetto, 0..n file singoli, fogli aperti, foglio attivo; un progetto alla volta; stesso file due volte; conferma alla perdita del fuoco; propagazione dei dati condivisi alle schede aperte; annulla/ripeti e revisioni con più schede; copia verso un altro modulo; sostituzione del terreno; chiusura con le domande di salvataggio | `MainWindow` nel suo insieme |

### 2.1 Regole conservate (dal codice storico)

- **Conferma di un foglio** (`MainWindow.Commit`): niente in sola lettura; `Commit` del ViewModel; proposta = copia del foglio
  con i dati del ViewModel; `SharedDataCoordinator.ConfirmSharedChanges`; dati scritti nel documento; nuova baseline; voce
  nella storia se il documento è cambiato.
- **Modifica** (`MarkDirty`): niente in sola lettura; documento non salvato; voce nella storia.
- **Annulla/ripeti**: `ProjectHistory.Restore` conferma prima il foglio attivo; il documento ripristinato diventa quello
  mostrato e resta «non salvato».
- **Revisioni**: stesse condizioni di `SwitchProjectRevision` (uscita anticipata, ambito non più presente →
  `ArgumentException` con lo stesso testo), stessa istantanea (solo il progetto della sezione, con la revisione mostrata),
  stessa eliminazione preparata e convalidata su una copia prima di toccare il documento (`DeleteProjectRevision`).
- **Struttura**: stessi nomi proposti (`ProjectDocuments.NextName`), stesse condizioni di `CanReorderProjectSection`,
  `CanNestSection`, `MoveProjectSheet` (calcolo dell'indice compreso), stessa domanda di eliminazione, stessa anteprima dello
  spostamento.
- **Salvataggio**: stesso filtro e nome proposto («Calcolo.programma»), rifiuto della cartella degli esempi con lo stesso
  testo, errore mostrato con il titolo «Salvataggio non riuscito»; si salva il documento di lavoro, mai l'istantanea di una
  revisione.
- **Domanda di salvataggio** (`ConfirmDiscard`): ora passa da `IConfirmationService.AskSaveChanges`, rinviata dal F5.2 a F5.13;
  `WpfConfirmationService` la riproduce già con lo stesso testo e gli stessi pulsanti.
- **Errore nell'aggiornamento dei fogli collegati** (rinviato dal F5.2 a F5.13): la sessione del foglio ripristina i dati di tutti i
  fogli della sezione, non solo del foglio confermato, prima di passare l'errore. La finestra storica resta com'è.
- **Riapertura di una scheda** quando i suoi dati cambiano fuori (dati condivisi, terreno, annulla, revisioni): il ViewModel si ricrea
  sui dati del documento, come `LoadSheetEditor` ricrea l'editor, invece di `ISheetViewModel.Reload` (SH17).

### 2.2 Più schede (proposta 4.1 e 4.4)

- **Un progetto alla volta.** Aprire o creare un archivio progetti con un progetto aperto lo chiude prima, con la domanda di
  salvataggio; «Annulla» lascia aperto il progetto e non apre il nuovo. I file singoli restano aperti.
- **File singoli.** «Nuovo …» apre una scheda nuova non salvata; aprire un file già aperto attiva la sua scheda; la copia verso
  un altro modulo fuori da un progetto apre una scheda singola non salvata (oggi una seconda `MainWindow`).
- **Un foglio, al più una scheda.** Aprire un foglio già aperto attiva la sua scheda.
- **Perdita del fuoco.** Attivare un'altra scheda conferma prima quella attiva: al più un foglio ha modifiche non confermate.
- **Dati condivisi.** Dopo ogni conferma o operazione sul documento, ogni altra scheda del progetto il cui foglio nel documento
  è diverso dall'ultimo caricato si ricarica (`ISheetViewModel.Reload`); la sessione pubblica quali fogli si sono ricaricati e
  da quale foglio. Le schede ricaricate non hanno bozze, per la regola del fuoco.
- **Annulla/ripeti** (3.7): conferma della scheda attiva, ripristino, ricarica delle schede del progetto per identificativo;
  una scheda il cui foglio non esiste più si chiude con un avviso.
- **Revisioni.** L'anteprima resta di tutto il progetto, come oggi: le schede del progetto passano al foglio con lo stesso
  identificativo nel documento mostrato, in sola lettura; quelle senza foglio nell'istantanea si chiudono.
- **Chiusura dell'applicazione.** `ConfirmClose` con una domanda di salvataggio per ogni documento non salvato.

## 3. Progetto della parte (b): ViewModel

Cartella `src/ANTHEA.Presentation/Shell` (sottocartelle `Navigation`, `Projects`, `Tabs`, `Commands`). I ViewModel derivano da
`ViewModelBase`; i comandi passano da `CommandGuard` (Q19). Testi, ordini e condizioni sono quelli della finestra storica.

| ViewModel | Proposta | Contenuto | Da dove viene |
| --- | --- | --- | --- |
| `NavigationViewModel`, `ModuleCatalogViewModel` | 4.2, 4.3 | pagine Home, Progetti, Modelli, Moduli singoli, Esempi, Wiki; ogni passaggio conferma la scheda attiva ed esce dall'anteprima delle revisioni; Home con «Riprendi · nome»; catalogo dei moduli per disciplina e elemento («Disponibile», «Apri»/«Riprendi»); piè di pagina | `BuildShell`, `SelectNavigation`, `ShowHome`, `ShowModules` |
| `ProjectTreeViewModel`, `ProjectNodeViewModel`, `ProjectCatalogViewModel` | 4.3 | nodi (progetto, sezione, foglio, modello) con nome, profondità, espansione ricordata, selezione, comandi del menu (Rinomina, Duplica, Elimina) fuori dalla sola lettura, sottosezione, report abilitato, badge di coerenza, riepilogo «N progetti · N sezioni · N fogli», albero vuoto; regole del trascinamento; catalogo delle schede da aggiungere con la ricerca | `RefreshTree`, `ProjectHierarchy.cs`, `ProjectSectionReorder.cs`, `ProjectPresentation.cs` |
| `RevisionBarViewModel`, `NewRevisionDialogViewModel`, `RevisionSummaryViewModel` | 4.3 | barra: ambito, schede delle revisioni, selezione, «Elimina revisione» e «+ Nuova revisione» con abilitazione e suggerimenti, dettagli; dialogo della nota; riepilogo delle modifiche | `UpdateProjectRevisionBar`, `CreateProjectRevision`, `ShowProjectRevisions` |
| `CoherenceViewModel`, `SharedStatusViewModel`, `SectionOverviewViewModel` | 4.3, 4.4 | dialogo «Confronto»: conflitti, partecipanti, valori, «Uniforma a questo» o «Dato collegato», strati mancanti, avvisi, collegamento delle stratigrafie; riga di stato sopra il foglio; panoramica della sezione | `ShowCoherence`, `RefreshSharedStatus`, `BuildSectionOverview` |
| `TabHostViewModel`, `ShellTabViewModel`, `SheetTabViewModel`, `ShellWindowsViewModel` | 4.1 | schede per finestra, scheda attiva, chiusura; «Sposta in una nuova finestra», «Riaggancia», chiusura di una finestra secondaria che riporta le schede nella principale; marcatore del foglio non salvato; `IWindowService` | nuovo (proposta 4.1, Q10 a) |
| `ShellCommandsViewModel`, `ReportSectionsDialogViewModel`, `ISheetExports` | 4.2, 4.3 | menu File con lo stesso ordine e le stesse scorciatoie, Apri, Salva, Salva con nome, Esporta foglio, Report Word (nomi dei file e sezioni per modulo), Report short, Risultati JSON, annulla/ripeti, Reologia | `BuildMenu`, `FileCommands`, `ExportSheet`, `ExportJson`, `ExportReport`, `ExportShortReport` |

La scrittura dei file dei report resta del desktop: `ISheetExports` (Presentation) la astrae, con l'implementazione in (c) sul
codice di `MvvmSheetEditor`. Il codice dei report non cambia (report congelati dall'8/10).

## 4. Vincoli tecnici

- P1–P7 di `ANTHEA.Architecture.Tests`: niente WPF, niente tipi di `ANTHEA.Calculations`/GPC nei metadati, niente stato statico
  mutabile (anche `static readonly` di array o liste), niente `Messenger.Default`/`Ioc.Default`/`Dispatcher`, niente `async void`.
- R1–R7 di F4.2 senza voci di baseline per Presentation: niente `.D(...)` (si leggono i numeri con `GetValue`), niente letterali
  numerici diversi da 0, ±1, ±2, ±100 nelle operazioni (costanti con nome), niente `Math.Pow` e simili.
- Nessuna baseline di architettura cambia in questo passo, salvo spostamenti di codice storico dichiarati nel commit.

## 5. Piano dei sottopassi

| Sottopasso | Contenuto | Uscita |
| --- | --- | --- |
| a0 | questo documento | commit |
| a1 | `IDocumentFiles`, `ShellTexts`, `DocumentSession`, `CalculationDocumentSession` | prove senza WPF |
| a2 | `ProjectSession`: struttura, storia, revisioni, dati condivisi | prove senza WPF, scenari ricavati dal codice storico |
| a3 | `SheetSession`, `ISheetFactory`, `RegistrySheetFactory` e `WorkspaceSession`: documenti aperti, un progetto, stesso file, fuoco, propagazione, annulla e revisioni con più schede, copia, terreno, chiusura (a3 e a4 della prima stesura in un solo commit: la sessione del foglio si crea solo dal coordinatore) | prove con il modulo di prova |
| b1 | navigazione e catalogo dei moduli | prove |
| b2 | albero del progetto, trascinamento, catalogo delle schede | prove |
| b3 | barra delle revisioni, dialogo, riepilogo | prove |
| b4 | confronto, stato dei dati condivisi, panoramica | prove |
| b5 | schede e finestre | prove |
| b6 | comandi | prove |
| chiusura | stato in questo documento, piano, corsa `standard` | |

Ogni commit compila e tiene verde il profilo `quick`. La shell storica non cambia; se resta tempo, `MainWindow` chiamerà le
funzioni pure nuove (titolo, intestazione, istantanea delle revisioni, regole del trascinamento) in un commit separato,
che richiede le suite `ui` della sezione 7 stanotte.

## 6. Domande «da decidere»

Scelte provvisorie con il criterio prudente (decisione del 17/10): si conserva il comportamento di oggi e non si aggiunge
nulla di nuovo oltre a ciò che la proposta approvata descrive. Si rivedono in F5.15; Q9, Q10, Q13 e Q18 anche al punto di
controllo della bozza di F5.13.

| # | Domanda | Opzione presa |
| --- | --- | --- |
| SH1 | Q9, dati condivisi con più schede | (a) della proposta: conferma alla perdita del fuoco, al più un foglio con modifiche non confermate |
| SH2 | Q10, schede staccabili | (a): realizzazione nostra; in (b) solo lo stato nei ViewModel e l'interfaccia `IWindowService` |
| SH3 | Q13, annulla per campo | (a): no; resta l'annulla/ripeti del progetto |
| SH4 | Q18, Wiki nella shell | (a): scheda; in (b) il tipo di scheda, la vista resta la `WikiView` fino a (c) |
| SH5 | Annulla/ripeti con una scheda il cui foglio non esiste più | si chiude con l'avviso della proposta 3.7, testo provvisorio «Il foglio «nome» non esiste più nel progetto: la scheda è stata chiusa.»; oggi annulla chiude sempre il foglio aperto |
| SH6 | Anteprima di una revisione con più schede | di tutto il progetto, come oggi; le schede passano al foglio con lo stesso identificativo in sola lettura; quelle senza foglio nell'istantanea si chiudono senza avviso, come oggi il foglio aperto |
| SH7 | Navigazione durante l'anteprima di una revisione | ogni pagina esce dall'anteprima, come `SelectNavigation` oggi |
| SH8 | Chiusura con più documenti non salvati | una domanda per documento (prima il progetto, poi i file singoli nell'ordine di apertura), con il foglio del documento attivato prima della domanda; testo invariato |
| SH9 | Avviso nella scheda ricaricata per i dati condivisi (4.4, punto 3) | (b) espone foglio di origine e fogli ricaricati; testo e forma dell'avviso al punto di controllo della bozza (c) |
| SH10 | Copia verso un altro modulo fuori da un progetto | scheda singola non salvata (proposta 4.1), con il nome e lo stato «non salvato» di oggi |
| SH11 | Stesso file aperto due volte | si attiva la scheda; percorsi confrontati per intero, senza distinguere maiuscole (Windows) |
| SH12 | «Nuovo …» con un file singolo aperto | scheda nuova, nessuna domanda di salvataggio (oggi il documento si sostituisce dopo la domanda) |
| SH13 | «Riprendi» della Home e del catalogo con più schede | riferito alla scheda attiva, come oggi all'unico foglio aperto |
| SH14 | Barra delle revisioni con più schede | segue la scheda attiva del progetto o la sezione selezionata, come oggi il foglio aperto o la selezione |
| SH15 | Marcatore del foglio non salvato (3.6, livello 2) | (b) espone lo stato, calcolato alla conferma e al salvataggio; la forma («•») al punto di controllo della bozza |
| SH16 | Annulla/ripeti dei file singoli | nessuno, come oggi (`ProjectHistory` vale solo per i progetti) |
| SH17 | Scheda i cui dati cambiano fuori da essa | ViewModel ricreato sui dati del documento, come oggi l'editor; `Reload` (proposta 5.1) conserverebbe lo stato della vista ma salterebbe le preparazioni che alcuni moduli fanno all'apertura |
| SH18 | Avviso delle schede chiuse dall'annulla | un solo messaggio «ANTHEA» con una riga per scheda, testo di SH5 |

## 7. Stato, verifiche e ripresa

Aggiornata a ogni sottopasso chiuso.

- Fatto: a0.
- Suite `ui` da eseguire stanotte: nessuna finché la shell storica non cambia.
