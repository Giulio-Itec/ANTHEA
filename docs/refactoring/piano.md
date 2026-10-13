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

- [x] F2.1 Cattura pre-M4 del legacy attuale; matrice delle API; casi vicini alla soglia; test con sezioni in forma chiusa. Fatti (branch `refactoring/f2-banco-confronto`, [f2.1-banco.md](f2.1-banco.md), [f2-matrice-api.md](f2-matrice-api.md)): cattura pre-M4 doppia in `supporto/artefatti/baseline/F2-pre-m4` (identica a meno degli identificativi casuali; contro B0 solo D7-c e D7-d), comando `compare-dense` con classificazione versionata, suite `baseline/banco-ca` (fixture di Checker del 1/10: 27 698 righe, 110 differenze tutte classificate). Fixture ricatturate il 7/10 (decisione B) dalla nuova cattura doppia `F2-pre-m4-v2` (d2d3225, con D7-b, D7-d d2 e R15; mesh identiche a B0): Checker 06d97733, test di migrazione senza casi speciali, banco con zero differenze e riferimento denso `F2-pre-m4-v2/a/tutte`. Casi vicini alla soglia e sezioni in forma chiusa l'8/10 (branch `refactoring/f2-1-soglie` da 98a21d4, [f2.1-banco.md](f2.1-banco.md), esito in `tests/ConcreteBench.Checks/esito.md`): suite `ConcreteBench.Checks` e `ConcreteBench.Checks/attesi` nello stadio fast; legacy di X.Calculations contro GPCChecker.Concrete S2 su 19 stati SLE e 25 resistenze SLU con attesi Python indipendenti (scarti massimi 1,0e-4 e 1,8e-3, tolleranze 1e-3 e 5e-3) e su 627 punti attorno a soglie di SLE, fessurazione, SLU e taglio (stessi esiti, numeri entro 1e-9). Divergenze registrate da decidere: S-1 (punto resistente della libreria con N fuori dalla tolleranza di 1 kN di `SectionMomentResistance`, il legacy non dà la resistenza in un caso), T-1 (taglio senza staffe con VEd = VRd: esito opposto per 1 ulp, da aggiungere a F2-1).
- [x] F2.2 [CP] Pagine dei metodi c.a. (revisione tecnica). Scritte in Checker (branch locali `anthea-metodi-ca-1` e `anthea-metodi-ca-2`, worktree Temp\cw-metodi1 e cw-metodi2): taglio, torsione, SLE, fessurazione, ancoraggi, dettagli, M-χ, durabilità e copriferri, con esempi a mano uguali alla libreria. Revisionate e unite in Checker develop: merge locali c555a3b8 e 1fbaea61 più le correzioni del verificatore (5675492f e 398eb36a con i merge 9eb800d4 e 26fb6b8b; bf23a16d, 7d86ffc7, bd9938ac, e41a803a). Revisione accettata il 7/10 dal coordinatore su delega dell'utente, da ratificare. Voci nuove R16-R21 nel registro (da decidere in F5.15) e R7 rettificata. Push di Checker develop (punta e41a803a, 35 commit oltre origin/develop) all'utente.
- [~] F2.3 [CP] GPCChecker.Concrete: dettagli di solette e pareti; mesh sicure in parallelo al posto del lock; tracce per i report; opzioni degli scostamenti. Fatti il 7/10 (Checker develop): D7-b con opzione legacy (0.0.16.0), R3, R15 della fascia interna (0.0.17.0), fixture c.a. ricatturate; allora restavano solette e pareti, mesh senza lock, tracce in italiano per i report, R1 e R2 dopo il riscontro dei testi.
  - [x] F2.3a — dettagli di solette e pareti: API `MemberDetailingCalculator`/`PlateDetailingData` disponibili nel set S e collegate da `ConcreteDetailingCalculator` (riuso API-01 già in main). Prove `ANTHEA.ApiReuse.Checks`: 266 casi congelati, compresi i quattro tipi di elemento, controllo delle chiamate reali alla DLL; 3268 numeri, solo due scarti di arrotondamento fino a 1,14e-16. Le sezioni circolari mantengono il ripiego API-02, tracciato separatamente. Evidenze nel [controllo dei contratti](audit-contratti-moduli-20261012.md) e nella corsa `20261012-195301-f3-defaults-standard`.
  - [x] F2.3b-mesh — sicurezza delle mesh in parallelo e rimozione del lock globale, provata il 12/10: punto 4 e registro F2-23.
  - [ ] F2.3b-tracce — completamento delle tracce per i report, R1 e R2 dopo il riscontro dei testi.
