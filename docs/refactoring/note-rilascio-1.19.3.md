# ANTHEA 1.19.3 - note di rilascio

Note del 17 novembre 2026, preparate per la distribuzione dopo l'approvazione dell'utente. Descrivono solo le differenze dalla 1.19.2.

La 1.19.3 importa da Straus7 i modelli con più freedom case con i loro risultati e le loro combinazioni. Comprende:
- le librerie GPC del set AY, con il convertitore GPC 2.25;
- il pannello di importazione Straus7 con il freedom case di ogni caso e i casi esclusi con il motivo;
- le guide Rev74.

Non c'è un formato nuovo dei documenti né un comando nuovo: l'incremento resta di tipo patch.

Il testo è anche l'annotazione prevista del tag `v1.19.3`.

## Librerie e risultati

ANTHEA passa dallo snapshot ufficiale `gpc-release-20261114-aw` della 1.19.2 al set ufficiale `gpc-release-20261117-ay`. Fra i due set cambiano solo i sorgenti del convertitore GPC, GPC.Converter 2.25.0.0 (2.22.0.3 nella 1.19.2). Le altre librerie sono ricompilate dagli stessi sorgenti con un numero di versione nuovo, quindi i risultati dei calcoli dei moduli non cambiano.

Il convertitore cambia solo l'importazione da Straus7, e in più:
- una combinazione della sorgente con lo stesso nome di una precedente dà ora l'avviso `CombinationNameRepeated`, per ogni sorgente; prima veniva saltata senza dirlo;
- la scrittura dei file .st7 delle sezioni aperte (T, C, L), che ANTHEA non usa.

## Importazione da Straus7

- **Più freedom case.** Prima ANTHEA importava questi modelli senza risultati, perché il convertitore non associava i casi di risultato ai casi di carico. Ora i casi di risultato sono selezionabili: su un modello con due freedom case, 15 casi su 15.
  - Nell'elenco dei casi il nome è seguito dal freedom case: «5: G1 · FC 2». Nella copia il caso conserva il nome del caso di carico, «5: G1».
  - Il riepilogo del controllo elenca i freedom case del modello, ciascuno con i suoi casi di carico.
  - Le combinazioni LSA ci sono tutte. Due combinazioni con lo stesso nome in freedom case diversi diventano «SLE_QP · FC 1» e «SLE_QP · FC 2».
  - Le reazioni di ogni caso sono quelle di Straus7 nel suo freedom case, e il messaggio del controllo di equilibrio porta il freedom case.
  - Vincoli e molle dei freedom case restano dati della sorgente: la copia non ne disegna i simboli.
- **Casi di risultato esclusi.** I casi che il convertitore non associa a un caso di carico sono in un elenco a parte del pannello, ciascuno con il motivo in italiano, preso dal codice del motivo del convertitore. L'elenco c'è anche con un solo freedom case, quando un caso di risultato non corrisponde a un caso di carico.
- **Equilibrio non valutato.** Un caso con pressioni su piastre che il controllo di equilibrio non può integrare, perché non piane entro 1e-7 mm, è ora «non valutato». Prima l'errore fermava tutta l'importazione.
- **Tempi sui modelli grandi.** La lettura dei risultati delle piastre e il controllo delle reazioni non crescono più con il quadrato del modello: sul modello di prova con 5142 piastre e 15 casi la lettura con «Tutto» dura meno di un minuto.
- **Nomi con «_».** L'elenco dei casi mostra interi anche i nomi con il freedom case, per esempio «9: Q_A · FC 2».

## Guide

Guide globali Rev74. La pratica, nel capitolo «Modelli e importazione da MIDAS», descrive l'importazione dei modelli Straus7 con più freedom case; la teorica, in «Architettura del calcolo e convenzioni», ne spiega casi, reazioni e combinazioni. La Wiki dell'applicazione cambia solo in queste due pagine.
