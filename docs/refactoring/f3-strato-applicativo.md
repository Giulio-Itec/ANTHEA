# F3 — confini e riferimento di equivalenza

Decisioni operative del 13 ottobre 2026, sulla delega dell’utente del 12 ottobre. Base bce1e337, ANTHEA 1.5.0 e set V congelato. Il numero effettivo è 12 moduli: alle undici voci del piano iniziale si sono aggiunte le fondazioni superficiali. F5 esclusa; la separazione della logica non comporta un rifacimento della shell o dei ViewModel.

## Decisioni

- Migrazioni pigre: apertura del documento non riscrive tutti i fogli; la preparazione lavora sulla copia della scheda aperta o del calcolo/report. Le revisioni congelate non vengono migrate sul posto. `str_palo`, chiavi miste e nomi di archivio restano compatibili. I dati derivati oggi persistiti, come cmin_dur e scelte del report, restano fino a una modifica esplicitamente classificata.
- Scelta del modello elastico precedente: stato di decisione pendente, dati originali conservati; il servizio non sceglie automaticamente per l’utente.
- Calcoli e metodi normativi restano in GPC o, per i residui già censiti, dietro X.Calculations fino a F4. In F3 si spostano orchestrazione, documenti, migrazioni, cache e presentazione dei report; nessuna formula nuova nella UI.
- Application senza WPF governa documenti, servizi dei moduli e calcoli. Reports senza WPF governa composizione e scrittura dei documenti. Wiki senza WPF governa catalogo, ricerca e parsing. Nessuna dipendenza Reports → Application: il progetto passa ai report una proiezione dei dati. Il generatore delle fondazioni si separa dalla libreria usata dal calcolo per evitare un ciclo fra Reports e X.Calculations.
- La rimozione di X.Core dalla catena di produzione può lasciare temporaneamente il progetto di compatibilità per il test esterno Checker/GPCChecker.Test.BridgeAudit, che lo referenzia direttamente. Non si dichiara eliminato ciò che resta necessario; il seguito viene tracciato. Namespace storici possono essere conservati durante il trasferimento per preservare l’API.
- X.Materiali: dati e report fuori da WPF, vista accorpata al progetto desktop nella finestra di spostamento autorizzata. UI e logica restano separate; nessun secondo catalogo materiali.
- Figure tramite un contratto del servizio: il percorso desktop conserva le figure correnti; un processo senza renderer dichiara l’indisponibilità, senza inventare immagini o calcoli. PDF breve con Word resta un adattatore desktop finché l’alternativa non è collaudata.
- Scheduler comune con annullamento cooperativo e controllo della revisione: un risultato vecchio non può sostituire quello corrente. Ritardi dei moduli conservati inizialmente; Bridge Design può adottare il ricalcolo ritardato dopo confronto dei risultati.
- I tre temi e la disposizione dei controlli non cambiano in questa fase. Nessuna libreria GPC viene aggiornata durante gli spostamenti.

## Banco unico

Si estende ANTHEA.Testing anziché creare il duplicato TestSupport ipotizzato nel piano iniziale. Le nuove catture WPF stanno in tests/ANTHEA.Desktop.Equivalence e sono compilate soltanto nella configurazione UiTests già esistente. Release non include il toolkit né i comandi di prova. Le nuove suite passano da build/ci.ps1.

36 input sintetici congelati in tests/fixtures/f3/inputs. Catture D/E: 20261013-011723-f3-baseline-d e 20261013-011837-f3-baseline-e. 458 file confrontati, zero differenze non ammesse e un solo tempo volatile già censito. Coperti dati dopo costruzione, visita di schede/pannelli, calcolo, export e riapertura; report singoli, brevi e di progetto, 71 articoli renderizzati testualmente e 30 ricerche Wiki. Le acquisizioni non sostituiscono gli attesi ingegneristici indipendenti.

Il primo confronto B/C mostrava differenze soltanto nell’ordine dei conflitti del progetto sintetico: il banco generava nuovi ID ad ogni corsa. Gli ID della fixture sono ora fissi; nessuna esclusione numerica, di testo o di ordinamento aggiunta. La verifica B/C resta negli artefatti per trasparenza.

Soglie iniziali misurate sulla Release: 145 TypeRef verso ANTHEA.Calculations, 36 CompositeBridge, 3 Concrete, 6 Geotechnics; 11 metodi async void. Sei timer di calcolo (10 occorrenze complessive DispatcherTimer includendo gerarchia e Wiki). Nove creazioni DOCX in X.Core, più quella delle fondazioni. Le soglie si riducono con i trasferimenti e non aumentano senza motivazione tracciata.

