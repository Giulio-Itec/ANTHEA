# Vista storica della palificata orizzontale

La cartella conserva la vista WPF storica della scheda «Palificata orizzontale» (modulo `geo_efficienza_orizzontale`) e la
prova delle sue tracce. Sono state archiviate il 17 ottobre 2026, quando la Release è passata alla vista MVVM (refactoring F5.4,
decisione Q11). I file non sono compilati: nessun progetto include questa cartella. Sotto la cartella i percorsi riproducono
quelli originali, relativi alla radice del repository. Gli stessi dati sono in `registro.json`.

| File | Origine | Ultimo commit che lo modifica |
| --- | --- | --- |
| `X.Desktop/Wpf/HorizontalPileGroupWorkspace.cs` | stesso percorso dalla radice del repository | `ceb344d0`, invariato fino a `refactoring/f5` `7979f79d` |
| `tests/Desktop/PileGroupHistoricTrace.cs` | stesso percorso dalla radice del repository | `b71a5221`, invariato fino a `refactoring/f5` `7979f79d` |

Impronte SHA-256 del file estratto su Windows, con fine riga CRLF:
- `HorizontalPileGroupWorkspace.cs`: `cf146330ef3ac3a18533d4d68d42c93c7113a0f41730c1823608019802f3cc9f`;
- `PileGroupHistoricTrace.cs`: `270252f9a795a830abb02b98d74ea36bb2d0e5812ce1820a23b730ab9814ce43`.

Il repository conserva i testi con fine riga LF. Le impronte del contenuto in Git sono queste:
- `HorizontalPileGroupWorkspace.cs`: `ab8e3cfed978206e180c9f3a68c1456a8ded17796beef84030030ff4af3dfd27`;
- `PileGroupHistoricTrace.cs`: `86bbe237f79d1d2fa658895a1cfa1c4d19ecf7da83efdf497b34909e4c6623f8`.

## Motivo

Al punto di controllo F5.4 del 17/10/2026 l'utente ha accettato la vista XAML della palificata (`docs/refactoring/decisioni.md`,
sezione «17 ottobre (mattina)»). Da allora la Release apre il modulo con la vista MVVM (`X.Desktop/Hosting/ModuleImplementations.cs`)
e la vista storica non è più raggiungibile. Con lei sono stati tolti:
- i rami della palificata di `X.Desktop/Wpf/SheetEditor.cs`;
- i comandi `--check-pile-group-historic` e `--check-pile-group-historic-capture` dell'harness UiTests;
- la suite `ui/check-pile-group-historic` di `build/ci.ps1`.

## Sostituzione

- **Vista.** `X.Desktop/Views/PileGroup`, ospitata da `X.Desktop/Hosting/MvvmSheetEditor.cs`. Il disegno `X.Desktop/Wpf/PileGroupPlan.cs`
  resta invariato.
- **ViewModel.** `src/ANTHEA.Presentation/Modules/PileGroup`.
- **Derivazioni.** `src/ANTHEA.Application/Presentation/PileGroupPresentation.cs`.
- **Prove.** Le tracce congelate della vista storica restano in `tests/fixtures/f5-palificata/storica`. Le confrontano due prove:
  - `Presentation.Tests`, con il ViewModel;
  - `ui/check-pile-group-view`, attraverso i controlli della vista nuova.

## Per rieseguire la vista storica

Fare `git revert` del commit di archiviazione. In alternativa:
1. riportare i due file nella posizione originale con `git mv`;
2. ripristinare i rami della palificata di `SheetEditor`, il comando dell'harness e la suite, come in `7979f79d`.
