# Guida pratica di Bridge Design
Edizione 30 settembre 2026 · Revisione 01 · ANTHEA BD P01

Questa guida accompagna la costruzione di un progetto preliminare, la ricerca delle alternative e la lettura dei risultati di Bridge Design in ANTHEA. Il percorso è pensato per chi deve confrontare tipologie, campate e quantità prima di sviluppare il modello strutturale e il computo completo. La guida teorica della stessa revisione spiega formule, prove e limiti; qui ogni passo indica cosa inserire, cosa controllare e come interpretare il risultato.

La parola ottimo significa migliore fra le combinazioni esplorate con i dati e i criteri scelti. La finestra non fornisce un’autorizzazione a costruire la soluzione e non esegue automaticamente le verifiche dei moduli GPC Engine. Usa il risultato per selezionare poche alternative motivate e conserva insieme geometrie, prezzi, ipotesi e avvisi.

## Preparare i dati prima di aprire la ricerca

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

## Creare il progetto di riferimento

Apri il modulo Bridge Design e lavora nella scheda Progetto. Le schede di ingresso sono Sito, Campate, Sezione e Pile. I risultati comprendono Quantità e costi, Dettagli del predimensionamento, Confronto A / B, Prezzi unitari, Ipotesi e coefficienti e Sezioni e quote. Il pulsante Ottimizza porta alla scheda separata Ottimizzazione.

1. In Sito imposta lunghezza, quota, corsie e larghezze accessorie. Controlla la larghezza complessiva restituita dal programma, non soltanto il numero di corsie.
2. Seleziona l’ostacolo e la larghezza. Osserva il prospetto: il disegno è schematico e non costituisce una verifica del franco, dell’alveo o della posizione reale degli appoggi.
3. In Campate scegli una famiglia iniziale e la continuità. Inserisci un numero di campate ragionevole oppure usa zero per la scelta automatica. Leggi poi le luci effettive, che non sono necessariamente tutte uguali.
4. In Sezione imposta i soli dati noti. Lascia zero nei campi automatici se vuoi che il motore li predimensioni. Per le lamiere usa gli spessori in mm; per anime e solette in cls usa le unità mostrate.
5. In Pile scegli terreno, schema e fondazione. Una misura a zero resta automatica; una misura positiva è una richiesta esplicita. Verifica gli avvisi su snellezza, compressione e capacità assiale.
6. Apri Prezzi unitari e Ipotesi e coefficienti. Aggiorna tariffe, carichi equivalenti, resistenze geotecniche di riferimento e fattori ambientali dove disponi di informazioni migliori.
7. Salva il progetto prima della ricerca. Se non è calcolabile, correggi l’errore prima di avviare l’ottimizzazione: il motore deve poter costruire il riferimento.

Una configurazione calcolabile può essere fuori dal campo usuale della famiglia. La scheda Progetto può mostrarla con un avviso, mentre l’ottimizzazione la esclude dai candidati ammessi. Perciò un costo visibile nella scheda Progetto non basta a dimostrare che il riferimento appartenga all’insieme esplorato.

## Leggere sezioni spessori e lunghezze

Apri Sezioni e quote prima di valutare il solo costo totale. Il prospetto riporta componenti, simboli, valori, unità, origine e significato. Impostato identifica un dato assegnato; Automatico una regola del motore; Derivato una conseguenza degli ingressi. Nelle famiglie speciali compaiono anche Adottato e Predimensionato. Nessuna di queste etichette significa sezione verificata.

d è l’altezza complessiva in campata, con soletta. dpila è l’altezza dell’impalcato sulle pile e può essere maggiore per il cassone variabile. h è l’altezza sotto soletta e rialzo; hw è l’altezza netta dell’anima; lw è lo sviluppo reale di un’anima inclinata. Le tabelle distinguono larghezza della piattabanda, spessore, numero di travi, luce di ogni campata e sviluppo complessivo degli elementi longitudinali.