I confronti preesistenti restano attivi: corpus headless, banco denso, mesh/concorrenza, regressioni e prove WPF. Un punto si chiude solo dopo la prova pertinente; nuove differenze vengono classificate prima di proseguire.

## F3.2 — scheletro e regole

Tre progetti net8.0 aggiunti alla soluzione, con un solo progetto di test di architettura e un ingresso console esplicitamente limitato all’aiuto finché i servizi non sono collegati. La suite controlla riferimenti di progetto e metadati degli assembly, target, WPF/Office/COM, inventario dei tipi di calcolo usati dalla UI, conteggi async void/timer e impronta del manifest GPC. Le suite funzionali Application/Reports/Wiki si aggiungono quando esistono servizi da provare, evitando progetti vuoti di test. Regole sul codice ingegneristico residuo e sui ripieghi normativi restano nel percorso F4; il loro trasferimento non viene mascherato come una semplice regola sulle dipendenze.

Corsa 20261013-012323-f3-scaffold: 7 PASS, compreso il riferimento F3. Controllo finale 20261013-012535-f3-architecture-final: 5 PASS; distribuzione Release senza marcatori di test. Le soglie di dipendenza non sono aumentate.

## F3.3b — documenti e revisioni

Spostati 17 sorgenti e il template Excel, con impronte prima del trasferimento negli artefatti f3/document-moves.json. DocumentService è il punto di apertura, validazione e salvataggio; Archivio delega per compatibilità. MainWindow usa il servizio; conservato il percorso di scrittura Office richiesto dalla protezione endpoint, distinto dalla rinomina atomica degli archivi. Il tool Headless valida e descrive documenti senza WPF, senza modificare i file.

121 controlli nuovi: 36 input storici aperti e risalvati senza migrazioni anticipate, revisione congelata indipendente dal foglio corrente, corruzione del pool rifiutata, scrittura invalida senza perdita del documento precedente, template Excel identico e segni conservati. Suite progetto passate. Corsa 20261013-013204-f3-documents-built: 12 PASS; 458 file equivalenti, unico tempo volatile già ammesso. La prima invocazione selezionava gli stadi senza build e non trovava il nuovo eseguibile di test; conservata come errore di invocazione, risolto eseguendo anche build. Nessuna eccezione aggiunta.

## F3.4 — report e scrittore condiviso

Tutti i generatori Core e il report delle fondazioni sono in Reports. Dieci punti di creazione ZIP e la riscrittura del report breve passano dal solo DocxWriter. Ogni generatore può fornire un DocxDocument in memoria; i metodi che restituiscono byte restano compatibili e delegano. Il compositore progetto riceve una proiezione Application di dati comuni, conflitti e input, senza dipendere dai servizi del documento. I capitoli condividono direttamente le parti XML e le immagini: nessun DOCX temporaneo viene compresso e riaperto nel percorso del prodotto. Il vecchio ingresso a byte resta un adattatore per i consumatori precedenti, non il percorso UI. DocumentFile contiene l’unica politica di scrittura durevole, riusata da archivi e report.

Report delle fondazioni e PDFsharp si spostano insieme per evitare il ciclo con la libreria di calcolo. Core non è più referenziato da Desktop né dal manifest delle dipendenze distribuite. Rimane il progetto di compatibilità composto da TypeForwardedTo per il test esterno Checker/BridgeAudit e per gli strumenti precedenti; non contiene logica. Nessun motore legacy viene eliminato.

Verifiche: 13 PASS nella corsa 20261013-013734-f3-reports, compresi 458 file del riferimento F3. Ulteriori controlli su proprietà dei buffer, caratteri, rimappatura delle immagini, livelli dei titoli, sorgenti non mutati, annullamento e rifiuto di collegamenti esterni. Corsa 20261013-014033-f3-report-parts-final: 5 PASS. Comparazione profonda nei report-parts-comparison: 30 DOCX, 134 parti XML e 97 immagini, zero differenze; riuso di TextNormalizer per i soli volatili già censiti. Nessuna variazione ingegneristica o del formato degli archivi.

## F3.5a — servizi e coordinamento

IModuleService usa le identità dell’unico ModuleCatalog; chiama i motori esistenti su copie, conservando gli esiti tipizzati dove servono alla presentazione. ModuleCalculation non equipara calcolo eseguito a verifica strutturale soddisfatta. Cache LRU per documento: massimo quattro esiti e 8 MiB di JSON serializzato; esiti tipizzati mutabili ed errori esclusi, restituzione sempre su copia. Non è una stima della memoria totale del processo.

