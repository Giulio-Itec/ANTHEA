# Azioni aggiuntive — 1 novembre 2026

## Mandato e integrazione

L'utente approva i sei fogli proposti e l'ordine: idraulica, fasi costruttive, effetti differiti e deformazioni imposte, precompressione e perdite, gru/carriponte, incendio. Traffico interamente in standby, seguito da un collega. Merge dei nostri branch sui rami principali al termine di tutti i lavori, dopo i collaudi; mantenere l'allineamento con i rami pubblicati. Le regole Git delle librerie autorizzano anche il push dopo integrazione di origin/master e suite verdi. Il ramo principale di ANTHEA si chiama main.

Checkout isolati in `../azioni-estese-20261101/{ANTHEA,Model,Checker,Utilities,Geometry}` rispetto al repository padre. Branch `codex/azioni-estese-20261101` nei tre repository modificati; Utilities e Geometry invariati, in checkout separati. Basi pubblicate: ANTHEA 1dfe7110, Model f4f78752, Checker eb16e72c. Nessuna modifica ai checkout degli altri lavori.

## Architettura e contratto

Contratti, unità e dati in GPC.Model; formule e integrazioni in GPC.Design, riuso dei motori esistenti. ANTHEA mantiene documenti versionati, adattatori senza WPF, ViewModel e viste F5. Materiali, pressione, forza, momento, deformazione e temperatura restano grandezze distinte; i valori delle azioni non vengono maggiorati due volte. Ogni foglio conserva i casi alternativi e la provenienza dei parametri. Schemi dinamici quotati e grafici, CSV, relazione, esempi e guide globali accompagnano ogni modulo.

## Piano e verifiche

- [x] Idraulica: parete, platea e pila in corrente; scenari alternativi, pressioni nette, risultanti e primi momenti; profili assegnati di sottospinta e velocità. Prove indipendenti su triangolo/trapezio, Archimede, traslazione del riferimento, corrente uniforme/variabile/inversa, coppia pura e input non validi.
- [x] Fasi costruttive: carichi e sequenza, senza attribuire al foglio una redistribuzione strutturale non calcolata. Riusare i contratti delle fasi GPC.
- [x] Ritiro, viscosità, maturazione e spostamenti imposti: storia degli eventi e separazione fra deformazioni libere e risposta vincolata.
- [x] Precompressione: geometria del cavo, tiro, attrito, ancoraggi, perdite immediate/differite e stato efficace inviabile alla sezione.
- [x] Gru/carriponte: azioni alle ruote e gruppi di carico compatibili, parametri e dati del costruttore tracciati.
- [x] Incendio: azioni termiche nominali, facce esposte e condizioni termiche; la temperatura dei gas non è la temperatura della sezione.
- [x] Guide globali, Word/PDF, esempi e collaudi UI nei tre temi; nessuna suite UI eliminata.
- [ ] Allineamento finale, versioni GPC, tutte le suite, merge e set ufficiale; full ANTHEA sul set coerente.

## Fonti controllate

Idraulica: NTC 2018, capitolo 5, PDF già acquisito nella precedente attività Azioni (GU 20/02/2018, pagine 151 e 155: §§5.1.2.3 e 5.1.3.8). Le NTC richiedono lo studio di compatibilità idraulica; non viene attribuita alla norma una tabella universale di Cd. La resistenza stazionaria usa Cd assegnato con fonte obbligatoria e larghezza proiettata; idrostatica e integrazione delle pressioni sono identificate come modello fisico. Profili di sottospinta assegnati richiedono una fonte; non sostituiscono il calcolo di filtrazione.

Il catalogo UNI è stato consultato per EN 1991-1-6:2026, EN 1991-3:2026 e EN 1991-1-2:2024. La scheda di catalogo non vale come verifica del testo integrale; le implementazioni normative saranno identificate soltanto dopo riscontro delle clausole effettivamente utilizzate. Neve A1:2015 resta nella precedente voce aperta, senza richieste pendenti all'utente.


## Punto di controllo idraulica

