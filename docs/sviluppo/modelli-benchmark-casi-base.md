# Esperimento: casi base, combinazioni e inviluppi

Misura dell'11 ottobre 2026 nel branch `codex/model-viewer-draft`. Documento di sviluppo, non validazione
ingegneristica per il rilascio. Il prototipo è in `tools/ModelViewer.CombinationBench`, fuori dalle librerie
di produzione e dalla UI. Nessuna modifica ai progetti GPC o al bundle `lib/Checker`.

## Dataset effettivamente acquisito

Modello MIDAS della spalla: 3.755 nodi, 3.435 plate, 181 beam. Acquisiti tutti i **49 casi statici** su tutti
gli elementi: 13.718 campioni nodali plate e 905 stazioni beam per caso. Otto componenti locali per plate,
sei per beam; totale **115.174 scalari per caso**, 5.643.526 valori per i 49 casi.
Le 73 definizioni lette comprendono 60 Add e 13 Envelope, anche annidate, con dipendenze ST/CBC e coefficienti
anche negativi. Si misurano tutte le definizioni, incluse quelle inattive usate come dipendenze.

L'acquisizione in 34 richieste seriali, fino a tre casi per richiesta, è terminata regolarmente:

| Operazione / dimensione | Misura |
| --- | ---: |
| Acquisizione dei risultati dei 49 casi, controlli e scrittura inclusi | 517,02 s (8 min 37 s) |
| Somma dei tempi HTTP | 494,44 s |
| JSON dei risultati | 145.360.947 byte |
| Lettura e indicizzazione JSON nel prototipo C# | 3,887 s |
| Array numerici dei casi base, soli double | 45.148.208 byte |
| Copia binaria sperimentale, indici inclusi | 47.491.313 byte |
| Scrittura copia binaria | 0,149 s |
| Rilettura e confronto bit per bit della copia binaria | 0,152 s |

Il tempo di acquisizione non include la precedente lettura delle tabelle geometriche. La rilettura binaria
avviene subito dopo la scrittura, con cache del sistema operativo calda: non è una misura di avvio a freddo.
I 45 MB sono solo i buffer numerici, non la memoria totale del processo. Il lettore JSON provvisorio,
con ordinamento e oggetti temporanei, raggiunge 927.014.912 byte di working set di picco: va sostituito da
una lettura per blocchi nel prodotto. L'archivio binario del test NON è il nuovo formato pubblico ANTHEA.

## Costruzione locale, dati già caricati

.NET 8.0.31 Release, Intel Core Ultra 9 275HX, Windows 11, 24 processori logici. Esecuzione su un thread,
vettori SIMD da quattro double. Il prototipo applica i coefficienti e i min/max componente per componente
in un grafo delle dipendenze, calcolando una volta ogni dipendenza nel singolo passaggio. Per ogni ripetizione
la cache delle combinazioni è svuotata: si riparte dai casi base. Sono inclusi allocazioni e controlli
di finitezza; esclusi rete, parsing, UI e GPU. Tre riscaldamenti, 31 ripetizioni; 21 per tutte le definizioni.

| Operazione | Mediana | 95° percentile |
| --- | ---: | ---: |
| T_0, tutte le componenti locali | 1,438 ms | 2,101 ms |
| SLU_Q1_1, tutte le componenti locali e dipendenze | 9,657 ms | 12,289 ms |
| SLV_X-_1, tutte le componenti locali e dipendenze | 4,068 ms | 6,247 ms |
| ENV_SLU, min e max di tutte le componenti locali | 57,864 ms | 68,127 ms |
| ENV_SLV, min e max di tutte le componenti locali | 63,755 ms | 74,361 ms |
| Tutte le 73 definizioni, min e max | 139,888 ms | 164,112 ms |
| SLU_Q1_1, solo Myy delle plate | 0,608 ms | 1,151 ms |
| ENV_SLU, solo Myy delle plate | 5,176 ms | 15,729 ms |
| Inviluppo già costruito: copia Myy e ricerca estremi | 0,108 ms | 0,123 ms |

Prima costruzione di SLU_Q1_1 dopo la lettura, prima del riscaldamento: 17,159 ms.
La prova WPF separata sul campo già importato dà 92,89 ms mediani fino a `OnRendered`, massimo 104,79 ms,
su sei cambi di componente. Non si sommano queste misure per dichiarare un tempo integrato mai misurato:
il prototipo delle combinazioni non è ancora collegato al viewer.

