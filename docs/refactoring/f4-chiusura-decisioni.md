# F4 — decisioni per la chiusura e il merge

Preparato il 14/10/2026 sera, in sola lettura. Non cambia gli stati di [piano.md](piano.md) e del
[registro](registro-differenze.json).

**Stato.** Ramo `refactoring/f4-correzioni-audit` a `429dcb1d` (14/10 22:52; su origin è a `08588fec`). Contiene
`refactoring/f4-calcoli-residui` (`7569f6fc`) ed è 96 commit avanti a `main` locale (`46b4c918`, 1.7.1), che non
ha commit propri. Versione 1.8.0; in `lib/Checker` il set X (`137acd4b`).

**Già deciso il 14/10, non riproposto** ([decisioni.md](decisioni.md), punti 1–9): σcp e curvatura dei muri,
norma c.a. (F4-40), NEd (F4-44), report sbloccati, legacy di `b7339f4a`, ratifiche 4a/4b, proposte F2.9, PDF dei
report, analisi composte, pagine dei metodi F4 pubblicate, F2-7 punti 1–2. Registro: nessuna voce F4
«da-decidere» o «da-correggere»; F2-7 «dichiarato», punto 3 in coda.

## Condizione comune: il full del 15/10

La chiusura dipende dal full del runner sul ramo (avvio verso le 04:30 del 15/10) e dalla classificazione delle
sue differenze rispetto al riferimento F3.

> **Esito del full del 15/10: da inserire** (commit, cartella, PASS / KNOWN / NEW-FAIL).
>
> **Differenze del confronto UI F3 nel full: da inserire** (numero, file, confronto con le 6805).

- `ui/f3-equivalence` darà NEW-FAIL per costruzione: confronto esatto
  (`tests/ANTHEA.Desktop.Equivalence/F3EquivalenceCapture.cs`, riga 96), attesi `tests/fixtures/f3/expected`
  invariati, `build/known-failures.json` vuoto. Ogni altro NEW-FAIL va spiegato.
- Corse in `supporto/artefatti/ci/`: primo full `20261014-f4-1.8.0-full` (`82fcd8fc`) 114 PASS e 5 NEW-FAIL,
  quattro corretti dopo; secondo full `20261014-f4-1.8.0-final` interrotto. Su albero pulito dopo l'audit:
  `20261014-173627-audit-x-standard` (`3b8f00dd`) 65 PASS, 1 NEW-FAIL e 1 BLOCKED da A12, corretti in
  `46fc35b5`; `20261014-220447-audit-baseline` (`2a23ba48`) 19 PASS, 1 NEW-FAIL, corretto in `52e4b8aa`. Le
  verifiche di queste correzioni e le corse successive (quick 47 PASS, installer 2 PASS, F2-7 2 PASS) sono su
  albero modificato; nessuna corsa con lo stadio `ui`. Il full del 15/10 è la prima corsa completa su albero
  pulito dopo l'audit.

## D1. Le 6805 differenze del confronto UI F3 (F4.16g)

Fonti: [classificazione](f4-transizioni-ui-classificazione.md) e [prospetto](f4-transizioni-ui-da-revisionare.md);
una riga per differenza in `supporto/artefatti/refactoring/f4/classificazione-ui/classificazione.tsv`. Cattura di
`82fcd8fc` contro il riferimento F3 (`bce1e337` più Rev40): 65 file, **nessuna differenza non spiegata, nessuna
regressione probabile**.

| Gruppi | Origine (commit · voce) | Diff. | Giudizio |
|---|---|---:|---|
| 1–2 | M–χ nel report di progetto, guida Rev42 (1.7.1) · `4a3044cb` · F3-11 | 643 | atteso, numeri nuovi |
| 3 | Guide Rev41 nella Wiki (1.7.0) · `b6fac621` · nessuna voce | 51 | atteso, editoriale |
| 4 | Taglio dei muri con σcp · `bbfe0638` + `3f9df6b8` · F4-03 | 968 | atteso; VRd +0,09…+2,60 %, esiti invariati |
| 5 | Curvatura totale dei muri · idem · F4-08 | 180 | atteso; ×3 esatto con φ = 2 |
| 6–7 | Ultime cifre degli adattatori dei muri · `bbfe0638`, `5afb47ab` · F4-13, F4-14 | 2180 | atteso; ≤ 5e-16 relativo |
| 8 | Esiti tipizzati del palo elastico · `2fbe0710` · F4-15 | 1096 | atteso, solo chiavi aggiunte |
| 9 | Bridge senza stazione doppia · `bed30616` · F4-23 | 832 | atteso; le altre stazioni identiche |
| 10–11 | Revisione dei fattori CO₂ · `3ecaf795` · F4-28 | 550 | atteso (536 stili spostati) |
| 12 | Traffico legacy e migrazione · `d4762582` · F4-29 | 22 | atteso |
| 13–14 | Norma c.a. esplicita · `9fee7a47` · F4-31 | 147 | atteso (123 stili spostati) |
| 15 | Versione di Geotechnics (set W) · `3f9df6b8` · F4-35 | 4 | atteso |
| 16–17 | Guide Rev43 nella Wiki e ricerca · `82fcd8fc` · nessuna voce | 132 | atteso, editoriale |

