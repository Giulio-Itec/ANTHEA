# F5 — Punti di controllo: le decisioni in una pagina

Questa pagina raccoglie le domande che le note dei moduli F5.4–F5.10 lasciano all'utente e i difetti trovati nelle viste attuali (17/10/2026). Non sostituisce le note: ogni voce porta il codice (D, M, V, S, O, E, MU, FN) per ritrovare il dettaglio.

Nella Release non è cambiato nulla: tutti i moduli aprono ancora le viste storiche. La vista nuova della palificata esiste solo nella build di prova.

Le risposte sbloccano le viste XAML. Le regole comuni (T1–T12) vanno decise una volta sola: le viste dei moduli si scrivono sopra.

Come rispondere: basta elencare i codici accettati e quelli da cambiare. «Proposta» è quello che scrive la nota del modulo. «Da decidere» vuol dire che la nota non dà una raccomandazione.

Già deciso, non si riapre: temi e token (`f5-temi-token.md`, sezione 8, punto di controllo F5.3 del 16/10). Token usati solo dalle viste nuove, accorpamento modulo per modulo con schermate (Q6 c); `Font.Subheading` = 14; accorpamento con ΔE ≤ 5 e ruolo compatibile; baseline della Chiara a 96 DPI.

## Decisioni trasversali

### T1. Riferimento F3: `esito.txt` cambia solo il numero

**Domanda.** Ogni ramo aggiunge fixture F3 e in `expected/esito.txt` cambia solo il numero delle fixture. La modifica è registrata nel manifest del ramo (`fixture_aggiunte_f5_X`, impronte prima e dopo). L'alternativa è una voce in `transitions-f5.json`. Il manifest va bene per tutti?

**Codici.** D2 (36 → 42), M1 (42 → 54), V2 (42 → 49, più 7 figure congelate in `f3-figures`), S1 (61 → 66), O8 (61 → 67), E9 (75 → 80), MU2 (69 → 81), FN2 (61 → 69).

**Proposta.** Il manifest, come in D2. I numeri sono per ramo: il coordinatore ricatena l'impronta quando unisce i rami (MU2, FN2).

### T2. Sola lettura dei form

**Domanda.** `FormViewModel.IsReadOnly` esiste (D4, fatto in F5.4): rifiuta digitazione, scelta e spunta; `FieldForm` blocca le caselle, mentre i «?» restano utilizzabili. La parità con `InputForm` è provata (2072 controlli). Diventa la regola per tutti i moduli?

**Codici.** D4, M7, V4. M7 e V4 sono stati scritti prima: dicevano «copie staccate finché `IsReadOnly` non c'è». Ora c'è.

**Proposta.** Sì, regola unica. I ViewModel di F5.5 e F5.6 passano da copie staccate a `IsReadOnly`.

### T3. Calcolo: sincrono, sullo sfondo, nessuno

**Domanda.** Quale modalità di calcolo usa ogni modulo? Lo stato dei ViewModel è questo:
- Sincrono sul thread dell'interfaccia, come oggi: palificata (Q8 a, D1; `SheetCalculationMode.Synchronous` come regola, D5), fondazioni (FN1: solo con «Calcola», nessun ricalcolo automatico), valori dell'acciaio a ogni modifica (M5).
- Sullo sfondo, ricalcolo 450 ms dopo l'ultima modifica e limitatore dell'applicazione: palo e micropalo verticale (V1), muri (MU1).
- Nessun calcolo: materiali e stratigrafia (M4, S2). «Calcola» conferma e valida, non c'è risultato, «Esporta risultati» rifiuta.

**Perché conta (Q8).** Con il calcolo sincrono la cattura F3 della palificata è uguale al riferimento, senza transizioni, e identica alla storica. Con lo sfondo cambierebbero `opened.json` (`direzione_selezionata`, `angolo`), servirebbero voci in `transitions-f5.json` e prove con barriera. Inoltre «Esporta report» subito dopo una modifica direbbe «Completare il calcolo prima di esportare il report». Il calcolo breve blocca il thread e non passa dal limitatore: con più fogli (F5.13) si potrebbe passare allo sfondo come differenza dichiarata.

**Proposta.** Confermare tutto come sopra.

### T4. Regole del componente comune

**Domanda.** Cinque scelte sul contratto comune, già implementate nei ViewModel:
- **D6 · `Reload`** (anche V6): il foglio si ricostruisce come all'apertura (migrazioni comprese) e ricalcola dopo il ritardo, senza `Modified`.
- **D7 · `Calcola`** da `MvvmSheetEditor` conferma prima le bozze (`Commit`); la palificata storica calcola senza. Catture e tracce risultano identiche.
- **M2 · `ISheetViewModel.PreviewData`**: membro nuovo, virtuale nella base. Alternativa: interfaccia facoltativa `ISheetPreview` e controllo di tipo nell'ospite.
- **O1 · Ganci additivi** `PublishedCalculation` e `CancelCalculation()` in `SheetViewModelBase`. Alternativa: un servizio del modulo che tiene l'ultima analisi, senza toccare la base.
- **E8 · `ISheetViewModel` reimplementato** dal ViewModel F5.7 per la scheda mostrata (`Result`, `HasResults`, `CalculateAsync`). Alternativa: membri virtuali nella base.

