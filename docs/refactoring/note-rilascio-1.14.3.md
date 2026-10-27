# ANTHEA 1.14.3 — note di rilascio

Note del 27 ottobre 2026. La 1.14.3 porta in ANTHEA due lavori sui pali decisi il 26 ottobre: la testa del palo sotto il piano
campagna e lo sviluppo parziale delle barre del palo elastico. Le librerie GPC restano quelle della 1.14.2 (release AJ). Gli altri
lavori decisi il 26/10 (combinazioni in Model, anello interno nei report, tension stiffening, R18, contrasto del tema Chiaro, guida
Rev60) passano alla 1.14.4 («solo i pali»). Il testo è scritto per chi usa ANTHEA ed è anche l'annotazione del tag `v1.14.3`, sul
commit del setup. Il verbale tecnico del rilascio è in [installer-1.14.3.md](installer-1.14.3.md).

## Novità nell'uso

- **Testa del palo sotto il piano campagna.** Nei dati generali del palo e del micropalo verticale e orizzontale c'è il campo nuovo
  «Profondità della testa sotto il piano campagna» (d, in metri). Vuoto o 0 significa testa al piano campagna, come prima: i fogli
  esistenti danno gli stessi risultati. Con d > 0 la stratigrafia resta riferita al piano campagna e deve arrivare almeno a d + L;
  il terreno sopra la testa dà le tensioni verticali (con la falda) ma nessuna resistenza laterale; quote, eccentricità e diagrammi
  partono dalla testa. Vale per il palo e il micropalo verticale, per il palo orizzontale con Broms, Stratificato e risposta
  elastica e per il micropalo orizzontale. Il risultato riporta d, un avviso e una nota nel report; i disegni indicano la profondità
  della testa. Una testa sotto il piano campagna con un tratto libero sopra il terreno è rifiutata: va azzerato uno dei
  due dati.

## Calcoli

- **Palo elastico, sviluppo parziale delle barre (BUG-PO-5b).** Vicino alle estremità delle barre, dove la lunghezza ancorata oltre
  a_l è minore di lbd, il momento resistente usa fyd·r, con r la lunghezza ancorata su lbd arrotondata per difetto a 0,05. Prima
  queste sezioni restavano «da completare». Vista e report scrivono «MRd con fyd·r» invece di «MRd nominale» dove r c'è; un tratto
  che diventa non soddisfatto lo dice nello stato. Sotto r = 0,05 MRd resta non utilizzabile. La sezione critica può spostarsi
  verso le estremità delle barre: nell'esempio a strato unico passa da x = 3,97 m (rapporto 0,22) a x = 0,94 m (rapporto 1,19).

Librerie GPC: release AJ, le stesse della 1.14.2 (Model 2acc60ef, Checker 4a1b0090). Le differenze nei risultati di prova sono nel
registro delle differenze (BUG-PO-5b, PO-TESTA-01), approvate dall'utente.

## Guide

Guide pratica e teorica **Rev59**, le stesse della 1.14.2. Il campo della testa del palo, il sottomenu «Separatore decimale» e le
correzioni di calcolo saranno nella Rev60.

## Limiti noti

Come nella 1.14.2. Per la testa del palo sotto il piano campagna: nel palo orizzontale il nome del modello di Broms è calcolato
sulla verticale vista dalla testa (stesso modello del calcolo), in attesa della funzione della libreria con la profondità della
testa.