- [x] F2.4 [CP] Rilascio `lib/Checker` con codice ANTHEA invariato. S2 compilata dai commit pushati e verificata; profilo full a schermo sull'integrazione 2 (258e81a, corsa `20261007-184928-int2-full`): PASS 69, KNOWN 1 (`verifiche/project-calculations`), NEW-FAIL 0, tutte le prove WPF superate. Branch `refactoring/f2-4-snapshot-s2` (da main 17b6c98): revisioni nuove a sorgente invariato per SourceLink (Model 5ad56681: GPCModel 1.6.1.1, GPCModelData 0.0.2.3; Checker 0d7ba50b: Geotechnics 0.1.1.1, CompositeBridge 1.4.0.4), GPCChecker.Concrete 0.0.17.0 con D7-b e R15, Utilities, Geometry e DelaunayMesh invariate. DLL compilate con la nuova opzione `Update-Snapshot.ps1 -FromUpstream` (worktree temporanei dei rami remoti in una radice fissa, 8d6a8ec) da Utilities df3b3e7, Geometry 6a0d1c1, Model 5ad56681 e Checker 0d7ba50b, tutti in origin (edc4fce); sostituiscono il candidato f6b6fdf, compilato da Checker develop 1fbaea61 locale. Profilo standard 33 PASS, 1 KNOWN, 0 NEW-FAIL; profilo baseline 39 PASS, 1 KNOWN, 0 NEW-FAIL con esiti e conteggi uguali al candidato e a S1, cattura headless uguale a F2-B2, banco c.a. e banco denso senza differenze, 80 mesh identiche; test delle librerie superati (dettagli in `lib/Checker/README.md`). Unita nell'integrazione 2 (`refactoring/integrazione-2`: merge del candidato 0c87141, merge della ricompilazione b818b20): profilo standard `int2-std` 33 PASS, 1 KNOWN, 0 NEW-FAIL e profilo baseline `int2-base` 39 PASS, 1 KNOWN, 0 NEW-FAIL, esiti e conteggi uguali a S2 (prove in `supporto/artefatti/refactoring/integrazione-2` del checkout principale). Nota del 7/10 del branch `refactoring/f2-taglio-torsione`, scritta prima di S2 (superata nella prima parte dalle versioni nuove di S2): GPCChecker.Concrete 0.0.17.0 è pronta ma non distribuita; ricompilando dai commit nuovi cambiano per SourceLink anche gli SHA-256 di GPCModel, GPCModelData, Geotechnics e CompositeBridge a sorgente invariato, e `tools/libs/Update-Snapshot.ps1` rifiuta uno SHA diverso senza versione più alta: servono versioni nuove (o una regola dello script per i componenti a sorgente invariato) e il profilo full a schermo. Dal passo F2.6 taglio e torsione della sezione passano dalla libreria: prima di distribuire una `GPCChecker.Concrete` nuova, rieseguire `tests/ConcreteLibraryAdapter.Checks` e confrontare i testi di `Shear/` e `Torsion/` (stati, rifiuti, espressioni dei dettagli) con la mappa di `X.Calculations/ConcreteLibraryMapping.cs`. Un testo senza traduzione è un errore di programma (`InvalidOperationException`): il calcolo headless e il progetto delle armature lo registrano per riga, la scheda WPF del taglio interrompe le righe successive (`X.Desktop/Wpf/ConcreteShear.cs:125` cattura solo `ArgumentException`) e mostra un solo errore "Taglio". Oggi nessun testo della libreria raggiungibile dal modulo ne è privo (verifica avversaria su ca6530d). Nell'integrazione 2, dopo il merge di F2.5-F2.6 (ea4c51e), la condizione è verificata per S2: `Shear/` e `Torsion/` di GPCChecker.Concrete sono identici fra S1 (Checker b994e188) e S2 (0d7ba50b), quindi stessi testi, e `tests/ConcreteLibraryAdapter.Checks` dà PASS con `misura.json` identico byte per byte a quello con S1.
- [x] F2.5 Strato di mappatura con contratto JSON invariato. Taglio e torsione (branch `refactoring/f2-taglio-torsione`): `X.Calculations/ConcreteLibraryMapping.cs` (conversioni kN ↔ N e kNm ↔ N·mm con nome, nomi delle norme → classi `Standard` per le 7 norme ordinarie, risultati della libreria → DTO del JSON 'taglio' e 'torsione', testi e rifiuti della libreria → testi italiani del legacy; nessuna costante normativa, controllato dalle prove), adattatore `ConcreteShearTorsionAdapter` con l'interruttore `Default` (motore legacy in questo passo), DTO della torsione in `ConcreteTorsionContracts.cs`. `ConcreteShearAnalysis` e `CheckerMigration.Capture` (opzione `--motore legacy|libreria`) passano dall'adattatore. Prove `tests/ConcreteLibraryAdapter.Checks` (stadio fast): interruttore, motore legacy dell'adattatore identico bit per bit al legacy, motore della libreria con stessi esiti, rifiuti e testi e numeri entro 1e-9 sulle griglie della cattura densa e sui calcoli del modulo (scarto relativo massimo 9,2e-16). Runner `-Profile baseline` con `-BaselineRef F2-B2` e `-DenseRef F2-pre-m4-v2`: PASS 41, KNOWN 1, NEW-FAIL 0; cattura headless uguale a B2 (432 file, 21 tempi volatili), banco denso uguale a F2-pre-m4-v2 (32 564 righe, solo identificativi casuali) e alle fixture di Checker. Differenze dichiarate: registro F2-1…F2-4. Commit 2583225. Correzione f5b5f50 (dopo la revisione di 7176c0c): in 2583225 `ConcreteShearAnalysis.Torsion` riceveva il motore ma calcolava ancora profilo resistente e torsione con il legacy diretto, quindi con la libreria il modulo avrebbe cambiato solo il taglio; ora passa da `ConcreteShearTorsionAdapter.TorsionGeometryOf` e `Torsion` con il motore richiesto. Interruttore di nuovo sul legacy (7176c0c ritirato) e tolleranze e confronto `f2-libreria` tolti, da rimettere in un commit dedicato prima dell'interruttore. Prove nuove: 3f (la torsione del modulo coincide bit per bit con l'adattatore chiamato direttamente con lo stesso motore; il controllo distingue i motori, fallisce sul codice di 7176c0c), 4 (attesi indipendenti di `supporto/test/ConcreteCode.Checks/reference.json`, letto in sola lettura, benchmark DIN, DS, UNI, NS e forme chiuse della torsione NTC attraverso l'adattatore con entrambi i motori), letterali con il punto iniziale (`.85`) nel controllo delle costanti. Evidenze in `supporto/artefatti/refactoring/f2-taglio-torsione/correzione`. Completamento della correzione dopo la seconda verifica avversaria (8c95d9b): interruttore di nuovo sul legacy dopo il primo 85314a6 per catturare la baseline headless B3 prima dell'interruttore; prova 3f anche contro il legacy diretto con il motore Legacy; prova 3e con la 'torsione' del calcolo headless uguale bit per bit all'adattatore con il motore predefinito (riga NTC T7 con uscite diverse fra i motori: un calcolo headless che ignorasse l'interruttore fallisce, prova negativa n1). Stato legacy dimostrato sul commit del corpus B3 (dcde952, stesso codice di calcolo): runner `-Profile baseline` PASS 40, KNOWN 1, NEW-FAIL 0; cattura densa identica a F2-pre-m4-v2 con il confronto esatto `pre-m4` (32 564 righe, 1244 con soli identificativi casuali); cattura headless uguale a B2 sui 432 file comuni, salvo le voci dei casi nuovi in `fallbacks.json`. Unito nell'integrazione 2 (`refactoring/integrazione-2`) con il merge ea4c51e del branch `refactoring/f2-taglio-torsione` (punta db4da92); misure con S2 in F2.6.
- [x] F2.6 Taglio e torsione → libreria. Storia dei commit (nessuna riscrittura): 7176c0c, primo interruttore, ritirato: collegava alla libreria il solo taglio del modulo (profilo resistente e torsione restavano sul legacy diretto e le prove 3d/3e confrontavano per la torsione il legacy con se stesso), con le tolleranze nel commit dell'interruttore; correzione di F2.5 f5b5f50; 8f991b4, tolleranze dense dedicate (confronto `f2-libreria`: come `pre-m4`, 1e-9 sui soli numeri di taglio e torsione, regole `f2-libreria/` di `tolerances.json`); 85314a6, secondo interruttore, sospeso da 8c95d9b perché il corpus headless di B2 non aveva sezioni c.a. con taglio e torsione; 256714d e dcde952, quattro casi in `tests/ANTHEA.Testing/corpus` (rettangolare, circolare e circolare cava NTC con torsione, rettangolare DIN con cot θ assegnato e rifiuti; `foro_presente` dichiarato) e baseline B3 catturata con il motore legacy (`supporto/artefatti/baseline/F2-B3`, LEGGIMI); ea6e0ac, tolleranze headless dedicate (1e-9 sui numeri di 'taglio' e 'torsione' dei risultati `str_palo`, il resto esatto); ca6530d, interruttore `ConcreteShearTorsionAdapter.Default` sulla libreria e `f2-libreria` predefinito di `-DenseSet`. Il modulo sezione c.a. (WPF, calcolo headless, progetto delle armature, tutti attraverso `ConcreteShearAnalysis`) calcola taglio, profilo resistente e torsione con `SectionShearCalculator`, `TorsionGeometry.Rectangle`/`Circle` e `SectionTorsionCalculator`; il legacy resta raggiungibile (`ShearTorsionEngine.Legacy`, cattura con `--motore legacy`, prove dell'adattatore) fino alla decisione di F2.11. Equivalenza misurata su ca6530d (registro F2-1, convenzione): `tests/ConcreteLibraryAdapter.Checks` 17 696 controlli (griglie della cattura densa; modulo 263 numeri diversi su 4510, massimo 5,4e-16; torsione del modulo uguale bit per bit all'adattatore del motore richiesto in 120 calcoli, 70 con uscite dei motori diverse; calcolo headless di 5 norme entro 1e-9, 25 numeri diversi su 1159, e torsione headless uguale all'adattatore con la libreria; relazioni identiche; attesi indipendenti 430); runner `-Profile baseline` con `-BaselineRef F2-B3\headless` e `-DenseRef F2-pre-m4-v2\a\tutte` (DenseSet `f2-libreria`): PASS 41, KNOWN 1 (verifiche/project-calculations), NEW-FAIL 0; cattura headless contro B3: 452 file, 5 numeri dei casi nuovi entro 1e-9 su 276 di 'taglio' e 'torsione' (14 righe di taglio, 9 di torsione; scarto relativo massimo 2,0e-16), relazioni, ripieghi e tutto il resto identici salvo 21 tempi volatili; cattura densa: 32 564 righe, 1083 (589 di taglio, 494 di torsione) con 1682 numeri entro 1e-9, nessuna non classificata, stessi esiti, rifiuti e testi; fixture di Checker come sopra; profilo standard PASS 35, KNOWN 1, NEW-FAIL 0. Prove negative: calcolo headless che ignora l'interruttore (fallisce la 3e), torsione del modulo dal legacy diretto (fallisce la 3f). Evidenze in `supporto/artefatti/refactoring/f2-taglio-torsione` del repository principale (`b3/`, `interruttore-b3/`; `interruttore/` è il primo interruttore 85314a6, `f2.6/` il tentativo ritirato 7176c0c). Decisioni del coordinatore su F2-3, F2-4, traccia NTC e B3 in [decisioni.md](decisioni.md), da ratificare; nell'integrazione 2 anche F2-1 (ripieghi di `foro_presente`) ratificata così com'è dal coordinatore, da ratificare. Restano fuori dall'interruttore, nel legacy: il taglio senza staffe dei muri in c.a. (`X.Calculations/RetainingWall.Structures.cs:98-100`, VRd,c con una formula propria senza σcp, fase F4.7) e la proposta di bw, d e Asl dal contorno della sezione (`SectionShearGeometry.Derive`, buco 3 di [f2-matrice-api.md](f2-matrice-api.md)); i pali elastici verificano il taglio con `GPC.Checkers.Concrete.Piles` già da prima di F2. Dopo la verifica avversaria di ca6530d e 801e11c, prove rafforzate in 4e8b30b (stesso contenuto di 63cda73, ritirato da 2b2f16b perché aveva per errore il messaggio di 8c95d9b; l'interruttore resta sulla libreria): il taglio del modulo e del calcolo headless distingue i motori (prova 3d: 131 calcoli del modulo con uscite diverse; prova 3e: 106 righe headless uguali bit per bit al motore predefinito, 42 con uscite diverse), la torsione headless anche su sezioni NTC di 5 forme con cot θ 1, 1,5 e 2,5 (63 righe uguali bit per bit all'adattatore, 36 che distinguono i motori sia per la sola torsione sia per tutto il calcolo), almeno 10 casi per controllo; prima la 3e aveva una sola riga che distingueva i motori (T7) e nessuna prova distingueva il motore del taglio del modulo (con il taglio sempre legacy le prove di 801e11c passano). 17 880 controlli; prove negative (calcolo headless con il motore legacy, taglio del modulo sempre legacy) e runner `-Profile baseline` contro B3 e F2-pre-m4-v2 su 4e8b30b (PASS 41, KNOWN 1, NEW-FAIL 0, stesse misure di ca6530d) e profilo standard sui documenti 660e1f7 (PASS 35, KNOWN 1, NEW-FAIL 0) in `prove-rafforzate/`. Provenienza legacy di B3 confermata anche sul codice di prima di F2.5: cattura headless di 17b6c98 con `Corpus.cs` e `corpus/` di dcde952 uguale a B3 (452 file, solo 21 tempi volatili; `fallbacks.json` identico byte per byte, 2848 voci; `b3/provenienza-17b6c98`). Documenti del branch 801e11c, 660e1f7 e db4da92 (punta); branch unito nell'integrazione 2 (`refactoring/integrazione-2`) con il merge ea4c51e. Le misure sopra erano con S1 (lib/Checker di main 17b6c98, GPCChecker.Concrete 0.0.15.0); nell'integrazione 2 si rimisurano con S2 (GPCChecker.Concrete 0.0.17.0, release dai commit pushati): `tests/ConcreteLibraryAdapter.Checks` PASS, 17 880 controlli, `misura.json` identico byte per byte; profilo standard `int2-f26-std` PASS 35, KNOWN 1 (verifiche/project-calculations), NEW-FAIL 0; profilo baseline `int2-f26-base` con `-BaselineRef F2-B3\headless` e `-DenseRef F2-pre-m4-v2\a\tutte` (DenseSet `f2-libreria`) PASS 41, KNOWN 1, NEW-FAIL 0 con le stesse misure di S1 (headless: 452 file, gli stessi 5 numeri entro 1e-9, 21 tempi volatili; densa: 32 564 righe, 1083 entro 1e-9, nessuna non classificata; fixture di Checker: 27 698 righe, 1083 entro 1e-9) e conteggi di tutte le suite uguali alle corse con S1. Catture con S2 uguali a quelle con S1 (densa salvo 1244 righe con soli identificativi casuali, headless salvo 21 tempi volatili): `Shear/` e `Torsion/` della libreria non cambiano fra S1 e S2, che differiscono per la fessurazione (D7-b, R15) e per le versioni. Prove in `supporto/artefatti/refactoring/integrazione-2` del checkout principale (`ci`, `console`, `f26`).
- [~] F2.7 Fessurazione e SLE, senza stato statico. Fatti (8/10): libreria K0-K4 e ciclo di prototipo (in S3), A2-A4
  sul branch di prototipo con motore predefinito Legacy (refactoring/integrazione-3). A5 il 9/10 su
  `refactoring/integrazione-4` (3dc8c57, b35afa8, c8a0927, bf7f6a6): X.Verifiche, ConcreteCode.Checks,
  ConcreteShort.Checks e l'harness WPF passano dall'adattatore (`--motore-sle legacy|libreria`; nell'harness la variabile
  `ANTHEA_MOTORE_SLE`), test di formula raddoppiati sulla libreria, 5k estesa a `supporto/test` e `tests` con
  `tests/ConcreteLibraryAdapter.Checks/legacy-allowlist.json`, prova negativa n11 rilevata. Profilo standard `int4-a5-std`
  PASS 39, KNOWN 1, NEW-FAIL 0. Prova generale con la libreria: tutto PASS salvo il caso (i) di CrackK2Checks
  (`--checker`): con le tensioni delle barre mancanti su una sezione interamente compressa il legacy dà wk = 0, la
  libreria rifiuta e la mappatura non traduce il rifiuto; caso non raggiungibile dalla produzione, segnalato all'utente,
  da decidere prima di A8. Restano: quella decisione, le prove WPF con i due motori (con il profilo full), le due
  revisioni del prototipo, A6-A9 e A12-A13 e il profilo full a schermo. A10 e le correzioni U1, U3, U5, U7 sono chiuse il 12/10 nei punti 2–3 seguenti.
  - [x] F2.7-A10 (12/10): omogeneizzazione delle proprietà, della scheda SLE e dei riepiloghi tramite le API GPC. Nessuna formula duplicata; messaggi italiani preservati. Chiamate IL verificate, attesi analitici esistenti e confronto 548 file superati (`20261012-212735-f27-a10-dopo`, 7 PASS). Il rifiuto nella vista di φ negativo segue in U5.
  - [x] F2.7-A11 (12/10 sera): esposizioni della UI e validazione dei muri dal catalogo GPC tramite `ConcreteLibraryMapping.ExposureChoices`, immutabile. Il catalogo legacy resta come riferimento indipendente, anch’esso immutabile. 19 voci e ordine invariati; prove 7c, tre suite WPF e catture prima/dopo superate (548 file, 50.996 righe dense; soli tempi e ID casuali). Registro F2-18.