**Proposta.** Confermare D6, D7, M2, O1, E8 come implementati. Tre altre domande chiedono se un'esigenza locale va nel componente comune: M6, S7, FN5 (nei moduli).

### T5. Dialoghi condivisi

**Domanda.** «Invia / carica terreno…» (`SoilTransferDialog`) e «Modello e dati comuni…» (`CalculationHelpView`) servono a più moduli. I ViewModel non li hanno ancora. Come e quando si fanno?
- Il trasferimento del terreno serve a stratigrafia, muri, pali e fondazioni. Oggi resta la finestra storica dietro `ISoilTransferDialog` (in `Modules/Stratigraphy`). I muri espongono l'evento `SoilTransferRequested`, rifiutato in sola lettura.
- «Modello e dati comuni…» serve ai pali (V9, O7) e ad altri moduli.

**Codici.** V9, O7, FN8, S3, MU9.

**Proposta.** Un solo ViewModel del trasferimento con `IDialogService`, e la guida comune con `IWikiNavigator` o un dialogo (V9). L'interfaccia si sposta in un posto comune (S3). Il momento: un passo dedicato insieme a F5.6, F5.7, F5.9, F5.9b (S3), oppure quando si scrive la vista, o con F5.6b (V9, FN8). Da decidere quale.

### T6. Chiavi di stato scritte in sola lettura

**Domanda.** Oggi, in una revisione precedente (sola lettura), alcune azioni scrivono chiavi di stato dell'interfaccia e sollevano `Modified`: la revisione risulta «modificata» senza cambiamenti di dati. I ViewModel lo riproducono. Si conserva o diventa stato della vista non salvato (differenza dichiarata)?
- V5: caselle delle curve e «Tutte»/«Nessuna» scrivono `visibilita_grafici`.
- O3: la scheda «Risposta elastica» scrive `vista_orizzontale`. La visita di `ui/f3-equivalence` scrive la stessa chiave ed è nel riferimento F3.
- FN3: aprire o chiudere un gruppo e «Raccogli input» scrivono `vista_fondazioni`.
- FN4: la sezione del meccanismo (X/Y) è bloccata oggi solo in certi casi; il ViewModel la rifiuta sempre. Alternativa: ammetterla sempre (stato non salvato).
- E3: i gruppi chiusi dell'elastico; il ViewModel li protegge sempre, la differenza non si vede. Si accetta?

**Codici.** O3, FN3, V5, FN4, E3.

**Proposta.** Da decidere, con una regola unica per tutti. Se si sceglie «non scrivere», vale per V5, O3, FN3 e FN4 insieme.

### T7. Difetti storici: correggere come differenza dichiarata o conservare

**Domanda.** I ViewModel riproducono la vista storica così com'è, tranne dove la vista storica fallisce (V3, S4) o riscrive una revisione in sola lettura (MU3, MU4). Per gli altri casi si corregge nella vista nuova (voce F5-… del registro e prova sulla vista nuova) o si conserva? Il dettaglio è nella tabella dei difetti.

**Codici.** V3, S4, S6, O4, O5, O6, E4, E5, E7, MU3, MU4, MU5, MU6.

**Proposta delle note.**
- Differenza dichiarata: V3 (con una prova sulla vista nuova), S4 (al passaggio della Release), O6 (con una prova), MU3 e MU4 (da accettare).
- Conservare: MU5 e MU6 (con alternativa), E7 (fino a F5.13).
- Da decidere: S6, O4, E4, E5. O5: correzione dopo F5.

### T8. Report: il PDF

**Domanda.** `MvvmSheetEditor.ExportReport` ricava oggi il PDF dal DOCX riletto (`ReportFiles.Write`). La vista storica dei materiali e delle fondazioni lo scrive dal documento (`MaterialReportChapter.Write`, `FoundationReport.Pdf` con MigraDoc). Si scrive come oggi, per modulo?

