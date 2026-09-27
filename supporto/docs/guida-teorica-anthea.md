# Guida teorica dei calcoli di ANTHEA

Modelli formule ipotesi ed esempi dei moduli disponibili

Edizione 1 del 26 settembre 2026

Questa guida descrive il comportamento dei motori di ANTHEA presenti nella versione del 26 settembre 2026. Spiega come i dati diventano geometrie, azioni, resistenze, tensioni e stime; chiarisce inoltre quali risultati appartengono a un modello semplificato e quali controlli richiedono informazioni ulteriori. Il manuale pratico separato illustra i comandi dell'interfaccia.

La distinzione fondamentale è fra il calcolo di una grandezza e la verifica completa di un problema progettuale. Una capacità assiale del palo non comprende automaticamente i cedimenti; un dominio resistente di sezione non comprende l'instabilità dell'elemento; un costo preliminare del ponte non equivale a un computo esecutivo. Le formule che seguono descrivono il campo effettivamente implementato. Coefficienti e correlazioni vanno scelti per la situazione analizzata, senza attribuire ai valori iniziali un'approvazione automatica del progetto.

## 1 Architettura del calcolo e convenzioni

### 1 1 Separazione fra modello e interfaccia

X.Calculations contiene i motori e le funzioni di calcolo indipendenti da WPF. X.Core gestisce archivi, integrazioni e report. X.Desktop presenta editor e grafici; X.Materiali presenta le schede dei materiali. I motori di sezione CA e composta utilizzano anche le librerie Model e Checker distribuite con il progetto. Il disegno non è il modello resistente: la discretizzazione della vista può essere diversa da quella usata per l'equilibrio.

Bridge Design utilizza un proprio motore puro, BridgeConcept.Calculate. Riceve input, listino e ipotesi e restituisce un risultato con geometria, quantità, dettagli, diagrammi e avvisi. Lo stesso risultato alimenta interfaccia e report. Il motore non modifica gli input: un parametro impostato a zero per richiedere la scelta automatica resta zero nell'archivio, mentre il valore adottato è riportato nel risultato.

I calcoli dei report di progetto vengono eseguiti su copie dei dati, evitando di cambiare i fogli aperti. La gestione delle revisioni congela input e contesto necessario; non congela un eseguibile storico. Ricalcolare un vecchio archivio con un motore aggiornato può quindi produrre risultati diversi, che devono essere identificati con versione e data.

### 1 2 Unità e segni

| Ambito | Lunghezze | Tensioni e resistenze | Azioni | Convenzione da ricordare |
| --- | --- | --- | --- | --- |
| Geotecnica verticale | m | kPa | kN | Compressione e trazione trattate in rami distinti |
| Broms | m per il terreno; mm per la sezione | kPa nel terreno; MPa nei materiali | kN e kNm | N positivo a compressione |
| Sezione CA | mm | MPa | kN e kNm | N negativo a compressione; trazione positiva |
| Sezione composta | mm | MPa | kN e kNm | N negativo a compressione nelle fasi; appoggi con convenzione propria |
| Bridge Design | m salvo spessori metallici in mm | MPa e kPa secondo etichetta | kN e kNm | Carichi e momenti dell'intero impalcato |

Vale 1 MPa = 1 N/mm² = 1000 kPa. Per trasformare Nmm in kNm si divide per un milione; per trasformare N in kN si divide per mille. Per passare da una curvatura espressa in 1/mm al valore numerico in 1/m si moltiplica per mille. Una deformazione di 0,001 equivale a 1 per mille e a 1000 microdeformazioni.

Le convenzioni del segno non sono intercambiabili fra i moduli. I colori dei diagrammi non costituiscono una convenzione universale dell'applicazione: leggere la legenda. Per confrontare due solver occorre allineare anche origine degli assi, verso dei momenti, punto di applicazione di N e trasformazione delle coordinate.

### 1 3 Dati mancanti e validità del risultato

La validazione degli input impedisce di utilizzare numeri non finiti, geometrie incompatibili e parametri fuori campo nelle procedure che li richiedono. Un valore mostrato con pochi decimali è una presentazione: calcolo ed esportazioni conservano precisione maggiore. L'arrotondamento grafico non deve essere usato per ricostruire un equilibrio con tolleranze molto strette.

I risultati precedenti eventualmente conservati a video sono marcati da aggiornare dopo una modifica. Un risultato incompleto o fuori campo non viene convertito in esito favorevole. La disponibilità va letta per ogni verifica: un equilibrio tensionale può riuscire mentre fessurazione, dettagli o instabilità restano non valutabili.

## 2 Calcestruzzo armature e copriferro

### 2 1 Resistenze e diagrammi

Per il calcestruzzo il passaggio dalla resistenza caratteristica a quella di progetto segue il coefficiente αcc e il parziale γc selezionati. Per l'armatura la resistenza di progetto deriva da fy e γs. I diagrammi possono essere di catalogo o personalizzati; l'uso di una classe nominale non autorizza a ignorare il diagramma effettivamente selezionato.

$$ fcd = αcc fck / γc
$$ fyd = fyk / γs
$$ εyd = fyd / Es

Con fck = 30 MPa, αcc = 0,85 e γc = 1,50 si ottiene fcd = 17 MPa. Con fyk = 450 MPa e γs = 1,15 si ottiene fyd = 391,30 MPa. Assumendo Es = 200000 MPa, la deformazione elastica a fyd è 0,0019565, cioè 1,9565 per mille. Sono esempi dei passaggi algebrici, non una scelta universale dei coefficienti.

Il catalogo delle armature comprende classi attuali e storiche. I dati nominali delle classi storiche non sostituiscono prove e valutazioni di un materiale esistente. A5 e deformazione ultima del diagramma hanno definizioni diverse. Quando il motore richiede una deformazione ultima, essa deve provenire da una scelta documentata e coerente con il modello costitutivo.

### 2 2 Esposizioni e requisiti del materiale

Il catalogo della durabilità comprende 18 esposizioni. Le prescrizioni combinate vengono ricavate assumendo il massimo dei requisiti minimi di resistenza, il minimo dei rapporti acqua cemento massimi e il massimo dei contenuti minimi di cemento pertinenti. Le tabelle di composizione implementate fanno riferimento al prospetto documentato nel repository, derivato dal materiale ATECAP 2020 relativo a UNI 11104; non costituiscono una selezione automatica delle edizioni normative eventualmente successive.

| Esposizione | Classe minima nel prospetto implementato |
| --- | --- |
| X0 | C12/15 |
| XC1 e XC2 | C25/30 |
| XC3 | C30/37 |
| XC4 | C32/40 |
| XD1 | C30/37 |
| XD2 | C32/40 |
| XD3 | C35/45 |
| XS1 | C32/40 |
| XS2 e XS3 | C35/45 |
| XF1 | C32/40 |
| XF2 e XF3 | C25/30 |
| XF4 | C30/37 |
| XA1 | C30/37 |
| XA2 | C32/40 |
| XA3 | C35/45 |

L'aria inglobata per XF2, XF3 e XF4 dipende anche dalla dimensione dell'aggregato. L'implementazione documenta il 4% per dimensioni superiori a 20 mm e il 5% nell'intervallo 12–16 mm; gli altri intervalli non devono essere interpolati tacitamente. La proposta Cl 0,4 per armatura ordinaria non deriva semplicemente dal massimo codice di esposizione. Il prospetto aiuta a formulare requisiti, senza calcolare una miscela industriale completa.

### 2 3 Copriferro minimo e nominale

Il minimo deve soddisfare aderenza, durabilità e condizioni aggiuntive. Il nominale comprende il margine esecutivo e gli eventuali limiti per la superficie di getto. Nel ramo implementato, il requisito di aderenza parte dal diametro e aumenta di 5 mm quando l'aggregato supera 32 mm. Rugosità e abrasione aggiungono i contributi assegnati.

$$ cmin = max(10; cbond; cdur) + crugosità + cabrasione
$$ cnom = max(cmin + Δcdev; ccontroterra)

Il ramo EC2 determina la classe strutturale secondo esposizione, vita e opzioni ammesse, poi consulta la tabella di durabilità. Le esposizioni che non definiscono da sole quel requisito richiedono un'associazione pertinente, anziché essere trasformate in una classe equivalente arbitraria.

