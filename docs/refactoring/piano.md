# Refactoring di ANTHEA: piano operativo

## Spettri NTC e isolamento — 19 ottobre 2026

Attività richiesta dall'utente su `refactoring/f5-azioni-spettri`, derivato da `refactoring/f5` b9c2d4b1. [Dettaglio ed evidenze](spettri-ntc.md).

- [x] Riferimenti acquisiti, catture Excel indipendenti, motore Spettri in GPC.Design: 19320 confronti passati, inclusi nodi esatti e tutte le configurazioni triangolari.
- [x] Isolamento e pendolo, attesi dell'esempio e controllo analitico; differenze intenzionali esplicitate.
- [x] Set candidato coerente verificato nelle librerie, senza push o merge.
- [x] Collaudo mirato del nuovo modulo MVVM Azioni e guide globali Rev48 con Word/PDF e Wiki.
- [x] Sola lettura della ricerca dei comuni allineata al contratto delle revisioni; due prove dei progetti e vista Spettri passate. Catture dei casi limite conservate nei test permanenti.
- [x] Limite Tis ≤ 4 s verificato sul testo NTC 2018 §3.2.3.2 e imposto nel motore GPC.Design 0.22.0.1, con prove al bordo e aggiornamento delle guide.
- [x] Rendering Wiki corretto e verificato; confronto completo F3 di 1070 file senza differenze non ammesse, dopo la classificazione puntuale dei soli metadati e delle aggiunte editoriali (SPETTRI-04).
- [x] Tracce F5 Fondazioni conservate e banco adeguato al set: tutte le prove Presentation e le due viste Fondazioni passate (`spettri-f5-metadata-02`, 5 PASS).
- [x] Contratto MVVM Palificata corretto e verificato nella ripetizione `spettri-final-rerun-01`.
- [x] Collaudo di consegna Spettri sul set 03: 7 PASS, zero NEW-FAIL, inclusi 1572 controlli applicativi, 43 WPF e 45 catture del contrasto (quattro fasi nei tre temi, zero difetti a 144 DPI). Anteprima autonoma della Release bea5211b predisposta.
- [~] Runner full 04 completato sul set 03: 159 PASS, quattro NEW-FAIL; Palificata risolta. Le due esportazioni arrestate da Trend Micro si fermano anche nella ripetizione e restano aperte. Contrasto generale da ripetere a 96 DPI secondo D6 (macchina a 144 DPI). Esiti ed evidenze in `spettri-ntc.md`.
- [ ] Rilascio definitivo delle librerie dopo autorizzazione al merge/push di Checker su master.

Piano approvato il 6 ottobre 2026. Decisioni e architettura: [decisioni.md](decisioni.md).
Il dettaglio di ogni passo (file, comandi di verifica, criteri di uscita) è stato preparato
in `supporto/artefatti/refactoring/piano/` (cartella non versionata).

**[CP]** indica un punto in cui serve l'approvazione dell'utente. Lo stato si aggiorna a
ogni commit: `[x]` fatto, `[~]` in corso, `[ ]` da fare.

## Residui di F2 — 15 ottobre 2026

L'utente ha deciso i residui di F2 sul [foglio dei residui](f2-residui-decisioni.md): «E1–E8 come nel foglio», più
R1, R2, il punto 3 di F2-7, i muri (E1-D4) e le superfici interne delle sezioni cave ([decisioni](decisioni.md),
sezione del 15 ottobre).
- Chiusi con le decisioni: F2.3, F2.7, F2.8, F2.10, F2.12 e F2.13; ratifiche di F2.6.
- Chiusi il 16/10 gli ultimi punti: gli attesi indipendenti di E3 (`ae8f429f`) e il seguito di E6 nel runner
  (`8be5f0a4`), già in main, e F2.11c (E5) sul ramo `refactoring/f2-11c`. **Con F2.11c F2 è chiusa**; resta il merge del
  ramo su `main`, con l'approvazione dell'utente.
- STG-4 e SEZ-3b passano nella traccia del modulo Modelli (E8).
- R1, R2 e F2-25 sono distribuite con la release Y delle librerie (ANTHEA `2257e991`).
- [Analisi dei bug](analisi-bug-20261015.md) del 15/10: piano approvato dall'utente, correzioni nella release delle
  correzioni, in corso.

## Chiusura di F4 — 15 ottobre 2026

### Attività autonoma successiva su richiesta dell’utente

- [x] STRAT-5: scambio interno del terreno fra fogli, copie indipendenti, selezione
  medi/minimi e controlli di compatibilità. Application 1582 controlli e UI 25;
  Architecture e Wiki PASS. Guide globali Word/PDF Rev46 aggiornate.
  Il pulsante AI resta visibile e disattivato su richiesta dell’utente.

- [x] STRAT-1, 15/10: modulo singolo Stratigrafia del terreno, da `main` `9df7e475`,
  branch `codex/modulo-stratigrafia`. Editor, contratto documentale senza WPF,
  persistenza e report DOCX/PDF implementati; guide Rev46. Collegamento ai motori
  geotecnici rinviato. Non avvia F5. [Contratto e verifiche](../stratigrafia.md).
  Quick `20261015-213157-stratigrafia-finale`: 46 PASS, un fallimento nel conteggio
  statico dei capitoli Wiki (71 precedenti più 2 nuovi). Corretto il controllo
  mantenendo la verifica dei 71 preesistenti; build e suite Wiki e UI stratigrafia
  `20261015-213831-stratigrafia-conclusione`: 4 PASS, zero NEW-FAIL.
  Nessuna modifica a motori o DLL GPC; full non eseguito per questa aggiunta.
