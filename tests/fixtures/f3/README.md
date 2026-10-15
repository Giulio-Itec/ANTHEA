# Riferimento F3

Input sintetici e acquisizioni del comportamento di ANTHEA 1.5.0, set V, prima degli spostamenti. Non sono attesi ingegneristici indipendenti: questi restano nelle suite originali e non vengono sovrascritti.

36 fixture coprono i 12 moduli, default incompleti, esempi numerici, migrazioni Nq/γb, peso del micropalo, esposizioni multiple, segni CA, coefficienti e staffe, ricerca globale, palificata, ponte con altezza d’anima precedente e accessori, Bridge Design, fondazioni v1 e scelta interattiva del palo elastico. I due pali orizzontali hanno anche risposta elastica valida. Le fixture dei singoli fogli vengono scritte una sola volta dal comando esplicito `fixtures-f3` del toolkit; il comando rifiuta destinazioni non vuote e non produce attesi.

La suite `ui/f3-equivalence` nel runner unico cattura apertura, visita delle schede e dei pannelli, calcolo, esportazione e riapertura. I report disponibili sono confrontati per testo, tabelle, stili applicati e dimensioni delle figure; i byte delle immagini restano negli artefatti e non entrano nel confronto numerico. Compresi report brevi CA, progetto con 12 fogli, 71 articoli e 30 ricerche Wiki. Il progetto sintetico usa identificativi GUID fissi: generarli a ogni esecuzione cambiava l’ordine dei conflitti a parità di contenuto. Non sono stati aggiunti ordinamenti o esclusioni ai confronti per nasconderlo.

Doppia cattura D/E: 458 file uguali, salvo il solo `tempi_ms` già escluso dal toolkit. Riferimento immutabile in `expected`, input e librerie identificati da `manifest.json`; soglie iniziali in `architecture.json`. I DOCX e le immagini restano in supporto/artefatti/ci. Il banco confronta automaticamente le catture future con questo riferimento e non lo aggiorna.

## Transizioni di F4

Decisione D2 del 15/10/2026 (`docs/refactoring/decisioni.md`). I risultati e i report del riferimento non cambiano: le
differenze introdotte da F4 sono ammesse una per una in `transitions-f4.json`. Ogni voce indica file, percorso, classe,
valore prima, valore dopo e voce del registro. Il confronto fallisce in due casi:
- una differenza che non è nell'elenco;
- una voce dell'elenco che non si presenta.

Non ci sono tolleranze nuove. Le prove negative sono in `tests/ANTHEA.Testing.Tests`.

Wiki e ricerca seguono invece la regola editoriale di F3-06: i file cambiati sono sostituiti nel riferimento.
- Rev40: `editorial_updates` del manifest.
- Rev41–Rev44: `editorial_updates_f4`, voce F4-48.
- Rev45: `editorial_updates_rev45`, frase d'installazione della guida pratica senza numero di versione.

Le impronte prima e dopo sono calcolate sulla forma conservata da git (fine riga LF).

L'elenco e l'aggiornamento della Wiki si generano con `tools/qa/f3_transizioni.py`. Il programma parte dal confronto di
una corsa e dalla classificazione puntuale delle differenze, in `docs/refactoring/f4-transizioni-ui-classificazione.md`.
Le differenze senza voce lo fermano.

Comando: `build/ci.ps1 -Stage build,ui -Only '^ui/f3-equivalence$' -Tag f3-equivalence`.