RecalculationScheduler non dipende da WPF: conserva il contesto del chiamante, serializza anche motori che non interrompono subito, annulla richieste superate e consente la pubblicazione solo della revisione corrente. Prove con barriere esplicite, ritardo annullato, errore osservato, invalidazione, annullamento esterno e chiusura. I timer dei moduli vengono sostituiti uno alla volta nel passo seguente. Il report progetto autonomo e IFigureRenderer restano da completare.

Corsa 20261013-014723-f3-module-services-final: 7 PASS e 185 controlli Application. Dodici default confrontati con l’ingresso di calcolo preesistente; le righe CA nuove vengono preparate una volta per dare alle due esecuzioni le stesse identità, senza normalizzare i risultati numerici. Headless calculate esporta JSON da un foglio singolo e rifiuta di usare il documento originale come destinazione. Collaudo con palificata orizzontale negli artefatti F3.

## F3.6a — pali verticali

SheetEditor dei pali e micropali verticali usa IModuleService, la sessione con cache e RecalculationScheduler. Ritardo iniziale invariato di 450 ms; il calcolo esplicito sostituisce quello in attesa, le modifiche invalidano i lavori precedenti e i motori che non si interrompono subito restano serializzati. Lo stato visuale viene aggiornato soltanto dal risultato corrente.

Corsa 20261013-015056-f3-vertical-service: 10 PASS, smoke WPF generale e confronto di 458 file senza differenze nuove. Nove prove WPF dedicate con un motore di test trattenuto da barriere, due geometrie consecutive e chiusura durante il lavoro. Il test dei timer esistente ora attende la Task della richiesta. Soglie correnti in limits.json: 10 async void e 9 occorrenze DispatcherTimer, cioè 5 timer di calcolo più quelli di gerarchia e Wiki; il riferimento originale rimane immutato.

## F3.6b — acciaio, palificata e fondazioni

Proprietà e curva dell’acciaio e proprietà derivate del report usano il servizio del modulo. La palificata conserva il calcolo breve sincrono, le modifiche alla direzione selezionata e il ritardo di 180 ms, ora coordinato dallo scheduler senza DispatcherTimer. La fondazione passa al servizio mantenendo la stessa FoundationLibrary iniettata, anche quando l’API non è disponibile.

Corsa 20261013-015601-f3-steel-group-foundations: 12 PASS; 185 controlli Application, controlli fondazioni anche WPF, smoke acciaio e report materiali, 458 file F3 equivalenti. Soglia DispatcherTimer ridotta a 8 occorrenze, quattro timer di calcolo residui. Nessuna formula cambiata.

## F3.5a.2 — un solo ingresso di calcolo

CalculationService è trasferito in Application, con la stessa firma e lo stesso namespace per i consumatori ricompilati. Il tipo cambia assembly: non si promette compatibilità binaria per eventuali eseguibili esterni non ricompilati. Moduli e facciata JSON condividono ora un solo dispatch, inclusi i risultati tipizzati e la FoundationLibrary iniettata; nessuna formula cambiata. I test dei motori restano riferiti ai calcoli, quelli dell’ingresso applicativo referenziano Application. TypeRef Desktop verso Calculations ridotti da 145 a 144.

Corsa 020239-f3-unified-dispatch-built: 14 PASS, 458 file F3 equivalenti. Muri e BridgeDesign superati nella corsa headless. La prima build richiedeva il riferimento esplicito Model ora aggiunto; nessuna eccezione. Il primo confronto numerico puntava erroneamente alla vecchia B3, anteriore alle chiusure F2: conservato ma non usato per attestare equivalenza. Sul riferimento finale F2 risultano solo 633 rinomine dei chiamanti diagnostici dei report, tutte classificate F3-01; le otto identità esatte sono normalizzate dal banco, con TSV originali conservati e 18 prove, senza sostituzioni generiche né tolleranze nuove. Il runner accetta CaptureCandidate per riutilizzare una cattura esistente. Lo strumento copia tutte le DLL del manifest, anche i convertitori non caricati: elimina un falso indicatore di incoerenza dovuto alle due DLL non necessarie al corpus ma assenti accanto all’eseguibile.

Corsa finale 20261013-020922-f3-dispatch-final: 5 PASS; 548 file equivalenti, soli 21 tempi volatili già censiti. Le librerie copiate coincidono tutte con il manifest. Attesi indipendenti e fixture F3 immutati.

