# Unificazione dei calcoli e controllo dei progetti

Revisione del 25 settembre 2026. Repository ANTHEA; riferimento committato: `7d1fcbf91196e4d7d28b8449e3a248b71a4d51b3`.

## Risultato del riordino

Il catalogo, le factory e la validazione dei fogli sono definiti una volta in `X.Core/ModuleCatalog.cs`. La creazione di archivi, progetti, sezioni e fogli usa `ProjectDocuments`. Moduli singoli, albero dei progetti e strumenti senza interfaccia attingono alle stesse definizioni.

`CalculationService.Calculate(module, data, token)` è l’ingresso senza interfaccia. Lavora su una copia dei dati e chiama gli stessi motori dei fogli. I servizi con risultati tipizzati restano disponibili per l’aggiornamento selettivo della UI: l’interfaccia conserva gestione degli eventi, cancellazione, grafici, tabelle ed esportazione.

| Ambito | Motore / servizio condiviso |
|---|---|
| Palo e micropalo verticali | `Calcolo`, con le factory di `ModuleCatalog` |
| Palo e micropalo orizzontali | `PaloOrizzontale`, `MicropaloOrizzontale` e relativi modelli di sezione |
| CA: domini, tensioni, fessurazione | `ConcreteAnalysisSession` → `CheckerSection` / `Ntc2018Checks` |
| CA: taglio e torsione | `ConcreteShearAnalysis` → `Ntc2018Checks.Shear` / `ConcreteTorsionCalculator` |
| CA: momento–curvatura | `ConcreteCurvatureAnalysis` → `MomentCurvatureCalculator` e Checker |
| CA: dettagli e ancoraggi | `ConcreteDetailingAnalysis` → motori di durabilità e `ConcreteDetailingCalculator` / `ConcreteAnchorageCalculator` |
| Calcestruzzo: durabilità, copriferro e prescrizioni | `X.Core/Materials`, mantenendo il namespace `Materiali` |
| Aderenza | Unica espressione in `ConcreteBond`, usata da materiali e ancoraggi |
| Acciaio per armature | `RebarMaterial` e materiali della libreria Model |
| Sezione composta da ponte | Adapter `BridgeSection` e libreria `GPCChecker.CompositeBridge`; mantenuti i diversi metodi di analisi |

`ConcreteCalculationSettings` prepara coefficienti, staffe, taglio, torsione, curva e dettagli prima dell’apertura delle schede. I coefficienti condivisi αcc, γc e γs provengono dall’input della sezione. Le copie delle opzioni normative sono sincronizzate e non costituiscono un secondo dato indipendente.

L’ingresso JSON CA esegue domini 3D/2D SLU/SLV, tensioni/fessurazione SLE e le righe di taglio/torsione. Curve, dettagli e ancoraggi hanno ingressi espliciti separati, perché richiedono scelte aggiuntive dell’utente. Per il modulo materiali CLS, l’ingresso JSON espone proprietà e copriferro; gli altri risultati sono accessibili dai rispettivi servizi. `calcoli_inclusi` dichiara il perimetro dell’output.

## Correzioni e controlli sui dati

