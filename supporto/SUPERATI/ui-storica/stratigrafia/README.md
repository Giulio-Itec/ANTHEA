# Vista storica della stratigrafia

La cartella conserva la vista WPF storica della scheda «Stratigrafia del terreno» (`geo_stratigrafia`, `StratigraphyWorkspace`), la sua finestra dell'importazione IA (`StratigraphyImportWindow`) e la prova delle sue tracce. Sono state archiviate il 23 ottobre 2026, quando la Release è passata alla vista MVVM (refactoring F5, passaggi della Release, agente f5-pass-geo; decisione Q11). I file non sono compilati: nessun progetto include questa cartella. Sotto la cartella i percorsi riproducono quelli originali, relativi alla radice del repository. Gli stessi dati sono in `registro.json`. Il disegno del profilo (`StratigraphyProfileView`, usato dalla vista nuova) e l'icona del modulo (`SoilProfileIcon`), in fondo a `StratigraphyWorkspace.cs`, restano in uso: sono stati spostati senza modifiche in `X.Desktop/Wpf/StratigraphyProfileView.cs`. La finestra `SoilTransferDialog` resta: la usano gli altri moduli storici.

| File | Origine | Ultimo commit che lo modifica |
| --- | --- | --- |
| `X.Desktop/Wpf/StratigraphyWorkspace.cs` | stesso percorso dalla radice del repository | `8231c412`, invariato fino a `refactoring/f5` `db7ef68d` |
| `X.Desktop/Wpf/StratigraphyImportWindow.cs` | stesso percorso dalla radice del repository | `8231c412`, invariato fino a `refactoring/f5` `db7ef68d` |
| `tests/Desktop/StratigraphyHistoricTrace.cs` | stesso percorso dalla radice del repository | `f39de91c`, invariato fino a `refactoring/f5` `db7ef68d` |

Impronte SHA-256 del file estratto su Windows, con fine riga CRLF:
- `X.Desktop/Wpf/StratigraphyWorkspace.cs`: `1a0079a44c701fba66b3c40f845c67179de1d2f2177221b7acc28dfd7475f43a`;
- `X.Desktop/Wpf/StratigraphyImportWindow.cs`: `86222e86d530e9dfc67e8d811dfdb509d10db63e3c36303e5f084bd8b2d88bcf`;
- `tests/Desktop/StratigraphyHistoricTrace.cs`: `020d60adae3bb1f51643fa5e91b11bbe5a4d76ebe2e5f625672fb3482990ba32`.

Il repository conserva i testi con fine riga LF. Le impronte del contenuto in Git sono queste:
- `X.Desktop/Wpf/StratigraphyWorkspace.cs`: `8d30608f928ecb7cb01b6ac18cdda4fe666e39e64a055a69a7a490f732caf489`;
- `X.Desktop/Wpf/StratigraphyImportWindow.cs`: `74962599c15d060b22378a0a95fdb61dc1c86c2934743832cee18529ba1ad69a`;
- `tests/Desktop/StratigraphyHistoricTrace.cs`: `314f2a2b5852f20d0bdf399d3b22bfebce4fd3c3f445f05cc7753ff0eddfc42e`.

## Motivo

L'utente ha provato le viste nuove nella versione portatile e il 23/10/2026 ha chiesto di finire il refactoring delle viste («Moduli ora, shell dopo»). Da allora la Release apre la stratigrafia con la vista MVVM (`X.Desktop/Hosting/ModuleImplementations.cs`) e la vista storica non è più raggiungibile.
Con le viste sono stati tolti:
- i rami della stratigrafia di `X.Desktop/Wpf/SheetEditor.cs` (campo `soilWorkspace`, costruttore, `Commit`, `CalculateAsync`, `ExportReport`);
- il comando `--check-stratigrafia-historic[-capture]` dell'harness UiTests e la suite `ui/check-stratigrafia-historic` di `build/ci.ps1`;
- il passo autonomo `mvvm-geo_stratigrafia` di `--check-contrast` (`StratigraphyContrastStep`): il passo del modulo mostra ora la vista nuova;
- la lettura per riflessione di `StratigraphyImportWindow` in `Stratigraphy.UiChecks`, sostituita dal ViewModel e dalla vista del dialogo dell'importazione.

## Sostituzione

- **Vista.** `X.Desktop/Views/Stratigraphy` (`StratigraphySheetView`, `StratigraphyEditorView`, `StratigraphyImportDialogView`), ospitata da `X.Desktop/Hosting/MvvmSheetEditor.cs`.
- **ViewModel.** `src/ANTHEA.Presentation/Modules/Stratigraphy`.
- **Prove.** Le tracce congelate della vista storica restano in `tests/fixtures/f5-stratigrafia/storica`. Le confrontano `Presentation.Tests` (con il ViewModel) e `ui/check-stratigrafia-view` (attraverso la vista nuova), che confronta anche esportazioni, nomi di automazione e sola lettura con i valori registrati della vista storica. `Stratigraphy.UiChecks` apre il foglio dalla fabbrica del prodotto e prova l'importazione sul ViewModel e sulla vista del dialogo.

## Per rieseguire la vista storica

Fare `git revert` del commit di archiviazione. In alternativa: riportare i file nella posizione originale con `git mv` (e le due classi di disegno di nuovo in fondo a `StratigraphyWorkspace.cs`), poi ripristinare i rami della stratigrafia di `SheetEditor`, il comando dell'harness, la suite, il passo del contrasto e la parte storica di `Stratigraphy.UiChecks`, come in `db7ef68d`.
