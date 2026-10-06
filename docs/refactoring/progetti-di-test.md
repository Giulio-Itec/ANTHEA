# Destino dei progetti di test

Tabella del passo F1.6 [CP], da approvare. Stato al 6 ottobre 2026, ramo `refactoring/f1-test-fuori-exe`.
Le voci "Nel runner" si riferiscono a `build/ci.ps1` (stadi `fast`, `regression`, `wiki`, `ui`, `word`);
"Compila" è l'esito di `dotnet build -c Release` nel worktree di F1.

## Prove WPF dopo F1: configurazione UiTests

- Le prove WPF (`supporto/test/Desktop`), gli autotest della scheda Materiali (`supporto/test/Materiali`) e i
  controlli condivisi (`supporto/test/Shared`) entrano in X.Desktop e X.Materiali solo con
  `dotnet build X.Desktop/X.Desktop.csproj -c UiTests` (ottimizzata come Release, uscita in `bin/UiTests`).
- I comandi di prova sono in `supporto/test/Desktop/AppHarness.cs`: `--smoke-*`, `--check-*`,
  `--check-concrete-design <cartella>`, `--capture-validation <ingressi.json> <cartella>`.
- La Release e la pubblicazione non contengono codice di prova: `tools/qa/Assert-NoTestCode.ps1` lo verifica nel
  runner (`qa/no-test-code`) e in `supporto/installer/Crea-Installer.ps1`, che si ferma se trova marcatori.
- Il runner compila X.Desktop in UiTests per lo stadio `ui` e lancia `X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.exe`.

### F1.5: perché `tests/ANTHEA.Desktop.UiTests` non è stato creato

Il progetto separato avrebbe dovuto ospitare le quattro prove offscreen già scritte come classi statiche.
Non è possibile farlo in modo pulito senza allargare la visibilità, che la decisione (c) esclude fino a F5:

1. `--check-wiki-offscreen` termina in `MainWindow.CheckWikiIntegration`, una partial che usa 16 membri privati di
   MainWindow (`wiki`, `editor`, `currentSheet`, `body`, `moduleView`, `dashboard`, `wikiHelp`, `dirty`, `path`,
   `document`, `OpenWikiModule`, `ResumeCalculation`, `NewCalculation`, `NewProjects`, `AddProject`, `ShowProjects`).
   Da un altro assembly servirebbero membri internal o la riflessione.
2. Le altre tre (`--check-global-guidance-offscreen`, `--check-wall-advanced-offscreen`,
   `--check-wall-materials-offscreen`) usano solo membri internal di RetainingWallWorkspace: potrebbero passare con un
   `InternalsVisibleTo`, ma si avrebbero due harness per lo stesso fine fino a F5, che riscrive proprio queste viste.
3. Un eseguibile separato deve caricare le risorse di `App.xaml` senza eseguire `App.OnStartup` di produzione: si
   fa con `App.InitializeComponent()` e un ciclo del dispatcher proprio, oppure estraendo le risorse in un
   `ResourceDictionary`. La seconda strada cambia la produzione e va verificata con le prove a schermo
   (`--check-appearance`, `--smoke-display`), non eseguibili durante F1.

Resta quindi la configurazione UiTests. In F5 ogni suite passa a `tests/ANTHEA.Desktop.UiTests` insieme alla
riscrittura MVVM del suo modulo; a fine F5 la configurazione UiTests e `supporto/test/Desktop` spariscono.

## Progetti in `supporto/test`

