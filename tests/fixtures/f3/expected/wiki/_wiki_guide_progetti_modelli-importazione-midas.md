## Modelli e importazione da MIDAS

Questa procedura riguarda la versione di prova del modulo Modelli disponibile al 12 ottobre 2026. Il modulo viene sviluppato separatamente: una distribuzione precedente di ANTHEA può non avere i comandi descritti. L'importazione acquisisce una copia del modello aperto in MIDAS Civil NX, con i risultati selezionati; non esegue l'analisi e non modifica il modello sorgente.

### Entrare nel modulo

In ANTHEA apri **Modelli** dalla navigazione principale. Se non hai ancora un archivio progetti, usa **Nuovo archivio progetti**, crea il progetto e organizza le eventuali fasi o sottofasi; in alternativa usa **Apri progetto…**. Torna a **Modelli** e premi **+ Modello** nella scheda del contenitore desiderato. Puoi arrivare allo stesso spazio anche dal riepilogo della fase o sottofase, con **+ Importa un modello**.

Ogni contenitore può ospitare più modelli e più fogli. Il visualizzatore ha uno spazio dedicato, mentre la copia importata appartiene all'archivio progetti. Un archivio aperto in sola lettura non permette l'importazione. Per il primo modello compare **Importa modello…**; su una copia già presente compare **Aggiorna modello…**.

### Preparare la connessione

Apri in MIDAS il modello da acquisire. Per leggere le sollecitazioni e la deformata occorrono risultati di analisi già disponibili per i casi che intendi importare. Lascia terminare le operazioni in corso e mantieni aperto il modello durante la lettura.

Attiva il collegamento API della sessione MIDAS e recupera i due valori richiesti dal pannello di ANTHEA: **Base URL** e **MAPI-Key**. Usa i valori della sessione corrente, anche se hai già importato lo stesso modello in precedenza. Non modificare il modello e non avviare nuove analisi mentre ANTHEA lo sta leggendo.

### Eseguire la prima importazione

1. Premi **Importa modello…**. Nel pannello seleziona **MIDAS Civil NX · modello aperto** in **Sorgente del modello**.

2. Incolla l'indirizzo in **Base URL** e la chiave nel campo **Chiave MAPI · solo per questa sessione**. La chiave resta nella sessione del visualizzatore e non viene salvata nel progetto.

3. Premi **Controlla connessione e casi**. Attendi il riepilogo del modello e l'elenco dei casi statici e delle eventuali fasi costruttive. Dopo un controllo riuscito i casi risultano tutti selezionati; se cambi URL o chiave devi ripetere il controllo.

4. In **Contenuto da acquisire** scegli **Tutto** per ottenere geometria, attributi, carichi e risultati. Per una prima prova limitata alla forma del modello puoi scegliere **Solo geometria**.

5. Se acquisisci i risultati, lascia attive **Sollecitazioni locali · beam e plate** e **Spostamenti globali · deformata** secondo ciò che vuoi visualizzare. Usa **Tutti**, **Nessuno** o le caselle dei singoli casi. Occorrono almeno un caso e almeno un tipo di risultato selezionati.

6. Premi **Importa dal modello aperto**. Il pannello mostra avanzamento e operazione corrente. **Annulla importazione** interrompe la lettura: una copia precedente valida rimane disponibile, senza un aggiornamento parziale.

7. Al termine controlla il modello, i messaggi di acquisizione e i casi disponibili. Usa **Inquadra** se la geometria è fuori dalla vista. Per assegnare un nome riconoscibile apri **Nome del modello** e premi **Rinomina**.

8. Premi **Salva progetto**. L'acquisizione nel visualizzatore non equivale al salvataggio dell'archivio su disco. Dopo il salvataggio puoi riaprire la copia e visualizzarla senza tenere MIDAS collegato.

Per collaudare il collegamento puoi iniziare con **Tutto**, il solo caso **G1** e i due tipi di risultato attivi. Successivamente ripeti l'aggiornamento scegliendo tutti i casi necessari. Il numero dei casi dipende dal modello aperto: i 49 casi del modello di prova non sono un limite dell'interfaccia.

