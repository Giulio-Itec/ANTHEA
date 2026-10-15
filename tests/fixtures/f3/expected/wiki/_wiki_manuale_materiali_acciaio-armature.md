## Acciaio per armature: proprietà e diagrammi

Il materiale definisce la risposta delle barre; non verifica da solo una sezione. La scheda propone B450C, B450A, classi storiche FeB e un materiale personalizzato. Nel catalogo B450A ha εu = 25 ‰, cioè (Agt)k ≥ 2,5 % (NTC 2018 Tab. 11.3.Ic; EN 1992-1-1 Tab. C.1, classe A), e B450C 75 ‰. Le schede salvate con valori diversi dal catalogo, per esempio B450A con il 30 ‰ di un catalogo precedente, conservano i propri valori, che il calcolo usa, e dichiarano la differenza; riselezionando la classe si adottano i valori del catalogo. I valori storici di catalogo non sostituiscono le prove su una struttura esistente.

### Grandezze e unità

| Simbolo | Significato | Unità |
| --- | --- | --- |
| Es | Modulo elastico | MPa |
| fyk, fu | Snervamento caratteristico e resistenza ultima | MPa |
| γs | Coefficiente parziale assegnato | adimensionale |
| εyd, εu | Deformazione di snervamento di progetto e ultima | adimensionale nelle formule |

```math
f_{yd}=\frac{f_{yk}}{\gamma_s}
\varepsilon_{yd}=\frac{f_{yd}}{E_s}
```

Con fyk = 450 MPa, γs = 1,15 ed Es = 200000 MPa, fyd = 391,304 MPa ed εyd = 0,00195652, cioè 1,95652 per mille. Nei campi che chiedono per mille si inserisce quest'ultimo valore; una deformazione adimensionale non si incolla senza conversione.

### Diagramma e classe storica

Il grafico della scheda materiali rappresenta il legame caratteristico. Il diagramma utilizzato dal calcolo dipende anche dal coefficiente parziale e dalle opzioni della verifica. Per un materiale personalizzato occorrono dati coerenti di modulo, snervamento, resistenza ultima, deformazione ultima e incrudimento.

L'allungamento storico A5 non viene convertito automaticamente nella deformazione ultima εu. Se εu manca, una funzione che la richiede non è completa. La scelta di una classe FeB non dimostra la corrispondenza del materiale esistente ai valori nominali del catalogo.

### Trasferimento fra moduli

Le proprietà compatibili possono essere condivise nel progetto; geometria, barre e carichi restano dati separati. Il tubo CHS del micropalo usa una definizione specifica. Il momento automatico del palo orizzontale conserva un modello elastico perfettamente plastico: l'incrudimento del catalogo non diventa automaticamente resistenza del meccanismo di Broms.

Consulta [Materiali e durabilità](wiki:guida-materiali-e-durabilita) per la compilazione e [Capacità orizzontale con Broms](wiki:capacita-orizzontale-con-broms) per il ruolo del momento resistente.

