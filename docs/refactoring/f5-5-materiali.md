# F5.5 — Materiali: preparazione

## Scopo

Questa nota chiude la **preparazione** di F5.5: moduli `mat_calcestruzzo` (vista storica `Materiali.MaterialView`, 8 file
partial e `Styles.xaml`) e `mat_acciaio_armatura` (vista storica `RebarMaterialView`). Segue i passi 1–5 della «Ricetta
per i moduli successivi» di [f5-4-pilota.md](f5-4-pilota.md).
Contiene:
- che cosa esiste dopo la preparazione;
- che cosa resta per finire F5.5;
- le domande per il punto di controllo dell'utente.

La vista XAML, il passaggio in `ModuleImplementations` e l'archiviazione delle viste storiche vengono **dopo** il punto di
controllo del pilota: non fanno parte di questa preparazione.

Riferimenti:
- [proposta](f5-proposta-architettura.md), sezioni 3, 5, 7 e riga F5.5 della sezione 8;
- [inventario](f5-inventario-ui.md), sezioni 1.4, 2.2 e 3.5;
- `tests/fixtures/f3/README.md` e `tests/fixtures/f5-materiali/README.md`.

Ramo `refactoring/f5-5-materiali`, da `refactoring/f5` `3c336992`, 16/10/2026.

**Aggiornamento del 17/10, ramo `refactoring/f5-5-vista`.** Dopo il punto di controllo («accetto tutte le proposte») sono
stati eseguiti i passi 1–4 della sezione 2: ViewModel allineati, viste XAML, banco, confronto diretto e suite
`ui/check-materials-view`. La sezione 4 contiene gli esiti, le differenze visibili, le domande nuove e le prove a mano. La
Release resta storica fino alla prova dell'utente.

## 1. Che cosa esiste dopo la preparazione

| Commit | Contenuto |
| --- | --- |
| `b87c9c0f` | 12 fixture F3 nuove dei materiali (calcestruzzo 2 → 8, acciaio 1 → 7; 54 nel riferimento), attese catturate con le viste storiche |
| `3285365f` | tracce congelate delle viste storiche: 21 scenari, suite `ui/check-materials-historic` |
| `0a7fa436` | derivazioni dei materiali in Application, P3 130 → 127, F4 invariata (motivo in 1.3) |
| `15182ac6` | contratto: anteprima `PreviewData` e modalità senza calcolo pubblicato |
| `e2f21c1b` | ViewModel `src/ANTHEA.Presentation/Modules/Materials` e prove senza WPF |

### 1.1 Fixture F3

Le 12 fixture nuove coprono, per il calcestruzzo:
- i due criteri del copriferro: EC2 con le tre riduzioni della classe strutturale, NTC + Circolare con piastra e controllo
  dei copriferri;
- i tre controlli di esecuzione e le loro tolleranze, getto, abrasione, superficie irregolare, vita utile di 100 anni;
- aderenza buona e coefficienti digitati, gelo con aria inglobata, acqua di mare, X0, una classe fuori dal catalogo
  della libreria (C16/20);
- numeri non validi con il report che lo segnala;
- un archivio con tre esposizioni, l'esposizione principale non prima, scelte parziali e una chiave sconosciuta.

Per l'acciaio:
- B450A, acciai storici con e senza εu, un acciaio personalizzato incrudente con riferimento su due righe;
- un B450A salvato con il catalogo precedente (DET-3);
- fu minore di fyk, con il report che lo segnala.

L'elenco è in `tests/fixtures/f3/README.md`. Gli attesi delle 42 fixture esistenti e `transitions-f4.json` non cambiano.
`ui/f3-equivalence` e `ui/f3-report-chapters` passano con 54 fixture. I materiali non hanno risultati: `result.json` è
nullo e `hasResults` falso anche nelle fixture nuove.

### 1.2 Tracce delle viste storiche

Il riferimento F3 confronta i dati in forma canonica e non vede la scheda. Le tracce (`tests/fixtures/f5-materiali/storica`)
registrano, dopo l'apertura e dopo ogni passo:
- i byte dei dati e quelli dell'anteprima (`PreviewData`), nell'ordine in memoria;
- `Modified` e se la scheda è modificabile;
- tutti i campi, le scelte con le voci e lo stato, le opzioni;
- testi, tabelle e indicatori; per l'acciaio anche lo stato (coerente o avviso) e la serie del diagramma.

**Semantica dell'anteprima, come è oggi.**
- Calcestruzzo: la vista tiene le modifiche nei controlli. I dati del foglio restano quelli aperti (l'archivio non
  normalizzato) fino alla conferma, che li sostituisce con lo stato della vista. L'anteprima è sempre lo stato della
  vista, ricostruito a ogni lettura (un oggetto nuovo). `Modified` scatta una volta per modifica, solo se lo stato cambia.
  In sola lettura la conferma scrive comunque lo stato.
