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
