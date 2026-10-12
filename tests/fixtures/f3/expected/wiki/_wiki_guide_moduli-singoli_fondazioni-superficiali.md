## Fondazioni superficiali

Il foglio Geotecnica → Fondazioni superficiali verifica la capacità portante statica di una fondazione su strato portante omogeneo, con piano campagna e base orizzontali. Esamina una combinazione e un sondaggio alla volta: la condizione governante dell'opera deve essere individuata confrontando tutti i casi pertinenti. Non esegue verifiche di scorrimento, cedimento, stabilità globale, sisma o resistenza strutturale.

La pagina mantiene insieme dati, tabella degli strati ed esito. Le schede cambiano soltanto il riquadro grafico: Geometria mostra vista laterale e pianta; Stratigrafia amplia il profilo; Area efficace, Tensioni, Andamenti e Rottura mostrano le letture del calcolo. Nei riquadri più piccoli i campi scorrono senza cambiare pagina. I gruppi Metodo e verifica, Geometria, Azioni e Stratigrafia si possono richiudere. Raccogli input nasconde temporaneamente i pannelli e amplia il grafico; Mostra input li ripristina. La disposizione viene salvata nel foglio e non invalida un calcolo già eseguito. I dettagli della verifica si espandono sotto il riepilogo.

### Preparazione e scelta del metodo

Scegliere geometria, drenaggio, metodo e profilo. Eurocodice 7 indica le espressioni dell'allegato D della EN 1997-1:2004/AC:2009. Il profilo NTC 2018 applica M1 e R3 alla portanza; richiede azioni A1 già combinate. La modalità Confronto caratteristico non produce un esito di conformità normativa. Non confrontare la resistenza caratteristica con azioni di progetto attribuendo al rapporto il significato di una verifica SLU.

Le formulazioni disponibili hanno campi differenti. Terzaghi accetta carichi verticali centrati e una striscia, un quadrato o un cerchio con D/B non maggiore di 1. Brinch Hansen accetta soltanto carichi normali. Meyerhof accetta carichi inclinati soltanto sulla striscia. Eurocodice 7 e Vesić trattano l'inclinazione delle azioni nei due assi. Il cerchio deve essere centrato; la sua larghezza è il diametro. Queste restrizioni sono controllate dal calcolo.

### Geometria terreno falda e azioni

Inserire B e L in metri e D come profondità positiva dal piano campagna. X è parallelo a B e Y a L. My produce eccentricità lungo B; Mx lungo L. La compressione N è positiva; le azioni trasversali e i momenti possono avere entrambi i segni. Per la striscia tutti i valori si riferiscono a un metro di sviluppo: Hy e Mx devono essere nulli. B′ e L′ nei risultati sono ordinati dal lato efficace minore al maggiore; ex ed ey mantengono gli assi originali.

La tabella usa la gestione degli strati del palo a portanza verticale: sondaggi selezionabili, aggiunta e rimozione degli strati, colori, spessore in metri, tipologia Granulare o Coesivo, γ e γsat in kN/m³, φ′ in gradi, c′ e cu in kPa. Lo spessore è verticale dal piano campagna verso il basso. Un γsat vuoto segue γ dello stesso strato, come nel palo verticale; non equivale a un valore saturo misurato. Assegnarlo quando disponibile. La tipologia non converte automaticamente c′ in cu: il drenaggio scelto stabilisce quali resistenze usa il calcolo.

La falda è idrostatica e la sua profondità è non negativa dal piano campagna. Sopra la base sono ammessi materiali diversi, integrati nel confinamento q. Sotto la base tutti gli strati devono avere proprietà resistenti e pesi omogenei: confermare l'ipotesi di semispazio, estesa oltre la profondità rappresentata. All'interfaccia il terreno portante è quello inferiore. Un profilo eterogeneo sotto la base viene conservato, ma il calcolo si arresta senza ricorrere a medie. Il profilo deve raggiungere una quota inferiore al piano di posa.

Invia / carica terreno trasferisce una copia del sondaggio verso gli altri fogli compatibili, mantenendo parametri, falda e quota di riferimento. Nei Progetti gli stessi campi partecipano al confronto e all'applicazione dei dati comuni; spessori e numero degli strati devono essere coerenti. Non vengono trasferite azioni o formule specifiche del palo. La classe del calcestruzzo, fck e il diagramma usano catalogo e denominazioni comuni. Restano dati descrittivi del progetto: non modificano la capacità portante e non introducono una verifica strutturale o un peso proprio automatico.