Per esempio, H per 4V uguale a 1 indica uno scarto orizzontale pari a un quarto dell’altezza verticale dell’anima; non indica una pendenza di 45 gradi. Il computo usa lo sviluppo inclinato per l’area e la proiezione verticale per l’inerzia. Per le travi a U controlla che la somma delle larghezze superiori non provochi sovrapposizioni.

Nel prospetto delle sottostrutture leggi separatamente altezza e dimensione del fusto, numero di colonne, dimensioni del pulvino, lati e spessore del plinto, numero, diametro e lunghezza dei pali. Una fondazione bloccata con dimensioni a zero può essere ridimensionata dopo una variazione delle reazioni: controlla sempre le quote effettivamente adottate.

## Scegliere obiettivo e costanti

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

## Impostare range utili

Il pulsante Suggerisci campate dalla lunghezza propone l’intervallo ricavato dai campi usuali delle famiglie. È un punto di partenza: il controllo preventivo usa la luce media e non garantisce che le luci finali, dopo la distribuzione e l’ostacolo, siano ammesse. Se hai bloccato il numero delle campate, mantieni minimo e massimo compatibili con quel numero.

Il minimo di altezza si riferisce alla campata; il massimo si applica anche all’altezza dell’impalcato sopra le pile. Zero significa nessun limite aggiuntivo, non una sezione di altezza nulla. Per un cassone variabile un limite massimo può quindi escludere una geometria la cui altezza in mezzeria sembra compatibile.

Per le griglie percentuali, 100 significa la regola automatica della combinazione. Con altezza 100–120 e passo 10 si esaminano 100, 110 e 120%. Con pali 100–150 e passo 25 si esaminano tre lunghezze riferite alla classe di terreno. Se la fondazione è bloccata, la relativa griglia è disabilitata. Non sono esplorati valori inferiori al 100%.

Come percorso iniziale, usa 2 o 3 valori di altezza e un intervallo di campate motivato dal sito. Se il vincitore cade all’estremo del range, amplia il campo solo dove fisicamente plausibile e ripeti. Se interessa affinare l’altezza, riduci il passo vicino ai risultati senza superare 11 valori nella griglia. Non è necessario usare subito tutti i 50.000 tentativi ammessi.

Una griglia più fitta aumenta il numero di combinazioni ma non elimina i limiti del modello. Se mancano dati su varo, pali o traffico, è spesso più utile ripetere scenari con ipotesi diverse che usare molti decimali sulla stessa ipotesi incerta.

## Avviare e leggere l’avanzamento

Premi Avvia ottimizzazione. Il motore comunica i nuovi tentativi in gruppi; la preview segue il migliore provvisorio quando cambia, al massimo due volte al secondo. Non viene ridisegnato ogni candidato. Il progetto corrente cambia soltanto quando premi Applica soluzione selezionata.

La barra indica quante combinazioni sono state calcolate e quante sono state ammesse rispetto alla griglia prevista. Il totale previsto può diminuire di fatto per salti e duplicati. Alla fine sono esposti tentativi calcolati, ammessi, geometrie distinte ed esclusi. Non sommare le occorrenze dei motivi di esclusione per ricavare il numero degli esclusi, perché uno stesso tentativo può violare più condizioni.

Interrompi annulla la ricerca senza applicare una soluzione. Una modifica agli input, al listino o ai vincoli rende obsoleta la ricerca: ANTHEA richiede di ripeterla. Non usare una graduatoria calcolata con prezzi vecchi per interpretare il costo di un progetto modificato.

## Usare i grafici

Il grafico Variabili esplorate ha in ascissa il numero del tentativo e permette di visualizzare costo, CO₂, altezza, campate, lunghezza pali, tipologia, schema di pila, fondazione e continuità. I punti grigi identificano tentativi esclusi; le quote non calcolabili non sono disegnate. Nei grafici economico e ambientale la linea scura indica il minimo progressivo, non una curva di convergenza strutturale.

L’ordine dei tentativi deriva dall’enumerazione del programma. Non interpretare una successione di punti come un percorso continuo di trasformazione del ponte, né come una misura della sensibilità di una sola variabile se ne cambiano più di una. Per studiare una variabile, blocca le altre e ripeti una ricerca dedicata.

