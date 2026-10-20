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
Alla richiesta di pubblicazione l'utente ha risposto il 20 ottobre 2026:
«Mantieni per ora il commit solo in locale». La preparazione del pacchetto
è stata quindi conclusa sui rami locali, senza push o tag nuovi.
Alla successiva richiesta di merge dei branch ANTHEA, il ramo
`refactoring/f5-paratie-capsula` è integrato localmente in `refactoring/f5`.
L'utente segnala che un altro agente sta integrando Model e Checker:
il raccordo con il nuovo set GPC è un passaggio successivo.
Il set ufficiale AB in `lib/Checker` è invariato.

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

## Candidata locale e collaudo conclusivo

Setup locale costruito dal commit pulito `fd755c8a424a19f74eda3769ad3c179db38def55`,
usando il set AC candidato senza modificare `lib/Checker`:
`supporto/artefatti/rilascio-1.12.1/installer-candidato/ANTHEA-1.12.1-Setup-x64.exe`.
Dimensione 69.396.917 byte; SHA-256
`8346DDBAFAA0929B818A638CF3348ED83F79028FDD238125B7D322BC14424997`.
L'audit dell'estrazione supera il confronto SHA-256 di 382 file distribuiti,
13 DLL GPC e 52 esempi; 326 file applicativi e tre PDF. Nessun Gmsh
o assembly di test nel pacchetto. È una candidata locale, non una
distribuzione ufficiale.

`ci/20261020-171913-rilascio-1.12.1-candidata` conferma equivalenza F3
senza differenze residue, Architecture e Testing. La nuova prova PDF
non compila per un `using Anthea.Calculations` mancante; corretto nel
solo test. Le tre suite NSIS si interrompono perché il processo figlio
Windows PowerShell non eredita il percorso dei moduli 5.1 e non trova
`Get-FileHash`: si ripetono con `PSModulePath` assegnato al processo
di collaudo. Esiti originali conservati, nessuna eccezione aggiunta
ai fallimenti ammessi.

`ci/20261020-172428-rilascio-1.12.1-pacchetto`, commit pulito
`cba43e82101ecd198cf5b88e5bc40cc4ca371505`: cinque suite PASS,
nessun fallimento o blocco. Reports supera 476 controlli, compreso il
nuovo PDF della capsula; NSIS supera 26 controlli di sicurezza,
30 sulle guide e 120 sugli esempi. Le tre pagine del nuovo PDF sono
renderizzate e controllate: parametri distinti, 50 barre e staffe leggibili,
intestazioni delle tabelle ripetute, nessun taglio del contenuto.
Evidenze visive in `supporto/artefatti/rilascio-1.12.1/capsula-pdf/`.
Tra il commit del setup e quello di questo collaudo cambiano solo il
`using` del test e il verbale: il codice di produzione è identico.

## Stato conclusivo e ambito della verifica

Installer candidato locale 1.12.1 completato e verificato con le suite
mirate riportate sopra. Il profilo ANTHEA `full` non è stato eseguito
su questa candidata: non si dichiara concluso il rilascio ufficiale.
L'utente ha scelto di mantenere locale il commit Checker; non si rinnova
la richiesta di pubblicazione e non si aggiorna lo snapshot ufficiale.

Un eventuale rilascio ufficiale richiederà l'autorizzazione alla
pubblicazione dei nuovi commit GPC, il set ufficiale coerente e il
collaudo completo previsto dal repository, quindi un installer riferito
a quella revisione. La riserva sul test del palo verticale della 1.12.0
non è assunta automaticamente per questo aggiornamento.

Il successivo merge locale di ANTHEA conserva esattamente il contenuto
del candidato e aggiunge solo la documentazione dell'integrazione.
La ripetizione di build, Architecture, Application e Presentation è
conclusa con sette suite PASS sul set AC. Non si ricostruisce l'installer:
il pacchetto sopra indicato conserva il suo commit e la sua impronta.
[Verbale del merge](merge-f5-sviluppo-20261020.md).
