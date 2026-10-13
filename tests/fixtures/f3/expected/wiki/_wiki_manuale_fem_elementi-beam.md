## Elementi Beam

Una trave reale occupa un volume; un elemento beam descrive invece come si muove una linea e come ruotano le sezioni lungo quella linea. Questa riduzione rende leggibile il percorso dei carichi e permette di analizzare telai con molti elementi, purché le dimensioni trasversali e i fenomeni locali non governino la risposta cercata. Il capitolo accompagna la scelta del modello, la lettura dei risultati e il trasferimento delle azioni alla verifica della sezione in Anthea.

### Cos'è un elemento beam

Il beam è un elemento finito monodimensionale con una sezione associata. L'asse geometrico collega due nodi, mentre area, inerzie e rigidezza torsionale rappresentano la parte trasversale. Il calcolo cerca spostamenti e rotazioni compatibili con equilibrio, vincoli e legame costitutivo. Le tensioni tridimensionali complete non sono tutte incognite indipendenti: si ricostruiscono a partire dalle ipotesi di trave.

Una trave trasferisce un carico trasversale agli appoggi soprattutto mediante flessione e taglio. La flessione produce una coppia di risultanti di trazione e compressione. Allontanare queste risultanti aumenta il braccio della coppia: per questo una sezione alta può essere molto più rigida di una bassa con la stessa area. Una forza allineata all'asse produce invece sforzo normale; un carico eccentrico rispetto al centro di taglio può introdurre torsione.

### Idealizzazione della struttura reale

Scegliere la linea d'asse significa decidere dove si trasferiscono le azioni. In una trave omogenea spesso si usa il baricentro; in sezioni composte o con eccentricità bisogna dichiarare il riferimento effettivo. Larghezza dell'appoggio, nodi estesi e zone di collegamento non scompaiono fisicamente: sono condensati in vincoli, offset o legami. Un modello è adeguato se restituisce le grandezze necessarie alla decisione, non perché assomiglia al disegno architettonico.

Confronta prima un modello semplice con equilibrio manuale e poi aggiungi dettagli. Per un telaio, controlla quali nodi sono realmente rigidi, quali consentono rotazione e quali impediscono traslazione. Una connessione bullonata non è automaticamente una cerniera ideale; una connessione gettata non è automaticamente infinitamente rigida.

### Nodi e asse dell'elemento

I nodi iniziale e finale definiscono lunghezza e verso dell'asse locale x. Due punti con coordinate uguali non sono necessariamente lo stesso nodo: se i rispettivi gradi di libertà non sono condivisi o collegati, gli elementi restano scollegati. Nei modelli importati controlla la connettività numerica, oltre alla coincidenza visiva.

![Fig. 6.1 Elemento beam tra i nodi i e j con asse locale x e direzioni trasversali y e z.](../../X.Desktop/Assets/Wiki/beam-axes.png)

La linea del beam rappresenta un riferimento strutturale. Non usare la sola linea inferiore del disegno come asse senza verificare l'eccentricità rispetto alla sezione. Cambiare il verso i-j richiede controllare assi, carichi locali e convenzioni dei risultati.

### Sistema locale e globale

Il sistema globale permette di assemblare elementi orientati diversamente. Il sistema locale permette di descrivere la risposta assiale e le due flessioni della singola trave. La trasformazione tra i due è una rotazione delle componenti, non una modifica del fenomeno fisico. Le inerzie Iy e Iz devono corrispondere alla sezione orientata nei rispettivi assi locali.

Definisci sempre una terna destrorsa. L'asse x coincide normalmente con i-j; gli altri due assi dipendono dalla regola di orientamento adottata dal solutore. Una trave verticale richiede particolare attenzione: una regola basata su un vettore parallelo alla trave può diventare indeterminata. Visualizza la terna e controllala su un elemento rappresentativo prima di assegnare carichi locali a un gruppo.

### Gradi di libertà

Nel beam spaziale classico ogni nodo ha tre traslazioni e tre rotazioni. Le dodici componenti dell'elemento descrivono movimento assiale, due flessioni e torsione. Un modello piano ne conserva normalmente tre per nodo: due traslazioni e una rotazione. Formulazioni con ingobbamento possono aggiungere altre incognite: non assumere che tutti i beam abbiano esattamente gli stessi gradi di libertà.

