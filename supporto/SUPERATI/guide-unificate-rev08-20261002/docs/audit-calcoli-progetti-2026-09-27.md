# Audit ANTHEA: calcoli, dati comuni e progetti

Data: 27 settembre 2026. Revisione del collegamento fra librerie, moduli di calcolo, archivi e interfaccia. Le verifiche riportate distinguono regressioni software e riscontri numerici indipendenti; non costituiscono una validazione indipendente di ogni formulazione disponibile nelle DLL.

## Organizzazione e librerie

Il contenitore trasferibile è `X.Calculations`, che produce `ANTHEA.Calculations.dll` (.NET 8) e non dipende da WPF, `X.Core` o Word. `CalculationService.Calculate` espone i nove moduli del catalogo. I servizi tipizzati sono disponibili direttamente per l’uso interattivo e per il riuso della cache. Il calcolo dei report richiama gli stessi servizi.

| Ambito | Punto di ingresso nella libreria | Dipendenze / modello |
|---|---|---|
| Palo verticale | `Calcolo.Calcola` | Integrazione per strati; `Nq`, coefficienti, falda ed efficienza comuni |
| Micropalo verticale | `Calcolo.Calcola(..., true)` | `BustamanteDoix`, `GeometriaMicropalo`, `Chs` |
| Palo orizzontale | `PaloOrizzontale.Calculate` | Broms; resistenza c.a. da `HorizontalConcreteSection` → Checker |
| Micropalo orizzontale | `PaloOrizzontale.Calculate` | `MicropaloOrizzontale`: CHS, classificazione e interazione N–M |
| Sezione c.a. | `ConcreteAnalysis`, `ConcreteAnalysisSession` | `CheckerSection` → GPCChecker.Concrete; materiali GPC.Model |
| Verifiche accessorie c.a. | `ConcreteShearAnalysis`, `ConcreteDetailingAnalysis`, `ConcreteCurvatureAnalysis` | Servizi numerici autonomi; ipotesi e limiti specifici |
| Sezione composta da ponte | `BridgeSection` | GPCChecker.CompositeBridge: fasi, storico, non lineare, M–κ/N–ε |
| Bridge Design | `BridgeConcept` | Predimensionamento, quantità, costi, CO₂ e ottimizzazione nella libreria |
| Materiali | `ConcreteMaterials`, `ConcreteMaterialCatalog`, `RebarMaterial`, `Materials/` | GPC.Model/ModelData; resistenze, legami, copriferro e durabilità |

Le distanze dai contorni e dai fori e l’appartenenza delle barre al calcestruzzo usano `GPC.Geometry.Polygon2d`; area delle barre e fasci da `Circle2d`, distanze fra barre da `Point2d`. La UI mantiene trasformazioni di schermo, disegno, formattazione e scelta degli input. Non è utile spostare le coordinate in pixel dentro il motore strutturale.

`SezioneCA` conserva la geometria parametrica e i metodi del vecchio motore per compatibilità con i confronti storici. Il percorso produttivo di resistenza/tensioni c.a. e del palo non chiama più quel solutore. La rimozione definitiva del codice storico richiede di separare anche il modello geometrico e archiviare i relativi test Python; non va confusa con la centralizzazione già realizzata.

## Correzioni e incoerenze individuate

