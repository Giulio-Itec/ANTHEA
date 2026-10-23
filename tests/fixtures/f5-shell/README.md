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

Corsa da cui vengono i valori: `supporto/artefatti/ci/20261023-195541-f5-13-g-rif1` del worktree.
