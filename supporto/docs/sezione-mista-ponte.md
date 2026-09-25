# Sezione composta da ponte

Modulo `str_mista_ponte`, disponibile in **Moduli singoli → Strutture → Sezione composta**,
nel menu File e nei fogli dei progetti. Analisi elastica delle tensioni normali N–Mx
di una soletta su carpenteria saldata ad H, con trattamento locale di classe 4.

File di esempio: [sezione_mista_ponte.json](../esempi/sezione_mista_ponte.json),
con tre contributi di carico, due piattabande inferiori e due file di armature.
I valori sono dimostrativi e possono essere modificati dopo l'apertura in ANTHEA.

## Interfaccia

Tre schede numerate, con la stessa organizzazione del modulo in calcestruzzo:
**Pannello di controllo**, **Fasi e omogeneizzazione**, **Tensioni e classe 4**.
Ogni fase raccoglie nello stesso gruppo le azioni, la sezione reagente e i parametri
di omogeneizzazione da φ oppure n. Normativa, coefficienti, materiali, geometria e armature
sono raccolti nei gruppi espandibili del pannello di controllo.
Ingressi a sinistra, viewport al centro, riepiloghi tensionali a destra e tre gruppi
di risultati in basso: **Tensioni**, **Fasi e proprietà**, **Sezione efficace**.
Le tabelle di omogeneizzazione e di equilibrio sono riunite; la sezione efficace
comprende i parametri di classe 4, le proprietà geometriche e i dettagli del metodo
in gruppi espandibili. I separatori regolano le dimensioni dei pannelli; disposizione,
scheda e modalità di visualizzazione sono salvate nel foglio. Le preferenze della
precedente disposizione a quattro schede sono migrate all'apertura.
Le unità di ingresso sono mm, MPa, kN e kNm; i risultati JSON mantengono i double.
Le tabelle si possono scorrere e copiare. Sono disponibili esportazioni JSON complete
e CSV delle tensioni in tutte le situazioni. **Esporta Word** produce la relazione
completa del modulo, anche come capitolo nella relazione di progetto.
Si possono scegliere normativa, materiali, geometria, azioni, omogeneizzazione,
tensioni, classe 4 e grafici. Ambito, riepilogo e avvisi sono sempre inclusi.
Il report singolo usa il risultato acquisito, senza ricalcolarlo: riporta tutte le
situazioni, i contributi separati, le proprietà geometriche e di integrazione,
i parametri dei pannelli e una figura per situazione, indipendentemente dalla
fase visualizzata. La relazione di progetto ricalcola una copia dei fogli,
come per gli altri moduli, e conserva gli ingressi specifici del ponte nel capitolo.

Come nel modulo CA, i campi numerici si acquisiscono all'uscita dal campo; la
presentazione arrotondata conserva la precisione del valore inserito. Il ricalcolo
parte dopo 500 ms dall'acquisizione. Gli esiti precedenti sono rimossi immediatamente
all'acquisizione; calcoli superati o interrotti non possono ripopolare i risultati.
Salvataggio, riapertura e Home/Riprendi conservano geometria, fasi e parametri.
Nelle finestre più piccole le barre di scorrimento mantengono accessibili i pannelli.

La viewport contiene geometria reale, armature, contorno della piattabanda equivalente,
asse a tensione nulla dell'acciaio e diagrammi totali/per contributo. Rotella e pulsanti
cambiano lo zoom; trascinamento sposta la vista; doppio clic e “Adatta” ripristinano
il disegno. La cornice `ViewportFrame` è la stessa del modulo CA, con esportazione
**PNG** e **Espandi** in una finestra dedicata.
La parte inefficace dell'anima è semitrasparente (opacità 28%), con contorno arancio
tratteggiato; le parti efficaci mantengono il riempimento pieno. Gli sbalzi inefficaci
delle piattabande sono tratteggiati in arancio. Si tratta delle parti eliminate dal
modello a larghezze efficaci, non di una deformata né di un esito di instabilità a taglio.

## Collegamento a Model e Checker

- Cataloghi di `GPCModelData`: `ConcreteMaterialEN1992Data`, `SteelMaterialEN1993Data`,
  `SteelMaterialEN1992Data`. `fck` è esposto come valore positivo: internamente Model
  conserva le resistenze a compressione negative.
- Geometria: costruttore da ponte di `ReinforcedConcreteSection` indicato nella richiesta,
  `SectionH`, `RebarSectionCircular` e distribuzione delle barre eseguita da Model.
- Proprietà composte: `GetHomogeneizedMechanicalProperties(phi)`; i risultati riferiti
  al CLS sono divisi per n per ottenere l'omogeneizzazione all'acciaio strutturale.
- Tensioni composte: `SectionSolver.GetLinearStressAnalysisResult` e
  `GetStructuralSteelVerticesTension`. Lo stesso campo lineare determina le tensioni
  di CLS e armature con i rispettivi rapporti modulari.
