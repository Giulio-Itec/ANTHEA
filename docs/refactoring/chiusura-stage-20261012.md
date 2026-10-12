# Risultati per fasi — rilascio ANTHEA 1.4.0

STG-1 e STG-2 completati nei commit 65fa556 e 5952558 di ANTHEA; dominio e lettori Model pubblicati su master 3c357622. STG-3 comprende importazione con scelta esplicita di fase/passo/caso, riferimento degli spostamenti, filtro della vista, archivio formato 2 e capacità dichiarate per Strand7. Architettura MVVM: protocolli e adattamento senza WPF; nessuna formula nuova nella UI.

## Verifiche prima del rilascio

Cattura dei controlli esistenti prima dell'estrazione dei mapper: 20261012-233557-stg-before-adapter-valid. Dopo il trasferimento, con il candidato V identico al set U: 461 controlli dei dati e 21 WPF, runner 20261012-234913-stg-format2-final, 4 PASS. Verificati segni, unità, identificativi, copertura, tre riferimenti degli spostamenti, errori, revisione sorgente, salvataggio/riapertura e separazione dai casi statici. I valori attesi indipendenti e le tolleranze non sono cambiati.

Il set U è non candidato, prodotto da commit già pubblicati di Model e Checker; il manifest completo è in lib/Checker/release-set.json. La correzione del registro concorrente del solutore è descritta in STG-02 del registro delle differenze. Il primo candidato U falliva il controllo preesistente 939/940; il candidato V e il rilascio U superano le suite. Nessuna nuova eccezione in known-failures.

Full ANTHEA 20261012-235957-stg-set-u-full: 102 PASS, zero fallimenti noti e un rilievo editoriale Wiki. Il metadato related del nuovo capitolo teorico conteneva il nome del programma sorgente: rimosso il collegamento, senza cambiare il controllo né aggiungere eccezioni. Ricontrollo 20261013-002139-stg-wiki-build-final: build WPF, indice, manuale e vista Wiki, 4 PASS, zero NEW-FAIL. Confronti delle catture precedenti, numerici, concorrenza e tutte le prove WPF superati.

Guide Rev38: 74 pagine pratiche e 143 teoriche, tutte renderizzate e controllate graficamente; verificati anche i quattro PDF degli indici. Conservate 337 formule editabili e 26 parti opache del modello Word. Revisioni precedenti archiviate con registro in supporto/SUPERATI/stage-rev38-20261012. Evidenze in supporto/artefatti/guide_anthea_itec_rev38/qa.

## Limiti ancora aperti (STG-4)

La sessione reale MIDAS risponde, ma le tabelle STAG e STCT sono vuote. Il collaudo dei risultati è sintetico: non sostituisce un confronto numerico con un modello realmente analizzato per fasi. La geometria completa resta un contesto; le attivazioni e disattivazioni non sono applicate alla scena come simulazione del sistema resistente. Le reazioni stage e la lettura nativa stage di Strand7 non sono disponibili. I passi devono essere indicati con le etichette esatte; non vengono inventati dal numero o dalla durata delle fasi. I casi stage non entrano nelle somme statiche. Questi limiti compaiono nelle guide globali e nella vista.

Dopo il rilascio si prosegue con SEZ, poi F3 e F4 per quanto completabile. F5 esclusa. Non è stata chiusa né modificata la finestra primaria con documento non salvato.
