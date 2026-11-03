# Preparazione di ANTHEA 1.18.0

Richiesta dell'utente del 3 novembre 2026, dopo la prova della 1.17.6: «comunque non riesce a gestire ancora tutte le combo. riesci a mettere
un pannello di gestione limite? o qualcosa del genere», poi «è il tetto dei 2 GB». La full non è stata eseguita: per la 1.17.6 l'utente
l'ha chiesta dopo la sua prova, e così resta. Il contenuto per l'utente è nelle [note di rilascio](note-rilascio-1.18.0.md).

## Contenuto

- Tutto il contenuto della 1.17.6 (tag `v1.17.6`).
- 9aa5afcf: pannello «Spazio delle combinazioni» nella gestione delle combinazioni, con l'archivio dei risultati:
  - spazio occupato dalla copia rispetto al limite;
  - limite scelto fra quelli che la memoria consente (`ModelResultArchive.ArchivedCopyLimits`: 2 GiB sempre, 4, 8 e 16 GiB con almeno 6 byte
    di memoria per byte di limite), ricordato in `%LOCALAPPDATA%\ANTHEA\combinazioni.json`;
  - «Libera selezionate» e «Libera tutte» (`ModelCombinations.DiscardResults`);
  - `ModelDocumentEntry.Prepare` che riusa i blocchi dei risultati già preparati (`ModelSnapshot.ResultBlocks`) e calcola l'impronta della
    copia dai blocchi, pochi alla volta;
  - pubblicazioni della serie distanziate di almeno 4 volte la durata dell'ultima;
  - prove in `CombinationSeriesChecks` e `CombinationSpaceRenderingChecks`.

## Misure

Modello sintetico da 3456 piastre, 3757 nodi e 49 casi, limite 4 GiB (`supporto/artefatti/combo-spazio`, sonda provvisoria):
- «Calcola tutte»: 581 combinazioni su 875 in 253,6 s, poi stop al limite con il messaggio nuovo;
- copia di 4093 MiB, memoria gestita 7,7 GB, picco del processo 18 GB, da cui il rapporto di 6;
- preparazione della copia da 4 GiB 2,4 s, con i blocchi riusati; prima del cambiamento, 1,6 s per una copia di 657 MiB, con tutto il JSON
  in memoria.

## Librerie

Set ufficiale AU, invariato rispetto alla 1.17.6 (Model `2f934f9f`, Checker `6156e4ce`); manifest `F593B14E…D151F`.

## Versione

`<Version>` 1.18.0 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` (2efb5ab0). Istruzioni dell'installer aggiornate, con il PDF rigenerato da
`supporto/scripts/documentazione/markdown-pdf.py` e controllato nelle tre pagine. Gli originali sono in
`supporto/SUPERATI/rilascio-1180-20261103`, con manifest e SHA-256. Funzione nuova: per la regola aumenta il secondo componente.

## Prove

Il lavoro è stato fatto nei worktree `aw-combo-spazio` e `aw-rel-1180`. Le evidenze sono nel checkout principale, in
`supporto/artefatti/combo-spazio` (suite mirate e catture) e `supporto/artefatti/rilascio-1.18.0` (rilascio).

- **Suite mirate** sul commit del pannello (`combo-spazio/mirate2`, `mirate3`, `mirate4`): PASS 18 nella corsa più ampia, NEW-FAIL 0. Le
  suite:
  - Architecture.Tests;
  - ModelWorkspace.Checks;
  - Presentation.Tests;
  - le suite UI del modulo Modelli (vista, combinazioni con il pannello, lettura dal vivo, importatori, sezioni, banco delle combinazioni);
  - `ui/shell-smoke-projects` e `ui/shell-smoke-project-workspace`.
- **Catture** del pannello in `combo-spazio/mirate4/ModelViewer.Combinations.UiChecks/combinazioni-spazio-Light.png` e `-Dark.png`.
- **Profilo standard con le suite dell'installer** sul commit della versione 2efb5ab0 (`rilascio-1.18.0/standard`): **PASS 87, NEW-FAIL 0**,
  con `installer/safety`, `installer/guides`, `installer/examples` e `qa/no-test-code`.
- **Full**: non eseguita; prevista dopo la prova dell'utente.
- **Setup**, costruito dal commit 2efb5ab0 con albero pulito, con `Crea-Installer.ps1` (`rilascio-1.18.0/installer`):
  - file `ANTHEA-1.18.0-Setup-x64.exe`, 74 038 987 byte;
  - SHA-256 `330D52D858079E870749314A480F8128782270C9A498F61469161722E0A3B613`;
  - `7z t` senza errori, 435 file;
  - estratto con `7z x` in `installer/estratto`;
  - `Audit-Installer.py` PASS: 330 file dell'applicazione, 430 nel pacchetto, set AU, guide Rev68, nessun file vietato.

Il tag annotato `v1.18.0` sta sul commit del setup 2efb5ab0 e riporta le note di rilascio. `main` e `refactoring/f5` arrivano al commit di
questo verbale, figlio del commit del setup.

## Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Commit del setup, albero pulito | albero pulito | 2efb5ab0, albero pulito |
| Full di rilascio | non richiesta | non eseguita; profilo standard e installer PASS 87, NEW-FAIL 0 |
| Suite installer | `installer/safety`, `installer/guides`, `installer/examples` PASS | PASS |
| `7z t` del setup | nessun errore | nessun errore, 435 file |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.18.0+<commit>` | `1.18.0+2efb5ab011f899c08e5014ff0c47d53f741346af` |
| DLL GPC uguali a `lib/Checker/manifest.json` | tutte quelle del manifest, set AU | 13 su 13 |
| Processo Straus7 | `ANTHEA.Strand7Worker` accanto ad `ANTHEA.exe`, senza `St7api.dll` | 4 file del processo, nessun `St7api.dll` |
| Guide | pratica e teorica Rev68 | Rev68 |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno | nessuno |
| Pannello presente nell'estratto | `FreeAllCombinationResults` in `ANTHEA.ModelViewer.Presentation.dll` | presente |
