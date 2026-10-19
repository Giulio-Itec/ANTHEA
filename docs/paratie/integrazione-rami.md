# Integrazione autorizzata a lavoro concluso

Il 19 ottobre 2026 l'utente ha richiesto: «finito tutto fai merge del branch
sulle paratie e su spettri dentro refactoring f5 (dentro anthea) e tutti i rami
in master dentro checker». Questa autorizzazione riguarda i merge locali;
non contiene autorizzazione al push. Non chiude né esclude i requisiti aperti
della matrice paratie.

Ricognizione, prima di qualsiasi merge:

- ANTHEA: destinazione `refactoring/f5` a `b9c2d4b1`, occupata dal worktree
  `C:/Users/g.pacini/AppData/Local/Temp/aw-f5`. Sorgenti `refactoring/f5-paratie`
  in questo worktree e `refactoring/f5-azioni-spettri` a `33919e8e` nella
  cartella originale ANTHEA. Non cambiare il branch della cartella originale.
- Checker: `master` a `bce7aaae`, pulito alla ricognizione. Gli unici rami
  locali non già contenuti in master sono `codex/paratie` (`cdc49139`) e
  `codex/azioni-spettri` (`5dc39d91`), entrambi in worktree separati.
- Model: le modifiche paratie sono ancora su `codex/paratie`. Il merge su
  master di Model non è nominato nella richiesta; ricontrollarne le revisioni
  e le necessità di integrazione prima del rilascio coordinato.

Prima dell'integrazione finale rileggere SHA, stati e regole nei tre repository,
verificare che il lavoro spettri sia concluso, conservare tutte le modifiche e
risolvere i conflitti di codice e versioni con collaudi del risultato unificato.
Non considerare gli esiti dei rami separati equivalenti al collaudo del merge.
Non installare DLL candidate o isolate per aggirare il requisito del set
coerente compilato da revisioni pushate. Nessun merge ancora eseguito.
