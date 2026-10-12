## Geometria e rappresentazione delle sezioni

La sezione grafica deriva dalla stessa geometria delle librerie che espone area, baricentro e inerzie. I contorni mantengono regioni materiali, fori e rientranze. La triangolazione destinata al disegno è costruita su copie dei contorni; non sostituisce né modifica la discretizzazione usata nei calcoli della sezione.

### Riferimento locale e punto di inserimento

Il piano della sezione è rappresentato con gli assi locali Y e Z dell'asta. Le coordinate del contorno sono espresse in millimetri; la scena del modello usa metri. Il punto O identifica il riferimento dell'asta all'interno o all'esterno del contorno. L'estrusione utilizza le coordinate relative a O e la terna locale importata, quando disponibile. Il riferimento geometrico non coincide necessariamente con G, il baricentro.

Un offset non interpretabile è un dato non risolto, distinto da un offset nullo. La rappresentazione conserva l'asse quando non è possibile collocare correttamente il volume, anche in presenza di offset di estremità non normalizzati. Un contorno numericamente definito non basta a ricostruire una sezione rastremata o la sua variazione per fase.

### Sezioni composte e significato delle proprietà

Per una sezione formata da materiali diversi, l'unione fisica delle parti consente di rappresentare gli ingombri e le quantità geometriche. Non costituisce una sezione omogeneizzata: area, baricentro e inerzie geometriche non incorporano rapporti dei moduli elastici, viscosità, ritiro o una fase di analisi. Questi parametri rimangono dati separati quando disponibili.

Un riferimento superiore o inferiore può essere collocato sul contorno fisico se è univoco. Un riferimento al baricentro elastico della composta richiede invece la definizione meccanica pertinente; il baricentro geometrico non lo sostituisce. Le proprietà riportano la disponibilità dichiarata dalla libreria: assegnata, calcolata geometricamente, approssimata, numerica differita o assente. L'assenza è distinta dal valore zero.

Il campo di colore sulle aste estruse conserva i campioni della sollecitazione lungo l'asse. La stessa ordinata è riportata sul perimetro a quella stazione; non è una ricostruzione della tensione nel materiale. Fra campioni si usa un'interpolazione grafica. Le zone non coperte dai risultati non ricevono valori estrapolati.
