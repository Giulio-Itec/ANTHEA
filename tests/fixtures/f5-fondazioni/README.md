# Tracce della vista storica delle fondazioni superficiali (F5.9b)

Proposta F5, sezione 7.2: prima di migrare un modulo si cattura il comportamento della vista storica. Il riferimento F3
confronta i dati in forma canonica (chiavi ordinate) e non vede l'interfaccia. Queste tracce conservano invece, dopo
l'apertura e dopo ogni passo di uno scenario:

- i byte dei dati nell'ordine in memoria;
- il numero di `Modified`, la sola lettura e le richieste del report;
- il risultato (impronta SHA-256) e lo stato;
- i campi nell'ordine della scheda: scelte con le voci, numeri come testo grezzo, opzioni, unità e visibilità;
- i gruppi apribili e gli input raccolti (`vista_fondazioni`), con il testo del pulsante;
- i sondaggi, il sondaggio scelto, le righe degli strati come le mostra la tabella (copia di presentazione, colore escluso) e
  il semispazio;
- i risultati: titolo, riepilogo, tabelle dei coefficienti e delle correzioni additive con le colonne, note e avvisi;
- le sei schede grafiche come dati: disegni della geometria e della stratigrafia sui dati del foglio, messaggio delle viste dei
  risultati oppure impronta dei grafici, testi delle tensioni e degli andamenti, sezione e impronta del meccanismo di rottura.

| Cosa | Dove |
| --- | --- |
| Scenari e formato | `tests/Shared/ShallowFoundationTrace.cs` |
| Vista storica (archiviata il 23/10/2026) | `supporto/SUPERATI/ui-storica/fondazioni/tests/Desktop/ShallowFoundationHistoricTrace.cs` (`FoundationWorkspace` aperta da `SheetEditor`, fuori schermo), non compilata |
| ViewModel | `tests/ANTHEA.Presentation.Tests/ShallowFoundationSheetChecks.cs` (senza WPF) |

Ogni passo si fa con i controlli, come l'utente: un controllo nascosto dalle regole della scheda, spento o protetto dalla
revisione in sola lettura non fa nulla. Gli scenari sono 15:
- `fixture-<id>`, uno per ognuna delle 11 fixture F3 delle fondazioni: apertura, conferma, calcolo, conferma, riapertura,
  conferma (il percorso di `ui/f3-equivalence`);
- `modifiche`: campi numerici, testo non numerico, regole di visibilità (lunghezza, momenti e Hy diversi da zero, compressibilità,
  densità relativa, falda, conferma A1, fattori di profondità, base ruvida), nome della combinazione vuoto, conferma tolta,
  confronto caratteristico, forme, calcestruzzo del catalogo e personalizzato, errori del calcolo mostrati nello stato;
- `gruppi-e-raccolta`: input raccolti e gruppi chiusi salvati nel foglio, apertura e chiusura dei gruppi e «Raccogli/Mostra
  input» senza perdere il risultato, sezione del meccanismo, report, modifica che azzera il risultato;
- `stratigrafia`: scelta del sondaggio, celle (numero, tipologia, γsat vuoto), strato aggiunto e tolto, semispazio, sondaggio
  aggiunto e tolto (domanda con risposta sì), rifiuto dell'ultimo sondaggio;
- `sola-lettura`: revisione in sola lettura; campi, scelte, opzioni, sondaggi e strati rifiutati; gruppi, «Raccogli input»,
  report e calcolo ammessi, come oggi.

## Cattura e controllo

- Catturate il 16/10/2026 sul codice di produzione di `615b6849` con:

  ```
  dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-fondazioni-historic-capture <radice> <cartella>
  ```

  Due catture (`supporto/artefatti/refactoring/f5-9b/traccia-1` e `-2`) sono uguali byte per byte.
- L'unica modifica alla vista storica è il parametro `confirm` di `DeleteSurvey` (vero dal pulsante): la traccia risponde sì
  alla domanda senza aprire la finestra di messaggio, come per `GeoEditor.DeleteSurvey`.
- La suite `ui/check-fondazioni-historic` dello stadio `ui` ricatturava la vista storica e la confrontava con queste tracce.
  È stata tolta il 23/10/2026 con l'archiviazione della vista storica (`supporto/SUPERATI/ui-storica/fondazioni`); `ui/check-fondazioni-view`
  confronta le tracce attraverso la vista XAML.
- `Presentation.Tests`, nello stadio `fast`, confronta le stesse tracce con il ViewModel.
- Le tracce non si rigenerano: una differenza della vista storica o del ViewModel si spiega e si registra.
