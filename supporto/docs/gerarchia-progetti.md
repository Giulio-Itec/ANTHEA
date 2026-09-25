# Gerarchia dei fogli nei progetti

Ogni sezione e il progetto possono contenere fogli insieme a sottosezioni. Nell'albero i fogli sono visualizzati prima delle sottosezioni, immediatamente sotto il contenitore a cui appartengono. Il trascinamento sul titolo aggiunge o sposta una scheda in quel contenitore; il trascinamento sulle righe delle schede mantiene il riordino manuale.

Trascinando il nome di una sezione sopra o sotto quello di un'altra dello stesso gruppo si cambia l'ordine delle sezioni. Una linea indica la posizione di inserimento. Si sposta l'intero contenuto, incluse schede e sottosezioni, senza cambiare il genitore o i riferimenti dei dati comuni. La stessa operazione riordina i progetti. L'ordine viene conservato nel file `.programma`.

Per ogni proprietà condivisa prevale il livello più alto che la definisce e che è compatibile con il destinatario. La regola attraversa anche sezioni intermedie vuote. Materiali, geometria, armatura e dati geotecnici mantengono gli adattatori, le unità e i limiti dei singoli moduli. I fogli in rami paralleli condividono soltanto i riferimenti dei loro antenati comuni; non vengono confrontati direttamente tra loro.

Quando si confermano o si salvano modifiche a un riferimento con fogli discendenti, i dati compatibili da esso governati si propagano nella sua sezione e nelle sottosezioni. Le modifiche effettuate sotto un riferimento superiore restano locali e sono segnalate come conflitti; non riscrivono il riferimento. Per cambiare il dato comune si modifica il foglio superiore. Per proprietà senza un riferimento superiore rimane disponibile la scelta di aggiornare i fogli dello stesso livello oppure mantenere il valore locale.

Le nuove schede ereditano i dati comuni. In presenza di riferimenti discordanti allo stesso livello, le proprietà ambigue restano da uniformare; una forma ambigua impedisce di inizializzare una geometria o un'armatura parziale. La geometria viene definita prima di verificare la compatibilità dell'armatura. Gli spostamenti e l'apertura di archivi esistenti conservano gli input e mostrano gli eventuali conflitti nella nuova collocazione.

Il confronto mostra il percorso dei fogli e include i riferimenti superiori anche quando si apre da una sottosezione. «Uniforma a questo» opera nella sezione del riferimento e nelle sue sottosezioni; i valori governati da un antenato non possono essere promossi dal basso. I distintivi dei gruppi riassumono conflitti e avvisi discendenti. Il controllo del copriferro cerca la scheda CLS anche nei livelli superiori.

Compatibilità archivio: `fogli` sul nodo progetto è facoltativo per i file precedenti; se presente viene validato come quello delle sezioni. Nessun archivio esistente viene riscritto durante la lettura o il confronto.

## Report delle sezioni

Il pulsante **Genera report** su ogni progetto e sezione esporta un Word unico con i fogli diretti e tutte le sottosezioni, nello stesso ordine dell'albero. È disabilitato soltanto sui rami senza schede. Il documento contiene un indice navigabile, dati comuni per proprietà, dati specifici, risultati e grafici dei moduli. I materiali includono anche le proprietà derivate. I riferimenti necessari degli antenati sono richiamati anche esportando una sola sottosezione; i fogli dei rami esterni non vengono inclusi.

L'accorpamento usa compatibilità e unità normalizzate della condivisione: non confronta JSON interi né unisce rami indipendenti. I dati discordanti restano separati, con valori e provenienza. Conflitti e avvisi vengono mostrati prima del salvataggio, con la possibilità di aprire il confronto oppure generare il documento con le segnalazioni. Copriferro e limiti dei moduli sono conservati.

Il calcolo usa una copia dei dati correnti, senza cambiare i fogli aperti. Tutti i moduli vengono ricalcolati; schede incomplete e verifiche non disponibili restano nel report con segnalazione esplicita. I report vengono assemblati in memoria e il Word finale viene scritto atomicamente. Annullando prima del salvataggio, un file già esistente rimane invariato. I report singoli conservano la propria funzione di esportazione.

La prova `--smoke-project-report <cartella>` verifica pulsanti su tutti i livelli, sette moduli, ordine e contenuti ricorsivi, proprietà comuni, parametri specifici, riferimenti esterni, conflitti, immagini, file sorgenti invariati, errori e annullamento.

Verifica: `ANTHEA.exe --smoke-hierarchy <cartella>` copre i drop sui gruppi con figli, precedenza a più livelli, ereditarietà, propagazione, conflitti risolvibili, separazione dei rami, copriferro, riordino/spostamento, ambiguità e salvataggio/riapertura. Le prove `--smoke-sharing`, `--smoke-projects`, `--smoke-materials` e `--smoke-steel` verificano le integrazioni precedenti.
