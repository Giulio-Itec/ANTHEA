# Fondazioni superficiali

## Stato del lavoro

Implementazione del 12 ottobre 2026 nel ramo `refactoring/f5-fondazioni-superficiali`; sorgenti del motore nel ramo `codex/fondazioni-superficiali` di Checker. Il modulo è registrato come `geo_fondazioni_superficiali` in Geotecnica. Non chiude le altre attività F5 e non elimina motori legacy.

L'insieme distribuito `lib/Checker` è invariato. L'API nuova non esiste nel suo manifest corrente. Il foglio consente preparazione e salvataggio, ma rifiuta il calcolo con un messaggio esplicito finché non viene installato il rilascio approvato. Il confine opzionale verifica `ContractVersion=1`, deserializza il DTO tipizzato della libreria e invoca esclusivamente quel motore: non contiene un algoritmo alternativo. La prova sorgente usa un AssemblyLoadContext separato nel test, senza modificare il percorso delle DLL di produzione.

## Architettura e funzionalità

- Checker: nuova API statica di portanza, separata dai muri; riuso dei coefficienti di `StandardNTC2018Geotechnics` in Model.
- `src/ANTHEA.FoundationWorkspace`: documento versionato, validazione del contenitore, conversione di unità, confine della libreria, presentazione e report Word/PDF; nessuna dipendenza WPF.
- `X.Calculations`: soltanto registrazione e delega. Nessuna formula nuova in X.Calculations, X.Core o X.Desktop.
- `X.Desktop/Wpf/FoundationWorkspace.cs`: campi condizionali, schema, azioni e combinazione, coefficienti e contributi, risultati invalidati alla modifica, aiuto contestuale. Salvataggio/riapertura usano l'archivio esistente; il report del progetto richiama il medesimo report del foglio.
- Wiki: capitoli originali nelle due guide globali, collegamenti bidirezionali, metadati, indice di ricerca e figura originale SVG/PNG. Il manuale Califfo rimane esclusivamente una fonte di lavoro non distribuita.

Le resultanti N/H/M sono già combinate al piano di posa, comprensive dei pesi. Nel drenato N e q sono efficaci; nel non drenato sono totali. Il foglio non genera le combinazioni e non pretende di identificarne l'inviluppo governante. La conferma A1 è esplicita. NTC 2018 usa M1 e R3; il confronto caratteristico non dà un esito normativo.

## Mappa teorica e differenze

La mappa completa delle API, delle fonti e dei riscontri è in `Checker/docs/metodi/fondazioni-superficiali.md`. Prima non esisteva un'API statica generale: si aggiunge una funzionalità, non si corregge indiscriminatamente il motore dei muri. Nessun errore accertato dei calcoli precedenti è stato trovato in questo perimetro. Tre difetti di sviluppo della nuova API sono stati corretti prima del rilascio: lato originale per l'avviso di eccentricità, overflow del risultato e convenzione non drenata con falda.

Varianti implementate: EC7 EN1997-1:2004/AC:2009 allegato D; Terzaghi con Nγ tabulato USACE; Meyerhof; Hansen normale con fattori correlati e ramo non drenato additivo; Vesić con dc correlato. Non si attribuisce validità universale alle osservazioni del manuale. Nessun confronto con il software di riferimento viene assunto come prova indipendente.

Limiti espliciti: terreno resistente omogeneo, piani orizzontali, statico; Terzaghi centrato su striscia/quadrato/cerchio e D/B≤1; Hansen normale; Meyerhof inclinato solo su striscia; cerchio centrato. Esclusi sisma, piani inclinati, compressibilità da indice di rigidezza, generazione combinazioni, cedimenti, scorrimento, stabilità globale e progetto strutturale. Per la compressibilità sono disponibili solo le riduzioni dei parametri Terzaghi e Vesić, con dominio dichiarato. Le lacune che richiedono fonti primarie ancora mancanti non sono state riempite con formule ipotetiche.

## Prove e consegna

Evidenze nella cartella non versionata `supporto/artefatti/fondazioni-superficiali/`:

- `checker/`: 108 test preesistenti e 10 nuovi test passati; attesi numerici indipendenti in Checker `tests/ShallowFoundation.Tests/independent.py`.
- `ci-standard/`: 45 PASS, 1 KNOWN già censito sui default dei muri, 0 NEW-FAIL; nessuna nuova deroga.
- `ci-quick/`: runner `build/ci.ps1`, 29 PASS, 0 nuovi fallimenti. Le esclusioni già previste dal runner restano registrate nel suo riepilogo.
- `final-candidate4/`: 26 controlli su unità, dati invalidi, archivio reale, API assente, API sorgente isolata, UI, risultati obsoleti, ricerca e aiuto Wiki, report Word/PDF e seconda esportazione nella stessa sessione. Ispezionati i temi chiaro, scuro e molto scuro; gli screenshot comprendono i risultati.
- `guide-qa/`: render delle edizioni globali e elenco delle pagine aggiunte. La pipeline Word mantiene 26 parti del template identiche e verifica tutti i frammenti e tutte le formule.

La nuova suite ANTHEA è nel runner (fast e ui); quella Checker è nel runner del treno di rilascio. La prova del candidato non sostituisce il collaudo del prodotto con il set approvato.

Passi residui di rilascio: versione assegnata dall'utente, approvazione merge su master e push Checker, produzione del set coerente con tutte le suite Model/Checker, installazione esclusivamente con `tools/libs/Update-Snapshot.ps1 -ReleaseSet`, aggiornamento manifest/README e profilo full dopo installazione. Nessuno di questi prerequisiti viene aggirato.

Controllo delle guide Rev33: 185 pagine con corpo graficamente identico alla Rev32; le restanti pagine cambiate e le nuove sono state ispezionate. Il confronto testuale esclude variazioni fuori da copertina, indice e aggiunta finale. Le Rev32 e i precedenti sorgenti canonici sono archiviati con impronte in `supporto/SUPERATI/fondazioni-superficiali-rev33/`.
