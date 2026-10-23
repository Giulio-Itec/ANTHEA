# F5.14, chiusura: X.Desktop → src/ANTHEA.Desktop e prove in tests/ANTHEA.Desktop.UiTests

Ramo `refactoring/f5-14` da `refactoring/f5` 0e201b2b, worktree `aw-f5-14`. Voce F5.14 di [piano.md](piano.md); proposta
[f5-proposta-architettura.md](f5-proposta-architettura.md) §2.1 e §7.3; destino delle prove in [progetti-di-test.md](progetti-di-test.md).

**Perimetro deciso dall'utente** (24/10, ~00:30): «Anche le prove in tests/». Due spostamenti: il progetto `X.Desktop` va in
`src/ANTHEA.Desktop`, le prove WPF fuori schermo vanno in `tests/ANTHEA.Desktop.UiTests`. Restano invariati i namespace
(`X.Desktop` e gli altri) e il nome dell'eseguibile `ANTHEA.exe`. La terza revisione di `AGENTS.md` fa parte del passo.

**Stato.** Fase 1 fatta: inventario, progetto e preparazioni senza spostare file (sezioni 1–7). La fase 2, cioè gli spostamenti e
le corse finali, parte quando il ritiro di `MainWindow` (agente f5-13-g3, [f5-13-shell.md](f5-13-shell.md) §14.8) è unito su
`refactoring/f5`: quel ritiro cambia proprio i file da spostare.

## 1. Inventario dei riferimenti al percorso `X.Desktop`

Rilevato su 0e201b2b con `git grep -E "X\.Desktop[/\\]|X\.Desktop\.csproj|[\"']X\.Desktop[\"']"`, escluso `supporto/SUPERATI`.
Il progetto ha 321 file versionati. Per ogni gruppo: che cosa fa il riferimento e che cosa succede nella fase 2.

### 1.1 Compilazione, soluzione, runner

| File | Riferimento | Fase 2 |
| --- | --- | --- |
| `X.Desktop/X.Desktop.csproj` | percorsi relativi `../src/…`, `../tests/…`, `../supporto/…` | diventa `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj`: un livello in più (`../…` → `../../…`, `../src/X` → `../X`). **`RootNamespace` esplicito `X.Desktop`**: oggi è implicito e viene dal nome del file di progetto; senza, cambierebbe in `ANTHEA.Desktop`. `AssemblyName` resta `ANTHEA` |
| `ANTHEA.sln` | `"ANTHEA.Desktop", "X.Desktop\X.Desktop.csproj"` (GUID F1AC66D5) | solo il percorso; nome e GUID uguali |
| `Compila.cmd`, `Avvia ANTHEA.cmd` | `dotnet build/publish "X.Desktop\X.Desktop.csproj"` | percorso |
| `Directory.Build.props` | commento della configurazione UiTests | percorso nel commento |
| `build/ci.ps1` | `$Desktop` (riga unica dopo la preparazione, sezione 6); commenti delle suite che citano `tests/Desktop/*.cs` | `$Desktop = 'src\ANTHEA.Desktop\ANTHEA.Desktop.csproj'`; commenti. Il log di build diventa `build/ANTHEA.Desktop.UiTests` e `build/ANTHEA.Desktop` (oggi `X.Desktop.*`); `build/known-failures.json` non ha voci del desktop |

### 1.2 Progetti che referenziano il desktop o leggono `App.xaml` dal disco

