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
