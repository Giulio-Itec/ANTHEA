# Vista storica della sezione composta da ponte

La cartella conserva la vista WPF storica della scheda «Sezione composta da ponte» (modulo `str_mista_ponte`, `BridgeWorkspace` e le sue
partial), la prova delle sue tracce e le partial di prova che la leggevano. Sono state archiviate il 23 ottobre 2026, quando la Release è passata
alla vista MVVM (refactoring F5.11, passaggio della Release; decisione Q11). I file non sono compilati: nessun progetto include questa cartella.
Sotto la cartella i percorsi riproducono quelli originali, relativi alla radice del repository. Gli stessi dati, con le impronte intere, sono in
`registro.json`.

| File | Contenuto | Ultimo commit che lo modifica | SHA-256 (CRLF, Windows) | SHA-256 (LF, contenuto in Git) |
| --- | --- | --- | --- | --- |
| `X.Desktop/Wpf/BridgeAccessories.cs` | file intero | `b280bce9` | `b831d704acac9ade…` | `7fd735a5cf0825b5…` |
| `X.Desktop/Wpf/BridgeActionsTable.cs` | file intero | `b280bce9` | `94da33f9a4e961ea…` | `b4f063dc5c6e5f9b…` |
| `X.Desktop/Wpf/BridgeContours.cs` | file intero | `000d6d92` | `389115ffa48b7f72…` | `f31a3bb9c0f72d94…` |
| `X.Desktop/Wpf/BridgeDetachedView.cs` | file intero | `577e319f` | `0b84e6f8258cc916…` | `0c8abe6e3b53b5f4…` |
| `X.Desktop/Wpf/BridgeDetailsInputs.cs` | file intero | `b280bce9` | `a348fd784ca38153…` | `1b8db5ebf9680ff0…` |
| `X.Desktop/Wpf/BridgeHistory.cs` | file intero | `b280bce9` | `fe8abc1b052b84ec…` | `54c1d7e638c1862b…` |
| `X.Desktop/Wpf/BridgeHomogenization.cs` | file intero | `b280bce9` | `c715eda684617ce6…` | `f1067c1424cf121d…` |
| `X.Desktop/Wpf/BridgeInformation.cs` | file intero | `0fc907fd` | `3818bd3a35b14b13…` | `56776ef137eef3fa…` |
| `X.Desktop/Wpf/BridgeLayout.cs` | file intero | `b280bce9` | `331b456b228e25d2…` | `aa07a429b6326e94…` |
| `X.Desktop/Wpf/BridgePredalle.cs` | file intero | `b280bce9` | `03ef8a9b123fa248…` | `f2a77768b5f17edb…` |
| `X.Desktop/Wpf/BridgeReport.cs` | file intero | `b280bce9` | `e5bebf9c311e84d0…` | `1733b65cdd0cd195…` |
| `X.Desktop/Wpf/BridgeResponse.cs` | file intero | `b280bce9` | `739d55c057cafff3…` | `f1e80580268a9f68…` |
| `X.Desktop/Wpf/BridgeSectionProperties.cs` | file intero | `3b8c83f3` | `64d2a92dac81b819…` | `cce67a0346414b40…` |
| `X.Desktop/Wpf/BridgeStressScale.cs` | file intero | `3b8c83f3` | `45368d17b20fc988…` | `79d48d323e5155e3…` |
| `X.Desktop/Wpf/BridgeTorsion.cs` | file intero | `b280bce9` | `aab94102b012604d…` | `5a5726e972923eb5…` |
| `X.Desktop/Wpf/BridgeWorkspace.cs` | file intero | `b280bce9` | `5ae5c43716359c4b…` | `eae3d5cbd83fe2b9…` |
| `tests/Desktop/CompositeSectionHistoricTrace.cs` | file intero | `576ec167` | `fa4005e075c98b4a…` | `9a42593f5d4b6e5e…` |
| `supporto/test/Desktop/BridgeWorkspace.Tests.cs` | file intero | `7edabf8d` | `564e64a013b323dc…` | `660813ba6e34b13f…` |
| `supporto/test/Desktop/BridgeTorsionProbes.cs` | file intero | `429e4c03` | `afd67f4553e30699…` | `a2ea0f1ef56e2e83…` |
| `tests/ANTHEA.Desktop.Equivalence/F3TypedSchedulingChecks.cs` | estratto: solo la partial BridgeWorkspace.CheckF3Scheduling (righe 1–14 del file a db7ef68d, con il namespace); il resto del file resta | `ecad955a` | `5491008c4664a41e…` | `9f90bc8b0311f45b…` |

