# Preparazione di ANTHEA 1.16.1

Richiesta dell'utente del 29 ottobre 2026 sera: «prepara l'installer 1.16.1» ([decisioni](decisioni.md), «29 ottobre (sera) — decisioni
della 1.16.1»). Il contenuto per l'utente è nelle [note di rilascio](note-rilascio-1.16.1.md).

## Contenuto

- Tutto il contenuto della 1.16.0 (bc6add07, tag `v1.16.0`).
- Refactoring delle combinazioni del modulo Modelli, chiesto dall'utente dopo la 1.16.0 («cerca colli di bottiglia o bug dentro la
  gestione delle combo»), da 8ce3a554 a e0f90c60:
  - cache di ricostruzione di GPCModel 5.28;
  - revisioni delle combinazioni tenute dalle copie, Attach e voce del documento senza serializzazioni ripetute;
  - catalogo, griglia e campi degli inviluppi senza ricerche quadratiche;
  - preparazione GPC anticipata all'apertura del catalogo;
  - bug COMBO-B1, COMBO-B2 e COMBO-B3;
  - prova dei tempi `CombinationTiming` con l'impronta dei risultati della 1.16.0 e lo script `tools/model-viewer/Measure-CombinationTimes.ps1`.
- Librerie della release AN (48047f3c).

## Librerie: release AN

- **Origine:** set ufficiale da Model `9f40d98a` e Checker `bc2b3435`, entrambi su origin/master. Geometry `6734eee1` e Utilities
  `e57b440b` sono invariati.
- **Contenuto:**
  - GPCModel 5.28.0.0, con `CombinationRebuildCache`;
  - GPC.Converter 2.10.0.0, che legge ogni risposta MIDAS una volta;
  - le altre DLL ricompilate senza modifiche al sorgente.
- **Installazione:** in `lib/Checker` da 48047f3c. Manifest delle librerie `ABEA94C3…D5B2`. Le versioni delle DLL sono in
  `lib/Checker/README.md` («Release AN»).
- **Fondazioni superficiali:** le impronte cambiano solo per la versione di Geotechnics 0.27.0.1 (B-FON-01, `seguito_an`).

## Versione

`<Version>` 1.16.1 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` (30f48e51): nome del setup, versione file `1.16.1.0`, prodotto
`1.16.1+<commit>`. Istruzioni dell'installer con PDF; originali 1.16.0 in `supporto/SUPERATI/installer-1161-20261029`.

## Prove

- **Full sul ramo:** full sul ramo `refactoring/combo-modelli` con il set AN (48047f3c, `supporto/artefatti/combo-modelli/full-an`):
  PASS 157, KNOWN 0, NEW-FAIL 0.
- **Campagna dei tempi:** nella notte, prima del setup (`supporto/artefatti/combo-modelli/tempi-20261029-notte*`). Le impronte dei
  modelli 100 × 50 e 500 × 100 sono uguali alla 1.16.0.
- **Full di rilascio:** sul commit 30f48e51 (`supporto/artefatti/rilascio-1.16.1/full`, profilo full con `-Installer` e
  `GPC_STRAUS7_ELEMENTS=versioned`): **PASS 160, KNOWN 0, NEW-FAIL 0**, senza ripetizioni. Una prima corsa del 29/10 sera si era
  fermata alle 23:43 con la chiusura della sessione, senza fallimenti fino alle prove UI (`rilascio-1.16.1/full-interrotta`); la corsa
  completa è del 30/10 mattina. Non eseguite, come nelle full precedenti, ElasticPile.Checks e BridgeValidationCurrent (passano nei
  test di libreria) ed ElasticPile.Performance (misura dei tempi).
- **Setup:** costruito poi dallo stesso commit con `Crea-Installer.ps1` (`supporto/artefatti/rilascio-1.16.1/installer`), albero pulito.

## Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Commit del full = commit del setup, albero pulito | stesso SHA | 30f48e51 per entrambi, albero pulito (`source_clean: true`) |
| Full `rilascio-1.16.1/full` | PASS, NEW-FAIL 0 | PASS 160, KNOWN 0, NEW-FAIL 0, nessuna ripetizione |
| Suite installer | PASS | `installer/safety`, `installer/guides`, `installer/examples` PASS |
| `7z t` del setup | nessun errore | nessun errore, 393 file |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.16.1+<commit>` | `1.16.1+30f48e51a1fcd36b088349b6a304049297468c67` |
| DLL GPC uguali a `lib/Checker/manifest.json` | 13, set AN | 13, set AN, manifest `ABEA94C3…D5B2` |
| Processo Straus7 | `ANTHEA.Strand7Worker` accanto ad `ANTHEA.exe`, senza `St7api.dll` | 4 file del worker, `St7api.dll` assente |
| Guide | pratica e teorica Rev62 | Rev62 |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno | nessuno (`forbidden_files` vuoto) |
| Setup | nome, dimensione, SHA-256 | `ANTHEA-1.16.1-Setup-x64.exe`, 71 122 799 byte, `38F32D6A5653BDB09913B640734637BE178CDC29E50F76ED5CFE0512846DA4DD` |

## Esito

- **File distribuiti:** 388, gli stessi della 1.16.0 (nessun file aggiunto o tolto); file applicativi 331. Esempi: 52 documenti per 15
  moduli.
- **Audit:** `Audit-Installer.py` PASS (`audit-payload.json` accanto al setup in `supporto/artefatti/rilascio-1.16.1/installer`).
- **Pubblicazione:** con l'ok dell'utente («Sì: se la full di rilascio è pulita, tag v1.16.1 sul commit del setup e push di main e
  refactoring/f5»), tag `v1.16.1` sul commit del setup e push di `main` e `refactoring/f5`.
