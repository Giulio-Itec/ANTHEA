# F5.9b — Fondazioni superficiali: preparazione

## Scopo

Questa nota chiude la **preparazione** di F5.9b: modulo `geo_fondazioni_superficiali`, vista storica `FoundationWorkspace`
(`X.Desktop/Wpf/FoundationWorkspace.cs`, `FoundationWorkspace.Materials.cs`, `FoundationViews.cs`, `FoundationIcon.cs`). I tipi
senza WPF del modulo stanno in `src/ANTHEA.FoundationWorkspace` (documento, materiali, presentazione) e il report in
`src/ANTHEA.Reports/Generators/FoundationReport.cs`. La nota segue i passi 1–5 della «Ricetta per i moduli successivi» di
[f5-4-pilota.md](f5-4-pilota.md). Contiene:
- che cosa esiste dopo la preparazione;
- i difetti della vista storica trovati con le tracce;
- che cosa resta per finire F5.9b;
- le domande per il punto di controllo dell'utente.

La vista XAML, il passaggio in `ModuleImplementations` e l'archiviazione della vista storica vengono **dopo** il punto di
controllo: non fanno parte di questa preparazione. La vista XAML, il banco e il confronto diretto sono stati fatti il 17/10, dopo
le decisioni del punto di controllo: sezione 5. Restano la prova dell'utente, il passaggio della Release e l'archiviazione.

Riferimenti:
- [proposta](f5-proposta-architettura.md), sezioni 3, 5, 7 e riga F5.9/F5.9b della sezione 8;
- [inventario](f5-inventario-ui.md), sezioni 1.4 e 3.5;
- [scheda tecnica del modulo](../sviluppo/fondazioni-superficiali.md), voci FON-02 e FON-03 del registro (contratto 2 della
  libreria GPC Geotechnics, 12/10);
- `tests/fixtures/f3/README.md` e `tests/fixtures/f5-fondazioni/README.md`.

Ramo `refactoring/f5-9b-fondazioni`, da `refactoring/f5` `92613e60`, 16/10/2026. Si lavora sulla raccomandazione Q8 (a), come
per la palificata: il calcolo breve resta sincrono sul thread dell'interfaccia, come oggi.

## 1. Che cosa esiste dopo la preparazione

| Commit | Contenuto |
| --- | --- |
| `615b6849` | 8 fixture F3 nuove delle fondazioni (3 → 11; 69 nel riferimento), attese catturate con la vista storica |
| `3c15fdfb` | tracce congelate della vista storica: 15 scenari, suite `ui/check-fondazioni-historic` |
| `f7bfe54f` | derivazioni in Application (`ShallowFoundationPresentation`), campi condivisi in Presentation (`ShallowFoundationFields`), baseline F4 3349 → 3347 |
| `8ff7cab0` | ViewModel `src/ANTHEA.Presentation/Modules/ShallowFoundation`, modulo registrato, prove senza WPF |

### 1.1 Fixture F3

Le 8 fixture nuove coprono:
- tutti i metodi: EC7, Terzaghi, Meyerhof, Brinch Hansen, Vesić;
- le tre forme: rettangolare, nastriforme (azioni per metro), circolare centrata;
- condizioni drenata e non drenata, con le correzioni additive di Hansen;
- il confronto caratteristico senza la conferma A1;
- le due riduzioni per la compressibilità (taglio locale, densità relativa);
- falda sopra e sotto la base;
- azioni eccentriche con contatto parziale (area compressa minore della base);
- la risultante esterna alla base: resistenza nulla, rapporti indefiniti, viste dei risultati non disponibili;
- due sondaggi con il secondo scelto;
- calcestruzzo del catalogo e personalizzato;
- input raccolti e gruppi chiusi salvati nel foglio: la visita della cattura li riapre e li scrive;
- un archivio di versione 1 con falda e base al piano campagna;
- due calcoli fermati: uno strato incompleto, il semispazio non assunto.

Prima della cattura ogni input è stato calcolato con il servizio del modulo, per verificare l'esito descritto. Tre bozze sono
state corrette così: Terzaghi accetta solo la base ruvida, la riduzione di Vesić richiede c′ = 0, EC7 rifiuta la base liscia.