- [~] F2.8 Dettagli, ancoraggi, aderenza, M–χ. Fatti (8/10): libreria L0-L3 (in S3), cattura A0. Restano B1-B3, T1,
  I1-I3, la correzione U2 (b) e D1.
- [x] Release S3 di `lib/Checker` (8/10): Checker develop 0ad8314b (F2.7, F2.8, F2.9, S-1), Model M1 6005d180;
  integrazione in `refactoring/integrazione-3` con F2.1 (banco), A0, F2.9 (E1-E6) e S-1 (palo orizzontale con D ≥ 1,6 m).
  Profilo full a schermo il 9/10 sull'integrazione 3 con le correzioni delle segnalazioni dell'8/10 (758b09c, corsa
  `20261009-154030-int3-segn-full`): PASS 74, KNOWN 1 (`verifiche/project-calculations`), NEW-FAIL 0; profilo standard
  `int3-segn-std` PASS 38, KNOWN 1, NEW-FAIL 0. Schermo preso dopo 5 minuti senza risposta, come concordato.
- [x] F2.9 Durabilità, copriferri, scheda Materiali e validazione di progetto: chiusura tecnica CD0-CD2/E1-E6 verificata il 12/10.
  Il set S distribuisce il contratto e il catalogo immutabile di Checker; l’adattatore usa la libreria per impostazione predefinita.
  Baseline B6 presente nel checkout principale; equivalenza bit per bit della durabilità, 247 percorsi controllati e prove WPF richieste superate.
  Integrazione già in main/origin al commit `9b42f093`. [Evidenze e limiti della chiusura](chiusura-f29-20261012.md).
  Restano separati: proposte F2.9-U1…U7 e ratifiche normative, F4.13 per le regole residue della scheda,
  F2.11 per ogni eventuale eliminazione del legacy (sonda comune completata in F2.11b). Nessuna nuova scelta normativa fatta con questa chiusura.
- [ ] F2.10 [CP] Registro delle differenze e accettazione dell'equivalenza.
- [~] F2.11 [CP] Eliminazione dei duplicati, dello stato statico e del lock.
  - [x] F2.11a — sei file geotecnici esclusi: rimossi su autorizzazione esplicita con `83081a67`, già in main/set S. BishopSolver, FoundationSettlement, NewmarkSliding, ShallowFoundationSeismic, SlopeGeometry e SlopeStability non sono più nel progetto. Nessuna nuova eliminazione con questa riconciliazione del piano; `SlopeStability.Models.cs` e `WallRecords.cs` restano contratti usati.
  - [x] F2.11b — sonda di durabilità (F2.9-D14) unificata con quella comune degli adattatori. Conservata la facciata tipizzata; contesti annidati ripristinati, calcoli paralleli isolati, eliminato il secondo AsyncLocal. 15 prove mirate (10 fallivano prima), regressione completa e catture: 9 PASS, 0 KNOWN, 0 NEW-FAIL; 548 file e 50.996 righe senza cambi di risultato. Registro F2-19.
  - [ ] F2.11c — altri duplicati, stato statico e lock: richiedono ancora confronto di equivalenza e accordo prima dell’eliminazione del legacy attivo.
- [ ] F2.12 [CP] Rimozione del solutore legacy a fibre conservando l'oracolo Python.
- [ ] F2.13 Registri di migrazione, chiusura.
- [x] Revisione di architettura R2 (11/10, unita in main il 12/10): punto resistente SLU con un
  solo proprietario. `ConcreteResistanceAdapter` per dominio 3D, resistenza a N assegnato, progetto delle armature e calcolo
  headless, con la porta `ISectionResistanceSolver` di GPCChecker.Concrete (il calcolo di ModelChecker); in Checker i punti
  grezzi pubblici sono `[Obsolete]` (5f87a390). Decisione dell'utente dell'11/10 (F2-17, corretto): solo il motore della
  libreria, senza interruttore né `--motore-slu`. Cattura del corpus uguale a quella di prima su 546 file; prove 12a-12g in
  `tests/ConcreteLibraryAdapter.Checks` contro il punto grezzo chiamato direttamente. lib/Checker: release R delle librerie
  della revisione (Model 5.0.0.0), dal primo set di rilascio da master. ANTHEA 1.2.0.

Finestra di layout [CP] tra F2 e F3: spostamento in `src/ tests/ tools/ build/`; prima, destino di `GPCChecker.Test.BridgeAudit` (referenzia `ANTHEA/X.Core`) e test di libreria nei repository delle librerie.

## Passaggio fra F2 e F3: stage dei modelli di calcolo

Aggiunto dall’utente il 12 ottobre, dopo i punti 1–5 e 5.2 e prima dell’avvio di F3.
Riusare entità, convertitori e contratti già presenti in GPC Model; nuove formule nelle librerie.

- [x] STG-1 (12/10, Model `6ede622c`): inventario delle API e dei dati stage già disponibili; studio della documentazione MIDAS e campione dei risultati. Distinguere incremento/cumulato, età e attivazione di elementi, vincoli e carichi. Studio e fixture sintetica completati; la sessione reale risponde con STAG/STCT vuoti, quindi il confronto con un modello reale resta esplicitamente in STG-4.
- [x] STG-2 (12/10, Model `cffd4879`, `b89ce317`, `3c357622`): modello comune degli stage e provenienza dei risultati, senza duplicare casi/combinazioni; lettura MIDAS con errori espliciti e test di unità/assi/associazione stage. Riutilizzati ResultState e convertitore comune; conservati i metadati delle attivazioni senza applicarli come vincoli permanenti. API catalogo riusabile senza geometria. Archivio legacy e impronte invariati; 1224 prove Model e 17 prove finali catalogo superate; set candidato in collaudo prima della distribuzione in ANTHEA.
- [x] STG-3 (13/10): importazione esplicita fase/passo/caso, filtro della vista, archivio formato 2 e capacità stage Strand7 dichiarate. Mapper statici riusati senza variazioni; 461 controlli dei dati e 21 WPF. Rilascio U, ANTHEA 1.4.0 e guide Rev38. Full: 102 PASS e un rilievo nei metadati Wiki, corretto e ricontrollato prima del commit. Rapporto in chiusura-stage-20261012.md.
- [~] STG-4: collaudo sintetico completo e suite ANTHEA concluse; confronto numerico reale ancora aperto. Aggiornamento 13/10: il ponte disponibile espone tre stage e 1715 elementi; le richieste campione di forze/spostamenti non restituiscono tabelle utilizzabili. Non è quindi provata l’equivalenza dei risultati reali per fase. Evidenze midas-stage-live/20261013-112333; nessun risultato inventato.

## Passaggio fra stage e F3: visualizzazione delle sezioni

Aggiunto dall’utente il 12 ottobre dopo il blocco stage. L’immagine fornita è un
riferimento funzionale: rappresentare i nostri dati e le capacità effettive degli
importatori, senza copiare schermate o introdurre nuovi calcoli nella UI.

- [x] SEZ-1 (13/10): inventario in sezioni-inventario.md; parser e geometrie condivisi in Model 7fd1fe4c, 1234 test superati. Campione reale con due sezioni quadrate; composta I/soletta collaudata come geometria sintetica separata. Offset non risolti distinti da zero. Candidato librerie in verifica prima della distribuzione.
- [x] SEZ-2 (13/10): finestra separata MVVM dall’albero o da un’asta; anteprima quotata, parti/materiali, proprietà con origine, coordinate G/O, assi locali/principali disattivabili, attributi originali. Unificati parser sorgente e proiezione degli attributi. Set V installato; 477 controlli dati e 11 WPF superati, tre temi e finestra compatta controllati graficamente (20261013-003436-sez-detail-reviewed). Full e guide previsti in SEZ-4 prima del merge.
- [~] SEZ-3: geometria, offset e associazioni; resta la variazione della sezione per fase.
  - [x] SEZ-3a (13/10): contorni condivisi fra 2D e 3D, fori e rientranze conservati, punto di inserimento applicato nelle terne locali, contouring lungo i volumi. Selezione/isola degli elementi associati; componenti e rapporti importati disponibili. Offset non risolto o di estremità non normalizzato: asta ad asse con avviso. Controllati vertici effettivi, aree indipendenti, orientamento dei triangoli; 477 controlli dati, 18 WPF sezioni e 33 WPF generali (20261013-003816-sez-volumes-validation).
  - [ ] SEZ-3b: sezione variabile per fase, collegata al futuro aggiornamento della geometria attiva STG-4. I dati restano conservati; la vista attuale dichiara il limite e non attribuisce al volume una sezione di fase non risolta.