- Sezioni efficaci: la carpenteria è ricostruita con rettangoli posizionati di Model.
  I tratti d'anima sono orientati verticalmente anche come `ThinWall`, perché Checker
  integra lungo la linea media. La costruzione dei solver condivide il lock già usato
  dal modulo CA per la triangolazione nativa.
- Acciaio solo e soletta interamente esclusa: equilibrio elastico N–Mx sulle proprietà
  dei componenti Model. Iterazione delle larghezze efficaci e somma dei contributi sono
  implementate in ANTHEA, non attribuite a un'API Checker di classe 4.

Sorgenti esaminati: `Checker/GPCChecker.Test.Concrete/MixedSectionTest.cs`, in particolare
`TensionCheck02` e `TensionCheck03` (categoria Bridge); il costruttore in
`Model/Model/Sections/Concrete/ReinforcedConcreteSection.cs`; la vista dei profili H
in `CheckerUI/ModuleConcreteSection/ViewModels/SteelSections/HSteelSectionViewModel.cs`
dell'archivio `CheckerUI.7z`; le tre fasi del precedente `CompositeSectionChecker`.
`GPCChecker.Steel/EuroCode/ECClass4ThinWallSection.cs` e i relativi test sono commentati;
`PanelsStability/EffectiveSection.cs` è escluso da `#if NEVER`.

Il metodo per le sezioni da ponte **è quindi presente in Checker ed è riutilizzato**.
Nei test Bridge il metodo `SectionChecker.GetLinearStressAnalysisResult(phi)` delega
a `SectionSolver.GetLinearStressAnalysisResults`; il modulo usa la variante pubblica
per singola azione `SectionSolver.GetLinearStressAnalysisResult`. Il percorso risolve
il piano di deformazione sulla geometria assegnata. L'iterazione implementata in
ANTHEA riguarda invece le larghezze efficaci della carpenteria tra chiamate al solver.

## Geometria e armature

La larghezza della soletta è **b_eff già determinata dal progettista**; non viene
ricavata automaticamente da luce, interasse, vincoli o shear lag.
Il profilo è centrato sotto la soletta; la flessione fuori piano e l'eccentricità
orizzontale non sono comprese nella versione N–Mx.

L'altezza dell'anima è quella libera tra le piattabande. La seconda piattabanda
inferiore è facoltativa, centrata sotto la prima e con larghezza non maggiore.
Nel calcolo si usa:

```
t_eq = t1 + t2
A_inf = b1*t1 + b2*t2
b_eq = A_inf / t_eq
```

L'equivalenza conserva area e spessore complessivo, **non in generale baricentro e
inerzia propri** di due piastre di larghezza diversa. Le proprietà reali ed equivalenti
sono esposte per confrontarle. La seconda piastra usa lo stesso acciaio della prima.
È disponibile un fy assegnato, comune alla carpenteria, per tener conto dello spessore.

Le due file di armature sono indipendentemente disattivabili; non sono inserite barre
fittizie quando una fila è assente. La quota richiesta è **faccia → asse barra**.
Model determina il numero di barre dal passo, centrandole sulla larghezza della soletta.
Sono respinte file sovrapposte/invertite e dimensioni non valide. Il calcolo non verifica
automaticamente il copriferro minimo di durabilità né l'interferro costruttivo minimo.

## Fasi e omogeneizzazione

Da 1 a 20 contributi, ciascuno attivabile, rinominabile e riordinabile. I valori N e Mx
sono **incrementi già combinati**; il programma non applica ulteriori γG/γQ e non genera
combinazioni. Le fasi di solo acciaio devono precedere quelle composte.
Per default: G1 su acciaio, G2 sulla composta a lungo termine, Q sulla composta a breve termine.

```
n0 = Ea / Ecm
n = n0 * (1 + psiL * phi)
phi = (n/n0 - 1) / psiL
```

L'ingresso può essere φ o n. Il parametro passato alle API di Model e Checker è
`psiL*phi`; l'inversa usa `ReinforcedConcreteSection.CalculateHomogenizedFactorPhi`.
Il rapporto delle armature Es/Ea è conservato; non si assumono uguali Ea ed Es.
Per G2 è preimpostato ψL=1,1; la determinazione di φ(t,t0) resta un dato esterno.
Riferimento: [JRC, Davaine, Bridge design, slide sui rapporti modulari](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/D1.8Davaine.pdf).

Per ogni situazione “Dopo fase i” si sommano i contributi fino a i sulla carpenteria
efficace comune a quella situazione, usando il rapporto modulare di ogni contributo.
È una sovrapposizione elastica per il controllo di sezione, **non una simulazione
evolutiva di costruzione**, né un modello di redistribuzione per viscosità o fessurazione.
“Composta” include la soletta non fessurata; “Soletta esclusa” esclude tutto il CLS
dal contributo e conserva le armature. Un CLS teso è segnalato senza assegnargli un
esito favorevole. Non è implementata la ricerca automatica della parte compressa.

