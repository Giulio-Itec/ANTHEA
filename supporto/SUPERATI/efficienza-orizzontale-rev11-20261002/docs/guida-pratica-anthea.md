# Guida pratica di ANTHEA

Manuale operativo dei moduli disponibili

Edizione 3 del 2 ottobre 2026 — revisione documentale 10

Questa edizione unifica la documentazione di ANTHEA in due volumi globali. Il volume pratico comprende uso, interfaccia e procedure; quello teorico comprende modelli, formule, ipotesi, limiti e approfondimenti di tutti i moduli. I capitoli di approfondimento conservano integralmente i contenuti delle precedenti schede. Audit, migrazioni e studi conservano la loro data e il loro ambito storico: non descrivono automaticamente lo stato attuale del programma.


Questa guida accompagna l'utilizzatore dalla creazione del progetto alla lettura dei risultati e alla produzione dei report. Comprende i moduli geotecnici, i materiali, le sezioni in calcestruzzo armato, la sezione composta da ponte e Bridge Design. La revisione 03 integra l'edizione del 26 settembre con H ad anima inclinata e cassoncino disponibili il 27 settembre 2026; la revisione 04 aggiunge le verifiche a torsione, distorsione e diaframmi del cassoncino disponibili il 28 settembre 2026. La revisione 05 aggiunge i muri con due stratigrafie; la revisione 06 completa il percorso guidato della stabilità globale, l’inserimento per spessori e il disegno del terreno profondo. La guida teorica separata descrive le formule e le scelte di modello; le due guide vanno utilizzate insieme quando si deve motivare un risultato.

ANTHEA raccoglie strumenti con scopi diversi. Alcuni verificano una sezione o un meccanismo specifico, mentre Bridge Design produce ordini di grandezza. Il risultato di un foglio riguarda il suo modello e i dati inseriti: non equivale alla verifica completa della struttura o dell'opera. Il percorso operativo più utile consiste nel definire il problema, scegliere il foglio adatto, controllare le unità, esaminare gli avvisi e archiviare i dati insieme al report.

## 1 Avvio e scelta del modulo

### 1 1 Avviare la versione aggiornata

Aprire Avvia ANTHEA.cmd nella cartella dell'applicazione. Il comando avvia app/ANTHEA.exe, quando presente; in ambiente di sviluppo può compilare e avviare il progetto. Compila.cmd aggiorna la distribuzione nella cartella app. La distribuzione dipendente dal framework richiede il runtime desktop .NET 8. Il sorgente e la distribuzione sono due cose distinte: modificare un file C# non aggiorna un eseguibile già pubblicato.

La Home consente di entrare nel catalogo, riprendere il lavoro della sessione o organizzare i fogli in un progetto. Il pulsante Riprendi conserva l'editor e il suo stato finché l'applicazione resta aperta. Non salva automaticamente un archivio su disco. Prima di chiudere utilizzare Salva o Salva con nome.

### 1 2 Scegliere il foglio adatto

| Foglio disponibile | Domanda alla quale risponde | Informazione da preparare |
| --- | --- | --- |
| Palo verticale | Quale resistenza assiale geotecnica si ottiene con questi terreni e questa lunghezza | Stratigrafie, falda, tecnologia, azioni e coefficienti |
| Micropalo verticale | Quale resistenza del bulbo iniettato è compatibile con l'abaco scelto | Terreni, p_l, iniezione, diametri, inclinazione e tratto attivo |
| Palo orizzontale | Quale carico limite laterale risulta dal meccanismo di Broms | Terreno, diametro, lunghezza, vincolo e momento resistente |
| Micropalo orizzontale | Quale capacità laterale risulta usando una sezione tubolare CHS | Diametro geotecnico, tubo, materiale, N e terreno |
| Sezione in c a | Come risponde e si verifica una sezione assegnata | Geometria, barre, materiali e combinazioni N M V T |
| Sezione composta | Come si ripartiscono le tensioni in una sezione da ponte attraverso le fasi | Tipo e geometria, soletta, barre, fasi e metodo |
| Bridge Design | Quale configurazione preliminare, quantità, costo e CO₂ sono plausibili | Sito, campate, larghezza, famiglia, fondazioni e listino |
| Calcestruzzo e durabilità | Quali requisiti del materiale e del copriferro derivano dalle scelte assegnate | Esposizioni, vita, materiale, diametri e condizioni esecutive |
| Acciaio per armature | Quali proprietà e diagramma usare per l'armatura | Classe o dati personalizzati e coefficienti |

Le altre voci eventualmente presenti nel catalogo possono essere predisposizioni. Una scheda di materiali non verifica una sezione. La sezione composta non analizza un intero ponte; Bridge Design non trasferisce automaticamente un modello verificato alla sezione composta. Per passare dall'uno all'altro occorre scegliere la sezione locale, assegnare le azioni da un'analisi appropriata e controllare nuovamente le unità.

### 1 3 Leggere e compilare i campi

Controllare sempre l'unità a fianco del valore. Nei moduli geotecnici le lunghezze sono generalmente in metri; nelle sezioni strutturali e nelle barre sono in millimetri. Una sezione alta 600 mm non va inserita come 0,60. MPa e N/mm² sono numericamente equivalenti; kPa e kN/m² lo sono altrettanto, ma un valore in MPa è mille volte il corrispondente valore espresso in kPa.

I campi vuoti servono anche a rappresentare una bozza incompleta. Non sostituire sistematicamente un dato sconosciuto con zero: in alcuni campi zero ha un significato fisico, in altri attiva un automatismo, in altri è un errore. Bridge Design indica esplicitamente «0 = auto» per i parametri che lo consentono. Quando un campo è temporaneamente incompleto, leggere il messaggio della scheda prima di considerare i risultati.

La finestra segue l'ingrandimento di Windows. I pannelli più stretti si dispongono verticalmente; le tabelle possono mantenere uno scorrimento proprio. I divisori nella composizione dei progetti permettono di allargare l'albero o il catalogo. Cercare prima la barra del pannello che contiene il dato: lo scorrimento della finestra e quello della tabella non sono necessariamente lo stesso comando.

## 2 Progetti e gestione del lavoro

### 2 1 Costruire una struttura leggibile

La pagina di composizione presenta le informazioni del progetto a sinistra, l'albero al centro e il catalogo dei fogli a destra. Creare il progetto, assegnargli un nome utile e aggiungere sezioni. Una possibile struttura per un ponte comprende Materiali, Impalcato, Pila 1 e Fondazioni. Ogni sezione può contenere sia fogli sia sottosezioni; anche il progetto può contenere fogli direttamente.

Trascinare una scheda dal catalogo nel contenitore desiderato. Le miniature aprono i fogli. Il nome di una sezione, oppure Invio, apre il riepilogo. Doppio clic o F2 consentono di rinominare; cliccare fuori dal nome conferma. Il menu contestuale delle sezioni offre rinomina, duplicazione ed eliminazione. Nei fogli la freccia nella barra mostra o nasconde l'albero laterale. Torna al progetto riapre il riepilogo.

Usare nomi che distinguano oggetto e condizione: «Pila 2 sezione base», «Palo 1000 sondaggio S3» o «Impalcato campata positiva». Nomi generici ripetuti rendono difficile capire quale dato sia stato modificato e quale foglio stia governando la condivisione.

### 2 2 Comprendere i dati comuni

La condivisione è gerarchica e avviene per proprietà compatibili. Per una proprietà prevale il livello più alto che la definisce e che può trasferirla al destinatario. La regola attraversa anche sezioni intermedie vuote. I rami paralleli condividono i dati dei loro antenati comuni, non tutti i dati l'uno dell'altro.

Per esempio, un materiale CLS definito nel progetto può governare i fogli compatibili delle pile e dell'impalcato. Se si vuole deliberatamente un materiale diverso per un ramo, occorre verificare come è organizzato il riferimento superiore: scrivere un valore diverso in basso può produrre un conflitto, senza modificare il riferimento. Il confronto mostra percorso, valore e provenienza dei dati. «Uniforma a questo» agisce nel ramo governato dal riferimento e non promuove dal basso una proprietà già governata da un antenato.

Quando si confermano o si salvano modifiche a un riferimento, le proprietà compatibili possono propagarsi ai discendenti. In assenza di un riferimento superiore, resta la scelta fra aggiornare i fogli dello stesso livello e mantenere il dato locale. I fogli nuovi ereditano ciò che è definito senza ambiguità. Se due riferimenti dello stesso livello sono discordanti, ANTHEA non sceglie silenziosamente uno dei due.

Prima di produrre un report di progetto aprire il riepilogo Controlli e Dati comuni. Un avviso sul copriferro o un conflitto non è necessariamente un errore numerico, ma richiede una decisione esplicita. La condivisione non deve essere confusa con una sincronizzazione universale di ogni campo: gli adattatori dei moduli preservano unità, forme ammesse e significato delle proprietà.

### 2 3 Spostare duplicare e annullare

Il bordo di un'intestazione indica il riordino prima o dopo un elemento dello stesso gruppo. Il centro di una sezione consente di trasferirvi un'altra sezione con il suo contenuto. Non si possono creare cicli. Un trasferimento fra rami può cambiare i riferimenti comuni: l'anteprima mostra gli effetti su una copia del documento prima di applicarli. Annullare l'anteprima lascia invariato il progetto.

Duplica sezione crea nuovi identificativi per tutti i fogli e le sottosezioni. La copia non acquisisce la cronologia delle revisioni dell'originale. I dati locali sono indipendenti, ma continuano a essere soggetti alle normali regole di condivisione del ramo in cui si trovano.

Annulla e Ripristina del progetto conservano fino a 30 stati della sessione. Ctrl+Z e Ctrl+Y operano sul progetto quando il cursore non è in un campo di testo; nei campi prevale l'annullamento del testo. La cronologia non viene salvata nel file. Bridge Design possiede inoltre un proprio Annulla per le modifiche alla scheda; non coincide con una revisione del progetto.

### 2 4 Usare le revisioni

Nuova revisione archivia la versione attuale e apre la successiva. Alla prima operazione si conserva Rev. 0 e si passa a Rev. 1. Ogni sezione può avere una numerazione indipendente. Nei fogli viene usata la sezione revisionata più vicina. La nota serve a ricordare il motivo della revisione, per esempio «Aggiornamento falda da indagini» o «Riduzione larghezza impalcato».

Le revisioni archiviate si consultano nella stessa finestra, con gli input protetti e i comandi di visualizzazione ed esportazione disponibili. Tornando alla revisione attuale si ritrovano le modifiche non ancora salvate. Il contesto storico comprende gli antenati necessari; non è un collegamento mutabile ai materiali correnti. Salva, anche mentre si consulta lo storico, conserva l'intero documento corrente con le revisioni.

Eliminare una revisione archiviata rimuove quella versione senza rinumerare le altre. Eliminare l'attuale ripristina l'ultima rimasta e la rende modificabile; riguarda il ramo scelto. I materiali degli antenati del progetto corrente possono differire da quelli del ramo ripristinato: controllare quindi il confronto. L'unica versione rimasta non è eliminabile con il comando delle revisioni.

## 3 Materiali e durabilità

### 3 1 Preparare il calcestruzzo

Selezionare una o più classi di esposizione realmente pertinenti alla superficie e all'ambiente. X0, XC, XD, XS, XF e XA descrivono fenomeni differenti; non sono livelli successivi di un'unica scala. In presenza di esposizioni combinate il modulo ricerca i requisiti più gravosi fra quelli applicabili. Non scegliere semplicemente la classe con il nome alfabeticamente maggiore.

Controllare la classe resistente proposta, il rapporto acqua cemento massimo, il contenuto minimo di cemento e le eventuali indicazioni sull'aria inglobata. I prospetti adottati sono identificati nella guida teorica e nei riferimenti del modulo. La scheda aiuta a specificare i requisiti; non determina da sola una ricetta di produzione del calcestruzzo, acqua totale, additivi o granulometria ottimizzata.

Per il copriferro assegnare norma selezionata, vita nominale, tipo di elemento, diametro delle barre, dimensione dell'aggregato, tolleranza esecutiva e condizioni aggiuntive. Distinguere il minimo dal nominale. Quest'ultimo comprende il margine esecutivo previsto dalla scelta del foglio. Un diametro maggiore o un requisito di getto controterra può governare anche quando la durabilità richiederebbe meno.

Il copriferro del disegno della sezione e quello richiesto dalla scheda materiali devono riferirsi alla stessa superficie: esterno staffa, superficie della barra longitudinale e asse barra non coincidono. Nel progetto il confronto evidenzia le incoerenze, ma la lettura del dettaglio resta necessaria quando vi sono staffe, più strati o fasci.

### 3 2 Preparare l'acciaio per armature

Scegliere B450C, B450A, una voce storica o un materiale personalizzato. Verificare E, fy, fu, deformazioni e coefficiente parziale. Il grafico mostra il diagramma associato ai dati: controllare in particolare la presenza o meno di incrudimento e il tratto ultimo. Un materiale personalizzato va nominato in modo da poter risalire alla sua provenienza.

Le classi storiche FeB non sostituiscono la caratterizzazione di un acciaio esistente. L'allungamento a rottura su base A5 non è automaticamente la deformazione ultima utilizzabile nel diagramma costitutivo. Se un dato richiesto non è noto, il foglio deve restare incompleto per quella funzione, anziché completarlo con un valore scelto per ottenere un esito favorevole.

Il trasferimento dei materiali ai fogli compatibili riguarda le proprietà comuni. Il tubo del micropalo orizzontale è un materiale strutturale specifico della sezione CHS. Inoltre un motore può adottare una legge semplificata propria: il calcolo automatico di My del palo orizzontale usa acciaio elastico perfettamente plastico anche se il catalogo dell'armatura contiene informazioni più estese.

## 4 Palo verticale

### 4 1 Compilazione ordinata

Impostare tecnologia del palo, diametro, lunghezza, peso del materiale, azioni assiali e coefficienti. Compilare poi le stratigrafie. Ogni nuovo sondaggio parte con una riga da completare, non con un terreno automaticamente valido. Inserire nome, spessore, famiglia del terreno, addensamento dove richiesto, pesi di volume e parametri di resistenza.

Per un calcolo drenato occorrono i parametri efficaci. Per il ramo non drenato degli strati coesivi occorre Cu; il programma considera anche la posizione della falda per decidere il tratto nel quale applicare quel ramo. Non usare c′ e Cu come sinonimi. Negli strati granulari il contributo c′ alla resistenza laterale drenata è nullo, anche se il campo contiene un numero.

Indicare la presenza e la profondità della falda. L'effetto sulle tensioni del terreno e l'opzione di sottospinta sul peso proprio sono aspetti distinti. Spuntare la sottospinta solo secondo l'ipotesi di peso immerso che si vuole applicare al palo; non aspettarsi che questo comando definisca da solo la stratigrafia satura.

«Copia in» e «Copia da» permettono di riutilizzare gli strati fra sondaggi creando copie indipendenti. Controllare la destinazione prima di sovrascriverla. Il pulsante meno della riga elimina uno strato; quello della linguetta elimina la stratigrafia. Il colore identifica il terreno nel profilo e non costituisce un parametro di calcolo.

### 4 2 Coefficienti e gruppo di pali

Verificare K e μ proposti per la tecnologia e l'addensamento. Controllare l'abaco Nq e la posizione del punto: un parametro sul bordo dell'abaco non dimostra che il terreno appartenga al campo sperimentale. Il numero di indagini scelto per ξ non è semplicemente il numero di linguette visibili. Deve rappresentare la base conoscitiva che si intende utilizzare.

Selezionare nessuna riduzione, Converse Labarre, Feld oppure efficienze definite dall'utente. Per i metodi geometrici servono numeri di pali e interassi coerenti. Il risultato è una riduzione della capacità per palo nel modello: non è una verifica completa del blocco di terreno, della distribuzione dei carichi nel plinto o dei cedimenti di gruppo.

### 4 3 Leggere i risultati

Le curve drenate sono verdi e quelle non drenate viola; compressione e trazione si distinguono anche per tono e tratteggio. Le azioni sono rappresentate separatamente. Alla lunghezza di progetto leggere contributo laterale, punta, resistenza in compressione, resistenza in trazione e relativi coefficienti. La trazione non utilizza automaticamente gli stessi fattori del ramo di compressione.

La punta e il laterale minimo possono provenire da sondaggi diversi. La curva costruita con i minimi delle componenti non rappresenta necessariamente un unico sondaggio reale. Confrontare anche le curve delle singole stratigrafie prima di interpretare il motivo della riduzione.

Se una stratigrafia non raggiunge la quota richiesta, il calcolo non inventa gli strati mancanti. Integrare l'indagine o rivedere la lunghezza. La casella che disattiva il laterale elimina il contributo resistente di quel tratto, ma ne conserva il peso e l'effetto sulle tensioni negli strati sottostanti. È utile per escludere un tratto non affidabile, non per modellare automaticamente l'attrito negativo.

### 4 4 Esempio di controllo manuale

Per una sola tratta con D = 1 m, lunghezza attiva 10 m e resistenza laterale uniforme τ = 50 kPa, il contributo laterale è π × 1 × 10 × 50 = 1570,8 kN. Questo è un controllo della geometria e delle unità, prima dei coefficienti ξ, γ ed η. Se il foglio restituisce un ordine di grandezza mille volte diverso, verificare MPa contro kPa e metri contro millimetri.

Procedere poi con i parametri realmente variabili del terreno. Non inserire τ = 50 nel campo Cu o c′ aspettandosi lo stesso risultato: quei parametri entrano in formule differenti. L'esempio serve a leggere il contributo restituito, non a sostituire il modello geotecnico.

## 5 Micropalo verticale

### 5 1 Dati del bulbo e dell'iniezione

Assegnare diametro di perforazione, iniezione IGU o IRS, famiglia di terreno, coefficiente di espansione α e p_l utilizzato dall'abaco. L'interfaccia espone il campo «Pressione p_i = p_l» e il codice assume quell'uguaglianza. p_l è però la grandezza geotecnica dell'abaco: usare il valore della pompa richiede una giustificazione della correlazione, non la sola coincidenza delle unità. Documentare quindi quale misura o interpretazione abbia prodotto il dato inserito.

Definire l'inizio del bulbo resistente. La lunghezza libera o il tratto escluso non produce resistenza laterale del bulbo. La resistenza viene integrata soltanto nelle tratte attive, considerando il terreno attraversato. L'eventuale contributo di punta è una percentuale del laterale: attivarlo non introduce una verifica autonoma della base.

Inserire l'inclinazione rispetto alla verticale e verificare la lunghezza lungo l'asse. I passaggi di strato sono definiti in profondità verticale e vengono convertiti nella lunghezza percorsa dal micropalo. Controllare quindi sia il profilo del terreno sia il riepilogo delle tratte, soprattutto per inclinazioni elevate.

### 5 2 Tubo e peso

Le dimensioni del tubo servono al calcolo dell'area di acciaio, del volume di boiacca e del peso. Nel modulo verticale non equivalgono a una verifica automatica di instabilità del tubo, della sezione composta acciaio boiacca o del collegamento in testa. Diametro esterno del tubo, diametro di perforazione e diametro espanso del bulbo sono tre misure diverse.

Verificare che p_l rientri nel campo della curva selezionata: il motore rifiuta l'estrapolazione. Un messaggio di fuori abaco va risolto cambiando la base di calcolo o il dato, non forzando il valore al bordo senza motivazione. Nel report conservare curva, tipo di iniezione, α, pressione e tratte: sono le informazioni che consentono di ricostruire la stima.

## 6 Pali e micropali caricati orizzontalmente

### 6 1 Preparare un modello coerente

Il modulo applica meccanismi limite di Broms. Definire il diametro geotecnico, la lunghezza infissa, la famiglia del terreno, il vincolo in testa e l'eccentricità della forza. Per la testa libera il momento esterno è H × e. La testa impedita richiede e = 0 nel modello disponibile. Un momento indipendente aggiunto alla forza non è gestito come azione generica.

Lo sforzo normale N è positivo a compressione in questi fogli. È una convenzione diversa dalla sezione in c.a. Non copiare il segno senza controllarlo. N entra nel calcolo del momento resistente della sezione quando questo è automatico; non trasforma Broms in un'analisi completa del secondo ordine del palo nel terreno.

I casi omogenei costituiscono il campo più direttamente interpretabile. La stratificazione della stessa famiglia e la falda sono indicate come estensioni sperimentali. Le alternanze di terreni coesivi e granulari non vengono assimilate silenziosamente a un unico terreno equivalente.

### 6 2 Momento resistente del palo in calcestruzzo

