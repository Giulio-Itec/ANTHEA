# F5.9 — Muri di sostegno: preparazione

## Scopo

Questa nota chiude la **preparazione** di F5.9: modulo `geo_muri_sostegno`, vista storica `RetainingWallWorkspace`
(`X.Desktop/Wpf/RetainingWallWorkspace*.cs`, 12 file). I disegni (`RetainingWallDrawing*.cs`, `RetainingWallBarDrawing.cs`,
`GlobalStabilityDrawing.cs`, `RetainingWallIcon.cs`) restano WPF (Q3). I tipi senza WPF del modulo stanno in
`src/ANTHEA.Application/Walls` (`WallModel`, `WallExports`) e il report in `src/ANTHEA.Reports/Generators/ReportRetainingWall*.cs`.
La nota segue i passi 1–5 della «Ricetta per i moduli successivi» di [f5-4-pilota.md](f5-4-pilota.md). Contiene:
- che cosa esiste dopo la preparazione;
- i difetti della vista storica trovati con le tracce;
- che cosa resta per finire F5.9;
- le domande per il punto di controllo dell'utente.

La vista XAML, il passaggio in `ModuleImplementations` e l'archiviazione della vista storica vengono **dopo** il punto di
controllo: non fanno parte di questa preparazione. Anche la generazione delle combinazioni dei muri resta dov'è oggi: spostarla
nel Model non è un passo di F5.9.

Riferimenti:
- [proposta](f5-proposta-architettura.md), sezioni 3, 5, 7 e riga F5.9 della sezione 8;
- [inventario](f5-inventario-ui.md), sezioni 1 e 3;
- [adattatori dei muri](f4-adattatori-muri.md) e [taglio dei muri](f4-taglio-muri.md) (F4);
- `tests/fixtures/f3/README.md` e `tests/fixtures/f5-muri/README.md`.

Ramo `refactoring/f5-9-muri`, da `refactoring/f5` `6ea62a0f`, 17/10/2026. Il calcolo dei muri è lungo (equilibrio, verifiche
GPC, stabilità globale): resta **sullo sfondo**, con il ricalcolo automatico 450 ms dopo l'ultima modifica, come oggi e come gli
altri moduli (la scelta (b) di Q8; (a), il calcolo sincrono, vale solo per i calcoli brevi).

## 1. Che cosa esiste dopo la preparazione

| Commit | Contenuto |
| --- | --- |
| `2550d454` | 12 fixture F3 nuove dei muri (4 → 16; 81 nel riferimento), attese catturate con la vista storica |
| `6a7fc606` | tracce congelate della vista storica: 24 scenari, 17 666 righe, suite `ui/check-muri-historic` |
| `79307cf4` | derivazioni in Application (`RetainingWallPresentation`), campi condivisi in Presentation (`RetainingWallFields`), baseline F4 3347 → 3341, P3 127 → 126 |
| `c841c89e` | ViewModel `src/ANTHEA.Presentation/Modules/RetainingWall`, piano delle figure del report in Application, modulo registrato, baseline F4 3341 → 3335 |
| `42e56e9f` | prove senza WPF: tracce, F3, sfondo, sola lettura, `Reload`, esportazioni |
| `d80a3130` | notifiche di `CanDesign` e del risultato globale per la vista futura |

### 1.1 Fixture F3

Le 12 fixture nuove (tabella completa in `tests/fixtures/f3/README.md`) coprono:
- sisma dai parametri del sito con pendio (St > 1), sisma assegnato con Wood, sisma escluso;
- azioni di ogni tipo e natura, correlate, disattivate e non disegnate;
- mensola con due zone di armatura e facce diverse; gravità in calcestruzzo non armato e in muratura (verifiche fuori campo);
- passiva a valle con frazione mobilitata e altezza libera assegnata, attriti automatici e assegnati, falda;
- matrice personalizzata confermata e matrice da rigenerare dopo una modifica delle azioni;
- stabilità globale drenata a due colonne con falda propria, non drenata su profilo unico con il sisma;
- opzioni avanzate: cedimenti e spostamenti, portanza sismica, Newmark, dettagli delle armature, copriferro EC2;
- avvisi (equilibrio perso, acciaio del catalogo con valori diversi, copriferro insufficiente);
- calcoli fermati: stratigrafia incompleta, tipologia prevista.

Prima della cattura ogni input è stato provato con la vista storica in una radice temporanea, per verificare l'esito descritto.
Gli input vengono dal generatore `F3Fixtures.RetainingWallF59`; gli attesi (102 file) da due catture uguali byte per byte. Le due
fixture col sisma del sito salvano kh e kv già ricavati, come la scheda: con i valori del catalogo `ui/f3-report-chapters`
segnalava dati preparati diversi (sezione 2, punto 6). Gli attesi delle 69 fixture esistenti e `transitions-f4.json` non
cambiano (le 14 441 transizioni dei muri riguardano solo le 4 fixture vecchie); in `expected/esito.txt` cambia solo il numero
delle fixture, 69 → 81, registrato nel manifest (`fixture_aggiunte_f5_9`). `WallContractChecks` (Application.Tests) nomina le
tre fixture che si fermano per costruzione. `ui/f3-equivalence`, `ui/f3-report-chapters` (82 capitoli) e `ui/f3-scheduling`
passano.

### 1.2 Tracce della vista storica

Le tracce (`tests/fixtures/f5-muri/storica`, formato e scenari in `tests/fixtures/f5-muri/README.md`) registrano lo stato visto
dall'utente: per intero dopo l'apertura, poi dopo ogni passo solo le voci cambiate. Le voci sono i dati nell'ordine in memoria,
`Modified` e le richieste alla finestra (report, terreno, sezioni c.a. con l'impronta dei dati), risultato, stabilità globale e
stato, i pulsanti dell'intestazione, combinazioni, viste, filtri e interruttori, ogni campo di ogni form (etichetta, simbolo,
unità, testo grezzo, voci, spento, attenuato, nascosto, nota), le schede, le tabelle degli input, i testi degli input, i pannelli
dei risultati come blocchi, la sezione selezionata, i disegni come dati e la finestra «Valori di calcolo…».

Gli scenari sono 24: il percorso F3 per ognuna delle 16 fixture, `modifiche-ingressi`, `azioni-sisma-matrice`,
`risultati-sezione`, `opzioni-armature`, `stabilita-globale`, `valori-di-calcolo`, `sola-lettura`, `calcolo-sullo-sfondo`
(motore trattenuto da `HeldModuleService`: calcolo superato, «Ricalcola» durante un calcolo, calcolo globale che prende il posto
del calcolo, chiusura durante un calcolo). Ogni passo si fa con i controlli, come l'utente: un controllo nascosto, spento o
protetto dalla revisione non fa nulla; prima del passo si apre la pagina del controllo. La vista storica non è modificata: la
traccia usa membri interni in una classe parziale compilata solo in UiTests; la finestra dei valori, oggi modale, è mostrata
fuori schermo e non modale per guidarla.

La stessa definizione (`tests/Shared/RetainingWallTrace.cs`) guida due prove:
- la vista storica, in `tests/Desktop/RetainingWallHistoricTrace.cs`, stadio `ui`, finché la vista esiste;
- il ViewModel, in `tests/ANTHEA.Presentation.Tests/RetainingWallSheetChecks.cs`, stadio `fast`.

