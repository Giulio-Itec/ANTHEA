# F5 · Suite UI per i passaggi della Release (f5-suite-passaggi)

Ramo `refactoring/f5-suite-passaggi`, da `refactoring/f5` `d22550c4` (21/10/2026). Lavoro interrotto il 21/10 sera su richiesta del
coordinatore (spegnimento del PC): questo documento descrive lo stato esatto e come riprendere.

## Scopo

Nove moduli hanno la vista XAML unita ma la Release apre ancora la vista storica: `geo_fondazioni_superficiali`, `geo_palo_verticale`,
`geo_micropalo_verticale`, `geo_palo_orizzontale`, `geo_micropalo_orizzontale` (con la scheda elastica), `geo_muri_sostegno`,
`mat_calcestruzzo`, `mat_acciaio_armatura`, `geo_stratigrafia`. Il passaggio di ciascuno deve diventare un solo commit (riga di
`ModuleImplementations.Declared`, vista storica in `supporto/SUPERATI/ui-storica/<modulo>/`, blocchi `mvvm-` del contrasto tolti e voci
della Chiara spostate, D12). Oggi è bloccato da suite che aprono o leggono le viste storiche.

## 1. Inventario

Fonti: i fallimenti della prova f5e (`aw-f5-prova-e`, `supporto/artefatti/ci/20261021-112344-f5e-prova`: 11 attesi), le sezioni «suite
storiche» dei documenti dei moduli, la ricerca dei tipi storici in `supporto/test`, `tests/Desktop`, `tests/ANTHEA.Desktop.Equivalence`,
`tests/*UiChecks*`, `tests/ANTHEA.Testing`.

Legenda dell'effetto: **P** = fallisce dopo il passaggio della Release; **A** = non compila (o fallisce a runtime per riflessione) dopo
l'archiviazione; **R** = controllo «la Release resta storica», da togliere nel commit del passaggio.

### 1.1 Palo e micropalo orizzontale (`geo_palo_orizzontale`, `geo_micropalo_orizzontale`)

