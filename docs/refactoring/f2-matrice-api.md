# F2.1 — Matrice di copertura delle API del calcestruzzo armato

Passo F2.1 del [piano](piano.md). Per ogni nucleo c.a. legacy di `X.Calculations` chiamato dalla
produzione: la funzione equivalente di `GPCChecker.Concrete`, l'adattatore ANTHEA che la
collegherà in F2.5-F2.9, le fixture e i test che la coprono e i buchi da chiudere in F2.3.
Il banco di confronto e i suoi esiti sono in [f2.1-banco.md](f2.1-banco.md).

Riferimenti dei numeri di riga, tutti verificati sui file:

- ANTHEA: branch `refactoring/f2-banco-confronto`, sorgenti di produzione come al commit 75e4626
  (F2.1 non cambia codice di produzione).
- Checker: `develop` 7c15cf4f, albero pulito. Lo snapshot S1 di `lib/Checker` è compilato da
  b994e188: fra i due commit `GPCChecker.Concrete` cambia solo il testo delle citazioni in
  `Cracking/CrackWidthCalculator.cs` e `Cracking/SectionCrackCheck.cs`, riga per riga, quindi
  le righe citate valgono per entrambi. I percorsi della libreria sono relativi a
  `Checker/GPCChecker.Concrete/`, quelli dei test a `Checker/GPCChecker.Test.Concrete/`.
- Fixture: `Checker/GPCChecker.Test.Concrete/Fixtures`, ultimo commit ddfe7edf (1/10/2026),
  catturate con `supporto/test/CheckerMigration.Capture` dagli ANTHEA fe4652c, b5f2222,
  4bb8815, e5efa45 e dc8415a (sorgenti di calcolo invariati da fe4652c). Righe = righe di dati,
  senza commenti e intestazione. Dal 7/10/2026 le fixture sono ricatturate da ANTHEA
  `refactoring/integrazione-d7b-d2` d2d3225 (Checker 06d97733, [f2.1-banco.md](f2.1-banco.md)):
  stesse righe; le righe dei test citate qui si riferiscono a Checker 7c15cf4f.

Tolleranze dei MigrationTests: 1e-9 · max(1, |atteso|) per taglio, SLE, torsione,
fessurazione, ancoraggi e dettagli; 1e-7 per M-χ (deformazioni dei punti snervati 1e-3);
1e-12 assoluto per i copriferri. Le stesse grandezze sono in `tests/ANTHEA.Testing/tolerances.json`
(regole `fixture-checker/`).

## Taglio

| Legacy | Libreria | Adattatore ANTHEA | Copertura | Buchi e note |
| --- | --- | --- | --- | --- |
| `ConcreteCodeChecks.Shear` (`X.Calculations/ConcreteCodeChecks.cs:53-155`), ingresso `ShearInput` (:48-51), norme ammesse `RequireOrdinary` (:42-46) | `SectionShearCalculator.Calculate` (`Shear/SectionShearCalculator.cs:16`), `SectionShearInput` (`Shear/SectionShearContracts.cs:54`), `ShearProfiles.Resolve` (`Shear/ShearProfiles.cs:43`) | `ConcreteShearAnalysis.Calculate` (`X.Calculations/ConcreteShearAnalysis.cs:10-62`) | `shear-legacy.csv`: 2016 righe (1840 esiti, 176 rifiuti, oltre 5000 valori intermedi); `ShearMigrationTests.cs:40-83` | unità kN e kNm → N e N·mm (il test moltiplica per 1000 e 1e6); nomi delle norme → classi `Standard` per tipo esatto; la libreria accetta anche CNR-DT 204 e CNR-DT 200, il legacy no |
| `Ntc2018Checks.Shear` (`X.Calculations/Ntc2018Checks.cs:264-291`), ramo NTC chiamato da `ConcreteCodeChecks.Shear` (:63-64) | stesso `SectionShearCalculator.Calculate`, profilo `Ntc2018` | come sopra | compreso nelle 2016 righe (norma "NTC 2018") | `Reference` e `Model` del risultato in inglese nella libreria: il testo delle relazioni va mappato (F2.3, tracce per i report) |
| `SectionShearGeometry.Derive` (`X.Calculations/SectionShearGeometry.cs:7`), usato da `ConcreteCalculationSettings.UpdateAutomaticShear` (`X.Calculations/ConcreteCalculationSettings.cs:61-77`) | nessuna | — | nessuna fixture; suite `verifiche/ca-module`, cattura headless | **buco**: bw, d e Asl proposti dal contorno non esistono in libreria (Model/CHECKER_PASSO_4.txt, limiti aperti: "derivazione automatica … da proporre come aiuto, mai come conferma") |

