## Esaminare le sezioni del modello

Nella scheda **Modello**, premi **Sezioni del modello…**. Puoi anche selezionare una sezione nell'albero o un'asta nella scena e premere **Dettaglio sezione…** nell'ispettore. La finestra si apre separatamente dal modello, è ridimensionabile e conserva il collegamento alla vista. L'elenco superiore permette di passare alle altre sezioni della copia salvata.

![Finestra di dettaglio con una sezione composta di esempio](../../src/ANTHEA.Desktop/Assets/Wiki/modelli-sezioni-dettaglio.png)

### Leggere geometria proprietà e attributi

L'anteprima rappresenta il piano locale Y–Z e indica le dimensioni in millimetri. Gli interruttori **Quote**, **Assi locali**, **Assi principali**, **Baricentro** e **Inserimento** sono indipendenti. Il simbolo G individua il baricentro disponibile; O il riferimento dell'asta rispetto al contorno. Nelle sezioni composte G è il baricentro geometrico delle parti, distinto da quello della sezione omogeneizzata.

La scheda **Proprietà** contiene area, inerzie e altre grandezze disponibili. La colonna **Origine** distingue valori calcolati, assegnati, approssimati o non disponibili. Il trattino non equivale a zero. I calcoli numerici differiti, come alcune proprietà di torsione, non vengono avviati per aprire la finestra. Sono indicati anche G e O nel sistema di coordinate dell'anteprima e le dimensioni delle parti riconosciute.

**Dati importati** conserva gli attributi originali, con le unità della sorgente nelle righe UNIT. I rapporti elastici o altri parametri presenti in questi dati non trasformano automaticamente le proprietà geometriche in proprietà omogeneizzate. **Disponibilità** elenca le limitazioni della ricostruzione. Le sezioni definite soltanto da valori numerici possono avere proprietà senza un contorno disegnabile.

### Individuare gli elementi e leggere i volumi

**Seleziona elementi** evidenzia gli oggetti associati alla sezione; **Isola elementi** limita la vista agli stessi oggetti. Per ripristinare il modello usa **Mostra tutto**. Chiudere la finestra di dettaglio conserva la selezione e la visibilità impostate.

In **Vista**, la rappresentazione **Volumi e offset** usa gli stessi contorni del dettaglio 2D, compresi fori e rientranze. Il punto di inserimento è collocato rispetto agli assi locali dell'asta. Se il contorno, il riferimento o un offset di estremità non sono risolti, l'elemento conserva la vista ad asse e la limitazione è segnalata: non viene applicato un centraggio presunto.

Il contouring può essere riportato sul volume dell'asta. Il colore varia lungo i campioni importati della sollecitazione e non rappresenta una tensione distribuita sulla sezione. Le porzioni prive di campioni restano neutre; un unico campione rimane puntuale.

La rappresentazione delle composte comprende il profilo a I con soletta centrata, senza raccordi verticali o irrigidimenti non interpretati. Le altre geometrie conservano gli attributi disponibili e dichiarano i propri limiti. La variazione della sezione per fase costruttiva non aggiorna ancora il volume della vista. Questa finestra è di ispezione e non modifica le sezioni del modello.
