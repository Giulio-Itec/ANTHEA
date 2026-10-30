# Preparazione di ANTHEA 1.16.2

Richiesta del 30 ottobre 2026: prima del setup aggiungere il materiale modificabile nella tabella delle barre del modulo Sezione in c.a.; fare lo stesso per i trefoli e mostrare la tensione di tiro iniziale.

## Contenuto

- Base main ada429e2, con le Azioni già integrate e il set ufficiale AO. Branch di lavoro codex/release-1.16.2; nessuna riscrittura di refactoring/f5.
- Barre: scelta del materiale per riga subito dopo ID e coordinate. «Del foglio» segue il materiale generale; catalogo e materiali personalizzati salvati assegnano una copia delle proprietà alla singola barra.
- Il materiale entra nelle classi GPC della sezione, nelle analisi N–M e tensionali e nella persistenza nativa. Cambiare le coordinate invalida l'archivio precedente e ricostruisce la sezione conservando i materiali assegnati. Ripristina barre da wizard elimina anche le assegnazioni individuali.
- Trefoli/cavi: materiale e tensione iniziale σp0 [MPa] accanto alle coordinate; uso della predeformazione GPC già presente. Il cambio di materiale conserva σp0, che viene nuovamente validata.
- Limiti SLE distinti per barra nell'interfaccia e nell'esportazione quando diversi; materiali individuali nella relazione completa e sintetica.
- Le staffe conservano l'acciaio generale. I modelli accessori che accettano un solo parametro rifiutano esplicitamente gli input incompatibili: dettagli, ancoraggi e torsione per fyk diverso dal generale; fessurazione e taglio MC2010 per Es diverso; M–χ per fyk o Es diverso. Non si sostituiscono valori né si fanno medie tra materiali. B450A e B450C, con uguali fyk ed Es, restano compatibili.
- Guide globali Rev64, con Word e PDF; originali Rev63 e istruzioni del setup conservati in supporto/SUPERATI/rilascio-1162-rev64-20261030, con manifest SHA-256.
- Versione 1.16.2 richiesta dall'utente. Librerie AO invariate: nessun nuovo calcolo normativo né nuova build GPC.

## Verifica e differenze

Cattura precedente: full su 02cd4d2d, cartella supporto/artefatti/ci/20261030-182344-azioni-merge-official-full; main ada429e2 contiene solo i successivi verbali. Copia della schermata iniziale e impronte dei sorgenti in supporto/artefatti/rilascio-1.16.2/prima.

R-1162-MAT: nuove colonne e ordine dei trefoli intenzionali. Le tracce storiche mantengono la proiezione delle vecchie colonne e dei vecchi dati di visualizzazione; attesi numerici e file storici non vengono riscritti. Prove dedicate verificano separatamente il contratto nuovo, le celle WPF reali e il passaggio ai materiali GPC.

Prova dedicata ConcreteMaterialEditors.Tests: 17 controlli PASS, corsa 20261030-204922-release-1162-materials-reports2. Comprende σ=Es·ε con tolleranza 1e-7 MPa, equilibrio della trazione pura a 100 kN con tolleranza 0,1 N, materiali distinti, predeformazione del trefolo, persistenza e input non validi. Architettura PASS nella corsa mirata precedente, senza aumento delle baseline.

Collaudo UI mirato: 234 controlli PASS (20261030-203856-release-1162-materials-ui), comprese le celle reali nei tre temi, tracce, disegni ed esportazioni storiche. Rifinite successivamente le larghezze delle colonne dei trefoli; da riconfermare nel collaudo finale.

Confronto F3 della corsa 20261030-204457-release-1162-f3: 1.201 file, sole differenze editoriali nei due articoli della sezione in c.a. (Markdown e testo). Registrate 613 voci nuove e 595 superate per gli spostamenti delle righe; attesi ingegneristici e ricerche invariati. Riconfronto completo della stessa cattura in 122 gruppi: zero differenze non ammesse, 80.854 transizioni applicate su 80.854. Evidenza: supporto/artefatti/rilascio-1.16.2/confronto-completo.json.

Guide: 123 pagine pratica e 200 teorica; 1.297/2.458 frammenti, 9/1.493 formule e 26 parti del modello ITEC conservate per documento. Controllate graficamente le pagine aggiornate, gli indici, i frontespizi e le istruzioni del setup. Relazioni con materiali distinti esportate anche in PDF (3 e 2 pagine).

Collaudo finale e audit del setup: da registrare sul commit congelato. Il set AO è invariato rispetto al full già completato; eseguire profilo standard, prove UI pertinenti alla sezione e confronto F3, controllo del contrasto e verifiche del pacchetto. Suite UI: 73 prima e dopo, nessuna eliminata. Aggiunta una suite rapida dedicata ai materiali.

## Limite già aperto

L'allineamento neve EN all'emendamento A1:2015 resta da completare, come accettato dall'utente. Edizione implementata 2003+AC:2009. Non è parte della modifica alle armature.
