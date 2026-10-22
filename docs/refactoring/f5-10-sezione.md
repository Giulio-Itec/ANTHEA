# F5.10 — Sezione in c.a.: preparazione

## Scopo

Questa nota chiude la **preparazione** di F5.10, parte A: modulo `str_palo`, la sezione in c.a. Oggi il foglio passa dal ramo c.a.
di `SheetEditor` (campo `concrete`, `new ConcreteWorkspace(Data)`) e da 26 file di `X.Desktop/Wpf` (3 211 righe dopo il passo 3):
`ConcreteWorkspace` (21 file partial), i disegni (`ConcreteViewports.cs`: `ConcreteSectionViewport`, `DomainViewport3D`,
`DomainWireOverlay`, `ViewportFrame`; `ConcreteDimensions.cs`, quote della sezione; `ConcreteStrainPlane.cs`; `NeutralAxisOverlay.cs`)
e la finestra delle sollecitazioni (`SectionActionsView.cs`, già su `SectionActionsViewModel`). La finestra della reologia
(`ConcreteTimeEffectsView`) è di F5.5.

Il foglio c.a. è usato anche da due moduli migrati in parallelo:
- il palo elastico (F5.8) apre una `ConcreteWorkspace` in un dialogo modale (`PileReinforcementEditor.OpenConcrete`), «verificatore
  c.a.» di un tratto, e riscrive i dati nel palo;
- i muri (F5.9) mandano la sezione di una parete a un foglio `str_palo` nuovo (`MainWindow.OpenWallConcreteSection` →
  `OpenModuleCopy`).

La preparazione segue la ricetta di [f5-4-pilota.md](f5-4-pilota.md) con una differenza voluta: il modulo è troppo grande per un
ViewModel in un colpo solo (proposta, riga F5.10 della sezione 8: «in sottopassi»). Per questo la parte A **non** scrive ViewModel,
vista XAML, `ModuleImplementations` né archiviazione: cattura il comportamento, sposta le derivazioni più chiare e scrive il piano
dei sottopassi. Contiene:
- che cosa esiste dopo la preparazione (sezione 1);
- i comportamenti della vista storica trovati (sezione 2);
- l'inventario della vista per file, con i membri nominati dalle prove (sezione 3);
- il piano dei sottopassi S1–S9 (sezione 4);
- l'uso della sezione nuova da parte del palo elastico e dei muri (sezione 5);
- che cosa resta e le domande per il punto di controllo (sezioni 6 e 7);
- il sottopasso S1 fatto il 18/10: ViewModel del guscio, del calcolo, degli ingressi e dei materiali, prove ed esiti (sezione 8);
- il riallineamento del 21/10 al codice dopo persistenza nativa, wizard e capsula (sezione 9);
- il sottopasso S2 fatto il 21/10: le azioni, il modello di tabella dei pannelli, prove ed esiti (sezione 10);
- il sottopasso S1b fatto il 21/10: sezioni da wizard e persistenza nativa nel ViewModel, con le tracce nuove (sezione 11);
- il sottopasso S3 fatto il 21/10: i pannelli dei domini 3D e 2D nel ViewModel, disegni come dati, prove ed esiti (sezione 12);
- il sottopasso S4 fatto il 21/10: le famiglie SLE nel ViewModel, la mappa delle tensioni come dati, prove ed esiti (sezione 13);
- il sottopasso S5 fatto il 21/10: taglio e torsione nel ViewModel, staffe in sola lettura, prove ed esiti (sezione 14);
- il sottopasso S6 fatto il 22/10: dettagli costruttivi e ancoraggi nel ViewModel, prove ed esiti (sezione 15);
- il sottopasso S7 fatto il 22/10: sezione σ/ε e momento–curvatura nel ViewModel, correzione dei pannelli dei domini (sezione 16);
- il sottopasso S8 fatto il 22/10: progetto delle armature nel ViewModel, prove ed esiti (sezione 17); da dove parte S9 (sezione 18);
- il sottopasso S9 fatto il 22/10, parte ViewModel: report Word e breve con i generatori congelati, scenari `report/*`, percorso F3 completo
  (sezione 19).

Riferimenti:
- [proposta](f5-proposta-architettura.md), sezioni 3, 5, 7, riga F5.10 della sezione 8, rischi R3, R5 e R7, domanda Q17;
- [inventario](f5-inventario-ui.md);
- `tests/fixtures/f3/README.md` (sezione «Fixture aggiunte in F5.10»), `tests/fixtures/f5-sezione/README.md`;
- le note dei moduli che ospitano la sezione: `f5-8-palo-elastico.md` (domanda E1) e `f5-9-muri.md`, sul ramo `refactoring/f5`.

Ramo `refactoring/f5-10-sezione`, da `refactoring/f5` `743d7aec`, 17/10/2026.

## 1. Che cosa esiste dopo la preparazione

| Commit | Contenuto |
| --- | --- |
| `029d5d0b` | sette fixture F3 della sezione c.a. (87 nel riferimento), attese catturate con la vista storica |
| `2b200297` | tracce e disegni congelati della vista storica: 37 scenari per sottopasso, suite `ui/check-sezione-historic` |
| `df0e4b6f` | derivazioni di ingressi, materiali e azioni in Application (`ConcreteInputPresentation`, `ConcreteActionPresentation`); baseline F4 3 342 → 3 340 |

### 1.1 Fixture F3

Prima di F5.10 la sezione aveva quattro fixture, tutte rettangolari, con le azioni storiche e le opzioni predefinite. Le sette nuove
(`F3Fixtures.ConcreteSectionF510`) coprono i sottopassi: sezione circolare cava con anello interno e spirale, modello di taglio delle
pile, dominio 2D proiettato; sezione a T con sollecitazioni uniche (EN 1992-1-1); materiali dell'utente e barre per coordinate (DIN);
SLE e fessurazione con assi personalizzati (Model Code 2010); taglio senza staffe con parametri manuali (UNI); soletta con dettagli,
sovrapposizioni e opzioni della ricerca; dati incompleti con gli avvisi della scheda. Dettaglio, limiti voluti (niente trefoli,
nessun calcolo fermato per intero, righe in ordine di nome) e catture nel README del riferimento.

Nel riferimento F3 cambiano solo i file aggiunti e il numero in `expected/esito.txt` (80 → 87), registrati nel manifest
(`fixture_aggiunte_f5_10`). Gli attesi esistenti, le figure CA congelate e `transitions-f4.json` non cambiano. Due prove di
Application che leggono ogni fixture `str_palo` sono state adeguate (`ConcreteDomainContractChecks` 4 → 11 fixture,
`ConcretePresentationChecks` con le righe di tutti gli stati limite, come la scheda); `F3ConcreteFigureChecks` conta 11 figure.

### 1.2 Tracce e disegni della vista storica

`tests/fixtures/f5-sezione` (README nella cartella), suite `ui/check-sezione-historic` (stadio `ui`, 165 s), comando
`--check-sezione-historic[-capture]` della build UiTests. Due catture uguali byte per byte: 37 scenari, 79 826 righe, 120 disegni
2D, 33 righe dei domini 3D.

Ogni traccia scrive, dopo l'apertura e dopo ogni passo, una **parte comune** (byte dei dati e valori cambiati, `Modified`, sola
lettura, calcolo in corso e in coda, risultati, barra di avanzamento, scheda, impronta del risultato esportato, stato, messaggi e
finestre modali) e le **parti dei sottopassi** chieste dallo scenario, ognuna con la sua etichetta (`[ingressi]`, `[azioni]`,
`[domini]`, `[sle]`, `[taglio]`, `[dettagli]`, `[armature]`, `[ispezione]`). Una parte uguale alla precedente è scritta come
«invariata». Gli scenari sono in una cartella per sottopasso:

| Cartella | Scenari | Righe |
| --- | --- | --- |
| `percorso-f3` | le 11 fixture F3: apertura, calcolo dell'apertura, conferma, visita della cattura F3, calcolo, riapertura (tutte le parti) | 20 889 |
| `ingressi` | `geometria-forme`, `barre-manuali`, `materiali-normativa`, `materiali-utente`, `staffe-trefoli`, `resistenze-rapide` | 33 274 |
| `azioni` | `tabelle-storiche`, `incolla`, `editor-sollecitazioni`, `sollecitazioni-uniche` | 5 875 |
| `domini` | `opzioni-domini`, `filtri-tabelle` | 2 573 |
| `sle` | `opzioni-sle`, `fessurazione` | 5 254 |
| `taglio` | `taglio-torsione`, `circolare-senza-staffe` | 3 195 |
| `dettagli` | `dettagli-ancoraggi`, `soletta` | 1 206 |
| `armature` | `ricerca-armature`, `opzioni-ricerca` | 3 002 |
| `ispezione` | `sezione-sigma-epsilon`, `momento-curvatura` | 766 |
| `report` | `disponibilita`, `dati-incompleti` | 1 281 |
| `calcolo` | `ricalcolo-revisione` (motore trattenuto, calcolo superato, «Calcola» durante un calcolo, chiusura), `sola-lettura` | 2 511 |

Tecniche dell'harness (`tests/Desktop/ConcreteSectionHistoricTrace.cs`, `ConcreteSectionHistoricState.cs`):
- **finestre fuori schermo mai attivate**: un aggancio WH_CBT del thread crea ogni finestra fuori schermo e ne rifiuta
  l'attivazione, un aggancio WH_CALLWNDPROC riscrive gli spostamenti; nessun mouse, nessuna tastiera;
- **finestre modali guidate** dentro il loro ciclo con un'operazione del dispatcher («Sollecitazioni della sezione», «Nuovo
  materiale · …»), passo per passo, con lo stato della finestra dopo ogni passo;
- **motore trattenuto**: il cancello dei lotti di `ConcreteAnalysisSession` (`SemaphoreSlim batches`) trattiene il calcolo della
  scheda finché la traccia non lo rilascia, per fissare il calcolo superato e la chiusura durante il calcolo;
- **disegni**: per ogni fixture aperta e calcolata, l'elenco di disegno di `DrawingView.Render` a dimensioni fisse (primitivi, penne,
  riempimenti, testi, impronta dei pixel delle immagini) di 120 viste: sezione (11), mappe SLE (16) e gli otto contorni della Rara
  di due fixture (16), sezione del taglio (11), sezione σ/ε e piano delle deformazioni al punto limite e all'azione (44), dominio 2D
  (11), momento–curvatura (11). Il dominio 3D è confrontato **come dati semplici** (mesh, punti, selezione, resistenze, opzioni,
  senza camera): 33 righe.

La definizione degli scenari (`tests/Shared/ConcreteSectionTrace.cs`) è compilata anche in `Presentation.Tests`: i ViewModel dei
sottopassi ripeteranno gli stessi scenari senza WPF (sezione 4.0).

Non sono nelle tracce: «Copia» e i comandi Excel (appunti e file del sistema), «Proprietà / report…», «Scala…», «Espandi», «PNG»,
i divisori e la navigazione col mouse nei disegni.

### 1.3 Derivazioni in Application

Spostate senza modifiche, chiamate dalla vista storica (passo 3, `df0e4b6f`):
- `src/ANTHEA.Application/Concrete/ConcreteInputPresentation.cs`: campi della geometria e dell'armatura per forma; foro (forme
  ammesse, campi, foro tolto alle altre forme); barre per coordinate e secondi strati (testi, blocco); scelte dei materiali, classe del
  catalogo e suoi valori (la vista li scrive e li mostra ancora uno alla volta), materiali resi «Personalizzato» da un valore
  digitato, materiali salvati, finestra «Nuovo materiale» (catalogo, bozza, nome rifiutato); resistenza passata alla reologia; cavi
  (materiali disponibili con la legge scelta, diametro equivalente e area, modulo del nuovo cavo, nuovo cavo con i suoi rifiuti).
- `src/ANTHEA.Application/Concrete/ConcreteActionPresentation.cs`: riga nuova delle azioni e suo nome, campi delle sollecitazioni
  uniche, righe del taglio, colonne e testo della copia, piano dell'incolla (tutto convalidato prima di cambiare una riga), stati.

I nomi dei membri privati della vista restano (le partial di UiTests e le prove per riflessione li usano). Il taglio usa
`ConcreteSheetPreparation.StoreShearRows`, già in Application e identica alla copia che la vista teneva in due punti. Nessuna regola
di ingegneria nuova: le formule restano nelle librerie (`ReinforcementGeometry` attraverso `ConcreteEditor`).

`ConcreteInputPresentationChecks` (Application.Tests, 56 controlli con valori scritti a mano: 2 095 → 2 151) prova le funzioni;
l'identità della vista è provata dalle tracce e dai disegni congelati prima dello spostamento, dal riferimento F3 e dalle suite CA.

**Baseline F4**, solo spostamenti: le tre voci R4 della vista (`ConcreteWorkspace.BuildControlPanel` e
`ConcreteWorkspace.BuildStressTabs`, `Input.D("fck_mpa")`, una ciascuna; `ConcreteWorkspace.TendonRow`, `values.D("area")`, due)
diventano due voci di `ConcreteInputPresentation` (`RheologyStrength`, `TendonDiameter`). Le due letture di fck della reologia sono
ora la stessa funzione e il diametro del cavo legge l'area una volta: 2 203 → 2 202 voci, 3 342 → 3 340 occorrenze, nessuna
aggiunta. Baseline P3 invariata.

Restano nella vista, per i sottopassi: le derivazioni delle altre schede (domini, SLE, taglio, dettagli, ricerca, ispezione), che
vivono già in gran parte in Application (`ConcreteDomainChart`, `ConcreteResponsePresentation`, `ConcreteDetailingPresentation`,
`ConcreteVerificationSummaries`…) e che i sottopassi chiameranno dal ViewModel.

### 1.4 Corsa finale

`-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` su `1d0d5c69`: `supporto/artefatti/ci/20261017-053643-f510-finale`,
PASS 117 (build 25, fast 31, wiki 2, ui 59), NEW-FAIL 0, nessun file tracciato modificato prima della corsa;
`transitions-f4.json` invariato.
- `ui/check-contrast` è escluso: lo schermo è al 150% (144 DPI) e la baseline è misurata a 96 DPI (decisione D6); il coordinatore
  lo ripete a 96 DPI.
- `ui/f3-equivalence` 87 fixture; `ui/f3-report-chapters` 88 capitoli e 166 immagini; `ui/f3-concrete-figures` 11 figure;
  `ui/check-sezione-historic` 37 scenari, 79 826 righe, 120 disegni e 33 righe 3D uguali ai congelati; Application.Tests 2 151,
  Presentation.Tests 1 552, Architecture.Tests 517 controlli.

## 2. Comportamenti della vista storica trovati

Nessuno è stato corretto; le tracce li fissano così come sono.
1. **Riga evidenziata nelle figure dei domini.** Il report della vista evidenzia la combinazione selezionata nella tabella (la prima
   inserita), il capitolo autonomo la prima per nome. Con righe non in ordine di nome `ui/f3-report-chapters` falliva
   (`word/media/image1.png`); le fixture sono scritte in ordine di nome.
2. **Ordine non deterministico degli errori parziali.** Con due verifiche fermate insieme (direzioni 3D rifiutate: 3D:SLU e 3D:SLV)
   l'ordine in `errori_calcolo`, nello stato e nel report dipende dal thread che finisce prima (catture F3 3 e 4 diverse).
3. **N = +1000 per i documenti senza `dominio2d`.** Un foglio di versione 2 senza `dominio2d` riceve N = +1000 (trazione) dal valore
   predefinito di `SectionWorkspace.Prepare`, pensato per la conversione dei segni della versione 1; con sezioni piccole il dominio 2D
   si ferma. ANTHEA scrive sempre `dominio2d`: solo documenti costruiti a mano.
4. **Messaggio grezzo della libreria nel dominio 2D SLV**: «Value cannot be null. (Parameter 'failureDomain')», con la legge «Stress
   block» (`ingressi/materiali-normativa`) e con N fuori dominio.
5. **La revisione in sola lettura scrive ancora**: scheda selezionata, filtro dei domini, «Colori η», «Forze», contorno SLE, con
   `Modified` (`calcolo/sola-lettura`).
6. **«Annulla» dell'editor delle sollecitazioni cambia i dati** (chiavi di visibilità scritte all'apertura) senza `Modified`
   (`azioni/editor-sollecitazioni`).
7. **L'impronta del risultato esportato cambia dopo la visita delle schede** (`percorso-f3`): l'esportazione sembra dipendere da
   stati costruiti dalle schede quando si aprono (causa non ancora isolata).
8. **Barre per coordinate**: l'armatura guidata è bloccata solo se `barre_manuali` è un elenco, i secondi strati se la chiave c'è,
   qualunque valore (ora visibile in `ConcreteInputPresentation.ManualBars` e `LayersLocked`).
9. **Ricerca delle armature**: rifiuta la riserva torsionale 0 % (valore della fixture `soletta-dettagli-ricerca`) e la sezione in zona
   di sovrapposizione; con i dati della fixture (copriferro 30 mm per un'esposizione XS1 a 100 anni, sovrapposizione corta per Ø16)
   scarta tutte le configurazioni. Lo scenario `ricerca-armature` porta copriferro e sovrapposizione a valori ammissibili.
10. **Verificatore c.a. del palo elastico** (da F5.8): dialogo modale con riscrittura nel palo; `ISheetHost.OpenModuleCopy` non ha
    un canale di ritorno (sezione 5).

## 3. Inventario della vista per file

Membri nominati dalle prove, contati per nome come nell'inventario (per eccesso: nomi brevi come `Mode` o `Section` possono
coincidere con altri). Su 371 membri dichiarati, 165 sono nominati dalle prove esistenti (compreso il nome della classe) e altri 38
solo dalla traccia di F5.10. Nella tabella non sono attribuiti alle prove i nomi generici che coincidono per caso con i loro membri
(`Refresh`, `Render`, `Options`, `Message` in `ElasticPile.UiChecks`).

Le prove arrivano alla vista in due modi: **partial** compilate in UiTests (`supporto/test/Desktop/*.cs`, `tests/Desktop/*.cs`) e
**riflessione** da progetti esterni (`ElasticPile.UiChecks`, `ConcreteInput.Checks`, `tests/ANTHEA.Desktop.Equivalence`).