- Acciaio: i campi scrivono nei dati a ogni modifica, quindi anteprima e dati sono lo stesso oggetto.

Gli scenari sono 21: le 15 fixture F3 sul percorso della cattura F3, le modifiche di ogni tipo per i due moduli, un archivio
con tre esposizioni, il catalogo precedente dell'acciaio, la sola lettura per i due moduli. Due catture uguali byte per byte.

La stessa definizione (`tests/Shared/MaterialTrace.cs`) guida due prove:
- le viste storiche, in `tests/Desktop/MaterialHistoricTrace.cs` e `MaterialViewTrace.cs`, stadio `ui`, finché le viste
  esistono;
- i ViewModel, in `tests/ANTHEA.Presentation.Tests/MaterialSheetChecks.cs`, stadio `fast`.

### 1.3 Derivazioni in Application

Spostate senza modifiche; le viste storiche le chiamano e danno un'uscita identica (tracce, F3, capitoli autonomi):
- `Materiali.MaterialExposurePresentation` (nuovo): descrizioni ed esempi delle 18 classi di esposizione (testi U5),
  aggressività, esposizione mostrata dal menu, riga delle classi attive e degli archivi con più esposizioni;
- `MaterialSheetInput`: ordine delle chiavi di numeri, scelte e opzioni, `DeviationChoosable`, `IsNtcCover`;
- `MaterialSheetPresentation`: `DeviationNote`, `DeviationToolTip`;
- `Anthea.Application.Materials.RebarPresentation` (nuovo): voci del tipo, passaggio a «Personalizzato» dopo una modifica,
  tipo mostrato, campi fissati dal catalogo, descrizione, fonte, valori, stato e punti del diagramma;
- `MaterialReportChapter.Document`/`Write`: il report di un foglio materiale, composto come in `SheetEditor.ExportReport`,
  che ora lo chiama (codice dei report invariato).

`MaterialViewPresentationChecks` (Application.Tests) li prova con valori scritti a mano.

**Baseline.**
- **F4 invariata (3351).** Le derivazioni spostate non hanno voci F4: sono testi, regole booleane e formati. Le sole voci F4
  delle viste dei materiali sono di impaginazione (`MaterialView.FitTable`, `ResizeColumns` e costruttore: 4 voci, 5
  occorrenze) e del disegno dell'icona (`RebarIcon.OnRender`: 9 voci). Restano nelle viste (Q3). Le prime escono con
  l'archiviazione della vista storica; l'icona resta (è usata dal catalogo dei moduli). Vedi la domanda M8.
- **P3 130 → 127.** Escono `MaterialView.ExposureDescriptions` ed `ExposureExamples` (dizionari spostati e resi
  immutabili) e `MaterialView.Classes` (array statico reso `IReadOnlyList`, stessi valori).

### 1.4 Contratto comune: anteprima e modalità senza calcolo

f5-2-host aveva rilevato che `MvvmSheetEditor.PreviewData == Data`: i materiali hanno bisogno di un'anteprima nel
contratto. Modifica minima e additiva (domanda M2):
- `ISheetViewModel.PreviewData`: i dati con le modifiche non ancora confermate, senza confermarle;
- `SheetViewModelBase.PreviewData`, virtuale, restituisce `Data`: nessun cambiamento per la palificata e per le prove;
- `MvvmSheetEditor.PreviewData` legge quella del ViewModel (una riga).

I materiali non pubblicano risultati: «Calcola» conferma soltanto e `Result` resta nullo. La base riceve
`SheetCalculationMode.None` (domanda M4):
- `CalculateAsync` non calcola e non pubblica (restituisce `false`);
- non c'è ricalcolo automatico: un ritardo con questa modalità è rifiutato, `ScheduleCalculationAsync` fallisce;
- `CalculateModule` resta disponibile: l'acciaio lo usa per i valori mostrati.

### 1.5 ViewModel

**Calcestruzzo** — `ConcreteMaterialSheetViewModel`:

| Vista storica | ViewModel |
| --- | --- |
| costruttore + `RestoreState(NormalizeState(Data))` | costruttore: `Restore()` sui dati normalizzati, stessi messaggi d'errore |
| `choice` (classe) | `Classes`, `SelectedClass` |
| `numbers`, `choices`, `Flags` (controlli) | `Inputs`: un `FormViewModel` sullo stato della scheda (testo grezzo, voce scelta, booleani), etichette e unità della vista |
| `choices["deviationValue"]`, `UpdateDeviation` | `DeviationValues`, `SelectedDeviationValue`, `CanChooseDeviationValue`, `DeviationNote`, `DeviationToolTip` |
| `exposureSelector`, `exposureChecks` | `Exposures`, `SelectedExposure`, `ActiveExposures` |
| `RefreshDetails`, `RefreshExposureSelector` | `Refresh()`: stessi passi, testi da `MaterialSheetPresentation` e `MaterialExposurePresentation` |
| testi, tabella, schede | `Strength` … `ConsistencyInfo`, `Properties`, `Composition`, `CoverError`, `IsNtcCover` |
| `CaptureState` | `PreviewData` (stesso ordine delle chiavi) |
| `AttachStateEvents.Changed` | `Edited()`: `Modified` solo se lo stato cambia |
| `SheetEditor.Commit` (stato nei dati) | `OnCommit` |
| `RevisionInspection.Protect` | `IsReadOnly`: modifiche rifiutate, campi su una copia staccata; la conferma scrive lo stato |

**Acciaio** — `RebarMaterialSheetViewModel`:

| Vista storica | ViewModel |
| --- | --- |
| `Selection` | `Types`, `SelectedType` (`RebarEditor.Select`, poi `RefreshEditors` e `Refresh`) |
| `identity`, `Properties`, `coefficient` (`InputForm`) | `IdentityForm`, `PropertiesForm`, `CoefficientForm` (`FormViewModel`, testo grezzo, compatti) |
| `Changed()` | `OnFieldChanged`: `RebarPresentation.MarkCustom`, poi `Refresh` e `Modified` |
| `IsReadOnly` dei campi, `Enable("steel_diagramma")` | `Locked`, `NameLocked`, `DiagramEnabled` (stato della vista, vedi M6) |
| riferimento (`TextBox`) | `Reference`: scrive «riferimento» a ogni modifica, con `Modified` |
| `Refresh()`: calcolo con `ModuleServices` | `Refresh()`: calcolo con il servizio del contesto (`CalculateModule`), sul thread dell'interfaccia |
| `results`, `Status`, `diagram` | `Results`, `Status`, `StatusCoherent`, `Diagram`, `DiagramName`, `HasDiagram` |

Comuni:
- `IMaterialSheetViewModel.ReportContent()`: il capitolo del materiale per i report (conferma, poi
  `MaterialReportChapter.Create`), come `SheetEditor.MaterialReportContent`;
- `Report.BuildReport`: conferma, poi `MaterialReportChapter.Document`: stessi byte DOCX della vista storica;
- moduli `ConcreteMaterialSheetModule` e `RebarMaterialSheetModule`: `ModulePersistence` senza chiavi di stato
  dell'interfaccia (vedi M14), report senza short; registrati in `DesktopComposition.CreateModules`. La Release resta
  storica: `ModuleImplementations` non cambia.

### 1.6 Prove

