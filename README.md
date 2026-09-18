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
`MainWindow` (Home, moduli e progetti), `SheetEditor` e le sue parti `GeoEditor` e
`SectionEditor`, i controlli e adattatori dei dati in `Ui`, e i disegni nativi WPF in
`Drawings`. La composizione delle schermate è in C#; non utilizza controlli WinForms
ospitati né dipendenze da `System.Drawing`.

## Moduli e compatibilità

Sono disponibili palo verticale, micropalo verticale e sezione in c.a. Gli altri
moduli del catalogo restano predisposizioni. La migrazione WPF mantiene la
disposizione dei pannelli esistenti, i comandi File, il ricalcolo automatico del
palo e quello manuale degli altri moduli.

Gli archivi `.programma` / `.anthea` conservano il formato JSON versione 1 e la
gerarchia progetto → struttura → foglio. La migrazione non cambia le formule, il
motore del calcestruzzo né la versione dei dati della sezione. Il collegamento alla
libreria Checker e la nuova organizzazione delle schede saranno interventi separati.

## Controllo automatico dell'interfaccia

Su Windows, dopo la compilazione Release:

```powershell
dotnet X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll --smoke verifiche_wpf/dopo casi_confronto.json
```

Il controllo apre l'applicazione e la chiude a fine prova. Produce schermate PNG,
archivi, risultati JSON, report Word e un riepilogo `smoke.txt`; se una verifica
fallisce scrive `errore.txt` e termina con codice 1. Usare una cartella di output
nuova per ogni esecuzione. I controlli coprono modifiche e invalidazione degli
input, ricalcolo, filtri, espansione dei pannelli, domini, passaggio tra fogli e
salvataggio dei progetti. Le schermate includono finestre da 1600 e 1366 pixel di
larghezza in unità WPF.