| Progetto | Cosa verifica | Come si esegue | Nel runner | Compila | Destinazione | Motivo |
| --- | --- | --- | --- | --- | --- | --- |
| X.Verifiche | Regressione numerica su `casi_confronto.json` (464 casi) e suite a flag: Checker, ponte, palo orizzontale, coesione, γsat, modulo e dati c.a., micropalo, software, audit e calcoli di progetto, benchmark | `dotnet run --project supporto/test/X.Verifiche -c Release -- [--flag]` | Sì: 8 suite `fast`, 6 `regression` (`project-calculations` è un fallimento noto) | Sì | Tenere | Rete di regressione dell'applicazione. La regressione diventa MSTest tra F1 e F2; le parti di calcolo seguono i calcoli nelle librerie (F2–F4) |
| CalculationLibrary.Checks | ANTHEA.Calculations senza UI: dati predefiniti dei moduli, omogeneizzazione, aderenza, copriferro, sezioni GPC, ponte; da F1.3 anche i controlli di riferimento di durabilità e copriferro NTC | `dotnet run --project supporto/test/CalculationLibrary.Checks -c Release` | Sì, `fast` | Sì | Spostare nei test di libreria | Verifica calcoli che in F2–F4 passano a GPCChecker e ANTHEA.Application |
| ConcreteCode.Checks | Taglio, fessurazione e verifiche c.a. contro `reference.json` prodotto da `reference.py` (atteso indipendente) | `dotnet run --project supporto/test/ConcreteCode.Checks -c Release` | Sì, `fast` | Sì | Spostare nei test di libreria (GPCChecker.Concrete, F2) | Calcoli c.a.; l'atteso Python resta invariato |
| ConcreteDesign.Checks | Ricerca automatica delle armature: alternative, ordinamento, parallelismo, opzioni; scrive `ui-fixture.json` | `dotnet run --project supporto/test/ConcreteDesign.Checks -c Release -- [cartella]` | Sì, `regression`; prepara anche `ui/check-concrete-design` | Sì | Spostare nei test di GPC.Design (F4.12) | Progetto delle armature (D2) |
| BridgeDesign.Checks | Bridge Design: calcoli, audit, esplorazione, ottimizzazione, relazione Word | `dotnet run --project supporto/test/BridgeDesign.Checks -c Release -- <cartella>` | Sì, `regression` | Sì | Spostare nei test di GPC.Design (F4.11) | BridgeConcept va in GPC.Design |
| BridgeDesign.IndependentChecks | 100 travi casuali confrontate con `verify.py` (atteso indipendente) | `dotnet run ... -- <cartella>`, poi `python verify.py` | Sì, `regression` (solo la parte C#) | Sì | Spostare con BridgeDesign.Checks (F4.11) | Conserva l'atteso indipendente |
| BridgeDesign.SiteComparison | Confronto con un sito web esterno (acquisizioni CUA) | `README.md` del progetto | No (sito esterno) | Sì | Archiviare, su decisione dell'utente | Dipende da un servizio esterno; riferimenti esterni in uscita con W0.5 |
| BridgeValidationCurrent | Test MSTest di `GPCChecker.Test.CompositeBridge` compilati dal repository Checker accanto ad ANTHEA | `dotnet test` con Checker in `../Checker` | No | Sì, ma senza Checker accanto compila un progetto vuoto | Spostare nei test di libreria (INF.10) | Sono test di Checker |
| CheckerMigration.Capture | Cattura congelata delle uscite legacy prima della migrazione a Checker (M2) | `dotnet run ... -- <cartella> <commit>` | No (eseguita nella baseline) | Sì | Tenere fino a F4, poi archiviare | Riferimento per le prove di equivalenza |
| ConcreteShort.Checks | Report short c.a. in Word; ricompila `X.Desktop/Wpf/ShortReportExport.cs` | `dotnet run ... -- <cartella>` (serve Microsoft Word) | Sì, stadio `word` (fuori dai profili) | Sì | Spostare nei test di ANTHEA.Reports (F3.4) | Togliere il collegamento a un sorgente di X.Desktop |
| ConcreteShort.UiChecks | Pulsante "Report short" della barra di MainWindow, per riflessione | `dotnet run ... -- <cartella>` | Sì, `ui` | Sì; da F1.2 contro X.Desktop UiTests (usa `FinishSmoke`) | Archiviare dopo F5 | La vista viene riscritta in F5 |
| ConcreteStressDiagnosis | Diagnosi delle tensioni di un file del 2 ottobre | `dotnet run ... -- <file.anthea> [--original]` | No (strumento) | Sì | Archiviare | Strumento una tantum |
| ElasticPile.Checks | Palo elastico in GPCChecker.Geotechnics: soluzioni esatte, Viggiani, armature, sisma | `dotnet run` con Checker in `../Checker` | No | Solo con il repository Checker accanto (nel worktree di F1: no) | Spostare nei test di libreria (INF.10) | Compila i progetti di Checker |
| ElasticPile.Performance | Misura dei tempi del palo elastico (diagnostica) | `dotnet run ... -- [input.json] [misure.json]` | No (input in `supporto/artefatti`) | Sì | Archiviare dopo F5, o spostare in `tools/` | Non è una verifica |
| ElasticPile.UiChecks | Palo elastico in WPF per riflessione; stili da `X.Desktop/App.xaml` letto dalla cartella corrente | `dotnet run ... -- <cartella>` dalla radice | Sì, `ui` | Sì (contro X.Desktop Release) | Archiviare dopo F5 | Citata per percorso nelle guide; la vista cambia in F5 |
| HorizontalPileGroup.Checks | Palificata orizzontale: motore e UI per riflessione | `dotnet run ... -- <cartella>` dalla radice | Sì, `ui` | Sì (contro X.Desktop Release) | Calcolo nei test di GPCChecker.Geotechnics; UI archiviata dopo F5.4 | Modulo pilota di F5 |
| GlobalStability.Checks | Stabilità globale (Bishop GPC) e relazioni | `dotnet run ... -- <cartella>` | Sì, `regression` | Sì | Metodo nei test di libreria, adattatore nei test di ANTHEA.Application (F3) | Il metodo è già in GPCChecker.Geotechnics |
| RetainingWall.Checks | Muri: azioni, modelli avanzati, distinta, due terreni, adattatore, materiali condivisi, sisma | `dotnet run ... -- <cartella>` | Sì, `regression` | Sì | Spostare con i muri in Concrete/Walls (F4.7–F4.9) e ANTHEA.Application | I muri passano in libreria |
| MaxRetainingWall.Cases | Preparazione dei casi di confronto con il programma MAX | `dotnet run ... -- <cartella>` | No (input esterni) | Sì | Archiviare, su decisione dell'utente | Confronto con un programma di terzi |
| MaxRetainingWall.Compare | Ripetizione di un cerchio rilevato in MAX con il solutore Bishop | `README.md` del progetto | No (input esterni) | Sì | Archiviare, su decisione dell'utente | Come sopra |
| ValidazioneCA20260925 | Campagna di validazione c.a. del 25/9 con riferimenti Python | `dotnet run ... -- <casi.json>` | No | No: `Extra.cs` non risolve `Ntc2018Checks`, già prima di F1 | Archiviare | I casi utili confluiscono nella regressione MSTest |
| Desktop (`supporto/test/Desktop`) | 20 prove `--smoke-*`, 6 `--check-*`, `--check-concrete-design`, `--capture-validation`, servizi di prova | ANTHEA.exe di UiTests; `build/ci.ps1 -Profile full` | Sì, `ui` | Sì, solo in UiTests | Tenere fino a F5, poi `tests/ANTHEA.Desktop.UiTests` modulo per modulo | Decisione (c): nessun allargamento di visibilità prima di F5 |
| Materiali (`supporto/test/Materiali`) | Autotest della scheda Materiali (`MaterialView.Check`), da F1.3 | Con `--smoke-materials` | Sì, `ui` (dentro smoke-materials) | Sì, solo in UiTests | Con la scheda Materiali in F3.9/F5.5 | La scheda viene accorpata e riscritta |
| Shared (`supporto/test/Shared`) | `DurabilityReferenceChecks`: durabilità e copriferro NTC, da F1.3 | In CalculationLibrary.Checks, X.Verifiche e smoke-materials | Sì, `fast` | Sì | Test di libreria con durabilità e copriferri (F2.9) | Saranno la prova di equivalenza dello spostamento |
| installer (`Test-Guide.ps1`) | Catalogo delle guide dell'installer, installazione e disinstallazione della documentazione in una cartella di prova | `powershell -File supporto/test/installer/Test-Guide.ps1` | No (serve NSIS) | n.a. | Tenere | Unico controllo dell'installer |
| ProgrammaAnthea (`qa.py`) | Controllo del documento "Programma ANTHEA" del 6/10 (xlsx e PDF) | `python qa.py` | No | n.a. | Archiviare (W0.3) | Controllo una tantum di un documento |
| WikiTechnicalLibrary (`verify.py`) | Esempi numerici e provenienza della biblioteca tecnica, resa delle guide | `python verify.py` | No | n.a. | Con la pipeline della Wiki (W0.2–W0.3) | Strumento della documentazione |
| `wiki-handbook-checks.py` | Catalogo, alias e migrazioni del manuale Wiki | `py -3 supporto/test/wiki-handbook-checks.py` | Sì, `wiki` (fallimento noto) | n.a. | Tenere fino allo strumento .NET (W2.1) | |
| `casi_confronto.json` | Dati della regressione (464 casi) | Usato da X.Verifiche | Sì | n.a. | Diviso per caso tra F1 e F2 (INF.7) | |

## Progetti spostati in F1.6

| Progetto | Prima | Ora |
| --- | --- | --- |
| ConcreteDesign.DesktopChecks | Ricompilava tutti i sorgenti di X.Desktop e le prove con un proprio App (AssemblyName ANTHEA) per provare "Calcola armature" in una finestra; non compilava più (WpfMath e risorse Wiki mancanti). | Codice invariato in `supporto/test/Desktop/ConcreteDesignDesktopChecks.cs`, comando `--check-concrete-design <cartella>` dell'exe UiTests; nel runner `ui/check-concrete-design`, preparato da ConcreteDesign.Checks. In caso d'errore scrive `errore.txt` (prima `ui-error.txt`). Il vecchio progetto è in `supporto/SUPERATI/test/ConcreteDesign.DesktopChecks`. |
| ValidationIllustrations | Stesso schema, per le schermate della validazione illustrata del 26/09; non compilava più. | Codice invariato in `supporto/test/Desktop/ValidationCaptureChecks.cs`, comando `--capture-validation <ingressi.json> <cartella>`; fuori dal runner (ingressi in `supporto/artefatti`). Il vecchio progetto è in `supporto/SUPERATI/test/ValidationIllustrations`. |

Nessun progetto del repository ricompila più l'intero X.Desktop. Resta il collegamento di un solo sorgente
(`ShortReportExport.cs`) in ConcreteShort.Checks, da togliere con F3.4.

## Progetti in `supporto/tmp` (non versionati, solo elencati)

`ca_extensions/Inspect`, `contour_audit/Audit`, otto copie della vecchia applicazione autonoma Materiali
(`materiali_atecap`, `materiali_classe_minima`, `materiali_cmin`, `materiali_composizione`, `materiali_esempi`,
`materiali_esposizione`, `materiali_schema`, `materiali_schermo`, tutte `src/Materiali.csproj`),
`muro_autonomo/Sorgenti/Muro` e `validazione_ca/Harness/Harness`. Non sono nel runner né nell'indice (decisione D8).
