# ANTHEA.Testing

Toolkit unico di cattura, normalizzazione e confronto del refactoring (piano F0.4). Le fasi
successive lo estendono: nuovi casi nel corpus, nuovi motori, nuove regole di tolleranza.
Non si creano altri comparatori.

Il progetto (net8.0, eseguibile) referenzia `X.Core` e quindi `X.Calculations`; le DLL GPC
arrivano dalle proprietà comuni (`Directory.Build.targets`), senza riferimenti diretti a
`lib/Checker`.

## Comandi

Dalla radice del repository, dopo `dotnet build tests\ANTHEA.Testing\ANTHEA.Testing.csproj -c Release`:

```
dotnet tests\ANTHEA.Testing\bin\Release\net8.0\ANTHEA.Testing.dll capture <uscita> [--tag B0] [--commit <sha>] [--only <regex modulo/caso>] [--no-trace]
dotnet tests\ANTHEA.Testing\bin\Release\net8.0\ANTHEA.Testing.dll compare <a> <b> [--report confronto.json] [--tolerances <file>] [--max 40]
dotnet tests\ANTHEA.Testing\bin\Release\net8.0\ANTHEA.Testing.dll normalize <ingresso> <uscita>
dotnet tests\ANTHEA.Testing\bin\Release\net8.0\ANTHEA.Testing.dll compare-dense <riferimento> <candidato> --confronto <nome> [--classificazione <file>] [--report confronto.json] [--max 40]
```

- `capture` scrive in una cartella nuova o vuota. Esce con 0 anche quando un caso lancia
  un'eccezione: l'eccezione fa parte del comportamento ed è scritta al posto del risultato
  (`errore_cattura`).
- `compare` esce con 0 se le due catture sono uguali entro le tolleranze, con 1 se ci sono
  differenze non ammesse, con 2 per argomenti errati.
- `normalize` rende confrontabili le uscite delle prove WPF e i report: DOCX in testo per
  paragrafo e cella, JSON in forma canonica, testi normalizzati, PNG e BMP ridotti alle
  dimensioni.

- `compare-dense` (F2.1) confronta le catture dense di `supporto/test/CheckerMigration.Capture`
  con un riferimento: un'altra cattura densa o le fixture del legacy congelate nelle librerie.
  Esce con 0 solo se ogni differenza è ammessa dalle tolleranze o classificata in
  `f2-classificazione.json` e ogni differenza attesa è trovata con i conteggi dichiarati; 1
  altrimenti; 2 per argomenti errati. Vedi la sezione "Confronto delle catture dense".

Nel runner: `build\ci.ps1 -Profile baseline` (standard più cattura e banco del c.a.) e, con
`-BaselineRef <cartella>`, anche il confronto con una cattura di riferimento.

## Contenuto di una cattura

| Cartella o file | Contenuto |
| --- | --- |
| `inputs/<modulo>/<caso>.json` | dati del foglio passati al calcolo |
| `results/<modulo>/<caso>.json` | `CalculationService.Calculate(modulo, dati)` |
| `engines/<motore>/…` | motori non coperti dal servizio (vedi sotto) |
| `reports/<modulo>/<caso>[.variante].txt` | testo dei report DOCX headless, figure nulle |
| `reports/<modulo>/<caso>.*.csv` | esportazioni CSV dei moduli |
| `archives/<file>.json` | lettura, scrittura e rilettura di ogni documento di `supporto/esempi` |
| `fallbacks.json` | ripieghi di `J.S`, `J.D`, `J.B` incontrati durante la cattura |
| `manifest.json` | commit, SHA delle DLL caricate e di `lib/Checker/manifest.json`, SDK, runtime, corpus |
| `log.txt`, `raw/` | tempi, DOCX originali, archivi riscritti: esclusi dal confronto |

JSON canonico: chiavi in ordine ordinale, numeri come double in formato `R` invariante,
GUID sostituiti da `<GUID-n>` numerati nell'ordine del documento (prima l'input, poi
`dati`/`input` del risultato, poi il resto). Nei testi si sostituiscono anche le date del
giorno della cattura (± 1 giorno) con l'ora che le segue, le durate in millisecondi e i
percorsi assoluti. Le date costanti (riferimenti normativi) restano. I testi oltre 2 milioni
di caratteri si scrivono compressi (`.gz`), letti in modo trasparente da `compare`.

## Corpus

- default degli 11 moduli di `ModuleCatalog.All` e vista elastica dei due moduli orizzontali;
- esempi della Wiki (`X.Desktop/Wiki/Examples`), applicati come `MainWindow.WikiExamples`;
- documenti di `supporto/esempi` (`.json`, `.anthea`, `.programma`) letti con `Archivio.Leggi`;
- casi `palo` e `micropalo` di `supporto/test/casi_confronto.json` (i default dei pali
  verticali sono moduli vuoti);
- `RetainingWall.Example("gravity")` ed `Example("cantilever")`.

Motori fuori da `CalculationService`: risposta e armature dei tratti del palo elastico
(`CalculateResponse`, `CompleteReinforcement`), stabilità globale dei muri con la proposta
del profilo e con il modello di `GlobalStability.Checks`, `DesignReinforcement` e distinta
dei muri a mensola, curve di risposta della sezione composta (`ResponseDefaults`: momento–
curvatura, forza–deformazione, dopo la fase 0), ottimizzazione di Bridge Design con le
opzioni predefinite. Ciò che resta escluso è elencato in `manifest.json` (`non_coperto`).

## Tolleranze

`tolerances.json`, versionato, è l'unica politica di tolleranza:

- numero JSON: `|a − b| ≤ abs + rel · max(|a|, |b|)` con la grandezza scelta dalla prima
  regola che corrisponde al nome della proprietà (o della colonna CSV) e al percorso; mai
  `max(1, |x|)`. Default 0: uguaglianza esatta per i refactoring puri;
- testi (report, celle CSV, stringhe): scheletro senza numeri identico; interi uguali;
  decimali entro metà dell'ultima cifra stampata di ciascun valore più la tolleranza della
  grandezza. Le righe che contengono JSON (`# NOME {…}`) si confrontano come JSON;
- `volatili`: chiavi escluse dal confronto, ciascuna con il motivo;
- `file_esclusi`: `manifest.json`, `log.txt`, `raw/` e i report di confronto.

Classi delle differenze: `file-aggiunto`, `file-rimosso`, `chiave-aggiunta`,
`chiave-rimossa`, `elemento-aggiunto`, `elemento-rimosso`, `riga-aggiunta`, `riga-rimossa`,
`tipo`, `valore`, `testo`, `numero` (non ammesse); `numero-entro-tolleranza`,
`arrotondamento`, `volatile` (ammesse e riportate).

## Confronto delle catture dense

`f2-classificazione.json`, versionato, descrive i confronti per nome (`--confronto`):

| Confronto | Riferimento | Tolleranze | Uso |
| --- | --- | --- | --- |
| `fixture-checker` | `Checker/GPCChecker.Test.Concrete/Fixtures` (ricatturate il 7/10/2026 da `F2-pre-m4-v2/a/tutte`, SHA-256 a fine riga LF fissati nel file; nessuna differenza attesa) | regole `fixture-checker/` di `tolerances.json`, allineate ai MigrationTests | suite `baseline/banco-ca` |
| `b0` | `supporto/artefatti/baseline/F0-B0/dense/<modalità>` | esatte (`denso/`) | misura F2.1 rispetto a B0 |
| `pre-m4` | `supporto/artefatti/baseline/F2-pre-m4-v2/a/<modalità>` (nessuna differenza attesa) | esatte (`denso/`) | doppia corsa e passi F2.5-F2.9 (`-DenseRef`) |

Regole del confronto:

- si confrontano i file della modalità (o quelli dichiarati dal confronto); `capture-manifest.json`
  è escluso;
- si escludono le righe che iniziano con `#` e le righe JSONL `{"header":…}` (commit e SHA-256
  di `ANTHEA.Calculations.dll`);
- gli identificativi casuali dichiarati (GUID degli archivi del Model, id delle azioni dei muri
  in versione 1) diventano `<GUID-n>` e `<ID-n>` nell'ordine di comparsa nel file;
- CSV per cella (`;`), con l'intestazione confrontata esattamente; JSONL per valore JSON; altri
  file per riga;
- i numeri dentro celle e stringhe si leggono nel formato invariante e si confrontano con la
  grandezza della regola che corrisponde a `<prefisso>/<file>:<campo>`; il resto del testo deve
  essere identico. Nessuna tolleranza di stampa: i valori sono scritti con `R`;
- campo: nome della colonna (o `c<i>` per i CSV senza intestazione né colonne dichiarate),
  percorso JSON (`$.result.avvisi[3]`) o `testo`.

Classi ammesse: `identificativo-casuale` (conteggiato per riga), `numero-entro-tolleranza`. Classi
da classificare: `file-aggiunto`, `file-rimosso`, `riga-aggiunta`, `riga-rimossa`,
`intestazione`, `struttura`, `testo`, `numero`, `tipo`, `valore`, `chiave-aggiunta`,
`chiave-rimossa`, `elemento-aggiunto`, `elemento-rimosso`. Una differenza attesa ha id,
categoria (`convenzione`, `intenzionale`, `difetto-legacy`, `rumore-numerico`), motivo ed
espressioni regolari su file, righe (chiave della riga), campo, classe e valori `a` e `b`;
`righe_attese` e `differenze_attese` la rendono esatta. Una differenza attesa di un file non
confrontato (un'altra modalità) non è applicabile. Un riferimento con SHA-256 diverso da quello
dichiarato è un errore.

## Registro dei ripieghi

Con la variabile `ANTHEA_TRACE_FALLBACKS=<file>`, `X.Calculations/JsonData.cs` registra ogni
ripiego distinto di `J.S`, `J.D` e `J.B` (contesto, tipo, chiave, valore di ripiego, genitore,
metodo chiamante). I valori restituiti non cambiano. `capture` attiva la variabile e salva
l'insieme ordinato in `fallbacks.json` (`--no-trace` lo disattiva). La compilazione a livelli
è disattivata nel toolkit (`TieredCompilation=false`) perché il chiamante registrato non
dipenda dall'inlining del momento.

## Non determinismi noti

- Tempi di esecuzione `tempi_ms` delle armature del palo elastico
  (`ElasticHorizontalPile.Reinforcement.cs:52`), anche dentro la riga
  `# ARMATURE_E_VERIFICHE` del CSV: chiave volatile.
- GUID generati a ogni chiamata: azioni dei muri (`RetainingWall.NewAction`, anche nella
  migrazione dei documenti di versione 1 a ogni calcolo), righe senza id della sezione c.a.
  (`SectionWorkspace.Prepare`), identificativi e revisioni dei progetti
  (`ProjectRevisions`): normalizzati.
- Data e ora nei report (`DateTime.Now` in `ReportWord`, `ReportBridge`, `ReportConcrete`,
  `ReportConcreteShort`, `BridgeConceptExport`): normalizzate.