N, Hx, Hy in kN e Mx, My in kNm sono risultanti al piano di posa già combinate, comprensive dei pesi della fondazione e del ricoprimento. Nel drenato N è efficace al netto della sottospinta; nel non drenato è totale, senza sottrarre la pressione interstiziale al piano di posa. Il foglio non genera le combinazioni né aggiunge automaticamente questi pesi. Il sovraccarico esterno inserito in kPa contribuisce al confinamento laterale q: non viene aggiunto a N. Per NTC 2018 confermare esplicitamente la preparazione delle azioni A1.

Il meccanismo di rottura è una scelta del progettista. Le riduzioni di Terzaghi e Vesić modificano i parametri drenati; non costituiscono un calcolo dei cedimenti o una diagnosi della compressibilità. La riduzione per densità relativa ammette solo sabbie con c′ nullo e Dr tra 0 e 2/3. I parametri pertinenti compaiono dopo la selezione. Azioni non ammissibili già presenti rimangono visibili per essere corrette.

### Lettura delle viste grafiche

Area efficace mostra l'impronta geometrica, il rettangolo efficace traslato sulla risultante e il contorno tratteggiato della zona compressa. Le dimensioni B′X e L′Y mantengono gli assi della pianta, anche quando i lati vengono riordinati nel calcolo dei coefficienti. L'area efficace A′ della portanza non coincide in generale con la zona a pressione positiva.

Tensioni mostra la mappa in kPa, i valori ai vertici, minimo e massimo; il puntatore legge il campione più vicino. Andamenti presenta due tagli passanti per la risultante. Sono pressioni efficaci nel drenato e totali nel non drenato, coerenti con N assegnato. Il modello convenzionale è affine senza trazione: rappresenta l'equilibrio del contatto, non calcola cedimenti. Il confronto di portanza usa N/A′, non il massimo della mappa.

Rottura mostra un riferimento di Prandtl per striscia su terreno privo di peso, con cuneo e ventagli logaritmici nelle sezioni X e Y. È uno schema illustrativo; non è la superficie critica calcolata per il metodo selezionato, non descrive una rottura tridimensionale e non determina profondità o estensione dell'indagine.

Come controllo delle pressioni, usare B = 2 m, L = 3 m, N = 600 kN, My = 300 kNm e Mx = 0. L'eccentricità X vale 0,5 m; A′ = 3 m². Il contatto triangolare ha larghezza 1,5 m e area compressa 4,5 m², con pmin = 0 e pmax = 266,667 kPa. Il valore N/A′ è invece 200 kPa. Il confronto distingue quindi tre grandezze con significato diverso.

### Esempio verificato e lettura dei risultati

Per il metodo Eurocodice 7 usare B = 2 m, L = 3 m, D = 1 m, φ′ = 30°, c′ = 0, γ = 18 kN/m³ sopra e sotto la base, assenza di falda e sovraccarico esterno, N = 600 kN e altre azioni nulle. Assumere base ruvida, rottura generale e profilo NTC 2018; confermare che i 600 kN siano la risultante A1 completa.

| Quantità | Atteso indipendente |
|---|---:|
| Nq | 18,401122 |
| Nγ | 20,093085 |
| sq / sγ | 1,333333 / 0,8 |
| q efficace | 18 kPa |
| Pressione limite lorda | 730,967360 kPa |
| Area efficace | 6 m² |
| γR | 2,3 |
| Resistenza di progetto | 1906,871374 kN |
| Indice N/R | 0,314651 circa |

Il caso risulta verificato per la sola capacità portante. La tabella dei contributi separa coesione, confinamento e peso del terreno. Nei casi Hansen non drenati la tabella dedicata espone gli incrementi s′, d′ e i′ e il fattore 1 + s′ + d′ − i′: non moltiplicare gli incrementi. La pressione netta è un dettaglio informativo; il confronto del foglio usa la resistenza lorda e la risultante coerente.

### Salvataggio errori e report

Il salvataggio del foglio o del progetto conserva scelte, azioni e nome della combinazione. Dopo una modifica i risultati precedenti sono invalidati; alla riapertura premere Calcola. Report Word e PDF produce le due edizioni affiancate, con dati, coefficienti, esito, campo di applicazione e versione della libreria. F1 o Guida apre questa pagina; la [teoria della portanza](wiki:fondazioni-superficiali) descrive le formule.

Una risultante esterna alla base o azioni oltre il dominio resistente del metodo producono resistenza nulla e, nel profilo normativo, esito Non verificata. Con area efficace nulla la pressione agente è indefinita; con resistenza nulla lo è l’indice N/R. Il foglio mostra un trattino e gli avvisi, senza sostituire tali rapporti con zero. Risultante normale non compressiva, numeri non finiti, dati incoerenti e ipotesi non implementate arrestano invece il calcolo. Le mappe non disponibili sono segnalate. Un messaggio sulla libreria mancante indica che occorre installare il rilascio approvato previsto per il foglio: i dati restano salvabili, ma non viene prodotto alcun risultato.

<!-- pagebreak -->

