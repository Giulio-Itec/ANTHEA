# Combinazioni di carico generate da Model — censimento e proposta

Stato del 16/10/2026. Documento di sola lettura: nessun codice è cambiato. Ramo `refactoring/combinazioni-model`,
da `main` `8b9ce6d3`. Librerie lette sui master del rilascio Z: Model `bf8563f5` (GPCModel 5.5.0.0) e Checker
`bce7aaae` (GPCChecker.Geotechnics 0.4.0.0), gli stessi del set installato in `lib/Checker` (`release-set.json`).

Convenzioni dei riferimenti:
- i percorsi senza prefisso sono di ANTHEA;
- `Model/…` e `Checker/…` sono i repository fratelli in `Documents/Repository`;
- `:n` è la riga, `:n-m` un intervallo.

## Richiesta

Nella notte fra il 15 e il 16/10, alla domanda se le combinazioni dei muri si fanno con il generatore di Model (no: i
muri usano `WallCombinations`, porta del preset legacy), l'utente ha risposto: «sì, mettilo in coda dopo il rilascio.
vorrei però che le combo venissero fatte tutte in model.» La voce è in [piano.md](piano.md) (coda dopo il rilascio
delle correzioni), in [decisioni.md](decisioni.md) (16 ottobre, notte) e nel registro, campo `decisione_combinazioni`
di BUG-MUR-1.

Questo documento fa tre cose:
1. censisce i punti di ANTHEA e delle librerie Checker che costruiscono combinazioni o applicano da sé coefficienti
   parziali e ψ, e i moduli dove l'utente inserisce le combinazioni a mano;
2. descrive che cosa sa fare oggi il generatore di Model e che cosa gli manca;
3. propone ordine di migrazione, aggiunte all'API di Model (descritte, non scritte), prova dell'equivalenza, rischi e
   domande per l'utente.

Tre categorie, usate in tutto il documento:
- **genera**: produce righe di combinazione (coefficienti per azione) a partire da azioni caratteristiche;
- **applica**: moltiplica per γ o ψ un'azione o un parametro, senza produrre righe;
- **consuma**: riceve azioni di progetto già combinate (dall'utente, da un modello, da un'altra libreria).

## Sintesi

- **Nessun codice di produzione usa il generatore di Model.** `StandardEN1990.CreateCombinations` ha solo chiamanti
  di prova (38 test di Model). `StandardGeotechnical.Combinations` è usato in Checker per M e γR di pali, fondazioni
  superficiali e pendii, mai per costruire righe.