I gruppi 1–3 (694) vengono da versioni già in `main`. «Stili spostati» e stazione in meno (1535) nascono dal
confronto per posizione: un elemento in più o in meno sposta i successivi.

Punti dubbi (V1–V7) al 14/10 sera:
- **V1**: i commit dopo la cattura non sono misurati (`7d3c9bc9` F4-36, `2850c031` F4-37, `660b4c71` F4-38,
  `d66379ac` F4-39, che cambia di sicuro un articolo Wiki). Li misura il full del 15/10.
- **V2** (tolleranze): aperto, vedi D2. **V5** (voci di registro per Rev41–Rev44): voce F4-48 aggiunta la sera
  del 14/10; il manifest F3 ha `editorial_updates` solo per Rev40, quindi l'ammissione resta in D2.
- **V3, V4** (F4-03, F4-08): scelte confermate il 14/10 (2a, 2b); resta solo l'ammissione.
- **V6**: delle due stazioni Bridge doppie resta quella con V = 9,1e-13 invece di V = 0. Solo estetico.
- **V7**: osservazione, nessuna differenza.

Le due regressioni vere di F4 (F4-36, F4-37) erano fuori da questo confronto, sono emerse nel primo full e sono
corrette in `7d3c9bc9` e `2850c031`.

Nel full del 15/10 (atteso da commit e registro, non misurato): spariscono le 24 differenze della nota F4-31
(F4-40); cambia la versione di Geotechnics (set X); possono comparire differenze da F4-39 e Rev44 (Wiki), F4-47
(testi della durabilità), F4-42 (fogli Materiali), F4-44 (combinazione che governa NEd), F4-43 e `13a49893`.

**Proposta.** Approvare i 17 gruppi come transizioni attese e lasciare V6 com'è (non cambia risultati né costi).
Le differenze nuove del full le classifico con lo stesso metodo e le porto all'utente solo se non spiegate o se
cambiano un esito.

**Domanda.** Approvi i 17 gruppi e V6 così com'è? Va bene il criterio per le differenze nuove?

## D2. Come ammettere le differenze

Oggi nessun meccanismo è attivo; la proposta per interi file è stata respinta. Già fatto, da far presente: con
F4-40 (`6e22b9a6`) due input F3 (`tests/fixtures/f3/inputs/str_palo--legacy-calculation-settings.json` e
`…--legacy-workspace-segni.json`) hanno ora la normativa NTC 2018, registrata in `tests/fixtures/f3/manifest.json`
(`input_updates`); attesi invariati.

**Proposta.** Risultati e report: elenco versionato delle transizioni per file e percorso, con valore prima e
dopo e voce di registro; ogni altra differenza fallisce; prove negative del prospetto; nessuna tolleranza nuova.
Wiki e ricerca: come F3-06, con impronte prima/dopo in `editorial_updates` e voci di registro per Rev41–Rev44.
Poi una nuova corsa completa sul commit finale: il full del 15/10 non può essere quello conforme.

**Domanda.** Approvi questo modo di ammissione, la corsa completa successiva e la modifica dei due input F3?

## D3. Legacy

F4.16 (riga 411) chiede l'elenco del legacy da rimuovere con i chiamanti: **manca**. Motori legacy o diagnostici
conservati in F4.7c.1, F4.8b.3, F4.10b.2, F4.12b.1, F4.12c.2, F4.13 e F4.14c; SLE legacy selezionabile fino alla
revisione delle pagine dei metodi (3b).

**Proposta.** Chiudere F4 senza rimozioni; elenco dopo il merge e rimozione famiglia per famiglia (F2.11c).

