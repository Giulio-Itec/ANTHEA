# Installer ANTHEA 1.8.0 — collaudo del 14 ottobre 2026

Sorgenti dell'applicazione e del setup: `d66379acb8a1eaa2e2008561a5fc2e4f8e809e83`, ramo `refactoring/f4-calcoli-residui`. Versione file `1.8.0.0`, prodotto `1.8.0+d66379acb8a1eaa2e2008561a5fc2e4f8e809e83`.

Pacchetto locale, escluso da Git: `supporto/installer/ANTHEA-1.8.0-Setup-x64.exe`.
Dimensione: 62886541 byte. SHA-256: `C34585B453F776F75E335EE925C72A60A3731C7A29EE4FF2730DD0D7D0ADF43E`.

## Comportamento richiesto

- ANTHEA in esecuzione, anche da una cartella diversa: arresto prima della disinstallazione e della cancellazione, senza terminare il programma. Codice 10 nelle esecuzioni silenziose.
- Prima della copia: rimozione completa dei contenuti della destinazione, inclusi file non appartenenti al precedente elenco. Progetti e report vanno conservati fuori dalla cartella del programma.
- Validazione del percorso locale assoluto e canonico, delle cartelle protette e dei collegamenti in destinazione e negli antenati. Codice 11 se non valido.
- Un file non rimovibile impedisce la copia dei nuovi file. Codice 12 per pulizia incompleta.
- La disinstallazione ordinaria conserva il comportamento precedente: rimuove soltanto file e collegamenti elencati.

## Prove

Runner sul commit pulito: `supporto/artefatti/ci/20261014-installer-180-final/summary.json`, **9 PASS, 0 NEW-FAIL**, nessun file tracciato modificato. Comando: `build/ci.ps1 -Stage build,fast,wiki -Only '^installer/|^Wiki.Tests$|^Architecture.Tests$|^wiki/' -Installer`, eseguito in Windows PowerShell con i permessi per l'SDK locale.

Le suite installer comprendono 26 controlli di sicurezza/pulizia e 30 controlli delle guide. Provati installazione nuova, residui annidati e nascosti, cartella personalizzata riconosciuta, processo ANTHEA di prova esterno, riavvio dopo la chiusura, file bloccato, destinazioni protette, junction interne e negli antenati. Nessuna installazione reale, registro utente o collegamento reale modificato. Rimozione delle guide e conservazione dei file personali collaudate separatamente.

Pubblicazione self-contained win-x64 e compilazione NSIS completate. Dodici DLL identiche al manifest di lib/Checker; Gmsh assente. Tre PDF inclusi: pratica 81 pagine, teoria 145, indice 3. Pratica e README installer aggiornati anche nei sorgenti modificabili e controllati graficamente. Audit: `supporto/artefatti/installer-safety/installer-audit.json`; log: `build-180.log` e `ci-final.log` nella stessa cartella.

## Stato F4

Il collaudo qui descritto riguarda la modifica dell'installer. **F4.16 resta aperta** per la revisione puntuale delle transizioni UI rispetto alla cattura F3: [prospetto](f4-transizioni-ui-da-revisionare.md). La proposta di classificazione per interi file è stata respinta dalla revisione automatica e rimossa; attesi e tolleranze sono invariati. Il secondo full precedente è interrotto e non è conteggiato come PASS. Nessun merge F4 in main o avvio F5 in questo rilascio del pacchetto.
