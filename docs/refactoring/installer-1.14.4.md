# Preparazione di ANTHEA 1.14.4

Richieste dell'utente del 28 ottobre 2026: «stasera consegnami l'installer della 1.14.4» e «ok tag v1.14.4 e push di main e
refactoring/f5 se la full di rilascio è pulita». Il contenuto per l'utente è nelle [note di rilascio](note-rilascio-1.14.4.md).

## Contenuto

- Tutto il contenuto della 1.14.3 (0c17faf9, tag `v1.14.3`).
- Lavori della 1.14.4 ([piano](piano.md), «Lavori della 1.14.4»): combinazioni dei muri dal generatore di Model con le decisioni D4 b e
  D5 b (COMB-MODEL-01, COMB-MURI-D4D5), tension stiffening con il modello del tirante (BUG-TS-2), ramo incrudente dell'acciaio di
  progetto (R18), secondo anello nei report del palo orizzontale (PO-ANELLO-01), contrasto del tema Chiaro (UI-CONTRASTO-01), nome del
  modello di Broms con la profondità della testa, «Esporta PNG…» confrontato a meno di un pixel di antialiasing, guide Rev60
  (F5-REL-1144-01).
- Riferimenti approvati in anticipo dall'utente il 28/10 («ti approvo già ora la richiesta su muri, fondazione e trasizione rev60» e
  «approvo anche r18 se non da differenze significative»): fe3214e5, registro in b2381579.
- Colonna modello, letture di Straus7 e importazione MIDAS vanno nella 1.15.0 («colonna modello e straus andranno nella 1.15.0»).

## Librerie: release AK

Set ufficiale da Model `8c0499ae` e Checker `66b33d08`, entrambi su origin/master, con i master locali della revisione Steel del 27/10
uniti prima («prima di aggiornare anthea aggiorna model e checker con gli sviluppi avvenuti in master»); Geometry `6734eee1` e
Utilities `e57b440b` invariati. Installato in `lib/Checker` da 3d5e0b00, DLL identiche al candidato h provato nella full `supporto/artefatti/1144/misura-h`.
Manifest delle librerie `D1D0AEAB…7685`. Le versioni delle DLL sono in `lib/Checker/README.md` («Release AK»).

## Versione

`<Version>` 1.14.4 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` (8a8ceacf): nome del setup, versione file `1.14.4.0`, prodotto
`1.14.4+<commit>`. Istruzioni dell'installer con PDF; originali 1.14.3 in `supporto/SUPERATI/installer-1144-20261028`.

## Prove

Full di rilascio sul commit 8a8ceacf (`supporto/artefatti/rilascio-1.14.4/full`, profilo full con `-Installer`): **PASS 158, KNOWN 0,
NEW-FAIL 0** al primo giro, senza ripetizioni. Non eseguite come nelle full precedenti
ElasticPile.Checks e BridgeValidationCurrent (passano nei test di libreria) ed ElasticPile.Performance (misura dei tempi). Setup
costruito poi dallo stesso commit con `Crea-Installer.ps1` (`supporto/artefatti/rilascio-1.14.4/installer`), albero pulito.

## Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Commit del full = commit del setup, albero pulito | stesso SHA | 8a8ceacf per entrambi, albero pulito (`source_clean: true`) |
| Full `rilascio-1.14.4/full` | PASS, NEW-FAIL 0 | PASS 158, KNOWN 0, NEW-FAIL 0, nessuna ripetizione |
| Suite installer | PASS | `installer/safety`, `installer/guides`, `installer/examples` PASS |
| `7z t` del setup | nessun errore | nessun errore, 388 file |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.14.4+<commit>` | `1.14.4+8a8ceacf6a0bcbb3721cb157da7eefef20855c73` |
| DLL GPC uguali a `lib/Checker/manifest.json` | 13, set AK | 13, set AK, manifest `D1D0AEAB…7685` |
| Guide | pratica e teorica Rev60 | Rev60 |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno | nessuno (`forbidden_files` vuoto) |
| Setup | nome, dimensione, SHA-256 | `ANTHEA-1.14.4-Setup-x64.exe`, 69 490 176 byte, `F2BFF9A1B64DA1E37E334ABF82233C6C4896C859A3A676A56383F8902FBC570B` |

## Esito

- **File distribuiti:** 383; file applicativi 327, come nella 1.14.3. Esempi: 52 documenti per 15 moduli.
- **Audit:** `Audit-Installer.py` PASS (`audit-payload.json` accanto al setup in `supporto/artefatti/rilascio-1.14.4/installer`).
- **Pubblicazione:** con l'ok dell'utente («ok tag v1.14.4 e push di main e refactoring/f5 se la full di rilascio è pulita»), tag
  `v1.14.4` sul commit del setup e push di `main` e `refactoring/f5`.
