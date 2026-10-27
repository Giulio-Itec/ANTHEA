# Tracce della vista storica dei muri di sostegno (F5.9)

Proposta F5, sezione 7.2: prima di migrare un modulo si cattura il comportamento della vista storica. Il riferimento F3
confronta i dati in forma canonica (chiavi ordinate) e non vede l'interfaccia. Queste tracce conservano invece lo stato visto
dall'utente: per intero dopo l'apertura, poi dopo ogni passo di uno scenario solo le voci cambiate («~» testo nuovo, «+» voce
nuova, «-» voce sparita). Le voci sono, nell'ordine:

- i dati: l'ordine delle chiavi e una voce per chiave, nell'ordine in memoria (byte compatti);
- il numero di `Modified`, la sola lettura, l'occupato e le richieste alla finestra (report, terreno, sezioni c.a. con nome e
  impronta dei dati inviati);
- il risultato (impronta), la stabilità globale (dal calcolo o indipendente, numero di casi) e lo stato;
- la tipologia con le voci, i pulsanti «Sisma», «Stabilità globale» e «Calcola armature», la scheda mostrata, combinazioni,
  vista, elemento, filtro, combinazioni globali, interruttori dei disegni, riepilogo;
- ogni campo di ogni form, nell'ordine della scheda: tipo, etichetta, simbolo, unità, valore (testo grezzo), voci, spento,
  attenuato, nascosto, nota; la visibilità di ogni form e lo stato delle schede;
- le tabelle degli input (colonne, selezione e l'oggetto di ogni riga: i dati, oppure la copia di presentazione per la matrice,
  la matrice globale e gli strati globali);
- i testi degli input (strati, matrice, matrice globale, guida globale con il suggerimento, ricerca, sisma, copriferro) e gli
  accelerogrammi;
- i pannelli dei risultati come blocchi (testi, tabelle con colonne, numero di righe, impronta e prime tre righe, pulsanti,
  gruppi): spinte, «Riepilogo e dettagli» del filtro scelto, proposta delle armature;
- la sezione selezionata e il pulsante «Apri sezione in c.a.»;
- i disegni come dati: combinazione, risultato, azione, strato e riga delle spinte selezionati, interruttori, modo ed elemento
  dei diagrammi, disegno e anteprima della stabilità globale;
- la finestra «Valori di calcolo…», quando è aperta: stato, messaggio, combinazioni, tabella dei valori e valori derivati.

Gli identificativi delle azioni (32 cifre esadecimali, nuovi a ogni azione aggiunta) sono scritti `<ID-n>` nell'ordine di
apparizione nello scenario, prima delle impronte.

| Cosa | Dove |
| --- | --- |
| Scenari e formato | `tests/Shared/RetainingWallTrace.cs` |
| Vista storica (archiviata il 23/10/2026) | `supporto/SUPERATI/ui-storica/muri/tests/Desktop/RetainingWallHistoricTrace.cs` (`RetainingWallWorkspace` creata come la crea `SheetEditor`, fuori schermo), non compilata |
| Vista XAML | `tests/Desktop/RetainingWallViewChecks.cs` (`ui/check-muri-view`) |
| Valori registrati della vista storica | `vista-storica.json`: dati e risultato dopo il calcolo, testi dei CSV, combinazioni mostrate, pagine della distinta, etichette «?», nomi di automazione, sola lettura |
| ViewModel | `tests/ANTHEA.Presentation.Tests/RetainingWallSheetChecks.cs` (senza WPF) |

Ogni passo si fa con i controlli, come l'utente: un controllo nascosto, spento o protetto dalla revisione in sola lettura non
fa nulla. Il calcolo passa da `HeldModuleService`, che può trattenere il motore. Gli scenari sono 24:
- `fixture-<id>`, uno per ognuna delle 16 fixture F3 dei muri: apertura, conferma, calcolo, conferma, riapertura, conferma,
  attesa del ricalcolo (il percorso di `ui/f3-equivalence`);