Chiamanti in produzione: `X.Desktop/Wpf/ConcreteShear.cs:113`; `X.Calculations/ConcreteAnalysis.cs:54`
(calcolo headless del modulo, da `CalculationService.cs:24`); `X.Calculations/ConcreteReinforcementDesign.cs:106`
(progetto delle armature, A09). Copia del ramo senza staffe nei muri:
`X.Calculations/RetainingWall.Structures.cs:98-100` (senza σcp, 112 430 verifiche "taglio senza
staffe" nella cattura densa dei muri; fase F4.7).

## Torsione

| Legacy | Libreria | Adattatore ANTHEA | Copertura | Buchi e note |
| --- | --- | --- | --- | --- |
| `ConcreteTorsionCalculator.Calculate` (`X.Calculations/ConcreteTorsion.cs:27-44`) | `SectionTorsionCalculator.Evaluate` (`Torsion/SectionTorsionCalculator.cs:54`) con `TorsionShearComponent` (`Torsion/SectionTorsionContracts.cs:83`), oppure `Calculate` (:23) che ricalcola il taglio con lo stesso cot θ; `SectionTorsionInput` (`Torsion/SectionTorsionContracts.cs:122`); `TorsionProfiles.Resolve` (`Torsion/TorsionProfiles.cs:50`) | `ConcreteShearAnalysis.Torsion` (`X.Calculations/ConcreteShearAnalysis.cs:63-76`) | `torsion-legacy.csv`: 986 righe (936 esiti, 49 rifiuti, 1 senza staffe); `TorsionMigrationTests.cs:28-68` | caso senza staffe: errore di ingresso nel legacy, esito non soddisfatto con resistenza nulla nella libreria (differenza intenzionale, `TorsionMigrationTests.cs:45-51`): cambia il messaggio del foglio; il legacy accetta solo NTC (`ConcreteShearAnalysis.cs:60`), la libreria tutte le norme non americane tranne CNR-DT 204 |
| `ConcreteTorsionCalculator.Geometry` (`X.Calculations/ConcreteTorsion.cs:16-26`) | `TorsionGeometry.Rectangle` e `Circle` (`Torsion/SectionTorsionContracts.cs:38`, :47) | `ConcreteShearAnalysis.Torsion` | `torsion-geometry-legacy.csv`: 10 righe (sezione a T rifiutata in entrambi); `TorsionMigrationTests.cs:70-92` | la distanza dell'asse delle barre (copriferro + Ø staffa + Ømax/2, `ConcreteTorsion.cs:20`) e l'area di calcestruzzo passano dall'adattatore |

Chiamanti in produzione: gli stessi del taglio (`ConcreteShearAnalysis.Calculate` → `Torsion`).

Collegamento (F2.5-F2.6, [piano](piano.md)): `ConcreteShearAnalysis` e la cattura densa chiamano
`ConcreteShearTorsionAdapter` (`X.Calculations/ConcreteShearTorsionAdapter.cs`), che sceglie il motore legacy o la
libreria con l'interruttore `Default`; unità, norme e testi passano da `X.Calculations/ConcreteLibraryMapping.cs`.
Esiti diversi per scelta della libreria riportati al legacy e testi: registro F2-1…F2-4.

## Tensioni SLE

| Legacy | Libreria | Adattatore ANTHEA | Copertura | Buchi e note |
| --- | --- | --- | --- | --- |
| `CheckerSection.Stress` (`X.Calculations/CheckerSection.cs:163-170`) e `DescribeStress` (:178-212): rapporto della caratteristica e della quasi permanente (:188-194), limiti k1/k2 fck con il fattore dei getti sottili e k3 fyk (:206-207) | `StressLimitCheck.Evaluate` (`Serviceability/StressLimitCheck.cs:73`) sullo stesso `StressAnalysisResult` del solutore nativo; `NotApplicableReason` (:69) per CS-TR34 | `ConcreteAnalysisSession.Stress` (`X.Calculations/ConcreteAnalysisSession.cs:91-128`) | `stress-legacy.csv`: 2016 righe (4 sezioni di `stress-sections.xml`, 9 norme, lineare e non lineare, combinazioni caratteristica, frequente e quasi permanente); `ServiceabilityMigrationTests.cs:49-100` | il legacy usa fyk della prima barra per il limite dell'acciaio (`CheckerSection.cs:207`), la libreria un limite per barra: il test confronta il limite solo se tutti i punti sono barre (`ServiceabilityMigrationTests.cs:95-96`); registro R5 (leggi di progetto nell'analisi non lineare), R6 (k5 DIN) |
| `SleCheckScope.Stress` (`X.Calculations/SleCheckScope.cs:7`) | nessuna (la frequente non ha limiti anche in libreria) | resta presentazione | — | — |

