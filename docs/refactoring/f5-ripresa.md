# F5: come riprendere il lavoro

Questa nota permette di riprendere F5 da una sessione nuova, anche con un'altra licenza, senza la cronologia della sessione che
ha fatto il lavoro. Si aggiorna a ogni chiusura di passo. Il piano resta [piano.md](piano.md); le decisioni restano in
[decisioni.md](decisioni.md).

## Stato (18/10/2026, notte)

**Rami e worktree.** Sono tutti locali e nessuno va spinto: l'utente ha detto «solo locale».

| Ramo | Worktree | Uso |
| --- | --- | --- |
| `refactoring/f5` | `C:\Users\g.pacini\AppData\Local\Temp\aw-f5` | integrazione di F5 |
| `prova/f5-viste` | `Temp\aw-prova-f5b` | solo versioni di prova portatili; non si unisce mai |
| `prova/f5-palificata` | nessuno | prima versione di prova; non si unisce mai |
| `main` | `Temp\aw-main` (ae0e1df4) | non spinto |

- **Checkout principale.** `C:\Users\g.pacini\Documents\Repository\ANTHEA` è di un'altra sessione (Codex, ramo
  `codex/modulo-stratigrafia`): non va toccato.

**Passi.**
- **Chiusi:** F5.1, F5.2, F5.3 e F5.4. La palificata è MVVM nella Release e la sua vista storica è archiviata.
- **Viste XAML unite, Release ancora storica.** Aspettano la prova dell'utente:

  | Passo | Modulo |
  | --- | --- |
  | F5.5 | materiali |
  | F5.6 | palo e micropalo verticale |
  | F5.6b | stratigrafia |
  | F5.9 | muri |
  | F5.9b | fondazioni superficiali |

  - Versioni di prova consegnate in `Documents\Repository\ANTHEA-prova-f5`:
    - f5b, f5c;
    - f5d, la più completa: 9 moduli con la vista nuova.

    Ogni zip ha il suo `LEGGIMI-prova-*.md`.
  - Ogni documento del modulo (`f5-*.md`, sezione «Vista XAML») elenca le prove a mano e le suite storiche da riscrivere al passaggio.
- **In corso nella notte 17–18/10** (blocco 8, base 591272d8):
  - F5.7 + F5.8: viste del palo orizzontale e della scheda elastica (ramo `refactoring/f5-7-vista`);
  - F5.10 S1: guscio, ingressi, materiali e coordinatore del calcolo della sezione c.a. (`refactoring/f5-10-s1`);
  - suite UI storiche rese indipendenti dall'implementazione (`refactoring/f5-suite-ui`).
- **Da fare:**
  - F5.10 S2–S9;
  - F5.11 (sezione composta), F5.12 (Bridge Design);
  - F5.13–F5.15.
- **F5.15.** L'utente ha deciso il 17/10 che si rivedono tutte le scelte «da decidere» (vedi [decisioni.md](decisioni.md), 17 ottobre).

## Come si passa un modulo nella Release (dopo l'ok dell'utente)

Nello stesso commit, come per la palificata ([f5-4-pilota.md](f5-4-pilota.md), passi 5–7):
1. `X.Desktop/Hosting/ModuleImplementations.cs`: `Declared` del modulo a `SheetImplementation.Mvvm`.
2. Vista storica in `supporto/SUPERATI/ui-storica/<modulo>/`, rami del modulo tolti da `SheetEditor`.
3. Suite storiche:
   - riscritte, o tenute solo nella variante indipendente dall'implementazione (`f5-suite-ui.md`, quando unito);
   - il controllo «la Release resta storica» delle suite della vista va aggiornato;
   - `ui/check-module-host-offscreen` va aggiornato.
4. Contrasto:
   - si toglie il blocco autonomo `mvvm-<modulo>` in fondo a `supporto/test/Desktop/ContrastAudit.cs`;
   - le voci della Chiara passano alla chiave della vista nuova (D12), a totale invariato;
   - si ripete a 96 DPI.
5. Guida pratica e PDF, se la disposizione descritta cambia.

## Regole imparate unendo i rami (17/10)

- **Navigatore della Wiki.** Ogni `FormViewModel` ha `WikiNavigator = Context.Services.Wiki`: da F5.5 `MvvmSheetEditor` imposta
  `WikiContextHelp.Module` e `FieldForm` lo passa ai form.
- **Registri di prova ridotti.** Devono contenere i moduli che sono MVVM nella Release, perché `SheetEditorFactory` li rifiuta.
- **Stile dei pulsanti.** Uno stile di pulsante con chiave vuole `BasedOn="{StaticResource {x:Type Button}}"`.
- **Dialogo del terreno.** È condiviso (`SoilTransferDialogService`, T5) in `Anthea.Presentation.Modules.Shared`.
- **Blocchi del contrasto.** Ogni modulo ha un blocco autonomo in fondo a `ContrastAudit.cs`: nelle unioni si tengono interi,
  uno dopo l'altro.
- **Fixture F3.** Quando due rami aggiungono fixture, si riconcatenano lo sha di `expected/esito.txt` e le sezioni del manifest.
  Il manifest si unisce come testo, mai con `json.dumps`.
- **Baseline F4.** Si controlla a conteggi (nostra + loro − base).
- **Fine riga.** `git show` dà file LF: normalizzare prima di confrontare con il worktree (CRLF).

## Ambiente

- **Schermo al 150% (144 DPI).** `ui/check-contrast` si esclude (D6) e va ripetuta a 96 DPI.
- **Corsa completa:**

  ```
  powershell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build\ci.ps1' -Stage build,fast,wiki,ui -Only '^(?!ui/check-contrast$)' -Tag <tag>"
  ```

- **Strumenti del coordinatore e istruzioni per gli agenti** in `C:\Users\g.pacini\Documents\Repository\ANTHEA-f5-ripresa`:
  - `strumenti\crea-wt.sh <nome> <base>`: worktree con gli artefatti di base;
  - `strumenti\crea-prova-f5.ps1`: versione portatile;
  - `strumenti\bl3.py`: baseline F4 a tre vie;
  - `strumenti\fix_merge16.py`: unione delle fixture F3;
  - `istruzioni-agenti\`: regole comuni e contesto dei blocchi;
  - `integrazione-note.md`: diario delle unioni.
- **Prove degli agenti.** Sono copiate in `aw-f5/supporto/artefatti` (non versionato, oltre 20 GB): chiedere all'utente prima di
  comprimerle.
