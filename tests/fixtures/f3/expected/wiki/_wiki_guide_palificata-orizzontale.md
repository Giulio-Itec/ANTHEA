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

