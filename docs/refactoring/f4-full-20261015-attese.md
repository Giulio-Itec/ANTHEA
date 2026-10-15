# F4 — differenze attese nel full del 15/10/2026

Previsione scritta il 14/10 sera, **prima** della corsa, in sola lettura. Non è letta dai test e non ammette nulla.
Base: la classificazione [f4-transizioni-ui-classificazione.md](f4-transizioni-ui-classificazione.md) (6805 differenze,
cattura di `82fcd8fc`). Commit della corsa: `fd28f353` (39 commit dopo `82fcd8fc`). Se il ramo avanza ancora, rifare
il controllo dei soli commit nuovi.

Scritto fuori dal repository durante la corsa, perché il runner segnala anche i file non tracciati, e inserito dopo
senza modifiche salvo questa nota, la nota sullo stadio baseline e la sezione finale «Esito».

## Fonti e metodo

- Commit: `git log 82fcd8fc..fd28f353` con i file toccati. Diff letti per ogni commit che tocca codice, guide o fixture.
- Runner: profilo `full` = stadi `build, fast, regression, wiki, ui`. **Lo stadio `baseline` non c'è**: le suite
  `baseline/f4-*` rese obbligatorie da `49d321e0` non girano nel full, salvo `-Stage` esplicito. La corsa del 14/10
  sera l'ha indicato esplicitamente: `-Stage build,fast,regression,wiki,baseline,ui`.
- `ui/f3-equivalence` (`tests/ANTHEA.Desktop.Equivalence/F3EquivalenceCapture.cs`): per le 36 fixture
  `opened, visited, calculated-data, result, exported-data, status, reopened` (JSON), `report.txt` e
  `report-structure.json` (testo e stili del DOCX), per `str_palo` anche `short.*`; 71 articoli Wiki (`.md` = fetta
  della guida, `.txt` = resa WPF), `wiki/search.json` (30 ricerche), progetto (`snapshot`, `status`, `report.*`).
  Confronto `BaselineComparer`: JSON per chiave, array **per posizione**; testo per riga con LCS; `^raw/` escluso.