| Suite | File | Che cosa legge della vista storica | Effetto | Piano |
| --- | --- | --- | --- | --- |
| `ui/smoke-horizontal`, parte orizzontale di `ui/smoke` e `ui/smoke-mvvm` | `supporto/test/Desktop/HorizontalSmokeChecks.cs` (+ partial `HorizontalLayoutChecks.cs`, `HorizontalChsChecks.cs`, `HorizontalStratifiedSmokeChecks.cs` di `HorizontalWorkspace`) | `historicEditor.WaitForHorizontalAutomatic/VerifyHorizontal/VerifyHorizontalLayout/VerifyChs`; campi privati del workspace (`general`, `model`, `sectionFields`, `grids`, `cards`, `layout`, `advanced`, `ResultsTabs`…) | P (f5e: 3 fallimenti), A | **in corso**: `OpenSheet` + `HorizontalPileSmoke` (sezione 2) e duplicato `ui/smoke-horizontal-mvvm` |
| `ui/f3-scheduling` | `tests/ANTHEA.Desktop.Equivalence/F3PileSegmentChecks.cs` (partial di `PileReinforcementEditor`) | `new PileReinforcementEditor`, `CreateSegmentDesignWindow`, `segmentDesignTask` | A (passa dopo il passaggio perché non usa il banco) | sonda MVVM su `PileSegmentDesignViewModel` + `PileSegmentDesignView` scelta dal banco, come `F3WallMvvmSchedulingChecks`; variante `ui/f3-scheduling-palo-orizzontale-mvvm` |
| `ElasticPile.UiChecks`, `/anello-interno`, `/nota-stress-block` | `supporto/test/ElasticPile.UiChecks/*.cs` (progetto esterno, riflessione) | `HorizontalWorkspace` (`elastic`, `sectionFields`, `general`, `surveys`), `HorizontalSoilEditor` (`Tables`, `Header`), `PileReinforcementEditor` (`InvalidateResults`, `UpdateResults`, `Refresh`), `ElasticPileWorkspace` (campi `status`, `options`, `drawing`, `selection`, `calculationProgress`, `csv`, `sectionDetail`, `sectionPreview`, `reinforcementDrawing`) | A (a runtime: `GetType` nullo) | riscrivere su `HorizontalPileSheetViewModel`/`ElasticPileViewModel` (pubblici) e sulle viste XAML, con l'ospite `MvvmSheetEditor` raggiunto per riflessione; scelta della vista con `--module-impl` letto dal programma; varianti `-mvvm` in `build/ci.ps1`. Le parti di sola libreria restano |
| `--check-report-appearance-offscreen` (fuori dal runner, D9) | `supporto/test/Desktop/ReportAppearanceChecks.cs` | `new SheetEditor(PaloOrizzontale.Module, …)`, `WaitForHorizontalAutomatic` | A | aprire dalla fabbrica del banco (`ModuleHostBench.SheetEditors()`) e attendere con `MvvmSheets`/`HorizontalPileSmoke` |
| `ui/check-palo-orizzontale-view` | `tests/Desktop/HorizontalPileViewChecks*.cs` | confronti con `new SheetEditor(...)` (esportazioni, aiuto, nomi, sola lettura, F5-02, schermate storiche), `HorizontalForChecks`, `ElasticForChecks`; riga 438 «la Release resta storica» | R; A per i confronti | al passaggio: togliere il controllo R; i confronti con la vista storica si tolgono con l'archiviazione (restano tracce congelate, confronto diretto F3 a zero) |
| `ui/check-palo-orizzontale-historic`, `ui/check-palo-elastico-historic` | `tests/Desktop/HorizontalPileHistoricTrace.cs`, `ElasticPileHistoricTrace.cs` | tutta la vista storica | A | seguono la vista in `supporto/SUPERATI/ui-storica/palo-orizzontale/` e `palo-elastico/`; le suite si tolgono |
| `--check-contrast` | `supporto/test/Desktop/ContrastAudit.cs` | blocco F5.7 (`HorizontalPileContrastStep`) | — | al passaggio: togliere il blocco, voci della Chiara al passo `modulo-…` alla prima corsa a 96 DPI (D12) |

### 1.2 Muri di sostegno (`geo_muri_sostegno`)

| Suite | File | Che cosa legge | Effetto | Piano |
| --- | --- | --- | --- | --- |
| `ui/check-global-guidance-offscreen` | `supporto/test/Desktop/GlobalGuidanceChecks.cs` | `new RetainingWallWorkspace(...)` (3 volte) | A | `WallSmoke` (già esistente) su un foglio aperto dalla fabbrica del banco; variante `-mvvm` |
| `ui/check-wall-advanced-offscreen` | `WallAdvancedChecks.cs` | `new RetainingWallWorkspace`, `ParseHistory`, `CreateBarDrawing`, sovraccarico `ReportFigureCaptions(RetainingWallWorkspace…)` | A | come sopra; `ParseHistory` → funzione dell'Application usata dalla vista nuova |
| `ui/check-wall-materials-offscreen` | `WallMaterialChecks.cs` | `new RetainingWallWorkspace`, `Forms["materials"]`, `BarFigures`, `CreateBarDrawing` | A | come sopra |
| `ui/f3-scheduling` | `F3TypedSchedulingChecks.cs`, `F3WallParameterChecks.cs` (partial di `RetainingWallWorkspace`) | probe storiche | A | il ramo MVVM esiste (`F3WallMvvmSchedulingChecks`): al passaggio si toglie il ramo storico di `F3SchedulingChecks` e si archiviano i due file |
| `--check-report-appearance-offscreen` | `ReportAppearanceChecks.cs` | `new SheetEditor(RetainingWall.Module)`, `editor.retainingWall` | A | `openSheet.Wall`/`WallSmoke` sul foglio della fabbrica |
| `Architecture.Tests` | `tests/ANTHEA.Architecture.Tests/Program.cs` righe 123–124 | legge `RetainingWallWorkspace.Advanced.cs` e `.Results.cs` | A (file assenti) | spostare i due controlli sui file nuovi o toglierli con l'archiviazione (già in `f5-9-muri.md` 5.5) |
| `ui/check-muri-view` | `tests/Desktop/RetainingWallViewChecks.cs` | confronti con `new SheetEditor`, `.Historic`; riga 1244 R | R; A | come per il palo orizzontale |
| `ui/smoke-retaining-wall(-mvvm)`, `ui/smoke-global-stability(-mvvm)` | `WallSmoke.cs` | già indipendenti (f5-suite-ui) | — | al passaggio si toglie `HistoricWallSmoke` |
| `ui/check-muri-historic` | `RetainingWallHistoricTrace.cs` | vista storica | A | segue la vista |

