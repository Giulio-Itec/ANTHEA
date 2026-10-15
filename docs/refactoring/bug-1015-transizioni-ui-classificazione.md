# Correzioni dei bug del 15-16/10 — classificazione puntuale delle differenze del confronto UI F3

Stato del 16/10/2026: classificazione completa e transizioni rigenerate.
`ui/f3-equivalence` passa con l'elenco nuovo:
`tests/fixtures/f3/transitions-f4.json`, 22126 voci, decisione D2 del 15/10.
Nessuna tolleranza nuova.

Il riferimento `tests/fixtures/f3/expected` non cambia. Il documento segue la
struttura di [f4-transizioni-ui-classificazione.md](f4-transizioni-ui-classificazione.md).
Le correzioni sono quelle di [analisi-bug-20261015.md](analisi-bug-20261015.md)
(sezione «Esito delle correzioni»), con le voci `BUG-*` del registro.

## Fonti e metodo

### Corsa

- Comando: `build/ci.ps1 -Stage build,ui -Only '^ui/f3-equivalence$' -GpcLibDir <lib-z-dev> -Tag f3-bug1016`.
- Commit `e34f176c`, albero pulito.
- Librerie: set candidato Z (`lib-z-dev`, manifest `7FD5DFF1…20B4B2`).
  - Checker `anthea-bug-1015` `6b83dfff`: GPCChecker.Geotechnics 0.4.0.0,
    Concrete 0.11.0.0, CompositeBridge 1.7.0.0, Design 0.21.0.0.
  - Model `bugint-model` `bf8563f5`: GPCModel 5.5.0.0, GPCModelData 0.0.2.14.
  - Stesso codice di libreria del rilascio Z.
- Cattura: `supporto/artefatti/ci/20261016-005646-f3-bug1016/ui_f3-equivalence/cattura`.
- Esito con l'elenco F4 precedente: 18120 differenze non ammesse, di cui 1436
  transizioni elencate e assenti.
- La corsa delle guide (`20261016-004452-guide-rev47-f3-cattura`, commit `8f5ace95`)
  dava 18285; quel confronto era troncato a 20000 dettagli.

### Confronto completo

- `confronto.json` del runner conserva al più 20000 differenze, comprese le
  transizioni ammesse.
- `tools/qa/f3_confronto_completo.py` esegue lo stesso comparatore
  (`ANTHEA.Testing compare`, stesse tolleranze) per gruppi di file e unisce i
  rapporti. Nessuna logica di confronto è reimplementata.
- Senza transizioni: 462 file, **22126 differenze non ammesse in 80 file**, nessun
  troncamento.
- Con l'elenco F4 precedente lo stesso programma riproduce le 18120 della corsa.

### Classificazione

Ogni differenza ha una voce e una regola:

1. **F4 invariata**: uguale (file, percorso, classe, a, b) a una transizione
   elencata. Resta la voce F4.
2. **Regola**: legata a una voce del registro.
   - Una transizione F4 il cui valore «dopo» o il cui percorso è cambiato prende la
     voce combinata `F4-… / BUG-…`.
   - «a» resta il valore del riferimento, «b» il valore nuovo.
3. **Transizioni F4 assenti**: spiegate a parte (sezione dedicata).

Strumenti delle regole:

- **Array dei muri.** Allineamento per identità:
  - combinazioni per nome;
  - verifiche per (combinazione, elemento, nome).

  Una differenza in posizione `[i]` è:
  - una riga gemella;
  - un elemento spostato con lo stesso valore;
  - una transizione F4 dell'elemento `[j]` spostata in `[i]`, con lo stesso
    valore «dopo»;
  - una chiave nuova.
