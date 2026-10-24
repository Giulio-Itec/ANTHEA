# Preparazione di ANTHEA 1.14.0

Decisioni dell'utente del 24 ottobre 2026 per la chiusura della fase F5 ([decisioni.md](decisioni.md), «24 ottobre (pomeriggio)»):
«Anche rilascio 1.14.0» (spunta delle voci, unione su `main` e push, versione 1.14.0 con setup verificato, tag e note di rilascio),
«Shell approvata», «Rev59 prima», e la 1.14.0 con le librerie AG dopo la correzione di Checker sulla strategia «Intersezione». Il
contenuto per l'utente è nelle [note di rilascio](note-rilascio-1.14.0.md).

## Contenuto

Tutto il lavoro entrato in `refactoring/f5` dopo il tag `v1.13.0` (e1d9976c):
- F5.5–F5.12: tutti i moduli nella Release con le viste MVVM (passaggi del 23/10);
- F5.13: shell nuova come finestra della Release; `MainWindow`, `SheetEditor` e l'ospite storico archiviati in
  `supporto/SUPERATI/ui-storica` (parte g); G2, G6, G8, G9, G10;
- F5.14: progetto desktop in `src/ANTHEA.Desktop`, prove WPF in `tests/ANTHEA.Desktop.UiTests`;
- guide Rev59 e figure della Wiki in `src/ANTHEA.Desktop/Assets/Wiki` (F5-REL-1140-01, F5-REL-1140-02);
- release AG delle librerie GPC e registrazione delle sue differenze (voci AG-*);
- F5.15: scheda delle 13 voci e decisioni del 24/10 (da attuare dopo il 28/10, fuori da questo rilascio).

## Librerie: release AG

Set ufficiale da Model `2d59bbc6` e Checker `22dabe69`, entrambi su origin/master; Geometry `6734eee1` e Utilities `e57b440b`
invariati. Installato in `lib/Checker` da 4dd8018a; set nel checkout principale in
`supporto/artefatti/librerie-20261024-ag/gpc-release-20261024-ag`.

| DLL | 1.13.0 (AF) | 1.14.0 (AG) |
| --- | --- | --- |
| GPCModel | 5.20.0.0 | 5.21.0.0 |
| GPCModelData | 0.3.0.0 | 0.3.0.1 |
| GPC.Converter | 2.6.0.0 | 2.6.0.1 |
| GPC.Model.Persistence | 3.3.0.0 | 3.3.0.1 |
| GPCChecker.Concrete | 0.18.0.0 | 0.20.0.1 |
| GPCChecker.Steel | 2.31.0.0 | 2.84.0.0 |
| GPCChecker.Geotechnics | 0.19.0.0 | 0.19.0.1 |
| GPCChecker.Masonry | 0.6.0.0 | 0.6.0.1 |
| GPC.Design | 0.35.0.0 | 0.36.0.0 (netstandard2.0) |
| GPCChecker.CompositeBridge | 1.12.0.0 | 1.13.0.1 |
| GPCUtilities, GPCGeometry, DelaunayMesh | 3.0.0.0, 2.3.0.0, 2.0.0.14 | invariate |

Percorso del set, il 24/10:
- i master pubblicati alle 12:46 (Checker f6d87d28) non superavano la verifica: sei prove di Concrete rosse dopo l'unione di due
  rami Codex. Cinque riconciliate (analitica corrente e storico tabellare); il confronto esatto dello StressBlock con lo storico 0.0.17
  ritirato per decisione dell'utente («2A»);
- in ANTHEA Concrete 0.20.0.0 rifiutava molti punti della strategia «Intersezione» (164 forze su 300 della griglia 12d): il controllo
  del piano restituito (62ecbc83) ora esclude i punti dell'intersezione, il cui piano è interpolato (87e114cc, Concrete 0.20.0.1);
- Checker master 22dabe69 pubblicato; Release-Set con tutte le suite di Model e Checker verdi senza fallimenti ammessi.

## Differenze registrate (registro delle differenze)

| Voce | Modulo | Effetto | Decisione |
| --- | --- | --- | --- |
| AG-CA-01, AG-CA-05 | sezione c.a. | resistenze da −0,21 % a +0,26 %, η fino a −0,24 %, curvature M–χ fino a −2,6 %, εc,comp fino a −4,4 % a momenti bassi; disegni e figure | «registra e aggiorna»; εc,comp accettata dall'utente |
| AG-CA-02, AG-CA-04 | sezione c.a. | prova 4d con tension stiffening (|εs| −3,55 %), stress block simmetrico (+3,64 %), SLE rara che ora si completa | «5A» |
| AG-CA-03 | sezione c.a. | rifiuti della strategia «Intersezione», corretti in libreria; traduzione del rifiuto nuovo | corretta |
| AG-PO-01, AG-PO-02 | palo e micropalo orizzontale, risposta elastica | My +0,026 %, \|MRd\| +0,02 % | «registra e aggiorna» |
| AG-MUR-01 | muri | resistenze N–M fino a +0,22 % | «registra e aggiorna» |
| AG-SM-01 | sezione mista | governanti −0,10 % e −1,04 %; grandezze piccole della soletta fino a −6,8 % | «4B» |
| AG-FON-01 | fondazioni | solo la versione della libreria nel risultato | «registra e aggiorna» |
| AG-SHELL-01 | persistenza | versione 4 del materiale calcestruzzo: i file non si aprono con la 1.13.0 | «6A», limite noto |

