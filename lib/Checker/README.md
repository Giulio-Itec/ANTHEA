# DLL Checker

**Release S2 (7 ottobre 2026, refactoring F2.4).** Ricostruita con `tools/libs/Update-Snapshot.ps1` da alberi
puliti, senza `-Install` (staging in `supporto/artefatti/lib-staging/S2`, poi copiati DLL, `manifest.json` e
`manifest.props`). Commit sorgente: Utilities df3b3e7 e Geometry 6a0d1c1 (binari committati, gli stessi di S1:
dopo 75182cc Geometry cambia solo un test), Model master 5ad56681, Checker develop 1fbaea61; SDK 9.0.318.

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
- Push: Model 5ad56681 era pushato al momento della build (`pushed: true`). Checker 1fbaea61 è locale: è il merge delle
  pagine dei metodi c.a. (F2.2, solo `docs/metodi`) sopra 0d7ba50b, già pushato. Il commit da pushare è develop
  1fbaea61; dopo il push il campo `pushed` delle tre DLL di Checker passa a `true` senza ricompilare (DLL e SHA-256
  invariati).
- Riproducibilità: una ricompilazione completa (`--no-incremental`) dagli stessi commit dà DLL identiche bit per bit.
  Una prima build è stata scartata: era partita mentre su Checker develop entravano i merge delle pagine dei metodi, e
  il manifest registrava 0d7ba50b per DLL compilate da 1fbaea61.
- ANTHEA, sul commit dell'installazione e senza `-GpcLibDir`: profilo standard 33 PASS, 1 KNOWN
  (`verifiche/project-calculations`), 0 NEW-FAIL; profilo baseline 39 PASS, 1 KNOWN, 0 NEW-FAIL, esiti e righe di
  conteggio uguali a quelli di S1 sullo stesso codice (17b6c98). Cattura headless uguale alla baseline F2-B2 su 432 file
  (solo i 21 tempi volatili; nel manifest cambiano commit, hash di questo manifest, le cinque DLL e, come già con S1 su
  17b6c98, ANTHEA.Calculations, ANTHEA.Core e ANTHEA.Testing, perché F2-B2 è catturata da a88b177). Banco c.a. contro le
  fixture di Checker: 27 698 righe, 26 462 identiche, 1236 con soli identificativi casuali; contro F2-pre-m4-v2: 32 564
  righe, 31 320 identiche, 1244 con soli identificativi casuali; nessuna differenza. Impronta delle 80 mesh identica a
  B0 e a F2-pre-m4-v2 bit per bit.
- Librerie, sulle DLL compilate da Model 5ad56681 e Checker 0d7ba50b (stesso sorgente di 1fbaea61): Concrete 510/510,
  Geotechnics 98/98, CompositeBridge 254/254, Model 892 superati e 2 ignorati, ModelChecker 106/106. Steel (non nello
  snapshot, 17 fallimenti storici) e BridgeAudit (compila ANTHEA/X.Core) non eseguiti.
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
Per aggiornare: sostituire un insieme coerente di DLL proveniente dalla stessa
build, aggiornare il manifest, compilare ed eseguire i controlli `--checker`, la
regressione completa e la prova WPF. Non usare wildcard sulle cartelle bin esterne.

Le DLL proprietarie restano soggette alle condizioni del titolare. Questo snapshot
non assegna nuove licenze ai componenti terzi (GMsh.Net, DelaunayMesh, UnsafeEx);
prima di distribuire ANTHEA all'esterno verificare licenze e avvisi applicabili.
La provenienza sopra descrive lo snapshot nativo; il nuovo sorgente CompositeBridge
è nel repository Checker, come richiesto per la migrazione del modulo ponte.
# Aggiornamento storico e non lineare

`GPCChecker.CompositeBridge.dll` include ora `History`: deformazioni al getto, ritiri incrementali, memoria plastica, analisi N–Mx lineare e non lineare. ANTHEA usa `HBridgeHistoryResults.Calculate` per i nuovi selettori e conserva il metodo cumulativo precedente. Il manifest contiene il nuovo hash; tutte le altre DLL native rimangono quelle dello snapshot originario. OpenSees viene usato soltanto nei test, senza dipendenze aggiuntive nell'applicazione.

Validazione e limiti: guida teorica di ANTHEA (sezione composta da ponte); riferimenti congelati in `Checker/GPCChecker.Test.CompositeBridge/Validation`.