- `Presentation.Tests` (677 controlli; 208 dei materiali e 8 del contratto):
  - 21 tracce uguali riga per riga;
  - 15 fixture uguali al riferimento F3: aperto, visitato, calcolato, risultato, testo e struttura del report, esportato,
    stato, riaperto. Per il report delle due fixture originali del calcestruzzo si applicano, una per una e con la regola
    del confronto F3, le transizioni F4 del riferimento (testi U5, voce F4-47);
  - moduli e registro, chiavi scritte, sola lettura, `Reload`, chiusura;
  - servizio di calcolo iniettato (acciaio sul thread dell'interfaccia; il calcestruzzo non lo chiama);
  - anteprima dei due fogli;
  - `SheetPreviewChecks`: anteprima predefinita e di un foglio che scrive alla conferma, modalità senza calcolo.
- `Application.Tests`: `MaterialViewPresentationChecks`.
- Stadio `ui`: `ui/check-materials-historic`, `ui/f3-equivalence`, `ui/f3-report-chapters`, `ui/smoke-materials`,
  `ui/smoke-steel`, `ui/smoke-material-report`, `ui/check-module-host-offscreen`.
- Architettura: P2 senza dipendenze dal calcolo in Presentation, F4 3351 (invariata), P3 127.
- **Corsa finale** `-Stage build,fast,wiki,ui` su `8fed9bec`: `supporto/artefatti/ci/20261016-143846-f55-finale`, PASS 112
  (build 25, fast 31, wiki 2, ui 54), NEW-FAIL 0, nessun file tracciato modificato. `ui/f3-equivalence` PASS con 54
  fixture e `transitions-f4.json` invariato; `ui/f3-report-chapters` PASS con 55 capitoli; Presentation.Tests 677.

## 2. Che cosa resta per finire F5.5

Passo 6 della ricetta, dopo il punto di controllo del pilota.

Stato al 17/10: punti 1–4 fatti sul ramo `refactoring/f5-5-vista` (sezione 4). Il punto 5 è sostituito, fino al passaggio
della Release, dalla suite `ui/check-materials-view`: le suite storiche restano com'è. Punti 6–7 dopo la prova dell'utente.

1. **Viste XAML** in `X.Desktop/Views/Materials`, con i token di F5.3 (P7) e i DataTemplate dei due ViewModel.
   - Calcestruzzo: tre colonne (proprietà, esposizione e copriferro, composizione), larghezza minima 1140 px; i campi si
     prendono per chiave da `Inputs` (oggi la vista usa colonne 136/44 px, diverse da `InputForm`: va deciso se tenerle,
     vedi M11). Gli `Expander` restano, perché la visita F3 li apre.
   - Acciaio: tre schede (300, 320, 360 px), `FieldForm` per i tre form, `Plot` del diagramma nel code-behind (Q3).
   - Campi fissati dal catalogo, sola lettura (`FormViewModel.IsReadOnly` di f5-4-vista, quando arriva) e colori dello stato
     e del copriferro dai token.
2. **Banco**: cattura F3 con `--module-impl mat_calcestruzzo=mvvm --module-impl mat_acciaio_armatura=mvvm`.
3. **Confronto diretto** fra la cattura storica e quella MVVM a differenza zero (`tools/qa/f5_confronto_diretto.py`), poi
   il confronto normale; ogni differenza va in `transitions-f5.json` con l'ok dell'utente.
4. **`MvvmSheetEditor`** (lavoro di f5-4-vista sulle esportazioni): `MaterialReportContent` da
   `IMaterialSheetViewModel.ReportContent`; `ExportReport` dei materiali con DOCX e PDF come `MaterialReportChapter.Write`
   (oggi `ReportFiles.Write` ricava il PDF dal DOCX riletto: va verificato che sia uguale).
5. **Suite UI riscritte** sui ViewModel e sulla vista nuova, in `tests/ANTHEA.Desktop.UiTests`:
   - `ui/smoke-materials` con `MaterialViewChecks` (oggi usa i campi privati e le caselle nascoste delle esposizioni).
     **Fatto** (f5-suite-ui, `519acbf5`): `OpenSheet.CheckConcrete`/`SetConcreteState`, `PreviewData`, duplicato
     `ui/smoke-materials-mvvm` ([f5-suite-ui.md](f5-suite-ui.md), sezione 2.2 e domande S4–S5);
   - `ui/smoke-material-report`. Usa solo `ISheetEditor`: passa già con i due moduli MVVM (prova a mano del 18/10, f5-suite-ui), nessuna
     riscrittura;
   - `ui/smoke-steel` (`historicEditor.rebarMaterial`, `Properties.Editors`). **Fatto** (f5-suite-ui, `519acbf5`): `OpenSheet.Steel`,
     duplicato `ui/smoke-steel-mvvm`;
   - le prove di progetto che aprono i materiali (`ProjectHierarchySmokeChecks`, `ProjectSharingSmokeChecks`,
     `ProjectCoverWarningSmokeChecks`, `ProjectReportSmokeChecks`, `ProjectWorkspaceSmokeChecks`) usano
     `historicEditor.materials`/`rebarMaterial`: vanno portate sull'anteprima e sui ViewModel. **Fatto** (f5-suite-ui):
     - `ProjectHierarchySmokeChecks`, `ProjectSharingSmokeChecks` e `ProjectCoverWarningSmokeChecks` in `519acbf5`, duplicati
       `ui/smoke-hierarchy-mvvm` e `ui/smoke-sharing-mvvm`;
     - `ProjectWorkspaceSmokeChecks` passa invariata (`8aece8fe`, `ui/smoke-project-workspace-mvvm`);
     - `ProjectReportSmokeChecks` usa solo `ISheetEditor` e passa già con i moduli MVVM.
6. **Punto di controllo**, poi passaggio della Release e archiviazione delle viste storiche (Q11): `Materials/*` e
   `RebarMaterialView` in `supporto/SUPERATI/ui-storica/materiali/`; rami `materials`/`rebarMaterial` di `SheetEditor`
   (campi 25-26, `PreviewData` 48, costruttore 126-134, `Commit` 166, `CalculateAsync` 261, `ExportReport` 361-366,
   `MaterialReportContent` 369-372); `RebarIcon` resta (catalogo dei moduli). Le tracce congelate restano.
7. **Guida pratica e PDF**, se la disposizione cambia.

## 3. Domande per il punto di controllo F5.5

**Risposte del 17/10.** Accettate tutte le proposte: le regole trasversali T1–T12 (M1, M2, M4, M5, M7, M8, M12 vi sono
coperte) e M3, M6, M10, M11; M9 con la scelta prudente del coordinatore, nessun comando Reologia nelle schede. Come sono state
applicate: sezione 4.2.

- **M1 · Riferimento F3.** Con le fixture nuove cambia solo il numero in `expected/esito.txt` (42 → 54), registrato nel
  manifest (`fixture_aggiunte_f5_5`), come D2 di F5.4.
- **M2 · Anteprima nel contratto.** `ISheetViewModel.PreviewData` (membro nuovo), virtuale in `SheetViewModelBase` con
  `Data` come valore, letto da `MvvmSheetEditor`. Alternativa: un'interfaccia facoltativa a parte (`ISheetPreview`) e un
  controllo di tipo nell'ospite.
- **M3 · Modifiche del calcestruzzo trattenute fino alla conferma.** Il ViewModel conserva la semantica storica: i dati
  cambiano solo a `Commit`. Uniformare alla scrittura immediata degli altri moduli sarebbe una differenza visibile nelle
  tracce (dati scritti a ogni modifica); si propone di non farlo in F5.
- **M4 · Modalità senza calcolo.** `SheetCalculationMode.None` nel componente comune: «Calcola» conferma e non pubblica
  nulla, `HasResults` resta falso ed «Esporta risultati» rifiuta come oggi.
- **M5 · Valori dell'acciaio.** Il calcolo dei valori mostrati (fyd, εyd, diagramma) resta sincrono sul thread
  dell'interfaccia a ogni modifica, come oggi, ma passa dal servizio del contesto (iniettabile) invece che da
  `ModuleServices`.
- **M6 · Campi fissati dal catalogo dell'acciaio.** Sono uno stato della vista (`Locked`, `NameLocked`, `DiagramEnabled`),
  come i `TextBox` di sola lettura di oggi: il ViewModel non rifiuta una scrittura su quei campi. Alternativa: sola lettura
  per campo in `FieldViewModel` (componente comune).
- **M7 · Sola lettura dei form.** Come la palificata (D4): copie staccate finché `FormViewModel.IsReadOnly` non c'è.
- **M8 · Baseline F4.** Non scende in questo passo: nelle viste dei materiali non ci sono voci F4 di derivazioni, solo di
  impaginazione e dell'icona (1.3). Scende di 4 voci (5 occorrenze) all'archiviazione della vista storica. La baseline P3
  scende di 3.
- **M9 · Reologia.** Le viste dei materiali oggi non aprono la finestra «Reologia del calcestruzzo»: la aprono il menu
  Strumenti e la sezione c.a. I ViewModel quindi non hanno un comando per aprirla. Aggiungerlo alla scheda del
  calcestruzzo sarebbe una funzione nuova: da decidere.
- **M10 · Caselle delle esposizioni.** La vista storica crea una casella per ogni classe, ma non la mostra: la scheda
  sceglie una sola esposizione dal menu. Solo `MaterialViewChecks` usa le caselle (più esposizioni attive). Il ViewModel
  tiene «esposizioni attive» uguale all'esposizione del menu; la vista nuova non avrà le caselle.
- **M11 · Disposizione dei campi del calcestruzzo.** Oggi etichetta, controllo di 136 px e unità di 44 px, diversa da
  `InputForm`/`FieldForm`. Si propone di tenerla nella vista nuova con un modello di campo proprio.
- **M12 · Report esportato dall'ospite.** Il ViewModel produce lo stesso DOCX; il PDF di `MvvmSheetEditor.ExportReport`
  si ricava dal DOCX riletto, mentre la vista storica lo ricava dal documento. Da verificare al passo dell'ospite.
- **M13 · `MaterialView.ReportProperties()`** non è usato da nessuno e non è stato portato.
- **M14 · Persistenza.** Nessuna chiave di solo stato dell'interfaccia per i due moduli: tutto ciò che salvano è dato del
  materiale. La migrazione pigra del calcestruzzo resta `MaterialSheetInput.NormalizeState` a ogni apertura.

**Materiale da mostrare** (dopo i passi 1–3 della sezione 2): schermate nei tre aspetti, `--check-contrast`, confronto
diretto a zero, `ui/f3-equivalence` con i moduli MVVM, conteggio delle suite `ui`, questa nota con le risposte.

## 4. Viste XAML (17/10, ramo `refactoring/f5-5-vista`)

Passi 1–4 della sezione 2 eseguiti dopo il punto di controllo del 17/10 («accetto tutte le proposte»), sul ramo
`refactoring/f5-5-vista` da `refactoring/f5` `7979f79d`. La Release resta **storica** per i due moduli: le viste nuove si
scelgono solo nel banco UiTests (`--module-impl mat_calcestruzzo=mvvm --module-impl mat_acciaio_armatura=mvvm`). Le viste
storiche non sono archiviate e le suite storiche (`ui/smoke-materials`, `ui/smoke-material-report`, `ui/smoke-steel`,
`MaterialViewChecks`, `ui/check-materials-historic`) non cambiano: restano fino al passaggio della Release, dopo la prova
dell'utente.

### 4.1 Commit

| Commit | Contenuto |
| --- | --- |
| `03b48dae` | ViewModel allineati alle decisioni: T2, Wiki dei servizi, stato del diagramma (M6), `CanEditDeviationValue` |
| `08d5517e` | viste XAML in `X.Desktop/Views/Materials`, `FieldForm` con i campi bloccati dall'ospite (M6), due token, `MvvmSheetEditor` (T8, capitolo, Wiki), suite `ui/check-materials-view`, passi del contrasto |
| `2148fddc` | questa sezione 4 |
| (successivo) | passo del contrasto dei materiali reso autonomo in `ContrastAudit` (richiesta del coordinatore), esito della corsa finale |

### 4.2 Decisioni applicate

- **T2.** Calcestruzzo e acciaio non usano più copie staccate: `FormViewModel.IsReadOnly` sugli stessi form, anche alla
  chiusura. In sola lettura la digitazione è rifiutata e il campo mostra di nuovo il testo dei dati (prima la copia staccata
  teneva il testo digitato: non si vedeva, perché la vista storica blocca le caselle). Le tracce non cambiano.
- **M3.** Il calcestruzzo tiene le modifiche fino alla conferma; l'anteprima (M2) è invariata. Provato attraverso la vista: le
  21 tracce registrano dati e anteprima a ogni passo.
- **M6.** I campi fissati dal catalogo dell'acciaio restano stato della vista. Il `FieldForm` riceve due proprietà ereditate,
  valide solo se l'ospite le imposta: `CalculatedKeys` (casella in sola lettura con il fondo dei campi calcolati, le proprietà
  meccaniche) e `ReadOnlyKeys` (sola lettura senza cambiare il fondo, il nome). Il diagramma si accende e si spegne sul form
  (`Enable`, come la vista storica). Il ViewModel non rifiuta le scritture. Senza le due proprietà il `FieldForm` non cambia:
  la parità con `InputForm` passa.
