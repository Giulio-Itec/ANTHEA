# Rilascio coerente F5: paratie e spettri

Il 19 ottobre 2026 l'utente ha autorizzato il push delle due revisioni già
integrate e collaudate, l'aggiornamento ufficiale di `lib/Checker` e il profilo
full. Questa autorizzazione non pubblica ANTHEA né i nuovi rami di prosecuzione.

## Revisioni pubblicate e set

- Model/master: `e1e6e1079fb7b90b4ca1fee60e01d05a1b0423d9`.
- Checker/master: `dfb808c8ab5463b0df0ab965813320ddc38eb5f0`.
- Utilities: `e57b440b16a0a311a7bf0af2f9cf4d2681b83491`, già pubblicata.
- Geometry: `6734eee1004142576b96759e34c15dec48776e23`, già pubblicata.

Fetch e verifica della discendenza prima dei due push; conferma di entrambi
gli SHA con `git ls-remote`, senza force push. Nuovo set non candidato:
`supporto/artefatti/paratie/rilascio-f5/gpc-release-01`, prodotto con
`Checker/build/Release-Set.ps1 -Root ... -Previous lib/Checker/release-set.json`.
La copia dei metadati del rilascio Z precedenti all'installazione è in
`rilascio-f5/precedente-release-z/`; il riferimento F3 rimane congelato.

Suite effettivamente rieseguite: 3172 PASS, soltanto i 17 fallimenti Steel
storici ammessi; due test Model nativi ignorati. Model Core 18, UnitTest 1249,
Concrete 795, Steel 88 PASS, CompositeBridge 284, Geotechnics 191,
Calculation 174, ShallowFoundation 28, EmbeddedWalls 67, Steel.Members 13,
Design 91, ModelChecker 174. Compatibilità API superata.

Audit indipendente: 14 DLL identiche byte per byte alla candidata integrata,
versioni aumentate rispetto a Z per ogni DLL cambiata; Utilities, Geometry
e Delaunay identiche a Z. Nessun Gmsh nel set. Installazione con
`tools/libs/Update-Snapshot.ps1 -ReleaseSet ... -Staging ... -Install`;
13 DLL verificate anche dopo la copia in `lib/Checker`.

Impronte SHA-256:

- Set completo: `b07095e0b1585bc0f6817e7380f8590ba0eb9fce0f734ad4eeed8bc3ca87b573`.
- Manifest installato: `397e9315c536de3954fc59b86f4589505e874343dcb341e1145b69c49573ac40`.

Il registro di provenienza F4 punta ora al rilascio AA: cambiano soltanto
nome, impronte del set e nota. Input, attesi numerici, transizioni F3/F5,
tolleranze e liste dei fallimenti ammessi restano invariati.

## Collaudo ANTHEA e pacchetto

Il profilo full deve ancora concludersi sullo snapshot installato. Il
confezionatore locale accetta ora `-PublishedLibraries` soltanto con un
full completo sulla revisione pulita, tutti gli stadi eseguiti, set non
candidato e sorgenti pubblicate; copia anche il manifest del set completo.
La versione ANTHEA rimane una prova locale F5, con la matrice paratie aperta.

La candidata precedente `candidata-integrata-01` resta conservata e
utilizzabile. I nuovi rami `refactoring/f5-paratie-seguito` e
`codex/paratie-seguito` non sono stati pubblicati o reintegrati nei master.
L'approfondimento di pali/micropali resta in coda al modulo paratie.