| File | Righe | Sottopasso | Nominati (prove / solo traccia) | Prove principali |
| --- | ---: | --- | --- | --- |
| `ConcreteWorkspace.cs` | 359 | S1 (azioni S2, riepiloghi S9) | 42 / 3 | ConcreteSmokeChecks 30, ConcreteFeatureChecks 27, ConcreteExtensionSmokeChecks 20, ConcreteWorkspace.Tests 10, ConcreteDesignDesktopChecks 10, ValidationCaptureChecks 10, SectionActionsSmokeChecks 9 e altri 16 file; ElasticPile.UiChecks (costruttore e `materials` per riflessione) |
| `ConcreteLayout.cs` | 44 | S1 | 2 / 0 | ConcreteExtensionSmokeChecks |
| `ConcreteMaterials.cs` | 144 | S1 | 1 / 0 | ConcreteExtensionSmokeChecks |
| `ConcreteMaterialEditor.cs` | 51 | S1 | 1 / 0 | ConcreteFeatureChecks, ContrastAudit, ValidationCaptureChecks |
| `ConcreteTendons.cs` | 66 | S1 | 4 / 0 | ConcreteFeatureChecks 3, ConcreteExtensionSmokeChecks |
| `ConcreteLayers.cs` | 36 | S1 | 1 / 0 | ConcreteFeatureChecks |
| `ConcreteParameters.cs` | 71 | S1 | 4 / 2 | ConcreteFeatureChecks, ConcreteSmokeChecks, ConcreteInput.Checks (`ValidateStirrups`) |
| `ConcreteQuickResistance.cs` | 55 | S1 | 3 / 2 | ConcreteExtensionSmokeChecks, ValidationCaptureChecks |
| `ConcreteSectionProperties.cs` | 54 | S1 | 2 / 0 | ConcreteFeatureChecks |
| `ConcreteTables.cs` | 74 | S1 (filtri comuni) | 3 / 0 | ConcreteSmokeChecks, F3ReportChapterChecks |
| `ConcreteDimensions.cs` | 77 | disegno (S1) | 1 / 0 | smoke CA, asse neutro |
| `ConcreteExchange.cs` | 134 | S2 | 3 / 0 | ConcreteSmokeChecks, ConcreteWorkspace.Tests |
| `SectionActionsView.cs` | 56 | S2 | 2 / 0 | SectionActionsSmokeChecks, AppHarness, ContrastAudit, HorizontalLayoutChecks, SmokeChecks, FieldFormParityChecks |
| `ConcreteDomains.cs` | 314 | S3 (calcolo S1) | 15 / 2 | ConcreteWorkspace.Tests 5, ConcreteFeatureChecks 5, ConcreteSmokeChecks, ContrastAudit, FieldFormParityChecks, ThemeTokenChecks, F3ConcreteFigureChecks |
| `ConcreteViewports.cs` | 555 | disegni (S1, S3, S5, S8) | 20 / 14 | DomainViewport3D.Tests 5, ConcreteSmokeChecks 5, BridgeSmokeChecks 4, ReportAppearanceChecks 3, NeutralAxisSmokeChecks 3 |
| `ConcreteStress.cs` | 180 | S4 | 5 / 1 | ConcreteExtensionSmokeChecks, ConcreteFeatureChecks, ConcreteSmokeChecks, ValidationCaptureChecks, ConcreteInput.Checks |
| `ConcreteRefinements.cs` | 154 | S4 (SLE comuni), S9 (riepiloghi) | 3 / 0 | ConcreteExtensionSmokeChecks, ConcreteSmokeChecks, ConcreteFeatureChecks |
| `ConcreteShear.cs` | 160 | S5 | 10 / 4 | ConcreteSmokeChecks 9, ConcreteExtensionSmokeChecks 5, ValidationCaptureChecks 3 |
| `ConcreteTorsion.cs` | 31 | S5 | 2 / 0 | ConcreteExtensionSmokeChecks |
| `ConcreteDetailing.cs` | 78 | S6 | 11 / 2 | ConcreteExtensionSmokeChecks 9, ValidationCaptureChecks 2 |
| `ConcreteInspection.cs` | 87 | S7 | 5 / 0 | ContrastAudit, FieldFormParityChecks, F3SchedulingChecks, smoke CA |
| `ConcreteCurvature.cs` | 70 | S7 | 4 / 3 | ConcreteExtensionSmokeChecks, F3ReportChapterChecks, ValidationCaptureChecks |
| `ConcreteStrainPlane.cs` | 37 | disegno (S7) | 1 / 0 | ContrastAudit |
| `NeutralAxisOverlay.cs` | 57 | disegno (S7) | 2 / 1 | NeutralAxisSmokeChecks, ThemeTokenChecks |
| `ConcreteDesign.cs` | 230 | S8 | 15 / 3 | ConcreteDesignDesktopChecks 15 |
| `ConcreteReport.cs` | 37 | S9 | 2 / 1 | ConcreteExtensionSmokeChecks, ConcreteFeatureChecks, F3ReportChapterChecks, ReportAppearanceChecks, BridgeSmokeChecks, SmokeChecks |

I membri del guscio (`Data`, `Input`, `settings`, `Commit`, `Busy`, `HasResults`, `Result`, `Invalidate`, `tabs`, `status`) sono
nominati da quasi tutti i 26 file di prova che arrivano alla vista: sono il contratto che le prove esistenti chiedono al foglio.

## 4. Piano dei sottopassi

### 4.0 Come si migra un modulo da 26 file

La vista storica resta la Release finché tutti i sottopassi non sono pronti: una scheda c.a. migrata a metà non si può dare
all'utente. Il ViewModel del foglio cresce un sottopasso alla volta nella build UiTests (`--module-impl str_palo=mvvm`) e a ogni
sottopasso le sue tracce si confrontano con quelle congelate:
- **ViewModel.** `ConcreteSheetViewModel` (`src/ANTHEA.Presentation/Modules/ConcreteSection`) su `SheetViewModelBase`: dati,
  `Modified`, scheda principale (`tab`), `Commit`, `Reload`, sola lettura, chiusura; un ViewModel figlio per sottopasso, creato
  quando il sottopasso è pronto.
- **Calcolo.** La vista non usa `SheetCalculation`: tiene le verifiche da rifare (`pendingCalculations`), le invalida per famiglia
  (3D/2D SLU e SLV, una famiglia SLE, il taglio), accoda il ricalcolo a un giro del dispatcher (nessun ritardo) e calcola con
  `ConcreteAnalysisSession.CalculateBatchAsync` (preparazione unica, passi in parallelo) e poi il taglio sul thread dell'interfaccia.
  Proposta: un coordinatore di Presentation (`ConcreteCalculation`) conserva questa orchestrazione sopra `RecalculationScheduler` e la
  sessione esistente, e pubblica i risultati ai ViewModel figli; il foglio usa la modalità `None` di `SheetViewModelBase` (domanda C1).
  Così S1 dà già la parte comune completa (risultato esportato e stato dipendono da tutte le verifiche).
- **Tracce.** Ogni sottopasso confronta la parte comune e le parti che possiede, negli scenari della sua cartella e in `percorso-f3`;
  le parti dei sottopassi non ancora migrati restano fuori dal confronto (filtro per etichetta). A S9 il confronto è integrale.
- **F3.** Il banco confronta la cattura MVVM con `expected` solo a S9; prima si usa il confronto diretto sui file dei dati
  (`opened`, `visited`, `calculated-data`, `exported-data`, `reopened`).
- **Disegni (Q3).** `ConcreteSectionViewport`, `DomainViewport3D`, `Plot` e `ConcreteStrainPlaneView` restano disegni WPF nel
  code-behind; il ViewModel dà loro dati semplici (le proprietà che oggi la vista assegna). `disegni/elenco.txt` prova che il
  disegno alimentato dal ViewModel resta identico; il 3D si confronta come dati (`disegni/domini-3d.txt`).
- **Prove esistenti.** Le prove che arrivano alla vista (sezione 3) restano sulla vista storica finché è la Release e vengono
  riscritte a S9 sul ViewModel e sulla vista ospitata, nessuna tolta senza sostituta (R3). Ogni sottopasso aggiunge le sue prove in
  `Presentation.Tests`.

**Ordine proposto**: S1 → S2 → (S3, S4, S5 in qualunque ordine) → S6 → S7 → S8 → S9. S1 e S2 portano il guscio, i dati e le azioni,
che tutti gli altri leggono; S7 legge la selezione dei domini (S3); S8 legge i dettagli (S6) e i limiti di tutte le verifiche; S9 per
ultimo, perché il report e i riepiloghi leggono lo stato di tutte le schede.

Per ogni sottopasso: file e membri, prove che li nominano, tracce che lo coprono, confine del ViewModel.

### S1 · Guscio, calcolo, ingressi e materiali

- **File e membri**: `ConcreteWorkspace.cs` (costruttore, schede, stato, avanzamento, `Commit`, `Invalidate`, `InvalidateChecks`,
  `QueueCalculation`, `RefreshPreview`, `BuildControlPanel`, `Dispose`), `ConcreteLayout.cs` (divisori), `ConcreteTables.cs` (filtri e
  ordinamenti comuni a tutte le tabelle), `ConcreteMaterials.cs`, `ConcreteMaterialEditor.cs`, `ConcreteTendons.cs`,
  `ConcreteLayers.cs`, `ConcreteParameters.cs` (coefficienti e staffe), `ConcreteQuickResistance.cs` (scheduler proprio),
  `ConcreteSectionProperties.cs` (finestra «Proprietà / report…»); da `ConcreteDomains.cs` il calcolo (`CalculateAllAsync`,
  `RunAnalysis`, `Step`, `analysisSession`); disegno della sezione (`ConcreteSectionViewport`, `ConcreteDimensions.cs`).
- **Prove che li nominano**: quasi tutte (sezione 3); in particolare ConcreteSmokeChecks, ConcreteFeatureChecks,
  ConcreteExtensionSmokeChecks, ConcreteWorkspace.Tests, ValidationCaptureChecks, F3SchedulingChecks e F3TypedSchedulingChecks (code
  e revisione del calcolo), FieldFormParityChecks, ElasticPile.UiChecks (costruttore e campo `materials` per riflessione).
- **Tracce**: parte comune di tutti gli scenari; `[ingressi]` di `ingressi/*` e `percorso-f3/*`; `calcolo/*`; disegni «sezione».
- **Confine**: `ConcreteSheetViewModel` (guscio, schede, stato, avanzamento) e `ConcreteInputViewModel`: un `FormViewModel` per
  blocco (normativa e nota, coefficienti, materiali con i gruppi, geometria, foro, armatura, secondi strati, staffe, nuovo cavo,
  resistenze rapide), tabelle delle barre e dei cavi (`JsonRowCollection`), materiali salvati, finestra «Nuovo materiale» come
  `IDialogViewModel` (`IDialogService`), proprietà della sezione disegnata (`Section`, `Tendons`, `Message`, assi, etichette, quote,
  barra selezionata). Le derivazioni sono quelle del passo 3. Coordinatore del calcolo (C1). Reologia: la finestra di F5.5.

### S2 · Azioni

- **File e membri**: da `ConcreteWorkspace.cs` le azioni (`actions`, `CreateAction`, `SyncActions`, `InvalidateActions`, `ActionTable`,
  `UnifiedActions`, `ActionSetLabel`); `ConcreteExchange.cs` (copia, incolla, Excel, `OpenActionEditor`); `SectionActionsView.cs`.
- **Prove**: ConcreteSmokeChecks (`PasteCells`, `RememberExcel`), ConcreteWorkspace.Tests, SectionActionsSmokeChecks (15 membri),
  `SectionActionsEditorChecks` (Application).
- **Tracce**: `[azioni]` di `azioni/*` e `percorso-f3/*`; l'isola modale in `azioni/editor-sollecitazioni`.
- **Confine**: `ConcreteActionsViewModel`: una `JsonRowCollection` per famiglia (SLU, SLV, SLE, SLE_FREQ, SLE_QP, taglio), «+ Riga»,
  «Duplica», «− Riga», incolla con `ConcreteActionPresentation.Plan`, copia con un servizio degli appunti, Excel con
  `IFileDialogService`, editor delle sollecitazioni con `IDialogService`; dopo l'editor, `Reload` al posto di `ReloadRequested`.
  `SectionActionsViewModel` passa in Presentation (Q17 a, domanda C3). Le tabelle si mostrano nei pannelli di S3, S4 e S5.

### S3 · Domini 3D e 2D

- **File e membri**: `ConcreteDomains.cs` (`DomainPanel`, `RefreshDomainPanel`, `PublishDomain`, `UpdateSelection`, `AttachDomainRows`,
  `ReadAction`); da `ConcreteViewports.cs` `DomainViewport3D` e `DomainWireOverlay`; il grafico 2D (`Plot`).
- **Prove**: ConcreteWorkspace.Tests, ConcreteFeatureChecks, ConcreteSmokeChecks, DomainViewport3D.Tests, F3ConcreteFigureChecks,
  ContrastAudit, ThemeTokenChecks.
- **Tracce**: `[domini]` di `domini/*` e `percorso-f3/*`; disegni «dominio 2D»; `disegni/domini-3d.txt`.
- **Confine**: `DomainPanelViewModel` (3D e 2D): form delle opzioni, tabella con colonne visibili, filtro, ordinamento e selezione,
  dettaglio, riepilogo, comandi e caselle della barra, cursori; dati del 3D (mesh, punti, selezione, opzioni) e serie del 2D
  (`ConcreteDomainChart`). Il viewport 3D resta WPF.

### S4 · SLE e tensioni

- **File e membri**: `ConcreteStress.cs` (`StressPanel`, `PublishStress`, `UpdateStressSelection`, `sleTabs`), da
  `ConcreteRefinements.cs` `SynchronizeSharedSle` e `tendonOnlyControls`.
- **Prove**: ConcreteExtensionSmokeChecks, ConcreteFeatureChecks, ConcreteSmokeChecks, ValidationCaptureChecks, FieldFormParityChecks,
  ConcreteInput.Checks.
- **Tracce**: `[sle]` di `sle/*` e `percorso-f3/*`; disegni «SLE» (16 mappe e 16 contorni).
- **Confine**: `StressPanelViewModel` per famiglia (Rara, Frequente, Quasi permanente) con le opzioni comuni: form, tabella, schede,
  dettaglio, passaggi della fessurazione, tabelle delle barre e del calcestruzzo, zone efficaci, proprietà della mappa e legenda.

### S5 · Taglio e torsione

- **File e membri**: `ConcreteShear.cs` (`shearForm`, `shearGrid`, `ShearRow`, `CalculateShear`, `UpdateShearSelection`,
  `RefreshAutomaticShear`), `ConcreteTorsion.cs`; staffe in sola lettura (da S1).
- **Prove**: ConcreteSmokeChecks (9 membri), ConcreteExtensionSmokeChecks, ValidationCaptureChecks, ConcreteWorkspace.Tests,
  SectionActionsSmokeChecks.
- **Tracce**: `[taglio]` di `taglio/*` e `percorso-f3/*`; disegni «taglio».
- **Confine**: `ShearViewModel`: form del taglio, della torsione e delle staffe (sola lettura), nota dei parametri automatici, tabella
  e comandi, dettaglio, sintesi, riquadri, sezione disegnata. Il calcolo del taglio (`ConcreteShearBatch.Calculate`) oggi gira sul
  thread dell'interfaccia dentro il lotto (domanda C2).

### S6 · Dettagli costruttivi e ancoraggi

- **File e membri**: `ConcreteDetailing.cs` (`detailingForm`, `coverDetailingForm`, `anchorageForm`, `RefreshDetailing`,
  `detailingTopics`, `anchorageResult`).
- **Prove**: ConcreteExtensionSmokeChecks (9 membri), ValidationCaptureChecks.
- **Tracce**: `[dettagli]` di `dettagli/*` e `percorso-f3/*`.
- **Confine**: `DetailingViewModel`: form dei dettagli, del copriferro e degli ancoraggi, riquadri del riepilogo, dei temi e degli
  ancoraggi, nota della durabilità (`ConcreteDetailingPresentation`). Si ricalcola a ogni modifica e a ogni cambio di scheda, come
  `RefreshDetailing`.

### S7 · Ispezione e momento–curvatura

- **File e membri**: `ConcreteInspection.cs` (`UpdateSectionInspection`, una `ConcreteInspectionSession` per pannello),
  `ConcreteCurvature.cs` (scheduler proprio), disegni `ConcreteStrainPlane.cs` e `NeutralAxisOverlay.cs`.
- **Prove**: ContrastAudit, FieldFormParityChecks, F3SchedulingChecks, NeutralAxisSmokeChecks, ConcreteExtensionSmokeChecks,
  F3ReportChapterChecks (`curvatureResult`).
- **Tracce**: `[ispezione]` di `ispezione/*` e `percorso-f3/*`; disegni «ispezione», «piano» e «momento–curvatura».
- **Confine**: `SectionInspectionViewModel` (3D e 2D: scheda, stato, messaggio, valori, mappa, piano delle deformazioni, asse neutro)
  e `CurvatureViewModel` (form, testo, serie del grafico), ognuno con la sua sessione o il suo scheduler esistente.

### S8 · Progetto delle armature

- **File e membri**: `ConcreteDesign.cs` (`RunDesignAsync` con annullamento, `designForm`, `designRanking`, `designSelection`,
  `ApplyDesign`, `designStale`, `designPreview`).
- **Prove**: ConcreteDesignDesktopChecks (15 membri di questo file, `ui/check-concrete-design`).
- **Tracce**: `[armature]` di `armature/*` e `percorso-f3/*` (ricerca con configurazioni scartate e ammissibili, alternative,
  applicazione).
- **Confine**: `DesignViewModel`: form della ricerca, dei limiti e delle prime alternative, comando asincrono annullabile con
  avanzamento, stato e scarti, proposta, ambito, riquadri, alternative, anteprima (sezione disegnata), «Applica configurazione» che
  scrive nei dati e invalida come oggi.

### S9 · Report, riepiloghi e passaggio della Release

- **File e membri**: `ConcreteReport.cs` (`BuildReport`, `BuildShortReport`, `ExportReport`), da `ConcreteRefinements.cs`
  `CollectVerificationSummaries`, da `ConcreteWorkspace.cs` i riepiloghi del pannello di controllo (`summaries`, `RefreshSummary`).
- **Prove**: F3ReportChapterChecks, ReportAppearanceChecks, ConcreteFeatureChecks, ConcreteExtensionSmokeChecks, `ui/f3-report-chapters`.
- **Tracce**: `report/*` (report completo e breve, con calcolo in coda o bloccato); `percorso-f3/*` integrale.
- **Confine**: il ViewModel dà al report i dati di `ConcreteReportAnalysis`/`ModuleReportService`; i report sono congelati dall'8/10
  e non cambiano. Poi il passaggio (sezione 6).

## 5. Palo elastico (F5.8) e muri (F5.9)

**Muri.** Il ViewModel dei muri (F5.9) manda la sezione con `ISheetHost.OpenModuleCopy("str_palo", dati, nome)`: la finestra apre un
foglio del progetto o una finestra nuova con una copia dei dati, senza ritorno, come oggi. Dopo S9 il foglio aperto è il ViewModel
della sezione, attraverso `ModuleImplementations`, senza modifiche ai muri. `MainWindow.OpenWallConcreteSection` e
`ISheetEditor.ConcreteSectionRequested` diventano superflui al passaggio dei muri (`OpenModuleCopy` resta). Prova da aggiungere a S1:
il ViewModel della sezione apre i dati prodotti dalla richiesta dei muri (dalle tracce di F5.9) come la vista storica.