Le correzioni delle prove senza effetto sui calcoli: `ui/check-appearance` aspetta il calcolo automatico (2dd321a5).

## Versione

`<Version>` 1.14.0 in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` (277f704b): nome del setup, versione file `1.14.0.0`, prodotto
`1.14.0+<commit>`. Istruzioni dell'installer con PDF; originali 1.13.0 in `supporto/SUPERATI/installer-1140-20261024`. Secondo
componente: funzioni nuove (la finestra principale e le viste) e librerie con calcoli cambiati.

## Prove

Full di rilascio sul commit d7e6b6a7 (`supporto/artefatti/ci/20261024-233211-rilascio-1.14.0-full-ag`, 23:32–00:46, profilo full con
`-Installer`, contrasto compreso a 96 DPI): **PASS 155, KNOWN 0, NEW-FAIL 0** al primo giro, senza ripetizioni. Albero pulito prima della
corsa; manifest delle librerie `D73AB6C9…5595` (release AG).

Corse intermedie del 24/10, nel checkout principale e nelle cartelle `supporto/artefatti/ag-po`, `ag-ca`, `ag-ca2`, `ag-ui2`:
- `ag-candidato-anticipo` (snapshot del 24/10 mattina) e `ag-upstream-ui` (DLL da origin/master): le differenze da registrare;
- `ag-unione` dopo l'unione dei rami ag-po e ag-ca: PASS 73, rosse solo le suite dei rifiuti dell'«Intersezione» e la provenienza F4;
- `ag-ufficiale-mirata` sulla release AG ufficiale: tutte le suite non UI PASS; restavano 7 prove UI con riferimenti congelati, allineate
  nei rami ag-ca2 e ag-ui2.

## Lista di controllo

| Controllo | Atteso | Esito |
| --- | --- | --- |
| Commit del full = commit del setup, albero pulito | stesso SHA | d7e6b6a7 per entrambi, albero pulito (`source_clean: true`) |
| Full `rilascio-1.14.0-full-ag`, contrasto compreso (96 DPI) | PASS, NEW-FAIL 0 dopo le ripetizioni | PASS 155, KNOWN 0, NEW-FAIL 0, nessuna ripetizione |
| `ui/f3-equivalence` | nessuna differenza non ammessa | PASS (165 s) |
| Suite installer | PASS | `installer/safety`, `installer/guides`, `installer/examples` PASS |
| `7z t` del setup | nessun errore | nessun errore, 388 file |
| Versione del prodotto di `ANTHEA.exe` estratto | `1.14.0+<commit>` | `1.14.0+d7e6b6a78140401747f556a2b8d42840ffa7d335` |
| DLL GPC uguali a `lib/Checker/manifest.json` | 13, set AG | 13, set AG, manifest `D73AB6C9…5595` |
| Guide | pratica e teorica Rev59 | Rev59 |
| Gmsh, UnsafeEx, assembly di prova o legacy | nessuno | nessuno (`forbidden_files` vuoto) |
| Setup | nome, dimensione, SHA-256 | `ANTHEA-1.14.0-Setup-x64.exe`, 69 361 880 byte, `C2D257E39286E357764C0840E443A55832425D632B5591D51194FDBE30A132C4` |

## Prova di riserva con le librerie AF

Prima della correzione di Checker è stata preparata una 1.14.0 con le librerie AF (ramo locale `refactoring/rel-af`, b64ffa48): full
PASS 155, setup e audit PASS. L'utente ha scelto di aspettare le librerie AG; il ramo non è unito.

## Esito del rilascio

- **File distribuiti:** 383; file applicativi 327 (326 nella 1.13.0). In più `System.IO.Pipelines.dll`; cambiano System.Text.Json
  (10.0.13, per GPC.Design in .NET Standard 2.0) e System.Text.Encodings.Web; la licenza dei font WpfMath cambia solo nei fine riga
  (spostamento di F5.14).
- **Audit:** `Audit-Installer.py` PASS (`audit-payload.json` accanto al setup in `supporto/artefatti/rilascio-1.14.0/installer`).
- **Pubblicazione:** tag annotato `v1.14.0` sul commit del setup d7e6b6a7, con le note di rilascio e lo SHA-256 del setup; `main` portato
  in avanti a `refactoring/f5`; push di `refactoring/f5`, `main` e del tag, come deciso dall'utente («Anche rilascio 1.14.0»).
