# F5.6b — Stratigrafia: preparazione

## Scopo

Questa nota chiude la **preparazione** di F5.6b: modulo `geo_stratigrafia` (modulo del 15/10, Q12). Oggi il foglio passa dal
ramo della stratigrafia di `SheetEditor` (righe 95-99) e da:
- `X.Desktop/Wpf/StratigraphyWorkspace.cs`: campi generali, elenco degli strati con le azioni, campi dello strato selezionato,
  medi e minimi, stato e profilo disegnato (`StratigraphyProfileView`);
- `X.Desktop/Wpf/StratigraphyImportWindow.cs`: importazione di una relazione PDF con l'IA (STRAT-2), con il pulsante spento nella
  Release su richiesta dell'utente;
- il pulsante «Invia / importa terreno da ANTHEA…» di `SheetEditor`, che apre il dialogo condiviso `SoilTransferDialog`.

L'inventario conta fra i quattro file anche `StratigraphyTable` e `SoilHatchBrush`. `StratigraphyTable` è la tabella degli strati
dei pali verticali, dei pali orizzontali e delle fondazioni (non della stratigrafia) e segue quei moduli; `SoilHatchBrush` e
`StratigraphyProfileView` sono disegno e restano nella vista (Q3).

La nota segue i passi 1–5 della «Ricetta per i moduli successivi» di [f5-4-pilota.md](f5-4-pilota.md). Contiene:
- che cosa esiste;
- i comportamenti della vista storica trovati con le tracce;
- che cosa resta da fare;
- le domande per il punto di controllo F5.6b.

La vista XAML, il passaggio in `ModuleImplementations` e l'archiviazione vengono **dopo** il punto di controllo.

Riferimenti:
- [proposta](f5-proposta-architettura.md), sezioni 3, 5, 7 e riga F5.6b della sezione 8 («l'`HttpClient` tolto dalla finestra
  e lasciato al servizio applicativo esistente», «con le prime fixture F3 create prima»);
- [inventario](f5-inventario-ui.md), sezioni 1.4, 1.8, 2.2 e 3.5;
- [modulo](../stratigrafia.md);
- `tests/fixtures/f3/README.md`, `tests/fixtures/f5-stratigrafia/README.md`.

Ramo `refactoring/f5-6b-stratigrafia`, da `refactoring/f5` `92613e60`, 17/10/2026.

## 1. Che cosa esiste dopo la preparazione

| Commit | Contenuto |
| --- | --- |
| `5d76779c` | prime 5 fixture F3 della stratigrafia (66 nel riferimento), attese catturate con la vista storica; ramo della stratigrafia in `F3ReportChapterChecks` |
| `f39de91c` | tracce congelate della vista storica: 15 scenari, suite `ui/check-stratigrafia-historic` |
| `8231c412` | importazione IA e derivazioni in Application (`StratigraphyImport`, `StratigraphyPresentation`), campi condivisi in Presentation (`StratigraphyFields`); baseline invariate |
| `a195b56d` | ViewModel `src/ANTHEA.Presentation/Modules/Stratigraphy`, modulo registrato, dialogo del terreno dietro un'interfaccia, prove senza WPF |

### 1.1 Fixture F3

Prima di F5.6b il modulo non aveva fixture. Le 5 nuove coprono:
- un profilo completo con quota e falda, quattro strati finiti granulari e coesivi, soli medi, nessun avviso;
- «Entrambi» con minimi completi, incompleti e maggiori dei medi, γsat minore di γ, ultimo strato indefinito, falda non nota: tutti
  gli avvisi della scheda;
- i valori come li scrive la scheda: testi con la virgola, testi vuoti, «Minimi», falda non considerata, strato senza comportamento;
- un documento del primo schema con la provenienza di un'importazione IA revisionata (data fissa, nessuna chiave);
- una stratigrafia ricevuta da un foglio del palo verticale.

Gli input vengono dal generatore `F3Fixtures.StratigraphyF56b`, con identificativi fissi. I documenti sono validi: la scheda valida
il documento a ogni conferma (`StratigraphyDocument.Validate`), quindi un documento non valido non si può catturare con il percorso
F3; i documenti non validi stanno nelle tracce. Gli attesi sono 45 file, da due catture uguali.

**Report.** La stratigrafia non calcola: `result.json` è nullo e `hasResults` falso. Il report è la scheda dati che la vista esporta
(`SheetEditor.ExportReport`), catturata come per gli altri moduli (`report.txt`, `report-structure.json`). `ui/f3-report-chapters`
la confronta parte per parte con il capitolo autonomo di `ModuleReportService`: il nuovo ramo di `F3ReportChapterChecks` esporta il
report della vista, perché `BuildReport` della vista storica chiede un calcolo. Il controllo passa da 62 a 67 capitoli.

Nel riferimento F3 cambiano solo due cose, registrate nel manifest (`fixture_aggiunte_f5_6b`): i file aggiunti e il numero delle
fixture in `expected/esito.txt` (61 → 66, impronta prima `1e6e1bf8…`, dopo `9fb85a28…`). Gli attesi esistenti e
`transitions-f4.json` non cambiano.

### 1.2 Tracce della vista storica