## F3.6c — Wiki indipendente da WPF

WikiCatalog, ricerca, alias e WikiProgress trasferiti in ANTHEA.Wiki. WikiMarkup restituisce blocchi e span tipizzati, con gli stessi delimitatori e le stesse regole di collegamento del lettore precedente. WikiEditorial interpreta solo questi blocchi con i controlli WPF; LaTeX originale conservato per copia e formule. Le figure grafiche rimangono nel progetto desktop che le disegna. Cataloghi, esempio e hash dei sorgenti appartengono ora a src/ANTHEA.Wiki/Content; i percorsi degli strumenti correnti sono aggiornati, quelli degli archivi storici restano originali. Gli undici file JSON spostati hanno la stessa impronta prima e dopo, registrata in f3/wiki-moves.json; cambiano soltanto i percorsi di sources.props. Nessuna modifica alle guide e ai loro PDF.

Corsa 20261013-021305-f3-wiki-extracted: 240 controlli portabili, indice e controlli editoriali, rendering completo Wiki e 458 file F3 equivalenti, tutti superati. La prima build del test fondazioni aveva ancora una seconda chiamata reflection al catalogo precedente: corretta usando la API pubblica, senza modifiche al prodotto. Corsa finale 20261013-021454-f3-wiki-foundation-final: build e prove WPF fondazioni passate. Nessuna tolleranza, atteso o testo applicativo cambiato.

## F3.6d — ponte composto e muri

Il calcolo principale usa il servizio del modulo. RecalculationScheduler sostituisce i timer da 500 e 450 ms, conservandone i ritardi; non si sovrappongono motori che ignorano temporaneamente l’annullamento. Curve del ponte su scheduler dedicato; stabilità globale e predimensionamento delle armature del muro condividono lo scheduler locale, con richieste opzionali in StructuralQueries. Input e richieste sono copiati prima dei calcoli. Le proposte non vengono applicate se cambia la revisione. Ripristino della fase selezionata nel ponte con callback di selezione sospese, come nel percorso precedente.

Corsa 20261013-021755-f3-bridge-wall-built: 11 PASS, smoke ponte e curve, muri, guida globale e muro avanzato, più 458 file F3 equivalenti. La prima build aveva rilevato riferimenti al timer/annullamento nella ricerca armature, poi migrata sullo stesso coordinatore. Corsa 20261013-022126-f3-typed-scheduling: 6 PASS; 21 prove WPF con barriere e motori deliberatamente non cooperativi su pali, ponte e muri, modifiche reali dei campi geometrici, sostituzione del lavoro in attesa e chiusura. Helper del motore di prova condiviso fra i tre moduli. Soglie: 8 async void, 6 occorrenze DispatcherTimer, di cui solo due per calcoli (orizzontale ed elastico). Risultati e report invariati.

## F3.6e — capacità orizzontale e risposta elastica

Gli ultimi due timer di calcolo sono sostituiti dallo scheduler comune, con ritardi 450/300 ms conservati. HorizontalPileAnalysis mantiene il momento della sezione disponibile anche con terreno incompleto. ElasticPileAnalysisSession possiede risposta FEM e cache delle resistenze già esistente: pubblica prima i diagrammi, poi le verifiche; variazioni delle armature riusano il FEM, variazioni dei carichi lo invalidano. Copie indipendenti in ingresso, callback ed esito; migrazione legacy ancora esplicita. Una chiusura annulla le richieste e ripulisce la cache anche quando il motore sta terminando.

Corsa 20261013-022732-f3-horizontal-elastic-final: 458 file F3 equivalenti, prove elastiche complete, secondo anello e nota stress-block passate. Due problemi del banco risolti senza cambiare attesi: la prova del terreno incompleto ora usa uno strato di spessore nullo invece di un archivio strutturalmente invalido; il ripristino diretto del JSON nel test di editing notifica la revisione, perché il calcolo usa una fotografia acquisita alla richiesta. Le prime build evidenziavano riferimenti di test al timer rimosso, sostituiti con attesa della Task. Corsa finale 20261013-022945-f3-piles-regression-final: 4 PASS, 199 controlli Application, smoke orizzontale passato. Soglie Desktop: 143 TypeRef Calculations, 6 async void e 4 DispatcherTimer, tutti per gerarchia/Wiki; nessun timer di calcolo residuo.

## F3.6f.1 — materiali e capitoli portabili

