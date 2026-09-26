# ANTHEA.Calculations — separazione e trasferimento

Aggiornamento: 26 settembre 2026.

## Confini

`X.Calculations` produce `ANTHEA.Calculations.dll`, namespace `Anthea.Calculations`, target .NET 8. Contiene motori numerici, adattatori verso Checker/Model, geometrie, modelli di input/risultato, cataloghi, coefficienti, impostazioni e validazioni. I motori Materiali conservano il namespace `Materiali` per compatibilità sorgente. La libreria non dipende da WPF, `X.Core`, archivi o Word.

`X.Core` contiene documenti/progetti, ereditarietà, confronto, salvataggio, importazione Excel e generatori Word. `X.Desktop` e `X.Materiali` contengono controlli, grafica, acquisizione dell’input e presentazione. Conversioni grafiche, formattazione, aggregazione dei risultati già calcolati e impostazioni della vista restano nell’interfaccia.

I metodi dei ponti, compreso lo storico lineare/non lineare e M–κ/N–ε, rimangono in `GPCChecker.CompositeBridge`. `BridgeSection` nella nuova libreria è il confine fra archivi JSON e contratti Checker. La separazione non sostituisce le formulazioni numeriche esistenti e non unifica metodi che rappresentano modelli fisici distinti.

Le forme native usano `GPC.Geometry.Polygon2d` e `Shape2d`; aree, distanze dai contorni e appartenenza al calcestruzzo delegano alla stessa Geometry fornita. Sezioni resistenti, materiali e barre usano i tipi `GPC.Model`. I cataloghi restituiscono istanze nuove per evitare contaminazioni fra calcoli. Il clipping specializzato delle zone efficaci e i contratti JSON degli archivi restano adattatori applicativi. C12/15 e C16/20, già supportate dalle schede materiali ma assenti dal catalogo ModelData dello snapshot, vengono conservate utilizzando il materiale EN1992 di Model.

## API

```csharp
using Anthea.Calculations;
var data = ModuleCatalog.CreateData("str_mista_ponte");
var result = CalculationService.Calculate("str_mista_ponte", data, cancellationToken);
```

- `ModuleCatalog`: ID stabili, un solo nome e descrizione per modulo, factory indipendenti e validazione della struttura.
- `CalculationService.Calculate`: ingresso non visuale per tutti gli otto moduli. Usa una copia dell’input; non aggiorna documenti e non converte implicitamente unità/segnali. Restano disponibili i servizi tipizzati.
- `ConcreteAnalysisSession`: cache locale alla sessione; invalidazione basata sugli input, nessuna cache globale di risultati fra progetti.
- `ConcreteShearAnalysis`, `ConcreteDetailingAnalysis`, `ConcreteCurvatureAnalysis`, `ConcreteSectionProperties`, `ConcreteCoverAnalysis`, `ConcreteBond`: servizi richiamabili senza controlli WPF.
- `CalculationCoefficients`: percorsi autorevoli, etichette, ambito normativo e rilevanza dei coefficienti.
- `CalculationValidation`: coefficienti finiti positivi e normativa supportata. La validazione strutturale dell’archivio permette di salvare input incompleti; il calcolo li rifiuta o restituisce gli errori dei singoli casi secondo il contratto del motore.

Geometrie delle sezioni: mm; tensioni/moduli: MPa; azioni: kN e kNm. Le funzioni geotecniche conservano m e kN. Per le sezioni, compressione negativa; deformazioni secondo il contratto del servizio, senza conversioni implicite fra adimensionale, ‰ e microdeformazioni. Fare riferimento ai nomi dei campi e alla documentazione del singolo modulo.

## Progetti e coefficienti

Normativa e coefficienti hanno gruppi distinti. I valori condivisi vengono trasferiti solo dove chiave, significato e riferimento sono compatibili. `γM0` del ponte e `γM0` del CHS hanno chiavi distinte. `γs` di una scheda di acciaio assegnato può fungere da riferimento esplicito per le armature. Il coefficiente `γR = 1,3` del palo orizzontale resta una costante del metodo, non viene esposto come falso input modificabile.

Un nuovo foglio eredita prima la normativa, poi geometria, materiali, coefficienti, armature e terreno. Le azioni e lo storico delle fasi restano propri del foglio. Conflitti allo stesso livello non vengono risolti scegliendo arbitrariamente un riferimento. I tre coefficienti base CA sono autorevoli in `input`; gli alias storici in `workspace_ca/coefficienti` vengono sincronizzati.

`ProjectValidation` serve anteprima, stato del foglio e report. `ReportProject` aggiunge i controlli del progetto anche se il chiamante non passa avvisi. Il report usa etichette comuni, omette coefficienti non pertinenti e riporta una sola volta i valori comuni nel riepilogo degli input. I nomi attribuiti dall’utente e gli ID degli archivi esistenti restano invariati.

## Trasferimento

Eseguire dal repository:

```powershell
./supporto/scripts/Export-CalculationLibrary.ps1
```

La cartella esportata contiene sorgenti, DLL Checker/Model/Geometry e test indipendenti, escludendo `bin`/`obj`. Non contiene un collegamento a `X.Core` o all’interfaccia. Dalla cartella esportata:

```powershell
dotnet build X.Calculations/X.Calculations.csproj -c Release
dotnet run --project supporto/test/CalculationLibrary.Checks -c Release
```

La dipendenza NuGet è MathNet.Numerics 5.0.0; occorre accesso al pacchetto o alla cache. Le DLL fornite restano necessarie. Per collocarle altrove, impostare `-p:CheckerLibraryDirectory=percorso-assoluto`. Il pacchetto è destinato al trasferimento interno, non è una pubblicazione NuGet né una ridistribuzione pubblica delle dipendenze.

## Validazione

Le prove della separazione e i relativi limiti sono registrati in `supporto/docs/validazione-libreria-calcolo.md`. Distinguono controlli analitici, confronti indipendenti, regressioni interne e verifiche dell’interfaccia. Lo spostamento del codice non certifica l’intero progetto né sana automaticamente le differenze numeriche già rilevate.