### 1.3 Fondazioni superficiali (`geo_fondazioni_superficiali`)

| Suite | File | Che cosa legge | Effetto | Piano |
| --- | --- | --- | --- | --- |
| `FoundationWorkspace.UiChecks` (e `--candidate`) | `tests/FoundationWorkspace.Checks/Program.cs`, metodo `Ui` e parte `--ui` di `ContractChecks` | `X.Desktop.FoundationWorkspace` per riflessione (`TryCalculate`, `Result`, `TextBox` Tag `Width`, `raccogli-input`, sei schede, aspetto) | A | vista XAML ospitata da `MvvmSheetEditor` fuori schermo, libreria candidata iniettata nel servizio del contesto (già pianificato in `f5-9b-fondazioni.md` 3.5) |
| `ui/check-fondazioni-view` | `ShallowFoundationViewChecks.cs` | confronti con `new SheetEditor`, `FoundationForChecks`; riga 955 R | R; A | come sopra |
| `ui/check-fondazioni-historic` | `ShallowFoundationHistoricTrace.cs` | vista storica | A | segue la vista |

### 1.4 Stratigrafia (`geo_stratigrafia`)

| Suite | File | Che cosa legge | Effetto | Piano |
| --- | --- | --- | --- | --- |
| `Stratigraphy.UiChecks` | `tests/ModelViewer.UiChecks/StratigraphyRenderingChecks.cs` | albero visuale della vista storica, `StratigraphyImportWindow` per riflessione (`drafts`, `profiles`, `reviewed`, `UpdateApply`) | P/A | ViewModel e vista nuova (`f5-6b-stratigrafia.md` 5.6) |
| `ui/check-stratigrafia-view` | `StratigraphyViewChecks.cs` | confronti con `new SheetEditor`; riga 1120 R | R; A | come sopra |
| `ui/check-module-host-offscreen` | `ModuleHostWpfChecks.cs` | «la Release apre … con la vista storica» | R | aggiornare la riga del modulo a ogni passaggio |
| `ui/check-stratigrafia-historic` | `StratigraphyHistoricTrace.cs` | vista storica | A | segue la vista |

### 1.5 Palo e micropalo verticale

| Suite | File | Che cosa legge | Effetto | Piano |
| --- | --- | --- | --- | --- |
| `ui/f3-figures` | `F3FigureChecks.cs` (partial di `SheetEditor`, cast `(SheetEditor)opened`) | `allSeries`, `capacityView`, `plot`, `sondages`, `reference` | A | il ramo MVVM esiste (`ui/f3-figures-palo-verticale-mvvm`): verificare che il cast storico sia solo nel ramo storico, poi toglierlo |
| `ui/f3-scheduling` | `F3SchedulingChecks.cs` | `((SheetEditor)editor).generalForm` | A | ramo MVVM esiste; al passaggio si toglie il ramo storico |
| suite generali (`ui/smoke*`, `ui/check-appearance`) | `SmokeChecks.cs`, `DisplaySmokeChecks.cs` | già su `OpenSheet` (f5-suite-ui) | — | pulizia della sezione 4 di `f5-suite-ui.md` |
| `ui/check-palo-verticale-view` | `VerticalPileViewChecks.cs` | confronti storici, partial `SheetEditor` riga 1022, riga 1017 R | R; A | come sopra. Nota: fallisce anche a `elica-coesivo-avvisi` per l'antivirus (S6, noto) |

### 1.6 Materiali (`mat_calcestruzzo`, `mat_acciaio_armatura`)

