# F5.11 — Sezione composta da ponte: preparazione

## Scopo

Questa nota chiude la **preparazione** di F5.11: modulo `str_mista_ponte`, vista storica `BridgeWorkspace`
(`X.Desktop/Wpf/BridgeWorkspace.cs` e 15 parziali `Bridge*.cs`: `BridgeLayout`, `BridgeAccessories`, `BridgeActionsTable`,
`BridgeContours`, `BridgeDetachedView`, `BridgeDetailsInputs`, `BridgeHistory`, `BridgeHomogenization`, `BridgeInformation`,
`BridgePredalle`, `BridgeReport`, `BridgeResponse`, `BridgeSectionProperties`, `BridgeStressScale`, `BridgeTorsion`). I disegni
(`BridgeDrawing*.cs`, `BridgeDetailSketch.cs`, `BridgeMethodSketch.cs`, `BridgeResponsePlot.cs`) restano WPF (Q3). I tipi senza WPF
del modulo stanno in `src/ANTHEA.Application/Bridge` (`BridgeEditor`, `BridgeResultView`, dati di presentazione) e
`src/ANTHEA.Application/Presentation`; il report in `src/ANTHEA.Reports/Generators/ReportBridge*.cs` (congelato).
La nota segue i passi 1–5 della «Ricetta per i moduli successivi» di [f5-4-pilota.md](f5-4-pilota.md). Contiene:
- che cosa esiste dopo la preparazione;
- i comportamenti della vista storica trovati con le tracce;
- che cosa resta per finire F5.11;
- le domande per il punto di controllo dell'utente.

La vista XAML, il passaggio in `ModuleImplementations` e l'archiviazione della vista storica vengono **dopo** il punto di
controllo: non fanno parte di questa preparazione. La Release resta con la vista storica. La vista XAML, il banco e le suite
riscritte sono fatti il 21/10 (sezione 5); restano il punto di controllo e il passaggio della Release.

Riferimenti:
- [proposta](f5-proposta-architettura.md), sezioni 3, 5, 7 e riga F5.11 della sezione 8;
- [inventario](f5-inventario-ui.md);
- [muri](f5-9-muri.md) (F5.9), il modulo più vicino per struttura (calcolo sullo sfondo, motore «occupato» fino al ritorno);
- `tests/fixtures/f3/README.md` (sezione «Fixture aggiunte in F5.11») e `tests/fixtures/f5-sezione-mista/README.md`.

Ramo `refactoring/f5-11-prep`, da `refactoring/f5` `c7fe03dd` (set ufficiale AE delle librerie), 21/10/2026. Il calcolo della
sezione composta (sezione efficace iterativa, storico lineare e non lineare a fibre) resta **sullo sfondo**, con il ricalcolo
automatico 500 ms dopo l'ultima modifica, come oggi (la scelta (b) di Q8).

## 1. Che cosa esiste dopo la preparazione

| Commit | Contenuto |
| --- | --- |
| `48ff81b5` | 7 fixture F3 nuove della sezione composta (2 → 9; 111 nel riferimento), attese catturate con la vista storica |
| `576ec167` | tracce congelate della vista storica: 20 scenari, 17 797 righe, 88 righe dei disegni, suite `ui/check-sezione-mista-historic` |
| `b280bce9` | derivazioni in Application (`BridgeResultPresentation`, `BridgeInputPresentation`, `BridgeResponsePresentation`, `BridgeReportPresentation`), campi condivisi in Presentation (`CompositeSectionFields`), baseline F4 3320 → 3221 |
| `3b8c83f3` | ViewModel `src/ANTHEA.Presentation/Modules/CompositeSection`, modulo registrato, testi e regola della scala del CLS in Application |
| `eb4def5e` | prove senza WPF: tracce, F3, sfondo, sola lettura, `Reload`, esportazioni |
| (questo commit) | questa nota |

### 1.1 Fixture F3

Le 7 fixture nuove (tabella completa in `tests/fixtures/f3/README.md`) coprono:
- H con anima inclinata in EN 1994-2, SLE rara, fy assegnato, predalle con i ferri riferiti all'intradosso, fasi di ogni tipo
  (solo acciaio, composta con n assegnato, ritiro, soletta esclusa alla quota comune, fase non attiva), la scheda aperta sulle fasi
  con contributi, situazione, asse neutro, contorno, limiti e scala del CLS manuale;
- cassoncino con torsione, distorsione, diaframmi a X e diaframma d'appoggio; pioli con armatura trasversale, forcine e fatica;
- seconda piattabanda inferiore, irrigidimenti intermedi diversi sui due lati, irrigidimento d'appoggio con montante rigido;
- storico lineare con due ritiri e il baricentro efficace iterativo; storico non lineare con tension stiffening e φ = 0 richiesto,
  aperto sulla pagina delle curve;
- dati incompleti (anima vuota, calcestruzzo fuori catalogo: nessun risultato, nessun report);
- un archivio con le prime disposizioni della vista (quattro pagine, nome precedente del metodo) migrato all'apertura.

Prima della cattura ogni input è stato calcolato con la vista storica in una radice temporanea. Gli input vengono dal generatore
`F3Fixtures.BridgeSectionF511`; gli attesi (61 file) da due catture uguali byte per byte. Gli attesi delle 104 fixture esistenti e
`transitions-f4.json` non cambiano (le 12 transizioni della sezione composta, `cls_teso_nl` aggiunto, restano sulle due fixture
precedenti); in `expected/esito.txt` cambia solo il numero delle fixture, 104 → 111, registrato nel manifest
(`fixture_aggiunte_f5_11`). `ui/f3-equivalence`, `ui/f3-report-chapters` (112 capitoli), `ui/f3-scheduling` e `ui/f3-figures`
passano.

### 1.2 Tracce della vista storica

Le tracce (`tests/fixtures/f5-sezione-mista/storica`, formato e scenari in `tests/fixtures/f5-sezione-mista/README.md`) registrano
lo stato visto dall'utente: per intero dopo l'apertura, poi dopo ogni passo solo le voci cambiate. Le voci sono i dati nell'ordine in
memoria, `Modified`, il calcolo, i testi, pagina e pannelli, situazioni, vista, interruttori, scala del CLS, prospetto, gruppi
visibili, ogni campo di ogni form, le schede delle fasi, la tabella delle azioni, i pannelli dei risultati come blocchi, le proprietà
della sezione, il disegno come dati e la pagina delle curve.

Gli scenari sono 20: il percorso F3 per ognuna delle 9 fixture, `ingressi-sezione`, `ingressi-accessori`, `fasi-schede`,
`tabella-azioni`, `risultati-viste`, `risultati-storico`, `curve-sezione`, `vista-staccata`, `report`, `sola-lettura`,
`calcolo-sullo-sfondo` (motore trattenuto da `HeldModuleService`). Ogni passo si fa con i controlli, come l'utente: un controllo
nascosto, spento, fuori dalla pagina mostrata o protetto dalla revisione non fa nulla. La vista storica non è modificata: la traccia
usa membri interni in una classe parziale compilata solo in UiTests (`tests/Desktop/CompositeSectionHistoricTrace.cs`).

