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

