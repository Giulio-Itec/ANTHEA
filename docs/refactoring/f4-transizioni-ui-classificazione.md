# F4 — classificazione puntuale delle 6805 differenze del confronto UI F3

Stato del 14/10/2026: **analisi in sola lettura, da sottoporre all'utente.**
Nessuna differenza è ammessa da questo documento. Riferimento
`tests/fixtures/f3/expected`, tolleranze e runner sono invariati; il documento
non è letto dai test. Integra il prospetto sommario
[f4-transizioni-ui-da-revisionare.md](f4-transizioni-ui-da-revisionare.md).

## Fonti e metodo

- Confronto: `C:/Users/g.pacini/.codex/worktrees/model-viewer-draft/ANTHEA/supporto/artefatti/ci/20261014-f4-1.8.0-full/ui_f3-equivalence/cattura/confronto.json`
  (458 file, 6805 differenze non ammesse in 65 file, nessun troncamento; più
  un volatile già ammesso, `tempi_ms` del palo elastico, fuori dal conteggio).
- **Commit della cattura: `82fcd8fc`** (summary.json della corsa, albero
  pulito), non `7569f6fc`. Vedi il punto V1 in «Regressioni probabili».
- Riferimento: catture D/E su `bce1e337` (ANTHEA 1.5.0, set V), con il solo
  aggiornamento editoriale Rev40 di `e8a6e38e` (F3-06). Ultimo confronto
  uguale: `20261013-133221-f3-final-ui-rules` (0 differenze). Dopo, nessuna
  corsa di `ui/f3-equivalence` fino alla 1.8.0: EXCEL-SOL (1.7.0) e F3.4-U5
  (1.7.1) non sono mai stati confrontati con il riferimento.
- Attribuzione: diff del codice fra `bce1e337` e `82fcd8fc` sui percorsi che
  producono ciascun file; testi nuovi cercati nelle versioni delle guide
  (`e8a6e38e`, `b6fac621`, `4a3044cb`, `82fcd8fc`); DOCX grezzi della cattura
  e dell'ultima corsa uguale per gli stili.
- Riproduzioni esatte usate come prova, non come stima:
  - stili dei report: la sequenza nuova coincide con quella di riferimento
    più un solo inserimento o rimozione (e la coda M–χ nel progetto); gli
    scarti per indice osservati e simulati coincidono uno per uno;
  - Bridge: tolta la stazione 105 del riferimento, le altre 209 coincidono
    bit per bit;
  - curvature: rapporto nuovo/vecchio esattamente 3,0 in tutti i 180 valori;
  - taglio dei muri: ΔVRd/N = 0,15·d/h in tutte le 160 verifiche di ogni muro.

File di lavoro (non versionati) in
`supporto/artefatti/refactoring/f4/classificazione-ui/`:
`classificazione.tsv` (una riga per ciascuna delle 6805 differenze con
origine, commit, voce, giudizio, valori), `riepilogo.json`,
`wiki-attribuzione.tsv`, `per-file/` (estratto del confronto per file) e gli
script `classifica.py`, `muri*.py`, `taglio*.py`, `ulp.py`, `stazioni.py`,
`shift.py`, `docxstili.py`, `wiki.py`.

## Riepilogo per origine

