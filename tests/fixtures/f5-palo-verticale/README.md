# Tracce della vista storica del palo e del micropalo verticale (F5.6)

Proposta F5, sezione 7.2: prima di migrare un modulo si cattura il comportamento della vista storica. Il riferimento F3
confronta i dati in forma canonica (chiavi ordinate) e non vede la scheda. Queste tracce conservano invece, dopo
l'apertura e dopo ogni passo di uno scenario:

- i byte dei dati nell'ordine in memoria;
- il numero di `Modified`, la sola lettura e la possibilità di esportare (risultato presente e nessun calcolo in corso);
- il risultato (impronta SHA-256), lo stato e gli avvisi;
- i campi dei tre form (generali, efficienza, coefficienti della normativa): etichetta, valore, voci, attivo, attenuato,
  visibile; il testo dell'efficienza;
- la tabella della verifica;
- le stratigrafie: righe come le mostra la tabella (copia di presentazione di ogni strato, con i valori calcolati; il
  colore dello strato è escluso, perché nella vista nuova è un token, `Swatch.Soil1`–`Swatch.Soil6`), stratigrafia
  selezionata e profilo disegnato;
- il grafico della capacità (testi, quota, serie visibili con colore, tratteggio e impronta dei punti), le curve con i
  valori a L, il grafico di riferimento (Nq o abachi, con i punti);
- le tabelle di «Tabelle e dettagli» e il JSON dei risultati.

| Cosa | Dove |
| --- | --- |
| Scenari e formato | `tests/Shared/VerticalPileTrace.cs` |
| Vista storica | `tests/Desktop/VerticalPileHistoricTrace.cs` (`SheetEditor` con `GeoEditor`, fuori schermo) |
| ViewModel | `tests/ANTHEA.Presentation.Tests/VerticalPileSheetChecks.cs` (senza WPF) |

Ogni passo è quello che fa l'utente con un controllo: un controllo che l'utente non può usare (spento, nascosto o protetto
dalla revisione in sola lettura) non fa nulla. Il servizio di calcolo è quello del modulo con una barriera
(`HeldModuleService`), che trattiene il calcolo sullo sfondo quando lo scenario lo chiede.

Gli scenari sono 18:
- `palo-<id>` e `micropalo-<id>`, uno per ognuna delle 13 fixture F3 dei due moduli: apertura, conferma, calcolo,
  conferma, riapertura, conferma, attesa del ricalcolo automatico (il percorso di `ui/f3-equivalence` più il ricalcolo
  dopo la riapertura);
- `modifiche-palo`: campi, scelte e opzioni dei tre form (tipo di palo con il γb della tecnologia, falda, verticali
  indagate, metodi di efficienza), «Reset» della normativa, testo non numerico, strato di un archivio senza
  `laterale_attiva`, viste della capacità, curve, «Tutte»/«Nessuna», tabelle;
- `stratigrafie-palo`: celle degli strati (scelta, casella, testo, Cu vuota), strato aggiunto e tolto, stratigrafia
  aggiunta, copiata in un'altra e in una nuova, tolta dalla scheda e con «−»;
- `modifiche-micropalo`: iniezione IGU → IRS (α degli strati ricalcolati), pressione, profilo CHS, punta, inclinazione,
  inizio dell'aderenza oltre la punta, terreno (α iniziale), α, laterale, strati e stratigrafie;
- `sola-lettura`: revisione in sola lettura, modifiche rifiutate dai controlli, curve, viste, tabelle e calcolo ammessi;
- `calcolo-sullo-sfondo`: ricalcolo automatico trattenuto e superato da una modifica, pubblicazione della sola revisione
  corrente, «Calcola» durante un calcolo, chiusura e riapertura durante un calcolo.

## Difetto della vista storica trovato con le tracce

Nel micropalo con un risultato, il cambio dell'iniezione (IGU ↔ IRS) ricostruisce le stratigrafie mentre il risultato
dell'altra iniezione è ancora pubblicato: `VerticalPileCharts.Reference` cerca la curva dell'abaco della nuova iniezione e
fallisce («Sequence contains no matching element»). In produzione l'errore va nel registro e in un messaggio; il foglio resta
con gli α già riscritti, senza `Modified` e senza ricalcolo. Lo scenario `modifiche-micropalo` cambia quindi l'iniezione
quando il risultato non c'è (pressione vuota). Il difetto è riportato in `docs/refactoring/f5-6-palo-verticale.md`.

## Cattura e controllo

- Catturate il 16/10/2026 sul codice di produzione di `0abe1ac2` (vista storica invariata da `3c336992`) con:

  ```
  dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-palo-verticale-historic-capture <radice> <cartella>
  ```

  Due catture (`supporto/artefatti/refactoring/f5-6/storica-1` e `-2`) sono uguali byte per byte: 18 scenari, 16178 righe.
- La suite `ui/check-palo-verticale-historic` dello stadio `ui` ricattura la vista storica e la confronta con queste
  tracce, finché la vista storica esiste.
- `Presentation.Tests`, nello stadio `fast`, confronta le stesse tracce con il ViewModel.
- Le tracce non si rigenerano: una differenza della vista storica o del ViewModel si spiega e si registra.
- 27/10/2026, voce PO-TESTA-01 (1.14.3, testa del palo sotto il piano campagna): il campo nuovo «Profondità della testa sotto il
  piano campagna d» dei dati generali del palo e del micropalo, che mostra «0» senza scriverlo. Nelle 18 tracce sono inserite solo le
  righe `campo profondita_testa "Profondità testa sotto il p.c." = "0"` (257), dalla vista XAML (`ui/check-palo-verticale-view`, corsa
  `supporto/artefatti/1143-teste/verticale-2`); nessun'altra riga cambia. `ui/check-palo-verticale-view` aggiunge l'etichetta «?» del
  campo a quelle registrate della vista storica.
