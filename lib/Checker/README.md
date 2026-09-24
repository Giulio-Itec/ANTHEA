# DLL Checker

Snapshot dei binari forniti da Giulio Pacini, aggiornato il 24 settembre 2026
dai `bin/Release` dei singoli progetti Model, ModelData, GPCUtilities,
GPCGeometry, DelaunayMesh e GPCChecker.Concrete. Le copie delle dipendenze
nella build di Checker coincidono con quelle dei rispettivi progetti.
GMsh.Net e UnsafeEx provengono da `Geometry/GMesh/bin/Release` e sono invariati.
I percorsi di origine, le versioni e gli hash di ciascun file sono nel manifest;
le versioni assembly da sole non distinguono questa build dalla precedente.
Non sono ricompilati da ANTHEA e non dipendono da percorsi assoluti della macchina.

Le DLL sono referenziate da X.Core e copiate negli output desktop/verifiche.
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
Le cartelle sorgenti del titolare non sono state modificate.