| # | Origine | Commit | Voce di registro | Differenze | Giudizio |
|---|---|---|---|---:|---|
| 1 | 1.7.1 / F3.4-U5: capitolo M–χ nel report di progetto | `4a3044cb` | F3-11 | 640 | atteso; mai confrontato |
| 2 | 1.7.1 / F3.4-U5: guide Rev42 nella Wiki | `4a3044cb` | nessuna voce editoriale | 3 | atteso, editoriale |
| 3 | EXCEL-SOL 1.7.0: guide Rev41 nella Wiki | `b6fac621` | nessuna voce editoriale (funzione: EXCEL-SOL-01/02) | 51 | atteso, editoriale |
| | **Subtotale anteriore a F4** | | | **694** | |
| 4 | Taglio dei muri con 0,15 σcp | `bbfe0638` + set W `3f9df6b8` | F4-03, F4-13 | 968 | atteso; cambia il calcolo |
| 5 | Curvatura totale dei muri | `bbfe0638` + `3f9df6b8` | F4-08, F4-13 | 180 | atteso; cambia il calcolo |
| 6 | Ultime cifre, adattatore muri c.a. | `bbfe0638` + `3f9df6b8` | F4-13 (API F4-06/F4-07/F4-11) | 1014 | atteso, ≤ 2 ulp |
| 7 | Ultime cifre, adattatore muri a gravità | `5afb47ab` + `3f9df6b8` | F4-14 (API F4-09) | 1166 | atteso, ≤ 4 ulp |
| 8 | Esiti tipizzati del palo elastico | `2fbe0710` + `3f9df6b8` | F4-15 (API F4-01) | 1096 | atteso, solo campi aggiunti |
| 9 | Campionamento Bridge senza stazione doppia | `bed30616` + `3f9df6b8` | F4-23 | 832 | atteso, spostamento di indice |
| 10 | Revisione dei fattori CO₂: testo e dati | `3ecaf795` | F4-28 | 14 | atteso |
| 11 | Revisione dei fattori CO₂: stili spostati | `3ecaf795` | F4-28 | 536 | atteso, artefatto di indice |
| 12 | Traffico: modello legacy e migrazione | `d4762582` | F4-29 | 22 | atteso |
| 13 | Norma CA esplicita: nota e dati | `9fee7a47` | F4-31 | 24 | atteso |
| 14 | Norma CA: paragrafo DIN duplicato rimosso, stili spostati | `9fee7a47` | F4-31 | 123 | atteso, artefatto di indice |
| 15 | Set W: versione di GPCChecker.Geotechnics | `3f9df6b8` | F4-35 | 4 | atteso |
| 16 | Guide Rev43 nella Wiki | `82fcd8fc` | nessuna voce editoriale | 88 | atteso, editoriale |
| 17 | Guide Rev43: ricerca Wiki spostata | `82fcd8fc` | nessuna voce editoriale | 44 | atteso, artefatto di indice |
| | **Subtotale F4** | | | **6111** | |
| 18 | Volatili o di cattura (F3-07/F3-08) | — | — | 0 | nessuno fra le 6805 |
| 19 | **Non spiegate** | — | — | **0** | vedi sotto |
| | **Totale** | | | **6805** | |

Le differenze marcate «artefatto di indice» (gruppi 9, 11, 14 e 17:
832 + 536 + 123 + 44 = 1535) derivano dal confronto per posizione:
un solo paragrafo, stazione o articolo in più o in meno sposta tutti gli
elementi successivi. Il contenuto realmente cambiato è indicato nel gruppo.

## Ripartizione per file

| File | Diff. | Origini |
|---|---:|---|
| `project/report.txt` | 322 | F3.4-U5 318; F4-03 2; F4-28 1; F4-31 1 |
| `project/report-structure.json` | 728 | F3.4-U5 322; F4-28 406 (405 stili spostati + 1 in coda) |
| `project/snapshot.json` | 4 | F4-28 1; F4-29 2; F4-31 1 |
| `sheets/geo_muri_sostegno/{default,example-cantilever,legacy-global-defaults}/result.json` | 3 × 718 | per muro: F4-03 320; F4-08 60; F4-13 338 |
| `sheets/geo_muri_sostegno/{…}/report.txt` (tre muri a mensola) | 3 × 2 | F4-03 |
| `sheets/geo_muri_sostegno/example-gravity/result.json` | 1166 | F4-14 |
| `sheets/geo_palo_orizzontale/single-soil-elastic/result.json` | 1096 | F4-15 |
| `sheets/str_bridge_design/{default,legacy-advanced-defaults}/result.json` | 2 × 416 | F4-23 |
| `sheets/str_bridge_design/{…}/report-structure.json` | 2 × 65 | F4-28 (spostamento) |
| `sheets/str_bridge_design/{…}/report.txt` | 2 × 1 | F4-28 |
| `sheets/str_bridge_design/{…}/{opened,visited,calculated-data,exported-data,reopened}.json` | 2 × 15 | F4-28 10; F4-29 20 |
| `sheets/str_palo/corpus-sezione-ca-taglio-din-rettangolare/report-structure.json` | 123 | F4-31 (spostamento) |
| `sheets/str_palo/corpus-…-din-rettangolare/report.txt` | 1 | F4-31 |
| `sheets/str_palo/{default,legacy-calculation-settings,legacy-workspace-segni}/*` | 3 × 7 | F4-31 |
| `sheets/geo_fondazioni_superficiali/{confirmed-actions,legacy-soil-profile}/{result.json,report.txt}` | 4 | F4-35 |
| `wiki/_wiki_guide_moduli_sezione-ca.{md,txt}` | 25 + 15 | EXCEL-SOL 27; F3.4-U5 3; Rev43 10 |
| `wiki/_wiki_manuale_calcestruzzo_profili-calcestruzzo.{md,txt}` | 16 + 8 | EXCEL-SOL 24 |
| `wiki/_wiki_guide_moduli-singoli_bridge-design.{md,txt}` | 23 + 13 | Rev43 |
| `wiki/_wiki_manuale_ponti_bridge-design.{md,txt}` | 19 + 12 | Rev43 |
| `wiki/_wiki_manuale_calcestruzzo-armato_portanza-sismica-…-rev07.{md,txt}` | 7 + 4 | Rev43 |
| `wiki/search.json` | 44 | Rev43 (spostamento) |