MaterialSheetInput e MaterialSheetPresentation contengono i dati iniziali condivisi, validazione, proprietà meccaniche, aderenza, copriferro e testi della composizione prima costruiti nei controlli WPF. I motori restano quelli esistenti. La mappatura JSON → CoverInput è estratta dal MaterialCover già usato da sezioni e muri: UI e ingresso precedente riusano lo stesso metodo, senza un secondo elenco di coefficienti. Le scelte e i valori iniziali dei controlli provengono dall’unico catalogo applicativo. MaterialReportChapter produce i capitoli calcestruzzo/acciaio senza una vista, usato anche da SheetEditor.

Corsa 20261013-023321-f3-material-presentation-built: 9 PASS, inclusi materiali, report e 458 file F3 equivalenti. Il collegamento dei capitoli passa nelle stesse prove della corsa 023520. Il nuovo confronto portabile inizialmente confrontava stringhe numeriche formattate (38,00) con celle numeriche del report (38): ora confronta i valori delle celle per etichetta, numericamente quando entrambe sono numeri, testualmente altrimenti; nessuna modifica alle fixture. Corsa finale 20261013-023645-f3-material-service-final: 4 PASS, 291 controlli Application e suite rapida ConcreteLibraryAdapter passata. Input incompleti, validità della tolleranza, errore locale senza perdita dei valori indipendenti e isolamento dell’archivio coperti. La prima compilazione aveva un riferimento locale Flag rimosso durante l’estrazione, corretto prima dei test. Nessun nuovo fallimento ammesso.

## F3.6f.2 — vista Materiali nel Desktop

La vista, gli stili di ripiego e le risorse grafiche sono in X.Desktop/Wpf/Materials e Assets/Materials. Namespace e API della vista conservati per i sorgenti ricompilati; l’assembly Materiali separato non è più una dipendenza del prodotto. Il catalogo delle esposizioni presenta alla UI solo codice, descrizione e severità già calcolata dal servizio: nessuna nuova dipendenza diretta dai tipi dei motori. Le impronte dei file trasferiti sono in f3/material-view-moves.json. Rimosso l’adattatore finestre Materiali inutilizzato e duplicato; rimane quello del Desktop. README e RIFERIMENTI della vecchia applicazione, già superati, archiviati con registro e rimando alle guide Rev39. Nessun contenuto della guida corrente cambia.

Corsa 20261013-023905-f3-material-view-move: architettura, 291 controlli Application, materiali, report e 458 file equivalenti passati. Il test dei report brevi disabilita i riferimenti transitivi e richiedeva quindi quelli espliciti ad Application/Reports al posto di Core e Materiali. Corsa finale 20261013-024042-f3-material-move-short-final: build e prova WPF passate. I conteggi delle dipendenze e dei timer restano invariati. Il progetto della vista viene eliminato, nessun motore di calcolo legacy viene eliminato.

## F3.6g — Bridge Design

BridgeDesignAnalysis prepara il ponte, il confronto fissato e le proposte sulla stessa fotografia dei dati, usando i servizi esistenti. La vista invalida subito gli esiti e ricalcola dopo 450 ms. La proposta con una campata in meno viene calcolata una sola volta; risultati, soglia di risparmio e filtri restano identici. L’ottimizzazione possiede la sua richiesta fin dall’attesa del calcolo iniziale: avvii doppi ignorati, annullamento e chiusura impediscono pubblicazioni tardive. SheetEditor attende la Task e dispone anche questo workspace.

Corsa 20261013-024321-f3-bridge-design: 9 PASS, inclusi 458 file equivalenti. Corsa finale 024915-f3-bridge-design-final: smoke completo e 31 controlli WPF di scheduling/chiusura passati. Prove portabili aggiunte per alternative applicabili, delta coerenti, confronto invalido circoscritto, proprietà delle copie e annullamento. In 025100 il test mancava del namespace Anthea.Calculations, corretto; 025112-f3-bridge-design-application-final: build e suite Application passate. Nessun atteso modificato, nessuna eccezione aggiunta.

## F3.6h.1 — sessione delle sezioni in calcestruzzo

ConcreteAnalysis e ConcreteAnalysisSession sono in Application, namespace e firme precedenti conservati per i consumatori ricompilati. La lettura comune N–Mx–My resta in SectionWorkspace e viene riusata dal progetto delle armature, evitando una dipendenza circolare. Hash prima dello spostamento in f3/concrete-moves.json. CalculateBatchAsync governa i domini e le famiglie SLE richiesti, su copie, con pubblicazioni progressive e cache della sezione preparata; le richieste alla stessa sessione sono serializzate. I callback conservano il contesto del chiamante e sono serializzati anche senza contesto UI. Gli errori locali non impediscono le altre famiglie. Il workspace usa lo scheduler comune per annullamento e revisione.

