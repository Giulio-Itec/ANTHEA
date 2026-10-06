# Programma di sviluppo ANTHEA (6 ottobre 2026)

Script una tantum che hanno prodotto il programma di sviluppo in
`supporto/documentazione/Programma_ANTHEA`.

`crea-programma.mjs` e `leggi-bozza.mjs` importano `@oai/artifact-tool`, un pacchetto
privato presente solo nel runtime Node di Codex. La cartella `node_modules` non è
versionata. Per rieseguire gli script, creare un collegamento temporaneo e toglierlo
subito dopo:

```bat
mklink /J node_modules C:\Users\g.pacini\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\node_modules
rem ... eseguire gli script ...
rmdir node_modules
```

Togliere il collegamento solo con `rmdir`, senza `/s`. Una cancellazione ricorsiva,
oppure un checkout git che la attraversa, agirebbe sui file del runtime.
