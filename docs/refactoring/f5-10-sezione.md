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
- che cosa resta e le domande per il punto di controllo (sezioni 6 e 7).

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