Regola importante delle catture: le continuazioni della vista (ritardo di 500 ms, ritorno del motore, pubblicazione, curve) girano
alla priorità di inattività del dispatcher, impostata solo intorno al passo; avanzano solo mentre la traccia aspetta. Senza questa
regola «occupato» dopo «riapri» e «rilascia» dipendeva dai tempi.

La stessa definizione (`tests/Shared/CompositeSectionTrace.cs`) guida due prove:
- la vista storica, `ui/check-sezione-mista-historic` (stadio `ui`), finché la vista esiste; registra anche gli elenchi dei disegni
  WPF (`disegni/elenco.txt`) e i pixel delle figure del report;
- il ViewModel senza WPF (sezione 1.5).

### 1.3 Derivazioni in Application e campi condivisi

Passo 3 (`b280bce9`). La vista storica chiama le funzioni spostate e dà un'uscita identica (tracce e disegni congelati prima dello
spostamento, F3, capitoli autonomi, smoke del ponte):
- `BridgeResultPresentation`: panoramica, avvisi, schede del riepilogo, pannelli dei risultati come blocchi (`BridgePanel`,
  `BridgeBlock`: testo, tabella, gruppo), situazioni, punti di applicazione di N, CSV delle tensioni;
- `BridgeInputPresentation`: testi dello stato e degli avvisi, intestazioni, campi mostrati dai dati, quota comune, campi delle
  fasi, celle non applicabili, limite delle fasi; dal passo 4 anche la regola e i testi della scala del CLS
  (`TryParseScale`, `ScaleHelp`, `ScaleRefused`) e le intestazioni e la nota delle proprietà;
- `BridgeResponsePresentation` (pagina delle curve), `BridgeReportPresentation` (piano delle figure: `ModuleFigureRenderer` ora
  disegna soltanto), aggiunte a `BridgeSheetPresentation`;
- `src/ANTHEA.Presentation/Modules/CompositeSection/CompositeSectionFields.cs`: unica definizione di campi, voci, colonne e
  suggerimenti degli editor, letta dalla vista storica e dal ViewModel.

Baseline F4 3320 → 3221: 5 voci R4 spostate in `BridgeInputPresentation.FieldShown`, 104 voci R2/R6 dei fattori di unità tolte
(costanti con nome). P3 invariata (126). Il passo 4 non cambia la baseline (3221).

### 1.4 ViewModel

`CompositeSectionSheetViewModel` (`src/ANTHEA.Presentation/Modules/CompositeSection`, sei file parziali più
`CompositeSectionViews.cs` e `CompositeSectionSheetModule.cs`) su `SheetViewModelBase`, senza WPF:

| Vista storica | ViewModel |
| --- | --- |
| costruttore: forme, opzioni del calcolo, fasi; `BuildLayout` (ui_mista, migrazione, proprietà, interruttori, prospetto, curve, pagina salvata); anteprima; ricalcolo dopo 500 ms | `Open()`: stessa sequenza, stesse scritture nello stesso ordine (anche l'ordine delle chiavi nuove); `ScheduleCalculationAsync` con `RecalculationDelay` (500 ms); `Reload` rifà l'apertura |
| `InputForm` compatti, `ShowFields`, callback e `Dispatcher.BeginInvoke` (schede ricostruite, coefficienti della normativa mostrati di nuovo) | `Forms` (`FormViewModel`, `CompositeSectionFields`), stesse regole; le operazioni che la vista accodava al dispatcher girano dopo la modifica, nello stesso ordine |
| schede delle fasi (`BuildPhases`), omogeneizzazione sincronizzata | `Phases` (`CompositeSectionPhaseCard`: intestazione, form, omogeneizzazione con il testo n₀ · φeff, note, comandi ↑ ↓ Elimina) |
| `JsonGrid` delle azioni con lo stile delle celle per tipo di fase | `ActionRows` (`JsonRowViewModel` sulle fasi stesse), `SelectedActionIndex`, `TorqueColumnShown`, `InactiveKinds`/`IsCellInactive` (configurati quando cambia ciò da cui dipendono, come la vista), `EditCell`, comandi «+ Carico», «+ Ritiro», «↑», «↓», «Elimina» |
| `Changed` | `UserChanged`: stessa sequenza, poi `Changed` della base (revisione, risultati azzerati, ricalcolo automatico, `Modified`) |
| `ViewChanged`, `Modified` diretti (pagine, asse neutro, scala, proprietà della soletta, curve, divisori, sezioni del report) | `ViewChanged` (salvataggio della vista, niente durante costruzione, scelta della pagina o calcolo) e `ChangedKeepingResults` della base: `Modified` senza ricalcolo |
| `CalculateScheduledAsync` (`Busy` dall'inizio del lavoro al ritorno del motore) | `SheetCalculation` della base, `SheetCalculationMode.Background` sul servizio del contesto; `CompositeSectionEngineWatch`: «occupato» finché il motore torna anche per una richiesta superata; `OnCalculationPublished` legge il risultato come la vista (`BridgeResultView.Read`, anche per un risultato incompleto) e mantiene la situazione |
| pagine, intestazione degli ingressi, pannello laterale, `SetAnalysisPanels` | `Page`, `InputPage`, `InputTitle`, `InputSubtitle`, `AnalysisPanelsShown`, `StageControlsShown`, `DisplayShown` |
| `StageChoice`, «Mostra tutte le fasi → ultima situazione», `DisplayChoice`, `Results` | `Situations`, `SelectedSituation`, `ShowLatestSituation`, `DisplayIndex`, `ResultsTab`, `ShowEffectiveDetails` |
| interruttori della barra, menu «Verifiche ▾», scala del CLS | `GeometryLabels`, `RebarLabels`, `LabelsShown`, `NeutralAxis`, `NeutralAxisShown`, `NeutralAxisToolTip`, `ContourSection`, `ContourDiagram`, `StressLimits`, `ScaleText`, `ScaleAuto`, `ScaleShown`, `ScaleError`, `ScaleToolTip`, `CommitScale`, `RestoreScale` |
| prospetto locale | `DetailShown`, `DetailExpanded`, `DetailSection`, `DetailAtSupport` |
| `BridgeDrawing` (geometria, ingresso, situazione, modo, interruttori, amplificazione, punti di N) | `Drawing` (`CompositeSectionDrawing`, dati semplici, un valore nuovo a ogni cambio); il disegno resta WPF (Q3) |
| pannelli dei risultati, `VerificationCards`, convergenza | `StressPanel`, `VerificationPanel`, `HomogenizationPanel`, `EquilibriumPanel`, `Class4Panel`, `GeometryPanel`, `ShearPanel`, `StudsPanel`, `TorsionPanel` (`BridgePanel`), `TorsionResultsShown`, `Overview`, `WarningText`, `ConvergenceText`, `SummaryCards` (`ConcreteCardsViewModel` di F5.10, riusato) |
| proprietà della sezione (acciaio, soletta con il suo form, una scheda per fase) | `PropertyTabs`, `SelectedPropertyTab`, `PropertyNotice`, `SteelPropertyTables`, `SlabPropertiesForm`, `SlabPropertyTables`, `SlabPropertyError` |
| pagina delle curve (`BridgeResponse`) | `ResponseData`, `CurveForm`, `CurvePhases`, `CurvePhaseIndex`, `CurveBusy`, `CalculateResponseAsync` (secondo `RecalculationScheduler`, annullato da una modifica), `CancelResponse`, `CurveNotice`, `PointValue`, `PointMaximum`, `FiberChoices`, `FiberIndex`, `SectionChart`, `FiberChart`, `CurveTable`, `ExportResponse` |
| «Esporta CSV…», `BuildReportDocument` | `ExportCsv` (`IFileDialogService`), `Report`: il generatore esistente con le figure di `ICompositeSectionFigureRenderer` (piano condiviso), sezioni salvate in `ui_mista` |
| `BridgeDetachedView` (finestra propria del disegno) | `Detach`, `Reattach`, `IsDetached`: la finestra è compito dell'ospite (sezione 4, SM3) |
| «Info modello…» (finestra modale con testi fissi) | nessun membro: resta alla vista (testi e schizzi del metodo) |
| `RevisionInspection.Protect` | `IsReadOnly`/`CanEdit`: form in sola lettura (T2), comandi, celle, interruttori, scala, prospetto e curve rifiutati; pagine, situazioni, viste, asse neutro, schede, dettagli, divisori, vista staccata, calcolo, report ed esportazioni ammessi |
| `Dispose` | `Dispose`: calcoli e curve annullati |

Modulo: `CompositeSectionSheetModule` (chiavi di stato `ui_mista` e `curve_sezione`, report senza breve), registrato in
`DesktopComposition.CreateModules` con `CompositeSectionFigureRenderer` (figure WPF di `ModuleFigureRenderer.BridgeFigures`).
`ModuleImplementations` non cambia: la Release apre la vista storica; il banco `--module-impl str_mista_ponte=mvvm` resta senza
vista finché manca il DataTemplate. In `tests/Desktop/ModuleHostWpfChecks.cs` il registro atteso include `str_mista_ponte` e
l'esempio di «modulo senza implementazione MVVM» diventa `str_bridge_design`.

### 1.5 Prove

`Presentation.Tests` aggiunge `CompositeSectionSheetChecks` (151 controlli, circa un minuto):
- 20 tracce uguali riga per riga a quelle della vista storica (17 797 righe);
- 9 fixture uguali al riferimento F3: aperto, visitato, calcolato, risultato, testo e struttura del report, esportato, stato,
  riaperto. Sono uguali byte per byte, salvo i 12 file delle 2 fixture vecchie con le transizioni F4, confrontati come in
  `ui/f3-equivalence` con quelle transizioni ammesse (nessuna differenza non ammessa);
- modulo, registro e chiavi di stato: viste, schede, situazioni, interruttori, scala, divisori, proprietà della soletta e curve
  scrivono solo `ui_mista` e `curve_sezione`, con `Modified`, e conservano il risultato;
- ogni form (anche le schede ricostruite entrando nella pagina delle fasi) apre la Wiki con il navigatore dei servizi e il modulo
  del foglio;
- calcolo sullo sfondo con barriere: fuori dal thread dell'interfaccia, occupato finché il motore torna, solo la revisione corrente
  pubblicata, chiusura durante un calcolo e durante il ritardo del ricalcolo automatico;
- sola lettura dal contesto, ritorno alla modifica, `Reload` uguale a una riapertura, CSV delle tensioni e della curva, esportazioni
  rifiutate senza risultato.

`--sezione-mista` esegue solo questi controlli (per lavorare sul modulo).

L'adattatore della traccia guida il ViewModel come la vista e lo legge come lo vede l'utente. Un solo punto modella la vista storica
invece di leggere il ViewModel: la protezione della revisione. `RevisionInspection.Protect` raggiunge subito i controlli dell'albero
logico (la pagina mostrata, anche nei gruppi chiusi) e poi, a ogni layout, quelli materializzati nell'albero visuale (mostrati almeno
una volta). Nella traccia `sola-lettura` i form della pagina «Pannello di controllo» nei gruppi chiusi (coefficienti, materiali,
armature, accessori) e la seconda piastra nascosta restano quindi «modificabili» anche dopo il passaggio alla pagina; l'utente però
non li può usare prima di vederli, e vedendoli sono protetti. Il ViewModel rifiuta tutto subito; l'adattatore registra solo quali
parti la protezione storica aveva raggiunto.

Altre prove: `Application.Tests` (`BridgeSectionPresentationChecks`, passo 3), `Architecture.Tests` (P1–P7, F4 3221, P3 126),
`ui/check-sezione-mista-historic` (la vista storica che chiama le funzioni spostate), `ui/check-module-host-offscreen`,
`ui/f3-equivalence`, `ui/f3-report-chapters`, `ui/f3-scheduling`, gli smoke del ponte. Corse e cartelle nella sezione 1.6.

### 1.6 Corse

Cartelle in `supporto/artefatti/ci` del worktree.

| Corsa | Esito |
| --- | --- |
| quick di base su `c7fe03dd` (`20261021-074911-f511-base`) | PASS 55 |
| dopo il passo 1 (`…-082335-f511-c1-quick`), il passo 2 (`…-100208-f511-c2-quick`), il passo 3 (`…-103045-f511-c3-quick`) | PASS 55, NEW-FAIL 0 |
| passo 4 da solo, senza i file del passo 5 (`20261021-113020-f511-c4-quick`) | PASS 55, NEW-FAIL 0 |
| suite `ui` toccate dal passo 4 (`…-112504-f511-c4-ui`, `…-112942-f511-c4-host`) | `ui/check-sezione-mista-historic`, `ui/f3-scheduling`, `ui/f3-report-chapters`, `ui/check-module-host-offscreen` PASS |
| passo 5 (`20261021-114719-f511-c5-quick`; `…-114625-f511-c5-quick` interrotta e ripetuta) | PASS 55, NEW-FAIL 0; Presentation.Tests 3674 (458 s) |

**Corsa finale** `-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` su `eb4def5e`, con questa nota non ancora
registrata: `supporto/artefatti/ci/20261021-120330-f511-finale`, PASS 148, NEW-FAIL 0 (build 26, fast 33, wiki 2, ui 87), 74 minuti,
nessun file tracciato modificato, `transitions-f4.json` invariato.
- `ui/f3-equivalence` PASS con 111 fixture; `ui/f3-report-chapters` 112 capitoli autonomi e 274 immagini;
  `ui/check-sezione-mista-historic` 20 scenari, 17 797 righe e 88 disegni uguali a quelli congelati;
  `ui/check-module-host-offscreen` 51 controlli; smoke del ponte (`ui/smoke-bridge`, `-curves`, `-predalle`, `-traffic`, `-prices`,
  `-design`) PASS.
- `Presentation.Tests` 3674 (449 s), `Application.Tests` 3113, `Architecture.Tests` 566, `Presentation.Tests/sezione-ca` 30.
- `ui/check-contrast` è esclusa: lo schermo della macchina è al 150% (144 DPI) e la baseline è a 96 DPI (SM9).

## 2. Comportamenti della vista storica trovati

Nessun difetto nei risultati: tracce, F3 e capitoli autonomi coincidono. Il ViewModel conserva i comportamenti seguenti, perché le
tracce siano identiche; si possono correggere dopo il passaggio della Release, con una nuova cattura.

1. **Pannelli vuoti all'apertura.** Prima del primo risultato i pannelli dei risultati sono vuoti (nessun blocco) e le schede del
   riepilogo senza testo; dopo una modifica senza risultato mostrano «Nessun risultato aggiornato» e «Risultati da aggiornare».
2. **Gruppo della torsione.** Il gruppo «Cassoncino · torsione, distorsione e diaframmi» delle verifiche è visibile anche per una
   sezione a H finché arriva il primo risultato.
3. **Situazione scelta durante un calcolo.** Durante un calcolo la scelta della situazione cambia, ma risultati e disegno restano
   quelli della situazione precedente e la vista non è salvata (`ViewChanged` e il gestore escono quando la vista è occupata).
   Anche vista, scheda dei risultati e interruttori non segnano il foglio modificato durante un calcolo.
4. **«Mostra tutte le fasi → ultima situazione».** Se la situazione cambia, `Modified` arriva due volte.
5. **Apertura sulla pagina delle curve.** Con `tab` = 2 salvato, la vista non ha mai mostrato una pagina di ingressi: titolo e
   sottotitolo vuoti, comandi delle situazioni e scelta della vista visibili, nessuna scheda dei risultati scelta (−1), pannello del
   riepilogo. Diventano coerenti alla prima pagina di ingressi.
6. **Scheda dei risultati sul pannello di controllo.** Sulla pagina «Pannello di controllo» la tabella dei risultati è nascosta: i
   passi sulle schede dei risultati non fanno nulla (non è un difetto, è la disposizione).
7. **Prospetto locale.** Aprire o chiudere il prospetto scrive `prospetto` in `ui_mista` senza segnare il foglio modificato.
8. **Protezione pigra della revisione** (sezione 1.5): non visibile all'utente.

## 3. Che cosa resta per finire F5.11

Dopo il punto di controllo (l'ordine segue i passi 1–7 di F5.4):
1. **Vista XAML** `X.Desktop/Views/CompositeSection/CompositeSectionSheetView.xaml` e il suo DataTemplate, solo token di F5.3 (P7):
   - disposizione di oggi: intestazione con «Info modello…» e le tre pagine; colonna degli ingressi (pagina 0: normativa,
     coefficienti, materiali, geometria, armature, accessori; pagina 1: metodo, discretizzazione, limiti, schede delle fasi) con i
     comandi delle situazioni; disegno con barra (vista, zoom, quote, armature, asse neutro, scala del CLS, «Verifiche ▾», «Stacca
     vista») e prospetto locale; pannello laterale (riepilogo o proprietà); tabella delle azioni e schede dei risultati; pagina delle
     curve; divisori con `SplitRatio`/`SetSplit`;
   - form con `FieldForm` e i suggerimenti degli editor di `CompositeSectionFields.EditorToolTips` e `TorqueToolTip` (SM7);
   - pannelli dai `BridgePanel` (testo, tabella, gruppo) e tabelle delle proprietà da `PresentationTable`;
   - disegni WPF invariati (`BridgeDrawing`, `BridgeDetailSketch`, `BridgeResponsePlot`) alimentati da `Drawing`, `SectionChart`,
     `FiberChart`; «Info modello…» resta una finestra della vista;
   - vista staccata (SM3) e sola lettura: legami a `CanEdit`, tranne pagine, situazioni, vista, asse neutro, schede, dettagli,
     «Stacca vista», «Esporta CSV…», «Esporta punti e fibre CSV…», «Info modello…».
2. **Banco**: cattura F3 con `--module-impl str_mista_ponte=mvvm` (anche `ui/f3-report-chapters` e `ui/f3-scheduling`).
3. **Confronto diretto** fra cattura storica e MVVM a differenza zero (`tools/qa/f5_confronto_diretto.py`), poi il confronto
   normale; ogni differenza va in `transitions-f5.json` con l'ok dell'utente.
4. **Suite UI riscritte** prima del passaggio della Release, sulla vista XAML ospitata da `MvvmSheetEditor` fuori schermo:
   - gli smoke del ponte in `supporto/test/Desktop` che aprono `BridgeWorkspace` e ne leggono controlli e membri:
     `BridgeSmokeChecks.cs`, `BridgeDetailsUiSmokeChecks.cs`, `BridgeGeometryViewSmokeChecks.cs`, `BridgeHistoryUiSmokeChecks.cs`,
     `BridgeOptionsSmokeChecks.cs`, `BridgePredalleUiSmokeChecks.cs`, `BridgeResponseUiSmokeChecks.cs`,
     `BridgeSectionTypeSmokeChecks.cs`, `BridgeShearUiSmokeChecks.cs`, `BridgeShrinkageUiSmokeChecks.cs`,
     `BridgeStressViewSmokeChecks.cs`, `BridgeTorsionProbes.cs`, `BridgeWorkspace.Tests.cs`, `NeutralAxisSmokeChecks.cs`
     (suite `ui/smoke-bridge`, `ui/smoke-bridge-curves`, `ui/smoke-bridge-predalle` e gli altri controlli che le chiamano);
   - `F3SchedulingChecks` (`BridgeWorkspace.CheckF3Scheduling`) e `F3TypedSchedulingChecks` (`tests/ANTHEA.Desktop.Equivalence`,
     suite `ui/f3-scheduling`);
   - `F3Fixtures` (`tests/ANTHEA.Testing/Capture`) per le prove preliminari degli input;
   - `ui/check-sezione-mista-historic` resta finché la vista storica esiste.
5. **Punto di controllo**, poi passaggio della Release e archiviazione (Q11) nello stesso commit:
   - `ModuleImplementations.Declared`: `("str_mista_ponte", SheetImplementation.Mvvm)`;
   - i 16 file `BridgeWorkspace*.cs`/`Bridge*.cs` della vista in `supporto/SUPERATI/ui-storica/sezione-mista/`, con
     `tests/Desktop/CompositeSectionHistoricTrace.cs`; i disegni restano;
   - il ramo `bridge` di `SheetEditor` (campo `bridge`, costruttore, `Result`, `Busy`, `Commit`, `CalculateAsync`,
     `BuildReportDocument`, `Dispose`) e il ramo del ponte di `MainWindow` per le sezioni del report salvate (`ui_mista` →
     `report_sezioni`);
   - le tracce congelate restano: `Presentation.Tests` continua a usarle.
6. **Guida pratica e PDF**, se la disposizione descritta cambia.

## 4. Domande per il punto di controllo F5.11 («da decidere»)

Le decisioni del 17/10 valgono anche qui (T2, T6, T7 e le convenzioni delle viste). Le scelte seguenti non hanno una proposta nei
documenti: sono provvisorie, con l'opzione presa, da rivedere con l'utente a F5.15.

- **SM1 · Calcolo sullo sfondo.** Opzione presa: ricalcolo automatico 500 ms dopo l'ultima modifica e «Calcola» immediato, sul
  servizio del contesto e con il limitatore dell'applicazione (`SheetCalculationMode.Background`), «occupato» fino al ritorno del
  motore, come oggi e come i muri (MU1, T3).
- **SM2 · Riferimento F3.** `expected/esito.txt` 104 → 111, registrato nel manifest (`fixture_aggiunte_f5_11`), come D2 di F5.4.
  Il coordinatore ricatena l'impronta con le fixture degli altri rami.
- **SM3 · Vista staccata.** Opzione presa: il ViewModel sa solo se il disegno è staccato (`Detach`, `Reattach`, `IsDetached`) e
  che cosa cambia nel pannello (scala e scelta della vista visibili, disegno con i risultati anche sulla pagina 0). La finestra
  (titolo, dimensioni, schermo intero F11, «Porta in primo piano», «Riaggancia») è dell'ospite: proposta per la vista XAML, una
  finestra aperta dalla vista con lo stesso disegno e la stessa barra legati al ViewModel, senza nuovi membri di `ISheetHost`.
- **SM4 · Schede del riepilogo.** Opzione presa: il ViewModel riusa `ConcreteCardsViewModel` di F5.10 (stessi testi di
  `VerificationCards`). Alternativa: spostarlo in `Modules/Shared` con un nome comune.
- **SM5 · Comportamenti storici conservati.** Opzione presa (T7): i punti 1–5 e 7 della sezione 2 sono conservati, le tracce sono
  identiche. Alternativa: correggerli dopo il passaggio della Release, con una nuova cattura.
- **SM6 · Prove nello stadio `fast`.** Opzione presa: `CompositeSectionSheetChecks` (circa un minuto, tracce e F3 compresi) nello
  stadio `fast`, come i muri (F5.9). Alternativa: tracce e F3 nello stadio `ui` con un'opzione, come C7 di F5.10.
- **SM7 · Suggerimenti degli editor.** Opzione presa: la vista storica sostituisce il suggerimento di alcuni editor
  (`EditorToolTips`, ΔT delle fasi) senza toccare l'etichetta; `FieldSpec.ToolTip` cambierebbe anche l'etichetta, quindi la vista
  XAML li leggerà da `CompositeSectionFields`.
- **SM8 · Baseline F4 dei disegni.** Resta a 3221: le voci rimaste del ponte sono nei disegni (Q3), come MU11.
- **SM9 · Contrasto a 96 DPI.** `ui/check-contrast` è stata esclusa dalle corse (schermo al 150%, 144 DPI): va ripetuta a 96 DPI
  prima dell'unione, come MU13. Il ramo non tocca colori né viste; il passo di contrasto autonomo arriva con la vista XAML.

**Materiale da mostrare** (dopo i passi 1–3 della sezione 3): schermate nei tre aspetti, `--check-contrast`, confronto diretto a
zero, `ui/f3-equivalence` con il modulo MVVM, questa nota con le risposte.

## 5. Vista fatta (21/10)

Agente f5-11-vista, ramo `refactoring/f5-11-vista` da `refactoring/f5` `d1f0c4d6`: punti 1–4 della sezione 3. La Release resta
**storica**: la vista nuova si apre solo con il banco UiTests (`--module-impl str_mista_ponte=mvvm`) e con
`ANTHEA.exe --interactive --module-impl str_mista_ponte=mvvm`, finché l'utente non prova una versione portatile. Le scelte provvisorie
della sezione 4 restano; le nuove scelte sono nella sezione 5.6.

### 5.1 Commit

| Commit | Contenuto |
| --- | --- |
| `ea373fbe` | vista XAML `X.Desktop/Views/CompositeSection` e il suo DataTemplate; piccole aggiunte al ViewModel |
| `d57ea875` | prove della vista `ui/check-sezione-mista-view`, passo di contrasto autonomo `mvvm-str_mista_ponte` |
| `35a7e382` | suite del ponte indipendenti dalla vista (`BridgeSmoke`), varianti `-mvvm`, sonda MVVM di `ui/f3-scheduling` |
| `8857eee4` | correzione trovata dalla corsa finale: i selettori dei modelli senza stato statico (regola P3) |
| (questo commit) | questa sezione; f5-ripresa e piano dal 21/10 |

### 5.2 Vista

**File** (`X.Desktop/Views/CompositeSection`):
- `CompositeSectionSheetView.xaml(.cs)`: la disposizione di `BridgeWorkspace`. In alto il motore, le tre pagine e «Info modello…»,
  l'avviso sul disegno; in basso l'avanzamento del calcolo e la riga di stato. Pagine 01 e 02: colonna degli ingressi (pannello di
  controllo o fasi, con le situazioni sulla pagina delle fasi), disegno con la barra e il prospetto locale, pannello laterale
  (riepilogo, oppure proprietà della sezione sul pannello di controllo), tabella delle azioni e schede dei risultati. Pagina 03:
  percorso, i due grafici, il cursore dei punti, la tabella dei punti.
- `CompositeSectionResources.xaml`: stili dei testi, carta e pannelli, gruppi, pulsanti, nota; i modelli dei pannelli dei risultati
  (`BridgePanel`: un blocco solo, una pila o una pila con lo scorrimento proprio, come `Render` della vista storica), dei blocchi
  (testo, tabella, gruppo), delle tabelle delle proprietà (`PresentationTable`) e delle schede del riepilogo
  (`ConcreteCardsViewModel`, SM4).
- `CompositeSectionPanels.cs`: i selettori dei modelli, `CompositeSectionGrid` (le tabelle di `ResultTable`),
  `CompositeSectionActionsGrid` (le colonne di `JsonGrid` con le larghezze, le prime due bloccate, ΔT con `TorqueColumnShown`, le
  celle che non si applicano spente, attenuate e con il suggerimento), i convertitori.
- `CompositeSectionDetachedView.xaml(.cs)`: la finestra della vista staccata (SM3); `CompositeSectionInformationView.xaml(.cs)`:
  «Info modello…» con gli stessi testi della finestra storica e i quattro schemi del metodo; `CompositeSectionTemplates.xaml`: il
  DataTemplate unito da `App.xaml`.

**Che cosa fa il code-behind** (Q3 e stato della vista):
- i disegni storici invariati (`BridgeDrawing`, `BridgeDetailSketch`, i due `BridgeResponsePlot`) sui dati semplici del ViewModel
  (`Drawing`, `DetailAtSupport`, `SectionChart`, `FiberChart`); «Adatta», «−», «+», «PNG» (scritto dal foglio con il dialogo dei
  file dei servizi, `SaveDrawingImage`), «Espandi» (dialogo della vista);
- una sola pagina, una sola pagina di ingressi e un solo pannello laterale nell'albero, come la vista storica che li scambia: la
  visita della cattura F3 e le suite vedono le stesse parti (SM11);
- i divisori con `SplitRatio`/`SetSplit` e la disposizione del pannello di controllo (`SetAnalysisPanels`); la pagina almeno di
  1120 × 700 con le barre di scorrimento sotto;
- «CLS ×»: Invio conferma, Esc ripristina, l'uscita conferma; il testo non è sovrascritto mentre l'utente scrive (SM12);
- la vista staccata: finestra 1400 × 900 (minimo 880 × 550) con la situazione, «Schermo intero · F11», «Riaggancia», l'avviso; il
  disegno e la sua barra si spostano nella finestra, nel foglio resta il segnaposto con «Porta in primo piano» e «Riaggancia vista»;
  la finestra si chiude con il foglio;
- la cella in modifica confermata prima dei pulsanti sotto la tabella, come `ActionsTable.Commit`.

**Sola lettura** (T2): form in sola lettura, tabella bloccata, «Quote sezione», «Info armature», «Auto n», «CLS ×», «Verifiche ▾»,
«Sezione in vista», comandi delle fasi e della tabella, «Mostra tutte le fasi → ultima situazione», curva (fase, fibra, cursore,
«Calcola curva», «Annulla») spenti; pagine, situazioni, vista, asse neutro, schede, «Dettagli sezione efficace ↓», «Stacca vista»,
«Adatta», «PNG», «Espandi», zoom, «Esporta CSV…», «Esporta punti e fibre CSV…» e «Info modello…» disponibili: gli stessi controlli
della vista storica dopo `InspectRevision` (sezione 5.4).

**ViewModel** (`ea373fbe`): `SaveDrawingImage` per «PNG» (nome proposto `ANTHEA_Sezione_composta_·_geometria_e_tensioni.png`, errore
come il messaggio storico «Esportazione immagine»); `SelectedSituation` rifiuta −1 dall'esterno (una scelta che perde la voce quando
le situazioni cambiano scriverebbe −1: la vista storica ha il gestore muto durante la pubblicazione); `Forms` notificato dopo
`Reload`. Le tracce senza WPF di `Presentation.Tests` non cambiano.

**Solo token di F5.3** (P7, `Architecture.Tests` PASS). Corpi: titoli dei blocchi e dei pannelli 15 → 14 (`Font.Subheading`), 10 e
11 → `Font.Caption`, 12 → `Font.Small`, 13 → `Font.Body`, titolo di «Info modello…» 20 → `Font.Title` (D8). Il colore della curva
della fibra resta quello del disegno storico (`BridgeResponsePlot.FiberBrush`, Q3).

### 5.3 Prove

- `ui/check-sezione-mista-view` (`tests/Desktop/CompositeSectionViewChecks*.cs`, comando `--check-sezione-mista-view`; un quarto
  argomento esegue solo alcune parti):
  - i 20 scenari delle tracce guidati attraverso i controlli della vista e riletti dai controlli (stato, avvisi, pagine, scelte,
    interruttori, scala, prospetto, tabella, schede delle proprietà, disegno, grafici e scelte delle curve); campi, schede e
    pannelli dal ViewModel (MU19). Le continuazioni del foglio girano alla priorità di inattività, come nelle tracce storiche;
  - i disegni della vista (sezione, prospetto, curve) e i pixel delle figure del report per le 9 fixture, contro
    `tests/fixtures/f5-sezione-mista/disegni/elenco.txt`;
  - per le 9 fixture: dopo il calcolo automatico, la visita della cattura F3 scrive come la vista storica (stesse scritture, stessi
    `Modified`), i tre aspetti non scrivono, dati e risultato dopo il calcolo, report Word e PDF, sezioni del report salvate, CSV
    delle tensioni (anche rifiutato senza risultato) e, per la fixture delle curve, la curva e il suo CSV uguali alla vista storica;
  - schermate nei tre aspetti, aiuto, nomi di automazione, sola lettura, vista staccata, «Espandi», «PNG», «Info modello…», banco
    e «la Release resta storica».
- Contrasto: passo autonomo `mvvm-str_mista_ponte` in fondo a `supporto/test/Desktop/ContrastAudit.cs` (commenti `F5.11 begin/end`);
  con `--baseline` solo Scuro e Molto scuro. **Non eseguito**: lo schermo è a 144 DPI e la baseline a 96 DPI (SM9).
- Suite storiche del ponte riscritte (`35a7e382`): sezione 5.5.

### 5.4 Esiti

| Prova | Esito |
| --- | --- |
| `ui/check-sezione-mista-view` | PASS 114 controlli in circa 11 minuti: 20 tracce, 17 797 righe uguali a quelle congelate; 88 righe dei disegni uguali; 9 fixture (8 report Word e PDF, 1 curva); 10 schermate; 119 pressioni di «?» e F1 su 108 campi di 21 form al navigatore; nomi di automazione della vista storica conservati su 4 fixture; sola lettura uguale alla storica (83 e 132 controlli comuni); finestre della vista |
| Cattura F3 con `--module-impl str_mista_ponte=mvvm` | PASS 111 fixture, 77 articoli, 30 ricerche, report di progetto: 1131 file uguali al riferimento entro le tolleranze, 0 differenze non ammesse; transizioni F4 22 412 elencate e ammesse, come la cattura storica |
| Cattura F3 storica dello stesso codice | PASS 111 fixture, stessi conteggi |
| Confronto diretto (`tools/qa/f5_confronto_diretto.py`) | **0 differenze** su 1135 file (1132 uguali byte per byte, 3 JSON uguali in forma canonica; ignorati solo i 6 tempi del palo elastico), le 9 fixture della sezione composta comprese |
| `ui/f3-report-chapters` con MVVM | PASS 112 capitoli autonomi, 274 immagini e tutte le parti XML confrontate con la vista |
| `ui/f3-scheduling` con MVVM (`ui/f3-scheduling-sezione-mista-mvvm`) | PASS 47 controlli, come la storica |
| Smoke del ponte, vista storica e vista MVVM | `ui/smoke-bridge`, `-curves`, `-predalle`, `ui/smoke-neutral-axis` PASS con le due viste |
| Profilo quick | `20261021-144648-f511v-c1-quick`: PASS 55, NEW-FAIL 0 (`Architecture.Tests` P1–P7, `Presentation.Tests` PASS) |
| Corsa finale | `-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` su `35a7e382`, `supporto/artefatti/ci/20261021-152044-f511v-finale`: PASS 153, KNOWN 0, **NEW-FAIL 1** (build 26, fast 33, wiki 2, ui 93), 74 minuti. Il NEW-FAIL è `Architecture.Tests`: regola P3, un dizionario delle risorse tenuto in un campo statico della vista; corretto in `8857eee4`. Tutte le suite `ui` PASS, anche `ui/check-field-form-parity` e `ui/check-palo-elastico-historic` al primo colpo; `ui/check-sezione-mista-view` 210 s, `ui/check-sezione-mista-historic`, `ui/f3-equivalence` (111 fixture), `ui/f3-report-chapters`, `ui/f3-scheduling`, gli smoke del ponte e le cinque varianti `-mvvm`; `Presentation.Tests` PASS |
| Ripetizione sulla correzione | `8857eee4`, `supporto/artefatti/ci/20261021-163522-f511v-finale-p3`: `Architecture.Tests`, `ui/check-sezione-mista-view`, `ui/f3-equivalence`, i quattro smoke del ponte con le due viste e `ui/f3-scheduling-sezione-mista-mvvm`: PASS 15 (con i build), NEW-FAIL 0; nessun file tracciato modificato |

Banco su `35a7e382` (codice di produzione di `ea373fbe`), cartella `supporto/artefatti/refactoring/f5-11-vista/banco-2` del worktree.
Le schermate (144 DPI) sono nella cartella `vista/schermate` della suite: pagina delle fasi, pannello di controllo con i gruppi aperti
e pagina delle curve nei tre aspetti, la vista storica alla stessa misura, la vista staccata, «Info modello…».

Baseline architetturali: nessuna cambiata (F4 3221, P3 126). `transitions-f4.json` invariato.

### 5.5 Suite storiche: riscritte e da togliere al passaggio della Release

Riscritte (`35a7e382`), passano con le due viste:
- gli smoke del ponte usano `BridgeSmoke` (`tests/Desktop/BridgeSmoke.cs`), con i membri della vista storica che usavano e gli stessi
  nomi: `HistoricBridgeSmoke` (BridgeWorkspace) e `MvvmBridgeSmoke` (controlli di `CompositeSectionSheetView`, valori del ViewModel,
  editori di `FieldForm` costruiti dove WPF non li ha ancora disposti). `OpenSheet.Bridge` dà il foglio della finestra,
  `MainWindow.OpenBridge` un foglio aperto da solo con la fabbrica della finestra. File: `BridgeSmokeChecks.cs`,
  `BridgeDetailsUiSmokeChecks.cs`, `BridgeGeometryViewSmokeChecks.cs`, `BridgeHistoryUiSmokeChecks.cs`, `BridgeOptionsSmokeChecks.cs`,
  `BridgePredalleUiSmokeChecks.cs`, `BridgeResponseUiSmokeChecks.cs`, `BridgeSectionTypeSmokeChecks.cs`, `BridgeShearUiSmokeChecks.cs`,
  `BridgeShrinkageUiSmokeChecks.cs`, `BridgeStressViewSmokeChecks.cs`, `NeutralAxisSmokeChecks.cs`. Equivalenze:
  - i dati si rileggono dal foglio (la vista XAML lavora su una copia, la storica sui dati passati);
  - `Shown(campo)` al posto della visibilità dell'editore (FieldForm nasconde la riga, InputForm l'editore);
  - i pulsanti sotto la tabella si premono come l'utente (evento Click e comando);
  - la finestra staccata chiusa con il foglio si controlla sulla finestra stessa;
  - «Tabelle non ricreate dopo cambio limiti» ammette la tabella riusata con le righe del nuovo risultato: la vista XAML riusa la
    tabella di un blocco dello stesso tipo e le dà le righe nuove (SM14);