- [x] SEZ-4 (13/10): full 20261013-004459-sez-set-v-full, 104 PASS e zero fallimenti; 478 controlli dati, 18 WPF sezioni e confronti numerici precedenti invariati. Guide globali Rev39 e ANTHEA 1.5.0; chiusura in chiusura-sezioni-20261013.md. SEZ-3b e il confronto reale STG-4 restano aperti. Si avvia F3 sul set V congelato.

## Fase 3: strato applicativo

- [x] F3.1 (13/10): decisioni su delega in f3-strato-applicativo.md; 36 fixture per i 12 moduli effettivi. Doppia cattura WPF D/E: 458 file equivalenti, unico volatile tempi_ms già censito. Dati migrati, risultati, report singoli/brevi/progetto e Wiki congelati in tests/fixtures/f3; nessun codice di produzione spostato. Le decisioni sull’accorpamento e i servizi saranno attuate nei passi successivi.
- [x] F3.2 (13/10): Application, Reports e Wiki net8.0 in src, ingresso Headless in tools e unica suite Architecture.Tests. Verificati target, assenza WPF/COM, direzione dei riferimenti, soglie TypeRef/async void/timer e set V congelato. Runner f3-scaffold: 7 PASS, equivalenza completa; f3-architecture-final: 5 PASS. Release priva di codice di prova. I comandi operativi Headless arrivano con i servizi, non sono dichiarati disponibili nello scheletro.
- [x] F3.3 Documento, progetti, revisioni, condivisione.
  - [x] F3.3a (12/10): confronto dei default progetto/foglio dei muri corretto. Si normalizzano solo gli ID delle azioni in copie di test, validando formato e unicità; gli ID dei documenti restano indipendenti. Registro TEST-01, prove di mutazione in `tests/ProjectCalculationChecks`. Eliminata l’ultima eccezione in `build/known-failures.json`. Suite progetto: 152 controlli; profilo standard `20261012-195301-f3-defaults-standard`: 48 PASS, 0 KNOWN, 0 NEW-FAIL.
  - [x] F3.3b (13/10): 17 sorgenti e template trasferiti in Application; DocumentService e scrittura atomica condivisa, Archivio come facciata compatibile. Headless validate/inspect operativi. 121 controlli documenti/revisioni, suite progetto e confronto di 458 file invariati: corsa 20261013-013204-f3-documents-built, 12 PASS. Nessuna migrazione anticipata in apertura.
- [x] F3.4 (13/10): generatori in Reports, un solo DocxWriter, capitoli in memoria e una sola serializzazione del report progetto. Core eliminato dal prodotto; resta il solo inoltro dei tipi per il test esterno BridgeAudit e consumatori precedenti, senza implementazioni duplicate. 458 file equivalenti; 30 DOCX, 134 parti XML e 97 immagini identici salvo normalizzazioni volatili già esistenti. Corsa f3-reports: 13 PASS; f3-report-parts-final: 5 PASS.
- [x] F3.5 `IModuleService`, esito comune, `RecalculationScheduler`, cache; report di progetto senza `SheetEditor`; figure tramite `IFigureRenderer` con dati confrontati numericamente.
  - [x] F3.5a (13/10): contratto dei 12 servizi, risultato comune, cache JSON per documento con copie indipendenti e limiti, scheduler seriale con annullamento e protezione dalla pubblicazione obsoleta. 185 controlli Application, 7 PASS; CLI calculate provata, ingresso protetto da sovrascrittura. Collegamento graduale alla UI in F3.6.
  - [x] F3.5a.2 (13/10): unico dispatch di calcolo in Application, facciata JSON compatibile a livello sorgente, nessun doppio calcolo dei risultati tipizzati. 14 PASS e 458 file equivalenti; corpus headless 548 file invariati dopo le sole otto rinomine diagnostiche F3-01 (18 prove negative/positive). Chiusura 20261013-020922-f3-dispatch-final: 5 PASS, set V coerente anche nello strumento.
  - [x] F3.5b: servizio report progetto autonomo e renderer delle figure.
    - [x] F3.5b.1 (13/10): orchestrazione progetto in ProjectReportService, snapshot con ascendenza, capitoli ordinati, errori per foglio e scrittura protetta dall’annullamento. 337 controlli Application, smoke report progetto e 458 file equivalenti. Resta un adattatore temporaneo SheetEditorReportChapterBuilder, da sostituire nei passi successivi: il punto F3.5b non è chiuso.
    - [x] F3.5b.2a (13/10): cattura numerica esatta dei grafici di pali e micropali, sei fixture con tutte le serie, modalità e stratigrafie; ripetibilità verificata prima degli spostamenti.
    - [x] F3.5b.2b (13/10): curve di capacità e abachi preparati in Application; adattatore WPF condiviso. Sei fixture grafiche esatte e 458 file equivalenti.
    - [x] F3.5b.2c (13/10): capitoli autonomi di pali, micropali e Bridge Design, contratto IFigureRenderer e preparazione condivisa con la vista; otto fixture, dati/XML/immagini equivalenti. Restanti moduli nel seguito.
    - [x] F3.5b.2d (13/10): capitoli autonomi materiali, palificata e fondazioni; stato storico e ordine delle proprietà conservati. 16 fixture autonome, 343 controlli Application, smoke e 458 file equivalenti.
    - [x] F3.5b.2e (13/10): capitolo muri autonomo, preparazione dei dati e distinta condivise, sole figure nel renderer. 21 fixture di capitoli e 55 immagini, incluse stabilità globale e tavole ferri; smoke muro/globale e 458 file equivalenti.
    - [x] F3.5b.2f (13/10): ponte composto autonomo; preparazione fasi/viste e omogeneizzazione condivisa, figure indipendenti dalla finestra. Otto suite passate, capitoli equivalenti e 458 file invariati.
    - [x] F3.5b.2g (13/10): pali e micropali orizzontali autonomi, capacità ed elastico; preparazione condivisa e migrazione legacy ancora esplicita. Cinque suite passate, 33 capitoli e 458 file equivalenti.
    - [x] F3.5b.2h.1 (13/10): preparazione CA portabile, coefficienti di omogeneizzazione, valori iniziali e serializzazione delle righe condivisi con la vista. Quattro preparazioni CA equivalenti, 33 capitoli precedenti e 458 file invariati.
    - [x] F3.5b.2h.2 (13/10): risultato completo CA autonomo, esportazione unica e ciclo taglio condiviso; quattro risultati identici alla vista. Congelati anche quattro riferimenti numerici dei grafici, verificati prima della loro estrazione.
    - [x] F3.5b.2h.3 (13/10): dati dei domini CA in Application, filtri/punti/segmenti e palette condivisi; quattro riferimenti grafici esatti, smoke CA e 458 file equivalenti.
    - [x] F3.5b.2 (13/10): tutti i 12 moduli autonomi, incluso CA; adattatore SheetEditor eliminato. Preparazione e ordinamento condivisi, renderer grafico comune. Otto suite passate; 37 capitoli e 85 immagini identici alla vista, 458 file equivalenti e 97 immagini dei 30 report congelati identiche byte per byte.
- [x] F3.6 Moduli uno alla volta: acciaio e palificata; palo verticale; calcestruzzo; orizzontale ed elastico; muri; ponte [CP ricalcolo ritardato]; sezione c.a. [CP report short senza Word].
  - [x] F3.6a (13/10): pali e micropali verticali collegati ai servizi, cache e scheduler; ritardo 450 ms conservato. Nove prove WPF con motore volutamente non cooperativo, cambi di geometria e chiusura. Corsa f3-vertical-service: 10 PASS, 458 file equivalenti; async void da 11 a 10, timer di calcolo da 6 a 5, soglie ridotte.
  - [x] F3.6b (13/10): acciaio per armature, palificata e fondazioni passano dai servizi applicativi. Palificata con scheduler comune e ritardo 180 ms; disponibilità e libreria iniettata delle fondazioni conservate. 12 PASS con prove WPF acciaio/fondazioni/materiali e 458 file equivalenti. Timer di calcolo da 5 a 4.
  - [x] F3.6c (13/10): catalogo, ricerca, alias, progressi e parser Wiki nella libreria portabile; rendering WPF conserva grafica e formule. 11 file di contenuto identici byte per byte, 240 controlli senza WPF, 71 articoli e 30 ricerche nel confronto F3 senza differenze. Wiki completa e aiuto fondazioni collaudati.
  - [x] F3.6d (13/10): ponte composto e muri con servizi e scheduler comuni, ritardi 500/450 ms conservati; curve del ponte, stabilità globale e ricerca armature protette da annullamento/revisione. 11 PASS, smoke completi e 458 file equivalenti. 21 prove WPF di ricalcolo e chiusura su pali, ponte e muri; 6 PASS finali, async void 8 e due soli timer di calcolo residui.
  - [x] F3.6e (13/10): pali orizzontali ed elastici su scheduler comune; sessione Application proprietaria di FEM e cache delle sezioni, diagrammi disponibili prima delle verifiche, riuso dopo cambio armature. 199 controlli Application, prove WPF complete, 458 file equivalenti. Corsa finale f3-piles-regression-final: 4 PASS; zero timer di calcolo, TypeRef 143 e async void 6.
  - [x] F3.6f.1 (13/10): presentazione e capitoli materiali senza WPF; catalogo dei valori iniziali condiviso e mappatura del copriferro riusata. 291 controlli Application, prove materiali/report e 458 file equivalenti; adattatori CLS rapidi passati. Accorpamento fisico della vista nel Desktop nel punto seguente.
  - [x] F3.6f.2 (13/10): vista Materiali e risorse accorpate nel Desktop, progetto separato rimosso, adattatore finestre inutilizzato eliminato. Catalogo esposizioni applicativo; documenti superati archiviati. Architettura, 291 controlli Application, materiali/report, 458 file equivalenti e report brevi WPF passati. Nessuna nuova dipendenza di calcolo.
  - [x] F3.6g (13/10): Bridge Design con ricalcolo ritardato 450 ms, confronto e alternative in Application su fotografie dei dati. Ottimizzazione seriale, annullabile anche durante l’attesa del calcolo, chiusura protetta. Smoke completo e 31 prove WPF di scheduling passati; equivalenza F3 di 458 file e controlli Application delle alternative/copie passati.
  - [x] F3.6h.1 (13/10): sessione CA e ingresso headless in Application, coordinamento incrementale di domini/SLE e cache della sezione preparata fuori da WPF. Scheduler condiviso e pubblicazione protetta; stesso modello per i quattro domini. 311 controlli Application, 35 WPF di scheduling, smoke CA e 458 file equivalenti. TypeRef Calculations ridotti a 140. Dettagli e riepiloghi per report autonomi nel passo seguente.
  - [x] F3.6h.2 (13/10): dettagli costruttivi, ancoraggi e riepiloghi CA portabili, con un solo formato dei riquadri condiviso fra UI e report. Conservati dati derivati, conferme manuali ed errori parziali. 330 controlli Application, smoke CA e 458 file equivalenti passati.
  - [x] F3.6h.3 (13/10): curve M–χ, resistenze rapide e ricerca armature tramite StructuralQueries. Scheduler comune per curve/resistenze; ricerche interrotte conservano i risultati parziali esplicitamente marcati, senza aggiornamenti dopo chiusura. 12 PASS con smoke, ricerca armature e 458 file equivalenti; 37 prove WPF di concorrenza/chiusura finali. TypeRef Calculations 133, async void 5.