Contratti Model 46c05aff e completamento del vocabolario 23b5ce1e; calcolo e test GPC.Design 083003f6. Il primo set candidato si è fermato per Gmsh assente nel checkout di test: copiati i soli binari di prova nel percorso fratello previsto da UnitTest, senza inserirli nel prodotto. Il secondo ha identificato i due nuovi enum non ancora registrati: completato il vocabolario di produzione, senza modificare gli attesi. Il terzo set, `supporto/artefatti/azioni-estese/candidate-idraulica-03`, ha superato 11734 test delle librerie, con 13 prove già ignorate dalle suite esistenti e nessun fallimento. Include la compatibilità delle API pubbliche.

ANTHEA usa il set candidato tramite GpcLibDir assoluto nella cartella ignorata candidate-libs-idraulica: lib/Checker resta invariata. PASS 556 controlli MVVM/report/persistenza/CSV (compresi i moduli precedenti), PASS 378 controlli dei 76 esempi e PASS 264 controlli WPF nei tre temi. Catture e report in `supporto/artefatti/ci/20261101-203553-azioni-idraulica-checkpoint`; Architecture.Tests e WPF sono PASS anche nella corsa azioni-idraulica-architecture-2 dopo la revisione delle costanti grafiche, senza aumentare le baseline. Inventario UI: 73 suite, nessuna rimossa.

La selezione del caso è ora unica sopra le schede di tabella e diagramma. La prova controlla il nome effettivamente disegnato, oltre all'indice: evita la selezione apparente con elemento nullo rilevata usando due selettori nei contenuti delle tab. Le trasformazioni geometriche e le frecce rimangono codice di disegno; pressioni, risultanti e momenti provengono esclusivamente da GPC.Design. Gli esempi sono nella cartella act_idrauliche della raccolta distribuita e nel catalogo.

Fonti successive acquisite, non ancora implementazioni: EN 1991-1-6:2005 (31 pagine; Tab. 4.2 e §4.11.2 riscontrati anche sul rendering), EN 1992-1-1:2004 con correzioni incluse nel PDF BSI (227 pagine), EN 1991-3:2006 (49 pagine), EN 1991-1-2:2002 (61 pagine), NTC 2018 Gazzetta Ufficiale integrale (372 pagine). Copie e testo estratto soltanto negli artefatti/fonti; non distribuiti. Il download iniziale EC2 dal catalogo etiope restituiva HTML: sostituito con il PDF effettivo prima di qualunque uso.

Allineamento del 1/11: ANTHEA origin/main ancora 1dfe7110, Model origin/master ancora f4f78752; Checker origin/master avanzato a 8ad4507a (acciaio e preparazione inviluppi). Integrato nel nostro branch con il merge 596b7c60 prima del secondo foglio. Guide Word/PDF e Wiki saranno aggiornate in una sola edizione coerente dopo i sei fogli; nessuna versione ufficiale dichiarata completata.


## Punto di controllo fasi costruttive

Model 679bdf06 e Checker 7691c0d2: geometria delle aree caricate, forze concentrate e orizzontali, momenti con assi dichiarati, intervalli di attivazione e StageDefinition GPC. Preset EN 1991-1-6:2005 distinti per personale, deposito sui ponti e getto; controllo del doppio conteggio del personale. Il peso del materiale e i carichi di lavoro sono separati; nel getto si aggiunge soltanto l’incremento sull’area 3×3 m, ridotta per impronte inferiori. Nessuna redistribuzione strutturale o verifica dei puntelli attribuita al foglio.

Set candidato candidate-fasi-01: tutte le suite Model/Checker PASS, compresa compatibilità API. In ANTHEA la corsa 20261101-205409-azioni-fasi-02 ha chiuso Architecture.Tests, Climate.Tests (649 controlli), Examples.Tests (79 esempi) e ui/check-climate-view (318 controlli). La successiva azioni-fasi-row-editor aggiunge e supera due controlli sul cambio del modello di carico e sulla ricostruzione dei campi condizionali. Schermate della sequenza e della pianta e schema originale controllati visivamente. Suite UI sempre 73, nessuna eliminata. Tre esempi aggiunti in act_fasi_costruttive. Guide globali ancora in lavorazione insieme agli altri fogli.


## Punto di controllo effetti differiti

Model 8f98d8b9 e Checker d74f1c6a: ritiro NTC tabellare o EN Appendice B, maturazione e viscosità EN, storia degli incrementi di carico e spostamenti assiali imposti. Riutilizzato ConcreteTimeEffects; non modificata l’API storica del ritiro, già annotata nel sorgente per difetti di kh e del segno di fcm. Nuovo modello limitato esplicitamente a calcestruzzo ordinario a 20 °C, RH costante e h0≥100 mm; NTC fck 20–80 MPa, EN 12–90 MPa. Evoluzione autogena EN dichiarata anche nel ramo NTC. Formule e tabelle controllate sul testo e sul rendering delle pagine pertinenti.