| Suite | File | Che cosa legge | Effetto | Piano |
| --- | --- | --- | --- | --- |
| `ui/smoke-materials`, `ui/smoke-hierarchy`, `ui/smoke-sharing`, `ui/smoke-steel` | `OpenSheetChecks.cs`, `MaterialSmokeChecks.cs` | già indipendenti; rami storici `HistoricConcrete`, `SteelSmoke` storico | — | pulizia (sezione 4 di `f5-suite-ui.md`) |
| ramo storico di `CheckConcrete` | `supporto/test/Materiali/MaterialViewChecks.cs` (partial di `MaterialView`) | vista storica | A | segue la vista in `supporto/SUPERATI` (già detto in `f5-suite-ui.md`) |
| `ui/check-materials-view` | `MaterialSheetViewChecks.cs` | confronti con `new SheetEditor`; riga 741 R | R; A | come sopra |
| `ui/check-materials-historic` | `MaterialHistoricTrace.cs`, `MaterialViewTrace.cs` | vista storica | A | segue la vista |

### 1.7 Non toccate

- `FieldFormParityChecks`: confronta l'`InputForm` generico (resta per ponte e sezione c.a.); i nomi dei file storici sono solo testi.
- `tests/Shared/*Trace.cs`, `tests/ANTHEA.Testing/Capture/*.cs`, `ConcreteLibraryAdapter.Checks`, `DurabilityReferenceChecks`: citano le viste
  storiche solo nei commenti.
- Ponte, Bridge Design, sezione c.a.: fuori dai nove moduli (F5.10–F5.12).

## 2. Lavoro fatto: palo orizzontale, `ui/smoke-horizontal` (parziale, non ancora verde)

File:
- `tests/Desktop/HorizontalPileSmokeChecks.cs` (nuovo): `HorizontalPileSmoke`, gli stessi controlli dei partial storici sulla vista XAML
  (campi dei `FieldForm`, celle e comandi delle stratigrafie, «Estendi»/«Riduci», «Opzioni avanzate», «Diagrammi e dettagli» con
  `HorizontalPileSheetView.ShowWindow` mostrato senza attesa). `HorizontalStratifiedExample`: la parte senza vista di `VerifyStratified`
  (esempio con falda, report, salvataggio e riapertura), comune alle due viste.
- `tests/Desktop/OpenSheetChecks.cs`: `WaitForHorizontalAutomatic`, `VerifyHorizontal`, `VerifyHorizontalLayout`, `VerifyChs` con il ramo
  storico.
- `supporto/test/Desktop/HorizontalSmokeChecks.cs`: `openSheet` al posto di `historicEditor`.
- `supporto/test/Desktop/HorizontalStratifiedSmokeChecks.cs`: la parte senza vista chiama `HorizontalStratifiedExample` (stesso codice; i
  dati salvati sono ora una copia, `DeepClone`, invece del nodo vivo).

Stato delle prove (build UiTests verde):
- con `--module-impl geo_palo_orizzontale=mvvm --module-impl geo_micropalo_orizzontale=mvvm` la suite passa `VerifyStratified` intero e si
  ferma in `VerifyAutomatic` a «Resistenza personalizzata non conservata» (`sh-mvvm-4`): dopo «33» digitato in fck la scelta «Calcestruzzo»
  non risulta «Personalizzato». Ipotesi da verificare: il campo scrive come bozza (`FormViewModel.CommitOnFocusLoss` / `AddDraft`) finché
  la casella ha il fuoco, oppure `Get` legge il testo mostrato e non il valore; guardare `FieldViewModel.EditText` (righe 94–130, 232–265).
- la stessa suite con la Release (vista storica) **non è stata rieseguita** dopo la modifica di `HorizontalSmokeChecks.cs` e di
  `HorizontalStratifiedSmokeChecks.cs`.
- da fare dopo: il resto di `VerifyAutomatic`, `VerifyEditing`, `VerifyLayout` alle cinque misure, il micropalo (`VerifyChs`), poi il
  duplicato `ui/smoke-horizontal-mvvm` in `build/ci.ps1` e l'aggiunta delle due coppie orizzontali a `$MvvmBench` (per `ui/smoke-mvvm`).

