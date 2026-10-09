# DLL Checker

**Procedura di aggiornamento.** Le DLL di questa cartella si producono solo con `tools/libs/Update-Snapshot.ps1
-FromUpstream`, cioè dai commit pushati delle librerie (AGENTS.md).

1. Nelle librerie (Utilities, Geometry, Model, Checker): commit con le versioni nuove e push. Ogni DLL ricompilata da
   un commit nuovo cambia SHA-256 anche a sorgente invariato (SourceLink scrive il commit nel PDB e, con l'id del PDB,
   nella DLL), quindi richiede una versione più alta (almeno la revisione, ultima cifra). Poi `git fetch` nei checkout
   locali, perché lo script legge i rami remoti all'ultimo fetch.
2. Dal checkout di ANTHEA: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\libs\Update-Snapshot.ps1
   -FromUpstream [-Staging <cartella>] [-Install]` (staging predefinito: `supporto/artefatti/lib-staging/<data-ora>`).
   Per ciascun repository lo script risolve il commit del ramo remoto tracciato dal ramo estratto (`@{u}`), lo estrae
   con `git worktree add --detach` in `<TEMP>\gpc-snapshot\<Repo>` (worktree fratelli, quindi HintPath e
   ProjectReference fra repository funzionano), usa i binari versionati di Utilities e Geometry, compila Model e Checker
   in Release con l'SDK di global.json, controlla che nessun worktree differisca dal suo commit dopo la build, scrive
   nello staging le 9 DLL (dalla S4 anche `GPC.Model.Persistence`), `GPCChecker.Geotechnics.xml`, `manifest.json` (commit e ramo remoti, `pushed: true`,
   `fromUpstream`, `buildRoot`) e `manifest.props`, poi rimuove i soli worktree creati (`git worktree remove`, `git
   worktree prune`) e la radice, anche dopo un errore. Lo stato dei checkout locali non conta (commit locali, merge in
   corso, modifiche). Da un worktree di ANTHEA che non sta accanto alle librerie: `-Repos <cartella dei repository>` e
   `-Lib <lib\Checker di riferimento>`.
3. Controllo di versione: una DLL con SHA-256 diverso da quello del manifest in `-Lib` (predefinito: questa cartella)
   deve avere una versione assembly più alta, altrimenti lo script si ferma. `-Install` copia in `-Lib` DLL, xml,
   `manifest.json` e `manifest.props`; senza `-Install` si copia a mano dallo staging.
4. Verifiche: `build\ci.ps1 -Profile full`, profilo baseline contro i riferimenti, impronta delle mesh DelaunayMesh, test
   delle librerie; voce in questo README.

Dal 9 ottobre 2026 il ramo principale di Checker è master (non più develop). Model 4 compila solo dal bundle fissato in
`build/dependencies.props`: lo script lo prepara nel worktree da `-ModelDependencies <cartella>` (predefinito: la cache
`.dependencies` del checkout Model in `-Repos`), validando ogni file per SHA-256, e si ferma se la Geometry o la
Utilities fissate da Model non sono quelle dello snapshot. `-At Repo=commit` compila un commit già pushato più vecchio
della testa del ramo remoto. Gmsh (GMsh.Net, UnsafeEx, gmsh-*.dll, GMesh) non entra mai in questa cartella: solo negli
unit test.

**Release S4 (9 ottobre 2026, librerie Model 4).** Compilata dai commit pushati con `tools/libs/Update-Snapshot.ps1
-FromUpstream -At Geometry=1f901ef -ModelDependencies <cache verificata> -Repos <radice> -Install` (staging in
`supporto/artefatti/lib-staging/20261009-225939`; controllo di versione contro S3). Commit sorgente: Utilities
origin/master c0466b0, Geometry 1f901ef (release 2.1.0.5, raggiungibile da origin/master a420e0e, che porta già la 2.2:
Model è fissato e verificato sulla 2.1.0.5), Model origin/master a324af3f, Checker origin/master 5fb6cdff; SDK 9.0.318;
`pushed: true`; radice di build `C:\Users\g.pacini\AppData\Local\Temp\gpc-snapshot`.

- Versioni: GPCGeometry 2.1.0.4 → 2.1.0.5, DelaunayMesh 2.0.0.11 → 2.0.0.12, GPCModel 1.6.1.1 → 4.0.1.0, GPCModelData
  0.0.2.3 → 0.0.2.5, GPCChecker.Concrete 0.0.18.0 → 0.0.25.0, GPCChecker.Geotechnics 0.1.1.2 → 0.1.1.4,
  GPCChecker.CompositeBridge 1.4.0.5 → 1.4.0.7; GPCUtilities 2.0.0.8 invariata (stesso SHA-256).
- Nuova: GPC.Model.Persistence 2.0.1.0 (Model a324af3f). In Model 4 `ModelArchive` sta in questo assembly; la usano
  solo gli strumenti di cattura (`supporto/test/CheckerMigration.Capture`, elenco `GpcLibraries` esplicito). Non è
  nell'elenco `all` di `Directory.Build.targets`, quindi non entra nell'applicazione né nell'installer.
- Model 4: namespace riorganizzati (guida `MIGRAZIONE_MODEL_4.txt` nel repository Model); ANTHEA compila senza
  modifiche ai sorgenti. Concrete 0.0.25.0: numero delle tensioni delle barre controllato al punto d'uso con
  ValidateAtUse (prova generale F2.7), risposte numeriche separate dalla verifica delle tensioni (8c45ba30, messaggio
  del caso senza piano di deformazione cambiato, contratto K0 aggiornato su decisione dell'utente). Geometry 2.1.0.5:
  hash e confronti di punti e vettori, mesh, poligoni; mesh usuali identiche (contratto K0 di Checker, 112027 righe).
- SHA-256 (per intero nel manifest): GPCGeometry 4498B921…, DelaunayMesh 1ACE7E6F…, GPCModel 46EB0FF8…,
  GPCModelData 5B091233…, GPCChecker.Concrete B6925BE8…, GPCChecker.Geotechnics C3EA33AF…, GPCChecker.CompositeBridge
  E9C72307…, GPC.Model.Persistence 4EE43C70…; GPCUtilities 638FF722… come in S3. Due corse dagli stessi commit hanno
  dato le stesse otto DLL comuni (stesso SHA-256).

Riproducibilità: lo SHA-256 dipende dal commit e anche dal percorso di build, perché la DLL contiene il percorso
completo del suo PDB (`<progetto>\obj\Release\netstandard2.0\<nome>.pdb`). Per questo la radice dei worktree è fissa
(`gpc-snapshot` nella cartella temporanea dell'utente in forma lunga, registrata in `buildRoot`): due corse dagli stessi
commit danno DLL identiche, mentre lo stesso commit compilato nel checkout principale dà SHA-256 diversi. Una sola corsa
`-FromUpstream` alla volta; una radice lasciata da una corsa interrotta si riconosce dal file `.update-snapshot-root` e
viene rimossa all'inizio. Senza `-FromUpstream` lo script compila il HEAD locale dei checkout (alberi puliti, nessun
commit o merge durante la corsa): serve per prove, non per questa cartella.

**Release S3 (8 ottobre 2026, refactoring F2.7-F2.9 e S-1).** Compilata dai commit pushati con
`tools/libs/Update-Snapshot.ps1 -FromUpstream -Install -Repos <radice>` (staging in
`supporto/artefatti/lib-staging/20261008-174751`; controllo di versione contro S2). `-Repos` indica una radice di
worktree con Model su master: il checkout principale di Model era su un altro branch, che `-FromUpstream` avrebbe
seguito. Commit sorgente, rami remoti: Utilities origin/master df3b3e7, Geometry origin/master 6a0d1c1, Model
origin/master 5ad56681 (come S2), Checker origin/develop 0ad8314b; SDK 9.0.318; `pushed: true` per tutte le DLL;
radice di build `C:\Users\g.pacini\AppData\Local\Temp\gpc-snapshot`.

- Versioni: GPCChecker.Concrete 0.0.17.0 → 0.0.18.0, GPCChecker.Geotechnics 0.1.1.1 → 0.1.1.2 e
  GPCChecker.CompositeBridge 1.4.0.4 → 1.4.0.5 (sorgente invariato, SourceLink). GPCUtilities, GPCGeometry,
  DelaunayMesh, GPCModel e GPCModelData invariate (stesso SHA-256 di S2).
- Concrete 0.0.18.0, solo aggiunte con i comportamenti predefiniti della 0.0.17.0 (contratti K0, L0, CD0 e del punto
  del dominio identici): F2.7 SLE e fessurazione (traccia, codici dei rifiuti, profili, getti sottili,
  omogeneizzazione); F2.8 dettagli di solette e pareti, aderenza, momento-curvatura con unità del chiamante; F2.9
  catalogo delle esposizioni di sola lettura e citazioni della UNI 11104; S-1 `DomainPointAxialTolerance`
  (tolleranza su N dei punti del dominio per legame e scala della sezione: corregge il palo orizzontale con D ≥ 1,6 m
  quando ANTHEA la usa; con lo stress block risultati SLU meno precisi).
- Prove: Concrete 568/568, Geotechnics 98, CompositeBridge 254, BridgeAudit 302, ModelChecker.Tests 106 (con il
  commit M1 di Model, 6005d180, solo test).
- SHA-256 (per intero nel manifest): GPCChecker.Concrete F803B3CA…, GPCChecker.Geotechnics 4AB0C7AE…,
  GPCChecker.CompositeBridge 58DC9529…; le altre come in S2.

**Release S2 (7 ottobre 2026, refactoring F2.4).** Compilata dai commit pushati con `tools/libs/Update-Snapshot.ps1
-FromUpstream -Install` (staging in `supporto/artefatti/lib-staging/S2-upstream`; controllo di versione contro S1,
cioè contro il manifest di main 17b6c98). Commit sorgente, rami remoti: Utilities origin/master df3b3e7 e Geometry
origin/master 6a0d1c1 (binari committati, gli stessi di S1: dopo 75182cc Geometry cambia solo un test), Model
origin/master 5ad56681, Checker origin/develop 0d7ba50b; SDK 9.0.318; `pushed: true` per tutte le DLL; radice di build
`C:\Users\g.pacini\AppData\Local\Temp\gpc-snapshot`.

- Versioni: GPCChecker.Concrete 0.0.15.0 → 0.0.17.0, GPCModel 1.6.1.0 → 1.6.1.1, GPCModelData 0.0.2.2 → 0.0.2.3,
  GPCChecker.Geotechnics 0.1.1.0 → 0.1.1.1, GPCChecker.CompositeBridge 1.4.0.3 → 1.4.0.4. GPCUtilities 2.0.0.8,
  GPCGeometry 2.1.0.4 e DelaunayMesh 2.0.0.11 invariate (stesso SHA-256).
- Concrete 0.0.17.0 porta nella libreria le regole di fessurazione già applicate da ANTHEA: D7-b (k2 = 0,5 con l'asse
  neutro interno alla sezione, nessun k2 nella sezione interamente compressa; opzione legacy `NtcK2FromCompressedBars`),
  R15 (h − x delle fasce interne dei fori = min[εmax/|∇ε|; altezza della sezione lungo il gradiente]) e le citazioni
  R3 (EN 1992-1-1 7.3.4(3), eq. (7.14)).
- GPCModel, GPCModelData, Geotechnics e CompositeBridge hanno sorgente invariato rispetto a S1 (Model 5ba61a04,
  Checker b994e188): SourceLink scrive il commit nella DLL, quindi ogni commit nuovo cambia lo SHA-256 e richiede una
  versione più alta; è stata alzata la revisione (ultima cifra, convenzione dei repository). Cambiano anche i
  riferimenti nei metadati: GPCModelData, Concrete, Geotechnics e CompositeBridge referenziano GPCModel 1.6.1.1,
  CompositeBridge referenzia Concrete 0.0.17.0.
- SHA-256 (per intero nel manifest): GPCModel 3C6D5788…, GPCModelData EA40DB14…, GPCChecker.Concrete E724B959…,
  GPCChecker.Geotechnics A72C43FC…, GPCChecker.CompositeBridge F9A59096…; GPCUtilities 638FF722…, GPCGeometry
  1D43F568… e DelaunayMesh 24B24DC1… come in S1.
- Sostituisce il candidato del pomeriggio (f6b6fdf), compilato nel checkout principale con il modo predefinito da
  Model 5ad56681 e da Checker develop 1fbaea61, locale (merge delle pagine dei metodi c.a. sopra 0d7ba50b, solo
  `docs/metodi`). Stesso sorgente delle DLL e stesse versioni; cambiano gli SHA-256 delle cinque DLL ricompilate, per
  il commit di Checker scritto da SourceLink e per il percorso di build: GPCModel, dallo stesso commit Model 5ad56681, è
  936FDE1A… nel checkout principale e 3C6D5788… nella radice temporanea. Una prima build del candidato era già stata
  scartata perché partita durante i merge delle pagine dei metodi su Checker develop (il manifest registrava 0d7ba50b
  per DLL compilate da 1fbaea61): con `-FromUpstream` il HEAD dei checkout non entra più nella build.
- Riproducibilità: quattro corse `-FromUpstream` dagli stessi commit, una delle quali partita da una radice lasciata da
  una corsa interrotta, hanno dato DLL, xml, `manifest.json` e `manifest.props` identici bit per bit.
- `GPCChecker.Geotechnics.xml` viene ora dalla stessa build: aggiunge la documentazione di `PileSegments` e
  `PileSegments.TubeWeight`, assente nella copia precedente, ferma a una build anteriore.
- ANTHEA, sul commit dell'installazione (edc4fce) e senza `-GpcLibDir`, con le DLL nuove negli output (SHA-256
  controllati): profilo standard (corsa `20261007-170249-s2u`) 33 PASS, 1 KNOWN (`verifiche/project-calculations`),
  0 NEW-FAIL; profilo baseline (corsa `20261007-170533-s2u-baseline`, `-BaselineRef <F2-B2>\headless`, `-DenseRef
  <F2-pre-m4-v2>\a\tutte`) 39 PASS, 1 KNOWN, 0 NEW-FAIL. Con `-CompareTo` le corse del candidato nessun avviso: esiti e
  righe di conteggio uguali a quelli del candidato, a loro volta uguali a S1 sullo stesso codice (17b6c98). Cattura
  headless uguale alla baseline F2-B2 su 432 file (solo i 21 tempi volatili; nel manifest cambiano commit, hash di
  questo manifest, le cinque DLL e, come già con S1 su 17b6c98, ANTHEA.Calculations, ANTHEA.Core e ANTHEA.Testing,
  perché F2-B2 è catturata da a88b177). Banco c.a. contro le fixture di Checker: 27 698 righe, 26 462 identiche, 1236
  con soli identificativi casuali; contro F2-pre-m4-v2: 32 564 righe, 31 320 identiche, 1244 con soli identificativi
  casuali; nessuna differenza. Impronta delle 80 mesh (cattura densa `mesh`) identica riga per riga a B0 e a
  F2-pre-m4-v2.
- Librerie, sulle DLL compilate nel checkout principale da Model 5ad56681 e Checker 0d7ba50b, stesso sorgente di questa
  release (non ripetuti sui binari della radice temporanea, che differiscono solo per il percorso del PDB): Concrete
  510/510, Geotechnics 98/98, CompositeBridge 254/254, Model 892 superati e 2 ignorati, ModelChecker 106/106. Steel
  (non nello snapshot, 17 fallimenti storici) e BridgeAudit (compila ANTHEA/X.Core) non eseguiti.
- Manca il profilo full con le prove WPF a schermo.

**Snapshot riproducibile da commit (7 ottobre 2026, refactoring F0.8-F0.9).** Prima ricostruzione con
`tools/libs/Update-Snapshot.ps1`: ogni DLL viene da un commit con albero pulito, registrato in `manifest.json`
(repository, ramo, commit, push, SDK, versione, SHA-256); `manifest.props` permette alla build di verificare gli
hash (Directory.Build.targets). Commit sorgente: Utilities df3b3e7, Geometry 75182cc (binari committati), Model
5ba61a04, Checker b994e188, tutti pushati il 7/10/2026 (campo `pushed` del manifest aggiornato, DLL e SHA-256
invariati); SDK 9.0.318 fissato da global.json in ogni libreria.

- Le versioni distribuite in precedenza da working tree con lo stesso numero (Concrete 0.0.14.0 e Geotechnics
  0.1.0.0 ricompilate più volte tra il 2 e il 6/10; GPCGeometry 2.1.0.3 senza la modifica di CoordinateSystem
  6590b90; GPCModel 1.6.0.0 senza i commit successivi) diventano: GPCGeometry 2.1.0.4, DelaunayMesh 2.0.0.11
  (stesso sorgente, ricompilata), GPCModel 1.6.1.0, GPCModelData 0.0.2.2, Concrete 0.0.15.0, Geotechnics 0.1.1.0,
  CompositeBridge 1.4.0.3. GPCUtilities 2.0.0.8 invariata.
- ANTHEA: tutte le suite del runner come prima; cattura headless identica alla baseline B0 a tolleranza zero;
  griglie dense di CheckerMigration.Capture identiche (a meno degli identificativi casuali); impronta delle 80 mesh
  identica bit per bit. Ricompilazioni complete dagli stessi commit identiche bit per bit.
- Librerie: Concrete 500/500 (CrackMigrationTests a 1e-9 compreso), CompositeBridge 254/254, Geotechnics 98/98,
  Model 892 superati e 2 ignorati, ModelChecker 105/106 e Geometry 852/853 per due aspettative superate dei test,
  corrette in Model d6631635 e Geometry 6a0d1c1, Steel con i soli 17 fallimenti storici. Rapporto in
  supporto/artefatti/refactoring/test-librerie-S1.
- SourceLink scrive lo SHA del commit nel PDB e quindi nella DLL: per riprodurre una DLL si compila il commit
  registrato nel manifest, non uno successivo.

**Aggiornamento DelaunayMesh 2.0.0.10 (6 ottobre 2026).** Geometry master 296d05d; le altre DLL sono invariate.

- Raffinamento: il controllo dei segmenti di bordo invasi dal circocentro usa una griglia (prima confrontava ogni triangolo con
  tutti i segmenti, tempo quadratico); con più di 1000 punti di bordo l'inserimento è in ordine casuale (prima il numero di
  scambi cresceva col quadrato dei punti). Fino a 1000 punti di bordo le mesh sono identiche alla 2.0.0.8; mesh a triangoli
  oltre 50 000 elementi da 2 a 5 volte più veloci.
- Corretta la classificazione dentro/fuori con segmenti comuni a due contorni (foro con un lato sul contorno, fori adiacenti,
  figlio uguale al proprio foro): prima il risultato dipendeva dal percorso e poteva includere triangoli esterni.
- ANTHEA: `--checker`, `--bridge` e confronto numerico completo identici alla 2.0.0.8 (stesso albero di lavoro, sostituita
  solo la DLL). Model 861 test, Checker.Test.Concrete 500 test superati; Checker.Test.Steel con gli stessi 17 errori della
  2.0.0.8. Prova WPF non eseguita.

**Snapshot corrente (2 ottobre 2026).** Model master c07e99ac, Checker develop 79268a9e. Utilities, Geometry e DelaunayMesh
sono invariate (hash identici).

- **Nuova `GPCChecker.Geotechnics.dll` 0.1.0.0**: i nuclei geotecnici spostati da ANTHEA (stabilità dei pendii con Bishop,
  cedimenti edometrici, Newmark, portanza sismica EN 1998-5 allegato F, amplificazione NTC del sito, pali e micropali, muri di
  sostegno con combinazioni, esercizio e stabilità globale). Unità di Model: N, mm, MPa, N/mm³, rad.
- **Model 1.6.0.0, ModelData 0.0.2.1**: terreni, stratigrafie e normative geotecniche (`GPC.Model.Geotechnics`), densità dei
  materiali in t/mm³ (`Material.GetUnitWeight`), catalogo CHS Celsius (Tata Steel).
- **Checker.Concrete 0.0.14.0, CompositeBridge 1.4.0.2**: verifiche di taglio, tensioni SLE, torsione, fessurazione, dettagli,
  durabilità e copriferri spostate da ANTHEA (nuovi spazi dei nomi, API precedenti invariate).
- ANTHEA: il muro di sostegno usa la libreria (`RetainingWall.Library.cs` è l’adattatore m, kN ↔ mm, N); i nuclei
  `X.Calculations/Geotechnics` di Bishop, cedimenti, Newmark e portanza sismica non sono più compilati. Con le sole DLL nuove,
  e il codice precedente, la cattura dei muri coincide con quella congelata; con l’adattatore le differenze sono quelle
  dichiarate nel registro della migrazione (Checker/docs/migrazione-anthea).
- Pali e micropali (`Calcolo`, `Nq`, `Micropali`, `PaloOrizzontale`, `MicropaloOrizzontale`) sono adattatori della libreria:
  cattura riprodotta entro 1e-14, regressione numerica invariata (464 casi; i 6 casi con cu = 0 sotto falda sono ora errori
  perché i terreni di Model richiedono cu > 0). GPCChecker.Geotechnics di questo snapshot aggiunge `LateralPileCapacity.ModelName`
  (Checker 40ca3008); le altre DLL sono quelle di 79268a9e.

**Snapshot precedente (1 ottobre 2026).** Model master ca76e6d1, Checker develop 23bc2d15. Utilities, Geometry e DelaunayMesh
sono invariate (hash identici).

- **Model 1.5.0.0, ModelData 0.0.2.0**: sezioni.
  - Cataloghi dei profilati EN e AISC in ModelData.
  - Torsione e ingobbamento numerici (elementi finiti) per le sezioni generiche; disponibilità esplicita delle proprietà
    (`Section.GetAvailability`).
  - Sezioni composte acciaio–calcestruzzo: sovrapposizione esatta, profili specchiabili, metodi statici di costruzione.
  - Sezioni parametriche, saldate, sagomate a freddo e variabili.
  - `ReinforcedConcreteSection.Shape` rinominata `ConcreteShape` (`Shape` resta solo come implementazione esplicita di
    `ISectionShape`): aggiornati `CheckerSection`, `Ntc2018Checks` e `SectionWorkspaceChecks`.
- **Checker.Concrete 0.0.13.1, CompositeBridge 1.4.0.1**: solo ricompilate contro Model 1.5, nessuna modifica del codice.
- ANTHEA: tutte le suite di X.Verifiche, i progetti `*.Checks` e le 20 prove WPF eseguiti prima e dopo l'aggiornamento con gli
  stessi esiti. Regressione numerica, `software`, checksum del benchmark CA, archivi `.programma`/`.anthea`/JSON e testo delle
  relazioni Word identici (a meno di identificativi, date e tempi). Gli esiti non nulli erano già presenti con lo snapshot del
  28 settembre e non dipendono dalle DLL: `--project-calculations` (default di `geo_muri_sostegno` diversi tra progetto e
  foglio) e sei prove WPF (`smoke`, `smoke-bridge`, `smoke-ca-extensions`, `smoke-global-stability`, `smoke-project-workspace`,
  `smoke-retaining-wall`).
- BridgeAudit (Checker): risultati identici; gli 8 casi `MigrationPreservesEveryResult` falliscono già dal 29 settembre perché
  l'ingresso del ponte contiene tre nuove chiavi (`predalle`, `h_predalle`, `rif_ferri_inf`) non presenti nelle baseline.

**Snapshot precedente (28 settembre 2026).** Cambia solo CompositeBridge 1.4.0.0; le altre DLL sono quelle del 27 settembre.

- **CompositeBridge 1.4**: torsione del cassoncino.
  - Cella chiusa di Bredt per fase (soletta o controvento superiore t*), q sommato ad anime, fondo, pioli e soletta.
  - Distorsione con la trave su suolo elastico e diaframmi intermedi a piastra o a X.
  - Diaframma d'appoggio e coppia degli apparecchi.
  - `BridgePhase.TorsionKNm`, `HBridgeInput.Box` e `BridgeStage.Torsion`.
- Per H e anima inclinata i risultati sono identici alla 1.3.1: le baseline BridgeAudit cambiano solo per il testo del campo di
  validità, le nuove chiavi di ingresso e il campo `Torsion` nullo.

**Build precedente (27 settembre 2026, seconda build).**

Versioni: Utilities 2.0.0.8, Geometry 2.1.0.3, DelaunayMesh 2.0.0.8, Model 1.4.1.0, ModelData 0.0.1.16, Checker.Concrete 0.0.13.0,
CompositeBridge 1.3.1.0.

- **Utilities 2.0.0.8**: costanti imperiali esatte (lbf, kip, lb, psi, ksi).
- **Model 1.4.1**: Annessi Nazionali corretti.
  - UNI (Annesso italiano, DM 31/7/2012): αcc = 0,85, γc accidentale = 1,0, k5 = 0,70.
  - DS (DK NA 2024): γc = γcE = 1,45, γs = γp = 1,20, γ accidentali = 1,0.
  - DIN: γc accidentale = 1,3.
  - NTC 2018: γc accidentale = 1,0; trefoli in esercizio 0,8 fp(0,1)k.
  - EN: limite dei trefoli in esercizio k5·fpk sulla resistenza a rottura (prima su fp0,1k).
- **Checker.Concrete 0.0.13**:
  - ricerca robusta del punto del dominio (bisezione sulla superficie di rottura) quando quella iterativa non converge;
  - assi delle forze di qualsiasi orientamento;
  - profilo inglobato: sottrazione lineare del calcestruzzo nel calcolo lineare e tensione di progetto fy/γM0 in quello non lineare;
  - `StrainPlane.GetNeutralAxis` corretto.
- ANTHEA: nessuna differenza nei risultati delle suite (assi −X, −Y, profilo sotto la soletta, preset letti dalle classi di normativa).

**Build precedente dello stesso giorno:** Utilities 2.0.0.7, Geometry 2.1.0.2, DelaunayMesh 2.0.0.7, Model 1.4.0.0, ModelData 0.0.1.15,
Checker.Concrete 0.0.12.7, CompositeBridge 1.3.0.0.

- **Model 1.4**:
  - nuove sezioni `SectionHInclinedWeb` (H con anima inclinata) e `SectionSteelBox` (cassoncino);
  - `Rck` con il segno di fck;
  - dilatazione termica del calcestruzzo 10·10⁻⁶.
- **Geometry 2.1.0.2**: corretto `Circle2d.ConvertToPolygon`.
- **Checker.Concrete 0.0.12.7**:
  - ricerca del punto del dominio con ripiego sulla strategia a intersezione (stress block);
  - nessun punto con N diverso da quello richiesto nelle analisi a N costante;
  - `FcdAccidental` corretto.
- **CompositeBridge 1.3**:
  - tipi di sezione H / H con anima inclinata / cassoncino (flessione retta, lamiere reali per le verifiche locali);
  - metodo Viviani (storico e confronto);
  - storico: tensioni ai lembi delle piastre estrapolate fino alle facce (prima quelle della fibra più esterna).

Per la sezione ad H i risultati del metodo cumulativo sono identici alla versione 1.2.

**Snapshot del 26 settembre 2026.** Tutte le DLL provengono dai `bin/Release`
della stessa build della catena Utilities → Geometry → Model → Checker:
Utilities 2.0.0.7, Geometry 2.1.0.1, DelaunayMesh 2.0.0.7, Model 1.3.0.0,
ModelData 0.0.1.13, Checker.Concrete 0.0.12.6 e CompositeBridge 1.2.0.0, compilata
contro le stesse dipendenze (non più contro lo snapshot del 24 settembre).
**GMsh.Net e UnsafeEx sono stati rimossi**: nessuna libreria li usa (Checker.Concrete
aveva solo un riferimento residuo del 2022, quando Gmsh fu sostituito da DelaunayMesh;
la DLL compilata non li referenziava). Model 1.3 contiene le correzioni di calcolo
delle sezioni (asse 1 sempre principale, momenti d'inerzia di C e H con raccordi
esatti, moduli plastici) e la nuova `SectionHDoubleBottomFlange`; CompositeBridge 1.1
usa le due piastre inferiori reali (non più il rettangolo equivalente), permette di
escludere l'instabilità locale di piattabanda superiore, inferiore e anima, distingue
Vpl,Rd NTC (hw tw) ed EC3 (η hw tw), corregge la forza negli irrigidimenti e (1.2) accelera l'iterazione delle larghezze efficaci: partenza dalla situazione precedente e rilassamento di Aitken.
Versioni e SHA-256 sono nel manifest; i paragrafi seguenti descrivono gli snapshot precedenti.

Il 25 settembre 2026 è stata aggiunta `GPCChecker.CompositeBridge.dll`, compilata
dal nuovo progetto Checker **contro le dipendenze di questo snapshot**. Le otto
DLL native elencate sotto sono rimaste identiche (hash verificati). La nuova
libreria contiene il calcolo ponte trasferito da ANTHEA; non viene compilata
dall'app. Vedere [migrazione](../../supporto/docs/guida-teorica-anthea.md).

Snapshot dei binari forniti da Giulio Pacini, aggiornato il 24 settembre 2026
dai `bin/Release` dei singoli progetti Model, ModelData, GPCUtilities,
GPCGeometry, DelaunayMesh e GPCChecker.Concrete. Le copie delle dipendenze
nella build di Checker coincidono con quelle dei rispettivi progetti.
GMsh.Net e UnsafeEx provengono da `Geometry/GMesh/bin/Release` e sono invariati.
I percorsi di origine, le versioni e gli hash di ciascun file sono nel manifest.
Lo snapshot corrente usa la build del 24 settembre alle 16:10: Model 1.1.0.3,
Checker.Concrete 0.0.12.2, Geometry 2.0.1.10, Utilities 2.0.0.6 e
DelaunayMesh 2.0.0.4. ModelData è ricompilata con versione invariata 0.0.1.10;
per distinguere anche questa libreria va verificato lo SHA-256.
Non sono ricompilati da ANTHEA e non dipendono da percorsi assoluti della macchina.

Dal 26 settembre le DLL sono referenziate da `X.Calculations` (assembly `ANTHEA.Calculations`), che contiene gli adattatori e i motori senza UI. I generatori di report in `X.Core` e le viste mantengono solo i riferimenti necessari ai contratti dei risultati. Le DLL vengono copiate negli output desktop/verifiche.
MathNet.Numerics 5.0.0 viene risolto tramite NuGet. Non è necessario importare
CheckerUI, Eyeshot, Rhino o Grasshopper. Il percorso attivo genera la mesh della
sezione tramite DelaunayMesh; non usa il runtime nativo Gmsh.

Le versioni assembly e gli SHA-256 sono in [manifest.json](manifest.json).
Il catalogo `GPCModelData.dll` proviene da
`Model/ModelData/bin/Release/netstandard2.0`. ANTHEA legge le proprietà statiche
di `ConcreteMaterialEN1992Data` e `SteelMaterialEN1992Data`, distinguendo
acciai per armature e trefoli tramite `SteelType`. I valori non sono duplicati
in tabelle locali. I nomi sono quelli del catalogo (anche `C80/90`, così denominato
nella DLL); la disponibilità nel catalogo EN 1992 non implica ammissibilità
automatica per ogni normativa o annesso nazionale selezionato.
La scelta esplicita di uno standard carica tutte le proprietà del preset;
aprire un foglio esistente conserva i valori salvati. I coefficienti normativi
restano separati dai materiali. Applicare un materiale trefolo modifica il
predefinito per i nuovi cavi; il menu materiale nella singola riga consente
di assegnarlo a un cavo esistente senza modificare gli altri.
Per aggiornare: vedere la procedura in testa a questo file (dal 7 ottobre 2026
`Update-Snapshot.ps1 -FromUpstream`). Non copiare DLL a mano dalle cartelle bin esterne.

Le DLL proprietarie restano soggette alle condizioni del titolare. Questo snapshot
non assegna nuove licenze ai componenti terzi (GMsh.Net, DelaunayMesh, UnsafeEx);
prima di distribuire ANTHEA all'esterno verificare licenze e avvisi applicabili.
La provenienza sopra descrive lo snapshot nativo; il nuovo sorgente CompositeBridge
è nel repository Checker, come richiesto per la migrazione del modulo ponte.
# Aggiornamento storico e non lineare

`GPCChecker.CompositeBridge.dll` include ora `History`: deformazioni al getto, ritiri incrementali, memoria plastica, analisi N–Mx lineare e non lineare. ANTHEA usa `HBridgeHistoryResults.Calculate` per i nuovi selettori e conserva il metodo cumulativo precedente. Il manifest contiene il nuovo hash; tutte le altre DLL native rimangono quelle dello snapshot originario. OpenSees viene usato soltanto nei test, senza dipendenze aggiuntive nell'applicazione.

Validazione e limiti: guida teorica di ANTHEA (sezione composta da ponte); riferimenti congelati in `Checker/GPCChecker.Test.CompositeBridge/Validation`.
