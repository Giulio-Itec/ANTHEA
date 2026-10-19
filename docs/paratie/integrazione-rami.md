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
integrato sono conclusi, con le evidenze finali riportate sotto.
Le evidenze risiedono in
`supporto/artefatti/paratie/integrazione-f5/`.

Lo snapshot ufficiale `lib/Checker` resta invariato finché non sono autorizzati
i push necessari e completati i controlli prescritti dal treno di rilascio.
La candidata usa un set completo in staging con manifest e SHA-256.

Merge ANTHEA completato in `5fda565c`; cartella principale riportata su
`refactoring/f5`. Primo collaudo mirato: 16 suite PASS e un difetto della Wiki:
WPF-Math 2.1 non supporta `\mathbf{q}`. La vista usa ora la notazione vettoriale
equivalente con freccia per il solo flusso di Darcy; sorgente copiabile, guide
e formule di calcolo restano invariati. La suite esistente renderizza tutti i
77 capitoli e viene rieseguita; nessun fallimento aggiunto alla lista ammessa.
Evidenza iniziale: `ci/20261019-195405-f5-integrata-target-01`.

Riesecuzione su `48405d30`: `ci/20261019-195800-f5-integrata-target-02`,
10 suite PASS e nessun nuovo fallimento, comprese Wiki e viste dei due moduli.
Gli archivi degli incrementi e dell'integrazione sono esclusi dalla conversione
automatica dei fine riga in Git: controllo di 96 originali contro le impronte
preesistenti, senza aggiornare gli SHA attesi. Ripristinati soltanto i dodici
Markdown dell'ultimo archivio che il cambio di ramo aveva convertito in CRLF.

## Candidata integrata consegnabile

Revisione applicativa `0f9891598c61efe40720366a8da55c2232d4c344`, pulita e
invariata durante entrambe le corse finali:

- Standard completo: `supporto/artefatti/ci/20261019-201522-f5-integrata-standard-02`,
  75 suite PASS, nessun fallimento. I tre controlli non previsti dal profilo
  sono riportati dal runner, senza attribuire loro un'esecuzione aggiuntiva.
- UI finale nel worktree ParatieWork/ANTHEA:
  `supporto/artefatti/ci/20261019-201640-f5-integrata-ui-final`, dieci suite
  PASS, compresi 59 controlli Paratie, 43 della vista Spettri, contrasto,
  host dei moduli e Wiki con 77 capitoli. Schermate controllate visivamente.
- Entrambe usano lo snapshot con SHA-256 del manifest
  `321e4ce4f3e7f239b543c25a54ccc4bd45ab73b91a338771c4247b003a830694`.

La prima corsa standard su `e5441a0a` aveva 74 PASS e un errore nel driver
di confronto Presentation: i metadati delle catture F3 erano proiettati
sulla versione congelata F5 (0.4.0.0), mentre le transizioni F3 preesistenti
richiedono 0.4.0.1. La correzione `0f989159` rende esplicita la distinzione,
continuando a verificare la versione della DLL realmente caricata. Attesi,
transizioni, valori numerici, tolleranze e liste di fallimenti ammessi sono
invariati. Riesecuzione dedicata: 3411 controlli PASS; successiva corsa
standard completa superata, come riportato sopra.

Pacchetto locale: `supporto/artefatti/paratie/candidata-integrata-01/`,
prodotto con `tools/paratie/New-LocalPreview.ps1`. Include 109 file applicativi,
13 DLL GPC, otto esempi paratie, uno spettri, dieci avviatori, due guide Rev52
in Word/PDF e i riepiloghi dei collaudi. Richiede .NET Desktop Runtime 8 x64.
Il manifest registra 135 file; audit indipendente di byte, dimensioni,
SHA-256, esempi, avviatori e provenienza superato. SHA-256 del manifest:
`e1bfa725d324f0d468ba626b305b824df31fd760c2bd85c05e6b48609ef35482`.
Evidenza: `integrazione-f5/preview-audit.json`. Il primo tentativo di audit
era partito mentre la copia era ancora in corso e ha rilevato il manifest
non ancora presente; l'esito PASS è della riesecuzione dopo il confezionamento.

Questa è una candidata locale degli incrementi 1–13 e Spettri NTC, con i
limiti descritti nella matrice e nelle guide. Non è il rilascio ufficiale:
`lib/Checker` non è stato sostituito e il profilo full successivo alla sua
installazione non è stato eseguito. La build dal checkout usa il set in
staging tramite `-GpcLibDir`; la candidata contiene già il set completo.
Push di Model/master `e1e6e107` e Checker/master `dfb808c8` richiesto
separatamente all'utente; nessun push eseguito alla chiusura di questo verbale.

## Prosecuzione separata

ANTHEA resta su `refactoring/f5` nella cartella principale. Il lavoro riprende
nel worktree ParatieWork/ANTHEA, ramo `refactoring/f5-paratie-seguito`;
Model e Checker usano `codex/paratie-seguito`, dalle revisioni integrate.
Primo passo implementato nelle sole librerie: kernel locale non drenato
del fondo scavo, Model `8c1e79a9` e Checker `56f48103`, con attesi congelati
prima del codice. EmbeddedWalls 74 PASS; runner Model completo e Checker
completo (1912 PASS, stessi 17 fallimenti Steel ammessi) conclusi.
Questo nuovo kernel non è incluso nella candidata e non ha ancora editor
e persistenza ANTHEA. La matrice rimane aperta; l'approfondimento della testa
di pali e micropali resta in coda al modulo paratie.

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
