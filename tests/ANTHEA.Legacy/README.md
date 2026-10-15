# ANTHEA.Legacy

Codice di `X.Calculations` che il prodotto non usa più, conservato per le prove (decisione D3 del 15/10/2026,
`docs/refactoring/decisioni.md`). Esce dagli assembly distribuiti ma resta nel repository: l'installer pubblica solo
`X.Desktop` e il suo grafo, e nessun progetto di `X.*` o `src/` può referenziare questo
(`tests/ANTHEA.Architecture.Tests`).

- **Contenuto**: motori storici (SLE, taglio e torsione, durabilità, sezione storica, muri, pali, progetto delle armature,
  Bridge Design), rami mai più raggiungibili e la sonda delle prove degli adattatori.
- **Namespace**: `Anthea.Calculations`, come in origine. Le classi intere si spostano con il loro nome. Le parti di classi
  `partial` non possono stare in due assembly: prendono un nome nuovo con il prefisso `Legacy` (per esempio
  `LegacyNtc2018Checks`).
- **Intestazione**: ogni file spostato riporta il file d'origine, il commit di partenza e il motivo.
- **Accesso**: i membri `internal` di `ANTHEA.Calculations` sono visibili tramite `InternalsVisibleTo`. Nessun membro del
  prodotto diventa pubblico per questo progetto.
- **Aggancio (modo b della decisione D3)**: gli adattatori del prodotto leggono l'aggancio corrente con
  `EngineHook.Get<T>()`. È un `AsyncLocal`, nullo in produzione, quindi gli adattatori usano la libreria. Le prove agganciano
  un motore storico per la durata di un ambito: `using (….Use(…)) { … }`, costruito con `EngineScope`. La sonda
  `ServiceabilityProbe` (con la vista `DurabilityProbe`) si aggancia allo stesso modo e registra adattatore, operazione e
  motore di ogni chiamata.
- **Riferimenti**: lo usano soltanto i progetti di prova. Le prove WPF lo ricevono tramite `ANTHEA.Testing`, nella sola
  configurazione `UiTests`.
