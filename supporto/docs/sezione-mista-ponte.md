# Sezione composta da ponte

Modulo `str_mista_ponte`, disponibile in **Moduli singoli → Strutture → Sezione composta**,
nel menu File e nei fogli dei progetti. Analisi elastica delle tensioni normali N–Mx
di una soletta su carpenteria saldata ad H, con trattamento locale di classe 4.

File di esempio: [sezione_mista_ponte.json](../esempi/sezione_mista_ponte.json),
con tre contributi di carico, due piattabande inferiori e due file di armature.
I valori sono dimostrativi e possono essere modificati dopo l'apertura in ANTHEA.

## Interfaccia

Due schede numerate, con la stessa organizzazione del modulo in calcestruzzo:
**Pannello di controllo** e **Fasi e tensioni**.
Ogni fase raccoglie nello stesso gruppo le azioni, la sezione reagente e i parametri
di omogeneizzazione φ, ψL e n, sempre modificabili e sincronizzati. Normativa, coefficienti, materiali, geometria e armature
sono raccolti nei gruppi espandibili del pannello di controllo. La seconda scheda
riunisce anche la scelta dei limiti tensionali, l'attivazione della classe 4, l'esclusione
dell'instabilità locale di piattabanda superiore, inferiore e anima (una parte esclusa resta
interamente efficace, ρ = 1) e la
quota di applicazione di N. La tabella e il dettaglio modificano gli stessi oggetti fase,
senza valori indipendenti da riallineare.
Il **Pannello di controllo** è dedicato a geometria, materiali e armature: non
contiene selettori delle fasi o delle tensioni, riepiloghi tensionali o tabelle
di verifica. La fascia verticale a destra mostra le **Proprietà della sezione**:
schede scorrevoli **Acciaio**, **Soletta** e una scheda per ogni fase inserita.
Acciaio espone carpenteria reale, singole piastre/anima e carpenteria equivalente.
Soletta espone il rettangolo lordo, le file di barre e le proprietà omogeneizzate al CLS:
i suoi φ/ψL/n servono all'esplorazione e non modificano le fasi di carico.
Le schede per fase usano invece gli stessi φ/n della tabella Sollecitazioni e
mostrano le proprietà lorde riferite all'acciaio. A, baricentro, Ix, Iy, Ixy,
moduli resistenti e raggi di inerzia vengono aggiornati al cambio degli ingressi.
Le proprietà efficaci restano nei risultati perché dipendono dai carichi.
La viewport mostra etichette con
richiami alle piattabande, all'anima, alla soletta e alle due file. Riporta le
dimensioni reali inserite, distinguendo le due piastre inferiori. I comandi
**Quote sezione** e **Info armature** nascondono separatamente le annotazioni;
nascondere le info delle barre non elimina le barre dalla geometria o dal calcolo.

In **Fasi e tensioni**: ingressi a sinistra, viewport al centro, riepiloghi
tensionali a destra e cinque gruppi in basso, nell'ordine: **Sollecitazioni**,
**Fasi e proprietà**, **Sezione efficace**, **Tensioni**, **Verifiche**.
Sollecitazioni è una tabella modificabile con attivazione, nome, tipo, N, Mx, V,
punto N/riferimento Mx, φ, ψL, n e incremento di ritiro. I campi non applicabili
sono attenuati e non modificabili. I pulsanti aggiungono, spostano o eliminano le fasi;
la selezione della situazione cumulata non limita le righe di ingresso visibili.
Le verifiche comprendono limiti tensionali SLU/SLE, taglio, interazione N–M–V e,
quando attivi, irrigidimenti, appoggi e connessione. Campo e ipotesi dei dettagli
sono descritti in [irrigidimenti, appoggi e connessione](irrigidimenti-appoggi-connessione.md).
Le tabelle di omogeneizzazione e di equilibrio sono riunite; la sezione efficace
comprende i parametri di classe 4, le proprietà geometriche e i dati di convergenza
in gruppi espandibili. Criteri, campo del modello, formule di omogeneizzazione e
convenzioni sono raccolti nella finestra **Info modello…**. Il fattore n adottato
compare accanto a φ e ψL; la tabella **Fasi e proprietà** ne raccoglie i valori con
le proprietà e i risultati dei contributi.