Le tracce (`tests/fixtures/f5-stratigrafia/storica`) registrano, dopo l'apertura e dopo ogni passo:
- i byte dei dati nell'ordine in memoria (ordine delle scritture: una chiave nuova va in fondo all'oggetto) e `Modified`;
- i messaggi e le domande del passo con la risposta, e l'eccezione della conferma di un documento non valido;
- il pulsante dell'importazione IA (spento, con il suggerimento);
- i campi generali e quelli dello strato selezionato nell'ordine della scheda, con etichetta, valore, voci e usabilità;
- i pulsanti degli strati, l'elenco, lo strato selezionato, la nota dei minimi assenti, lo stato e il profilo;
- il trasferimento del terreno avviato dalla scheda: modulo e dati passati al dialogo, fogli del progetto ed esito (copia in un foglio
  nuovo con modulo, nome e dati; sostituzione; nessuno, con il testo dell'errore del dialogo).

Gli scenari sono 15: il percorso F3 delle 5 fixture; modifiche dei dati generali e degli strati con i loro errori; azioni sugli
strati (primo e ultimo, indefinito, ultimo rimasto); minimi; valori digitati; invio ai moduli di verifica, a una stratigrafia nuova e
sostituzione dal foglio stesso o da un altro foglio del progetto; sola lettura. Gli identificativi creati durante lo scenario
diventano `<nuovo-n>`.

Due tecniche nuove nell'harness UiTests (`tests/Desktop/StratigraphyHistoricTrace.cs`):
- **`MessageBoxHook`**: i messaggi della vista sono `MessageBox` di sistema. Un aggancio WH_CBT del thread li sposta fuori schermo
  e ne rifiuta l'attivazione; un aggancio WH_CALLWNDPROCRET, alla fine di WM_INITDIALOG, ne legge titolo e testo e preme la risposta
  dello scenario. Un messaggio con il solo OK si chiude con IDCANCEL, l'identificativo del suo unico pulsante.
- **Dialogo modale guidato**: `SoilTransferDialog` è una finestra modale. Prima del clic si accoda al dispatcher un'operazione che
  gira nel ciclo del dialogo, ne usa i controlli e lo chiude se mostra un errore. Il foglio sta in una finestra fuori schermo, quindi
  anche il dialogo, centrato sul proprietario.

La stessa definizione (`tests/Shared/StratigraphyTrace.cs`) guida due prove:
- la vista storica, stadio `ui`, finché esiste;
- il ViewModel, stadio `fast`. Qui il dialogo del terreno, condiviso e non migrato, è una replica dei suoi due percorsi con le stesse
  chiamate a `SoilProfileTransfer`: la traccia prova che cosa il foglio passa al dialogo e che cosa fa con la sua risposta.

### 1.3 Derivazioni in Application e campi condivisi

Spostati senza modifiche, con la vista storica e la finestra che li chiamano:
- `src/ANTHEA.Application/Stratigraphy/StratigraphyImport.cs`: il flusso dell'importazione della finestra:
  - l'estrazione con un client HTTP per richiesta, senza redirect e senza timeout proprio (`OpenAi()`, o `OpenAi(handler)` per le
    prove);
  - i testi della finestra;
  - quali errori si mostrano e con quale testo (`Shown`, `Failure`);
  - nomi dei profili e messaggio della bozza;
  - applicazione della bozza revisionata e sostituzione dei dati;
  - modello e chiave proposti dall'ambiente (`StratigraphyImportDefaults`, il cui testo dice solo se la chiave c'è).

  L'interfaccia `IStratigraphyDraftSource`, implementata anche da `StratigraphyAiImporter`, rende la sorgente iniettabile: nella
  finestra con un costruttore nuovo, nel ViewModel con le opzioni del modulo.
- `src/ANTHEA.Application/Stratigraphy/StratigraphyPresentation.cs`: stato della scheda (avvisi, «Dati validi…» o errore da
  correggere, profilo disegnato o no), scelte predefinite, comportamenti, testi di messaggi e domande, estensione indefinita
  dell'ultimo strato (spessore nullo, o 1 m come numero).
- `src/ANTHEA.Presentation/Modules/Stratigraphy/StratigraphyFields.cs`: l'unica definizione dei campi (chiavi ed etichette
  nell'ordine della scheda), usata dalla vista storica e dal ViewModel.

Uscita identica: tracce uguali, F3 e capitoli autonomi invariati, `Stratigraphy.UiChecks` PASS (usa la finestra per riflessione).
`StratigraphyImportChecks` (Application.Tests) prova il codice spostato con valori scritti a mano e un `HttpMessageHandler` scritto
dalla prova, mai la rete.

**Baseline.** F4 invariata (3349) e P3 invariata (127): nella vista e nella finestra non c'erano voci F4 (testi, regole booleane,
formati; il numero 1.0 è un argomento, non un operando) e il codice spostato non ne crea. La baseline F4 non scende perché non c'era
nulla da togliere.

**Segreti.** Nessuna chiave o segreto nel repository: la finestra legge la chiave solo dall'ambiente (`OPENAI_API_KEY`) e nelle
prove ci sono solo segnaposto non validi («chiave-di-prova», «test-key-not-real»). La chiave non va mai nel documento, nelle tracce o
nei log; le prove lo verificano.

