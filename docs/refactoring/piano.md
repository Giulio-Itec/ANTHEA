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
7. `lib/Checker` si rilascia solo da commit pushati, con versione successiva a quella del
   manifest, suite complete e controllo delle mesh DelaunayMesh.
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
- [x] F0.9 Snapshot S1 installato in `lib/Checker`: suite invariate, cattura identica a B0, griglie dense identiche, 80 mesh identiche bit per bit, test delle librerie con i soli fallimenti storici (due aspettative superate corrette in Geometry e Model). Push delle librerie in attesa dell'utente.
- [x] F0.10 Correzioni a costo zero (puntatori rotti, README, riferimento esterno nel report Bridge Design: 79da697); D7-g ripristinato (0d861de); runner con `dotnet exec` (f4d81ad).
- [~] F0.11 [CP] Baseline B1, tag, merge e push su autorizzazione. Fatti: B1 in `supporto/artefatti/baseline/F0-B1` (commit b4d2e81; rispetto a B0 solo le 6 righe di testo volute di F0.10 e D7-g); tag locali `refactoring/post-f0` e `refactoring/pre-f1`. Restano all'utente: approvazione della fase, merge su main e push (ANTHEA e librerie).

## Fase 1: test fuori dall'eseguibile

- [x] F1.1 Rompere la dipendenza produzione → test (`HorizontalWorkspace.Smoke`, `HorizontalChs.VerifyChs`, sonde di `BridgeTorsion`) (429e4c0).
- [x] F1.2 Configurazione `UiTests` con harness e flag; exe pubblicato pulito; installer che blocca codice di test; log degli errori non gestiti (d11af03).
- [x] F1.3 Autotest fuori da X.Materiali e dal motore (e805280).
- [~] F1.4 [CP] `IConfirmationService` e `IMessageService` al posto degli agganci di test; 4 scenari verificati a mano. Codice fatto (6468b7d); mancano le prove WPF a schermo e i 4 scenari manuali.
- [x] F1.5 `tests/ANTHEA.Desktop.UiTests` non creato: `--check-wiki-offscreen` usa 16 membri privati di MainWindow; resta la configurazione `UiTests` e le suite migrano modulo per modulo in F5 (motivi in [progetti-di-test.md](progetti-di-test.md)).
- [~] F1.6 [CP] Tabella "destino dei progetti di test" in [progetti-di-test.md](progetti-di-test.md), da approvare; ConcreteDesign.DesktopChecks e ValidationIllustrations portati nella configurazione `UiTests` e spostati in `supporto/SUPERATI/test`.
- [ ] F1.7 Chiusura: stessi esiti di B1.

Tra F1 e F2: regressione in MSTest (464 casi singoli) e `casi_confronto.json` diviso per caso [CP]; GitHub Actions per gli stadi non UI (facoltativo) [CP].

## Fase 2: chiusura del c.a.

- [ ] F2.1 Cattura pre-M4 del legacy attuale; matrice delle API; casi vicini alla soglia; test con sezioni in forma chiusa.
- [ ] F2.2 [CP] Pagine dei metodi c.a. (revisione tecnica).
- [ ] F2.3 [CP] GPCChecker.Concrete: dettagli di solette e pareti; mesh sicure in parallelo al posto del lock; tracce per i report; opzioni degli scostamenti.
- [ ] F2.4 [CP] Rilascio `lib/Checker` con codice ANTHEA invariato.
- [ ] F2.5 Strato di mappatura con contratto JSON invariato.
- [ ] F2.6 Taglio e torsione → libreria.
- [ ] F2.7 Fessurazione e SLE, senza stato statico.
- [ ] F2.8 Dettagli, ancoraggi, aderenza, M–χ.
- [ ] F2.9 Durabilità, copriferri, scheda Materiali, validazione di progetto.
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
- [ ] F4.5 Coefficienti dalle norme (γ dei pali da PileExecution); scostamento (d) in due commit.
- [ ] F4.6 Inventario delle soglie dipendenti dalle unità; palo elastico in N e mm.
- [ ] F4.7 Muri in c.a. in `Concrete/Walls` (tre passi).
- [ ] F4.8 Muri a gravità e dettagli.
- [ ] F4.9 [CP] Collegamento dei muri e rilascio `lib/Checker`.
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
- [ ] F5.13 [CP] Shell; sessione di prova prima di eliminare SheetEditor e la vecchia MainWindow.
- [ ] F5.14 Chiusura: X.Desktop → ANTHEA.Desktop, terza revisione di AGENTS.md.

## Wiki (in parallelo)

- [ ] W0.2 [CP] Pipeline riproducibile da clone pulito (figure ed esempi, template Word, dipendenze Python).
- [ ] W0.3 [CP] Archiviazione degli script una tantum.
- [ ] W0.4 Pulizia del diario di sviluppo e dei riferimenti a concorrenti, con controllo automatico; paragrafi duplicati.
- [ ] W0.5 Eliminazione del corpus esterno e di tutti i riferimenti esterni da fonti, exe e PDF (decisione del 6/10: solo contenuti nostri).
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
