# Vista storica del palo e del micropalo orizzontale

La cartella conserva la vista WPF storica della scheda «Dati comuni e capacità · Broms / stratificato» dei moduli `geo_palo_orizzontale`
e `geo_micropalo_orizzontale` (`HorizontalWorkspace` e le sue partial, l'editor del terreno della riga `HorizontalSoilEditor`), la prova
delle sue tracce e le partial di prova di `ui/smoke-horizontal` sulla vista storica. Sono state archiviate il 23 ottobre 2026, quando la
Release è passata alla vista MVVM (passaggio dei moduli del refactoring F5, decisione Q11), insieme alla scheda della risposta elastica
(`supporto/SUPERATI/ui-storica/palo-elastico`). I file non sono compilati: nessun progetto include questa cartella. Sotto la cartella i
percorsi riproducono quelli originali, relativi alla radice del repository. Origine, ultimo commit e impronte SHA-256 (file estratto su
Windows con fine riga CRLF, e contenuto in Git con fine riga LF) sono in `registro.json`.

| File | Contenuto |
| --- | --- |
| `X.Desktop/Wpf/HorizontalWorkspace.cs` | la scheda storica (schede, forme, stratigrafie, calcolo automatico) |
| `X.Desktop/Wpf/HorizontalPresentation.cs`, `HorizontalMaterials.cs`, `HorizontalExplanations.cs` | partial di `HorizontalWorkspace` |
| `X.Desktop/Wpf/HorizontalSoilEditor.cs` | l'editor del terreno di una riga della tabella degli strati |
| `X.Desktop/Wpf/HorizontalChs.cs`, `HorizontalDiagrams.cs` | copia dei file interi; in `X.Desktop/Wpf` restano i soli disegni `ChsDrawing` e `HorizontalDiagrams`, usati dalla vista nuova e dalle figure del report |
| `tests/Desktop/HorizontalPileHistoricTrace.cs` | la prova delle tracce della vista storica (suite `ui/check-palo-orizzontale-historic`) |
| `supporto/test/Desktop/HorizontalLayoutChecks.cs`, `HorizontalChsChecks.cs`, `HorizontalStratifiedSmokeChecks.cs` | partial di prova di `HorizontalWorkspace` (`ui/smoke-horizontal` sulla vista storica) |

## Motivo

Il 23/10/2026 l'utente ha chiesto di finire il refactoring delle viste e ha scelto «Moduli ora, shell dopo»: i moduli passano alla vista
nuova nella Release. Da allora `X.Desktop/Hosting/ModuleImplementations.cs` apre i due moduli con la vista MVVM e la vista storica non è più
raggiungibile. Con lei sono stati tolti:
- i rami del palo orizzontale di `X.Desktop/Wpf/SheetEditor.cs` (campo, `Result`, `Busy`, costruttore con il parametro dell'analisi
  elastica, `Dispose`, `Commit`, `CalculateAsync`, `BuildReportDocument`);
- la partial di prova di `SheetEditor` in `supporto/test/Desktop/HorizontalSmokeChecks.cs` (`MainWindow.SmokeHorizontal` resta) e i rami
  storici di `OpenSheet` (`tests/Desktop/OpenSheetChecks.cs`);
- i comandi `--check-palo-orizzontale-historic` e `--check-palo-orizzontale-historic-capture` dell'harness UiTests e la suite
  `ui/check-palo-orizzontale-historic` di `build/ci.ps1`;
- i confronti con la vista storica di `ui/check-palo-orizzontale-view` e il controllo «la Release resta storica».

Due parti generiche della prova delle tracce restano compilate, invariate, in `tests/Desktop/HorizontalPileDrawingRecord.cs` (la classe
mantiene il nome `HorizontalPileHistoricTrace` per i chiamanti): `Record`, la lista di visualizzazione di un disegno, e `DrawingsReference`.

## Sostituzione

- **Vista.** `X.Desktop/Views/HorizontalPile` (`HorizontalPileSheetView`, dialoghi dei risultati e del terreno), ospitata da
  `X.Desktop/Hosting/MvvmSheetEditor.cs`.
- **ViewModel.** `src/ANTHEA.Presentation/Modules/HorizontalPile` (`HorizontalPileSheetViewModel`, `HorizontalSoilRowViewModel`).
- **Derivazioni.** `src/ANTHEA.Application/Presentation/HorizontalPilePresentation.cs`, `HorizontalSheetPresentation.cs`.
- **Prove.** Le tracce e i disegni congelati della vista storica restano in `tests/fixtures/f5-palo-orizzontale`. Li confrontano
  `Presentation.Tests` (con il ViewModel) e `ui/check-palo-orizzontale-view` (attraverso la vista nuova). `ui/smoke-horizontal` e la parte
  orizzontale di `ui/smoke` girano su `HorizontalPileSmoke` (`tests/Desktop/HorizontalPileSmokeChecks.cs`).

## Per rieseguire la vista storica

Fare `git revert` del commit di archiviazione. In alternativa:
1. riportare i file nella posizione originale con `git mv` (per `HorizontalChs.cs` e `HorizontalDiagrams.cs`, sostituire i file ridotti);
2. togliere `tests/Desktop/HorizontalPileDrawingRecord.cs`, che la prova storica ridefinisce;
3. ripristinare i rami di `SheetEditor`, il comando dell'harness e la suite, come in `refactoring/f5` `db7ef68d`.
