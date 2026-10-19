## Spettri di risposta e isolamento sismico

Il modulo descrive la domanda sismica mediante spettri di accelerazione e un sistema traslazionale equivalente per l’isolamento. La parte Spettri riproduce le convenzioni numeriche del riferimento NTC 2008; l’estensione per isolamento applica la distinzione degli smorzamenti del §7.10.5.3.2 delle NTC 2018, Italia. Le due parti sono identificate separatamente per evitare di attribuire alla riproduzione numerica una validità normativa più ampia.

### Pericolosità e periodo di riferimento

Il reticolo originale contiene 10.751 nodi, con ag, F0, Tc∗ e ordinate spettrali per TR pari a 30, 50, 72, 101, 140, 201, 475, 975 e 2475 anni. Le coordinate in gradi decimali sono arrotondate a cinque decimali secondo il riferimento. L’interpolazione locale utilizza la distanza inversa oppure la superficie rigata; nodi e pesi effettivi sono esposti nei risultati. Non si estrapola al di fuori del reticolo. La localizzazione attraverso il catalogo comunale non identifica necessariamente il sito dell’opera.

La vita di riferimento e il tempo di ritorno derivano da:

```math
V_R = \max(V_N C_U,35), \qquad T_R = -\frac{V_R}{\ln(1-P_{VR})}
```

VN e VR sono in anni; CU è adimensionale. Le probabilità PVR sono 0,81, 0,63, 0,10 e 0,05 per SLO, SLD, SLV e SLC. Il TR adottato è limitato al campo dei dati; per compatibilità si conservano il confronto inferiore 30,11 anni dello SLV e il limite superiore 2474,9 anni dello SLC. Negli altri casi i limiti sono 30 e 2475 anni.

Tra due TR tabulati, i parametri sono interpolati logaritmicamente. Posti p1 e p2 i valori agli estremi:

```math
\ln p = \ln p_1 + \frac{\ln(T_R/T_{R1})}{\ln(T_{R2}/T_{R1})}\ln(p_2/p_1)
```

Il riferimento usa lo stesso rapporto per i tre parametri; nel caso particolare ag2/ag1 = 1 il denominatore viene posto pari a 1. Questa convenzione è conservata nella modalità compatibile. L’assegnazione manuale delle quattro terne sostituisce l’interpolazione per gli stati limite e viene dichiarata nei risultati.

### Forma dello spettro

ag è espresso come frazione di g, F0 è adimensionale e Tc∗ è in secondi. Le categorie di sottosuolo determinano SS e CC; ST deriva dalla categoria topografica e dalla quota relativa h/H. I periodi caratteristici sono TB = TC/3, TC = CC Tc∗ e TD = 1,6 + 4 ag. Il valore numerico di ag in questa espressione è in g.

| Sottosuolo | SS | CC |
| --- | --- | --- |
| A | 1 | 1 |
| B | 1,4 − 0,4 F0 ag, limitato a 1–1,2 | $1{,}1\,(T_c^*)^{-0{,}2}$ |
| C | 1,7 − 0,6 F0 ag, limitato a 1–1,5 | $1{,}05\,(T_c^*)^{-0{,}33}$ |
| D | 2,4 − 1,5 F0 ag, limitato a 0,9–1,8 | $1{,}25\,(T_c^*)^{-0{,}5}$ |
| E | 2 − 1,1 F0 ag, limitato a 1–1,6 | $1{,}15\,(T_c^*)^{-0{,}4}$ |

ST = 1 + (ST,max − 1) h/H; ST,max vale 1, 1,2, 1,2 e 1,4 per T1–T4. Per lo spettro elastico η = max[0,55; √(10/(5 + ξ))], con ξ in percentuale. Nel ramo Progetto con q si usa η = 1/q, con q = q0 Kr e Kr pari a 1 per regolarità in altezza oppure 0,8 negli altri casi.

Posto A = ag SS ST, la forma elastica orizzontale senza soglia inferiore è:

```math
S_a(T)=A[1+(\eta F_0-1)T/T_B] \quad (0\leq T<T_B)
```

```math
S_a(T)=A\eta F_0 \quad (T_B\leq T<T_C)
```

```math
S_a(T)=A\eta F_0 T_C/T \quad (T_C\leq T<T_D)
```

```math
S_a(T)=A\eta F_0 T_C T_D/T^2 \quad (T\geq T_D)
```

La parte compatibile conserva la soglia 0,2 ag per SLV e SLC, anche nella scelta Elastico, secondo le celle del riferimento. Per la componente verticale conserva TB = 0,05 s, TC = 0,15 s, TD = 1 s, Fv = 1,35 F0 √ag, valore iniziale ag Fv ST/F0 e plateau ag Fv ST/qv. La componente verticale del riferimento dipende dal proprio qv. Queste particolarità sono convenzioni di compatibilità, non prescrizioni da estendere ad altri profili normativi.