| Progetto | Riferimento | Suite (stadio) |
| --- | --- | --- |
| `tests/ConcreteInput.Checks` | `ProjectReference ../../X.Desktop/X.Desktop.csproj`; `WpfTestResources.Load` | `ConcreteInput.Checks` (fast), `ConcreteInput.UiChecks` (ui) |
| `tests/ConcreteTimeEffects.Checks` | idem | `ConcreteTimeEffects.Checks` (fast), `ConcreteTimeEffects.UiChecks` (ui) |
| `tests/FoundationWorkspace.Checks` | `ProjectReference` | `FoundationWorkspace.Checks` (fast), `.UiChecks` (ui) |
| `tests/ModelViewer.UiChecks` | `ProjectReference`; **`EmbeddedResource ../../X.Desktop/App.xaml`** | cinque suite ui (`ModelViewer.*`, `Stratigraphy.UiChecks`) |
| `tests/ANTHEA.Desktop.Testing/WpfTestResources.cs` | **`source = "X.Desktop/App.xaml"`** letto dalla cartella corrente | (sorgente collegato dai due progetti sopra) |
| `supporto/test/ElasticPile.UiChecks` | `ProjectReference ../../../X.Desktop/…`; **`TestApp.LoadStyles` legge `X.Desktop/App.xaml`** | tre suite ui |
| `supporto/test/HorizontalPileGroup.Checks` | `ProjectReference` | `HorizontalPileGroup.Checks` (ui) |
| `supporto/test/ConcreteShort.UiChecks` | `ProjectReference … SetConfiguration=UiTests`; chiama metodi di `MainWindow` | `ConcreteShort.UiChecks` (ui): la riscrive o l'archivia il ritiro di `MainWindow` |

Nella fase 2 cambia solo il percorso. Il caricamento delle risorse da `Themes/*.xaml` invece che da `App.xaml` letto dal disco
(proposta §7.3) appartiene alla forma separata delle prove (sezione 4.3), non a questo passo.

### 1.3 Prove che cercano sorgenti o binari per percorso

Qui lo spostamento può **indebolire una prova in silenzio** (cartella che non esiste più e viene saltata) o farla fallire.

| File | Riferimento | Effetto se non si cambia |
| --- | --- | --- |
| `tests/ANTHEA.Architecture.Tests/Program.cs` | `X.Desktop/X.Desktop.csproj`; `X.Desktop/bin/Release/net8.0-windows` (3 volte); `X.Desktop/Wpf/ProjectReport.cs`; `X.Desktop/Wpf` (renderer); `X.Desktop/Views/CompositeSection`, `X.Desktop/Views/RetainingWall`; `new[] { "X.Desktop", "X.Materiali" }.Where(Directory.Exists)` per `async void` e `DispatcherTimer` | errore, salvo l'ultimo: **conteggi a zero in silenzio**. Le sorgenti F4 (`X.*` più `src/*`) restano coperte perché `src` comprende il progetto spostato |
| `tests/ANTHEA.Architecture.Tests/F5PresentationRules.cs` | `NewViewFolders = ["X.Desktop/Views/", "X.Desktop/Shell/"]` filtrate con `Directory.Exists`; `X.Desktop/bin/Release/…`; percorsi delle sonde delle autoverifiche | **P6/P7 sulle viste nuove saltate in silenzio** («cartelle non ancora presenti») |
| `tests/ANTHEA.Architecture.Tests/F4DomainRules.cs` | sonda `X.Desktop/Wpf/Probe.cs` (R7 negativa) | nessuno (resta negativa); si allinea |
| `tests/ConcreteLibraryAdapter.Checks/` `Durability.cs`, `Resistance.cs`, `ServiceabilityChecks.cs`, `ThinCastingGrid.cs` | elenchi di cartelle con `"X.Desktop"` accanto a `"src"` | `DirectoryNotFoundException`; si toglie `"X.Desktop"`: `"src"` copre il progetto spostato, conteggi uguali |
| `supporto/test/Desktop/ContrastAudit.cs` | radice trovata con `X.Desktop/X.Desktop.csproj`; `SourceIndex` su `{"X.Desktop","X.Materiali"}.Where(Directory.Exists)` | radice non trovata; indice delle sorgenti vuoto in silenzio. Il file lo sta cambiando f5-13-g3 (tappa A): si corregge dopo l'unione |
| `supporto/test/wiki-handbook-checks.py` | `(ROOT/'X.Desktop/Wpf').glob('Wiki*.cs')` (2 volte, 5 file oggi) | **controllo dei testi saltato in silenzio** |
| `tools/qa/f5_censimento_temi.py` | radice `X.Desktop`, catalogo `X.Desktop/Themes/ThemeTokens.cs`, esclusioni | strumento fuori dal runner: si allinea |

