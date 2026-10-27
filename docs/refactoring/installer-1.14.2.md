# Preparazione di ANTHEA 1.14.2

Richieste dell'utente del 26 ottobre 2026: «tra mezz'ora dovrebbe arrivare una nuova versione di model e cheker. rieffettua il rilascio
con le nuove librerie» e «versione 1.14.2 e 1.14.3 dopo correzioni»; il 27 ottobre «aggiornate librerie». Il contenuto per l'utente è
nelle [note di rilascio](note-rilascio-1.14.2.md).

## Contenuto

- Tutto il contenuto della 1.14.1 (7547fa6c, mai pubblicata): F5.15 e le release AH e AI delle librerie GPC.
- Release AJ delle librerie GPC con le correzioni di calcolo decise il 26/10 sera: R4, R9, R11, BUG-MUR-2b, BUG-FES-1b/1c e la chiave
  di BUG-PO-5b (voci AJ-LIB-01 e AJ-FON-01 del registro delle differenze). ANTHEA adegua testi e prove (bea243b6, ac21a044) e i
  riferimenti (e2a4f888), approvati dall'utente il 27/10: «Approvo i riferimenti aggiornati AJ-LIB-01 della 1.14.2: se la full è
  pulita, unisci, metti il tag v1.14.2 e pubblica.»
- Le correzioni di ANTHEA, le teste dei pali, le combinazioni in Model, la guida Rev60 e gli altri lavori del 26/10 vanno nella 1.14.3.

## Librerie: release AJ

Set ufficiale da Model `2acc60ef` e Checker `4a1b0090`, entrambi su origin/master; Geometry `6734eee1` e Utilities `e57b440b`
invariati. Installato in `lib/Checker` da f614eac5; set in `supporto/artefatti/librerie-20261027-aj/gpc-release-20261027-aj`, con
tutte le suite verdi e DLL identiche al set candidato `candidato-master-4a1b0090` provato nella full della notte. Manifest delle
librerie `1CB28D30…7CBF`. Le versioni delle DLL sono nelle note di rilascio.

## Versione

`<Version>` 1.14.2 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` (aa887e6a): nome del setup, versione file `1.14.2.0`, prodotto
`1.14.2+<commit>`. Istruzioni dell'installer con PDF; originali 1.14.1 in `supporto/SUPERATI/installer-1142-20261027`.

## Prove

Full di rilascio sul commit aa887e6a (`supporto/artefatti/ci/20261027-065047-rilascio-1.14.2-full-aj`, profilo full con
`-Installer`): **PASS 157, KNOWN 0, NEW-FAIL 0** al primo giro, senza ripetizioni. Albero pulito prima e dopo la corsa. Una prima
corsa sul commit precedente (e36a278e, senza l'approvazione nel registro) è stata fermata dopo 4 minuti per mettere l'approvazione nel
commit del setup (`20261027-064331-rilascio-1.14.2-full-aj`, con `INTERROTTA.txt`).

## Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Commit del full = commit del setup, albero pulito | stesso SHA | aa887e6a per entrambi, albero pulito (`source_clean: true`) |
| Full `rilascio-1.14.2-full-aj` | PASS, NEW-FAIL 0 | PASS 157, KNOWN 0, NEW-FAIL 0, nessuna ripetizione |
| Suite installer | PASS | `installer/safety`, `installer/guides`, `installer/examples` PASS |
| `7z t` del setup | nessun errore | nessun errore, 388 file |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.14.2+<commit>` | `1.14.2+aa887e6a0730beef1faa91fd071a90ef758e3224` |
| DLL GPC uguali a `lib/Checker/manifest.json` | 13, set AJ | 13, set AJ, manifest `1CB28D30…7CBF` |
| Guide | pratica e teorica Rev59 | Rev59 |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno | nessuno (`forbidden_files` vuoto) |
| Setup | nome, dimensione, SHA-256 | `ANTHEA-1.14.2-Setup-x64.exe`, 69 453 741 byte, `515EE512D9E3E817DD24C90B5BBAEE92C348AF83350CB08B57567FF60EEF738A` |

## Esito

- **File distribuiti:** 383; file applicativi 327, come nella 1.14.1. Esempi: 52 documenti per 15 moduli.
- **Audit:** `Audit-Installer.py` PASS (`audit-payload.json` accanto al setup in `supporto/artefatti/rilascio-1.14.2/installer`).
- **Pubblicazione:** con l'ok dell'utente («punto 2. hai il mio ok» del 26/10 e l'approvazione del 27/10), tag `v1.14.2` sul commit
  del setup e push di `main` e `refactoring/f5`. La 1.14.1, sostituita dalla 1.14.2, non ha tag; se metterlo resta da chiedere
  all'utente.
