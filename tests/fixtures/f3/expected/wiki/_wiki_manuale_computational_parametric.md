## Progettazione parametrica e controllo delle dipendenze

Un modello parametrico conserva la relazione fra dati e risultati. Se una luce cambia, devono aggiornarsi geometria, carichi dipendenti e quantità; una variabile non collegata resta invece un input separato. L'automazione è affidabile solo quando queste dipendenze sono esplicite e verificabili.

### Separare quattro passaggi

La generazione definisce geometria e identificativi. L'analisi determina la risposta per le ipotesi e le azioni assegnate. La verifica confronta domanda e capacità secondo il metodo pertinente. L'ottimizzazione ordina le alternative ammissibili rispetto a obiettivi dichiarati. Un valore basso di costo non dimostra che una soluzione sia ammissibile; una verifica favorevole non prova l'ottimalità.

### Un esempio di dipendenza

In una trave semplicemente appoggiata con carico uniforme assegnato $q$, il momento massimo cresce col quadrato della luce. Se invece $q$ include un peso proprio che cambia con la sezione, modificare la sezione richiede ricalcolare anche il carico. Una cache indicizzata solo dalla luce conserva allora risultati obsoleti.

```math
M_{max}=\frac{qL^2}{8}
\frac{M_{max}(1{,}1L)}{M_{max}(L)}=1{,}21
```

Il secondo rapporto vale soltanto a $q$ costante. Per $q=25\,\mathrm{kN/m}$ e $L=8\,\mathrm{m}$ il momento è 200 kNm; a 8,8 m diventa 242 kNm. Il 10% in più di luce introduce il 21% in più di domanda flessionale in questo modello. Il confronto discende dall'equilibrio della trave, non da una regola generale di dimensionamento.

### Dati strutturati e identità

In un grafo parametrico o in uno script, ogni record deve associare identificativo, unità e provenienza. Non accoppiare carichi e sezioni soltanto per posizione in una lista se un filtro può cambiarne l'ordine. Nelle strutture di dati ad albero anche il percorso del ramo è informazione: appiattire i rami può accoppiare campate e combinazioni estranee. Controlla le cardinalità prima e dopo ogni join o filtro.

### Riproducibilità e limiti

Salva versione degli input, versione del metodo, criteri di arresto e motivi di esclusione. Confronta un caso semplice con soluzione indipendente e ripeti il processo cambiando un parametro per volta. Le sensibilità dipendono dal punto di partenza; una graduatoria fra candidati campionati non è la prova di un minimo globale.

In Anthea il motore numerico resta nei progetti di calcolo e nelle librerie previste. La Wiki espone esempi didattici e collega i moduli, senza diventare una seconda implementazione del solutore. Per formulazione e benchmark della trave, prosegui con [Elementi beam](wiki:beam).

