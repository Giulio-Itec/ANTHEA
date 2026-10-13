# Stato del lavoro notturno — checkpoint 13 ottobre, 01:59

Mandato fino alle 07:19 Europe/Rome (05:19 UTC): F2 → STG → SEZ → F3 → F4 se completabile. F5 esclusa. Un commit per punto, test/versioni/merge/push autorizzati; nessun agente parallelo. Preservare la finestra primaria con documento non salvato.

F2 approvato chiuso, main a701af8a, ANTHEA 1.3.0; rapporto chiusura-blocco-f2-20261012.md. STG-3 chiuso, main 22f6a1a, ANTHEA 1.4.0; rapporto chiusura-stage-20261012.md. STG-4 resta parziale: modello reale senza STAG/STCT. Nessuna simulazione della geometria attiva, reazione stage o lettura nativa stage Strand7 dichiarata disponibile.

SEZ su refactoring/sez-modelli: d44ba4d inventario, a64a4c1 dettaglio, f58abeb volumi condivisi. SEZ-4 chiuso, main/origin bce1e337: full 104 PASS/0 fallimenti, 478 controlli dati e 18 WPF sezioni. Guide Rev39, 76/144 pagine; ANTHEA 1.5.0. Set V installato da Model master/origin 7fd1fe4c e Checker master/origin 75e9adaa; 2778 test delle librerie superati, 17 Steel noti e due native skip. SEZ-3b legato alla geometria di fase resta aperto.

Installer 1.5.0 pronto, SHA-256 FC7B90F71807CE0AC038901E947A3D7D400A2D73A1D93ECA298AC6E50C7F2631. Ramo corrente refactoring/f3-strato-applicativo. F3.1 chiuso: 36 fixture, 458 file equivalenti nella doppia cattura D/E, 71 articoli Wiki e 30 ricerche; riferimento congelato in tests/fixtures/f3. Guardia automatica ricollaudata: 20261013-012127-f3-baseline-guard, 2 PASS. F3.1 commit a35a7413. F3.2 chiuso: scheletro, tool Headless/help, suite Architecture.Tests e controllo distribuzione; 7 PASS con equivalenza e 5 PASS finali. Prossimo F3.3b: documenti, progetti, revisioni e servizio di apertura/salvataggio. Audit F3: 12 moduli effettivi; toolkit ANTHEA.Testing da riusare, UiTests resta configurazione separata. Baseline TypeRef Desktop: 145 Calculations, 36 CompositeBridge, 3 Concrete, 6 Geotechnics; 11 async void. Sei timer di calcolo, distinti da gerarchia/Wiki. Preparazione delle catture nei soli artefatti primari supporto/artefatti/refactoring/f3/preparation. Non ancora spostato codice di produzione F3.

Combo della finestra utente: limite cartesiano 4096 diagnosticato, preflight/scalabilità tracciati e aperti; documento intatto. Principali e generatore automatico restano in coda. Conservare archivi e attesi indipendenti, non duplicare metodi già presenti.

F3.3b chiuso: documenti, revisioni e condivisione in Application, Headless validate/inspect; 121 controlli dedicati, 12 PASS e 458 file equivalenti (013204-f3-documents-built). Prossimo F3.4: report condivisi e composizione senza serializzare/riaprire i singoli DOCX.

F3.4 chiuso: report trasferiti, DocxWriter unico, progetto senza ZIP intermedi. Core compatibilità solo TypeForwardedTo, assente dal prodotto. Cattura 013734 e confronto profondo 30 DOCX/134 XML/97 immagini senza differenze; tests Reports 14 controlli + confronto corpus. Prossimo F3.5 servizi moduli/scheduler e F3.6 collegamento graduale; Wiki da estrarre.

F3.5a completato: servizi, cache, scheduler, 185 controlli e CLI calculate. F3.5b autonomizzazione report ancora aperto. Prossimo F3.6 primo collegamento UI su pali verticali, poi moduli tipizzati e Wiki.

F3.6a chiuso: pali verticali con servizi/scheduler, 9 prove WPF dedicate, 10 PASS inclusa equivalenza. Prossimi: ingressi di calcolo tipizzati e Wiki. Ultimi commit c35a041 documenti, cadf784 report, 0260dd9 servizi.

F3.6b completato: acciaio, palificata, fondazioni; corsa 015601 12 PASS/0 nuovi. Prima di altri moduli conviene unificare l’ingresso CalculationService che resta in X.Calculations con l’orchestrazione Application; nessun caller dentro X.Calculations, solo UI e test. I confronti numerici sono congelati, non aggiornare attesi.

Checkpoint 02:12: unificazione CalculationService completata, 548 file headless equivalenti; rinomine diagnostiche F3-01 registrate e collaudate. Prossimo: estrazione Wiki, script preparato solo negli artefatti f3/extract-wiki.py. F3.5b/report autonomi e F3.6 resto moduli ancora aperti, F4 non iniziata, F5 esclusa.

Checkpoint 02:15: F3.6c Wiki chiuso; 240 controlli portabili, 458 file equivalenti, rendering Wiki e aiuto fondazioni passati. Prossimi moduli: ponte composto e muri con coordinamento comune; restano report autonomi, materiali CLS, orizzontale/elastico, CA e chiusura F3.

Checkpoint 02:22: F3.6d ponte/muri chiuso, 11 PASS + 6 PASS di schedulazione, 21 controlli WPF e 458 file invariati. F3.6c Wiki in 463f4cb, unificazione dispatch in c8286da. Prossimo orizzontale/elastico: preservare cache FEM e cache delle sezioni, migrazione elastica sempre esplicita. Restano F3.5b/report, CLS, CA, BridgeDesign ritardato e chiusura; F4 non iniziata, F5 esclusa. La sessione utente primaria resta intatta.

Checkpoint 02:31: F3.6e pali orizzontali/elastici completato, 199 controlli Application e tutte le prove dei pali passate; 458 file equivalenti. Zero timer di calcolo residui. Prossimo F3.6f: separare la presentazione dei materiali CLS per riusarla nei report autonomi; poi CA, BridgeDesign, report progetto e chiusura F3. F4 non iniziata, F5 esclusa.

Checkpoint 02:38: F3.6f.1 materiali portabili e capitoli senza vista completato: 291 controlli Application, prove WPF e 458 file equivalenti, adattatori CLS passati. Prossimo accorpamento fisico della vista Materiali nel Desktop; poi BridgeDesign e CA/report.
