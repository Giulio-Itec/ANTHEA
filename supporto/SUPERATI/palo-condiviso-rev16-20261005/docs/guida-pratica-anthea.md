# Guida pratica di ANTHEA

Manuale operativo dei moduli disponibili

Edizione 5 del 4 ottobre 2026 — revisione documentale 15

Questa edizione integra i contenuti precedenti nel percorso dell'Engineering Handbook. Le procedure correnti e la teoria sono separate dai resoconti di sviluppo. Le fonti integrali e le evidenze storiche restano nell'archivio Rev14; gli indirizzi precedenti della Wiki raggiungono le pagine consolidate. Lo stato editoriale distingue contenuti integrati, pagine revisionate e profili che richiedono ulteriori riscontri normativi.

## Avvio e scelta del modulo

### Avviare la versione aggiornata

Nel repository, Avvia ANTHEA.cmd ricompila il progetto in Release, pubblica nella cartella app e avvia l'eseguibile soltanto se i passaggi riescono. Richiede quindi l'SDK .NET e ANTHEA chiuso durante l'aggiornamento. La distribuzione già pubblicata può essere avviata direttamente da app/ANTHEA.exe. La distribuzione dipendente dal framework richiede il runtime desktop .NET 8. Il sorgente e la distribuzione sono due cose distinte: modificare un file C# non aggiorna un eseguibile già pubblicato.

La Home consente di entrare nel catalogo, riprendere il lavoro della sessione o organizzare i fogli in un progetto. Il pulsante Riprendi conserva l'editor e il suo stato finché l'applicazione resta aperta. Non salva automaticamente un archivio su disco. Prima di chiudere utilizzare Salva o Salva con nome.

### Scegliere il foglio adatto

| Foglio disponibile | Domanda alla quale risponde | Informazione da preparare |
| --- | --- | --- |
| Palo verticale | Quale resistenza assiale geotecnica si ottiene con questi terreni e questa lunghezza | Stratigrafie, falda, tecnologia, azioni e coefficienti |
| Micropalo verticale | Quale resistenza del bulbo iniettato è compatibile con l'abaco scelto | Terreni, p_l, iniezione, diametri, inclinazione e tratto attivo |
| Palo orizzontale | Quale carico limite laterale risulta dal meccanismo di Broms | Terreno, diametro, lunghezza, vincolo e momento resistente |
| Micropalo orizzontale | Quale capacità laterale risulta usando una sezione tubolare CHS | Diametro geotecnico, tubo, materiale, N e terreno |
| Palificata orizzontale | Come cambiano i fattori di riduzione con geometria e direzione del carico | Posizioni, diametro, teoria e interassi rappresentativi |
| Muri di sostegno | Quali verifiche locali e globali sono applicabili al muro | Geometria, materiali, terreni, acqua, azioni e combinazioni |
| Sezione in c a | Come risponde e si verifica una sezione assegnata | Geometria, barre, materiali e combinazioni N M V T |
| Sezione composta | Come si ripartiscono le tensioni in una sezione da ponte attraverso le fasi | Tipo e geometria, soletta, barre, fasi e metodo |
| Bridge Design | Quale configurazione preliminare, quantità, costo e CO₂ sono plausibili | Sito, campate, larghezza, famiglia, fondazioni e listino |
| Calcestruzzo e durabilità | Quali requisiti del materiale e del copriferro derivano dalle scelte assegnate | Esposizioni, vita, materiale, diametri e condizioni esecutive |
| Acciaio per armature | Quali proprietà e diagramma usare per l'armatura | Classe o dati personalizzati e coefficienti |

Le altre voci eventualmente presenti nel catalogo possono essere predisposizioni. Una scheda di materiali non verifica una sezione. La sezione composta non analizza un intero ponte; Bridge Design non trasferisce automaticamente un modello verificato alla sezione composta. Per passare dall'uno all'altro occorre scegliere la sezione locale, assegnare le azioni da un'analisi appropriata e controllare nuovamente le unità.

### Leggere e compilare i campi

Controllare sempre l'unità a fianco del valore. Nei moduli geotecnici le lunghezze sono generalmente in metri; nelle sezioni strutturali e nelle barre sono in millimetri. Una sezione alta 600 mm non va inserita come 0,60. MPa e N/mm² sono numericamente equivalenti; kPa e kN/m² lo sono altrettanto, e 1 MPa = 1000 kPa: per esprimere in kPa un valore dato in MPa occorre moltiplicarlo per 1000.

I campi vuoti servono anche a rappresentare una bozza incompleta. Non sostituire sistematicamente un dato sconosciuto con zero: in alcuni campi zero ha un significato fisico, in altri attiva un automatismo, in altri è un errore. Bridge Design indica esplicitamente «0 = auto» per i parametri che lo consentono. Quando un campo è temporaneamente incompleto, leggere il messaggio della scheda prima di considerare i risultati.

La finestra segue l'ingrandimento di Windows. I pannelli più stretti si dispongono verticalmente; le tabelle possono mantenere uno scorrimento proprio. I divisori nella composizione dei progetti permettono di allargare l'albero o il catalogo. Cercare prima la barra del pannello che contiene il dato: lo scorrimento della finestra e quello della tabella non sono necessariamente lo stesso comando.

## Progetti e gestione del lavoro

### Costruire una struttura leggibile

La pagina di composizione presenta le informazioni del progetto a sinistra, l'albero al centro e il catalogo dei fogli a destra. Creare il progetto, assegnargli un nome utile e aggiungere sezioni. Una possibile struttura per un ponte comprende Materiali, Impalcato, Pila 1 e Fondazioni. Ogni sezione può contenere sia fogli sia sottosezioni; anche il progetto può contenere fogli direttamente.

Trascinare una scheda dal catalogo nel contenitore desiderato. Le miniature aprono i fogli. Il nome di una sezione, oppure Invio, apre il riepilogo. Doppio clic o F2 consentono di rinominare; cliccare fuori dal nome conferma. Il menu contestuale delle sezioni offre rinomina, duplicazione ed eliminazione. Nei fogli la freccia nella barra mostra o nasconde l'albero laterale. Torna al progetto riapre il riepilogo.

Usare nomi che distinguano oggetto e condizione: «Pila 2 sezione base», «Palo 1000 sondaggio S3» o «Impalcato campata positiva». Nomi generici ripetuti rendono difficile capire quale dato sia stato modificato e quale foglio stia governando la condivisione.

### Comprendere i dati comuni

La condivisione è gerarchica e avviene per proprietà compatibili. Per una proprietà prevale il livello più alto che la definisce e che può trasferirla al destinatario. La regola attraversa anche sezioni intermedie vuote. I rami paralleli condividono i dati dei loro antenati comuni, non tutti i dati l'uno dell'altro.

Per esempio, un materiale CLS definito nel progetto può governare i fogli compatibili delle pile e dell'impalcato. Se si vuole deliberatamente un materiale diverso per un ramo, occorre verificare come è organizzato il riferimento superiore: scrivere un valore diverso in basso può produrre un conflitto, senza modificare il riferimento. Il confronto mostra percorso, valore e provenienza dei dati. «Uniforma a questo» agisce nel ramo governato dal riferimento e non promuove dal basso una proprietà già governata da un antenato.

Quando si confermano o si salvano modifiche a un riferimento, le proprietà compatibili possono propagarsi ai discendenti. In assenza di un riferimento superiore, resta la scelta fra aggiornare i fogli dello stesso livello e mantenere il dato locale. I fogli nuovi ereditano ciò che è definito senza ambiguità. Se due riferimenti dello stesso livello sono discordanti, ANTHEA non sceglie silenziosamente uno dei due.

Prima di produrre un report di progetto aprire il riepilogo Controlli e Dati comuni. Un avviso sul copriferro o un conflitto non è necessariamente un errore numerico, ma richiede una decisione esplicita. La condivisione non deve essere confusa con una sincronizzazione universale di ogni campo: gli adattatori dei moduli preservano unità, forme ammesse e significato delle proprietà.

### Spostare duplicare e annullare

Il bordo di un'intestazione indica il riordino prima o dopo un elemento dello stesso gruppo. Il centro di una sezione consente di trasferirvi un'altra sezione con il suo contenuto. Non si possono creare cicli. Un trasferimento fra rami può cambiare i riferimenti comuni: l'anteprima mostra gli effetti su una copia del documento prima di applicarli. Annullare l'anteprima lascia invariato il progetto.

Duplica sezione crea nuovi identificativi per tutti i fogli e le sottosezioni. La copia non acquisisce la cronologia delle revisioni dell'originale. I dati locali sono indipendenti, ma continuano a essere soggetti alle normali regole di condivisione del ramo in cui si trovano.

Annulla e Ripristina del progetto conservano fino a 30 stati della sessione. Ctrl+Z e Ctrl+Y operano sul progetto quando il cursore non è in un campo di testo; nei campi prevale l'annullamento del testo. La cronologia non viene salvata nel file. Bridge Design possiede inoltre un proprio Annulla per le modifiche alla scheda; non coincide con una revisione del progetto.

### Usare le revisioni

Nuova revisione archivia la versione attuale e apre la successiva. Alla prima operazione si conserva Rev. 0 e si passa a Rev. 1. Ogni sezione può avere una numerazione indipendente. Nei fogli viene usata la sezione revisionata più vicina. La nota serve a ricordare il motivo della revisione, per esempio «Aggiornamento falda da indagini» o «Riduzione larghezza impalcato».

Le revisioni archiviate si consultano nella stessa finestra, con gli input protetti e i comandi di visualizzazione ed esportazione disponibili. Tornando alla revisione attuale si ritrovano le modifiche non ancora salvate. Il contesto storico comprende gli antenati necessari; non è un collegamento mutabile ai materiali correnti. Salva, anche mentre si consulta lo storico, conserva l'intero documento corrente con le revisioni.

Eliminare una revisione archiviata rimuove quella versione senza rinumerare le altre. Eliminare l'attuale ripristina l'ultima rimasta e la rende modificabile; riguarda il ramo scelto. I materiali degli antenati del progetto corrente possono differire da quelli del ramo ripristinato: controllare quindi il confronto. L'unica versione rimasta non è eliminabile con il comando delle revisioni.

### Report del ramo e compatibilità degli archivi

Genera report esporta il progetto o la sezione selezionata con i fogli diretti e le sottosezioni nell'ordine dell'albero. Include i riferimenti necessari degli antenati, senza aggiungere i rami esterni. I dati comuni compatibili vengono riuniti; valori discordanti mantengono provenienza e segnalazioni. Le schede incomplete restano riconoscibili nel documento.

