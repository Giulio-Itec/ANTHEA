# ANTHEA 1.13.0 — note di rilascio

Note del 23 ottobre 2026, approvate dall'utente con i limiti noti («Note di rilascio con i Limiti noti»). Il testo è scritto
per chi usa ANTHEA ed è anche l'annotazione del tag `v1.13.0`, sul commit e1d9976c del setup. Il verbale tecnico del rilascio è
in [installer-1.13.0.md](installer-1.13.0.md).

La 1.13.0 segue la 1.12.1, preparata il 21 ottobre e mai pubblicata: chi installa la 1.13.0 riceve anche le novità della 1.12.1
(capsula come pilastro, albero di lavoro delle paratie, guide Rev56).

## Novità nell'uso

- **Tab seleziona il contenuto.** In tutte le schede, Tab e Maiusc+Tab portano alla casella successiva o precedente e ne
  selezionano il contenuto: il valore digitato sostituisce quello presente. Con un clic il cursore resta dove si è cliccato.
  Le caselle su più righe e le tabelle mantengono il loro comportamento.
- **Pali, stratigrafia estesa.** Nel palo e nel micropalo, verticali e orizzontali, «Estendi» sulla scheda Stratigrafia
  allarga la tabella degli strati e lascia a destra il profilo stratigrafico con il palo, per tutta l'altezza. Vale anche per
  «Modifica dati e strati» della risposta elastica. Con la finestra stretta la stratigrafia occupa tutta la larghezza, come
  prima.
- **Palo verticale, peso specifico nullo.** «Peso specifico CLS / palo» può valere 0: il peso proprio non entra nelle azioni e
  il risultato lo segnala con un avviso. Prima il calcolo si fermava.

## Correzioni nei calcoli

Le correzioni vengono dalla revisione del modulo paratie del 21–22 ottobre e dalla release AF delle librerie GPC. Ogni
differenza nei risultati di prova è stata registrata e approvata dall'utente (PARATIE-19 e PARATIE-20).

- **Paratie, pretensione fra le fasi.** Una fase successiva che rielenca un tirante non lo rilascia più. Nella tabella delle
  presenze una cella vuota mantiene la pretensione della fase precedente; un valore la sostituisce e 0 la rilascia. Le
  riduzioni sono segnalate nel disegno e nei risultati. I documenti già salvati conservano i valori scritti. Prima la forza
  nel tirante poteva risultare sottostimata, a sfavore di sicurezza.
- **Paratie, tiranti e puntoni che si staccano.** Un sostegno che si scarica resta scarico e riprende carico solo quando la
  paratia torna al punto di contatto. Prima il sostegno «seguiva» la paratia e riprendeva carico troppo presto: in un caso di
  prova 15,65 kN e −2,03 mm invece di 7,83 kN e +0,78 mm.
- **Paratie, convergenza.** Meno falsi «non convergente». La ricerca lineare del solutore lavora sull'energia potenziale e la
  tolleranza tiene conto dei termini di flessione. Una paratia tirantata stabile e i casi con mesh fini o pareti rigide ora
  convergono.
- **Pali circolari in c.a., taglio.** Nelle paratie e nella risposta elastica dei pali orizzontali l'altezza utile è quella
  dell'anello continuo di barre, d = R + 2rs/π, con armatura tesa As/2. Prima dipendeva dall'arrotondamento della posizione
  delle barre vicine all'asse. Nei casi di prova VRd cresce dallo 0,35 all'1,9 % (palo D 800 con 12φ20: da 559,2 a 562,5 kN);
  cambiano anche i rapporti e le lunghezze della distinta che ne dipendono. La risposta elastica non cambia.
- **Stabilità globale con terreni non drenati.** Nei muri e nelle paratie, un concio con φ = 0 e sforzo normale efficace
  negativo non fa più scartare il cerchio: la sua resistenza è c·l. Prima molti cerchi restavano senza soluzione. Nel caso di
  prova dei muri in terreno non drenato con sisma le quattro combinazioni globali risultavano «Discretizzazione non
  convergente»; ora sono calcolate. Il risultato precedente poteva essere a sfavore di sicurezza.
- **Librerie GPC.** Release AF (Model 5.20, Concrete 0.18, Geotechnics 0.19, Steel 2.31, Design 0.35, CompositeBridge 1.12,
  Masonry 0.6). In ANTHEA la libreria dell'acciaio serve solo alle verifiche dei tubi delle paratie: le prove di ANTHEA non hanno
  registrato differenze dovute all'aggiornamento.

## Ambiente

- **Esportazione delle immagini.** ANTHEA non viene più chiuso dall'antivirus durante l'esportazione dei PNG: le immagini si
  copiano invece di essere rinominate.

## Guide

Guide pratica e teorica **Rev58**, in Word e PDF, installate con il programma. Oltre alle novità sopra, la guida teorica
descrive il taglio dei pali circolari con l'anello continuo, i sostegni che si scaricano, la ricerca lineare del solutore e i
conci con φ = 0. La Rev57, mai distribuita, aveva documentato la pretensione fra le fasi.

## Lavori interni senza effetti visibili

Il codice contiene le viste MVVM dei moduli (F5.5–F5.12) e le parti a–d della nuova finestra principale (F5.13), compresa
l'apertura dei fogli a pagina intera o in una nuova finestra. La Release apre ancora le viste storiche e la finestra principale
attuale: chi usa ANTHEA non vede differenze. Le viste nuove entreranno a fine refactoring, dopo la prova dell'utente.

## Limiti noti

- **Paratie, filtrazione di uno scavo simmetrico.** Il bordo laterale del dominio è sempre a carico imposto. Se come
  estensione si inserisce la semilarghezza di uno scavo simmetrico, l'asse fa da dreno e il gradiente lungo la parete risulta
  sottostimato, a sfavore di sicurezza. Le librerie hanno già il bordo a flusso nullo; il campo in ANTHEA non c'è ancora.
- **Paratie, coefficienti parziali.** Le storie marcate SLU non ricevono coefficienti parziali sulle azioni: vanno inserite già
  fattorizzate.
