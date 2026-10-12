## Taglio irrigidimenti e connessione della sezione composta

### Resistenza a taglio

Questi controlli sono attivi nel percorso cumulativo e adottano le edizioni identificate dall'interfaccia: NTC 2018 e norme EN richiamate nella documentazione del modulo. L'anima intera partecipa al taglio, senza includere un contributo resistente della soletta. L'area plastica assunta cautelativamente è hw tw. Il contributo favorevole delle flange alla resistenza per instabilità a taglio viene omesso.

```math
\tau_{cr} = \frac{k_{\tau}\pi^2 E}{12(1-\nu^2)}\left(\frac{t_w}{h_w}\right)^2
\lambda_w = \sqrt{\frac{f_y}{\sqrt{3}\tau_{cr}}}
V_{pl,Rd} = \frac{h_w t_w f_y}{\sqrt{3}\gamma_{M0}}
V_{bw,Rd} = \frac{\chi_w h_w t_w f_y}{\sqrt{3}\gamma_{M1}}
V_{Rd} = \min(V_{pl,Rd};V_{bw,Rd})
```

kτ dipende dal rapporto del pannello e dalla validità degli irrigidimenti; χw segue la curva del montante terminale applicabile. Senza intermedi idonei si adotta il pannello lungo. Il montante rigido richiede la verifica positiva del dettaglio previsto, non la sola selezione del nome. I coefficienti iniziali documentati sono γM1 = 1,10, γV = 1,25 ed η = 1,20; γM0 è 1,05 per NTC e 1,00 per il percorso EC. L'utente può modificarli e deve controllare l'Appendice Nazionale pertinente.

Le formule precedenti descrivono una singola anima verticale. Per H inclinata e cassoncino la resistenza e l'instabilità della singola lamiera si calcolano sostituendo alla sua altezza la lunghezza reale ℓw, mantenendo lo spessore normale tw. Il taglio immesso nelle fasi è invece la componente verticale totale V. Con ripartizione uguale fra le nw anime, la domanda nel piano di una lamiera e la resistenza verticale complessiva sono:

```math
V_{lamiera} = \frac{V}{n_w\cos\alpha}
V_{Rd,verticale} = n_w\cos\alpha\,V_{Rd,lamiera}
\tau_{media} = \frac{V}{n_w h_w t_w}
```

L'ultima formula richiede V in N e dimensioni in mm. Deriva da V_lamiera/(tw ℓw); il coseno si semplifica perché ℓw = hw/cos α. La tensione media non coincide in generale con il massimo della distribuzione V S/(I t). Nei due esempi, con V = 600 kN, la domanda per lamiera è 608,276253 kN per l'H e 302,879697 kN per il cassoncino; le tensioni medie sono 23,809524 e 11,904762 MPa. La snellezza e τcr continuano a dipendere da ℓw: l'inclinazione non può essere cancellata nella verifica di instabilità.

### Interazione con il momento e tensioni tangenziali

Oltre 0,5 VRd il taglio può ridurre il margine flessionale. Per il campo N = 0, fy ≤ 355 MPa e anima non interamente compressa, si calcolano capacità plastiche di riferimento integrando flange efficaci, anima intera e soletta compressa. La capacità Mf omette l'anima. Il cls teso è nullo e le barre sono omesse cautelativamente in queste capacità di riferimento. Le verifiche elastiche della sezione non vengono sostituite da tali integrazioni plastiche.

Negli altri casi ad alto taglio viene usato un inviluppo elastico cautelativo con Mf = 0, segnalato esplicitamente. Non è un dominio plastico esatto N M V per qualunque sezione. Le tensioni tangenziali includono la media già definita e il massimo del campo elastico, trasformato nel piano dell'anima reale, con somma algebrica dei contributi di fase.

L'inviluppo √(max|σ|² + 3 max|τ|²) è un controllo aggiuntivo conservativo; i due massimi possono trovarsi in punti diversi. Non sostituisce instabilità del pannello, verifiche degli appoggi o fatica.

### Irrigidimenti appoggi e saldature

