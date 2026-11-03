# ANTHEA 1.17.5 — note di rilascio

Note del 3 novembre 2026. La 1.17.5 rende molto più veloce il calcolo in serie delle combinazioni del modulo Modelli: **Calcola tutte** e
**Calcola selezionate**. Valori, governanti, componenti principali e revisioni non cambiano: sono uguali bit per bit a quelli della
1.17.4. Non ci sono funzioni nuove, quindi il numero segue la regola delle correzioni.

Contiene le librerie GPC della release AU e le guide Rev68, come la 1.17.4.

Il testo è scritto per chi usa ANTHEA ed è anche l'annotazione del tag `v1.17.5`, sul commit del setup. Il verbale tecnico del rilascio è
in [installer-1.17.5.md](installer-1.17.5.md); il dettaglio del lavoro in
[modelli-motore-combinazioni](../sviluppo/modelli-motore-combinazioni.md), sezione «La serie dopo la 1.17.4».

## Combinazioni in serie

Con il motore a matrici della 1.17.4 la somma dei casi base costa pochi millisecondi. In una serie, il resto del tempo andava in tre passi
che ANTHEA ripeteva per ogni combinazione su dati che non erano cambiati:
- il controllo dell'intera copia del modello, due volte;
- la misura del pacchetto del modello per il limite di 256 MiB, che rileggeva tutte le combinazioni già calcolate;
- la revisione di ogni definizione già calcolata.

La misura del pacchetto cresceva con la serie: ogni combinazione era più lenta della precedente. Ora ciascuno di questi passi si fa una
volta sola per ogni dato.

Tempi su un modello di prova con 500 elementi, 472 nodi, 100 casi statici e 951 definizioni NTC 2018:

| Operazione | 1.17.4 | 1.17.5 |
| --- | ---: | ---: |
| **Calcola selezionate**, 120 combinazioni | 98,6 s | 4,8 s |
| Una combinazione nella serie | da 0,5 a oltre 1 s, crescente | 40 ms, costante |
| Una combinazione lineare, calcolo e inserimento nel modello | 0,16 s | 0,02 s |
| Inviluppo SLU, calcolo e inserimento nel modello | 0,51 s | 0,29 s |
| Salvataggio di una definizione | 0,14 s | 0,01 s |

- La prima combinazione dopo l'apertura del modello prepara i casi base e dura come prima: alcuni secondi sul modello di prova.
- Restano il salvataggio nel progetto ogni 20 secondi e alla fine della serie, e il limite del pacchetto del modello (256 MiB): una serie
  che lo raggiunge si ferma e lo dice, come prima.

## Librerie

Release AU, come la 1.17.4: GPCModel 5.38.0.0 (Model `2f934f9f`) e Checker `6156e4ce`. Le versioni delle DLL sono in
`lib/Checker/README.md`.

## Guide

Guide pratica e teorica Rev68, come la 1.17.4. Non descrivono ancora gli spessori nella finestra delle sezioni e il calcolo in serie delle
combinazioni (1.17.2), né il calcolo all'apertura e il «?» dei campi Azioni (1.17.3).

## Limiti noti

- Gli inviluppi con più di 4096 stati alternativi restano sul calcolo per termini della 1.17.3.
- Un inviluppo viene ancora calcolato dai casi base anche quando le combinazioni che contiene sono già calcolate.
- Restano quelli della 1.17.4.