Risposta vincolata di una barra assiale integra: molla o distanza imposta; superposizione degli incrementi con J(t,ti), senza modulo efficace unico. Riferimento inizialmente privo di tensioni all’età assegnata. Equilibrio e compatibilità controllati; doppia griglia temporale e indicatore di convergenza. Il superamento di fctm o 0,45 fck(t) viene segnalato come previsione lineare fuori campo, senza attribuire al foglio un’analisi di fessurazione o di viscosità non lineare.

14 prove su net8/net472 nel set candidato candidate-tempo-01; 11843 test delle librerie PASS, nessun fallimento, API compatibili. ANTHEA azioni-tempo-01: Climate.Tests 753 controlli PASS, WPF 372 nei tre temi PASS, Architecture.Tests PASS senza variazioni di baseline. Verificati visivamente grafici e schema originali. Tre esempi distribuiti (totale 82); ordinamento temporale, persistenza e CSV delle storie verificati. Le guide globali saranno pubblicate con l’insieme dei sei fogli.


## Punto di controllo precompressione

Model 34b3be73 e Checker 1b8e4ef2: profilo rettilineo/parabolico piano, attrito, rientro con lunghezza di influenza risolta, tiro bilaterale simultaneo simmetrico, trasferimento, rilassamento delle tre classi EN e perdite differite accoppiate §5.10.6. Per il preteso si applica il tempo equivalente dell’Appendice D alla prosecuzione del rilassamento. Modello di sezione costante, inclinazione del cavo ≤10°; tensioni nel cls assegnate dall’analisi strutturale. Il ramo NTC adotta conservativamente il limite iniziale anche al tiro; formule delle perdite EC2 dichiarate. Non aderente interno: perdita media lungo il cavo, senza trasferimento alla verifica locale Concrete.

16 prove numeriche indipendenti su net8/net472 PASS; tutte le suite del set candidate-prestress-01 PASS. ANTHEA: corsa 20261101-213706-azioni-prestress-transfer, Climate.Tests 871 controlli, WPF 462 controlli, Architecture.Tests PASS. Trasferimento a copia della sezione: verifica di area/materiale, conferma della corrispondenza dei dati strutturali, aggiornamento del trefolo nativo GPC e del relativo archivio; σeff/Ep conserva lo stato dopo salvataggio/riapertura, senza aggiungere N/M duplicati. Tiro e trasferimento restano nella provenienza. Quattro esempi distribuiti; immagini originali e finestra di trasferimento controllate visivamente. Guide nella revisione complessiva.


## Punto di controllo gru e carriponte

Model 14588a87, Checker 595adbf1: EN 1991-3:2006 Tab. 2.2 e gruppi compatibili, φ1 nei due segni della pulsazione, φ2 HC1–HC4, φ3 di rilascio con conservazione delle reazioni negative, φ4–φ7 assegnati con fonte e condizioni, prove dinamiche/statiche. Quattro ruote con ponte e carrello centrati fra gli assi oppure componenti nominali per ruota dal costruttore; azioni orizzontali di guida, vento, urto e ribaltamento assegnate con segno. Accelerazione automatica limitata allo schema a quattro ruote dichiarato. Nessun inviluppo strutturale, fatica o combinazione di più gru attribuiti al foglio.

13 prove net8/net472 e set candidate-cranes-01 completo PASS. ANTHEA azioni-cranes-01: Climate.Tests 961 e WPF 516 controlli PASS. Architecture.Tests ha rilevato una voce del registro aggiunta per errore nell’elenco degli stati: spostata nell’elenco voci, senza modifiche alle baseline; corsa azioni-cranes-arch PASS. Pianta e schema originali controllati visivamente. Tre esempi distribuiti, totale 89. Fonti riscontrate sul rendering delle Tabelle 2.2, 2.4–2.5 e delle formule HL/HT. Allineamento remoto del 1/11 ore 21:51 invariato.


## Punto di controllo incendio