Le curve delle fasi 2 e 3 hanno 45 punti ciascuna fino a 4 s, con ascisse distinte per orizzontale e verticale. Il reticolo conserva anche gli 11 periodi delle ordinate tabulate. Esportare le coppie T, Sa della componente richiesta: non assumere che due righe con lo stesso indice abbiano lo stesso periodo.

### Sistema equivalente con isolamento

Il modello fisico è una massa M con dispositivi in parallelo, in una direzione traslazionale. Non comprende torsione né risposta verticale. Con nj dispositivi di rigidezza kj per gruppo e fattore di rigidezza α:

```math
K = \alpha\sum_j n_j k_j, \qquad T_{is}=2\pi\sqrt{\frac{M}{1000K}}
```

M è in kN·s²/m; kj e K sono in kN/mm. Il fattore 1000 converte K in kN/m, rendendo M/(1000K) dimensionalmente un tempo al quadrato. SLO e SLD utilizzano αSLE; SLV e SLC utilizzano αSLU. I fattori sono assegnati dal progettista e non rappresentano una legge universale del dispositivo.

Per T minore di 0,8 Tis lo spettro usa lo smorzamento della struttura; da T uguale a 0,8 Tis usa quello equivalente dell’isolamento, secondo NTC 2018 §7.10.5.3.2. La transizione può essere discontinua. Il calcolo adotta la forma elastica senza la soglia inferiore della modalità compatibile e non applica q0. I due smorzamenti sono ammessi tra 5% e 30%; il programma non estende il modello oltre questo intervallo.

```math
F=M\,g\,S_a(T_{is}), \qquad d=\frac{F}{K}, \qquad F_j=\frac{F k_j}{\sum_i n_i k_i}
```

g = 9,81 m/s², Sa è in g, F e la forza Fj del singolo dispositivo sono in kN, d è in mm. I fattori comuni di rigidezza si elidono nella ripartizione. La curva visualizzata contiene un campionamento uniforme e i punti caratteristici, compresi i due lati della soglia; forza e spostamento sono valutati direttamente a Tis, senza ricerca del campione precedente.

### Pendolo e esempio riproducibile

Il pendolo equivalente usa carico verticale N, attrito μ, spostamento positivo d e raggio equivalente R positivo:

```math
F_\mu=\mu N, \qquad K_e=N\left(\frac{1}{R}+\frac{\mu}{d}\right), \qquad \xi_e=\frac{2}{\pi}\frac{1}{1+d/(\mu R)}
```

```math
T_e=2\pi\sqrt{\frac{1}{g(1/R+\mu/d)}}
```

N è in kN, d e R in mm, Ke in kN/mm. ξe è una frazione, convertita in percentuale in interfaccia. Per il periodo del pendolo si conserva g = 9806 mm/s² dell’esempio; è distinto dal valore 9,81 m/s² usato per la forza globale. Il modello implementato richiede attrito strettamente positivo; l’eventuale impiego del risultato nel calcolo dell’isolamento resta soggetto al campo di smorzamento ammesso da quest’ultimo. Il raggio non viene ricavato dalla corsa.

Un caso riproducibile usa M = 412,6 kN·s²/m, otto dispositivi da 1,01 kN/mm, αSLU = 1, sottosuolo B, ST = 1, ξ struttura = 5% e ξ isolamento = 10%. Allo SLC assegnare ag = 0,24191837917593156 g, F0 = 2,3947171362491604 e Tc∗ = 0,3155006983889627 s. Si ottengono K = 8,08 kN/mm, Tis = 1,4198380147 s, SS = 1,1682695647 e CC = 1,3854556248.

| Grandezza allo SLC | Valutazione analitica | Ricerca del campione dell’esempio |
| --- | --- | --- |
| Sa [g] | 0,1701275566 | 0,1762843725 |
| F [kN] | 688,6093189 | 713,5296840 |
| d [mm] | 85,2239256 | 88,3081292 |

Il confronto campionato riproduce il campionamento dell’esempio, compreso il suo test del ramo di plateau sulla riga seguente; serve alla tracciabilità e non governa il risultato principale. Gli attesi indipendenti sono conservati separatamente dalle uscite del motore.

### Campo di applicazione

Le NTC 2018 §3.2.3.2 limitano l’impiego di questi spettri a periodi fondamentali non superiori a 4 s. Il modulo ammette 0 < Tis ≤ 4 s in tutti gli stati limite e limita il grafico a 4 s. Se anche un solo periodo supera il limite, il calcolo viene rifiutato: servono apposite analisi oppure storie temporali del moto del terreno, non un’estrapolazione della formula.

Sono calcolati domanda spettrale e risposta del sistema equivalente. Non sono verificate distribuzione spaziale e torsione, eccentricità accidentali, irregolarità, capacità e stabilità dei dispositivi, risposta verticale, spostamenti di progetto con relative amplificazioni o condizioni complessive di applicabilità dell’analisi. Il confronto numerico con un foglio di riferimento verifica le quantità confrontate, non l’idoneità del modello per una struttura reale.

