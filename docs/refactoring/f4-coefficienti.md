# F4.5 — coefficienti dei pali e profili

13 ottobre 2026. Il confine del palo verticale legge i coefficienti mancanti
da `PileResistanceFactors.FromStandard` con `StandardNTC2018Geotechnics`,
tecnologia `PileExecution` e numero di verticali. Rimossi i quattro valori
numerici duplicati per attrito in compressione/trazione e peso favorevole/
sfavorevole. Le letture mantengono la diagnostica dei valori di riserva.

Le assegnazioni esplicite valide hanno precedenza. Valori espliciti vuoti,
non numerici, non finiti o non positivi sono ancora rifiutati. Nessuna
migrazione o modifica del documento durante il calcolo. γb, override e
migrazione una tantum di D7-d sono già implementati e non vengono riscritti.

I profili di taglio/torsione e i coefficienti personalizzati restano nelle
API tipizzate GPC già attive e nella suite `ConcreteLibraryAdapter.Checks`.
La formula locale senza σcp dei muri resta F4.7/F4-03: non è dichiarata
corretta da questa chiusura. Le validazioni residue più generali sono F4.14.

## Prove

- Cattura preventiva `20261013-f4-coeff-before`: 548 file semantici del
  corpus completo (risultati, motori, report, archivi e diagnostica).
- `F4PileCoefficientChecks`: 76 controlli, due famiglie di palo, tre
  tecnologie, tre numeri di verticali, override e valori espliciti invalidi.
- Riusate le prove `verifiche/pile-factors` e
  `ConcreteLibraryAdapter.Checks/rapido`, senza duplicare gli attesi normativi.
- Corsa finale `20261013-f4-coeff-final`: **12 PASS, 0 nuovi fallimenti**;
  Application, architettura, coefficienti, adattatori e confronto completo.
  Tutti i 548 file sono equivalenti secondo il confronto esistente (tempi
  già esclusi); attesi e tolleranze invariati.
- Il primo tentativo rimuoveva il tracciamento dei fallback e il confronto
  lo ha rilevato. Il percorso finale lo conserva; nessuna eccezione aggiunta.
- Baseline architetturale: aggiornate soltanto le quattro impronte delle
  letture mutate, stesso numero di occorrenze ammesse (4610). I coefficienti
  numerici non sono più copiati, il fallback dichiarato rimane rilevabile.

Output del runner nell'area `supporto/artefatti/ci` del worktree
`C:/Users/g.pacini/.codex/worktrees/model-viewer-draft/ANTHEA`; log di riepilogo
nel repository principale `supporto/artefatti/refactoring/f4/coeff-final.log`.
DLL del set V invariate; nessuna nuova API o versione di libreria necessaria.
