# Pretensione dei tiranti fra le fasi — report delle prove

ANTHEA e Checker, 21 ottobre 2026. Rilievo A1 della revisione del modulo paratie.

## 1. Oggetto

Il motore Checker ricalcola in ogni fase la forza del sostegno come N = P₀ + EA/L·(e − ep), dove P₀ è la pretensione indicata in quella fase. Prima della correzione, ANTHEA passava la pretensione scritta in ciascuna riga di attivazione, e una riga nuova nasceva con 0. Un tirante rielencato in una fase successiva perdeva quindi la pretensione, a sfavore di sicurezza. La guida teorica affermava invece che le pretensioni si trasferiscono fra le fasi.

Comportamento scelto dall'utente il 21/10/2026: opzione (a) con avviso, senza migrazione dei documenti.

- La riga nuova di «Sostegni presenti nelle fasi» nasce con la pretensione vuota.
- Se il sostegno era già presente nella fase precedente, la cella vuota mantiene la sua pretensione; all'installazione o alla reinstallazione vale 0.
- Un numero sostituisce la pretensione; 0 la rilascia.
- Una riduzione rispetto alla fase precedente viene segnalata nel disegno e nei risultati.
- I documenti già salvati conservano i valori scritti. I loro risultati e le loro impronte non cambiano.

## 2. Modello di confronto e attesi

Tutte le prove di calcolo usano la stessa mensola analitica, l'esempio della guida teorica:

- L = 6 m, incastro al piede, EI = 200 000 kNm² (2·10¹⁴ Nmm²), larghezza della fascia b = 1 m;
- forza H = 10 kN in testa;
- tirante orizzontale in testa con EA/L = 10 000 kN/m.

Le rigidezze sono kc = 3EI/L³ = 2 777,78 kN/m per la mensola e ka = EA/L per il tirante.

Le formule chiuse sono:

```
tirante installato con il carico:   u = H / (kc + ka·(b/s)·cos²α)        F = ka·cos α·u
F1 senza tirante:                   u1 = H / kc = 3,60 mm
F2, tirante installato con P0:      u2 = (H − P0 + ka·u1) / (kc + ka)    F2 = P0 + ka·(u2 − u1)
F3, rilascio (P0 = 0):              u3 = (H + ka·u1) / (kc + ka) = u1    F3 = 0
```

Gli attesi vengono dagli script indipendenti della revisione, `tools/attesi/paratie_revisione/anthea/cases.py` e `prestress.py` del branch Checker `claude/verifica-paratie`. Sono ricalcolati in aritmetica razionale esatta (Python `fractions`) e arrotondati al double più vicino. Nessun atteso è stato preso dal motore. Le conversioni di unità hanno attesi definitori, per esempio 20° = 0,3490658503988659 rad e 2,5 m = 2500 mm.

Lo scarto è sempre calcolato come ottenuto − atteso.

## 3. Prove ANTHEA

Le prove stanno in `tests/ANTHEA.Presentation.Tests/EmbeddedWallPrestressChecks.cs` e fanno parte della suite Presentation.Tests. Ognuna registra atteso, ottenuto e scarto in `paratie/pretensione-esiti.csv`; le tabelle che seguono sono generate da quel file.

| Prova | Cosa verifica |
|---|---|
| C1 conversioni | `EmbeddedWallAdapter.ToModel` su un documento con terreni, falda, sovraccarico, carichi e quattro sostegni T1–T4. Conversioni: m→mm, kNm²→Nmm² (×10⁹), kN/m³→N/mm³ (×10⁻⁶), kPa→MPa, gradi→radianti, kN→N, kNm→Nmm. Grandezze già in unità del motore: kh in N/mm³, EA/L e i carichi lineari. Pretensione vuota all'installazione = 0. |
| C2–C4 tirante in testa | Analisi completa con il tirante installato insieme al carico: interasse uguale alla fascia, interasse 2,5 m, inclinazione 30°. |
| C5 esempio della guida | F1 senza tirante, F2 con 50 kN, F3 rielencato con la cella vuota. F3 deve restare come F2. Si controlla anche la pretensione risolta dal documento. |
| C6 valore ripetuto | F3 con 50 kN scritti: la storia deve essere identica a quella di C5, bit per bit. |
| C7 rilascio | F3 con 0: tirante scarico e avviso «T1 da 50 a 0 kN nella fase F3» nei risultati. |
| C8 ritesatura | F3 con 80 kN: nuova pretensione, nessun avviso. |
| C9 reinstallazione | Tirante installato in F1 con 50 kN, omesso in F2 (rimosso), rielencato vuoto in F3: riparte da 0, senza la pretensione precedente. |
| C10 editor | Riga aggiunta con **Aggiungi** in F3: fase e caso assegnati, cella vuota, P₀ = 50 kN nel disegno, «pretensione mantenuta» nell'albero, calcolo. |
| C11 editor | La stessa riga portata a 0: avviso nel disegno e nei risultati, tirante scarico. |

