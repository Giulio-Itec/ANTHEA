# Vista storica dei muri di sostegno

La cartella conserva la vista WPF storica della scheda «Muri di sostegno» (`geo_muri_sostegno`, `RetainingWallWorkspace` nei suoi dodici file `RetainingWallWorkspace*.cs`), la prova delle sue tracce (`RetainingWallHistoricTrace.cs`) e la sonda di `ui/f3-scheduling` sulla sua finestra dei valori (`F3WallParameterChecks.cs`). Sono state archiviate il 23 ottobre 2026, quando la Release è passata alla vista MVVM (refactoring F5, passaggi della Release, agente f5-pass-muri; decisione Q11). I file non sono compilati: nessun progetto include questa cartella. Sotto la cartella i percorsi riproducono quelli originali, relativi alla radice del repository. Gli stessi dati sono in `registro.json`.

Restano, perché li usano la vista nuova, i report o le altre schede: i disegni (`RetainingWallDrawing*.cs`, `RetainingWallBarDrawing.cs`, `RetainingWallFigureRenderer.cs`, `ModuleFigureRenderer.Wall.cs`), l'icona (`RetainingWallIcon.cs`) e `MainWindow.RetainingWall.cs` (`OpenModuleCopy` e `ReplaceCurrentSoil` servono a tutte le schede; `OpenWallConcreteSection` resta collegata all'evento `ConcreteSectionRequested` di `ISheetEditor` e si ritira con `MainWindow`, parte (g)).

| File | Origine | Ultimo commit che lo modifica |
| --- | --- | --- |
| `X.Desktop/Wpf/RetainingWallWorkspace.cs` | stesso percorso dalla radice del repository | `79307cf4`, invariato fino a `refactoring/f5-pass-muri` `2cf2a908` |
| `X.Desktop/Wpf/RetainingWallWorkspace.Advanced.cs` | stesso percorso | `79307cf4`, invariato fino a `2cf2a908` |
| `X.Desktop/Wpf/RetainingWallWorkspace.BarSchedule.cs` | stesso percorso | `d580c8a4`, invariato fino a `2cf2a908` |
| `X.Desktop/Wpf/RetainingWallWorkspace.GlobalGuidance.cs` | stesso percorso | `79307cf4`, invariato fino a `2cf2a908` |
| `X.Desktop/Wpf/RetainingWallWorkspace.GlobalSoils.cs` | stesso percorso | `79307cf4`, invariato fino a `2cf2a908` |
| `X.Desktop/Wpf/RetainingWallWorkspace.GlobalStability.cs` | stesso percorso | `79307cf4`, invariato fino a `2cf2a908` |
| `X.Desktop/Wpf/RetainingWallWorkspace.Inputs.cs` | stesso percorso | `79307cf4`, invariato fino a `2cf2a908` |
| `X.Desktop/Wpf/RetainingWallWorkspace.Materials.cs` | stesso percorso | `79307cf4`, invariato fino a `2cf2a908` |
| `X.Desktop/Wpf/RetainingWallWorkspace.Parameters.cs` | stesso percorso | `79307cf4`, invariato fino a `2cf2a908` |
| `X.Desktop/Wpf/RetainingWallWorkspace.Results.cs` | stesso percorso | `79307cf4`, invariato fino a `2cf2a908` |
| `X.Desktop/Wpf/RetainingWallWorkspace.Seismic.cs` | stesso percorso | `79307cf4`, invariato fino a `2cf2a908` |
| `X.Desktop/Wpf/RetainingWallWorkspace.Soils.cs` | stesso percorso | `79307cf4`, invariato fino a `2cf2a908` |
| `tests/Desktop/RetainingWallHistoricTrace.cs` | stesso percorso | `6a7fc606`, invariato fino a `2cf2a908` |
| `tests/ANTHEA.Desktop.Equivalence/F3WallParameterChecks.cs` | stesso percorso | `699b93a3`, invariato fino a `2cf2a908` |

Le impronte SHA-256 dei file (estratti su Windows con fine riga CRLF, e del contenuto in Git con fine riga LF) sono in `registro.json`.

La sonda `RetainingWallWorkspace.CheckF3Scheduling` era in `tests/ANTHEA.Desktop.Equivalence/F3TypedSchedulingChecks.cs`, che resta (contiene `F3SchedulingProbe` e la parte della sezione c.a.): la parte dei muri è stata tolta, come quelle della sezione composta e di Bridge Design; il testo è nella storia di Git fino a `2cf2a908`.

## Motivo

L'utente ha provato le viste nuove nella versione portatile e il 23/10/2026 ha chiesto di finire il refactoring delle viste («Moduli ora, shell dopo»). Da allora la Release apre i muri di sostegno con la vista MVVM (`X.Desktop/Hosting/ModuleImplementations.cs`) e la vista storica non è più raggiungibile.
Con le viste sono stati tolti:
- i rami dei muri di `X.Desktop/Wpf/SheetEditor.cs` (campo `retainingWall`, `Result`, `Busy`, costruttore, `Dispose`, `Commit`, `CalculateAsync`, `BuildReportDocument`);
- il comando `--check-muri-historic[-capture]` dell'harness UiTests e la suite `ui/check-muri-historic` di `build/ci.ps1`;
- `$MvvmBench` di `build/ci.ps1` (i muri erano l'ultimo modulo) e le suite `-mvvm` che apriva: `ui/smoke-project-workspace-mvvm`, `ui/smoke-retaining-wall-mvvm`, `ui/smoke-global-stability-mvvm`, `ui/check-appearance-mvvm`, `ui/shell-smoke-project-workspace-mvvm`, e la sonda `ui/f3-scheduling-muri-mvvm`;
- `HistoricWallSmoke` di `tests/Desktop/WallSmoke.cs` e il ramo storico di `F3SchedulingChecks`;
- il passo autonomo `mvvm-geo_muri_sostegno` di `--check-contrast` (`RetainingWallContrastStep`): i passi del modulo mostrano ora la vista nuova.

## Sostituzione

- **Vista.** `X.Desktop/Views/RetainingWall` (`RetainingWallSheetView`, `RetainingWallParametersView`, `RetainingWallBarScheduleView`), ospitata da `X.Desktop/Hosting/MvvmSheetEditor.cs`; i disegni restano quelli di `X.Desktop/Wpf`.
- **ViewModel.** `src/ANTHEA.Presentation/Modules/RetainingWall`.
- **Prove.** Le tracce congelate della vista storica restano in `tests/fixtures/f5-muri/storica`. Le confrontano `Presentation.Tests` (con il ViewModel) e `ui/check-muri-view` (attraverso la vista nuova), che confronta anche dati, risultati, CSV, combinazioni mostrate, pagine della distinta, etichette «?», nomi di automazione e sola lettura con i valori registrati della vista storica (`tests/fixtures/f5-muri/vista-storica.json`), e report, «Word globale» e distinta con la ricetta della vista storica. `ui/check-global-guidance-offscreen`, `ui/check-wall-advanced-offscreen`, `ui/check-wall-materials-offscreen` e `--check-report-appearance-offscreen` guidano la vista nuova fuori schermo (`OffscreenWall`); `ui/f3-scheduling` usa `F3WallMvvmSchedulingChecks`.

## Per rieseguire la vista storica

Fare `git revert` del commit di archiviazione. In alternativa: riportare i file nella posizione originale con `git mv`, poi ripristinare i rami dei muri di `SheetEditor`, la sonda in `F3TypedSchedulingChecks.cs`, il comando dell'harness, le suite, `$MvvmBench`, `HistoricWallSmoke` e il passo del contrasto, come in `2cf2a908`.