## Confronti con MIDAS

- Campione dell'elemento plate 1000 già acquisito: 49 casi base e 120 etichette min/max, **5.408 scalari**
  confrontati, nessuna differenza fuori tolleranza. Di questi 3.840 sono risultati ricostruiti e 1.568
  confermano i casi base dopo la riapertura. Incluse combinazioni annidate e sismiche con coefficienti negativi.
- SLU_Q1_1(max) richiesto di nuovo su tutte le plate: **109.744 scalari**, nessuna differenza fuori tolleranza.
  Massimo errore assoluto 8,1561e-7 nelle unità della tabella; errore scalato massimo 8,0218e-10.
  La richiesta MIDAS è durata 12,05 s.
- ENV_SLU(max) su tutte le plate: MIDAS non ha completato la richiesta entro **180 s**. Il confronto completo
  dell'inviluppo e il confronto beam con MIDAS non sono dichiarati conclusi; restano il campione, gli attesi
  manuali indipendenti e la misura locale sull'intero modello. I dati già acquisiti sono conservati.

Tolleranza fissata prima del confronto: `abs(ricostruito - MIDAS) <= 1e-6 + 1e-9 * abs(MIDAS)`.
Nessun atteso è sostituito con l'output del prototipo. Gli attesi manuali dei test coprono somma firmata,
min/max, coefficiente negativo su inviluppo annidato, percorso SIMD e coda scalare, rifiuto di cicli.

## Ambito e decisioni da portare nel prodotto

Il risultato sostiene l'importazione dei casi base con definizioni delle combinazioni e cache locale.
La lentezza residua è nella prima acquisizione dal solutore, non nella combinazione degli array già caricati.
Il motore definitivo dovrà vivere in GPC, con provenienza, revisione e convenzioni esplicite. ANTHEA riceve
il campo e lo rappresenta. Nessuna nuova formula entra in WPF, X.Core o X.Calculations.

Questo esperimento NON copre principali, CBall/all, deformate, tensioni, carichi mobili, combinazioni modali,
fasi costruttive o analisi non lineari. Non ricostruisce stati simultanei dagli estremi indipendenti.
Per il prodotto servono eligibilità alla sovrapposizione, casi governanti e stati concomitanti per le verifiche,
semantica dei coefficienti negativi e delle combinazioni annidate, gestione esplicita dei metodi non supportati.
Le componenti principali vanno elaborate sullo stato appropriato, non sommate dai principali dei casi base.

La richiesta originale di portare tutti gli inviluppi nel viewer prosegue dopo questa valutazione:
non è stato inserito un motore sperimentale nel visualizzatore. Il formato temporaneo `.antheamodel`
mantiene il proprio limite; i 49 casi completi sono conservati come evidenza e array del benchmark.

## Riproduzione ed evidenze

Directory ignorata da Git: `supporto/artefatti/model-viewer/base-case-benchmark/`.

- `run-01/source/base-acquisition.json`: completamento, tempi, richieste, dimensioni e SHA-256.
- `run-01/source/`: tabelle e 34 risposte originali. Input principali ricontrollati alla fine dell'acquisizione.
- `full-01/benchmark.json`: campioni temporali, memoria, controlli e runtime.
- `full-combination-validation/validation.json`: confronto completo SLU_Q1_1(max) plate.
- `run-01/references/`: risposta completa della combinazione; nessun manifest di completamento degli inviluppi.
- `ui/`: impostazioni vista, contouring, simulazione dichiarata del banner di connessione, tempi dei fotogrammi.

```powershell
dotnet run --project tools/ModelViewer.CombinationBench -c Release -- <source> <nuovo-output> <tabella-o-cartella-riferimenti>
dotnet run --project tools/ModelViewer.CombinationBench -c Release -- <source> <nuovo-output> <tabella-plate> --validate-only
build/ci.ps1 -Profile quick
```

Gli script di acquisizione ricevono la chiave solo a runtime, non la scrivono nei file. Sono richieste
di lettura delle tabelle dei risultati; nessuna modifica o nuova analisi del modello MIDAS.
Runner quick dell'esperimento: 27 PASS, nessun nuovo fallimento. Controlli della libreria applicativa: 105 PASS;
WPF sul modello reale: 65 PASS. Registro MOD-07.