Regola per la fase 2: in ogni prova la cartella del desktop deve esistere (controllo esplicito dove oggi c'è `Directory.Exists`),
così una cartella sbagliata fallisce invece di saltare.

### 1.4 Baseline di architettura con chiavi per percorso

| File | Voci con `X.Desktop/` | Confronto |
| --- | ---: | --- |
| `tests/ANTHEA.Architecture.Tests/f4-domain-baseline.json` | 1 099 righe | per chiave (file, regola, proprietario, impronta) |
| `tests/ANTHEA.Architecture.Tests/f5-static-baseline.json` | 9 | per chiave |
| `tests/fixtures/f3/architecture.json` | 19 (`AsyncVoid`, `DispatcherTimer`) | per somma |

Nella fase 2 cambiano solo le chiavi, `X.Desktop/` → `src/ANTHEA.Desktop/`, con conteggi e impronte uguali: l'impronta è lo
SHA-256 di proprietario e testo normalizzato, senza il percorso (`F4DomainRules.cs`, riga 78). Si verifica con
`Architecture.F4.Inventory` prima e dopo: stesso numero di file e di candidati. Il ritiro di
`MainWindow` toglie prima alcune voci (`SheetEditor.cs`, `ProjectReport.cs`, …): si parte dalle baseline dopo l'unione.

### 1.5 Rilascio e installer

`supporto/installer/Crea-Installer.ps1` (progetto, icona, logo, messaggio sulla versione), `tools/qa/Audit-Installer.py`
(`<Version>` letta dal csproj), `tools/qa/Assert-NoTestCode.ps1` (esempio nel commento), `tools/paratie/New-LocalPreview.ps1`,
`supporto/scripts/Test-CalculationUi.ps1`, `supporto/scripts/Generate-AppIcon.ps1`, `Render-AntheaAnthemion.ps1`,
`Render-AntheaIconV2.ps1`. `ANTHEA.nsi` e `tests/Installer/*` non citano il percorso (lavorano sulla cartella pubblicata).
Le prove dell'installer (`-Installer`: `installer/safety`, `installer/guides`, `installer/examples`) e `Audit-Installer.py`
vanno rieseguite nella fase 2.

### 1.6 Wiki: figure delle guide

`X.Desktop/Assets/Wiki` contiene 42 file: le figure delle due guide globali, incorporate nell'eseguibile (`Resource` per i PNG,
`EmbeddedResource Wiki.figures.*` per gli SVG). Le guide le citano con il percorso relativo `../../X.Desktop/Assets/Wiki/…`
(5 righe nella pratica, 13 nella teorica). Da quel percorso dipendono:
- `src/ANTHEA.Wiki/Content/assets.json` (chiavi = collegamenti delle guide) e `sources.props` (28 voci `WikiSource` con
  SHA-256 in `../../../X.Desktop/Assets/Wiki/…`, controllate a ogni build di `ANTHEA.Wiki`);
- `supporto/scripts/wiki/build-wiki-index.py` (suite `wiki/indice`) e `validate_wiki.py` (suite `wiki/manuale`), che
  risolvono i collegamenti delle guide sul disco e falliscono se la figura manca;
- gli script che scrivono le figure: `build-handbook-figures.py`, `build-wiki-sketches.py`, `render-technical-figures.py`,
  `technical-chapters.py`, `tools/docs/Draw-FoundationFigure.py`;
- **i riferimenti F3 congelati** `tests/fixtures/f3/expected/wiki/*.md` (8 file), che contengono il testo delle guide con
  quegli stessi collegamenti. Vedi la sezione 3.

