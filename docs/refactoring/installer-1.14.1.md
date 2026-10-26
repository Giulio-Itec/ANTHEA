# Preparazione di ANTHEA 1.14.1

Richiesta dell'utente del 26 ottobre 2026: «fai versione 1.14.1. crea installer e manda percorso». Il contenuto per l'utente è nelle
[note di rilascio](note-rilascio-1.14.1.md).

## Contenuto

Tutto il lavoro entrato in `refactoring/f5` dopo il tag `v1.14.0` (d7e6b6a7):
- F5.15, revisione delle scelte di F5, chiusa il 26/10 (passi a–f del [piano](piano.md));
- release AH e AI delle librerie GPC (voci AH-FON-01, AI-FON-01, R10, R12, R13, R19, R21 del registro delle differenze).

## Librerie: release AI

Set ufficiale da Model `2acc60ef` e Checker `799a6355`, entrambi su origin/master; Geometry `6734eee1` e Utilities `e57b440b`
invariati. Installato in `lib/Checker` da 38390d94; set in `supporto/artefatti/librerie-20261026-ai/gpc-release-20261026-ai`.
Manifest delle librerie `267492A7…ABE4`. Le versioni delle DLL sono nelle note di rilascio.

## Versione

`<Version>` 1.14.1 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` (7547fa6c): nome del setup, versione file `1.14.1.0`, prodotto
`1.14.1+<commit>`. Istruzioni dell'installer con PDF; originali 1.14.0 in `supporto/SUPERATI/installer-1141-20261026`. Il numero è
quello scelto dall'utente, anche se il rilascio porta funzioni nuove (impostazione del separatore decimale) e calcoli cambiati.

## Prove

Full di rilascio sul commit 7547fa6c (`supporto/artefatti/ci/20261026-193012-rilascio-1.14.1-full-ai`, profilo full con `-Installer`):
**PASS 157, KNOWN 0, NEW-FAIL 0** al primo giro, senza ripetizioni. Albero pulito prima della corsa.

## Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Commit del full = commit del setup, albero pulito | stesso SHA | 7547fa6c per entrambi, albero pulito (`source_clean: true`) |
| Full `rilascio-1.14.1-full-ai` | PASS, NEW-FAIL 0 | PASS 157, KNOWN 0, NEW-FAIL 0, nessuna ripetizione |
| Suite installer | PASS | `installer/safety`, `installer/guides`, `installer/examples` PASS |
| `7z t` del setup | nessun errore | nessun errore, 388 file |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.14.1+<commit>` | `1.14.1+7547fa6c8fa8a3b89a85203f8758c2df87f795a2` |
| DLL GPC uguali a `lib/Checker/manifest.json` | 13, set AI | 13, set AI, manifest `267492A7…ABE4` |
| Guide | pratica e teorica Rev59 | Rev59 |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno | nessuno (`forbidden_files` vuoto) |
| Setup | nome, dimensione, SHA-256 | `ANTHEA-1.14.1-Setup-x64.exe`, 69 419 758 byte, `F95591A08F7ED14EAB4307702271428F6DF39EF087ABF4E5A9E44E241FDF6C53` |

## Esito

- **File distribuiti:** 383; file applicativi 327, come nella 1.14.0. Esempi: 52 documenti per 15 moduli.
- **Audit:** `Audit-Installer.py` PASS (`audit-payload.json` accanto al setup in `supporto/artefatti/rilascio-1.14.1/installer`).
- **Pubblicazione:** il tag `v1.14.1` sul commit del setup e il push di `main` e `refactoring/f5` attendono l'ok dell'utente.
