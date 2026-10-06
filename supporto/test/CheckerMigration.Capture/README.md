# CheckerMigration.Capture

Congela le uscite dei nuclei di calcolo di `X.Calculations` su griglie dense e l'impronta delle mesh
delle sezioni in c.a. Serve come riferimento prima di spostare codice o di cambiare le DLL di
`lib/Checker`. Formato dei numeri: `R` invariante. Ogni file riporta nella prima riga il commit
passato e lo SHA-256 di `ANTHEA.Calculations.dll`, che dipende anche dal percorso del checkout:
nei confronti si escludono le righe `#` e le righe `header`.

## Comandi

Dalla radice del repository:

```powershell
dotnet build supporto\test\CheckerMigration.Capture\CheckerMigration.Capture.csproj -c Release -nologo
dotnet supporto\test\CheckerMigration.Capture\bin\Release\net8.0\CheckerMigration.Capture.dll <cartella> <commit> [modalità] [--manifest]
```

Con un altro insieme di DLL si compila con `-p:GpcLibDir=<cartella>\` (vedi `Directory.Build.props`).

| Modalità | Contenuto | File | Durata indicativa |
| --- | --- | --- | --- |
| `tutte` (o nessuna) | taglio, SLE, torsione, fessurazione, ancoraggi e aderenza, dettagli, M-χ, durabilità, pali, muri | `shear`, `stress`, `torsion*`, `crack*`, `anchorage`, `detailing`, `curvature`, `durability` (CSV e XML del Model), `piles-*.jsonl`, `walls-*` | 1 min |
| `muri` | solo muri di sostegno | `walls-functions.jsonl`, `walls-combinations.jsonl`, `walls-documents.jsonl.gz` | 45 s |
| `pali` | solo pali e micropali | `piles-nq.jsonl`, `piles-bustamante-doix.jsonl`, `piles-chs.jsonl`, `piles-lateral.jsonl`, `piles-vertical.jsonl` | pochi secondi |
| `mesh` | impronta delle mesh DelaunayMesh delle sezioni usuali | `mesh-fingerprint.csv`, `mesh-detail.jsonl.gz` | pochi secondi |

Un terzo argomento diverso da `muri`, `pali` e `mesh` esegue tutto, come prima. La geotecnica
generale (`GeotechnicsCapture`) si compila solo con `LEGACY_GEOTECHNICS` su un checkout fino a
dadea50: i suoi riferimenti sono in `GPCChecker.Test.Geotechnics/Fixtures`.

`--manifest`, dopo la modalità, aggiunge `capture-manifest.json`: argomenti e riga di comando,
commit passato, assembly caricati dalla cartella dell'applicazione (DLL GPC comprese) con versione e
SHA-256, SHA-256 di ogni file prodotto, tempi. Senza l'opzione le uscite non cambiano.

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

Confronto di due corse complete con il manifest:

```powershell
$a = (Get-Content <a>\capture-manifest.json -Raw | ConvertFrom-Json).outputs
$b = (Get-Content <b>\capture-manifest.json -Raw | ConvertFrom-Json).outputs
Compare-Object $a $b -Property file, sha256
```

Il riferimento B0 delle griglie dense e delle mesh è in `supporto/artefatti/baseline/F0-B0/dense`
(cartella non versionata), con il manifest di ogni modalità e l'esito della doppia corsa.