Model b1f26e2f, Checker 5c99a732: curve nominali NTC 2018 ed EN 1991-1-2:2002, storia assegnata con raffreddamento, facce esposte/non esposte/adiabatiche. Scambio convettivo e radiativo con temperature superficiali prescritte e conferme esplicite; nessuna temperatura interna o resistenza R/REI dichiarata. Irraggiamento distinto dall’opzione α=9 del lato non esposto, senza doppio conteggio. Interpolazione senza extrapolazione; nodi delle storie inclusi nel campionamento. Formule e coefficienti controllati sul rendering EN pp. 23–25 e NTC §3.6.1.5.1.

16 prove numeriche su net8/net472 e tutte le suite candidate-fire-01 PASS. ANTHEA 20261101-220415-azioni-fire-01: Climate.Tests 1084, WPF 600 controlli e Architecture.Tests PASS. Verificati grafici nei temi chiaro/scuro e schema originale; corretto il margine del grafico statico per eliminare sovrapposizione della didascalia. Quattro esempi distribuiti, totale 93. Inventario UI sempre 73 suite. Guide globali in completamento.


## Chiusura editoriale e set candidato finale

Guide Rev65: pratica 129 pagine, teorica 209; 1341 e 2537 frammenti di testo verificati, 9 e 1513 formule native OMML conservate, 26 parti del modello ITEC e cinque sezioni inalterate per documento. Rendering Word nativo, controllo visivo di copertine, indici, confine e tutte le pagine aggiunte; schemi e formule senza tagli. LibreOffice non disponibile nel runtime Windows: i PNG di verifica derivano dai PDF finali esportati da Word. Template conservato della Rev64 verificato per SHA-256. Fonti, Word/PDF precedenti e indici archiviati con manifest; PDF accanto a tutti i sorgenti destinati agli utenti.

Wiki: 94 capitoli, dodici nuovi, collegamenti dai sei moduli verificati. Il primo controllo ha rilevato il censimento delle aggiunte non aggiornato; esteso soltanto l’elenco dei nuovi capitoli e aggiunti sei controlli di collegamento, preservando la fixture dei 73 articoli F5. Corsa 20261101-221622-azioni-wiki-final PASS. Climate.Tests 1084 controlli e catalogo dei 93 esempi PASS nella precedente azioni-final-docs.

Set candidate-final-versions da Model 07fb7618 e Checker c79f2c81: 11933 test PASS, zero fallimenti, 13 prove già ignorate dalle suite; API compatibili. GPCModel 5.35.0.0 e GPC.Design 0.45.0.0 per nuove funzionalità, revisioni di ricompilazione per le dipendenze. Nessuna modifica agli attesi numerici. Allineamento remoto finale verificato prima della pubblicazione.


## Integrazione GPC e set ufficiale AQ

Model 07fb7618 e Checker c79f2c81 integrati per fast-forward nei rispettivi origin/master; gli origin precedenti erano già antenati dei branch verificati. Nessun checkout degli altri lavori modificato. Set ufficiale release-aq ricostruito dopo il push con -Previous sul set AP: 11933 PASS, zero fallimenti, 13 prove già ignorate. Tutte le 14 DLL sono identiche al candidato finale. Snapshot ANTHEA installato mediante Update-Snapshot, manifest e provenienza F4 aggiornati; attesi numerici invariati. Full ANTHEA da eseguire su questo insieme prima del merge finale.


## Allineamento alla 1.17.0 e set AR

Durante il full della base precedente è arrivato origin/main f2906bbd (1.17.0). Corsa 20261101-222412-azioni-aq-full interrotta esplicitamente prima delle prove UI: risultati parziali conservati, non dichiarata completa. Rilevato un solo conteggio obsoleto in Presentation.Tests (10 fogli anziché 16): corretto e aggiunto il controllo di presenza univoca dei sei nuovi ID.

Integrato origin/main nel branch Azioni: nessun conflitto di codice; registri uniti per ID senza scartare modifiche, snapshot più recente conservato, tutti i contenuti delle guide di main preservati integralmente. Le due Rev65 sono archiviate con SHA-256 e confluiscono nella Rev66: 130 e 212 pagine, 1355 e 2551 frammenti verificati, 9 e 1566 formule native, 26 parti ITEC identiche. Controllate visivamente copertine, indici, pagine nuove e sezioni interessate dall’integrazione; PDF degli indici e README aggiornati.

