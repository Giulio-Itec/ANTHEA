## Sezione in calcestruzzo armato

### Geometria e deformazioni piane

Sono disponibili sezioni rettangolari, circolari e a T, con fori centrali per le forme supportate. Il contorno circolare è discretizzato mediante un poligono con numero di lati configurabile. Le aree dei vuoti sono escluse e le barre incompatibili con il contorno o interne ai vuoti vengono rifiutate.

L'ipotesi cinematica è una distribuzione piana delle deformazioni. Indicando con ε0 la deformazione al riferimento e con kx e ky i gradienti, la deformazione in un punto è una funzione affine delle coordinate. Le convenzioni dei gradienti e dei momenti sono trasformate nel sistema di riferimento esposto dal foglio.

```math
\varepsilon(x,y) = \varepsilon_0+k_x x+k_y y
N = \int_{A_c}\sigma_c\,dA+\sum A_s\sigma_s+\sum A_p\sigma_p
```

I momenti derivano dagli integrali delle tensioni moltiplicate per i rispettivi bracci, nel verso degli assi del solver. Le barre e i tendini sono contributi discreti; il calcestruzzo è integrato sulla regione resistente. Il piano di deformazione richiesto dalle azioni e il piano al limite resistente sono risultati differenti e vengono esposti separatamente.

### Equilibrio e domini

Per un dato stato N Mx My, il motore cerca le deformazioni che producono le risultanti assegnate, entro i limiti dei materiali. I domini resistenti sono ricavati esplorando stati limite delle deformazioni e integrando le corrispondenti tensioni. Il campionamento della superficie usato per la visualizzazione non sostituisce la procedura diretta di verifica richiamata dalle librerie.

Il criterio di crescita delle azioni determina la direzione della ricerca sul dominio. Con N costante si aumenta la componente flettente lungo la direzione scelta; con eccentricità costante si percorre una direzione differente nello spazio delle azioni. Un coefficiente di sfruttamento non è quindi interpretabile senza il criterio associato.

I quattro momenti resistenti rapidi, a N assegnato, corrispondono alle direzioni positive e negative dei due assi. Sono quattro interrogazioni del motore, non un rettangolo resistente dentro cui qualunque coppia di momenti sia ammessa. La proiezione 2D di un punto fuori piano può apparire interna pur non risolvendo il problema tridimensionale.

### Materiali tendini e analisi di esercizio

La risposta dipende dai diagrammi selezionati per cls, barre e tendini. Nelle leggi senza resistenza a trazione, il calcestruzzo teso non contribuisce all'equilibrio. Nelle analisi elastiche si distinguono le proprietà del modello tensionale da quelle impiegate successivamente nelle formule di fessurazione.

Per un gruppo di n fili o trefoli con area assegnata, l'eventuale diametro equivalente rappresenta la somma delle aree; a parità di diametro elementare cresce con √n. La tensione iniziale del tendine è un input efficace. Il motore non ricostruisce in modo generale attrito, rientro degli ancoraggi, rilassamento e tutte le perdite differite della struttura.

Le verifiche di tensione di esercizio utilizzano le combinazioni già assegnate e i limiti disponibili nel modulo. La scelta Rara, Frequente o Quasi permanente non genera le combinazioni. In alcuni percorsi la verifica di decompressione o di formazione delle fessure usa uno stato ausiliario interamente reagente: tale stato non va confuso con quello fessurato usato per le tensioni delle barre.

Nell'analisi elastica lineare con viscosità il solutore integra il calcestruzzo con Ec e l'acciaio con Es(1 + ψ). Le tensioni dipendono solo da n = Es(1 + ψ)/Ec, ma il piano che risolve l'equilibrio è quello di una sezione 1 + ψ volte più rigida di quella reale. Deformazioni e curvature mostrate, e quelle integrate per gli spostamenti dei muri, appartengono al piano totale:

```math
\varepsilon_{tot}(x,y) = (1+\psi)\,\varepsilon_{sol}(x,y)
\chi_{tot} = (1+\psi)\,\chi_{sol} = \frac{M}{E_{c,eff}\,I_{cr}(n)}
E_{c,eff} = \frac{E_c}{1+\psi}
```

Sul piano totale una barra a tensione σs ha deformazione σs/Es e il calcestruzzo compresso σc(1 + ψ)/Ec. Tensioni e asse neutro non cambiano. Nelle tre combinazioni SLE lo stesso ψ vale anche per la parte variabile del carico: la deformazione è sovrastimata, a favore di sicurezza. Con ψ = 0 e nell'analisi non lineare i due piani coincidono.

### Selezione delle verifiche nel report short

Il report short è una rappresentazione dei risultati correnti, senza nuovi calcoli resistenti. Per ogni famiglia sceglie il massimo tasso di lavoro numerico finito non arrotondato, mantenendo concomitanti azioni e risultati della combinazione selezionata. La selezione è distinta per SLU N–Mx–My, SLV N–Mx–My, taglio, tensioni rara, fessurazione frequente, tensioni quasi permanente e fessurazione quasi permanente.

Per N–Mx–My usa il tasso e il punto resistente del dominio 3D con il criterio salvato. Per il taglio usa il massimo dei rapporti nelle direzioni x e y e riporta entrambe le direzioni della medesima combinazione. Il tasso tensionale raro considera il massimo fra calcestruzzo compresso e acciaio in valore assoluto, confrontati con i rispettivi limiti; quello quasi permanente riguarda il calcestruzzo. Per la fessurazione usa wk/wlim e conserva distanza fra fessure, differenza di deformazioni e aree efficaci quando disponibili nella traccia. Il calcolo come limite superiore senza barre efficaci è esplicitamente riconoscibile.

Il numero di combinazioni con tasso disponibile viene confrontato con le azioni della famiglia. Un risultato mancante non viene trasformato in zero né escluso dalla valutazione di completezza: il massimo disponibile può essere mostrato, ma la riga rimane incompleta. Criteri non esprimibili con un rapporto, comprese le verifiche di decompressione, non vengono ordinati artificiosamente. Una famiglia priva di azioni è distinta da una verifica incompleta. Gli arrotondamenti riguardano soltanto la stampa.

La sintesi copre esclusivamente le sette famiglie riportate. Non costituisce attestazione di conformità per torsione, dettagli costruttivi, ancoraggi o altre verifiche del modulo. Il limite di due pagine viene controllato sull'impaginazione effettiva del documento Word prima del salvataggio dei due formati; non si riduce il contenuto tecnico per superare tale controllo.

### Apertura delle fessure

La formula implementata parte da tensione delle barre efficaci, modulo dell'acciaio, Ecm, resistenza media a trazione e rapporto fra armatura e area efficace di calcestruzzo. αe usa Es/Ecm e può differire dal coefficiente di omogeneizzazione con viscosità impiegato per l'analisi tensionale. Nel codice attuale fct,eff coincide con fctm, senza riduzione automatica per l'età di fessurazione.

```math
\rho_{eff} = \frac{A_{s,eff}}{A_{c,eff}}
\alpha_e = \frac{E_s}{E_{cm}}
\Delta\sigma = \frac{k_t f_{ctm}(1+\alpha_e\rho_{eff})}{\rho_{eff}}
\Delta\varepsilon = \max\left[\frac{\sigma_s-\Delta\sigma}{E_s};\frac{0{,}60\sigma_s}{E_s}\right]
```