Chiamanti in produzione: `X.Desktop/Wpf/ConcreteStress.cs:109`; `X.Desktop/Wpf/ConcreteInspection.cs:83`;
`X.Calculations/ConcreteAnalysis.cs:44`; `X.Calculations/ConcreteCurvatureAnalysis.cs:24` (stati della
curva); `X.Calculations/ConcreteReinforcementDesign.Checks.cs:70`; muri
`X.Calculations/RetainingWall.Structures.cs:104-108` (8156 verifiche "tensione CLS" e 4241
"tensione acciaio" nella cattura densa dei muri).

## Fessurazione

| Legacy | Libreria | Adattatore ANTHEA | Copertura | Buchi e note |
| --- | --- | --- | --- | --- |
| `Ntc2018Checks.Cracking` (`X.Calculations/Ntc2018Checks.cs:37-197`) con `FullyTensionedCracking` (`X.Calculations/ConcreteTensionCracking.cs:7`) e `InnerCracking` (`X.Calculations/ConcreteInnerCracking.cs:10`) | `SectionCrackCheck.Evaluate` (`Cracking/SectionCrackCheck.cs:132`), `SectionCrackInput` (:48), `SectionCrackInput.OrdinaryBarStresses` (:69) | `ConcreteAnalysisSession.Stress` (`X.Calculations/ConcreteAnalysisSession.cs:119`) | `crack-legacy.csv`: 936 righe (362 aperture, 347 esiti, 1104 regioni, 10 errori, 22 + 8 casi intenzionali) su 6 sezioni di `crack-sections.xml`; `CrackMigrationTests.cs:73-182` | traccia del legacy (`CrackCalculationDetail`, formule e note in italiano, `Ntc2018Checks.cs:41-196`) contro i dettagli della libreria: il testo dei report va mappato (F2.3); il copriferro alle barre (copriferro + Ø staffa) è un ingresso esplicito; i muri leggono i testi degli esiti (`RetainingWall.Structures.cs:115`): con la libreria vanno letti gli esiti tipizzati (`CrackOutcome`); 30 stati cambiati nel legacy dopo il congelamento (vedi f2.1-banco.md) |
| `CrackRequirement` (`Ntc2018Checks.cs:19-29`; `ConcreteCodeChecks.cs:157-172`) | `CrackRequirements.For` (`Cracking/CrackRequirements.cs:49`), `CrackProfiles.Resolve` (`Cracking/CrackProfiles.cs:47`) | come sopra | `crack-scalar-legacy.csv`: 1596 righe di requisiti; `CrackMigrationTests.cs:227-255` | `SleCheckScope.Cracking` (`SleCheckScope.cs:8-13`) usa il requisito per la presentazione: passa dalla libreria |
| `CrackWidth` (`Ntc2018Checks.cs:198-256`; `ConcreteCodeChecks.cs:174-211`), `CrackK2` (`Ntc2018Checks.cs:31-36`) | `CrackWidthCalculator.Width` (`Cracking/CrackWidthCalculator.cs:43`), `K2` (:117) | come sopra | `crack-scalar-legacy.csv`: 1400 righe di aperture | registro D7-a, D7-b, R1, R2, R8 |
| `UnbondedCrackWidthBound` (`ConcreteCodeChecks.cs:215-231`) | `CrackWidthCalculator.UnbondedUpperBound` (`Cracking/CrackWidthCalculator.cs:99`) | come sopra | 8 stati di `crack-legacy.csv` (`CrackMigrationTests.cs:130-144`) | registro R3 (citazioni corrette), R7 |
| `EffectiveCrackDepth` (`ConcreteCodeChecks.cs:9-40`), `SectionRegions.Clip` e `Region` (`X.Calculations/SectionRegions.cs:9`, :22), `TensionBarSpacing.Maximum` (`X.Calculations/TensionBarSpacing.cs:11`) tramite lo stato statico `Ntc2018Checks.SpacingCalculator` (`Ntc2018Checks.cs:12`) | `CrackSectionGeometry.From` (`Cracking/CrackSectionGeometry.cs:63`), `Clip` (:83), `Region` (:99), `MaximumSpacing` (:124), `EffectiveDepth` (:192) | come sopra | regioni e interassi dei 936 stati | stato statico da eliminare (F2.7); superfici interne solo per foro rettangolare allineato o anello (Model/CHECKER_PASSO_4.txt, 4E) |

