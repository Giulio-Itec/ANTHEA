# Preparazione di ANTHEA 1.19.1

Bozza del verbale tecnico del compito 191b, 15 novembre 2026. Distribuzione dopo F8 e le decisioni dell'utente;
tag, push e copia dell'installer restano al coordinatore. [Note di rilascio](note-rilascio-1.19.1.md).

## Contenuto e versione

Ramo `codex/191b-rilascio-1.19.1` dal ramo AW del 191, `5a2dcde7b75f055d0a32bfb47f9ed17d14ce37a3`.
Versione 1.19.1: aggiornamento compatibile dello snapshot AV ad AW e correzioni delle prove, senza nuove
funzioni applicative o formati. Questo commit isola versione e documenti per poterli rivedere se cambia
la decisione sul numero. Cambiare il numero richiede anche ricreare e verificare il setup, non rinominarlo.

## Prove di ANTHEA sul set AW

Rapporto `chatgpt-compiti/anthea-aw-191.md`, evidenze in `chatgpt-compiti/evidenze-191`:
33 build PASS e full di 130 suite (73 UI), 129 PASS e un FAIL temporale del confronto PDF materiali.
La diagnosi confronta tutti i 21 oggetti PDF: resta soltanto l'ora stampata attraverso il cambio di minuto
e i metadati già esclusi dal test. Correzione del solo test in `5a2dcde7`, 2 build e 2 suite mirate PASS,
229 controlli su 15 fixture. Il ramo di ripetizione non è stato forzato nella prova successiva.

Dodici F4 conclusi: 6 PASS e 6 FAIL storici; i sei oggetti di confronto coincidono con quelli AW del 183,
già confrontati con AV e classificati nel 188. Gli attesi restano invariati. I residui non qualificati
non sono approvati implicitamente; `build/known-failures.json` resta vuoto.

## Installer

Suite mirate con TEMP/TMP sotto CX, lock esclusivo e senza usare l'installazione reale:
`installer/safety` (26) ed `installer/examples` (206) PASS su `fc2173677a568fe2e1d15d3ce19b8569060e8846`.
`installer/guides` resta FAIL: exit 0 del disinstallatore ma manca il marcatore finale e restano i PDF.
Il tentativo sul tracciamento `29de6e4e` non risolve ed è ritirato con revert `fd1e2811`.
La matrice completa la rimozione senza leggere gli hash e la interrompe dopo la lettura, anche
con due modalità di attesa e con rilascio esplicito dei collegamenti COM. Il test completo fallisce
anche con SHA-256 e rilascio esplicito degli stream; otto aperture esclusive dei PDF riescono.
Il sintomo sotto Documents/Repository era già documentato nel sorgente precedente; la causa di sistema
non è determinata. TEMP non è spostato fuori da CX e nessun controllo è eliminato per ottenere verde.
La distribuzione richiede di chiarire questo FAIL oltre alle decisioni su F8 e F4.
Il setup di produzione non cambia. Tutte le corse e le diagnosi restano nelle evidenze 191b.
Note e README prodotti con `supporto/scripts/documentazione/markdown-pdf.py`: 2 e 3 pagine, tutte controllate.
Il README 1.19.0 è conservato con hash in `supporto/SUPERATI/rilascio-1191-20261115`.

La costruzione successiva a questo commit usa `supporto/installer/Crea-Installer.ps1`, destinazione
`supporto/artefatti/rilascio-1.19.1/installer`, quindi `7z t`, estrazione e `tools/qa/Audit-Installer.py`.
SHA, dimensione, commit del prodotto e risultati del pacchetto saranno registrati prima della consegna.
Non è stata eseguita una nuova full né una prova nativa Straus7/MIDAS; i comandi nativi restano al coordinatore.
