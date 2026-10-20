# F5: come riprendere il lavoro

Questa nota serve a riprendere F5 da una sessione nuova, anche con un'altra licenza, senza la cronologia della sessione che ha
fatto il lavoro. Si aggiorna a ogni chiusura di passo. Il piano è in [piano.md](piano.md), le decisioni in
[decisioni.md](decisioni.md).

## Stato al 20/10/2026, sera

Fotografia ricavata dai repository, da [decisioni.md](decisioni.md) (sezioni dal 19 al 20 ottobre), dal [piano](piano.md) e dal
[verbale dell'integrazione del 20/10](merge-f5-sviluppo-20261020.md). Dal 18/10 sono entrati 75 commit in `refactoring/f5`.

### ANTHEA

| Riferimento | Stato |
| --- | --- |
| `refactoring/f5` | 93c15882, nel checkout principale `Documents\Repository\ANTHEA`. È **6 commit avanti** a `origin/refactoring/f5` (9b242761): albero Paratie, capsula, candidata 1.12.1 e verbali. |
| Tag pubblicati | `v1.11.0` (paratie e spettri), `v1.11.1` (vista grafica paratie), `v1.12.0` (persistenza nativa GPC, dodici wizard delle sezioni, 52 esempi). |
| 1.12.1 | Candidata **solo locale**: capsula come pilastro e albero Paratie, guide Rev56. L'utente ha detto: «Mantieni per ora il commit solo in locale». Il profilo full non è stato eseguito. [Verbale](installer-1.12.1.md). |
| `main` | `origin/main` è ancora 8b9ce6d3 (1.10.0). Il prodotto si rilascia da F5. |
| Rami `refactoring/f5-*` | Sono tutti antenati di `refactoring/f5`. Restano fuori solo `prova/f5-viste` e `prova/f5-palificata`, che servono per le versioni di prova. |
| Worktree | `Temp\aw-f5` è su `refactoring/f5-passi-20261020`, il ramo di questa nota. Ci sono anche `Temp\aw-prova-f5b`, `Temp\aw-main` e `Documents\Repository\ParatieWork\ANTHEA` (`refactoring/f5-paratie-seguito`). |

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
| Model | `origin/master` a5391971, versione 5.16.0.1: input del sollevamento del fondo e correzione del modulo plastico del canale. | Allineato. |
| Checker | `origin/master` 60448490: unione di tre rami, con la capsula (8bf4af33) e le correzioni Steel EC3/NTC; Steel 2.4.0.2. | `master` 29897f24 ha **5 commit non pubblicati**: Steel allegato A, versioni 2.4.0.3–2.4.0.5. Il worktree ha modifiche in corso, cioè un'altra sessione sta lavorando su `AnnexANplNormalizationTests`. `codex/steel-legacy-reference-review` ha 3 commit fuori da `origin/master`. |

In `lib/Checker` di ANTHEA è installato il set **AB** ufficiale (Checker 894892c1).

**Attenzione.** Le nuove API della capsula sono nel set **AC**, che resta candidato:
- `refactoring/f5` compila e si collauda solo con `-GpcLibDir supporto/artefatti/capsula-pilastro/snapshot-AC`;
- vale finché non si installa un set ufficiale che contiene la capsula.

### Fase F5 (MVVM)

| Stato | Passi |
| --- | --- |
| Chiusi | F5.1–F5.4. La palificata è MVVM anche nella Release. |
| Viste fatte, Release ancora storica | F5.5 materiali, F5.6 palo verticale, F5.6b stratigrafia, F5.9 muri, F5.9b fondazioni, F5.7 + F5.8 palo orizzontale con la scheda elastica. Banco a differenza zero per tutti. |
| In corso | F5.10 sezione c.a.: fatti la parte A e S1; restano S2–S9. |
| Non avviati | F5.11, F5.12, F5.13, F5.14, F5.15. |

Versioni di prova consegnate (f5b, f5c, f5d) sul ramo `prova/f5-viste`:
- partono da 591272d8, cioè prima di paratie, spettri, wizard e set AB/AC;
- F5.7 + F5.8 non sono in nessuna.

## Prossimi passi, in ordine

1. **Librerie: un set ufficiale che contenga la capsula.**
   - Chiudere il lavoro Steel sull'allegato A, in corso in un'altra sessione. Poi chiedere all'utente se pubblicare i 5 commit
     locali di Checker (2.4.0.3–2.4.0.5) ed eventualmente `codex/steel-legacy-reference-review`.
   - Con i commit pubblicati: `Checker/build/Release-Set.ps1` (set non candidato, `-Previous` = file `release-set.json` di AB),
     tutte le suite (attesi: i 17 fallimenti Steel storici), poi `tools/libs/Update-Snapshot.ps1 -ReleaseSet … -Install`.
   - Il nuovo set sostituisce AB e AC: da lì `refactoring/f5` compila senza `-GpcLibDir`.
   - Procedura: `Checker/build/RILASCIO.txt`. Il set va installato da commit pubblicati.
2. **ANTHEA sul nuovo set.** Profilo full, mai eseguito sulla 1.12.1, poi rilascio ufficiale. Il numero sarà 1.12.1, oppure
   1.12.2 se entrano le correzioni Steel.
   - Push di `refactoring/f5` (oggi 6 commit avanti) e tag **solo con l'ok esplicito dell'utente**.
3. **Ambiente** (deroghe della 1.11.0 e della 1.12.0, ancora aperte):
   - dopo la verifica dell'amministratore di Trend Micro, ripetere `ui/check-palo-verticale-view` e `ui/check-muri-view`;
   - domanda S6: `DocumentFile.Write` rinomina le immagini;
   - ripetere `ui/check-contrast` a 96 DPI con i passi autonomi `mvvm-…`, senza cambiare la baseline;
   - le deroghe non vanno in `known-failures.json`.
4. **Passaggio nella Release dei moduli F5 con la vista pronta.**
   - Le versioni di prova f5b–f5d sono vecchie. Rifare una sola versione di prova dal `refactoring/f5` di oggi, sul nuovo set,
     con tutti e sette i moduli a Mvvm: materiali, palo verticale, stratigrafia, muri, fondazioni, palo orizzontale con la scheda
     elastica. Va su un ramo `prova/…` nuovo, oppure si aggiorna `prova/f5-viste`.
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

- **Pubblicazioni:**
  - Checker: commit Steel 2.4.0.3–2.4.0.5 ed eventuale revisione dei riferimenti storici;
  - ANTHEA: 6 commit di `refactoring/f5`, con tag della 1.12.1 o della versione successiva.
- **S6, antivirus.** Copiare anche le immagini in `DocumentFile.Write` (una riga, ma nel codice dei report congelato), oppure
  aspettare la verifica di Trend Micro.
- **Versione di prova unica** dei sette moduli F5 dal codice di oggi (passo 4).
- **Scelte «da decidere»:** si rivedono tutte in F5.15. L'elenco è in decisioni.md (17 ottobre) e nei documenti `f5-*.md`:
  M15–M20, S13–S17, MU14–MU19, S1-a…S1-e, f5-6 5.3, [f5-suite-ui.md](f5-suite-ui.md), f5-7 5 / f5-8 6,
  [f5-punti-di-controllo.md](f5-punti-di-controllo.md).
- **Pulizia del disco:**
  - `Temp\aw-f5\supporto\artefatti`, oltre 20 GB di prove;
  - le catture dei banchi nello scratchpad del 16–18/10 (2,5 GB);
  - i rami `refactoring/f5-*` già uniti.

  Si cancella o comprime solo con l'ok dell'utente.

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

  Finché il set ufficiale non contiene la capsula, aggiungere
  `-GpcLibDir C:\Users\g.pacini\Documents\Repository\ANTHEA\supporto\artefatti\capsula-pilastro\snapshot-AC`. Lo staging AC
  non è versionato e si trova solo nel checkout principale.

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
- **Prove degli agenti.** Sono copiate in `aw-f5/supporto/artefatti`, non versionato e oltre 20 GB: chiedere all'utente prima
  di comprimerle.
- **Catture dei banchi** (circa 2,5 GB). Sono nello scratchpad della sessione del 16–18/10:
  `C:\Users\G4B9B~1.PAC\AppData\Local\Temp\claude\C--Users-g-pacini-Documents-Repository\e49bc49a-a62e-4702-b1f8-1fcb3b34cc01\scratchpad\f5-agenti`.
