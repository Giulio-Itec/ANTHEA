## BIM e controllo dello scambio informativo

Un oggetto geometrico diventa utile nello scambio quando conserva identità, significato e proprietà verificabili. Il modello architettonico di una trave non definisce da solo asse analitico, rigidezza, svincoli e fase strutturale. Il passaggio al calcolo è una trasformazione controllata, non un cambio di formato.

### Definire prima il requisito

Per uno scambio di travi inizia dal risultato necessario: elenco degli elementi, materiale, sezione, posizione e identificativo persistente. Specifica unità, riferimento geografico, convenzione di orientamento e trattamento degli oggetti esclusi. Una proprietà assente deve restare distinguibile da un valore nullo. Lo zero assegnato automaticamente a una rigidezza mancante può produrre un meccanismo; un default non dichiarato può nascondere l'assenza dell'informazione.

### IFC e modello analitico

IFC descrive entità e relazioni in uno schema aperto buildingSMART. La classe di un elemento, le sue proprietà e la sua collocazione hanno ruoli differenti. La presenza di una geometria visualizzabile non dimostra che il ricevente abbia importato associazioni, unità o proprietà. Conserva il collegamento fra identificativo originale ed elemento analitico generato, compresi gli eventuali molti-a-uno.

| Controllo | Evidenza richiesta | Errore intercettato |
| --- | --- | --- |
| Unità | Lunghezza nota confrontata prima e dopo | Scala metri/millimetri |
| Posizionamento | Coordinate di almeno due punti e orientamento | Traslazione o rotazione inattesa |
| Identità | Mappa degli identificativi | Elementi duplicati o persi |
| Materiale | Proprietà e fonte | Nome senza parametri utilizzabili |
| Connettività | Nodi e relazioni analitiche | Solidi che si toccano ma aste scollegate |

### CDE e revisione

Un ambiente di condivisione dei dati, CDE, gestisce stati e responsabilità della documentazione. Il nome del file non basta a distinguere un modello di lavoro da una revisione autorizzata per lo scambio. Registra autore, revisione, scopo e data del pacchetto ricevuto; conserva un rapporto degli elementi modificati anziché sovrascrivere silenziosamente gli input di calcolo.

### Procedura di accettazione

Prova prima un campione con una trave ruotata, un materiale personalizzato e un elemento escluso. Confronta quantità e coordinate con il sorgente, poi verifica la mappa dei dati. Una clash detection geometrica non certifica equilibrio o validità meccanica. Lo schema informativo va controllato separatamente dal modello strutturale, discusso in [Elementi beam](wiki:beam).

Anthea conserva i suoi dati di progetto e modulo; questa pagina non dichiara disponibile un importatore IFC. Il riferimento per lo schema IFC è la documentazione ufficiale buildingSMART IFC 4.3. I requisiti contrattuali di scambio e il livello di fabbisogno informativo vanno concordati per l'attività concreta.