### 1.3 Derivazioni in Application e campi condivisi

`src/ANTHEA.Application/Presentation/RetainingWallPresentation{,.Global,.Inputs,.Figures}.cs` contiene il codice che la vista
calcolava da sé, spostato senza modifiche:
- testi dello stato e loro gravità, pulsanti «Sisma» e «Stabilità globale», riepilogo, sezione selezionata e suo nome;
- righe e colonne delle tabelle: verifiche (filtri e sezione di una riga), sollecitazioni, spinte, dettagli delle armature,
  portanza sismica, cedimenti, Newmark, proposta delle armature, valori usati nel calcolo, stabilità globale (casi, superficie
  critica, conci, note, combinazione mostrata e peggiore);
- regole dei campi (`InputRules` per UpdateFields, `GlobalRules` per RefreshGlobal), campi dell'azione per tipo, scelta dei
  materiali del catalogo;
- matrice delle combinazioni e matrice globale (righe di presentazione, modifica di una cella, genera, conferma, copia,
  elimina), guida e ricerca della stabilità globale, colonne profonde, nuovi strati, punti e accelerogrammi;
- il piano delle figure del report (`Figures`: tipo, membro, didascalia, dimensioni, categoria, rapporto d'aspetto, pagine della
  distinta) e la figura di «Word globale».

`WallSheetPresentation.TotalHeight` legge H + t una volta; `WallParameterTable.Validate` è la verifica sincrona della finestra
dei valori. `src/ANTHEA.Presentation/Modules/RetainingWall/RetainingWallFields.cs` è l'unica definizione di campi, gruppi, voci e
colonne, letta dalla vista storica e dal ViewModel.

La vista storica chiama queste funzioni e dà un'uscita identica: tracce, F3, capitoli autonomi e prove fuori schermo dei muri.
`RetainingWallPresentationChecks` (Application.Tests, 29 controlli) le prova con valori scritti a mano; `ExposureChoicesChecks` 7c
segue i campi spostati.

**Baseline F4 3347 → 3335**, solo spostamenti e rimozioni: 15 voci spostate dalla vista in Application con le derivazioni;
`H + t` compariva 8 volte e diventa la sola `WallSheetPresentation.TotalHeight` (−6); le 6 costanti delle figure (1100d e 460 in
`WallFigures`, 1120d e 790 in `BarFigures`, 4d e 8d in `RetainingWallBarDrawing`) diventano costanti con nome nel piano (−6). Le
voci rimaste nei file dei muri sono di disegno e di layout (`RetainingWallDrawing*.cs`, `GlobalStabilityDrawing.cs`, dimensioni
della figura di «Word globale» nella vista storica) e restano nella vista (Q3). **P3 127 → 126**: `RetainingWallWorkspace.GlobalSearchLimits` è ora una proprietà `IReadOnlyList` in
Application. Nessuna voce F4 o P3 in Presentation.

### 1.4 ViewModel

`RetainingWallSheetViewModel` (`src/ANTHEA.Presentation/Modules/RetainingWall`, quattro file parziali) riproduce
`RetainingWallWorkspace` sullo stesso documento, con le stesse scritture nello stesso ordine:

| Vista storica | ViewModel |
| --- | --- |
| costruttore: `WallModel.Upgrade`, tipologie, `BuildInputs`, `BuildMatrix` se il documento ha combinazioni, `UpdateFields`, `Preview`, ricalcolo dopo 450 ms | costruttore: stessa sequenza; `ScheduleCalculationAsync` con `RecalculationDelay` (450 ms) |
| combo della tipologia (previste spente), «Mensola/Gravità esempio» | `Families`, `Family` (rifiutata in sola lettura e per le tipologie previste), `LoadExample` |
| `InputForm` compatti con i simboli, `UpdateFields` con `ApplyRules` | `Forms` (`FormViewModel`), stesse regole (`InputRules`, `GlobalRules`); `IsFormShown` per le schede che contengono un form |
| `Expander` delle schede | `Cards` (`WallCardViewModel`: `IsVisible`, `IsExpanded`) |
| `JsonGrid` degli strati, dei cedimenti, delle azioni, dei punti, degli strati globali e delle matrici | `WallTableViewModel` (`ValleyLayers`, `MountainLayers`, `ServiceLayers`, `Actions`, `ProfilePoints`, `GlobalRearLayers`, `GlobalValleyLayers`, `Matrix`, `GlobalMatrix`): righe `JsonRowViewModel`, la selezione segue le righe come la griglia (togliere la riga scelta la azzera e lo notifica) |
| pulsanti «+», «−», «↑», «↓» delle tabelle, accelerogrammi, importazione CSV | `AddSoilLayer`, `RemoveSoilLayer`, `MoveSoilLayer`, `AddServiceLayer`, `RemoveServiceLayer`, `NewActionType` + `AddAction`, `RemoveAction`, `AddGlobalLayer`, `RemoveGlobalLayer`, `AddProfilePoint`, `RemoveProfilePoint`, `AddHistory`, `RemoveHistory`, `ImportHistory` (`IFileDialogService`) |
| `Changed` | `UserChanged`: stessa sequenza, poi `Changed` della base (revisione, risultati azzerati, ricalcolo automatico, `Modified`); «Disegna» di un'azione → `ChangedKeepingResults` |
| `CalculateScheduledAsync` (uno scheduler, `Busy`) | `SheetCalculation` della base, `SheetCalculationMode.Background` sul servizio del contesto; `WallEngineWatch`: «occupato» finché il motore torna anche per una richiesta superata, un motore alla volta; `OnCalculationPublished` → `ShowResults` (`ApplyResult` scrive le combinazioni automatiche senza `Modified`, come oggi) |
| ricerca delle armature e calcolo globale sullo stesso scheduler | `DesignRebarAsync`, `CalculateGlobalAsync` su un secondo `RecalculationScheduler`: `CancelCalculation` annulla il calcolo non pubblicato, il lavoro parte quando il motore torna, una modifica o «Ricalcola» lo annulla; `InterruptSearch`, `ApplyProposal` |
| combo combinazione, vista, elemento, filtro, combinazione globale e loro eventi a catena | `SelectedCombination`, `SelectedView`, `SelectedMember` (`IsMemberShown`), `SelectedFilter`, `SelectedGlobalCombination` (`IsGlobalView`): stessi aggiornamenti nello stesso ordine; un elenco che non contiene più la scelta la azzera e rinfresca |
| pannelli delle spinte, «Riepilogo e dettagli», proposta (testi, `DataGrid`, `Expander`, pulsanti) | `PressurePanel`, `ChecksPanel`, `ProposalPanel` come `WallPanelBlock` (testo con stile, tabella con colonne, righe, altezza e azione della riga, gruppo, pulsante con comando); `SelectResultRow`, `SelectPressure`, `SelectSlice` |
| `SelectSection`, «Apri sezione in c.a.» (`SectionRequested` → `MainWindow.OpenWallConcreteSection`) | `SectionSelection`, `CanSendSection`, `SendSelectedSection` → `ISheetHost.OpenModuleCopy("str_palo", …)`, lo stesso comando dell'ospite |
| `RetainingWallDrawing` (input, diagrammi), `GlobalStabilityDrawing` (risultati, anteprima) | dati semplici: `Drawing`, `DrawingCase`, `Diagrams`, `DiagramCase`, `GlobalDrawing`, `GlobalPreview`, `GlobalCase`, `Calculation`, evento `DrawingsChanged`; interruttori `ShowLoads`, `ShowLabels`, `ShowRebar`, `ShowPressures`, `GlobalFullProfile`; i disegni restano WPF (Q3) |
| «Sisma», «Stabilità globale · imposta», «Modifica profilo, strati e ricerca» | `ShowSeismic`, `ShowGlobalSetup`: pagina e schede aperte; lo scorrimento fino alla scheda resta alla vista |
| «Prepara dal muro», matrice globale, `InvalidateGlobalConfirmation` (la casella deselezionata è una modifica del form) | `PrepareGlobal`, `GenerateGlobalMatrix`, `ConfirmGlobalMatrix`, stessa conferma |
| `CalculationParametersWindow` (dialogo modale con uno scheduler proprio) | `OpenParametersAsync` → `RetainingWallParametersViewModel` (`Rows`, `Combinations`, `SelectedCombination`, `Derived`, `Message`, `IsEnabled`, `ApplyAsync`, `Close`) con una propria `SheetCalculation<JsonObject>` e `WallParameterTable.Validate` |
| «CSV», «Word globale», «Distinta ferri…» | `ExportCsv`, `ExportGlobalWord` (`IRetainingWallFigureRenderer.RenderGlobal`, `ReportFiles.Write`), `CreateBarSchedule` (dati; la finestra della distinta e le sue esportazioni restano alla vista) |
| «Relazione Word», `BuildReportDocument` | `RequestReport` (`ISheetHost`), `Report`: `WallExports.CreateDocument` con le figure di `IRetainingWallFigureRenderer` (piano condiviso), stessi byte DOCX |
| «Invia / carica terreno…» (`SheetEditor.OpenSoilTransfer`) | evento `SoilTransferRequested` (MU9) |
| `RevisionInspection.Protect` | `IsReadOnly`/`CanEdit`: form in sola lettura, comandi di modifica rifiutati; combinazioni, viste, filtri, schede, interruttori, selezioni, calcolo, calcolo globale, report e «Valori di calcolo…» ammessi |
| `Dispose` | `Dispose`: calcoli e ricerche annullati, finestre dei valori chiuse |