Oltre ai valori numerici, la suite controlla anche condizioni logiche:

- tipo e verso dei sostegni;
- assenza di avvisi quando la pretensione non cala;
- testo dell'avviso nei risultati e nel disegno;
- fase, caso e cella vuota della riga creata da `NewEditingRow`, anche per un caso diverso dal principale.

### 3.1 Analisi (C2–C11)

Tolleranza assoluta 10⁻⁹ mm sugli spostamenti e 10⁻⁹ kN sulle forze. Le pretensioni risolte dal documento sono confrontate esattamente; il caso C6 è confrontato bit per bit con C5.

**C2 tirante in testa α 0°, s 1 m**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| spostamento in testa | mm | 0,7826086956521739 | 0,7826086956521778 | 3,9·10⁻¹⁵ |
| forza nel tirante | kN | 7,826086956521739 | 7,826086956521778 | 3,9·10⁻¹⁴ |

**C3 tirante in testa, interasse 2,5 m**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| spostamento in testa | mm | 1,475409836065574 | 1,475409836065619 | 4,5·10⁻¹⁴ |
| forza nel tirante | kN | 14,75409836065574 | 14,75409836065619 | 4,5·10⁻¹³ |

**C4 tirante in testa inclinato 30°**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| spostamento in testa | mm | 0,972972972972973 | 0,972972972972985 | 1,2·10⁻¹⁴ |
| forza nel tirante | kN | 8,426193117902645 | 8,426193117902752 | 1,1·10⁻¹³ |

**C5 esempio della guida, F3 con cella vuota**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| F1 spostamento in testa | mm | 3,6 | 3,599999999999406 | -5,9·10⁻¹³ |
| F2 spostamento in testa | mm | -0,3130434782608696 | -0,3130434782613533 | -4,8·10⁻¹³ |
| F2 forza nel tirante | kN | 10,8695652173913 | 10,8695652173924 | 1,1·10⁻¹² |
| F3 spostamento in testa | mm | -0,3130434782608696 | -0,3130434782613533 | -4,8·10⁻¹³ |
| F3 forza nel tirante | kN | 10,8695652173913 | 10,8695652173924 | 1,1·10⁻¹² |
| F3 pretensione risolta dal documento | kN | 50 | 50 | 0 |
| F3 pretensione della fase precedente | kN | 50 | 50 | 0 |

**C6 F3 con 50 kN ripetuti**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| F3 forza nel tirante, uguale alla cella vuota | kN | 10,8695652173924 | 10,8695652173924 | 0 |

**C7 rilascio esplicito con 0**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| F3 spostamento in testa | mm | 3,6 | 3,599999999999354 | -6,5·10⁻¹³ |
| F3 forza nel tirante | kN | 0 | 0 | 0 |

**C8 ritesatura a 80 kN**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| F3 spostamento in testa | mm | -2,660869565217391 | -2,660869565217904 | -5,1·10⁻¹³ |
| F3 forza nel tirante | kN | 17,39130434782609 | 17,3913043478269 | 8,2·10⁻¹³ |

**C9 rimozione in F2 e reinstallazione in F3**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| F1 spostamento in testa | mm | -3,130434782608696 | -3,130434782608711 | -1,6·10⁻¹⁴ |
| F1 forza nel tirante | kN | 18,69565217391304 | 18,69565217391289 | -1,6·10⁻¹³ |
| F2 spostamento in testa | mm | 3,6 | 3,599999999996467 | -3,5·10⁻¹² |
| F3 spostamento in testa | mm | 3,6 | 3,599999999996467 | -3,5·10⁻¹² |
| F3 forza nel tirante | kN | 0 | 0 | 0 |

**C10 riga aggiunta dall'editor in F3**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| pretensione mostrata nel disegno | kN | 50 | 50 | 0 |
| F3 forza nel tirante | kN | 10,8695652173913 | 10,8695652173924 | 1,1·10⁻¹² |