### 1.4 ViewModel

`src/ANTHEA.Presentation/Modules/Stratigraphy`:

| Vista storica | ViewModel |
| --- | --- |
| `StratigraphyWorkspace(data, allowImport)` | `StratigraphyEditorViewModel(data, services, changed, importVisible)`: lo stesso editor serve il foglio e l'anteprima della bozza |
| `Field`/`Choice` dei dati generali | `GeneralForm` (`FormViewModel` con `StratigraphyFields.General`, testo grezzo; scelta predefinita finché la chiave manca) |
| `layers` (`ListBox` con il nome), `SelectionChanged` | `Layers` (`JsonRowCollection` sugli oggetti degli strati), `SelectedLayerIndex` (ammessa anche in sola lettura; −1 come la deselezione con Ctrl+clic) |
| «+ Strato», «Duplica», «Elimina», «↑», «↓» con `Edit` e `MessageBox` | `AddLayer`, `DuplicateLayer`, `RemoveLayer`, `MoveLayerUp`, `MoveLayerDown`; errori con `IMessageService.Inform("Stratigrafia", …)` |
| `ShowLayer`: campi dello strato | `LayerForm`, `ThicknessForm` (nullo se indefinito), `MeansForm`, `MinimaForm` (nullo senza minimi), `NotesForm`, ricostruiti come i controlli |
| casella «Ultimo strato esteso indefinitamente» | `IsInfinite`, `CanToggleInfinite` (solo l'ultimo strato), `ToggleInfinite` |
| casella dei minimi e domanda `MessageBox` | `HasMinimum`, `ToggleMinimum` con `IConfirmationService.ConfirmDelete("Valori minimi", …)` |
| `Refresh`: stato e disegno | `Status`, `Profile` (documento disegnato o nullo), `ProfileSelection` |
| `Import` e `StratigraphyImportWindow` | `ImportCommand` (asincrono, annullabile), `CanImport`, `ImportToolTip`; `StratigraphyImportDialogViewModel` con `IDialogService`/`IFileDialogService` e la sorgente iniettata |
| `SheetEditor.OpenSoilTransfer` | `TransferSoilCommand`: conferma, poi `ISoilTransferDialog` (nel desktop `WpfSoilTransferDialog`, la finestra storica condivisa) e `ISheetHost.OpenModuleCopy`/`ReplaceSoil` |
| `SheetEditor.Commit`/`CalculateAsync` | `OnCommit` valida il documento; `SheetCalculationMode.None` (nessun calcolo, «Calcola» conferma soltanto) |
| `SheetEditor.ExportReport` | `Report`: conferma, poi la scheda di `StratigraphyReport` |
| `RevisionInspection.Protect` | `IsReadOnly`: form in sola lettura, azioni, caselle e comandi spenti; selezione degli strati e conferma ammesse |

`StratigraphySheetModule` (con `StratigraphySheetOptions`: sorgente delle bozze, valori proposti, dialogo del terreno, importazione
attiva o no) non dichiara chiavi di stato: lo strato selezionato non si salva e tutto ciò che il foglio scrive è dato del documento.
È registrato in `DesktopComposition.CreateModules`. Lì l'importazione usa `StratigraphyImport.OpenAi()` e resta spenta, come il
pulsante storico. La Release resta storica: `ModuleImplementations` non cambia.

**Modalità di calcolo.** `None`, come i materiali (M4 di F5.5): la vista storica non calcola; `CalculateAsync` storico conferma
soltanto, cioè valida. Senza risultato, «Esporta risultati» rifiuta come oggi; il report Word della scheda resta disponibile.

### 1.5 Prove

`Presentation.Tests` passa da 921 a 1030 controlli; i 109 nuovi sono di `StratigraphySheetChecks`:
- le 15 tracce uguali riga per riga a quelle della vista storica;
- le 5 fixture F3 uguali al riferimento: aperto, visitato, calcolato, risultato, testo e struttura del report, esportato, stato,
  riaperto;
- modulo, registro, modalità, anteprima, chiavi scritte (un comportamento scelto in uno strato senza la chiave va in fondo, come
  oggi);
- sola lettura, `Reload` (come una riapertura, senza `Modified`), chiusura;
- importazione con una sorgente scritta dalla prova, mai la rete: spenta nella Release; attivata dal modulo: successo con revisione
  e applicazione, revisione azzerata da una modifica della bozza, errori di connessione e dei dati, bozza non valida, interruzione,
  chiusura durante l'estrazione, chiave cancellata alla chiusura;
- trasferimento: copia, sostituzione, dialogo chiuso senza scelta, conferma non valida mostrata dal comando.

Stadio `ui`: `ui/check-stratigrafia-historic`, `ui/check-module-host-offscreen` (registro con la stratigrafia, la Release apre la
vista storica), le suite F3, `Stratigraphy.UiChecks`.

**Corsa finale** `-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` su `a35224c1`:
`supporto/artefatti/ci/20261017-020803-f56b-finale`, PASS 114 (build 25, fast 31, wiki 2, ui 56), NEW-FAIL 0, nessun file
tracciato modificato; `transitions-f4.json` invariato.
- `ui/check-contrast` è escluso: lo schermo è al 150% (144 DPI) e la baseline è misurata a 96 DPI (decisione D6); il coordinatore
  lo ripete a 96 DPI.
- Presentation.Tests 1030; `ui/f3-equivalence` 66 fixture; `ui/f3-report-chapters` 67 capitoli; `ui/check-stratigrafia-historic`
  15 scenari, 7882 righe.

## 2. Comportamenti della vista storica trovati con le tracce

Nessun errore che fermi la vista. Riprodotti dal ViewModel così come sono, da decidere al punto di controllo:
1. **«No» alla rimozione dei minimi solleva `Modified`.** Il documento risulta modificato senza cambiamenti.
2. **Conferma non valida prima del trasferimento.** «Invia / importa terreno» conferma il foglio; con un documento non valido la
   conferma lancia un'eccezione non gestita (registro degli errori e messaggio del gestore globale). Il ViewModel la mostra con il
   `CommandGuard` (Q19), senza registro: è l'unica differenza, e solo dove la vista storica fallisce.
3. **Errori imprevisti dell'estrazione IA.** La finestra scarta il task dell'estrazione (`_ = Extract()`): un errore non previsto
   andrebbe perso. Nel ViewModel passa dal `CommandGuard`. Oggi è irraggiungibile: l'importazione è spenta.
4. **`BuildReport` del foglio storico** chiede un calcolo («Premere Calcola prima di esportare.») e nessuno lo chiama per la
   stratigrafia; il report si esporta con `ExportReport`. Il ViewModel dà la scheda anche da `BuildReport`, che `MvvmSheetEditor`
   usa per esportare.

## 3. Che cosa resta

L'ordine segue il passo 6 della ricetta di F5.4, dopo il punto di controllo F5.6b.

1. **Vista XAML** `X.Desktop/Views/Stratigraphy/StratigraphySheetView.xaml` e il suo DataTemplate, solo con i token di F5.3.
   - Due colonne come oggi: a sinistra il pulsante dell'importazione (spento, con il suggerimento), i dati generali, i pulsanti
     degli strati, l'elenco (`Layers`, nome), i campi dello strato; a destra il profilo e lo stato.
   - `FieldForm` per blocco:
     - generali, identità dello strato con la nota sui terreni misti fra comportamento e descrizione, spessore;
     - medi e minimi su due colonne, o la nota dei minimi assenti;
     - note.

     I campi su più righe (descrizioni, fonti, note) oggi hanno `AcceptsReturn`: vedi S7.
   - Code-behind solo per il disegno: `StratigraphyProfileView` con `Profile` e `ProfileSelection`, il clic su uno strato →
     `SelectedLayerIndex`.
   - Il pulsante «Invia / importa terreno da ANTHEA…» oggi è nella barra di `SheetEditor`: va nella vista.
   - Vista del dialogo dell'importazione (`StratigraphyImportDialogViewModel`, con la chiave in un `PasswordBox` passata dal
     code-behind) e anteprima con la stessa vista dell'editor.