**Palo elastico.** Il verificatore c.a. di un tratto non è una copia: un dialogo modale ospita la sezione sul foglio del tratto
(`ElasticPileEditor.CreateConcreteSheet`), «Applica armatura…» porta l'armatura nel palo, la chiusura conserva il foglio in
`foglio_cls`, e dopo l'editor delle sollecitazioni la sezione si ricarica nel dialogo. `OpenModuleCopy` non ha questo canale di
ritorno (domanda E1 di F5.8). Proposta per F5.10 (domanda C4):
- un dialogo di Presentation, `ConcreteSectionDialogViewModel : IDialogViewModel<JsonObject>`, ospita il ViewModel della sezione
  creato su un `SheetContext` con il foglio del tratto (copia di lavoro) e un ospite interno (le richieste di report e di copia
  passano all'ospite del palo);
- «Applica armatura…» chiama `Commit` del foglio e poi `PileConcreteVerifier.Apply` del palo con i dati del ViewModel; la chiusura
  restituisce i dati, che il palo conserva in `foglio_cls` (`PileConcreteVerifier.Close`); `Reload` sostituisce `ReloadRequested`;
- fino a S9 l'ospite desktop mostra nello stesso dialogo la `ConcreteWorkspace` storica, come oggi; dopo S9 la vista della sezione
  ospitata da `IDialogService`.

Così `ISheetHost` resta uguale per tutti i moduli e il ritorno vive dove serve, nel dialogo. Requisito per S1: il ViewModel della
sezione deve funzionare senza progetto e senza finestra (ospite interno, nessun `RequestReport` obbligatorio), e `Commit` deve
lasciare i dati completi nel `JsonObject` del contesto.

## 6. Che cosa resta

1. **Punto di controllo dell'utente** su questa nota (sezione 7).
2. **Sottopassi S1–S9** come nella sezione 4, ognuno con ViewModel, prove in `Presentation.Tests` sulle tracce della sua cartella,
   derivazioni residue spostate in Application con la baseline F4 aggiornata nello stesso commit.
3. **Vista XAML** `X.Desktop/Views/ConcreteSection/…`, solo con i token di F5.3, per sottopasso o alla fine, con schermate fuori
   schermo e `--check-contrast`.
4. **Banco e confronto diretto** con `--module-impl str_palo=mvvm`, poi il confronto con `expected` e le transizioni F4.
5. **Prove riscritte** sul ViewModel e sulla vista ospitata (sezione 3), compreso `ElasticPile.UiChecks` per la parte c.a.
6. **Passaggio della Release e ritiro della vista storica (Q11)**, nello stesso commit: `ModuleImplementations.Declared` per
   `str_palo`; i 26 file in `supporto/SUPERATI/ui-storica/sezione-ca/` tranne i disegni; in `SheetEditor` il campo `concrete` e i suoi
   rami; il verificatore del palo elastico sul dialogo nuovo (sezione 5); le tracce restano per `Presentation.Tests`, la suite
   `ui/check-sezione-historic` e i file dell'harness seguono la vista.
7. **Unione nel ramo di integrazione.** `refactoring/f5` ha già unito F5.8 (cinque fixture F3) e F5.9 (dodici): all'unione di questo
   ramo `expected/esito.txt` e il manifest vanno ricomposti (numero delle fixture somma delle tre aggiunte; la sezione
   `fixture_aggiunte_f5_10` concatenata dopo le loro) e le suite F3 ripetute.

## 7. Domande per il punto di controllo F5.10

- **C1 · Calcolo del foglio.** La vista calcola con un'orchestrazione propria (verifiche da rifare per famiglia, un giro del
  dispatcher, lotto con passi in parallelo, poi il taglio). Proposta: coordinatore in Presentation sopra `RecalculationScheduler` e
  `ConcreteAnalysisSession`, foglio in modalità `None` di `SheetViewModelBase`; in alternativa una quarta modalità del componente
  comune. Quale?
- **C2 · Taglio sul thread dell'interfaccia.** `ConcreteShearBatch.Calculate` gira oggi sul thread dell'interfaccia dentro il lotto.
  Proposta: conservarlo in F5.10 (come Q8 a), per tracce identiche; spostarlo in seguito come differenza dichiarata. Da confermare.
- **C3 · `SectionActionsViewModel` in Presentation** (Q17 a) a S2, con il suo controllo di Application; resta in Application
  `PriceCatalogueViewModel` fino a F5.12 (e con lui CommunityToolkit). Da confermare.
- **C4 · Verificatore c.a. del palo elastico** (con E1 di F5.8): dialogo di Presentation che ospita il ViewModel della sezione e
  restituisce i dati (sezione 5), oppure un membro nuovo di `ISheetHost`, oppure la copia senza ritorno come differenza dichiarata.
- **C5 · Comportamenti storici 3–9** (sezione 2): il ViewModel li conserva per avere tracce identiche, e diventano voci del registro
  da decidere al passaggio (S9)? In particolare la sola lettura che scrive (5) e «Annulla» che cambia i dati (6).
- **C6 · Ordine degli errori parziali (2) e riga evidenziata (1).** Proposta: il ViewModel scrive gli errori nell'ordine fisso delle
  verifiche (differisce dalla vista solo quando oggi l'ordine è casuale) e conserva la riga evidenziata della vista (selezionata).
- **C7 · Peso delle tracce.** 37 scenari, 79 826 righe, 7,3 MB; la suite storica impiega 165 s. Nel ViewModel le stesse tracce
  calcolano con GPC Engine: proposta `Presentation.Tests` (stadio `fast`) con le cartelle dei sottopassi e tre fixture di
  `percorso-f3`, il resto in una suite dello stadio `ui` o `full`. Si accetta?
- **C8 · Disegni (Q3).** I quattro disegni restano WPF alimentati dal ViewModel; il viewport 3D si confronta solo come dati, senza
  elenchi di disegno. Si accetta?
- **C9 · Copertura del riferimento F3.** Trefoli (la curva M–χ con trefoli non è disponibile) e calcolo fermato per intero sono
  solo nelle tracce, non nelle fixture. Si accetta, o si chiede alla libreria di dare la curva con trefoli?
- **C10 · Strategia per sottopassi.** Release storica fino a S9; ViewModel che cresce in UiTests; prove esistenti riscritte solo a S9.
  Si conferma, o si preferisce una vista XAML per sottopasso ospitata dentro la vista storica?

**Materiale da mostrare**: questa nota; le tracce (`tests/fixtures/f5-sezione`) e un estratto per sottopasso; i disegni congelati;
la corsa finale.

## 8. S1 fatto (18/10)

Sottopasso S1 «Guscio, calcolo, ingressi e materiali» (sezione 4, S1 e 4.0), ramo `refactoring/f5-10-s1` da `refactoring/f5`
`591272d8`, notte del 18/10/2026. Decisioni applicate: C1 (coordinatore in Presentation, foglio in modalità `None`), C2 (taglio sul
thread dell'interfaccia), C4 (il ViewModel funziona senza progetto e senza finestra), C5 (comportamenti storici conservati), C6 (ordine
fisso degli errori), C7 (tracce lunghe nello stadio `ui`), C8–C10. La Release resta storica; nulla va all'utente prima di S9.

### 8.1 Commit

| Commit | Contenuto |
| --- | --- |
| `1fa75d05` | derivazioni del guscio in Application (`ConcreteSheetPresentation`: scheda iniziale, divisori); baseline F4 spostata |
| `68668f1b` | ViewModel S1 in `src/ANTHEA.Presentation/Modules/ConcreteSection`, modulo registrato (Release storica) |
| `e763af44` | prove in `Presentation.Tests`, sezioni dei muri e loro tracce storiche, suite `Presentation.Tests/sezione-ca`, correzione di `Reload` |
| (questo commit) | questa sezione, esito della corsa finale |

### 8.2 Che cosa c'è

- **`ConcreteSheetViewModel`** su `SheetViewModelBase` in modalità `None`: all'apertura prepara i dati con le stesse scritture e nello
  stesso ordine della vista storica (costruttore e costruzione di ogni scheda: coefficienti, parametri, fori, materiali, ala, trefolo
  predefinito, materiali salvati, strati, staffe, `layout`, resistenze rapide, omogeneizzazione e contorno di ogni famiglia SLE,
  parametri automatici del taglio, `calcola_armature`, poi l'anteprima). Scheda principale (`SelectedTab`, salvata in `tab` con Commit,
  dettagli e Modified, anche in sola lettura: C5), stato, avanzamento, `Commit` (form, righe delle azioni e del taglio nei dati del
  contesto), `Reload` come un foglio nuovo, sola lettura con `IsReadOnly` su ogni form e sui comandi, chiusura.
  `Result` è l'esportazione storica (`ConcreteResultExport`) costruita quando la si legge e conservata fino all'azzeramento successivo,
  come la vista storica (vedi 8.4, punto 1).
- **`ConcreteCalculation`** (C1): l'orchestrazione storica sopra `RecalculationScheduler` e `ConcreteAnalysisSession`; verifiche da rifare
  per famiglia, ricalcolo in coda su un giro del thread dell'interfaccia, `CalculateBatchAsync`, poi il taglio sul thread dell'interfaccia
  (C2); stessi stati e testi dello stato. C6: gli esiti dei passi paralleli di una fase (i domini, poi le tensioni) si applicano insieme,
  quando arriva l'ultimo della fase, nell'ordine fisso `3D:SLU, 2D:SLU, 3D:SLV, 2D:SLV, SLE, SLE_FREQ, SLE_QP, Taglio`.
- **`ConcreteInputViewModel`**: un `FormViewModel` per blocco (normativa e nota, coefficienti, materiali con i gruppi, geometria, foro,
  armatura, tre secondi strati, staffe e schema, nuovo cavo, trefolo standard, resistenze rapide), tutti con `WikiNavigator` dei servizi,
  conferma all'uscita dal campo e numeri formattati come la vista storica; tabelle delle barre e dei cavi (`ConcreteTableViewModel` su
  `JsonRowCollection`, con i filtri e gli ordinamenti comuni di `ConcreteTables.cs` e la selezione che segue l'elemento corrente della
  vista di raccolta storica); materiali salvati e trefolo standard (`ConcreteChoiceViewModel`); gruppi del pannello (aperti o chiusi come
  oggi); resistenze rapide con il loro scheduler; dati semplici del disegno della sezione (`ConcreteSectionDrawingViewModel`, Q3 e C8).
- **«Nuovo materiale»**: `ConcreteMaterialDialogViewModel` (`DialogViewModel<JsonObject>`) su `IDialogService`: bozza, form, curve della
  libreria, valori derivati, errore, «Salva materiale e applica».
- **Finestre non modali**: «Reologia del calcestruzzo…» (evento `RheologyRequested` con fck) e «Proprietà / report…»
  (`ConcreteSectionPropertiesViewModel`, evento `PropertiesRequested`: φ e n scritti in `proprieta_sezione` con Modified, esportazione TXT
  e copia). Senza vista non si apre nulla (domanda S1-a).
- **Parti dei sottopassi successivi**: finché i loro ViewModel non esistono, il foglio conserva solo quello che la parte comune delle
  tracce legge: righe delle azioni (una `JsonRowCollection` per famiglia, `SyncActions`, `InvalidateActions`, uscite dei domini e delle
  tensioni), righe e risultati del taglio e della torsione, parametri automatici del taglio, valori derivati dei dettagli, riepiloghi del
  pannello di controllo (`Summaries`, `ShearDashboard` su `ConcreteCardsViewModel`). File `ConcreteSheetViewModel.Checks.cs`.
- **Modulo**: `ConcreteSectionSheetModule` (chiavi di stato S1 `tab`, `layout`, `quote_dimensioni`, `quote_copriferro`,
  `quote_interferro`; report breve dichiarato) registrato in `DesktopComposition.CreateModules`. Nessuna vista XAML in S1 (sezione 4.0: la
  vista per sottopasso o alla fine, domanda S1-d): con `--module-impl str_palo=mvvm` il banco seleziona il modulo e rifiuta l'apertura
  («Vista del foglio non registrata»), come per la sonda senza DataTemplate.
- **Report**: rinviato a S9; `BuildReport` rifiuta con un messaggio.
- **Componenti comuni**, aggiunte additive: `SheetViewModelBase.Result`, `HasResults` e `CalculateAsync` virtuali;
  `FieldViewModel.ReplaceChoices` (le classi dei materiali dopo il cambio di normativa, come il nuovo ItemsSource della combo storica).
- **Application**: `ConcreteSheetPresentation` (scheda iniziale, divisori), chiamata anche dalla vista storica.

### 8.3 Esiti

- **Tracce** (`ConcreteSectionSheetChecks`, parte comune e `[ingressi]`; le parti degli altri sottopassi escluse per etichetta): 21
  scenari uguali alle tracce congelate della vista storica, `ingressi/*` (6), `percorso-f3/*` (11), `calcolo/ricalcolo-revisione` (motore
  trattenuto con il cancello dei lotti della sessione), `muri/*` (2). `calcolo/sola-lettura` è uguale fino al passo «scelta 3D filtro
  «Fuori dominio»», il primo passo di un sottopasso non migrato (S3) con effetti sulla parte comune: lì il confronto si ferma, e il punto
  d'arresto è esso stesso controllato (un arresto altrove è un fallimento). I passi degli altri sottopassi non fanno nulla nel ViewModel.
- **File dei dati F3** (sezione 4.0, confronto diretto): per le 11 fixture `opened`, `visited`, `calculated-data`, `exported-data` e
  `reopened` fatti dal ViewModel come li fa la cattura F3 sono uguali byte per byte al riferimento, tranne `report_sezioni` che scrive
  l'esportazione del report (S9), tolta da `exported-data` e `reopened`: 55 file.
- **Muri (sezione 5)**: il ViewModel dei muri, nello scenario `risultati-sezione` di F5.9, manda al modulo c.a. due sezioni con nomi e
  impronte uguali alla traccia congelata dei muri; i loro dati sono conservati in `tests/fixtures/f5-sezione/muri` (provati uguali a
  quelli prodotti, a meno degli identificativi nuovi) e due scenari `muri/…` li aprono come la richiesta dei muri: tracce della vista
  storica catturate il 18/10 su `68668f1b` (due catture uguali), ViewModel uguale.
- **Prove rapide** (`Presentation.Tests`, stadio `fast`): tre scenari (`calcolo/ricalcolo-revisione`, `ingressi/resistenze-rapide`,
  `percorso-f3/default`), contratto del modulo, foglio senza finestra e senza richieste all'ospite con Commit nei dati del contesto, sola
  lettura, chiusura durante il calcolo, ordine fisso degli errori di due domini 2D fermati insieme (C6, tre aperture uguali), F1 di ogni
  campo di ogni form verso il navigatore registrato, finestre non modali, Reload.
- **Tracce lunghe** (C7): suite `Presentation.Tests/sezione-ca` nello stadio `ui` (`--sezione-tracce`, circa 45 s): tutte le tracce S1, i
  file dei dati F3, le sezioni dei muri.
- **Baseline**: F4 solo spostamenti (2185 voci, 3320 occorrenze invariate; `settings.D("tab")` e `settings["layout"].D(group, initial)`
  passano a `ConcreteSheetPresentation`); P3 invariata (nessuno stato statico mutabile in Presentation).
- `ui/check-sezione-historic`: 39 scenari (37 + 2 `muri`), 84 832 righe, disegni invariati.
- **Corsa finale**: `-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` su `e763af44`:
  `supporto/artefatti/ci/20261018-020253-f510s1-finale`, PASS 127 (i 126 della base più `Presentation.Tests/sezione-ca`), NEW-FAIL 0, circa
  45 minuti; nessun file tracciato modificato durante la corsa. `ui/check-contrast` escluso (schermo al 150%, D6); nessun passo di contrasto
  aggiunto, perché S1 non ha una vista. Presentation.Tests 1 955 controlli, Architecture.Tests 524, Application.Tests 2 660;
  `ui/check-sezione-historic` 39 scenari, 84 832 righe, 120 disegni e 33 righe 3D uguali ai congelati; `ui/f3-equivalence` 104 fixture;
  `ui/check-module-host-offscreen` con la sezione registrata.

### 8.4 Comportamenti storici trovati in S1 (conservati, C5)

1. **Causa del comportamento 7** (sezione 2): l'esportazione si costruisce quando la si legge e resta in cache finché non la azzera un
   aggiornamento dei dettagli (ogni cambio di scheda, ogni modifica), una curva M–χ o un calcolo; `Commit` cambia i dati (righe delle
   azioni con `visible`) ma non la azzera. Così l'impronta cambia al primo cambio di scheda dopo la conferma (la visita). Il ViewModel fa
   lo stesso.
2. **Trefolo standard invalidato due volte**: scegliere il trefolo standard (o applicare un materiale trefolo salvato) invalida il foglio
   due volte (`TendonsChanged` e poi `Apply`): due Modified.
3. **Selezione delle tabelle**: segue l'elemento corrente della vista di raccolta (la prima riga è selezionata quando la vista nasce
   sopra righe già presenti, dopo «−» passa alla riga al suo posto o all'ultima).
4. **Artefatto delle tracce, non della finestra**: WPF non passa lo stato disattivato di un form ai campi di un gruppo chiuso finché il
   gruppo non è stato disposto. Nella traccia storica (finestra fuori schermo, gruppo «Armature» chiuso) `bottom_bar_count` si scrive con
   le coordinate delle barre attive (`ingressi/barre-manuali`); dopo la visita i campi risultano spenti. Il driver delle prove lo
   riproduce (stato «disposto» dei gruppi); il ViewModel espone `ReinforcementEnabled` e `FormEnabled` degli strati. Da ricordare per le
   prove della vista XAML.

### 8.5 Domande «da decidere» (scelte provvisorie, da rivedere in F5.15)

Ogni voce è «da decidere»: la scelta indicata vale per andare avanti.

- **S1-a · Finestre non modali.** «Reologia…» e «Proprietà / report…» sono richieste alla vista con eventi del ViewModel
  (`RheologyRequested`, `PropertiesRequested`); non c'è un servizio comune di finestre non modali. Scelta prudente: nessun componente
  comune nuovo. Alternativa: un servizio in `PresentationServices`.
- **S1-b · «Calcola» dell'ospite.** `MvvmSheetEditor.CalculateAsync` conferma il foglio prima di calcolare; `SheetEditor` per la sezione
  storica no. La conferma scrive le righe delle azioni e del taglio, che il calcolo legge comunque: nessun effetto sulle tracce. Da
  decidere al passaggio (S9): conservare o dichiarare.
- **S1-c · C6 per fasi.** Un calcolo annullato a metà di una fase non pubblica gli esiti già arrivati di quella fase (restano da
  calcolare; la cache della sessione li rende rapidi). Si vede solo con il motore trattenuto; le tracce sono uguali.
- **S1-d · Vista XAML.** S1 non ne scrive. Proposta: una vista unica dopo S9 (o per sottopasso, se l'utente vuole provare prima la
  scheda degli ingressi); fino ad allora `--module-impl str_palo=mvvm` rifiuta l'apertura.
- **S1-e · Chiavi di stato.** Dichiarate per S1 (`tab`, `layout`, `quote_*`); i sottopassi aggiungono le loro (filtri, contorni,
  opzioni dei grafici).

### 8.6 Da dove parte S2

- Le righe delle azioni sono già nel foglio: `actions` (una `JsonRowCollection` per famiglia, in `ConcreteSheetViewModel.Checks.cs`) con
  `CreateAction`, `SyncActions`, `InvalidateActions` e le uscite dei domini (`RefreshDomainOutputs`) e delle tensioni; le righe del taglio
  in `shearRows`. S2 le espone con `ConcreteActionsViewModel` (sezione 4, S2), aggiunge «+ Riga», «Duplica», «− Riga», incolla
  (`ConcreteActionPresentation.Plan`, con `synchronizing` come la vista storica), copia (servizio degli appunti), Excel
  (`IFileDialogService`) e l'editor delle sollecitazioni su `IDialogService`, con `Reload` al posto di `ReloadRequested`.
- C3: `SectionActionsViewModel` è ancora in Application; S2 la sposta in Presentation con il suo controllo di Application.
- Tabelle: `ConcreteTableViewModel` ha già `BaseFilter` (il filtro degli esiti dei domini), `Attach` (lo stato SLU/SLV) e la barra dei
  filtri; i messaggi dello stato di copia e incolla chiedono un metodo interno per scrivere `Status`.
- Prove: in `ConcreteSectionSheetChecks` aggiungere `azioni` a `OwnParts`, i passi delle tabelle (cella, riga, incolla, finestra
  «sollecitazioni») e gli scenari `azioni/*`; le tabelle delle azioni vivono nei pannelli dei domini, delle SLE e del taglio, quindi
  molti passi di `azioni/*` toccano S3–S5: usare la stessa regola d'arresto (`ExpectedStops`).
- Il driver delle tracce e i controlli rapidi sono in `tests/ANTHEA.Presentation.Tests/ConcreteSectionSheetChecks*.cs`.
- **Legame con S3–S5, perché S2 non è partito il 18/10.** I passi di `azioni/*` agiscono sulle tabelle dei pannelli: la tabella del
  dominio 3D (filtro degli esiti e ordine per nome della vista storica, selezione di `AttachDomainRows`), quelle delle SLE e quella del
  taglio. Con le sole tabelle delle azioni di S2 (un modello di tabella per pannello, con il filtro dei domini come funzione di
  `ConcreteDomainChart.Includes`) si confrontano per intero `incolla`, `editor-sollecitazioni`, `sollecitazioni-uniche` e la parte
  `[azioni]` di `percorso-f3`; `tabelle-storiche` si ferma a «menu 3D stato «SLV»», scelta del form del dominio (S3). Stima: circa sei
  ore con la corsa finale, troppe per chiudere S2 nella stessa notte senza rischiare di lasciarlo a metà. Proposta: S2 insieme alle
  tabelle dei pannelli (il modello di tabella di S3–S5), oppure S2 prima con l'arresto controllato su `tabelle-storiche`.

