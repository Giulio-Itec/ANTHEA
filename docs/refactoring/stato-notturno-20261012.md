# Stato aggiornato — chiusura F3, 13 ottobre 2026

**Questo aggiornamento sostituisce le istruzioni operative del checkpoint storico sotto.** F3 è chiusa, ANTHEA 1.6.0 e guide Rev40 pronte; collaudo completo con riprese mirate risolto, installer verificato. Dettagli in [chiusura-f3-20261013.md](chiusura-f3-20261013.md). Merge e push conclusi in 812d8ab6; setup definitivo costruito dal commit pulito e pubblicato 8b0da2a2.

F4 e F5 non devono iniziare. EXCEL-SOL è accodata con Elemento facoltativo; resta da distinguere il criterio Plastico/Elastico dallo stato limite importato. STG-4 reale e SEZ-3b ancora aperte. Set V invariato. Non modificare il processo ANTHEA dell’utente né avviare build nel checkout principale.

Il checkpoint seguente è conservato come cronologia: i suoi conteggi, punti aperti e proposta di prosecuzione a F4 sono superati.

---

# Stato del lavoro notturno — checkpoint 13 ottobre, 05:24

Mandato fino alle 07:19 Europe/Rome (05:19 UTC): F2 → STG → SEZ → F3 → F4 se completabile. F5 esclusa. Un commit per punto, test/versioni/merge/push autorizzati; nessun agente parallelo. Preservare la finestra primaria con documento non salvato: non chiudere, non riavviare, non compilare nel checkout primario.

## Rilasci conclusi

- F2: main a701af8a, ANTHEA 1.3.0; rapporto chiusura-blocco-f2-20261012.md.
- STG-3: main 22f6a1a, ANTHEA 1.4.0. STG-4 parziale: modello reale senza STAG/STCT; geometria attiva, reazioni stage e lettura nativa Strand7 da completare con un caso reale.
- SEZ-4: main/origin bce1e337, ANTHEA 1.5.0. Full 104 PASS, nessun fallimento nuovo; 478 controlli dati e 18 WPF sezioni. Guide Rev39, 76/144 pagine. SEZ-3b dipende dalla geometria di fase, resta aperto.
- Set V congelato: Model master/origin 7fd1fe4c; Checker master/origin 75e9adaa. 2778 test delle librerie superati, 17 Steel noti e due native skip. Installer 1.5.0 pronto, SHA-256 FC7B90F71807CE0AC038901E947A3D7D400A2D73A1D93ECA298AC6E50C7F2631.

## F3 in corso sul worktree separato

Ramo refactoring/f3-strato-applicativo in model-viewer-draft/ANTHEA; non ancora integrato in main. F3.1 riferimento congelato: 36 fixture, 458 file, 71 articoli Wiki e 30 ricerche. Ultimo riferimento headless prima di F3: corsa 20261012-225933-f2-set-t-full/baseline_cattura/cattura, 548 file. Non usare la vecchia B3 per il confronto corrente.

Punti chiusi e commit:

- F3.1 a35a7413, F3.2 1d1b45e: catture, progetti e regole automatiche.
- F3.3b c35a041: documenti, revisioni e condivisione in Application; Headless validate/inspect.
- F3.4 cadf784: report portabili e DocxWriter unico. 30 DOCX/134 XML/97 immagini equivalenti. Core è solo compatibilità TypeForwardedTo, assente dal prodotto; necessario ai vecchi consumatori esterni.
- F3.5a 0260dd9 e F3.5a.2 c8286da: moduli, scheduler, cache e unico dispatch Application; CLI calculate. 548 file equivalenti, rinomine diagnostiche esatte F3-01 registrate e provate.
- F3.6a 507dc8f e F3.6b c94ac699: pali verticali, acciaio, palificata e fondazioni.
- F3.6c 463f4cb: catalogo, ricerca e parser Wiki portabili; 240 controlli.
- F3.6d d35a20c: ponte/muri e analisi opzionali, 21 prove WPF di aggiornamento e chiusura.
- F3.6e 2565132: orizzontale/elastico; risposta FEM e cache sezioni fuori dalla vista, riuso FEM al cambio armature.
- F3.6f.1 04793a5: presentazione e capitoli materiali portabili, dati iniziali condivisi e mappatura copriferro riusata. 291 controlli Application e adattatori CLS rapidi passati.
- F3.6f.2 4240dfb: vista Materiali accorpata nel Desktop, prove materiali/report e 458 file equivalenti; report brevi WPF passati in 024042-f3-material-move-short-final.

Soglie correnti: 125 TypeRef Calculations, 36 CompositeBridge, 2 Concrete, 4 Geotechnics; 2 async void; 4 DispatcherTimer, solo gerarchia/Wiki. Nessun timer di calcolo residuo. Le fixture ingegneristiche non sono state aggiornate.

## Seguito

