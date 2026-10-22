# F5.12 — Bridge Design e prezzari: preparazione

## Scopo

Questa nota chiude la **preparazione** di F5.12: modulo `str_bridge_design`, vista storica `BridgeDesignWorkspace`
(`X.Desktop/Wpf/BridgeDesignWorkspace.cs` e le parziali `BridgeDesignOptimization.cs`, `BridgeDesignOptimizationPanel.cs`,
`BridgeDesignTechnical.cs`) con la finestra dei prezzari `BridgePricesView`. I disegni (`BridgeDesignDrawing.cs`,
`BridgeDesignAdvancedDrawing.cs`, `BridgeOptimizationPlot.cs`, con `BridgeFamilyIcon` e `BridgeConceptMomentPlot`) restano WPF (Q3).
I tipi senza WPF del modulo stanno in `src/ANTHEA.Application/BridgeDesign` (`BridgeDesignModel`, `PriceCatalogue`) e
`src/ANTHEA.Application/Modules/BridgeDesignAnalysis.cs`; il report in `src/ANTHEA.Reports/Generators/BridgeConceptExport.cs` (congelato).
La nota segue i passi 1–5 della «Ricetta per i moduli successivi» di [f5-4-pilota.md](f5-4-pilota.md). Contiene:
- che cosa esiste dopo la preparazione;
- i comportamenti della vista storica trovati con le tracce;
- che cosa resta per finire F5.12;
- le domande per il punto di controllo dell'utente.

La vista XAML, il passaggio in `ModuleImplementations` e l'archiviazione della vista storica vengono **dopo** il punto di
controllo: non fanno parte di questa preparazione. La Release resta con la vista storica.

Riferimenti:
- [proposta](f5-proposta-architettura.md), sezioni 3, 5, 7, riga F5.12 della sezione 8 e domanda Q17;
- [inventario](f5-inventario-ui.md), parti Bridge Design e prezzari;
- [muri](f5-9-muri.md) e [sezione composta](f5-11-sezione-mista.md), i moduli più vicini (calcolo sullo sfondo, motore «occupato»
  fino al ritorno);
- `tests/fixtures/f3/README.md` (sezione «Fixture aggiunte in F5.12») e `tests/fixtures/f5-bridge-design/README.md`.

Ramo `refactoring/f5-12-prep`, da `refactoring/f5` `d1f0c4d6` (set ufficiale AE delle librerie), 21/10/2026. Il calcolo di Bridge
Design (ponte, alternativa A e alternative a ridotta impronta, `BridgeDesignAnalysis`) resta **sullo sfondo**, con il ricalcolo
automatico 450 ms dopo l'ultima modifica e subito all'apertura e con «Calcola», come oggi (la scelta (b) di Q8).

## 1. Che cosa esiste dopo la preparazione

| Commit | Passo | Contenuto |
| --- | --- | --- |
| `7259150a` | 1 | otto fixture F3 di Bridge Design catturate con la vista storica |
| `b863652c` | 2 | tracce congelate della vista storica, `ui/check-bridge-design-historic` |
| `f398251c` | 3 | derivazioni in Application, editor dei prezzari in Presentation |
| `f29c1e23` | 4 | ViewModel e modulo MVVM |
| `26541726` | 5 | prove senza WPF del ViewModel |
| questa nota | 6 | — |

### 1.1 Fixture F3

Le 8 fixture nuove (tabella completa in `tests/fixtures/f3/README.md`) coprono le tipologie con campi propri (cassone in acciaio,
strallato e sospeso con la piastra ortotropa, arco con catena, reticolare), i tre ostacoli, i quattro terreni, ogni tipo di pila e di
fondazione, le sottostrutture imposte, il traffico uniforme dell'archivio, l'inizio e la fine su pila, un documento prezzi scritto
dall'editor dei prezzari (catalogo proprio, analisi composta, prezzo personalizzato motivato, inclusione, cronologia), l'alternativa A
fissata e non calcolabile, i fattori ambientali cambiati dopo un insieme salvato, le opzioni a ridotta impronta, i valori con la
virgola e un calcolo fermato da dati da completare.

Gli input vengono dal generatore `F3Fixtures.BridgeDesignF512`; gli attesi (70 file) da due catture uguali byte per byte. I documenti
dei prezzi e dei fattori ambientali hanno una data fissa (15/01/2026): il normalizzatore delle date del giorno sostituisce le date
vicine a quella della corsa, e una data del giorno nell'input avrebbe reso il riferimento instabile. Gli attesi delle 111 fixture
esistenti e `transitions-f4.json` non cambiano; in `expected/esito.txt` cambia solo il numero delle fixture, 111 → 119, registrato nel
manifest (`fixture_aggiunte_f5_12`). `ui/f3-equivalence`, `ui/f3-report-chapters` (120 capitoli), `ui/f3-scheduling` e `ui/f3-figures`
passano.

### 1.2 Tracce della vista storica

Le tracce (`tests/fixtures/f5-bridge-design/storica`, formato e scenari in `tests/fixtures/f5-bridge-design/README.md`) registrano lo
stato visto dall'utente: per intero dopo l'apertura, poi dopo ogni passo solo le voci cambiate. Le voci sono i dati nell'ordine in
memoria, `Modified`, il calcolo, lo stato, le metriche, la configurazione, pagina e schede, gli stati di «Annulla», il disegno come
dati, le schede degli ingressi e i pannelli dei risultati come blocchi (testi con il ruolo, parametri con bordo e cursore, scelte,
opzioni, tipologie, tabelle, pulsanti, gruppi, diagrammi), le alternative e la ricerca dell'ottimo (scelte, stato, graduatoria,
variazioni, anteprima, grafici come dati).

Gli scenari sono 19: il percorso F3 per ognuna delle 10 fixture, `ingressi-sito-campate`, `sezione-tipologie`, `pile-traffico`,
`risultati-confronto`, `prezzi-ipotesi`, `prezzario-documento`, `ottimizzazione`, `sola-lettura`, `calcolo-sullo-sfondo` (motore
trattenuto da `HeldModuleService`). Prima di ogni passo l'utente apre la pagina e le schede del controllo; ogni passo si fa con i
controlli, come l'utente. La finestra dell'editor dei prezzari è modale: lo script del passo gira dal dispatcher sul ViewModel a cui la
finestra è legata (`BridgeDesignPriceScripts`). La vista storica non è modificata: la traccia usa membri interni in una classe parziale
compilata solo in UiTests (`tests/Desktop/BridgeDesignHistoricTrace.cs`). Come in F5.11, le continuazioni della vista (ritardo,
ritorno del motore, pubblicazione, ricerca e suo avanzamento) girano alla priorità di inattività del dispatcher, impostata solo intorno
al passo: avanzano solo mentre la traccia aspetta.

La stessa definizione (`tests/Shared/BridgeDesignTrace.cs`) guida due prove:
- la vista storica, `ui/check-bridge-design-historic` (stadio `ui`), finché la vista esiste; registra anche gli elenchi dei disegni WPF
  (`disegni/elenco.txt`: prospetto, sezione, diagramma dei momenti, icone, grafici della ricerca) e i pixel delle figure del report;
- il ViewModel senza WPF (sezione 1.5).

### 1.3 Derivazioni in Application ed editor dei prezzari

Passo 3 (`f398251c`). La vista storica chiama le funzioni spostate e dà un'uscita identica (tracce e disegni congelati prima dello
spostamento, F3, capitoli autonomi, smoke di Bridge Design, prezzari e traffico):
- `src/ANTHEA.Application/Presentation/BridgeDesignPresentation.cs`: testi della scheda e dei pannelli, le quattro metriche e la
  configurazione, le righe delle tabelle (quantità e totale, dettagli e appoggi, confronto A / B, sezioni e quote, campate,
  sottostrutture), i campi della sezione per tipologia (`SectionFields`), le regole dei pulsanti (`SelectFamily`, `AutoSize`, `NewScene`,
  `Randomize` con le stesse estrazioni nello stesso ordine, `Pinned`, `Restore`, `ApplyAlternative`, `RestoreEnvironment`,
  `ApplyIndicativeRates`), i parametri (testo, valore del cursore, validità, testo scritto dal cursore) e i testi della pagina stampata;
