## Instabilità delle aste compresse

Un'asta compressa può perdere stabilità senza che tutta la sezione raggiunga la resistenza del materiale. Una piccola deviazione laterale produce un momento aggiuntivo dovuto alla forza assiale; il momento aumenta la deviazione, che a sua volta amplifica il momento. La rigidezza flessionale contrasta questa retroazione.

### Modello di Eulero

Si considera un'asta rettilinea prismatica, elastica, caricata al baricentro, con rigidezza costante e piccoli spostamenti all'innesco dell'instabilità. La forza di compressione $N$ è positiva in questa pagina; non è la convenzione della scheda Sezione in c.a. Le estremità ideali impediscono la traslazione trasversale ma permettono la rotazione; una consente lo scorrimento assiale.

![Figura 05.1 — Asta cerniera-carrello: carichi assiali opposti e primo modo sinusoidale. La deformata è amplificata e non rappresenta un'ampiezza calcolata.](../../X.Desktop/Assets/Wiki/euler-mode.svg)

L'equilibrio linearizzato nella configurazione deformata conduce al problema agli autovalori seguente. $x$ misura la distanza lungo l'asta e $v$ lo spostamento trasversale; gli apici indicano derivate rispetto a $x$.

```math
EI v''''+N v''=0
v(0)=v(L)=v''(0)=v''(L)=0
v_n(x)=a_n\sin\left(\frac{n\pi x}{L}\right)
N_n=\frac{n^2\pi^2 EI}{L^2}
```

Il primo autovalore positivo corrisponde a $n=1$. L'ampiezza $a_n$ resta indeterminata nell'analisi lineare: un grafico del modo non è una previsione dello spostamento reale. La forma generalizzata con lunghezza efficace è:

```math
N_{cr} = \frac{\pi^2 EI}{L_0^2}
```

E è il modulo elastico, I l'inerzia nel piano della deformata e L₀ la lunghezza efficace. Con E in N/mm², I in mm⁴ e L₀ in mm, Ncr è in N. La formula descrive una biforcazione elastica di un'asta ideale; non è direttamente la resistenza di progetto di un'asta reale imperfetta. Raddoppiare L₀ riduce il carico critico a un quarto.

### Lunghezza libera di inflessione

L₀ rappresenta il vincolo efficace nel modo di instabilità considerato, non semplicemente la lunghezza disegnata. Per un'asta ideale isolata incernierata a entrambe le estremità coincide con la lunghezza fra cerniere. Per telai e vincoli elastici dipende dal comportamento del sistema. Un ritegno è efficace solo se ha rigidezza, resistenza e percorso del carico adeguati.

| Simbolo | Significato | Unità nell'esempio |
| --- | --- | --- |
| $E$ | Modulo elastico longitudinale | N/mm² |
| $I$ | Inerzia nel piano del modo | mm⁴ |
| $A$ | Area della sezione | mm² |
| $L$ | Distanza fra i vincoli | mm |
| $K$ | Fattore di lunghezza efficace | adimensionale |
| $L_0=KL$ | Lunghezza efficace | mm |
| $N_{cr}$ | Carico di biforcazione | N |
| $i=\sqrt{I/A}$ | Raggio d'inerzia | mm |
| $\lambda=L_0/i$ | Snellezza geometrica | adimensionale |

Per aste isolate con vincoli ideali, $K=1$ per cerniera-carrello, $K=2$ per incastro-estremo libero e $K=0{,}5$ per due incastri senza traslazione relativa. Questi valori non si trasferiscono automaticamente a una colonna di telaio: la rotazione dei nodi e la traslazione di piano modificano il modo.

### Snellezza e tensione critica

```math
\sigma_{cr}=\frac{N_{cr}}{A}=\frac{\pi^2E}{\lambda^2}
```

![Figura 05.2 — Tensione critica ideale per E = 210000 MPa. La linea orizzontale indica fy = 355 MPa; il ramo sopra tale valore non è una resistenza elastica utilizzabile.](../../X.Desktop/Assets/Wiki/euler-curve.svg)

La tensione critica cala con il quadrato della snellezza. Il confronto con lo snervamento è un controllo di campo, non una curva normativa di resistenza: tensioni residue e imperfezioni riducono la capacità prima della previsione ideale.

### Esempio numerico verificabile

Si assegnano $E=210000\,\mathrm{N/mm^2}$, $I=8\cdot10^6\,\mathrm{mm^4}$, $A=4000\,\mathrm{mm^2}$, $L=4000\,\mathrm{mm}$ e $K=1$. L'inerzia è un dato didattico riferito al piano studiato, non una sezione commerciale selezionata.

```math
N_{cr}=\frac{\pi^2\cdot210000\cdot8\cdot10^6}{4000^2}
N_{cr}=1036308\,\mathrm{N}=1036{,}31\,\mathrm{kN}
i=44{,}72\,\mathrm{mm},\quad\lambda=89{,}44
\sigma_{cr}=259{,}08\,\mathrm{MPa}
```

Con $f_y=355\,\mathrm{MPa}$ la tensione critica resta sotto lo snervamento, condizione necessaria ma non sufficiente per applicare il modello ideale a un'asta reale. Se il medesimo elemento è una mensola con $K=2$, il carico critico scende a $259{,}08\,\mathrm{kN}$. Confondere i vincoli produce qui un errore di fattore quattro.

### Dalla teoria alla verifica

Disegna i ritegni nei due piani, scegli le rispettive inerzie e determina il modo governante. Solo dopo valuta la procedura normativa applicabile: imperfezioni, analisi del secondo ordine, classificazione della sezione e instabilità locale possono richiedere controlli distinti. L'instabilità flesso-torsionale, spesso indicata LTB, riguarda accoppiamento di spostamento laterale e torsione di un elemento inflesso: non si verifica sostituendo semplicemente un'inerzia nella formula di Euler.

La derivazione è un modello teorico, verificato sulle lezioni MIT *Buckling of Beams*, corso 16.001, 2021. Le verifiche dell'acciaio ricadono nel quadro NTC ed Eurocodice 3 adottato dal progetto; qui non sono assegnati coefficienti parziali o curve di buckling normative. Bibliografia per approfondire: Timoshenko e Gere, *Theory of Elastic Stability*, stabilità delle aste.

### Errori comuni

> ATTENZIONE
> Il carico critico elastico non include automaticamente imperfezioni, tensioni residue, plasticità e instabilità locale. La verifica normativa richiede il metodo applicabile al materiale e al sistema. La presente versione di Anthea non contiene un modulo autonomo Instabilità dell'acciaio.

### Riepilogo

Controlla il piano debole, i ritegni reali e il modo di instabilità. L'area da sola non descrive la vulnerabilità: due aste con la stessa area possono avere inerzie e capacità di stabilità molto differenti. Cerca anche i termini buckling, snellezza ed Euler nella Wiki per trovare i riferimenti presenti nelle guide globali.