**C11 stessa riga portata a 0 kN**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| pretensione mostrata nel disegno | kN | 0 | 0 | 0 |
| F3 forza nel tirante | kN | 0 | 0 | 0 |

### 3.2 Conversioni (C1): 62 grandezze

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| testa 0 m | mm | 0 | 0 | 0 |
| piede −6 m | mm | -6000 | -6000 | 0 |
| larghezza fascia 1,25 m | mm | 1250 | 1250 | 0 |
| lunghezza elementi 0,25 m | mm | 250 | 250 | 0 |
| EI 200 000 kNm² | Nmm² | 200000000000000 | 200000000000000 | 0 |
| peso 12,5 kN/m | N/mm | 12,5 | 12,5 | 0 |
| tratto quota inferiore −6 m | mm | -6000 | -6000 | 0 |
| γ sinistra 18 kN/m³ | N/mm³ | 1,8·10⁻⁵ | 1,8·10⁻⁵ | 0 |
| γsat sinistra 20 kN/m³ | N/mm³ | 2·10⁻⁵ | 2·10⁻⁵ | -3,4·10⁻²¹ |
| γ destra 19 kN/m³ | N/mm³ | 1,9·10⁻⁵ | 1,9·10⁻⁵ | -3,4·10⁻²¹ |
| γsat destra 20,5 kN/m³ | N/mm³ | 2,05·10⁻⁵ | 2,05·10⁻⁵ | 0 |
| φ' 30° | rad | 0,5235987755982988 | 0,5235987755982988 | 0 |
| φ' 24° | rad | 0,4188790204786391 | 0,4188790204786391 | 0 |
| c' 5 kPa | MPa | 0,005 | 0,005 | 0 |
| c' 10 kPa | MPa | 0,01 | 0,01 | 0 |
| cu destra 40 kPa | MPa | 0,04 | 0,04 | 0 |
| cu sinistra vuota | MPa | — | — | — |
| strato quota inferiore −20 m | mm | -20000 | -20000 | 0 |
| kh 0,002 N/mm³ (senza conversione) | N/mm³ | 0,002 | 0,002 | 0 |
| kh 0,003 N/mm³ | N/mm³ | 0,003 | 0,003 | 0 |
| K0 0,5 |  | 0,5 | 0,5 | 0 |
| K0 0,6 |  | 0,6 | 0,6 | 0 |
| terreno sinistra 0 m | mm | 0 | 0 | 0 |
| terreno destra −2 m | mm | -2000 | -2000 | 0 |
| falda sinistra −1 m | mm | -1000 | -1000 | 0 |
| falda destra assente | mm | — | — | — |
| sovraccarico sinistra 10 kPa | MPa | 0,01 | 0,01 | 0 |
| sovraccarico destra 0 kPa | MPa | 0 | 0 | 0 |
| carico uniforme 1,5 kN/m | N/mm | 1,5 | 1,5 | 0 |
| moltiplicatore EI 0,8 |  | 0,8 | 0,8 | 0 |
| forza orizzontale 10 kN | N | 10000 | 10000 | 0 |
| coppia 3 kNm | Nmm | 3000000 | 3000000 | 0 |
| forza verso il basso 100 kN | N | 100000 | 100000 | 0 |
| quota del carico 0 m | mm | 0 | 0 | 0 |
| carico distribuito da −1 m | mm | -1000 | -1000 | 0 |
| carico distribuito a −4,5 m | mm | -4500 | -4500 | 0 |
| q superiore 1 kN/m | N/mm | 1 | 1 | 0 |
| q inferiore 4 kN/m | N/mm | 4 | 4 | 0 |
| T1 quota | mm | -1000 | -1000 | 0 |
| T1 inclinazione | rad | 0,3490658503988659 | 0,3490658503988659 | 0 |
| T1 interasse | mm | 2500 | 2500 | 0 |
| T1 capacità | N | 500000 | 500000 | 0 |
| T1 EA/L 10 000 kN/m | N/mm | 10000 | 10000 | 0 |
| T2 quota | mm | -2000 | -2000 | 0 |
| T2 inclinazione | rad | 0 | 0 | 0 |
| T2 interasse | mm | 1000 | 1000 | 0 |
| T2 capacità | N | — | — | — |
| T2 EA/L 10 000 kN/m | N/mm | 10000 | 10000 | 0 |
| T3 quota | mm | -500 | -500 | 0 |
| T3 inclinazione | rad | -0,1745329251994329 | -0,1745329251994329 | 0 |
| T3 interasse | mm | 3000 | 3000 | 0 |
| T3 capacità | N | 250500 | 250500 | 0 |
| T3 EA/L 10 000 kN/m | N/mm | 10000 | 10000 | 0 |
| T4 quota | mm | -3250 | -3250 | 0 |
| T4 inclinazione | rad | 0,5235987755982988 | 0,5235987755982988 | 0 |
| T4 interasse | mm | 1800 | 1800 | 0 |
| T4 capacità | N | 600000 | 600000 | 0 |
| T4 EA/L 10 000 kN/m | N/mm | 10000 | 10000 | 0 |
| T1 pretensione 50 kN | N | 50000 | 50000 | 0 |
| T2 pretensione vuota all'installazione | N | 0 | 0 | 0 |
| T3 pretensione 12,5 kN | N | 12500 | 12500 | 0 |
| T4 pretensione 0 kN | N | 0 | 0 | 0 |

