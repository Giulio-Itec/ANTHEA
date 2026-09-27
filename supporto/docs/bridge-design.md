# Bridge Design

Modulo `str_bridge_design`, introdotto il 26 settembre 2026. Predimensionamento parametrico indipendente dall'interfaccia, integrato nel catalogo Strutture, nel menu File, nei fogli e nei report di progetto.

## Riferimento e ambito

Riferimento funzionale osservato nel browser: https://thebridgeeng.com/design. Sono stati esplorati i quattro gruppi di input, le otto famiglie, gli automatismi delle campate, le sezioni, le pile, i confronti e le stime. Fra i riscontri: larghezza iniziale 20,2 m, campate terminali continue pari a 0,8 delle interne, intervalli usuali delle famiglie e altezza indicativa delle sezioni a 45 m. Questi riscontri non dimostrano identità dei motori.

La versione ANTHEA riproduce il flusso con grafica nativa WPF e viste vettoriali proprie. Il dettaglio AASHTO del sito non è replicato: viene fornita un'analisi esplicita della trave sotto carichi uniformi, con quantità e stime. Non costituisce una verifica NTC, EC o AASHTO. Il listino è in EUR ed è modificabile; i valori iniziali sono convenzionali.

## Organizzazione del codice

- `X.Calculations/BridgeConcept.cs`: schema, famiglie, valori iniziali, validazione, contratti dei risultati.
- `X.Calculations/BridgeConcept.Calculation.cs`: dimensioni, quantità, trave continua, fondazioni, costi, CO₂ e avvisi. Nessuna dipendenza da WPF e nessuna modifica dell'input.
- `X.Desktop/Wpf/BridgeDesignWorkspace.cs`: editor, ricalcolo, confronto A/B, cronologia, suggerimenti ed esportazioni.
- `X.Desktop/Wpf/BridgeDesignDrawing.cs`: prospetto e sezione schematica, diagramma del momento, immagini delle famiglie.
- `X.Core/BridgeConceptExport.cs`: CSV e report Word, riusati dal report di progetto.

I valori automatici sono richiesti con zero nei campi che lo dichiarano e restano zero nell'archivio. I risultati contengono i valori effettivamente adottati. Il confronto A conserva una copia dei dati, del listino e delle ipotesi; non contiene riferimenti mutabili allo stato B.

## Modello

Le formule, i coefficienti e gli esempi sono descritti nel capitolo Bridge Design della [guida teorica](guida-teorica-anthea.md). Il capitolo corrispondente della [guida pratica](guida-pratica-anthea.md) descrive tutti i comandi.

I principali limiti sono: geometrie ideali, EI costante e lordo, carico uniforme su tutte le campate, assenza di inviluppo mobile, armature per incidenza, geotecnica convenzionale, azioni soltanto verticali, carbonio parziale senza EPD e durata senza cronoprogramma. I prezzi dei pali includono cls e perforazione, con armatura separata. Le famiglie fuori dalle luci usuali sono segnalate. La disposizione rispetto all'ostacolo è un tentativo geometrico limitato e può restare irrisolta con avviso.

## Controlli riproducibili

```powershell
dotnet run --project supporto/test/BridgeDesign.Checks -c Release -- supporto/artefatti/bridge_design/calcoli
dotnet build X.Desktop/X.Desktop.csproj -c Release --no-restore
dotnet X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll --smoke-bridge-design supporto/artefatti/bridge_design/interfaccia
```

La suite numerica controlla formule chiuse di travi appoggiate e continue, equilibrio di 200 travi diseguali, 128 combinazioni di famiglie/terreni/pile, proprietà di indipendenza del listino e del paesaggio, quantità, input invalidi, file, baseline e struttura degli export. La suite desktop esercita i quattro tab di input, le otto famiglie, cinque tab dei risultati, undo/listino, invalidazione, A/B, auto, random, PNG, archivio, Word singolo e di progetto e layout a 1600/1366/960/780 pixel.

Gli output sono sotto `supporto/artefatti/bridge_design`. Per le guide i sorgenti mantenibili sono i due Markdown in `supporto/docs`, il builder è `supporto/scripts/Build-AntheaGuides.py` e i Word finali sono sotto `supporto/documentazione/Guide_ANTHEA`. Il file di esempio `supporto/esempi/bridge-design.anthea` contiene anche un confronto A e un prezzo modificato.