- `ui/f3-scheduling`: `F3CompositeSectionMvvmSchedulingChecks` al posto di `BridgeWorkspace.CheckF3Scheduling` quando il banco apre
  la sezione con la vista MVVM;
- `F3Fixtures`: le prove preliminari di nuovi input si fanno con la cattura F3 in una radice temporanea, anche con il banco
  (commento di `BridgeSectionF511`, README del riferimento F3);
- varianti in `build/ci.ps1` con `--module-impl str_mista_ponte=mvvm` (`$MvvmCompositeSection`): `ui/smoke-bridge-mvvm`,
  `ui/smoke-bridge-curves-mvvm`, `ui/smoke-bridge-predalle-mvvm`, `ui/smoke-neutral-axis-mvvm`, `ui/f3-scheduling-sezione-mista-mvvm`.

Al passaggio della Release (punto 5 della sezione 3), oltre a quanto già elencato:
- togliere le cinque varianti `-mvvm` e `$MvvmCompositeSection`, `HistoricBridgeSmoke` (e `BridgeSmoke.Mvvm` diventa vero sempre),
  il ramo storico di `F3SchedulingChecks` e `BridgeWorkspace.CheckF3Scheduling` (`F3TypedSchedulingChecks.cs`), le partial di prova
  della vista storica `supporto/test/Desktop/BridgeWorkspace.Tests.cs` e `BridgeTorsionProbes.cs`;