Il ramo NTC utilizza tre livelli di severità. Il valore tabellare di base è (15 mm per elemento a piastra, 20 mm negli altri casi) più 10 mm per livello di severità; si aggiungono 5 mm se fck è inferiore a C0 = 35 + 5 × severità. La vita di 100 anni aggiunge 10 mm, una resistenza sotto il minimo pertinente aggiunge 5 mm e l'opzione di qualità del copriferro riduce di 5 mm. Il minimo pertinente può essere assegnato nel campo ammesso dal motore e non coincide necessariamente con tutte le prescrizioni di composizione.

Come esempio del ramo NTC, XF2, fck = 30 MPa, elemento non a piastra, vita 50 anni, barra 16 mm, aggregato 20 mm e Δcdev = 10 mm danno 45 mm nominali senza riduzione di qualità. Se si passa a vita 100 anni, lo stesso caso dà 55 mm. I valori sono esempi riproducibili del codice NtcCover, utili per controllare l'input.

## 3 Palo verticale

### 3 1 Geometria e tensioni geostatiche

Per un palo circolare di diametro D si usano area di base Ab e perimetro u. La stratigrafia è integrata per tratte; la falda può suddividere una stessa tratta in una parte asciutta e una immersa. Sotto falda la tensione efficace cresce con il peso sommerso, mentre la tensione totale continua a usare il peso saturo.

$$ Ab = π D² / 4
$$ u = π D
$$ γ′ = max(0; γsat − 9,81)
$$ σ′v(z) = ∫ γ′(z) dz

La media della tensione efficace in una tratta è ottenuta integrando il profilo effettivo, compreso il cambio di pendenza in corrispondenza della falda. Usare la sola tensione al centro dello strato può dare un valore diverso se il tratto attraversa quella discontinuità. Il peso del terreno di uno strato continua a influire sugli strati sottostanti anche quando la sua resistenza laterale viene esclusa.

### 3 2 Laterale drenato

La tensione tangenziale limite drenata è espressa in funzione di coesione efficace, coefficiente K, attrito palo terreno μ e tensione verticale efficace media. Negli strati granulari il termine c′ viene posto a zero. Negli strati coesivi rimane disponibile nel ramo drenato.

$$ τs = c′ + K μ σ′v
$$ Rs = Σ π D Lj τsj

| Tecnologia | K sciolto | K denso | μ adottato |
| --- | --- | --- | --- |
| Profilato di acciaio battuto | 0,7 | 1,0 | tan 20° |
| Tubo chiuso battuto | 1,0 | 2,0 | tan 20° |
| Calcestruzzo prefabbricato | 1,0 | 2,0 | tan 0,75φ |
| Calcestruzzo gettato in opera | 1,0 | 3,0 | tan φ |
| Trivellato | 0,5 | 0,4 | tan φ |
| Elica continua | 0,7 | 0,9 | tan φ |

La tabella descrive i valori presenti nel programma, inclusa la particolare coppia del trivellato. Sono correlazioni del modello, non parametri misurati automaticamente. La scelta di un valore manuale richiede di mantenerne traccia nel foglio e nel report.

### 3 3 Laterale non drenato

Negli strati coesivi interessati dal ramo non drenato sotto falda si impiega τs = α Cu. Per pali battuti α vale 1 fino a Cu = 25 kPa, decresce secondo 1 − 0,0111(Cu − 25) fra 25 e 70 kPa e vale 0,5 da 70 kPa. Per le altre tecnologie vale 0,7, poi 0,7 − 0,008(Cu − 25), poi 0,35 negli stessi intervalli. Queste sono le espressioni a tratti effettivamente implementate.

I tratti sopra falda che non soddisfano le condizioni del ramo non drenato riutilizzano il calcolo drenato; gli strati granulari conservano la propria formulazione. La curva denominata non drenata può quindi essere composta da contributi calcolati con rami diversi. Occorre leggere la stratigrafia e non attribuire Cu a tutto il profilo.

### 3 4 Resistenza di punta e abaco Nq

Nel ramo drenato la base utilizza Ab σ′v Nq. Nel ramo non drenato coesivo sotto falda si usa una formulazione lorda Ab (Nc Cu + σv), con tensione verticale totale. Il contributo di sovraccarico non va quindi sottratto di nuovo senza considerare come sono state definite le azioni.

$$ Rb drenata = Ab σ′v Nq
$$ Rb non drenata = Ab (Nc Cu + σv)

Nq proviene dall'abaco parametrizzato NQ 2026 09 09 e non dalla sola formula esponenziale classica della capacità portante. Per D ≤ 0,8 m sono disponibili le curve z/D = 5, 10, 20 e 50. Ciascuna è descritta dalle coppie di angoli alle quali Nq vale 10 e 100: (23; 35,6), (24,6; 37), (25,8; 37,8), (27,5; 38,8) gradi.

$$ log10(Nq) = 1 + (φ − φ10) / (φ100 − φ10)

Fra le curve si interpola nella scala logaritmica del rapporto z/D e di Nq. Per D > 0,8 m si usano le curve per z/D = 4 e 32, definite da polinomi cubici raccordati, con interpolazione aritmetica fra esse e φ limitato al campo 26–42°. I valori esterni vengono ricondotti al bordo e segnalati; non costituiscono un'estrapolazione validata.

### 3 5 Più indagini e coefficienti

Si calcolano media e minimo separati di laterale e punta alla quota considerata. Il minimo della somma delle componenti non è necessariamente la somma dei minimi: ANTHEA adotta la seconda costruzione nel ramo definito dalle componenti minime. Questa scelta va distinta dal risultato di un singolo sondaggio.

$$ Rd,C = ηC min[(Rs,medio/γs + Rb,medio/γb)/ξ3;
$$                    (Rs,min/γs + Rb,min/γb)/ξ4]
$$ Rd,T = ηT min[Rs,medio/(ξ3 γt); Rs,min/(ξ4 γt)]

| Numero di indagini selezionato | ξ3 | ξ4 |
| --- | --- | --- |
| 1 | 1,70 | 1,70 |
| 2 | 1,65 | 1,55 |
| 3 | 1,60 | 1,48 |
| 4 | 1,55 | 1,42 |
| 5 | 1,50 | 1,34 |
| 7 | 1,45 | 1,28 |
| Almeno 10 | 1,40 | 1,21 |

L'azione di compressione comprende il peso proprio sfavorevole e quella di trazione è ridotta dal peso favorevole, con i coefficienti specificati. L'opzione di sottospinta modifica il peso secondo la parte immersa del palo, separatamente dal calcolo delle tensioni del terreno.

$$ Ed,C = NC + γG,sfav W
$$ Ed,T = max(0; NT − γG,fav W)

### 3 6 Efficienza di gruppo e limiti

Converse Labarre usa la geometria del reticolo nelle due direzioni. Gli angoli della seguente espressione sono in gradi; ciascun termine è nullo quando in quella direzione esiste un solo palo.

$$ η = 1 − [atan(D/sx)/90] (nx−1)/nx
$$         − [atan(D/sy)/90] (ny−1)/ny

Feld conta le coppie adiacenti ortogonali e diagonali: P = (nx−1)ny + nx(ny−1) + 2(nx−1)(ny−1), poi η = 1 − 2P/(16 nx ny). Per questi due metodi la versione corrente applica la stessa efficienza a compressione e trazione. L'opzione manuale permette valori distinti. Un risultato non positivo viene rifiutato.

La profondità analizzabile deve essere coperta dalle stratigrafie necessarie. Sono fuori dal modello automatico cedimenti, attrito negativo, resistenza del blocco di gruppo e interazione completa terreno struttura. Il rapporto Ed/Rd riguarda la resistenza assiale considerata e non esprime da solo la prestazione di esercizio.

## 4 Micropalo verticale

### 4 1 Correlazione del bulbo

La resistenza laterale usa curve digitalizzate Bustamante Doix documentate nel materiale di riferimento di Viggiani, sezione 13.1.6. Le famiglie SG, AL, MC e R e le curve 1 IRS e 2 IGU individuano la correlazione applicabile. La grandezza p_l, in MPa, viene interpolata linearmente fra i punti della curva; l'ordinata viene convertita in kPa. Il codice rifiuta valori esterni al campo disponibile. Il campo storico dell'interfaccia è «Pressione p_i = p_l»: l'uguaglianza è un'assunzione dell'integrazione e non dimostra che la pressione della pompa coincida fisicamente con il parametro geotecnico dell'abaco.

Il diametro del bulbo viene stimato come Ds = α D, dove D è il diametro di perforazione. La resistenza di ogni tratta attiva vale π Ds Lj τj. α rappresenta l'espansione convenzionale e va scelto in relazione a terreno e iniezione; non è un incremento di resistenza indipendente dalla geometria.

$$ Ds = α D
$$ Rs = Σ π Ds Lj τj

