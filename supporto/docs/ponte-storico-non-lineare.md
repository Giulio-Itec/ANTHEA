# Sezione da ponte con storico lineare e non lineare

Implementazione del 25 settembre 2026. Tutto il calcolo è in `Checker/GPCChecker.CompositeBridge/History`. ANTHEA contiene lettura degli ingressi, presentazione ed esportazione; i metodi precedenti restano disponibili.

## Scelta del metodo

- **Cumulativo · metodo precedente**: conserva l'algoritmo precedente, con contributi ricalcolati sulla carpenteria efficace della situazione; include taglio, pioli e dettagli locali. Massimo 20 fasi come nell'API preesistente.
- **Storico lineare**: conserva piano di deformazione al getto, deformazioni imposte e tensioni già accumulate. Fasi e ritiri senza limite numerico prefissato. Classe 4 disponibile tramite le larghezze efficaci elastiche. φ/n modifica il modulo dei nuovi incrementi; non rappresenta una legge di creep dipendente dall'età.
- **Storico non lineare**: equilibrio N–Mx a fibre con la stessa sequenza costruttiva, memoria plastica dell'acciaio e ritiro della soletta. Acciaio bilineare a incrudimento isotropo con scarico elastico; CLS sull'inviluppo tabulato Model, senza danno o isteresi ciclica. Armature con legge plastica propria. Una sola fase permette il caso di carico singolo.

Nel non lineare la sezione è **lorda**, a legami **caratteristici**. L'opzione di classe 4 resta memorizzata per il lineare ma non viene applicata. Non si attribuisce una verifica SLU di classe 4 a un calcolo plastico privo di instabilità locale. V viene registrato; taglio, interazione N–M–V, pioli e irrigidimenti non ricevono esiti dai metodi con storico. I dati accessori e il loro calcolo nel metodo precedente sono conservati.

Il non lineare offre due scelte per la viscosità: analisi istantanea esplicita (φ=0 nel calcolo, φ/n d'archivio conservati), oppure obbligo di φ=0 negli ingressi. Non viene applicata una riduzione arbitraria di E a una legge plastica. I ritiri continuano a essere deformazioni imposte anche nell'analisi istantanea.

## Risultati e uso della vista

La scheda Fasi e tensioni contiene selettore del metodo e gruppo espandibile per strisce di anima/flange/CLS e sottopassi. Due punti di integrazione per striscia. I valori iniziali sono 160/8/64 strisce e 8 sottopassi: sono impostazioni iniziali, non una garanzia di convergenza al risultato continuo.

Si riportano piani totali e incrementali, riferimento di N per fase, risultanti integrate, residui, aree efficaci, tensioni di ciascuna fibra, riferimento al getto, deformazioni imposte/meccaniche e plastiche. I diagrammi e il contouring utilizzano il campo a fibre, non una retta ricavata da My/I. I Δσ sono differenze fra stati successivi, comprese le redistribuzioni. Alle facce il diagramma non lineare usa la fibra prossima, senza estrapolare fuori dal dominio costitutivo; gli estremi integrati sono riportati esplicitamente.

Il CSV dei metodi con storico esporta tutte le fibre e gli stati delle fasi. Il JSON contiene anche gli stati plastici. Il report Word ha contenuti specifici per lo storico e non presenta forze equivalenti di ritiro o rigidezze tangenti fittizie. Le proprietà omogeneizzate sono diagnostiche ai moduli iniziali della fase.

**Stacca vista** sposta il grafico in una finestra indipendente e sincronizzata. È possibile selezionare la fase, modificare scala/contouring, massimizzare o usare F11 per schermo intero. Esc esce dallo schermo intero. Riaggancia o la chiusura della finestra riporta la vista al modulo; la chiusura del modulo chiude anche la finestra. Durante un ricalcolo resta visibile l'ultimo stato con indicazione di risultato da aggiornare.

## Affidabilità e verifiche

Il nuovo solutore ha verifiche analitiche, test di comportamento e un confronto numerico **eseguito con OpenSees 3.8.0**, indipendente dal codice Checker: 7 storie e 29 stati. Il riferimento e lo script sono in `Checker/GPCChecker.Test.CompositeBridge/Validation`, insieme a ipotesi, tolleranze e studio dei sottopassi. Lo scarto massimo misurato è 0,00084431 MPa per la tensione e 7,6281e−9 per la deformazione di fibra.

Le soluzioni analitiche e i test di regressione sono stati scritti durante questo sviluppo: non equivalgono a una validazione di terzi. I dati del confronto OpenSees sono risolti dall'eseguibile esterno e congelati nel repository; il modello di confronto e le scelte dei materiali sono stati predisposti nello stesso lavoro. Non si tratta di una validazione sperimentale o di una certificazione normativa.

La suite CompositeBridge conta 121 casi; l'audit ordinario del modulo con integrazione degli archivi e del report conta 279 casi. L'audit ordinario esclude esplicitamente i difetti noti delle vecchie librerie native (categorie KnownBug e ConstructorRegression), già documentati; non sono stati dichiarati corretti né conteggiati tra gli esiti verdi. Le prove di interfaccia e gli artefatti di questa attività sono in `supporto/artefatti/ponte_non_lineare`.

È stato corretto un difetto di convergenza del nuovo metodo emerso nel confronto: tangenti non coerenti fra fibre equivalenti alla cuspide di snervamento per arrotondamento. Il predittore iniziale è ora elastico e le iterazioni successive usano le tangenti dei materiali; nessuno stato è accettato senza equilibrio.

Prima dell'uso progettuale restano necessarie revisione indipendente del modello e verifiche applicabili alla struttura reale. In particolare non sono coperti post-instabilità plastica di classe 4, evoluzione viscosa nel tempo, comportamento ciclico danneggiato del CLS, connessione parziale o equilibrio globale della trave. Per casi nuovi confrontare discretizzazioni e sottopassi e leggere i residui. Se una fase non converge, il calcolo corrente fallisce e non sostituisce l'ultimo risultato valido con uno stato parziale.