Corsa 025316-f3-concrete-batch-built: 13 PASS, adattatori CLS, riuso API, funzioni CA e 458 file equivalenti. La prima build segnalava un test che accedeva alla vecchia cache privata: la verifica ora confronta direttamente l’identità della sezione condivisa dai quattro domini. Corsa finale 20261013-025551-f3-concrete-session-final: 11 PASS, 311 controlli Application, 35 WPF scheduling/chiusura, smoke generale e CA, 458 file equivalenti. Nuove prove su riuso dei domini e degli stati SLE, filtri grafici, input modificati dopo la richiesta, errore parziale, annullamento e recupero della sessione. Soglia TypeRef Calculations ridotta da 143 a 140; nessuna tolleranza nuova.

## F3.6h.2 — dettagli e riepiloghi delle sezioni

ConcreteDetailingPresentation costruisce testi, esiti degli ancoraggi e raggruppamenti dei dettagli chiamando gli stessi adattatori. I valori derivati si applicano esplicitamente al workspace; il calcolo non modifica il documento di ingresso. La distinzione fra controlli numerici e conferme manuali resta intatta, così come l’indipendenza dell’ancoraggio da un errore nei dettagli. VerificationDetail è l’unico formato testuale dei riquadri; WPF ne interpreta stato e contenuto. ConcreteVerificationSummaries riusa l’aggregatore già esistente e mantiene ordine, conteggi e criteri dei riepiloghi.

Corsa 030001-f3-concrete-presentation-built: 9 PASS, smoke CA e 458 file equivalenti. Il primo build ha individuato il vecchio nome Children nel conteggio dei riquadri, ora Items. Corsa finale 20261013-030206-f3-concrete-presentation-tests: 330 controlli Application; prove dirette sui quattro output CA congelati, nessuna riscrittura degli attesi, prova della mancata mutazione, applicazione idempotente dei derivati, dettagli validi e errore parziale. Le figure e i testi dei report mantengono il riferimento F3.

## F3.6h.3 — analisi opzionali delle sezioni

StructuralQueries governa gli ingressi di curve M–χ, resistenze nelle quattro direzioni e ricerca delle armature, su copie dei dati e tramite gli stessi motori. Le resistenze rapide ora annullano e serializzano le richieste superate, invece di eseguire ogni modifica in parallelo e scartare solo la visualizzazione. Le curve usano lo scheduler e restituiscono una Task osservata dai test; nessun async void di calcolo. La ricerca armature mantiene l’esito parziale previsto quando il motore termina dopo l’interruzione; i progressi controllano l’identità della richiesta e il risultato viene assegnato solo se la vista è ancora aperta. Le risorse della richiesta vengono liberate al termine.

Corsa 20261013-030344-f3-concrete-queries: 12 PASS, 330 controlli Application, smoke CA, ricerca armature e 458 file equivalenti. Corsa finale 030616-f3-concrete-queries-close: 5 PASS e 37 controlli WPF, compresa chiusura con calcolo principale, curva e resistenze rapide in corso; nessun testo o risultato tardivo nella vista chiusa. Soglie: 133 TypeRef Calculations, 5 async void, 4 DispatcherTimer non di calcolo. Nessuna modifica numerica o agli attesi.

## F3.5b.1 — orchestrazione del report progetto

ProjectReportService possiede cattura del ramo con la sua ascendenza, ordine dei capitoli, segnalazione degli errori e composizione finale. WriteSnapshotAsync riceve esplicitamente una fotografia posseduta dal report e vi conserva i dati preparati per diagnosi; l’archivio aperto non viene passato al costruttore dei capitoli. IProjectReportChapterBuilder è il contratto per i moduli. Nel Desktop resta temporaneamente SheetEditorReportChapterBuilder: la sua sostituzione con preparazioni e renderer autonomi è ancora aperta, non mascherata dal trasferimento del ciclo.

Corsa 20261013-030829-f3-project-report-service: 8 PASS, 337 controlli Application, smoke gerarchico del report e 458 file equivalenti. Prove portabili su ascendenza, ordine, capitoli incompleti visibili, archivi non mutati, dati preparati nella fotografia, annullamento a metà e errori I/O che conservano il file precedente. Nessun formato o testo del report modificato.

## F3.5b.2a — riferimento grafico

