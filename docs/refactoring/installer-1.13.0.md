# Preparazione di ANTHEA 1.13.0

Richiesta dell'utente del 22 ottobre 2026: «domani facciamo la versione 1.13.0 con tutte queste modifiche» e «domani merge e
generale». Il rilascio porta tutto il lavoro entrato in `refactoring/f5` dopo la 1.12.1 (c7fe03dd), mai pubblicata, con la
release AF delle librerie GPC. Il contenuto per l'utente è nelle [note di rilascio](note-rilascio-1.13.0.md).

Questo verbale è preparato la notte del 22–23/10 dall'agente `rilascio-1.13.0`, prima del collaudo completo. Le parti
**«DA COMPILARE»** si riempiono dopo il full, la costruzione e l'audit del setup.

## Contenuto, ricavato da `git log c7fe03dd..3733b1a6`

147 commit, 27 di primo livello. Visibili all'utente:

| Lavoro | Commit | Note |
| --- | --- | --- |
| Paratie A1, pretensione dei tiranti fra le fasi, guide Rev57 | 573d9c73 (401f9791, 896518b5, a40aa2d7) | [pretensione-fra-le-fasi.md](../paratie/pretensione-fra-le-fasi.md) |
| S6, le immagini si copiano invece di essere rinominate (antivirus) | 78ccba8e (7a03ff7d) | deroghe ambientali della 1.11.0 e 1.12.0 chiuse |
| Pali: stratigrafia estesa con il profilo a destra; Tab seleziona il contenuto; γ del palo = 0 | dccfc9d0 (79fdc958, 1661d60d, 0c387b93) | decisioni.md, 22 ottobre pomeriggio |
| Set candidato del 22/10 e differenze PARATIE-19 (S2) e PARATIE-20 (φ = 0), approvate dall'utente | 957acdd6 (49efdff0, 8c40303c) | registro-differenze.json |
| Release AF ufficiale delle librerie | be4e18e2 | [lib/Checker/README.md](../../lib/Checker/README.md) |
| Versione 1.13.0, guide Rev58, transizioni editoriali della Wiki, questo verbale | ramo `refactoring/rilascio-1.13.0` | vedi sotto |

Senza effetti visibili nella Release, che apre ancora le viste storiche e `MainWindow`: F5.10 S2–S9 e vista XAML della
sezione c.a., F5.11 e F5.12 (viste XAML), F5.13 shell parti a, b, c (fino a c2) e d, P3, suite e prove. Le derivazioni spostate
in `ANTHEA.Application` sono chiamate anche dalle viste storiche, con le tracce congelate e il banco F3 a differenza zero.

L'elenco dato al coordinatore il 22/10 era corretto, con due precisazioni. P4, P5 e S1 stanno solo nelle librerie: da ANTHEA
non si raggiungono, e per P5 manca ancora il campo del bordo a flusso nullo. La Rev57 non è mai stata distribuita: la 1.12.1
aveva la Rev56 e la 1.13.0 distribuisce la Rev58.

## Librerie: release AF

Set ufficiale da Model `45c3976c` e Checker `f6916d8e`, su origin/master; Geometry `6734eee1` e Utilities `e57b440b`
invariati. Installato in `lib/Checker` da be4e18e2; set completo nel checkout principale in
`supporto/artefatti/librerie-20261022-sera/gpc-release-20261022`. SHA-256 di `lib/Checker/manifest.json`:
`F4FC33A2276184E6E6CDB6384A0166533C39F503C95CCB2804779293A4B7D03F`.

| DLL | 1.12.1 (AE) | 1.13.0 (AF) |
| --- | --- | --- |
| GPCModel | 5.16.0.1 | 5.20.0.0 |
| GPCModelData | 0.0.2.27 | 0.3.0.0 |
| GPC.Converter | 2.2.0.16 | 2.6.0.0 |
| GPC.Model.Persistence | 3.0.0.20 | 3.3.0.0 |
| GPCChecker.Concrete | 0.13.0.0 | 0.18.0.0 |
| GPCChecker.Steel | 2.4.0.17 | 2.31.0.0 |
| GPCChecker.Geotechnics | 0.14.0.0 | 0.19.0.0 |
| GPCChecker.Masonry | 0.1.0.19 | 0.6.0.0 |
| GPC.Design | 0.30.0.0 | 0.35.0.0 |
| GPCChecker.CompositeBridge | 1.7.0.16 | 1.12.0.0 |
| GPCUtilities, GPCGeometry, DelaunayMesh | 3.0.0.0, 2.3.0.0, 2.0.0.14 | invariate |

Il lavoro Codex sul ramo `codex/steel-autonomy-class4` (Steel 2.32, ModelChecker 2.16/2.17) non è nel set.
GPCModel 5.20.0.0 e Geotechnics 0.19.0.0 hanno un cambio di sorgente dopo il commit di versione, mai distribuito con un
contenuto diverso: il coordinatore ha scelto di non fare un altro bump (README di lib/Checker).

