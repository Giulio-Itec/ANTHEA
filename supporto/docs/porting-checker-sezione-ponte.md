# Metodo della sezione da ponte da rivedere per Checker

Aggiornamento dettagli locali: [irrigidimenti, appoggi e connessione](irrigidimenti-appoggi-connessione.md).
I punti 1–3 sono aggiunti a valle dei risultati; in questa revisione il ciclo delle
fasi descritto qui rimane invariato. I nuovi metodi puri e i loro test sono già in Checker.

Il metodo scritto in ANTHEA è stato inizialmente separato in file dedicati senza
modifiche numeriche. Successivamente sono stati aggiunti, su richiesta, i riferimenti
di N per fase descritti sotto. Le formule delle larghezze efficaci, il ciclo,
le tolleranze e i difetti già documentati restano invariati. Non è stato ancora
trasferito in Checker. Questa separazione rende leggibili il ciclo di
classe 4, l'adattatore del solver e i risultati prima di decidere l'API di libreria.

## Ordine di lettura

| File e metodo | Responsabilità |
| --- | --- |
| [BridgeSection.Analysis.cs](../../X.Core/BridgeSection.Analysis.cs), `Calculate` | Validazione delle opzioni, situazioni cumulative, iterazione, convergenza, diagnostica |
| Stesso file, `Solve` | Un contributo sulla geometria efficace corrente, omogeneizzazione, chiamata Checker, equilibrio |
| Stesso file, `SteelPieces` | Rettangoli Model della carpenteria efficace; anima orientata verticalmente anche come parete sottile |
| [BridgeSection.EffectiveWidths.cs](../../X.Core/BridgeSection.EffectiveWidths.cs), `EffectiveWidths`, `InternalPlate`, `Outstand` | Riduzioni locali dell'anima e degli sbalzi; senza dipendenze da UI o JSON |
| [BridgeSection.Results.cs](../../X.Core/BridgeSection.Results.cs) | Geometria, pannelli, contributi, punti tensionali e situazioni |
| [BridgeSection.cs](../../X.Core/BridgeSection.cs) | Ingressi ANTHEA, cataloghi, costruttore Model da ponte e φ↔n |

I nomi delle classi sono rimasti gli stessi. I nuovi campi di risultato sono
aggiuntivi e i parametri introdotti nelle firme pubbliche sono opzionali. La classe parziale
permette di separare i sorgenti senza introdurre due implementazioni concorrenti.
Le classi dei risultati già espongono i coefficienti del campo affine
`UniformStress`, `StressSlope`, `Centroid`, le proprietà complete e l'inerzia
di integrazione, oltre a φ, ψLφ e n per ciascun contributo. `LoadReference` e
`LoadY` espongono il riferimento adottato; `MomentAtInterface` riporta il momento
alla quota comune y=0 prima della somma.

## Percorso da esaminare insieme

Per ogni situazione `i`, `Calculate` riparte dalla carpenteria lorda e include
le fasi attive da 0 a i. A ogni iterazione `Solve` risolve ciascun incremento
sulla stessa carpenteria efficace corrente, con i propri parametri di
omogeneizzazione. La somma delle tensioni nell'acciaio determina le nuove larghezze.
Il rilassamento è 0,55, la tolleranza relativa sulle larghezze 1E−7 e il limite
120 iterazioni. La mancata convergenza non produce un risultato utilizzabile.

`Solve` ricava le proprietà da Model. Per la composta chiama
`SectionSolver.GetLinearStressAnalysisResult` con il prodotto ψLφ e ricava
il campo lineare dai vertici della carpenteria. Acciaio solo e soletta esclusa
usano l'equilibrio elastico sulle proprietà dei componenti Model. La somma dei
contributi e l'iterazione della sezione efficace sono il codice aggiunto in ANTHEA;
il percorso interno di analisi lineare delle fasi resta quello di Checker.