Il nome AQ era già utilizzato dalla 1.17.0: il set finale è AR. Ripetuto Release-Set con -Previous sul vero AQ pubblicato: 11933 PASS, zero fallimenti, 13 prove già ignorate; 14 DLL identiche al candidato. Installazione ufficiale e provenienza F4 aggiornate. Nuovo full da eseguire sul risultato unito; nessun installer o tag aggiuntivo creato da questa attività.


## Full sul set AR e correzioni di chiusura

Corsa `20261101-224308-azioni-ar-full`, commit pulito `fe14dae2`: 158 PASS, due NEW-FAIL, nessun blocco e nessun file versionato modificato. Tutte le 73 suite UI mantenute; contrasto completo dei tre temi PASS. Le due anomalie sono l’ordine del catalogo dei nuovi moduli e le posizioni dei risultati della ricerca Wiki. Il catalogo viene riordinato secondo il mandato, senza cambiare nomi, dati o calcoli.

F3: 168 differenze, esclusivamente `wiki/search.json`; 108 variazioni esatte e 60 transizioni di posizione superate. Rispetto alla cattura della 1.17.0 le 30 ricerche restano identiche dopo la rimozione dei dodici nuovi ID. Il registro Rev66 contiene 3199 transizioni editoriali, contro 3127: 133 aggiunte e 61 superate, compreso il conteggio e i 24 nuovi file. Nessuna modifica a input, attesi, transizioni ingegneristiche o tolleranze. Ricontrollo della stessa cattura: 1201 file, zero differenze non ammesse; la copia esclude soltanto il diagnostico errore.txt scritto dopo il fallimento, con SHA-256 di tutti i 1443 file copiati. La cattura originale resta intatta.

PASS: Presentation 4926 controlli, Azioni 1084, Azioni WPF 600 nei tre temi, Reports 476, Esempi 461 su 93 esempi, oltre alle regressioni numeriche e ai confronti indipendenti. Le tre suite non eseguite dal runner restano quelle infrastrutturali già dichiarate: ElasticPile.Checks e BridgeValidationCurrent passano nel collaudo delle librerie; ElasticPile.Performance è una misura separata con input negli artefatti. Nessun fallimento ammesso aggiunto. Ripetizione mirata dopo le due correzioni ancora da completare.


## Chiusura Azioni e richiesta Reologia del 2 novembre

Il ricontrollo pulito `20261101-235831-azioni-ar-final` sul commit `c2c646ad` conclude i due rilievi del full: 22 PASS, zero NEW-FAIL. Il confronto F3 è una cattura nuova, non la sola riclassificazione della precedente. L’audit `final-delta-audit.json` attesta che il codice successivo al full cambia soltanto l’ordine dei sei ModuleInfo, con dati, calcoli, DLL, attesi numerici e tolleranze immutati.

Durante la chiusura l’utente ha chiesto di spostare la finestra della viscosità fra i moduli singoli. La voce è ora **Materiali → Viscosità e maturazione** (`mat_reologia`), salvabile come foglio autonomo e di progetto. Il menu Strumenti globale è tolto; i richiami contestuali dalla sezione Concrete restano disponibili. Il foglio Azioni Ritiro e viscosità continua a gestire la storia dei carichi e la risposta assiale vincolata.

Prima della modifica: `20261102-001031-reologia-before`, cattura di 12 casi (quattro insiemi di parametri e tre cementi), tutti i campioni e sei letture puntuali come bit IEEE754, oltre ai riferimenti indipendenti già presenti. Cattura congelata in `tests/fixtures/rheology`, sorgenti originali archiviati con SHA-256. Dopo: `20261102-002108-reologia-after-first`, confronto esatto, tre temi WPF, validazione, selezione tabella e PNG PASS.

MVVM: ViewModel senza GPC/WPF in Presentation, layout XAML con DataTemplate, disegno tecnico conservato. Application accede allo stesso `ConcreteTimeEffects` di GPC. Il campionamento è copiato integralmente: sei espressioni R1-math trasferite con gli stessi token, cardinalità invariata e nessuna eccezione nuova. Il progetto DesignTools.Presentation rimasto vuoto è rimosso; il controllo di assenza WPF rimane sui due progetti Presentation effettivi.

