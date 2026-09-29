# 10-non-drenato

cu=50 kPa, φu=0; analisi globale a tensioni totali. Conci in trazione segnalati, nessun taglio automatico.

Aprire modello.anthea, Terreno → Stabilità globale. I parametri, le combinazioni e il dominio di ricerca sono salvati nel file. Vista Verifiche → Stabilità globale.

## Confronto MAX 16

Questa cartella contiene il modello e i risultati ANTHEA. Lo stato del confronto indipendente MAX è documentato in stato-max.json e nel rapporto della raccolta.

Per confrontare: impostare Bishop, terreno, falda, carichi e fattori identici a risultati-anthea.json. Confrontare prima il medesimo cerchio (xc, yc, R), quindi la ricerca con il medesimo dominio; documentare conci, esclusioni, fattori e versione MAX. Non confrontare F caratteristico con F su parametri M2 ridotti.

- Globale A2–M2–R2 1: F=3,066589818; η=0,358704641; Discretizzazione non convergente: aumentare i conci.; risolte 418/3026.
- Globale A2–M2–R2 2: F=2,489353434; η=0,4418818096; Discretizzazione non convergente: aumentare i conci.; risolte 468/3207.