Le unità interne sono N, mm e MPa; le azioni archiviate sono kN e kNm.
Le quote sono misurate dall'interfaccia, positive verso l'alto.
Nel trasporto al baricentro entra `Mx,G = Mx + N*(yG-yN)`.
Il residuo di equilibrio è verificato con l'inerzia di integrazione di Checker.

`Calculate` prepara un punto per ogni fase: `y_ref` per quota comune oppure
`GrossPhaseCentroid` per baricentro lordo. Per il riferimento efficace passa un
valore nullo interno a `Solve`, che lo risolve nel `cy` corrente dopo avere
ricavato le proprietà omogeneizzate. È un'ipotesi distinta: il punto si muove
insieme al baricentro efficace. Il valore risolto viene usato sia nel solver sia
nell'audit dell'equilibrio ed esposto nel contributo, anche a carico nullo.
I test dedicati sono in
[BridgeLoadReferenceTests](../../../Checker/GPCChecker.Test.BridgeAudit/BridgeLoadReferenceTests.cs).

La carpenteria efficace è comune a tutti i contributi della singola situazione:
questo **non congela lo stato raggiunto nelle fasi precedenti** e non simula una
storia evolutiva con redistribuzione viscosa. È la prima ipotesi da confermare
prima di trasferire l'algoritmo in libreria.

## Confine proposto per la futura API

### Estensione ritiro uniforme (25 settembre 2026)

`SolveShrinkage` è nell'adattatore ANTHEA e usa il percorso meccanico esistente di
Checker per la deformazione compatibile. Il metodo Checker ispezionato non riceve
una deformazione iniziale del CLS. Il ritiro non è quindi ottenuto limitandosi ad
assegnare un carico esterno equivalente.

Per ogni fase si assegnano Δεcs in µε, φ, ψL e n; ψL iniziale 0,55. Si calcolano
Ac netto delle barre, relativo yc ed Ec,eff = Ea/n. Si risolve la sezione composta
per Neq = Ec,eff Ac Δεcs a yc, poi si corregge il solo CLS con −Ec,eff Δεcs.
Le tensioni di acciaio e barre restano quelle della deformazione compatibile.
Il contributo conserva separatamente `ShrinkageStrain`, `ConcreteStressOffset`,
`EquivalentN`, `EquivalentMomentAtInterface`; le sue azioni esterne N/Mx/V sono zero.
Sono effetti primari autoequilibrati; gli effetti secondari di vincoli esterni e
l'evoluzione del ritiro/viscosità nel tempo non vengono dedotti automaticamente.
Il ritiro può essere inserito più volte e in qualsiasi posizione. Le larghezze
efficaci continuano a essere comuni ai contributi di ogni situazione.

I 15 test in [BridgeShrinkageTests](../../../Checker/GPCChecker.Test.BridgeAudit/BridgeShrinkageTests.cs)
risolvono indipendentemente compatibilità ed equilibrio con una matrice EA/ES/EI,
nelle stesse ipotesi di integrazione del solver. Coprono nessuna/una/due file di barre,
φ nullo/non nullo, segni e ritiro nullo, sovrapposizione/riordino, φ↔n, classe 4,
taglio ininfluente sulle tensioni normali e soletta isolata senza barre.
La suite filtrata di regressione raggiunge 176 test superati; B02–B05 restano esclusi
come casi noti, senza modifiche al solver di Checker.