### 8.7 Come riprendere

```
dotnet build tests/ANTHEA.Presentation.Tests/ANTHEA.Presentation.Tests.csproj
dotnet tests/ANTHEA.Presentation.Tests/bin/Debug/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-rapide
dotnet tests/ANTHEA.Presentation.Tests/bin/Debug/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-tracce
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)' -Tag <tag>"
```

## 9. Riallineamento 21/10 (S2, parte a)

Ramo `refactoring/f5-10-s2`, da `refactoring/f5` `c7fe03dd` (set ufficiale AE in `lib/Checker`), 21/10/2026. Dopo S1 la sezione c.a.
è cambiata il 20/10 in tre lavori fuori da F5: persistenza nativa GPC con formato 3 ([sezioni-persistenza.md](sezioni-persistenza.md)),
dodici wizard delle tipologie ([sezioni-wizard.md](sezioni-wizard.md)), capsula con armature per tratti e dettagli del pilastro
([capsula-pilastro.md](capsula-pilastro.md)). Prima di S2 si è controllato che le tracce congelate e il ViewModel S1 valgano ancora,
e il piano dei sottopassi è stato aggiornato.

### 9.1 Esito dei controlli

| Controllo | Esito |
| --- | --- |
| `ui/check-sezione-historic`: la vista storica di oggi contro `tests/fixtures/f5-sezione` | PASS: 39 scenari, 84 832 righe, 120 disegni 2D e 33 righe dei domini 3D uguali ai congelati |
| `Presentation.Tests/sezione-ca`: il ViewModel S1 contro le stesse tracce | PASS, 30 controlli: 21 scenari uguali, `calcolo/sola-lettura` uguale fino all'arresto atteso; file dei dati F3 e sezioni dei muri uguali |

Corsa `supporto/artefatti/ci/20261021-074153-s2a-verifica` su `c7fe03dd`. **Nessuna traccia da ricatturare**: `tests/fixtures/f5-sezione`
resta com'è.

### 9.2 File e membri cambiati dopo S1

Vista storica (`X.Desktop/Wpf`), commit `cf080886` (wizard), `810b62a6` (campi del foro, F5-REL-112-01) e `2ce55493` (capsula):

| File | Membri | Che cosa |
| --- | --- | --- |
| `ConcreteWorkspace.cs` | campi `templateGeometryNote`, `templateReinforcementNote`; `BuildControlPanel` | nel gruppo «Geometria» il pulsante «Wizard tipologie / modifica disposizione…» e la nota; nel gruppo «Armature» la nota della disposizione da wizard |
| `ConcreteWorkspace.cs` | `refreshHoleControls` | «Foro centrale» spento per le sezioni da wizard; i campi del foro rileggono `input` e sono spenti per le sezioni da wizard |
| `ConcreteWorkspace.cs` | `OpenTemplateWizard` (nuovo) | `Commit`, finestra modale `ConcreteTemplateWizardWindow`; dopo «Applica sezione»: voce nuova in «Sezione», form della geometria, dell'armatura, degli strati e del taglio riletti, foro, `Invalidate` |
| `ConcreteWorkspace.cs` | `RefreshPreview` | geometria e armatura nascoste e spente per le sezioni da wizard, note mostrate, suggerimento dell'armatura |
| `ConcreteLayers.cs` | `RefreshAdditionalLayers` | secondi strati nascosti per le sezioni da wizard |
| `ConcreteParameters.cs` | `SynchronizeStirrups` | campi della staffa circolare (`tipo_staffa`, `rami_interni`, `schema_interno`, `rotazione_staffa`) nascosti per le sezioni da wizard |
| `ConcreteShear.cs` | `RefreshAutomaticShear` | con parametri manuali la nota è `taglio.nota_wizard`, se c'è |
| `ConcreteViewports.cs` | `ConcreteSectionViewport.DrawStirrups` | disegna i percorsi chiusi delle staffe del wizard (`ConcreteGeometry.TemplateLinks`) |
| `ConcreteTemplateWizardWindow.cs` (nuovo) | | finestra WPF che ospita `ConcreteTemplateWizardViewModel` e disegna l'anteprima |

Tutti i cambiamenti dipendono da `ConcreteTemplateAdapter.Uses(input)` (forma avanzata, oppure foro con `wizard_layout` «Doppio contorno»), da
`taglio.nota_wizard` o da `TemplateLinks`, tranne la rilettura dei campi del foro.

Sotto la vista, senza effetti sul foglio:
- **Persistenza nativa** (`37ab4295`): `DocumentService.Load/Save` passano per `ConcreteDocumentPersistence.Unpack/Pack`; `input.sezione_gpc`
  (contratto, impronta della ricetta, XML di `ModelArchive`) si scrive su una copia al salvataggio; `CheckerSection.PrepareModel` riusa la
  sezione archiviata se l'impronta della ricetta (`ConcreteSectionArchive.EditorStamp`: `input` senza `sezione_gpc`, più `workspace_ca.trefoli`)
  è quella di oggi. Il foglio porta `input.sezione_gpc` come dato opaco.
- **Wizard**: `ConcreteTemplateWizard` (Application: bozza, scelta, anteprima, nota, applicazione transazionale, che azzera i parametri del
  taglio e scrive `taglio.nota_wizard`), progetto nuovo `ANTHEA.ConcreteWorkspace` (`ConcreteSectionArchive`, `ConcreteTemplateAdapter`),
  `ConcreteGeometry.TemplateLinks`, `SezioneCA` e adattatori in X.Calculations, campi dei report (`ConcreteTemplateReportFields`).
- **Capsula**: dettagli del pilastro in `X.Calculations/ConcreteDetailing.cs`, modo «Curve e rettilinei separati» del wizard.

ViewModel di Presentation: `ConcreteTemplateWizardViewModel` è nuovo in `Modules/ConcreteSection` (nato MVVM, ospitato dalla finestra storica),
ma **non è collegato** a `ConcreteSheetViewModel`. Nessun file del ViewModel S1 è cambiato; `ConcreteSectionSheetChecks` chiama in più
`StadiumWizardChecks` (capsula, stadio `fast`).

### 9.3 Perché le tracce non cambiano

Le 11 fixture F3 e le 2 sezioni dei muri hanno forme ordinarie senza `wizard_layout`, nessuna `nota_wizard` e nessun percorso di staffe: ogni
ramo nuovo resta spento. Il pulsante del wizard e le due note sono nel gruppo, fuori dai form: la parte `[ingressi]` scrive form, interruttori e
tabelle, non i pulsanti dei gruppi né la visibilità di un form intero. La rilettura dei campi del foro dà gli stessi valori già mostrati. Le
fixture sono documenti di versione 2: nessuna ha `input.sezione_gpc`.

Conseguenza: **le tracce non coprono i wizard né la persistenza nativa**. Un errore del ViewModel su una sezione da wizard non si vedrebbe.

### 9.4 Che cosa manca al ViewModel S1

Differenze dalla vista storica di oggi, tutte sulle sezioni da wizard (nessuna nelle tracce):
1. il comando «Wizard tipologie / modifica disposizione…» (`ConcreteTemplateWizardViewModel` su `IDialogService`, poi la rilettura dei form e
   `Invalidate`, come `OpenTemplateWizard`);
2. geometria e armatura nascoste e spente, le due note, il suggerimento dell'armatura (`RefreshPreview`);
3. «Foro centrale» e campi del foro spenti; campi del foro riletti da `input` (`refreshHoleControls`);
4. secondi strati nascosti (`RefreshAdditionalLayers`) e campi della staffa circolare nascosti (`SynchronizeStirrups`);
5. `ShearAutomaticNote` da `taglio.nota_wizard` con parametri manuali (`RefreshAutomaticShear`);
6. la voce nuova di «Sezione» dopo il wizard (`FieldViewModel.ReplaceChoices`, come per le classi dei materiali).

I percorsi delle staffe (`TemplateLinks`) arrivano al disegno con `ConcreteGeometry`: il disegno resta WPF (Q3, C8) e li riceve senza
modifiche al ViewModel.

### 9.5 Wizard e persistenza nei sottopassi

