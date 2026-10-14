# F2-7 — Rifiuti e uscite della fessurazione non raggiunti dal modulo

Voce F2-7 del registro, ricostruita dopo l'audit del 14/10/2026. Riguarda la fessurazione della sezione in c.a.
(`SectionCrackCheck` di GPCChecker.Concrete 0.9.0.0), chiamata da `ConcreteServiceabilityAdapter.LibraryCracking`
(X.Calculations). Fonte della copertura: `misura.json` del full del 12/10
(`supporto/artefatti/ci/20261012-225933-f2-set-t-full/ConcreteLibraryAdapter.Checks`): 926 risultati del corpus,
934 con i 15 stati mirati.

## 1. Ordine dei controlli: libreria e legacy

La libreria è il trasferimento del legacy (`Ntc2018Checks.Cracking`, ancora selezionabile con
`ServiceabilityEngine.Legacy`): l'ordine delle uscite è lo stesso, con gli stessi testi italiani tramite
`ConcreteLibraryMapping`.

| # | Uscita della libreria (`SectionCrackCheck.cs`) | Legacy (`Ntc2018Checks.cs`) | Corpus | Stati mirati |
| --- | --- | --- | --- | --- |
| 1 | requisito: `NotRequired`, `MissingExposure`, `MissingDesignLimit` (276-279) | riga 80 | sì | sì |
| 2 | decompressione e formazione: `UncrackedStressRequired` se manca la sezione non fessurata (285-286) | — (la calcola sempre, 84-96) | no | no |
| 3 | `PrestressNotSupported` (303) | riga 99, stesso testo | no | sì |
| 4 | `RequiresLinearCrackedAnalysis` (304) | righe 100-101 | sì | sì |
| 5 | tensioni delle barre mancanti o non finite, `BarStresses` (339-340) | riga 130 | no | no |
| 6 | sezione interamente compressa, wk = 0 (332-338) | riga 129 | sì | sì |
| 7 | sezione interamente tesa: facce, `FaceWithoutAreaOrSteel` (561-565), `SpacingUndetermined` (569) | `ConcreteTensionCracking` | facce sì, rifiuto no | idem |
| 8 | `NeutralAxisUndetermined` (366) | riga 153 | no | no |
| 9 | `NoTensileReinforcement` (385) | riga 172 | no | no |
| 10 | `ZeroEffectiveDepth` (406) | riga 186 | no | no |
| 11 | nessuna barra in Ac,eff: limite superiore (427-443) | righe 210-223 | sì | sì |
| 12 | `NoEffectiveSteelOrArea` (445-449) | riga 224 | no | no |
| 13 | `SpacingUndetermined` (465-469) | stesso ramo | sì | sì |
| 14 | fori: `InnerSurfaceUnreinforced`, `InnerSurfaceNotSupported` (757) | `ConcreteInnerCracking` | il primo sì | il primo sì |
| 15 | formula: `RibbedBarsRequired`, `WidthParameters`, `UpperBoundParameters` (`CrackWidthCalculator`) | riga 262 e testi equivalenti | solo `RibbedBarsRequired` | idem |

Unica differenza di ordine: il punto 2 non esiste nel legacy, che calcola sempre la sezione non fessurata.
L'adattatore passa sempre la funzione `Uncracked` (`ConcreteServiceabilityAdapter.cs:246-264`), quindi il rifiuto non
può presentarsi.

## 2. Esame delle uscite non raggiunte

Tre esiti possibili: **impossibile** (le condizioni del modulo lo escludono, con la prova), **degenere** (possibile
solo con dati numericamente assurdi), **raggiungibile** (dati di progetto plausibili lo producono: è un buco del
corpus, non un ramo morto).

