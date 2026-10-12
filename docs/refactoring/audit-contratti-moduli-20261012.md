# Contratti dei moduli e riuso delle API — 12 ottobre 2026

## Perimetro e criterio

Richiesta dell'utente: controllare che i moduli usino i dati delle librerie di calcolo;
successiva istruzione: usare le API già disponibili, rimuovere i duplicati e segnalare
quanto non può essere sostituito. Ramo `refactoring/f4-riuso-api`.

Il controllo iniziale trova **non conformità ancora attive**. Non basta avere una
dipendenza da Model/Checker per delegare tutti i calcoli. Allo stesso tempo un DTO
di archivio, una traduzione italiana o una preferenza grafica non sono un secondo
modello ingegneristico e non devono essere aggiunti a Model.

Inventario iniziale: 12 moduli del catalogo, 9 progetti di produzione, 392 sorgenti
C# effettivamente compilati in Release; 598 sorgenti nelle librerie Model/Checker.
L'inventario usa gli elementi Compile di MSBuild. La ricerca lessicale di classi,
import e chiavi JSON aiuta l'ispezione, **non certifica la corrispondenza semantica
di ogni campo**. I percorsi di calcolo e i casi descritti sotto sono stati letti
manualmente. Non è una nuova validazione normativa di tutti i metodi.

Riferimenti iniziali: ANTHEA `76e791644b7602d22b72a1dda0e17dd46c9c6f9f`,
Model `d39fbe93bd5b1f138aa75f9e728aef3725cfbf90`,
Checker `e5a01a2b745f04295734e1bc36de39c3a5d433ca`.
I sorgenti candidati sono distinti dalle DLL distribuite: ispezione dei metadati PE
di Model 5.0.0.0, Concrete 0.0.27.0, Geotechnics 0.1.1.6 e CompositeBridge 1.5.0.0.

Evidenze iniziali in `supporto/artefatti/audit-contratti-moduli-20261012/`:
`inventory.json` (con SHA-256 dei sorgenti), `installed-api.json` (tipi selezionati
delle DLL). Strumento ripetibile: `tools/qa/Audit-DomainBoundary.py`.
Cattura prima/dopo della sostituzione in `supporto/artefatti/riuso-api-20261012/`.

## Mappa iniziale dei moduli

| Modulo | API effettivamente usate | Residui o limiti riscontrati |
| --- | --- | --- |
| Fondazioni superficiali | Model `Soil`, `SoilLayer`, `SoilProfile`; nuova API Checker `ShallowFoundationInput`/`ShallowFoundationBearing` | Adattatore di documento senza WPF, collegamento opzionale tramite reflection. API presente nei sorgenti candidati ma assente dalla DLL distribuita: il calcolo viene esplicitamente bloccato. Nessun motore alternativo. |
| Palificata orizzontale | `LateralGroupInput`, `GroupPile`, `PileLayoutOptions`, `PileGroupLayout`, `LateralPileGroup` | Dati grafici e aggregazioni di presentazione locali; nessun secondo motore di efficienza rilevato nel percorso eseguito. |
| Palo verticale | `SoilProfile`, `AxialPileSurvey`, `AxialPileLayer`, `AxialPileCapacity`, `PileResistanceFactors`, `PileGroupEfficiency` | Conversioni locali e convenzione di normalizzazione di γsat; classificazione e addensamento hanno già equivalenti in Checker. |
| Palo orizzontale | `SoilProfile`, `LateralPileSurvey`, `LateralPileCapacity`; modalità elastica `ElasticPileInput`/`ElasticPile`, `PileReinforcement` | Pesi di riferimento per profili interamente coesivi senza γ; adattatori e scelta locale delle direzioni resistenti. |
| Micropalo verticale | `MicropileSurvey`, `MicropileLayer`, `BustamanteDoix`, `AxialPileCapacity` | Materiale terreno riempitivo con γ=18, γsat=20 kN/m³, φ=c=0 per trasportare le sole quote. |
| Micropalo orizzontale | Stesso motore laterale del palo, sezione CHS e API Steel; modalità elastica GPC | Stessa convenzione dei pesi del palo orizzontale. |
| Muri di sostegno | `WallInput`, Model Soil/Profile, Checker Walls per geotecnica; resistenza della sezione in Checker | Verifiche strutturali, armature minime, muri a gravità, progetto/distinta e ancoraggi ancora locali; SLE con default legacy. |
| Sezione in c.a. | Model materiali/sezioni; resistenza SLU, taglio/torsione, durabilità in Checker | SLE predefinito legacy; dettagli, ancoraggi e M–χ locali nonostante API distribuite; ricerca armature e proposta geometrica del taglio locali. |
| Sezione composta da ponte | `HBridgeInput`, `HSectionDimensions`, `BridgeMaterials`, `HBridgeSection`, `HBridgeHistoryResults` | `BridgeSlabLayout` deriva localmente le quote delle barre dalla predalle; default dei coefficienti ripetuti. |
| Bridge Design | Riutilizzi puntuali non sostituiscono il motore del modulo | `BridgeConcept.*` contiene famiglie, predimensionamento, geometrie, rigidezze, azioni, fondazioni, quantità, listini e CO₂ propri. Destinazione prevista GPC.Design, F4.11. |
| Calcestruzzo | Catalogo Model; durabilità, copriferri e composizione tramite adattatore Checker | Aderenza calcolata in `ConcreteBond`; non confondere le facciate `MinimumConcrete`/`MixAutomation` con motori autonomi. |
| Acciaio per armature | Catalogo Model; gli altri moduli costruiscono `SteelMaterial` con `ConcreteMaterials.Rebar` | La scheda usa un proprio `Values`, calcola fyd e deformazioni e costruisce il diagramma localmente. |