- **S1b · Sezioni da wizard (nuovo sottopasso, dopo S2 e prima di S3).** Chiude le lacune 1–6.
  - Prima le tracce. Scenari nuovi in `ingressi/` sui 15 esempi `supporto/esempi/distribuzione/str_palo/*.anthea` (12 da wizard), aperti con
    `DocumentService.Load` (codec di versione 3, `input.sezione_gpc` presente). Prefisso di fixture `esempi/`, come `muri/`.
  - Parte nuova `[wizard]`, chiesta solo dai nuovi scenari: visibilità e stato dei form della geometria e dell'armatura, note, foro, comando del
    wizard; finestra «Wizard · Sezione in cemento armato» guidata come le altre finestre modali, con lo stato del suo ViewModel dopo ogni passo
    (form, nota, errore, `CanApply`, impronta dell'anteprima).
  - Le 39 tracce congelate non cambiano: non chiedono la parte nuova.
  - Le tracce nuove si catturano dalla vista storica (due catture uguali) in un commit a sé; poi il ViewModel.
  - `ConcreteTemplateWizardViewModel` diventa un `IDialogViewModel` (aggiunta); la finestra storica resta fino a S9.
- **S2 · Azioni.** Piano invariato (sezioni 4 e 8.6); è la parte b di questo ramo (sezione 10). Nessun legame con i wizard. L'editor delle
  sollecitazioni copia nel foglio solo `combinazioni` e `workspace_ca`: `input.sezione_gpc` e la ricetta non cambiano. `workspace_ca.trefoli`
  fa parte dell'impronta, ma l'editor non lo tocca.
- **S3 · Domini, S4 · SLE.** Il calcolo riusa la sezione archiviata (`CheckerSection.PrepareModel`), sotto il ViewModel. Le mappe disegnano
  i percorsi delle staffe con il disegno WPF. Scenario da aggiungere: una sezione da wizard per `domini` e `sle`.
- **S5 · Taglio.** `nota_wizard` e i parametri azzerati dal wizard (lacuna 5 se S1b non l'ha già chiusa). Campi della staffa circolare nascosti
  anche nel form delle staffe in sola lettura del taglio. Scenario da aggiungere: una sezione da wizard con taglio manuale.
- **S6 · Dettagli.** La capsula come pilastro passa da `ConcreteDetailingPresentation` (calcolo in X.Calculations). Scenario da aggiungere:
  `dettagli/capsula-pilastro` sull'esempio `wizard-capsula`.
- **S8 · Progetto delle armature.** I rifiuti per le tipologie avanzate vengono dalla libreria. Scenario da aggiungere: rifiuto su una sezione
  da wizard.
- **S9 · Report e passaggio.** I campi del wizard nei report (`ConcreteTemplateReportFields`) sono sotto `ConcreteReportAnalysis`, nessun legame
  col ViewModel. Prove storiche nuove da riscrivere al passaggio:
  - `ui/check-concrete-template-wizard` (`tests/Desktop/ConcreteTemplateViewChecks.cs`);
  - la parte `str_palo` di `ExampleLibraryChecks`;
  - `HorizontalPileViewChecks.Elastic` e `PileConcreteVerifierView.xaml.cs` (vista XAML del verificatore c.a. del palo elastico, F5.8, unita il
    18/10), che ospita la `ConcreteWorkspace` storica con `ReloadRequested`.

  Al passaggio `ConcreteTemplateWizardWindow` va in `supporto/SUPERATI` con le altre viste. Il confronto F3 continua a escludere solo
  `input.sezione_gpc` (`tests/Shared/ConcretePersistenceComparison.cs`).

Ordine aggiornato: S1 → S2 → S1b → (S3, S4, S5) → S6 → S7 → S8 → S9.

### 9.6 Domande «da decidere»

- **S2a-1 · Copertura dei wizard.** Le tracce di S1b sono tracce nuove della vista storica, non una ricattura. Si aggiungono come scenari
  nuovi con la parte `[wizard]`, lasciando invariate le 39 congelate. Scelta prudente: sì, in S1b. Alternativa: rinviare la copertura a S9,
  con il rischio che il ViewModel cresca senza prove sulle sezioni da wizard.

## 10. S2 fatto (21/10)

Sottopasso S2 «Azioni» (sezione 4, S2, e 8.6), ramo `refactoring/f5-10-s2`, 21/10/2026, sul riallineamento della sezione 9. Decisioni
applicate: C3 (`SectionActionsViewModel` in Presentation), C5 (comportamenti storici conservati), C7 (tracce lunghe nello stadio `ui`). La
Release resta storica; nulla va all'utente prima di S9.

### 10.1 Commit

| Commit | Contenuto |
| --- | --- |
| `e35b2d98` | sezione 9, riallineamento |
| `a5567277` | C3: `SectionActionsViewModel` e `SectionActionRow` in `src/ANTHEA.Presentation/Modules/ConcreteSection`, con il loro controllo (da Application.Tests a Presentation.Tests) |
| `9be9f6c5` | ViewModel delle azioni e modello di tabella dei pannelli, prove in `Presentation.Tests` |
| (questo commit) | questa sezione, esito della corsa finale |

### 10.2 Che cosa c'è

- **`ConcreteActionsViewModel`** (`ConcreteSheetViewModel.Actions`, creato all'apertura dopo il taglio, senza scritture): le tabelle delle
  azioni dei pannelli nell'ordine delle schede (`Domain3D`, `Domain2D`, `Stress` per SLE, SLE_FREQ e SLE_QP, `Shear`), il file delle azioni
  (`ExcelPath`, `file_sollecitazioni`, con Modified come `RememberExcel`) e la finestra «Sollecitazioni della sezione» (`EditCommand`: conferma
  del foglio, `SectionActionsViewModel` su `IDialogService`, «Applica» che sostituisce `combinazioni` e `workspace_ca`).
- **`ConcreteActionTableViewModel`**, il modello di tabella che S3–S5 riusano (`ActionTable`, `AttachClipboard` e `WithFilters` della vista
  storica):
  - righe, filtro, ordine e selezione su `ConcreteTableViewModel` di S1; colonne della vista storica, anche con le sollecitazioni uniche
    (sola lettura tranne «Mostra», «Elemento» e, per il taglio, «Stato limite» e tabella in sola lettura);
  - selezione come la vista di raccolta storica: prima riga inserita alla nascita, riga nuova dopo «+ Riga» e «Duplica», riga al suo posto
    dopo «− Riga», prima riga del taglio dopo un calcolo se nessuna è selezionata;
  - comandi «+ Riga», «Duplica», «− Riga» («+ Combinazione» e «−» per il taglio), «Copia» con la selezione estesa della vista
    (`SetSelectedRows`), «Incolla» e «Aggiungi dagli appunti» (`ConcreteActionPresentation.Plan`, tutto convalidato prima di scrivere, righe
    scritte senza invalidazioni riga per riga, poi conferma, invalidazione e stato; rifiuto con l'avviso «Incolla sollecitazioni» su
    `IMessageService`), «Template Excel», «Importa Excel» / «Gestisci sollecitazioni…», «Esporta Excel», «Sfoglia…»;
  - per i domini: righe dello stato limite del pannello (`State`, `dominio3d.stato` o `dominio2d.stato`), filtro degli esiti
    (`ConcreteDomainChart.Includes`), di nuovo con la riga selezionata prima quando è fra quelle mostrate (`AttachDomainRows`) a ogni cambio
    di stato e a ogni pubblicazione del dominio mostrato.
- **`SectionActionsViewModel`**: spostato senza modifiche (C3), più i membri del servizio dei dialoghi (titolo, «Applica» che chiude solo
  se riesce, «Annulla») e, con il costruttore che riceve `IFileDialogService`, i quattro comandi Excel della finestra storica.
- **Foglio**: dopo «Applica» della finestra il foglio si apre di nuovo sui suoi dati (`Reload` al posto di `ReloadRequested`). Come la
  `ConcreteWorkspace` nuova: lo stato torna quello dell'apertura, le uscite del dominio della scheda si scrivono subito, il calcolo
  dell'apertura entra in coda un giro dopo (il `Loaded` della vista). A ogni apertura e a ogni cambio di scheda, la tabella del dominio
  mostrato scrive le uscite delle sue righe se mancano (`IsVisibleChanged` della griglia storica, `RefreshDomainPanel`).
- **Prove** (`tests/ANTHEA.Presentation.Tests`): `ConcreteSectionSheetChecks.Actions.cs` guida le tabelle e la finestra come la traccia
  storica e scrive la parte `[azioni]`; `ConcreteSectionSheetChecks.ActionUnits.cs` (stadio `fast`) prova ciò che le tracce non vedono.

### 10.3 Esiti

- **Tracce** (parte comune, `[ingressi]` e `[azioni]`): 25 scenari uguali alle tracce congelate della vista storica. Sono `azioni/*` (4),
  `ingressi/*` (6), `percorso-f3/*` (11, ora anche con `[azioni]`), `calcolo/ricalcolo-revisione` e `muri/*` (2). `calcolo/sola-lettura` resta
  uguale fino al passo «scelta 3D filtro «Fuori dominio»» (S3), come in S1.
- **Prove rapide**: `--sezione-rapide` 192 controlli (158 di S1, più 34 di S2): colonne e comandi delle tabelle storiche e uniche, righe e
  taglio, prima riga del taglio dopo il calcolo, copia, incolla rifiutato con l'avviso, «Aggiungi dagli appunti» del taglio,
  «Template Excel» e «Sfoglia…», finestra annullata, «Esporta Excel» delle azioni storiche, comandi Excel della finestra, «Applica» con la
  nuova apertura e il calcolo un giro dopo, sola lettura, stato del dominio. Scenario `azioni/incolla` aggiunto alle tracce rapide.
- **Conteggi**: con C3 i 26 controlli di `SectionActionsEditorChecks` passano da Application.Tests (3 057 → 3 031) a Presentation.Tests
  (3 523 → 3 549); con S2 Presentation.Tests sale a 3 584 (34 controlli nuovi e lo scenario `azioni/incolla` fra le tracce rapide).
  Corse intermedie: `supporto/artefatti/ci/20261021-080244-s2b-c3` e `20261021-082926-s2b-azioni` (quick più le suite della sezione),
  PASS 76 ciascuna.
- **Baseline**: F4 e P3 invariate (nessuna voce per Presentation; il file spostato non ne aveva).
- **Corsa finale**: `-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` su `9be9f6c5`:
  `supporto/artefatti/ci/20261021-091429-s2-finale`, PASS 147 (build 26, fast 33, wiki 2, ui 86), NEW-FAIL 0; nessun file tracciato
  modificato durante la corsa.
  - `ui/check-contrast` è escluso: lo schermo è al 150% (D6). Nessun passo di contrasto aggiunto, perché S2 non ha una vista.
  - Presentation.Tests 3 584, Application.Tests 3 031, Architecture.Tests 561.
  - `ui/check-sezione-historic`: 39 scenari, 84 832 righe, 120 disegni e 33 righe 3D uguali ai congelati.
  - `Presentation.Tests/sezione-ca` 34 controlli; `ui/f3-equivalence`, `ui/smoke-section-actions` e `ui/check-module-host-offscreen` PASS.

### 10.4 Comportamenti storici trovati in S2 (conservati, C5)

1. **«Esporta Excel» in sola lettura.** Il comando resta attivo nella revisione in sola lettura. Con le azioni storiche apre la finestra
   delle sollecitazioni, dove «Applica» cambia i dati con Modified e riapre il foglio. È della stessa famiglia del comportamento 5 della
   sezione 2.
2. **«−» del taglio senza selezione.** Salva le righe e invalida il taglio lo stesso: Modified e ricalcolo.
3. **«Duplica» non conferma la cella in modifica**, «+ Riga» e «− Riga» sì.
4. **Righe nuove dell'incolla nel taglio.** Si chiamano «Combo n», non «Taglio n» come quelle di «+ Combinazione».
5. **Riapertura dopo la finestra.** Subito dopo «Applica» il foglio non ha risultati e il calcolo non è ancora in coda: parte quando la vista
   nuova è caricata. Il ViewModel lo riproduce con un giro del thread dell'interfaccia.

### 10.5 Domande «da decidere» (scelte provvisorie, da rivedere in F5.15)

- **S2-a · Stato del dominio.** Fino al form delle opzioni di S3, `ConcreteActionTableViewModel.State` è l'unico punto che cambia
  `dominio3d.stato` e `dominio2d.stato`: scrive, mostra le righe dello stato, scrive le uscite, Modified, anche in sola lettura. In S3 il
  campo «stato» del form passerà da qui, perché la tabella deve seguire lo stato. Alternativa: il form scrive e chiama `Reattach`.
- **S2-b · Calcolo dopo la finestra.** Il giro di ritardo vale solo per la riapertura dopo la finestra delle azioni. `Reload` per i dati
  condivisi (S1) resta com'è, con il calcolo subito in coda: nessuna traccia lo copre. Da decidere al passaggio (S9): stessa regola per i due
  casi o no.
- **S2-c · Comandi Excel della finestra.** Sono nel `SectionActionsViewModel`, con un costruttore che riceve `IFileDialogService` e il file
  proposto. Non c'è un ViewModel della finestra separato. La vista storica usa ancora i suoi pulsanti.
- **S2-d · «Elimina» nella finestra.** `SectionActionsViewModel.Remove` lascia `Selected` sulla riga tolta; la griglia storica lo azzera con
  il binding. Nelle tracce l'indice è −1 in tutti e due i casi. La vista XAML (S9) deve legare la selezione come la storica.

### 10.6 Che cosa resta e come riprendere

- **S1b** (sezione 9.5): le sezioni da wizard, prima con le tracce nuove.
- **S3 · Domini.**
  - Il form delle opzioni scrive `stato` attraverso `Domain3D.State` e `Domain2D.State` (S2-a).
  - `filtro` richiama `Reattach` come `AttachDomainRows` e scrive anche in sola lettura (comportamento 5).
  - I passi `filtro` di `domini/filtri-tabelle` usano `Table.Filter`; i pannelli seguono `Table.SelectionChanged` (`UpdateSelection`).
  - Si toglie l'arresto atteso di `calcolo/sola-lettura`, se S3 copre il filtro.
- **S4 e S5.** `Stress[...]` e `Shear` sono le tabelle dei pannelli; i dettagli seguono la selezione.
- **Vista XAML** (S9 o per sottopasso): le note per la vista sono nel commento di `ConcreteActionTableViewModel`.

```
dotnet build tests/ANTHEA.Presentation.Tests/ANTHEA.Presentation.Tests.csproj
dotnet tests/ANTHEA.Presentation.Tests/bin/Debug/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-rapide
dotnet tests/ANTHEA.Presentation.Tests/bin/Debug/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-tracce
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)' -Tag <tag>"
```

## 11. S1b fatto (21/10)

Sottopasso S1b «Sezioni da wizard e persistenza nativa» (sezione 9.5), ramo `refactoring/f5-10-s1b` da `refactoring/f5` `3e1b86de`
(S2 unito), 21/10/2026. Chiude le sei lacune della sezione 9.4. Decisioni applicate: S2a-1 (tracce nuove, le 39 congelate invariate), C5
(comportamenti storici conservati), C7 (tracce lunghe nello stadio `ui`), C8 (disegni WPF alimentati dal ViewModel). La Release resta
storica; nulla va all'utente prima di S9.

### 11.1 Commit

| Commit | Contenuto |
| --- | --- |
| `ade9737c` | tracce nuove della vista storica: 15 scenari `esempi/…`, 3 scenari `ingressi/wizard-…`, parte `[wizard]` |
| `0852f275` | lacune 1–6 nel ViewModel, derivazioni in Application chiamate anche dalla vista storica, prove |
| (questo commit) | questa sezione e la riga di `piano.md` |

### 11.2 Tracce nuove (prima le tracce)

Catturate dalla vista storica sul codice di `3e1b86de`, in un commit a sé: due catture uguali byte per byte (57 scenari, 118 860 righe);
nelle due catture i 39 scenari e i disegni già congelati sono uguali ai file del riferimento. README di `tests/fixtures/f5-sezione`
aggiornato.

- **`esempi/…` (15).** Gli esempi `supporto/esempi/distribuzione/str_palo/*.anthea` (documenti di versione 3 con `input.sezione_gpc`, 12
  fatti dal wizard), aperti come la finestra apre un esempio (`DocumentService.Open`, poi i dati del foglio), con il percorso di
  `percorso-f3` (apertura, calcolo, conferma, visita, «Calcola», riapertura) e tutte le parti, più `[wizard]`. Le parti dei sottopassi
  successivi (domini, SLE, taglio, dettagli, armature, ispezione) sono già congelate anche per le sezioni da wizard: coprono gli «scenari
  da aggiungere» della sezione 9.5 per S3, S4 e S6 (capsula come pilastro nell'esempio `wizard-capsula`).
- **`ingressi/wizard-…` (3).** `wizard-tipologie` (dalla fixture `default`: finestra annullata; «A I» con un valore rifiutato,
  «Applica sezione» spento, poi accettato; controlli dell'editor bloccati; «modifica disposizione»; rettangolo cavo a doppio contorno; di
  nuovo un cerchio ordinario), `wizard-capsula` (barre per coordinate su una sezione da wizard, le due disposizioni della capsula con un
  conteggio rifiutato, staffe assenti e di nuovo presenti), `wizard-sola-lettura` (cerchio cavo: foro e staffe circolari bloccati, wizard
  riapplicato, poi la revisione in sola lettura con il wizard non disponibile).
- **Parte `[wizard]`**, chiesta solo dagli scenari nuovi: comando del wizard (spento in sola lettura), form della geometria e
  dell'armatura nascosti o spenti con le due note, campi del foro nel loro insieme, nota dei parametri del taglio.
- **Finestra del wizard.** È un'isola MVVM dal 20/10 (`ConcreteTemplateWizardViewModel` ospitato da `ConcreteTemplateWizardWindow`): nella
  traccia storica i suoi campi si leggono e si scrivono dal ViewModel (con la stessa regola dei controlli: un campo nascosto o spento non
  fa nulla), i pulsanti si premono nella finestra; il suo stato dopo ogni passo è tipologia, «Applica sezione» disponibile, errore, nota,
  ogni campo (valore, nascosto, spento), anteprima con i percorsi delle staffe, impronta della bozza.
- **Righe dei valori cambiati.** L'archivio XML di `input.sezione_gpc` (centinaia di kB) è scritto come dimensione e impronta; l'impronta
  della riga `dati` copre tutti i byte. Le tracce di S1 non hanno sezione nativa e non cambiano.

### 11.3 Che cosa c'è nel ViewModel

Le lacune della sezione 9.4, una per una:
1. **Comando del wizard.** `ConcreteInputViewModel.OpenWizardCommand`: conferma del foglio, `ConcreteTemplateWizardViewModel` sul servizio
   dei dialoghi (con il navigatore della Wiki dei servizi, `CreateWizardDialog`), e dopo «Applica sezione» i form della geometria,
   dell'armatura e dei secondi strati riletti dai dati, i controlli del foro, l'invalidazione (come `OpenTemplateWizard`). Spento in sola
   lettura. `ConcreteTemplateWizardViewModel` diventa un `IDialogViewModel<bool>` (aggiunta: titolo, risultato, `ApplyCommand`,
   `CancelCommand`, navigatore opzionale); la finestra storica lo ospita come prima e ne prende il titolo.
2. **Geometria e armatura.** `IsTemplateSection`, `GeometryShown`, `GeometryEnabled`, `ReinforcementShown`, `TemplateNotesShown` e le due
   note; l'armatura spenta anche per le sezioni da wizard, con la sua nota.
3. **Foro.** «Foro centrale» spento per le sezioni da wizard (`HoleEnabled`), campi del foro spenti (`HoleFormEnabled`) e riletti dai dati a
   ogni aggiornamento dell'anteprima.
4. **Secondi strati e staffa circolare** nascosti per le sezioni da wizard.
5. **Nota del taglio** con parametri manuali: `taglio.nota_wizard`, se c'è.
6. **Voce nuova di «Sezione»** dopo il wizard (`FieldViewModel.ReplaceChoices`).

Le derivazioni sono in Application (`ConcreteInputPresentation`: `TemplateSection`, `HoleToggleEnabled`, `ReinforcementToolTip(input,
enabled)`, `LayerVisible`, `CircularLinkScheme`, `ShapeChoicesAfterWizard`, `ManualShearNote`, le due note) e la vista storica le chiama al
posto delle sue espressioni, come le derivazioni della parte A: le 57 tracce lo provano.

**Persistenza nativa (versione 3).** Il foglio porta `input.sezione_gpc` come dato opaco: il ViewModel non lo scrive, il wizard lo toglie
applicando una sezione nuova (`ConcreteTemplateWizard.Apply`), il salvataggio lo ricrea (`ConcreteDocumentPersistence.Pack`, sotto il
ViewModel). Le tracce `esempi/…` provano che i dati del ViewModel sono quelli della vista storica byte per byte; un controllo delle tracce
lunghe apre, calcola e conferma ogni esempio nel ViewModel e lo salva: quando l'apertura lascia la ricetta com'è, la sezione GPC resta
corrente e il salvataggio conserva il suo archivio (tutti i 12 esempi del wizard).

### 11.4 Esiti

- **Tracce** (`Presentation.Tests/sezione-ca`, parte comune, `[ingressi]`, `[azioni]` e `[wizard]`): 43 scenari uguali alle tracce congelate
  (i 25 di S2, i 15 `esempi/…` e i 3 `ingressi/wizard-…`); `calcolo/sola-lettura` uguale fino all'arresto atteso di S1 (S3). 54 controlli
  con i file dei dati F3, le sezioni dei muri e la persistenza nativa.
- **Prove rapide** (`--sezione-rapide`): 242 controlli; in più lo scenario `ingressi/wizard-tipologie` fra le tracce rapide, il wizard sul
  servizio dei dialoghi (titolo, «Annulla», «Applica sezione» spento con un valore rifiutato, chiusura con la sezione applicata, editor nascosto,
  voce nuova di «Sezione», calcolo in coda, nota del taglio), la bozza confermata prima del wizard, la sola lettura, F1 dei campi del wizard.
- **Application.Tests**: 8 controlli delle derivazioni nuove di `ConcreteInputPresentation`, con valori scritti a mano.
- **Vista storica**: `ui/check-sezione-historic` 57 scenari, 118 860 righe, 120 disegni e 33 righe 3D uguali ai congelati, dopo il passaggio
  della vista alle derivazioni di Application; `ui/check-concrete-template-wizard` PASS.
- **Corse**: tracce `ci/20261021-114512-s1b-tracce-quick` (quick PASS 55) e `ci/20261021-120025-s1b-tracce-sezione` (PASS 4); ViewModel
  `ci/20261021-121807-s1b-vm` (PASS 10, suite mirate) e `ci/20261021-122849-s1b-vm-quick` (quick PASS 55, NEW-FAIL 0). La corsa finale del
  ramo è nella sezione di S3 o in quella di chiusura.
- **Baseline**: F4 e P3 invariate (le derivazioni spostate non avevano voci).

### 11.5 Comportamenti storici trovati in S1b (conservati, C5)

1. **Esempi ordinari salvati senza i valori dell'editor.** `circolare-cava`, `rettangolare-materiali-utente` e `sle-fessurazione-assi` non
   hanno i valori predefiniti che l'editor scrive all'apertura (`gettato_sottile`, `staffe_presenti`, campi del foro, dell'ala e dei secondi
   strati): aprirli cambia l'impronta della ricetta e la sezione GPC archiviata non viene riusata (il calcolo la ricostruisce, il
   salvataggio ne cattura una nuova). Nessun effetto sui risultati; per riusare la sezione basterebbe salvarli di nuovo dall'editor
   (fuori da F5, da proporre all'utente).
2. **Taglio dopo il wizard.** Per le sezioni da wizard il wizard svuota i parametri del taglio (CA-WIZARD-01): ogni calcolo dopo
   l'applicazione finisce con «Aggiornamento parziale · Taglio: rami_x: inserire un numero finito.» finché l'utente non li assegna.
3. **Voci di «Sezione».** La tipologia del wizard resta fra le voci di «Sezione» anche dopo un ritorno a una sezione ordinaria (fino alla
   chiusura del foglio): l'utente può sceglierla nell'editor ordinario.
4. **Artefatto delle tracce, non della finestra** (seguito del punto 4 della sezione 8.4): un form mai disposto non passa ai campi il suo
   stato spento. La geometria di un esempio da wizard è nascosta fin dall'apertura: nella traccia i suoi campi risultano attivi; i campi
   del foro di una sezione da wizard senza foro, idem. Il driver delle prove estende la regola dei form disposti a geometria, foro e
   armatura.

### 11.6 Domande «da decidere» (scelte provvisorie, da rivedere in F5.15)

- **S1b-a · Cartella degli esempi.** I 15 esempi sono in una cartella `esempi/` (come `muri/`) con il percorso completo e tutte le parti,
  non in `ingressi/` come proponeva la sezione 9.5: coprono anche i sottopassi successivi. Alternativa: solo la parte `[ingressi]`.
- **S1b-b · Archivio nelle tracce.** Scritto come dimensione e impronta nelle righe dei valori cambiati.
- **S1b-c · Finestra del wizard nella traccia storica.** Campi guidati dal ViewModel (isola MVVM), pulsanti premuti nella finestra.
- **S1b-d · Disegni.** Nessun disegno nuovo congelato per le sezioni da wizard: i percorsi delle staffe (`TemplateLinks`) arrivano al disegno
  con `ConcreteGeometry` e la sezione disegnata è nelle tracce come dati. Alternativa: elenchi di disegno degli esempi in un file a parte
  di `disegni/`.

### 11.7 Che cosa resta e come riprendere

- **S3–S9** come nella sezione 9.5, con gli scenari degli esempi già congelati per le parti di ogni sottopasso.
- **Vista XAML** (S9, o per sottopasso): il comando del wizard con `IDialogService`, la finestra del wizard come DataTemplate di
  `ConcreteTemplateWizardViewModel` (la finestra storica va in `supporto/SUPERATI` al passaggio), la regola dei form disposti da ricordare
  per le prove della vista.

```
dotnet build tests/ANTHEA.Presentation.Tests/ANTHEA.Presentation.Tests.csproj -c Release
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-rapide
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-tracce
dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-sezione-historic . <cartella>
```

## 12. S3 fatto (21/10)

Sottopasso S3 «Domini 3D e 2D» (sezione 4, S3; sezione 10.6), stesso ramo `refactoring/f5-10-s1b`, 21/10/2026, dopo S1b. Decisioni
applicate: S2-a (lo stato limite passa dal campo del form del pannello), C5 (comportamenti storici conservati, compresa la sola lettura che
scrive), C7 (tracce lunghe nello stadio `ui`), C8 (il viewport 3D resta WPF e si confronta come dati). La Release resta storica; nessuna vista
XAML (S1-d).

### 12.1 Commit

| Commit | Contenuto |
| --- | --- |
| `4e657c4a` | pannelli dei domini nel ViewModel, derivazioni in Application chiamate anche dalla vista storica, baseline F4 spostata, prove |
| (questo commit) | questa sezione, riga di `piano.md`, esito della corsa finale |

### 12.2 Che cosa c'è

- **`ConcreteDomainPanelViewModel`** (`Domain3D`, `Domain2D` del foglio, creati all'apertura dopo le tabelle delle azioni, senza scritture),
  da `DomainPanel`, `BuildDomainPanel`, `AttachDomainRows`, `RefreshDomainPanel` e `UpdateSelection` della vista storica:
  - form «Opzioni di calcolo» sulle opzioni `dominio3d` / `dominio2d`, con i gruppi e le invalidazioni storiche: stato e filtro riattaccano le
    righe; interpolazione e suddivisioni ridisegnano solo la mesh (su un thread di lavoro, con il suo stato); ricerca, metodo e proiezione
    ricalcolano punti resistenti e tassi conservando il dominio; le altre opzioni ridisegnano il dominio. Stato e filtro restano usabili in
    sola lettura (`FormViewModel.AllowWhenReadOnly`, aggiunta);
  - la tabella del pannello è quella di S2 (`ConcreteActionTableViewModel`); lo stato limite passa dal campo «stato» del form anche quando lo
    sceglie la tabella (S2-a);
  - dettaglio della riga selezionata, riepilogo delle verifiche, barra del disegno (forze, cinque caselle, «Mesh», dimensioni dei punti,
    trasparenza, «Scala…» come `ConcreteDomainScaleDialogViewModel` su `IDialogService`, «Adatta»), «Sezione sull’azione selezionata» per il 2D;
    le due caselle della sezione σ/ε che stanno nella stessa barra della traccia («Asse neutro», «Valori σ/ε sulle barre»), la scheda
    interna «Dominio» / «Sezione σ / ε» (il resto della sezione σ/ε è S7);
  - disegno e dettaglio seguono la selezione solo mentre la tabella è visibile (scheda principale e scheda interna «Dominio»), come la
    griglia storica: altrimenti aspettano (`NeedsVisualRefresh`).
- **Disegni come dati** (Q3, C8): `ConcreteDomain3DViewModel` (mesh, punti delle azioni con l'esito, selezione, azione e resistenza
  selezionate, opzioni, fattori degli assi, tassi, resistenze, nota: le regole di `SetMesh`, `SetActions` e `SetAxisScale` del viewport
  storico) e `ConcreteDomain2DViewModel` (titoli, segmenti, punti e linee di verifica di `ConcreteDomainChart`, fattori degli assi).
- **Application** (`ConcreteDomainChart`): dettaglio della riga, piano sull'azione selezionata, dimensioni dei punti, trasparenza, fattori
  degli assi e lettura della finestra «Scala…», triangoli della mesh; la vista storica li chiama al posto delle sue espressioni.
- **Modello di tabella**: una riga selezionata che il filtro nasconde passa la selezione alla prima riga mostrata (l'elemento corrente della
  vista di raccolta dopo il suo Refresh), come la griglia storica (`domini/filtri-tabelle`); prima la selezione si azzerava.
- **Baseline F4**, solo spostamenti: 13 voci della vista (`BuildDomainPanel` 4, `UpdateSelection` 2, `AddDomainScaleControls` 6,
  `DomainViewport3D.TriangleCount` 1) diventano 6 voci di `ConcreteDomainChart`; 2 185 → 2 178 voci, 3 320 → 3 313 occorrenze, nessuna
  aggiunta. P3 invariata.

### 12.3 Esiti

- **Tracce** (`Presentation.Tests/sezione-ca`, parte comune, `[ingressi]`, `[azioni]`, `[domini]` e `[wizard]`): 45 scenari uguali alle tracce
  congelate (i 43 di S1b più `domini/opzioni-domini` e `domini/filtri-tabelle`; la parte `[domini]` è confrontata anche in `percorso-f3/*`,
  `muri/*`, `esempi/*`, `azioni/*`, `report/dati-incompleti` e `calcolo/*`). `calcolo/sola-lettura` è uguale fino al nuovo arresto atteso,
  «menu SLE contorno «Solo geometria»» (S4). Domini 3D come dati: le 33 righe di `disegni/domini-3d.txt` uguali (ogni fixture F3 aperta,
  calcolata e mostrata come nei disegni della traccia storica). 57 controlli con le tracce lunghe.
- **Prove rapide** (`--sezione-rapide`): 280 controlli; in più la sola lettura dei pannelli (stato e filtro usabili, opzioni e dimensioni
  rifiutate, barra che scrive), il punto scelto nel disegno 3D, «Scala…» con un fattore rifiutato e poi applicato, annullata, «Adatta», il
  ripristino del grafico 2D, F1 dei form dei pannelli e della finestra «Scala…».
- **Application.Tests**: 11 controlli di `ConcreteDomainChart` con valori scritti a mano (3 050).
- **Vista storica**: `ui/check-sezione-historic` 57 scenari e disegni uguali ai congelati dopo il passaggio della vista alle derivazioni
  di Application.
- **Corse**: mirata `ci/20261021-125445-s3-mirata` (PASS 10: Architecture.Tests con la baseline spostata, Application.Tests,
  Presentation.Tests 3 672, `ui/check-sezione-historic`, `Presentation.Tests/sezione-ca`); `ci/20261021-143555-f510-s1b-s3-finale2` su
  `4e657c4a` (build, fast, wiki e ui senza `ui/check-contrast`): PASS 144, KNOWN 0, NEW-FAIL 3, tutte di ambiente e ripetute da sole:
  `Architecture.Tests` (AccessViolationException nel parser di Roslyn, censimento F4 già passato senza aggiunte; da sola PASS,
  `ci/20261021-154230-f510-s1b-s3-finale2-arch`), `ui/check-muri-view` (da sola PASS, `ci/20261021-154248-f510-s1b-s3-finale2-ui`) e
  `ui/check-palo-verticale-view` (antivirus, domanda S6: il programma si chiude sempre allo stesso salvataggio dell'esempio
  `elica-coesivo-avvisi` e resta il file `.x_<guid>.tmp`; da sola fallisce allo stesso punto, con `dotnet ANTHEA.dll` PASS 377 controlli).
  Una prima corsa finale (`ci/20261021-131034-f510-s1b-s3-finale`) è stata interrotta dal riavvio della sessione prima del riepilogo.

### 12.4 Comportamenti storici trovati in S3 (conservati, C5)

1. **Cultura dei numeri del dettaglio.** Il dettaglio scritto durante un passo dell'utente usa la cultura della traccia (virgola), quello
   scritto dalla pubblicazione di un calcolo la cultura del thread dell'interfaccia (sulla macchina del banco, punto decimale). Il ViewModel
   segue gli stessi percorsi e dà gli stessi testi; la vista XAML dovrà fissare la cultura (da decidere in F5.15).
2. **Sola lettura che scrive** (famiglia del comportamento 5): filtro e stato del pannello, caselle e forze della barra, trasparenza, «Scala…»
   e «Adatta» scrivono le opzioni con Modified anche nella revisione in sola lettura.
3. **«Adatta» del 2D** scrive due volte i fattori x e y (il pulsante e il ripristino della vista del grafico) con due Modified.

### 12.5 Domande «da decidere» (scelte provvisorie, da rivedere in F5.15)

- **S3-a · Stato limite.** Il campo «stato» del form è l'unico che scrive `dominio3d.stato` / `dominio2d.stato`; `ConcreteActionTableViewModel.State`
  passa da lì (chiude S2-a).
- **S3-b · Esenzioni della sola lettura.** `FormViewModel.AllowWhenReadOnly` è un'aggiunta al componente comune, usata solo dai pannelli dei
  domini. Alternativa: due form separati.
- **S3-c · Zoom e camera.** Lo zoom resta della vista (`Zoom`, `FitRequested`, `ZoomRequested`); la vista del 3D («Isometrica», «N–Mx»…) non
  passa dal ViewModel.
- **S3-d · Disegni del 2D.** Gli elenchi di disegno del dominio 2D (`disegni/elenco.txt`) si confrontano solo con la vista XAML (S9); qui il
  grafico è confrontato come dati nella parte `[domini]`.

### 12.6 Che cosa resta e come riprendere

Piano di S4–S9 aggiornato (ordine invariato: S4, S5, poi S6, S7, S8, S9):
- **S4 · SLE e tensioni.** È il primo arresto delle tracce: `calcolo/sola-lettura` si ferma a «menu SLE contorno «Solo geometria»» (il
  contorno della mappa si scrive anche in sola lettura). `StressPanelViewModel` per famiglia sul modello di tabella di S2
  (`Actions.Stress[...]`), con le opzioni comuni (`SynchronizeSharedSle`) e la mappa come dati; gli esempi da wizard sono già nelle tracce
  `esempi/…` per la parte `[sle]`.
- **S5 · Taglio e torsione.** `Actions.Shear`; la nota del wizard è già nel foglio (S1b). Campi della staffa circolare nascosti anche nel form
  delle staffe in sola lettura del taglio.
- **S6 · Dettagli.** La capsula come pilastro è nella traccia `esempi/wizard-capsula` (parte `[dettagli]`).
- **S7 · Ispezione e momento–curvatura.** Il pannello dei domini ha già la scheda interna (`InspectionTab`) e le due caselle della barra
  della sezione σ/ε (`NeutralAxis`, `BarValues`); S7 aggiunge la sessione dell'ispezione in `UpdateSelection` (il punto è segnato nel codice)
  e il resto della scheda «Sezione σ / ε».
- **S8, S9** come nella sezione 4. Al passaggio (S9) la vista XAML dei domini lega il viewport 3D e il grafico 2D ai dati dei ViewModel
  (`Changed`, `FitRequested`, `ZoomRequested`, `ActionPicked`, `ChartViewReset`) e confronta gli elenchi di disegno del dominio 2D.

```
dotnet build tests/ANTHEA.Presentation.Tests/ANTHEA.Presentation.Tests.csproj -c Release
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-rapide
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-tracce
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)' -Tag <tag>"
```

## 13. S4 fatto (21/10)

Sottopasso S4 «SLE e tensioni» (sezione 4, S4; sezione 12.6), ramo `refactoring/f5-10-s4` da `refactoring/f5` `d22550c4` (S1, S2, S1b, S3 e
preparazione di F5.11 uniti), 21/10/2026. Decisioni applicate: C5 (comportamenti storici conservati, compresa la sola lettura che scrive), C7
(tracce lunghe nello stadio `ui`), C8 (la mappa resta un disegno WPF, confrontata come dati). La Release resta storica; nessuna vista XAML
(S1-d).

### 13.1 Commit

| Commit | Contenuto |
| --- | --- |
| `f99eaff8` | derivazioni della scheda SLE in Application (`ConcreteStressPresentation`), chiamate dalla vista storica; baseline F4 spostata; Application.Tests |
| `946d81d2` | famiglie SLE nel ViewModel (`ConcreteStressPanelViewModel`), mappa come dati, prove in `Presentation.Tests`, arresto delle tracce per occorrenza |
| (questo commit) | questa sezione, riga di `piano.md`, esito della corsa finale |

### 13.2 Che cosa c'è

- **`ConcreteStressPanelViewModel`**, una per famiglia (`StressPanels`, `StressPanel("SLE" | "SLE_FREQ" | "SLE_QP")` del foglio), creata
  all'apertura dopo i pannelli dei domini, senza scritture (da `StressPanel`, `BuildStressTabs`, `PublishStress`, `UpdateStressSelection` e
  `SynchronizeHomogenization` di `ConcreteStress.cs`, `SynchronizeSharedSle` e `RefreshTendonOptions` di `ConcreteRefinements.cs`):
  - form «Opzioni SLE comuni» su `sle.<famiglia>`, con i quattro gruppi storici; n e φ calcolati l'uno dall'altro (`SynchronizeHomogenization`),
    con gli errori in un dizionario condiviso dalle tre famiglie (`HomogenizationErrors`); campi di φ e n abilitati dall'analisi lineare,
    origine e rotazione dagli assi personalizzati, avviso dell'analisi non lineare; campi e casella dei trefoli solo con i cavi;
  - una modifica passa da `StressOptionChanged` del foglio: omogeneizzazione, opzioni comuni copiate nelle altre famiglie e in `sle_comuni`
    (`SynchronizeSharedSle`), campi abilitati; un'opzione della fessurazione ricalcola le tre famiglie, le altre azzerano prima i risultati
    della famiglia;
  - la tabella della famiglia è quella di S2 (`Actions.Stress[...]`): le colonne delle tensioni e della fessurazione seguono le verifiche
    della famiglia, per indice come la griglia storica;
  - dettaglio, passaggi della fessurazione, tabelle delle barre e dei vertici del calcestruzzo, zone efficaci e scheda della fessurazione della
    riga selezionata, solo mentre la famiglia è mostrata (scheda 04 e famiglia scelta, `IsShown`), come la mappa storica (`IsVisibleChanged`);
  - schede: famiglia mostrata (`StressFamily` del foglio, anche dalla scelta «Combinazione SLE»), colonna dei risultati (`VerificationTab`,
    «Riepilogo verifiche» all'apertura), schede dei dettagli (`DetailTab`, -1 finché non sono mostrate la prima volta);
  - barra della mappa: contorno, «Asse neutro» e zona efficace usabili anche in sola lettura (il contorno e l'asse scrivono con Modified, come
    la vista storica); «σ/ε barre», «σ/ε trefoli», «σ/ε vertici CLS» rifiutate in sola lettura; una barra cliccata sulla mappa porta alla
    scheda «Barre e trefoli» e alla sua riga; «Reologia del calcestruzzo…» con il comando degli ingressi.
- **Mappa come dati** (Q3, C8): `ConcreteSectionDrawingViewModel` con tre proprietà nuove, `Stress`, `EffectiveRegion` e `Legend`. La legenda
  è quella del disegno sullo schermo: la vista storica la scrive quando disegna, quindi solo mentre la famiglia è mostrata e la sezione
  esiste; il ViewModel la aggiorna negli stessi momenti (domanda S4-a).
- **Application** (`ConcreteStressPresentation`): uscite delle righe dopo il calcolo, campi abilitati dall'analisi, avviso non lineare,
  dettaglio della riga, passaggi della fessurazione, righe delle barre e dei vertici, scala e legenda del contorno della mappa
  (`ContourScale`). La vista storica le chiama al posto delle sue espressioni (`ConcreteStress.cs`, `ConcreteRefinements.cs` e il `Render` di
  `ConcreteSectionViewport` per la scala); le uscite del foglio di S1 usano la stessa funzione. Nello spostamento si è tolta la prima
  assegnazione del dettaglio, che la seconda sovrascriveva subito (nessun effetto).
- **Prove delle tracce**: l'arresto atteso di uno scenario indica ora passo e occorrenza (`("attendi", 2)`), perché il nuovo arresto è un
  «attendi» che compare più volte (domanda S4-d).
- **Baseline F4**, solo spostamenti: le quattro voci R4 di `ConcreteWorkspace.UpdateStressSelection` (`options.D("n_armature")`, `phi`,
  `n_trefoli`, `phi_trefoli`) diventano le quattro di `ConcreteStressPresentation.Detail`; 2 157 voci e 3 214 occorrenze invariate, nessuna
  aggiunta. P3 invariata.

### 13.3 Esiti

- **Tracce** (`Presentation.Tests/sezione-ca`, parte comune, `[ingressi]`, `[azioni]`, `[domini]`, `[sle]` e `[wizard]`): 47 scenari uguali alle
  tracce congelate (i 45 di S3 più `sle/opzioni-sle` e `sle/fessurazione`; la parte `[sle]` è confrontata anche in `percorso-f3/*`, `muri/*`,
  `esempi/*`, `azioni/tabelle-storiche` e `ingressi/materiali-normativa` e `staffe-trefoli`), compresa la legenda della mappa con la cultura di
  ogni passo. `calcolo/sola-lettura` è uguale fino al nuovo arresto atteso, il secondo «attendi»: dopo «Calcola curva» la curva M–χ (S7)
  calcolata anche in sola lettura entra nel risultato esportato. 59 controlli con le tracce lunghe.
- **Prove rapide** (`--sezione-rapide`): 355 controlli; in più lo scenario `sle/fessurazione` fra le tracce rapide, F1 dei campi delle tre
  famiglie, la barriera del motore (una modifica durante il calcolo trattenuto lo supera: la famiglia mostra solo le tensioni della revisione
  corrente), la sola lettura (opzioni, tabelle e caselle protette; contorno, asse neutro, famiglia e zona usabili), le opzioni comuni
  copiate, l'errore dell'omogeneizzazione, la barra cliccata, le caselle dei valori, la legenda di una famiglia nascosta, «Reologia…» e Reload.
- **Application.Tests**: 13 controlli di `ConcreteStressPresentation` (3 119), con valori scritti a mano: le tensioni della fixture `default`
  sono quelle della sua traccia storica congelata.
- **Vista storica**: `ui/check-sezione-historic` 57 scenari, 118 860 righe, 120 disegni 2D e 33 righe 3D uguali ai congelati dopo il passaggio della vista alle
  derivazioni di Application (`ci/20261021-163125-s4-storica`).
- **Corse**: `ci/20261021-163801-s4-a` su `f99eaff8` (Architecture.Tests 566, Application.Tests 3 119, Presentation.Tests 3 823:
  PASS 7); `ci/20261021-164655-s4-b` sul ViewModel (Architecture.Tests 566, Presentation.Tests 3 898, `Presentation.Tests/sezione-ca` 59:
  PASS 6). La corsa finale del ramo è nella sezione di S5.

### 13.4 Comportamenti storici trovati in S4 (conservati, C5)

1. **Colonne per indice con le sollecitazioni uniche.** Le colonne nascoste delle tabelle SLE si scelgono per indice (6 e 7 le tensioni, 8 la
   fessurazione). Con le sollecitazioni uniche la colonna «Elemento» è la prima e gli indici cadono sulle colonne successive: la Rara nasconde
   «Tensioni» e mostra «Fessurazione»; la Frequente nasconde «|σs|» e mostra «Tensioni» quando verifica la fessurazione; la Quasi permanente
   nasconde «Tensioni» quando non la verifica; «Fessurazione» resta sempre visibile (fixture `sezione-a-t-sollecitazioni-uniche`).
2. **Valori grezzi nelle altre famiglie.** Dopo una modifica di n o di φ in una famiglia, le altre mostrano i valori come sono scritti nei dati
   («17.607108191890596»), la famiglia modificata il numero formattato («17,61»).
3. **Legenda della mappa** (famiglia del comportamento 1 di S3): la scrive il disegno sullo schermo. Una famiglia nascosta conserva la legenda
   del suo ultimo disegno anche dopo l'azzeramento dei risultati, e la cultura dei numeri segue il percorso (virgola nei passi dell'utente,
   punto dopo la pubblicazione di un calcolo sulla macchina del banco).
4. **Schede dei dettagli a -1.** Il controllo delle schede «Riepilogo / Barre e trefoli / Calcestruzzo / Fessurazione · passaggi» non ha una
   scheda scelta finché non è mostrato la prima volta (la colonna dei risultati apre su «Riepilogo verifiche»); la visita F3 lo rimette a -1.
5. **Sola lettura che scrive** (famiglia del comportamento 5): contorno della mappa e «Asse neutro» scrivono le opzioni con Modified anche
   nella revisione in sola lettura.

### 13.5 Domande «da decidere» (scelte provvisorie, da rivedere in F5.15)

- **S4-a · Legenda come dato.** `ConcreteSectionDrawingViewModel.Legend` è scritta dal ViewModel quando la famiglia è mostrata, come la
  scrive il disegno storico; la vista XAML disegnerà la sua con la stessa funzione di Application (`ContourScale`). Alternativa: nessuna
  proprietà, legenda solo nel disegno (le tracce la leggerebbero dal disegno).
- **S4-b · Colonne per indice** (comportamento 1): conservate. Da decidere al passaggio (S9): correggere per chiave come differenza dichiarata.
- **S4-c · Schede dei dettagli** (comportamento 4): `DetailTab` a -1 e selezione della prima scheda alla prima vista; la vista XAML dovrà
  legarla così, o fissare la prima scheda come differenza dichiarata.
- **S4-d · Arresto per occorrenza.** `ExpectedStops` indica passo e occorrenza del passo nella traccia; prima bastava il nome del passo.
- **S4-e · Errori dell'omogeneizzazione.** Il dizionario condiviso vive nel foglio (`HomogenizationErrors`) e i form lo leggono negli stessi
  momenti della vista storica: un avviso di una famiglia compare nelle altre solo quando si aggiornano.

### 13.6 Che cosa resta e come riprendere

- **S5 · Taglio e torsione** (sezione 12.6): `Actions.Shear`; campi della staffa circolare nascosti anche nei form delle staffe in sola
  lettura (della scheda del taglio e di quella dei dettagli: la parte `[taglio]` della traccia li scrive tutti e quattro).
- **S6–S9** come nella sezione 12.6. Al passaggio (S9) la vista XAML della scheda 04 lega la mappa (`ConcreteSectionViewport`) ai dati della
  famiglia (`Map.Changed`, `BarClicked`) e confronta gli elenchi di disegno delle mappe (`disegni/elenco.txt`).

```
dotnet build tests/ANTHEA.Presentation.Tests/ANTHEA.Presentation.Tests.csproj -c Release
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-rapide
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-tracce
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)' -Tag <tag>"
```

## 14. S5 fatto (21/10)

Sottopasso S5 «Taglio e torsione» (sezione 4, S5; sezione 12.6), stesso ramo `refactoring/f5-10-s4`, 21/10/2026, dopo S4. Decisioni applicate:
C2 (il taglio resta sul thread dell'interfaccia, dentro il lotto del calcolo), C5 (comportamenti storici conservati), C7 (tracce lunghe nello
stadio `ui`), C8 (la sezione del taglio resta un disegno WPF, confrontata come dati). La Release resta storica; nessuna vista XAML (S1-d).

### 14.1 Commit

| Commit | Contenuto |
| --- | --- |
| `f7ea07dd` | derivazioni della scheda del taglio in Application (`ConcreteShearPresentation`), chiamate dalla vista storica; Application.Tests |
| `5670695d` | taglio e torsione nel ViewModel (`ConcreteShearViewModel`), staffe in sola lettura delle schede 05 e 06, prove in `Presentation.Tests` |
| (questo commit) | questa sezione, riga di `piano.md`, esito della corsa finale |

### 14.2 Che cosa c'è

- **`ConcreteShearViewModel`** (`Shear` del foglio), creato all'apertura dopo le famiglie SLE, senza scritture (da `BuildShearPanel`,
  `RefreshAutomaticShear`, `UpdateShearSelection` e dai riepiloghi di `CalculateShear` di `ConcreteShear.cs`, da `BuildTorsionOptions` di
  `ConcreteTorsion.cs`):
  - le staffe in sola lettura («Staffe · dati comuni», `Stirrups`), il form del taglio su `taglio` (modello, parametri automatici o manuali,
    direzioni Vx e Vy, eccentricità del Model Code) con i campi abilitati e mostrati come la vista storica, il form della torsione e del
    modello circolare (`Torsion`), la nota dei parametri automatici;
  - una modifica del taglio passa da `ShearOptionChanged` del foglio: il modello con staffe riporta le staffe negli ingressi (con
    l'invalidazione del foglio), poi campi, parametri automatici, staffe dei disegni e ricalcolo del taglio; una modifica della torsione
    ricalcola il taglio con due Modified (comportamento 2);
  - la tabella è quella di S2 (`Actions.Shear`) con i suoi comandi;
  - dettaglio della riga selezionata, sintesi sotto la tabella, riepilogo delle verifiche (`Worst`) e sezione disegnata come dati (`Drawing`):
    il dettaglio e il disegno seguono la selezione solo mentre la scheda 05 è mostrata, come la griglia storica.
- **Staffe in sola lettura** (`ConcreteStirrupsViewModel`): una coppia di form nella scheda del taglio e una nella scheda dei dettagli
  (`DetailingStirrups` del foglio, che S6 prenderà nel suo ViewModel: la parte `[taglio]` della traccia scrive tutti e quattro i form). Ogni form
  delle staffe, anche questi, passa da `ConcreteInputViewModel.SynchronizeStirrupForm`: valori dai dati, campi spenti senza staffe, campi della
  staffa circolare nascosti per le sezioni da wizard.
- **Calcolo** (C2): il taglio resta nel foglio (`CalculateShear` di `ConcreteSheetViewModel.Checks.cs`), sul thread dell'interfaccia dopo i
  passi del lotto; il foglio scrive le uscite delle righe con `ConcreteShearPresentation.Outputs`, poi la sintesi, la prima riga selezionata,
  il dettaglio e i riepiloghi. I parametri automatici (`RefreshAutomaticShear`) raggiungono il form del taglio.
- **Wizard** (S1b): dopo «Applica sezione» il form del taglio mostra di nuovo le opzioni riscritte dal wizard, come `OpenTemplateWizard`.
- **Application** (`ConcreteShearPresentation`): parametri automatici, modello con staffe e Model Code, nota dei parametri automatici, uscite
  delle righe (taglio e torsione, o l'errore), sintesi, dettaglio della riga con la torsione. La vista storica le chiama al posto delle sue
  espressioni (`ConcreteShear.cs`, `ConcreteTorsion.cs`).
- **Baseline F4** e P3 invariate: il codice spostato non aveva voci.

### 14.3 Esiti

- **Tracce** (`Presentation.Tests/sezione-ca` lanciato a mano con `--sezione-tracce`, parte comune, `[ingressi]`, `[azioni]`, `[domini]`,
  `[sle]`, `[taglio]` e `[wizard]`): 49 scenari uguali alle tracce congelate (i 47 di S4 più `taglio/taglio-torsione` e
  `taglio/circolare-senza-staffe`; la parte `[taglio]` è confrontata anche in `percorso-f3/*`, `muri/*`, `esempi/*`, `ingressi/geometria-forme`,
  `ingressi/staffe-trefoli`, `azioni/editor-sollecitazioni`, `azioni/sollecitazioni-uniche` e `calcolo/*`). `calcolo/sola-lettura` è uguale
  fino all'arresto atteso di S4 (secondo «attendi», curva M–χ di S7). 61 controlli.
- **Prove rapide** (`--sezione-rapide`): 407 controlli; in più lo scenario `taglio/circolare-senza-staffe` fra le tracce rapide, F1 dei form del
  taglio, della torsione e delle staffe in sola lettura, la barriera del motore (il taglio aspetta il lotto trattenuto e mostra solo la
  revisione corrente), la sola lettura con i comandi della tabella, le staffe in sola lettura di una sezione da wizard, i due Modified della
  torsione, il modello con staffe, i parametri manuali, Reload.
- **Application.Tests**: 8 controlli di `ConcreteShearPresentation` (3 127), con valori scritti a mano dalla traccia storica congelata.
- **Vista storica**: `ui/check-sezione-historic` PASS dopo il passaggio della vista alle derivazioni di Application: 57 scenari e disegni uguali ai
  congelati (`ci/20261021-170857-s5-storica`).
- **Non verificato sullo stato finale** (fermo chiesto dal coordinatore il 21/10 sera): Architecture.Tests e l'intero Presentation.Tests (stadio
  `fast`) su `5670695d`, e la **corsa finale** `-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` del ramo, ancora da fare. Ultime
  corse complete di queste due suite: `ci/20261021-164655-s4-b` su S4 (PASS 6).

### 14.4 Comportamenti storici trovati in S5 (conservati, C5)

1. **Valori dei parametri automatici con la cultura del percorso** (famiglia del comportamento 1 di S3): all'apertura il form mostra «800,00»,
   dopo la pubblicazione di un calcolo «800.00» sulla macchina del banco.
2. **Torsione con due Modified**: una modifica della torsione invalida il taglio (un Modified) e ne aggiunge un secondo.
3. **Staffe assenti dal pannello di controllo**: il modello del taglio passa a «Senza staffe» solo come valore mostrato, senza ricalcolare i campi
   abilitati del form del taglio (angolo e cot θ restano come erano finché il form non cambia).
4. **Sezione del taglio senza messaggio**: il disegno della scheda 05 non riceve mai il messaggio della sezione, che resta «Inserire la geometria
   della sezione» anche con una sezione valida (non è visibile perché la sezione c'è), e prende la sezione solo quando la scheda è mostrata.
5. **Riepilogo delle verifiche scritto due volte**: dopo il calcolo la vista scrive il testo dei casi peggiori e lo sostituisce subito con le
   schede dei riepiloghi; il ViewModel scrive solo le schede (nessuna differenza visibile).
6. **Dettaglio «Dati modificati» dopo un calcolo**: il calcolo del taglio azzera il dettaglio e lo riscrive solo se la scheda 05 è mostrata;
   con la scheda nascosta il dettaglio resta «Dati modificati · aggiornamento automatico in attesa» anche con i risultati correnti.

### 14.5 Domande «da decidere» (scelte provvisorie, da rivedere in F5.15)

- **S5-d · Dettaglio dopo il calcolo dell'apertura.** Il calcolo azzera il dettaglio del taglio anche se la scheda è nascosta («Dati
  modificati · aggiornamento automatico in attesa» finché la scheda non viene mostrata): conservato (comportamento 6).
- **S5-a · Staffe in sola lettura della scheda 06.** Sono create con la scheda 05 (`DetailingStirrups` del foglio) perché la traccia del taglio le
  scrive; S6 le sposterà nel suo ViewModel. Alternativa: una sola coppia condivisa dalle due schede (la vista storica ne ha due).
- **S5-b · Sintesi del pannello di controllo.** Il foglio scrive ancora il testo del taglio nel riepilogo del pannello di controllo prima che i
  riepiloghi lo sostituiscano, come la vista storica; il testo dei casi peggiori no (comportamento 5).
- **S5-c · C2.** Confermato per F5.10: il calcolo del taglio resta sul thread dell'interfaccia. Spostarlo sullo sfondo è una differenza da
  dichiarare dopo S9.

### 14.6 Che cosa resta e come riprendere

- **S6 · Dettagli** (sezione 12.6): prende `DetailingStirrups` e la capsula come pilastro (traccia `esempi/wizard-capsula`).
- **S7, S8, S9** come nella sezione 12.6; `calcolo/sola-lettura` si ferma ancora al secondo «attendi» (curva M–χ, S7).
- **Prima di unire il ramo**: la corsa finale (sopra) e la ripetizione da sole delle suite note per i tempi o l'antivirus, se cadono.

```
dotnet build tests/ANTHEA.Presentation.Tests/ANTHEA.Presentation.Tests.csproj -c Release
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-rapide
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-tracce
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)' -Tag <tag>"
```

## 15. S6 fatto (22/10)

Sottopasso S6 «Dettagli costruttivi e ancoraggi» (sezione 4, S6; sezione 12.6), ramo `refactoring/f5-10-s6` da `refactoring/f5` `5451a8cc` (S4,
S5 e la correzione P3 del taglio uniti), 22/10/2026, di giorno: niente stadio `ui` né harness WPF (regola del giorno dell'utente). Decisioni
applicate: C5 (comportamenti storici conservati), C7 (tracce lunghe nello stadio `ui`), S5-a (le staffe in sola lettura della scheda 06 passano
al ViewModel dei dettagli). La Release resta storica; nessuna vista XAML (S1-d).

### 15.1 Commit

| Commit | Contenuto |
| --- | --- |
| `2146e171` | derivazioni rimaste nella scheda dei dettagli in Application (`ConcreteDetailingPresentation`), chiamate dalla vista storica; Application.Tests |
| `38ca4549` | dettagli e ancoraggi nel ViewModel (`ConcreteDetailingViewModel`), prove in `Presentation.Tests` |
| (commit della documentazione) | sezioni 15–17, righe di `piano.md`, esiti delle corse standard |

### 15.2 Che cosa c'è

- **`ConcreteDetailingViewModel`** (`Detailing` del foglio, scheda 06), creato all'apertura dopo il taglio, senza scritture (da
  `BuildDetailingPanel` e `RefreshDetailing` di `ConcreteDetailing.cs`):
  - i cinque form su `dettagli_costruttivi` nell'ordine della vista storica (`Forms`): elemento; inerte, esposizione condivisa SLE (calcolata),
    vita utile, controllo dei copriferri, cmin,dur (calcolato), Δcdev; armatura (giunzione, barre ortogonali, zona critica); barre compresse
    trattenute; appoggi;
  - il copriferro netto degli ingressi in sola lettura (`CoverGeometry`), le staffe in sola lettura (`Stirrups`, prima `DetailingStirrups`
    del foglio, S5-a) e il form dell'ancoraggio o della sovrapposizione (`Anchorage`, su `ancoraggi`);
  - riepilogo dei dettagli, verifiche dei quattro temi nell'ordine delle righe della scheda (`Topics`: copriferro, armatura, staffe,
    ancoraggi), verifiche dell'ancoraggio e nota della durabilità;
  - «Aggiorna verifiche ancoraggi» (conferma delle bozze dell'ancoraggio, poi ricalcolo) e «Modifica … nel pannello di controllo →» (scheda
    01): spenti in sola lettura, come i pulsanti storici.
- **Callback storiche**: ogni modifica dei form ricalcola i dettagli con Modified; l'inerte massimo, condiviso con il taglio, ricalcola anche il
  taglio; l'ancoraggio ricalcola i dettagli con Modified.
- **Foglio** (`ConcreteSheetViewModel.Detailing.cs`, spostato da `ConcreteSheetViewModel.Checks.cs`): `RefreshDetailing` mostra i campi
  dell'elemento e della sovrapposizione, calcola i dettagli dai dati e dalle righe delle azioni (`ConcreteDetailingPresentation.Calculate`,
  già in Application), scrive i valori derivati nei dati, azzera l'esportazione e mostra le verifiche; si chiama negli stessi punti di prima
  (ogni cambio di scheda, ogni invalidazione, le staffe, le opzioni SLE comuni).
- **Componenti comuni**, aggiunta: `ConcreteCardsViewModel.Fill`, il `Fill` di `RefreshDetailing` (dettagli come riquadri, altrimenti il testo).
- **Application** (`ConcreteDetailingPresentation`): campi della sovrapposizione (`LapFields`, `Lap`), campi mostrati per elemento
  (`ElementFieldShown`), testo di cmin,dur (`DurabilityCoverText`). La vista storica le chiama al posto delle sue espressioni.
- **Baseline F4** e P3 invariate: il codice spostato non aveva voci.

### 15.3 Esiti

- **Tracce** (`--sezione-tracce`, parte comune, `[ingressi]`, `[azioni]`, `[domini]`, `[sle]`, `[taglio]`, `[dettagli]` e `[wizard]`): 51 scenari
  uguali alle tracce congelate (i 49 di S5 più `dettagli/dettagli-ancoraggi` e `dettagli/soletta`; la parte `[dettagli]` è confrontata anche in
  `percorso-f3/*`, `muri/*`, `esempi/*` (compresa la capsula come pilastro di `esempi/wizard-capsula`), `sle/opzioni-sle` e
  `calcolo/sola-lettura`). `calcolo/sola-lettura` è ancora uguale fino all'arresto atteso di S4 (secondo «attendi», curva M–χ di S7). PASS 63
  (`supporto/artefatti/f5-10-s6/s6-tracce`; base prima di S6: PASS 61, `base-tracce`).
- **Prove rapide** (`--sezione-rapide`): 441 controlli; in più lo scenario `dettagli/soletta` fra le tracce rapide, la bozza dell'ancoraggio
  scritta da «Aggiorna verifiche ancoraggi» con un Modified, la sovrapposizione, la soletta (campi e esposizione condivisa), l'inerte condiviso
  con il taglio, il pannello di controllo, Reload, la sola lettura, F1 dei form della scheda 06.
- **Application.Tests**: 4 controlli di `ConcreteDetailingPresentation` con valori scritti a mano.
- **Vista storica**: non verificata di giorno (regola del giorno): `ui/check-sezione-historic` va eseguita stasera.
- **Corse** su `38ca4549`: quick `ci/20261022-090334-s6-quick` PASS 55, NEW-FAIL 0; standard `ci/20261022-093131-s6-standard` PASS 77
  (build, fast, regression e wiki), KNOWN 0, NEW-FAIL 0; Presentation.Tests 4 278.

### 15.4 Comportamenti storici trovati in S6 (conservati, C5)

1. **Esposizione condivisa vuota all'apertura.** Finché l'elemento è «Da scegliere» il calcolo dei dettagli si ferma prima dell'esposizione: il
   campo resta vuoto e `esposizione_sle` non si scrive nei dati; si scrive al primo ricalcolo con un elemento scelto.
2. **Ricalcolo doppio di «Aggiorna verifiche ancoraggi».** Il pulsante conferma le bozze (ognuna ricalcola i dettagli con Modified) e poi
   ricalcola ancora.
3. **Pulsanti della scheda spenti in sola lettura**, compresi i due che portano al pannello di controllo, che non scrivono nulla.

### 15.5 Domande «da decidere» (scelte provvisorie, da rivedere in F5.15)

- **S6-a · Pulsanti di navigazione in sola lettura.** «Modifica geometria / armature nel pannello di controllo →» restano spenti nella revisione
  in sola lettura come oggi (comportamento 3), anche se cambiano solo la scheda. Alternativa: lasciarli attivi come differenza dichiarata.
- **S6-b · Temi come elenco.** I quattro temi sono un elenco ordinato (`Topics`) con chiave, titolo e riquadri, al posto del dizionario della vista
  storica; la vista XAML li dispone in righe come oggi.

### 15.6 Che cosa resta e come riprendere

- **S7 e S8** (sezioni 16 e 17).
- **S9 · Report e passaggio** come nella sezione 4.
- Stasera: `ui/check-sezione-historic` (la vista storica chiama le funzioni nuove di `ConcreteDetailingPresentation`).

```
dotnet build tests/ANTHEA.Presentation.Tests/ANTHEA.Presentation.Tests.csproj -c Release
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-rapide
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-tracce
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Profile standard -Tag <tag>"
```

## 16. S7 fatto (22/10)

Sottopasso S7 «Ispezione e momento–curvatura» (sezione 4, S7; sezione 12.6), stesso ramo `refactoring/f5-10-s6`, 22/10/2026, dopo S6.
Decisioni applicate: C5 (comportamenti storici conservati, compresa la curva calcolata in sola lettura), C7 (tracce lunghe nello stadio `ui`),
C8 (mappa σ/ε, piano delle deformazioni e curva restano disegni WPF, confrontati come dati). Ognuno conserva la sua sessione o il suo
scheduler: una `ConcreteInspectionSession` per pannello, il `RecalculationScheduler` della curva. La Release resta storica; nessuna vista XAML.

### 16.1 Commit

| Commit | Contenuto |
| --- | --- |
| `739cfbb9` | derivazioni della sezione σ/ε e della curva in Application (`ConcreteInspectionPresentation`, `ConcreteCurvaturePresentation`), chiamate dalla vista storica; Application.Tests |
| `76ad2cab` | sezione σ/ε e momento–curvatura nel ViewModel, correzione di `IsShown` dei pannelli dei domini, prove in `Presentation.Tests` |
| (commit della documentazione) | sezioni 15–17, righe di `piano.md`, esiti delle corse standard |

### 16.2 Che cosa c'è

- **`ConcreteSectionInspectionViewModel`** (`Inspection` di ogni pannello dei domini, da `SectionInspection`, `BuildSectionInspection` e
  `UpdateSectionInspection` di `ConcreteInspection.cs`): soluzione mostrata (`State`: azione inserita o punto limite, all'apertura il punto
  limite) e contorno della mappa (`Contour`, non salvato, senza Modified), entrambi usabili in sola lettura; messaggio, valori, mappa σ/ε
  (`Map`, `ConcreteSectionDrawingViewModel`) e piano delle deformazioni (`Plane`, `ConcreteStrainDiagram`) come dati; «Proprietà…» (spento in
  sola lettura). Ogni aggiornamento abbandona la soluzione in corso, azzera mappa, piano e valori (la mappa prende la sezione del pannello di
  controllo) e, con la scheda «Sezione σ / ε» scelta e una riga calcolata selezionata, legge il piano del punto limite o analizza l'azione con
  la sessione del pannello; pubblica solo la richiesta corrente sui dati correnti (revisione del calcolo invariata).
- **Quando si aggiorna**, come la vista storica: a ogni `UpdateSelection` del pannello (tabella visibile), a ogni cambio della soluzione e
  della scheda interna, a ogni invalidazione del foglio (prima dell'anteprima).
- **Pannelli dei domini** (S3, correzione): `IsShown` dipende solo dalla scheda principale. La tabella storica sta sotto le schede interne,
  quindi resta visibile anche con «Sezione σ / ε»: lo prova la traccia `ispezione/sezione-sigma-epsilon` (dopo un calcolo la sezione σ/ε del
  2D si aggiorna con la scheda interna «Sezione σ / ε»). La scheda interna (`InspectionTab`) vale -1 finché il pannello non è mostrato la prima
  volta, poi 0 «Dominio», come il TabControl storico; ogni cambio aggiorna la sezione σ/ε. «Asse neutro» e «Valori σ/ε sulle barre» arrivano
  alla mappa.
- **`ConcreteCurvatureViewModel`** (`Curvature` del foglio, scheda 07, da `ConcreteCurvature.cs`): form del percorso di carico su
  `momento_curvatura` («CLS teso nelle risposte» mostra il suo predefinito senza scriverlo), «Calcola curva», «Interrompi» ed «Esporta CSV…»
  (disponibili in sola lettura, come i pulsanti storici), testo e grafico (`Chart`: titoli, punti χ–M) come dati. La curva si calcola su una
  copia dei dati con lo scheduler proprio; con gli stessi dati non si ricalcola; ogni modifica dei dati o del form la azzera; quella pubblicata
  entra nell'esportazione dei risultati. Le prove usano `IsRunning` dei due ViewModel per aspettare la fine come la traccia storica.
- **Application**: `ConcreteInspectionPresentation` (soluzioni, messaggi, valori della soluzione) e `ConcreteCurvaturePresentation` (messaggi,
  firma, titoli e punti del grafico, testo, CSV). La vista storica le chiama (`ConcreteInspection.cs`, `ConcreteCurvature.cs`,
  `PlotPresentation.cs`: titoli e punti della curva, gli stessi per la figura M–χ del report).
- **Baseline F4** e P3 invariate.

### 16.3 Esiti

- **Tracce** (`--sezione-tracce`, ora anche `[ispezione]`): 53 scenari uguali alle tracce congelate (i 51 di S6 più `ispezione/sezione-sigma-epsilon`
  e `ispezione/momento-curvatura`; la parte `[ispezione]` è confrontata anche in `percorso-f3/*`, `muri/*`, `esempi/*` e `calcolo/sola-lettura`).
  `calcolo/sola-lettura` è uguale fino in fondo: nessun arresto atteso (l'unico passo di S8 rimasto, un campo della ricerca in sola lettura, non
  ha effetti). Domini 3D come dati uguali. PASS 65 (`supporto/artefatti/f5-10-s6/s7-tracce`).
- **Prove rapide** (`--sezione-rapide`): 459 controlli; in più `ispezione/sezione-sigma-epsilon` fra le tracce rapide; scheda interna a -1 prima della
  prima vista; punto limite e azione inserita; soluzione abbandonata mai pubblicata; asse neutro (con Modified), valori sulle barre e contorno;
  dati cambiati che azzerano le due sezioni e la soluzione di nuovo dopo il calcolo; curva calcolata ed esportata nel risultato, non
  ricalcolata con gli stessi dati, CSV, form che la azzera, «Interrompi», sola lettura.
- **Application.Tests**: 7 controlli con valori scritti a mano dalla traccia storica congelata (`ispezione/momento-curvatura`: MRd = 800,027
  kNm, χy, χu, μχ = 7,085, 62 punti). Architecture.Tests PASS 573.
- **Corsa standard**: S7 e S8 sono stati provati mentre girava la standard di S6, compilando in cartelle separate
  (`-p:OutDir=supporto/artefatti/f5-10-s6/bin-*`) per non toccare i binari della corsa; la standard che li copre è quella sul commit di S8
  (sezione 17.3).

### 16.4 Comportamenti storici trovati in S7 (conservati, C5)

1. **Messaggio della mappa σ/ε mai scritto**: la mappa mostra sempre «Inserire la geometria della sezione» come messaggio (non visibile con la
   sezione), come la sezione del taglio (comportamento 4 di S5).
2. **Messaggio rimasto dopo un aggiornamento abbandonato**: tornando alla scheda «Dominio» mentre la soluzione è in lettura (la visita F3), la
   soluzione non si pubblica ma il messaggio resta «Lettura del piano già calcolato al limite…».
3. **Sezione σ/ε azzerata con la sezione di prima**: un'invalidazione azzera le due sezioni σ/ε prima di aggiornare l'anteprima, quindi la mappa
   tiene la sezione precedente finché il pannello non la aggiorna (dopo il calcolo, solo se il pannello è mostrato).
4. **Curva in sola lettura**: «Calcola curva» è disponibile nella revisione in sola lettura, conferma il foglio e la curva entra nel risultato
   esportato (il vecchio arresto atteso di `calcolo/sola-lettura`).
5. **«Calcolo interrotto» dopo una modifica**: una modifica durante il calcolo della curva scrive «Dati modificati · ricalcolare la curva.», poi il
   calcolo abbandonato lo sostituisce con «Calcolo interrotto; nessuna curva parziale pubblicata.».

### 16.5 Domande «da decidere» (scelte provvisorie, da rivedere in F5.15)

- **S7-a · Correzione di S3.** `IsShown` dei pannelli dei domini non dipende più dalla scheda interna (sezione 16.2): le tracce di S3 restano
  uguali (nessuno scenario di S3 seleziona la scheda interna con la tabella visibile). Da confermare all'unione.
- **S7-b · Scheda interna a -1.** Il ViewModel espone -1 finché il pannello non è mostrato, come il TabControl storico; la vista XAML dovrà
  legarla così o fissare «Dominio» fin dall'apertura come differenza dichiarata (come S4-c).
- **S7-c · Messaggi rimasti** (comportamenti 1 e 2): conservati. Alternativa al passaggio: il messaggio della sezione del pannello di controllo e
  l'azzeramento del messaggio quando la scheda interna non è «Sezione σ / ε».
- **S7-d · Curva in sola lettura** (comportamento 4): conservata, come T6 per le chiavi di stato.

## 17. S8 fatto (22/10)

Sottopasso S8 «Progetto delle armature» (sezione 4, S8; sezione 12.6), stesso ramo, 22/10/2026, dopo S7. Decisioni applicate: C5, C7, C8 (la
sezione della proposta resta un disegno WPF, confrontata come dati). La Release resta storica; nessuna vista XAML.

### 17.1 Commit

| Commit | Contenuto |
| --- | --- |
| `6f0e2165` | derivazioni della ricerca in Application (`ConcreteDesignPresentation`), chiamate dalla vista storica; baseline F4 spostata; Application.Tests |
| `bc3703e7` | progetto delle armature nel ViewModel (`ConcreteDesignViewModel`), prove in `Presentation.Tests` |
| `88ebf046` | F1 dei form della curva e della ricerca nella prova della Wiki |
| (commit della documentazione) | questa sezione, righe di `piano.md`, esiti delle corse standard |

### 17.2 Che cosa c'è

- **`ConcreteDesignViewModel`** (`Design` del foglio, scheda 08, da `ConcreteDesign.cs`), creato all'apertura dopo la preparazione delle sue opzioni,
  senza scritture:
  - form della ricerca su `calcola_armature` con i campi mostrati per forma della sezione, elemento dei dettagli, obiettivo, staffe e ricerca
    completa (`Form`), limiti η max di ogni verifica (`Limits`), «Mostra le prime» (`Shown`);
  - «Calcola armature» (`RunCommand`, asincrono e annullabile): conferma del foglio, risultati azzerati, ricerca su una copia dei dati con
    `Task.Run`, avanzamento (`Progress`, `Status`, la migliore proposta mostrata) solo finché la ricerca è corrente; poi stato, alternative,
    scarti e tempi, o «Ricerca non avviata: …», o «Ricerca interrotta.». «Ferma ricerca» (`StopCommand`) annulla;
  - alternative (`Ranking`, `ConcreteTableViewModel` in sola lettura), proposta selezionata con descrizione, metriche, ambito, verifiche e
    sezione con le staffe (`Preview`) come dati;
  - «Applica configurazione» (`ApplyCommand`): conferma del foglio e controllo della ricerca, poi `ConcreteDesignPresentation.Apply` scrive la
    proposta nei dati (gli stessi oggetti, svuotati e riempiti), il foglio rilegge i form della geometria, dell'armatura, dei secondi strati, dei
    dettagli, dell'ancoraggio, del taglio e della torsione, svuota le barre e si invalida (`DesignApplied`), lo stato dice che cosa è applicato;
  - ogni `Modified` del foglio (come `Modified += RefreshDesignFreshness` della vista storica) rinnova i campi mostrati e segna superata una
    ricerca i cui dati sono cambiati («Applica» spento, ricerca in corso annullata);
  - i tre comandi e i pulsanti «Sezione →», «Dettagli →», «SLE / esposizione →» sono spenti in sola lettura, come i pulsanti storici.
- **Application** (`ConcreteDesignPresentation`): campi mostrati, firma dei dati, frazione e testo dell'avanzamento, stato, copertura, scarti,
  descrizione senza configurazioni, righe delle alternative, metriche, ambito, verifiche, applicazione ai dati, testi fissi. La vista storica le
  chiama al posto delle sue espressioni.
- **Baseline F4**, solo spostamenti: le due voci R4 di `ConcreteDesign.cs` (`ConcreteWorkspace.RefreshDesignRanking`, `DesignOptions.D("mostra",
  10)`; `ConcreteWorkspace.SelectDesign`, `c.Detailing.D("as_secondaria")`) diventano le due di `ConcreteDesignPresentation` (`RankingRows`,
  `Scope`); 2 153 voci e 3 206 occorrenze invariate, nessuna aggiunta. P3 invariata.

### 17.3 Esiti

- **Tracce** (`--sezione-tracce`, ora anche `[armature]`): 55 scenari uguali alle tracce congelate (i 53 di S7 più `armature/ricerca-armature` e
  `armature/opzioni-ricerca`; la parte `[armature]` è confrontata anche in `percorso-f3/*`, `muri/*`, `esempi/*` e `calcolo/sola-lettura`). Da S8
  ogni parte della traccia è confrontata in tutti gli scenari dei sottopassi S1–S8; restano fuori solo gli scenari `report/*` (S9). PASS 67
  (`supporto/artefatti/f5-10-s6/s8-tracce`).
- **Prove rapide** (`--sezione-rapide`): 471 controlli; in più `armature/opzioni-ricerca` fra le tracce rapide; campi della ricerca che seguono i
  dettagli; ricerca rifiutata (elenco non valido, esposizione mancante); ricerca breve sulla soletta della traccia con proposta, dati cambiati,
  ricerca di nuovo e «Applica configurazione» (proposta nei dati, conferme da rifare, foglio invalidato, form riletti); «Ferma ricerca»; Reload;
  sola lettura.
- **Application.Tests**: 9 controlli di `ConcreteDesignPresentation` (testi e campi scritti a mano, una ricerca breve sulla soletta). Application.Tests
  3 116, Architecture.Tests 574.
- **Corsa standard** su `bc3703e7` (S7 e S8): `supporto/artefatti/ci/20261022-095402-s8-standard`, PASS 77 (build, fast, regression e wiki), KNOWN 0,
  NEW-FAIL 0, nessun file tracciato modificato durante la corsa; Presentation.Tests 4 308, Application.Tests 3 116, Architecture.Tests 574.
  Dopo la corsa, il commit di sole prove `88ebf046` porta F1 anche ai form della curva e della ricerca (`--sezione-rapide` 518,
  Presentation.Tests 4 355).
- **Vista storica**: non verificata di giorno (regola del giorno): le suite `ui/*` della sezione 18 vanno eseguite stasera.

### 17.4 Comportamenti storici trovati in S8 (conservati, C5)

1. **Stato dopo «Applica»**: l'invalidazione del foglio segna superata la ricerca (stato «Dati modificati…»), poi lo stato è sostituito da
   «Configurazione applicata…»: «Applica» resta spento, ma lo stato non dice che la ricerca è superata.
2. **Stato durante la ricerca**: all'avvio cambia solo la descrizione («Ricerca in corso…»); lo stato resta quello precedente (anche «Ricerca non
   avviata: …») finché non arriva il primo avanzamento.
3. **Campi della ricerca rinnovati a ogni Modified**, anche per modifiche che non li riguardano (tabella, schede).
4. **Avanzamento in ritardo scartato**: un avanzamento che arriva dopo la fine, dopo un annullamento o dopo una modifica non cambia nulla.

### 17.5 Domande «da decidere» (scelte provvisorie, da rivedere in F5.15)

- **S8-a · Ascolto di `Modified`.** Il ViewModel si iscrive a `Modified` del foglio come la vista storica, invece di una chiamata esplicita del
  foglio; si disiscrive alla chiusura e al Reload.
- **S8-b · Pulsanti di navigazione in sola lettura.** Spenti come oggi (come S6-a).
- **S8-c · Stato dopo «Applica»** (comportamento 1): conservato. Alternativa: dire nello stato che la ricerca va ripetuta.

## 18. Da dove parte S9 e come riprendere

- **S9 · Report, riepiloghi e passaggio** (sezione 4, S9): il report (`PendingReport` del foglio), i riepiloghi, gli scenari `report/*`, poi la
  vista XAML e il passaggio della Release con l'utente (i report sono congelati dall'8/10).
- **Stasera, con l'interfaccia** (regola del giorno del 22/10): `ui/check-sezione-historic` (la vista storica chiama le derivazioni nuove di S6,
  S7 e S8), `ui/smoke-ca-extensions`, `ui/smoke-ca-features`, `ui/smoke-neutral-axis`, `ui/smoke`, `ui/check-concrete-design`,
  `ui/f3-report-chapters`, `ui/f3-concrete-figures` (figura M–χ), `ui/f3-scheduling`, `ui/check-field-form-parity`, `ui/f3-equivalence` e
  `Presentation.Tests/sezione-ca` (già PASS 67 di giorno con `--sezione-tracce`).

