## Palo verticale

### Compilazione ordinata

Impostare tecnologia del palo, diametro, lunghezza, peso del materiale, azioni assiali e coefficienti. Compilare poi le stratigrafie. Ogni nuovo sondaggio parte con una riga da completare, non con un terreno automaticamente valido. Inserire nome, spessore, famiglia del terreno, addensamento dove richiesto, pesi di volume e parametri di resistenza.

Per un calcolo drenato occorrono i parametri efficaci. Per il ramo non drenato degli strati coesivi occorre Cu; il programma considera anche la posizione della falda per decidere il tratto nel quale applicare quel ramo. Non usare c′ e Cu come sinonimi. Negli strati granulari il contributo c′ alla resistenza laterale drenata è nullo, anche se il campo contiene un numero.

Indicare la presenza e la profondità della falda. L'effetto sulle tensioni del terreno e l'opzione di sottospinta sul peso proprio sono aspetti distinti. Spuntare la sottospinta solo secondo l'ipotesi di peso immerso che si vuole applicare al palo; non aspettarsi che questo comando definisca da solo la stratigrafia satura.

«Copia in» e «Copia da» permettono di riutilizzare gli strati fra sondaggi creando copie indipendenti. Controllare la destinazione prima di sovrascriverla. Il pulsante meno della riga elimina uno strato; quello della linguetta elimina la stratigrafia. Il colore identifica il terreno nel profilo e non costituisce un parametro di calcolo.

### Coefficienti e gruppo di pali

Verificare K e μ proposti per la tecnologia e l'addensamento. Controllare l'abaco Nq e la posizione del punto: un parametro sul bordo dell'abaco non dimostra che il terreno appartenga al campo sperimentale. Il numero di indagini scelto per ξ non è semplicemente il numero di linguette visibili. Deve rappresentare la base conoscitiva che si intende utilizzare.

Selezionare nessuna riduzione, Converse Labarre, Feld oppure efficienze definite dall'utente. Per i metodi geometrici servono numeri di pali e interassi coerenti. Il risultato è una riduzione della capacità per palo nel modello: non è una verifica completa del blocco di terreno, della distribuzione dei carichi nel plinto o dei cedimenti di gruppo.

### Leggere i risultati

Le curve drenate sono verdi e quelle non drenate viola; compressione e trazione si distinguono anche per tono e tratteggio. Le azioni sono rappresentate separatamente. Alla lunghezza di progetto leggere contributo laterale, punta, resistenza in compressione, resistenza in trazione e relativi coefficienti. La trazione non utilizza automaticamente gli stessi fattori del ramo di compressione.

La punta e il laterale minimo possono provenire da sondaggi diversi. La curva costruita con i minimi delle componenti non rappresenta necessariamente un unico sondaggio reale. Confrontare anche le curve delle singole stratigrafie prima di interpretare il motivo della riduzione.

Se una stratigrafia non raggiunge la quota richiesta, il calcolo non inventa gli strati mancanti. Integrare l'indagine o rivedere la lunghezza. La casella che disattiva il laterale elimina il contributo resistente di quel tratto, ma ne conserva il peso e l'effetto sulle tensioni negli strati sottostanti. È utile per escludere un tratto non affidabile, non per modellare automaticamente l'attrito negativo.

### Esempio di controllo manuale

Per una sola tratta con D = 1 m, lunghezza attiva 10 m e resistenza laterale uniforme τ = 50 kPa, il contributo laterale è π × 1 × 10 × 50 = 1570,8 kN. Questo è un controllo della geometria e delle unità, prima dei coefficienti ξ, γ ed η. Se il foglio restituisce un ordine di grandezza mille volte diverso, verificare MPa contro kPa e metri contro millimetri.

Procedere poi con i parametri realmente variabili del terreno. Non inserire τ = 50 nel campo Cu o c′ aspettandosi lo stesso risultato: quei parametri entrano in formule differenti. L'esempio serve a leggere il contributo restituito, non a sostituire il modello geotecnico.