## 3. Domande «da decidere» (scelte provvisorie, da rivedere in F5.15)

- **P1 · Scritture dirette dei dati nelle prove storiche.** `VerifyStratified` e `VerifyEditing` scrivevano il documento
  (`Data["stratigrafie"] = …; RebuildSurveys(); Changed()`). Sulla vista XAML gli stessi dati passano dal foglio con
  `SheetViewModelBase.Reload` (lo stesso effetto: il foglio ricostruito sui dati, ricalcolo automatico). Alternativa: digitare gli strati
  nelle celle (dati non identici a quelli della prova storica).
- **P2 · Colori dell'esito.** Come S1 di `f5-suite-ui.md`: i risultati sintetici (Rd nullo, 50, 200 kN) passano da
  `HorizontalPilePresentation.Verification` con le stesse attese; in più la vista deve mostrare righe, testo e colore (token
  `Status.Ok`/`Status.Fail`/`Muted`, differenza dichiarata in `f5-7-palo-orizzontale.md` 5.4) per tre risultati veri: soddisfatta, non
  soddisfatta (HEd 100000 kN), dati incompleti.
- **P3 · Larghezza del selettore del metodo stratificato.** La prova storica chiede almeno 180 px; la vista XAML ha la scelta compatta
  del `FieldForm` (120 px, D10, differenza dichiarata «Opzioni avanzate»), e «Stratificato» vi è intero (schermata
  `sh-mvvm-3/stratificato-opzioni.png`). Scelta: sulla vista XAML si controlla che il selettore sia largo come le altre scelte del form,
  che il testo scelto vi stia intero e che il bordo destro resti nella scheda (come prima). Il ramo storico tiene i 180 px. Alternativa:
  allargare la scelta nella vista (decisione di disegno, non presa).

## 4. Che cosa deve fare ogni commit di passaggio (stato al 21/10)

Per tutti: la riga del modulo in `X.Desktop/Hosting/ModuleImplementations.cs` → `Mvvm`; il controllo R della suite della vista e la riga di
`ModuleHostWpfChecks`; la vista storica e la traccia storica in `supporto/SUPERATI/ui-storica/<modulo>/` con `README.md` e `registro.json`
(modello: `palificata`); i rami del modulo in `SheetEditor`; il comando `--check-<modulo>-historic` di `AppHarness.cs` e la suite in
`build/ci.ps1`; il blocco del contrasto del modulo in fondo a `ContrastAudit.cs` (F5.9b fondazioni, F5.6b stratigrafia, F5.9 muri, F5.7
palo orizzontale, `AuditMvvmMaterials` materiali) e le voci della Chiara al passo `modulo-<modulo>` misurate a 96 DPI (D12); la coppia in
`$MvvmBench` e i duplicati `-mvvm` diventati superflui (`f5-suite-ui.md` sezione 4).

**Oggi nessun modulo è a un passaggio di una riga**: le righe «A» dell'inventario restano da riscrivere per ogni modulo (e per il palo
orizzontale anche le «P»). Le prove del passaggio (sezione 3 del compito) non sono state fatte.

## 5. Come riprendere

```
cd C:\Users\g.pacini\AppData\Local\Temp\aw-f5-suite-passaggi
dotnet build X.Desktop/X.Desktop.csproj -c UiTests
dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --smoke-horizontal supporto/artefatti/f5-suite-passaggi/<cartella> --module-impl geo_palo_orizzontale=mvvm --module-impl geo_micropalo_orizzontale=mvvm
dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --smoke-horizontal supporto/artefatti/f5-suite-passaggi/<cartella-storica>
```

Ordine: chiudere `ui/smoke-horizontal` nei due modi, poi `F3PileSegmentChecks`, `ElasticPile.UiChecks`, la prova del passaggio del palo
orizzontale su un ramo usa e getta; poi muri, fondazioni, stratigrafia, palo verticale, materiali. Corsa finale
`-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` ancora da fare.