- Riproduzioni usate per i numeri:
  - stili: tolto dalla cattura di `82fcd8fc` il paragrafo che F4-40 non scrive più (indice 9 dei tre report
    `str_palo`, indice 2627 del report di progetto, letti dai DOCX grezzi), poi confronto per posizione col riferimento;
  - Wiki `.md`: corpi ricalcolati da `index.json` e dalle guide del checkout (impronte uguali a `sources.props`);
  - ricerca: reimplementata; riproduce esattamente le 30 ricerche della cattura di `82fcd8fc`;
  - Wiki `.txt`: **stima** (righe del `.md` senza righe vuote e recinti ```` ``` ````); riproduce esattamente i
    conteggi osservati 15, 13, 12, 4 dei quattro file già diversi.
  Script di lavoro temporanei, fuori dal repository.

## Riepilogo atteso

| Famiglia / file | 82fcd8fc | Previsto | Δ | Voce |
|---|---:|---:|---:|---|
| `sheets/str_palo/{default,legacy-calculation-settings,legacy-workspace-segni}/{opened,visited,calculated-data,result,exported-data,reopened}.json` | 18 | 0 | −18 | F4-40 |
| `sheets/str_palo/{stesse tre}/report.txt` (riga 11) | 3 | 3 | 0 | F4-40 (classe cambiata) |
| `sheets/str_palo/default/report-structure.json` | 0 | 130 | +130 | F4-40 |
| `sheets/str_palo/legacy-calculation-settings/report-structure.json` | 0 | 130 | +130 | F4-40 |
| `sheets/str_palo/legacy-workspace-segni/report-structure.json` | 0 | 117 | +117 | F4-40 |
| `sheets/str_palo/corpus-…-din-rettangolare/{report.txt,report-structure.json}` | 1 + 123 | 1 + 123 | 0 | F4-31 |
| `project/report.txt` | 322 | 324 | +2 | F4-47 (+ F4-40 classe cambiata) |
| `project/report-structure.json` | 728 | 623 | −105 | F4-40 × F4-28 |
| `project/snapshot.json` | 4 | 3 | −1 | F4-40 |
| `sheets/mat_calcestruzzo/{default,legacy-esposizioni}/report.txt` | 0 | 2 + 2 | +4 | F4-47 |
| `sheets/geo_fondazioni_superficiali/{confirmed-actions,legacy-soil-profile}/{result.json,report.txt}` | 4 | 4 | 0 | F4-35, valore 0.3.0.1 |
| Wiki `.md` (23 file) | 90 | 162 | +72 | F4-48 (F4-39, Rev44) |
| Wiki `.txt` (23 file) | 52 | ~100 | ~+48 | F4-48 (stima) |
| `wiki/search.json` | 44 | 71 | +27 | F4-48 |
| Muri, palo elastico, Bridge, resto del progetto | 5416 | 5416 | 0 | invariati |
| **Totale** | **6805** | **~7211** | **~+406** | |

File con differenze: 65 → **88** (−18 JSON `str_palo` senza più differenze; +3 `report-structure.json` `str_palo`;
+2 report dei materiali; +36 file Wiki di 18 articoli nuovi). Nessun numero di calcolo nuovo o cambiato è atteso.

## Tabella per commit e voce

| Commit | Voce | Output catturati interessati | Differenza prevista | Attesa | Note |
|---|---|---|---|---|---|
| `7d3c9bc9` | F4-36 | `sheets/str_mista_ponte/*` | nessuna | sì | Le due fixture hanno `rebars_top` e `rebars_bottom` veri: stessi valori al mapping. |
| `2850c031` | F4-37 | `sheets/geo_muri_sostegno/*` | nessuna | sì | `two_zones` falso nelle quattro fixture; GPC.Design legge `LowerHeight` solo con due zone (`WallBarScheduling`, `WallRebarQuantities`, `WallReinforcementDetailing`). |
| `660b4c71` | F4-38 | nessuno (`beam-ca.json` non è catturato) | nessuna | sì | Corregge `ui/check-wiki-offscreen`, fallita nel full 1.8.0. |
| `ef1b0cff` | — | nessuno | nessuna | sì | Solo prova: corregge `ui/f3-report-chapters` (Bridge), fallita nel full 1.8.0. |
| `fd55a6c5`, `9707b109`, `7569f6fc`, `90e4a797`, `1ec03a4f`, `372d38cf`, `eaf64673` | — | nessuno | nessuna | sì | Documenti, `.gitattributes`, archivi in `SUPERATI`. |
| `244a0f52` | — | nessuno | nessuna | sì | Prova installer, gira solo con `-Installer`. |
| `d66379ac` | F4-39 (Wiki: F4-48) | `wiki/_wiki_guide_primi-passi_avvio-e-scelta-del-modulo.{md,txt}`, `wiki/search.json` | `.md` +4 righe aggiunte (righe 5–8), `.txt` +2; ricerca «report»: articolo inserito in posizione 1, 23 spostamenti + 1 elemento aggiunto (+24) | sì | Due paragrafi sull'installazione 1.8.0. Già previsto da V1. |
| `285a2219` | — | nessuno | nessuna | sì | Riprova di scrittura di `DocumentFile`. |
| `6e22b9a6` | F4-40 | `str_palo` (tre fixture senza norma nel riferimento), progetto | vedi dettaglio: −18 chiavi, +377 stili spostati, riga 11 da «testo» a «riga-rimossa», progetto −105 stili e −1 chiave | sì | **Non «spariscono le 24»** come scrive `f4-chiusura-decisioni.md`: la ripetizione della nota NTC ora manca, quindi gli stili successivi si spostano. Input F3 aggiornati in `manifest.json`. |
| `5e7e7218` | F4-41 | `sheets/geo_palo_orizzontale/single-soil-elastic/*` | nessuna | sì | L'errore «Classe di esposizione non assegnata.» nasce in ANTHEA prima della libreria; nessun «fonte» del copriferro nelle fixture. |
| `46910aaa` | F4-42 | nessuno nel confronto F3 | nessuna | sì | Le fixture portano i propri dati; `NormalizeState` conserva l'ordine delle chiavi. Cambiano solo i fogli nuovi (`CreateData`): vedi «da guardare». |
| `430bed65` | — | nessuno | nessuna | sì | Script archiviato. |
| `3f8e358c` | A12 | nessuno | nessuna | sì | `ImmutableArray` con lo stesso ordine. |
| `13a49893` | — | nessuno | nessuna | sì | `MigrationNotice` è solo testo della vista, non catturato. |
| `137acd4b` | set X (F4-35) | `sheets/geo_fondazioni_superficiali/{confirmed-actions,legacy-soil-profile}/{result.json,report.txt}` | stesse 4 differenze, valore nuovo «0.1.3.3 → **0.3.0.1**» (`$.library_version`, riga 52) | sì | Geotechnics 0.3.0.0 → 0.3.0.1, revisione tecnica (SDK). Altre DLL: nessuna versione stampata nelle catture. |
| `01523e9a` | F4-43 | nessuno | nessuna | sì | φ = 0 in tutte le fixture `str_palo` (`phi` "0"): piano totale = piano del solutore. Il testo di ispezione non è catturato. |
| `c13db4e3` | F4-44 | nessuno | nessuna | sì | Nelle quattro fixture `str_palo` e nel progetto i dettagli si fermano a «Scegliere il tipo di elemento.» prima di NEd; `dettagli` è escluso dai report delle schede. Anche con calcolo, SLU = SLV = SLE (N = −2500). |
| `a105e011` | F4-45 | nessuno | nessuna | sì | Nessun prezzario con analisi nelle fixture Bridge; senza analisi la serializzazione non cambia. |
| `6bd55f6b` | F4-46 | `raw/**/*.pdf` (esclusi da `^raw/`) | nessuna | sì | DOCX invariati. Rischio di fallimento del PDF: vedi «da guardare». |
| `49d321e0` | — | nessuno | nessuna | sì | Stadio `baseline`, fuori dal full. |
| `3b8f00dd` | — | nessuno | nessuna | sì | Registro e documenti. |
| `46fc35b5` | — | nessuno | nessuna | sì | Prove adeguate ad A12; `ConcreteShort.Checks` passa allo stadio `regression` con prova `^PASS \d+ assertions`. |
| `99fd0ad6` | F4-47 | `sheets/mat_calcestruzzo/{default,legacy-esposizioni}/report.txt` righe 107 e 109; `project/report.txt` righe 2358 e 2360 | 6 «testo»: nota dei cloruri (`[T2 r20 c2]`, `[T82 r20 c2]`) e riferimento della composizione (`[T2 r21 c2]`, `[T82 r21 c2]`) senza ATECAP | sì | Vita utile 50 anni, nessuna XF: nota dei 100 anni e nota dell'aria non compaiono. Stili invariati. |
| `2a23ba48` | Rev44 (F4-48) | 21 articoli Wiki (4 già diversi, 17 nuovi), `wiki/search.json` | `.md` +68, `.txt` ~+46, ricerca «σ» +3 (guida `sezione-ca` inserita in posizione 14) | sì | Vedi tabella Wiki. `editorial.json` e `references.json` non entrano nella cattura. |
| `e4424b1a`, `08588fec`, `146531e3`, `a96d2d35` | F2-7, F4-48 | nessuno | nessuna | sì | Documenti e registro. |
| `52e4b8aa` | F4-44 | nessuno | nessuna | sì | Prova dello stadio `baseline`. |
| `c3b21f38` | F2-7 | nessuno | nessuna | sì | Stati mirati in `ConcreteLibraryAdapter.Checks` (stadio `regression`). |
| `429dcb1d`, `fd28f353` | TEST-02 | nessuno nel confronto F3 | nessuna | sì | `supporto/test/casi_confronto.json`: usato da `verifiche/*` e da `ui/smoke` (`{cases}`), non da `ui/f3-equivalence`. Numeri identici o entro 4,6e-13. |
| `ce78d290` | — | nessuno | nessuna | sì | Nuovi tentativi su `summary.txt` («Flusso illeggibile», che ha interrotto il full `9707b109`). |

## Dettaglio delle differenze previste

### F4-40, norma della sezione c.a. (`6e22b9a6`)

La nota «Documento precedente senza normativa…» non si scrive più e viene tolta. Nel riferimento F3 il report aveva la
nota NTC due volte; F4-31 aveva sostituito la seconda con la nota di migrazione; ora la seconda manca.

- JSON delle tre fixture: sparisce `$.workspace_ca.migrazione_normativa` (e `$.dati.…` in `result.json`): −18.
  Le due fixture sintetiche hanno ora `normativa` nell'input; `default` non ha area di lavoro e riceve NTC 2018
  senza nota. I JSON tornano uguali al riferimento.
- `report.txt` delle tre fixture: riga 11 «¶ NTC 2018 · coefficienti della DLL…» da «testo» a **«riga-rimossa»**.
  Conteggio invariato (3).
- `report-structure.json` delle tre fixture: `$.styles` spostati dall'indice 9. `default` e
  `legacy-calculation-settings` 130 (129 spostamenti + 1 elemento rimosso in coda, su 579 stili);
  `legacy-workspace-segni` 117 (116 + 1, su 502). Tabelle, righe, celle e immagini invariate. Stesso meccanismo
  già visto nella fixture DIN (123).
- Progetto: `snapshot.json` perde `$.fogli[11].dati.workspace_ca.migrazione_normativa` (−1); `report.txt` riga
  4186 da «testo» a «riga-rimossa» (conteggio invariato).
- `project/report-structure.json` 728 → **623**. Lo spostamento di F4-28 (paragrafo inserito all'indice 742) ora
  si chiude dove F4-40 toglie un paragrafo (indice 2627 della cattura): 301 spostamenti invece di 405, sparisce
  l'elemento in coda di F4-28; restano i 318 stili in coda di M–χ (F3.4-U5) e le 4 differenze di
  tabelle/righe/celle/immagine. Il gruppo 11 della classificazione scende da 536 a 431.
- `short.*` invariati: il report breve non ha mai avuto la nota.

### F4-47, testi della durabilità (`99fd0ad6`)

- `sheets/mat_calcestruzzo/{default,legacy-esposizioni}/report.txt`: riga 107 `[T2 r20 c2]` «Cl 0,40: proposta per
  armatura ordinaria dagli esempi ATECAP, pp. 43–50…» → «Cl 0,40: valore proposto per il calcestruzzo con armatura
  ordinaria… secondo UNI EN 206…»; riga 109 `[T2 r21 c2]` «UNI 11104:2016, prospetto 5 (riportato in ATECAP 2020,
  p. 19). Nella UNI 11104:2025 in vigore…» → «UNI 11104:2016, prospetto 5, richiamata dalle NTC 2018 al §11.2.11;
  l'edizione 2025 non è adottata…». Le righe «Esposizioni: XC1/XC3. Classe minima…» restano.
- `project/report.txt`: stesse due celle del capitolo materiali, righe 2358 e 2360 (`[T82 …]`).
- Nessun altro file dei materiali: i testi non sono in `result.json` né nei dati.

### Set X (`137acd4b`)

Le 4 differenze di F4-35 restano, con il valore nuovo `0.3.0.1`. Concrete 0.9.0.0 e Design 0.20.0.0 aggiungono API
non usate dalle fixture in modo visibile. Nessun numero di calcolo atteso diverso. Evidenza indiretta: `baseline/f4-*`
PASS a `2a23ba48` con il set X installato (a parte `f4-input-contracts`, corretta in `52e4b8aa`).

### Wiki e ricerca (`d66379ac` e `2a23ba48`, voce F4-48)

Articoli già diversi nella cattura di `82fcd8fc`:

| Articolo | `.md` | `.txt` (stima) | Cosa cambia |
|---|---:|---:|---|
| `guide_moduli_sezione-ca` | 25 → 29 | 15 → ~18 | NEd (F4-44), report short senza Word (F4-46), deformazioni con viscosità (F4-43); riga 5 della norma (F4-40) già diversa, valore nuovo |
| `guide_moduli-singoli_bridge-design` | 23 → 27 | 13 → ~15 | unità «h», analisi composte (F4-45) |
| `manuale_ponti_bridge-design` | 19 → 38 | 12 → ~24 | riferimenti FHWA e TU Delft tolti, bibliografia [R3]–[R5] → [R3], analisi composte con formula (F4-45) |
| `manuale_calcestruzzo-armato_portanza-sismica-…-rev07` | 7 → 8 | 4 → ~5 | «Fonti:» senza JRC |
| `manuale_calcestruzzo_profili-calcestruzzo` | 16 | 8 | invariato |

Articoli nuovi con differenze (18):

| Articolo | `.md` | `.txt` (stima) | Cosa cambia |
|---|---:|---:|---|
| `guide_primi-passi_avvio-e-scelta-del-modulo` | 4 | 2 | installazione 1.8.0 (F4-39, `d66379ac`) |
| `guide_esportazione_salvataggio-e-report` | 3 | 2 | Word e PDF affiancati (F4-46) |
| `guide_progetti_modelli-importazione-midas` | 1 | 1 | tolta la frase su Straus |
| `manuale_calcestruzzo-armato_calcestruzzo-armature-e-copriferro` | 2 | 2 | UNI 11104:2016 e NTC §11.2.11; tabella «XF1 \| C32/40» senza 2025 (U1, F4-47) |
| `manuale_calcestruzzo-armato_sezione-in-calcestruzzo-armato` | 16 | 7 | piano totale con tre formule (F4-43), NEd con formula (F4-44); solo righe aggiunte |
| `theory_palo-elastico` | 3 | 3 | fonti TU Delft e JRC tolte |
| `manuale_fem_elementi-beam` | 2 | 2 | TU Delft (1 testo, 1 riga rimossa) |
| `manuale_fem-e-modellazione_releases-e-connettivita` | 1 | 1 | TU Delft |
| `manuale_acciaio_instabilita-delle-aste-compresse` | 1 | 1 | MIT |
| `manuale_dinamica-e-sisma_dinamica-e-sisma-del-modello` | 1 | 1 | MIT |
| `manuale_metodi-costruttivi_fasi-costruttive-e-percorso-dei-carichi` | 1 | 1 | SCI |
| `manuale_ponti_bridge` | 1 | 1 | FHWA |
| `manuale_calcestruzzo_cracking` | 1 | 1 | Walraven/JRC → EN 1992-1-1 §7.3.4 |
| `manuale_ponti_ponti-solette` | 2 | 1 | paragrafo FHWA rimosso |
| `manuale_ponti_ponti-acciaio-fasi` | 1 | 1 | FHWA/SCI → «Esempio numerico originale.» |
| `manuale_ponti_ponti-precompressione` | 2 | 1 | paragrafo FHWA rimosso |
| `manuale_ponti_ponti-stralli` | 1 | 1 | frase FHWA tolta |
| `manuale_ponti_ponti-deformazioni` | 1 | 1 | «Fonti:» senza FHWA |

Ricerca, 44 → 71: «report» +24 (F4-39: `avvio-e-scelta-del-modulo` in posizione 1, 24 → 25 risultati); «σ» +3
(Rev44: `guide/moduli/sezione-ca` in posizione 14, 16 → 17). Le altre 28 ricerche come nella cattura di `82fcd8fc`
(«armature», «taglio», «curvatura» con le loro 44 differenze già classificate).

Numero di articoli (71) ed `esito.txt` invariati.

## Altre suite del full: fallimenti possibili non numerici

- `ui/f3-equivalence`: **NEW-FAIL per costruzione** (confronto esatto, `build/known-failures.json` vuoto).
  Proof: `esito.txt` «PASS 36 fixture; 71 articoli; 30 ricerche…» viene scritto prima del confronto.
- `ui/f3-report-chapters`, `ui/check-wiki-offscreen`: fallite nel full 1.8.0, corrette dopo (`ef1b0cff`,
  `660b4c71` + `6e22b9a6`) ma **mai rieseguite in una corsa con stadio `ui`**. Atteso PASS.
- `ui/f3-concrete-figures`, `ui/f3-figures`, `ui/f3-scheduling`: atteso PASS. Le due fixture `str_palo` con norma
  aggiunta danno gli stessi dati dopo `Prepare`.
- Tutte le prove che esportano report (`ui/smoke`, `smoke-retaining-wall`, `smoke-horizontal`,
  `smoke-global-stability`, `smoke-project-report`, `smoke-project-workspace`, `smoke-bridge*`, `smoke-ca-*`,
  `smoke-display`, `ui/f3-equivalence`): con F4-46 ogni report scrive anche il PDF da MigraDoc. È la prima corsa
  WPF dopo F4-46; Reports.Tests copre solo i 151 report della cattura headless, non il report di progetto né le
  schede materiali (lo dice `PdfChecks.cs`).
- `ConcreteShort.Checks`: ora nello stadio `regression`, con prova su `test-results.txt`. PASS in
  `20261014-213118-audit-fix-a12`.
- `verifiche/regressione`, `verifiche/software`, `verifiche/micropalo`: `casi_confronto.json` cambiato da TEST-02;
  PASS a `429dcb1d` (t30-attesi), `fd28f353` non rieseguito in una corsa del runner nota a questo documento.
- Build `X.Desktop` in configurazione UiTests: compilata il 14/10 alle 21:30 durante la verifica di `46fc35b5`;
  dopo, solo `99fd0ad6` tocca `X.Desktop` (Materiali). Rischio basso.
- Uscita del runner: codice 0 solo senza file tracciati modificati. Un'altra sessione che scrive nel checkout
  durante la corsa sporca l'esito.
- Non nel full: stadio `baseline` (`baseline/f4-*`, `baseline/f4-reference` con il riferimento non versionato da
  37 MB) e suite `installer/*` (solo con `-Installer`).

## Da guardare con attenzione

1. **PDF dei report (F4-46).** Se `DocxPdf.Render` fallisce su un report di scheda, la cattura scrive
   `status.json` con `$.report` = «unavailable» e `$.reason`, e mancano `report.txt` e `report-structure.json`
   (`file-rimosso`). Se fallisce sul report di progetto (`ReportProject.Write`), la cattura si ferma: `errore.txt`
   senza `confronto.json`. Nessuna di queste è attesa; il rischio non è misurabile prima della corsa.
2. **Conteggi delle `.txt` della Wiki.** Stima ±; i `.md` e la ricerca sono calcolati esattamente. Le formule
   nuove (`\varepsilon_{tot}`, `\chi_{tot}`, `\max_r\ \max(0;\ -N_r)`, `\sum_i q_i p_i`) non sono mai state rese da
   WPF; comandi già usati nelle guide, rischio basso. Un errore di formula ferma la cattura Wiki e
   `ui/check-wiki-offscreen`.
3. **Spostamenti di stile di F4-40.** 130/130/117 e 623 valgono se l'unico paragrafo cambiato è la nota. Un
   conteggio diverso con lo stesso inizio (`$.styles[9]` nelle schede, `$.styles[742]`…`[2626]` nel progetto) indica
   un altro paragrafo aggiunto o tolto.
4. **Nessun numero di calcolo cambiato.** Ogni differenza numerica fuori dai gruppi 4–9 della classificazione
   (muri, palo elastico, Bridge) non è prevista: in particolare nei muri a mensola (F4-37), nelle sezioni
   (`str_palo`, F4-43/F4-44) e in `str_mista_ponte` (F4-36).
5. **F4-42 nelle prove WPF.** I fogli Materiali nuovi hanno tutte le chiavi; i dati condivisi del progetto ne
   elencano di più (`ProjectSharedData`). Incerto per le prove di progetto che creano fogli nuovi; nessuna prova trovata
   che ne conti le voci.
6. **Voci di registro.** Le differenze Wiki vanno su F4-48 (editoriale), con richiamo alla voce del contenuto
   (F4-39…F4-47). Le +377 differenze di stile delle schede `str_palo` e la riduzione nel progetto vanno su F4-40,
   da annotare anche nella frase di `f4-chiusura-decisioni.md` sulle «24 differenze che spariscono».
7. **Ramo in movimento.** Questa previsione copre fino a `fd28f353`. Un commit successivo che tocchi guide, Wiki,
   report, fixture F3 o `lib/Checker` va aggiunto prima di usarla.

## Esito

Corsa `supporto/artefatti/ci/20261014-230601-f4-full-20261015`, commit `fd28f353`, albero pulito prima e dopo, stadi
`build, fast, regression, wiki, baseline, ui`, 30 minuti. **136 PASS, 0 KNOWN, 2 NEW-FAIL**.

| Stadio | PASS | NEW-FAIL |
|---|---:|---:|
| build | 32 | 0 |
| fast | 27 | 0 |
| regression | 14 | 0 |
| wiki | 2 | 0 |
| baseline | 16 | 0 |
| ui | 45 | 2 |

- **`ui/f3-equivalence`.** NEW-FAIL per costruzione, come previsto: 7214 differenze non ammesse in 88 file, contro le
  circa 7211 previste. Confronto con la cattura di `82fcd8fc`:
  - 6337 differenze identiche;
  - le variazioni coincidono file per file con la tabella «Riepilogo atteso»:
    - `project/report-structure.json` 728 → 623, `project/report.txt` 322 → 324, `project/snapshot.json` 4 → 3;
    - 18 chiavi `str_palo` sparite e +130, +130, +117 stili spostati;
    - +2 e +2 testi nei report dei materiali;
    - Wiki `.md` 90 → 162, ricerca 44 → 71;
  - gli unici numeri nuovi sono le 4 versioni 0.3.0.1 di Geotechnics;
  - l'unico scarto dalla stima sono le `.txt` della Wiki: 103 contro circa 100. Ogni `.txt` con differenze ha il
    suo `.md` cambiato.

  Le righe M–χ del report di progetto compaiono con un numero di riga diverso solo perché la riga tolta da F4-40 sposta
  la numerazione: il contenuto è invariato. Nessuna differenza fuori previsione.
- **`ui/smoke-bridge-prices`.** NEW-FAIL non previsto: «Price editor tabs/preview unavailable». La prova attendeva 3
  schede e F4-45 (`a105e011`) ha aggiunto «Analisi composte»; lo stadio `ui` non era stato eseguito dopo quel commit.
  La prova è aggiornata a 4 schede, con i controlli della scheda nuova, e le intestazioni tagliate della tabella delle
  analisi vanno su due righe (`5ba83f1f`, registro F4-45). Rieseguita da sola: PASS, 30 controlli WPF.
- **Rischi della sezione precedente.** Nessuno si è presentato:
  - PDF dei report (F4-46): 36 schede e report di progetto catturati, `ui/smoke-project-report` PASS;
  - formule nuove della Wiki e `ui/check-wiki-offscreen` PASS;
  - `ui/f3-report-chapters` PASS;
  - `baseline/f4-*` tutte PASS, compresa `f4-input-contracts`.