## Versione

Unica fonte: `<Version>` in `X.Desktop/X.Desktop.csproj`, ora `1.13.0`. Ne derivano il nome del setup, la versione file
`1.13.0.0` e il prodotto `1.13.0+<commit>`. Le istruzioni dell'installer (`supporto/installer/README.md` e PDF) riportano la
versione e le righe di comando; gli originali 1.12.1 sono in `supporto/SUPERATI/installer-1130-20261023/`.
Secondo componente: funzioni nuove e correzioni di calcolo.

## Guide Rev58

La Rev57 non descriveva Tab, stratigrafia estesa e γ = 0, e la guida teorica conteneva una frase superata da S2 («d dal
baricentro delle barre del semicerchio teso; si adotta il minore d dei due versi»). La Rev58 aggiunge:

- guida pratica: Tab e Maiusc+Tab; «Estendi» della stratigrafia dei pali; peso specifico del palo nullo;
- guida teorica: γ = 0 nel palo verticale; taglio dei pali circolari con l'anello continuo (palo elastico e paratie);
  sostegni che si scaricano senza modificare ep (P1); ricerca lineare di Armijo e tolleranza con i termini di flessione
  (P2, P3); conci con φ = 0 nella stabilità globale di muri e paratie;
- intestazioni «revisione documentale 58» (la Rev57 aveva lasciato 56).

Non sono documentati perché non raggiungibili da ANTHEA: P4, P5 (il campo del bordo a flusso nullo manca) e S1.

Pipeline: Build, Render, Finalize, Render -VerifyFinal con il modello ITEC del checkout principale. Pratica 100 pagine e
1114 frammenti; teorica 166 pagine, 2122 frammenti e 391 formule; 26 parti ITEC identiche. PDF accanto ai Markdown, indice,
README e istruzioni dell'installer con i loro PDF; pagine cambiate controllate a vista. Originali Rev57 (18 file) in
`supporto/SUPERATI/rilascio-1130-rev58-20261023/` con SHA-256. Wiki rigenerata con `build-wiki-index.py`.

Le differenze editoriali che la Rev58 produce nella cattura F3 della Wiki sono registrate in
`tests/fixtures/f3/transitions-f5-wiki.json` (voce F5-REL-1130-01): **vedi sotto, «Prove della notte»**.

## Prove della notte (ramo `refactoring/rilascio-1.13.0`)

Cartelle in `supporto/artefatti/ci` del worktree `Temp\aw-rilascio-1.13.0`, da copiare nel checkout principale prima di
cancellare il worktree.

- `20261023-004336-rev58-f3`, commit afb80a16 (Rev58 senza transizioni): `ui/f3-equivalence` cattura 119 fixture,
  77 articoli e 30 ricerche e trova 21 differenze non ammesse, tutte editoriali nella Wiki. Registrate in 51b8c328
  (F5-REL-1130-01); il confronto della sola Wiki con il registro aggiornato non ha differenze non ammesse.
- `20261023-005328-rilascio-1.13.0-standard-f3`, commit c3f77daf: profilo standard più `ui/f3-equivalence`
  (`-Profile full -Only '^(?!ui/)|^ui/f3-equivalence$'`). **PASS 98, KNOWN 0, NEW-FAIL 0.** `ui/f3-equivalence` PASS,
  Architecture, Application, Presentation (anche `paralleli` e `sezione-ca`), Wiki, Examples e regressione PASS; `wiki/indice` e
  `wiki/manuale` PASS. Il runner esce con 1 solo perché durante la corsa ho modificato due documenti
  (`installer-1.13.0.md`, `note-rilascio-1.13.0.md`, poi in 16fdc411): nessun file di codice o di prova.
- `20261023-012842-rilascio-1.13.0-installer`, commit 16fdc411: `-Stage fast -Only '^installer/' -Installer`, PASS 3.
  Sicurezza NSIS 26 controlli, guide 30 controlli con Rev58 scelta per entrambe le guide, esempi PASS.

Il collaudo full con lo stadio ui completo resta per il commit finale, dopo le unioni.

## Passi per il rilascio (23/10)

Tutti i comandi vanno lanciati da Windows PowerShell 5.1, dalla radice del checkout che contiene il commit finale. Da
PowerShell 7 il processo figlio eredita i moduli della 7 e le suite NSIS non trovano `Get-FileHash` (1.11.0): in quel caso
assegnare a `PSModulePath` i percorsi della 5.1.

### 1. Unione

