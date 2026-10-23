# Vista storica di Bridge Design e finestra storica dei prezzari

La cartella conserva la vista WPF storica della scheda «Bridge Design» (modulo `str_bridge_design`, `BridgeDesignWorkspace` e le sue partial),
la finestra storica dell'editor dei prezzari (`BridgePricesView`), la prova delle tracce e la partial di prova che le leggevano. Sono state
archiviate il 23 ottobre 2026, quando la Release è passata alla vista MVVM (refactoring F5.12, passaggio della Release; decisione Q11). I file non
sono compilati: nessun progetto include questa cartella. Sotto la cartella i percorsi riproducono quelli originali, relativi alla radice del
repository. Gli stessi dati, con le impronte intere, sono in `registro.json`.

| File | Contenuto | Ultimo commit che lo modifica | SHA-256 (CRLF, Windows) | SHA-256 (LF, contenuto in Git) |
| --- | --- | --- | --- | --- |
| `X.Desktop/Wpf/BridgeDesignWorkspace.cs` | file intero | `f398251c` | `65c2882a133435e5…` | `7f62624499c87cc3…` |
| `X.Desktop/Wpf/BridgeDesignOptimization.cs` | file intero | `f398251c` | `cc6da881bcd9fde0…` | `2f6bf1a6fe84389f…` |
| `X.Desktop/Wpf/BridgeDesignOptimizationPanel.cs` | file intero | `f398251c` | `5cdd16f77b649d7b…` | `65cf91410a187a18…` |
| `X.Desktop/Wpf/BridgeDesignTechnical.cs` | file intero | `f398251c` | `dd416f092a189e20…` | `859b9aa08ec4eb0d…` |
| `X.Desktop/Wpf/BridgePricesView.cs` | file intero | `f398251c` | `133dc7d532b777b1…` | `57a0ce3d1bd1f636…` |
| `tests/Desktop/BridgeDesignHistoricTrace.cs` | file intero | `67b554dc` | `932b3c6aceb86dc2…` | `ac1de2be3155796a…` |
| `supporto/test/Desktop/BridgeDesignWorkspace.Tests.cs` | file intero | `7edabf8d` | `a9aad6eff9952630…` | `f7aab224bc069f5a…` |
| `tests/ANTHEA.Desktop.Equivalence/F3TypedSchedulingChecks.cs` | estratto: solo la partial BridgeDesignWorkspace.CheckF3Scheduling (con il namespace); il resto del file resta | `67b554dc` | `ec40f804b42e5519…` | `0e0d72bcc8da8ac0…` |

Tutti i file sono invariati fino a `refactoring/f5-pass-strutture` `67b554dc` (il passaggio della sezione composta, che in
`BridgeDesignHistoricTrace.cs` aveva solo spostato `Record` e `Pixels` in `tests/Desktop/ViewTraceTools.cs`). Per `F3TypedSchedulingChecks.cs` è
archiviata solo la partial `BridgeDesignWorkspace.CheckF3Scheduling`; il resto del file resta al suo posto.

## Motivo

L'utente ha deciso il 23/10/2026 di passare ora alla vista nuova i moduli ancora storici della Release («Moduli ora, shell dopo»). Da allora la
Release apre Bridge Design con la vista MVVM (`X.Desktop/Hosting/ModuleImplementations.cs`) e l'editor dei prezzari come dialogo del servizio;
la vista storica e la finestra storica non sono più raggiungibili. Con loro sono stati tolti:
- i rami di Bridge Design di `X.Desktop/Wpf/SheetEditor.cs` (campo `bridgeDesign`, `Result`, costruttore, `Dispose`, `CalculateAsync`,
  `ExportResult`, `BuildReportDocument`);
- i comandi `--check-bridge-design-historic` e `--check-bridge-design-historic-capture` dell'harness UiTests e la suite
  `ui/check-bridge-design-historic`;
- la variante `ui/f3-scheduling-bridge-design-mvvm` e `$MvvmBridgeDesign` di `build/ci.ps1`, ora uguali a `ui/f3-scheduling`;
- il ramo storico di `ui/f3-scheduling`;
- il passo di contrasto autonomo `mvvm-str_bridge_design` di `supporto/test/Desktop/ContrastAudit.cs`.

## Sostituzione

- **Vista.** `X.Desktop/Views/BridgeDesign` (`BridgeDesignSheetView`, ospitata da `X.Desktop/Hosting/MvvmSheetEditor.cs`; `PriceCatalogueView`,
  mostrata da `WpfDialogService`). Restano in `X.Desktop/Wpf` i disegni (`BridgeDesignDrawing`, `BridgeDesignAdvancedDrawing`,
  `BridgeOptimizationPlot`) e la pagina di «Stampa scheda» (`BridgeDesignPrintPage`); `BridgeDesignWorkspace.F` è sostituito da
  `BridgeDesignPresentation.F`, che chiamava.
- **ViewModel.** `src/ANTHEA.Presentation/Modules/BridgeDesign`.
- **Prove.** Le tracce congelate restano in `tests/fixtures/f5-bridge-design` (`storica`, `disegni`): le confrontano `Presentation.Tests` con il
  ViewModel e `ui/check-bridge-design-view` attraverso la vista nuova. I valori che la suite della vista confrontava con la vista storica e la
  finestra storica aperte accanto sono registrati in `tests/fixtures/f5-bridge-design/vista-storica.json`; il report Word e PDF si confronta con
  la ricetta della vista storica (`BridgeDesignModel.ReportDocument` con le due immagini del disegno). `BridgeOptimizationPlot.DataForChecks` è in
  `supporto/test/Desktop/BridgeOptimizationPlot.Tests.cs`. Gli smoke `ui/smoke-bridge-design`, `ui/smoke-bridge-traffic` e `ui/smoke-bridge-prices`
  sono riscritti sulla vista e sull'editor della Release (`BridgeDesignSmoke`).

## Per rieseguire la vista storica

Fare `git revert` del commit di archiviazione. In alternativa:
1. riportare i file nella posizione originale con `git mv` (la partial di `F3TypedSchedulingChecks.cs` dopo quella dei muri);
2. ripristinare i rami di `SheetEditor`, i comandi dell'harness, le suite e la variante `-mvvm` di `build/ci.ps1`, come a `67b554dc`.