- [x] F3.7 [CP] Chiusura: X.Desktop dipende solo dallo strato applicativo.
  - [x] F3.7a (13/10): preparazione delle preview CA e sessione delle proprietà in Application. Modello riusato, richieste seriali e risultati tardivi scartati; tredici controlli di copie/recupero/chiusura, smoke CA e 458 file equivalenti. TypeRef Calculations 127, async void 4.
  - [x] F3.7b.1 (13/10): ispezione σ/ε in Application, cache per verifica e lettura del piano limite nativo; richieste seriali, revisione e chiusura protette. Nove suite e cinque finali passate, 32 PNG identici prima/dopo, nove prove portabili con barriere. Async void 3.
  - [x] F3.7b.2 (13/10): tabella modificabile dei parametri dei muri in Application, validazione con servizio comune; applicazione protetta da revisione e chiusura. 376 controlli Application e quattro nuove prove WPF, 24 PNG identici; F3-03. Async void 2.
  - [x] F3.7b.3 (13/10): tabelle delle proprietà di carpenteria, soletta e fasi preparate in Application; rendering WPF dai dati, formattazione numerica condivisa. Dieci suite passate, 458 file equivalenti e quattro PNG predalle identici.
  - [x] F3.7b.4 (13/10): anteprime dei pali orizzontali in Application, con modello automatico, coefficienti, materiali, CHS e geometria circolare. Dodici controlli portabili aggiunti, undici suite e cinque finali passate; 76 PNG identici e 458 file equivalenti.
  - [x] F3.7b.5 (13/10): inventario dei membri realmente referenziati dalla Release e protezione di dieci ingressi di calcolo trasferiti. Distinti campi, proprietà e costruttori; tre suite passate. Il conteggio non certifica la separazione semantica, F3.7b resta aperto.
  - [x] F3.7b.6 (13/10): divisione, proposte e sessione di dimensionamento dei tratti in Application. Fotografie dei dati/cataloghi, verifica della risposta FEM, applicazione atomica e per revisione, chiusura protetta. 401 controlli Application, 47 WPF e 458 file equivalenti; registro F3-04. Il messaggio indica di riaprire la finestra per acquisire la nuova fotografia.
  - [x] F3.7b.7a (13/10): confronto dei capitoli corretto per il cambio di minuto/data nella sola riga generata. Nove prove del normalizzatore e otto suite passate; valori, date normative e immagini restano esatti. Registro F3-05.
  - [x] F3.7b.7b (13/10): anteprime del ponte, testi predalle/altezze e materiali/copriferro dei muri in Application. Formattazione condivisa, ordine delle invalidità preservato; 17 immagini identiche e 458 file equivalenti. TypeRef Calculations 125.
  - [x] F3-DOC (13/10): guide Rev40, Wiki e istruzioni del tool headless aggiornate. PDF 78/144 pagine, 337 formule e 26 parti ITEC conservate; revisione grafica eseguita. Dieci suite passate; cinque riferimenti editoriali classificati F3-06, 453 altri riferimenti e 36 input immutati.
  - [x] F3-BLD (13/10): qualificato System.Windows.Application nel supporto WPF condiviso dei test, dopo il conflitto con Anthea.Application trovato dal full. Due build e due suite reologia/input passate; nessuna modifica al prodotto o nuova eccezione ammessa.
  - [x] F3.7b.8 (13/10): preparazione dei parametri del terreno, cataloghi e rigidezza del palo fuori dalla UI; nessuna tabella o formula duplicata. 443 controlli Application, 13 suite passate, 37 capitoli e 458 file equivalenti. Zero riferimenti diretti Desktop a Checker.Geotechnics.
  - [x] F3.7b.9 (13/10): contratto grafico delle sezioni con copie di contorni, fori e barre; nessun motore nella geometria della vista. Quote e coordinate di cinque forme identiche bit per bit, 474 controlli Application, 14 suite passate, 37 capitoli e 458 file equivalenti. Soglia Calculations 123.
  - [x] F3.7b.10 (13/10): coefficienti e valori delle anteprime di pali/micropali verticali preparati nei servizi; cataloghi e correlazioni condivisi con i pali orizzontali. 511 controlli applicativi, 11 suite UI/report/figure passate e 458 file equivalenti. Soglia Calculations 119.
  - [x] F3.7b.11 (13/10): cataloghi e anteprime dei materiali condivisi nello strato applicativo, con curve trasferite senza modifiche e valori derivati preparati fuori dalla UI. 589 controlli Application, 13 suite passate, 37 capitoli e 458 file equivalenti. Soglia Calculations 116.
  - [x] F3.7b.12 (13/10): piano delle deformazioni e assi neutri ricevono dati grafici posseduti, senza interrogare i motori in WPF. Campioni identici bit per bit, 606 controlli applicativi; cinque immagini dei viewport identiche, 37 capitoli e 458 file equivalenti. Zero riferimenti Desktop a Checker.Concrete; soglia Calculations 115.
  - [x] F3.7b.13 (13/10): contratti posseduti di geometria, fasi, verifiche e fibre del ponte; letture native confinate in Application, stesso risultato riusato da viste e report. 87.462 valori confrontati esattamente, 677 controlli applicativi, 12 suite e cinque finali passate. Zero riferimenti Desktop a Checker; soglia Calculations 113. Differenze transitorie delle catture censite F3-07.
  - [x] F3.7b.14 (13/10): cataloghi e preparazione degli input CA dietro ConcreteEditor; aiuti e riepiloghi trasferiti senza modifiche in Application. 745 controlli applicativi, 12 suite passate, 37 capitoli e 458 file equivalenti. Soglia Calculations 104; catture con dimensioni diverse registrate F3-08, senza dichiarare equivalenza delle finestre.
  - [x] F3.7b.15 (13/10): Bridge Design usa contratti posseduti per risultati, geometrie, cataloghi, ricerca e avanzamento; report e interrogazioni riusano il risultato originario. 24.043 valori esatti in 14 famiglie e ottimizzazione, 825 controlli applicativi, dieci suite e cinque finali passate; 60/61 immagini identiche, un contorno di scheda transitorio (F3-07). Soglia Calculations 84.
  - [x] F3.7b.16 (13/10): contratti posseduti dei muri, stabilità globale, sisma e distinte; interrogazioni e report delegati al risultato originale. 97.959 valori esatti, 1003 controlli applicativi, tutte le suite UI/report/equivalenza passate. Sei immagini identiche su 29; le altre differiscono soltanto per hover del selettore (F3-07). Soglia Calculations 48.
  - [x] F3.7b.17 (13/10): metadati del catalogo, acciaio per armature, editor e progressi dei pali espongono contratti applicativi; nessun calcolo duplicato. 1050 controlli Application, 15 suite e nove finali passate, 80 immagini identiche e 458 file equivalenti. Soglia Calculations 39.
  - [x] F3.7b.18 (13/10): lettura JSON, traccia dei ripieghi e formattazione condivise in Serialization, senza motori né WPF. Implementazioni trasferite identiche; somma compensata conservata nel legacy. Undici suite passate, 548 file headless e 458 file F3 equivalenti. Soglia Calculations 37.
  - [x] F3.7b.19 (13/10): risultati posseduti di taglio/torsione, momento-curvatura, resistenze rapide e progetto armature; progressi copiati nel contesto esistente, esportazioni originali conservate. 5625 valori esatti, 1104 controlli Application, 32 proposte e 12 punti curva; tutte le suite UI passate, 51/52 immagini identiche e 458 file equivalenti. Soglia Calculations 22.
  - [x] F3.7b.20 (13/10): contratti posseduti di domini, tensioni, fessurazione, dettagli e ancoraggi. Cache e generazione differita di mesh/raster/piani limite preservate, JSON esportati dagli originali. 116.992 valori esatti, 1149 controlli Application, 13 suite UI/report e cinque finali passate. Zero riferimenti Desktop a Calculations; residui Model ancora aperti. 38/52 immagini identiche, 14 con dimensioni diverse (F3-08).
  - [x] F3.7b.21 (13/10): reologia e stratigrafia espongono soli dati posseduti, etichetta armatura muri trasferita identica nel backend. Zero dipendenze dirette Desktop da tutte le DLL GPC e da Calculations, protetto dai metadati. Dodici suite e sei finali passate; 1149 controlli Application, valori reologici esatti e 458 file equivalenti. Collaudo complessivo a seguire.
  - [x] F3-TST1 (13/10): banchi adattati ai contratti posseduti con sonda nativa condivisa solo nei test e scansioni estese a src. Intero adattatore: 158018 controlli taglio/torsione, 673640 SLE, 30 + 579 controlli aggiuntivi; API reuse, report breve Word e confronto F3 passati. Attesi e tolleranze invariati.
  - [x] F3-TST2 (13/10): corretta la fixture di condivisione materiali che sostituiva un documento valido con dati senza versione_materiali. Nessuna validazione del prodotto allentata; smoke-sharing passato, registro F3-09.
  - [x] F3.7b.22 (13/10): cambio norma del ponte spostato identico nel backend; parser accelerogrammi e riepilogo stratigrafie nei servizi; angolo sismico riusa la API GPC esistente. 1178 controlli Application, undici suite passate, 37 capitoli e 458 file equivalenti. Catture: 27 identiche, 68 con dimensioni diverse e una scrollbar in hover; F3-07/F3-08.
  - [x] F3.7b.23 (13/10): Release/Desktop compila senza riferimenti GPC diretti, set runtime copiato dal manifest verificato. Overload nativo dell’asse neutro reso interno; dieci DLL pubblicate con SHA-256 corretti, nessun codice test o Gmsh. Quattro controlli finali PASS e installer 1.6.0 prodotto; F3-10.
  - [x] F3.7b (13/10): chiusura dei contratti e degli accessi della UI, Release senza riferimenti diretti a Calculations/GPC, collaudo full con riprese mirate integralmente risolto (122 esiti coperti, zero fallimenti residui). ANTHEA 1.6.0, guide Rev40, installer verificato; [verbale e limiti](chiusura-f3-20261013.md). F4 non iniziata.