Aggiunte minime alla base comune, provate in `SheetCalculationChecks`: `IsBusy` virtuale, `PublishedCalculation`,
`CancelCalculation` e `SheetCalculation.Cancel` (annulla la richiesta non pubblicata e conserva il risultato pubblicato).

`RetainingWallSheetModule` non dichiara chiavi di stato (viste, schede e interruttori non sono salvati, come oggi) e nessun
report short; riceve il disegnatore delle figure. È registrato in `DesktopComposition.CreateModules` con
`RetainingWallFigureRenderer` (X.Desktop, che usa `ModuleFigureRenderer.WallFigures`); `ModuleHostWpfChecks` lo conta. La Release
resta storica: `ModuleImplementations` non cambia.

### 1.5 Prove

- `Presentation.Tests` passa da 1068 a 1264 controlli: 3 di `SheetCalculation.Cancel`, 193 di `RetainingWallSheetChecks`:
  - 24 tracce uguali riga per riga a quelle della vista storica;
  - 16 fixture uguali al riferimento F3: aperto, visitato, calcolato, risultato, testo e struttura del report, esportato, stato,
    riaperto. Sono uguali byte per byte, salvo i 24 file delle 4 fixture vecchie con le 14 441 transizioni F4, confrontati come in
    `ui/f3-equivalence` con quelle transizioni ammesse (nessuna differenza non ammessa);
  - modulo, registro e chiavi di stato: viste, filtri, elementi, schede, interruttori e selezioni non scrivono i dati e
    conservano il risultato;
  - calcolo sullo sfondo con barriere: fuori dal thread dell'interfaccia, occupato finché il motore torna, solo la revisione
    corrente pubblicata, chiusura durante un calcolo;
  - sola lettura dal contesto (anche la finestra dei valori), `Reload` uguale a una riapertura, CSV delle verifiche e dei conci,
    distinta ferri, importazione di un accelerogramma, esportazioni rifiutate senza risultato, notifiche per la vista.
- `Application.Tests`: `RetainingWallPresentationChecks`, `WallContractChecks`.
- Stadio `ui`: `ui/check-muri-historic`, `ui/f3-equivalence`, `ui/f3-report-chapters`, `ui/f3-scheduling` (con
  `F3WallParameterChecks`), `ui/f3-figures`, `ui/check-module-host-offscreen`, `ui/check-global-guidance-offscreen`,
  `ui/check-wall-advanced-offscreen`, `ui/check-wall-materials-offscreen`, `ui/smoke-retaining-wall`, `ui/smoke-global-stability`.
- Architettura: P1–P7 rispettate, F4 3335, P3 126, nessuna dipendenza dal calcolo nei metadati di Presentation.

**Corsa finale** `-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` su `42e56e9f`, con questa nota non ancora
registrata: `supporto/artefatti/ci/20261017-032617-f59m-finale`, PASS 115, NEW-FAIL 0 (build 25, fast 27, wiki 2, ui 61), nessun
file tracciato modificato, `transitions-f4.json` invariato.
- `ui/f3-equivalence` PASS con 81 fixture; `ui/f3-report-chapters` 82 capitoli; `ui/check-muri-historic` 24 scenari e 17 666
  righe; `ui/check-module-host-offscreen` 43 controlli; Presentation.Tests 1263; Application.Tests 2357; Architecture.Tests 515.
- Dopo la corsa, `d80a3130` (solo notifiche del ViewModel, +1 controllo): profilo quick PASS 52, NEW-FAIL 0
  (`supporto/artefatti/ci/20261017-035158-f59m-c6-quick`, Presentation.Tests 1264).
- `ui/check-contrast` è esclusa: lo schermo della macchina è al 150% (144 DPI) e la baseline è a 96 DPI (MU13).

## 2. Difetti della vista storica trovati

1. **Finestra dei valori in sola lettura.** In una revisione in sola lettura «Valori di calcolo…» è ammesso (giusto: è
   consultazione), ma la finestra è fuori dall'albero protetto da `RevisionInspection`: la tabella resta modificabile e «Applica
   e ricalcola» riscrive i dati della revisione e la segna modificata. Verificato con uno scenario temporaneo non versionato
   (`Modified` 1). Il ViewModel rende la finestra in sola lettura e rifiuta «Applica e ricalcola»: differenza dichiarata, MU3.