Per esempio, D = 0,20 m, α = 1,30, lunghezza attiva 8 m e τ = 150 kPa producono Ds = 0,26 m e Rs = 980,18 kN prima dei coefficienti. Aumentare α del 10% aumenta linearmente la superficie resistente, a parità delle altre ipotesi. Il risultato resta condizionato alla validità della correlazione e della realizzazione del bulbo.

### 4 2 Inclinazione punta e peso

Per un'inclinazione θ dalla verticale, una differenza di quota Δz corrisponde a una tratta lungo l'asse Δs = Δz/cos θ. La resistenza viene sommata lungo quell'asse. Non deve essere letta direttamente come componente verticale di una capacità di gruppo senza risolvere la geometria delle azioni.

La punta opzionale è una frazione assegnata del laterale; non deriva da una capacità portante indipendente. Per il peso si separano area del tubo e area di boiacca. Il diametro del tubo è distinto da quello del bulbo. La componente adottata nella procedura segue il fattore cos θ previsto dal modello.

$$ As,tubo = π (De² − Di²) / 4
$$ q = 9,81 × 7850 As,tubo / 1000 + γboiacca Aboiacca
$$ W = q s cos θ

Diametri e aree devono essere riportati in metri e metri quadrati nella formula del peso. Media, minimi, ξ, γ ed efficienze seguono la struttura del calcolo verticale. Instabilità del tubo, flessione, sfilamento del collegamento e trasferimento locale fra tubo e boiacca restano verifiche distinte.

## 5 Capacità orizzontale con Broms

### 5 1 Idealizzazione e pressione limite

Il palo viene analizzato attraverso meccanismi limite compatibili con la resistenza del terreno e un momento plastico My assegnato. Non si introduce una legge di spostamento delle molle; il risultato principale è il carico limite H. Il terreno omogeneo costituisce il riferimento più diretto, mentre stratificazioni della stessa famiglia e falda sono estensioni sperimentali della procedura integrale.

Nei coesivi la resistenza laterale per unità di lunghezza è nulla nei primi 1,5D dal piano campagna e vale poi 9CuD. Il tratto escluso non ricomincia a ogni cambio di strato. Nei granulari la resistenza è 3KpDσ′v. Le unità di p sono kN/m.

$$ p(z) = 9 Cu D per z ≥ 1,5D nei coesivi
$$ p(z) = 3 Kp D σ′v(z) nei granulari
$$ Kp = (1 + sin φ) / (1 − sin φ)

Si definiscono Q come integrale delle forze, S come integrale dei momenti rispetto al piano campagna e A come momento rispetto alla sezione alla profondità z. Queste funzioni permettono di formulare in modo uniforme equilibrio e criteri limite.

$$ Q(z) = ∫₀ᶻ p(s) ds
$$ S(z) = ∫₀ᶻ s p(s) ds
$$ A(z) = z Q(z) − S(z)
$$ V(z) = H − Q(z)
$$ M(z) = M0 + H z − A(z)

Con testa libera M0 = He. La profondità zf del massimo momento soddisfa Q(zf) = H. Per il terreno coesivo omogeneo zf = 1,5D + H/(9CuD). I rami vengono confrontati in base a equilibrio e capacità, non soltanto mediante una soglia geometrica L/D.

### 5 2 Meccanismi granulari

Per la testa libera e il palo corto H = A(L)/(L+e). Nel ramo lungo il momento massimo raggiunge My: He + S(zf) = My. Per testa impedita il ramo corto ha H = Q(L), M0 = −S(L), ed è ammissibile quando il momento alla testa non supera My.

Nel ramo intermedio impedito H = [My + A(L)]/L con momento in testa −My. Nel ramo lungo impedito la condizione è S(zf) = 2My. L'equilibrio può richiedere una forza concentrata al piede F = Q(L) − H nei regimi corti e intermedi. Tale forza spiega il salto del taglio e non deve essere rimossa per rendere il diagramma visivamente continuo.

Per un palo lungo, la porzione mobilitata e l'eventuale chiusura del diagramma oltre il massimo derivano dall'idealizzazione del meccanismo; non costituiscono una distribuzione elastica univoca delle reazioni del terreno.

### 5 3 Meccanismi coesivi

La coppia resistente inferiore C fra zf e t è ottenuta dividendo la distribuzione al punto che ne equilibra le risultanti. Nel coesivo omogeneo vale 9CuD(t−zf)²/4. Più in generale, individuato b in modo che Q(b) sia la media di Q(zf) e Q(t), si usa C = S(t) + S(zf) − 2S(b).

Per testa libera corta si impone He + S(zf) = C(zf,L); per il ramo lungo He + S(zf) = My e C(zf,t) = My con t ≤ L. Con testa impedita, nel ramo intermedio −My + S(zf) = C(zf,L), mentre nel lungo S(zf) = 2My. Il ramo corto impedito resta H = Q(L) con controllo del momento alla testa.

La ricerca numerica usa bisezione e verifiche dell'equilibrio. La tolleranza della radice è dell'ordine di 10⁻⁸ con massimo 100 iterazioni; i residui di equilibrio sono controllati rispetto alle scale delle azioni. Il regime governante è quello ammissibile con capacità minore.

### 5 4 Da Hu alla resistenza di progetto

Le capacità dei sondaggi vengono combinate tramite media e minimo e i fattori ξ selezionati. La riduzione di gruppo e il coefficiente di resistenza vengono applicati successivamente. Nel percorso implementato il parziale di resistenza laterale vale 1,3.

$$ Rk = min(Hu,medio/ξ3; Hu,min/ξ4)
$$ Rd = η Rk / 1,3

HEd è già un'azione di progetto assegnata. Il motore non genera l'intero percorso delle combinazioni normative. L'efficienza di gruppo basata su Reese e Van Impe usa fattori direzionali: davanti min[1; 0,7(s/D)^0,26], dietro min[1; 0,48(s/D)^0,38], lateralmente min[1; 0,64(s/D)^0,34]. I contributi diagonali combinano i fattori longitudinali e laterali secondo l'angolo; il prodotto dei vicini attivi dà η. Questa riduzione agisce su Rd, senza ridisegnare il meccanismo Hu del singolo palo.

### 5 5 Momento resistente del palo in c a

La procedura automatica considera una sezione circolare con 4–512 barre uniformemente distribuite. La distanza dell'asse delle barre dal bordo comprende copriferro, diametro della staffa e mezzo diametro longitudinale. N è positivo a compressione in questo motore.

Si assume conservazione delle sezioni piane, calcestruzzo compresso a parabola rettangolo senza contributo teso e acciaio elastico perfettamente plastico. Si sottrae il calcestruzzo sostituito dalle barre. L'equilibrio assiale viene ricercato nel campo con asse neutro interno, 0 < x < D. Non è quindi una procedura generale per qualsiasi stato assiale, inclusi i campi interamente tesi o compressi.

Due integrazioni, 28 × 96 e 56 × 192, vengono confrontate. Se lo scarto supera il 2% il momento automatico non è accettato. Un My manuale può essere utilizzato con provenienza esplicita, ma il meccanismo di Broms non dimostra la duttilità necessaria a sviluppare la cerniera.

### 5 6 Tubo CHS del micropalo

La sezione resistente è il solo tubo; la boiacca non contribuisce. Le formule geometriche sono esatte per l'anello, con De e Di in millimetri. Il diametro geotecnico usato in p(z) resta un parametro separato.

$$ A = π (De² − Di²) / 4
$$ I = π (De⁴ − Di⁴) / 64
$$ Wel = 2I / De
$$ Wpl = (De³ − Di³) / 6
$$ Npl = A fy / γM0
$$ Mpl = Wpl fy / γM0
$$ My(N) = Mpl [1 − |N|/Npl]

L'ultimo rapporto va valutato con N e Npl nelle stesse unità; i risultati di forza e momento sono convertiti in kN e kNm. Il ramo automatico richiede classe 1 secondo De/t ≤ 50ε², con ε² = 235/fy. Le soglie 70ε² e 90ε² identificano i campi successivi, senza abilitarli come sezione plastica automatica. La relazione lineare N M è la semplificazione adottata; non verifica instabilità globale, fatica o ovalizzazione locale.

## 6 Sezione in calcestruzzo armato

### 6 1 Geometria e deformazioni piane

Sono disponibili sezioni rettangolari, circolari e a T, con fori centrali per le forme supportate. Il contorno circolare è discretizzato mediante un poligono con numero di lati configurabile. Le aree dei vuoti sono escluse e le barre incompatibili con il contorno o interne ai vuoti vengono rifiutate.