- **Report di testo.**
  - Una replica in Python dell'accoppiamento di `BaselineComparer.Text`
    (scheletro, LCS, limite di 10⁷ celle) dice quali righe stanno dietro ogni
    differenza. Riproduce una per una le differenze dei 22 file di testo.
  - Un modello per contenuto allinea tabelle, righe e colonne. Toglie le righe
    gemelle «b» e trova colonne e paragrafi inseriti.
  - Ogni coppia accoppiata è una delle tre:
    - stesso elemento con contenuto cambiato: regola sul testo, per segmenti di
      parole;
    - elemento nuovo: voce del suo contenuto;
    - elementi diversi: spostamento. La voce è quella delle righe inserite o tolte
      che lo causano.
- **Contenuto riallineato** (controllo in più). Ogni riga ed elemento di «b» è
  confrontato con il suo corrispondente di «a», non con quello nella stessa
  posizione. Ogni cambio ha una regola o è il valore di una transizione F4.
  Questo esclude cambi veri nascosti dietro gli spostamenti:
  - muri: 968 valori F4-03, 180 F4-08, 1011 F4-13 e 1166 F4-14, tutti uguali al
    valore «dopo» elencato. Gli altri cambi sono solo le chiavi nuove;
  - report: solo cambi spiegati da MUR-1…MUR-4, SLU-2, PO-4/PO-5, PV-1, FES-2,
    F4-47, F4-03 e dalla versione di Geotechnics.

### File di lavoro

Non versionati, in `supporto/artefatti/refactoring/bug-1015/classificazione-ui/`:

| File | Contenuto |
|---|---|
| `confronto-completo.json` | confronto completo senza transizioni |
| `classificazione.tsv` | una riga per differenza: voce, gruppo, regola, valori; ingresso di `f3_transizioni.py --voci` |
| `classificazione-assenti.tsv` | le 1436 transizioni F4 assenti con la spiegazione |
| `classificazione-riallineato.tsv` | il controllo per contenuto |
| `classificazione-frasi.tsv` | i segmenti di testo cambiati con la voce attribuita |
| `classificazione.json`, `classificazione.log` | riepiloghi |
| `transitions-f4-prima.json` | l'elenco F4 sostituito |

Script nella stessa cartella: `classifica_bug1015.py`, `report_modello.py`,
`testo_confronto.py`, `verifica_replica.py`, `lcs_senza_limite.py`.

## Riepilogo per origine

| # | Origine | Voce | Differenze | Natura |
|---|---|---|---:|---|
| 1 | Transizioni F4 invariate | F3-11 315; F4-03/F4-13 666; F4-08/F4-13 180; F4-13 621; F4-14 753; F4-15 1096; F4-23 832; F4-28 445; F4-29 22; F4-31 124; F4-40 384; F4-47 4 | 5442 | uguali all'elenco precedente |
| 2 | Transizioni F4 spostate dalle gemelle | F4-03 / F4-13 / BUG-MUR-1 120; F4-13 (…) / BUG-MUR-1 144; F4-14 (F4-09) / BUG-MUR-1 148 | 412 | stesso valore F4, indice nuovo |
| 3 | M–χ del report di progetto spostato di 274 righe | F3-11 / BUG-MUR-1 + BUG-MUR-2 317 (+1 struttura); F3-11 / BUG-MUR-1 1 | 319 | stesse righe F3-11, numerate dopo le righe nuove dei muri |
| 4 | ε0 della riga limite del M–χ | F3-11 / BUG-MUR-1 + BUG-MUR-2 + BUG-SLU-4 | 1 | −3,50 → 8,03 ‰ |
| 5 | Versione di Geotechnics del rilascio Z | F4-35 / BUG-MUR-1 + BUG-MUR-2 + BUG-PV-1 | 4 | 0.3.0.1 → 0.4.0.0 |
| 6 | Righe gemelle «b» dei muri | BUG-MUR-1 | 11631 | righe nuove, spostamenti di indice e di riga, chiave `heel_surcharge`, verifiche governate da una gemella |
| 7 | Gemelle e passiva insieme | BUG-MUR-1 + BUG-MUR-2 | 3540 | spostamenti dove agiscono righe gemelle e colonne nuove; testi con le due regole |
| 8 | Passiva come resistenza di progetto | BUG-MUR-2 | 674 | chiavi nuove, colonne «Ed scorr.» e «Ed rib.», colonna γR,e, testi |
| 9 | Passiva e terreno di valle sopra il taglio | BUG-MUR-2 + BUG-MUR-3 | 9 | un avviso e una riga del report per muro |
| 10 | Braccio H + t dello spostamento in testa | BUG-MUR-4 | 4 | frase del report |
| 11 | Palo elastico: sezione critica e sviluppo | BUG-PO-4 7; BUG-PO-4 + BUG-PO-5 5; BUG-PO-5 9 | 21 | sezione critica a x = 4 m, sviluppo da completare |
| 12 | N costante fuori dal campo | F2-17 (SLU-2) | 47 | «Fuori dominio», η 1,84 e 2,69 |
| 13 | Azioni per condizione e verso, avviso di Nq | BUG-PV-1 3; BUG-PV-3 1 | 4 | chiave `azioni_condizione`, criteri del report, avviso z/D |
| 14 | Copriferro della formula dalle barre | BUG-FES-2 | 4 | frase del report breve |
| 15 | CLS teso dello storico non lineare | BUG-PON-2 | 13 | chiave `cls_teso_nl` = «Trascurato» |
| 16 | Articoli della stratigrafia nella Wiki | F4-51 | 1 | `esito.txt`: 71 → 73 articoli |
| 17 | **Non spiegate** | — | **0** | |
| | **Totale** | | **22126** | |