Il documento conserva parametri, cemento, campionamento, curva, scale logaritmiche ed età della lettura. Una bozza numerica incompleta resta salvabile, senza pubblicare risultati superati. In sola lettura gli ingressi non cambiano; le scelte di consultazione non alterano i dati archiviati. Relazioni autonome e di progetto, due esempi originali nel catalogo centrale: totale 95 esempi e 32 moduli. Guide Rev66, ancora non pubblicate, aggiornate al 2 novembre; nessuna formula o legge di materiale modificata.

Allineamento remoto del 02/11: ANTHEA resta su `f2906bbd`; i nuovi master Model `1a51dad5` e Checker `c2ee6cf0`, pubblicati da un’altra attività per le sovrapposizioni, contengono entrambi i nostri commit Azioni. Non si sovrascrivono quei rami. Lo snapshot AR di questa integrazione rimane quello ufficiale e collaudato; l’adozione di un set successivo è un aggiornamento distinto.


Collaudo Reologia: `20261102-002434-reologia-integrata` ha 4948 controlli Presentation PASS; unico rilievo: il confronto del catalogo storico in Application non escludeva il nuovo modulo. Corretto il confronto preservando tutti i moduli storici. La corsa è di sviluppo, con modifiche contemporanee documentate; non è dichiarata una corsa pulita di rilascio.

Corsa pulita `20261102-003500-reologia-candidate`, commit `2552af70`: 23 PASS e un rilievo F3 soltanto editoriale. Application 3323 controlli, Reologia 24, Esempi 472 su 95 esempi e 32 moduli, architettura, confronto IEEE754, tre temi WPF, shell e ModuleHost PASS. F3: 1201 file, 26 differenze non ammesse, esclusivamente tre articoli Wiki e due ricerche. La parola chiave reologia mantiene il capitolo sui risultati per fasi nella ricerca; registrate 24 variazioni esatte e rimossa una posizione report superata, senza modificare attesi, transizioni ingegneristiche o tolleranze. Nuova cattura finale da eseguire.

Guide finali: 131 e 212 pagine, 1359 e 2551 frammenti verificati, 9 e 1566 formule native e 26 parti ITEC identiche. Capitoli modificati, indici, copertine e istruzioni dei nuovi esempi controllati in PDF. Le relazioni del nuovo foglio usano tabelle con bordi e intestazioni ripetute; i campioni indistinguibili alla precisione stampata sono riportati una volta soltanto nella tabella sintetica, senza cambiare i dati o la tabella interattiva.


## Esito finale del collaudo

Corsa pulita `20261102-004152-reologia-final` sul commit `332b4c11`: **24 PASS, zero NEW-FAIL, zero blocchi, nessun file versionato modificato dalle prove**. Nuova cattura F3: 119 fixture, 94 articoli, 30 ricerche, 1201 file confrontati e zero differenze non ammesse. La ricerca reologia conserva la sequenza precedente; il solo risultato nuovo della ricerca report è la guida del foglio. Le relazioni dei due esempi, due pagine ciascuna, sono copiate byte per byte dagli artefatti della prova e controllate graficamente.

Consolidamento del full AR e dei ricontrolli mirati: **161 voci PASS**, incluse le build e la nuova suite rapida Rheology.Tests; non è una seconda corsa full completa sul commit finale. Inventario UI invariato a **73 suite** prima e dopo. Il dettaglio per suite, commit e corsa è in `supporto/artefatti/azioni-estese/consolidated-final.json`. Le tre voci infrastrutturali non eseguite dal runner restano quelle già spiegate; nessuna nuova esclusione o tolleranza. Catture numeriche, mesh, DLL ufficiali, attesi e transizioni ingegneristiche restano invariati rispetto al collaudo AR.

Verifiche del nuovo foglio: 12 casi storici IEEE754 esatti, riferimenti Python indipendenti, tre temi e formato compatto, tabella, lettura puntuale e PNG; 24 controlli di persistenza, revisioni, invalidazione, relazioni autonome/di progetto ed esempi. Catalogo: 472 controlli su 95 esempi e 32 moduli. Application: 3323 controlli; Presentation completa: 4948; architettura: 591. Guide, indici e report PDF controllati.

Codice e documentazione pronti per l’integrazione finale autorizzata su main, che contiene già la base pubblicata 1.17.0. Nessuna modifica al lavoro degli altri checkout.
