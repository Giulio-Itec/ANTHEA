# ANTHEA 1.19.3: verbale di costruzione e collaudo

Verbale tecnico del 17 novembre 2026, redatto dal coordinatore. Le note per gli utenti sono in [note-rilascio-1.19.3.md](note-rilascio-1.19.3.md) e fanno anche da annotazione del tag `v1.19.3`.

## Contenuto e commit

Il ramo `claude/1193-candidata` parte da main `e61e0915`, cioè la 1.19.2 pubblicata. Su GitHub origin/main è stato ricontrollato il 17/11 alle 16:20 ed era ancora `e61e0915`, quindi non è servito nessun merge.

| Commit | Contenuto |
|---|---|
| `59e10f5e` | `lib/Checker` dal set GPC ufficiale **AY** (`release-set.json` DA364F51…9A65; Model `bea29afe`, Checker `69ba97c8` su origin/master). GPC.Converter 2.25.0.0 importa i modelli Straus7 con più freedom case e dà un codice a ogni caso escluso; le altre DLL sono ricompilate e salgono solo nell'ultimo componente della versione |
| `104ba094`, `bb08ffda`, `bddf0fa3`, `a2dfdfbf` | Modelli: importazione Straus7 con più freedom case. Ogni caso porta il suo freedom case («· FC n» nel pannello e nei messaggi); le combinazioni hanno nomi unici; i motivi dei casi esclusi sono in italiano. Ci sono anche le correzioni della revisione indipendente (P2, P3, N1–N4) |
| `e2fffce7` | Registro, piano e documento dei Modelli dopo la revisione dei freedom case |
| `a046d9e4` | Guide Rev74: paragrafo sui modelli con più freedom case nella pratica («Modelli e importazione da MIDAS») e nella teorica («Architettura del calcolo e convenzioni»), con la Wiki rigenerata |
| `7e09710c` | Verbale della 1.19.2 senza il nome del progetto dell'utente |
| `ffbe6105` | Motivi dei casi esclusi letti dal codice del convertitore (`Straus7LinearStaticCase.ReasonCode`) e non più dal testo inglese; tolte le sei espressioni regolari |
| `95531fa6` | Versione 1.19.3, note di rilascio, README dell'installer e SUPERATI |
| `d5f8d06d` | Edizioni Word e PDF Rev74 delle due guide e PDF di servizio. La pratica passa da 142 a 143 pagine e la teorica resta di 214 |
| `76f76fb7` | **Commit di prodotto**: transizioni F3 delle due pagine Wiki della Rev74, approvate dall'utente |

## Prove

- **Full della candidata** `20261117-143817-1193-full-95531fa6`, sotto `pesante.lock` e `straus7.lock`. Risultato: 34 build ok, PASS 164, KNOWN 0, NEW-FAIL 1. L'unico FAIL è `ui/f3-equivalence`, atteso. `Architecture.Tests` passa la guardia F4 e `ModelWorkspace.Checks` ha 813 controlli.
- **Prove native Straus7** `20261117-161440-1193-nativi`: `Straus7Native.Checks` PASS. Sul modello Straus7 completo dell'utente, in una copia di lavoro in sola lettura e fuori dal repository:
  - 15 casi su 15, nessuno escluso;
  - 31 combinazioni con nomi unici;
  - reazioni in tutti i nodi;
  - 5 casi in equilibrio e 10 «non valutato», come atteso dal set AY.
- **F3.** Il FAIL della full aveva 248 differenze non ammesse, tutte nei file .md e .txt delle due pagine Wiki della Rev74. La ricerca non cambia.
  - L'utente le ha approvate il 17/11 alle 16:33 («Sì, approvo»).
  - Sono registrate in `76f76fb7` con la voce `GUIDE-REV74-20261117`: 127 transizioni aggiunte e 121 superate tolte, registro da 3496 a 3502 voci.
  - `ui/f3-equivalence` su `76f76fb7` (`20261117-165159-f3-finale-1193-76f76fb7`): PASS, 0 differenze non ammesse.
