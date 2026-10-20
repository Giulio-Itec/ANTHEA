# Preparazione di ANTHEA 1.12.1

L'utente ha confermato il numero 1.12.1 il 20 ottobre 2026.
Il rilascio comprende l'albero di lavoro richiudibile di Paratie,
la capsula con armature distinte sui tratti curvi e rettilinei e i
dettagli generali della capsula come pilastro. Conserva i 52 esempi
organizzati in 15 moduli e distribuisce le due guide globali Rev56.
La 1.12.0 pubblicata resta identificata dal suo tag e dal suo installer.

## Codice e librerie

ANTHEA: ramo `refactoring/f5-paratie-capsula`, derivato da
`refactoring/f5` dopo la pubblicazione della 1.12.0.
Checker: commit locale `8bf4af33`; candidato AC completo, 3.181 PASS,
17 fallimenti Steel già ammessi e due prove Model ignorate.
La pubblicazione di questo nuovo commit è richiesta separatamente
per poter produrre e installare il set ufficiale.
Il set ufficiale AB in `lib/Checker` è per ora invariato.

## Prove già eseguite

`ci/20261020-164315-paratie-capsula-integrazione`, candidato AC:
Application 3.057, Presentation 3.523, Architecture, WPF Paratie 79 e
wizard sezioni 84 PASS. Viste controllate nei tre temi e a pannello chiuso.
Nove geometrie preesistenti restano identiche bit per bit.

`ci/20261020-170550-paratie-capsula-documenti`: 13 PASS e un rilievo
F3 editoriale. Reports 475; Examples 249 su 52 documenti; Wiki,
Testing, Architecture e Wiki WPF PASS. Le 26 differenze F3 sono
22 nuove righe editoriali e quattro transizioni superate: aggiornate
nel registro dedicato, senza cambiare gli attesi numerici o le transizioni F4.
Le 30 ricerche restano integralmente identiche alla 1.12.0.
L'audit completo conserva anche il file diagnostico della prima esecuzione.

37 calcoli di esempio: 301.420 numeri e 97.689 testi identici.
Le sole differenze sono tre versioni del motore e 18 campi impronta;
20 impronte sono riprodotte indipendentemente dagli input e dai manifest.
Evidenze in `supporto/artefatti/paratie-albero/` e
`supporto/artefatti/rilascio-1.12.1/f3-diagnosi/`.

Guide Rev56: 264 pagine e 387 formule modificabili; parti immutabili
ITEC, testi e stili controllati. Le 18 revisioni sostituite sono archiviate
con impronte. PDF e indici renderizzati e controllati.

## Passi residui

Pubblicazione autorizzata delle GPC, set ufficiale coerente, collaudo completo
ANTHEA, installer con audit di tutti i file distribuiti e pubblicazione
finale da autorizzare. La riserva sul test del palo verticale della 1.12.0
non è assunta automaticamente come autorizzazione per questo rilascio.