Punti d'ingresso: [catalogo](../../X.Calculations/ModuleCatalog.cs),
[servizio](../../X.Calculations/CalculationService.cs),
[fondazioni](../../src/ANTHEA.FoundationWorkspace/FoundationDocument.cs).

## Terreno e materiali condivisi

| Dato del foglio | Proprietà o contratto GPC |
| --- | --- |
| Spessore e quota dello strato | `SoilLayer.Top/Bottom`, `SoilProfile` |
| γ, γsat | `Soil.UnitWeight/SaturatedUnitWeight`, conversione kN/m³ → N/mm³ |
| φ′ | `Soil.FrictionAngle`, gradi → radianti |
| c′, cu | `Soil.EffectiveCohesion/UndrainedShearStrength`, kPa → MPa |
| Tipologia granulare/coesivo | `SoilBehaviour` nei metodi dei pali; nelle fondazioni la condizione drenata/non drenata è una scelta distinta |
| Addensamento, Nc, strato attivo | `SoilDensity`, `AxialPileLayer` |
| E, Eoed, ν quando richiesti | Proprietà opzionali di `Soil`; non sono proprietà da reinventare in ANTHEA |
| CLS fck, nome e diagramma | `ConcreteMaterialEN1992`, `ConcreteMaterial.CompressionStressStrainDiagrams` |
| Acciaio fyk, fu, E, εu e diagramma | `SteelMaterial`; deformazioni del foglio in ‰, materiale in valore assoluto |
| Coefficienti parziali | Contratti delle norme e input dei checker, non nuove proprietà del terreno |

`X.Core/ProjectSharedFields.cs` e `ProjectSharedSoils.cs` condividono i campi dei
fogli, senza un solutore di terreno alternativo. Identità degli strati, collegamenti
fra fogli, nomi, percorsi di archivio e stato delle viste restano dati applicativi.
Nelle fondazioni i vecchi scalari del terreno servono alla migrazione: con un profilo
presente non sono una seconda sorgente attiva. Il CLS descrive il manufatto e non
implica una verifica strutturale della fondazione.

Le convenzioni di γ richiedono invece un intervento sul contratto:

- `Calcolo.cs`, micropalo: i valori 18/20/0/0 sono inseriti dal chiamante, non
  dall'utente. `MicropileLayer.FromProfile` legge soltanto quota superiore/inferiore
  e piano campagna; questi valori non entrano nella resistenza di Bustamante–Doix.
  Serve un ingresso stratigrafico che non richieda un materiale fisico inutilizzato.
- `PaloOrizzontale.cs`, profilo interamente coesivo: γ mancante viene sostituito
  con 18. `LateralPileCapacity.Ground` esclude i pesi dalla capacità in questo caso;
  l'adattatore sopprime le tensioni se i pesi reali mancano. Il profilo passato alla
  libreria contiene comunque un dato fittizio, da eliminare tramite un contratto
  che esprima l'assenza del dato senza alterare l'analisi.