Chiamanti in produzione: `ConcreteAnalysisSession.cs:119` (da `X.Desktop/Wpf/ConcreteStress.cs:109` e
`ConcreteAnalysis.cs:44`); muri `RetainingWall.Structures.cs:111` (8154 verifiche "fessurazione" nella cattura
densa dei muri); `ConcreteReinforcementDesign.Checks.cs:79`; ambito SLE in `X.Desktop/Wpf/ConcreteStress.cs:121`,
`ConcreteReport.cs:57`, `ConcreteRefinements.cs:128` e `X.Core/ReportConcrete.cs:161`.

## Ancoraggi e aderenza

| Legacy | Libreria | Adattatore ANTHEA | Copertura | Buchi e note |
| --- | --- | --- | --- | --- |
| `ConcreteAnchorageCalculator.Calculate` (`X.Calculations/ConcreteDetailing.cs:20-38`) | `AnchorageCalculator.Calculate` (`Detailing/AnchorageCalculator.cs:77`), `AnchorageInput` (:28), `DetailingProfiles.Resolve` (`Detailing/DetailingProfiles.cs:45`) | `ConcreteDetailingAnalysis.Anchorage` (`X.Calculations/ConcreteDetailingAnalysis.cs:50-66`) | `anchorage-legacy.csv`: 468 righe (445 ancoraggi e sovrapposizioni, 5 rifiuti, 18 aderenze); `DetailingMigrationTests.cs:40-65` | **tetto di aderenza**: fctk,0,05 con fck ≤ 60 MPa è applicato dall'adattatore (`ConcreteDetailingAnalysis.cs:60`), non dalla libreria, che usa il valore ricevuto (registro R10); il legacy accetta solo NTC (`ConcreteDetailingAnalysis.cs:52-53`), la libreria anche EN, UNI, DS e CNR-DT 200 |
| `ConcreteBond.Strength` (`X.Calculations/ConcreteBond.cs:16-22`) e `ConcreteBond.Calculate` (:8-15, tetto a :12) | `AnchorageCalculator.BondStrength` (`Detailing/AnchorageCalculator.cs:69`) | scheda Materiali (`X.Materiali/Bond.cs:20-21`) | 18 righe "bond" di `anchorage-legacy.csv` | tetto come sopra; il valore di fctk,0,05 viene da `ConcreteMaterialCatalog.Material` (`X.Calculations/ConcreteMaterialCatalog.cs:12`) |

Chiamanti in produzione: `X.Desktop/Wpf/ConcreteDetailing.cs:95`; `X.Materiali/Bond.cs:20-21`;
`ConcreteReinforcementDesign.Checks.cs:106`; muri `RetainingWall.Reinforcement.cs:24` e :45-52, con
fctk,0,05 senza tetto (`RetainingWall.Reinforcement.cs:23`, `ConcreteMaterials.Concrete`): incoerenza
con il modulo sezione, da classificare (punto aperto in f2.1-banco.md).

## Dettagli costruttivi