2. **Banco e confronto diretto**: `--module-impl geo_stratigrafia=mvvm`, cattura F3, poi `tools/qa/f5_confronto_diretto.py` a
   differenza zero e il confronto normale con `expected` (nessuna transizione F4 per la stratigrafia).
3. **`Stratigraphy.UiChecks` riscritto** sul ViewModel e sulla vista ospitata da `MvvmSheetEditor`. Oggi
   (`tests/ModelViewer.UiChecks/StratigraphyRenderingChecks.cs`) apre il foglio dalla fabbrica, ma legge la vista storica per albero
   visuale e la finestra dell'importazione per riflessione (`drafts`, `profiles`, `reviewed`, `UpdateApply`). La parte dei retini
   (`SoilHatchBrush`) e del dialogo del terreno resta. Ripetere `ui/check-module-host-offscreen` e le suite di progetto che aprono
   una stratigrafia.
4. **Punto di controllo dell'utente** con il materiale della sezione 4.
5. **Passaggio della Release e ritiro della vista storica (Q11)**, nello stesso commit:
   - `ModuleImplementations.Declared` per `geo_stratigrafia`; `ModuleHostWpfChecks` aggiornato;
   - `StratigraphyWorkspace` (senza `StratigraphyProfileView` e `SoilProfileIcon`, che restano: disegno e icona del catalogo) e
     `StratigraphyImportWindow` in `supporto/SUPERATI/ui-storica/stratigrafia/`; `tests/Desktop/StratigraphyHistoricTrace.cs` e
     la suite `ui/check-stratigrafia-historic` li seguono; le tracce restano per `Presentation.Tests`;
   - in `SheetEditor` si tolgono il campo `soilWorkspace` (riga 31) e i suoi rami (costruttore 95-99, `Commit` 166,
     `CalculateAsync` 255, `ExportReport` 346-349). `OpenSoilTransfer` resta per gli altri moduli dei terreni. Il ramo di
     `MainWindow.ExportReport` (riga 419, nome «Stratigrafia.docx») dipende dal modulo e resta.
