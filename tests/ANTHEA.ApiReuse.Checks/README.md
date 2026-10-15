# Riuso delle API GPC

`Fixtures/before.json` è una cattura dei risultati ANTHEA **prima** delle
sostituzioni, sorgenti di produzione di `76e7916`, salvata nel commit `285e048`.
Non è un atteso indipendente e non deve essere rigenerata dopo una modifica.
`--capture <file nuovo>` rifiuta di sovrascrivere un file esistente.

I 266 casi coprono aderenza, ancoraggi e sovrapposizioni, dettagli dei quattro
tipi di elemento e delle tre forme, interassi, regioni, acciai e diagrammi.
Testi, esiti, rifiuti e struttura dei dati devono restare identici. Tolleranza
numerica relativa `1e-12 * max(|prima|, |dopo|)`, senza soglia assoluta:
assorbe soltanto differenze di ordine delle operazioni e di norma
euclidea, non scostamenti dei metodi. La prova stampa numero e massimo scarto.

Due casi di ancoraggio hanno attesi indipendenti scritti a mano nel programma:
fbd=3 MPa, lb=400 mm, l0=600 mm. Il controllo del codice IL verifica inoltre
le chiamate reali alle librerie: non basta importarne il namespace o replicare
la medesima formula. Le curve M–χ e i percorsi completi di produzione sono
coperti dalla cattura densa esistente, incluse interruzioni e rifiuti.

Esecuzione tramite `build/ci.ps1`, suite `ANTHEA.ApiReuse.Checks` nel profilo quick.

API-02: un caso della cattura densa vicino alla soglia dei raggi conserva
esplicitamente l'interasse storico di 452,3893578248936 mm. È una regressione di
compatibilità, **non un atteso geometrico corretto**: il raggruppamento per raggi
arrotondati è instabile nel legacy e nella DLL. Interassi, dettagli e fessurazione
delle sezioni circolari mantengono il percorso precedente fino alla correzione
e al rilascio coerente della libreria. Gli altri casi usano le API native.

DET-1 (analisi dei bug del 15/10/2026): con GPCChecker.Concrete corretto
l'interasse di solette e pareti si misura lungo gli strati. La prova ammette una
per una le sole quattro voci «Interasse verticale sulla sezione» delle pareti
600×800 (con e senza sovrapposizione e staffe): da 424 a 208 mm, atteso a mano
(800 − 2·(70 + 10 + 8))/3, verifica da non soddisfatta a soddisfatta. Ogni altro
campo resta identico alla cattura, che non si rigenera.