Gli input vengono dal generatore `F3Fixtures.ShallowFoundationF59b`. Gli attesi (68 file) vengono da due catture uguali byte per
byte. Gli attesi delle 61 fixture esistenti e `transitions-f4.json` non cambiano; in `expected/esito.txt` cambia solo il numero
delle fixture, 61 → 69, registrato nel manifest (`fixture_aggiunte_f5_9b`, catena dall'impronta `1e6e1bf8…`).
`ui/f3-equivalence` e `ui/f3-report-chapters` (70 capitoli) passano.

Il generatore `fixtures-f3` oggi produce input diversi da quelli congelati per dieci fixture più vecchie (muri, calcestruzzo,
Bridge Design, sezione c.a.): i default dei cataloghi sono cambiati dopo F3. Come in F5.4–F5.6 si sono copiate solo le fixture
nuove; quelle vecchie restano come sono.

### 1.2 Tracce della vista storica

Le tracce (`tests/fixtures/f5-fondazioni/storica`) registrano, dopo l'apertura e dopo ogni passo:
- i byte dei dati nell'ordine in memoria;
- `Modified`, la sola lettura e le richieste del report;
- risultato (impronta) e stato;
- i 25 campi nell'ordine della scheda: scelte con le voci, numeri come testo grezzo, opzioni, unità e visibilità;
- i quattro gruppi salvati e gli input raccolti, con il testo del pulsante;
- sondaggi, sondaggio scelto, righe degli strati come le mostra la tabella (colore escluso) e semispazio;
- i risultati: titolo, riepilogo, tabelle dei coefficienti e delle correzioni additive con le colonne, note e avvisi;
- le sei schede grafiche come dati: disegni della geometria e della stratigrafia sui dati del foglio; messaggio delle viste dei
  risultati, oppure impronta dei grafici, testi delle tensioni e degli andamenti, sezione e impronta del meccanismo.

Gli scenari sono 15: il percorso F3 per ognuna delle 11 fixture, `modifiche`, `gruppi-e-raccolta`, `stratigrafia`,
`sola-lettura` (elenco in `tests/fixtures/f5-fondazioni/README.md`). Ogni passo è fatto con i controlli, come l'utente: un
controllo nascosto dalle regole della scheda, spento o protetto dalla revisione non fa nulla. Due catture uguali byte per byte,
8198 righe.

La sola modifica alla vista storica è il parametro `confirm` di `DeleteSurvey`, vero dal pulsante: la traccia risponde sì alla
domanda senza aprire la finestra di messaggio, come `GeoEditor.DeleteSurvey`. Comportamento invariato.

La stessa definizione (`tests/Shared/ShallowFoundationTrace.cs`) guida due prove:
- la vista storica, in `tests/Desktop/ShallowFoundationHistoricTrace.cs`, stadio `ui`, finché la vista esiste;
- il ViewModel, in `tests/ANTHEA.Presentation.Tests/ShallowFoundationSheetChecks.cs`, stadio `fast`.

### 1.3 Derivazioni in Application e campi condivisi

`src/ANTHEA.Application/Presentation/ShallowFoundationPresentation.cs` contiene il codice che la vista calcolava da sé, spostato
senza modifiche:
- regola di visibilità dei campi: WaterEnabled dai dati comuni, conferma A1 solo con NTC 2018, Mx/My/Hy visibili se diversi
  da «0», compressibilità visibile se non nulla;
- stato dei gruppi in `vista_fondazioni` (lettura con ripiego, scrittura);
- nomi e scelta del sondaggio, lettera dello strato;
- titolo del risultato, riga del riepilogo, stato dopo il calcolo, testo delle pressioni, chiave del meccanismo, testo dei
  risultati senza calcolo (con o senza la libreria approvata);
- voci e sincronizzazione del calcestruzzo condiviso: una classe del catalogo copia i suoi valori, un fck digitato nomina la
  classe con quel fck o «Personalizzato»;
- testi fissi e colonne delle tabelle.

`src/ANTHEA.Presentation/Modules/ShallowFoundation/ShallowFoundationFields.cs` è l'unica definizione di etichette, unità, voci,
gruppi e colonne degli strati, letta dalla vista storica e dal ViewModel (come `VerticalPileFields` in F5.6).

La vista storica chiama queste funzioni e dà un'uscita identica: tracce, F3, capitoli autonomi e `FoundationWorkspace.UiChecks`.
`ShallowFoundationPresentationChecks` (Application.Tests) le prova con valori scritti a mano.

**Baseline F4 3349 → 3347**, solo rimozioni e uno spostamento: la lettera dello strato (`'A' + i % 26`) compariva tre volte
(`FoundationSoilView.OnRender` ×2, `FoundationWorkspace.BuildSoils.Labels` ×1) e diventa la sola
`ShallowFoundationPresentation.LayerLetter` (una voce R2). Le altre voci F4 delle viste delle fondazioni sono di disegno
(`FoundationViews.cs`: scale, margini, conversioni di unità per le quote) e restano nella vista (Q3); escono solo se un giorno i
disegni ricevono dati già convertiti, che sarebbe una modifica dei disegni e non di questo passo (domanda FN7). Nessuna voce P3
nuova: le liste nuove sono proprietà `IReadOnlyList` in sola lettura.

### 1.4 ViewModel

`ShallowFoundationSheetViewModel` (`src/ANTHEA.Presentation/Modules/ShallowFoundation`) riproduce `FoundationWorkspace`:

| Vista storica | ViewModel |
| --- | --- |
| costruttore: `ValidateShape`, `Complete` (migrazione v1 → v2), campi, `RefreshSurveyChoices`, `BuildSoils`, `RefreshVisibility`, `ClearResults` | costruttore: `Build` con la stessa sequenza; nessun calcolo |
| `Choice(...)`: combo che scrive l'**indice** in `input` | `ShallowFoundationChoiceViewModel` (`MethodChoices`, `Compression`): esplicito, perché `FieldSpec` scrive il testo della voce |
| `Number(...)`, `Option(...)`: testo grezzo, booleani | `GeometryForm`, `ActionsForm`, `SurchargeForm`, `RelativeDensityForm`, `MethodOptionsForm`, `WaterUnitWeightForm` (`FormViewModel`, `RawText`) |
| `InputForm` della falda (generali) | `WaterForm` |
| nome della combinazione, conferma A1 | `CombinationForm`, `ConfirmationForm` |
| `ConcreteEditor` (`InputForm` con la sincronizzazione) | `ConcreteForm` + `ShallowFoundationPresentation.ConcreteChanged`, valori mostrati con `FormViewModel.Set(…, display: true)` |
| `RefreshVisibility` | stessa regola (`FieldVisible`): `ShowField` dei form, `IsVisible` delle scelte; `InputDrawing` rinnovato |
| `InputGroup` + `SaveView` (`vista_fondazioni`) | `Groups` (`ShallowFoundationGroupViewModel.IsExpanded`), `ChangedKeepingResults` |
| «Raccogli input» | `ToggleInputs`, `InputsCollapsed`, `CollectText` |
| combo dei sondaggi, «+ Sondaggio», «− Sondaggio» (`MessageBox`) | `Surveys`, `SelectedSurvey`, `AddSurvey`, `RemoveSurvey` (`IConfirmationService.ConfirmDelete`) |
| `JsonGrid` con copie di presentazione, `Labels` | `Layers` (`JsonRowCollection`, copie con «__strato»), `LayerColumns`, `AddLayer`, `RemoveLayer`; colore dello strato lasciato alla vista |
| casella del semispazio | `HalfSpaceForm` |
| `Changed` | `Edited`: regole, risultati azzerati, «Dati modificati · premere Calcola», `Changed` della base |
| `TryCalculate` (sincrono, `ModuleServices.ForFoundation`) | `SheetCalculationMode.Synchronous`, nessun ricalcolo automatico; `CalculateSynchronously` azzera i risultati e calcola con l'`IModuleService` del contesto; un errore è il testo dello stato |
| pannello dei risultati | `ResultsText`, `HasDetails`, `Summary`, `Coefficients`, `Additive`, `Notes`, colonne |
| sei schede grafiche (`FoundationPlanView`, `FoundationPressureSection`, `FoundationMechanismView`, `FoundationSoilView`, `FoundationInputPlan`) | dati semplici: `InputDrawing`, `GraphicsMessage`, `Graphics`, `PressureSummary`, `ContactDescription`, `MechanismAxes`, `MechanismAxis`, `Mechanism`, `MechanismDescription`; i disegni restano WPF (Q3) |
| «Report Word e PDF», «Guida» | `RequestReport` (`ISheetHost.RequestReport`), `OpenGuide` (`IWikiNavigator`) |
| `BuildReportDocument` | `Report`: `FoundationReport.WordDocument` sul risultato, stessi byte DOCX |
| `RevisionInspection.Protect` | `IsReadOnly`: form in sola lettura (D4), scelte, sondaggi, strati e sezione del meccanismo rifiutati; gruppi, «Raccogli input», report e calcolo ammessi |

`ShallowFoundationSheetModule` dichiara la chiave di stato `vista_fondazioni`, nessun report short, e legge la disponibilità della
libreria approvata (`FoundationLibrary.Available`) alla creazione del foglio. È registrato in
`DesktopComposition.CreateModules`; `ModuleHostWpfChecks` lo conta. La Release resta storica: `ModuleImplementations` non cambia.

Non ha ancora un equivalente nel ViewModel l'«Invia / carica terreno…» (`SoilTransferDialog`, oggi aperto da `SheetEditor`): vedi
FN8, come V9 di F5.6.

### 1.5 Prove

- `Presentation.Tests` passa da 921 a 1068 controlli; i 147 nuovi sono di `ShallowFoundationSheetChecks`:
  - 15 tracce uguali riga per riga a quelle della vista storica;
  - 11 fixture uguali al riferimento F3: aperto, visitato (gruppi aperti), calcolato, risultato, testo e struttura del report,
    esportato, stato, riaperto. Sono uguali byte per byte, salvo i 4 file delle due fixture vecchie con la transizione F4 della
    versione della libreria (F4-35), confrontati come in `ui/f3-equivalence` con quelle transizioni ammesse;
  - modulo, registro e chiave di stato (gruppi e input raccolti scrivono solo `vista_fondazioni` e conservano il risultato);
  - sola lettura dal contesto, `Reload` (migrazione v1, nessun `Modified`, nessun risultato), chiusura;
  - servizio di calcolo iniettato: nessun calcolo all'apertura né automatico, «Calcola» sincrono sul thread dell'interfaccia,
    errore nello stato;
  - testo dei risultati senza la libreria approvata; domanda dell'eliminazione del sondaggio con risposta «no».
- `Application.Tests`: `ShallowFoundationPresentationChecks`.
- Stadio `ui`: `ui/check-fondazioni-historic`, `ui/f3-equivalence`, `ui/f3-report-chapters`, `FoundationWorkspace.UiChecks`,
  `ui/check-module-host-offscreen`.
- Architettura: P1–P7 rispettate, F4 3347, P3 invariata (127), nessuna dipendenza dal calcolo nei metadati di Presentation.

**Corsa finale** `-Stage build,fast,wiki,ui` su `dd5f1d93`: `supporto/artefatti/ci/20261016-225809-f59b-finale`, PASS 114,
NEW-FAIL 1 (build 25, fast 31, wiki 2, ui 57, una suite `ui` in più: `ui/check-fondazioni-historic`), nessun file tracciato
modificato.
- `ui/f3-equivalence` PASS con 69 fixture e `transitions-f4.json` invariato; `ui/f3-report-chapters` PASS con 70 capitoli;
  `ui/check-fondazioni-historic` 15 scenari; `ui/check-module-host-offscreen` 42 controlli; `FoundationWorkspace.UiChecks` 89;
  Presentation.Tests 1068; Application.Tests 1866; Architecture.Tests 511.
- **`ui/check-contrast` fallita per l'ambiente, non per questo ramo.** Durante la corsa la finestra aveva 144 DPI invece dei 96
  della baseline (lo schermo della macchina era passato al 150%; la verifica lo dichiara: «misure non confrontabili»). Scuro 0 e
  Molto scuro 0; Chiara 694 difetti invece di 699. Le 5 voci mancanti sono pagine di scorrimento di viste non toccate da questo
  ramo (palo e micropalo orizzontale «p3», sezione mista «p4»), che a 144 DPI non esistono; le 25 voci delle fondazioni sono
  uguali alla baseline una per una. La baseline non si cambia: la suite va ripetuta a 96 DPI (FN11).

## 2. Difetti della vista storica trovati

1. **Sezione del meccanismo in sola lettura.** In una revisione in sola lettura la scelta «Sezione X / Sezione Y» della scheda
   «Rottura» è bloccata solo se la scheda esisteva al momento della protezione o se è visibile: `RevisionInspection` rinfresca
   soltanto l'albero visuale, e la combo ricreata da un calcolo in una scheda non mostrata resta attiva. È una scelta di sola
   vista, non salvata. Il ViewModel la rifiuta sempre in sola lettura, come l'interasse mostrato della palificata; la traccia
   usa il caso in cui la vista storica la blocca. Vedi FN4.
2. **Gruppi in sola lettura.** In sola lettura aprire o chiudere un gruppo e «Raccogli input» scrivono `vista_fondazioni` e
   segnano il foglio come modificato. Non è un errore di calcolo ma rende «modificata» una revisione precedente; il ViewModel lo
   conserva. Vedi FN3.

Nessun difetto nei risultati: tracce, F3 e capitoli autonomi coincidono.

## 3. Che cosa resta per finire F5.9b

Passo 6 della ricetta, dopo il punto di controllo (l'ordine segue i passi 1–7 di F5.4).

**Stato al 17/10** (ramo `refactoring/f5-9b-vista`, sezione 5): punti 1–4 fatti; del punto 5 c'è la suite nuova
`ui/check-fondazioni-view`, mentre la riscrittura di `FoundationWorkspace.UiChecks` aspetta il passaggio della Release. Restano la
riscrittura del punto 5 e i punti 6 e 7, dopo la prova dell'utente con la build di prova (sezione 5.6).
1. **Vista XAML** `X.Desktop/Views/ShallowFoundation/ShallowFoundationSheetView.xaml` e il suo DataTemplate, solo token di F5.3
   (P7):
   - disposizione di oggi: colonna degli input (0,8*, minimo 370 px) e grafici (1,2*, minimo 440 px), stratigrafia sotto
     (massimo 270 px), risultati in fondo; «Raccogli input» nasconde input e stratigrafia;
   - i quattro gruppi salvati restano `Expander` legati a `Groups[i].IsExpanded` (la visita di `ui/f3-equivalence` li apre e
     scrive `vista_fondazioni`, come oggi); «Opzioni del metodo» e «Calcestruzzo» restano `Expander` senza stato;
   - campi con `FieldForm` per blocco; le cinque scelte a indice con `ComboBox` legate a `SelectedIndex`/`IsVisible`/`CanChange`;
   - tabella degli strati con le colonne di `StratigraphyTable` (γsat in grigio quando vuoto, colore dal token per indice);
   - disegni WPF invariati (`FoundationPlanView`, `FoundationPressureSection`, `FoundationMechanismView`, `FoundationSoilView`,
     `FoundationInputPlan`) alimentati dai dati semplici nel code-behind (Q3); il campione di pressione sotto il puntatore resta
     nel disegno;
   - sola lettura: legami a `CanEdit`, tranne gruppi, «Raccogli input», schede, report, guida e calcolo.
2. **Banco**: cattura F3 con `--module-impl geo_fondazioni_superficiali=mvvm` (anche `ui/f3-report-chapters`).
3. **Confronto diretto** fra cattura storica e MVVM a differenza zero (`tools/qa/f5_confronto_diretto.py`), poi il confronto
   normale; ogni differenza va in `transitions-f5.json` con l'ok dell'utente.
4. **`MvvmSheetEditor`**: per le fondazioni l'esportazione del report deve scrivere il DOCX di `FoundationReport.Word` e il PDF di
   `FoundationReport.Pdf` (MigraDoc), come `SheetEditor.ExportReport`; oggi l'ospite ricava il PDF dal DOCX riletto
   (`ReportFiles.Write`). Vedi FN6.
5. **Suite UI riscritte** prima del passaggio della Release:
   - `FoundationWorkspace.UiChecks` (`tests/FoundationWorkspace.Checks/Program.cs`, metodo `Ui` e la parte `--ui` di
     `ContractChecks` e `--candidate`): oggi crea per riflessione `X.Desktop.FoundationWorkspace` con la libreria, chiama
     `TryCalculate`, legge `Result`, cerca il `TextBox` con Tag `Width` e il pulsante `raccogli-input`, scorre le sei schede e
     cambia aspetto. Diventa la vista XAML ospitata da `MvvmSheetEditor` fuori schermo (con la libreria candidata iniettata
     nel servizio del contesto per `--candidate`), come `ui/check-pile-group-view` (D13);
   - la parte UI di `FoundationWorkspace.Checks` resta nello stadio `ui`; le parti di documento, adattatore, report e progetti
     (fast) non cambiano;
   - `ui/check-fondazioni-historic` resta finché la vista storica esiste.
6. **Punto di controllo**, poi passaggio della Release e archiviazione (Q11) nello stesso commit:
   - `ModuleImplementations.Declared`: `("geo_fondazioni_superficiali", SheetImplementation.Mvvm)`;
   - `FoundationWorkspace.cs` e `FoundationWorkspace.Materials.cs` in `supporto/SUPERATI/ui-storica/fondazioni/`, con
     `tests/Desktop/ShallowFoundationHistoricTrace.cs`; i disegni di `FoundationViews.cs` e `FoundationIcon` restano;
   - rami `foundation` di `SheetEditor` (campo 30, `Result` 35, costruttore 100-104, `Commit` 166, `CalculateAsync` 256,
     `ExportReport` 350-356, `BuildReportDocument` 373); `MainWindow.ExportReport` (riga 426) sceglie per modulo e resta;
   - le tracce congelate restano: `Presentation.Tests` continua a usarle.
7. **Guida pratica e PDF**, se la disposizione descritta cambia.

## 4. Domande per il punto di controllo F5.9b

- **FN1 · Q8 (a) per le fondazioni.** Calcolo sincrono sul thread dell'interfaccia e **nessun ricalcolo automatico**: si calcola
  solo con «Calcola», come oggi (`SheetCalculationMode.Synchronous`, ritardo nullo). Da confermare.
- **FN2 · Riferimento F3.** `expected/esito.txt` 61 → 69, registrato nel manifest (`fixture_aggiunte_f5_9b`), come D2 di F5.4. Il
  coordinatore ricatena l'impronta con le fixture di F5.7.
- **FN3 · Gruppi in sola lettura.** Aprire o chiudere un gruppo e «Raccogli input» scrivono `vista_fondazioni` e sollevano
  `Modified` anche in una revisione in sola lettura, come oggi (sezione 2, punto 2). Conservare, o renderli stato della vista
  non salvato in sola lettura (differenza dichiarata)?
- **FN4 · Sezione del meccanismo in sola lettura.** Il ViewModel la rifiuta sempre (sezione 2, punto 1). L'alternativa è
  ammetterla sempre, essendo stato della vista non salvato: differenza dichiarata rispetto ai casi in cui oggi è bloccata.
- **FN5 · Scelte a indice.** Le cinque scelte del metodo scrivono l'indice della voce, non il testo: restano un componente
  esplicito (`ShallowFoundationChoiceViewModel`) invece di `FieldSpec`. Si accetta, o si aggiunge una regola di scrittura
  «indice» a `FieldWriteRule` (componente comune)?
- **FN6 · PDF del report.** L'ospite MVVM deve scrivere il PDF con `FoundationReport.Pdf` come oggi (sezione 3, punto 4), non
  ricavarlo dal DOCX. Da confermare insieme a M12 di F5.5.
- **FN7 · Baseline F4 dei disegni.** Resta a 3347: le voci rimaste delle fondazioni sono nei disegni (Q3). Si accetta che
  scendano solo se si decide di passare ai disegni dati già convertiti (lavoro dei disegni, fuori da F5.9b)?
- **FN8 · Trasferimento del terreno.** «Invia / carica terreno…» apre `SoilTransferDialog` da `SheetEditor`; il ViewModel non ha
  ancora il comando. Stessa proposta di V9 (F5.6): un ViewModel del trasferimento con `IDialogService`, quando si scrive la
  vista o con F5.6b.
- **FN9 · Aiuto dei campi.** I numeri della vista storica non hanno «?» né F1 per campo (righe scritte a mano); la falda e il
  calcestruzzo sì (`InputForm`). Il ViewModel conserva la distinzione (navigatore Wiki solo su quei due form). Con `FieldForm`
  si potrebbe dare l'aiuto a tutti i campi: differenza visibile, da decidere.
- **FN10 · Disponibilità della libreria.** Il ViewModel riceve dal modulo se il rilascio approvato è installato
  (`FoundationLibrary.Available`) e cambia solo il testo dei risultati senza calcolo; il calcolo resta nel servizio del contesto.
- **FN11 · Contrasto a 96 DPI.** `ui/check-contrast` della corsa finale non è confrontabile (144 DPI, sezione 1.5). Va ripetuta a
  96 DPI prima dell'unione: il ramo non tocca colori né viste diverse dalle fondazioni, e le voci delle fondazioni coincidono già.

**Materiale da mostrare** (dopo i passi 1–3 della sezione 3): schermate nei tre aspetti, `--check-contrast`, confronto diretto a
zero, `ui/f3-equivalence` con il modulo MVVM, conteggio delle suite `ui`, questa nota con le risposte.

**Risposte del 17/10** ([decisioni](decisioni.md), sezione «17 ottobre (mattina)»): proposte accettate (FN1, FN2 con T1, FN3 e FN4
con T6, FN6 con T8, FN7 con T12, FN8 con T5, FN10 informativa, FN11 con T10); FN5: componente esplicito per le scelte a indice;
FN9: l'aiuto dei campi resta com'è, senza estenderlo.

## 5. Vista XAML (17/10)

Ramo `refactoring/f5-9b-vista`, da `refactoring/f5` `7979f79d`, con le decisioni del 17/10. La Release resta **storica**:
`ModuleImplementations` non cambia, la vista storica non è archiviata, `FoundationWorkspace.UiChecks` e le parti UI di
`FoundationWorkspace.Checks` non sono riscritte (aspettano il passaggio della Release, dopo la prova dell'utente). La vista nuova si
apre solo nella build UiTests, con `--module-impl geo_fondazioni_superficiali=mvvm`.

### 5.1 Commit

| Commit | Contenuto |
| --- | --- |
| `e3f96a60` | ViewModel allineato alle decisioni, comandi della vista, «Invia / carica terreno…» dietro `ISoilTransferDialog`, `IModuleReportPdf` e PDF di MigraDoc in `MvvmSheetEditor` |
| `51813834` | vista `X.Desktop/Views/ShallowFoundation` (`ShallowFoundationSheetView.xaml(.cs)`, convertitori, DataTemplate unito da `App.xaml`) |
| `66182043` | `tests/Desktop/ShallowFoundationViewChecks.cs`, comando `--check-fondazioni-view`, suite `ui/check-fondazioni-view` |
| `b022bf6d` | passo `mvvm-geo_fondazioni_superficiali` di `--check-contrast` (non eseguito: 144 DPI) |
| `9cf2ced7` | dettagli della verifica chiusi a ogni calcolo, come nella vista storica (stato di vista `DetailsExpanded`) |
| `4c1fdc38` | questa sezione |
| `4fbc3397` | passo del contrasto in un blocco a sé (`ShallowFoundationContrastStep`), senza il meccanismo `mvvm-` tolto dalla chiusura del pilota |

### 5.2 Decisioni applicate

- **FN1, T3.** «Calcola» (`CalculateCommand`) conferma le bozze (D7) e calcola subito sul thread dell'interfaccia; nessun ricalcolo
  automatico. È ammesso anche in sola lettura, come oggi.
- **FN3, T6.** Aprire o chiudere un gruppo e «Raccogli input» scrivono `vista_fondazioni` e sollevano `Modified` anche in sola
  lettura, e conservano il risultato.
- **FN4.** La scelta «Sezione X / Sezione Y» della scheda «Rottura» è sempre bloccata in sola lettura.
- **FN5.** Le cinque scelte a indice restano `ShallowFoundationChoiceViewModel`; nella vista sono righe con l'etichetta di 215 px e
  la combo sul resto, come le righe storiche.
- **FN6, T8.** Interfaccia `IModuleReportPdf` in Presentation: la sorgente del report delle fondazioni dà il DOCX di
  `FoundationReport.WordDocument` e il PDF di `FoundationReport.Pdf` (MigraDoc). `MvvmSheetEditor.ExportReport` li scrive come
  `SheetEditor.ExportReport` (`Archivio.ScriviAtomico`, stesso nome con `.pdf`); gli altri moduli tengono il PDF ricavato dal DOCX
  (`ReportFiles.Write`). Report invariati.
- **FN8, T5.** «Invia / carica terreno…» apre la finestra storica `SoilTransferDialog` dietro `ISoilTransferDialog`
  (`WpfSoilTransferDialog`, come la stratigrafia): il foglio conferma e passa modulo, dati e fogli terreno del progetto; una copia va
  alla finestra come nuovo foglio, una sostituzione come terreno di questo foglio. Spento in sola lettura. Il ViewModel condiviso del
  trasferimento arriva con F5.6.
- **FN9.** Nessuna «?»: la Wiki non ha argomenti per i campi delle fondazioni, come nella vista storica. F1 su un campo, in una
  cella degli strati o fuori dai campi e «Guida» aprono la guida del modulo dal navigatore della Wiki dei servizi. Falda e
  calcestruzzo hanno il modulo esplicito nei loro form (gli `InputForm` storici lo ereditavano dalla scheda).
- **FN10.** Invariata: solo il testo dei risultati senza calcolo dipende dalla libreria approvata.
- **T2.** Sola lettura con `IsReadOnly`: campi, scelte, sondaggi, strati, trasferimento del terreno e sezione del meccanismo
  bloccati; gruppi, «Raccogli input», schede, report, guida e «Calcola» liberi. Sono gli stessi controlli della vista storica sotto
  `RevisionInspection` (provato).
- **D9–D12.** Titolo a 22 punti (`Font.Title`); numeri in `FieldForm` compatti; solo token (P7): intestazioni della tabella degli
  strati con `Token.CommandBar` e `Token.OnCommandBar`, campioni degli strati con `Swatch.Soil1…6`. Nella vista non ci sono schede
  `Ui.Paper`.
- **T7.** Nessun difetto storico corretto. L'espansore dei dettagli dei risultati si chiude a ogni calcolo, come quello che la
  vista storica ricreava.
- **Q3.** I disegni di `FoundationViews.cs` (geometria, stratigrafia, area efficace, pressioni, sezioni, meccanismo) sono ospitati
  invariati; il code-behind li alimenta con i dati semplici del ViewModel (`InputDrawing`, `Graphics`, `Mechanism`). Il campione di
  pressione sotto il puntatore resta nel disegno.

### 5.3 Esiti

| Criterio | Esito |
| --- | --- |
| Vista con la disposizione di oggi, solo token | sì; `Architecture.Tests` PASS, P6 e P7 attive anche su `Views/ShallowFoundation`, nessuna voce nuova nelle baseline (F4 3347, P3 invariata) |
| Tracce della vista storica attraverso i controlli della vista XAML | 15 scenari, 8198 righe, uguali riga per riga alle tracce congelate (`ui/check-fondazioni-view`) |
| Cattura F3 con `--module-impl geo_fondazioni_superficiali=mvvm` | PASS 104 fixture contro `expected` e `transitions-f4.json` invariato; le 4 transizioni F4 delle fondazioni (versione della libreria, F4-35) come oggi |
| Confronto diretto storica/MVVM (`tools/qa/f5_confronto_diretto.py`) | **identiche**: 1066 file, 1063 uguali byte per byte, 3 JSON uguali in forma canonica (tempi volatili del palo elastico); 0 differenze, quindi anche sulle 11 fixture delle fondazioni |
| `--check-f3-report-chapters` con il modulo MVVM | PASS 105 capitoli autonomi, 246 immagini |
| Visita senza scritture | per le 11 fixture: schede, espansori senza stato e tre aspetti senza scritture né `Modified`; la visita F3 (gruppi aperti) scrive `vista_fondazioni` come la vista storica |
| Disegni | per le 11 fixture, dopo «Calcola»: stessi tipi e stessi elenchi di disegno della vista storica a 640×420 e 900×600 (136 confronti) |
| Esportazioni | per le 8 fixture con risultato: JSON uguale byte per byte, Word uguale in ogni parte, PDF uguale alla storica e uguale a `FoundationReport.Pdf` (date, identificativi e sigle dei font normalizzati); per le 3 senza risultato, stesso rifiuto |
| Schermate | 18 PNG (vista nuova e storica, tre fixture, tre aspetti), a 144 DPI |
| F1 e «?» | nessuna «?» in entrambe le viste; F1 e «Guida» aprono la guida del modulo, anche in sola lettura |
| Controlli usati da `FoundationWorkspace.UiChecks` | campo Width (nome di automazione «Larghezza B [m]»), pulsante `raccogli-input`, sei schede grafiche, tabella degli strati; «Raccogli input» allarga i grafici di oltre 200 px, conserva il risultato e salva lo stato nel foglio |
| Nomi di automazione | i 12 nomi della vista storica conservati su quattro fixture |
| Sola lettura | stessi controlli bloccati e liberi della vista storica; FN3 e FN4 provati |
| Profilo quick | PASS 52 (`supporto/artefatti/ci/20261017-100413-f59bv-c1-quick`), Presentation.Tests 1790 |

Le catture e il confronto diretto sono nella cartella non versionata dell'agente (scratchpad `f5-agenti/f5-9b-vista`: `f3-storica1`,
`f3-mvvm1`, `confronto-diretto1.json`, `capitoli-mvvm1`). La corsa finale è nella sezione 5.7.

**Nota sulla prova.** La vista XAML costruisce il contenuto di un espansore chiuso, degli input raccolti, di una scheda non mostrata
o di una riga nascosta solo quando lo mostra, mentre la vista storica costruisce tutti i controlli nel codice. Per leggere e guidare
le tracce come la prova storica, `ui/check-fondazioni-view` misura quel contenuto fuori dall'albero visuale e applica i modelli
(`Realize`); un controllo che l'utente non può usare continua a non fare nulla. Il prodotto non lo fa.

### 5.4 Differenze visibili fra le due viste

Sono le stesse in tutti gli aspetti (schermate di `ui/check-fondazioni-view`, cartella `vista/schermate` della corsa):
- **Titolo.** 22 punti (`Font.Title`) invece di 19 (D9).
- **Numeri.** `FieldForm` compatto (D10): etichetta a 12 punti su larghezza variabile invece di 215 px, casella di 82 px allineata a
  destra invece di una casella fino al bordo, colonna dell'unità vuota (le etichette contengono già l'unità). Lo stesso per il nome
  della combinazione, la densità relativa e il peso specifico dell'acqua. Falda e calcestruzzo erano già `InputForm`: invariati.
- **Tabella degli strati.** L'altezza segue le righe: manca la striscia vuota che la vista storica lasciava sotto l'ultima riga.
- Le scelte a indice, le schede, i disegni, i risultati e i colori sono quelli di oggi.

### 5.5 Domande e note

- **Contrasto (T10).** Il passo `mvvm-geo_fondazioni_superficiali` è aggiunto ma non eseguito: lo schermo è al 150%. Con
  `--baseline` il passo misura solo Scuro e Molto scuro; la Chiara della vista MVVM si misura senza `--baseline`. È un blocco a sé in
  fondo a `ContrastAudit.cs` (`ShallowFoundationContrastStep`, chiamato fra i commenti «F5.9b begin/end»), perché la chiusura del
  pilota toglie il meccanismo dei passi `mvvm-`; il coordinatore riconcilia il file all'unione. Da ripetere a 96 DPI prima
  dell'unione; al passaggio della Release il blocco si toglie e le voci della Chiara passano dal passo storico a quello del modulo
  (D12).
- **Suite storiche.** `FoundationWorkspace.UiChecks` (metodo `Ui` di `tests/FoundationWorkspace.Checks/Program.cs`, con la parte
  `--ui` di `ContractChecks` e `--candidate`) apre ancora per riflessione la vista storica. Al passaggio della Release diventa la vista
  XAML ospitata fuori schermo, con la libreria candidata iniettata nel servizio del contesto per `--candidate`; i suoi controlli della
  disposizione sono già provati sulla vista nuova in `ui/check-fondazioni-view`.
- Nessuna domanda nuova.

### 5.6 Da provare a mano in una build di prova

```
dotnet build X.Desktop/X.Desktop.csproj -c UiTests
X.Desktop\bin\UiTests\net8.0-windows\ANTHEA.exe --interactive --module-impl geo_fondazioni_superficiali=mvvm
```

1. Nuovo foglio delle fondazioni superficiali: disposizione, campi, schede e stratigrafia come oggi. «Calcola»: titolo del
   risultato, dettagli chiusi, sei schede grafiche.
2. Cambiare metodo, geometria (nastriforme, circolare), drenaggio e verifica: i campi compaiono e scompaiono come oggi; i risultati
   si azzerano («Dati modificati · premere Calcola») e nulla si ricalcola da solo.
3. Aprire e chiudere i gruppi, «Raccogli input» / «Mostra input»: lo stato resta dopo il salvataggio, il risultato resta.
4. Stratigrafia: «+ Sondaggio», «− Sondaggio» (con la domanda), scelta del sondaggio, modifica delle celle, γsat vuoto in grigio,
   «Aggiungi strato», [−], semispazio.
5. Calcestruzzo: scegliere una classe (fck si aggiorna); scrivere un fck (classe del catalogo o «Personalizzato»).
6. Schede grafiche: campione di pressione sotto il puntatore; sezione X/Y della rottura.
7. «Invia / carica terreno…»: invio a un palo e sostituzione del terreno da una stratigrafia del progetto.
8. «Report Word e PDF»: nome proposto, DOCX e PDF affiancati come oggi; esportazione dei risultati in JSON.
9. F1 su un campo e fuori dai campi, «Guida»: la guida del modulo.
10. Aspetto Scuro e Molto scuro dal menu, con il foglio aperto.
11. In un progetto, una revisione precedente: campi, scelte, strati, trasferimento e sezione del meccanismo bloccati; gruppi,
    «Raccogli input», «Calcola» e report disponibili.
12. Salvare, chiudere e riaprire l'archivio: stessi dati.

### 5.7 Corsa finale

Corsa `-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` su `4c1fdc38`:
`supporto/artefatti/ci/20261017-102232-f59bv-finale`, **PASS 120**, KNOWN 0, NEW-FAIL 0 (build 25, fast 31, wiki 2, ui 62: una suite in più della base
`20261017-060301-f5-int13`, `ui/check-fondazioni-view`). La corsa non ha modificato file tracciati (l'unico segnalato è
`ContrastAudit.cs`, cambiato durante la corsa per il commit `4fbc3397`); `transitions-f4.json`, le fixture F3 e le baseline di
architettura invariati.
- `ui/f3-equivalence` (vista storica) PASS 104 fixture; `ui/f3-report-chapters` PASS; `ui/check-fondazioni-historic` PASS;
  `ui/check-fondazioni-view` PASS 294 controlli; `FoundationWorkspace.Checks` e `FoundationWorkspace.UiChecks` PASS (suite storiche
  invariate); `ui/check-module-host-offscreen` PASS; Presentation.Tests 1790; Architecture.Tests PASS.
- Con gli stessi binari: cattura F3 con `--module-impl geo_fondazioni_superficiali=mvvm` PASS 104 fixture contro il riferimento e le
  transizioni F4; confronto diretto con la cattura storica della corsa **identiche** (1066 file, 0 differenze);
  `--check-f3-report-chapters` con il modulo MVVM PASS 105 capitoli autonomi, 246 immagini.
- Dopo il blocco del contrasto (`4fbc3397`, solo codice di prova UiTests): build UiTests e `ui/check-fondazioni-view` PASS
  (`supporto/artefatti/ci/20261017-110211-f59bv-c7-vista`).
- `ui/check-contrast` esclusa (144 DPI, T10). Le schermate sono a 144 DPI.

## 6. Passaggio della Release (23/10, agente f5-pass-geo)

Ramo `refactoring/f5-pass-geo`, da `refactoring/f5` `db7ef68d`, dopo materiali, pali verticali e stratigrafia. Decisione dell'utente del
23/10: «finisci il refactoring che avevamo previsto delle viste», «Moduli ora, shell dopo». Eseguiti i punti 5 e 6 della sezione 3.

### 6.1 Che cosa è fatto

| Commit | Contenuto |
| --- | --- |
| `f4d63bbe` | `ui/check-fondazioni-view` senza la vista storica; `FoundationWorkspace.UiChecks` sulla vista XAML ospitata da `MvvmSheetEditor` |
| commit del passaggio | Release MVVM per `geo_fondazioni_superficiali`, vista storica archiviata, suite, contrasto, F4 e documenti |

- **Release.** `ModuleImplementations.Declared`: `geo_fondazioni_superficiali` = `Mvvm`; `ui/check-module-host-offscreen` e
  `ui/check-fondazioni-view` (banco interattivo e Release) lo provano.
- **Archiviazione (Q11)** in `supporto/SUPERATI/ui-storica/fondazioni/`, con `README.md` e `registro.json`: `X.Desktop/Wpf/FoundationWorkspace.cs`,
  `X.Desktop/Wpf/FoundationWorkspace.Materials.cs`, `tests/Desktop/ShallowFoundationHistoricTrace.cs`. Restano `FoundationViews.cs`,
  `FoundationIcon`, `StratigraphyTable`, `SoilTransferDialog`.
- **Tolti:** i rami `foundation` di `SheetEditor` (campo, `Result`, costruttore, `Commit`, `CalculateAsync`, `ExportReport`,
  `BuildReportDocument`); `--check-fondazioni-historic[-capture]` e la suite; il blocco autonomo `ShallowFoundationContrastStep`; il modulo
  da `$MvvmBench` (resta solo il muro).
- **`ui/check-fondazioni-view`** (`f4d63bbe`): dati all'apertura uguali alla traccia congelata; dopo la visita della cattura F3 e dopo
  «Calcola» Modified, dati e risultato uguali ai valori registrati della vista storica (SHA-256); elenchi di disegno dei disegni tecnici
  uguali a quelli registrati (tipo, primitivi, SHA-256, alle due misure); messaggi senza risultato; JSON, Word di `FoundationReport.Word` e
  PDF di `FoundationReport.Pdf` (T8) come li scriveva la vista storica; «?», nomi e sola lettura dai valori registrati (corsa di cattura
  `20261023-124229-geo-cattura-3`); schermate della sola vista nuova.
- **`FoundationWorkspace.UiChecks`** (`f4d63bbe`, D13): la vista XAML con `MvvmSheetEditor`, `ShallowFoundationSheetModule` e i servizi
  della radice di composizione, con la libreria iniettata nel servizio di calcolo del contesto (`ModuleServices.ForFoundation`: installata o
  candidata di `--candidate`), in una finestra fuori schermo mai attivata; stessi controlli sugli stessi identificativi. Due differenze di
  prova: la vista si misura in una finestra (fuori da una finestra il layout della vista XAML non si assesta) e quindi la misura dell'area
  ammette l'arrotondamento del layout entro mezzo pixel fisico (1249,6 DIP a 144 DPI), come `ui/check-stratigrafia-view`; il dispatcher si
  fa girare con brevi operazioni a priorità `Background` (non c'è `Application.Run`).
- **Baseline F4** (solo in discesa): tolta la voce (1 occorrenza) di `FoundationWorkspace.Materials.cs` (`FoundationWorkspace.BuildSoils`),
  codice archiviato.
- **Contrasto.** Tolto il blocco autonomo. La baseline della Chiara ha 25 voci (31 occorrenze) sui passi `modulo-geo_fondazioni_superficiali…`
  misurati sulla vista storica: le chiavi della vista nuova si conoscono solo con una corsa a 96 DPI (D12). **Da fare alla prima corsa a
  96 DPI.**
- **Registro.** Nessuna voce nuova: F5-11 è già «attiva nella Release dopo il passaggio del modulo».

### 6.2 Prove

| Corsa | Esito |
| --- | --- |
| `ui/check-fondazioni-view` su `f4d63bbe` (`20261023-135247-geo-fo-vista`) | PASS 286 controlli |
| `FoundationWorkspace.UiChecks` su `f4d63bbe` (`20261023-142904-geo-fo-uichecks2`) | PASS 98 controlli |
| dopo il passaggio (`20261023-143234-geo-fo-pass`): `Architecture.Tests`, `FoundationWorkspace.UiChecks`, `ui/check-fondazioni-view`, `ui/check-stratigrafia-view`, `ui/check-module-host-offscreen`, `ui/f3-report-chapters`, `ui/f3-figures`, `ui/f3-scheduling`, `ui/smoke-sharing`, `ui/smoke-project-workspace` e `-mvvm`, `ui/shell-smoke-project-workspace` | PASS 16, NEW-FAIL 0 |

### 6.3 Che cosa resta

- `ui/check-contrast` a 96 DPI e spostamento delle voci della Chiara.
- `ui/f3-equivalence` sul ramo unito (coordinatore).
- `FoundationWorkspace.UiChecks --candidate` non è nel runner: va provata con la prossima libreria candidata.
- Guida pratica: nessun cambiamento della disposizione descritta («F1 o Guida apre questa pagina» resta vero).

### 6.4 Come riprendere

```
cd C:\Users\g.pacini\AppData\Local\Temp\aw-f5-pass-geo
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,ui -Only '^(FoundationWorkspace\.UiChecks|ui/check-fondazioni-view)$' -Tag <tag>"
```
