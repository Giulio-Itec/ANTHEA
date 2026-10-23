# F5 · Suite UI indipendenti dall'implementazione (f5-suite-ui)

Ramo `refactoring/f5-suite-ui`, da `refactoring/f5` `591272d8` (notte 17-18/10/2026, blocco 8).

## Scopo

Le viste XAML di fondazioni superficiali, calcestruzzo, acciaio d'armatura, palo e micropalo verticale, stratigrafia e muri di
sostegno sono unite, ma la Release le apre ancora con la vista storica finché l'utente non prova una versione portatile. Undici suite
UI storiche raggiungono la vista storica attraverso `historicEditor` (`tests/Desktop/HistoricSheetEditor.cs`) e ne leggono le parti
interne: con i sette moduli `Mvvm` falliscono subito con «Il foglio aperto non usa SheetEditor: <modulo>».

Obiettivo: ogni suite passa
- (a) con la Release di oggi;
- (b) con i sette moduli MVVM, scelti solo con il banco (`--module-impl`), senza toccare `ModuleImplementations.Declared`.

Così il passaggio della Release di un modulo non richiede più di riscrivere queste suite: basta togliere il ramo storico (sezione 4).

Ordine: suite generali, suite dei materiali, suite dei muri, chiudendo ogni gruppo prima del successivo.

## 1. Come funziona

- **`OpenSheet`** (`tests/Desktop/OpenSheetChecks.cs`). È il foglio aperto come lo guidano le suite. `MainWindow.openSheet` lo crea
  sul foglio attivo. Ogni membro:
  - con `SheetEditor` chiama il metodo storico di oggi (le partial di prova di `SheetEditor`);
  - con `MvvmSheetEditor` esegue gli stessi controlli sulla vista XAML del modulo.

  Le suite chiamano `openSheet.X(…)` dove chiamavano `historicEditor.X(…)`. Per l'acciaio e per i muri, di cui le suite usavano molte
  parti della vista, `openSheet.Steel` e `openSheet.Wall` danno un adattatore con le operazioni usate e un'implementazione per vista
  (`SteelSmoke`; `HistoricWallSmoke` e `MvvmWallSmoke`).
- **Guardia del banco.** Se il banco della riga di comando apre un modulo con la vista MVVM ma il foglio aperto usa `SheetEditor`,
  `OpenSheet` fallisce con «Il banco apre <modulo> con la vista MVVM, ma il foglio aperto usa SheetEditor.». Una suite `-mvvm` non
  può quindi passare sulla vista storica per errore.
- **Controlli sulla vista XAML.** Seguono i modelli delle prove della vista (`VerticalPileViewChecks`, `MaterialSheetViewChecks`,
  `RetainingWallViewChecks`):
  - si scrive nei controlli della vista come l'utente: caselle dei `FieldForm`, celle delle tabelle, scelte, caselle di spunta;
  - si premono i pulsanti (evento `Click` e comando);
  - si legge dalla vista e dal suo ViewModel.

  I messaggi d'errore sono quelli delle suite storiche.
- **Servizi dei fogli del banco** (`BenchSheetServices` in `tests/Desktop/ModuleHostBench.cs`). I fogli MVVM di una finestra di prova
  ricevono servizi propri, che le suite raggiungono con `MainWindow.SheetServices`:
  - domande (`Confirmations`, un `ScriptedConfirmations` proprio, come `Scripted` per la finestra);
  - dialoghi (`Dialog`: guida il ViewModel del dialogo e ne restituisce il risultato; `DialogAsync` per un dialogo che resta aperto
    mentre la suite ci lavora);
  - salvataggi (`SaveFile`).

  Senza copione si comportano come `ModuleHostBench.Services()`: una domanda, un dialogo, un file, gli appunti, un collegamento o la
  Wiki non previsti fanno fallire la prova. Le suite storiche chiamavano invece i metodi della vista con `confirm: false`; ora la
  domanda arriva davvero e la prova ne controlla anche il testo.
