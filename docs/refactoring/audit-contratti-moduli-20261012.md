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
- `RetainingWallWorkspace.Results`: la didascalia As inizialmente ricalcolava
  l'area delle barre in WPF; ora usa `SectionCircular.Area` di Model.

`ANTHEA.ModelWorkspace.ModelSnapshot` conserva nodi, elementi, proprietà, risultati
e terne importati in un pacchetto di presentazione. È un contratto locale, mentre
Model possiede già `ModelView` e `ModelImage`; non è però un secondo solutore e
la separazione è documentata nelle decisioni del 9 ottobre. L'eventuale convergenza
dei contratti deve mantenere provenienza, unità e componenti mancanti, senza
ricostruire risultati non importati. Lo stesso criterio vale per i DTO dei report.

All'inizio dell'attività sei file geotecnici legacy erano esclusi da Compile in
`X.Calculations.csproj`: BishopSolver, FoundationSettlement, NewmarkSliding,
ShallowFoundationSeismic, SlopeGeometry e SlopeStability. Non sono motori attivi;
la loro presenza su disco era distinta dalle classi di contratto ancora compilate.

## Esito del riuso delle API

Le API distribuite sono state collegate senza aggiornare `lib/Checker` o le versioni:
- aderenza, tetto C60 e ancoraggi: `AnchorageCalculator.Bond/BondStrength/BondFctk05/Calculate`;
- dettagli delle sezioni non circolari: `MemberDetailingCalculator`, compresi solette e pareti;
- curve M–χ: `MomentCurvatureAnalysis`, con conversioni delle sole unità e traduzioni dei messaggi;
- interassi non circolari, ritaglio e regioni efficaci: `CrackSectionGeometry`;
- proprietà resistenti, deformazioni e diagrammi dell'acciaio: `SteelMaterial`;
- limiti tensionali e fessurazione non circolare: percorso GPC dell'adattatore SLE;
- didascalia dell'area della barra nei muri: proprietà `SectionCircular.Area`.

Interfacce e DTO di presentazione sono conservati: le traduzioni italiane non sono
formule di calcolo. Le nuove chiamate reali sono controllate anche sul codice IL.
La sonda di sessione verifica il percorso effettivo con scelta automatica o esplicita
del motore. L'input dei Progetti continua a usare gli stessi campi condivisi.

I sei vecchi motori geotecnici, già esclusi dalla compilazione, sono stati cancellati
su consenso esplicito dell'utente del 12 ottobre. Corrispondenze in
`GPCChecker.Geotechnics`: `Foundations.FoundationSettlement`,
`Foundations.ShallowFoundationSeismic`, `Seismic.NewmarkSliding`,
`Slopes.BishopSolver`, `Slopes.SlopeGeometry`, `Slopes.SlopeStability`.
Conservati gli adattatori e i contratti ancora usati. Prova iniziale: zero occorrenze
dei sei file negli elementi Compile; SHA-256 in `legacy-excluded-proof.json`.
Inventario finale: 9 progetti, 394 sorgenti compilati (testi di mappatura e ramo legacy conservato);
i sei file eliminati non erano fra i 392 iniziali.

## Eccezione accertata: interassi circolari (API-02)

Il confronto esteso impedisce una sostituzione completa. Con D=400 mm,
8Ø16 sulla corona esterna, copriferro 39,999995 mm, staffa Ø8 e 6Ø12 sulla
corona interna con interferro radiale 20 mm, i raggi nominali sono
144,000005 e 110,000005 mm. Il massimo arco fra barre adiacenti è
115,19173586761349 mm (circonferenza divisa per il numero delle barre).
Il legacy restituisce 452,3893578248936 mm, la DLL 904,778715649787 mm.

Entrambi raggruppano i raggi arrotondati a cinque decimali: piccole differenze
tra `double.Hypot` e `sqrt(x*x+y*y)` separano una corona in gruppi diversi.
Non è rumore numerico da ammettere con una tolleranza. Nei quattro casi della
cattura il verdetto rimane negativo, ma il valore è sbagliato in entrambe le versioni.

La migrazione di interassi, dettagli e fessurazione delle sezioni circolari
è sospesa: il percorso automatico conserva i risultati precedenti, anche nella
sessione usata da UI/Progetti. `LegacyCircularDetailing` e il ramo circolare
di `TensionBarSpacing` sono quindi duplicati **deliberatamente rimasti**.
La selezione esplicita del motore rimane disponibile alle prove di migrazione.
Serve correggere il raggruppamento in Checker, con regressioni indipendenti,
poi produrre e installare il set coerente prima di rimuovere queste copie.
La conservazione del legacy non certifica il suo risultato.