Il modello ammette piatti mono o bilaterali anche differenti, pannelli adiacenti diversi, appoggi interni o terminali, eccentricità della reazione, montante rigido a due coppie e saldature continue. Le verifiche di pressoflessione elastica includono un'amplificazione del secondo ordine e imperfezioni equivalenti nel campo del metodo implementato. La lunghezza critica parte dal rapporto Lcr/L assegnato, inizialmente 1,00.

Un irrigidimento insufficiente non aumenta la resistenza del pannello a taglio. Le azioni di traversi, intagli, concentrazioni locali o dettagli non inseriti non vengono ricavate dalla sola sezione trasversale. La capacità di un piatto non dimostra da sola che anima, flangia e saldature trasferiscano l'intera reazione.

Con anima inclinata le dimensioni di controllo di irrigidimenti e saldature seguono la lamiera reale, non l'altezza dell'anima equivalente. Per il cassoncino l'implementazione ripartisce la reazione d'appoggio assegnata fra le due anime e usa dettagli per anima; questa ipotesi non sostituisce l'analisi di un appoggio eccentrico che solleciti in modo diverso le due pareti. Con le verifiche a torsione attive il torcente trasferito agli apparecchi aggiunge la coppia T/e_b alla metà della reazione sull'anima più caricata; altre distribuzioni trasversali devono essere valutate separatamente.

### Resistenza e domanda dei pioli

Il ramo disponibile considera pioli a testa saldata, distribuzione uniforme, soletta piena ordinaria C20/25–C60/75, diametro 16–25 mm e altezza almeno 3d. La resistenza è il minore dei due meccanismi, acciaio e calcestruzzo, con fu limitato a 500 MPa. Il fattore α dipende da h/d nel campo previsto.

```math
P_{Rd} = \frac{\min\left[0{,}8f_u\pi d^2/4;0{,}29\alpha d^2\sqrt{f_{ck}E_{cm}}\right]}{\gamma_V}
q = \sum_i\left(\frac{V_i S_i}{I_i}+\Delta q_i\right)
P_{Ed} = \frac{|q|\,passo}{n_{pioli}}
```

La prima formula dà una forza in N con dimensioni in mm e tensioni in MPa; la domanda deve essere convertita coerentemente. q è il flusso longitudinale, passo la distanza fra file e npioli il numero per fila. Le fasi prima della collaborazione del cls non caricano la connessione nel modello. Δq consente di introdurre contributi longitudinali ottenuti da altra analisi.

V S/I presuppone proprietà costanti nel tratto e N costante lungo l'asse. Un'introduzione locale di N o una variazione di sezione non è descritta dal solo valore del taglio. Il ritiro uniforme della sezione non produce da solo V, mentre le sue zone di trasferimento possono richiedere un Δq specifico.

Nel percorso NTC si usano le proprietà della fase tensionale, inclusa l'eventuale esclusione della soletta. Nel percorso EC4 lo scorrimento considera la soletta non fessurata e la carpenteria efficace, con il φ o n pertinente. Si tratta di una distinzione di proprietà della fase, non di un passaggio nascosto ad acciaio completamente lordo.

Nel cassoncino q è il flusso totale della sezione e la ripartizione simmetrica assegna q/2 a ciascuna piattabanda superiore. Il numero npioli immesso è quello per fila di una singola piattabanda. Indicando con nf il numero di piattabande superiori, la formula generale adottata è:

```math
P_{Ed} = \frac{|q|\,passo}{n_f n_{pioli}}
```

nf vale uno per l'H e due per il cassoncino. Per esempio q = 100 N/mm, passo = 200 mm e due pioli per fila e per piattabanda producono 5000 N, cioè 5 kN per piolo, nel cassoncino; una sola piattabanda con due pioli riceverebbe 10 kN per piolo. Il controllo del bordo usa la larghezza della singola piattabanda e i controlli della soletta trasversale usano il flusso a essa attribuito. I flussi minimo e massimo assegnati alla fatica dei pioli si riferiscono già alla piattabanda: non sono automaticamente interpretati come flussi totali da dimezzare.

