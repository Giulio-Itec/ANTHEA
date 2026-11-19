# ANTHEA 1.19.4: verbale di costruzione e collaudo

Verbale tecnico del 18 novembre 2026, redatto dal coordinatore. Le note per gli utenti sono in [note-rilascio-1.19.4.md](note-rilascio-1.19.4.md) e fanno anche da annotazione del tag `v1.19.4`.

## Contenuto e commit

Il ramo `claude/1194-candidata` parte da main `38d8fbfa`, cioè la 1.19.3 pubblicata. Contiene anche il lavoro di Giulio pubblicato su main fino a `630028b8` (18/11 alle 14:38), unito con un merge e non con un rebase.

Dopo la verifica Giulio ha spinto main a `d470afea`, con due commit della Sezione c.a. rapida delle 16:21 e delle 18:03, che non toccano le librerie di calcolo. Quei due commit non sono nella 1.19.4: entrano nella 1.19.5. L'utente ha scelto la strada A («Sì, pubblica stasera», 18/11 verso le 21:50):
- la release resta la candidata verificata;
- main su GitHub diventa un merge di questo verbale con `d470afea`;
- così GitHub avanza in avanti veloce e Giulio riparte da lì.

| Commit | Contenuto |
|---|---|
| `278b66f3`, `602a5f97`, `0e9c8fdd`, `c0ebc35f`, `919e2efa`, `a3ac4212`, `f92a12b3`, `f94d4256`, `f813d1f2`, `51833618`, `cecebe4d`, `20f4e8d5` | Paratie: storia di progetto confermata per le verifiche SLU (A2, PARATIE-22) e fase nuova con quote e falda della precedente (A3, PARATIE-23). Ci sono anche le correzioni della revisione (M1, M2, B1–B6) e gli esempi 04–06 con la storia confermata, approvati dall'utente |
| `850e258a`, `3bc32dcd`, `04cd44cf` | Numeri dei binding con il separatore decimale di «Aspetto»; report delle paratie con i decimali per unità, senza esponenti né «-0» |
| `9d53d3d6` | Paratie: avviso della prima fase non geostatica nel foglio, negli inviluppi e nella relazione (A4, PARATIE-24) |
| `1978cd54`, `5ff77bbb`, `33f8c6f3` | Palo orizzontale: Hu di Broms e rapporto accanto allo Stratificato (PO-BROMS-01) |
| `38242915` | Numeri salvati con il separatore scelto, «Dati assegnati» con tutte le cifre, mm² e mm⁴ senza decimali (voce 12h) |
| `897087c6`, `c161e8d7`, `5d89903e`, `7b7ed1b4` | Decisioni del 17/11, testo delle guide Rev75, preparazione della 1.19.4 |
| `5a76fe23` | `lib/Checker` dal set GPC ufficiale **AZ** (Model `a3406225`, Checker `5723a084` su origin/master; GPCModel 5.53.0.0, Converter 2.27.0.0) |
| `241d8dbe` | Modelli: vincoli e molle del freedom case del caso scelto nella copia e nel visore (MOD-FC-AZ-20261118) |
| `a3042f1f` | Guide Rev75 e note con il set AZ. È il codice della build di prova F13 |
| `a849a12e`, `f2a14669`, `39a3a88a` | Report delle paratie: le opzioni dei «Dati assegnati» scritte «sì» o «no»; «mm2» e «mm2/m» delle librerie con i decimali di mm² e mm²/m |
| `716d1a9c`, `f23cee0e`, `2c5ce854`, `3dd69aad` | Prove: Presentation.Tests raccoglie tutti i fallimenti; 53 e 17 tracce congelate con la virgola dei numeri salvati; fixture F3 `stratificato-sabbia-broms` |
| `15d80b4e`, `2a44af2d`, `c6c00ba1`, `81e880e7` | Registro delle paratie, transizioni F3 della Wiki Rev75, figure delle paratie ricatturate, edizioni Word e PDF Rev75 |
| `2c4b4a1e` | **Merge di main `630028b8`**: 12 commit di Giulio, cioè la Sezione c.a. rapida, la stratigrafia con l'addensamento e «Copia in…/Copia da…» e la relazione ITEC dei pali con le tabelle di Viggiani. Quattro file in conflitto, risolti tenendo le due parti |
| `264ce17d` | Note di rilascio con le novità unite |
| `4eec4a8d` | `StratigraphyDocument.Densities` come `IReadOnlyList<string>` (regola P3), comportamento invariato |
| `cd37c8f8` | Riferimenti congelati e attesi cambiati dal lavoro di Giulio, registrati con l'OK dell'utente (voce `MERGE-GIULIO-RIFERIMENTI-20261118`). Il contrasto della Sezione c.a. rapida nelle modalità scure è dichiarato in `build/known-failures.json` |
| `f79dd72c` | Il report dei pali si salva solo in Word, per decisione dell'utente («Sì, solo Word»), che supera la T8 del 17/10 per i pali (voce `REPORT-PALI-SOLO-WORD-20261118`) |
| `a11dfbde` | **Commit di prodotto**: SHA del PNG della capacità delle due fixture di regressione del palo verticale, conseguenza del peso proprio sfavorevole predefinito 1,35 |

