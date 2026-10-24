# Tracce e disegni della vista storica della sezione composta da ponte (F5.11)

Proposta F5, sezione 7.2: prima di migrare un modulo si cattura il comportamento della vista storica. Il riferimento F3
confronta i dati in forma canonica (chiavi ordinate) e non vede l'interfaccia. Queste tracce conservano invece lo stato visto
dall'utente nella scheda della sezione composta (`str_mista_ponte`, `BridgeWorkspace` creata come la crea `SheetEditor`, su una copia
dei dati, fuori schermo): per intero dopo l'apertura, poi dopo ogni passo di uno scenario solo le voci cambiate («~» testo nuovo,
«+» voce nuova, «-» voce sparita). Le voci sono, nell'ordine:

- i dati: l'ordine delle chiavi e una voce per chiave, nell'ordine in memoria (byte compatti);
- il numero di `Modified`, il messaggio del passo (report disponibile o il motivo del rifiuto), il calcolo (occupato, impronta
  del risultato, risultato mostrato corrente o da aggiornare, esportabile);
- i testi: stato, avviso sopra le pagine, avvisi del risultato, panoramica, schede del riepilogo, materiali adottati, altezza
  dell'anima, quote della soletta e della predalle, metodo di calcolo, convenzione dei carichi, numero delle fasi, convergenza;
- la pagina mostrata con titolo e sottotitolo degli ingressi, il pannello laterale (riepilogo o proprietà), la tabella dei risultati,
  la vista staccata; le situazioni (voci, scelta, visibili, attive), la vista del disegno, la scheda dei risultati;
- gli interruttori (quote, informazioni delle armature, asse neutro con la sua spiegazione, contorni, limiti, menu «Verifiche ▾»),
  la scala del CLS (testo, automatica, visibile, errore, riferimento, modificabile) e il prospetto locale;
- la visibilità dei gruppi (seconda piastra e sua nota, armatura trasversale, fatica, cassoncino, discretizzazione, risultati della
  torsione);
- ogni campo dei form della definizione, degli accessori, del metodo e dei limiti, nell'ordine di
  `CompositeSectionScenarios.Forms`: tipo, etichetta, valore (testo grezzo), voci, spento, attenuato, nascosto, bloccato;
- le schede delle fasi (intestazione, note, comandi), i loro form e quelli dell'omogeneizzazione con il testo n₀ · φeff;
- la tabella delle azioni (righe, selezione, colonna ΔT, sola lettura, celle spente per tipo di fase) e i suoi comandi;
- i pannelli dei risultati come blocchi (testi, tabelle con colonne, numero di righe, impronta e prime tre righe, gruppi): tensioni,
  verifiche, omogeneizzazione, equilibrio, classe 4, geometria, taglio, pioli, torsione;
