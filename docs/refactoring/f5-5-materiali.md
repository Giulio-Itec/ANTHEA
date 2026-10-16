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

## 2. Che cosa resta per finire F5.5

Passo 6 della ricetta, dopo il punto di controllo del pilota:
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
   - `ui/smoke-materials` con `MaterialViewChecks` (oggi usa i campi privati e le caselle nascoste delle esposizioni);
   - `ui/smoke-material-report`;
   - `ui/smoke-steel` (`historicEditor.rebarMaterial`, `Properties.Editors`);
   - le prove di progetto che aprono i materiali (`ProjectHierarchySmokeChecks`, `ProjectSharingSmokeChecks`,
     `ProjectCoverWarningSmokeChecks`, `ProjectReportSmokeChecks`, `ProjectWorkspaceSmokeChecks`) usano
     `historicEditor.materials`/`rebarMaterial`: vanno portate sull'anteprima e sui ViewModel.
6. **Punto di controllo**, poi passaggio della Release e archiviazione delle viste storiche (Q11): `Materials/*` e
   `RebarMaterialView` in `supporto/SUPERATI/ui-storica/materiali/`; rami `materials`/`rebarMaterial` di `SheetEditor`
   (campi 25-26, `PreviewData` 48, costruttore 126-134, `Commit` 166, `CalculateAsync` 261, `ExportReport` 361-366,
   `MaterialReportContent` 369-372); `RebarIcon` resta (catalogo dei moduli). Le tracce congelate restano.
7. **Guida pratica e PDF**, se la disposizione cambia.

## 3. Domande per il punto di controllo F5.5

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