L'ipotesi cinematica è una distribuzione piana delle deformazioni. Indicando con ε0 la deformazione al riferimento e con kx e ky i gradienti, la deformazione in un punto è una funzione affine delle coordinate. Le convenzioni dei gradienti e dei momenti sono trasformate nel sistema di riferimento esposto dal foglio.

$$ ε(x,y) = ε0 + kx x + ky y
$$ N = ∫Ac σc dA + Σ As σs + Σ Ap σp

I momenti derivano dagli integrali delle tensioni moltiplicate per i rispettivi bracci, nel verso degli assi del solver. Le barre e i tendini sono contributi discreti; il calcestruzzo è integrato sulla regione resistente. Il piano di deformazione richiesto dalle azioni e il piano al limite resistente sono risultati differenti e vengono esposti separatamente.

### 6 2 Equilibrio e domini

Per un dato stato N Mx My, il motore cerca le deformazioni che producono le risultanti assegnate, entro i limiti dei materiali. I domini resistenti sono ricavati esplorando stati limite delle deformazioni e integrando le corrispondenti tensioni. Il campionamento della superficie usato per la visualizzazione non sostituisce la procedura diretta di verifica richiamata dalle librerie.

Il criterio di crescita delle azioni determina la direzione della ricerca sul dominio. Con N costante si aumenta la componente flettente lungo la direzione scelta; con eccentricità costante si percorre una direzione differente nello spazio delle azioni. Un coefficiente di sfruttamento non è quindi interpretabile senza il criterio associato.

I quattro momenti resistenti rapidi, a N assegnato, corrispondono alle direzioni positive e negative dei due assi. Sono quattro interrogazioni del motore, non un rettangolo resistente dentro cui qualunque coppia di momenti sia ammessa. La proiezione 2D di un punto fuori piano può apparire interna pur non risolvendo il problema tridimensionale.

### 6 3 Materiali tendini e analisi di esercizio

La risposta dipende dai diagrammi selezionati per cls, barre e tendini. Nelle leggi senza resistenza a trazione, il calcestruzzo teso non contribuisce all'equilibrio. Nelle analisi elastiche si distinguono le proprietà del modello tensionale da quelle impiegate successivamente nelle formule di fessurazione.

Per un gruppo di n fili o trefoli con area assegnata, l'eventuale diametro equivalente rappresenta la somma delle aree; a parità di diametro elementare cresce con √n. La tensione iniziale del tendine è un input efficace. Il motore non ricostruisce in modo generale attrito, rientro degli ancoraggi, rilassamento e tutte le perdite differite della struttura.

Le verifiche di tensione di esercizio utilizzano le combinazioni già assegnate e i limiti disponibili nel modulo. La scelta Rara, Frequente o Quasi permanente non genera le combinazioni. In alcuni percorsi la verifica di decompressione o di formazione delle fessure usa uno stato ausiliario interamente reagente: tale stato non va confuso con quello fessurato usato per le tensioni delle barre.

### 6 4 Apertura delle fessure

La formula implementata parte da tensione delle barre efficaci, modulo dell'acciaio, Ecm, resistenza media a trazione e rapporto fra armatura e area efficace di calcestruzzo. αe usa Es/Ecm e può differire dal coefficiente di omogeneizzazione con viscosità impiegato per l'analisi tensionale. Nel codice attuale fct,eff coincide con fctm, senza riduzione automatica per l'età di fessurazione.

$$ ρeff = As,eff / Ac,eff
$$ αe = Es / Ecm
$$ Δσ = kt fctm (1 + αe ρeff) / ρeff
$$ Δε = max[(σs − Δσ)/Es; 0,60 σs/Es]

kt vale 0,60 per breve durata e 0,40 per lunga durata. k1 vale 0,80 per barre ad aderenza migliorata e 1,60 per barre lisce. Nel ramo ordinario k2 vale 0,50 quando almeno una barra ordinaria è compressa, altrimenti 1,00; una barra a tensione esattamente nulla non è compressa. Nel ramo di sezione interamente tesa si impiega invece la distribuzione di deformazioni: k2 = (εmax + εmin)/(2εmax), limitato fra 0,5 e 1.

Il codice conserva una rappresentazione tramite distanza media Δsm e fattore finale 1,70. Per le barre ravvicinate questo conduce allo stesso prodotto scritto direttamente con il termine 3,4c + 0,425 k1 k2 φeq/ρeff. Per le barre distanziate viene confrontata anche la regione distante dalle armature.

$$ Δsm,vicino = [3,4c + 0,425 k1 k2 φeq/ρeff] / 1,70
$$ slim = 5(c + φeq/2)
$$ Δsm,distante = 0,75(h − x)
$$ wk = max[0; 1,70 Δsm Δε]

Se s ≤ slim si usa Δsm,vicino; altrimenti si usa il massimo fra le due distanze. Questo dettaglio è rilevante: sostituire il ramo distante con un coefficiente arrotondato ricordato da un'altra formulazione non riproduce esattamente il codice. σs è il massimo delle barre efficaci, non la media delle tensioni di tutte le barre.

L'area efficace viene costruita sulla zona tesa e sulle barre pertinenti. Nella trazione integrale sono esaminate regioni di bordo o radiali e governa il risultato massimo; non si sommano le aree sovrapposte come se fossero indipendenti. Disposizioni e superfici interne non supportate restano fuori campo. La disponibilità della formula va verificata anche rispetto al metodo tensionale e alla presenza di tendini.

### 6 5 Esempio di fessurazione

Assumere σs = 200 MPa, Es = 200000 MPa, Ecm = 33000 MPa, fctm = 2,9 MPa, ρeff = 0,02, φeq = 16 mm, c = 30 mm, k1 = 0,8, k2 = 0,5 e lunga durata. Si ottiene αe = 6,0606 e Δσ = 65,03 MPa. La deformazione calcolata è 0,0006748, maggiore del minimo 0,0006000.

Il termine vicino vale (102 + 136)/1,7 = 140 mm. La soglia di interasse è 190 mm. Con s = 150 mm, wk = 1,7 × 140 × 0,0006748 = 0,1606 mm. Se s supera 190 mm e h−x = 300 mm, il termine distante vale 225 mm e wk aumenta a 0,2581 mm. Cambiano quindi apertura e ramo governante, pur conservando σs e area efficace nell'esempio.

### 6 6 Taglio

Senza armatura trasversale, e nel campo ammesso, il motore confronta il termine proporzionale alla radice cubica di 100ρfck con il minimo basato su √fck. k è limitato a 2, ρ a 0,02 e la compressione media a 0,2fcd. La compressione media è positiva benché N nel foglio sia negativo a compressione.

$$ k = min[2; 1 + √(200/d)]
$$ ρ = min[0,02; Asl/(bw d)]
$$ σcp = min[−1000N/Ac; 0,2fcd]
$$ VRd = max[0,18 k ∛(100ρfck)/γc + 0,15σcp;
$$                0,035 k^1,5 √fck + 0,15σcp] bw d /1000

Per N di trazione il ramo senza staffe non fornisce automaticamente una resistenza favorevole. Con staffe il modello a traliccio confronta resistenza dell'armatura e del puntone; cot θ è compreso fra 1 e 2,5 e può essere assegnato o determinato dalla procedura automatica.

$$ VRsd = z (Asw/s) fyd (cot α + cot θ) sin α /1000
$$ VRcd = z bw αc 0,5fcd (cot α + cot θ)/(1+cot²θ) /1000
$$ VRd = min(VRsd; VRcd)

αc dipende dalla compressione: 1 + σcp/fcd fino a 0,25fcd, 1,25 fino a 0,50fcd e max[0; 2,5(1−σcp/fcd)] oltre tale soglia. Il braccio z deriva dal fattore della geometria. Per il ramo circolare dei pali si adottano 0,75d nella sezione piena e 0,60d nella cava, con bw e d ricavati dalla geometria e dalle barre. Si tratta del ramo specifico documentato, non di un'estensione indistinta a qualunque elemento circolare.

### 6 7 Torsione e interazione

La sezione resistente a torsione è un circuito periferico chiuso con area Ak, perimetro uk e spessore efficace t. La geometria automatica è disponibile per rettangolo e cerchio, pieni o con foro compatibile. Sono richieste staffe chiuse e armatura periferica adeguata; lo spessore deve contenerne gli assi.

$$ TRcd = 2 Ak t 0,5fcd cot θ/(1+cot²θ) /10⁶
$$ TRsd = 2 Ak (Asw/s) fyd cot θ /10⁶
$$ TRld = 2 Ak (Asl,disp/uk) fyd/cot θ /10⁶
$$ TRd = min(TRcd; TRsd; TRld)

