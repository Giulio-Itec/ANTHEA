# Sezioni rapide: commit e push del 23 novembre 2026

Branch: `codex/sezioni-rapide-taglio`, pubblicato su `origin`. Include `origin/main` a `5436403e` tramite merge `c983c3c8`. Il branch main non è stato modificato.

Commit del lavoro:

- `6a0d9659`: codice e prove di raccolte, copie, schede e taglio rapido.
- `70259201`: guide Rev77, Word/PDF, indice Wiki e revisioni archiviate.
- `c4ba92fa`: decisioni, piano e registro delle differenze.

La verifica precedente al merge con BC aveva 13 esiti PASS, inclusi 291 controlli della sezione rapida. Le verifiche successive al merge sono registrate qui senza assimilarle al collaudo precedente.

- `mirati-bc`: 14 PASS.
- `baseline-bc`: 99 PASS, 12 NEW-FAIL.

Limiti di integrazione:

Il main remoto contiene già il set BC e documenta un gate non chiuso in `lib/Checker/README.md`: banco c.a., confronti storici e contrasto della full. Le catture del task 0143 citate dal README non sono presenti in questo workspace. Nessuna modifica a riferimenti, tolleranze o allowlist; nessun rilascio/installer e nessun aggiornamento della copia locale installata in `app/` in questo passo.

Le transizioni editoriali Wiki sul salvataggio e sugli aggiornamenti successivi restano in attesa, come richiesto. Il commit delle guide comprende soltanto le transizioni sul riordino delle schede già approvate il 20 novembre.

Esiti non superati della corsa baseline locale:

- `baseline/f4-last-adapters`: NEW-FAIL — Unhandled exception. System.IO.DirectoryNotFoundException: Could not find a part of the path 'C:\Users\g.sciarpa\Desktop\ANTHEA\supporto\artefatti\refactoring\f4\last-adapters-before.json'.
- `baseline/f4-residual`: NEW-FAIL — Unhandled exception. System.IO.DirectoryNotFoundException: Could not find a part of the path 'C:\Users\g.sciarpa\Desktop\ANTHEA\supporto\artefatti\refactoring\f4\residual-before.json'.
- `baseline/f4-bridge`: NEW-FAIL — Unhandled exception. System.IO.DirectoryNotFoundException: Could not find a part of the path 'C:\Users\g.sciarpa\Desktop\ANTHEA\supporto\artefatti\refactoring\f4\bridge-before\bridge-before.json'.
- `baseline/f4-input-contracts`: NEW-FAIL — Unhandled exception. System.IO.DirectoryNotFoundException: Could not find a part of the path 'C:\Users\g.sciarpa\Desktop\ANTHEA\supporto\artefatti\refactoring\f4\input-contracts-before\input-contracts-before.json'.
- `baseline/f4-section-design`: NEW-FAIL — Unhandled exception. System.IO.DirectoryNotFoundException: Could not find a part of the path 'C:\Users\g.sciarpa\Desktop\ANTHEA\supporto\artefatti\refactoring\f4\before-port-1.7.1\inputs\section-design'.
- `baseline/f4-wall-design`: NEW-FAIL — Unhandled exception. System.IO.DirectoryNotFoundException: Could not find a part of the path 'C:\Users\g.sciarpa\Desktop\ANTHEA\supporto\artefatti\refactoring\f4\before-port-1.7.1\inputs\wall-design'.
- `baseline/f4-pile-design`: NEW-FAIL — Unhandled exception. System.IO.DirectoryNotFoundException: Could not find a part of the path 'C:\Users\g.sciarpa\Desktop\ANTHEA\supporto\artefatti\refactoring\f4\before-port-1.7.1\inputs\piles\design-segment.json'.
- `baseline/f4-pile-outcomes`: NEW-FAIL — Unhandled exception. System.IO.DirectoryNotFoundException: Could not find a part of the path 'C:\Users\g.sciarpa\Desktop\ANTHEA\supporto\artefatti\refactoring\f4\before-port-1.7.1\inputs\piles'.
- `baseline/f4-gravity-adapter`: NEW-FAIL — Unhandled exception. System.IO.DirectoryNotFoundException: Could not find a part of the path 'C:\Users\g.sciarpa\Desktop\ANTHEA\supporto\artefatti\refactoring\f4\wall-sigma-study\independent.json'.
- `baseline/f4-structural-adapter`: NEW-FAIL — Unhandled exception. System.IO.DirectoryNotFoundException: Could not find a part of the path 'C:\Users\g.sciarpa\Desktop\ANTHEA\supporto\artefatti\refactoring\f4\wall-sigma-study\independent.json'.
- `baseline/f4-detail-adapter`: NEW-FAIL — Unhandled exception. System.IO.DirectoryNotFoundException: Could not find a part of the path 'C:\Users\g.sciarpa\Desktop\ANTHEA\supporto\artefatti\refactoring\f4\before-port-1.7.1\inputs\walls'.
- `baseline/f4-reference`: NEW-FAIL — attestazione finale assente: esito.txt

Evidenze: `supporto/artefatti/commit-push-sezioni-20261123/` (stato iniziale, log, riepiloghi completi e riferimento remoto).

La corsa locale del profilo baseline termina con 99 PASS e 12 NEW-FAIL: undici confronti non trovano i rispettivi file/cartelle originali F4; il dodicesimo non trova `supporto/artefatti/refactoring/f4/before-port-1.7.1`. Il banco `baseline/banco-ca` non viene eseguito perché le fixture del repository Checker non sono disponibili. Questi limiti non vengono trasformati in esiti ammessi e la full non viene dichiarata eseguita. I problemi dei riferimenti erano già registrati prima di questo lavoro; nessuna cattura storica viene rigenerata dai risultati correnti.
