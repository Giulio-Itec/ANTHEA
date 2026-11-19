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
| Vista storica (archiviata il 23/10/2026) | `supporto/SUPERATI/ui-storica/stratigrafia/tests/Desktop/StratigraphyHistoricTrace.cs` (`StratigraphyWorkspace` aperta da `SheetEditor` in una finestra fuori schermo), non compilata |
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
- 18/11/2026, voce TRACCE-VISTE-DOMANDA5-20261118 (ANTHEA 1.19.4, domanda 5 della voce 12h, con l'OK dell'utente): un numero salvato come numero JSON si mostra nel campo con la virgola. In 8 tracce sono sostituite solo le 320 righe che differiscono per il separatore decimale, prese dalla vista XAML (full `20261118-051529-1194-full-az-a3042f1f`); ogni altra riga resta. Il ViewModel scrive ora nella traccia il testo che il campo mostra (`EditText`), come la vista.
- 18/11/2026, voce MERGE-GIULIO-RIFERIMENTI-20261118 (ANTHEA 1.19.4, merge di main 630028b8, con l'OK dell'utente): uno strato nuovo prende il
  colore `#F4C95D` e un duplicato un colore nuovo (commit `8796bae8`); nella copia al palo verticale il peso proprio sfavorevole è
  1,35. In 4 tracce cambiano 55 righe, prese dal ViewModel (`Presentation.Tests`, corsa `20261118-151110-1194-p2-full-264ce17d`).
