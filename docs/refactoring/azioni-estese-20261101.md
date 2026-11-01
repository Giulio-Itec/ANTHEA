# Azioni aggiuntive — 1 novembre 2026

## Mandato e integrazione

L'utente approva i sei fogli proposti e l'ordine: idraulica, fasi costruttive, effetti differiti e deformazioni imposte, precompressione e perdite, gru/carriponte, incendio. Traffico interamente in standby, seguito da un collega. Merge dei nostri branch sui rami principali al termine di tutti i lavori, dopo i collaudi; mantenere l'allineamento con i rami pubblicati. Le regole Git delle librerie autorizzano anche il push dopo integrazione di origin/master e suite verdi. Il ramo principale di ANTHEA si chiama main.

Checkout isolati in `../azioni-estese-20261101/{ANTHEA,Model,Checker,Utilities,Geometry}` rispetto al repository padre. Branch `codex/azioni-estese-20261101` nei tre repository modificati; Utilities e Geometry invariati, in checkout separati. Basi pubblicate: ANTHEA 1dfe7110, Model f4f78752, Checker eb16e72c. Nessuna modifica ai checkout degli altri lavori.

## Architettura e contratto

Contratti, unità e dati in GPC.Model; formule e integrazioni in GPC.Design, riuso dei motori esistenti. ANTHEA mantiene documenti versionati, adattatori senza WPF, ViewModel e viste F5. Materiali, pressione, forza, momento, deformazione e temperatura restano grandezze distinte; i valori delle azioni non vengono maggiorati due volte. Ogni foglio conserva i casi alternativi e la provenienza dei parametri. Schemi dinamici quotati e grafici, CSV, relazione, esempi e guide globali accompagnano ogni modulo.

## Piano e verifiche

- [ ] Idraulica: parete, platea e pila in corrente; scenari alternativi, pressioni nette, risultanti e primi momenti; profili assegnati di sottospinta e velocità. Prove indipendenti su triangolo/trapezio, Archimede, traslazione del riferimento, corrente uniforme/variabile/inversa, coppia pura e input non validi.
- [ ] Fasi costruttive: carichi e sequenza, senza attribuire al foglio una redistribuzione strutturale non calcolata. Riusare i contratti delle fasi GPC.
- [ ] Ritiro, viscosità, maturazione e spostamenti imposti: storia degli eventi e separazione fra deformazioni libere e risposta vincolata.
- [ ] Precompressione: geometria del cavo, tiro, attrito, ancoraggi, perdite immediate/differite e stato efficace inviabile alla sezione.
- [ ] Gru/carriponte: azioni alle ruote e gruppi di carico compatibili, parametri e dati del costruttore tracciati.
- [ ] Incendio: azioni termiche nominali, facce esposte e condizioni termiche; la temperatura dei gas non è la temperatura della sezione.
- [ ] Guide globali, Word/PDF, esempi e collaudi UI nei tre temi; nessuna suite UI eliminata.
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
