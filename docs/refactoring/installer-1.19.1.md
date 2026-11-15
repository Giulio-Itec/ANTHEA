# Preparazione di ANTHEA 1.19.1

Bozza finale del verbale tecnico, compito 191b, 15/11/2026 22:32 +0100.
Installer costruito e verificato soltanto nel clone; nessuna pubblicazione.
**Non pronto alla distribuzione: il collaudo installer/guides resta FAIL**, oltre ai gate F4/F8.
L'utente ha deciso il passaggio ad AW dopo F8. Conferma F8, gestione formale dei sei F4 e numero
definitivo restano alla decisione dell'utente e al coordinatore. [Note di rilascio](note-rilascio-1.19.1.md),
anche in [PDF](note-rilascio-1.19.1.pdf).

## Contenuto e provenienza

Ramo `codex/191b-rilascio-1.19.1` dal 191, `5a2dcde7b75f055d0a32bfb47f9ed17d14ce37a3`.
Set ufficiale `gpc-release-20261114-aw`: Model `c2abf222`, Checker `9c71ca65`, Geometry `6734eee1`,
Utilities `e57b440b`. Quattordici DLL coerenti; manifest non candidato e provenienza dichiarata
da origin/master, verificati nel 191 rispetto al set ufficiale. Nessun aggiornamento remoto in questa attività.
Manifest nel pacchetto verificato: `4E43D4666E40FC4EFCABC3968E9DDDF64D2B0945E6A5BEA11BE09EEB560CA3D0`.

La versione 1.19.1 è un incremento patch per l'allineamento compatibile AV–AW e le correzioni delle prove:
nessun comando nuovo nell'interfaccia o formato documentale nuovo rispetto alla 1.19.0.
Il commit di versione e documentazione è `ddaf66c96ca846483e58e2cfe6d30b3972323c72`. È separato dalle correzioni dei test;
se cambia il numero occorre adeguare questo commit e i documenti, ricostruire e ripetere l'audit del setup.
Non è sufficiente rinominare il file eseguibile.

## Full e F4 del 191

Rapporto locale `chatgpt-compiti/anthea-aw-191.md`, prove in `chatgpt-compiti/evidenze-191`.
Full su `1859bd47c8c845a517a706e079d5231575cdaf95`: 33 build PASS, 130 suite (73 UI),
129 PASS e un FAIL temporale dei PDF materiali. I PDF sono prodotti tra 17:26:59 e 17:27:00:
tutti i 21 oggetti e i sei stream coincidono dopo aver isolato l'ora dell'intestazione e i metadati
già esclusi dal test. Differenza reale dei byte dell'ora, prova instabile al cambio di minuto;
nessuna regressione del contenuto tecnico attribuibile ad AW.

Correzione del solo test in `5a2dcde7`: ripetizione limitata della coppia di esportazioni,
senza modificare `SamePdf`. Verifica mirata: due build e due suite PASS, 229 controlli su 15 fixture.
Il ramo di ripetizione non è stato forzato. Non si dichiara una seconda full verde sul commit successivo.

Dodici F4 completati: sei PASS e sei FAIL storici, con oggetti di confronto identici al 183 AW,
già dimostrati identici AV/AW e classificati nel 188. Restano aperti last-adapters, residual,
input-contracts, section-design, pile-outcomes e structural-adapter. Le proposte di aggiornamento
puntuale dei riferimenti sono nel 191; i residui N–M/MRd non qualificati richiedono verifiche indipendenti.
`build/known-failures.json` resta vuoto: questa preparazione non costituisce accettazione dei sei FAIL.

## Collaudo mirato dell'installer

Le fixture usano TEMP/TMP in `CX/temporary/191b`, fuori dal checkout.

Prima corsa tramite `build/ci.ps1`, 20:59:07–21:00:14 del 15/11, commit
`fc2173677a568fe2e1d15d3ce19b8569060e8846`: safety PASS (26), examples PASS (206), guides NEW-FAIL.
Installazione, hash dei quattro PDF e collegamenti passano; il disinstallatore della fixture restituisce 0,
ma manca `Removal completed` e i PDF rimangono. Il controllo di completamento intercetta il problema.

Un primo tentativo sul tracciamento (`29de6e4e`) non risolve nella suite completa ed è ritirato con
revert `fd1e2811`. Il sorgente torna identico a `fc2173677`; non si conserva una correzione inefficace.
La diagnosi successiva usa gli stessi comandi NSIS e fixture nuove: il caso senza lettura degli hash
completa la rimozione; i quattro casi con lettura degli hash falliscono, anche con le due modalità
di attesa del processo e con rilascio esplicito degli oggetti COM dei collegamenti.

Il test completo fallisce anche calcolando SHA-256 con rilascio esplicito dello stream e del provider.
Tutte le otto aperture esclusive dei PDF prima della rimozione riescono: non è dimostrato un handle
lasciato aperto. Questa seconda modifica resta soltanto nella diagnosi, non nei sorgenti versionati.
La prima diagnosi isolata riuscita ometteva la lettura degli hash: non provava un problema nel log.

