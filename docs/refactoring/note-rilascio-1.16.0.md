# ANTHEA 1.16.0 — note di rilascio

Note del 29 ottobre 2026. La 1.16.0 completa l'importazione da Straus7 nel modulo Modelli, estende la colonna modello a tutte le
combinazioni, aggiunge Hansen con carico inclinato nelle fondazioni e il bordo a flusso nullo nella filtrazione delle paratie, rifà le
armature e la distinta dei muri a mensola e porta le combinazioni generate dalla libreria Model in muri e Bridge Design. Porta le librerie
GPC della release AM. Il testo è scritto per chi usa ANTHEA ed è anche l'annotazione del tag `v1.16.0`, sul commit del setup. Il verbale
tecnico del rilascio è in [installer-1.16.0.md](installer-1.16.0.md).

## Novità nell'uso

- **Modelli: Straus7.** Un file di risultati `.lsa` risolto senza fase attiva si legge come statico; con una fase attiva dà i casi
  statici di quella configurazione. Un `.nla` per fasi dà i risultati «NLA · fase · incremento», come quelli MIDAS, che non si
  combinano. Truss e cavi entrano nelle combinazioni; si importano reazioni (con il controllo di equilibrio), masse e temperature.
  Nelle tabelle del modulo Modelli le colonne con l'unità tra parentesi («X [m]») mostrano i valori.
- **Sezione c.a., «08 Colonna modello».** Tutte le combinazioni con l'esito peggiore e il dominio ridotto N–M1Rd,max.
- **Fondazioni: Hansen con carico inclinato**, drenato e non drenato (prima il caso era rifiutato). Il metodo si chiama «Brinch Hansen».
- **Paratie, bordi del dominio di filtrazione.** Nella filtrazione FEM 2D si sceglie per ciascun bordo laterale il carico imposto o il
  flusso nullo. Con metà di uno scavo simmetrico e il flusso nullo sull'asse, il gradiente lungo la parete non è più sottostimato. I file
  precedenti si aprono con il carico imposto su entrambi i bordi, come prima.
- **Muri a mensola: armature e distinta ferri come tavola.** «Distinta ferri…» apre la tavola del tratto di muro: sezione con tutte le
  posizioni numerate (pos. 1, 2, …) e i richiami «Ø16/16,7», ferri longitudinali disegnati come punti, riepilogo per diametro, peso
  totale, volume del calcestruzzo e incidenza in kg/m³; poi la distinta con le sagome quotate in cm, Ø, numero di pezzi, lunghezza
  unitaria e totale e peso. Stesse informazioni nel Word, nel PDF, nei PNG e nel CSV. Le armature sono corrette:
  - passo delle barre 1000/n, come nelle verifiche (prima il passo di una striscia isolata di 1 m: su 10 m di muro 56 barre invece
    di 61);
  - piedi delle barre del fusto incrociati nella fondazione (quelle tese di monte piegano verso valle, EN 1992-1-1 Allegato J), appoggiati
    sulla rete inferiore;
  - barre di fondazione uniche fra le due mensole quando hanno le stesse barre, con i risvolti ai bordi liberi; forcella in testa al
    fusto (EN 1992-1-1 §9.3.1.4);
  - spilli della giunzione con sagoma e sviluppo calcolati; angolo della piega della scarpa sulla sagoma;
  - ferri longitudinali nello strato interno, uno in ogni angolo, in pezzi uguali con la sovrapposizione oltre la barra commerciale.
  Nelle verifiche dei dettagli cambia il passo: il rapporto dell'interasse massimo scende, interferro e altezza per la piega diventano più
  severi; nessun esito cambia, salvo «ingombro coda» dove prima il piede usciva dal getto.
- **Calcoli in coda.** Sezione c.a. (domini, tensioni, M–χ, colonna modello, ricerca delle armature) e risposta elastica del palo
  orizzontale passano dal limitatore come gli altri moduli; il taglio della sezione c.a. si calcola sullo sfondo. Più calcoli insieme
  non rallentano l'interfaccia.
- **Palo orizzontale, metodo Stratificato.** Da «Info modello», «Apri riferimento» apre l'articolo «Capacità orizzontale del palo in
  terreno stratificato», installato anche nel menu Start alla voce Articoli.
- **Dominio 3D.** L'unità dei fattori della «Scala del dominio 3D» è «−», come gli altri fattori adimensionali.

## Calcoli