| Legacy | Libreria | Adattatore ANTHEA | Copertura | Buchi e note |
| --- | --- | --- | --- | --- |
| `ConcreteDetailingCalculator.Calculate`, travi e pilastri (`X.Calculations/ConcreteDetailing.cs:48-108`, :119-124, :136-138) | `MemberDetailingCalculator.Calculate` (`Detailing/MemberDetailingCalculator.cs:103`), `MemberDetailingInput` (:51), tipi `Beam` e `Column` (:8) | `ConcreteDetailingAnalysis.Calculate` (`X.Calculations/ConcreteDetailingAnalysis.cs:13-48`) | `detailing-legacy.csv`: 144 righe (oltre 1000 controlli) su `detailing-sections.xml`; `DetailingMigrationTests.cs:107-133` | nomi dei controlli: italiano nel legacy, chiavi inglesi nella libreria (mappa in `DetailingMigrationTests.cs:89-99`; precedente in `ElasticHorizontalPile.Reinforcement.cs:163`); fctm ricevuto dall'adattatore (il legacy lo calcola da fck, `ConcreteDetailing.cs:58`); il legacy accetta solo NTC (`ConcreteDetailingAnalysis.cs:15-16`); registro R12, R13, R14 |
| `ConcreteDetailingCalculator.Calculate`, **solette** (`ConcreteDetailing.cs:51`, :88-118) | nessuna | `ConcreteDetailingAnalysis.Calculate` | nessuna fixture | **buco**: interasse principale e secondario, armatura secondaria ≥ 20 % (EC2 §9.3.1.1) non sono in libreria (Model/CHECKER_PASSO_4.txt, 4F: "rinviate al passo 6"); F2.3 |
| `ConcreteDetailingCalculator.Calculate`, **pareti** (`ConcreteDetailing.cs:125-135`, :137-138) | nessuna | `ConcreteDetailingAnalysis.Calculate` | nessuna fixture | **buco**: armatura verticale e orizzontale, interassi, 0,08 Ac in giunzione (EC2 §9.6) non sono in libreria; F2.3 |

Parametri delle solette e pareti senza corrispondenza in `MemberDetailingInput`:
`SecondarySteelPerMetre`, `SecondarySpacing`, `CriticalSlabRegion` (`ConcreteDetailing.cs:40-44`).
Parametri della libreria che il legacy non passa: maggiorazioni del copriferro e copriferro contro
terra (`MemberDetailingCalculator.cs:46-49`, predefiniti 0).

Chiamanti in produzione: `X.Desktop/Wpf/ConcreteDetailing.cs:72`; `ConcreteReinforcementDesign.cs:85` e :90.
I dettagli dei pali elastici usano già la libreria (`GPC.Checkers.Concrete.Piles`,
`ElasticHorizontalPile.Reinforcement.cs:5`); i dettagli dei muri sono propri
(`RetainingWall.Structures.cs:80-85`, `RetainingWall.Reinforcement.cs`, fase F4.7).

## Momento-curvatura

| Legacy | Libreria | Adattatore ANTHEA | Copertura | Buchi e note |
| --- | --- | --- | --- | --- |
| `MomentCurvatureCalculator.Calculate` (`X.Calculations/MomentCurvature.cs:20-70`) con dominio e stati di `CheckerSection` | `MomentCurvatureAnalysis.Calculate` (`Response/MomentCurvatureAnalysis.cs:174`, con `SectionCheckerModelCode2010`; forma generale a :108), `MomentCurvatureRequest` (:28) | `ConcreteCurvatureAnalysis.Calculate` (`X.Calculations/ConcreteCurvatureAnalysis.cs:8-25`) | `curvature-legacy.csv`: 5 curve (NTC, EN, MC2010; almeno 205 punti); `DetailingMigrationTests.cs:223-265`, tolleranza 1e-7 | unità kN, kNm e 1/m del legacy, N, N·mm e 1/mm della libreria; con trefoli il legacy rifiuta (`ConcreteCurvatureAnalysis.cs:12-13`); definizioni di χy e χu (registro R14) |

Chiamanti in produzione: `X.Desktop/Wpf/ConcreteCurvature.cs:39`.

## Durabilità e copriferri

