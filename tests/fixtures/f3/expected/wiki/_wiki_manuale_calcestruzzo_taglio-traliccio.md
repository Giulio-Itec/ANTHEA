## Taglio nel calcestruzzo armato e scelta del traliccio

L’inclinazione del puntone cambia contemporaneamente la forza nelle staffe e la compressione nel calcestruzzo. Aumentare cot θ aiuta il ramo delle staffe, ma riduce quello del puntone: il valore più favorevole dipende da quale meccanismo governa. Questo approfondimento sviluppa il caso con armatura trasversale verticale, z = 0,9d e modello a inclinazione variabile; non estende automaticamente il risultato a torsione, sisma o sezioni prive di staffe.

### Due meccanismi che devono resistere insieme

Posto x = cot θ, Asw è l’area dei rami efficaci della staffa e s il passo. Le espressioni delle NTC 2018 §4.1.2.3.5.2, specializzate a staffe verticali, possono essere scritte come segue. Nella notazione adottata ν = 0,5 è il fattore della resistenza del calcestruzzo usato dalla formulazione NTC citata; αc dipende dallo stato di compressione e non va assunto sempre unitario.

```math
A=z\frac{A_{sw}}{s}f_{yd}
B=z b_w\alpha_c\nu f_{cd}
V_{Rsd}=Ax
V_{Rcd}=B\frac{x}{1+x^2}
V_{Rd}=\min(V_{Rsd},V_{Rcd})
```

Con le grandezze geometriche in mm e le tensioni in MPa, A e B sono in N. Il campo della formulazione citata è 1 ≤ x ≤ 2,5. Non basta verificare le staffe: occorre controllare anche il puntone e i dettagli costruttivi, compresi ancoraggio e armatura longitudinale richiesta dal meccanismo resistente.

### Perché la scelta ottimale non è sempre quarantacinque gradi

Nel campo x ≥ 1 il primo ramo cresce e il secondo non cresce. L’intersezione, se reale, è data da x² = B/A − 1. Se B/A ≤ 2 governa il puntone già per x = 1, quindi il massimo si trova all’estremo x = 1. Se 2 < B/A < 7,25 il massimo coincide con l’intersezione. Se B/A ≥ 7,25 il ramo delle staffe governa fino a x = 2,5, che diventa l’estremo ottimale. Vale per A > 0 e B > 0: con staffe assenti questo problema non descrive la verifica senza armatura a taglio.

La massimizzazione è un confronto fra modelli di resistenza nello stesso stato limite. Non autorizza a cambiare θ separatamente in verifiche che richiedono un traliccio coerente, né a ignorare le prescrizioni del comportamento dissipativo.

### Esempio numerico con due quantità di staffe

Si assumono bw = 300 mm, d = 500 mm, z = 450 mm, fck = 30 MPa, αcc = 0,85, γc = 1,5, fyk = 450 MPa, γs = 1,15, αc = 1. Risultano fcd = 17 MPa, fyd = 391,30 MPa e B = 1147,50 kN.

| Staffatura | Asw | A | x ottimale | VRd |
| --- | --- | --- | --- | --- |
| Due rami Ø8 ogni 200 mm | 100,53 mm² | 88,51 kN | 2,50 | 221,28 kN |
| Due rami Ø10 ogni 100 mm | 157,08 mm² | 276,60 kN | 1,774 | circa 490,80 kN |

Nel primo caso l’intersezione sarebbe oltre x = 2,5: non è ammissibile usarla. A x = 2,5 il puntone porta circa 395,69 kN, ma le staffe limitano la resistenza a 221,28 kN. Nel secondo caso l’intersezione cade nel campo ammesso. Imporre sempre x = 2,5 darebbe ancora circa 395,69 kN, inferiore al massimo compatibile: più staffe non implica che convenga mantenere l’inclinazione più bassa del puntone.

![Resistenze di staffe e puntone al variare di cotangente theta per i due esempi](../../X.Desktop/Assets/Wiki/taglio-traliccio.png)

### Come leggere il risultato in ANTHEA

Nel modulo della sezione in c.a. controllare separatamente bw, d, Asw, passo, inclinazione delle staffe, criterio e cot θ per ciascuna direzione. Un valore geometrico suggerito deve essere coerente con la disposizione effettiva delle barre. Confrontare il taglio richiesto con entrambi i rami resistenti e leggere gli avvisi del profilo normativo scelto. Il calcolo di una sezione non sostituisce il progetto del dettaglio lungo l’elemento.

La fessura inclinata attraversa un tratto finito di trave: è per questo che la resistenza coinvolge un insieme di staffe e un flusso di compressione, mentre la flessione ordinaria viene spesso ricondotta all’equilibrio di una sola sezione. Vicino a carichi concentrati, appoggi o brusche discontinuità geometriche può essere necessario un modello a puntoni e tiranti dedicato.

Fonte: NTC 2018, §4.1.2.3.5. Sviluppo algebrico, grafico ed esempi numerici di questa pagina sono redatti per ANTHEA.

### Proseguire nella Wiki

- [Sezione in calcestruzzo armato](wiki:sezione-in-calcestruzzo-armato)