Il selettore **Risultati cumulati fino alla fase** controlla grafico, riepilogo e
tabelle nella scheda **Fasi e tensioni**. Le tensioni e la sezione efficace dipendono dagli
incrementi delle fasi attive fino a quella selezionata; geometria e materiali
restano comuni. I separatori regolano le dimensioni dei pannelli; disposizione,
scheda e modalità di visualizzazione sono salvate nel foglio. Le preferenze delle
precedenti disposizioni a quattro o tre schede sono migrate all'apertura.
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
parte dopo 500 ms dall'acquisizione. Gli esiti precedenti restano visibili insieme
alla loro geometria, con avviso **DA AGGIORNARE**; non sono esportabili come
risultati correnti. Il nuovo risultato li sostituisce soltanto a calcolo riuscito.
Anche in presenza di dati invalidi si conserva l'ultimo calcolo valido. Nella
scheda geometrica si aggiorna invece l'anteprima con i dati geometrici acquisiti,
senza sovrapporvi tensioni precedenti. Calcoli superati o interrotti non possono
ripopolare i risultati correnti.
Salvataggio, riapertura e Home/Riprendi conservano geometria, fasi e parametri.
Nelle finestre più piccole le barre di scorrimento mantengono accessibili i pannelli.

La viewport contiene geometria reale (anche le due piastre inferiori), armature,
asse a tensione nulla dell'acciaio e diagrammi totali/per contributo. I mirini
indicano i punti di applicazione di N sull'asse di simmetria, con quota y e forza
dei contributi della situazione selezionata. Punti coincidenti sono raggruppati.
Sono visibili anche a N nullo e vengono inclusi
nell'inquadratura se esterni alla sezione; non indicano forze verticali.
Rotella e pulsanti
cambiano lo zoom; trascinamento sposta la vista; doppio clic e “Adatta” ripristinano
il disegno. La cornice `ViewportFrame` è la stessa del modulo CA, con esportazione
**PNG** e **Espandi** in una finestra dedicata.
I diagrammi tensionali sono campiti in blu per la compressione e in rosso per la
trazione, con ordinate orizzontali. In **Contributi delle fasi** le rette incrementali
sono tratteggiate sopra le campiture della somma. Le croci arancio indicano le armature.
Il controllo **CLS ×** amplifica soltanto la larghezza del diagramma del calcestruzzo:
etichette, tabelle, verifiche e risultati conservano le tensioni reali in MPa.
**Auto n**, attivo inizialmente, segue il rapporto di omogeneizzazione dell'ultima
fase composta attiva (Q nello schema standard), indipendentemente dalla situazione
visualizzata. Il tooltip identifica la fase sorgente. Se ci sono soltanto fasi di
ritiro si usa il loro ultimo n attivo; senza fasi con CLS si usa 1.
Inserire un numero tra 0,01 e 1000 imposta la scala manuale; riattivare **Auto n**
ripristina il collegamento. Queste preferenze sono salvate nell'archivio e applicate
anche ai grafici del report, senza ricalcolare la sezione.
La parte inefficace dell'anima è semitrasparente (opacità 28%), con contorno arancio
tratteggiato; le parti efficaci mantengono il riempimento pieno. Gli sbalzi inefficaci
delle piattabande sono tratteggiati in arancio. Si tratta delle parti eliminate dal
modello a larghezze efficaci, non di una deformata né di un esito di instabilità a taglio.

## Collegamento a Model e Checker

La finestra **Info modello…** comprende quattro schemi vettoriali illustrativi:
somma delle fasi, relazione n/φ, punto N fisso o iterativo e ciclo di classe 4.
Il testo distingue il modello implementato, le scelte di carico e le approssimazioni.

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

Dal 26 settembre 2026 (CompositeBridge 1.1, Model `SectionHDoubleBottomFlange`) il calcolo
usa le **due piastre reali**; il rettangolo equivalente, che conserva area e spessore ma
non in generale baricentro e inerzia, resta esposto solo per confronto. Per l'instabilità
locale ciascuna piastra è uno sbalzo dall'anima con il proprio spessore (a favore di sicurezza). La seconda piastra usa lo stesso acciaio della prima.
**Sovrascrivi fy per tutta la carpenteria** sostituisce il valore di catalogo con un
unico fy assegnato per anima e tutte le piattabande, prima dell'applicazione di γM0.
Non modifica le armature e non corregge automaticamente fy in funzione dello spessore;
il tooltip dei campi chiarisce questi aspetti.