```
dotnet build tests/ANTHEA.Presentation.Tests/ANTHEA.Presentation.Tests.csproj -c Release
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-rapide
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-tracce
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Profile standard -Tag <tag>"
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)' -Tag <tag>"
```

## 19. S9 fatto (22/10)

Sottopasso S9 «Report, riepiloghi» (sezione 4, S9; sezione 18), parte ViewModel, ramo `refactoring/f5-10-s9` da `refactoring/f5` `790b9081`
(S6–S8 uniti), 22/10/2026, di giorno: niente stadio `ui` né harness WPF (regola del giorno). Decisioni applicate: report congelati dall'8/10
(nessun generatore, capitolo o testo cambiato), C5 (comportamenti storici conservati), C7 (tracce lunghe nello stadio `ui`), C8 (i disegni
del report restano WPF, alimentati dai dati del ViewModel). La Release resta storica. Il passaggio della Release (sezione 6, punto 6) è fuori
da S9 per decisione dell'utente del 22/10 («i test per il passaggio alla release li facciamo a fine refactoring»).

### 19.1 Commit

| Commit | Contenuto |
| --- | --- |
| `1802538b` | report Word e breve nel ViewModel, disegni del report come dati, report breve e risultati JSON nell'ospite, prove |
| (commit della documentazione) | questa sezione, riga di `piano.md` |

