# Vista storica della risposta elastica del palo orizzontale

La cartella conserva la vista WPF storica della scheda «Risposta elastica · trave su molle» dei moduli `geo_palo_orizzontale` e
`geo_micropalo_orizzontale` (`ElasticPileWorkspace`) e dell'editor delle armature del palo (`PileReinforcementEditor` e la partial
`Clarity`), la prova delle sue tracce e la sonda storica di `ui/f3-scheduling` sul dimensionamento di un tratto. Sono state archiviate il
23 ottobre 2026, insieme alla scheda della capacità (`supporto/SUPERATI/ui-storica/palo-orizzontale`), quando la Release è passata alla
vista MVVM (passaggio dei moduli del refactoring F5, decisione Q11). I file non sono compilati: nessun progetto include questa cartella.
Sotto la cartella i percorsi riproducono quelli originali, relativi alla radice del repository. Origine, ultimo commit e impronte SHA-256
(file estratto su Windows con fine riga CRLF, e contenuto in Git con fine riga LF) sono in `registro.json`.

| File | Contenuto |
| --- | --- |
| `X.Desktop/Wpf/ElasticPileWorkspace.cs` | la scheda storica della risposta elastica |
| `X.Desktop/Wpf/PileReinforcementEditor.cs`, `PileReinforcementEditor.Clarity.cs` | l'editor storico delle armature (tratti, ipotesi, riepilogo) |
| `tests/Desktop/ElasticPileHistoricTrace.cs` | la prova delle tracce della scheda storica (suite `ui/check-palo-elastico-historic`) |
| `tests/ANTHEA.Desktop.Equivalence/F3PileSegmentChecks.cs` | la sonda di `ui/f3-scheduling` sull'editor storico (partial di `PileReinforcementEditor`) |

## Motivo

Lo stesso del palo orizzontale (`../palo-orizzontale/README.md`): dal 23/10/2026 la Release apre i due moduli con la vista MVVM, che
contiene anche la scheda elastica. Con la vista storica sono stati tolti i comandi `--check-palo-elastico-historic` e
`--check-palo-elastico-historic-capture` dell'harness UiTests e la suite `ui/check-palo-elastico-historic` di `build/ci.ps1`.

Restano in `X.Desktop/Wpf`, invariati: i disegni `ElasticPileDrawing` e `PileReinforcementDrawing` (Q3). `PileReinforcementEditor.SegmentColor`
è passato a `PileReinforcementDrawing.SegmentColor` (stessi colori; la voce della baseline F4 si è spostata con il codice). Resta compilata,
invariata, anche la classe `ElasticHistoricControls` della prova delle tracce, in `tests/Desktop/ElasticHistoricControls.cs`: i lettori dei
controlli servono alle tracce attraverso la vista nuova.

**Il verificatore c.a. non è toccato.** Nella vista nuova «Apri verificatore c.a.» mostra `PileConcreteVerifier` con il servizio dei dialoghi;
la sua vista `PileConcreteVerifierView` ospita ancora la `ConcreteWorkspace` storica della sezione c.a., che passa alla sezione c.a. nuova con
il passaggio di `str_palo` (F5.10). Nessun file archiviato qui serve a quel dialogo.

## Sostituzione

- **Vista.** `X.Desktop/Views/HorizontalPile` (`ElasticPileView`, `PileReinforcementView`, `PileSegmentDesignView` e gli altri dialoghi).
- **ViewModel.** `src/ANTHEA.Presentation/Modules/HorizontalPile/Elastic` (`ElasticPileViewModel`, `PileReinforcementViewModel`,
  `PileSegmentDesignViewModel`).
- **Prove.** Le tracce e i disegni congelati della scheda storica restano in `tests/fixtures/f5-elastico`. Li confrontano `Presentation.Tests`
  (con il ViewModel) e `ui/check-palo-orizzontale-view` (attraverso la vista nuova). `ui/f3-scheduling` usa
  `tests/ANTHEA.Desktop.Equivalence/F3PileSegmentMvvmSchedulingChecks.cs`; `ElasticPile.UiChecks` apre la vista nuova con `PileHost`.

## Per rieseguire la vista storica

Fare `git revert` del commit di archiviazione. In alternativa:
1. riportare i file nella posizione originale con `git mv`;
2. togliere `tests/Desktop/ElasticHistoricControls.cs`, che la prova storica ridefinisce, e ridare a `PileReinforcementEditor` il suo
   `SegmentColor` (o farlo chiamare `PileReinforcementDrawing.SegmentColor`);
3. ripristinare il comando dell'harness, la suite e la sonda di `F3SchedulingChecks`, come in `refactoring/f5` `db7ef68d`.