Nel ramo automatico assegnare sezione circolare, diametro, copriferro, staffa, barre longitudinali distribuite uniformemente e materiali. Verificare la posizione degli assi delle barre e il valore di N. La procedura confronta due discretizzazioni; se non raggiunge l'accordo richiesto o esce dal campo ammesso, il momento non deve essere considerato disponibile.

È possibile assegnare My manualmente, corredandolo della provenienza. Registrare quale sezione, N, diagrammi e coefficienti abbiano prodotto il valore. My non è un parametro da aumentare fino a far passare il controllo. La formazione di una cerniera nel modello richiede inoltre una valutazione separata della capacità rotazionale del dettaglio.

### 6 3 Sezione CHS del micropalo

Inserire il diametro esterno e lo spessore del tubo in millimetri, distinti dal diametro geotecnico in metri. Il momento automatico è disponibile nel campo di classe 1 adottato. La riduzione con N utilizza l'interazione semplificata specificata nella guida teorica. La boiacca non incrementa la resistenza della sezione CHS in questo ramo.

Un tubo classificato fuori campo richiede un altro calcolo di sezione oppure un My documentato. Non basta che l'area del tubo sia grande: snellezza locale, sforzo normale e possibile instabilità globale pongono problemi differenti.

### 6 4 Interpretare diagrammi e capacità

Leggere Hu, il regime governante, My usato, profondità caratteristiche e diagrammi di pressione, taglio e momento. La pressione limite rappresentata lungo il palo ha unità kN/m; non è una pressione superficiale in kPa. Il passaggio da Hu a resistenza di progetto introduce separatamente coefficienti statistici, parziale di resistenza ed efficienza di gruppo.

Nei regimi granulari corti o intermedi può comparire una reazione concentrata al piede, necessaria per l'equilibrio del modello. Un salto nel taglio in quel punto non è necessariamente un errore grafico. Il risultato non include spostamenti laterali di esercizio e non è una soluzione con molle p y. Se il problema principale è limitare la rotazione in testa o lo spostamento, occorre un modello di deformabilità ulteriore.

## 7 Sezione in calcestruzzo armato

### 7 1 Geometria barre e materiali

Nel pannello di controllo scegliere rettangolo, cerchio o T e definire le dimensioni in millimetri. I fori centrali sono disponibili per le geometrie ammesse: controllare dimensioni, posizione e sezione netta. La circonferenza è rappresentata da un poligono; aumentarne i lati serve alla discretizzazione geometrica e non coincide con l'aumento delle divisioni angolari del dominio.

Compilare le barre per coordinate o disposizioni previste dalla scheda. Le barre sono identificate singolarmente e devono trovarsi nel calcestruzzo, fuori dai vuoti. Nella T controllare anche le barre presso l'ala e gli angoli superiori della staffa d'anima. Il disegno è il primo controllo dell'input: contare le barre e confrontare l'area totale con quella attesa.

Per un tendine usare l'area metallica effettiva e la tensione iniziale efficace. Il diametro equivalente di un gruppo serve a rappresentarne l'area, non è il diametro esterno nominale di una guaina. Il foglio non ricava automaticamente tutte le perdite di precompressione: il valore iniziale deve essere già coerente con la situazione analizzata.

### 7 2 Azioni assi e convenzioni

Nella sezione in c.a. N negativo indica compressione. Le azioni sono in kN e kNm; i materiali in MPa. Esaminare gli assi riportati sulla sezione e l'eventuale rotazione verso gli assi principali o scelti dall'utente. La posizione di un punto nel dominio si interpreta nel sistema di riferimento indicato dal foglio, non in un sistema ricordato da un altro programma.

Inserire combinazioni già definite. Le famiglie Rara, Frequente e Quasi permanente identificano gli stati di esercizio da verificare; il foglio non deduce automaticamente tutte le combinazioni dai singoli carichi dell'opera. Cambiare la famiglia senza aggiornare le azioni non crea una nuova combinazione corretta.

### 7 3 Domini tridimensionali e bidimensionali

Le schede dominio 3D e dominio 2D permettono di esaminare le resistenze della sezione e il punto richiesto. Scegliere il criterio di ricerca coerente con il problema: mantenere N costante e aumentare il momento non è lo stesso percorso che mantenere costante l'eccentricità. Il coefficiente risultante dipende anche da questo percorso.

Un dominio bidimensionale è una sezione o una proiezione del problema. Se il punto ha componenti fuori dal piano rappresentato, leggere l'indicazione di proiezione e non trattare l'immagine come una verifica completa della pressoflessione deviata. I quattro momenti rapidi Mx positivo e negativo, My positivo e negativo sono utili per orientarsi, ma non descrivono da soli l'intero dominio N Mx My.

I nomi Plastico ed Elastico descrivono i modelli disponibili. Eventuali chiavi storiche SLU e SLV nei file non vanno interpretate come una generazione automatica di verifiche sismiche a un livello di danno.

### 7 4 Tensioni e fessurazione

Aprire la scheda dedicata, scegliere la combinazione e leggere prima lo stato tensionale: zona compressa, barre tese, tensione massima, asse neutro e deformazioni. Le deformazioni a video possono essere in per mille; i coefficienti di curvatura hanno un'altra dimensione. Non confrontare i due numeri senza conversione.

La fessurazione dipende anche da durata, aderenza, copriferro alle barre, diametri, interassi e area efficace di calcestruzzo teso. Aprire il dettaglio del calcolo di wk: permette di capire se governa il termine di deformazione minima o quello corretto per il tension stiffening, e se si applica il ramo di barre ravvicinate o distanziate.

La trazione dell'intera sezione segue un ramo specifico con regioni di bordo. Le superfici dei fori e le disposizioni non supportate non devono essere considerate verificate perché il disegno appare completo. Analogamente, una soluzione tensionale non lineare o una sezione con tendini può avere disponibilità diversa per il controllo di fessurazione. Leggere lo stato della singola verifica, non soltanto il grafico delle tensioni.

### 7 5 Taglio torsione e dettagli

Nella scheda taglio e torsione inserire azioni, staffe, passo, inclinazione e armatura longitudinale pertinente. Controllare bw, d e braccio interno ricavati. Per sezioni circolari il ramo adottato è quello specifico per pali descritto nella guida teorica; non coincide automaticamente con una formula generica per travi rettangolari.

La torsione richiede un dettaglio chiuso compatibile e armatura longitudinale disponibile dopo le esigenze di flessione. Non assegnare come disponibile tutta l'armatura senza aver verificato questa condizione. La verifica combinata usa anche un'interazione conservativa fra torsione e i due tagli, esplicitata nel dettaglio.

Nella scheda dettagli costruttivi scegliere il tipo di elemento e completare le informazioni su diametri, passi, sovrapposizioni, ancoraggi e copriferro. «Da completare» non equivale a «Verificato». I controlli automatici riguardano le condizioni esplicitamente rappresentate; ganci, nodi, confinamento e situazioni esecutive speciali possono richiedere valutazioni aggiuntive.

### 7 6 Curva momento curvatura e aggiornamento

La curva M χ si avvia con il proprio comando. Assegnare N, direzione del momento, numero di passi e distribuzione dei punti. Esaminare la progressione della risposta e le deformazioni terminali. La curva arriva al limite trattato dal motore e non descrive automaticamente un ramo post picco di degradazione della struttura.

Se si modifica l'input mentre sono visibili risultati precedenti, le sezioni strutturali possono segnalarli come «DA AGGIORNARE». Quel risultato non deve essere esportato o letto come riferito ai nuovi dati. Attendere il completamento del ricalcolo e controllare lo stato. La conservazione grafica serve a orientarsi durante l'editing, non a certificare la validità di valori obsoleti.

Le tabelle CA consentono copia e incolla e dispongono dei comandi di template e reimportazione previsti dall'interfaccia. Prima di un'importazione estesa salvare il foglio, controllare intestazioni e unità, quindi verificare numero di righe, barre e combinazioni importate. La semplice riuscita dell'importazione non dimostra che l'ordine delle colonne fosse quello voluto.

## 8 Sezione composta da ponte

### 8 1 Definire la sezione locale

Nel Pannello di controllo aprire Geometria e scegliere il Tipo di sezione: H saldato, H con anima inclinata oppure Cassoncino. Inserire soletta, carpenteria e armature opzionali. La scelta cambia la geometria, i campi visibili e l'interpretazione delle larghezze; non è una semplice variante del disegno. Queste sezioni appartengono al modulo Sezione composta da ponte, distinto dalle famiglie parametriche del predimensionamento Bridge Design.

L'Altezza libera anima è la distanza verticale netta fra le flange. Lo Spessore anima è misurato perpendicolarmente alla lamiera anche quando questa è inclinata: inserire lo spessore nominale, non la sua proiezione orizzontale. Il programma calcola la lunghezza inclinata e la sezione equivalente necessaria all'analisi. Tutte queste dimensioni sono in millimetri.

La Seconda piattabanda inferiore è disponibile solo per H saldato. Le due piastre sono reali e distinte, con spessore e larghezza propri; la seconda non deve essere più larga della prima. Passando all'anima inclinata o al cassoncino il riquadro viene nascosto e la seconda piastra non partecipa al calcolo, anche se un valore precedente resta nell'archivio. Tornando all'H controllare nuovamente l'opzione prima di ricalcolare.

La larghezza efficace della soletta beff è un dato assegnato. Va determinata esternamente per la sezione e la situazione considerate. Il foglio non ricostruisce dalla sola geometria trasversale tutte le luci equivalenti e le condizioni longitudinali necessarie. Le quote delle barre seguono la convenzione faccia asse mostrata dal controllo: non confonderle con il copriferro esterno della staffa. Nelle fasi della sezione la compressione è negativa e la trazione positiva; le reazioni assegnate per gli appoggi hanno invece l'etichetta specifica positiva a compressione.

### 8 2 Compilare la sezione con anima inclinata

Selezionare H con anima inclinata e compilare Scostamento anima al piede. Il valore è lo spostamento orizzontale del piede rispetto alla sommità: positivo verso destra, negativo verso sinistra. Non è un angolo in gradi. La piattabanda superiore è centrata sulla sommità dell'anima e quella inferiore sul piede. Le larghezze superiore e inferiore rimangono quelle delle rispettive piattabande.

Come esempio, impostare altezza libera 1800 mm, spessore anima 14 mm, scostamento +300 mm, piattabanda superiore 500 × 25 mm e inferiore 700 × 30 mm. Il disegno deve mostrare il piede spostato a destra, lunghezza della lamiera 1824,829 mm e inclinazione 9,462° dalla verticale. Lo spessore orizzontale equivalente è 14,193 mm; questo valore è un risultato, mentre nel campo Spessore anima devono rimanere 14 mm. Cambiando lo scostamento a −300 mm si ottiene la configurazione speculare.

![H con anima inclinata e scostamento positivo di 300 mm](../artefatti/guide_anthea_itec_rev03/interfaccia/sezione_anima_inclinata.png)

Il programma accetta inclinazioni fino a 45° dalla verticale, comprese quelle negative: il valore assoluto dello scostamento non deve superare l'altezza libera. Questo è il campo geometrico dell'implementazione, non una verifica di stabilità della trave. Se compare un errore, correggere il dato senza confondere altezza verticale e lunghezza inclinata.

### 8 3 Compilare il cassoncino

Selezionare Cassoncino. La carpenteria comprende due anime simmetriche, due piattabande superiori separate e un fondo; la soletta chiude superiormente la cella nella configurazione composta. Interasse anime in sommità indica la distanza fra gli assi delle anime sotto le piattabande superiori. Scostamento anima al piede indica il rientro di ciascuna anima: un valore positivo restringe il fondo, uno negativo lo allarga. L'interasse al piede è quello superiore meno due volte lo scostamento.

Larghezza superiore è la larghezza di ciascuna delle due piattabande. Larghezza inferiore 1 è la larghezza dell'intero fondo. Non inserire nella prima casella la somma delle due flange né nella seconda metà del fondo. Il numero di pioli per fila e i relativi dettagli si riferiscono a ciascuna piattabanda superiore; il modello ripartisce fra le due piattabande il flusso totale di connessione. Per i dati di fatica assegnare i flussi per piattabanda, senza dividere una seconda volta un valore già ripartito.

Per riprodurre l'esempio usare altezza libera 1800 mm, spessore anima 14 mm, interasse superiore 1800 mm, scostamento 250 mm, ciascuna piattabanda superiore 450 × 25 mm e fondo 1400 × 25 mm. L'interasse al piede è 1300 mm; le due anime sono lunghe 1817,278 mm e inclinate di 7,907°. Il disegno deve indicare due piattabande da 450 mm, non una sola piattabanda da 450 mm. Nel calcolo N–Mx la larghezza superiore complessiva è 900 mm.

![Cassoncino con due anime inclinate e due piattabande superiori](../artefatti/guide_anthea_itec_rev03/interfaccia/sezione_cassoncino.png)

Il fondo deve contenere gli appoggi delle due anime, considerate con il loro spessore orizzontale; le anime devono restare separate e le piattabande superiori non devono sovrapporsi. Nell'esempio il fondo interno netto è 1285,866 mm e ogni sbalzo esterno è 42,933 mm. Queste larghezze dipendono dallo spessore e dall'inclinazione: non coincidono esattamente con 1300 e 50 mm. Il programma respinge le geometrie incompatibili, ma la loro accettazione non certifica saldature o montaggio. Il comportamento torsionale si verifica soltanto attivando le opzioni di torsione del cassoncino descritte più avanti.

### 8 4 Leggere le proprietà e riconoscere i limiti

Nel pannello Proprietà della sezione distinguere Carpenteria asse orizzontale calcolo da Sezione reale nel piano Model. Il primo gruppo descrive il modello usato per N–Mx: area, quota del baricentro e inerzia rispetto all'asse orizzontale corrispondono alla carpenteria reale; le proprietà rispetto all'altro asse sono quelle della rappresentazione equivalente. Il secondo gruppo permette di consultare le proprietà piane della geometria effettiva, compresi prodotto d'inerzia e assi principali. Un valore Ixy nullo nel primo gruppo non dimostra che la carpenteria inclinata sia simmetrica.

| Controllo dell'esempio | H inclinata di 300 mm | Cassoncino con rientro 250 mm |
| --- | --- | --- |
| Area della sola carpenteria | 590,476 cm² | 1083,838 cm² |
| Quota del baricentro dalla sommità dell'acciaio | −1057,245 mm | −1030,239 mm |
| Inerzia orizzontale baricentrica | 3385733,876 cm⁴ | 6041896,480 cm⁴ |
| Anime e piattabande superiori | Una e una | Due e due |

I valori in tabella sono geometrici e lordi: non includono soletta, armature o riduzioni di efficacia. I risultati di fase possono essere differenti perché cambiano collaborazione, omogeneizzazione e parti efficaci. Ricordare che 1 cm² equivale a 100 mm² e 1 cm⁴ a 10000 mm⁴.

L'analisi assume flessione retta attorno all'asse orizzontale e un vincolo laterale fornito da soletta e controventi. Per l'H inclinata non risolve l'accoppiamento della flessione dovuto al prodotto d'inerzia della sola carpenteria. Occorre verificare separatamente che l'ipotesi sia rappresentativa, soprattutto durante getto e montaggio: selezionare una fase Solo acciaio non crea da sé un vincolo laterale reale.

Per il cassoncino torsione e distorsione della cella e diaframmi sono verificati soltanto se si attivano le verifiche a torsione. Restano esclusi gli irrigidimenti longitudinali del fondo: il fondo non viene verificato come piastra irrigidita longitudinalmente. L'H con anima inclinata resta in flessione retta e un ΔT salvato nelle sue fasi non viene considerato. La rappresentazione grafica chiusa e un esito positivo delle verifiche disponibili non coprono i fenomeni esclusi. Leggere Info modello, gli avvisi e la descrizione del tipo di sezione nel report prima di utilizzare i risultati.

Gli archivi precedenti privi del tipo di sezione vengono interpretati come H saldato. Dopo il cambio di tipologia salvare con un nome riconoscibile, riaprire il foglio e controllare tipo, scostamento e interasse. Il report delle nuove sezioni distingue le dimensioni equivalenti impiegate nel calcolo dalle lamiere reali; confrontare entrambe le tabelle con il disegno.

### 8 5 Scegliere consapevolmente il metodo

| Metodo | Impiego operativo | Aspetto da controllare |
| --- | --- | --- |
| Cumulativo | Somma di contributi elastici delle fasi sulla geometria efficace comune | Non rappresenta una memoria costitutiva cronologica |
| Storico lineare | Successione di fasi con deformazioni e riferimento al getto | φ o n del nuovo incremento non rilassa automaticamente tutto il passato |
| Storico non lineare | Evoluzione a fibre con plasticità dell'acciaio e scarico elastico | Sezione lorda e caratteristiche istantanee; niente riduzione locale di classe 4 |

La scelta va fatta prima di interpretare le differenze fra due risultati. Uno storico non lineare non è semplicemente un cumulativo «più preciso» per ogni scopo: introduce fenomeni diversi e non include tutti i controlli locali del cumulativo. I controlli di taglio, connessione e accessori non vengono valutati dai due metodi storici.

### 8 6 Costruire le fasi

Aprire la scheda fasi e tensioni e inserire gli incrementi in ordine. Un percorso comune comprende peso della carpenteria e getto sulla sezione di acciaio, permanenti successivi sulla sezione composta a lungo termine e variabili sulla sezione composta a breve termine. Il primo carico applicato prima della maturazione non deve beneficiare della soletta che ancora non collabora.

Ogni riga rappresenta un incremento, non necessariamente il totale della situazione. Inserire un totale in ciascuna riga può duplicare i carichi. Assegnare φ oppure n, controllando che il valore di uno derivi coerentemente dall'altro. Scegliere il punto di applicazione di N: baricentro lordo della fase, baricentro efficace aggiornato o riferimento comune. Il momento riportato al riferimento cambia con questa scelta.

Le azioni di SLU devono arrivare già combinate e fattorizzate. Il selettore SLU o SLE modifica i limiti di controllo, non moltiplica automaticamente le azioni per tutti i coefficienti delle combinazioni. Anche una variazione di segno di V nelle fasi si somma algebricamente nella domanda di connessione: verificare il significato delle situazioni cumulate.

Per il ritiro assegnare la deformazione con il segno corretto: un accorciamento è negativo. Se l'unità è microdeformazione, −250 corrisponde a −0,25 per mille. Il modulo tratta l'effetto locale di sezione. Le azioni dovute a vincoli longitudinali dell'intero ponte devono provenire da un modello globale.

### 8 7 Leggere efficacia tensioni e controlli locali

Selezionare la situazione da visualizzare e distinguere tensioni della fase, contributi precedenti e stato cumulato. Le parti inefficaci dell'acciaio evidenziano la riduzione per instabilità locale sotto tensioni normali. L'anima resistente a taglio non viene automaticamente ridotta nello stesso modo. Leggere area, baricentro, inerzia, coefficienti di omogeneizzazione e residui di convergenza.

Nel metodo cumulativo aprire i controlli di taglio, irrigidimenti, appoggi e pioli. Inserire geometrie reali, pannelli, reazioni, eccentricità, saldature, passi e materiali. Un irrigidimento non idoneo non deve produrre il beneficio di un pannello corto. La connessione richiede anche dettagli costruttivi e, se pertinente, dati di fatica; un rapporto di resistenza favorevole del singolo piolo non esaurisce questi controlli.

La colorazione principale della sezione riguarda le tensioni normali rapportate ai limiti. Non è una mappa completa di instabilità, fatica, torsione o sollevamento della soletta. Per conoscere lo stato di quelle verifiche occorre leggere le rispettive tabelle e gli avvisi.

Con anime inclinate il taglio V inserito nelle fasi rimane il taglio verticale totale della sezione. Non trasformarlo preventivamente nel taglio della singola lamiera: il programma applica V/cos α per l'H inclinata e V/(2 cos α) per ciascuna anima del cassoncino. Con V = 600 kN negli esempi precedenti le domande nel piano delle lamiere sono rispettivamente 608,276 kN e 302,880 kN per anima. Il risultato di resistenza globale viene riportato alla componente verticale totale.

### 8 8 Torsione distorsione e diaframmi del cassoncino

Con Cassoncino selezionato, in fondo al Pannello di controllo compare il riquadro Cassoncino · torsione, distorsione e diaframmi. Attivare Verifiche a torsione del cassoncino: nella tabella Sollecitazioni diventa modificabile la colonna ΔT [kNm] e ogni scheda di fase mostra Momento torcente T. ΔT è l'incremento del momento torcente della fase nella sezione, ricavato dal modello globale e già combinato come N, Mx e V. Il ritiro non ha momento torcente. Per H saldato e H con anima inclinata la colonna non compare e un valore rimasto nell'archivio non partecipa al calcolo.

