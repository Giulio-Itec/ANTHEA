## Stralli equilibrio rigidezza geometrica e vibrazioni

Uno strallo trasmette trazione lungo il proprio asse e introduce simultaneamente forze verticali e orizzontali. La geometria decide quanto sforzo serve per sostenere una data componente verticale e quanta compressione viene trasferita all’impalcato. La configurazione di equilibrio e lo stato di tesatura fanno parte del modello.

### Inclinazione e azioni sull’impalcato

Per un cavo rettilineo ideale inclinato di α rispetto all’orizzontale:

```math
T=\frac{V}{\sin\alpha}
H=V\cot\alpha
```

Con V = 1000 kN, a 30 gradi servono T = 2000 kN e H = 1732 kN. A 60 gradi T scende a circa 1155 kN e H a 577 kN. Per confrontare due configurazioni progettuali occorre però tenere conto anche di lunghezza, altezza dell’antenna, posizione degli ancoraggi e distribuzione delle forze negli altri stralli.

Se gli altri spostamenti dell’ancoraggio sono impediti e si considera soltanto l’allungamento assiale lineare di una barra tesa, la rigidezza verticale materiale è (EA/L) sin²α. Un cavo reale aggiunge effetti di freccia, pretensione e rigidezza geometrica; può perdere tensione e non lavora come un puntone compresso. Usare soltanto la proiezione di EA/L è quindi una semplificazione circoscritta.

### Un ordine di grandezza dinamico

Per la corda tesa ideale con massa lineare μ e trazione uniforme T, la prima frequenza è:

```math
f_1=\frac{1}{2L}\sqrt{\frac{T}{\mu}}
```

Con L = 100 m, T = 2 MN e μ = 50 kg/m risulta f1 = 1 Hz. Una variazione della tensione cambia la frequenza: a parità del resto, raddoppiare T la moltiplica per √2. Curvatura del cavo, rigidezza flessionale, condizioni degli ancoraggi e dispositivi esterni possono modificare questa stima.

### Vibrazioni e manutenzione

Pioggia e vento possono attivare meccanismi aeroelastici che la sola frequenza propria non descrive. Trattamenti della superficie, smorzatori e collegamenti fra cavi affrontano fenomeni differenti; un collegamento può anche creare nuovi modi locali. Ispezionabilità degli ancoraggi, protezione dalla corrosione e possibilità di sostituzione devono essere considerate insieme alla risposta resistente.

ANTHEA non calcola attualmente l’equilibrio non lineare o la dinamica di un sistema di stralli. Riferimento bibliografico: FHWA, Wind-Induced Vibration of Stay Cables, capitolo 4, rapporto di ricerca storico da leggere nel proprio contesto, non prescrizione normativa corrente.

### Proseguire nella Wiki

- [Cos'è un ponte?](wiki:bridge)
- [Dinamica e sisma del modello](wiki:dinamica-e-sisma-del-modello)

