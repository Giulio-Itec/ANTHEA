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
