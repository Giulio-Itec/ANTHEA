# Integrazione dei branch ANTHEA — 20 ottobre 2026

Richiesta: «merge dei branch di anthea che sono in sviluppo ora».
Destinazione: `refactoring/f5`, ramo di integrazione della fase.

## Branch e risultato

`refactoring/f5-paratie-capsula` aggiunge i cinque commit mancanti:
albero Paratie, correzione della capsula e armature per tratti,
preparazione del candidato 1.12.1 e relativi collaudi/documentazione.
Fast-forward da `9b242761` a `b795a550`, senza conflitti.
L'albero Git risultante è identico a quello del ramo sorgente:
`696a50948bca5431d1620004fc2003792e4a7ed1`.

I rami Spettri, Paratie e seguito, sezioni/persistenza, integrazione,
stratigrafia e tutti gli altri `refactoring/f5-*` erano già antenati
di `refactoring/f5`. Dopo l'operazione, i soli branch locali non inclusi
sono `prova/f5-palificata` e `prova/f5-viste`: contengono selezioni
sperimentali delle viste per build di prova e sono esclusi dal merge.
Nessun branch eliminato, nessun push o aggiornamento di `main` o tag.

La matrice prima dell'operazione è conservata in
`supporto/artefatti/merge-branch-f5-20261020/prima.json`.

## Verifica del ramo integrato

Runner `build/ci.ps1`, profilo `quick`, selezione
`^(Application.Tests|Presentation.Tests|Architecture.Tests)$`,
con `-GpcLibDir supporto/artefatti/capsula-pilastro/snapshot-AC`.

Esecuzione `ci/20261020-174520-merge-f5-sviluppo`, commit pulito
`b795a550813c340d4589418fac9d25b65c928b3b`:

- quattro build PASS, compresa X.Desktop Release;
- Architecture: 562 controlli PASS;
- Application: 3.057 controlli PASS;
- Presentation: 3.523 controlli PASS;
- nessun file versionato modificato dalle prove, nessun nuovo fallimento.

Presentation termina in 1.475,7 s contro i 358,7 s della precedente
esecuzione. Il processo è rimasto attivo e ha prodotto gli artefatti,
concludendo entro il timeout invariato di 1.800 s. La durata anomala
è registrata senza attribuirle una causa non dimostrata; non si modifica
il codice o il criterio di accettazione per questo rallentamento.

Il contenuto integrato conserva inoltre i collaudi WPF, numerici,
dei report e del pacchetto documentati nel
[verbale 1.12.1](installer-1.12.1.md). Questa ripetizione mirata non
equivale al profilo full sul futuro set GPC integrato.

## Raccordo con Model e Checker

L'utente segnala il merge delle librerie in corso da parte di un altro
agente, chiedendo di unire poi il risultato. Lettura dello stato durante
l'attività: Model/master `a5391971`; Checker sul ramo
`codex/merge-checker-three-branches` a `60448490`, che contiene già
`8bf4af33`, necessario per le nuove API della capsula.
Questi riferimenti sono una fotografia del lavoro in corso, non la
designazione di un set GPC rilasciato.

Il merge ANTHEA non modifica i repository delle librerie o `lib/Checker`.
Il manifest ufficiale AB conserva SHA-256
`77966D25FE066599215B102B0DDF711C9AD5D3C30608BD189A5A3EF82F9E83D9`.
Le prove usano il candidato AC, manifest SHA-256
`A46C57838E9F4A2442EDAC59E5D48A5E23DD8F3C5FBB3E51BCC155AB621BF99E`.
La compilazione delle nuove API della capsula richiede questo override
finché lo snapshot ufficiale non viene aggiornato.

Per l'integrazione comune successiva: identificare i commit definitivi,
produrre il set coerente con `Checker/build/Release-Set.ps1`, installarlo
con `tools/libs/Update-Snapshot.ps1` rispettando il requisito dei commit
pubblicati e svolgere il collaudo full previsto dal repository.
La richiesta corrente autorizza il merge locale ANTHEA; non viene
interpretata come una nuova autorizzazione al push.