- Un identificativo di modulo sconosciuto viene rifiutato; non produce più accidentalmente i dati di un palo.
- Foglio singolo e foglio di progetto nascono dagli stessi valori iniziali. Ogni foglio possiede una copia indipendente dei dati.
- Progetti e sezioni nuovi contengono sia l’elenco dei fogli sia quello delle sottosezioni, con identificativi nuovi e nomi automatici non duplicati fra fratelli.
- La creazione del foglio applica l’eredità dopo il collegamento all’albero reale. Se l’eredità fallisce, annulla l’inserimento senza lasciare un foglio parziale.
- Le combinazioni CA malformate e i contenitori di impostazioni non validi vengono rifiutati prima della migrazione. Non vengono sostituiti silenziosamente con elenchi vuoti. Il controllo della struttura non vieta il salvataggio di testi numerici ancora incompleti nei campi di input.
- La migrazione del segno di N resta quella esistente e si esegue una sola volta. I calcoli senza interfaccia non modificano l’archivio sorgente.
- Cache SLE e domini sono nel servizio comune. Una variazione delle azioni invalida lo stato; una variazione dei soli criteri di fessurazione riusa lo stato tensionale e aggiorna la verifica. Input non validi non restituiscono il precedente stato come corrente.
- La conferma dell’ancoraggio a taglio viene invalidata quando cambia la geometria numerica, non per una diversa rappresentazione testuale dello stesso numero.
- Nei progetti, geometria, materiali e armature dei ponti partecipano a eredità, confronto e report. Seconda piattabanda, armature disattivabili e sovrascrittura fy sono gestite insieme ai rispettivi dati. Fasi, ritiri, carichi e opzioni di analisi restano propri del foglio. Nessuna conversione implicita fra materiali del ponte e legami personalizzati del CA.
- Il test dell’aderenza della scheda Materiali ora segue il separatore decimale della cultura corrente, conservando il benchmark numerico.

## Metodi mantenuti distinti

Unificare il codice comune non equivale a sostituire i modelli fisici. Per il ponte restano i metodi cumulativo, con storico e non lineare, oltre alle curve M–κ/N–ε. Non sono stati fusi in un solo algoritmo.

`SezioneCA`/`SezioneElastica` e `CalcoloSezione` mantengono le API storiche. Il palo orizzontale utilizza ancora il proprio percorso di capacità della sezione, con convenzioni e affinamento numerico esistenti. Il foglio CA corrente e il comando `--calcola` usano invece i servizi Checker. Cambiare il modello del palo richiederebbe una migrazione fisica e una validazione dedicate, non una sostituzione di nomi.

I motori normativi non sono stati riformulati in questa attività. Le prove di parità verificano il riordino e la coerenza fra percorsi, non costituiscono una validazione indipendente delle formule.

## Prove eseguite

Output in `supporto/artefatti/unificazione_progetto/`; nuovi sorgenti in `supporto/test/X.Verifiche/ProjectCalculationChecks.cs`.

| Prova | Esito |
|---|---|
| Compilazione Release desktop | 0 errori, 0 avvisi |
| Nuovi controlli su progetti e servizi | 80 superati |
| Checker e CA: domini/NTC, Excel, estensioni, dati | 102 + 18 + 174 + 27 superati |
| Modulo CA ampliato | 78 superati |
| Palo orizzontale | 1.079 superati; anche la suite CHS superata |
| Coesione efficace | 312 superati |
| Peso specifico saturo | 57 superati |
| Sezione da ponte | 117 superati |
| Micropalo verticale | 34 casi di riferimento, 101.883 confronti superati |
| Interfaccia | Progetti, workspace progetto, gerarchie, condivisione, materiali, acciaio, CA, estensioni CA, curve ponte, palo orizzontale e report progetto superati |
| Eseguibile pubblicato in `app` | Creazione, trascinamento, rinomina, gerarchie, conservazione input e riapertura progetti superati |
| Vecchi riferimenti Python | **414 casi superati, 50 differenze su 464 casi** |
| Aggregatore storico archivi/report | **Non completato: interruzione sul salvataggio del report micropalo** |

I conteggi delle suite comprendono controlli di coerenza e confronti numerici; non rappresentano altrettanti benchmark indipendenti.

### Interruzione da approfondire nel salvataggio del report micropalo

L’aggregatore `--software` si interrompe ripetutamente durante `File.Move(temp, path, true)` di `Archivio.ScriviAtomico`, con destinazione `micro.docx`. Il processo restituisce zero senza completare il metodo; non viene intercettata un’eccezione gestita. La diagnostica temporanea ha verificato che scrittura e `Flush(true)` del file temporaneo terminano. Il pacchetto DOCX generato è leggibile e l’XML risulta valido. Il medesimo flush e spostamento eseguiti separatamente dal terminale sono riusciti.

