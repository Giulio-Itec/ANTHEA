# Preparazione di ANTHEA 1.19.0

Decisione dell'utente dell'8 novembre 2026, alla domanda «Pubblico ANTHEA 1.19.0 prima, appena F7e e il full sono verdi?»: «Sì, ANTHEA
prima». La 1.19.0 esce quindi prima dei plug-in Rhino (BridGHe 0.4.0, Arachne 0.4.0 e ReBear 0.3.4 a fine novembre, perché condividono
un processo di Rhino), con le librerie dai master pubblicati come vuole la regola dell'utente. Il contenuto per l'utente è nelle
[note di rilascio](note-rilascio-1.19.0.md). Le corsie del rilascio (F7d set candidato e full, F7e decisioni dell'utente sui riferimenti,
R119 pubblicazione) sono descritte in `gpc-unification-20261102/revisione-claude-20261103/correzioni-F7d.md`.

## Contenuto

- Tutto il contenuto della 1.18.0 (tag `v1.18.0`) e di `main` fino a `8ef58180`: progetti (blocco dei file, copie di recupero,
  salvataggio in background, stato dei calcoli), guide Rev69 e Rev70, grafici e messaggi senza «Premere Calcola», pubblicazione dei fogli
  in un giro successivo.
- Migrazione comune GPC: GPC.Archive al posto della copia di ANTHEA (F6 L3/L4), sezioni c.a. nel formato compatto con i documenti di
  versione 5 (`GPC-F7A-01`).
- `codex/anthea-1190-integrazione` `7bfb3a95` (ChatGPT, 7/11): copertina con un foglio Materiali senza dati (`6b774b90`).
- `refactoring/report-template` `dc1b8d24`, unito in `95242989`: template di stili dei report, duttilità nella relazione c.a., avvisi dei
  pali verticali, guide Rev71.
- F7e: lunghezze in mm nella sezione efficace di classe 4 della relazione della sezione composta (`0b31758b`); riferimenti delle prove
  rinnovati con l'ok dell'utente (`4fb66e8c`, `a3d9b3a8`, `bf255331`; voci FON-HANSEN-02, SM-CB131-ARROTONDAMENTI,
  REPORT-LUNGHEZZE-MM-20261108, REPORT-TEMPLATE-CURVA-20261108).

## Librerie

Set ufficiale **AV** `gpc-release-20261108-av` (`gpc-f6-claude/F7/sets`), da Model `ff318f87` e Checker `9e513417`, portati su
origin/master l'8/11 in avanti veloce da `2f934f9f` e `b4080622`; Geometry `6734eee1` e Utilities `e57b440b` invariati. Prodotto con
`Release-Set.ps1 -Previous` rispetto ad AU: Model Core 63, UnitTest 1950 (7 ignorati, prove Straus7 native comprese) e .NET Framework
141; Checker 10.822 con Concrete net472 988 (contratto K0 PASS); API solo con le 26 rimozioni approvate di GPCModel. I 34 file del set
(15 DLL con PDB e XML) sono identici byte per byte a quelli del set candidato F7d; `release-set.json` cambia solo in `candidate`, data,
`previous` e `onOriginMaster` (manifest del set `D004291C…30CE`). Installato con `Update-Snapshot.ps1 -ReleaseSet ... -Install` in
1aea70dc: nessuna DLL di `lib/Checker` cambia, cambiano `manifest.json` (`453B766B…1C8C`), `release-set.json` e la provenienza F4
(release AV).

## Versione

