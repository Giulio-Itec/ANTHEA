# Preparazione di ANTHEA 1.17.2

Richiesta dell'utente del 2 novembre 2026, dopo le prove della 1.17.1 nel modulo Modelli: «Sì, 1.17.2», per provare le correzioni. Per
la 1.17.1 l'utente aveva chiesto «voglio testarla io. non fare girare il full»: anche qui la full non è stata eseguita. Il contenuto per
l'utente è nelle [note di rilascio](note-rilascio-1.17.2.md).

## Contenuto

- Tutto il contenuto della 1.17.1 (tag `v1.17.1`).
- c901c09b: terne GPC nella lettura dal vivo di MIDAS (`MidasLiveNormalization`), aggiornamento «Solo risultati» delle copie lette prima,
  pressioni uniformi scritte per vertice e direzione VECTOR nei simboli dei carichi.
- d668aa92: spessori delle piastre nella finestra delle sezioni e nell'ispettore.
- 62c14ccb: «Calcola tutte» e «Calcola selezionate» nelle combinazioni.

## Librerie

Set ufficiale AT, invariato rispetto alla 1.17.1 (Model `5449e4d1`, Checker `a2acd3d2`); manifest `2234729D…9BB7`.

## Versione

`<Version>` 1.17.2 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` (dbacb9d6): nome del setup, versione file `1.17.2.0`, prodotto
`1.17.2+<commit>`. Istruzioni dell'installer aggiornate, con il PDF; gli originali sono in `supporto/SUPERATI/rilascio-1172-20261102`.
Funzioni nuove, quindi la regola darebbe 1.18.0: il numero è quello scelto dall'utente.

## Prove

Nessuna full di rilascio.
- **Suite del modulo Modelli** sul ramo, codice uguale a quello della versione (`supporto/artefatti/modelli-terne/mirate`): **PASS 25,
  NEW-FAIL 0**: ModelWorkspace.Checks (703 controlli), le cinque ModelViewer, le sei shell-smoke, `ui/check-module-host-offscreen`,
  Architecture, Presentation e Application.
- **Profilo standard con le suite dell'installer** sul commit della versione dbacb9d6 (`supporto/artefatti/rilascio-1.17.2/standard`):
  PASS 85, NEW-FAIL 2. `wiki/indice` e `wiki/manuale` cercavano le figure in `supporto/artefatti/guide_anthea_itec_rev03`, ingressi non
  versionati assenti nel worktree nuovo. Copiate le cartelle delle figure dal checkout principale, le due suite ripetute da sole sono PASS
  (`…/rilascio-1.17.2/wiki`). Totale **PASS 87, NEW-FAIL 0**, con `installer/safety`, `installer/guides` e `installer/examples`.
- **Setup**, costruito dal commit dbacb9d6 con `Crea-Installer.ps1` (`supporto/artefatti/rilascio-1.17.2/installer`):
  - file `ANTHEA-1.17.2-Setup-x64.exe`, 74 019 880 byte;
  - SHA-256 `E12F691F23BDDE81000C7776BE3A940FDCC92BE4DEFF3714274E4D6C45B8B3D3`;
  - `7z t` senza errori: 435 file;
  - `Audit-Installer.py` PASS (`audit.log`, `audit-payload.json`): 330 file dell'applicazione, 430 nel pacchetto, 95 documenti e 32
    moduli di esempio.

Il tag annotato `v1.17.2` sta sul commit del setup dbacb9d6 e riporta le note di rilascio. `main` e `refactoring/f5` arrivano al commit di
questo verbale, figlio del commit del setup.

## Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Commit del setup, albero pulito | albero pulito | dbacb9d6, albero pulito |
| Full di rilascio | non richiesta | non eseguita; profilo standard e installer PASS 87, NEW-FAIL 0 |
| Suite installer | `installer/safety`, `installer/guides`, `installer/examples` PASS | PASS |
| `7z t` del setup | nessun errore | nessun errore |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.17.2+<commit>` | `1.17.2+dbacb9d661079b82753c167d5aa658d85bec7b95` |
| DLL GPC uguali a `lib/Checker/manifest.json` | tutte quelle del manifest, set AT | 13 su 13, manifest `2234729D…9BB7` |
| Processo Straus7 | `ANTHEA.Strand7Worker` accanto ad `ANTHEA.exe`, senza `St7api.dll` | 4 file del processo, nessun `St7api.dll` |
| Guide | pratica e teorica Rev68 | Rev68 |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno | nessuno |