![Fig. 6.2 Traslazioni e rotazioni nodali nel piano; deformata amplificata a solo scopo illustrativo.](../../X.Desktop/Assets/Wiki/beam-dof.png)

Una rotazione nodale è un'incognita cinematica. Non equivale sempre alla pendenza della linea deformata: questa uguaglianza vale nella cinematica Euler-Bernoulli, mentre nel modello Timoshenko la differenza rappresenta la deformazione a taglio.

### Rigidezza assiale

```math
k_a = \frac{EA}{L}
\delta = \frac{NL}{EA}
```

E è il modulo elastico, A l'area resistente e L la lunghezza; N è uno sforzo assiale costante. Con E in N/mm², A in mm² e L in mm, kₐ è in N/mm e δ in mm. La seconda espressione vale per un'asta prismatica elastica con forza costante. Raddoppiare la lunghezza dimezza la rigidezza e raddoppia l'allungamento, a parità di N, E e A.

Un'area sovrastimata irrigidisce il percorso assiale e può attirare carico da elementi paralleli. In un sistema iperstatico non basta che la verifica della singola sezione sia soddisfatta: la distribuzione delle azioni dipende dalle rigidezze relative.

### Dalla cinematica alla matrice

Nel problema flessionale piano Euler-Bernoulli le incognite di un elemento a due nodi sono spostamento trasversale e rotazione ai due estremi. L'interpolazione cubica di Hermite garantisce continuità di spostamento e pendenza. Indicando con $B$ la matrice delle derivate seconde delle funzioni di forma e con $K_e$ la matrice di rigidezza elementare:

```math
K_e=\int_0^LB^T EI B\,dx
```

La matrice trasforma spostamenti e rotazioni nodali nelle azioni nodali elastiche. Il carico distribuito deve entrare anche nel vettore nodale coerente: applicare soltanto metà della risultante a ogni nodo può perdere le coppie equivalenti. Un elemento libero possiede moti rigidi; la singolarità scompare soltanto con vincoli fisicamente sufficienti, non aumentando artificialmente la rigidezza.

![Figura 09.1 — Trave di 8 m con carico 25 kN/m, cerniera e carrello. Le reazioni sono 100 kN; il momento positivo è parabolico e raggiunge 200 kNm in mezzeria.](../../X.Desktop/Assets/Wiki/beam-benchmark.svg)

Nello schema, il momento positivo tende le fibre inferiori. L'area sotto il carico è 200 kN e coincide con la somma delle reazioni. Il diagramma nullo agli appoggi è una verifica indipendente dei rilasci terminali. L'esempio è statico, elastico e del primo ordine; non rappresenta fessurazione, viscosità o variazioni di rigidezza.

La formulazione variazionale e l'interpolazione sono documentate nel capitolo *Euler-Bernoulli beam elements* del manuale di modellazione computazionale TU Delft. Il segno di curvatura può differire fra testi: la relazione costitutiva deve essere letta con la stessa convenzione del momento, senza combinare definizioni provenienti da solutori diversi.

### Flessione

```math
\kappa = \frac{M}{EI}
\sigma = \frac{M}{W}
```

κ è la curvatura elastica, M il momento e I l'inerzia rispetto all'asse di flessione. W è il modulo resistente elastico relativo alla fibra considerata. Con M in Nmm e EI in Nmm², κ è in 1/mm; σ risulta in N/mm². La formula della tensione descrive una sezione omogenea in regime elastico e non sostituisce la verifica non lineare di una sezione in c.a.

Le componenti della matrice flessionale contengono termini proporzionali a EI/L³, EI/L² ed EI/L: traslazioni e rotazioni hanno unità differenti. Non confrontare direttamente i numeri di due termini senza considerare le relative incognite. Nei telai, la rigidezza flessionale governa anche la ripartizione dei momenti fra travi e pilastri.

### Taglio

Il taglio è la risultante delle tensioni tangenziali sulla sezione. Nel modello Timoshenko la rigidezza trasversale è rappresentata da GAs, dove G è il modulo a taglio e As un'area efficace dipendente dalla formulazione e dalla forma della sezione. L'area efficace non coincide necessariamente con l'area geometrica totale.

```math
\gamma = \frac{V}{GA_s}
```