| Legacy | Libreria | Adattatore ANTHEA | Copertura | Buchi e note |
| --- | --- | --- | --- | --- |
| `Durability.Cover` (`X.Calculations/Materials/Durability.cs:50-61`), `StructuralClass` (:44-49) | `CoverRequirements.Calculate` (`Durability/CoverRequirements.cs:141`) con profilo EN o UNI, `StructuralClass` (:135), `CoverInput` (:82) | `MaterialCover.Required` (`X.Calculations/Materials/MaterialCover.cs:16-32`) | `durability-legacy.csv`: 588 righe EC2; `DurabilityMigrationTests.cs:24-70` | la libreria aggiunge DS (senza classi strutturali) e il profilo UNI |
| `NtcCover.Calculate` (`X.Calculations/Materials/NtcCover.cs:18-33`), `DefaultCmin` (:13-17), `Severity` (:6-12) | `CoverRequirements.Calculate` con profilo `Ntc2018` (`Durability/CoverRequirements.cs:152-162`), gruppo ambientale `ExposureClass.NtcEnvironment` (`Durability/ExposureClasses.cs:25`) | `MaterialCover.Required`; `ConcreteDetailingAnalysis.Calculate` (`ConcreteDetailingAnalysis.cs:29-40`) | `durability-legacy.csv`: 1932 righe NTC e 505 rifiuti | registro R11 (Cmin predefinito) |
| `MinimumConcrete.Required` (`X.Calculations/Materials/MinimumConcrete.cs:15-18`) | `ExposureClasses.Uni11104MinimumStrength` (`Durability/ExposureClasses.cs:90`); per profilo `MinimumStrength` (:100) | scheda Materiali; `ConcreteDetailingAnalysis.cs:38` | 23 righe "MIX" di `durability-legacy.csv` | registro D7-e (C30/37 contro C28/35) |
| `AtecapMix.Required` e `Air` (`X.Calculations/Materials/MixAutomation.cs:15-26`) | `ExposureClasses.Uni11104Mix` (:122), `Uni11104Air` (:129) | scheda Materiali (`X.Materiali/MixAutomation.cs:54`, :61) | 23 righe "MIX" | la composizione della miscela resta in ANTHEA (Checker/docs/migrazione-anthea/MIGRAZIONE_ANTHEA.txt:262) |
| `Durability.Exposures` (`Durability.cs:11-31`) con le descrizioni in italiano | `ExposureClasses.All` (`Durability/ExposureClasses.cs:56`), `Get` (:78) | scheda Materiali | indiretta | le descrizioni della libreria sono in inglese: i testi dell'interfaccia restano in ANTHEA |
| `Durability.EffectiveWater` (`Durability.cs:62-66`) | nessuna | scheda Materiali | nessuna | composizione della miscela: fuori da GPCChecker.Concrete; candidato GPC.Design (F4) |
| `ConcreteCoverAnalysis.Calculate` (`X.Calculations/ConcreteCoverAnalysis.cs:14-28`) | `CoverRequirements.Calculate` tramite `MaterialCover` | è l'adattatore | nessuna fixture (solo i nuclei) | validazione di progetto (F2.9, F4.15) |

Chiamanti in produzione: `X.Core/ProjectValidation.cs:46-49`; `X.Calculations/CalculationService.cs:39`;
`X.Calculations/RetainingWall.Materials.cs:71`; `X.Calculations/RetainingWall.Reinforcement.cs:44`;
`X.Materiali/MaterialDetails.cs:131` e :173; `X.Materiali/MinimumConcrete.cs:14`;
`X.Materiali/MixAutomation.cs:54-71`; `X.Materiali/ExposureSelector.cs:59` e :87-89. I pali elastici
usano già `CoverRequirements` (`ElasticHorizontalPile.Reinforcement.cs:155`).

## Sessione, orchestrazione e stato condiviso

| Legacy | Ruolo in F2 | Note |
| --- | --- | --- |
| `ConcreteAnalysisSession` (`X.Calculations/ConcreteAnalysisSession.cs:12-129`): cache dei domini e degli stati, `Stress` in parallelo (:103) | adattatore di SLE e fessurazione | non contiene formule; resta in ANTHEA (strato applicativo, F3) |
| `ConcreteAnalysis.Calculate` (`X.Calculations/ConcreteAnalysis.cs:9-73`) | calcolo headless del modulo (`CalculationService.cs:24`) | contratto JSON da mantenere (F2.5) |
| `CheckerSection` (`X.Calculations/CheckerSection.cs`), lock `NativeSolverConstruction` (:27, :98) | costruzione del solutore nativo e della mesh | lock da sostituire con mesh sicure in parallelo (F2.3, F2.11) |
| `ConcreteCodeChecks.IsEurocode` e `RequireOrdinary` (`ConcreteCodeChecks.cs:41-46`), nomi delle norme come stringhe | sostituiti dai profili per tipo esatto | la mappa nomi → `Standard` è in `X.Calculations/ConcreteStandards.cs:37` |

## Buchi da chiudere prima del collegamento

1. **Solette e pareti**: le regole di `ConcreteDetailing.cs:109-118` e :125-135 non hanno
   equivalente (`MemberDetailingKind` ha solo `Beam` e `Column`). Senza di esse `ConcreteDetailingAnalysis`
   non può passare tutto alla libreria. F2.3.
