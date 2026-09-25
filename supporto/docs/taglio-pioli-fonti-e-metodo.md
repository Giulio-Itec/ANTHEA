# Sezione da ponte — taglio, irrigidimenti e connessione

Revisione del 25 settembre 2026. Metodi ricavati dai testi normativi, senza assumere corretto il codice precedente. Le edizioni sono quelle selezionabili nel modulo: NTC 2018, EN 1993-1-5:2006 con corrigendum, EN 1993-2:2006 ed EN 1994-2:2005 con corrigendum. Non si applicano implicitamente le edizioni di seconda generazione.

## Fonti primarie consultate

- [NTC 2018, allegato ufficiale in Gazzetta Ufficiale](https://www.gazzettaufficiale.it/atto/serie_generale/caricaArticolo?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.flagTipoArticolo=1&art.idArticolo=1&art.idGruppo=0&art.idSottoArticolo=1&art.idSottoArticolo1=10&art.progressivo=0&art.versione=1): capitoli 4 e 5, in particolare Tab. 4.2.VII, §§4.3.4.2.2 e 4.3.4.3.1–5.
- [EN 1993-1-5:2006, testo CEN riprodotto](https://www.phd.eng.br/wp-content/uploads/2015/12/en.1993.1.5.2006.pdf): §§5, 7.1, 9.1–9.4, allegato A.3.
- [EN 1993-2:2006, testo CEN riprodotto](https://www.phd.eng.br/wp-content/uploads/2015/12/en.1993.2.2006.pdf): §6.1, coefficienti per ponti.
- [EN 1994-2:2005, testo CEN riprodotto](https://www.phd.eng.br/wp-content/uploads/2015/12/en.1994.2.2005.pdf): §§6.2.2.2–5, 6.6.2.1, 6.6.3.1, 6.6.5, 6.8.1(3), 7.2.2.
- [Commentario JRC a EN 1993-1-5](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2021-12/EUR22898EN.pdf): riscontro del modello di irrigidimento e del caso numerico hw=2720, tw=18, a=8000 mm.

Le copie consultate e le estrazioni di controllo sono sotto `supporto/artefatti/ponte_taglio_pioli/fonti`. Queste ultime non sostituiscono la lettura delle formule grafiche del PDF.

## Scelte implementate e loro campo

**Taglio.** La soletta non contribuisce a V resistente. L’anima intera resiste al taglio anche quando la riduzione per tensioni normali ne rende inefficace una parte. Si calcolano kτ, τcr, λw, χw, Vpl,Rd e Vbw,Rd. VRd è il minore delle ultime due; si omette il contributo favorevole delle flange. Il limite plastico usa cautelativamente Av=hw·tw. Senza intermedi idonei si adotta il pannello lungo. La curva del montante terminale è non rigida salvo verifica positiva del dettaglio rigido a due coppie. Per anime snelle senza appoggi inseriti rimane una segnalazione di incompletezza.

**Coefficienti.** γM1 iniziale 1,10 sia NTC sia EC per ponti, γV=1,25, η=1,20. γM0 rimane 1,05 per NTC e 1,00 per EC. Sono esposti e modificabili in un unico punto. I valori EC sono raccomandati per l’edizione indicata, non una selezione automatica di tutte le Appendici Nazionali.

**Interazione N–M–V.** EN 1994-2 §6.2.2.4(3) rimanda, per classe 3/4, a EN 1993-1-5 §7.1 usando il momento totale e le capacità della sezione composta. Per N=0, fy≤355 MPa e anima non interamente compressa, Mpl è integrato con flange efficaci, anima intera e soletta compressa; Mf omette l’anima. Il calcestruzzo teso è nullo e le barre sono omesse cautelativamente nelle sole capacità di riferimento. L’integrazione dei blocchi rettangolari è esatta; le verifiche elastiche non sono sostituite da queste capacità plastiche. La soglia di applicazione è 0,5 VRd. Negli altri casi ad alto taglio si usa un inviluppo elastico cautelativo con Mf=0, esplicitato in [dettagli locali](irrigidimenti-appoggi-connessione.md#interazione-nmv). Non si attribuisce al criterio il significato di un dominio plastico esatto sotto N.

**Tensioni tangenziali.** Si espongono V/(hw·tw) e il massimo del campo elastico V·S/(I·tw) sulla sezione lorda omogeneizzata di ciascuna fase. La somma è algebrica. L’inviluppo √(max|σ|²+3 max|τ|²) è un controllo elastico aggiuntivo, non sostituisce l’instabilità o l’interazione. La colorazione della viewport è σ/limite delle sole tensioni normali.

**Irrigidimenti e appoggi.** Sono gestiti piatti mono/bilaterali anche diversi, pannelli adiacenti diversi, appoggi interni e terminali, reazione eccentrica, montante rigido a due coppie e saldature continue. Il metodo corrente usa pressoflessione elastica del secondo ordine con imperfezioni equivalenti, eccentricità reale e Lcr/L iniziale 1,00; sostituisce nell’adattatore la precedente ipotesi dei soli piatti simmetrici. Campo, formule e verifiche sono descritti in [irrigidimenti, appoggi e connessione](irrigidimenti-appoggi-connessione.md). Un irrigidimento non idoneo non aumenta VRd. Intagli e azioni dei traversi non assegnate restano fuori campo.

**Pioli.** Pioli a testa saldata uniformi, soletta piena ordinaria C20/25–C60/75, d=16–25 mm, h≥3d. Resistenza minima dei due meccanismi normativi, fu limitato a 500 MPa. La domanda elastica è q=Σ(Vi·Si/Ii+Δqi), PEd=|q|·passo/numero per fila. Il getto/solo acciaio non carica la connessione e non riceve un esito pioli. La relazione V·S/I assume proprietà costanti nel tratto e N costante lungo la trave; introduzioni locali di N e variazioni di sezione richiedono Δq separato. Ritiro uniforme: nessun V, ma Δq può introdurre gli effetti di estremità ottenuti da un modello longitudinale; non è deducibile dal solo stato di una sezione.

**Distinzione NTC/EC per S e I.** NTC §4.3.4.3.3: medesime proprietà della fase tensionale, inclusa l’esclusione della soletta. EC4 §6.6.2.1(2): soletta non fessurata, mantenendo la carpenteria efficace; la fase con soletta esclusa ha quindi un proprio φ/n per lo scorrimento se la verifica pioli è attiva. Nessuna commutazione nascosta a una geometria d’acciaio lorda. S e I sono riportati per fase.

**Pioli SLE.** 0,75 PRd è il limite sotto combinazione caratteristica, secondo §§6.8.1(3)/7.2.2 EC4. Il selettore quasi permanente non riceve un esito di tale controllo. Il modulo non genera combinazioni né moltiplica le azioni per γF.

**Dettagli.** Passi minimo longitudinale 5d e trasversale 2,5d; massimo longitudinale min(800 mm,4hc). Bordo libero flangia minimo 20 mm NTC, 25 mm EC. Testa almeno 1,5d e 0,4d; copriferro almeno quello assegnato, almeno 20 mm NTC e almeno quello delle barre superiori per EC. Intradosso testa 30 mm sopra l’armatura inferiore. Il limite d≤1,5tf è attivo inizialmente per azioni ripetute ed è obbligatorio con verifica a fatica; il limite statico 2,5tf è adottato cautelativamente anche sopra l’anima. Non si aumenta la classe della flangia grazie ai pioli. Sono aggiunte armature trasversali, superfici a–a/b–b, ancoraggio, bordi e fatica resistente dei pioli con interazione della flangia tesa. Restano fuori campo sollevamento, splitting attraverso lo spessore, gruppi non uniformi, mensole locali e lamiere grecate.

## Organizzazione e prove

- Metodi nuovi in Checker: `GPCChecker.Steel/CompositeBridges/BridgeShearConnection.cs`, `BridgeBendingShear.cs` e `BridgeLocalDetails.cs`. ANTHEA compila temporaneamente gli stessi sorgenti con `Compile Link` poiché i DLL distribuiti non espongono ancora queste classi. Nessuna modifica ai solver precedenti.
- Adattatore per le fasi e unità in ANTHEA `X.Core/BridgeSection.Shear.cs`.
- Test in Checker `GPCChecker.Test.BridgeAudit/BridgeShearConnectionTests.cs` e `BridgeLocalDetailsTests.cs`: valori indipendenti, transizioni, errori di ingresso, segni, fasi, proprietà efficaci, NTC/EC, ritiro, SLE, dettagli e fallback degli irrigidimenti. Suite ordinaria: 259 test passati, con esclusione esplicita dei difetti storici `KnownBug` e `ConstructorRegression`.
- Test della vista in ANTHEA `supporto/test/Desktop/BridgeShearUiSmokeChecks.cs`: aggiunte rapide, 11 fasi, modifiche e inattivazione, scelta dell’ultima situazione, eliminazione e riapertura, contouring e report. Le righe di input sono riconciliate senza svuotare la collezione WPF.

## Difetti del codice preesistente trovati nel confronto

Non corretti in questa attività:

1. `EN1993p11Checker.CalculateVbRd1`, circa riga 1061: nel ramo oltre la soglia di snellezza usa χ=0,83/η, mentre la Tab. 5.1 richiede la dipendenza da λw. Il ramo non rigido deve decrescere all’aumentare della snellezza. Questo può sovrastimare la resistenza di anime molto snelle.
2. `PanelsStability/PanelShearStability.cs`, circa righe 93–101: i rami a/h<1 e a/h>1 non includono l’uguaglianza; un pannello quadrato può lasciare kτ nullo. Quel sorgente è attualmente escluso tramite `#if NEVER`.

I nuovi test non usano questi metodi come riferimento. Restano inoltre aperti i difetti del solver lineare già documentati nella precedente revisione.
