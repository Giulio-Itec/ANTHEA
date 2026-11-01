# ANTHEA 1.17.0 — note di rilascio

Note del 1 novembre 2026. La 1.17.0 completa le combinazioni del modulo Modelli:
- un pannello per generare le combinazioni NTC 2018;
- le componenti principali dei plate;
- il calcolo delle combinazioni molto grandi;
- un'interfaccia che resta libera mentre i risultati vengono scritti.

Nella sezione in c.a. la scheda «Momento–curvatura» mostra la duttilità in curvatura μφ. I risultati delle combinazioni esistenti sono
identici a quelli della 1.16.0. Porta le librerie GPC della release AQ e le guide Rev65.

Il testo è scritto per chi usa ANTHEA ed è anche l'annotazione del tag `v1.17.0`, sul commit del setup. Il verbale tecnico del rilascio è
in [installer-1.17.0.md](installer-1.17.0.md).

## Novità nell'uso

- **Modelli, pannello «Genera NTC 2018…».** Nel gestore delle combinazioni si assegnano a ogni caso statico:
  - la natura;
  - la categoria della Tab. 2.5.I;
  - il gruppo;
  - per i casi sismici, la direzione.

  Il pannello mostra quante combinazioni nascono per ogni stato limite prima di generarle. Funziona così:
  - **Carichi MIDAS «L».** Non hanno una categoria predefinita: la generazione parte solo quando è scelta.
  - **Casi sismici con direzione.** Vale la regola 100/30 di NTC 2018 §7.3.5: 1,00 in una direzione e 0,30 nelle altre, con tutti i
    segni. Senza direzione un caso sismico entra da solo con +1 e −1, come prima.
  - **Limiti.** Oltre 4096 combinazioni o 12 gruppi la generazione non parte e il pannello dice come rientrare: riunire i casi correlati
    nello stesso gruppo o escludere quelli che non servono.
  - **Generate e create dall'utente.** La nuova generazione sostituisce le combinazioni generate in precedenza e conserva quelle create
    dall'utente.
  - **Scelte salvate.** Restano nel modello.
- **Modelli, componenti principali dei plate.** Le combinazioni calcolate hanno anche Mmax, Mmin, Fmax e Fmin dei plate, nel selettore
  «Principali», nel contouring e nelle tabelle.
  - **Combinazione lineare:** principali dello stato combinato.
  - **Inviluppo:** principali di ogni stato simultaneo, con l'estremo, la combinazione che lo governa e il suo stato concomitante.

  Non sono mai la somma delle principali dei casi.
- **Modelli, combinazioni molto grandi.** Una somma di inviluppi può avere più di 4096 stati alternativi.
  - **Prima:** era «Non disponibile».
  - **Ora:** si calcola cercando gli estremi termine per termine. Sotto il limite il calcolo è lo stesso di prima, bit per bit.
- **Modelli, interfaccia libera.**
  - **Scrittura dei risultati.** Avviene in background. Nella 1.16 la finestra si fermava circa 1,4 s sul modello 500 × 100 dopo ogni
    calcolo.
  - **Modifica di una definizione.** Salvarla o eliminarla scarta solo i risultati che dipendono da quella definizione. Prima scartava
    tutti i risultati, inviluppi compresi.
- **Modelli, tempi.** Misure su un modello MIDAS di 100 elementi e 50 casi di carico, con le combinazioni NTC 2018 generate, e su uno
  di 500 elementi e 100 casi. Sono mediane in Release a macchina ferma, con le principali calcolate:

  | Operazione | 100 × 50, 1.16.2 | 100 × 50, 1.17.0 | 500 × 100, 1.16.2 | 500 × 100, 1.17.0 |
  | --- | ---: | ---: | ---: | ---: |
  | Prima combinazione, con la preparazione del modello | 1,09 s | 0,64 s | 9,9 s | 5,9 s |
  | Una combinazione lineare, modello già preparato | 0,04 s | 0,03 s | 0,27 s | 0,30 s |
  | Inviluppo delle combinazioni SLU | 0,28 s | 0,24 s | 2,0 s | 3,3 s |
  | «Calcola» dal modulo, prima volta | 0,83 s | 0,36 s | 8,7 s | 4,7 s |
  | Interfaccia ferma per scrivere il risultato | circa 0,15 s | 0 | circa 1,4 s | 0 |
  | Salvataggio di una definizione (in background nella 1.17.0) | 0,17 s | 0,51 s | 0,96 s | 3,4 s |
  | Salvataggio / riapertura del progetto | 0,23 / 0,58 s | 0,21 / 0,36 s | 1,3 / 2,5 s | 1,5 / 2,2 s |
  | Prova completa | 5,7 s | 5,1 s | 36,6 s | 36,0 s |

  Le colonne 1.16.2 sono la campagna della notte del 29/10, con lo stesso codice delle combinazioni della 1.16.1 e della 1.16.2.

  Le principali dei plate hanno un costo. Sul 500 × 100 l'inviluppo SLU sale da 2,0 a 3,3 s, perché calcola le principali di ogni
  stato simultaneo. Il salvataggio di una definizione sale da 1,0 a 3,4 s, ma ora avviene in background e conserva i risultati non
  toccati, quindi dopo una modifica non si ricalcola più nessun inviluppo. Sul modello 1000 × 100 la prima combinazione richiede circa
  12 s e l'inviluppo SLU 6,6 s.

