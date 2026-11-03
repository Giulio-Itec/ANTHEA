# ANTHEA 1.17.6 — note di rilascio

Note del 3 novembre 2026. La 1.17.6 corregge il calcolo in serie delle combinazioni sui modelli grandi: **Calcola tutte** e **Calcola
selezionate** si fermavano dopo poche combinazioni (7 su un modello MIDAS di 3447 piastre e 49 casi). Valori e revisioni non cambiano. È
una correzione, quindi aumenta l'ultimo componente del numero.

Contiene le librerie GPC della release AU e le guide Rev68, come la 1.17.5.

Il testo è scritto per chi usa ANTHEA ed è anche l'annotazione del tag `v1.17.6`, sul commit del setup. Il verbale tecnico del rilascio è
in [installer-1.17.6.md](installer-1.17.6.md).

## Combinazioni sui modelli grandi

La copia del modello con le combinazioni calcolate doveva stare in 256 MiB, il limite di un pacchetto del modello nel documento. Su un
modello di 3447 piastre e 49 casi i soli casi base occupano circa 165 MiB, una combinazione lineare circa 6,5 MiB e un inviluppo circa
97 MiB: dopo poche combinazioni la serie si fermava con «Limite del pacchetto del modello (256 MiB) raggiunto».

Dalla 1.17.4 i risultati delle copie grandi vanno nell'archivio accanto al progetto, un blocco per ogni risultato. Il limite di 256 MiB
vale ora per ogni blocco; la copia con le sue combinazioni può arrivare a 2 GiB. Oltre, come prima, escono dalla cache le combinazioni
calcolate per prime, e la serie si ferma se dovrebbe togliere le sue.

Su un modello di prova della stessa taglia (3456 piastre, 3757 nodi, 49 casi), «Calcola selezionate» di 61 combinazioni prima si
fermava al primo inviluppo; ora le calcola tutte e 61 in 41 s, compresa la preparazione dei casi base (circa 23 s), con una copia di
657 MiB. Una combinazione lineare costa circa 0,3 s, un inviluppo circa 1-2 s.

- La copia resta in memoria finché il modello è aperto: circa metà della sua dimensione.
- Nel progetto salvato i risultati sono compressi, circa un quinto.
- Il limite di 2 GiB è provvisorio. Calcolare le combinazioni solo quando servono, invece di salvarle tutte, è una decisione ancora aperta
  (D4 del piano).

## Librerie

Release AU, come la 1.17.5: GPCModel 5.38.0.0 (Model `2f934f9f`) e Checker `6156e4ce`.

## Guide

Guide pratica e teorica Rev68, come la 1.17.5.

## Limiti noti

- La full di rilascio non è stata eseguita su questo commit, per richiesta dell'utente: è prevista dopo la prova della versione.
- Gli inviluppi con più di 4096 stati alternativi restano sul calcolo per termini.
- Restano quelli della 1.17.5.
