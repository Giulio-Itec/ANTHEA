## Capacità portante delle fondazioni superficiali

La capacità portante descrive il collasso del terreno sotto la fondazione. Non coincide con una pressione ammissibile universale: la stessa fondazione può raggiungere cedimenti incompatibili molto prima del collasso. Questa pagina sviluppa un riferimento statico per carico verticale centrato, terreno omogeneo e piano di posa orizzontale; non implementa un nuovo solutore Anthea.

### Geometria, drenaggio e azioni

![Figura 06.1 — Fondazione nastriforme: larghezza B, profondità D, carico verticale e pressione di contatto. La regione tratteggiata indica il terreno coinvolto, non una superficie critica calcolata.](../../X.Desktop/Assets/Wiki/footing.svg)

La risultante verticale deve comprendere i pesi pertinenti alla convenzione scelta. Distingui la pressione totale al contatto dalla pressione netta rispetto alla tensione preesistente alla quota di posa. Dichiarare una capacità netta e confrontarla con un'azione lorda altera il confronto. Prima dei coefficienti scegli il tipo di analisi: tensioni efficaci per condizioni drenate, tensioni totali e resistenza non drenata per il caso idealizzato non drenato.

### Struttura della relazione drenata

Per fondazione nastriforme ideale, terreno orizzontale e azione verticale centrata, la struttura classica della relazione è:

```math
q_{ult}=c'N_c+q'N_q+\frac{1}{2}\gamma'BN_\gamma
```

I tre termini rappresentano il contributo di coesione, sovraccarico efficace alla base e peso del terreno nel meccanismo. Le estensioni per forma, profondità, inclinazione del carico, base e pendio dipendono dal metodo scelto. In particolare $N_\gamma$ e i correttivi non vanno prelevati da autori diversi per costruire una formula ibrida. Qui non si propone una formulazione completa di Brinch Hansen.

| Simbolo | Definizione | Unità |
| --- | --- | --- |
| $q_{ult}$ | Pressione ultima lorda | kPa |
| $c'$ | Intercetta di resistenza in tensioni efficaci | kPa |
| $q'$ | Tensione verticale efficace alla quota di posa | kPa |
| $\gamma'$ | Peso efficace pertinente al terreno sotto base | kN/m³ |
| $B$ | Larghezza della fondazione nastriforme | m |
| $N_c,N_q,N_\gamma$ | Fattori del metodo dichiarato | adimensionali |
| $c_u$ | Resistenza non drenata | kPa |
| $D$ | Profondità del piano di posa | m |

Il simbolo $\gamma'$ non significa che tutta la zona sia necessariamente immersa: sopra falda occorre il peso appropriato, sotto falda il peso immerso. Una falda che attraversa il meccanismo richiede un trattamento coerente, non una media scelta senza giustificazione. Anche il sovraccarico alla base va costruito con tensioni efficaci nel caso drenato.

### Caso non drenato completamente specificato

Per una striscia su terreno omogeneo idealmente puramente coesivo, carico verticale centrato e correzioni geometriche non applicate, il riferimento limite è:

```math
\phi_u=0,\quad N_q=1,\quad N_\gamma=0,\quad N_c=2+\pi
q_{ult,net}=(2+\pi)c_u
q_{ult,gross}=(2+\pi)c_u+q
```

Qui $q$ è il sovraccarico totale alla quota di posa, non $q'$ del modello drenato. Non sottrarre la pressione interstiziale una seconda volta da una resistenza espressa in tensioni totali. Il valore $2+\pi$ è riferito a questo modello limite e non si identifica con tutti i fattori chiamati $N_c$ in letteratura.

### Esempio per metro di fondazione

Assegna $c_u=50\,\mathrm{kPa}$, $B=2\,\mathrm{m}$, $D=1\,\mathrm{m}$ e peso totale del terreno sopra base $\gamma=18\,\mathrm{kN/m^3}$. Si modella l'interramento soltanto mediante sovraccarico uniforme, senza incremento di resistenza da profondità. La risultante applicata lorda è $V=400\,\mathrm{kN/m}$.

```math
q=\gamma D=18\,\mathrm{kPa}
q_{ult,net}=(2+\pi)50=257{,}08\,\mathrm{kPa}
q_{ult,gross}=257{,}08+18=275{,}08\,\mathrm{kPa}
q_{appl}=\frac{400}{2}=200\,\mathrm{kPa}
R_{ult}=q_{ult,gross}B=550{,}16\,\mathrm{kN/m}
```

La pressione applicata è inferiore alla pressione ultima ideale, ma questo confronto non costituisce una verifica SLU di progetto: non sono stati applicati coefficienti parziali, né esaminati eccentricità, scorrimento, stabilità globale e cedimenti. I valori permettono invece di controllare unità e distinzione lordo/netto in un modello elementare.

### Quando cambiare modello

Una lente debole sotto uno strato resistente può spostare il meccanismo e rendere inadeguata l'ipotesi omogenea. Un carico eccentrico modifica area efficace e contatto; il terreno non offre una trazione illimitata alla base. Un pendio vicino rompe la simmetria; il sisma aggiunge azioni inerziali e può modificare la resistenza. In questi casi serve una formulazione esplicitamente documentata per il problema.

Il riferimento tecnico consultato è FHWA GEC 6, *Shallow Foundations*, per fattori, convenzioni e campo. L'eventuale verifica italiana richiede NTC, Circolare e quadro Eurocodice 7 adottati nel progetto: FHWA non è una prescrizione italiana. Il modulo Muri di sostegno tratta la fondazione del muro con le proprie ipotesi; non è presentato come modulo universale per plinti.