## Prove

- **Full sul merge** `20261118-151110-1194-p2-full-264ce17d`, sotto `pesante.lock`. Risultato: 34 build ok, PASS 153, KNOWN 0, NEW-FAIL 14.
  - Tutti i FAIL vengono dalla commit `8796bae8` di Giulio, che cambia comportamenti coperti da riferimenti congelati senza registrarli. Le eccezioni sono due punti in cui il suo lavoro incontra il nostro: la fixture `stratificato-sabbia-broms` e la prova di Broms accanto allo Stratificato.
  - L'elenco, con esempi e conteggi, è in `INT/rapporti/anthea-1194-merge-differenze.md`. L'utente lo ha approvato («Sì, registratele voi», «Sì ai consigli»).
- **Prove native Straus7** `20261118-164807-1194-nativi-264ce17d`: `Straus7Native.Checks` e fixture versionate, PASS 41, due modelli. Dopo `264ce17d` non sono state rifatte: le commit successive cambiano solo il tipo di `Densities`, le prove, i riferimenti e i documenti, e nessuna tocca Straus7 o i Modelli.
- **Sei differenze nascoste.** Le suite ferme al primo controllo nascondevano altre sei differenze, tutte del codice di `8796bae8`:
  1. `Densities` statico mutabile (regola P3);
  2. la relazione dello Stratificato senza «validazione sperimentale»;
  3. l'eccezione del report del palo verticale con gli input nella prova dell'esito;
  4. il report dei pali solo in Word;
  5. la sostituzione di 1.35 con 1.30 nella prova della vista della stratigrafia;
  6. lo SHA del PNG della capacità.

  Sono corrette o registrate nelle commit `4eec4a8d`–`a11dfbde`. La (4) è decisione dell'utente.
- **Corse di verifica dopo la registrazione:**
  - verifica mirata `20261118-172446-1194-p2-rv-264ce17d`, figure F3 e 19 suite: Presentation.Tests (5547 controlli), Reports.Tests, Application.Tests, Stratigraphy.EditChecks, ui/smoke, ui/smoke-horizontal, ui/f3-report-chapters, ui/f3-figures, ui/f3-equivalence (120 fixture), ui/check-itec-pile-report, ui/shell-capture e ui/shell-capture-confronto PASS;
  - `20261118-175952-1194-p2-rv2-f79dd72c`: Architecture.Tests per intero (634 controlli, guardia F4 compresa), verifiche/horizontal, verifiche/software, verifiche/regressione e ui/check-stratigrafia-view PASS;
  - `20261118-212554-1194-p2-rv4-a11dfbde`: ui/check-palo-verticale-view e ui/check-palo-orizzontale-view PASS;
  - `20261118-214124-1194-p2-rv5-a11dfbde`: ui/check-contrast KNOWN, con i 12 difetti dichiarati (Scuro 6, Molto scuro 6).
- Fra `264ce17d` e `a11dfbde` l'unico file sotto `src/` che cambia è `StratigraphyDocument.cs` (tipo di `Densities`).

## Setup e audit

Dal commit di prodotto `a11dfbde`, con l'albero pulito, sotto `INT/pesante.lock`, TEMP e TMP in `%LOCALAPPDATA%\Temp`:

```powershell
& .\supporto\installer\Crea-Installer.ps1 -OutputDirectory .\supporto\artefatti\rilascio-1.19.4\installer -MakeNsis 'C:\Program Files (x86)\NSIS\makensis.exe'
& 'C:\Program Files\7-Zip\7z.exe' t  .\supporto\artefatti\rilascio-1.19.4\installer\ANTHEA-1.19.4-Setup-x64.exe
& 'C:\Program Files\7-Zip\7z.exe' x  .\supporto\artefatti\rilascio-1.19.4\installer\ANTHEA-1.19.4-Setup-x64.exe '-o.\supporto\artefatti\rilascio-1.19.4\installer\estratto' -y
py -3 -X utf8 -I .\tools\qa\Audit-Installer.py --setup .\supporto\artefatti\rilascio-1.19.4\installer\ANTHEA-1.19.4-Setup-x64.exe --extract .\supporto\artefatti\rilascio-1.19.4\installer\estratto --version 1.19.4 --guide-revision 75 --library-set AZ --out .\supporto\artefatti\rilascio-1.19.4\installer\audit-payload.json
```

| Controllo | Risultato |
|---|---|
| Setup | `supporto/artefatti/rilascio-1.19.4/installer/ANTHEA-1.19.4-Setup-x64.exe`, nel clone `CX/ANTHEA-1194` |
| Dimensione | **75.390.131 byte** (1.19.3: 74.867.377) |
| SHA-256 | **`04872218A8EC399B5EA0B383A3C03CF74ABC0C68761545179D04370FB87BBC1F`** |
| ProductVersion di ANTHEA.exe estratto | `1.19.4+a11dfbdee068c212e0ca81fc99b1d94fdd489cfd` |
| Integrità | `7z t`: «Everything is Ok»; estrazione con `7z x` |
| Audit-Installer | PASS: 435 file del payload uguali allo staging, 332 dell'applicazione, set AZ con 14 DLL, guide Rev75, 98 documenti e 33 moduli di esempio, nessun file vietato |
| Collaudi | `build/ci.ps1 -Stage fast -Installer -NoBuild -Only '^installer/(safety\|guides\|examples)$'`: PASS 3 alla prima corsa (`20261118-221238-setup-1194-a11dfbde-a`) |

## Approvazione dell'utente

Questa volta la prova dell'utente copre il codice fino a `a3042f1f`. Per il resto vale la sua approvazione con il confronto del coordinatore.

- **Build di prova F13** (`a3042f1f`, set AZ). Il 18/11 verso le 15:15 l'utente ha risposto «Tutto OK» ai punti 1, 3, 4 e 5 del LEGGIMI: modelli Straus7 con più freedom case, paratie, separatore decimale e Wiki. Il punto 2, Broms accanto allo Stratificato nel palo orizzontale, non l'ha provato. Lo coprono la fixture F3 `stratificato-sabbia-broms` e la prova di Broms di Presentation.Tests.
- **Dopo il merge.** Il 18/11 verso le 21:25 l'utente ha scritto che non poteva provare una F13 bis. Ha approvato le modifiche di Giulio «se non cambiano rispetto alla versione precedente se non nelle parti di giulio».
- **Confronto del coordinatore fra `a3042f1f` e `a11dfbde`.** Fuori dai file toccati da Giulio cambiano solo questi file di prodotto:
  - `EmbeddedWallReport.cs`, con i «sì»/«no» delle paratie;
  - `UnitNumberFormat.cs`, con mm²;
  - quattro figure delle paratie della Wiki e `sources.props`;
  - le edizioni Word e PDF Rev75 delle guide, con i loro README e l'indice dell'installer.

  L'utente li aveva approvati la mattina del 18/11. Gli altri 67 file di prodotto cambiati sono quelli dei commit di Giulio. Fra questi, nostre sono solo la riga di Broms nella sintesi del palo orizzontale, tenuta nel merge, e il tipo di `Densities`.

## Pubblicazione

A cura del coordinatore, con `INT/rapporti/pubblica-1194-A.ps1`:
- tag annotato `v1.19.4` sul commit di prodotto `a11dfbde`;
- main su un merge di questo verbale (primo genitore) con `d470afea`, in avanti veloce da `d470afea`. Rispetto al verbale il merge cambia solo file dei due commit nuovi di Giulio;
- setup sulla condivisione, con la 1.19.3 spostata in `_SUPERATI`.