## Dettaglio dei gruppi

### 1–2. F3.4-U5, ANTHEA 1.7.1 (643)

- `project/report.txt`, 318: riga 4611 «Curva non calcolata con i dati
  correnti.» → «Ramo a momento crescente, N costante… My = 710,2349 kNm…»;
  aggiunte le righe 4613–4929: «MRd = 800,03 kNm; χy = 4,07E-3 1/m; χu =
  0,03 1/m», didascalia «Momento–curvatura · percorso assegnato» e la tabella
  T167 di 63 righe × 5 colonne (315 celle; ultima riga 800,03 · 0,03 · −3,50 ·
  3,50 · 16,90).
- `project/report-structure.json`, 322: 318 stili in coda (Normal, Caption,
  5 TableHeader, 310 TableText, Normal); tabelle 166 → 167, righe 1182 → 1245,
  celle 3990 → 4305, una figura in più (`$.images[21]`, 5800000 × 3625000 EMU).
- Wiki, 3: in `sezione-ca` il paragrafo Rev42 «Nel report di progetto la curva
  viene ricalcolata automaticamente con N, direzione, passi e opzioni salvati
  nel foglio…» (md righe 119–120, txt riga 93).

Giudizio: atteso. È la correzione di F3-11 (curva prima nulla); sono numeri
nuovi, non variazioni di numeri esistenti. L'audit stimava circa 1040
differenze da F3.4-U5: le 405 + 1 differenze di stile del report di progetto
non vengono invece da F3.4-U5 ma dall'inserimento F4-28 all'indice 742 (gruppo
11). Senza F4-28, la 1.7.1 avrebbe dato 322 differenze in quel file, non 728.

### 3. EXCEL-SOL, ANTHEA 1.7.0 (51, solo Wiki)

Testi delle guide Rev41 (`b6fac621`) nelle pagine `sezione-ca` (27) e
`profili-calcestruzzo` (24). Esempi: «I nomi Plastico ed Elastico descrivono i
modelli disponibili…» → «Nei nuovi fogli scegliere separatamente lo Stato
limite (SLU o SLV) e il Criterio resistente…»; «Le combinazioni di taglio
conservano nome, N, Mx, My, Vx, Vy, T anche in» → «Nel nuovo schema ogni
combinazione conserva insieme N, Vx, Vy, T, Mx e My,»; nuovi paragrafi su
Importa e sostituisci/aggiungi, Origine precedente, limiti di importazione.
Nessuna differenza di dati, risultati o report proviene da `504de76e`,
`9e1ac2ca`, `93a1eca9`: i documenti F3 restano nel formato precedente fino alla
conversione esplicita, come dichiarato in EXCEL-SOL-01. Giudizio: atteso,
editoriale; manca una voce di registro come F3-06 per Rev40.

### 4. F4-03, taglio dei muri senza staffe con σcp (968)

- Tre muri a mensola, 160 verifiche «taglio senza staffe» del fusto ciascuno
  (tutte quelle con N > 0, da 0,95 a 31,69 kN/m): 160 Resistance + 160 Ratio
  per muro. Le 320 verifiche di valle e monte (N = 0) non cambiano resistenza.