La Famiglia di soluzioni plausibili rappresenta costo e CO₂ delle geometrie ammesse. In basso a sinistra si trovano i valori minori. L’anello identifica Pareto, la stella il migliore per l’obiettivo e la croce il progetto corrente. Filtra per famiglia o attiva Solo frontiera Pareto; clicca un punto per esaminarlo anche quando non compare fra le prime N righe.

![Nuvola costo e CO₂ ricavata dai risultati riproducibili dell’esempio](figure/costo-co2.png)

Il grafico qui riportato è generato dai dati della ricerca descritta nel capitolo successivo. Nella finestra gli stessi concetti sono interattivi. Una nuvola non sostituisce il controllo del prospetto: due punti vicini possono avere numeri di appoggi, dimensioni o difficoltà costruttive molto diversi.

![Variazione di altezza e costo durante l’enumerazione dell’esempio](figure/traccia.png)

## Esempio guidato da centoventi metri

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

![Differenze di costo fra il riferimento e alcuni scenari dell’esempio](figure/scenari.png)

Se imposti un minimo di cinque campate, il riferimento a tre campate è escluso. Vince una soluzione a T in c.a. con cinque campate e costo 2.657.209,43 euro. Un vincitore più caro del riferimento è quindi possibile quando il riferimento non soddisfa i nuovi vincoli. Leggi sempre la riga che descrive le esclusioni del progetto corrente.

Moltiplicando tutti i prezzi per 0,8 o per 1,2, senza cambiare altro, la geometria vincente deve restare la stessa, la CO₂ deve restare invariata e il costo deve diventare rispettivamente 1.639.620,59 e 2.459.430,88 euro. Questo è un controllo di coerenza economica, non uno studio completo della sensibilità: per quest’ultimo occorre variare separatamente le voci più incerte, non soltanto tutto il listino dello stesso fattore.

## Selezionare applicare e conservare le alternative

La tabella ordina le soluzioni secondo il punteggio non arrotondato. Se due righe mostrano lo stesso punteggio stampato, possono differire nelle cifre successive. Sono mostrati posizione, famiglia, campate, altezza, costo, differenza dal riferimento, CO₂, punteggio e appartenenza a Pareto.

Seleziona una riga o un punto della nuvola e leggi Variazioni rispetto al progetto corrente. La tabella confronta luci, continuità, fondazione e quote tecniche adottate. Controlla anche gli avvisi: un candidato ammesso conserva comunque i limiti generali del modello e le voci di computo mancanti.

Premendo Applica soluzione selezionata, il progetto adotta gli input del candidato. Se non esiste già un’alternativa A, ANTHEA conserva il progetto precedente come A. Se A esiste, non viene sostituita automaticamente: controlla che il confronto A / B rappresenti ancora ciò che vuoi confrontare. Dopo l’applicazione rivedi Sezioni e quote e salva il documento con un nome che renda riconoscibile la variante.

La ricerca completa non va considerata un registro permanente nel file di progetto. Per documentare la decisione salva il progetto di riferimento e le alternative scelte, esporta i loro computi e annota obiettivo, vincoli e griglie usati. L’export del progetto descrive la soluzione corrente; non sostituisce automaticamente un report dell’intera nuvola di ottimizzazione.

## Prezzi geotecnica e scenari da confrontare

Prima di concludere prepara almeno uno scenario di riferimento e uno cautelativo motivato dai dati disponibili. Varia, per esempio, lunghezza dei pali, resistenze geotecniche di riferimento, costo della carpenteria, incidenza di montaggio e necessità di opere provvisionali. Se il vincitore cambia facilmente, conserva più alternative da approfondire invece di attribuire precisione eccessiva al primo posto.

Non usare la pressione convenzionale del terreno come se fosse automaticamente una portanza certificata. Un aumento della lunghezza del palo accresce l’attrito laterale nel modello, ma non simula il passaggio a uno strato reale. Il progetto geotecnico deve confermare lunghezza, capacità, gruppo, cedimenti e risposta trasversale. Per un ponte in alveo, il fatto che le pile evitino l’ostacolo disegnato non dimostra compatibilità idraulica.

