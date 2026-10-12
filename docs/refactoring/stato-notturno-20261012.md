# Checkpoint operativo — 12/13 ottobre 2026

Aggiornare ai passaggi chiave; integra il piano, non lo sostituisce.

- Finestra autorizzata: 12 ottobre 22:19 → 13 ottobre 07:19 Europe/Rome (05:19 UTC).
- Limite: arrivare a F4 se possibile; NON iniziare F5.
- Ordine: blocco F2 punti 1–5 e 5.2 → STG → SEZ → F3 → F4.
- Un commit per punto chiuso, riepilogo a fine fase, test/versioni/merge/push già autorizzati.
- Niente agenti paralleli: non richiesti. Nessun motore legacy eliminato senza equivalenza e autorizzazione specifica.

## Stato

1. API-02: chiuso, 66a9d351.
2. Validazioni: chiuso, cf33c16e.
3. SLE: chiuso, fbedb8ca (A10: 1e9998e0).
4. Mesh/concorrenza: chiuso; 326 controlli, 80 mesh esatte, 548 file invariati. Registro F2-23.
5. Collaudo finale e rilascio: da eseguire includendo la finestra 5.2.
5.2. Model API pronta (94efc9bc); vista MVVM, test e guide da completare.

## Rami e set

- ANTHEA: worktree `model-viewer-draft/ANTHEA`, `refactoring/f2-chiusure-pendenti`. Il checkout principale main resta alla release 9b42f093 fino al merge.
- Model: `codex/f2-reologia`, 94efc9bc, Model 5.1.0.0. API ConcreteTimeEffects con φ(t), φ∞, Ecm(t) e ModulusFactor.
- Checker: `codex/f2-api02-interassi`, 97b7d1f1, Concrete 0.0.27.2.
- Geometry: `codex/regole-versioni`, 6734eee; Utilities: `codex/regole-versioni`, e57b440.
- Candidato punti 1–4: `supporto/artefatti/refactoring/api02/lib-candidate/` nel worktree.
- Candidato UI reologia: `supporto/artefatti/refactoring/reologia/lib-model51/` nel worktree; prodotto e testato da worktree puliti, non ancora distribuito.
- Prima del rilascio: versioni tecniche dei dipendenti ricompilati, commit pushati, Release-Set e installazione coerente, profilo full e test librerie.

## Evidenze e precauzioni

- Punto 4: `supporto/artefatti/ci/20261012-221801-f2-concorrenza-prima` e `20261012-221914-f2-concorrenza-dopo`.
- Non modificare file tracciati mentre il runner CI è attivo: verifica l'albero prima/dopo.
- Il documento utente aperto nella build principale è non salvato: non chiudere né riavviare quella finestra.
- Combo: rifiuto ENV_SLE_R diagnosticato (somma di inviluppi supera 4096); nessuna modifica al documento. Diagnostica/scalabilità tracciata nel piano.
- Finestra reologia: riusare Plot/Ui.Dialog, ViewModel senza WPF, formule soltanto in Model. Distinguere maturazione Ecm(t) dal modulo efficace per viscosità. Aggiornare le due guide globali e i relativi Word/PDF/indici.