## 4. Prove Checker

I casi K1–K7 sono in `tests/EmbeddedWalls.Tests/LockOffBetweenStagesTests.cs`, a livello di motore, in N e mm. Fissano il lato motore del contratto: per mantenere la pretensione di una cella vuota, ANTHEA passa lo stesso P₀. La tolleranza è 10⁻⁹ mm sugli spostamenti e 10⁻⁶ N sulle forze. I valori che seguono sono stati ottenuti su `master` 44e4d628, cioè con le correzioni P1–P3 del motore. Coincidono bit per bit con quelli ottenuti prima delle correzioni (f4c08315).

**K1 tirante in testa α 0°, s 1 m**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| forza nel tirante | N | 7826,086956521739 | 7826,086956521778 | 3,9·10⁻¹¹ |
| spostamento in testa | mm | 0,7826086956521739 | 0,7826086956521778 | 3,9·10⁻¹⁵ |

**K2 tirante in testa, interasse 2,5 m**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| forza nel tirante | N | 14754,09836065574 | 14754,09836065619 | 4,5·10⁻¹⁰ |
| spostamento in testa | mm | 1,475409836065574 | 1,475409836065619 | 4,5·10⁻¹⁴ |

**K3 tirante in testa inclinato 30°**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| forza nel tirante | N | 8426,193117902645 | 8426,193117902752 | 1,1·10⁻¹⁰ |
| spostamento in testa | mm | 0,972972972972973 | 0,972972972972985 | 1,2·10⁻¹⁴ |

**K4 pretensione 50 kN ripetuta in F3**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| F1 spostamento in testa | mm | 3,6 | 3,599999999999406 | -5,9·10⁻¹³ |
| F2 forza nel tirante | N | 10869,56521739131 | 10869,5652173924 | 1,1·10⁻⁹ |
| F2 spostamento in testa | mm | -0,3130434782608696 | -0,3130434782613533 | -4,8·10⁻¹³ |
| F3 forza nel tirante | N | 10869,56521739131 | 10869,5652173924 | 1,1·10⁻⁹ |
| F3 riferimento d'installazione | mm | 3,6 | 3,599999999999406 | -5,9·10⁻¹³ |
| F3 spostamento in testa | mm | -0,3130434782608696 | -0,3130434782613533 | -4,8·10⁻¹³ |

**K5 rilascio con 0 in F3**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| F3 forza nel tirante | N | 0 | 0 | 0 |
| F3 spostamento in testa | mm | 3,6 | 3,599999999999354 | -6,5·10⁻¹³ |

**K6 ritesatura a 80 kN in F3**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| F3 forza nel tirante | N | 17391,30434782608 | 17391,3043478269 | 8,2·10⁻¹⁰ |
| F3 spostamento in testa | mm | -2,660869565217391 | -2,660869565217904 | -5,1·10⁻¹³ |

**K7 rimozione in F2, reinstallazione in F3**

| Grandezza | Unità | Atteso | Ottenuto | Scarto |
|---|---|---|---|---|
| F1 forza nel tirante | N | 18695,65217391304 | 18695,65217391289 | -1,6·10⁻¹⁰ |
| F1 spostamento in testa | mm | -3,130434782608696 | -3,130434782608711 | -1,6·10⁻¹⁴ |
| F2 spostamento in testa | mm | 3,6 | 3,599999999996467 | -3,5·10⁻¹² |
| F3 forza nel tirante | N | 0 | 0 | 0 |
| F3 riferimento d'installazione | mm | 3,6 | 3,599999999996467 | -3,5·10⁻¹² |
| F3 spostamento in testa | mm | 3,6 | 3,599999999996467 | -3,5·10⁻¹² |

