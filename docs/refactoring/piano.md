# Refactoring di ANTHEA: piano operativo

Piano approvato il 6 ottobre 2026. Decisioni e architettura: [decisioni.md](decisioni.md).
Il dettaglio di ogni passo (file, comandi di verifica, criteri di uscita) è stato preparato
in `supporto/artefatti/refactoring/piano/` (cartella non versionata).

**[CP]** indica un punto in cui serve l'approvazione dell'utente. Lo stato si aggiorna a
ogni commit: `[x]` fatto, `[~]` in corso, `[ ]` da fare.

## Regole valide per tutte le fasi

1. Un branch per fase, commit piccoli, merge su main a fine fase con l'approvazione
   dell'utente; push solo su richiesta. Tag `pre-`/`post-<fase>` e `pre-lib-<versione>`;
   per tornare indietro `git revert`, mai un checkout di commit vecchi.
2. Ogni spostamento di codice: cattura prima, modifica, confronto dopo, con un solo toolkit
   di confronto e tolleranze per grandezza. I report si confrontano sui numeri, non
   lettera per lettera.
3. Un solo registro delle differenze e degli scostamenti (`registro-differenze.json`).
4. Gli attesi indipendenti (oggi 432 dei 464 casi hanno un atteso Python) non si
   sovrascrivono mai; un nuovo atteso si affianca e si verifica in modo metamorfico.
5. Ogni correzione tecnica in due commit: collegamento con l'opzione legacy (identico),
   poi cambio di opzione (differenze uguali a quelle previste dalla scheda).
6. La pagina del metodo (revisione tecnica) si approva prima di eliminare il legacy della
   famiglia.
7. `lib/Checker` si rilascia solo da commit pushati (`tools/libs/Update-Snapshot.ps1 -FromUpstream`),
   con versione successiva a quella del manifest, suite complete e controllo delle mesh DelaunayMesh.
8. AGENTS.md cambia tre volte: fine F0, fine ristrutturazione Wiki, fine F5.
9. Pacchetti esterni: elenco unico in `decisioni.md`.

## Fase 0: messa in sicurezza e riferimenti

- [x] F0.1 node_modules e `supporto/tmp` fuori dall'indice, junction tolta, esempi versionati (9708530).
- [x] F0.2 [CP] Decisioni di avvio, `docs/refactoring`, prima revisione di AGENTS.md (approvata il 6/10/2026).
- [x] F0.3 Runner `build/ci.ps1` (profili quick, standard, full), `build/known-failures.json`, `Verifica.cmd` non interattiva. La prima corsa ha scoperto 5 harness fermi per DelaunayMesh 2.0.0.8 e un alias Wiki rotto.
- [x] F0.4 Baseline B0: catture headless degli 11 moduli e dei motori non coperti dal servizio, griglie dense di CheckerMigration.Capture, testo dei report, impronta delle mesh, registro dei default silenziosi; doppia corsa.
  F0.4a: toolkit unico `tests/ANTHEA.Testing` (capture, compare, normalize; tolleranze per grandezza in `tolerances.json`, default 0), B0 headless in `supporto/artefatti/baseline/F0-B0/headless` (118 casi, motori fuori dal servizio, testo dei report, archivi, registro dei ripieghi di `J.S/D/B`), doppia corsa uguale a meno dei tempi `tempi_ms`; stadio e profilo `baseline` in `build/ci.ps1`.
  F0.4b: catture dense di CheckerMigration.Capture e impronta di 80 mesh in `supporto/artefatti/baseline/F0-B0/dense`, ripetibili byte per byte (a meno di identificativi casuali). Restano da aggiungere le uscite normalizzate delle prove WPF.
- [x] F0.5 [CP] Diagnosi dei fallimenti noti. `--project-calculations` smascherato (122/123; l'unico fallimento è un difetto del test: confronta un GUID casuale dei default dei muri, chiuso in F3.3); BridgeAudit (9 fallimenti dal commit 0a63315: etichetta del metodo, ripristinata con D7-g, e convenzione `h_trave` nelle fixture, corretta in Checker ecf1dbe0: 302/302); alias Wiki corretto con W0.5; 8 prove WPF (non 6) che fallivano già in 2cdf9fd, 11 cause, 8 commit (rapporto in `supporto/artefatti/refactoring/diagnosi-ui/rapporto.md`): due correzioni del disegno del ponte da confermare (titolo del tag dell'anima inclinata, tag senza sovrapposizioni a 1366 × 900). Profilo full: 0 NEW-FAIL.
- [x] F0.6 [CP] Schede degli scostamenti (a)–(g) con effetto quantificato in `scostamenti.md` e registro unico `registro-differenze.json`. Decisioni dell'utente del 6/10 in `decisioni.md`; (b) da discutere. Esiti delle ricerche e voci nuove R1-R14 nel registro (7/10).
- [x] F0.7 Build: proprietà `GpcLibDir`, elenco esplicito delle DLL, verifica SHA-256 (5 harness di nuovo eseguibili) e `.gitattributes` (controllo Wiki valido anche su checkout LF). Anticipato prima di F0.4.
- [x] F0.8 [CP] `tools/libs/Update-Snapshot.ps1`, global.json nelle librerie (SDK 9.0.318), versioni nuove (commit locali non pushati), snapshot S1 da commit, build deterministiche verificate.
- [x] F0.9 Snapshot S1 installato in `lib/Checker`: suite invariate, cattura identica a B0, griglie dense identiche, 80 mesh identiche bit per bit, test delle librerie con i soli fallimenti storici (due aspettative superate corrette in Geometry e Model). Librerie pushate il 7/10 su richiesta dell'utente (Utilities df3b3e7, Geometry 6a0d1c1, Model d6631635, Checker develop 7c15cf4f).
- [x] F0.10 Correzioni a costo zero (puntatori rotti, README, riferimento esterno nel report Bridge Design: 79da697); D7-g ripristinato (0d861de); runner con `dotnet exec` (f4d81ad).
- [x] F0.11 [CP] Baseline B1, tag, merge e push su autorizzazione. B1 in `supporto/artefatti/baseline/F0-B1` (commit b4d2e81; rispetto a B0 solo le 6 righe di testo volute di F0.10 e D7-g); tag locali `refactoring/post-f0` e `refactoring/pre-f1`. Fase approvata dall'utente il 7/10 e unita a main in locale insieme a F1, D7-c/D7-d/R3 e W0.4/W0.5; push di ANTHEA da confermare.