Controvento superiore · spessore equivalente t* chiude la cella del cassone di acciaio nelle fasi Solo acciaio: inserire lo spessore della lamiera equivalente al controvento orizzontale posto fra le piattabande superiori. Con t* = 0 il cassone è aperto e la torsione delle fasi di solo acciaio resta da completare, perché la torsione non uniforme della sezione aperta non viene calcolata. Nelle fasi composte la cella è chiusa dalla soletta e t* non interviene. Il flusso nel controvento viene riportato, ma le sue aste vanno verificate a parte.

Per la distorsione assegnare la luce della campata e, se presenti, il passo dei diaframmi intermedi e il loro tipo: Piastra con il suo spessore, oppure Controvento a X con area, raggio d'inerzia minimo e rapporto Lcr/L di una diagonale. Il torcente distribuito m_t e il torcente concentrato T_c derivano dai carichi eccentrici della combinazione esaminata, per esempio corsie caricate da un solo lato. Il programma applica m_t su tutta la luce e T_c nella posizione più sfavorevole, con lo stesso segno. Con luce nulla la distorsione non viene analizzata e resta da completare.

All'appoggio assegnare il torcente trasferito agli apparecchi, l'interasse trasversale degli apparecchi e lo spessore del diaframma d'appoggio. Come la reazione R, questi dati sono inviluppi indipendenti dalle fasi. Quando la verifica dell'appoggio è attiva, la coppia T/e_b si somma a metà della reazione sull'irrigidimento dell'anima più caricata.

![Riquadro delle verifiche a torsione del cassoncino](../artefatti/guide_anthea_itec_rev04/interfaccia/cassoncino_torsione_ingressi.png)

Per riprodurre l'esempio della guida teorica usare il cassoncino descritto sopra con le tre fasi iniziali del foglio, t* = 4 mm e ΔT pari a 200, 300 e 1000 kNm. Nella scheda Verifiche il gruppo Cassoncino · torsione, distorsione e diaframmi riporta per la fase di solo acciaio A0 = 2,829 m² e q = 35,351 kN/m; per le due fasi composte A0 = 3,079 m² e q = 48,712 e 162,372 kN/m. Il flusso cumulato nelle anime e nel fondo è 246,435 kN/m, quello della soletta 211,083 kN/m. Le tensioni tangenziali di torsione sono 17,602 MPa nelle anime da 14 mm e 9,857 MPa nel fondo da 25 mm.

![Flussi di torsione per fase nella scheda Verifiche](../artefatti/guide_anthea_itec_rev04/interfaccia/cassoncino_torsione_risultati.png)

Aggiungendo luce 40000 mm, diaframmi a piastra da 12 mm ogni 5000 mm, m_t = 60 kNm/m e T_c = 600 kNm, il programma individua 7 diaframmi intermedi e calcola al fondo σdw = 14,946 MPa, pari al 20,2% della tensione di flessione: la tensione di distorsione viene quindi sommata nelle verifiche del fondo. Il diaframma intermedio più sollecitato ha τ = 8,394 MPa.

Nella tabella Verifiche la riga Anima · resistenza a taglio comprende il flusso torsionale sull'anima più caricata; le righe Fondo, Soletta, Distorsione e Diaframma riportano i controlli specifici. Restano da completare i controlli privi di dati, per esempio il taglio da torsione nella soletta senza armatura trasversale attiva o la torsione del cassone aperto. I metodi con storico non eseguono queste verifiche e lo segnalano negli avvisi.

### 8 9 Curve di risposta

La terza scheda permette curve M κ a N fissato oppure N ε a curvatura fissata. Si può partire da uno stato vergine oppure dalla storia di una fase ricostruita dal motore non lineare. Quando si parte da una fase caricata, il ramo di risposta conserva la memoria prevista dal modello e non riparte da zero tensioni.

Controllare la variabile mantenuta costante, direzione dei passi, grandezza degli incrementi e criterio di arresto. Una curva interrotta per mancata convergenza non va completata idealmente a mano per dichiarare una resistenza. Le curve sono caratteristiche e su sezione lorda; non costituiscono da sole una capacità di progetto con instabilità locale inclusa.

La vista separata segue la fase selezionata. F11 la porta a schermo intero ed Esc esce da quella modalità. CSV ed esportazioni delle fibre consentono un controllo numerico dei dati; la precisione del file non è limitata ai decimali mostrati a video.

## 9 Bridge Design

### 9 1 Scopo e struttura della finestra

Bridge Design serve a esplorare rapidamente configurazioni di ponte e confrontarne geometria, quantità, costo, CO₂ e durata indicativa. Riprende il percorso del riferimento thebridgeeng.com/design, con grafica ANTHEA e un motore autonomo. Non replica il calcolo dettagliato AASHTO del sito e non produce una verifica normativa completa. Le formule implementate sono documentate nella guida teorica.

La parte di input contiene quattro schede: Sito, Campate, Sezione e Pile. La vista alterna prospetto e sezione trasversale. Il riepilogo presenta costo, emissioni, altezza dell'impalcato e durata. Le tabelle inferiori consentono di leggere quantità, dettagli, confronto A/B, prezzi e ipotesi. Su finestre strette la vista passa sotto gli input: scorrere verso il basso per raggiungerla.

![Finestra Bridge Design con impostazione ANTHEA](../artefatti/bridge_design/interfaccia_v2/bridge_design_1600.png)

### 9 2 Impostare il sito e le campate

In Sito scegliere ostacolo, terreno convenzionale, lunghezza totale, quota sul terreno, larghezza dell'ostacolo e componenti della carreggiata. La larghezza del ponte deriva da corsie, banchine, spartitraffico e barriere. Il numero di corsie è complessivo, non il numero per direzione. La configurazione iniziale di quattro corsie da 3,65 m, due banchine da 1,50 m, spartitraffico da 1,60 m e due barriere da 0,50 m produce 20,20 m.

In Campate assegnare il numero oppure lasciare l'automatismo, scegliere continuità e appoggi estremi. Con più di due campate continue il criterio iniziale accorcia le campate di estremità rispetto alle interne. Il motore tenta di tenere le pile fuori dall'ostacolo centrale; quando non riesce, lo segnala. Il disegno non costituisce verifica di franco idraulico, franco stradale o interferenza con un rilievo reale.

### 9 3 Scegliere famiglia e dimensioni

Sono disponibili soletta piena, travi a T in c.a., travi a I e U in c.a.p., cassone in c.a.p., cassone a conci ad altezza variabile, travi a I e cassoni acciaio cls. Selezionando la famiglia compaiono i parametri pertinenti. Un campo inattivo per una famiglia non deve essere interpretato come una dimensione comunque utilizzata.

Per altezza, soletta, interassi e altre dimensioni indicate, zero mantiene la scelta automatica. Inserire un valore positivo blocca quella dimensione: se in seguito aumenta la luce, non viene automaticamente aumentata. Il pulsante Sezione automatica ripristina gli automatismi previsti, compresi quelli delle dimensioni principali di pile e fondazioni; controllare quindi anche la quarta scheda dopo averlo usato.

La sezione disegnata è schematica e non in scala rigorosa. Serve a riconoscere la famiglia, il numero degli elementi e le proporzioni generali. I valori numerici e il dettaglio delle quantità sono il riferimento. Il cambio del paesaggio modifica il solo scenario grafico e non cambia i risultati.

### 9 4 Pile fondazioni e materiali

Scegliere telaio a colonne, colonna circolare, setto o testa a martello. Assegnare fondazione automatica, diretta o pali da 1,0 o 1,5 m. Lunghezza e numero di pali, dimensione della pila e lato della fondazione possono restare automatici oppure essere imposti. Il lato indicato per il plinto è longitudinale; la larghezza trasversale può aumentare per accogliere spalla, setto o telaio e viene riportata nel dettaglio.

La classe di terreno attiva pressioni e resistenze convenzionali. È possibile sostituirle nelle Ipotesi, ma non vengono ricavate da una stratigrafia geotecnica del progetto. La scelta «Roccia» non sostituisce un'indagine né considera automaticamente fratturazione e ammasso. Se il rapporto assiale di fondazione è elevato, modificare la configurazione e passare successivamente a una verifica specifica.

La resistenza del cls dell'impalcato interviene nella stima del modulo elastico. Quella delle sottostrutture permette di leggere il rapporto fra compressione media delle pile e fc. Nessuno dei due campi dimensiona automaticamente tutta l'armatura o verifica pressoflessione e duttilità. Le quantità di armatura sono ottenute da incidenze configurabili.

### 9 5 Personalizzare il listino

Aprire Prezzi e sostituire le voci in euro con il proprio listino. Sono distinti cls impalcato, cls sottostrutture, armatura, precompressione, carpenteria, cassoni metallici, casseforme, pali, appoggi, giunti, barriere e pavimentazione. I pali sono quotati al metro con calcestruzzo e perforazione inclusi, mentre l'armatura è conteggiata separatamente. Se il proprio prezzo comprende anche l'armatura, occorre separare le voci per evitare una duplicazione.

Le Ipotesi contengono oneri di cantiere, imprevisti e intervallo di costo. Oneri e imprevisti vengono applicati in successione: 12% e 15% producono un moltiplicatore 1,12 × 1,15 = 1,288, non 1,27. Il valore è IVA esclusa. L'intervallo ±30% iniziale è un margine indicativo configurabile, non un intervallo statistico di confidenza.

Modificare un prezzo cambia il costo, lasciando invariate geometria, azioni e CO₂. Modificare un'incidenza di armatura cambia quantità, costo ed emissioni. Questo è un controllo pratico utile per distinguere le due famiglie di input.

### 9 6 CO₂ alternative e confronto A B

I coefficienti ambientali sono editabili e inizialmente indicativi. La voce CO₂ comprende i materiali strutturali conteggiati e una maggiorazione per trasporti e cantiere. Non comprende un ciclo di vita completo né tutte le finiture. L'opzione cls a ridotta CO₂ riduce il fattore del calcestruzzo; acciaio riciclato agisce sul fattore della carpenteria. Non attribuisce automaticamente a tutte le armature lo stesso beneficio.

«Fissa A» conserva una copia dell'alternativa corrente, inclusi prezzi e ipotesi. Modificare poi B e aprire il confronto: leggere differenze assolute e relative insieme alle configurazioni. Se si desidera confrontare soltanto due geometrie, mantenere lo stesso listino nei due stati. Se il listino cambia dopo aver fissato A, il confronto contiene anche quell'effetto.

Le proposte di riduzione della CO₂ mostrano differenze ricalcolate per alcune modifiche semplici. Applicarle richiede comunque di controllare le nuove campate e gli avvisi. «Ponte casuale» è utile per esplorare il funzionamento, ma sostituisce la configurazione geometrica corrente: fissare A o salvare prima se si vuole conservarla. Annulla consente di tornare agli stati precedenti della sessione.

### 9 7 Risultati ed esportazioni

Nel dettaglio leggere carichi per metro dell'intero impalcato, momenti, freccia indicativa, reazioni e fondazioni. I momenti non sono quelli della singola trave e derivano da carico uniforme su tutte le campate. Non comprendono un inviluppo di veicoli in movimento. La freccia usa rigidezza lorda e non include tutte le correzioni di esercizio di un ponte reale.

Il CSV contiene quantità e prezzi; il PNG conserva la vista; il comando di stampa produce una presentazione della scheda. Il report Word raccoglie configurazione, viste, quantità, ipotesi e risultati. Salva conserva il foglio riapribile con il confronto A/B. Per poter cambiare prezzi in seguito conservare l'archivio, non soltanto il report Word.

## 10 Salvataggio e report

### 10 1 Conservare dati modificabili

Usare il formato proposto dal modulo per il foglio singolo e .programma per il progetto. I formati dei fogli possono variare per modulo; Bridge Design usa l'archivio .anthea. Gli archivi mantengono input e impostazioni necessarie, mentre CSV, PNG e Word sono esportazioni destinate a lettura, controllo o presentazione.

Utilizzare Salva con nome prima di una variante che si desidera mantenere indipendente. Per la storia interna di un progetto usare le revisioni. I file di progetto con revisioni utilizzano un formato aggiornato e richiedono una versione di ANTHEA compatibile; non risalvarli con un eseguibile precedente senza averne verificato il supporto.

### 10 2 Report del singolo foglio e del progetto

Il report di un foglio riguarda i dati e i risultati di quel modulo. «Genera report» su progetto o sezione comprende invece i fogli diretti e tutte le sottosezioni, nell'ordine dell'albero. Include indice navigabile, dati comuni, dati specifici, risultati e immagini. I riferimenti degli antenati necessari vengono richiamati anche esportando un solo ramo.

La generazione ricalcola i fogli su copie dei dati. Le schede incomplete restano nel documento con segnalazione; non vengono trasformate in verifiche favorevoli. In presenza di conflitti si può aprire il confronto oppure proseguire conservando le segnalazioni. Prima della consegna leggere sempre le pagine degli avvisi e verificare il ramo e la revisione indicati nel titolo.

Un report storico ricalcola i dati della revisione selezionata con il motore della versione in uso. Per la riproducibilità conservare quindi anche versione dell'applicazione, data del report e archivio originario. Lo snapshot dei dati protegge gli input; non contiene una copia autonoma di ogni vecchio motore di calcolo.

## 11 Percorso completo per un primo progetto

1. Avviare ANTHEA, creare un progetto e assegnargli un nome riconoscibile. Salvare subito l'archivio in una cartella di commessa.
2. Definire i materiali comuni e organizzare i rami prima di moltiplicare i fogli. Controllare il significato della condivisione ai livelli scelti.
3. Per un ponte preliminare aprire Bridge Design, impostare sito, larghezza e famiglia, quindi sostituire il listino. Fissare A e costruire una seconda configurazione.
4. Leggere gli avvisi, le campate effettive e il dettaglio delle quantità. Salvare la soluzione prescelta con una nota che descriva le ipotesi ancora da consolidare.
5. Creare i fogli di verifica locale necessari. Per la sezione composta assegnare geometria e azioni locali da analisi; per le fondazioni inserire indagini e dati geotecnici effettivi. Non trasferire indiscriminatamente i carichi uniformi di Bridge Design.
6. Esaminare risultati, convergenza e verifiche mancanti. Fare almeno un controllo manuale di unità ed equilibrio per ciascun tipo di modello.
7. Risolvere i conflitti dei dati comuni, creare una revisione e generare il report del ramo o del progetto. Conservare insieme archivio, report e identificazione della versione.

Questo percorso può essere abbreviato per un controllo isolato: il progetto non è obbligatorio per usare un singolo foglio. Diventa utile quando più calcoli devono restare coerenti e quando si vogliono confrontare modifiche nel tempo.

## 12 Problemi frequenti e controlli finali

| Situazione | Interpretazione e intervento |
| --- | --- |
| Risultati assenti dopo una modifica | Completare il campo segnalato e verificare intervalli e selezioni obbligatorie |
| Risultati marcati DA AGGIORNARE | Attendere o avviare il ricalcolo previsto; non riutilizzare il valore precedente |
| Valori differiscono di 1000 | Controllare MPa e kPa, N e kN, mm e m, momenti in Nmm e kNm |
| Modifica locale genera un conflitto | Individuare il riferimento superiore che governa quella proprietà |
| Un parametro non cambia il risultato atteso | Controllare famiglia, metodo attivo, automatismi e campo di applicazione |
| File riaperto senza alcuni comandi | Controllare versione dell'eseguibile e compatibilità dell'archivio |
| Fessurazione o verifica locale non disponibile | Leggere il motivo specifico; la soluzione tensionale non garantisce la disponibilità di ogni controllo |
| Ponte con costo inatteso | Controllare larghezza totale, numero di campate, fondazioni, voci del listino e maggiorazioni |
| Curva non lineare interrotta | Controllare convergenza, incremento, stato iniziale e limite del materiale |

Prima di usare un risultato, verificare che il nome del foglio e la revisione siano corretti, che il disegno corrisponda ai dati, che azioni e segni siano coerenti, che i coefficienti siano quelli voluti e che gli avvisi siano stati letti. Prima di consegnare un report, riaprirlo e controllare titoli, unità, immagini e presenza delle conclusioni realmente supportate dal modello.

### Riferimenti per approfondire

La Guida teorica di ANTHEA descrive i calcoli della stessa edizione. La documentazione tecnica del repository in supporto/docs contiene approfondimenti su sezioni CA, metodi della sezione composta, taglio e connessione, pali orizzontali, progetti e revisioni. Il file bridge-design.md descrive specificamente il nuovo motore di predimensionamento e le prove eseguite. I riferimenti normativi vanno letti nelle edizioni identificate dal modulo, insieme alle relative condizioni di applicabilità.

## 13 Muri di sostegno con stratigrafie di monte e valle

La revisione 07 mantiene l’allineamento del modulo muri a mensola e a gravità al percorso guidato della stabilità globale, mantenendo le sole schede Input e Verifiche. La guida specifica muri-sostegno.md e il PDF omonimo in supporto/docs descrivono input, formule e limiti. I documenti vanno usati insieme per controllare il significato dei risultati.

Le stratigrafie di monte e valle sono affiancate, indipendenti o collegate per spessori e proprietà; le profondità partono dalle rispettive superfici. Hlib è la distanza dalla sommità al terreno di valle: Dv=H+t−Hlib. Il terreno davanti al muro entra nei pesi, nei momenti, nelle sollecitazioni della mensola e nel ricoprimento efficace della portanza. La passiva richiede attivazione e frazione mobilitata; è esclusa nel sisma. Gli attriti del muro e della fondazione sono assegnabili oppure ricavati da φcv,k e tipo di interfaccia. Il valore a volume costante va caratterizzato, senza sostituirlo automaticamente con quello di picco.

Valori di calcolo consente di interrogare e modificare gli input e leggere coefficienti effettivi per combinazione, pesi, attriti, pressioni e sollecitazioni. Le forze risultanti dipendono dagli input e non si possono forzare. Le combinazioni sono modificabili: il preset locale è A1+M1+R3, quello globale A2+M2+R2. Azioni eccezionali e sisma Mononobe–Okabe o Wood semplificato sono espliciti. Il terreno del lato selezionato può essere trasferito ai moduli dei pali; la sezione selezionata può essere inviata al modulo c.a.

La stabilità globale Bishop ha un motore separato e un proprio profilo esteso, anche con due colonne profonde e confine verticale assegnato. Il pulsante Stabilità globale apre il percorso; al primo accesso a un profilo vuoto ne prepara i dati e attiva la verifica. Gli strati si inseriscono per spessore, con quota del fondo calcolata automaticamente. La precompilazione non prolunga le indagini: rilievo, terreni profondi e falda del sito vanno controllati e confermati. Il disegno rappresenta anche il terreno sotto il piano di posa. Parametri, dominio e combinazioni restano interrogabili e modificabili.

Le relazioni Word includono le due stratigrafie e i coefficienti utilizzati. Le guide e gli esempi conservati per l’utente hanno anche PDF verificati graficamente. Cedimenti e spostamenti sono disponibili nei modelli separati descritti in Rev07; verifiche idrauliche, liquefazione e completamento esecutivo richiedono analisi dedicate: il modulo non emette una verifica complessiva dell’opera. I controlli delle due colonne sono documentati in supporto/artefatti/muri-due-colonne-20260930/CONTROLLO.pdf; quelli del percorso guidato, in supporto/artefatti/globale-guidata-20260930/CONTROLLO.pdf. La guida illustrata stabilita-globale-guida-rapida.pdf, in supporto/docs, contiene un modello stratificato salvato e i passi per riprodurre gli esiti. I confronti MAX precedenti restano parziali e non costituiscono validazione delle nuove opzioni.


### 13 1 Avviare la verifica globale

La verifica globale riguarda il possibile scivolamento del muro insieme al terreno sottostante. I soli parametri del terreno di fondazione utilizzati per la portanza non definiscono la stratigrafia necessaria per questa analisi.

1. Premere Stabilità globale nella barra superiore. Se il profilo globale è vuoto, il programma copia i dati locali disponibili, propone superfici orizzontali sui due lati e attiva la verifica. Riaprire il pannello conserva il lavoro già impostato. Prepara dal muro sostituisce invece profilo e strati con una nuova proposta: usarlo solo quando si vuole ripartire dai dati locali.

2. Controllare gli strati di monte e valle, anche sotto la fondazione. Selezionare una riga per completare le proprietà; correggere il rilievo e la linea di falda se diversi dalla proposta. Controllare anche la condizione Drenata o Non drenata e l’opzione Includi il sisma SLV. Il messaggio in alto indica il primo dato mancante; il suggerimento sul messaggio elenca gli altri.

