# Tracce della vista storica della palificata (F5.4)

Proposta F5, sezione 7.2: prima di migrare un modulo si cattura il comportamento della vista storica. Il riferimento F3
confronta i dati in forma canonica (chiavi ordinate) e non vede l'interfaccia. Queste tracce conservano invece, dopo
l'apertura e dopo ogni passo di uno scenario:

- i byte dei dati nell'ordine in memoria;
- il numero di `Modified`;
- il risultato (impronta SHA-256 e risultato selezionato);
- i testi della scheda (titolo, stato, indicatori, geometria, fonte, note);
- le righe del confronto, dei dettagli per palo e delle coordinate;
- i campi del generatore, delle direzioni, del basamento, delle opzioni e degli interassi;
- quello che riceve il disegno.

| Cosa | Dove |
| --- | --- |
| Scenari e formato | `tests/Shared/PileGroupTrace.cs` |
| Vista storica | `tests/Desktop/PileGroupHistoricTrace.cs` (`HorizontalPileGroupWorkspace` aperta da `SheetEditor`, fuori schermo) |
| ViewModel | `tests/ANTHEA.Presentation.Tests/PileGroupSheetChecks.cs` (`PileGroupSheetViewModel`, senza WPF) |

Gli scenari sono 11:
- `fixture-<id>`, uno per ognuna delle 8 fixture F3 della palificata: apertura, conferma, calcolo, conferma, riapertura,
  conferma (il percorso di `ui/f3-equivalence`);
- `modifiche-default`: diametro, generatore, disposizioni, testo non numerico, direzioni, basamento, opzioni, interassi
  per direzione, chiave opzionale svuotata, selezione nel confronto, attese del ricalcolo automatico;
- `modifiche-generica`: tabella delle coordinate (cella, riga nuova, riga tolta, modifica annullata, testo non numerico),
  passaggio a una disposizione generata e ritorno a «Generica»;
- `sola-lettura`: revisione in sola lettura, selezione nel confronto e calcolo.

## Cattura e controllo

- Catturate il 16/10/2026 sul codice di produzione di `f8ac25d3` (vista storica invariata da `5f29067b`) con:

  ```
  dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-pile-group-historic-capture <radice> <cartella>
  ```

  Due catture (`supporto/artefatti/refactoring/f5-4/storica-1` e `-2`) sono uguali byte per byte.
- La suite `ui/check-pile-group-historic` dello stadio `ui` ricattura la vista storica e la confronta con queste tracce,
  finché la vista storica esiste.
- `Presentation.Tests`, nello stadio `fast`, confronta le stesse tracce con il ViewModel.
- Le tracce non si rigenerano: una differenza della vista storica o del ViewModel si spiega e si registra.
