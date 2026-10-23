# Vista storica del palo e del micropalo verticale

La cartella conserva la vista WPF storica delle schede «Palo verticale» (`geo_palo_verticale`) e «Micropalo verticale» (`geo_micropalo_verticale`), `GeoEditor.cs` (una partial di `SheetEditor`), e la prova delle sue tracce. Sono state archiviate il 23 ottobre 2026, quando la Release è passata alla vista MVVM (refactoring F5, passaggi della Release, agente f5-pass-geo; decisione Q11). I file non sono compilati: nessun progetto include questa cartella. Sotto la cartella i percorsi riproducono quelli originali, relativi alla radice del repository. Gli stessi dati sono in `registro.json`. I componenti condivisi che la vista usava restano dove sono: `StratigraphyTable`, `StratigraphyDrawing`, `Plot`, `ExtendedStratigraphy`, `CalculationHelpView`, `SoilTransferDialog`, `ModuleFigureRenderer.VerticalFigures`.

| File | Origine | Ultimo commit che lo modifica |
| --- | --- | --- |
| `X.Desktop/Wpf/GeoEditor.cs` | stesso percorso dalla radice del repository | `8f970183`, invariato fino a `refactoring/f5` `db7ef68d` |
| `tests/Desktop/VerticalPileHistoricTrace.cs` | stesso percorso dalla radice del repository | `db1a0b44`, invariato fino a `refactoring/f5` `db7ef68d` |

Impronte SHA-256 del file estratto su Windows, con fine riga CRLF:
- `X.Desktop/Wpf/GeoEditor.cs`: `c47c85d3594b4ab25569e9c7b3250828bf3a7156ad397af4dc6946a5d4310f39`;
- `tests/Desktop/VerticalPileHistoricTrace.cs`: `dc0f05a809620bc688a9f4537a53d61f2a0fc448f1de47ef14458d4f5edcc6e2`.

Il repository conserva i testi con fine riga LF. Le impronte del contenuto in Git sono queste:
- `X.Desktop/Wpf/GeoEditor.cs`: `93696678af2b17f0473f4fad6cfcac34f00c76b5a1a0d1cf6018ca8e360047cc`;
- `tests/Desktop/VerticalPileHistoricTrace.cs`: `b179554797f9d575f1f3d423390a6a96ef23eb00577b9a1952eda258ec4b7b32`.

## Motivo

L'utente ha provato le viste nuove nella versione portatile e il 23/10/2026 ha chiesto di finire il refactoring delle viste («Moduli ora, shell dopo»). Da allora la Release apre i due moduli con la vista MVVM (`X.Desktop/Hosting/ModuleImplementations.cs`) e la vista storica non è più raggiungibile.
Con le viste sono stati tolti:
- i rami del palo verticale di `X.Desktop/Wpf/SheetEditor.cs`: il ramo generico del costruttore (schede, piede con stato e avvisi, calcolo automatico), i campi e i metodi che lo servivano (`AddCard`, `LayoutCards`, `Changed`, `QueueCalculation`, `CalculateScheduledAsync`, tabelle, `SavePlot`), il ramo di `CalculateAsync`, `Commit` e `BuildReportDocument`; un modulo senza vista storica è ora rifiutato con un messaggio chiaro;
- le partial di prova di `SheetEditor` della vista storica: `DisplaySmokeChecks.cs` (`ui/smoke-display`), il ramo geo di `SmokeChecks.cs` (`VerifyWpf`, `SetGeometryForSmoke`; `WaitForAutomatic` attende ora solo un calcolo in corso), `VerticalCapacityPngForChecks` e i rami storici di `F3FigureChecks`, `F3SchedulingChecks`, `OpenSheet` e `ShellSmokeDisplay`;
- il comando `--check-palo-verticale-historic[-capture]` dell'harness UiTests e la suite `ui/check-palo-verticale-historic` di `build/ci.ps1`;
- le suite `ui/f3-figures-palo-verticale-mvvm`, `ui/f3-scheduling-palo-verticale-mvvm`, `ui/smoke-mvvm`, `ui/smoke-display-mvvm`, `ui/smoke-projects-mvvm`, `ui/shell-smoke-projects-mvvm` e `ui/shell-smoke-display-mvvm`, uguali alle suite normali.

## Sostituzione

- **Vista.** `X.Desktop/Views/VerticalPile` (`VerticalPileSheetView`), ospitata da `X.Desktop/Hosting/MvvmSheetEditor.cs`.
- **ViewModel.** `src/ANTHEA.Presentation/Modules/VerticalPile`.
- **Prove.** Le tracce congelate della vista storica restano in `tests/fixtures/f5-palo-verticale/storica`. Le confrontano `Presentation.Tests` (con il ViewModel) e `ui/check-palo-verticale-view` (attraverso la vista nuova), che confronta anche esportazioni, «Salva PNG…», aiuto, nomi di automazione e sola lettura con i valori registrati della vista storica. `ui/smoke`, `ui/smoke-display` e `ui/f3-figures` leggono la vista nuova (`VerticalPileSmoke`, `MvvmFigureStates`).

## Per rieseguire la vista storica

Fare `git revert` del commit di archiviazione. In alternativa: riportare i file nella posizione originale con `git mv`, poi ripristinare i rami del palo verticale di `SheetEditor` e delle prove (`DisplaySmokeChecks.cs`, `SmokeChecks.cs`, `F3FigureChecks.cs`, `F3SchedulingChecks.cs`), il comando dell'harness e le suite, come in `db7ef68d`.