### Scegliere il contenuto da acquisire

| Modalità | Contenuto della copia dopo l'importazione |
| --- | --- |
| Solo geometria | Nodi, elementi e dati geometrici disponibili per sezioni, spessori, offset e terne. Materiali, gruppi, vincoli e carichi esclusi. **Risultati precedenti rimossi.** |
| Geometria + attributi + carichi | Geometria e dati del modello acquisiti dalla sorgente, senza risultati. **Risultati precedenti rimossi.** |
| Solo risultati | Risultati dei casi selezionati. Richiede una copia già aperta con geometria, attributi e tabelle compatibili. **Risultati precedenti sostituiti integralmente.** |
| Tutto | Geometria, attributi, carichi e risultati selezionati della sorgente. **Risultati precedenti sostituiti integralmente.** |

**La selezione dei casi sostituisce l'intero insieme dei risultati della copia.** Se prima erano presenti 49 casi e ora selezioni soltanto G1, al termine resteranno i risultati di G1. Per conservare l'insieme completo seleziona tutti i casi necessari a ogni aggiornamento. Le modalità prive di risultati eliminano le vecchie sollecitazioni per evitare di mostrarle su una geometria diversa.

### Controllare il modello acquisito

Le schede laterali seguono l'ordine **Vista**, **Modello**, **Combinazioni**, **Risultati**, **Verifiche**, **Report**. In **Vista** scegli rappresentazione e visibilità; in **Modello** esplora le proprietà e controlla i dati acquisiti. Le informazioni presenti nelle tabelle originali possono essere più complete dei simboli già disponibili nella scena.

In **Risultati** seleziona caso, famiglia di elementi, componente e unità. Per controllare un plate scegli, ad esempio, G1, la famiglia plate e Mxx negli assi locali. Confronta un elemento noto, tenendo conto della posizione di lettura e dell'eventuale media ai vertici: un colore mediato non è il valore grezzo di ogni campione. Attiva **Mostra modello deformato** per la deformata; controlla il fattore applicato, perché la scala automatica amplifica gli spostamenti per renderli leggibili.

Le combinazioni vengono acquisite come definizioni. La loro presenza nella scheda **Combinazioni** non significa che tutti i relativi campi di risultato siano già stati scaricati o calcolati. Il calcolo locale richiede i risultati dei casi base richiamati e un tipo di combinazione supportato. Puoi quindi acquisire i casi base e gestire successivamente le combinazioni in ANTHEA.

### Importare e visualizzare i risultati per fase

Se il modello contiene fasi costruttive, il pannello di importazione mostra **Fasi costruttive · stage**. Attiva **Acquisisci anche risultati per fase**, seleziona le fasi e indica le etichette esatte dei passi disponibili nella sorgente. Per esempio, `001(last)` identifica un passo con quella precisa etichetta; non è un valore proposto automaticamente. Per più passi della stessa fase usa il punto e virgola. La durata della fase non permette di dedurre con certezza i numeri dei passi salvati.

Seleziona i casi di risultato degli stage elencati e il riferimento degli spostamenti: **Accumulative**, **Current** oppure **Real**. Il riferimento rimane associato ai dati acquisiti. ANTHEA non somma gli spostamenti di fasi diverse e non ricostruisce i riferimenti mancanti. Per importare soltanto stage puoi deselezionare tutti i casi statici, lasciando almeno una fase, un passo, un caso stage e un tipo di risultato.

Nella scheda **Risultati**, **Ambito dei risultati** separa tutti i risultati, casi statici e combinazioni, oppure fasi costruttive. I filtri della fase e del passo restringono l'elenco dei campi già acquisiti; non interrogano nuovamente il software sorgente. Ogni campo conserva caso, fase, passo e riferimento della deformata. La selezione resta riconoscibile anche nelle viste salvate e nei report.