I percorsi `supporto/SUPERATI/…/X.Desktop/Wiki/…` di `integrate-existing.py`, `publish-technical-library.py` e del test
`test_migration_coverage` puntano all'archivio e non cambiano. `supporto/docs/wiki-riscontri.json` (accanto alle guide) cita
`X.Desktop/Wpf/*.cs` come evidenze delle revisioni: è un registro storico e non si tocca.

### 1.7 Testi dentro riferimenti congelati e letterali che li riproducono

Non si cambiano: descrivono la provenienza al momento della cattura.
- `tests/fixtures/f5/token-colori.json`, campo `nota` («…tavolozza di X.Desktop/Wpf/Appearance.cs al commit di F5.3…»), e
  lo stesso letterale in `tests/Desktop/ThemeTokenChecks.cs` (riga 115), che deve restare identico.
- `tests/fixtures/f5-*/README.md` (10 file): il comando di cattura `dotnet X.Desktop/bin/UiTests/…`. Il comando di oggi sta
  nella sezione 5.
- `tests/fixtures/f5/contrasto-chiara.json`, campo `strumento` (`supporto/test/Desktop/ContrastAudit.cs --baseline`), e il
  letterale in `tests/Desktop/ContrastBaseline.cs`. La tappa A di f5-13-g3 può cambiare questa baseline: si segue la sua unione.
- Le etichette di provenienza di `tests/Desktop/FieldFormParityChecks.cs` («X.Desktop/Wpf/GeoEditor.cs BuildGeo», …).

### 1.8 Commenti nel codice e documentazione

