# Esempi distribuiti — 20 ottobre 2026

52 documenti per i 15 moduli disponibili, almeno due per modulo, nella sola
cartella `supporto/esempi/distribuzione/`, con sottocartelle per identificativo.
`catalogo.json` contiene descrizione, categoria, origine e impronta SHA-256.
Gli esempi precedenti che costituiscono fonti di prove ancora attive restano
nelle rispettive sedi; l'applicazione e l'installer usano soltanto il catalogo.
I byte degli esempi e degli originali archiviati sono protetti in `.gitattributes`.

37 esempi di calcolo, due stratigrafie, dodici geometrie di sezioni e un
progetto con paratia grafica. I casi di sezione sono nel formato 3 con modello
GPC nativo. Le geometrie da wizard richiedono azioni e parametri manuali del
taglio: la descrizione non le presenta come verifiche strutturali complete.
Il caso rettangolare con materiali personalizzati mantiene il riferimento DIN
EN 1992-1-1 e la proiezione 2D è esplicita. Nel caso circolare cavo con spirale
si assegnano azioni senza torsione. Nessun atteso indipendente è modificato.

## Architettura e comportamento

Application legge e valida il catalogo e restituisce documenti indipendenti.
Presentation gestisce gruppi, selezione e comando protetto; la vista WPF
contiene solo binding e composizione. La shell conserva la normale domanda
sulle modifiche da salvare e apre gli esempi con percorso nullo e stato
modificato. Lo stesso comportamento vale per i file installati aperti da
Explorer. Il salvataggio nella cartella degli esempi è rifiutato.

Il componente NSIS è facoltativo e selezionato inizialmente. Il catalogo viene
verificato prima della pubblicazione; gli elenchi di installazione e rimozione
sono espliciti. Il collegamento Start apre la cartella Esempi. I file sono
copiati nelle build locali, mentre la pubblicazione li esclude dall'applicazione
obbligatoria per lasciare la scelta al componente dell'installer.

## Collaudo

- `ci/20261020-144954-esempi-08`: 249 controlli, 52 documenti riaperti e
  salvati come copie; 37 calcoli eseguiti senza errori o risultati parziali.
  Risultati in `Examples.Tests/calcoli.json`, come evidenza di esecuzione,
  senza sostituire gli attesi indipendenti delle regressioni.
- `ci/20261020-144818-esempi-07`: architettura 562, Wiki 340, WPF 20 e
  installer 120 controlli superati. Rifiuto di file mancanti, percorsi esterni,
  duplicati e impronte alterate. Installazione/rimozione della sezione NSIS
  reale in cartella isolata e conservazione di un file personale.
- Guide Rev55: pratica 99 pagine, teoria 164, 387 formule Word invariate,
  parti ITEC conservate. Controllo visivo delle 263 pagine mediante tavole,
  della nuova procedura a piena risoluzione e dei quattro indici/istruzioni.
  18 originali conservati con SHA-256 in
  `supporto/SUPERATI/esempi-rev55-20261020/`.

Gli errori iniziali degli esempi vuoti o fuori campo sono stati risolti
scegliendo dati completi e dichiarando le ipotesi applicabili; i controlli
di produzione non sono stati indeboliti. Il test di rifiuto riconosce
esplicitamente `InvalidDataException`. Il collaudo completo del rilascio
segue sul set ufficiale AB e sulla revisione pulita.