kt vale 0,60 per breve durata e 0,40 per lunga durata. k1 vale 0,80 per barre ad aderenza migliorata e 1,60 per barre lisce. k2 descrive la distribuzione delle deformazioni (Circolare 2019 C4.1.2.2.4.5; EN 1992-1-1 7.3.4(3)). Se la sezione è interamente compressa (εc,max ≤ 10⁻¹² ai vertici) wk = 0 e k2 non interviene. Se l'asse neutro taglia la sezione, anche in presso- o tensoflessione, la sezione è inflessa e k2 = 0,50 per ogni normativa; la traccia lo riporta in un solo dettaglio «Criterio k₂». Nel ramo di sezione interamente tesa si impiega invece la distribuzione di deformazioni: k2 = (εmax + εmin)/(2εmax), limitato fra 0,5 e 1. k2 entra in wk solo con il termine k1 k2 k4 φeq/ρeff di sr,max: non entra con l'asse neutro nel copriferro senza barre tese (wk = 0), con le barre tese tutte fuori da Ac,eff (limite superiore), con barre distanziate quando governa il termine in (h − x), né con Model Code 2010 e DIN EN 1992-1-1, il cui sr,max non contiene k2. Le fasce interne dei fori sono aree locali (EN 1992-1-1 7.3.4(3)): ciascuna usa k2 = (εmax + εmin)/(2εmax) sulla propria parte tesa, quindi 0,50 se l'asse neutro taglia la fascia e più di 0,50 se la fascia è tutta tesa, come la soletta inferiore di un cassone inflesso; la traccia lo riporta come «k₂ della fascia». L'altezza tesa h − x della fascia, usata nel termine in (h − x) delle barre distanziate, è εmax/|∇ε| misurata dall'asse neutro, ma non supera l'altezza della sezione lungo il gradiente: con l'asse neutro interno alla sezione il limite non agisce; nella sezione interamente tesa (x = 0) agisce dove εmax/|∇ε| supera quell'altezza, cioè per le fasce lontane dall'asse neutro e per tutte le fasce vicino alla trazione uniforme, mentre una fascia vicina alla faccia meno tesa conserva εmax/|∇ε|. Se il gradiente è trascurabile (|∇ε| per l'altezza della sezione lungo il gradiente non oltre 10⁻⁴ εmax) la trazione è uniforme: tutta la fascia è tesa, k2 = 1 e h − x è l'altezza della sezione normale alla faccia della fascia (il diametro per l'anello). La traccia riporta h − x solo dove agisce uno di questi limiti. Quando l'asse neutro entra nella sezione h − x e wk delle fasce variano con continuità, anche in flessione deviata. Alla soglia della trazione uniforme, invece, h − x passa dall'altezza normale alla faccia a quella lungo il gradiente, che differiscono nei cassoni non quadrati e con il gradiente obliquo: in un cassone 1000 × 1400 mm con il gradiente lungo il lato di 1400 mm le fasce sui lati lunghi passano da 1000 a 1400 mm e la loro apertura cresce del 40 %, per un'eccentricità trascurabile; nell'anello la differenza è solo quella del poligono che approssima il cerchio.

Fino al 7/10/2026 nel ramo NTC 2018 k2 si sceglieva dalle tensioni delle barre: 0,50 con almeno una barra ordinaria compressa, altrimenti 1,00 anche con l'asse neutro interno alla sezione. Le sezioni a semplice armatura inflesse, le solette con la rete superiore tesa e la tensoflessione con asse neutro interno avevano quindi k2 = 1,00 e un'apertura maggiore. Sui casi di prova la regola attuale riduce l'apertura in media del 24 % (fino al 44 %) nelle sezioni a semplice armatura e del 36 % (fino al 48 %) in tensoflessione; non cambia nulla dove governa la regione distante dalle barre, e alcuni esiti passano da non soddisfatto a soddisfatto. Le altre normative usavano già 0,50.

Il codice conserva una rappresentazione tramite distanza media Δsm e fattore finale 1,70. Per le barre ravvicinate questo conduce allo stesso prodotto scritto direttamente con il termine 3,4c + 0,425 k1 k2 φeq/ρeff. Per le barre distanziate viene confrontata anche la regione distante dalle armature.