## Classe 4 e risultati

Riduzione dell'anima come pannello interno non irrigidito e delle piattabande come
sbalzi con tensione uniforme assunta pari alla più compressiva nello spessore.
I parametri sono ψ, kσ, λp, ρ, larghezza compressa e tratti efficaci. Per ψ < −3,
kσ e la curva di riduzione sono valutati a −3, mantenendo la larghezza compressa
effettiva: scelta conservativa esplicitata, fuori dall'intervallo tabulato.

L'iterazione usa rilassamento 0,55, tolleranza relativa 1E−7 sulle larghezze e massimo
120 iterazioni. La mancata convergenza impedisce l'emissione di risultati utilizzabili.
Il rapporto è riferito a fy caratteristico, non a fy/γM0.
Riferimento e benchmark: [JRC, Commentary and worked examples to EN 1993-1-5](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2021-12/EUR22898EN.pdf),
capitoli 4 e 17, in particolare il sottopannello d'anima b=492 mm, t=8 mm, ψ=0,406:
kσ≈5,632, λp≈0,912, ρ≈0,871.

Risultati esposti:

- Tensioni nei bordi della soletta, file di armature, estradosso/intradosso della
  carpenteria e estremi dell'anima; contributo di ogni fase e somma.
- Limiti tensionali e rapporti locali, con distinzione di elementi inattivi e CLS teso.
  SLU: αcc·fck/γc, fy/γM0, fyk/γs. SLE: limiti di compressione del CLS 0,60/0,45 fck,
  armature 0,80 fyk e controllo elastico della carpenteria a fy.
- A*, yG, Ix*, moduli resistenti alle fibre estreme dell'acciaio, n0, n, φ, ψLφ,
  Ec,eff, quota a tensione nulla e curvatura per contributo.
- Area, baricentro e inerzia della carpenteria lorda/efficace, rapporti di riduzione,
  spostamento del baricentro e quote della zona inefficace dell'anima.
- Proprietà reali/equivalenti delle piastre inferiori, quantità e area delle barre,
  azioni incrementali/cumulative, iterazioni e residuo di convergenza.

**Ambito dei controlli:** N–Mx, connessione completa, sezione simmetrica, anima senza
irrigidimenti longitudinali, instabilità locale sotto tensioni normali. Non sono
verificati taglio, torsione, interazione M–V, connettori, fatica, instabilità globale,
fessurazione o shear lag. I rapporti tensionali non attestano la verifica completa del ponte.

## Verifiche riproducibili

L'audit aggiuntivo del 25/09/2026 è nella libreria Checker:
[rapporto di audit](../../../Checker/docs/audit-sezioni-miste-ponte.md) e
[suite per le fasi](../../../Checker/GPCChecker.Test.BridgeAudit/README.md).
Il motore non è stato modificato durante l'audit. I test `KnownBug` conservano
le aspettative corrette e falliscono fino alla risoluzione dei difetti segnalati.

```
dotnet run --project supporto/test/X.Verifiche -- --bridge
dotnet X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll --smoke-bridge supporto/artefatti/verifiche_mista
```

La suite comprende il benchmark JRC, il caso di sezione del test Bridge di Checker,
confronti diretti con l'API della DLL corrente per φ=0 e φ=2 (tolleranza relativa 1E−5),
proprietà di Model, trasporto del momento in presenza di N, φ↔n, somma dei contributi,
assenza delle file, piastre, soletta esclusa, azioni nulle, compressione oltre il limite,
errori di ingresso e roundtrip dell'archivio. I valori tabulati storici di TensionCheck03
sono confrontati con tolleranza 0,6%; il confronto diretto con la DLL corrente è più stretto.
Checker integra le pareti sottili sulla linea media; le inerzie geometriche Model
includono anche l'inerzia propria nello spessore. Non si forzano i due risultati a coincidere.
L'inerzia di integrazione è esposta nella scheda Fasi. Un controllo indipendente
ricostruisce N e Mx dal campo tensionale secondo queste medesime ipotesi;
un residuo normalizzato maggiore di 1E−5 impedisce l'emissione dei risultati.

Lo smoke WPF salva immagini delle schede e delle fasi e controlla ricalcolo automatico,
invalidazione, dati incompleti, Home/Riprendi, riapertura e JSON a 1600, 1366 e 960 px.
Controlla inoltre espansione/rientro della viewport, acquisizione al cambio di focus,
precisione degli ingressi, navigazione senza ricalcolo e persistenza dei separatori.
Le schermate sono artefatti di verifica locali, non nuove dipendenze dell'applicazione.
