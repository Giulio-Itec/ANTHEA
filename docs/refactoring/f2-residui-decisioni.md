# F2 — residui: decisioni per la chiusura

Preparato il 15/10/2026, dopo la chiusura di F4, sul ramo `refactoring/f2-residui` (da `main` `7cb08295`). Ordine
concordato con l'utente: residui di F2, poi F2-7 punto 3, analisi dei bug, rilettura delle pagine dei metodi e F5.
Inventario in sola lettura di piano, decisioni, registro e micro-piani (`supporto/artefatti/refactoring/piano/`,
`f27-f28-progetto/`). Non cambia gli stati di [piano.md](piano.md) e del [registro](registro-differenze.json).

## Già deciso il 15/10, non riproposto

- **F2-7 punto 3**, zona tesa senza armatura ([f2-7-rifiuti-sle.md](f2-7-rifiuti-sle.md) §4): con barre tutte
  compresse e zona tesa senza armatura la verifica a fessurazione è **non soddisfatta**, con il motivo; l'utente,
  sull'esempio della R300x500 armata solo in basso (zona tesa di circa 20 cm): «Non soddisfatta». Con l'asse neutro
  nel copriferro e nessuna barra tesa la sezione non fessura: wk = 0, come oggi. Vale anche per la faccia senza
  barre di una sezione interamente tesa. Il calcestruzzo teso non entra nella verifica wk (domanda dell'utente
  «dovrebbe reagire a trazione il cls???»). Cambio nella libreria, differenza voluta nel registro.
- **R1 e R2** (τbms e β del fib MC2010, kt della DIN): «Correggi su fonti secondarie». Correzioni del rapporto
  `supporto/artefatti/refactoring/ricerca-a/rapporto.md` §6.7, con l'opzione della regola precedente per le fixture
  congelate; certezza dichiarata «probabile» (Tab. 7.6-2 e NA DIN non lette in originale). Entrambe le regole
  attuali sono a favore di sicurezza: wk scende fino al 22–33 % con MC2010 a lunga durata, di circa lo 0,5 % con DIN
  a breve durata.

Le due modifiche della libreria vanno in un solo set di `lib/Checker` con le correzioni che usciranno dall'analisi
dei bug, per fare un solo full a schermo.

## Prova nuova: corsa baseline contro B4

`supporto/artefatti/ci/20261015-124031-f2res-b4` (worktree, `7cb08295`), profilo baseline con `-BaselineRef
F2-B4\libreria`:

- **sezioni c.a. (`str_palo`), tensioni e fessurazione: nessuna differenza**;
- muri: cambiano solo il taglio senza staffe (σcp, F4-03), le resistenze N–M entro tolleranza, l'armatura massima
  (ultime cifre) e le curvature (F4-08), tutte modifiche del 14/10 sera, successive a B4; nessuna verifica di
  esercizio;
- `fallbacks.json`: cambiano i chiamanti registrati, per lo spostamento D3;
- 389 file aggiunti: il corpus è cresciuto dopo B4 (pali, micropali, materiali, ponte).