| Uscita | Esito | Motivo |
| --- | --- | --- |
| `UncrackedStressRequired` | impossibile | la funzione della sezione non fessurata è sempre passata (sopra) |
| `BarStresses` | impossibile | le tensioni vengono dal solutore lineare per tutte le barre della geometria (`tensioni_barre.Take(Bars.Count)`); il punto 4 esclude l'analisi non lineare |
| `ZeroEffectiveDepth` | impossibile | ogni barra è dentro il contorno (`CheckerSection.ValidateRebarPosition`, copriferro con segno ≥ −1e-8 mm con raggio > 0), quindi h − d > 0; h − x = εmax/\|∇ε\| > 0 e h > 0: tutti i candidati di hc,eff sono positivi in ogni profilo (EC2/NTC, DS, DIN) |
| `NoEffectiveSteelOrArea` | impossibile | con hc,eff > 0 la fascia oltre Qmax − hc,eff contiene il vertice più teso del contorno e ha area positiva; i fori sono centrali e non toccano il contorno (`SezioneCA.BuildHole`) |
| `InnerSurfaceNotSupported` | impossibile | il modulo genera solo un foro rettangolare centrato e allineato agli assi o un anello circolare concentrico (`SezioneCA.BuildHole`; foro disabilitato per la sezione a T), cioè le due forme supportate |
| `WidthParameters`, `UpperBoundParameters` | impossibile in condizioni ordinarie | i dati della formula sono già controllati a monte (materiali positivi, ρ > 0, s > 0 dopo il punto 13); restano come difesa della libreria |
| `NeutralAxisUndetermined` | degenere | richiede εmax > 1e-12, εmin < 0 e \|∇ε\| ≤ 1e-15 1/mm: poiché εmax − εmin ≤ \|∇ε\|·D, serve una sezione con estensione D > 1 m e uno scarto di deformazione fra 1e-12 e 1e-15·D, cioè azioni dell'ordine di 1e-8 volte quelle di esercizio |
| `PrestressNotSupported` | **raggiungibile** | qualunque sezione con trefoli e criterio di apertura delle fessure (`workspace.trefoli` non vuoto); raggiunto dagli stati mirati «precompressione», assente dal corpus |
| `NoTensileReinforcement` | **raggiungibile con armature libere** | sezione armata da un solo lato con il lato opposto teso, per esempio barre solo inferiori e momento negativo: nessuna barra tesa e la barra più vicina al lembo teso oltre h/2. La disposizione automatica lo esclude, perché impone almeno due barre superiori e due inferiori (`SezioneCA.cs:120`) e la sezione circolare ha barre su tutto il contorno |
| `FaceWithoutAreaOrSteel` | **raggiungibile con armature libere** | sezione interamente tesa armata da un solo lato: per la faccia senza barre la fascia efficace (al più h/2 o h/3) non contiene barre tese; stessa esclusione della disposizione automatica |

## 3. Valutazione

- L'ordine dei rifiuti della libreria coincide con quello del legacy; l'unico controllo in più (punto 2) non è
  raggiungibile dal modulo.
- Sei uscite non raggiunte sono impossibili con i dati che il modulo produce e una è degenere: nessuna serve a un
  progetto reale e nessuna dà un esito favorevole senza calcolo.
- Tre uscite sono raggiungibili con dati plausibili e mancano dal corpus: precompressione con apertura delle
  fessure e, con le armature libere, sezione armata da un solo lato con il lato opposto teso, in flessione o in
  trazione. In tutti e tre i casi
  la libreria e il legacy si fermano senza esito numerico e con lo stesso testo: «Apertura CAP: modello
  aderenza/decompressione da definire», «Nessuna armatura tesa», «⟨faccia⟩: area o armatura efficace assente».

## 4. Proposta per la decisione dell'utente

1. Accettare le sette uscite impossibili o degeneri come difese della libreria, senza prove nel modulo.
2. Aggiungere agli stati mirati di `ConcreteLibraryAdapter.Checks` i due casi raggiungibili che mancano
   (`NoTensileReinforcement` e `FaceWithoutAreaOrSteel`), con il confronto fra libreria e legacy; la precompressione
   è già coperta dagli stati mirati.
3. Valutare a parte se «Nessuna armatura tesa» e la faccia senza armatura debbano restare senza esito: il calcolo si
   ferma correttamente, ma l'utente non riceve un'indicazione sulla fessurazione della zona non armata.
