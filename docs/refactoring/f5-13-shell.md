# F5.13 shell: parti (a) e (b), sessioni e ViewModel; parte (c) fino al punto di controllo della bozza; parte (d), stato statico e calcoli in parallelo; parte (e), prove di MainWindow trasferite e cattura del progetto

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

`ShellViewModel` compone le parti come le compone `MainWindow`: titolo della finestra dal documento del foglio con il fuoco, riepilogo
della sezione chiesto dall'albero con la sua barra delle revisioni, barra e riga di stato del foglio con il fuoco aggiornate a ogni
modifica, report chiesto da un foglio. Le viste e le finestre vengono in (c).

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
| b7 | `ShellViewModel`, composizione delle parti | prove |
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
| SH19 | Finestra in cui si apre una scheda nuova | quella con il fuoco (la principale o una staccata) |
| SH20 | Finestra secondaria rimasta senza schede | si chiude; chiudere una finestra secondaria con schede le riporta nella principale (proposta 4.1) |
| SH21 | Destinatario dei comandi Salva, report, risultati, esporta foglio | il foglio con il fuoco e il suo documento; senza fogli, Salva agisce sul progetto; senza documenti non fa nulla |
| SH22 | Pagina «Progetti» senza progetto | apre un archivio nuovo senza domande, perché i file singoli restano aperti (oggi `NewProjects` chiede di salvare il documento sostituito) |

## 7. Stato, verifiche e ripresa

Stato al 22/10/2026: parti (a) e (b) fatte, ferme a un punto chiuso. Nessun file di `X.Desktop` è cambiato: la shell storica e la
Release sono quelle di 5451a8cc.

### 7.1 Fatto

| Sottopasso | Commit | Contenuto | Controlli |
| --- | --- | --- | ---: |
| a0 | 27aae8c5 | questo documento | — |
| a1 | 5a8457d6 | `Shell/DocumentFiles.cs`, `ShellTexts.cs`, `DocumentSession.cs` | 28 |
| a2 | f5817c37 | `Shell/ProjectSession.cs` | 57 |
| a3 | 67ed545a | `Shell/SheetSession.cs`, `SheetFactory.cs`, `WorkspaceSession.cs` | 63 |
| b1 | 72e69733 | `Shell/Navigation/NavigationViewModel.cs`: navigazione, catalogo dei moduli | 25 |
| b2 | 0b533b2d | `Shell/Projects/ProjectTreeViewModel.cs`: albero, trascinamento, catalogo delle schede | 39 |
| b3 | 0e863196 | `Shell/Projects/RevisionBarViewModel.cs`: barra, dialogo e riepilogo delle revisioni | 16 |
| b4 | ff503152 | `Shell/Projects/CoherenceViewModel.cs`: riga di stato, confronto, collegamento, riepilogo della sezione | 20 |
| b5 | 3f837763 | `Shell/Tabs/TabsViewModel.cs`: schede, finestre staccabili, `IWindowService` | 18 |
| b6 | 45e8689d | `Shell/Commands/ShellCommandsViewModel.cs`: menu, file, report, `ISheetExports` | 37 |
| b7 | commit di chiusura | `Shell/ShellViewModel.cs`: composizione; questa sezione | 11 |

In tutto circa 3 700 righe in `src/ANTHEA.Presentation/Shell` e 1 600 righe di prove in `tests/ANTHEA.Presentation.Tests`
(`ShellChecks`, `ShellBench`, `ShellDocumentChecks`, `ProjectSessionChecks`, `WorkspaceChecks`, `NavigationChecks`,
`ProjectTreeChecks`, `RevisionBarChecks`, `CoherenceChecks`, `TabsChecks`, `CommandsChecks`, `ShellViewModelChecks`): 314 controlli
nello stadio `fast` con le altre prove di Presentation, e da soli con `--shell`. Le attese vengono dal codice storico e dalle prove WPF
della shell (sezione 1.2), lette e riportate a mano; nessuna è presa dall'esecuzione della shell nuova.

### 7.2 Verifiche

- A ogni commit: build di `tests/ANTHEA.Presentation.Tests`, prove della shell, `Architecture.Tests` (P1–P7 e R1–R7 senza voci nuove;
  baseline invariate).
- Profilo `quick`: `20261022-090358-f5-13-a1` PASS 55 e `20261022-093550-f5-13-a3` PASS 55, 0 NEW-FAIL.
- Profilo `standard` sul commit di chiusura: esito nel rapporto finale dell'agente e in
  `ANTHEA-f5-ripresa\f5-agenti\f5-13-ab\avanzamento.md`.

### 7.3 Suite `ui` da eseguire stanotte

Nessuna è necessaria per questo lavoro: `X.Desktop`, le prove WPF e la Release non cambiano. Le suite che toccano la shell
(sezione 1.2) restano quelle di oggi e girano con il profilo full come ogni notte.

### 7.4 Cosa resta e come riprendere

- **Chiamate della shell storica alle funzioni nuove.** Non fatte, per prudenza: con la regola diurna il confronto con le prove WPF
  non si può fare oggi. Candidate, tutte funzioni pure:
  - `ShellTexts.WindowTitle` in `MainWindow.UpdateTitle` e `ShellTexts.SheetHeading` in `SheetHeading`;
  - `ProjectSession.Snapshot` in `SwitchProjectRevision`;
  - le regole `CanReorderSection`, `CanNestSection` e l'indice di `MoveSheet` in `ProjectHierarchy` e `ProjectSectionReorder`;
  - i testi di `RevisionBarViewModel` in `UpdateProjectRevisionBar`;
  - la riga di `SharedStatusViewModel` in `RefreshSharedStatus`.

  Se si fanno, in un commit a parte, con le suite `ui/smoke-projects`, `ui/smoke-project-workspace`, `ui/smoke-hierarchy`,
  `ui/smoke-sharing` (e le varianti `-mvvm`), `ui/smoke-project-report` e `ui/f3-equivalence`.
- **Parte (c)**, dopo il punto di controllo della bozza:
  - viste XAML della shell e finestre;
  - implementazioni desktop di `ISheetExports` (sul codice di `MvvmSheetEditor`), di `IWindowService` e del menu «Aspetto»;
  - ospite dei moduli ancora storici nella shell nuova, se servirà (proposta 5.2, rischio R5);
  - pagina Modelli e scheda del modello; esempi della Wiki che aprono un modulo (`MainWindow.WikiExamples`);
  - adattamento ai piccoli schermi; argomenti di avvio (`--wiki`, `--models`, percorso).

  Aggiornamento del 22/10 (agente `f5-13-c`): i pezzi che non dipendono dalla disposizione e la bozza per il punto di controllo sono fatti,
  sezioni 8–10; quel che resta della parte (c) è nella sezione 10.
- **Parti (d)–(g)** come da piano. Il report di sezione (`ProjectReport.cs`) passa con (e), insieme alla cattura del progetto. Aggiornamento della
  notte del 22–23/10: parte (e) nella sezione 12 (report dalla shell senza toccare `ProjectReport.cs`, suite trasferite, cattura).
- **Domande:** SH1–SH22 nella sezione 6, da rivedere al punto di controllo della bozza e in F5.15.

Per riprendere:

```
cd C:\Users\g.pacini\AppData\Local\Temp\aw-f5-13-ab
dotnet build tests/ANTHEA.Presentation.Tests/ANTHEA.Presentation.Tests.csproj -c Release
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll supporto/artefatti/f5-13/presentation --shell
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Profile quick -Tag <tag>"
```

## 8. Parte (c): pezzi che non dipendono dalla disposizione

Agente `f5-13-c`, 22/10/2026, ramo `refactoring/f5-13-c` da `refactoring/f5` a 790b9081. Regola diurna: nessuna prova WPF eseguita di giorno.

Tutti in `src/ANTHEA.Presentation/Shell`, con prove senza WPF in `tests/ANTHEA.Presentation.Tests` (`--shell`).

| Pezzo | Dove | Da dove viene | Prove |
| --- | --- | --- | --- |
| `ISheetExports` | `Commands/SheetFileExports.cs` | il codice di `MvvmSheetEditor` (JSON del risultato con `dati` per Bridge Design, BD7; report nei tre modi di T8; report short rifiutato con lo stesso testo e scritto entro due pagine fuori dal thread dell'interfaccia). `MvvmSheetEditor.ExportResult`, `ExportReport` e `BuildShortReport` chiamano ora questi membri: stesso codice, nessuna copia | `SheetExportsChecks` |
| `IWindowService` | `Tabs/ShellWindowTracker.cs` (`IShellWindow`) | nuovo (proposta 4.1, Q10 a): attivazione, chiusura dall'utente o dalla shell, chiusura con la principale | `WindowTrackerChecks` |
| Menu «Aspetto» | `Commands/AppearanceMenuViewModel.cs`, `IAppearanceService`; `X.Desktop/Services/WpfAppearanceService.cs` | `MainWindow.BuildMenu` (voci, segno, rilettura all'apertura) | `ShellPartCChecks` |
| Argomenti di avvio | `StartupRequest.cs`, `ShellViewModel.Start` | `App.OnStartup` (`--wiki`, percorso, `--models`) | `ShellPartCChecks` |
| Esempi della Wiki che aprono un modulo | `Navigation/WikiViewModels.cs` (`WikiExamples`, `WikiTabViewModel`), `ShellViewModel.ShowWiki`, `OpenWikiModule`, `OpenModule`, `ReturnToWork` | `MainWindow.Wiki.cs` | `ShellPartCChecks`; confronto con `MainWindow.WikiExamples` nella prova WPF della bozza |
| Pagina Modelli e scheda del modello | `Projects/ModelsViewModels.cs` (`ModelsPageViewModel`, `ModelTabViewModel`, `ModelReferenceViewModel`), `ShellViewModel.ShowModel` | `ProjectModels.cs` | `ShellPartCChecks` |
| Barra del foglio | `Navigation/SheetBarViewModel.cs` (`WikiMenu` per «Approfondimenti») | `BuildShell`, `UpdateBackButton`, `ShowModuleWikiTopics` | `ShellPartCChecks` |
| Centro della finestra | `ShellViewModel.Center`, `ShellWindowsViewModel.ShowPage`, `NavigationViewModel.ShowTab` | `SelectNavigation`, `ShowSheet` | `ShellPartCChecks` |

`MainWindow.WikiExamples` resta nella finestra storica fino al suo ritiro (g): la prova della bozza confronta le due copie su ogni esempio.
La shell storica non cambia; l'unico file della Release toccato è `MvvmSheetEditor` (riuso del codice, nessun cambiamento di
comportamento), che va provato stanotte con le suite delle viste MVVM (sezione 10).

## 9. Bozza della disposizione per il punto di controllo

### 9.1 Cosa mostra la bozza

**Non è la finestra dell'applicazione**: la Release apre `MainWindow`. I file sono in `X.Desktop/Views/Shell` (regole P6 e P7 delle viste
nuove, soli token):

- `ShellWindow.xaml(.cs)`: la finestra principale su `ShellViewModel`;
- `ShellTemplates.xaml`: i DataTemplate per tipo di ViewModel (pagine, schede, barre, albero) e gli stili, uniti da `ShellWindow` e non da
  `App.xaml`;