1. **Resistenza c.a. del palo:** il precedente algoritmo dedicato usava il vecchio legame del calcestruzzo e l’acciaio elastoplastico, mentre il modulo c.a. impiegava Checker. Ora il palo usa la stessa ricerca diretta a N costante del modulo strutturale. Considera entrambi i versi Mx e prende il minimo valore assoluto, coerentemente con la possibile formazione di cerniere di segno opposto. Non costruisce un dominio 3D per ottenere due punti.
2. **Segni e geometria:** N geotecnico positivo a compressione viene convertito esplicitamente in N negativo per Checker. Il cerchio del motore precedente era integrato come cerchio esatto; Checker usa il poligono inscritto impostato nella sezione. Il numero di lati è ora esposto fra le opzioni del palo e nei risultati. La scelta può modificare la resistenza: a 32 lati il confronto a N = 2.500 kN evidenziava circa lo 0,45% rispetto al cerchio esatto.
3. **Risultati del palo:** il residuo N, la tolleranza di accettazione e il motore effettivo sostituiscono il vecchio indicatore di confronto fra mesh. La tolleranza del servizio condiviso è `max(1 kN, |N|·10⁻⁶)`: non si dichiara un equilibrio a 10⁻⁶ kN. Il piano resistente viene letto dal risultato nativo senza risolvere di nuovo l’equilibrio.
4. **Materiali:** `ConcreteMaterials.DesignValues` alimenta i valori fcd/fyd mostrati nella UI e nei risultati del palo; il catalogo CLS del palo usa Model. Sono esposti legami CLS/acciaio, fu, εu e discretizzazione del contorno. Un legame sconosciuto viene rifiutato. I campi assenti nei file storici conservano i default precedenti; campi esplicitamente vuoti o non finiti restano errori, anche per gli acciai storici che richiedono una deformazione ultima assegnata.
5. **Geometria delle armature:** eliminati i duplicati di distanza/appartenenza nel validatore Checker e i calcoli di area/diametro dei fasci nella UI dei trefoli. Restano i controlli di interferenza con bordi, fori e altre barre.
6. **Confronti di progetto:** `ProjectComparison` legge i campi una volta per foglio e alimenta sia il riepilogo UI sia il piano del report. Non conserva una cache globale: una modifica genera una nuova lettura. Nomi descrittivi dell’acciaio non diventano conflitti fisici; i rami delle staffe rettangolari sono inattivi per il cerchio; la modalità manuale delle barre è riconosciuta anche quando il relativo array è vuoto e incompleto.
7. **Confronto JSON:** eliminata la clonazione ricorsiva per ogni confronto. Si conserva l’equivalenza numerica fra stringhe e numeri, anche con virgola decimale. Corrette due peculiarità: zero e zero negativo sono equivalenti; proprietà differenti con valore null non sono equivalenti. L’ordine degli array resta significativo.
8. **Cache c.a.:** attivare/disattivare l’asse neutro non invalida dominio o tensioni. Il cambiamento di materiali e azioni continua a invalidare il risultato pertinente. Le verifiche di fessurazione vengono rivalutate anche quando le tensioni sono riutilizzabili.
9. **Affidabilità dei test UI:** l’app scrive una conferma unica solo dopo il ritorno dell’intera suite asincrona. Il runner la richiede, oltre al codice d’uscita, evitando sia falsi successi per chiusura anticipata sia falsi fallimenti dovuti alla diversa posizione della parola «OK» nei vecchi log.

Le formule geotecniche di palo verticale, micropalo e Broms non sono state sostituite. Le modifiche contemporanee di Bridge Design appartengono a una lavorazione separata e non fanno parte di questo audit.

## Dati comuni e gestione del progetto

La creazione, il confronto, l’uniformazione e il report usano `ModuleCatalog`, `CalculationCoefficients` e le regole `ProjectSharedData`. Restano locali azioni, combinazioni e storico delle fasi. Si confrontano i fogli nello stesso ambito e gli antenati compatibili; non si equiparano automaticamente i rami fratelli. I coefficienti con uguale simbolo ma significato diverso, per esempio γs geotecnico e γs delle armature, restano distinti.

Il comando di uniformazione mantiene i controlli direzionali di compatibilità e prepara le modifiche prima di applicarle. I valori inattivi rimangono salvati, senza apparire come discrepanze fisiche pertinenti. Un conflitto fra riferimenti non viene risolto scegliendo arbitrariamente un foglio. I tre coefficienti base del c.a. sono autorevoli in `input`; gli alias storici vengono sincronizzati.

Tooltip spiegano unità, segni, coefficienti, legami, discretizzazione, azioni già combinate, falda e abachi. «Modello e dati comuni…» è disponibile per sezioni c.a., pali e micropali; la finestra delle differenze include le regole del confronto. Le spiegazioni sono centralizzate in `CalculationHelp`.

## Prove eseguite