Le due file di armature sono indipendentemente disattivabili; non sono inserite barre
fittizie quando una fila è assente. La quota richiesta è **faccia → asse barra**.
Model determina il numero di barre dal passo, centrandole sulla larghezza della soletta.
Sono respinte file sovrapposte/invertite e dimensioni non valide. Il calcolo non verifica
automaticamente il copriferro minimo di durabilità né l'interferro costruttivo minimo.

## Fasi e omogeneizzazione

Da 1 a 20 contributi, ciascuno attivabile, rinominabile e riordinabile. I valori N, Mx e V
sono **incrementi già combinati**; il programma non applica ulteriori γG/γQ e non genera
combinazioni. Le fasi di solo acciaio devono precedere quelle composte.
Per default: G1 su acciaio, G2 sulla composta a lungo termine, Q sulla composta a breve termine.

Allo **SLU** assegnare le azioni di progetto già coefficientate con γF e ψ;
γM0, γc e γs agiscono sulle resistenze. Cambiare SLU/SLE aggiorna i limiti tensionali,
senza generare nuove combinazioni né moltiplicare i carichi. **V** viene acquisito,
cumulato, salvato ed esposto nei risultati/report; non modifica le tensioni normali
e non viene ancora verificato.

### Ritiro della soletta

**+ Ritiro** aggiunge un incremento Δεcs uniforme imposto al solo CLS, negativo per
accorciamento: −250 µε = −0,25‰. La deformazione iniziale è zero da completare;
φ iniziale 2, ψL iniziale 0,55. Ogni fase ha propri φ/ψL/n sincronizzati, può essere
spostata in qualsiasi punto e può essere ripetuta. Inserire incrementi, non valori
cumulativi già inclusi nelle fasi precedenti.

Il calcolo usa Ec,eff = Ea/n e l'area di CLS al netto delle barre. Risolve Neq =
Ec,eff Ac Δεcs al baricentro del CLS netto, poi aggiunge −Ec,eff Δεcs alle sole
tensioni del CLS. Questo termine è indispensabile per ottenere l'equilibrio senza
carichi esterni: le barre e la carpenteria vincolano il ritiro libero della soletta.
Neq e Meq,0 sono ausiliari, riportati separatamente in **Fasi e proprietà** e nel
report; N/Mx/V esterni della fase restano nulli. Il grafico include le tensioni da
ritiro ma non un mirino N fittizio.

Il modello include gli effetti primari locali; gli effetti secondari dei vincoli
esterni richiedono azioni separate. Non deduce ritiro da età/umidità né evolve la
viscosità nel tempo. La scheda **Info modello…** espone formule e campo di applicazione.

Le armature possono avere una tensione totale inferiore all'estradosso della trave:
non ricevono G1 su solo acciaio. Nelle altre fasi conta la deformazione alla quota
della barra e il suo modulo Es; il confronto va eseguito per contributo, non
assumendo uguali le tensioni totali a quote/materiali diversi.

Ogni fase sceglie il riferimento di N e del proprio incremento Mx:

- **Baricentro omogeneizzato lordo**, iniziale per nuovi fogli e nuove fasi UI:
  quota calcolata una sola volta sulla sezione lorda con i materiali e n della
  fase; resta fissa durante la ricerca della sezione efficace.
- **Baricentro efficace · iterativo**: quota uguale al baricentro della fase
  a ogni iterazione. L'ipotesi è un carico che resta centrato sulla sezione efficace.
- **Quota comune**: usa `y_ref`. I vecchi archivi senza la nuova chiave conservano
  questa modalità e quindi il precedente significato delle azioni.

La quota comune è disabilitata nella UI se nessuna fase attiva la usa. Il momento
assegnato è riferito al punto N della propria fase. La tabella espone `yN` effettivo
per ogni contributo; i momenti cumulati sono riportati a y=0:
`Mx,0 = Mx − N*yN/1000` con kN, mm e kNm. Con baricentro iterativo la stessa fase
può avere quote diverse in situazioni cumulative diverse. I report seguono le
stesse convenzioni e riportano le quote per situazione.