Asl,disp è l'armatura longitudinale disponibile per la torsione dopo la flessione, da assegnare consapevolmente. Taglio e torsione devono usare lo stesso cot θ. Il programma controlla anche la somma |T|/TRcd + |Vx|/VRcd,x + |Vy|/VRcd,y per il calcestruzzo e |T|/TRsd + max(|Vx|/VRsd,x; |Vy|/VRsd,y) per le staffe. L'estensione a due componenti di taglio è una combinazione conservativa del modello, non un dominio normativo generale ricostruito in ogni dettaglio.

### 6 8 Dettagli e curva momento curvatura

Le verifiche costruttive dipendono dal tipo di elemento scelto. Il modulo confronta geometria, armature e parametri necessari con i limiti implementati e mantiene «Da completare» quando mancano informazioni. La lunghezza di ancoraggio rettilineo parte da lbrqd = φσsd/(4fbd), con i coefficienti di forma e condizioni α1–α5 assunti unitari nel ramo documentato. Le sovrapposizioni introducono α6 e i relativi minimi; i dettagli speciali non vengono ricavati da un disegno ideale della sezione.

La curva M χ viene costruita a N fissato nella direzione assegnata, con passi uniformi o quadratici fino al limite resistente. Ogni punto richiede equilibrio di sezione. In una sezione asimmetrica la direzione del gradiente di deformazione può non coincidere con quella del momento. Non sono inclusi automaticamente softening strutturale, lunghezza della cerniera, rotazione globale o interazione con instabilità dell'elemento.

## 7 Sezione composta da ponte

### 7 1 Geometria e omogeneizzazione

Il modello rappresenta una sezione locale composta da H saldato, soletta, fino a due piattabande inferiori e barre opzionali. Le piastre aggiunte rimangono elementi reali, con posizione e geometria proprie. La larghezza efficace beff della soletta è un dato esterno. La piena collaborazione è assunta nel calcolo N Mx; lo scorrimento non viene introdotto come un grado di libertà del solver di sezione.

Nel metodo elastico si trasforma il contributo del calcestruzzo mediante n = Ea/Ec,eff. La relazione implementata per gli effetti differiti è n = n0(1+ψLφ), con n0 = Ea/Ecm. L'inversione permette di assegnare direttamente n. È richiesto n ≥ n0.

$$ n0 = Ea / Ecm
$$ n = n0 (1 + ψL φ)
$$ φ = (n/n0 − 1)/ψL

Il coefficiente ψL distingue la natura dell'effetto: nel percorso documentato G2 usa 1,1 e il ritiro 0,55. Questi coefficienti non costituiscono una legge completa nel tempo. Un φ assegnato a una fase descrive la rigidezza efficace di quel contributo secondo il metodo scelto.

### 7 2 Trasporto delle azioni

Il riferimento del momento deve essere coerente con il punto di applicazione di N. Il codice riporta il momento al riferimento comune attraverso la quota yN espressa in millimetri.

$$ Mx0 = Mx − N yN /1000

Con N = 1000 kN e una differenza di quota di 200 mm, il trasporto modifica il momento di 200 kNm con il segno stabilito dalla formula. Trascurare questa operazione può spiegare differenze rilevanti fra due calcoli che hanno la stessa sezione e gli stessi valori nominali di N e M.

È possibile riferirsi al baricentro lordo della fase, a quello efficace aggiornato o a un riferimento comune. Nel secondo caso l'eccentricità cambia durante l'iterazione delle larghezze efficaci. La scelta fa parte del problema fisico e deve essere riportata insieme alle azioni.

### 7 3 Metodo cumulativo

Ogni incremento viene analizzato con il proprio coefficiente di omogeneizzazione e la propria situazione di collaborazione. Le tensioni vengono sommate sulla configurazione efficace comune risultante dall'iterazione. Questo metodo è adatto alla sovrapposizione elastica prevista dall'implementazione, ma non conserva la stessa memoria delle deformazioni di un'analisi cronologica.

La riduzione locale delle piastre dipende dalle tensioni complessive, per cui geometria efficace e tensioni vengono aggiornate iterativamente. L'aggiunta di una fase modifica anche la geometria efficace comune sulla quale sono valutati i contributi. Non è quindi corretto aspettarsi che le tensioni di una fase precedente restino sempre identiche al calcolo eseguito isolatamente prima dell'aggiunta.

Il cumulativo dispone dei controlli aggiuntivi di taglio, appoggi, irrigidimenti e connessione. Le azioni in ingresso sono già quelle della combinazione da verificare; la scelta SLU o SLE seleziona limiti e percorsi di controllo, senza costruire i carichi fattorizzati.

### 7 4 Metodo storico lineare

Il metodo conserva il riferimento di deformazione al momento del getto e gli stati incrementali. La soletta attivata in una fase non acquisisce retroattivamente le tensioni dovute ai carichi applicati alla carpenteria prima della sua collaborazione. Il percorso cronologico diventa quindi parte dei dati del problema.

φ e n si applicano ai nuovi incrementi. Cambiare il coefficiente di una fase futura non produce automaticamente il rilassamento nel tempo di tutti gli stati precedenti. Il metodo non è un integratore reologico completo con storia di età, umidità, maturazione e viscosità per ogni giorno. La riduzione locale di classe 4 è disponibile, mentre i controlli accessori di taglio e connessione non sono valutati in questo percorso.

### 7 5 Metodo storico non lineare

La sezione è discretizzata a fibre e l'equilibrio N Mx viene risolto seguendo la storia. L'acciaio adotta una legge bilineare con incrudimento isotropo, memoria plastica e scarico elastico. Il calcestruzzo usa l'inviluppo del materiale tabulato disponibile nella libreria, senza una legge completa di danno ciclico e degradazione.

Il calcolo è istantaneo, su sezione lorda e con proprietà caratteristiche. Non applica la riduzione locale di classe 4 e non sostituisce le verifiche di instabilità con la plasticità delle fibre. Il percorso non ammette di simulare la viscosità alterando arbitrariamente il modulo di un materiale plastico attraverso n. Le impostazioni iniziali usano 160 suddivisioni nell'anima, 8 nelle flange, 64 nel cls e 8 sottopassi per incremento; la sensibilità numerica va controllata nei casi impegnativi.

La memoria plastica comporta che due sequenze con la stessa risultante finale possano produrre stati diversi. Un ciclo carico scarico può lasciare deformazioni e tensioni residue. Ciò non implica che il modello descriva automaticamente una prova a fatica, la rottura oligociclica o il degrado del calcestruzzo confinato.

### 7 6 Ritiro

Il ritiro viene assegnato come deformazione propria del calcestruzzo, negativa per accorciamento. La procedura elastica costruisce una forza equivalente Ec,eff Ac Δεcs applicata al baricentro del cls netto e una correzione di tensione propria −Ec,eff Δεcs. L'insieme riproduce l'incompatibilità locale mantenendo l'equilibrio della sezione con le risultanti esterne previste.

$$ Neq = Ec,eff Ac Δεcs
$$ Δσc,propria = −Ec,eff Δεcs

Le unità vanno rese coerenti prima della conversione in kN. Il solo stato della sezione non determina le forze secondarie causate da vincoli longitudinali di una trave continua. Analogamente, gli scorrimenti concentrati presso le estremità richiedono un modello lungo l'asse del ponte o una domanda aggiuntiva assegnata.

### 7 7 Larghezze efficaci e convergenza

Per le piastre compresse si valuta il rapporto di tensione ψ, il coefficiente di instabilità kσ, la snellezza adimensionale e il fattore ρ. Le porzioni efficaci vengono poi ricollocate nella sezione e si ricalcolano proprietà e tensioni. La snellezza locale usa fy caratteristico, non fyd. Le piattabande aggiunte vengono valutate con i propri sbalzi.

Il significato di ρ è una riduzione della porzione resistente nella verifica elastica della piastra snella. Non è una riduzione fisica del peso e non comporta rimozione di acciaio dal disegno costruttivo. La sezione lorda continua a governare i quantitativi e alcune altre grandezze.

L'iterazione utilizza una tolleranza dell'ordine di 10⁻⁷ e massimo 120 passi, con rilassamento e accelerazione secondo la procedura corrente. Il rilassamento iniziale è 0,55, limitato nel campo ammesso. Si controlla anche un residuo di equilibrio relativo dell'ordine di 10⁻⁵. Un arresto al numero massimo di iterazioni non è una convergenza positiva.

