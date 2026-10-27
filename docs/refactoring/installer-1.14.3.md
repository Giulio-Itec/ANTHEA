# Preparazione di ANTHEA 1.14.3

Richieste dell'utente del 27 ottobre 2026: «mandami il percorso della 1.14.3 quando lo hai fatto» e, alla domanda se fare tutti i punti
o solo i pali, «solo i pali ma non fare la full. fai solo le viste interessate. poi push e versione». Il contenuto per l'utente è nelle
[note di rilascio](note-rilascio-1.14.3.md).

## Contenuto

- Tutto il contenuto della 1.14.2 (aa887e6a, tag `v1.14.2`).
- BUG-PO-5b in ANTHEA: sviluppo parziale delle barre del palo elastico (0b8bfd66), riferimenti 3c3babb5 approvati dall'utente il
  27/10.
- Testa dei pali e dei micropali sotto il piano campagna (PO-TESTA-01): e3fa4a01, 0af50802, riferimenti f860a3d8 ed e00ef273
  approvati il 27/10 («Approvo i riferimenti PO-TESTA-01 (f860a3d8 ed e00ef273). Registra la decisione.»); prova smoke del palo
  orizzontale con il fuoco logico della cella (8f0ff4b7).
- Gli altri lavori del 26/10 passano alla 1.14.4 ([piano](piano.md), «Lavori della 1.14.4»).

## Librerie

Release AJ, la stessa della 1.14.2: Model `2acc60ef`, Checker `4a1b0090`, manifest `1CB28D30…7CBF`. `lib/Checker` invariata.

## Versione

`<Version>` 1.14.3 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` (0c17faf9): nome del setup, versione file `1.14.3.0`, prodotto
`1.14.3+<commit>`. Istruzioni dell'installer con PDF; originali 1.14.2 in `supporto/SUPERATI/installer-1143-20261027`.

## Prove

Su indicazione dell'utente niente full: le prove delle viste dei pali con l'installer, sul commit del setup.

- Ramo, prima della versione: full `supporto/artefatti/1143-teste/full-1` sul commit e00ef273, 154 PASS e ui/check-contrast fuori
  misura a 144 DPI; ripetuta a 96 DPI in `1143-teste/contrasto-3`: PASS (Chiara 643 su 645, le 2 occorrenze in meno vengono da
  BUG-PO-5b, voce PO-TESTA-01).
- Commit del setup 0c17faf9, `supporto/artefatti/rilascio-1.14.3/viste-pali` (profilo full con `-Installer`, suite scelte con
  `-Only`): PASS 17 e 1 NEW-FAIL, `ui/check-palo-orizzontale-view`. «Esporta PNG…» della tavola delle armature differiva
  dall'immagine attesa in un solo pixel (48, 1865), di 9 livelli su 255: antialiasing del disegno, non contenuto. Ripetuta da
  sola sullo stesso commit (`viste-pali-2`, `-NoBuild`): PASS. Seguito: il confronto byte per byte del PNG è fragile.
- Suite passate: verifiche/testa-interrata, installer/safety, installer/guides, installer/examples, Architecture.Tests,
  Presentation.Tests, ui/smoke-horizontal, ui/check-palo-verticale-view, ui/check-palo-orizzontale-view (ripetuta),
  ElasticPile.UiChecks con anello-interno e nota-stress-block.

## Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Commit delle prove = commit del setup, albero pulito | stesso SHA | 0c17faf9 per entrambi, albero pulito (`source_clean: true`) |
| Prove delle viste dei pali | PASS, NEW-FAIL 0 | PASS 17 più la ripetizione di ui/check-palo-orizzontale-view |
| Suite installer | PASS | `installer/safety`, `installer/guides`, `installer/examples` PASS |
| `7z t` del setup | nessun errore | nessun errore, 388 file |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.14.3+<commit>` | `1.14.3+0c17faf9663508e7d310bf7050ab9cbd303a626c` |
| DLL GPC uguali a `lib/Checker/manifest.json` | 13, set AJ | 13, set AJ, manifest `1CB28D30…7CBF` |
| Guide | pratica e teorica Rev59 | Rev59 |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno | nessuno (`forbidden_files` vuoto) |
| Setup | nome, dimensione, SHA-256 | `ANTHEA-1.14.3-Setup-x64.exe`, 69 471 444 byte, `7CB457F1C32E0984ED09FB2CAEA0E14A3C876D178C80959E6C31BBC456F3F4D0` |

## Esito

- **File distribuiti:** 383; file applicativi 327, come nella 1.14.2. Esempi: 52 documenti per 15 moduli.
- **Audit:** `Audit-Installer.py` PASS (`audit-payload.json` accanto al setup in `supporto/artefatti/rilascio-1.14.3/installer`).
- **Pubblicazione:** con l'ok dell'utente («poi push e versione»), tag `v1.14.3` sul commit del setup e push di `main` e
  `refactoring/f5`.
