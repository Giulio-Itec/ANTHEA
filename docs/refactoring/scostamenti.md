# Scostamenti tecnici D7: schede con effetto quantificato

Passo F0.6 del [piano](piano.md), decisione D7 di [decisioni.md](decisioni.md). Schede preparate il
6 ottobre 2026; le decisioni dell'utente della sera stessa e gli esiti delle ricerche sono riassunti
in fondo ([Decisioni ed esiti](#decisioni-ed-esiti-6-7-ottobre-2026)). Il registro leggibile da macchina è
[registro-differenze.json](registro-differenze.json); ogni scheda corrisponde alla voce con lo
stesso identificativo (D7-a … D7-g).

Questo documento riunisce in un solo passo le schede che il piano dettagliato distribuiva fra F2.2,
F4.3 e W1.2, come chiesto dalle critiche dei revisori. Dopo la decisione F2.2 e F4.3 attuano le
correzioni scelte; W1.2 scrive soltanto i riquadri delle voci dichiarate.

## Come leggere le schede

- **Codice**: percorsi relativi alla radice del repository indicato. ANTHEA al commit 2673b55
  (branch `refactoring/f0-scostamenti`), Checker al commit 5cc315f2 (develop), Model al commit
  c85b70a6 (master). I sorgenti citati di Checker e Model sono identici nel working tree.
- **DLL usate nei calcoli**: quelle di `lib/Checker` (manifest del 6/10: GPCChecker.Concrete
  0.0.14.0, GPCChecker.Geotechnics 0.1.0.0, GPCChecker.CompositeBridge 1.4.0.2, GPCModel 1.6.0.0) e
  ANTHEA.Calculations compilata dal branch.
- **Segno**: lo scostamento è cautelativo se il codice dà un risultato più sfavorevole della norma
  (apertura di fessura maggiore, resistenza minore, azione maggiore).
- **Effetto**: calcolato eseguendo il codice con le due varianti, senza modificare il codice di
  prodotto. Le varianti si ottengono cambiando un dato d'ingresso, usando il ramo di un altro
  profilo della libreria o applicando la formula alternativa alle grandezze intermedie restituite
  dalla traccia di calcolo. Harness e risultati sono descritti in fondo
  ([Come rieseguire i calcoli](#come-rieseguire-i-calcoli)).
- **Categoria** (campo `categoria` del registro):
  - `convenzione`: il codice segue alla lettera un testo normativo diverso da un altro testo
    applicabile;
  - `intenzionale`: scelta dichiarata nel codice, nei commenti o nei test;
  - `difetto-legacy`: comportamento non voluto;
  - `scostamento-normativo`: il codice si discosta dal testo della norma del profilo.
- **Stato** (campo `stato`): `da-decidere`, `dichiarato`, `da-correggere`, `corretto`.

## Fonti normative consultate

| Fonte | Disponibilità e punti riscontrati |
| --- | --- |
| D.M. 17/01/2018 (NTC 2018), testo della G.U. | copia locale `supporto/artefatti/ponte_taglio_pioli/fonti/NTC2018.txt` (non versionata): Tab. 4.1.I, §5.1.3.3.5 con Tab. 5.1.II, Tab. 5.1.V, Tab. 6.4.II, §11.2.11 |
| Circolare 21/01/2019 n. 7 (G.U. S.O. n. 5 alla n. 35 dell'11/02/2019) | pagine 87-88 (`supporto/artefatti/verifiche_checker_finale/circolare-c4-08.png` e `-09.png`) e 90 (`supporto/tmp/materiali_norme/CIRC_2019-91.png`): C4.1.2.2.4.5, eq. [C4.1.5]-[C4.1.10], Fig. C4.1.11, Tab. C4.1.IV |
| EN 1992-1-1:2004 | `supporto/tmp/materiali_norme/en.1992.1.1.2004.txt`: §7.3.4(2)-(3), eq. (7.9)-(7.14) |
| UNI EN 206-1:2006 | `supporto/tmp/materiali_norme/UNI-EN-206.txt`: prospetto F.1 |
| EN 1998-5:2004, Annesso F | non presente nei repository; riscontro fatto durante il port della libreria su una copia del testo (Checker `docs/migrazione-anthea/MIGRAZIONE_ANTHEA.txt:358-362`) |
| UNI 11104:2025 (in vigore dal 24/07/2025) | non presente nei repository. Anteprima pubblicata nel catalogo UNI, pagine visibili: indice (pp. III-IV, punto 6.1 «Requisiti relativi alle classi di esposizione» a p. 10), introduzione (p. 1), punti 1 e 2 (p. 2); frontespizio e prospetto 6 non visibili. Prospetto 6 da un estratto pubblicato il 28/07/2025 (fonte secondaria); riscontro sul testo con licenza prima di F2.9. Date e sostituzione della 2016 dalle schede del catalogo UNI |
| UNI 11104:2016 (ritirata il 24/07/2025) e 2004 | non presenti; 2016 dal prospetto 5 riprodotto in ATECAP 2020, p. 19 (fonte secondaria); 2004 non riscontrata |

## Quadro riassuntivo

| Voce | Oggetto | Codice | Norma | Segno | Effetto principale | Raccomandazione |
| --- | --- | --- | --- | --- | --- | --- |
| [D7-a](#d7-a-fessurazione-ntc-con-barre-distanziate) | Fessurazione NTC, barre distanziate | 1,7 · 0,75 (h − x) = 1,275 (h − x), massimo fra zona vicina e distante | Circolare [C4.1.10]: uguale; EC2 (7.14): 1,3 (h − x) | rispetto a EC2: non cautelativo (−1,9 %) se governa la zona distante, cautelativo se governa la zona vicina | griglia: 77 592 stati su 222 696 con +1,96 % (variante 1,3); 337 esiti cambiano (0,15 %) | dichiarare il ramo delle barre distanziate; 1,3 (h − x) solo per il limite senza barre aderenti |
| [D7-b](#d7-b-k2-del-ramo-ntc-senza-barre-compresse) | k2 del ramo NTC | 1,0 se nessuna barra è compressa | 0,5 in flessione | cautelativo | wk −24 … −48 % negli stati coinvolti; 9 282 esiti su 37 128 | correggere con opzione legacy |
| [D7-c](#d7-c-portanza-sismica-dei-muri-secondo-en-1998-5-annesso-f) | Portanza sismica dei muri | γRd anche su F̄ | (F.7): F̄ senza γRd | cautelativo | η +1,7 … +7,0 % (es. 4: 0,791 contro 0,760); 3 esiti su 46 | correggere (la libreria segue già la norma) |
| [D7-d](#d7-d-coefficiente-di-base-dei-pali-uguale-per-ogni-tecnologia) | γb dei pali | 1,35 per ogni tecnologia | Tab. 6.4.II: 1,15 infissi, 1,35 trivellati, 1,30 elica | cautelativo (infissi, elica) | Rc,d +4,3 … +7,0 % infissi, +1,0 … +1,2 % elica; nessun esito | correggere il predefinito, archivi invariati |
| [D7-e](#d7-e-classe-minima-per-xc3-xd1-xf4-e-xa1) | Classe minima per XC3, XD1, XF4, XA1 | C30/37 | UNI 11104:2025 prospetto 6 (secondo l'estratto del 28/07/2025) e 2016 prospetto 5: C30/37 (EN 206 F.1: C30/37); C28/35 attribuito alla 2004 | cautelativo rispetto alla 2004 | solo C28/35: avviso in 46 combinazioni su 520, +5 mm di copriferro in 184 casi su 17 646 | C30/37 confermato, citazioni corrette in ANTHEA; seguito su XF1 e composizione: e4 in F2.9 dopo il riscontro del prospetto 6 (da ratificare) |
| [D7-f](#d7-f-bridge-design-coefficiente-e-carico-da-traffico) | Bridge Design, carichi da traffico | γQ 1,50; 9 kN/m² su tutta la larghezza, senza tandem | Tab. 5.1.V: γQ 1,35; Schema 1 (Tab. 5.1.II) | γQ cautelativo (+11 %); carico non cautelativo sotto 20 m di luce, cautelativo sopra | momenti −2,5 … −5,5 %; costi −1,4 … −5,2 % solo per strallati, sospesi, archi e reticolari; ottimizzazione invariata | γQ 1,35 per i documenti nuovi; dichiarare il carico equivalente |
| [D7-g](#d7-g-testo-del-metodo-nei-report-della-sezione-composta) | Testo del metodo nei report del ponte | etichetta breve della scelta | — | nessun effetto numerico | report senza norma e versione; 8 baseline BridgeAudit | correggere |

## D7-a Fessurazione NTC con barre distanziate

**Categoria proposta**: convenzione. **Famiglia**: calcestruzzo, fessurazione.

### Codice

- Libreria, ramo NTC: `Checker/GPCChecker.Concrete/Cracking/CrackWidthCalculator.cs:58-62`.
  Δsm,vicino = (3,4 c + k1 k2 0,425 Ø/ρ)/1,7; Δsm,distante = 0,75 (h − x); con
  s ≤ 5 (c + Ø/2) si usa Δsm,vicino, altrimenti max(Δsm,vicino; Δsm,distante);
  wk = max[0; 1,7 Δsm (εsm − εcm)]. In termini di distanza fra le fessure, con barre distanziate
  sr = max(3,4 c + k1 k2 0,425 Ø/ρ; 1,275 (h − x)).
- Libreria, limite superiore senza barre aderenti in Ac,eff:
  `CrackWidthCalculator.cs:108` (sr = 1,7 · 0,75 (h − x) per NTC, 1,3 (h − x) per gli altri profili).
- Prodotto attuale, da cui la libreria è stata trasferita: `X.Calculations/Ntc2018Checks.cs:217-222`
  e `X.Calculations/ConcreteCodeChecks.cs:224`. Il ramo Eurocodice dello stesso codice usa
  1,3 (h − x) (`ConcreteCodeChecks.cs:202`, `CrackWidthCalculator.cs:86`).
- Nella libreria lo stesso ramo vale anche per il profilo CNR-DT 200 (`CrackProfiles.cs:57`).

### Norma e riscontro sul testo

- Circolare 2019, C4.1.2.2.4.5 (pp. 87-88 della G.U.): wk = 1,7 εsm Δsm [C4.1.5]; con spaziatura
  non superiore a 5 (c + φ/2) Δsm = (k3 c + k1 k2 k4 φ/ρeff)/1,7 [C4.1.7]; con spaziatura
  superiore, nella fascia di estensione 5 (c + φ/2) attorno alle barre vale ancora [C4.1.7], nella
  parte rimanente Δsm = 0,75 (h − x) [C4.1.10] (il testo stampa «Δσμ»); la Fig. C4.1.11 mostra
  l'apertura maggiore nella zona distante. **Riscontro**: il codice riproduce la Circolare alla
  lettera, compreso il massimo fra le due zone (apertura caratteristica della superficie).
- EN 1992-1-1:2004, §7.3.4(3), eq. (7.14): con spaziatura superiore a 5 (c + φ/2) o senza armatura
  aderente nella zona tesa si assume sr,max = 1,3 (h − x) come limite superiore, al posto della
  (7.11). 1,3 corrisponde a 1,7 · 0,765: la Circolare arrotonda a 0,75.
- Il caso senza armatura aderente in Ac,eff è trattato solo dalla EN 1992-1-1; il codice NTC vi
  estende per analogia il coefficiente 1,7 · 0,75.
- Nota testuale: i commenti di `CrackWidthCalculator.cs:95` e `ConcreteCodeChecks.cs:213` citano
  «EC2 7.3.4(4)»; nel testo del 2004 la (7.14) è al punto 7.3.4(3).

### Segno

Rispetto alla Circolare nessuno scostamento. Rispetto alla EN 1992-1-1 (7.14): dove governa la
zona distante il codice dà un'apertura minore dell'1,92 % (1,275/1,3), quindi non cautelativo; dove
il termine della zona vicina supera 1,3 (h − x) il codice dà un'apertura maggiore della (7.14),
quindi cautelativo.

### Effetto quantificato

Varianti: **L** codice; **A1** coefficiente 1,3 (h − x) mantenendo il massimo fra le due zone;
**A2** regola EC2 completa (sr = 1,3 (h − x) con barre distanziate), identica al ramo EN 1992-1-1
della libreria.

1. Fixture scalare di Checker `GPCChecker.Test.Concrete/Fixtures/crack-scalar-legacy.csv` (cattura
   ANTHEA 4bb8815), 200 righe NTC: ANTHEA (`ConcreteCodeChecks.CrackWidth`), libreria
   (`CrackWidthCalculator.Width`) e fixture coincidono entro 1e-9. 79 righe hanno barre
   distanziate: in 50 governa la zona distante (A1 e A2: +1,96 %), in 29 il termine vicino supera
   1,3 (h − x) (A1: invariato; A2: da −0,6 % a −95 %); 2 hanno σs = 0.
2. Fixture di sezione `crack-legacy.csv` (177 stati NTC, sezioni di `crack-sections.xml`): 34 stati
   parzializzati a regione singola, tutti con barre ravvicinate. Nessun effetto.
3. Griglia mirata con `SectionCrackCheck.Evaluate` della libreria: 4 176 sezioni (travi
   rettangolari b 300-1 200 mm, h 500-1 500 mm, c 25-50 mm, Ø 12-32, interassi 100-350 mm; travi a T
   con ala 1 200-2 000 mm; solette 1 000 × 200-400 mm con rete superiore; sezioni a semplice
   armatura), stato fessurato elastico con n = 15, flessione retta e tensoflessione con asse neutro
   a 0,05-0,25 h, σs 200, 260 e 320 MPa, C30/37, lunga durata, XC3 e XD1, combinazioni frequente e
   quasi permanente (wlim 0,4/0,3 e 0,3/0,2 mm). Risultati su 222 696 stati con apertura dalla
   formula:
   - barre distanziate in 98 376 stati (44 %), con zona distante governante in 77 592: +1,96 %
     con A1 e A2;
   - A1: 337 esiti cambiano (0,15 %), tutti da soddisfatto a non soddisfatto (ηw fra 0,981 e 1);
   - A2: 4 581 esiti cambiano, 337 peggiorano e 4 244 migliorano (solette e sezioni a semplice
     armatura in cui 1,3 (h − x) è minore del termine vicino; riduzione dell'apertura fino al 67 %);
   - travi a T: barre distanziate solo nell'anima, sempre con zona distante governante; nessun
     esito cambia.
   La formula scalare di ANTHEA coincide con quella della libreria in tutti gli stati.
4. Limite superiore senza barre aderenti: +1,96 % esatto (1,3/1,275); 216 stati nella griglia,
   nessun esito cambia; nessuno stato nelle fixture di sezione.

Evidenze: `out/a-fixture-scalare.csv`, `out/a-b-fixture-sezioni.csv`, `out/a-b-griglia.csv`,
`out/a-b-fessurazione.txt`.

### Test e catture che lo fissano

- Checker `GPCChecker.Test.Concrete/CrackMigrationTests.cs:228`
  (LegacyCrackWidthsAndRequirementsAreReproduced, `crack-scalar-legacy.csv`), `:74`
  (LegacyCrackStatesAreReproduced, `crack-legacy.csv`), `:210`
  (TensileBarsOutsideEffectiveAreaGiveUpperBound).
- ANTHEA `supporto/test/X.Verifiche/SectionWorkspaceChecks.cs:90-96` (asserzione «C4.1.10 distanza
  media 0,75(h-x)»).

### Opzioni e raccomandazione

- a1: coefficiente 1,3 (h − x) mantenendo il massimo fra le due zone;
- a2: regola EC2 completa (sostituzione con 1,3 (h − x));
- a3: dichiarare il ramo della Circolare;
- a4: solo per il limite superiore senza barre aderenti, 1,3 (h − x).

**Raccomandazione**: a3 per il ramo delle barre distanziate, perché il profilo NTC segue la
Circolare alla lettera, a2 non è cautelativa quando governa la zona vicina e a1 cambierebbe la
Circolare per l'1,96 %. In più a4, con l'opzione legacy per le fixture, perché quel caso è trattato
solo dalla EN 1992-1-1. Correggere il rimando «7.3.4(4)» nei commenti (solo testo). Attuazione in
F2.3 (libreria) e F2.7 (collegamento), in due commit.

### Riquadro Wiki

`supporto/docs/guida-teorica-anthea.md:343` («Apertura delle fessure», capitolo Sezione in
calcestruzzo armato) e `:2187` («Limiti e confronto con il software», capitolo Fessurazione).

## D7-b k2 del ramo NTC senza barre compresse

**Categoria proposta**: scostamento-normativo. **Famiglia**: calcestruzzo, fessurazione.

### Codice

- Libreria: `Checker/GPCChecker.Concrete/Cracking/SectionCrackCheck.cs:163` (k2 dalle tensioni di
  tutte le barre ordinarie) e `:173-174` (solo i profili diversi da NTC e CNR-DT 200 passano a 0,5
  quando l'asse neutro taglia la sezione); `CrackWidthCalculator.cs:116-122` (K2: 0,5 se almeno una
  barra ha σs < 0, altrimenti 1,0).
- Prodotto attuale: `X.Calculations/Ntc2018Checks.cs:30-36` (CrackK2), `:77`, `:92-96`.
- Regola del codice: con sezione parzializzata k2 = 0,5 se esiste una barra compressa, altrimenti
  k2 = 1,0.

### Norma e riscontro sul testo

- Circolare 2019, C4.1.2.2.4.5 (p. 88): k2 = 0,5 nel caso di flessione, 1,0 nel caso di trazione
  semplice; in trazione eccentrica k2 = (ε1 + ε2)/(2 ε1) [C4.1.9], con ε1 ed ε2 deformazioni di
  trazione alle estremità della sezione fessurata. EN 1992-1-1:2004 §7.3.4(3), (7.13): uguale.
- Con asse neutro interno alla sezione la deformazione minima non è di trazione: è flessione e
  k2 = 0,5. Il ramo Eurocodice dello stesso codice usa già 0,5 (`Ntc2018Checks.cs:92-96`).
- La guida teorica descrive la regola del codice (`guida-teorica-anthea.md:354`) e, nel capitolo
  sulla fessurazione, quella della norma (`:2171`).

### Segno

Cautelativo: con k2 = 1,0 il termine k1 k2 k4 Ø/ρ raddoppia.

### Effetto quantificato

Variante **B**: k2 = 0,5 per ogni sezione parzializzata, calcolata con la libreria sugli stessi
dati.

- Fixture di sezione `crack-legacy.csv`: 1 stato parzializzato con k2 = 1 su 34; wk −40,8 %,
  esito invariato.
- Griglia mirata (la stessa di D7-a): 37 128 stati con k2 = 1 su 222 696:
  - sezioni a semplice armatura in flessione: tutte (8 640); wk −24 % in media, fino a −44 %;
    1 877 esiti cambiano;
  - solette in flessione con la rete superiore tesa (asse neutro sopra le barre superiori): 48 su
    5 544; wk −37 … −39 %; 24 esiti cambiano;
  - tensoflessione con asse neutro a 0,05-0,25 h: travi 16 200 su 144 000 (wk −36 % in media,
    fino a −48 %; 2 623 esiti), solette 12 240 su 28 800 (wk −36 % in media, fino a −47 %; 4 758
    esiti);
  - travi con barre superiori e travi a T in flessione: nessun caso, le barre superiori sono
    compresse.
- In totale 9 282 esiti cambiano, tutti da non soddisfatto a soddisfatto (un quarto degli stati
  coinvolti). Dove governa la zona distante l'effetto è nullo.

Evidenze: `out/a-b-fixture-sezioni.csv`, `out/a-b-griglia.csv` (colonne `k2`, `wk_B`, `esito_B`).

### Test e catture che lo fissano

- Checker `CrackMigrationTests.cs:277` (K2) e `:74` (`crack-legacy.csv`).
- ANTHEA `supporto/test/X.Verifiche/SectionWorkspaceChecks.cs:75-77` e `:123-128`.

### Opzioni e raccomandazione

- b1: k2 = 0,5 per ogni sezione parzializzata (come il ramo Eurocodice), con opzione legacy;
- b2: dichiarare.

**Raccomandazione**: b1. L'effetto (−24 … −48 % sull'apertura) è troppo grande per un riquadro e
riguarda casi comuni: sezioni senza armatura compressa e tensoflessione. Attuazione in F2.3 e F2.7
in due commit: collegamento con l'opzione legacy, poi cambio del predefinito con le differenze di
questa scheda.

### Riquadro Wiki

`supporto/docs/guida-teorica-anthea.md:354` (testo della regola, da aggiornare) e `:2171`.

## D7-c Portanza sismica dei muri secondo EN 1998-5 Annesso F

**Categoria proposta**: intenzionale. **Famiglia**: geotecnica, muri di sostegno.

### Codice

- ANTHEA: `X.Calculations/RetainingWall.Library.cs:19-20`
  (`ModelFactorOnSoilInertia = true`) e `:132-135` (passato a `WallSeismicBearing.FromSite` e
  `Assigned`).
- Libreria: `Checker/GPCChecker.Geotechnics/Foundations/ShallowFoundationSeismic.cs:57` e `:67`
  (F̄ = γRd · kh/tan φ'd solo con `modelFactorOnInertia`), `Walls/WallModels.cs:170-175`
  (predefinito `false`), `Walls/RetainingWallAnalysis.cs:303`.
- Formula del codice: F̄ = γRd · ag S/(g tan φ'd), oltre a N̄ = γRd NEd/Nmax, V̄ = γRd VEd/Nmax,
  M̄ = γRd MEd/(B Nmax).

### Norma e riscontro sul testo

EN 1998-5:2004, Annesso F (informativo): γRd compare nelle grandezze adimensionali N̄, V̄ e M̄;
l'inerzia del terreno F̄ della (F.7), per terreni incoerenti asciutti ag/(g tan φ'd), non lo
contiene. Tab. F.2: γRd 1,00 per sabbia mediamente densa o densa, 1,15 sciolta asciutta, 1,50
sciolta satura, 1,00 argilla non sensibile, 1,15 sensibile. Il testo non è nei repository: il
riscontro è quello del port (`MIGRAZIONE_ANTHEA.txt:358-362`).

### Segno

Cautelativo: F̄ maggiore riduce la capacità portante sismica.

### Effetto quantificato

- Esempio 4 dei muri (Checker `GPCChecker.Test.Geotechnics/WallExamplesTests.cs:87-113`):
  η 0,79114 (ANTHEA) contro 0,76031 (EN), +4,06 %.
- Documenti dei muri: 110 della cattura `walls-documents.jsonl.gz` (ANTHEA cbed972) e i 2 esempi di
  `supporto/esempi/muri-sostegno`. 20 hanno il sisma; 3 sono rifiutati dai controlli dei dati, 2 non
  hanno portanza sismica calcolata, 15 la hanno, con 46 combinazioni e γRd = 1,15 in tutti. La
  catena ricostruita coincide con `RetainingWall.Calculate`.
  - F̄ aumenta del 15 % (= γRd);
  - Δη = +1,7 … +7,0 % (media +3,8 %) nelle 30 combinazioni con η(EN) ≤ 1,5; fino a +40 % nei
    casi fuori campo con dominio esaurito (η ≫ 1);
  - esiti che cambiano: 3 combinazioni in 3 documenti (η EN 0,986 e 0,998 contro ANTHEA 1,030 e
    1,067).
- Con γRd = 1,00 l'effetto è nullo.

Evidenze: `out/c-muri.csv`, `out/c-muri.txt`.

### Test e catture che lo fissano

- ANTHEA `supporto/test/RetainingWall.Checks/LibraryAdapterChecks.cs:91-92` e
  `AdvancedChecks.cs:38-40`.
- Checker `WallExamplesTests.cs:109-112`, `FoundationSeismicEdgeCaseTests.cs:215`,
  `GeneralGeotechnicsMigrationTests.cs:256-259` (`geotechnics-seismic-bearing.csv`) e la cattura
  `walls-documents.jsonl.gz`.

### Opzioni e raccomandazione

- c1: ANTHEA adotta il predefinito della libreria (`false`); l'opzione legacy resta nella libreria
  per le fixture;
- c2: dichiarare.

**Raccomandazione**: c1. La libreria segue già la norma; in ANTHEA cambiano una costante e i test
che la fissano. Effetto atteso: η −1,7 … −6,5 %, 3 esiti dei documenti di prova passano a
soddisfatto. Attuazione con il collegamento dei muri (F4.9), in due commit.

### Riquadro Wiki

`supporto/docs/guida-teorica-anthea.md:1203` («Portanza sismica»).

## D7-d Coefficiente di base dei pali uguale per ogni tecnologia

**Categoria proposta**: scostamento-normativo. **Famiglia**: geotecnica, pali e micropali.

### Codice

- ANTHEA: `X.Calculations/Calcolo.cs:221-223` (γs 1,15, γst 1,25, γb = `sicurezza_base` o 1,35,
  γG 1,30/1,00); `X.Calculations/CalculationDefaults.cs:7` (i fogli nuovi scrivono
  `"sicurezza_base": "1.35"` per ogni tecnologia). Il valore è modificabile
  (`X.Desktop/Wpf/Ui.cs:256`) e gli archivi lo memorizzano.
- Model: `Model/Standards/StandardNTC2018Geotechnics.cs:49-50` e `:68-70` (PileExecution, predefinito
  Bored), oggi non usato da ANTHEA.
- Checker: `GPCChecker.Geotechnics/Piles/PileModels.cs:61-73` (`PileResistanceFactors.FromStandard`),
  `Piles/AxialPileModels.cs:8` (`PileInstallation`, 6 valori).

### Norma e riscontro sul testo

NTC 2018 §6.4.3.1.1, Tab. 6.4.II (R3), riscontrata sul testo: γb 1,15 pali infissi, 1,35
trivellati, 1,30 elica continua; γs 1,15; γt 1,15/1,30/1,25; γst 1,25. Gli altri coefficienti del
codice coincidono con la tabella e con `FromStandard` (γG 1,30/1,00, ξ3 = ξ4 = 1,70 con una
verticale).

Corrispondenza proposta fra le tecnologie della libreria e l'esecuzione di Model:

| PileInstallation | Foglio ANTHEA | PileExecution | γb |
| --- | --- | --- | --- |
| Bored | Trivellato | Bored | 1,35 |
| ContinuousFlightAuger | Elica continua | ContinuousFlightAuger | 1,30 |
| DrivenSteelSection | Battuto · Profilato d'acciaio | Driven | 1,15 |
| DrivenClosedSteelTube | Battuto · Tubo d'acciaio chiuso | Driven | 1,15 |
| DrivenPrecastConcrete | Battuto · Calcestruzzo prefabbricato | Driven | 1,15 |
| DrivenCastInPlace | Battuto · Calcestruzzo gettato in opera | Driven | 1,15 (palo infisso a spostamento) |
| micropali IGU e IRS | Micropalo | Bored | 1,35 (perforati e iniettati: la NTC non ha valori propri) |

### Segno

Cautelativo per infissi ed elica continua; nessun effetto per trivellati e micropali.

### Effetto quantificato

94 casi palo e micropalo di `supporto/test/casi_confronto.json`: 85 calcolati, 9 rifiuti attesi
(atteso nullo). Resistenza di progetto Rc,d alla profondità massima della curva di progetto:

| Tecnologia | Casi | ΔRc,d drenata | ΔRc,d non drenata |
| --- | --- | --- | --- |
| Battuto (4 sottotipi) | 12 | +4,3 … +7,0 % (media +6,0 %) | +3,3 … +4,6 % |
| Elica continua | 8 | +1,0 … +1,2 % | +0,7 … +1,0 % |
| Trivellato | 31 | 0 | 0 |
| Micropali | 34 | 0 | — |

Verifica metamorfica superata in 85 casi su 85: a ogni profondità e per i rami medio e minimo
Rb,d diventa Rb,d · 1,35/γb e Rs,d resta invariata. Nessun esito Ed ≤ Rc,d cambia (azioni dei
casi di 100 kN). Evidenze: `out/d-pali.csv`, `out/d-pali.txt`.

### Test e catture che lo fissano

- `supporto/test/casi_confronto.json`: gli attesi Python dei 20 casi infissi ed elica sono
  calcolati con 1,35. Non si sovrascrivono: un nuovo atteso si affianca, con la verifica metamorfica
  di questa scheda.
- Checker `GPCChecker.Test.Geotechnics/PileCapacityMigrationTests.cs:177` (`piles-vertical.jsonl`),
  `PileEdgeCaseTests.cs:344-345`; Model `UnitTest/GeotechnicsTest.cs:77-79`.

### Opzioni e raccomandazione

- d1: predefinito per tecnologia da `PileExecution`, per i fogli nuovi e al cambio di tecnologia;
  gli archivi conservano il valore memorizzato, quindi i progetti salvati non cambiano;
- d2: come d1, più la migrazione degli archivi con «1.35» e tecnologia infissa o a elica (cambia i
  risultati dei progetti salvati);
- d3: dichiarare 1,35 come valore cautelativo unico.

**Raccomandazione**: d1, con un avviso nel foglio quando γb differisce da quello della tecnologia.
Attuazione in F4.5 in due commit: equivalenza con l'override 1,35, poi predefinito per tecnologia.

### Riquadro Wiki

`supporto/docs/guida-teorica-anthea.md:158` («Più indagini e coefficienti», capitolo Palo
verticale).

## D7-e Classe minima per XC3, XD1, XF4 e XA1

**Categoria proposta**: intenzionale. **Famiglia**: calcestruzzo, durabilità.

### Codice

- Libreria: `Checker/GPCChecker.Concrete/Durability/ExposureClasses.cs:61` (XC3), `:63` (XD1),
  `:72` (XF4), `:73` (XA1): `Uni11104MinStrength` = 30; fonte dichiarata ai righi `:10-13` (ATECAP
  2020, fonte secondaria, senza edizione); riferimento «UNI 11104 prospetto 5» a `:107`; commenti
  senza edizione a `:89`, `:121`, `:128`. Da correggere nella prossima release (testo proposto più
  sotto).
- ANTHEA: `X.Calculations/Materials/MinimumConcrete.cs:5-7` (fonte: prospetto 6 della UNI 11104:2025,
  valori trascritti dal prospetto 5 della 2016, eccezione XF1), `:21` (etichette);
  `X.Calculations/Materials/MixAutomation.cs:5-7` (A/C e cemento dal prospetto 5 della 2016);
  `X.Materiali/MinimumConcrete.cs:15-16` (nota della scheda) e `X.Materiali/MixAutomation.cs:46`
  («Riferimenti della composizione», anche nel report Word dei materiali).
- Uso: copriferro NTC con +5 mm se fck < Cmin pertinente (`X.Calculations/Materials/NtcCover.cs:24`
  e `:27`, `MaterialCover.cs:31`, `ConcreteDetailingAnalysis.cs:38`,
  `ElasticHorizontalPile.Reinforcement.cs:155`); avviso «classe inferiore al minimo»
  (`X.Materiali/MixAutomation.cs:71-72`, `MaterialDetails.cs:173`).

### Norma e riscontro sul testo

- NTC 2018 §11.2.11: le caratteristiche del calcestruzzo si indicano secondo le Linee Guida «facendo
  anche, in assenza di analisi specifiche, utile riferimento» a UNI EN 206 e UNI 11104. La nota alla
  Tab. 4.1.I ammette le classi C28/35 e C32/40 «già in uso».
- Circolare 2019, Tab. C4.1.IV (p. 90): Cmin per ambiente (C25/30, C30/37, C35/45), da intendere
  riferita alla pertinente classe di esposizione della UNI EN 206:2016.
- Edizioni (catalogo UNI, riscontro diretto): UNI 11104:2025 in vigore dal 24/07/2025, sostituisce
  la 2016; UNI 11104:2016 in vigore dal 14/07/2016, ritirata il 24/07/2025, sostituiva la 2004.
- UNI 11104:2025, punto 6.1, prospetto 6 «Valori limite per la composizione e le proprietà del
  calcestruzzo» (riscontro indiretto). Le pagine visibili dell'anteprima pubblicata da UNI sono
  l'indice (pp. III-IV, con il punto 6.1 «Requisiti relativi alle classi di esposizione» a p. 10),
  l'introduzione (p. 1) e i punti 1 e 2 (p. 2): confermano edizione e struttura, non i valori; il
  frontespizio e il prospetto 6 non sono visibili e l'indice non elenca i prospetti. Secondo un
  estratto pubblicato il 28/07/2025 (fonte secondaria) il prospetto 6 dà C30/37 per XC3, XD1, XF4 e
  XA1, come il codice; XF1 C30/37 con A/C 0,55 contro C32/40 e 0,50 del codice; cemento minimo più
  basso in tutte le classi che lo prescrivono. La trascrizione completa del 7/10, attribuita alla
  «p. 12 dell'anteprima», veniva da contenuti del file che il lettore non mostra (residuo di una
  versione precedente del file): i valori per classe nel registro sono provvisori e si riscontrano
  sul testo di una copia con licenza prima di F2.9.
- UNI 11104:2016, prospetto 5, stesso titolo (riscontro indiretto: riproduzione in ATECAP 2020,
  p. 19; per XC1-XC4 concorde un articolo tecnico di settore del 2024): coincide con il codice in
  tutte le 18 classi, XF1 compresa (C32/40), e con AtecapMix per A/C e cemento. Testo UNI non
  consultato.
- UNI 11104:2004, prospetto 4: C28/35 per XC3, XD1, XF4 e XA1 secondo il commento della libreria e
  la revisione del 6/10; non riscontrato.
- UNI EN 206-1:2006, prospetto F.1 (informativo, valori raccomandati), riscontrato: C30/37 per le
  stesse quattro classi, come il codice.

### Segno

Rispetto all'edizione in vigore nessuno per XC3, XD1, XF4 e XA1; cautelativo per XF1 e per la
composizione (seguito). Rispetto alla 2004, cautelativo.

### Effetto quantificato

Le misure seguenti sono quelle dell'opzione e1 (C28/35), scartata il 7/10.

- ANTHEA (`MinimumConcrete.Fck`) e libreria (`Uni11104MinStrength`) coincidono per tutte le 18
  classi.
- 520 combinazioni di esposizione (una classe per famiglia XC, XD, XS, XF, XA; da 1 a 3 classi; più
  X0): la classe minima passa da C30/37 a C28/35 in 46 combinazioni (4 singole, 18 doppie, 24
  triple).
- Catalogo della scheda Materiali (16 classi, C28/35 compresa): cambia solo C28/35, che oggi nelle 46
  combinazioni è segnalata «inferiore al minimo». Le altre classi hanno fck < 28 o ≥ 30 MPa e non
  cambiano.
- Copriferro nominale NTC su 17 646 casi (combinazioni × fck × trave o piastra): cambia in 184 casi,
  tutti con fck fra 28 e 30 MPa; il codice aggiunge 5 mm, con C28/35 no. ANTHEA (`NtcCover`) e
  libreria (`CoverRequirements`) coincidono.
- L'opzione e1 avrebbe richiesto anche l'etichetta «C28/35» (`MinimumConcrete.Label(28)` lancia
  un'eccezione). Con C30/37 non serve: `Label` riceve solo valori di `MinimumConcrete.Required`
  (12, 25, 30, 32, 35 MPa).

Evidenze: `out/e-durabilita.txt`, `out/e-copriferri.csv`.

### Test e catture che lo fissano

- Checker `GPCChecker.Test.Concrete/DurabilityEdgeCaseTests.cs:122-125` (XC3, profilo NTC = 30) e
  `DurabilityMigrationTests.cs` (fixture `durability-legacy.csv`, righe MIX).
- ANTHEA `X.Materiali/ExposureSelector.cs:102` e `:109` (autoverifica delle etichette).

### Opzioni e raccomandazione

- e1: riscontrare il testo della UNI 11104 (2016) e, se confermato, adottare C28/35 nel profilo NTC
  (tabella della libreria ed etichetta);
- e2: dichiarare la scelta dei valori del prospetto F.1 della EN 206, più cautelativa.

**Raccomandazione** (6/10): e1 dopo il riscontro, perché il profilo NTC rimanda alla UNI 11104 e
l'effetto è limitato a C28/35. Fino al riscontro, riquadro dichiarato. La decisione serve prima di
F2.9; attuazione in F2.3 (libreria) e F2.9.

### Esito e attuazione (7/10)

Decisione dell'utente: «confermo la norma più aggiornata». Il riscontro ha mostrato che e1 si
basava sull'edizione 2004: la 2016 e la 2025 in vigore (questa secondo l'estratto del 28/07/2025)
danno C30/37 per le quattro classi, come il codice. C30/37 resta; nessun valore numerico cambia.
Stato della voce: dichiarato.

ANTHEA, branch `refactoring/d7e-uni11104`: citazioni con edizione e prospetto nel commento di
`MinimumConcrete` e di `AtecapMix`, nella nota della classe minima della scheda Materiali
(«UNI 11104:2025, prospetto 6», con la nota su XF1 quando XF1 è attiva), nel testo «Riferimenti
della composizione» della scheda e del report Word dei materiali, nel README di X.Materiali e nella
guida teorica («Esposizioni e requisiti del materiale» e riga XF1 della tabella; edizioni Word e PDF
da rigenerare con le guide); indice della Wiki rigenerato. Nessun atteso da aggiornare: questi testi
non hanno uscite catturate nel runner.

### Seguito: XF1 e composizione (e4 in F2.9, da ratificare)

Secondo l'estratto del 28/07/2025 il prospetto 6 della 2025 differisce dai valori del codice, presi
dal prospetto 5 della 2016, in tre punti, tutti con il codice più restrittivo. I valori di cemento
per classe della tabella sono quelli provvisori del registro, da riscontrare sul prospetto 6:

| Grandezza | Codice (2016) | UNI 11104:2025 (estratto; cemento per classe provvisorio) |
| --- | --- | --- |
| Classe minima XF1 | C32/40 | C30/37 |
| A/C massimo XF1 | 0,50 | 0,55 |
| Cemento minimo, kg/m³ | 300 (XC1-XC2); 320 (XC3, XD1, XF1, XA1); 340 (XC4, XS1, XD2, XF2-XF3, XA2); 360 (XS2-XS3, XD3, XF4, XA3) | 280; 300; 320; 340 per XF4 e 350 per XS2-XS3, XD3, XA3 |

Effetto della classe minima di XF1: cambiano 13 combinazioni su 520 (1 singola, 5 doppie, 7 triple:
XF1 con XC1, XC2, XC3, XD1, XA1), da C32/40 a C30/37; nel catalogo cambia solo C30/37, oggi segnalata
inferiore al minimo in quelle combinazioni, e il suo copriferro nominale NTC perderebbe i 5 mm della
resistenza sotto il minimo. La composizione cambia in tutte le combinazioni tranne X0 (cemento) e
dove XF1 governa l'A/C.

- e4: allineare classe minima e composizione al prospetto 6 della 2025, in F2.9 insieme alla
  libreria (due commit). Ricattura di `durability-legacy.csv`: 22 righe MIX con cemento (per XF1 e
  XF1+XF3 anche la classe minima, per XF1 l'A/C) e 36 righe NTC di XF1 e XF1+XF3 con Cmin pertinente
  da `MinimumConcrete.Required`, da 32 a 30, di cui 4 con fck 30 senza la maggiorazione di 5 mm. Gli
  attesi di A/C e cemento di `supporto/test/Materiali/MaterialViewChecks.cs:78` si ritrascrivono dal
  prospetto 6;
- e5: tenere i valori della 2016 come scelta cautelativa dichiarata (stato attuale, già scritto
  nelle citazioni).

**Raccomandazione**: e4, coerente con «confermo la norma più aggiornata»; decisione prima di F2.9.

**Decisione** (7/10, del coordinatore su delega dell'utente, «esegui tutto te», da ratificare): e4
in F2.9, insieme allo spostamento della durabilità nella libreria, dopo il riscontro del prospetto 6
su una copia con licenza della norma. Il riscontro viene prima perché la variazione su XF1 e sul
cemento minimo è a sfavore rispetto al codice attuale e le fonti di oggi sono le pagine visibili
dell'anteprima e un estratto secondario. Se il testo smentisse l'estratto, la scelta fra e4 ed e5
torna all'utente.

### Libreria: testo proposto per la prossima release (F2.9)

Checker non si modifica ora: una modifica del sorgente cambierebbe la DLL della release S2. Con la
decisione e4 (da ratificare) in F2.9 si scrivono i testi «con e4», dopo il riscontro del prospetto 6
su una copia con licenza; gli altri valgono solo se il riscontro riporta la scelta all'utente. Righe di
`GPCChecker.Concrete/Durability/ExposureClasses.cs` (develop 0d7ba50b, file invariato da ddfe7edf):

- `:10` «and the UNI 11104 requirements (minimum fck, maximum w/c, minimum cement).» → «and the
  UNI 11104 requirements (minimum fck, maximum w/c, minimum cement; UNI 11104:2016, prospetto 5).»
- `:12-13` «UNI 11104 from ANTHEA (ATECAP 2020, secondary source: XC3, XD1, XF4 and XA1 give C30/37
  where UNI 11104:2004 gave C28/35, on the safe side).» → «UNI 11104:2016 prospetto 5 from ANTHEA, as
  reproduced in ATECAP 2020 p. 19 (secondary source). UNI 11104:2025 (in force since 24/07/2025),
  prospetto 6, according to an extract published on 28/07/2025 (secondary source, to be checked on
  the text of the standard): same minimum classes except XF1 (C30/37), w/c 0.55 for XF1 and lower
  minimum cement contents; XC3, XD1, XF4 and XA1 are C30/37 in both editions (C28/35 attributed to
  UNI 11104:2004).» Con e4: «UNI 11104:2025 prospetto 6 (in force since 24/07/2025, replaces
  UNI 11104:2016), checked on the text of the standard. Before this release the values were those of
  UNI 11104:2016 prospetto 5 as reproduced in ATECAP 2020 p. 19 (secondary source): XF1 C32/40 with
  w/c 0.50 and higher minimum cement contents. XC3, XD1, XF4 and XA1 are C30/37 in both editions
  (C28/35 attributed to UNI 11104:2004).»
- `:89` «UNI 11104 minimum characteristic cylinder strength» → «UNI 11104:2016 (prospetto 5) minimum
  characteristic cylinder strength».
- `:107` «"UNI 11104 prospetto 5 (NTC 2018 §11.2.11)"» → «"UNI 11104:2016 prospetto 5 (NTC 2018
  §11.2.11)"»; con e4 «"UNI 11104:2025 prospetto 6 (NTC 2018 §11.2.11)"».
- `:121` «UNI 11104: largest w/c» → «UNI 11104:2016 prospetto 5: largest w/c».
- `:128` «(UNI 11104)» → «(UNI 11104:2016 prospetto 5, note a; same note in UNI 11104:2025
  prospetto 6)».
- Con e4, righe dei dati (`uniStrength`, `uniRatio`, `uniCement`; valori provvisori del registro, da
  confermare sul prospetto 6): `:59-60` XC1, XC2 cemento 280;
  `:61` XC3 300; `:62` XC4 320; `:63` XD1 300; `:64` XD2 320; `:65` XD3 350; `:66` XS1 320; `:67-68`
  XS2, XS3 350; `:69` XF1 30, .55, 300; `:70-71` XF2, XF3 320; `:72` XF4 340; `:73` XA1 300; `:74`
  XA2 320; `:75` XA3 350; commenti di `:12-13` e `:107` con la 2025.

Stesso aggiornamento per `GPCChecker.Concrete/README.md:163-164` e `:176`,
`docs/migrazione-anthea/MIGRAZIONE_ANTHEA.txt:74` e `:260-261`,
`GPCChecker.Test.Concrete/DurabilityEdgeCaseTests.cs:122` («UNI 11104 (ANTHEA) C30/37» →
«UNI 11104:2016 e 2025 C30/37»). Nessun test controlla il testo di `:107`.

La pagina dei metodi `docs/metodi/ca.durabilita-copriferri.md` (entrata in develop con il merge
1fbaea61) dava la 2025 uguale al codice anche per a/c e cemento. È stata allineata in develop
7d86ffc7 (locale): edizioni distinte, differenze della 2025 dall'estratto del 28/07/2025 come fonte
secondaria da riscontrare, tabella 6.2 con le tre edizioni e riquadro C-1 riscritto. Con e4 la pagina
si aggiorna di nuovo in F2.9, dopo il riscontro del prospetto 6.

### Riquadro Wiki

`supporto/docs/guida-teorica-anthea.md:61` («Esposizioni e requisiti del materiale») e `:85`
(«Copriferro minimo e nominale»). Il paragrafo `:63` dichiara già edizione, prospetto ed eccezione
XF1; il riquadro resta per W1.

## D7-f Bridge Design, coefficiente e carico da traffico

**Categoria proposta**: scostamento-normativo per γQ; il carico equivalente è una semplificazione
intenzionale del predimensionamento. **Famiglia**: ponti, Bridge Design.

### Codice

- `X.Calculations/BridgeConcept.cs:99-102`: ipotesi modificabili `wear_load` 2,5 kN/m²,
  `traffic_load` 9 kN/m², `gamma_g` 1,35, `gamma_q` 1,5. Gli archivi le memorizzano (per esempio
  `supporto/esempi/bridge-design.anthea`).
- `X.Calculations/BridgeConcept.Calculation.cs:102-105`: G = 25 Acls + 9,81 massa acciaio/m +
  g2 · W + 8 kN/m per barriera; Q = 9 · W sull'intera larghezza dell'impalcato; trave continua sotto
  γG G + γQ Q su tutte le campate, senza assi tandem né inviluppo.
- `X.Calculations/BridgeConcept.AdvancedCalculation.cs:43-61`: lo stesso carico di progetto
  dimensiona aste, cavi, stralli, antenne e ancoraggi delle famiglie speciali.
- Ambito dichiarato a `BridgeConcept.cs:9`: predimensionamento, non verifica NTC.

### Norma e riscontro sul testo

- NTC 2018, Tab. 5.1.V, colonna A1: γG1 = 1,35; γG2 = 1,50 (1,35 per i permanenti non strutturali
  ben definiti, nota 2); γQ = 1,35 per le azioni variabili da traffico; γQi = 1,50 per le altre
  azioni variabili.
- NTC 2018 §5.1.3.3.5 e Tab. 5.1.II: corsie convenzionali di 3,00 m; corsia 1 con tandem di assi da
  300 kN e 9,00 kN/m²; corsia 2 con 200 kN e 2,50 kN/m²; corsia 3 con 100 kN e 2,50 kN/m²; altre
  corsie e area rimanente 2,50 kN/m².

### Segno

- γQ = 1,50 al posto di 1,35: cautelativo, +11,1 % sulla parte da traffico.
- 9 kN/m² sull'intera larghezza senza tandem: non cautelativo per le luci brevi, cautelativo per le
  luci medie e lunghe (vedi sotto).
- γG = 1,35 anche sui permanenti portati: non cautelativo rispetto a 1,50 se non sono ben definiti.

### Effetto quantificato

1. γQ 1,35 contro 1,50 su 225 casi: il predefinito e le 224 combinazioni famiglia × terreno × pila
   di `supporto/test/BridgeDesign.Checks/Program.cs:52-64`.
   - Momento positivo indicativo: −2,5 … −5,5 % (161 casi con momenti).
   - Famiglie a trave (10 su 14): costo, quantità e pali invariati. L'altezza viene dalle regole
     luce/altezza e nessuna quantità dipende dal momento fattorizzato.
   - Strallati: costo −1,4 … −1,8 %; archi con catena −3,3 … −3,6 %; reticolari −3,3 … −3,9 %;
     sospesi −4,2 … −5,2 %. Acciaio −1,9 … −4,4 %; numero di pali diverso in 21 casi su 64.
   - Avvisi: cambia solo il valore del carico medio sugli appoggi nel testo. Esclusioni
     dell'ottimizzazione: nessuna differenza.
   - Ottimizzazione del caso predefinito: 1 771 soluzioni valutate e 672 ammissibili in entrambi i
     casi; stessa migliore (travi a I in c.a.p., 3 campate, 2 573 558 €) e stessa graduatoria.
2. Carico equivalente sul caso predefinito (impalcato 20,20 m, carreggiata 19,20 m: 6 corsie
   convenzionali e 1,20 m residui): q dello Schema 1 = 67,5 kN/m contro 9 · 20,20 = 181,8 kN/m.
   Momento in mezzeria di una campata appoggiata, rapporto fra il codice e lo Schema 1 (valori
   caratteristici; tra parentesi con γQ 1,50 contro 1,35):

   | Luce | 10 m | 15 m | 20 m | 25 m | 30 m | 40 m | 60 m | 100 m | 200 m |
   | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
   | Codice/Schema 1 | 0,65 (0,73) | 0,85 (0,94) | 1,01 (1,12) | 1,14 (1,27) | 1,26 (1,40) | 1,45 (1,61) | 1,70 (1,89) | 1,99 (2,22) | 2,29 (2,54) |

   Le reazioni d'appoggio seguono lo stesso andamento (0,62 a 10 m, 1,44 a 40 m).
3. γG2 sul caso predefinito: G = 257,8 kN/m, di cui g2 = 74,5 kN/m (pavimentazione e barriere);
   qd = 620,7 kN/m con il codice, 604,6 kN/m con γG2 1,50 e γQ 1,35 (−2,6 %).

Evidenze: `out/f-bridge.csv`, `out/f-lm1.csv`, `out/f-bridge.txt`.

### Test e catture che lo fissano

Nessun test fissa γQ. `supporto/test/BridgeDesign.Checks` (Program.cs, AuditChecks,
OptimizationChecks, ExplorationChecks) e `BridgeDesign.IndependentChecks` usano il predefinito 1,5.

### Opzioni e raccomandazione

- f1: γQ 1,35 come predefinito dei documenti nuovi, con l'etichetta «γQ traffico (NTC Tab.
  5.1.V)»; gli archivi conservano il proprio valore;
- f1': dichiarare 1,50 come margine del predimensionamento;
- f2: per il carico equivalente, dichiarare il modello uniforme con i limiti quantificati e un
  avviso automatico per campate sotto 20 m, oppure introdurre il tandem dello Schema 1 (modifica del
  modello);
- f3: γG2 1,50 sui permanenti portati, oppure dichiararli ben definiti.

**Raccomandazione**: f1 (effetto piccolo, solo sulle famiglie speciali); f2 come dichiarazione con
avviso, valutando il tandem quando BridgeConcept passa in GPC.Design; f3 come dichiarazione.
Attuazione in F4.11.

### Riquadro Wiki

`supporto/docs/guida-teorica-anthea.md:892` («Acciai e carichi equivalenti», capitolo Bridge
Design).

## D7-g Testo del metodo nei report della sezione composta

**Categoria proposta**: difetto-legacy. **Famiglia**: ponti, sezione composta.

Questa voce non fa parte dei sei scostamenti originari di D7: è aggiunta in F0.6 perché ha la
stessa natura di scelta da decidere.

### Codice

- `X.Calculations/BridgeSection.Analysis.cs:17`: dal commit ANTHEA 0a63315 il risultato porta
  l'etichetta della scelta (`method`) al posto del metodo della libreria (`result.Method`); `:12` e
  `:19-20`: etichette «Cumulativo lineare», «Storico lineare», «Storico non lineare» (la vecchia
  «Cumulativo · metodo precedente» è convertita).
- `X.Core/ReportBridge.cs:80` («Metodo: » + `result.Method`), `X.Core/ReportBridge.History.cs:13`
  («ANTHEA · » + `result.Method`), `X.Desktop/Wpf/BridgeHistory.cs:60`. Il metodo della libreria è
  anche in `X.Calculations/BridgeSection.cs:14`.

### Norma

Nessuna. Il testo della libreria cita la norma, l'edizione e la versione del metodo.

### Segno

Nessun effetto numerico: stati e risultati vengono dalla libreria senza modifiche; cambia solo la
stringa.

### Effetto quantificato

Calcolato su `BridgeSection.Defaults()` e `supporto/esempi/sezione_mista_ponte.json` con i tre
metodi:

| Scelta | Testo nel report | Metodo della libreria |
| --- | --- | --- |
| Cumulativo lineare | Metodo: Cumulativo lineare | Sezione composta N–Mx · larghezze efficaci EN 1993-1-5:2006 · v1 |
| Storico lineare | Metodo: Storico lineare | Storico lineare · deformazioni e ritiro incrementali |
| Storico non lineare | Metodo: Storico non lineare | Storico non lineare · fibre e plasticità dell’acciaio |

Le baseline di Checker `GPCChecker.Test.BridgeAudit/Baselines/bridge-0.json` … `bridge-7.json`
registrano il metodo della libreria: sono parte dei 9 fallimenti noti di BridgeAudit, la cui
diagnosi è già stata fatta. Evidenze: `out/g-metodo.txt`.

### Opzioni e raccomandazione

- g1: il risultato porta sia l'etichetta della scelta sia il metodo della libreria; il report
  scrive «Metodo: Cumulativo lineare · Sezione composta N–Mx · larghezze efficaci EN 1993-1-5:2006 ·
  v1»;
- g2: ripristinare `result.Method`;
- g3: dichiarare.

**Raccomandazione**: g1, con numeri invariati. Il campo `Method` torna al metodo della libreria e le
baseline BridgeAudit non vanno riscritte. Attuazione fra le correzioni a costo zero (F0.10) o con
il modulo ponte (F3.12).

### Riquadro Wiki

Nessun riquadro tecnico; una nota sul report nella guida pratica della sezione composta.

## Come rieseguire i calcoli

L'harness è un progetto .NET 8 usa e getta, non versionato, in
`supporto/artefatti/refactoring/scostamenti/harness` del repository principale. Usa
`X.Calculations/bin/Release/net8.0` del worktree, cioè ANTHEA.Calculations e le DLL di
`lib/Checker`; gli input sono le fixture di Checker, `supporto/test/casi_confronto.json` e gli
esempi di `supporto/esempi`.

```
dotnet build X.Calculations\X.Calculations.csproj -c Release
dotnet build <harness>\Scostamenti.Harness.csproj -c Release
dotnet <harness>\bin\Release\net8.0\Scostamenti.Harness.dll ab   (oppure c, d, e, f, g)
```

Ogni modo scrive in `supporto/artefatti/refactoring/scostamenti/out` un riepilogo `.txt` e le
tabelle `.csv` per caso. Prima di confrontare, l'harness controlla che le varianti riproducano il
codice: fixture di fessurazione entro 1e-9, catena dei muri uguale a `RetainingWall.Calculate`,
ANTHEA uguale alla libreria per durabilità e copriferri, verifica metamorfica dei pali.

## Decisioni richieste

Per ogni voce l'utente sceglie un'opzione; la scelta si registra nel campo `decisione` del registro
con la data e porta lo stato a `dichiarato` o `da-correggere`.

1. D7-a: a3 + a4 (raccomandata), oppure a1, a2 o a3 da sola.
2. D7-b: b1 (raccomandata) o b2.
3. D7-c: c1 (raccomandata) o c2.
4. D7-d: d1 (raccomandata), d2 o d3; corrispondenza delle tecnologie della tabella.
5. D7-e: e1 dopo il riscontro della UNI 11104 (raccomandata; serve il testo) o e2. Superata il
   7/10 (C30/37 confermato); seguito: e4 in F2.9 dopo il riscontro del prospetto 6 su una copia con
   licenza (decisione del coordinatore su delega, da ratificare).
6. D7-f: f1, f2 come dichiarazione con avviso, f3 come dichiarazione (raccomandate), oppure le
   alternative.
7. D7-g: g1 (raccomandata), g2 o g3.

## Decisioni ed esiti (6-7 ottobre 2026)

| Voce | Decisione dell'utente | Esito | Stato |
| --- | --- | --- | --- |
| D7-a | correggere se è sbagliato, controllando EC2, NTC, annessi e Model Code | il ramo NTC è la Circolare 2019 alla lettera ([C4.1.10]); corretti anche EN, UNI, CNR-DT 200, DS. Da correggere MC2010 (R1), DIN kt (R2) e le citazioni (R3) | dichiarato |
| D7-b | per sole armature tese 1, per flessione 0,5: da vedere insieme | la regola dell'utente è quella della norma; il codice riconosce la flessione dalla barra compressa e non dall'asse neutro, quindi usa 1,0 nelle travi a semplice armatura inflesse | da decidere |
| D7-c | seguire la norma | c1 | corretto (d2cf2f7, anticipato da F4.9) |
| D7-d | seguire la norma | d1, archivi invariati con avviso | corretto (7c96c04, e05a05b, anticipato da F4.5) |
| D7-e | entrambi i riferimenti, scelta dell'utente; poi (7/10) «confermo la norma più aggiornata» | C30/37 è di UNI 11104:2025 (prospetto 6, secondo l'estratto del 28/07/2025) e 2016 (prospetto 5), come EN 206 F.1; C30/37 tenuto e citazioni di ANTHEA corrette (branch refactoring/d7e-uni11104); libreria in F2.9; seguito XF1 e composizione della 2025: e4 in F2.9 dopo il riscontro del prospetto 6 su copia con licenza (coordinatore su delega, da ratificare) | dichiarato; seguito da correggere |
| D7-f | cercare il metodo semplificato più vicino al reale | nessun carico uniforme regge entro il 10 % sulle travi continue; Schema 1 con linee di influenza e γQ 1,35 in GPC.Design, modello legacy per gli archivi ([ricerca](../../supporto/artefatti/refactoring/ricerca-f/rapporto.md), non versionata) | da correggere (F4.11) |
| D7-g | ripristinare | commit 0d861de | corretto |

Le ricerche hanno trovato altre quattordici voci (R1-R14 del registro). Queste sono a sfavore di
sicurezza, da decidere con priorità:

- R4: MC2010, θmin fisso a 20° (+38,8 % sul taglio resistente nell'esempio);
- R5: SLE con analisi non lineare e leggi di progetto (σc −31 %);
- R6: DIN, k5 dei trefoli 0,75 (fonte secondaria: 0,65);
- R10: ancoraggi senza limite di fctk alla C60/75 (lb,rqd −11 % con C90/105);
- R11: Cmin di default del copriferro NTC (−5 mm);
- R9: limite EC2 6.2.2(6) non controllato.

La verifica delle pagine dei metodi unite in Checker develop (F2.2) ha aggiunto sei voci, R16-R21, da
decidere con R4-R14 alla fine del refactoring (F5.15): a sfavore di sicurezza R16 (DS, torsione con
νv invece di νt: TRcd × 1,43 fino a fck = 50 MPa, × 1,61 a fck = 60 MPa), R17 (XC3 C25/30 nei profili EN
e UNI contro C30/37 del prospetto E.1N del DM 2012) e R18 (incrudimento dell'acciaio di progetto
riferito a fyk, circa +2 % in εud); R19 citazioni della torsione; R20 ρw,min DS non implementato; R21
condizione DIN su hc,eff con NominalCover invece del copriferro assegnato (segno da stabilire). R7 è
rettificata: NTC 2018 [4.1.38] dà 1 ≤ cot θ ≤ 2,5 anche in torsione (0,4 era della NTC 2008), quindi
«cot θ ≥ 1 anche in torsione pura» non è uno scostamento; resta da stabilire il segno del limite
1,3 (h − x) con barre tese fuori da Ac,eff (riquadro F-4 della pagina ca.fessurazione).

R15 (7/10/2026, corretto): nelle fasce interne dei fori h − x era εmax/|∇ε| anche con il gradiente
di rumore della trazione quasi uniforme (h − x ≈ 1e12 mm, wk ≈ 1e9 mm con barre distanziate).
Regola adottata per **decisione dell'utente del 7/10** ("Altezza lungo il gradiente"): h − x =
min[εmax/|∇ε|; h della sezione lungo il gradiente] (x ≥ 0) e, con |∇ε| h ≤ 1e-4 εmax, trazione
uniforme: k2 = 1 e h − x = h della sezione normale alla faccia (diametro per l'anello). Con l'asse
neutro interno nulla cambia rispetto a prima della correzione; k2 della fascia con l'asse neutro
che taglia la sezione resta la (7.13) della propria distribuzione (decisione confermata). Nel banco
c.a. cambiano 4 stati C1000H in sola trazione (+2,8e-8 relativo, esiti invariati).

Continuità: quando l'asse neutro entra nella sezione εmax/|∇ε| tende all'altezza lungo il
gradiente, quindi h − x e wk delle fasce sono continui, in flessione retta e deviata, nei cassoni
quadrati e non (X.Verifiche (k), piani prescritti a ±1e-6 mm dall'ingresso: differenze ≤ 2,2e-8).
Resta un salto alla soglia della trazione uniforme (un'eccentricità trascurabile) dove l'altezza
normale alla faccia e quella lungo il gradiente differiscono, cioè nei cassoni non quadrati e con
il gradiente obliquo: cassone 1000×1400 con foro 600×1000, N = 1500 kN, momento intorno a x:
fasce ±x da 1000 a 1400 mm, wk della fascia +40 %; intorno a y: fasce ±y da 1400 a 1000 mm, wk
della sezione 1,1538 → 0,8242 mm NTC; cassone 1000×1000 con foro 600×600, N = 3000 kN, momento
lungo (0,6; 0,8): tutte le fasce da 1000 a 1372,5 mm, wk della sezione 2,0114 → 2,7608 mm NTC
(+37 %). Nell'anello solo l'effetto del poligono che approssima il cerchio.

La regola intermedia (ANTHEA 76a2062, Checker d2f81329; commenti f2e3830 e 984cf849), con il
limite all'altezza normale alla faccia nella sezione interamente tesa, era continua alla soglia ma
saltava all'ingresso dell'asse neutro (stesso cassone 1000×1000 obliquo, M = 485,44 kNm: fascia
+x 1000 → 1183,68 mm, wk della sezione 3,9279 → 4,6494 mm NTC, +18,4 %): è stata ritirata con
ANTHEA 08c6dcc e Checker 12547700. Con la regola adottata la fascia +x vale 1183,68 mm ai due lati.