- `BridgeDesignOptimizationPresentation.cs`: testi della ricerca, campate suggerite, opzioni lette dai limiti, avanzamento, migliore,
  graduatoria, motivi di esclusione, selezione e variazioni, soluzione applicata;
- `src/ANTHEA.Application/BridgeDesign/BridgeDesignHistory.cs`: «Annulla» (80 stati, poi i 60 più recenti);
- **prezzari (Q17 a).** `PriceCatalogueViewModel` passa da `ANTHEA.Application` a `src/ANTHEA.Presentation/Modules/BridgeDesign` con le sue
  righe, selezioni e comandi. Non poteva passare intero: usava i tipi del calcolo (`BridgePricing`, lo snapshot, le analisi di
  `ANTHEA.Calculations`), che Presentation non può riferire (regola P2). Catalogo, anteprima, documenti e rifiuti passano senza modifiche
  in `PriceCatalogueEditing` (Application), dietro bozze delle righe (`PriceEntryDraft`, `PriceAnalysisDraft`, `WorkPriceDraft`). L'anteprima
  restituisce gli aggiornamenti nell'ordine dell'editor storico: prezzi delle analisi azzerati e poi risolti, lavorazioni elaborate fino al
  primo rifiuto (le successive conservano quello che mostravano), stima e proposta quando raggiunte. Il ViewModel ha i membri del servizio
  dei dialoghi (`Title`, `CloseRequested`, `CancelCommand`); la vista storica `BridgePricesView` cambia solo lo using. I suoi controlli
  (`BridgePricesChecks`, `BridgePriceAnalysesChecks`) passano da Application.Tests a Presentation.Tests; il generatore delle fixture F3
  scrive il documento prezzi con `PriceCatalogueEditing` (gli 8 input rigenerati sono uguali byte per byte). `ANTHEA.Application` non
  usa più CommunityToolkit.Mvvm e il pacchetto è tolto dal suo progetto.

Baseline F4 3221 → 3213: 8 voci R2/R6 dei fattori di unità delle metriche (1e6 ×3, 1000) tolte (costanti con nome), 12 voci spostate
da `BridgeDesignWorkspace` a `BridgeDesignPresentation` (7 R2 delle estrazioni del ponte casuale, 5 R4). P3 invariata.

### 1.4 ViewModel

`BridgeDesignSheetViewModel` (`src/ANTHEA.Presentation/Modules/BridgeDesign`, quattro file parziali più `BridgeDesignViews.cs`,
`BridgeDesignSheetModule.cs` e `PriceCatalogueViewModel.cs`) su `SheetViewModelBase`, senza WPF:

| Vista storica | ViewModel |
| --- | --- |
| costruttore: `PrepareBridgeDesign`, schede degli ingressi, prezzi, ipotesi, ricerca, calcolo subito (`QueueCalculation(0)`) | stessa sequenza, stesse scritture nello stesso ordine; il calcolo dell'apertura parte subito (`CalculateAsync`) |
| `Number` (testo, cursore, bordo rosso), `Choice`, `Toggle`, pulsanti delle tipologie | elementi `BridgeDesignParameter` (testo grezzo nei dati, `Invalid`, `SliderValue`), `BridgeDesignChoice`, `BridgeDesignOption`, `BridgeDesignFamilies`; schede in `Inputs`, pannelli `Prices` e `Assumptions`; le stesse regole di ricostruzione (`RefreshInputs`: il bordo e il cursore restano come erano) |
| `Changed` (cronologia, ricalcolo dopo 450 ms, `Modified`), `Mutate`, `Undo` | `UserChanged` con `BridgeDesignHistory`, fattori ambientali sincronizzati prima della copia, poi `Changed` della base; `Undo` |
| `CalculateScheduledAsync` (`Busy` dall'inizio del lavoro al ritorno del motore) | `SheetCalculation` della base, `SheetCalculationMode.Background` sul servizio del contesto: `BridgeDesignEngine` esegue `BridgeDesignAnalysis` (ponte con il servizio del contesto, alternativa A e alternative con quello del modulo, come oggi) e segnala il ritorno del motore; «occupato» fino al ritorno anche per una richiesta superata |
| metriche, configurazione, pannelli dei risultati, alternative | `Metrics`, `Configuration`, `Quantities`, `Details` (con `BridgeDesignMomentChart`), `Comparison`, `Technical`, `Advice` come elementi (`BridgeDesignText` con il ruolo, `BridgeDesignTable`, `BridgeDesignButton`, `BridgeDesignGroup`) |
| `BridgeDesignDrawing` (dati, risultato, prospetto o sezione) | `DrawingData`, `DrawingResult`, `SectionView`; il disegno resta WPF (Q3) |
| pagine e schede | `Page`, `InputTab`, `OutputTab` |
| pulsanti della scheda del disegno | `ElevationCommand`, `SectionCommand`, `NewSceneCommand`, `RandomCommand` (`Randomize(Random)`), `OptimizeCommand`, `PinCommand`, `UndoCommand`, `DimensionsCommand`, `ExportCsvCommand`, `ExportPngCommand` |
| «Gestisci prezzario…» (`BridgePricesView`, finestra modale) | `ManagePricesAsync`: `PriceCatalogueViewModel` sul servizio dei dialoghi, documento applicato nei dati (una modifica, schede ricostruite); rifiutato in sola lettura (sezione 4, BD4) |
| pagina «Ottimizzazione» | `Objective`, `SetLock`, `Locks`, `SetLimit`/`LeaveLimit`, `Limits`, `IsLimitEnabled`, `SuggestSpans`, `RunOptimizationAsync` (su un thread di fondo, con l'avanzamento e l'annullamento), `StopOptimization`, `RefreshRanking`, `SelectSolution`, `ApplySolution`, `SearchStatus`, `SearchBest`, `SearchSelection`, `Progress`, `SearchResults`, `SearchChanges`, `SearchButtons`, `OptimizationSource`, `SolutionData`/`SolutionResult`/`SolutionSection` (l'anteprima della soluzione: il nome `PreviewData` nasconderebbe `ISheetViewModel.PreviewData`), `Cloud`, `Trace`, `CloudRank`, `Variable`, `FamilyFilter`, `ParetoOnly` |
| esportazioni, «Stampa scheda», report | `ExportCsv`, `ExportPng` e `ExportTechnicalCsv` con `IFileDialogService`; `PrintLines` (la vista stampa); `Report` e `BuildReportDocument` con il generatore esistente e i disegni di `IBridgeDesignFigureRenderer` |
| `RevisionInspection.Protect` | `IsReadOnly`/`CanEdit`: parametri, scelte, opzioni, tipologie, comandi che cambiano i dati, obiettivo, blocchi, limiti, variabile, filtro e Pareto rifiutati; pagine, schede, viste del disegno e dell'anteprima, selezione delle soluzioni, «Interrompi», «Suggerisci campate», «Aggiorna elenco», esportazioni, stampa, report e calcolo ammessi |
| `Dispose` | `Dispose`: calcolo e ricerca annullati |

Modulo: `BridgeDesignSheetModule` (chiave di stato `scene`, un report senza breve), registrato in `DesktopComposition.CreateModules`
con `BridgeDesignFigureRenderer` (`BridgeDesignDrawing.Png`, 1200 × 480). `ModuleImplementations` non cambia: la Release apre la vista
storica; il banco `--module-impl str_bridge_design=mvvm` resta senza vista finché manca il DataTemplate. In
`tests/Desktop/ModuleHostWpfChecks.cs` il registro atteso include `str_bridge_design`; ogni modulo della directory ha ora un ViewModel,
quindi la prova del «modulo senza implementazione MVVM» usa un registro ridotto senza Bridge Design.

### 1.5 Prove

`Presentation.Tests` aggiunge `BridgeDesignSheetChecks` (134 controlli; `Presentation.Tests` passa da 3790 a 3924) e riceve i controlli dell'editor dei prezzari
(`BridgePricesChecks`, `BridgePriceAnalysesChecks`, da Application.Tests):
- 19 tracce uguali riga per riga a quelle della vista storica (15 143 righe), con l'editor dei prezzari sul servizio dei dialoghi e gli
  stessi script;
- 10 fixture uguali al riferimento F3: aperto, visitato, calcolato, risultato, testo e struttura del report, esportato, stato, riaperto.
  Sono uguali byte per byte, salvo i file delle 2 fixture vecchie con le transizioni F4, confrontati come in `ui/f3-equivalence` con
  quelle transizioni ammesse (nessuna differenza non ammessa);
- modulo, registro e chiave di stato `scene`; pagine, schede, viste del disegno e della ricerca, selezioni non scrivono nei dati e
  conservano il risultato; «Nuovo paesaggio» è una modifica;
- calcolo sullo sfondo con barriere: fuori dal thread dell'interfaccia, occupato finché il motore torna, solo la revisione corrente
  pubblicata, chiusura durante un calcolo e durante il ritardo del ricalcolo automatico;
- sola lettura dal contesto (ogni modifica rifiutata, anche l'editor dei prezzari e la ricerca; viste, esportazioni, report e calcolo
  ammessi), ritorno alla modifica, `Reload` uguale a una riapertura, esportazioni CSV e PNG, report.

`--bridge-design` esegue solo questi controlli e quelli dei prezzari (189, circa 40 s), per lavorare sul modulo. Una prova di mutazione
(una riga cambiata in una traccia congelata, poi ripristinata) fa fallire il confronto.

L'adattatore della traccia guida il ViewModel come la vista e lo legge come lo vede l'utente; i passi che nella vista storica chiamano
direttamente ciò che il controllo chiama («Avvia ottimizzazione», «Ponte casuale» con il seme, il clic su un punto della nuvola) sono gli
stessi sui due lati.

Altre prove: `Architecture.Tests` (P1–P7, F4 3213, P3 invariata), `ui/check-bridge-design-historic` (la vista storica che chiama le
funzioni spostate), `ui/check-module-host-offscreen`, `ui/f3-equivalence`, `ui/f3-report-chapters`, `ui/f3-scheduling`, gli smoke di
Bridge Design, prezzari e traffico. Corse e cartelle nella sezione 1.6.

### 1.6 Corse

Cartelle in `supporto/artefatti/ci/` (non versionate):

| Corsa | Che cosa | Esito |
| --- | --- | --- |
| `20261021-132809-f512-base` | quick su `d1f0c4d6` | PASS 55 |
| `20261021-134741-f512-f3-cattura-1`, `…-135322-f512-f3-cattura-2` | cattura F3 con la vista storica (la prima senza attesi) | catture uguali; la seconda PASS |
| `20261021-135724-f512-c1-f3` | `ui/f3-report-chapters`, `ui/f3-scheduling`, `ui/f3-figures` | PASS 3 |
| `20261021-143601-f512-c1-quick2` | quick dopo il passo 1 (la prima, `…-140033`, interrotta dal riavvio della sessione) | PASS 55 |
| `20261021-145539-f512-c2-traccia` | `ui/check-bridge-design-historic` | PASS |
| `20261021-145944-f512-c3-arch2` | Architecture.Tests e Application.Tests dopo il passo 3 (la prima, `…-145831`, prima di spostare le voci della baseline F4) | PASS |
| `20261021-150111-f512-c3-ui` | tracce storiche, `ui/f3-equivalence`, capitoli, scheduling, smoke di Bridge Design, prezzari e traffico | PASS 8 |
| `20261021-150908-f512-c3-quick` | quick dopo il passo 3 | PASS 55 |
| `20261021-153427-f512-c4` | Architecture.Tests, Presentation.Tests, `ui/check-module-host-offscreen` | PASS 7 |
| `20261021-154545-f512-c5` | Architecture.Tests, Presentation.Tests con `BridgeDesignSheetChecks` | PASS 5 (Presentation.Tests 3924 controlli) |
| `20261021-155524-f512-finale` | corsa finale `-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` su `26541726`, con questa nota non ancora registrata (72 minuti) | PASS 148, NEW-FAIL 1: `ui/check-palo-verticale-view` senza attestazione finale (macchina condivisa; il ramo non tocca il palo verticale) |
| `20261021-170818-f512-finale-palo` | `ui/check-palo-verticale-view` ripetuta da sola (`-Stage ui -NoBuild`) | PASS |

Le catture delle tracce sono in `supporto/artefatti/refactoring/f5-12/traccia-1` … `traccia-4` (3 e 4 uguali, registrate).

## 2. Comportamenti della vista storica trovati

Nessun difetto nei risultati: tracce, F3 e capitoli autonomi coincidono. Due comportamenti riguardano la finestra e una revisione in sola
lettura (domande BD3 e BD4); gli altri il ViewModel li conserva perché le tracce siano identiche, e si possono correggere dopo il
passaggio della Release, con una nuova cattura.

1. **«Occupato» non arriva alla finestra.** `SheetEditor.Busy` legge lo stato occupato dei muri, della sezione composta, del palo
   orizzontale e della sezione c.a., ma non quello di Bridge Design: per la finestra il foglio non è mai occupato. Durante un «Calcola»
   senza modifiche il risultato mostrato resta fino alla pubblicazione, quindi «Esporta report» e l'esportazione JSON partono con quello
   (uguale ai dati, perché nessuna modifica è intervenuta). Domanda BD3.
2. **Prezzario in sola lettura.** «Gestisci prezzario…» è ammesso in una revisione in sola lettura (consultazione), ma la finestra è
   fuori dall'albero protetto da `RevisionInspection` e «Applica prezzario» scrive il documento nei dati della revisione, la segna
   modificata e ricalcola. Verificato con la vista storica e uno scenario temporaneo (sola lettura, poi «Applica» dello script
   `applica`), non registrato: dopo l'«Applica» i dati della revisione hanno `pricing`, il foglio è modificato, «Annulla» ha uno stato e
   il calcolo riparte. Il ViewModel rifiuta l'editor in sola lettura: differenza proposta, BD4.
3. **Bordo rosso dopo «Annulla».** Un valore non valido delle ipotesi o delle tariffe ha il bordo rosso; «Annulla» rimette il testo dei
   dati ma lascia il bordo. I campi degli ingressi, ricostruiti, lo perdono anche quando il valore resta non valido.
4. **Tipologia già scelta.** Il suo pulsante riscrive le stesse regole, ricalcola e segna il foglio modificato (nessuno stato nuovo per
   «Annulla», perché i dati non cambiano).
5. **Ricerca in sola lettura.** «Suggerisci campate dalla lunghezza» è ammesso e riscrive i limiti, quindi azzera la ricerca; obiettivo,
   blocchi, limiti, variabile del grafico, filtro e «Solo frontiera Pareto» sono spenti, anche se non cambiano i dati.
6. **«Ripristina A».** Ripristina l'alternativa A del momento dell'ultima pubblicazione del confronto.
7. **Esportazione JSON.** `SheetEditor` aggiunge `dati` al risultato esportato di Bridge Design (domanda BD7).
8. **Ricerca senza risultato.** «Avvia ottimizzazione» prima del primo risultato aspetta il calcolo del ponte («Attesa dell'aggiornamento
   del ponte…») e poi parte; una modifica durante la ricerca la annulla («Dati o listino modificati…», poi «Ricerca interrotta…»).

## 3. Che cosa resta per finire F5.12

Dopo il punto di controllo (l'ordine segue i passi 1–7 di F5.4):
1. **Vista XAML** `X.Desktop/Views/BridgeDesign/BridgeDesignSheetView.xaml` e il suo DataTemplate, solo token di F5.3 (P7):
   - disposizione di oggi: pagine «Progetto» e «Ottimizzazione»; intestazione; colonna degli ingressi con le cinque schede; scheda del
     disegno con i pulsanti, la configurazione e le esportazioni; le quattro metriche; schede dei risultati; alternative; stato;
   - un modello per ogni elemento (`BridgeDesignText` per ruolo, `BridgeDesignParameter` con il cursore, `BridgeDesignChoice`,
     `BridgeDesignOption`, `BridgeDesignFamilies` con le icone, `BridgeDesignTable`, `BridgeDesignButton`, `BridgeDesignGroup`,
     `BridgeDesignMomentChart`), domanda BD6;
   - disegni WPF invariati (`BridgeDesignDrawing` da `DrawingData`/`DrawingResult`/`SectionView` e dall'anteprima, `BridgeOptimizationPlot`
     da `Cloud`/`Trace`, `BridgeConceptMomentPlot`, `BridgeFamilyIcon`); la larghezza delle colonne della tabella delle quote resta nella vista;
   - «Stampa scheda» con la finestra di stampa e `PrintLines` (BD8);
   - l'editor dei prezzari come dialogo del servizio (DataTemplate di `PriceCatalogueViewModel`, oggi `BridgePricesView` in codice);
   - sola lettura: legami a `CanEdit`, tranne pagine, schede, prospetto e sezione, «Ottimizza», «Sezioni e quote», esportazioni,
     «Interrompi», «Suggerisci campate», «Aggiorna elenco», selezione delle soluzioni.
2. **Ospite**: `MvvmSheetEditor.ExportResult` aggiunge `dati` per Bridge Design come `SheetEditor` (BD7); stato occupato secondo BD3.
3. **Banco**: cattura F3 con `--module-impl str_bridge_design=mvvm` (anche `ui/f3-report-chapters` e `ui/f3-scheduling`).
4. **Confronto diretto** fra cattura storica e MVVM a differenza zero (`tools/qa/f5_confronto_diretto.py`), poi il confronto normale;
   ogni differenza va in `transitions-f5.json` con l'ok dell'utente.
5. **Suite UI riscritte** prima del passaggio della Release, sulla vista XAML ospitata da `MvvmSheetEditor` fuori schermo:
   - `supporto/test/Desktop/BridgeDesignSmokeChecks.cs` (`MainWindow.SmokeBridgeDesign`, suite `ui/smoke-bridge-design`),
     `BridgeDesignWorkspace.Tests.cs`, `BridgeOptimizationPlot.Tests.cs`;
   - `tests/Desktop/BridgePricesSmokeChecks.cs` (`ui/smoke-bridge-prices`) e `BridgeTrafficSmokeChecks.cs` (`ui/smoke-bridge-traffic`);
   - `F3TypedSchedulingChecks` (`BridgeDesignWorkspace.CheckF3Scheduling`, suite `ui/f3-scheduling`);
   - `ui/check-bridge-design-historic` resta finché la vista storica esiste.
6. **Punto di controllo**, poi passaggio della Release e archiviazione (Q11) nello stesso commit:
   - `ModuleImplementations.Declared`: `("str_bridge_design", SheetImplementation.Mvvm)`;
   - `BridgeDesignWorkspace.cs`, `BridgeDesignOptimization.cs`, `BridgeDesignOptimizationPanel.cs`, `BridgeDesignTechnical.cs` e
     `BridgePricesView.cs` in `supporto/SUPERATI/ui-storica/bridge-design/`, con `tests/Desktop/BridgeDesignHistoricTrace.cs`; i disegni
     restano;
   - il ramo `bridgeDesign` di `SheetEditor` (campo, costruttore, `Result`, `CalculateAsync`, `ExportResult`, `BuildReportDocument`,
     `Dispose`) e i rami di Bridge Design di `MainWindow` (nome del report) e `ModuleFigureRenderer` dove servono ancora;
   - le tracce congelate restano: `Presentation.Tests` continua a usarle.
7. **Guida pratica e PDF**, se la disposizione descritta cambia.

## 4. Domande per il punto di controllo F5.12 («da decidere»)

Le decisioni del 17/10 valgono anche qui (T2, T6, T7 e le convenzioni delle viste). Le scelte seguenti non hanno una proposta nei
documenti: sono provvisorie, con l'opzione presa, da rivedere con l'utente a F5.15.

- **BD1 · Calcolo sullo sfondo.** Opzione presa: ricalcolo automatico 450 ms dopo l'ultima modifica, subito all'apertura e con
  «Calcola», sul servizio del contesto e con il limitatore dell'applicazione (`SheetCalculationMode.Background`), «occupato» fino al
  ritorno del motore, come oggi e come muri e sezione composta (MU1, SM1).
- **BD2 · Riferimento F3.** `expected/esito.txt` 111 → 119, registrato nel manifest (`fixture_aggiunte_f5_12`), come SM2. Il
  coordinatore ricatena l'impronta con le fixture degli altri rami.
- **BD3 · «Occupato» per la finestra.** La vista storica non dà a `SheetEditor` il suo stato occupato (sezione 2, punto 1): durante un
  «Calcola» senza modifiche la finestra esporta il report con il risultato mostrato. Opzione presa: `IsBusy` del ViewModel vero
  durante il calcolo, come negli altri moduli migrati, quindi con l'ospite MVVM la finestra chiede di attendere. Alternativa: `IsBusy`
  sempre falso, come oggi.
- **BD4 · Prezzario in una revisione in sola lettura.** La vista storica ammette «Gestisci prezzario…» e «Applica prezzario» scrive nei
  dati della revisione (sezione 2, punto 2). Opzione presa, come MU3: il ViewModel rifiuta l'editor in sola lettura. Alternativa:
  aprirlo in sola lettura, senza «Applica».
- **BD5 · Editor dei prezzari in Presentation (Q17 a).** Opzione presa: il ViewModel passa in Presentation e la sua logica, che usa i
  tipi del calcolo, resta in Application come `PriceCatalogueEditing`; Application senza CommunityToolkit. Alternativa: tipi specchio
  dei prezzi in Application e logica nel ViewModel.
- **BD6 · Parametri con il cursore.** Opzione presa: elementi espliciti (`BridgeDesignParameter`: testo grezzo, bordo rosso, cursore),
  non `FormViewModel`/`FieldForm`, perché la regola di scrittura (testo grezzo, validità contro l'intervallo, cursore sincronizzato) e il
  bordo che resta dopo «Annulla» non sono quelli di `InputForm`; F1 apre la guida del modulo come oggi. La vista XAML avrà un modello
  proprio per queste righe invece del `FieldForm` compatto (convenzione D9). Alternativa: estendere `FieldSpec` con il cursore.
- **BD7 · Esportazione del risultato JSON.** `SheetEditor` aggiunge `dati` al risultato esportato di Bridge Design, `MvvmSheetEditor` no.
  Opzione presa: il ViewModel non cambia `Result` (il riferimento F3 lo legge senza `dati`); l'ospite MVVM si adegua con la vista.
- **BD8 · «Stampa scheda».** Resta alla vista (finestra di stampa di WPF); il ViewModel dà i testi della pagina (`PrintLines`).
- **BD9 · Chiave di stato.** `scene` («Nuovo paesaggio», «Ponte casuale») dichiarata come stato dell'interfaccia del modulo; resta
  scritta come una modifica dei dati (ricalcolo, «Annulla»), come oggi.
- **BD10 · Comportamenti storici conservati.** Opzione presa (T7): i punti 3–8 della sezione 2 sono conservati, le tracce sono
  identiche. Alternativa: correggerli dopo il passaggio della Release, con una nuova cattura.
- **BD11 · Prove nello stadio `fast`.** Opzione presa: `BridgeDesignSheetChecks` (tracce e F3 compresi) nello stadio `fast`, come muri e
  sezione composta: circa 40 s in più (Presentation.Tests da circa 430 a 473 s su questa macchina). Alternativa: tracce e F3 nello
  stadio `ui` con un'opzione, come C7 di F5.10.
- **BD12 · Baseline F4 dei disegni.** I file della vista storica non hanno più voci. Restano quelle dei disegni (`BridgeDesignDrawing` 83,
  `BridgeDesignAdvancedDrawing` 21, `BridgeOptimizationPlot` 21: Q3), le 10 spostate in `BridgeDesignPresentation` (estrazioni del ponte
  casuale e regole dei pulsanti), una di `BridgeDesignAnalysis` e 3 del report congelato. Opzione presa: restano, come MU11 e SM8.
- **BD13 · Contrasto a 96 DPI.** `ui/check-contrast` è esclusa dalle corse (schermo al 150%, 144 DPI): va ripetuta a 96 DPI prima
  dell'unione. Il ramo non tocca colori né viste; il passo di contrasto autonomo arriva con la vista XAML.

**Materiale da mostrare** (dopo i passi 1–3 della sezione 3): schermate nei tre aspetti, `--check-contrast`, confronto diretto a zero,
`ui/f3-equivalence` con il modulo MVVM, l'editor dei prezzari sul servizio dei dialoghi, questa nota con le risposte.

## 5. Vista fatta (22/10, di giorno)

Agente f5-12-vista, ramo `refactoring/f5-12-vista` da `refactoring/f5` `5451a8cc`: punti 1 e 2 della sezione 3; le prove del punto 3 e del
punto 5 (la sola parte che non riscrive suite storiche) sono **scritte e compilate ma non eseguite**, per la regola del giorno dell'utente
(22/10: «i test sulla UI le facciamo stasera»). La Release resta **storica**: la vista nuova si apre solo con il banco UiTests
(`--module-impl str_bridge_design=mvvm`) e con `ANTHEA.exe --interactive --module-impl str_bridge_design=mvvm`. Le scelte provvisorie della
sezione 4 restano; le nuove sono nella sezione 5.6.

### 5.1 Commit

| Commit | Contenuto |
| --- | --- |
| `6cdbde5e` | vista XAML `X.Desktop/Views/BridgeDesign`, editor dei prezzari come dialogo del servizio, pagina di «Stampa scheda», colore della legenda; comandi dei file nel ViewModel dell'editor |
| `e88956d3` | ospite: `MvvmSheetEditor.ExportResult` con `dati` (BD7) |
| `eefb4a57` | prove scritte e non eseguite: `ui/check-bridge-design-view`, sonda MVVM di `ui/f3-scheduling`, passo di contrasto autonomo, voci di `build/ci.ps1` |
| `867df33c` | rilettura della prova: errori dello script del prezzario riportati dopo il clic, vista ospitata letta una volta |
| (questo commit) | questa sezione |

### 5.2 Vista

**File** (`X.Desktop/Views/BridgeDesign`):
- `BridgeDesignSheetView.xaml(.cs)`: la disposizione di `BridgeDesignWorkspace`.
  - Pagina «Progetto»: titolo, sottotitolo e ambito; la colonna degli ingressi con le cinque schede (ognuna scorre entro 490) e, accanto (sotto,
    sotto i 1100 di larghezza), la scheda del disegno con i pulsanti, il disegno, la configurazione e le esportazioni; le quattro metriche (su due
    colonne sotto 850); le sei schede dei risultati; le alternative; la riga di stato.
  - Pagina «Ottimizzazione»: «1 · Imposta la ricerca» accanto all'anteprima (sotto, sotto 1120); «2 · Variabili esplorate» e «3 · Famiglia di
    soluzioni plausibili»; la legenda delle tipologie; «4 · Alternative ordinate…» con la graduatoria.
- `BridgeDesignResources.xaml`: stili dei testi, carta, schede, pulsanti chiari e primari, caselle della ricerca; **un modello per ogni elemento**
  (BD6), implicito per tipo: `BridgeDesignText` per ruolo, `BridgeDesignTable`, `BridgeDesignButton`, `BridgeDesignGroup`, `BridgeDesignMomentChart`,
  `BridgeDesignParameter` (testo grezzo, bordo rosso, cursore), `BridgeDesignChoice`, `BridgeDesignOption`, `BridgeDesignFamilies` con le icone.
  I parametri non usano il `FieldForm` compatto (D9), come deciso in BD6.
- `BridgeDesignItems.cs`:
  - `BridgeDesignGrid`: le tabelle di `Table`, con colonne a stella (la prima doppia; quelle di «Sezioni e quote» con le larghezze di
    `ShowTechnical`), celle a capo, l'altezza massima della tabella storica riconosciuta dalle colonne, la selezione della graduatoria;
  - `BridgeDesignSlider` (BD20);
  - `BridgeDesignMomentHost` e `BridgeDesignFamilyIcon`: i disegni storici `BridgeConceptMomentPlot` e `BridgeFamilyIcon`, invariati (Q3);
  - la rotella inoltrata alla pagina e i convertitori. Nessuno stato statico mutabile (P3).
- `PriceCatalogueView.xaml(.cs)`: l'editor dei prezzari, DataTemplate di `PriceCatalogueViewModel` per `WpfDialogService`, con la disposizione di
  `BridgePricesView` e la misura 1350 × 780 senza i bordi, come gli altri dialoghi del servizio (BD22).
- `BridgeDesignTemplates.xaml`: i due DataTemplate, uniti da `App.xaml`.

Fuori dalle viste:
- `X.Desktop/Wpf/BridgeDesignPrintPage.cs`: la pagina di «Stampa scheda», copia di `BridgeDesignWorkspace.PrintPage` (BD18);
- `BridgeOptimizationPlot.LegendBrush`: il colore di una tipologia nella legenda dei grafici.

**Che cosa fa il code-behind** (Q3 e stato della vista):
- i disegni storici sui dati del ViewModel: `DrawingData`, `DrawingResult` e `SectionView` per il disegno; `SolutionData`, `SolutionResult` e
  `SolutionSection` per l'anteprima; `Cloud`, `CloudRank`, `FamilyFilter` e `ParetoOnly` per la nuvola; `Trace` e `Variable` per le variabili
  esplorate. Il clic su un punto della nuvola seleziona la soluzione attraverso il foglio;
- le due larghezze della vista storica;
- i blocchi e i limiti della ricerca: le caselle mostrano i dizionari del foglio e scrivono con `SetLock`, `SetLimit` e `LeaveLimit` (BD19);
- «Sezioni e quote» e «Applica soluzione selezionata» portano in vista i risultati;
- «Stampa scheda» con la finestra di WPF (BD8).

Nessuna scrittura del documento fuori dal ViewModel.

**Sola lettura** (T2):
- bloccati: parametri, scelte, opzioni, tipologie, blocchi, limiti, obiettivo, variabile, filtro, Pareto e i comandi che cambiano i dati;
- disponibili, come nella vista storica dopo `RevisionInspection.Protect`: pagine, schede, «Prospetto», «Sezione», «Ottimizza», «Sezioni e quote»,
  esportazioni, «Stampa scheda», «Suggerisci campate», «Interrompi», «Aggiorna elenco» e la selezione delle soluzioni;
- «Gestisci prezzario…» bloccato (BD4).

**ViewModel** (`6cdbde5e`): `PriceCatalogueViewModel` riceve il servizio dei file del foglio (parametro facoltativo) e ha i comandi «Importa CSV /
Excel / JSON», «Modello CSV» ed «Esporta catalogo JSON», con i filtri, i nomi, il limite di 20 MB e i rifiuti della finestra storica (BD17). Le
tracce senza WPF non cambiano (`--bridge-design` PASS 189).

**Solo token di F5.3** (P7, `Architecture.Tests` PASS). Corpi: 10 e 11 → `Font.Caption`, 12 → `Font.Small`, 13 → `Font.Body`, 14 →
`Font.Subheading`, 15 e 17 → `Font.Heading`, 21, 24 e 26 → `Font.Title` (BD14).

### 5.3 Ospite

- **BD7.** `MvvmSheetEditor.ExportResult` aggiunge `dati` al JSON del risultato di Bridge Design, come `SheetEditor`. Il risultato del foglio è un
  oggetto nuovo a ogni lettura; i dati sono copiati.
- **BD3.** Nessuna modifica: `MvvmSheetEditor.Busy` legge `IsBusy` del foglio, vero dall'avvio del motore al suo ritorno. La finestra quindi chiede
  di attendere (report, JSON) durante un calcolo. La prova è nella parte `banco` della suite della vista.

### 5.4 Prove scritte (non eseguite di giorno)

`ui/check-bridge-design-view` (`tests/Desktop/BridgeDesignViewChecks*.cs`, comando `--check-bridge-design-view <radice> <cartella> [parti]`), con
queste parti:
- `tracce`: i 19 scenari guidati attraverso i controlli della vista, con il motore trattenuto delle tracce: pagine e schede, testo e cursore dei
  parametri, scelte, opzioni, tipologie, pulsanti dei pannelli, alternative, l'editor dei prezzari mostrato dal suo DataTemplate, blocchi, limiti,
  graduatoria, nuvola, variabile, filtro, Pareto. Lo stato si rilegge dai controlli (stato, metriche, configurazione, pagine, disegno, caselle e
  testi della ricerca, avanzamento, pulsanti, anteprima, grafici); i pannelli dal ViewModel (MU19). Deve uguagliare le tracce congelate;
- `disegni`: gli elenchi dei disegni della vista e i pixel delle figure del report per le 10 fixture, contro `disegni/elenco.txt`;
- `esportazioni`: per le 10 fixture la visita della cattura F3, i tre aspetti, dati e risultato dopo il calcolo, il JSON con i dati (BD7), Word,
  PDF, CSV delle quantità e delle quote e PNG, uguali alla vista storica;
- `prezzario`: l'editor sul servizio dei dialoghi contro la finestra storica (titolo, misura, schede, colonne, testi, pulsanti); i file sul
  servizio dei file; «Annulla»; «Applica prezzario»; il rifiuto in sola lettura (BD4);
- `stampa`: la pagina uguale a quella storica (testi e pixel), la finestra della stampante solo con un risultato;
- `schermate` (tre aspetti, disposizione stretta, vista storica), `aiuto` (F1 al navigatore), `nomi`, `sola-lettura` (diverso solo «Gestisci
  prezzario…»), `banco` (banco, Release storica, BD3, BD7).

Inoltre:
- `ui/f3-scheduling-bridge-design-mvvm`: `F3BridgeDesignMvvmSchedulingChecks` sostituisce `BridgeDesignWorkspace.CheckF3Scheduling` quando il banco
  apre Bridge Design con la vista MVVM (stesse barriere, ricerca in attesa del calcolo);
- contrasto: passo autonomo `mvvm-str_bridge_design` in fondo a `supporto/test/Desktop/ContrastAudit.cs` (commenti `F5.12 begin/end`); con
  `--baseline` solo Scuro e Molto scuro;
- `tests/Shared/BridgeDesignTrace.cs`: `BridgeDesignItemBlocks` (gli elementi del ViewModel come blocchi della traccia), spostato da
  `BridgeDesignSheetChecks` per usarlo anche nella prova della vista.

### 5.5 Esiti di giorno

| Prova | Esito |
| --- | --- |
| Build Release e UiTests | ok, nessun avviso nei file nuovi |
| `Architecture.Tests` | PASS 571 controlli: P1–P7 (P7 sulle viste nuove), P3 126 invariata, nessuna voce F4 nuova |
| `Presentation.Tests --bridge-design` | PASS 189: le tracce senza WPF non cambiano con i comandi dei file dell'editor dei prezzari |
| Profilo quick dopo la vista, `supporto/artefatti/ci/20261022-094228-f512v-c1-quick` | PASS 55, NEW-FAIL 0 (uscita 1 solo per i file modificati durante la corsa) |
| Corsa finale standard su `867df33c`, `supporto/artefatti/ci/20261022-100634-f512v-finale` (22 minuti) | PASS 77, KNOWN 0, **NEW-FAIL 0**; `Presentation.Tests` 4244 controlli |
| Prove UI (`ui/check-bridge-design-view`, banco F3, sonda MVVM, smoke, contrasto) | **non eseguite** (regola del giorno): sezione 5.7 |

Nessuna baseline cambiata (F4 3213, P3 126); tracce congelate, riferimento F3, `transitions-f4.json` e report congelati invariati.

### 5.6 Differenze visibili e scelte da confermare («da decidere», con la scelta fatta)

Differenze visibili, le stesse nei tre aspetti:
- **Corpi** (BD14):
  - titoli delle pagine da 24 a 22 punti, titolo del prezzario da 21 a 22, valori delle metriche da 26 a 22;
  - titoli dei riquadri della ricerca e di «Sezioni e quote» da 17 a 16, titoli dei pannelli da 15 a 16;
  - titoletti delle metriche da 10 a 11.
- **Colori** (BD15):
  - il bordo delle carte `#D8E0EB` diventa `Token.Border`;
  - l'ambito `Ui.Blue` diventa `Token.Accent`;
  - gli avvisi `#895C17` diventano `Token.Note.Foreground`;
  - gli errori e il bordo di un valore non valido (`#CD5C5C`) diventano `Token.Status.Fail`;
  - le luci della tipologia scelta `#BCCFE2` diventano `Token.OnCommandBar.Muted`.

  Negli aspetti scuri i colori seguono l'aspetto.
- **Prezzario** (BD16): nella scheda «Prezzario» il titolo e la nota del documento dei prezzi stanno nel riquadro che scorre (la vista storica li
  metteva sopra). Senza documento la disposizione è quella storica.

Scelte provvisorie della vista (da rivedere a F5.15):
- **BD14 · Corpi.** Opzione presa: i valori delle metriche (26) a `Font.Title` (22), non a `Font.Display` (30), per non tagliarli nelle quattro
  colonne; 15 e 17 a `Font.Heading` (16, D3). Alternativa: `Font.Display` per le metriche.
- **BD15 · Colori.** Opzione presa: i token più vicini (sopra). Alternativa: token nuovi per `#BCCFE2` e `#CD5C5C`.
- **BD16 · Riquadri dei pannelli.** Opzione presa: altezze e larghezze delle tabelle storiche riconosciute dalle colonne (un'informazione della
  vista, non del ViewModel); pulsanti in barra sotto il confronto e sotto «Sezioni e quote», a tutta larghezza altrove, come la vista storica.
  Alternativa: le misure come dati degli elementi del ViewModel.
- **BD17 · File del prezzario.** Opzione presa: comandi del ViewModel dell'editor sul servizio dei file del foglio. Il parametro è facoltativo:
  la finestra storica tiene i suoi dialoghi. Alternativa: dialoghi nel code-behind della vista.
- **BD18 · Pagina di «Stampa scheda».** Opzione presa: costruita da `BridgeDesignPrintPage` in `X.Desktop/Wpf`, con i corpi e i colori storici,
  perché è un documento (stampato nell'aspetto chiaro), non una vista. Alternativa: una pagina XAML con i token.
- **BD19 · Blocchi e limiti della ricerca.** Opzione presa: il ViewModel espone dizionari; la vista li mostra con legami a una via e scrive con
  `SetLock`, `SetLimit` e `LeaveLimit`. Alternativa: un elemento del ViewModel per ogni blocco e ogni limite.
- **BD20 · Cursore.** Opzione presa: `BridgeDesignSlider`, senza legame a due vie sul valore e con l'intervallo impostato prima del valore. Così
  un cursore riciclato o un intervallo provvisorio non può mai scrivere nei dati. Alternativa: legame a due vie, affidato all'ordine degli
  attributi.
- **BD21 · «Occupato».** Opzione presa: nessuna barra di avanzamento del calcolo (la vista storica non l'aveva); la riga di stato dice
  «Aggiornamento del ponte e delle alternative…». Alternativa: la barra indeterminata delle altre viste.
- **BD22 · Finestra del prezzario.** Opzione presa: la misura viene dalla vista (1334 × 741) e la finestra si adatta (`SizeToContent` di
  `WpfDialogService`), come i dialoghi dei muri (MU10).

### 5.7 Da eseguire stasera, e perché

I comandi esatti sono nella sezione 5.9 e nel file di avanzamento dell'agente (`ANTHEA-f5-ripresa\f5-agenti\f5-12-vista\avanzamento.md`).
1. `ui/check-bridge-design-view`: la prova della vista, mai eseguita. Se una traccia diverge, `tracce/differenze.txt` dà la prima riga diversa.
2. Banco F3 (punto 3 della sezione 3):
   - cattura storica e cattura con `--module-impl str_bridge_design=mvvm`;
   - `tools/qa/f5_confronto_diretto.py`, atteso 0 differenze;
   - `ui/f3-report-chapters` con il banco;
   - `ui/f3-scheduling` e `ui/f3-scheduling-bridge-design-mvvm`.
3. Le suite storiche che toccano i file cambiati:
   - `ui/check-bridge-design-historic` (il disegno della ricerca ha un metodo in più);
   - `ui/smoke-bridge-design`, `ui/smoke-bridge-prices`, `ui/smoke-bridge-traffic` (costruttore dell'editor dei prezzi);
   - `ui/check-module-host-offscreen` (DataTemplate registrato);
   - `ui/f3-equivalence` (Release storica, deve restare invariata).
4. `ui/check-contrast` a 96 DPI con il passo `mvvm-str_bridge_design` (BD13), senza cambiare la baseline.

### 5.8 Suite storiche: da riscrivere o togliere al passaggio della Release

Non riscritte (fuori dal compito di oggi); vanno fatte prima del passaggio (punto 5 della sezione 3):
- `supporto/test/Desktop/BridgeDesignSmokeChecks.cs` (`ui/smoke-bridge-design`), `BridgeDesignWorkspace.Tests.cs`, `BridgeOptimizationPlot.Tests.cs`;
- `tests/Desktop/BridgePricesSmokeChecks.cs` (`ui/smoke-bridge-prices`, apre `BridgePricesView`) e `BridgeTrafficSmokeChecks.cs`
  (`ui/smoke-bridge-traffic`).

Al passaggio della Release, oltre al punto 6 della sezione 3:
- togliere `ui/f3-scheduling-bridge-design-mvvm`, `$MvvmBridgeDesign` e il ramo storico di `F3SchedulingChecks`;
- togliere il blocco `F5.12` di `ContrastAudit.cs` e passare le voci della Chiara a `modulo-str_bridge_design` (D12);
- `BridgeDesignDrawing` e `BridgeConceptMomentPlot` chiamano `BridgeDesignWorkspace.F`: passarli a `BridgeDesignPresentation.F` prima di archiviare
  la vista;
- le parti di `ui/check-bridge-design-view` che aprono la vista storica (esportazioni, prezzario, stampa, nomi, sola lettura) passano al confronto
  con valori attesi;
- `ui/check-bridge-design-historic` va con la vista storica.

La guida pratica non cambia: la disposizione descritta è quella di oggi (punto 7 della sezione 3).

### 5.9 Come riprendere

```
cd <worktree>
git log --oneline 5451a8cc..HEAD
dotnet build X.Desktop\X.Desktop.csproj -c UiTests
dotnet X.Desktop\bin\UiTests\net8.0-windows\ANTHEA.dll --check-bridge-design-view <radice> <cartella vuota> [tracce,disegni,esportazioni,prezzario,stampa,schermate,aiuto,nomi,sola-lettura,banco]
dotnet X.Desktop\bin\UiTests\net8.0-windows\ANTHEA.dll --check-f3-equivalence <radice> <cartella>\storica
dotnet X.Desktop\bin\UiTests\net8.0-windows\ANTHEA.dll --check-f3-equivalence <radice> <cartella>\mvvm --module-impl str_bridge_design=mvvm
python -I tools\qa\f5_confronto_diretto.py --a <cartella>\storica --b <cartella>\mvvm --out <cartella>\confronto-diretto.json
dotnet X.Desktop\bin\UiTests\net8.0-windows\ANTHEA.dll --check-f3-report-chapters <radice> <cartella>\capitoli --module-impl str_bridge_design=mvvm
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,ui -Only '^ui/(check-bridge-design-view|check-bridge-design-historic|f3-scheduling|f3-scheduling-bridge-design-mvvm|smoke-bridge-design|smoke-bridge-prices|smoke-bridge-traffic|check-module-host-offscreen|f3-equivalence|f3-report-chapters)$' -Tag f512v-sera"
```

Poi, dopo l'ok dell'utente: versione portatile con la vista attiva, riscrittura delle suite della sezione 5.8, punto di controllo, passaggio della
Release e archiviazione (punto 6 della sezione 3).

## 6. Prima esecuzione della vista (22/10 sera)

Agente f5-12-vista-prova, ramo `refactoring/f5-12-vista-prova` da `refactoring/f5` `c1dd1d1c`. Le prove sono nel `supporto/artefatti` del
worktree (`ci\…` e `refactoring\f5-12-vista-prova\…`).

### 6.1 Perché `ui/check-bridge-design-view` si fermava

Nella corsa mirata del coordinatore (`ci\20261022-190511-mirate-22-10`, checkout principale) e nella sua ripresa
(`ci\20261022-200741-mirate-22-10-ripresa`) la suite si chiudeva dopo circa 100 s, sempre nello stesso punto:
- codice d'uscita 0, nessun `esito.txt`, `dettaglio.txt` o `errore.txt`. Il `finally` e il `catch` della prova non giravano: il processo è
  stato chiuso da fuori. Un'eccezione non gestita avrebbe dato un codice diverso da 0 e un evento nel registro Application;
- in `vista\esportazioni` restava un `.x_<guid>.tmp` scritto subito dopo `…cassone-acciaio-prezzario-quantita.csv`. L'esportazione successiva
  è «Immagine PNG» (`…-ponte.png`): il temporaneo è proprio quel PNG, già completo (40 816 byte, 1200 × 480, gli stessi byte del PNG scritto
  poi dal worktree). Il processo si chiude fra la scrittura del temporaneo e la sua rinomina in `DocumentFile.Write`. È la terza immagine della
  sessione;
- il registro del monitoraggio del comportamento di Trend Micro, leggibile senza diritti di amministratore
  (`C:\Program Files (x86)\Trend Micro\Client Server Security Agent\Misc\AEGIS_BM.log`, UTF-16), ha un evento per ognuna delle quattro fermate
  della sera. La regola è `NON_POLICY_JUST_ADC`, il processo `…\ANTHEA\X.Desktop\bin\UiTests\net8.0-windows\ANTHEA.exe` (SHA1 `43840EBD…`), il
  file un `.x_*.tmp` nella cartella `vista\esportazioni` della suite:

  | Ora | Suite | Corsa |
  | --- | --- | --- |
  | 19:23:28 | `ui/check-palo-verticale-view` | `mirate-22-10` |
  | 19:40:28 | `ui/check-bridge-design-view` | `mirate-22-10` |
  | 20:09:06 | `ui/check-palo-verticale-view` | `mirate-22-10-ripresa` |
  | 20:10:51 | `ui/check-bridge-design-view` | `mirate-22-10-ripresa` |

È la domanda **S6** di [f5-ripresa.md](f5-ripresa.md). Il commento di `src/ANTHEA.Reports/DocumentFile.cs` descrive la stessa chiusura senza errori
(27/09/2026) per i documenti Office, che per questo si copiano; le immagini invece si rinominano ancora (`File.Move`). Anche la vista storica
esporta il PNG con `Archivio.ScriviAtomico` (`BridgeDesignWorkspace.ExportPng`): non è un comportamento nuovo della vista XAML.

**La chiusura non dipende dal codice.** Lo stesso eseguibile (stesso SHA1), dal worktree, scrive tutte le esportazioni e arriva in fondo, senza
eventi di Trend Micro:

| Prova (eseguibile `ANTHEA.exe`, uscite catturate: stdout e stderr vuoti) | Esito |
| --- | --- |
| a mano, parte `esportazioni` (codice della base, nove PNG rinominati), `refactoring\f5-12-vista-prova\diagnosi\exe-esportazioni` | PASS 109, codice 0 |
| a mano, `tracce,disegni,esportazioni` (la sequenza della corsa del coordinatore), `diagnosi\exe-tracce-disegni-esportazioni` | PASS 111, codice 0 |
| a mano, `esportazioni,immagini` con l'uscita sotto Documenti (`ANTHEA-f5-ripresa\f5-agenti\f5-12-vista-prova\diagnosi\…`) | PASS 146, codice 0 |
| runner, tutta la suite (dopo `7decf8ce`), `ci\20261022-204130-f512vp-vista` | **PASS 186 controlli**, 108 s |
| runner, `ui/check-palo-verticale-view` (stessa causa, per confronto), `ci\20261022-205422-f512vp-pv-diagnosi` | PASS, 79 s |

Stasera l'antivirus ha chiuso l'eseguibile del checkout principale, che ha eventi di Trend Micro dal 19/10, e non quello del worktree. Il
21/10 però aveva chiuso anche eseguibili di worktree (`aw-f5-10-s1b`, `aw-f5-12-prep`). Il criterio dell'antivirus non si legge dal registro:
la fermata non è riproducibile a comando e non indica un difetto della vista né della prova.

**Palo verticale.** `ui/check-palo-verticale-view` ha la stessa causa: righe delle 19:23:28 e 20:09:06 qui sopra. Il temporaneo rimasto è il PNG
del grafico della capacità (1200 × 750, «Salva PNG…», `Archivio.ScriviAtomico` → `DocumentFile.Write`), alla terza fixture (`elica-coesivo-avvisi`)
e cioè ancora alla terza immagine. Dal worktree passa.

### 6.2 Correzioni (solo nella prova)

`7decf8ce` test(f5.12): la prova non nasconde la fermata, ma prima verifica tutto il resto.
- **Ordine delle parti:** `tracce, disegni, prezzario, stampa, schermate, aiuto, nomi, sola-lettura, banco, esportazioni, immagini`. Una parte
  sconosciuta nel quarto argomento è rifiutata.
- **Parte `immagini`, l'ultima:** «Immagine PNG» esce da `esportazioni`. Per ogni fixture con un risultato rifà i passi di prima dell'immagine
  (calcolo dell'apertura, visita, calcolo) e controlla:
  - il nome proposto e il filtro della vista storica;
  - il PNG uguale pixel per pixel al disegno della vista storica;
  - nessuna scrittura nei dati.

  `esportazioni` controlla JSON, Word, PDF e i due CSV per tutte le fixture.
- **`dettaglio.txt` scritto riga per riga:**
  - le righe `== parte <nome>` e `== parte <nome>: PASS, n controlli` aprono e chiudono ogni parte;
  - prima di ogni PNG una riga `in corso: <fixture>: «Immagine PNG» → <file> (… se la prova finisce qui senza esito: antivirus, domanda S6)`.

  Se il processo è chiuso da fuori, `dettaglio.txt` mostra le parti passate e l'immagine in corso; il runner riporta come prima «attestazione
  finale assente: esito.txt».
- **Schermata dell'ottimizzazione:** la ricerca della traccia «ottimizzazione» (fixture `default`, pila e fondazione bloccate, campate 3–6
  scritte nelle caselle del foglio): «29 calcolate · 10 ammesse». La stessa ricerca su `confronto-alternativa-a` non ammette soluzioni e
  lasciava la pagina vuota.

Le parti mai eseguite prima (prezzario, stampa, schermate, aiuto, nomi, sola-lettura, banco) sono passate alla prima esecuzione
(`refactoring\f5-12-vista-prova\prove\dll-parti-nuove-1`, PASS 76): nessuna correzione della vista o del ViewModel. Codice di produzione,
`DocumentFile.Write`, fixture, tracce congelate, baseline e `transitions-*.json` sono invariati. La suite non va in `build/known-failures.json`:
un'eccezione d'ambiente non ci va (f5-ripresa.md).

### 6.3 Esiti

| Prova | Cartella (`supporto\artefatti\` del worktree) | Esito |
| --- | --- | --- |
| `ui/check-bridge-design-view`, runner | `ci\20261022-204130-f512vp-vista` | PASS 186 controlli: tracce (19 scenari, 15 143 righe uguali alle congelate), disegni (72 righe di `elenco.txt`), prezzario 15, stampa 5, schermate 11 PNG, aiuto 3, nomi 3, sola lettura 4, banco 6, esportazioni 108 (9 report), immagini 37 (9 PNG) |
| Banco F3, cattura storica | `refactoring\f5-12-vista-prova\banco-1\storica` | PASS 119 fixture, 77 articoli, 30 ricerche (182 s) |
| Banco F3, cattura con `--module-impl str_bridge_design=mvvm` | `…\banco-1\mvvm` | PASS 119 fixture (199 s) |
| `f5_confronto_diretto.py` | `…\banco-1\confronto-diretto.json` | **0 differenze**: 1205 file, 1202 uguali byte per byte, 3 JSON uguali in forma canonica; 6 tempi volatili del palo elastico ignorati |
| `--check-f3-report-chapters --module-impl str_bridge_design=mvvm` | `…\banco-1\capitoli-mvvm` | PASS 120 capitoli autonomi, 288 immagini |
| Corsa finale ui su `df84474c`: `-Stage build,ui -Only '^ui/(check-bridge-design-view\|f3-scheduling-bridge-design-mvvm)$'` | `ci\20261022-205706-f512vp-finale-ui` | PASS 3: build UiTests, vista 186 controlli, sonda MVVM 47 controlli |
| Corsa finale `-Profile standard` su `df84474c` | `ci\20261022-205930-f512vp-finale` | PASS 78, KNOWN 0, NEW-FAIL 0 (26 minuti), nessun file tracciato modificato |
| Corsa del coordinatore, le altre suite della sezione 5.7 | checkout principale, `ci\20261022-190511-mirate-22-10` | PASS: `check-bridge-design-historic`, `f3-scheduling-bridge-design-mvvm`, `f3-scheduling`, `smoke-bridge-design`, `smoke-bridge-prices`, `smoke-bridge-traffic`, `check-module-host-offscreen`, `f3-equivalence`, `f3-report-chapters` |

Nessuna differenza fra la vista storica e la vista XAML nel banco F3: niente da proporre per `transitions-f5.json`.

Schermate (144 DPI) controllate e copiate per l'utente in `ANTHEA-f5-ripresa\f5-agenti\f5-12-vista-prova\schermate\`:
- `progetto-chiara.png` e, accanto, `storica-progetto-chiara.png` (la vista storica alla stessa misura);
- `confronto-scuro.png`;
- `ottimizzazione-molto-scuro.png`;
- `prezzario.png`.

Negli aspetti scuri i disegni (ponte, grafici, anteprima) restano chiari, come i disegni storici (Q3).

### 6.4 Che cosa resta

- **S6 (domanda all'utente, invariata):** copiare anche le immagini in `DocumentFile.Write` (una riga, nel codice dei report congelato), oppure
  lanciare le suite `check` con `dotnet ANTHEA.dll` (firmato) invece di `ANTHEA.exe` (`build/ci.ps1`, file condiviso), oppure la verifica di
  Trend Micro. Finché resta aperta, la suite può fermarsi nella parte `immagini`. Le altre parti però hanno già riportato PASS in
  `dettaglio.txt`, e la riga «in corso» dice quale immagine si scriveva. Lo stesso vale per `ui/check-palo-verticale-view`, dove l'immagine sta
  ancora dentro la parte delle esportazioni (prova di F5.6, non toccata qui).
- **Contrasto a 96 DPI** (BD13), con il passo `mvvm-str_bridge_design`: lo schermo di questa macchina è a 144 DPI.
- Poi, come nella sezione 5.9:
  - le domande BD1–BD22 al punto di controllo;
  - la versione portatile;
  - la riscrittura delle suite della sezione 5.8;
  - il passaggio della Release e l'archiviazione (punto 6 della sezione 3).

Per ripetere (`<R>` = radice del worktree; parti: `tracce,disegni,prezzario,stampa,schermate,aiuto,nomi,sola-lettura,banco,esportazioni,immagini`):

```
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,ui -Only '^ui/check-bridge-design-view$' -Tag <tag>"
dotnet X.Desktop\bin\UiTests\net8.0-windows\ANTHEA.dll --check-bridge-design-view <R> <cartella vuota> [parti]
```

Per leggere il registro di Trend Micro dopo una fermata:

```
iconv -f UTF-16LE -t UTF-8 "/c/Program Files (x86)/Trend Micro/Client Server Security Agent/Misc/AEGIS_BM.log" | tail -5 | cut -f1,5,7,9
```
