# Assistenza grafica uniforme dei fogli Azioni

Richiesta del 2 novembre 2026. Branch `refactoring/f5-azioni-assistenza`, da `4a1507ce`, allineato a `main` `1bce0729` con `2faa04fb`. Nessuna modifica ai motori GPC o ai documenti di input. Librerie: set AS già integrato dal lavoro parallelo, nessun nuovo rilascio di librerie in questa attività.

## Comportamento

I sedici fogli climatici e aggiuntivi condividono un pannello richiudibile di assistenza. Il focus espone etichetta, valore e unità; quote e componenti associati sono marcati nello schema. I parametri senza rappresentazione geometrica sono dichiarati come tali. Lo schema è qualitativo, separato dai grafici delle grandezze calcolate, e resta disponibile quando il risultato è invalidato. La scheda Schemi amplia il disegno. Le tavole originali, comprese le zone delle coperture, sono conservate e rese dai loro SVG incorporati con i token del tema.

La selezione di scenari idraulici, fasi ed età è bidirezionale con il caso dei risultati. I carriponte conservano il gruppo quando si cambia posizione, se disponibile, altrimenti scelgono il primo gruppo di quella posizione. La faccia d’incendio è evidenziata anche nel risultato, senza cambiare l’istante. Le altre righe restano componenti del modello; il pannello dichiara sempre contesto dei dati e caso visualizzato. Focus e selezione non creano revisioni o modifiche del documento. Nessuna variazione alle formule di traffico; Spettri non appartiene a questa modifica.

Tipografia, superfici, palette delle serie, frecce e marcatura sono comuni. I colori di stampa delle figure originali sono tradotti in token nel livello Themes. Nessun colore letterale introdotto nelle viste, nessuna dipendenza grafica aggiuntiva. Le figure SVG sono solo risorse incorporate, con risoluzione XML esterna disabilitata.

## Evidenze

- Prima: `supporto/artefatti/ci/20261102-094015-azioni-assistenza-before`, build e collaudo WPF originali PASS.
- Candidato conclusivo: `supporto/artefatti/ci/20261102-102115-azioni-assistenza-candidate`, 8 voci PASS, zero fallimenti e nessuna mutazione tracciata durante il runner. Climate.Tests 1249 controlli; WPF 824; architettura e assenza di codice di prova nella Release PASS. Inventario UI 73 prima e dopo.
- Corsa `20261102-101210-azioni-assistenza-vector`: Wiki.Tests, indice e manuale PASS; F3 119 fixture, 94 articoli e 30 ricerche, 1201 file confrontati e zero differenze non ammesse. Nessun atteso, transizione o tolleranza modificati. La segnalazione P7 di quella corsa riguardava la tabella di conversione dei colori SVG nella vista: spostata nel livello Themes e chiusa dal candidato conclusivo. Non classificata come fallimento noto.
- `supporto/artefatti/azioni-assistenza-20261102/numeric-comparison.json`: 41 CSV identici byte per byte rispetto al main 1bce0729, catturato autonomamente nella corsa `20261102-095752-azioni-assistenza-numeric-before`. Nessun atteso ricavato dal codice modificato.
- Guide Rev67: pratica 132 pagine, 1363 frammenti e 9 formule native; teoria 212 pagine, 2551 frammenti e 1566 formule native. 26 parti ITEC identiche per documento; contenuto e struttura matematica verificati. Copertina e pagine 114–115 della pratica controllate in rendering. Revisioni precedenti in `supporto/SUPERATI/guide-rev66-prima-assistenza-20261102`, con manifest SHA-256.

Le corse esplorative precedenti contengono anche un errore di permessi del sandbox, la vecchia aspettativa di immagini raster, il conteggio di campi limitato ai soli parametri generali e un tentativo di compilazione sovrapposto alla suite completa Presentation. Cause risolte senza esclusioni. Presentation completa sul primo stato della modifica: 4948 controlli passati; il collaudo conclusivo è quello mirato sopra, non un secondo full.

Integrazione locale su main e compilazione finale da registrare a chiusura. Nessun nuovo installer o tag richiesto in questa attività.