- Fra `95531fa6` e `76f76fb7` cambiano solo documenti, PDF e dati di prova, nessun file sotto `src/`.

## Setup e audit

Dal commit di prodotto `76f76fb7`, con l'albero pulito, sotto `INT/pesante.lock`, TEMP e TMP in `%LOCALAPPDATA%\Temp`:

```powershell
& .\supporto\installer\Crea-Installer.ps1 -OutputDirectory .\supporto\artefatti\rilascio-1.19.3\installer -MakeNsis 'C:\Program Files (x86)\NSIS\makensis.exe'
& 'C:\Program Files\7-Zip\7z.exe' t  .\supporto\artefatti\rilascio-1.19.3\installer\ANTHEA-1.19.3-Setup-x64.exe
& 'C:\Program Files\7-Zip\7z.exe' x  .\supporto\artefatti\rilascio-1.19.3\installer\ANTHEA-1.19.3-Setup-x64.exe '-o.\supporto\artefatti\rilascio-1.19.3\installer\estratto' -y
py -3 -X utf8 -I .\tools\qa\Audit-Installer.py --setup .\supporto\artefatti\rilascio-1.19.3\installer\ANTHEA-1.19.3-Setup-x64.exe --extract .\supporto\artefatti\rilascio-1.19.3\installer\estratto --version 1.19.3 --guide-revision 74 --library-set AY --out .\supporto\artefatti\rilascio-1.19.3\installer\audit-payload.json
```

| Controllo | Risultato |
|---|---|
| Setup | `supporto/artefatti/rilascio-1.19.3/installer/ANTHEA-1.19.3-Setup-x64.exe`, nel clone `CX/ANTHEA-1193` |
| Dimensione | **74.867.377 byte** (1.19.2: 74.831.765) |
| SHA-256 | **`7D9CF01A6464F9BAD2ADA97876778BE6837BBDC02D33D06654FD429FB38BE991`** |
| ProductVersion di ANTHEA.exe estratto | `1.19.3+76f76fb7f4b65c5b936fc36f23f7ed44a8c5ecd3` |
| Integrità | `7z t`: «Everything is Ok»; estrazione con `7z x` |
| Audit-Installer | PASS: 432 file del payload uguali allo staging, 332 dell'applicazione, set AY con 14 DLL, guide Rev74, 95 documenti e 32 moduli di esempio, nessun file vietato |
| Collaudi | `build/ci.ps1 -Stage fast -Installer -NoBuild -Only '^installer/(safety\|guides\|examples)$'`: PASS 3 |

La prima corsa dei collaudi si è fermata su `installer/examples`, con «Flusso illeggibile» mentre scriveva `catalogo.json` nella cartella di prova: il file era ancora aperto, probabilmente dall'antivirus, come per il setup 1.19.2. Un minuto dopo la seconda corsa sullo stesso setup è passata (PASS 3, `20261117-170002-setup-1193-76f76fb7-b`).

## Prova dell'utente

L'utente ha provato la build `F12-anthea-candidata-1193`, che ha lo stesso codice dell'applicazione del setup (`95531fa6`: dopo cambiano solo documenti, PDF e dati di prova) e le librerie del set AY ufficiale. Il 17/11 alle 21:45 ha risposto «prova OK» ai 9 punti del LEGGIMI, sul suo modello Straus7 completo con due freedom case:
- controllo dei file e dei casi;
- importazione «Tutto», con 15 casi e 31 combinazioni dai nomi unici;
- casi nella copia senza «· FC n»;
- reazioni vincolari;
- equilibrio dei casi valutati;
- salvataggio e riapertura;
- Wiki.

## Pubblicazione

A cura del coordinatore, con `INT/rapporti/pubblica-1193.ps1`:
- tag annotato `v1.19.3` sul commit di prodotto `76f76fb7`;
- main sul commit di questo verbale, in avanti veloce da `e61e0915`;
- setup sulla condivisione, con la 1.19.2 spostata in `_SUPERATI`.