3. Dopo il controllo del sito, spuntare Ho controllato profilo, strati e falda del sito e premere Calcola globale. Il risultato si apre in Verifiche, vista Stabilità globale. Le modifiche geotecniche del percorso annullano la conferma e il risultato precedente. Calcola globale esegue la sola analisi globale anche quando le verifiche locali hanno input fuori campo.

### 13 2 Inserire spessori e proprietà dei terreni

La tabella globale mostra Terreno, Spessore e Fondo y. Gli strati vanno dall’alto verso il basso e ogni colonna parte dalla propria superficie presso il muro. Il fondo è una quota calcolata, non un secondo spessore da inserire. L’origine (0;0) è al bordo di valle del piano di posa: x cresce verso monte e y verso l’alto. Fondo y=−2 m significa 2 m sotto la fondazione.

Esempio: H=3 m, t=0,45 m, terreno di valle al piano di posa. A monte si può descrivere un riempimento fino a y=0, seguito da due terreni profondi. A valle non occorre ripetere il riempimento che si trova soltanto sopra la fondazione sul lato di monte.

| Colonna | Terreno | Spessore in m | Fondo y in m |
| --- | --- | --- | --- |
| Monte | Riempimento | 3,45 | 0 |
| Monte | Alluvioni | 2 | −2 |
| Monte | Ghiaia | 8 | −10 |
| Valle | Alluvioni | 2 | −2 |
| Valle | Ghiaia | 8 | −10 |

Selezionando una riga compaiono γ e γsat in kN/m³ e le resistenze caratteristiche: φ′k in gradi e c′k in kPa per Drenata; cu,k in kPa per Non drenata. Un nuovo strato ha proprietà da completare. Stesso nome del terreno significa stesso colore nelle due colonne e nel disegno, ma non collega automaticamente i valori.

![Profilo dell’esempio stratificato con terreno sotto la fondazione e limiti di ricerca evidenziati in blu.](../artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato-profilo.png)

La vista principale del muro rappresenta il terreno sotto il piano di posa. Prima di definire la globale mostra il terreno di fondazione con spessore grafico indicativo; con il modello globale attivo usa gli strati profondi. La vista globale completa la lettura nell’area di ricerca e permette di interrogare i dati. Una porzione disegnata non estende la profondità conosciuta delle indagini.

### 13 3 Controllare rilievo falda sisma e ricerca

Rilievo e falda si modificano nel gruppo dedicato tramite punti x, y. La falda è una linea di quote y assolute rispetto al piano di posa, non una profondità dalla superficie. Il modello proposto va adattato al rilievo reale: non ricava automaticamente pendenze o terreni profondi.

L’attivazione del sisma globale viene ripresa dal muro durante la precompilazione. Il gruppo Falda e sisma espone l’opzione e la sorgente dei coefficienti. Da sito usa i dati del progetto con βs=0,38; in alternativa si assegnano kh e il modulo di kv. Il coefficiente globale non va confuso con quello delle spinte del muro. Se i dati del sito non sono disponibili, occorre completare la sorgente scelta; non viene assunto un valore sismico implicito.

Con Area di ricerca Automatica i limiti orizzontali seguono il rilievo e la profondità si adatta agli strati noti di entrambi i lati, con massimo iniziale pari a 2(H+t). La proposta non dimostra che l’estensione sia sufficiente. Per cambiare i limiti scegliere Assegnata e aprire Limiti e precisione; nodi, conci e raffinamenti sono sempre modificabili. Gli archivi precedenti conservano i limiti già salvati come Assegnati.

### 13 4 Leggere il risultato e riprodurre l’esempio

Controllare l’Esito di tutte le combinazioni. F è il fattore trovato con i parametri di progetto; il tasso di lavoro è η=γR/F. Il caso iniziale è quello con tasso maggiore, non necessariamente quello con F minore se γR cambia; un caso privo di superficie valida ha priorità. La tabella e il cerchio restano interrogabili per ogni combinazione.

Soddisfatta nel dominio esplorato richiede η≤1 e controlli della ricerca superati. Minimo sul bordo richiede di ampliare l’area, sempre entro rilievo e indagini disponibili. Ricerca incompleta o discretizzazione non convergente impediscono una conclusione favorevole anche con η≤1. Non modificare arbitrariamente i parametri del terreno per ottenere un esito favorevole.

La scala comune è 0–0,50 blu, 0,50–0,70 verde, 0,70–0,90 giallo, 0,90–1,00 arancio, oltre 1,00 rosso; grigio per controlli incompleti. Nel disegno le linee verticali individuano i conci della superficie critica. La selezione nella tabella permette di leggere pesi, pressioni interstiziali, parametri ridotti, resistenze e azioni.

Per riprodurre il caso illustrato aprire supporto/artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato.anthea e premere Calcola globale, senza ripreparare i dati locali. La guida rapida contiene proprietà, dominio e risultati completi. Il secondo caso statico ha F=1,075737 e η=1,022555: l’esito non soddisfatto è conservato nell’esempio. È un controllo interno ANTHEA; non è un nuovo confronto numerico MAX.


## Portanza sismica cedimenti spostamenti e armature Rev07

La revisione 07 aggiunge i calcoli dei punti 2, 3 e 5 nel campo dichiarato: inerzia del terreno nella portanza sismica; cedimenti e spostamenti; dettagli e predimensionamento delle armature e modelli strutturali per gravità. Rimangono le due schede Input e Verifiche. Il report Word contiene input, ipotesi, coefficienti, risultati e distinta delle barre; una quinta figura mostra le armature della sezione.

### Portanza sismica

In Terreno aprire Portanza sismica. Con Da sito si usa ah/g=ag/g·Ss·St, prima della riduzione β del muro; av/g=±0,5ah/g. In alternativa assegnare entrambe le accelerazioni. Il fattore γRD è modificabile: 1 per sabbia medio densa, 1,15 per sabbia sciolta asciutta. Non è un valore ricavato automaticamente dal solo angolo di attrito.

Si applica EN 1998-5:2004 allegato F alla fondazione nastriforme su terreno granulare asciutto, omogeneo e con base ruvida. Nmax=0,5γ(1−av/g)B²Nγ, con Nγ=2(Nq−1)tanφd. Si trascura il contributo favorevole del ricoprimento. N, V e M sono normalizzati con γRD·γR; F=γRD·ah/(g tanφd). Il γR della combinazione è applicato separatamente e dichiarato nella relazione.

Il dominio usa a=c=0,92; b=d=1,25; e=0,41; f=0,32; m=0,96; k=1; k′=0,39; cT=1,14; cM=c′M=1,01; β=2,90; γ=2,80. La somma dei termini di interazione deve essere ≤1, con 0<N̄<(1−0,96F)^0,39. La capacità è cercata lungo il raggio N,V,M: il tasso η è l’inverso del moltiplicatore limite, non il valore della funzione di interazione. Non si applicano una seconda volta larghezza efficace e fattori di inclinazione.

In Verifiche scegliere una combinazione SISMA e Portanza sismica nel riepilogo. Sono leggibili Nmax, F, N̄, V̄, M̄, limite verticale, interazione, tasso ed esito. Un’accelerazione mancante, un terreno fuori campo o una risultante non ammissibile restano esplicitamente non verificati.

### Cedimenti e spostamenti di esercizio

In Terreno attivare Calcola cedimenti finali e inserire, a partire dal piano di posa, nome, spessore e modulo edometrico M di ciascuno strato. M è espresso in kPa: per esempio 30 MPa corrispondono a 30000 kPa. Non viene dedotto da φ o riempito con un valore presunto. La pressione del terreno rimosso è il carico geostatico eliminato con lo scavo, da valutare nel modello scelto; zero è una scelta esplicita.

Si integra s=∫Δσz/M dz con tensioni Boussinesq di una striscia infinita e pressione di contatto lineare. Il calcolo è ripetuto a valle, al centro e a monte. La profondità deve arrivare a Δσz≤10% del carico netto oppure a un substrato rigido documentato. Viene controllata anche la convergenza numerica. Profili insufficienti non producono un esito favorevole né uno spostamento totale valido.

È un cedimento finale con moduli costanti assegnati: non ricostruisce tempi di consolidazione, OCR, scarico e ricarico, variazione di M con le tensioni o degrado ciclico. La rotazione θ=(smonte−svalle)/B deriva dal profilo libero; non è una soluzione accoppiata della fondazione rigida.

Per Calcola spostamenti in testa servono anche la rigidezza orizzontale di fondazione K per metro di muro, in kN/m², e il limite scelto. Il fusto in c.a. usa curvature delle sezioni fessurate GPC con viscosità assegnata. La doppia integrazione fornisce u del fusto con base fissa; la stima disaccoppiata totale è utesta=ufusto+H/K−θHmuro. Il termine di rotazione conserva il segno. Le curvature mancanti impediscono il risultato. La gravità usa il modello elastico del materiale nel campo senza trazione.

Limiti iniziali modificabili: 25 mm per cedimento, 0,002 rad per rotazione e 20 mm per spostamento in testa. Sono valori di avvio da valutare per l’opera, non limiti normativi universali. In Verifiche scegliere Cedimenti e spostamenti per la tabella per combinazione, i contributi degli strati e le curvature.

### Spostamenti permanenti Newmark

In Azioni aprire Spostamenti permanenti e aggiungere una storia. Scegliere SLD o SLV, inserire ky/g, fattore di scala e limite di spostamento. ky/g è la soglia di inizio scorrimento del muro, da ricavare da un’analisi di equilibrio: non coincide con ag/g e non viene dedotta automaticamente dal coefficiente kh.

Importare un CSV a due colonne separate da punto e virgola: tempo in secondi e accelerazione verso valle in g. È ammessa una prima riga t;a_g e il separatore decimale italiano. I tempi devono essere crescenti. Confermare che storia e scala siano compatibili con sito e stato limite. I campioni restano salvati nel file del muro.

Il blocco rigido scorre in una sola direzione. L’integrazione dei tratti lineari di a(t)−ky·g tiene conto degli attraversamenti della soglia, dell’arresto e della coda finale a terreno fermo. Wood è escluso perché presuppone un muro vincolato. Il risultato riguarda ciascuna storia; la scelta e la conformità normativa dell’insieme degli accelerogrammi devono essere documentate. Lo SLD non viene ricavato dal solo ag/g SLV.

### Armature e comando Calcola armature

In Geometria si possono mantenere le facce simmetriche o assegnare due armature indipendenti. La prima faccia è monte nel fusto e inferiore nelle solette; la seconda è valle nel fusto e superiore nelle solette. Rimangono disponibili le due zone verticali separate da h₁.

Ogni zona contiene barre principali, diametro e passo delle secondarie, lunghezza di ancoraggio, sovrapposizione e mandrino. Zero nelle lunghezze significa calcolo automatico, non lunghezza nulla. Il pannello dei dettagli espone aggregato, aderenza, vita nominale, tolleranza del copriferro e collegamenti della giunzione.

Calcola armature cerca diametri e numeri interi di barre entro i limiti impostati. Ogni candidato viene controllato con GPC a N–M, a taglio e in SLE; la proposta usa armature simmetriche per zona, poi modificabili. L’area stimata dalla flessione serve soltanto a scartare candidati impossibili. La verifica finale include i dettagli: una sezione resistente può avere una piega o una giunzione che non entra. In tal caso l’esito lo segnala e può occorrere aumentare lo spessore. La ricerca è interrompibile. Premere Applica proposta per sostituire le barre inserite; prima di applicare restano conservate.

Il predimensionamento usa ancoraggi a fyd e nessuna riduzione favorevole dei coefficienti di forma o confinamento. fbd deriva dalle proprietà GPC e dalle condizioni di aderenza. Le giunzioni sono alla stessa quota, quindi lo schema richiede il 100% delle barre giuntate e numeri compatibili nelle due zone. Si controllano lunghezza comune, interferro tra coppie, ingombro, area e passo dei collegamenti. Il mandrino considera anche la pressione nel calcestruzzo all’interno della piega.

In Vista dei risultati scegliere Armature: si vedono i percorsi delle barre, le pieghe, la fascia di sovrapposizione e le marche. Le barre giuntate sono affiancate lungo lo sviluppo del muro; le proiezioni sono leggermente distanziate sul disegno per leggibilità. Dettagli armature riporta la distinta, fbd, lunghezze richieste e usate, mandrini, quantità e tutti i controlli. I pesi sono stime per metro comprensive di ancoraggi, giunzioni e secondarie. Restano da definire il disegno esecutivo, i giunti di costruzione, i bordi lungo il muro, le interferenze tridimensionali e gli sfridi: la vista non è una distinta di officina.

### Gravità in calcestruzzo o muratura

In Materiali scegliere Calcestruzzo non armato oppure Muratura. Il primo usa fck e proprietà GPC, con compressione e taglio NTC 4.1.11 e fct1d=0,85 fctk,0.05/γc. Per muratura occorrono fk, fvk0, limite caratteristico a taglio, γM, fattore di confidenza e modulo elastico; non si possono usare automaticamente le resistenze del calcestruzzo.

Il fusto è una mensola libera: lunghezza efficace almeno 2H, imperfezione almeno H/200, rigidezza EI minima e amplificazione 1/(1−N/Ncr). La verifica rimane nel campo senza trazione e N<0,8Ncr. Se queste condizioni non sono soddisfatte serve un modello non lineare e l’esito non è dichiarato favorevole. Per muratura si controllano blocco compresso 0,85fk/(γM·FC) e scorrimento dei giunti; la resistenza a trazione è nulla. Le mensole di fondazione dello stesso materiale sono controllate anche a trazione, quindi una mensola in muratura può richiedere una diversa soluzione costruttiva.

La modalità Resistenze assegnate conserva la compatibilità con i file precedenti e i relativi controlli elastici; non diventa automaticamente una verifica normativa completa.

### Esempio ripercorribile e rapporto

Aprire supporto/artefatti/muri-completamento-20260930/interfaccia-finale/esempio-completo.anthea. Il modello dimostrativo ha H=3 m, B=3 m, due zone di armatura, terreno deformabile di spessore 25 m con M=30000 kPa, sisma da sito e una storia triangolare sintetica. Questi dati servono a riprodurre il test e non descrivono un sito reale. La storia sintetica non è un accelerogramma normativamente qualificato.

Nella stessa cartella sono presenti relazione Word e PDF, figure della sezione e risultati JSON. Il rapporto CONTROLLO.md e PDF nella cartella principale dell’attività descrive test, correzioni e limiti. I confronti MAX rimangono sospesi: nessuna delle nuove funzioni è dichiarata validata contro MAX 16.

I nuovi motori ShallowFoundationSeismic, FoundationSettlement e NewmarkSliding sono separati in X.Calculations/Geotechnics; l’adattatore del muro è RetainingWall.Serviceability. Geometria delle barre e predimensionamento sono separati dall’interfaccia. Materiali, equilibrio e tensioni delle sezioni riutilizzano GPC. Sono candidati per una successiva estrazione nelle librerie GPC; nessun repository GPC esterno è stato modificato.