- `modifiche-ingressi`: geometria (anche un valore non numerico), strati di monte (cella, aggiunta, spostamento, eliminazione),
  colonna di valle (altezza libera, passiva, colonne collegate), attriti automatici e assegnati, falda, materiali (catalogo e
  personalizzati), copriferro (metodo EC2, «Adotta copriferro minimo»), due zone e facce delle armature, distinta, dettagli,
  tipologia a gravità con i modelli del materiale, esempi;
- `azioni-sisma-matrice`: azioni aggiunte, cambio del tipo (il form si ricostruisce), visibilità e calcolo, selezione ed
  eliminazione; il pulsante «Sisma»; sisma dai parametri del sito, amplificazioni assegnate, kh e kv assegnati, Wood; matrice
  generata, celle modificate (anche «q var. mensola»), riga copiata ed eliminata, conferma, conferma dopo una modifica delle
  azioni, sisma disattivato e rigenerazione;
- `risultati-sezione`: combinazioni, viste, elementi, interruttori, tutti i filtri, selezione di righe strutturali e richiesta
  della sezione c.a. (anche rifiutata), report, invio del terreno, richiesta dopo una modifica e con la gravità;
- `opzioni-armature`: cedimenti e spostamenti, portanza sismica, accelerogramma aggiunto ed eliminato, ricerca delle armature
  applicata, proposta superata da una modifica, ricerca interrotta e superata da una modifica;
- `stabilita-globale`: preparazione dal muro, conferma del profilo, ricerca assegnata, vista e combinazioni globali, strati
  globali (proprietà e spessori), condizione non drenata, due colonne, punti del profilo, falda e sisma globali, matrice
  globale personalizzata, calcolo globale indipendente (anche con le verifiche locali fermate e con la ricerca fuori dagli
  strati), «Modifica profilo, strati e ricerca», «Prepara dal muro»;
- `valori-di-calcolo`: la finestra dei valori usati nel calcolo con il suo scheduler: valore applicato e ricalcolo, valore non
  valido, foglio cambiato durante la convalida (motore trattenuto), chiusura durante la convalida;
- `sola-lettura`: revisione in sola lettura; ingressi, tabelle, tipologia, esempi, azioni, matrice, pulsanti di modifica
  rifiutati; combinazioni, viste, filtri, selezione, sezione c.a., interruttori, calcolo globale, report e calcolo ammessi;
- `calcolo-sullo-sfondo`: con il motore trattenuto, un calcolo superato da una modifica non è mai pubblicato, «Ricalcola»
  durante un calcolo, il calcolo globale che annulla il calcolo locale in corso, la chiusura durante un calcolo.

## Cattura e controllo

- Catturate il 17/10/2026 sul codice di produzione di `2550d454` con:

  ```
  dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-muri-historic-capture <radice> <cartella>
  ```

  Due catture (`supporto/artefatti/refactoring/f5-9/traccia-1` e `-2`) sono uguali byte per byte (24 scenari, 17 666 righe).
