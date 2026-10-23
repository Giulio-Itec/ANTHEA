# Vista storica delle fondazioni superficiali

La cartella conserva la vista WPF storica della scheda «Fondazioni superficiali» (`geo_fondazioni_superficiali`, `FoundationWorkspace` con la parte `FoundationWorkspace.Materials.cs`) e la prova delle sue tracce. Sono state archiviate il 23 ottobre 2026, quando la Release è passata alla vista MVVM (refactoring F5, passaggi della Release, agente f5-pass-geo; decisione Q11). I file non sono compilati: nessun progetto include questa cartella. Sotto la cartella i percorsi riproducono quelli originali, relativi alla radice del repository. Gli stessi dati sono in `registro.json`. I disegni tecnici (`X.Desktop/Wpf/FoundationViews.cs`), l'icona del modulo (`FoundationIcon`), `StratigraphyTable` e `SoilTransferDialog` restano: li usano la vista nuova o gli altri moduli.

| File | Origine | Ultimo commit che lo modifica |
| --- | --- | --- |
| `X.Desktop/Wpf/FoundationWorkspace.cs` | stesso percorso dalla radice del repository | `f7bfe54f`, invariato fino a `refactoring/f5` `db7ef68d` |
| `X.Desktop/Wpf/FoundationWorkspace.Materials.cs` | stesso percorso dalla radice del repository | `f7bfe54f`, invariato fino a `refactoring/f5` `db7ef68d` |
| `tests/Desktop/ShallowFoundationHistoricTrace.cs` | stesso percorso dalla radice del repository | `9994ded5`, invariato fino a `refactoring/f5` `db7ef68d` |

Impronte SHA-256 del file estratto su Windows, con fine riga CRLF:
- `X.Desktop/Wpf/FoundationWorkspace.cs`: `7d601e502a642add793f95021a7a33ae4a7c2bfc1110ec9e2432a839719cb3f3`;
- `X.Desktop/Wpf/FoundationWorkspace.Materials.cs`: `5a9f49e83eab35d293c1f8d71bc92d47b1cb8a20ae7cdf4ad7085435e6df7abf`;
- `tests/Desktop/ShallowFoundationHistoricTrace.cs`: `6d951a9e04a3ea6904e3a7a04cf69718b7c0673e383b63784f05161013ae5827`.

Il repository conserva i testi con fine riga LF. Le impronte del contenuto in Git sono queste:
- `X.Desktop/Wpf/FoundationWorkspace.cs`: `0de34521ad4310ece9e97a43c46ed9a9c05e639807e2ec6c12850807908b3269`;
- `X.Desktop/Wpf/FoundationWorkspace.Materials.cs`: `dcbecfd6b125dcc5e86553bd12f97e93cb62b6aabe75089f0be4c0427ca9f9ab`;
- `tests/Desktop/ShallowFoundationHistoricTrace.cs`: `14a148d8e6346685f4438a7c793878f5c734986c7ca53416f764768bda451917`.

## Motivo

L'utente ha provato le viste nuove nella versione portatile e il 23/10/2026 ha chiesto di finire il refactoring delle viste («Moduli ora, shell dopo»). Da allora la Release apre le fondazioni superficiali con la vista MVVM (`X.Desktop/Hosting/ModuleImplementations.cs`) e la vista storica non è più raggiungibile.
Con le viste sono stati tolti:
- i rami delle fondazioni di `X.Desktop/Wpf/SheetEditor.cs` (campo `foundation`, `Result`, costruttore, `Commit`, `CalculateAsync`, `ExportReport`, `BuildReportDocument`);
- il comando `--check-fondazioni-historic[-capture]` dell'harness UiTests e la suite `ui/check-fondazioni-historic` di `build/ci.ps1`;
- il passo autonomo `mvvm-geo_fondazioni_superficiali` di `--check-contrast` (`ShallowFoundationContrastStep`): i passi del modulo mostrano ora la vista nuova;
- la creazione per riflessione di `X.Desktop.FoundationWorkspace` in `FoundationWorkspace.UiChecks`, sostituita dalla vista XAML ospitata da `MvvmSheetEditor`.

## Sostituzione

- **Vista.** `X.Desktop/Views/ShallowFoundation` (`ShallowFoundationSheetView`), ospitata da `X.Desktop/Hosting/MvvmSheetEditor.cs`; i disegni di `FoundationViews.cs`, invariati.
- **ViewModel.** `src/ANTHEA.Presentation/Modules/ShallowFoundation`.
- **Prove.** Le tracce congelate della vista storica restano in `tests/fixtures/f5-fondazioni/storica`. Le confrontano `Presentation.Tests` (con il ViewModel) e `ui/check-fondazioni-view` (attraverso la vista nuova), che confronta anche dati, risultati, elenchi di disegno, esportazioni, nomi di automazione e sola lettura con i valori registrati della vista storica. `FoundationWorkspace.UiChecks` (e `--candidate`) apre la vista nuova con la libreria iniettata nel servizio di calcolo del contesto.

## Per rieseguire la vista storica

Fare `git revert` del commit di archiviazione. In alternativa: riportare i file nella posizione originale con `git mv`, poi ripristinare i rami delle fondazioni di `SheetEditor`, il comando dell'harness, la suite, il passo del contrasto e il metodo `Ui` storico di `tests/FoundationWorkspace.Checks/Program.cs`, come in `db7ef68d`.
