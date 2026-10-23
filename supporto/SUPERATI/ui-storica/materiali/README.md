# Viste storiche dei materiali

La cartella conserva le viste WPF storiche delle schede «Calcestruzzo» (`mat_calcestruzzo`, `Materiali.MaterialView` con le sue parti e lo stile `Styles.xaml`) e «Acciaio per armature» (`mat_acciaio_armatura`, `RebarMaterialView`), l'autoverifica della vista storica del calcestruzzo e la prova delle loro tracce. Sono state archiviate il 23 ottobre 2026, quando la Release è passata alle viste MVVM (refactoring F5, passaggi della Release, agente f5-pass-geo; decisione Q11). I file non sono compilati: nessun progetto include questa cartella. Sotto la cartella i percorsi riproducono quelli originali, relativi alla radice del repository. Gli stessi dati sono in `registro.json`. L'icona del modulo dell'acciaio (`RebarIcon`, in fondo a `RebarMaterialView.cs`) resta in uso: è stata spostata senza modifiche in `X.Desktop/Wpf/RebarIcon.cs`.

| File | Origine | Ultimo commit che lo modifica |
| --- | --- | --- |
| `X.Desktop/Wpf/Materials/Bond.cs` | stesso percorso dalla radice del repository | `4240dfb6`, invariato fino a `refactoring/f5` `db7ef68d` |
| `X.Desktop/Wpf/Materials/ExposureSelector.cs` | stesso percorso dalla radice del repository | `0a7fa436`, invariato fino a `refactoring/f5` `db7ef68d` |
| `X.Desktop/Wpf/Materials/MaterialDetails.cs` | stesso percorso dalla radice del repository | `0a7fa436`, invariato fino a `refactoring/f5` `db7ef68d` |
| `X.Desktop/Wpf/Materials/MaterialReport.cs` | stesso percorso dalla radice del repository | `4240dfb6`, invariato fino a `refactoring/f5` `db7ef68d` |
| `X.Desktop/Wpf/Materials/MaterialState.cs` | stesso percorso dalla radice del repository | `46910aaa`, invariato fino a `refactoring/f5` `db7ef68d` |
| `X.Desktop/Wpf/Materials/MaterialView.cs` | stesso percorso dalla radice del repository | `0a7fa436`, invariato fino a `refactoring/f5` `db7ef68d` |
| `X.Desktop/Wpf/Materials/MinimumConcrete.cs` | stesso percorso dalla radice del repository | `4240dfb6`, invariato fino a `refactoring/f5` `db7ef68d` |
| `X.Desktop/Wpf/Materials/MixAutomation.cs` | stesso percorso dalla radice del repository | `99fd0ad6`, invariato fino a `refactoring/f5` `db7ef68d` |
| `X.Desktop/Wpf/Materials/Styles.xaml` | stesso percorso dalla radice del repository | `4240dfb6`, invariato fino a `refactoring/f5` `db7ef68d` |
| `X.Desktop/Wpf/RebarMaterialView.cs` | stesso percorso dalla radice del repository | `0a7fa436`, invariato fino a `refactoring/f5` `db7ef68d` |
| `tests/Desktop/MaterialHistoricTrace.cs` | stesso percorso dalla radice del repository | `3285365f`, invariato fino a `refactoring/f5` `db7ef68d` |
| `tests/Desktop/MaterialViewTrace.cs` | stesso percorso dalla radice del repository | `3285365f`, invariato fino a `refactoring/f5` `db7ef68d` |
| `supporto/test/Materiali/MaterialViewChecks.cs` | stesso percorso dalla radice del repository | `0a7fa436`, invariato fino a `refactoring/f5` `db7ef68d` |

