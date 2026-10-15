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

**Attuazione (15/10 pomeriggio).**
- **Checker**, ramo `anthea-f2-residui` da master `26cef01a`, non pubblicato:
  - `3b48eca4` aggiunge tre opzioni di `SectionCrackOptions`, spente per default: `ModelCode2010BondStages`, `DinKtByDuration` e
    `UnreinforcedTensileZoneFails`. Il default resta quello di prima, quindi il contratto K0 di ModelChecker e le fixture congelate non
    cambiano;
  - attesi indipendenti in frazioni esatte per MC2010 (due stadi e due durate), DIN e il limite senza barre aderenti;
  - 94 prove di fessurazione, SLE, muri e ModelChecker superate;
  - versioni del candidato: Concrete 0.10.0.0 (`a7247664`) e versioni di correzione per le DLL ricompilate (`6f15f185`).
- **ANTHEA**:
  - l'adattatore SLE attiva le tre opzioni;
  - la mappatura traduce i due codici nuovi (σsr, τbms/fctm) e lo stadio, e scrive «non soddisfatta: zona tesa senza armatura, apertura
    delle fessure non controllata»;
  - le prove di equivalenza con il motore storico girano con le regole precedenti (aggancio di sola prova `PreviousCrackRules`, D3).
- **Prova 5n**, regole nuove contro regole precedenti sugli stessi 936 stati e sugli stati mirati: cambiano solo
  - MC2010, 31 stati: wk minore a lunga durata, più due voci di traccia a breve durata;
  - DIN a breve durata, 4 stati, con wk mai maggiore;
  - zona tesa senza armatura, 35 stati, che passano da «senza esito» a «non soddisfatta» senza altre differenze.

  Gli altri 866 stati sono identici.
- **Conseguenza da confermare.** Dei 35 stati della zona tesa, 33 sono sezioni cave con le pareti interne tese e senza barre vicino al
  foro, armate solo sul contorno esterno. Oggi la libreria tratta così anche la superficie interna, e l'ho allineata alla stessa regola. La
  verifica, che prima restava sospesa, ora è non soddisfatta. Alternativa: limitare la regola alla zona tesa esterna e lasciare le superfici
  interne senza esito.

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

**Da decidere (D4, muri).** La regola dei muri scatta proprio nel caso deciso oggi per le sezioni: la faccia tesa
del fusto o della fondazione senza barre («Nessuna armatura tesa»). Lì il muro usa la resistenza a trazione del
calcestruzzo: se la tensione della sezione non fessurata è al più fctk,0,05 dichiara «Non fessurata», wk = 0;
altrimenti la verifica resta senza esito, con valore 0 e senza limite (`GPCChecker.Concrete/Walls/WallSectionSession.cs:204-208`,
`RetainingWall.Library.Structures.cs:90`). Con le armature su entrambe le facce il caso non si presenta; nel corpus
non c'è. **Proposta: allineare i muri alla decisione delle sezioni**: faccia tesa senza barre → verifica non
soddisfatta, senza la prova con fctk,0,05. Alternativa: tenere la prova con fctk,0,05 e rendere non soddisfatta solo
la verifica oggi senza esito.

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

## Risposte del 15/10

L'utente ha approvato tutte le proposte così come sono scritte: «E1–E8 come nel foglio». Due risposte a parte:

- **E1-D4, muri**: la proposta. La faccia tesa del fusto o della fondazione senza barre dà la verifica «non
  soddisfatta», come nelle sezioni, senza la prova con fctk,0,05. Si attua nella release delle correzioni dei bug.
- **Superfici interne delle sezioni cave** (estensione di F2-25, 33 stati della griglia): restano «non soddisfatta».

Le decisioni sono trascritte in [decisioni.md](decisioni.md), sezione del 15 ottobre; piano e registro sono allineati.
L'analisi dei bug dello stesso giorno (SLU-2) ha portato anche la rettifica di F2-17: con N costante la libreria non
trova sempre un punto convergente.

## Dopo le risposte

Documenti e registro (E1–E8), poi F2.11c (E5) e gli attesi di E3 con il profilo standard, la libreria (R1, R2, punto
3) nel ramo di Checker, e infine F2 `[x]` nel piano. Il merge del ramo su `main` si chiede a fine lavoro.
