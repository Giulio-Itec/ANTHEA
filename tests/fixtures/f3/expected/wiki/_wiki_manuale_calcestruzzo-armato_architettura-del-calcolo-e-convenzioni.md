## Architettura del calcolo e convenzioni

### Separazione fra modello e interfaccia

La tavola delle armature è una proiezione dei pezzi longitudinali, delle zone di staffatura e delle sezioni restituiti da Checker. Marche e coordinate grafiche non alterano quantità, quote o resistenze. Il modello attuale fornisce barre longitudinali rettilinee e posizioni nominali delle staffe: il disegno non aggiunge ganci o sagomature non calcolati. Le lunghezze di taglio delle staffe restano da definire e i dettagli costruttivi non completati sono segnalati. Le sezioni trasversali rappresentano l’armatura nominale del tratto, senza attribuire resistenza aggiuntiva alle barre sovrapposte.

I nuovi fogli del palo inizializzano l’acciaio B450C dal catalogo comune alla sezione in c.a., includendo l’intero legame costitutivo. La riapertura di archivi precedenti conserva invece i parametri salvati: la sola resistenza fyk di 450 MPa non identifica automaticamente tutte le proprietà di B450C.

Il riferimento GPC Engine nel palo orizzontale identifica il motore delle librerie di calcolo; la presentazione dei materiali usa i nomi Calcestruzzo e Acciaio. Formulazioni, parametri e risultati rimangono quelli descritti nelle sezioni teoriche del modulo.

ANTHEA.Application gestisce documenti, preparazione dei fogli, revisioni e coordinamento dei calcoli senza dipendere da WPF. ANTHEA.Reports compone i report e condivide lo scrittore Word; ANTHEA.Wiki gestisce catalogo, ricerca e lettura delle guide. X.Desktop presenta editor, grafici e schede dei materiali. X.Core conserva soltanto un ingresso di compatibilità per strumenti precedenti. I metodi ingegneristici sono nelle librerie GPC e negli adattatori residui di X.Calculations, ancora in refactoring; la separazione completa dei contratti delle viste non è conclusa. Il disegno non è il modello resistente: la discretizzazione della vista può essere diversa da quella usata per l’equilibrio.

Ogni ricalcolo lavora su una fotografia degli ingressi. La pubblicazione richiede che la revisione sia ancora corrente; la sola interruzione di una richiesta non garantisce che ogni motore smetta subito di lavorare. Questo controllo riguarda la coerenza del documento e non modifica formule, coefficienti o criteri di verifica. I capitoli di progetto usano gli stessi esiti dei moduli, con un renderer dedicato alle sole figure.

Bridge Design utilizza un proprio motore puro, BridgeConcept.Calculate. Riceve input, listino e ipotesi e restituisce un risultato con geometria, quantità, dettagli, diagrammi e avvisi. Lo stesso risultato alimenta interfaccia e report. Il motore non modifica gli input: un parametro impostato a zero per richiedere la scelta automatica resta zero nell'archivio, mentre il valore adottato è riportato nel risultato.

I calcoli dei report di progetto vengono eseguiti su copie dei dati, evitando di cambiare i fogli aperti. La gestione delle revisioni congela input e contesto necessario; non congela un eseguibile storico. Ricalcolare un vecchio archivio con un motore aggiornato può quindi produrre risultati diversi, che devono essere identificati con versione e data.

### Unità e segni

| Ambito | Lunghezze | Tensioni e resistenze | Azioni | Convenzione da ricordare |
| --- | --- | --- | --- | --- |
| Geotecnica verticale | m | kPa | kN | Compressione e trazione trattate in rami distinti |
| Broms | m per il terreno; mm per la sezione | kPa nel terreno; MPa nei materiali | kN e kNm | N positivo a compressione |
| Sezione CA | mm | MPa | kN e kNm | N negativo a compressione; trazione positiva |
| Sezione composta | mm | MPa | kN e kNm | N negativo a compressione nelle fasi; appoggi con convenzione propria |
| Bridge Design | m salvo spessori metallici in mm | MPa e kPa secondo etichetta | kN e kNm | Carichi e momenti dell'intero impalcato |

Vale 1 MPa = 1 N/mm² = 1000 kPa. Per trasformare Nmm in kNm si divide per un milione; per trasformare N in kN si divide per mille. Per passare da una curvatura espressa in 1/mm al valore numerico in 1/m si moltiplica per mille. Una deformazione di 0,001 equivale a 1 per mille e a 1000 microdeformazioni.

Le convenzioni del segno non sono intercambiabili fra i moduli. I colori dei diagrammi non costituiscono una convenzione universale dell'applicazione: leggere la legenda. Per confrontare due solver occorre allineare anche origine degli assi, verso dei momenti, punto di applicazione di N e trasformazione delle coordinate.

### Dati mancanti e validità del risultato

La validazione degli input impedisce di utilizzare numeri non finiti, geometrie incompatibili e parametri fuori campo nelle procedure che li richiedono. Un valore mostrato con pochi decimali è una presentazione: calcolo ed esportazioni conservano precisione maggiore. L'arrotondamento grafico non deve essere usato per ricostruire un equilibrio con tolleranze molto strette.

I risultati precedenti eventualmente conservati a video sono marcati da aggiornare dopo una modifica. Un risultato incompleto o fuori campo non viene convertito in esito favorevole. La disponibilità va letta per ogni verifica: un equilibrio tensionale può riuscire mentre fessurazione, dettagli o instabilità restano non valutabili.

