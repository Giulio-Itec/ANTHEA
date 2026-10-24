# Shell storica: SheetEditor, ospite R5 e finestra storica

La cartella conserva il codice della shell storica di ANTHEA ritirato con la parte (g) di F5.13 (refactoring F5, decisione Q11), sul ramo
`refactoring/f5-13-g` da `refactoring/f5` 67eafba5. I file non sono compilati: nessun progetto include questa cartella. Sotto la cartella i percorsi
riproducono quelli originali, relativi alla radice del repository. Gli stessi dati, con le impronte SHA-256, sono in `registro.json`.

Il ramo si unisce solo dopo la prova della shell nuova da parte dell'utente (decisione del 23/10, «Moduli ora, shell dopo»). Il piano e lo stato
sono in `docs/refactoring/f5-13-shell.md`, sezione 14.

## Primo passo (23/10/2026): SheetEditor e ospite R5

| File | Origine | Ultimo commit che lo modifica |
| --- | --- | --- |
| `X.Desktop/Wpf/SheetEditor.cs` | stesso percorso | `006f65c4`, invariato fino a 67eafba5 |
| `X.Desktop/Hosting/HistoricSheetViewModel.cs` | stesso percorso | `28a8ee6b`, invariato fino a 67eafba5 |
| `X.Desktop/Views/Shell/HistoricSheetHost.cs` | stesso percorso | `28a8ee6b`, invariato fino a 67eafba5 |
| `tests/Desktop/HistoricSheetEditor.cs` | stesso percorso | `a8b69641`, invariato fino a 67eafba5 |
| `tests/Desktop/ShellDraftChecks.HistoricHost.cs` | frammento di `tests/Desktop/ShellDraftChecks.cs`: il metodo `HistoricHost` con il suo commento, righe 567–681 a 67eafba5 | `879e6b71` |

### Motivo

Dal 23/10/2026 tutti i 15 moduli aprono la vista MVVM nella Release (`X.Desktop/Hosting/ModuleImplementations.cs`, nessun `Historic`). `SheetEditor`
era rimasto un guscio: il costruttore rifiutava ogni modulo. L'ospite R5 (`HistoricSheetViewModel` e `HistoricSheetHost`, notte del 22–23/10)
apriva nella shell nuova i moduli ancora storici: non ne restano. Il passo `HistoricHost` di `ui/check-shell-draft` era «non applicabile».

Con loro sono stati tolti, senza archiviarli perché sono rami di file che restano:
- il `DataTemplate` di `HistoricSheetViewModel` in `X.Desktop/Views/Shell/ShellTemplates.xaml`;
- i rami storici di `ReleaseSheetFactory` e di `DesktopSheetExports` (`X.Desktop/Hosting/ShellSheets.cs`) e di `SheetEditorFactory`: un modulo senza
  vista MVVM è rifiutato da `SheetEditorFactory.NoView`, con il testo del costruttore di `SheetEditor`;
- la partial di prova di `SheetEditor` in `supporto/test/Desktop/SmokeChecks.cs` (`SetGeometryForSmoke`, `WaitForAutomatic`, `VerifyWpf`, tutti
  senza fogli storici);
- i rami storici di `OpenSheet` (`tests/Desktop/OpenSheetChecks.cs`), di `ShellSheet` (`tests/Desktop/ShellSmoke.cs`), di `ShellSmokeDisplay` e di
  `ContrastAudit.AuditWaitEditor`;
- la scelta `--module-impl modulo=historic` del banco (`tests/Desktop/ModuleHostBench.cs`): ora è rifiutata con «archiviata», come già succedeva per
  ogni modulo MVVM nella Release.

`F3FigureChecks.cs` e `F3SchedulingChecks.cs` erano partial di `SheetEditor` solo per ospitare metodi statici: sono diventate classi statiche con lo
stesso nome del file.

`SheetImplementation.Historic` resta come valore dei moduli non elencati, che si rifiutano: domanda G1, `docs/refactoring/f5-15-revisione.md` §15.3.

### Sostituzione

- **Fogli.** Le viste XAML dei moduli, nella shell (`RegistrySheetFactory` dentro `ReleaseSheetFactory`) e nelle prove (`MvvmSheetEditor` da
  `SheetEditorFactory`).
- **File dei fogli.** `SheetFileExports` (`src/ANTHEA.Presentation`), attraverso `DesktopSheetExports`.

## Passi successivi

`MainWindow` (15 file partial di `X.Desktop/Wpf/`, 2 134 righe), i servizi della finestra storica (`WpfDesktopServices`, `DesktopServices`) e le
prove che la aprono vanno qui quando le suite elencate nella sezione 14.2 di `f5-13-shell.md` saranno passate sulla shell.

