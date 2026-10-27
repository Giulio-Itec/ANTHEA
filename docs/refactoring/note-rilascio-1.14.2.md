# ANTHEA 1.14.2 — note di rilascio

Note del 27 ottobre 2026. La 1.14.2 sostituisce la 1.14.1, mai pubblicata: ne contiene tutte le novità e porta le librerie GPC
nuove di Model e Checker, con le prime correzioni di calcolo decise il 26 ottobre. I numeri sono quelli scelti dall'utente
(«versione 1.14.2 e 1.14.3 dopo correzioni»): le correzioni di ANTHEA, la guida Rev60 e gli altri lavori decisi il 26/10 arrivano
con la 1.14.3. Il testo è scritto per chi usa ANTHEA ed è anche l'annotazione del tag `v1.14.2`, sul commit del setup. Il verbale tecnico del rilascio è in [installer-1.14.2.md](installer-1.14.2.md).

## Novità nell'uso

Le stesse della 1.14.1 ([note della 1.14.1](note-rilascio-1.14.1.md)): separatore decimale nel menu «Aspetto», revisioni in sola
lettura con una regola unica, finestre di dialogo che seguono la vista, sotto-schede ricordate, «?» dei progetti, pulsanti dei muri
spenti durante il calcolo.

## Correzioni

Le correzioni dell'interfaccia della 1.14.1 (sezione c.a., risposta elastica del palo, sezione composta, Bridge Design), più:

- **Palo orizzontale, anello interno di altro diametro.** La distinta delle armature ha un gruppo per diametro (per esempio G02A e
  G02B), ognuno con ancoraggio e sovrapposizione propri. Prima il calcolo si fermava con «diametri misti».

## Calcoli

Librerie GPC: release AJ (Model 2acc60ef, Checker 4a1b0090). Ogni differenza nei risultati di prova è registrata nel registro delle
differenze (AJ-LIB-01, AJ-FON-01) e approvata dall'utente.

| DLL | 1.14.0 (AG) | 1.14.2 (AJ) |
| --- | --- | --- |
| GPCModel | 5.21.0.0 | 5.21.0.1 |
| GPCModelData, GPC.Converter, GPC.Model.Persistence | 0.3.0.1, 2.6.0.1, 3.3.0.1 | 0.3.0.2, 2.6.0.2, 3.3.0.2 |
| GPCChecker.Concrete | 0.20.0.1 | 0.25.0.0 |
| GPCChecker.Steel | 2.84.0.0 | 2.118.0.2 |
| GPCChecker.CompositeBridge | 1.13.0.1 | 1.19.0.5 |
| GPC.Design | 0.36.0.0 | 0.41.0.4 |
| GPCChecker.Geotechnics, GPCChecker.Masonry | 0.19.0.1, 0.6.0.1 | 0.24.0.0, 0.10.0.4 |
| GPCUtilities, GPCGeometry, DelaunayMesh | 3.0.0.0, 2.3.0.0, 2.0.0.14 | invariate |

Dalla 1.14.1: pilastri NTC con l'interasse lungo il perimetro (R12), staffe EC nelle sovrapposizioni (R13), hc,eff DIN (R21),
ancoraggi dei muri oltre C60/75 (R10), riferimenti della torsione (R19). Nuove nella 1.14.2:

- **Taglio Model Code 2010 (R4).** θmin = 20° + 10 000 εx, al più 45°, nel taglio e nella torsione: cot θ massimo scende quando
  la sezione è deformata, la resistenza delle staffe pure. Un cot θ assegnato oltre il limite è rifiutato con il limite nel messaggio.
- **Taglio senza staffe (R9).** Il controllo VEd ≤ 0,5 bw d ν fcd (EN 1992-1-1, 6.2.2(6); DIN ν = 0,675) compare fra i dettagli;
  con le formule usate VRd,c resta sotto il limite, quindi nessun valore cambia.
- **Copriferro NTC senza Cmin (R11).** Senza una classe minima assegnata vale la classe UNI 11104 delle esposizioni, non più
  25 + 5 × gruppo: il copriferro nominale può cambiare di 5 mm (per esempio XF2 con C25/30: 45 invece di 50 mm; XC3 con C28/35: 40
  invece di 35 mm).
- **Muri, ribaltamento (BUG-MUR-2b).** La passiva a valle non entra più nella resistenza al ribaltamento; resta fra le resistenze
  allo scorrimento. I testi del metodo lo dicono.
- **Fessurazione (BUG-FES-1b/1c).** La soglia di wk = 0 usa il copriferro reale della barra tesa più esterna.
- **Pali.** Le librerie accettano la testa dei pali e dei micropali sotto il piano campagna; in ANTHEA il dato arriva con la 1.14.3.
  Lo sviluppo parziale delle barre vicino alle estremità (BUG-PO-5b) è nella libreria; ANTHEA lo userà con la 1.14.3.
- **Ponti in acciaio.** Steel, CompositeBridge e GPC.Design portano il lavoro delle librerie sulle giunzioni e sui collegamenti.

## Guide

Guide pratica e teorica **Rev59**, le stesse della 1.14.0 e della 1.14.1. Il sottomenu «Separatore decimale» e le correzioni di
calcolo saranno nella Rev60, con la 1.14.3.

## Limiti noti

Come nella 1.14.1, salvo R4, R9 e R11, corretti; restano rinviati, fra gli altri, R5 (dichiarato: le tensioni di esercizio con il
solutore non lineare usano le leggi di progetto) e R17 (classe indicativa di XC3 nei profili EN e UNI).