**Domanda.** Chiudiamo F4 senza rimuovere il legacy?

## D4. Chiusura dei punti F4

`[~]` con tutti i sottopunti `[x]`: F4.4, F4.7, F4.8, F4.9 [CP], F4.10, F4.11, F4.12, F4.15, F4.16 [CP]; aspettano
solo distribuzione e full. `[ ]`: voci dell'audit di F4.9 (riga 366) e F4.16 (riga 407), F4.16g (riga 422). Il set
distribuito è X, non W (Model `2650cf73`, Checker `f094dda5`, su master con l'ok dell'utente).

**Proposta.** Dopo D1, D2 e una corsa completa conforme: punti a `[x]`, [CP] F4.9 (set X) e F4.16 chiusi, verbale
di chiusura. Un NEW-FAIL o una differenza non spiegata torna all'utente.

**Domanda.** Approvi la chiusura di F4 a queste condizioni?

## D5. Merge, versione e push

`origin/main` è a `90550d7e` (1.7.0, `git ls-remote` del 14/10 sera): il push di `main` pubblicherebbe anche il
merge 1.7.1 `46b4c918`. Il tag `v1.8.0` è su `d66379ac`; dopo sono entrati F4-44, F4-45, F4-46 e il set X, quindi
l'agente propone 1.9.0 (la delega sulle versioni riguarda le DLL delle librerie, non il numero di ANTHEA).

**Proposta.** Dopo la corsa conforme: merge senza avanzamento rapido in `main`, tag `refactoring/post-f4`,
ANTHEA 1.9.0 con installer, suite dell'installer e tag `v1.9.0`; poi cancellazione dei due rami F4 uniti.

**Domanda.** Approvi il merge? Vuoi l'installer 1.9.0? Autorizzi il push di `main`, dei tag e la cancellazione
dei rami anche su origin?

## Diciture del piano superate dalle decisioni del 14/10

Righe di [piano.md](piano.md) a `429dcb1d`; punti della sezione del 14 ottobre di [decisioni.md](decisioni.md).
Aggiornate la sera del 14/10 nel commit che aggiunge questo documento: a ogni dicitura è aggiunta la ratifica con il
punto di `decisioni.md`, senza cancellare il testo storico; nel registro sono aggiornate F4-20 e F4-31.

| Righe | Dicitura | Superata da |
|---|---|---|
| 19 | [CP] F1.4, F1.6, F2.2, F2.9, F3.1, F3.4, F3.6, F3.7, F4.3, F4.14 «ratifica dell'utente aperta» | 4b (F3.6 solo ponte; report short 4c-1) |
| 66, 73 | tabella: ratifica di F1.4/F1.6 e di F3.1/F3.4/F3.6/F3.7 aperta | 4b |
| 66, 125 | junction «in attesa dell'ok dell'utente» | 5a |
| 144, 146, 154 | F1.4, F1.6, F2.2: «da ratificare», «ratifica dell'utente aperta» | 4b |
| 175 | F2.7: «B4, A7 e le decisioni F2.7-D … da ratificare» | in parte: F2.7-D1 (4b), B4 (3b, F2-8) |
| 178 | F2.8: «Da ratificare o da ripristinare» | 3a |
| 190 | F2.9: ratifica aperta su D1…D14 e U1…U7 | 4b e 4c (U6 in coda) |
| 235, 240, 270, 333 | F3.1, F3.4, F3.7, F4.3: «ratifica dell'utente aperta» | 4b |
| 258 | F3.6: ricalcolo del ponte; «report short senza Word non è fatto» | 4b; 4c-1 e F4-46 |
| 366 | F4.9: merge e push dei master «da ratificare» | 4a |
| 399 | F4.14: ratifica aperta, migrazione a NTC | 4b; 2c e F4-40 |

Restano aperte, non bloccanti per F4: riga 161 (F2-1, F2-3, F2-4, traccia NTC, B3), riga 175 (A7,
F2.7-D2…D12), riga 450 (F5.15, R16-R21). Nel registro: F4-20 e F4-31 «da ratificare» superate da 2d e 2c; F2-6
«F2.7-D1 non registrata» superata da 4b.

## Non bloccanti

Eurocodici, sovrapposizione delle SLE dei muri, revisione dei report, U6, F2-7 punto 3, rilettura delle pagine
dei metodi; residui di altre fasi (F2.3b-tracce, F2.10–F2.13, STG-4, SEZ-3b, F5.15).