- **M9.** Nessun comando Reologia nelle schede.
- **M10.** Nessuna casella delle esposizioni: l'esposizione si sceglie dal menu.
- **M11.** I campi del calcestruzzo hanno un modello di campo proprio, `ConcreteFieldRow`: etichetta e «?» nella colonna
  libera, editor di 136 px, unità di 44 px, come `Materiali.MaterialView.Field`.
- **T8.** `MvvmSheetEditor.ExportReport` di un foglio materiale conferma e scrive con `MaterialReportChapter.Write`, come
  `SheetEditor`: Word e PDF dallo stesso documento. `MaterialReportContent` passa da `IMaterialSheetViewModel.ReportContent`,
  `BuildReport` da `Report.BuildReport` (lo stesso Word esportato).
- **D8–D12.** Titoli e testi con i token tipografici, bordo delle schede `Token.Border`, F1 sul campo con argomento apre la
  sua sezione, altrimenti la guida del modulo (D11). Per il calcestruzzo è una differenza di comportamento: la vista storica
  apriva sempre la guida. Per l'acciaio no: l'`InputForm` storico apriva già la sezione del campo.
- **Ospite.** La vista eredita `WikiContextHelp.Module` dal suo `MvvmSheetEditor`, come la vista storica da `SheetEditor`: i
  form dell'acciaio, senza modulo proprio come l'`InputForm` storico, trovano così gli argomenti dei «?».