```math
\Delta s_{m,vicino} = \frac{3{,}4c+0{,}425 k_1 k_2\varphi_{eq}/\rho_{eff}}{1{,}70}
s_{lim} = 5(c+\varphi_{eq}/2)
\Delta s_{m,distante} = 0{,}75(h-x)
w_k = \max(0;1{,}70\Delta s_m\Delta\varepsilon)
```

Se s ≤ slim si usa Δsm,vicino; altrimenti si usa il massimo fra le due distanze. Questo dettaglio è rilevante: sostituire il ramo distante con un coefficiente arrotondato ricordato da un'altra formulazione non riproduce esattamente il codice. σs è il massimo delle barre efficaci, non la media delle tensioni di tutte le barre.

L'area efficace viene costruita sulla zona tesa e sulle barre pertinenti. Nella trazione integrale sono esaminate regioni di bordo o radiali e governa il risultato massimo; non si sommano le aree sovrapposte come se fossero indipendenti. Disposizioni non supportate restano fuori campo; per le superfici interne compatibili valgono le fasce di parete o anello descritte più avanti. La disponibilità della formula va verificata anche rispetto al metodo tensionale e alla presenza di tendini.

### Esempio di fessurazione

Assumere σs = 200 MPa, Es = 200000 MPa, Ecm = 33000 MPa, fctm = 2,9 MPa, ρeff = 0,02, φeq = 16 mm, c = 30 mm, k1 = 0,8, k2 = 0,5 e lunga durata. Si ottiene αe = 6,0606 e Δσ = 65,03 MPa. La deformazione calcolata è 0,0006748, maggiore del minimo 0,0006000.

Il termine vicino vale (102 + 136)/1,7 = 140 mm. La soglia di interasse è 190 mm. Con s = 150 mm, wk = 1,7 × 140 × 0,0006748 = 0,1606 mm. Se s supera 190 mm e h−x = 300 mm, il termine distante vale 225 mm e wk aumenta a 0,2581 mm. Cambiano quindi apertura e ramo governante, pur conservando σs e area efficace nell'esempio.

### Taglio

Senza armatura trasversale, e nel campo ammesso, il motore confronta il termine proporzionale alla radice cubica di 100ρfck con il minimo basato su √fck. k è limitato a 2, ρ a 0,02 e la compressione media a 0,2fcd. La compressione media è positiva benché N nel foglio sia negativo a compressione.

```math
k = \min\left[2;1+\sqrt{\frac{200}{d}}\right]
\rho = \min\left[0{,}02;\frac{A_{sl}}{b_w d}\right]
\sigma_{cp} = \min\left[-\frac{1000N}{A_c};0{,}2f_{cd}\right]
V_{Rd} = \max\left[\frac{0{,}18k\sqrt[3]{100\rho f_{ck}}}{\gamma_c}+0{,}15\sigma_{cp}; 0{,}035 k^{1{,}5}\sqrt{f_{ck}}+0{,}15\sigma_{cp}\right]\frac{b_w d}{1000}
```

Per N di trazione il ramo senza staffe non fornisce automaticamente una resistenza favorevole. Con staffe il modello a traliccio confronta resistenza dell'armatura e del puntone; cot θ è compreso fra 1 e 2,5 e può essere assegnato o determinato dalla procedura automatica.

```math
V_{Rsd} = \frac{z(A_{sw}/s)f_{yd}(\cot\alpha+\cot\theta)\sin\alpha}{1000}
V_{Rcd} = \frac{z b_w\alpha_c\,0{,}5f_{cd}(\cot\alpha+\cot\theta)}{(1+\cot^2\theta)1000}
V_{Rd} = \min(V_{Rsd};V_{Rcd})
```

αc dipende dalla compressione: 1 + σcp/fcd fino a 0,25fcd, 1,25 fino a 0,50fcd e max[0; 2,5(1−σcp/fcd)] oltre tale soglia. Il braccio z deriva dal fattore della geometria. Per il ramo circolare dei pali si adottano 0,75d nella sezione piena e 0,60d nella cava, con bw e d ricavati dalla geometria e dalle barre. Si tratta del ramo specifico documentato, non di un'estensione indistinta a qualunque elemento circolare.