Il confronto denso con `F2-pre-m4-v2` non serve più: somma otto giorni di modifiche approvate (1386 righe, fra cui la
versione di serializzazione delle sezioni 4 → 5 e i moduli plastici NaN nelle sezioni salvate, da guardare
nell'analisi dei bug). Gli altri fallimenti della corsa (Wiki e catture F4) dipendono dagli input non versionati di
`supporto/artefatti`, assenti in un worktree nuovo.

## E1. Ratifiche a comportamento invariato (F2.6, F2.7)

Scelte del coordinatore del 7–12/10, mai ratificate. **Proposta: ratificare tutto così.**

| Voce | Scelta |
|---|---|
| F2-1 (con T-1) | Taglio e torsione della libreria con differenze nelle ultime cifre (tolleranza 1e-9); con VEd = VRd senza staffe l'esito può cambiare per 1 ulp, senza tolleranza sul verdetto. |
| F2-3 | Torsione senza staffe chiuse: resta il rifiuto del legacy («controllare geometria, armature…»), non la verifica non soddisfatta con resistenza nulla della libreria. |
| F2-4 | Limiti propri della libreria (torsione oltre C90/105, CNR-DT) non raggiungibili dal modulo: nessuna azione. |
| B3 | Quattro casi di taglio e torsione nel corpus, baseline catturata col legacy. |
| F2.7-D2…D12 | Attuate o superate: D2 → U3, D5 → U1, D6 → U5, D8 → 3b e D3, D11 → A12; D3, D9, D10, D12 attuate. D7 vedi sotto. |
| A7 e T1 | I commit dedicati delle tolleranze sono sostituiti dalle regole `api_arrotondamento` di `1b3c38f1` (relative 1e-12, più strette di quelle previste). |
| D7 / B4 | La B4 attuale (17 documenti, catturata con i due motori dopo l'interruttore) sostituisce il corpus SLE del micro-piano; con la corsa di oggi. |

**Da guardare (D4).** Nei muri la fessurazione è «Non fessurata» se la tensione della sezione non fessurata è al
più fctk,0,05 (`RetainingWall.Library.Structures.cs:90`): usa la resistenza a trazione del calcestruzzo, come nel
legacy. I muri hanno armatura su entrambe le facce e la soglia fctk,0,05 è più prudente di fctm/1,2 (formazione delle
fessure NTC) e di fctm (EN 1992-1-1 §7.1(2)). **Proposta: tenerla e dichiararla nella guida.** Alternativa: calcolare
sempre wk anche nei muri.

## E2. Traccia del calcolo a taglio NTC nei report (F2-2)

La libreria scrive la traccia; la mappatura restituisce i testi del legacy, senza traccia. **Proposta: rinviarla
alla revisione dei report, in coda dopo il refactoring** (decisione del 14/10: «per ora però prendi i report per come
sono»). Alternativa: esporla ora (codici stabili in Checker, mappatura, report, catture: lavoro medio-grande).

## E3. Chiusura di F2.8 (dettagli, ancoraggi, aderenza, M–χ)

Il micro-piano prevedeva adattatori con interruttore e le prove B1–B3, T1, I1–I3. Con la ratifica 3a del 14/10 le
famiglie sono passate alla libreria senza interruttore. Equivalenza già misurata: confronto denso del riuso API
(ancoraggi 468/468 e aderenza 1918/1918 identici; dettagli e M–χ entro 1e-12), controllo delle chiamate reali alla
DLL, 266 casi congelati, catture D3 uguali, full del 15/10.

**Proposta:**
- accettare queste prove al posto di B1–B3, T1 e I1–I3; nel registro una voce di rimando ad API-01 invece di
  ricostruire F2-9…F2-12;
- trascrivere in `decisioni.md` e ratificare F2.8-D1…D12 (D2 e D11 superate; D3, muri senza il tetto R10 fino a
  F5.15, resta; D10, B4 unica, superata da E1);
- aggiungere alle suite di ANTHEA gli attesi indipendenti delle pagine dei metodi che mancano: ancoraggi (AN-01/02,
  SO-01/02), dettagli (As,min, As,max, Ast/s) e M–χ. Lavoro piccolo.

## E4. F2.10, accettazione dell'equivalenza

**Proposta:** accettare l'equivalenza di F2 sul full conforme del 15/10 (`20261015-105444-f4-full-190`, 140 PASS),
sulle catture prima e dopo D3 e sulla corsa B4 di oggi; ratificare il rinvio di R16–R21 a F5.15 (esteso dal
coordinatore). Si allineano nel registro le voci superate (D7-e e D7-f già attuate, F2-13 e F2-14 già ratificate con
4b, dieci percorsi di codice spostati da D3 o F3).

## E5. F2.11c dopo D3: duplicati, stato statico, lock

Con D3 non si elimina più nulla: i motori legacy sono già in `tests/ANTHEA.Legacy`. Restano nel prodotto alcuni membri
usati solo dalle prove e dello stato statico modificabile.

**Proposta:**
- spostare in `tests/` i membri usati solo dalle prove: `TensionBarSpacing`, `SectionRegions.Clip/Area/Region`,
  `RetainingWall.SoilMass` e `ValleyBand`, `ConcreteReinforcementDesign.SearchIndices`,
  `Durability.ValidateExposure`, due interfacce senza utilizzatori; cattura prima e dopo;
- c.a.: array e dizionari statici pubblici resi immutabili, come A12; `JsonFallbackHook.Observer` (aggancio di sola
  prova, pubblico e globale) reso interno e per contesto;
- il resto dello stato statico (ponte, muri, pali, fondazioni, Wiki) va in F5.13, che chiede già «calcoli in parallelo
  senza stato statico»;
- lock: punto chiuso. Nessun lock globale sul calcolo; restano lock di istanza e quelli dei font PDF e del registro
  errori.

## E6. F2.12, solutore legacy a fibre

Attuato con D3 (F4-49): il solutore è in `tests/ANTHEA.Legacy/Section`, i 90 casi di `X.Verifiche` girano ancora contro
di esso, l'oracolo Python (`tools/attesi/sezione_ca.py`, 464 casi) è conservato. **Proposta:** chiudere F2.12 così;
mettere `sezione_ca.py --valida` e `--confronta` nello stadio fast del runner; rinviare a dopo il refactoring il
confronto dei 90 casi fra libreria e Python.

## E7. F2.13, registri di migrazione

**Proposta:**
- voce «M4 c.a.» in `Checker/docs/migrazione-anthea/MIGRAZIONE_ANTHEA.txt` (taglio e torsione, SLE e fessurazione,
  dettagli, ancoraggi, M–χ, durabilità, SLU R2, API-02, spostamento D3, con versioni e commit) e una riga in
  `Model/CHECKER_PASSO_4.txt`, che dice ancora «ANTHEA non usa ancora le nuove API». Rami da master; merge e push
  con l'ok dell'utente;
- nessuna `SCHEDA_SCOSTAMENTI_FESSURAZIONE.txt`: il registro la sostituisce;
- `AGENTS.md`, regola «il motore legacy si elimina solo a equivalenza provata»: allinearla ora a D3 («si sposta in
  `tests/ANTHEA.Legacy`, non si elimina»);
- ratificare F2.3 [CP]: API e versioni sono distribuite dai set S, T e X.

## E8. STG-4 e SEZ-3b fuori da F2

Confronto numerico per fase sul ponte reale e sezione variabile per fase: oggi MIDAS restituisce tabelle per fase
vuote, quindi non dipendono dal refactoring. **Proposta:** spostarli nella traccia del modulo Modelli, da riprendere
quando ci sarà un modello MIDAS con i risultati per fase.

## Dopo le risposte

Documenti e registro (E1–E8), poi F2.11c (E5) e gli attesi di E3 con il profilo standard, la libreria (R1, R2, punto
3) nel ramo di Checker, e infine F2 `[x]` nel piano. Il merge del ramo su `main` si chiede a fine lavoro.