### 4.3 Esiti

Cartelle non versionate in `supporto/artefatti/refactoring/f5/f5-5-vista/` del worktree `C:\Users\g.pacini\AppData\Local\Temp\aw-f5-5-vista`:
`cattura-mvvm`, `cattura-storica`, `confronto-diretto.json` e `capitoli-mvvm` (tutte su `08d5517e`), `vista/` (l'uscita di
`ui/check-materials-view` della corsa finale).

| Criterio | Esito |
| --- | --- |
| Viste XAML con la disposizione di oggi, solo token (P7) | sì; `Architecture.Tests` PASS 522, P6 e P7 attive su `Views/`, F4 senza aggiunte, P3 invariata |
| 21 tracce attraverso i controlli delle viste | uguali riga per riga a quelle congelate (6923 righe), anteprima compresa |
| Cattura F3 con i due moduli MVVM contro il riferimento | PASS 104 fixture, 1066 file, 0 differenze non ammesse; `transitions-f4.json` invariato, nessuna voce nuova (`cattura-mvvm`) |
| Cattura F3 storica dello stesso codice | PASS 104 fixture, 0 differenze non ammesse (`cattura-storica`) |
| Confronto diretto storica/MVVM (`tools/qa/f5_confronto_diretto.py`) | **identiche**: 1066 file, 1063 uguali byte per byte, 3 JSON uguali in forma canonica (tempi del palo elastico, chiave volatile); 0 differenze. I 139 file delle 15 fixture dei materiali e del report di progetto sono uguali byte per byte (`confronto-diretto.json`) |
| `--check-f3-report-chapters` con i due moduli MVVM | PASS 105 capitoli autonomi, 246 immagini (`capitoli-mvvm`) |
| Esportazioni, 15 fixture | Word uguale in ogni parte e PDF uguale (date, identificativi e sigle dei font normalizzati) al report della vista storica; JSON rifiutato con lo stesso messaggio; `MaterialReportContent` uguale; `Report.BuildReport` uguale al Word esportato |
| Capitolo nel report di progetto | uguale, dopo le stesse modifiche fatte attraverso le due viste (calcestruzzo e acciaio) |
| Visita senza scritture, 15 fixture | espansori aperti e tre aspetti: nessuna scrittura, nessun `Modified`, anteprima invariata |
| F1 e «?» | «?» visibili uguali alla vista storica; F1 e «?» dal navigatore della Wiki dei servizi, anche in sola lettura |
| Nomi di automazione | quelli della vista storica conservati (calcestruzzo 7–8, acciaio 16); nel calcestruzzo si aggiungono i nomi degli editor |
| Sola lettura | stessi controlli bloccati e liberi della vista storica, prima e dopo; digitazione e scelte rifiutate |
| `ui/check-materials-view` | PASS 214 controlli |
| Profilo quick | PASS 52 su `03b48dae` e su `08d5517e`, NEW-FAIL 0 |
| Corsa finale `-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` | PASS 120, KNOWN 0, NEW-FAIL 0 su `2148fddc` (`supporto/artefatti/ci/20261017-101806-f55v-finale`); nessun file tracciato modificato; `ui/f3-equivalence` con le viste storiche PASS 104 fixture e lo stesso `transitions-f4.json`; `ui/f3-report-chapters` PASS; `Presentation.Tests` PASS; una suite nuova, `ui/check-materials-view` |

