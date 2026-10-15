# F4.1a — matrice dei servizi e delle prove

**Stato al 14 ottobre:** implementazioni e collegamenti completati nei sorgenti, compreso Bridge Design riattivato dall’utente. [Mappa finale dei servizi e residui espliciti](f4-audit-finale.md). Il testo seguente conserva il riscontro iniziale e le decisioni prima dei port; le sue voci al futuro non rappresentano attività ancora da iniziare. Distribuzione e collaudo finale sono tracciati in piano.md.

Riscontro del 13 ottobre 2026 sul ramo `refactoring/f4-calcoli-residui`, base
ANTHEA 1.7.1 (`46b4c918`). Bridge Design/prezzari e F4.16 sono sospesi su
richiesta dell’utente. La finestra di lavoro concessa è di 90 minuti; nessuna
casella viene chiusa per il solo esaurimento del tempo.

L’inventario iniziale è in `supporto/artefatti/refactoring/f4/avvio-20261013`.
I sorgenti Model `7fd1fe4c` e Checker `75e9adaa` corrispondono ai commit del
manifest distribuito: Model 5.2.0.1, Concrete 0.0.27.4, Geotechnics 0.1.3.3,
CompositeBridge 1.5.0.4. Le firme sotto sono riscontrate nei sorgenti di quel
set e nei chiamanti compilati; non si confonde una futura API con una presente.

## Mappa dei percorsi attivi

| Ambito / ingresso corrente | Servizi esistenti da riusare | Residuo effettivo / destinazione | Prova disponibile e completamento richiesto |
| --- | --- | --- | --- |
| Muri: `RetainingWall.Calculate`, `RetainingWall.Structures` | Geotechnics/Walls per azioni e geotecnica; Concrete per N–M, taglio, SLE e dettagli | Orchestrazione delle sezioni, minimi, taglio senza staffe e raccolta delle curvature in Concrete/Walls; adattamento in Application | `WallCapture`, `CaptureRunner.Walls`, `WallContractChecks`, `RetainingWall.Checks`; estendere materiali, facce, segni, esposizioni e rifiuti |
| Muri: `RetainingWall.Gravity` | Materiali Model e azioni Geotechnics/Walls | Leggi di resistenza a gravità ancora locali; distinguere muratura, CLS non armato e resistenze assegnate prima della collocazione F4.3 | Catture muri e test RetainingWall; aggiungere snellezza/eccentricità e materiali non validi |
| Muri: `CalculateReinforcementDetails` | `MemberDetailingCalculator` (Beam, Column, Slab, Wall), `AnchorageCalculator`, durabilità | Sviluppi geometrici, disposizione e composizione del dettaglio; riuso delle verifiche Concrete e progetto in GPC.Design | `WallContractChecks` e catture muri; due zone, facce asimmetriche, ancoraggi automatici/assegnati |
| Muri: `CalculateBarSchedule` | Geometrie/aree Model, sviluppo/ancoraggi Concrete | Algoritmo della distinta ancora in ANTHEA, da trasferire in GPC.Design | `CaptureRunner.Walls` cattura JSON, CSV e DOCX; ampliare lunghezze commerciali, pannello, copriferri, legature e rifiuti |
| Muri: `DesignReinforcement` | Verifiche del muro e delle sezioni già disponibili | Ricerca discreta e ordinamento delle proposte in GPC.Design | Cattura del progetto muri esistente; variare cataloghi e obiettivo, includere nessuna soluzione e annullamento |
| Pali: `CalculateResponse`, `CompleteReinforcement` | `ElasticPile`, `PileReinforcement`, `ViggianiHorizontalSoil` | Conversioni, esiti e soglie da verificare; non riscrivere FEM | Corpus elastico e `ElasticPileSessionChecks`; cattura sequenziale delle sei leggi, sezioni, tratti, sisma e rifiuti |
| Pali: `ProposeSegments` | `PileSegments`, `PileReinforcement.SuggestBoundaries/SuggestWithDevelopment` | Coordinamento e adattamento dei tratti, da separare dalla geometria del documento | `PileSegmentOperationChecks`; catturare proposte e conservazione degli input |
| Pali: `DesignSegment` | `PileReinforcement.SelectCandidate` e verifiche di resistenza/costruzione/sisma | Ricerca locale più ampia del singolo selettore: unificare le parti equivalenti in GPC.Design, senza perdere conferme o motivi di esclusione | Test della sessione F3; catturare proposta, nessuna soluzione, cataloghi invalidi e annullamento |
| Sezioni: `ConcreteReinforcementDesign.Optimize` | Solutori Concrete, dettagli, ancoraggi, taglio/torsione e SLE | Generazione, filtro, ordinamento, metriche e ricerca in GPC.Design | `ConcreteDesign.Checks`, `ConcreteAuxiliaryContractChecks`; congelare risultati completi e confrontare Workers 1/4 separando i tempi |
| Geometria del taglio: `SectionShearGeometry.Derive` | Sezioni e proprietà geometriche Model | Proposta di bw/d/Asl ancora locale; non è sostituita dalla sola `SectionShearCalculator.Calculate` | Prove adattatore CA e geometria; aggiungere contratto dedicato prima del port |
| Pali verticali: `Calcolo.SicurezzaBase` | `PileResistanceFactors.FromStandard`, `StandardNTC2018Geotechnics.PileExecution` | γb già migrato; restano coefficienti e default da censire, non ripetere D7-d | `verifiche/pile-factors`, corpus verticale e migrazioni; conservare gli override espliciti |
| Esiti di pali/muri | `EngineeringOutcome`, `ExecutionStatus`, `DataStatus` in Model | Composizione degli esiti e stringhe locali da sostituire nei contratti, traduzioni in ANTHEA | Prove dei contratti Application; coprire mancato calcolo, dato insufficiente, errore, annullamento e mancata convergenza |
| Copriferri: `ProjectValidation.CoverChecks` | `ConcreteCoverAnalysis`, `MaterialCover`, API di durabilità Concrete | Il validatore è già in Application; unificare il ramo muri e i contratti senza introdurre formule nel progetto | Test di progetto/materiali e banco durabilità; confronto di avvisi, valori adottati e copriferri richiesti |
| Norme e input: adattatori residui | Profili/norme GPC, lettori validati e migrazioni Application | Default su NTC e letture con zero di riserva: separare dato mancante da zero valido e trasformazione documentale da calcolo | Test Application/archivi; integrare casi negativi e controllo architetturale F4.2 |
| Logica residua UI/predalle/terreni | Servizi spostati in Application da F3; materiali, terreni e geometrie Model | Verificare i chiamanti rimasti; descrivere dati mancanti senza riempitivi fisici fittizi; non rifare le viste F5 | `FinalUiRuleChecks`, `HorizontalSoilChecks`, `BridgeContractChecks`, controlli metadati/sorgenti |
| BridgeConcept | Escluso dalla sessione per richiesta dell’utente | F4.11 sospesa, compresi prezziari e nuove politiche dei dati | Le prove precedenti restano conservate; nessuna nuova implementazione |