2. **Tetto di aderenza**: fck ≤ 60 MPa per fctk,0,05 è nell'adattatore ANTHEA
   (`ConcreteDetailingAnalysis.cs:60`, `ConcreteBond.cs:12`), non nella libreria (R10); i muri non lo
   applicano (`RetainingWall.Reinforcement.cs:23`). Decidere se la regola entra in
   `AnchorageCalculator` (calcolo nella libreria, come chiede AGENTS.md).
3. **Proposta di bw, d e Asl** (`SectionShearGeometry.Derive`) e **profilo resistente a torsione**
   (`ConcreteTorsionCalculator.Geometry`, solo in parte coperto da `TorsionGeometry.Rectangle/Circle`):
   la libreria vuole dati espliciti. Serve un aiuto geometrico in libreria o la decisione che resti
   una proposta dell'interfaccia.
4. **Tracce e testi delle relazioni**: le tracce del legacy (simboli, formule, note in italiano) e i
   messaggi di stato alimentano report e JSON; la libreria ha dettagli e messaggi in inglese. Per
   lasciare invariati contratto JSON e testo delle relazioni serve una mappa nell'adattatore o tracce
   localizzabili in libreria (F2.3, "tracce per i report").
5. **Norme**: torsione, dettagli e ancoraggi del legacy sono solo NTC; la libreria copre più norme.
   Decidere se l'interfaccia le apre (cambia il contratto) o resta su NTC in F2.
6. **Esiti diversi per scelta**: torsione senza staffe (non soddisfatta invece di errore), asse
   neutro nel copriferro e barre fuori da Ac,eff (già allineati nel legacy dal commit 0a63315). I
   muri leggono i testi degli esiti di fessurazione (`RetainingWall.Structures.cs:115`).
7. **Stato statico e lock**: `Ntc2018Checks.SpacingCalculator` (`Ntc2018Checks.cs:12`) e il lock di
   `CheckerSection` (:27, :98).
8. **Nessuna fixture** per solette, pareti, `SectionShearGeometry`, gli adattatori
   (`ConcreteShearAnalysis`, `ConcreteDetailingAnalysis`, `ConcreteCoverAnalysis`, `MaterialCover`) e
   `Durability.EffectiveWater`: per questi il riferimento resta la cattura headless (B1) e le suite
   ANTHEA (`verifiche/ca-module`, `verifiche/ca-data`, `ConcreteCode.Checks`, `ConcreteDesign.Checks`).

## Copertura in numeri

| Fixture | Righe | Funzioni legacy | Test di Checker |
| --- | ---: | --- | --- |
| `shear-legacy.csv` | 2016 | `ConcreteCodeChecks.Shear`, `Ntc2018Checks.Shear` | `ShearMigrationTests.cs:40-83` |
| `stress-legacy.csv` + `stress-sections.xml` | 2016 | `CheckerSection.Stress` / `DescribeStress` | `ServiceabilityMigrationTests.cs:49-100` |
| `torsion-legacy.csv` | 986 | `ConcreteTorsionCalculator.Calculate` | `TorsionMigrationTests.cs:28-68` |
| `torsion-geometry-legacy.csv` | 10 | `ConcreteTorsionCalculator.Geometry` | `TorsionMigrationTests.cs:70-92` |
| `crack-legacy.csv` + `crack-sections.xml` | 936 | `Ntc2018Checks.Cracking` e catena | `CrackMigrationTests.cs:73-182` |
| `crack-scalar-legacy.csv` | 2996 | `CrackWidth`, `CrackRequirement` | `CrackMigrationTests.cs:227-255` |
| `anchorage-legacy.csv` | 468 | `ConcreteAnchorageCalculator`, `ConcreteBond.Strength` | `DetailingMigrationTests.cs:40-65` |
| `detailing-legacy.csv` + `detailing-sections.xml` | 144 | `ConcreteDetailingCalculator` (travi e pilastri) | `DetailingMigrationTests.cs:107-133` |
| `curvature-legacy.csv` | 5 | `ConcreteCurvatureAnalysis`, `MomentCurvatureCalculator` | `DetailingMigrationTests.cs:223-265` |
| `durability-legacy.csv` | 3048 | `Durability.Cover`, `NtcCover`, `MinimumConcrete`, `AtecapMix` | `DurabilityMigrationTests.cs:24-70` |
| totale | 12 625 | | |