### Artefatti di posizione

Le differenze di posizione sono **14 910**, più 730 transizioni F4 spostate:

| Dove | Differenze |
|---|---:|
| Array JSON dei muri (gemelle al posto di altre righe, spostamenti di indice, elementi in coda) | 9699 |
| Report di testo, accoppiamento di righe diverse | 4575 |
| Report di testo, righe gemelle accoppiate a righe di «a» | 532 |
| Report di testo, colonne nuove accoppiate a colonne di «a» | 104 |
| Transizioni F4 spostate: valori dei muri (412) e righe M–χ del report di progetto (318) | 730 |

Derivano dal confronto per posizione. Il contenuto realmente cambiato è descritto
nei gruppi.

Nel report di progetto la LCS è saltata. La zona diversa va dalla riga 668 alla
4611 di «a» e alla 5202 di «b»: 17 886 040 celle, oltre il limite di 10⁷ di
`BaselineComparer.Text`. Quindi le righe sono accoppiate per posizione e quasi
ogni riga dopo il capitolo dei muri diventa una differenza:

- per posizione: **4334** differenze;
- con la LCS calcolata (replica senza limite): **803**.

## Ripartizione per file

| File | Diff. | Voci |
|---|---:|---|
| `sheets/geo_muri_sostegno/{default,example-cantilever,legacy-global-defaults}/result.json` | 3 × 2781 | MUR-1 2124; F4-03 220 + 40 spostate; F4-13 207 + 48 spostate; F4-08 60; MUR-2 79; MUR-1 + MUR-2 2; MUR-2 + MUR-3 1 |
| `sheets/geo_muri_sostegno/example-gravity/result.json` | 3767 | MUR-1 2784; F4-14 753 + 148 spostate; MUR-2 79; MUR-1 + MUR-2 2; MUR-2 + MUR-3 1 |
| `sheets/geo_muri_sostegno/{3 a mensola}/{calculated-data,exported-data,reopened}.json` | 9 × 33 | MUR-1 22; MUR-2 11 |
| `sheets/geo_muri_sostegno/example-gravity/{stessi tre}.json` | 3 × 33 | MUR-1 22; MUR-2 11 |
| `sheets/geo_muri_sostegno/{3 a mensola}/report.txt` | 3 × 481 | MUR-1 352; MUR-1 + MUR-2 80; MUR-2 45; F4-03 2; MUR-2 + MUR-3 1; MUR-4 1 |
| `sheets/geo_muri_sostegno/example-gravity/report.txt` | 484 | MUR-1 357; MUR-1 + MUR-2 80; MUR-2 45; MUR-2 + MUR-3 1; MUR-4 1 |
| `sheets/geo_muri_sostegno/*/report-structure.json` | 4 × 2 | righe +56 (MUR-1), celle +272 (MUR-1 + MUR-2) |
| `project/report.txt` | 4334 | MUR-1 + MUR-2 3208; MUR-1 772; F3-11 spostate 317 + 1 (SLU-4); MUR-2 35; MUR-2 + MUR-3 1 |
| `project/report-structure.json` | 623 | F3-11 315; F4-28 302; F4-40 4; righe e celle F3-11 / MUR 2 |
| `project/snapshot.json` | 37 | MUR-1 22; MUR-2 11; F4-29 2; F4-28 1; PON-2 1 |
| `sheets/geo_palo_orizzontale/single-soil-elastic/result.json` | 1114 | F4-15 1096; PO-4 7; PO-5 7; PO-4 + PO-5 4 |
| `sheets/geo_palo_orizzontale/single-soil-elastic/report.txt` | 3 | PO-5 2; PO-4 + PO-5 1 |
| `sheets/str_palo/legacy-calculation-settings/{result.json,report.txt,short.txt}` | 20 + 19 + 10 | F2-17 (SLU-2) 47; F4-40 1; FES-2 1 |
| `sheets/str_palo/{default,corpus-…-din-rettangolare,legacy-workspace-segni}/short.txt` | 3 | FES-2 |
| `sheets/str_mista_ponte/{default,legacy-web-and-accessories}/{opened,visited,calculated-data,result,exported-data,reopened}.json` | 12 | PON-2 |
| `sheets/geo_palo_verticale/regressione-palo_storico_0/{result.json,report.txt}` | 2 + 1 | PV-1 2; PV-3 1 |
| `sheets/geo_micropalo_verticale/regressione-micropalo_igu_0_nessuna-riduzione/result.json` | 1 | PV-1 |
| `sheets/geo_fondazioni_superficiali/{confirmed-actions,legacy-soil-profile}/{result.json,report.txt}` | 4 | F4-35 / rilascio Z |
| `esito.txt` | 1 | F4-51 |
| Bridge Design, materiali, altre `str_palo` (struttura e report) | 1501 | F4-23 832; F4-28 142; F4-29 20; F4-31 124; F4-40 379; F4-47 4: tutte invariate |

