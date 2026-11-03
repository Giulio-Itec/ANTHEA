# ANTHEA 1.18.0 — note di rilascio

Note del 3 novembre 2026. La 1.18.0 aggiunge alla finestra delle combinazioni del modulo Modelli il pannello **Spazio delle combinazioni**:
- mostra quanto spazio occupa la copia del modello con le combinazioni calcolate;
- permette di scegliere il limite;
- permette di liberare i risultati che non servono.

Nella 1.17.6 il limite era fisso a 2 GiB, e **Calcola tutte** si fermava lì sui modelli grandi. Valori e revisioni non cambiano. È una
funzione nuova, quindi aumenta il secondo componente del numero.

Contiene le librerie GPC della release AU e le guide Rev68, come la 1.17.6.

Il testo è scritto per chi usa ANTHEA ed è anche l'annotazione del tag `v1.18.0`, sul commit del setup. Il verbale tecnico del rilascio è
in [installer-1.18.0.md](installer-1.18.0.md).

## Spazio delle combinazioni

Il pannello è in fondo alla gestione delle combinazioni, sotto **Calcola tutte** e **Calcola selezionate**:

- **Spazio occupato.** Barra e testo: per esempio «1,4 GiB di 4 GiB · 120 combinazioni con risultati salvati».
- **Limite.** Si sceglie fra quelli che la memoria del computer consente: 2 GiB sempre; 4, 8 e 16 GiB con almeno 24, 48 e 96 GiB di
  memoria. Su un computer da 64 GB sono disponibili 2, 4 e 8 GiB. La scelta vale per il computer e resta per i modelli aperti dopo.
- **Libera selezionate** e **Libera tutte.** Tolgono dalla copia i risultati salvati delle combinazioni selezionate nell'elenco, o di tutte.
  Le definizioni restano e tornano «Da calcolare». **Annulla** del progetto le riporta.

Quando una serie raggiunge il limite si ferma, come prima, e il messaggio indica le tre strade: alzare il limite, liberare i risultati che
non servono, calcolare meno combinazioni alla volta.

## Copie grandi

Su un modello di prova con 3456 piastre e 49 casi, con il limite di 4 GiB:
- **Calcola tutte** ha calcolato 581 combinazioni in poco più di 4 minuti, circa 0,4 s ciascuna salvataggi compresi;
- la copia è arrivata a 4 GiB, con circa 8 GB di memoria occupata e un picco di 18 GB.

Per reggere queste dimensioni il salvataggio della copia è cambiato:
- ogni risultato si scrive e si comprime una volta sola;
- il salvataggio non tiene più insieme tutti i risultati;
- durante una serie lunga avviene meno spesso, quando diventa costoso.

Su quel modello preparare il salvataggio della copia da 4 GiB richiede 2,4 s.

## Librerie

Release AU, come la 1.17.6: GPCModel 5.38.0.0 (Model `2f934f9f`) e Checker `6156e4ce`.

## Guide

Guide pratica e teorica Rev68, come la 1.17.6: non descrivono ancora il pannello dello spazio, gli spessori nella finestra delle sezioni e
il calcolo in serie delle combinazioni.

## Limiti noti

- La full di rilascio non è stata eseguita su questo commit: sono passate le suite del modulo Modelli, della shell e dell'architettura
  elencate nel verbale.
- Calcolare le combinazioni solo quando servono, invece di salvarle, è una decisione ancora aperta (D4 del piano).
- Restano quelli della 1.17.6.