2. **«Stabilità globale» in sola lettura con il profilo vuoto.** Il pulsante dell'intestazione è ammesso in sola lettura
   (consultazione), ma se il profilo globale è vuoto chiama «Prepara dal muro»: scrive il profilo nei dati della revisione, la
   segna modificata (`Modified` 1), azzera i risultati e ricalcola. Peggio: ricostruisce gli input, e i controlli nuovi sono
   protetti da `RevisionInspection` solo quando entrano nell'albero visuale; dopo il clic 111 campi della pagina «Input»
   tornano modificabili (attriti, terreno di posa, falda, …) e «Calcola armature» si riaccende. Verificato con uno scenario
   temporaneo non versionato sulla fixture `default` (traccia in
   `supporto/artefatti/refactoring/f5-9/prova-globale-sola-lettura`). Il ViewModel apre la scheda senza preparare il profilo:
   differenza dichiarata, MU4.
3. **«Calcola armature» spento dopo una modifica durante un calcolo.** `UpdateFields` spegne il pulsante se il motore lavora e
   nulla lo riaccende quando il motore torna: resta spento fino alla modifica successiva. Il ViewModel lo conserva (MU6).
4. **Le ricerche annullano il ricalcolo in attesa.** La ricerca delle armature, il calcolo globale e «Interrompi ricerca» usano lo
   scheduler del calcolo: se partono entro 450 ms da una modifica, le verifiche locali non vengono ricalcolate fino alla modifica
   successiva o a «Ricalcola» (resta «Aggiornamento in attesa…» o lo stato della ricerca). Il ViewModel lo conserva (MU5).
5. **Falda proposta da «Prepara dal muro».** Con la falda attiva `WallModel.PrepareGlobalProfile` propone una linea che passa
   sotto il muro sopra il piano di posa: il calcolo globale risponde «Water above the ground: free water is not supported.».
   Osservato preparando le fixture; la fixture globale usa una falda propria. Non corretto (calcolo, fuori da F5.9).
6. **kh e kv del sito nel capitolo autonomo.** La scheda ricava kh e kv dai parametri del sito all'apertura e li scrive; il
   capitolo autonomo del report di progetto non lo fa, quindi per un documento non salvato dalla scheda i dati preparati
   differiscono (il calcolo li ricava comunque). Le fixture salvano i valori ricavati, come la scheda.

Nessun difetto nei risultati: tracce, F3 e capitoli autonomi coincidono.

## 3. Che cosa resta per finire F5.9