## Dettaglio dei gruppi

### Righe gemelle «b» dei muri (MUR-1, commit `774d5a9b`, `1d1d003b`, `f365af44`)

In ognuno dei quattro muri le combinazioni SLU 8–11 hanno il sovraccarico
uniforme variabile (γQ 1,5). Ognuna è seguita dalla gemella SLU 8b…11b, con la
stessa spinta e `heel_surcharge` falso.

| Array | a mensola | a gravità |
|---|---|---|
| `combinazioni` | 11 → 15 | 11 → 15 |
| `input.combinations` | 11 → 15 | 11 → 15 |
| `verifiche_strutturali` | 1304 → 1784 (4 × 120) | 1440 → 2160 (4 × 180) |
| `verifiche_geotecniche` | 32 → 48 | 32 → 48 |

Nei dati dei fogli (`combinations`) e nell'istantanea del progetto
(`fogli[4].dati.combinations`) le combinazioni passano da 11 a 15.

**Ordine conservato.** Ogni gemella segue la sua combinazione. Gli altri elementi
restano nello stesso ordine, spostati:

| Caso | Differenze | Voce |
|---|---:|---|
| Elementi in coda | 2308 | MUR-1 |
| Gemelle al posto di altre righe | 5192 | MUR-1 |
| Elementi spostati con valore uguale | 2199 | MUR-1 |
| Valori F4 spostati di indice | 412 | `F4-… / BUG-MUR-1` |