- La vista storica non è modificata: la traccia usa i suoi membri interni (classe parziale compilata solo in UiTests).
  - «Valori di calcolo…» apre la finestra come dialogo modale: la traccia calcola e la mostra fuori schermo, non modale, per
    guidarla; la chiusura durante la convalida è quella della barra del titolo.
  - Un testo digitato passa da `InputForm.Set`, come quando l'utente entra nel campo (il testo grezzo sostituisce quello
    formattato), scrive ed esce.
  - Il cambio del tipo di un'azione ricostruisce il form sul dispatcher: la traccia lo lascia eseguire prima di leggere lo stato.
  - Prima di ogni passo la traccia apre la pagina del controllo («Input» o «Verifiche»), come l'utente: in una revisione in sola
    lettura la vista storica protegge solo i controlli della pagina mostrata (`RevisionInspection` sull'albero visuale).
- La suite `ui/check-muri-historic` dello stadio `ui` ricatturava la vista storica e la confrontava con queste tracce.
  È stata tolta il 23/10/2026 con l'archiviazione della vista storica (`supporto/SUPERATI/ui-storica/muri`); `ui/check-muri-view`
  confronta le tracce attraverso la vista XAML.
- `vista-storica.json` è stato registrato da `ui/check-muri-view` sulla vista storica il 23/10/2026, prima dell'archiviazione
  (corsa `20261023-160230-muri-cattura` sulla base `2cf2a908` del ramo `refactoring/f5-pass-muri`); non si rigenera.
- `Presentation.Tests`, nello stadio `fast`, confronta le stesse tracce con il ViewModel.
- Le tracce non si rigenerano: una differenza della vista storica o del ViewModel si spiega e si registra.
- 22/10/2026, voce PARATIE-20 (set candidato del 22/10, Geotechnics 0.19.0.0, cerchi di Bishop con φ = 0 non più scartati),
  con l'ok dell'utente: ricatturata fuori schermo, con il comando sopra, la sola traccia `fixture-globale-non-drenata-sisma`.
  Cambiano l'impronta del risultato, lo stato («4 non disponibili» → «2 non disponibili»), la combinazione globale scelta
  (A2–M2–R2 2 → SLV kv− 3) e l'impronta della tabella delle verifiche; numero di righe invariato. Le altre 23 tracce non
  cambiano.
- 24/10/2026, voce AG-MUR-01 (librerie del 24/10: Model 2d59bbc6, GPCModel 5.21.0.0; Checker f6d87d28, GPCChecker.Concrete
  0.20.0.0; parabola del calcestruzzo analitica come legge predefinita), con la decisione dell'utente del 24/10 («registra e
  aggiorna»). La vista storica è archiviata e non si ricattura: ricattura parziale dal ViewModel. Nelle 17 tracce che cambiano
  sono sostituite solo le 130 righe cambiate, con il valore del ViewModel (`Presentation.Tests`, corsa
  `20261024-133916-ag-po-raccolta2`): impronta del risultato (69) e impronta delle tabelle «Verifica|Combinazione|Ed|Rd|…» delle
  verifiche e della proposta delle armature (61), con lo stesso numero di righe. Tolti numeri e impronte ogni riga è uguale a
  quella storica. Le resistenze N–M GPC cambiano fra −0,022 % e +0,224 %, i rapporti fra −0,223 % e +0,022 %; nessun esito
  cambia. Le altre 7 tracce non cambiano. `vista-storica.json` non è stato toccato: va controllato con lo stadio `ui`.
- 24/10/2026, stessa voce AG-MUR-01, set AG ufficiale (4dd8018a: Model 2d59bbc6, Checker 22dabe69): in `vista-storica.json`
  sostituiti solo 20 valori di 10 fixture (`default`, `example-cantilever`, `globale-drenata-due-colonne`,
  `globale-non-drenata-sisma`, `legacy-global-defaults`, `matrice-personalizzata`, `mensola-avvisi`, `mensola-sisma-sito-pendio`,
  `mensola-wood-azioni`, `opzioni-avanzate`): impronta del risultato e del CSV delle verifiche, dalla vista XAML
  (`ui/check-muri-view`, corsa `20261024-225208-ag-ui2-raccolta`). Impronte dei dati, risultati e calcolo mostrati, combinazioni,
  CSV dei conci, pagine e CSV della distinta non cambiano. Il risultato della vista è uguale in ogni numero a quello del ViewModel
  già registrato con AG-MUR-01 (`Presentation.Tests`, f3-transizioni); i CSV delle verifiche, confrontati con quelli della corsa
  `20261023-193404-merge-muri-ui`, hanno le stesse righe e cambiano solo nei numeri (al più 0,22 %). Le altre 6 fixture non cambiano.
- 27/10/2026, librerie della 1.14.2 (Checker 4a1b0090, AJ-LIB-01, voce BUG-MUR-2b): la passiva a valle non entra più nella resistenza al ribaltamento. In `storica` sostituite 122 righe di 21 tracce (impronte del risultato, MRd del ribaltamento e righe delle verifiche dei muri con passiva, testo della passiva nei parametri derivati); in `vista-storica.json` 14 valori dalla vista XAML con una registrazione temporanea di `ui/check-muri-view`, non committata (corsa `supporto/artefatti/lib-1142r/ui-muri-registrazione`): 13 impronte del risultato (testo del metodo) e il CSV delle verifiche di `gravita-cls-passiva`. Impronte dei dati, combinazioni, conci, distinta e report (ricetta) invariati.