Il report di progetto ricalcola su una copia dei dati. Il report del singolo modulo segue invece le condizioni del suo editor: nella Sezione in c.a. occorre attendere il completamento del calcolo corrente. Prima dell'esportazione controlla il riepilogo e conserva il file riapribile oltre al Word.

Gli archivi con revisioni usano il formato 2; quelli precedenti senza revisioni restano leggibili. I risultati dei report storici sono ricalcolati sui dati della revisione selezionata: conservare anche il report originale è necessario per documentare un risultato ottenuto con un'altra versione del motore.

### Controllo della condivisione

Prova la gerarchia su un progetto semplice: materiale al livello progetto, due sottosezioni e un foglio compatibile in ciascuna. Cambia il riferimento superiore e controlla la propagazione; modifica poi un discendente e verifica che il conflitto resti visibile. Un ramo parallelo non deve diventare riferimento dell'altro.

Consulta [convenzioni e confini del calcolo](wiki:architettura-del-calcolo-e-convenzioni) prima di trasferire risultati fra moduli. La condivisione di un materiale non trasferisce automaticamente combinazioni o verifiche.

## Materiali e durabilità

### Preparare il calcestruzzo

Selezionare una o più classi di esposizione realmente pertinenti alla superficie e all'ambiente. X0, XC, XD, XS, XF e XA descrivono fenomeni differenti; non sono livelli successivi di un'unica scala. In presenza di esposizioni combinate il modulo ricerca i requisiti più gravosi fra quelli applicabili. Non scegliere semplicemente la classe con il nome alfabeticamente maggiore.

Controllare la classe resistente proposta, il rapporto acqua cemento massimo, il contenuto minimo di cemento e le eventuali indicazioni sull'aria inglobata. I prospetti adottati sono identificati nella guida teorica e nei riferimenti del modulo. La scheda aiuta a specificare i requisiti; non determina da sola una ricetta di produzione del calcestruzzo, acqua totale, additivi o granulometria ottimizzata.

Per il copriferro assegnare norma selezionata, vita nominale, tipo di elemento, diametro delle barre, dimensione dell'aggregato, tolleranza esecutiva e condizioni aggiuntive. Distinguere il minimo dal nominale. Quest'ultimo comprende il margine esecutivo previsto dalla scelta del foglio. Un diametro maggiore o un requisito di getto controterra può governare anche quando la durabilità richiederebbe meno.

Il copriferro del disegno della sezione e quello richiesto dalla scheda materiali devono riferirsi alla stessa superficie: esterno staffa, superficie della barra longitudinale e asse barra non coincidono. Nel progetto il confronto evidenzia le incoerenze, ma la lettura del dettaglio resta necessaria quando vi sono staffe, più strati o fasci.

### Preparare l'acciaio per armature

Scegliere B450C, B450A, una voce storica o un materiale personalizzato. Verificare E, fy, fu, deformazioni e coefficiente parziale. Il grafico mostra il diagramma associato ai dati: controllare in particolare la presenza o meno di incrudimento e il tratto ultimo. Un materiale personalizzato va nominato in modo da poter risalire alla sua provenienza.

Le classi storiche FeB non sostituiscono la caratterizzazione di un acciaio esistente. L'allungamento a rottura su base A5 non è automaticamente la deformazione ultima utilizzabile nel diagramma costitutivo. Se un dato richiesto non è noto, il foglio deve restare incompleto per quella funzione, anziché completarlo con un valore scelto per ottenere un esito favorevole.

Il trasferimento dei materiali ai fogli compatibili riguarda le proprietà comuni. Il tubo del micropalo orizzontale è un materiale strutturale specifico della sezione CHS. Inoltre un motore può adottare una legge semplificata propria: il calcolo automatico di My del palo orizzontale usa acciaio elastico perfettamente plastico anche se il catalogo dell'armatura contiene informazioni più estese.

## Palo verticale

### Compilazione ordinata

Impostare tecnologia del palo, diametro, lunghezza, peso del materiale, azioni assiali e coefficienti. Compilare poi le stratigrafie. Ogni nuovo sondaggio parte con una riga da completare, non con un terreno automaticamente valido. Inserire nome, spessore, famiglia del terreno, addensamento dove richiesto, pesi di volume e parametri di resistenza.

Per un calcolo drenato occorrono i parametri efficaci. Per il ramo non drenato degli strati coesivi occorre Cu; il programma considera anche la posizione della falda per decidere il tratto nel quale applicare quel ramo. Non usare c′ e Cu come sinonimi. Negli strati granulari il contributo c′ alla resistenza laterale drenata è nullo, anche se il campo contiene un numero.

Indicare la presenza e la profondità della falda. L'effetto sulle tensioni del terreno e l'opzione di sottospinta sul peso proprio sono aspetti distinti. Spuntare la sottospinta solo secondo l'ipotesi di peso immerso che si vuole applicare al palo; non aspettarsi che questo comando definisca da solo la stratigrafia satura.

«Copia in» e «Copia da» permettono di riutilizzare gli strati fra sondaggi creando copie indipendenti. Controllare la destinazione prima di sovrascriverla. Il pulsante meno della riga elimina uno strato; quello della linguetta elimina la stratigrafia. Il colore identifica il terreno nel profilo e non costituisce un parametro di calcolo.

### Coefficienti e gruppo di pali

Verificare K e μ proposti per la tecnologia e l'addensamento. Controllare l'abaco Nq e la posizione del punto: un parametro sul bordo dell'abaco non dimostra che il terreno appartenga al campo sperimentale. Il numero di indagini scelto per ξ non è semplicemente il numero di linguette visibili. Deve rappresentare la base conoscitiva che si intende utilizzare.

Selezionare nessuna riduzione, Converse Labarre, Feld oppure efficienze definite dall'utente. Per i metodi geometrici servono numeri di pali e interassi coerenti. Il risultato è una riduzione della capacità per palo nel modello: non è una verifica completa del blocco di terreno, della distribuzione dei carichi nel plinto o dei cedimenti di gruppo.

### Leggere i risultati

Le curve drenate sono verdi e quelle non drenate viola; compressione e trazione si distinguono anche per tono e tratteggio. Le azioni sono rappresentate separatamente. Alla lunghezza di progetto leggere contributo laterale, punta, resistenza in compressione, resistenza in trazione e relativi coefficienti. La trazione non utilizza automaticamente gli stessi fattori del ramo di compressione.

La punta e il laterale minimo possono provenire da sondaggi diversi. La curva costruita con i minimi delle componenti non rappresenta necessariamente un unico sondaggio reale. Confrontare anche le curve delle singole stratigrafie prima di interpretare il motivo della riduzione.

Se una stratigrafia non raggiunge la quota richiesta, il calcolo non inventa gli strati mancanti. Integrare l'indagine o rivedere la lunghezza. La casella che disattiva il laterale elimina il contributo resistente di quel tratto, ma ne conserva il peso e l'effetto sulle tensioni negli strati sottostanti. È utile per escludere un tratto non affidabile, non per modellare automaticamente l'attrito negativo.

### Esempio di controllo manuale

Per una sola tratta con D = 1 m, lunghezza attiva 10 m e resistenza laterale uniforme τ = 50 kPa, il contributo laterale è π × 1 × 10 × 50 = 1570,8 kN. Questo è un controllo della geometria e delle unità, prima dei coefficienti ξ, γ ed η. Se il foglio restituisce un ordine di grandezza mille volte diverso, verificare MPa contro kPa e metri contro millimetri.

Procedere poi con i parametri realmente variabili del terreno. Non inserire τ = 50 nel campo Cu o c′ aspettandosi lo stesso risultato: quei parametri entrano in formule differenti. L'esempio serve a leggere il contributo restituito, non a sostituire il modello geotecnico.

## Micropalo verticale

### Dati del bulbo e dell'iniezione

Assegnare diametro di perforazione, iniezione IGU o IRS, famiglia di terreno, coefficiente di espansione α e p_l utilizzato dall'abaco. L'interfaccia espone il campo «Pressione p_i = p_l» e il codice assume quell'uguaglianza. p_l è però la grandezza geotecnica dell'abaco: usare il valore della pompa richiede una giustificazione della correlazione, non la sola coincidenza delle unità. Documentare quindi quale misura o interpretazione abbia prodotto il dato inserito.

Definire l'inizio del bulbo resistente. La lunghezza libera o il tratto escluso non produce resistenza laterale del bulbo. La resistenza viene integrata soltanto nelle tratte attive, considerando il terreno attraversato. L'eventuale contributo di punta è una percentuale del laterale: attivarlo non introduce una verifica autonoma della base.

Inserire l'inclinazione rispetto alla verticale e verificare la lunghezza lungo l'asse. I passaggi di strato sono definiti in profondità verticale e vengono convertiti nella lunghezza percorsa dal micropalo. Controllare quindi sia il profilo del terreno sia il riepilogo delle tratte, soprattutto per inclinazioni elevate.

### Tubo e peso

Le dimensioni del tubo servono al calcolo dell'area di acciaio, del volume di boiacca e del peso. Nel modulo verticale non equivalgono a una verifica automatica di instabilità del tubo, della sezione composta acciaio boiacca o del collegamento in testa. Diametro esterno del tubo, diametro di perforazione e diametro espanso del bulbo sono tre misure diverse.

Verificare che p_l rientri nel campo della curva selezionata: il motore rifiuta l'estrapolazione. Un messaggio di fuori abaco va risolto cambiando la base di calcolo o il dato, non forzando il valore al bordo senza motivazione. Nel report conservare curva, tipo di iniezione, α, pressione e tratte: sono le informazioni che consentono di ricostruire la stima.

## Pali e micropali caricati orizzontalmente

### Preparare un modello coerente

Il modulo applica meccanismi limite di Broms. Definire il diametro geotecnico, la lunghezza infissa, la famiglia del terreno, il vincolo in testa e l'eccentricità della forza. Per la testa libera il momento esterno è H × e. La testa impedita richiede e = 0 nel modello disponibile. Un momento indipendente aggiunto alla forza non è gestito come azione generica.

Lo sforzo normale N è positivo a compressione in questi fogli. È una convenzione diversa dalla sezione in c.a. Non copiare il segno senza controllarlo. N entra nel calcolo del momento resistente della sezione quando questo è automatico; non trasforma Broms in un'analisi completa del secondo ordine del palo nel terreno.

I casi omogenei costituiscono il campo più direttamente interpretabile. La stratificazione della stessa famiglia e la falda sono indicate come estensioni sperimentali. Le alternanze di terreni coesivi e granulari non vengono assimilate silenziosamente a un unico terreno equivalente.

### Momento resistente del palo in calcestruzzo