**Codici.** M12 («da verificare al passo dell'ospite»), FN6 («da confermare insieme a M12»).

**Proposta.** L'ospite scrive il PDF come oggi, per modulo, con la prova che il risultato è uguale. Nella palificata il PDF affiancato è già uguale.

### T9. Peso delle tracce e dipendenza da font e DPI

**Domanda.**
- **O9 · Diagrammi come dati semplici.** I dati congelati sono circa 4 MB di JSON indentato (6 fixture). Si accetta il peso o si preferisce un'impronta per diagramma?
- **E9 · Riferimento elastico.** Gli attesi nuovi pesano circa 5 MB e le tracce circa 3,9 MB (oltre al cambio del numero, T1).
- **O10 · Elenchi di disegno.** Dipendono dai font installati (Segoe UI): valgono sulla macchina del banco, come le prove di contrasto. Si accetta come prova dell'identità del disegno?
- **E10 · Elenchi di disegno dell'elastico.** La cattura con `ANTHEA.exe` e con `dotnet ANTHEA.dll` è uguale sul banco (schermo al 150%). Va ripetuta a 96 DPI.

**Proposta.** O9: da decidere. O10: accettare, come oggi. E10: ripetere a 96 DPI (vedi T10).

### T10. Contrasto da ripetere a 96 DPI

**Domanda.** Nessuna decisione: è un passo da fare. `ui/check-contrast` è stata esclusa dalle corse finali di F5.8, F5.9 e F5.9b perché lo schermo della macchina è al 150% (144 DPI) e la baseline è a 96 DPI; a un altro DPI la prova fallisce senza confrontare (D6 dei token, già deciso). I rami non toccano i colori.

**Codici.** MU13, FN11, E10.

**Proposta.** Ripeterla a 96 DPI prima dell'unione di ogni ramo. Per la palificata la Chiara della vista nuova ha gli stessi 4 difetti della storica (D12, nei moduli).

### T11. Strumenti di prova

**Domanda.**
- **D13 · Suite UI.** La parte UI è in `ui/check-pile-group-view` dell'harness UiTests (raggiunge `MvvmSheetEditor` senza riflessione), non in un progetto `tests/ANTHEA.Desktop.UiTests` (richiederebbe riflessione o `InternalsVisibleTo`). Si conferma per i moduli successivi?
- **D14 · Banco interattivo.** `ANTHEA.exe --interactive --module-impl <modulo>=mvvm` esiste solo nella build UiTests, per le prove a mano. Si tiene fino a F5.14?
- **S11 · `MessageBoxHook`.** Risponde ai `MessageBox` delle viste storiche fuori schermo, senza mouse né tastiera. Può servire anche a muri e ponte. Si adotta come strumento comune dell'harness?

**Proposta.** D13: confermare (i piani di muri e fondazioni già lo seguono). D14 e S11: da decidere.

### T12. Baseline F4 e disegni

**Domanda.** Le baseline F4 non scendono nei rami dei moduli (M8, S12, MU11). Quella dei materiali scende di 4 voci (5 occorrenze) all'archiviazione della vista storica, la P3 di 3. Nei muri e nelle fondazioni le voci F4 rimaste sono nei disegni, che restano WPF (Q3). FN7: si accetta che la baseline delle fondazioni scenda solo se si passa ai disegni con dati già convertiti (lavoro dei disegni, fuori da F5.9b)?

**Codici.** M8, S12, MU11, FN7.

**Proposta.** Baseline ferma (FN7), da confermare. Gli altri codici sono informativi.

## Domande per modulo

Le voci già coperte dalla sezione trasversale sono elencate in testa a ogni modulo. «Nei difetti» rimanda alla tabella.

### F5.4 — Palificata orizzontale (pilota)

Coperte: D1 e D5 (T3), D2 (T1), D4 (T2), D6 e D7 (T4), D13 e D14 (T11).

**Esiti automatici.** Vista XAML con la disposizione di oggi e solo token. Tracce uguali riga per riga, sia dal ViewModel sia attraverso la vista. Cattura F3 contro il riferimento: 42 fixture, 514 file, 0 differenze non ammesse. Confronto diretto storica/MVVM: 514 file senza differenze, quindi anche le 8 fixture della palificata. Report Word e PDF uguali. Corsa `build,fast,wiki,ui`: PASS 112, KNOWN 0, NEW-FAIL 0. Contrasto: Scuro 0, Molto scuro 0; Chiara 4 difetti, gli stessi della storica.

**Da guardare.** Schermate nei tre aspetti, vista nuova e storica, in `supporto/artefatti/refactoring/f5/f5-4-vista/vista/schermate/` del worktree di integrazione `C:\Users\g.pacini\AppData\Local\Temp\aw-f5`. Prove a mano: la build portatile è già a disposizione; l'elenco dei passi è nella sezione 4.5 di `f5-4-pilota.md`.

**Domande.**
- **D3 · Interassi mancanti.** Se un archivio non ha l'oggetto degli interassi della direzione mostrata all'apertura, la vista lo crea vuoto (senza `Modified`); la preparazione del capitolo autonomo no. Il report è uguale e nessuna fixture lo mostra. Allineare `ModuleSheetPreparation.PreparePileGroup` (modifica dell'Application)? Da decidere.
- **D8 · Etichette delle direzioni.** Senza gli spazi di allineamento; le due righe diventano due colonne (+X/−X, +Y/−Y). Differenza visibile, da confermare.
- **D9 · Titolo.** 20 → 22 punti (`Font.Title`), l'unico accorpamento tipografico (Q6 c). Alternativa: un token da 20, che oggi non esiste.
- **D10 · Disposizione dei campi.** `FieldForm` compatto come negli altri moduli: editor di 82 px invece di 94, colonna dell'unità vuota (le etichette contengono già l'unità). Per conservare la disposizione il ViewModel ha form per blocco visivo. Si accetta?
- **D11 · F1.** La vista storica apriva la guida del modulo con F1 su qualunque campo. La nuova apre la sezione del campo quando esiste (solo il diametro: «Effetto di gruppo e applicabilità»), altrimenti la guida. I «?» sono gli stessi. Differenza di comportamento, da confermare.
- **D12 · Contrasto della Chiara.** La vista nuova conserva i colori di oggi, quindi i 4 difetti delle etichette degli indicatori (T9 vale anche qui). Proposta: conservare; al passaggio le 4 voci passano al passo del modulo, a totale invariato. Alternativa: correggere il contrasto, cambiando la Chiara.
- **Bordo delle schede.** `#D8E0EB` di `Ui.Paper` diventa `Token.Border` (`#DCE2E9`, ΔE 2,3) in Chiara; negli aspetti scuri i due colori coincidono. Nella tabella dei token il colore è vicino a `Border` (ΔE 2,3) e a `Border.Strong` (4,2); il criterio ΔE ≤ 5 con ruolo compatibile è già accettato. La vista nuova usa `Border`. Da guardare nelle schermate della Chiara; l'alternativa è `Border.Strong`.

Senza decisione: la guida pratica (riga 819) cita l'espansore «Applicabilità e interassi rappresentativi»; nelle due viste si chiama «Opzioni di calcolo e applicabilità». È così anche oggi.

### F5.5 — Materiali (calcestruzzo, acciaio)

Coperte: M1 (T1), M2 (T4), M4 e M5 (T3), M7 (T2), M8 (T12), M12 (T8). Nessun difetto trovato nelle viste.

- **M3 · Modifiche del calcestruzzo trattenute fino alla conferma.** I dati cambiano solo a `Commit`, come oggi. Uniformare alla scrittura immediata degli altri moduli cambierebbe le tracce. Proposta: non farlo in F5.
- **M6 · Campi fissati dal catalogo dell'acciaio.** Sono stato della vista (`Locked`, `NameLocked`, `DiagramEnabled`) come i `TextBox` di sola lettura di oggi: il ViewModel non rifiuta la scrittura. Alternativa: sola lettura per campo in `FieldViewModel` (componente comune). Da confermare lo stato della vista.
- **M9 · Reologia.** Le viste dei materiali non aprono la finestra «Reologia del calcestruzzo» (la aprono il menu Strumenti e la sezione c.a.). Un comando nella scheda del calcestruzzo sarebbe una funzione nuova. Da decidere.
- **M10 · Caselle delle esposizioni.** La vista storica crea una casella per classe ma non la mostra: l'esposizione si sceglie dal menu. Il ViewModel tiene «esposizioni attive» uguale al menu; la vista nuova non avrà le caselle. Le usa solo `MaterialViewChecks`. Da confermare.
- **M11 · Disposizione dei campi del calcestruzzo.** Oggi etichetta, controllo di 136 px e unità di 44 px, diversa da `InputForm`/`FieldForm`. Proposta: tenerla con un modello di campo proprio.
- **M13 · `MaterialView.ReportProperties()`** non è usato da nessuno e non è stato portato. Informativo.
- **M14 · Persistenza.** Nessuna chiave di solo stato dell'interfaccia. Informativo.

### F5.6 — Palo e micropalo verticale

Coperte: V1 (T3), V2 (T1), V4 (T2), V6 (T4), V9 (T5), V5 (T6). Nei difetti: V3.

- **V7 · «Occupato».** Dopo una modifica durante un calcolo, `Busy` storico resta vero finché il motore non torna; nel ViewModel `IsBusy` diventa falso subito. Dall'esterno si vede un solo caso: un secondo «Calcola» con gli stessi dati mentre il primo è in corso; il ViewModel ammette allora l'esportazione del risultato già pubblicato (quello degli stessi dati). Da confermare.
- **V8 · Figura della capacità nel report.** Il report storico copia il grafico come appare (zoom e spostamento compresi). Il ViewModel passa le serie mostrate e la figura si disegna a vista intera. Cambia solo l'immagine, che il confronto F3 non vede. Da confermare.
- **V10 · Colore degli strati.** Il ViewModel non scrive più `__color`; la vista usa `Swatch.Soil1`–`6` per indice, uguali ai colori di oggi. Informativo.
- **V11 · Sezioni del report.** La finestra «Contenuti del report Word» resta in `MainWindow`; `ModuleReportCapabilities` dichiara ancora solo il report short. Proposta: le sezioni nel contratto con la shell (F5.13).
- **V12 · Ampiezza del passaggio.** Il palo verticale è il documento predefinito della finestra: molte suite UI aprono un foglio verticale. Il passaggio della Release richiede prima le suite riscritte (nota F5.6, sezione 3, punto 3). Si conferma come lavoro dello stesso passo?

### F5.6b — Stratigrafia

Coperte: S1 (T1), S2 (T3), S3 (T5), S11 (T11), S12 (T12). Nei difetti: S4, S6, S10.

- **S5 · Importazione IA spenta.** Il ViewModel la conserva spenta con lo stesso suggerimento. `StratigraphySheetOptions.ImportEnabled` la attiva solo nelle prove. La riattivazione (configurazione e fatturazione da definire, STRAT-2) diventa una riga della radice di composizione, e va scritta anche la vista del dialogo. Da confermare.
- **S7 · Campi su più righe.** Descrizioni, fonti e note vanno a capo e accettano l'invio; `FieldSpec` non lo sa. Due strade: una proprietà `Multiline` di `FieldSpec`/`FieldForm` (componente comune), oppure un modello di campo locale nella vista. Da decidere prima della vista.
- **S8 · Disposizione.** `FieldForm` per blocco, con la nota sui terreni misti fra comportamento e descrizione e medi e minimi affiancati. Si accetta?
- **S9 · Voci delle scelte.** `FieldViewModel` aggiunge alle voci il valore corrente quando non è fra quelle (regola di `InputForm`); la casella storica no. Con un documento valido non succede mai. Si accetta?

### F5.7 — Palo e micropalo orizzontale

Coperte: O1 (T4), O7 (T5), O8 (T1), O9 e O10 (T9), O3 (T6). Nei difetti: O4, O5, O6.

- **O2 · Ampiezza del passo rispetto a F5.8 (e E2).** Con la scheda elastica di F5.8 il ViewModel copre ora i due moduli interi. Proposta: passare i due moduli insieme, dopo le viste XAML delle due schede. Alternativa di O2: passare F5.7 prima, con la vista elastica storica ospitata nella seconda scheda. Quale?

### F5.8 — Risposta elastica e armature

Coperte: E8 (T4), E9 e E10 (T9), E3 (T6). Nei difetti: E4, E5, E6, E7.

- **E1 · Verificatore c.a. del tratto.** La vista storica apre un dialogo modale con una `ConcreteWorkspace`; «Applica armatura…» porta l'armatura nel palo. Il ViewModel chiede il foglio all'ospite con `ISheetHost.OpenModuleCopy`, che oggi apre una copia in un'altra finestra, senza canale di ritorno. Proposta: fino a F5.10 l'ospite desktop mostra il foglio nel dialogo storico e chiama `Apply`/`Close` (un membro nuovo di `ISheetHost`, oppure un dialogo di `IDialogService` che ospita la `ConcreteWorkspace`). Alternativa: accettare la copia senza ritorno come differenza dichiarata. Quale?
- **E2 · Passaggio insieme.** Come O2 (F5.7): si conferma il passaggio dei due moduli insieme, dopo le viste XAML delle due schede?

### F5.9 — Muri di sostegno

Coperte: MU1 (T3), MU2 (T1), MU9 (T5), MU11 (T12), MU13 (T10). Nei difetti: MU3, MU4, MU5, MU6.

- **MU7 · Stato durante l'attesa di una ricerca.** Se un calcolo esce dal ritardo mentre una ricerca lavora, il ViewModel mostra subito «Calcolo di equilibrio e verifiche GPC…» e resta occupato; la vista storica lo mostra quando la ricerca finisce. Le tracce non lo vedono (lo stato finale coincide). Da accettare, oppure aggiungere alla base l'attesa del lavoro secondario prima di `Running`.
- **MU8 · Eventi a catena delle combo.** Il ViewModel riproduce gli aggiornamenti a catena delle combo e delle selezioni delle griglie: è ciò che rende le tracce identiche. Dopo il passaggio si potranno semplificare con una nuova cattura. Da accettare.
- **MU10 · Distinta ferri e finestra dei valori.** Proposta: la finestra dei valori come dialogo modale su `RetainingWallParametersViewModel`; la distinta come dialogo della vista su `CreateBarSchedule`, senza ViewModel proprio. Da decidere.
- **MU12 · Generazione delle combinazioni.** Resta nella scheda, come oggi; portarla nel Model è un lavoro separato, dopo F5.9. Informativo.

### F5.9b — Fondazioni superficiali

Coperte: FN1 (T3), FN2 (T1), FN3 e FN4 (T6), FN6 (T8), FN7 (T12), FN8 (T5), FN11 (T10).

- **FN5 · Scelte a indice.** Le cinque scelte del metodo scrivono l'indice della voce, non il testo. Restano un componente esplicito (`ShallowFoundationChoiceViewModel`) invece di `FieldSpec`. Si accetta, o si aggiunge una regola di scrittura «indice» a `FieldWriteRule` (componente comune)?
- **FN9 · Aiuto dei campi.** I campi numerici della vista storica non hanno «?» né F1 per campo; la falda e il calcestruzzo sì (`InputForm`). Il ViewModel conserva la distinzione. Con `FieldForm` si potrebbe dare l'aiuto a tutti: differenza visibile, da decidere.
- **FN10 · Disponibilità della libreria.** Il ViewModel riceve dal modulo se il rilascio approvato è installato (`FoundationLibrary.Available`) e cambia solo il testo dei risultati senza calcolo. Informativo.

### F5.10 — Sezione c.a. (parte A: fixture, tracce, derivazioni, piano dei sottopassi)

Nessun ViewModel ancora: la [nota](f5-10-sezione.md) divide il modulo in nove sottopassi (S1–S9) e chiede prima queste
scelte. Ogni codice rimanda alla sezione 7 della nota.

- **C1 · Calcolo.** Un coordinatore in Presentation sopra i calcoli della sezione, con il foglio in modalità `None`, oppure
  una modalità nuova del componente comune? Da decidere.
- **C2 · Taglio.** Il calcolo del taglio resta sul thread dell'interfaccia come oggi? Da decidere (vedi T3).
- **C3 · Azioni.** `SectionActionsViewModel` passa da Application a Presentation (Q17)? Da decidere.
- **C4 · Verificatore del palo elastico.** Un dialogo che restituisce i dati al palo; è la stessa domanda di E1 (F5.8).
- **C5 · Comportamenti storici.** Si conservano e diventano voci del registro (vedi T7)?
- **C6 · Errori parziali.** Il ViewModel dà un ordine fisso agli errori parziali, che oggi non è deterministico?
- **C7 · Peso delle tracce.** 7,3 MB e 165 s: come dividerle fra gli stadi delle prove?
- **C8 · Disegni.** I disegni restano WPF e il dominio 3D si confronta solo come dati (vedi T9)?
- **C9 · Trefoli e calcolo bloccato.** Coperti solo dalle tracce: bastano?
- **C10 · Strategia.** Sottopassi in sequenza, con la Release storica fino a S9?

## Difetti trovati nelle viste attuali

Comprende i difetti e i comportamenti storici che le note chiedono di decidere. Nei muri e nelle fondazioni nessun difetto tocca i risultati: tracce, F3 e capitoli autonomi coincidono. Nei materiali non ne sono stati trovati.

| Modulo | Cosa succede oggi | Come si comporta il nuovo ViewModel | Proposta |
| --- | --- | --- | --- |
| F5.4 palificata | Contrasto della Chiara: 4 etichette (MINIMO, MEDIA, MASSIMO, RIDUZIONE MEDIA), `#64748B` su `#F3F5F8`, 4,36:1 (D12) | Stessi colori e stessi 4 difetti | Conservare; alternativa: correggere cambiando la Chiara |
| F5.6 micropalo verticale | Cambio dell'iniezione (IGU↔IRS) con un risultato presente: eccezione «Sequence contains no matching element» in `VerticalPileCharts.Reference`. Gli α restano riscritti, senza `Modified` né ricalcolo; il risultato mostrato è quello dell'iniezione precedente (V3, punto 1) | Non fallisce: ricalcola il grafico di riferimento solo con il risultato dei dati mostrati, o senza risultato dopo una modifica | Differenza dichiarata (voce F5-…) con una prova sulla vista nuova; non si corregge la vista storica |
| F5.6 micropalo verticale | Probabile, non provato: stessa eccezione copiando una stratigrafia su una con terreni di un'altra famiglia, o quando l'eliminazione sposta la selezione (V3, punto 2) | Come sopra | Come sopra |
| F5.6 verticale | In sola lettura, curve e «Tutte»/«Nessuna» scrivono `visibilita_grafici` e sollevano `Modified` (V5) | Fa lo stesso | Da decidere (T6) |
| F5.6b stratigrafia | «No» alla rimozione dei minimi solleva `Modified` senza cambiamenti (S6) | Lo riproduce | Da decidere: conservare o correggere come differenza dichiarata |
| F5.6b stratigrafia | «Invia / importa terreno» con un documento non valido: la conferma lancia un'eccezione non gestita (registro degli errori e messaggio del gestore globale) (S4) | Mostra l'errore con il `CommandGuard` (Q19), senza registro | Differenza dichiarata al passaggio della Release |
| F5.6b stratigrafia | Importazione IA: la finestra scarta il task (`_ = Extract()`), un errore imprevisto andrebbe perso. Oggi irraggiungibile, l'importazione è spenta | Passa dal `CommandGuard` | Nessuna decisione; vedi S5 |
| F5.6b stratigrafia | `BuildReport` del foglio storico chiede un calcolo che nessuno chiama per la stratigrafia; il report si esporta con `ExportReport` (S10) | Dà la scheda anche da `BuildReport`, usato da `MvvmSheetEditor` | Da confermare |
| F5.7 orizzontale | In sola lettura la scheda «Risposta elastica» resta selezionabile e scrive `vista_orizzontale` con `Modified`; la visita di `ui/f3-equivalence` scrive la stessa chiave (O3) | Lo riproduce | Da decidere (T6): mantenere o non scrivere, come differenza dichiarata |
| F5.7 orizzontale | Due `Modified` per ogni modifica della scheda della capacità: uno dalla risposta elastica (`InvalidateShared`), uno dalla scheda (O4) | Lo riproduce | Da decidere: mantenere o uno solo (nessun effetto sui dati; lo vedrebbe il contatore delle modifiche) |
| F5.7 orizzontale | Un nuovo strato riceve `reazione_orizzontale` quando la riga è mostrata, dopo la copia dei dati per il calcolo e senza `Modified`; lo stesso al cambio di tipologia con Kp invariato (O5) | Lo riproduce emulando la riga mostrata | Dopo F5: preparare il campo alla creazione dello strato (cambierebbe i dati prima del calcolo e le tracce) |
| F5.7 orizzontale | Acciaio personalizzato non mostrato: se un valore dell'acciaio della sezione c.a. (fyk, Es, fu, εu, legame) lo rende personalizzato, i dati diventano «Personalizzato» ma la scelta «Acciaio» mostra ancora la classe precedente; alla riapertura è giusta. Probabile, non provato: anche la nota della classe scelta dal catalogo è calcolata prima che il catalogo scriva i valori (O6) | Lo riproduce | Correggere nella vista nuova (differenza dichiarata, voce F5-…) con una prova. Alternativa: conservarlo fino a F5.14 |
| F5.7 orizzontale | Passando alla scheda elastica il calcolo della capacità in attesa o in corso è annullato; il risultato pubblicato e l'ultimo stato restano (anche «Calcolo di sezione ed equilibri in corso…»). Dopo il passaggio al momento manuale resta la nota del momento c.a.; la sezione in errore conserva diametro della staffa e didascalia | Lo riproduce | Nessuna domanda nella nota |
| F5.8 elastico | Dopo un calcolo in sola lettura le schede si ricostruiscono e i campi dei gruppi chiusi non sono protetti finché il gruppo non si apre; aperto, li si trova protetti (E3) | Li protegge sempre; la differenza non si vede | Accettare? (T6) |
| F5.8 elastico | Archivio da migrare con confronto in sospeso: lo stato dice «Dati modificati · analisi FEM e convergenza in coda» con barra indeterminata, ma nessun calcolo è in coda. Dopo un errore di importazione la barra resta indeterminata (E4) | Lo riproduce | Da decidere: correggere (stato e barra coerenti, differenza dichiarata) o conservare |
| F5.8 elastico | Quando la quota selezionata si perde, la sezione perde contorno, barre e staffa ma conserva didascalia, diametro della staffa e assi (E5) | Lo riproduce | Da decidere: correggere o conservare |
| F5.8 elastico | Il calcolo elastico gira sempre: ogni modifica dei dati comuni, anche sulla scheda della capacità, mette in coda FEM e verifiche di sezione; passare alla capacità non lo annulla (E7) | Lo riproduce | Conservare fino a F5.13 |
| F5.8 elastico | Le schede dei tratti si ricostruiscono a ogni risultato: l'intestazione («T1 · 0 → 5 m») resta quella vecchia fino al risultato successivo; la vista storica salta la ricostruzione se il fuoco è nell'editor (E6) | Ricostruisce sempre | Nella vista nuova aggiornare sul posto, con le stesse scritture |
| F5.9 muri | «Valori di calcolo…» in sola lettura: la finestra è fuori dall'albero protetto, la tabella resta modificabile e «Applica e ricalcola» riscrive i dati della revisione e la segna modificata (MU3) | Finestra in sola lettura, rifiuta «Applica e ricalcola» | Differenza dichiarata, da accettare |
| F5.9 muri | «Stabilità globale» in sola lettura con il profilo vuoto: chiama «Prepara dal muro», scrive il profilo, segna la revisione modificata, azzera i risultati e ricalcola. 111 campi della pagina «Input» tornano modificabili e «Calcola armature» si riaccende (MU4) | Apre pagina e schede senza preparare il profilo | Differenza dichiarata, da accettare |
| F5.9 muri | «Calcola armature» resta spento dopo una modifica durante un calcolo: `UpdateFields` lo spegne e nulla lo riaccende fino alla modifica successiva (MU6) | Lo conserva | Conservare; alternativa: riaccenderlo quando il motore torna |
| F5.9 muri | Le ricerche (armature, calcolo globale, «Interrompi ricerca») annullano il ricalcolo in attesa se partono entro 450 ms da una modifica: le verifiche locali restano ferme fino alla modifica successiva o a «Ricalcola» (MU5) | Lo conserva | Conservare; alternativa: ricalcolare quando la ricerca finisce (differenza visibile, da decidere dopo il passaggio) |
| F5.9 muri | Con la falda attiva «Prepara dal muro» propone una linea sotto il muro sopra il piano di posa; il calcolo globale risponde «Water above the ground: free water is not supported.» (falda libera non supportata) | Non corretto: è del calcolo, fuori da F5.9 | Nessuna azione in F5.9 |
| F5.9 muri | Capitolo autonomo senza kh e kv del sito: la scheda li ricava dai parametri del sito all'apertura e li scrive, il capitolo autonomo no, quindi per un documento non salvato dalla scheda i dati preparati differiscono (il calcolo li ricava comunque; le fixture salvano i valori ricavati) | Non indicato nella nota | Da decidere |
| F5.9b fondazioni | La scelta «Sezione X / Sezione Y» della scheda «Rottura» è bloccata in sola lettura solo se la scheda esisteva alla protezione o è visibile; una combo ricreata da un calcolo in una scheda non mostrata resta attiva. È stato di sola vista, non salvato (FN4) | La rifiuta sempre in sola lettura | Da decidere; alternativa: ammetterla sempre (T6) |
| F5.9b fondazioni | In sola lettura aprire o chiudere un gruppo e «Raccogli input» scrivono `vista_fondazioni` e segnano il foglio modificato: una revisione precedente risulta «modificata» (FN3) | Lo conserva | Da decidere (T6): conservare o stato della vista non salvato |

| F5.10 sezione c.a. | Le figure dei domini nel report della vista evidenziano la riga selezionata (la prima inserita); il capitolo autonomo la prima per nome | Da definire al sottopasso | Da decidere |
| F5.10 sezione c.a. | L'ordine degli errori parziali non è deterministico quando due verifiche si fermano insieme | Da definire (C6) | Ordine fisso nel ViewModel |
| F5.10 sezione c.a. | Un documento di versione 2 senza `dominio2d` riceve N = +1000 | Da definire | Da decidere |
| F5.10 sezione c.a. | Nel dominio 2D SLV compare il messaggio grezzo della libreria «Value cannot be null. (Parameter 'failureDomain')» | Da definire | Da decidere |
| F5.10 sezione c.a. | In sola lettura la revisione scrive ancora scheda, filtri e opzioni dei grafici, con `Modified` | Da definire | Vedi T6 |
| F5.10 sezione c.a. | «Annulla» dell'editor delle sollecitazioni cambia i dati senza `Modified` | Da definire | Da decidere |
| F5.10 sezione c.a. | L'impronta del risultato esportato cambia dopo la visita delle schede | Da definire | Da decidere |
| F5.10 sezione c.a. | Blocco delle barre incoerente: `ManualBars` controlla che sia un elenco, `LayersLocked` solo che la chiave esista | Da definire | Da decidere |
| F5.10 sezione c.a. | La ricerca delle armature rifiuta la riserva torsionale 0 %; con i dati di una fixture scarta tutte le configurazioni | Da definire | Da decidere |

## Cosa sblocca la risposta

1. **Punto di controllo F5.4** (D3, D8–D12, bordo delle schede, più T1–T4 e T11). Sblocca il passo 6 della ricetta, nello stesso commit:
   - passaggio della Release della palificata (`ModuleImplementations.Declared`);
   - vista storica in `supporto/SUPERATI/ui-storica/palificata/` e rami `pileGroup` di `SheetEditor` tolti;
   - le 4 voci di contrasto della Chiara passano al passo del modulo (D12);
   - poi guida pratica e PDF, se la disposizione descritta cambia (passo 7).
2. **Le regole trasversali** (T1–T12). Rispondere una volta evita di riaprirle a ogni punto di controllo. T5 e T11 servono prima di scrivere le viste che le usano; T10 prima dell'unione di ogni ramo.
3. **Le viste XAML degli altri moduli**, per tracce e nell'ordine della ripianificazione. Per ognuno: vista, banco con confronto diretto a differenza zero, suite UI riscritte, punto di controllo del modulo, passaggio della Release.
   - Traccia A, **F5.5 materiali**: prima M9 e M11 (disposizione, Reologia).
   - Traccia B, **F5.6 palo verticale**: prima T5 e V12. Poi **F5.6b stratigrafia** (S7 prima della vista), **F5.9 muri** (MU3, MU4, MU10) e **F5.9b fondazioni** (FN5).
   - Traccia C, **F5.7 e F5.8 palo orizzontale ed elastico**: O2/E2 decidono se i due moduli passano insieme; E1 prima della vista elastica.
   - Traccia D, **F5.10 sezione c.a.**: prima C1, C3 e C10; poi i sottopassi S1–S9, ciascuno con il suo ViewModel e le sue tracce.