- **Muri, combinazioni.** Nel sisma e nelle combinazioni eccezionali le azioni variabili entrano solo se sfavorevoli (NTC 2018 §2.5.3):
  le righe sismiche ed eccezionali compaiono a gruppi, con e senza ciascun gruppo variabile.
- **Muri, documenti della prima versione.** Calcolati senza aprirli (per esempio nel calcolo del progetto) si aggiornano come
  all'apertura e usano le combinazioni della versione 2, con i loro nomi: nel sisma più righe e i γR della Tab. 7.11.III, quindi
  scorrimento e ribaltamento scendono fino a −9 % e −13 % dove governa una riga sismica. Aprendoli, i documenti con ψ1 > 0,7 sono validi.
  La nota del sisma con kh e kv assegnati dice i γR della Tab. 7.11.III, usati dalla 1.14.4.
- **Bridge Design.** Nei documenti nuovi i coefficienti SLU sono le combinazioni A1 dei ponti stradali della NTC 2018 Tab. 5.1.V
  generate dalla libreria Model: G1 1,35/1,00, G2 (finiture e barriere) 1,50/0, traffico 1,35/0, permanenti favorevoli nei minimi
  (momenti massimi dello schema 1 da +1 % a +2 %, a favore di sicurezza). I documenti salvati conservano i moltiplicatori del progetto e i
  risultati. Scelta «Coefficienti parziali SLU» in fondo alla scheda Traffico.
- **Sezione c.a. e dettagli (GPCChecker.Concrete 0.28).** DIN EN 1992-1-1: k5 = 0,65 per la precompressione nella combinazione
  caratteristica (prima 0,75, a sfavore). Torsione DS con l'efficienza della biella del DK NA (prima a sfavore). Staffe minime delle travi
  DS ρw,min = 0,063 √fck/fyk. Classe indicativa di XC3 C30/37 con EN e UNI (prima C25/30). Riferimenti normativi dei dettagli corretti.
  Ritiro MC2010 opzionale nell'apertura delle fessure.
- **Fondazioni, Hansen non drenato con H > 0.** La formula del manuale per la forma con carico inclinato vale zero per H → 0: la portanza
  scende appena compare una spinta orizzontale (esempio 2 × 3 m: 532 → 455 kPa). È a favore di sicurezza ed è dichiarato nella guida.
- **Librerie dell'acciaio e della sezione c.a.** I punti aperti della revisione Steel, le verifiche di esercizio delle aste e i
  collegamenti entrano nelle librerie (GPCChecker.Steel 2.126); i domini di rottura della sezione c.a. sono tenuti in memoria e calcolati
  una volta sola. Nessun risultato delle schede esistenti di ANTHEA cambia per questo. Fondazioni superficiali: il risultato riporta la
  versione nuova della libreria.

Librerie GPC: release AM (Model d0d76b51, Checker c301be7a, su origin/master). Le differenze nei risultati di prova sono nel registro
delle differenze e seguono la regola dell'utente «Approvati se spiegati»: voci R6, R7, R8, R14, R16, R17 e R20, COMB-D6, COMB-D7,
COMB-D7B, COMB-D8, COMB-D9, COMB-D10, COMB-D11, COMB-D11-EN16612, COMB-MURI-D5-NOTA, UPG-PSI0-01, CA-COLONNA-02, FON-HANSEN-01,
INFRA-LIMITATORE-01, MOD-33, PARATIE-21, R-MURI-01…09, UI-CONTRASTO-03, B-FON-01, F5-REL-1160-01 e F5-REL-1160-02.

## Guide

Guide pratica e teorica **Rev62** (111 e 189 pagine): formule in notazione matematica (1469 nella teorica), testi di tutte le novità
sopra (tavola e dettagli d'armatura dei muri, bordo a flusso nullo delle paratie, coefficienti dei ponti stradali di Bridge Design),
capitoli completati (metodo Stratificato, limiti di esercizio della sezione c.a., ancoraggi), ricerca della Wiki anche senza pedici (fck,
Nq, kh). Articolo «Capacità orizzontale del palo in terreno stratificato».

## Limiti noti

- Straus7: brick esclusi; un `.nla` senza fasi è rifiutato con un messaggio.
- Colonna modello: flessione retta nella direzione scelta, una sola sezione critica e sforzo normale costante.
- I file paratie salvati con la 1.16.0 (documento versione 12) non si aprono con le versioni precedenti.
- Per il resto come nella 1.15.0 (tolti «una sola azione per volta» della colonna modello e il bordo a carico imposto delle paratie).