- [x] STRAT-2, STRAT-3 e STRAT-4: importatore PDF tramite OpenAI con anteprima revisionabile,
  fonti e profili separati; medi principali, minimi opzionali, scelta di entrambi.
  Menu di famiglie prevalenti e retini vettoriali, senza assegnazione di proprietà.
  Implementati e guide Word/PDF Rev46 aggiornate. Nessuna
  richiesta API reale eseguita; prove del trasporto con risposte simulate.
  Quick `20261015-215818-stratigrafia-ai-finale`: 46 PASS, un fallimento editoriale
  per il nome del servizio AI nella procedura richiesta. Lint adeguato solo per
  OpenAI nel capitolo operativo della stratigrafia e relativo indice di ricerca,
  preservando divieti di diario di sviluppo, altri prodotti e link esterni.
  Collaudo finale `20261015-221211-stratigrafia-completa-finale`: 11 PASS, zero
  NEW-FAIL e BLOCKED (build, Application, Architecture, Wiki, UI e lint Wiki).
  Le segnalazioni diagnostiche sui limiti in byte e sul metodo statico del banco
  UI sono corrette senza nuove eccezioni architetturali. Nessun full eseguito.
  Requisiti e limiti nella [coda evolutiva](../stratigrafia.md#coda-evolutiva).

**F4 è chiusa il 15/10** con le decisioni D1–D5 dell'utente ([decisioni](decisioni.md)):
[verbale di chiusura](chiusura-f4-20261015.md).
- Full conforme `20261015-105444-f4-full-190` sul commit pulito `a46e91fb`: 140 PASS, 0 NEW-FAIL, 0 BLOCKED.
- ANTHEA 1.9.0 con installer verificato.
- Merge in `main` con tag `refactoring/post-f4`.

F5 non è iniziata. La sezione seguente descrive lo stato del 14/10 ed è conservata come storico.

## Audit indipendente del 14 ottobre 2026

Controllo in sola lettura di F0–F4 chiesto dall'utente il 14/10, sul ramo F4 `7569f6fc`
(ANTHEA 1.8.0, set W). **F4 non è chiusa.** Il codice delle librerie e gli attesi
indipendenti risultano intatti, ma piano, registro e diario non corrispondevano allo
stato reale. Le correzioni di questo documento cambiano solo stati e descrizioni: nessun
punto viene dichiarato chiuso e nessuna scelta viene fatta al posto dell'utente.

- [CP] chiusi dall'agente senza approvazione: F1.4, F1.6, F2.2, F2.9, F3.1, F3.4, F3.6,
  F3.7, F4.3 e F4.14 sono ora «tecnicamente fatto, ratifica dell'utente aperta»; il
  marcatore [CP] è rimesso in F0.9, F3.1 e F3.4. Ratificati dall'utente lo stesso 14/10
  ([decisioni](decisioni.md), punto 4b; per F3.6 il solo ricalcolo del ponte, il report short al punto 4c-1).
- F2.7 e F2.8: il commit F4 `b7339f4a` (12/10) ha attivato l'interruttore A8 delle SLE e
  sostituito senza interruttore aderenza/ancoraggi, dettagli non circolari, M–χ e
  interassi, fuori dai micro-piani e contro `decisioni.md` (U6, F2.8-U1).
- Blocco del 12/10: correzioni accorpate invece di una per commit, corse di chiusura su
  alberi non committati, fallimento del full chiuso con una regola di classificazione.
- Report di calcolo modificati durante il blocco dell'8/10: 30 commit, 8 con cambi di
  testo o comportamento (ratificati e blocco tolto dall'utente il 14/10).
- F4: set W pubblicato e installato e installer 1.8.0 prodotto senza un full conforme
  (primo full 114 PASS e 5 NEW-FAIL, secondo interrotto); merge e push dei master di
  libreria del 14/10 alle 04:04 senza ok registrato; voci F4 del 14/10 scritte fuori
  dalla sezione «Fase 4» (ora spostate).
- F0.1: la junction negli artefatti esiste dal 18/09/2026, non è «ricomparsa».
- Registro e diario: stati superati, regressioni F4-36/F4-37 classificate come difetti
  legacy, voci F2-5…F2-8 e R22 mancanti, diario fermo al 12/10 01:03.

Profilo standard sul ramo F4 durante l'audit: 66 PASS e un fallimento intermittente di
`Application.Tests` (file bloccato), PASS ripetuto da solo. Le decisioni dell'utente,
punto per punto, sono in [decisioni.md](decisioni.md) (sezione del 14 ottobre, aggiornata
dal coordinatore); dove manca una decisione questo piano scrive «da ratificare».

## Controllo di completezza prima di F4 — 13 ottobre 2026

Audit su `main`/`origin/main` `90550d7e` (ANTHEA 1.7.0), incrociando piano,
decisioni, sorgenti, manifest e verbali di collaudo. **Non tutte le attività
previste prima di F4 sono chiuse. Il refactoring architetturale F3 e il blocco
EXCEL-SOL sono rilasciati; l'audit riapre la richiesta funzionale F3.4-U5 sul
report M–χ di progetto. Restano inoltre residui di F2, STG e SEZ.**
F4 resta ferma su istruzione dell'utente *(frase storica del 13/10 mattina, superata
dalla ripresa di F4 del 13/10: vedi «Fase 4»)*. Questo audit non modifica il prodotto,
non elimina file e non sostituisce un nuovo collaudo funzionale.

**Aggiornamento successivo all’audit, 13/10:** F3.4-U5 corretta e collaudata;
F3 è nuovamente chiusa. I residui delle altre fasi restano aperti e non vengono
avviati, secondo l’ultima istruzione dell’utente. Evidenze nella voce F3.4-U5 e
nel [verbale integrativo](chiusura-f3-20261013.md#integrazione-di-chiusura-f34-u5).

**Rilascio successivo, 13/10:** su richiesta dell’utente, correzione F3.4-U5
integrata nel ramo principale `main` con versione **1.7.1** (il repository non
ha un ramo `master`). Build e controlli Release aggiuntivi: 4 PASS; installer
verificato, set V invariato, guide Rev42. Dettagli nel verbale di chiusura F3.
La successiva richiesta dell’utente riapre la pianificazione F4, da svolgere
su un ramo dedicato; i precedenti arresti descrivono lo stato storico.

| Ambito | Stato verificato | Lavoro residuo prima della chiusura dell'ambito |
| --- | --- | --- |
| F0/F1 | Blocchi rilasciati; una junction locale negli artefatti non è mai stata tolta | F0.1: la junction `supporto/artefatti/verifiche_ca_auto/template/node_modules` (verso `C:\Users\g.pacini\.cache\codex-runtimes\...`) esiste dal 18/09/2026; non è ricomparsa: F0.1 del 6/10 tolse solo `supporto/scripts/programma-anthea/node_modules` (correzione dell'audit del 14/10). Tolta il 14/10 con `cmd /c rmdir` su autorizzazione dell'utente (decisioni, punto 5a), destinazione intatta. Restano separati i checkpoint MSTest/corpus e layout elencati sotto; F1.4 e F1.6 ratificati dall'utente il 14/10 (punto 4b). |
| Blocco F2 punti 1–5 e 5.2 | Concluso, compresa Reologia con φ infinito ed E(t) | Nessuno per il blocco autorizzato; non chiude tutta F2. [Verbale](chiusura-blocco-f2-20261012.md). |
| F2.3 | Solette/pareti e lock globale completati | Tracce dei report e riscontro normativo R1/R2. |
| F2.7/F2.8 | SLE, dettagli, ancoraggi e M–χ usano già le API GPC | Array statici A12, revisione delle pagine dei metodi già accodata e riconciliazione dei criteri di uscita dei vecchi micro-piani con le prove realmente eseguite. Non ricreare adattatori già presenti. Audit 14/10: quelle API sono state attivate dal commit F4 `b7339f4a` fuori dai micro-piani (dettagli nelle voci F2.7 e F2.8). |
| F2.10–F2.13 | Parziali o aperte | Accettazione finale delle differenze, residui legacy/duplicati e registri di chiusura. Rimozione del legacy solo dopo equivalenza e accordo; i lock locali necessari non sono da eliminare indiscriminatamente. |
| STG | Contratto, importazione, filtri e prove sintetiche presenti | STG-4: confronto numerico sul ponte reale e geometria attiva per fase; reazioni stage e lettura nativa Strand7 non disponibili. |
| SEZ | Dettaglio separato, geometrie condivise 2D/3D e offset supportati | SEZ-3b: sezione variabile per fase, dipendente dalla geometria attiva. |
| F3 | Chiusa, compresa la correzione F3.4-U5 successiva all’audit | Nessun punto F3 aperto. Il report CA calcola M–χ con tabella e figura; casi non applicabili e annullamento collaudati. X.Core di compatibilità resta l'eccezione dichiarata in [confini F3](f3-strato-applicativo.md). Audit 14/10: F3.1, F3.4, F3.6 (ricalcolo del ponte) e F3.7 ratificati dall'utente il 14/10 (decisioni, punto 4b); il «report short senza Word» di F3.6 è fatto il 14/10 dopo l'audit, insieme al PDF di tutti i report (F4-46). |
| EXCEL-SOL | Conclusa e rilasciata in 1.7.0 | Nessuno per i requisiti concordati: elemento facoltativo, azioni unificate, instradamento per stato limite e criterio resistente indipendente. [Verbale](chiusura-excel-sol-20261013.md). |

Evidenze controllate: 122 voci finali PASS del collaudo F3 (full con riprese
mirate), 71 voci finali PASS di EXCEL-SOL (standard e quattro suite WPF),
`build/known-failures.json` senza eccezioni ANTHEA e dieci DLL locali con
SHA-256 identici a `lib/Checker/manifest.json`. I conteggi si riferiscono alle
corse conservate, non a suite rieseguite durante questo audit. Le tre prove
esterne escluse dal runner restano dichiarate nei verbali; non sono nuovi PASS.

**Richieste Modelli già accodate, distinte dalle fasi numerate:** principali dei
plate dopo le combo; inviluppi annidati (limite 4096 alternative) e obiettivo
di 5 secondi ancora non raggiunto nella misura integrata di 7,606 s; confronto
ENV_SLU completo e beam reali; worker nativo Straus/Strand7; archivio dei grandi
risultati e storico completo. Il generatore automatico delle combo resta una
successiva attività, come richiesto. Le predisposizioni delle verifiche e dei
loro report non costituiscono un motore di verifica già collegato.

**Checkpoint non eseguiti, senza trasferirli implicitamente a F4:** conversione
dei 464 casi in test MSTest individuali e separazione del corpus; completamento
della finestra di layout dei progetti preesistenti. GitHub Actions non UI resta
facoltativa. Wiki, revisione normativa finale F5.15 e attività assegnate
esplicitamente a F4/F5 non sono dichiarate mancanti di F3.

**Seguito documentale:** riconciliare anche `f2-matrice-api.md`, i micro-piani
storici F2.7/F2.8 e le annotazioni superate del registro (per esempio API-02,
corretta e distribuita nel punto 1). Non cancellare la storia delle differenze
né interpretare una casella aperta come prova dell'assenza del codice.

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
7. `lib/Checker` si rilascia solo da commit pushati: insieme coerente prodotto da
   `Checker/build/Release-Set.ps1` e installato con `tools/libs/Update-Snapshot.ps1 -ReleaseSet <cartella>`,
   versioni secondo AGENTS.md, suite complete e controllo delle mesh DelaunayMesh.
8. AGENTS.md cambia tre volte: fine F0, fine ristrutturazione Wiki, fine F5.
9. Pacchetti esterni: elenco unico in `decisioni.md`.

## Fase 0: messa in sicurezza e riferimenti

- [x] F0.1 node_modules e `supporto/tmp` fuori dall'indice, esempi versionati (9708530). Il commit del 6/10 tolse dall'indice e dall'albero solo `supporto/scripts/programma-anthea/node_modules` e la sua junction. La junction `supporto/artefatti/verifiche_ca_auto/template/node_modules`, collegata al runtime esterno `C:\Users\g.pacini\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\node_modules`, esiste dal 18/09/2026 e non è mai stata tolta: non è «ricomparsa», come scritto nel controllo del 13/10 (correzione dell'audit del 14/10). Rimozione del solo collegamento con `cmd /c rmdir`, in attesa dell'ok dell'utente; nessuna cancellazione effettuata. Collegamento tolto il 14/10 con `cmd /c rmdir` su autorizzazione dell'utente ([decisioni](decisioni.md), punto 5a); destinazione intatta.
- [x] F0.2 [CP] Decisioni di avvio, `docs/refactoring`, prima revisione di AGENTS.md (approvata il 6/10/2026).
- [x] F0.3 Runner `build/ci.ps1` (profili quick, standard, full), `build/known-failures.json`, `Verifica.cmd` non interattiva. La prima corsa ha scoperto 5 harness fermi per DelaunayMesh 2.0.0.8 e un alias Wiki rotto.
- [x] F0.4 Baseline B0: catture headless degli 11 moduli e dei motori non coperti dal servizio, griglie dense di CheckerMigration.Capture, testo dei report, impronta delle mesh, registro dei default silenziosi; doppia corsa.
  F0.4a: toolkit unico `tests/ANTHEA.Testing` (capture, compare, normalize; tolleranze per grandezza in `tolerances.json`, default 0), B0 headless in `supporto/artefatti/baseline/F0-B0/headless` (118 casi, motori fuori dal servizio, testo dei report, archivi, registro dei ripieghi di `J.S/D/B`), doppia corsa uguale a meno dei tempi `tempi_ms`; stadio e profilo `baseline` in `build/ci.ps1`.
  F0.4b: catture dense di CheckerMigration.Capture e impronta di 80 mesh in `supporto/artefatti/baseline/F0-B0/dense`, ripetibili byte per byte (a meno di identificativi casuali). Il successivo banco di equivalenza WPF è stato aggiunto in F3.1: 36 input e 458 file delle catture D/E; non è più un'attività da iniziare.
- [x] F0.5 [CP] Diagnosi dei fallimenti noti. `--project-calculations` smascherato (122/123; l'unico fallimento è un difetto del test: confronta un GUID casuale dei default dei muri, chiuso in F3.3); BridgeAudit (9 fallimenti dal commit 0a63315: etichetta del metodo, ripristinata con D7-g, e convenzione `h_trave` nelle fixture, corretta in Checker ecf1dbe0: 302/302); alias Wiki corretto con W0.5; 8 prove WPF (non 6) che fallivano già in 2cdf9fd, 11 cause, 8 commit (rapporto in `supporto/artefatti/refactoring/diagnosi-ui/rapporto.md`): due correzioni del disegno del ponte da confermare (titolo del tag dell'anima inclinata, tag senza sovrapposizioni a 1366 × 900). Profilo full: 0 NEW-FAIL.
- [x] F0.6 [CP] Schede degli scostamenti (a)–(g) con effetto quantificato in `scostamenti.md` e registro unico `registro-differenze.json`. Decisioni dell'utente del 6/10 in `decisioni.md`; (b) da discutere. Esiti delle ricerche e voci nuove R1-R14 nel registro (7/10).
- [x] F0.7 Build: proprietà `GpcLibDir`, elenco esplicito delle DLL, verifica SHA-256 (5 harness di nuovo eseguibili) e `.gitattributes` (controllo Wiki valido anche su checkout LF). Anticipato prima di F0.4.
- [x] F0.8 [CP] `tools/libs/Update-Snapshot.ps1`, global.json nelle librerie (SDK 9.0.318), versioni nuove (commit locali non pushati), snapshot S1 da commit, build deterministiche verificate.
- [x] F0.9 [CP] Snapshot S1 installato in `lib/Checker`: suite invariate, cattura identica a B0, griglie dense identiche, 80 mesh identiche bit per bit, test delle librerie con i soli fallimenti storici (due aspettative superate corrette in Geometry e Model). Librerie pushate il 7/10 su richiesta dell'utente (Utilities df3b3e7, Geometry 6a0d1c1, Model d6631635, Checker develop 7c15cf4f). Marcatore [CP] del piano del 6/10 rimesso dopo l'audit del 14/10; l'approvazione della fase F0 del 7/10 è registrata in F0.11.
- [x] F0.10 Correzioni a costo zero (puntatori rotti, README, riferimento esterno nel report Bridge Design: 79da697); D7-g ripristinato (0d861de); runner con `dotnet exec` (f4d81ad).
- [x] F0.11 [CP] Baseline B1, tag, merge e push su autorizzazione. B1 in `supporto/artefatti/baseline/F0-B1` (commit b4d2e81; rispetto a B0 solo le 6 righe di testo volute di F0.10 e D7-g); tag locali `refactoring/post-f0` e `refactoring/pre-f1`. Fase approvata dall'utente il 7/10 e unita a main in locale insieme a F1, D7-c/D7-d/R3 e W0.4/W0.5; push di ANTHEA da confermare.

## Fase 1: test fuori dall'eseguibile

- [x] F1.1 Rompere la dipendenza produzione → test (`HorizontalWorkspace.Smoke`, `HorizontalChs.VerifyChs`, sonde di `BridgeTorsion`) (429e4c0).
- [x] F1.2 Configurazione `UiTests` con harness e flag; exe pubblicato pulito; installer che blocca codice di test; log degli errori non gestiti (d11af03).
- [x] F1.3 Autotest fuori da X.Materiali e dal motore (e805280).
- [x] F1.4 [CP] `IConfirmationService` e `IMessageService` al posto degli agganci di test; 4 scenari verificati sull'exe Release. Codice fatto (6468b7d); prove WPF a schermo superate (profilo full sul merge con F0 e diagnosi UI: 66 PASS, 1 KNOWN, 0 NEW-FAIL). Scenari eseguiti il 7/10 dal coordinatore su delega dell'utente, da ratificare, con UI Automation e mouse reale sull'exe Release di main 17b6c98 e i dialoghi di produzione, esiti controllati rileggendo i file salvati (`supporto/artefatti/refactoring/f1.4/esito.md`): avvio con la Home; dati condivisi («Aggiorna tutti i fogli collegati» e «Solo questo foglio»), conferma spostamento («Annulla» e «Sposta e aggiorna»), chiusura con modifiche («Annulla», «No», «Sì») ed errore di un comando (messaggio «Operazione non completata», applicazione viva) superati. `errori.log` registra solo gli errori non gestiti, come prima di F1: l'errore di un comando non lo scrive. **Audit 14/10: tecnicamente fatto; ratificato dall'utente il 14/10 ([decisioni](decisioni.md), punto 4b).**
- [x] F1.5 `tests/ANTHEA.Desktop.UiTests` non creato: `--check-wiki-offscreen` usa 16 membri privati di MainWindow; resta la configurazione `UiTests` e le suite migrano modulo per modulo in F5 (motivi in [progetti-di-test.md](progetti-di-test.md)).
- [x] F1.6 [CP] Tabella "destino dei progetti di test" in [progetti-di-test.md](progetti-di-test.md), decisa il 7/10 dal coordinatore su delega dell'utente, da ratificare; ConcreteDesign.DesktopChecks e ValidationIllustrations portati nella configurazione `UiTests` e spostati in `supporto/SUPERATI/test`. Esito: archiviati nella stessa cartella, invariati e con registro (`supporto/SUPERATI/registro-20261007-test.json`), BridgeDesign.SiteComparison, MaxRetainingWall.Cases e .Compare, ConcreteStressDiagnosis, ValidazioneCA20260925 e ProgrammaAnthea (4ff5026, f5647f5, d01caf7); profilo quick 18 PASS, 0 NEW-FAIL. Seguito nell'integrazione 2: generatore della relazione del 25/9 archiviato in `supporto/SUPERATI/script/validazione_ca_2026_09_25` con `build_document.py` al testo originale; ElasticPile.Performance riportato in `supporto/test` con la destinazione della tabella (3ff710e, a57aff6, cb9b750, a2a3ac1; tabella e documenti 99b2ae7); MaxRetainingWall.Cases e .Compare restano archiviati (scelta reversibile); le cartelle `bin` e `obj` ignorate rimaste nelle vecchie posizioni non si cancellano; profilo quick `int2-f16` 18 PASS, 0 KNOWN, 0 NEW-FAIL. Seguiti decisi dal coordinatore su delega, da ratificare ([decisioni.md](decisioni.md)). **Audit 14/10: tecnicamente fatto; ratificato dall'utente il 14/10 ([decisioni](decisioni.md), punto 4b).**
- [x] F1.7 Chiusura: stessi esiti di B1. Cattura headless del branch F1 uguale a B1 su 432 file (solo campi volatili); profilo full 66 PASS, 1 KNOWN (project-calculations, F3.3), 0 NEW-FAIL; tolte da `known-failures.json` le due voci WpfMath dei progetti spostati in SUPERATI. Checkpoint F1.4 (scenari sull'exe Release) e F1.6 chiusi il 7/10.

Tra F1 e F2: regressione in MSTest (464 casi singoli) e `casi_confronto.json` diviso per caso [CP]; GitHub Actions per gli stadi non UI (facoltativo) [CP]. Audit 13/10: checkpoint non eseguito; i 464 casi passano ancora nel runner eseguibile `supporto/test/X.Verifiche`, con corpus unico in `supporto/test/casi_confronto.json`. Le suite esistenti non equivalgono alla conversione in test individuali.

## Fase 2: chiusura del c.a.

- [x] F2.1 Cattura pre-M4 del legacy attuale; matrice delle API; casi vicini alla soglia; test con sezioni in forma chiusa. Fatti (branch `refactoring/f2-banco-confronto`, [f2.1-banco.md](f2.1-banco.md), [f2-matrice-api.md](f2-matrice-api.md)): cattura pre-M4 doppia in `supporto/artefatti/baseline/F2-pre-m4` (identica a meno degli identificativi casuali; contro B0 solo D7-c e D7-d), comando `compare-dense` con classificazione versionata, suite `baseline/banco-ca` (fixture di Checker del 1/10: 27 698 righe, 110 differenze tutte classificate). Fixture ricatturate il 7/10 (decisione B) dalla nuova cattura doppia `F2-pre-m4-v2` (d2d3225, con D7-b, D7-d d2 e R15; mesh identiche a B0): Checker 06d97733, test di migrazione senza casi speciali, banco con zero differenze e riferimento denso `F2-pre-m4-v2/a/tutte`. Casi vicini alla soglia e sezioni in forma chiusa l'8/10 (branch `refactoring/f2-1-soglie` da 98a21d4, [f2.1-banco.md](f2.1-banco.md), esito in `tests/ConcreteBench.Checks/esito.md`): suite `ConcreteBench.Checks` e `ConcreteBench.Checks/attesi` nello stadio fast; legacy di X.Calculations contro GPCChecker.Concrete S2 su 19 stati SLE e 25 resistenze SLU con attesi Python indipendenti (scarti massimi 1,0e-4 e 1,8e-3, tolleranze 1e-3 e 5e-3) e su 627 punti attorno a soglie di SLE, fessurazione, SLU e taglio (stessi esiti, numeri entro 1e-9). Divergenze registrate da decidere: S-1 (punto resistente della libreria con N fuori dalla tolleranza di 1 kN di `SectionMomentResistance`, il legacy non dà la resistenza in un caso), T-1 (taglio senza staffe con VEd = VRd: esito opposto per 1 ulp, da aggiungere a F2-1).
- [x] F2.2 [CP] Pagine dei metodi c.a. (revisione tecnica). Scritte in Checker (branch locali `anthea-metodi-ca-1` e `anthea-metodi-ca-2`, worktree Temp\cw-metodi1 e cw-metodi2): taglio, torsione, SLE, fessurazione, ancoraggi, dettagli, M-χ, durabilità e copriferri, con esempi a mano uguali alla libreria. Revisionate e unite in Checker develop: merge locali c555a3b8 e 1fbaea61 più le correzioni del verificatore (5675492f e 398eb36a con i merge 9eb800d4 e 26fb6b8b; bf23a16d, 7d86ffc7, bd9938ac, e41a803a). Revisione accettata il 7/10 dal coordinatore su delega dell'utente, da ratificare. Voci nuove R16-R21 nel registro (da decidere in F5.15) e R7 rettificata. Push di Checker develop (punta e41a803a, 35 commit oltre origin/develop) all'utente. **Audit 14/10: tecnicamente fatto; ratificato dall'utente il 14/10 ([decisioni](decisioni.md), punto 4b).**
- [x] F2.3 [CP] GPCChecker.Concrete: dettagli di solette e pareti; mesh sicure in parallelo al posto del lock; tracce per i report; opzioni degli scostamenti. Fatti il 7/10 (Checker develop): D7-b con opzione legacy (0.0.16.0), R3, R15 della fascia interna (0.0.17.0), fixture c.a. ricatturate; allora restavano solette e pareti, mesh senza lock, tracce in italiano per i report, R1 e R2 dopo il riscontro dei testi. **Ratificato dall'utente il 15/10 (E7): API e versioni distribuite dai set S, T e X.**
  - [x] F2.3a — dettagli di solette e pareti: API `MemberDetailingCalculator`/`PlateDetailingData` disponibili nel set S e collegate da `ConcreteDetailingCalculator` (riuso API-01 già in main). Prove `ANTHEA.ApiReuse.Checks`: 266 casi congelati, compresi i quattro tipi di elemento, controllo delle chiamate reali alla DLL; 3268 numeri, solo due scarti di arrotondamento fino a 1,14e-16. Il ripiego circolare API-02 necessario nel set S è stato superato dal punto 1 del blocco F2, distribuito nel set T. Evidenze nel [controllo dei contratti](audit-contratti-moduli-20261012.md), nella corsa `20261012-195301-f3-defaults-standard` e nel [verbale del blocco F2](chiusura-blocco-f2-20261012.md).
  - [x] F2.3b-mesh — sicurezza delle mesh in parallelo e rimozione del lock globale, provata il 12/10: punto 4 e registro F2-23.
  - [x] F2.3b-tracce — completamento delle tracce per i report, R1 e R2 dopo il riscontro dei testi. Chiuso il 15/10: R1 e R2 corrette su fonti secondarie («Correggi su fonti secondarie»), distribuite con la release Y (registro R1 e R2); la traccia NTC del taglio (F2-2) va alla revisione dei report, in coda dopo il refactoring (E2).
- [x] F2.4 [CP] Rilascio `lib/Checker` con codice ANTHEA invariato. S2 compilata dai commit pushati e verificata; profilo full a schermo sull'integrazione 2 (258e81a, corsa `20261007-184928-int2-full`): PASS 69, KNOWN 1 (`verifiche/project-calculations`), NEW-FAIL 0, tutte le prove WPF superate. Branch `refactoring/f2-4-snapshot-s2` (da main 17b6c98): revisioni nuove a sorgente invariato per SourceLink (Model 5ad56681: GPCModel 1.6.1.1, GPCModelData 0.0.2.3; Checker 0d7ba50b: Geotechnics 0.1.1.1, CompositeBridge 1.4.0.4), GPCChecker.Concrete 0.0.17.0 con D7-b e R15, Utilities, Geometry e DelaunayMesh invariate. DLL compilate con la nuova opzione `Update-Snapshot.ps1 -FromUpstream` (worktree temporanei dei rami remoti in una radice fissa, 8d6a8ec) da Utilities df3b3e7, Geometry 6a0d1c1, Model 5ad56681 e Checker 0d7ba50b, tutti in origin (edc4fce); sostituiscono il candidato f6b6fdf, compilato da Checker develop 1fbaea61 locale. Profilo standard 33 PASS, 1 KNOWN, 0 NEW-FAIL; profilo baseline 39 PASS, 1 KNOWN, 0 NEW-FAIL con esiti e conteggi uguali al candidato e a S1, cattura headless uguale a F2-B2, banco c.a. e banco denso senza differenze, 80 mesh identiche; test delle librerie superati (dettagli in `lib/Checker/README.md`). Unita nell'integrazione 2 (`refactoring/integrazione-2`: merge del candidato 0c87141, merge della ricompilazione b818b20): profilo standard `int2-std` 33 PASS, 1 KNOWN, 0 NEW-FAIL e profilo baseline `int2-base` 39 PASS, 1 KNOWN, 0 NEW-FAIL, esiti e conteggi uguali a S2 (prove in `supporto/artefatti/refactoring/integrazione-2` del checkout principale). Nota del 7/10 del branch `refactoring/f2-taglio-torsione`, scritta prima di S2 (superata nella prima parte dalle versioni nuove di S2): GPCChecker.Concrete 0.0.17.0 è pronta ma non distribuita; ricompilando dai commit nuovi cambiano per SourceLink anche gli SHA-256 di GPCModel, GPCModelData, Geotechnics e CompositeBridge a sorgente invariato, e `tools/libs/Update-Snapshot.ps1` rifiuta uno SHA diverso senza versione più alta: servono versioni nuove (o una regola dello script per i componenti a sorgente invariato) e il profilo full a schermo. Dal passo F2.6 taglio e torsione della sezione passano dalla libreria: prima di distribuire una `GPCChecker.Concrete` nuova, rieseguire `tests/ConcreteLibraryAdapter.Checks` e confrontare i testi di `Shear/` e `Torsion/` (stati, rifiuti, espressioni dei dettagli) con la mappa di `X.Calculations/ConcreteLibraryMapping.cs`. Un testo senza traduzione è un errore di programma (`InvalidOperationException`): il calcolo headless e il progetto delle armature lo registrano per riga, la scheda WPF del taglio interrompe le righe successive (`X.Desktop/Wpf/ConcreteShear.cs:125` cattura solo `ArgumentException`) e mostra un solo errore "Taglio". Oggi nessun testo della libreria raggiungibile dal modulo ne è privo (verifica avversaria su ca6530d). Nell'integrazione 2, dopo il merge di F2.5-F2.6 (ea4c51e), la condizione è verificata per S2: `Shear/` e `Torsion/` di GPCChecker.Concrete sono identici fra S1 (Checker b994e188) e S2 (0d7ba50b), quindi stessi testi, e `tests/ConcreteLibraryAdapter.Checks` dà PASS con `misura.json` identico byte per byte a quello con S1.
- [x] F2.5 Strato di mappatura con contratto JSON invariato. Taglio e torsione (branch `refactoring/f2-taglio-torsione`): `X.Calculations/ConcreteLibraryMapping.cs` (conversioni kN ↔ N e kNm ↔ N·mm con nome, nomi delle norme → classi `Standard` per le 7 norme ordinarie, risultati della libreria → DTO del JSON 'taglio' e 'torsione', testi e rifiuti della libreria → testi italiani del legacy; nessuna costante normativa, controllato dalle prove), adattatore `ConcreteShearTorsionAdapter` con l'interruttore `Default` (motore legacy in questo passo), DTO della torsione in `ConcreteTorsionContracts.cs`. `ConcreteShearAnalysis` e `CheckerMigration.Capture` (opzione `--motore legacy|libreria`) passano dall'adattatore. Prove `tests/ConcreteLibraryAdapter.Checks` (stadio fast): interruttore, motore legacy dell'adattatore identico bit per bit al legacy, motore della libreria con stessi esiti, rifiuti e testi e numeri entro 1e-9 sulle griglie della cattura densa e sui calcoli del modulo (scarto relativo massimo 9,2e-16). Runner `-Profile baseline` con `-BaselineRef F2-B2` e `-DenseRef F2-pre-m4-v2`: PASS 41, KNOWN 1, NEW-FAIL 0; cattura headless uguale a B2 (432 file, 21 tempi volatili), banco denso uguale a F2-pre-m4-v2 (32 564 righe, solo identificativi casuali) e alle fixture di Checker. Differenze dichiarate: registro F2-1…F2-4. Commit 2583225. Correzione f5b5f50 (dopo la revisione di 7176c0c): in 2583225 `ConcreteShearAnalysis.Torsion` riceveva il motore ma calcolava ancora profilo resistente e torsione con il legacy diretto, quindi con la libreria il modulo avrebbe cambiato solo il taglio; ora passa da `ConcreteShearTorsionAdapter.TorsionGeometryOf` e `Torsion` con il motore richiesto. Interruttore di nuovo sul legacy (7176c0c ritirato) e tolleranze e confronto `f2-libreria` tolti, da rimettere in un commit dedicato prima dell'interruttore. Prove nuove: 3f (la torsione del modulo coincide bit per bit con l'adattatore chiamato direttamente con lo stesso motore; il controllo distingue i motori, fallisce sul codice di 7176c0c), 4 (attesi indipendenti di `supporto/test/ConcreteCode.Checks/reference.json`, letto in sola lettura, benchmark DIN, DS, UNI, NS e forme chiuse della torsione NTC attraverso l'adattatore con entrambi i motori), letterali con il punto iniziale (`.85`) nel controllo delle costanti. Evidenze in `supporto/artefatti/refactoring/f2-taglio-torsione/correzione`. Completamento della correzione dopo la seconda verifica avversaria (8c95d9b): interruttore di nuovo sul legacy dopo il primo 85314a6 per catturare la baseline headless B3 prima dell'interruttore; prova 3f anche contro il legacy diretto con il motore Legacy; prova 3e con la 'torsione' del calcolo headless uguale bit per bit all'adattatore con il motore predefinito (riga NTC T7 con uscite diverse fra i motori: un calcolo headless che ignorasse l'interruttore fallisce, prova negativa n1). Stato legacy dimostrato sul commit del corpus B3 (dcde952, stesso codice di calcolo): runner `-Profile baseline` PASS 40, KNOWN 1, NEW-FAIL 0; cattura densa identica a F2-pre-m4-v2 con il confronto esatto `pre-m4` (32 564 righe, 1244 con soli identificativi casuali); cattura headless uguale a B2 sui 432 file comuni, salvo le voci dei casi nuovi in `fallbacks.json`. Unito nell'integrazione 2 (`refactoring/integrazione-2`) con il merge ea4c51e del branch `refactoring/f2-taglio-torsione` (punta db4da92); misure con S2 in F2.6.
- [x] F2.6 Taglio e torsione → libreria. Storia dei commit (nessuna riscrittura): 7176c0c, primo interruttore, ritirato: collegava alla libreria il solo taglio del modulo (profilo resistente e torsione restavano sul legacy diretto e le prove 3d/3e confrontavano per la torsione il legacy con se stesso), con le tolleranze nel commit dell'interruttore; correzione di F2.5 f5b5f50; 8f991b4, tolleranze dense dedicate (confronto `f2-libreria`: come `pre-m4`, 1e-9 sui soli numeri di taglio e torsione, regole `f2-libreria/` di `tolerances.json`); 85314a6, secondo interruttore, sospeso da 8c95d9b perché il corpus headless di B2 non aveva sezioni c.a. con taglio e torsione; 256714d e dcde952, quattro casi in `tests/ANTHEA.Testing/corpus` (rettangolare, circolare e circolare cava NTC con torsione, rettangolare DIN con cot θ assegnato e rifiuti; `foro_presente` dichiarato) e baseline B3 catturata con il motore legacy (`supporto/artefatti/baseline/F2-B3`, LEGGIMI); ea6e0ac, tolleranze headless dedicate (1e-9 sui numeri di 'taglio' e 'torsione' dei risultati `str_palo`, il resto esatto); ca6530d, interruttore `ConcreteShearTorsionAdapter.Default` sulla libreria e `f2-libreria` predefinito di `-DenseSet`. Il modulo sezione c.a. (WPF, calcolo headless, progetto delle armature, tutti attraverso `ConcreteShearAnalysis`) calcola taglio, profilo resistente e torsione con `SectionShearCalculator`, `TorsionGeometry.Rectangle`/`Circle` e `SectionTorsionCalculator`; il legacy resta raggiungibile (`ShearTorsionEngine.Legacy`, cattura con `--motore legacy`, prove dell'adattatore) fino alla decisione di F2.11. Equivalenza misurata su ca6530d (registro F2-1, convenzione): `tests/ConcreteLibraryAdapter.Checks` 17 696 controlli (griglie della cattura densa; modulo 263 numeri diversi su 4510, massimo 5,4e-16; torsione del modulo uguale bit per bit all'adattatore del motore richiesto in 120 calcoli, 70 con uscite dei motori diverse; calcolo headless di 5 norme entro 1e-9, 25 numeri diversi su 1159, e torsione headless uguale all'adattatore con la libreria; relazioni identiche; attesi indipendenti 430); runner `-Profile baseline` con `-BaselineRef F2-B3\headless` e `-DenseRef F2-pre-m4-v2\a\tutte` (DenseSet `f2-libreria`): PASS 41, KNOWN 1 (verifiche/project-calculations), NEW-FAIL 0; cattura headless contro B3: 452 file, 5 numeri dei casi nuovi entro 1e-9 su 276 di 'taglio' e 'torsione' (14 righe di taglio, 9 di torsione; scarto relativo massimo 2,0e-16), relazioni, ripieghi e tutto il resto identici salvo 21 tempi volatili; cattura densa: 32 564 righe, 1083 (589 di taglio, 494 di torsione) con 1682 numeri entro 1e-9, nessuna non classificata, stessi esiti, rifiuti e testi; fixture di Checker come sopra; profilo standard PASS 35, KNOWN 1, NEW-FAIL 0. Prove negative: calcolo headless che ignora l'interruttore (fallisce la 3e), torsione del modulo dal legacy diretto (fallisce la 3f). Evidenze in `supporto/artefatti/refactoring/f2-taglio-torsione` del repository principale (`b3/`, `interruttore-b3/`; `interruttore/` è il primo interruttore 85314a6, `f2.6/` il tentativo ritirato 7176c0c). Decisioni del coordinatore su F2-3, F2-4, traccia NTC e B3 in [decisioni.md](decisioni.md), da ratificare; nell'integrazione 2 anche F2-1 (ripieghi di `foro_presente`) ratificata così com'è dal coordinatore, da ratificare. Restano fuori dall'interruttore, nel legacy: il taglio senza staffe dei muri in c.a. (`X.Calculations/RetainingWall.Structures.cs:98-100`, VRd,c con una formula propria senza σcp, fase F4.7) e la proposta di bw, d e Asl dal contorno della sezione (`SectionShearGeometry.Derive`, buco 3 di [f2-matrice-api.md](f2-matrice-api.md)); i pali elastici verificano il taglio con `GPC.Checkers.Concrete.Piles` già da prima di F2. Dopo la verifica avversaria di ca6530d e 801e11c, prove rafforzate in 4e8b30b (stesso contenuto di 63cda73, ritirato da 2b2f16b perché aveva per errore il messaggio di 8c95d9b; l'interruttore resta sulla libreria): il taglio del modulo e del calcolo headless distingue i motori (prova 3d: 131 calcoli del modulo con uscite diverse; prova 3e: 106 righe headless uguali bit per bit al motore predefinito, 42 con uscite diverse), la torsione headless anche su sezioni NTC di 5 forme con cot θ 1, 1,5 e 2,5 (63 righe uguali bit per bit all'adattatore, 36 che distinguono i motori sia per la sola torsione sia per tutto il calcolo), almeno 10 casi per controllo; prima la 3e aveva una sola riga che distingueva i motori (T7) e nessuna prova distingueva il motore del taglio del modulo (con il taglio sempre legacy le prove di 801e11c passano). 17 880 controlli; prove negative (calcolo headless con il motore legacy, taglio del modulo sempre legacy) e runner `-Profile baseline` contro B3 e F2-pre-m4-v2 su 4e8b30b (PASS 41, KNOWN 1, NEW-FAIL 0, stesse misure di ca6530d) e profilo standard sui documenti 660e1f7 (PASS 35, KNOWN 1, NEW-FAIL 0) in `prove-rafforzate/`. Provenienza legacy di B3 confermata anche sul codice di prima di F2.5: cattura headless di 17b6c98 con `Corpus.cs` e `corpus/` di dcde952 uguale a B3 (452 file, solo 21 tempi volatili; `fallbacks.json` identico byte per byte, 2848 voci; `b3/provenienza-17b6c98`). Documenti del branch 801e11c, 660e1f7 e db4da92 (punta); branch unito nell'integrazione 2 (`refactoring/integrazione-2`) con il merge ea4c51e. Le misure sopra erano con S1 (lib/Checker di main 17b6c98, GPCChecker.Concrete 0.0.15.0); nell'integrazione 2 si rimisurano con S2 (GPCChecker.Concrete 0.0.17.0, release dai commit pushati): `tests/ConcreteLibraryAdapter.Checks` PASS, 17 880 controlli, `misura.json` identico byte per byte; profilo standard `int2-f26-std` PASS 35, KNOWN 1 (verifiche/project-calculations), NEW-FAIL 0; profilo baseline `int2-f26-base` con `-BaselineRef F2-B3\headless` e `-DenseRef F2-pre-m4-v2\a\tutte` (DenseSet `f2-libreria`) PASS 41, KNOWN 1, NEW-FAIL 0 con le stesse misure di S1 (headless: 452 file, gli stessi 5 numeri entro 1e-9, 21 tempi volatili; densa: 32 564 righe, 1083 entro 1e-9, nessuna non classificata; fixture di Checker: 27 698 righe, 1083 entro 1e-9) e conteggi di tutte le suite uguali alle corse con S1. Catture con S2 uguali a quelle con S1 (densa salvo 1244 righe con soli identificativi casuali, headless salvo 21 tempi volatili): `Shear/` e `Torsion/` della libreria non cambiano fra S1 e S2, che differiscono per la fessurazione (D7-b, R15) e per le versioni. Prove in `supporto/artefatti/refactoring/integrazione-2` del checkout principale (`ci`, `console`, `f26`). **Ratificate dall'utente il 15/10 (E1): F2-1 con T-1, F2-3, F2-4 e B3; la traccia NTC (F2-2) va alla revisione dei report (E2).**
- [x] F2.7 Fessurazione e SLE, senza stato statico. **Chiusa il 15/10 (E1):** ratificate F2.7-D2…D12, trascritte in [decisioni](decisioni.md) con F2.7-D1; A7 e T1 sostituiti dalle regole `api_arrotondamento` di `1b3c38f1`; la B4 attuale sostituisce il corpus SLE del micro-piano. **Stato dell'audit del 13/10:** `ConcreteServiceabilityAdapter.Default` è `Library`; le suite confrontano entrambi i motori (673640 controlli SLE nell'ultimo standard). L'attivazione della libreria non è da rifare. Restano A12 (array statici pubblici ancora mutabili, fra cui `ConcreteStandards.Names/OrdinaryNames/Coefficients` e `ConcreteMaterials.ConcreteDiagrams`) e la riconciliazione delle evidenze A6–A9/A13 con i criteri del micro-piano storico, comprese le decisioni sui casi limite. A10/A11 e U1/U3/U5/U7 sono concluse. **Cronologia del prototipo, non lista attuale delle mancanze:** fatti (8/10) libreria K0-K4 e ciclo di prototipo (in S3), A2-A4
  sul branch di prototipo con motore predefinito Legacy (refactoring/integrazione-3). A5 il 9/10 su
  `refactoring/integrazione-4` (3dc8c57, b35afa8, c8a0927, bf7f6a6): X.Verifiche, ConcreteCode.Checks,
  ConcreteShort.Checks e l'harness WPF passano dall'adattatore (`--motore-sle legacy|libreria`; nell'harness la variabile
  `ANTHEA_MOTORE_SLE`), test di formula raddoppiati sulla libreria, 5k estesa a `supporto/test` e `tests` con
  `tests/ConcreteLibraryAdapter.Checks/legacy-allowlist.json`, prova negativa n11 rilevata. Profilo standard `int4-a5-std`
  PASS 39, KNOWN 1, NEW-FAIL 0. Prova generale con la libreria: tutto PASS salvo il caso (i) di CrackK2Checks
  (`--checker`): con le tensioni delle barre mancanti su una sezione interamente compressa il legacy dà wk = 0, la
  libreria rifiuta e la mappatura non traduce il rifiuto; caso non raggiungibile dalla produzione, segnalato all'utente,
  allora da decidere prima di A8. A quel checkpoint restavano: quella decisione, le prove WPF con i due motori (con il profilo full), le due
  revisioni del prototipo, A6-A9 e A12-A13 e il profilo full a schermo. A10 e le correzioni U1, U3, U5, U7 sono chiuse il 12/10 nei punti 2–3 seguenti.
  - [x] F2.7-A10 (12/10): omogeneizzazione delle proprietà, della scheda SLE e dei riepiloghi tramite le API GPC. Nessuna formula duplicata; messaggi italiani preservati. Chiamate IL verificate, attesi analitici esistenti e confronto 548 file superati (`20261012-212735-f27-a10-dopo`, 7 PASS). Il rifiuto nella vista di φ negativo segue in U5.
  - [x] F2.7-A11 (12/10 sera): esposizioni della UI e validazione dei muri dal catalogo GPC tramite `ConcreteLibraryMapping.ExposureChoices`, immutabile. Il catalogo legacy resta come riferimento indipendente, anch’esso immutabile. 19 voci e ordine invariati; prove 7c, tre suite WPF e catture prima/dopo superate (548 file, 50.996 righe dense; soli tempi e ID casuali). Registro F2-18.
  - Audit 14/10 — interruttore A8 fuori dal micro-piano: `ConcreteServiceabilityAdapter.Default => Library` è stato attivato nel commit F4 `b7339f4a` del 12/10 (riuso API, ramo `refactoring/f4-riuso-api`, unito in main con `80191514`), non con il commit A8 di F2.7b. Mancavano la baseline B4 (A6, nessuna cartella `F2-B4` negli artefatti), il commit dedicato delle tolleranze A7 (le regole `api_arrotondamento` di `1b3c38f1`, relative 1e-12 su alcuni campi della fessurazione, sono quelle del riuso API-01), le voci del registro F2-5…F2-8 e R22 (A9) e la registrazione in `decisioni.md` delle scelte F2.7-D1…D12 del progetto. Le voci F2-5…F2-8 e R22 sono ora nel registro, ricostruite o segnate «da-ricostruire». B4, A7 e le decisioni F2.7-D restano da trattare con l'utente: da ratificare. Aggiornamento 14/10: F2.7-D1 ratificata ([decisioni](decisioni.md), punto 4b); evidenze B4 e voci F2-7/F2-8 completate su decisione 3b (F2-7 dichiarata con le scelte dell'utente, punto 9). Restano A7 e F2.7-D2…D12. Ratificati dall'utente il 15/10 (E1).
  - [x] F2.7-A12 (14/10, dopo l'audit): i dieci cataloghi statici pubblici del progetto F2.7 (`CrackFields`, `Sets`, `SharedSleFields`, `Criteria`, `ConcreteStandards.Names/OrdinaryNames/Coefficients`, `ConcreteMaterials.ConcreteDiagrams`, `RetainingWall.States`, `ConcreteReinforcementDesign.Targets`) sono `ImmutableArray`; prova 5l estesa a questi membri. Application, Architecture, RetainingWall e adattatore rapido PASS.
- [x] F2.8 Dettagli, ancoraggi, aderenza, M–χ. **Chiusa il 15/10 (E3):** le prove di equivalenza già misurate sostituiscono B1–B3, T1 e I1–I3 (confronto denso del riuso API, chiamate reali alla DLL, 266 casi congelati, catture D3 uguali, full del 15/10); voce F2-9 del registro di rimando ad API-01; F2.8-D1…D12 trascritte e ratificate in [decisioni](decisioni.md). Libreria L0-L3 e cattura A0 risalgono all'8/10. Audit 13/10: `ConcreteAnchorageCalculator`, `ConcreteDetailingCalculator`, `ConcreteBond` e `MomentCurvatureCalculator` chiamano già le API GPC; non vanno duplicati con nuovi adattatori solo per rispettare i nomi del vecchio progetto. U2 (rami interi 2–100) è chiusa nel punto 2 del blocco F2. U5, M–χ nel report di progetto, è chiusa dalla correzione F3.4-U5 del 13/10: il servizio comune calcola ed esporta la curva. Restano inoltre la revisione dei metodi accodata con F2.7-U6, la riconciliazione puntuale delle prove B1–B3/T1/I1–I3 e la chiusura D1: la presenza delle chiamate GPC non basta a dichiarare soddisfatto ogni criterio del micro-piano.
  - Audit 14/10 — sostituzione senza interruttore: il commit F4 `b7339f4a` del 12/10 ha sostituito direttamente, senza interruttore né motore legacy selezionabile in produzione, aderenza/ancoraggi (`ConcreteBond`), dettagli non circolari (`ConcreteDetailing`), M–χ (`MomentCurvature`) e interassi non circolari (`TensionBarSpacing`); restano sul percorso precedente solo i casi circolari, poi corretti con API-02. Questo va contro `decisioni.md` (8/10, F2.11): senza la revisione delle pagine dei metodi (U6 e F2.8-U1, in coda) il legacy di quelle famiglie doveva restare finché l'utente non decide. Da ratificare o da ripristinare come motore di confronto. Ratificato dall'utente il 14/10 ([decisioni](decisioni.md), punto 3a): restano sulla libreria senza interruttore.
  - [x] Attesi indipendenti delle pagine dei metodi che mancano nelle suite di ANTHEA (E3): ancoraggi (AN-01/02, SO-01/02), dettagli (As,min, As,max, Ast/s) e M–χ. Fatto con `ae8f429f` (ramo `refactoring/f2-chiusura`, in main): suite `ConcreteLibraryAdapter.Checks/attesi` nello stadio fast.
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
  **Audit 14/10: tecnicamente fatto; ratificato dall'utente il 14/10** per le decisioni F2.9-D1…D14 del coordinatore ([decisioni](decisioni.md), punto 4b); proposte F2.9-U1…U6 decise al punto 4c, come sotto.
  Proposte decise dall'utente il 14/10 (punto 4c): U1 resta la UNI 11104:2016, U2 etichetta della libreria, U3 testi dei pali
  tradotti, U4 valori iniziali espliciti, U5 testi della durabilità con i soli rimandi normativi (F4-47); U6 in coda.
- [x] F2.10 [CP] Registro delle differenze e accettazione dell'equivalenza. Accettata dall'utente il 15/10 (E4): equivalenza di F2 sul full conforme `20261015-105444-f4-full-190` (140 PASS), sulle catture prima e dopo D3 e sulla corsa baseline contro B4 del 15/10; rinvio di R16–R21 a F5.15 ratificato. Registro allineato: D7-e e D7-f attuate; ratifiche di F2-1…F2-4, F2-13, F2-14 e R16–R21; voce F2-9; rettifica di F2-17 (SLU-2); 41 riferimenti di codice portati ai percorsi attuali dopo D3, F3 e F4.
- [x] F2.11 [CP] Eliminazione dei duplicati, dello stato statico e del lock. Chiusa il 16/10 con F2.11c.
  - [x] F2.11a — sei file geotecnici esclusi: rimossi su autorizzazione esplicita con `83081a67`, già in main/set S. BishopSolver, FoundationSettlement, NewmarkSliding, ShallowFoundationSeismic, SlopeGeometry e SlopeStability non sono più nel progetto. Nessuna nuova eliminazione con questa riconciliazione del piano; `SlopeStability.Models.cs` e `WallRecords.cs` restano contratti usati.
  - [x] F2.11b — sonda di durabilità (F2.9-D14) unificata con quella comune degli adattatori. Conservata la facciata tipizzata; contesti annidati ripristinati, calcoli paralleli isolati, eliminato il secondo AsyncLocal. 15 prove mirate (10 fallivano prima), regressione completa e catture: 9 PASS, 0 KNOWN, 0 NEW-FAIL; 548 file e 50.996 righe senza cambi di risultato. Registro F2-19.
  - [x] F2.11c — altri duplicati, stato statico e lock. Con D3 non si elimina nulla: i motori legacy sono già in `tests/ANTHEA.Legacy`. Piano deciso dall'utente il 15/10 (E5), attuato il 16/10 sul ramo `refactoring/f2-11c` (da main `8b9ce6d3`), registro F2-26:
    - [x] membri usati solo dalle prove in `tests/ANTHEA.Legacy`, nessun chiamante di produzione (`7976f077`): `TensionBarSpacing` con `ITensionBarSpacing`; `SectionRegions.Clip/Area/Region` in `LegacySectionRegions` (`BarCover` resta nel prodotto); `RetainingWall.SoilMass` e `ValleyBand`, record di `RetainingWallFunctions`; `ConcreteReinforcementDesign.SearchIndices` in `LegacyConcreteReinforcementDesign.NativeSearchIndices`; `Durability.ValidateExposure` in `LegacyDurability`; le due interfacce senza utilizzatori `IConcreteAnchorageCalculator` e `IMomentCurvatureCalculator`. Nomi aggiunti ai marcatori di `tools/qa/Assert-NoTestCode.ps1`; nessuna voce delle baseline di architettura cambiata;
    - [x] c.a.: i sette cataloghi statici pubblici ancora modificabili sono `ImmutableArray`, come A12 (`87202dd4`): `ConcreteCalculationSettings.CommonCoefficients`, `RebarMaterial.Keys`, `ConcreteSheetPreparation.Contours`, `SectionActionInputs.Components`, `SectionActionsExcel.Headers` e `UnifiedHeaders`, `ReportConcrete.Sections`; prova 5l estesa. `JsonFallbackHook` interno (visibile solo a `ANTHEA.Testing` e alle sue prove) e per contesto con un AsyncLocal (`5a66ff99`): 10 prove mirate in `ANTHEA.Testing.Tests`, con prova negativa sull'aggancio globale di prima;
    - il resto dello stato statico (ponte, muri, pali, fondazioni, Wiki) in F5.13;
    - lock: punto chiuso. Nessun lock globale sul calcolo; restano i lock di istanza, quelli dei font PDF e quello del registro degli errori.
    - verifica: runner `-Profile baseline` prima `20261016-032148-f211c-prima` su `8b9ce6d3` (89 PASS, 0 KNOWN, 0 NEW-FAIL) e dopo `20261016-034639-f211c-dopo` su `5a66ff99` con `-BaselineRef` sulla cattura di prima (90 PASS: le stesse 89 più `baseline/confronto`; 0 KNOWN, 0 NEW-FAIL). Cattura headless: 548 file confrontati senza differenze non ammesse, solo i 21 tempi volatili delle armature del palo elastico; byte per byte 550 file su 710 identici, `fallbacks.json` (3218 voci, 143 chiavi) e testi dei report identici, le altre differenze sono tempi, data e ora, commit e impronte delle DLL ANTHEA e identificativi casuali. Cattura densa del banco: 27 file, 51 045 righe uguali salvo intestazioni (commit, impronta) e identificativi casuali. Evidenze in `supporto/artefatti/ci` del worktree del ramo (non versionate).
- [x] F2.12 [CP] Rimozione del solutore legacy a fibre conservando l'oracolo Python. Chiusa il 15/10 (E6) con D3 (F4-49): il solutore è in `tests/ANTHEA.Legacy/Section`, fuori dal prodotto, e i 90 casi di `X.Verifiche` girano ancora contro di esso; l'oracolo Python `tools/attesi/sezione_ca.py` (464 casi) è conservato. Il confronto dei 90 casi fra libreria e Python si fa dopo il refactoring.
  - [x] `sezione_ca.py --valida` e `--confronta` nello stadio fast del runner (E6). Fatto con `8be5f0a4` (ramo `refactoring/f2-chiusura`, in main): suite `attesi/sezione_ca-valida` e `attesi/sezione_ca-confronta`.
- [x] F2.13 Registri di migrazione, chiusura. Fatti il 15/10 (E7):
  - voce «M4 (c.a.)» in `Checker/docs/migrazione-anthea/MIGRAZIONE_ANTHEA.txt` (Checker `14673564`) e riga in `Model/CHECKER_PASSO_4.txt` (Model `2ebc01e2`), sui rami di integrazione delle correzioni (Checker `anthea-bug-1015`, Model `bugint-model`), che si uniscono con la prossima release;
  - nessuna `SCHEDA_SCOSTAMENTI_FESSURAZIONE.txt`: la sostituisce il registro delle differenze;
  - AGENTS.md allineata a D3: il motore legacy non si elimina, si sposta in `tests/ANTHEA.Legacy`.
- [x] Revisione di architettura R2 (11/10, unita in main il 12/10): punto resistente SLU con un
  solo proprietario. `ConcreteResistanceAdapter` per dominio 3D, resistenza a N assegnato, progetto delle armature e calcolo
  headless, con la porta `ISectionResistanceSolver` di GPCChecker.Concrete (il calcolo di ModelChecker); in Checker i punti
  grezzi pubblici sono `[Obsolete]` (5f87a390). Decisione dell'utente dell'11/10 (F2-17, corretto): solo il motore della
  libreria, senza interruttore né `--motore-slu`. Cattura del corpus uguale a quella di prima su 546 file; prove 12a-12g in
  `tests/ConcreteLibraryAdapter.Checks` contro il punto grezzo chiamato direttamente. lib/Checker: release R delle librerie
  della revisione (Model 5.0.0.0), dal primo set di rilascio da master. ANTHEA 1.2.0.

Finestra di layout [CP] tra F2 e F3: spostamento in `src/ tests/ tools/ build/`; prima, destino di `GPCChecker.Test.BridgeAudit` (referenzia `ANTHEA/X.Core`) e test di libreria nei repository delle librerie. Audit 13/10: completata per i nuovi progetti e per le estrazioni F3, non per l'intero albero preesistente (`X.Desktop`, `X.Calculations`, test e script storici). `X.Core` conserva la compatibilità richiesta da BridgeAudit come dichiarato nei confini F3; il checkpoint di layout complessivo resta aperto.

## Passaggio fra F2 e F3: stage dei modelli di calcolo

Aggiunto dall’utente il 12 ottobre, dopo i punti 1–5 e 5.2 e prima dell’avvio di F3.
Riusare entità, convertitori e contratti già presenti in GPC Model; nuove formule nelle librerie.

- [x] STG-1 (12/10, Model `6ede622c`): inventario delle API e dei dati stage già disponibili; studio della documentazione MIDAS e campione dei risultati. Distinguere incremento/cumulato, età e attivazione di elementi, vincoli e carichi. Studio e fixture sintetica completati; la sessione reale risponde con STAG/STCT vuoti, quindi il confronto con un modello reale resta esplicitamente in STG-4.
- [x] STG-2 (12/10, Model `cffd4879`, `b89ce317`, `3c357622`): modello comune degli stage e provenienza dei risultati, senza duplicare casi/combinazioni; lettura MIDAS con errori espliciti e test di unità/assi/associazione stage. Riutilizzati ResultState e convertitore comune; conservati i metadati delle attivazioni senza applicarli come vincoli permanenti. API catalogo riusabile senza geometria. Archivio legacy e impronte invariati; 1224 prove Model e 17 prove finali catalogo superate; set candidato in collaudo prima della distribuzione in ANTHEA.
- [x] STG-3 (13/10): importazione esplicita fase/passo/caso, filtro della vista, archivio formato 2 e capacità stage Strand7 dichiarate. Mapper statici riusati senza variazioni; 461 controlli dei dati e 21 WPF. Rilascio U, ANTHEA 1.4.0 e guide Rev38. Full: 102 PASS e un rilievo nei metadati Wiki, corretto e ricontrollato prima del commit. Rapporto in chiusura-stage-20261012.md.
- STG-4 è passato il 15/10 nella traccia del modulo Modelli (decisione E8): vedi «Bozza del modulo Modelli».

## Passaggio fra stage e F3: visualizzazione delle sezioni

Aggiunto dall’utente il 12 ottobre dopo il blocco stage. L’immagine fornita è un
riferimento funzionale: rappresentare i nostri dati e le capacità effettive degli
importatori, senza copiare schermate o introdurre nuovi calcoli nella UI.

- [x] SEZ-1 (13/10): inventario in sezioni-inventario.md; parser e geometrie condivisi in Model 7fd1fe4c, 1234 test superati. Campione reale con due sezioni quadrate; composta I/soletta collaudata come geometria sintetica separata. Offset non risolti distinti da zero. Candidato librerie in verifica prima della distribuzione.
- [x] SEZ-2 (13/10): finestra separata MVVM dall’albero o da un’asta; anteprima quotata, parti/materiali, proprietà con origine, coordinate G/O, assi locali/principali disattivabili, attributi originali. Unificati parser sorgente e proiezione degli attributi. Set V installato; 477 controlli dati e 11 WPF superati, tre temi e finestra compatta controllati graficamente (20261013-003436-sez-detail-reviewed). Full e guide previsti in SEZ-4 prima del merge.
- [x] SEZ-3: geometria, offset e associazioni. La variazione della sezione per fase (SEZ-3b) è passata il 15/10 nella traccia del modulo Modelli (decisione E8).
  - [x] SEZ-3a (13/10): contorni condivisi fra 2D e 3D, fori e rientranze conservati, punto di inserimento applicato nelle terne locali, contouring lungo i volumi. Selezione/isola degli elementi associati; componenti e rapporti importati disponibili. Offset non risolto o di estremità non normalizzato: asta ad asse con avviso. Controllati vertici effettivi, aree indipendenti, orientamento dei triangoli; 477 controlli dati, 18 WPF sezioni e 33 WPF generali (20261013-003816-sez-volumes-validation).
  - SEZ-3b: spostato nella traccia del modulo Modelli (vedi «Bozza del modulo Modelli»).
- [x] SEZ-4 (13/10): full 20261013-004459-sez-set-v-full, 104 PASS e zero fallimenti; 478 controlli dati, 18 WPF sezioni e confronti numerici precedenti invariati. Guide globali Rev39 e ANTHEA 1.5.0; chiusura in chiusura-sezioni-20261013.md. SEZ-3b e il confronto reale STG-4 restano aperti. Si avvia F3 sul set V congelato.

## Fase 3: strato applicativo

- [x] F3.1 [CP] (13/10): decisioni su delega in f3-strato-applicativo.md; 36 fixture per i 12 moduli effettivi. Doppia cattura WPF D/E: 458 file equivalenti, unico volatile tempi_ms già censito. Dati migrati, risultati, report singoli/brevi/progetto e Wiki congelati in tests/fixtures/f3; nessun codice di produzione spostato. Le decisioni sull’accorpamento e i servizi saranno attuate nei passi successivi. **Audit 14/10: tecnicamente fatto; ratificato dall'utente il 14/10 ([decisioni](decisioni.md), punto 4b)** (marcatore [CP] del piano del 6/10 rimesso).
- [x] F3.2 (13/10): Application, Reports e Wiki net8.0 in src, ingresso Headless in tools e unica suite Architecture.Tests. Verificati target, assenza WPF/COM, direzione dei riferimenti, soglie TypeRef/async void/timer e set V congelato. Runner f3-scaffold: 7 PASS, equivalenza completa; f3-architecture-final: 5 PASS. Release priva di codice di prova. I comandi operativi Headless arrivano con i servizi, non sono dichiarati disponibili nello scheletro.
- [x] F3.3 Documento, progetti, revisioni, condivisione.
  - [x] F3.3a (12/10): confronto dei default progetto/foglio dei muri corretto. Si normalizzano solo gli ID delle azioni in copie di test, validando formato e unicità; gli ID dei documenti restano indipendenti. Registro TEST-01, prove di mutazione in `tests/ProjectCalculationChecks`. Eliminata l’ultima eccezione in `build/known-failures.json`. Suite progetto: 152 controlli; profilo standard `20261012-195301-f3-defaults-standard`: 48 PASS, 0 KNOWN, 0 NEW-FAIL.
  - [x] F3.3b (13/10): 17 sorgenti e template trasferiti in Application; DocumentService e scrittura atomica condivisa, Archivio come facciata compatibile. Headless validate/inspect operativi. 121 controlli documenti/revisioni, suite progetto e confronto di 458 file invariati: corsa 20261013-013204-f3-documents-built, 12 PASS. Nessuna migrazione anticipata in apertura.
- [x] F3.4 [CP] (13/10): estrazione dei generatori conclusa e richiesta aggiuntiva U5 corretta e collaudata dopo l’audit. Generatori in Reports, un solo DocxWriter, capitoli in memoria e una sola serializzazione del report progetto. Core eliminato dal prodotto; resta il solo inoltro dei tipi per il test esterno BridgeAudit e consumatori precedenti, senza implementazioni duplicate. 458 file equivalenti; 30 DOCX, 134 parti XML e 97 immagini identici salvo normalizzazioni volatili già esistenti. Corsa f3-reports: 13 PASS; f3-report-parts-final: 5 PASS. **Audit 14/10: tecnicamente fatto; ratificato dall'utente il 14/10 ([decisioni](decisioni.md), punto 4b)** (marcatore [CP] del piano del 6/10 rimesso).
  - [x] F3.4-U5 (13/10) — M–χ nel report di progetto CA. Corretto il risultato nullo rilevato nell’audit: il servizio Application riusa StructuralQueries.ConcreteCurvature, il renderer riceve il contratto posseduto e vista/report condividono la configurazione del grafico. Tabella e figura presenti, motivi dei casi non applicabili espliciti, annullamento senza sostituzione del documento precedente. Nessuna formula o DLL nuova. Collaudo mirato finale `20261013-f3-u5-closure`: 15 PASS, 1274 controlli Application (45 specifici), 239 di architettura, 94 interfaccia, 37 capitoli con 89 immagini confrontati con la vista. 46 DOCX prima/dopo invariati fuori da M–χ; risultati numerici preesistenti identici salvo gli ID generati delle righe. Guide Rev42 Word/PDF, registro F3-11; [integrazione al verbale](chiusura-f3-20261013.md#integrazione-di-chiusura-f34-u5). F3 chiusa; arresto richiesto dall’utente, F4 non avviata.
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
- [x] F3.6 Moduli uno alla volta: acciaio e palificata; palo verticale; calcestruzzo; orizzontale ed elastico; muri; ponte [CP ricalcolo ritardato]; sezione c.a. [CP report short senza Word]. **Audit 14/10: tecnicamente fatto; ratificato dall'utente il 14/10 ([decisioni](decisioni.md), punto 4b)** per il ricalcolo ritardato del ponte (Bridge Design a 450 ms, F3.6g, scelto su delega). Il «report short senza Word», allora non fatto (`X.Desktop/Wpf/ShortReportExport.cs` usava Word), è fatto il 14/10 su decisione dell'utente (punto 4c-1, F4-46): DOCX e PDF senza Word, `ShortReportExport.cs` eliminato.
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
- [x] F3.7 [CP] Chiusura: X.Desktop dipende solo dallo strato applicativo. **Audit 14/10: tecnicamente fatto; ratificato dall'utente il 14/10 ([decisioni](decisioni.md), punto 4b).**
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
  - [x] F3.7b (13/10): chiusura dei contratti e degli accessi della UI, Release senza riferimenti diretti a Calculations/GPC, collaudo full con riprese mirate integralmente risolto (122 esiti coperti, zero fallimenti residui). ANTHEA 1.6.0, guide Rev40, installer verificato; [verbale e limiti](chiusura-f3-20261013.md). Il successivo audit ha riaperto F3.4-U5, ora corretta con collaudo mirato e nuova chiusura nel verbale. Gli esiti del full restano quelli storici; il fix aggiunge prove specifiche, senza dichiarare una nuova corsa full. F4 non iniziata.

## Coda dopo F3: input delle sollecitazioni da Excel

- [x] EXCEL-SOL (richiesta del 13/10, requisiti precisati dall'utente): riscrittura degli input delle sollecitazioni tramite Excel, dopo la chiusura dei punti F3 già concordati. Colonne nell'ordine: `Elemento` (facoltativo), `Nome combo`, `Stato limite`, `N`, `Vx`, `Vy`, `T`, `Mx`, `My`. Un solo insieme di sei componenti per riga/combinazione alimenta tutte le verifiche pertinenti allo stato limite. SLU e SLV alimentano anche taglio e torsione; Rara, Frequente e Quasi permanente alimentano i rispettivi controlli SLE. Eliminare la famiglia di input separata `Taglio`, conservando l'accesso alle verifiche di taglio/torsione. Requisiti e cautele di compatibilità in [decisioni.md](decisioni.md#13-ottobre--input-unificato-delle-sollecitazioni-da-excel). I requisiti principali sono già forniti: a fine F3 discutere soltanto le eventuali ambiguità residue, senza chiedere nuovamente il formato. Implementazione sul ramo `codex/section-actions-excel`; criterio Plastico/Elastico selezionabile nella verifica approvato il 13/10. Sottopassi: contratto e instradamento, Excel/editor, collaudo e guide.
- *Storico (13/10, prima della ripresa):* F4 resta ferma per indicazione esplicita dell'utente; la chiusura di F3 e questa attività accodata non ne autorizzano l'avvio. Superato dalla ripresa di F4 del 13/10, dopo il merge della 1.7.1: vedi «Fase 4».

## Fase 4: calcoli residui nelle librerie

**Ripresa autorizzata il 13/10 dopo il merge della 1.7.1.** Piano aggiornato:
[F4 — calcoli residui](f4-calcoli-residui.md), ramo
`refactoring/f4-calcoli-residui`. Ordine operativo: F4.1–F4.9, F4.10,
F4.12, F4.11, F4.13–F4.16. Si completa insieme il progetto delle armature
prima di Bridge Design; gli identificativi storici non cambiano. Le
precedenti annotazioni «F4 ferma» sono storiche e superate da questa richiesta.
F5 rimane esclusa. I residui di altre fasi restano separati e tracciati.

- [x] F4-P0 (13/10): pianificazione e inventario dopo F3, 13 progetti e
  507 voci Compile, 606 sorgenti di libreria. Strumenti di audit e test di
  architettura esistenti riusati; nessuna modifica ai motori o alle DLL.
  Questo passo non chiude F4.1: mancano matrice semantica e catture aggiornate.

- [x] 12 ottobre: [audit dei contratti dei 12 moduli](audit-contratti-moduli-20261012.md), delle classi di terreno/materiale e dei Progetti; inventario dei sorgenti effettivamente compilati e confronto con le API delle DLL distribuite.
- [x] 12 ottobre: riuso verificato delle API per aderenza/ancoraggi, dettagli non circolari, M–χ, regioni, interassi non circolari, acciaio e SLE; sei vecchi file geotecnici esclusi rimossi su consenso esplicito. Ramo `refactoring/f4-riuso-api`, registro API-01. Runner baseline: 52 PASS, 1 KNOWN, 0 NEW-FAIL; confronto completo di 548 file e denso di 50.996 righe passato; soluzione e app Release compilate. Residui e contratti incompleti nell'audit; non chiude F4.1–F4.16 né il rilascio delle fondazioni.
- [x] API-02: interassi circolari.
  - [x] API-02a (12/10 sera): correzione geometrica in Checker `e1a9c78e`, branch `codex/f2-api02-interassi`. 38 nuove prove indipendenti (7 fallivano prima), suite complete 1544 PASS, 17 Steel già noti, zero nuovi fallimenti. Atteso del caso C400R2: 115.19173586761349 mm; contratti e fixture preservati.
  - [x] API-02b: Concrete 0.0.27.2 distribuita nel set T da master pushati, installata con manifest e confronto completo. Attivazione in ANTHEA 1.3.0 verificata: 548 file headless, 50996 righe dense, prove WPF e librerie. Chiusura nel [collaudo F2](chiusura-blocco-f2-20261012.md).

- [x] F4.1 Catture aggiornate dopo F3 per i cinque ambiti autorizzati il 13/10. Bridge Design, allora sospesa, è stata riattivata il 13/10 sera e catturata in F4.11a.0.
  - Riepilogo della finestra del 13/10: [passi, commit, collaudi e ripresa](f4-sessione-20261013.md). Quick 47 PASS; Checker 1625 PASS e 17 Steel già censiti. F4 aperta, nessuna attivazione anticipata delle DLL candidate.
  - [x] F4.1a (13/10): [matrice API e prove](f4-matrice-api.md), riscontrata sui sorgenti dei commit distribuiti nel set V. Individuati i riusi e le lacune di cattura; nessun nuovo motore.
  - [x] F4.1b (13/10): [115 casi e 14 rifiuti attesi](f4-catture.md), 234 file identici byte per byte nelle due esecuzioni (141,7/142,1 s), progetto CA identico con uno/quattro worker. Runner 1 PASS, zero nuovi fallimenti; fonte immutabile e SHA versionati, nessun calcolo modificato.
- [x] F4.2 (13/10): [controlli sintattici e baseline puntuale](f4-architettura.md), sette regole su 507 sorgenti, 4610 occorrenze censite; modifiche/duplicazioni falliscono, rimozioni richiedono riduzione della baseline. 26 prove del rilevatore; runner 5 PASS, nessun nuovo fallimento.
- [x] F4.3 [CP] Decisioni su collocazione di GPC.Design, Concrete/Walls, muratura, σcp, CRd,c/k1/vmin, esiti dei pali; implementazione nei passi successivi. **Audit 14/10: tecnicamente fatto; ratificato dall'utente il 14/10 ([decisioni](decisioni.md), punto 4b)** (le scelte erano dell'agente; σcp di F4.3b confermata dall'utente il 14/10, F4-03).
  - [x] F4.3a (13/10): [dipendenze, collocazioni e contratti](f4-contratti.md), unità/segni, input immutabili, esiti comuni, annullamento e compatibilità. Nessun assembly vuoto o calcolo duplicato.
  - [x] F4.3b (13/10): [studio indipendente σcp](f4-taglio-muri.md), fonte NTC primaria e decisione di riuso della API GPC nel port; 12512 controlli su 26 muri, 4192 capacità diverse fino a +2,80197%, nessun cambio di esito. Cinque attesi Python e suite taglio Checker `9ff8b494`: 12 PASS. Registro F4-03; formula runtime ancora invariata, attivazione F4.7.
- [x] F4.4 Esito comune tipizzato in pali e muri.
  - [x] F4.4a (13/10): Checker `c6f10056`, API additive Outcome/Execution/Data con enum Model; selezione e sviluppo del palo senza confronti di testo. 1565 PASS, 17 Steel già noti, zero nuovi fallimenti; 18 casi legacy identici byte per byte. Concrete candidata 0.1.0.0, Geotechnics candidata 0.2.0.0.
  - [x] F4.4b (14/10): consumatori pali/muri su esiti tipizzati, senza giudizi ricavati dalle etichette. 20 pali/28 tratti/3698 verifiche/22188 numeri equivalenti; 1358 controlli Application e prove WPF PASS. 13 occorrenze rimosse dalla baseline, nessuna aggiunta. Runner mirato 10 PASS. Risultati vecchi non tipizzati richiedono ricalcolo; distribuzione nel set F4.9 ancora aperta. Registro F4-15.
- [x] F4.5 Coefficienti dalle norme: completati i quattro valori di riserva dei pali attraverso `PileResistanceFactors.FromStandard`, con tecnologia e numero di verticali. γb e migrazione D7-d già attivi restano invariati. Profili taglio/torsione tipizzati e override riusano le API correnti; la formula locale dei muri resta tracciata separatamente in F4.7/F4-03. 76 nuovi controlli, 12 PASS del runner e 548 file prima/dopo invariati salvo tempi; [evidenze e confini](f4-coefficienti.md), registro F4-04.
- [x] F4.6 Inventario delle soglie e confini delle unità del palo elastico (attivazione delle modifiche di libreria nel set coerente F4.9).
  - [x] F4.6a (13/10): [inventario e 56 prove dimensionali](../../../Checker/docs/refactoring/f4-unita-palo.md), Checker `7c3ce910`; 173/173 prove geotecniche. Il solutore conserva il contratto a unità coerenti, senza duplicazioni. Campioni adattivi ed estremi non unici distinti dai valori fisici.
  - [x] F4.6b (13/10): conversioni nominate e fattori da GPC.Model.Units; Checker `0cb1c206`, Concrete candidata 0.2.0.0. 735 prove Concrete, cattura dei pali identica; ANTHEA 48 controlli aggiunti, 548 file generali e 234 file F4 equivalenti. Sei residui architetturali rimossi. [Contratti ed evidenze](f4-unita-adattatori.md), registro F4-05.
  - [x] F4.6c (13/10): Checker `164e4836`, eliminato l'elemento spurio alla punta entro la tolleranza già esistente, senza alterare interfacce interne/input; registro F4-02. Quattro regressioni, 1625 PASS nelle suite complete e soli 17 Steel già censiti; catture legacy identiche. Geotechnics candidata 0.2.0.1, attivazione ANTHEA con il successivo set coerente.
- [x] F4.7 Muri in c.a. in `Concrete/Walls` (tre passi).
  - [x] F4.7a: Checker `1033ed05`, sezioni immutabili ed esiti tipizzati; primi controlli longitudinali con formule condivise. 576 sezioni catturate prima del port e 23 test equivalenti, inclusi dettagli preesistenti. Concrete candidata 0.3.0.0; [contratti ed evidenze](../../../Checker/docs/refactoring/f4-muri-ca.md), registro F4-06.
  - [x] F4.7b: N–M, taglio, SLE e curvature mediante servizi GPC esistenti; API candidata verificata, attivazione separata F4.7c/F4.9.
    - [x] F4.7b.1: sessione nativa N–M/SLE equivalente, 62568 confronti su 26 muri/576 sezioni/286 combinazioni, 33 test di libreria e 6 PASS del runner; registro F4-07. Candidata Concrete 0.4.0.0, runtime invariato.
    - [x] F4.7b.2: taglio GPC con σcp, 12512 confronti con gli attesi Python invariati e 22 test mirati PASS. 4192 capacità corrette, nessun cambio di giudizio nel corpus; F4-03. Concrete candidata 0.4.0.1, attivazione ancora F4.9.
    - [x] F4.7b.3: Checker `2a9e2d84`, curvatura totale con viscosità corretta nella candidata Concrete 0.4.0.2; sei regressioni indipendenti (quattro fallivano prima), 28 test mirati e 6 PASS del runner. 1572 curvature e 78 forme confrontate, variazione flessionale massima 3.541154 mm; 94114 confronti complessivi, zero differenze non ammesse. Suite complete Checker: 1651 PASS, 17 Steel già censiti, nessun nuovo fallimento e API compatibili. F4-08; runtime ancora invariato. [Verbale aggiornato](f4-sessione-20261013.md).
  - [x] F4.7c: adattatore, confronto del percorso completo e integrazione con set coerente F4.9.
    - [x] F4.7c.0: Checker `8f823b54`, stima delle armature principali in Design e densità condivisa da Model. 22 masse congelate e prova manuale equivalenti; 15 test Design PASS, API compatibili. Design candidata 0.4.0.0.
    - [x] F4.7c.1: adattatore completo c.a. nella build candidata. 26 muri, 285743 numeri, 12512 tagli indipendenti, 1572 curvature, tre forme di spostamento e annullamento: zero differenze non classificate; 6 PASS del runner. Conservati selettori diagnostici e mesh preparate. Attivazione distribuita ancora F4.9; registro F4-13.
- [x] F4.8 Muri a gravità e dettagli.
  - [x] F4.8a: Checker `b5e40cca`, servizi distinti per resistenze assegnate, CLS non armato e muratura; coordinamento in GPC.Design senza JSON/WPF. 8672 verifiche e 240 curvature equivalenti su sei muri congelati, quattro prove native e suite complete 1655 PASS / 17 Steel già noti / zero nuovi fallimenti. 5 PASS ANTHEA; registro F4-09. Concrete candidata 0.5.0.0, prime Design/Masonry 0.1.0.0; nessuna attivazione runtime.
  - [x] F4.8a.1: Checker `34e8f823`, domanda assoluta del ramo assegnato anche in trazione assiale; cinque test PASS, corpus invariato. Design candidata 0.1.0.1.
  - [x] F4.8a.2: Checker `c31b9201`, resistenze assegnate senza modulo elastico, assenza di rigidezza esplicita; 16 prove Design PASS. Design 0.4.0.1.
  - [x] F4.8a.3: Checker `0b2285bf`, limite negativo di assenza trazione non valutabile; regressione falliva prima e passa dopo, 17 prove Design PASS e API compatibili. Design 0.4.0.2. Attivazione nel prossimo set candidato.
  - [x] F4.8a.4: adattatore completo a gravità su set coerente candidato B. Sei muri congelati, 82568 numeri e 240 curvature equivalenti; resistenze assegnate senza E, parametro di instabilità inapplicabile e materiale incompleto controllati. 6 PASS del runner; suite complete del set: 1668 Checker PASS e soli 17 Steel noti, API compatibili. Distribuzione ancora F4.9; registro F4-14.
  - [x] F4.8b: dettagli delle armature, ancoraggi, sovrapposizioni e copriferri condivisi; confronto e collegamento con distinta F4.10.
    - [x] F4.8b.1: cattura diagnostica esplicita di 56 muri/distinte dal percorso legacy invariato, con controllo SHA degli input congelati; 2 PASS del runner. Nessuna sovrascrittura degli attesi. Il confronto nativo e il collegamento restano separati.
    - [x] F4.8b.2: Checker `bd3b2483`, dettagli tipizzati in Design e regole elementari condivise in Concrete. 374 barre, 2316 punti, 3260 controlli e 56 pesi equivalenti; suite complete 1660 PASS, soli 17 Steel noti. Concrete 0.6.0.0 / Design 0.2.0.0; integrazione ancora F4.9, registro F4-10.
    - [x] F4.8b.3 / F4.10a.1: adattatore dettagli/distinta collegato nella build candidata, legacy conservato. 56 modelli, 30 distinte/CSV e 26127 numeri equivalenti; tre rifiuti espliciti, 5 PASS del runner con architettura. [Provenienza e limiti](f4-adattatori-muri.md); distribuzione ancora da completare in F4.9. *(Audit 14/10: la lettura obbligatoria di `lower_height` nell'adattatore nativo dei muri, commit `ac058a5b`/`5afb47ab`, ha causato la regressione F4-37 sui muri precedenti senza suddivisione, emersa solo nel full e corretta in `2850c031`.)*
- [x] F4.9 [CP] Collegamento dei muri e rilascio `lib/Checker`. Anticipato il 7/10 lo scostamento D7-c (γRd non applicato a F̄).
  - [x] F4.9a.1: corretto GpcLibDir passato da riga di comando senza separatore finale: MSBuild può normalizzare la proprietà locale. Prima due build fallivano cercando snapshotmanifest.props; dopo build e banco dettagli PASS con lo stesso argomento. Nessuna disattivazione dei controlli SHA.
  - [x] F4.9 (14/10) — set W definitivo: Model d5e328db e Checker 949b4527 integrati e pushati su master; Release-Set completo: 2953 PASS, soli 17 Steel gia censiti e due prove native Model ignorate. Dodici DLL installate, Design 0.19.0.0, Concrete 0.8.0.0, Composite 1.6.0.0, Geotechnics 0.3.0.0, Model 5.4.0.1. DelaunayMesh identica. Corretto Update-Snapshot per conservare il manifest integrale del treno insieme alle DLL, aggiunto controllo di provenienza senza toccare gli attesi F3. release-w-provenance: 5 PASS. Registro F4-35; collaudo full distribuito ancora da completare.
  - [x] Audit 14/10 — collaudo e autorizzazioni del set W: set da Model `d5e328db` e Checker `949b4527` su origin/master, 12 DLL installate in `lib/Checker` con il commit ANTHEA `3f9df6b8`; su questo set sono costruiti ANTHEA 1.8.0 (`0eaebd3e`) e l'installer 1.8.0. Il full successivo non è conforme: il primo full su 1.8.0/set W ha 114 PASS e 5 NEW-FAIL (quattro corretti dopo: F4-36, F4-37, F4-38 e confronto dei capitoli; resta il confronto UI con la cattura F3), il secondo è interrotto e non ha esito. Merge e push dei master di Model e Checker del 14/10 alle 04:04 senza un ok dell'utente registrato: ratificati dall'utente il 14/10 ([decisioni](decisioni.md), punto 4a). Con il set W le API candidate di F4.4–F4.15 sono distribuite; la chiusura dei punti resta subordinata al full conforme di F4.16. 15/10: distribuito il set X (Model `2650cf73`, Checker `f094dda5`); full conforme `20261015-105444-f4-full-190`, 140 PASS.
- [x] F4.10 GPC.Design: armature e distinta dei muri, tratti dei pali.
  - [x] F4.10c.1 (14/10): Checker `3844fa59`, ricerca armature dei muri su catalogo storico e regole Concrete condivise, dati tipizzati, annullamento e controlli di ammissibilità. Design 0.6.0.0; 23 test PASS e API compatibili. Adattatore/corpus completo nel passo successivo.
  - [x] F4.10c.2 (14/10): adattatore ricerca muri collegato al set candidato D. Dodici cataloghi, tre rifiuti e 68184 numeri equivalenti (scarto zero), proposte e giudizi invariati rispetto al congelato; confronto completo con ricerca precedente sugli stessi servizi GPC. Runner 6 PASS; nessuna nuova eccezione architetturale. Registro F4-17, distribuzione F4.9.
  - [x] F4.10b.1: Checker `6cb4c21c`, ricerca dei tratti dei pali sui servizi esistenti, criteri tipizzati, annullamento e avanzamento. Sezione reale con uno/due worker e vecchio selettore concordi, 20 test Design PASS e API compatibili. Design candidata 0.5.0.0; adattatore e confronto completo ancora aperti.
  - [x] F4.10b.2 (14/10): adattatore tratti collegato a Design 0.5.0.0 nel set candidato C. Proposta congelata e due cataloghi estesi identici con uno/due worker; annullamento, avanzamento, conferme e input mancanti provati. Runner 8 PASS, compresa Application; errore iniziale del lettore sui segni corretto prima del commit. Motore legacy conservato, distribuzione F4.9. Registro F4-16.
  - [x] F4.10a: Checker `b8763b87`, distinta basata sul servizio condiviso dei dettagli. 30 distinte, 435 barre, 600 tratti e 1680 punti equivalenti; quantità intere identiche. 13 test Design PASS, API compatibili; Design candidata 0.3.0.0. Ricerche e integrazione ancora aperte.
- [x] F4.11 GPC.Design: BridgeConcept con listini e CO₂ come dati versionati. **Riattivata il 13/10 sera**: cataloghi sostituibili, associazioni alle lavorazioni, prezzi personalizzati e revisioni; calcolo e ottimizzazione in GPC.Design, gestione documentale/UI in ANTHEA.
  - [x] F4.11a.0 (14/10): cattura prima del port, 31 casi/14 famiglie/17 griglie di ottimizzazione, quantità/reazioni/dettagli e graduatorie. Manifest SHA dei dati e dei dieci sorgenti, tempi separati. Runner 2 PASS; motore ancora invariato.
  - [x] F4.11a.1–2 (14/10): port nativo Checker `fb0e6f81` e fix campionamento `932ed63a`, Design 0.11.0.1. Collegamento ANTHEA tramite contratti espliciti, catalogo condiviso, archivi preservati e ramo diagnostico. 39 test nativi; 31 modelli/17 griglie/161550 numeri equivalenti nell'adattatore, 33 campioni ridondanti classificati. Set candidato J; runner applicativo 10 PASS, progressi e identità dei trial corretti. Cataloghi, materiali e traffico restano aperti. Registro F4-23.
  - [x] F4.11b.1–3 (14/10): Checker `454efa78`, `1c475a0a`, `2d6fc667`; contratti di quantità/prezzi, rivalutazione senza ricalcolo e uno snapshot per tutta la ricerca. Design 0.14.0.0, 45 test PASS e API compatibili. Prezzi mancanti non diventano zero, inclusioni per lavorazione e unità tipizzate; attivazione applicativa ancora in corso.
  - [x] F4.11c.1 (14/10): lettore celle Excel estratto dal precedente import in Serialization, riusabile per i prezzari. Prima/dopo Application PASS, architettura PASS; unica impronta dell'indice alfabetico trasferita senza allargare la baseline. Registro F4-24.
  - [x] F4.11c.2 (14/10): gestione prezzari MVVM, import CSV/XLSX/JSON, associazioni, prezzi personalizzati con motivo e riporto esplicito, anteprima prima/dopo, applicazione transazionale e cronologia. Rivalutazione facoltativa di A con propria storia conservata; ricerca invalidata e rilanciabile. Report/CSV indicano la revisione effettiva. Runner 14 PASS e 24 controlli WPF nei tre temi; rinforzi finali 5 PASS. Registro F4-25. Guide e distribuzione finali ancora aperte.
  - [x] F4.11a.3 (14/10): materiali unificati senza allargare le tolleranze. Model `95965ff5` 5.3.0.0, Checker `ee07be60` Design 0.15.0.0; 141 resistenze e densità identiche bit per bit nei due ordini di valutazione delle unità. 1236 test Model PASS (due prove native facoltative non eseguite), 45 Design PASS. Registro F4-26, candidata L in preparazione.
  - [x] F4.11b.4 (14/10): Checker `865f360d`, Design 0.15.0.1 rifiuta inclusioni prive di scomposizione supportata. Relazione attiva: armatura nei prezzi dei pali; le altre non vengono ignorate silenziosamente. Test mirati PASS, integrazione nel prossimo set candidato. Registro F4-27.
  - [x] F4.11c.3 (14/10): revisioni ambientali indipendenti dal prezzario, fattori storicizzati nel documento, impronta dei dati effettivamente calcolati in risultato/report/CSV. Candidata L completa; environment-a: 8 PASS, corpus Bridge invariato (161550 numeri). Produzione ancora set V; chiusura F4.16 pendente.
  - [x] F4.11d.1 (14/10) — servizio traffico: Checker Design 0.16.0.0: Schema 1 NTC, corsie, tandem e inviluppi da linee di influenza cubiche. Quarantotto test Design PASS inclusi 20 attesi Python preesistenti e controlli esatti di travi appoggiate/continue. Tre campate, 123 sezioni: 3.776 ms. Nessun cambiamento ancora in BridgeConcept o negli archivi; integrazione e prestazioni della ricerca nel passo seguente. Dettagli e SHA nel documento Checker docs/refactoring/f4-bridge-traffic.md.
  - [x] F4.11d.2 (14/10): motore travate Schema 1 e ricerche collaudato, 50 PASS; strutture superiori e adattatore UI ancora in corso.
  - [x] F4.11d.3 (14/10) — traffico, interfaccia e provenienza: Set N da Checker e3d8c273, Design 0.18.0.1, completo. Nuovi documenti Schema 1, precedenti conservati con migrazione esplicita; scelta dedicata e gamma Q unico, modifica volontaria al valore consigliato. Min/max dei diagrammi e delle reazioni, metodi e coefficienti dal risultato in report/CSV. Strutture superiori con ambito parametrico dichiarato. traffic-ui-c: 11 PASS, WPF nei tre temi e tutte le famiglie; 161550 numeri precedenti invariati entro la soglia originale. Fix di un ULP al bordo campata riprodotto prima e verificato dopo (52 Design PASS); nessuna tolleranza modificata. F4-29. Distribuzione e guide restano F4.9/F4.16.
- [x] F4.12 GPC.Design: ricerca delle armature di sezione.
  - [x] F4.12a (14/10): Checker `cb0ef314`, servizi condivisi per quantità, obiettivi, graduatoria/Pareto, campionamento, rete ortogonale e riserva torsionale. Design 0.7.0.0, 27 test PASS e API compatibili. Adattatore, coordinamento completo e NEd ancora da chiudere.
  - [x] F4.12b.1 (14/10): collegati quantità, obiettivi, Pareto, rete secondaria, distribuzione torsionale e campionamento; mantenuti rami diagnostici. Nove casi congelati, tre rifiuti, 81 candidati e 18039 numeri equivalenti; regressioni progetto sezioni e architettura PASS, runner 8 PASS. Registro F4-18. Coordinatore completo/NEd/distribuzione ancora aperti.
  - [x] F4.12c.1 (14/10): Checker `b974777e`, catalogo e coordinatore in GPC.Design 0.8.0.0. Trenta test PASS, API compatibili; graduatoria/esclusioni indipendenti dai worker, riuso longitudinale, budget e cancellazione. Collegamento applicativo e residui NEd ancora aperti.
  - [x] F4.12c.2 (14/10): coordinatore e catalogo collegati alla candidata Design 0.9.0.0 (Checker `b8867d9c`, quota torsionale e soglie comuni incluse). Runner 10 PASS, 9 casi/81 candidati/21579 numeri equivalenti e stessi contatori di riuso; rami precedenti conservati, preparazione documenti e note condivise. Un residuo matematico rimosso dalla baseline. Restano NEd e le ultime proiezioni numeriche delle verifiche, prima della chiusura F4.12; registro F4-19.
  - [x] F4.12d.0 / F4.15b.0 (14/10): cattura prima della correzione degli input: 13 casi, 2 PASS runner, SHA in `tests/fixtures/f4/input-contracts-reference.json`. Riprodotta NEd scheda 100 kN contro progetto 6000 kN, As,min 720 contro 1533.333333 mm². Dodici casi di copriferro/progetto congelati per il confronto successivo.
  - [x] F4.12d.1 (14/10): corretto NEd nella scheda dettagli, includendo le righe del taglio già considerate dal progetto. Corpus prima/dopo SHA preservato, trazione/dati errati/input invariato provati; caso gravoso porta As,min a 5111.111111 mm² e fallisce correttamente. Runner 11 PASS, nessuna regressione delle proposte. Registro F4-20; chiuso F2.8-U4.
  - [x] F4.12e.1 (14/10): Checker `89ec3fc6`, domande e rapporti nel coordinatore GPC.Design 0.10.0.0; 34 test PASS e API compatibili. Collegamento e confronto applicativo successivi.
  - [x] F4.12e.2 (14/10): collegate le ultime proiezioni numeriche a Design 0.10.0.0: NEd, rapporti SLE, ancoraggi e capacità obiettivo. 11 controlli pertinenti PASS, corpus 21579 numeri equivalente e prove NEd PASS; ridotta di una occorrenza la baseline. Ricerca sezioni completata nei sorgenti/candidata; rimangono distribuzione F4.9 e full finale. Registro F4-22.
- [x] F4.13 Logica ingegneristica dei percorsi censiti trasferita e riusi completati; [audit dei residui espliciti](f4-audit-finale.md). Corpus 53 + 44 casi, baseline ridotta senza aggiunte, motori diagnostici conservati. Distribuzione/collaudo finale F4.9/F4.16.
  - [x] F4.13/F4.14 (14/10) — cattura preventiva: Congelati 53 casi di geometria taglio, seconde file, sezioni cave, predalle, nove profili normativi e micropali inclinati. Manifest SHA in tests/fixtures/f4/residual-reference.json. residual-capture-b: 2 PASS; prima cattura corretta soltanto per la proprietà dei nodi JSON nel banco. Nessun metodo ingegneristico modificato prima della cattura.
  - [x] F4.13a (14/10) — geometrie e stratigrafia: Collegati i tre contratti GPC (Design 0.19, CompositeBridge 1.6, Geotechnics 0.3), mapping DesignBar comune e modulo automatico da Model/Homogenization. Nessun terreno fittizio per le sole quote del micropalo. 53 casi congelati equivalenti e input invariati. residual-adapter-c: 10 PASS, suite completa adattatori/SLE; set P completo. Registro F4-30. Restano il braccio del taglio e il riesame finale dei residui. *(Audit 14/10: il mapping delle predalle di questo passo, commit `ba307ce0`, ha introdotto la regressione F4-36, emersa solo nel full e corretta in `7d3c9bc9`.)*
  - [x] F4.13b/F4.14b (14/10) — cattura degli ultimi adattatori: 44 risultati congelati prima delle modifiche: sette norme, rettangolo/circolare/anello, copriferri 40/70 mm e due cambi di norma della sezione composta. Manifest last-adapters-reference.json. last-capture-a: 2 PASS; nessun rifiuto inatteso, output e coefficienti completi conservati.
  - [x] F4.13b (14/10) — ultimi calcoli condivisi: Braccio del taglio in Concrete 0.8.0.0, duttilità danese e area delle barre condivisi; proprietà delle aste Bridge dal contratto nativo e report sintetico dallo stesso rapporto circolare. 44 casi congelati, 53 residui e 161550 numeri Bridge equivalenti; suite completa adattatori/SLE PASS. Tredici occorrenze eliminate, nessuna aggiunta. last-adapters-a ha individuato un richiamo UI diretto al catalogo: corretto tramite ConcreteEditor e collaudato in last-adapters-b (5 PASS). Registro F4-32. Resta l’audit finale F4.13 e la distribuzione F4.9.
- [x] F4.14 [CP] Catalogo Model tipizzato, migrazioni e input ereditati tracciati, dati nulli/errati rifiutati, norma dal risultato nei report. F4-31/F4-33/F4-34; sette controlli finali PASS. Due soli ripieghi diagnostici conservati, [audit](f4-audit-finale.md). **Audit 14/10: tecnicamente fatto; ratificato dall'utente il 14/10 ([decisioni](decisioni.md), punto 4b)**; la migrazione dei vecchi documenti senza norma è decisa al punto 2c e attuata con F4-40.
  - [x] F4.14a (14/10) — norme, rifiuti e report: Catalogo Model 5.4.0.1 e nove profili congelati equivalenti. Nuove sezioni con NTC esplicita; vecchie senza norma migrate con nota, coefficienti conservati; norme errate rifiutate. Terreno micropalo ignoto non diventa ghiaia. Report CA dal proprio snapshot, con rifiuto se assente/incoerente. residual-adapter-c: 10 PASS. La prima corsa ha rilevato la differenza prevista per l’ambito della fessurazione senza norma: verifica ora visibile e dato richiesto, confronto legacy mantenuto separatamente. Registro F4-31. Restano audit dei default delle sezioni composte e tracciamento degli input ereditati.
  - [x] F4.14b (14/10) — migrazioni e sezioni composte: Profili composti riconosciuti esattamente, coefficienti dai servizi GPC e default Bridge esistente; due cambi di norma congelati equivalenti, override conservati. I campi CA mancanti integrati dall’archivio vengono registrati e mostrati in scheda/report; nulli e dati errati restano invalidi. Progetto senza NTC di ripiego. last-adapters-b: 5 PASS con confine UI/Application ripristinato, nessuna deroga architetturale. Sei occorrenze eliminate. Registro F4-33; audit conclusivo, guide e distribuzione restano separati.
  - [x] F4.14c (14/10) — etichette dei risultati: Norma effettiva validata per i risultati dei pali, descrizione esplicita dei coefficienti unitari e segnalazione di provenienza assente nella vista. Filtro di omogeneizzazione composto sul profilo tipizzato. standard-labels-a: 7 PASS, 20 pali/28 tratti/22188 numeri equivalenti. Altre quattro occorrenze eliminate; solo due ripieghi R3 diagnostici conservati. Registro F4-34.
- [x] F4.15 Copriferro di progetto in un validatore di Application.
  - [x] F4.15a (14/10): servizio CoverAssessment in Concrete 0.7.0.0, requisiti materiali/barre condivisi ed esiti comuni; dati mancanti non certificano un copriferro. Tre prove indipendenti PASS, API compatibili. Collegamento e confronto Application ancora aperti.
  - [x] F4.15b (14/10): sezioni e muri usano lo stesso giudizio nativo tramite adattatore di durabilità; il progetto non ricalcola i minimi. Dodici casi congelati e testi invariati, 247 percorsi con sonda, 11 PASS runner incluse regressione completa adattatori/SLE, ricerca muri e distinta. Nessuna nuova eccezione; registro F4-21. Distribuzione e collaudo full F4.9/F4.16 ancora aperti.
- [x] F4.16 [CP] Chiusura, pagine dei metodi, eliminazione del legacy su autorizzazione. **Riattivata il 13/10 sera**: richiesta di completare F4 durante la notte, con commit e aggiornamento a ogni passo. Confronti, guide, treno coerente e collaudo full restano obbligatori; F5 esclusa.
  - [x] Audit 14/10 — cosa manca davvero per chiudere F4.16:
    - un full conforme con il set W: il primo ha 114 PASS e 5 NEW-FAIL, il secondo è interrotto;
    - la revisione puntuale delle 6805 differenze del confronto UI con la cattura F3 ([prospetto](f4-transizioni-ui-da-revisionare.md)), distinguendo la parte che deriva già dalla 1.7.1: fatta il 14/10 ([classificazione](f4-transizioni-ui-classificazione.md), nessuna differenza non spiegata), da rifare sul commit finale con il full;
    - le pagine dei metodi per σcp nel taglio dei muri (F4-03), per la curvatura con viscosità (F4-08), per il traffico Schema 1 (F4-29) e per GPC.Design: scritte il 14/10 in Checker `docs/metodi` (commit 26cef01a, esempi verificati con la libreria), su master del Checker e pubblicate il 14/10 su autorizzazione dell'utente;
    - l'elenco del legacy da rimuovere con i chiamanti rimasti, da preparare prima di chiedere l'accordo dell'utente;
    - il requisito «analisi composte» dei prezzari (decisioni del 13/10 sera): attuato il 14/10 dopo l'audit (registro F4-45,
      GPC.Design 0.20.0.0 nel set X);
    - l'approvazione dell'utente per il merge di F4 in main.

    Esito al 15/10: tutti i punti chiusi.
    - Full conforme `20261015-105444-f4-full-190`.
    - Confronto UI con le 6878 transizioni ammesse (D1, D2).
    - Legacy fuori dagli assembly distribuiti, senza cancellazioni (D3, F4-49).
    - Merge approvato (D5).
  - [x] F4.16a (14/10) — guide Rev43 e versione: Guide globali pratiche e teoriche aggiornate per prezzari/revisioni/inclusioni, traffico Schema 1, input normativi e calcoli condivisi; esempio storico Bridge conservato con ipotesi esplicite. Word/PDF finali: 80 + 145 pagine, 337 formule e parti ITEC conservate, rendering e controllo grafico completati. Wiki: 71 capitoli/6736 termini; indici aggiornati. Rev42 e indici precedenti archiviati con SHA e origine. ANTHEA 1.8.0 per le nuove funzionalita F4 (secondo componente), commit 0eaebd3e. Restano il full con il set W e la chiusura del rilascio.
  - [x] 14/10 audit — guide Rev44: riferimenti di terzi tolti (punto 7) e controllo automatico esteso; durabilità con UNI 11104:2016 e testi propri (U1, U5); report Word e PDF senza Word, norma esplicita, NEd dei pilastri, piano delle deformazioni totali e analisi composte documentati. Word/PDF 82 + 145 pagine, 345 formule OMML, 26 parti ITEC identiche per volume, 227 pagine rasterizzate senza sforamenti né glifi sostitutivi. Wiki 71 capitoli/6716 termini. Rev43 e indici archiviati in `supporto/SUPERATI/f4-rev44-20261014` (16 file con SHA-256). Quick 47 PASS, installer 2 PASS.
  - [x] F4.16b (14/10) — regressione predalle: F4-36: mapping dei soli dati delle file attive, campi dormienti conservati. predalle-fix: 9 PASS; 53 casi congelati equivalenti, geometria/report/progetto e prova WPF superati. Nessun atteso o tolleranza modificato.
  - [x] F4.16c (14/10) — muri precedenti: F4-37: quota inferiore letta solo con due zone attive. Suite storica muri PASS; confronti congelati delle verifiche, distinte e ricerca PASS (9 esiti complessivi). Archivi e attesi conservati.
  - [x] F4.16d (14/10) — esempio Wiki: F4-38: esempio della trave aggiornato al contratto N,Vx,Vy,T,Mx,My; momento 200 kNm invariato. Prova completa della Wiki WPF PASS.
  - [x] F4.16e (14/10) — confronto capitoli: Inizializzata una sola volta la data della revisione ambientale, prima delle copie indipendenti della fixture. Il confronto esatto scheda/capitolo progetto resta attivo e passa per tutti i moduli; nessun campo escluso, nessun atteso aggiornato. wiki-chapters: ui/f3-report-chapters PASS.
  - [x] F4.16f (14/10) — provenienza dell’archivio guide: Disabilitata la normalizzazione Git del solo archivio Rev42: tre Markdown conservano ora i byte CRLF originali anche nei checkout futuri. Verificati i 16 SHA sia nei file sia nell’indice; manifest e contenuti originali invariati.
  - [x] F4.16g (14/10) — confronto UI storico da revisionare: Prima corsa full: 114 PASS e cinque fallimenti. Predalle, muri, Wiki e capitoli corretti e ricollaudati; resta il confronto esatto F3 con le variazioni F4. [Prospetto delle 6805 differenze](f4-transizioni-ui-da-revisionare.md), riferimento F3 e tolleranze invariati. La revisione automatica ha respinto una classificazione per interi file; proposta rimossa. Chiusura e merge finali subordinati alla revisione puntuale e al full conforme.
    Full del 14/10 sera (`20261014-230601-f4-full-20261015`, commit `fd28f353`, albero pulito, stadi build, fast, regression, wiki, baseline e ui): 136 PASS, 2 NEW-FAIL.
    - `ui/f3-equivalence`: 7214 differenze, tutte nella previsione scritta prima della corsa ([f4-full-20261015-attese.md](f4-full-20261015-attese.md)), nessun numero di calcolo cambiato.
    - `ui/smoke-bridge-prices`: prova non aggiornata a F4-45, corretta in `5ba83f1f`.

    Restano le decisioni D1–D5 di [f4-chiusura-decisioni.md](f4-chiusura-decisioni.md) e la corsa conforme dopo D2.
    15/10: D1–D5 decise dall'utente ([decisioni](decisioni.md)). D2 fatta (registro TEST-03): `ui/f3-equivalence` PASS con le 6878 transizioni elencate e la Wiki aggiornata; restano D3 (codice legacy e di prova fuori dagli assembly distribuiti) e il full conforme.
    15/10: D3 fatta (registro F4-49), modo (b) confermato dall'utente:
    - **Spostamento.** Codice legacy e di sola prova in `tests/ANTHEA.Legacy` e nei progetti di prova, senza cancellazioni; nel prodotto resta solo l'aggancio `EngineHook`.
    - **Esecuzione.** Otto famiglie, sette lavorate in parallelo su rami propri e unite in `98e1535c`.
    - **Confronto delle catture.** Prima (`4144fe9e`) e dopo: uguali in tutte le famiglie.
    - **Baseline F4.** Da 2909 a 2211 voci.
    - **Installer.** `Assert-NoTestCode.ps1` esteso a 12 assembly e ai nomi spostati.

    Resta il full conforme.
    15/10: full conforme `20261015-105444-f4-full-190` sul commit pulito `a46e91fb`: 140 PASS, 0 KNOWN, 0 NEW-FAIL,
    0 BLOCKED, comprese le suite dell'installer 1.9.0. Il full precedente (`20261015-101226-f4-full-d3`) aveva un NEW-FAIL
    di build in `ConcreteShort.UiChecks`, corretto in `ea4b1de5`. F4 chiusa: [verbale](chiusura-f4-20261015.md).
  - [x] 14/10 notte — attesi indipendenti e preparazione della chiusura:
    - TEST-02: gli ultimi 32 attesi di `casi_confronto.json` rigenerati dal codice sono ricalcolati con `tools/attesi` (30 sezioni a T, 2 pali).
    - F2-7: due stati mirati con armature libere.
    - Runner: nuovi tentativi su `summary.txt`.
    - Registro: voce F4-48 per le guide Rev41–Rev44.
    - Riepilogo delle decisioni per chiudere F4.
  - [x] F4.16h (14/10) — banco installer isolato: Le fixture modificabili del catalogo e l'installazione di prova delle guide sono ora in una cartella temporanea univoca esterna al checkout; log e percorso della fixture restano in supporto/artefatti. La stessa disinstallazione, interrotta sotto Documents/Repository, completa il percorso in Temp. Tutti i 30 controlli originari rimangono attivi e passano, inclusi rimozione dei PDF/collegamenti e conservazione dei file personali. Evidenza: installer-180-e, installer/guides PASS. Nessuna esclusione o tolleranza introdotta.
  - [x] F4.16i (14/10) — installer 1.8.0: pulizia e blocco applicazione: Richiesta esplicita: eliminare tutto il contenuto della destinazione prima della copia e impedire l'installazione se ANTHEA è in uso. Controllo dei processi all'avvio e prima delle operazioni; nessuna chiusura forzata. Percorso locale canonico, cartelle protette e collegamenti controllati prima della precedente disinstallazione e della pulizia. File bloccati o pulizia incompleta impediscono la copia. Codici silenziosi 10 (programma in uso/controllo non disponibile), 11 (destinazione), 12 (pulizia). Disinstallazione ordinaria ancora limitata al catalogo originale.

    Runner installer-180-e: 2 PASS, 56 controlli (26 sicurezza/pulizia + 30 guide), nessuna esclusione. Build Release, Architecture, Wiki e indici: 7 PASS in installer-180-b; i due errori di avvio delle suite PowerShell sono risolti nella corsa e eseguendo il runner da Windows PowerShell come previsto. Guida pratica Rev43 aggiornata (81 pagine, 941 frammenti verificati, 26 parti ITEC identiche), PDF/Word e Wiki coerenti; README installer e PDF aggiornati. Resa delle 84 pagine interessate controllata, nessuno sforamento. Registro F4-39. Versione applicazione 1.8.0 già assegnata per le funzionalità F4.

    Il secondo full avviato in precedenza non ha prodotto un esito finale ed è da considerare interrotto, non superato. La richiesta di commit, installer e push procede sul ramo F4; chiusura e merge della fase restano subordinati alla revisione puntuale del confronto UI F3 già documentata. F5 non avviata.
  - [x] 14/10 — installer 1.8.0 prodotto e collaudo finale: [Verbale del pacchetto](installer-1.8.0.md): setup da d66379ac, 62886541 byte, 12 DLL coerenti e tre guide PDF. Runner finale sul commit pulito: 9 PASS, 0 NEW-FAIL, inclusi i 56 controlli installer. File locali e SHA-256 nel verbale. Commit e push richiesti dall’utente; F4.16 resta aperta sul confronto UI storico, F5 esclusa.

## Fase 5: interfaccia MVVM

Stato e passi da fare aggiornati al 21/10 (set AE, installer 1.12.1, agenti in corso): [f5-ripresa.md](f5-ripresa.md); piano dei prossimi blocchi: [f5-piano-dal-21-10.md](f5-piano-dal-21-10.md).

- [x] F5.0 [CP] Ripianificazione sul codice dopo F4; `ANTHEA.Presentation` senza WPF. Decisa il 16/10 mattina: tutte le
  raccomandazioni di Q1–Q5, Q7, Q11, Q12, Q15, Q16, Q19 e Q20 ([decisioni](decisioni.md), sezione del 16 ottobre mattina).
  Documenti: [nota per il punto di controllo](f5-ripianificazione.md), [inventario dell'interfaccia](f5-inventario-ui.md),
  [proposta di architettura](f5-proposta-architettura.md). Le altre domande si decidono ai passi indicati.
- [x] F5.1 Infrastruttura, servizi, gestione degli errori, analyzer. Fatta il 16/10 su `refactoring/f5` (merge 6486b5cf e
  bbb89416):
  - `src/ANTHEA.Presentation` senza WPF, con `ViewModelBase`, `CommandGuard` (Q19), comandi guardati, `SheetCalculation` sullo
    scheduler esistente, `CalculationLimiter` di istanza, servizi astratti e radice di composizione `DesktopComposition`;
  - `tests/ANTHEA.Presentation.Tests` nello stadio fast (79 controlli);
  - regole P1–P6 con 91 prove dei rilevatori e censimento P3 dello stato statico (133 voci, solo in calo);
  - Microsoft.VisualStudio.Threading.Analyzers 18.7.23 solo su Presentation.

  Corsa build, fast, wiki e ui sul merge `20261016-073701-f5-int2`: PASS 106, 0 NEW-FAIL; `ui/f3-equivalence` invariata.
- [x] F5.2 `ModuleHost` (moduli nuovi e legacy insieme); componenti dei form. Fatta il 16/10 su `refactoring/f5`:
  - **Editor dei fogli** (merge 85575d2f, a0bf6e3a):
    - `ISheetEditor` e `SheetEditorFactory` sono usati da `MainWindow`, dalla cattura F3 e da 11 delle 16 costruzioni nelle
      prove;
    - la regola dei renderer copre anche la fabbrica.
  - **ModuleHost** (merge 1264a1c1):
    - il contratto (`IModuleDescriptor`, `ISheetModule`, `SheetContext`, `ISheetViewModel`, `ISheetHost`, `ModuleRegistry` su
      `ModuleDirectory`) e `SheetViewModelBase` stanno in Presentation;
    - `MvvmSheetEditor` e `ModuleImplementations`, con 13 moduli tutti storici;
    - la fabbrica viene dalla radice di composizione;
    - `--module-impl <modulo>=mvvm` esiste solo nella build UiTests;
    - il confronto diretto a differenza zero di due catture F3 è `tools/qa/f5_confronto_diretto.py`;
    - c'è una prova fuori schermo, `ui/check-module-host-offscreen`.
  - **Componenti dei form** (merge d39d0183):
    - `FieldSpec` con le regole di scrittura, `FieldViewModel`, `FormViewModel`, `JsonRowViewModel` e `JsonRowCollection`;
    - gli argomenti di aiuto stanno in `Presentation/Help`;
    - `FieldForm` è in XAML;
    - parità con `InputForm` (`ui/check-field-form-parity`, 2025 controlli, stessi byte del documento);
    - il censimento P3 scende da 133 a 130.
  - **Progetto** (merge d168f8d3):
    - `SharedDataCoordinator` copre dati condivisi, anteprima dello spostamento, confronto e coerenza;
    - `ProjectHistory` copre annulla e ripristina;
    - `MainWindow` li usa con gli stessi testi e la stessa sequenza.
  - **Corse:**
    - build, fast, wiki e ui sul merge `20261016-094843-f5-int4`: PASS 108, 0 NEW-FAIL;
    - `ui/f3-equivalence` è invariata;
    - Presentation.Tests conta 318 controlli.
  - **Correzioni delle prove:**
    - il riepilogo del runner si scrive con un flusso condiviso (2abd404e);
    - la smoke c.a. attende il disegno della scheda (17712fa4).
  - **Rinviati a F5.13:**
    - il report di progetto della cattura passa dalla sessione del progetto;
    - `ConfirmDiscard` passa da `IConfirmationService`;
    - dopo un errore si ripristinano anche i fogli collegati.
  - **Rinviati al primo modulo migrato:** controllo tabellare XAML, aiuto F1 nell'ospite, esportazioni per modulo, anteprima
    dei materiali (F5.5).
- [x] F5.3 [CP] Temi a token. Fatta il 16/10 (merge 1626e776); il punto di controllo è stato accettato dall'utente nello
  stesso giorno, con tutte le raccomandazioni e `Font.Subheading` = 14 ([decisioni](decisioni.md), sezione del 16 ottobre
  pomeriggio; [documento](f5-temi-token.md)).
  - **Token:** 69 colori (36 semantici e i gruppi Series, Drawing, Swatch e Utilization), registrati con lo stesso pennello
    di `Appearance.Colour`, e 7 dimensioni `Font.*`.
  - **Dizionari** in `X.Desktop/Themes`, con le viste storiche invariate.
  - **Regola P7** con 36 prove dei rilevatori; `FieldForm` usa solo token.
  - **Prove nuove:** `ui/check-theme-tokens` e `ui/check-contrast` nello stadio ui. Scuro e Molto scuro hanno 0 difetti; la
    Chiara ne ha 699 nella baseline a 96 DPI.
  - **Corsa** build, fast, wiki e ui sul merge con il pilota `20261016-123436-f5-int5`: PASS 111, 0 NEW-FAIL.
- [x] F5.4 [CP] Modulo pilota: palificata orizzontale. Punto di controllo accettato dall'utente il 17/10 («accetto tutte le
  proposte», [decisioni](decisioni.md), sezione «17 ottobre (mattina)»); chiusa lo stesso giorno sul ramo
  `refactoring/f5-4-chiusura` ([ricetta, materiale e chiusura](f5-4-pilota.md), sezioni 4 e 5).
  - **Preparazione** (merge 826eb8ed):
    - 6 fixture F3 nuove catturate con la vista storica e 11 tracce congelate;
    - derivazioni spostate in Application;
    - ViewModel uguale byte per byte alla vista storica;
    - `SheetCalculationMode.Synchronous` (Q8 a).
  - **Vista XAML** (merge 065f6ab3):
    - `Views/PileGroup`, con sola lettura e aiuto nel form comune;
    - banco `--module-impl`;
    - confronto diretto storica/MVVM identico su 514 file;
    - `ui/check-pile-group-view` al posto della parte UI di `HorizontalPileGroup.Checks`;
    - contrasto 0 in Scuro e Molto scuro.
  - **Versione di prova portatile `1.11.0-prova.f5`**, consegnata all'utente il 16/10. Ramo locale `prova/f5-palificata`, con la
    palificata MVVM anche nella Release.
  - **Chiusura** (passi 6–7 della ricetta):
    - `832af117`: `ui/check-pile-group-view` confronta con i valori della vista storica senza aprirla (tracce congelate, ricetta
      delle esportazioni, valori registrati);
    - `e8855058`: la Release apre la palificata con la vista MVVM e tutti gli altri moduli con quella storica. La vista storica
      e la sua prova delle tracce sono in `supporto/SUPERATI/ui-storica/palificata`. Sono tolti i rami della palificata di
      `SheetEditor` e la suite `ui/check-pile-group-historic`. La voce della Chiara del contrasto passa alla vista nuova (D12),
      a totale invariato;
    - baseline di architettura invariate (il file archiviato non aveva più voci F4 né P3).
  - **Corse:**
    - `ui/f3-equivalence` con la Release MVVM: PASS, 0 differenze non ammesse, `transitions-f4.json` invariato;
    - cattura identica a quella della Release storica di `20261017-060301-f5-int13` (confronto diretto);
    - corsa finale build, fast, wiki e ui senza `ui/check-contrast` su `e8855058` (`20261017-095952-f54c-finale`): PASS 118,
      0 NEW-FAIL (build 25, fast 31, wiki 2, ui 60: una suite in meno, `ui/check-pile-group-historic`).
  - **Restano:**
    - `ui/check-contrast` alla prima corsa a 96 DPI (esito atteso nella sezione 5.2 della nota);
    - la correzione del nome dell'espansore nella guida pratica, che è sbagliato già prima del passaggio («Applicabilità e
      interassi rappresentativi» invece di «Opzioni di calcolo e applicabilità»). Va fatta in un passo documentale con
      l'aggiornamento editoriale F3-06 della Wiki, poi PDF da rigenerare (sezione 5.4 della nota). Il passaggio in sé non cambia
      la guida.
- [ ] F5.5–F5.12 Materiali, palo verticale, orizzontale, elastico, muri, sezione c.a., sezione composta, Bridge Design.
  - **Preparazioni fatte il 16/10** (passi 1–5 della ricetta, Release storica). Per entrambe restano la vista XAML e il punto
    di controllo:
    - **F5.5, materiali** ([f5-5-materiali.md](f5-5-materiali.md), merge ac82c69e):
      - 12 fixture e 21 tracce;
      - `PreviewData` nel contratto dei fogli e `SheetCalculationMode.None`;
      - ViewModel del calcestruzzo e dell'acciaio.
    - **F5.6, palo e micropalo verticale** ([f5-6-palo-verticale.md](f5-6-palo-verticale.md), merge 56992d3c):
      - 7 fixture, 7 figure e 18 tracce;
      - ViewModel sullo sfondo;
      - un difetto storico del micropalo segnalato (cambio dell'iniezione con un risultato presente).
  - **Corsa** build, fast, wiki e ui sul merge `20261016-155151-f5-int7`: PASS 114, 0 NEW-FAIL, 61 fixture F3.
  - **Preparazioni della notte fra il 16 e il 17/10** (passi 1–5 della ricetta, Release storica; manca la vista XAML di ogni modulo):
    - **F5.9b, fondazioni superficiali** ([f5-9b-fondazioni.md](f5-9b-fondazioni.md), merge 6ea62a0f): 8 fixture, 15 tracce,
      ViewModel sincrono senza ricalcolo automatico.
    - **F5.7, palo e micropalo orizzontale, scheda della capacità** ([f5-7-palo-orizzontale.md](f5-7-palo-orizzontale.md), merge
      4b6a59de): 6 fixture, 22 tracce e 40 disegni congelati, ViewModel sullo sfondo.
    - **F5.6b, stratigrafia** ([f5-6b-stratigrafia.md](f5-6b-stratigrafia.md), merge 743d7aec):
      - le prime 5 fixture e 15 tracce;
      - l'importazione con l'IA spostata in Application, con la sorgente iniettabile; le prove non usano mai la rete;
      - il ViewModel senza calcolo.
    - **F5.9, muri di sostegno** ([f5-9-muri.md](f5-9-muri.md), merge cb35f915): 12 fixture, 24 tracce, ViewModel con la finestra
      dei valori; le 14 441 transizioni F4 dei muri restano identiche con il ViewModel.
    - **F5.8, risposta elastica del palo e armature** ([f5-8-palo-elastico.md](f5-8-palo-elastico.md), merge 0954c59f):
      - 5 fixture, 20 tracce e 49 disegni;
      - ViewModel integrato con quello di F5.7;
      - le 1 117 transizioni F4 dell'elastico restano identiche con il ViewModel.
    - **`SheetCalculation.Cancel(restorePublished)`.** Il palo orizzontale lascia lo stato annullato, i muri tornano al risultato
      pubblicato, come le rispettive viste storiche.
    - **F5.10, sezione c.a., parte A** ([f5-10-sezione.md](f5-10-sezione.md), merge 8717b9c9): 7 fixture e 7 figure, 37
      scenari di traccia divisi per sottopasso, 120 disegni 2D congelati; derivazioni di ingressi e azioni; piano dei sottopassi
      S1–S9 con le domande C1–C10. Nessun ViewModel ancora.
  - **Totali dopo i merge:** 104 fixture F3; baseline F4 da 3349 a 3320 occorrenze.
  - **Corse** build, fast, wiki e ui, senza `ui/check-contrast`:
    - merge `20261017-043656-f5-int12`: PASS 118, 0 NEW-FAIL;
    - merge della sezione `20261017-060301-f5-int13`: PASS 119, 0 NEW-FAIL.
    Dalla notte del 16/10 lo schermo è al 150%: per la decisione D6 la prova del contrasto si confronta solo a 96 DPI e va ripetuta.
  - **Domande e difetti** delle viste storiche, raccolti per modulo: [f5-punti-di-controllo.md](f5-punti-di-controllo.md).
  - **Viste XAML del 17/10** (Release ancora storica; il passaggio viene dopo la prova dell'utente). Per ogni modulo il
    confronto diretto fra vista storica e vista nuova dà zero differenze su 1066 file:
    - **F5.9b, fondazioni superficiali** (merge ecf227fc): `ui/check-fondazioni-view` con 294 controlli; il PDF si scrive con
      il generatore del modulo (`IModuleReportPdf`, T8).
    - **F5.5, materiali** (merge 43170c9d): `ui/check-materials-view` con 214 controlli; domande nuove M15–M20.
    - **F5.6, palo e micropalo verticale** (merge b9458f85): `ui/check-palo-verticale-view` con 377 controlli. Arrivano anche il
      dialogo condiviso del trasferimento del terreno (`Modules/Shared`, T5) e la voce F5-01 del registro (V3).
    - **F5.6b, stratigrafia** (merge 034839b4): `ui/check-stratigrafia-view` con 181 controlli. La stratigrafia MVVM usa il
      dialogo condiviso del terreno (T5). `Multiline` e `ToolTip` sono additivi in `FieldSpec`/`FieldForm`. L'importazione con
      l'IA ha una vista nuova, provata senza rete. Domande S13–S17.
    - **F5.9, muri di sostegno** (merge f60875e2): `ui/check-muri-view` con 187 controlli e `ui/f3-scheduling-muri-mvvm`. Le
      14 441 transizioni F4 restano identiche. Dialoghi dei valori, della distinta ferri e del terreno (MU9, MU10). Domande
      MU14–MU19.
    - **Correzioni di integrazione.**
      - 842239dc: il registro di prova delle fondazioni contiene anche la palificata, che nella Release è già MVVM.
      - 9fea83ba: tutti i form delle fondazioni hanno il navigatore della Wiki dei servizi, perché da F5.5 i form ereditano
        il modulo dalla vista.
      - Per le viste future vale la stessa regola: ogni `FormViewModel` ha `WikiNavigator`.
      - 2cf6f276: i pulsanti della vista del palo verticale prendono lo stile del tema (`BasedOn`), come il dialogo condiviso
        (S17).
    - **Corse** senza `ui/check-contrast`:
      - sul merge del palo verticale `20261017-171818-f5-int16`: PASS 123, 0 NEW-FAIL;
      - sul merge della stratigrafia `20261017-211836-f5-int17`: PASS 124, 0 NEW-FAIL;
      - sul merge dei muri `20261017-222849-f5-int18`: PASS 126, 0 NEW-FAIL.
    - **Versioni di prova** sul ramo locale `prova/f5-viste`, consegnate all'utente il 17/10. In ognuna `ui/f3-equivalence`
      passa con le viste attive.
      - 1.11.0-prova.f5b: palificata, fondazioni, calcestruzzo e acciaio con la vista nuova;
      - f5c: in più palo e micropalo verticale;
      - f5d: in più stratigrafia e muri, nove moduli.
  - **Notte fra il 17 e il 18/10** (blocco 8, base 591272d8). Ogni lavoro è chiuso o fermo a un punto chiuso e documentato:
    - **F5.7 + F5.8, palo orizzontale e scheda elastica** (merge d8a3b8cd):
      - fatto: ViewModel, viste XAML e controlli delle due schede; `ui/check-palo-orizzontale-view` con 221 controlli;
      - E1: verificatore c.a. del tratto come dialogo di `IDialogService` con la `ConcreteWorkspace` storica;
      - O6: voce F5-02 del registro;
      - banco fatto (merge 89fade27): catture F3 storica e MVVM PASS 104, 22 126 transizioni F4 ammesse (comprese le 1 117
        dell'elastico), confronto diretto 0 differenze su 1066 file, capitoli PASS 105
        ([f5-7-palo-orizzontale.md](f5-7-palo-orizzontale.md) 5.8, [f5-8-palo-elastico.md](f5-8-palo-elastico.md) 6.8);
      - restano la prova dell'utente e il passaggio dei due moduli insieme (O2/E2).
    - **F5.10 S1, sezione c.a.** (merge eac4773c): guscio, coordinatore del calcolo (C1, C2), ingressi e materiali.
      - 21 scenari uguali alle tracce congelate; 55 file dei dati F3 uguali al riferimento.
      - S2 (ramo `refactoring/f5-10-s2`, 21/10, da unire): riallineamento al codice del 20/10 (sezione 9: tracce congelate
        ancora valide, lacune del ViewModel sulle sezioni da wizard, sottopasso S1b proposto) e azioni (sezione 10:
        `SectionActionsViewModel` in Presentation, C3; tabelle dei pannelli e finestra delle sollecitazioni; 25 scenari uguali).
      - S1b (ramo `refactoring/f5-10-s1b`, 21/10, da unire): sezioni da wizard e persistenza nativa (sezione 11 di
        [f5-10-sezione.md](f5-10-sezione.md)): 18 tracce nuove della vista storica (15 esempi, 3 scenari del wizard), le sei lacune
        chiuse nel ViewModel, 43 scenari uguali.
      - S3 (stesso ramo, 21/10, da unire): domini 3D e 2D (sezione 12 di [f5-10-sezione.md](f5-10-sezione.md)):
        `ConcreteDomainPanelViewModel` sul modello di tabella di S2, disegni come dati, derivazioni in `ConcreteDomainChart`
        chiamate anche dalla vista storica; 45 scenari uguali, domini 3D come dati uguali ai congelati.
      - S4 (ramo `refactoring/f5-10-s4`, 21/10, da unire): SLE e tensioni (sezione 13 di [f5-10-sezione.md](f5-10-sezione.md)):
        `ConcreteStressPanelViewModel` per famiglia sul modello di tabella di S2, opzioni comuni, mappa come dati con la legenda, derivazioni
        in `ConcreteStressPresentation` chiamate anche dalla vista storica; 47 scenari uguali.
      - S5 (stesso ramo, 21/10, da unire): taglio e torsione (sezione 14 di [f5-10-sezione.md](f5-10-sezione.md)): `ConcreteShearViewModel`
        su `Actions.Shear`, staffe in sola lettura delle schede 05 e 06, taglio sul thread dell'interfaccia (C2), derivazioni in
        `ConcreteShearPresentation` chiamate anche dalla vista storica; 49 scenari uguali.
      - S6, S7 e S8 (ramo `refactoring/f5-10-s6`, 22/10, da unire; sezioni 15–18 di [f5-10-sezione.md](f5-10-sezione.md)):
        dettagli e ancoraggi (`ConcreteDetailingViewModel`), sezione σ/ε per pannello e momento–curvatura (`ConcreteSectionInspectionViewModel`,
        `ConcreteCurvatureViewModel`), progetto delle armature (`ConcreteDesignViewModel`, ricerca asincrona annullabile, «Applica
        configurazione»); derivazioni in `ConcreteDetailingPresentation`, `ConcreteInspectionPresentation`, `ConcreteCurvaturePresentation` e
        `ConcreteDesignPresentation` chiamate anche dalla vista storica; 55 scenari uguali, ogni parte della traccia confrontata. Suite `ui/*`
        della vista storica da eseguire la sera (regola del giorno del 22/10).
      - S9, parte ViewModel (ramo `refactoring/f5-10-s9`, 22/10, da unire; sezione 19 di [f5-10-sezione.md](f5-10-sezione.md)): report Word e
        breve con i generatori congelati e le figure WPF alimentate dai dati del foglio (`IConcreteSectionFigureRenderer`), report breve
        nell'ospite (`IModuleShortReport`); 57 scenari uguali (anche `report/*`), percorso F3 completo con i report uguale al riferimento.
        Vista XAML (stesso ramo, sezione 20): le otto schede e le finestre legate ai ViewModel; suite `ui/check-sezione-view` scritta, da
        eseguire la sera; restano le tracce attraverso i controlli della vista.
        Prima esecuzione della vista (ramo `refactoring/f5-10-vista-prova`, 22/10 sera, da unire; sezione 21): suite della vista corretta
        (stato di cultura della cattura storica, ordine C6, identificativi nuovi), sola lettura e aspetto delle otto schede come la storica;
        `ui/check-sezione-view` PASS 131, banco F3 storico e MVVM uguali.
        Tracce guidate dalla vista (ramo `refactoring/f5-10-tracce-vista`, notte 22–23/10, da unire; sezione 22): i 57 scenari congelati guidati
        sui controlli della vista per nome di automazione, finestre comprese, uguali riga per riga; nomi di automazione e finestre «Scala…» e
        «Proprietà / report…» accanto alla storica; quattro correzioni della vista (tabelle in sola lettura, selezione, ordine, nomi).
    - **Suite UI indipendenti dall'implementazione** (merge 35bba417, [f5-suite-ui.md](f5-suite-ui.md)):
      - le 11 suite storiche generali, dei materiali e dei muri passano con la Release di oggi e, nelle varianti `-mvvm`, con
        i sette moduli MVVM;
      - al passaggio di un modulo si toglie solo il ramo storico.
      - Domanda S6 per l'utente: l'antivirus chiude l'eseguibile non firmato quando `DocumentFile.Write` rinomina un PNG.
    - **Corsa** sul merge della notte `20261018-035914-f5-int20`: PASS 139, 0 NEW-FAIL, senza `ui/check-contrast`.
- [ ] F5.13 [CP] Shell; sessione di prova prima di eliminare SheetEditor e la vecchia MainWindow. Requisito
  dell'utente dell'8/10 («considera le opzioni 1 e 3. non multi progetto. però dai la possibilità di staccare la
  finestra per più monitor»):
  - più fogli aperti insieme in schede dentro il progetto;
  - più file di calcolo singoli aperti insieme, con un solo progetto alla volta;
  - schede staccabili in una finestra separata, per lavorare su più monitor.

  Da gestire: aggiornamento dei fogli aperti quando cambiano i dati condivisi (sezione, terreni, fogli CLS
  collegati), modifiche non salvate per foglio, calcoli in parallelo senza stato statico (dopo F2.11). Oggi
  MainWindow tiene un solo foglio e un solo documento (`MainWindow.cs:220-229`).
  - **Parti (a) e (b)** (22/10, ramo `refactoring/f5-13-ab`, da unire; [nota](f5-13-shell.md)): sessioni dei documenti e dei
    fogli, coordinatore dei documenti aperti, storia e revisioni con più schede, dati condivisi propagati alle schede aperte, e i
    ViewModel di navigazione, albero, revisioni, coerenza, schede staccabili e comandi in `src/ANTHEA.Presentation/Shell`; 314
    controlli senza WPF; shell storica e Release invariate. Domande provvisorie SH1–SH22. Restano (c)–(g).
  - **Parte (c) fino al punto di controllo** (22/10, ramo `refactoring/f5-13-c`, da unire; [nota](f5-13-shell.md), sezioni 8–10):
    `ISheetExports` sul codice di `MvvmSheetEditor`, `IWindowService`, menu «Aspetto», argomenti di avvio, Wiki e Modelli nei ViewModel
    (89 controlli senza WPF); bozza XAML della shell in `X.Desktop/Views/Shell` (non è la finestra della Release) con la cattura
    `ui/check-shell-draft` da eseguire di notte. **[CP]** sulla bozza: domande SH23–SH32 e le SH1–SH22 che la bozza rende visibili.
  - **Punto di controllo della bozza** (22/10 sera): le dieci immagini approvate, SH5–SH32 con le opzioni della bozza. Ramo
    `refactoring/f5-13-c2`, da unire ([nota](f5-13-shell.md), sezioni 9.2, 9.5, 10.1). Dall'albero un foglio si apre in una nuova finestra
    o a pagina intera (Esc e «Esci da pagina intera»), richiesta dell'utente; poi Ctrl+Z/Ctrl+Y, riepilogo della sezione, rinomina in linea
    e icone nell'albero, icone del catalogo, radice di composizione `ShellComposition` avviabile solo nella build UiTests
    (`--interactive-shell`). Release invariata. Domande SH33–SH37.
  - **Resto della parte (c)** (notte del 22–23/10, ramo `refactoring/f5-13-c3`, da unire; [nota](f5-13-shell.md), sezione 10.2): riepilogo della
    sezione con «Confronto e avvisi», «Riepilogo modifiche» e «Dettaglio avvisi»; albero con trascinamento e indicatore, clic singolo, testo per
    profondità e frecce leggibili in «Scuro»; «Riprendi» e schede da aggiungere trascinabili; piccoli schermi e avviso SH9; ospite dei moduli
    storici (R5: i 12 moduli storici della Release nel loro `SheetEditor` dentro la scheda); report short c.a. provato dalla shell. Release
    invariata. Domande SH38–SH47.
  - **Parte (d)** (22/10, ramo `refactoring/f5-13-d`, da unire; [nota](f5-13-shell.md), sezione 11): stato statico E5 di ponte, muri, pali,
    fondazioni e Wiki. Viste dei risultati tenute dal calcolo (`ModuleCalculation.View`) al posto di tre `ConditionalWeakTable` statiche;
    cataloghi immutabili (`ImmutableArray`, `ReadOnlyDictionary` dove conta l'ordine, `FrozenDictionary` per le sole ricerche). Baseline P3
    da 126 a 37 voci, zero nelle cinque aree salvo le due liste di sezioni dei report congelati. Prova di uscita `--paralleli` (suite
    `Presentation.Tests/paralleli`, stadio regression): 95 fixture F3 in serie e in due giri paralleli con il limitatore di istanza, bit per
    bit. Domande D-1…D-4; suite `ui` da eseguire di notte nella sezione 11.5.2.
  - **Parte (d) per la sezione c.a.** (22/10, ramo `refactoring/f5-13-d2`, da unire; [nota](f5-13-shell.md), sezione 11.7): le ultime quattro
    voci P3 della sezione c.a. diventano cataloghi immutabili. Sono `ConcreteTimeEffectsViewModel.Inputs` e, nella vista storica, invariata,
    `CommonCoefficients`, `Contours` e `AllCalculations`. Baseline P3 da 37 a 33 voci, zero nella sezione c.a. La prova `--paralleli`
    comprende le 11 fixture `str_palo`: due fogli per fixture accanto agli altri moduli, lotti della sezione contati a parte dal limitatore,
    PASS 437 in sei esecuzioni con impronte della serie identiche. Domanda D-5: la sezione calcola fuori dal limitatore.
  - **Parte (e)** (notte del 22–23/10, ramo `refactoring/f5-13-e`, da unire dopo `f5-13-c3`; [nota](f5-13-shell.md), sezione 12): le sei suite di
    `MainWindow` sul lavoro della finestra portate sulla shell nuova (`ui/shell-smoke-*` e `-mvvm`, stessi scenari e asserzioni); «Genera report» e
    «Report Word della sezione» dalla shell con `ISectionReports` (la chiamata di `MainWindow`, report invariati), report Word uguale a quello di
    `MainWindow`; cattura di un progetto dalla shell uguale a quella da `MainWindow` (`ui/shell-capture`, confronto diretto «identiche»). Otto
    differenze trovate e corrette nella shell (E1–E8). Domande SH48–SH54; nota per la guida in 12.7. Release invariata.
- [ ] F5.14 Chiusura: X.Desktop → ANTHEA.Desktop, terza revisione di AGENTS.md.
- [ ] F5.15 [CP] Ridiscutere con l'utente le voci sfavorevoli o aperte del registro delle differenze R4-R21 (R4-R14 e R16-R21; R15 è corretta) (decisione del 7/10: "le guardiamo a fine refactoring"; estesa a R16-R21 dal coordinatore su delega, ratificata dall'utente il 15/10, E4). Nello stesso punto si rivedono tutte le scelte provvisorie del coordinatore sulle voci «da decidere» di F5 (decisione dell'utente del 17/10, [decisioni](decisioni.md), 17 ottobre tarda mattina).

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
  - analisi dei bug del codice di calcolo: fatta il 15/10 con un workflow leggero nuovo (`wf_42fd8504-6ec`,
    [rapporto](analisi-bug-20261015.md)) al posto della corsa `wf_9c55965c-68b`, ferma dall'8/10; piano delle correzioni
    approvato dall'utente il 15/10, correzioni in corso per la release delle correzioni;
  - revisione delle pagine dei metodi ca.sle-tensioni e ca.fessurazione (U6);
  - revisione delle pagine dei metodi ca.dettagli, ca.ancoraggi e ca.momento-curvatura (F2.8-U1, decisione dell'utente
    dell'8/10: in coda con U6; rimessa in elenco dopo l'audit del 14/10);
  - traccia Wiki (W). Fra le voci da fare:
    - la guida pratica (riga 19) cita ancora la tendina dell'aspetto nella Wiki, tolta l'8/10;
    - la sezione «Materiali tendini e analisi di esercizio» della guida teorica, a cui rimanda il «?» accanto al tipo
      di analisi della scheda Tensioni, va completata con la scelta fra analisi lineare e non lineare (legami di
      progetto, avviso dell'8/10).
- [ ] Dopo la release delle correzioni dei bug (richiesta dell'utente della notte fra il 15 e il 16/10, «vorrei che le
  combo venissero fatte tutte in model»): tutte le combinazioni di carico generate dal generatore di Model.
  - Generatori: `StandardEN1990.CreateCombinations` e `StandardGeotechnical.Combinations`; le parti variabili favorevoli
    sono casi di carico con γQ,fav = 0.
  - Prima i muri, che usano un generatore proprio: `WallCombinations.Generate` di GPCChecker.Geotechnics, port del
    preset legacy.
  - Poi il censimento di tutti i moduli che costruiscono le combinazioni da sé e la migrazione modulo per modulo, con
    cattura prima, confronto dopo e differenze nel registro.
  - Collegato al generatore automatico delle combinazioni del modulo Modelli («Bozza del modulo Modelli»).
- [ ] Secondo anello interno del palo orizzontale, seguiti di F2-16 (decisione dell'utente del 9/10: «aggiungilo nella
  lista di cose da fare successivamente insieme alla correzione vista»):
  - [ ] anello di diametro diverso dalla prima fila: la distinta di `GPC.Checkers.Concrete.Piles.PileReinforcement`
    rifiuta i diametri misti e il calcolo elastico del palo si ferma; serve la distinta per diametro nella libreria
    (release successiva di `lib/Checker`), poi la prova `ElasticPile.UiChecks --inner-ring-only` con l'anello misto;
  - [ ] report del palo (ReportElasticPile, ReportOrizzontale) con l'anello, quando l'utente sblocca i report.
- [x] Report di calcolo: non si modificano finché l'utente non lo dice (decisione dell'8/10). Audit 14/10: dall'8/10
  30 commit hanno toccato `src/ANTHEA.Reports` e i report; 8 cambiano testo o comportamento: EXCEL-SOL `504de76e` e
  `9e1ac2ca`; Bridge Design `440db3fd`, `3ecaf795` e `d4762582`; F3.4-U5 `4a3044cb`, autorizzato dall'utente; F4.14
  `9fee7a47` e `575e2396`. Decisione dell'utente del 14/10, «Ratifico e sblocco»: le 8 modifiche restano e il blocco
  è tolto; i report si modificano con cattura prima, confronto dopo e differenze nel registro.
- [ ] Revisione dei report di calcolo, appena finito il refactoring (decisione dell'utente del 14/10, punto 4c-1: «per
  ora però prendi i report per come sono. li dovremo rivedere appena finito il refactoring»). Comprende la traccia del
  calcolo a taglio NTC della libreria (F2-2, decisione E2 del 15/10).
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
- [x] In coda dopo F4 (decisione dell'utente del 14/10 su F2-7, punto 3): indicazione all'utente quando la fessurazione si
  ferma su una zona tesa non armata («Nessuna armatura tesa», «⟨faccia⟩: area o armatura efficace assente»), oggi senza
  esito come nel legacy. Proposta dell'agente: verifica non soddisfatta con il motivo. Cambia il comportamento: si decide
  con l'utente, si implementa nella libreria e si registra come differenza voluta ([f2-7-rifiuti-sle.md](f2-7-rifiuti-sle.md)).
  **Deciso dall'utente il 15/10** («Non soddisfatta») e attuato in libreria con la release Y (registro F2-25), anche
  per le superfici interne delle sezioni cave (confermato il 15/10); i muri si allineano con E1-D4 nella release delle
  correzioni ([decisioni](decisioni.md)).
- [ ] Dopo la fine del refactoring (decisione dell'utente del 14/10, punto 2c dell'audit): Eurocodici con una norma di
  progetto, non per modulo, scritta in ogni documento; annesso nazionale come dato del profilo; profili tipizzati per
  disciplina nelle librerie (combinazioni EN 1990, geotecnica EN 1997 con DA1/DA2/DA3, sisma EN 1998, traffico EN
  1991-2, copriferri e durabilità; il calcestruzzo ha già i profili EC2); i moduli che non supportano la norma scelta
  rifiutano il calcolo; rimandi normativi dei report dal profilo; attesi indipendenti per ogni modulo e norma. Oggi solo
  sezioni c.a. e Bridge hanno una scelta della norma, gli altri moduli sono fissi su NTC (`CalculationCoefficients.cs`).
- [ ] Eventuale, fuori da questo refactoring (decisione dell'utente del 14/10): spostamenti SLE dei muri per
  sovrapposizione, quasi permanente con viscosità più incremento dei carichi variabili senza viscosità. Fino ad allora
  (1+φ) vale in tutte e tre le combinazioni SLE.

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
- [~] Modelli: `GPC.Converter.dll` è già inclusa nel set coerente e nel manifest (da R, conservata in V); non resta da aggiungere una DLL isolata. L'integrazione MIDAS e il modello normalizzato delle combinazioni sono attivi (MOD-28/MOD-30). Restano da verificare il completamento della normalizzazione del primo import rispetto al perimetro originario e da attivare il worker Straus/Strand7 con prova nativa. La predisposizione MOD-29 non è un'importazione funzionante.
- [x] Collaudo finale importazioni: profilo quick 27 PASS, nessun nuovo fallimento; 381 controlli logici, 17 WPF del pannello e 32 WPF di regressione dell'integrazione. Evidenze `20261012-120857-import-midas-straus-finale` e `20261012-121018-import-ui-regressione`.
- [x] 12 ottobre: guida pratica Rev33, capitolo 20 sull'importazione MIDAS nel modulo Modelli in prova; connessione, quattro ambiti, selezione sostitutiva dei casi, salvataggio, revisione e recupero dagli errori. Aggiornati Word, PDF e indici; Rev32 pratica archiviata con SHA-256. Confronto dei 60 fogli precedenti del corpo del PDF senza variazioni di contenuto. Evidenze `supporto/artefatti/guide_anthea_itec_rev33`.
- [~] STG-4, spostato il 15/10 dalla traccia degli stage (decisione E8: non dipende dal refactoring di F2). Collaudo sintetico completo e suite ANTHEA concluse; confronto numerico reale ancora aperto. Aggiornamento 13/10: il ponte disponibile espone tre stage e 1715 elementi; le richieste campione di forze/spostamenti non restituiscono tabelle utilizzabili. Non è quindi provata l’equivalenza dei risultati reali per fase. Evidenze midas-stage-live/20261013-112333; nessun risultato inventato. Si riprende quando ci sarà un modello MIDAS con i risultati per fase.
- [ ] SEZ-3b, spostato il 15/10 dalla traccia delle sezioni (decisione E8): sezione variabile per fase, collegata al futuro aggiornamento della geometria attiva STG-4. I dati restano conservati; la vista attuale dichiara il limite e non attribuisce al volume una sezione di fase non risolta.

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
- [x] 2. Validazioni approvate: F2.7-U5 (φ/n validati dalla API GPC, errore accanto al campo, dato derivato svuotato e recupero nelle tre SLE), F2.7-U7 (`staffe_presenti`: soltanto Sì/No, assente compatibile con il default), F2.8-U2 (rami interi 2–100 se usati, nessuna troncatura). Riutilizzati parser e validazioni esistenti. Prove ingressi/WPF, 548 file headless invariati salvo tempi, suite estesa 157808 + 673588 controlli e banco 50996 righe: soltanto cinque casi con rami −2 ora rifiutati. Catalogo testi verificato per contenuto e conteggi indipendentemente dall’ordine di prima comparsa. Registro F2-21; chiusura `20261012-215329-f2-input-chiusura` (7 PASS, nessun fallimento). *Audit 14/10:* le tre correzioni U5, U7 e F2.8-U2 sono accorpate nel solo commit `cf33c16e`, contro la regola «una per commit» di `decisioni.md` (8/10); la corsa di chiusura è su un albero non committato (`dirtyBefore` vero sul commit precedente `455a17bd`).
- [x] 3. SLE/dettagli: F2.7-U1 espone i 13 interassi NTC già usati nelle formule, lasciando invariati wk/tassi/esiti; U3 attiva la regola GPC dei getti sottili anche per UNI con appendice italiana (limiti CLS, αcc/fcd ×0,8). A10 già chiusa in 1e9998e. Registro F2-22; 548 file headless invariati salvo tempi e 50996 righe dense con sole 13 differenze dichiarate. Suite estesa: 157808 + 673640 controlli, 27 stato statico e 369 getti sottili. Corsa finale `20261012-220834-f2-sle-chiusura`, 6 PASS, nessun nuovo fallimento. *Audit 14/10:* U1 e U3 sono accorpate nel solo commit `fbedb8ca`, contro la regola «una per commit»; la corsa finale è su un albero non committato (`dirtyBefore` vero sul commit precedente `32dbac5e`).
- [x] 4. Mesh e parallelismo: rimosso il lock globale di costruzione dopo la cattura preventiva. 326 controlli (36 stati, sezioni condivise e indipendenti, analisi lineare/non lineare, viscosità), 80 mesh identiche bit per bit e 548 file headless invariati salvo tempi. Restano i lock locali del solutore e della cache grafica. Registro F2-23; chiusura `20261012-221914-f2-concorrenza-dopo`, 6 PASS e nessun fallimento. *Audit 14/10:* anche questa corsa è su un albero non committato (`dirtyBefore` vero sul commit precedente `fbedb8ca`).
- [x] 5. Collaudo del blocco F2: set T da master pushati, 2742 prove di libreria superate con sole esclusioni note; full ANTHEA e confronto finale completati, 548 file e 50996 righe invariati salvo dati volatili/identificativi, 80 mesh bit per bit. ANTHEA 1.3.0 e guide Rev37. Il confronto fixture riconosce ora le 13 differenze F2-22 già approvate, senza modificare attesi o tolleranze. [Esiti e limiti](chiusura-blocco-f2-20261012.md). Il legacy attivo resta conservato. *Audit 14/10:* il full `20261012-225933-f2-set-t-full` aveva 1 NEW-FAIL (confronto delle fixture); è stato chiuso aggiungendo regole a `tests/ANTHEA.Testing/f2-classificazione.json` nel commit di chiusura `a701af8a` e ripetendo il solo confronto (`20261012-232515-f2-set-t-fixture-final`, 3 PASS, anch'essa con `dirtyBefore` vero su `71aaa60b`), non con un nuovo full.
- [x] 5.2. Finestra autonoma «Reologia del calcestruzzo», WPF/MVVM, richiamabile dai punti pertinenti: dati del CLS e di maturazione, φ(t,t₀), φ a infinito, E(t) e fattore E(t)/E₂₈, grafici e tabella. Calcoli in GPC Model; separazione fra modulo elastico per maturazione e coefficienti di viscosità. Guide globali e PDF aggiornati; nessun report di calcolo esistente modificato.

Punto 5.2: implementato in `ANTHEA.DesignTools.Presentation` senza WPF e vista che riusa Plot/Ui.Dialog. Richiami da Strumenti, materiali e SLE; 57 controlli di stato/numerici e 21 WPF, guide Rev37 e Wiki aggiornate. [Architettura ed evidenze](f2.5-reologia.md). Model 5.1 e set T da commit pubblicati; ANTHEA 1.3.0 per la nuova funzionalità. Il collaudo full e il confronto finale sono conclusi nel punto 5.

### EXCEL-SOL — avanzamento

- [x] Contratto unico, instradamento e criterio resistente indipendente; EXCEL-SOL-01, controlli prima/dopo PASS.
- [x] Nuovo Excel, editor MVVM, conversione esplicita e report; EXCEL-SOL-02, 1229 controlli Application e suite WPF passati.
- [x] Guide Rev41, indici e Wiki aggiornati; Word/PDF controllati su 222 pagine, 337 formule conservate. Rev40 archiviata con impronte. Versione applicazione 1.7.0 per la nuova funzionalità; DLL GPC invariate.
- [x] Collaudo standard e quattro suite WPF conclusi: 71 esiti finali PASS, con ripresa del solo controllo sul registro. Installer 1.7.0 da sorgente pulito e pubblicato 68330851; dieci DLL identiche al manifest. [Verbale e limiti](chiusura-excel-sol-20261013.md). F4 ferma *(storico, superato dalla ripresa del 13/10)*.

- [x] EXCEL-SOL-UI: contenitore opaco e spaziatura della finestra verificati anche in esportazione immagine; suite WPF dedicata PASS (20261013-144358-excel-sol-window-final).

- [x] EXCEL-SOL-UX: percorso Excel storico proposto nel nuovo editor, messaggi coerenti con sostituzione/aggiunta; avviso di migrazione aggiornato anche quando si elimina l’ultima riga storica.

- [x] EXCEL-SOL-REG: corretto il posizionamento delle voci 01/02 nel registro, intercettato dalla suite architetturale. Il collaudo standard ha 66 PASS e questo solo errore documentale; quattro suite WPF PASS. Nessun atteso o tolleranza modificato.

Le voci F4 del 14/10 scritte in passato sotto questa sezione sono state spostate, senza togliere testo, nelle voci corrispondenti della sezione «Fase 4» (audit del 14/10).

## Paratie — incarico del 18 ottobre 2026

Base esplicita F5 `b9c2d4b1`, ramo isolato `refactoring/f5-paratie`. Registro
PARATIE-01; [matrice completa](../paratie/requisiti.md).

- [x] Censimento fonti e librerie; matrice con funzionalità avanzate e dipendenze esterne.
- [x] Primo incremento numerico: trave, molle elastoplastiche, fasi, sostegni e Concrete; 14 prove GPC dedicate.
- [x] Primo percorso applicativo MVVM, persistenza dei risultati, diagrammi e report; 28 controlli logici e 7 WPF.
- [x] Set candidato locale coerente `candidate-04`, validato in staging `snapshot-04`; librerie distribuite invariate.
- [ ] Completare le righe aperte della matrice, guide globali, collaudi complessivi e rilascio ufficiale autorizzato.

I limiti e le evidenze sono nel [verbale di incremento](../paratie/incremento-01.md).
Nessun motore legacy spostato o rimosso; nessun atteso indipendente modificato.

- [x] PARATIE-02: chiusi i tre rilievi di integrazione del primo standard con riesecuzioni mirate del runner; attesi invariati, provenienza della libreria distinta dal contenuto numerico. Standard completo successivo ancora da eseguire.

- [x] PARATIE-03: filtrazione FEM 2D stazionaria, trasferimento delle pressioni, schema 2, vista WPF e report. 23 test Geotechnics (12 idraulici), 43 controlli applicativi/MVVM, 10 WPF e architettura PASS. Set locale `candidate-05`/`snapshot-05`; [evidenze e limiti](../paratie/incremento-02.md). PAR-14 resta in corso per le estensioni dichiarate.
- [x] PARATIE-04: corretto il solo driver logico della traccia elastica per allinearne l'attesa a quella WPF. Standard 73 PASS/1 rilievo; riesecuzione mirata Presentation PASS. Corsa standard segnalata con sorgente modificato: collaudo finale completo ancora aperto. Attesi e produzione invariati.
- [x] PARATIE-05: verifiche locali UPL/HYD, campionamento FEM, criterio dei pesi da pressioni, schema 3, editor e report. 30 test Geotechnics, 62 controlli applicativi, 14 WPF; set candidato 3095 PASS e soli 17 fallimenti Steel già ammessi. [Evidenze e limiti](../paratie/incremento-03.md). Merge paratie/spettri autorizzati a lavoro concluso, non ancora eseguiti.
- [x] PARATIE-06: stabilità globale per parete continua e fase, riuso Bishop con pressioni/pesi FEM; schema 4, azioni caratteristiche, risultati e report. 39 test Geotechnics, 80 controlli applicativi e 19 WPF PASS; cattura Slopes identica. Set candidato 3104 PASS, soli 17 fallimenti ammessi. Regressione standard sul commit 212ce340: 74 PASS, nessun nuovo fallimento. [Evidenze e limiti](../paratie/incremento-04.md). Estensioni a sostegni/geometrie, guide e collaudo complessivo restano aperti.
- [x] PARATIE-07: sostegni circolari pieni/CHS assiali in Checker Steel, trasferimento Design per fase, schema 5, editor/esiti/report/CSV. Corretto il segno verticale dei puntoni inclinati con cattura prima/dopo. Nove test Steel, 43 Geotechnics, 103 applicativi/MVVM e 23 WPF PASS; candidato 3119 PASS e soli 17 fallimenti storici ammessi. [Evidenze e limiti](../paratie/incremento-05.md). N–M–V delle sezioni della parete e altre famiglie restano aperti.
- [x] Regressione PARATIE-07 su 03afcfa4 con snapshot-12: profilo standard 74 PASS, nessun nuovo fallimento, sorgenti invariati durante la corsa. Rilascio e matrice complessiva ancora aperti.
- [x] PARATIE-08: sfilamento dei tiranti, pressioni pl/pi distinte, profili NTC per indagini e prove, trasferimento per fase; schema 6, editor/esiti/report/CSV. 50 test Geotechnics, due nuovi Design, 128 controlli applicativi e 27 WPF PASS; candidato 3128 PASS, soli 17 fallimenti storici Steel ammessi. [Evidenze e limiti](../paratie/incremento-06.md). PAR-11 e il rilievo storico pl/pi dei micropali restano aperti.
- [x] Regressione PARATIE-08 su 03da426c/snapshot-13: standard 74 PASS, 0 NEW-FAIL, sorgenti ANTHEA invariati durante tutta la corsa.
- [x] PARATIE-09: sezioni tubolari CHS classe 1–2 N–M–V in Checker Steel, EI e azioni per interasse in Design; schema 7, editor/esiti/report/CSV. 13 test Steel, due Design, 303 controlli applicativi e 31 WPF PASS; candidato 3134 PASS, stessi 17 fallimenti storici ammessi. [Evidenze e limiti](../paratie/incremento-07.md). Altre famiglie/classi, instabilità della membratura e collaudo complessivo restano aperti.
- [x] Regressione PARATIE-09 su f52db8d8/snapshot-14: standard 74 PASS, 0 NEW-FAIL, sorgenti ANTHEA invariati durante tutta la corsa.
- [ ] Richiesta in coda del 19/10, confermata: approfondimento della testa di pali e micropali rispetto al piano di campagna, tensioni litostatiche comprensive del ricoprimento nelle verifiche verticali/orizzontali. [Interpretazione e accettazione](../paratie/seguito-pali-interrati.md). Da implementare dopo paratie, prima dell'integrazione finale.
- [x] PARATIE-10: sezioni circolari in c.a., SLU/SLE in Checker Concrete e dettagli separati; EI e trasferimento in Design. Schema 8, editor, persistenza, report/CSV. Sei nuovi test Design, 483 controlli applicativi e 35 WPF PASS; candidato 3140 PASS, stessi 17 fallimenti storici Steel ammessi. [Evidenze e limiti](../paratie/incremento-08.md). Progetto automatico, matrice e rilascio restano aperti.
- [x] Regressione PARATIE-10 su 6a84c4e9/snapshot-15: standard con stadio UI filtrato, 94 PASS e nessun nuovo fallimento. Corretto un falso positivo del driver WPF booleano (raccolta vuota); riesecuzione dedicata con 35 controlli PASS e schermata verificata, produzione e attesi numerici invariati.
- [x] PARATIE-11: carichi distribuiti lineari per intervalli e fasi, integrazione consistente e trasferimento di forza/coppia alla stabilità globale. Schema 9, editor scorrevole, diagramma q netto, persistenza, CSV e report. 60 test Geotechnics, 1206 applicativi/MVVM, 39 WPF e architettura PASS; candidato 3150 PASS, stessi 17 fallimenti storici ammessi. Cattura numerica preesistente identica. [Evidenze e limiti](../paratie/incremento-09.md). Regressione standard su 8db29979/snapshot-17: 74 PASS, 0 NEW-FAIL, sorgenti invariati. Diffusione dei carichi sul terreno e matrice completa ancora aperte.

- [x] PARATIE-12: guide globali Rev48 e aiuto contestuale; 245 pagine rasterizzate e controllate, 365 formule verificate, 16 file precedenti conservati con SHA-256. Runner filtrato: 9 PASS, nessun nuovo fallimento; 541 controlli architetturali, 3206 di presentazione, 40 WPF e 12 del manuale. Estensioni successive della matrice ancora aperte. [Evidenze e limiti](../paratie/incremento-10.md).

- [x] PARATIE-13: ricerca discreta dell'infissione in GPC, con geometrie e storie indipendenti, ricalcolo FEM, aggregazione dei verificatori e distinzione fra minimo dimostrato e candidato accettabile. Schema 10, editor, persistenza, annullamento, report e CSV; 10 nuovi test GPC, 1279 controlli applicativi e 46 WPF PASS. Set candidato: 3160 PASS, stessi 17 fallimenti Steel storici. Guide Rev49 finalizzate e controllate: 247 pagine, 370 formule, archivio Rev48 con 16 file identici. Collaudo consolidato su 6e4b47f0/snapshot-18: 93 PASS, un falso allarme del censimento Wiki corretto preservando le 73 identità F5; riesecuzione mirata quattro suite e 331 controlli PASS. Combinazioni multiple nel foglio e criteri ulteriori restano aperti. [Evidenze e limiti](../paratie/incremento-11.md).

- [x] PARATIE-14: campi recuperati e inviluppi GPC con provenienza e azioni concomitanti; otto grandezze nel foglio, diagrammi, persistenza, CSV/report. Sette nuovi test GPC, 1305 controlli applicativi e 52 WPF PASS; candidato 3167 PASS con soli 17 fallimenti Steel storici. Catture numeriche e 5099 righe CSV precedenti invariate esclusa impronta. Guide Rev50: 249 pagine e 373 formule controllate; 16 originali Rev49 archiviati identici. Multi-caso nel foglio e matrice completa restano aperti. [Evidenze e limiti](../paratie/incremento-12.md).

- [x] Regressione PARATIE-14 su 84cee634/snapshot-19: profilo standard, 74 PASS e 0 NEW-FAIL, sorgenti invariati durante tutta la corsa.

- [x] PARATIE-15: schema 11 e storie assegnate indipendenti, verifiche/FEM per caso, ricerca con minimo comune, selettore grafico, persistenza e report. 1419 controlli applicativi e 58 WPF PASS; 5099 righe precedenti invariate. Guide Rev51: 251 pagine e 373 formule conservate. Otto esempi apribili; candidata locale da confezionare dopo la regressione standard. [Evidenze e limiti](../paratie/incremento-13.md). Combinazioni automatiche, matrice e rilascio ufficiale ancora aperti.

- [x] Regressione PARATIE-15 su 6425927e/snapshot-19: standard completo, 74 PASS e 0 NEW-FAIL, revisione pulita e sorgenti invariati. Corretto poi il solo titolo della ricerca multi-caso, con prova WPF dedicata; build e collaudo finale della vista precedono il confezionamento locale.

- [x] Prima candidata locale paratie su 2f60b336: build e collaudo finale puliti, 6 suite PASS con 59 controlli WPF; `supporto/artefatti/paratie/candidata-01` contiene otto esempi e guide Rev51. Audit indipendente: 133 file e 13 DLL verificati per SHA-256. Standard riferito a 6425927e; differenze successive e revisione della vista registrate nel manifest. Pacchetto pronto per la prova utente; matrice completa, pali interrati, rilascio e merge finali ancora aperti.

## Integrazione F5 richiesta il 19 ottobre 2026

- [x] Autorizzazione aggiornata: integrare ora i soli rami paratie/spettri sviluppati per l'incarico, comprese le librerie; lasciare separati i rami di prova e storici. Model e Checker confluiti in master, versioni del set integrate; nessun push.
- [x] Risolti i punti condivisi ANTHEA conservando entrambi i moduli, i cataloghi precedenti e gli attesi indipendenti. Riuniti i capitoli delle due guide globali in Rev52; 32 originali archiviati con impronte.
- [x] Set GPC integrato collaudato: 3172 PASS, soli 17 fallimenti Steel storici ammessi; staging coerente pronto. Guide Rev52 finalizzate e controllate visivamente: 257 pagine, 387 formule e dieci pagine degli indici.
- [x] Cartella principale riportata su `refactoring/f5`; corretto il difetto di resa del vettore di Darcy nella Wiki. Riesecuzione mirata su 48405d30: dieci suite PASS, nessun nuovo fallimento. Archivi protetti dalle conversioni dei fine riga, 96 originali verificati contro gli SHA preesistenti.
- [x] Candidata integrata su `0f989159`: standard completo 75 PASS e UI finale dieci suite PASS sulla stessa revisione pulita e con lo stesso manifest GPC. Corretto il solo driver di provenienza F3/F5, senza cambiare attesi o transizioni. `supporto/artefatti/paratie/candidata-integrata-01` contiene nove esempi, guide Rev52 e 13 DLL coerenti; audit indipendente dei 135 file superato. Cartella principale su `refactoring/f5`, snapshot ufficiale invariato e push ancora da autorizzare.
- [x] Ripresa l'implementazione in `refactoring/f5-paratie-seguito`, con Model/Checker in `codex/paratie-seguito`, dalle revisioni integrate. Primo kernel locale del fondo scavo collaudato nelle librerie: 74 EmbeddedWalls PASS, runner completi Model e Checker conclusi; integrazione nel foglio ancora aperta. Il kernel nuovo non entra nella candidata consegnata. [Incremento 14](../paratie/incremento-14.md). La matrice completa e i pali interrati restano aperti.

Revisioni, conflitti e collaudi nel [verbale di integrazione](../paratie/integrazione-rami.md).

## Rilascio F5 autorizzato il 19 ottobre 2026

- [x] Pubblicati soltanto Model/master `e1e6e107` e Checker/master `dfb808c8`, con verifica delle revisioni remote.
- [x] Release-Set non candidato, controllo versioni rispetto a Z e suite complete: 3172 PASS, stessi 17 fallimenti Steel ammessi. Installate tutte le 13 DLL ANTHEA con Update-Snapshot; set di 14 DLL identico alla candidata, Delaunay/Geometry/Utilities invariate. Aggiornati README e sola provenienza del rilascio, senza attesi numerici nuovi.
- [x] Eseguito tutto il full su e39741c9: 160 PASS e cinque rilievi; corretti e ricollaudati i due confronti F3/fondazioni. Le due prove interrotte da Trend Micro e il confronto a 96 DPI restano aperti con autorizzazione esplicita. Esiti originali conservati, nessuna lista di fallimenti ammessi modificata.

[Verbale del rilascio](../paratie/rilascio-f5.md). ANTHEA principale resta su F5;
nuovi rami di prosecuzione locali. Pubblicazione della 1.11.0 autorizzata.

- [x] F5-LIB-02: distinti i metadati F3/F5 nel driver WPF delle fondazioni; registrate separatamente 122 differenze editoriali Wiki esatte, con 30 ricerche storiche conservate. Testing.Tests: 13 controlli delle transizioni PASS, inclusi rifiuti di variazioni numeriche/non editoriali. Attesi numerici e transizioni F4 congelati. Collaudo WPF finale su f82d7ae3 PASS: 104 fixture F3 e 8198 righe delle fondazioni invariate.
- [x] Predisposta ANTHEA 1.11.0 per le nuove funzioni paratie/spettri; istruzioni installer aggiornate e PDF di tre pagine controllato. I due originali precedenti sono conservati con SHA-256. Commit/push di F5 e creazione installer autorizzati dall'utente, subordinati al collaudo finale.
- [x] F5-LIB-03: collaudo mirato concluso su f82d7ae3: 18 PASS, più due suite installer rieseguite PASS dopo la diagnosi del percorso moduli PowerShell. Installer 1.11.0 creato, 325 file applicativi estratti e tre PDF verificati; tutte le 13 DLL identiche al set AA. Nove esempi apribili e ZIP con impronte. [Verbale](installer-1.11.0.md). Matrice completa e pali interrati ancora aperti.
- [ ] Dopo la verifica dell'amministratore: ripetere `ui/check-palo-verticale-view` e `ui/check-muri-view`; ripetere `ui/check-contrast` nell'ambiente a 96 DPI, senza modificare la baseline.

## Interfaccia paratie richiesta il 20 ottobre 2026

- [x] PARATIE-17: disegno degli input ed editor affiancati in Modello, navigazione per caso/fase, selezione grafica e aggiunta contestuale delle righe. Modifiche di scavo e falda visibili senza cambiare scheda. Collaudati reload, fasi omonime, input geometrici invalidi, tema scuro e finestra compatta; invariati nove JSON numerici. [Dettaglio](../paratie/interfaccia-grafica.md).
- [x] Guide globali Rev53 aggiornate: 260 pagine PDF, 387 formule modificabili conservate, indici e Wiki rigenerati. Controllo visivo delle pagine variate e confronto delle restanti; 18 originali Rev52 conservati con SHA-256.
- [x] Collaudo finale su `b60d5aa4`, albero pulito e invariato: 13 suite mirate PASS, 3433 controlli Presentation, 557 architetturali, 340 Wiki e 3439 Wiki WPF, 69 WPF paratie. Nove JSON numerici e tutte le 13 DLL del set AA identici. Build Release locale ed esempio grafico pronti; nessun nuovo installer o push.

## Sezioni c.a.: persistenza e wizard, richiesta del 20 ottobre 2026

- [x] Persistenza nativa GPC con ricetta editabile, bozze e migrazione dei file precedenti/revisioni storiche. Sette confronti numerici esatti; Application 2870 controlli, Presentation e adattatore rapido PASS. Corretti e ricollaudati i tre rilievi architetturali senza deroghe. [Contratto ed evidenze](sezioni-persistenza.md).
- [x] Wizard delle dieci tipologie richieste, compresi doppi strati/anelli, capsula e canale aperto a C; aggiunti trapezio e poligono regolare. Generatori in GPC.Design, anteprima MVVM e staffe esplicite. Per le tipologie avanzate i parametri efficaci del taglio sono manuali e azzerati alla conferma. [Implementazione, limiti ed evidenze](sezioni-wizard.md).
- [x] Collaudo consolidato: standard 72 PASS e tre confronti documentali corretti e rieseguiti; successiva esecuzione 11 PASS, Application 3045 e WPF 82 controlli. Dodici confronti esatti delle tensioni prima/dopo il reload; guide globali Rev54, 262 pagine PDF, Word e indici controllati, 18 originali archiviati.
- [x] Chiusura su revisione pulita: Reports 328 e Wiki 340 controlli; ulteriori confronti WPF adeguati al nuovo formato e quattro smoke superati su `0fa316d3` (generale, gerarchia, riordino, condivisione), compresi 204 controlli del workspace c.a. Nessun nuovo fallimento ammesso.
- [x] Distribuzione delle librerie autorizzata con la richiesta successiva della 1.12.0: Checker `894892c1` integrato e pushato su master; set ufficiale AB installato dopo tutte le suite Model/Checker (3176 PASS, stessi 17 fallimenti Steel censiti). Collaudo ANTHEA full nella chiusura del rilascio.

## Rilascio 1.11.1 richiesto il 20 ottobre 2026

- [x] Versione applicativa 1.11.1 assegnata come richiesto dall'utente, con vista grafica paratie e guide Rev53; set GPC AA invariato. Istruzioni installer aggiornate, originali archiviati con SHA-256.
- [x] Ricompilazione e collaudi su `5f85ea81`, pulito e invariato: standard completo con installer 77 PASS; WPF mirate 5 PASS. Presentation 3433, Spettri 1566, paratie WPF 69; nove JSON numerici invariati.
- [x] Installer self-contained 1.11.1 creato: 325 file applicativi, 13 DLL AA e tre PDF Rev53 verificati per SHA-256; dieci esempi nello ZIP. [Verbale e impronte](installer-1.11.1.md). Tag della build `v1.11.1`; integrazione e pubblicazione in F5 autorizzate. Le tre prove ambientali precedenti e la matrice funzionale restano aperte.

## Rilascio 1.12.0 richiesto il 20 ottobre 2026

- [x] Versione 1.12.0 assegnata su richiesta dell'utente, comprendente persistenza nativa GPC, dodici wizard delle sezioni e guide Rev54; istruzioni installer e PDF aggiornati, predecessori archiviati con SHA-256.
- [x] Autorizzazione esplicita ricevuta per Checker/master, ANTHEA/refactoring/f5 e tag v1.12.0. Checker pubblicato, set AB non candidato prodotto e installato; 14 DLL identiche al candidato collaudato, 13 distribuite in ANTHEA.
- [x] Catalogo unico di 52 esempi per 15 moduli, organizzati in sottocartelle e pagina MVVM Esempi. Apertura come copia indipendente, anche da Explorer; componente NSIS facoltativo selezionato inizialmente. 249 controlli del catalogo e 37 calcoli completi PASS, WPF 20 e installer 120; guide Rev55 controllate, 263 pagine e 387 formule conservate. [Dettaglio](esempi.md).
- [ ] Collaudo completo, creazione e audit del setup self-contained e degli esempi; tag della revisione esatta della build. Nessun esito della 1.11.1 viene esteso automaticamente a questo rilascio.

- [x] F5-REL-112-01: completato il full (157 PASS, 15 rilievi); isolate e corrette impaginazione dei wizard, transizioni Wiki, due confronti DPI e stato dei campi foro. Ripetizioni concluse: 171 PASS consolidati, solo il blocco Trend Micro del palo verticale rimane aperto. Setup e audit del payload completati da 810b62a6; pubblicazione con riserva subordinata a deroga esplicita. [Verbale](installer-1.12.0.md).
- [x] Deroga puntuale autorizzata dall'utente il 20 ottobre: pubblicati `refactoring/f5` a 9b242761 e tag annotato `v1.12.0` a 810b62a6, con riserva sul test interrotto da Trend Micro. Installer e tag identificano la stessa build.

## Albero Paratie e capsula come pilastro

- [x] Albero di lavoro MVVM richiudibile: gruppi, navigazione verso editor/caso/fase, aggiornamento delle righe e riferimenti ambigui visibili. 82 controlli aggiunti in Presentation; WPF Paratie 79 PASS. [Dettaglio](paratie-albero.md).
- [x] Capsula: armature distinte per archi e rettilinei, compatibilità dei documenti precedenti e dettagli generali del pilastro tramite GPC. Cattura del rifiuto precedente, nove geometrie bit-identiche; 3181 PASS nelle librerie, Application 3057, Presentation 3523 e wizard WPF 84. [Dettaglio](capsula-pilastro.md).
- [x] Guide Rev56 finalizzate e controllate: 264 pagine, 387 formule conservate; 18 originali archiviati. Esempi: 301.420 valori numerici identici e sole versioni/impronte differenti, riprodotte separatamente. Reports 475, Examples 249 e Wiki PASS. Registro F3 editoriale aggiornato dopo diagnosi puntuale; 30 ricerche identiche.
- [x] Installer candidato locale 1.12.1 completato: 382 file verificati per SHA-256, 13 DLL GPC AC, 52 esempi e guide Rev56. F3 senza differenze residue; collaudo conclusivo cinque suite PASS, Reports 476, sicurezza NSIS 26, guide 30 ed esempi 120 controlli. PDF della capsula verificato anche visivamente. [Verbale](installer-1.12.1.md).
- [ ] Rilascio ufficiale 1.12.1 differito per scelta dell'utente: «Mantieni per ora il commit solo in locale». Checker 8bf4af33 e ANTHEA restano locali; snapshot ufficiale AB invariato. Nessun push o tag nuovo. Il profilo full sulla nuova candidata non è stato eseguito.
- [x] Successiva richiesta di merge dei branch ANTHEA: integrati in `refactoring/f5` i cinque commit di `refactoring/f5-paratie-capsula`, da 9b242761 a b795a550, mediante fast-forward senza conflitti. Tutti gli altri rami di sviluppo erano già inclusi; esclusi i due rami sperimentali `prova/*`. Collaudo sul ramo integrato con set candidato AC: sette suite PASS, Architecture 562, Application 3057 e Presentation 3523 controlli. [Verbale e raccordo con le librerie](merge-f5-sviluppo-20261020.md).
