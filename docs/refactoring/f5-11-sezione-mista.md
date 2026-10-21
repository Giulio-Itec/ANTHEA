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
controllo: non fanno parte di questa preparazione. La Release resta con la vista storica.

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