- **Chi genera righe da sé:**
  - muri, verifiche locali: `WallCombinations.Generate` (Checker), fino a 4096 righe, con gemelle MUR-1;
  - muri, stabilità globale: `WallGlobalStability.Combinations` (Checker), A2+M2+R2 ricavato dalle righe A1;
  - muri versione 1 senza interfaccia: `LegacyCombinations` (ANTHEA, l'unico generatore dentro ANTHEA);
  - Bridge Design: una combinazione γG·G + γQ·Q in GPC.Design.
- **Chi applica γ da sé, senza righe:** Viviani in CompositeBridge (tre moltiplicatori dell'utente) e il peso dei pali
  verticali (γG letto da Model).
- **Tutti gli altri moduli consumano** azioni già combinate, spesso inserite dall'utente (sezione c.a. ed Excel,
  sezione mista, pali, fondazioni superficiali, Modelli).
- **Model oggi non basta per i muri.** `CreateCombinations` ha i valori EN 1990, un solo γG, ψ per tipo di caso,
  generazione lineare e un difetto nella SLE caratteristica. `StandardNTC2018Geotechnics` ha gli insiemi NTC giusti,
  ma restituisce coefficienti parziali, non righe. Le lacune sono dodici (§2.4).
- **Due valori dei muri differiscono da Model:** G2 favorevole 0 invece di 0,8; γR statici invece di quelli sismici
  nelle righe con kh assegnato.
- **Proposta:** un generatore combinatorio nuovo in Model, con un profilo NTC 2018. Poi, nell'ordine: muri bit per bit,
  stabilità globale, ANTHEA, le decisioni normative una per volta, Bridge Design e il generatore dei Modelli con piani
  propri. Undici domande per l'utente, con le raccomandazioni (§4).

## 1. Censimento

### 1.1 Muri: il generatore da migrare per primo

#### Libreria: `WallCombinations.Generate`

`Checker/GPCChecker.Geotechnics/Walls/WallCombinations.cs:24-101`, portato da ANTHEA nella fase W1 (`4a4c90c4`; preset
`RetainingWall.GenerateCombinations`, commit ANTHEA `fe4652c`), poi corretto con MUR-1 (`cec70517`, `edc6ac35`) e MUR-2
(`2836af62`). Genera, applica e assegna anche i γR: tutto in un solo posto, senza Model.

Ingresso (`WallModels.cs`):
- `WallAction` (:90-114): natura G1, G2, Q o A (:83); tipo sovraccarico uniforme, forza orizzontale o verticale,
  momento, pressione laterale, urto (:80); ψ0, ψ1, ψ2 propri di ogni azione (predefiniti 0,7/0,5/0,3, :105-106);
  gruppo delle azioni correlate (`Key`, :113); abilitazione.
- Dal `WallInput` il generatore legge anche la presenza della valle (`Valley.Height > 0`), della falda e della mensola
  di monte (`Geometry.Heel > 0`) e le opzioni sismiche (`WallSeismic`, :204-239).

Righe generate, nell'ordine:

| Famiglia | Righe | Coefficienti | γR (scorr./ribalt./portanza/passiva) | Righe di codice |
|---|---|---|---|---|
| SLE caratteristica «SLE n» | una per gruppo variabile principale (una sola se non ci sono variabili) | G1, G2 = 1; principale 1; altre ψ0; A = 0 | 1 / 1 / 1 / 1 | :58-63 |
| SLE frequente «SLE_FREQ n» | come sopra | principale ψ1; altre ψ2 | 1 / 1 / 1 / 1 | :58-63 |
| Quasi permanente | una | variabili ψ2 | 1 / 1 / 1 / 1 | :64 |
| SLU «SLU n», A1+M1+R3 | ogni combinazione di: muro 1/1,3; terreno 1/1,3 (peso e spinta, sorgente unica); acqua 1/1,3 se c'è falda; ogni gruppo G1 1/1,3; ogni gruppo G2 0/1,5; ogni sottoinsieme dei gruppi variabili e ogni principale (1,5, le altre 1,5·ψ0); valle 1/1,3 se c'è terreno a valle | come a lato | 1,1 / 1,15 / 1,4 / 1,4 | :65-81, :35, :43, :46 |
| Sisma assegnato «Sisma kv−», «Sisma kv+» | ±kv, kh assegnato, «Sismica · M1/R3» | quasi permanente | 1,1 / 1,15 / 1,4 / 1 (passiva mai contata) | :85-86, :43 |
| SLV dal sito «SLV Generale kv±», «SLV Ribaltamento kv±» | due destinazioni, βm e βm,rib, ±kv | quasi permanente | 1 / 1 / 1,2 / 1 | :88-93, :43 |
| Eccezionale «Eccezionale ‹nome›» | una per gruppo di azioni A | quasi permanente + A = 1 | 1 / 1 / 1 / 1 | :95-99 |
| Gemelle «…b» (MUR-1) | dopo ogni riga SLU, sismica o eccezionale con un sovraccarico uniforme Q a fattore > 0, su muro con mensola | stessi coefficienti, `VariableSurchargeOnHeel = false`: q resta nella spinta e manca sulla mensola | come la principale | :48-55 |

Altre regole:
- doppioni eliminati con un'impronta dei fattori in formato `R` (:38-39);
- al più 4096 righe, con errore esplicito (:53) e controllo preventivo dei bit (≤ 12) e del prodotto per il numero di
  variabili (≤ 4090, :65-66);
- sisma dal sito: kh ≤ 0,4 e |kv| ≤ 0,2, altrimenti errore senza troncare (:91);
- etichetta dell'approccio nella riga (:41-42): «A1+M1+R3», «Sismica · M1/R3», «SLV · NTC 7.11.III», «Eccezionale ·
  M1/R=1», «SLE».

Uscita: `WallCombination` (`WallModels.cs:132-176`), con nome, stato (`WallLimitState`, :117), approccio, fattori del
muro, del terreno, della valle e dell'acqua, γM di tan φ, γR di scorrimento, ribaltamento e portanza, kh, kv,
destinazione sismica, coefficiente di ogni azione per id, `VariableSurchargeOnHeel` (:153) e `PassiveFactor` (:158).
La consuma `RetainingWallAnalysis.Calculate` (il flag a :99, γR,e a :133).

Differenze rispetto ai valori di Model (§2.3), da decidere prima o dopo la migrazione:

| Punto | `WallCombinations` | Model, `StandardNTC2018Geotechnics` |
|---|---|---|
| G2 favorevole in A1 | 0 (:73), valore di NTC 2008 | 0,8 (:27), NTC 2018 Tab. 6.2.I |
| γR delle righe sismiche con kh assegnato | 1,1 / 1,15 / 1,4, come le statiche (:43) | 1,0 / 1,0 / 1,2 (:40-41), Tab. 7.11.III |
| passiva nel sisma | mai contata (`RetainingWallAnalysis.cs:133`) | γR,e sismico 1,2 definito (:41) |
| eccezionale | γR = 1 | situazione eccezionale assente |
| fattori di muro, terreno, acqua, valle | canali propri 1/1,3 | un solo γG1 per insieme, senza canali |

#### Libreria: `WallGlobalStability.Combinations`

`Checker/GPCChecker.Geotechnics/Walls/WallGlobalStability.cs:73-102`. Genera le righe della stabilità globale dalle
ordinarie di `WallCombinations.Generate` senza sisma:
- da ogni SLU, A2+M2+R2: G1 = 1, le altre azioni × 1,3/1,5 (G2 da 1,5 a 1,3, Q da 1,5 a 1,3; G2 favorevole resta 0),
  γM tan φ′ = γM c′ = 1,25, γM cu = 1,4, γR = 1,1 (:89-94);
- da ogni eccezionale, fattori unitari (:94);
- con il sisma, la quasi permanente con ±kv e γR = 1,2 (:96-100);
- doppioni eliminati sui coefficienti, quindi le gemelle «b» (stessi coefficienti) non producono righe globali; al più
  256 righe (:80-85).

I valori coincidono con A2, M2 e R2 di Model (`StandardNTC2018Geotechnics.cs:28, 32, 43-44`), salvo G2 favorevole, ma
sono scritti a mano. `SlopeFactors.FromCombination` (`Checker/GPCChecker.Geotechnics/Slopes/SlopeModels.cs:135-145`),
che li legge da una `GeotechnicalCombination`, non è usato.

#### ANTHEA: adattatore, documenti, matrici

| Dove | Che cosa | Categoria |
|---|---|---|
| `X.Calculations/RetainingWall.Actions.cs:46-52` | `GenerateCombinations`: chiama `WallCombinations.Generate` e converte ogni riga con `ToRow` | adattatore |
| `X.Calculations/RetainingWall.Library.cs:178-187` | `ToRow`: riga del documento | adattatore |
| `X.Calculations/RetainingWall.Library.cs:150-164` | `ToCombination`: riga del documento → `WallCombination`; righe salvate prima del 16/10 senza `heel_surcharge` (vero) e `rpassive` (`DefaultPassiveFactor`: 1,4 SLU, 1 altrove, :169-171) | consuma |
| `X.Calculations/RetainingWall.Library.cs:196-214` | `LegacyCombinations`: casi fissi dei documenti versione 1 (3 SLE con ψ1 e ψ2 dei carichi, 8 o 16 SLU con muro, terreno, q 0/1,5 e acqua, sisma ±kv con ψ2, gemelle MUR-1) | **genera, in ANTHEA** |
| `X.Calculations/RetainingWall.Equilibrium.cs:25-29` | calcolo: «Automatiche» rigenera le righe a ogni calcolo; «Personalizzate» usa le righe salvate; versione 1 usa `LegacyCombinations` | instradamento |
| `X.Calculations/RetainingWall.Equilibrium.cs:38, 70` | nota «Matrice personalizzata» con le gemelle mancanti (`MissingHeelTwins`) | controllo |
| `X.Calculations/RetainingWall.Actions.cs:53-98` | `ValidateActions`: azioni (al più 30, ψ2 ≤ ψ1 ≤ ψ0, gruppi coerenti) e righe personalizzate (nome unico, stato, fattori 0–5 e γ 0,1–5, kh ≤ 0,4, |kv| ≤ 0,2 solo in SISMA, A solo in ECCEZIONALE, al più 4096 righe, firma) | controllo |
| `X.Calculations/RetainingWall.Actions.cs:40-41` | `CombinationSignature`: firma di azioni, ψ, gruppi e sisma dal sito | controllo |
| `X.Calculations/RetainingWall.GlobalStability.cs:88-102` | righe globali da `WallGlobalStability.Combinations` | adattatore |
| `X.Calculations/RetainingWall.GlobalStability.cs:140-155` | righe globali personalizzate: firma, stati SLU/SISMA/ECCEZIONALE, campi 0,1–5 | controllo |
| `X.Calculations/RetainingWall.Actions.cs:8` | `ApproachHelp`: testo dell'approccio 2 e dei γR | testo |
| `src/ANTHEA.Application/Walls/WallModel.cs:40-52` | facciata verso il motore (`GenerateCombinations`, `GenerateGlobalCombinations`, `HeelSurcharge`, `DefaultPassiveFactor`) | facciata |
| `X.Desktop/Wpf/RetainingWallWorkspace.Results.cs:14` | «Genera combinazioni»: scrive le righe, modo «Automatiche», firma | interfaccia |
| `X.Desktop/Wpf/RetainingWallWorkspace.GlobalStability.cs:70-78` | «Genera globali», «Conferma matrice globale» (modo «Personalizzate») | interfaccia |

Formato persistito (documento del muro, versione 2):
- `actions[]`: `id`, `name`, `type`, `category` (G1, G2, Q, A), `enabled`, `visible`, `value`, `z`, `z0`, `x`, `psi0`,
  `psi1`, `psi2`, `group`;
- `combination_mode`: «Automatiche» o «Personalizzate»; `combination_signature`;
- `combinations[]`: `enabled`, `name`, `state` (SLU, SLE, SLE_FREQ, SLE_QP, SISMA, ECCEZIONALE;
  `RetainingWall.States`, `RetainingWall.Actions.cs:12`), `approach`, `wall`, `soil`, `valley_soil`, `water`, `mphi`,
  `rslide`, `rover`, `rbearing`, `rpassive`, `heel_surcharge`, `kh`, `kv`, `purpose` (Generale, Ribaltamento),
  `coefficients{id azione: fattore}`;
- `global_stability.combination_mode`, `global_stability.combination_signature`, `global_stability.combinations[]`:
  `enabled`, `name`, `state`, `soil`, `wall`, `mphi`, `mc`, `mcu`, `r`, `kh`, `kv`, `coefficients`;
- versione 1: `loads.surcharge`, `loads.horizontal`, `loads.vertical`, `loads.psi1`, `loads.psi2`; `Upgrade` li porta
  in tre azioni del gruppo «legacy» (`RetainingWall.Actions.cs:19-38`).

Con il modo «Automatiche» le righe salvate non fanno fede: il calcolo le rigenera. Un cambio del generatore cambia quindi
subito i risultati dei documenti automatici, mentre quelli «Personalizzati» conservano le righe salvate. Nelle catture
congelate della geotecnica i modi sono 217 «Automatiche» e 3 «Personalizzate» (`walls-combinations.jsonl`), 209 e 3
(`walls-documents.jsonl.gz`); i quattro ingressi F3 dei muri sono tutti «Automatiche».

Prove che fissano il comportamento:
- Checker, `GPCChecker.Test.Geotechnics`:
  - `WallsMigrationTests.LegacyCombinationsAreReproduced` (:105-150): 110 documenti della cattura
    `Fixtures/walls-combinations.jsonl` (2524 righe legacy; il README di Geotechnics dichiara 2491 righe confrontate),
    nome, stato, approccio, destinazione, fattori e coefficienti con tolleranza 1e-14 relativa; gemelle contate a parte
    (:121-125);
  - `WallsServiceGlobalMigrationTests.LegacyGlobalCombinationsAreReproduced` (:83-100): 388 righe globali della stessa
    cattura;
  - `WallsMigrationTests.LegacyWallsAreReproduced` (:153): 85 muri completi di `walls-documents.jsonl.gz` (non usa
    `Generate` per i documenti calcolati: righe salvate o ricostruite da `WallsFixture.Combinations`);
  - `WallEdgeCaseTests`: `CombinationsFollowTheNtcPreset` (:318, 15 righe con un q, 20 SLU e 16 gemelle con due Q,
    G2 0/1,5, gruppi, eccezionali, sisma assegnato e dal sito, limiti), `SeismicAndAccidentalRowsHaveTheTwinWithout…`
    (:184), `PassiveInFrontIsADesignResistance` (:226), `VariableSurchargeIsAbsentFromTheHeelInTheTwinRows` (:392); gli
    attesi delle gemelle vengono da modelli Python indipendenti (`mur1_heel.py`, `mur1b_seismic.py`);
  - `WallExamplesTests`: cinque esempi (:24, :51, :73, :95, :130; 15, 27, 27 righe nei primi tre);
  - `WallServiceGlobalEdgeTests.GlobalCombinationsAreA2M2R2AndSlv` (:102).
- ANTHEA:
  - `supporto/test/RetainingWall.Checks`: `ActionsChecks.cs:17-49` (20 SLU, 16 gemelle, eccezionali),
    `LibraryAdapterChecks.cs:40-110` (righe del documento = `WallCombinations.Generate`, γR,e, versione 1, gemelle
    sismiche), `Program.cs:36-44` (8 SLU, 4 gemelle, 3 SLE);
  - `supporto/test/GlobalStability.Checks/Program.cs:78-100`;
  - `tests/ANTHEA.Application.Tests/WallParameterChecks.cs:16`;
  - `supporto/test/Desktop/GlobalGuidanceChecks.cs:83-84`, `GlobalStabilitySmokeChecks.cs:35-40`;
  - confronti congelati F4: `tests/ConcreteLibraryAdapter.Checks/F4WallFixCategories.cs` (categorie MUR-1 e MUR-2:
    104 gemelle in f4-structural-adapter, 24 in f4-gravity-adapter);
  - confronto UI F3: `tests/fixtures/f3/transitions-f4.json`, 22126 transizioni ammesse. 15907 citano BUG-MUR-1
    (11631 da sola, 4276 insieme ad altre voci); delle 16594 che citano una voce MUR, 12221 stanno nei quattro fogli dei
    muri e 4369 nel progetto.

Due dettagli dell'adattatore:
- i documenti versione 1 passano alla libreria come tre azioni Q del gruppo «legacy» con ψ = 1
  (`X.Calculations/RetainingWall.Library.cs:115-121`); `LegacyCombinations` serve solo al calcolo senza interfaccia
  (`src/ANTHEA.Application/Modules/CalculationService.cs:29` chiama `RetainingWall.Calculate` sul documento così com'è),
  perché l'interfaccia aggiorna prima il documento (`src/ANTHEA.Application/Presentation/WallSheetPresentation.cs:58`,
  `X.Desktop/Wpf/RetainingWallWorkspace.Inputs.cs:26`);
- l'esportazione di una sezione del muro verso il foglio Sezione c.a. mette le righe SISMA ed ECCEZIONALE nell'insieme
  SLU, non SLV (`X.Calculations/RetainingWall.SectionExport.cs:22-27`). Non genera combinazioni, ma è un punto da tenere
  presente quando le righe avranno uno stato limite tipizzato da Model.

### 1.2 Altri moduli di ANTHEA

In `X.Calculations`, `src` e `X.Desktop` nessun codice di produzione chiama `CreateCombinations` o
`StandardGeotechnical.Combinations` direttamente. Fuori dai muri, nessun modulo di ANTHEA genera combinazioni: o le
riceve già fatte dall'utente, o una libreria applica i coefficienti.

| Modulo | Dove | Che cosa | Categoria | Chiavi persistite | Prove |
|---|---|---|---|---|---|
| Bridge Design (`str_bridge_design`) | `X.Calculations/BridgeConcept.Traffic.cs:16-28`; `BridgeConcept.Library.cs:137-138, 246-247`; libreria `Checker/src/GPC.Design/Bridges` | una combinazione fondamentale γG·G + γQ·Q dentro GPC.Design; γQ = 1,35 (`RoadTraffic.NtcUnfavourableGamma`) per lo Schema 1 dei documenti nuovi, 1,5 negli archivi; γG = 1,35 anche su G2 (registro D7-f, componente f3 non attuata) | applica (in libreria) | `assumptions.gamma_g`, `assumptions.gamma_q`, `assumptions.traffic_load`, `input.traffic_model`, `traffic_migration` | `tests/ANTHEA.Application.Tests/BridgeTrafficChecks.cs:18-25`; `tests/Desktop/BridgeTrafficSmokeChecks.cs:26-39`; F3 `str_bridge_design` |
| Sezione mista del ponte (`str_mista_ponte`) | `X.Calculations/BridgeSection.cs:35-41`; `BridgeSection.CheckerInput.cs:64`; testo in `X.Desktop/Wpf/BridgeActionsTable.cs:123-125` | azioni per fase inserite dall'utente, già combinate; lo stato (SLU, SLE rara, SLE quasi permanente) sceglie solo i limiti; `psi` delle fasi è il moltiplicatore ψL di EN 1994, non un ψ di combinazione | consuma | `fasi[]`: `nome`, `tipo`, `attiva`, `N`, `Mx`, `V`, `T`, `q_conn`, `epsilon_cs`, `modo`, `phi`, `psi`, `n`, `riferimento_N`; `stato` | `supporto/test/X.Verifiche/BridgeSectionChecks.cs`; `tests/ANTHEA.Application.Tests/BridgeContractChecks.cs`; F3 `str_mista_ponte` |
| Sezione c.a. (`str_palo`) | `X.Calculations/SectionWorkspace.cs:12, 28-29`; `src/ANTHEA.Application/Concrete/SectionActionInputs.cs:5, 34-59` | insiemi SLU, SLV, SLE (rara), SLE_FREQ, SLE_QP inseriti a mano; nessun coefficiente applicato | consuma (matrice dell'utente) | `combinazioni.{SLU,SLV,SLE,SLE_FREQ,SLE_QP}[]` = `id`, `nome`, `azioni` (3 o 6 componenti), `visible`, `elemento`; `workspace_ca.sollecitazioni_versione`; storico `workspace_ca.taglio.azioni[]` (`src/ANTHEA.Application/Concrete/ConcreteSheetPreparation.cs:100-125`) | `tests/ANTHEA.Application.Tests/SectionActionInputsChecks.cs:16-31`, `SectionActionsEditorChecks.cs:17-46`; F3 `str_palo` |
| Sollecitazioni da Excel (EXCEL-SOL) | `src/ANTHEA.Application/Documents/SectionActionsExcel.cs:15-16, 85-90, 120`; modello `src/ANTHEA.Application/Templates/ANTHEA_Sollecitazioni.xlsx` | colonne Elemento, Nome combo, Stato limite, N, Vx, Vy, T, Mx, My; solo valori, al più 10000 righe | consuma (matrice dell'utente) | come la sezione c.a. | `SectionActionsEditorChecks.cs:17-46`; `supporto/test/Desktop/ConcreteSmokeChecks.cs:219, 261` |
| Pali e micropali verticali | `X.Calculations/Calcolo.cs:308-313`; predefiniti `X.Calculations/CalculationDefaults.cs:7`; `X.Calculations/CalculationCoefficients.cs:73-81` | azioni di progetto dell'utente (`azione_compressione`, `azione_trazione`); il solo peso del palo è moltiplicato per γG sfav./fav., letti da Model (`PileResistanceFactors.FromStandard` con `StandardNTC2018Geotechnics`) salvo valori del documento; ripieghi 1,30/1,00 scritti anche nei predefiniti e in `src/ANTHEA.Reports/Generators/ReportWord.cs:65` | applica (γG del peso, da Model) | `generali.peso_palo_sfavorevole`, `generali.peso_palo_favorevole`, `generali.sicurezza_*` | `tests/ANTHEA.Application.Tests/F4PileCoefficientChecks.cs:30-45`; `supporto/test/X.Verifiche/PileFactorChecks.cs:87-134` |
| Pali e micropali orizzontali, palo elastico | `X.Calculations/PaloOrizzontale.cs:222-263, 250`; `MicropaloOrizzontale.cs:56-61`; `ElasticHorizontalPile.Library.Design.cs:16`; `ElasticHorizontalPile.Reinforcement.cs:221` | una sola azione di progetto (HEd, N, e); ξ e γR da Model (`LateralPileFactors.FromStandard`); il palo elastico chiede la conferma `elastico.dettagli.azioni_progetto` ed esclude le combinazioni sismiche automatiche; il valore 1,30 è ripetuto come testo in `src/ANTHEA.Application/Presentation/HorizontalPreview.cs:34` | consuma | `generali.azione_orizzontale`, `xi3`, `xi4`, `gamma_r` | `tests/ANTHEA.Testing/Capture/F4Capture.cs:166, 208`; `supporto/test/X.Verifiche/HorizontalChecks.cs` |
| Fondazioni superficiali | `src/ANTHEA.FoundationWorkspace/FoundationDocument.cs:16, 29-31, 38, 92-93`; `FoundationPresentation.cs:50, 77` | una combinazione dell'utente (N, Hx, Hy, Mx, My) già combinata, con conferma esplicita per il profilo NTC 2018 A1+M1+R3; M1 e γR da Model in libreria | consuma | `nome_combinazione` («SLU · A1»), `azioni_confermate` | `tests/FoundationWorkspace.Checks/Program.cs:24`; F3 `geo_fondazioni_superficiali` |
| Modelli (MOD-30) | `src/ANTHEA.ModelWorkspace/ModelCombinations.cs:23-24, 99-125, 141`; `GpcModelCombinationService.cs:37-57`; `src/ANTHEA.ModelViewer.Presentation/ModelViewerViewModel.Combinations.cs:145-152` | combinazioni importate da MIDAS (LCOM-GEN, -CONC, -STEEL, -SRC, -STLCOMP, -SEISMIC) o create a mano (lineari o inviluppi, coefficienti liberi anche negativi); somma e inviluppi nel motore GPC (`CombinationExpansion`, `CombinationRebuild`), al più 4096 alternative; nessun generatore automatico nel codice (solo la voce del piano, `piano.md:689`) | consuma (matrice dell'utente o importata) | `CustomCombinations[]` (`Id`, `Name`, `Kind`, `Source`, `Status`, `Terms[]` con `Reference`, `IsCombination`, `Factor`, `Analysis`, `Editable`), `ExcludedCombinationIds[]`, `Results[].Combination` (`src/ANTHEA.ModelWorkspace/ModelSnapshot.cs:29, 49, 51`) | `tests/ModelWorkspace.Checks/CombinationChecks.cs:39-157`; `tests/ModelViewer.UiChecks/CombinationRenderingChecks.cs` |

Senza carichi né combinazioni: efficienza dei gruppi di pali (`X.Calculations/HorizontalPileGroup.cs`), stratigrafia.
Non esistono in ANTHEA un modulo dei plinti né un modulo di stabilità dei pendii separato dai muri.

`X.Calculations/CalculationCoefficients.cs` non contiene coefficienti di combinazione (nessun γG/γQ dei carichi, nessun
ψ): elenca coefficienti di resistenza e di materiale e il loro percorso nei documenti; l'unico γG è quello del peso dei
pali verticali (:73-81). `Standard()` (:25-30) fissa «NTC 2018» per tutti i moduli tranne sezione c.a., sezione mista,
gruppo di pali e acciaio d'armatura.

I report ripetono i coefficienti senza calcolarli: `src/ANTHEA.Reports/Generators/ReportRetainingWall.cs:56, 103-108`
(ψ, fattori e nota delle gemelle), `ReportRetainingWall.GlobalStability.cs:34`, `ReportWord.cs:65`,
`ReportOrizzontale.cs:107, 136`, `ReportConcrete.cs:132-138`.

### 1.3 Altre librerie Checker

In questa sezione i percorsi sono relativi a `Checker/`; dopo il primo percorso di una cella, i nomi senza cartella
stanno nella stessa cartella del precedente.

In Checker nessuno chiama `CreateCombinations`. `StandardGeotechnical.Combinations` è chiamato in produzione da
`GeotechnicalActions.Required` (pali), da `ShallowFoundationBearing` e, tramite una combinazione passata dal chiamante,
da `SlopeFactors.FromCombination`: mai dai muri. Fuori dai muri genera righe solo GPC.Design (una combinazione).

| Progetto | Dove | Che cosa | Categoria | Prove |
|---|---|---|---|---|
| GPC.Design, Bridge Design | `src/GPC.Design/Bridges/BridgeConcept.cs:97-98` (γG 1,35, γQ 1,5, campo 1–2); `BridgeConcept.Traffic.cs:30-60`; `AdvancedCalculation.cs:50-89`; `Calculation.cs:106-107, 202` | passata di servizio 1/1 e passata fattorizzata γG·(G1 + G2) + γQ·Q con l'inviluppo del traffico; nessun ψ, nessuna parte favorevole; `RoadTraffic.NtcUnfavourableGamma = 1,35` (`RoadTraffic.cs:18`) è letto solo da ANTHEA | genera (una riga) e applica | `tests/Design.Checks/BridgeTrafficTests.cs:22-24, 46-60, 72`; `BridgeConceptTests.cs:60, 77` con `Fixtures/bridge-f4.json.gz` |
| CompositeBridge, metodo di Viviani | `GPCChecker.CompositeBridge/Viviani/VivianiModels.cs:55-62`; `VivianiMethod.cs:92-97, 343-344` | MEd = γG1·M1 + γG2·M2 + γQ·MQ (e TEd), tre scalari dell'utente (predefinito 1); `VivianiComparison.cs:94` li fissa a 1 | applica | `GPCChecker.Test.CompositeBridge/Viviani/VivianiEquivalenceTests.cs:26, 82, 129`; `VivianiHistoryTests.cs:34` (1,35/1,5/1,35) |
| CompositeBridge, sezioni e storico | `GPCChecker.CompositeBridge/Models/BridgeInputs.cs:8, 52`; `GPCChecker.CompositeBridge/HSections/HBridgeSection.Analysis.cs:16, 221-223`; fatica `HBridgeSection.Details.cs:208-215` | carichi già combinati (README: «I carichi sono già combinati»); `BridgeLimitState` sceglie solo i limiti; `PsiL` è il moltiplicatore di viscosità; fatica con γFf e γMf dell'utente | consuma | `GPCChecker.Test.BridgeAudit/BridgeLocalDetailsTests.cs` |
| Geotechnics, pali | `GPCChecker.Geotechnics/Piles/PileModels.cs:26-33, 61-73`; `AxialPileCapacity.cs:295-296` | ξ, γR e γG del peso da Model; compressione NEd + γG,sfav·W, trazione max(0, NEd,t − γG,fav·W′) | applica (γG del peso, da Model) | `GPCChecker.Test.Geotechnics/PileEdgeCaseTests.cs:221-222, 447-453` |
| Geotechnics, pendii | `GPCChecker.Geotechnics/Slopes/SlopeModels.cs:108-146`; `SlopeStability.cs:72` | righe di fattori del chiamante; `FromCombination` da una combinazione di Model | consuma | `GPCChecker.Test.Geotechnics/SlopeEdgeCaseTests.cs:231-249`; `Fixtures/geotechnics-slope.csv`, `geotechnics-bishop.csv` |
| Geotechnics, fondazioni superficiali | `GPCChecker.Geotechnics/Foundations/ShallowFoundationBearing.cs:13-17, 170-174`; `ShallowFoundationSeismic.cs:56-90` | risultanti di progetto già combinate; M1 e γR = 2,3 da Model | consuma | `tests/ShallowFoundation.Tests/BearingTests.cs:35-38` |
| Geotechnics, valori di progetto | `GPCChecker.Geotechnics/DesignValues.cs:10-25` (`DesignSoil`), `:33-43` (`GeotechnicalActions.Factor`), `:49-55` (`Required`) | M da Model; `Factor` sceglie γ fav./sfav. di un insieme di Model ma non ha chiamanti di produzione | applica (M da Model) | `GPCChecker.Test.Geotechnics/DesignValuesTests.cs:35-50` |
| Concrete | `GPCChecker.Concrete/Serviceability/StressLimitCheck.cs:12, 91-117`; `GPCChecker.Concrete/Cracking/CrackRequirements.cs:64-130`; `GPCChecker.Concrete/Walls/WallSectionSession.cs:180-196`; `GPCChecker.Concrete/Piles/PileReinforcement.cs:29-36` | `ServiceabilityCombination` (caratteristica, frequente, quasi permanente) è un'etichetta che sceglie il limite; azioni di progetto confermate | consuma | suite Concrete |
| Steel | `GPCChecker.Steel/Checkers/BeamChecker.cs:109-112`; `BoltStresses.cs:13-23` | verifica ogni `ILoadCase` dei risultati di Model; `CombCaseType` SLU/SLS dal chiamante | consuma | suite Steel |
| GPC.Design, muri e pali | `src/GPC.Design/Walls/GravityWallAnalysis.cs:52, 69-126`; `WallReinforcementDesign.cs:6, 16`; `src/GPC.Design/Piles/PileSegmentDesign.cs:16-30`; `src/GPC.Design/Sections/SectionDesignDemand.cs:4, 26-39` | righe di sollecitazioni del chiamante, etichettate per famiglia (SLU, SLV, taglio, SLE) | consuma | suite Design |
| ModelChecker | `ModelChecker/Execution/PlateChecking.cs:45, 80`; `ModelChecker/Adapters/Concrete/ConcreteSectionVerifier.*.cs` | combinazioni già calcolate da Model, scelte per `CombinationCategory` (`Model/Model/Checking/Contracts/SectionCheckSpecification.cs:7`) | consuma | `ModelChecker.Tests` |
| Masonry | `src/GPCChecker.Masonry/MasonryWallMaterial.cs:15-26` | solo γM e fattore di confidenza | — | — |

`src/Checker` è una cartella vuota.

Da tenere presente per i muri: `WallsFixture.Combinations` (`GPCChecker.Test.Geotechnics/WallsFixture.cs:181-191`)
ricostruisce le righe dei documenti versione 1 della cattura con γR scritti nel test (1,1/1,15/1,4; 1,2 in portanza
sismica). È una prova, ma ripete il preset una terza volta.

### 1.4 Combinazioni inserite dall'utente

| Modulo | Che cosa inserisce l'utente | Rapporto con un generatore |
|---|---|---|
| Muri | matrice «Personalizzate»: righe con fattori di corpo, γM, γR, kh, kv, flag della mensola e coefficienti per azione | parte dalle righe generate («Genera combinazioni», poi la modifica di una cella passa a «Personalizzate», `src/ANTHEA.Application/Presentation/WallParameterTable.cs:54-58`); la firma obbliga a rigenerare o confermare se cambiano azioni, ψ o sisma; la nota `MissingHeelTwins` segnala le gemelle mancanti |
| Muri, stabilità globale | matrice globale «Personalizzate» | come sopra, con «Conferma matrice globale» |
| Sezione c.a. ed EXCEL-SOL | elenco di righe N, Vx, Vy, T, Mx, My per stato limite, a mano o da Excel | nessun generatore: le azioni vengono da un modello o da un calcolo esterno |
| Sezione mista del ponte | azioni per fase, stato limite e flussi di fatica | nessun generatore |
| Pali, micropali, palo elastico, fondazioni superficiali | una azione di progetto (o una combinazione) con conferma esplicita | nessun generatore |
| Modelli | combinazioni lineari e inviluppi, importati da MIDAS o creati a mano | nessun generatore; il generatore automatico è una voce aperta del piano (`piano.md:689`) |
| Bridge Design | γG, γQ e carico da traffico come ipotesi modificabili | il generatore è la combinazione fissa di GPC.Design |

## 2. Il generatore di Model oggi

### 2.1 Due strumenti distinti, non collegati

| Strumento | Dove | Che cosa restituisce |
|---|---|---|
| `StandardEN1990.CreateCombinations(loadCases, options, prefix)` | `Model/Model/Design/Standards/StandardEN1990.cs:1399-1478`; interfaccia `Standard.ICombinationsGenerator` (`Standard.cs:127-137`); involucro `NormativeCombinations.Generate` (`Model/Model/Design/Combinations/NormativeCombinations.cs:13-20`) | righe di combinazione: un coefficiente per caso di carico, uno stato limite per chiamata |
| `StandardGeotechnical.Combinations(check, situation)` | `StandardGeotechnical.cs:146`; NTC 2018 in `StandardNTC2018Geotechnics.cs:78-87`; EN 1997-1 in `StandardEN1997p1.cs:78-100` | insiemi di coefficienti parziali A, M, R e γR per una verifica geotecnica; nessun caso di carico |

`StandardGeotechnical` deriva da `Standard` ma non implementa `ICombinationsGenerator`: i due strumenti non si parlano.
In produzione nessuno chiama `CreateCombinations` né `NormativeCombinations.Generate`, né in Model né in Checker: i soli
chiamanti sono 38 test di `Model/UnitTest/CombinationTest.cs`. `StandardEN1990` è istanziato in Checker solo in
`GPCChecker.Test.Concrete/DurabilityContractTests.cs:297-299`.

### 2.2 `StandardEN1990.CreateCombinations`

Opzioni (`EN1990CombinationsOptions`, `StandardEN1990.cs:795-889`):

| Opzione | Significato | Predefinito |
|---|---|---|
| `LimitState` (:800) | EQU, STR, GEO, fatica, sismica, eccezionale, SLE caratteristica, frequente, quasi permanente (:39-77) | obbligatoria |
| `ULS` (:810) | insieme B o C dell'Annesso A1 (:24-34) | B |
| `Category` (:805) | categoria dei carichi d'esercizio per i ψ, una per chiamata | A |
| `HighAltitude` (:815) | ψ della neve sopra 1000 m | vero |

Valori (costruttore, :441-506): sono quelli raccomandati di EN 1990. `StandardUNIEN1990` è una sottoclasse vuota
(`StandardUNIEN1990.cs:10-78`): non c'è un profilo NTC 2018 né un annesso nazionale. Ogni γ e ψ è una proprietà
modificabile e serializzata (:200-426, :588-651).

| Insieme | γG sfav./fav. | γQ sfav./fav. | γP |
|---|---|---|---|
| A (EQU) | 1,10 / 0,90 | 1,50 / 0 | 1,00 |
| B (STR/GEO) | 1,35 / 1,00 | 1,50 / 0 | 1,00 |
| C (GEO) | 1,00 / 1,00 | 1,30 / 0 | 1,00 |

| Stato limite | Principale | Accompagnamento |
|---|---|---|
| EQU, STR, GEO, fatica | γQ | γQ·ψ0 |
| sismica | ψ2 | ψ2 |
| eccezionale | ψ1 | ψ2 |
| SLE caratteristica | **γ·ψ2** (:1947-1952; atteso 1) | ψ0 |
| SLE frequente | ψ1 | ψ2 |
| SLE quasi permanente | ψ2 | ψ2 |

Come lavora:
- **Casi di carico.** `LoadCase.LoadCaseTypes` (`Model/Model/Actions/LoadCases/LoadCase.cs:20-30`): SelfWeight,
  SuperImposedDeadLoad, Prestress, LiveLoad, WindPressure, WindSuction, Snow, Maintenance, Earthquake, Temperature. Non
  ci sono tipi per azioni eccezionali, terreno, acqua o traffico. I ψ dipendono dal tipo e dalla categoria della chiamata,
  non dal singolo caso.
- **Permanenti.** Tutti a γinf o tutti a γsup (`GetBasicCombinationsMinCoeff` e `MaxCoeff`, :1572-1661), mai misti.
  SelfWeight e SuperImposedDeadLoad hanno lo stesso γG (:1880-1910): G1 e G2 di NTC 2018 (1,3/1,0 e 1,5/0,8) non si
  possono rappresentare.
- **Variabili.** Ogni caso variabile entra in ogni combinazione con variabili, come principale o di accompagnamento
  (:1695-1820). γQ,fav = 0 è memorizzato, ma `GetGammaQFavourable` (:1128) non è mai chiamato. Mancano i sottoinsiemi
  di variabili assenti, salvo le due combinazioni «solo permanenti». Tutti i casi dello stesso tipo sono principali
  insieme (:1834-1848). L'unica esclusione codificata è fra vento in pressione e in suzione (:1745-1790).
- **Generazione lineare.** Quattro famiglie (γinf con variabili, γsup con variabili, solo γinf, solo γsup; :1416-1476),
  non l'enumerazione di tutte le disposizioni: 8 casi danno 6 combinazioni (`CombinationTest.cs:1362`).
- **Sisma.** I casi Earthquake entrano come permanenti con γ = 1 e un solo segno, sommati tutti (:1598-1606). Mancano
  ±, regola 100/30, componente verticale, SLV/SLD/SLO; le variabili valgono sempre ψ2, senza la clausola «solo se
  sfavorevole».
- **Eccezionale.** Lo stato esiste, ma non c'è un tipo di azione eccezionale: Ad va aggiunta a mano.
- **Uscita.** `UniqueNameCollection<Combination>`, nomi `prefisso n` (:1419), doppioni eliminati sui soli coefficienti
  (:1412-1413). Lo stato limite non è leggibile dalla riga: `Combination._options` è protetto
  (`Model/Model/Actions/Combinations/Combination.cs:29`). Gli inviluppi non sono un concetto di `Combination`: esistono
  nel Converter (`CombinationKind`, `Model/GPC Converter/Contracts/ImportContracts.cs:185`; `CombinationExpansion`,
  `Model/GPC Converter/Mapping/CombinationExpansion.cs`) e nell'algebra dei risultati
  (`Model/Model/Results/Processing/ResultAlgebra.cs:35-52`).

Altri generatori dello stesso schema: `StandardEN16612` (vetro, carichi climatici; ψ climatici dichiarati provvisori
nel codice, :92-104), `StandardASCE16` (LRFD/ASD, :479-738), `StandardCopSuos2011` (`NotImplementedException`, :129-132).

Prove: `Model/UnitTest/CombinationTest.cs`, 51 metodi, 38 chiamano `CreateCombinations` (22 EN 1990, 8 EN 16612,
8 ASCE). `CommonAssert` (:16-32) controlla i coefficienti, non nomi e opzioni. Il difetto della SLE caratteristica è
fissato da `EN1990GeneratorServiceabilityCharacteristic` (:1115-1160): la neve principale vale 0,20 (ψ2) invece di 1.

### 2.3 `StandardNTC2018Geotechnics`

`StandardNTC2018Geotechnics.cs:17-92`:

| Insieme | G1 sfav./fav. | G2 sfav./fav. | Q sfav./fav. | Riga |
|---|---|---|---|---|
| A1 | 1,3 / 1,0 | 1,5 / 0,8 | 1,5 / 0 | :27 |
| A2 | 1,0 / 1,0 | 1,3 / 0,8 | 1,3 / 0 | :28 |
| A sismico | 1,0 / 1,0 | 1,0 / 1,0 | 1,0 / 0 | :29 |

- M1 = 1; M2 = 1,25 (tan φ′), 1,25 (c′), 1,4 (cu), 1,6 (qu), 1,0 (γ) (:31-32).
- γR (:35-56): fondazioni superficiali 2,3 portanza e 1,1 scorrimento; muri statici 1,4 portanza, 1,1 scorrimento,
  1,15 ribaltamento, 1,4 passiva; muri sismici 1,2, 1,0, 1,0, 1,2; stabilità globale e pendii R2 1,1 statico e 1,2
  sismico; pali per tecnologia (:49-55) e trasversali 1,3 (:56); ξ della Tab. 6.4.IV (:91).
- Scelta dell'approccio (:78-87): stabilità globale e pendii A2+M2+R2; il resto A1+M1+R3; sisma A = 1, M1, γR
  sismico; pali in sisma nessuna combinazione.
- Mancano un insieme EQU e la situazione eccezionale: `GeotechnicalSituation` ha solo PersistentTransient e Seismic
  (`StandardGeotechnical.cs:17`).
- Ogni insieme si può sostituire esplicitamente (`SetActionSet`, `SetMaterialSet`, `SetResistanceFactor`,
  `StandardGeotechnical.cs:130-143`).
- Prove: `Model/UnitTest/GeotechnicsTest.cs:52-87` (tabelle NTC), `:90-108` (approcci EN 1997), `:111-127`
  (sostituzioni e serializzazione).

Chi lo usa già, in `Checker/GPCChecker.Geotechnics/` (sempre per M e R, con azioni già combinate dall'utente):
- `DesignValues.cs:49-55` (`GeotechnicalActions.Required`); `:33-43` (γ di un'azione per categoria e insieme) esiste ma
  non ha chiamanti di produzione;
- pali: `Piles/PileModels.cs:30` (trasversali) e `:61-73` (verticali, `PileResistanceFactors.FromStandard`);
- fondazioni superficiali: `Foundations/ShallowFoundationBearing.cs:170` (M1 e γR = 2,3);
- pendii: `Slopes/SlopeModels.cs:135-145` (`SlopeFactors` da una `GeotechnicalCombination`).

### 2.4 Che cosa manca rispetto al censimento

| # | Esigenza | Chi la usa oggi | Model oggi |
|---|---|---|---|
| L1 | Coefficienti NTC 2018 delle azioni (Tab. 2.5.I per i ψ, Tab. 2.6.I e 6.2.I per EQU, A1, A2) con G1 e G2 distinti | muri; Bridge Design (γG su G2, D7-f) | solo come insiemi geotecnici (`StandardNTC2018Geotechnics`); `CreateCombinations` ha i valori EN 1990 e un solo γG |
| L2 | Enumerazione completa: ogni gruppo permanente a γinf o γsup, ogni sottoinsieme delle variabili, ogni principale | muri (fino a 2^12 disposizioni) | generazione lineare a quattro famiglie |
| L3 | ψ0, ψ1, ψ2 propri di ogni azione e gruppi di azioni correlate (una sola presenza, un solo principale) | muri | ψ per tipo di caso e per una categoria della chiamata; nessun gruppo |
| L4 | Azioni «di corpo» con fattore proprio: peso del muro, terreno (peso e spinta, sorgente unica), acqua, terreno di valle | muri | nessun concetto; sarebbero casi SelfWeight con un solo γG |
| L5 | Parte favorevole di una variabile assente (γQ,fav = 0) come riga gemella, con la stessa spinta: MUR-1 | muri | γQ,fav memorizzato e mai usato; nessuna gemella |
| L6 | Sisma: quasi permanente con varianti ±kv e due destinazioni (generale, ribaltamento), kh e kv come dati della riga | muri, stabilità globale | Earthquake come permanente, un segno, nessun kh/kv nella riga |
| L7 | Eccezionale per gruppo di azioni A, con γR = 1 | muri, stabilità globale | stato eccezionale senza tipo di azione; nessuna situazione eccezionale geotecnica |
| L8 | γR e γM nella riga, per verifica (scorrimento, ribaltamento, portanza, passiva; M2 e R2 della stabilità globale) | muri | presenti in `StandardGeotechnical`, ma non legati alle righe |
| L9 | Stato limite, approccio e destinazione leggibili nella riga | muri, sezione c.a. (insiemi per stato), ModelChecker (`CombinationCategory`) | non leggibili: opzioni protette, nomi «cmb n» |
| L10 | Nomi e ordine stabili («SLU n», «SLU nb», «SLV Generale kv− n»), doppioni eliminati senza consumare numeri, limite esplicito di 4096 righe | muri (nomi nei documenti, nei report, nelle catture e nel confronto UI F3) | nomi `prefisso n`; nessun limite |
| L11 | SLE caratteristica corretta | tutti i futuri utilizzatori | principale γ·ψ2 invece di 1 (difetto, §2.2) |
| L12 | Una sola combinazione fondamentale γG·G + γQ·Q con inviluppo del traffico | Bridge Design | possibile con `CreateCombinations`, ma senza tipo «traffico» e con i valori EN 1990 |

Nessuna di queste lacune riguarda moduli che consumano azioni già combinate: per loro «fatte in Model» vuol dire solo che,
quando un giorno le azioni verranno da un modello, la riga dovrà venire dal generatore di Model (§3.4).

## 3. Proposta

### 3.1 Principio

- **Model genera, Checker consuma, ANTHEA conserva.** Le righe (coefficiente di ogni azione, stato limite tipizzato,
  insieme di coefficienti usato, γM e γR delle verifiche geotecniche) nascono in Model. Checker traduce le righe nei
  concetti del proprio dominio (per i muri `WallCombination`) senza scrivere coefficienti. ANTHEA salva e mostra le righe
  e non calcola coefficienti.
- **Model non conosce i muri.** Model non referenzia Checker (AGENTS di Model): mensola, kh/kv e destinazioni sismiche
  restano nell'adattatore di Checker; Model offre concetti generali (parte favorevole assente, varianti sismiche,
  canali di fattori di corpo).
- **Prima l'equivalenza, poi le norme.** Il primo passo riproduce le righe di oggi bit per bit: nomi, ordine, stati,
  approccio, coefficienti e γR, comprese le scelte che differiscono da Model (G2 favorevole 0, γR statici nelle righe
  sismiche assegnate). Ogni differenza normativa diventa poi una decisione separata, con la sua voce nel registro.
- **Le matrici dell'utente restano dati dell'utente**, salvo diversa decisione (domanda D1): «fatte in Model» vale per
  ogni generazione automatica.

### 3.2 Aggiunte all'API di Model (descritte, non scritte)

Nello spazio dei nomi `GPC.Model.Design.Combinations`, accanto a `NormativeCombinations`. Tutto additivo:
`CreateCombinations` resta com'è, salvo la correzione della SLE caratteristica (M6).

| # | Aggiunta | Contenuto | Lacune coperte |
|---|---|---|---|
| M1 | Azioni di progetto tipizzate | id, nome, natura (G1, G2, P, Q, A, E), gruppo delle azioni correlate, ψ0/ψ1/ψ2 espliciti o da categoria, flag «ha una parte che può essere favorevole» (genera la gemella), flag «sorgente unica» | L3, L5 |
| M2 | Canali di fattori di corpo | permanenti con un proprio γinf/γsup e un nome (peso della struttura, terreno, acqua, terreno di valle), ognuno un gruppo dell'enumerazione, presenti solo se il chiamante li dichiara | L4 |
| M3 | Profilo delle regole | insiemi EQU, A1, A2, A sismico con G1, G2, Q fav./sfav. (si riusa `GeotechnicalActionFactors`, che li modella già), ψ per categoria (NTC 2018 Tab. 2.5.I; EN 1990 Tab. A1.1), formule degli stati limite (NTC 2018 §2.5.3); sostituzioni esplicite come in `StandardGeotechnical.SetActionSet` | L1 |
| M4 | Generatore combinatorio | richiesta con le famiglie da emettere (SLE caratteristica, frequente, quasi permanente; SLU con un insieme e il modo «completo» o «lineare»; sisma con varianti fornite dal chiamante, per esempio ±kv × generale/ribaltamento con i loro kh e kv; eccezionale per gruppo A); ordine di enumerazione dichiarato dal chiamante (per i muri, dal più rapido: valle, principale, muro, terreno, acqua, gruppi permanenti, presenza dei gruppi variabili); gemella dopo ogni riga SLU, sismica o eccezionale in cui un'azione con parte favorevole ha fattore > 0; doppioni eliminati senza consumare numeri; nomi da un modello per famiglia (etichetta, contatore unico, suffisso «b»); limite di 4096 righe con controllo preventivo ed errore esplicito | L2, L5, L6, L7, L10 |
| M5 | Riga generata | nome, stato limite tipizzato (SLU, SLE rara, frequente, quasi permanente, SLV, eccezionale), insieme A usato, coefficiente per id, azioni con la parte favorevole assente, variante sismica, e per le verifiche geotecniche la `GeotechnicalCombination` (A+M+R e γR) di ogni verifica richiesta, presa da `StandardGeotechnical.Combinations` | L8, L9 |
| M6 | Correzione di `CreateCombinations` | SLE caratteristica con la principale a 1; stato limite leggibile da `Combination` | L9, L11 |

Per la stabilità globale non serve un'API nuova: A2 dal generatore (M4) e M2/R2 da `StandardNTC2018Geotechnics`,
convertiti con `SlopeFactors.FromCombination` (`Checker/GPCChecker.Geotechnics/Slopes/SlopeModels.cs:135-145`).

Model cresce di versione sul secondo componente (funzionalità nuove), Checker pure, secondo la delega del 12/10.

### 3.3 Ordine di migrazione

| Passo | Che cosa | Equivalenza attesa |
|---|---|---|
| 0 | Model: M1–M5, con attesi indipendenti (tabelle NTC; esempi a mano come quelli di `WallEdgeCaseTests.CombinationsFollowTheNtcPreset`, `:318-382`: 15 righe con un q, 20 SLU e 16 gemelle con due Q) | nuove prove, nessuna esistente toccata |
| 1 | Muri, verifiche locali: `WallCombinations.Generate` diventa un adattatore (`WallAction` → azioni di Model, riga di Model → `WallCombination`, γR da `StandardNTC2018Geotechnics`). Le scelte di oggi diventano sostituzioni dichiarate: insieme «A1 muri» con G2 favorevole 0; γR statici nelle righe sismiche assegnate. Il generatore attuale passa nel progetto di prova come riferimento congelato (come D3 del 15/10: fuori dalle DLL distribuite, conservato) | righe identiche bit per bit su tutto il corpus; zero transizioni F3 nuove |
| 2 | Muri, stabilità globale: `WallGlobalStability.Combinations` da A2 di Model, M2/R2 da Model | righe identiche, ma attenzione all'aritmetica (§3.6, rischio R2) |
| 3 | ANTHEA: `LegacyCombinations` (versione 1 senza interfaccia) dal generatore (domanda D7); `DefaultPassiveFactor` letto dalla libreria; testi dell'approccio e dei report dal profilo | catture invariate |
| 4 | Decisioni normative, una per volta: G2 favorevole (D4), γR delle righe sismiche assegnate (D5), assenza delle altre variabili favorevoli nel sisma e negli eccezionali (D6), SLE caratteristica di Model (M6, D11) | differenze volute, quantificate sul corpus e registrate |
| 5 | Bridge Design e Viviani (D8) | piano dedicato |
| 6 | Generatore automatico del modulo Modelli (`piano.md:689`) sullo stesso generatore, con i casi MIDAS mappati sulle nature di M1 (D9) | piano dedicato |

Il passo 1 è il più piccolo che rende vera la richiesta per i muri. I passi 1 e 2 cambiano solo le librerie: ANTHEA
riceve un nuovo set in `lib/Checker`.

### 3.4 Moduli che consumano azioni già combinate

Sezione c.a., EXCEL-SOL, sezione mista, pali, micropali, palo elastico e fondazioni superficiali non generano righe e non
cambiano. Due pulizie collegate, senza cambiare risultati:
- il γG del peso dei pali viene già da Model (`PileResistanceFactors.FromStandard`); restano copie dei valori in
  `X.Calculations/CalculationDefaults.cs:7` e `src/ANTHEA.Reports/Generators/ReportWord.cs:65`, e il γR = 1,30 come testo
  in `src/ANTHEA.Application/Presentation/HorizontalPreview.cs:34`;
- `GeotechnicalActions.Factor` (`Checker/GPCChecker.Geotechnics/DesignValues.cs:33-43`) non ha chiamanti di produzione.

### 3.5 Prova dell'equivalenza

1. **Cattura prima.** Prima di toccare il codice, si congelano le uscite attuali di `WallCombinations.Generate` e di
   `WallGlobalStability.Combinations`, gemelle comprese (oggi le fissano solo le prove di unità e gli esempi, perché la
   cattura legacy non le ha):
   - ingressi: i 110 documenti di `walls-combinations.jsonl`, i documenti di `walls-documents.jsonl.gz`, i quattro
     ingressi F3 dei muri;
   - più un insieme di ingressi generati con seme fisso (numero e natura delle azioni, gruppi, falda, valle, mensola,
     sisma assegnato e dal sito, eccezionali), fino al limite di 4096 righe;
   - fixture con SHA-256 nel progetto di prova di Geotechnics.
2. **Confronto dopo.** Uguaglianza esatta, senza tolleranza: nomi, ordine, stati, approccio, destinazione, flag e
   ogni fattore in formato `R`. È più stretta della tolleranza 1e-14 di `LegacyCombinationsAreReproduced`.
3. **Prove esistenti invariate**, senza rigenerare attesi:
   - Checker: `LegacyCombinationsAreReproduced`, `LegacyGlobalCombinationsAreReproduced`, `LegacyWallsAreReproduced`,
     `WallEdgeCaseTests`, `WallExamplesTests`, `WallServiceGlobalEdgeTests`, suite complete di Model e Checker
     (`Release-Set.ps1`, `Verify.ps1`, net472 comprese);
   - ANTHEA: `RetainingWall.Checks`, `GlobalStability.Checks`, `ANTHEA.Application.Tests`, confronti congelati F4
     (`ConcreteLibraryAdapter.Checks` con `tests/fixtures/f4`), prove Desktop dei muri.
4. **Confronto UI F3 con la regola D2.** `ui/f3-equivalence` con `transitions-f4.json` invariato: nei passi 1–3 ogni
   differenza nuova o transizione mancante fa fallire la corsa; nessuna tolleranza nuova.
5. **Registro.** Una voce per la migrazione (categoria «intenzionale», stato «attuato», con le evidenze). Per ogni
   decisione del passo 4 una voce propria, «da-decidere» fino alla risposta dell'utente, con l'effetto quantificato
   sul corpus come per BUG-MUR-1; le transizioni F3 si rigenerano solo dopo la decisione.
6. **Tempi.** Generazione misurata prima e dopo sul caso peggiore (4096 righe) e sul corpus.

### 3.6 Rischi

| # | Rischio | Misura |
|---|---|---|
| R1 | Nomi e ordine delle righe: compaiono nei documenti «Personalizzate», nei report e nelle catture; uno spostamento fa fallire F3 | ordine di enumerazione dichiarato (M4) e confronto esatto (§3.5) |
| R2 | Aritmetica: oggi la stabilità globale scala le righe A1 per 1,3/1,5; il valore non coincide con quello di A2 calcolato direttamente. Per esempio, in formato `R`, (1,5·0,7)·1,3/1,5 = 0.90999999999999981, mentre 1,3·0,7 = 0.90999999999999992; con ψ0 = 0,6: 0.77999999999999992 contro 0.78 | o l'adattatore conserva la regola «A1 × γQ,A2/γQ,A1» per l'equivalenza e la si cambia poi con una voce, o si accettano differenze all'ultima cifra con una voce del registro |
| R3 | I documenti «Automatiche» rigenerano le righe a ogni calcolo: ogni cambio del generatore cambia subito i risultati dei progetti salvati | passi normativi separati e registrati; i documenti «Personalizzate» non cambiano |
| R4 | Un concetto generico di gemella troppo stretto o troppo largo: oggi la gemella toglie tutti i sovraccarichi Q dalla mensola insieme (`RetainingWallAnalysis.cs:99`), mentre forze e momenti Q non hanno una variante assente nel sisma e negli eccezionali | M1/M4 dichiarano la parte favorevole per azione; l'estensione è la domanda D6 |
| R5 | Persistenza e impronte: tipi nuovi di Model dentro documenti o impronte attivano le regole di Model (versioni di serializzazione, contratti delle impronte) | ANTHEA continua a salvare righe JSON, non oggetti di Model |
| R6 | Due generatori in Model (lineare di `CreateCombinations`, completo nuovo) | ruoli scritti nella documentazione; convergenza decisa con il generatore dei Modelli (D9) |
| R7 | Guide e Wiki descrivono il preset (`ApproachHelp`, `Method`, guida teorica sui muri, `supporto/docs/guida-teorica-anthea.md:1241-1281`) | testi invariati nei passi 1–3; con i passi normativi, regola editoriale F3-06 |
| R8 | Perimetro: il profilo dei ponti (NTC 2018 Tab. 5.1.V e 5.1.VI, traffico) e la mappatura dei casi MIDAS sono lavori più grandi dei muri | passi 5 e 6 con piani dedicati |

## 4. Domande per l'utente

| # | Domanda | Opzioni | Raccomandazione |
|---|---|---|---|
| D1 | Che cosa vuol dire «tutte in Model»? | a) ogni generazione automatica in Model, le matrici scritte dall'utente restano (muri «Personalizzate», sezione c.a., Modelli, azioni singole); b) anche i muri senza matrice libera, solo righe di Model da abilitare o escludere | **a**: le matrici sono dati dell'utente; la firma attuale obbliga già a ripartire dalle righe generate quando cambiano le azioni |
| D2 | Dove nasce il generatore? | a) generatore nuovo e generico in Model (M1–M5), adattatore dei muri in Checker; b) estendere `StandardEN1990.CreateCombinations`; c) spostare `WallCombinations` in Model così com'è | **a**: b cambia un'API provata con valori EN 1990 e un algoritmo lineare; c porta in Model concetti dei muri, contro la stratificazione |
| D3 | Primo passo bit per bit, con le scelte di oggi come sostituzioni dichiarate? | a) sì, decisioni normative dopo; b) adottare subito i valori NTC di Model, con le differenze registrate nello stesso passo | **a**: separa la prova della migrazione dalle modifiche dei risultati |
| D4 | G2 favorevole nelle righe SLU dei muri | a) 0, come oggi (valore di NTC 2008, cautelativo), dichiarato; b) 0,8 (NTC 2018 Tab. 2.6.I e 6.2.I, valore di Model) | **b**, dopo la quantificazione sul corpus: è il valore della norma del profilo e quello di Model; meno cautelativo, quindi solo con il tuo ok |
| D5 | γR delle righe sismiche con kh e kv assegnati | a) statici R3 come oggi (1,1 / 1,15 / 1,4, cautelativi); b) quelli della Tab. 7.11.III (1,0 / 1,0 / 1,2), come le righe dal sito e Model | **b**: la stessa situazione sismica ha oggi due serie di γR a seconda di come si danno kh e kv |
| D6 | Variabili favorevoli nel sisma e negli eccezionali (ψ2·Qk solo se sfavorevole, §2.5.3) | a) presenza o assenza di ogni gruppo Q anche in queste righe, come nelle SLU; b) solo la gemella della mensola, come oggi | **a**, dopo aver contato le righe in più sul corpus |
| D7 | Documenti versione 1 calcolati senza interfaccia (`LegacyCombinations`) | a) generarli da Model con le loro regole fisse, bit per bit; b) aggiornarli alla versione 2 anche nel calcolo, come fa l'interfaccia (cambiano nomi e righe); c) lasciarli come eccezione | **a**, poi valutare b |
| D8 | Bridge Design (γG·G + γQ·Q in GPC.Design) e Viviani (tre moltiplicatori dell'utente) | a) in Model subito, con un profilo dei ponti; b) dopo i muri, con un piano proprio; c) restano ipotesi dell'utente del predimensionamento, dichiarate | **b** |
| D9 | Generatore automatico del modulo Modelli | a) sullo stesso generatore di Model, dopo i muri, con piano proprio; b) generatore separato | **a** |
| D10 | Norma dei profili | a) NTC 2018 ora, con i valori EN 1990 tenuti nel profilo per la voce «Eurocodici con una norma di progetto» (dopo il refactoring); b) solo NTC 2018 | **a**, senza scelta della norma nell'interfaccia per ora |
| D11 | SLE caratteristica di `CreateCombinations` (principale ψ2 invece di 1), fissata da un test di Model | a) correggere e aggiornare l'atteso del test (serve il tuo ok, regola di AGENTS); b) lasciare finché non ci sono chiamanti | **a**: nessun chiamante di produzione, quindi nessun risultato di ANTHEA cambia |
