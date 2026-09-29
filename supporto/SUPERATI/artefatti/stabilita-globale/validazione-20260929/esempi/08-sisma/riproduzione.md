# 08-sisma

SLV: ag/g=0,20, F0=2,5, categoria C, St=1; kh=0,1064 e kv=±0,0532.

Aprire modello.anthea, Terreno → Stabilità globale. I parametri, le combinazioni e il dominio di ricerca sono salvati nel file. Vista Verifiche → Stabilità globale.

## Confronto MAX 16

Questa cartella contiene il modello e i risultati ANTHEA. Lo stato del confronto indipendente MAX è documentato in stato-max.json e nel rapporto della raccolta.

Per confrontare: impostare Bishop, terreno, falda, carichi e fattori identici a risultati-anthea.json. Confrontare prima il medesimo cerchio (xc, yc, R), quindi la ricerca con il medesimo dominio; documentare conci, esclusioni, fattori e versione MAX. Non confrontare F caratteristico con F su parametri M2 ridotti.

- Globale A2–M2–R2 1: F=1,235741751; η=0,890153626; Soddisfatta nel dominio esplorato; risolte 1193/3041.
- Globale A2–M2–R2 2: F=1,127652772; η=0,9754775826; Soddisfatta nel dominio esplorato; risolte 1205/3060.
- Globale SLV kv− 3: F=1,268239743; η=0,9461933412; Soddisfatta nel dominio esplorato; risolte 1299/3094.
- Globale SLV kv+ 4: F=1,243005496; η=0,9654020069; Soddisfatta nel dominio esplorato; risolte 1299/3094.