Questo esito non identifica ancora la causa dell’interruzione, né dimostra un errore delle formule. Il salvataggio atomico di produzione è stato lasciato invariato e la diagnostica temporanea rimossa. La suite non è conteggiata fra quelle superate; le prove UI del report di progetto sono invece concluse. Tracce: `software-io.log`, `software-final.trace.txt` e `software-local.trace.txt`. Gli output intermedi sono conservati sotto `run-*`.

`supporto/scripts/Test-SoftwareReports.ps1` controlla anche la presenza del messaggio finale nella traccia e restituisce un errore se il processo termina senza aver completato le asserzioni, anche con codice zero.

### Differenze storiche riprodotte

Gli stessi 464 casi sono stati eseguiti anche su una copia dei sorgenti ANTHEA committati. I 50 messaggi di differenza sono identici e i due riepiloghi JSON hanno lo stesso SHA-256:

`3BBD26EDE2F4AEA7CDDDE252F9E4588C5D8D5D0D0994BDCC4CDD0498C7E3DF4B`

La baseline è sotto `baseline-head/`. Per renderla compilabile è stato necessario reinserire i tre sorgenti esterni del ponte allora collegati da Checker, usando quelli attuali con il namespace precedente; nessuno dei 464 casi chiama quei tre sorgenti. I motori ANTHEA e le DLL della baseline provengono dal commit indicato.

| Famiglia di confronto | Differenze |
|---|---:|
| Capacità del palo, casi storici | 2 |
| Geometria/risultati delle sezioni | 24 |
| Analisi elastica | 16 |
| Resistenza elastica limite | 4 |
| Domini | 4 |

Sono quindi divergenze preesistenti rispetto ai riferimenti Python, non introdotte da questo riordino. Il confronto da solo non stabilisce quale delle due formulazioni sia corretta. I riferimenti e le tolleranze sono rimasti invariati: serve una revisione mirata dei casi circolari e a T e dei due casi del palo prima di dichiarare interamente superata la vecchia suite.

## Ripetizione delle prove

```powershell
dotnet build X.Desktop/X.Desktop.csproj -c Release
dotnet run --project supporto/test/X.Verifiche -c Release -- --project-calculations supporto/artefatti/unificazione_progetto/core
dotnet run --project supporto/test/X.Verifiche -c Release -- --checker
dotnet run --project supporto/test/X.Verifiche -c Release -- --ca-module
dotnet run --project supporto/test/X.Verifiche -c Release -- --horizontal
dotnet run --project supporto/test/X.Verifiche -c Release -- --coesione
dotnet run --project supporto/test/X.Verifiche -c Release -- --gamma-sat
dotnet run --project supporto/test/X.Verifiche -c Release -- --bridge
dotnet run --project supporto/test/X.Verifiche -c Release -- --micropalo supporto/test/casi_confronto.json
dotnet run --project supporto/test/X.Verifiche -c Release -- --reference-only supporto/test/casi_confronto.json supporto/artefatti/unificazione_progetto/riferimenti-python.json
```

L’ultimo comando restituisce 1 finché sono presenti le differenze documentate. Il runner intercetta esplicitamente le eccezioni per produrre una diagnostica e un codice di uscita non nullo.

Per l’aggregatore archivi/report usare il controllo esterno di completamento:

```powershell
./supporto/scripts/Test-SoftwareReports.ps1 -OutputDirectory supporto/artefatti/unificazione_progetto
```

## Aggiornamento del 26 settembre 2026

I servizi numerici descritti sopra sono stati trasferiti da X.Core a X.Calculations, assembly ANTHEA.Calculations, senza dipendenze dalla UI o dalla gestione degli archivi. Il resoconto aggiornato è in [validazione-libreria-calcolo.md](validazione-libreria-calcolo.md); architettura e trasferimento in [libreria-calcolo.md](libreria-calcolo.md).

La nuova prova del report ricorsivo di progetto è **incompleta**: il processo termina prima dell’attestazione finale. Il solo codice zero e il file di avanzamento non autorizzano a considerare la suite superata. Il nuovo runner controlla espressamente l’attestazione finale. Restano documentate anche le 50 differenze storiche dei riferimenti Python e l’interruzione del report micropalo.