`<Version>` 1.19.0 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` dal 4/11 (18d96c67). Nel commit del setup 12657b91 le istruzioni
dell'installer passano a 1.19.0, con il PDF rigenerato da `supporto/scripts/documentazione/markdown-pdf.py` e controllato nelle tre
pagine; gli originali sono in `supporto/SUPERATI/rilascio-1190-20261108`, con manifest e SHA-256. Funzioni nuove e documenti che la 1.18.0
non apre: per la regola aumenta il secondo componente.

## Prove

Worktree `gpc-f6-claude/F7/ANTHEA-f7d`, branch `claude/anthea-1190-rilascio`; corse in `supporto/artefatti/ci` del worktree, riepiloghi
copiati in `gpc-f6-claude/F7/results/ci-summaries/20261108-*r119*`.

- **Full di rilascio** `20261108-094903-r119-full` (`build\ci.ps1 -Profile full -Installer -Keep`, commit 12657b91, albero pulito prima e
  dopo, 85 min 41 s, 96 DPI): **PASS 166, NEW-FAIL 0, KNOWN 0, BLOCKED 0** (build 33, fast 42, regression 16, wiki 2, ui 73). Non
  eseguite come sempre: ElasticPile.Checks, BridgeValidationCurrent, ElasticPile.Performance.
  - Confronto suite per suite con la full F7d sul set candidato: 158 suite uguali, 8 diverse, cioè le 7 rosse di F7d ora verdi
    (Architecture.Tests 618 controlli con la provenienza F4 della release AV, Presentation.Tests 5143, FoundationWorkspace.Checks e
    UiChecks, `ui/f3-equivalence`, `ui/check-sezione-mista-view` 124, `ui/check-sezione-view` 234) e Reports.Tests 496 → 515 (prova del
    formato delle lunghezze di F7e).
  - `installer/safety`, `installer/guides` (35) e `installer/examples` (206) PASS; `ui/check-contrast` zero difetti nei tre temi.
- **Suite native Straus7**, sotto il blocco condiviso con la chiave presente e Rhino chiuso:
  - `20261108-111511-r119-full-straus7` (`GPC_STRAUS7_ELEMENTS=versioned`): `Straus7Native.Checks` «PASS 29 controlli nativi Straus7 · 1
    modelli» e `ModelViewer.Importers.UiChecks` con il worker reale, PASS 2;
  - `20261108-111546-r119-native-2` (ambiente F7b con `STAGES` e il pilota BridGHe di F4): «PASS 113 controlli nativi Straus7 · 8 modelli».
  - Riepiloghi uguali a quelli di F7d.
- **Setup**, costruito dal commit 12657b91 con albero pulito, con `Crea-Installer.ps1 -OutputDirectory`
  (`gpc-f6-claude/F7/results/r119-rilascio/installer`):
  - file `ANTHEA-1.19.0-Setup-x64.exe`, 74 260 513 byte;
  - SHA-256 `9BE3AA84EE741C254920C46FA157D8A2C6007F7DA4EA973D6582C5CDB421C4B4`;
  - `7z t` senza errori, 437 file; estratto con `7z x` in `installer/estratto`;
  - `Audit-Installer.py` PASS: 332 file dell'applicazione, 432 nel pacchetto, 14 DLL GPC uguali al manifest della release AV, guide
    Rev71, 95 esempi, nessun file vietato, processo Straus7 completo.

Il tag annotato `v1.19.0` sta sul commit del setup 12657b91 e riporta le note di rilascio. `main` arriva al commit di questo verbale,
figlio del commit del setup.

## Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Librerie da origin/master | set non candidato, repository su origin/master | AV, `candidate=false`, Model `ff318f87` e Checker `9e513417` su origin/master |
| DLL del set uguali al candidato provato | 34 file con lo stesso SHA-256 | 34 su 34 |
| Commit del setup, albero pulito | albero pulito | 12657b91, albero pulito |
| Full di rilascio | tutte PASS, compresa `ANTHEA.Architecture.Tests` | PASS 166, NEW-FAIL 0 |
| Suite native Straus7 | PASS | 29 + 113 controlli nativi, `ModelViewer.Importers.UiChecks` PASS |
| Suite installer | `installer/safety`, `installer/guides`, `installer/examples` PASS | PASS |
| `7z t` del setup | nessun errore | nessun errore, 437 file |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.19.0+<commit>` | `1.19.0+12657b91cbaabd9d6e39c225f15c3544ea3e437b` |
| DLL GPC uguali a `lib/Checker/manifest.json` | tutte quelle del manifest, release AV | 14 su 14 |
| Processo Straus7 | `ANTHEA.Strand7Worker` accanto ad `ANTHEA.exe`, senza `St7api.dll` | 4 file del processo, nessun `St7api.dll` |
| Guide | pratica e teorica Rev71 | Rev71 |
| Gmsh, UnsafeEx, GPCModelChecker, assembly di prova o legacy | nessuno | nessuno |
| Template dei report nell'estratto | `ReportTemplateStore` in `ANTHEA.Reports.dll` | presente |