La geometria completa rimane in contesto. Questa versione non simula l'attivazione degli elementi, dei vincoli e dei carichi lungo la costruzione. Un oggetto privo di risultati non riceve valori nulli inventati; le deformate richiedono gli spostamenti effettivamente importati. Le definizioni delle fasi sono conservate, mentre i risultati di fase sono esclusi dalle somme statiche del gestore combinazioni.

I modelli con risultati per fase usano il formato modello 2. Le versioni precedenti di ANTHEA lo rifiutano; questa versione continua a leggere i modelli statici nel formato 1. **Solo risultati** controlla anche le tabelle delle fasi: se la copia precedente non contiene i metadati necessari, usa **Tutto** e verifica la revisione. Gli export tabellari privi di un contesto stage esplicito non vengono confusi con casi statici.

La lettura per fasi è stata collaudata sul protocollo e su dati sintetici. Il confronto con un modello reale dotato di stage resta da completare.

### Aggiornare una copia esistente

Apri il modello dall'archivio e premi **Aggiorna modello…**. Seleziona la sorgente MIDAS, controlla nuovamente la connessione e scegli contenuto e casi. Per aggiornare soltanto le sollecitazioni usa **Solo risultati**: ANTHEA controlla la corrispondenza dei dati del modello prima di sostituirli. Se sono cambiati geometria, attributi o tabelle, l'operazione può essere rifiutata; usa **Tutto** per acquisire la nuova revisione e controllare le differenze.

Il comando **Revisione** serve a esaminare il confronto disponibile dopo l'aggiornamento; non avvia una nuova lettura da MIDAS. Controlla gli elementi aggiunti, modificati o eliminati e salva il progetto quando hai verificato la nuova copia. Le definizioni di combinazione create in ANTHEA vengono conservate, ma i risultati derivati possono dover essere ricalcolati sui nuovi casi base.

### Risolvere le interruzioni e conoscere i limiti

| Messaggio o situazione | Operazione da eseguire |
| --- | --- |
| Collegamento MIDAS non più disponibile | Riattiva il collegamento API; se la chiave è cambiata incolla quella nuova. Ripeti Controlla connessione e casi. |
| Nessun modello aperto in MIDAS | Apri il modello, attendi che sia pronto e ripeti il controllo. |
| Accesso a MIDAS rifiutato | Verifica Base URL e chiave della sessione corrente. |
| MIDAS non ha risposto entro il tempo previsto | Attendi che MIDAS sia nuovamente disponibile; prova un insieme più piccolo di casi. |
| Importazione o pulsante non disponibile | Verifica che l'archivio sia modificabile, il controllo della connessione sia riuscito e, se richiesti, siano selezionati casi e tipi di risultato. |

Quando una lettura fallisce, ANTHEA mantiene l'ultima copia valida e indica il recupero possibile. Dopo aver ripristinato la sessione ripeti il controllo e avvia nuovamente l'importazione. Un'operazione annullata o fallita non produce una copia parzialmente aggiornata.

**Limiti della versione di prova.** Il collegamento acquisisce casi statici e stati costruttivi esplicitamente selezionati, le sollecitazioni locali beam e plate e gli spostamenti globali disponibili. Non costituisce un importatore completo di ogni famiglia di risultato. Nel primo import diretto la trasformazione delle tabelle in terne locali, vincoli, rilasci e link del visualizzatore è ancora incompleta: i dati originali vengono conservati, ma i relativi simboli possono non essere presenti. Le informazioni già normalizzate di una copia precedente vengono riutilizzate soltanto quando i dati confrontati coincidono. Leggi gli avvisi prima di interpretare l'assenza di un simbolo come assenza del relativo attributo nel modello.

Per aprire una copia già acquisita scegli **File o copia già acquisita**: sono previsti il pacchetto **.antheamodel** oppure **NODE.json** della cartella di acquisizione MIDAS completa. Questa scelta non è l'apertura diretta di un file nativo MIDAS; per il modello nativo usa il collegamento al modello aperto descritto sopra.

