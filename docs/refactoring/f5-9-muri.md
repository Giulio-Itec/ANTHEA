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