### 7 8 Limiti di tensione e interpretazione delle curve

Il percorso elastico di esercizio confronta l'acciaio strutturale con fy, il cls con i limiti 0,60fck o 0,45fck secondo la situazione e l'armatura con 0,80fyk nel percorso previsto. L'esclusione della soletta tesa è una scelta del modello; l'armatura può restare attiva. Nel cumulativo non viene introdotta automaticamente una fessurazione parziale della soletta tale da risolvere ogni distribuzione tesa.

Le curve M κ a N costante e N ε a curvatura costante possono partire da stato vergine o dalla ricostruzione della storia fino a una fase. Il motore conserva la memoria prevista dallo storico non lineare. Il risultato è una risposta caratteristica della sezione lorda: per trasformarlo in una capacità di progetto servono le verifiche e i coefficienti pertinenti. Punti non convergenti non vengono sostituiti da un inviluppo artificiale.

## 8 Taglio irrigidimenti e connessione della sezione composta

### 8 1 Resistenza a taglio

Questi controlli sono attivi nel percorso cumulativo e adottano le edizioni identificate dall'interfaccia: NTC 2018 e norme EN richiamate nella documentazione del modulo. L'anima intera partecipa al taglio, senza includere un contributo resistente della soletta. L'area plastica assunta cautelativamente è hw tw. Il contributo favorevole delle flange alla resistenza per instabilità a taglio viene omesso.

$$ τcr = kτ π² E [tw/hw]² / [12(1−ν²)]
$$ λw = √[fy/(√3 τcr)]
$$ Vpl,Rd = hw tw fy / (√3 γM0)
$$ Vbw,Rd = χw hw tw fy / (√3 γM1)
$$ VRd = min(Vpl,Rd; Vbw,Rd)

kτ dipende dal rapporto del pannello e dalla validità degli irrigidimenti; χw segue la curva del montante terminale applicabile. Senza intermedi idonei si adotta il pannello lungo. Il montante rigido richiede la verifica positiva del dettaglio previsto, non la sola selezione del nome. I coefficienti iniziali documentati sono γM1 = 1,10, γV = 1,25 ed η = 1,20; γM0 è 1,05 per NTC e 1,00 per il percorso EC. L'utente può modificarli e deve controllare l'Appendice Nazionale pertinente.

### 8 2 Interazione con il momento e tensioni tangenziali

Oltre 0,5 VRd il taglio può ridurre il margine flessionale. Per il campo N = 0, fy ≤ 355 MPa e anima non interamente compressa, si calcolano capacità plastiche di riferimento integrando flange efficaci, anima intera e soletta compressa. La capacità Mf omette l'anima. Il cls teso è nullo e le barre sono omesse cautelativamente in queste capacità di riferimento. Le verifiche elastiche della sezione non vengono sostituite da tali integrazioni plastiche.

Negli altri casi ad alto taglio viene usato un inviluppo elastico cautelativo con Mf = 0, segnalato esplicitamente. Non è un dominio plastico esatto N M V per qualunque sezione. Le tensioni tangenziali includono la media V/(hw tw) e il massimo del campo elastico V S/(I tw), con somma algebrica dei contributi di fase.

L'inviluppo √(max|σ|² + 3 max|τ|²) è un controllo aggiuntivo conservativo; i due massimi possono trovarsi in punti diversi. Non sostituisce instabilità del pannello, verifiche degli appoggi o fatica.

### 8 3 Irrigidimenti appoggi e saldature

Il modello ammette piatti mono o bilaterali anche differenti, pannelli adiacenti diversi, appoggi interni o terminali, eccentricità della reazione, montante rigido a due coppie e saldature continue. Le verifiche di pressoflessione elastica includono un'amplificazione del secondo ordine e imperfezioni equivalenti nel campo del metodo implementato. La lunghezza critica parte dal rapporto Lcr/L assegnato, inizialmente 1,00.

Un irrigidimento insufficiente non aumenta la resistenza del pannello a taglio. Le azioni di traversi, intagli, concentrazioni locali o dettagli non inseriti non vengono ricavate dalla sola sezione trasversale. La capacità di un piatto non dimostra da sola che anima, flangia e saldature trasferiscano l'intera reazione.

### 8 4 Resistenza e domanda dei pioli

Il ramo disponibile considera pioli a testa saldata, distribuzione uniforme, soletta piena ordinaria C20/25–C60/75, diametro 16–25 mm e altezza almeno 3d. La resistenza è il minore dei due meccanismi, acciaio e calcestruzzo, con fu limitato a 500 MPa. Il fattore α dipende da h/d nel campo previsto.

$$ PRd = min[0,8 fu πd²/4; 0,29 α d² √(fck Ecm)] / γV
$$ q = Σ (Vi Si/Ii + Δqi)
$$ PEd = |q| passo / npioli

La prima formula dà una forza in N con dimensioni in mm e tensioni in MPa; la domanda deve essere convertita coerentemente. q è il flusso longitudinale, passo la distanza fra file e npioli il numero per fila. Le fasi prima della collaborazione del cls non caricano la connessione nel modello. Δq consente di introdurre contributi longitudinali ottenuti da altra analisi.

V S/I presuppone proprietà costanti nel tratto e N costante lungo l'asse. Un'introduzione locale di N o una variazione di sezione non è descritta dal solo valore del taglio. Il ritiro uniforme della sezione non produce da solo V, mentre le sue zone di trasferimento possono richiedere un Δq specifico.

Nel percorso NTC si usano le proprietà della fase tensionale, inclusa l'eventuale esclusione della soletta. Nel percorso EC4 lo scorrimento considera la soletta non fessurata e la carpenteria efficace, con il φ o n pertinente. Si tratta di una distinzione di proprietà della fase, non di un passaggio nascosto ad acciaio completamente lordo.

### 8 5 Servizio dettagli e fatica

Il limite 0,75 PRd viene controllato nella situazione caratteristica di esercizio prevista; non viene trasferito automaticamente a una combinazione quasi permanente. I dettagli comprendono passi longitudinali e trasversali, bordi, testa, copriferro, posizione rispetto alle barre, rapporto fra diametro e spessore della flangia e armature trasversali.

I passi minimi implementati sono 5d longitudinale e 2,5d trasversale, con massimo longitudinale min(800 mm; 4hc). Le condizioni di azioni ripetute e fatica attivano il limite pertinente d ≤ 1,5tf; per il campo statico disponibile è adottato cautelativamente 2,5tf. Testa e distanze non sono dettagli ornamentali: possono governare la validità del collegamento anche con PEd basso.

Sono presenti controlli dell'armatura trasversale, superfici di scorrimento, ancoraggio e fatica resistente dei pioli con interazione della flangia tesa, quando i dati necessari sono assegnati. Restano fuori campo, fra gli altri, sollevamento, splitting attraverso lo spessore, gruppi non uniformi, mensole locali e lamiere grecate. Una verifica non alimentata con le escursioni e i dati di fatica non ricava autonomamente lo spettro di traffico.

## 9 Bridge Design

### 9 1 Natura del predimensionamento

Bridge Design è un modello parametrico di ordine di grandezza, con geometrie idealizzate e criteri espliciti. Il sito thebridgeeng.com/design è stato usato come riferimento funzionale e osservato variando gli input. Le regole ricostruibili dalla risposta sono state affiancate da assunzioni autonome dichiarate. Non viene affermata identità con il motore proprietario del sito, con il suo listino o con la sua verifica AASHTO dettagliata.

Il modulo separa le scelte geometriche dalle ipotesi di costo e ambientali. Per questa ragione un nuovo prezzo non cambia la resistenza del terreno o l'inerzia dell'impalcato, e un nuovo paesaggio non cambia alcuna quantità. Le otto famiglie condividono il procedimento, ma utilizzano sezioni e rapporti di snellezza diversi.

### 9 2 Larghezza e ripartizione delle campate

La larghezza totale W comprende tutte le corsie, le due banchine, lo spartitraffico e le due fasce delle barriere. Non è la sola superficie caricabile dalle corsie normative.

$$ W = nc bc + 2 banchina + spartitraffico + 2 barriera

Con più di due campate continue i pesi delle estremità sono 0,8 e quelli delle interne 1. Per n campate la campata interna iniziale vale L/(n−0,4); le due estreme valgono 0,8 volte tale valore. Con campate semplicemente appoggiate o un numero non superiore a due, la ripartizione iniziale è uniforme. L'automatismo tenta poi di spostare eventuali pile interferenti verso i bordi dell'ostacolo, con un margine di un metro e senza produrre campate inferiori a due metri.