Esempio di valore F4 spostato: `$.verifiche_strutturali[945].Resistance`
(F4-03, 128,63783970021547) è ora in `[1065]`.

Delle 412 voci, 13 del muro a gravità hanno un valore che compare due volte. La
transizione F4 in `[j]` resta invariata, perché l'elemento che ora occupa `[j]` ha
gli stessi valori. Lo stesso valore spostato in `[i]` prende la voce combinata.

**Chiavi nuove (MUR-1).** `VariableSurchargeOnHeel`, `Factors.heel_surcharge` e
`heel_surcharge`: 275 differenze.

**Report di ogni muro.**

- 56 righe gemelle (248 celle) in nove tabelle.
- Il paragrafo «Righe «b»: gemelle delle combinazioni SLU, sismiche ed eccezionali…».
- La nota «q variabile solo nella spinta» nella matrice dei fattori.
- La struttura passa da 348 a 404 righe (a gravità da 222 a 278).

**Verifiche governate da una gemella.** Sono le uniche variazioni di valori
visibili.

| Verifica | Muri a mensola | Muro a gravità |
|---|---|---|
| Scorrimento | SLU 10 → 10b, η 0,679 → 0,778 (resistenza 93,771 → 81,799 kN/m) | SLU 10 → 10b, 0,928 → 0,951 |
| Ribaltamento | SLU 8 → 8b, 0,242 → 0,291 | SLU 10 → 10b, 0,355 → 0,369 |
| Capacità portante | SLU 11 → 10b, 0,269 → 0,322 | SLU 10 → 10b, 0,505 → 0,55 |
| Contatto | SLU 6 → 9b, 0,04 → 0,105 | SLU 10 → 10b, 0,11 → 0,135 |
| N–M del fusto, Monte z = 1,80 m | SLU 10 → 10b, 0,236 → 0,241 | — |
| Valle z = 0,25 m: compressione, flessione e taglio | — | da SLU 11 a 11b, η +0,002…+0,008 |

- I valori coincidono con il massimo di `verifiche_geotecniche` del JSON.
- Le 496 verifiche gemelle di ogni muro a mensola e le 736 del muro a gravità sono
  tutte soddisfatte o con contatto in compressione. η massimo: 0,778 e 0,951.
- Nessun esito cambia. La correzione rende le verifiche più cautelative, come
  atteso dalla voce.

### Passiva come resistenza di progetto (MUR-2, commit `2ade15d8`)

**Chiavi nuove**, 451 differenze:

- combinazioni: `PassiveFactor`, `SlidingDemand`, `OverturningDemand`,
  `SoilAudit.passiva_progetto_kN_m`, `SoilAudit.passiva_fattore`,
  `Factors.rpassive`;
- dati: `rpassive` = 1 negli SLE, 1,4 negli SLU.

**Report.**

- Colonne nuove «Ed scorr. [kN/m]» e «Ed rib. [kNm/m]» nelle tabelle
  dell'equilibrio: 24 celle per muro più quelle delle righe gemelle.
- Nota «H e Mrib sono i valori dell'equilibrio…».
- Colonna dei fattori «γMφ / γRsc / γRrib / γRport / γR,e».
- Dettagli della passiva: «Passiva mobilitata ηp·Pp», «γR,e della passiva»,
  «Passiva di progetto ηp·Pp/γR,e», «Passiva usata nell'equilibrio».
- Frasi «frazione ηp=0 (al più 0,5…)» e avvisi 2, 4, 6 e 7.

Nelle fixture la passiva non è richiesta (ηp = 0): nessun numero di calcolo cambia.
`SlidingDemand` è la spinta motrice, per esempio 47,2075 kN/m in SLE 1.

### Terreno di valle sopra il taglio e braccio H + t (MUR-3 `84ce57d8`, MUR-4 `1a2e0958`)

Cambiano solo i testi:

- avviso 6 e riga 11 del report (con MUR-2): «…con il terreno di valle che grava
  sul paramento inclinato sopra il taglio…»;
- paragrafo dei cedimenti: «u totale = u fusto + H/K − θ (H + t): la rotazione θ è
  rigida attorno alla base…».

Le fixture hanno Dv = 0 e non calcolano spostamenti: nessun valore cambia.

### Report di progetto (capitolo del muro e M–χ)

**Capitolo del muro** (`fogli[4]`, uguale a `default`): stesse righe gemelle,
colonne e testi del report del muro.

**Effetto sul confronto.**

- Le righe nuove (+274) e il primo testo cambiato alla riga 668 rendono la zona
  diversa troppo grande per la LCS. Da lì l'accoppiamento è per posizione.
- 3731 differenze sono spostamenti:
  - 524 MUR-1, dove a monte ci sono solo righe gemelle;
  - 3207 MUR-1 + MUR-2, dopo le prime colonne nuove.
- 248 accoppiano righe gemelle a righe di «a», 24 colonne nuove a righe di «a».
- 13 sono testi cambiati al loro posto: avvisi e dettagli della passiva della
  tabella T49.
- 318 sono le righe M–χ (sotto).
- Il controllo per contenuto trova sotto gli spostamenti solo i cambi del capitolo
  del muro e le transizioni F4 già elencate:
  - F4-47, cloruri e UNI 11104: ancora alle righe 2632 e 2634 di «b»;
  - F4-03, «50,1 / 164,301»;
  - F4-28, revisione CO₂;
  - F4-40, nota tolta.

**M–χ (F3-11; SLU-4 `298be57e`).**

- Le 318 righe sono le stesse, numerate 274 righe più in basso: 317 voci
  `F3-11 / BUG-MUR-1 + BUG-MUR-2`.
- Una sola cella cambia, l'ultima riga della tabella T167, colonna ε0: −3,50 →
  8,03 ‰ (`… + BUG-SLU-4`).
- La riga limite è ora riferita all'origine degli assi locali, come le altre.
  χ, M ed εc,comp (3,50) non cambiano.

**Struttura.** Righe 1182 → 1301 (+56 gemelle +63 M–χ), celle 3990 → 4577 (+272
+315). Prima 1245 e 4305 (F3-11).

### Palo elastico (PO-4 `564bf02a`, PO-5 `47ee9517`)

`single-soil-elastic/result.json`, `armature.tratti[0]`:

- **Sezione critica (PO-4).** Da x = 0 (M = 1,4·10⁻¹⁰ kNm, η 1,3·10⁻¹³) a
  x = 4 m: N = 78,54 kN, V = 0,53 kN, M = 242,20 kNm, η 0,2174; MRd± 1088,1 → 1114,1
  kNm.
- **Sezione critica (PO-4 + PO-5).** `DevelopmentAvailable`, `Usable MRd±` e il
  messaggio: alla quota nuova lo sviluppo è disponibile.
- **Sviluppo (PO-5).**
  - Alle quote di testa e di punta (M ≈ 0, `verifiche[0]` e `[363]`) lo sviluppo
    non è richiesto: `DevelopmentAvailable` null, messaggio senza «Sviluppo delle
    barre o giunti non disponibili».
  - «Sviluppo barre insufficiente…» passa da `non_soddisfatti` a `da_completare`
    («Da completare: …»).
- **Report.** Le stesse frasi alle righe 5398/5404 e 5473.
- Le 1096 voci F4-15 (esiti tipizzati) restano uguali.

### Sezione c.a., N costante fuori dal campo (SLU-2, `9a9d46d6`, voce F2-17)

`str_palo/legacy-calculation-settings`, criterio «N costante», N = 2500 kN di
trazione oltre NRd,t.

- Domini 3D SLU e SLV: «Checker: punto resistente non convergente» → «Fuori
  dominio (N fuori dal campo [NRd,c; NRd,t])».
