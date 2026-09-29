# 09-urto

Urto equivalente H=50 kN/m in testa; combinazione eccezionale separata.

Aprire modello.anthea, Terreno → Stabilità globale. I parametri, le combinazioni e il dominio di ricerca sono salvati nel file. Vista Verifiche → Stabilità globale.

## Confronto MAX 16

Questa cartella contiene il modello e i risultati ANTHEA. Lo stato del confronto indipendente MAX è documentato in stato-max.json e nel rapporto della raccolta.

Per confrontare: impostare Bishop, terreno, falda, carichi e fattori identici a risultati-anthea.json. Confrontare prima il medesimo cerchio (xc, yc, R), quindi la ricerca con il medesimo dominio; documentare conci, esclusioni, fattori e versione MAX. Non confrontare F caratteristico con F su parametri M2 ridotti.

- Globale A2–M2–R2 1: F=1,235741751; η=0,890153626; Soddisfatta nel dominio esplorato; risolte 1193/3041.
- Globale A2–M2–R2 2: F=1,127652772; η=0,9754775826; Soddisfatta nel dominio esplorato; risolte 1205/3060.
- Globale eccezionale 3: F=1,301958841; η=0,7680734357; Soddisfatta nel dominio esplorato; risolte 1320/2976.