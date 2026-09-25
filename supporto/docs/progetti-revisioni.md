# Progetti e revisioni

La pagina di composizione presenta tre colonne: informazioni e riepilogo a sinistra, struttura del progetto al centro e catalogo delle schede a destra. I pannelli scorrono indipendentemente e i divisori consentono di regolare le larghezze. Nei fogli l’albero è inizialmente nascosto: la freccetta nella barra dei comandi lo mostra o lo nasconde e mantiene la scelta passando da un foglio all’altro. Le miniature aprono i fogli; clic sul nome di una sezione o Invio apre il riepilogo. Il pulsante Torna al progetto riapre il riepilogo. Doppio clic e F2 rinominano; clic fuori conferma il nome e chiude l’editing. Il menu destro delle sezioni contiene Rinomina, Duplica ed Elimina.

Il riepilogo organizza le informazioni nei riquadri Controlli, Dati comuni e Revisioni. I dati principali sono subito visibili; gli altri valori, i fogli, le sottosezioni e il dettaglio degli avvisi sono espandibili. L’albero evidenzia tutta la riga selezionata, mostra guide di gerarchia e mantiene allineati i comandi di aggiunta, confronto e report. Il catalogo compatto conserva le miniature originali, raggruppa le schede per disciplina e offre una ricerca per nome o disciplina. La struttura iniziale resta vuota e presenta il pulsante Crea progetto. I riferimenti di ereditarietà restano operativi senza etichette aggiuntive nell’albero o nel riepilogo.

## Spostamenti e annullamento

Il bordo di un’intestazione indica un riordino prima o dopo un elemento dello stesso gruppo. Il centro di una sezione consente di trasferirvi un’altra sezione, comprese le sue sottosezioni. Non è possibile creare cicli o annidare un progetto radice. I fogli si spostano su una sezione o prima/dopo altri fogli.

Un’anteprima calcolata su una copia del documento mostra i cambiamenti dei valori comuni prima di un trasferimento tra sezioni. Annullare l’anteprima non modifica il progetto. In caso di riferimenti discordanti resta la logica di confronto esistente, senza scegliere arbitrariamente un valore.

Annulla e Ripristina conservano fino a 30 stati nella sessione corrente, comprese modifiche confermate, spostamenti, eliminazioni, uniformazioni, duplicazioni e revisioni. Ctrl+Z e Ctrl+Y operano sul progetto quando il fuoco non è in un campo di testo; nei campi resta l’annullamento locale. La cronologia di annullamento si azzera cambiando documento e non viene salvata nel file.

## Duplicazione

Duplica sezione copia l’intero ramo con nuovi identificativi di sezioni e fogli. La copia ha un nome automatico rinominabile e non acquisisce la cronologia delle revisioni dell’originale. I dati locali sono indipendenti; la normale condivisione dei dati nel progetto continua a funzionare.

## Revisioni

La barra Revisioni mostra i pulsanti Rev. 0, Rev. 1, ecc., con la versione selezionata evidenziata e quella modificabile indicata come attuale. Il cambio avviene nella stessa finestra, anche quando l’albero laterale è nascosto. Il foglio aperto viene conservato se presente nella revisione; altrimenti compare la struttura e il foglio viene ritrovato tornando a una versione che lo contiene. La scelta di mostrare l’albero resta invariata.

Nuova revisione archivia la versione attuale (Rev. 0 alla prima operazione), incrementa il numero e seleziona subito la nuova revisione. La nota iniziale è facoltativa. Il riepilogo modifiche mostra data, nota, aggiunte, eliminazioni, cambi d’ordine e variazioni dei parametri comuni; le altre modifiche sono indicate per foglio. Selezionando una sezione nel riepilogo si gestiscono le sue revisioni; nei fogli si usa la sezione revisionata più vicina. Ogni sezione può avere una numerazione indipendente.

Elimina revisione agisce sulla versione selezionata nella barra e consente di annullare l’operazione. Eliminando una versione archiviata, le altre mantengono i loro numeri e il riepilogo viene riferito alla precedente ancora disponibile. Eliminando l’attuale, vengono ripristinati i dati, i fogli e le sottosezioni dell’ultima revisione rimasta, che diventa immediatamente modificabile. L’editor viene ricaricato sui dati ripristinati. L’unica versione rimasta non è eliminabile da questo comando; l’eliminazione dell’intera sezione resta nel menu della struttura.

Il ripristino riguarda il ramo selezionato. I materiali dei livelli superiori conservano i valori del progetto corrente; eventuali differenze con i fogli ripristinati sono visibili nel confronto. I precedenti storici delle sottosezioni ancora disponibili sono conservati fino al numero ripristinato. Se un vecchio foglio è stato spostato in un altro ramo, la copia ripristinata riceve un identificativo indipendente. Il salvataggio rimuove dall’archivio condiviso i dati non più usati da alcuna revisione.

Le revisioni archiviate proteggono gli input e i comandi che modificano i dati, lasciando disponibili schede, scorrimento, selezione delle righe, viste dei risultati ed esportazione. La consultazione usa una copia separata: non modifica gli archivi, il documento corrente o la sua cronologia Annulla/Ripristina. Anche dalla vista storica Salva conserva il documento corrente completo, comprese le revisioni. Tornando all’attuale si ritrovano le modifiche non ancora salvate.

Gli snapshot includono il contesto degli antenati e i rispettivi fogli, escludendo i rami estranei. Vengono congelati i dati, senza collegamenti mutabili al progetto attivo. La numerazione delle revisioni compare nei report di sezione e nel titolo dei report dei fogli. I calcoli dei report vengono rieseguiti sui dati della revisione selezionata.

`ProjectRevisions` usa un archivio di contenuti identificati tramite SHA-256 del JSON ordinato per chiave. Gli alberi storici contengono riferimenti `dati_ref`; i contenuti uguali occupano un’unica voce. In memoria i fogli attivi sono oggetti indipendenti per mantenere gli editor esistenti. Al salvataggio anche i fogli correnti diventano riferimenti e vengono eliminati dall’archivio i contenuti non più utilizzati. Il salvataggio resta atomico.

I file con revisioni usano il formato 2 e richiedono questa versione di ANTHEA o una successiva. I vecchi file di formato 1 restano leggibili; i documenti senza archivio revisioni continuano a essere salvati nel formato 1. Lettura e validazione controllano l’esistenza e l’integrità dei contenuti referenziati.

## Verifica

`ANTHEA.exe --smoke-project-workspace <cartella>` verifica navigazione e selezione, copia indipendente, spostamenti con anteprima e annullamento, recupero di eliminazioni e uniformazioni, immutabilità degli snapshot e dei materiali del contesto, deduplicazione, salvataggio e lettura, riferimenti corrotti, sola lettura e revisione nel report. Produce schermate e un archivio di prova.

I test `--smoke-projects` e `--smoke-hierarchy` verificano le operazioni precedenti, i trascinamenti WPF e la condivisione dei dati a più livelli.