### 19.2 Che cosa c'è

- **Riepiloghi del pannello di controllo** (`summaries`, `RefreshSummary`, `CollectVerificationSummaries`): erano già nel foglio da S1
  (`ConcreteSheetViewModel.Checks.cs`, `Summaries` e `ShearDashboard` su `ConcreteCardsViewModel`); S9 non li cambia. Le tracce li confrontano in
  ogni scenario (riquadri del riepilogo nella parte `[ingressi]`).
- **Report Word** (`ConcreteSheetViewModel.Report.cs`, da `BuildReportDocument` di `ConcreteReport.cs`), nello stesso ordine della vista storica:
  conferma del foglio; rifiuto con il messaggio storico se il calcolo è in corso, in coda o senza risultati; le sezioni scelte salvate in
  `report_sezioni`, in ordine, con Modified (non nel report del progetto); dettagli ricalcolati (`RefreshDetailing`), esportazione letta di nuovo e
  arricchita dei dati del foglio (`dati`); il pannello del dominio 2D aggiornato anche se nascosto quando la sua figura è chiesta e il dominio
  esiste (`RefreshPanel(renderHidden: true)`); poi `ReportConcrete.CreateDocument` con le figure. Il capitolo del report del progetto passa ancora
  per `ModuleReportService` e `ConcreteReportAnalysis` (capitolo autonomo, invariato).