- `--capture-validation` (`AppHarness.StartValidationCapture`, illustrazioni di validazione, non è una suite) apre ancora
  `BridgeWorkspace`: va riscritto su `MvvmSheetEditor` o archiviato con la vista;
- `Architecture.Tests` (`Program.cs`, riga 122) legge `X.Desktop/Wpf/BridgeWorkspace.cs` («Regola cambio norma nella UI»): da
  spostare sul ViewModel o togliere con l'archiviazione;
- contrasto: togliere il blocco `F5.11` di `ContrastAudit.cs`, passare le voci della Chiara a `modulo-str_mista_ponte` con la chiave
  della vista nuova (D12), ripetere a 96 DPI;
- `ui/check-sezione-mista-view` resta (le tracce congelate); le parti che aprono la vista storica (esportazioni, aiuto, nomi, sola
  lettura, testi di «Info modello…») passano al confronto con i valori attesi; `ui/check-sezione-mista-historic` va con la vista.

### 5.6 Differenze visibili e scelte da confermare («da decidere», con la scelta fatta)

Differenze visibili, le stesse nei tre aspetti:
- **Corpi** (D8): titoli dei blocchi e dei pannelli da 15 a 14 punti; intestazioni delle schede del riepilogo da 10 a 11 (in una
  colonna stretta il titolo di «Cassoncino · torsione e distorsione» si accorcia prima); titolo di «Info modello…» da 20 a 22.
