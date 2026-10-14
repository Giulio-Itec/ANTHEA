# F4.13 e F4.14 — audit conclusivo dei percorsi

Riscontro del 14 ottobre 2026, dopo i commit af6a0cbb, 575e2396 e b0a625c0. Questo audit chiude la mappa dei residui del perimetro F4; il collaudo del set distribuito resta F4.9/F4.16. Non certifica l'assenza di ogni operazione matematica in ANTHEA. F5 non avviata.

## Percorsi attivi e riuso

| Ingresso ANTHEA | Proprietario del calcolo dopo F4 | Confine rimasto in ANTHEA | Prova |
| --- | --- | --- | --- |
| Strutture dei muri | Concrete/Walls, solutori N–M e SLE esistenti | Conversione della striscia e segni, mapping risultati | 94114 confronti e correzioni F4-03/F4-08 |
| Gravità e dettagli del muro | Design/Walls, Concrete e Masonry | Dati del foglio e grafica | 56 modelli e 26127 numeri |
| Distinta del muro | Design/WallBarScheduling e dettagli condivisi | Serializzazione, CSV e disegno | 30 distinte, 435 barre, quantità intere esatte |
| Ricerca armature muro | Design/WallReinforcementDesign | Documento e progresso | 12 cataloghi, 68184 numeri |
| Tratti dei pali | Design/PileSegmentDesign, Concrete/PileReinforcement | Sessione e conferme del documento | Proposta e cataloghi con uno/due worker |
| Esiti pali e muri | Enum EngineeringOutcome/ExecutionStatus/DataStatus di Model | Traduzione e aggregazione degli avvisi | 20 pali, 28 tratti, 22188 numeri |
| Progetto di sezione | Design/SectionDesignSearch, catalogo, domande e quantità condivise | Cattura input, adattatore e progresso | 9 casi, 81 candidati, 21579 numeri |
| Bridge Design | Design/Bridges | Documento, prezzari, revisioni, viste | 31 modelli, 17 ricerche, 161550 numeri |
| Geometria del taglio | Design/ShearGeometryProposal | Parametri geometrici del documento | 18 sezioni nel corpus residuo |
| Predalle | CompositeBridge/SlabRebarLayout | Quote assegnate e visualizzazione | 20 casi, inclusi sei rifiuti |
| Micropalo, soli spessori | Geotechnics/MicropileDepthSurvey | Lettura della stratigrafia | Sei casi inclinati; nessun terreno fittizio |
| Braccio del taglio e duttilità | Concrete/ShearLeverArm | Selezione del profilo e dati validati | 42 casi taglio + due transizioni composite |
| Profili CA | Model/ConcreteStandardCatalog | Etichette, override espliciti e migrazioni | Nove profili congelati; rifiuti e idempotenza |
| Copriferri di progetto | Concrete/CoverAssessment | Validatore Application e messaggi | 12 casi congelati e 247 percorsi con sonda |

Prima di aggiungere i tre contratti geometrici sono state riscontrate le API disponibili e congelati 53 casi. Il braccio del taglio e le transizioni normative hanno altri 44 attesi preventivi. Nessun atteso indipendente o limite numerico è stato sostituito. I metodi equivalenti già disponibili (area delle barre, omogeneizzazione, proprietà materiali/aste, lettura celle Excel) sono riusati.

## Residui conservati con scopo esplicito

- `SectionShearGeometry.LegacyDerive`, `BridgeSection.LegacySlabLayout`, `BridgeConcept.LegacyCalculate/LegacyOptimize`, rami diagnostici muri/ricerche e motori `LegacyServiceability`, `Ntc2018Checks`, `ConcreteCodeChecks`: necessari ai confronti storici. I normali ingressi migrati chiamano GPC; nessuna eliminazione senza l'accordo previsto da AGENTS.md.
- `SezioneCA`: il costruttore continua a preparare il contorno parametrico, i fori, le barre e le fibre grafiche usate dall'adattatore. Conserva anche parametri e metodi del motore storico. Il percorso resistente corrente è `ConcreteResistanceAdapter` verso `ISectionResistanceSolver` di Checker; il nome nativo `LegacySectionCalculation` non indica un calcolo ricopiato in ANTHEA. Separare fisicamente la geometria dalla classe storica richiede una successiva operazione con cattura e conservazione delle mesh.
- `CheckerSection.PrepareModel`, `SectionGeometry` e proiezioni dei risultati: adattamento di coordinate, raggi equivalenti, unità, geometria e validazioni d'ingresso. Non sono nuove leggi resistenti; i solutori, i rapporti e le deformazioni provengono da GPC. La geometria consueta non è stata riscritta per azzerare un contatore sintattico.
- `ConcreteMaterials` e preparazione dei documenti conservano i vincoli dell'editor e i default dichiarati dei nuovi fogli. I campi CA ereditati integrati sono ora tracciati; valori presenti ma nulli/errati vengono rifiutati. I moduli geotecnici che supportano soltanto NTC la impostano esplicitamente nel proprio contratto.
- Raster, trasformazioni della camera, colori, logaritmi degli assi, spaziatura, riconoscimento grafico degli anelli nei report e conversioni per le etichette sono presentazione. Ogni espressione resta identificata nella baseline: non è un'esenzione generale di cartella.

Queste conservazioni attuano il criterio di uscita F4.13 «mappa chiusa o eccezioni esplicite». Non anticipano né chiudono i lavori di riordino/MVVM di F5 o le attività autonome del modulo Modelli.

## Protezioni e risultati

La baseline passa da 4610 a 4553 occorrenze: R1 249, R2 2392, R3 2, R4 1382, R5 2, R6 526, R7 0. Le ridenominazioni diagnostiche già registrate non autorizzano formule nuove. Le uniche due R3 rimaste sono nei motori diagnostici `LegacyServiceability.CrackingRequired` e `Ntc2018Checks.Cracking`. I controlli sui riferimenti compilati UI/Application rimangono attivi.

Ultime corse: residual-adapter-c 10 PASS; last-adapters-a 11 PASS e un richiamo UI diretto intercettato; last-adapters-b 5 PASS dopo correzione tramite ConcreteEditor; standard-labels-a 7 PASS con corpus pali equivalente. La correzione mantiene il divieto architetturale, senza introdurre deroghe. La scansione sintattica è una protezione contro regressioni e non sostituisce questo esame dei percorsi.