## Fase 1: test fuori dall'eseguibile

- [x] F1.1 Rompere la dipendenza produzione → test (`HorizontalWorkspace.Smoke`, `HorizontalChs.VerifyChs`, sonde di `BridgeTorsion`) (429e4c0).
- [x] F1.2 Configurazione `UiTests` con harness e flag; exe pubblicato pulito; installer che blocca codice di test; log degli errori non gestiti (d11af03).
- [x] F1.3 Autotest fuori da X.Materiali e dal motore (e805280).
- [x] F1.4 [CP] `IConfirmationService` e `IMessageService` al posto degli agganci di test; 4 scenari verificati sull'exe Release. Codice fatto (6468b7d); prove WPF a schermo superate (profilo full sul merge con F0 e diagnosi UI: 66 PASS, 1 KNOWN, 0 NEW-FAIL). Scenari eseguiti il 7/10 dal coordinatore su delega dell'utente, da ratificare, con UI Automation e mouse reale sull'exe Release di main 17b6c98 e i dialoghi di produzione, esiti controllati rileggendo i file salvati (`supporto/artefatti/refactoring/f1.4/esito.md`): avvio con la Home; dati condivisi («Aggiorna tutti i fogli collegati» e «Solo questo foglio»), conferma spostamento («Annulla» e «Sposta e aggiorna»), chiusura con modifiche («Annulla», «No», «Sì») ed errore di un comando (messaggio «Operazione non completata», applicazione viva) superati. `errori.log` registra solo gli errori non gestiti, come prima di F1: l'errore di un comando non lo scrive.
- [x] F1.5 `tests/ANTHEA.Desktop.UiTests` non creato: `--check-wiki-offscreen` usa 16 membri privati di MainWindow; resta la configurazione `UiTests` e le suite migrano modulo per modulo in F5 (motivi in [progetti-di-test.md](progetti-di-test.md)).
- [x] F1.6 [CP] Tabella "destino dei progetti di test" in [progetti-di-test.md](progetti-di-test.md), decisa il 7/10 dal coordinatore su delega dell'utente, da ratificare; ConcreteDesign.DesktopChecks e ValidationIllustrations portati nella configurazione `UiTests` e spostati in `supporto/SUPERATI/test`. Esito: archiviati nella stessa cartella, invariati e con registro (`supporto/SUPERATI/registro-20261007-test.json`), BridgeDesign.SiteComparison, MaxRetainingWall.Cases e .Compare, ConcreteStressDiagnosis, ValidazioneCA20260925 e ProgrammaAnthea (4ff5026, f5647f5, d01caf7); profilo quick 18 PASS, 0 NEW-FAIL. Seguito nell'integrazione 2: generatore della relazione del 25/9 archiviato in `supporto/SUPERATI/script/validazione_ca_2026_09_25` con `build_document.py` al testo originale; ElasticPile.Performance riportato in `supporto/test` con la destinazione della tabella (3ff710e, a57aff6, cb9b750, a2a3ac1; tabella e documenti 99b2ae7); MaxRetainingWall.Cases e .Compare restano archiviati (scelta reversibile); le cartelle `bin` e `obj` ignorate rimaste nelle vecchie posizioni non si cancellano; profilo quick `int2-f16` 18 PASS, 0 KNOWN, 0 NEW-FAIL. Seguiti decisi dal coordinatore su delega, da ratificare ([decisioni.md](decisioni.md)).
- [x] F1.7 Chiusura: stessi esiti di B1. Cattura headless del branch F1 uguale a B1 su 432 file (solo campi volatili); profilo full 66 PASS, 1 KNOWN (project-calculations, F3.3), 0 NEW-FAIL; tolte da `known-failures.json` le due voci WpfMath dei progetti spostati in SUPERATI. Checkpoint F1.4 (scenari sull'exe Release) e F1.6 chiusi il 7/10.

Tra F1 e F2: regressione in MSTest (464 casi singoli) e `casi_confronto.json` diviso per caso [CP]; GitHub Actions per gli stadi non UI (facoltativo) [CP].

## Fase 2: chiusura del c.a.

- [~] F2.1 Cattura pre-M4 del legacy attuale; matrice delle API; casi vicini alla soglia; test con sezioni in forma chiusa. Fatti (branch `refactoring/f2-banco-confronto`, [f2.1-banco.md](f2.1-banco.md), [f2-matrice-api.md](f2-matrice-api.md)): cattura pre-M4 doppia in `supporto/artefatti/baseline/F2-pre-m4` (identica a meno degli identificativi casuali; contro B0 solo D7-c e D7-d), comando `compare-dense` con classificazione versionata, suite `baseline/banco-ca` (fixture di Checker del 1/10: 27 698 righe, 110 differenze tutte classificate). Fixture ricatturate il 7/10 (decisione B) dalla nuova cattura doppia `F2-pre-m4-v2` (d2d3225, con D7-b, D7-d d2 e R15; mesh identiche a B0): Checker 06d97733, test di migrazione senza casi speciali, banco con zero differenze e riferimento denso `F2-pre-m4-v2/a/tutte`. Restano: casi vicini alla soglia, test con sezioni in forma chiusa.
- [x] F2.2 [CP] Pagine dei metodi c.a. (revisione tecnica). Scritte in Checker (branch locali `anthea-metodi-ca-1` e `anthea-metodi-ca-2`, worktree Temp\cw-metodi1 e cw-metodi2): taglio, torsione, SLE, fessurazione, ancoraggi, dettagli, M-χ, durabilità e copriferri, con esempi a mano uguali alla libreria. Revisionate e unite in Checker develop: merge locali c555a3b8 e 1fbaea61 più le correzioni del verificatore (5675492f e 398eb36a con i merge 9eb800d4 e 26fb6b8b; bf23a16d, 7d86ffc7, bd9938ac, e41a803a). Revisione accettata il 7/10 dal coordinatore su delega dell'utente, da ratificare. Voci nuove R16-R21 nel registro (da decidere in F5.15) e R7 rettificata. Push di Checker develop (punta e41a803a, 35 commit oltre origin/develop) all'utente.
- [~] F2.3 [CP] GPCChecker.Concrete: dettagli di solette e pareti; mesh sicure in parallelo al posto del lock; tracce per i report; opzioni degli scostamenti. Fatti il 7/10 (Checker develop): D7-b con opzione legacy (0.0.16.0), R3, R15 della fascia interna (0.0.17.0), fixture c.a. ricatturate; restano solette e pareti, mesh senza lock, tracce in italiano per i report, R1 e R2 dopo il riscontro dei testi.
- [x] F2.4 [CP] Rilascio `lib/Checker` con codice ANTHEA invariato. S2 compilata dai commit pushati e verificata; profilo full a schermo sull'integrazione 2 (258e81a, corsa `20261007-184928-int2-full`): PASS 69, KNOWN 1 (`verifiche/project-calculations`), NEW-FAIL 0, tutte le prove WPF superate. Branch `refactoring/f2-4-snapshot-s2` (da main 17b6c98): revisioni nuove a sorgente invariato per SourceLink (Model 5ad56681: GPCModel 1.6.1.1, GPCModelData 0.0.2.3; Checker 0d7ba50b: Geotechnics 0.1.1.1, CompositeBridge 1.4.0.4), GPCChecker.Concrete 0.0.17.0 con D7-b e R15, Utilities, Geometry e DelaunayMesh invariate. DLL compilate con la nuova opzione `Update-Snapshot.ps1 -FromUpstream` (worktree temporanei dei rami remoti in una radice fissa, 8d6a8ec) da Utilities df3b3e7, Geometry 6a0d1c1, Model 5ad56681 e Checker 0d7ba50b, tutti in origin (edc4fce); sostituiscono il candidato f6b6fdf, compilato da Checker develop 1fbaea61 locale. Profilo standard 33 PASS, 1 KNOWN, 0 NEW-FAIL; profilo baseline 39 PASS, 1 KNOWN, 0 NEW-FAIL con esiti e conteggi uguali al candidato e a S1, cattura headless uguale a F2-B2, banco c.a. e banco denso senza differenze, 80 mesh identiche; test delle librerie superati (dettagli in `lib/Checker/README.md`). Unita nell'integrazione 2 (`refactoring/integrazione-2`: merge del candidato 0c87141, merge della ricompilazione b818b20): profilo standard `int2-std` 33 PASS, 1 KNOWN, 0 NEW-FAIL e profilo baseline `int2-base` 39 PASS, 1 KNOWN, 0 NEW-FAIL, esiti e conteggi uguali a S2 (prove in `supporto/artefatti/refactoring/integrazione-2` del checkout principale). Nota del 7/10 del branch `refactoring/f2-taglio-torsione`, scritta prima di S2 (superata nella prima parte dalle versioni nuove di S2): GPCChecker.Concrete 0.0.17.0 è pronta ma non distribuita; ricompilando dai commit nuovi cambiano per SourceLink anche gli SHA-256 di GPCModel, GPCModelData, Geotechnics e CompositeBridge a sorgente invariato, e `tools/libs/Update-Snapshot.ps1` rifiuta uno SHA diverso senza versione più alta: servono versioni nuove (o una regola dello script per i componenti a sorgente invariato) e il profilo full a schermo. Dal passo F2.6 taglio e torsione della sezione passano dalla libreria: prima di distribuire una `GPCChecker.Concrete` nuova, rieseguire `tests/ConcreteLibraryAdapter.Checks` e confrontare i testi di `Shear/` e `Torsion/` (stati, rifiuti, espressioni dei dettagli) con la mappa di `X.Calculations/ConcreteLibraryMapping.cs`. Un testo senza traduzione è un errore di programma (`InvalidOperationException`): il calcolo headless e il progetto delle armature lo registrano per riga, la scheda WPF del taglio interrompe le righe successive (`X.Desktop/Wpf/ConcreteShear.cs:125` cattura solo `ArgumentException`) e mostra un solo errore "Taglio". Oggi nessun testo della libreria raggiungibile dal modulo ne è privo (verifica avversaria su ca6530d). Nell'integrazione 2, dopo il merge di F2.5-F2.6 (ea4c51e), la condizione è verificata per S2: `Shear/` e `Torsion/` di GPCChecker.Concrete sono identici fra S1 (Checker b994e188) e S2 (0d7ba50b), quindi stessi testi, e `tests/ConcreteLibraryAdapter.Checks` dà PASS con `misura.json` identico byte per byte a quello con S1.
- [x] F2.5 Strato di mappatura con contratto JSON invariato. Taglio e torsione (branch `refactoring/f2-taglio-torsione`): `X.Calculations/ConcreteLibraryMapping.cs` (conversioni kN ↔ N e kNm ↔ N·mm con nome, nomi delle norme → classi `Standard` per le 7 norme ordinarie, risultati della libreria → DTO del JSON 'taglio' e 'torsione', testi e rifiuti della libreria → testi italiani del legacy; nessuna costante normativa, controllato dalle prove), adattatore `ConcreteShearTorsionAdapter` con l'interruttore `Default` (motore legacy in questo passo), DTO della torsione in `ConcreteTorsionContracts.cs`. `ConcreteShearAnalysis` e `CheckerMigration.Capture` (opzione `--motore legacy|libreria`) passano dall'adattatore. Prove `tests/ConcreteLibraryAdapter.Checks` (stadio fast): interruttore, motore legacy dell'adattatore identico bit per bit al legacy, motore della libreria con stessi esiti, rifiuti e testi e numeri entro 1e-9 sulle griglie della cattura densa e sui calcoli del modulo (scarto relativo massimo 9,2e-16). Runner `-Profile baseline` con `-BaselineRef F2-B2` e `-DenseRef F2-pre-m4-v2`: PASS 41, KNOWN 1, NEW-FAIL 0; cattura headless uguale a B2 (432 file, 21 tempi volatili), banco denso uguale a F2-pre-m4-v2 (32 564 righe, solo identificativi casuali) e alle fixture di Checker. Differenze dichiarate: registro F2-1…F2-4. Commit 2583225. Correzione f5b5f50 (dopo la revisione di 7176c0c): in 2583225 `ConcreteShearAnalysis.Torsion` riceveva il motore ma calcolava ancora profilo resistente e torsione con il legacy diretto, quindi con la libreria il modulo avrebbe cambiato solo il taglio; ora passa da `ConcreteShearTorsionAdapter.TorsionGeometryOf` e `Torsion` con il motore richiesto. Interruttore di nuovo sul legacy (7176c0c ritirato) e tolleranze e confronto `f2-libreria` tolti, da rimettere in un commit dedicato prima dell'interruttore. Prove nuove: 3f (la torsione del modulo coincide bit per bit con l'adattatore chiamato direttamente con lo stesso motore; il controllo distingue i motori, fallisce sul codice di 7176c0c), 4 (attesi indipendenti di `supporto/test/ConcreteCode.Checks/reference.json`, letto in sola lettura, benchmark DIN, DS, UNI, NS e forme chiuse della torsione NTC attraverso l'adattatore con entrambi i motori), letterali con il punto iniziale (`.85`) nel controllo delle costanti. Evidenze in `supporto/artefatti/refactoring/f2-taglio-torsione/correzione`. Completamento della correzione dopo la seconda verifica avversaria (8c95d9b): interruttore di nuovo sul legacy dopo il primo 85314a6 per catturare la baseline headless B3 prima dell'interruttore; prova 3f anche contro il legacy diretto con il motore Legacy; prova 3e con la 'torsione' del calcolo headless uguale bit per bit all'adattatore con il motore predefinito (riga NTC T7 con uscite diverse fra i motori: un calcolo headless che ignorasse l'interruttore fallisce, prova negativa n1). Stato legacy dimostrato sul commit del corpus B3 (dcde952, stesso codice di calcolo): runner `-Profile baseline` PASS 40, KNOWN 1, NEW-FAIL 0; cattura densa identica a F2-pre-m4-v2 con il confronto esatto `pre-m4` (32 564 righe, 1244 con soli identificativi casuali); cattura headless uguale a B2 sui 432 file comuni, salvo le voci dei casi nuovi in `fallbacks.json`. Unito nell'integrazione 2 (`refactoring/integrazione-2`) con il merge ea4c51e del branch `refactoring/f2-taglio-torsione` (punta db4da92); misure con S2 in F2.6.
- [x] F2.6 Taglio e torsione → libreria. Storia dei commit (nessuna riscrittura): 7176c0c, primo interruttore, ritirato: collegava alla libreria il solo taglio del modulo (profilo resistente e torsione restavano sul legacy diretto e le prove 3d/3e confrontavano per la torsione il legacy con se stesso), con le tolleranze nel commit dell'interruttore; correzione di F2.5 f5b5f50; 8f991b4, tolleranze dense dedicate (confronto `f2-libreria`: come `pre-m4`, 1e-9 sui soli numeri di taglio e torsione, regole `f2-libreria/` di `tolerances.json`); 85314a6, secondo interruttore, sospeso da 8c95d9b perché il corpus headless di B2 non aveva sezioni c.a. con taglio e torsione; 256714d e dcde952, quattro casi in `tests/ANTHEA.Testing/corpus` (rettangolare, circolare e circolare cava NTC con torsione, rettangolare DIN con cot θ assegnato e rifiuti; `foro_presente` dichiarato) e baseline B3 catturata con il motore legacy (`supporto/artefatti/baseline/F2-B3`, LEGGIMI); ea6e0ac, tolleranze headless dedicate (1e-9 sui numeri di 'taglio' e 'torsione' dei risultati `str_palo`, il resto esatto); ca6530d, interruttore `ConcreteShearTorsionAdapter.Default` sulla libreria e `f2-libreria` predefinito di `-DenseSet`. Il modulo sezione c.a. (WPF, calcolo headless, progetto delle armature, tutti attraverso `ConcreteShearAnalysis`) calcola taglio, profilo resistente e torsione con `SectionShearCalculator`, `TorsionGeometry.Rectangle`/`Circle` e `SectionTorsionCalculator`; il legacy resta raggiungibile (`ShearTorsionEngine.Legacy`, cattura con `--motore legacy`, prove dell'adattatore) fino alla decisione di F2.11. Equivalenza misurata su ca6530d (registro F2-1, convenzione): `tests/ConcreteLibraryAdapter.Checks` 17 696 controlli (griglie della cattura densa; modulo 263 numeri diversi su 4510, massimo 5,4e-16; torsione del modulo uguale bit per bit all'adattatore del motore richiesto in 120 calcoli, 70 con uscite dei motori diverse; calcolo headless di 5 norme entro 1e-9, 25 numeri diversi su 1159, e torsione headless uguale all'adattatore con la libreria; relazioni identiche; attesi indipendenti 430); runner `-Profile baseline` con `-BaselineRef F2-B3\headless` e `-DenseRef F2-pre-m4-v2\a\tutte` (DenseSet `f2-libreria`): PASS 41, KNOWN 1 (verifiche/project-calculations), NEW-FAIL 0; cattura headless contro B3: 452 file, 5 numeri dei casi nuovi entro 1e-9 su 276 di 'taglio' e 'torsione' (14 righe di taglio, 9 di torsione; scarto relativo massimo 2,0e-16), relazioni, ripieghi e tutto il resto identici salvo 21 tempi volatili; cattura densa: 32 564 righe, 1083 (589 di taglio, 494 di torsione) con 1682 numeri entro 1e-9, nessuna non classificata, stessi esiti, rifiuti e testi; fixture di Checker come sopra; profilo standard PASS 35, KNOWN 1, NEW-FAIL 0. Prove negative: calcolo headless che ignora l'interruttore (fallisce la 3e), torsione del modulo dal legacy diretto (fallisce la 3f). Evidenze in `supporto/artefatti/refactoring/f2-taglio-torsione` del repository principale (`b3/`, `interruttore-b3/`; `interruttore/` è il primo interruttore 85314a6, `f2.6/` il tentativo ritirato 7176c0c). Decisioni del coordinatore su F2-3, F2-4, traccia NTC e B3 in [decisioni.md](decisioni.md), da ratificare; nell'integrazione 2 anche F2-1 (ripieghi di `foro_presente`) ratificata così com'è dal coordinatore, da ratificare. Restano fuori dall'interruttore, nel legacy: il taglio senza staffe dei muri in c.a. (`X.Calculations/RetainingWall.Structures.cs:98-100`, VRd,c con una formula propria senza σcp, fase F4.7) e la proposta di bw, d e Asl dal contorno della sezione (`SectionShearGeometry.Derive`, buco 3 di [f2-matrice-api.md](f2-matrice-api.md)); i pali elastici verificano il taglio con `GPC.Checkers.Concrete.Piles` già da prima di F2. Dopo la verifica avversaria di ca6530d e 801e11c, prove rafforzate in 4e8b30b (stesso contenuto di 63cda73, ritirato da 2b2f16b perché aveva per errore il messaggio di 8c95d9b; l'interruttore resta sulla libreria): il taglio del modulo e del calcolo headless distingue i motori (prova 3d: 131 calcoli del modulo con uscite diverse; prova 3e: 106 righe headless uguali bit per bit al motore predefinito, 42 con uscite diverse), la torsione headless anche su sezioni NTC di 5 forme con cot θ 1, 1,5 e 2,5 (63 righe uguali bit per bit all'adattatore, 36 che distinguono i motori sia per la sola torsione sia per tutto il calcolo), almeno 10 casi per controllo; prima la 3e aveva una sola riga che distingueva i motori (T7) e nessuna prova distingueva il motore del taglio del modulo (con il taglio sempre legacy le prove di 801e11c passano). 17 880 controlli; prove negative (calcolo headless con il motore legacy, taglio del modulo sempre legacy) e runner `-Profile baseline` contro B3 e F2-pre-m4-v2 su 4e8b30b (PASS 41, KNOWN 1, NEW-FAIL 0, stesse misure di ca6530d) e profilo standard sui documenti 660e1f7 (PASS 35, KNOWN 1, NEW-FAIL 0) in `prove-rafforzate/`. Provenienza legacy di B3 confermata anche sul codice di prima di F2.5: cattura headless di 17b6c98 con `Corpus.cs` e `corpus/` di dcde952 uguale a B3 (452 file, solo 21 tempi volatili; `fallbacks.json` identico byte per byte, 2848 voci; `b3/provenienza-17b6c98`). Documenti del branch 801e11c, 660e1f7 e db4da92 (punta); branch unito nell'integrazione 2 (`refactoring/integrazione-2`) con il merge ea4c51e. Le misure sopra erano con S1 (lib/Checker di main 17b6c98, GPCChecker.Concrete 0.0.15.0); nell'integrazione 2 si rimisurano con S2 (GPCChecker.Concrete 0.0.17.0, release dai commit pushati): `tests/ConcreteLibraryAdapter.Checks` PASS, 17 880 controlli, `misura.json` identico byte per byte; profilo standard `int2-f26-std` PASS 35, KNOWN 1 (verifiche/project-calculations), NEW-FAIL 0; profilo baseline `int2-f26-base` con `-BaselineRef F2-B3\headless` e `-DenseRef F2-pre-m4-v2\a\tutte` (DenseSet `f2-libreria`) PASS 41, KNOWN 1, NEW-FAIL 0 con le stesse misure di S1 (headless: 452 file, gli stessi 5 numeri entro 1e-9, 21 tempi volatili; densa: 32 564 righe, 1083 entro 1e-9, nessuna non classificata; fixture di Checker: 27 698 righe, 1083 entro 1e-9) e conteggi di tutte le suite uguali alle corse con S1. Catture con S2 uguali a quelle con S1 (densa salvo 1244 righe con soli identificativi casuali, headless salvo 21 tempi volatili): `Shear/` e `Torsion/` della libreria non cambiano fra S1 e S2, che differiscono per la fessurazione (D7-b, R15) e per le versioni. Prove in `supporto/artefatti/refactoring/integrazione-2` del checkout principale (`ci`, `console`, `f26`).
- [ ] F2.7 Fessurazione e SLE, senza stato statico.
- [ ] F2.8 Dettagli, ancoraggi, aderenza, M–χ.
- [ ] F2.9 Durabilità, copriferri, scheda Materiali, validazione di progetto. D7-e: opzione e4 (allineamento alla UNI 11104:2025, prospetto 6), decisa il 7/10 dal coordinatore su delega, da ratificare. Prima il riscontro del prospetto 6 su una copia con licenza della norma; il riscontro preliminare (pagine visibili dell'anteprima UNI ed estratto pubblicato il 28/07/2025, fonte secondaria) dà XF1 C30/37 con A/C 0,55 e cemento minimo più basso in tutte le classi, a sfavore rispetto al codice. Poi libreria e ANTHEA in due commit con lo spostamento della durabilità: citazioni e dati di `ExposureClasses`, ricattura di `durability-legacy.csv`, attesi di `MaterialViewChecks.cs:78` ritrascritti dal prospetto 6.
- [ ] F2.10 [CP] Registro delle differenze e accettazione dell'equivalenza.
- [ ] F2.11 [CP] Eliminazione dei duplicati, dello stato statico, del lock e dei 6 file geotecnici esclusi.
- [ ] F2.12 [CP] Rimozione del solutore legacy a fibre conservando l'oracolo Python.
- [ ] F2.13 Registri di migrazione, chiusura.

Finestra di layout [CP] tra F2 e F3: spostamento in `src/ tests/ tools/ build/`; prima, destino di `GPCChecker.Test.BridgeAudit` (referenzia `ANTHEA/X.Core`) e test di libreria nei repository delle librerie.

## Fase 3: strato applicativo

- [ ] F3.1 [CP] Decisioni: migrazioni pigre, `str_palo` e chiavi miste congelati con alias, X.Materiali accorpato, figure senza WPF; fixture per gli 11 moduli.
- [ ] F3.2 Scheletro, strumento headless, progetto unico di test di architettura.
- [ ] F3.3 Documento, progetti, revisioni, condivisione; correzione dei default progetto/foglio dei muri.
- [ ] F3.4 [CP] Report: un solo `DocxWriter`; report di progetto senza rifusione; eliminazione di X.Core.
- [ ] F3.5 `IModuleService`, esito comune, `RecalculationScheduler`, cache; report di progetto senza `SheetEditor`; figure tramite `IFigureRenderer` con dati confrontati numericamente.
- [ ] F3.6 Moduli uno alla volta: acciaio e palificata; palo verticale; calcestruzzo; orizzontale ed elastico; muri; ponte [CP ricalcolo ritardato]; sezione c.a. [CP report short senza Word].
- [ ] F3.7 [CP] Chiusura: X.Desktop dipende solo dallo strato applicativo.

## Fase 4: calcoli residui nelle librerie

- [ ] F4.1 Catture aggiornate dopo F3.
- [ ] F4.2 Regole automatiche estese.
- [ ] F4.3 [CP] Collocazione di GPC.Design, Concrete/Walls, muratura, σcp, CRd,c/k1/vmin, esiti dei pali.
- [ ] F4.4 Esito comune tipizzato in pali e muri.
- [~] F4.5 Coefficienti dalle norme (γ dei pali da PileExecution); scostamento (d) in due commit. Anticipato il 7/10: D7-d opzioni d1 e d2 (γb da Model per tecnologia, valore di riserva e migrazione una tantum dei fogli).
- [ ] F4.6 Inventario delle soglie dipendenti dalle unità; palo elastico in N e mm.
- [ ] F4.7 Muri in c.a. in `Concrete/Walls` (tre passi).
- [ ] F4.8 Muri a gravità e dettagli.
- [ ] F4.9 [CP] Collegamento dei muri e rilascio `lib/Checker`. Anticipato il 7/10 lo scostamento D7-c (γRd non applicato a F̄).
- [ ] F4.10 GPC.Design: armature e distinta dei muri, tratti dei pali.
- [ ] F4.11 GPC.Design: BridgeConcept con listini e CO₂ come dati versionati.
- [ ] F4.12 GPC.Design: ricerca delle armature di sezione.
- [ ] F4.13 Logica ingegneristica delle viste verso librerie o Application.
- [ ] F4.14 [CP] Norme tipizzate negli adattatori residui; validazione esplicita.
- [ ] F4.15 Copriferro di progetto in un validatore di Application.
- [ ] F4.16 [CP] Chiusura, pagine dei metodi, eliminazione del legacy su autorizzazione.

## Fase 5: interfaccia MVVM

- [ ] F5.0 [CP] Ripianificazione sul codice dopo F4; `ANTHEA.Presentation` senza WPF.
- [ ] F5.1 Infrastruttura, servizi, gestione degli errori, analyzer.
- [ ] F5.2 `ModuleHost` (moduli nuovi e legacy insieme); componenti dei form.
- [ ] F5.3 [CP] Temi a token.
- [ ] F5.4 [CP] Modulo pilota: palificata orizzontale.
- [ ] F5.5–F5.12 Materiali, palo verticale, orizzontale, elastico, muri, sezione c.a., sezione composta, Bridge Design.
- [ ] F5.13 [CP] Shell; sessione di prova prima di eliminare SheetEditor e la vecchia MainWindow. Requisito
  dell'utente dell'8/10 («considera le opzioni 1 e 3. non multi progetto. però dai la possibilità di staccare la
  finestra per più monitor»):
  - più fogli aperti insieme in schede dentro il progetto;
  - più file di calcolo singoli aperti insieme, con un solo progetto alla volta;
  - schede staccabili in una finestra separata, per lavorare su più monitor.

  Da gestire: aggiornamento dei fogli aperti quando cambiano i dati condivisi (sezione, terreni, fogli CLS
  collegati), modifiche non salvate per foglio, calcoli in parallelo senza stato statico (dopo F2.11). Oggi
  MainWindow tiene un solo foglio e un solo documento (`MainWindow.cs:220-229`).
- [ ] F5.14 Chiusura: X.Desktop → ANTHEA.Desktop, terza revisione di AGENTS.md.
- [ ] F5.15 [CP] Ridiscutere con l'utente le voci sfavorevoli o aperte del registro delle differenze R4-R21 (R4-R14 e R16-R21; R15 è corretta) (decisione del 7/10: "le guardiamo a fine refactoring"; estesa a R16-R21 dal coordinatore su delega, da ratificare).

## Wiki (in parallelo)

- [ ] W0.2 [CP] Pipeline riproducibile da clone pulito (figure ed esempi, template Word, dipendenze Python). Difetti di impaginazione delle edizioni Word e PDF, già presenti nella Rev31 e confermati dalla verifica della Rev32 (b1998ec): numero dei titoli di secondo livello a due cifre attaccato al testo («15.10TRATTI…», «16.10TORSIONE»; tabulazione dello stile del titolo nel modello protetto); spazio di `\quad` perso nella conversione in OMML (formula di Rg); celle di tabella oltre 18 caratteri allineate a sinistra (`write_table`); decimali con il punto in alcune tabelle e formule; ≤ e ≥ in Manrope fuori dall'elenco SYMBOLS; date di `docProps/core.xml` del modello.
- [ ] W0.3 [CP] Archiviazione degli script una tantum.
- [x] W0.4 Pulizia del diario di sviluppo e dei riferimenti a concorrenti, con controllo automatico; paragrafi duplicati. 73 interventi nelle due guide, elencati in [w0.4-pulizia-guide.md](w0.4-pulizia-guide.md) (d606fac); espressioni vietate e paragrafi duplicati fra le guide controllati da `supporto/test/wiki-handbook-checks.py` (suite `wiki/manuale`). Profilo standard 33 PASS, 1 KNOWN, 0 NEW-FAIL; `ui/check-wiki-offscreen` PASS.
- [x] W0.5 Eliminazione del corpus esterno e di tutti i riferimenti esterni da fonti, exe e PDF (decisione del 6/10: solo contenuti nostri). Fonti ed exe in F0 (guida teorica da 2,2 MB a 293 kB, 63 articoli); edizioni Word e PDF Rev31 (301f920; teorica 134 pagine contro 900 della Rev30), con lettere greche, segni combinati e «<», «>» leggibili nel testo corrente (4b04066, c751bd6: in Manrope η, ν, χ avevano il contorno di n, v, x, difetto già presente nella Rev30). Rev30 in `supporto/SUPERATI` (40da3f5): la teorica archiviata contiene ancora il corpus, come le altre copie di SUPERATI; da decidere con l'utente.
- [ ] W1.1 [CP] Convenzioni, template della pagina tecnica, registro delle norme.
- [ ] W1.2 Riquadri "Scostamento dichiarato".
- [ ] W1.3 P1: Muri, Palo verticale, Micropalo, Sezione c.a.
- [ ] W1.4 P2: Sezione composta, portanza sismica, durabilità, Broms, Bridge Design, palo elastico, efficienza, armature del palo.
- [ ] W1.5 P3: guida d'uso per modulo, figure con didascalia e fonte.
- [ ] W2.1 Strumento .NET (Markdig) al posto di Python e del controllo sugli hash.
- [ ] W2.2 [CP] Un file per argomento, front-matter, redirect; seconda revisione di AGENTS.md.
- [ ] W2.3 Teoria per metodo nei repository delle librerie, distribuita con le DLL.
- [ ] W2.4 Registro unico delle grandezze.
- [ ] W2.5 [CP] Word e PDF generati in release; PDF fuori dall'indice.

## Infrastruttura successiva

- [ ] Directory.Build comuni, gestione centrale dei pacchetti, lock file, soglia dei warning.
- [ ] [CP] Versione da git, installer `ANTHEA-<versione>`, firma.
- [ ] CI di catena delle librerie.
- [ ] [CP] Feed NuGet con switch verso i progetti delle librerie; script `PushNugetPackage*.bat`.
- [ ] [CP] SUPERATI fuori dall'indice con archivio esterno; script in un'unica cartella.
- [ ] Dopo F5: archiviazione dei progetti di test non più usati.
- [ ] Su richiesta: riscrittura della storia (node_modules, corpus esterno), `git fsck`, `git gc`.
- [x] Tema scuro e molto scuro corretti e verificati (branch `refactoring/ui-tema-scuro`, unito nell'integrazione 2 con
  09142a9 e 356d9b9, l'8/10):
  - verifica `--check-contrast`: Scuro e Molto scuro a 0 difetti, Chiara invariata;
  - immagini dei report sempre chiare (`--check-report-appearance-offscreen`);
  - esito in `decisioni.md`, sezione «Tema scuro».

  Seguiti:
  - [ ] difetti di contrasto della sola Chiara (719 in 28 gruppi), da decidere con l'utente;
  - [ ] misura del testo disegnato in OnRender in `ContrastAudit`;
  - [ ] attesa più robusta nelle catture di `str_mista_ponte`;
  - [ ] figure della guida pratica in `supporto/artefatti` (ignorato da git), che fanno fallire lo stadio wiki in un
    worktree nuovo.
- [ ] In coda a tutte le altre attività (decisione dell'utente dell'8/10):
  - analisi dei bug del codice di calcolo, corsa `wf_9c55965c-68b` ferma con 85 agenti in cache;
  - revisione delle pagine dei metodi ca.sle-tensioni e ca.fessurazione (U6);
  - traccia Wiki (W). Fra le voci da fare:
    - la guida pratica (riga 19) cita ancora la tendina dell'aspetto nella Wiki, tolta l'8/10;
    - la sezione «Materiali tendini e analisi di esercizio» della guida teorica, a cui rimanda il «?» accanto al tipo
      di analisi della scheda Tensioni, va completata con la scelta fra analisi lineare e non lineare (legami di
      progetto, avviso dell'8/10).
- [ ] Report di calcolo: non si modificano finché l'utente non lo dice (decisione dell'8/10).