- Esempio: `$.verifiche_strutturali[503]`, Fusto z=3,00 m, SLU 5: VRd
  161,08379263671384 → 165,26654263671384 kN/m; η 0,16761 → 0,16337.
- Report del muro (T15 r18) e del progetto (T54 r18), Fusto z=3,00 m, SLU 10:
  «50,1 / 161,084» → «50,1 / 164,301»; η 0,311 → 0,305 (sei valori nei tre
  muri, due nel progetto).
- Riscontro indipendente: ΔVRd = 0,15·(N/(b·h))·b·d, cioè ΔVRd/N = 0,15·d/h,
  da 0,1220 (h = 257,5 mm) a 0,1320 (h = 400 mm) con h − d = 48 mm in tutte le
  sezioni; nessun caso in trazione, nessun esito cambiato.

Giudizio: atteso, correzione normativa NTC 2018 4.1.23 decisa in F4.3b. Rende
la verifica meno conservativa: richiede l'approvazione esplicita della
transizione.

### 5. F4-08, curvatura totale dei muri (180)

`$.combinazioni[*].Curvatures[*].Curvature` dei tre muri a mensola: 20 quote
del fusto nelle tre combinazioni SLE 1, SLE_FREQ 2 e Quasi permanente 3.
Rapporto esattamente 3,0 = 1 + φ con φ = 2 (es. 1,1256824786143121E-05 →
3,3770474358429364E-05 1/m; scarto massimo 0,00132 1/m). Nessuno
spostamento calcolato nelle fixture (`esercizio` vuoto) e nessun effetto sui
report. Giudizio: atteso (F4-08); vedi il punto da verificare V4.

### 6. F4-13, ultime cifre dell'adattatore dei muri in c.a. (1014)

