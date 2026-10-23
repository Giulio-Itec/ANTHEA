# F5: come riprendere il lavoro

Questa nota serve a riprendere F5 da una sessione nuova, anche con un'altra licenza, senza la cronologia della sessione che ha
fatto il lavoro. Si aggiorna a ogni chiusura di passo. Il piano è in [piano.md](piano.md), le decisioni in
[decisioni.md](decisioni.md).

## Stato al 21/10/2026, mattina

Fotografia ricavata dai repository, da [decisioni.md](decisioni.md) (sezioni dal 19 al 21 ottobre), dal [piano](piano.md) e dal
[verbale dell'integrazione del 20/10](merge-f5-sviluppo-20261020.md). Dal 18/10 sono entrati 75 commit in `refactoring/f5`; il
21/10 si aggiungono il set ufficiale AE e l'installer 1.12.1 rifatto. Il piano operativo dei prossimi blocchi è in
[f5-piano-dal-21-10.md](f5-piano-dal-21-10.md).

### ANTHEA

| Riferimento | Stato |
| --- | --- |
| `refactoring/f5` | Nel checkout principale `Documents\Repository\ANTHEA`, **9 commit avanti** a `origin/refactoring/f5` (9b242761): albero Paratie, capsula, set AE, verbali. |
| Tag pubblicati | `v1.11.0` (paratie e spettri), `v1.11.1` (vista grafica paratie), `v1.12.0` (persistenza nativa GPC, dodici wizard delle sezioni, 52 esempi). |
| 1.12.1 | Capsula come pilastro e albero Paratie, guide Rev56, set ufficiale **AE**. Collaudo full PASS 169 con due prove intermittenti ripetute PASS; setup rifatto da c7fe03dd e verificato. **Non pubblicata**: push e tag `v1.12.1` solo con l'ok dell'utente. [Verbale](installer-1.12.1.md). |
| `main` | `origin/main` è ancora 8b9ce6d3 (1.10.0). Il prodotto si rilascia da F5. |
| Rami | `main`, `refactoring/f5` e i rami degli agenti in corso (`refactoring/f5-10-s2`, `refactoring/f5-11-prep`). I rami uniti e `prova/*` sono cancellati il 21/10. |
| Worktree | `Temp\aw-f5-10-s2`, `Temp\aw-f5-11-prep` (agenti) e `.codex\worktrees\model-viewer-draft` (staccato, di un'altra sessione). `aw-f5`, `aw-prova-f5b`, `aw-main` e `ParatieWork` sono cancellati. |

Moduli nuovi, nati direttamente MVVM e quindi senza migrazione F5:
- **Azioni e spettri NTC con isolamento**: [spettri-ntc.md](spettri-ntc.md).
- **Paratie**: PARATIE-01…17. La [matrice](../paratie/requisiti.md) ha ancora righe aperte.
- **Pagina Esempi**: [esempi.md](esempi.md).

Sono cambiate molto anche le sezioni c.a.:
- persistenza nativa GPC, formato versione 3: [sezioni-persistenza.md](sezioni-persistenza.md);
- dodici wizard: [sezioni-wizard.md](sezioni-wizard.md);
- capsula come pilastro: [capsula-pilastro.md](capsula-pilastro.md).

### Librerie

| Repository | Pubblicato | Locale |
| --- | --- | --- |
| Model | `origin/master` 553860fa: GPCModel 5.16.0.1 (fondo scavo, modulo plastico dei canali), GPCModelData 0.0.2.27, GPC.Model.Persistence 3.0.0.20, GPC Converter 2.2.0.16. | Allineato. |
| Checker | `origin/master` eddcdb36: capsula, verifiche del fondo scavo, correzioni Steel fino a 2.4.0.17. | Il checkout principale è su `codex/steel-section-architecture` (3 commit non pubblicati) di un'altra sessione: non toccarlo. |

In `lib/Checker` di ANTHEA è installato il set **AE** ufficiale (c7fe03dd): Model 553860fa, Checker eddcdb36, Geometry 6734eee,
Utilities e57b440. Copia del set in `supporto/artefatti/librerie-ae/gpc-release-ae`. Sostituisce AB e il candidato AC:
`-GpcLibDir` non serve più.

### Fase F5 (MVVM)

| Stato | Passi |
| --- | --- |
| Chiusi | F5.1–F5.4. La palificata è MVVM anche nella Release. |
| Viste fatte, Release ancora storica | F5.5 materiali, F5.6 palo verticale, F5.6b stratigrafia, F5.9 muri, F5.9b fondazioni, F5.7 + F5.8 palo orizzontale con la scheda elastica; F5.11 sezione composta da ponte (ramo `refactoring/f5-11-vista`, da unire: [nota](f5-11-sezione-mista.md), sezione 5). Banco a differenza zero per tutti. |
| In corso | F5.10 sezione c.a.: fatti la parte A e S1; agente `f5-10-s2` su S2a (riallineamento) e S2 (azioni). F5.11: preparazione unita, vista fatta (agente `f5-11-vista`). |
| Non avviati | F5.12, F5.13, F5.14, F5.15. |

Versioni di prova consegnate (f5b, f5c, f5d) sul ramo `prova/f5-viste`:
- partono da 591272d8, cioè prima di paratie, spettri, wizard e set AB/AC;
- F5.7 + F5.8 non sono in nessuna.

## Prossimi passi, in ordine

1. **Librerie: set ufficiale con la capsula.** Fatto il 21/10: set AE installato (procedura `Checker/build/RILASCIO.txt`).
2. **ANTHEA sul nuovo set.** Profilo full e installer 1.12.1 fatti il 21/10. Resta la pubblicazione: push di `refactoring/f5` e
   tag `v1.12.1` **solo con l'ok esplicito dell'utente**.
   - Correggere la sensibilità ai tempi di `ui/check-field-form-parity` e `ui/check-palo-elastico-historic` (nelle prove).
   - `ui/check-palo-orizzontale-view` (21/10, macchina carica): «Esporta PNG…» della tavola delle armature diverso in 7 pixel
     su 7,5 milioni (al massimo 9 livelli, riquadro 10×10) dal riferimento disegnato nello stesso processo; da sola PASS.
     Il confronto byte per byte delle immagini va reso robusto alla resa grafica (nelle prove).
3. **Ambiente** (deroghe della 1.11.0 e della 1.12.0, ancora aperte):
   - **S6 chiusa il 22/10** (scelta dell'utente «1 a»): `DocumentFile.Write` copia anche le immagini invece di rinominarle, merge
     78ccba8e. La causa era la regola `NON_POLICY_JUST_ADC` di Trend Micro (`AEGIS_BM.log`), che chiudeva ANTHEA alla terza immagine
     della sessione. Dopo la correzione, dal checkout principale `ui/check-palo-verticale-view` e `ui/check-bridge-design-view` sono
     PASS (`supporto/artefatti/ci/20261022-225232-s6-verifica`). Resta da ripetere `ui/check-muri-view`;
   - ripetere `ui/check-contrast` a 96 DPI con i passi autonomi `mvvm-…`, senza cambiare la baseline;
   - le deroghe non vanno in `known-failures.json`.
4. **Passaggio nella Release dei moduli F5 con la vista pronta.**
   - Le versioni di prova f5b–f5d sono vecchie. Rifare una sola versione di prova dal `refactoring/f5` di oggi, sul nuovo set,
     con tutti e sette i moduli a Mvvm: materiali, palo verticale, stratigrafia, muri, fondazioni, palo orizzontale con la scheda
     elastica; dopo l'unione di `refactoring/f5-11-vista` anche la sezione composta da ponte (`str_mista_ponte`). Va su un ramo
     `prova/…` nuovo, oppure si aggiorna `prova/f5-viste`.
   - Dopo l'ok dell'utente, un modulo alla volta, con la procedura qui sotto.
   - Prima di passare palo orizzontale ed elastico va riscritta `F3PileSegmentChecks` (`ui/f3-scheduling`).
5. **F5.10 sezione c.a., S2–S9.**
   - Prima di S2, riallineare il piano dei sottopassi ([f5-10-sezione.md](f5-10-sezione.md), sezioni 8.5–8.6) al codice di oggi:
     persistenza nativa (versione 3), wizard, capsula con armature per tratti, dettagli generali del pilastro.
   - Verificare che le tracce congelate in `tests/fixtures/f5-sezione` siano ancora quelle della vista storica attuale. Si
     ricatturano solo con una motivazione e nello stesso commit.
6. **F5.11** sezione composta, **F5.12** Bridge Design e prezzari, poi **F5.13** shell (con sessione di prova), **F5.14**
   chiusura e **F5.15** revisione di tutte le scelte «da decidere» (decisione del 17/10).
7. **Paratie, fuori da F5 ma nello stesso ramo.**
   - Righe aperte della [matrice](../paratie/requisiti.md):
     - PAR-06 Culmann;
     - PAR-19 pushover di paratie affacciate;
     - PAR-20 interazione fra paratie;
     - PAR-21 effetto arco e cedimenti;
     - PAR-22 cerniere plastiche;
     - PAR-26 altre norme.
   - Dipendenze esterne PAR-31…33 (FLAC, Tokbo, chatbot).
   - Poi collaudi complessivi e rilascio ufficiale autorizzato.
   - In coda, dopo le paratie: [testa di pali e micropali sotto il piano di campagna](../paratie/seguito-pali-interrati.md).
8. **Code già nel piano**, invariate:
   - combinazioni nel Model (D1–D11 e i 3 possibili difetti);
   - pagine dei metodi;
   - revisione dei report a fine refactoring;
   - Eurocodici;
   - controllo di dettaglio in Bridge Design;
   - decisioni sui bug ancora «da decidere» (BUG-MUR-2b, PO-5b, FES-1b/1c, SLU-2b, TS-2, N7).

## Domande aperte per l'utente

- **Pubblicazione** di `refactoring/f5` (9 commit) con il tag `v1.12.1`.
- **Versione di prova unica** dei sette moduli F5 dal codice di oggi (passo 4).
- **Scelte «da decidere»:** si rivedono tutte in F5.15. L'elenco è in decisioni.md (17 ottobre) e nei documenti `f5-*.md`:
  M15–M20, S13–S17, MU14–MU19, S1-a…S1-e, f5-6 5.3, [f5-suite-ui.md](f5-suite-ui.md), f5-7 5 / f5-8 6,
  [f5-punti-di-controllo.md](f5-punti-di-controllo.md).
- **Elenco unico per F5.15** (23/10): [f5-15-revisione.md](f5-15-revisione.md), con le voci da guardare per prime, il conteggio per
  gruppo, le voci già decise e le voci R4–R21 del registro.
- **Pulizia del disco.** Il 21/10, con l'ok dell'utente («Cancella senza archiviare»), sono cancellati `Temp\aw-f5` con le
  prove, le catture dei banchi nello scratchpad e i rami uniti. La pulizia della cartella `Repository` è in corso: si cancella
  solo con l'ok dell'utente, cartella per cartella.

## Come si passa un modulo nella Release (dopo l'ok dell'utente)

Nello stesso commit, come per la palificata ([f5-4-pilota.md](f5-4-pilota.md), passi 5–7):
1. In `X.Desktop/Hosting/ModuleImplementations.cs`, `Declared` del modulo diventa `SheetImplementation.Mvvm`.
2. La vista storica va in `supporto/SUPERATI/ui-storica/<modulo>/` e i rami del modulo escono da `SheetEditor`.
3. Suite:
   - per le 11 suite di [f5-suite-ui.md](f5-suite-ui.md), si toglie il ramo storico e il duplicato `-mvvm` (sezione 4 di quella
     nota);
   - per le altre suite storiche elencate nel documento del modulo, la riscrittura;
   - si aggiornano il controllo «la Release resta storica» della suite della vista e `ui/check-module-host-offscreen`.
4. Contrasto:
   - si toglie il blocco autonomo `mvvm-<modulo>` in fondo a `supporto/test/Desktop/ContrastAudit.cs`;
   - le voci della Chiara passano alla chiave della vista nuova (D12), a totale invariato;
   - si ripete a 96 DPI.
5. Guida pratica e PDF, se la disposizione descritta cambia.

## Regole imparate unendo i rami

- **Navigatore della Wiki.** Ogni `FormViewModel` ha `WikiNavigator = Context.Services.Wiki`, perché `FieldForm` eredita
  `WikiContextHelp.Module` dalla vista.
- **Registri di prova ridotti.** Devono contenere i moduli che sono MVVM nella Release: `SheetEditorFactory` rifiuta gli altri.
- **Stili dei pulsanti.** Uno stile di pulsante con chiave vuole `BasedOn="{StaticResource {x:Type Button}}"`.
- **Dialogo del terreno.** È condiviso: `SoilTransferDialogService`, T5, in `Anthea.Presentation.Modules.Shared`.
- **Contrasto nelle unioni.** I blocchi autonomi in fondo a `ContrastAudit.cs` (uno per modulo) si tengono interi, uno dopo
  l'altro.
- **Fixture F3.** Quando due rami aggiungono fixture, lo sha di `expected/esito.txt` e le sezioni del manifest si riconcatenano.
  Il manifest si unisce come testo, mai con `json.dumps`.
- **Baseline F4.** Si controlla a conteggi: nostra + loro − base.
- **Fine riga.** `git show` dà file LF: normalizzare prima di confrontare con il worktree (CRLF).
- **Agenti.** Ogni lavoro si chiude o si ferma a un punto chiuso e documentato (richiesta dell'utente del 18/10). Più di tre
  agenti insieme consumano la quota in fretta: circa 1,5–2% della quota settimanale per agente di vista.

## Ambiente e strumenti

- **Schermo al 150% (144 DPI).** `ui/check-contrast` si esclude (D6).
- **Corsa completa:**

  ```
  powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)' -Tag <tag>"
  ```

  Con la macchina carica (agenti che compilano) `ui/check-field-form-parity`, `ui/check-palo-elastico-historic` e
  `ui/check-palo-orizzontale-view` (PNG della tavola delle armature) possono fallire: ripeterle da sole con
  `-Stage ui -NoBuild -Only`.

- **Cartella stabile** `C:\Users\g.pacini\Documents\Repository\ANTHEA-f5-ripresa`:
  - `LEGGIMI.md`;
  - `strumenti\`:
    - `crea-wt.sh <nome> <base>`: worktree con gli artefatti di base;
    - `crea-prova-f5.ps1`: versione portatile;
    - `bl3.py`: baseline F4 a tre vie;
    - `fix_merge16.py`: unione delle fixture F3;
  - `istruzioni-agenti\`: regole comuni e contesto dei blocchi 7 e 8;
  - `f5-agenti\<agente>\avanzamento.md`: avanzamento di ogni agente;
  - `integrazione-note.md`: diario delle unioni.
- **Prove degli agenti.** Ogni worktree creato con `crea-wt.sh` riceve una copia degli artefatti di base dal checkout
  principale; le prove restano nel `supporto/artefatti` del worktree e si cancellano con il worktree, dopo l'unione.
