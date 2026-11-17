# ANTHEA 1.19.2 - note di rilascio

Note del 17 novembre 2026, preparate per la distribuzione dopo l'approvazione dell'utente.

La 1.19.2 corregge il modulo Modelli dopo la prova su un modello MIDAS reale: la memoria nelle serie di combinazioni, «Libera» e «Annulla», la riapertura dei progetti grandi e la preparazione dei casi base. Comprende anche:
- la griglia degli strati in sola lettura nelle revisioni precedenti;
- i nomi con «_» nelle caselle;
- le guide Rev73.

Le librerie GPC sono le stesse della 1.19.1 e i risultati dei calcoli non cambiano. Non c'è un formato nuovo dei documenti: l'incremento resta di tipo patch.

Il testo è anche l'annotazione prevista del tag `v1.19.2`.

## Librerie e risultati

ANTHEA mantiene lo snapshot ufficiale `gpc-release-20261114-aw` della 1.19.1, con le stesse quattordici librerie. I risultati dei calcoli sono identici bit per bit a quelli della 1.19.1: la prova completa delle suite non ha differenze nei calcoli.

## Modulo Modelli

Misure su un modello MIDAS reale con 48 casi e 73 combinazioni (PC di prova con 68 GB). I tempi dipendono dal PC.

- **Calcola tutte.** Prima, la serie accumulava i risultati di ogni combinazione e non restituiva la memoria: picco di 26,5 GB, con 26 GB ancora occupati alla fine. Ora il picco è di 21,7 GB e alla fine ne restano circa 14 GB. Alla fine della serie la finestra può fermarsi 4–5 s mentre la memoria torna a Windows.
- **Libera e Annulla.**
  - Dopo «Libera tutte» la memoria scende verso 7 GB; prima ne restavano 25.
  - Accanto a «Libera» c'è «↶ Annulla Libera». Questo comando e ↶ Annulla del progetto riportano i risultati senza fermare a lungo la finestra; prima serviva fino a 90 s.
- **Riapertura.** La copia di un modello grande si legge in un altro thread, all'apertura del progetto e dopo «Annulla». La scheda del modello mostra «Lettura del modello e dei risultati salvati… N %» e la finestra resta utilizzabile; prima restava ferma circa 64 s.
- **Casi base.** Appena il modello è sullo schermo, e dopo un'importazione, ANTHEA prepara in background i casi base. Così la prima combinazione parte subito; prima aspettava circa 24 s. Con meno di 8 GiB liberi la preparazione non parte da sola.
  - Il passaggio ad altre schede non la ripete.
- **Campione e File › Apri.**
  - Il campione dei risultati dice «Importato» per i valori letti dalla sorgente.
  - File › Apri su un file di modello (`.antheamodel`, `.mct`, `.mgt`, `.st7`) spiega dove si importa e mostra la pagina Modelli.
- **Straus7 con più freedom case.** Quando un modello Straus7 usa più freedom case, il pannello di importazione dice perché non ci sono casi di risultato selezionabili, elenca i casi esclusi e gli avvisi. In questo caso «Tutto» importa il modello senza risultati, cioè geometria, attributi e carichi.
- **Nomi con «_».** Nelle caselle dei casi, delle fasi e delle viste salvate i nomi si vedono interi: «SLU_Q1» e non «SLUQ1». Prima il primo «_» veniva preso come segno di un tasto di scelta rapida. I nomi della copia importata erano già giusti.

## Progetti

- **Strati del terreno nelle revisioni precedenti.** Nella stratigrafia riorganizzata, la griglia degli strati di una revisione precedente è di nuovo in sola lettura, come le altre tabelle storiche.

## Guide

Guide globali Rev73, con le edizioni Word e PDF della pratica e della teorica. Il capitolo «Modelli e importazione da MIDAS» descrive i comandi di questa versione. La Wiki dell'applicazione cambia solo in quella pagina.