- **Report breve**: conferma, rifiuto con il messaggio storico, `ReportConcreteShort.Create` sui dati di calcolo e sull'esportazione.
- **Figure** (Q3, C8): il foglio dà al disegnatore i dati semplici dei suoi disegni nel momento del report (`ConcreteReportDrawings`: dati,
  esportazione, anteprima del pannello di controllo, i due pannelli dei domini con le righe mostrate in ordine, la selezione, domini e verifiche
  dello stato limite, il grafico 2D con il suo zoom; le tre famiglie SLE con mappa, esiti e righe; la curva M–χ pubblicata).
  `IConcreteSectionFigureRenderer` è in Presentation; nel desktop `ConcreteSectionFigureRenderer` (`X.Desktop/Wpf/ConcreteSectionDrawings.cs`)
  costruisce i disegni WPF della vista storica da quei dati e chiama `ModuleFigureRenderer.ConcreteFigures`, invariato. Le stesse funzioni di
  `ConcreteSectionDrawings` (sezione disegnata e grafico 2D dai dati) servono alla vista XAML.
- **Ospite** (`MvvmSheetEditor`): il report breve passa da `IModuleShortReport` (interfaccia nuova, accanto a `IModuleReportPdf`); i
  risultati JSON della sezione c.a. hanno il messaggio storico e i dati del foglio, come `SheetEditor.ExportResult`.
- **Modulo**: `ConcreteSectionSheetModule` riceve il disegnatore delle figure, come la sezione composta (F5.11); `PendingReport` è tolto.

### 19.3 Esiti

- **Tracce** (`--sezione-tracce`): **57 scenari uguali alle tracce congelate, ogni parte in ogni scenario**, compresi `report/disponibilita` e
  `report/dati-incompleti` (report completo e breve con il calcolo in coda, disponibili dopo il calcolo, di nuovo in coda dopo una modifica,
  rifiutati con i dati non validi; il dominio 2D nascosto disegnato dal report). Nessun arresto atteso. PASS 70
  (`supporto/artefatti/f5-10-s9/s9-tracce-1`).
- **Percorso F3 completo** (stesso comando): per le 11 fixture il ViewModel fa quello che fa la cattura F3 (aperto, visitato, calcolato,
  risultato, report e report breve con testo e struttura, stato, dati esportati, riaperto). 121 file confrontati: 108 uguali byte per byte al
  riferimento della vista storica nella forma canonica della cattura, compresi `exported-data` e `reopened` interi con `report_sezioni`; 13 con
  transizioni F4 (le quattro fixture precedenti a F5.10) uguali entro le transizioni ammesse, con il confronto di `ui/f3-equivalence`. Le
  immagini delle figure non sono confrontate qui (solo le didascalie, nel testo, e le dimensioni, nella struttura): si confrontano stasera con
  `ui/f3-report-chapters` e con il banco (sezione 20).
- **Prove rapide** (`--sezione-rapide`): PASS 541 (518 prima di S9); in più `report/disponibilita` fra le tracce rapide e i controlli del report:
  rifiuti con i messaggi storici, sezioni salvate in ordine con un Modified, report del progetto senza scritture, esportazione con i dati,
  disegni chiesti una volta (domini 3D e 2D in ordine con righe e selezione della tabella, famiglie SLE, anteprima, curva), dominio 2D nascosto
  disegnato, report breve uguale al generatore, nessuna richiesta all'ospite, curva pubblicata nei disegni, sola lettura, foglio senza disegnatore,
  chiusura.
- **Architecture.Tests** PASS 574 (`s9-arch-2`). Baseline F4 e P3 invariate.

### 19.4 Comportamenti storici trovati in S9 (conservati, C5)

1. **Il report cambia l'esportazione in cache**: aggiunge `dati` all'oggetto dei risultati, che resta così fino all'azzeramento successivo;
   l'impronta del risultato cambia dopo il report (`report/disponibilita`).
2. **Il report aggiorna il pannello del dominio 2D nascosto** (dettaglio, grafico, sezione σ/ε) quando la figura del 2D è chiesta
   (`report/dati-incompleti`).
3. **Report in sola lettura**: disponibile, e salva `report_sezioni` con Modified (famiglia del comportamento 5 della sezione 2).
4. **Tre messaggi diversi** per lo stesso rifiuto: quello della finestra («…correggere gli eventuali dati non validi.»), quello del report
   («…correggere i dati non validi prima di esportare il report.») e quello del report breve («Attendere l’aggiornamento delle verifiche prima di
   esportare.»).
5. **Modified anche con le stesse sezioni**: il report salva `report_sezioni` e segnala Modified anche quando le sezioni non cambiano.

### 19.5 Domande «da decidere» (scelte provvisorie, da rivedere in F5.15)

- **S9-a · Zoom e spostamento dei disegni nel report.** La vista storica disegna il report dai suoi controlli: lo zoom e lo spostamento fatti col
  mouse sull'anteprima, sulle mappe SLE e sulla curva M–χ entrano nelle figure. Il ViewModel dà i dati dei disegni, non lo stato della vista:
  le figure sono disegnate adattate, come nel capitolo autonomo del progetto; il grafico 2D porta lo zoom del pannello (`Zoom`, che la vista
  XAML aggiorna). Nessuna traccia lo copre. Alternativa: la vista registra i suoi disegni vivi presso il disegnatore.
- **S9-b · `IModuleShortReport`.** Interfaccia nuova di Presentation per il report breve, accanto a `IModuleReportPdf` (T8). Alternativa: un
  membro di `IModuleReportSource` con un'implementazione predefinita.
- **S9-c · Foglio senza disegnatore.** Il costruttore senza disegnatore (usato solo dalle prove) rifiuta i report con «grafici» e scrive gli
  altri senza figure; il modulo lo richiede sempre.

### 19.6 Che cosa resta e come riprendere

- **Vista XAML** (sezione 20).
- **Stasera** (regola del giorno): le suite della sezione 18 e quelle della sezione 20.
- **Passaggio della Release** con l'utente, a fine refactoring: sezione 6, punto 6, e la ricetta di [f5-ripresa.md](f5-ripresa.md).

```
dotnet build tests/ANTHEA.Presentation.Tests/ANTHEA.Presentation.Tests.csproj -c Release
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-rapide
dotnet tests/ANTHEA.Presentation.Tests/bin/Release/net8.0/ANTHEA.Presentation.Tests.dll <cartella> --sezione-tracce
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Profile standard -Tag <tag>"
```