V è la forza di taglio e γ la deformazione angolare, adimensionale. Trascurare questa deformazione può sottostimare gli spostamenti di travi corte, sezioni con anima deformabile o materiali con basso modulo a taglio. La presenza di un risultato V nel solutore non dimostra che la deformabilità a taglio sia stata inclusa: anche un beam Euler-Bernoulli fornisce il taglio per equilibrio.

### Torsione

```math
\theta = \frac{TL}{GJ}
```

Per una barra prismatica in torsione uniforme di Saint-Venant, T è il momento torcente e J la costante torsionale; con unità coerenti θ è una rotazione in radianti. J non coincide generalmente con il momento polare d'inerzia: l'identificazione è corretta per sezioni circolari, ma può essere gravemente errata per sezioni aperte sottili.

Il beam ordinario non descrive automaticamente bimomento, torsione non uniforme e ingobbamento impedito. Vicino a un incastro o a un diaframma può essere necessario un modello con ingobbamento oppure una modellazione locale shell. Se il carico non passa per il centro di taglio, controlla la torsione generata dall'eccentricità.

### Euler-Bernoulli e Timoshenko

Euler-Bernoulli assume che le sezioni restino piane e ortogonali all'asse deformato, trascurando la deformazione a taglio. Timoshenko mantiene una rotazione di sezione indipendente dalla pendenza della linea d'asse e include il contributo a taglio. La scelta dipende dalla precisione richiesta e dal rapporto fra contributi deformativi; non esiste un unico rapporto L/h che renda corretto ogni modello.

| Aspetto | Euler-Bernoulli | Timoshenko |
| --- | --- | --- |
| Cinematica | Rotazione legata alla pendenza | Rotazione indipendente |
| Taglio deformabile | Trascurato | Incluso mediante area efficace |
| Controllo | Adeguatezza della snellezza e delle ipotesi | Parametri di taglio e possibili fenomeni di locking |

Un elemento numerico mal formulato può diventare artificialmente rigido nel limite di trave snella: è il locking a taglio. Raffinare la mesh senza comprendere il fenomeno non è sempre sufficiente. Verifica il benchmark della formulazione e confronta una soluzione analitica coerente con le stesse ipotesi.

### Proprietà della sezione

L'area governa la risposta assiale; Iy e Iz governano le flessioni; J governa la torsione di Saint-Venant; le aree efficaci governano il taglio deformabile. Il baricentro e il centro di taglio descrivono riferimenti differenti. Per sezioni non simmetriche occorre considerare anche il prodotto d'inerzia o usare assi principali.

Nel c.a. l'inerzia geometrica integra non è automaticamente la rigidezza efficace di esercizio di una sezione fessurata. Per sezioni composte, la rigidezza dipende da materiali, omogeneizzazione, collaborazione e fasi. Specifica la rigidezza adottata nel modello globale e non scambiarla con la resistenza ultima calcolata separatamente.

### Orientamento

Ruotare una sezione rettangolare di 90° scambia i ruoli delle due inerzie. Nel caso di una sezione b × h, l'inerzia rispetto all'asse parallelo alla base è bh³/12: il cubo dell'altezza rende l'orientamento decisivo. Il disegno della sezione e la freccia del carico devono essere coerenti con gli assi usati per i risultati.

Controlla il segno dei momenti dopo ogni cambio di orientamento. Nella sezione in c.a. Anthea, il piano della sezione è x-y e Mx è associato alla coordinata y; non coincide automaticamente con la terna x-y-z di un beam, il cui x è longitudinale. Il trasferimento richiede una mappatura esplicita.

### Offset ed eccentricità

Un offset collega il nodo alla sezione terminale tramite un tratto rigido o una trasformazione cinematica. Non equivale a spostare soltanto il disegno. Trasferisce anche momenti quando la risultante di forza agisce fuori dal riferimento. Una forza F con eccentricità e introduce una coppia di modulo Fe nel caso piano.

![Fig. 6.3 Offset tra asse del beam e punto di applicazione della forza; la coppia aggiuntiva dipende dal braccio e.](../../X.Desktop/Assets/Wiki/beam-offset.png)