Passo 6 della ricetta, dopo il punto di controllo (l'ordine segue i passi 1–7 di F5.4):
1. **Vista XAML** `X.Desktop/Views/RetainingWall/RetainingWallSheetView.xaml` e il suo DataTemplate, solo token di F5.3 (P7):
   - disposizione di oggi: intestazione (tipologia, esempi, «Ricalcola», «Relazione Word», «CSV»), barra della combinazione
     («Sisma», «Stabilità globale · imposta», «Valori di calcolo…»), pagine «Input» e «Verifiche»; colonna delle schede (con
     scorrimento proprio, alta al massimo 605 px) accanto al disegno degli input, che sotto i 1450 px va sotto le schede;
     matrice delle combinazioni in fondo agli input;
   - schede come `Expander` legati a `Cards[...]`, form con `FieldForm`, tabelle con le colonne di `WallTableViewModel` (bordo
     colorato degli strati per indice e degli strati globali con `RetainingWallDrawing.GlobalLayerColor`, altezze di oggi);
   - pannelli dei risultati dai `WallPanelBlock` (un piccolo `DataTemplateSelector` per testo, tabella, gruppo e pulsante; la
     selezione della prima tabella va a `SelectResultRow`, quella dei conci a `SelectSlice`, quella delle spinte a
     `SelectPressure`);
   - disegni WPF invariati alimentati dai dati semplici nel code-behind (Q3); il testo sotto il puntatore resta nel disegno, il
     clic su un carico seleziona la riga di `Actions` e apre la scheda «Azioni»; `ShowSeismic` e `ShowGlobalSetup` aprono pagina e
     schede, lo scorrimento fino alla scheda è della vista;
   - sola lettura: legami a `CanEdit`, tranne combinazioni, viste, filtri, interruttori, schede, «Ricalcola», «Relazione Word»,
     «CSV», «Valori di calcolo…», «Calcola globale», «Word globale», «Distinta ferri…» e «Modifica profilo, strati e ricerca».
2. **Finestre**: «Valori di calcolo…» come dialogo modale su `RetainingWallParametersViewModel` (`IDialogService`); «Distinta
   ferri…» (pagine, zoom ed esportazioni PDF, Word, CSV e PNG sulla distinta di `CreateBarSchedule`, con `RetainingWallBarDrawing` e
   `DrawingPdf`); trasferimento del terreno (MU9).
3. **Banco**: cattura F3 con `--module-impl geo_muri_sostegno=mvvm` (anche `ui/f3-report-chapters`).
4. **Confronto diretto** fra cattura storica e MVVM a differenza zero (`tools/qa/f5_confronto_diretto.py`), poi il confronto
   normale; ogni differenza va in `transitions-f5.json` con l'ok dell'utente.
5. **Suite UI riscritte** prima del passaggio della Release, sulla vista XAML ospitata da `MvvmSheetEditor` fuori schermo (come
   `ui/check-pile-group-view`, D13):
   - `ui/smoke-retaining-wall` (`supporto/test/Desktop/RetainingWallSmokeChecks.cs`) e `ui/smoke-global-stability`
     (`GlobalStabilitySmokeChecks.cs`): oggi aprono `RetainingWallWorkspace` in `MainWindow` e ne leggono controlli e membri;
   - `ui/check-global-guidance-offscreen` (`GlobalGuidanceChecks.cs`), `ui/check-wall-advanced-offscreen`
     (`WallAdvancedChecks.cs`), `ui/check-wall-materials-offscreen` (`WallMaterialChecks.cs`): creano la vista storica e guidano
     form, griglie e pulsanti;
   - `F3WallParameterChecks`, `F3TypedSchedulingChecks` e `F3SchedulingChecks` (`tests/ANTHEA.Desktop.Equivalence`, suite
     `ui/f3-scheduling`): la finestra dei valori e lo scheduler della vista storica; diventano prove del ViewModel figlio (in
     parte già coperte dallo scenario `valori-di-calcolo`) o della vista nuova;
   - `FieldFormParityChecks` (`tests/Desktop`), `ContrastAudit` e `ReportAppearanceChecks` (`supporto/test/Desktop`), che
     nominano la vista storica;
   - `ui/check-muri-historic` resta finché la vista storica esiste.
6. **Punto di controllo**, poi passaggio della Release e archiviazione (Q11) nello stesso commit:
   - `ModuleImplementations.Declared`: `("geo_muri_sostegno", SheetImplementation.Mvvm)`;
   - i 12 file `RetainingWallWorkspace*.cs` in `supporto/SUPERATI/ui-storica/muri/`, con
     `tests/Desktop/RetainingWallHistoricTrace.cs`; i disegni restano;
   - rami `retainingWall` di `SheetEditor` (campo 34, `Result` 35, `Busy` 37, costruttore 108-112, `Dispose` 165, `Commit` 166,
     `CalculateAsync` 258, `BuildReportDocument` 375); `ISheetEditor.ConcreteSectionRequested` e
     `MainWindow.OpenWallConcreteSection` diventano superflui (`OpenModuleCopy` resta);
   - le tracce congelate restano: `Presentation.Tests` continua a usarle.
7. **Guida pratica e PDF**, se la disposizione descritta cambia.

## 4. Domande per il punto di controllo F5.9

- **MU1 · Calcolo sullo sfondo.** Ricalcolo automatico 450 ms dopo l'ultima modifica e «Ricalcola» immediato, sul servizio del
  contesto e con il limitatore dell'applicazione (`SheetCalculationMode.Background`), come oggi. Da confermare.
- **MU2 · Riferimento F3.** `expected/esito.txt` 69 → 81, registrato nel manifest (`fixture_aggiunte_f5_9`), come D2 di F5.4. Il
  coordinatore ricatena l'impronta con le fixture degli altri rami.
- **MU3 · Finestra dei valori in sola lettura.** Il ViewModel la rende in sola lettura e rifiuta «Applica e ricalcola» (sezione
  2, punto 1): differenza dichiarata rispetto alla vista storica, che riscrive la revisione. Da accettare.
- **MU4 · «Stabilità globale» in sola lettura.** Il ViewModel apre pagina e schede senza preparare il profilo vuoto (sezione 2, punto 2):
  differenza dichiarata rispetto alla vista storica, che scrive la revisione e la rende di nuovo modificabile. Da accettare.
- **MU5 · Ricerche e ricalcolo in attesa.** Conservato com'è (sezione 2, punto 4). L'alternativa è ricalcolare le verifiche locali
  quando la ricerca finisce: differenza visibile, da decidere dopo il passaggio.
- **MU6 · «Calcola armature» spento.** Conservato com'è (sezione 2, punto 3); l'alternativa è riaccenderlo quando il motore torna.
- **MU7 · Stato durante l'attesa di una ricerca.** Se un calcolo esce dal ritardo mentre una ricerca lavora, il ViewModel mostra
  subito «Calcolo di equilibrio e verifiche GPC…» e resta occupato; la vista storica lo mostra quando la ricerca finisce. È uno
  stato intermedio che le tracce non vedono (lo stato finale coincide). Da accettare, oppure aggiungere alla base l'attesa del
  lavoro secondario prima di `Running`.
- **MU8 · Eventi a catena delle combo.** Il ViewModel riproduce gli aggiornamenti a catena delle combo e della selezione delle
  griglie (per esempio l'elenco delle combinazioni globali azzerato che rinfresca il pannello globale). È ciò che rende le tracce
  identiche; dopo il passaggio si potranno semplificare con una nuova cattura. Da accettare.
- **MU9 · Trasferimento del terreno.** «Invia / carica terreno…» oggi apre `SoilTransferDialog` da `SheetEditor`; il ViewModel
  espone l'evento `SoilTransferRequested` (rifiutato in sola lettura, come il pulsante protetto). Stessa proposta di V9 (F5.6) e
  FN8 (F5.9b): un ViewModel del trasferimento con `IDialogService`.
- **MU10 · Distinta ferri e finestra dei valori.** Proposta: la finestra dei valori come dialogo modale su
  `RetainingWallParametersViewModel`; la distinta come dialogo della vista su `CreateBarSchedule` (pagine e zoom sono stato della
  vista, le esportazioni scrivono file con `IFileDialogService`), senza un ViewModel proprio. Da decidere.
- **MU11 · Baseline F4 dei disegni.** Resta a 3335: le voci rimaste dei muri sono nei disegni (Q3), come FN7 di F5.9b.
- **MU12 · Generazione delle combinazioni.** Resta nella scheda (`ApplyResult` dopo il calcolo, matrice in `RetainingWallPresentation`),
  come oggi; il passaggio nel Model è un lavoro separato, dopo F5.9.
- **MU13 · Contrasto a 96 DPI.** `ui/check-contrast` è stata esclusa dalle corse (schermo al 150%, 144 DPI): va ripetuta a 96 DPI
  prima dell'unione, come FN11 di F5.9b. Il ramo non tocca colori né la disposizione delle viste.

**Materiale da mostrare** (dopo i passi 1–4 della sezione 3): schermate nei tre aspetti, `--check-contrast`, confronto diretto a
zero, `ui/f3-equivalence` con il modulo MVVM, conteggio delle suite `ui`, questa nota con le risposte.

## 5. Vista XAML (17/10)

Ramo `refactoring/f5-9-vista`, da `refactoring/f5` `11f48446`: passi 1–4 della sezione 3, senza il passaggio della Release. Decisioni
applicate: quelle del 17/10 (mattina), cioè tutte le proposte della sezione 4 e di [f5-punti-di-controllo.md](f5-punti-di-controllo.md),
più le scelte prudenti del coordinatore in [decisioni.md](decisioni.md):
- MU1 e Q8 (b): il calcolo resta sullo sfondo, 450 ms dopo l'ultima modifica, sul servizio del contesto e con il limitatore;
  `CancelCalculation(restorePublished: true)` riporta i muri al risultato pubblicato quando una ricerca prende il posto del
  calcolo;
- MU3 e MU4: differenze dichiarate, come proposto;
- MU9 e T5: «Invia / carica terreno…» con il dialogo condiviso `SoilTransferDialogService` di `Modules/Shared`;
- MU10: «Valori di calcolo…» è un dialogo modale su `RetainingWallParametersViewModel` (`IDialogService`); la distinta ferri è
  un dialogo della vista;
- T2: sola lettura con `FormViewModel.IsReadOnly`.

La Release resta **storica**: la vista nuova si sceglie solo nel banco UiTests (`--module-impl geo_muri_sostegno=mvvm`) e con
`ANTHEA.exe --interactive` più lo stesso argomento, finché l'utente non prova una versione portatile.

### 5.1 Che cosa è stato fatto

| Commit | Contenuto |
| --- | --- |
| `afad06d4` | ViewModel allineato alle decisioni: dialogo dei valori, dialogo del terreno, comandi della vista |
| `e4e83b16` | vista XAML `X.Desktop/Views/RetainingWall` e il suo DataTemplate |
| `61470a55` | `ui/check-muri-view`, `ui/f3-scheduling-muri-mvvm`, passo di contrasto autonomo, correzioni trovate dalle prove |
| (questo commit) | questa sezione |

**ViewModel** (`src/ANTHEA.Presentation/Modules/RetainingWall`).
- MU10: `RetainingWallParametersViewModel` deriva da `DialogViewModel<bool>`. Ha `ApplyCommand` (spento in sola lettura, MU3) e
  `CloseCommand`; il risultato è true quando i valori sono applicati. `ShowParametersAsync`, il pulsante, calcola, mostra il dialogo
  con `IDialogService` e, quando il dialogo torna, chiude la finestra. Se la finestra è chiusa durante il controllo, il controllo è
  annullato e niente è applicato. `OpenParametersAsync` resta per le tracce.
- MU9: `TransferSoilAsync`, come il palo verticale: conferma i form, apre il dialogo condiviso con modulo, dati e fogli terreno del
  progetto, manda la copia alla finestra (`OpenModuleCopy`) o sostituisce il terreno (`ReplaceSoil`); rifiutato in sola lettura.
  L'evento `SoilTransferRequested` non c'è più.
- Comandi dei pulsanti (`RetainingWallSheetViewModel.Commands.cs`), tutti con la guardia del foglio (Q19). Quelli che modificano i
  dati sono spenti in sola lettura, come i pulsanti protetti da `RevisionInspection`. Quelli che la vista storica ammette in una
  revisione restano attivi: calcolo, report, CSV, valori, «Sisma», «Stabilità globale», calcolo globale e il suo Word, «Modifica
  profilo, strati e ricerca».
- Per la vista: `FormVisibility` (i form nascosti dalle regole, una copia nuova a ogni cambiamento), `CardShown` (lo scorrimento
  a «Sisma» e «Stabilità globale» è della vista), gli oggetti dei disegni (`DrawingCalculation`, `DiagramCalculation`,
  `GlobalDrawingResult/Case`, `GlobalPreviewResult/Case`), `OpenBarSchedule` e `SaveBarSchedule` per il dialogo della distinta,
  `NewActionType` validato e riportato al primo tipo a ogni ricostruzione degli input, come la combo storica.
- T2 e lezione 1 del 17/10: tutti i form, anche quelli costruiti dopo l'apertura (azione scelta, strato profondo, accelerogramma,
  profilo preparato), hanno `IsReadOnly` del foglio e `WikiNavigator` dei servizi. Lo provano `ViewContractAsync` di
  `Presentation.Tests` e l'aiuto di `ui/check-muri-view` (157 pressioni su 157 al navigatore).

**Vista** (`X.Desktop/Views/RetainingWall`).
- `RetainingWallSheetView`: la disposizione di `RetainingWallWorkspace` (sezione 3.1). Intestazione, barra della combinazione,
  pagine «Input» e «Verifiche», riga di stato. La colonna delle schede (scorrimento proprio, al massimo 605 px) sta accanto al
  disegno degli input, che sotto i 1450 px va sotto le schede; la matrice delle combinazioni è in fondo.
- Le schede sono `Expander` legati a `Cards[...]`. I form sono `FieldForm` compatti scelti per chiave fra `Forms` e nascosti da
  `FormVisibility`. Le tabelle sono `WallInputGrid` sulle `WallTableViewModel`, con le colonne di `JsonGrid`, le altezze e le
  larghezze storiche e il bordo colorato degli strati (per indice; per nome negli strati globali, `GlobalLayerColor`), con i token
  `Swatch.Wall1–6`.
- I pannelli dei risultati sono liste di `WallPanelBlock` mostrate da `WallBlockTemplateSelector` (testo, tabella `WallBlockGrid`,
  pulsante, gruppo). La selezione della prima tabella di «Riepilogo e dettagli» va a `SelectResultRow`, quella dei conci a
  `SelectSlice`, quella delle spinte degli input a `SelectPressure`.
- Il code-behind fa solo ciò che Q3 ammette e ciò che è stato della vista: i disegni storici (`RetainingWallDrawing`,
  `GlobalStabilityDrawing`) sui dati semplici del ViewModel a ogni `DrawingsChanged`; il testo sotto il puntatore; il clic su un
  carico (riga di `Actions` e scheda «Azioni»); la disposizione sotto i 1450 px; lo scorrimento alle schede; i gruppi interni
  riportati allo stato iniziale e la colonna delle schede in cima quando il foglio ricostruisce gli input, come la vista storica
  che li ricostruisce; la presentazione dei numeri attiva sulla vista, come `RetainingWallWorkspace`.
- Sola lettura (T2): form in sola lettura, tabelle bloccate, comandi di modifica spenti; combinazioni, viste, filtri,
  interruttori, schede, «Distinta ferri…» e i comandi ammessi restano utilizzabili.
- Dialoghi (MU10): `RetainingWallParametersView` è il DataTemplate del dialogo dei valori per `WpfDialogService` (1150×780 con i
  bordi; tutto spento durante il controllo, come la finestra storica; senza la presentazione dei numeri, come la finestra storica;
  righe e «Applica e ricalcola» bloccati in sola lettura, MU3). `RetainingWallBarScheduleView` è la distinta ferri come dialogo
  della vista (pagina, zoom, «PDF completo», «Word completo», «CSV distinta», «PNG pagina»): i byte si costruiscono come nella
  finestra storica, il foglio li scrive nel file scelto con il dialogo dei file dei servizi. Il dialogo del terreno è quello
  condiviso di `Views/Shared` (T5).
- Solo token (P7). I corpi seguono D8 e le scelte del palo verticale: titolo `Font.Title`, 15 → `Font.Subheading`, 18 →
  `Font.Heading`, 10 → `Font.Caption`.

**Prove.**
- `ui/check-muri-view` (`tests/Desktop/RetainingWallViewChecks.cs`, comando `--check-muri-view`):
  - le 24 tracce guidate attraverso i controlli della vista e rilette dai controlli. Il contenuto che WPF non ha ancora disposto
    (gruppi chiusi, pagina non mostrata) è costruito fuori dall'albero visuale quando un passo lo usa; i campi e i blocchi non
    disposti si leggono dal ViewModel, come nel palo verticale. Il valore di un campo di testo è il suo testo grezzo: l'editor
    lo mostra formattato senza fuoco;
  - per le 16 fixture: visita, dati, risultato ed esportazioni contro la vista storica;
  - schermate, aiuto, nomi di automazione, sola lettura contro la storica, dialogo dei valori, dialogo del terreno, banco.
- `ui/f3-scheduling-muri-mvvm`: le prove dei muri di `ui/f3-scheduling` (`F3WallMvvmSchedulingChecks`) sul foglio MVVM e sulla
  vista del dialogo dei valori, quando il banco sceglie la vista MVVM.
- Passo di contrasto autonomo `mvvm-geo_muri_sostegno` in fondo a `ContrastAudit.cs` (`RetainingWallContrastStep`, fra i commenti
  «---- F5.9 begin/end»): con `--baseline` solo Scuro e Molto scuro. Non eseguito (T10).
- `Presentation.Tests`: `ViewContractAsync` (19 controlli). Le tracce contano i dialoghi del terreno mostrati al posto dell'evento;
  le tracce congelate non cambiano.

### 5.2 Risultati

| Prova | Esito |
| --- | --- |
| `ui/check-muri-view` | PASS 187 controlli: 24 tracce, 17 666 righe uguali a quelle congelate; 16 fixture con visita senza scritture e dati e risultato uguali alla vista storica; report Word e PDF, CSV delle verifiche uguali (13 fixture con un risultato; nelle altre 3 il CSV è rifiutato con il messaggio storico); CSV dei conci e «Word globale» (2); distinta ferri PDF, Word, CSV e PNG (10); 11 schermate; 16 etichette «?» uguali, 157 pressioni di «?» e F1 su 133 campi di 21 form al navigatore; nomi di automazione su 4 fixture; sola lettura uguale alla storica (152 e 157 controlli comuni); dialogo dei valori; dialogo del terreno |
| Cattura F3 con `--module-impl geo_muri_sostegno=mvvm` | PASS 104 fixture, uguale al riferimento con le transizioni F4 di sempre (22 126 elencate e ammesse, 14 441 dei muri, come la cattura storica) |
| Confronto diretto (`tools/qa/f5_confronto_diretto.py`) fra cattura storica e MVVM | **0 differenze** su 1066 file (1063 uguali byte per byte, 3 JSON uguali in forma canonica; ignorati solo i 6 tempi del palo elastico) |
| `ui/f3-report-chapters` con MVVM | PASS 105 capitoli autonomi, 246 immagini e tutte le parti XML confrontate con la vista |
| `ui/f3-scheduling` con MVVM (`ui/f3-scheduling-muri-mvvm`) | PASS 47 controlli, come la storica |
| `Presentation.Tests`, `Architecture.Tests` | PASS 1818; PASS 523 (P1–P7, F4 3335, P3 126 invariate) |
| Profilo quick | `20261017-202443-f59v-c1` (ViewModel) e `20261017-213346-f59v-c3` (vista e prove): PASS 52, NEW-FAIL 0 |
| Corsa finale | `-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` su `17d75297`, `20261017-214434-f59v-finale`: PASS 125, NEW-FAIL 0; nessun file tracciato modificato, `transitions-f4.json` invariato; `ui/check-muri-view` PASS 187, `ui/f3-scheduling-muri-mvvm` PASS 47, `ui/check-muri-historic` e `ui/f3-equivalence` PASS; `Presentation.Tests` 1818, `Application.Tests` 2657, `Architecture.Tests` 523 |

Le schermate sono a 144 DPI (schermo al 150%), in `vista/schermate` della suite: «Input», «Verifiche» e la stabilità globale nei
tre aspetti; la disposizione stretta (sotto i 1450 px); la scheda della stabilità globale aperta dal pulsante dell'intestazione.
`ui/check-contrast` non è stata eseguita (T10: va ripetuta a 96 DPI, MU13).

Baseline architetturali: nessuna cambiata. Le altezze delle colonne del terreno (`UpdateColumnDepths`) sono costanti con nome in
`WallInputGrid`: nessuna voce F4 nuova.

### 5.3 Differenze visibili

- **MU3 e MU4** (differenze dichiarate): in una revisione precedente la finestra dei valori è in sola lettura e «Stabilità
  globale» apre la scheda senza preparare il profilo.
- **Righe alterne delle tabelle degli input.** La vista storica usa #F3F6FA, che non segue l'aspetto: in Scuro e Molto scuro le
  righe alterne restano chiare. La vista nuova usa `Token.Surface` (#F3F5F8 in Chiara), che segue l'aspetto.
- **Colori degli avvisi.** «DarkGoldenrod» (stato con avvisi, prontezza della stabilità globale) diventa `Token.Status.Caution`
  (#9A6700). Firebrick e Navy hanno già i loro token (`Status.Fail`, `Ink`).
- **Corpi**: titolo da 19 a 22 (D8), 15 → 14, 18 → 16, 10 → 11.
- **Dialogo dei valori a dimensione fissa**: `WpfDialogService` adatta la finestra al contenuto. Il dialogo ha la dimensione
  della finestra storica (1150×780) invece di una finestra ridimensionabile con minimo 780×530 (come i dialoghi del palo, F5.6).

### 5.4 Scelte da confermare («da decidere»)

- **MU14 · Comandi che avviano un lavoro sullo sfondo.** «Ricalcola», «Calcola globale», «Calcola armature» e «Valori di
  calcolo…» sono comandi che avviano il lavoro e tornano, non `AsyncRelayCommand`: i pulsanti restano attivi durante il lavoro,
  come nella vista storica, e un secondo clic non è rifiutato (gli scheduler del foglio decidono che cosa si pubblica). La
  convenzione di `GuardedCommands` li spegnerebbe durante il lavoro. Scelta: conservare il comportamento storico.
- **MU15 · Righe alterne** con `Token.Surface` (sezione 5.3). Alternativa: `Token.PaperAlt`, il token delle righe alterne
  (#F8FAFC).
- **MU16 · «DarkGoldenrod» → `Token.Status.Caution`.** Alternativa: un token nuovo con il colore storico.
- **MU17 · Dimensione fissa del dialogo dei valori** (sezione 5.3), come le finestre del palo (F5.6).
- **MU18 · Gruppi interni riportati allo stato iniziale** quando il foglio ricostruisce gli input («Mensola/Gravità esempio»,
  «Prepara dal muro», «Applica proposta», valori applicati). È il comportamento storico, tenuto nel code-behind. Alternativa:
  lasciarli come li ha messi l'utente.
- **MU19 · Lettura delle tracce attraverso la vista.** I campi non ancora disposti e i blocchi dei pannelli non generati si
  leggono dal ViewModel; il valore di un campo di testo è il testo grezzo del `FieldViewModel`. È la scelta del palo verticale.

### 5.5 Suite storiche da riscrivere al passaggio della Release

Usano `RetainingWallWorkspace` o i rami `retainingWall` di `SheetEditor`; vanno riscritte sulla vista XAML ospitata da
`MvvmSheetEditor` prima del passaggio (punto 5 della sezione 3):
- `ui/smoke-retaining-wall` (`supporto/test/Desktop/RetainingWallSmokeChecks.cs`) e `ui/smoke-global-stability`
  (`GlobalStabilitySmokeChecks.cs`). **Fatto** (f5-suite-ui, `a1bbb117`): `openSheet.Wall` (`WallSmoke`, vista storica o XAML), duplicati
  `ui/smoke-retaining-wall-mvvm` e `ui/smoke-global-stability-mvvm` ([f5-suite-ui.md](f5-suite-ui.md), sezione 2.3). Al passaggio si
  tolgono `HistoricWallSmoke` e i duplicati;
- `ui/check-global-guidance-offscreen` (`GlobalGuidanceChecks.cs`), `ui/check-wall-advanced-offscreen` (`WallAdvancedChecks.cs`),
  `ui/check-wall-materials-offscreen` (`WallMaterialChecks.cs`);
- `ui/f3-scheduling`: `F3WallParameterChecks.cs` e `F3TypedSchedulingChecks.cs` (`RetainingWallWorkspace.CheckF3Scheduling`)
  diventano superflui. Le stesse prove sulla vista MVVM ci sono già (`F3WallMvvmSchedulingChecks`, `ui/f3-scheduling-muri-mvvm`):
  al passaggio basta togliere il ramo storico di `F3SchedulingChecks`;
- `FieldFormParityChecks` (`tests/Desktop`), `ReportAppearanceChecks` (`supporto/test/Desktop`, `editor.retainingWall`) e
  `ContrastAudit` (`supporto/test/Desktop`): il passo `mvvm-geo_muri_sostegno` si toglie e il passo del modulo mostra la vista
  nuova;
- `Architecture.Tests` (`tests/ANTHEA.Architecture.Tests/Program.cs`, righe 123–124): due controlli leggono
  `RetainingWallWorkspace.Advanced.cs` e `.Results.cs`, da spostare sui file nuovi o togliere con l'archiviazione;
- `ui/check-muri-historic` resta finché la vista storica esiste; `ui/check-muri-view` resta, con le tracce congelate.

Per l'archiviazione (non è una suite): i disegni che restano usano ancora due funzioni statiche della vista storica,
`RetainingWallWorkspace.F` (in `RetainingWallDrawing.cs`) e `RetainingWallWorkspace.RebarLabel` (in
`RetainingWallDrawing.Inspection.cs`). Sono già alias di `WallSheetPresentation.Number` e `WallModel.RebarLabel`: con
l'archiviazione i disegni chiameranno direttamente queste.

### 5.6 Da provare a mano (versione portatile con le viste attive)

1. Aprire muri reali di un progetto, a mensola e a gravità: schede, disegno degli input, matrice, «Verifiche» come oggi;
   ridimensionare la finestra sotto e sopra i 1450 px.
2. Modificare geometria, terreni (anche colonne collegate, «↑», «↓», «−»), materiali, armature e azioni (anche il clic su un
   carico nel disegno): il ricalcolo parte dopo mezzo secondo, lo stato passa da «in attesa» a «in corso» a «aggiornato».
3. «Sisma» e «Stabilità globale · imposta»: pagina e scheda aperte e portate in vista; «Prepara dal muro», strati profondi,
   punti, matrice globale, «Calcola globale», «Word globale», «CSV» dei conci.
4. «Verifiche»: viste, elementi, filtri, interruttori, selezione di una riga e «Apri sezione in c.a.».
5. «Calcola armature», «Interrompi ricerca», «Applica proposta».
6. «Valori di calcolo…»: modificare un valore, «Applica e ricalcola»; chiudere la finestra durante il controllo.
7. «Distinta ferri…»: pagine, zoom, PDF, Word, CSV e PNG.
8. «Invia / carica terreno…»: invio a un nuovo foglio e sostituzione del terreno.
9. I «?» dei campi e F1.
10. Una revisione precedente (sola lettura): campi, tabelle e comandi di modifica bloccati; viste, valori (in sola lettura),
    report, CSV, distinta e calcolo globale disponibili.
11. Report Word/PDF e aspetti Scuro e Molto scuro.

## 6. Passaggio della Release: fatto (23/10, agente f5-pass-muri)

L'agente f5-pass-geo si era fermato prima dei muri, per la quota, lasciando il piano di ripresa in sei passi. L'agente f5-pass-muri lo ha
eseguito sul ramo `refactoring/f5-pass-muri` (base `2cf2a908`, dopo l'unione di f5-pass-strutture e f5-pass-geo). La Release apre i muri
con la vista MVVM; la vista storica è in `supporto/SUPERATI/ui-storica/muri/` (README e registro).

**Fatto.**
1. Cattura (commit `1f394f82`): `ui/check-muri-view` non apre più `new SheetEditor`. Dati e risultato dopo il calcolo, testi dei CSV
   delle verifiche, dei conci e della distinta, combinazioni mostrate (report e «Word globale»), pagine della distinta, etichette «?»,
   nomi di automazione e controlli in sola lettura sono confrontati con i valori della vista storica registrati in
   `tests/fixtures/f5-muri/vista-storica.json` (corsa di cattura `20261023-160230-muri-cattura`, PASS 216). Report Word e PDF,
   «Word globale» e distinta (PDF, Word, PNG) si confrontano con la ricetta della vista storica sullo stesso risultato
   (`WallExports.CreateDocument` con `ModuleFigureRenderer.WallFigures`, `WallExports.CreateGlobal`, `WallModel.CalculateBarSchedule`
   con `ModuleFigureRenderer.BarFigures`).
2. Prove fuori schermo (stesso commit): `GlobalGuidanceChecks`, `WallAdvancedChecks` e `WallMaterialChecks` guidano la vista XAML
   aperta dalla fabbrica del banco in una finestra fuori schermo mai attivata (`OffscreenWall` in `tests/Desktop/WallSmoke.cs`,
   con `MvvmWallSmoke`); «nessuna finestra nativa» diventa «nessuna finestra sullo schermo». `ParseHistory` da
   `WallSheetPresentation`; tolto il sovraccarico `ReportFigureCaptions(RetainingWallWorkspace…)`. I disegni chiamano
   `WallSheetPresentation.Number` e `WallModel.RebarLabel` direttamente.
3. `F3WallParameterChecks.cs` archiviato; la sonda dei muri di `F3TypedSchedulingChecks.cs` tolta (il file resta: contiene
   `F3SchedulingProbe` e la parte della sezione c.a.); `F3SchedulingChecks` chiama solo `F3WallMvvmSchedulingChecks` (tolto `Active`).
4. `ReportAppearanceChecks` (fuori dal runner): la parte dei muri su `OffscreenWall`; solo compilata, non eseguita.
5. `Architecture.Tests`: i due controlli (parser dell'accelerogramma, angolo sismico) leggono i file di `X.Desktop/Views/RetainingWall`
   e di `src/ANTHEA.Presentation/Modules/RetainingWall`.
6. Commit del passaggio: riga `Mvvm` in `ModuleImplementations.Declared`; archiviati i dodici `RetainingWallWorkspace*.cs`,
   `tests/Desktop/RetainingWallHistoricTrace.cs` e `F3WallParameterChecks.cs`; tolti i rami `retainingWall` di `SheetEditor`,
   `HistoricWallSmoke`, `--check-muri-historic[-capture]` e `ui/check-muri-historic`, il blocco `RetainingWallContrastStep`,
   `$MvvmBench` con tutte le suite `-mvvm` (`ui/smoke-project-workspace-mvvm`, `ui/smoke-retaining-wall-mvvm`,
   `ui/smoke-global-stability-mvvm`, `ui/check-appearance-mvvm`, `ui/shell-smoke-project-workspace-mvvm`) e `ui/f3-scheduling-muri-mvvm`;
   `ModuleHostWpfChecks` con i muri in `ReleaseMvvm` e «historic» rifiutato; baseline F4: tolte le 4 voci dei file archiviati.
   `$MvvmBench` apriva solo i muri: la sezione c.a. ha il suo banco (`$MvvmConcreteSection`), che il passaggio non tocca.

**Non toccato.** `MainWindow.RetainingWall.cs` resta: `OpenModuleCopy` e `ReplaceCurrentSoil` servono a tutte le schede;
`OpenWallConcreteSection` e `ISheetEditor.ConcreteSectionRequested` non hanno più chi li solleva e si ritirano con `MainWindow`
(parte g). `tests/fixtures/f3/architecture.json` non cambia (come negli altri passaggi). Le 18 voci della Chiara del modulo restano
sulla chiave storica fino alla prima corsa di `ui/check-contrast` a 96 DPI (D12); la corsa a 144 DPI non si fa.

**Resta.** `ui/check-contrast` a 96 DPI; `ui/f3-equivalence` e la corsa completa sul ramo unito (coordinatore); la revisione delle
guide (nessun cambio di disposizione: le differenze visibili sono MU3 e MU4, già registrate). **Fatta nella Rev59** (24/10, 21a781eb):
le due frasi sulle revisioni in sola lettura sono nel capitolo dei muri della guida pratica; l'esito di MU3 e MU4 nel registro dice che sono
attive nella Release. `--check-report-appearance-offscreen` va
eseguita almeno una volta (fuori dal runner).

**Come riprendere.**

```
cd <worktree>
git log --oneline 2cf2a908..HEAD
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,ui -Only '^ui/(check-muri-view|check-global-guidance-offscreen|check-wall-advanced-offscreen|check-wall-materials-offscreen|smoke-retaining-wall|smoke-global-stability|f3-scheduling|check-module-host-offscreen)$' -Tag <tag>"
```
