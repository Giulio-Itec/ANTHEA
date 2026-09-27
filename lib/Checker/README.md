# DLL Checker

**Snapshot corrente (28 settembre 2026).** Cambia solo CompositeBridge 1.4.0.0; le altre DLL sono quelle del 27 settembre.

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
dall'app. Vedere [migrazione](../../supporto/docs/migrazione-composite-bridge.md).

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

Validazione e limiti: `supporto/docs/ponte-storico-non-lineare.md`; riferimenti esterni congelati in `Checker/GPCChecker.Test.CompositeBridge/Validation`.
