# DLL Checker

Snapshot dei binari forniti da Giulio Pacini, copiati dalla cartella
`Checker/GPCChecker.Concrete/bin/Release/netstandard2.0` il 21 settembre 2026.
Non sono ricompilati da ANTHEA e non dipendono da percorsi assoluti della macchina.

Le DLL sono referenziate da X.Core e copiate negli output desktop/verifiche.
MathNet.Numerics 5.0.0 viene risolto tramite NuGet. Non è necessario importare
CheckerUI, Eyeshot, Rhino o Grasshopper. Il percorso attivo genera la mesh della
sezione tramite DelaunayMesh; non usa il runtime nativo Gmsh.

Le versioni assembly e gli SHA-256 sono in [manifest.json](manifest.json).
Per aggiornare: sostituire un insieme coerente di DLL proveniente dalla stessa
build, aggiornare il manifest, compilare ed eseguire i controlli `--checker`, la
regressione completa e la prova WPF. Non usare wildcard sulle cartelle bin esterne.

Le DLL proprietarie restano soggette alle condizioni del titolare. Questo snapshot
non assegna nuove licenze ai componenti terzi (GMsh.Net, DelaunayMesh, UnsafeEx);
prima di distribuire ANTHEA all'esterno verificare licenze e avvisi applicabili.
Le cartelle sorgenti del titolare non sono state modificate.