1. BridgeDesign F3.6g 339281e completato: ricalcolo ritardato, confronto/proposte su servizi e annullamento alla chiusura; 31 prove WPF, smoke e Application superati. Commit nel log.
2. CA F3.6h.1 f8023b3 completato: sessione e batch domini/SLE in Application, scheduler comune, 311 controlli Application e 35 WPF; 458 file equivalenti. F3.6h.2 060bb8c completato: dettagli/ancoraggi/riepiloghi portabili, 330 controlli Application. F3.6h.3 ecad955 completato: analisi opzionali e chiusura sicura, 37 prove WPF; ricerca armature e 458 file equivalenti. Segue report autonomo.
3. F3.5b.1 6bab13c completato. F3.5b.2a 1b6e4a0: cattura esatta di sei grafici; 2b abd8f94: curve in Application; 2c 33a2a62: pali/micropali/BridgeDesign autonomi con IFigureRenderer, 30 DOCX/97 immagini uguali a prima; 2d ffb28be: materiali/palificata/fondazioni, 343 controlli Application. 2e completato: muro autonomo, 21 capitoli e 55 immagini esatti, inclusa globale. 2f completato: ponte composto autonomo, corsa 033428 con 8 PASS. 2g completato: orizzontali/elastici autonomi, 33 capitoli e 458 file equivalenti nella corsa 034045. 2h.1 completato: preparazione CA identica alla vista, riuso dei metodi e confronto nella corsa 034639. 2h.2 completato: risultato CA autonomo ed esportazione condivisa, quattro baseline grafiche congelate/ripetibili (035255). 2h.3 completato: grafici domini portabili, cinque suite passate (035446). F3.5b.2 completato: dodici moduli autonomi, adattatore temporaneo eliminato, 8 PASS nella corsa 040439; 37 capitoli/85 immagini uguali alla vista e 97 immagini congelate invariate.
4. F3.7a completato: preview e proprietà CA in Application, 9 PASS e 5 PASS finali (041258), 13 controlli aggiunti. F3.7b.1 completato: ispezione CA, 32 PNG identici, 9 PASS e 5 PASS finali (042027). F3.7b.2 completato: parametri muri e protezione revisione, 376 controlli Application, quattro WPF, 24 PNG identici (042707). F3.7b.3 completato: proprietà della sezione composta in Application, dieci suite e quattro PNG identici (043222). F3.7b.4 completato: anteprime pali portabili, 388 controlli Application, 76 PNG identici (044250). F3.7b.5 completato: inventario dei membri e dieci ingressi protetti (044409). F3.7b.6 completato: tratti e proposte, 401 controlli Application, 47 WPF e 458 file equivalenti (045043), F3-04. F3.7b.7a 07ac496 corregge il solo volatile dell’orario nei report (050137); F3.7b.7b completa preview ponte/materiali muro con immagini identiche. F3.7b: verificare il confine effettivo, aggiornare guide del tool, suite complete, versione e integrazione/push a fase conclusa. Nessuna dichiarazione di chiusura se rimangono dipendenze concrete.
5. F4 solo dopo F3, entro il mandato. F5 esclusa.

La combo della finestra utente resta intatta. Limite cartesiano 4096 e preflight/scalabilità tracciati nel piano; ENV_SLU originale ha 26168 alternative. Principali e generatore automatico in coda. Nessuna modifica automatica delle combinazioni dal loro nome.

## Rilascio intermedio in preparazione

HEAD 1c783d0 sul ramo F3. Ultimi punti: b754bf1 proprietà ponte; f0cd462 anteprime pali; 8a4f9b6 audit accessi; e3f61eb tratti e proposte; 07ac496 solo timestamp nel banco report; fc89e8c preview strutturali; 1c783d0 messaggio di riapertura. Tutti con test e commit. 401 controlli Application, 47 WPF, 27 normalizzatore; 37 capitoli/85 immagini e 458 file equivalenti prima dell’aggiornamento editoriale.

Candidato 1.6.0 e guide Rev40 NON ancora committati. Docx e PDF finalizzati con Word: 78/144 pagine, 337 formule e 26 parti ITEC conservate. Skill documents/pdf lette e annunciate; marcatori eseguiti (PDF con quello del plugin PDF). Il renderer confezionato fallisce per assenza di LibreOffice: usare le pagine PDF native di Word già generate, rasterizzate con pdfium, conservando la diagnosi. Riferimenti di stile e Rev39 già verificati nelle attività precedenti; nuove pagine da controllare.

Archiviati 14 file Rev39 in supporto/SUPERATI/servizi-f3-rev40-20261013 con hash. Sorgenti Wiki indicizzati (71 articoli); le sole due sezioni editoriali cambiano (architettura e salvataggio/report). Prossimi: cattura F3, classificare e aggiornare esclusivamente i riferimenti Wiki derivati cambiati (non gli attesi ingegneristici), visual QA dei PDF, commit guide/versione, full con baseline headless aggiornata F2, installer, merge/push autorizzati del blocco concluso. F3.7b resta aperto, F4 non anticipata, F5 esclusa. Main primaria resta bce1e337 e app non toccata.

Documentazione F3-DOC conclusa: guide e indici controllati visivamente, copie PDF identiche. Cinque riferimenti Wiki editoriali aggiornati con hash e registro F3-06; 453 altri file e 36 input invariati. Corsa 20261013-051954-f3-rev40-classified-final: 10 PASS, zero fallimenti. Segue full della candidata 1.6.0; nessuna nuova modifica di calcolo prevista durante il collaudo.
