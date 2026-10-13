# F4.3b — sforzo normale nel taglio dei muri

13 ottobre 2026. Studio e decisione per il successivo port; **nessuna formula
runtime modificata**. Si riusa `SectionShearCalculator`, già distribuito, senza
creare un secondo verificatore. Registro F4-03.

## Riscontro e decisione

Fonte primaria: [NTC 2018, capitolo 4, pagina 79, §4.1.2.3.5.1 e formula
4.1.23](https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.num=1&art.tiposerie=SG&cdimg=18A0071600100010110005&dgu=2018-02-20).
Il contributo 0,15 σcp compare in entrambi i rami della resistenza, con
σcp limitata a 0,2 fcd; CRd,c=0,18/γc, k1=0,15 e vmin=0,035 k^1,5 √fck.
La norma richiede armatura longitudinale prolungata e ancorata; l'omissione
delle staffe richiede le condizioni previste per il tipo di elemento. Per
trazione significativa non è ammesso attribuire resistenza al calcestruzzo
senza armatura trasversale.

Il codice locale `RetainingWall.Structures` omette σcp, anche in presenza di
trazione. La API GPC include la compressione e rifiuta automaticamente ogni N
positivo (convenzione GPC: trazione positiva). Quest'ultimo è un limite
conservativo del supporto automatico, non una soglia normativa da inventare.

Decisione F4.3b: nel successivo F4.7 adottare il percorso GPC completo, separando
questa correzione dal trasferimento equivalente. Conservare in chiaro N, Ac,
faccia tesa, armatura efficace, σcp adottata e provenienza dei coefficienti.
In trazione, dati insufficienti o condizioni di ancoraggio non dimostrate,
non trasformare l'assenza di un risultato in un esito positivo. I controlli
costruttivi restano distinti dalla capacità della sezione. F4.5/F4.7 devono
implementare e collaudare questi contratti; questo studio non li dichiara chiusi.

## Riferimenti indipendenti e riuso dei test

`tools/qa/Study-F4WallShear.py` calcola gli attesi in Python, senza invocare
ANTHEA o GPC. Il test Checker `9ff8b494` li conserva come costanti; non modifica
le fixture precedenti. Per bw=1000, d=350, Ac=400000, Asl=1500 (mm/mm²),
fck=30, fcd=17 MPa e γc=1,5:

| N GPC (kN) | σcp (MPa) | VRd (kN) |
| ---: | ---: | ---: |
| 0 | 0 | 172,771081435 |
| −400 | 1 | 225,271081435 |
| −1360 | 3,4 | 351,271081435 |
| −2000 | 3,4 | 351,271081435 |
| +40 | non applicabile | non valutata automaticamente |

Cinque nuove prove più le sette `ShearMigrationTests`: **12/12 PASS**,
incluse le 2016 osservazioni legacy già presenti. Tolleranze fissate nei test:
1e-6 N sulla resistenza, 1e-12 su σcp e rapporto. Nessun valore atteso ottenuto
dai risultati del codice sotto prova.

## Effetto previsto sulle catture

Il programma legge le catture F4.1b senza modificarle. Per ogni controllo
ricostruisce faccia, zona, armatura e altezza utile; verifica prima che formula
indipendente e capacità legacy coincidano (1e-10 relativo/1e-9 kN assoluto),
poi aggiunge soltanto il contributo assiale della formula.

- **26 muri CA, 12512 controlli**: tutti i valori legacy riprodotti.
- **4192 capacità** cambierebbero, massimo **+4,301578125 kN/m**, **+2,80197%**.
- **Nessun cambio di esito** nel corpus; nessun caso di trazione nel corpus.
  Trazione e limite di compressione sono coperti dai vettori sintetici.
- Nove casi esclusi esplicitamente: sei muri a gravità, tre rifiuti già attesi.

È una previsione isolata della variazione di formula, non un confronto del
futuro adattatore in esecuzione né una certificazione dei dettagli costruttivi.
Lo studio rifiuta spessori sotto 50 mm: quel ramo richiede il contratto dei
materiali con le riduzioni pertinenti. Le impronte di tutti gli input e risultati
letti sono salvate nell'evidenza.

Riproduzione (destinazione nuova; i file esistenti non vengono sovrascritti):

```text
python tools/qa/Study-F4WallShear.py supporto/artefatti/refactoring/f4/before-port-1.7.1 supporto/artefatti/refactoring/f4/wall-sigma-study/independent.json
```

SHA-256 dell'evidenza originale:
`4D6837431389ED7D0C71D8206047961B1D81C55EBE580A86A446C2FB223B5F8C`.
Prove GPC: `supporto/artefatti/refactoring/f4/wall-sigma-tests/wall-sigma.trx`.