6. **Guida pratica e PDF**, se la disposizione descritta cambia.

## 4. Domande per il punto di controllo F5.6b

- **S1 · Riferimento F3.** Prime 5 fixture, catturate con la vista storica; in `expected/esito.txt` cambia solo il numero
  (61 → 66), registrato nel manifest. Per i capitoli autonomi un ramo nuovo in `F3ReportChapterChecks` confronta il capitolo con il
  report esportato dalla vista, perché la stratigrafia non ha calcolo. Come D2 di F5.4: da confermare.
- **S2 · Modalità senza calcolo.** `SheetCalculationMode.None` come i materiali: «Calcola» conferma e valida soltanto, non c'è
  risultato. Da confermare.
- **S3 · Dialogo del terreno.** È condiviso da stratigrafia, muri, pali e fondazioni e resta la finestra storica, dietro
  `ISoilTransferDialog` (in `Modules/Stratigraphy`). Proposta: un ViewModel condiviso del dialogo in un passo dedicato, insieme a
  V9 di F5.6 e ai moduli F5.7/F5.9/F5.9b. L'interfaccia si sposta allora in un posto comune. Da decidere.
- **S4 · Conferma non valida prima del trasferimento** (sezione 2, punto 2). Oggi è un errore non gestito; il ViewModel lo mostra
  con il `CommandGuard`, senza registro. Proposta: differenza dichiarata (voce F5-… del registro) al passaggio della Release.
- **S5 · Importazione IA spenta.** Il ViewModel la conserva spenta con lo stesso suggerimento; l'opzione del modulo
  (`StratigraphySheetOptions.ImportEnabled`) la attiva oggi solo nelle prove. La riattivazione (configurazione e fatturazione da
  definire, STRAT-2) diventa allora una riga della radice di composizione. Va scritta anche la vista del dialogo. Da confermare.
- **S6 · «No» ai minimi che solleva `Modified`** (sezione 2, punto 1). Si conserva in F5 o si corregge come differenza
  dichiarata?
- **S7 · Campi su più righe.** Descrizioni, fonti e note oggi vanno a capo e accettano l'invio. `FieldSpec` non lo sa. Due strade:
  una proprietà `Multiline` di `FieldSpec`/`FieldForm` (componente comune), oppure un modello di campo locale nella vista della
  stratigrafia. Da decidere prima della vista.
- **S8 · Disposizione.** `FieldForm` per blocco come sopra (sezione 3, punto 1), con la nota sui terreni misti fra comportamento e
  descrizione e medi e minimi affiancati. Si accetta?
- **S9 · Voci delle scelte.** `FieldViewModel` aggiunge alle voci il valore corrente quando non è fra quelle (regola di
  `InputForm`); la casella storica no. Con un documento valido non succede mai (`Validate` controlla scelte, famiglie, comportamenti
  e qualificazioni), e le tracce lo confermano. Si accetta?
- **S10 · `BuildReport`** (sezione 2, punto 4): il ViewModel dà la scheda anche lì. Da confermare.
- **S11 · Prove con i messaggi di sistema.** `MessageBoxHook` risponde ai `MessageBox` delle viste storiche fuori schermo, senza
  mouse né tastiera. Può servire anche ai moduli successivi (muri, ponte) per le tracce. Si adotta come strumento comune
  dell'harness?
- **S12 · Baseline F4.** Invariata, perché la vista non aveva voci F4: non c'era nulla da togliere (sezione 1.3).

**Materiale da mostrare** (dopo i passi 1–3 della sezione 3):
- schermate fuori schermo nei tre aspetti e `--check-contrast`;
- confronto diretto a zero;
- `ui/f3-equivalence` con il modulo MVVM;
- conteggio delle suite `ui`;
- questa nota con le risposte.

## 5. Vista XAML (17/10)

Ramo `refactoring/f5-6b-vista`, da `refactoring/f5` `11f48446`, con le decisioni del 17/10 (mattina e tarda mattina): tutte le
proposte della sezione 4 e di [f5-punti-di-controllo.md](f5-punti-di-controllo.md), più le scelte prudenti del coordinatore in
[decisioni.md](decisioni.md). La Release resta **storica**: `ModuleImplementations` non cambia, la vista storica non è archiviata e
le suite storiche non sono riscritte (sezione 5.6). La vista nuova si apre solo nella build UiTests, con
`--module-impl geo_stratigrafia=mvvm` (banco e `ANTHEA.exe --interactive`).

### 5.1 Commit

