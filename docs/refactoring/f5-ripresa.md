# F5: come riprendere il lavoro

Questa nota serve a riprendere F5 da una sessione nuova, anche con un'altra licenza, senza la cronologia della sessione che ha
fatto il lavoro. Si aggiorna a ogni chiusura di passo. Il piano è in [piano.md](piano.md), le decisioni in
[decisioni.md](decisioni.md).

## Stato al 18/10/2026, ore 4

**Rami e worktree.** Sono tutti locali e nessuno si spinge: l'utente ha detto «solo locale».

| Ramo | Worktree | Uso |
| --- | --- | --- |
| `refactoring/f5` | `C:\Users\g.pacini\AppData\Local\Temp\aw-f5` | integrazione di F5; ultima corsa completa `20261018-035914-f5-int20`: PASS 139, 0 NEW-FAIL |
| `prova/f5-viste` | `Temp\aw-prova-f5b` | solo versioni di prova portatili, non si unisce mai |
| `prova/f5-palificata` | nessuno | prima versione di prova, non si unisce mai |
| `main` | `Temp\aw-main` (ae0e1df4) | non spinto |

- **Checkout principale.** `C:\Users\g.pacini\Documents\Repository\ANTHEA` è di un'altra sessione (Codex, ramo
  `codex/modulo-stratigrafia`): non toccarlo.
- **Agenti e worktree.** Nessun agente è in corso e nessun worktree di agente è rimasto.

**Passi chiusi.** F5.1, F5.2, F5.3 e F5.4: la palificata è MVVM nella Release e la sua vista storica è archiviata.

**Viste XAML unite; la Release resta storica fino all'ok dell'utente.**

| Passo | Modulo | Stato |
| --- | --- | --- |
| F5.5 | materiali | vista, controlli e banco fatti |
| F5.6 | palo e micropalo verticale | vista, controlli e banco fatti |
| F5.6b | stratigrafia | vista, controlli e banco fatti |
| F5.9 | muri | vista, controlli e banco fatti |
| F5.9b | fondazioni superficiali | vista, controlli e banco fatti |
| F5.7 + F5.8 | palo orizzontale e scheda elastica | vista e controlli fatti; **manca il banco** ([f5-7-palo-orizzontale.md](f5-7-palo-orizzontale.md) §5, [f5-8-palo-elastico.md](f5-8-palo-elastico.md) §6); i due moduli passano insieme (O2/E2) |

**Sezione c.a. (F5.10).** S1 è chiuso: guscio, coordinatore del calcolo, ingressi e materiali. S2–S9 sono da fare; la sezione
8.6 di [f5-10-sezione.md](f5-10-sezione.md) dice da dove parte S2. La Release resta storica fino a S9.

**Suite UI.** Le 11 suite storiche generali, dei materiali e dei muri non dipendono più dall'implementazione. Hanno le varianti
`-mvvm` e si descrivono in [f5-suite-ui.md](f5-suite-ui.md).

**Versioni di prova consegnate** in `Documents\Repository\ANTHEA-prova-f5`: f5b, f5c e f5d. La f5d è la più completa: nove moduli
con la vista nuova (tutti i passi della tabella tranne F5.7 + F5.8). Ogni zip ha il suo `LEGGIMI-prova-*.md`.

**Non avviati.** F5.11 (sezione composta), F5.12 (Bridge Design), F5.13, F5.14 e F5.15.

## Prossimi passi, in ordine

1. **Risposte dell'utente sulla prova f5d.** Per ogni modulo approvato si fa il passaggio della Release (sezione sotto).
2. **Banco di F5.7 + F5.8:**
   - cattura F3 con `--module-impl geo_palo_orizzontale=mvvm --module-impl geo_micropalo_orizzontale=mvvm`;
   - confronto diretto a differenza zero;
   - le 1 117 transizioni F4 dell'elastico invariate;
   - capitoli.

   Poi una versione di prova per l'utente.
3. **F5.10 S2**, poi S3–S9, ognuno chiuso con le sue prove.
4. F5.11, F5.12, poi F5.13–F5.15.

## Domande aperte per l'utente

- **S6, antivirus.** `DocumentFile.Write` (`src/ANTHEA.Reports/DocumentFile.cs`) copia i documenti Office e i PDF, ma rinomina gli
  altri file con `File.Move`, PNG compresi. Sulla macchina delle prove la protezione dell'antivirus ha chiuso più volte
  `ANTHEA.exe`, che non è firmato, durante «Salva PNG…» del palo verticale. Due strade:
  - copiare anche le immagini (una riga; tocca il codice dei report, che è congelato);
  - lanciare le suite con `dotnet`.

  Vedi [f5-suite-ui.md](f5-suite-ui.md).
- **Scelte «da decidere» di F5.** Si rivedono tutte in F5.15 (decisione del 17/10):
  - decisioni.md, «17 ottobre (mattina)» e «(tarda mattina)»;
  - le domande segnate «da decidere» nei documenti `f5-*.md`: M15–M20, S13–S17, MU14–MU19, S1-a–S1-e della sezione, f5-6 §5.3,
    le domande di [f5-suite-ui.md](f5-suite-ui.md), e f5-7 §5 / f5-8 §6;
  - [f5-punti-di-controllo.md](f5-punti-di-controllo.md).
- **Contrasto.** `ui/check-contrast` va ripetuta a 96 DPI con i passi autonomi `mvvm-…` (schermo al 150% da 16/10).

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