## Copertura da costruire in F4.1b

Il banco unico `ANTHEA.Testing` possiede già manifest delle DLL, corpus,
serializzazione canonica, confronto e report. Le catture F4 estendono questo
banco e usano gli input F3 o quelli delle prove esistenti; non si crea un
secondo strumento di confronto. I cinque ambiti autorizzati sono strutture
muri, distinta muri, progetto muri, pali elastici/armature e progetto CA.

Per ogni caso: input conservato, risultato completo o rifiuto atteso, conteggio
di verifiche/candidati, tempo separato e provenienza. Un’eccezione inattesa
deve far fallire il banco, non diventare un risultato di riferimento accettato.
I tempi e gli ID casuali eventualmente esclusi sono nominati esplicitamente;
forze, coefficienti, rapporti, esiti e selezione dei candidati non si normalizzano.

La cattura prova il comportamento prima dello spostamento, non la correttezza
normativa. Per correzioni che cambiano il metodo servono anche attesi indipendenti.
Le prove F3 di uguaglianza fra contratti nativi e proiezioni non sostituiscono
la nuova cattura congelata del motore dopo il rilascio 1.7.1.

## Decisioni ancora da concretizzare

- F4.3: dipendenze di GPC.Design e collocazione della muratura senza cicli;
  nessun progetto aggiunto per la sola compilazione di questa matrice.
- σcp nel taglio senza staffe: il muro ha ancora una formula locale priva
  del contributo; un port equivalente e una correzione normativa sono due passi.
- Il solutore `ElasticPile` usa unità forza/lunghezza coerenti; prima di cambiare
  il contratto servono prove sulle soglie e sulle conversioni dei chiamanti.
- Le classi dei profili normativi e i limiti della libreria si verificano prima
  di scegliere dove collocare coefficienti e override ancora locali.

F4.1a chiusa come inventario e matrice dei percorsi, con lacune e prove indicate.
F4.1 resta aperta finché le catture pertinenti non sono eseguite e confrontate.
