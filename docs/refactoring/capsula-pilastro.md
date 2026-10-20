# Capsula: armature per tratti e dettagli del pilastro

Richiesta del 20 ottobre 2026: capsula 5000 × 2500 mm usata come
pilastro/pila, dettagli non disponibili e armature da governare
separatamente sui tratti curvi e rettilinei.

## Causa e correzione

Il guard di `ConcreteDetailingCalculator` rifiutava ogni sezione con
`Template`, anche se geometricamente valida. La cattura prima della
modifica riproduce il rifiuto con 24 barre e area 11.158.245,7046 mm².
Il limite non dipende dalle dimensioni del caso segnalato.

Il nuovo adattatore ammette la sola capsula come pilastro, quando il
generatore GPC fornisce il percorso perimetrale degli assi delle barre.
Le altre tipologie avanzate conservano i limiti dichiarati. GPC applica
le regole generali NTC già implementate e sostituisce soltanto il
riconoscimento dell'interasse: ascissa curvilinea sul percorso chiuso,
compresi archi discretizzati e tratto di chiusura. Una barra manuale
fuori percorso lascia l'interasse da completare, mantenendo gli altri
controlli. Non viene estesa la verifica al confinamento sismico della pila.

## Wizard e persistenza

Il modo «Curve e rettilinei separati» espone numero e diametro per
semicirconferenza, e numero e diametro delle barre interne per rettilineo.
Gli estremi di tangenza appartengono agli archi: il totale è
2 × numero per arco + 2 × numero interno per rettilineo.
I raggi sono riferiti al centro di ciascuna semicirconferenza.
La dimensione maggiore determina l'orientamento.

Chiavi nuove `wizard_stadium_layout`, `wizard_stadium_curve_count`,
`wizard_stadium_curve_diameter_mm`, `wizard_stadium_straight_count`,
`wizard_stadium_straight_diameter_mm`. Chiave modo assente: disposizione
radiale precedente; aprire un vecchio wizard non cambia le barre.
Il vecchio nome del wizard è riconosciuto come alias. L'applicazione
rimane transazionale e il documento conserva la sezione nativa GPC.
I report includono i conteggi e i diametri distinti.

Staffe: il percorso perimetrale geometrico non conferma il trattenimento
delle barre. Conferma delle legature e parametri efficaci del taglio
rimangono espliciti; il wizard azzera i parametri della sezione precedente.

## Evidenze

Checker `8bf4af33`, ramo locale `codex/capsula-armature-dettagli`.
Set candidato AC: 3.181 PASS, 17 fallimenti Steel già ammessi e due prove
Model ignorate; Concrete 795 e Design 100 PASS. Compatibilità API
superata, nessuna rimozione inattesa.

Le nove geometrie preesistenti conservano bit per bit contorni, fori,
barre, staffe e fibre. DelaunayMesh e il set ufficiale AB non sono modificati.
Le catture sono in `supporto/artefatti/capsula-pilastro/compatibilita/`.
Il caso indipendente 17 Ø32 per arco e 8 Ø25 per rettilineo produce
50 barre, As = 35.198,4041 mm² e interasse massimo 277,7999 mm.
Questi sono dati sintetici di collaudo, non dimensionamento della pila
dell'utente.

Il primo collaudo integrato supera Application (3.057 controlli),
Presentation (3.523) e Architecture. WPF wizard: 84 PASS; WPF Paratie: 79 PASS.
Reports supera i 475 controlli del corpus preesistente; aggiunto poi un caso
PDF dedicato alla capsula 5000 × 2500 con 50 barre, da includere nella
riesecuzione conclusiva.

Guide globali Rev56: 99 e 165 pagine; 3.229 frammenti conservati,
387 formule Word modificabili e 26 parti del modello ITEC identiche per
ciascun documento. Tutte le pagine sono renderizzate; controllati i
montaggi e le pagine nuove a dimensione intera. Dopo una prima
esportazione Word senza file di uscita, il PDF teorico è prodotto
correttamente al secondo tentativo. Il renderer LibreOffice non è
disponibile; i PDF canonici sono esportati da Word e renderizzati con PDFium.

Il confronto dei 37 calcoli di esempio conserva esattamente 301.420 valori
numerici e 97.689 testi. Le 21 differenze sono soltanto versioni del motore
e impronte: 20 impronte sono riprodotte separatamente a partire dagli
input salvati e dai manifest AB/AC.

Il set candidato è consumato da `-GpcLibDir` senza installarlo in
`lib/Checker`. Per distribuire le nuove DLL occorre pubblicare i nuovi
commit GPC con autorizzazione, produrre il set ufficiale e ripetere il
collaudo prescritto. Il setup 1.12.0 già pubblicato è antecedente a questa
correzione.