- **Colori**: il bordo delle carte `#D8E0EB` diventa `Token.Border` (D12); gli avvisi e l'avviso sul disegno `#8B5916` diventano
  `Token.Note.Foreground` (#865D16 in Chiara); il bordo di «CLS ×» rifiutato `#B33A40` diventa `Token.Status.Fail`. Negli aspetti
  scuri i colori seguono l'aspetto (la vista storica usava colori fissi).

Scelte provvisorie della vista (da rivedere a F5.15):
- **SM10 · Celle della tabella delle azioni scritte a ogni tasto** (`UpdateSourceTrigger=PropertyChanged`, la convenzione delle tabelle
  MVVM di F5.4). La vista storica scriveva alla conferma della cella. Il risultato non cambia (ricalcolo 500 ms dopo l'ultima
  modifica), ma le schede delle fasi si ricostruiscono a ogni tasto. Alternativa: scrittura alla conferma della cella, con un modo
  per confermarla quando il foglio fa `Commit`.
- **SM11 · Parti mostrate nell'albero.** Opzione presa: come la vista storica, nell'albero c'è solo la pagina mostrata (01–02 o 03),
  la sua colonna degli ingressi e il suo pannello laterale (scambiati dal code-behind). La visita della cattura F3 e le suite vedono le
  stesse parti; i gruppi aperti e lo scorrimento si conservano. Alternativa: tutto nell'albero con la visibilità, che cambierebbe la
  visita F3 (la pagina delle curve aprirebbe anche il prospetto locale).
- **SM12 · «CLS ×».** Opzione presa: la casella non è legata al ViewModel; il code-behind mostra il fattore del foglio quando la
  casella non ha il fuoco, come la vista storica. Alternativa: un legame semplice, che sovrascriverebbe il testo mentre si scrive a
  ogni ricalcolo.
- **SM13 · «Calcola curva».** È un comando asincrono: resta spento fino alla fine del comando, un istante dopo la fine del calcolo
  della curva. Non visibile all'utente.
- **SM14 · Tabelle dei pannelli riusate.** I pannelli dei risultati riusano la tabella di un blocco dello stesso tipo e le danno le
  righe nuove; la vista storica creava tabelle nuove a ogni risultato.
- **SM15 · Schede delle proprietà.** Sono generate dall'elenco del ViewModel, che cambia a ogni modifica dei dati: si rigenerano
  tutte (la vista storica teneva le prime due e rifaceva quelle delle fasi). La scheda scelta resta.
- **SM16 · «Info modello…».** I testi fissi stanno nella vista XAML; la prova li confronta con quelli di
  `X.Desktop/Wpf/BridgeInformation.cs` finché esiste. Alternativa: spostarli in Application con la vista storica che li legge.

### 5.7 Che cosa resta e da dove si riparte

1. `--check-contrast` a 96 DPI con il passo `mvvm-str_mista_ponte` (coordinatore, senza cambiare la baseline).
2. **Versione di prova portatile**: il modulo è pronto per la prossima versione portatile con le viste attive (aggiunto all'elenco di
   [f5-piano-dal-21-10.md](f5-piano-dal-21-10.md), sezione 1); prova a mano nella sezione 5.8.
3. **Punto 5 della sezione 3**: punto di controllo dell'utente, poi passaggio della Release e archiviazione nello stesso commit, con la
   pulizia della sezione 5.5. Poi il punto 6 (guida pratica e PDF, se la disposizione descritta cambia: non cambia).

Per riprendere:

```
cd <worktree>
dotnet build X.Desktop\X.Desktop.csproj -c UiTests
dotnet X.Desktop\bin\UiTests\net8.0-windows\ANTHEA.dll --check-sezione-mista-view <radice> <cartella vuota> [tracce,disegni,...]
dotnet X.Desktop\bin\UiTests\net8.0-windows\ANTHEA.dll --check-f3-equivalence <radice> <cartella> --module-impl str_mista_ponte=mvvm
dotnet X.Desktop\bin\UiTests\net8.0-windows\ANTHEA.dll --smoke-bridge <cartella> --module-impl str_mista_ponte=mvvm
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,ui -Only '^ui/(check-sezione-mista-view|smoke-bridge.*|smoke-neutral-axis.*|f3-scheduling.*)$' -Tag f511"
```

### 5.8 Da provare a mano (versione portatile con la vista attiva; oppure `ANTHEA.exe --interactive --module-impl str_mista_ponte=mvvm`)

1. Aprire sezioni reali (H, anima inclinata, cassoncino): le tre pagine, i gruppi del pannello di controllo, le proprietà della
   sezione (acciaio, soletta con φ/ψL/n, fasi), il prospetto locale e «Sezione in vista».
2. Modificare geometria, materiali, armature, accessori e fasi (schede e tabella, anche «+ Carico», «+ Ritiro», «↑», «↓»,
   «Elimina»): il ricalcolo parte dopo mezzo secondo, lo stato passa da «in attesa» a «in corso» ad «aggiornato», i risultati vecchi
   restano segnati «da aggiornare».
3. Situazioni, «Mostra tutte le fasi → ultima situazione», vista (tensioni totali, contributi, geometria), quote, armature, asse
   neutro, «Verifiche ▾», «CLS ×» e «Auto n», schede dei risultati, «Dettagli sezione efficace ↓», divisori (anche dopo una
   riapertura).
4. «Stacca vista»: situazione nella finestra, «Schermo intero · F11» ed Esc, «Riaggancia», «Porta in primo piano», «Riaggancia vista».
5. «Adatta», «−», «+», «PNG», «Espandi»; «Info modello…».
6. Curve della sezione: percorso, fase di partenza, «Calcola curva», «Annulla», fibra, cursore, «Esporta punti e fibre CSV…».
7. «Esporta CSV…», report Word e PDF; i «?» dei campi e F1.
8. Una revisione precedente (sola lettura): campi, tabella, comandi di modifica, curva bloccati; pagine, situazioni, viste, schede,
   esportazioni e report disponibili.
9. Aspetti Scuro e Molto scuro.
