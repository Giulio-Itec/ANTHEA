# ANTHEA

Applicazione desktop Windows per strumenti di calcolo strutturale e geotecnico.
L'interfaccia è **WPF su .NET 8**; il motore di calcolo e gli archivi sono indipendenti dalla presentazione.

## Avvio e compilazione

È necessario un SDK .NET 8 o successivo con supporto desktop Windows; per eseguire
la distribuzione dipendente dal framework occorre il .NET Desktop Runtime 8.

- `Avvia ANTHEA.cmd`: avvia `app/ANTHEA.exe`, se presente, oppure compila e avvia il progetto.
- `Compila.cmd`: compila e pubblica la versione corrente nella cartella `app`.
- `Verifica.cmd`: esegue i confronti numerici e i controlli software.

La soluzione di Visual Studio è `ANTHEA.sln`.

## Organizzazione

| Progetto | Responsabilità |
| --- | --- |
| `X.Core` | Motori di calcolo, archivi JSON, tabelle e report Word |
| `X.Desktop` | Interfaccia WPF, navigazione, editor, grafici e dialoghi |
| `X.Verifiche` | Confronti con `casi_confronto.json`, archivi e report |

In `X.Desktop`, `App.xaml` definisce gli stili condivisi. La cartella `Wpf` contiene
`MainWindow` (Home, moduli e progetti), `SheetEditor` e `GeoEditor` (geotecnica),
`ConcreteWorkspace` con `ConcreteDomains`, `ConcreteStress` e `ConcreteShear` (calcestruzzo),
i controlli e adattatori dei dati in `Ui`, e i disegni nativi WPF in
`Drawings`. La composizione delle schermate è in C#; non utilizza controlli WinForms
ospitati né dipendenze da `System.Drawing`.

## Moduli e compatibilità

Sono disponibili palo verticale, palo orizzontale, micropalo verticale e sezione in c.a. Gli altri
moduli del catalogo restano predisposizioni. La migrazione WPF mantiene la
disposizione dei pannelli geotecnici, i comandi File, il ricalcolo automatico del
palo e il ricalcolo automatico delle cinque schede in c.a.; micropalo e palo
orizzontale mantengono i rispettivi comandi esistenti. La sezione in c.a. ha cinque schede:
pannello di controllo, dominio 3D, dominio 2D, tensioni e fessurazione (Rara,
Frequente, Quasi permanente), taglio. Ogni tabella CA offre template Excel,
reimportazione e Ctrl+C/Ctrl+V. Opzioni avanzate richiudibili, riepiloghi estesi,
selettore delle forze, trasparenza 3D e contouring SLE sono documentati nella guida.

Gli archivi `.programma` / `.anthea` conservano il formato JSON versione 1 e la
gerarchia progetto → struttura → foglio. Il workspace del calcestruzzo usa le
DLL Checker versionate in `lib/Checker`, con compressione negativa. I vecchi
workspace sono migrati una sola volta alla nuova convenzione di N. Domini,
resistenze e tensioni sono calcolati da Checker; taglio e fessurazione integrano
la logica Rhino2Midas con le correzioni NTC 2018 documentate.
Vedere [interfaccia del calcestruzzo](docs/calcestruzzo-interfaccia.md) per funzionalità,
limiti di applicabilità, API collegate e formato dei dati.

## Controllo automatico dell'interfaccia

Il nuovo [modulo orizzontale](docs/palo-orizzontale.md) comprende Broms omogeneo,
estensione multistrato sperimentale, momento resistente della sezione circolare,
diagrammi, export CSV/JSON e relazione Word. L'interfaccia riprende il palo verticale
con il pannello del momento al posto dei grafici. La verifica normativa resta
incompleta; i fattori opzionali sono manuali e richiedono una fonte documentata.
Un esempio riproducibile è in `esempi/palo_orizzontale.json`.

Su Windows, dopo la compilazione Release:

```powershell
dotnet X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll --smoke verifiche_wpf/dopo casi_confronto.json
```

Il controllo apre l'applicazione e la chiude a fine prova. Produce schermate PNG,
archivi, risultati JSON, report Word e un riepilogo `smoke.txt`; se una verifica
fallisce scrive `errore.txt` e termina con codice 1. Usare una cartella di output
nuova per ogni esecuzione. I controlli coprono modifiche e invalidazione degli
input, ricalcolo, filtri, espansione dei pannelli, domini, passaggio tra fogli e
salvataggio dei progetti. Il file `ca_workspace_tests.txt` riepiloga anche le prove
sulle cinque schede CA, con schermate dedicate, domini nativi, selezioni,
invalidazione e verifiche degli esiti parziali. Le schermate includono finestre da 1600 e 1366 pixel di
larghezza in unità WPF.

Il controllo mirato `dotnet X.Verifiche/bin/Release/net8.0/ANTHEA.Verifiche.dll --checker`
verifica il collegamento delle DLL, i benchmark e le correzioni NTC. I limiti e
gli ultimi esiti sono in [verifica Checker](docs/checker-verifica.md).
