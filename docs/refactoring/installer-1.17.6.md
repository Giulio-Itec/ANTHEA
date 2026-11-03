# Preparazione di ANTHEA 1.17.6

Segnalazione dell'utente del 3 novembre 2026 sulla 1.17.5: «si ferma a 7 combinazioni calcolate. questa cosa va risolta». Scelta
dell'utente «Sì, opzione 1»; poi «non fare il full finale. se funziona prepara direttamente la versione 1.17.6. se funziona la mostro. poi
facciamo la full». La full non è stata eseguita. Il contenuto per l'utente è nelle [note di rilascio](note-rilascio-1.17.6.md).

## Contenuto

- Tutto il contenuto della 1.17.5 (tag `v1.17.5`).
- 18e6b405: con l'archivio dei risultati la copia con le combinazioni arriva a `ModelResultArchive.ArchivedCopyBytes` (2 GiB, provvisorio),
  ogni risultato entro un blocco di 256 MiB:
  - `ModelCombinations.Attach` riceve il limite della copia; senza parametro resta 256 MiB;
  - la vista lo riceve da `CombinationCopyBytes`, e la scheda del modello della shell, che ha sempre l'archivio, le dà 2 GiB;
  - i messaggi citano il limite vero;
  - nuova prova in `CombinationSeriesChecks`.

## Causa

L'Attach teneva l'intera copia entro 256 MiB, il limite del pacchetto di un modello nel documento. Dalla decisione «Blocchi accanto»
(2/11/2026) i risultati delle copie grandi vanno nell'archivio accanto al progetto, un blocco ciascuno, e la copia in memoria non ha più quel
limite. Su un modello sintetico della stessa taglia di quello dell'utente (3456 piastre, 3757 nodi, 49 casi):
- casi base, 165 MiB;
- una lineare, 6,5 MiB;
- un inviluppo, 97 MiB, che veniva rifiutato.

## Librerie

Set ufficiale AU, invariato rispetto alla 1.17.5 (Model `2f934f9f`, Checker `6156e4ce`); manifest `F593B14E…D151F`.

## Versione

`<Version>` 1.17.6 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` (a998caf7). Istruzioni dell'installer aggiornate, con il PDF rigenerato da
`supporto/scripts/documentazione/markdown-pdf.py` e controllato nelle tre pagine. Gli originali sono in
`supporto/SUPERATI/rilascio-1176-20261103`, con manifest e SHA-256. Solo una correzione: per la regola aumenta l'ultimo componente.

## Prove

Il lavoro è stato fatto nei worktree `aw-combo-limite` e `aw-rel-1176`. Le evidenze sono nel checkout principale, in
`supporto/artefatti/combo-limite` (suite mirate) e `supporto/artefatti/rilascio-1.17.6` (rilascio).

- **Suite mirate** sul commit del fix (`combo-limite/mirate`, `mirate-mw`): Architecture.Tests, ModelWorkspace.Checks (con la prova nuova),
  ModelViewer.UiChecks, ModelViewer.Combinations.UiChecks, ModelViewer.LiveImport.UiChecks, ModelViewer.Sections.UiChecks e
  ModelViewer.CombinationBench.Checks PASS.
- **Serie sul modello sintetico** della stessa taglia, dalla vista con un progetto con archivio: 61 combinazioni su 61 in 40,9 s, copia di
  657 MiB; prima rifiuto al primo inviluppo.
- **Modello MIDAS vero**: la lettura dal vivo (48 casi, 213 richieste) si è interrotta più volte per il collegamento API di MIDAS:
  - una richiesta scaduta ha lasciato il relè sfasato di una risposta (NODE riceveva UNIT), azzerato dall'utente ricollegando l'API;
  - poi «MIDAS non è raggiungibile» al passo 76;
  - infine «client does not exist».

  La prova è rimasta da fare.
- **Profilo standard con le suite dell'installer** sul commit della versione a998caf7 (`rilascio-1.17.6/standard`): **PASS 87, NEW-FAIL 0**,
  con `installer/safety`, `installer/guides`, `installer/examples` e `qa/no-test-code`.
- **Full**: non eseguita, per richiesta dell'utente; prevista dopo la sua prova.
- **Setup**, costruito dal commit a998caf7 con albero pulito, con `Crea-Installer.ps1` (`rilascio-1.17.6/installer`):
  - file `ANTHEA-1.17.6-Setup-x64.exe`, 74 046 724 byte;
  - SHA-256 `18DAB25990969659639B46CB4AD1E39C8E3CFA7C808FDBFBAF626BE29DF08D59`;
  - `7z t` senza errori, 435 file;
  - estratto con `7z x` in `installer/estratto`;
  - `Audit-Installer.py` PASS (`audit.log`, `audit-payload.json`): 330 file dell'applicazione, 430 nel pacchetto, set AU, guide Rev68,
    nessun file vietato.

Il tag annotato `v1.17.6` sta sul commit del setup a998caf7 e riporta le note di rilascio. `main` e `refactoring/f5` arrivano al commit di
questo verbale, figlio del commit del setup.

## Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Commit del setup, albero pulito | albero pulito | a998caf7, albero pulito |
| Full di rilascio | non richiesta | non eseguita; profilo standard e installer PASS 87, NEW-FAIL 0 |
| Suite installer | `installer/safety`, `installer/guides`, `installer/examples` PASS | PASS |
| `7z t` del setup | nessun errore | nessun errore, 435 file |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.17.6+<commit>` | `1.17.6+a998caf72250c02af48f7cde4d320c8b435681a7` |
| DLL GPC uguali a `lib/Checker/manifest.json` | tutte quelle del manifest, set AU | 13 su 13 |
| Processo Straus7 | `ANTHEA.Strand7Worker` accanto ad `ANTHEA.exe`, senza `St7api.dll` | 4 file del processo, nessun `St7api.dll` |
| Guide | pratica e teorica Rev68 | Rev68 |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno | nessuno |
| Correzione presente nell'estratto | `ModelResultArchive.ArchivedCopyBytes` in `ANTHEA.ModelWorkspace.dll` | presente |
