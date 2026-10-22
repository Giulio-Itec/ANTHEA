# F5.13 shell: parti (a) e (b), sessioni e ViewModel; parte (c) fino al punto di controllo della bozza; parte (d), stato statico e calcoli in parallelo

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
- **Parti (d)–(g)** come da piano. Il report di sezione (`ProjectReport.cs`) passa con (e), insieme alla cattura del progetto.
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
scelta.

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
