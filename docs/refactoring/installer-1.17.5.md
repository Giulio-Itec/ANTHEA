# Preparazione di ANTHEA 1.17.5

Richiesta dell'utente del 3 novembre 2026, dopo la serie delle combinazioni più veloce: «Sì, fai la 1.17.5». Il contenuto per l'utente è
nelle [note di rilascio](note-rilascio-1.17.5.md); il lavoro in [modelli-motore-combinazioni](../sviluppo/modelli-motore-combinazioni.md),
sezione «La serie dopo la 1.17.4».

## Contenuto

- Tutto il contenuto della 1.17.4 (tag `v1.17.4`).
- 8b9289ec..cbd0522a: serie delle combinazioni:
  - controlli della copia, lunghezze del pacchetto, definizioni delle tabelle MIDAS e revisioni delle definizioni fatti una volta per
    riferimento e tenuti dalla copia (e642e23e, regola P3: niente stato statico);
  - prove `CombinationSeriesChecks` e banco `--serie N` di `CombinationTiming`.

## Librerie

Set ufficiale AU, invariato rispetto alla 1.17.4 (Model `2f934f9f`, Checker `6156e4ce`); manifest `F593B14E…D151F`.

## Versione

`<Version>` 1.17.5 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` (4b07b34a): nome del setup, versione file `1.17.5.0`, prodotto
`1.17.5+<commit>`. Istruzioni dell'installer aggiornate, con il PDF rigenerato da `supporto/scripts/documentazione/markdown-pdf.py` e
controllato nelle tre pagine. Gli originali sono in `supporto/SUPERATI/rilascio-1175-20261103`, con manifest e SHA-256. I valori non
cambiano e non ci sono funzioni nuove: per la regola aumenta l'ultimo componente.

## Prove

Il lavoro è stato fatto nei worktree `aw-combo-serie` e `aw-rel-1175`. Le evidenze sono nel checkout principale, in
`supporto/artefatti/combo-serie` (full e misure) e `supporto/artefatti/rilascio-1.17.5` (rilascio).

- **Full** sul commit del contenuto e642e23e (`combo-serie/full`): **PASS 161, NEW-FAIL 0**, compresi i controlli di Straus7 con la
  chiave della licenza. La full precedente, su 8b9289ec (`combo-serie/full-1-p3`), aveva il solo rosso della regola P3 (stato statico
  nuovo), corretto in e642e23e; cbd0522a aggiunge solo la riga del piano.
- **Serie** sul 500 × 100 (`combo-serie/serie-prima-marcatori`, `tempi-dopo-p3`): 120 combinazioni da 98,6 a 5,1 s, impronte uguali
  (valori `342829B4…EDB4`, completa `16AF3A90…D062`, serie `54BB22FC…51BD`).
- **Profilo standard con le suite dell'installer** sul commit della versione 4b07b34a (`rilascio-1.17.5/standard`): **PASS 87, NEW-FAIL 0**, con
  `installer/safety`, `installer/guides`, `installer/examples` e `qa/no-test-code`.
- **Setup**, costruito dal commit 4b07b34a con albero pulito, con `Crea-Installer.ps1` (`rilascio-1.17.5/installer`):
  - file `ANTHEA-1.17.5-Setup-x64.exe`, 74 038 630 byte;
  - SHA-256 `14558B3D90DF61BB225669B3473D569A263644E8E4A201CF2B61DAD2A86C1DA1`;
  - `7z t` senza errori: 435 file;
  - estratto con `7z x` in `installer/estratto`;
  - `Audit-Installer.py` PASS (`audit.log`, `audit-payload.json`): 330 file dell'applicazione, 430 nel pacchetto, 95 documenti e 32 moduli di esempio, set AU, guide Rev68, nessun file vietato.

Il tag annotato `v1.17.5` sta sul commit del setup 4b07b34a e riporta le note di rilascio. `main` e `refactoring/f5` arrivano al commit di
questo verbale, figlio del commit del setup.

## Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Commit del setup, albero pulito | albero pulito | 4b07b34a, albero pulito |
| Full di rilascio | PASS | sul commit del contenuto e642e23e: PASS 161 |
| Suite installer | `installer/safety`, `installer/guides`, `installer/examples` PASS | PASS |
| `7z t` del setup | nessun errore | senza errori: 435 file |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.17.5+<commit>` | `1.17.5+4b07b34a75987f04c7dfd76fdac2bc7687f24e4d`, file `1.17.5.0` |
| DLL GPC uguali a `lib/Checker/manifest.json` | tutte quelle del manifest, set AU | 13 su 13, manifest `F593B14E…D151F` |
| Processo Straus7 | `ANTHEA.Strand7Worker` accanto ad `ANTHEA.exe`, senza `St7api.dll` | 4 file del processo, nessun `St7api.dll` |
| Guide | pratica e teorica Rev68 | Rev68 |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno | nessuno |
| Serie presente nell'estratto | `ModelSnapshot.Checked` e `ModelCombinations.KnownSizes` in `ANTHEA.ModelWorkspace.dll` | presenti |
