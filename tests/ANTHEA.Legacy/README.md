# ANTHEA.Legacy

Codice di `X.Calculations` che il prodotto non usa più, conservato per le prove (decisione D3 del 15/10/2026,
`docs/refactoring/decisioni.md`). Esce dagli assembly distribuiti ma resta nel repository: l'installer pubblica solo
`src/ANTHEA.Desktop` e il suo grafo, e nessun progetto di `X.*` o `src/` può referenziare questo
(`tests/ANTHEA.Architecture.Tests`).

- **Contenuto**: motori storici (SLE, taglio e torsione, durabilità, sezione storica, muri, pali, progetto delle armature,
  Bridge Design), rami mai più raggiungibili e la sonda delle prove degli adattatori.
- **Namespace**: `Anthea.Calculations`, come in origine. Le classi intere si spostano con il loro nome. Le parti di classi
  `partial` non possono stare in due assembly: prendono un nome nuovo con il prefisso `Legacy` (per esempio
  `LegacyNtc2018Checks`).
- **Intestazione**: ogni file spostato riporta il file d'origine, il commit di partenza e il motivo.
- **Accesso**: i membri `internal` di `ANTHEA.Calculations` sono visibili tramite `InternalsVisibleTo`. Nessun membro del
  prodotto diventa pubblico per questo progetto.
- **Aggancio (modo b della decisione D3)**: gli adattatori del prodotto leggono l'aggancio corrente con
  `EngineHook.Get<T>()`. È un `AsyncLocal`, nullo in produzione, quindi gli adattatori usano la libreria. Le prove agganciano
  un motore storico per la durata di un ambito: `using (….Use(…)) { … }`, costruito con `EngineScope`. La sonda
  `ServiceabilityProbe` (con la vista `DurabilityProbe`) si aggancia allo stesso modo e registra adattatore, operazione e
  motore di ogni chiamata.
- **Riferimenti**: lo usano soltanto i progetti di prova. Le prove WPF lo ricevono tramite `ANTHEA.Testing`, nella sola
  configurazione `UiTests`. `tools/qa/Assert-NoTestCode.ps1` blocca l'installer se `ANTHEA.Legacy.dll` o uno dei nomi
  spostati compare nella cartella pubblicata.

## Cartelle

| Cartella | Contenuto | Aggancio |
| --- | --- | --- |
| `Serviceability/` | `LegacyNtc2018Checks` (con le parti `TensionCracking` e `InnerCracking`), `LegacyConcreteCodeChecks`, `LegacyServiceability`; enum `ServiceabilityEngine` | `ServiceabilityEngines.Use`, `ForModule`, firme di prima con il motore come ultimo argomento |
| `ShearTorsion/` | `ConcreteTorsionCalculator`; enum `ShearTorsionEngine` | `ShearTorsionEngines.Use` e firme di prima |
| `Durability/` | `DurabilityLegacy`, `LegacyDurability` (con `ValidateExposure`, E5), `LegacyNtcCover`; enum `DurabilityEngine` | `LegacyDurabilityEngine.Use` |
| `Walls/` | `LegacyRetainingWall` (verifiche strutturali, dettagli, distinta, gravità, ricerca delle armature precedente), `RetainingWallFunctions` (con i record `SoilMass` e `ValleyBand`, E5) | `LegacyRetainingWall.Use`, `Calculate` e `DesignReinforcement` con la semantica di prima |
| `Section/` | `SezioneElastica`, `LegacySezioneCA` (motore storico di `SezioneCA`); interfaccia `IMomentCurvatureCalculator` senza utilizzatori (E5) | chiamate dirette |
| `SectionDesign/` | `LegacyConcreteReinforcementDesign` (ricerca precedente e rami mai raggiungibili; `NativeSearchIndices`, E5) | chiamate dirette |
| `Piles/` | `LegacyElasticHorizontalPile`, `LegacyPaloOrizzontale`, `LegacyElasticPileVerificationCache` | chiamate dirette |
| `Detailing/` | `LegacyCircularDetailingCalculator`, `LegacyCircularSpacing` (codice senza chiamanti); `TensionBarSpacing` con `ITensionBarSpacing` e l'interfaccia `IConcreteAnchorageCalculator` senza utilizzatori (E5) | — |
| `Bridge/` | `LegacyBridgeConcept` (motore precedente, ricerca, schede tecniche), `BridgeLibraryAccess` (involucri GPC usati solo dalle prove) | chiamate dirette |
| `Geometry/` | `LegacySectionShearGeometry`, `LegacyBridgeSection` (codice senza chiamanti); `LegacySectionRegions` (`Clip`, `Area`, `Region` di `SectionRegions`, E5) | — |
| radice | `EngineScope`, `ServiceabilityProbe`, `DurabilityProbe`, `MappingCoverage` (viste delle tabelle della mappatura per la prova 5a) | — |

E5 indica i membri usati solo dalle prove e le due interfacce senza utilizzatori spostati con la decisione E5 del 15/10/2026 (F2.11c).

Altro codice di sola prova uscito dagli assembly distribuiti sta nei progetti che lo usano: la traccia dei ripieghi del JSON
in `tests/ANTHEA.Testing/Capture/JsonFallbackTrace.cs`, il modello Excel storico delle azioni in `tests/Shared/`, il testo
dei PDF in `tests/ANTHEA.Reports.Tests/DocxPdfText.cs`, i membri delle finestre usati dalle prove WPF in parti `partial`
sotto `tests/ANTHEA.Desktop.UiTests/` (compilate solo in `UiTests`).
