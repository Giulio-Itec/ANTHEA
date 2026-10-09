# CheckerMigration.Capture

Congela le uscite dei nuclei di calcolo di `X.Calculations` su griglie dense e l'impronta delle mesh
delle sezioni in c.a. Serve come riferimento prima di spostare codice o di cambiare le DLL di
`lib/Checker`. Formato dei numeri: `R` invariante. Ogni file riporta nella prima riga il commit
passato e lo SHA-256 di `ANTHEA.Calculations.dll`, che cambia con il commit di compilazione (la
versione informativa contiene lo SHA di git) e con il percorso del checkout: nei confronti si
escludono le righe `#` e le righe `header`.

## Comandi

Dalla radice del repository:

```powershell
dotnet build supporto\test\CheckerMigration.Capture\CheckerMigration.Capture.csproj -c Release -nologo
dotnet supporto\test\CheckerMigration.Capture\bin\Release\net8.0\CheckerMigration.Capture.dll <cartella> <commit> [modalità] [--manifest] [--motore legacy|libreria] [--motore-sle legacy|libreria] [--motore-durabilita legacy|libreria]
```

Con un altro insieme di DLL si compila con `-p:GpcLibDir=<cartella>\` (vedi `Directory.Build.props`).

| Modalità | Contenuto | File | Durata indicativa |
| --- | --- | --- | --- |
| `tutte` (o nessuna) | taglio, SLE, torsione, fessurazione, ancoraggi e aderenza, dettagli, M-χ, durabilità, pali, muri; dal passo F2.8-A0 la cattura estesa di dettagli, aderenza e M-χ (sotto) | `shear`, `stress`, `torsion*`, `crack*`, `anchorage`, `detailing`, `curvature`, `durability` (CSV e XML del Model), `piles-*.jsonl`, `walls-*`; `detailing-plate-*`, `detailing-texts`, `detailing-adapter`, `bond`, `curvature-production` | 1 min |
| `muri` | solo muri di sostegno | `walls-functions.jsonl`, `walls-combinations.jsonl`, `walls-documents.jsonl.gz` | 45 s |
| `pali` | solo pali e micropali | `piles-nq.jsonl`, `piles-bustamante-doix.jsonl`, `piles-chs.jsonl`, `piles-lateral.jsonl`, `piles-vertical.jsonl` | pochi secondi |
| `mesh` | impronta delle mesh DelaunayMesh delle sezioni usuali | `mesh-fingerprint.csv`, `mesh-detail.jsonl.gz` | pochi secondi |

Un terzo argomento diverso da `muri`, `pali` e `mesh` esegue tutto, come prima. La geotecnica
generale (`GeotechnicsCapture`) si compila solo con `LEGACY_GEOTECHNICS` su un checkout fino a
dadea50: i suoi riferimenti sono in `GPCChecker.Test.Geotechnics/Fixtures`.

`--manifest`, dopo la modalità, aggiunge `capture-manifest.json`: argomenti e riga di comando,
commit passato, assembly caricati dalla cartella dell'applicazione (DLL GPC comprese) con versione e
SHA-256, SHA-256 di ogni file prodotto, tempi. Senza l'opzione le uscite non cambiano.

`--motore`, dopo la modalità (refactoring F2.5-F2.6): taglio (`shear-legacy.csv`) e torsione
(`torsion-legacy.csv`, `torsion-geometry-legacy.csv`) passano da `ConcreteShearTorsionAdapter`. Con `legacy` si
catturano i nuclei di `X.Calculations` (le fixture di Checker), con `libreria` GPCChecker.Concrete attraverso lo
strato di mappatura (unità di ANTHEA, testi del legacy); senza l'opzione il motore predefinito dell'adattatore. Il
motore usato è scritto nella riga `#` dei tre file. Per la torsione la libreria riceve fck = 30 MPa e γc = 1,5, come
nei `TorsionMigrationTests`: per NTC 2018 non entrano nelle resistenze.

`--motore-sle`, dopo la modalità (refactoring F2.7b, commit A4): tensioni SLE (`stress-legacy.csv`), fessurazione
(`crack-legacy.csv`, `crack-scalar-legacy.csv`) e muri (`walls-documents.jsonl.gz`) passano da
`ConcreteServiceabilityAdapter` con il motore indicato (`legacy` o `libreria`); senza l'opzione il motore predefinito
dell'adattatore. Con `libreria` la parte scalare usa le classi della libreria (`CrackWidthCalculator` con il profilo
della norma dalla mappatura; requisiti attraverso l'adattatore). Il motore usato è scritto nelle righe `#` e
nell'intestazione dei documenti dei muri, escluse dal confronto.