Con la torsione del cassoncino il flusso torsionale della soletta qT, somma delle fasi composte, si trasferisce attraverso i pioli di ciascuna piattabanda con verso opposto sulle due anime. Sulla piattabanda in cui si somma al flusso di flessione la domanda diventa:

```math
P_{Ed} = \frac{(|q|/n_f+|q_T|)\,passo}{n_{pioli}}
```

Con q = 100 N/mm, qT = 50 N/mm, passo 200 mm e due pioli per fila si ottengono 10 kN per piolo. Il flusso qT si aggiunge anche alla superficie a–a interna alla cella di ciascuna piattabanda e alle superfici b–b.

### Servizio dettagli e fatica

Il limite 0,75 PRd viene controllato nella situazione caratteristica di esercizio prevista; non viene trasferito automaticamente a una combinazione quasi permanente. I dettagli comprendono passi longitudinali e trasversali, bordi, testa, copriferro, posizione rispetto alle barre, rapporto fra diametro e spessore della flangia e armature trasversali.

I passi minimi implementati sono 5d longitudinale e 2,5d trasversale, con massimo longitudinale min(800 mm; 4hc). Le condizioni di azioni ripetute e fatica attivano il limite pertinente d ≤ 1,5tf; per il campo statico disponibile è adottato cautelativamente 2,5tf. Testa e distanze non sono dettagli ornamentali: possono governare la validità del collegamento anche con PEd basso.

Sono presenti controlli dell'armatura trasversale, superfici di scorrimento, ancoraggio e fatica resistente dei pioli con interazione della flangia tesa, quando i dati necessari sono assegnati. Restano fuori campo, fra gli altri, sollevamento, splitting attraverso lo spessore, gruppi non uniformi, mensole locali e lamiere grecate. Una verifica non alimentata con le escursioni e i dati di fatica non ricava autonomamente lo spettro di traffico.

### Torsione del cassoncino

Con le verifiche a torsione attive il cassoncino è una cella singola chiusa. Il momento torcente ΔT di ciascuna fase produce il flusso di St. Venant della formula di Bredt, costante lungo il perimetro e, in una cella singola, indipendente dagli spessori delle pareti. A0 è l'area racchiusa dalle linee medie. Nelle fasi composte la parete superiore è la soletta al suo piano medio, con le anime prolungate fino a esso; nelle fasi di solo acciaio è il controvento orizzontale di spessore equivalente t* al piano medio delle piattabande superiori. Con t* = 0 la cella è aperta e il flusso della fase non viene calcolato.

```math
q = \frac{T}{2A_0}
A_0 = \frac{(b_{sup}+b_{inf})h_0}{2}
J = \frac{4A_0^2}{\sum_i(\ell_i/t_i)}
n_G = \frac{n(1+\nu_c)}{1+\nu_a}
```

b_sup e b_inf sono le distanze fra gli assi delle anime prolungate ai piani delle pareti superiore e inferiore, h0 la distanza verticale fra i due piani. Nella rigidezza J la soletta ha spessore hc/nG, con νc = 0,2, νa = 0,3 e il coefficiente n della fase comprensivo della viscosità (EN 1994-2 §5.4.2.2(11)); con soletta esclusa lo spessore è dimezzato, come per la soletta fessurata della EN 1994-2 §5.4.2.3(6). J serve al modello globale e non modifica q.

I flussi delle fasi si sommano con il proprio segno. Anime e fondo ricevono il flusso di tutte le fasi, soletta e connessione quello delle sole fasi composte, il controvento quello delle fasi di solo acciaio. La tensione tangenziale di torsione in una parete di spessore t è q/t. Nell'anima più caricata il taglio di flessione e quello di torsione si sommano (EN 1993-1-1 §6.2.7(9)); il risultato è riportato alla componente verticale totale per il confronto con la resistenza e per l'interazione con il momento.

```math
\tau_T = \frac{q}{t}
V_{lamiera} = \frac{V}{n_w\cos\alpha}+q\ell_w
V_{eq} = n_w\cos\alpha\,V_{lamiera}
```

