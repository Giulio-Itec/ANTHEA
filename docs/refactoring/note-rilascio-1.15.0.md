# ANTHEA 1.15.0 — note di rilascio

Note del 28 ottobre 2026. La 1.15.0 aggiunge la verifica dei pilastri snelli con il metodo della colonna modello nella sezione c.a. e porta nel
modulo Modelli i file di testo di MIDAS Civil e Gen e i modelli Straus7. Porta le librerie GPC della release AL. Il testo è scritto per
chi usa ANTHEA ed è anche l'annotazione del tag `v1.15.0`, sul commit del setup. Il verbale tecnico del rilascio è in
[installer-1.15.0.md](installer-1.15.0.md).

## Novità nell'uso

- **Sezione c.a., scheda «08 Colonna modello».** Verifica di un pilastro isolato a sezione e sforzo normale costanti con il metodo della
  colonna modello (EN 1992-1-1 5.8.6(6) e 5.8.8.2, richiamato da NTC 2018 §4.1.2.3.9.3): dati della colonna (N, direzione del momento,
  M0Ed nella sezione critica, l0, l, fattore c), imperfezione geometrica ed eccentricità minima, viscosità con φef, «Calcola colonna» e
  «Interrompi». Due grafici: M(χ) con la retta del momento esterno e la tangente al massimo; M − M2 con la capacità del primo ordine
  M1Rd,max. Riquadri con snellezza, momento del primo ordine, viscosità, capacità e verifica; «Esporta CSV…»; capitolo «Colonna modello»
  nella relazione Word, con la scelta «Momento–curvatura e colonna modello».
  - «Calcola armature» diventa la scheda 09. I progetti salvati con le versioni precedenti si riaprono sulla stessa scheda; le opzioni
    della colonna modello entrano nel foglio solo alla prima modifica.
- **Modelli: file di testo MIDAS (.mct, .mgt).** «Apri file» legge i file di testo di MIDAS Civil e Gen: geometria, sezioni, spessori,
  terne, vincoli, molle, link, rilasci, carichi grafici, casi e combinazioni. Il file non contiene risultati. Restano fuori, con un
  messaggio, i muri e gli elementi solidi o a stato piano, le piastre con spessore non unico e i modelli che la libreria rifiuta.
- **Modelli: importazione da Straus7.** Con Straus7 R3 installato e la sua licenza: «Controlla file e casi» legge il modello `.st7` e i
  casi del file di risultati `.lsa`; si scelgono casi e contenuto e «Importa da Straus7» porta geometria, proprietà, vincoli, molle, link,
  rilasci e i risultati scelti. Le combinazioni lineari di Straus7 sono ricostruite dalla libreria. Lettura in sola lettura, in un
  processo separato: un'importazione annullata o non riuscita non cambia il documento. I momenti hanno il segno della copia MIDAS
  , come per tutte le sorgenti: Mxx, Myy e Mxy delle piastre e My delle aste sono opposti alla convenzione di Straus7.
- **Dominio 3D.** Le etichette degli assi stanno sulla carta anche nel tema Chiaro, come le etichette dei punti: si leggono anche quando
  cadono sulla superficie del dominio, nella scheda e nelle figure dei report.

## Calcoli

- **Colonna modello (GPCChecker.Concrete 0.27.0.1).** Curva M(χ) della sezione a N costante con i legami di progetto (calcestruzzo teso
  trascurato), M2 = k·χ con k = NEd·l0²/c, M1Rd,max = max(M − M2); verifica M0Ed,tot ≤ M1Rd,max e configurazione di equilibrio. Imperfezione
  di EN 1992-1-1 5.2(7)a con θ0 = 1/200 (o l0/400, NTC l/300, valore assegnato), eccentricità minima della norma come minimo del momento
  totale, viscosità con φef della (5.19). La curvatura nominale di EN 5.8.8 è mostrata solo come informazione. Gli scostamenti dalla
  norma sono dichiarati nella guida teorica (K-1…K-6).
- **Nessun risultato delle schede esistenti cambia**, neppure con le fasi della revisione delle librerie dell'acciaio
  (GPCChecker.Steel 2.124). Fondazioni superficiali: il risultato riporta la versione nuova della libreria.

Librerie GPC: release AL (Model 02027760, Checker 0951cbf9). Le differenze nei risultati di prova sono nel registro delle differenze
(CA-COLONNA-01, UI-CONTRASTO-02, B-FON-01, MOD-31, MOD-32, F5-REL-1150-01), approvate dall'utente.

## Guide

Guide pratica e teorica **Rev61**: scheda «Colonna modello» (uso, metodo, formule, limiti e scostamenti dichiarati), file di testo MIDAS
e importazione Straus7 nel modulo Modelli, copie dei modelli importati (unità e segni).

## Limiti noti

- Colonna modello: flessione retta nella direzione scelta, una sola sezione critica e sforzo normale costante; una sola azione per volta
  (tutte le combinazioni con l'esito peggiore e il dominio ridotto N–M1Rd,max in una versione successiva).
- Straus7: brick, fasi, masse, temperature e reazioni non sono importati; truss e cavi entrano nel modello ma non ancora nelle
  combinazioni.
- I documenti con un modello importato da Straus7 hanno un campo nuovo (le combinazioni della sorgente) e non si aprono con la
  1.14.
- Per il resto come nella 1.14.4.