Tutti i file sono invariati fino a `refactoring/f5` `db7ef68d`. Per `F3TypedSchedulingChecks.cs` è archiviata solo la partial
`BridgeWorkspace.CheckF3Scheduling`; il resto del file resta al suo posto.

## Motivo

L'utente ha deciso il 23/10/2026 di passare ora alla vista nuova i moduli ancora storici della Release («Moduli ora, shell dopo»). Da allora la
Release apre la sezione composta da ponte con la vista MVVM (`X.Desktop/Hosting/ModuleImplementations.cs`) e la vista storica non è più
raggiungibile. Con lei sono stati tolti:
- i rami della sezione composta di `X.Desktop/Wpf/SheetEditor.cs` (campo `bridge`, `Result`, `Busy`, costruttore, `Dispose`, `Commit`,
  `CalculateAsync`, `BuildReportDocument`);
- i comandi `--check-sezione-mista-historic` e `--check-sezione-mista-historic-capture` dell'harness UiTests e la suite
  `ui/check-sezione-mista-historic`;
- le varianti `-mvvm` del banco (`ui/smoke-bridge-mvvm`, `ui/smoke-bridge-curves-mvvm`, `ui/smoke-bridge-predalle-mvvm`,
  `ui/smoke-neutral-axis-mvvm`, `ui/f3-scheduling-sezione-mista-mvvm`), ora uguali alle suite normali;
- `HistoricBridgeSmoke` di `tests/Desktop/BridgeSmoke.cs` e il ramo storico di `ui/f3-scheduling`;
- il passo di contrasto autonomo `mvvm-str_mista_ponte` di `supporto/test/Desktop/ContrastAudit.cs`.

## Sostituzione

- **Vista.** `X.Desktop/Views/CompositeSection`, ospitata da `X.Desktop/Hosting/MvvmSheetEditor.cs`. I disegni (`BridgeDrawing*.cs`,
  `BridgeDetailSketch.cs`, `BridgeResponsePlot.cs`, `BridgeMethodSketch.cs`) restano in `X.Desktop/Wpf`; `BridgeWorkspace.F` è sostituito da
  `BridgePropertyPresentation.Number`, che chiamava.
- **ViewModel.** `src/ANTHEA.Presentation/Modules/CompositeSection`.
- **Prove.** Le tracce congelate della vista storica restano in `tests/fixtures/f5-sezione-mista` (`storica`, `disegni`). Le confrontano
  `Presentation.Tests` con il ViewModel e `ui/check-sezione-mista-view` attraverso la vista nuova. I valori che la suite della vista confrontava con
  la vista storica aperta accanto (dati, risultato e scritture lungo il percorso della cattura F3, CSV, curva, etichette «?», nomi di automazione,
  sola lettura, testi di «Info modello…») sono registrati in `tests/fixtures/f5-sezione-mista/vista-storica.json`; il report Word e PDF si
  confronta con la ricetta della vista storica (`BridgeReport.BuildReportDocument`: `BridgeResultView.Report` con le figure di
  `ModuleFigureRenderer.BridgeFigures`).
- **Strumenti comuni.** `Record`, `Pixels` e `WithIdleContinuations` della prova storica sono in `tests/Desktop/ViewTraceTools.cs`.
- **`--capture-validation`.** Il ramo del ponte apre la vista della Release con `BridgeSmoke` (gli stessi membri della vista storica).
- **Architettura.** La regola «cambio norma nella UI» di `tests/ANTHEA.Architecture.Tests` legge la vista XAML.

## Per rieseguire la vista storica

Fare `git revert` del commit di archiviazione. In alternativa:
1. riportare i file nella posizione originale con `git mv` (la partial di `F3TypedSchedulingChecks.cs` in testa al file);
2. ripristinare i rami di `SheetEditor`, i comandi dell'harness, le suite e le varianti `-mvvm` di `build/ci.ps1`, come a `db7ef68d`.
