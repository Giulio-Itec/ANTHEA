# Tracce delle viste storiche dei materiali (F5.5)

Proposta F5, sezione 7.2: prima di migrare un modulo si cattura il comportamento della vista storica. Il riferimento F3
confronta i dati in forma canonica (chiavi ordinate) e non vede l'interfaccia. Queste tracce conservano invece, dopo
l'apertura e dopo ogni passo di uno scenario:

- i byte dei dati nell'ordine in memoria;
- i byte dell'anteprima (`PreviewData`): i materiali tengono le modifiche nella vista fino alla conferma, quindi i dati
  cambiano solo alla conferma, mentre l'anteprima mostra subito lo stato che il foglio scriverebbe;
- il numero di `Modified`;
- se il foglio è modificabile;
- calcestruzzo: classe, numeri, scelte (con le voci e lo stato della tolleranza di posa), opzioni, esposizione del menu ed
  esposizioni attive, testi delle esposizioni, proprietà e tabella, aderenza, classe minima, copriferro (con il colore
  d'errore), criterio mostrato, nota della tolleranza, composizione;
- acciaio: tipo e voci, campi con il loro blocco, diagramma scelto e se è attivo, riferimento, descrizione, fonte,
  risultati, stato (coerente o avviso) e serie del diagramma del materiale (impronta dei punti).

| Cosa | Dove |
| --- | --- |
| Scenari e formato | `tests/Shared/MaterialTrace.cs` |
| Viste storiche (archiviate il 23/10/2026) | `supporto/SUPERATI/ui-storica/materiali/tests/Desktop/MaterialHistoricTrace.cs` e `MaterialViewTrace.cs` (`Materiali.MaterialView` e `RebarMaterialView` aperte da `SheetEditor`, fuori schermo), non compilati |
| ViewModel | `tests/ANTHEA.Presentation.Tests/MaterialSheetChecks.cs` (senza WPF) |
| Viste nuove | `tests/Desktop/MaterialSheetViewChecks.cs` (`ui/check-materials-view`, attraverso i controlli delle viste XAML) |

Gli scenari sono 21, con i file `calcestruzzo-<nome>.txt` e `acciaio-<nome>.txt`:
- `fixture-<id>`, uno per ognuna delle 15 fixture F3 dei due moduli: apertura, conferma, calcolo, conferma, riapertura,
  conferma (il percorso di `ui/f3-equivalence`);
- `calcestruzzo-modifiche`: classe, numeri (anche non validi), criterio del copriferro, riduzioni EC2, i tre controlli di
  esecuzione con le loro tolleranze, elemento e controllo NTC, esposizioni, vita utile, getto, abrasione, superficie,
  aderenza, composizione e cemento, conferma, modifiche non confermate, opzione attivata e disattivata, «Calcola», modifiche
  che non cambiano nulla, riapertura;
- `calcestruzzo-modifiche-archivio`: archivio con tre esposizioni, cambio dell'esposizione e conferma;
- `calcestruzzo-sola-lettura`: revisione in sola lettura di un archivio non ancora normalizzato (la conferma scrive lo stato,
  come oggi);
- `acciaio-modifiche`: preset del catalogo, acciaio storico con εu e diagramma, coefficiente, «Personalizzato», campi validi e
  non validi, testo uguale a quello mostrato, riferimento, dati personalizzati che ritrovano un acciaio storico, ritorno al
  catalogo, «Calcola», εu sotto lo snervamento, riapertura;
- `acciaio-modifiche-catalogo-precedente`: B450A del catalogo precedente (DET-3) che diventa personalizzato alla prima
  modifica, poi riselezionato;
- `acciaio-sola-lettura`: revisione in sola lettura di un acciaio personalizzato.

## Cattura e controllo

- Catturate il 16/10/2026 sul codice di produzione di `b87c9c0f` (viste storiche invariate da `3c336992`; la sola modifica è
  `partial` su `RebarMaterialView`, per le prove) con:

  ```
  dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-materials-historic-capture <radice> <cartella>
  ```

  Due catture (`supporto/artefatti/refactoring/f5-5/storica-1` e `-2`) sono uguali byte per byte.
- La suite `ui/check-materials-historic` dello stadio `ui` ricatturava le viste storiche e le confrontava con queste tracce.
  È stata tolta il 23/10/2026 con l'archiviazione delle viste storiche (`supporto/SUPERATI/ui-storica/materiali`), quando la
  Release è passata alle viste MVVM.
- `Presentation.Tests`, nello stadio `fast`, confronta le stesse tracce con i ViewModel; `ui/check-materials-view` le confronta
  attraverso le viste XAML.
- Le tracce non si rigenerano: una differenza della vista storica o del ViewModel si spiega e si registra.
