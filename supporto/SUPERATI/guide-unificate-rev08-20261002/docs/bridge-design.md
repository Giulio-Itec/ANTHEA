# Bridge Design

Modulo `str_bridge_design`, introdotto il 26 settembre 2026. Predimensionamento parametrico indipendente dall'interfaccia, integrato nel catalogo Strutture, nel menu File, nei fogli e nei report di progetto.

## Riferimento e ambito

Riferimento funzionale osservato nel browser: https://thebridgeeng.com/design. Sono stati esplorati i quattro gruppi di input, le otto famiglie, gli automatismi delle campate, le sezioni, le pile, i confronti e le stime. Fra i riscontri: larghezza iniziale 20,2 m, campate terminali continue pari a 0,8 delle interne, intervalli usuali delle famiglie e altezza indicativa delle sezioni a 45 m. Questi riscontri non dimostrano identità dei motori.

La versione ANTHEA riproduce il flusso con grafica nativa WPF e viste vettoriali proprie. Il dettaglio AASHTO del sito non è replicato: viene fornita un'analisi esplicita della trave sotto carichi uniformi, con quantità e stime. Non costituisce una verifica NTC, EC o AASHTO. Il listino è in EUR ed è modificabile; i valori iniziali sono convenzionali.

## Organizzazione del codice

- `X.Calculations/BridgeConcept.cs`: schema, famiglie, valori iniziali, validazione, contratti dei risultati.
- `X.Calculations/BridgeConcept.Calculation.cs`: dimensioni, quantità, trave continua, fondazioni, costi, CO₂ e avvisi. Nessuna dipendenza da WPF e nessuna modifica dell'input.
- `X.Calculations/BridgeConcept.Technical.cs`: prospetti delle dimensioni adottate, campate e fondazioni.
- `X.Calculations/BridgeConcept.Optimization.cs`: ricerca discreta vincolata per costo, CO₂ o compromesso, indipendente dalla UI.
- `X.Desktop/Wpf/BridgeDesignWorkspace.cs`: editor, ricalcolo, confronto A/B, cronologia, suggerimenti ed esportazioni.
- `X.Desktop/Wpf/BridgeDesignTechnical.cs` e `BridgeDesignOptimization.cs`: quote tecniche, scelta dei vincoli, graduatoria e applicazione delle alternative.
- `X.Desktop/Wpf/BridgeDesignDrawing.cs`: prospetto e sezione schematica, diagramma del momento, immagini delle famiglie.
- `X.Core/BridgeConceptExport.cs`: CSV e report Word, riusati dal report di progetto.

I valori automatici sono richiesti con zero nei campi che lo dichiarano e restano zero nell'archivio. I risultati contengono i valori effettivamente adottati. Il confronto A conserva una copia dei dati, del listino e delle ipotesi; non contiene riferimenti mutabili allo stato B.

## Modello

La [guida a sezioni tecniche e ottimizzazione](guida-teorica-anthea.md) descrive la scheda principale **Ottimizzazione**, i parametri bloccabili, gli intervalli percentuali, la traccia dei tentativi, la nuvola costo–CO₂ con frontiera Pareto e la graduatoria delle prime N soluzioni. La preview segue il migliore provvisorio e consente poi di confrontare le alternative senza modificare il progetto. I report e il CSV tecnico usano gli stessi prospetti dimensionali della finestra.

Le formule, i coefficienti e gli esempi sono descritti nel capitolo Bridge Design della [guida teorica](guida-teorica-anthea.md). Il capitolo corrispondente della [guida pratica](guida-pratica-anthea.md) descrive tutti i comandi.

I principali limiti sono: geometrie ideali, EI costante e lordo, carico uniforme su tutte le campate, assenza di inviluppo mobile, armature per incidenza, geotecnica convenzionale, azioni soltanto verticali, carbonio parziale senza EPD e durata senza cronoprogramma. I prezzi dei pali includono cls e perforazione, con armatura separata. Le famiglie fuori dalle luci usuali sono segnalate. La disposizione rispetto all'ostacolo è un tentativo geometrico limitato e può restare irrisolta con avviso.

## Controlli riproducibili

```powershell
dotnet run --project supporto/test/BridgeDesign.Checks -c Release -- supporto/artefatti/bridge_design/calcoli
dotnet build X.Desktop/X.Desktop.csproj -c Release --no-restore
dotnet X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll --smoke-bridge-design supporto/artefatti/bridge_design/interfaccia
```

La suite numerica controlla formule chiuse di travi appoggiate e continue, equilibrio di 200 travi diseguali, 224 combinazioni di famiglie/terreni/pile, proprietà di indipendenza del listino e del paesaggio, quantità, input invalidi, file, baseline e struttura degli export. Sono inclusi determinismo della ricerca, rispetto dei vincoli, costo e CO₂, cancellazione, assenza di candidati, variazioni dei prezzi e ricostruzione delle quantità dalle quote tecniche. La suite desktop esercita quattro tab di input, quattordici famiglie, sei tab dei risultati e la scheda principale Ottimizzazione, undo/listino, invalidazione, A/B, auto, random, PNG, archivio, Word singolo e di progetto, ricerca/applicazione/annullamento e layout a 1600/1366/960/780 pixel.

La campagna live di confronto con il sito è separata dai test interni: sorgenti in `supporto/test/BridgeDesign.SiteComparison`, dati e report in `supporto/artefatti/bridge_design_site_1000`. Un test interno superato non dimostra equivalenza con il sito.

Gli output sono sotto `supporto/artefatti/bridge_design`. Per le guide i sorgenti mantenibili sono i due Markdown in `supporto/docs`, il builder è `supporto/scripts/Build-AntheaGuides.py` e i Word finali sono sotto `supporto/documentazione/Guide_ANTHEA`. Il file di esempio `supporto/esempi/bridge-design.anthea` contiene anche un confronto A e un prezzo modificato.


## Audit e famiglie aggiuntive — 27 settembre 2026

Il catalogo comprende ora 14 famiglie: alle otto iniziali si aggiungono travi incorporate, cassone ortotropo, arco metallico con catena, strallato, sospeso e reticolare. Per le strutture superiori il motore usa equilibri e aree convenzionali dedicati, non i diagrammi della trave ordinaria. I file `BridgeConcept.Advanced*.cs` ne separano schema, impalcato e struttura principale.

Il [report dell'audit generale](guida-teorica-anthea.md) documenta prezzi, formule, correzioni, limiti, 1.008 configurazioni, regressione dei 2.000 input del sito e test finali di ottimizzazione. `BridgeConcept.Foundation.cs` include il peso del plinto nell'autodimensionamento e `BridgeConcept.Pricing.cs` espone i riferimenti economici. Le nuove tariffe iniziali sono calibrate su riscontri ANAS/RER 2026; i prezzi salvati negli archivi restano invariati. La finestra dispone di un caricamento esplicito dei valori orientativi, annullabile.