Nel ramo automatico assegnare sezione circolare, diametro, copriferro, staffa, barre longitudinali distribuite uniformemente e materiali. Verificare la posizione degli assi delle barre e il valore di N. La procedura confronta due discretizzazioni; se non raggiunge l'accordo richiesto o esce dal campo ammesso, il momento non deve essere considerato disponibile.

È possibile assegnare My manualmente, corredandolo della provenienza. Registrare quale sezione, N, diagrammi e coefficienti abbiano prodotto il valore. My non è un parametro da aumentare fino a far passare il controllo. La formazione di una cerniera nel modello richiede inoltre una valutazione separata della capacità rotazionale del dettaglio.

### Sezione CHS del micropalo

Inserire il diametro esterno e lo spessore del tubo in millimetri, distinti dal diametro geotecnico in metri. Il momento automatico è disponibile nel campo di classe 1 adottato. La riduzione con N utilizza l'interazione semplificata specificata nella guida teorica. La boiacca non incrementa la resistenza della sezione CHS in questo ramo.

Un tubo classificato fuori campo richiede un altro calcolo di sezione oppure un My documentato. Non basta che l'area del tubo sia grande: snellezza locale, sforzo normale e possibile instabilità globale pongono problemi differenti.

### Interpretare diagrammi e capacità

Leggere Hu, il regime governante, My usato, profondità caratteristiche e diagrammi di pressione, taglio e momento. La pressione limite rappresentata lungo il palo ha unità kN/m; non è una pressione superficiale in kPa. Il passaggio da Hu a resistenza di progetto introduce separatamente coefficienti statistici, parziale di resistenza ed efficienza di gruppo.

Nei regimi granulari corti o intermedi può comparire una reazione concentrata al piede, necessaria per l'equilibrio del modello. Un salto nel taglio in quel punto non è necessariamente un errore grafico. Il risultato non include spostamenti laterali di esercizio e non è una soluzione con molle p y. Se il problema principale è limitare la rotazione in testa o lo spostamento, occorre un modello di deformabilità ulteriore.

### Separare tubo e diametro geotecnico

Nel micropalo il diametro geotecnico D è in metri e governa il contatto con il terreno. Diametro esterno De e spessore t del tubo CHS sono in millimetri e governano la sezione metallica. De deve essere minore di D dopo la conversione delle unità. Il catalogo dimensionale non assegna automaticamente l'acciaio né garantisce disponibilità commerciale.

Il momento automatico richiede un CHS di classe 1 e una forza assiale inferiore alla resistenza assiale plastica. Il riempimento non contribuisce al momento resistente adottato. Per classi diverse il programma non attribuisce automaticamente la duttilità necessaria al meccanismo plastico; il momento manuale richiede una provenienza motivata.

Il calcolo di capacità con Broms è distinto dalla [risposta elastica del palo](wiki:guida-palo-elastico). Non usare un carico limite per dedurre direttamente lo spostamento di esercizio.

## Sezione composta da ponte

### Definire la sezione locale

Nel Pannello di controllo aprire Geometria e scegliere il Tipo di sezione: H saldato, H con anima inclinata oppure Cassoncino. Inserire soletta, carpenteria e armature opzionali. La scelta cambia la geometria, i campi visibili e l'interpretazione delle larghezze; non è una semplice variante del disegno. Queste sezioni appartengono al modulo Sezione composta da ponte, distinto dalle famiglie parametriche del predimensionamento Bridge Design.

L'Altezza libera anima è la distanza verticale netta fra le flange. Lo Spessore anima è misurato perpendicolarmente alla lamiera anche quando questa è inclinata: inserire lo spessore nominale, non la sua proiezione orizzontale. Il programma calcola la lunghezza inclinata e la sezione equivalente necessaria all'analisi. Tutte queste dimensioni sono in millimetri.

La Seconda piattabanda inferiore è disponibile solo per H saldato. Le due piastre sono reali e distinte, con spessore e larghezza propri; la seconda non deve essere più larga della prima. Passando all'anima inclinata o al cassoncino il riquadro viene nascosto e la seconda piastra non partecipa al calcolo, anche se un valore precedente resta nell'archivio. Tornando all'H controllare nuovamente l'opzione prima di ricalcolare.

La larghezza efficace della soletta beff è un dato assegnato. Va determinata esternamente per la sezione e la situazione considerate. Il foglio non ricostruisce dalla sola geometria trasversale tutte le luci equivalenti e le condizioni longitudinali necessarie. Le quote delle barre seguono la convenzione faccia asse mostrata dal controllo: non confonderle con il copriferro esterno della staffa. Nelle fasi della sezione la compressione è negativa e la trazione positiva; le reazioni assegnate per gli appoggi hanno invece l'etichetta specifica positiva a compressione.

### Compilare la sezione con anima inclinata

Selezionare H con anima inclinata e compilare Scostamento anima al piede. Il valore è lo spostamento orizzontale del piede rispetto alla sommità: positivo verso destra, negativo verso sinistra. Non è un angolo in gradi. La piattabanda superiore è centrata sulla sommità dell'anima e quella inferiore sul piede. Le larghezze superiore e inferiore rimangono quelle delle rispettive piattabande.

Come esempio, impostare Altezza totale H = 1855 mm, da cui il motore ricava altezza libera dell’anima 1800 mm, spessore anima 14 mm, scostamento +300 mm, piattabanda superiore 500 × 25 mm e inferiore 700 × 30 mm. Il disegno deve mostrare il piede spostato a destra, lunghezza della lamiera 1824,829 mm e inclinazione 9,462° dalla verticale. Lo spessore orizzontale equivalente è 14,193 mm; questo valore è un risultato, mentre nel campo Spessore anima devono rimanere 14 mm. Cambiando lo scostamento a −300 mm si ottiene la configurazione speculare.

![H con anima inclinata e scostamento positivo di 300 mm](../artefatti/guide_anthea_itec_rev03/interfaccia/sezione_anima_inclinata.png)

Il programma accetta inclinazioni fino a 45° dalla verticale, comprese quelle negative: il valore assoluto dello scostamento non deve superare l'altezza libera. Questo è il campo geometrico dell'implementazione, non una verifica di stabilità della trave. Se compare un errore, correggere il dato senza confondere altezza verticale e lunghezza inclinata.

### Compilare il cassoncino

Selezionare Cassoncino. La carpenteria comprende due anime simmetriche, due piattabande superiori separate e un fondo; la soletta chiude superiormente la cella nella configurazione composta. Interasse anime in sommità indica la distanza fra gli assi delle anime sotto le piattabande superiori. Scostamento anima al piede indica il rientro di ciascuna anima: un valore positivo restringe il fondo, uno negativo lo allarga. L'interasse al piede è quello superiore meno due volte lo scostamento.

Larghezza superiore è la larghezza di ciascuna delle due piattabande. Larghezza inferiore 1 è la larghezza dell'intero fondo. Non inserire nella prima casella la somma delle due flange né nella seconda metà del fondo. Il numero di pioli per fila e i relativi dettagli si riferiscono a ciascuna piattabanda superiore; il modello ripartisce fra le due piattabande il flusso totale di connessione. Per i dati di fatica assegnare i flussi per piattabanda, senza dividere una seconda volta un valore già ripartito.

Per riprodurre l’esempio usare Altezza totale H = 1850 mm, che con le due flange da 25 mm produce altezza libera 1800 mm, spessore anima 14 mm, interasse superiore 1800 mm, scostamento 250 mm, ciascuna piattabanda superiore 450 × 25 mm e fondo 1400 × 25 mm. L'interasse al piede è 1300 mm; le due anime sono lunghe 1817,278 mm e inclinate di 7,907°. Il disegno deve indicare due piattabande da 450 mm, non una sola piattabanda da 450 mm. Nel calcolo N–Mx la larghezza superiore complessiva è 900 mm.

![Cassoncino con due anime inclinate e due piattabande superiori](../artefatti/guide_anthea_itec_rev03/interfaccia/sezione_cassoncino.png)

Il fondo deve contenere gli appoggi delle due anime, considerate con il loro spessore orizzontale; le anime devono restare separate e le piattabande superiori non devono sovrapporsi. Nell'esempio il fondo interno netto è 1285,866 mm e ogni sbalzo esterno è 42,933 mm. Queste larghezze dipendono dallo spessore e dall'inclinazione: non coincidono esattamente con 1300 e 50 mm. Il programma respinge le geometrie incompatibili, ma la loro accettazione non certifica saldature o montaggio. Il comportamento torsionale si verifica soltanto attivando le opzioni di torsione del cassoncino descritte più avanti.

### Leggere le proprietà e riconoscere i limiti

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

### Scegliere consapevolmente il metodo

| Metodo | Impiego operativo | Aspetto da controllare |
| --- | --- | --- |
| Cumulativo | Somma di contributi elastici delle fasi sulla geometria efficace comune | Non rappresenta una memoria costitutiva cronologica |
| Storico lineare | Successione di fasi con deformazioni e riferimento al getto | φ o n del nuovo incremento non rilassa automaticamente tutto il passato |
| Storico non lineare | Evoluzione a fibre con plasticità dell'acciaio e scarico elastico | Sezione lorda e caratteristiche istantanee; niente riduzione locale di classe 4 |

La scelta va fatta prima di interpretare le differenze fra due risultati. Uno storico non lineare non è semplicemente un cumulativo «più preciso» per ogni scopo: introduce fenomeni diversi e non include tutti i controlli locali del cumulativo. I controlli di taglio, connessione e accessori non vengono valutati dai due metodi storici.

### Costruire le fasi

Aprire la scheda fasi e tensioni e inserire gli incrementi in ordine. Un percorso comune comprende peso della carpenteria e getto sulla sezione di acciaio, permanenti successivi sulla sezione composta a lungo termine e variabili sulla sezione composta a breve termine. Il primo carico applicato prima della maturazione non deve beneficiare della soletta che ancora non collabora.

Ogni riga rappresenta un incremento, non necessariamente il totale della situazione. Inserire un totale in ciascuna riga può duplicare i carichi. Assegnare φ oppure n, controllando che il valore di uno derivi coerentemente dall'altro. Scegliere il punto di applicazione di N: baricentro lordo della fase, baricentro efficace aggiornato o riferimento comune. Il momento riportato al riferimento cambia con questa scelta.

Le azioni di SLU devono arrivare già combinate e fattorizzate. Il selettore SLU o SLE modifica i limiti di controllo, non moltiplica automaticamente le azioni per tutti i coefficienti delle combinazioni. Anche una variazione di segno di V nelle fasi si somma algebricamente nella domanda di connessione: verificare il significato delle situazioni cumulate.

