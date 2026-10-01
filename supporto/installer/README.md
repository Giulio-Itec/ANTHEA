# Installer di ANTHEA

L'installer è costruito con **NSIS 3** (`ANTHEA.nsi`) e contiene ANTHEA pubblicato
*self-contained* per Windows x64: il runtime .NET 8 è incluso, quindi sul PC di destinazione
non serve installare nulla oltre al setup (Windows 10 o 11 a 64 bit).

## Creare il setup

Requisiti: SDK .NET 8 (come per `Compila.cmd`) e NSIS 3 (`winget install NSIS.NSIS`).

- `Crea installer.cmd` oppure `powershell -File supporto/installer/Crea-Installer.ps1`
- risultato: `supporto/installer/ANTHEA-<versione>-Setup-x64.exe`, in questa cartella; lo script stampa dimensione e SHA256. I setup sono esclusi da Git (`.gitignore`): circa 50 MB ciascuno. La pubblicazione e i file intermedi restano in `supporto/artefatti/installer`.

Lo script:

1. pubblica `X.Desktop` con `-r win-x64 --self-contained` in `supporto/artefatti/installer/stage/app`, usando una cartella di compilazione separata (`--artifacts-path`): `bin`/`obj` del repository e la cartella `app` di `Compila.cmd` restano invariati;

2. verifica e copia l'ultima revisione delle due guide generali di `supporto/documentazione/Guide_ANTHEA`, più l'indice e tutti i PDF elencati in `Guide.json`; se manca un PDF o le revisioni delle due guide generali non coincidono, si interrompe prima della pubblicazione;

3. disegna dal logo le immagini delle pagine dell'installer (`welcome.bmp`, `header.bmp`);

4. genera gli elenchi dei file e dei collegamenti da installare e da rimuovere, inclusa la documentazione;

5. compila `ANTHEA.nsi` con makensis.

Opzioni: `-SkipPublish` riusa la pubblicazione già presente, aggiornando comunque guide e collegamenti,
`-MakeNsis <percorso>` indica makensis, `-OutputDirectory <cartella>` sposta il risultato.

## Versione

La versione è `<Version>` in `X.Desktop/X.Desktop.csproj`, formato `maggiore.minore.patch`
(oggi `1.0.0`). Determina il nome del setup, le proprietà degli eseguibili e la voce in
«App installate». ANTHEA.exe riporta versione file `1.0.0.0` e prodotto `1.0.0+<commit>`.

A parità di versione il setup viene sovrascritto. Prima della distribuzione aumentare
`patch` per correzioni, `minore` per funzioni, `maggiore` per cambi di formato degli archivi.
Il setup sostituisce anche una versione installata più recente, senza conferma.

## Cosa fa il setup

- **Per tutti gli utenti** in `C:\Program Files\ANTHEA` (richiede l'amministratore) oppure **solo per l'utente corrente** in `%LOCALAPPDATA%\Programs\ANTHEA`, senza diritti di amministratore. Un utente senza diritti installa direttamente per sé.
- Componenti obbligatori: programma e documentazione completa in PDF con collegamenti nel menu Start. Componenti selezionabili: collegamento sul desktop, apertura dei file `.anthea` e `.programma` con doppio clic. Un'eventuale associazione precedente viene salvata e ripristinata alla disinstallazione.
- Se ANTHEA è aperto, chiede di chiuderlo prima di sostituire i file.
- Aggiornamento: una versione già installata viene rimossa con il suo disinstallatore, quindi non restano file delle versioni precedenti.
- Se un file non può essere scritto, l'installazione si interrompe con un errore (codice 2) invece di risultare riuscita.
- La disinstallazione, da «App installate» o `Uninstall.exe`, elimina soltanto i file installati, i collegamenti e le chiavi di registro scritte dal setup. Altri file salvati dall'utente nella cartella del programma non vengono eliminati.

## Documentazione distribuita

La cartella `Guide` accanto ad `ANTHEA.exe` contiene sette PDF:

- `Indice-guide.pdf`: mappa dei capitoli per ciascun modulo e degli approfondimenti;
- `Guida pratica ANTHEA.pdf`: uso di tutto il software documentato, dall'avvio alle relazioni;
- `Guida teorica ANTHEA.pdf`: teoria, equazioni e limiti dei moduli documentati;
- `palo-orizzontale.pdf`: capacità orizzontale con Broms;
- `palo-stratificato.pdf`: metodo stratificato per pali e micropali orizzontali;
- `muri-sostegno.pdf`: guida del modulo muri e delle verifiche;
- `stabilita-globale-guida-rapida.pdf`: percorso operativo illustrato.

Le guide generali comprendono materiali, pali e micropali verticali e orizzontali, sezioni in c.a.,
sezioni composte da ponte, Bridge Design, muri e stabilità globale. Per ciascun modulo l'indice
indica dove trovare uso e teoria. I PDF sono consultabili offline con il lettore PDF del computer.

Nel menu **Start → ANTHEA** compaiono **Indice delle guide**, **Guida pratica**, **Guida teorica**,
**Documentazione** (apre la cartella) e la sottocartella **Guide dei moduli**.
La documentazione è obbligatoria anche nelle installazioni silenziose.

Per aggiungere una guida specifica, conservare sorgente modificabile e PDF omonimo in `supporto/docs`,
aggiungere il PDF a `Guide.json` e aggiornare `Indice-guide.md` e `Indice-guide.pdf`.
Il catalogo esplicito evita di distribuire audit interni, evidenze di test o revisioni superate.
`Documentazione.ps1` usa lo stesso catalogo per copia, collegamenti e disinstallazione;
la rimozione elimina solo i file elencati e le cartelle rimaste vuote.

Il controllo `powershell -File supporto/test/installer/Test-Guide.ps1` verifica il catalogo,
la selezione delle revisioni, i PDF mancanti e l'installazione/disinstallazione della documentazione
in una cartella di prova, senza modificare l'installazione di ANTHEA.
`-SkipUninstall` limita la prova a catalogo, elenchi, installazione e collegamenti,
segnalando esplicitamente che la disinstallazione non è stata eseguita.

## Installazione da riga di comando

Riga di comando (installazioni silenziose o distribuite):

```bat
ANTHEA-1.0.0-Setup-x64.exe /S /AllUsers
ANTHEA-1.0.0-Setup-x64.exe /S /CurrentUser /D=C:\Programmi\ANTHEA
"C:\Program Files\ANTHEA\Uninstall.exe" /S /AllUsers
```

`/D=` deve essere l'ultimo argomento, senza virgolette.

## Firma

Il setup non è firmato digitalmente: al primo avvio Windows SmartScreen può mostrare
«Windows ha protetto il PC» (Ulteriori informazioni → Esegui comunque). Con un certificato di
firma del codice si firmano `ANTHEA.exe` prima della compilazione NSIS, il disinstallatore con
`!uninstfinalize` e il setup finale.
