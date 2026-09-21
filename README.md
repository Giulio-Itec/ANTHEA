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

L'interfaccia segue la percentuale di ingrandimento di Windows per ciascun monitor
(DPI PerMonitorV2). La finestra viene contenuta nell'area utile del monitor,
escludendo la barra delle applicazioni, anche dopo un cambio DPI o risoluzione.
I pannelli di palo e micropalo verticali passano a due o a una colonna con finestre
più strette, seguendo la larghezza effettiva del modulo. Nel layout compatto,
stratigrafia, profilo e grafici occupano tutta la larghezza; lo scorrimento verticale
permette di raggiungere tutti i pannelli mentre intestazione e stato rimangono visibili.
Le tabelle conservano lo scorrimento delle proprie colonne. Per le altre superfici
di lavoro più estese rimangono disponibili le barre di scorrimento, senza ridurre
artificialmente la dimensione dei testi.

Nel palo verticale, i pannelli di input separano nome, simbolo, valore e unità.
Ogni nuovo sondaggio parte con uno strato con parametri numerici a zero (Nc = 9) e scelte
del terreno da completare. La tabella stratigrafica mostra nome, colore del profilo
e opzione laterale nella prima colonna; il pulsante di aggiunta segue l'ultima riga
e la colonna finale `[−]` elimina la singola riga. Ogni linguetta di sondaggio offre
un proprio `[−]` per eliminare quella stratigrafia, previa conferma. Le caselle sono sempre editabili
e il ricalcolo automatico non chiude la cella attiva.

Nel calcolo del palo, il contributo di coesione efficace c′ alla resistenza laterale
drenata è nullo negli strati granulari, anche se il campo contiene un valore;
negli strati coesivi resta attivo. Il ramo non drenato mantiene questa stessa
regola nei tratti in cui riutilizza la formulazione drenata.
Controllo dedicato: `dotnet run --project X.Verifiche -c Release -- --coesione`.

Nel modulo palo verticale, le curve drenate sono verdi (compressione scura,
trazione chiara), quelle non drenate viola; le azioni sono rosse in compressione
e azzurre in trazione. Media e minimo sono distinguibili anche per tratteggio.
Le tabelle a video mostrano un decimale e valori centrati, con larghezza massima
contenuta anche in modalità estesa; il calcolo e gli export mantengono la precisione
originale. Il punto Nq è proiettato sugli assi con indicazione di φ e Nq.
I comandi “Copia in…” e “Copia da…” copiano tutti gli strati tra sondaggi,
creando dati indipendenti e richiedendo conferma per sovrascrivere una destinazione.
“Copia in…” permette anche di creare una nuova stratigrafia. Le opzioni vuote
non sono incluse negli elenchi dei menu; i campi non compilati restano senza selezione.

Il riepilogo drenato/non drenato distingue laterale compressione, punta compressione
e laterale trazione. Le componenti della trazione usano il relativo coefficiente di
sicurezza, l'efficienza e il ramo governante propri della trazione.
Tornando alla Home, “Riprendi” riapre il modulo attivo nella stessa sessione con
input, risultati e stato dell'interfaccia conservati; anche il catalogo riprende il
modulo già aperto. Per iniziare un nuovo calcolo usare i comandi “Nuovo…” del menu File.
La conservazione durante la navigazione non sostituisce il salvataggio del file
prima di chiudere il programma.

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
palo e del micropalo e il ricalcolo automatico delle cinque schede in c.a.; il palo
orizzontale mantiene i propri comandi. La sezione in c.a. ha cinque schede:
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

Per verificare l'adattamento dell'interfaccia a quattro dimensioni di finestra e
registrare la scala DPI effettiva, avviare direttamente l'eseguibile con il suo manifest:

```powershell
X.Desktop/bin/Release/net8.0-windows/ANTHEA.exe --smoke-display verifiche_wpf/display
```

Questa prova non cambia la scala di Windows. Per verificare scale diverse e il
passaggio fra monitor, ripeterla sui monitor e con le impostazioni interessate.

Palo e micropalo condividono i pannelli adattivi, la tabella stratigrafica compatta
con copia ed eliminazione individuale, il mantenimento dello stato tornando alla
Home e il ricalcolo senza interrompere la digitazione. Il micropalo conserva il
metodo Bustamante–Doix, con abachi per il sondaggio selezionato e profilo inclinato.
Le relazioni includono profilo e abachi dei sondaggi; i risultati sono presentati
con un decimale senza modificare la precisione del calcolo. La prova `--smoke-display`
controlla queste funzioni su entrambi i moduli, comprese le dipendenze dei campi.
Per confrontare le curve del micropalo con i 34 casi di riferimento:

```powershell
dotnet X.Verifiche/bin/Release/net8.0/ANTHEA.Verifiche.dll --micropalo casi_confronto.json
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
