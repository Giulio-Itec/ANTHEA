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