| Commit | Contenuto |
| --- | --- |
| `f14f6617` | ViewModel allineato alle decisioni; `FieldSpec.Multiline` e `FieldSpec.ToolTip` additivi; dialogo condiviso del terreno nella radice di composizione |
| `4b5e3960` | vista `X.Desktop/Views/Stratigraphy` (foglio, editor, dialogo dell'importazione IA), trigger `Multiline` di `FieldForm`, pulsanti del dialogo condiviso con lo stile del tema |
| `98a93aca` | `tests/Desktop/StratigraphyViewChecks.cs`, comando `--check-stratigrafia-view`, suite `ui/check-stratigrafia-view` |
| `f01698f4` | passo `mvvm-geo_stratigrafia` di `--check-contrast` in un blocco a sé (`StratigraphyContrastStep`), non eseguito |
| (questo commit) | questa sezione |

### 5.2 Decisioni applicate

- **T2.** Sola lettura con `IsReadOnly` dei form dell'editor (già nel ViewModel). Azioni degli strati, caselle «Ultimo strato
  esteso indefinitamente» e «Gestisci anche i valori minimi» e «Invia / importa terreno da ANTHEA…» sono comandi spenti in sola
  lettura; l'elenco degli strati e il clic sul profilo restano ammessi, come nella vista storica protetta da `RevisionInspection`.
- **S7.** Proprietà additiva `FieldSpec.Multiline` (`FieldViewModel.Multiline`), letta da `FieldForm`: invio accettato, testo a capo,
  altezza massima 110 con scorrimento, testo a sinistra, come le caselle storiche con `AcceptsReturn`. In `StratigraphyFields` la
  hanno descrizione (dati generali e strato), fonti (medi e minimi) e note (minimi e strato), le stesse chiavi della vista storica.
  Con `false` (predefinito) `FieldForm` non cambia per gli altri moduli.
- **S8, D10.** `FieldForm` compatti per blocco: dati generali; nome, famiglia e comportamento (`LayerForm`); la nota sui terreni
  misti; descrizione e colore (`DescriptionForm`, blocco nuovo del ViewModel per mettere la nota fra i due); spessore; medi e minimi
  affiancati, o la nota dei minimi assenti; note. Ogni etichetta sta sopra il suo editor, a tutta larghezza (`Wide`), come le
  etichette storiche sopra le caselle.
- **D8–D12.** Titolo «Stratigrafia del terreno» a 22 (`Font.Title`; storico 23); intestazioni 16 → `Font.Heading`, 14 →
  `Font.Subheading`; testi 12 → `Font.Small`; solo token (P7). Nessuna scheda (D11 non si applica).
- **Navigatore della Wiki (lezione delle unioni del 17/10).** Ogni `FormViewModel` dell'editor, anche dell'anteprima della bozza IA,
  ha `WikiNavigator = Context.Services.Wiki`. La Wiki non ha argomenti per i campi della stratigrafia: nessun «?», come oggi; F1 su
  un campo apre la guida del modulo dal navigatore (prima la apriva `SheetEditor` con `WikiContextHelp.Open`), F1 fuori dai campi la
  apre `MvvmSheetEditor`. Nell'anteprima della bozza (finestra a sé) F1 non apre nulla, come nella finestra storica.
- **T5, S3.** «Invia / importa terreno da ANTHEA…» passa dal dialogo condiviso: `StratigraphySheetOptions.Transfer` è facoltativo e
  senza dialogo il foglio usa `SoilTransferDialogService` sui servizi del foglio. `DesktopComposition` non dà più
  `WpfSoilTransferDialog` alla stratigrafia; vale solo per il foglio MVVM (il foglio storico usa ancora
  `SheetEditor.OpenSoilTransfer`). Le fondazioni restano con `WpfSoilTransferDialog`.
  - Equivalenza sulla stratigrafia: le 15 tracce congelate sono uguali anche guidando la vista del dialogo condiviso
    (`SoilTransferTemplates.xaml`) attraverso i suoi controlli. Comprendono 14 trasferimenti (invii ai cinque moduli con «Medi» e
    «Minimi», sostituzioni dal foglio stesso e da un altro foglio del progetto, errori del dialogo, una conferma non valida) e uno
    rifiutato in sola lettura. Inoltre la vista del dialogo e la finestra storica mostrano lo stesso stato (profili, stato,
    descrizione, anteprima, destinazioni, guida, pulsanti) su tutte le 5 fixture della stratigrafia, con e senza gli altri fogli
    del progetto, per ogni destinazione, dopo l'importazione dal progetto e per i due insiemi di parametri (95 stati).
  - Correzione nel dialogo condiviso (vista F5.6): gli stili `Command` e `Primary` dei pulsanti non ereditavano lo stile implicito
    del tema, quindi i pulsanti perdevano padding, margine e altezza minima di `Ui.Button`. Ora hanno `BasedOn`, e le schermate del
    dialogo nuovo e della finestra storica coincidono. Vale anche per il palo verticale.
- **S5.** Importazione IA spenta nella Release, con il suggerimento storico; la vista del dialogo (`StratigraphyImportDialogView`,
  1200×850 come la finestra storica, cornice compresa) è provata solo con il modulo che la attiva e una sorgente scritta dalla prova.
  La chiave sta in un `PasswordBox` passato dal code-behind, solo in memoria, e si cancella quando il dialogo si chiude.
- **S6, T7.** Il «No» alla rimozione dei minimi solleva ancora `Modified`; la casella torna spuntata (il ViewModel rinotifica le due
  caselle dopo ogni clic, come i controlli ricostruiti dalla vista storica).
- **S9, S10.** Accettate e invariate (voci delle scelte di `FieldViewModel`; `BuildReport` dà la scheda).
- **Q19.** I comandi dell'editor passano dal guard dei comandi; gli errori previsti (`ArgumentException` delle azioni) restano
  messaggi «Stratigrafia», come i `MessageBox` storici.
- **Q3.** Code-behind solo per il disegno (`StratigraphyProfileView` storico riceve `Profile` e `ProfileSelection`; il clic su uno
  strato lo seleziona) e per il `PasswordBox` della chiave.
- **Selezione durante la ricostruzione.** L'elenco della vista perde la selezione quando le righe si ricostruiscono (reset della
  collezione); l'editor la ignora mentre ricostruisce, come la vista storica ignorava `SelectionChanged` durante il refresh.

