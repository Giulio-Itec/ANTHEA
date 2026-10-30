# Installer di ANTHEA

L'installer è costruito con **NSIS 3** (`ANTHEA.nsi`) e contiene ANTHEA pubblicato
self-contained per Windows x64: il runtime .NET 8 è incluso, quindi sul PC di destinazione
non serve installare nulla oltre al setup (Windows 10 o 11 a 64 bit).

## Creare il setup

Requisiti: SDK .NET 8 (come per `Compila.cmd`) e NSIS 3 (`winget install NSIS.NSIS`).

- `Crea installer.cmd` oppure `powershell -File supporto/installer/Crea-Installer.ps1`
- risultato: `supporto/installer/ANTHEA-<versione>-Setup-x64.exe`, in questa cartella; lo script stampa dimensione e SHA256. I setup sono esclusi da Git (`.gitignore`): circa 50 MB ciascuno. La pubblicazione e i file intermedi restano in `supporto/artefatti/installer`.

Lo script:

1. pubblica `src/ANTHEA.Desktop` con `-r win-x64 --self-contained` in `supporto/artefatti/installer/stage/app`, usando una cartella di compilazione separata (`--artifacts-path`): `bin`/`obj` del repository e la cartella `app` di `Compila.cmd` restano invariati; poi controlla con `tools/qa/Assert-NoTestCode.ps1` che gli assembly pubblicati non contengano le prove WPF (configurazione `UiTests`) e in caso contrario si interrompe senza creare il setup;

2. verifica e copia l'ultima revisione delle due guide generali di `supporto/documentazione/Guide_ANTHEA`, più l'indice e tutti i PDF elencati in `Guide.json`; se manca un PDF o le revisioni delle due guide generali non coincidono, si interrompe prima della pubblicazione;

3. disegna dal logo le immagini delle pagine dell'installer (`welcome.bmp`, `header.bmp`);

4. genera gli elenchi dei file e dei collegamenti da installare e da rimuovere, inclusa la documentazione;

5. compila `ANTHEA.nsi` con makensis.

Opzioni: `-SkipPublish` riusa la pubblicazione già presente, aggiornando comunque guide, esempi e collegamenti,
`-MakeNsis <percorso>` indica makensis, `-OutputDirectory <cartella>` sposta il risultato.

## Versione

La versione è `<Version>` in `src/ANTHEA.Desktop/ANTHEA.Desktop.csproj`, formato `maggiore.minore.patch`
(oggi `1.16.1`). Determina il nome del setup, le proprietà degli eseguibili e la voce in
«App installate». ANTHEA.exe riporta versione file `1.16.1.0` e prodotto `1.16.1+<commit>`.

A parità di versione il setup viene sovrascritto. Prima della distribuzione aumentare
`patch` per correzioni, `minore` per funzioni, `maggiore` per cambi di formato degli archivi.
Il setup sostituisce anche una versione installata più recente, senza conferma.

## Cosa fa il setup

