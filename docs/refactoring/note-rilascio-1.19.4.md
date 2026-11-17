# ANTHEA 1.19.4 - note di rilascio

Note provvisorie del 18 novembre 2026, preparate con la candidata (parte 1) per la distribuzione dopo l'approvazione dell'utente. Descrivono
solo le differenze dalla 1.19.3. Le librerie sono quelle candidate del set AZ: i numeri definitivi arrivano con il set ufficiale AZ e la
parte 2 (vincoli e molle per freedom case di GPC.Model).

La 1.19.4 porta le decisioni del 17/11 sulla scheda del 16/11 e sul separatore decimale. Comprende:
- le paratie con la conferma della storia di progetto per le verifiche SLU, la fase nuova che riprende quote e falda, l'avviso della prima
  fase non geostatica e il passo delle barre della faccia tesa nella fessurazione delle fasce;
- il palo orizzontale con la Hu di Broms accanto a quella dello Stratificato;
- il separatore decimale scelto in tutte le viste e il report delle paratie con i decimali per unità;
- le librerie GPC del set AZ;
- le guide Rev75.

Il documento delle paratie passa alla versione 13: un file salvato con la 1.19.4 non si apre con le versioni precedenti, che rifiutano le
versioni future del documento. Non ci sono comandi nuovi né un formato nuovo dell'archivio.

## Paratie

- **Storia di progetto per le verifiche SLU (A2).** Campo nuovo «Storia delle fasi già di progetto per le verifiche SLU», per la storia
  principale e per ogni caso. Il servizio non aggiunge coefficienti parziali né alle azioni né agli effetti: senza la conferma la fascia
  in c.a. SLU non si verifica e pali circolari, tubi CHS e sostegni metallici SLU restano «Non valutato», con il caso nel messaggio; il
  resto del calcolo prosegue. SLE, sfilamento dei tiranti (con la sua conferma A1 e M1), UPL/HYD e stabilità globale non cambiano.
  I file salvati prima si aprono con la conferma da dare; la relazione riporta l'avvertenza sotto le azioni di ogni storia.
- **Fase nuova (A3).** «Aggiungi» delle fasi riprende terreno e falda, a sinistra e a destra, dall'ultima fase dello stesso caso;
  sovraccarichi, carichi e rigidezza ripartono dai valori iniziali. Un solo passo dello storico, che «Annulla» toglie per intero.
- **Prima fase non geostatica (A4).** Quando le spinte a riposo e l'acqua dei due lati della prima fase non si equilibrano, il foglio,
  gli inviluppi e la relazione lo dicono con lo scarto massimo e la sua quota. Il calcolo non cambia.
- **Fessurazione delle fasce (S3).** Il passo delle barre è quello della faccia tesa, combinazione per combinazione (GPC.Design 0.47).
  Cambia solo con armature diverse sulle due facce.
- **Esempi 04, 05 e 06** di distribuzione con la storia confermata, e la loro provenienza nel catalogo.
- **Report.** Ogni numero con i decimali della sua unità, senza esponenti e senza «-0»; m/s e N/mm³ come potenze di dieci; aree e
  momenti d'inerzia senza decimali; «Dati assegnati» con tutte le cifre degli ingressi e la virgola. I CSV non cambiano.

## Palo orizzontale

- **Stratificato con Broms accanto.** Broms resta il metodo predefinito. Con lo Stratificato, quando governa un palo corto o intermedio in
  terreno granulare, la sintesi del foglio, i dettagli della verticale e il report mostrano anche Hu di Broms per lo stesso palo e il
  rapporto fra le due capacità. La capacità adottata e la verifica restano quelle dello Stratificato.

## Separatore decimale

- I numeri delle viste (risultati delle paratie, tabelle e revisioni del visore dei modelli e gli altri binding) seguono il separatore
  scelto in Aspetto; prima erano sempre con il punto.
- Un numero salvato come numero, per esempio negli esempi, si mostra nei campi con il separatore scelto, senza arrotondamenti e senza
  cambiare il dato.
- Restano come prima le caselle numeriche del visore dei modelli e i disegni e i report che scrivono sempre la virgola: sono passi a parte.

## Librerie

Snapshot candidato del set AZ, al posto del set ufficiale AY della 1.19.3:
- GPCChecker.Concrete 0.39: R16 della torsione nel profilo danese con la variante (b2) e il limite ν ≥ 2/√fck, fori eccentrici rifiutati e
  foro ad anello; nessun effetto su ANTHEA, che fa la torsione con NTC e usa fori concentrici;
- GPCChecker.Geotechnics 0.30: Hu di Broms accanto allo Stratificato e avviso della prima fase non geostatica;
- GPC.Design 0.47: passo della faccia tesa nelle fasce;
- GPCChecker.Steel 2.136: basi di colonna e giunti tubolari facoltativi e V1 ratificato, che ANTHEA non usa;
- le altre DLL ricompilate.

L'impronta dei documenti delle paratie contiene le versioni delle librerie: con il set nuovo i risultati salvati risultano superati, come
a ogni set, e vanno ricalcolati.

## Guide

Guide globali Rev75, in Markdown e nella Wiki: paratie (A2, A3, A4, S3 e cifre del report), palo orizzontale (Broms accanto allo
Stratificato), separatore decimale e torsione del profilo danese. Le edizioni Word e PDF seguono.