### 4.4 Schermate e differenze visibili

Cartella non versionata `supporto/artefatti/refactoring/f5/f5-5-vista/vista/schermate/` del worktree
`C:\Users\g.pacini\AppData\Local\Temp\aw-f5-5-vista` (le produce `ui/check-materials-view`): `vista-*` e `storica-*` per
calcestruzzo `default` (NTC) ed `ec2-riduzioni-tolleranza`, acciaio `default` e `storico-feb38k-eps`, in Chiara, Scuro e Molto
scuro. Lo schermo della macchina è al 150%: le schermate sono rese a 96 DPI fuori schermo, valgono come documentazione.

Differenze visibili:
- **Titoli dei riquadri del calcestruzzo** 15 → 16 punti (`Font.Heading`), il «cnom» 23 → 22 (`Font.Title`): accorpamenti
  tipografici (Q6 c, D3 del 16/10). Il contenuto scende di qualche pixel.
- **Valori in sola lettura del calcestruzzo** (classe minima, composizione): `#EDF5FF` diventa `Token.Calculated` (`#EAF2FA`,
  ΔE 1,5). In Chiara non si distingue; negli aspetti scuri le caselle prendono il fondo dei campi calcolati invece di quello
  dei campi modificabili.
- **Campi liberi dell'acciaio**: il fondo `Paper` che la vista storica assegnava alle proprietà libere diventa quello dei campi
  modificabili del `FieldForm` (`Token.Field.Background`, ΔE 2,2 in Chiara).
- **Stato dell'acciaio**: stessi colori, con due token nuovi (`Status.Coherent` `#18794E`, `Status.Caution` `#9A6700`), perché
  nessun token è entro ΔE 5. La tabella congelata dei colori ha le due voci (D10).
- **Tabella delle proprietà**: colonne in proporzione (45/18/23/14) sulla stessa larghezza, invece delle larghezze in pixel di
  `FitTable` (al più 2 px di differenza per colonna).

### 4.5 Contrasto

