# Preparazione di ANTHEA 1.15.0

Decisioni dell'utente del 28 ottobre 2026 sera ([decisioni](decisioni.md), «28 ottobre (sera) — decisioni della 1.15.0»): «Anche steel e
model-prepare» per le librerie e «Tag e push se pulita» per il rilascio (versione 1.15.0, full con l'installer, setup, audit; se pulita tag
`v1.15.0` sul commit del setup, merge in `main`, push di `main` e `refactoring/f5`). Il contenuto per l'utente è nelle
[note di rilascio](note-rilascio-1.15.0.md).

## Contenuto

- Tutto il contenuto della 1.14.4 (8a8ceacf, tag `v1.14.4`).
- Importatori del modulo Modelli (407bf0e3): file di testo MIDAS Civil .mct e Gen .mgt (MOD-31) e modelli Straus7 in un processo separato
  (MOD-32); worker Straus7 nel setup senza sostituire le DLL di ANTHEA (7fbd13de).
- Scheda «08 Colonna modello» della sezione c.a. (2d4db2f6) e correzioni dopo la full sul set candidato b (b892cae4): CA-COLONNA-01,
  UI-CONTRASTO-02.
- Guide pratica e teorica Rev61 (13674454), transizioni editoriali della Wiki F5-REL-1150-01.
- Decisioni della sera e riferimenti approvati (5305ceb5): «Approva tutti», «Tieni la correzione», «Convenzione unica», «Accetta».

## Librerie: release AL

Set ufficiale da Model `02027760` e Checker `0951cbf9`, entrambi su origin/master, con i rami della 1.15.0 uniti alle fasi della revisione
steel; Geometry `6734eee1` e Utilities `e57b440b` invariati. Installato in `lib/Checker` da d0eb5492. Manifest delle librerie
`31780AE7…8DD2`. Le versioni delle DLL sono in `lib/Checker/README.md` («Release AL»). Le impronte delle fondazioni superficiali cambiano
solo per la versione di Geotechnics 0.25.0.4 (B-FON-01, `seguito_al`).

## Versione

`<Version>` 1.15.0 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` (5d5185ba): nome del setup, versione file `1.15.0.0`, prodotto
`1.15.0+<commit>`. Istruzioni dell'installer con PDF; originali 1.14.4 in `supporto/SUPERATI/installer-1150-20261028`.

## Prove

Suite mirate sul set AL prima del commit di versione (`supporto/artefatti/1150/al-mirate`): PASS 36. Full di rilascio sul commit
5d5185ba (`supporto/artefatti/rilascio-1.15.0/full`, profilo full con `-Installer` e `GPC_STRAUS7_ELEMENTS=versioned`): **PASS 160,
KNOWN 0, NEW-FAIL 0** al primo giro, senza ripetizioni. Non eseguite come nelle full precedenti ElasticPile.Checks e
BridgeValidationCurrent (passano nei test di libreria) ed ElasticPile.Performance (misura dei tempi). Setup costruito poi dallo stesso
commit con `Crea-Installer.ps1` (`supporto/artefatti/rilascio-1.15.0/installer`), albero pulito.

## Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Commit del full = commit del setup, albero pulito | stesso SHA | 5d5185ba per entrambi, albero pulito (`source_clean: true`) |
| Full `rilascio-1.15.0/full` | PASS, NEW-FAIL 0 | PASS 160, KNOWN 0, NEW-FAIL 0, nessuna ripetizione |
| Suite installer | PASS | `installer/safety`, `installer/guides`, `installer/examples` PASS |
| `7z t` del setup | nessun errore | nessun errore, 392 file |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.15.0+<commit>` | `1.15.0+5d5185ba885eeed673d5cd02ffc2f2be9fea5633` |
| DLL GPC uguali a `lib/Checker/manifest.json` | 13, set AL | 13, set AL, manifest `31780AE7…8DD2` |
| Processo Straus7 | `ANTHEA.Strand7Worker` accanto ad `ANTHEA.exe`, senza `St7api.dll` | 4 file del worker, `St7api.dll` assente |
| Guide | pratica e teorica Rev61 | Rev61 |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno | nessuno (`forbidden_files` vuoto) |
| Setup | nome, dimensione, SHA-256 | `ANTHEA-1.15.0-Setup-x64.exe`, 69 666 285 byte, `B3008285118FE95E555E8D7E1B400FB30536F890B484FA98B9B63B906CF0E991` |

## Esito

- **File distribuiti:** 387; file applicativi 331, quattro in più della 1.14.4 (il processo `ANTHEA.Strand7Worker`). Esempi: 52 documenti
  per 15 moduli.
- **Audit:** `Audit-Installer.py` PASS (`audit-payload.json` accanto al setup in `supporto/artefatti/rilascio-1.15.0/installer`).
- **Pubblicazione:** con l'ok dell'utente («Tag e push se pulita»), tag `v1.15.0` sul commit del setup e push di `main` e
  `refactoring/f5`.