Impronte SHA-256 del file estratto su Windows, con fine riga CRLF:
- `X.Desktop/Wpf/Materials/Bond.cs`: `a9a3374a463aa68940881358f290a54310b267e6660cfc346cf9a283906657a3`;
- `X.Desktop/Wpf/Materials/ExposureSelector.cs`: `9ec0b08d8a0e4612a7cb445603469f18115ea8a4d1f43dc60d83dc40035ac0f4`;
- `X.Desktop/Wpf/Materials/MaterialDetails.cs`: `307fb9751da59865532c4bca0ca4dda5492f3f4f2d9a0f3a231d0b1687ed0c22`;
- `X.Desktop/Wpf/Materials/MaterialReport.cs`: `b8fefbee1e4b714379c78086ad64b8bff8dd91c758894c16d70ff25c38a9dde3`;
- `X.Desktop/Wpf/Materials/MaterialState.cs`: `6d39704fcc5bc3cb95d2771dbbc585e3bc3277286392ebbdf1ee46f3563b4164`;
- `X.Desktop/Wpf/Materials/MaterialView.cs`: `7fbf4aa0a0ce8f51bcc8bc63e4ad138c7f87c6b26d6c7a429a4ac5a81c778975`;
- `X.Desktop/Wpf/Materials/MinimumConcrete.cs`: `458b053725e066673caa2404091bf9b6df6d06813b968fccff306139ad5389df`;
- `X.Desktop/Wpf/Materials/MixAutomation.cs`: `422786a4afe85cb7c8a22b304b9703474875079bdcb33789a2ec6e9c1a6ec045`;
- `X.Desktop/Wpf/Materials/Styles.xaml`: `e9ed72d51d7fcd04b5c7aa659914e5d6246e0d571d623deaa499c7e9265bb180`;
- `X.Desktop/Wpf/RebarMaterialView.cs`: `0a784c2819c62e089131971796048e1f15b14ab3299ddafd75af6300c5db53d9`;
- `tests/Desktop/MaterialHistoricTrace.cs`: `46aed394b2f154c6df9093138a78a7cfbb260a35a567b6cd5a562312ff57380c`;
- `tests/Desktop/MaterialViewTrace.cs`: `ab8a6597d063fad4c7dc2cd0a1e8cd26acf87af395569151db2efc63291a9e15`;
- `supporto/test/Materiali/MaterialViewChecks.cs`: `0e6e40649f37adc4d23538f0ef6265e80acf14a1a0409ff54846453d87420556`.

Il repository conserva i testi con fine riga LF. Le impronte del contenuto in Git sono queste:
- `X.Desktop/Wpf/Materials/Bond.cs`: `f40de489584fd52937a82d1e0e0321b2da38953d99a9eece3b16402ea226d0ad`;
- `X.Desktop/Wpf/Materials/ExposureSelector.cs`: `8ddd322047b7262d8efdab6dfa90e46be7763074d14c7d96c9b27e69e1c10e00`;
- `X.Desktop/Wpf/Materials/MaterialDetails.cs`: `1da0829426b5b40703971672aa6252e405e36fa1a8ee2774bc81667b7f508563`;
- `X.Desktop/Wpf/Materials/MaterialReport.cs`: `f1619b71a26b5ec16c12e050a44933c1f9c382f0b9ed1109084547e13d3ca6fa`;
- `X.Desktop/Wpf/Materials/MaterialState.cs`: `440d713ba6bb3f845d377332f5469beffece257454a3267e930b27be62764836`;
- `X.Desktop/Wpf/Materials/MaterialView.cs`: `6a2ec14734ba14228345e7100c3b5dfe12469c02c3fd53a863bfc0f12e9f22f7`;
- `X.Desktop/Wpf/Materials/MinimumConcrete.cs`: `0a2809163dbfdf75c0578a22739a0c40479a9d5afc0fbf5b0297151abfd5e442`;
- `X.Desktop/Wpf/Materials/MixAutomation.cs`: `4fe31e0d0e50659f43325aa8deb33193abb968ca76aca9228209469606de235b`;
- `X.Desktop/Wpf/Materials/Styles.xaml`: `9a33cf247f3acf945622d0a271c4c53d164b0441587ec70ffe6816b2f6f1443c`;
- `X.Desktop/Wpf/RebarMaterialView.cs`: `6b580d945527a76e3f44637a376073ccb4ebd802fe4dc51072ab9b91793835b8`;
- `tests/Desktop/MaterialHistoricTrace.cs`: `3846588a9583063485e4fb680103d856eae7ea0c9df32ba2925365ee6a8a5888`;
- `tests/Desktop/MaterialViewTrace.cs`: `e1d8f1a00b5380d16fafac886d9fc5e95dbbbc292cd23acf7da6ed90eab48b70`;
- `supporto/test/Materiali/MaterialViewChecks.cs`: `563b00c3101dc547ae6b70c6f43b446288e2a9ef1f746f378439fcb71dc9f113`.