- **Sezione in c.a., scheda «Momento–curvatura».** Oltre alla curva mostra la duttilità in curvatura μφ di NTC 2018 §4.1.2.3.4.2, con la
  bilineare della Circolare 2019 (Fig. C4.1.12). La scheda riporta:
  - primo snervamento convenzionale M'yd e φ'yd;
  - MRd del punto limite e φyd = (MRd/M'yd) φ'yd;
  - Mmax e φu, la curvatura a cui il momento scende a 0,85 Mmax dopo il picco, o quella del punto limite;
  - μφ = φu/φyd.

  Il calcolo richiede il ramo a curvatura controllata alla stessa N, per cui la scheda impiega un po' di più. Con «CLS teso nelle risposte»
  non trascurato la duttilità non è calcolata e la scheda dice perché. Il report non cambia.

## Correzioni

- **Definizione importata rinominata come un'altra** (COMBO-B4).
  - Prima: una definizione importata da MIDAS, modificata e rinominata con il nome di un'altra della stessa tabella, era accettata e il
    calcolo poi falliva.
  - Ora: il salvataggio rifiuta il nome doppio e ne spiega il motivo.

## Calcoli

- **Combinazioni.** I risultati esistenti non cambiano: le impronte dei valori dei modelli di prova sono quelle della 1.16.0. I risultati
  hanno in più le principali dei plate.
- **Combinazioni calcolate con le versioni precedenti, fino alla 1.16.2.**
  - **Copia del modello invariata dopo il calcolo:** restano «Calcolate» con i risultati di allora, identici. Non hanno le principali
    dei plate: chiedendole, il comando «Ricalcola con le componenti principali» le aggiunge.
  - **Copia cambiata dopo il calcolo:** le combinazioni compaiono «Da calcolare» e si ricalcolano una volta.
  - **Formato del progetto:** il progetto passa al nuovo formato la prima volta che la copia viene scritta.
- **Librerie GPC della release AQ** (Model f4f78752, Checker eb16e72c, su origin/master):
  - **GPCModel 5.34:** regola 100/30, principali dei plate e somme di inviluppi per termini.
  - **GPC.Converter 2.12:** preparazione dei risultati MIDAS in parallelo e albero delle combinazioni per i calcoli per termini.
  - **GPCChecker.Concrete 0.35:**
    - μφ;
    - nella famiglia Eurocodice, EN 1992-1-1 8.7.2(3): la sovrapposizione si allunga dell'intero interferro quando supera 4Ø o 50 mm;
    - controlli EN 8.7.2(4) sulla percentuale giuntata e EN 8.8 sulle barre oltre 32 mm, in sospeso quando mancano i dati.

    ANTHEA usa il profilo NTC 2018 per ancoraggi e dettagli, quindi questi risultati non cambiano.
  - **Altre librerie:** ricompilate.
- **Registro delle differenze.** Le differenze nei risultati di prova sono registrate e seguono la regola dell'utente «Approvati se
  spiegati»: COMBO-REV-01, COMBO-B4, COMBO-GEN-01, COMBO-PRIN-01, COMBO-B2-bis e CA-METODI-01, 02 e 03.

## Guide

Guide pratica e teorica **Rev65**:
- generatore, principali, combinazioni molto grandi e interfaccia libera del modulo Modelli;
- μφ nella scheda «Momento–curvatura»;
- testi sulle combinazioni rimandati dalla 1.16.1.

## Limiti noti

- **Report.** Principali dei plate e μφ non entrano nei report di calcolo, che saranno rivisti a parte.
- **Combinazioni con più di 4096 stati.** Le principali dei plate non sono disponibili, perché richiedono ogni stato.
- **Dimensione del progetto.** Un modello grande pesa nel documento: circa 100 MB sul 500 × 100. Un archivio dei risultati a blocchi
  accanto al documento è in progetto.
- **Prima combinazione.** La preparazione della prima combinazione resta la parte lenta sui modelli grandi: circa 6 s sul
  500 × 100 e 12 s sul 1000 × 100.
- **Principali e inviluppi.** Le principali rallentano gli inviluppi: sul 500 × 100 l'SLU passa da 2,0 a 3,3 s.
- **Limiti della 1.16.** Restano:
  - Straus7 senza brick;
  - colonna modello in flessione retta;
  - file paratie della versione 12 non leggibili dalle versioni precedenti.