- **Per tutti gli utenti** in `C:\Program Files\ANTHEA` (richiede l'amministratore) oppure **solo per l'utente corrente** in `%LOCALAPPDATA%\Programs\ANTHEA`, senza diritti di amministratore. Un utente senza diritti installa direttamente per sé.
- Componenti obbligatori: programma e documentazione completa in PDF con collegamenti nel menu Start. Componenti selezionabili: esempi per tutti i moduli (selezionati inizialmente), collegamento sul desktop, apertura dei file `.anthea` e `.programma` con doppio clic. Un'eventuale associazione precedente viene salvata e ripristinata alla disinstallazione.
- Se ANTHEA è aperta, anche da un'altra cartella, il setup si blocca prima di disinstallare o cancellare file. Chiudere tutte le finestre e riavviare il setup. Il blocco vale anche in modalità silenziosa (codice 10); il setup non chiude forzatamente l'applicazione.
- Aggiornamento: una versione già installata viene rimossa con il suo disinstallatore; prima della copia, viene poi eliminato **tutto il contenuto della cartella di destinazione**, comprese vecchie DLL, sottocartelle e file aggiunti dall'utente. Conservare progetti, modelli e report fuori dalla cartella del programma.
- La destinazione deve essere una cartella locale dedicata di nome ANTHEA, una cartella nuova oppure un'installazione precedente riconosciuta. Il setup rifiuta percorsi di sistema o profilo e collegamenti nelle cartelle da attraversare o pulire (codice 11). Una pulizia incompleta blocca la copia dei nuovi file (codice 12).
- Se un file non può essere scritto, l'installazione si interrompe con un errore (codice 2) invece di risultare riuscita.
- La disinstallazione, da «App installate» o `Uninstall.exe`, elimina soltanto i file installati, i collegamenti e le chiavi di registro scritte dal setup. Altri file salvati dall'utente nella cartella del programma non vengono eliminati.

## Esempi distribuiti

Il componente **Esempi per tutti i moduli** installa 52 file nella cartella `Esempi`, suddivisi per modulo, e il collegamento Esempi nel menu Start. Dentro ANTHEA si consultano dalla pagina **Esempi** e si aprono come copie senza percorso; il primo salvataggio richiede una destinazione personale. Il componente è facoltativo, selezionato per impostazione iniziale.

La fonte unica è `supporto/esempi/distribuzione/catalogo.json`, con percorso, modulo, descrizione, provenienza e SHA-256 di ciascun file. `Esempi.ps1` verifica integrità e percorsi prima della build e genera elenchi espliciti per installazione e rimozione. I file sono versionati insieme al catalogo. I casi geometrici da wizard indicano nella descrizione le verifiche ancora da completare.

`tests/Installer/Test-Examples.ps1` collauda la sezione reale NSIS in una destinazione isolata, inclusi collegamento, integrità e rimozione che conserva i file personali. Il runner lo esegue con `-Installer`.

Azioni: neve, vento e temperatura negli edifici; vento CNR e temperatura negli impalcati; permanenti e sovraccarichi, combinazioni SLU/SLE, traffico stradale, ripartizione rigida e deformabile, urti veicolari. Dieci fogli con schemi e formule; venti esempi nel catalogo centrale, con dati ANTHEA e relazioni Word/PDF e CSV.

## Documentazione distribuita

La cartella `Guide` accanto ad `ANTHEA.exe` contiene quattro PDF:

- `Indice-guide.pdf`: indice dei due volumi globali;
- `Guida pratica ANTHEA.pdf`: uso e UI di tutti i moduli;
- `Guida teorica ANTHEA.pdf`: teoria, formule, ipotesi e limiti di tutti i moduli;
- `Palo orizzontale in terreno stratificato.pdf`: articolo sul metodo Stratificato di G. Pacini, aperto anche da Diagrammi e dettagli e dalla Wiki.

Gli approfondimenti sono incorporati nelle due guide globali Rev63; l'articolo sul palo orizzontale in terreno stratificato è l'eccezione approvata dall'utente. Nel menu Start sono presenti Indice delle guide, Guida pratica, Guida teorica, Articoli e Documentazione. La documentazione è obbligatoria anche nelle installazioni silenziose.

Per i nuovi argomenti aggiornare i due volumi, i PDF corrispondenti e l’indice. Per casi particolari chiedere all’utente prima di creare un documento autonomo. `Guide.json` contiene l’indice e l’articolo; i due manuali sono selezionati automaticamente alla revisione più recente.

`Documentazione.ps1` usa lo stesso catalogo per copia, collegamenti e disinstallazione;
la rimozione elimina solo i file elencati e le cartelle rimaste vuote.

Il controllo `powershell -File supporto/test/installer/Test-Guide.ps1` verifica il catalogo,
la selezione delle revisioni, i PDF mancanti e l'installazione/disinstallazione della documentazione
in una cartella di prova, senza modificare l'installazione di ANTHEA.
`-SkipUninstall` limita la prova a catalogo, elenchi, installazione e collegamenti,
segnalando esplicitamente che la disinstallazione non è stata eseguita.

Il runner `powershell -File build/ci.ps1 -Stage fast -Only '^installer/' -Installer`
esegue anche le prove di pulizia e blocco in `tests/Installer/Test-Safety.ps1`.
Usa cartelle temporanee e un proprio processo di prova, senza modificare
l'installazione reale, il registro o i collegamenti dell'utente. Richiede NSIS 3.

## Installazione da riga di comando

Riga di comando (installazioni silenziose o distribuite):

```bat
ANTHEA-1.16.1-Setup-x64.exe /S /AllUsers
ANTHEA-1.16.1-Setup-x64.exe /S /CurrentUser /D=C:\Programmi\ANTHEA
"C:\Program Files\ANTHEA\Uninstall.exe" /S /AllUsers
```

`/D=` deve essere l'ultimo argomento, senza virgolette.

## Firma

Il setup non è firmato digitalmente: al primo avvio Windows SmartScreen può mostrare
«Windows ha protetto il PC» (Ulteriori informazioni → Esegui comunque). Con un certificato di
firma del codice si firmano `ANTHEA.exe` prima della compilazione NSIS, il disinstallatore con
`!uninstfinalize` e il setup finale.