Usare contemporaneamente un offset rigido e una coppia già inclusa nei carichi può contare due volte l'eccentricità. Controlla il punto rispetto al quale sono fornite le azioni. Non modellare come rigidissimo un collegamento realmente deformabile solo per risolvere un problema di connettività.

### Releases

Una release elimina il trasferimento di una specifica azione terminale rendendo libero il grado di libertà coniugato. Una cerniera flessionale non elimina per forza taglio, sforzo normale e torsione. In un modello spaziale occorre dire quale rotazione è rilasciata e rispetto a quale asse.

Rilasciare la stessa rotazione su tutti gli elementi di un nodo senza un controllo dei vincoli può creare un meccanismo. Il messaggio di matrice singolare è una conseguenza del modello: non va eliminato aggiungendo molle arbitrarie. Disegna la cinematica consentita e controlla che non esista uno spostamento rigido senza energia.

### Discretizzazione

Inserisci nodi dove cambiano carico, sezione, connessione o vincolo. Per carichi uniformi usa il vettore di carico coerente previsto dall'elemento, anziché sostituire il carico con forze concentrate senza verificarne l'equivalenza. Una mesh più fitta migliora alcune approssimazioni, ma non corregge un vincolo sbagliato o un'ipotesi costitutiva inadatta.

Confronta almeno due discretizzazioni e osserva la stabilizzazione delle grandezze decisive: freccia, reazioni, momenti critici. Il numero di elementi necessario dipende dall'interpolazione e dal risultato cercato. Una soluzione nodale esatta in un caso particolare non garantisce la ricostruzione esatta di tutto il diagramma interno.

### Risultati FEM

Prima delle tensioni controlla deformata e reazioni. Le reazioni devono equilibrare le azioni esterne; la deformata deve rispettare i vincoli; il percorso dei carichi deve essere plausibile. Poi leggi N, Vy, Vz, T, My e Mz nel sistema dichiarato dal solutore.

Le azioni terminali, le azioni sulle facce di una sezione e le risultanti interne possono usare segni diversi. Un salto del diagramma di taglio può essere fisico in corrispondenza di una forza concentrata. Una discontinuità di momento senza coppia applicata richiede invece controllare la ricostruzione, il verso e la connettività.

### Convenzioni di segno

Indica su uno schizzo un momento positivo e le fibre che comprime. Definisci il verso positivo dello sforzo normale. Nell'interfaccia corrente della sezione in c.a. Anthea usa N negativo a compressione; gli archivi precedenti con compressione positiva vengono migrati dal workspace. Il motore legacy può adottare una convenzione diversa da quella esposta nella UI. Confronta il riferimento del solutore FEM e quello della scheda, converti il segno una volta nel passaggio dei dati e mantieni traccia del riferimento originale.

Le verifiche biassiali richiedono conservare l'accoppiamento fra N, Mx e My della stessa combinazione. Non comporre una terna prendendo separatamente i massimi assoluti da combinazioni diverse senza dichiarare la scelta conservativa e controllarne il significato fisico.

### Limiti del modello

Un beam non risolve automaticamente concentrazioni di tensione, diffusione su appoggi, instabilità locale di piastre, ovalizzazione o dettagli delle saldature. Un modello elastico del primo ordine non include da solo plasticità, fessurazione evolutiva, grandi rotazioni e instabilità geometrica. Ogni estensione richiede una formulazione e parametri coerenti.

Quando una zona locale governa, usa un modello dedicato e condizioni al contorno derivate dal modello globale. Il passaggio beam-shell-solid richiede compatibilità del trasferimento di forza e momento: collegare un solo nodo può non rappresentare il comportamento del giunto reale.

### Errori comuni

> ERRORI COMUNI
> Nodi coincidenti ma scollegati; elementi duplicati che raddoppiano la rigidezza; inerzie scambiate; J sostituito dal momento polare; release che generano un meccanismo; metri mescolati a millimetri; carico locale applicato nel verso globale; offset ed eccentricità conteggiati due volte.

Per trovare un errore riduci il modello al percorso coinvolto. Applica un carico unitario, visualizza la deformata e confronta le reazioni con l'equilibrio. Un risultato numerico convergente può essere perfettamente coerente con un modello fisicamente sbagliato.

### Regole pratiche

