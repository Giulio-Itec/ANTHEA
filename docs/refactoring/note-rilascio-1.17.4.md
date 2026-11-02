# ANTHEA 1.17.4 — note di rilascio

Note del 2 novembre 2026. La 1.17.4 rende più veloce il calcolo delle combinazioni del modulo Modelli. I casi base si sommano con il
motore a matrici di GPCModel 5.38, che dà gli stessi valori bit per bit del calcolo della 1.17.3: valori, governanti, vettori
concomitanti e componenti principali delle piastre non cambiano. Non ci sono funzioni nuove, quindi il numero segue la regola delle
correzioni.

Contiene le librerie GPC della release AU e le guide Rev68.

Il testo è scritto per chi usa ANTHEA ed è anche l'annotazione del tag `v1.17.4`, sul commit del setup. Il verbale tecnico del rilascio è
in [installer-1.17.4.md](installer-1.17.4.md); il piano del lavoro in
[modelli-motore-combinazioni](../sviluppo/modelli-motore-combinazioni.md).

## Combinazioni più veloci

Il motore prepara una volta, per ogni punto del modello, la tabella dei casi base allineati e ruotati. Ogni combinazione è poi una
somma pesata delle righe di quella tabella, invece di una ricostruzione dei risultati caso per caso. Vale per la combinazione scelta,
per **Calcola tutte** e per **Calcola selezionate**.

Tempi su un modello di prova con 500 elementi, 472 nodi, 100 casi statici e 951 definizioni NTC 2018. La tabella riporta la mediana di
giri alternati con il calcolo della 1.17.3 e con il motore, sulla stessa macchina:

| Combinazione | 1.17.3 | 1.17.4 |
| --- | ---: | ---: |
| Lineare generata | 0,60 s | 0,16 s |
| Lineare dell'utente | 0,37–0,39 s | 0,16–0,17 s |
| Inviluppo SLU | 5,4–5,9 s | 0,71 s |
| Inviluppo SLV | 2,6–2,9 s | 0,55–0,57 s |

- A ogni combinazione si aggiunge la scrittura del risultato nel modello, circa 0,2–0,3 s, che non cambia.
- La prima combinazione dopo l'apertura del modello prepara i casi base e dura come prima: 10–12 s sul modello di prova.
- Gli inviluppi con più di 4096 stati alternativi restano sul calcolo della 1.17.3, per termini sull'albero della definizione.

## Librerie

Release AU: GPCModel 5.38.0.0 con il motore a matrici (Model `2f934f9f`) e Checker `6156e4ce`, ricompilato senza modifiche ai
sorgenti. Le versioni delle DLL sono in `lib/Checker/README.md`.

## Guide

Guide pratica e teorica Rev68, come la 1.17.3. Il motore non cambia comandi né etichette. Le guide non descrivono ancora gli spessori
nella finestra delle sezioni e il calcolo in serie delle combinazioni (1.17.2), il calcolo all'apertura e il «?» dei campi Azioni
(1.17.3).

## Limiti noti

- Un inviluppo viene ancora calcolato dai casi base anche quando le combinazioni che contiene sono già calcolate. Con il motore questo
  costa meno di un secondo sul modello di prova.
- Restano quelli della 1.17.3.
