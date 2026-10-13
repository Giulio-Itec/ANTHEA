## Ripercorrere un esempio strutturale con controlli indipendenti

Un esempio svolto è utile quando permette di ricostruire il calcolo, non soltanto di copiarne le formule. Questa pagina propone un metodo di lettura e alcuni controlli indipendenti da applicare a esempi, dispense o calcoli di confronto prima di usarli con ANTHEA.

### Ricostruire un risultato senza copiare le sole formule

Prima di seguire il procedimento, annotare geometria, vincoli, combinazioni, convenzioni e norma. Poi riprodurre un passaggio d’equilibrio e uno di compatibilità. Per una trave, verificare somma delle forze e dei momenti; per una sezione, ricostruire risultanti delle tensioni; per un telaio, controllare il numero e il significato dei gradi di libertà. Il confronto è valido solo se modello e input coincidono.

In un esempio di trave composta, separare acciaio solo, getto, collaborazione, ritiro e viscosità. In un esempio di dominio N–M, controllare il segno della compressione, le deformazioni limite e la discretizzazione. In una connessione, identificare l’intero percorso del carico: bulloni, piatti, saldature e parti collegate possono governare in modi diversi.

### Due controlli rapidi utili

Per una mensola elastica con carico P in punta, L = 3 m, E = 210 GPa e I = 8 × 10⁻⁵ m⁴, con P = 10 kN si hanno momento all’incastro di 30 kNm e spostamento PL³/(3EI) ≈ 5,36 mm. Una differenza di tre ordini di grandezza suggerisce prima di tutto un errore fra m e mm o fra Pa e MPa.

Per l’asta compressa ideale con vincoli incernierati, Ncr = π²EI/L²: con gli stessi E, I e L risulta circa 18423 kN. Questo è un carico critico elastico ideale, non la resistenza di progetto della membratura. Sostituire la lunghezza libera d’inflessione con metà del valore quadruplica Ncr: la scelta dei vincoli pesa più di molti arrotondamenti.

### Norme e campo dell’esempio

Un esempio svolto può riferirsi a NTC2008, ACI318, Eurocodici o a un altro programma di calcolo. Il nome dell’argomento non rende equivalenti coefficienti, classi di sezione, domini e criteri di verifica. Il rimando a un modulo ANTHEA segnala un tema comune; non promette la riproduzione automatica di ogni esempio. Temi come punzonamento, connessioni, legno o macchine vibranti restano utili alla formazione anche quando non esiste un modulo corrispondente.

Gli esempi numerici di questa pagina sono originali.

### Proseguire nella Wiki

- [Elementi Beam](wiki:beam)
- [Instabilità delle aste compresse](wiki:euler)
- [Sezione composta da ponte](wiki:sezione-composta-da-ponte)

