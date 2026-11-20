# Sezione composta verifica rapida

Richiesta del 19 novembre 2026, implementazione su `main`, senza commit né push. Approvazione dopo l’anteprima `supporto/artefatti/sezione-composta-rapida/anteprima/layout-v2.png`: tre colonne fisse dei momenti, taglio incluso, controllo tensionale senza classe/MRd e senza SLE frequente, non esposti dal motore attuale.

## Documento e calcolo

Identificativo `str_sezione_composta_rapida`. Geometria, materiali, coefficienti e fasi provengono da `str_mista_ponte`. La chiave aggiuntiva `verifica_rapida_mista` conserva versione, combinazioni con identificatore, stato limite, fasi native e selezione. La combinazione selezionata è sincronizzata con `fasi` e `stato` alla radice. Aprendo il completo si conserva l’intero documento; tornando al rapido, eventuali modifiche delle fasi alla radice aggiornano la combinazione selezionata.

La preparazione e le proiezioni stanno in Application; il ViewModel in Presentation; XAML e adattamento del disegno esistente in Desktop. Ogni combinazione chiama il servizio del modulo completo: nessun motore, formula o coefficiente nuovo. Il riepilogo seleziona i massimi rapporti nativi in tutte le situazioni. Per gli SLE il motore non restituisce il rapporto della resistenza a taglio: il rapido mostra domanda/resistenza e dichiara l’assenza del rapporto, conservando gli altri controlli disponibili. La figura rappresenta l’ultima situazione.

N e V della tabella appartengono alla fase breve. Il ritiro rimane una fase autonoma conservata. Configurazioni non rappresentabili nel rapido sono segnalate e restano apribili nel completo. Nessuna ripartizione automatica di N, V o M, nessun coefficiente ulteriore sui carichi.

Excel conserva l’ordine Nome, SL, M getto, M lungo, M breve, V, N; menu con tre SL, lettura per intestazione, SL facoltativo nei file precedenti. La lettura valida l’intera tabella prima della sostituzione. I parametri delle fasi sono conservati per le righe corrispondenti durante l’importazione.

## File

- Application: `BridgeQuickSection.cs`, `BridgeQuickActionsExcel.cs`, `BridgeQuickReportAnalysis.cs`; registrazione in ModuleDirectory, ModuleServices e ModuleReportService.
- Presentation: `Modules/QuickCompositeSection/QuickCompositeSectionSheetViewModel.cs`; adattamento di SheetLayout, WorkspaceSession, TabsViewModel, ShellCommandsViewModel e SheetFileExports.
- Desktop: `Views/QuickCompositeSection`, `QuickCompositeFigureRenderer.cs`; registrazione, DataTemplate, icona e figura del report.
- Reports: `BridgeQuickReport.cs`, `Generators/ItecPileReport.QuickBridge.cs`.
- Test: `QuickCompositeSectionChecks.cs`, `QuickCompositeReportWpfChecks.cs`, integrazione delle suite shell e ospite MVVM. Comando mirato `--sezione-composta-rapida`, suite `QuickCompositeSection.Tests` nel runner.
- Documentazione: due guide globali Rev76, edizioni Word/PDF, indice delle guide e indice Wiki; Rev75 conservata con manifest in `supporto/SUPERATI/sezione-composta-rapida-20261119`.

## Evidenze

Cartella di attività: `supporto/artefatti/sezione-composta-rapida`. Prima: `anteprima/default-documento.json`, `anteprima/default-risultato.json` e `prima-accesso` (8 verifiche passate; codice nuovo aggiunto durante la corsa, rilevato dal controllo dell’albero).

La suite dedicata verifica l’uguaglianza integrale dei risultati nativi, default, tre SL, modifiche, combinazioni, dati non validi, passaggio al completo, sola lettura, salvataggio/riapertura, Excel e contenuti del report. Atteso indipendente fisso: trave senza barre, n=10 e nessuna riduzione efficace; area acciaio 58700 mm², area omogeneizzata 133700 mm², baricentro −52731250/133700 mm. Gli attesi non sono ricavati dal codice né sovrascritti.

Le prove WPF salvano le schermate SLU, SLE RA, SLE QP e geometria; verificano selezione, zoom/centratura, nuova scheda e destinazione dei comandi W/W breve. La prova dei report salva Word, PDF e PNG delle figure completa/breve. Suite UI prima e dopo: 74; nessuna suite rimossa.

Le corse intermedie documentano anche le correzioni di compilazione e dei nuovi test. Il controllo di sola lettura usa `FieldViewModel.EditText`, come l’utente; `FormViewModel.Set` è intenzionalmente una scrittura interna consentita anche in sola lettura. Le liste esplicite dei moduli MVVM e il conteggio delle finestre della prova shell includono il nuovo modulo.

Le guide sono aggiornate preservando il pacchetto delle precedenti edizioni. L’audit confronta i frammenti di testo e le formule modificabili: pratica 1430 frammenti/9 formule, teorica 2571 frammenti/1592 formule; 62 e 48 parti del pacchetto identiche. Word aggiorna gli indici e produce i PDF finali. Nessun aggiornamento delle DLL GPC.