## 5. Controllo inverso

Per verificare che le prove colgano davvero il difetto, ho reintrodotto per un momento il vecchio comportamento: cella vuota = 0 anche per un sostegno già presente. In quel caso la suite si ferma su «F3 rielencato con pretensione vuota: la pretensione di F2 deve restare». Poi il codice è stato ripristinato.

## 6. Suite complete

- ANTHEA, `build/ci.ps1` profilo standard sul branch `fix/paratie-pretensione`: PASS 77, KNOWN 0, NEW-FAIL 0, uscita 0 (commit 896518b5); Presentation.Tests 4117 controlli senza WPF.
- ANTHEA, Presentation.Tests in sola modalità paratie: PASS 1628 controlli; prima delle nuove prove erano 1557.
- Checker, EmbeddedWalls.Tests su f4c08315, prima delle correzioni del motore: 81 superati su 81.
- Checker, EmbeddedWalls.Tests su 44e4d628, con le correzioni P1–P3: 96 superati su 96 con `TestCategory!=ReviewFinding`. Senza il filtro falliscono 2 test, P4 e P5: sono i rilievi ancora aperti della revisione, già ammessi come noti e indipendenti da questa modifica.

## 7. Differenze dai risultati attesi

- **Conversioni.** Scarto nullo su tutte le grandezze tranne γ = 19 kN/m³ e γsat = 20 kN/m³. Per queste il prodotto 19·10⁻⁶ o 20·10⁻⁶ in doppia precisione differisce di un'unità nell'ultima cifra dal letterale 1,9·10⁻⁵ o 2·10⁻⁵: −3,4·10⁻²¹ N/mm³, cioè 1,7·10⁻¹⁶ in termini relativi. È un arrotondamento della macchina e non un errore di conversione; resta dentro la tolleranza relativa di 10⁻¹⁵.
- **Analisi ANTHEA.** Scarto massimo 3,5·10⁻¹² mm sugli spostamenti e 1,1·10⁻¹² kN sulle forze. La tolleranza è 10⁻⁹, quindi lo scarto massimo vale lo 0,4 % della tolleranza. L'origine sono la tolleranza di convergenza del solutore di Newton e gli arrotondamenti in doppia precisione: il modello è lineare e l'elemento Hermite è esatto per carichi concentrati ai nodi.
- **Analisi Checker.** Scarto massimo 3,5·10⁻¹² mm e 1,1·10⁻⁹ N, con la stessa origine.
- **Casi esatti.** Nei casi di rilascio e di reinstallazione la forza vale esattamente 0: il tirante è unilaterale e la forza viene troncata a zero. La cella vuota e il valore ripetuto danno risultati identici bit per bit.
- **Esito.** Nessuna differenza supera le tolleranze. Nessun atteso è stato modificato per adattarlo ai risultati del codice.

## 8. Limiti e passi successivi

- Le prove ANTHEA usano le librerie installate in `lib/Checker` (set AE), cioè il motore prima delle correzioni P1–P3. Le prove Checker su `master` danno valori identici con il motore corretto.
- Le correzioni P1–P3 entreranno in ANTHEA con il prossimo set di librerie. L'attività pianificata del 22/10 alle 05:00 assegna le versioni a Model e Checker e propone il set.
- Wiki e prova WPF `ui/f3-equivalence` del profilo full: PASS su 896518b5 (119 fixture, 77 articoli, 30 ricerche, nessuna differenza non ammessa). I due articoli paratie modificati dalla Rev57 sono file aggiunti già ammessi in `tests/fixtures/f3/transitions-f5-wiki.json`, quindi non servono transizioni nuove.
- Restano decisioni dell'utente, non toccate qui: nessun coefficiente parziale (A2), fase nuova da quota 0 (A3), prima fase non geostatica (A4), segni ed etichette.

## 9. Commit

- ANTHEA, branch `fix/paratie-pretensione` da `refactoring/f5` d9132b92: 401f9791 codice e prove; 896518b5 guide Rev57, con gli originali Rev56 in `supporto/SUPERATI/paratie-pretensione-rev57-20261021` e la Wiki rigenerata.
- ANTHEA, unione in `refactoring/f5`: merge senza fast-forward del branch dopo la CI standard verde, come chiesto dall'utente.
- Checker, `master`: b00f1a6c prove, 5a01af62 merge.
