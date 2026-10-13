## Reologia del calcestruzzo

Lo strumento considera viscosità lineare e maturazione del modulo secante del calcestruzzo ordinario secondo EN 1992-1-1:2004, §§3.1.2, 3.1.3(3), 3.1.4 e allegato B. L'umidità è costante e la temperatura di riferimento è 20 °C. L'età è misurata dal getto; si considera una sola età di applicazione del carico. Ritiro, storie termiche e variazioni del carico non fanno parte di questo calcolo.

### Coefficiente di viscosità e limite analitico

$$ \varphi(t,t_0)=\varphi_0\left(\frac{t-t_0}{\beta_H+t-t_0}\right)^{0.3}

L'espressione vale per t maggiore di t₀; prima del carico il coefficiente è zero. Il limite è analitico, indipendente dall'età finale scelta per il grafico:

$$ \varphi(\infty,t_0)=\varphi_0=\varphi_{RH}\,\beta(f_{cm})\,\beta(t_{0,adj})

I fattori di umidità, resistenza, dimensione nominale e sviluppo temporale sono quelli delle espressioni B.2–B.9. La resistenza media a 28 giorni è fcm = fck + 8 MPa. La dimensione nominale h₀ = 2 Ac / u usa area di calcestruzzo e perimetro esposto all'essiccamento, con unità coerenti. La correzione dell'età di carico per il cemento è:

$$ t_{0,adj}=\max\left(0.5,\ t_0\left[1+\frac{9}{2+t_0^{1.2}}\right]^{\alpha}\right)

L'esponente α vale −1, 0 e 1 rispettivamente per cemento S, N e R. L'età corretta entra nel fattore β(t₀); la durata t − t₀ resta cronologica. Il coefficiente φ è riferito al modulo tangente Ec = 1,05 Ecm,28. L'espressione non restituisce direttamente una deformazione: nel campo lineare, la deformazione viscosa dipende anche dalla tensione e dal modulo di riferimento.

### Maturazione del modulo elastico

Il modulo secante riusa la legge di maturazione del materiale:

$$ \beta_{cc}(t)=\exp\left[s\left(1-\sqrt{\frac{28}{t}}\right)\right],\qquad f_{cm}(t)=\beta_{cc}(t)f_{cm}

$$ E_{cm}(t)=\left(\frac{f_{cm}(t)}{f_{cm}}\right)^{0.3}E_{cm,28},\qquad k_E(t)=\frac{E_{cm}(t)}{E_{cm,28}}

Il coefficiente s vale 0,38, 0,25 e 0,20 rispettivamente per cemento S, N e R. Si usa il modulo nominale del materiale EN 1992 con aggregato di riferimento; la finestra non introduce una correzione per il tipo di aggregato. A 28 giorni kE = 1. Il rapporto può superare 1 alle età successive e non rappresenta un fattore di riduzione per viscosità. La resistenza e il modulo nel tempo non sono intercambiabili con la legge di deformazione differita.

### Esempio e campo di applicazione

Con fck = 25 MPa, cemento N, RH = 50 %, h₀ = 200 mm e t₀ = 7 giorni, un riferimento indipendente dà φ∞ = 3,4427042105 ed Ecm,28 = 31475,806210 MPa.

| Età t in giorni | φ(t, t₀) | Ecm(t) in MPa | Ecm(t) / Ecm,28 |
| --- | --- | --- | --- |
| 0,5 | 0 | 19355,383269 | 0,614929 |
| 7 | 0 | 29201,474188 | 0,927743 |
| 17 | 1,029049 | 30813,898916 | 0,978971 |
| 28 | 1,278108 | 31475,806210 | 1 |
| 365 | 2,603961 | 33229,779639 | 1,055724 |

Gli ingressi accettati sono fck fra 12 e 90 MPa, RH fra 40 e 100 %, h₀ positivo, t₀ almeno 0,5 giorni ed età di lettura positiva. Questi controlli non sostituiscono la verifica delle ipotesi fisiche: la viscosità non lineare per tensioni elevate, la maturazione a temperatura diversa e la variabilità ambientale richiedono metodi ulteriori. L'uso del modello lineare va verificato rispetto al livello tensionale al carico, con la soglia normativa di 0,45 fck(t₀). L'andamento alle primissime età richiede particolare cautela e dati pertinenti al calcestruzzo impiegato.

Le curve sono campionate solo per la rappresentazione; ogni lettura puntuale è valutata direttamente. La media, l'interpolazione grafica e la scala degli assi non alterano i valori calcolati. La [procedura operativa](wiki:guida-reologia-calcestruzzo) chiarisce come leggere e trasferire consapevolmente i risultati.

### Getti sottili nelle verifiche di sezione

Per l'opzione di getto in opera inferiore a 50 mm, le verifiche NTC e UNI EN 1992 con appendice italiana applicano la riduzione prevista del 20 % ai limiti pertinenti del calcestruzzo e al coefficiente αcc, quindi a fcd. Le altre norme conservano la propria regola. Questa scelta delle verifiche di sezione non modifica le curve di reologia dello strumento, che dipendono dagli ingressi esplicitamente indicati.