| Prova | Esito |
|---|---|
| Audit nuovo: confronti JSON, conflitti, cache, materiali e resistenza palo | 988 controlli superati |
| Progetti, creazione, servizi e coefficienti | 117 controlli superati |
| Libreria autonoma | 69 controlli superati |
| Checker c.a., Excel azioni, estensioni, dati | 102 + 18 + 174 + 27 controlli superati |
| Modulo c.a. ampliato | 78 controlli superati |
| Palo orizzontale | 1.080 controlli superati; suite CHS superata |
| Coesione e γsat | 312 + 57 controlli superati |
| Micropalo verticale | 34 casi di riferimento; 101.883 controlli superati |
| Ponte | 143 controlli di sezione, 2.957 di metodi/curve, 164 di esempi inclinati/cassoncino |
| Riferimenti Python conservati nel repository | 464/464 casi; 1.025.450 valori; delta assoluto massimo 1,82·10⁻¹² |
| Archivi e report software | 1.181 controlli superati; conferma finale presente |
| UI | Palo/CHS, c.a., estensioni, progetti, condivisione, report progetto, materiali, acciaio, gerarchie e workspace completati |

I riferimenti Python derivano dalla precedente implementazione, non da un programma commerciale indipendente. Gli attesi non sono stati modificati in questa revisione. I vecchi documenti del 26 settembre riportavano 50 differenze: **il confronto della revisione corrente non le riproduce**, con le DLL e gli attesi presenti oggi. Non si attribuisce la loro risoluzione a questo intervento senza ricostruire le revisioni intermedie.

Per il nuovo collegamento del palo a Checker il riscontro separato integra per strisce Simpson il contorno poligonale, calcolato autonomamente per intersezione dei lati. Conserva anche il riferimento analitico del cerchio per verificare la convergenza a 128 lati. Non confronta soltanto due chiamate allo stesso motore. Le prove di geometria e resistenze condivise includono valori analitici di area, inerzia, fcd, fyd e copriferro; cache, ereditarietà e UI sono invece prove software.

I report Word sono stati generati e verificati dai test di contenuto/pacchetto. Questo audit non attesta l’impaginazione di ogni pagina stampata. Le schermate UI sono negli artefatti.

## Prestazioni misurate

Stessa macchina, 24 processori logici; cinque esecuzioni, prima esclusa per riscaldamento. Tempi: mediana delle quattro esecuzioni successive. Allocazioni: media, in MB decimali. Nessuna soglia temporale viene usata per dichiarare corretti i risultati.

| Operazione | Prima | Dopo | Riduzione del tempo | Allocazioni prima → dopo |
|---|---:|---:|---:|---:|
| Confronto dati, 24 fogli | 155,5 ms | 83,0 ms | 46,6% | 56,4 → 11,2 MB |
| Preparazione del piano report, 24 fogli | 293,8 ms | 100,4 ms | 65,8% | 111,6 → 16,3 MB |
| Lettura tensioni di 12 righe già in cache | 0,164 ms | 0,132 ms | non significativa su tempi così piccoli | 0,110 → 0,106 MB |

Entrambi i confronti rilevano 144 conflitti prima e dopo. Il riscontro delle tensioni rimane invariato. Il tempo del piano report non comprende impaginazione e scrittura Word. Queste misure sono indicative, non un benchmark in ambiente isolato.

### Ottimizzazioni successive suggerite

1. **Firma semantica del modello c.a.** Oggi la firma include tutto l’input serializzato: nomi e alcuni campi storici possono provocare ricalcoli superflui. Un contratto tipizzato limitato a geometria/materiali/coefficenti/azioni consentirebbe invalidazioni più precise. Servono regressioni per ogni campo influente.
2. **Preparazione riutilizzabile di sezioni e mesh.** Riutilizzare `CheckerSectionModel` per gruppi di azioni dello stesso foglio; non condividere indiscriminatamente solver mutabili. La costruzione nativa Delaunay è già serializzata perché non thread-safe.
3. **Pali verticali con molti strati.** Precalcolare integrali cumulati per falda, tensione efficace e resistenza laterale, per evitare di ripercorrere tutti gli strati a ogni profondità. Verificare prima falda interna, disattivazione laterale e passaggi di strato contro i 464 riferimenti.
4. **Risultati compatti e output su richiesta.** Separare capacità/residui dai punti dei diagrammi e dalle righe Word. Ridurre il passo del grafico non aumenta la precisione della soluzione di Broms, ma moltiplica memoria e costo di esportazione.
5. **Confronti incrementali su progetti molto grandi.** Lo snapshot elimina le letture ripetute dei campi, ma il numero di coppie allo stesso livello resta quadratico. Un indice per chiave/ambito può ridurre il lavoro; deve conservare i conflitti multipli e le priorità gerarchiche.
6. **Calcoli annullabili e misure per fase.** Nei motori sincroni che accettano l’annullamento solo all’ingresso/uscita, aggiungere checkpoint interni e misurare costruzione, soluzione, raster e report separatamente prima di aumentare il parallelismo.