Unire in `refactoring/f5` i rami della notte: `f5-10-tracce-vista`, `f5-13-c3` e `refactoring/rilascio-1.13.0`. Se un altro
ramo ha cambiato le guide dopo la Rev58, rigenerare indice della Wiki, edizioni Word e PDF e transizioni editoriali prima del
full (stessa pipeline, sezione «Guide Rev58»). Il commit da collaudare deve essere pulito: `git status` vuoto.

### 2. Collaudo completo

Profilo full con le suite dell'installer; contrasto escluso perché lo schermo è a 144 DPI (D6):

```
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Profile full -Installer -Only '^(?!ui/check-contrast$)' -Tag rilascio-1.13.0-full"
```

Dura circa un'ora e occupa lo schermo. Con la macchina carica possono cadere `ui/check-field-form-parity`,
`ui/check-palo-elastico-historic` e `ui/check-palo-orizzontale-view`: ripeterle da sole, senza ricompilare:

```
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage ui -NoBuild -Only '^(ui/check-field-form-parity|ui/check-palo-elastico-historic|ui/check-palo-orizzontale-view)$' -Tag rilascio-1.13.0-ripeti"
```

Un fallimento nuovo non si aggiunge a `build/known-failures.json` senza averlo spiegato all'utente.

### 3. Setup

Dallo stesso commit pulito del full:

```
powershell -NoProfile -ExecutionPolicy Bypass -File supporto\installer\Crea-Installer.ps1 -OutputDirectory supporto\artefatti\rilascio-1.13.0\installer
```

Lo script pubblica in `supporto/artefatti/installer/stage`, controlla l'assenza di codice di prova, copia le guide Rev58,
l'indice e i 52 esempi e stampa dimensione e SHA-256 di `ANTHEA-1.13.0-Setup-x64.exe`.

### 4. Audit

```
& 'C:\Program Files\7-Zip\7z.exe' t supporto\artefatti\rilascio-1.13.0\installer\ANTHEA-1.13.0-Setup-x64.exe
& 'C:\Program Files\7-Zip\7z.exe' x supporto\artefatti\rilascio-1.13.0\installer\ANTHEA-1.13.0-Setup-x64.exe -osupporto\artefatti\rilascio-1.13.0\installer\estratto -y
& 'C:\Users\g.pacini\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -I tools\qa\Audit-Installer.py --setup supporto\artefatti\rilascio-1.13.0\installer\ANTHEA-1.13.0-Setup-x64.exe --extract supporto\artefatti\rilascio-1.13.0\installer\estratto --version 1.13.0 --guide-revision 58 --library-set AF
```

`tools/qa/Audit-Installer.py` generalizza gli script della 1.12.0 e della 1.12.1. Si ferma al primo controllo non superato
e scrive `audit-payload.json` accanto al setup. Provato la notte del 23/10 sul setup 1.12.1 (estrazione e staging del
checkout principale): tutti i controlli dei file superati; l'ultimo, la versione del prodotto, cade come atteso perché il
commit della prova non è c7fe03dd.

### 5. Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Commit del full = commit del setup, albero pulito | stesso SHA | DA COMPILARE |
| Full `rilascio-1.13.0-full` | PASS, NEW-FAIL 0 dopo le ripetizioni | DA COMPILARE |
| `ui/f3-equivalence` | nessuna differenza non ammessa | DA COMPILARE |
| Suite installer (`installer/safety`, `installer/guides`, `installer/examples`) | PASS | DA COMPILARE |
| `7z t` del setup | nessun errore | DA COMPILARE |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.13.0+<commit>` | DA COMPILARE |
| File distribuiti uguali allo staging (SHA-256) | tutti; 382 nella 1.12.1 | DA COMPILARE |
| File applicativi | 326 nella 1.12.1; differenze da spiegare | DA COMPILARE |
| DLL GPC uguali a `lib/Checker/manifest.json` | 13, set AF, manifest `F4FC33A2…7D03F` | DA COMPILARE |
| Guide | pratica e teorica Rev58 e indice uguali ai PDF del repository | DA COMPILARE |
| Esempi | 52 in 15 moduli, uguali al catalogo | DA COMPILARE |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno | DA COMPILARE |
| Setup | nome, dimensione, SHA-256 | DA COMPILARE |

### 6. Pubblicazione, solo con l'ok esplicito dell'utente

- tag annotato `v1.13.0` sul commit del setup, con le note di rilascio e lo SHA-256 del setup;
- push di `refactoring/f5`;
- merge su `main`: il `main` locale (ae0e1df4) è antenato di `refactoring/f5`, quindi l'unione è un avanzamento rapido;
  `origin/main` è ancora 8b9ce6d3 (1.10.0);
- tag `v1.12.1` su c7fe03dd: la 1.12.1 non è mai stata pubblicata; l'utente decide se marcarla o saltarla.

## Esito del rilascio

DA COMPILARE: full, ripetizioni, setup (percorso, byte, SHA-256, versione del prodotto), audit, decisioni dell'utente.
