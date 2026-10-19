# Integrazione paratie e spettri in F5

## Richiesta aggiornata del 19 ottobre 2026

L'utente ha anticipato il merge per avere una versione testabile e ha chiarito
che «tutti» significa soltanto i rami sviluppati per questi lavori. Restano
separati `prova/f5-palificata`, `prova/f5-viste` e i rami storici delle librerie.
Dopo la consegna su `refactoring/f5`, lo sviluppo paratie riprende da quella
revisione su un nuovo ramo; la matrice non viene dichiarata completata.
La nuova richiesta autorizza anche il merge locale di Model e il ritorno della
cartella principale ANTHEA a F5, superando le cautele della ricognizione iniziale
riportata sotto. Il push non è autorizzato.

Sorgenti integrate:

| Repository | Base | Rami e revisioni |
| --- | --- | --- |
| ANTHEA | `b9c2d4b1` | paratie `b789e246`, spettri `33919e8e` |
| Model | `bf8563f5` | paratie `064b9214` |
| Checker | `bce7aaae` | paratie `c78e0128`, spettri `5dc39d91` |
| Geometry | `6734eee1` | invariata |
| Utilities | `e57b440b` | invariata |

Model è confluito in master, con revisione di build `e1e6e107`; Checker è
confluito in master con `dfb808c8`. GPC.Design 0.28.0.0 combina le due famiglie;
le altre DLL Model/Checker hanno un incremento di revisione per la ricompilazione
con le identità integrate. Nessuna formula dei rami è stata riscritta nel merge.
Il tentativo iniziale di includere il vecchio ramo locale GlassChecker è stato
annullato integralmente dopo la precisazione dell'utente; non fa parte del set.

ANTHEA integra i due moduli nel catalogo, nei servizi applicativi, nei report e
nella composizione WPF. Il confronto dei moduli preesistenti resta invariato;
le prove controllano separatamente la presenza unica di paratie e spettri.
La proiezione della sola versione nelle catture Fondazioni conserva il controllo
della provenienza runtime introdotto da paratie; resta anche il controllo
specifico della precedente transizione Spettri. Gli attesi numerici non cambiano.

Guide globali Rev52: capitoli di entrambi i rami riuniti, originali conservati
byte per byte in `supporto/SUPERATI/integrazione-f5-rev52-20261019/` (32 file).
Il pacchetto locale richiede standard completo e collaudo WPF sulla stessa
revisione pulita, con identico manifest GPC, ed esempi salvati e riaperti dai test.
Set coerente candidato `gpc-candidate-01`: Release-Set concluso con 3172 PASS e
i soli 17 fallimenti Steel storici ammessi; due test Model ignorati. Design ha
91 test PASS e EmbeddedWalls 67. Snapshot coerente preparato con Update-Snapshot
in `snapshot-01`, senza installazione in `lib/Checker`. Le DLL Geometry,
Utilities e Delaunay conservano le impronte precedenti.

Guide Rev52 finalizzate: 94 pagine pratiche, 163 teoriche e 387 formule
modificabili conservate. Tutte le pagine e le dieci pagine degli indici sono
state rasterizzate e controllate visivamente. Audit del testo di entrambi i
rami e dei 32 originali archiviati superato. Collaudi ANTHEA e pacchetto
integrato sono da registrare alla conclusione delle rispettive esecuzioni.
Le evidenze risiedono in
`supporto/artefatti/paratie/integrazione-f5/`.

Lo snapshot ufficiale `lib/Checker` resta invariato finché non sono autorizzati
i push necessari e completati i controlli prescritti dal treno di rilascio.
La candidata usa un set completo in staging con manifest e SHA-256.

Merge ANTHEA completato in `5fda565c`; cartella principale riportata su
`refactoring/f5`. Primo collaudo mirato: 16 suite PASS e un difetto della Wiki:
WPF-Math 2.1 non supporta `\\mathbf{q}`. La vista usa ora la notazione vettoriale
equivalente con freccia per il solo flusso di Darcy; sorgente copiabile, guide
e formule di calcolo restano invariati. La suite esistente renderizza tutti i
77 capitoli e viene rieseguita; nessun fallimento aggiunto alla lista ammessa.
Evidenza iniziale: `ci/20261019-195405-f5-integrata-target-01`.

## Ricognizione iniziale, prima della richiesta aggiornata

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
coerente compilato da revisioni pushate. Alla data di questa ricognizione
iniziale non era stato ancora eseguito alcun merge.