## Per rieseguire il codice storico

Fare `git revert` del commit di archiviazione. In alternativa riportare i file nella posizione originale con `git mv` e ripristinare i rami
elencati sopra come in 67eafba5.

## Secondo passo (24/10/2026): MainWindow

Tappa B dell'agente `f5-13-g3`, ramo `refactoring/f5-13-g3` da `refactoring/f5` 3e6c6f77. Dal 23/10 la Release apre la shell nuova
(`App.StartShell`); dal 24/10 nessuna prova apre più `MainWindow`: le suite contenitore girano sulla shell (`ShellSmokeWindow`, G6 compresa) e
`ui/check-contrast` pure (G2). Elenco completo, con l'ultimo commit che modifica ogni file e le impronte, in `registro.json`.

| Gruppo | File |
| --- | --- |
| finestra storica | `X.Desktop/Wpf/MainWindow.cs`, `MainWindow.Examples.cs`, `MainWindow.RetainingWall.cs`, `MainWindow.ShortReport.cs`, `MainWindow.Wiki.cs` |
| progetti della finestra storica | `X.Desktop/Wpf/ProjectCoverStatus.cs`, `ProjectHierarchy.cs`, `ProjectModels.cs`, `ProjectMovePreview.cs`, `ProjectPresentation.cs`, `ProjectReport.cs`, `ProjectRevisionNavigation.cs`, `ProjectSectionReorder.cs`, `ProjectSharing.cs`, `ProjectWorkspace.cs` |
| servizi di una finestra (F1.4) | `X.Desktop/Services/DesktopServices.cs` |
| partial di prova delle sei suite sostituite da `ui/shell-smoke-*` | `supporto/test/Desktop/ProjectSmokeChecks.cs`, `ProjectWorkspaceSmokeChecks.cs`, `ProjectHierarchySmokeChecks.cs`, `ProjectSharingSmokeChecks.cs`, `ProjectRevisionDeletionSmokeChecks.cs`, `ProjectRevisionNavigationSmokeChecks.cs`, `ProjectComparisonChecks.cs`, `ProjectCoverWarningSmokeChecks.cs`, `ProjectDragLayoutSmokeChecks.cs`, `DisplaySmokeChecks.cs`, `ProjectReportSmokeChecks.cs`, `MainWindow.Tests.cs` |
| prova esterna | `supporto/test/ConcreteShort.UiChecks/` (pulsante «Report short» nella barra di `MainWindow`, per riflessione) |
| frammenti | `tests/Desktop/ShellProjectCapture.MainWindow.cs`, `tests/Desktop/ShellSmokeReport.MainWindow.cs` (le partial di `MainWindow` delle due prove di confronto) |

Rami tolti senza archiviarli, perché sono parti di file che restano:
- `WpfDesktopServices.Create` (`X.Desktop/Wpf/WpfDesktopServices.cs`) e `DesktopComposition.WindowServices`; restano `WpfConfirmationService` e
  `WpfMessageService` della radice di composizione;
- la classe `TestServices` e la partial `MainWindow.Scripted` (`supporto/test/Desktop/TestServices.cs`); restano `ScriptedConfirmations` e
  `RethrowingMessages`, i servizi del banco della shell e dei fogli;
- i costruttori di prova di `MainWindow` e `MainWindow.SheetServices` (`tests/Desktop/ModuleHostBench.cs`), la partial `openSheet`
  (`tests/Desktop/OpenSheetChecks.cs`);
- i rami `MainWindow` di `WikiContextHelp.Open`, `CalculationHelpView.Show` e `WpfWikiNavigator.Show`; `WikiEditorial` usa
  `Anthea.Presentation.Shell.Navigation.WikiExamples`, la stessa copia;
- in `AppHarness.cs` il ramo `MainWindow` di `StartSmoke`, le sei bandiere delle suite sostituite e `StartInteractive`: `--interactive` apre la shell
  con le stesse coppie `--module-impl`; in `build/ci.ps1` le sei suite escono da `$smokes` e `ConcreteShort.UiChecks` dalle prove esterne.

Le prove che confrontavano con `MainWindow` confrontano con i suoi valori registrati (`tests/fixtures/f5-shell`): `ui/shell-capture` (la cartella
`mainwindow/` è la copia della cattura registrata), `ui/shell-smoke-project-report` (la chiamata registrata `ShellReferences.SectionReport`) e
`ui/check-shell-draft` (gli esempi della Wiki). `tests/ModelViewer.UiChecks` apre la shell della Release (`ProductShell`) al posto di `MainWindow`.
Le altre prove esterne usavano `MainWindow` solo come ancora dell'assembly: ora `typeof(X.Desktop.App)`.
