# Guida pratica di ANTHEA

Manuale operativo dei moduli disponibili

Edizione 2 del 30 settembre 2026 — revisione documentale 05

Questa guida accompagna l'utilizzatore dalla creazione del progetto alla lettura dei risultati e alla produzione dei report. Comprende i moduli geotecnici, i materiali, le sezioni in calcestruzzo armato, la sezione composta da ponte e Bridge Design. La revisione 03 integra l'edizione del 26 settembre con H ad anima inclinata e cassoncino disponibili il 27 settembre 2026; la revisione 04 aggiunge le verifiche a torsione, distorsione e diaframmi del cassoncino disponibili il 28 settembre 2026. La guida teorica separata descrive le formule e le scelte di modello; le due guide vanno utilizzate insieme quando si deve motivare un risultato.

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

La revisione 05 aggiunge il modulo muri a mensola e a gravità, con schede Input e Verifiche. La guida specifica muri-sostegno.md e il PDF omonimo in supporto/docs descrivono input, formule e limiti. I documenti vanno usati insieme per controllare il significato dei risultati.

Le stratigrafie di monte e valle sono affiancate, indipendenti o collegate per spessori e proprietà; le profondità partono dalle rispettive superfici. Hlib è la distanza dalla sommità al terreno di valle: Dv=H+t−Hlib. Il terreno davanti al muro entra nei pesi, nei momenti, nelle sollecitazioni della mensola e nel ricoprimento efficace della portanza. La passiva richiede attivazione e frazione mobilitata; è esclusa nel sisma. Gli attriti del muro e della fondazione sono assegnabili oppure ricavati da φcv,k e tipo di interfaccia. Il valore a volume costante va caratterizzato, senza sostituirlo automaticamente con quello di picco.

Valori di calcolo consente di interrogare e modificare gli input e leggere coefficienti effettivi per combinazione, pesi, attriti, pressioni e sollecitazioni. Le forze risultanti dipendono dagli input e non si possono forzare. Le combinazioni sono modificabili: il preset locale è A1+M1+R3, quello globale A2+M2+R2. Azioni eccezionali e sisma Mononobe–Okabe o Wood semplificato sono espliciti. Il terreno del lato selezionato può essere trasferito ai moduli dei pali; la sezione selezionata può essere inviata al modulo c.a.

La stabilità globale Bishop ha un motore separato e un proprio profilo esteso, anche con due colonne profonde e confine verticale assegnato. La precompilazione non prolunga le indagini e richiede conferma del rilievo; parametri profondi, falda, dominio e combinazioni restano interrogabili. Il muro sostituisce il terreno nel proprio volume. Non si aggiungono alla massa globale spinte e reazioni interne. Minimi sul bordo, mancate convergenze e verifiche non disponibili restano segnalati.

Le relazioni Word includono le due stratigrafie e i coefficienti utilizzati. Le guide e gli esempi conservati per l’utente hanno anche PDF verificati graficamente. Cedimenti, spostamenti, verifiche idrauliche, liquefazione e dettagli esecutivi richiedono analisi dedicate: il modulo non emette una verifica complessiva dell’opera. I test della revisione sono documentati in supporto/artefatti/muri-due-colonne-20260930/CONTROLLO.pdf. I confronti MAX precedenti restano parziali e non costituiscono validazione delle nuove opzioni.