Nell'inviluppo elastico dell'anima q/tw si aggiunge al massimo di V S/(I t). Nel fondo si controllano la tensione equivalente al nodo con l'anima, l'imbozzamento a taglio del pannello compreso fra due diaframmi con η = 1 e l'interazione della EN 1993-1-5 §7.1(5) con Mf,Rd nullo. Il taglio di flessione del fondo usa il momento statico della sua metà interna; nell'imbozzamento si adotta il τ medio del pannello, non inferiore a metà del massimo.

```math
\tau_b = \frac{q}{t_b}+\frac{V S_{fondo}}{I t_b}
\eta_1+(2\eta_3-1)^2\leq1
```

Nella soletta il flusso si somma alla superficie a–a interna alla cella di ciascuna piattabanda e alle superfici b–b attorno ai pioli, con lo stesso angolo θ dei puntoni. L'armatura longitudinale deve assorbire la trazione q cotθ per unità di larghezza (EN 1992-1-1 §6.3.2(3)), confrontata con la compressione disponibile del calcestruzzo e con la capacità residua delle barre oltre la flessione; senza armatura trasversale attiva si usa cotθ = 1,25.

```math
q\cot\theta\leq\max(0;-\sigma_{c,media})h_c+\sum_i\frac{A_{s,i}}{s_i}\left[f_{yd}-\max(0;\sigma_{s,i})\right]
```

All'appoggio il diaframma riceve il flusso perimetrale e lo porta agli apparecchi. La piastra del diaframma è in taglio puro, con A0 della cella di acciaio a favore di sicurezza; la reazione verticale degli apparecchi comprende la coppia del torcente, che si somma a R/2 sull'anima più caricata. Se gli apparecchi non sono allineati alle anime la flessione del diaframma non è verificata e viene segnalata.

```math
\tau_D = \frac{T}{2A_0 t_D}
\Delta R = \frac{T}{e_b}
```

L'esempio riprende il cassoncino del capitolo precedente (δ = 250 mm, s_top = 1800 mm, piattabande 450 × 25 mm, fondo 1400 × 25 mm) con la soletta 3000 × 250 mm e le fasi iniziali del foglio: G1 di solo acciaio con t* = 4 mm, G2 composta con φ = 2 e ψL = 1,1, Q composta a breve termine. Per la cella composta b_sup = 1841,667 mm, b_inf = 1296,528 mm e h0 = 1962,5 mm; per la cella di acciaio b_sup = 1803,472 mm e h0 = 1825 mm.

| Grandezza | G1 solo acciaio | G2 composta | Q composta |
| --- | --- | --- | --- |
| ΔT [kNm] | 200 | 300 | 1000 |
| A0 [m²] | 2,828750 | 3,079353 | 3,079353 |
| q [kN/m] | 35,3513 | 48,7115 | 162,3718 |
| J [m⁴] | 0,041788 | 0,080873 | 0,100659 |

Il flusso cumulato vale 246,4346 kN/m nelle anime e nel fondo, con τT = 17,6025 MPa nelle anime da 14 mm e 9,8574 MPa nel fondo da 25 mm; nella soletta agiscono 211,0833 kN/m. Con T = 1500 kNm all'appoggio, tD = 15 mm ed e_b = 1300 mm risultano τD = 17,6757 MPa e ΔR = 1153,846 kN. I valori sono confrontati con il calcolo del programma.

### Distorsione e diaframmi

La distorsione è la deformazione della sezione trasversale del cassone prodotta dalla parte dei carichi eccentrici che la cella non assorbe per torsione. Il modello segue l'analogia della trave su suolo elastico di Wright, Abdel-Samad e Robinson (1968). Il modo distorsivo è il meccanismo delle quattro pareti incernierate negli spigoli con scorrimento nullo in ogni parete: gli spostamenti tangenziali Vi delle pareti di lunghezza ℓi soddisfano la condizione di chiusura, per cui i flussi di St. Venant non compiono lavoro sul modo. L'ingobbamento ω è lineare su ogni parete con pendenza Vi ed è reso ortogonale agli ingobbamenti di sforzo normale e flessione; mensole della soletta, sbalzi del fondo e piattabande superiori partecipano agli integrali, con la soletta divisa per n0. Il modo è normalizzato con la media dei valori assoluti delle variazioni degli angoli agli spigoli, che nel rettangolo coincide con la distorsione γ.