## Coda dopo F3: input delle sollecitazioni da Excel

- [ ] EXCEL-SOL (richiesta del 13/10, requisiti precisati dall'utente): riscrittura degli input delle sollecitazioni tramite Excel, dopo la chiusura dei punti F3 già concordati. Colonne nell'ordine: `Elemento` (facoltativo), `Nome combo`, `Stato limite`, `N`, `Vx`, `Vy`, `T`, `Mx`, `My`. Un solo insieme di sei componenti per riga/combinazione alimenta tutte le verifiche pertinenti allo stato limite. SLU e SLV alimentano anche taglio e torsione; Rara, Frequente e Quasi permanente alimentano i rispettivi controlli SLE. Eliminare la famiglia di input separata `Taglio`, conservando l'accesso alle verifiche di taglio/torsione. Requisiti e cautele di compatibilità in [decisioni.md](decisioni.md#13-ottobre--input-unificato-delle-sollecitazioni-da-excel). I requisiti principali sono già forniti: a fine F3 discutere soltanto le eventuali ambiguità residue, senza chiedere nuovamente il formato. Implementazione sul ramo `codex/section-actions-excel`; criterio Plastico/Elastico selezionabile nella verifica approvato il 13/10. Sottopassi: contratto e instradamento, Excel/editor, collaudo e guide.
- F4 resta ferma per indicazione esplicita dell'utente; la chiusura di F3 e questa attività accodata non ne autorizzano l'avvio.

## Fase 4: calcoli residui nelle librerie

- [x] 12 ottobre: [audit dei contratti dei 12 moduli](audit-contratti-moduli-20261012.md), delle classi di terreno/materiale e dei Progetti; inventario dei sorgenti effettivamente compilati e confronto con le API delle DLL distribuite.
- [x] 12 ottobre: riuso verificato delle API per aderenza/ancoraggi, dettagli non circolari, M–χ, regioni, interassi non circolari, acciaio e SLE; sei vecchi file geotecnici esclusi rimossi su consenso esplicito. Ramo `refactoring/f4-riuso-api`, registro API-01. Runner baseline: 52 PASS, 1 KNOWN, 0 NEW-FAIL; confronto completo di 548 file e denso di 50.996 righe passato; soluzione e app Release compilate. Residui e contratti incompleti nell'audit; non chiude F4.1–F4.16 né il rilascio delle fondazioni.
- [x] API-02: interassi circolari.
  - [x] API-02a (12/10 sera): correzione geometrica in Checker `e1a9c78e`, branch `codex/f2-api02-interassi`. 38 nuove prove indipendenti (7 fallivano prima), suite complete 1544 PASS, 17 Steel già noti, zero nuovi fallimenti. Atteso del caso C400R2: 115.19173586761349 mm; contratti e fixture preservati.
  - [x] API-02b: Concrete 0.0.27.2 distribuita nel set T da master pushati, installata con manifest e confronto completo. Attivazione in ANTHEA 1.3.0 verificata: 548 file headless, 50996 righe dense, prove WPF e librerie. Chiusura nel [collaudo F2](chiusura-blocco-f2-20261012.md).

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
- [x] Segnalazioni dell'utente dell'8/10 sera, dalla prova della build dell'integrazione 3 («appunti da correggere poi»),
  chiuse il 9/10 (prove in `supporto/artefatti/refactoring/segnalazioni-8-10` del checkout principale):
  - [x] tema scuro, scritte del menu in alto (File, Aspetto): non riproducibile con il codice di integrazione-3 (1c09d78).
    Fuori schermo il menu è leggibile in Scuro e Molto scuro all'avvio, dopo aver aperto e chiuso una tendina e dopo il
    cambio dal menu Aspetto (diagnosi `--check-contrast <cartella> --diagnosi-menu`); l'utente, con la Release compilata
    il 9/10 dallo stesso commit, conferma che «File» e «Aspetto» si leggono. Nessuna modifica del codice di produzione.
  - [x] pali: secondo anello interno nel palo orizzontale (sezione principale, schede dei tratti, «Applica armatura» e
    riapertura del verificatore c.a.), registro F2-16 (0cada3f prova, 4a147ea correzione). In coda (seguiti di F2-16, più
    sotto): l'anello di diametro diverso dalla prima fila, che la distinta della libreria rifiuta, e l'anello nei report
    del palo.
  - [x] nota «Stress block: risultati SLU meno precisi» sotto il legame del calcestruzzo, visibile solo con lo stress
    block, nella sezione c.a., nel palo orizzontale, nei muri e nel nuovo materiale (84f6942 prova e correzione
    successiva).
- [ ] In coda a tutte le altre attività (decisione dell'utente dell'8/10):
  - analisi dei bug del codice di calcolo, corsa `wf_9c55965c-68b` ferma con 85 agenti in cache;
  - revisione delle pagine dei metodi ca.sle-tensioni e ca.fessurazione (U6);
  - traccia Wiki (W). Fra le voci da fare:
    - la guida pratica (riga 19) cita ancora la tendina dell'aspetto nella Wiki, tolta l'8/10;
    - la sezione «Materiali tendini e analisi di esercizio» della guida teorica, a cui rimanda il «?» accanto al tipo
      di analisi della scheda Tensioni, va completata con la scelta fra analisi lineare e non lineare (legami di
      progetto, avviso dell'8/10).
- [ ] Secondo anello interno del palo orizzontale, seguiti di F2-16 (decisione dell'utente del 9/10: «aggiungilo nella
  lista di cose da fare successivamente insieme alla correzione vista»):
  - [ ] anello di diametro diverso dalla prima fila: la distinta di `GPC.Checkers.Concrete.Piles.PileReinforcement`
    rifiuta i diametri misti e il calcolo elastico del palo si ferma; serve la distinta per diametro nella libreria
    (release successiva di `lib/Checker`), poi la prova `ElasticPile.UiChecks --inner-ring-only` con l'anello misto;
  - [ ] report del palo (ReportElasticPile, ReportOrizzontale) con l'anello, quando l'utente sblocca i report.
- [ ] Report di calcolo: non si modificano finché l'utente non lo dice (decisione dell'8/10).
- [ ] Dopo la fine di tutto il refactoring (richiesta dell'utente dell'8/10, «in coda a tutte le cose implementa il
  "detailed check" dentro il modulo design bridge con le info che sono presenti sul sito
  https://thebridgeeng.com/design. metti in coda alla fine di tutto il refactoring»): verifica di dettaglio nel
  modulo Bridge Design.
  - Il sito serve solo come riferimento funzionale, per capire ambito e contenuti della verifica. Si rilegge quando
    il lavoro parte.
  - Metodi e coefficienti si prendono dalle norme e si implementano nelle librerie (GPC.Design o Checker), non in
    ANTHEA.
  - Come vuole AGENTS.md, interfaccia, guide e report non contengono testi, figure né rimandi al sito.
  - Si progetta con un piano dedicato e con le decisioni dell'utente.

## Bozza del modulo Modelli — 9 ottobre 2026

Attività autonoma autorizzata dall'utente nel branch `codex/model-viewer-draft`, base `781b6a9`; non chiude né anticipa
le fasi del refactoring. Specifica e attività future: [modelli-visualizzatore](../sviluppo/modelli-visualizzatore.md).

- [x] Modelli autonomi nella gerarchia progetto/fase/sottofase, più modelli e fogli nello stesso contenitore.
- [x] Libreria di importazione e archivio senza WPF; ViewModel senza WPF con comandi asincroni; vista XAML e renderer separati.
- [x] Mesh, volumi preliminari con offset delle piastre, risultati importati e contouring per elemento.
- [x] Riferimenti versionati ai fogli, persistenza e controlli mirati; nessun trasferimento automatico di sollecitazioni.
- [x] Allineamento di main ANTHEA 1.1.0 nel branch; vista Modelli autonoma con albero, tab Sollecitazioni/Verifiche e proprietà.
- [x] Assi e assegnazioni da GPC, risultati beam/principali importati, unità/stazioni, ID, colori, isolamento/wireframe e tabelle sui dati salvati.
- [x] Integrazione di sviluppo FRLS/RIGD/ELNK nel branch separato Model codex/viewer-boundary-data, senza aggiornare lib/Checker.
- [x] Collaudo della copia completa MIDAS, 90 test GPC, 46 controlli ModelWorkspace, WPF sintetico/reale e quick 25 PASS; registro MOD-02.
- [x] Acquisizione S4/Model 4 da main: full 79 PASS, 1 KNOWN già censito e nessun nuovo fallimento; 1196 test GPC superati.
- [x] 11 ottobre: filtri per attributi combinabili, visibilità con nome nella sessione, scale globali/simmetriche/manuali e tabelle delle proprietà non assegnate; 67 controlli ModelWorkspace, WPF sintetico 21 e reale 44, registro MOD-03.
- [x] 11 ottobre: navigazione e proprietà riducibili a barra, menu contestuale e tabella degli elementi associati, terne nodali esplicite, ambito della selezione e interruttori per ogni livello; ModelWorkspace reale 78, WPF sintetico 27 e reale 49 PASS, registro MOD-04.
- [x] 11 ottobre: predisposizione MVVM della barra di avanzamento delle verifiche, attività e conteggio, annullamento cooperativo e gestione di errore/fine; motore e assegnazioni sezioni ancora da collegare, registro MOD-05.
- [x] Integrazione GPC e calcolo locale di combinazioni/inviluppi nel prodotto: MOD-30, set R e successivo set S; limiti prestazionali e casi ancora da confrontare nelle voci dedicate sotto.
- [ ] Archivio dei risultati per grandi modelli e storico completo delle copie: resta da implementare.
- [x] 11 ottobre: menu delle impostazioni vista, navigazione e viste standard/45°, Esc, palette e antialiasing; contratto e messaggi per aggiornamento interrotto con conservazione della copia valida. Registro MOD-06.
- [x] Finestra di prova interattiva aggiornata: controllo WPF reale, scorciatoia alle impostazioni e simulazione esplicita dei cinque avvisi MIDAS, senza connessioni o modifiche ai file sorgente. Combinazioni locali e confronto revisioni restano separati dalla preview.
- [x] Confronto revisioni collegato all’importazione delle copie: elenco e grafica, aggiunte/modifiche/cancellazioni, delta per campo su richiesta, annullamento e report con provenienza; MOD-19.
- [x] Connettore MIDAS live e aggiornamenti parziali delle copie collegati alla UI, con gestione del rifiuto; collaudi del 12/10, MOD-21 e documentazione importazione.
- [ ] Storico completo delle copie e dei risultati nel progetto e completamento del worker Straus: integrazione successiva.
- [x] Rilascio Modelli, guide globali e merge conclusi il 12/10 su autorizzazione dell’utente: main/origin `9b42f093`, set S coerente (Converter 2.0.3.1 e Model 5.0.0.1), guide pratiche/teoriche Rev36 con Word/PDF e indici aggiornati. Full `20261012-170114-release-s-full`: 88 PASS, 1 KNOWN storico ora risolto in F3.3a, zero NEW-FAIL; tutte le suite WPF Modelli superate. Impronte delle DLL ricontrollate il 12/10 sera. Resta aperto soltanto quanto elencato nelle voci successive (prestazioni, generatori, principali, archivio storico).
- [x] 11 ottobre: esperimento sui 49 casi statici completi, 73 definizioni Add/Envelope, misure C# e confronto numerico. Somma delle combinazioni fuori dalla UI e dalle librerie di produzione; dettagli e limiti in [benchmark casi base](../sviluppo/modelli-benchmark-casi-base.md), registro MOD-07.
- [x] 12 ottobre: gestione delle combo importate e locali, CRUD, griglia coefficienti, finestra staccabile, persistenza e risultati GPC collegati a vista/tabelle. Cache del modello normalizzato, governanti e stati concomitanti; GPC.Converter acquisito dallo stesso set R. Confronto di 109744 valori plate riuscito. [Dettagli e limiti](../sviluppo/modelli-combinazioni.md), MOD-30.
- [x] Collaudo combinazioni: full 83 PASS / 1 KNOWN / 0 nuovi fallimenti; 426 controlli logici finali e tre suite WPF Modelli passate. Model rieseguito sul set rilasciato: 1192 PASS, 2 esclusi; importazione malformata rifiutata prima di modificare il documento.
- [x] Combinazioni nella scheda sinistra, ordine Vista/Modello/Combinazioni/Risultati/Verifiche/Report e barra ridotta coerente; finestra staccabile conservata. Coefficienti ripuliti solo nella visualizzazione. Corsa finale `20261012-154019-combo-tab-sinistra-finale`, nessun nuovo fallimento.
- [ ] Ottimizzare il motore integrato e ampliare il confronto a ENV_SLU completo e beam reali: ricostruzione a caldo misurata 7,606 s, obiettivo 5 s non ancora raggiunto. Il precedente inviluppo MIDAS completo ha superato 180 s; i tempi del prototipo non certificano quelli GPC.
- [ ] Diagnostica e scalabilità degli inviluppi annidati: anticipare in UI il limite di 4096 alternative e spiegare il rifiuto, mantenendo la definizione. Nel documento aperto il 12/10 sera ENV_SLE_R è una somma lineare con G1 fra i termini e genera CombinationExpansionLimit; nella copia MIDAS conservata è invece un inviluppo delle cinque SLE_R (736 alternative). ENV_SLU originale genera 26168 alternative. Verificare conteggio e dipendenze in GPC, poi valutazione senza espansione cartesiana completa, con attesi indipendenti e governanti concomitanti. Non cambiare automaticamente il tipo dalla sola etichetta ENV.
- [ ] Generatore automatico delle combinazioni: fase successiva richiesta dall'utente, con regole e coefficienti nelle librerie GPC e collegamento al medesimo catalogo/editor. Non implementato nella gestione manuale.
- [ ] **In coda dopo combinazioni e inviluppi — richiesta del 12 ottobre:** calcolo GPC delle componenti principali dei plate (`Mmax`, `Mmin`, `Fmax`, `Fmin`) e collegamento al selettore Principali, contouring, tabelle e report. Per ogni combinazione calcolare prima lo stato locale combinato e poi le principali; per gli inviluppi valutare le principali dei singoli stati simultanei e conservarne estremi e governanti. Non sommare le principali dei casi base né ricavarle da estremi locali non simultanei. Confronti indipendenti e MIDAS; distinguere disponibilità dei dati e supporto al calcolo nella UI. Gli assi principali delle sezioni delle aste richiedono una copertura distinta.
- [x] 11 ottobre: contouring plate con media opzionale ai vertici condivisi, confini per proprietà/materiale/terne, legenda sul campo grafico e dati originali invariati. Attesi manuali e verifica dei valori nel buffer GPU; 129 controlli di logica e 74 WPF reali, registro MOD-08.

- [x] 11 ottobre: schede Modello/Risultati/Vista/Verifiche, selezione grafica/per ID/per valore, clipping, opacità, letture/estremi, confronto con camera comune, viste locali persistenti e PNG; carichi riconosciuti e deformata solo da dati espliciti. Verifiche ingegneristiche invariate. Registro MOD-09.

- [x] 11 ottobre: impostazioni unificate nelle schede e compositore Report MVVM; figure dinamiche in serie, testi, riferimenti alle verifiche, ricetta portabile e PDF/HTML con rigenerazione isolata. Archivio nel progetto e provider degli esiti ancora da implementare. Registro MOD-10.

- [x] 11 ottobre: Geometria e Volumi e offset spostati nella scheda Vista, senza cambio involontario di scheda; accesso ai risultati dalla loro scheda e barra superiore alleggerita. Registro MOD-11.

- [x] 11 ottobre: risultati separati dalla rappresentazione; contouring plate sui volumi con offset e diagrammi/contouring delle aste combinabili in wireframe o estrusione, incluse viste e report. Registro MOD-12.

- [x] 11 ottobre: deformata nodale completa, campo degli spostamenti globali, scala automatica/reale/manuale, sagoma iniziale, animazione con buffer riutilizzati e import dei 49 casi MIDAS. Verificati chiaro, scuro e molto scuro su schede, menu e tabelle. Registro MOD-13.
- [x] 11 ottobre: pacchetto completo dei 49 casi acquisiti (forze e traslazioni, 123,8 MB), lettura in streaming e limite 256 MiB; impronte delle revisioni identiche al contratto precedente. Apertura nel progetto e rendering verificati. Registro MOD-14.
- [x] 11 ottobre: diagrammi 2D dell'asta selezionata, sei componenti, cursore comune, campioni distinti dalle interpolazioni, CSV e figure dinamiche di report. Legenda adattiva per viewport ridotti. Registro MOD-15.
- [x] 11 ottobre: ritaglio a volume XYZ, contorno opzionale, geometria estrusa e deformata statica, selezione e report coerenti; MOD-16.
- [x] 11 ottobre: misure fra nodi (originale/deformata reale), angoli, viste e report; inquadratura rispettosa della camera e del taglio, proiezione immediata corretta. MOD-17.

- [x] 11 ottobre: topologia grafica dei bordi plate coerente con isolamento e visibilita; 11 controlli WPF sintetici/reali, cattura prima e dopo. MOD-18.
- [x] 11 ottobre: legenda interattiva e colori per categoria personalizzabili, selezione/isola/nascondi/mostra, viste e report; MOD-20.
- [x] 11 ottobre: quattro modalità di importazione della copia, compatibilità rigorosa per i soli risultati, annullamento, rimozione esplicita delle sollecitazioni nei modi senza risultati e provenienza; MOD-21.
- [x] 11 ottobre: preview diretta del pacchetto completo, risorse incorporate e indicatore di caricamento; lettura 2,3–2,4 s, prima vista circa 8 s inclusa inizializzazione WPF/Direct3D. MOD-22.
- [x] 11 ottobre: controlli coerenti nei tre temi, cubo di orientamento corretto e clic verificati, ridisegno dopo ripristino camera e pannelli inferiori adattivi. MOD-23.
- [x] 11 ottobre: versi degli elementi, etichette fedeli dei DOF, dimensione simboli, collegamento alla tabella e confronto coerente; aree strumenti scorrevoli per conservare l'albero. MOD-24.
- [x] 11 ottobre: letture del modulo degli spostamenti e della lunghezza delle aste delegate alle operazioni geometriche GPC tramite adattatore, con cattura prima/dopo identica; MOD-25.
- [x] 11 ottobre: esportazione del controllo incorporato e ridimensionato corretta, tema applicato al primo avvio, preferenze della preview separate e strumento di confezionamento con tre avviatori; MOD-26.
- [x] 11 ottobre: sincronizzazione del layout prima della cattura Direct3D, verificata aprendo i diagrammi ed esportando immediatamente; 12 controlli WPF. MOD-27.
- [x] Collaudo della build e25b7ed: standard 43 PASS / 1 KNOWN / 0 nuovi fallimenti; WPF isolato 138 PASS, diagrammi/report 17 PASS, deformate/report 9 PASS, tutte le otto pagine PDF controllate.
- [x] 12 ottobre: importazione MIDAS API dal modulo Modelli, scelta dei casi, quattro ambiti, progresso/annullamento, controllo completezza e stabilità della sorgente; dati normalizzati riusati solo a input identico. Prova reale G1 identica al riferimento; 381 controlli logici e 17 WPF. MOD-28.
- [x] 12 ottobre: predisposizione Straus/Strand7 R3 con selezione modello/risultati/API, controllo file e DLL nativa x64 e contratto del futuro worker; importazione esplicitamente disattivata fino all'integrazione del convertitore distribuito. MOD-29.
- [ ] Modelli: includere i convertitori nel set GPC coerente, completare la normalizzazione della prima importazione MIDAS e attivare il worker Straus/Strand7 con prova nativa. Versioni secondo la delega del 12/10; nessun aggiornamento isolato delle DLL.
- [x] Collaudo finale importazioni: profilo quick 27 PASS, nessun nuovo fallimento; 381 controlli logici, 17 WPF del pannello e 32 WPF di regressione dell'integrazione. Evidenze `20261012-120857-import-midas-straus-finale` e `20261012-121018-import-ui-regressione`.
- [x] 12 ottobre: guida pratica Rev33, capitolo 20 sull'importazione MIDAS nel modulo Modelli in prova; connessione, quattro ambiti, selezione sostitutiva dei casi, salvataggio, revisione e recupero dagli errori. Aggiornati Word, PDF e indici; Rev32 pratica archiviata con SHA-256. Confronto dei 60 fogli precedenti del corpo del PDF senza variazioni di contenuto. Evidenze `supporto/artefatti/guide_anthea_itec_rev33`.

## Fondazioni superficiali — 12 ottobre 2026

Attività autorizzata dall'utente, ramo `refactoring/f5-fondazioni-superficiali`; registro FON-01 e [scheda tecnica](../sviluppo/fondazioni-superficiali.md).

- [x] Lettura integrale del manuale e riscontro visivo delle formule, mappa delle API e delle lacune.
- [x] Nuova API statica in Checker con coefficienti normativi da Model, dieci test indipendenti; 108 regressioni esistenti invariate.
- [x] Documento e report nella libreria senza WPF, foglio ANTHEA, archivio, dettagli e aiuto contestuale; test sorgente isolato.
- [x] Due capitoli nelle guide globali, esempio verificato, figura originale e indice Wiki rigenerato.
- [x] Guide Rev33 rigenerate e controllate, Rev32 archiviate. Standard: 45 PASS, 1 KNOWN già censito, 0 nuovi fallimenti.
- [x] Geotechnics 0.1.2.0 assegnata dall'utente, prima implementazione integrata e pushata su Checker; estensioni sul ramo `codex/fondazioni-visualizzazioni`.
- [x] Profili Soil/SoilLayer/SoilProfile di Model riutilizzati, tabella degli strati come il palo verticale, CLS e terreno condivisi nei Progetti, pagina unica con schede soltanto grafiche; registro FON-02.
- [x] Pressioni di contatto senza trazione, area efficace nei due assi e schema illustrativo di Prandtl; 23 test Checker, 67 controlli documento/Progetti/UI, input richiudibili senza perdere il risultato.
- [x] Collaudo estensione: standard 45 PASS / 1 KNOWN / 0 nuovi fallimenti; runner mirato documento/UI tutto PASS. Wiki e guide Rev34 rigenerate, controllo grafico completato, Rev33 archiviate.
- [x] Rilascio coerente S con versioni assegnate e pubblicate dall’utente: provenienza e impronte verificate, suite del treno conservate, manifest installati e runner full superato. Adattatore Fondazioni aggiornato al contratto 2; dettagli sotto (FON-03).

## Integrazione F4 e combinazioni — 12 ottobre 2026

- [x] Su richiesta dell'utente, integrati i contenuti di `refactoring/f4-riuso-api` nel ramo `codex/model-combinations`: nessun conflitto nel codice; conservati i registri di entrambi i rami.
- [x] Riunite le guide: pratica Rev35 con Fondazioni e importazione Modelli (capitolo 21), teoria Rev34 invariata. Indice Wiki di 66 articoli; revisioni originali archiviate con SHA-256.
- [x] Compilazione e runner quick dell'integrazione: 31 PASS, 0 NEW-FAIL (`20261012-162043-merge-f4-combinations`), con le DLL distribuite correnti.
- [x] Installato il set S dopo le versioni curate dall’utente; runner full 88 PASS, 1 KNOWN preesistente, 0 NEW-FAIL. Merge su main e push autorizzati dopo il collaudo.

## Aggiornamento DLL al set S — 12 ottobre 2026

- [x] Verificate le versioni assegnate e pubblicate dall'utente: set coerente S, dodici DLL e nove suite originali, 2698 PASS e 17 fallimenti Steel già censiti. Commit e SHA-256 controllati; installato il sottoinsieme ANTHEA con `Update-Snapshot.ps1 -ReleaseSet`.
- [x] Collaudo full: 88 PASS / 1 KNOWN preesistente / 0 NEW-FAIL, nessun file alterato dalle suite. Baseline: 6 PASS, 548 file completi e 50996 righe / 27 file densi senza differenze non ammesse; attesi e tolleranze invariati. DelaunayMesh identica e 39 prove di mesh/contratti del set superate. Evidenze `20261012-170114-release-s-full` e `20261012-165624-release-s-baseline`.

- [x] Adeguato il confine Fondazioni al contratto 2 pubblicato: coefficienti Hansen, dominio esaurito, rapporti indefiniti e viste indisponibili. FON-03; 44 controlli funzionali e 86 WPF passati. Guide globali Rev36 allineate e precedenti conservate.

## Blocco autorizzato il 12 ottobre 2026 — chiusure F2 e supporto reologia

L’utente approva i primi cinque passi proposti e aggiunge il punto 5.2; un commit
per ogni punto concluso, con aggiornamento ed evidenze. Le voci seguenti coordinano
le attività già tracciate, senza dichiarare chiusi i residui delle fasi successive.

- [x] 1. API-02b, collegamento sul ramo: set candidato coerente Concrete 0.0.27.2, percorsi nativi di interassi/dettagli/fessurazione attivati e confrontati. 266 casi congelati, atteso geometrico indipendente, 548 file headless e 50996 righe dense; differenze API-02 classificate puntualmente. Evidenze `20261012-212422-api02-chiusura`. Il candidato è poi confluito nel set T definitivo installato e collaudato nel punto 5.
- [x] 2. Validazioni approvate: F2.7-U5 (φ/n validati dalla API GPC, errore accanto al campo, dato derivato svuotato e recupero nelle tre SLE), F2.7-U7 (`staffe_presenti`: soltanto Sì/No, assente compatibile con il default), F2.8-U2 (rami interi 2–100 se usati, nessuna troncatura). Riutilizzati parser e validazioni esistenti. Prove ingressi/WPF, 548 file headless invariati salvo tempi, suite estesa 157808 + 673588 controlli e banco 50996 righe: soltanto cinque casi con rami −2 ora rifiutati. Catalogo testi verificato per contenuto e conteggi indipendentemente dall’ordine di prima comparsa. Registro F2-21; chiusura `20261012-215329-f2-input-chiusura` (7 PASS, nessun fallimento).
- [x] 3. SLE/dettagli: F2.7-U1 espone i 13 interassi NTC già usati nelle formule, lasciando invariati wk/tassi/esiti; U3 attiva la regola GPC dei getti sottili anche per UNI con appendice italiana (limiti CLS, αcc/fcd ×0,8). A10 già chiusa in 1e9998e. Registro F2-22; 548 file headless invariati salvo tempi e 50996 righe dense con sole 13 differenze dichiarate. Suite estesa: 157808 + 673640 controlli, 27 stato statico e 369 getti sottili. Corsa finale `20261012-220834-f2-sle-chiusura`, 6 PASS, nessun nuovo fallimento.
- [x] 4. Mesh e parallelismo: rimosso il lock globale di costruzione dopo la cattura preventiva. 326 controlli (36 stati, sezioni condivise e indipendenti, analisi lineare/non lineare, viscosità), 80 mesh identiche bit per bit e 548 file headless invariati salvo tempi. Restano i lock locali del solutore e della cache grafica. Registro F2-23; chiusura `20261012-221914-f2-concorrenza-dopo`, 6 PASS e nessun fallimento.
- [x] 5. Collaudo del blocco F2: set T da master pushati, 2742 prove di libreria superate con sole esclusioni note; full ANTHEA e confronto finale completati, 548 file e 50996 righe invariati salvo dati volatili/identificativi, 80 mesh bit per bit. ANTHEA 1.3.0 e guide Rev37. Il confronto fixture riconosce ora le 13 differenze F2-22 già approvate, senza modificare attesi o tolleranze. [Esiti e limiti](chiusura-blocco-f2-20261012.md). Il legacy attivo resta conservato.
- [x] 5.2. Finestra autonoma «Reologia del calcestruzzo», WPF/MVVM, richiamabile dai punti pertinenti: dati del CLS e di maturazione, φ(t,t₀), φ a infinito, E(t) e fattore E(t)/E₂₈, grafici e tabella. Calcoli in GPC Model; separazione fra modulo elastico per maturazione e coefficienti di viscosità. Guide globali e PDF aggiornati; nessun report di calcolo esistente modificato.

Punto 5.2: implementato in `ANTHEA.DesignTools.Presentation` senza WPF e vista che riusa Plot/Ui.Dialog. Richiami da Strumenti, materiali e SLE; 57 controlli di stato/numerici e 21 WPF, guide Rev37 e Wiki aggiornate. [Architettura ed evidenze](f2.5-reologia.md). Model 5.1 e set T da commit pubblicati; ANTHEA 1.3.0 per la nuova funzionalità. Il collaudo full e il confronto finale sono conclusi nel punto 5.

### EXCEL-SOL — avanzamento

- [x] Contratto unico, instradamento e criterio resistente indipendente; EXCEL-SOL-01, controlli prima/dopo PASS.
- [x] Nuovo Excel, editor MVVM, conversione esplicita e report; EXCEL-SOL-02, 1229 controlli Application e suite WPF passati.
- [x] Guide Rev41, indici e Wiki aggiornati; Word/PDF controllati su 222 pagine, 337 formule conservate. Rev40 archiviata con impronte. Versione applicazione 1.7.0 per la nuova funzionalità; DLL GPC invariate.
- [ ] Collaudo generale e rilascio.

- [x] EXCEL-SOL-UI: contenitore opaco e spaziatura della finestra verificati anche in esportazione immagine; suite WPF dedicata PASS (20261013-144358-excel-sol-window-final).

- [x] EXCEL-SOL-UX: percorso Excel storico proposto nel nuovo editor, messaggi coerenti con sostituzione/aggiunta; avviso di migrazione aggiornato anche quando si elimina l’ultima riga storica.
