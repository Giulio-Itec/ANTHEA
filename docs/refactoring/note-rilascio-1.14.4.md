# ANTHEA 1.14.4 — note di rilascio

Note del 28 ottobre 2026. La 1.14.4 chiude i lavori decisi il 26 ottobre rimasti dopo la 1.14.3: combinazioni dei muri generate dalla
libreria Model, tension stiffening del momento–curvatura, ramo incrudente dell'acciaio di progetto (R18), anello interno nei report del
palo orizzontale, contrasto del tema Chiaro e guide Rev60. Porta le librerie GPC della release AK. Il testo è scritto per chi usa ANTHEA
ed è anche l'annotazione del tag `v1.14.4`, sul commit del setup. Il verbale tecnico del rilascio è in
[installer-1.14.4.md](installer-1.14.4.md).

## Novità nell'uso

- **Tema Chiaro più leggibile.** Il grigio dei testi secondari passa da #64748B a #5B697E e quello dei comandi disattivati da #808080 a
  #767676 (contrasto almeno 4,5:1 sul fondo). Vale anche nei disegni tecnici e nelle figure dei report. Il tema Scuro non cambia.
- **Momento–curvatura, «Tension stiffening».** L'opzione ora applica il modello del tirante alle barre tese (β = 0,4, carichi di lunga
  durata): il calcestruzzo teso non lavora dopo la fessurazione, le barre nell'area efficace hanno la deformazione media irrigidita,
  le barre fuori dall'area efficace restano nude. Lo snervamento si legge alla barra nella fessura; il punto limite viene dal dominio,
  senza tirante. Lo stato della scheda lo dice.
- **Palo orizzontale.** Nei report di Broms la frase dopo la tabella della sezione riporta il secondo anello interno di diametro diverso;
  nel report della risposta elastica la riga dell'anello è nell'armatura del tratto. Il nome del modello di Broms è calcolato con la
  profondità della testa sotto il piano campagna, come il calcolo.

## Calcoli

- **Muri di sostegno: combinazioni dalla libreria Model.** Le combinazioni dei muri e i coefficienti della stabilità globale sono
  generati dal generatore delle combinazioni geotecniche di Model (NTC 2018 §2.5.3). Con le due decisioni del 28/10:
  - G2 favorevole 0,8 nelle combinazioni SLU (NTC 2018 Tab. 2.6.I e 6.2.I), prima 0; nella stabilità globale γG2 = 0,8/1,3;
  - nelle combinazioni sismiche con kh e kv assegnati i γR della Tab. 7.11.III (1; 1; 1,2), come per quelle dal sito, invece degli
    statici R3 (1,1; 1,15; 1,4); l'approccio si chiama «Sismica · NTC 7.11.III».
  Gli esiti possono cambiare: nei casi di prova aumentano i controlli non soddisfatti quando G2 è sfavorevole, e le resistenze sismiche
  crescono; in un caso la combinazione che governa lo scorrimento passa dal sisma all'urto di un veicolo.
- **Acciaio di progetto con incrudimento (R18).** Oltre εyd il ramo di progetto va da fyd in εyd a k·fyk/γs in εu (EN 1992-1-1 3.2.7(2)
  e Fig. 3.8; 3.3.6(7) per i trefoli), invece della curva caratteristica abbassata di fyk − fyd. Riguarda solo gli acciai con ramo
  incrudente: nei casi di prova la resistenza a trazione scende dello 0,55 % e i momenti resistenti dallo 0,5 al 2 %, a favore di
  sicurezza. Gli acciai con ramo orizzontale non cambiano.
- **Fondazioni superficiali.** Il risultato riporta la versione nuova della libreria; nessun numero cambia.

Librerie GPC: release AK (Model 8c0499ae, Checker 66b33d08), con la revisione delle librerie Steel del 27/10. Le differenze nei
risultati di prova sono nel registro delle differenze (COMB-MURI-D4D5, R18, BUG-TS-2, UI-CONTRASTO-01, PO-ANELLO-01, AK-FON-01,
AK-LIB-01), approvate dall'utente.

## Guide

Guide pratica e teorica **Rev60**: menu «Aspetto › Separatore decimale», profondità della testa dei pali e dei micropali, secondo anello
nei report, sviluppo parziale delle barre «MRd con fyd·r», tension stiffening con il modello del tirante, SLE non lineare con le leggi di
progetto (R5), correzioni di calcolo della 1.14.2–1.14.4 (R4, R9, R11, BUG-MUR-2b, BUG-FES-1b/1c, BUG-PO-5b, R18) e combinazioni dei
muri (D4 b, D5 b).

## Limiti noti

Come nella 1.14.3, tranne il nome del modello di Broms con la testa interrata, ora calcolato dalla libreria.
