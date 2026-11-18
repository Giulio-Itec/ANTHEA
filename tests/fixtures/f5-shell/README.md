# Valori osservabili della finestra storica (F5.13 g)

Registrati il 23/10/2026 sul ramo `refactoring/f5-13-g`, sulla base `refactoring/f5` 67eafba5, prima di archiviare `MainWindow`
(`docs/refactoring/f5-13-shell.md`, sezione 14). Dopo l'archiviazione le prove della shell nuova confrontano con questi valori al posto della
finestra storica. Non si rigenerano per far passare una prova: se un valore sembra sbagliato, ci si ferma e lo si spiega.

| File | Che cosa contiene | Da dove viene | Chi lo confronta |
| --- | --- | --- | --- |
| `cattura-mainwindow/` (14 file) | lo stato dopo ciascuno dei 12 passi della cattura del progetto, il file salvato ed `esito.txt`, in JSON canonico (identificativi numerati, date oscurate) | cartella `mainwindow/` di `ui/shell-capture` (`MainWindow.CaptureProjectForChecks`) | `ui/shell-capture`, byte per byte, e `ui/shell-capture-confronto` |
| `finestra-storica.json`, `esempi_wiki` | i dati di ogni esempio della Wiki che apre un modulo | `MainWindow.WikiExamples.Create`, uguale a `WikiExamples.Create` della shell nella stessa corsa | `ui/check-shell-draft` |
| `finestra-storica.json`, `report_sezione` | le parti del report Word della sezione di prova e le schede senza risultati | `MainWindow.GenerateSectionReport` (`report-mainwindow.json` di `ui/shell-smoke-project-report`) | `ui/shell-smoke-project-report` |

Il report Word si confronta anche parte per parte, nella stessa corsa, con quello scritto dalla chiamata di `MainWindow` registrata in
`tests/Desktop/ShellReferences.cs` (`SectionReport`: `ProjectReportService.Snapshot`, poi `ProjectReportService.WriteSnapshotAsync` con
`ModuleReportService` e `ModuleFigureRenderer`, il codice di `ProjectReport.cs` a 67eafba5). La fixture tiene solo le parti e il conteggio: i numeri del
report dipendono dalle librerie e la data della copertina dall'ora.

**24/10/2026, librerie GPC da origin/master (AG-SHELL-01).** GPCModel 5.21.0.0 scrive la versione 4 del materiale calcestruzzo
(`ConcreteMaterialEuropeanCommonVersion`) nell'archivio della sezione. Nei 13 file JSON di `cattura-mainwindow/` quel solo valore
è passato da 3 a 4 (23 sostituzioni); i file sono identici byte per byte alla cattura della shell della corsa
`20261024-132441-ag-upstream-ui`. Nessun altro valore è cambiato.

**2/11/2026, criterio resistente per stato limite (CA-CRITERIO-STATI-01, ANTHEA 1.17.1).** I fogli c.a. nuovi hanno un criterio per
stato limite, `workspace_ca.modello_resistente` = {SLU: Plastico, SLV: Elastico}, al posto del «Plastico» unico dei pannelli dominio3d e
dominio2d. Nei 13 file di `cattura-mainwindow/` (26 fogli) e nell'esempio Wiki `beam-ca` di `finestra-storica.json` è cambiato solo questo:
i file di `cattura-mainwindow/` sono identici byte per byte alla cattura della shell della corsa `20261102-111306-criterio-stati-ui-1`
(senza gli scarti dell'ora), trasformati riga per riga per conservare la scrittura .NET.

**4/11/2026, sezioni c.a. nel formato compatto (GPC-F7A-01, ANTHEA 1.19, branch `claude/f7-anthea`; aggiornamento autorizzato
dall'utente, «aggiornala»).** La sezione GPC dei fogli c.a. si salva nel contratto 2 (archivio compatto di GPC in base64) e un file con
sezioni ha la versione 5. Il base64 cambia a ogni corsa (la cattura ricostruisce la sezione con GUID nuovi) e nasconde i GUID alla
normalizzazione: `ShellProjectCapture.DecodeCompactSections` scrive ogni archivio compatto come il documento GPC che contiene, nel testo
che 1.18 memorizzava (`ModelArchive.SaveDocument`), con `"archivio_compatto_decodificato": true` davanti; lo stesso archivio dà lo stesso
testo in tutta la corsa. Nei 13 file JSON di `cattura-mainwindow/` (23 sezioni) sono cambiati solo: `"versione": 1` → `2` di ogni sezione,
la riga `"archivio_compatto_decodificato": true` in più, e nel file salvato `"versione": 3` → `5` con `"versione_senza_compatto": 3` (e la
virgola dopo `"versione_base": 2`). Il testo di ogni `"archivio"` decodificato è identico byte per byte a quello XML registrato, e gli
SHA-256 derivati (`editor_sha256`, `archivio_sha256`, `dati_ref`) hanno la stessa numerazione. I file sono la cattura della shell della
corsa `20261104-144134-f7a-capture-3` del worktree `gpc-f6-claude/F7/ANTHEA`, con gli scarti dell'ora registrati conservati
(`<istante>+02:00`); `esito.txt` non cambia. Nessun'altra fixture è stata toccata.

Corsa da cui vengono i valori: `supporto/artefatti/ci/20261023-195541-f5-13-g-rif1` del worktree.

18/11/2026, voce MERGE-GIULIO-RIFERIMENTI-20261118 (ANTHEA 1.19.4, merge di main 630028b8, con l'OK dell'utente): il foglio di palo verticale del
progetto si apre con il peso proprio sfavorevole predefinito 1,35 (commit `8796bae8`). Nei 13 file JSON di `cattura-mainwindow/`
cambiano solo i 29 valori `"peso_palo_sfavorevole": "1.30"` → `"1.35"`, come nella cattura della corsa `20261118-151110-1194-p2-full-264ce17d`.