- `SheetBarView`, `WikiTabView`, `ModelTabView`, `DetachedShellWindow`, `ShellConverters.cs`, `ShellWindowParts.cs`
  (`DetachedWindowHandle` per `ShellWindowTracker`, scelta dell'intestazione della scheda).

Disposizione, sul modello di `MainWindow` con i fogli in schede (proposta 4.2):

```
┌ File · Strumenti · Aspetto ──────────────────────────────────────────────────────────────────────────────┐
│ Navigazione   │ Albero del progetto │ ▸ [Scheda 1 •] [Scheda 2] [Wiki] [Modello] …                       │
│ (logo,        │ (con un progetto,   ├─────────────────────────────────────────────────────────────────────┤
│ Home,         │ «+ Progetto»,       │ ANTHEA | Progetti                              ↶ Annulla ↷ Ripristina│
│ Progetti,     │ «+ Sezione»,        │ Barra delle revisioni della sezione o del foglio                    │
│ Modelli,      │ albero, riepilogo)  │ Barra del foglio: ← Torna · file · Conferma · Wiki · Approfondimenti│
│ Moduli,       │                     │ Riga di stato dei dati condivisi · avviso del modello collegato     │
│ Esempi, Wiki; │                     │                                                                     │
│ Apri, Salva…; │                     │ Pagina (Home, Moduli singoli, Progetti, Modelli, Esempi)            │
│ piè di pagina)│                     │ oppure la scheda selezionata                                        │
└───────────────┴─────────────────────┴─────────────────────────────────────────────────────────────────────┘
```

- **Navigazione** sempre visibile (230 px), con i pulsanti dei file; la pagina scelta è evidenziata.
- **Albero del progetto** in una colonna della shell (330 px), visibile con un archivio aperto, chiuso e riaperto da «▸» accanto alle
  schede; menu del nodo (Apri, Rinomina, Duplica, Sottosezione, Report Word della sezione, Elimina), «•» dei fogli non salvati, badge di
  coerenza.
- **Schede** della finestra in una barra sopra il contenuto: titolo, «•» del foglio non salvato, «×»; menu della scheda con «Sposta in una
  nuova finestra», «Riaggancia», «Chiudi». Le pagine della navigazione si mostrano al posto della scheda, che resta nella barra senza fuoco;
  la Home è la pagina iniziale quando non c'è nessuna scheda.
- **Barra del progetto** «ANTHEA | Progetti» con annulla e ripristina e, sotto, la **barra delle revisioni**, con un archivio aperto.
- **Barra del foglio** con il fuoco (Navy): «← Torna al progetto» o «← Torna ad ANTHEA», Apri, Salva, Salva con nome, Report Word, Report
  short (solo sezione c.a.), «Conferma modifiche» con annulla e ripristina in un progetto modificabile, «Wiki del modulo», «Approfondimenti ▾»,
  intestazione del foglio.
- **Riga di stato** dei dati condivisi e **avviso del modello collegato** sopra il foglio, come oggi.
- **Pagina Progetti**: riepilogo della sezione (dati comuni, revisioni, modelli, avvisi) e catalogo «Schede da aggiungere» con la ricerca.
- **Finestra staccata**: barra delle sue schede, «Riaggancia», la scheda selezionata; titolo «scheda — ANTHEA».

Parti della bozza ancora segnaposto: il riepilogo della sezione non ha i comandi (Confronto, Duplica, Report, fogli e sottosezioni
cliccabili); il catalogo dei moduli non ha le icone e il «Riprendi» per modulo; l'albero non ha trascinamento né rinomina in linea; mancano
l'adattamento ai piccoli schermi e le scorciatoie Ctrl+Z/Ctrl+Y. Nella shell nuova si aprono solo i moduli con il ViewModel MVVM (rischio R5);
la sezione c.a. mostra la sua vista quando F5.10 S9 aggiunge i suoi template. Si completano dopo il punto di controllo, sulla disposizione
scelta. Aggiornamento del 22/10 sera: quel che è stato completato dopo il punto di controllo è nella sezione 10.1; il resto, compresi i moduli storici
nella shell nuova (R5), nella sezione 10.2 (notte del 22–23/10).

### 9.2 Scelte per l'utente

Per ognuna, l'opzione presa nella bozza. Le SH1–SH22 sono nella sezione 6; qui quelle che la bozza rende visibili, più quelle nuove della
disposizione.

| # | Domanda | Opzione nella bozza |
| --- | --- | --- |
| SH5, SH18 | Avviso delle schede chiuse dall'annulla | un messaggio «ANTHEA» con una riga per scheda: «Il foglio «nome» non esiste più nel progetto: la scheda è stata chiusa.» |
| SH6 | Anteprima di una revisione con più schede | di tutto il progetto; le schede passano in sola lettura, quelle senza foglio si chiudono senza avviso |
| SH9 | Avviso nella scheda ricaricata per i dati condivisi | nessun avviso nella bozza (il ViewModel espone foglio di origine e fogli ricaricati); proposta: una riga sotto la barra del foglio «Aggiornato da «foglio»: …» |
| SH15 | Marcatore del foglio non salvato | «•» dopo il titolo, nella scheda e nell'albero, colore `Token.Accent` |
| SH17 | Scheda i cui dati cambiano fuori | ViewModel ricreato sui dati del documento |
| SH19 | Finestra in cui si apre una scheda nuova | quella con il fuoco |
| SH20 | Finestra secondaria senza schede | si chiude; chiuderla riporta le schede nella principale |
| SH21 | Destinatario di Salva, report, risultati | il foglio con il fuoco e il suo documento; senza fogli il progetto |
| SH22 | Pagina «Progetti» senza progetto | apre un archivio nuovo senza domande |
| SH23 | Barra di navigazione | sempre visibile (oggi solo nelle pagine; nel foglio e nel progetto c'è «← Torna…») |
| SH24 | Albero del progetto | colonna della shell, visibile con un archivio aperto anche sulle pagine, «▸» per chiuderla (oggi a sinistra nell'area progetti e ripiegabile nel foglio) |
| SH25 | Pagine e schede | la pagina scelta prende il centro, le schede restano nella barra senza fuoco; nessuna scheda «Home» |
| SH26 | Wiki | scheda (Q18 a): «Wiki» della navigazione apre o seleziona la scheda; mentre è mostrata nessuna voce della navigazione è evidenziata |
| SH27 | Modelli | una scheda per modello; «+ Modello» una scheda «Nuovo modello» per sezione (oggi una pagina che sostituisce la precedente) |
| SH28 | Esempio della Wiki che apre un modulo | scheda nuova non salvata, senza domanda (oggi sostituisce il documento dopo la domanda), come SH12 |
| SH29 | Finestre staccate | non possedute dalla principale (restano su un altro monitor anche quando la principale è ridotta a icona), si chiudono con la principale |
| SH30 | «← Torna…» nella barra del foglio | resta come oggi, anche se la navigazione è sempre visibile |
| SH31 | Annulla e ripristina | nella barra del progetto e, con un foglio del progetto, anche nella barra del foglio (oggi nell'una o nell'altra) |
| SH32 | Archivio aperto | mostra la pagina «Progetti» (come oggi `NewProjects`, `LoadFile` e la copia di un esempio) |

**Approvate dall'utente il 22/10 (sera).** Dopo le dieci immagini della sezione 9.4 l'utente ha scritto: «le 10 immagini vanno bene. dall'albero si
possono aprire a pagina intera o staccare dalla finestra? se si, ok. altrimenti va implementato». La disposizione è approvata e **SH5–SH32 valgono
con le opzioni prese nella bozza** (tabella sopra; SH1–SH4, SH7, SH8, SH10–SH14, SH16 come nella sezione 6). Dall'albero un foglio si apriva solo in
una scheda della principale: l'apertura in una nuova finestra e a pagina intera sono state aggiunte dopo il punto di controllo (sezione 9.5).

### 9.3 Comando per le immagini (stanotte)

La prova `ui/check-shell-draft` (`tests/Desktop/ShellDraftChecks.cs`, argomento `--check-shell-draft <cartella>`) è nello stadio `ui` di
`build/ci.ps1`. Apre la bozza fuori dallo schermo, mai attivata, con i moduli del prodotto e un progetto di esempio (un viadotto con una spalla
e una pila, quattro fogli), e scrive dieci PNG: Home, Moduli singoli, Progetti, quattro schede, file singolo, Modelli, Wiki, finestra staccata,
principale con la staccata, schede in «Scuro» (senza salvare la preferenza dell'utente). Controlla anche la struttura a ogni passo, che non
ci siano errori di binding della bozza (quelli delle viste dei moduli e delle viste storiche ospitate vanno in un file a parte) e che gli
esempi della Wiki diano gli stessi dati con `WikiExamples` e con `MainWindow.WikiExamples`. Le immagini sono a 96 DPI (dimensioni logiche).

```
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,ui -Only '^ui/check-shell-draft$' -Tag f5-13-c-bozza"
```

Le immagini sono nella cartella della suite dentro `supporto/artefatti/ci/<data>-f5-13-c-bozza/`, con `binding-errors.txt`,
`binding-errors-altre-viste.txt`, `test.txt` ed `esito.txt`.

### 9.4 Prima esecuzione e correzioni (sera del 22/10)

Agente `f5-13-bozza-prova`, ramo `refactoring/f5-13-bozza-prova` da `refactoring/f5` c1dd1d1c. La corsa mirata del coordinatore
(`ci\20261022-190511-mirate-22-10`) aveva scritto le immagini da 01 a 09 e fallito a `ShellDraftChecks.cs:126`.

| Difetto | Causa | Correzione | Commit |
| --- | --- | --- | --- |
| In 04 e 09 la scheda di un foglio di **progetto** mostra solo la barra del foglio, stirata a tutta altezza, e l'ultima riga della vista del modulo | `SheetBarView` era un `DockPanel` con i comandi a sinistra e l'intestazione come ultimo figlio. Accanto a navigazione e albero restano circa 1040 px: con «Conferma modifiche», annulla e ripristina i comandi li occupano tutti, l'intestazione riceve larghezza zero e il sottotitolo va a capo a ogni lettera (circa 750 px). Il file singolo (05) non ha quei comandi, la finestra staccata (08) non ha la barra | una `WrapPanel` sola, alta almeno 68 px: ciò che non entra va alla riga sotto, la barra è alta quanto le sue righe; titolo e sottotitolo senza a capo. Stessi comandi, stesso ordine | 5042fc7d |
| Controllo combinato a `ShellDraftChecks.cs:126` | `QuietWindow.IsActive` sempre falso: `MoveToNewWindow` chiede l'attivazione due volte (`OnActiveChanged` e `Select`); sul desktop la seconda non parte perché la finestra è già attiva. Il tracker è corretto | `QuietWindow` come una finestra vera (dopo `Activate` è attiva e segnala `Activated`); quattro controlli separati con i conteggi nel messaggio, più quello sulla creazione della finestra; nulla di allentato | b2e03b7a |
| Due errori di binding di un `TreeViewItem` al passo 04, contati tra quelli «di altre viste» | albero del progetto della bozza: lo stile del tema lega gli allineamenti all'`ItemsControl` genitore e un nodo costruito alla ricostruzione dell'albero e tolto prima del layout restava senza sorgente | allineamenti in uno stile implicito del `TreeView` (in `ItemContainerStyle` non bastava); «TreeViewItem» ora conta come errore della bozza | 8203bc84, b2e03b7a |
| Striscia vuota a destra e in basso in ogni immagine | la bitmap era grande quanto la finestra intera, cornice e titolo compresi | immagine dell'area client | b2e03b7a |

Gli errori di binding ora riportano il passo («[04-schede] …»). Resta in `binding-errors-altre-viste.txt` un errore di un `TabItem`
(`TabStripPlacement`) al passo 05, dalla vista dei materiali (acciaio per armature), non dalla bozza.

Esito: `ui/check-shell-draft` PASS 16 controlli, dieci PNG compreso `10-scuro.png`, nessun errore di binding della bozza. Le dieci immagini
per il punto di controllo sono in `C:\Users\g.pacini\Documents\Repository\ANTHEA-f5-ripresa\f5-agenti\f5-13-bozza-prova\immagini\` (area
client, 96 DPI: principale 1586 × 953, staccata 1186 × 823).

Da sapere guardando le immagini, prima di decidere SH5–SH32 (nessuna cambiata dalla prova):
- **Larghezza per il foglio.** Con navigazione (SH23) e albero (SH24) sempre visibili restano circa 1025 px per il foglio. La barra di un
  foglio di progetto va su due righe (04, 09, 10: «Approfondimenti ▾» e l'intestazione sotto). La vista del calcestruzzo (09, 10) scorre in
  orizzontale e la colonna «Composizione» è tagliata. Nella vista del palo verticale (04) il menu «Nessuna riduzione» è tagliato. Con
  l'albero chiuso da «▸» si guadagnano 330 px.
- **File singolo con un archivio aperto (05).** Restano la barra «ANTHEA | Progetti» e le revisioni di «Spalla A», e l'albero evidenzia
  ancora «Pali della spalla». Annulla e ripristina della barra del progetto valgono per il progetto, non per il file (SH21, SH31).
- **Finestra staccata (08).** Ha solo la barra delle sue schede e «Riaggancia». Non ha la barra del foglio né quella del progetto: Salva,
  report e «Conferma modifiche» del foglio staccato sono nella barra della principale, che segue il foglio con il fuoco (SH21, SH29).
- **Scuro (10).** I testi si leggono. I triangoli dell'albero, del modello WPF di base senza token, si vedono appena; l'albero storico usa
  `ProjectTreeItem` con le sue frecce.
- **Pagina Progetti (03).** I dati comuni booleani si leggono «false» (testo del ViewModel, non della disposizione).

### 9.5 Dopo il punto di controllo: dall'albero in una nuova finestra o a pagina intera

Agente `f5-13-c2`, sera del 22/10, ramo `refactoring/f5-13-c2` da `refactoring/f5` 87181e61. Richiesta dell'utente al punto di controllo (9.2).
Prima l'albero apriva un foglio solo in una scheda della principale; «Sposta in una nuova finestra» c'era solo nel menu delle schede e «▸»
chiudeva soltanto l'albero.

**Come ci si arriva.**
- **Menu del nodo di un foglio** (clic destro nell'albero), dopo «Apri»:
  - «Apri in una nuova finestra»;
  - «Apri a pagina intera».

  Le due voci sono nascoste per progetti, sezioni e modelli. Ci sono anche nell'anteprima di una revisione: il foglio si apre in sola lettura.
- **Barra del foglio**, dopo «← Torna…»: l'interruttore «Pagina intera», che a pagina intera diventa «Esci da pagina intera».
- **Esc** nella finestra principale esce dalla pagina intera. Fuori dalla pagina intera il tasto passa oltre, quindi le viste e la rinomina
  tengono il loro Esc.

**Regole** (ViewModel, prove senza WPF in `ShellPlacementChecks`, `--shell`):
- **Apertura.** Il foglio si apre come con «Apri»: il foglio con il fuoco viene confermato prima; un foglio già aperto non si riapre e si usa
  la sua scheda, con lo stesso ViewModel.
- **Nuova finestra.** Una scheda della principale va in una finestra staccata nuova, con `ShellWindowsViewModel.ShowInNewWindow` →
  `MoveToNewWindow` e il tracker. Se la scheda è già in una finestra staccata, quella finestra va in primo piano, una volta sola, senza
  crearne un'altra.
- **Pagina intera** (`ShellViewModel.IsFullPage`):
  - la scheda è selezionata nella principale; navigazione e albero si nascondono e il foglio prende tutta la larghezza;
  - lo stato dell'albero è ricordato e l'uscita lo ripristina;
  - intanto «▸» riapre l'albero; un altro foglio aperto a pagina intera dall'albero lo richiude.
- **Fine della pagina intera.** Vale solo finché al centro c'è la scheda di un foglio e un foglio ha il fuoco, cioè finché a schermo c'è la
  barra con «Esci». Escono dalla pagina intera:
  - le pagine della navigazione (Home, Progetti, Modelli, Moduli singoli, Esempi, Wiki);
  - «← Torna al progetto»;
  - una scheda senza foglio (Wiki, modello);
  - la chiusura del foglio senza un altro foglio con il fuoco.

  Il fuoco che passa a una finestra staccata non la fa uscire.
- **Finestra staccata.** Mostra già solo il foglio: la pagina intera non vale. «Apri a pagina intera» di un foglio staccato porta in primo
  piano la sua finestra, e la principale non cambia. Con il fuoco su un foglio staccato, l'interruttore della barra della principale è
  disabilitato.

**Immagini** (`ui/check-shell-draft`, nuove dopo le dieci della sezione 9.4):
- `11-pagina-intera.png`: i pali della spalla a pagina intera, aperti dall'albero.
- `12-staccata-dall-albero.png`: la stratigrafia della pila aperta dall'albero in una nuova finestra; l'utente è tornato sui pali nella
  principale. La principale è a sinistra e la finestra staccata a destra, in un'unica immagine.

La prova controlla anche:
- il menu del nodo;
- navigazione e albero nascosti, con il foglio largo quanto la finestra;
- Esc inviato alla finestra;
- l'interruttore premuto due volte (peer di automazione);
- un'attivazione sola della nuova finestra;
- la chiusura della finestra, che riporta la scheda nella principale.

Le dodici immagini sono in `C:\Users\g.pacini\Documents\Repository\ANTHEA-f5-ripresa\f5-agenti\f5-13-c2\immagini\`.

**Scelte provvisorie** (criterio prudente, da rivedere in F5.15):

| # | Domanda | Opzione presa |
| --- | --- | --- |
| SH33 | Cosa nasconde la pagina intera | solo navigazione e albero, come chiesto; restano menu, schede, barra del progetto, barra delle revisioni (dice se il foglio è in sola lettura), barra del foglio, riga di stato e «▸» per riaprire l'albero |
| SH34 | «Apri a pagina intera» di un foglio in una finestra staccata | quella finestra in primo piano, senza riagganciare né cambiare la principale |
| SH35 | Quando la pagina intera finisce da sola | quando sparirebbe la barra con «Esci»: una pagina della navigazione, «← Torna…», una scheda senza foglio, nessun foglio con il fuoco. Il fuoco su una finestra staccata non la fa finire |
| SH36 | Pagina intera e cambio di scheda | è uno stato della finestra, non del foglio: passare a un'altra scheda di un foglio la mantiene; non si ricorda fra le sessioni |
| SH37 | Voci nuove nel menu dei nodi che non sono fogli | nascoste (le altre voci del menu restano disabilitate come nella bozza) |

Scelte della notte del 22–23/10 (agente `f5-13-c3`, sezione 10.2), stesso criterio:

| # | Domanda | Opzione presa |
| --- | --- | --- |
| SH38 | Badge di coerenza nell'albero | pulsante con il numero di conflitti e avvisi o «✓», con il titolo come nome di automazione e il suggerimento di oggi, come `AddCoherenceBadge`; apre il confronto della sezione (la bozza mostrava il titolo come testo, senza comando) |
| SH39 | Elenco dei moduli la cui vista segue la finestra | tabella della shell in Presentation (`SheetLayout.FitsWindow`, l'elenco di `MainWindow.LoadSheetEditor`), non un membro dei descrittori dei moduli come dice la proposta 4.3: i descrittori non si toccano mentre i moduli migrano. Si può spostare nei descrittori al passaggio della Release |
| SH40 | Finestre piccole | la shell intera tiene 1120 × 680, il cruscotto di MainWindow, e scorre; le pagine 870 × 680; i fogli che non seguono la finestra 1120 × 600 dentro la scheda. Navigazione e albero restano (SH23, SH24): «▸» e la pagina intera liberano spazio. Non prese: chiudere da solo l'albero o passare alla pagina intera sotto una certa larghezza |
| SH41 | Moduli ancora storici nella shell nuova (R5) | si aprono come li sceglie la Release (`ModuleImplementations`): i 12 storici nel loro `SheetEditor` dentro la scheda, gli altri con il ViewModel; nella build UiTests `--module-impl` cambia la scelta, anche con `--interactive-shell`. La prova della bozza tiene tutti i moduli MVVM per le immagini del punto di controllo e disegna a parte la shell della Release (immagine 21) |
| SH42 | Avviso nella scheda ricaricata (SH9) | «Aggiornato da «foglio»: campo, campo e altre n proprietà.» (cinque campi al più, come la riga di stato) in una riga sotto la barra del foglio, fondo `Token.Calculated`; solo dopo la conferma di un altro foglio con i dati condivisi propagati, non dopo annulla, revisioni, confronto o spostamenti; resta finché la scheda non viene confermata (perde il fuoco o «Conferma modifiche»). Per un foglio di una finestra staccata si vede nella barra della principale, come la barra del foglio (SH29) |
| SH43 | Clic singolo sul nome di un modello nell'albero | lo apre dopo il tempo del doppio clic, come le sezioni; in MainWindow il nodo del modello era un pulsante e si apriva subito. Il doppio clic lo apre comunque |
| SH44 | Indicatore del trascinamento | linea di due pixel con il punto prima o dopo la riga, come `SectionDropAdorner`, in `Token.Accent`; per l'inserimento dentro una sezione il contorno della riga nello stesso colore, al posto del fondo #D4E6F7 dell'intestazione (nessun token, e un fondo disegnato sopra coprirebbe il nome). Schede del catalogo al passaggio del mouse: `Token.Hover.Row` con il bordo `Token.Selection.Border`; presa in `Token.Drawing.Axis` |
| SH45 | Grandezza dei nomi nell'albero | sui token tipografici: progetti `Font.Title` (22, oggi 20), sezioni di primo livello `Font.Heading` (16, oggi 17), le altre sezioni e i fogli `Font.Subheading` (14, oggi 15 al secondo livello), i fogli non in grassetto |
| SH46 | Dialoghi della shell | nel dizionario dell'applicazione (`ShellDialogs.xaml` unito da App.xaml, come gli altri dialoghi delle viste MVVM), mostrati dal servizio dei dialoghi: confronto (900 × 650), collegamento dei sondaggi, riepilogo delle revisioni, nuova revisione e contenuti del report Word. Gli ultimi due prima non avevano vista nella shell. Il confronto conferma prima la scheda attiva, come `ShowCoherence` |
| SH47 | F1 e «?» dei fogli storici nella shell | `WikiContextHelp` (file storico, non `SheetEditor`) cerca dopo MainWindow una finestra della shell (`IShellWikiHost`) e apre la scheda Wiki; con MainWindow, quindi nella Release, nulla cambia |

## 10. Parte (c): stato, verifiche e cosa resta

Fatto il 22/10, fino al punto di controllo:

| Commit | Contenuto |
| --- | --- |
| a98b7207 | `SheetFileExports` sul codice di `MvvmSheetEditor` |
| 1d6a2383 | `ShellWindowTracker` (`IWindowService`) |
| 6128fdfe | menu «Aspetto», avvio, Wiki, Modelli, barra del foglio, centro della finestra |
| cd9a337c | bozza XAML in `X.Desktop/Views/Shell` |
| 71706664 | `ui/check-shell-draft` (`--check-shell-draft`), compilata e non eseguita |
| commit di chiusura | questa sezione, riga in piano.md |

Verifiche di giorno: `--shell` PASS 403 (89 controlli nuovi); `Architecture.Tests` PASS 574 con P6 e P7 sulle viste nuove, baseline invariate;
`qa/no-test-code` PASS; profilo `quick` `20261022-110519-f5-13-c-vm` PASS 55, 0 NEW-FAIL; profilo `standard` finale nel rapporto
dell'agente e in `ANTHEA-f5-ripresa\f5-agenti\f5-13-c\avanzamento.md`.

Suite `ui` da eseguire stanotte per questo lavoro:
- `ui/check-shell-draft`: la bozza e le immagini per il punto di controllo;
- le suite che passano da `MvvmSheetEditor` (esportazioni ora in `SheetFileExports`): le 11 `ui/check-*-view` (in particolare
  `ui/check-pile-group-view`, palificata MVVM nella Release; `ui/check-materials-view`, capitolo dei materiali; `ui/check-fondazioni-view`, PDF
  del generatore; `ui/check-bridge-design-view`, BD7), `ui/check-module-host-offscreen`, `ui/f3-equivalence` e `ui/f3-report-chapters`.
  Girano tutte nel profilo full.

Dopo il punto di controllo, sulla disposizione scelta:
- viste definitive al posto della bozza, con i comandi ancora segnaposto (sezione 9.1), il trascinamento e la rinomina nell'albero,
  l'adattamento ai piccoli schermi (con l'elenco dei moduli non scalati come dato del modulo, proposta 4.3), Ctrl+Z/Ctrl+Y;
- radice di composizione della shell (servizi, `ShellWindowTracker` con `DetachedWindowHandle`, `IWikiNavigator` che apre la scheda della
  Wiki, avvio con `StartupRequest`), avviabile nella build di prova con un argomento; la Release resta `MainWindow` fino a (f)–(g);
- ospite dei moduli ancora storici nella shell nuova, se servirà (proposta 5.2, rischio R5): oggi la shell nuova apre solo i moduli MVVM;
- report short della sezione c.a. MVVM in `SheetFileExports` quando F5.10 S9 lo porta in `MvvmSheetEditor`;
- prove WPF della shell (finestre staccate reali, schede, fuoco), poi le parti (d)–(g).

Per riprendere:

```
cd C:\Users\g.pacini\AppData\Local\Temp\aw-f5-13-c
dotnet build tests/ANTHEA.Presentation.Tests/ANTHEA.Presentation.Tests.csproj -c Release
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll supporto/artefatti/f5-13-c/presentation --shell
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Profile standard -Tag <tag>"
```

### 10.1 Dopo il punto di controllo (sera del 22/10)

Agente `f5-13-c2`, ramo `refactoring/f5-13-c2` da `refactoring/f5` 87181e61. Disposizione approvata (9.2). Per prima cosa l'apertura dall'albero
chiesta dall'utente (9.5); poi una parte di quel che restava della parte (c), in commit piccoli.

| Commit | Contenuto |
| --- | --- |
| bfe1f875 | 9.2: disposizione approvata, SH5–SH32 con le opzioni della bozza |
| 81230f06 | ViewModel: «Apri in una nuova finestra», «Apri a pagina intera», `IsFullPage`; `ShellPlacementChecks` |
| c1dc8689 | viste: menu del nodo, «Pagina intera»/«Esci da pagina intera», Esc; immagini 11 e 12 |
| 62ead12e | sezione 9.5 |
| 8b204715 | Ctrl+Z/Ctrl+Y del progetto; riepilogo della sezione («Duplica sezione», «Fogli e sottosezioni», «Tutti i dati comuni»); `ShellWikiNavigator`; radice di composizione `ShellComposition` e `--interactive-shell` |
| 0c2db08f | albero: pulsante con l'icona del modulo, rinomina in linea (doppio clic, F2, Invio, Esc), Invio che apre; icone nel catalogo dei moduli |
| commit di chiusura | questa sezione |

**Cosa c'è adesso**, oltre alla 9.5. Le regole vengono dal codice storico citato:

- **Ctrl+Z / Ctrl+Y** (`ProjectWorkspace.PreviewKeyDown`):
  - valgono dove `MainWindow` mostrava l'area progetti, cioè sulla pagina Progetti o su un foglio del progetto, con un progetto modificabile;
  - non nei file singoli, nelle altre pagine, nei modelli o nell'anteprima di una revisione;
  - servono il solo Ctrl, fuori da caselle di testo e combo;
  - il tasto è gestito anche quando non c'è nulla da annullare;
  - funzionano nella principale e nelle finestre staccate (`ShellViewModel.HistoryKeysApply`, `RestoreHistory`; vista `ProjectHistoryKeys`).
- **Riepilogo della sezione** (`BuildSectionOverview`):
  - cinque dati comuni; gli altri nel gruppo chiuso «Tutti i dati comuni»;
  - «Duplica sezione», fuori dalla sola lettura;
  - il gruppo chiuso «Fogli e sottosezioni»: un foglio si apre nella sua scheda, una sottosezione mostra il suo riepilogo;
  - «Genera report» e «Confronto e avvisi» restano segnaposto.
- **Albero** (`ConfigureProjectNode`, `BeginProjectRename`):
  - il pulsante con l'icona del modulo apre il foglio;
  - il doppio clic sul nome, F2 e «Rinomina» avviano la rinomina in linea; Invio o la perdita del fuoco salvano, Esc annulla;
  - Invio sul nodo lo apre;
  - un modello si apre con il doppio clic.
- **Catalogo dei moduli**: icona del modulo nelle schede (`Ui.ModuleIcon`, disegnata in codice come oggi).
- **Radice di composizione** `X.Desktop/Services/ShellComposition.cs`:
  - fabbrica dei fogli dal registro dei moduli, con il limitatore;
  - file ed esempi installati, esportazioni, menu «Aspetto»;
  - finestre staccate con `DetachedWindowHandle` e Ctrl+Z;
  - `ShellWikiNavigator`: F1 e le guide dei fogli aprono la scheda Wiki. Prima che la shell esista e dopo che si chiude, la richiesta va alla Wiki dei
    servizi di partenza.

  La prova della bozza costruisce la shell da questa radice.
- **Avvio a mano, solo nella build UiTests**: `X.Desktop\bin\UiTests\net8.0-windows\ANTHEA.exe --interactive-shell [archivio [--models] | --wiki
  articolo]`.
  - Usa i servizi del prodotto (`DesktopComposition`), l'aspetto dell'utente e il registro degli errori.
  - **La Release apre ancora `MainWindow`**: `App.OnStartup` non è cambiato. La radice è compilata anche nella Release ma nessuno la chiama.

**Verifiche**:
- `--shell` di Presentation.Tests PASS 467 (64 controlli nuovi: `ShellPlacementChecks`, `ShellFinalViewsChecks`).
- Profilo `quick` `20261022-230600-f5-13-c2-quick` su 62ead12e: PASS 55, NEW-FAIL 0.
- `ui/check-shell-draft` `20261022-233437-f5-13-c2-bozza5` su 0c2db08f: PASS 41 controlli, dodici PNG, nessun errore di binding della bozza. In
  `binding-errors-altre-viste.txt` restano errori `TabItem`/`TabStripPlacement` delle viste dei moduli (materiali, stratigrafia), non della bozza.
- Corsa finale (standard più `ui/check-shell-draft` e `ui/check-module-host-offscreen`): esito nel rapporto dell'agente e in
  `ANTHEA-f5-ripresa\f5-agenti\f5-13-c2\avanzamento.md`.

**Cosa resta della parte (c)**:
- riepilogo: «Confronto e avvisi» (vista del dialogo di `CoherenceViewModel`), «Riepilogo modifiche» (`RevisionSummaryViewModel`), «Dettaglio
  avvisi» come gruppo chiuso, «Genera report» con la parte (e);
- albero:
  - trascinamento: i ViewModel `PreviewDrop`/`Drop` ci sono, manca la vista con l'indicatore di inserimento;
  - clic singolo su una sezione che ne apre il riepilogo dopo il tempo del doppio clic (`ScheduleProjectSectionClick`);
  - grandezza del testo per profondità;
  - triangoli in «Scuro»;
- catalogo:
  - «Riprendi» per modulo nel pulsante della scheda (`ModuleCatalogViewModel.ButtonText`);
  - schede di «Schede da aggiungere» trascinabili, o con il doppio clic che aggiunge;
- adattamento ai piccoli schermi (proposta 4.3); avviso nella scheda ricaricata (SH9);
- ospite dei moduli storici nella shell nuova (R5); report short della sezione c.a. in `SheetFileExports`;
- prove WPF della shell con finestre reali e fuoco; poi le parti (d)–(g).

Aggiornamento della notte del 22–23/10: tutto l'elenco è fatto (sezione 10.2) tranne «Genera report», che viene con la parte (e), e le prove WPF
con finestre reali e fuoco.

Per riprendere:

```
cd C:\Users\g.pacini\AppData\Local\Temp\aw-f5-13-c2
dotnet build tests/ANTHEA.Presentation.Tests/ANTHEA.Presentation.Tests.csproj -c Release
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll supporto/artefatti/f5-13-c2/presentation --shell
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,ui -Only '^(ui/check-shell-draft|ui/check-module-host-offscreen)$' -Tag <tag>"
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Profile standard -Tag <tag>"
```

### 10.2 Il resto della parte (c) (notte del 22–23/10)

Agente `f5-13-c3`, ramo `refactoring/f5-13-c3` da `refactoring/f5` 3733b1a6 (con la release AF delle librerie). Le voci della sezione 10.1, una per
commit, con testi e comportamento di MainWindow; le scelte nuove sono SH38–SH47 (sezione 9.5).

| Commit | Contenuto |
| --- | --- |
| 8505e216 | riepilogo della sezione: «Confronto e avvisi», «Riepilogo modifiche», «Dettaglio avvisi»; dialoghi della shell |
| a3d63a1b | albero: trascinamento con indicatore, clic singolo su una sezione, testo per profondità, frecce in «Scuro» |
| 517277d4 | catalogo: «Riprendi» per modulo, schede da aggiungere trascinabili e con il doppio clic |
| 4d5de70b | piccoli schermi; avviso nella scheda ricaricata (SH9) |
| 28a8ee6b | ospite dei moduli storici nella shell nuova (R5) |
| 4ed99d4d | report short della sezione c.a. provato dalla shell |
| commit di chiusura | questa sezione, SH38–SH47, voce in piano.md |

**Cosa c'è adesso e come ci si arriva** (build UiTests, `ANTHEA.exe --interactive-shell`; la Release apre ancora MainWindow):

- **Riepilogo della sezione** (pagina «Progetti», `ProjectPresentation.BuildSectionOverview`), nell'ordine di oggi:
  - «Modelli di calcolo» con i pulsanti dei modelli e «+ Importa un modello» fuori dalla sola lettura (`AddModelsOverview`);
  - «Controlli» con i due stati («!  1 conflitto», «✓  0 avvisi») e «Confronto e avvisi», che apre il dialogo «Confronto · sezione»
    (`ShowCoherence`): conflitti con «Uniforma a questo» e la scelta della tipologia, strati mancanti, avvisi, «Collegamento stratigrafie» con il
    suo dialogo. Il confronto si apre anche dal badge di coerenza dell'albero (SH38). Prima conferma la scheda attiva;
  - «Riepilogo modifiche» nella scheda «Revisioni», con revisioni archiviate: il dialogo «Revisioni · sezione» (`ShowProjectRevisions`);
    «Mostra questa revisione» lo chiude e mostra la revisione in sola lettura, con la sua barra;
  - «Dettaglio avvisi», gruppo chiuso, solo con avvisi.

  I dialoghi sono in `X.Desktop/Views/Shell/ShellDialogs.xaml` (SH46), con la nuova revisione e i contenuti del report Word, che prima non avevano
  vista nella shell.
- **Albero** (`ProjectHierarchy`, `ProjectSectionReorder`):
  - un foglio si trascina dalla sua riga, un progetto o una sezione dal nome, fuori dalla sola lettura; sopra un nodo il suggerimento
    («Inserisci in: …», «Prima di …», «Dopo …», «Sposta dentro: …») copre l'istruzione dell'intestazione e l'indicatore è disegnato sopra la riga
    (SH44); il rilascio passa dalle regole di `PreviewDrop`/`Drop`, con le domande di oggi (spostamento con i dati condivisi);
  - un clic sul nome di un progetto o di una sezione ne mostra il riepilogo dopo il tempo del doppio clic del sistema; un'altra pressione nella
    finestra lo annulla, quindi il doppio clic rinomina come prima; un modello si apre (SH43);
  - nomi per profondità sui token (SH45); nodi sul modello di `ProjectTreeItem`, con le frecce in `Token.InkSecondary`, leggibili in «Scuro»;
  - il riepilogo di una sezione la seleziona nell'albero, come `ShowProjectOverview`: «+ Sezione» e il doppio clic del catalogo vanno lì.
- **Cataloghi**: in «Moduli singoli» il modulo dell'ultimo foglio dice «Riprendi»; le schede di «Schede da aggiungere» hanno icona e presa, si
  trascinano su una sezione (una copia) e con il doppio clic aggiungono il foglio alla sezione selezionata e lo aprono (`MainWindow.AddSheet`).
- **Piccoli schermi** (SH40): la finestra resta nel monitor (`DisplayAdaptation.Attach`); la shell tiene 1120 × 680 e scorre; le pagine 870 × 680;
  un foglio che non segue la finestra (sezione c.a., fondazioni, stratigrafia, palificata, spettri, paratie) tiene 1120 × 600 e scorre nella
  sua scheda (`SheetLayout`, SH39; `MinimumSizeViewport` nelle viste).
- **Avviso SH9** (SH42): dopo «Aggiorna tutti i fogli collegati» la scheda ricaricata dice «Aggiornato da «foglio»: …» in una riga sotto la barra.
- **Moduli storici (R5, SH41)**: `ShellComposition` apre ogni modulo come la Release (`ReleaseSheetFactory`). I 12 moduli storici aprono il
  `SheetEditor`, invariato, dietro `HistoricSheetViewModel` (l'adattatore della proposta 5.2): conferma, dati, risultato, calcolo, report e
  sola lettura passano all'editor; copie, sezione di un muro, terreno e report diventano i comandi di `ISheetHost`; i file li scrive l'editor
  (`DesktopSheetExports`). La vista (`HistoricSheetHost`) prende il controllo dell'editor, che resta lo stesso fra principale e finestre staccate.
  F1 e i «?» aprono la scheda Wiki (SH47). Con `--module-impl modulo=mvvm` il modulo apre la vista MVVM, come nel banco di MainWindow.
- **Report short c.a.**: «Report short» nella barra e «Report short CLS…» nel menu File, per la vista MVVM (`SheetFileExports.ShortReport`) e per
  quella storica (`SheetEditor.BuildShortReport`); provato dalla shell.

**Verifiche**:
- `--shell` di Presentation.Tests: PASS 513 (46 controlli nuovi: `ShellOverviewChecks`, `ShellTreeDragChecks`, `ShellCatalogueChecks`,
  `ShellLayoutChecks`). L'adattatore storico è WPF e si prova nella bozza.
- `ui/check-shell-draft`: PASS 82 controlli, 21 immagini, nessun errore di binding della bozza. Le immagini nuove: 13-riepilogo, 14-confronto,
  15-revisioni, 16-trascinamento, 17-moduli-riprendi, 18-schede-da-aggiungere, 19-avviso-ricaricata, 20-piccolo-schermo (900 × 620),
  21-ospite-storico; le 01–12 cambiano dove cambiano riepilogo e albero. In `binding-errors-altre-viste.txt` restano i due errori `TabItem` della
  vista dei materiali, già noti.
- `ui/check-module-host-offscreen` PASS 51; `Architecture.Tests` PASS 574 a ogni commit, baseline invariate (nessun `DispatcherTimer` nuovo:
  l'attesa del clic singolo è un ritardo che torna sul thread della finestra).
- Corsa finale (standard più `ui/check-shell-draft` e `ui/check-module-host-offscreen`): esito nel rapporto dell'agente e in
  `ANTHEA-f5-ripresa\f5-agenti\f5-13-c3\avanzamento.md`. Immagini in `ANTHEA-f5-ripresa\f5-agenti\f5-13-c3\immagini\`.

File storici toccati: `X.Desktop/Wpf/WikiContextHelp.cs` (SH47, solo il ramo senza MainWindow), `App.xaml` (una riga, SH46). Nessun file di
`SheetEditor`, `MainWindow`, `MvvmSheetEditor` o dei moduli. Suite da eseguire con il profilo full, oltre alle due della bozza: `ui/check-wiki-offscreen`
(WikiContextHelp) e le suite delle viste MVVM che aprono dialoghi (App.xaml).

**Cosa resta della parte (c)**:
- «Genera report» del riepilogo e «Report Word della sezione» dell'albero, con la parte (e) (report congelati); fatto nella parte (e), sezione 12.3;
- prove WPF della shell con finestre reali, fuoco e mouse (le prove di stanotte sono fuori dallo schermo, con finestre mai attivate: il
  trascinamento e il clic singolo passano per i metodi che gli eventi chiamano, non per `DoDragDrop` e il mouse).

Per riprendere:

```
cd C:\Users\g.pacini\AppData\Local\Temp\aw-f5-13-c3
dotnet build tests/ANTHEA.Presentation.Tests/ANTHEA.Presentation.Tests.csproj -c Release
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll supporto/artefatti/f5-13-c3/presentation --shell
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,ui -Only '^(ui/check-shell-draft|ui/check-module-host-offscreen)$' -Tag <tag>"
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Profile standard -Tag <tag>"
X.Desktop\bin\UiTests\net8.0-windows\ANTHEA.exe --interactive-shell [archivio [--models] | --wiki articolo] [--module-impl modulo=mvvm|historic]
```

## 11. Parte (d): stato statico e calcoli in parallelo

Agente `f5-13-d`, 22/10/2026, ramo `refactoring/f5-13-d` da `refactoring/f5` a 10110f78. Fonti: decisione E5 del 15/10 ([decisioni](decisioni.md)),
F2.11c e registro F2-26 ([piano](piano.md)), [proposta](f5-proposta-architettura.md) (riga dello stato statico della sezione 1, sezione 4.6,
regola P3 della sezione 7.4, rischio R6), baseline P3 `tests/ANTHEA.Architecture.Tests/f5-static-baseline.json` (126 voci a 10110f78). Regola
diurna del 22/10: nessuna prova WPF eseguita di giorno.

### 11.1 Perimetro

E5 assegna a F5.13 lo stato statico di ponte, muri, pali, fondazioni e Wiki; la regola P3 chiede zero voci per queste aree alla fine di F5.13.
Le aree sono lette così, su tutto il codice di produzione (`X.*` e `src/*`, come P3):

- **ponte**: Bridge Design (`str_bridge_design`) e sezione composta da ponte (`str_mista_ponte`): `BridgeConcept`, `BridgePricing`,
  `BridgeSection`, le viste `BridgeDesignModel` e `BridgeResultView`, l'import dei prezzari, i dati ambientali, i disegni del ponte;
- **muri**: muri di sostegno (`geo_muri_sostegno`), con la stabilità globale (`Slope`, usata solo dai muri) e i disegni dei muri;
- **pali**: palo e micropalo verticale e orizzontale, risposta elastica, palificata, con le loro presentazioni e l'editor storico degli strati;
- **fondazioni**: fondazioni superficiali (`ANTHEA.FoundationWorkspace`);
- **Wiki**: `ANTHEA.Wiki` e la tavolozza della vista della Wiki.

Le paratie non hanno voci. `JsonData.observer` (`JsonFallbackHook.Observer`) è già interno e per contesto da F2.11c (`5a66ff99`, un
AsyncLocal): resta una voce P3 sintattica (proprietà statica con setter sopra l'AsyncLocal), fuori dalle cinque aree; nessuna correzione.

### 11.2 Inventario e correzioni

«Inizializzatore» è l'inizializzatore statico del tipo: una volta, al primo uso, con la garanzia del runtime sui costruttori statici. Per
i cataloghi nessuno scrive dopo l'inizializzatore: lo prova la compilazione con i tipi immutabili, perché una scrittura non compilerebbe più.
Le scelte dei tipi:
- array → `ImmutableArray<T>` costruito una volta, come A12 (stessi elementi, stesso ordine);
- dizionari enumerati (liste dei fogli e dei report nell'ordine delle chiavi) → `ReadOnlyDictionary` sullo stesso `Dictionary`, che conserva
  l'ordine di inserimento; `FrozenDictionary` non lo garantisce e serve solo ai dizionari di sole ricerche;
- `Array.IndexOf(c, v)` → `c.IndexOf(v)`: stesso confronto, −1 se assente.

| # | Area | File e membro | Chi lo scrive e quando | Correzione | Commit |
| --- | --- | --- | --- | --- | --- |
| D1 | ponte | `src/ANTHEA.Application/BridgeDesign/BridgeDesignModel.cs`, `Views` (`ConditionalWeakTable<ModuleCalculation, Result>`) | `Read` alla prima lettura di ogni calcolo: thread del calcolo (`BridgeDesignAnalysis`: progetto, alternativa fissata, candidati dell'ottimizzazione), thread dell'interfaccia (ViewModel, vista storica), figure del report | la vista vive nel calcolo: `ModuleCalculation.View<T>` | a687a871 |
| D2 | ponte | `src/ANTHEA.Application/Bridge/BridgeResultView.cs`, `Views` (`ConditionalWeakTable<ModuleCalculation, BridgeResultView>`) | `Read` alla prima lettura: ViewModel della sezione composta, vista storica (`BridgeWorkspace` dopo il calcolo di fondo), figure del report | come D1 | a687a871 |
| D3 | ponte | `X.Calculations/BridgeConcept.cs`, `Families`, `Site`, `Layout`, `Section`, `Substructure`, `Rates`, `Assumptions`, `Obstacles`, `Soils`, `Piers`, `Foundations`; `BridgeConcept.AdvancedSchema.cs`, `DeckTypes`, `AdvancedSection`, `AdvancedAssumptions` (14 array pubblici) | inizializzatore (copie dei cataloghi di sola lettura di GPC.Design); letti dai valori iniziali, dalle facciate di Application (che copiano), dal report di Bridge Design e dalle prove | `ImmutableArray<T>` | d7ff277f |
| D4 | ponte | `X.Calculations/BridgePricing.cs`, `AnalysisMessages` (array privato di coppie espressione regolare e testo) | inizializzatore; letto nella traduzione dei rifiuti delle analisi prezzi | `ImmutableArray<T>` | d7ff277f |
| D5 | ponte | `X.Calculations/BridgeSection.cs`, `Standards`, `ConcreteNames`, `SteelNames`, `RebarNames`, `SectionTypes`, `PhaseKinds`, `HomoModes`, `LoadReferences`; `.Analysis.cs`, `CalculationMethods`, `NonlinearCreepModes`, `ConcreteTensionModes`; `.Details.cs`, `StiffenerSides`, `SupportLocations`, `DiaphragmKinds`; `.Response.cs`, `ResponseModes`, `ResponseOrigins`; `.SlabLayout.cs`, `BottomRebarReferences` (17 array pubblici) | inizializzatore; letti dal calcolo (indici degli enum della libreria in `CheckerInput`), dall'editor e dai report | `ImmutableArray<string>`; `Array.IndexOf(c, v)` diventa `c.IndexOf(v)` | d7ff277f |
| D6 | ponte | `src/ANTHEA.Application/BridgeDesign/PriceCatalogue.cs`, `PriceCatalogueImport.Headers` | inizializzatore; letto dall'import e dal modello del prezzario | `ImmutableArray<string>` | d7ff277f |
| D7 | ponte | `src/ANTHEA.Serialization/BridgeEnvironmentData.cs`, `Keys` (privato) | inizializzatore; letto alla lettura dei dati ambientali | `ImmutableArray<string>` | d7ff277f |
| D8 | ponte | `X.Desktop/Wpf/BridgeDrawing.cs`, `Colors`; `BridgeOptimizationPlot.cs`, `Palette`, `FamilyLabels` (array privati) | inizializzatore; letti dai disegni sul thread dell'interfaccia | `ImmutableArray<T>` | e5005cc7 |
| M1 | muri | `src/ANTHEA.Application/Walls/WallModel.cs`, `Views` (`ConditionalWeakTable<ModuleCalculation, Result>`) | `Read` alla prima lettura: ViewModel dei muri alla pubblicazione, vista storica dopo il calcolo di fondo, figure del report | come D1 | a687a871 |
| M2 | muri | `X.Calculations/RetainingWall.cs`, `Families`, `GeometryFields`, `FoundationFields`, `LoadFields`, `MaterialFields`, `SeismicFields`; `.Actions.cs`, `ActionTypes`, `ActionCategories`; `.Materials.cs`, `SectionMaterialKeys`; `.Soils.cs`, `FrictionModes`; `.Library.cs`, `MemberNames` (privato) | inizializzatore; letti dal calcolo (indici degli enum della libreria), dalle facciate di Application, dal report dei muri | `ImmutableArray<T>`; `Array.IndexOf` come D5 | 15111ba0 |
| M3 | muri | `X.Calculations/Geotechnics/SlopeStability.Models.cs`, `Slope.Notes` | inizializzatore; letto da `FromLibrary` nella stabilità globale dei muri (thread del calcolo), che ne fa una copia per ogni risultato | `ImmutableArray<string>` | 15111ba0 |
| M4 | muri | `X.Desktop/Wpf/RetainingWallDrawing.Inspection.cs`, `LayerColors` | inizializzatore; letto dai disegni e dalla griglia degli strati (vista nuova e storica) sul thread dell'interfaccia | `ImmutableArray<string>` | e5005cc7 |
| P1 | pali | `X.Calculations/Calcolo.cs`, `Indagate` (privato), `Verticali`, `Tipi` (privato), `Parametri` | inizializzatore; letti dal calcolo del palo verticale e del palo orizzontale e dalle liste del foglio e del report (nell'ordine delle chiavi) | `Indagate` `ImmutableArray<string>`; i dizionari `ReadOnlyDictionary`, che conserva l'ordine di inserimento delle chiavi | 33f61da0 |
| P2 | pali | `X.Calculations/Micropali.cs`, `BustamanteDoix.Terreni`, `BustamanteDoix.Curve`, `Chs.Taglie` (privato), `Chs.Catalogo` | inizializzatore (dai cataloghi di GPCChecker.Geotechnics e di ModelData); letti dal calcolo, dai grafici e dalle liste del foglio | `ReadOnlyDictionary` con valori `ImmutableArray` (anche gli intervalli α e i punti degli abachi) | 33f61da0 |
| P3 | pali | `X.Calculations/Nq.cs`, `RapportiMedio`, `RapportiGrande` | inizializzatore (copie della libreria); letti dai grafici del palo verticale | `ImmutableArray<double>` | 33f61da0 |
| P4 | pali | `X.Calculations/PaloOrizzontale.cs`, `Messages`, `Mechanisms`, `Sides` (privati) | inizializzatore; letti dal calcolo del palo orizzontale | `Messages` `FrozenDictionary` (solo ricerche); gli array `ImmutableArray<string>` | 33f61da0 |
| P5 | pali | `X.Calculations/ElasticHorizontalPile.cs`, `Laws`; `.Reinforcement.cs`, `ReinforcementKeys`, `InnerRingKeys`, `SegmentKeys`; `.ReinforcementPresentation.cs`, `OrdinaryApprovalKeys` | inizializzatore; letti dal calcolo e dalla presentazione della risposta elastica e delle armature | `ImmutableArray<string>`; `Array.IndexOf` come D5 | 33f61da0 |
| P6 | pali | `X.Calculations/HorizontalPileGroup.cs`, `MethodNames`, `LayoutNames` | inizializzatore; letti dal calcolo della palificata e dal suo report | `ImmutableArray<string>`; `Array.IndexOf` come D5 | 33f61da0 |
| P7 | pali | `src/ANTHEA.Application/Presentation/HorizontalPreview.cs`, `ChsKeys`; `HorizontalSheetPresentation.cs`, `ConcreteClasses` (privati) | inizializzatore; letti dall'anteprima e dalla presentazione del palo orizzontale | `ImmutableArray<string>`; `ReadOnlyDictionary` (la ricerca per valore prende la prima classe nell'ordine del catalogo) | 33f61da0 |
| P8 | pali | `X.Desktop/Wpf/HorizontalSoilEditor.cs`, `Widths` | inizializzatore; letto dall'editor storico degli strati sul thread dell'interfaccia | `ImmutableArray<double>` | e5005cc7 |
| F1 | fondazioni | `src/ANTHEA.FoundationWorkspace/FoundationDocument.cs`, `Methods`, `Shapes`, `Conditions`, `Profiles`, `Compressions`, `Fields` | inizializzatore; letti dalla convalida del documento, dalla presentazione e dai campi del ViewModel | `ImmutableArray<T>` | 2ea3a885 |
| F2 | fondazioni | `src/ANTHEA.FoundationWorkspace/FoundationMaterials.cs`, `LegacySoilKeys`, `SoilFields` | inizializzatore; letti dalla convalida e dal report di progetto | `ImmutableArray<T>` | 2ea3a885 |
| W1 | Wiki | `src/ANTHEA.Wiki/WikiCatalog.cs`, `Articles`, `Chapters`, `References` (array dei file JSON incorporati), `Aliases` (dizionario) | inizializzatore del tipo, al primo uso della Wiki (vista, aiuto contestuale, ricerca, barra del foglio); poi solo letture | `ImmutableArray<T>`; `Aliases` `FrozenDictionary` (solo ricerche) | 0c00660f |
| W2 | Wiki | `X.Desktop/Wpf/WikiVector.cs`, `WikiPalette.Dark` (proprietà statica con setter) | `WikiView.SetDark` al cambio di aspetto, sul thread dell'interfaccia; è una facciata di `Appearance`, lo stato globale del tema | proprietà di sola lettura; `SetDark` imposta il tema con la stessa chiamata `Appearance.Set(…, false)` | e5005cc7 |
| R1 | ponte | `src/ANTHEA.Reports/Generators/ReportBridge.cs`, `ReportBridge.Sections` | inizializzatore; letto dal report | **non toccato**: codice dei report congelato dall'8/10 | — |
| R2 | pali | `src/ANTHEA.Reports/Generators/ReportWord.cs`, `ReportWord.Sezioni` (sezioni del report della palificata) | inizializzatore; letto dal report | **non toccato**: come R1 | — |

Nessun campo statico non readonly è scritto durante un calcolo nelle cinque aree: P3 non ne trova (i soli campi statici non readonly del
prodotto sono `Appearance.initialized` ed `ErrorLog.attached`, dell'interfaccia). Lo stato statico scritto durante un calcolo erano le tre
`ConditionalWeakTable` delle viste (D1, D2, M1): sicure fra thread, ma condivise da tutti i fogli; ora ogni vista vive nel suo calcolo.

### 11.3 Stato statico fuori da P3, voci lasciate e seguiti

Fuori da P3, nelle cinque aree, controllati e lasciati come sono:
- **istanze condivise di tipi mutabili delle librerie**, lette e mai scritte dal calcolo: `Calcolo.Ntc` (usata solo per costruire
  `Verticali`) e `PaloOrizzontale.Ntc` (`StandardNTC2018Geotechnics`: `PileCorrelationFactors` e `Combinations` leggono dizionari interni),
  `Chs.Acciaio` (`SteelMaterial`: `MicropileTube.Weight`, `CalculateFyd`, `Density`). I tipi hanno setter pubblici: seguito per le librerie
  (norme e materiali immutabili, oppure un'istanza per calcolo, attenzione al Guid del materiale);
- **Wiki**: i `Lazy` dell'indice di ricerca e degli asset (costruiti una volta in modo sicuro fra thread, poi solo letti) e le proprietà array
  dei record `WikiArticle` (lette dal JSON, mai scritte). Da rivedere con la ristrutturazione della Wiki.

Voci P3 delle cinque aree lasciate:
- R1 `ReportBridge.Sections` e R2 `ReportWord.Sezioni`: codice dei report congelato dall'8/10. Si correggono con la revisione dei report a fine
  refactoring (domanda D-1 in 11.6).

Voci P3 fuori dalle cinque aree (37 a fine passo, invariate):
- **sezione c.a.** (agente `f5-10-s9`, non toccate): `ConcreteWorkspace.CommonCoefficients`, `ConcreteSectionViewport.Contours`,
  `ConcreteWorkspace.AllCalculations` (X.Desktop); `ConcreteTimeEffectsViewModel.Inputs` (strumenti c.a., `ANTHEA.DesignTools.Presentation`);
- **report congelati**: R1, R2 e `PdfFonts.Families`, `Files`, `Maps` (cache dei font PDF, ammesse da E5 con il loro lock);
- **interfaccia globale**: `Appearance` (7 voci), `ErrorLog.attached`, `StratigraphyDrawing.LayerColors`;
- **stratigrafia, progetti, documenti, modelli**: `StratigraphyDocument` (5), `StratigraphyTransfer.Keys`, `ProjectSharedData` (2),
  `SoilProfileTransfer` (2), `Archivio.Moduli`, `DocumentService.ModuleIds`, `ModelDisplayCatalog.Palette`, `ModelViewerViewModel.FamilyKeys`,
  `MidasModelSource` (3), `ModelSnapshot.Components`;
- **serializzazione**: `JsonFallbackHook.Observer` (11.1).

Seguiti per le librerie GPC (non toccate):
- GPCChecker.Geotechnics, `BearingCapacityFactors.MediumRatios` e `LargeRatios`: `static readonly IReadOnlyList<double>` su un array,
  modificabile con un cast; ANTHEA ne tiene una copia immutabile (P3);
- GPCChecker.Geotechnics, `BustamanteDoix`: dizionari statici privati con array (`Soils`, `Charts`); `Chart` restituisce un `IReadOnlyList`.
  ANTHEA copia (P2);
- Model, `StandardNTC2018Geotechnics` e `SteelMaterial` mutabili (sopra).

I cataloghi di GPC.Design (`BridgeConcept`) sono già `Array.AsReadOnly`: nessun seguito.

### 11.4 Prova di uscita (proposta 4.6)

`tests/ANTHEA.Presentation.Tests/ParallelChecks.cs`, interruttore `--paralleli`, suite `Presentation.Tests/paralleli` nello stadio `regression`.

- **Fogli**: le 95 fixture F3 di Bridge Design (10), sezione composta da ponte (9), muri (16), palo e micropalo verticale (7 + 6), palo e
  micropalo orizzontale (11 + 10), palificata (8), fondazioni superficiali (11) e acciaio d'armatura (7), aperte come file di calcolo singoli
  in un `WorkspaceSession` con `RegistrySheetFactory`: ogni foglio è una `SheetSession` con il ViewModel esistente del modulo.
- **Limitatore**: `CalculationLimiter` di istanza dell'applicazione, `DefaultMaximum` = processori − 1 (23 su questa macchina), lo stesso della
  radice di composizione del desktop. Esisteva già (F5.1, `SheetContext.Limiter`): nessuna aggiunta in Presentation.
- **Serie**: un foglio alla volta, «Calcola» atteso prima di aprire il successivo; per i pali orizzontali che si aprono sulla risposta
  elastica anche il suo calcolo; poi i ricalcoli automatici ancora in attesa.
- **Parallelo** (due giri per esecuzione): ogni fixture in due documenti (stesso modulo in due `SheetSession`) accanto alle fixture degli
  altri moduli; tutti i calcoli di fondo avviati insieme (138 fogli); intanto il thread dell'interfaccia apre e calcola palificata e
  fondazioni (Q8 a, calcolo sul thread dell'interfaccia) e l'acciaio (52 fogli). Un cancello nel servizio di calcolo trattiene i primi
  calcoli di fondo finché non ne girano insieme tanti quanti il limitatore ne ammette: il giro è parallelo a ogni esecuzione, non per caso.
- **Confronto bit per bit** con la serie, per ogni foglio: stato, messaggio e avvisi del calcolo, risultato, dati dopo il calcolo, risposta
  elastica, valori e diagramma dell'acciaio. Numeri come double con tutti i bit (`"R"`), chiavi nel loro ordine. Esclusi solo le chiavi
  volatili di `tests/ANTHEA.Testing/tolerances.json` (`tempi_ms`, come nel confronto F3) e l'istante ISO 8601 che Bridge Design scrive
  all'apertura nei prezzi fissati (`PriceCatalogueEditing`, `DateTimeOffset.UtcNow`).
- **Controlli sul limitatore**: picco dei calcoli contemporanei = 1 nella serie, ≤ limite e ≥ 2 nei giri paralleli, cancello raggiunto.
- **Esito**: PASS 388 controlli, sulla base 10110f78 e a ogni passo; serie circa 23 s, giri paralleli circa 35 s, picco 23. Le impronte della
  serie sono in `paralleli/serie/`, il riepilogo con l'hash di ogni foglio in `paralleli/riepilogo.txt` della cartella di uscita.

Esecuzioni del 22/10 (cartelle in `supporto/artefatti/f5-13-d/` del worktree, non versionate), tutte PASS 388 con picco 23:
- `prova2`, sulla base 10110f78 con la sola prova (prima delle correzioni);
- `ponte/par`, dopo d7ff277f;
- nella corsa standard `20261022-124535-f5-13-d-codice` su e5005cc7 (82,5 s);
- `ripetizione-1`, `-2`, `-3`, su e5005cc7: giri paralleli fra 28 e 42 s.

Le impronte della serie (95 file) sono identiche byte per byte fra la base e il codice corretto, e fra le tre ripetizioni: le correzioni non
cambiano alcun risultato dei 95 fogli e la prova non dipende dall'esecuzione.

### 11.5 Stato, verifiche e ripresa

Fatto il 22/10, parte (d) chiusa:

| Commit | Contenuto | Voci P3 |
| --- | --- | ---: |
| 6892cb97 | prova di uscita `ParallelChecks`, `--paralleli`, suite `Presentation.Tests/paralleli` | 126 |
| a687a871 | viste dei risultati nel calcolo (`ModuleCalculation.View`): D1, D2, M1 | 123 |
| d7ff277f | cataloghi del ponte: D3–D7 | 89 |
| 33f61da0 | cataloghi dei pali: P1–P7 | 67 |
| 15111ba0 | cataloghi dei muri: M2, M3 | 55 |
| 2ea3a885 | cataloghi delle fondazioni: F1, F2 | 47 |
| 0c00660f | catalogo della Wiki: W1 | 43 |
| e5005cc7 | X.Desktop: D8, M4, P8, W2 | 37 |
| commit di chiusura | questa sezione, riga in piano.md | 37 |

Baseline P3 da 126 a 37 voci: 89 tolte, nessuna aggiunta. Ponte, muri, pali, fondazioni e Wiki a zero, salvo R1 e R2 (report congelati, D-1).
Nessun risultato numerico cambia: i tipi cambiano, i valori, l'ordine e i confronti no; lo provano la regressione, le tracce dei ViewModel
contro le viste storiche (stadio fast) e la prova di uscita.

Fuori dal codice di produzione sono cambiate solo righe che usavano gli array come `string[]`: `tests/ANTHEA.Legacy` (tre `Array.IndexOf` e una
copia nel motore storico di Bridge Design), `BridgePricesChecks`, `EditorContractChecks` e `supporto/test/Desktop/NeutralAxisSmokeChecks`.

### 11.5.1 Verifiche di giorno

- Profilo `quick` sulla base `20261022-115811-f5-13-d-base`: PASS 55, KNOWN 0, NEW-FAIL 0.
- A ogni commit: build della soluzione, build `UiTests` di X.Desktop (le prove WPF compilate, non eseguite), build dei progetti di prova
  fuori dalla soluzione, `Architecture.Tests` PASS 574 con la baseline P3 ridotta nello stesso commit, `Application.Tests` PASS 3116; per il
  ponte `--bridge-design` PASS 189 e `--sezione-mista` PASS 151; per le fondazioni `FoundationWorkspace.Checks` PASS 47; per la Wiki
  `Wiki.Tests` PASS 340.
- Profilo `standard` su e5005cc7, `20261022-124535-f5-13-d-codice`: PASS 78, KNOWN 0, NEW-FAIL 0 (build, fast, regression, wiki), con
  `Presentation.Tests` (tracce dei ViewModel contro le viste storiche e riferimento F3), `verifiche/regressione` e
  `Presentation.Tests/paralleli`.
- Profilo `standard` finale sul commit di chiusura, tag `f5-13-d-finale`: esito nel rapporto dell'agente e in
  `ANTHEA-f5-ripresa\f5-agenti\f5-13-d\avanzamento.md`.

### 11.5.2 Suite `ui` da eseguire stanotte

Il codice WPF toccato è poco (D8, M4, P8, W2 e una riga di `NeutralAxisSmokeChecks`), ma le viste dei risultati (D1, D2, M1) e i cataloghi sono
letti da tutte le viste di quei moduli. Senza la corsa full, queste:

| Suite | Perché |
| --- | --- |
| `ui/f3-equivalence` | tutti i moduli: report e risultati delle viste storiche con le viste nel calcolo e i cataloghi immutabili |
| `ui/f3-report-chapters`, `ui/f3-figures` | figure dei report di ponte e muri (`BridgeResultView.Read`, `WallModel.Read`, `BridgeDrawing`, `RetainingWallDrawing`) |
| `ui/check-sezione-mista-view`, `ui/check-sezione-mista-historic`, `ui/f3-scheduling-sezione-mista-mvvm`, `ui/smoke-bridge`, `ui/smoke-bridge-curves`, `ui/smoke-bridge-predalle` | `BridgeDrawing.Colors`, `BridgeResultView.Read`, cataloghi di `BridgeSection` |
| `ui/smoke-neutral-axis` | come sopra, più la riga cambiata in `NeutralAxisSmokeChecks` (`CalculationMethods.IndexOf`) |
| `ui/check-bridge-design-view`, `ui/check-bridge-design-historic`, `ui/f3-scheduling-bridge-design-mvvm`, `ui/smoke-bridge-design`, `ui/smoke-bridge-prices`, `ui/smoke-bridge-traffic` | `BridgeOptimizationPlot`, `BridgeDesignModel.Read`, cataloghi di `BridgeConcept`, intestazioni del prezzario |
| `ui/check-muri-view`, `ui/check-muri-historic`, `ui/f3-scheduling-muri-mvvm`, `ui/smoke-retaining-wall` e `-mvvm`, `ui/smoke-global-stability` e `-mvvm`, `ui/check-wall-advanced-offscreen`, `ui/check-wall-materials-offscreen`, `ui/check-global-guidance-offscreen` | `RetainingWallDrawing.LayerColors`, `WallInputGrid`, `WallModel.Read`, cataloghi dei muri, `Slope.Notes` |
| `ui/check-palo-orizzontale-view`, `ui/check-palo-orizzontale-historic`, `ui/check-palo-elastico-historic`, `ui/smoke-horizontal`, `ui/check-palo-verticale-view`, `ui/check-palo-verticale-historic`, `ui/f3-scheduling-palo-verticale-mvvm`, `ui/f3-figures-palo-verticale-mvvm`, `ui/check-pile-group-view`, `ui/f3-scheduling` | `HorizontalSoilEditor.Widths` e cataloghi dei pali |
| `ui/check-fondazioni-view`, `ui/check-fondazioni-historic` | cataloghi delle fondazioni |
| `ui/check-wiki-offscreen`, `ui/check-examples`, `ui/check-appearance`, `ui/check-appearance-mvvm`, `ui/check-shell-draft` | `WikiCatalog`, `WikiPalette.Dark` e `WikiView.SetDark` (cambio di aspetto della Wiki) |

### 11.5.3 Cosa resta e come riprendere

- D-1…D-4 (11.6), da rivedere in F5.15.
- Seguiti per le librerie (11.3).
- Parti (e)–(g) di F5.13 come da piano. La prova di uscita resta nello stadio `regression`: le parti successive la tengono verde.

```
cd C:\Users\g.pacini\AppData\Local\Temp\aw-f5-13-d
dotnet build ANTHEA.sln -c Release
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll supporto/artefatti/f5-13-d/paralleli --paralleli
dotnet tests/ANTHEA.Architecture.Tests/bin/Release/net8.0/ANTHEA.Architecture.Tests.dll supporto/artefatti/f5-13-d/arch
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Profile standard -Tag <tag>"
```

### 11.6 Domande «da decidere»

| # | Domanda | Opzione presa |
| --- | --- | --- |
| D-1 | R1 e R2: le liste di sezioni dei report di ponte e palificata sono array statici pubblici, ma nel codice dei report congelato | non toccate; si rendono immutabili con la revisione dei report, a fine refactoring |
| D-2 | La risposta elastica del palo orizzontale (`ElasticPileViewModel`, `ElasticPileAnalysisSession`) calcola su un thread di fondo senza passare dal limitatore (rischio R6) | lasciata come oggi, come la vista storica; da decidere con (e)–(g) o in F5.15 se farla passare dal limitatore |
| D-3 | Istanze condivise di tipi mutabili delle librerie, lette e mai scritte (11.3) | lasciate; seguito per le librerie |
| D-4 | Istante scritto all'apertura di Bridge Design nei prezzi fissati: non è un risultato, ma cambia a ogni esecuzione | escluso dal confronto della prova di uscita, come nella cattura F3 |

### 11.7 Parte (d) per la sezione c.a.

Agente `f5-13-d2`, 22/10/2026, ramo `refactoring/f5-13-d2` da `refactoring/f5` a 19153431 (blocco 9, S9 e vista XAML della sezione c.a.,
parti (a)–(d) della shell). Chiude le quattro voci P3 lasciate alla sezione c.a. in 11.3 e porta la sezione nella prova di uscita (11.4).
Regola diurna: nessuna prova WPF eseguita.

**Voci tolte.** Stessa ricetta di 11.2: cataloghi immutabili, stessi elementi nello stesso ordine, nessun risultato cambiato. Nessuna delle quattro
era scritta durante un calcolo. `ConcreteWorkspace` e `ConcreteSectionViewport` sono della vista storica, che resta quella della Release: cambia
solo il tipo. Il codice dei report non è toccato.

| # | File e membro | Chi lo scrive e quando | Correzione | Commit |
| --- | --- | --- | --- | --- |
| C1 | `src/ANTHEA.DesignTools.Presentation/ConcreteTimeEffectsViewModel.cs`, `Inputs` (`HashSet<string>` privato, strumenti c.a.) | inizializzatore; letto da `OnPropertyChanged` (`Contains`) e dal rinnovo della convalida dei campi (`foreach`, che notifica le proprietà nell'ordine di inserimento) | `ImmutableArray<string>` nell'ordine della dichiarazione: `Contains` ordinale come prima; le otto notifiche nello stesso ordine (nomi distinti, nessuna in più) | f64b77b9 |
| C2 | `X.Desktop/Wpf/ConcreteParameters.cs`, `ConcreteWorkspace.CommonCoefficients` | inizializzatore (copia di `ConcreteEditor.CommonCoefficients`); letto dai coefficienti della vista storica | `ImmutableArray` con le stesse coppie | e157454d |
| C3 | `X.Desktop/Wpf/ConcreteViewports.cs`, `ConcreteSectionViewport.Contours` | inizializzatore (copia in `string[]` del catalogo immutabile di Application, F2.11c); letto dalle scelte dei contorni della vista storica, dalle figure del report del c.a. (`ModuleFigureRenderer.Concrete`) e dalle prove WPF | proprietà che restituisce il catalogo `ConcreteSheetPreparation.Contours` (`ImmutableArray`); `Ui.Choice` copia già le voci in un vettore nuovo, quindi `ItemsSource` resta uno `string[]` come leggono le prove | e157454d |
| C4 | `X.Desktop/Wpf/ConcreteWorkspace.cs`, `ConcreteWorkspace.AllCalculations` | inizializzatore; letto dalle invalidazioni della vista storica (`HashSet`, `UnionWith`) | `ImmutableArray<string>`, stesso ordine | e157454d |

Fuori dal prodotto è cambiata una riga: `supporto/test/Desktop/ConcreteSmokeChecks.cs`, `Array.IndexOf(Contours, c)` → `Contours.IndexOf(c)`.

**Conteggi P3.** Baseline da 37 voci (base 19153431) a 36 (f64b77b9) e a 33 (e157454d), aggiornata nello stesso commit di ogni correzione;
nessuna aggiunta. Sezione c.a. a zero voci. Le 33 rimaste sono quelle di 11.3 fuori dalla sezione c.a.: report congelati (R1, R2 e
`PdfFonts` ×3), interfaccia globale (`Appearance` ×7, `ErrorLog.attached`, `StratigraphyDrawing.LayerColors`), stratigrafia, progetti,
documenti e modelli (18), `JsonFallbackHook.Observer`.

**Prova di uscita estesa** (`ParallelChecks`, b45770f1):
- **Fogli**: le 11 fixture F3 di `str_palo`, aperte per prime, con `ConcreteSheetViewModel` creato dal modulo registrato
  (`ConcreteSectionSheetModule`) nello stesso `WorkspaceSession` degli altri moduli: 106 casi. Nei giri paralleli ogni fixture ha due documenti
  (due `SheetSession` dello stesso modulo) accanto agli altri moduli, come in 11.4: 212 fogli per giro.
- **Calcolo**: la sezione non passa dal servizio del modulo né dal limitatore. Ha il suo coordinatore (decisione C1: una preparazione e i passi
  paralleli di `ConcreteAnalysisSession` su thread di lavoro, il taglio sul thread dell'interfaccia). Il cancello conta quindi solo i calcoli dei
  servizi. I lotti della sezione si contano a parte dallo stato `IsBusy` dei fogli: nella serie uno alla volta e nessuno in corso accanto ai
  calcoli dei moduli; nei giri paralleli almeno due insieme e almeno uno in corso all'ingresso di un calcolo dei moduli. Le sezioni partono per
  prime, quindi i loro lotti sono in corso quando partono i calcoli degli altri moduli.
- **Attesa**: dopo «Calcola» il foglio si lascia fermare: nessun lotto in corso o in coda, nessuna resistenza rapida in corso (thread di lavoro con
  il loro pianificatore, avviate dall'anteprima dell'apertura). L'attesa è svegliata dalle notifiche del foglio e degli ingressi, mai da una pausa.
- **Impronta**: quella comune (risultato e dati bit per bit), più la riga di stato, gli errori delle verifiche e le resistenze rapide.
  - Le fixture senza id nelle righe delle azioni ricevono l'id all'apertura (`Guid.NewGuid`): è un'identità, non un risultato, e cambia a ogni
    apertura. La prima esecuzione è caduta proprio su questo (`corpus-sezione-ca-taglio-din-rettangolare`, riga SLU).
  - Gli id generati si numerano per prima comparsa («<id generato n>»), come la cattura F3 numera i GUID (`GuidMap`). Quelli scritti nelle
    fixture restano come sono.
- **Esito**: PASS 437 controlli (erano 388: uno in più nella serie, due e 22 fogli in più per giro). In ogni esecuzione:
  - serie 27–30 s, giri paralleli 38–44 s;
  - picco del limitatore 23;
  - 22 lotti della sezione insieme e 22 in corso all'ingresso di un calcolo dei moduli.

Esecuzioni del 22/10 (cartelle in `supporto/artefatti/f5-13-d2/` del worktree, non versionate), tutte PASS 437:
- `prova2` (dopo `prova1`, caduta sugli id generati);
- `ripetizione-1`…`ripetizione-5`, 110–113 s l'una;
- la suite `Presentation.Tests/paralleli` della corsa standard finale `f5-13-d2-finale` (11.7.1).

Impronte della serie:
- le 106 impronte (`paralleli/serie/`) sono identiche byte per byte fra `prova2` e le cinque ripetizioni, e così gli hash del riepilogo;
- le 95 impronte già esistenti sono identiche byte per byte a quelle della base 19153431 (`base-paralleli`, PASS 388).

Le correzioni C1–C4 e la sezione aggiunta non cambiano alcun risultato, e la prova non dipende dall'esecuzione.

#### 11.7.1 Verifiche, suite di stanotte e ripresa

- **A ogni commit**:
  - build Release della soluzione; build `UiTests` di X.Desktop per e157454d (le prove WPF compilate, non eseguite);
  - Architecture.Tests PASS 574, con 36 e poi 33 stati statici;
  - `ConcreteTimeEffects.Checks` 379 PASS per f64b77b9.
- **Profilo `standard` finale**, tag `f5-13-d2-finale`: esito nel rapporto dell'agente e in `ANTHEA-f5-ripresa\f5-agenti\f5-13-d2\avanzamento.md`.

Suite `ui` da eseguire stanotte per i file toccati:

| Suite | Perché |
| --- | --- |
| `ConcreteTimeEffects.UiChecks` | finestra degli effetti nel tempo su `ConcreteTimeEffectsViewModel` (C1: notifiche della convalida) |
| `ui/check-sezione-historic`, `ui/f3-equivalence` | tracce e cattura F3 della vista storica della sezione c.a. (C2–C4) |
| `ui/smoke`, `ui/smoke-mvvm` | `ConcreteSmokeChecks.VerifyWorkspace` (riga cambiata con `Contours.IndexOf`); il banco `-mvvm` lascia storica la sezione c.a. |
| `ui/smoke-ca-extensions`, `ui/smoke-ca-features`, `ui/smoke-neutral-axis`, `ui/smoke-section-actions`, `ui/check-concrete-template-wizard`, `ui/check-concrete-design` | aprono la vista storica (`ConcreteWorkspace`) e le scelte dei contorni (C3) |
| `ui/f3-report-chapters`, `ui/f3-report-chapters-sezione-mvvm`, `ui/f3-concrete-figures` | figure del report del c.a. (`ModuleFigureRenderer.Concrete` legge `ConcreteSectionViewport.Contours`) |
| `ui/check-sezione-view` | la vista XAML legge `ConcreteSectionViewport.Contours` e disegna con `ConcreteSectionViewport` |
| `ui/f3-scheduling`, `ui/check-field-form-parity` | ricalcoli e moduli della vista storica della sezione c.a. (C2, C4) |
| `ui/check-palo-elastico-historic`, `ui/check-palo-orizzontale-view`, `ElasticPile.UiChecks` (con `anello-interno` e `nota-stress-block`) | il verificatore c.a. del palo apre la vista storica della sezione (`PileReinforcementEditor`, `PileConcreteVerifierView`) |

**Cosa resta.** Per la sezione c.a. nessuna voce P3. Restano D-1…D-4 (11.6) e D-5 (sotto), da rivedere in F5.15; parti (e)–(g) di F5.13.

```
cd C:\Users\g.pacini\AppData\Local\Temp\aw-f5-13-d2
dotnet build ANTHEA.sln -c Release
dotnet build tests/ANTHEA.Presentation.Tests/ANTHEA.Presentation.Tests.csproj -c Release
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll supporto/artefatti/f5-13-d2/paralleli --paralleli
dotnet tests/ANTHEA.Architecture.Tests/bin/Release/net8.0/ANTHEA.Architecture.Tests.dll supporto/artefatti/f5-13-d2/arch
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Profile standard -Tag <tag>"
```

#### 11.7.2 Domanda «da decidere»

| # | Domanda | Opzione presa |
| --- | --- | --- |
| D-5 | La sezione c.a. calcola i suoi lotti fuori dal limitatore dell'applicazione: `ConcreteAnalysisSession` usa `Task.Run` per la preparazione, i domini e le tensioni. Anche le resistenze rapide, la sezione σ/ε, la curva M–χ e la ricerca delle armature girano su thread di lavoro con i loro pianificatori, fuori dal limitatore. Con molte sezioni aperte i lotti si sommano ai calcoli dei moduli (rischio R6, come D-2). | lasciata come oggi: C1 conserva l'orchestrazione della vista storica. La prova conta i lotti a parte e prova che i risultati non cambiano. Da decidere con (e)–(g) o in F5.15 se far passare i lotti dal limitatore |

## 12. Parte (e): prove di MainWindow trasferite, cattura del progetto, report della sezione

Agente `f5-13-e`, notte del 22–23/10/2026, ramo `refactoring/f5-13-e` da `refactoring/f5-13-c3` 30d175f6. Fonti: sezione 1.2 (le prove della
shell storica), sezione 7.4, riga F5.13 della sezione 8 della [proposta](f5-proposta-architettura.md) («prove di `MainWindow` trasferite, cattura del
progetto»). La Release apre ancora `MainWindow`: le suite storiche restano tutte e girano come prima.

### 12.1 Suite trasferite e copertura

Banco: `tests/Desktop/ShellSmoke.cs`, argomento `--check-shell-smoke-<suite> <cartella> [--module-impl modulo=mvvm …]` della build UiTests.

- **Shell.** `ShellWindow` su `ShellViewModel` dalla radice di composizione `ShellComposition`, fuori dallo schermo e mai attivata.
- **Moduli.** Si aprono come li sceglie la Release: gli storici nel loro `SheetEditor`, R5. Con le coppie del banco usano la vista MVVM.
  - Le suite `-mvvm` usano lo stesso banco `$MvvmBench` delle suite storiche `-mvvm`.
- **Risposte.** Domande, dialoghi e file passano da `BenchSheetServices`, i servizi dei fogli del banco, come `TestServices` per `MainWindow`.
  - I dialoghi della shell si disegnano come li mostra `WpfDialogService`.
- **Comandi.** Dove la suite storica chiamava un metodo di `MainWindow`, la nuova usa il controllo o il comando della shell che l'utente preme:
  - nodo, menu e trascinamento dell'albero;
  - «← Torna al progetto», «+ Progetto»;
  - barra delle revisioni con i suoi dialoghi;
  - confronto;
  - annulla, ripristina e salva della barra;
  - «Riprendi».
- **Codice dell'applicazione.** Dove la suite storica chiamava direttamente `ProjectSharedData`, `ProjectRevisions` o `DocumentService`, la nuova fa la
  stessa chiamata, con lo stesso messaggio.

| Suite nuova (`build/ci.ps1`) | Suite storica | Copertura | Controlli (Release / `-mvvm`) |
| --- | --- | --- | ---: |
| `ui/shell-smoke-projects` e `-mvvm` | `ui/smoke-projects` (`ProjectSmokeChecks`, `ProjectDragLayoutSmokeChecks`) | selezione senza apertura, miniatura, «← Torna al progetto», drop nelle sottosezioni con gli input modificati, spostamenti senza duplicati, salvataggio e riapertura, validazione ricorsiva e archivi precedenti, «+ Progetto» e sottosezioni senza dialoghi, gerarchia dei nomi, rinomina in linea (doppio clic, Invio, Esc, clic fuori, altra finestra), clic singolo che apre il riepilogo, nomi dei fogli nuovi, riordino dei fogli, disposizione stabile durante il trascinamento con nomi lunghi, menu del nodo, miniatura del c.a., drop sulle righe, trascinamento di sezioni e progetti prima e dopo, drop non validi e annullamento, modifiche in sospeso confermate dal riordino, ordine nel file | 221 / 221 |
| `ui/shell-smoke-project-workspace` e `-mvvm` | `ui/smoke-project-workspace` (`ProjectWorkspaceSmokeChecks`, `ProjectRevisionNavigationSmokeChecks`, `ProjectRevisionDeletionSmokeChecks`) | progetto vuoto, ricerca delle schede da aggiungere, albero accanto al foglio con «▸», «Duplica» ed «Elimina» dal menu, annulla e ripristina, anteprima dello spostamento e annidamento, revisioni dalla barra (creazione, annullamento, numerazione, passaggi 0/1/attuale, eliminazione e promozione, unica versione protetta), deduplicazione e riferimenti danneggiati, «Uniforma a questo» dal riepilogo e dal foglio, revisione in sola lettura per tutti i moduli, riepilogo delle revisioni, report della sezione con la revisione, salvataggio e riapertura | 169 / 169 |
| `ui/shell-smoke-hierarchy` e `-mvvm` | `ui/smoke-hierarchy` (`ProjectHierarchySmokeChecks`) | moduli trascinati su progetto e sezioni, schede prima delle sottosezioni, eredità a più livelli, propagazione senza domande dal riferimento superiore (calcestruzzo, acciaio, geometria, armatura), conflitti gerarchici e «Uniforma a questo» nel confronto, avvisi, riordino e spostamenti, salvataggio e riapertura, riferimenti ambigui | 39 / 39 |
| `ui/shell-smoke-sharing` e `-mvvm` | `ui/smoke-sharing` (`ProjectSharingSmokeChecks`, `ProjectCoverWarningSmokeChecks`, `ProjectComparisonChecks`) | dati condivisi «tutti» o «solo questo», nessuna domanda ripetuta, conversioni mm/m, geometrie incompatibili, armatura, esposizione del menu, terreni e strati, copriferro minimo nella riga di stato delle schede (anche mentre si scrive), nel riepilogo, nel badge e nel confronto, ordine dei fogli indifferente, «Uniforma a questo» con la scheda aperta ricaricata, persistenza; i rami con `ANTHEA_SOIL_REGRESSION_FILE` e `ANTHEA_SOIL_MISSING_FILE` come la suite storica | 572 / 572 |
| `ui/shell-smoke-project-report` | `ui/smoke-project-report` (`ProjectReportSmokeChecks`) | report dei materiali da «Report Word…» di un file singolo, «Report Word della sezione» a ogni livello (disabilitato solo senza fogli), «Genera report» con il controllo prima del report, piano dei dati comuni, report ricorsivo di tutti i moduli con immagini e identificativi unici, progetto aperto invariato, scheda incompleta segnalata, «Annulla» che non sovrascrive il file; report Word uguale a quello di `MainWindow` (12.3) | 54 / — |
| `ui/shell-smoke-display` e `-mvvm` | `ui/smoke-display` (`DisplaySmokeChecks`) | Home, «Moduli singoli», palo e micropalo verticale calcolati alle quattro dimensioni della finestra (modulo nell'area visibile, nessuno scorrimento orizzontale esterno, ultimo pannello raggiungibile, pannelli estesi); a 1600 × 990 stratigrafia, esiti, presentazione, report e ritorno con «Riprendi · nome» e «Riprendi» | 43 / 47 |

`ui/shell-smoke-project-report` non ha la coppia `-mvvm`, come `ui/smoke-project-report`.

Prove di prova cambiate senza cambiare il comportamento delle suite storiche:
- `VerticalPileSmoke` e `ConcreteMaterialSmoke` prendono un `HostedSheet`: ViewModel ed elemento della vista, cercato a ogni uso.
- `MvvmSheetEditor` si converte in `HostedSheet`; nella shell la vista è quella della scheda.

### 12.2 Differenze trovate e corrette nella shell

Le suite hanno trovato otto differenze dalla finestra storica. Ognuna è corretta con un controllo senza WPF in `ShellPartEChecks`
(`--shell`).

| # | Differenza | Correzione | Commit |
| --- | --- | --- | --- |
| E1 | La principale attivata mentre mostra una pagina riselezionava la scheda sotto la pagina. Il foglio prendeva il fuoco, la pagina spariva e l'albero si ricostruiva, chiudendo la rinomina in linea | la pagina resta e nessun foglio prende il fuoco (`ShellWindowsViewModel.MainShowsPage`) | 8cdc6e22 |
| E2 | La rinomina in linea finiva solo con Invio, Esc o la perdita del fuoco della casella | come `BeginProjectRename`, la chiudono salvando anche un clic altrove nella finestra e la finestra che perde il fuoco (`ProjectTreeKeys`) | 8cdc6e22 |
| E3 | Il foglio con il fuoco senza nodo nella revisione mostrata, o dopo annulla o ripeti, si chiudeva. Il centro tornava alla Home o a una scheda senza fuoco | riepilogo della sezione delle revisioni, altrimenti di quella del foglio, come `MainWindow` mostra la struttura (`ShellViewModel.OnSheetClosed`) | 231425f4 |
| E4 | Tornando alla versione corrente, le schede chiuse dall'anteprima restavano chiuse. `MainWindow` riapre il foglio della navigazione | si riaprono; quella che aveva il fuoco lo riprende dopo un cambio della barra delle revisioni. Una pagina che chiude l'anteprima le riapre senza fuoco (`WorkspaceSession.ReopenClosedByPreview`) | 231425f4 |
| E5 | «Uniforma a questo» selezionava nell'albero il foglio uniformato | i cambi dei dati lasciano la selezione; i cambi della struttura selezionano il loro nodo (`ProjectTreeViewModel.OnProjectChanged`) | 231425f4 |
| E6 | Mancava l'albero vuoto di `MainWindow` (`BuildEmptyProjectTree`) | titolo, testo e «+  Crea progetto» fuori dalla sola lettura; riepilogo dell'albero nascosto. Soli token | 231425f4 |
| E7 | La riga di stato dei dati condivisi non seguiva la conferma del foglio quando i dati erano arrivati senza una modifica della vista | ricalcolata a ogni conferma del foglio con il fuoco, come `MainWindow.Commit` (`WorkspaceSession.ActiveCommitted`) | 7f6bde5e |
| E8 | La riga di stato restava sulla differenza dopo che «Uniforma a questo» dal foglio aveva allineato gli altri fogli | ricalcolata a ogni cambio del progetto | c96c4167 |

### 12.3 Report della sezione dalla shell

- **Comandi.** «Genera report» del riepilogo e «Report Word della sezione» del menu del nodo eseguono `ShellViewModel.SectionReportCommand`.
  - Il pulsante del riepilogo non è più un segnaposto.
  - Il comando segue la sequenza e i testi di `MainWindow.ExportSectionReport`, in `SectionReportRunner` (`Shell/Commands/SectionReport.cs`).
- **Sequenza.**
  1. Conferma del foglio con il fuoco. Senza fogli nella sezione non succede nulla.
  2. Con differenze o avvisi, il dialogo «Report · sezione» («Controllo prima del report») con «Apri confronto», «Genera con segnalazioni» e «Annulla».
  3. Il file «Relazione nome.docx», filtro «Word e PDF affiancati».
  4. La copia della sezione scritta sotto «Generazione del report». «Annulla» e la chiusura della finestra annullano al termine del calcolo in
     corso (`IGuardedDialog`, rispettata da `WpfDialogService`).
  5. Il messaggio «Report salvato in Word e PDF», un avviso quando ci sono schede senza risultati. Un errore va alla guardia.
- **Scrittura.** Passa da `ISectionReports`. Il desktop la implementa in `DesktopSectionReports` con la stessa chiamata di
  `MainWindow.GenerateSectionReport`: `ProjectReportService.WriteSnapshotAsync` con `ModuleReportService` e `ModuleFigureRenderer`.
  - `ProjectReport.cs` e il codice dei report non sono toccati.
- **Confronto con `MainWindow`.** `ui/shell-smoke-project-report` scrive il report del progetto di prova due volte: dalla shell, e con
  `GenerateSectionReport` di `MainWindow` da un partial di prova.
  - Il confronto è parte per parte (`confronto-report.txt`): 21 parti uguali.
  - Le immagini e le altre parti sono uguali byte per byte. `word/document.xml` è uguale a meno della data e dell'ora scritte in copertina
    (`DateTime.Now` nei generatori congelati).
  - Il PDF accanto è scritto dal DOCX con lo stesso codice e non è confrontato.

### 12.4 Cattura del progetto

`tests/Desktop/ShellProjectCapture.cs`, argomento `--check-shell-capture <cartella>`, suite `ui/shell-capture` e `ui/shell-capture-confronto`.

- **File.** Lo stesso file di progetto, copiato byte per byte, si apre in `MainWindow` (fuori dallo schermo) e nella shell nuova.
  - Il progetto ha una spalla con il calcestruzzo, la sezione c.a. dei pali e i pali.
- **Passi.**
  1. Apertura.
  2. Foglio del calcestruzzo.
  3. Classe cambiata e propagata.
  4. Diametro dei pali cambiato e propagato.
  5. Nuova revisione dalla barra.
  6. Differenza locale («solo questo foglio»).
  7. «Uniforma a questo» nel confronto.
  8. Rev. 0 in sola lettura.
  9. Versione corrente.
  10. Annulla.
  11. Ripristina.
  12. Salva.
- **Stato scritto dopo ogni passo**, in JSON canonico:
  - documento di lavoro e documento mostrato;
  - titolo della finestra, «non salvato», sola lettura, revisione mostrata;
  - foglio con il fuoco e i suoi dati;
  - riga di stato dei dati condivisi.

  Alla fine si scrive anche il file salvato.
- **Normalizzazione.** Uguale dai due lati:
  - i GUID dei nodi e dei modelli della sezione c.a., e gli SHA-256 che ne dipendono, numerati per prima comparsa. Ogni salvataggio riscrive il
    modello con identificativi nuovi;
  - le date delle revisioni oscurate.
- **Foglio escluso.** Da «annulla» in poi il foglio non entra nella cattura, dai due lati. `MainWindow` chiude il foglio a ogni annulla; la shell
  tiene la scheda, ricaricata (proposta 3.7, SH5 approvata).
- **Esito.**
  - `ui/shell-capture`: 14 file uguali byte per byte (`confronto.txt`).
  - `ui/shell-capture-confronto`: `tools/qa/f5_confronto_diretto.py` sulle cartelle `mainwindow/` e `shell/`, «identiche», 0 differenze.
  - La cattura ha trovato E8 (12.2).

### 12.5 Differenze dichiarate (scelte già approvate), nelle suite

Le suite le controllano come comportamento della shell, con il riferimento alla scelta:
- **SH24.** L'albero è una colonna della finestra: aperto all'apertura di un foglio (`MainWindow` lo chiudeva). Lo stato di «▸» vale per la
  finestra (vedi SH48).
- **SH40.** Sotto 1120 × 680 la shell scorre. Il menu occupa un po' d'altezza, quindi a 1280 × 720 scorre di poco in verticale.
  - `ui/shell-smoke-display` controlla il modulo nell'area che scorre, non nella finestra.
- **Menu del nodo** (bozza approvata, 9.1): Apri, Rinomina, Duplica, Sottosezione, Report Word della sezione, Elimina. Per i fogli anche le voci
  della 9.5.
  - `MainWindow` aveva Rinomina, Duplica, Elimina e un pulsante del report per ogni sezione dell'albero (vedi SH50).
- **Miniature.** La miniatura di un foglio è un pulsante di 36 × 36 con l'icona del modulo in un `Viewbox`.
- **Riepilogo.** Si ricostruisce a ogni cambio del progetto: dopo «Uniforma a questo» è un oggetto nuovo sulla stessa sezione.
- **SH5, proposta 3.7.** Annulla e ripeti tengono le schede aperte.
- **Più schede.** La shell tiene più schede aperte, `MainWindow` un solo editor.
  - Dove la suite storica eliminava l'editor e scriveva il documento a mano, la nuova chiude prima le schede confermandole, oppure passa da
    `ProjectSession.ChangeData`, che le ricarica.
- **Clic sull'albero.** Gli eventi del mouse alzati a mano non arrivano ai binding d'input. Per questo:
  - il doppio clic esegue il comando del gesto del nome;
  - la pressione del `TreeViewItem` non si alza, perché prenderebbe il fuoco della tastiera e attiverebbe la finestra fuori schermo.

### 12.6 Domande «da decidere»

| # | Domanda | Opzione presa |
| --- | --- | --- |
| SH48 | Albero sul riepilogo dopo che l'utente l'ha chiuso su un foglio | resta chiuso: lo stato è della finestra (SH24) e «▸» lo riapre. `MainWindow` lo mostrava sempre sulla pagina del progetto; riaprirlo da solo sul riepilogo è possibile se l'utente lo chiede |
| SH49 | Stato delle viste XAML tenuto solo dalla vista (la scheda dei risultati del palo verticale), quando la scheda torna a schermo | la vista della scheda si ricostruisce (proposta R6); resta lo stato del ViewModel (dati, risultati, pannello esteso, vista della capacità), la scheda dei risultati torna alla prima. `MainWindow` teneva l'editor. Proposta: portare nel ViewModel del modulo lo stato che l'utente vuole conservare, modulo per modulo |
| SH50 | Report di una sezione dall'albero | voce «Report Word della sezione» del menu del nodo e «Genera report» del riepilogo, come la bozza approvata; nessun pulsante per nodo come in `MainWindow` |
| SH51 | Chiusura della finestra «Generazione del report» mentre lavora | annulla al termine del calcolo in corso e la finestra resta, come `MainWindow`, con `IGuardedDialog` (interfaccia facoltativa in Presentation, gestita da `WpfDialogService`; gli altri dialoghi non cambiano) |
| SH52 | Schede chiuse dall'anteprima di una revisione | si riaprono tutte sulla versione corrente, nel loro ordine. Quella che aveva il fuoco lo riprende dopo un cambio della barra, non dopo una pagina della navigazione. `MainWindow` riapriva il suo foglio; con più schede vale per ognuna |
| SH53 | Foglio con il fuoco chiuso da una revisione o da annulla | riepilogo della sezione delle revisioni, altrimenti di quella del foglio, come `MainWindow` |
| SH54 | Finestra principale attivata mentre mostra una pagina | la pagina resta e nessun foglio ha il fuoco; quello di una finestra staccata è confermato |

### 12.7 Guida pratica e PDF: cosa servirà alla parte (g)

La guida non è aggiornata adesso: la Release apre ancora `MainWindow`. Quando la shell nuova diventerà la Release (parte g), servirà questo in
`supporto/docs/guida-pratica-anthea.md`, con il PDF rigenerato e controllato.

- **«Avvio e scelta del modulo».**
  - Home, «Riprendi · nome».
  - Barra di navigazione sempre visibile: Home, Progetti, Modelli, Moduli singoli, Esempi, Wiki, con Apri, Salva, Salva con nome.
  - Schede dei fogli: più file singoli aperti insieme; «Nuovo …» apre una scheda; un file già aperto si attiva; «•» per i fogli non salvati.
  - «Sposta in una nuova finestra», «Riaggancia», la pagina intera ed Esc.
  - La Wiki in una scheda; F1 e «?» aprono la scheda.
  - Aspetto (invariato).
- **«Leggere e compilare i campi».**
  - Finestre piccole: la shell tiene 1120 × 680 e scorre; i fogli che non seguono la finestra tengono 1120 × 600.
  - Al posto dei divisori della composizione dei progetti, la colonna dell'albero e «▸».
- **«Progetti e gestione del lavoro».**
  - La disposizione: navigazione, colonna dell'albero, riepilogo al centro con «Schede da aggiungere».
  - Menu del nodo: Apri, Apri in una nuova finestra, Apri a pagina intera, Rinomina, Duplica, Sottosezione, Report Word della sezione, Elimina.
  - Badge di coerenza che apre il confronto; trascinamento con l'indicatore e il suggerimento.
  - Rinomina in linea, che finisce anche con un clic fuori.
  - Annulla e Ripristina nella barra del progetto e del foglio: le schede restano aperte e si chiudono con un avviso se il foglio non esiste più.
  - Revisioni con più schede: in sola lettura, chiuse e riaperte (SH6, SH52).
  - L'avviso «Aggiornato da «foglio»: …» delle schede ricaricate (SH42).
  - «Genera report» e «Report Word della sezione» con il controllo prima del report e la finestra di avanzamento.
- **Immagini.** Nella guida non ci sono immagini della finestra storica: quelle presenti sono dei moduli e non cambiano. Se servono immagini
  della shell, le prendono le prove della bozza (`ui/check-shell-draft`, 21 PNG).
- **Wiki.** Gli articoli che descrivono la finestra (Home, progetti, revisioni) vanno riletti con la parte (g): non sono cambiati qui.

### 12.8 Verifiche, suite da eseguire e cosa resta

**A ogni commit:**
- build di Presentation.Tests, `--shell`, da PASS 532 a PASS 548;
- build UiTests e Release di X.Desktop;
- Architecture.Tests PASS 574, con le baseline invariate.

**Corse delle suite nuove**, con `dotnet ANTHEA.dll` sulla build UiTests, cartelle `supporto/artefatti/f5-13-e/prove/` del worktree:
- tutte PASS, con i numeri della 12.1;
- `ui/check-shell-draft` PASS 82 dopo le correzioni.

**Corse finali:** esiti nel rapporto dell'agente e in `ANTHEA-f5-ripresa\f5-agenti\f5-13-e\avanzamento.md`.
- profilo `standard`;
- `-Stage build,ui` con le suite nuove, `ui/check-shell-draft`, `ui/check-module-host-offscreen` e `ui/check-wiki-offscreen`;
- le suite storiche `-mvvm` che usano gli helper cambiati.

**File storici e condivisi toccati:**
- `X.Desktop/Services/WpfPresentationServices.cs`: `IGuardedDialog`, additivo;
- `X.Desktop/Services/ShellComposition.cs`;
- `X.Desktop/Hosting/ShellSheets.cs`: `DesktopSectionReports`;
- `supporto/test/Desktop/AppHarness.cs`: due rami;
- `build/ci.ps1`: 13 suite;
- gli helper di prova della 12.1.

Nessun file di `MainWindow`, `SheetEditor` o dei report, nessuna baseline.

**Suite da eseguire con il profilo full:**
- le 13 nuove;
- `ui/check-shell-draft`;
- le suite storiche `-mvvm` che usano `VerticalPileSmoke` e `ConcreteMaterialSmoke`: `ui/smoke-mvvm`, `ui/smoke-display-mvvm`, `ui/smoke-projects-mvvm`, `ui/smoke-project-workspace-mvvm`,
  `ui/smoke-hierarchy-mvvm`, `ui/smoke-sharing-mvvm`, `ui/smoke-materials-mvvm`;
- le viste MVVM con dialoghi, per `WpfDialogService`.

**Cosa resta prima di (f) e (g):**
- **Prove con finestre reali, fuoco e mouse.** Le suite di stanotte sono fuori dallo schermo e alzano gli eventi a mano.
  - Restano da provare `DoDragDrop`, il doppio clic vero, il fuoco fra finestre staccate e la principale, e lo stesso flusso con la finestra
    attiva.
- **Le altre suite che aprono `MainWindow`:**
  - `ui/smoke`, `ui/smoke-materials`, `ui/smoke-steel`, `ui/smoke-retaining-wall`, `ui/smoke-global-stability`, `ui/smoke-bridge*`, `ui/smoke-horizontal`,
    `ui/check-appearance`, `ui/check-wiki-offscreen` ed `ExampleLibraryChecks` usano la finestra solo come contenitore di un foglio, del tema,
    della Wiki o degli esempi;
  - `ui/f3-equivalence`, `ui/f3-report-chapters` e `ui/check-error-log-offscreen` usano metodi privati di `MainWindow`.

  Vanno portate sulla shell con il ritiro di `MainWindow` in (g). Le sei suite della sezione 1.2 sul lavoro della finestra (progetti, revisioni,
  condivisione, report, display) sono quelle di questa parte.
- **Domande SH48–SH54**, da rivedere con l'utente in F5.15.
- **Parte (f).** La sessione di prova con l'utente: la shell avviabile nella build UiTests con `--interactive-shell`.
- **Parte (g).** Il passaggio di `App.OnStartup` alla shell e il ritiro di `MainWindow` e `SheetEditor`.
  - Prima servono l'ospite storico per i moduli ancora storici, o il loro passaggio, e la guida della 12.7.

Per riprendere:

```
cd C:\Users\g.pacini\AppData\Local\Temp\aw-f5-13-e
dotnet build X.Desktop/X.Desktop.csproj -c UiTests
dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-shell-smoke-<suite> <cartella> [--module-impl modulo=mvvm …]
dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-shell-capture <cartella>
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,ui -Only '^ui/(shell-smoke|shell-capture|check-shell-draft)' -Tag <tag>"
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Profile standard -Tag <tag>"
```

## 13. Passo R5 della bozza dopo i passaggi della Release (23/10)

Agente f5-pass-strutture, ramo `refactoring/f5-pass-strutture`. Con i passaggi della Release del 23/10 i moduli storici della Release
diminuiscono fino a zero. Il passo `HistoricHost` di `ui/check-shell-draft` (immagine 21, ospite R5 dei moduli storici nella shell) apriva la
sezione c.a. e i pali verticali, storici fino a quel giorno. Ora non dipende da un modulo:
- apre il primo modulo ancora `Historic` in `ModuleImplementations.Declared`, e il secondo se c'è, con la palificata MVVM, e ripete gli stessi
  controlli (scheda, conferma, F1, finestra staccata, revisione in sola lettura);
- il report short della sezione c.a. storica si prova solo se il primo modulo storico è ancora `str_palo`;
- se nessun modulo della Release è storico, il passo non è applicabile: scrive il motivo in `21-ospite-storico-non-applicabile.txt` e non cade.

È l'unico allentamento ammesso dal prompt dei passaggi. L'infrastruttura R5 (`HistoricSheetViewModel`, `HistoricSheetHost`, `ReleaseSheetFactory`)
si ritira con la parte (g), insieme a `SheetEditor` e `MainWindow`.

Esito sul ramo, con la sezione composta e Bridge Design passati (`ci/20261023-131014-fps-shell`): PASS 80 controlli; il passo ha usato
`geo_fondazioni_superficiali` e `geo_palo_verticale`.

## 14. Parte (g): la shell nuova nella Release e il ritiro di MainWindow

Agente `f5-13-g`, 23/10/2026 sera, ramo `refactoring/f5-13-g` da `refactoring/f5` 67eafba5. Fonti: istruzioni del coordinatore
(`ANTHEA-f5-ripresa\istruzioni-agenti\prompt-f5-13-g.md`), sezioni 1, 10.2, 12 e 13 di questa nota, [f5-ripresa.md](f5-ripresa.md) («Come si passa
un modulo nella Release»), [f5-suite-ui.md](f5-suite-ui.md) sezione 4, il commit 7d53d9dc di `prova/f5-viste-3` (avvio `--shell-nuova` della
prova f5f, solo letto).

Decisione dell'utente del 23/10, «Moduli ora, shell dopo»: **il ramo non si unisce** finché l'utente non ha provato la shell nuova (parte f, prova
f5f). Lo unisce il coordinatore. Le istruzioni chiamavano questa sezione «13»: la 13 c'era già (passo R5 della bozza), quindi è la 14.

### 14.1 Inventario di ciò che si ritira (aggiorna 1.1 a 67eafba5)

Tutti i 15 moduli aprono la vista MVVM nella Release (`ModuleImplementations.Declared`, nessun `Historic`). La Release apre ancora `MainWindow`
(`App.OnStartup`). `MainWindow` non ha XAML: è una partial in 15 file, 2 134 righe, come in 1.1.

| Codice (righe) | Uso a 67eafba5 | Destinazione |
| --- | --- | --- |
| `X.Desktop/Wpf/MainWindow*.cs` (5 file, 666), `Project*.cs` (10 file, 1 468) | finestra storica, aperta da `App.OnStartup` e da molte prove | `supporto/SUPERATI/ui-storica/shell/` |
| `X.Desktop/Wpf/SheetEditor.cs` (80) | guscio senza fogli: il costruttore rifiuta ogni modulo. `GeoEditor` è già in `ui-storica/palo-verticale` | archivio |
| `X.Desktop/Hosting/HistoricSheetViewModel.cs` (132), `Views/Shell/HistoricSheetHost.cs` (35), ramo storico di `ReleaseSheetFactory` e di `DesktopSheetExports` (`ShellSheets.cs`) | ospite R5 (10.2): nessun modulo storico, mai usato | archivio |
| `SheetImplementation.Historic`, `ModuleHostBench` con `=historic` | scelta storica: nessun modulo la usa | da togliere o motivare (scelta provvisoria, F5.15 §15.3) |
| passo `HistoricHost` di `ui/check-shell-draft` (sezione 13) | già «non applicabile» senza moduli storici | archivio |
| `X.Desktop/Wpf/WpfDesktopServices.cs` (78), `Services/DesktopServices.cs` (7), `DesktopComposition.WindowServices` | servizi della finestra storica (F1.4), solo `MainWindow` | archivio |
| `tests/Desktop/HistoricSheetEditor.cs` (13), `supporto/test/Desktop/TestServices.cs` (61), `MainWindow.Tests.cs` (15) | prove della finestra storica | archivio |
| rami `MainWindow` di `WikiContextHelp.cs`, `CalculationHelpView.cs`, `WpfWikiNavigator` (`WpfPresentationServices.cs`) | aiuto contestuale nella finestra storica | si toglie il ramo; resta quello della shell |
| `WikiEditorial.cs` usa `MainWindow.WikiExamples.Definition` | articoli con esempio | `Anthea.Presentation.Shell.Navigation.WikiExamples` (stesso codice, 10.1) |

Restano: `ISheetEditor`, `MvvmSheetEditor` (usato in produzione dalla verifica c.a. del palo orizzontale, `ConcreteVerifierSection`, e dalle prove
F3), `SheetEditorFactory` (prove F3 e banco), `RevisionInspection` (viste MVVM), `DisplayAdaptation` e `WikiView` (shell), `ShellComposition`.

### 14.2 Prove che aprono MainWindow o ne confrontano i valori

| Suite | Uso di `MainWindow` | Passo |
| --- | --- | --- |
| `ui/shell-capture` | cattura dello stesso progetto in `MainWindow` (`mainwindow/`, 14 file) | 2: valori in `tests/fixtures/f5-shell/` |
| `ui/shell-smoke-project-report` | report Word della sezione di `MainWindow.GenerateSectionReport` | 2: chiamata registrata e valori nella fixture |
| `ui/check-shell-draft` | dati di `MainWindow.WikiExamples` | 2: valori nella fixture |
| `ui/f3-equivalence` | `MainWindow.CaptureF3Project` (report di progetto con `SectionReportSnapshot` e `GenerateSectionReport`) | 3: la stessa chiamata di `DesktopSectionReports` |
| `ui/smoke-projects`, `-project-workspace`, `-hierarchy`, `-sharing`, `-project-report`, `-display` | lavoro della finestra | sostituite da `ui/shell-smoke-*` (12.1): si archiviano con `MainWindow` |
| `ui/smoke-material-report` | `SmokeProjectReport(materialsOnly)` | 3: sulla shell (coperta in parte da `ui/shell-smoke-project-report`) |
| `ui/smoke`, `ui/smoke-materials`, `ui/smoke-steel`, `ui/smoke-retaining-wall`, `ui/smoke-global-stability`, `ui/smoke-horizontal` | contenitore di un foglio, Home, «Moduli singoli», progetti | 3: sulla shell |
| `ui/smoke-bridge`, `-bridge-predalle`, `-bridge-curves`, `-bridge-design`, `-bridge-traffic`, `-bridge-prices` | contenitore (13 file di prova `Bridge*`) | 3: sulla shell |
| `ui/smoke-section-actions`, `-ca-features`, `-ca-extensions`, `-neutral-axis` | contenitore della sezione c.a. | 3: sulla shell |
| `ui/check-appearance`, `ui/check-wiki-offscreen`, `ui/check-examples`, `ui/check-module-host-offscreen` | tema, Wiki, esempi, ospite nella finestra | 3: sulla shell |
| `ui/check-contrast` (`ContrastAudit.cs`) | finestre e pagine di `MainWindow` | 3 e 5: sulla shell; non si esegue a 144 DPI |
| `ui/f3-figures`, `ui/f3-scheduling` | metodi statici in partial di `SheetEditor` (`F3FigureChecks.cs`, `F3SchedulingChecks.cs`), nessun foglio storico | 4: classi proprie |
| `ui/f3-report-chapters`, `ui/check-error-log-offscreen` | nessuno a 67eafba5 (solo `ISheetEditor` e `ErrorLog`) | nulla da fare |

### 14.3 Piano dei sottopassi

Commit piccoli; ognuno compila e tiene verde il profilo quick.
1. **Riferimenti prima del ritiro** (passo 2 delle istruzioni). Valori di `MainWindow` registrati in `tests/fixtures/f5-shell/`; `ui/shell-capture`,
   `ui/shell-smoke-project-report` e `ui/check-shell-draft` confrontano con quelli.
2. **`ui/f3-equivalence` senza `MainWindow`**: il report del progetto con la chiamata di `DesktopSectionReports`, identica a quella di
   `MainWindow.GenerateSectionReport`. Stessi esiti, 0 differenze, catture di riferimento intatte.
3. **Avvio.** `App.OnStartup` apre la shell (`ShellComposition`) in Release; restano argomenti, file aperti da Esplora risorse (`StartupRequest`),
   registro degli errori, tema, conferma dei fogli non salvati alla chiusura. Singola istanza: non c'è oggi, non si aggiunge. `qa/no-test-code` verde.
4. **Suite contenitore sulla shell**, una o poche per commit, con il banco della parte (e) (`ShellSmokeBench`).
5. **Ritiro** in `supporto/SUPERATI/ui-storica/shell/` con `README.md` e `registro.json`; baseline di architettura solo in discesa.
6. **Contrasto** (D12) e **testi proposti** per la guida pratica e la Wiki (14.6).
