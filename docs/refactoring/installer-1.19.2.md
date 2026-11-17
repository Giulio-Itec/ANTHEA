# ANTHEA 1.19.2: verbale di costruzione e collaudo

Verbale tecnico del 17 novembre 2026, redatto dal coordinatore. Le note per gli utenti sono in [note-rilascio-1.19.2.md](note-rilascio-1.19.2.md) e fanno anche da annotazione del tag `v1.19.2`.

## Contenuto e commit

Il ramo `claude/1192-candidata` parte da main `a323af43`, che contiene la stratigrafia riorganizzata di Giulio (d202c46f). origin/main su GitHub è stato ricontrollato il 17/11 alle 08:30 ed era ancora `a323af43`.

| Commit | Contenuto |
|---|---|
| `865868b0` (via `55c4824b`) | Modulo Modelli, rilievi R1–R10 della prova MIDAS del 16/11, con le correzioni della revisione indipendente; guide Rev73 del capitolo Modelli |
| `576f40f8` | Merge di `eb248595`: griglia degli strati in sola lettura nelle revisioni precedenti |
| `e91713d8`, `ce40872e` | Nomi con «_» interi nelle caselle (P4 della revisione del ramo Straus7, con `Name` al posto di `Label`) e la sua prova |
| `a1a8b9ee` | Guide: «ANTHEA 1.19.2» nella riga dell'edizione e nel capitolo Modelli; esempio di Annulla «Esempio» al posto di «Ponte»; Wiki rigenerata |
| `829f852b` | Versione 1.19.2, note di rilascio, README dell'installer (il 1.19.1 è in `supporto/SUPERATI/rilascio-1192-20261117`), voce del piano |
| `c6fa1702`, `7426f226` | Note senza «↶», che Arial non ha; edizioni Word e PDF Rev73 della pratica (cambiano solo le p. 97 e 104) e PDF di servizio |
| `a27bee25` | **Commit di prodotto**: transizioni F3 della pagina Wiki dei Modelli, approvate dall'utente |

Le librerie GPC sono quelle della 1.19.1 (set AW): nessuna DLL di `lib/Checker` è cambiata.

## Prove

- **Full della candidata.** La corsa `20261117-084353-candidata-1192-829f852b` è stata fermata alle 10:17, su richiesta dell'utente, per liberare il PC per il rilascio della 0.4.1. I risultati delle suite già finite sono conservati.
  - In quella corsa: 34 build ok, 108 PASS, 1 FAIL (`ui/f3-equivalence`).
  - Le 19 suite che mancavano sono girate su `7426f226` (corsa `20261117-103525-resto-candidata-1192-7426f226`): PASS 23, nessun fallimento.
  - Fra `829f852b` e `7426f226` cambiano solo documenti, PDF e note.
- **F3.** Il FAIL aveva 260 differenze non ammesse, tutte nei due file della pagina Wiki «Modelli e importazione da MIDAS»; la ricerca non cambia.
  - L'utente le ha approvate il 17/11 («Sì, approvo»). Sono registrate in `a27bee25` con la voce `GUIDE-REV73-20261117`: 138 transizioni aggiunte e 122 superate tolte.
  - `ui/f3-equivalence` su `a27bee25`: PASS (119 fixture, 94 articoli, 30 ricerche).
- **Suite mirate.** `ModelViewer.LiveImport.UiChecks` (con la prova dei nomi con «_»), `Stratigraphy.UiChecks`, `ui/shell-smoke-project-workspace` (le tabelle storiche protette, compresa la griglia degli strati) e `Architecture.Tests`: PASS.

## Setup e audit

Dal commit di prodotto `a27bee25`, con l'albero pulito, sotto `INT/pesante.lock`, TEMP e TMP in `%LOCALAPPDATA%\Temp`:

```powershell
& .\supporto\installer\Crea-Installer.ps1 -OutputDirectory .\supporto\artefatti\rilascio-1.19.2\installer -MakeNsis 'C:\Program Files (x86)\NSIS\makensis.exe'
& 'C:\Program Files\7-Zip\7z.exe' t  .\supporto\artefatti\rilascio-1.19.2\installer\ANTHEA-1.19.2-Setup-x64.exe
& 'C:\Program Files\7-Zip\7z.exe' x  .\supporto\artefatti\rilascio-1.19.2\installer\ANTHEA-1.19.2-Setup-x64.exe '-o.\supporto\artefatti\rilascio-1.19.2\installer\estratto' -y
py -3 -X utf8 -I .\tools\qa\Audit-Installer.py --setup .\supporto\artefatti\rilascio-1.19.2\installer\ANTHEA-1.19.2-Setup-x64.exe --extract .\supporto\artefatti\rilascio-1.19.2\installer\estratto --version 1.19.2 --guide-revision 73 --library-set AW --out .\supporto\artefatti\rilascio-1.19.2\installer\audit-payload.json
```

| Controllo | Risultato |
|---|---|
| Setup | `supporto/artefatti/rilascio-1.19.2/installer/ANTHEA-1.19.2-Setup-x64.exe`, nel clone `CX/ANTHEA-modelli-guide` |
| Dimensione | **74.831.765 byte** (1.19.1: 74.814.011) |
| SHA-256 | **`C081CF3543757A1E6F70B0904CFEF60BFDB408791F9E325265033D60E0F5E64F`** |
| ProductVersion di ANTHEA.exe estratto | `1.19.2+a27bee256dbca5398ae01bd0a172524b63bfe0d5` |
| Integrità | `7z t`: «Everything is Ok»; estrazione con `7z x` |
| Audit-Installer | PASS, 432 file del payload uguali allo staging |
| Codice di prova | `Assert-NoTestCode.ps1` PASS durante la costruzione |
| Collaudi | `build/ci.ps1 -Stage fast -Installer -NoBuild -Only '^installer/(safety\|guides\|examples)$'`: PASS 3 |

Il primo passaggio di Crea-Installer è fallito solo nel `Get-FileHash` finale, con «Flusso illeggibile», dopo che makensis era uscito con 0. Il file appena scritto era ancora aperto, probabilmente dall'antivirus. Due minuti dopo l'hash si leggeva normalmente, e prova, estrazione, audit e collaudi sono stati fatti sullo stesso setup.

## Prova dell'utente

L'utente ha provato la build `F11-anthea-candidata-1192`, che ha lo stesso codice dell'applicazione del setup (`829f852b`; dopo cambiano solo documenti, PDF e dati di prova). Il 17/11 ha risposto «Prova OK» agli 11 punti del LEGGIMI: Modelli sul suo progetto MIDAS, nomi con «_», File › Apri, file Straus7 con due freedom case senza risultati, stratigrafia storica e Wiki.

## Pubblicazione

A cura del coordinatore, con `INT/rapporti/pubblica-1192.ps1`:
- tag annotato `v1.19.2` sul commit di prodotto `a27bee25`;
- main sul commit di questo verbale, in avanti veloce da `a323af43`;
- setup sulla condivisione, con la 1.19.1 spostata in `_SUPERATI`.