Riferimenti primari: [JRC, calcolo di ponti composti, slide 13](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/D1.8Davaine.pdf)
per ψL = 0,55 e [SCI P356](https://steelconstruction.info/images/archive/c/c8/20140319164505%21SCI_P356.pdf)
per effetti primari autoequilibrati e distinzione dagli effetti secondari.

Il trasferimento dovrà sostituire `JsonObject` e le etichette italiane con oggetti
tipizzati: sezione/materiali Model, elenco degli incrementi, tipo di sezione,
quota di riferimento e opzioni di analisi. I cataloghi UI, il formato di archivio,
i limiti tensionali selezionati e le esportazioni devono restare nel livello
applicativo, oppure essere passati esplicitamente come dati della verifica.

Proposta da discutere, non ancora implementata: un ingresso `BridgeAnalysisInput`,
con sezione, fasi e opzioni; un risultato `BridgeLinearAnalysisResult`, con una
collezione di situazioni e relativi contributi. Per ciascun contributo servono
campo tensionale, piano fisico delle deformazioni, eventuale piano grezzo
restituito dal solver, coefficienti di omogeneizzazione e residui. Il piano grezzo
Checker è scalato: non si possono sommare direttamente piani con ψLφ diversi.

Le dipendenze applicative da rimuovere sono `J`, i default JSON, `ConcreteStandards.Create`
e il lock `CheckerSection.NativeSolverConstruction`. Quest'ultimo protegge la
costruzione del solver e della mesh: va sostituito dalla sincronizzazione corretta
in libreria, non semplicemente eliminato. `BridgeResult.Input` e `Json()` sono
responsabilità dell'adattatore ANTHEA, non del futuro risultato numerico puro.

## Punti aperti preservati

Non sono stati corretti i bug numerici già segnalati nell'
[audit del solver](../../../Checker/docs/approfondimento-solver-lineare-ponte.md):

- **B02:** a carico nullo `Solve` non entra nel ramo della composta che sottrae
  le inerzie proprie dalle proprietà di integrazione. Il metadato `SolverInertia`
  differisce dal caso caricato, pur con tensioni nulle corrette. Il ramo è invariato.
- **B03–B05:** rotazione del riferimento, sottrazione del CLS per acciaio inglobato
  e asse neutro globale con riferimento traslato sono difetti del percorso Checker
  documentati separatamente. Il presente intervento non modifica quei metodi.

Da confermare nella revisione anche l'equivalenza delle due piattabande inferiori
(conserva area e spessore, non in generale baricentro e inerzia), l'uso della
tensione più compressiva per gli sbalzi e il trattamento di ψ inferiore a −3.
La relazione continua a dichiarare queste ipotesi e il campo N–Mx del modello.

## Prove da usare nella revisione

I test numerici principali restano in Checker:

- [BridgeElasticStagesTests](../../../Checker/GPCChecker.Test.Concrete/BridgeElasticStagesTests.cs): fasi elastiche e confronti indipendenti;
- [BridgeLinearSolverAuditTests](../../../Checker/GPCChecker.Test.Concrete/BridgeLinearSolverAuditTests.cs): comportamento del solver e casi di regressione dei bug;
- [GPCChecker.Test.BridgeAudit](../../../Checker/GPCChecker.Test.BridgeAudit/README.md): suite riproducibile e collegamento al metodo ANTHEA.

In ANTHEA, [BridgeSectionChecks](../test/X.Verifiche/BridgeSectionChecks.cs)
contiene 117 controlli su classe 4, equilibrio, omogeneizzazione, armature,
fasi e confronto diretto con Checker. Lo spostamento dei metodi conserva
integralmente i loro corpi; questi controlli passano dopo la separazione.
Le prove UI sono in [BridgeOptionsSmokeChecks](../test/Desktop/BridgeOptionsSmokeChecks.cs):
verificano sincronizzazione dei campi, aggiornamento delle opzioni, invalidazione
e scarto dei risultati calcolati su revisioni superate, senza sostituire i test fisici.

```powershell
dotnet build ANTHEA.sln -c Release --no-restore
dotnet supporto/test/X.Verifiche/bin/Release/net8.0/ANTHEA.Verifiche.dll --bridge
dotnet X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll --smoke-bridge supporto/artefatti/ponte_opzioni_N
```


## Nuovi metodi a taglio e pioli

I metodi puri sono già in `Checker/GPCChecker.Steel/CompositeBridges`, temporaneamente inclusi da ANTHEA con collegamento ai sorgenti. Sono ricavati direttamente da NTC/EC, senza utilizzare il precedente metodo a taglio come oracolo. Riferimenti, difetti preesistenti non corretti e campo di validità: [revisione normativa](taglio-pioli-fonti-e-metodo.md).