Prima di spostare la preparazione delle figure sono congelati sei input di pali/micropali, tutte le serie numeriche, i filtri di capacità, le stratigrafie, i marcatori, colori, tratteggi e opzioni degli assi. Confronto esatto senza tolleranze nuove in ui/f3-figures; acquisizione esplicita distinta dalla verifica. Corsa 031523-f3-figures-baseline-repeat passata; i 458 file F3 restano equivalenti (031405). Due invocazioni iniziali del solo stadio build non selezionavano alcuna suite: non usate come prova. Nessun atteso ingegneristico sostituito.

## F3.5b.2b — curve portabili

VerticalPileCharts prepara le stesse serie di capacità, visibilità, filtri, abachi e marcatori; PlotPresentation traduce soltanto colori e stili WPF. I valori Nq e Bustamante–Doix provengono dagli stessi motori. Nessuna formula o atteso aggiornato. Corsa 031655-f3-vertical-charts-final: 6 PASS, sei fixture grafiche esatte, 458 file F3 equivalenti e regole architetturali superate. Primo errore di compilazione dovuto al nome di un tipo trasferito, corretto prima dei confronti.

## F3.5b.2c — primi capitoli autonomi

ModuleReportService prepara e calcola pali, micropali e Bridge Design senza creare controlli; IFigureRenderer riceve il risultato già calcolato. ModuleSheetPreparation riusa le migrazioni pigre della vista. Il renderer costruisce solo disegni e la vista usa il medesimo generatore delle figure verticali, preservando selezione e visibilità correnti. Altri moduli restano sull’adattatore temporaneo, senza dichiarare concluso F3.5b.2.

Corsa 032000: 5 PASS, report progetto, sei catture grafiche, 458 file equivalenti. Nuova suite ui/f3-report-chapters: otto fixture, dati preparati, XML e dieci immagini identici alla vista; metadati normalizzati esclusivamente dal normalizzatore comune (032128). Confronto aggiuntivo con cattura prima dello spostamento: 30 DOCX, 97 immagini identiche byte per byte. Nessuna variazione ingegneristica; correzione iniziale del namespace del preparatore prima delle prove.

## F3.5b.2d — materiali, palificata e fondazioni

Sette moduli su dodici producono ora capitoli senza editor. I materiali condividono lo stato iniziale portabile; RestoreState conserva il comportamento delle restaurazioni parziali. Palificata: migrazione, coordinate numeriche e selezione risultante conservate, parser comune con la vista. Fondazioni: stesso completamento dei dati e stesso report portabile già esistenti.

Corsa 032514: 7 PASS, 343 controlli Application, smoke materiali/progetto, 16 capitoli autonomi confrontati con la vista e 458 file equivalenti. Un confronto intermedio ha rilevato soltanto l’ordine di cinque proprietà materiali nel capitolo progetto: ripristinato l’ordine storico di inserimento; attesi immutati, nessuna tolleranza aggiunta. Correzione sintattica iniziale del ramo senza figure prima del collaudo.

## F3.5b.2e — muri autonomi

Ottavo modulo separato: dati globali, quote degli strati persistite e matrici risultanti condivisi in WallSheetPresentation; scelta della combinazione e preparazione distinta in WallReportPresentation. Il renderer disegna soltanto sezioni, diagrammi, superficie critica e tavole della distinta, senza dipendenze dagli editor. Il report interattivo conserva la combinazione scelta; il progetto adotta la stessa selezione iniziale precedente.

Corsa 032911: 5 PASS, smoke muri/progetto, 20 capitoli e 458 file equivalenti. Corsa 033130: 3 PASS, smoke stabilità globale ed equivalenza di 21 capitoli (55 immagini e tutte le parti XML), compreso un caso aggiuntivo con profilo globale confermato. Nessuna differenza o eccezione aggiunta.

## F3.5b.2f — ponte composto autonomo

BridgeSheetPresentation condivide con la vista i valori iniziali, migrazione delle opzioni, fase selezionata, omogeneizzazione e amplificazione delle tensioni. I metodi ingegneristici chiamati restano quelli già esistenti. ModuleFigureRenderer disegna geometria e fasi dai risultati senza costruire BridgeWorkspace; la relazione interattiva riusa lo stesso renderer.

Corsa 20261013-033428-f3-report-composite-bridge: 8 PASS, smoke ponte, curve, predalle, Bridge Design e progetto; capitoli autonomi confrontati integralmente con quelli della vista e 458 file F3 equivalenti. Nessuna modifica agli attesi o nuova eccezione.

## F3.5b.2g — pali orizzontali ed elastici