`--check-contrast` ha i passi `mvvm-mat_calcestruzzo` e `mvvm-mat_acciaio_armatura`, con la regola della palificata: con
`--baseline` solo Scuro e Molto scuro, la Chiara delle viste MVVM è dichiarata fra le finestre non coperte. Non sono stati
eseguiti nella corsa: lo schermo è a 144 DPI e la baseline a 96 DPI (T10). Vanno eseguiti a 96 DPI prima dell'unione.

Il blocco è autonomo (richiesta del coordinatore dopo la chiusura del pilota, che ha tolto il passo `mvvm-` della palificata):
`ContrastAudit.AuditMvvmMaterials` apre una finestra con i due fogli MVVM e `MainWindow.AuditMvvmMaterialViews` esplora i due
moduli come i passi dei moduli, fra commenti «F5.5 (f5-5-vista)». Si toglie al passaggio della Release dei materiali.

Solo per controllare che il passo funzioni, non per giudicare: `--check-contrast <cartella> --viste ^mvvm-mat`, senza
`--baseline`, a 144 DPI, ha fatto 27 catture nei tre aspetti senza difetti (`supporto/artefatti/refactoring/f5/f5-5-vista/contrasto-prova`).

### 4.6 Domande nuove

- **M15 · Titoli 15 → 16.** Si è scelto `Font.Heading` (16) per i titoli dei riquadri e delle colonne del calcestruzzo.
  Alternativa: `Font.Subheading` (14), come le intestazioni delle schede dell'acciaio.
- **M16 · Valori in sola lettura.** `#EDF5FF` → `Token.Calculated` (scelta per significato e distanza). Alternativa che
  conserva anche gli aspetti scuri: `Token.Field.Background` (ΔE 4,9 in Chiara).
- **M17 · Token nuovi.** `Status.Coherent` e `Status.Caution` nel catalogo (insieme semantico 36 → 38 token). Alternativa:
  accorpare a `Status.Ok` e `Warning.Foreground`, con un cambiamento visibile (ΔE 27,8 e 17,5).
- **M18 · `FieldForm`.** Le due proprietà `CalculatedKeys` e `ReadOnlyKeys` sono nel componente comune (vista), non nel
  `FieldViewModel`: lo stato della vista di M6 resta fuori dal ViewModel. Può servire ad altri moduli con campi bloccati da un
  catalogo.
- **M19 · `BuildReport` dei materiali.** `SheetEditor.BuildReport` di un foglio materiale fallisce («Premere Calcola prima di
  esportare.», ramo generico senza risultato); quello di `MvvmSheetEditor` restituisce il Word del foglio. Nessuno lo chiama per
  i materiali (finestra, report di progetto e catture usano `ExportReport` e `MaterialReportContent`).
- **M20 · Rotella sulle scelte.** Come oggi: le scelte del calcestruzzo hanno il comportamento predefinito di WPF; il tipo
  dell'acciaio passa la rotella alla pagina quando è chiuso (`ChoiceWheel`, come `Ui.Choice`).

### 4.7 Da provare a mano

```
dotnet build X.Desktop/X.Desktop.csproj -c UiTests
X.Desktop\bin\UiTests\net8.0-windows\ANTHEA.exe --interactive --module-impl mat_calcestruzzo=mvvm --module-impl mat_acciaio_armatura=mvvm
```

1. Calcestruzzo nuovo: tre colonne come oggi; restringere la finestra sotto i 1160 px (barra orizzontale) e allargarla (le
   colonne si allargano).
2. Cambiare classe, esposizione, diametro, criterio (EC2 ↔ NTC: le opzioni si scambiano), controllo di esecuzione (la
   tolleranza segue), opzioni: i valori si aggiornano subito e il documento risulta modificato; Salva conferma.
3. «?» di «Criterio copriferro» e F1 su «Diametro»: si apre la sezione della Wiki; F1 su «Designazione» o sul menu
   dell'esposizione: la guida del modulo.
4. Report Word del calcestruzzo e dell'acciaio: nome proposto `Scheda_calcestruzzo.docx` / `Scheda_acciaio_armature.docx`,
   Word e PDF affiancati, uguali a oggi.
5. Acciaio: B450C con i campi fissati (fondo azzurro), «Personalizzato» li libera; FeB44k libera εu e il diagramma; stato
   verde o ambra; il diagramma segue; certificato.
6. Aspetto Scuro e Molto scuro dal menu, con le schede aperte.
7. In un progetto con calcestruzzo, acciaio e una sezione c.a.: modificare il materiale e salvare (aggiornamento dei dati
   condivisi, avvisi del copriferro), report della sezione; aprire una revisione precedente: campi e scelte bloccati, «?»
   utilizzabili.
8. Salvare, chiudere e riaprire: stessi dati.
