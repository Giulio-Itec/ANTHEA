# Preparazione di ANTHEA 1.17.3

Richiesta dell'utente del 2 novembre 2026, dopo le correzioni dell'interfaccia dei fogli Azioni: «fallo. ma non fare il full». La full non è stata eseguita. Il contenuto per l'utente è nelle [note di rilascio](note-rilascio-1.17.3.md); i rilievi corretti in [azioni-ui-revisione-20261102.md](azioni-ui-revisione-20261102.md).

## Contenuto

- Tutto il contenuto della 1.17.2 (tag `v1.17.2`).
- e20c46ef..e707bc4c: correzioni dell'interfaccia dei sedici fogli Azioni e del foglio Viscosità e maturazione:
  - scheda visibile all'apertura e stato della vista nel foglio;
  - messaggio del calcolo e calcolo all'apertura;
  - testo nei campi numerici senza blocchi;
  - area minima, separatore decimale, «?» della guida, focus;
  - test che vedono i difetti.

## Librerie

Set ufficiale AT, invariato rispetto alla 1.17.1 e alla 1.17.2 (Model `5449e4d1`, Checker `a2acd3d2`); manifest `2234729D…9BB7`.

## Versione

`<Version>` 1.17.3 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` (4707f776): nome del setup, versione file `1.17.3.0`, prodotto `1.17.3+<commit>`. Istruzioni dell'installer aggiornate, con il PDF rigenerato da `supporto/scripts/documentazione/markdown-pdf.py` e controllato nelle tre pagine. Gli originali sono in `supporto/SUPERATI/rilascio-1173-20261102`, con manifest e SHA-256. Solo correzioni: per la regola aumenta l'ultimo componente.

## Prove

Nessuna full di rilascio. Il lavoro è stato fatto nel worktree `aw-rel-1173` e le evidenze copiate in `supporto/artefatti/rilascio-1.17.3` del checkout principale.

- **Suite mirate delle correzioni**, prima dell'integrazione: `supporto/artefatti/ci/20261102-*-azioni-ui-*`, tutte PASS, contrasto completo compreso. Il dettaglio è nel verbale della revisione.
- **Profilo standard con le suite dell'installer** sul commit della versione 4707f776 (`rilascio-1.17.3/standard`): **PASS 87, NEW-FAIL 0**, con `installer/safety`, `installer/guides`, `installer/examples` e `qa/no-test-code`. Le suite Wiki sono passate dopo aver copiato nel worktree le cartelle delle figure non versionate: `guide_anthea_itec_rev03`, `rev04`, `globale-guidata-20260930`, `muri-materiali-distinta-20261005`.
- **Setup**, costruito dal commit 4707f776 con albero pulito, con `Crea-Installer.ps1` (`rilascio-1.17.3/installer`):
  - file `ANTHEA-1.17.3-Setup-x64.exe`, 74 031 841 byte;
  - SHA-256 `4FCD294F03FEEF9839533FBEDFD071769EC6FDD12CA6E4A9DA852171FAD1B16D`;
  - `7z t` senza errori: 435 file;
  - estratto con `7z x` in `installer/estratto`;
  - `Audit-Installer.py` PASS (`audit.log`, `audit-payload.json`): 330 file dell'applicazione, 430 nel pacchetto, 95 documenti e 32 moduli di esempio;
  - nell'estratto ci sono i tipi nuovi (`CappedSplitPanel`, `ActionFieldPresentation`, `PendingFocusKey`, la validazione del documento Azioni).

Il tag annotato `v1.17.3` sta sul commit del setup 4707f776 e riporta le note di rilascio. `main` e `refactoring/f5` arrivano al commit di questo verbale, figlio del commit del setup.

## Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Commit del setup, albero pulito | albero pulito | 4707f776, albero pulito |
| Full di rilascio | non richiesta | non eseguita; profilo standard e installer PASS 87, NEW-FAIL 0 |
| Suite installer | `installer/safety`, `installer/guides`, `installer/examples` PASS | PASS |
| `7z t` del setup | nessun errore | nessun errore, 435 file |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.17.3+<commit>` | `1.17.3+4707f77611c413464224fba2701184c692678f91`, file `1.17.3.0` |
| DLL GPC uguali a `lib/Checker/manifest.json` | tutte quelle del manifest, set AT | 13 su 13, manifest `2234729D…9BB7` |
| Processo Straus7 | `ANTHEA.Strand7Worker` accanto ad `ANTHEA.exe`, senza `St7api.dll` | 4 file del processo, nessun `St7api.dll` |
| Guide | pratica e teorica Rev68 | Rev68 |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno | nessuno |
| Correzioni presenti nell'estratto | tipi nuovi nelle DLL | presenti |