Per il ritiro assegnare la deformazione con il segno corretto: un accorciamento è negativo. Se l'unità è microdeformazione, −250 corrisponde a −0,25 per mille. Il modulo tratta l'effetto locale di sezione. Le azioni dovute a vincoli longitudinali dell'intero ponte devono provenire da un modello globale.

### Leggere efficacia tensioni e controlli locali

Selezionare la situazione da visualizzare e distinguere tensioni della fase, contributi precedenti e stato cumulato. Le parti inefficaci dell'acciaio evidenziano la riduzione per instabilità locale sotto tensioni normali. L'anima resistente a taglio non viene automaticamente ridotta nello stesso modo. Leggere area, baricentro, inerzia, coefficienti di omogeneizzazione e residui di convergenza.

Nel metodo cumulativo aprire i controlli di taglio, irrigidimenti, appoggi e pioli. Inserire geometrie reali, pannelli, reazioni, eccentricità, saldature, passi e materiali. Un irrigidimento non idoneo non deve produrre il beneficio di un pannello corto. La connessione richiede anche dettagli costruttivi e, se pertinente, dati di fatica; un rapporto di resistenza favorevole del singolo piolo non esaurisce questi controlli.

La colorazione principale della sezione riguarda le tensioni normali rapportate ai limiti. Non è una mappa completa di instabilità, fatica, torsione o sollevamento della soletta. Per conoscere lo stato di quelle verifiche occorre leggere le rispettive tabelle e gli avvisi.

Con anime inclinate il taglio V inserito nelle fasi rimane il taglio verticale totale della sezione. Non trasformarlo preventivamente nel taglio della singola lamiera: il programma applica V/cos α per l'H inclinata e V/(2 cos α) per ciascuna anima del cassoncino. Con V = 600 kN negli esempi precedenti le domande nel piano delle lamiere sono rispettivamente 608,276 kN e 302,880 kN per anima. Il risultato di resistenza globale viene riportato alla componente verticale totale.

### Torsione distorsione e diaframmi del cassoncino

Con Cassoncino selezionato, in fondo al Pannello di controllo compare il riquadro Cassoncino · torsione, distorsione e diaframmi. Attivare Verifiche a torsione del cassoncino: nella tabella Sollecitazioni diventa modificabile la colonna ΔT [kNm] e ogni scheda di fase mostra Momento torcente T. ΔT è l'incremento del momento torcente della fase nella sezione, ricavato dal modello globale e già combinato come N, Mx e V. Il ritiro non ha momento torcente. Per H saldato e H con anima inclinata la colonna non compare e un valore rimasto nell'archivio non partecipa al calcolo.

Controvento superiore · spessore equivalente t* chiude la cella del cassone di acciaio nelle fasi Solo acciaio: inserire lo spessore della lamiera equivalente al controvento orizzontale posto fra le piattabande superiori. Con t* = 0 il cassone è aperto e la torsione delle fasi di solo acciaio resta da completare, perché la torsione non uniforme della sezione aperta non viene calcolata. Nelle fasi composte la cella è chiusa dalla soletta e t* non interviene. Il flusso nel controvento viene riportato, ma le sue aste vanno verificate a parte.

Per la distorsione assegnare la luce della campata e, se presenti, il passo dei diaframmi intermedi e il loro tipo: Piastra con il suo spessore, oppure Controvento a X con area, raggio d'inerzia minimo e rapporto Lcr/L di una diagonale. Il torcente distribuito m_t e il torcente concentrato T_c derivano dai carichi eccentrici della combinazione esaminata, per esempio corsie caricate da un solo lato. Il programma applica m_t su tutta la luce e T_c nella posizione più sfavorevole, con lo stesso segno. Con luce nulla la distorsione non viene analizzata e resta da completare.

All'appoggio assegnare il torcente trasferito agli apparecchi, l'interasse trasversale degli apparecchi e lo spessore del diaframma d'appoggio. Come la reazione R, questi dati sono inviluppi indipendenti dalle fasi. Quando la verifica dell'appoggio è attiva, la coppia T/e_b si somma a metà della reazione sull'irrigidimento dell'anima più caricata.

![Riquadro delle verifiche a torsione del cassoncino](../artefatti/guide_anthea_itec_rev04/interfaccia/cassoncino_torsione_ingressi.png)

Per riprodurre l'esempio della guida teorica usare il cassoncino descritto sopra con le tre fasi iniziali del foglio, t* = 4 mm e ΔT pari a 200, 300 e 1000 kNm. Nella scheda Verifiche il gruppo Cassoncino · torsione, distorsione e diaframmi riporta per la fase di solo acciaio A0 = 2,829 m² e q = 35,351 kN/m; per le due fasi composte A0 = 3,079 m² e q = 48,712 e 162,372 kN/m. Il flusso cumulato nelle anime e nel fondo è 246,435 kN/m, quello della soletta 211,083 kN/m. Le tensioni tangenziali di torsione sono 17,602 MPa nelle anime da 14 mm e 9,857 MPa nel fondo da 25 mm.

![Flussi di torsione per fase nella scheda Verifiche](../artefatti/guide_anthea_itec_rev04/interfaccia/cassoncino_torsione_risultati.png)

Aggiungendo luce 40000 mm, diaframmi a piastra da 12 mm ogni 5000 mm, m_t = 60 kNm/m e T_c = 600 kNm, il programma individua 7 diaframmi intermedi e calcola al fondo σdw = 14,946 MPa, pari al 20,2% della tensione di flessione: la tensione di distorsione viene quindi sommata nelle verifiche del fondo. Il diaframma intermedio più sollecitato ha τ = 8,394 MPa.

Nella tabella Verifiche la riga Anima · resistenza a taglio comprende il flusso torsionale sull'anima più caricata; le righe Fondo, Soletta, Distorsione e Diaframma riportano i controlli specifici. Restano da completare i controlli privi di dati, per esempio il taglio da torsione nella soletta senza armatura trasversale attiva o la torsione del cassone aperto. I metodi con storico non eseguono queste verifiche e lo segnalano negli avvisi.

### Curve di risposta

La terza scheda permette curve M κ a N fissato oppure N ε a curvatura fissata. Si può partire da uno stato vergine oppure dalla storia di una fase ricostruita dal motore non lineare. Quando si parte da una fase caricata, il ramo di risposta conserva la memoria prevista dal modello e non riparte da zero tensioni.

Controllare la variabile mantenuta costante, direzione dei passi, grandezza degli incrementi e criterio di arresto. Una curva interrotta per mancata convergenza non va completata idealmente a mano per dichiarare una resistenza. Le curve sono caratteristiche e su sezione lorda; non costituiscono da sole una capacità di progetto con instabilità locale inclusa.

La vista separata segue la fase selezionata. F11 la porta a schermo intero ed Esc esce da quella modalità. CSV ed esportazioni delle fibre consentono un controllo numerico dei dati; la precisione del file non è limitata ai decimali mostrati a video.

## Bridge Design

Questa guida accompagna la costruzione di un progetto preliminare, la ricerca delle alternative e la lettura dei risultati di Bridge Design in ANTHEA. Il percorso è pensato per chi deve confrontare tipologie, campate e quantità prima di sviluppare il modello strutturale e il computo completo. Il [modello teorico di Bridge Design](wiki:bridge-design) spiega formule e limiti; qui ogni passo indica cosa inserire, cosa controllare e come interpretare il risultato.

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

## Salvataggio e report

### Conservare dati modificabili

Usare il formato proposto dal modulo per il foglio singolo e .programma per il progetto. I formati dei fogli possono variare per modulo; Bridge Design usa l'archivio .anthea. Gli archivi mantengono input e impostazioni necessarie, mentre CSV, PNG e Word sono esportazioni destinate a lettura, controllo o presentazione.

Utilizzare Salva con nome prima di una variante che si desidera mantenere indipendente. Per la storia interna di un progetto usare le revisioni. I file di progetto con revisioni utilizzano un formato aggiornato e richiedono una versione di ANTHEA compatibile; non risalvarli con un eseguibile precedente senza averne verificato il supporto.

### Report del singolo foglio e del progetto

Il report di un foglio riguarda i dati e i risultati di quel modulo. «Genera report» su progetto o sezione comprende invece i fogli diretti e tutte le sottosezioni, nell'ordine dell'albero. Include indice navigabile, dati comuni, dati specifici, risultati e immagini. I riferimenti degli antenati necessari vengono richiamati anche esportando un solo ramo.

La generazione ricalcola i fogli su copie dei dati. Le schede incomplete restano nel documento con segnalazione; non vengono trasformate in verifiche favorevoli. In presenza di conflitti si può aprire il confronto oppure proseguire conservando le segnalazioni. Prima della consegna leggere sempre le pagine degli avvisi e verificare il ramo e la revisione indicati nel titolo.

Un report storico ricalcola i dati della revisione selezionata con il motore della versione in uso. Per la riproducibilità conservare quindi anche versione dell'applicazione, data del report e archivio originario. Lo snapshot dei dati protegge gli input; non contiene una copia autonoma di ogni vecchio motore di calcolo.

### Conservare dati e risultati insieme

Il file riapribile conserva gli input e, per i progetti, le revisioni. Word, PDF e immagini documentano risultati e configurazione al momento dell'esportazione. Non sostituire il file riapribile con un'immagine della schermata.

