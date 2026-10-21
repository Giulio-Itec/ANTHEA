# Tracce e disegni della vista storica di Bridge Design (F5.12)

Proposta F5, sezione 7.2: prima di migrare un modulo si cattura il comportamento della vista storica. Il riferimento F3 confronta i dati
in forma canonica (chiavi ordinate) e non vede l'interfaccia. Queste tracce conservano invece lo stato visto dall'utente nella scheda di
Bridge Design (`str_bridge_design`, `BridgeDesignWorkspace` creata come la crea `SheetEditor`, su una copia dei dati, fuori schermo): per
intero dopo l'apertura, poi dopo ogni passo di uno scenario solo le voci cambiate («~» testo nuovo, «+» voce nuova, «-» voce sparita).
Le voci sono, nell'ordine:

- i dati: l'ordine delle chiavi e una voce per chiave, nell'ordine in memoria (byte compatti; le date dei fattori ambientali e della
  cronologia dei prezzi diventano `<DATAORA>`);
- il numero di `Modified`, il messaggio del passo (report disponibile o il motivo del rifiuto, i testi della pagina stampata, lo stato
  dell'editor dei prezzari alla chiusura), il calcolo (occupato, impronta del risultato, confronto con l'alternativa A o il suo errore,
  alternative proposte);
