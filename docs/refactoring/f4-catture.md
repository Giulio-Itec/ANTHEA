# F4.1b — catture prima dei trasferimenti

Banco del 13 ottobre 2026, ANTHEA 1.7.1, ramo `refactoring/f4-calcoli-residui`.
Cinque ambiti autorizzati; Bridge Design rimane sospesa e non è dichiarata
coperta da questo banco. Il codice di produzione e il set GPC V sono invariati.

| Ambito | Casi | Copertura principale |
| --- | ---: | --- |
| Strutture muri | 35 | Sei esposizioni, tre resistenze, due zone, facce, dettagli; gravità in muratura, CLS non armato e resistenze assegnate; dati invalidi e annullamento |
| Distinte muri | 32 | Pannelli, lunghezze commerciali, copriferri, legature, quantità intere, CSV e rifiuti |
| Progetto muri | 15 | Cataloghi, obiettivi, una/due zone; soluzioni trovate e non trovate, catalogo invalido e annullamento |
| Pali/armature | 24 | Sei leggi, falda, inversione dei segni, tratti, sisma, staffe/spirali, proposte, dati invalidi e annullamento |
| Progetto sezioni CA | 9 | Rettangolo e cerchio, uno/quattro worker, assenza di soluzione, budget, dato mancante e annullamento |

Esecuzioni finali: **141,7 s** e **142,1 s**. Verifica tramite runner:
`supporto/artefatti/ci/20261013-f4-capture-verified/`, **1 PASS, zero nuovi fallimenti**.
La modalità `-F4CaptureCandidate <cartella con a e b>` verifica catture esistenti
senza ricalcolarle; provenienza e tempi rimangono quelli dei relativi manifest.

Totale **115 casi**, **14 rifiuti attesi**, **234 file semantici** (input,
risultati e testo di quattro report). Il banco rifiuta cartelle già popolate,
modifiche agli input, eccezioni inattese e DLL incoerenti con il manifest.
La copertura non sostituisce gli attesi indipendenti e le prove di solutore,
mancata convergenza, interfaccia e report già disponibili.

## Confronto e conservazione

Il runner unico esegue `baseline/f4`: due catture, confronto comune del banco,
uguaglianza SHA-256 di ogni file semantico, conteggi minimi fissati e confronto
byte per byte dei risultati di progetto CA con uno e quattro worker.
Il corpus osservato serve all'equivalenza del refactoring, non dimostra da solo
la correttezza normativa. Gli attesi indipendenti non sono stati modificati.

Riferimento congelato:
`supporto/artefatti/refactoring/f4/before-port-1.7.1/`.
La mappa versionata `tests/fixtures/f4/reference.json` contiene percorso,
provenienza e SHA-256 dei file: non rigenerarla dopo un cambiamento del motore.
Le due esecuzioni e i log del runner sono conservati nel checkout di prova:
`C:/Users/g.pacini/.codex/worktrees/model-viewer-draft/ANTHEA/supporto/artefatti/ci/20261013-f4-capture-v4/`.
I futuri candidati vanno confrontati con questo riferimento tramite il comando
`ANTHEA.Testing compare`; ogni differenza richiede classificazione nel registro.

Sono separati in `raw/` esclusivamente tempi/contatori strumentali della ricerca
CA, `armature.tratti[*].tempi_ms` del palo, tempi esterni e byte DOCX.
Restano nel confronto numeri, forze, coefficienti, esiti, candidati, ordine,
conteggi di attività/esclusioni, distinte e testo estratto dei report.
Gli ID generati e i percorsi usano la normalizzazione comune già esistente.

## Problemi del banco risolti

Prima del PASS definitivo sono stati corretti due input negativi del banco
(materiale a gravità inizialmente non pertinente e annullamento CA che propaga
`OperationCanceledException`) e due incompatibilità dell’ambiente PowerShell del runner
(percorso relativo e disponibilità del cmdlet hash), risolte con API .NET. Nessuna correzione al motore e nessuna nuova eccezione
nel registro dei fallimenti ammessi. Le esecuzioni precedenti restano conservate.