`--motore-durabilita`, dopo la modalità (refactoring F2.9): la durabilità (`durability-legacy.csv`) passa dalle
facciate `Materiali.Durability`, `NtcCover`, `MinimumConcrete` e `AtecapMix` con il motore indicato di
`ConcreteDurabilityAdapter`. Con `legacy` si cattura il nucleo `DurabilityLegacy` di `X.Calculations` (le fixture di
Checker), con `libreria` GPCChecker.Concrete attraverso lo strato di mappatura (testi del legacy); senza l'opzione il
motore predefinito dell'adattatore. Il motore usato è scritto nella riga `#` del file. I due motori danno uscite
identiche (registro F2-13).

## Cattura estesa di dettagli, aderenza e M-χ (F2.8-A0)

`DetailingExtendedCapture.cs`, eseguita nella modalità `tutte` dopo tutte le altre catture, scrive solo file nuovi:
i file precedenti restano identici byte per byte (salvo gli identificativi casuali). Riferimento: progetto F2.8 rivisto,
§5 voce A0 (`supporto/artefatti/refactoring/f27-f28-progetto/F28-progetto-rivisto.md`).

| File | Contenuto |
| --- | --- |
| `detailing-plate-legacy.csv`, `detailing-plate-sections.xml` | `ConcreteDetailingCalculator` su solette piene (strisce 1000×h con barre sulle due facce, senza staffe, con barre manuali sfalsate senza interasse riconosciuto, con barre sulla sola faccia inferiore, dense e rade) e pareti (1000×200, 200×1000, 250×1200 senza staffe, dense, rade, con barre manuali sfalsate): ogni regola di soletta e parete ha entrambi gli esiti, più i rami in sospeso (R9). Variano cmin,dur (NaN, +∞, −5, 25, 45), zona critica, zona di sovrapposizione, armatura e passo secondari (0 = in sospeso), staffe, aggregato, Δcdev, NEd e conferme. Rifiuti della soletta (rettangolo cavo, circolare, a T, anche con un parametro negativo: il messaggio della striscia viene prima della validazione numerica), rifiuti numerici raggiungibili dal foglio, righe di trave, pilastro, soletta e parete con **rami = −2**. Colonne di `detailing-legacy.csv` più `secondarySteel`, `secondarySpacing`, `critical`. |
| `detailing-texts-legacy.csv` | Per ogni tipo di elemento, ogni combinazione distinta di nome, unità, riferimento e spiegazione dei controlli incontrati nella cattura (anche le due spiegazioni di «As,min» con e senza barre nella metà di sezione), con il numero di comparse e di esiti soddisfatti, non soddisfatti e in sospeso. |
| `detailing-adapter-legacy.jsonl` | `ConcreteDetailingAnalysis.Calculate` e `.Anchorage` su fogli completi (`SezioneCA.DefaultData`, `SectionWorkspace.Prepare`): i 4 elementi, esposizione assente o presente, norma diversa da NTC, righe SLU e SLV di compressione e trazione, Ø e σ vuoti, σ > fyd, lunghezza vuota, fck 12, 25, 60, 70 e 90, sovrapposizione al 50 % con interferro > 4Ø, `cls_diagramma` non valido, `rami_y` = −2, elemento non scelto, pilastri circolari a due anelli con raggi vicini all'arrotondamento a 5 cifre di `TensionBarSpacing`. Una riga per foglio (`name` = elemento/sezione/scenario) con gli ingressi e la proiezione esplicita dei DTO (`detailing`, `anchorage`) o l'eccezione (`error`, `message`). |
| `bond-legacy.csv` | `ConcreteBond.Calculate` in tre blocchi: fck × Ø × η1; fck × αct × γc; Ø × αct × γc. fck 0, −1, NaN, +∞, 5, 10 e le classi del catalogo da 12 a 90; Ø da 6 a 40, 131,9 e 132; αct 1, 0,85, 0, −0,5, NaN e 1,2; γc 1,5, 1, 0,9, NaN e +∞. Rifiuti con tipo e messaggio. |
| `curvature-production-legacy.csv` | `ConcreteCurvatureAnalysis` con le impostazioni di produzione (`ConcreteCalculationSettings`: 60 passi, 64 direzioni, campionamento quadratico, tolleranza 1 kN, 12 bisezioni) e lo Status completo: θ = 0°, 90°, 180°, 30°, 60°, 135°, 217° e 333°, trazione del calcestruzzo, frazione < 1 senza bisezioni, campionamento uniforme, sezione cava, circolare a due anelli, sezione a T, EN 1992-1-1 e Model Code 2010; rifiuti: tolleranza stretta sul residuo, trefoli, N oltre il dominio, passi non validi, frazione > 1. |

Formati: numeri `R` invarianti, anche dentro il JSONL (come stringhe, così NaN e infiniti restano scritti). Nei CSV
nomi, messaggi e Status restano esatti: `%`, `;`, `|`, CR e LF sono scritti come `%25`, `%3B`, `%7C`, `%0D`, `%0A`
(`Uri.UnescapeDataString` li ripristina). I testi che il legacy compone con la cultura corrente (Status delle curve)
si scrivono con it-IT, come nella cattura headless; la cultura è nella riga `#`.

