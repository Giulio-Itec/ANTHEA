# F5.7 — Palo e micropalo orizzontale: preparazione

## Scopo

Questa nota chiude la **preparazione** di F5.7: moduli `geo_palo_orizzontale` e `geo_micropalo_orizzontale`. Oggi entrambi
passano da `X.Desktop/Wpf/HorizontalWorkspace.cs` e dai file collegati (9 file, 959 righe):
- `InputForm` e `JsonGrid`;
- schede impaginate su un `Canvas`, con «Estendi»/«Riduci»;
- la tabella degli strati con l'editor della reazione orizzontale del terreno (`HorizontalSoilEditor`);
- i diagrammi di «Diagrammi e dettagli» e del report (`HorizontalDiagrams`), la sezione c.a. o CHS disegnata (`ChsDrawing`);
- il calcolo sullo sfondo con `RecalculationScheduler`, ritardo di 450 ms;
- una seconda scheda, «Risposta elastica · trave su molle» (`ElasticPileWorkspace`), che è il passo F5.8.

La nota segue i passi 1–5 della «Ricetta per i moduli successivi» di [f5-4-pilota.md](f5-4-pilota.md). Contiene:
- che cosa esiste;
- i comportamenti della vista storica trovati con le tracce, e i difetti;
- che cosa resta da fare;
- le domande per il punto di controllo F5.7.

La vista XAML, il passaggio in `ModuleImplementations` e l'archiviazione vengono **dopo** il punto di controllo. La risposta
elastica e le armature del palo (F5.8) non fanno parte di questo passo: il ViewModel copre la scheda della capacità e la parte
dell'elastico che agisce sui dati da quella scheda.

Riferimenti:
- [proposta](f5-proposta-architettura.md), sezioni 3, 4.5, 5, 7 e righe F5.7–F5.8 della sezione 8;
- [inventario](f5-inventario-ui.md), sezioni 1.6 e 3.5;
- `tests/fixtures/f3/README.md`, `tests/fixtures/f5-palo-orizzontale/README.md`.

Ramo `refactoring/f5-7-orizzontale`, da `refactoring/f5` `92613e60`, 16–17/10/2026.

## 1. Che cosa esiste dopo la preparazione

| Commit | Contenuto |
| --- | --- |
| `d3b0750a` | 6 fixture F3 nuove (3 palo, 3 micropalo; 16 in tutto, 67 nel riferimento), catturate con la vista storica |
| `6ab9e310` | tracce congelate della vista storica (22 scenari) ed elenchi di disegno (40), suite `ui/check-palo-orizzontale-historic` |
| `f077fe02` | derivazioni in Application (`HorizontalPilePresentation`, `HorizontalPileDiagrams`), campi condivisi in Presentation (`HorizontalPileFields`), baseline F4 3349 → 3344 |
| `bae4cf30` | ViewModel `src/ANTHEA.Presentation/Modules/HorizontalPile`, moduli registrati, ganci additivi di `SheetViewModelBase`, diagrammi congelati, prove senza WPF |

### 1.1 Fixture F3

Delle 10 fixture esistenti calcolavano la capacità solo le due `single-soil-layer` (una stratigrafia granulare, Broms, testa
libera); le altre sono default incompleti e fixture della risposta elastica. Le 6 nuove coprono, tutte sulla scheda della capacità:
- Broms con testa impedita in terreni coesivi: palo corto, meccanismo corto in una stratigrafia e intermedio nell'altra;
- il metodo stratificato su sequenze miste (modello sperimentale), con il palo e con il micropalo;
- falda con il suo peso unitario, forza assiale, eccentricità, più stratigrafie;
- l'efficienza di Reese e Van Impe con gli interassi (anche con la virgola) e quella manuale; ξ di 2, 3 e ≥10 verticali;
- il momento della sezione c.a. (secondo anello interno, coefficienti unitari), del CHS di catalogo, del tubo CHS manuale con il
  peso dell'iniezione, e quello manuale con la sua fonte;
- due calcoli fermati con «Dati da completare»: una stratigrafia più corta del palo con una sequenza mista con Broms, e un tubo CHS
  senza spessore.

