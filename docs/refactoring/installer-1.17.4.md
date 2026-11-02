# Preparazione di ANTHEA 1.17.4

Richiesta dell'utente del 2 novembre 2026, dopo la full del motore a matrici delle combinazioni: «ricorda di fare la nuova versione»
(piano [modelli-motore-combinazioni](../sviluppo/modelli-motore-combinazioni.md), decisione D3: versione di ANTHEA quando F4 è verde).
Il contenuto per l'utente è nelle [note di rilascio](note-rilascio-1.17.4.md).

## Contenuto

- Tutto il contenuto della 1.17.3 (tag `v1.17.3`).
- 9520bdd1..a6d25fb2: motore a matrici delle combinazioni:
  - piano del lavoro;
  - `GpcModelCombinationService` su `CombinationMatrix` di GPCModel 5.38, con la ricostruzione di prima dietro `UseMatrix = false`;
  - prova `CombinationMatrixChecks` del confronto byte per byte in `ModelWorkspace.Checks`;
  - set ufficiale AU.

## Librerie

Set ufficiale AU (`supporto/artefatti/librerie-20261102-au`): Model `2f934f9f` (GPCModel 5.38.0.0), Checker `6156e4ce` ricompilato,
Geometry `6734eee1` e Utilities `e57b440b` invariati; manifest `F593B14E…D151F`. Le prove di libreria (`Verify.ps1` di Model e Checker,
`Release-Set.ps1 -Previous` rispetto ad AT) sono in `lib/Checker/README.md`.

## Versione

`<Version>` 1.17.4 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` (a2f90766): nome del setup, versione file `1.17.4.0`, prodotto
`1.17.4+<commit>`. Istruzioni dell'installer aggiornate, con il PDF rigenerato da `supporto/scripts/documentazione/markdown-pdf.py` e
controllato nelle tre pagine. Gli originali sono in `supporto/SUPERATI/rilascio-1174-20261102`, con manifest e SHA-256. I valori non
cambiano e non ci sono funzioni nuove: per la regola aumenta l'ultimo componente.

## Prove

Il lavoro è stato fatto nel worktree `aw-combo-inviluppi`. Le evidenze sono nel checkout principale, in
`supporto/artefatti/combo-inviluppi` (full e misure) e `supporto/artefatti/rilascio-1.17.4` (rilascio).

- **Full** sul commit del contenuto a6d25fb2 (`combo-inviluppi/full`): **PASS 159, NEW-FAIL 2**. I due rossi sono
  `Straus7Native.Checks` e `ModelViewer.Importers.UiChecks`: «cannot find the hardware lock», con la chiave della licenza Straus7
  assente. Con la chiave inserita dall'utente, rilanciati con `ci.ps1 -NoBuild -Only` (`combo-inviluppi/full-straus7`): **PASS 2**.
  Totale 161 PASS.
- **Confronto del motore** in `ModelWorkspace.Checks` (`CombinationMatrixChecks`): risultati salvati uguali byte per byte con e senza
  matrice, per tutte le definizioni calcolabili del 24 × 12 e per una ogni dodici del 100 × 50, con le definizioni dell'utente.
- **Tempi** sul 500 × 100, giri alternati con e senza matrice (`combo-inviluppi/ab-con`, `ab-senza`): impronta dei valori
  `342829B4…EDB4` uguale nei due casi e alla 1.16.0.
- **Profilo standard con le suite dell'installer** sul commit della versione a2f90766 (`rilascio-1.17.4/standard`):
  **PASS 87, NEW-FAIL 0**, con `installer/safety`, `installer/guides`, `installer/examples` e `qa/no-test-code`.
- **Setup**, costruito dal commit a2f90766 con albero pulito, con `Crea-Installer.ps1` (`rilascio-1.17.4/installer`):
  - file `ANTHEA-1.17.4-Setup-x64.exe`, 74 021 986 byte;
  - SHA-256 `A53D9D27D3EFB5F12429EE075E00D41308DE63D45E8D8359CC8EC3E8D99F9A69`;
  - `7z t` senza errori: 435 file;
  - estratto con `7z x` in `installer/estratto`;
  - `Audit-Installer.py` PASS (`audit.log`, `audit-payload.json`): 330 file dell'applicazione, 430 nel pacchetto, 95 documenti e 32 moduli di esempio, set AU, guide Rev68, nessun file vietato.

Il tag annotato `v1.17.4` sta sul commit del setup a2f90766 e riporta le note di rilascio. `main` e `refactoring/f5` arrivano al
commit di questo verbale, figlio del commit del setup.

## Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Commit del setup, albero pulito | albero pulito | a2f90766, albero pulito |
| Full di rilascio | PASS | sul commit del contenuto a6d25fb2: PASS 159 + 2 Straus7 rilanciati con la chiave |
| Suite installer | `installer/safety`, `installer/guides`, `installer/examples` PASS | PASS |
| `7z t` del setup | nessun errore | senza errori: 435 file |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.17.4+<commit>` | `1.17.4+a2f90766a42d1be3ba5c813549b584e88ae92e1e`, file `1.17.4.0` |
| DLL GPC uguali a `lib/Checker/manifest.json` | tutte quelle del manifest, set AU | 13 su 13, manifest `F593B14E…D151F` |
| Processo Straus7 | `ANTHEA.Strand7Worker` accanto ad `ANTHEA.exe`, senza `St7api.dll` | 4 file del processo, nessun `St7api.dll` |
| Guide | pratica e teorica Rev68 | Rev68 |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno | nessuno |
| Motore presente nell'estratto | `CombinationMatrix` nella GPCModel 5.38.0.0 | GPCModel.dll 5.38.0.0 con `CombinationMatrix`; `ANTHEA.ModelWorkspace.dll` con `UseMatrix` |
