# 05-coesione-profonda

Come 04, c′=10 kPa nello strato di fondazione; drenato.

Aprire modello.anthea, Terreno → Stabilità globale. I parametri, le combinazioni e il dominio di ricerca sono salvati nel file. Vista Verifiche → Stabilità globale.

## Confronto MAX 16

Questa cartella contiene il modello e i risultati ANTHEA. Lo stato del confronto indipendente MAX è documentato in stato-max.json e nel rapporto della raccolta.

Per confrontare: impostare Bishop, terreno, falda, carichi e fattori identici a risultati-anthea.json. Confrontare prima il medesimo cerchio (xc, yc, R), quindi la ricerca con il medesimo dominio; documentare conci, esclusioni, fattori e versione MAX. Non confrontare F caratteristico con F su parametri M2 ridotti.

- Globale A2–M2–R2 1: F=1,434025292; η=0,7670715477; Soddisfatta nel dominio esplorato; risolte 1023/2786.
- Globale A2–M2–R2 2: F=1,273924164; η=0,8634736913; Soddisfatta nel dominio esplorato; risolte 1054/2769.