# ANTHEA 1.17.2 — note di rilascio

Note del 2 novembre 2026. La 1.17.2 corregge e completa il modulo Modelli dopo le prove dell'utente sulla 1.17.1:
- i modelli letti da MIDAS aperto hanno le terne locali, quindi mostrano i carichi sulle piastre e la media ai vertici;
- la finestra delle sezioni mostra anche gli spessori delle piastre;
- la gestione delle combinazioni calcola tutte le combinazioni, o quelle selezionate, in un solo comando.

Contiene le librerie GPC della release AT e le guide Rev68, come la 1.17.1. Il numero di versione è quello scelto dall'utente.

Il testo è scritto per chi usa ANTHEA ed è anche l'annotazione del tag `v1.17.2`, sul commit del setup. Il verbale tecnico del rilascio è
in [installer-1.17.2.md](installer-1.17.2.md).

## Correzioni

- **Modelli da MIDAS aperto (API), terne locali.** La lettura dal collegamento con MIDAS passa ora le tabelle del modello dal
  convertitore GPC, come già i file .mct e .mgt: piastre e aste hanno le terne locali, i nodi le loro terne, e compaiono i simboli di
  vincoli, molle, link e rilasci.
  - **Prima.** Le piastre restavano senza terna (avviso «MIDAS-CONVERTER-PENDING»). Un carico di pressione in direzione locale, il caso
    normale in MIDAS, finiva fra le «assegnazioni non rappresentate» e la «Media ai vertici» lasciava i valori originali.
  - **Modelli già importati con la 1.17.1 o prima.** Le terne arrivano con una nuova lettura da MIDAS. «Solo risultati» le aggiunge
    alla copia aperta, se tabelle e geometria non sono cambiate; «Tutto» le porta con il resto.
  - **Sola geometria.** Le terne ci sono; vincoli e link restano «non acquisiti», perché quella lettura non li scarica.
- **Carichi sulle piastre.** Una pressione uniforme scritta un valore per vertice e la direzione definita da un vettore sono disegnate
  come le altre, come le legge il convertitore GPC. Le pressioni proiettate e quelle variabili restano fra le non rappresentate.

## Novità nell'uso

- **Sezioni del modello, spessori.** «Sezioni del modello…» elenca, dopo le sezioni delle aste, gli spessori delle piastre
  («Spessore N · nome»). Ogni spessore è disegnato come una striscia larga 1 m: spessore e offset della proprietà, G sul piano medio, O
  sul piano dei nodi e la normale z. **Seleziona elementi** e **Isola elementi** agiscono sulle piastre di quello spessore. Selezionando
  piastre di un solo spessore, l'ispettore mostra la striscia e **Dettaglio sezione…** apre lo spessore.
- **Combinazioni, calcolo in serie.** Sotto il comando della combinazione scelta:
  - **Calcola tutte (N)** calcola, una dopo l'altra, le combinazioni non ancora calcolate (e quelle da ricalcolare per le componenti
    principali delle piastre);
  - **Calcola selezionate (N)** calcola le righe scelte nell'elenco con Ctrl o Maiusc + clic.

  I casi base si preparano una volta sola per tutta la serie. I risultati vanno nel progetto ogni 20 secondi e alla fine: **Annulla
  calcolo** ferma la serie e tiene le combinazioni già calcolate. Una combinazione che non si calcola non ferma le altre e viene elencata
  nel messaggio finale. Se il limite del pacchetto del modello (256 MiB) comincia a togliere combinazioni della serie, il calcolo si ferma
  e lo dice.

## Librerie

Release AT, come la 1.17.1 (Model `5449e4d1`, Checker `a2acd3d2`). Le versioni delle DLL sono in `lib/Checker/README.md`.

## Guide

Guide pratica e teorica Rev68, come la 1.17.1: non descrivono ancora gli spessori nella finestra delle sezioni e il calcolo in serie
delle combinazioni. Le etichette che citano («Sezioni del modello…», «Dettaglio sezione…») non sono cambiate.

## Limiti noti

- La full di rilascio non è stata eseguita su questo commit: sono passate le suite del modulo Modelli e le altre elencate nel verbale.
- Un inviluppo di combinazioni viene calcolato dai casi base anche quando le combinazioni che contiene sono già calcolate.
- Restano quelli della 1.17.1.