Nessuna nuova fixture apre la risposta elastica, quindi nessuna tocca le transizioni F4 dell'elastico (BUG-PO-4, BUG-PO-5,
F4-15), che riguardano solo `single-soil-elastic`. Gli input vengono dal generatore `F3Fixtures.HorizontalPileF57`; gli attesi sono
50 file, da due catture uguali byte per byte. Passano `ui/f3-equivalence` (67 fixture, transizioni F4 invariate) e
`ui/f3-report-chapters` (68 capitoli). In `expected/esito.txt` cambia solo il numero delle fixture, 61 → 67, registrato nel
manifest (`fixture_aggiunte_f5_7`, impronte prima e dopo, `before_sha256` di «PASS 61 fixture»; il coordinatore ri-concatena le
sezioni all'unione con F5.9b).

### 1.2 Tracce e disegni della vista storica

Le tracce (`tests/fixtures/f5-palo-orizzontale/storica`) registrano, dopo l'apertura e dopo ogni passo, la scheda della capacità:
- i byte dei dati nell'ordine in memoria; `Modified`, la sola lettura, l'esportabilità, i pulsanti dei risultati, la scheda delle
  analisi;
- risultato, stato, sintesi, momento con la sua nota, modello con la sua nota, avvisi;
- i sette form campo per campo (etichetta, valore, voci, attivo, attenuato, visibile) con i gruppi espandibili;
- la verifica con l'esito e il suo colore;
- le stratigrafie (righe come le mostra la tabella, colore escluso) e l'editor del terreno di ogni riga della stratigrafia
  selezionata;
- la sezione disegnata (contorno, barre, staffa, didascalia) o le proprietà del CHS;
- le tabelle e la riga di «Diagrammi e dettagli» di ogni sondaggio.

Gli scenari sono 22: il percorso F3 di ognuna delle 16 fixture (con la visita delle due schede delle analisi, come
`ui/f3-equivalence`); le modifiche di ogni tipo sul palo e sul micropalo; le stratigrafie con l'editor del terreno; la sola
lettura; la scheda elastica; il calcolo sullo sfondo con il motore trattenuto da `HeldModuleService` (calcolo superato, «Calcola»
durante un calcolo, passaggio alla scheda elastica durante un calcolo, chiusura e riapertura). Ogni passo usa i controlli come
l'utente. Tre catture uguali byte per byte, 36211 righe.

I disegni non si possono confrontare senza WPF, quindi la suite congela anche:
- gli **elenchi di disegno** (linee, forme, penne, riempimenti, testi con la loro origine) di `HorizontalDiagrams` di ogni
  sondaggio, terreno e palo, a 1000 × 660 (il report) e 1280 × 820, e del profilo con le forze, per le 6 fixture con un risultato:
  40 disegni, `disegni/elenco.txt`, congelati **prima** dello spostamento dei dati in Application e uguali dopo;
- i **dati dei diagrammi** (`diagrammi/*.json`, `HorizontalPileDiagrams`), che il ViewModel espone e che `Presentation.Tests`
  confronta.

Non sono nelle tracce: il dialogo modale dei dettagli dell'editor del terreno, l'esportazione CSV, «Estendi»/«Riduci» e
l'espansore «Opzioni avanzate» (stato della vista). Le provano le prove del ViewModel con valori scritti a mano.

### 1.3 Derivazioni in Application e campi condivisi

`src/ANTHEA.Application/Presentation/HorizontalPilePresentation.cs` contiene il codice spostato senza modifiche da
`HorizontalWorkspace` e dai suoi partial:
- testi di stato, sintesi, momento (sezione c.a. e CHS, manuale inserito e adottato, non disponibile con l'errore della sezione),
  avvisi;
- verifica, con l'esito e il modo di mostrarlo;
- tabelle e riga di «Diagrammi e dettagli»;
- campi attivi o mostrati dalle opzioni (falda, momento manuale, efficienza, CHS, EJ, secondo anello);
- materiali della sezione c.a. dal catalogo, secondo anello riempito, nuovo strato, nome dello strato;
- piano delle figure del report.

`src/ANTHEA.Application/Presentation/HorizontalPileDiagrams.cs` contiene i diagrammi come dati semplici (pannelli, curve con i
segmenti e il tratteggio, campi degli assi, strati, quote, note) e le righe fisiche della reazione. `HorizontalDiagrams` ora
disegna soltanto.

`src/ANTHEA.Presentation/Modules/HorizontalPile/HorizontalPileFields.cs` è l'unica definizione dei campi dei sette form (gruppi,
campi nascosti, suggerimenti), delle colonne degli strati, delle intestazioni dell'editor del terreno e dei testi fissi delle
schede.

La vista storica chiama queste funzioni e dà un'uscita identica: tracce, elenchi di disegno, F3, capitoli autonomi,
`ui/smoke-horizontal`, `ui/check-field-form-parity`, `ElasticPile.UiChecks` con le due varianti.

La baseline F4 cambia solo per spostamenti e rimozioni, da 3349 a 3344 occorrenze (voci 2206 → 2204):
- tolte 5 occorrenze: il rapporto `1000d / 660` delle figure (R2 1000d, R2 660, R6 1000d), che diventa il rapporto delle
  dimensioni del piano; un letterale 1,5D e una lettura di My in `HorizontalDiagrams.Render`, calcolati una volta sola nel
  modello;
- spostate le altre letture R4 e i letterali R2 delle derivazioni in Application. Presentation non ha voci F4 (P2); la P3 resta 127.

`ConcreteLibraryAdapter.Checks` (regola 7c, catalogo condiviso delle esposizioni) legge ora `HorizontalPileFields.cs`, dove la lista
dei campi è passata.

### 1.4 ViewModel

`HorizontalPileSheetViewModel` (`src/ANTHEA.Presentation/Modules/HorizontalPile`) riproduce la scheda della capacità di
`HorizontalWorkspace`:

| Vista storica | ViewModel |
| --- | --- |
| costruttore: `PrepareGeneral`, form, `PrepareFactors`, `RebuildSurveys`, `PrepareSection`, preparazione dell'elastico, scheda, `Preview`, `QueueCalculation` | costruttore: stessa sequenza (`Build`), poi `ScheduleCalculationAsync` sulla scheda della capacità |
| `general`, `model`, `factors`, `efficiency`, `moment`, `sectionFields` (c.a. o CHS), `ejForm` (`InputForm`) | sette `FormViewModel` con `HorizontalPileFields` (`Forms`), testo grezzo, gruppi, campi nascosti, `SectionToolTips` |
| `SectionMaterialChanged`, `ShowInnerRing` | stessa sequenza con `HorizontalPilePresentation.ApplySectionMaterial` e `FillInnerRing` |
| `analyses` (due schede), `vista_orizzontale` | `SelectedAnalysis`: scrive `vista_orizzontale` con `Modified`; la scheda elastica annulla il calcolo senza azzerare il risultato |
| `surveys`, `grids`, `Bind` (copia di presentazione), `RebuildSurveys` | `Surveys` (`HorizontalPileSurveyViewModel`: `Columns`, `Rows` come `JsonRowCollection`), `SelectedSurveyIndex` |
| «+ Stratigrafia», [−] della scheda, «− Stratigrafia», «Copia in…», «Copia da…» | `AddSurvey`, `RemoveSurvey`, `RemoveSelectedSurvey`, `CopySurvey`, `CopySurveyIntoAsync`/`CopySurveyFromAsync` (`SurveyCopyDialogViewModel`, `ConfirmDelete`, `Inform`) |
| «Aggiungi strato», [−] dello strato | `AddLayer`, `RemoveLayer` |
| `HorizontalSoilEditor` (creato con la riga mostrata) e il dialogo dei dettagli | `HorizontalSoilRowViewModel` per ogni riga mostrata della stratigrafia selezionata sulla scheda della capacità: categoria, legge, valore, «nh*», descrizione, fonte, motivazione, «Adotta nuova media», «Mantieni valore…» |
| `ElasticPileWorkspace.InvalidateShared` dalla scheda della capacità | `ElasticChanged`: `elastico.stratigrafia` = −1 quando cambia il numero delle stratigrafie, secondo `Modified` |
| `summary`, `momentValue` (con il suo `ToolTip`), `status`, `warnings`, `modelLabel` | `Summary`, `MomentText`, `MomentToolTip`, `Status`, `WarningText`, `ModelText`, `ModelHint` |
| `UpdateVerification` | `Verification` (`HorizontalPileVerification`) |
| `sectionDrawing`, `chsDrawing` | `SectionDrawing` (`HorizontalPileSectionDrawing`), `ChsProperties` |
| «Diagrammi e dettagli» (`ResultsTabs`, `HorizontalDiagrams`), «Esporta CSV» | `CanShowResults`, `Results`, `Diagram(survey, soil)` (dati semplici), `References`, `ExportCsv` (`IFileDialogService`) |
| `Changed` | `UserChanged`: testi, elastico, anteprima, verifica, stato, poi `Changed` della base |
| `CalculateScheduledAsync` (450 ms, `Task.Run`, `HorizontalPileAnalysis`) | `SheetCalculationMode.Background`, ritardo 450 ms; `HorizontalPileAnalysis` con l'`IModuleService` del contesto, portata come risultato tipizzato (`ModuleCalculation.Native`) e letta alla pubblicazione con `PublishedCalculation` |
| report: `ReportOrizzontale` con `ReportFigures` | `Report`: `ReportOrizzontale` con le figure di `IHorizontalPileFigureRenderer` (nel desktop `HorizontalPileFigureRenderer`) |
| `RevisionInspection.Protect` | `IsReadOnly`: `FormViewModel.IsReadOnly` (D4), righe, editor del terreno e stratigrafie rifiutati; selezione, scheda e calcolo ammessi |

`HorizontalPileSheetModule` dichiara la chiave di stato `vista_orizzontale`; ne esiste un'istanza per ognuno dei due moduli,
registrata in `DesktopComposition.CreateModules`. La Release resta storica: `ModuleImplementations` non cambia.

**Ganci additivi nel componente comune** (domanda O1). Due membri protetti di `SheetViewModelBase`, che non cambiano nulla per i
moduli esistenti:
- `PublishedCalculation`: il `ModuleCalculation` dell'ultima pubblicazione, anche quando il risultato è incompleto. Serve perché
  la vista storica mostra il momento della sezione anche con la stratigrafia incompleta;
- `CancelCalculation()`, su `SheetCalculation.Cancel()`: annulla il calcolo in attesa o in corso senza azzerare il risultato, come
  `RecalculationScheduler.Invalidate` della vista storica quando si passa alla scheda elastica.

### 1.5 Prove

`Presentation.Tests` passa da 921 a 1296 controlli; i 375 nuovi sono di `HorizontalPileSheetChecks`:
- le 22 tracce uguali riga per riga a quelle della vista storica;
- le 16 fixture F3: per le 10 sulla capacità ogni file (aperto, visitato, calcolato, risultato, testo e struttura del report,
  esportato, stato, riaperto) è uguale byte per byte al riferimento; per le 6 sulla risposta elastica si confrontano i dati
  (aperto, visitato, calcolato, esportato, riaperto), perché risultato, stato e report sono quelli elastici di F5.8. Le transizioni
  F4 dei due moduli riguardano solo questi file non confrontati;
- i diagrammi uguali a quelli congelati;
- modulo e chiavi di stato; derivazioni di Application con valori scritti a mano;
- con la barriera: sola revisione corrente, scheda elastica durante un calcolo, chiusura durante il calcolo;
- sola lettura, `Reload`, dettagli dell'editor del terreno, CSV, conferme e dialogo di copia.

Stadio `ui`: `ui/check-palo-orizzontale-historic` (tracce, elenchi di disegno, dati dei diagrammi), `ui/check-module-host-offscreen`
(registro con i due moduli orizzontali, Release storica), le suite F3.

**Corsa finale** `-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` su `b7de81c0`:
`supporto/artefatti/ci/20261017-005213-f57-finale`, PASS 114 (build 25, fast 31, wiki 2, ui 56), KNOWN 0, NEW-FAIL 0, nessun file
tracciato modificato. `ui/f3-equivalence` PASS con 67 fixture e `transitions-f4.json` invariato; `ui/f3-report-chapters` PASS con 68
capitoli; `ui/check-palo-orizzontale-historic` 22 scenari, 36211 righe, 40 disegni; `Presentation.Tests` 1296; `ui/smoke-horizontal`
ed `ElasticPile.UiChecks` con le due varianti PASS. `ui/check-contrast` è escluso: dal 16/10 sera lo schermo è al 150% (144 DPI) e il
controllo non può confrontarsi con la sua baseline a 96 DPI (decisione D6); il coordinatore lo ripete a 96 DPI. La suite
`ui/check-palo-orizzontale-historic` è l'unica nuova dello stadio `ui` (57 con il contrasto).

## 2. Comportamenti della vista storica trovati con le tracce

Riprodotti dal ViewModel, perché la vista storica li ha; da decidere al punto di controllo (domande O3–O6).

1. **Scheda delle analisi in sola lettura.** In una revisione in sola lettura la scheda «Risposta elastica» resta selezionabile e
   scrive `vista_orizzontale` con `Modified`. Anche la visita di `ui/f3-equivalence` scrive `vista_orizzontale` (è nel riferimento
   F3).
2. **Due `Modified` per modifica.** Ogni modifica sulla scheda della capacità solleva `Modified` due volte: una dalla risposta
   elastica (`InvalidateShared`), una dalla scheda.
3. **Annullamento senza azzeramento.** Il passaggio alla scheda elastica annulla il calcolo della capacità in attesa o in corso;
   il risultato già pubblicato resta e lo stato resta l'ultimo mostrato (anche «Calcolo di sezione ed equilibri in corso…»).
   Tornando alla capacità si ricalcola solo se non c'è un risultato.
4. **Scritture differite dell'editor del terreno.** Un nuovo strato riceve `reazione_orizzontale` quando la sua riga viene mostrata,
   dopo la copia dei dati per il calcolo e senza `Modified`. Lo stesso vale per il cambio di tipologia di uno strato quando Kp non
   cambia.
5. **Testi che restano.** La nota del momento (il modello della sezione c.a.) resta dopo il passaggio al momento manuale; la
   sezione disegnata in errore conserva diametro della staffa e didascalia.

**Difetto della vista storica** (non corretto, comportamento storico invariato, riprodotto dal ViewModel):
6. **Acciaio personalizzato non mostrato.** Quando un valore dell'acciaio della sezione c.a. (fyk, Es, fu, εu, legame) lo rende
   personalizzato, i dati diventano `classe_acciaio` = «Personalizzato», ma la scelta «Acciaio» mostra ancora la classe precedente
   (traccia `modifiche-palo`, passo `scelta steel_diagramma «Incrudente»`). Alla riapertura la scelta è giusta. Probabile, non
   provato: anche la nota della classe dell'acciaio scelta dal catalogo è calcolata prima che il catalogo scriva i suoi valori.

## 3. Che cosa resta

L'ordine segue il passo 6 della ricetta di F5.4, dopo il punto di controllo F5.7.

1. **Vista XAML** `X.Desktop/Views/HorizontalPile/HorizontalPileSheetView.xaml` e il suo DataTemplate, solo token di F5.3:
   - sette `FieldForm` (gruppi espandibili, campi nascosti, suggerimenti di `SectionToolTips`);
   - le schede impaginate come `LayoutCards`, con «Estendi»/«Riduci» (stato della vista; `ShowAll` del profilo segue la scheda
     estesa) e l'espansore «Opzioni avanzate»;
   - le stratigrafie in un `TabControl` con la barra dei comandi nella striscia delle schede; la tabella con le colonne di
     `StratigraphyTable` più la colonna dell'editor del terreno (larghezze di `HorizontalSoilEditor.Widths`) e il dialogo dei
     dettagli;
   - code-behind solo per il disegno: `StratigraphyDrawing` con le forze, `SectionDrawing` o `ChsDrawing`, `HorizontalDiagrams`
     dal modello (`HorizontalDiagrams(HorizontalDiagram)` esiste già);
   - le due schede delle analisi: la seconda ospita la vista elastica, storica fino a F5.8 (vedi O2);
   - piede: stato, «Ipotesi e limiti del calcolo», «Modello e dati comuni…» e, nel `SheetEditor`, «Invia / carica terreno…» (O7).
2. **Banco e confronto diretto**: `--module-impl geo_palo_orizzontale=mvvm` e `geo_micropalo_orizzontale=mvvm`, cattura F3, poi
   `tools/qa/f5_confronto_diretto.py` a differenza zero. Per le 6 fixture sulla risposta elastica serve F5.8 (o l'ospitata della vista
   elastica storica, O2).
3. **Suite UI riscritte** sul ViewModel o sulla vista ospitata da `MvvmSheetEditor`, prima del passaggio della Release:
   - `ui/smoke-horizontal`: `HorizontalSmokeChecks`, `HorizontalLayoutChecks` (layout, diagrammi, cerniere, verifica),
     `HorizontalChsChecks`, `HorizontalStratifiedSmokeChecks`, tutte partial di `HorizontalWorkspace` con accesso ai campi privati;
   - le parti orizzontali di `ElasticPile.UiChecks` (e varianti): per riflessione usano `HorizontalWorkspace`
     (`sectionFields`, `sectionDrawing`, `ActiveResult`, `CalculateActiveAsync`) come ospite dell'elastico; vanno riscritte con F5.8;
   - `ContrastAudit` (finestra «Palo orizzontale · risultati», `HorizontalWorkspace.ShowResults`) e il passo del modulo in
     `--check-contrast`;
   - `ui/check-palo-orizzontale-historic` resta finché la vista storica esiste; le tracce restano per `Presentation.Tests`.
4. **Punto di controllo dell'utente** con il materiale della sezione 4.
5. **Passaggio della Release e archiviazione (Q11)**, nello stesso commit, dopo F5.8 o con la vista elastica storica ospitata (O2):
   `ModuleImplementations.Declared` per i due moduli; `HorizontalWorkspace` e partial, `HorizontalSoilEditor`, la suite e la traccia
   storica in `supporto/SUPERATI/ui-storica/palo-orizzontale/`; i rami `horizontal` di `SheetEditor` (campo, `Result`, `Busy`,
   costruttore, `Dispose`, `Commit`, `CalculateAsync`, `BuildReportDocument`).
6. **Guida pratica e PDF**, se la disposizione descritta cambia.

## 4. Domande per il punto di controllo F5.7

- **O1 · Ganci additivi nel componente comune.** `SheetViewModelBase.PublishedCalculation` e `CancelCalculation()` (con
  `SheetCalculation.Cancel()`), protetti e additivi, nessun cambiamento per i moduli esistenti. Alternativa: un servizio del modulo
  che tiene l'ultima analisi, senza toccare la base. Si confermano?
- **O2 · Ampiezza del passo rispetto a F5.8.** Il ViewModel copre la scheda della capacità e la parte dell'elastico che agisce sui
  dati da quella scheda. Su una scheda elastica `Result`, «Calcola» e report sono ancora quelli della capacità (la vista storica usa
  `ActiveResult` e il report elastico). Proposta: F5.8 aggiunge un ViewModel elastico figlio e reimplementa `Result`/`HasResults`/
  `CalculateAsync` per la scheda attiva; il passaggio della Release dei due moduli avviene con F5.8, oppure prima con la vista
  elastica storica ospitata nella seconda scheda. Quale?
- **O3 · Scheda delle analisi in sola lettura** (sezione 2, punto 1): mantenere la scrittura con `Modified`, o non scrivere
  `vista_orizzontale` in sola lettura (differenza dichiarata)?
- **O4 · Due `Modified` per modifica** (punto 2): mantenere, o uno solo (nessun effetto sui dati; il contatore delle modifiche lo
  vedrebbe)?
- **O5 · Scritture differite dell'editor del terreno** (punto 4): il ViewModel le riproduce emulando la riga mostrata. Proposta per
  dopo F5: preparare `reazione_orizzontale` alla creazione dello strato (cambierebbe i dati prima del calcolo e le tracce).
- **O6 · Acciaio personalizzato non mostrato** (difetto 6): correggerlo nella vista nuova (differenza dichiarata, voce F5-…) o
  conservarlo fino a F5.14? Proposta: correggerlo nella vista nuova, con una prova.
- **O7 · Dialoghi condivisi.** «Modello e dati comuni…» (`CalculationHelpView`) e «Invia / carica terreno…» (`SoilTransferDialog`):
  come V9 di F5.6, il ViewModel non li ha ancora.
- **O8 · Riferimento F3.** Come D2 di F5.4: cambia solo il numero delle fixture in `expected/esito.txt` (61 → 67), registrato nel
  manifest.
- **O9 · Diagrammi come dati semplici.** `HorizontalPileDiagrams` dà al ViewModel e al disegno lo stesso modello; il disegno resta in
  `HorizontalDiagrams` (Q3). I dati congelati sono circa 4 MB di JSON indentato (6 fixture): si accetta il peso o si preferisce
  un'impronta per diagramma?
- **O10 · Elenchi di disegno.** Dipendono dai font installati (Segoe UI): valgono sulla macchina del banco, come le prove di
  contrasto. Si accetta come prova dell'identità del disegno?

**Materiale da mostrare** (dopo i passi 1–3 della sezione 3): schermate fuori schermo nei tre aspetti, `--check-contrast`, confronto
diretto a zero, `ui/f3-equivalence` con i due moduli MVVM, conteggio delle suite `ui`, questa nota con le risposte.

## 5. Vista XAML (18/10)

Lavoro dell'agente f5-7-vista sul ramo `refactoring/f5-7-vista` (da `refactoring/f5` 591272d8), insieme alla vista della risposta
elastica (F5.8, sezione 6 di `f5-8-palo-elastico.md`). La Release resta storica (O2/E2): i due moduli passano insieme dopo la
prova dell'utente sulle due schede. **Stato reale:** vista della capacità e suoi controlli fatti; banco F3 con i moduli MVVM e
confronto diretto **non ancora fatti** (chiusura anticipata chiesta dal coordinatore).

### 5.1 Commit

| Commit | Contenuto |
| --- | --- |
| `3e9286d8` | ViewModel della capacità per la vista: comandi (stratigrafie, «Copia in…»/«Copia da…», CSV, riferimenti, «Estendi»/«Riduci», «Invia / carica terreno…», «Modello e dati comuni…»), `WikiNavigator` su ogni form, O6 come voce F5-02 del registro |
| `31198665` | Vista XAML della capacità in `X.Desktop/Views/HorizontalPile` (`HorizontalPileSheetView`, pannello delle schede, tabella degli strati con l'editor del terreno, dialogo «Diagrammi e dettagli», dettagli del terreno); `FieldForm.EditorToolTips` (aggiunta) |
| `4f5135cf` | Controlli della vista (`tests/Desktop/HorizontalPileViewChecks*.cs`), suite `ui/check-palo-orizzontale-view`, passo di contrasto autonomo in `ContrastAudit` (non eseguito) |
| `4064cab7`, `d026b081`, `test(f5.8): controlli…` | ViewModel e vista della risposta elastica, controlli elastici (sezione 6 di `f5-8-palo-elastico.md`) |

### 5.2 Decisioni applicate

- **O2/E2.** La Release resta storica; con `--module-impl geo_palo_orizzontale=mvvm --module-impl geo_micropalo_orizzontale=mvvm`
  il banco UiTests apre la vista nuova con le due schede.
- **O6 → F5-02.** Nella vista nuova, dopo un valore dell'acciaio personalizzato (fyk, Es, fu, εu, diagramma) la scelta «Acciaio»
  mostra «Personalizzato», come i dati. Voce F5-02 del registro (difetto-legacy, dichiarato); le tracce della vista storica si
  confrontano con `HorizontalPileTrace.WithDeclaredDifferences`, che cambia solo le righe `scelta classe_acciaio` dello scenario
  `modifiche-palo` (20 righe). La seconda parte probabile di O6 (nota della classe dal catalogo) non si presenta: anche la vista
  storica mostra la nota giusta. Il coordinatore può dover rinumerare F5-02 se un altro agente ha usato lo stesso numero.
- **O7/T5.** «Invia / carica terreno…» usa `SoilTransferDialogService` (dialogo condiviso); «Modello e dati comuni…» apre la pagina
  di teoria con `IWikiNavigator` (`CalculationGuide`).
- **Regola del blocco.** Ogni `FormViewModel` ha `WikiNavigator = Context.Services.Wiki`: ogni «?» e ogni F1 dei campi arriva al
  navigatore (provato).

### 5.3 Esiti

| Prova | Esito |
| --- | --- |
| `ui/check-palo-orizzontale-view`, parti della capacità | 27 tracce attraverso i controlli uguali a quelle congelate (40277 righe, 20 righe F5-02); 40 disegni uguali; 21 fixture con visita senza scritture, schede delle analisi che scrivono come la storica, dati e risultato dopo «Calcola», JSON, Word, PDF e CSV uguali alla vista storica; F5-02; schermate; aiuto; nomi di automazione; sola lettura; dialogo del terreno; banco e Release |
| `ui/check-palo-orizzontale-view`, suite intera | PASS 221 controlli con le parti elastiche (sezione 6 di `f5-8-palo-elastico.md`), circa 6 minuti |
| `Presentation.Tests` | PASS 1863 |
| `Architecture.Tests` | PASS 523 (P1–P7, F4 senza voci nuove) |
| Profilo quick | `f57v-c3` su `4f5135cf` e `f57v-c5` su `d026b081`: PASS 52, NEW-FAIL 0 |

Le schermate (144 DPI) sono in `vista/schermate` della suite, con quelle della vista storica alle stesse misure in
`schermate/storica`.

### 5.4 Differenze visibili e scelte da confermare («da decidere», con la scelta fatta)

- **Colori dell'esito della verifica**: storici #16703C/#B42318 → token `Status.Ok`/`Status.Fail` (come F5.6).
- **«Opzioni avanzate»** (metodo, passo, tolleranza): editor di 82 px del `FieldForm` compatto invece di 150 px (D10).
- **Titoli delle tabelle 14.5/14.6** nel dialogo dei dettagli del terreno: 15 → 14 punti (`Font.Subheading`).
- **F5-02** (sopra): la scelta «Acciaio» mostra «Personalizzato».
- **Stili delle caselle della tabella degli strati** basati sullo stile del tipo (`BasedOn` del `TextBox`/`ComboBox` del tema): negli
  aspetti scuri restano del tema. Probabile stesso difetto latente nella vista F5.6 (`LayerChoice`/`LayerText` basati su uno stile
  senza tema): segnalato, non corretto.

### 5.5 Suite storiche da riscrivere prima del passaggio della Release

- `ui/smoke-horizontal`: `HorizontalSmokeChecks`, `HorizontalLayoutChecks`, `HorizontalChsChecks`, `HorizontalStratifiedSmokeChecks`;
- le parti di `ElasticPile.UiChecks` (e varianti) che usano `HorizontalWorkspace` per riflessione;
- `F3PileSegmentChecks` in `ui/f3-scheduling`;
- i passi del modulo in `ContrastAudit` (`--check-contrast`); il passo nuovo della vista è pronto ma non eseguito (144 DPI);
- `ui/check-palo-orizzontale-historic` e `ui/check-palo-elastico-historic` restano finché esiste la vista storica.

### 5.6 Che cosa resta e da dove si riparte

1. **Banco F3** (non avviato): cattura con `dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-f3-equivalence <radice>
   <uscita> --module-impl geo_palo_orizzontale=mvvm --module-impl geo_micropalo_orizzontale=mvvm` (atteso PASS 104), poi
   `python -I tools/qa/f5_confronto_diretto.py --a <cattura storica> --b <cattura mvvm> --out <json>` a differenza zero (fixture
   elastiche comprese), transizioni F4 (1 117) invariate, `--check-f3-report-chapters` con gli stessi `--module-impl`.
2. `--check-contrast` a 96 DPI con il passo della vista nuova (coordinatore).
3. Punto di controllo dell'utente con la prova a mano (5.7), poi passaggio della Release e archiviazione insieme a F5.8.

### 5.7 Da provare a mano (banco UiTests: `ANTHEA.exe --interactive --module-impl geo_palo_orizzontale=mvvm --module-impl geo_micropalo_orizzontale=mvvm`)

1. Aprire un palo e un micropalo orizzontale reali: schede, forme, stratigrafie, profilo, sezione c.a. o CHS e verifica come oggi;
   ridimensionare la finestra e provare «Estendi»/«Riduci» delle tre schede.
2. Modificare campi, strati, editor del terreno (categoria, legge, valore, override, nh) e stratigrafie («+»/«−», «Copia in…»/«Copia
   da…», eliminazione con conferma): ricalcolo e stato.
3. Acciaio personalizzato (fu, Es…): la scelta «Acciaio» mostra «Personalizzato» (F5-02); poi una classe del catalogo.
4. «Diagrammi e dettagli», «Esporta CSV», i riferimenti bibliografici.
5. «Invia / carica terreno…» e «Modello e dati comuni…»; i «?» dei campi e F1.
6. Una revisione precedente (sola lettura); report Word/PDF; aspetti Scuro e Molto scuro.
