## Cos'è un ponte?

Un ponte mantiene la continuità di un percorso sopra un ostacolo lasciando uno spazio libero sottostante. La scelta strutturale parte da questo spazio: posizione degli appoggi, franco richiesto, terreno e accesso al cantiere definiscono le possibilità prima del dimensionamento delle travi.

### Leggere l'opera prima del modello

![Figura 07.1 — Taccuino di un viadotto a travata: campate, pila centrale e spalle. Il disegno identifica i componenti, senza attribuire loro dimensioni di progetto.](../../X.Desktop/Assets/Wiki/bridge-notebook.svg)

L'impalcato sostiene il piano viabile. Soletta, travi longitudinali, traversi e diaframmi ne distribuiscono le azioni; pile e spalle le trasferiscono alle fondazioni. Gli appoggi costituiscono l'interfaccia meccanica fra impalcato e sottostruttura. I giunti consentono movimenti relativi del piano viabile: non sono appoggi e non si modellano come sostegni verticali.

La luce si misura fra i riferimenti strutturali degli appoggi della campata; la lunghezza totale dell'opera comprende più campate e dipende dalla convenzione geometrica adottata. Nel disegno di calcolo annota entrambi i riferimenti: una quota architettonica fra facce non coincide necessariamente con la luce fra assi.

### Seguire il carico

Una ruota produce una domanda locale nella soletta; la soletta la ripartisce alle travi secondo rigidezze e geometria. Gli appoggi raccolgono reazioni concentrate, le pile portano azioni assiali e momenti, le fondazioni mobilitano il terreno. Il modello a trave equivalente condensa questo percorso, ma perde la distribuzione trasversale e gli effetti locali.

![Figura 07.2 — Percorso verticale in una campata ideale: carichi discendenti sull'impalcato, reazioni ascendenti agli appoggi e trasferimento attraverso pila e fondazione.](../../X.Desktop/Assets/Wiki/bridge-load-path.svg)

Per frenatura, temperatura e sisma il percorso cambia. Un appoggio mobile longitudinalmente non riceve nel modello ideale la stessa forza di un appoggio fisso; l'attrito e la rigidezza reali possono rendere meno netta questa distinzione. Occorre una pianta dei vincoli che mostri anche le direzioni libere. Un ritegno orizzontale senza fondazione capace di riceverne la reazione non completa il percorso resistente.

### Forma strutturale e conseguenze

| Sistema | Meccanismo prevalente | Decisione da esplicitare |
| --- | --- | --- |
| Travata | Flessione e taglio | Continuità, controventi e rigidezza trasversale |
| Arco | Compressione con possibili momenti | Destinazione della spinta orizzontale |
| Reticolare | Azioni assiali nelle aste del modello ideale | Eccentricità e comportamento dei nodi reali |
| Strallato | Stralli tesi e impalcato compresso/inflesso | Tesatura e sequenza di attivazione |
| Sospeso | Funi principali tese, pendini e trave irrigidente | Ancoraggi e risposta ai carichi non simmetrici |

Questa classificazione descrive il meccanismo, non prescrive intervalli universali di luce. Un cassone è una forma di sezione, non un sistema alternativo alla travata: può far parte di una travata, di un ponte strallato o di un ponte ad arco.

### Il ponte durante la costruzione

Prima del getto della soletta collaborante una trave metallica ha rigidezza, peso portato e ritegni diversi da quelli finali. Nei conci a sbalzo l'avanzamento modifica lo schema a ogni fase; la chiusura introduce continuità. Un'analisi della sola configurazione finale non ricostruisce la storia tensionale. Individua sezioni attive, appoggi provvisori e carichi di montaggio nella stessa sequenza temporale.

### Dettagli che decidono la vita utile

L'acqua raccolta da un giunto può raggiungere testate, appoggi e pulvini. Il progetto deve permettere drenaggio, ispezione e sostituzione degli appoggi con un percorso temporaneo dei carichi. Non si deduce la durabilità dal solo copriferro: contano esposizione, accessibilità, protezioni e manutenzione programmata.

Durante un'ispezione un quadro fessurativo o una corrosione osservata costituiscono evidenze; la capacità residua richiede rilievi, modello e verifiche. La stessa lesione può avere interpretazioni diverse in una fase costruttiva e nell'esercizio definitivo.

### Dall'anatomia al progetto in Anthea

Prima del predimensionamento prepara uno schema degli appoggi, una sezione trasversale e una sequenza delle fasi. Nel modulo Bridge Design verifica poi le ipotesi della famiglia scelta: il confronto di alternative non sostituisce le verifiche locali, geotecniche o sismiche. Prosegui con [il percorso dei carichi](wiki:load-path), [gli elementi beam](wiki:beam) e con il capitolo sulle fasi costruttive.

Testi e figure di questa pagina sono originali.

