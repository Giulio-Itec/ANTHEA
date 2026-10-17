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

  Le suite chiamano `openSheet.X(…)` dove chiamavano `historicEditor.X(…)`.
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
  - dialoghi (`Dialog`: guida il ViewModel del dialogo e ne restituisce il risultato);
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

## 3. Suite `-mvvm`

Moduli fra i sette che ogni suite apre: servono a sapere quando un duplicato diventa superfluo (sezione 4).

| Suite | Moduli aperti fra i sette |
| --- | --- |
| `ui/smoke-mvvm` | palo e micropalo verticale |
| `ui/smoke-display-mvvm` | palo e micropalo verticale |
| `ui/smoke-projects-mvvm` | palo verticale |
| `ui/smoke-project-workspace-mvvm` | tutti e sette: acciaio, palo, e ogni modulo nella revisione in sola lettura |
| `ui/check-appearance-mvvm` | palo verticale, muri di sostegno |

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
- **Muri di sostegno**: `ui/check-appearance-mvvm` si toglie se anche i pali verticali sono passati.
- **Fondazioni, calcestruzzo, acciaio, stratigrafia**: per il gruppo 1 basta togliere la coppia da `$MvvmBench`.
- **Ultimo dei sette**: si tolgono `$MvvmBench` e tutte le suite `-mvvm` rimaste.

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

## 6. Esiti

| Corsa | Esito |
| --- | --- |
| `-Stage build,ui -Only '^ui/(smoke\|smoke-display\|smoke-projects\|smoke-project-workspace\|check-appearance)(-mvvm)?$'`, `20261018-011024-suiteui-g1` | PASS 11, NEW-FAIL 0 (le 5 suite con la Release e le 5 `-mvvm`) |
| profilo `quick` su `8aece8fe` (codice uguale), `suiteui-g1-quick` | PASS 52, NEW-FAIL 0 |

Schermo al 150% (144 DPI): le schermate delle suite valgono come documentazione.

## 7. Stato e ripresa

- Gruppo 1 (suite generali): fatto.
- Gruppo 2 (materiali: `ui/smoke-hierarchy`, `ui/smoke-materials`, `ui/smoke-sharing`, `ui/smoke-steel`): da fare.
- Gruppo 3 (muri: `ui/smoke-retaining-wall`, `ui/smoke-global-stability`): da fare.

Per riprendere: `dotnet build X.Desktop/X.Desktop.csproj -c UiTests`, poi una suite con o senza i sette moduli MVVM:

```
dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --smoke-steel <cartella> --module-impl mat_acciaio_armatura=mvvm …
powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,ui -Only '^ui/smoke-steel(-mvvm)?$' -Tag prova"
```