## Motivo

L'utente ha provato le viste nuove nella versione portatile e il 23/10/2026 ha chiesto di finire il refactoring delle viste («Moduli ora, shell dopo»: `docs/refactoring/decisioni.md` e `ANTHEA-f5-ripresa/istruzioni-agenti/prompt-passaggi-23-10-comune.md`). Da allora la Release apre i due moduli con le viste MVVM (`X.Desktop/Hosting/ModuleImplementations.cs`) e le viste storiche non sono più raggiungibili.
Con le viste sono stati tolti:
- i rami dei materiali di `X.Desktop/Wpf/SheetEditor.cs` (campi `materials` e `rebarMaterial`, costruttore, anteprima, `Commit`, `CalculateAsync`, `ExportReport`);
- i rami storici dei materiali delle prove (`OpenSheet`, `SteelSmoke`, `ShellSheet` in `tests/Desktop`);
- il comando `--check-materials-historic[-capture]` dell'harness UiTests e la suite `ui/check-materials-historic` di `build/ci.ps1`;
- il passo autonomo `mvvm-mat_calcestruzzo`/`mvvm-mat_acciaio_armatura` di `--check-contrast` (`AuditMvvmMaterials`): i passi dei moduli mostrano ora le viste nuove;
- le suite `ui/smoke-materials-mvvm`, `ui/smoke-hierarchy-mvvm`, `ui/smoke-sharing-mvvm`, `ui/smoke-steel-mvvm`, `ui/shell-smoke-hierarchy-mvvm` e `ui/shell-smoke-sharing-mvvm`, uguali alle suite normali.

## Sostituzione

- **Viste.** `X.Desktop/Views/Materials` (`ConcreteMaterialSheetView`, `RebarMaterialSheetView`), ospitate da `X.Desktop/Hosting/MvvmSheetEditor.cs`.
- **ViewModel.** `src/ANTHEA.Presentation/Modules/Materials`.
- **Autoverifica del calcestruzzo** (`MaterialView.Check`, `ui/smoke-materials`): `ConcreteMaterialSmoke.SelfCheck` in `tests/Desktop/MaterialSmokeChecks.cs`, sulla vista nuova; `supporto/test/Shared/DurabilityReferenceChecks.cs` resta.
- **Prove.** Le tracce congelate delle viste storiche restano in `tests/fixtures/f5-materiali/storica`. Le confrontano `Presentation.Tests` (con i ViewModel) e `ui/check-materials-view` (attraverso le viste nuove), che confronta anche esportazioni, aiuto, nomi di automazione e sola lettura con i valori registrati delle viste storiche.

## Per rieseguire la vista storica

Fare `git revert` del commit di archiviazione. In alternativa: riportare i file nella posizione originale con `git mv` (e `RebarIcon` di nuovo in fondo a `RebarMaterialView.cs`), poi ripristinare i rami dei materiali di `SheetEditor` e delle prove, il comando dell'harness, la suite e la riga `../supporto/test/Materiali/*.cs` di `X.Desktop.csproj`, come in `db7ef68d`.