- `Calcolo.Terreno` normalizza γsat con `Math.Max(saturated,gamma)`; i controlli
  respingono l'incoerenza quando il dato è attivo sotto falda. Normalizzazione e
  dati effettivamente assegnati devono restare distinguibili.

Questi rilievi non dimostrano da soli un errore della capacità portante.

## Duplicati con API distribuita

1. `ConcreteBond`, `ConcreteAnchorageCalculator` → `AnchorageCalculator.Bond`,
   `BondStrength`, `Calculate`. Preservare il campo normativo NTC già esposto e
   distinguere il tetto C60 applicato dalla scheda dal comportamento storico dei muri.
2. `ConcreteDetailingCalculator` → `MemberDetailingInput/Options`,
   `PlateDetailingData`, `MemberDetailingCalculator`. Solette e pareti sono già
   disponibili nella DLL attuale: la vecchia matrice F2.1 non basta a descriverla.
3. `MomentCurvatureCalculator` → `MomentCurvatureAnalysis` con unità del chiamante,
   punto limite pigro, esiti di interruzione e rifiuti tipizzati. Tradurre i testi,
   non riprodurre campionamento, bisezioni o regole di esito.
4. `TensionBarSpacing` → `CrackSectionGeometry.MaximumSpacing`, con mappatura
   della disposizione delle barre già disponibile in `ConcreteLibraryMapping`.
5. `RebarMaterial` → `SteelMaterial` per proprietà e diagramma; verificare il
   mantenimento dei valori caratteristici assegnati e di γs prima di sostituire.
6. `ConcreteServiceabilityAdapter.Default` iniziale è `Legacy`: il ramo GPC
   esiste, ma l'equivalenza e le decisioni aperte F2.7 vanno controllate prima
   del cambio. La presenza del ramo non rende conforme il percorso predefinito.

## Parti senza sostituzione diretta dimostrata

- `BridgeConcept.*`: manca il contratto complessivo di progetto; F4.11 GPC.Design.
- `RetainingWall.Structures/Gravity/Reinforcement`: la geotecnica è in Checker,
  le verifiche strutturali e il progetto del muro non sono il medesimo contratto.
  Non sostituire una verifica con un'altra solo perché entrambe trattano c.a.; F4.7–F4.10.
- `ConcreteReinforcementDesign.*` e `SectionShearGeometry`: ricerca delle armature
  e proposta di bw/d/Asl non sono coperte dalla sola verifica di una sezione;
  F4.12–F4.13. Il solutore resistente è già delegato.
- `BridgeSlabLayout`: la predalle modifica un riferimento geometrico delle barre,
  poi Checker riceve un'unica soletta omogenea. Non esiste una verifica autonoma
  della predalle. Serve formalizzare questo contratto, non scartare gli input.
- `SezioneCA`: rimangono geometrie, disposizione delle barre e infrastruttura
  legacy; rimozione integrale soltanto dopo aver coperto i chiamanti e le API mancanti.
- `RetainingWallWorkspace.Results`: persino la didascalia As calcola l'area delle
  barre in WPF; deve riceverla dal risultato, senza introdurre una nuova formula.

`ANTHEA.ModelWorkspace.ModelSnapshot` conserva nodi, elementi, proprietà, risultati
e terne importati in un pacchetto di presentazione. È un contratto locale, mentre
Model possiede già `ModelView` e `ModelImage`; non è però un secondo solutore e
la separazione è documentata nelle decisioni del 9 ottobre. L'eventuale convergenza
dei contratti deve mantenere provenienza, unità e componenti mancanti, senza
ricostruire risultati non importati. Lo stesso criterio vale per i DTO dei report.

Sei file geotecnici legacy sono già esclusi da Compile in
`X.Calculations.csproj`: BishopSolver, FoundationSettlement, NewmarkSliding,
ShallowFoundationSeismic, SlopeGeometry e SlopeStability. Non sono motori attivi;
la loro presenza su disco va distinta dalle classi di contratto ancora compilate.

## Stato dell'intervento

Cattura iniziale e sostituzioni in corso. Nessuna modifica a versioni o snapshot
di libreria; restano validi i prerequisiti del rilascio delle fondazioni.
Gli esiti finali, le differenze misurate e le parti rimaste saranno registrati
qui e nel registro delle differenze dopo il confronto.