### 5.3 Esiti

| Prova | Esito |
| --- | --- |
| `ui/check-stratigrafia-view` | PASS 181 controlli (circa 2 minuti): 15 tracce attraverso i controlli della vista e del dialogo condiviso uguali a quelle congelate (7882 righe); 5 fixture con visita senza scritture, «Calcola», «Esporta risultati» rifiutato con lo stesso messaggio, report Word e PDF uguali alla vista storica; schermate; F1 (11 pressioni su 11, anche in sola lettura); nomi di automazione; sola lettura; importazione IA con sorgente scritta dalla prova; dialogo condiviso contro la finestra storica (95 stati); banco e Release |
| Cattura F3 con il modulo MVVM (`--module-impl geo_stratigrafia=mvvm`) | PASS 104 fixture, uguale al riferimento `expected` (1066 file) con le transizioni F4 di sempre, nessuna per la stratigrafia; la cattura storica degli stessi binari dà lo stesso esito |
| Confronto diretto (`tools/qa/f5_confronto_diretto.py`) con la cattura storica degli stessi binari | **0 differenze** su 1066 file (1063 uguali byte per byte, 3 JSON uguali in forma canonica; ignorati solo i 6 tempi del palo elastico) |
| `--check-f3-report-chapters` con il modulo MVVM | PASS 105 capitoli autonomi, 246 immagini e tutte le parti XML confrontate con la vista (i 5 della stratigrafia con il report esportato dal foglio MVVM) |
| `Presentation.Tests` | PASS 1813 controlli (da 1799): forme e comandi della vista, dialogo condiviso dal servizio dei dialoghi, navigatore nell'anteprima |
| `Architecture.Tests` | PASS 523; baseline invariate (F4, P3, `f5-static-baseline.json`, `limits.json`) |

Schermate a 144 DPI (schermo al 150%), nella cartella `vista\schermate` della suite: vista nuova e storica nei tre aspetti, ultimo
strato indefinito, area stretta (730 px con scorrimento orizzontale), dialogo dell'importazione con una bozza, dialogo condiviso del
terreno e finestra storica nello stesso stato. `ui/check-contrast` non è stata eseguita (T10: va ripetuta a 96 DPI).

### 5.4 Differenze visibili fra le due viste

- Titolo 23 → 22 (D9) e corpi dei testi portati ai token (sezione 5.2).
- Campi `FieldForm` compatti: fondo `Token.Field.Background` invece del bianco, caselle un poco più basse, testo dei campi a una riga
  allineato a destra come in `InputForm` (le caselle storiche lo allineavano a sinistra), suggerimento con l'etichetta su etichette
  ed editor (φ′ compreso, vedi S14). I testi su più righe restano a sinistra.
- Il pulsante dell'importazione e gli altri pulsanti hanno lo stile del tema come oggi; la riga selezionata dell'elenco usa
  `Token.Selection` (il modello della riga mostra comunque il colore di selezione di sistema, come oggi).
- «Invia / importa terreno da ANTHEA…» apre il dialogo condiviso (T5) invece della finestra storica: stesso contenuto, finestra a
  dimensione fissa (`WpfDialogService` adatta la finestra al contenuto), piccole differenze di spaziatura.
- Dialogo dell'importazione IA a dimensione fissa 1200×850 (la finestra storica era ridimensionabile, minimo 850×600).
- Solo dove la vista storica fallisce: un errore imprevisto di un'azione o del trasferimento si vede come «Operazione non
  completata» (guard, Q19) invece del gestore globale con il registro degli errori (S4 per la conferma non valida).

### 5.5 Domande «da decidere» (scelte provvisorie, da rivedere in F5.15)