Per ciascun muro a mensola 338 valori: N–M GPC Ratio 168 e Demand 21; taglio
senza staffe Ratio 129 (soprattutto valle/monte) e Demand 7; armatura massima
Resistance 6 e Ratio 6; `acciaio_kg_m` 1. Scarto relativo ≤ 2,6e-16, al più
2 ulp. Esempi: N–M Demand 2,1249000000000002 → 2,1249000000000007; armatura
massima 10600,000000000002 → 10600 mm²/m; peso 113,64020273977286 →
113,64020273977289 kg/m. Il nuovo percorso `StructuralChecksNative`
(`RetainingWall.Library.Structures.cs`, `bbfe0638`) converte le azioni in N e
N·mm e i risultati indietro. Per 1587 dei 2180 valori arrotondati dei gruppi
6 e 7 il valore nuovo è riprodotto esattamente da una conversione andata e
ritorno per 10³ o 10⁶ (in un verso o nell'altro) o dal rapporto |D|/C in unità
native. Esiti invariati.

### 7. F4-14, ultime cifre dell'adattatore dei muri a gravità (1166)

`example-gravity`: compressione Demand 223 e Ratio 191, flessione fondazione
Demand 180 e Ratio 156, taglio Demand 145 e Ratio 129, assenza trazione Ratio
78 e Resistance 64. Scarto ≤ 5e-16 relativo, al più 4 ulp (es. Resistance
1,5730000000000002 → 1,573 kN/m). Origine: `GravityChecksNative`
(`5afb47ab`), calcolo in GPC con N/mm e N·mm/mm. Esiti invariati.

### 8. F4-15, esiti tipizzati del palo elastico (1096)

Solo chiavi aggiunte in `armature.tratti[0]` di `single-soil-elastic`:
`Outcome`, `Execution`, `Data` in ciascuna delle 364 verifiche (1092) e nella
critica (3), più `da_completare_tipizzati` (1, otto voci). Valori: Outcome 2 =
NotSatisfied, Execution 1 = Completed, Data 1 = Insufficient, coerenti con
«Non soddisfatto», taglio non verificato e copriferro incompleto. Nessun valore
preesistente cambia. È l'unica fixture con tratti armati.

### 9. F4-23, campionamento Bridge (832)

In entrambe le fixture 210 → 209 stazioni: rimossa la stazione 105 (campata 3,
X = 52,5 m, M = 13374,331 kNm, V = 0, duplicato di quella con V = 9,1e-13).
Tutte le altre coincidono bit per bit; le 832 differenze (104 Moment,
104 Shear, 103 DeflectionMm, 102 X, 2 Span, 1 elemento rimosso per fixture)
sono solo lo spostamento degli indici. Nessun'altra chiave del risultato
cambia: quantità, costi e CO₂ identici.

### 10–11. F4-28, revisione dei fattori CO₂ (550)

- Testo/dati, 14: riga «Revisione fattori CO₂ del calcolo:
  F1A8366F8814A7B4…» nei due report Bridge e nel capitolo Bridge del progetto;
  chiave `environment` (fattori, revisione, storia vuota) nei cinque JSON di
  documento delle due fixture e nell'istantanea del progetto.
- Spostamento, 536: il paragrafo aggiunto all'indice 6 (Bridge) e 742
  (progetto) sposta gli stili successivi: 64 + 1 per fixture Bridge, 405 + 1
  nel progetto. Verificato che non c'è nessun altro cambio di stile.

### 12. F4-29, traffico (22)

`input.traffic_model` = «Uniforme equivalente (legacy)» e
`traffic_migration` = «Archivio v1: mantenuti modello uniforme e gamma_q
salvato» nei cinque JSON delle due fixture e nell'istantanea del progetto.
Nessun effetto sui risultati, coerente con la conservazione del modello
uniforme.

### 13–14. F4-31, norma CA esplicita (147)

- Tre fixture `str_palo` senza campo `normativa`: chiave
  `workspace_ca.migrazione_normativa` nei sei JSON (18); nel report la
  ripetizione «NTC 2018 · coefficienti della DLL…» diventa «Documento
  precedente senza normativa: mantenuto il profilo storico NTC 2018…» (3);
  stessa nota e chiave nel progetto (2).
- Fixture DIN con norma esplicita: rimossa la ripetizione «EC2 con parametri
  DIN…» (riga 11, 1); il primo paragrafo resta. La rimozione sposta 122 stili
  e ne toglie uno (123).
- F4-33 (`575e2396`) non produce differenze: gli input F3 non hanno campi
  mancanti, quindi nessuna `migrazione_input`.

### 15. F4-35, set W (4)

«Libreria GPCChecker.Geotechnics 0.1.3.3» → «0.3.0.0» e
`$.library_version` nelle due fixture delle fondazioni. Nessun valore di
calcolo cambia.

### 16–17. Guide Rev43 nella Wiki (132)

- 88 righe da `82fcd8fc`: guida Bridge Design (scheda Traffico, Prezzario,
  «Scegliere e cambiare prezzario», «Impostare il traffico del ponte», esempio
  con traffico uniforme), manuale Bridge Design (motore in GPC.Design,
  cataloghi CSV/XLSX/JSON, «Traffico mobile Schema 1», computo e inclusioni),
  `sezione-ca` («Controllare norma e dati ereditati»), manuale dei muri
  («curvature totali… senza moltiplicarla nuovamente per 1 + φ», «Taglio senza
  staffe e stato delle verifiche del muro»).
- 44 in `search.json`: un solo articolo inserito per ricerca, il resto è
  spostamento. «armature» e «taglio»: guida Bridge Design, per le nuove parole
  «armature» e «taglio»; «curvatura»: manuale dei muri, per la nuova parola
  «curvatura» (la ricerca usa i prefissi delle parole).

Giudizio: atteso, editoriale. Il prospetto attribuiva tutte le 186
differenze Wiki a Rev43: 51 sono Rev41 e 3 Rev42.

## Effetti sui risultati numerici di calcolo

| Gruppo | Valori | Entità | Esiti |
|---|---:|---|---|
| F4-03 taglio | 960 nei risultati + 8 nei report | VRd +0,116…+4,183 kN/m (+0,09…+2,60 %); η −0,0079 al massimo | invariati |
| F4-08 curvatura | 180 | ×3 esatto; Δ ≤ 0,00132 1/m; non usata da spostamenti o report delle fixture | — |
| F4-13 / F4-14 arrotondamenti | 2180 | ≤ 4 ulp (≤ 5e-16 relativo); invisibili nei report | invariati |
| F4-23 Bridge | 1 stazione rimossa | stazioni restanti identiche bit per bit | invariati |
| F3.4-U5 M–χ | numeri nuovi | curva prima assente | — |

Tutti gli altri gruppi aggiungono testi, chiavi o versioni senza cambiare
valori di calcolo. Nelle verifiche strutturali dei quattro muri (3 × 1304 +
1440) e nelle 364 del palo nessuno `Status` cambia.

## Non spiegate

**Nessuna.** Ogni differenza ha un commit, un percorso di codice o di testo e,
dove è numerica, un meccanismo riprodotto. Residuo di minima entità: per 593
dei 2180 arrotondamenti (575 del muro a gravità, 6 × 3 «armatura massima»
Resistance; vedi `ulp.py`) l'origine è certa, ma il valore non è riprodotto
uno per uno, perché il calcolo avviene nella libreria in unità native. Sono al
massimo 4 ulp.

## Regressioni probabili

**Nessuna** fra le 6805 differenze. Punti da verificare prima di ammettere le
transizioni:

1. **Commit non misurati (V1).** La cattura è di `82fcd8fc`; dopo vengono
   `7d3c9bc9` (F4-36), `2850c031` (F4-37), `ef1b0cff` (solo test),
   `660b4c71` (F4-38), `d66379ac` (F4-39) e commit di documentazione.
   - Differenza certa non censita: `d66379ac` aggiunge alla guida pratica due
     paragrafi sull'installazione 1.8.0 nella sezione «Avviare la versione
     aggiornata», quindi l'articolo `wiki/_wiki_guide_primi-passi_avvio-e-scelta-del-modulo`
     (`.md` e `.txt`) cambierà; possibili effetti su `search.json`.
   - F4-37: nei tre muri F3 a mensola (`two_zones` falso, `lower_height` 1,5)
     l'input nativo dei dettagli riceve ora 0 invece di 1,5; l'effetto su
     `acciaio_kg_m` e sui dettagli non è misurato (atteso nullo con una zona).
   - F4-36 (predalle) e F4-38 (esempio Wiki) non sono esercitati da questa
     cattura; con `str_mista_ponte` a zero differenze non sono attesi effetti.
   Serve una nuova corsa su `7569f6fc` prima di qualunque ammissione.
2. **Tolleranze (V2).** La regola API-01 (1e-12 relativo) vale solo per
   `results/geo_muri_sostegno/…:(Demand|Ratio)` della cattura headless, non per
   `sheets/…` né per Resistance e `acciaio_kg_m`. I 2180 arrotondamenti vanno
   ammessi per percorso, come richiesto dai controlli del prospetto.
3. **F4-03 (V3).** La correzione aumenta la resistenza: è una scelta normativa
   già decisa, ma l'utente deve approvare la transizione UI.
4. **F4-08 (V4).** Il fattore 1 + φ è applicato allo stesso modo alle
   combinazioni caratteristica, frequente e quasi permanente. La scelta
   discende dal φ unico del materiale già usato dal percorso storico; va
   confermata come ipotesi ingegneristica, non è un effetto del refactoring.
5. **Registro (V5).** Mancano voci editoriali per Rev41, Rev42 e Rev43
   (precedente: F3-06 per Rev40, con `editorial_updates` nel manifest).
6. **Bridge (V6).** Delle due stazioni doppie resta quella con V = 9,1e-13
   invece di quella con V = 0: effetto solo estetico.
7. **Osservazione (V7).** Nei documenti F3 il report Bridge riporta la
   revisione dei fattori CO₂, non quella del prezzario. `BridgeConceptExport`
   la scrive solo nel CSV, che questa cattura non contiene. Nessuna differenza
   ne deriva.

## Confronto con il prospetto sommario

| Famiglia del prospetto | Diff. | Correzione della classificazione |
|---|---:|---|
| Muri | 3326 | confermata: F4-03 966, F4-08 180, F4-13 1014, F4-14 1166 |
| Palo elastico | 1096 | confermata, F4-15 |
| Bridge Design | 994 | confermata: F4-23 832, F4-28 142, F4-29 20 |
| Fondazioni | 4 | confermata, F4-35 |
| Sezione CA | 145 | solo F4-31; F4-33 non produce differenze |
| Report e snapshot di progetto | 1054 | F3.4-U5 640, F4-28 408, F4-03 2, F4-31 2, F4-29 2 |
| Wiki e ricerca | 186 | Rev43 132, EXCEL-SOL/Rev41 51, F3.4-U5/Rev42 3 |