- Commenti di produzione: `X.Desktop/App.xaml`, `Hosting/MvvmSheetEditor.cs` (anche il testo di un'eccezione, «(X.Desktop/Views)»),
  `Themes/ThemeTokens.cs`, `Views/Common/FieldForm.xaml`, cinque file di `src/ANTHEA.Presentation`, `src/ANTHEA.Application/
  Presentation/SoilTransferPresentation.cs`. Si allineano in un commit a parte, dopo lo spostamento.
- Documentazione viva, da allineare nella fase 2: `README.md` (righe 79–82, 179–187), `AGENTS.md` (sezione 7),
  `docs/refactoring/progetti-di-test.md`, `docs/sviluppo/fondazioni-superficiali.md`, `docs/sviluppo/modelli-visualizzatore.md`,
  `docs/stratigrafia.md`, `docs/paratie/interfaccia-grafica.md`, `tests/ANTHEA.Testing/README.md`, `tests/ANTHEA.Legacy/README.md`,
  `supporto/scripts/validazione_illustrata_2026_09_26/README.md`.
- **Con PDF accanto, non toccati da questo agente**: `supporto/README.md` (+ `README.pdf`), `supporto/installer/README.md`
  (+ `README.pdf`). Le guide e i loro PDF. Restano al coordinatore, che li aggiorna con i PDF.
- Documentazione storica, invariata: le note dei passi (`f2-*`…`f5-13-*`), `registro-differenze.json`, `decisioni.md`,
  `piano.md` (salvo la voce F5.14), `installer-*.md`, `diario.md`, `scostamenti.md`. Descrivono lo stato del loro momento.

`.gitattributes` non cita il percorso.

## 2. Le prove WPF di oggi (0e201b2b)

| Sorgenti | File | Righe | Dove si compilano |
| --- | ---: | ---: | --- |
| `supporto/test/Desktop/*.cs` | 46 | 7 201 | `X.Desktop` in UiTests, `Link="Tests/…"` |
| `tests/Desktop/*.cs` | 61 | 22 743 | idem |
| `tests/ANTHEA.Desktop.Equivalence/*.cs` (cattura F3) | 10 | | idem, `Link="Tests/F3/…"` |
| `tests/Shared/*` (11 tracce, lettore della sezione c.a., sonde, confronto di persistenza, Excel storico) e `supporto/test/Shared/DurabilityReferenceChecks.cs` | 18 | | idem, condivisi con `ANTHEA.Presentation.Tests` e altri |

Nessun nome di file in comune fra `supporto/test/Desktop` e `tests/Desktop`.

**Configurazione UiTests.** `Directory.Build.props` (ottimizzata come la Release, simbolo `UITESTS`, uscita in `bin/UiTests`);
`X.Desktop.csproj` (in UiTests le GPC sono riferimenti di compilazione, `ANTHEA.Testing` e `ANTHEA.Legacy` referenziati, i
sorgenti sopra compilati). Nessun `#if UITESTS` nel codice di produzione. `qa/no-test-code` controlla la Release.

**`AppHarness`** (`supporto/test/Desktop/AppHarness.cs`, 654 righe): `partial class App` che implementa `partial void
RunTestHarness` di `App.cs` e smista la riga di comando (`--smoke-*`, `--check-*`, `--capture-validation`,
`--interactive-shell`, `--module-impl`).

**Tipi di produzione estesi da sorgenti di prova** (partial nello stesso assembly, con accesso ai membri privati):
`App` (AppHarness), `WikiView`, `RetainingWallDrawing`, `DomainViewport3D`, `BridgeOptimizationPlot` (i quattro `*.Tests.cs`
della decisione D3), `ModuleImplementations` (`tests/Desktop/ModuleHostBench.cs`) e `MainWindow` (19 partial, che escono con
il ritiro). `ShellSmokeWindow` (24 partial) e le altre classi partial sono tipi delle prove.

**Progetti esterni con riflessione o WPF** (§7.3): sezione 1.2.

**Suite dello stadio `ui`**: 77 a 0e201b2b (23 `smoke`, 39 `check`, 14 `run`, 1 `python`), contate con
`build\ci.ps1 -Profile full -List`. Il ritiro di `MainWindow` ne toglie almeno sei (le storiche di `$smokes` sostituite da
`ui/shell-smoke-*`) e decide di `ConcreteShort.UiChecks`: il conteggio di riferimento della fase 2 è quello di
`refactoring/f5` dopo l'unione, prima del primo spostamento.

## 3. Fermo: le figure della Wiki stanno in un riferimento congelato

Le istruzioni dicono: se un riferimento congelato contiene il percorso `X.Desktop`, fermarsi e spiegarlo. È il caso delle figure
della Wiki (1.6). Se `X.Desktop/Assets/Wiki` si sposta:
- le guide puntano a file che non esistono più: `wiki/indice` e `wiki/manuale` falliscono, e così la build di `ANTHEA.Wiki`
  (hash di `sources.props`);
- per rimediare bisogna cambiare 18 collegamenti nelle due guide, rifare indice e `sources.props` e, con le guide, i PDF;
- il testo delle guide finisce nei riferimenti congelati `tests/fixtures/f3/expected/wiki/*.md`: i collegamenti cambiati
  diventano differenze di `ui/f3-equivalence`, da trattare con la regola editoriale F3-06 (file sostituiti nel riferimento,
  voce del registro), come per ogni revisione delle guide.

Guide, PDF e riferimenti congelati sono fuori dal mio perimetro. **Opzioni:**
- **A (raccomandata per F5.14):** `X.Desktop/Assets/Wiki` resta dov'è; `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` la incorpora
  con `Link="Assets/Wiki/…"`, così gli URI `pack://application:,,,/ANTHEA;component/Assets/Wiki/…` e i nomi `Wiki.figures.*`
  restano uguali. Nessun cambiamento a guide, PDF, Wiki e riferimenti congelati. Difetto: resta una cartella `X.Desktop/` con
  le sole figure della Wiki, con un `README.md` che spiega perché.
- **B (definitiva, del coordinatore):** le figure seguono il progetto (o vanno in `src/ANTHEA.Wiki`) in una revisione delle
  guide (Rev59 o successiva): collegamenti, indice, PDF e transizioni editoriali della Wiki insieme.

In mancanza di altra indicazione, la fase 2 applica A (la più conservativa) e lascia B come voce aperta.

## 4. Forma di `tests/ANTHEA.Desktop.UiTests`

### 4.1 Scelta: le stesse sorgenti spostate, compilate ancora nella build UiTests

`tests/ANTHEA.Desktop.UiTests/` riceve i sorgenti di `supporto/test/Desktop` e `tests/Desktop` rimasti dopo il ritiro di
`MainWindow`, più un `README.md`. Non ha un progetto proprio: `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj` li compila nella
configurazione UiTests, come oggi. Eseguibile, flag, suite e runner restano gli stessi; cambiano solo i percorsi.

**Motivo.** La forma della proposta (eseguibile separato che vede il desktop con `InternalsVisibleTo`, risorse da
`Themes/*.xaml`) è preferibile, ma non ci sta entro le 9 con tutte le suite verdi:
1. sei tipi di produzione, dopo il ritiro di `MainWindow`, sono estesi dalle prove con partial e membri privati (sezione 2). Da un altro assembly servono
   membri `internal` al posto di quelli privati, o la riflessione: è un cambiamento della produzione, tipo per tipo;
2. l'ingresso delle prove è `partial void RunTestHarness` dentro `App`. Un eseguibile separato ha bisogno di una sua
   `Application`, che carichi le risorse senza eseguire `App.OnStartup`, e di un nuovo smistamento della riga di comando
   (654 righe): la stessa difficoltà del punto 3 di F1.5 ([progetti-di-test.md](progetti-di-test.md));
3. sono circa 30 000 righe di prove e 77 suite `ui`, compresa `ui/f3-equivalence` (fino a 30 minuti): ogni cambiamento della
   forma va provato con tutte, di notte, dopo un ritiro di `MainWindow` che arriva a un'ora che non conosco;
4. la forma scelta cambia solo percorsi: il conteggio delle suite resta uguale per costruzione, e il ramo resta verde anche se
   la notte finisce a metà.

Restano dunque la configurazione UiTests e i partial di prova; spariscono `supporto/test/Desktop` e `tests/Desktop`.

### 4.2 Disposizione

```
tests/ANTHEA.Desktop.UiTests/
  README.md          cosa contiene, come si compila e si esegue, perché non è (ancora) un progetto separato
  *.cs               da tests/Desktop e supporto/test/Desktop (nessun nome in comune)
  Equivalence/*.cs   da tests/ANTHEA.Desktop.Equivalence (facoltativo, ultimo commit)
```

Nel csproj: `<Compile Include="../../tests/ANTHEA.Desktop.UiTests/*.cs" Link="Tests/%(Filename)%(Extension)" />` (e
`Equivalence/*.cs` con `Link="Tests/F3/…"`), senza `**` per non raccogliere un domani `obj/`. `tests/Shared` e
`supporto/test/Shared` restano dove sono: li usano anche altri progetti.

### 4.3 Che cosa resta per la forma della proposta (dopo F5.14)

1. Rendere `internal` i membri usati dai partial di `WikiView`, `RetainingWallDrawing`, `DomainViewport3D`,
   `BridgeOptimizationPlot`, `ModuleImplementations`, e spostare i partial in classi di prova;
2. un progetto `tests/ANTHEA.Desktop.UiTests/ANTHEA.Desktop.UiTests.csproj` (net8.0-windows, WinExe) con una sua
   `Application`, le risorse da `/ANTHEA;component/Themes/*.xaml` e dai dizionari dei modelli di dati (le stesse fusioni di
   `App.xaml`), `InternalsVisibleTo("ANTHEA.Desktop.UiTests")` nel desktop;
3. `AppHarness` come `Main` di quel progetto; `RunTestHarness` tolto da `App`;
4. runner: `$Exe` verso il nuovo eseguibile; la configurazione UiTests e il simbolo `UITESTS` tolti; `qa/no-test-code` invariato;
5. poi, modulo per modulo, le parti di interfaccia dei progetti esterni della sezione 1.2 e `WpfTestResources` dentro il nuovo
   progetto (risorse da `Themes/*.xaml`, non più `App.xaml` letto dal disco);
6. tutte le suite `ui` verdi con lo stesso numero, prima e dopo ogni passo.

Stima: L (2–4 sessioni). Da proporre come voce dopo F5.15.

## 5. Piano della fase 2

Comandi dalla radice del worktree; `<base>` è l'unione di `refactoring/f5` dopo il ritiro di `MainWindow`.

0. `git merge refactoring/f5` (unione permessa). Profilo quick verde. Conteggio di riferimento:
   `powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Profile full -List"` e
   `-Stage baseline -Only '^Architecture.F4.Inventory$'` per il numero dei candidati F4.
   Ripetere l'inventario della sezione 1 (il ritiro può aver tolto o aggiunto riferimenti).
1. **Commit «spostamento del desktop»** (solo percorsi e riferimenti):
   - `git mv X.Desktop src/ANTHEA.Desktop`, poi `git mv src/ANTHEA.Desktop/X.Desktop.csproj src/ANTHEA.Desktop/ANTHEA.Desktop.csproj`;
     con l'opzione A, `Assets/Wiki` torna in `X.Desktop/Assets/Wiki` (`git mv`) e il csproj la incorpora con `Link`;
   - csproj: percorsi relativi e `<RootNamespace>X.Desktop</RootNamespace>`;
   - sezioni 1.1, 1.2, 1.3 (senza `ContrastAudit` se è già cambiato, altrimenti anche lui), 1.4 (chiavi), 1.5;
   - controlli espliciti di esistenza della cartella del desktop dove oggi c'è `Directory.Exists` (1.3);
   - verifica: `git grep -nE "X\.Desktop[/\\]|X\.Desktop\.csproj|[\"']X\.Desktop[\"']" -- . ':!supporto/SUPERATI' ':!docs/refactoring'`
     deve restituire solo le voci di 1.6 (opzione A), 1.7 e i commenti di 1.8;
   - messaggio: baseline con le sole chiavi cambiate, conteggi uguali.
   - Prova: quick, `Architecture.F4.Inventory` con lo stesso numero di candidati, `-Stage build,ui -Only '^ui/(smoke|check-theme-tokens|check-wiki-offscreen)$'`.
2. **Commit «prove in tests/ANTHEA.Desktop.UiTests»**: `git mv` dei file di `tests/Desktop` e `supporto/test/Desktop`,
   csproj, `README.md` della cartella, commenti del runner, `Assert-NoTestCode.ps1`. Facoltativo, commit a parte:
   `tests/ANTHEA.Desktop.Equivalence` → `tests/ANTHEA.Desktop.UiTests/Equivalence`.
3. **Commit «commenti e documentazione viva»**: sezione 1.8, `docs/refactoring/progetti-di-test.md` (tabella e sezione
   UiTests), terza revisione di `AGENTS.md` (sezione 7).
4. **Corse finali**: profilo standard, tag `f5-14-fine`; `-Stage build,ui` con tutte le suite `ui/*` (anche
   `ui/f3-equivalence`), tag `f5-14-ui`; `-Installer` per le prove dell'installer. Il numero delle suite `ui` uguale a quello
   del punto 0.
5. Voce F5.14 di `piano.md` aggiornata, senza spunta; questa nota aggiornata con fatto, resto e ripresa.

Se la notte finisce prima: il ramo resta verde dopo ogni commit; quel che manca resta scritto qui.

## 6. Preparazioni fatte (fase 1)

- `build/ci.ps1`:
  - il percorso del progetto desktop compare una sola volta (`$Desktop`); `$DesktopDir` ne deriva l'eseguibile UiTests e la
    cartella Release di `qa/no-test-code`;
  - nuovo interruttore `-List`: elenca le suite degli stadi scelti (stadio, tipo, nome) con il totale per stadio, senza
    compilare, eseguire o creare la cartella di uscita. Serve al conteggio prima e dopo. A 0e201b2b: fast 33, regression 15,
    wiki 2, ui 77 (`-Profile full`); baseline 16.
- Nessuna preparazione nelle prove di architettura e nei progetti di prova: sono gli stessi file che il ritiro di `MainWindow`
  cambia (baseline, `Program.cs`), e un cambiamento ora darebbe solo conflitti all'unione.

## 7. Bozza della terza revisione di `AGENTS.md`

Si applica nella fase 2, quando i percorsi esistono. Cambiano le sezioni «Architettura e refactoring» (una riga), «Struttura
del repository» e «Verifiche»; le altre restano.

> **Architettura e refactoring**, primo punto: «… Non aggiungere calcoli nuovi a `src/ANTHEA.Desktop`, `src/ANTHEA.Presentation`,
> X.Core o X.Calculations.»
>
> # Struttura del repository
>
> - Il codice di produzione sta in `src/`: l'interfaccia WPF in `src/ANTHEA.Desktop` (eseguibile `ANTHEA.exe`, namespace
>   `X.Desktop`), i ViewModel senza WPF in `src/ANTHEA.Presentation`, gli altri progetti `ANTHEA.*`. Restano fuori da `src/`
>   soltanto i progetti storici `X.Core` e `X.Calculations` e, con l'opzione A della sezione 3, la cartella
>   `X.Desktop/Assets/Wiki` delle figure delle guide. I test in `tests/`, script e strumenti in `tools/`, runner e
>   configurazione di build in `build/`, la documentazione di sviluppo in `docs/`.
> - Le prove WPF fuori schermo stanno in `tests/ANTHEA.Desktop.UiTests` e si compilano soltanto nella configurazione `UiTests`
>   di `src/ANTHEA.Desktop` (`dotnet build src/ANTHEA.Desktop/ANTHEA.Desktop.csproj -c UiTests`); la Release non contiene
>   codice di prova (`qa/no-test-code`).
> - I progetti di `supporto/test` ancora attivi restano dove sono finché non passano nei test delle librerie o in `tests/`
>   ([progetti-di-test.md](docs/refactoring/progetti-di-test.md)); non se ne aggiungono di nuovi.
> - (invariati) `supporto/artefatti/`, divieto di `verifiche_*` e `tmp`, risorse grafiche nei progetti che le usano.
>
> # Verifiche
>
> - Il runner unico è `build/ci.ps1`: profilo `quick` (build e suite rapide), `standard` (tutto tranne le prove WPF, come
>   `supporto/Verifica.cmd`), `full` (anche le prove WPF fuori schermo). Le suite WPF si eseguono con
>   `-Stage build,ui` (con `-Only` per sceglierle); `-List` le elenca e le conta senza eseguirle.
> - Nessuna suite dello stadio `ui` si toglie senza la sostituta: il numero delle suite si riporta prima e dopo.
> - (invariato) i fallimenti ammessi sono solo quelli di `build/known-failures.json`.
> - Salvare tutti i nuovi sorgenti di test in `tests/`.

Da decidere con il coordinatore: se aggiungere alla revisione le regole MVVM di F5 (Presentation senza WPF, viste con un
DataTemplate per ViewModel, niente `Messenger.Default`/`Ioc.Default`, radice di composizione scritta a mano), oggi nelle
decisioni e nelle prove di architettura ma non in `AGENTS.md`.

## 8. Domande aperte

1. Figure della Wiki (sezione 3): A per F5.14 e B con una revisione delle guide? Senza risposta, la fase 2 applica A.
2. `supporto/README.md` e `supporto/installer/README.md` hanno un PDF accanto: li aggiorna il coordinatore con i PDF?
3. Regole MVVM in `AGENTS.md` (sezione 7)?
4. La forma della proposta per le prove (4.3) come voce dopo F5.15?

## 9. Come riprendere

```
cd C:\Users\g.pacini\AppData\Local\Temp\aw-f5-14
git log --oneline 0e201b2b..HEAD
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Profile full -List"
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Profile quick -Tag f5-14-quick"
```

Avanzamento dell'agente: `ANTHEA-f5-ripresa\f5-agenti\f5-14\avanzamento.md`.