- **S13 · Allineamento del testo.** Campi a una riga allineati a destra, la convenzione di `FieldForm`/`InputForm` (anche i campi
  `Wide` di altri moduli, per esempio il nome dell'acciaio). Alternativa: una proprietà additiva di `FieldSpec` per l'allineamento a
  sinistra dei testi. **Scelta presa:** convenzione di `FieldForm`, nessuna API comune nuova.
- **S14 · Suggerimento di φ′.** `FieldViewModel` usa `CalculationHelp.Field(chiave)` per etichetta ed editor; per la chiave «phi»
  quel testo è il coefficiente di viscosità φ del calcestruzzo, non l'angolo di resistenza φ′ del terreno. **Scelta presa:** proprietà
  additiva `FieldSpec.ToolTip`; la stratigrafia mette l'etichetta come suggerimento di φ′ (medi e minimi). Alternativa: suggerimenti
  per modulo in `CalculationHelp`.
- **S15 · Identità in due blocchi.** `LayerForm` ha ora nome, famiglia e comportamento; `DescriptionForm` descrizione e colore, per
  la nota sui terreni misti fra i due (S8). **Scelta presa:** due form; le tracce e `Presentation.Tests` lo seguono. Alternativa: la
  nota come `FieldSpec.Note` del comportamento (riquadro giallo delle note, diverso dal testo storico).
- **S16 · Finestra del dialogo IA.** Dimensione fissa 1200×850 come per i dialoghi di F5.6 (domanda già aperta lì). Alternativa:
  finestra ridimensionabile con minimo 850×600 in `WpfDialogService`.
- **S17 · Pulsanti del dialogo condiviso.** Corretti con `BasedOn` (sezione 5.2). Da confermare insieme alla stessa correzione per la
  vista del palo verticale (`VerticalPileSheetView.xaml`, stile `Command`), non fatta qui perché fuori dal modulo.

Segnalazioni, senza correzioni:
- **Muri, vista storica.** «Angolo di attrito caratteristico» (chiave «phi» di `RetainingWallFields`, `InputForm`) ha come
  suggerimento dell'etichetta l'aiuto della viscosità φ del calcestruzzo (`CalculationHelp.Field("phi")`). Stessa causa di S14.
- **Vista del palo verticale.** Pulsanti senza lo stile del tema (S17).
- **Finestra storica dell'importazione.** Legge la chiave dall'ambiente (`OPENAI_API_KEY`) già alla costruzione, nei suoi campi: le
  prove della vista nuova non la costruiscono mai; la vista nuova riceve la chiave solo dalle opzioni del modulo all'apertura del
  dialogo.

### 5.6 Suite storiche da riscrivere al passaggio della Release

Nello stesso commit del passaggio (sezione 3, punto 5):
- `Stratigraphy.UiChecks` (`tests/ModelViewer.UiChecks/StratigraphyRenderingChecks.cs`): apre il foglio dalla fabbrica ma legge la
  vista storica per albero visuale e la finestra dell'importazione per riflessione (`drafts`, `profiles`, `reviewed`, `UpdateApply`).
  Va riscritta sul ViewModel e sulla vista nuova; i testi e i nomi che legge esistono già nella vista nuova (provato da
  `ui/check-stratigrafia-view`). La parte dei retini (`SoilHatchBrush`) e del dialogo del terreno resta.
- `ui/check-stratigrafia-historic` (`tests/Desktop/StratigraphyHistoricTrace.cs`): segue la vista storica in `supporto/SUPERATI`; le
  tracce congelate restano per `Presentation.Tests` e per `ui/check-stratigrafia-view`.
- `ui/check-stratigrafia-view`: i confronti con `SheetEditor` (esportazioni, schermate storiche, nomi di automazione, sola lettura) e
  il controllo «la Release resta storica» si tolgono o passano a dati congelati; il confronto del dialogo condiviso con la finestra
  storica resta finché `SoilTransferDialog` serve alle fondazioni.
- `ui/check-module-host-offscreen` (`tests/Desktop/ModuleHostWpfChecks.cs`): «la Release apre geo_stratigrafia con la vista storica»
  diventa la vista MVVM.
- `--check-contrast`: si toglie `StratigraphyContrastStep`; il passo `modulo-geo_stratigrafia` mostra la vista nuova e le voci della
  Chiara passano al passo del modulo alla prima corsa a 96 DPI (D12).
- Le suite F3 (`ui/f3-equivalence`, `ui/f3-report-chapters`) non cambiano: passano già con il modulo MVVM (sezione 5.3).

### 5.7 Da provare a mano in una build di prova (versione portatile con la vista attiva)

1. Aprire una stratigrafia di un progetto reale: dati generali, elenco degli strati, profilo e stato come oggi; selezionare gli
   strati dall'elenco e con un clic sul profilo; restringere la finestra (scorrimento orizzontale sotto i 730 px).
2. Modificare nome, descrizione su più righe (Invio), quota e falda; i campi dello strato (famiglia, comportamento, colore, spessore,
   parametri, fonti e note); provare valori non validi e leggere lo stato.
3. «+ Strato», «Duplica», «Elimina», «↑», «↓», anche sul primo e sull'ultimo strato e con l'ultimo indefinito (messaggi).
4. «Ultimo strato esteso indefinitamente» sull'ultimo strato; «Gestisci anche i valori minimi» con la domanda (Sì e No).
5. «Invia / importa terreno da ANTHEA…»: invio a un nuovo foglio di ogni modulo con «Medi» e «Minimi», «Carica terreno…» e «Salva
   terreno…», importazione da un altro foglio del progetto, sostituzione del terreno del foglio.
6. Una revisione precedente (sola lettura): campi e comandi bloccati, elenco e profilo utilizzabili.
7. Report Word e PDF; «Esporta risultati» rifiutato come oggi.
8. F1 su un campo e fuori dai campi (guida del modulo).
9. Aspetti Scuro e Molto scuro.

L'importazione IA resta spenta nella Release (S5): non si prova a mano.

### 5.8 Corsa finale

Corsa `-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` sul commit di questa sezione: l'esito è nel commit successivo.