Se lo spostamento non è risolvibile si mantiene una segnalazione. L'ottimizzazione non cerca tutte le possibili distribuzioni e non dispone di un rilievo del terreno: la planimetria, lo sghembo e l'idraulica restano esterni.

### 9 3 Famiglie e altezza automatica

| Famiglia | Campo usuale della luce in m | Rapporto r | Altezza minima in m | Soletta iniziale in m |
| --- | --- | --- | --- | --- |
| Soletta piena in c a | 6–15 | 18 | 0,35 | Intera altezza |
| Travi a T in c a | 12–30 | 17 | 0,70 | 0,22 |
| Travi a I in c a p | 20–50 | 22,22 | 1,00 | 0,22 |
| Travi a U in c a p | 25–50 | 22,22 | 1,10 | 0,22 |
| Cassone in c a p | 35–80 | 22,22 | 1,30 | 0,25 |
| Cassone variabile a conci | 80–200 | 45 | 2,00 | 0,28 |
| Travi a I acciaio cls | 30–90 | 25 | 1,00 | 0,25 |
| Cassone acciaio cls | 40–150 | 25 | 1,20 | 0,25 |

$$ d = max[dmin; (Lmax/r) k]

k vale 0,95 per continuità e 1,10 per campate appoggiate. Un'altezza positiva inserita dall'utente sostituisce la formula. Nel cassone variabile l'altezza sulla pila è max(d; Lmax/18), mentre quantità e rigidezza sono valutate con altezza media d + (dpila−d)/3. Il modello non analizza le fasi costruttive a sbalzo.

I campi usuali producono avvisi, non una dimostrazione di fattibilità. Il fatto che una luce rientri nella tabella non verifica flessione, taglio, vibrazioni o trasporto dei prefabbricati. Il numero di travi deriva dalla larghezza e dall'interasse con arrotondamento intero e minimo di due per le famiglie a travi. Per cassoni e soletta il conteggio segue la geometria specifica.

### 9 4 Sezioni quantità e rigidezza

Le sezioni sono composte da rettangoli ideali. La soletta occupa W per il proprio spessore. Le travi a T aggiungono anime; le I in c.a.p. usano due flange e un'anima semplificate, con rialzo sotto soletta; le U e i cassoni aggiungono fondo e anime. Per le U la lunghezza inclinata delle anime è usata nella quantità, mentre la rigidezza conserva una rappresentazione rettangolare semplificata.

Le travi metalliche utilizzano spessori in millimetri convertiti in metri, piattabande e anime. Nei cassoni metallici l'inclinazione delle anime è espressa come H per 4V e incide sulla lunghezza reale della lamiera. La massa della carpenteria è aumentata del 15% per rappresentare diaframmi, irrigidimenti e connessioni; tale maggiorazione non viene applicata all'inerzia flessionale.

$$ Ec = 22000 [(fc + 8)/10]^0,3 MPa
$$ n = 200000 / Ec
$$ yG = Σ(ni Ai yi) / Σ(ni Ai)
$$ Ieq = Σ ni [Ii + Ai(yi−yG)²]

ni vale 1 per il cls e n per l'acciaio. Ieq è lordo e non fessurato, senza scorrimento. Per i cassoni variabili è riferito alla sezione media, non a una trave con EI variabile lungo l'asse. Le incidenze di armatura e precompressione sono quantità parametriche e non modificano l'inerzia attraverso una disposizione reale di barre e cavi.

### 9 5 Carichi ed equilibrio della trave

Il permanente comprende peso del cls a 25 kN/m³, peso della carpenteria, permanenti portati per superficie e 8 kN/m per ogni linea di barriera, due oppure tre se esiste spartitraffico. Il traffico è un carico uniforme equivalente esteso a tutta la larghezza. Le azioni si riferiscono all'intero impalcato.

$$ g = 25 Acls + 9,81 macciaio/L + g2 W + 8 nbarriere
$$ qservizio = g + qtraffico W
$$ qfattorizzato = γG g + γQ qtraffico W

I valori iniziali sono g2 = 2,5 kN/m², traffico = 9 kN/m², γG = 1,35 e γQ = 1,50. Sono coefficienti di un modello equivalente e non identificano una combinazione normativa completa. In particolare il carico simultaneo su tutte le campate non produce l'inviluppo peggiore di traffico alternato e non rappresenta gli assi mobili.

Per continuità si risolve il sistema tridiagonale dei tre momenti con EI costante e momenti nulli alle estremità. Per campate appoggiate tutti i momenti agli appoggi sono nulli. Per due luci adiacenti L₁ e L₂, con tre momenti agli appoggi M₀, M₁ e M₂, l'equazione interna è la seguente.

$$ L₁ M₀ + 2(L₁+L₂) M₁ + L₂ M₂
$$       = −q (L₁³ + L₂³)/4

In ogni campata, con ascissa locale x e momenti estremi ML e MR, la reazione sinistra del tratto, il momento e il taglio derivano direttamente dall'equilibrio. Le reazioni dei tratti adiacenti vengono sommate sull'appoggio comune.

$$ RL = qL/2 + (MR−ML)/L
$$ M(x) = ML + RL x − qx²/2
$$ V(x) = RL − qx

La freccia deriva dalla doppia integrazione M/EI con spostamento nullo agli estremi della campata. Si campionano 41 stazioni per tratto e si aggiunge il punto di massimo momento positivo quando interno. Il massimo di freccia riportato è quindi campionato; non è ricercato con una radice analitica per ogni campata. Si usa Ec in kN/m² e I in m⁴, convertendo infine la freccia in millimetri.

### 9 6 Controlli analitici della trave

Per una trave appoggiata con L = 20 m e q = 100 kN/m, le reazioni sono 1000 kN ciascuna, il massimo momento è 5000 kNm e la somma delle reazioni è qL = 2000 kN. La freccia massima teorica è 5qL⁴/(384EI). Questo caso isola il solver dall'automatismo delle sezioni e dei carichi.

Per due campate uguali continue, entrambe caricate, il momento sull'appoggio centrale vale −qL²/8. Le reazioni estreme sono 3qL/8 e quella interna è 5qL/4. Con gli stessi q e L si ottengono −5000 kNm, 750 kN, 2500 kN e 750 kN; la somma è 4000 kN. Il massimo momento positivo è 9qL²/128 = 2812,5 kNm. Questi risultati sono coperti dai controlli automatici.

### 9 7 Pile e fondazioni

La dimensione automatica dei setti è max(1 m; H/25); per le colonne è max(1,2 m; H/12). Un telaio utilizza almeno due colonne, con numero crescente in funzione della larghezza. La quantità del pulvino usa dimensioni convenzionali, maggiorate per la testa a martello. Le spalle adottano una geometria parametrica differente e un'altezza contenuta nel modello.

La snellezza indicativa è 2H/r, con r = D/4 per la colonna circolare e t/√12 per il setto. La compressione media viene rapportata a fc delle sottostrutture. Il superamento della soglia orientativa 0,30fc produce un avviso, senza costituire una verifica di pressoflessione o una valutazione di sicurezza con coefficienti di progetto.

| Terreno convenzionale | Pressione diretta kPa | Laterale palo kPa | Punta palo kPa | Lunghezza iniziale pali m |
| --- | --- | --- | --- | --- |
| Roccia | 1000 | 150 | 8000 | 10 |
| Sabbia o ghiaia densa | 400 | 70 | 2500 | 15 |
| Terreno medio | 200 | 45 | 1500 | 22 |
| Argilla soffice | 100 | 25 | 500 | 30 |

Le fondazioni automatiche sono dirette su roccia e sul terreno denso per altezza inferiore a 15 m; negli altri casi sono pali da 1 m. La tabella può essere sostituita da valori positivi nelle Ipotesi. Sono riferimenti convenzionali già ridotti per il modello di stima, non parametri dedotti da un'indagine o resistenze calcolate dal modulo Palo verticale.

$$ Rpalo = π D L qs + π D² qb/4

Il numero automatico dei pali è almeno quattro, arrotondato al numero pari superiore richiesto dall'assiale. La disposizione usa interasse indicativo 3D. Un numero manuale o un plinto manuale può risultare insufficiente: vengono controllati rapporto assiale e ingombro della disposizione. Il lato longitudinale del plinto è arrotondato al quarto di metro, mentre la larghezza trasversale accoglie la sottostruttura.

