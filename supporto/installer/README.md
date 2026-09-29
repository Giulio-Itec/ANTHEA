# Installer di ANTHEA

L'installer è costruito con **NSIS 3** (`ANTHEA.nsi`) e contiene ANTHEA pubblicato
*self-contained* per Windows x64: il runtime .NET 8 è incluso, quindi sul PC di destinazione
non serve installare nulla oltre al setup (Windows 10 o 11 a 64 bit).

## Creare il setup

Requisiti: SDK .NET 8 (come per `Compila.cmd`) e NSIS 3 (`winget install NSIS.NSIS`).

- `Crea installer.cmd` oppure `powershell -File supporto/installer/Crea-Installer.ps1`
- risultato: `supporto/artefatti/installer/ANTHEA-<versione>-Setup-x64.exe`; lo script stampa
  dimensione e SHA256.

La versione è `<Version>` in `X.Desktop/X.Desktop.csproj` (formato `maggiore.minore.patch`):
aggiornarla prima di ogni distribuzione, perché la stessa versione va nell'eseguibile, nel setup,
nel nome del file e in «App installate».

Lo script:

1. pubblica `X.Desktop` con `-r win-x64 --self-contained` in `supporto/artefatti/installer/stage/app`,
   usando una cartella di compilazione separata (`--artifacts-path`): `bin`/`obj` del repository
   e la cartella `app` di `Compila.cmd` restano invariati;
2. copia l'ultima revisione delle guide PDF di `supporto/documentazione/Guide_ANTHEA`;
3. disegna dal logo le immagini delle pagine dell'installer (`welcome.bmp`, `header.bmp`);
4. genera gli elenchi dei file da installare e da rimuovere (`install-files.nsh`, `uninstall-files.nsh`);
5. compila `ANTHEA.nsi` con makensis.

Opzioni: `-SkipPublish` riusa la pubblicazione già presente (per modificare solo lo script NSIS),
`-MakeNsis <percorso>` indica makensis, `-OutputDirectory <cartella>` sposta il risultato.

## Cosa fa il setup

- **Per tutti gli utenti** in `C:\Program Files\ANTHEA` (richiede l'amministratore) oppure
  **solo per l'utente corrente** in `%LOCALAPPDATA%\Programs\ANTHEA`, senza diritti di amministratore.
  Un utente senza diritti installa direttamente per sé.
- Componenti: programma (obbligatorio), guide pratica e teorica in PDF con collegamenti nel menu Start,
  collegamento sul desktop, apertura dei file `.anthea` e `.programma` con doppio clic. Un'eventuale
  associazione precedente viene salvata e ripristinata alla disinstallazione.
- Se ANTHEA è aperto, chiede di chiuderlo prima di sostituire i file.
- Aggiornamento: una versione già installata viene rimossa con il suo disinstallatore, quindi non
  restano file delle versioni precedenti.
- Se un file non può essere scritto, l'installazione si interrompe con un errore (codice 2)
  invece di risultare riuscita.
- La disinstallazione, da «App installate» o `Uninstall.exe`, elimina soltanto i file installati,
  i collegamenti e le chiavi di registro scritte dal setup. Altri file salvati dall'utente
  nella cartella del programma non vengono eliminati.

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