- Il punto è a eccentricità costante: NRd 1357,85 e 930,96 kN, η = |N/NRd| 1,841
  e 2,685.
- Il riepilogo passa da «DA COMPLETARE» a «NON VERIFICATA», «Combo 1», 1/1.
- Report: tabelle 1, 13, 14.
- Report breve: tabella 2 e titolo «Una o più verifiche non soddisfatte».
- Totale 47 differenze. Il caso «N e Mx costanti» (BUG-SLU-2b) non è nelle fixture.

### Altri gruppi

- **FES-2** (`73f2bdff`): riga 22 di `short.txt` nelle quattro fixture `str_palo`.
  «(vuoto = automatico)» → «(c vuoto: copriferro delle barre efficaci verso la
  faccia tesa; interasse vuoto: automatico)».
- **PV-1** (`4aed55b9`):
  - chiave `azioni_condizione` nel palo e nel micropalo verticale;
  - criteri del report: le regole del peso sostituiscono «La sottospinta agisce
    separatamente sul peso nelle azioni».
- **PV-3** (Geotechnics 0.4): l'avviso «ATTENZIONE Nq…» diventa la nota del tratto
  superficiale «…a favore di sicurezza».
- **PON-2** (`8df78f81`): chiave `cls_teso_nl` = «Trascurato» nei dati e nel
  risultato delle due fixture `str_mista_ponte` e nell'istantanea del progetto.
- **Versione di Geotechnics**: «0.1.3.3» → «0.4.0.0», prima «0.3.0.1» (F4-35).
  - Il set Z porta GPCChecker.Geotechnics 0.4.0.0.
  - Checker `3ae0cdb6` motiva il secondo numero con le API nuove di MUR-1 (righe
    gemelle), MUR-2 (fattore della passiva) e PV-1 (azioni per condizione).
- **F4-51**: `esito.txt` conta 73 articoli. Sono i due articoli della stratigrafia
  (Rev46), già nel riferimento della Wiki con `editorial_updates_rev47`.

## Transizioni F4 non più presenti (1436)

Tutte spiegate (`classificazione-assenti.tsv`):

| Voce | Assenti | Spiegazione |
|---|---:|---|
| F4-03 / F4-13, F4-13, F4-14 nei muri | 300 + 393 + 413 | L'elemento è spostato dalle gemelle. Il valore F4 è nel nuovo indice di «b», verificato uno per uno. In 399 casi c'è la voce combinata `F4-… / BUG-MUR-1`. Nei 707 restanti il nuovo indice supera la lunghezza dell'array di «a», quindi il comparatore elenca l'elemento intero come elemento aggiunto (MUR-1, coda). |
| F3-11 nel report di progetto | 318 + 2 | Sostituite dalle voci `F3-11 / BUG-…` con lo stesso contenuto. Righe M–χ e struttura. |
| F4-35 | 4 | Sostituite da `F4-35 / BUG-…` (0.4.0.0). |
| F4-03, F4-28, F4-40, F4-47 nel report di progetto | 2 + 1 + 1 + 2 | Il contenuto F4 è ancora in «b», ma l'accoppiamento per posizione lo confronta con un'altra riga. |

## Effetti sui risultati numerici

| Gruppo | Valori | Esiti |
|---|---|---|
| MUR-1 | Verifiche nuove delle gemelle. Le verifiche governanti di scorrimento, ribaltamento, portanza e contatto passano alle gemelle con η più alto: +0,002…+0,099. | Invariati, tutte soddisfatte. |
| MUR-2, MUR-3, MUR-4 | Nessun valore di calcolo: passiva non richiesta, Dv = 0, spostamenti non calcolati. | — |
| PO-4 | Sezione critica a x = 4 m, η 0,2174 invece di 1,3·10⁻¹³ alla testa. | — |
| PO-5 | Lo sviluppo insufficiente passa dai non soddisfatti ai punti da completare. | Il tratto resta «NON VERIFICATO» per il passo delle staffe. |
| SLU-2 | η 1,841 e 2,685 dove prima mancava. | Da «DA COMPLETARE» a «NON VERIFICATA». |
| SLU-4 | ε0 della riga limite del M–χ −3,50 → 8,03 ‰. | — |