Il sorgente precedente del test (`5a2dcde7`, righe 115–117) documentava già lo stesso sintomo sotto
Documents/Repository e il completamento sotto LocalAppData/Temp. Questa attività rispetta però TEMP
sotto CX: non prova la destinazione esterna e non elimina i confronti dei contenuti o la disinstallazione.
La causa OS/processo rimane non determinata; non è attribuita senza prova all'antivirus o a NSIS.
Il risultato è **FAIL guide ancora aperto**, non un PASS parziale né un fallimento aggiunto ai known failures.
Prima della distribuzione occorre qualificare e chiudere questo punto, oltre alle decisioni F4/F8.

Tutte le verifiche riguardano fixture isolate. Nessun setup completo è stato installato sul profilo
dell'utente; nessun codice NSIS di produzione, atteso o tolleranza è stato modificato.


Prima corsa, diagnosi e ripetizione restano distinte in `chatgpt-compiti/evidenze-191b/installer-aggregate.json`.

## Pacchetto locale

Costruito da `ddaf66c96ca846483e58e2cfe6d30b3972323c72` con albero pulito, mediante `supporto/installer/Crea-Installer.ps1`.
Destinazione `supporto/artefatti/rilascio-1.19.1/installer` del clone `CX/ANTHEA-aw`.

| Controllo | Risultato verificato |
|---|---|
| Setup | `ANTHEA-1.19.1-Setup-x64.exe` |
| Dimensione | 74,290,636 byte |
| SHA-256 | `CB0FE61A1F4AFB1BC528DF361876D599444575F60FAF9D0085E235FEF00FB0F7` |
| ProductVersion di ANTHEA.exe estratto | `1.19.1+ddaf66c96ca846483e58e2cfe6d30b3972323c72` |
| Integrità archivio | `7z t` PASS; estrazione con `7z x` |
| Audit-Installer | PASS, 332 file applicazione e 432 payload |
| DLL GPC | 14/14 uguali al manifest AW |
| Guide ed esempi | Rev71, quattro PDF, 95 documenti esempio |
| Worker Straus7 | quattro file presenti, self-contained; St7api.dll assente |
| File vietati | nessuno; controllo esteso anche a GMesh e GPCModelChecker |
| Codice di prova nel prodotto | `Assert-NoTestCode.ps1` PASS durante la costruzione |

Audit completo: `supporto/artefatti/rilascio-1.19.1/installer/audit-payload.json`.
Verifica aggiuntiva: `chatgpt-compiti/evidenze-191b/packaging-verification.json`.
La compilazione registra 15 occorrenze di warning, 11 messaggi distinti,
su sorgenti C# invariati; elenco in `chatgpt-compiti/evidenze-191b/package-warnings.json`. Non sono corretti in questo compito.
Il worker è verificato come contenuto del pacchetto; non è stata eseguita alcuna prova nativa Straus7/MIDAS.
Non è stato eseguito il setup completo sulla macchina dell'utente.

## Documenti, risorse e consegna

Note e README installer prodotti con `supporto/scripts/documentazione/markdown-pdf.py`:
due e tre pagine, tutte renderizzate e controllate. README 1.19.0 conservato con hash in
`supporto/SUPERATI/rilascio-1191-20261115`. Guide Rev71 e attesi non rigenerati:
1,942 file protetti identici per SHA-256 al termine del 191.

Tutte le corse pesanti del 191b hanno tenuto `INT/pesante.lock` con handle esclusivo nello stesso processo,
con verifica preventiva di Rhino/processi, RAM e disco, priorità BelowNormal. Nessuna nuova full, UI o wiki.
Picco osservato del clone 4,529,314,811 byte, sotto 5.000.000.000.
I precedenti bin/obj delle prove già concluse sono conservati per rinomina in `CX/retained/191b`,
con verifica di tutti gli hash; gli input e i congelati non sono stati spostati.

Il tag annotato previsto `v1.19.1`, con `--cleanup=verbatim` e testo delle note, dovrà puntare al commit
del prodotto `ddaf66c96ca846483e58e2cfe6d30b3972323c72`. `main` e `refactoring/f5` arriveranno al commit figlio di questo verbale,
come per la 1.19.0. Comandi e controlli preventivi sono consegnati in
`chatgpt-compiti/evidenze-191b/pubblica-coordinatore.ps1`; non eseguiti da Codex.
La destinazione documentata è `\\STORAGESARZANA\Varie\20 - AGGIORNAMENTI_SOFTWARE\Anthea`:
nuovo setup verificato prima di spostare la 1.19.0 in `_SUPERATI`, senza sovrascritture.
Nessun push, tag, accesso alla condivisione o a X: effettuato.
Con la ricevuta attuale lo script si ferma sul FAIL guide prima di ogni mutazione. Dopo la chiusura
di quel punto occorre aggiornare note, setup, audit e ricevuta; non basta cambiare il campo dell'esito.