> REGOLA PRATICA
> Prima verifica equilibrio, connettività e assi; poi raffina la mesh. Usa una trave appoggiata o una mensola come controllo della catena unità-carichi-rigidezza. Queste sono procedure di controllo del modello e non verifiche normative.

Per la trave appoggiata prismatica elastica, il momento massimo cresce con L² e la freccia flessionale con L⁴ a carico lineare costante. Piccole modifiche della luce possono quindi influenzare gli spostamenti più dei momenti. Questo ordine di grandezza aiuta a individuare errori di scala, ma non sostituisce il calcolo del sistema effettivo.

### Esempio concettuale

Considera una trave semplicemente appoggiata di luce L = 8 m con carico uniforme q = 25 kN/m. Per questo esempio q è già un'azione di progetto assegnata: non stiamo ricavando una combinazione normativa. Le reazioni sono ciascuna qL/2 = 100 kN; la somma 200 kN equilibra il carico totale.

```math
M_{\max} = \frac{qL^2}{8} = \frac{25\cdot8^2}{8} = 200\ \mathrm{kN\,m}
V_{app} = \frac{qL}{2} = 100\ \mathrm{kN}
```

![Fig. 6.4 Trave appoggiata con carico uniforme; diagramma qualitativo del momento positivo massimo in mezzeria.](../../X.Desktop/Assets/Wiki/beam-load.png)

Per una sezione rettangolare integra b = 600 mm e h = 800 mm, $I = \frac{bh^3}{12} = 25{,}6\cdot10^9\ \mathrm{mm^4}$. Se si assume, solo per il controllo elastico del modello, E = 30.000 N/mm², la freccia flessionale è $v_{\max} = \frac{5qL^4}{384EI} \simeq 1{,}74\ \mathrm{mm}$, usando q = 25 N/mm e L = 8.000 mm. È la freccia del modello omogeneo non fessurato: non è una verifica SLE del c.a.

Confronta la reazione e il momento analitico con il modello beam. Poi trasferisci la terna N = 0, Mx = 200 kNm, My = 0 a una sezione in c.a. la cui altezza è lungo y. Il valore 200 kNm deve essere associato all'asse della sezione che produce compressione e trazione lungo l'altezza. Il segno definisce quale bordo è compresso.

### Interazione con Anthea

Il pulsante Apri esempio crea una Sezione in c.a. con geometria rettangolare 600 × 800 mm, materiale C35/45 e armature iniziali del modulo: 4Ø20 superiori, 6Ø24 inferiori, 2Ø16 per lato, staffe Ø10/200 e copriferro netto 70 mm. La sola combinazione plastica contiene N = 0, Mx = 200 kNm, My = 0. Le altre famiglie restano vuote, perché non sono state definite azioni di esercizio o accidentali.

Questi valori costituiscono un esempio didattico, non una proposta esecutiva. Modifica l'altezza mantenendo luce e azione assegnata; osserva come cambiano dominio resistente e deformazioni di sezione. Modifica poi il segno di Mx per vedere il ruolo delle diverse armature ai due bordi. Il modulo verifica sezioni, non risolve la trave generale FEM né determina automaticamente il carico q.

[Guida del modulo Sezione in c.a.](/wiki/guide/moduli/sezione-ca)

### Riepilogo

Un beam concentra il comportamento di una struttura allungata in una linea con proprietà di sezione. La qualità del risultato dipende da cinematica, collegamenti, orientamento, rigidezze e unità. Verifica prima il comportamento globale; trasferisci poi azioni e convenzioni a una verifica locale appropriata. Raffinare una discretizzazione corretta e scegliere una formulazione adeguata sono due controlli distinti.

### Argomenti correlati

[Elementi Shell](/wiki/manuale/fem-e-modellazione/elementi-shell)

[Releases e connettività](/wiki/manuale/fem-e-modellazione/releases-e-connettivita)

[Instabilità delle aste compresse](/wiki/manuale/acciaio/instabilita-delle-aste-compresse)

### Riferimenti

- TU Delft, Computational Modelling, Euler-Bernoulli beam elements: derivazione della cinematica e dell'interpolazione.
- Il capitolo spiega un modello meccanico e non introduce coefficienti di verifica normativa. Le prescrizioni dei materiali e gli stati limite si consultano nella guida teorica globale, nelle sezioni dei moduli corrispondenti.

