# ANTHEA 1.16.1 — note di rilascio

Note del 29 ottobre 2026. La 1.16.1 rende più veloce il calcolo delle combinazioni nel modulo Modelli e corregge tre difetti delle
combinazioni dell'utente. I risultati sono identici, bit per bit, a quelli della 1.16.0. Porta le librerie GPC della release AN. Il testo
è scritto per chi usa ANTHEA ed è anche l'annotazione del tag `v1.16.1`, sul commit del setup. Il verbale tecnico del rilascio è in
[installer-1.16.1.md](installer-1.16.1.md).

## Novità nell'uso

- **Modelli, combinazioni più veloci.** Le misure sono su un modello MIDAS di 100 elementi e 50 casi di carico, con le 867 combinazioni
  NTC 2018 generate, e su uno di 500 elementi e 100 casi. Sono mediane di 7 corse in Release:

  | Operazione | 100 × 50, 1.16.0 | 100 × 50, 1.16.1 | 500 × 100, 1.16.0 | 500 × 100, 1.16.1 |
  | --- | ---: | ---: | ---: | ---: |
  | Una combinazione lineare, modello già preparato | 0,40 s | 0,04 s | 3,3 s | 0,27 s |
  | Inviluppo delle 434 combinazioni SLU | 3,0 s | 0,28 s | 27,6 s | 2,0 s |
  | «Calcola» dal modulo, prima volta | 1,5 s | 0,8 s | 13,5 s | 8,7 s |
  | Apertura del gestore delle combinazioni | 0,18 s | 0,02 s | 1,0 s | 0,03 s |
  | Salvataggio del progetto | 0,33 s | 0,23 s | 2,2 s | 1,3 s |
  | Prova completa | 17,7 s | 5,7 s | 151 s | 37 s |

- **Preparazione anticipata.** Quando si apre il catalogo delle combinazioni, ANTHEA prepara il modello di calcolo in background, mentre si
  sceglie la combinazione. Così la prima combinazione parte prima.

## Correzioni

- **Inviluppo con lo stesso caso due volte (±Q, ±E).**
  - Prima: un inviluppo con Q × 1 e Q × −1, il modo abituale di scrivere ±Q, era rifiutato al calcolo, anche se il catalogo lo dava
    «Da calcolare».
  - Ora: ogni termine è un'alternativa e l'inviluppo dà −|Q| e +|Q|. Nella tabella dei governanti i termini ripetuti sono distinti
    («±Q=Q» e «±Q=Q #2»). Le etichette degli inviluppi senza ripetizioni non cambiano.
- **Combinazioni con più di 4096 stati alternativi.** Una somma lineare di inviluppi ha il prodotto delle loro alternative: due inviluppi
  di 65 termini ne danno 4225.
  - Prima: la combinazione era mostrata «Da calcolare» e il calcolo falliva con un messaggio tecnico.
  - Ora: è «Non disponibile», con il numero degli stati e il rimedio (un inviluppo delle combinazioni).
- **Combinazioni con tutti i coefficienti nulli.**
  - Prima: erano mostrate «Da calcolare» e il calcolo falliva con un messaggio tecnico.
  - Ora: sono «Vuota». Quando i termini si annullano fra loro (Q × 1 e Q × −1 in una somma), il calcolo dice quali sono.

## Calcoli

Nessun risultato cambia. Le librerie GPC sono della release AN (Model 9f40d98a, Checker bc2b3435, su origin/master):
- GPCModel 5.28 tiene, fra una combinazione e l'altra, i casi statici già abbinati, controllati e ruotati;
- GPC.Converter 2.10 legge una volta sola ogni tabella dei risultati MIDAS;
- le altre librerie sono ricompilate senza modifiche al sorgente.

Fondazioni superficiali: il risultato riporta la versione nuova della libreria (GPCChecker.Geotechnics 0.27.0.1).

Le differenze nei risultati di prova sono nel registro delle differenze: COMBO-B1, COMBO-B2, COMBO-B3 e il seguito AN di B-FON-01. Seguono
la regola dell'utente «Approvati se spiegati».

## Guide

Guide pratica e teorica **Rev62**, come nella 1.16.0. Il testo sulle combinazioni (±Q, «Vuota», «Non disponibile», preparazione
anticipata) entra nella prossima revisione delle guide.

## Limiti noti

- **Scrittura del risultato.** Dopo ogni calcolo di una combinazione l'interfaccia si ferma mentre il risultato viene scritto nel
  documento: circa 0,15 s sul modello 100 × 50 e 1,4 s su quello 500 × 100.
- **Risultati scartati.** Salvare o eliminare una qualunque definizione scarta tutti i risultati delle combinazioni già calcolati, che vanno
  ricalcolati.
- **Prima combinazione.** La preparazione della prima combinazione resta lenta sui modelli grandi: circa 9 s sul 500 × 100. La
  preparazione anticipata ne nasconde una parte.
- **Limiti della 1.16.0.** Restano quelli della 1.16.0:
  - Straus7 senza brick;
  - colonna modello in flessione retta;
  - file paratie della versione 12 non leggibili dalle versioni precedenti.
