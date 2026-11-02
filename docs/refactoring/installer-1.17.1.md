# Preparazione di ANTHEA 1.17.1

Richiesta dell'utente del 2 novembre 2026: «finito tutto merge, commit, nuova versione di anthea», poi «usa 1.17.1» e «non fare la
full finale. fai la versione e installer. la full la facciamo stanotte sulla prossima fix» ([decisioni](decisioni.md), «2 novembre —
criterio resistente per stato limite e 1.17.1»). Il contenuto per l'utente è nelle [note di rilascio](note-rilascio-1.17.1.md).

## Contenuto

- Tutto il contenuto della 1.17.0 (tag `v1.17.0`).
- Lavori entrati in `main` dopo la 1.17.0: sei fogli Azioni aggiuntivi e set AR; foglio Viscosità e maturazione; dati delle
  sovrapposizioni e set AS; archivio dei risultati dei modelli e «Copie precedenti»; assistenza grafica dei fogli Azioni e guide Rev67.
- Criterio resistente per stato limite della sezione c.a. (voce CA-CRITERIO-STATI-01), ramo `refactoring/ca-criterio-stati`.
- Guide Rev68 (ramo `refactoring/ca-criterio-rev68`).
- Set ufficiale di librerie AT (ramo `refactoring/lib-at`, sessione delle prestazioni di GPCModel, full PASS 161).

## Librerie: release AT

Set ufficiale da Model `5449e4d1` e Checker `a2acd3d2` su origin/master; Geometry `6734eee1` e Utilities `e57b440b` invariati. GPCModel
5.37.0.0 e le altre DLL come in `lib/Checker/README.md` («Release AT»); manifest `2234729D…9BB7`.

## Versione

`<Version>` 1.17.1 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` (63a7f306): nome del setup, versione file `1.17.1.0`, prodotto
`1.17.1+<commit>`. Istruzioni dell'installer aggiornate, con il PDF; gli originali sono in `supporto/SUPERATI/rilascio-1171-20261102`.
Funzioni nuove, quindi la regola darebbe 1.18.0: il numero è quello scelto dall'utente.

## Prove

Nessuna full di rilascio, per scelta dell'utente: è prevista nella notte, con la correzione successiva.
- **Profilo standard** sul commit della versione 63a7f306 (`supporto/artefatti/ci/20261102-125217-rilascio-1171-standard`): **PASS 84,
  KNOWN 0, NEW-FAIL 0**.
- **Suite WPF toccate** (`…-130609-rilascio-1171-ui`), 18 suite della sezione c.a., F3, shell, Wiki, esempi, Azioni e ModelViewer: 17 PASS;
  `ui/f3-equivalence` aveva le sole differenze editoriali della Wiki Rev68, registrate in cd4bd5d7, poi PASS
  (`…-132403-rilascio-1171-f3`).
- **Suite dell'installer** sul commit del setup cd4bd5d7 (`…-132658-rilascio-1171-installer`): `installer/safety`, `installer/guides` e
  `installer/examples` PASS.
- **Modifica del c.a.**: profilo standard e stadio `ui` completo sul ramo prima dei merge (`…-105521-criterio-stati-1`,
  `…-111306-criterio-stati-ui-1`), riferimenti ricatturati dopo il confronto riga per riga e suite ripassate (`…-121706-criterio-stati-ui-2`,
  `…-122940-criterio-stati-ui-3`, `…-123857-criterio-stati-registrazione`).
- **Setup**, costruito dal commit cd4bd5d7 con `Crea-Installer.ps1` (`supporto/artefatti/rilascio-1.17.1/installer`):
  - file `ANTHEA-1.17.1-Setup-x64.exe`, 74 005 200 byte;
  - SHA-256 `F1B453B4BC0FE14D6E6AB0BC4346D7389F1F0D959C59F77D316211FF6EF26C6D`;
  - `7z t` senza errori: 435 file;
  - `Audit-Installer.py` PASS (`audit.log`, `audit-payload.json`): 330 file dell'applicazione, 430 nel pacchetto, 95 documenti e 32
    moduli di esempio.

Il tag annotato `v1.17.1` sta sul commit del setup cd4bd5d7 e riporta le note di rilascio. `main` e `refactoring/f5` arrivano al commit di
questo verbale, figlio del commit del setup.

## Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Commit del setup, albero pulito | albero pulito | cd4bd5d7, albero pulito |
| Full di rilascio | rinviata dall'utente | non eseguita; profilo standard PASS 84, NEW-FAIL 0 |
| Suite installer | `installer/safety`, `installer/guides`, `installer/examples` PASS | PASS |
| `7z t` del setup | nessun errore | nessun errore |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.17.1+<commit>` | `1.17.1+cd4bd5d7ccd70e2de7ddc63afb7cf2e64015e2fb` |
| DLL GPC uguali a `lib/Checker/manifest.json` | tutte quelle del manifest, set AT | 13 su 13, manifest `2234729D…9BB7` |
| Processo Straus7 | `ANTHEA.Strand7Worker` accanto ad `ANTHEA.exe`, senza `St7api.dll` | 4 file del processo, nessun `St7api.dll` |
| Guide | pratica e teorica Rev68 | Rev68 |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno | nessuno |