## Limiti effettivi da mantenere visibili

- Il ponte conserva metodi differenti (incrementale, storico, non lineare): non sono formule intercambiabili da fondere indiscriminatamente.
- Broms fornisce capacità ultima, non cedimenti/spostamenti SLE; l’estensione multistrato rimane sperimentale. Duttilità della cerniera e secondo ordine richiedono verifiche specifiche.
- Per c.a. le regole accessorie di taglio/fessurazione non sono implementate per tutte le normative elencate dal catalogo Model. I messaggi esistenti devono continuare a distinguerle dai risultati del dominio.
- Il caso SLE del trefolo con predeformazione nulla resta esplicitamente rifiutato dall’adattatore perché la DLL usa la predeformazione per distinguerlo dall’armatura ordinaria.
- Sono presenti documenti di validazione storici con esiti superati: questo dossier descrive l’esecuzione corrente, mantenendo tracciabile la cronologia.

Riferimenti normativi generali: [NTC 2018](https://www.gazzettaufficiale.it/eli/id/2018/2/20/18A00716/sg), [Circolare 2019](https://www.gazzettaufficiale.it/atto/serie_generale/caricaDettaglioAtto/originario?atto.codiceRedazionale=19A00855&atto.dataPubblicazioneGazzetta=2019-02-11). Questa revisione ha controllato i percorsi software e le regressioni; i collegamenti non sostituiscono una verifica articolo per articolo delle formulazioni.

## Ripetizione e trasferimento

Sorgenti dei nuovi test: `supporto/test/X.Verifiche/ProjectAuditChecks.cs`, `ProjectAuditBenchmark.cs`; integrazioni a `HorizontalChecks.cs` e `CalculationLibrary.Checks`. Output: `supporto/artefatti/audit-calcoli-progetti/`. Gli artefatti sono esclusi da Git; sorgenti e questo dossier sono versionati.

```powershell
dotnet run --project supporto/test/X.Verifiche -c Release -- --project-audit
dotnet run --project supporto/test/X.Verifiche -c Release -- --horizontal
dotnet run --project supporto/test/X.Verifiche -c Release -- --checker
dotnet run --project supporto/test/X.Verifiche -c Release -- --reference-only supporto/test/casi_confronto.json supporto/artefatti/audit-calcoli-progetti/nuovo-confronto.json
dotnet run --project supporto/test/X.Verifiche -c Release -- --audit-benchmark supporto/artefatti/audit-calcoli-progetti/nuovo-benchmark.json
./supporto/scripts/Test-CalculationUi.ps1
./supporto/scripts/Test-SoftwareReports.ps1
./supporto/scripts/Export-CalculationLibrary.ps1
```

La copia esportata contiene `X.Calculations`, i test autonomi e lo snapshot delle DLL GPC con SHA-256. Si compila ed esegue dalla propria cartella, senza riferimenti ai progetti UI o archivio del repository originale.

La copia `portable/` ha completato i 69 controlli autonomi. La cartella `app/` è aggiornata e ha superato nuovamente palo/CHS, progetti e report progetto. Gli SHA-256 delle DLL distribuite coincidono con `lib/Checker/manifest.json`: Utilities 2.0.0.8, Geometry 2.1.0.3, DelaunayMesh 2.0.0.8, Model 1.4.1.0, ModelData 0.0.1.16, Checker.Concrete 0.0.13.0 e Checker.CompositeBridge 1.3.1.0. Anche `ANTHEA.Calculations.dll` coincide fra build e `app/`.

È stata inoltre compilata una copia dei soli file destinati al commit, escludendo le modifiche concorrenti di Bridge Design: libreria autonoma, audit, palo, Checker e ponte superano le stesse prove. La compilazione desktop di questa copia termina senza errori e con un warning nullable preesistente in `BridgeDesignOptimization.cs`; la lavorazione concorrente contiene la relativa modifica, esclusa dal commit dell’audit.