Leggi le note accanto ai prezzi. Pali e cls hanno inclusioni diverse; appoggi e giunti dipendono da forze e movimenti. Gli oneri aggiuntivi e gli imprevisti sono riserve, non l’insieme delle lavorazioni mancanti. Per le famiglie speciali un’offerta di sistema può includere montaggio, terminali e protezioni già computati in altre righe: evita duplicazioni.

## Usare le famiglie speciali

Arco metallico con catena e reticolare richiedono campate indipendenti. Se tieni bloccata una continuità attiva, queste famiglie non entrano nella ricerca. Per confrontarle devi liberare la continuità o impostare correttamente il riferimento. Il modello comprende arco con catena, non tutte le possibili configurazioni di arco con spinta sulle spalle.

Strallato e sospeso richiedono tre campate, impalcato continuo ed estremi su spalla. Le campate sono L/4, L/2, L/4. I campi di luce usuale si riferiscono alla campata centrale; un ponte complessivamente lungo 150 m non diventa uno strallato ammesso solo perché la lunghezza totale supera 100 m. Le antenne e i cavi hanno parametri avanzati da impostare prima della ricerca.

Per la piastra ortotropa controlla lamiera, altezza e larghezze delle canalette, interasse e spessori del cassone. Per le travi incorporate controlla spazio fra profili e cls sopra e sotto le piattabande. Il prospetto rappresenta un profilo ideale, non un componente commerciale già selezionato.

Su archi, reticolari, strallati e sospesi la mancanza di diagrammi globali non è un errore grafico. Il motore stima equilibri e quantità senza risolvere la deformabilità globale di queste strutture. Se il loro vantaggio economico è interessante, il passo successivo è un modello specialistico, non l’interpretazione della sola inerzia dell’impalcato come rigidezza del ponte completo.

## Risolvere i messaggi più comuni

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

## Esportare un confronto leggibile

Usa Esporta quantità CSV per il computo della soluzione corrente e l’export delle quote per ricostruirne dimensioni e sviluppi. Il pulsante Immagine PNG conserva il disegno; la relazione Word del progetto riporta geometria, ipotesi, quantità e avvisi. Per una consegna tecnica aggiungi il PDF e controlla che tabelle, unità e figure siano leggibili.

Accompagna ogni alternativa con obiettivo, dati del sito, blocchi attivati, griglie, listino, fattori ambientali e versione del motore. Riporta numero dei tentativi, delle geometrie ammesse e motivo della selezione. Se scegli la terza alternativa perché più costruibile, scrivilo esplicitamente: la posizione in classifica descrive soltanto il criterio scelto dal software.

## Quanto fidarsi dei risultati

La revisione documentata ha superato 44.259 asserzioni della suite generale. Una nuova verifica indipendente, senza sito e senza librerie ANTHEA nel calcolo dei valori attesi, ha controllato 100 travi con un secondo solutore FEM e quantità, costi e graduatoria di 26 geometrie di un caso imposto. Sono stati eseguiti 80.133 confronti numerici e corretto un difetto di arrotondamento che poteva aggiungere 5 cm a un’altezza esatta.

Questi riscontri aumentano la fiducia nella correttezza delle equazioni implementate e della ricerca discreta. Non validano automaticamente le incidenze convenzionali, i costi consuntivi o la sicurezza di un ponte reale. I 1.000 casi storici del sito avevano invece mostrato differenze e non dimostrano equivalenza fra i due programmi. Il rapporto teorico della stessa revisione contiene numeri, metodi e percorsi delle evidenze.

Prima di scegliere definitivamente verifica che la configurazione sia fisicamente costruibile, che il modello strutturale dedicato confermi le sezioni e che la valutazione geotecnica confermi le fondazioni. Aggiorna poi quantità e prezzi con le informazioni nuove e ripeti il confronto. Questo passaggio permette al predimensionamento di accompagnare il progetto senza attribuirgli prestazioni che non calcola.