Gli altri gruppi aggiungono chiavi, colonne, testi o una versione.

## Correzioni senza differenze nelle 36 fixture

Nessuna differenza da queste correzioni:

- SLU-1, SLU-3, SLU-5, SLU-6;
- FES-1;
- TAG-1, TAG-2, TAG-3;
- DET-1, DET-2, DET-3;
- ARM-1…ARM-4;
- PV-2, PV-4;
- PO-1, PO-2, PO-3, PO-6;
- MUR-5;
- GEO-1, GEO-2;
- PON-1, PON-3, PON-4, PON-5;
- N1–N6, TS-1.

L'assenza di differenze vale per le fixture catturate e non dimostra che queste
correzioni siano senza effetto in generale. Per esempio DET-3 non compare perché
tutte le fixture usano B450C.

## Non spiegate

**Nessuna.** Le 22126 differenze e le 1436 transizioni assenti hanno ognuna una
regola legata a una voce del registro. Lo stesso vale per i cambi trovati dal
controllo per contenuto. Nessuna regressione probabile.

## Osservazioni

1. **Limite della LCS.** Il report di progetto supera il limite di 10⁷ celle di
   `BaselineComparer.Text`.
   - Per posizione: 4334 transizioni. Con la LCS sarebbero 803.
   - Ogni modifica futura del capitolo dei muri o dei capitoli successivi sposta di
     nuovo tutte le righe.
   - L'elenco pesa 5,6 MB.
   - Alzare il limite (circa 18·10⁶ celle, 72 MB di memoria) cambierebbe il metodo
     del confronto. Va deciso a parte, non con questo aggiornamento.
2. **Confronto troncato.** Con 22126 transizioni ammesse anche il `confronto.json`
   di una corsa che passa è troncato (`dettagli_troncati`). Per le prossime
   rigenerazioni serve `tools/qa/f3_confronto_completo.py`.

## Come rigenerare

1. Corsa: `build/ci.ps1 -Stage build,ui -Only '^ui/f3-equivalence$' -GpcLibDir <set> -Tag <tag>`.
2. Confronto completo senza transizioni: `python -I tools/qa/f3_confronto_completo.py
   --a tests/fixtures/f3/expected --b <cattura> --tool <copia di tests/ANTHEA.Testing/bin/…/net8.0>
   --tolerances tests/ANTHEA.Testing/tolerances.json --out <confronto-completo.json>`.
3. Classificazione: `classifica_bug1015.py --confronto <confronto-completo.json>
   --precedenti tests/fixtures/f3/transitions-f4.json --a tests/fixtures/f3/expected --b <cattura> --out <classificazione>`.
   - Esce con 1 se resta una differenza senza regola.
   - Le differenze nuove richiedono regole nuove, legate alla loro voce.
4. Elenco: `python -I tools/qa/f3_transizioni.py --confronto <confronto-completo.json>
   --voci <classificazione>.tsv --commit <commit> --radice . --registro-cattura <cattura>`.
   - Riscrive `transitions-f4.json`.
   - Nel manifest aggiorna solo impronta e numero delle voci di `transitions`.
5. Nuova corsa: `ui/f3-equivalence` deve passare.

Corse di questo aggiornamento, in `supporto/artefatti/ci/`:

| Corsa | Esito |
|---|---|
| `20261016-005646-f3-bug1016` | classificata |
| `20261016-012815-f3-bug1016-verifica` | PASS: 22126 transizioni elencate e ammesse, 0 non ammesse; cattura uguale alla precedente su 462 file |
| `20261016-012957-f3-bug1016-prove` | Testing.Tests PASS (8 controlli delle transizioni); Architecture.Tests PASS (370 controlli) |