## Residui che non si possono dichiarare risolti

Restano i contratti incompleti elencati nella mappa iniziale: progetto strutturale
e distinta dei muri, ricerca armature, Bridge Design/GPC.Design, geometria
parametrica `SezioneCA`, riferimento predalle e dati di terreno riempitivi.
I valori γ=18/20 non sono stati trasformati in proprietà reali né nascosti.

I rami espliciti legacy di taglio/torsione, SLE e durabilità sono ancora
richiamati dalle prove di confronto: le rispettive API esistono, dunque non
sono «API mancanti». La loro separazione dai progetti di produzione è un
successivo passo di equivalenza; il percorso ordinario usa già le librerie,
salvo l'eccezione circolare dichiarata. `Durability.Strength/EffectiveWater`
e `NtcCover.DefaultCmin` restano riferimenti delle prove senza chiamanti
di produzione individuati. `RebarMaterial.Values.Ratio` mantiene il solo
rapporto fu/fy di presentazione. Il costruttore dei materiali caratteristici
in `SezioneCA` e alcune regole dei muri non hanno una facciata completa
equivalente nella DLL attuale.

Le guide Rev34 e la Wiki delle fondazioni restano quelle già collaudate:
questo intervento mantiene le formule e l'uso dei fogli. Il rilascio del nuovo
foglio è ancora vincolato alle altre versioni DLL che l'utente ha riservato
a una successiva assegnazione. Nessuna sostituzione parziale delle librerie.

## Verifiche finali

- Intera soluzione `ANTHEA.sln` e applicazione con dipendenze Release compilate:
  zero errori. La build diretta conserva il solo avviso CS0618 preesistente sul
  lock `CompositeSolverSynchronization`. La soluzione storica omette X.Materiali
  e lo compila in Debug: l'ultima build diretta di X.Desktop riallinea l'output
  distribuito a Release, come nel runner.
- Runner `build/ci.ps1 -Profile baseline`, tag `riuso-api-conclusivo`:
  **52 PASS, 1 KNOWN, 0 NEW-FAIL**, uscita 0; albero dei sorgenti invariato durante la corsa.
  Comprende standard, Wiki, cattura completa e i due confronti densi.
- Il KNOWN è quello già presente in `build/known-failures.json`:
  identificativo casuale differente nei default di progetto/foglio del muro.
- Nuova suite `ANTHEA.ApiReuse.Checks`: 266 casi congelati prima della modifica,
  3268 numeri, due soli arrotondamenti (massimo relativo 1,14×10⁻¹⁶);
  due ancoraggi con attesi a mano, chiamate GPC nel codice IL, quattro percorsi
  di sessione e regressione esplicita API-02.
- Confronto denso: 27 file, 50.996 righe, 9663 differenze numeriche entro tolleranza
  in 587 righe; nessun cambiamento non ammesso. Comprende 24 curve M–χ di
  produzione, casi di interruzione/rifiuto, solette/pareti e 77 fogli completi.
- Cattura completa: 548 file, 1421 arrotondamenti numerici (massimo relativo
  1,1436×10⁻¹⁵), 712 differenze ammesse dal formato decimale e 21 tempi volatili
  già esclusi. Input, archivi, messaggi, esiti e registro dei ripieghi inalterati.
- Tolleranza delle sole grandezze mutate: relativa 10⁻¹², assoluta zero,
  senza max(1,|x|). Registrata prima del commit di implementazione nei commit
  `1b3c38f` e `d69b97d`. Gli attesi indipendenti e `Fixtures/before.json`
  non sono stati sovrascritti.
- Le prove WPF complete non sono state rieseguite: nessun aggiornamento di DLL
  e nessuna modifica alla disposizione delle viste; l'unica variazione WPF
  delega la proprietà d'area della didascalia a Model.
- Tre suite che compilano i sorgenti esterni delle librerie risultano non eseguite
  dal runner, come già previsto dal registro delle suite; non sono conteggiate
  fra le verifiche superate.

Evidenze: `supporto/artefatti/riuso-api-20261012/conclusivo/`,
`confronto-headless-finale.json`, `api02-geometria-indipendente.json`,
`inventario-dopo/inventory.json` e `build-soluzione-release.log`.
Le prime corse di preparazione e quelle interrotte non sono le attestazioni finali:
sono conservate, incluse la limitazione di accesso NuGet nel sandbox e la discrepanza
circolare trovata prima di restringere la migrazione.