Fonti: [JRC Eurocode 8 Worked Examples](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/EC8_Seismic_Design_of_Buildings-Worked_examples.pdf), §4.8; [JRC Eurocode 2 Detailing](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/05_EC2WS_Arrieta_Detailing.pdf); [USGS Newmark](https://pubs.usgs.gov/sir/2007/5196/sir2007-5196_text.pdf); NTC 2018 §§4.1.11 e 7.8.2.2.3; USACE EM 1110-1-1905, 2025.


## Approfondimenti integrati

- PRATICA A01: Calcestruzzo armato — collegamento Checker
- PRATICA A02: Gerarchia dei fogli nei progetti
- PRATICA A03: Micropalo orizzontale con CHS
- PRATICA A04: Progetti e revisioni
- PRATICA A05: Stabilità globale: guida rapida
- PRATICA A06: Guida pratica di Bridge Design


## PRATICA A01 — Calcestruzzo armato — collegamento Checker

Le [estensioni di settembre 2026](guida-teorica-anthea.md) aggiungono piani
di deformazione, dettagli costruttivi, curva M–χ, torsione, taglio circolare e
sezioni con foro centrale; integrano le funzionalità descritte in questa guida.

### Motore e convenzioni

L'interfaccia WPF usa la copia delle DLL in `lib/Checker`, non riferimenti ai progetti
sorgenti esterni. Versioni, provenienza e impronte sono in
[manifest.json](../../lib/Checker/manifest.json). Il repository Checker e Rhino2Midas
non sono stati modificati.

**N negativo a compressione**, positivo a trazione. Azioni in kN/kNm; geometria in
mm e tensioni in MPa. L'adattatore converte nelle unità della libreria (N, Nmm).
Gli assi locali delle azioni riprendono CheckerUI: origine nel baricentro, assi
opposti agli assi geometrici x/y della preview. Sono disponibili assi principali
e personalizzati, con trasformazioni ed eccentricità calcolate dalla DLL.
I grafici dei domini sono negli assi locali; Ed nella tabella resta negli assi
scelti per l'input, Rd nei dettagli è negli assi locali.

Le resistenze non derivano più da intersezioni implementate in ANTHEA.
`SectionDomainMesh` contiene soltanto triangoli per il disegno WPF.

| Funzione | API della DLL |
| --- | --- |
| Dominio 3D | `GetFailureDomainResult`, `Domain.GetMesh` |
| Punto resistente e tasso 3D | `CalculateForce`, `CalculateWorkingRatio` |
| Meridiani 2D locali a multipli di 90° | `GetPlasticFailureDomainResult2d` / `GetElasticFailureDomainResult2d` |
| Altri tagli 2D | `CalculateDomainConstantMomentsRatio` / `CalculateDomainConstantAxialForce` |
| Punto resistente 2D | `FailureDomainResult2d.AddForce` e tasso nativo sul vettore locale |
| Analisi tensionale | `GetTensionAnalysisResult(force)` |
| Tensioni e limiti | getter e controlli SLE di `StressAnalysisResult` |

Nel 2D la selezione N–M identifica un piano passante per l'asse N negli assi locali
del dominio. Le eccentricità dell'origine possono quindi portare le azioni fuori
piano; la proiezione deve essere esplicitamente abilitata. Il tasso dell'azione
proiettata non è una verifica dell'azione completa.

È usato l'overload sincrono a singola azione per le tensioni: nei sorgenti esaminati
l'overload asincrono massivo senza argomenti presenta i rami lineare/non lineare
invertiti. L'interfaccia esegue il calcolo in background. Non c'è ripiego su un
solutore alternativo se Checker non converge. L'annullamento viene controllato
fra chiamate: non interrompe una chiamata interna già iniziata nella DLL.

### Schede e opzioni

### Rifiniture del 21 settembre 2026

- Le etichette dei domini sono **Plastico** ed **Elastico**. Gli identificativi
  interni e le famiglie Excel rimangono SLU/SLV per compatibilità degli archivi.
- Ø e passo delle staffe sono solo nel gruppo Staffe, non duplicati in Armature.
- Cambiare criterio di ricerca o strategia aggiorna le opzioni del solver nativo
  e ricalcola punti resistenti/tassi, mantenendo le stesse istanze di dominio e
  mesh visualizzata. Cambiando strategia in Intersezione, la DLL può comunque
  preparare una propria mesh ausiliaria interna; cambiare solo N costante/eccentricità
  ecc. non richiede questa preparazione. Senza altri aggiornamenti pendenti non vengono ricalcolate neppure le
  SLE o l'altra scheda dominio. La proiezione 2D conserva la curva nativa.
- Proietta sul piano è visibile prima del filtro azioni, fuori dalle opzioni avanzate.
- **Scala…** offre zoom percentuale e fattori indipendenti X/Y nel 2D,
  Mx/N/My nel 3D. Sono trasformazioni grafiche, non alterano sollecitazioni,
  resistenze o unità. Adatta ripristina i fattori unitari. Il fit 2D considera
  normalmente solo il dominio e mantiene l'origine al centro; includere le
  azioni è facoltativo. Lo zoom è continuo, anche in riduzione. Nel 3D il fit
  usa gli estremi effettivi, l'orientamento corrente e le proporzioni del viewport.
- Senza trefoli si nascondono n/φp/Ep, i coefficienti normativi dei trefoli,
  il relativo comando delle etichette e il pulsante materiale dedicato.
  Rimane disponibile + Trefolo; aggiungendone uno ricompaiono le impostazioni.
- Le opzioni di calcolo/verifica SLE sono comuni alle tre famiglie. Azioni,
  contouring ed etichette restano indipendenti. Al primo passaggio di un vecchio
  foglio si adottano le opzioni Rara e si conserva una copia delle tre configurazioni
  in `sle_precedenti_unificazione`. I dati comuni sono in `sle_comuni`.
- Accanto ai dettagli della combinazione, **Riepilogo verifiche** mostra il
  nome governante e η massimo per Plastico/Elastico, tensioni/fessurazione delle
  tre SLE e taglio Vx/Vy. Include tutte le righe, non solo quelle filtrate, e
  distingue gli esiti privi di tasso da quelli verificati.
- Il taglio propone **Automatici da sezione** per i parametri del wizard.
  Per rettangolare: bw,x = h e bw,y = b; per T: bw,x = hf e bw,y = bw dell'anima.
  d deriva dal baricentro delle barre di lembo; si usa il minore nei due versi.
  Si raggruppano le barre entro un diametro massimo dal centro più esterno,
  separatamente nelle due metà geometriche; Asl è il minimo delle aree dei
  due gruppi, non tutta l'armatura longitudinale. Sono stime geometriche esplicite,
  da verificare rispetto al meccanismo resistente reale; non usano il segno di V
  per dedurre il lembo teso. **Manuali** permette di sovrascriverle.
  Le precedenti impostazioni sono conservate in `parametri_precedenti`.
  Senza staffe l'Asl geometrica richiede conferma dell'ancoraggio efficace prima
  di produrre un esito; cambiare i parametri geometrici derivati annulla la
  conferma. Circolare/CAP e geometria generica restano fuori campo.

Riferimento per bw, d e condizione di ancoraggio di Asl:
[NTC 2018, §4.1.2.3.5](https://www.gazzettaufficiale.it/eli/gu/2018/02/20/42/so/8/sg/pdf#page=83).

### Aggiornamento automatico e scambio delle azioni

Nelle cinque schede CA non ci sono pulsanti **Calcola**. All'apertura e dopo
ogni conferma dei dati il calcolo parte automaticamente, **senza timer**.
I campi di testo e le celle si confermano al cambio di focus o al salvataggio;
durante la digitazione rimane valido lo stato precedentemente confermato.
Selettori e caselle di spunta si confermano subito. Gli input rimangono utilizzabili; una nuova modifica annulla
la pubblicazione dei risultati precedenti e accoda l'aggiornamento. Le chiamate
interne alla DLL già avviate devono terminare prima della richiesta successiva.
Gli errori di una verifica non impediscono il calcolo delle altre; vedere gli
esiti delle righe, il riepilogo e `errori_calcolo` nell'esportazione JSON.

I domini nativi sono riutilizzati quando cambiano solo le azioni. La cache è
distinta per 2D/3D e SLU/SLV e confronta geometria, materiali, trefoli e opzioni
effettive. Filtri, selezione, trasparenza e contouring sono solo visualizzazioni:
non lanciano un nuovo calcolo. Le opzioni avanzate sono raggruppate in sezioni
apribili/richiudibili; nasconderle non ne cambia i valori.

Ogni tabella delle sollecitazioni offre **Template Excel**, **Importa Excel** e
**Esporta Excel**. Senza percorso selezionato, Importa chiede il file. Template
ed Esporta memorizzano il percorso nel foglio ANTHEA: Importa rilegge quel file.
Il percorso è visibile e **Sfoglia** permette di cambiarlo. Se il file non esiste
più viene chiesto di selezionarne un altro. Esporta include tutte le famiglie e
tutte le righe, indipendentemente dai filtri; conserva i valori double senza
arrotondare i dati. I nomi sono testi Excel anche se iniziano con `=`.
Il template `.xlsx` è incluso nel programma e non richiede Excel installato.
Un solo foglio `Azioni`, con colonne Famiglia, Nome, N [kN], Mx [kNm], My [kNm],
Vx [kN], Vy [kN]. Famiglie: SLU, SLV, Rara, Frequente, Quasi permanente, Taglio.
Le righe di Taglio usano N/Vx/Vy, le altre N/Mx/My; le componenti non pertinenti
devono essere vuote o nulle. Le azioni devono essere già combinate. N rimane
negativo a compressione: non avvengono conversioni implicite di segni o unità.

Sono predisposte 500 righe vuote, senza combinazioni dimostrative; se ne possono
aggiungere fino a 10.000 per importazione (file massimo 20 MB). Scrivere zero
quando la componente è nulla: un campo obbligatorio vuoto non vale zero.
L'importazione valida tutte le righe prima di modificare i dati e chiede se
aggiungerle o sostituire le sole famiglie presenti. Le altre famiglie restano
intatte. Si leggono numeri e testi, anche con virgola decimale. Le formule non
vengono eseguite: se hanno un risultato memorizzato viene segnalato nella
conferma; occorre aver ricalcolato e salvato il file in Excel. Formule senza
risultato salvato o celle con errori bloccano l'importazione.

**Ctrl+C** copia nome e sollecitazioni delle righe selezionate, senza le colonne
dei risultati. Ctrl/Maiusc+clic permettono la selezione multipla. **Ctrl+V**
incolla dalla riga corrente: quattro colonne includono il nome, tre sono le
azioni. Una/due colonne partono dalla cella di input corrente. Le righe eccedenti
sono aggiunte; il menu contestuale offre anche **Aggiungi dagli appunti**.
In modifica di una cella, un testo singolo mantiene il normale incolla testuale.
Un incolla tabellare non valido non modifica alcuna riga.

Tutte le tabelle CA, comprese barre, trefoli e vertici CLS, hanno filtro per
colonna con Contiene, Inizia con, Uguale a e Non contiene. Ordinamento crescente
o decrescente dalla barra oppure clic sull'intestazione: confronto numerico per
valori numerici, alfabetico per testi. Non si eliminano né si escludono azioni dal
calcolo. I filtri sono locali alla vista e non vengono salvati nell'archivio.

### Visualizzazioni e riepiloghi

Nei domini 3D e 2D **Forze: tutte / selezionata** alterna le azioni visibili,
rispettando filtro e colonna Mostra. Il 3D ha uno slider di trasparenza 0–100%:
0% opaco, 100% superficie invisibile; assi, punti e reticolo restano leggibili.
Sollecitazioni, resistenze e linee di verifica hanno interruttori indipendenti.
Colori η è facoltativo: verde sotto 0,80, ambra fino a 1, rosso oltre 1, grigio
se il tasso non è determinato. Sono soglie grafiche, non ulteriori verifiche.
Il 3D ha illuminazione con normali interpolate e griglia sul piano N=0.
Il 2D mantiene l'origine al centro anche nello zoom, senza traslazione, con
assi simmetrici e passi arrotondati 1/2/5 × 10ⁿ (multipli di 10 quando la scala
lo consente). La spezzata origine–Ed–Rd identifica la verifica selezionata;
non sostituisce il criterio resistente della DLL.
Il riepilogo laterale distingue Ed negli assi di input da Rd negli assi locali,
mostra criterio, tasso, tensioni/deformazioni minime e massime, altezza utile,
posizione/inclinazione dell'asse neutro forniti da `CalculateStrainPlaneResult`.
Gli estremi della mesh/curva sono **campionati sull'intero dominio** e non vanno
confusi con le resistenze al N della combinazione. Lo stato tensionale riportato
nel dominio è quello **al punto resistente**, non all'azione Ed.

In SLE il dettaglio ha le viste **Riepilogo**, **Barre e trefoli** e **Calcestruzzo**. Riporta azioni,
modello, limiti applicabili, tassi, stato tensionale/deformativo all'azione Ed,
asse neutro, condizioni ambientali, wk, limite, Ac,eff e As,eff quando disponibili.
Il selettore contouring riprende le rappresentazioni pertinenti di CheckerUI:
gradiente/bande delle tensioni CLS, scala riferita alla resistenza del CLS,
tensioni nelle barre, tasso delle barre, tasso della sezione; aggiunge
deformazioni CLS e sola geometria. Ogni mappa ha legenda e unità. I rapporti alla
resistenza del materiale **non sono gli esiti tensionali SLE**. Il campo CLS è
campionato con getter nativi su un raster 96×96, ritagliato al contorno della
sezione e interpolato soltanto per il disegno; non interviene nel calcolo.
La palette predefinita è blu per valori negativi, bianco allo zero e rosso per
positivi. Le legende sono verticali. Gli estremi della legenda campionata possono differire dai valori
ai vertici nel riepilogo. Le resistenze usate per normalizzare le mappe sono
positive in valore assoluto; il segno delle tensioni resta quello di Checker.
Non sono aggiunti i contouring dei profili metallici interni, non presenti nel
modello geometrico attuale di ANTHEA.

Tre comandi, spenti di default, mostrano σ/ε per tutte le barre, tutti i trefoli
e tutti i vertici del CLS; per contorni con molti vertici conviene espandere e
ingrandire la vista o usare la tabella Calcestruzzo. Le deformazioni usano le API
native `GetRebarStrain` / `GetVerticeStrain`, con φ nella lineare: sono incrementali,
senza εp iniziale. Valori calcolati a due decimali; per piccoli valori non nulli
si usa notazione scientifica invece di farli apparire nulli. I risultati e gli
esiti continuano a usare la precisione completa.

Nelle opzioni comuni delle tre SLE sono modificabili sia n armature sia n trefoli (se presenti):
`n = Eacciaio × (1 + φ) / Ec`. Modificare n aggiorna φ, modificare φ aggiorna n.
Cambiare materiale mantiene φ e aggiorna n. Si usa Ec del materiale DLL.
Ep di riferimento è il primo trefolo presente, oppure il materiale trefolo
predefinito, oppure 195000 MPa se non ci sono trefoli. Con Ep differenti,
φp è comune e ogni materiale ha un n effettivo diverso. φ deve essere non negativo;
valori n incompatibili bloccano l'analisi SLE. n/φ non governano la non lineare.

Il Taglio ha gruppi separati Vx/Vy, preview e riepilogo selezionato con VRsd,
VRcd, VRd, cot θ, elemento governante e tasso per direzione. Restano espliciti
i limiti di applicabilità già documentati; la riorganizzazione grafica non
estende il modello resistente.
Ø, passo e braccia si impostano sia nel pannello di controllo sia nel Taglio,
su dati condivisi. Rettangolare/T: braccia per Vx e Vy; circolare: staffa chiusa
o spirale e ferri interni. Il disegno è **indicativo e non esecutivo**: non include
ancoraggi, piegature e sagomario. La scelta Spirale non abilita un modello a taglio
circolare non ancora validato.

### Contenuto delle schede

1. Pannello di controllo: geometria, materiali, coefficienti, armature, trefoli,
   preview e riepiloghi distinti delle verifiche, compreso il taglio.
2. Dominio 3D: SLU plastico/SLV elastico; N costante, eccentricità costante,
   Mx–My costanti, N–Mx costanti o N–My costanti; ricerca iterativa/intersezione,
   direzioni angolari, interpolazione della mesh, suddivisioni N, contributo del
   CLS teso, assi, filtri e tabella delle combinazioni.
3. Dominio 2D: N–M con direzione θ oppure Mx–My a N fissato, risoluzione,
   assi, CLS teso, filtro e proiezione esplicita delle azioni fuori piano.
4. Tensioni e fessurazione: Rara, Frequente e Quasi permanente con azioni indipendenti e opzioni comuni;
   analisi lineare/non lineare, viscosità φ per barre e trefoli nella lineare,
   CLS teso, assi; esposizione, sensibilità dell'armatura, durata, aderenza,
   copriferro e spaziatura massima delle barre tese.
5. Taglio e torsione: azioni N/Vx/Vy/T; modello con/senza staffe; bw, d, Asl ancorata,
   rami, inclinazione delle staffe, cot θ automatica o manuale per ogni asse.
6. Dettagli costruttivi: tipo di elemento, controlli capitolo 4 / EC2, ancoraggi.
7. Momento–curvatura: percorso configurabile, primo snervamento, limite e CSV.
   Ø e passo sono nei dati comuni. Vx agisce lungo x (d nella larghezza);
   Vy lungo y (d nell'altezza). Non è una verifica di interazione biassiale.

I viewport hanno zoom, adattamento, esportazione PNG e apertura ingrandita.
Il 3D è ruotabile; selezionare una riga evidenzia Ed e Rd. Le azioni SLU/SLV sono
condivise fra 2D e 3D. SLE e taglio richiedono azioni già combinate: non vengono
generati coefficienti ψ né combinazioni di carico.

I materiali predefiniti usano CLS parabola-rettangolo e acciaio elastico-perfettamente
plastico. I pulsanti + CLS / + Acciaio / + Trefoli creano materiali custom salvati
nel foglio e riapplicabili dal relativo elenco. CLS: nome, fck (12–90 MPa) e
diagramma parabola-rettangolo, bilineare, stress block o non lineare. Moduli,
deformazioni limite e resistenza a trazione derivano dalla DLL. Acciaio: Es,
fyk, fu, εu e legge elastoplastica/incrudente. Trefoli: Ep, fpyk, fpk, εpu;
l'applicazione aggiorna i materiali dei trefoli esistenti senza cambiare Ap,
posizione o σp0, e predispone i nuovi. L'acciaio è marcato
`SteelTypes.Rebar`: lasciare Undefined impediva alla DLL di costruire il dominio.
Non è stato importato l'intero editor materiali/geometrie di CheckerUI: curve
tabellari, FRC e profili metallici interni restano da implementare. Restano le
geometrie parametriche circolare, rettangolare e a T. **Generica (da definire)**
è una scelta salvabile ma blocca esplicitamente il calcolo finché non sarà
implementata la definizione di contorni, fori e armature.
I trefoli richiedono Ap, Ep, fpyk, fpk, εpu e σp0 (tensione iniziale positiva,
da fornire già coerente con le perdite considerate). Il motore li include
effettivamente; dati incompleti bloccano il calcolo.
Si controllano valori finiti, dimensioni, copriferro/disposizione, conteggi,
sovrapposizione delle armature e contenimento dell'intera area di barre/trefoli
nel CLS. Fu e deformazioni ultime devono essere coerenti con E e fy.
La DLL distingue alcune API SLE dei trefoli tramite εp: per σp0=0 l'adattatore
non pubblica un esito SLE impropriamente classificato come armatura ordinaria.
Il coefficiente SLE dei trefoli è applicato dalla DLL a **fpyk**, non a fpk:
questo è reso esplicito nell'editor e va riesaminato nella validazione normativa CAP.

### Normative disponibili e report

Il selettore collega NTC 2018, Model Code 2010, EN 1992-1-1, UNI, DIN, DS e NS
EN 1992-1-1. CNR-DT 204/2006 e CS-TR34 restano leggibili nei vecchi archivi ma
sono esclusi dal selettore per il calcestruzzo ordinario. I coefficienti della classe
base sono visibili e modificabili; la scelta di una nuova normativa ripristina
i suoi valori predefiniti. I tre input storici αcc/γc/γs sono sincronizzati.
La cache dei domini include normativa, coefficienti e materiali. Taglio e
fessurazione hanno formule dedicate per tutti i sette profili; il Model Code
usa anche i momenti associati al taglio. Le verifiche dei dettagli e la torsione
restano nel campo NTC documentato.
Per supporto effettivo, differenze nazionali e parti mancanti vedere
[normative-calcestruzzo.md](guida-teorica-anthea.md).

Dal menu **Report Word** si selezionano geometria, materiali, coefficienti,
azioni, domini, ogni SLE, taglio, dettagli di barre/vertici e grafici. Le scelte
si salvano nel foglio. Ambito, limiti ed errori sono sempre inclusi. La struttura
è Input (geometria, armature, staffe, trefoli), Materiali (CLS, acciaio, trefoli),
Coefficienti, Sollecitazioni e Verifiche. I grafici sono nelle rispettive sezioni.
Per ciascuna famiglia SLE, di default si stampano gli estremi algebrici delle
tensioni/deformazioni con la combinazione di origine, il caso con ησ massimo e
quello con ηw massimo, distinti. Gli estremi non sono uno stato simultaneo.
Gli esiti senza tasso numerico (non applicabilità, decompressione, errori) restano
espliciti: non si inventa un caso governante. L'opzione `sle_tutte` abilita tutte
le combinazioni. I dettagli facoltativi riportano i casi da cui provengono gli
estremi e i governanti, oppure tutti i casi in modalità completa. Le immagini SLE
sono riferite ai governanti effettivi, indipendentemente dalla riga selezionata;
i domini rappresentano le opzioni correnti (3D in isometria).
Un aggiornamento in corso o dati globalmente invalidi bloccano
l'export. Il DOCX non ricalcola e non dichiara una conformità normativa globale.

La finestra CA ha scorrimento orizzontale e verticale quando lo spazio è inferiore
alla superficie minima di lavoro; i pannelli mantengono i propri scroll interni.
La rotella su un menu chiuso scorre il pannello senza cambiare la selezione.
I menu contour e filtri leggono la voce selezionata, non il testo ancora in
aggiornamento. I numeri nelle griglie sono centrati, anche durante l'editing.
Lo spessore disegnato delle staffe segue Ø × scala senza limite massimo in pixel;
asse della staffa a copriferro + Ø/2. Resta uno schema, non un disegno esecutivo
di piegatura, ancoraggio o sviluppo della spirale.

### Integrazione Rhino2Midas e correzioni autorizzate

Provenienza: `Fem2Rhino.Grasshopper.Reading/ConcreteChecker/Components`,
componenti ServiceabilityStressCheck, ServiceabilityCrackWidthControlCheck,
RectangularSectionShearCheck e RectangularSectionShearCheckNoStirrup.
Il porting è isolato in `X.Core/Ntc2018Checks.cs`; non importa Grasshopper/Rhino.

### NTC 2018

Riferimento: [capitolo 4 ufficiale](https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.num=1&art.tiposerie=SG&cdimg=18A0071600100010110005&dgu=2018-02-20),
§§4.1.2.2.4–5 e 4.1.2.3.5.

- Taglio senza staffe: eliminato |N|; la trazione non incrementa la resistenza.
  In trazione non si emette un esito automatico. Compressione limitata come
  previsto nella formula; si confrontano formula principale e minimo.
- Con staffe: resistenza minima fra tirante e puntone; cot θ limitata a 1–2,5.
  Corretti l'incrocio delle inclinazioni x/y e la radice negativa nell'ottimizzazione.
- Rara: limiti CLS e acciaio dalla libreria; QP: limite CLS; frequente: solo
  tensioni, senza inventare un limite tensionale.
- L'opzione comune per elementi piani gettati in opera sotto 50 mm riduce fcd
  e i limiti tensionali del CLS; vale anche nel modello a taglio.
- Il limite di compressione viene applicato alle sole tensioni negative del CLS.
- Decompressione e formazione delle fessure sono controlli sulla sezione
  omogeneizzata integra, distinti dall'apertura: nessuna divisione per limite zero.
- La tabella di esposizione distingue formazione e decompressione per le armature
  sensibili in ambiente molto aggressivo.

### Circolare 2019

Riferimento: [capitolo C4 ufficiale](https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=19A00855&art.dataPubblicazioneGazzetta=2019-02-11&art.num=1&art.tiposerie=SG&cdimg=19A0085500100010110005&dgu=2019-02-11),
§C4.1.2.2.4, formule C4.1.6–10.

- Corretti i coefficienti di durata: kt = 0,6 breve; kt = 0,4 lunga.
- L'area efficace viene ottenuta tagliando la mesh della sezione con gli strumenti
  geometrici GPC; As,eff e diametro equivalente comprendono solo le barre tese
  nella fascia efficace, non tutta l'armatura.
- La spaziatura massima è esplicita, senza dedurla dal vicino più prossimo.
  Nella zona lontana dalle barre non si sceglie indiscriminatamente il minore dei
  due modelli di distanza: viene mantenuto il caso più gravoso delle zone applicabili.
  Per C4.1.10 si usa esattamente Δsm = 0,75(h−x), anziché 1,3/1,7(h−x).
- Gestiti area/armatura efficace assente e numeri non finiti.
  La sezione interamente tesa richiede un trattamento separato delle aree efficaci
  e non riceve un risultato automatico.

### Limiti ancora espliciti

Questi controlli non costituiscono una validazione integrale del software né una
certificazione della struttura. L'esportazione mantiene
`verifica_normativa_completa: false`.

- Taglio circolare: richiede la scelta esplicita del modello e dei parametri,
  descritta nelle [estensioni del modulo](guida-teorica-anthea.md).
- Taglio con precompressione: da completare, incluse le componenti dei cavi.
- Apertura delle fessure con trefoli: da completare, incluse aderenza ed area
  efficace specifica. Decompressione/formazione hanno un controllo distinto.
- Apertura wk: disponibile per analisi lineare fessurata, incluse le sezioni interamente tese;
  la non lineare calcola le tensioni ma non produce automaticamente wk.
- Dettagli, ancoraggi, momento–curvatura, torsione e interazione V–T hanno
  controlli dedicati nelle nuove schede; valgono le ipotesi documentate nelle
  estensioni. Gerarchia sismica e secondo ordine non sono verificati.
- SLV elastico è il dominio elastico della libreria, non l'intera verifica sismica.
- Per geometrie, esposizioni, materiali e carichi reali occorre una verifica
  indipendente del progettista. Le scelte fuori campo sono segnalate nella tabella.
- Normative e coefficienti nazionali incompleti non vengono presentati come
  implementazioni normative complete; vedere la matrice dedicata.

### Archivi e compatibilità

Il formato generale degli archivi resta versione 1; `versione_sezione` resta 2.
`workspace_ca.versione` passa a 2. In apertura, i workspace v1/privi di versione
usavano N positivo a compressione: N viene invertito una sola volta per input,
combinazioni e piano 2D; Mx/My restano invariati. Convenzione e migrazione sono
registrate nel workspace. Le vecchie SLE restano Rara, senza duplicazione.

Opzioni, trefoli e azioni a taglio si salvano con il foglio. I risultati non sono
riutilizzati come validi dopo la riapertura; le modifiche invalidano i risultati
interessati. JSON di esportazione: DLL utilizzata, norma, dati, risultati per ID
ed esiti mancanti/parziali.

Il vecchio motore `SezioneCA` resta per geometria e per i confronti storici,
oltre che nel modulo distinto del palo orizzontale; non calcola le resistenze
delle nuove schede CA. Il comando diagnostico storico `supporto/test/X.Verifiche --calcola`
per `str_palo` non va usato per i nuovi workspace Checker: usare l'esportazione
del foglio WPF. I confronti storici non dimostrano l'identità fra i due motori.

### Prove riproducibili

```powershell
dotnet build ANTHEA.sln -c Release
dotnet supporto/test/X.Verifiche/bin/Release/net8.0/ANTHEA.Verifiche.dll --checker
dotnet supporto/test/X.Verifiche/bin/Release/net8.0/ANTHEA.Verifiche.dll supporto/test/casi_confronto.json supporto/artefatti/verifiche_checker/confronto.json
dotnet X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll --smoke supporto/artefatti/verifiche_checker/wpf supporto/test/casi_confronto.json
```

I controlli dedicati confrontano punti/tassi con le API native, tutti i percorsi,
SLU/SLV, lineare/non lineare, meridiani 0/90/35°, proiezioni, cambi di riferimento,
trefoli, migrazione e cancellazione. Il benchmark VCA_N_1 riprende i test Checker:
rettangolo 300×500, quattro Ø18 a 50 mm, n=15; scarto ammesso 5% sui valori
pubblicati arrotondati, non una tolleranza generale di progetto.
Sono presenti risultati analitici indipendenti per taglio, apertura delle
fessure e area efficace rettangolare. La prova WPF verifica tabelle, mappe,
invalidazione, filtri, salvataggi e viewport. Vedere anche
[esito dell'integrazione](guida-teorica-anthea.md).


## PRATICA A02 — Gerarchia dei fogli nei progetti

Ogni sezione e il progetto possono contenere fogli insieme a sottosezioni. Nell'albero i fogli sono visualizzati prima delle sottosezioni, immediatamente sotto il contenitore a cui appartengono. Il trascinamento sul titolo aggiunge o sposta una scheda in quel contenitore; il trascinamento sulle righe delle schede mantiene il riordino manuale.

Trascinando il nome di una sezione sopra o sotto quello di un'altra dello stesso gruppo si cambia l'ordine delle sezioni. Una linea indica la posizione di inserimento. Si sposta l'intero contenuto, incluse schede e sottosezioni, senza cambiare il genitore o i riferimenti dei dati comuni. La stessa operazione riordina i progetti. L'ordine viene conservato nel file `.programma`.

Per ogni proprietà condivisa prevale il livello più alto che la definisce e che è compatibile con il destinatario. La regola attraversa anche sezioni intermedie vuote. Materiali, geometria, armatura e dati geotecnici mantengono gli adattatori, le unità e i limiti dei singoli moduli. I fogli in rami paralleli condividono soltanto i riferimenti dei loro antenati comuni; non vengono confrontati direttamente tra loro.

Quando si confermano o si salvano modifiche a un riferimento con fogli discendenti, i dati compatibili da esso governati si propagano nella sua sezione e nelle sottosezioni. Le modifiche effettuate sotto un riferimento superiore restano locali e sono segnalate come conflitti; non riscrivono il riferimento. Per cambiare il dato comune si modifica il foglio superiore. Per proprietà senza un riferimento superiore rimane disponibile la scelta di aggiornare i fogli dello stesso livello oppure mantenere il valore locale.

Le nuove schede ereditano i dati comuni. In presenza di riferimenti discordanti allo stesso livello, le proprietà ambigue restano da uniformare; una forma ambigua impedisce di inizializzare una geometria o un'armatura parziale. La geometria viene definita prima di verificare la compatibilità dell'armatura. Gli spostamenti e l'apertura di archivi esistenti conservano gli input e mostrano gli eventuali conflitti nella nuova collocazione.

Il confronto mostra il percorso dei fogli e include i riferimenti superiori anche quando si apre da una sottosezione. «Uniforma a questo» opera nella sezione del riferimento e nelle sue sottosezioni; i valori governati da un antenato non possono essere promossi dal basso. I distintivi dei gruppi riassumono conflitti e avvisi discendenti. Il controllo del copriferro cerca la scheda CLS anche nei livelli superiori.

Compatibilità archivio: `fogli` sul nodo progetto è facoltativo per i file precedenti; se presente viene validato come quello delle sezioni. Nessun archivio esistente viene riscritto durante la lettura o il confronto.

### Report delle sezioni

Il pulsante **Genera report** su ogni progetto e sezione esporta un Word unico con i fogli diretti e tutte le sottosezioni, nello stesso ordine dell'albero. È disabilitato soltanto sui rami senza schede. Il documento contiene un indice navigabile, dati comuni per proprietà, dati specifici, risultati e grafici dei moduli. I materiali includono anche le proprietà derivate. I riferimenti necessari degli antenati sono richiamati anche esportando una sola sottosezione; i fogli dei rami esterni non vengono inclusi.

L'accorpamento usa compatibilità e unità normalizzate della condivisione: non confronta JSON interi né unisce rami indipendenti. I dati discordanti restano separati, con valori e provenienza. Conflitti e avvisi vengono mostrati prima del salvataggio, con la possibilità di aprire il confronto oppure generare il documento con le segnalazioni. Copriferro e limiti dei moduli sono conservati.

Il calcolo usa una copia dei dati correnti, senza cambiare i fogli aperti. Tutti i moduli vengono ricalcolati; schede incomplete e verifiche non disponibili restano nel report con segnalazione esplicita. I report vengono assemblati in memoria e il Word finale viene scritto atomicamente. Annullando prima del salvataggio, un file già esistente rimane invariato. I report singoli conservano la propria funzione di esportazione.

La prova `--smoke-project-report <cartella>` verifica pulsanti su tutti i livelli, sette moduli, ordine e contenuti ricorsivi, proprietà comuni, parametri specifici, riferimenti esterni, conflitti, immagini, file sorgenti invariati, errori e annullamento.

Verifica: `ANTHEA.exe --smoke-hierarchy <cartella>` copre i drop sui gruppi con figli, precedenza a più livelli, ereditarietà, propagazione, conflitti risolvibili, separazione dei rami, copriferro, riordino/spostamento, ambiguità e salvataggio/riapertura. Le prove `--smoke-sharing`, `--smoke-projects`, `--smoke-materials` e `--smoke-steel` verificano le integrazioni precedenti.


## PRATICA A03 — Micropalo orizzontale con CHS

Modulo `geo_micropalo_orizzontale`, con workspace condiviso con il palo orizzontale.
Il diametro geotecnico D (m, inizialmente 0,24) governa le reazioni del terreno;
De e t del CHS (mm) governano la sola sezione resistente in acciaio. De < D.
Il catalogo dimensionale è `Chs.Catalogo`, già usato dal micropalo verticale:
non è un catalogo di disponibilità commerciale né assegna la qualità dell'acciaio.
La modalità manuale ammette dimensioni arbitrarie valide, senza cambiare quelle del catalogo.

Proprietà analitiche, con di = De - 2t:
A = π(De²-di²)/4; I = π(De⁴-di⁴)/64; Wel = 2I/De;
Wpl = (De³-di³)/6. Massa = A · 0,00785 kg/m per A in mm².
Npl = A fy/γM0; Mpl = Wpl fy/γM0, con conversioni N→kN e Nmm→kNm.
My(N) = Mpl (1-|N|/Npl): interazione lineare conservativa della sezione.
Il riempimento non contribuisce alla resistenza.

Classificazione secondo i limiti tradizionali EN1993-1-1, tabella 5.2:
D/t ≤ 50ε² (1), 70ε² (2), 90ε² (3), altrimenti 4; ε² = 235/fy.
Per non attribuire duttilità a sezioni non idonee, il ramo automatico è limitato
alla classe 1. Non è una verifica completa della rotazione plastica disponibile.
Per classi successive serve una valutazione specifica: il programma non assegna
automaticamente un momento elastico a un meccanismo che presuppone cerniere plastiche.
fy (inizialmente 355 MPa) e γM0 (inizialmente 1,05) sono input del progettista,
da verificare rispetto a materiale, spessore, norma e situazione di progetto.

Riferimenti di confronto: SCI P362 (EN1993-1-1, resistenza e interazione N-M),
https://www.steelconstruction.info/images/6/6a/SCI_P362.pdf ;
Steel for Life, note alle tabelle interazione:
https://www.steelforlifebluebook.co.uk/explanatory-notes/ec3-ukna/axial-force-bending-tables .
Non si implementano le resistenze migliorate di quelle tabelle: si usa
il criterio lineare conservativo esplicitato sopra.

Limiti: nessuna verifica di instabilità globale, taglio, giunti, corrosione,
connessioni, spostamenti o risposta ciclica. Valgono inoltre i limiti del motore
Broms e dell'estensione multistrato descritti in palo-orizzontale.md.
Il momento manuale resta disponibile con indicazione obbligatoria della provenienza;
non attribuisce conformità normativa automatica.


## PRATICA A04 — Progetti e revisioni

La pagina di composizione presenta tre colonne: informazioni e riepilogo a sinistra, struttura del progetto al centro e catalogo delle schede a destra. I pannelli scorrono indipendentemente e i divisori consentono di regolare le larghezze. Nei fogli l’albero è inizialmente nascosto: la freccetta nella barra dei comandi lo mostra o lo nasconde e mantiene la scelta passando da un foglio all’altro. Le miniature aprono i fogli; clic sul nome di una sezione o Invio apre il riepilogo. Il pulsante Torna al progetto riapre il riepilogo. Doppio clic e F2 rinominano; clic fuori conferma il nome e chiude l’editing. Il menu destro delle sezioni contiene Rinomina, Duplica ed Elimina.

Il riepilogo organizza le informazioni nei riquadri Controlli, Dati comuni e Revisioni. I dati principali sono subito visibili; gli altri valori, i fogli, le sottosezioni e il dettaglio degli avvisi sono espandibili. L’albero evidenzia tutta la riga selezionata, mostra guide di gerarchia e mantiene allineati i comandi di aggiunta, confronto e report. Il catalogo compatto conserva le miniature originali, raggruppa le schede per disciplina e offre una ricerca per nome o disciplina. La struttura iniziale resta vuota e presenta il pulsante Crea progetto. I riferimenti di ereditarietà restano operativi senza etichette aggiuntive nell’albero o nel riepilogo.

### Spostamenti e annullamento

Il bordo di un’intestazione indica un riordino prima o dopo un elemento dello stesso gruppo. Il centro di una sezione consente di trasferirvi un’altra sezione, comprese le sue sottosezioni. Non è possibile creare cicli o annidare un progetto radice. I fogli si spostano su una sezione o prima/dopo altri fogli.

Un’anteprima calcolata su una copia del documento mostra i cambiamenti dei valori comuni prima di un trasferimento tra sezioni. Annullare l’anteprima non modifica il progetto. In caso di riferimenti discordanti resta la logica di confronto esistente, senza scegliere arbitrariamente un valore.

Annulla e Ripristina conservano fino a 30 stati nella sessione corrente, comprese modifiche confermate, spostamenti, eliminazioni, uniformazioni, duplicazioni e revisioni. Ctrl+Z e Ctrl+Y operano sul progetto quando il fuoco non è in un campo di testo; nei campi resta l’annullamento locale. La cronologia di annullamento si azzera cambiando documento e non viene salvata nel file.

### Duplicazione

Duplica sezione copia l’intero ramo con nuovi identificativi di sezioni e fogli. La copia ha un nome automatico rinominabile e non acquisisce la cronologia delle revisioni dell’originale. I dati locali sono indipendenti; la normale condivisione dei dati nel progetto continua a funzionare.

### Revisioni

La barra Revisioni mostra i pulsanti Rev. 0, Rev. 1, ecc., con la versione selezionata evidenziata e quella modificabile indicata come attuale. Il cambio avviene nella stessa finestra, anche quando l’albero laterale è nascosto. Il foglio aperto viene conservato se presente nella revisione; altrimenti compare la struttura e il foglio viene ritrovato tornando a una versione che lo contiene. La scelta di mostrare l’albero resta invariata.

Nuova revisione archivia la versione attuale (Rev. 0 alla prima operazione), incrementa il numero e seleziona subito la nuova revisione. La nota iniziale è facoltativa. Il riepilogo modifiche mostra data, nota, aggiunte, eliminazioni, cambi d’ordine e variazioni dei parametri comuni; le altre modifiche sono indicate per foglio. Selezionando una sezione nel riepilogo si gestiscono le sue revisioni; nei fogli si usa la sezione revisionata più vicina. Ogni sezione può avere una numerazione indipendente.

Elimina revisione agisce sulla versione selezionata nella barra e consente di annullare l’operazione. Eliminando una versione archiviata, le altre mantengono i loro numeri e il riepilogo viene riferito alla precedente ancora disponibile. Eliminando l’attuale, vengono ripristinati i dati, i fogli e le sottosezioni dell’ultima revisione rimasta, che diventa immediatamente modificabile. L’editor viene ricaricato sui dati ripristinati. L’unica versione rimasta non è eliminabile da questo comando; l’eliminazione dell’intera sezione resta nel menu della struttura.

Il ripristino riguarda il ramo selezionato. I materiali dei livelli superiori conservano i valori del progetto corrente; eventuali differenze con i fogli ripristinati sono visibili nel confronto. I precedenti storici delle sottosezioni ancora disponibili sono conservati fino al numero ripristinato. Se un vecchio foglio è stato spostato in un altro ramo, la copia ripristinata riceve un identificativo indipendente. Il salvataggio rimuove dall’archivio condiviso i dati non più usati da alcuna revisione.

Le revisioni archiviate proteggono gli input e i comandi che modificano i dati, lasciando disponibili schede, scorrimento, selezione delle righe, viste dei risultati ed esportazione. La consultazione usa una copia separata: non modifica gli archivi, il documento corrente o la sua cronologia Annulla/Ripristina. Anche dalla vista storica Salva conserva il documento corrente completo, comprese le revisioni. Tornando all’attuale si ritrovano le modifiche non ancora salvate.

Gli snapshot includono il contesto degli antenati e i rispettivi fogli, escludendo i rami estranei. Vengono congelati i dati, senza collegamenti mutabili al progetto attivo. La numerazione delle revisioni compare nei report di sezione e nel titolo dei report dei fogli. I calcoli dei report vengono rieseguiti sui dati della revisione selezionata.

`ProjectRevisions` usa un archivio di contenuti identificati tramite SHA-256 del JSON ordinato per chiave. Gli alberi storici contengono riferimenti `dati_ref`; i contenuti uguali occupano un’unica voce. In memoria i fogli attivi sono oggetti indipendenti per mantenere gli editor esistenti. Al salvataggio anche i fogli correnti diventano riferimenti e vengono eliminati dall’archivio i contenuti non più utilizzati. Il salvataggio resta atomico.

I file con revisioni usano il formato 2 e richiedono questa versione di ANTHEA o una successiva. I vecchi file di formato 1 restano leggibili; i documenti senza archivio revisioni continuano a essere salvati nel formato 1. Lettura e validazione controllano l’esistenza e l’integrità dei contenuti referenziati.

### Verifica

`ANTHEA.exe --smoke-project-workspace <cartella>` verifica navigazione e selezione, copia indipendente, spostamenti con anteprima e annullamento, recupero di eliminazioni e uniformazioni, immutabilità degli snapshot e dei materiali del contesto, deduplicazione, salvataggio e lettura, riferimenti corrotti, sola lettura e revisione nel report. Produce schermate e un archivio di prova.

I test `--smoke-projects` e `--smoke-hierarchy` verificano le operazioni precedenti, i trascinamenti WPF e la condivisione dei dati a più livelli.


## PRATICA A05 — Stabilità globale: guida rapida

ANTHEA · Muri di sostegno · 30 settembre 2026

La verifica cerca una superficie lungo la quale possono scivolare **insieme il muro e il terreno sottostante**. Il cerchio viene diviso in conci: il programma confronta l’effetto delle azioni con la resistenza dei terreni attraversati. Serve quindi conoscere il terreno anche sotto la fondazione.

### Il percorso in tre passi

**1. Apri Stabilità globale**, nella barra superiore del muro. Se non hai ancora iniziato il profilo, ANTHEA lo prepara dai dati locali e attiva la verifica. Riaprire il pannello conserva il lavoro. Il pulsante Prepara dal muro sostituisce invece la proposta con i dati locali.

**2. Controlla il sito.** Completa gli strati profondi di monte e valle, correggi le superfici in base al rilievo e controlla falda e sisma. La ricerca è già proposta; i dettagli si possono aprire e modificare. Il messaggio in alto indica il primo dato mancante.

**3. Conferma i dati e premi Calcola globale.** Spunta Ho controllato profilo, strati e falda del sito. Il risultato si apre direttamente in Verifiche → Stabilità globale. Una modifica ai terreni del percorso annulla la conferma e il risultato precedente.

![Esempio stratificato: fondazione a y=0, strati profondi e limiti blu della ricerca. Il disegno è ritagliato sull’area di analisi; i fondi effettivi sono riportati nelle etichette.](../artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato-profilo.png)

**Già proposto:** geometria del muro, strati locali disponibili, attivazione di falda e sisma, area di ricerca, precisione iniziale e combinazioni automatiche.

**Da conoscere per il sito:** rilievo, stratigrafia profonda, proprietà geotecniche, falda e condizioni dell’analisi. Il programma non inventa indagini né resistenze profonde.

<!-- pagebreak -->

### Come inserire gli strati

Nella tabella scrivi **nome e spessore**, dall’alto verso il basso. Il fondo y si calcola automaticamente. Il primo spessore parte dalla superficie del proprio lato presso il muro; ogni successivo spessore parte dal fondo precedente.

**y=0 è il piano di posa. y=−2 m significa 2 m sotto la fondazione.** Nell’esempio H=3 m e t=0,45 m: la superficie di monte è a y=3,45 m, quella di valle a y=0.

| Lato | Terreno | Spessore [m] | Fondo y [m] |
| --- | --- | --- | --- |
| Monte | Riempimento | 3,45 | 0 |
| Monte | Alluvioni | 2 | −2 |
| Monte | Ghiaia | 8 | −10 |
| Valle | Alluvioni | 2 | −2 |
| Valle | Ghiaia | 8 | −10 |

Seleziona una riga per modificare le proprietà. In **Drenata** inserisci φ′k e c′k; in **Non drenata** serve cu,k per ogni strato. γ e γsat sono i pesi di volume naturale e saturo. Stesso nome significa stesso colore nelle due colonne, ma i valori restano indipendenti.

![Editor della colonna di monte con lo strato Alluvioni selezionato. Le quote derivano dagli spessori; le proprietà si modificano sotto la tabella.](../artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato-editor.png)

Per l’esempio illustrativo: Riempimento γ/γsat=18/20 kN/m³ e φ′k=30°; Alluvioni 19/21 e 28°; Ghiaia 20/22 e 36°. c′k=0 per tutti, assenza di falda e sisma. Questi valori servono a riprodurre l’esempio; non sono valori da assumere per un sito reale.

<!-- pagebreak -->

### Rilievo, falda e ricerca

Nel gruppo Rilievo e falda si possono correggere i punti **x, y**. L’origine è il bordo di valle del piano di posa. x cresce verso monte e y verso l’alto. La proposta iniziale ha superfici orizzontali sui due lati: modificarla se il terreno reale è inclinato o presenta dislivelli.

La falda globale è una linea di quote y, non una profondità misurata dal piano campagna. La condizione drenata/non drenata va scelta in base al problema geotecnico; il programma cambia i parametri di resistenza richiesti.

Con ricerca **Automatica**, i limiti orizzontali seguono il rilievo. La profondità va da 0,10 m al minore fra 2(H+t) e la profondità nota comune alle colonne: nell’esempio è 6,90 m. La proposta non garantisce che il dominio sia sufficiente. Per modificarla scegliere **Assegnata** e aprire Limiti e precisione. Anche in Automatica si possono modificare nodi, conci e raffinamenti.

Il sisma globale ha una propria opzione. Da sito usa βs=0,38 e i dati sismici del progetto; in alternativa si assegnano kh e il modulo di kv. Non coincide necessariamente con il coefficiente usato per le spinte del muro. Controllare l’opzione nel gruppo Falda e sisma; la precompilazione ne riprende l’attivazione dal muro.

### Come leggere l’esito

Leggi la colonna **Esito** per tutte le combinazioni. F è il fattore calcolato con i parametri di progetto; **η=γR/F** è il tasso di lavoro. La verifica è soddisfatta nel dominio esplorato quando η≤1 e i controlli della ricerca e della convergenza sono superati.

| Messaggio | Cosa controllare |
| --- | --- |
| Soddisfatta nel dominio esplorato | Risultato favorevole entro il modello e l’area analizzati. |
| Non soddisfatta | Resistenza insufficiente per quella combinazione. Riesaminare progetto e modello; non modificare arbitrariamente i terreni. |
| Minimo sul bordo | Ampliare la ricerca, con rilievo e indagini che coprano la nuova area. |
| Ricerca incompleta / discretizzazione non convergente | Esaminare i dettagli e aumentare la precisione; il tasso da solo non conclude la verifica. |

Il disegno mostra il cerchio critico e i conci. La tabella permette di interrogare terreno, pesi, pressioni interstiziali, resistenze e azioni. Il caso iniziale ha il tasso più alto; un caso privo di superficie valida ha priorità. I dettagli e le combinazioni restano modificabili.

<!-- pagebreak -->

### Esempio salvato e risultati ripercorribili

Apri `supporto/artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato.anthea`. Premi Stabilità globale e poi Calcola globale. Il file contiene già input e conferma per questo esempio didattico. Non premere Prepara dal muro, che sostituirebbe gli strati globali con quelli locali.

Muro a mensola: H=3 m, t=0,45 m, fusto 0,40/0,25 m, mensola di valle 0,80 m e di monte 1,80 m; peso del muro 25 kN/m³. Terreno di valle fino al piano di posa, qk=10 kPa uniforme a monte. Profilo da x=−13,80 m a valle a x=16,80 m a monte; confine delle colonne x=1,20 m. Strati e proprietà sono quelli della pagina 2.

Ricerca: uscite x=−13,80/−0,10 m, ingressi x=3,10/16,80 m, profondità 0,10/6,90 m; 9 nodi per direzione, 60 conci iniziali, 4 raffinamenti. Preset statico A2–M2–R2, γR=1,10. Si ottengono:

| Combinazione | F | η=γR/F | Esito |
| --- | --- | --- | --- |
| Globale A2–M2–R2 1 | 1,176315 | 0,935124 | Soddisfatta nel dominio esplorato |
| Globale A2–M2–R2 2 | 1,075737 | 1,022555 | Non soddisfatta |

![Superficie della seconda combinazione: il tasso supera 1 e il cerchio è rosso. L’esempio conserva l’esito sfavorevole per mostrare la lettura della verifica.](../artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato-esito.png)

Le tabelle dei conci sono in `esempio-stratificato-conci.csv`, accanto al modello. Questo è un controllo interno ANTHEA, non un confronto numerico MAX. Cambiando precisione o dominio possono cambiare leggermente la superficie critica e i valori.

Per l’organizzazione dell’input sono stati consultati il manuale installato di MAX 16 (`manualeMAX.pdf`, §§6.5, 6.6, 6.8 e 16.7) e la [pagina ufficiale MAX](https://www.aztec.it/max-muri-di-sostegno/). L’impostazione per spessori, colori e dettagli ha ispirato l’interfaccia; ANTHEA conserva il proprio motore Bishop e la ricerca per ingresso, uscita e profondità.

Metodo, coefficienti e limiti completi: [guida del modulo](guida-teorica-anthea.md), disponibile anche in PDF. La stabilità globale non comprende cedimenti, liquefazione o una verifica complessiva dell’opera.


## PRATICA A06 — Guida pratica di Bridge Design
Edizione 30 settembre 2026 · Revisione 01 · ANTHEA BD P01

Questa guida accompagna la costruzione di un progetto preliminare, la ricerca delle alternative e la lettura dei risultati di Bridge Design in ANTHEA. Il percorso è pensato per chi deve confrontare tipologie, campate e quantità prima di sviluppare il modello strutturale e il computo completo. La guida teorica della stessa revisione spiega formule, prove e limiti; qui ogni passo indica cosa inserire, cosa controllare e come interpretare il risultato.

La parola ottimo significa migliore fra le combinazioni esplorate con i dati e i criteri scelti. La finestra non fornisce un’autorizzazione a costruire la soluzione e non esegue automaticamente le verifiche dei moduli GPC Engine. Usa il risultato per selezionare poche alternative motivate e conserva insieme geometrie, prezzi, ipotesi e avvisi.

### Preparare i dati prima di aprire la ricerca

Servono almeno lunghezza totale del ponte, larghezza funzionale e quota sul terreno. Per ottenere un confronto utile aggiungere l’ingombro dell’ostacolo, un’ipotesi di terreno, gli estremi su spalla o pila e un listino coerente con l’opera. Se un’informazione non è disponibile, scegliere un valore convenzionale dichiarato e pianificare una seconda ricerca con un’ipotesi diversa.

| Informazione | Dove inserirla | Controllo da fare |
| --- | --- | --- |
| Lunghezza e quota | Sito | Non confondere quota impalcato e altezza della trave |
| Corsie e larghezze | Sito | La larghezza totale comprende banchine spartitraffico e barriere |
| Ostacolo | Sito | Il modello lo centra e aggiunge un margine geometrico di 1 m |
| Tipologia e continuità | Campate | Lo schema deve essere compatibile con la famiglia |
| Altezza e spessori | Sezione | Zero significa automatico soltanto nei campi che lo dichiarano |
| Terreno pile fondazioni | Pile | Distinguere quote manuali e quote automatiche |
| Prezzi e carichi | Risultati del progetto | Adeguare le ipotesi prima di classificare le alternative |

I metri e i millimetri sono indicati accanto ai campi. Non inserire 250 in una soletta richiesta in metri: il valore corretto è 0,25. I prezzi dell’armatura sono in euro per tonnellata; 1,66 euro per kg corrispondono a 1.660 euro per tonnellata. Una differenza di unità può alterare la graduatoria più di una scelta geometrica.

### Creare il progetto di riferimento

Apri il modulo Bridge Design e lavora nella scheda Progetto. Le schede di ingresso sono Sito, Campate, Sezione e Pile. I risultati comprendono Quantità e costi, Dettagli del predimensionamento, Confronto A / B, Prezzi unitari, Ipotesi e coefficienti e Sezioni e quote. Il pulsante Ottimizza porta alla scheda separata Ottimizzazione.

1. In Sito imposta lunghezza, quota, corsie e larghezze accessorie. Controlla la larghezza complessiva restituita dal programma, non soltanto il numero di corsie.
2. Seleziona l’ostacolo e la larghezza. Osserva il prospetto: il disegno è schematico e non costituisce una verifica del franco, dell’alveo o della posizione reale degli appoggi.
3. In Campate scegli una famiglia iniziale e la continuità. Inserisci un numero di campate ragionevole oppure usa zero per la scelta automatica. Leggi poi le luci effettive, che non sono necessariamente tutte uguali.
4. In Sezione imposta i soli dati noti. Lascia zero nei campi automatici se vuoi che il motore li predimensioni. Per le lamiere usa gli spessori in mm; per anime e solette in cls usa le unità mostrate.
5. In Pile scegli terreno, schema e fondazione. Una misura a zero resta automatica; una misura positiva è una richiesta esplicita. Verifica gli avvisi su snellezza, compressione e capacità assiale.
6. Apri Prezzi unitari e Ipotesi e coefficienti. Aggiorna tariffe, carichi equivalenti, resistenze geotecniche di riferimento e fattori ambientali dove disponi di informazioni migliori.
7. Salva il progetto prima della ricerca. Se non è calcolabile, correggi l’errore prima di avviare l’ottimizzazione: il motore deve poter costruire il riferimento.

Una configurazione calcolabile può essere fuori dal campo usuale della famiglia. La scheda Progetto può mostrarla con un avviso, mentre l’ottimizzazione la esclude dai candidati ammessi. Perciò un costo visibile nella scheda Progetto non basta a dimostrare che il riferimento appartenga all’insieme esplorato.

### Leggere sezioni spessori e lunghezze

Apri Sezioni e quote prima di valutare il solo costo totale. Il prospetto riporta componenti, simboli, valori, unità, origine e significato. Impostato identifica un dato assegnato; Automatico una regola del motore; Derivato una conseguenza degli ingressi. Nelle famiglie speciali compaiono anche Adottato e Predimensionato. Nessuna di queste etichette significa sezione verificata.

d è l’altezza complessiva in campata, con soletta. dpila è l’altezza dell’impalcato sulle pile e può essere maggiore per il cassone variabile. h è l’altezza sotto soletta e rialzo; hw è l’altezza netta dell’anima; lw è lo sviluppo reale di un’anima inclinata. Le tabelle distinguono larghezza della piattabanda, spessore, numero di travi, luce di ogni campata e sviluppo complessivo degli elementi longitudinali.

Per esempio, H per 4V uguale a 1 indica uno scarto orizzontale pari a un quarto dell’altezza verticale dell’anima; non indica una pendenza di 45 gradi. Il computo usa lo sviluppo inclinato per l’area e la proiezione verticale per l’inerzia. Per le travi a U controlla che la somma delle larghezze superiori non provochi sovrapposizioni.

Nel prospetto delle sottostrutture leggi separatamente altezza e dimensione del fusto, numero di colonne, dimensioni del pulvino, lati e spessore del plinto, numero, diametro e lunghezza dei pali. Una fondazione bloccata con dimensioni a zero può essere ridimensionata dopo una variazione delle reazioni: controlla sempre le quote effettivamente adottate.

### Scegliere obiettivo e costanti

La scheda Ottimizzazione propone Costo minimo, CO₂ minima e Compromesso costo / CO₂ 50 / 50. Il compromesso normalizza i due indicatori sui minimi della ricerca. Non valuta sicurezza, paesaggio, tempi, manutenzione o interferenze oltre ai filtri presenti. Se uno di questi aspetti governa la scelta, traduci ciò che puoi in vincoli e confronta esternamente le alternative rimaste.

| Blocco | Quando usarlo | Significato operativo |
| --- | --- | --- |
| Tipologia | Hai già scelto una famiglia | Confronta geometrie della stessa famiglia |
| Numero campate | Gli appoggi sono vincolati | Conserva il numero non una libera lista di luci |
| Altezza in campata | Hai un limite già assegnato | Congela la quota adottata in campata |
| Dimensioni della sezione | Vuoi mantenere la sezione ordinaria | Richiede anche il blocco della tipologia |
| Continuità | Vuoi confrontare lo stesso schema | È bloccata all’avvio |
| Schema pila e quote imposte | Le sottostrutture sono già definite | Le quote automatiche restano automatiche |
| Fondazione e lunghezza pali | Vuoi un confronto a fondazione fissata | Quote e conteggi a zero possono ricalcolarsi |

Per un primo confronto ampio puoi liberare tipologia e numero di campate, conservando continuità, schema di pila e fondazione. In una seconda ricerca puoi liberare anche le sottostrutture. Fare entrambe le ricerche aiuta a capire se il risparmio proviene dall’impalcato o da una modifica sostanziale degli appoggi.

La lunghezza totale e la larghezza non vengono ridotte dall’ottimizzatore per ottenere un costo inferiore. Carichi, prezzi, coefficienti e materiali restano quelli del progetto. Liberare la sezione richiama dimensioni standard; non avvia una ricerca sistematica su tutti gli spessori possibili. I parametri avanzati di archi, cavi e ortotropi rimangono quelli impostati nel progetto.

### Impostare range utili

Il pulsante Suggerisci campate dalla lunghezza propone l’intervallo ricavato dai campi usuali delle famiglie. È un punto di partenza: il controllo preventivo usa la luce media e non garantisce che le luci finali, dopo la distribuzione e l’ostacolo, siano ammesse. Se hai bloccato il numero delle campate, mantieni minimo e massimo compatibili con quel numero.

Il minimo di altezza si riferisce alla campata; il massimo si applica anche all’altezza dell’impalcato sopra le pile. Zero significa nessun limite aggiuntivo, non una sezione di altezza nulla. Per un cassone variabile un limite massimo può quindi escludere una geometria la cui altezza in mezzeria sembra compatibile.

Per le griglie percentuali, 100 significa la regola automatica della combinazione. Con altezza 100–120 e passo 10 si esaminano 100, 110 e 120%. Con pali 100–150 e passo 25 si esaminano tre lunghezze riferite alla classe di terreno. Se la fondazione è bloccata, la relativa griglia è disabilitata. Non sono esplorati valori inferiori al 100%.

Come percorso iniziale, usa 2 o 3 valori di altezza e un intervallo di campate motivato dal sito. Se il vincitore cade all’estremo del range, amplia il campo solo dove fisicamente plausibile e ripeti. Se interessa affinare l’altezza, riduci il passo vicino ai risultati senza superare 11 valori nella griglia. Non è necessario usare subito tutti i 50.000 tentativi ammessi.

Una griglia più fitta aumenta il numero di combinazioni ma non elimina i limiti del modello. Se mancano dati su varo, pali o traffico, è spesso più utile ripetere scenari con ipotesi diverse che usare molti decimali sulla stessa ipotesi incerta.

### Avviare e leggere l’avanzamento

Premi Avvia ottimizzazione. Il motore comunica i nuovi tentativi in gruppi; la preview segue il migliore provvisorio quando cambia, al massimo due volte al secondo. Non viene ridisegnato ogni candidato. Il progetto corrente cambia soltanto quando premi Applica soluzione selezionata.

La barra indica quante combinazioni sono state calcolate e quante sono state ammesse rispetto alla griglia prevista. Il totale previsto può diminuire di fatto per salti e duplicati. Alla fine sono esposti tentativi calcolati, ammessi, geometrie distinte ed esclusi. Non sommare le occorrenze dei motivi di esclusione per ricavare il numero degli esclusi, perché uno stesso tentativo può violare più condizioni.

Interrompi annulla la ricerca senza applicare una soluzione. Una modifica agli input, al listino o ai vincoli rende obsoleta la ricerca: ANTHEA richiede di ripeterla. Non usare una graduatoria calcolata con prezzi vecchi per interpretare il costo di un progetto modificato.

### Usare i grafici

Il grafico Variabili esplorate ha in ascissa il numero del tentativo e permette di visualizzare costo, CO₂, altezza, campate, lunghezza pali, tipologia, schema di pila, fondazione e continuità. I punti grigi identificano tentativi esclusi; le quote non calcolabili non sono disegnate. Nei grafici economico e ambientale la linea scura indica il minimo progressivo, non una curva di convergenza strutturale.

L’ordine dei tentativi deriva dall’enumerazione del programma. Non interpretare una successione di punti come un percorso continuo di trasformazione del ponte, né come una misura della sensibilità di una sola variabile se ne cambiano più di una. Per studiare una variabile, blocca le altre e ripeti una ricerca dedicata.

La Famiglia di soluzioni plausibili rappresenta costo e CO₂ delle geometrie ammesse. In basso a sinistra si trovano i valori minori. L’anello identifica Pareto, la stella il migliore per l’obiettivo e la croce il progetto corrente. Filtra per famiglia o attiva Solo frontiera Pareto; clicca un punto per esaminarlo anche quando non compare fra le prime N righe.

![Nuvola costo e CO₂ ricavata dai risultati riproducibili dell’esempio](../documentazione/Bridge_Design/figure/costo-co2.png)

Il grafico qui riportato è generato dai dati della ricerca descritta nel capitolo successivo. Nella finestra gli stessi concetti sono interattivi. Una nuvola non sostituisce il controllo del prospetto: due punti vicini possono avere numeri di appoggi, dimensioni o difficoltà costruttive molto diversi.

![Variazione di altezza e costo durante l’enumerazione dell’esempio](../documentazione/Bridge_Design/figure/traccia.png)

### Esempio guidato da centoventi metri

Costruisci un riferimento con i valori seguenti. Mantieni gli altri parametri ai valori iniziali della revisione documentata: un archivio precedente potrebbe avere un listino diverso. L’esempio numerico è anche conservato in input-riferimento.json nelle evidenze dell’attività.

| Campo | Valore |
| --- | --- |
| Lunghezza e quota | 120 m e 12 m |
| Corsie e larghezza corsia | 4 e 3,65 m |
| Banchine spartitraffico barriere | 1,50 m per lato 1,60 m 0,50 m per lato |
| Larghezza totale attesa | 20,20 m |
| Ostacolo e terreno | Nessuno e Roccia |
| Famiglia e campate | Travi a I in c.a.p. e 3 |
| Continuità | Attiva |
| Schema pila | Setto |
| Fondazione | Automatica che in questo caso adotta plinto diretto |
| Quote automatiche | Altezza sezione pila e fondazione a zero |
| Estremi | Entrambi su spalla |

Il riferimento restituisce costo 2.332.503,86 euro e CO₂ 1.371,939 t. Le tre luci non sono 40 m ciascuna: con continuità e tre campate le terminali hanno peso 0,8, quindi le luci sono circa 36,923 + 46,154 + 36,923 m. Questo è il primo controllo da fare per capire quale ponte si sta confrontando.

Nella ricerca scegli Costo minimo; blocca Continuità, Schema pila e Fondazione. Lascia libere Tipologia, Numero campate, Altezza e Dimensioni della sezione. Imposta campate da 1 a 8, altezze da 100 a 120% con passo 10, limiti assoluti d’altezza a zero. La griglia dei pali non interviene perché la fondazione è bloccata.

Il risultato atteso è 91 tentativi, 70 ammessi e 69 geometrie distinte. Il primo candidato è Cassone in c.a.p., due campate da 60 m, altezza 2,565257 m, costo 2.049.525,73 euro e CO₂ 1.287,035 t. Il risparmio rispetto al riferimento è circa 282.978 euro, pari al 12,13%; la riduzione ambientale è circa 84,904 t, pari al 6,19%. Le molte cifre servono a ripetere l’esempio, non descrivono la precisione della stima reale.

Ripeti con CO₂ minima e poi con Compromesso. In questo caso vince la stessa geometria; non aspettarti sempre questa coincidenza. Puoi cambiare N per osservare altre soluzioni senza eseguire nuovamente il calcolo. Apri il prospetto della selezione e confronta altezza, sezioni, numero di pile e quantità prima di applicarla.

![Differenze di costo fra il riferimento e alcuni scenari dell’esempio](../documentazione/Bridge_Design/figure/scenari.png)

Se imposti un minimo di cinque campate, il riferimento a tre campate è escluso. Vince una soluzione a T in c.a. con cinque campate e costo 2.657.209,43 euro. Un vincitore più caro del riferimento è quindi possibile quando il riferimento non soddisfa i nuovi vincoli. Leggi sempre la riga che descrive le esclusioni del progetto corrente.

Moltiplicando tutti i prezzi per 0,8 o per 1,2, senza cambiare altro, la geometria vincente deve restare la stessa, la CO₂ deve restare invariata e il costo deve diventare rispettivamente 1.639.620,59 e 2.459.430,88 euro. Questo è un controllo di coerenza economica, non uno studio completo della sensibilità: per quest’ultimo occorre variare separatamente le voci più incerte, non soltanto tutto il listino dello stesso fattore.

### Selezionare applicare e conservare le alternative

La tabella ordina le soluzioni secondo il punteggio non arrotondato. Se due righe mostrano lo stesso punteggio stampato, possono differire nelle cifre successive. Sono mostrati posizione, famiglia, campate, altezza, costo, differenza dal riferimento, CO₂, punteggio e appartenenza a Pareto.

Seleziona una riga o un punto della nuvola e leggi Variazioni rispetto al progetto corrente. La tabella confronta luci, continuità, fondazione e quote tecniche adottate. Controlla anche gli avvisi: un candidato ammesso conserva comunque i limiti generali del modello e le voci di computo mancanti.

Premendo Applica soluzione selezionata, il progetto adotta gli input del candidato. Se non esiste già un’alternativa A, ANTHEA conserva il progetto precedente come A. Se A esiste, non viene sostituita automaticamente: controlla che il confronto A / B rappresenti ancora ciò che vuoi confrontare. Dopo l’applicazione rivedi Sezioni e quote e salva il documento con un nome che renda riconoscibile la variante.

La ricerca completa non va considerata un registro permanente nel file di progetto. Per documentare la decisione salva il progetto di riferimento e le alternative scelte, esporta i loro computi e annota obiettivo, vincoli e griglie usati. L’export del progetto descrive la soluzione corrente; non sostituisce automaticamente un report dell’intera nuvola di ottimizzazione.

### Prezzi geotecnica e scenari da confrontare

Prima di concludere prepara almeno uno scenario di riferimento e uno cautelativo motivato dai dati disponibili. Varia, per esempio, lunghezza dei pali, resistenze geotecniche di riferimento, costo della carpenteria, incidenza di montaggio e necessità di opere provvisionali. Se il vincitore cambia facilmente, conserva più alternative da approfondire invece di attribuire precisione eccessiva al primo posto.

Non usare la pressione convenzionale del terreno come se fosse automaticamente una portanza certificata. Un aumento della lunghezza del palo accresce l’attrito laterale nel modello, ma non simula il passaggio a uno strato reale. Il progetto geotecnico deve confermare lunghezza, capacità, gruppo, cedimenti e risposta trasversale. Per un ponte in alveo, il fatto che le pile evitino l’ostacolo disegnato non dimostra compatibilità idraulica.

Leggi le note accanto ai prezzi. Pali e cls hanno inclusioni diverse; appoggi e giunti dipendono da forze e movimenti. Gli oneri aggiuntivi e gli imprevisti sono riserve, non l’insieme delle lavorazioni mancanti. Per le famiglie speciali un’offerta di sistema può includere montaggio, terminali e protezioni già computati in altre righe: evita duplicazioni.

### Usare le famiglie speciali

Arco metallico con catena e reticolare richiedono campate indipendenti. Se tieni bloccata una continuità attiva, queste famiglie non entrano nella ricerca. Per confrontarle devi liberare la continuità o impostare correttamente il riferimento. Il modello comprende arco con catena, non tutte le possibili configurazioni di arco con spinta sulle spalle.

Strallato e sospeso richiedono tre campate, impalcato continuo ed estremi su spalla. Le campate sono L/4, L/2, L/4. I campi di luce usuale si riferiscono alla campata centrale; un ponte complessivamente lungo 150 m non diventa uno strallato ammesso solo perché la lunghezza totale supera 100 m. Le antenne e i cavi hanno parametri avanzati da impostare prima della ricerca.

Per la piastra ortotropa controlla lamiera, altezza e larghezze delle canalette, interasse e spessori del cassone. Per le travi incorporate controlla spazio fra profili e cls sopra e sotto le piattabande. Il prospetto rappresenta un profilo ideale, non un componente commerciale già selezionato.

Su archi, reticolari, strallati e sospesi la mancanza di diagrammi globali non è un errore grafico. Il motore stima equilibri e quantità senza risolvere la deformabilità globale di queste strutture. Se il loro vantaggio economico è interessante, il passo successivo è un modello specialistico, non l’interpretazione della sola inerzia dell’impalcato come rigidezza del ponte completo.

### Risolvere i messaggi più comuni

| Messaggio o situazione | Significato | Azione utile |
| --- | --- | --- |
| Nessuna soluzione ammessa | Tutti i candidati violano almeno un filtro | Apri i motivi e modifica il vincolo pertinente |
| Griglia troppo estesa | Superati 50.000 tentativi previsti | Blocca variabili o aumenta il passo |
| Altezza inferiore alla regola | Quota bloccata troppo bassa | Rivedi altezza o numero di campate |
| Pile interferenti con ostacolo | Appoggi nella fascia esclusa | Riduci campate o cambia schema |
| Fondazione oltre soglia | Assiale superiore al riferimento | Rivedi dati terreno e fondazione |
| Plinto non contiene pali | Griglia dei pali incompatibile | Aumenta il lato imposto o torna in automatico |
| Pila troppo snella o compressa | Soglia orientativa superata | Aumenta la sezione e verifica il modello reale |
| Sollevamento | Reazione negativa non gestita | Rivedi schema oppure analizza dispositivi dedicati |
| Prezzi o dati modificati | La graduatoria è obsoleta | Ripeti la ricerca |
| Vincitore più caro del riferimento | Il riferimento può essere escluso | Leggi le esclusioni prima di valutare il risparmio |

Non eliminare un messaggio aumentando arbitrariamente una resistenza geotecnica o azzerando una voce di costo. Correggi un dato solo quando hai una ragione documentabile. Se la combinazione resta incompatibile, conserva il rifiuto fra gli esiti dello studio.

### Esportare un confronto leggibile

Usa Esporta quantità CSV per il computo della soluzione corrente e l’export delle quote per ricostruirne dimensioni e sviluppi. Il pulsante Immagine PNG conserva il disegno; la relazione Word del progetto riporta geometria, ipotesi, quantità e avvisi. Per una consegna tecnica aggiungi il PDF e controlla che tabelle, unità e figure siano leggibili.

Accompagna ogni alternativa con obiettivo, dati del sito, blocchi attivati, griglie, listino, fattori ambientali e versione del motore. Riporta numero dei tentativi, delle geometrie ammesse e motivo della selezione. Se scegli la terza alternativa perché più costruibile, scrivilo esplicitamente: la posizione in classifica descrive soltanto il criterio scelto dal software.

### Quanto fidarsi dei risultati

La revisione documentata ha superato 44.259 asserzioni della suite generale. Una nuova verifica indipendente, senza sito e senza librerie ANTHEA nel calcolo dei valori attesi, ha controllato 100 travi con un secondo solutore FEM e quantità, costi e graduatoria di 26 geometrie di un caso imposto. Sono stati eseguiti 80.133 confronti numerici e corretto un difetto di arrotondamento che poteva aggiungere 5 cm a un’altezza esatta.

Questi riscontri aumentano la fiducia nella correttezza delle equazioni implementate e della ricerca discreta. Non validano automaticamente le incidenze convenzionali, i costi consuntivi o la sicurezza di un ponte reale. I 1.000 casi storici del sito avevano invece mostrato differenze e non dimostrano equivalenza fra i due programmi. Il rapporto teorico della stessa revisione contiene numeri, metodi e percorsi delle evidenze.

Prima di scegliere definitivamente verifica che la configurazione sia fisicamente costruibile, che il modello strutturale dedicato confermi le sezioni e che la valutazione geotecnica confermi le fondazioni. Aggiorna poi quantità e prezzi con le informazioni nuove e ripeti il confronto. Questo passaggio permette al predimensionamento di accompagnare il progetto senza attribuirgli prestazioni che non calcola.

## Wiki e centro della conoscenza

Wiki è il terzo ambiente di Anthea insieme a Progetti e Moduli singoli. Raccoglie il Manuale di ingegneria e le Guide Anthea in una navigazione e una ricerca comuni. I contenuti sono ricavati dalle due guide globali, evitando una seconda documentazione indipendente.

### Cercare e scegliere un percorso

Apri Wiki dalla navigazione principale o dalla Home. La ricerca considera titoli, testo, formule, categorie e sinonimi italiano-inglese; per esempio buckling trova contenuti sull'instabilità. Ogni risultato indica se appartiene al manuale teorico o alla guida applicativa. Le macroaree con contenuti aprono un elenco di capitoli; le aree ancora prive di pagine sono dichiarate in preparazione.

### Leggere e riprendere

L'indice laterale raccoglie le sezioni della pagina. Seleziona una voce per raggiungerla; la sezione corrente è evidenziata durante lo scorrimento. Copia collegamento produce un indirizzo Wiki interno leggibile. Segna sezione letta registra una scelta esplicita: la posizione di lettura non equivale a un apprendimento verificato.

Continua a leggere mostra le ultime pagine e la posizione approssimativa; riaprendo una pagina viene ripristinata la sezione visitata. Il progresso è memorizzato localmente nel profilo Windows, separato dai documenti di calcolo. Su finestre strette i pulsanti Indice del manuale e delle guide e In questa pagina mostrano i menu laterali. Non occorre una connessione per consultare i contenuti incorporati; i riferimenti web aprono il browser.

### Passare dalla teoria al calcolo

Prova in Anthea apre un modulo esistente oppure crea un foglio con i dati dell'esempio. Il documento corrente segue il normale controllo di salvataggio. Aprire il modulo senza esempio riprende la scheda già attiva, quando disponibile. Il comando Come funziona nella barra del modulo apre la guida pertinente e permette di tornare al calcolo con i dati conservati.

I pulsanti ? accanto a copriferro e azioni della sezione offrono una definizione breve e aprono la sezione specifica della Wiki. Guida progetti nella barra dei progetti apre le procedure della gestione del lavoro. Il precedente comando Modello e dati comuni raggiunge ora la teoria pertinente nella stessa Wiki.

Per gli elementi Beam, il percorso consigliato è leggere il modello, confrontare reazioni e momento con l'esempio manuale, aprire la Sezione in c.a. e modificare altezza o verso del momento. La Wiki distingue l'analisi della trave dalla verifica della sezione: non introduce un nuovo solutore FEM generale.

### Progetti e problemi frequenti

Le procedure di creazione, rinomina, duplicazione e revisioni restano nei capitoli Progetti della stessa guida globale e sono indicizzate nella Wiki. Per un calcolo usa sempre Salva o Salva con nome: il progresso della Wiki non salva i dati del modulo. Se una ricerca non trova il termine, prova un sinonimo o una parte della parola; le guide storiche integrate mantengono le loro date e il proprio campo di validità.

## Modulo Sezione in c.a.

Questo modulo verifica una sezione in calcestruzzo armato a partire da geometria, materiali, armature e azioni assegnate. Permette di esplorare domini resistenti, stati tensionali e deformativi, taglio, torsione e dettagli nei limiti indicati dalle rispettive schede. Non determina le azioni di un telaio completo e non sostituisce l'analisi globale della struttura.

### A cosa serve e quando usarlo

Usalo quando disponi delle terne N, Mx e My per le combinazioni rilevanti e vuoi confrontarle con la risposta della sezione. È adatto anche al controllo di sensibilità: mantenendo le azioni, modifica una dimensione o l'armatura e osserva il dominio. Per un elemento lungo e compresso la verifica della sezione non esaurisce l'eventuale problema di instabilità del sistema.

[Teoria del modello Beam e trasferimento delle azioni](/wiki/manuale/fem/elementi-beam)

### Input geometria e unità

| Dato | Unità | Significato e controllo |
| --- | --- | --- |
| Sezione | scelta | Circolare, Rettangolare, A T; la sezione generica richiede un contorno definito |
| b e h | mm | Larghezza e altezza positive della sezione rettangolare |
| D | mm | Diametro positivo della sezione circolare |
| bf, bw, hf | mm | Ala, anima e spessore ala della T; bw non supera bf e hf è inferiore a h |
| Copriferro netto | mm | Distanza dal bordo alla staffa; non è la distanza al centro della barra longitudinale |
| Discretizzazione circolare | numero | Da 12 a 720 lati, multipli di 4 |

Le dimensioni sono in millimetri anche se le azioni provengono da un modello in metri. Controlla nel disegno che il contorno e tutte le barre restino nella posizione attesa. Un foro o una forma generica devono avere geometria compatibile con le opzioni realmente abilitate, non semplicemente un nome selezionato.

### Input materiali armature e default

Il nuovo foglio rettangolare parte da b = 600 mm, h = 800 mm, copriferro netto 70 mm, calcestruzzo C35/45 e acciaio B450C. L'armatura iniziale è 4Ø20 superiori, 6Ø24 inferiori e 2Ø16 per lato; le staffe sono Ø10 a passo 200 mm. Sono valori iniziali modificabili, non dimensioni minime normative né una proposta esecutiva.

Le resistenze dei materiali sono in MPa, equivalenti a N/mm². La selezione dal catalogo trasferisce i parametri effettivi della voce scelta; controlla il riepilogo dopo una selezione. Numero e diametro delle barre devono descrivere l'armatura presente; le barre devono rimanere nel contorno con copriferro e spaziatura coerenti. Le opzioni specifiche per pali o zone dissipative richiedono il contesto corrispondente.

Scegli la normativa prima di personalizzare i coefficienti. Cambiarla ripristina i coefficienti della nuova classe normativa: ricontrolla eventuali modifiche manuali. I default non attestano l'applicabilità della norma al caso concreto. Le funzionalità predisposte nei coefficienti non implicano che ogni meccanismo sia implementato.

### Input azioni e convenzioni

N è in kN; Mx e My in kNm. Nell'interfaccia corrente N è negativo a compressione. Il workspace migra gli archivi precedenti con compressione positiva; l'importazione delle azioni da un modello esterno richiede invece il controllo esplicito dei segni. Nel piano della sezione, Mx è associato alla coordinata y e My alla coordinata x con la convenzione indicata dal modulo. Mantieni la terna della stessa combinazione e documenta la trasformazione dagli assi del modello esterno.

Il foglio ordinario iniziale mostra N = -2500 kN, Mx = 500 kNm e My = 250 kNm nelle famiglie iniziali, dopo la preparazione del workspace. Il pulsante Apri esempio della Wiki sostituisce questi valori con la combinazione didattica descritta sotto e lascia vuote le altre famiglie. Non interpretare una tabella vuota come una verifica superata.

### Descrizione del calcolo

La sezione integra il contributo del calcestruzzo e delle barre con i legami costitutivi previsti dal metodo selezionato. I domini confrontano le terne di azione con la capacità resistente. Le analisi di esercizio usano invece le proprie ipotesi per tensioni, fessurazione e omogeneizzazione. Non trasferire un coefficiente di utilizzo di un dominio a un controllo di taglio o di fessurazione.

I ricalcoli e le viste dipendono dalla scheda attiva. Aspetta la conclusione del calcolo e controlla warning e combinazione selezionata prima di esportare. Un input visivamente corretto ma non ancora calcolato non rende corrente un risultato precedente.

### Output e lettura dei risultati

| Output | Interpretazione | Controllo dell'utente |
| --- | --- | --- |
| Dominio 3D | Capacità nello spazio N-Mx-My | Assi, segni e posizione della terna di azione |
| Dominio 2D | Sezione del dominio per la scelta attiva | Non confonderla con ogni direzione possibile |
| Tensioni e deformazioni | Risposta delle fibre e delle barre | Fibra compressa, regime e ipotesi costitutive |
| Fessurazione | Risultato della combinazione SLE attiva | Azioni SLE, esposizione e parametri effettivi |
| Taglio e torsione | Controlli dedicati della scheda | Staffe presenti, braccia resistenti e modello |

Leggi il nome della combinazione e il metodo prima del colore o del rapporto. Un esito favorevole riguarda quel controllo con quegli input. Guarda anche la deformata di sezione e il bordo più sollecitato: un segno sbagliato può produrre un risultato plausibile ma riferito all'armatura opposta.

### Warning e problemi frequenti

> ATTENZIONE
> La sezione non verifica da sola instabilità globale, dettagli del nodo, aderenza in ogni possibile giunto o azioni mai assegnate. I limiti delle schede e le funzioni predisposte restano quelli documentati nella guida teorica globale.

Se il contorno non è valido controlla dimensioni, fori e geometria della T. Se le barre non sono dove previsto controlla copriferro netto, diametro staffa e diametro longitudinale. Se le azioni sembrano invertite confronta la terna degli assi e N negativo a compressione. Se un risultato SLE manca verifica che la famiglia abbia combinazioni assegnate. Se un ricalcolo non termina, conserva il file e annota messaggio, input e scheda, senza considerare corrente l'ultimo grafico disponibile.

### Esempio completo dalla Wiki

La trave appoggiata del capitolo Beam ha L = 8 m e q = 25 kN/m, assunto già come azione di progetto. Il momento massimo è qL²/8 = 200 kNm. Apri esempio crea una sezione rettangolare 600 × 800 mm, C35/45, B450C, con le armature iniziali sopra descritte e una sola terna plastica N = 0, Mx = 200 kNm, My = 0.

1. Apri l'esempio e verifica geometria, copriferro e armature nel disegno.
2. Controlla che la combinazione Wiki contenga esattamente la terna 0, 200, 0.
3. Esegui l'analisi del dominio prevista dalla scheda e leggi il punto di domanda rispetto alla capacità.
4. Modifica h mantenendo b e le azioni: confronta il dominio e la posizione delle armature.
5. Inverti Mx e osserva il diverso ruolo dei due bordi armati.
6. Salva il foglio con nome; assegna separatamente combinazioni di esercizio se vuoi studiare fessurazione o tensioni SLE.

Il taglio di appoggio è 100 kN ma non è il taglio della stessa sezione di mezzeria a momento massimo. La Wiki non inserisce una terna simultanea artificiale di massimi provenienti da sezioni diverse. Per il controllo di taglio usa una sezione e una combinazione coerenti, inserendo i dati nella scheda dedicata.

### Teoria collegata e riepilogo

[Elementi Beam](/wiki/manuale/fem/elementi-beam) spiega come ottenere e trasferire le azioni. I capitoli Sezione in c.a. e Calcestruzzo armato della guida teorica descrivono i modelli costitutivi e i limiti del motore. Il comando Apri modulo nella Wiki raggiunge lo stesso strumento disponibile in Moduli singoli; Come funziona nella barra del modulo torna a questa guida.

Il percorso completo richiede un modello globale plausibile, azioni coerenti, una sezione fisicamente realizzabile e verifiche appropriate ai diversi stati limite. Un esempio numerico aiuta a imparare il metodo; la verifica del progetto richiede i dati reali e tutti i meccanismi rilevanti.

## Interpretazione dei risultati e controlli indipendenti

Un risultato è utilizzabile quando sono chiari input, stato limite, metodo e combinazione. Comincia dal titolo della scheda e dagli avvisi, poi confronta il dato decisivo con un controllo semplice. La precisione delle cifre mostrate non misura la precisione del modello.

### Dalla domanda alla capacità

Un rapporto fra domanda e capacità ha significato solo se le due grandezze descrivono lo stesso meccanismo. Non confrontare un momento elastico, una capacità plastica e un limite di fessurazione come se fossero tre versioni dello stesso controllo. Un esito verde riguarda il controllo indicato nella scheda e non l'intera opera.

### Controllo numerico e fisico

Controlla unità e segni con un valore manuale. Per la trave appoggiata dell'esempio Beam, la somma delle reazioni è qL e il momento di mezzeria è qL²/8. Per una sezione osserva quale bordo è compresso. Per un problema geotecnico confronta quote, strati attraversati e verso dell'azione. Se il risultato cambia radicalmente per una piccola modifica, verifica se hai attraversato un limite fisico o una scelta discreta del modello.

### Archiviazione

Conserva il documento modificabile insieme al report e alla revisione degli input. Se un warning dichiara una parte fuori campo, documenta quale controllo aggiuntivo serve. Una tabella vuota o un risultato non aggiornato non costituiscono una verifica favorevole.

## Tutorial dal modello Beam alla verifica di sezione

Questo percorso unisce teoria, controllo manuale e utilizzo del software senza confondere analisi globale e verifica locale. Richiede circa venti minuti e usa solo strumenti presenti in Anthea.

### Capisci e controlla

Apri [Elementi Beam](/wiki/manuale/fem/elementi-beam#esempio-concettuale). Disegna la trave appoggiata, indica L = 8 m e q = 25 kN/m e calcola due reazioni da 100 kN e un momento di 200 kNm. Il carico è già assegnato come azione di progetto dell'esempio. Controlla separatamente che 25 kN/m equivalga a 25 N/mm.

### Calcola e modifica

Usa Apri esempio in Anthea. Nella Sezione in c.a. verifica la terna 0, 200, 0 e le dimensioni 600 × 800 mm. Leggi il dominio della combinazione attiva, poi modifica l'altezza e ripeti l'analisi. Conserva le azioni per isolare l'effetto della geometria. Inverti il segno del momento per osservare il ruolo delle armature superiori e inferiori.

### Osserva e archivia

Confronta i due risultati descrivendo quale ipotesi è cambiata e quale è rimasta assegnata. Non usare il controllo di resistenza per concludere sulla freccia o sulla fessurazione: richiedono analisi e combinazioni proprie. Salva il documento modificabile e annota il riferimento alla pagina Wiki. La [guida Sezione in c.a.](/wiki/guide/moduli/sezione-ca) descrive input, default e problemi frequenti.