Il riferimento esatto di questi file è la cattura **F2-pre-f28** (`supporto/artefatti/baseline/F2-pre-f28`, confronto
`pre-f28` di `tests/ANTHEA.Testing/f2-classificazione.json`), catturata con `--motore legacy`. Nei confronti `b0`,
`pre-m4` e `f2-libreria` i sei file sono esclusi (`file_esclusi`).

## Modalità mesh

Per ogni sezione del catalogo (rettangolari, a T, circolari con uno e due anelli di barre e con 12–720
lati, cave e tubolari, default dei moduli sezione c.a., palo orizzontale e muro a mensola) la mesh si
costruisce come nel solutore c.a.: `CheckerSection.PrepareModel` e `ReinforcedConcreteSection.Mesh`,
con la dimensione automatica degli elementi. Colonne principali di `mesh-fingerprint.csv`:

- `nodes`, `faces`: numero di nodi e di facce (`triangles`, `quads`);
- `shaNodes`: SHA-256 di `id;x;y;z` di ogni nodo, nell'ordine della mesh;
- `shaFaces`: SHA-256 di `id;a;b;c;d` di ogni faccia, nell'ordine della mesh;
- `shaCanonical`: facce come cicli di coordinate, indipendente da numerazione e ordine;
- `shaOutline`: contorno e fori passati a `Shape2d`, per distinguere un cambio di input da un cambio del mesher;
- `solverMesh`: `same` se dopo la costruzione del solutore e un'analisi tensionale la sezione conserva la mesh misurata.

Mesh identiche bit per bit: stessi `nodes`, `faces`, `shaNodes` e `shaFaces`. Confronto con il
riferimento:

```powershell
$old = Get-Content <riferimento>\mesh-fingerprint.csv | Where-Object { $_ -notmatch '^#' }
$new = Get-Content <nuova>\mesh-fingerprint.csv | Where-Object { $_ -notmatch '^#' }
Compare-Object $old $new   # nessuna riga: mesh invariate
```

Confronto di due corse con il manifest (segnala anche i file con identificativi casuali, vedi sotto):

```powershell
$a = (Get-Content <a>\capture-manifest.json -Raw | ConvertFrom-Json).outputs
$b = (Get-Content <b>\capture-manifest.json -Raw | ConvertFrom-Json).outputs
Compare-Object $a $b -Property file, sha256
```

## Riproducibilità

Due corse sullo stesso binario danno file identici byte per byte, salvo identificativi casuali che
non entrano nei calcoli: i GUID degli archivi del Model (`stress-`, `crack-`, `detailing-sections.xml`,
`detailing-plate-sections.xml`)
e gli id delle azioni creati dall'aggiornamento dei documenti dei muri in versione 1 (6 righe di
`walls-combinations.jsonl`, 2 di `walls-documents.jsonl.gz`). Questi file si confrontano dopo aver
sostituito gli identificativi. Le uscite della modalità `mesh` sono identiche byte per byte.

Il riferimento B0 delle griglie dense e delle mesh è in `supporto/artefatti/baseline/F0-B0/dense`
(cartella non versionata), con il manifest di ogni modalità e l'esito della doppia corsa.

Dal passo F2.1 (`docs/refactoring/f2.1-banco.md`):

- `tools/banco/Invoke-DenseCapture.ps1 -Output <cartella>` compila lo strumento e cattura le
  quattro modalità con `--manifest`, con un log per modalità; `-Motore legacy|libreria` passa
  `--motore` a ogni modalità;
- `ANTHEA.Testing compare-dense` confronta due catture, o una cattura con le fixture di Checker,
  con le regole sopra (righe `#` e `header` escluse, identificativi casuali sostituiti) e la
  classificazione versionata `tests/ANTHEA.Testing/f2-classificazione.json`;
- il riferimento pre-M4 del c.a. è `supporto/artefatti/baseline/F2-pre-m4-v2/a` (commit d2d3225,
  7/10/2026, con D7-b, D7-d d2 e la fascia interna dei fori); la prima cattura di F2.1
  (`F2-pre-m4/a`, commit 75e4626) resta come storico;
- le fixture c.a. di Checker (`GPCChecker.Test.Concrete/Fixtures`) sono i file di
  `F2-pre-m4-v2/a/tutte` (CSV e XML del c.a.; Checker 06d97733);
- dal passo F2.8-A0 il riferimento esatto di tutte le modalità, file nuovi compresi, è
  `supporto/artefatti/baseline/F2-pre-f28/a` (`--motore legacy`, doppia corsa in `b`, LEGGIMI con
  conteggi e misure); le fixture di CL1 e CL2 di Checker (`detailing-plate-*`, `bond-legacy.csv`)
  si copiano da lì.