- le proprietà della sezione (schede, nota, acciaio, form e tabelle della soletta, una scheda per fase);
- il disegno come dati: modo, risultati da aggiornare, interruttori, amplificazione del CLS, sezione del prospetto, geometria
  (dell'anteprima o del risultato, impronta), ingresso, situazione, punti di applicazione di N;
- la pagina delle curve: form, fasi di partenza, comandi, avviso, punto scelto, fibre, grafici (assi, impronta dei punti, punto
  scelto) e la tabella dei punti.

| Cosa | Dove |
| --- | --- |
| Scenari e formato | `tests/Shared/CompositeSectionTrace.cs` |
| Vista storica | archiviata il 23/10/2026 con la sua prova (`supporto/SUPERATI/ui-storica/sezione-mista`, già `tests/Desktop/CompositeSectionHistoricTrace.cs`) |
| ViewModel | `tests/ANTHEA.Presentation.Tests` (senza WPF) |
| Vista XAML | `ui/check-sezione-mista-view` (`tests/Desktop/CompositeSectionViewChecks*.cs`), le stesse tracce attraverso i controlli della vista |
| Valori registrati | `vista-storica.json`: i valori che `ui/check-sezione-mista-view` confrontava con la vista storica aperta accanto, registrati prima dell'archiviazione (sotto) |

Ogni passo è quello che fa l'utente con un controllo: un controllo che l'utente non può usare (non sulla pagina mostrata, nascosto,
spento o protetto dalla revisione in sola lettura) non fa nulla. Un testo digitato passa da `InputForm.Set`, come quando l'utente
lascia il campo; una cella è scritta come la scrive il legame della griglia quando la cella è confermata.

Il calcolo passa da `HeldModuleService`, che può trattenere il motore. Le continuazioni della vista (il ritardo di 500 ms del ricalcolo
automatico, il ritorno del motore, la pubblicazione, le curve) girano alla priorità di inattività del dispatcher: avanzano solo mentre
la traccia aspetta («attendi», «calcola», «attendi-motore», «calcola-curva»), mai mentre un passo si assesta. Così la traccia non
dipende dal tempo che un passo impiega (le prime catture senza questa regola davano «occupato» in passi diversi), come il ViewModel
senza WPF, le cui continuazioni girano solo quando le sue prove aspettano.

## Scenari

| Scenario | Contenuto |
| --- | --- |
| `percorso-<id>` | uno per ognuna delle 9 fixture F3 del modulo: apertura, attesa del calcolo automatico, conferma, la visita della cattura F3 (espansori aperti, ogni scheda di ogni controllo a schede), conferma, calcolo, conferma, riapertura, conferma, attesa |
| `ingressi-sezione` | nome, normativa (con i coefficienti della normativa), coefficienti, materiali e fy assegnato (anche non numerico), soletta, predalle e riferimento dei ferri inferiori, file di barre, seconda piastra, altezza e anima, i tre tipi di sezione con le opzioni del cassoncino, anima vuota (geometria precedente), proprietà della soletta esplorate |
| `ingressi-accessori` | irrigidimenti intermedi (lati, pannelli, saldature), irrigidimento d'appoggio con montante terminale, prospetto locale, pioli (una o più file), armatura trasversale e forcine, fatica, ΔV e Δq delle fasi, scheda delle verifiche |
| `fasi-schede` | schede delle fasi: nome, φ, n e ψL, riferimento di N e quota comune, tipo (soletta esclusa, ritiro), fase esclusa e di nuovo inclusa, fasi aggiunte, spostate ed eliminate, limiti e classe 4, i tre metodi con la discretizzazione e le opzioni del non lineare |
| `tabella-azioni` | la tabella delle azioni: celle di ogni tipo (anche non applicabili), righe aggiunte, spostate ed eliminate con i comandi sotto la tabella |
| `risultati-viste` | situazioni, viste, schede dei risultati, interruttori, contorni e limiti, scala del CLS (non valida, manuale, automatica), dettagli della sezione efficace, modifiche con una situazione scelta, proprietà |
| `risultati-storico` | i risultati dello storico lineare: situazioni, schede, viste, una fase esclusa |
| `curve-sezione` | la pagina delle curve: form, calcolo, fibra e punto, curva M–κ, dati cambiati dopo una curva, sezione vergine |
| `vista-staccata` | «Stacca vista»: pagina, vista e situazione mentre la vista è staccata, una modifica, «Riaggancia» |
| `report` | report con sezioni scelte (salvate nel foglio), report con tutte le sezioni, report rifiutato senza risultato |
| `sola-lettura` | revisione in sola lettura: form, tabella, comandi, prospetto, quote, contorni, scala e curve rifiutati; pagine, situazioni, viste, asse neutro, proprietà, dettagli e calcolo ammessi |
| `calcolo-sullo-sfondo` | motore trattenuto: calcolo superato da una modifica, «Calcola» durante un calcolo, chiusura e riapertura durante un calcolo |

Non sono nelle tracce: «Info modello…» (finestra modale con testi fissi), «Esporta CSV…» ed «Esporta punti e fibre CSV…»
(dialoghi dei file), «Annulla» della curva (dipende dal momento in cui il motore controlla l'annullamento), i divisori, lo zoom e la
navigazione nel disegno, «Schermo intero · F11» della vista staccata. Le prove del ViewModel li coprono con valori scritti a mano dove
servono.

## Disegni

`disegni/elenco.txt`: per ogni fixture F3 del modulo, aperta e calcolata fuori schermo, una riga per disegno (numero dei primitivi e
impronta) degli elenchi di disegno (linee, forme, penne, riempimenti, testi con la loro origine):
- la sezione come la scheda la mostra all'apertura (1000 × 640);
- il prospetto locale, quando la scheda lo mostra (800 × 165);
- le curve e la fibra, calcolate per la fixture che si apre sulla pagina delle curve (700 × 420);
- la geometria del pannello di controllo, le tensioni totali e i contributi della prima e dell'ultima situazione (1000 × 640);

poi una riga per ogni figura del report con tutte le sezioni (`ModuleFigureRenderer.BridgeFigures`): categoria, didascalia, dimensioni
e impronta dei pixel. Gli elenchi completi restano nella cartella della corsa (`disegni/*.txt`). Servono a provare che lo spostamento
delle derivazioni in Application e del piano delle figure lascia i disegni identici. I testi dipendono dai font installati e dalla
scala dello schermo: gli elenchi valgono sulla macchina del banco, come le prove di contrasto (catturati a 144 DPI).

## Cattura e controllo

- Catturate il 21/10/2026 sul codice di produzione di `48ff81b5` (vista storica invariata da `c7fe03dd`) con:

  ```
  dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-sezione-mista-historic-capture <radice> <cartella>
  ```

  Due catture uguali byte per byte (`supporto/artefatti/refactoring/f5-11/traccia-1` e `traccia-2`, non versionate): 20 scenari,
  17 797 righe, 88 righe dei disegni (52 elenchi e 36 figure del report). Le catture precedenti, senza la regola delle
  continuazioni, differivano in tre scenari solo per «occupato» e lo stato del calcolo dopo «riapri» e «rilascia».
- La suite `ui/check-sezione-mista-historic` dello stadio `ui` ricatturava la vista storica e la confrontava con queste tracce e con
  l'elenco dei disegni. È archiviata con la vista al passaggio della Release (23/10/2026).
- `vista-storica.json` (23/10/2026): registrato da `ui/check-sezione-mista-view` sulla vista storica, sulla base `db7ef68d`, parti
  `esportazioni,aiuto,nomi,sola-lettura,finestre` (PASS 109: la vista XAML dava gli stessi valori), prima dell'archiviazione. Per le 9
  fixture: impronte SHA-256 dei dati e numero di `Modified` dopo il calcolo automatico dell'apertura, la visita, il calcolo e il report,
  impronta del risultato, del CSV delle tensioni e della curva (punti e CSV); le etichette «?» (nessuna), i nomi di automazione di 4
  fixture, i controlli liberi e bloccati in sola lettura di 2 fixture, i 44 testi di «Info modello…».
- Le tracce non si rigenerano: una differenza della vista storica o del ViewModel si spiega e si registra.
- 24/10/2026, voce AG-SM-01 (librerie del 24/10: Model 2d59bbc6, GPCModel 5.21.0.0; Checker f6d87d28, GPCChecker.CompositeBridge
  1.13.0.1; legge del calcestruzzo analitica nello storico non lineare a fibre), accettata dall'utente il 24/10 («4B»: accettare e
  aggiornare subito, segnalandola nelle note di rilascio). La vista storica è archiviata e non si ricattura: ricattura parziale dal
  ViewModel. Nelle 3 tracce che cambiano sono sostituite solo le 143 righe cambiate, con il valore del ViewModel
  (`Presentation.Tests`, corsa `20261024-133916-ag-po-raccolta2`): `curve-sezione` 80, `percorso-storico-non-lineare-curve` 43,
  `fasi-schede` 20, solo pannelli dei risultati (panoramica, convergenza, tensioni, verifiche, omogeneizzazione, equilibrio, curve)
  e impronta del risultato, con lo stesso numero di righe. Tolti numeri e impronte ogni riga è uguale a quella storica: dati, campi
  e stati non cambiano. Le grandezze che governano cambiano poco (|σa|max −0,10 %, ala superiore −1,04 %); calano in modulo oltre
  il 3 % alcune grandezze piccole: tensioni dell'armatura della soletta fino a −5,4 %, incrementi di fase fino a −6,8 %,
  deformazioni fino a −6,0 %. Le altre 17 tracce non cambiano. `vista-storica.json` non è stato toccato: va controllato con lo
  stadio `ui`.
- 24/10/2026, stessa voce AG-SM-01, set AG ufficiale (4dd8018a: Model 2d59bbc6, Checker 22dabe69), dalla vista XAML
  (`ui/check-sezione-mista-view`, corsa `20261024-225208-ag-ui2-raccolta`):
  - `disegni/elenco.txt`: sostituite solo le impronte di 7 righe di `storico-non-lineare-curve` (vista, situazione 3 viste 0 e 1,
    curva, fibra, figure fase_1 e fase_2 del report), stesso numero di primitive e stesse didascalie. Rispetto ai dump della corsa
    `20261023-170938-fix-mista-report` cambiano solo coordinate e testi numerici (σa −61,22 → −60,6 MPa, scala acciaio
    ±124,14 → ±124,01, CLS ±20,14 → ±20,12 MPa). Le altre 81 righe non cambiano.
  - `vista-storica.json`: per `storico-non-lineare-curve` sostituite le impronte del risultato, del CSV delle tensioni e del CSV
    della curva. Dati e `Modified` (apertura, visita, calcolo, report) e numero dei punti della curva non cambiano; il risultato
    è uguale in ogni numero a quello del ViewModel già accettato con AG-SM-01; i CSV hanno le stesse righe e cambiano solo nei
    numeri (al più lo 0,7 % del massimo della colonna, 2,8 % per la deformazione plastica, piccola). Le altre 8 fixture non cambiano.