HorizontalSheetPresentation riusa preparazione generale, coefficienti, materiali e vista elastica nei due percorsi. La relazione segue vista_orizzontale; l’elastico usa la sessione applicativa con annullamento, la capacità il servizio del modulo. Nessuna risoluzione implicita della migrazione legacy. Il disegno di capacità è condiviso con la relazione interattiva; le figure elastiche erano già portabili.

Corsa 20261013-034045-f3-report-horizontal: 5 PASS, smoke orizzontale/progetto, 33 capitoli autonomi (anche input incompleti e migrazioni pendenti), equivalenza completa di 458 file. Nessuna formula, atteso o tolleranza modificati. Undici moduli autonomi su dodici; rimane la sezione CA.

## F3.5b.2h.1 — preparazione della sezione CA

ConcreteSheetPreparation condivide valori iniziali, strati di armatura, materiali, lettura e serializzazione delle azioni e sincronizzazione φ/n. Quest’ultima conserva il campo sorgente invalido, azzera solo il derivato e restituisce alla vista il messaggio esistente. La preparazione autonoma conserva i dati derivati persistiti e le migrazioni già adottate dalla finestra.

Corsa 034430: 458 file equivalenti e ricerca armature passati; il nuovo confronto dei dati aveva ID diversi, generati dalle due copie di un input legacy senza identificativi. Banco corretto creando le identità una volta prima delle copie, come nella suite dei servizi. Corsa finale 20261013-034639-f3-concrete-preparation-identities: 2 PASS; quattro preparazioni CA uguali alla vista, 33 capitoli precedenti ancora equivalenti. Nessun atteso o tolleranza modificati.

## F3.5b.2h.2 — risultato CA e riferimento grafico

ConcreteReportAnalysis riusa il batch domini/SLE e prepara l’esportazione completa con riepiloghi, dettagli e ancoraggi. ConcreteResultExport è condiviso con la vista; il contratto più piccolo della CLI calculate rimane distinto. ConcreteShearBatch elimina il doppio ciclo di calcolo per riga e conserva errori locali ed esiti parziali.

Corsa 034827: 3 PASS, quattro risultati CA identici alla finestra e 458 file equivalenti. Corsa 035103: 4 PASS, risultato con ciclo taglio condiviso, 33 capitoli, 458 file e cattura di quattro configurazioni grafiche CA. Riferimenti in tests/fixtures/f3-concrete-figures: SLU/SLV, quattro filtri, punti resistenti e colori. Ripetibilità esatta in 035255 prima di estrarre i grafici; nessun atteso precedente modificato. L’acquisizione è esplicita con CaptureConcreteFigures e non scrive nelle fixture.

## F3.5b.2h.3 — grafici dei domini CA

ConcreteDomainChart prepara contorni, proiezioni, punti Ed/Rd e linee di verifica usando i domini già calcolati. I filtri, la validazione delle azioni visualizzate e i colori sono condivisi; WPF converte soltanto dati e colori nel Plot. Conservati scala, selezione, livelli e comportamento con azioni invalide.

Corsa 035446: 5 PASS, quattro riferimenti grafici esatti, 33 capitoli e quattro risultati CA equivalenti alla vista, smoke CA e 458 file invariati. La prima build chiedeva il riferimento Concrete del tipo nativo trasferito: aggiunto ad Application, senza nuove dipendenze della UI. Nessuna nuova tolleranza o modifica alle fixture.

## F3.5b.2 — tutti i capitoli autonomi

Anche CA usa il servizio applicativo e il renderer comune. La geometria viene preparata nel servizio; filtri, ordinamento delle azioni e figure sono condivisi con la finestra. Il percorso interattivo conserva selezione, grafici correnti e curva opzionale. Il report di progetto non costruisce più SheetEditor né workspace. Le regole di architettura impediscono di reintrodurre editor o costruzione di motori nei renderer. Errori parziali conservati nel capitolo.

Corsa finale 20261013-040439-f3-all-chapters-final: 8 PASS; 37 capitoli dei dodici moduli e 85 immagini, ogni parte XML identica alla vista; quattro riferimenti grafici CA e 458 file F3 equivalenti. Confronto aggiuntivo con la cattura precedente all’estrazione dei grafici: 30 DOCX, 97 immagini identiche byte per byte (all-chapters-images-final.json). Nessuna differenza ingegneristica, nuova tolleranza o modifica alle fixture. Una prima invocazione passava erroneamente l’elenco degli stadi come singola stringa ed è stata rifiutata prima dell’esecuzione.
