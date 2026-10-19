# Incremento 9 — carichi distribuiti variabili per fase

Model definisce gli intervalli di carico, Geotechnics li integra nella
trave e trasferisce le azioni alla stabilità globale. ANTHEA conserva e
presenta dati e risultati: nessuna formula di carico nella presentazione.

## Campo implementato

Carico orizzontale lineare fra due quote, positivo verso destra, uniforme,
triangolare o trapezoidale; ammessi cambio di segno e intervalli sovrapposti.
Input obbligatori: fase, identificativo univoco nella fase, quote interne
alla paratia con superiore maggiore dell'inferiore, due intensità finite
e fonte. Il carico è espresso in N/mm sull'intera fascia GPC, equivalente
a kN/m verticale nell'editor. Non è una pressione e non viene moltiplicato
nuovamente per la larghezza. Il carico uniforme preesistente si somma.

Ogni fase elenca tutti i carichi presenti. Quelli omessi vengono rimossi
nella transizione; i campi sono interpolati negli incrementi della stessa
storia elastoplastica. Nessuna somma di risultati non lineari. La mesh
include tutti gli estremi; carico consistente mediante quadratura Gauss
con funzioni di Hermite. Recupero V/M e valori unilaterali di q alle
discontinuità, rollback e isolamento delle analisi conservati.

Nella stabilità globale della parete continua, le superfici ammissibili
passano sotto tutto il corpo rigido. In questo campo la coppia forza/momento
equivalente conserva esattamente risultante e momento del carico originale,
anche quando la risultante è nulla. Azione `StructureInterval-<Id>` con
coefficiente esplicito; γ·ψ applicato una volta. Superfici che tagliano la
parete e sostegni esterni restano fuori dal campo dell'adattatore esistente.
Slopes non è stato modificato.

Schema ANTHEA 9: nuova tabella `distributed_loads`, vuota nella migrazione
da schema 8; dati precedenti conservati e cache invalidata coerentemente.
L'elenco delle tabelle scorre verticalmente. Diagramma e colonna q netto
in kN/m, comprensivi delle reazioni del terreno, trasferiti direttamente
da GPC. PDF e CSV usano lo stesso snapshot; il CSV aggiunge q netto dopo
le colonne precedenti e mantiene la precisione completa.

## Evidenze eseguite

- Attesi razionali indipendenti creati prima del motore: sei campi,
  tredici quote della mensola di 6000 mm, EI=2e14 Nmm². Generatore
  `Checker/tools/ParatieDistributedExpected.py`. Copia immutata nella suite
  applicativa, SHA-256
  `1EBED08744C8BC3EC3CC0D0299EC9DA31F2ACDEA48B28D5664EE57636937B57C`.
- `distributed-kernel-04`: 58 test Geotechnics PASS, raffinamento
  1500/500/125 mm e incrementi 1/8/32; equivalenza elastoplastica del
  carico uniforme, input invalidi, memoria, rollback, annullamento e
  meccanismo. Tolleranze nodali: 5e-8 mm, 5e-11 rad, 2e-5 N e 0.05 Nmm,
  motivate dal condizionamento; equilibrio globale dei momenti 0.1 Nmm.
- Cattura di sei mensole uniformi e uno scavo prima/dopo identica byte per
  byte, SHA-256
  `7A404044A37A10BBBC5C40A0B9E8D271048808386639D1CF0B711C579A5C9EC3`.
  Nessun atteso storico o elenco di fallimenti modificato.
- `distributed-global-01`: 60 test Geotechnics PASS. Nuovi attesi razionali
  per forza e momento, due versi di valle, traslazione delle quote,
  risultante nulla, azioni dei conci, equivalenza alla soluzione uniforme
  e fattore normativo applicato una sola volta.
- `candidate-16`: 3148 PASS e 17 fallimenti storici Steel ammessi.
  `candidate-17`, con trasferimento globale: 3150 PASS, stessi 17 fallimenti
  ammessi e due test Model già ignorati; API compatibili. Model
  `80a5463b1e09df43f781f3406b0153c7c1f9be58`, Checker
  `924f6e7689f17d9b49a72f2aee0a9ed6f60e3989`. Model 5.14.0.0,
  Geotechnics 0.11.0.0; versioni e SHA-256 nel manifest. Staging integrale
  `snapshot-17`; `lib/Checker` invariato.
- `distributed-app-03`: 1206 controlli applicativi/MVVM PASS. Include
  tutti gli attesi indipendenti, fascia 2.5 m, migrazione, persistenza,
  rimozione/inversione, invalidazione, annullamento, isolamento concorrente,
  coefficienti globali, report e CSV. Mensola con carico 1→4 kN/m fra
  z=−1 e −4.5 m: u testa=0.9843932291666667 mm, V piede=8.75 kN,
  M piede=25.375 kNm. Fase senza carico: ritorno elastico a zero;
  carico invertito: stessi valori in segno opposto.
- `ci/20261019-135757-paratie-inc09-ui2`: build Release/UiTests,
  architettura e 39 controlli WPF PASS. Editor, diagramma chiaro/scuro,
  aggiornamento e invalidazione verificati. Schermate esaminate.
- Report `distributed-app-03/paratie/carichi-distribuiti.pdf`, tre pagine
  A4 renderizzate e controllate in `distributed-report-render-03`.
  Esponenti compattati senza azzerare residui, estratto comprensivo degli
  estremi di q. Il primo tentativo di formattazione ha prodotto un errore
  di firma delle callback, corretto prima della corsa conclusiva.
  Nessuna attestazione di rendering Word non eseguito.

Artefatti in `supporto/artefatti/paratie/`, salvo le corse CI indicate.
Regressione standard `ci/20261019-140055-paratie-inc09-standard` eseguita
su `8db299791c40e5861a88b465960db7e353c92338` con `snapshot-17`:
74 PASS, 0 NEW-FAIL; sorgenti invariati durante la corsa. Le tre suite
infrastrutturali che compilano direttamente Checker o misurano prestazioni
non sono incluse in questo profilo con snapshot candidato; le suite GPC
sono state eseguite dal produttore del set coerente.

## Ambito ancora aperto

Questa funzione assegna azioni alla struttura. Non implementa diffusione
di carichi sul terreno, Culmann, pressioni sismiche, interazione fra pareti
o combinazioni automatiche. Le righe PAR-12 e PAR-17 restano in corso.
Guide globali, funzioni residue, rilascio ufficiale e merge restano aperti.
Approfondimento pali/micropali confermato in coda al modulo paratie.