- **Banco nell'harness** (`supporto/test/Desktop/AppHarness.cs`). `--smoke*` e `--check-appearance` accettano le coppie
  `--module-impl <modulo>=mvvm` dopo i loro argomenti, come `--check-f3-figures` e `--check-f3-scheduling` (`WithoutBench`). La
  finestra di prova le legge già dalla riga di comando (`ModuleHostBench.SheetEditors()`).
- **Suite `-mvvm`** (`build/ci.ps1`). `$MvvmBench` elenca le coppie dei sette moduli. Ogni suite riscritta ha un duplicato
  `ui/<nome>-mvvm` con gli stessi argomenti più `$MvvmBench`, la stessa attestazione e lo stesso tempo massimo.

## 2. Che cosa è cambiato, suite per suite

### 2.1 Suite generali (gruppo 1, commit `8aece8fe`)

Aprono un palo verticale (il documento predefinito della finestra) e, alcune, gli altri moduli. Il lato MVVM è
`tests/Desktop/VerticalPileSmokeChecks.cs` (`VerticalPileSmoke`).

| Suite | Che cosa leggeva della vista storica | Sulla vista XAML |
| --- | --- | --- |
| `ui/smoke` | `WaitForAutomatic`, `VerifyWpf` (palo e micropalo), `SetGeometryForSmoke` (sezione c.a.) | `WaitForAutomatic` sul ViewModel; `VerifyWpf` riscritto (sotto). La sezione c.a. non è fra i sette moduli: resta storica |
| `ui/smoke-display` | `VerifyDisplayLayout`, `VerifyDisplayScrolling`, `VerifyStratigraphyEditing`, `VerifyPileOutcomes`, `VerifyPresentation`, `VerifyReport`, `VerifyNavigationThrough` | tutti riscritti (sotto) |
| `ui/smoke-projects` | `SetGeometryForSmoke` sul palo | «Diametro palo» digitato nel `FieldForm` |
| `ui/smoke-project-workspace` | `SetGeometryForSmoke` sul palo (revisioni: navigazione ed eliminazione) | come sopra. Il resto (sola lettura della revisione di ogni modulo, fogli dell'acciaio, confronto) usa `ISheetEditor` e la finestra: passa invariato con i sette moduli MVVM |
| `ui/check-appearance` | `WaitForAutomatic` sul palo | `WaitForAutomatic` sul ViewModel |

**`WaitForAutomatic`** (`MvvmSheets`). Attende al più 10 s, come la vista storica, che siano terminati:
- il ricalcolo automatico in attesa (`SheetViewModelBase.PendingCalculation`);
- ogni calcolo in corso.

**`VerifyWpf`** sulla vista XAML. Stessi controlli, stesse schermate (`palo_grafico_esteso`, `palo_nq`, `micropalo_abachi`,
`palo_tabelle_compatte`, `palo_tabelle_compatte_estese`, `palo_stratigrafia_estesa`, `palo_profili`):
- sette schede di `VerticalPileCardPanel`, ognuna di almeno 100 px;
- «Diametro palo» modificato e ripristinato: risultato azzerato e dati invariati;
- ricalcolo: automatico per il palo, «Calcola» per il micropalo;
- la vista del grafico non scrive;
- «Estendi»/«Riduci» dei grafici: le sei altre schede nascoste, poi tutte visibili;
- la scheda Nq/abachi estesa;
- la cella dello spessore azzera il risultato e ricalcola.

Solo per il palo:
- tabelle compatte: 2 tabelle di 4 colonne e 3 righe, anche estese;
- stratigrafia estesa;
- lunghezza non valida: nessun risultato e nessuna serie nel `Plot` della vista;
- seconda stratigrafia da «Copia in…» → «Nuova stratigrafia», con φ′ = 32 digitato nell'ultimo strato: il profilo mostra solo quella
  e il marcatore Nq è a 32; con il profilo esteso si vedono entrambe;
- la copia si toglie con il [−] della sua scheda.

**`ui/smoke-display`** sulla vista XAML:
- **`VerifyDisplayLayout`**: lo `ScrollViewer` `Scroll` e il pannello `Cards` della vista al posto del canvas; le posizioni delle
  schede sono quelle assegnate dal pannello (`LayoutInformation.GetLayoutSlot`). Nessuno scorrimento orizzontale esterno, schede dentro
  il pannello e non sovrapposte.
- **`VerifyDisplayScrolling`**: fondo raggiungibile; le schede 4, 5 e 6 estese con i pulsanti «Estendi»/«Riduci» invece di
  `expanded = i`; stesse schermate `_fondo` ed `_esteso_<i>`.
- **`VerifyStratigraphyEditing`**: tutti i controlli, attraverso le celle e i campi della vista:
  - la digitazione non è interrotta dal ricalcolo (fuoco, testo e stato della casella);
  - micropalo: peso lineare in sola lettura e aggiornato con diametro e profilo CHS; α dalla scelta del terreno; parametri della punta
    spenti; τ uguale all'aderenza del calcolo, letta anche nella casella; τ con la pressione, con la laterale esclusa e fuori abaco;
  - palo: γsat automatico in grigio; valori espliciti; γb della tecnologia (D7-d);
  - due strati aggiunti, eliminazione di quello intermedio e dell'ultimo.

  Eliminazione della seconda stratigrafia: si aggiunge con «+» e si toglie con il [−] della sua scheda, rispondendo «Sì» alla domanda
  (controllato anche il testo della domanda).
- **`VerifyPileOutcomes`**: cambia il meccanismo, vedi la domanda S1.
- **`VerifyPresentation`**:
  - le copie passano dai dialoghi «Copia in…» e «Copia da…», con la conferma della sostituzione; l'indipendenza delle copie si prova
    digitando nella cella della copia;
  - il ripristino passa dalla vista: spessore ridigitato e copia eliminata. Un controllo in più verifica che le stratigrafie
    conservino ogni valore iniziale (la vista storica rimetteva la copia di riserva senza controllare);
  - poi, come prima: colori distinti delle curve di progetto e delle azioni (`Curves` del ViewModel con `PlotPresentation.Series`),
    nessuna voce vuota nei menu, tabelle compatte larghe al più 621 px con un decimale, proiezioni Nq, schermate.
- **`VerifyReport`**: i controlli del Word sono in `DisplayReportChecks`, comune alle due viste; il PNG del profilo viene dallo
  `StratigraphyDrawing` della vista.
- **`VerifyNavigationThrough`**: lo stato conservato è `ExpandedCard`, la scheda dei risultati e la vista del grafico. Uscire verso
  Home e riprendere conserva anche dati e risultato (lo stesso oggetto).

### 2.2 Suite dei materiali (gruppo 2, commit `519acbf5`)

Dove basta, le suite leggono il foglio attraverso `ISheetEditor`, in entrambi i modi:
- `PreviewData` al posto di `MaterialView.CaptureState()`: in entrambe le viste è lo stato del calcestruzzo non ancora confermato;
- una vista nuova (`new MaterialView()` + `RestoreState`) diventa un foglio aperto dalla fabbrica della finestra (`sheetEditors.Open`),
  cioè il percorso di apertura di un documento (domanda S5);
- l'identità della scheda («Riprendi perde la scheda») è quella dell'editor;
- il testo mostrato dalla scheda del calcestruzzo si cerca nella vista dell'editor (`editor.View`).

Il resto passa da `OpenSheet`, con il lato MVVM in `tests/Desktop/MaterialSmokeChecks.cs`:
- `SetConcreteState`: `RestoreState` sulla vista storica; sulla vista XAML gli stessi valori impostati attraverso i controlli
  (`ConcreteMaterialSmoke.Apply`: classe, campi, scelte, tolleranza dopo il suo controllo, opzioni, esposizione del menu);
- `CheckConcrete`: `MaterialView.Check` sulla vista storica, `ConcreteMaterialSmoke.SelfCheck` sulla vista XAML;
- `TypeConcreteNumber`: digitazione in un campo del calcestruzzo, al posto della riflessione su `numbers`;
- `Steel` (`SteelSmoke`): tipo, campi, ingresso e riga di stato dell'acciaio, con l'una o l'altra vista.

| Suite | Che cosa leggeva della vista storica | Ora |
| --- | --- | --- |
| `ui/smoke-materials` | `historicEditor.materials`: identità, `Check()`, `CaptureState`/`RestoreState`, `new MaterialView()` | identità dell'editor, `CheckConcrete`, `PreviewData`, `SetConcreteState`, fogli aperti dalla fabbrica |
| `ui/smoke-hierarchy` | `CaptureState`/`RestoreState` (classe C40/50), `rebarMaterial.Selection` | `PreviewData` + `SetConcreteState`, `Steel.Type` |
| `ui/smoke-sharing` | `CaptureState`/`RestoreState` (classi, stato con due esposizioni); in `CheckCoverWarnings` i `TextBlock` della vista storica e la riflessione su `numbers` | `PreviewData` + `SetConcreteState`; lo stato con due esposizioni aperto in un foglio nuovo; `TextBlock` di `editor.View`; `TypeConcreteNumber` |
| `ui/smoke-steel` | `Selection`, `Properties.Editors`, `Properties.Set`, `Input`, `Status` | `Steel` |

**`SelfCheck`** ripete `MaterialView.Check` (`supporto/test/Materiali/MaterialViewChecks.cs`) sulla vista XAML:
- stessi benchmark delle librerie;
- stesse modifiche attraverso i controlli: classe, esposizione dal menu, aderenza, composizione automatica, classe minima,
  descrizioni, copriferro, tolleranze, criterio, diametro non valido, consistenza;
- stesse attese.

Differenze:
- fck della classe dal ViewModel: la scheda di fck della vista storica era costruita e mai mostrata, come in `MaterialSheetViewChecks`;
- XF2, spuntata a mano nella vista storica, si sceglie dal menu;
- le combinazioni di esposizioni (domanda S4).

**Domande e Modified.** `SetConcreteState` sulla vista XAML passa dai controlli, quindi solleva `Modified`; il `RestoreState` storico
no. Nessuna suite ne dipende: la propagazione nel progetto si decide al `Commit` della finestra, sui dati.

### 2.3 Suite dei muri (gruppo 3, commit `a1bbb117`)

Le due suite guidano il muro attraverso `openSheet.Wall` (`WallSmoke`, `tests/Desktop/WallSmoke.cs`), con i nomi della vista storica:
form e campo («valley», «height_mode»), schede («Terreno», «Sisma»), tabelle con i nomi delle tracce di F5.9 («valle», «monte», «azioni»,
«matrice», «matrice-globale»).
- **`HistoricWallSmoke`** chiama gli stessi membri di `RetainingWallWorkspace` che le suite usavano: `Forms`, `Cards`, griglie, `Pages`,
  pulsanti, `CalculateAsync`, `Example`, `AddAction`, `GenerateMatrix` e gli altri. Con la Release il comportamento non cambia.
- **`MvvmWallSmoke`** usa i controlli di `RetainingWallSheetView` come `RetainingWallViewChecks`:
  - editor dei `FieldForm` costruiti anche nei gruppi chiusi (`Realize`); per i numeri formattati si simula il fuoco, come nelle tracce;
  - celle delle tabelle scritte attraverso la riga della griglia (`UpdateSourceTrigger=PropertyChanged`);
  - pulsanti «Ricalcola», «Calcola globale», «Mensola esempio»/«Gravità esempio», «+ Azione», «Genera / ripristina automatiche»,
    «Prepara dal muro», «Genera globali», «Sisma», «Apri sezione in c.a.»;
  - scelte della combinazione, della vista, del filtro e della combinazione globale; la prima tabella di «Riepilogo e dettagli»;
  - disegni della vista (`InputDrawing`, `DiagramsDrawing`, `GlobalDrawing`).

I calcoli sul ViewModel si attendono con `PendingWork`, entro 120 s.

**Dialoghi sulla vista XAML** (servizi dei fogli del banco):
- «Valori di calcolo…» passa da `BenchSheetServices.DialogAsync`, perché la finestra resta aperta mentre la suite ci lavora:
  - la vista di `RetainingWallParametersViewModel` va in una finestra fuori schermo (schermata `valori-di-calcolo.png`);
  - si cambia il valore della riga e si preme «Applica e ricalcola»;
  - si attende la chiusura, al più 15 s come la prova storica.
- «Invia / carica terreno…» apre il dialogo condiviso `SoilTransferDialogViewModel` (T5):
  - la sua vista si disegna fuori schermo per la schermata `trasferisci-terreno.png`;
  - «Invia a un nuovo foglio» (`SendCommand`) usa la destinazione proposta, come il clic sulla finestra storica;
  - la finestra principale apre poi la copia del palo come prima.

**Equivalenze:**
- «Invio terreno/sezione conserva il muro»: si controlla l'identità dell'editor invece di quella di `RetainingWallWorkspace`, che è
  unico per editor.
- Due zone d'armatura: la vista storica faceva `Forms["zones"].Set("two_zones", "true")` più una scrittura diretta nei dati; la vista
  XAML spunta la casella, cioè l'azione dell'utente.
- Didascalie del report: `WallAdvancedChecks.ReportFigureCaptions` ha un sovraccarico con il numero di tavole della distinta. Sulla
  vista XAML le tavole vengono da `RetainingWallBarDrawing(Calculation.Input, Sheet.CreateBarSchedule())`.
- `Forms["global"].BringIntoView()` diventa il `FieldForm` di quel form; serve solo per la schermata.

## 3. Suite `-mvvm`

Moduli fra i sette che ogni suite apre: servono a sapere quando un duplicato diventa superfluo (sezione 4).

| Suite | Moduli aperti fra i sette |
| --- | --- |
| `ui/smoke-mvvm` | palo e micropalo verticale |
| `ui/smoke-display-mvvm` | palo e micropalo verticale |
| `ui/smoke-projects-mvvm` | palo verticale |
| `ui/smoke-project-workspace-mvvm` | tutti e sette: acciaio, palo, e ogni modulo nella revisione in sola lettura |
| `ui/check-appearance-mvvm` | palo verticale, muri di sostegno |
| `ui/smoke-materials-mvvm` | calcestruzzo |
| `ui/smoke-hierarchy-mvvm` | calcestruzzo, acciaio |
| `ui/smoke-sharing-mvvm` | calcestruzzo |
| `ui/smoke-steel-mvvm` | acciaio |
| `ui/smoke-retaining-wall-mvvm` | muri di sostegno, palo verticale (la copia del terreno e il foglio del progetto, letti con `ISheetEditor`) |
| `ui/smoke-global-stability-mvvm` | muri di sostegno |

## 4. Che cosa resta da fare a ogni passaggio della Release

Al commit che porta un modulo a `Mvvm` in `ModuleImplementations.Declared` (con l'archiviazione della vista storica, Q11):
1. togliere la sua coppia da `$MvvmBench` in `build/ci.ps1`;
2. togliere ogni suite `-mvvm` i cui moduli (sezione 3) sono ora tutti MVVM nella Release: è uguale alla suite normale;
3. togliere i rami storici di `OpenSheet` che servivano solo a quel modulo, e le partial di prova di `SheetEditor` che chiamavano.

Per modulo:
- **Palo e micropalo verticale** (insieme, come la vista):
  - si tolgono `ui/smoke-mvvm`, `ui/smoke-display-mvvm` e `ui/smoke-projects-mvvm`;
  - in `OpenSheet` restano storici solo `SetGeometryForSmoke` e `VerifyWpf` della sezione c.a. (`str_palo`, F5.10); si tolgono i rami
    storici di `WaitForAutomatic` e di `smoke-display`;
  - in `SheetEditor` si tolgono il ramo geo di `VerifyWpf`, `WaitForAutomatic` e l'intera partial di `DisplaySmokeChecks.cs`
    (`DisplayReportChecks` resta);
  - `ui/check-appearance-mvvm` resta finché i muri sono storici.
- **Muri di sostegno**:
  - si tolgono `ui/smoke-global-stability-mvvm` e `ui/smoke-retaining-wall-mvvm`. Il palo di quest'ultima si legge solo con
    `ISheetEditor`, ed è già coperto dalle suite del gruppo 1;
  - `ui/check-appearance-mvvm` si toglie se anche i pali verticali sono passati;
  - si tolgono `HistoricWallSmoke` e il sovraccarico di `WallAdvancedChecks.ReportFigureCaptions` sul workspace storico, insieme alle
    suite storiche dei muri elencate in [f5-9-muri.md](f5-9-muri.md), sezione 5.5.
- **Calcestruzzo**:
  - si tolgono `ui/smoke-materials-mvvm` e `ui/smoke-sharing-mvvm`, e `ui/smoke-hierarchy-mvvm` se anche l'acciaio è passato;
  - in `OpenSheet` si tolgono i rami storici di `SetConcreteState`, `CheckConcrete` e `TypeConcreteNumber`;
  - `supporto/test/Materiali/MaterialViewChecks.cs` segue la vista storica in `supporto/SUPERATI`.
- **Acciaio d'armatura**:
  - si toglie `ui/smoke-steel-mvvm`, e `ui/smoke-hierarchy-mvvm` se anche il calcestruzzo è passato;
  - in `SteelSmoke` si toglie il ramo di `RebarMaterialView`.
- **Fondazioni, stratigrafia**: basta togliere la coppia da `$MvvmBench` (`ui/smoke-project-workspace-mvvm` resta fino all'ultimo dei
  sette).
- **Ultimo dei sette**: si tolgono `$MvvmBench` e tutte le suite `-mvvm` rimaste. Fatto il 23/10/2026 con i muri (f5-pass-muri,
  [f5-9-muri.md](f5-9-muri.md) sezione 6): restano solo le suite della sezione c.a. con il suo banco (`$MvvmConcreteSection`).

Nessuna di queste suite ha bisogno di un'altra riscrittura.

## 5. Domande «da decidere» (scelte provvisorie, da rivedere in F5.15)

- **S1 · `VerifyPileOutcomes` sulla vista XAML.**
  - Prima: la prova storica scriveva risultati sintetici nella vista (`Result = …; UpdateVerification()`). Controllava ordine ed esito
    a 99, 100 e 101 kN, la copertura incompleta e l'azione mancante.
  - Ostacolo: un ViewModel non accetta un risultato scritto da fuori.
  - Scelta: gli stessi risultati sintetici passano dalla stessa funzione che entrambe le viste mostrano,
    `VerticalPilePresentation.Verification`, con le stesse attese. In più, la tabella della vista (`PileVerification` o
    `MicroVerification`) deve mostrare esattamente le righe di quella funzione per il risultato del foglio.
  - Alternativa: un servizio di calcolo di prova che pubblica il risultato sintetico attraverso il ViewModel. Richiede un foglio aperto
    a parte con quel servizio, e il risultato sintetico deve bastare a grafici e tabelle.
- **S2 · Grigio del γsat automatico.**
  - Prima: la prova storica confrontava il colore del suggerimento con `Appearance.Foreground(Brushes.Gray)`.
  - Scelta: la vista XAML usa il token `Token.Disabled` (P7), e la prova confronta con quel token.
- **S3 · Domande dei fogli MVVM nelle suite.**
  - Prima: le suite storiche saltavano le domande (`confirm: false`).
  - Scelta: sui fogli MVVM le domande arrivano a `BenchSheetServices.Confirmations`. Le suite rispondono «Sì» e controllano il testo
    della domanda. La risposta si imposta solo intorno al comando che la pone.
- **S4 · Combinazioni di esposizioni nel controllo del calcestruzzo.**
  - Prima: `MaterialView.Check` spuntava le caselle nascoste delle esposizioni e controllava i testi della vista storica:
    - XF3+XS3 e X0+XC4 nella composizione;
    - XC1+XS3: combinazione «molto aggressiva» e classe minima C35/45;
    - XS3+XF4: copriferro 60 mm.
  - Ostacolo: la vista XAML ha solo l'esposizione del menu (M10, accettata). Una combinazione non si può scegliere, e un archivio con più
    esposizioni si apre con quella del menu.
  - Scelta: le stesse attese sulle funzioni che le due viste mostrano (`MaterialSheetPresentation.Evaluate` con le esposizioni attive,
    `MaterialExposurePresentation.Texts`), sullo stato del foglio in quel momento.
  - Alternativa: togliere questi controlli, perché nessun utente può arrivare a quello stato.
- **S5 · Vista nuova del calcestruzzo nelle suite dei materiali.**
  - Prima: `new MaterialView()` + `RestoreState(stato)`. Serviva per tre controlli: un foglio nuovo non eredita le modifiche; gli
    input incompleti si conservano alla riapertura; uno stato con due esposizioni ne tiene una.
  - Scelta: un foglio aperto dalla fabbrica della finestra (`sheetEditors.Open(…).PreviewData`), in entrambi i modi. È il percorso di
    apertura di un documento, con `NormalizeState` come all'apertura della vista storica.
  - Cambia quindi anche la suite con la Release: passa dall'apertura di `SheetEditor` invece che dal solo `RestoreState`.
  - Alternativa: tenere `RestoreState` per la vista storica e `Reload` del ViewModel per la vista XAML.

## 6. Esiti

| Corsa | Esito |
| --- | --- |
| `-Stage build,ui -Only '^ui/(smoke\|smoke-display\|smoke-projects\|smoke-project-workspace\|check-appearance)(-mvvm)?$'`, `20261018-011024-suiteui-g1` | PASS 11, NEW-FAIL 0 (le 5 suite con la Release e le 5 `-mvvm`) |
| profilo `quick` su `8aece8fe` (codice uguale), `suiteui-g1-quick` | PASS 52, NEW-FAIL 0 |
| `-Stage build,ui -Only '^ui/(smoke-hierarchy\|smoke-materials\|smoke-sharing\|smoke-steel)(-mvvm)?$'`, `20261018-013746-suiteui-g2` | PASS 9, NEW-FAIL 0 (build e le 4 suite nei due modi) |
| profilo `quick` (codice di `519acbf5`), `20261018-013852-suiteui-g2-quick` | PASS 52, NEW-FAIL 0 |
| `-Stage build,ui -Only '^ui/(smoke-retaining-wall\|smoke-global-stability)(-mvvm)?$'`, `20261018-020144-suiteui-g3` | PASS 5, NEW-FAIL 0 |
| `ui/check-wall-advanced-offscreen` (sovraccarico delle didascalie), `suiteui-g3-adv` | PASS |
| profilo `quick` (codice di `a1bbb117`), `suiteui-g3-quick` | PASS 52, NEW-FAIL 0 |
| **corsa finale** `-Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)'` su `56d16879`, `20261018-021530-suiteui-finale` | PASS 136, KNOWN 0, **NEW-FAIL 1** (`ui/check-palo-verticale-view`, ambiente: vedi sotto); build 25, fast 31, wiki 2, ui 78 + 1. Le 11 suite `-mvvm` nuove PASS, le 11 suite riscritte PASS con la Release. `ui/f3-equivalence` PASS, `Presentation.Tests` e `Architecture.Tests` PASS; nessun file tracciato modificato dalla corsa |
| `ui/check-palo-verticale-view` ripetuta da sola dal runner, `20261018-030247-suiteui-finale-pv` | FAIL come sopra |
| la stessa suite con `dotnet ANTHEA.dll --check-palo-verticale-view …` (stessa build) | **PASS 377 controlli** |

**`ui/check-palo-verticale-view` nella corsa finale: problema d'ambiente, non di questo ramo.**
- Sintomo: la suite si interrompe senza `errore.txt`, `dettaglio.txt` né `esito.txt`, con codice d'uscita 0.
  - Succede sempre allo stesso punto: «Salva PNG…» della quarta fixture (`elica-coesivo-avvisi`), dove l'immagine della capacità si scrive
    con `DocumentFile.Write`.
  - Nella cartella resta il file temporaneo `.x_<guid>.tmp`, già scritto.
- Riproduzione: stessa cosa nel runner, da sola, e lanciando a mano `ANTHEA.exe` come fa il runner. Con `dotnet ANTHEA.dll` e la stessa
  build passa: PASS 377 controlli.
- Causa probabile: il commento di `src/ANTHEA.Reports/DocumentFile.cs` descrive proprio questo comportamento. Il monitoraggio del
  comportamento dell'antivirus (Trend Micro, 27/09/2026) chiude senza errori un programma non firmato che rinomina file temporanei in
  documenti.
  - Per i documenti Office quel file copia invece di rinominare; per un PNG rinomina ancora (`File.Move`).
  - `ANTHEA.exe`, appena compilato, non è firmato; `dotnet.exe` sì.
- Questo ramo non tocca né `VerticalPileViewChecks` né `DocumentFile`; la suite usa servizi propri.
- Da decidere (S6, fuori da questo compito): estendere la copia di `DocumentFile.Write` anche alle immagini, oppure far partire le
  suite `check` con `dotnet`. Nessuna correzione è stata fatta qui (regola: segnalare, non correggere in silenzio).

Fuori dal compito, solo per informazione: `ui/smoke-material-report` e `ui/smoke-project-report`, lanciate a mano con i sette moduli MVVM,
passano senza modifiche. Usano solo `ISheetEditor`.

Schermo al 150% (144 DPI): le schermate delle suite valgono come documentazione.

## 7. Stato e ripresa

- Gruppo 1 (suite generali): fatto.
- Gruppo 2 (materiali: `ui/smoke-hierarchy`, `ui/smoke-materials`, `ui/smoke-sharing`, `ui/smoke-steel`): fatto.
- Gruppo 3 (muri: `ui/smoke-retaining-wall`, `ui/smoke-global-stability`): fatto.

Tutte le 11 suite sono indipendenti dall'implementazione e hanno il loro duplicato `-mvvm`. Non resta nessuna riscrittura di queste
suite. Ai passaggi della Release resta solo la pulizia della sezione 4.

Non fanno parte di questo compito e restano come sono:
- le suite storiche delle viste: `-historic`, `FoundationWorkspace.UiChecks`, `Stratigraphy.UiChecks`;
- le prove offscreen dei muri: `ui/check-global-guidance-offscreen`, `ui/check-wall-advanced-offscreen`,
  `ui/check-wall-materials-offscreen`;
- `ReportAppearanceChecks` e `ContrastAudit`.

Gli elenchi dei documenti dei moduli le riportano ancora.

Per riprendere: `dotnet build X.Desktop/X.Desktop.csproj -c UiTests`, poi una suite con o senza i sette moduli MVVM:

```
dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --smoke-steel <cartella> --module-impl mat_acciaio_armatura=mvvm …
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,ui -Only '^ui/smoke-steel(-mvvm)?$' -Tag prova"
```
