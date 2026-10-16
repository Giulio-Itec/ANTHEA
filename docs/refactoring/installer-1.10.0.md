# Installer ANTHEA 1.10.0 — collaudo del 16 ottobre 2026

Sorgenti dell'applicazione e del setup: `3bcf375cd02d56bc1d3b2bc8137a3a3d81299552`, ramo `refactoring/integrazione-1016`
(poi `main`). Versione file `1.10.0.0`, prodotto `1.10.0+3bcf375cd02d56bc1d3b2bc8137a3a3d81299552`. Librerie GPC del
rilascio Z (Model `bf8563f5`, Checker `bce7aaae`).

Pacchetto locale, escluso da Git: `supporto/installer/ANTHEA-1.10.0-Setup-x64.exe`.
Dimensione: 62931742 byte. SHA-256: `C60F19A05CCA25469BBD486D7A6446154AA2D2C46527B623E2F7C9158DFF1331`.

## Contenuto della versione

- Correzioni dell'analisi dei bug del 15/10/2026 (voci BUG-* del [registro](registro-differenze.json), esito in
  [analisi-bug-20261015.md](analisi-bug-20261015.md)), con il rilascio Z delle librerie.
- Modulo Stratigrafia e scambio del terreno fra i moduli (ramo `codex/modulo-stratigrafia`).
- Opzioni del calcestruzzo teso: «CLS teso (non lineare)» nello storico della sezione composta e «CLS teso nelle
  risposte» nel M–χ della sezione c.a., entrambe con «Trascurato» predefinito.
- Chiusura di F2 (decisioni E1–E8), guide Rev47 (voce F4-51).

Il comportamento dell'installer non cambia rispetto alla 1.8.0 ([verbale](installer-1.8.0.md)).

## Prove

- Profilo full con tutti gli stadi sul commit `5c3f5baf` (stesso codice; dopo solo registro e diario):
  `supporto/artefatti/ci/20261016-022553-int1016-full2`, **142 PASS, 0 NEW-FAIL**.
- Runner dell'installer sul commit pulito `3bcf375c`: `supporto/artefatti/ci/20261016-030129-installer-1100`,
  **9 PASS, 0 NEW-FAIL**, nessun file tracciato modificato. Comando: `build/ci.ps1 -Stage build,fast,wiki -Only
  '^installer/|^Wiki.Tests$|^Architecture.Tests$|^wiki/' -Installer`. Comprende i 26 controlli di sicurezza e pulizia
  dell'installer e i controlli delle guide.
- Pubblicazione self-contained win-x64 e compilazione NSIS completate (`supporto/installer/Crea-Installer.ps1`). Dodici
  DLL GPC identiche al manifest di `lib/Checker` (SHA-256 confrontati sulla cartella di pubblicazione); Gmsh assente.
  Tre PDF inclusi: pratica 85 pagine, teoria 149, indice 3 (guide Rev47).

Le cartelle dei risultati sono nel worktree di integrazione del 16/10 e vanno conservate in `supporto/artefatti/ci` del
checkout principale.