## Verifica conclusiva

Tutte le suite selezionate sono passate, senza fallimenti aperti né aggiunte a `known-failures.json`:

| Suite | Esito | Evidenza nella cartella di attività |
|---|---|---|
| Build Release e UiTests | PASS | `finale`, `finale-schermate` |
| qa/no-test-code | PASS | `finale` |
| Architecture.Tests | PASS 642 | `finale-presentation` |
| Presentation.Tests, inclusi shell, c.a. rapido e composta completa | PASS 5685 | `finale-presentation` |
| QuickCompositeSection.Tests | PASS 50 | `finale-presentation` |
| ui/check-shell-draft | PASS 100 | `finale-schermate` |
| ui/check-module-host-offscreen, inclusi i nuovi Word/PDF | PASS 82 | `finale` |
| ui/check-sezione-mista-view | PASS 124 | `finale` |
| ui/check-theme-tokens | PASS 139 | `finale` |

La prima corsa conclusiva aveva due attesi di catalogo rimasti al conteggio precedente: un solo rapido con finestra propria e il filtro dei 13 moduli storici. Sono stati estesi esplicitamente al nuovo modulo, conservando il controllo dei precedenti, e l’intera suite Presentation è stata ripetuta con esito positivo. La prova WPF cambia lo SL dal ComboBox e verifica la concordanza fra selezione, titolo e risultato; prima della cattura QP attende anche il fotogramma aggiornato. Non sono emerse differenze numeriche fra rapido e completo.

Schermate definitive in `finale-schermate/ui_check-shell-draft`: `08h-composta-rapida-SLU.png`, `08i-composta-rapida-SLE.png`, `08j-composta-rapida-QP.png`, `08k-composta-rapida-geometria.png`. Conteggio suite UI: 74 prima e dopo (`suite-ui-dopo.txt`). Elenchi puntuali dei file in `file-nuovi.txt` e `file-modificati.txt`.

Report di esempio in `finale/ui_check-module-host-offscreen/relazione-composta-rapida`: Word e PDF, breve di 2 pagine; completa di 13 pagine nel PDF nativo e 14 nell’impaginazione Word. Il controllo grafico ha incluso tutte le pagine Word e le figure aggiornate del PDF nativo. `render_docx.py` non ha potuto usare LibreOffice, assente nell’ambiente: la verifica è stata eseguita con l’esportazione nativa di Microsoft Word e il rendering Poppler, conservati in `qa-finale`. Controllati anche copertine, capitoli aggiunti e indice delle guide. Le due guide finali hanno 146 e 215 pagine; la revisione 75 rimane nell’archivio, compresa la relativa riga storica delle copertine.

La Release è stata pubblicata in `app` e avviata con `--modulo str_sezione_composta_rapida`. Osservata sul desktop la finestra «Sezione 1 — ANTHEA» con geometria, armature, tabella delle azioni, controlli e diagramma delle tensioni. Impronte degli eseguibili locali in `eseguibile-locale.json`. Non è stato aggiornato l’installer o l’eventuale installazione separata in AppData.

Non eseguiti: profilo full integrale, baseline di rilascio, installer e modifica manuale del workbook nell’applicazione Excel. Il formato XLSX, i menu SL, la lettura/scrittura, la reimportazione e la gestione del file bloccato sono coperti dalle prove automatiche. Nessuna modifica alle librerie GPC, nessun commit né push.

## Aggiornamento successivo: set BB e guide Rev77

Gli esiti sopra descrivono la prima verifica, precedente all'aggiornamento delle librerie richiesto dall'utente. Sul commit di Gabriele 7ee431c9 sono stati ripetuti i 50 controlli della sezione composta e le prove WPF/report (shell 103, ospite 87), con esito positivo. Aggiunti due esempi al catalogo, con Word e PDF: tre stati limite e momenti negativi. L'esempio negativo usa il report completo; la protezione delle brevi oltre due pagine resta invariata.

Corretto il tema delle ComboBox e del menu Approfondimenti della finestra. Il contrasto mirato fondazioni/ponte passa su 58 catture a 96 DPI e tre temi. La documentazione corrente è Rev77, con le transizioni Wiki approvate e l'equivalenza F3 passata. Il contrasto globale conferma zero nuovi difetti; restano solo i 12 già registrati del c.a. rapido. Dopo l’ultima correzione DXF la Release finale è in `supporto/artefatti/import-dxf-20261119/app-finale`, apribile direttamente sul ponte con `Apri-sezione-composta.cmd`. L’istanza precedente in `app` rimane aperta sul desktop Windows bloccato; nessun installer, commit o push. Il dettaglio della full, delle ripetizioni, del controllo grafico di tutte le relazioni e dei confronti F4 ancora bloccati dalle catture mancanti è in [importazione-dxf-sezione-ca.md](importazione-dxf-sezione-ca.md).

Il 20/11/2026 l'utente autorizza commit e push su main: codice in 1b9c7c4f, prove in d4a194a9, guide ed esempi in 60d351a7. Nessun nuovo commit remoto da integrare dopo il fetch; rimangono le limitazioni di verifica riportate nella nota DXF.
