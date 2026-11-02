# ANTHEA 1.17.3 — note di rilascio

Note del 2 novembre 2026. La 1.17.3 corregge l'interfaccia dei sedici fogli Azioni e del foglio Viscosità e maturazione, dopo una revisione indipendente del lavoro della 1.16.2–1.17.1. Calcoli, formule e risultati non cambiano.

Contiene le librerie GPC della release AT e le guide Rev68, come la 1.17.2. Il numero segue la regola delle correzioni.

Il testo è scritto per chi usa ANTHEA ed è anche l'annotazione del tag `v1.17.3`, sul commit del setup. Il verbale tecnico del rilascio è in [installer-1.17.3.md](installer-1.17.3.md); i rilievi corretti in [azioni-ui-revisione-20261102.md](azioni-ui-revisione-20261102.md).

## Correzioni dei fogli Azioni

- **Risultati visibili all'apertura.** Nove fogli aprivano su una scheda nascosta: l'area dei risultati restava vuota, o mostrava il testo dell'incendio, anche dopo il calcolo. Erano Neve, Vento e Temperatura degli edifici, Vento e Temperatura dei ponti, Carichi, Traffico, Urti e Combinazioni. Ora si apre «Casi di carico», oppure la scheda propria del foglio (pressioni, fasi, facce, vie di corsa…).
- **Calcolo automatico.** Il foglio calcola all'apertura: nuovo foglio, file o progetto riaperto, revisione, Annulla e Ripristina. Prima i risultati comparivano solo dopo aver modificato un dato. Il testo «quindi Calcola», che citava un comando assente, è sostituito dallo stato del calcolo.
- **Motivo del calcolo mancato.** Quando il calcolo non dà risultati, un riquadro sotto il titolo dice perché: dato da completare, conferma o fonte da documentare, valore fuori campo. Con i dati iniziali, per esempio, Temperatura negli edifici, Vento sui ponti, Ripartizione, Precompressione, Gru e Incendio chiedono una conferma o una fonte.
- **Testo in un campo numerico.** Un testo come «abc» in «Quota sul mare» bloccava il cambio di scheda, Annulla e la relazione, con il messaggio «Campo non valido… $.AltitudeM». Ora:
  - l'errore compare sotto il campo;
  - il messaggio usa l'etichetta del campo e, nelle tabelle, la riga;
  - il foglio si conferma, si salva e si riapre normalmente.
- **Più fogli Azioni aperti.** Ogni foglio conserva la scheda scelta, l'assistenza e il diagramma aperti o chiusi, anche passando da un foglio all'altro.
- **Schermi piccoli.** Sul foglio più piccolo della finestra (1120 × 600) l'assistenza e il diagramma si riducono e la tabella del caso resta leggibile. Con spazio sufficiente il diagramma è intero.
- **Separatore decimale.** I valori dei campi e i nomi delle righe usano il separatore scelto in «Aspetto», come i risultati.
- **Guida dai campi.** Ogni campo ha «?» e F1, che aprono la sezione della guida del foglio.
- **Tastiera.** Dopo una scelta che cambia i campi mostrati, il cursore resta sulla scelta.

## Viscosità e maturazione

- Valori, tabella e assi del grafico seguono il separatore scelto in «Aspetto». Prima usavano sempre la virgola e il punto delle migliaia: con il punto decimale Ecm,28 appariva «34.077 MPa».
- Su uno schermo basso la colonna del grafico scorre: il grafico non si riduce più a una striscia illeggibile.

## Librerie

Release AT, come la 1.17.1 e la 1.17.2 (Model `5449e4d1`, Checker `a2acd3d2`). Le versioni delle DLL sono in `lib/Checker/README.md`.

## Guide

Guide pratica e teorica Rev68, come la 1.17.2. Descrivono già messaggi e invalidazione dei risultati; il calcolo automatico all'apertura e il «?» dei campi Azioni entreranno nella prossima edizione.

## Limiti noti

- La full di rilascio non è stata eseguita su questo commit: sono passate le suite elencate nel verbale.
- Spettri e Stratigrafia, come prima, calcolano dopo la prima modifica dei dati, non all'apertura.
- Restano aperti piccoli dettagli della revisione:
  - nel foglio Viscosità e maturazione, una riga scelta nella tabella segna il foglio come modificato e mostra l'età con molte cifre;
  - il diagramma dei casi parte dai coefficienti adimensionali;
  - alcuni nomi per l'accessibilità mancano.
- Restano quelli della 1.17.2.