- lo stato (testo e colore d'errore), le quattro metriche (valore e didascalia), la configurazione sotto il disegno;
- la pagina («Progetto», «Ottimizzazione») e le schede degli ingressi e dei risultati mostrate; gli stati che «Annulla» può ripristinare;
- il disegno come dati: prospetto o sezione, dati disegnati, impronta del risultato;
- le cinque schede degli ingressi come blocchi: testi (con il ruolo: titolo, evidenza, nota, avviso, errore, accento), parametri (chiave,
  etichetta, testo grezzo, unità, bordo rosso, cursore), scelte (voce scelta e voci), opzioni, i pulsanti delle tipologie (la scelta
  segnata «*») e i pulsanti;
- i pannelli dei risultati come blocchi (tabelle con colonne, numero di righe, impronta, prime tre righe e riga scelta): quantità e costi,
  dettagli (con il diagramma dei momenti come impronta del risultato), confronto A / B, prezzario, ipotesi e coefficienti, sezioni e
  quote, alternative;
- la ricerca dell'ottimo: obiettivo, blocchi, limiti (spenti quando la griglia è bloccata), stato, migliore, selezione e rango, avanzamento,
  «Avvia» e «Applica» (non letti in una revisione in sola lettura, dove la protezione li spegne), risultato, progetto cercato, graduatoria e
  motivi di esclusione, variazioni della soluzione scelta, anteprima, nuvola (tentativi, soluzioni, riferimento, rango segnato, filtro,
  Pareto) e grafico delle variabili.

| Cosa | Dove |
| --- | --- |
| Scenari e formato | `tests/Shared/BridgeDesignTrace.cs` |
| Vista storica | `tests/Desktop/BridgeDesignHistoricTrace.cs` (passi sui controlli, stato, disegni) |
| Comando | `ANTHEA.exe --check-bridge-design-historic <radice> <cartella>` (confronto) e `--check-bridge-design-historic-capture` (cattura), solo build UiTests |
| Suite | `ui/check-bridge-design-historic` |
| ViewModel | `tests/ANTHEA.Presentation.Tests` (senza WPF) |

Ogni passo è quello che fa l'utente con un controllo: prima del passo l'utente apre la pagina e le schede che contengono il controllo
(`BridgeDesignScenarios.PlaceOf`), e un controllo che non può usare (su una pagina o una scheda non mostrata, nascosto, spento o protetto
dalla revisione in sola lettura) non fa nulla. Un testo digitato è scritto nella casella, un cursore spostato è il valore del cursore. Tre
passi chiamano quello che il controllo chiama, perché il controllo lo nasconde: «Avvia ottimizzazione» (il gestore attende la ricerca: la
traccia ne tiene il compito per «attendi-ricerca»), «Ponte casuale» (il pulsante usa il generatore condiviso, la traccia uno con il seme
del passo) e il clic su un punto della nuvola (`SelectAt` nella posizione del punto del rango). La finestra dell'editor dei prezzari è
modale: lo script del passo (`BridgeDesignPriceScripts`) gira dal dispatcher mentre la finestra è aperta, sul ViewModel a cui la finestra è
legata, poi «Applica prezzario» o la chiusura della finestra.

Il calcolo passa da `HeldModuleService`, che può trattenere il motore (il calcolo del ponte; l'alternativa A e le alternative usano il
servizio del modulo, come oggi). Le continuazioni della vista (il ritardo di 450 ms, il ritorno del motore, la pubblicazione, la ricerca e
il suo avanzamento) girano alla priorità di inattività del dispatcher: avanzano solo mentre la traccia aspetta («attendi», «calcola»,
«attendi-motore», «attendi-ricerca»), mai mentre un passo si assesta. Lo stato intermedio della ricerca (anteprima del migliore provvisorio,
al più due volte al secondo) non entra nella traccia: alla fine della ricerca è sempre sostituito.

## Scenari

| Scenario | Contenuto |
| --- | --- |
| `percorso-<id>` | uno per ognuna delle 10 fixture F3 del modulo: apertura, conferma, la visita della cattura F3 mentre il calcolo dell'apertura lavora, conferma, calcolo, conferma, report, stampa, riapertura, conferma, attesa |
| `ingressi-sito-campate` | ostacolo e terreno, lunghezza non numerica, fuori campo e con la virgola, cursori, corsia e banchina (anche vuota), campate, le tre opzioni delle campate, campate automatiche, «Ripristina dimensioni automatiche» |
| `sezione-tipologie` | ogni genere di tipologia (cassone in acciaio, arco con il piano carrabile ortotropo, strallato, sospeso, reticolare, travi incorporate, ortotropo, conci, travi a U, soletta, travi a I) con i campi propri, «Sezione automatica» |
| `pile-traffico` | pile e fondazioni con le dimensioni imposte, i due modelli di traffico con carico e γQ (anche non numerico), «Applica γQ NTC consigliato», le antenne |
| `risultati-confronto` | «Fissa A», confronto, «Ripristina A», «Rimuovi confronto», le alternative e «Ripristina fattori ambientali ordinari», prospetto e sezione, «Sezioni e quote», «Nuovo paesaggio», due ponti casuali, «Annulla», «Ottimizza» |
| `prezzi-ipotesi` | tariffe e ipotesi (anche non numeriche), «Applica valori orientativi 2026», «Annulla» con il bordo rosso, fattori ambientali, l'editor dei prezzari annullato, applicato, con un prezzo personalizzato e con una voce mancante |
| `prezzario-documento` | il documento prezzi di una fixture: editor annullato, catalogo cambiato, alternativa A rivalutata |
| `ottimizzazione` | blocchi, limiti, campate suggerite, ricerca, graduatoria e nuvola, variabili, filtro e Pareto, prime N (anche non valide), anteprima, obiettivi, soluzione applicata e annullata, ricerca senza soluzioni, limiti non validi, ricerca interrotta e invalidata da una modifica |
| `sola-lettura` | una revisione in sola lettura: ogni modifica rifiutata; pagine, schede, viste, ricerca suggerita e interrotta, report, stampa e calcolo ammessi |
| `calcolo-sullo-sfondo` | il motore trattenuto: un calcolo superato non è pubblicato, «Calcola» durante un calcolo, la ricerca che aspetta il calcolo, la chiusura durante un calcolo |

## Disegni

`disegni/elenco.txt` ha una riga per disegno (numero di primitivi e impronta dell'elenco di disegno: linee, forme, penne, pennelli, testi):
per ogni fixture il prospetto e la sezione (1000 × 400), il diagramma dei momenti dei dettagli (800 × 180) e i pixel delle due figure del
report; per la prima fixture anche le 14 icone delle tipologie e, dopo una ricerca breve, la nuvola, il grafico di ogni variabile e
l'anteprima. I disegni restano WPF (Q3): l'elenco prova che le derivazioni spostate non cambiano ciò che disegnano.

## Cattura

Due catture uguali byte per byte sul codice di produzione di `7259150a` (prima di toccare la vista): `supporto/artefatti/refactoring/f5-12/traccia-3`
e `-4` del worktree, non versionate. La vista storica non è modificata: la traccia usa membri interni in una classe parziale compilata solo
in UiTests.