Nel rapporto di fondazione si includono reazione G+Q, peso della sottostruttura e del plinto. L'automatismo iniziale non risolve iterativamente un progetto geotecnico completo: un rapporto maggiore di uno richiede una revisione della configurazione. Non sono considerati momenti, taglio, sisma, erosione, cedimenti e interazione di gruppo.

### 9 8 Costi carbonio e durata

Il costo diretto è la somma dei prodotti fra quantità e prezzi. Le armature derivano da incidenze in kg/m³. Il costo dei pali è al metro e comprende cls e perforazione, mentre l'armatura è separata: il cls dei pali entra nel volume complessivo e nelle emissioni, ma non viene addebitato nuovamente come calcestruzzo di plinto.

$$ Cdiretto = Σ Qi pi
$$ Ctotale = Cdiretto (1+oneri/100)(1+imprevisti/100)

Il listino iniziale è in euro ed è puramente indicativo. L'intervallo basso alto usa la percentuale ± assegnata; non proviene da una distribuzione probabilistica. Appoggi e giunti dipendono anche dalla continuità, quindi cambiare schema può modificare le finiture oltre alle sollecitazioni.

La CO₂ somma cls, carpenteria, armature e precompressione, poi applica la maggiorazione per trasporti e cantiere. I fattori del cls sono in kg/m³; quelli degli acciai in kg/kg. Una massa espressa in tonnellate moltiplicata per kg/kg dà numericamente tonnellate di CO₂. Le opzioni a ridotta CO₂ e riciclato moltiplicano rispettivamente il fattore del cls e quello della carpenteria.

$$ CO₂ = [Vcls fcls/1000 + Σ macciaio facciaio] (1+cantiere/100)

Finiture, esercizio e fine vita non sono quantificati. I fattori iniziali non sono riferiti a EPD specifiche. Il confronto è utile solo mantenendo coerenti perimetro, unità e qualità delle ipotesi delle alternative.

La durata è arrotondata al mese superiore e deriva da avvio, numero di campate, numero di pile, maggiorazione per fondazioni su pali e per conci a sbalzo. I coefficienti iniziali sono 4 mesi di avvio, 1,2 per campata, 0,4 per pila, 1,5 aggiuntivi con pali e 1,5 per campata nel cassone a conci. È una stima parametrica con incertezza indicativa ±25%, senza calendario, risorse e percorso critico.

## 10 Esempi trasversali e lettura critica

### 10 1 Separare capacità domanda e quantità

Un incremento del prezzo dell'acciaio non può aumentare il momento resistente di una sezione. Un aumento della quantità parametrica di armatura in Bridge Design può aumentare il costo senza creare una disposizione di barre verificata. Al contrario, nella sezione CA una nuova barra modifica direttamente l'equilibrio e il dominio. La stessa parola «armatura» descrive quindi oggetti differenti nei due moduli.

Un aumento di lunghezza del palo modifica resistenza e peso, ma può uscire dalla profondità investigata. Un aumento della resistenza del cls può influire sul dominio CA, sul copriferro prescritto e sulla rigidezza del ponte preliminare, con leggi differenti. Il confronto di sensitività deve identificare quale ramo stia cambiando.

### 10 2 Controlli indipendenti semplici

Per una quantità di cls di 100 m³ a 240 €/m³ il costo diretto è 24000 €. Con 12% di oneri e 15% di imprevisti diventa 30912 €. Con fattore 320 kgCO₂/m³ le emissioni materiali sono 32 tCO₂; aggiungendo 15% di cantiere diventano 36,8 tCO₂. Prezzo e fattore ambientale operano su canali distinti.

Per una sezione metallica ideale in campo elastico, verificare preliminarmente che N/A e M/W abbiano unità MPa dopo la conversione delle azioni. Per un palo laterale verificare che la somma delle forze distribuite, dell'eventuale forza al piede e della forza in testa sia equilibrata. Per una trave verificare che la somma delle reazioni equivalga al carico totale. Questi controlli intercettano errori di unità e interpretazione prima di discutere dettagli costitutivi.

### 10 3 Cosa dimostrano le prove del software

Le prove numeriche confrontano casi noti, identità di equilibrio, sensibilità ai parametri e round trip degli archivi. Le prove dell'interfaccia verificano navigazione, invalidazione dei risultati, persistenza dei dati ed esportazioni. Superarle dimostra coerenza rispetto ai casi e alle proprietà controllate; non dimostra universalmente l'adeguatezza del modello per qualsiasi opera.

Per Bridge Design la campagna di questa integrazione comprende 5753 controlli: soluzioni analitiche di travi, 200 travi con luci diseguali, 128 combinazioni di famiglie terreni e pile, indipendenza dal listino per le grandezze fisiche, CO₂, archivi e documenti. La prova WPF esercita le otto famiglie, viste, larghezze della finestra, prezzi, Annulla, A/B e report. Le precedenti campagne degli altri moduli sono documentate separatamente: non vengono presentate qui come una nuova validazione integrale di tutti i motori.

## 11 Tracciabilità e riferimenti

### 11 1 Mappa dei sorgenti

| Tema | Sorgenti principali nel repository |
| --- | --- |
| Palo verticale e gruppo | X.Calculations/Calcolo.cs e Nq.cs |
| Micropalo verticale | X.Calculations/Micropali.cs |
| Broms e momento CA | X.Calculations/PaloOrizzontale.cs e SectionMomentResistance.cs |
| Tubo CHS | X.Calculations/MicropaloOrizzontale.cs |
| Materiali e copriferro | X.Calculations/Materials e RebarMaterial.cs |
| Sezione CA | X.Calculations/ConcreteAnalysis.cs e CheckerSection.cs |
| Fessurazione | Ntc2018Checks.cs e ConcreteTensionCracking.cs in X.Calculations |
| Taglio torsione dettagli CA | ConcreteShearAnalysis.cs, ConcreteTorsion.cs e ConcreteDetailing.cs |
| Sezione composta | X.Calculations/BridgeSection e relativi file parziali |
| Bridge Design | X.Calculations/BridgeConcept.cs e BridgeConcept.Calculation.cs |
| Archivi revisioni report | X.Core e documentazione di progetto in supporto/docs |

Per i metodi delle librerie esterne integrate, questa guida descrive l'uso effettuato da ANTHEA e la documentazione tecnica disponibile, senza attribuire alla sola interfaccia la responsabilità delle leggi interne del solver. Aggiornamenti delle DLL possono modificare il comportamento e richiedono una nuova verifica dei casi di confronto pertinenti.

### 11 2 Documentazione tecnica del progetto

Gli approfondimenti principali sono palo-orizzontale.md, micropalo-orizzontale.md, calcestruzzo-sle-geometria.md, calcestruzzo-estensioni.md, sezione-mista-ponte.md, ponte-storico-non-lineare.md, ponte-curve-sezione.md, taglio-pioli-fonti-e-metodo.md e irrigidimenti-appoggi-connessione.md, tutti in supporto/docs. Per gestione dati e riproducibilità consultare gerarchia-progetti.md e progetti-revisioni.md. Alcuni documenti conservano paragrafi storici: i successivi aggiornamenti e il codice corrente prevalgono per identificare il comportamento della versione.

### 11 3 Riferimenti esterni identificati

Il riferimento nazionale richiamato dai moduli è il Decreto 17 gennaio 2018, pubblicato nella Gazzetta Ufficiale del 20 febbraio 2018. Il collegamento ufficiale identifica atto ed edizione; la scelta delle disposizioni applicabili al singolo progetto rimane esterna all'automatismo del foglio: https://www.gazzettaufficiale.it/eli/id/2018/02/20/18A00716/sg.

Per le piastre metalliche, il commentario JRC del 2007 illustra il contesto e gli esempi relativi a EN 1993-1-5. È un riferimento di supporto e non sostituisce il testo normativo con la relativa Appendice Nazionale: https://eurocodes.jrc.ec.europa.eu/sites/default/files/2021-12/EUR22898EN.pdf.

Le edizioni della documentazione del modulo composto sono EN 1993-1-5:2006, EN 1993-2:2006 ed EN 1994-2:2005 con i corrigenda richiamati. Non viene assunta un'applicazione implicita delle edizioni di seconda generazione. Le correlazioni geotecniche, gli abachi digitalizzati e i coefficienti ambientali hanno campi e provenienze differenti, indicati nei rispettivi capitoli; non devono essere assimilati a un unico livello di prescrizione normativa.

Il riferimento funzionale di Bridge Design è https://thebridgeeng.com/design. L'implementazione ANTHEA utilizza formule e ipotesi autonome esplicite per il predimensionamento; il confronto con il sito non costituisce una verifica indipendente della sicurezza strutturale.
