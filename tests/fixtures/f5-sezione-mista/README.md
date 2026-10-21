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
| Vista storica | `tests/Desktop/CompositeSectionHistoricTrace.cs` (passi sui controlli, stato, disegni) |
| Comando | `ANTHEA.exe --check-sezione-mista-historic <radice> <cartella>` (confronto) e `--check-sezione-mista-historic-capture` (cattura), solo build UiTests |
| Suite | `ui/check-sezione-mista-historic` |
| ViewModel | `tests/ANTHEA.Presentation.Tests` (senza WPF) |

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
- La suite `ui/check-sezione-mista-historic` dello stadio `ui` ricattura la vista storica e la confronta con queste tracce e con
  l'elenco dei disegni, finché la vista storica esiste.
- Le tracce non si rigenerano: una differenza della vista storica o del ViewModel si spiega e si registra.