Per esportare un ramo usa [Progetti e gestione del lavoro](wiki:guida-progetti-e-gestione-del-lavoro#report-del-ramo-e-compatibilita-degli-archivi). Nella Sezione in c.a. il report distingue gli estremi di tensione dai casi governanti: un inviluppo di massimi non è uno stato simultaneo.

## Percorso completo per un primo progetto

1. Avviare ANTHEA, creare un progetto e assegnargli un nome riconoscibile. Salvare subito l'archivio in una cartella di commessa.
2. Definire i materiali comuni e organizzare i rami prima di moltiplicare i fogli. Controllare il significato della condivisione ai livelli scelti.
3. Per un ponte preliminare aprire Bridge Design, impostare sito, larghezza e famiglia, quindi sostituire il listino. Fissare A e costruire una seconda configurazione.
4. Leggere gli avvisi, le campate effettive e il dettaglio delle quantità. Salvare la soluzione prescelta con una nota che descriva le ipotesi ancora da consolidare.
5. Creare i fogli di verifica locale necessari. Per la sezione composta assegnare geometria e azioni locali da analisi; per le fondazioni inserire indagini e dati geotecnici effettivi. Non trasferire indiscriminatamente i carichi uniformi di Bridge Design.
6. Esaminare risultati, convergenza e verifiche mancanti. Fare almeno un controllo manuale di unità ed equilibrio per ciascun tipo di modello.
7. Risolvere i conflitti dei dati comuni, creare una revisione e generare il report del ramo o del progetto. Conservare insieme archivio, report e identificazione della versione.

Questo percorso può essere abbreviato per un controllo isolato: il progetto non è obbligatorio per usare un singolo foglio. Diventa utile quando più calcoli devono restare coerenti e quando si vogliono confrontare modifiche nel tempo.

## Problemi frequenti e controlli finali

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

La [guida teorica](wiki:architettura-del-calcolo-e-convenzioni) descrive i calcoli della stessa edizione. Prosegui con [Sezione in c.a.](wiki:sezione-in-calcestruzzo-armato), [Sezione composta](wiki:sezione-composta-da-ponte), [Bridge Design](wiki:bridge-design) o [Broms](wiki:capacita-orizzontale-con-broms). Le evidenze e le revisioni precedenti sono descritte in [Fonti e archivio](wiki:tracciabilita-e-riferimenti). I riferimenti normativi vanno letti nelle edizioni identificate dal modulo, insieme alle relative condizioni di applicabilità.

## Muri di sostegno con stratigrafie di monte e valle

La scheda Input definisce muro, terreni e azioni; Verifiche raccoglie gli esiti locali e globali. Prepara separatamente stratigrafie delle spinte, terreno di fondazione e profilo profondo della stabilità globale. Un modello completo per una verifica può essere insufficiente per le altre.

Le stratigrafie di monte e valle sono affiancate, indipendenti o collegate per spessori e proprietà; le profondità partono dalle rispettive superfici. Hlib è la distanza dalla sommità al terreno di valle: Dv=H+t−Hlib. Il terreno davanti al muro entra nei pesi, nei momenti, nelle sollecitazioni della mensola e nel ricoprimento efficace della portanza. La passiva richiede attivazione e frazione mobilitata; è esclusa nel sisma. Gli attriti del muro e della fondazione sono assegnabili oppure ricavati da φcv,k e tipo di interfaccia. Il valore a volume costante va caratterizzato, senza sostituirlo automaticamente con quello di picco.

Valori di calcolo consente di interrogare e modificare gli input e leggere coefficienti effettivi per combinazione, pesi, attriti, pressioni e sollecitazioni. Le forze risultanti dipendono dagli input e non si possono forzare. Le combinazioni sono modificabili: il preset locale è A1+M1+R3, quello globale A2+M2+R2. Azioni eccezionali e sisma Mononobe–Okabe o Wood semplificato sono espliciti. Il terreno del lato selezionato può essere trasferito ai moduli dei pali; la sezione selezionata può essere inviata al modulo c.a.

La stabilità globale Bishop ha un motore separato e un proprio profilo esteso, anche con due colonne profonde e confine verticale assegnato. Il pulsante Stabilità globale apre il percorso; al primo accesso a un profilo vuoto ne prepara i dati e attiva la verifica. Gli strati si inseriscono per spessore, con quota del fondo calcolata automaticamente. La precompilazione non prolunga le indagini: rilievo, terreni profondi e falda del sito vanno controllati e confermati. Il disegno rappresenta anche il terreno sotto il piano di posa. Parametri, dominio e combinazioni restano interrogabili e modificabili.

Conserva nel report le due stratigrafie e i coefficienti effettivi. Per le verifiche aggiuntive consulta [Portanza, cedimenti e armature](wiki:guida-portanza-sismica-cedimenti-spostamenti-e-armature-rev07).

### Avviare la verifica globale

La verifica globale riguarda il possibile scivolamento del muro insieme al terreno sottostante. I soli parametri del terreno di fondazione utilizzati per la portanza non definiscono la stratigrafia necessaria per questa analisi.

1. Premere Stabilità globale nella barra superiore. Se il profilo globale è vuoto, il programma copia i dati locali disponibili, propone superfici orizzontali sui due lati e attiva la verifica. Riaprire il pannello conserva il lavoro già impostato. Prepara dal muro sostituisce invece profilo e strati con una nuova proposta: usarlo solo quando si vuole ripartire dai dati locali.

2. Controllare gli strati di monte e valle, anche sotto la fondazione. Selezionare una riga per completare le proprietà; correggere il rilievo e la linea di falda se diversi dalla proposta. Controllare anche la condizione Drenata o Non drenata e l’opzione Includi il sisma SLV. Il messaggio in alto indica il primo dato mancante; il suggerimento sul messaggio elenca gli altri.

3. Dopo il controllo del sito, spuntare Ho controllato profilo, strati e falda del sito e premere Calcola globale. Il risultato si apre in Verifiche, vista Stabilità globale. Le modifiche geotecniche del percorso annullano la conferma e il risultato precedente. Calcola globale esegue la sola analisi globale anche quando le verifiche locali hanno input fuori campo.

### Inserire spessori e proprietà dei terreni

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

### Controllare rilievo falda sisma e ricerca

Rilievo e falda si modificano nel gruppo dedicato tramite punti x, y. La falda è una linea di quote y assolute rispetto al piano di posa, non una profondità dalla superficie. Il modello proposto va adattato al rilievo reale: non ricava automaticamente pendenze o terreni profondi.

L’attivazione del sisma globale viene ripresa dal muro durante la precompilazione. Il gruppo Falda e sisma espone l’opzione e la sorgente dei coefficienti. Da sito usa i dati del progetto con βs=0,38; in alternativa si assegnano kh e il modulo di kv. Il coefficiente globale non va confuso con quello delle spinte del muro. Se i dati del sito non sono disponibili, occorre completare la sorgente scelta; non viene assunto un valore sismico implicito.

Con Area di ricerca Automatica i limiti orizzontali seguono il rilievo e la profondità si adatta agli strati noti di entrambi i lati, con massimo iniziale pari a 2(H+t). La proposta non dimostra che l’estensione sia sufficiente. Per cambiare i limiti scegliere Assegnata e aprire Limiti e precisione; nodi, conci e raffinamenti sono sempre modificabili. Gli archivi precedenti conservano i limiti già salvati come Assegnati.

### Leggere il risultato e riprodurre l’esempio

Controllare l’Esito di tutte le combinazioni. F è il fattore trovato con i parametri di progetto; il tasso di lavoro è η=γR/F. Il caso iniziale è quello con tasso maggiore, non necessariamente quello con F minore se γR cambia; un caso privo di superficie valida ha priorità. La tabella e il cerchio restano interrogabili per ogni combinazione.

Soddisfatta nel dominio esplorato richiede η≤1 e controlli della ricerca superati. Minimo sul bordo richiede di ampliare l’area, sempre entro rilievo e indagini disponibili. Ricerca incompleta o discretizzazione non convergente impediscono una conclusione favorevole anche con η≤1. Non modificare arbitrariamente i parametri del terreno per ottenere un esito favorevole.

La scala comune è 0–0,50 blu, 0,50–0,70 verde, 0,70–0,90 giallo, 0,90–1,00 arancio, oltre 1,00 rosso; grigio per controlli incompleti. Nel disegno le linee verticali individuano i conci della superficie critica. La selezione nella tabella permette di leggere pesi, pressioni interstiziali, parametri ridotti, resistenze e azioni.

Per riprodurre il caso illustrato aprire supporto/artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato.anthea e premere Calcola globale, senza ripreparare i dati locali. Le proprietà e il dominio completi sono riportati nell’esempio seguente. Il secondo caso statico ha F=1,075737 e η=1,022555: l’esito non soddisfatto è conservato nell’esempio. È un controllo interno ANTHEA; non è un nuovo confronto numerico MAX.

### Esempio salvato e risultati ripercorribili

Apri `supporto/artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato.anthea`. Premi Stabilità globale e poi Calcola globale. Il file contiene già input e conferma per questo esempio didattico. Non premere Prepara dal muro, che sostituirebbe gli strati globali con quelli locali.

Muro a mensola: H=3 m, t=0,45 m, fusto 0,40/0,25 m, mensola di valle 0,80 m e di monte 1,80 m; peso del muro 25 kN/m³. Terreno di valle fino al piano di posa, qk=10 kPa uniforme a monte. Profilo da x=−13,80 m a valle a x=16,80 m a monte; confine delle colonne x=1,20 m. I pesi γ/γsat sono 18/20, 19/21 e 20/22 kN/m³ rispettivamente per Riempimento, Alluvioni e Ghiaia; φ′k è 30°, 28° e 36°, con c′k nullo. Non sono presenti falda e sisma.

Ricerca: uscite x=−13,80/−0,10 m, ingressi x=3,10/16,80 m, profondità 0,10/6,90 m; 9 nodi per direzione, 60 conci iniziali, 4 raffinamenti. Preset statico A2–M2–R2, γR=1,10. Si ottengono:

| Combinazione | F | η=γR/F | Esito |
| --- | --- | --- | --- |
| Globale A2–M2–R2 1 | 1,176315 | 0,935124 | Soddisfatta nel dominio esplorato |
| Globale A2–M2–R2 2 | 1,075737 | 1,022555 | Non soddisfatta |

![Superficie della seconda combinazione: il tasso supera 1 e il cerchio è rosso. L’esempio conserva l’esito sfavorevole per mostrare la lettura della verifica.](../artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato-esito.png)

Le tabelle dei conci sono in `esempio-stratificato-conci.csv`, accanto al modello. Questo è un controllo interno ANTHEA, non un confronto numerico MAX. Cambiando precisione o dominio possono cambiare leggermente la superficie critica e i valori.

## Portanza, cedimenti e armature dei muri: procedura

Le verifiche usano modelli distinti. Prima di attivarle completa geometria, stratigrafie, falda e combinazioni del muro. Un risultato favorevole a scorrimento non sostituisce portanza, cedimenti, stabilità globale o verifica strutturale.

### Scegliere il controllo

| Domanda | Dati da controllare | Risultato da leggere |
| --- | --- | --- |
| Portanza della fondazione | Terreno di posa, carico verticale efficace, eccentricità, inclinazione e sisma | Campo di applicabilità e resistenza per combinazione |
| Cedimento | Parametri deformativi e modello del terreno, combinazione di esercizio | Spostamento e ipotesi, separatamente dalla resistenza |
| Spostamento permanente | Accelerogramma e parametri Newmark richiesti | Spostamento accumulato, soglia e unità temporali |
| Armature | Materiali, sezioni critiche, sollecitazioni, copriferro e disposizione | Proposta, controlli eseguiti e condizioni incomplete |
| Muro a gravità | Materiale, peso e resistenze pertinenti | Equilibrio e verifiche coerenti con calcestruzzo o muratura |

### Procedura e lettura

Apri i controlli pertinenti nella scheda Verifiche e leggi le ipotesi prima dell'esito. Inserisci i dati mancanti; non sostituire un parametro deformativo con un parametro di resistenza. Per il sisma distingue i coefficienti delle spinte da quelli del terreno di fondazione e della stabilità globale.

Calcola armature produce una proposta da controllare, non il dettaglio esecutivo completo. Verifica le sezioni critiche, l'armatura disponibile e le condizioni non coperte. Se trasferisci una sezione al modulo c.a., controlla segni, assi e combinazione del foglio risultante.

Conserva nel report la combinazione, i parametri effettivi e l'eventuale stato fuori campo. Consulta [modelli di portanza, cedimenti e armature](wiki:portanza-sismica-cedimenti-spostamenti-e-armature-rev07) per formule e limiti e [stabilità globale](wiki:muri-di-sostegno-e-stabilita-globale) per il modello Bishop.

### Dati della portanza sismica

In Terreno aprire Portanza sismica. Con Da sito si usa ah/g=ag/g·Ss·St, prima della riduzione β del muro; av/g=±0,5ah/g. In alternativa assegnare entrambe le accelerazioni. Il fattore γRD è modificabile: 1 per sabbia medio densa, 1,15 per sabbia sciolta asciutta. Non è un valore ricavato automaticamente dal solo angolo di attrito.

In Verifiche scegliere una combinazione SISMA e Portanza sismica nel riepilogo. Sono leggibili Nmax, F, N̄, V̄, M̄, limite verticale, interazione, tasso ed esito. Un’accelerazione mancante, un terreno fuori campo o una risultante non ammissibile restano esplicitamente non verificati.

### Dati dei cedimenti

In Terreno attivare Calcola cedimenti finali e inserire, a partire dal piano di posa, nome, spessore e modulo edometrico M di ciascuno strato. M è espresso in kPa: per esempio 30 MPa corrispondono a 30000 kPa. Non viene dedotto da φ o riempito con un valore presunto. La pressione del terreno rimosso è il carico geostatico eliminato con lo scavo, da valutare nel modello scelto; zero è una scelta esplicita.

Limiti iniziali modificabili: 25 mm per cedimento, 0,002 rad per rotazione e 20 mm per spostamento in testa. Sono valori di avvio da valutare per l’opera, non limiti normativi universali. In Verifiche scegliere Cedimenti e spostamenti per la tabella per combinazione, i contributi degli strati e le curvature.

### Importare una storia Newmark

In Azioni aprire Spostamenti permanenti e aggiungere una storia. Scegliere SLD o SLV, inserire ky/g, fattore di scala e limite di spostamento. ky/g è la soglia di inizio scorrimento del muro, da ricavare da un’analisi di equilibrio: non coincide con ag/g e non viene dedotta automaticamente dal coefficiente kh.

Importare un CSV a due colonne separate da punto e virgola: tempo in secondi e accelerazione verso valle in g. È ammessa una prima riga t;a_g e il separatore decimale italiano. I tempi devono essere crescenti. Confermare che storia e scala siano compatibili con sito e stato limite. I campioni restano salvati nel file del muro.

### Disporre e proporre le armature

In Geometria si possono mantenere le facce simmetriche o assegnare due armature indipendenti. La prima faccia è monte nel fusto e inferiore nelle solette; la seconda è valle nel fusto e superiore nelle solette. Rimangono disponibili le due zone verticali separate da h₁.

Ogni zona contiene barre principali, diametro e passo delle secondarie, lunghezza di ancoraggio, sovrapposizione e mandrino. Zero nelle lunghezze significa calcolo automatico, non lunghezza nulla. Il pannello dei dettagli espone aggregato, aderenza, vita nominale, tolleranza del copriferro e collegamenti della giunzione.

Calcola armature cerca diametri e numeri interi di barre entro i limiti impostati. Ogni candidato viene controllato con GPC a N–M, a taglio e in SLE; la proposta usa armature simmetriche per zona, poi modificabili. L’area stimata dalla flessione serve soltanto a scartare candidati impossibili. La verifica finale include i dettagli: una sezione resistente può avere una piega o una giunzione che non entra. In tal caso l’esito lo segnala e può occorrere aumentare lo spessore. La ricerca è interrompibile. Premere Applica proposta per sostituire le barre inserite; prima di applicare restano conservate.

In Vista dei risultati scegliere Armature: si vedono i percorsi delle barre, le pieghe, la fascia di sovrapposizione e le marche. Le barre giuntate sono affiancate lungo lo sviluppo del muro; le proiezioni sono leggermente distanziate sul disegno per leggibilità. Dettagli armature riporta la distinta, fbd, lunghezze richieste e usate, mandrini, quantità e tutti i controlli. I pesi sono stime per metro comprensive di ancoraggi, giunzioni e secondarie. Restano da definire il disegno esecutivo, i giunti di costruzione, i bordi lungo il muro, le interferenze tridimensionali e gli sfridi: la vista non è una distinta di officina.

## Efficienza orizzontale della palificata

Il modulo Palificata orizzontale confronta sei modelli di effetto di gruppo a partire da un'unica geometria di pali identici e da una direzione dell'azione orizzontale. Il risultato è un fattore di riduzione: per Davisson riguarda il modulo di reazione kh o nh; per gli altri metodi è il p-multiplier medio. La scheda non esegue un'analisi laterale completa e non fornisce direttamente la resistenza della palificata.

### Apertura e geometria

Aprire Moduli singoli → Geotecnica → Palificata orizzontale, oppure File → Nuova palificata orizzontale. La scheda può essere inserita anche in un progetto. Il motore numerico è GPCChecker.Geotechnics; ANTHEA gestisce input, risultati, rappresentazione e archivio.

Scegliere in alto a sinistra Rettangolare, Quinconce, Triangolare, Pentagonale, Esagonale oppure Generica. Gli input sottostanti generano automaticamente la disposizione: Nx/Ny e Sx/D/Sy/D per le file; numero di pali sul lato e S/D per il triangolo; anelli, suddivisioni, centro e S/D per i poligoni. Quinconce alterna file di N e N−1 pali con sfalsamento di mezzo interasse. Il triangolo è pieno; i poligoni usano anelli concentrici con suddivisioni crescenti. Le coordinate sono modificabili solo in Generica. X e Y sono attive inizialmente; X− (180°), Y− (270°) e la direzione personalizzata sono facoltative e inizialmente disattivate. Ogni direzione ha risultati e interassi rappresentativi distinti; invertire H può cambiare i coefficienti dei singoli pali anche quando la media della palificata simmetrica resta uguale. Il suo angolo è antiorario da +X. La rotazione della geometria è un input distinto. Sono ammessi da 1 a 500 pali, identificativi univoci e distanze fra centri almeno pari a D.

La pianta mostra pali, coefficienti del metodo selezionato, direzione H, assi parallelo e ortogonale, diametro e interassi disponibili. La fila 1 è quella con maggiore proiezione lungo il verso del carico. Invertendo H si scambiano file anteriori e posteriori. La geometria non viene ruotata quando cambia soltanto il carico.

### Metodo e applicabilità

| Metodo | Risultato | Impiego nella scheda |
| --- | --- | --- |
| Davisson 1970 | Rg di kh/nh | Griglia allineata; interasse parallelo almeno 3D, trasversale almeno 2.5D |
| AASHTO 2014 | p-multiplier per fila | Interpolazione 3D–5D; oltre 5D mantenimento del valore di 5D segnalato |
| FHWA 2018 | p-multiplier per fila | Interpolazione 3D–6D; oltre 6D coefficienti unitari |
| Rollins e FEMA | p-multiplier per fila | Formule logaritmiche; campo sperimentale circa 3.3D–5.65D |
| Reese e Van Impe | p-multiplier palo per palo | Tutte le coppie, anche per geometrie irregolari e H obliqua |
| Caltrans 2025 | p-multiplier modificato | Interazione palo-palo e correzione alfa per fila |

Il riconoscimento automatico accetta una griglia cartesiana completa e uniforme nel riferimento del carico, anche se ruotata rispetto agli assi globali. Le file vengono distinte con tolleranza D per 10 alla meno 6. Per geometrie irregolari o H obliqua, i metodi per file non vengono applicati automaticamente. Aprire Applicabilità e interassi rappresentativi e, solo dopo una valutazione ingegneristica, accettare l'estensione per file proiettate e assegnare S parallelo e S trasversale. Per Caltrans, accettata l'estensione, la media degli interassi fra file proiettate è disponibile automaticamente; si può assegnare un interasse rappresentativo diverso. L'estensione resta identificata negli avvisi.

I due interassi rappresentativi vuoti mantengono il comportamento automatico. Non sono dimensioni che modificano la pianta: cambiano soltanto la schematizzazione per file. Reese usa sempre le distanze reali fra tutti i pali. Il riepilogo elenca anche gli intervalli geometrici proiettati, per rendere verificabile la scelta.

Consenti estrapolazioni è disattivato inizialmente. Abilitarlo permette di ottenere un risultato fuori dai limiti inferiori delle tabelle o dal campo sperimentale di Rollins, con avviso esplicito e coefficienti limitati a zero–uno. Non supera la condizione trasversale di Davisson: sotto 2.5D il metodo resta non disponibile, perché manca una correzione documentata. Per un solo palo tutti i fattori valgono uno. Per un'unica fila trasversale AASHTO, FHWA e Rollins richiedono un interasse parallelo rappresentativo; preferire Reese quando manca una schematizzazione per file giustificabile.

### Risultati e confronto

Il ricalcolo è automatico per tutti i sei metodi e tutte le direzioni attive. Selezionare una riga del confronto per vedere sulla pianta il metodo e la direzione corrispondenti. Il riepilogo mostra minimo, media, massimo e riduzione media. La legenda va da 0 rosso, riduzione 100%, a 1 verde, nessuna riduzione. Il basamento è il contorno convesso esterno oppure un rettangolo; il margine asse–bordo è espresso in multipli di D, inizialmente 1D, equivalente a 0,5D libero dal palo. Il basamento è solo rappresentativo e non modifica i coefficienti. Un trattino significa grandezza non definita, non valore nullo.

La tabella di confronto indica separatamente grandezza, valore e indisponibilità. Selezionare ciascun metodo per leggere fonte, avvisi e dettaglio dei pali. Il dettaglio riporta fila, coordinate proiettate, beta, alfa e coefficiente finale. Alfa riguarda la modifica Caltrans; per gli altri p-multiplier vale uno. I coefficienti di Davisson non devono essere letti come p-multiplier.

Esempio di controllo: griglia 3 per 3, D = 1 m, Sx = Sy = 3 m, H = 0 gradi. Davisson restituisce Rg = 0.25; AASHTO restituisce Pm medio = 0.50; FHWA restituisce circa 0.5167. Rollins a 3D richiede l'abilitazione esplicita dell'estrapolazione e restituisce circa 0.5887. Questi valori non sono quattro stime equivalenti della capacità del gruppo.

Le modifiche invalidano i risultati precedenti; attendere il ricalcolo automatico prima di esportare. Salva conserva coordinate, opzioni, metodo e interassi rappresentativi nel normale archivio ANTHEA. Risultati JSON esporta il confronto completo. Report Word include geometria, risultati per metodo e per palo, fonti e limiti; può essere convertito in PDF con il normale flusso documentale. Le due guide globali sono distribuite anche in PDF.

## Risposta elastica del palo orizzontale

Il modulo Palo orizzontale comprende la scheda Risposta elastica · trave su molle. Calcola spostamenti e sollecitazioni sotto la forza assegnata. La scheda Capacità laterale conserva Broms e l'estensione stratificata preesistenti, i cui diagrammi si riferiscono al carico limite. I due risultati rispondono a domande diverse e hanno parametri distinti salvati nello stesso foglio. La nuova analisi è disponibile anche nel modulo Micropalo orizzontale, con EI assegnato esplicitamente.

### Compilazione dei dati

Aprire Moduli singoli → Geotecnica → Palo → Orizzontale, quindi Risposta elastica · trave su molle. Inserire diametro geotecnico D, lunghezza totale testa–punta e lunghezza libera sopra il terreno, in metri. La lunghezza immersa è la differenza fra totale e libera. Il comando Copia geometria, H e stratigrafia dalla capacità riprende diametro, lunghezza infissa, forza, vincolo di testa e prima stratigrafia; imposta tratto libero e momento a zero e lascia vuote le rigidezze del terreno. La copia è esplicita e sostituisce gli strati elastici. Le successive modifiche delle due analisi rimangono indipendenti.

Assegnare EI in kN m² e descriverne l'origine, ad esempio E, I e ipotesi di fessurazione. Non usare il momento resistente My al posto di EI. Il diametro controlla la larghezza di interazione col terreno; non ricalcola automaticamente la rigidezza strutturale. Per un micropalo specificare se EI riguarda il solo tubo o una sezione composta e giustificare l'ipotesi adottata.

H è applicata alla testa del modello. Inserire alternativamente il momento C oppure l'eccentricità e, da cui C = H e; entrambi diversi da zero costituiscono errore. e è un braccio equivalente rispetto alla testa, non la lunghezza libera del palo: questa è già rappresentata geometricamente. Sono ammessi carichi positivi, negativi e nulli. La testa può ruotare liberamente oppure avere rotazione impedita; lo spostamento orizzontale resta libero. La punta è libera per impostazione iniziale; cerniera e incastro devono essere scelti esplicitamente.

### Assegnazione delle rigidezze

Selezionare uno strato nella tabella: il pannello sottostante mostra soltanto gli input della sua modalità. Assegnare nome, spessore e condizioni di impiego. Il nome del terreno non seleziona una rigidezza. Coprire tutta la lunghezza immersa; il terreno oltre la punta non contribuisce. Sono disponibili:

| Modalità | Input espliciti | Valore adottato |
| --- | --- | --- |
| kh costante assegnato | kh [kN/m³] | k = kh D |
| Reese–Matlock con nh assegnato | nh [kN/m³] | kh = nh z/D; k = nh z |
| Tabella 14.5 per sabbie | Stato di addensamento e falda | nh tabellato, distinto per sabbie immerse e non immerse |
| Tabella 14.6 per coesivi | Riga e autore; valore scelto [N/cm³] | Conversione del valore scelto; nessuna media automatica |
| Correlazione A γ/1,35 | Addensamento, A, γ e γsat [kN/m³] | nh in kN/m³; sotto falda γ′ = γsat − γw |
| k distribuito assegnato | k [kN/m²] | Nessuna ulteriore moltiplicazione per D |

Il testo associa kh costante alle argille sovraconsolidate; la legge lineare di Reese e Matlock (1956) ai terreni incoerenti e alle argille normalmente consolidate o debolmente sovraconsolidate. Indicare nelle condizioni dello strato l'applicabilità, il drenaggio e il livello di deformazione della rigidezza adottata. Queste indicazioni non costituiscono un'assegnazione automatica in base al nome del terreno.

La tabella 14.6 richiede un valore esplicito nell'intervallo della singola riga. Il pannello mostra autore e limiti; anche il valore singolo della torba di Davisson deve essere confermato con l'inserimento. La correlazione richiede A esplicito, mostrando intervallo e valore consigliato della tabella 14.5. Le modalità assistite permettono un override di nh in kN/m³ con motivazione obbligatoria: risultato e report conservano sia il valore di base sia quello adottato. Non mescolare righe di autori diversi.

Attivare la falda e inserire la sua profondità dal piano campagna, oltre a γw (inizialmente 9,81 kN/m³). La tabella delle sabbie passa al valore immerso; la correlazione usa γ′. Le modalità manuali e la tabella dei coesivi conservano il parametro assegnato. Il riepilogo mostra per ogni tratto modalità, profondità, parametro, fonte e applicabilità. Gli archivi precedenti con kh,rif mantengono il valore, ora indicato come nh della legge lineare.

z parte dal piano campagna, esclude il tratto libero e non si azzera alle interfacce. Il raccordo a strati è una convenzione numerica dichiarata, non una prescrizione originale del libro. Il modello crea nodi alla falda quando cambia la legge; i diagrammi conservano i due valori alle discontinuità. La linea tratteggiata azzurra identifica la falda. CSV, JSON e relazione contengono anche la determinazione dei parametri. Il catalogo completo è nella guida teorica.

Carlo Viggiani, Fondazioni, scansione locale fornita dall'utente: pagina PDF 237, pp. stampate 464–465 (§14.4.1); PDF 238, pp. 466–467; PDF 244, pp. 478–479 (equazione 14.25 e tabelle 14.5–14.6). Sono state lette visivamente anche le pagine adiacenti PDF 243 e 245, pp. 476–477 e 480–481. L'edizione non è identificabile nella scansione: la prima pagina contiene la fine della prefazione, datata dicembre 1998, senza frontespizio o colophon. Tale data non viene usata per dedurre l'edizione. La trattazione è di Viggiani; le correlazioni sono attribuite agli autori indicati nel libro. Gli articoli originali non sono stati consultati.

### Lettura e conservazione dei risultati

Il ricalcolo è automatico; ogni modifica invalida immediatamente i risultati precedenti. I dati non validi impediscono CSV, JSON e report del risultato attivo. Il programma confronta la mesh iniziale con una mesh dimezzata e mostra i risultati della seconda. Le variazioni relative di y in testa, massimo assoluto M e massimo assoluto V devono essere al più 0,1%; altrimenti compare Raffinare la mesh. Questa è una verifica numerica fra due mesh, non una garanzia assoluta dell'errore o dell'adeguatezza geotecnica.

Il profilo a sinistra mostra terreno, tratto libero e vincoli. I sei diagrammi affiancati hanno la stessa profondità: k, y, θ, V, M e reazione q. Le scale orizzontali sono indipendenti e dichiarate. I salti del terreno vengono conservati con valori sui due lati e tratto puntinato, senza interpolazione attraverso lo strato. Le barre laterali del palo rappresentano schematicamente il sostegno distribuito; non sono molle concentrate impiegate dal solutore.

Il riepilogo riporta minimi, massimi con segno, massimi assoluti e rispettive quote x dalla testa, oltre a y e θ in testa, reazioni dei vincoli e residui di equilibrio. La tabella e il CSV riportano anche z dal piano campagna, indice dello strato, kh e k con unità. Above significa limite dal lato superiore, Below dal lato inferiore, Interior interno all'elemento; strato zero indica il tratto libero. I valori sono riportati da entrambi i lati degli estremi degli elementi, anche quando coincidono.

Esporta tabella CSV conserva la precisione numerica e le convenzioni dei segni. L'esportazione generale JSON/Word usa l'analisi selezionata: per esportare la capacità tornare alla scheda Capacità laterale. Il report elastico conserva input, fonti assegnate, estremi, equilibrio, convergenza e tabelle. Archiviare il foglio ANTHEA conserva entrambe le analisi; il risultato viene ricalcolato all'apertura.

### Esempio riproducibile e limiti

Assegnare D = 1 m, lunghezza totale 30 m, tratto libero nullo, EI = 50000 kN m² con origine Benchmark numerico, H = 100 kN, C = e = 0, testa e punta libere. Un unico strato spesso 30 m ha kh costante = 10000 kN/m³, fonte Esempio numerico assegnato. Con passo iniziale 0,50 m il risultato a passo 0,25 m è ytesta = 0,0094574084 m e Mmax = 68,178634 kNm a x circa 1,660914 m. Questi parametri costituiscono una prova del codice, non valori consigliati per un terreno reale.

La soluzione non comprende plasticità, distacco, curve p-y, effetti della forza assiale, secondo ordine, consolidazione, ciclicità o gruppo. Le molle elastiche reagiscono in entrambi i versi. La convergenza non verifica la capacità del terreno né le resistenze di sezione. I coefficienti della palificata non vengono trasferiti automaticamente: Rg di kh/nh e p-multiplier non sono intercambiabili senza un modello esplicito.

## Wiki e centro della conoscenza

### Engineering Handbook

La copertina presenta dodici capitoli numerati: Fondamenti, Scienza e tecnica delle costruzioni, Materiali, Calcestruzzo armato, Acciaio, Geotecnica, Ponti e infrastrutture, Ingegneria sismica, FEM, BIM, Computational Design e Anthea. Le schede mostrano anteprime degli articoli e il collegamento Esplora il capitolo; si dispongono su una, due o tre colonne secondo lo spazio. Inizia dai fondamenti apre il primo percorso. Ogni capitolo presenta introduzione e articoli numerati con livello e tempo di lettura. Le pagine distinguono introduzione, metodo e approfondimento tecnico; i sei piloti revisionati sono Cos'è un ponte, Instabilità di Euler, Fessurazione, Capacità portante, Elementi Beam e guida della Sezione in c.a.

Le pagine correnti consolidano istruzioni e teoria; i resoconti originali di audit e sviluppo sono conservati nell’archivio Rev14. Fonti e archivio spiega la provenienza. I vecchi collegamenti aprono la destinazione corrente pertinente. Contenuto integrato indica una revisione editoriale; Riscontri sulle fonti da completare segnala attribuzioni normative o bibliografiche non ancora confermate sui testi primari.

Usa Chiaro / scuro per cambiare il tema del lettore. Formule e nuovi schemi vettoriali seguono il tema; le immagini storiche conservano la propria tavola chiara. A finestra stretta Indice e In questa pagina sono pannelli alternativi; formule e tabelle larghe scorrono orizzontalmente. Il glossario raccoglie anche SLU, SLE, FEM, DOF, MPC, SRSS, CQC, LTB, p-y e M-N. Gli acronimi alimentano la ricerca; una definizione può rimandare a una sezione che ne spiega i limiti, senza simulare un modulo di calcolo assente.

I prerequisiti precedono il testo; fonti, correlati e navigazione precedente/indice/successivo chiudono la lettura. I nuovi collegamenti usano identificativi centrali: titolo e destinazione vengono risolti dal catalogo. La validazione controlla identità, categorie, capitoli, sezioni, figure e riferimenti; le prove WPF verificano formule e pagine in entrambi i temi.

Wiki è il terzo ambiente di Anthea insieme a Progetti e Moduli singoli. Raccoglie il Manuale di ingegneria e le Guide Anthea in una navigazione e una ricerca comuni. I contenuti sono ricavati dalle due guide globali, evitando una seconda documentazione indipendente.

### Cercare e scegliere un percorso

Apri Wiki dalla navigazione principale o dalla Home. La ricerca considera titoli, testo, formule, categorie e sinonimi italiano-inglese; per esempio buckling trova contenuti sull'instabilità. Ogni risultato indica se appartiene al manuale teorico o alla guida applicativa. I dodici capitoli aprono indici ordinati delle voci disponibili; i contenuti storici utili sono integrati nelle pagine correnti e gli originali restano nell’archivio documentale.

### Leggere e riprendere

Nella copertina il menu laterale è raccolto nel pulsante Indice. Durante la lettura, il menu a sinistra espande gli articoli del capitolo corrente; quello a destra raccoglie le sezioni della pagina. Seleziona una voce per raggiungerla; la sezione corrente è evidenziata durante lo scorrimento. Copia collegamento produce un indirizzo Wiki interno leggibile. Segna sezione letta registra una scelta esplicita: la posizione di lettura non equivale a un apprendimento verificato.

Riprendi la lettura mostra le ultime tre pagine; riaprendo una pagina viene ripristinata la sezione visitata. Il progresso è memorizzato localmente nel profilo Windows, separato dai documenti di calcolo. Su finestre strette i pulsanti Indice e In questa pagina mostrano i menu laterali. Non occorre una connessione per consultare i contenuti incorporati; i riferimenti web aprono il browser.

### Passare dalla teoria al calcolo

Prova in Anthea apre un modulo esistente oppure crea un foglio con i dati dell'esempio. Il documento corrente segue il normale controllo di salvataggio. Aprire il modulo senza esempio riprende la scheda già attiva, quando disponibile. Il comando Come funziona nella barra del modulo apre la guida pertinente e permette di tornare al calcolo con i dati conservati.

I pulsanti ? accanto a copriferro e azioni della sezione offrono una definizione breve e aprono la sezione specifica della Wiki. Guida progetti nella barra dei progetti apre le procedure della gestione del lavoro. Il precedente comando Modello e dati comuni raggiunge ora la teoria pertinente nella stessa Wiki.

Per gli elementi Beam, il percorso consigliato è leggere il modello, confrontare reazioni e momento con l'esempio manuale, aprire la Sezione in c.a. e modificare altezza o verso del momento. La Wiki distingue l'analisi della trave dalla verifica della sezione: non introduce un nuovo solutore FEM generale.

### Progetti e problemi frequenti

Le procedure di creazione, rinomina, duplicazione e revisioni restano nei capitoli Progetti della stessa guida globale e sono indicizzate nella Wiki. Per un calcolo usa sempre Salva o Salva con nome: il progresso della Wiki non salva i dati del modulo. Se una ricerca non trova il termine, prova un sinonimo o una parte della parola; il campo di validità è dichiarato nelle pagine correnti; date e revisioni originarie si trovano nell’archivio documentale.

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

### Domini tridimensionali e bidimensionali

Le schede dominio 3D e dominio 2D permettono di esaminare le resistenze della sezione e il punto richiesto. Scegliere il criterio di ricerca coerente con il problema: mantenere N costante e aumentare il momento non è lo stesso percorso che mantenere costante l'eccentricità. Il coefficiente risultante dipende anche da questo percorso.

Un dominio bidimensionale è una sezione o una proiezione del problema. Se il punto ha componenti fuori dal piano rappresentato, leggere l'indicazione di proiezione e non trattare l'immagine come una verifica completa della pressoflessione deviata. I quattro momenti rapidi Mx positivo e negativo, My positivo e negativo sono utili per orientarsi, ma non descrivono da soli l'intero dominio N Mx My.

I nomi Plastico ed Elastico descrivono i modelli disponibili. Eventuali chiavi storiche SLU e SLV nei file non vanno interpretate come una generazione automatica di verifiche sismiche a un livello di danno.

### Tensioni e fessurazione

Aprire la scheda dedicata, scegliere la combinazione e leggere prima lo stato tensionale: zona compressa, barre tese, tensione massima, asse neutro e deformazioni. Le deformazioni a video possono essere in per mille; i coefficienti di curvatura hanno un'altra dimensione. Non confrontare i due numeri senza conversione.

La fessurazione dipende anche da durata, aderenza, copriferro alle barre, diametri, interassi e area efficace di calcestruzzo teso. Aprire il dettaglio del calcolo di wk: permette di capire se governa il termine di deformazione minima o quello corretto per il tension stiffening, e se si applica il ramo di barre ravvicinate o distanziate.

La trazione dell'intera sezione segue un ramo specifico con regioni di bordo. Le superfici interne dei fori compatibili hanno controlli dedicati; una superficie tesa priva di armatura efficace o di interasse definito lascia la verifica incompleta. Il disegno completo non dimostra la completezza dei controlli. Analogamente, una soluzione tensionale non lineare o una sezione con tendini può avere disponibilità diversa per il controllo di fessurazione. Leggere lo stato della singola verifica, non soltanto il grafico delle tensioni.

### Taglio torsione e dettagli

Nella scheda taglio e torsione inserire azioni, staffe, passo, inclinazione e armatura longitudinale pertinente. Controllare bw, d e braccio interno ricavati. Per sezioni circolari il ramo adottato è quello specifico per pali descritto nella guida teorica; non coincide automaticamente con una formula generica per travi rettangolari.

La torsione richiede un dettaglio chiuso compatibile e armatura longitudinale disponibile dopo le esigenze di flessione. Non assegnare come disponibile tutta l'armatura senza aver verificato questa condizione. La verifica combinata usa anche un'interazione conservativa fra torsione e i due tagli, esplicitata nel dettaglio.

Nella scheda dettagli costruttivi scegliere il tipo di elemento e completare le informazioni su diametri, passi, sovrapposizioni, ancoraggi e copriferro. «Da completare» non equivale a «Verificato». I controlli automatici riguardano le condizioni esplicitamente rappresentate; ganci, nodi, confinamento e situazioni esecutive speciali possono richiedere valutazioni aggiuntive.

### Curva momento curvatura e aggiornamento

La curva M χ si avvia con il proprio comando. Assegnare N, direzione del momento, numero di passi e distribuzione dei punti. Esaminare la progressione della risposta e le deformazioni terminali. La curva arriva al limite trattato dal motore e non descrive automaticamente un ramo post picco di degradazione della struttura.

Se si modifica l'input mentre sono visibili risultati precedenti, le sezioni strutturali possono segnalarli come «DA AGGIORNARE». Quel risultato non deve essere esportato o letto come riferito ai nuovi dati. Attendere il completamento del ricalcolo e controllare lo stato. La conservazione grafica serve a orientarsi durante l'editing, non a certificare la validità di valori obsoleti.

Le tabelle CA consentono copia e incolla e dispongono dei comandi di template e reimportazione previsti dall'interfaccia. Prima di un'importazione estesa salvare il foglio, controllare intestazioni e unità, quindi verificare numero di righe, barre e combinazioni importate. La semplice riuscita dell'importazione non dimostra che l'ordine delle colonne fosse quello voluto.

### Importare ed esportare le azioni

Le tabelle offrono Template Excel, Importa Excel ed Esporta Excel. Il foglio Azioni usa le colonne Famiglia, Nome, N [kN], Mx [kNm], My [kNm], Vx [kN], Vy [kN] e T [kNm]. N è negativo a compressione. I nomi SLU e SLV nei file corrispondono ai domini Plastico ed Elastico; Rara, Frequente e Quasi permanente conservano azioni distinte.

Nella famiglia Taglio sono richiesti N, Vx e Vy; Mx e My possono accompagnarli, ad esempio per il Model Code, e T identifica la torsione. Nelle altre famiglie si usano N, Mx e My: tagli e torsione devono essere vuoti o nulli. I vecchi file a sette colonne restano leggibili senza torsione; per introdurla occorre l'intestazione T [kNm].

L'importazione controlla tutte le righe prima di modificare il foglio. Si possono aggiungere le righe o sostituire le famiglie presenti. Sono ammessi fino a 10.000 combinazioni e file fino a 20 MB. Le formule Excel non vengono eseguite: servono risultati già calcolati e salvati, oppure valori. Errori di cella e formule prive di risultato memorizzato impediscono l'importazione.

Filtri e ordinamenti cambiano la vista, non eliminano le azioni dal calcolo. Dopo l'importazione verifica numero di combinazioni, unità, segni e corrispondenza dei nomi. Non importare un inviluppo di massimi come se fosse una terna simultanea.

### Leggere asse neutro, mappe e report

La linea dell'asse neutro rappresenta deformazione nulla dello stato selezionato. Nei domini il piano è quello del punto resistente; nelle SLE è quello dell'azione assegnata. Una mappa normalizzata alla resistenza del materiale non sostituisce il controllo dei limiti SLE. Con campo uniforme l'asse può non essere definito; un asse esterno non indica da solo un errore.

Il report raccoglie input, materiali, coefficienti, azioni ed esiti scelti. Gli estremi di tensione/deformazione e i casi governanti possono provenire da combinazioni diverse. Non leggerli come un unico stato. Dati globalmente invalidi o un aggiornamento in corso impediscono l'esportazione dei risultati correnti.

Prima di scegliere una norma consulta [Profili di calcolo del calcestruzzo](wiki:profili-calcestruzzo): il selettore non attesta la copertura di tutte le verifiche della norma.

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
