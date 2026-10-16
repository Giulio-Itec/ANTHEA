# Tracce della vista storica della stratigrafia (F5.6b)

Proposta F5, sezione 7.2: prima di migrare un modulo si cattura il comportamento della vista storica. Il riferimento F3
confronta i dati in forma canonica (chiavi ordinate) e non vede l'interfaccia. Queste tracce conservano invece, dopo
l'apertura e dopo ogni passo di uno scenario:

- i byte dei dati nell'ordine in memoria (l'ordine delle scritture: una chiave nuova va in fondo all'oggetto);
- il numero di `Modified`;
- i messaggi e le domande del passo, con la risposta, e l'eccezione del passo (la conferma di un documento non valido);
- il pulsante dell'importazione IA (presente, spento, suggerimento);
- i campi generali e quelli dello strato selezionato, nell'ordine della scheda: etichetta, valore, voci delle scelte, se
  l'utente li può usare (sola lettura, opzione «indefinito» solo sull'ultimo strato);
- i pulsanti degli strati, l'elenco degli strati e lo strato selezionato;
- la nota dei minimi assenti, lo stato (avvisi, dati validi o errore da correggere) e il profilo (disegnato, selezione);
- il trasferimento del terreno avviato dalla scheda: modulo e dati passati al dialogo, numero dei fogli del progetto, ed
  esito (copia in un foglio nuovo con modulo, nome e dati; sostituzione del terreno; nessuno, con il testo del dialogo).

Gli identificativi creati durante lo scenario (strato nuovo o duplicato, copia di una stratigrafia) cambiano a ogni corsa:
diventano `<nuovo-n>` nell'ordine della prima comparsa. Quelli delle fixture restano come sono.

| Cosa | Dove |
| --- | --- |
| Scenari e formato | `tests/Shared/StratigraphyTrace.cs` |
| Vista storica | `tests/Desktop/StratigraphyHistoricTrace.cs` (`StratigraphyWorkspace` aperta da `SheetEditor` in una finestra fuori schermo) |
| ViewModel | `tests/ANTHEA.Presentation.Tests/StratigraphySheetChecks.cs` (senza WPF) |

Gli scenari sono 15:
- `fixture-<id>`, uno per ognuna delle 5 fixture F3 del modulo: apertura, conferma, calcolo, conferma, riapertura,
  conferma (il percorso di `ui/f3-equivalence`);
- `modifiche-generali`: importazione (spenta), nome (anche vuoto, con la conferma che fallisce), descrizione su due righe,
  quota (anche non valida), scelta dei parametri, stati della falda e profondità (mancante, negativa), «Calcola», riapertura;
- `modifiche-strati`: tutti i campi dello strato, famiglia, comportamento, qualificazione, colore e spessore non validi,
  parametri fuori campo, l'estensione indefinita dell'ultimo strato (e il suo spessore che torna a 1), riapertura;
- `azioni-strati`, `azioni-indefinito`: «+ Strato», «Duplica», «Elimina», «↑», «↓», con i messaggi di errore (primo e ultimo
  strato, strato indefinito, ultimo strato rimasto);
- `minimi`: minimi assegnati, scritti, rimozione rifiutata e confermata (anche la risposta «no» solleva `Modified`, come oggi);
- `testi`: comportamento scelto in uno strato che non ha la chiave (scritta in fondo all'oggetto), numeri con la virgola;
- `trasferimento`, `trasferimento-minimi`: invio a fogli nuovi dei moduli di verifica (medi e minimi) e a una stratigrafia
  nuova, errori del dialogo, sostituzione del terreno del foglio con il suo profilo, conferma che fallisce prima del dialogo;
- `trasferimento-progetto`: il terreno di un altro foglio del progetto sostituisce quello del foglio;
- `sola-lettura`: revisione precedente, ogni modifica rifiutata, la selezione degli strati resta possibile.

Come l'utente, ogni passo agisce sui controlli: un controllo spento o protetto dalla revisione non fa nulla. I messaggi
della vista (`MessageBox`) li legge e li chiude `MessageBoxHook`, fuori schermo, con la risposta dello scenario; il dialogo
del terreno (`SoilTransferDialog`, condiviso dai moduli dei terreni) è guidato con i suoi controlli mentre è aperto.

## Cattura e controllo

- Catturate il 17/10/2026 sul codice di produzione di `5d76779c` (vista storica invariata da `92613e60`; la sola modifica è
  `partial` su `StratigraphyWorkspace`, per le prove) con:

  ```
  dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-stratigrafia-historic-capture <radice> <cartella>
  ```

  Due catture (`supporto/artefatti/refactoring/f5/f5-6b/storica-1` e `-2`) sono uguali byte per byte.
- La suite `ui/check-stratigrafia-historic` dello stadio `ui` ricattura la vista storica e la confronta con queste tracce,
  finché la vista storica esiste.
- `Presentation.Tests`, nello stadio `fast`, confronta le stesse tracce con il ViewModel.
- Le tracce non si rigenerano: una differenza della vista storica o del ViewModel si spiega e si registra.