```math
\sum_i\ell_i V_i = 0
\int\omega t\,ds = \int\omega x t\,ds = \int\omega y t\,ds = 0
I_{Dw} = \int\omega^2 t\,ds
```

La rigidezza a telaio K per unità di lunghezza deriva dal telaio trasversale a nodi rigidi deformato secondo il modo, con rigidezze flessionali D = E t³/[12(1 − ν²)] di anime, fondo e soletta. I diaframmi intermedi sono molle KD: per la piastra si usa l'energia dello stato piano di tensione con i bordi mossi dalle pareti, per il controvento a X l'energia delle due diagonali. Un torcente applicato come coppia verticale alla sommità delle anime produce il carico generalizzato p = T (V_dx − V_sx)/b_sup, dove V_dx e V_sx sono gli spostamenti verticali del modo agli spigoli superiori.

```math
E I_{Dw}\psi''''+K\psi = p
\sigma_{dw} = E\omega\psi''
m = m_1\psi
```

Per un rettangolo b × h di spessore costante t valgono le forme chiuse seguenti, usate come controllo del programma insieme a un modello a telaio indipendente e alle formule di Yoo et al. (SSRC 2015) per il trapezio. Per il controvento a X del trapezio la rigidezza coincide con quella della letteratura dopo il cambio di normalizzazione dell'angolo; per la piastra la formula semplificata a taglio uniforme è dal 5 al 10% più bassa del calcolo con i bordi mossi dalle pareti.

```math
I_{Dw} = \frac{t(b+h)b^2 h^2}{96}
K = \frac{24}{b/D_h+h/D_v}
K_D = G t b h
K_D = \frac{2E A b^2 h^2}{L^3}
```

La trave su suolo elastico è la campata semplicemente appoggiata con diaframmi d'estremità rigidi, discretizzata con elementi cubici e con le molle dei diaframmi intermedi. m_t agisce su tutta la luce e T_c nella posizione più sfavorevole, con lo stesso segno; si ricavano gli inviluppi di σdw agli spigoli e agli sbalzi, del momento trasversale agli spigoli e della distorsione ai diaframmi. Secondo la EN 1993-2 §6.2.7(3) σdw viene sommata alle verifiche del fondo quando supera il 10% della tensione di flessione. I nodi anima–fondo e anima–piattabanda superiore combinano sempre σx con σdw, la flessione trasversale σz = 6m/t² con il segno sfavorevole e τ.

```math
\sigma_{eq} = \sqrt{\sigma_x^2+\sigma_z^2+|\sigma_x\sigma_z|+3\tau^2}
```

I diaframmi a piastra sono verificati a taglio con imbozzamento (EN 1993-1-5 §5 con η = 1) e a tensione equivalente; le diagonali dei controventi a X in compressione con la curva c e Lcr = β L. Aperture, collegamenti, aste del controvento superiore e accoppiamento fra distorsione e ingobbamento torsionale restano fuori dal modello, come nell'analogia originale. Gli inviluppi della distorsione non dipendono dalle fasi e sono calcolati nelle situazioni con la soletta.

Nell'esempio del paragrafo precedente, con luce 40 m, diaframmi a piastra da 12 mm ogni 5 m, m_t = 60 kNm/m e T_c = 600 kNm, si ottengono i valori seguenti. σdw supera il 10% della flessione e viene sommata nelle verifiche del fondo.

| Grandezza | Valore |
| --- | --- |
| IDw | 0,010042 m⁶ |
| K | 607,168 kN·m/m |
| KD della piastra | 2872,26 MN·m |
| Carico generalizzato di un torcente unitario | 0,341377 |
| Diaframmi intermedi | 7 |
| ψ massimo | 2,762·10⁻⁴ rad |
| σdw al fondo | 14,946 MPa, pari al 20,2% della flessione |
| Momento trasversale ai nodi inferiori | 0,0434 kNm/m |
| τ nel diaframma più sollecitato | 8,394 MPa |

