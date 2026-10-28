# File di testo MIDAS per le prove del modulo Modelli (1.15.0)

Prove: `tests/ModelWorkspace.Checks/MidasTextImportChecks.cs` e la prova WPF fuori schermo
`ImporterRenderingChecks` di `tests/ModelViewer.UiChecks` (suite `ModelViewer.Importers.UiChecks`).
Sono tutti file nostri, copiati senza modifiche di contenuto dalle prove della libreria GPC Model
(branch `lib-1150/importatori`, commit 749548b5, `UnitTest/MidasTextCoverageTest.cs` e
`UnitTest/Fixtures/MidasCivil/`). Nessun atteso è stato ricavato dal codice di ANTHEA.

| File | Origine | Contenuto |
|---|---|---|
| `coverage.mct` | costante `Coverage` di `MidasTextCoverageTest`, con l’intestazione dell’export Civil | modello sintetico con ogni comando mappato: travi, truss, tirante, piastre quadrangolare e triangolare con ANGLE, sezioni SB, H, P, VALUE, spessori con offset, gruppi, assi nodali, vincoli, offset, rilasci, link elastici e rigidi, casi, peso proprio, carichi nodali, beam e di pressione, combinazioni GEN e STEEL |
| `springs.mct` | costante `SpringsText` | molle puntuali lineari, DOF fissati, assi nodali, molla COMP conservata; travi con sezione non definita |
| `families.mct` | costante `FamiliesText` | COMPTR, TENSTR, WALL, PLSTRS, SOLID |
| `gen-walls.mgt` | testo della prova `GenMgt_StoriesDiaphragmsAndWallsArePreservedAndTheRestIsMapped` | export MIDAS Gen con parete, piano e diaframma rigido |
| `native-packed-pairs-9.5.5.mct` | `Fixtures/MidasCivil/identical-pairs-native-9.5.5/packed-pairs-full.mct` | export nativo MIDAS Civil NX 9.5.5 (coppie di profili accoppiati) |
| `native-catalog-pairs-9.5.5.mct` | `Fixtures/MidasCivil/catalog-pairs-native-9.5.5/catalog-pairs-full.mct` | export nativo MIDAS Civil NX 9.5.5 (48 mensole, sezioni di catalogo) |
| `api-packed-pairs-9.5.5/*.json` | tabelle API dello stesso modello di `native-packed-pairs-9.5.5.mct` (stessa cartella della libreria) | UNIT, NODE, ELEM, SECT, STLD, CONS, CNLD, MATL: confronto testo/API della copia ANTHEA |

Le fixture native sono state generate con l’API Civil NX attiva dell’utente (vedi `PROVENANCE.txt` della libreria);
il repository della libreria contiene soltanto gli export di testo e le risposte JSON, senza chiavi o indirizzi.