### Torsione e interazione

La sezione resistente a torsione è un circuito periferico chiuso con area Ak, perimetro uk e spessore efficace t. La geometria automatica è disponibile per rettangolo e cerchio, pieni o con foro compatibile. Sono richieste staffe chiuse e armatura periferica adeguata; lo spessore deve contenerne gli assi.

```math
T_{Rcd} = \frac{2A_k t\,0{,}5 f_{cd}\cot\theta}{(1+\cot^2\theta)10^6}
T_{Rsd} = \frac{2A_k(A_{sw}/s)f_{yd}\cot\theta}{10^6}
T_{Rld} = \frac{2A_k(A_{sl,disp}/u_k)f_{yd}}{\cot\theta\,10^6}
T_{Rd} = \min(T_{Rcd};T_{Rsd};T_{Rld})
```

Asl,disp è l'armatura longitudinale disponibile per la torsione dopo la flessione, da assegnare consapevolmente. Taglio e torsione devono usare lo stesso cot θ. Il programma controlla anche la somma |T|/TRcd + |Vx|/VRcd,x + |Vy|/VRcd,y per il calcestruzzo e |T|/TRsd + max(|Vx|/VRsd,x; |Vy|/VRsd,y) per le staffe. L'estensione a due componenti di taglio è una combinazione conservativa del modello, non un dominio normativo generale ricostruito in ogni dettaglio.

### Dettagli e curva momento curvatura

Le verifiche costruttive dipendono dal tipo di elemento scelto. Il modulo confronta geometria, armature e parametri necessari con i limiti implementati e mantiene «Da completare» quando mancano informazioni. La lunghezza di ancoraggio rettilineo parte da lbrqd = φσsd/(4fbd), con i coefficienti di forma e condizioni α1–α5 assunti unitari nel ramo documentato. Le sovrapposizioni introducono α6 e i relativi minimi; i dettagli speciali non vengono ricavati da un disegno ideale della sezione.

Lo sforzo NEd dei minimi di armatura dei pilastri (NTC 2018 §4.1.6.1.2) è la compressione più gravosa fra tutte le combinazioni del foglio: SLU, SLV, taglio, rara, frequente e quasi permanente. A parità di valore prevale una combinazione di progetto. Con combinazioni ultime complete le SLE di norma non governano; se governano, il risultato indica che NEd è un valore di esercizio.

```math
N_{Ed} = \max_r\ \max(0;\ -N_r)
```

La curva M χ viene costruita a N fissato nella direzione assegnata, con passi uniformi o quadratici fino al limite resistente. Ogni punto richiede equilibrio di sezione. In una sezione asimmetrica la direzione del gradiente di deformazione può non coincidere con quella del momento. Non sono inclusi automaticamente softening strutturale, lunghezza della cerniera, rotazione globale o interazione con instabilità dell'elemento.

### Asse neutro e stato resistente

L'asse neutro nel c.a. è il luogo di deformazione nulla del piano affine. Con deformazione uniforme non esiste una retta univoca. L'asse del punto resistente sul dominio e quello della combinazione di esercizio appartengono a stati diversi. Nei tendini la predeformazione può separare lo zero della deformazione geometrica da quello della tensione.

Le superfici interne dei fori sono trattate con fasce di parete o anello, limitate a metà spessore; le facce sono controllate separatamente. Ogni fascia usa il k2 della propria distribuzione di deformazioni, non quello della sezione (vedi l'apertura delle fessure). Una superficie compressa non richiede apertura delle fessure. Una superficie tesa senza armatura efficace o senza interasse assegnato lascia il controllo incompleto; un superamento già accertato resta sfavorevole.

I coefficienti e i campi dei diversi profili sono distinti nella [matrice del calcestruzzo](wiki:profili-calcestruzzo). La [guida operativa](wiki:guida-sezione-ca) descrive importazioni, stati del calcolo e lettura dei risultati.