```
n0 = Ea / Ecm
n = n0 * (1 + psiL * phi)
phi = (n/n0 - 1) / psiL
```

φ, ψL e n sono sempre modificabili: cambiando n si ricava φ; cambiando φ o ψL
si ricava n, mantenendo φ quando si modifica ψL. Ogni fase ha valori indipendenti.
Il selettore «Parametro di ingresso» è stato eliminato. Al cambio materiale resta
fisso l'ultimo parametro assegnato (n oppure φ) e si aggiorna quello dipendente.
La chiave `modo` rimane nel formato di archivio per questa preferenza e per leggere
i file precedenti. Non vengono salvati valori arrotondati dalla presentazione.
n < n₀, φ negativo e ψL non positivo impediscono il calcolo; il valore derivato non
disponibile è mostrato come trattino. Il parametro passato alle API di Model e Checker è
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

L'iterazione usa tolleranza relativa 1E−7 sulle larghezze e massimo 120 iterazioni.
Da CompositeBridge 1.2 (`AcceleratedIteration`, predefinito) ogni situazione parte
dalla geometria efficace convergente della precedente e il fattore di rilassamento
segue la formula di Aitken (Irons–Tuck), partendo da 0,55 e limitato a [0,05; 1]:
23–24 iterazioni diventano 7–9, con lo stesso punto fisso entro la tolleranza.
Disattivando l'opzione ogni situazione riparte dalla sezione lorda con rilassamento
fisso 0,55. La mancata convergenza impedisce l'emissione di risultati utilizzabili.
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
irrigidimenti longitudinali, instabilità locale sotto tensioni normali. Sono aggiunti
taglio, interazione N–M–V e dettagli opzionali di irrigidimenti, appoggi e connessione,
inclusa la fatica dei pioli. Restano esclusi torsione, instabilità globale, fatica
generale della carpenteria, fessurazione e shear lag. I rapporti locali non attestano
la verifica completa del ponte. Ipotesi e limiti sono nel documento
[dettagli locali](irrigidimenti-appoggi-connessione.md).

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
Le prove delle opzioni coprono φ↔n, ψL, materiali, quota di N interna/esterna,
classe 4 attiva/disattiva, tutti i limiti SLU/SLE, tipo di sezione, attivazione delle
fasi e modifiche durante il calcolo. La fase visualizzata resta la stessa quando
si attiva o disattiva una fase precedente, invece di seguire l'indice della riga.
Le schermate sono artefatti di verifica locali, non nuove dipendenze dell'applicazione.

I 25 test dei nuovi riferimenti sono in Checker:
[BridgeLoadReferenceTests](../../../Checker/GPCChecker.Test.BridgeAudit/BridgeLoadReferenceTests.cs).
Coprono baricentri da aree omogeneizzate indipendenti, armature opzionali,
trasporto N–Mx, punto lordo fisso, punto efficace iterativo, riferimenti misti,
persistenza e compatibilità degli archivi precedenti.

Il codice da rivedere prima del trasferimento in Checker è descritto in
[Preparazione del metodo per Checker](porting-checker-sezione-ponte.md).


## Taglio, pioli e aggiornamento delle fasi

Il taglio ora ha verifiche dedicate dell’anima e degli irrigidimenti trasversali intermedi opzionali. Sono disponibili input, resistenze e flussi per i pioli uniformi. Formule, fonti primarie, differenze NTC/EC e limiti sono raccolti in [Fonti e metodo di taglio e connessione](taglio-pioli-fonti-e-metodo.md).

La tabella conserva tutte le righe durante le modifiche. Aggiungendo una fase si segue l’ultima situazione cumulativa; scegliendo esplicitamente una fase precedente la scelta resta conservata. Il comando “Mostra tutte le fasi” riattiva il seguito dell’ultima situazione.

La viewport offre tre comandi indipendenti nel menu Verifiche: colorazione σ/limite sulla sezione, sui diagrammi, e rette dei limiti. Rosso indica superamento, viola CLS teso. Le parti inefficaci dell’anima restano semitrasparenti. Le opzioni grafiche non ricalcolano e sono conservate nell’archivio e nel report.
