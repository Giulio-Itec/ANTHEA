# Sezione da ponte: irrigidimenti, appoggi e connessione

Revisione 25 settembre 2026. Perimetro concordato: completare i dettagli locali,
l’interazione N–M–V e la connessione, lasciando invariato il calcolo delle fasi.
Questo documento integra [taglio e pioli](taglio-pioli-fonti-e-metodo.md).

## Interfaccia e dati

Restano due schede principali e cinque gruppi di risultati. Nel pannello di controllo
i gruppi espandibili raccolgono irrigidimenti intermedi, appoggi, pioli, armatura
trasversale e fatica. I campi non pertinenti sono nascosti e non vengono validati:
per esempio il pannello oltre la fine della trave, il secondo piatto in disposizione
monolaterale e l’interasse del montante rigido su un appoggio interno.

La sezione disegna i piatti effettivi, anche diversi fra i due lati. Un prospetto
longitudinale richiudibile mostra pannelli, fine trave, posizione dell’appoggio e
seconda coppia terminale. È uno schema quotato non in scala. La scelta
Intermedio/Appoggio cambia la vista senza modificare i risultati delle fasi.
Le preferenze e tutti gli ingressi sono salvati nell’archivio.

Verifiche e diagnostica sono nello stesso gruppo inferiore già utilizzato per
taglio e pioli. Il Word riporta gli ingressi attivi, i prospetti, le resistenze,
gli indici, le ipotesi e gli esiti incompleti. Il riepilogo include anche questi
controlli: una buona verifica tensionale non nasconde un appoggio non verificato.

## Irrigidimenti intermedi e appoggi

Sono ammessi piatti bilaterali uguali, diversi oppure su un solo lato. Un elemento
monolaterale sposta il baricentro: il modello conserva questa eccentricità invece
di raddoppiare il piatto. La sezione resistente comprende i piatti e una striscia
di anima fino a 15εtw per lato, limitata simmetricamente dagli spazi disponibili.
Questo evita sovrapposizioni fra montanti e anima fittizia oltre la fine trave.

Il modello usa un’analisi elastica del secondo ordine con imperfezioni equivalenti
secondo EN 1993-1-1 §§5.2.2(7)a e 5.3.4. Considera entrambi i piani, eccentricità
reali e imperfezione Lcr/200 della curva c; non accredita riserve plastiche. Il
fattore Lcr/L è modificabile, inizialmente 1,00, nel campo 0,75–2,00. Occorrono
collegamento continuo all’anima e ritegni laterali alle flange. Le lunghezze di
vincolo devono corrispondere al dettaglio reale.

Si espongono area, baricentro, inerzie, Nst, carichi critici, momenti del secondo
ordine, tensione e freccia. I controlli comprendono anche rigidezza richiesta da
entrambi i pannelli adiacenti, rigidezza torsionale e snellezza locale dei piatti.
I piatti devono rientrare in classe 3; non vengono applicate riduzioni automatiche
per piatti di classe 4. I casi oltre il carico critico sono segnalati senza un
indice finito favorevole.

La compressione include l’azione del campo diagonale e l’eventuale forza esterna.
Per la deviazione delle tensioni normali si considera la compressione integrata
dell’anima e si assume cautelativamente σcr,c/σcr,p=1. Un irrigidimento intermedio
non idoneo non produce il beneficio del pannello corto. Se i pannelli sono diversi,
la resistenza a taglio usa cautelativamente il più lungo; la rigidezza è controllata
per entrambi.

L’appoggio ha una reazione SLU d’inviluppo da inserire esplicitamente: non viene
dedotta dal taglio della singola sezione. Si assegnano eccentricità trasversale e
longitudinale, impronta di carico, posizione interna/terminale e distanza dal bordo.
L’impronta deve coprire integralmente i piatti e rispettare i bordi. Si controllano
pressoflessione, trasferimento alla base e ingombro. Non è un dimensionamento
del dispositivo di appoggio né un modello di patch loading per anime non irrigidite.

Il montante terminale rigido è limitato a due coppie simmetriche uguali. Si controllano
interasse e, per ciascuna coppia, area richiesta da EN 1993-1-5 §9.3.1; si sommano
cautelativamente l’utilizzo per ancoraggio del campo diagonale e quello per reazione.
Entrambe le coppie sono verificate assumendo l’intera reazione. La curva favorevole
del montante rigido si attiva solo quando geometria, elementi e collegamenti risultano
idonei. In caso contrario resta la curva non rigida, con segnalazione esplicita.

Le saldature sono cordoni continui su entrambi i bordi dei piatti, all’anima e alle
flange. Il metodo semplificato EN 1993-1-8 §4.5.3.3 usa fu del materiale Model,
βw=1 cautelativo, γM2 unico e riduzione per giunti lunghi. Gola e lunghezza efficace
sono controllate. L’esclusione della verifica delle saldature lascia un esito
incompleto: non equivale all’approvazione del collegamento.

## Interazione N–M–V

Sotto 0,5 VRd non si applica la penalizzazione per alto taglio. Per N=0, fy≤355 MPa
e anima non interamente compressa resta l’interazione M–V con Mpl e Mf, descritta
nel documento principale. Negli altri casi si usa un criterio elastico
cautelativo con Mf=0:

`η = ηnormale + max(0, 2|V|/VRd − 1)² ≤ 1`.

ηnormale è l’inviluppo delle tensioni cumulative nei materiali rispetto ai limiti
SLU. È una scelta cautelativa dichiarata, non il dominio plastico esatto della
sezione sotto N. Può essere più onerosa. Non modifica tensioni, sezione efficace,
contributi o omogeneizzazione; usa i risultati già prodotti dalle fasi.
Il metodo di libreria `AtAxialForce` ha test indipendenti sul dominio plastico,
ma non viene impiegato per sostituire questo criterio nella vista.

## Soletta e connessione

L’armatura trasversale è distinta dalle due file longitudinali della sezione.
Ogni strato può essere assente. Le superfici a–a sono controllate sui due lati,
con ripartizione assegnabile del flusso; le superfici b–b comprendono ogni gruppo
contiguo di pioli uniformemente sollecitati. Si espongono flusso, lunghezza della
superficie, armatura presente/richiesta/minima e resistenza della biella compressa.

Il traliccio EC2 §6.2.4 non accredita la coesione del CLS. Il campo 1≤cotθ≤1,25
copre anche la soletta tesa. La richiesta di armatura comprende il minimo e
l’interazione con l’armatura richiesta dalla flessione trasversale, inserita
dall’utente. L’ancoraggio assume barre diritte sollecitate a fyd, senza riduzioni
favorevoli dei coefficienti α. Oltre a EC2 §8.4, in NTC si rispettano i minimi
di 20 diametri e 150 mm del §4.1.6.1.4. La lunghezza disponibile è quella minima
oltre tutte le superfici pertinenti, da entrambi i lati.

Le distanze ai bordi fisici della soletta sono indipendenti dalla larghezza efficace.
Vicino a un bordo si controllano il minimo di 6d e le forcine di diametro almeno
0,5d secondo EC4 §6.6.5.3. Le ipotesi sono soletta piena e pioli verticali;
non sono coperti sollevamento, splitting attraverso lo spessore, lamiere grecate
o distribuzioni non uniformi dei connettori.

La fatica usa qmin/qmax e i fattori di equivalenza e dinamico assegnati; non ricava
il ciclo dalle fasi costruttive. Si calcola Δτ equivalente a due milioni di cicli,
categoria 90 dei pioli. Per flangia tesa si assegna anche Δσ equivalente e si
verificano categoria 80 e interazione EC4 §6.8.7.2. Gli inviluppi devono includere
gli stati fessurati/non fessurati pertinenti del modello globale. Il controllo
geometrico d≤1,5tf rimane obbligatorio quando è attiva la verifica a fatica.

## Sorgenti e verifiche

- Metodi puri in Checker: `GPCChecker.Steel/CompositeBridges/BridgeLocalDetails.cs`
  e `BridgeBendingShear.cs`. ANTHEA compila gli stessi sorgenti tramite collegamento;
  non duplica le formule e non sostituisce il gruppo di DLL Checker distribuito.
- Adattatore, unità e dati: `X.Core/BridgeSection.Details.cs`,
  `BridgeSection.Shear.cs` e `BridgeSection.DetailReport.cs`.
- Test numerici: `Checker/GPCChecker.Test.BridgeAudit/BridgeLocalDetailsTests.cs`.
  La suite ordinaria passa 259 casi, escludendo esplicitamente `KnownBug` e
  `ConstructorRegression`: i difetti preesistenti del solver non sono stati corretti.
- Prove WPF: `supporto/test/Desktop/BridgeDetailsUiSmokeChecks.cs`, integrate
  nello smoke completo. Sono controllati modifiche, persistenza, campi condizionali,
  risultati, indipendenza dalle fasi, Word e proporzioni delle immagini.
- Output e schermate: `supporto/artefatti/ponte_dettagli/`.

L’invarianza delle tensioni, dei contributi e delle larghezze efficaci è verificata
attivando i dettagli locali. I file del ciclo di calcolo delle fasi, delle larghezze
efficaci e delle proprietà non sono stati modificati in questa revisione.

## Fonti primarie

- [NTC 2018, Gazzetta Ufficiale](https://www.gazzettaufficiale.it/eli/id/2018/2/20/18A00716/sg).
- [EN 1993-1-5:2006](https://www.phd.eng.br/wp-content/uploads/2015/12/en.1993.1.5.2006.pdf), §§5, 7 e 9.
- [EN 1993-1-1:2005](https://www.phd.eng.br/wp-content/uploads/2015/12/en.1993.1.1.2005.pdf), §§5.2.2 e 5.3.4.
- [EN 1993-1-8:2005](https://www.phd.eng.br/wp-content/uploads/2015/12/en.1993.1.8.2005-1.pdf), §§4.5 e 4.11.
- [EN 1994-2:2005](https://www.phd.eng.br/wp-content/uploads/2015/12/en.1994.2.2005.pdf), §§6.6 e 6.8.
- [EN 1992-1-1:2004](https://www.phd.eng.br/wp-content/uploads/2015/12/en.1992.1.1.2004.pdf), §§6.2.4, 8.4 e 9.2.2.

Le edizioni sono quelle dichiarate dal modulo; i coefficienti esposti non
costituiscono una scelta automatica dell’Appendice Nazionale.
