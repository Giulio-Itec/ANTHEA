# Guida teorica dei calcoli di ANTHEA

Modelli formule ipotesi ed esempi dei moduli disponibili

Edizione 5 aggiornata il 5 ottobre 2026 — revisione documentale 24

Questa edizione integra i contenuti precedenti nel percorso dell'Engineering Handbook. Le procedure correnti e la teoria sono separate dai resoconti di sviluppo. Le fonti integrali e le evidenze storiche restano nell'archivio Rev14; gli indirizzi precedenti della Wiki raggiungono le pagine consolidate. Lo stato editoriale distingue contenuti integrati, pagine revisionate e profili che richiedono ulteriori riscontri normativi.

## Architettura del calcolo e convenzioni

### Separazione fra modello e interfaccia

La tavola delle armature è una proiezione dei pezzi longitudinali, delle zone di staffatura e delle sezioni restituiti da Checker. Marche e coordinate grafiche non alterano quantità, quote o resistenze. Il modello attuale fornisce barre longitudinali rettilinee e posizioni nominali delle staffe: il disegno non aggiunge ganci o sagomature non calcolati. Le lunghezze di taglio delle staffe restano da definire e i dettagli costruttivi non completati sono segnalati. Le sezioni trasversali rappresentano l’armatura nominale del tratto, senza attribuire resistenza aggiuntiva alle barre sovrapposte.

I nuovi fogli del palo inizializzano l’acciaio B450C dal catalogo comune alla sezione in c.a., includendo l’intero legame costitutivo. La riapertura di archivi precedenti conserva invece i parametri salvati: la sola resistenza fyk di 450 MPa non identifica automaticamente tutte le proprietà di B450C.

Il riferimento GPC Engine nel palo orizzontale identifica il motore delle librerie di calcolo; la presentazione dei materiali usa i nomi Calcestruzzo e Acciaio. Formulazioni, parametri e risultati rimangono quelli descritti nelle sezioni teoriche del modulo.

X.Calculations contiene i motori e le funzioni di calcolo indipendenti da WPF. X.Core gestisce archivi, integrazioni e report. X.Desktop presenta editor e grafici; X.Materiali presenta le schede dei materiali. I motori di sezione CA e composta utilizzano anche le librerie Model e Checker distribuite con il progetto. Il disegno non è il modello resistente: la discretizzazione della vista può essere diversa da quella usata per l'equilibrio.

Bridge Design utilizza un proprio motore puro, BridgeConcept.Calculate. Riceve input, listino e ipotesi e restituisce un risultato con geometria, quantità, dettagli, diagrammi e avvisi. Lo stesso risultato alimenta interfaccia e report. Il motore non modifica gli input: un parametro impostato a zero per richiedere la scelta automatica resta zero nell'archivio, mentre il valore adottato è riportato nel risultato.

I calcoli dei report di progetto vengono eseguiti su copie dei dati, evitando di cambiare i fogli aperti. La gestione delle revisioni congela input e contesto necessario; non congela un eseguibile storico. Ricalcolare un vecchio archivio con un motore aggiornato può quindi produrre risultati diversi, che devono essere identificati con versione e data.

### Unità e segni

| Ambito | Lunghezze | Tensioni e resistenze | Azioni | Convenzione da ricordare |
| --- | --- | --- | --- | --- |
| Geotecnica verticale | m | kPa | kN | Compressione e trazione trattate in rami distinti |
| Broms | m per il terreno; mm per la sezione | kPa nel terreno; MPa nei materiali | kN e kNm | N positivo a compressione |
| Sezione CA | mm | MPa | kN e kNm | N negativo a compressione; trazione positiva |
| Sezione composta | mm | MPa | kN e kNm | N negativo a compressione nelle fasi; appoggi con convenzione propria |
| Bridge Design | m salvo spessori metallici in mm | MPa e kPa secondo etichetta | kN e kNm | Carichi e momenti dell'intero impalcato |

Vale 1 MPa = 1 N/mm² = 1000 kPa. Per trasformare Nmm in kNm si divide per un milione; per trasformare N in kN si divide per mille. Per passare da una curvatura espressa in 1/mm al valore numerico in 1/m si moltiplica per mille. Una deformazione di 0,001 equivale a 1 per mille e a 1000 microdeformazioni.

Le convenzioni del segno non sono intercambiabili fra i moduli. I colori dei diagrammi non costituiscono una convenzione universale dell'applicazione: leggere la legenda. Per confrontare due solver occorre allineare anche origine degli assi, verso dei momenti, punto di applicazione di N e trasformazione delle coordinate.

### Dati mancanti e validità del risultato

La validazione degli input impedisce di utilizzare numeri non finiti, geometrie incompatibili e parametri fuori campo nelle procedure che li richiedono. Un valore mostrato con pochi decimali è una presentazione: calcolo ed esportazioni conservano precisione maggiore. L'arrotondamento grafico non deve essere usato per ricostruire un equilibrio con tolleranze molto strette.

I risultati precedenti eventualmente conservati a video sono marcati da aggiornare dopo una modifica. Un risultato incompleto o fuori campo non viene convertito in esito favorevole. La disponibilità va letta per ogni verifica: un equilibrio tensionale può riuscire mentre fessurazione, dettagli o instabilità restano non valutabili.

## Calcestruzzo armature e copriferro

### Resistenze e diagrammi

Per il calcestruzzo il passaggio dalla resistenza caratteristica a quella di progetto segue il coefficiente αcc e il parziale γc selezionati. Per l'armatura la resistenza di progetto deriva da fy e γs. I diagrammi possono essere di catalogo o personalizzati; l'uso di una classe nominale non autorizza a ignorare il diagramma effettivamente selezionato.

```math
f_{cd} = \frac{\alpha_{cc} f_{ck}}{\gamma_c}
f_{yd} = \frac{f_{yk}}{\gamma_s}
\varepsilon_{yd} = \frac{f_{yd}}{E_s}
```

Con fck = 30 MPa, αcc = 0,85 e γc = 1,50 si ottiene fcd = 17 MPa. Con fyk = 450 MPa e γs = 1,15 si ottiene fyd = 391,30 MPa. Assumendo Es = 200000 MPa, la deformazione elastica a fyd è 0,0019565, cioè 1,9565 per mille. Sono esempi dei passaggi algebrici, non una scelta universale dei coefficienti.

Il catalogo delle armature comprende classi attuali e storiche. I dati nominali delle classi storiche non sostituiscono prove e valutazioni di un materiale esistente. A5 e deformazione ultima del diagramma hanno definizioni diverse. Quando il motore richiede una deformazione ultima, essa deve provenire da una scelta documentata e coerente con il modello costitutivo.

### Esposizioni e requisiti del materiale

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

### Copriferro minimo e nominale

Il minimo deve soddisfare aderenza, durabilità e condizioni aggiuntive. Il nominale comprende il margine esecutivo e gli eventuali limiti per la superficie di getto. Nel ramo implementato, il requisito di aderenza parte dal diametro e aumenta di 5 mm quando l'aggregato supera 32 mm. Rugosità e abrasione aggiungono i contributi assegnati.

```math
c_{\min} = \max(10; c_{bond}; c_{dur}) + c_{rugosita} + c_{abrasione}
c_{nom} = \max(c_{\min}+\Delta c_{dev}; c_{controterra})
```

Il ramo EC2 determina la classe strutturale secondo esposizione, vita e opzioni ammesse, poi consulta la tabella di durabilità. Le esposizioni che non definiscono da sole quel requisito richiedono un'associazione pertinente, anziché essere trasformate in una classe equivalente arbitraria.

Il ramo NTC utilizza tre livelli di severità. Il valore tabellare di base è (15 mm per elemento a piastra, 20 mm negli altri casi) più 10 mm per livello di severità; si aggiungono 5 mm se fck è inferiore a C0 = 35 + 5 × severità. La vita di 100 anni aggiunge 10 mm, una resistenza sotto il minimo pertinente aggiunge 5 mm e l'opzione di qualità del copriferro riduce di 5 mm. Il minimo pertinente può essere assegnato nel campo ammesso dal motore e non coincide necessariamente con tutte le prescrizioni di composizione.

Come esempio del ramo NTC, XF2, fck = 30 MPa, elemento non a piastra, vita 50 anni, barra 16 mm, aggregato 20 mm e Δcdev = 10 mm danno 45 mm nominali senza riduzione di qualità. Se si passa a vita 100 anni, lo stesso caso dà 55 mm. I valori sono esempi riproducibili del codice NtcCover, utili per controllare l'input.

## Palo verticale

### Geometria e tensioni geostatiche

Per un palo circolare di diametro D si usano area di base Ab e perimetro u. La stratigrafia è integrata per tratte; la falda può suddividere una stessa tratta in una parte asciutta e una immersa. Sotto falda la tensione efficace cresce con il peso sommerso, mentre la tensione totale continua a usare il peso saturo.

```math
A_b = \frac{\pi D^2}{4}
u = \pi D
\gamma' = \max(0; \gamma_{sat}-9{,}81)
\sigma'_v(z) = \int \gamma'(z)\,dz
```

La media della tensione efficace in una tratta è ottenuta integrando il profilo effettivo, compreso il cambio di pendenza in corrispondenza della falda. Usare la sola tensione al centro dello strato può dare un valore diverso se il tratto attraversa quella discontinuità. Il peso del terreno di uno strato continua a influire sugli strati sottostanti anche quando la sua resistenza laterale viene esclusa.

### Laterale drenato

La tensione tangenziale limite drenata è espressa in funzione di coesione efficace, coefficiente K, attrito palo terreno μ e tensione verticale efficace media. Negli strati granulari il termine c′ viene posto a zero. Negli strati coesivi rimane disponibile nel ramo drenato.

```math
\tau_s = c' + K\mu\sigma'_v
R_s = \sum_j \pi D L_j\tau_{sj}
```

| Tecnologia | K sciolto | K denso | μ adottato |
| --- | --- | --- | --- |
| Profilato di acciaio battuto | 0,7 | 1,0 | tan 20° |
| Tubo chiuso battuto | 1,0 | 2,0 | tan 20° |
| Calcestruzzo prefabbricato | 1,0 | 2,0 | tan 0,75φ |
| Calcestruzzo gettato in opera | 1,0 | 3,0 | tan φ |
| Trivellato | 0,5 | 0,4 | tan φ |
| Elica continua | 0,7 | 0,9 | tan φ |

La tabella descrive i valori presenti nel programma, inclusa la particolare coppia del trivellato. Sono correlazioni del modello, non parametri misurati automaticamente. La scelta di un valore manuale richiede di mantenerne traccia nel foglio e nel report.

### Laterale non drenato

Negli strati coesivi interessati dal ramo non drenato sotto falda si impiega τs = α Cu. Per pali battuti α vale 1 fino a Cu = 25 kPa, decresce secondo 1 − 0,0111(Cu − 25) fra 25 e 70 kPa e vale 0,5 da 70 kPa. Per le altre tecnologie vale 0,7, poi 0,7 − 0,008(Cu − 25), poi 0,35 negli stessi intervalli. Queste sono le espressioni a tratti effettivamente implementate.

I tratti sopra falda che non soddisfano le condizioni del ramo non drenato riutilizzano il calcolo drenato; gli strati granulari conservano la propria formulazione. La curva denominata non drenata può quindi essere composta da contributi calcolati con rami diversi. Occorre leggere la stratigrafia e non attribuire Cu a tutto il profilo.

### Resistenza di punta e abaco Nq

Nel ramo drenato la base utilizza Ab σ′v Nq. Nel ramo non drenato coesivo sotto falda si usa una formulazione lorda Ab (Nc Cu + σv), con tensione verticale totale. Il contributo di sovraccarico non va quindi sottratto di nuovo senza considerare come sono state definite le azioni.

```math
R_{b,\mathrm{drenata}} = A_b\sigma'_v N_q
R_{b,\mathrm{non\ drenata}} = A_b(N_c C_u+\sigma_v)
```

Nq proviene dall'abaco parametrizzato NQ 2026 09 09 e non dalla sola formula esponenziale classica della capacità portante. Per D ≤ 0,8 m sono disponibili le curve z/D = 5, 10, 20 e 50. Ciascuna è descritta dalle coppie di angoli alle quali Nq vale 10 e 100: (23; 35,6), (24,6; 37), (25,8; 37,8), (27,5; 38,8) gradi.

```math
\log_{10}(N_q) = 1+\frac{\varphi-\varphi_{10}}{\varphi_{100}-\varphi_{10}}
```

Fra le curve si interpola nella scala logaritmica del rapporto z/D e di Nq. Per D > 0,8 m si usano le curve per z/D = 4 e 32, definite da polinomi cubici raccordati, con interpolazione aritmetica fra esse e φ limitato al campo 26–42°. I valori esterni vengono ricondotti al bordo e segnalati; non costituiscono un'estrapolazione validata.

### Più indagini e coefficienti

Si calcolano media e minimo separati di laterale e punta alla quota considerata. Il minimo della somma delle componenti non è necessariamente la somma dei minimi: ANTHEA adotta la seconda costruzione nel ramo definito dalle componenti minime. Questa scelta va distinta dal risultato di un singolo sondaggio.

```math
R_{d,C} = \eta_C\min\left[\frac{R_{s,medio}/\gamma_s+R_{b,medio}/\gamma_b}{\xi_3}; \frac{R_{s,\min}/\gamma_s+R_{b,\min}/\gamma_b}{\xi_4}\right]
R_{d,T} = \eta_T\min\left[\frac{R_{s,medio}}{\xi_3\gamma_t};\frac{R_{s,\min}}{\xi_4\gamma_t}\right]
```

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

```math
E_{d,C} = N_C+\gamma_{G,sfav}W
E_{d,T} = \max(0; N_T-\gamma_{G,fav}W)
```

### Efficienza di gruppo e limiti

Converse Labarre usa la geometria del reticolo nelle due direzioni. Gli angoli della seguente espressione sono in gradi; ciascun termine è nullo quando in quella direzione esiste un solo palo.

```math
\eta = 1-\frac{\arctan(D/s_x)}{90}\frac{n_x-1}{n_x} -\frac{\arctan(D/s_y)}{90}\frac{n_y-1}{n_y}
```

Feld conta le coppie adiacenti ortogonali e diagonali: P = (nx−1)ny + nx(ny−1) + 2(nx−1)(ny−1), poi η = 1 − 2P/(16 nx ny). Per questi due metodi la versione corrente applica la stessa efficienza a compressione e trazione. L'opzione manuale permette valori distinti. Un risultato non positivo viene rifiutato.

La profondità analizzabile deve essere coperta dalle stratigrafie necessarie. Sono fuori dal modello automatico cedimenti, attrito negativo, resistenza del blocco di gruppo e interazione completa terreno struttura. Il rapporto Ed/Rd riguarda la resistenza assiale considerata e non esprime da solo la prestazione di esercizio.

## Micropalo verticale

### Correlazione del bulbo

La resistenza laterale usa curve digitalizzate Bustamante Doix documentate nel materiale di riferimento di Viggiani, sezione 13.1.6. Le famiglie SG, AL, MC e R e le curve 1 IRS e 2 IGU individuano la correlazione applicabile. La grandezza p_l, in MPa, viene interpolata linearmente fra i punti della curva; l'ordinata viene convertita in kPa. Il codice rifiuta valori esterni al campo disponibile. Il campo storico dell'interfaccia è «Pressione p_i = p_l»: l'uguaglianza è un'assunzione dell'integrazione e non dimostra che la pressione della pompa coincida fisicamente con il parametro geotecnico dell'abaco.

Il diametro del bulbo viene stimato come Ds = α D, dove D è il diametro di perforazione. La resistenza di ogni tratta attiva vale π Ds Lj τj. α rappresenta l'espansione convenzionale e va scelto in relazione a terreno e iniezione; non è un incremento di resistenza indipendente dalla geometria.

```math
D_s = \alpha D
R_s = \sum_j\pi D_s L_j\tau_j
```

Per esempio, D = 0,20 m, α = 1,30, lunghezza attiva 8 m e τ = 150 kPa producono Ds = 0,26 m e Rs = 980,18 kN prima dei coefficienti. Aumentare α del 10% aumenta linearmente la superficie resistente, a parità delle altre ipotesi. Il risultato resta condizionato alla validità della correlazione e della realizzazione del bulbo.

### Inclinazione punta e peso

Per un'inclinazione θ dalla verticale, una differenza di quota Δz corrisponde a una tratta lungo l'asse Δs = Δz/cos θ. La resistenza viene sommata lungo quell'asse. Non deve essere letta direttamente come componente verticale di una capacità di gruppo senza risolvere la geometria delle azioni.

La punta opzionale è una frazione assegnata del laterale; non deriva da una capacità portante indipendente. Per il peso si separano area del tubo e area di boiacca. Il diametro del tubo è distinto da quello del bulbo. La componente adottata nella procedura segue il fattore cos θ previsto dal modello.

```math
A_{s,tubo} = \frac{\pi(D_e^2-D_i^2)}{4}
q = \frac{9{,}81\cdot7850 A_{s,tubo}}{1000}+\gamma_{boiacca}A_{boiacca}
W = q s\cos\theta
```

Diametri e aree devono essere riportati in metri e metri quadrati nella formula del peso. Media, minimi, ξ, γ ed efficienze seguono la struttura del calcolo verticale. Instabilità del tubo, flessione, sfilamento del collegamento e trasferimento locale fra tubo e boiacca restano verifiche distinte.

## Capacità orizzontale con Broms

### Idealizzazione e pressione limite

Il palo viene analizzato attraverso meccanismi limite compatibili con la resistenza del terreno e un momento plastico My assegnato. Non si introduce una legge di spostamento delle molle; il risultato principale è il carico limite H. Il terreno omogeneo costituisce il riferimento più diretto, mentre stratificazioni della stessa famiglia e falda sono estensioni sperimentali della procedura integrale.

Nei coesivi la resistenza laterale per unità di lunghezza è nulla nei primi 1,5D dal piano campagna e vale poi 9CuD. Il tratto escluso non ricomincia a ogni cambio di strato. Nei granulari la resistenza è 3KpDσ′v. Le unità di p sono kN/m.

```math
p(z) = 9 C_u D\quad\mathrm{per}\ z\geq1{,}5D\ \mathrm{nei\ coesivi}
p(z) = 3 K_p D\sigma'_v(z)\quad\mathrm{nei\ granulari}
K_p = \frac{1+\sin\varphi}{1-\sin\varphi}
```

Si definiscono Q come integrale delle forze, S come integrale dei momenti rispetto al piano campagna e A come momento rispetto alla sezione alla profondità z. Queste funzioni permettono di formulare in modo uniforme equilibrio e criteri limite.

```math
Q(z) = \int_0^z p(s)\,ds
S(z) = \int_0^z s p(s)\,ds
A(z) = z Q(z)-S(z)
V(z) = H-Q(z)
M(z) = M_0+H z-A(z)
```

Con testa libera M0 = He. La profondità zf del massimo momento soddisfa Q(zf) = H. Per il terreno coesivo omogeneo zf = 1,5D + H/(9CuD). I rami vengono confrontati in base a equilibrio e capacità, non soltanto mediante una soglia geometrica L/D.

### Meccanismi granulari

Per la testa libera e il palo corto H = A(L)/(L+e). Nel ramo lungo il momento massimo raggiunge My: He + S(zf) = My. Per testa impedita il ramo corto ha H = Q(L), M0 = −S(L), ed è ammissibile quando il momento alla testa non supera My.

Nel ramo intermedio impedito H = [My + A(L)]/L con momento in testa −My. Nel ramo lungo impedito la condizione è S(zf) = 2My. L'equilibrio può richiedere una forza concentrata al piede F = Q(L) − H nei regimi corti e intermedi. Tale forza spiega il salto del taglio e non deve essere rimossa per rendere il diagramma visivamente continuo.

Per un palo lungo, la porzione mobilitata e l'eventuale chiusura del diagramma oltre il massimo derivano dall'idealizzazione del meccanismo; non costituiscono una distribuzione elastica univoca delle reazioni del terreno.

### Meccanismi coesivi

La coppia resistente inferiore C fra zf e t è ottenuta dividendo la distribuzione al punto che ne equilibra le risultanti. Nel coesivo omogeneo vale 9CuD(t−zf)²/4. Più in generale, individuato b in modo che Q(b) sia la media di Q(zf) e Q(t), si usa C = S(t) + S(zf) − 2S(b).

Per testa libera corta si impone He + S(zf) = C(zf,L); per il ramo lungo He + S(zf) = My e C(zf,t) = My con t ≤ L. Con testa impedita, nel ramo intermedio −My + S(zf) = C(zf,L), mentre nel lungo S(zf) = 2My. Il ramo corto impedito resta H = Q(L) con controllo del momento alla testa.

La ricerca numerica usa bisezione e verifiche dell'equilibrio. La tolleranza della radice è dell'ordine di 10⁻⁸ con massimo 100 iterazioni; i residui di equilibrio sono controllati rispetto alle scale delle azioni. Il regime governante è quello ammissibile con capacità minore.

### Da Hu alla resistenza di progetto

Le capacità dei sondaggi vengono combinate tramite media e minimo e i fattori ξ selezionati. La riduzione di gruppo e il coefficiente di resistenza vengono applicati successivamente. Nel percorso implementato il parziale di resistenza laterale vale 1,3.

```math
R_k = \min\left(\frac{H_{u,medio}}{\xi_3};\frac{H_{u,\min}}{\xi_4}\right)
R_d = \frac{\eta R_k}{1{,}3}
```

HEd è già un'azione di progetto assegnata. Il motore non genera l'intero percorso delle combinazioni normative. L'efficienza di gruppo basata su Reese e Van Impe usa fattori direzionali: davanti min[1; 0,7(s/D)^0,26], dietro min[1; 0,48(s/D)^0,38], lateralmente min[1; 0,64(s/D)^0,34]. I contributi diagonali combinano i fattori longitudinali e laterali secondo l'angolo; il prodotto dei vicini attivi dà η. Questa riduzione agisce su Rd, senza ridisegnare il meccanismo Hu del singolo palo.

### Momento resistente del palo in c a

La procedura automatica considera una sezione circolare con 4–512 barre uniformemente distribuite. La distanza dell'asse delle barre dal bordo comprende copriferro, diametro della staffa e mezzo diametro longitudinale. N è positivo a compressione in questo motore.

Si assume conservazione delle sezioni piane, calcestruzzo compresso a parabola rettangolo senza contributo teso e acciaio elastico perfettamente plastico. Si sottrae il calcestruzzo sostituito dalle barre. L'equilibrio assiale viene ricercato nel campo con asse neutro interno, 0 < x < D. Non è quindi una procedura generale per qualsiasi stato assiale, inclusi i campi interamente tesi o compressi.

Due integrazioni, 28 × 96 e 56 × 192, vengono confrontate. Se lo scarto supera il 2% il momento automatico non è accettato. Un My manuale può essere utilizzato con provenienza esplicita, ma il meccanismo di Broms non dimostra la duttilità necessaria a sviluppare la cerniera.

### Tubo CHS del micropalo

La sezione resistente è il solo tubo; la boiacca non contribuisce. Le formule geometriche sono esatte per l'anello, con De e Di in millimetri. Il diametro geotecnico usato in p(z) resta un parametro separato.

```math
A = \frac{\pi(D_e^2-D_i^2)}{4}
I = \frac{\pi(D_e^4-D_i^4)}{64}
W_{el} = \frac{2I}{D_e}
W_{pl} = \frac{D_e^3-D_i^3}{6}
N_{pl} = \frac{A f_y}{\gamma_{M0}}
M_{pl} = \frac{W_{pl}f_y}{\gamma_{M0}}
M_y(N) = M_{pl}\left(1-\frac{|N|}{N_{pl}}\right)
```

L'ultimo rapporto va valutato con N e Npl nelle stesse unità; i risultati di forza e momento sono convertiti in kN e kNm. Il ramo automatico richiede classe 1 secondo De/t ≤ 50ε², con ε² = 235/fy. Le soglie 70ε² e 90ε² identificano i campi successivi, senza abilitarli come sezione plastica automatica. La relazione lineare N M è la semplificazione adottata; non verifica instabilità globale, fatica o ovalizzazione locale.

## Sezione in calcestruzzo armato

### Geometria e deformazioni piane

Sono disponibili sezioni rettangolari, circolari e a T, con fori centrali per le forme supportate. Il contorno circolare è discretizzato mediante un poligono con numero di lati configurabile. Le aree dei vuoti sono escluse e le barre incompatibili con il contorno o interne ai vuoti vengono rifiutate.

L'ipotesi cinematica è una distribuzione piana delle deformazioni. Indicando con ε0 la deformazione al riferimento e con kx e ky i gradienti, la deformazione in un punto è una funzione affine delle coordinate. Le convenzioni dei gradienti e dei momenti sono trasformate nel sistema di riferimento esposto dal foglio.

```math
\varepsilon(x,y) = \varepsilon_0+k_x x+k_y y
N = \int_{A_c}\sigma_c\,dA+\sum A_s\sigma_s+\sum A_p\sigma_p
```

I momenti derivano dagli integrali delle tensioni moltiplicate per i rispettivi bracci, nel verso degli assi del solver. Le barre e i tendini sono contributi discreti; il calcestruzzo è integrato sulla regione resistente. Il piano di deformazione richiesto dalle azioni e il piano al limite resistente sono risultati differenti e vengono esposti separatamente.

### Equilibrio e domini

Per un dato stato N Mx My, il motore cerca le deformazioni che producono le risultanti assegnate, entro i limiti dei materiali. I domini resistenti sono ricavati esplorando stati limite delle deformazioni e integrando le corrispondenti tensioni. Il campionamento della superficie usato per la visualizzazione non sostituisce la procedura diretta di verifica richiamata dalle librerie.

Il criterio di crescita delle azioni determina la direzione della ricerca sul dominio. Con N costante si aumenta la componente flettente lungo la direzione scelta; con eccentricità costante si percorre una direzione differente nello spazio delle azioni. Un coefficiente di sfruttamento non è quindi interpretabile senza il criterio associato.

I quattro momenti resistenti rapidi, a N assegnato, corrispondono alle direzioni positive e negative dei due assi. Sono quattro interrogazioni del motore, non un rettangolo resistente dentro cui qualunque coppia di momenti sia ammessa. La proiezione 2D di un punto fuori piano può apparire interna pur non risolvendo il problema tridimensionale.

### Materiali tendini e analisi di esercizio

La risposta dipende dai diagrammi selezionati per cls, barre e tendini. Nelle leggi senza resistenza a trazione, il calcestruzzo teso non contribuisce all'equilibrio. Nelle analisi elastiche si distinguono le proprietà del modello tensionale da quelle impiegate successivamente nelle formule di fessurazione.

Per un gruppo di n fili o trefoli con area assegnata, l'eventuale diametro equivalente rappresenta la somma delle aree; a parità di diametro elementare cresce con √n. La tensione iniziale del tendine è un input efficace. Il motore non ricostruisce in modo generale attrito, rientro degli ancoraggi, rilassamento e tutte le perdite differite della struttura.

Le verifiche di tensione di esercizio utilizzano le combinazioni già assegnate e i limiti disponibili nel modulo. La scelta Rara, Frequente o Quasi permanente non genera le combinazioni. In alcuni percorsi la verifica di decompressione o di formazione delle fessure usa uno stato ausiliario interamente reagente: tale stato non va confuso con quello fessurato usato per le tensioni delle barre.

### Apertura delle fessure

La formula implementata parte da tensione delle barre efficaci, modulo dell'acciaio, Ecm, resistenza media a trazione e rapporto fra armatura e area efficace di calcestruzzo. αe usa Es/Ecm e può differire dal coefficiente di omogeneizzazione con viscosità impiegato per l'analisi tensionale. Nel codice attuale fct,eff coincide con fctm, senza riduzione automatica per l'età di fessurazione.

```math
\rho_{eff} = \frac{A_{s,eff}}{A_{c,eff}}
\alpha_e = \frac{E_s}{E_{cm}}
\Delta\sigma = \frac{k_t f_{ctm}(1+\alpha_e\rho_{eff})}{\rho_{eff}}
\Delta\varepsilon = \max\left[\frac{\sigma_s-\Delta\sigma}{E_s};\frac{0{,}60\sigma_s}{E_s}\right]
```

kt vale 0,60 per breve durata e 0,40 per lunga durata. k1 vale 0,80 per barre ad aderenza migliorata e 1,60 per barre lisce. Nel ramo ordinario k2 vale 0,50 quando almeno una barra ordinaria è compressa, altrimenti 1,00; una barra a tensione esattamente nulla non è compressa. Nel ramo di sezione interamente tesa si impiega invece la distribuzione di deformazioni: k2 = (εmax + εmin)/(2εmax), limitato fra 0,5 e 1.

Il codice conserva una rappresentazione tramite distanza media Δsm e fattore finale 1,70. Per le barre ravvicinate questo conduce allo stesso prodotto scritto direttamente con il termine 3,4c + 0,425 k1 k2 φeq/ρeff. Per le barre distanziate viene confrontata anche la regione distante dalle armature.

```math
\Delta s_{m,vicino} = \frac{3{,}4c+0{,}425 k_1 k_2\varphi_{eq}/\rho_{eff}}{1{,}70}
s_{lim} = 5(c+\varphi_{eq}/2)
\Delta s_{m,distante} = 0{,}75(h-x)
w_k = \max(0;1{,}70\Delta s_m\Delta\varepsilon)
```

Se s ≤ slim si usa Δsm,vicino; altrimenti si usa il massimo fra le due distanze. Questo dettaglio è rilevante: sostituire il ramo distante con un coefficiente arrotondato ricordato da un'altra formulazione non riproduce esattamente il codice. σs è il massimo delle barre efficaci, non la media delle tensioni di tutte le barre.

L'area efficace viene costruita sulla zona tesa e sulle barre pertinenti. Nella trazione integrale sono esaminate regioni di bordo o radiali e governa il risultato massimo; non si sommano le aree sovrapposte come se fossero indipendenti. Disposizioni non supportate restano fuori campo; per le superfici interne compatibili valgono le fasce di parete o anello descritte più avanti. La disponibilità della formula va verificata anche rispetto al metodo tensionale e alla presenza di tendini.

### Esempio di fessurazione

Assumere σs = 200 MPa, Es = 200000 MPa, Ecm = 33000 MPa, fctm = 2,9 MPa, ρeff = 0,02, φeq = 16 mm, c = 30 mm, k1 = 0,8, k2 = 0,5 e lunga durata. Si ottiene αe = 6,0606 e Δσ = 65,03 MPa. La deformazione calcolata è 0,0006748, maggiore del minimo 0,0006000.

Il termine vicino vale (102 + 136)/1,7 = 140 mm. La soglia di interasse è 190 mm. Con s = 150 mm, wk = 1,7 × 140 × 0,0006748 = 0,1606 mm. Se s supera 190 mm e h−x = 300 mm, il termine distante vale 225 mm e wk aumenta a 0,2581 mm. Cambiano quindi apertura e ramo governante, pur conservando σs e area efficace nell'esempio.

### Taglio

Senza armatura trasversale, e nel campo ammesso, il motore confronta il termine proporzionale alla radice cubica di 100ρfck con il minimo basato su √fck. k è limitato a 2, ρ a 0,02 e la compressione media a 0,2fcd. La compressione media è positiva benché N nel foglio sia negativo a compressione.

```math
k = \min\left[2;1+\sqrt{\frac{200}{d}}\right]
\rho = \min\left[0{,}02;\frac{A_{sl}}{b_w d}\right]
\sigma_{cp} = \min\left[-\frac{1000N}{A_c};0{,}2f_{cd}\right]
V_{Rd} = \max\left[\frac{0{,}18k\sqrt[3]{100\rho f_{ck}}}{\gamma_c}+0{,}15\sigma_{cp}; 0{,}035 k^{1{,}5}\sqrt{f_{ck}}+0{,}15\sigma_{cp}\right]\frac{b_w d}{1000}
```

Per N di trazione il ramo senza staffe non fornisce automaticamente una resistenza favorevole. Con staffe il modello a traliccio confronta resistenza dell'armatura e del puntone; cot θ è compreso fra 1 e 2,5 e può essere assegnato o determinato dalla procedura automatica.

```math
V_{Rsd} = \frac{z(A_{sw}/s)f_{yd}(\cot\alpha+\cot\theta)\sin\alpha}{1000}
V_{Rcd} = \frac{z b_w\alpha_c\,0{,}5f_{cd}(\cot\alpha+\cot\theta)}{(1+\cot^2\theta)1000}
V_{Rd} = \min(V_{Rsd};V_{Rcd})
```

αc dipende dalla compressione: 1 + σcp/fcd fino a 0,25fcd, 1,25 fino a 0,50fcd e max[0; 2,5(1−σcp/fcd)] oltre tale soglia. Il braccio z deriva dal fattore della geometria. Per il ramo circolare dei pali si adottano 0,75d nella sezione piena e 0,60d nella cava, con bw e d ricavati dalla geometria e dalle barre. Si tratta del ramo specifico documentato, non di un'estensione indistinta a qualunque elemento circolare.

### Torsione e interazione

La sezione resistente a torsione è un circuito periferico chiuso con area Ak, perimetro uk e spessore efficace t. La geometria automatica è disponibile per rettangolo e cerchio, pieni o con foro compatibile. Sono richieste staffe chiuse e armatura periferica adeguata; lo spessore deve contenerne gli assi.

```math
T_{Rcd} = \frac{2A_k t\,0{,}5 f_{cd}\cot\theta}{(1+\cot^2\theta)10^6}
T_{Rsd} = \frac{2A_k(A_{sw}/s)f_{yd}\cot\theta}{10^6}
T_{Rld} = \frac{2A_k(A_{sl,disp}/u_k)f_{yd}}{\cot\theta\,10^6}
T_{Rd} = \min(T_{Rcd};T_{Rsd};T_{Rld})
```

Asl,disp è l'armatura longitudinale disponibile per la torsione dopo la flessione, da assegnare consapevolmente. Taglio e torsione devono usare lo stesso cot θ. Il programma controlla anche la somma |T|/TRcd + |Vx|/VRcd,x + |Vy|/VRcd,y per il calcestruzzo e |T|/TRsd + max(|Vx|/VRsd,x; |Vy|/VRsd,y) per le staffe. L'estensione a due componenti di taglio è una combinazione conservativa del modello, non un dominio normativo generale ricostruito in ogni dettaglio.

### Dettagli e curva momento curvatura

Le verifiche costruttive dipendono dal tipo di elemento scelto. Il modulo confronta geometria, armature e parametri necessari con i limiti implementati e mantiene «Da completare» quando mancano informazioni. La lunghezza di ancoraggio rettilineo parte da lbrqd = φσsd/(4fbd), con i coefficienti di forma e condizioni α1–α5 assunti unitari nel ramo documentato. Le sovrapposizioni introducono α6 e i relativi minimi; i dettagli speciali non vengono ricavati da un disegno ideale della sezione.

La curva M χ viene costruita a N fissato nella direzione assegnata, con passi uniformi o quadratici fino al limite resistente. Ogni punto richiede equilibrio di sezione. In una sezione asimmetrica la direzione del gradiente di deformazione può non coincidere con quella del momento. Non sono inclusi automaticamente softening strutturale, lunghezza della cerniera, rotazione globale o interazione con instabilità dell'elemento.

### Asse neutro e stato resistente

L'asse neutro nel c.a. è il luogo di deformazione nulla del piano affine. Con deformazione uniforme non esiste una retta univoca. L'asse del punto resistente sul dominio e quello della combinazione di esercizio appartengono a stati diversi. Nei tendini la predeformazione può separare lo zero della deformazione geometrica da quello della tensione.

Le superfici interne dei fori sono trattate con fasce di parete o anello, limitate a metà spessore; le facce sono controllate separatamente. Una superficie compressa non richiede apertura delle fessure. Una superficie tesa senza armatura efficace o senza interasse assegnato lascia il controllo incompleto; un superamento già accertato resta sfavorevole.

I coefficienti e i campi dei diversi profili sono distinti nella [matrice del calcestruzzo](wiki:profili-calcestruzzo). La [guida operativa](wiki:guida-sezione-ca) descrive importazioni, stati del calcolo e lettura dei risultati.

## Sezione composta da ponte

### Geometria e omogeneizzazione

Il modello rappresenta una sezione locale composta da carpenteria, soletta e barre opzionali. La carpenteria può essere un H saldato con anima verticale, un H con anima inclinata oppure un cassoncino con due anime simmetriche, due piattabande superiori e un fondo. La seconda piattabanda inferiore è disponibile solo per l'H verticale. Le piastre restano elementi reali, con posizione e geometria proprie. La larghezza efficace beff della soletta è un dato esterno. La piena collaborazione è assunta nel calcolo N–Mx; lo scorrimento non viene introdotto come un grado di libertà del solver di sezione.

Nel metodo elastico si trasforma il contributo del calcestruzzo mediante n = Ea/Ec,eff. La relazione implementata per gli effetti differiti è n = n0(1+ψLφ), con n0 = Ea/Ecm. L'inversione permette di assegnare direttamente n. È richiesto n ≥ n0.

```math
n_0 = \frac{E_a}{E_{cm}}
n = n_0(1+\psi_L\varphi)
\varphi = \frac{n/n_0-1}{\psi_L}
```

Il coefficiente ψL distingue la natura dell'effetto: nel percorso documentato G2 usa 1,1 e il ritiro 0,55. Questi coefficienti non costituiscono una legge completa nel tempo. Un φ assegnato a una fase descrive la rigidezza efficace di quel contributo secondo il metodo scelto.

### Geometria delle anime inclinate e del cassoncino

Indichiamo con hw l'altezza libera verticale, tw lo spessore normale alla lamiera e δ lo scostamento orizzontale fra sommità e piede. Per l'H inclinata δ è positivo verso destra; per il cassoncino è il rientro simmetrico di ogni anima verso l'interno. L'angolo α è misurato dalla verticale. Per evitare confusione con il coefficiente di omogeneizzazione n, il numero di anime è indicato con nw: vale uno per l'H e due per il cassoncino.

```math
\alpha = \arctan\left(\frac{\delta}{h_w}\right)
\ell_w = \sqrt{h_w^2+\delta^2} = \frac{h_w}{\cos\alpha}
t_{w,h} = \frac{t_w}{\cos\alpha}
```

Le anime sono rappresentate come lamiere di spessore normale costante tagliate alle quote orizzontali delle flange. La loro larghezza orizzontale è tw,h, non tw. Il campo attuale Altezza totale H comprende le piattabande: hw è ricavata sottraendo gli spessori superiore, inferiore e, per H verticale, dell’eventuale seconda piastra. Nell’input si assegnano H e tw; non si deve anticipare la trasformazione dello spessore, che il motore esegue internamente. Il campo implementato impone |α| ≤ 45°, equivalente a |δ| ≤ hw. È un limite dell'implementazione geometrica, non una soglia normativa di sicurezza.

Nel cassoncino s_top e s_bottom sono gli interassi fra gli assi delle anime in sommità e al piede. La larghezza bt è quella di ciascuna piattabanda superiore; bb è quella dell'intero fondo. Un valore positivo di δ restringe il fondo, mentre un valore negativo lo allarga, purché la geometria sia valida.

```math
s_{bottom} = s_{top}-2\delta
b_{interno} = s_{bottom}-t_{w,h}
b_{sbalzo} = \frac{b_b-s_{bottom}-t_{w,h}}{2}
```

Per la geometria accettata devono risultare s_top ≥ bt, s_bottom > tw,h e bb ≥ s_bottom + tw,h; inoltre ciascuna piattabanda deve essere più larga dello spessore orizzontale dell'anima. Queste condizioni impediscono sovrapposizione delle flange superiori, contatto delle anime e fondo insufficiente a contenerle. La larghezza interna e gli sbalzi sono netti rispetto agli ingombri delle anime. Il programma applica una piccola tolleranza numerica al controllo di contenimento, che non modifica il significato geometrico delle disuguaglianze.

### Equivalenza per sforzo normale e flessione retta

La distribuzione delle tensioni normali del modello dipende soltanto dalla quota verticale y. A ogni quota dell'anima, una striscia di altezza dy ha area nw tw,h dy. Si possono quindi sostituire le anime inclinate con un'anima verticale equivalente di larghezza totale nw tw,h senza cambiare area, momento statico verticale e inerzia rispetto all'asse orizzontale. Le due flange superiori del cassoncino sono rappresentate da una larghezza complessiva 2bt alla medesima quota. Il fondo mantiene larghezza e spessore reali.

```math
t_{w,eq} = \frac{n_w t_w}{\cos\alpha}
b_{t,eq} = n_f b_t
A_w = n_w t_w\ell_w = t_{w,eq}h_w
A_s = n_f b_t t_t+n_w t_w\ell_w+b_b t_b
```

nf vale uno per l'H e due per il cassoncino; tt e tb sono gli spessori delle flange superiore e inferiore. La formula di As riguarda le nuove sezioni senza seconda piastra. Per ciascuna parte i di area Ai e quota yi, con origine alla sommità dell'acciaio e y negativo verso il basso, si applicano i momenti statici e il teorema di trasporto.

```math
y_G = \frac{\sum_i A_i y_i}{\sum_i A_i}
I_x = \sum_i\left[I_{xi}+A_i(y_i-y_G)^2\right]
```

Per l'insieme delle anime Ixi rispetto all'asse orizzontale del loro baricentro vale Aw hw²/12. Per una flangia orizzontale vale b t³/12, con b larghezza e t spessore; per le due flange superiori si sommano i contributi. La posizione orizzontale delle parti non entra negli integrali rispetto a y. L'identità è esatta per la geometria ideale rappresentata e per la flessione retta considerata.

L'identità non si estende automaticamente a Iy, Ixy, assi principali, torsione o distorsione. L'H inclinata può avere prodotto d'inerzia non nullo: un'analisi generale della carpenteria libera richiederebbe entrambe le curvature e il loro accoppiamento. Qui è assunta la curvatura nel piano verticale, con vincolo laterale da soletta e controventi. Il programma espone le proprietà della sezione reale, ma non usa Ixy per risolvere la flessione deviata. L'ipotesi va motivata anche nelle fasi precedenti alla collaborazione della soletta. La torsione del cassoncino non passa per questa equivalenza: è trattata con il modello di cella chiusa descritto con le verifiche di taglio e connessione, mentre l'H con anima inclinata resta in flessione retta.

### Esempi numerici delle nuove sezioni

Gli esempi riguardano esclusivamente la carpenteria lorda. Per riprodurli nel campo Altezza totale H assegnare 1855 mm per l’H inclinata e 1850 mm per il cassoncino: in entrambi i casi l’altezza libera hw è 1800 mm. Nel primo caso si usano hw = 1800 mm, tw = 14 mm, δ = 300 mm, piattabanda superiore 500 × 25 mm e inferiore 700 × 30 mm. Nel secondo caso si mantengono hw e tw, si assegnano δ = 250 mm, s_top = 1800 mm, due piattabande superiori da 450 × 25 mm e fondo 1400 × 25 mm.

| Grandezza | H con anima inclinata | Cassoncino |
| --- | --- | --- |
| Inclinazione dalla verticale | 9,462322° | 7,907163° |
| Lunghezza di ciascuna anima | 1824,828759 mm | 1817,278185 mm |
| Spessore orizzontale di ciascuna anima | 14,193113 mm | 14,134386 mm |
| Larghezza totale dell'anima equivalente | 14,193113 mm | 28,268772 mm |
| Larghezza superiore complessiva | 500 mm | 900 mm |
| Area dell'acciaio As | 59047,602627 mm² | 108383,789167 mm² |
| Quota del baricentro yG | −1057,244996 mm | −1030,239447 mm |
| Inerzia orizzontale Ix | 33857338759,972 mm⁴ | 60418964803,193 mm⁴ |

Nel cassoncino s_bottom = 1800 − 2 × 250 = 1300 mm; la parte interna netta del fondo è 1285,865614 mm e ogni sbalzo misura 42,932807 mm. La somma della parte interna, dei due ingombri orizzontali delle anime e dei due sbalzi ricostruisce 1400 mm. Per l'H, invertire δ cambia il lato dell'inclinazione ma non As, yG e Ix: è un utile controllo della convenzione dei segni.

Un ulteriore riferimento elastico usa N = −200 kN e M₀ = 100 kNm assegnati alla quota y = 0, sola carpenteria e classe 4 disattivata per isolare l'equivalenza lorda. Convertiti N in newton e M₀ in Nmm, il momento baricentrico e la tensione alla quota y sono:

```math
M_c = M_0+N y_G
\sigma(y) = \frac{N}{A_s}-\frac{M_c(y-y_G)}{I_x}
```

Questo riferimento verifica unità, trasporto del momento e distribuzione delle tensioni; non è un caso di progetto completo. I test dell'aggiornamento lo confrontano con i tre percorsi cumulativo, storico lineare e storico non lineare mantenuti nel campo elastico, anche per δ negativo e per δ nullo. Per la fase composta vanno invece aggiunti soletta e armature con il coefficiente di omogeneizzazione pertinente.

### Campo del modello e compatibilità dei dati

Le geometrie inclinate sono disponibili attraverso lo stesso ingresso dei tre metodi e delle curve di risposta; l'adozione della sezione equivalente non cambia il significato di fasi, carichi incrementali e riferimento al getto. Nei metodi storici i controlli locali di taglio, connessione e accessori restano non valutati; il metodo non lineare conserva il proprio campo istantaneo e lordo. La presenza della nuova forma non estende il campo di verifica del metodo selezionato.

La chiusura superiore del cassoncino mediante soletta attiva il modello torsionale di cella chiusa soltanto quando si abilitano le verifiche a torsione; in tal caso sono calcolati anche distorsione e diaframmi. Restano fuori dal calcolo gli irrigidimenti longitudinali del fondo, la verifica del fondo compresso come piastra irrigidita e la torsione non uniforme del cassone aperto. Il fondo viene trattato come lamiera interna non irrigidita longitudinalmente, con i suoi sbalzi esterni. Instabilità globale e comportamento dell'intero ponte richiedono altri modelli.

Gli archivi senza le chiavi del tipo di sezione continuano a rappresentare H saldato. Il dato di seconda piastra inferiore viene escluso dall'adattatore per H inclinata e cassoncino, anche se era salvato in un precedente H. Il risultato espone sia i parametri equivalenti sia quelli reali; i report riportano le ipotesi e una tabella delle lamiere. Non si deve usare un valore equivalente come dimensione esecutiva della singola lamiera.

### Trasporto delle azioni

Il riferimento del momento deve essere coerente con il punto di applicazione di N. Il codice riporta il momento al riferimento comune attraverso la quota yN espressa in millimetri.

```math
M_{x0} = M_x-\frac{N y_N}{1000}
```

Con N = 1000 kN e una differenza di quota di 200 mm, il trasporto modifica il momento di 200 kNm con il segno stabilito dalla formula. Trascurare questa operazione può spiegare differenze rilevanti fra due calcoli che hanno la stessa sezione e gli stessi valori nominali di N e M.

È possibile riferirsi al baricentro lordo della fase, a quello efficace aggiornato o a un riferimento comune. Nel secondo caso l'eccentricità cambia durante l'iterazione delle larghezze efficaci. La scelta fa parte del problema fisico e deve essere riportata insieme alle azioni.

### Metodo cumulativo

Ogni incremento viene analizzato con il proprio coefficiente di omogeneizzazione e la propria situazione di collaborazione. Le tensioni vengono sommate sulla configurazione efficace comune risultante dall'iterazione. Questo metodo è adatto alla sovrapposizione elastica prevista dall'implementazione, ma non conserva la stessa memoria delle deformazioni di un'analisi cronologica.

La riduzione locale delle piastre dipende dalle tensioni complessive, per cui geometria efficace e tensioni vengono aggiornate iterativamente. L'aggiunta di una fase modifica anche la geometria efficace comune sulla quale sono valutati i contributi. Non è quindi corretto aspettarsi che le tensioni di una fase precedente restino sempre identiche al calcolo eseguito isolatamente prima dell'aggiunta.

Il cumulativo dispone dei controlli aggiuntivi di taglio, appoggi, irrigidimenti e connessione. Le azioni in ingresso sono già quelle della combinazione da verificare; la scelta SLU o SLE seleziona limiti e percorsi di controllo, senza costruire i carichi fattorizzati.

### Metodo storico lineare

Il metodo conserva il riferimento di deformazione al momento del getto e gli stati incrementali. La soletta attivata in una fase non acquisisce retroattivamente le tensioni dovute ai carichi applicati alla carpenteria prima della sua collaborazione. Il percorso cronologico diventa quindi parte dei dati del problema.

φ e n si applicano ai nuovi incrementi. Cambiare il coefficiente di una fase futura non produce automaticamente il rilassamento nel tempo di tutti gli stati precedenti. Il metodo non è un integratore reologico completo con storia di età, umidità, maturazione e viscosità per ogni giorno. La riduzione locale di classe 4 è disponibile, mentre i controlli accessori di taglio e connessione non sono valutati in questo percorso.

### Metodo storico non lineare

La sezione è discretizzata a fibre e l'equilibrio N Mx viene risolto seguendo la storia. L'acciaio adotta una legge bilineare con incrudimento isotropo, memoria plastica e scarico elastico. Il calcestruzzo usa l'inviluppo del materiale tabulato disponibile nella libreria, senza una legge completa di danno ciclico e degradazione.

Il calcolo è istantaneo, su sezione lorda e con proprietà caratteristiche. Non applica la riduzione locale di classe 4 e non sostituisce le verifiche di instabilità con la plasticità delle fibre. Il percorso non ammette di simulare la viscosità alterando arbitrariamente il modulo di un materiale plastico attraverso n. Le impostazioni iniziali usano 160 suddivisioni nell'anima, 8 nelle flange, 64 nel cls e 8 sottopassi per incremento; la sensibilità numerica va controllata nei casi impegnativi.

La memoria plastica comporta che due sequenze con la stessa risultante finale possano produrre stati diversi. Un ciclo carico scarico può lasciare deformazioni e tensioni residue. Ciò non implica che il modello descriva automaticamente una prova a fatica, la rottura oligociclica o il degrado del calcestruzzo confinato.

### Ritiro

Il ritiro viene assegnato come deformazione propria del calcestruzzo, negativa per accorciamento. La procedura elastica costruisce una forza equivalente Ec,eff Ac Δεcs applicata al baricentro del cls netto e una correzione di tensione propria −Ec,eff Δεcs. L'insieme riproduce l'incompatibilità locale mantenendo l'equilibrio della sezione con le risultanti esterne previste.

```math
N_{eq} = E_{c,eff}A_c\Delta\varepsilon_{cs}
\Delta\sigma_{c,propria} = -E_{c,eff}\Delta\varepsilon_{cs}
```

Le unità vanno rese coerenti prima della conversione in kN. Il solo stato della sezione non determina le forze secondarie causate da vincoli longitudinali di una trave continua. Analogamente, gli scorrimenti concentrati presso le estremità richiedono un modello lungo l'asse del ponte o una domanda aggiuntiva assegnata.

### Larghezze efficaci e convergenza

Per le anime inclinate la riduzione si calcola sulla lamiera di lunghezza ℓw e spessore normale tw, usando le tensioni ai due estremi. Se la fascia efficace lungo la lamiera misura beff,w, l'altezza verticale corrispondente è beff,w cos α. Associare questa altezza alla larghezza equivalente nw tw/cos α conserva l'area efficace delle nw lamiere. Non si deve calcolare la snellezza locale usando hw e tw,eq: sarebbe una piastra diversa da quella reale.

Nel cassoncino ogni piattabanda superiore ha due sbalzi rispetto alla propria anima. Il fondo è suddiviso in una lamiera interna fra le anime e due sbalzi esterni. Alla lamiera interna uniformemente compressa il modello applica kσ = 4; la sua riduzione è distinta da quella degli sbalzi. Le porzioni occupate dagli ingombri orizzontali delle anime vengono ricomposte con le larghezze efficaci delle parti libere. Non viene simulato un eventuale sistema di irrigidimenti longitudinali del fondo.

Per le piastre compresse si valuta il rapporto di tensione ψ, il coefficiente di instabilità kσ, la snellezza adimensionale e il fattore ρ. Le porzioni efficaci vengono poi ricollocate nella sezione e si ricalcolano proprietà e tensioni. La snellezza locale usa fy caratteristico, non fyd. Le piattabande aggiunte vengono valutate con i propri sbalzi.

Il significato di ρ è una riduzione della porzione resistente nella verifica elastica della piastra snella. Non è una riduzione fisica del peso e non comporta rimozione di acciaio dal disegno costruttivo. La sezione lorda continua a governare i quantitativi e alcune altre grandezze.

L'iterazione utilizza una tolleranza dell'ordine di 10⁻⁷ e massimo 120 passi, con rilassamento e accelerazione secondo la procedura corrente. Il rilassamento iniziale è 0,55, limitato nel campo ammesso. Si controlla anche un residuo di equilibrio relativo dell'ordine di 10⁻⁵. Un arresto al numero massimo di iterazioni non è una convergenza positiva.

### Limiti di tensione e interpretazione delle curve

Il percorso elastico di esercizio confronta l'acciaio strutturale con fy, il cls con i limiti 0,60fck o 0,45fck secondo la situazione e l'armatura con 0,80fyk nel percorso previsto. L'esclusione della soletta tesa è una scelta del modello; l'armatura può restare attiva. Nel cumulativo non viene introdotta automaticamente una fessurazione parziale della soletta tale da risolvere ogni distribuzione tesa.

Le curve M κ a N costante e N ε a curvatura costante possono partire da stato vergine o dalla ricostruzione della storia fino a una fase. Il motore conserva la memoria prevista dallo storico non lineare. Il risultato è una risposta caratteristica della sezione lorda: per trasformarlo in una capacità di progetto servono le verifiche e i coefficienti pertinenti. Punti non convergenti non vengono sostituiti da un inviluppo artificiale.

### Interpretare l'asse neutro nelle fasi

Nel metodo cumulativo la linea indica lo zero delle tensioni cumulate nell'acciaio. Nello storico lineare e non lineare indica lo zero della deformazione totale del piano di fase. Ritiro, attivazione dei materiali e plasticità possono separare gli zeri delle tensioni nei diversi materiali: non esiste necessariamente un asse tensionale comune a soletta e carpenteria.

La vista delle curve N–ε o M–χ interroga un percorso di sezione. Non rappresenta automaticamente duttilità globale del ponte, instabilità o una legge ciclica. Dati, fase e metodo devono accompagnare il grafico esportato.

La [guida alla sezione composta](wiki:guida-sezione-composta-da-ponte) descrive la sequenza degli input; [taglio e connessione](wiki:taglio-irrigidimenti-e-connessione-della-sezione-composta) hanno controlli separati.

## Taglio irrigidimenti e connessione della sezione composta

### Resistenza a taglio

Questi controlli sono attivi nel percorso cumulativo e adottano le edizioni identificate dall'interfaccia: NTC 2018 e norme EN richiamate nella documentazione del modulo. L'anima intera partecipa al taglio, senza includere un contributo resistente della soletta. L'area plastica assunta cautelativamente è hw tw. Il contributo favorevole delle flange alla resistenza per instabilità a taglio viene omesso.

```math
\tau_{cr} = \frac{k_{\tau}\pi^2 E}{12(1-\nu^2)}\left(\frac{t_w}{h_w}\right)^2
\lambda_w = \sqrt{\frac{f_y}{\sqrt{3}\tau_{cr}}}
V_{pl,Rd} = \frac{h_w t_w f_y}{\sqrt{3}\gamma_{M0}}
V_{bw,Rd} = \frac{\chi_w h_w t_w f_y}{\sqrt{3}\gamma_{M1}}
V_{Rd} = \min(V_{pl,Rd};V_{bw,Rd})
```

kτ dipende dal rapporto del pannello e dalla validità degli irrigidimenti; χw segue la curva del montante terminale applicabile. Senza intermedi idonei si adotta il pannello lungo. Il montante rigido richiede la verifica positiva del dettaglio previsto, non la sola selezione del nome. I coefficienti iniziali documentati sono γM1 = 1,10, γV = 1,25 ed η = 1,20; γM0 è 1,05 per NTC e 1,00 per il percorso EC. L'utente può modificarli e deve controllare l'Appendice Nazionale pertinente.

Le formule precedenti descrivono una singola anima verticale. Per H inclinata e cassoncino la resistenza e l'instabilità della singola lamiera si calcolano sostituendo alla sua altezza la lunghezza reale ℓw, mantenendo lo spessore normale tw. Il taglio immesso nelle fasi è invece la componente verticale totale V. Con ripartizione uguale fra le nw anime, la domanda nel piano di una lamiera e la resistenza verticale complessiva sono:

```math
V_{lamiera} = \frac{V}{n_w\cos\alpha}
V_{Rd,verticale} = n_w\cos\alpha\,V_{Rd,lamiera}
\tau_{media} = \frac{V}{n_w h_w t_w}
```

L'ultima formula richiede V in N e dimensioni in mm. Deriva da V_lamiera/(tw ℓw); il coseno si semplifica perché ℓw = hw/cos α. La tensione media non coincide in generale con il massimo della distribuzione V S/(I t). Nei due esempi, con V = 600 kN, la domanda per lamiera è 608,276253 kN per l'H e 302,879697 kN per il cassoncino; le tensioni medie sono 23,809524 e 11,904762 MPa. La snellezza e τcr continuano a dipendere da ℓw: l'inclinazione non può essere cancellata nella verifica di instabilità.

### Interazione con il momento e tensioni tangenziali

Oltre 0,5 VRd il taglio può ridurre il margine flessionale. Per il campo N = 0, fy ≤ 355 MPa e anima non interamente compressa, si calcolano capacità plastiche di riferimento integrando flange efficaci, anima intera e soletta compressa. La capacità Mf omette l'anima. Il cls teso è nullo e le barre sono omesse cautelativamente in queste capacità di riferimento. Le verifiche elastiche della sezione non vengono sostituite da tali integrazioni plastiche.

Negli altri casi ad alto taglio viene usato un inviluppo elastico cautelativo con Mf = 0, segnalato esplicitamente. Non è un dominio plastico esatto N M V per qualunque sezione. Le tensioni tangenziali includono la media già definita e il massimo del campo elastico, trasformato nel piano dell'anima reale, con somma algebrica dei contributi di fase.

L'inviluppo √(max|σ|² + 3 max|τ|²) è un controllo aggiuntivo conservativo; i due massimi possono trovarsi in punti diversi. Non sostituisce instabilità del pannello, verifiche degli appoggi o fatica.

### Irrigidimenti appoggi e saldature

Il modello ammette piatti mono o bilaterali anche differenti, pannelli adiacenti diversi, appoggi interni o terminali, eccentricità della reazione, montante rigido a due coppie e saldature continue. Le verifiche di pressoflessione elastica includono un'amplificazione del secondo ordine e imperfezioni equivalenti nel campo del metodo implementato. La lunghezza critica parte dal rapporto Lcr/L assegnato, inizialmente 1,00.

Un irrigidimento insufficiente non aumenta la resistenza del pannello a taglio. Le azioni di traversi, intagli, concentrazioni locali o dettagli non inseriti non vengono ricavate dalla sola sezione trasversale. La capacità di un piatto non dimostra da sola che anima, flangia e saldature trasferiscano l'intera reazione.

Con anima inclinata le dimensioni di controllo di irrigidimenti e saldature seguono la lamiera reale, non l'altezza dell'anima equivalente. Per il cassoncino l'implementazione ripartisce la reazione d'appoggio assegnata fra le due anime e usa dettagli per anima; questa ipotesi non sostituisce l'analisi di un appoggio eccentrico che solleciti in modo diverso le due pareti. Con le verifiche a torsione attive il torcente trasferito agli apparecchi aggiunge la coppia T/e_b alla metà della reazione sull'anima più caricata; altre distribuzioni trasversali devono essere valutate separatamente.

### Resistenza e domanda dei pioli

Il ramo disponibile considera pioli a testa saldata, distribuzione uniforme, soletta piena ordinaria C20/25–C60/75, diametro 16–25 mm e altezza almeno 3d. La resistenza è il minore dei due meccanismi, acciaio e calcestruzzo, con fu limitato a 500 MPa. Il fattore α dipende da h/d nel campo previsto.

```math
P_{Rd} = \frac{\min\left[0{,}8f_u\pi d^2/4;0{,}29\alpha d^2\sqrt{f_{ck}E_{cm}}\right]}{\gamma_V}
q = \sum_i\left(\frac{V_i S_i}{I_i}+\Delta q_i\right)
P_{Ed} = \frac{|q|\,passo}{n_{pioli}}
```

La prima formula dà una forza in N con dimensioni in mm e tensioni in MPa; la domanda deve essere convertita coerentemente. q è il flusso longitudinale, passo la distanza fra file e npioli il numero per fila. Le fasi prima della collaborazione del cls non caricano la connessione nel modello. Δq consente di introdurre contributi longitudinali ottenuti da altra analisi.

V S/I presuppone proprietà costanti nel tratto e N costante lungo l'asse. Un'introduzione locale di N o una variazione di sezione non è descritta dal solo valore del taglio. Il ritiro uniforme della sezione non produce da solo V, mentre le sue zone di trasferimento possono richiedere un Δq specifico.

Nel percorso NTC si usano le proprietà della fase tensionale, inclusa l'eventuale esclusione della soletta. Nel percorso EC4 lo scorrimento considera la soletta non fessurata e la carpenteria efficace, con il φ o n pertinente. Si tratta di una distinzione di proprietà della fase, non di un passaggio nascosto ad acciaio completamente lordo.

Nel cassoncino q è il flusso totale della sezione e la ripartizione simmetrica assegna q/2 a ciascuna piattabanda superiore. Il numero npioli immesso è quello per fila di una singola piattabanda. Indicando con nf il numero di piattabande superiori, la formula generale adottata è:

```math
P_{Ed} = \frac{|q|\,passo}{n_f n_{pioli}}
```

nf vale uno per l'H e due per il cassoncino. Per esempio q = 100 N/mm, passo = 200 mm e due pioli per fila e per piattabanda producono 5000 N, cioè 5 kN per piolo, nel cassoncino; una sola piattabanda con due pioli riceverebbe 10 kN per piolo. Il controllo del bordo usa la larghezza della singola piattabanda e i controlli della soletta trasversale usano il flusso a essa attribuito. I flussi minimo e massimo assegnati alla fatica dei pioli si riferiscono già alla piattabanda: non sono automaticamente interpretati come flussi totali da dimezzare.

Con la torsione del cassoncino il flusso torsionale della soletta qT, somma delle fasi composte, si trasferisce attraverso i pioli di ciascuna piattabanda con verso opposto sulle due anime. Sulla piattabanda in cui si somma al flusso di flessione la domanda diventa:

```math
P_{Ed} = \frac{(|q|/n_f+|q_T|)\,passo}{n_{pioli}}
```

Con q = 100 N/mm, qT = 50 N/mm, passo 200 mm e due pioli per fila si ottengono 10 kN per piolo. Il flusso qT si aggiunge anche alla superficie a–a interna alla cella di ciascuna piattabanda e alle superfici b–b.

### Servizio dettagli e fatica

Il limite 0,75 PRd viene controllato nella situazione caratteristica di esercizio prevista; non viene trasferito automaticamente a una combinazione quasi permanente. I dettagli comprendono passi longitudinali e trasversali, bordi, testa, copriferro, posizione rispetto alle barre, rapporto fra diametro e spessore della flangia e armature trasversali.

I passi minimi implementati sono 5d longitudinale e 2,5d trasversale, con massimo longitudinale min(800 mm; 4hc). Le condizioni di azioni ripetute e fatica attivano il limite pertinente d ≤ 1,5tf; per il campo statico disponibile è adottato cautelativamente 2,5tf. Testa e distanze non sono dettagli ornamentali: possono governare la validità del collegamento anche con PEd basso.

Sono presenti controlli dell'armatura trasversale, superfici di scorrimento, ancoraggio e fatica resistente dei pioli con interazione della flangia tesa, quando i dati necessari sono assegnati. Restano fuori campo, fra gli altri, sollevamento, splitting attraverso lo spessore, gruppi non uniformi, mensole locali e lamiere grecate. Una verifica non alimentata con le escursioni e i dati di fatica non ricava autonomamente lo spettro di traffico.

### Torsione del cassoncino

Con le verifiche a torsione attive il cassoncino è una cella singola chiusa. Il momento torcente ΔT di ciascuna fase produce il flusso di St. Venant della formula di Bredt, costante lungo il perimetro e, in una cella singola, indipendente dagli spessori delle pareti. A0 è l'area racchiusa dalle linee medie. Nelle fasi composte la parete superiore è la soletta al suo piano medio, con le anime prolungate fino a esso; nelle fasi di solo acciaio è il controvento orizzontale di spessore equivalente t* al piano medio delle piattabande superiori. Con t* = 0 la cella è aperta e il flusso della fase non viene calcolato.

```math
q = \frac{T}{2A_0}
A_0 = \frac{(b_{sup}+b_{inf})h_0}{2}
J = \frac{4A_0^2}{\sum_i(\ell_i/t_i)}
n_G = \frac{n(1+\nu_c)}{1+\nu_a}
```

b_sup e b_inf sono le distanze fra gli assi delle anime prolungate ai piani delle pareti superiore e inferiore, h0 la distanza verticale fra i due piani. Nella rigidezza J la soletta ha spessore hc/nG, con νc = 0,2, νa = 0,3 e il coefficiente n della fase comprensivo della viscosità (EN 1994-2 §5.4.2.2(11)); con soletta esclusa lo spessore è dimezzato, come per la soletta fessurata della EN 1994-2 §5.4.2.3(6). J serve al modello globale e non modifica q.

I flussi delle fasi si sommano con il proprio segno. Anime e fondo ricevono il flusso di tutte le fasi, soletta e connessione quello delle sole fasi composte, il controvento quello delle fasi di solo acciaio. La tensione tangenziale di torsione in una parete di spessore t è q/t. Nell'anima più caricata il taglio di flessione e quello di torsione si sommano (EN 1993-1-1 §6.2.7(9)); il risultato è riportato alla componente verticale totale per il confronto con la resistenza e per l'interazione con il momento.

```math
\tau_T = \frac{q}{t}
V_{lamiera} = \frac{V}{n_w\cos\alpha}+q\ell_w
V_{eq} = n_w\cos\alpha\,V_{lamiera}
```

Nell'inviluppo elastico dell'anima q/tw si aggiunge al massimo di V S/(I t). Nel fondo si controllano la tensione equivalente al nodo con l'anima, l'imbozzamento a taglio del pannello compreso fra due diaframmi con η = 1 e l'interazione della EN 1993-1-5 §7.1(5) con Mf,Rd nullo. Il taglio di flessione del fondo usa il momento statico della sua metà interna; nell'imbozzamento si adotta il τ medio del pannello, non inferiore a metà del massimo.

```math
\tau_b = \frac{q}{t_b}+\frac{V S_{fondo}}{I t_b}
\eta_1+(2\eta_3-1)^2\leq1
```

Nella soletta il flusso si somma alla superficie a–a interna alla cella di ciascuna piattabanda e alle superfici b–b attorno ai pioli, con lo stesso angolo θ dei puntoni. L'armatura longitudinale deve assorbire la trazione q cotθ per unità di larghezza (EN 1992-1-1 §6.3.2(3)), confrontata con la compressione disponibile del calcestruzzo e con la capacità residua delle barre oltre la flessione; senza armatura trasversale attiva si usa cotθ = 1,25.

```math
q\cot\theta\leq\max(0;-\sigma_{c,media})h_c+\sum_i\frac{A_{s,i}}{s_i}\left[f_{yd}-\max(0;\sigma_{s,i})\right]
```

All'appoggio il diaframma riceve il flusso perimetrale e lo porta agli apparecchi. La piastra del diaframma è in taglio puro, con A0 della cella di acciaio a favore di sicurezza; la reazione verticale degli apparecchi comprende la coppia del torcente, che si somma a R/2 sull'anima più caricata. Se gli apparecchi non sono allineati alle anime la flessione del diaframma non è verificata e viene segnalata.

```math
\tau_D = \frac{T}{2A_0 t_D}
\Delta R = \frac{T}{e_b}
```

L'esempio riprende il cassoncino del capitolo precedente (δ = 250 mm, s_top = 1800 mm, piattabande 450 × 25 mm, fondo 1400 × 25 mm) con la soletta 3000 × 250 mm e le fasi iniziali del foglio: G1 di solo acciaio con t* = 4 mm, G2 composta con φ = 2 e ψL = 1,1, Q composta a breve termine. Per la cella composta b_sup = 1841,667 mm, b_inf = 1296,528 mm e h0 = 1962,5 mm; per la cella di acciaio b_sup = 1803,472 mm e h0 = 1825 mm.

| Grandezza | G1 solo acciaio | G2 composta | Q composta |
| --- | --- | --- | --- |
| ΔT [kNm] | 200 | 300 | 1000 |
| A0 [m²] | 2,828750 | 3,079353 | 3,079353 |
| q [kN/m] | 35,3513 | 48,7115 | 162,3718 |
| J [m⁴] | 0,041788 | 0,080873 | 0,100659 |

Il flusso cumulato vale 246,4346 kN/m nelle anime e nel fondo, con τT = 17,6025 MPa nelle anime da 14 mm e 9,8574 MPa nel fondo da 25 mm; nella soletta agiscono 211,0833 kN/m. Con T = 1500 kNm all'appoggio, tD = 15 mm ed e_b = 1300 mm risultano τD = 17,6757 MPa e ΔR = 1153,846 kN. I valori sono confrontati con il calcolo del programma nei controlli dell'aggiornamento.

### Distorsione e diaframmi

La distorsione è la deformazione della sezione trasversale del cassone prodotta dalla parte dei carichi eccentrici che la cella non assorbe per torsione. Il modello segue l'analogia della trave su suolo elastico di Wright, Abdel-Samad e Robinson (1968). Il modo distorsivo è il meccanismo delle quattro pareti incernierate negli spigoli con scorrimento nullo in ogni parete: gli spostamenti tangenziali Vi delle pareti di lunghezza ℓi soddisfano la condizione di chiusura, per cui i flussi di St. Venant non compiono lavoro sul modo. L'ingobbamento ω è lineare su ogni parete con pendenza Vi ed è reso ortogonale agli ingobbamenti di sforzo normale e flessione; mensole della soletta, sbalzi del fondo e piattabande superiori partecipano agli integrali, con la soletta divisa per n0. Il modo è normalizzato con la media dei valori assoluti delle variazioni degli angoli agli spigoli, che nel rettangolo coincide con la distorsione γ.

```math
\sum_i\ell_i V_i = 0
\int\omega t\,ds = \int\omega x t\,ds = \int\omega y t\,ds = 0
I_{Dw} = \int\omega^2 t\,ds
```

La rigidezza a telaio K per unità di lunghezza deriva dal telaio trasversale a nodi rigidi deformato secondo il modo, con rigidezze flessionali D = E t³/[12(1 − ν²)] di anime, fondo e soletta. I diaframmi intermedi sono molle KD: per la piastra si usa l'energia dello stato piano di tensione con i bordi mossi dalle pareti, per il controvento a X l'energia delle due diagonali. Un torcente applicato come coppia verticale alla sommità delle anime produce il carico generalizzato p = T (V_dx − V_sx)/b_sup, dove V_dx e V_sx sono gli spostamenti verticali del modo agli spigoli superiori.

```math
E I_{Dw}\psi''''+K\psi = p
\sigma_{dw} = E\omega\psi''
m = m_1\psi
```

Per un rettangolo b × h di spessore costante t valgono le forme chiuse seguenti, usate come controllo del programma insieme a un modello a telaio indipendente e alle formule di Yoo et al. (SSRC 2015) per il trapezio. Per il controvento a X del trapezio la rigidezza coincide con quella della letteratura dopo il cambio di normalizzazione dell'angolo; per la piastra la formula semplificata a taglio uniforme è dal 5 al 10% più bassa del calcolo con i bordi mossi dalle pareti.

```math
I_{Dw} = \frac{t(b+h)b^2 h^2}{96}
K = \frac{24}{b/D_h+h/D_v}
K_D = G t b h
K_D = \frac{2E A b^2 h^2}{L^3}
```

La trave su suolo elastico è la campata semplicemente appoggiata con diaframmi d'estremità rigidi, discretizzata con elementi cubici e con le molle dei diaframmi intermedi. m_t agisce su tutta la luce e T_c nella posizione più sfavorevole, con lo stesso segno; si ricavano gli inviluppi di σdw agli spigoli e agli sbalzi, del momento trasversale agli spigoli e della distorsione ai diaframmi. Secondo la EN 1993-2 §6.2.7(3) σdw viene sommata alle verifiche del fondo quando supera il 10% della tensione di flessione. I nodi anima–fondo e anima–piattabanda superiore combinano sempre σx con σdw, la flessione trasversale σz = 6m/t² con il segno sfavorevole e τ.

```math
\sigma_{eq} = \sqrt{\sigma_x^2+\sigma_z^2+|\sigma_x\sigma_z|+3\tau^2}
```

I diaframmi a piastra sono verificati a taglio con imbozzamento (EN 1993-1-5 §5 con η = 1) e a tensione equivalente; le diagonali dei controventi a X in compressione con la curva c e Lcr = β L. Aperture, collegamenti, aste del controvento superiore e accoppiamento fra distorsione e ingobbamento torsionale restano fuori dal modello, come nell'analogia originale. Gli inviluppi della distorsione non dipendono dalle fasi e sono calcolati nelle situazioni con la soletta.

Nell'esempio del paragrafo precedente, con luce 40 m, diaframmi a piastra da 12 mm ogni 5 m, m_t = 60 kNm/m e T_c = 600 kNm, si ottengono i valori seguenti. σdw supera il 10% della flessione e viene sommata nelle verifiche del fondo.

| Grandezza | Valore |
| --- | --- |
| IDw | 0,010042 m⁶ |
| K | 607,168 kN·m/m |
| KD della piastra | 2872,26 MN·m |
| Carico generalizzato di un torcente unitario | 0,341377 |
| Diaframmi intermedi | 7 |
| ψ massimo | 2,762·10⁻⁴ rad |
| σdw al fondo | 14,946 MPa, pari al 20,2% della flessione |
| Momento trasversale ai nodi inferiori | 0,0434 kNm/m |
| τ nel diaframma più sollecitato | 8,394 MPa |

## Bridge Design

Il modello costruisce e confronta alternative parametriche. La graduatoria riguarda soltanto le combinazioni ammesse ed esplorate: non dimostra la sicurezza del ponte né l’ottimo fra tutte le soluzioni realizzabili.

### Ambito del motore e significato di ottimizzazione

Bridge Design usa il motore parametrico BridgeConcept di ANTHEA. I moduli Sezione in c.a. e Sezione composta da ponte impiegano invece le librerie indicate nell’interfaccia come GPC Engine. L’ottimizzazione di Bridge Design non richiama automaticamente quelle verifiche di sezione. La presenza delle librerie GPC non trasforma il predimensionamento in una verifica strutturale completa.

L’ottimizzatore è una ricerca discreta deterministica: genera combinazioni, calcola ciascuna, applica filtri e ordina quelle rimaste. Non usa apprendimento automatico, un modello addestrato sui risultati del sito, un algoritmo genetico o una ricerca continua per gradiente. A parità di dati, opzioni e versione del motore produce gli stessi risultati. Non promette di trovare una soluzione compresa fra due valori della griglia o appartenente a una tipologia non modellata.

La soluzione ammissibile è una soluzione che supera i filtri interni elencati nel seguito. Il termine non equivale a verificata secondo NTC, Eurocodici o AASHTO. Nel modello non esiste un filtro generale che confronti il momento flettente con la resistenza dell’impalcato, né un limite automatico di freccia per tutte le tipologie. Una graduatoria economica può quindi favorire una sezione che richiederà un aumento di materiale nel progetto strutturale.

### Dati esterni e loro ingresso nel calcolo

I dati esterni entrano attraverso i campi del progetto e il listino. Durante la ricerca non vengono interrogati il sito, ANAS, un servizio geologico o una banca dati di ponti. I valori iniziali sono contenuti nel codice e restano modificabili; un archivio riaperto conserva i propri prezzi e coefficienti. L’utente deve aggiornare consapevolmente le ipotesi quando cambia sito, anno economico, fornitore o prestazione richiesta.

| Dato | Origine prevista | Effetto nel modello |
| --- | --- | --- |
| Lunghezza quota e larghezza | Rilievo e requisiti funzionali | Geometria campate volumi e carichi |
| Ostacolo e sua larghezza | Vincoli del sito | Spostamento o esclusione degli appoggi interferenti |
| Classe del terreno e resistenze | Valori convenzionali oppure studio geotecnico | Fondazione automatica e soglie assiali |
| Carichi equivalenti e moltiplicatori | Ipotesi dell’utente | Sollecitazioni reazioni e dimensioni automatiche |
| Prezzi unitari | Prezzari e preventivi | Costo a parità di quantità |
| Fattori ambientali | Convenzioni oppure EPD coerenti | Indicatore parziale di CO₂ |

Per dati geotecnici reali non basta scegliere la classe più simile al terreno. Pressione di riferimento, resistenza laterale e resistenza di punta devono essere coerenti con il tipo di fondazione, le profondità, la falda e il livello di cautela adottato. Il modello usa valori uniformi e non ricostruisce una stratigrafia. Non applica automaticamente i coefficienti geotecnici di una combinazione normativa: i valori di classe sono già trattati come riferimenti convenzionali ridotti, senza una tracciabilità normativa completa.

I prezzari servono a verificare l’ordine di grandezza delle tariffe e le inclusioni delle lavorazioni. Non forniscono la geometria ottima. Il sito TheBridgeEng è stato un riferimento di interfaccia e un termine di confronto numerico; i suoi risultati non vengono utilizzati come obiettivi da inseguire durante una ricerca.

### Variabili libere e grandezze conservate

Restano sempre costanti la lunghezza totale, la quota sul terreno, la composizione della larghezza, l’ostacolo, il terreno, le resistenze dei materiali, i carichi, i prezzi, i fattori ambientali e gli estremi su spalla o pila. I parametri delle strutture speciali, come freccia dell’arco, altezza antenna, freccia dei cavi e canalette, sono ereditati dal progetto: non sono assi autonomi della griglia.

Si possono liberare tipologia, numero di campate, altezza, sezione standard, schema di pila, fondazione e continuità. La continuità è bloccata per impostazione iniziale. Liberare la sezione significa adottare i parametri standard della famiglia per i campi ordinari della sezione; non significa ottimizzare individualmente ogni spessore metallico o ogni interasse. I parametri avanzati rimangono quelli del progetto anche quando si libera la sezione.

Bloccare la sezione richiede di bloccare la tipologia. Il programma congela le quote ordinarie adottate, inclusi soletta, interasse, anima e fondo ove pertinenti. Bloccare l’altezza congela quella in campata; per il cassone variabile l’altezza sulle pile continua a dipendere dalla luce. Bloccare il numero delle campate non introduce una scelta libera delle singole luci: queste seguono la distribuzione prevista dal modello e l’eventuale ostacolo.

Il blocco di pile e fondazioni conserva le quote manuali. Le quote lasciate a zero rimangono regole automatiche e possono cambiare se cambiano le reazioni. Il blocco della fondazione conserva il tipo effettivamente adottato e la lunghezza dei pali; numero dei pali e dimensione del plinto restano automatici se lo erano all’origine. Questo comportamento va distinto dal congelamento di un intero progetto esecutivo delle fondazioni.

### Costruzione della griglia di ricerca

Il numero di campate esplorabile va da 1 a 30, entro minimo e massimo impostati. Per ciascuna famiglia si fa un primo controllo sulla luce media L/n; successivamente vengono controllate le luci effettive, che possono essere diverse dalla media. Strallato e sospeso usano soltanto tre campate, con luce centrale pari a metà della lunghezza totale. La griglia completa preliminare non può superare 50.000 tentativi.

Le griglie percentuali dell’altezza e della lunghezza dei pali ammettono valori da 100% a 200%, con passo da 1 a 100 punti percentuali e al massimo 11 valori per griglia. L’estremo superiore è incluso anche quando il passo non divide l’intervallo: 100–115 con passo 10 genera 100, 110 e 115%. Il riferimento dell’altezza è il predimensionamento della singola combinazione; quello dei pali è la lunghezza convenzionale della classe del terreno.

L’impostazione iniziale esplora altezze 100 e 115%, pali 100, 125 e 150%, campate da 1 a 12. Per i pali la lunghezza generata non supera 80 m. Le fondazioni libere sono plinto diretto, pali da 1,0 m e pali da 1,5 m; la voce Automatica viene risolta e non costituisce un quarto tipo da classificare.

Le altezze aumentate vengono arrotondate verso l’alto a passi di 0,05 m; il valore al 100% conserva la quota automatica non arrotondata. La tolleranza numerica evita che una quota teorica esatta sul passo venga aumentata per il solo rumore di rappresentazione.

La configurazione corrente viene aggiunta come riferimento prima della griglia, anche se fuori dagli intervalli percentuali. Deve comunque soddisfare i filtri, compresi i limiti assoluti di campate e altezza. Il numero di tentativi previsto è un limite superiore: salti di combinazioni incompatibili e rimozione di duplicati possono ridurlo. I tentativi ammessi possono essere più delle geometrie distinte, perché la stessa geometria può essere rappresentata con quote automatiche o esplicite.

### Geometria longitudinale e larghezza

Indicando con nc il numero delle corsie, bc la larghezza della corsia, b la banchina per lato, m lo spartitraffico e bb l’ingombro della barriera per lato, la larghezza complessiva W è:

```math
W = n_c\cdot b_c+2b+m+2b_b
```

Per le famiglie ordinarie, n campate semplicemente appoggiate sono uguali. Se l’impalcato è continuo e n è maggiore di 2, le due campate terminali hanno peso 0,8 e le interne peso 1. La lunghezza di ciascuna campata è la lunghezza totale moltiplicata per il proprio peso e divisa per la somma dei pesi. I ponti con antenne adottano invece L/4, L/2, L/4. Arco con catena e reticolare hanno campate indipendenti.

L’ostacolo è centrato sulla lunghezza del ponte e viene ampliato di 1 m per lato. Il programma prova a spostare gli appoggi interni ai bordi di questa fascia, senza creare campate inferiori a 2 m. Se non può farlo, conserva o segnala la disposizione incompatibile e la ricerca la esclude. Non vengono letti un tracciato planimetrico, una curva d’alveo, il franco idraulico o l’erosione. Il margine di 1 m è geometrico e non costituisce un franco di progetto.

### Regole di altezza per le quattordici famiglie

Per le famiglie senza struttura superiore l’altezza automatica d è il maggiore fra un minimo e Lmax/r moltiplicato per 0,95 in continuità o 1,10 con campate indipendenti. Per arco, reticolare, strallato e sospeso il moltiplicatore è 1. Per strallato e sospeso Lmax indica la luce centrale. Sono regole convenzionali interne, non risultati di una verifica resistente né rapporti prescritti universalmente dalle norme.

```math
d = \max\left(d_{\min};\frac{k L_{\max}}{r}\right)
```

| Tipologia | Campo luce m | Rapporto r | Minimo d m |
| --- | --- | --- | --- |
| Soletta piena in c.a. | 6–15 | 18 | 0,35 |
| Travi a T in c.a. | 12–30 | 17 | 0,70 |
| Travi a I in c.a.p. | 20–50 | 22,22 | 1,00 |
| Travi a U in c.a.p. | 25–50 | 22,22 | 1,10 |
| Cassone in c.a.p. | 35–80 | 22,22 | 1,30 |
| Cassone a conci variabile | 80–200 | 45 | 2,00 |
| Travi a I acciaio cls | 30–90 | 25 | 1,00 |
| Cassone acciaio cls | 40–150 | 25 | 1,20 |
| Travi incorporate | 8–40 | 28 | 0,45 |
| Piastra ortotropa | 40–200 | 30 | 1,20 |
| Arco con catena | 40–250 | 120 | 0,80 |
| Strallato | 100–700 centrale | 150 | 1,00 |
| Sospeso | 200–1200 centrale | 200 | 1,20 |
| Reticolare | 30–150 | 100 | 0,70 |

Nel cassone variabile l’altezza sulle pile vale max(d; Lmax/18). Quantità e inerzia sono calcolate con altezza equivalente deq = d + (dpila − d)/3. Il motore non integra una legge reale di variazione lungo l’asse e non simula la costruzione a sbalzo. Il limite massimo di altezza nell’ottimizzazione si applica anche a dpila, cioè all’altezza dell’impalcato in corrispondenza delle pile, non all’altezza del fusto sul terreno.

### Sezioni ideali quantità e inerzie

Il calcolo usa metri per le dimensioni generali, m² per le aree, m³ per i volumi, m⁴ per le inerzie e tonnellate per gli acciai. Gli spessori metallici inseriti in mm sono divisi per 1.000. Il numero ordinario di travi è max(2; parte intera di W/interasse); soletta e cassone in c.a.p. hanno un elemento longitudinale equivalente, mentre i cassoni metallici usano il numero impostato o una regola sulla larghezza.

La soletta piena ha area Wd. Le travi a T sommano soletta e anime rettangolari. Le travi a I in c.a.p. sono profili ideali: piattabande di spessore min(0,18; h/4), larghezza min(0,70; 0,70W/ng), anima min(0,20; bf/2) e rialzo configurabile. Non sono sezioni di catalogo del produttore. Il cassone in c.a.p. somma soletta, fondo di larghezza W per il rapporto impostato e numero di anime pari al numero delle celle più uno.

Per una trave metallica a I, tf e tw sono gli spessori di piattabanda e anima, bf è la larghezza delle piattabande e h l’altezza sotto soletta. L’area per trave è:

```math
A_a = 2b_f t_f+t_w(h-2t_f)
```

Per il cassone metallico ordinario il fondo per cassone vale 0,40W/ng; le due piattabande superiori e le due anime sono conteggiate separatamente. Se s è il parametro H per 4V, lo scarto orizzontale di ogni anima è hw × s/4. Lo sviluppo reale dell’anima inclinata è:

```math
\ell_w = \sqrt{h_w^2+(h_w s/4)^2}
```

L’area delle anime è 2ng tw lw. L’inerzia verticale locale delle anime sottili usa A hw²/12: lo sviluppo reale determina l’area, mentre la proiezione verticale determina la distribuzione delle quote. Il medesimo criterio è usato per le anime delle U, con scarto orizzontale pari a metà della differenza fra larghezza superiore e inferiore. Non si aggiunge l’inerzia microscopica nello spessore della parete: resta una schematizzazione a parete sottile.

Il calcestruzzo usa Ec = 22.000((fc + 8)/10)^0,3 MPa, senza viscosità. L’acciaio usa Es = 200.000 MPa. Il baricentro e l’inerzia sono omogeneizzati a calcestruzzo, con rapporto nj = Ej/Ec. Per ogni componente si somma l’inerzia locale e il termine di trasporto. La rigidezza per l’analisi longitudinale è Ec × 1.000 × I, in kN m².

```math
y_G = \frac{\sum_j n_j A_j y_j}{\sum_j n_j A_j}
I_{eq} = \sum_j n_j\left[I_j+A_j(y_j-y_G)^2\right]
```

Per le travi incorporate, Ac = Wd − Aa: il volume d’acciaio sostituisce calcestruzzo. L’inerzia usa il rettangolo lordo in cls più l’apporto dell’acciaio con coefficiente Es/Ec − 1, evitando di conteggiare due volte la stessa area. Si assume collaborazione perfetta; adesione, fasi di getto e verifiche dei profili non sono risolte.

La piastra ortotropa somma lamiera superiore, canalette, fondi e due anime verticali per cassone. La lunghezza dei lati inclinati delle canalette è calcolata geometricamente. Il numero delle canalette deriva dalla parte intera di W/interasse e il prospetto espone l’interasse adottato W/n. La massa riceve un’aggiunta per traversi e accessori, ma il modello non esegue una verifica locale ortotropa o delle saldature. Il manuale FHWA [R4] documenta la necessità di trattare distintamente flessione locale, distorsione e fatica; citarlo non significa che queste verifiche siano implementate.

### Acciai e carichi equivalenti

Il volume di calcestruzzo dell’impalcato è l’area della sezione per la lunghezza. La massa di carpenteria è 7,85 t/m³ per il volume geometrico, maggiorata del 15% per traversi, irrigidimenti e connessioni; nelle travi incorporate l’aggiunta è 5%. Questa maggiorazione entra in massa, costo e peso proprio, ma non nell’inerzia flessionale.

Le armature sono stimate per incidenza: 140 kg/m³ per impalcati in c.a., 110 per c.a.p., 120 per soletta mista e famiglie estese; 150 per sottostrutture e 120 per fondazioni. La precompressione vale inizialmente 30 kg/m³ di impalcato precompresso. I parametri editabili sono riportati nell’interfaccia; l’incidenza 120 delle solette miste è una convenzione del motore. Non vengono ricavati numero, tracciato, tesatura o perdite dei cavi di precompressione.

Indicando con ma la massa totale di carpenteria dell’impalcato in t, con g2 il carico permanente portato in kN/m² e con nb il numero convenzionale di barriere, i carichi sull’intera larghezza sono:

```math
G = 25A_c+\frac{9{,}81m_a}{L}+g_2 W+8n_b
Q = q_{traffico}\cdot W
q_s = G+Q
q_d = \gamma_G G+\gamma_Q Q
```

I valori iniziali sono g2 = 2,5 kN/m², qtraffico = 9 kN/m², γG = 1,35 e γQ = 1,50. nb vale 2, oppure 3 in presenza di spartitraffico. Il traffico è uniforme e contemporaneo su tutte le campate: non è un inviluppo di assi mobili né un modello di corsie caricate alternativamente. L’impalcato è rappresentato come un’unica trave equivalente per tutta la larghezza.

### Analisi della trave equivalente

Per le famiglie senza struttura superiore, ANTHEA risolve una trave di Euler Bernoulli con EI costante, appoggi verticali e rotazioni libere alle estremità. Nella continuità i momenti sugli appoggi interni sono ottenuti con il teorema dei tre momenti. Per due campate adiacenti a e b, con momenti Ml, Mi e Mr e carico uniforme q:

```math
M_l a+2M_i(a+b)+M_r b = -\frac{q(a^3+b^3)}{4}
```

Una volta noti i momenti agli estremi della campata di lunghezza l, la reazione locale sinistra, il taglio e il momento sono:

```math
R_l = \frac{ql}{2}+\frac{M_r-M_l}{l}
V(x) = R_l-qx
M(x) = M_l+R_l x-\frac{qx^2}{2}
```

La freccia è ricavata integrando M/EI due volte e imponendo spostamento nullo ai due appoggi. Il motore campiona 41 punti per campata e aggiunge il punto di taglio nullo per individuare l’estremo del momento. L’estremo della freccia resta campionato: il valore visualizzato non è sempre il massimo analitico esatto. I diagrammi delle azioni usano qd; la freccia indicativa e le reazioni usate nel dimensionamento ordinario delle fondazioni sono riferite a qs.

Per una campata appoggiata valgono R = ql/2, Mmax = ql²/8 e vmax = 5ql⁴/(384EI). Per due campate uguali continue, il momento centrale è −ql²/8, la reazione esterna 3ql/8, quella centrale 5ql/4 e il massimo positivo 9ql²/128. Questi casi sono controllati direttamente dalla suite. La formulazione FEM adottata nella nuova verifica indipendente è documentata da TU Delft [R5].

Il modello trascura deformazione a taglio, fessurazione, viscosità, ritiro, effetti reali della precompressione, rigidezza variabile, cedimenti degli appoggi e fasi costruttive. La buona concordanza fra due solutori con queste ipotesi dimostra la corretta soluzione del problema ideale, non la validità delle ipotesi per qualsiasi ponte.

### Pile spalle e fondazioni

La quota del terreno è schematica. Per una pila ordinaria l’altezza H è la quota dell’impalcato meno l’altezza della sezione sull’appoggio; per una spalla si usa min(7 m; quota meno altezza impalcato). Sono richiesti almeno 1 m di spazio verticale. Una pila a telaio ha max(2; arrotondamento superiore di W/7) colonne, una pila circolare o a martello ha un fusto, il setto ha lunghezza trasversale max(1; W − 2).

Il pulvino ordinario vale W × 1,5 × 1,4 m³; la testa a martello W × 2 × 1,8 m³. La spalla ha volume convenzionale W[H max(0,6; H/7) + 3]. Il termine 3 è un’area equivalente in m² per metro di larghezza, non una misura completa di paraghiaia, muri d’ala e mensole. La spalla non è verificata per spinta del terreno o stabilità.

Il dimensionamento automatico della pila ordinaria soddisfa una snellezza convenzionale non maggiore di 90 e una compressione media non maggiore di 0,30fc,sub. Considerando il peso proprio del fusto, l’area minima si ricava da:

```math
A_{req} = \frac{\max(0;R+25V_{pulvino})}{300f_{c,sub}-25H}
```

R è la reazione di servizio in kN e fc,sub è in MPa. Per colonne circolari il diametro è almeno max(1,2; 8H/90; √(4Areq/(πnc))) m; per il setto lo spessore è almeno max(1; 2H√12/90; Areq/max(1; W − 2)). La misura automatica è arrotondata verso l’alto a 0,05 m. La snellezza diagnostica è 2H/r, con r = D/4 per la colonna o t/√12 per il setto. La ricerca esclude valori maggiori di 100: la soglia 90 è un margine della regola automatica, 100 è la soglia di esclusione.

| Classe convenzionale | Pressione kPa | Attrito palo kPa | Punta palo kPa | Lunghezza palo m |
| --- | --- | --- | --- | --- |
| Roccia | 1.000 | 150 | 8.000 | 10 |
| Sabbia o ghiaia densa | 400 | 70 | 2.500 | 15 |
| Terreno medio | 200 | 45 | 1.500 | 22 |
| Argilla soffice | 100 | 25 | 500 | 30 |

La fondazione Automatica delle otto famiglie ordinarie sceglie il plinto in roccia, oppure in terreno denso se la quota è inferiore a 15 m; negli altri casi sceglie pali da 1,0 m. Le sei famiglie estese usano plinto su roccia o terreno denso con quota inferiore a 25 m, altrimenti pali da 1,5 m. Sono convenzioni differenti del software: per confronti controllati conviene imporre esplicitamente il tipo di fondazione.

Per un palo di diametro D e lunghezza Lp, la resistenza assiale di riferimento è:

```math
R_{pal} = \pi D L_p q_{s,palo}+\frac{\pi D^2 q_b}{4}
```

L’azione comprende reazione dell’impalcato, peso della sottostruttura e peso del plinto. Nel plinto diretto il rapporto indicativo è N/(B T p); su pali è N/(np Rpal). B e T sono i lati della fondazione, p la pressione di riferimento e np il numero dei pali. Il peso del plinto viene aggiornato durante il dimensionamento; non è trascurato nel numeratore.

Per il plinto diretto si parte da una dimensione basata su √(N/(0,85p)), con minimo geometrico e arrotondamento a 0,25 m; lo spessore è max(0,60; B/6). La dimensione viene aumentata a passi di 0,25 m finché il rapporto è non maggiore di 1. Per i pali il numero automatico è pari e non inferiore a 4; aumenta di almeno due unità quando necessario. Il plinto su pali ha spessore 1,5D e deve contenere una griglia a interasse 3D con ingombro minimo [ceil(√np) − 1]3D + 2D. Il lato trasversale tiene conto anche della larghezza della sottostruttura.

Il ciclo di fondazione ha un limite di 1.024 aggiornamenti e rifiuta casi non convergenti. Quote e numeri imposti dall’utente vengono rispettati, esponendo l’eventuale superamento del rapporto. Restano esclusi eccentricità, pressoflessione dei pali, carichi orizzontali, effetto di gruppo, cedimenti, attrito negativo, liquefazione e scalzamento. Il quadro delle verifiche reali è distinto da queste formule e va ricondotto alle norme applicabili [R6].

### Archi reticolari stralli e sospensioni

Le strutture superiori sono predimensionate con equilibri ideali e aree pari alla forza assiale divisa per una tensione di riferimento. I valori iniziali sono 180 MPa per elementi tesi di carpenteria, 100 MPa per archi e aste compresse, 600 MPa per cavi e 6 MPa per antenne in cls. Non sono resistenze di progetto derivanti da una verifica completa. La carpenteria superiore riceve un’aggiunta iniziale del 20% per collegamenti; cavi e pendini sono conteggiati a parte.

```math
A = \max\left(0{,}00001;\frac{|N|}{\sigma_{rif}}\right)
```

N e σrif devono essere in unità coerenti: nel codice σrif in MPa viene moltiplicata per 1.000 per ottenere kN/m². La massa superiore aumenta il peso proprio; il motore aggiorna massa e carico fino a variazione relativa non maggiore di 10⁻⁸, con massimo 80 iterazioni. Un mancato equilibrio del ciclo produce un rifiuto esplicito. Queste sono iterazioni interne di un singolo candidato, diverse dai tentativi dell’ottimizzazione.

Nell’arco con catena, due archi parabolici hanno freccia f = rapporto impostato × luce. Ogni piano porta metà del carico totale. La componente orizzontale per arco è H = qd l²/(16f); la compressione adottata per l’intero arco è √[H² + (qd l/4)²], la catena porta H e ogni pendino porta qd Δx/2. Gli archi sono divisi in 80 segmenti per stimarne lo sviluppo. Si assume la forza massima lungo ciascun arco; instabilità e pressoflessione reale non sono calcolate.

Nel reticolare l’altezza è il rapporto impostato per la luce. Due piani resistenti sono stimati con forza nei correnti qd l²/(16h). Le diagonali usano una forza convenzionale ottenuta dal massimo taglio e dall’inclinazione, applicata a tutti i pannelli. Questa regola dà quantità orientative e non risolve un reticolo con carichi mobili, nodi e controventi reali.

Nello strallato lo schema è simmetrico, L/4–L/2–L/4, con due antenne. L’altezza sopra impalcato è quella imposta oppure max(10 m; 0,20 della luce centrale). Gli stralli a ventaglio sostengono le fasce di lunghezza Δx e, per ciascun piano, hanno forza T = qd Δx/(2 sin α). Il modello assegna a ciascuna antenna metà del carico verticale complessivo e reazioni verticali nulle alle spalle per il solo impalcato. Non risolve la rigidezza relativa impalcato stralli antenne né la tesatura.

Nel sospeso la freccia del cavo principale è un rapporto della luce centrale; l’antenna deve superarla di almeno 2 m. Per ciascuno dei due cavi H = qd l²/(16f), T = √[H² + (qd l/4)²]. I cavi di riva sono anch’essi parabolici e i pendini sostengono anche le campate laterali. Il computo include i cavi di riva, non soltanto la campata centrale.

Per le reazioni del sospeso, Htot è la componente orizzontale dei due cavi calcolata con il carico pertinente; indicando con a la campata di riva e ht l’altezza antenna sopra impalcato, la reazione all’estremo è qa/2 − Htot ht/a. Può risultare negativa. La stima dei due blocchi è Vanc = 2[max(0; −Restremo,d) + Htot,d/μ]/25, con μ iniziale 0,5. Si tratta di peso stabilizzante convenzionale: non comprende una verifica di ribaltamento, pressioni eccentriche o stabilità geotecnica dell’ancoraggio.

Le antenne sono due fusti quadrati con traverso. La loro area tiene conto della reazione amplificata, del peso proprio amplificato e della tensione di riferimento; si applica anche una regola di snellezza. L’inerzia mostrata resta quella del solo impalcato. Per arco, reticolare, strallato e sospeso non vengono prodotti diagrammi globali di momento e freccia, perché richiederebbero un modello diverso. Non vanno sostituiti mentalmente con i diagrammi della trave ordinaria.

### Computo prezzi e indicatore ambientale

Il costo diretto è la somma delle quantità per i prezzi. Al risultato vengono applicati in successione oneri aggiuntivi e imprevisti. Con i valori iniziali 12% e 15%, il fattore complessivo è 1,288, non 1,27. L’intervallo iniziale ±30% è una fascia convenzionale scelta dall’utente, non un intervallo statistico di confidenza e non un vincolo dell’ottimizzazione.

```math
C_{diretto} = \sum_j Q_j p_j
C_{totale} = C_{diretto}(1+oneri/100)(1+imprevisti/100)
```

I valori correnti sono stati confrontati con ANAS NC MP 2026 Rev 1 [R1] e con Emilia Romagna 2026 [R2]. Le tariffe ANTHEA sono aggregate e arrotondate: si devono leggere inclusioni, esclusioni e unità. Il confronto non rende automatico l’adeguamento alla classe del calcestruzzo, all’esposizione, al varo o alla corsa degli appoggi.

| Voce ANTHEA | Prezzo iniziale | Natura del riferimento |
| --- | --- | --- |
| Cls impalcato | 260 €/m³ | Confronto C45/55 ANAS |
| Cls sottostrutture | 240 €/m³ | Aggregato fondazioni ed elevazioni |
| Armatura ordinaria | 1.660 €/t | ANAS 1,66 €/kg |
| Precompressione | 3.600 €/t | Riserva di sistema con accessori |
| Carpenteria ordinaria | 3.500 €/t | Base ANAS con varo ordinario |
| Carpenteria cassoni | 4.000 €/t | Maggiorazione convenzionale |
| Casseforme | 50 €/m² | Superfici equivalenti e prezzo medio |
| Pali da 1,0 e 1,5 m | 300 e 550 €/m | Perforazione e cls armatura separata |
| Appoggi | 5.000 €/cad | Indennità media non dimensionamento |
| Giunti | 2.400 €/m | Corsa moderata da confermare |
| Barriere | 360 €/m | Confronto bordo ponte H4 |
| Pavimentazione | 32 €/m² | Pacchetto convenzionale |
| Carpenteria ortotropa | 5.000 €/t | Aggregato non singola voce ANAS |
| Cavi e pendini | 16.000 €/t | Sistema installato da preventivare |
| Montaggio speciale | 1.000 €/t | Aggiunta per complessità speciale |

Il cls dei pali è incluso nella tariffa al metro e non viene nuovamente addebitato a volume; il volume resta conteggiato per l’impronta ambientale e per l’armatura. Gli appoggi ANAS sono articolati per forza e movimento, mentre qui si usa un importo medio a dispositivo. Le casseforme sono equivalenti; scavi, rinterri, drenaggi, impermeabilizzazione, protezioni, accessi, centine alte e sicurezza specifica non sono computati analiticamente. Per la carpenteria ortotropa la maggiorazione ANAS riguarda la lamiera interessata, mentre ANTHEA adotta un prezzo aggregato del sistema.

Il listino ANAS dichiara spese generali 15% e utile 10% già inclusi e tratta la sicurezza specifica separatamente [R1]. Per questo il 12% ANTHEA deve coprire soltanto oneri aggiuntivi non computati; aggiungerlo come nuova percentuale generale di impresa può produrre un doppio conteggio. Il montaggio speciale va azzerato o adattato quando già compreso nel preventivo.

La CO₂ somma cls e acciai e applica una maggiorazione convenzionale di trasporti e cantiere. Con masse in tonnellate e fattori dell’acciaio in kg/kg, il prodotto restituisce tonnellate di CO₂ equivalente. Il cls usa kg/m³ e richiede divisione per 1.000. I fattori iniziali sono 320 kg/m³ per cls, 1,4 kg/kg per armatura, 2 per carpenteria, 2,5 per precompressione e cavi; il cantiere aggiunge 15%.

```math
E_{CO2} = \left(\frac{V_{cls}f_{cls}}{1000}+\sum_s m_s f_s\right)(1+cantiere/100)
```

Le opzioni cls a ridotta CO₂ e acciaio riciclato moltiplicano rispettivamente il fattore del cls per 0,60 e quello della carpenteria per 0,35. Non riducono automaticamente armature, cavi o prezzi. I fattori non provengono da EPD specifiche; finiture, manutenzione, esercizio e fine vita restano esclusi. Una scelta a CO₂ minima è quindi minima per questo indicatore parziale, non per un’analisi completa del ciclo di vita.

La durata ordinaria è ceil(avvio + n × coefficiente campata + npile × coefficiente pila + aggiunte). I valori iniziali sono 4 mesi, 1,2 mesi/campata e 0,4 mesi/pila; i pali aggiungono 1,5 mesi, il cassone a conci altri 1,5 mesi/campata, le strutture superiori 6 + luce principale/50 mesi. È una stima parametrica, non un cronoprogramma e non è un obiettivo selezionabile della ricerca.

### Filtri di ammissibilità

Una combinazione viene esclusa se non è calcolabile o supera uno dei filtri seguenti. Le motivazioni sono registrate; una combinazione può avere più motivi, quindi la somma delle occorrenze per motivo può superare il numero delle combinazioni escluse.

1. Numero di campate fuori dai limiti, luci effettive fuori dal campo della famiglia, altezza sotto il minimo richiesto o la regola di predimensionamento, oppure altezza massima dell’impalcato superata.
2. Appoggi interni dentro l’ostacolo ampliato di 1 m per lato; schemi di continuità incompatibili con la tipologia; geometrie impossibili.
3. Rapporto assiale della fondazione maggiore di 1, snellezza delle pile maggiore di 100, compressione media maggiore di 0,30fc,sub o antenna oltre la tensione di riferimento.
4. Fusto non contenuto nel plinto, più di 64 pali per appoggio o griglia a interasse 3D non contenuta nel plinto.
5. Reazione negativa per tipologie diverse dal sospeso, quando servirebbero dispositivi antisollevamento non dimensionati; risultante di fondazione non compressa dopo i pesi propri.
6. Sovrapposizione delle travi a U, cassoni metallici ordinari oltre la larghezza disponibile, costo o CO₂ negativi o non finiti.

I controlli applicano piccole tolleranze numeriche ai confronti. Non sono filtri di resistenza a flessione o taglio dell’impalcato, fatica, instabilità locale, dinamica, sisma, vento, comfort, montaggio, trasporto o manutenzione. Allargare i range aumenta le alternative esplorate ma non aggiunge queste verifiche.

### Punteggi graduatoria e frontiera Pareto

Con Costo minimo il punteggio coincide con C; con CO₂ minima coincide con E. Il compromesso usa i minimi Cmin ed Emin delle soluzioni ammesse della stessa ricerca:

```math
S = \frac{0{,}5C}{\max(1;C_{\min})}+\frac{0{,}5E}{\max(10^{-9};E_{\min})}
```

Il punteggio di compromesso è adimensionale e va minimizzato. I denominatori sono limitati inferiormente per gestire anche indicatori nulli. Non è una monetizzazione della CO₂ e non attribuisce un prezzo in euro a una tonnellata emessa. Il punteggio non va confrontato direttamente fra ricerche con minimi diversi. A parità di punteggio non arrotondato, l’ordinamento usa costo, CO₂, identificativo della famiglia e numero di campate; l’enumerazione è deterministica.

Una soluzione appartiene alla frontiera Pareto quando nessun’altra soluzione distinta ammessa ha costo e CO₂ entrambi non maggiori e almeno uno strettamente minore. Una soluzione dominata può comunque interessare per aspetti non rappresentati nei due indicatori, ad esempio costruibilità o minor interferenza idraulica; tali motivazioni devono essere valutate esternamente.

Il motore conserva l’elenco completo delle soluzioni distinte e una selezione iniziale delle prime alternative. La casella Mostra le prime N dell’interfaccia agisce sull’elenco completo, da 1 a 50.000 righe, e non ripete la ricerca. Il limite 1–50 dell’opzione tecnica Alternatives riguarda soltanto la selezione restituita dall’API, non il numero dei punti disponibili nella nuvola della finestra.

### Cosa rimane da validare prima dell’uso progettuale

Non sono disponibili, in questa attività, confronti sperimentali, consuntivi economici di ponti costruiti, una calibrazione statistica dei rapporti luce altezza, verifiche normative complete di tutte le alternative o una validazione indipendente completa delle strutture speciali. I test delle famiglie speciali controllano geometrie, equilibri ideali, masse, filtri e riproducibilità, ma non sostituiscono un’analisi strutturale dedicata.

Per portare un’alternativa allo studio di fattibilità occorre almeno definire il reale schema statico e costruttivo, l’inviluppo di traffico, i materiali, i dettagli di impalcato, il modello geotecnico, i vincoli territoriali e le lavorazioni mancanti. I controlli di sezioni, fasi, fatica, stabilità, fondazioni e appoggi devono essere eseguiti con modelli adeguati. Se queste verifiche aumentano le quantità, vanno riportate nel computo e nel confronto delle alternative.

Un buon uso del modulo consiste nel restringere un insieme di idee a poche alternative leggibili e riproducibili. Il risultato da conservare è l’insieme dati ipotesi soluzione quantità avvisi, insieme al motivo per cui è stata scelta un’alternativa. La sola etichetta ottimo non è una giustificazione progettuale.

### Riferimenti

[R1] ANAS, Elenco prezzi 2026 Rev 1, Nuove costruzioni e manutenzione programmata, giugno 2026. Consultato il 30 settembre 2026. Usato per il riscontro delle tariffe e delle inclusioni, non per le regole di dimensionamento. https://www.stradeanas.it/it/elenco-prezzi

[R2] Regione Emilia Romagna, Elenco regionale prezzi 2026 e correzioni 2026. Riscontro territoriale delle tariffe; non attribuisce una localizzazione al progetto. https://territorio.regione.emilia-romagna.it/osservatorio/elenco_regionale_prezzi/prezzario-2026

[R3] TheBridgeEng, Bridge Design. Riferimento della campagna storica conservata in bridge_design_site_1000. Non usato nel nuovo verificatore indipendente. https://thebridgeeng.com/design

[R4] FHWA IF 12 027, Manual for Design Construction and Maintenance of Orthotropic Steel Deck Bridges, febbraio 2012. Riferimento tecnico per l’estensione reale delle verifiche ortotrope. https://www.fhwa.dot.gov/bridge/pubs/if12027/if12027.pdf

[R5] TU Delft, Computational Modelling, Euler Bernoulli beam elements, capitolo 4.1. Riferimento della formulazione per rigidezze e funzioni di Hermite del verificatore indipendente. https://teachbooks.tudelft.nl/computational-modelling/structural_linear/euler_bernouilli.html

[R6] Decreto 17 gennaio 2018, Aggiornamento delle Norme tecniche per le costruzioni, pubblicazione in GU 20 febbraio 2018. Riferimento del quadro normativo, non certificazione del predimensionamento. https://www.gazzettaufficiale.it/eli/id/2018/2/20/18A00716/sg

### Controllo manuale della trave equivalente

Per una campata appoggiata di 20 m con carico uniforme 100 kN/m, ogni reazione vale 1000 kN e il momento massimo è 5000 kNm. Il carico è riferito all'intero impalcato equivalente. Per due campate uguali continue, entrambe caricate:

```math
M_{appoggio}=-\frac{qL^2}{8}=-5000\,\mathrm{kNm}
R_{estremo}=\frac{3qL}{8}=750\,\mathrm{kN}
R_{centrale}=\frac{5qL}{4}=2500\,\mathrm{kN}
M_{positivo}=\frac{9qL^2}{128}=2812{,}5\,\mathrm{kNm}
```

La somma delle reazioni è 4000 kN, uguale al carico sulle due campate. Il controllo riguarda una trave a EI costante: non valida lo schema globale di uno strallato, le fasi costruttive o l'inviluppo di carichi mobili.

## Esempi trasversali e lettura critica

### Separare capacità domanda e quantità

Un incremento del prezzo dell'acciaio non può aumentare il momento resistente di una sezione. Un aumento della quantità parametrica di armatura in Bridge Design può aumentare il costo senza creare una disposizione di barre verificata. Al contrario, nella sezione CA una nuova barra modifica direttamente l'equilibrio e il dominio. La stessa parola «armatura» descrive quindi oggetti differenti nei due moduli.

Un aumento di lunghezza del palo modifica resistenza e peso, ma può uscire dalla profondità investigata. Un aumento della resistenza del cls può influire sul dominio CA, sul copriferro prescritto e sulla rigidezza del ponte preliminare, con leggi differenti. Il confronto di sensitività deve identificare quale ramo stia cambiando.

### Controlli indipendenti semplici

Per una quantità di cls di 100 m³ a 240 €/m³ il costo diretto è 24000 €. Con 12% di oneri e 15% di imprevisti diventa 30912 €. Con fattore 320 kgCO₂/m³ le emissioni materiali sono 32 tCO₂; aggiungendo 15% di cantiere diventano 36,8 tCO₂. Prezzo e fattore ambientale operano su canali distinti.

Per una sezione metallica ideale in campo elastico, verificare preliminarmente che N/A e M/W abbiano unità MPa dopo la conversione delle azioni. Per un palo laterale verificare che la somma delle forze distribuite, dell'eventuale forza al piede e della forza in testa sia equilibrata. Per una trave verificare che la somma delle reazioni equivalga al carico totale. Questi controlli intercettano errori di unità e interpretazione prima di discutere dettagli costitutivi.

### Interpretare una prova

Confronti analitici, benchmark e controlli dell’interfaccia rispondono a domande diverse. Consulta [Tracciabilità, fonti e archivio](wiki:tracciabilita-e-riferimenti) per conservare il campo del confronto senza estenderlo all’intera opera.

## Tracciabilità, fonti e archivio

Un risultato è ripercorribile se conserva input, unità, combinazione, modello, coefficienti e versione del motore. Una schermata del tasso non basta. Il file riapribile conserva i dati; il report documenta il risultato della sessione. Una riapertura con librerie diverse può richiedere un nuovo confronto.

### Tre tipi di fonte

| Fonte | Cosa sostiene | Cosa non dimostra |
| --- | --- | --- |
| Norma ed edizione applicabile | Requisiti e campo della verifica | Che il software implementi ogni clausola |
| Modello teorico o pubblicazione | Ipotesi, derivazione e limiti | Che valga per qualsiasi geometria e terreno |
| Codice e prova del software | Comportamento della versione controllata | La sicurezza dell'opera nel suo insieme |

Le pagine distinguono queste fonti. Il riferimento a una norma identifica un'edizione; non equivale a un aggiornamento automatico alla versione più recente. In particolare, i profili EC2 del modulo c.a. descrivono implementazioni di prima generazione.

### Ritrovare i contenuti precedenti

Le appendici di sviluppo, migrazione e audit sono state consolidate nelle pagine per argomento. I vecchi indirizzi della Wiki portano alla pagina corrente pertinente, senza creare duplicati nella ricerca. I risultati storici restano evidenze della versione e del caso originari, non risultati appena rieseguiti.

Le due guide Rev14 complete, i relativi Word/PDF e il catalogo precedente sono conservati in `supporto/SUPERATI/wiki-integrazione-rev15-20261004/`. Il registro riporta origine, impronta SHA-256 e revisione sostitutiva. L'inventario di integrazione associa ogni vecchia voce alla destinazione e alle fonti del codice. Modelli di esempio e risultati usati dalle attività rimangono nelle loro cartelle.

### Controlli indipendenti

Parti da un caso con soluzione semplice e unità esplicite. Per una trave appoggiata controlla reazioni e momento; per una sezione omogenea area, baricentro e inerzia; per un contributo laterale uniforme di palo controlla superficie per tensione. Estendi poi il confronto al caso completo, registrando scarti e ipotesi.

I test di integrazione confrontano chiamate, dati e visualizzazione; non sono indipendenti dal motore se usano la stessa libreria come riferimento. I benchmark numerici devono dichiarare il modello e la fonte della soluzione. Il numero di test superati non sostituisce questa distinzione.

### Riferimenti normativi identificati

Il [DM 17 gennaio 2018](https://www.gazzettaufficiale.it/eli/id/2018/02/20/18A00716/sg) e la [Circolare 21 gennaio 2019 n. 7](https://www.gazzettaufficiale.it/eli/id/2019/02/11/19A00855/sg) sono i riferimenti italiani citati nelle formulazioni NTC delle guide. Verifica edizione, modifiche e disposizioni applicabili all'opera prima di assegnare coefficienti o limiti. La [matrice dei profili c.a.](wiki:profili-calcestruzzo) specifica ciò che il programma tratta e ciò che resta escluso.

## Muri di sostegno e stabilità globale

Il modulo tratta muri a mensola e a gravità. Le verifiche locali e la stabilità globale hanno modelli e combinazioni distinti. La [guida ai muri](wiki:guida-muri-di-sostegno-con-stratigrafie-di-monte-e-valle) descrive il percorso Input e Verifiche; [portanza, cedimenti e armature](wiki:portanza-sismica-cedimenti-spostamenti-e-armature-rev07) approfondisce i modelli aggiuntivi.

Le stratigrafie di monte e valle sono affiancate, indipendenti o collegate per spessori e proprietà; le profondità partono dalle rispettive superfici. Hlib è la distanza dalla sommità al terreno di valle: Dv=H+t−Hlib. Il terreno davanti al muro entra nei pesi, nei momenti, nelle sollecitazioni della mensola e nel ricoprimento efficace della portanza. La passiva richiede attivazione e frazione mobilitata; è esclusa nel sisma. Gli attriti del muro e della fondazione sono assegnabili oppure ricavati da φcv,k e tipo di interfaccia. Il valore a volume costante va caratterizzato, senza sostituirlo automaticamente con quello di picco.

Valori di calcolo consente di interrogare e modificare gli input e leggere coefficienti effettivi per combinazione, pesi, attriti, pressioni e sollecitazioni. Le forze risultanti dipendono dagli input e non si possono forzare. Le combinazioni sono modificabili: il preset locale è A1+M1+R3, quello globale A2+M2+R2. Azioni eccezionali e sisma Mononobe–Okabe o Wood semplificato sono espliciti. Il terreno del lato selezionato può essere trasferito ai moduli dei pali; la sezione selezionata può essere inviata al modulo c.a.

La stabilità globale Bishop ha un motore separato e un proprio profilo esteso, anche con due colonne profonde e confine verticale assegnato. Il pulsante Stabilità globale apre il percorso; al primo accesso a un profilo vuoto ne prepara i dati e attiva la verifica. Gli strati si inseriscono per spessore, con quota del fondo calcolata automaticamente. La precompilazione non prolunga le indagini: rilievo, terreni profondi e falda del sito vanno controllati e confermati. Il disegno rappresenta anche il terreno sotto il piano di posa. Parametri, dominio e combinazioni restano interrogabili e modificabili.

L’attrito automatico usa δd=k·atan(tanφcv,k/γMφ), con k=1 per gettato in opera e 2/3 per prefabbricato liscio. L’assegnazione usa tanδd=tanδk/γMφ. Coulomb/MO sul fusto tiene conto di δ muro; l’equilibrio del blocco muro e terreno sulla mensola usa il piano virtuale a δ=0, evitando il doppio conteggio delle forze interne. La portanza drenata include q′B′Nq iq e 0,5γ′B′²Nγ iγ; rimane fuori campo con base non ruvida, eccentricità eccessiva o inerzia sismica del terreno di fondazione. Lo scorrimento usa V′tanδb,d/γR.

Le relazioni Word includono stratigrafie e coefficienti utilizzati. Cedimenti e spostamenti sono modelli separati; verifiche idrauliche, liquefazione e completamento esecutivo richiedono analisi dedicate. Il modulo non emette una verifica complessiva dell’opera.

### Dati del profilo globale e conversione degli spessori

La precompilazione copia i terreni locali senza estenderne lo spessore conosciuto. Il profilo globale resta poi indipendente: cambiare i terreni delle spinte non lo sovrascrive. Il terreno omogeneo della portanza non sostituisce la stratigrafia profonda.

L’interfaccia acquisisce gli spessori e conserva nell’archivio i fondi a quota assoluta. Ogni colonna parte dalla superficie del rilievo presso il muro, inizialmente H+t a monte e Dv a valle. Con y positivo verso l’alto e spessori h_i positivi:

```math
y_{fondo,i} = y_{superficie}-\sum_{j=1}^i h_j
```

Un fondo negativo è sotto il piano di posa. Le superfici degli strati restano orizzontali e decrescenti entro ogni colonna; Due colonne usa un confine verticale assegnato, inizialmente x=a+s₀. Il disegno e l’editor usano lo stesso riferimento. Nomi uguali condividono il colore, senza imporre uguaglianza delle proprietà o collegamento dei dati.

In Drenata si utilizzano φ′k, c′k, pesi naturali e saturi e pressioni interstiziali dalla falda. In Non drenata serve cu,k di ogni strato: l’analisi a tensioni totali usa φu=0 e non sottrae nuovamente le pressioni interstiziali nella resistenza del concio. La falda globale è una polilinea di quote piezometriche, non una successione di profondità sotto la superficie; non è previsto battente esterno sopra il terreno.

### Proposta automatica e dominio della ricerca

La modalità Automatica è una proposta di input, non una prescrizione normativa sulla sufficienza delle indagini. Le uscite vanno dall’estremo del rilievo di valle a x=−0,10 m; gli ingressi da x=B+0,10 m all’estremo del rilievo di monte. La profondità minima è 0,10 m sotto il piano di posa. La profondità massima proposta è il minore fra 2(H+t) e quella nota in entrambe le colonne; nel Profilo unico si usa la sola colonna attiva.

Fondi incompleti o insufficienti non generano limiti fittizi né prolungamenti dell’ultimo strato. In Assegnata i limiti sono quelli dell’utente; gli archivi precedenti mantengono questa modalità e la ricerca salvata. Nodi, conci e raffinamenti restano modificabili.

La ricerca esplora superfici circolari sotto l’intero muro, con ingresso a monte e uscita a valle, entro i limiti assegnati. Non garantisce il minimo assoluto. Un minimo sul bordo richiede di estendere la ricerca verificando le indagini disponibili; una discretizzazione non convergente resta incompleta.

### Metodo di equilibrio e interpretazione dei tassi

Bishop semplificato impone l’equilibrio verticale dei conci e quello globale dei momenti, trascurando il taglio interconcio; non impone l’equilibrio orizzontale. Il muro sostituisce il terreno nel proprio volume, così il peso non viene contato due volte. Spinte muro–terreno e reazioni di fondazione sono interne alla massa globale e non si aggiungono come azioni esterne. Il motore geotecnico, le operazioni geometriche e l’adattatore del muro restano componenti separate.

Le combinazioni globali sono indipendenti dalle verifiche locali. Il preset statico è A2–M2–R2: γM,tanφ e γM,c′ pari a 1,25, γM,cu pari a 1,40 e γR=1,10. Le azioni e i coefficienti effettivamente usati restano esposti nella matrice. Lo SLV globale usa la propria impostazione, con βs=0,38 per la derivazione dal sito e γR=1,20; i coefficienti delle spinte non vengono trasferiti come se fossero quelli del meccanismo globale.

F è calcolato sui parametri e sulle azioni di progetto della combinazione. Il confronto con il fattore di resistenza si esprime con:

```math
\eta = \frac{\gamma_R}{F}
```

Nel dominio analizzato la verifica è soddisfatta se F≥γR, equivalente a η≤1, e i controlli numerici e di ricerca sono conclusi favorevolmente. Un F superiore a 1 non basta quando γR è maggiore di 1. Con γR diversi fra combinazioni, il massimo tasso non coincide necessariamente con il minimo F: la vista iniziale usa il tasso più alto e dà priorità ai casi privi di una superficie valida.

Le tabelle espongono geometria della superficie, numero di superfici risolte/provate, stato della ricerca e dettagli dei conci. Un risultato non soddisfatto e un controllo numericamente incompleto sono esiti distinti. La conferma dei dati del sito abilita il calcolo ma non è una verifica positiva; le modifiche dei dati geotecnici del percorso invalidano conferma e risultati precedenti.

### Riscontro ripercorribile e limiti

Il caso stratificato della guida rapida ha H=3 m, t=0,45 m, strati profondi fino a y=−10 m e ricerca automatica fino a 6,90 m. Il modello completo è salvato in supporto/artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato.anthea; i valori di riferimento dei due casi statici sono F=1,176315 e F=1,075737, con γR=1,10. Il secondo tasso è 1,022555 e non soddisfa il confronto.

Nel motore Bishop restano esclusi i meccanismi non circolari e la liquefazione. La ricerca circolare nel dominio assegnato non esaurisce la stabilità generale del versante.

## Portanza, cedimenti, spostamenti e armature dei muri

Il modulo distingue portanza sismica, cedimenti, spostamenti permanenti e verifiche delle armature. Le relazioni seguenti descrivono i modelli implementati e il loro campo. La [procedura operativa](wiki:guida-portanza-sismica-cedimenti-spostamenti-e-armature-rev07) indica quali dati preparare e come leggere gli esiti.

### Portanza sismica

Si applica EN 1998-5:2004 allegato F alla fondazione nastriforme su terreno granulare asciutto, omogeneo e con base ruvida. Nmax=0,5γ(1−av/g)B²Nγ, con Nγ=2(Nq−1)tanφd. Si trascura il contributo favorevole del ricoprimento. N, V e M sono normalizzati con γRD·γR; F=γRD·ah/(g tanφd). Il γR della combinazione è applicato separatamente e dichiarato nella relazione.

Il dominio usa a=c=0,92; b=d=1,25; e=0,41; f=0,32; m=0,96; k=1; k′=0,39; cT=1,14; cM=c′M=1,01; β=2,90; γ=2,80. La somma dei termini di interazione deve essere ≤1, con 0<N̄<(1−0,96F)^0,39. La capacità è cercata lungo il raggio N,V,M: il tasso η è l’inverso del moltiplicatore limite, non il valore della funzione di interazione. Non si applicano una seconda volta larghezza efficace e fattori di inclinazione.

### Cedimenti e spostamenti di esercizio

Si integra s=∫Δσz/M dz con tensioni Boussinesq di una striscia infinita e pressione di contatto lineare. Il calcolo è ripetuto a valle, al centro e a monte. La profondità deve arrivare a Δσz≤10% del carico netto oppure a un substrato rigido documentato. Viene controllata anche la convergenza numerica. Profili insufficienti non producono un esito favorevole né uno spostamento totale valido.

È un cedimento finale con moduli costanti assegnati: non ricostruisce tempi di consolidazione, OCR, scarico e ricarico, variazione di M con le tensioni o degrado ciclico. La rotazione θ=(smonte−svalle)/B deriva dal profilo libero; non è una soluzione accoppiata della fondazione rigida.

Per Calcola spostamenti in testa servono anche la rigidezza orizzontale di fondazione K per metro di muro, in kN/m², e il limite scelto. Il fusto in c.a. usa curvature delle sezioni fessurate GPC con viscosità assegnata. La doppia integrazione fornisce u del fusto con base fissa; la stima disaccoppiata totale è utesta=ufusto+H/K−θHmuro. Il termine di rotazione conserva il segno. Le curvature mancanti impediscono il risultato. La gravità usa il modello elastico del materiale nel campo senza trazione.

### Spostamenti permanenti Newmark

Il blocco rigido scorre in una sola direzione. L’integrazione dei tratti lineari di a(t)−ky·g tiene conto degli attraversamenti della soglia, dell’arresto e della coda finale a terreno fermo. Wood è escluso perché presuppone un muro vincolato. Il risultato riguarda ciascuna storia; la scelta e la conformità normativa dell’insieme degli accelerogrammi devono essere documentate. Lo SLD non viene ricavato dal solo ag/g SLV.

### Armature e comando Calcola armature

Il predimensionamento usa ancoraggi a fyd e nessuna riduzione favorevole dei coefficienti di forma o confinamento. fbd deriva dalle proprietà GPC e dalle condizioni di aderenza. Le giunzioni sono alla stessa quota, quindi lo schema richiede il 100% delle barre giuntate e numeri compatibili nelle due zone. Si controllano lunghezza comune, interferro tra coppie, ingombro, area e passo dei collegamenti. Il mandrino considera anche la pressione nel calcestruzzo all’interno della piega.

### Gravità in calcestruzzo o muratura

In Materiali scegliere Calcestruzzo non armato oppure Muratura. Il primo usa fck e proprietà GPC, con compressione e taglio NTC 4.1.11 e fct1d=0,85 fctk,0.05/γc. Per muratura occorrono fk, fvk0, limite caratteristico a taglio, γM, fattore di confidenza e modulo elastico; non si possono usare automaticamente le resistenze del calcestruzzo.

Il fusto è una mensola libera: lunghezza efficace almeno 2H, imperfezione almeno H/200, rigidezza EI minima e amplificazione 1/(1−N/Ncr). La verifica rimane nel campo senza trazione e N<0,8Ncr. Se queste condizioni non sono soddisfatte serve un modello non lineare e l’esito non è dichiarato favorevole. Per muratura si controllano blocco compresso 0,85fk/(γM·FC) e scorrimento dei giunti; la resistenza a trazione è nulla. Le mensole di fondazione dello stesso materiale sono controllate anche a trazione, quindi una mensola in muratura può richiedere una diversa soluzione costruttiva.

La modalità Resistenze assegnate conserva la compatibilità con i file precedenti e i relativi controlli elastici; non diventa automaticamente una verifica normativa completa.

### Esempio ripercorribile e rapporto

Aprire supporto/artefatti/muri-completamento-20260930/interfaccia-finale/esempio-completo.anthea. Il modello dimostrativo ha H=3 m, B=3 m, due zone di armatura, terreno deformabile di spessore 25 m con M=30000 kPa, sisma da sito e una storia triangolare sintetica. Questi dati servono a riprodurre il test e non descrivono un sito reale. La storia sintetica non è un accelerogramma normativamente qualificato.

Nella stessa cartella sono presenti relazione Word e PDF, figure della sezione e risultati JSON. Il rapporto CONTROLLO.md e PDF nella cartella principale dell’attività descrive test, correzioni e limiti. I confronti MAX rimangono sospesi: nessuna delle nuove funzioni è dichiarata validata contro MAX 16.

Fonti: [JRC Eurocode 8 Worked Examples](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/EC8_Seismic_Design_of_Buildings-Worked_examples.pdf), §4.8; [JRC Eurocode 2 Detailing](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/05_EC2WS_Arrieta_Detailing.pdf); [USGS Newmark](https://pubs.usgs.gov/sir/2007/5196/sir2007-5196_text.pdf); NTC 2018 §§4.1.11 e 7.8.2.2.3; USACE EM 1110-1-1905, 2025.

### Relazioni degli spostamenti

La stima del cedimento integra l'incremento di tensione verticale nel profilo deformabile. I moduli devono essere coerenti con il campo di tensione del problema.

```math
s=\int\frac{\Delta\sigma_z}{M}\,dz
\theta=\frac{s_{monte}-s_{valle}}{B}
u_{testa}=u_{fusto}+\frac{H}{K}-\theta H_{muro}
```

Nell'ultima relazione H è la risultante orizzontale per metro di muro, non l'altezza; Hmuro è l'altezza geometrica. Con H in kN/m e K in kN/m², H/K è uno spostamento in metri. Somma i contributi conservandone i segni. Il modello disaccoppiato non ricostruisce l'interazione rigida terreno–fondazione.

## Efficienza orizzontale della palificata

Il modulo calcola fattori di riduzione per pali verticali identici sottoposti ad azione orizzontale con direzione comune. Non risolve l'equilibrio del plinto, la risposta non lineare p-y, la compatibilità degli spostamenti o la capacità laterale ultima. Sono esclusi diametri differenti, pali inclinati e conversioni empiriche automatiche fra rigidezza e capacità.

### Grandezze e geometria

L'efficienza di capacità sarebbe definita come segue.

$$ \eta_H=\frac{H_{gruppo}}{N H_{singolo}}

La scheda riporta invece la media dei p-multiplier come indicatore di riduzione per pali identici.

$$ \overline{P_m}=\frac{1}{N}\sum_{i=1}^{N}P_{m,i}

L'identificazione fra questa media ed efficienza di capacità richiede ipotesi sulla risposta dei singoli pali e sulla ripartizione del carico; non è effettuata come verifica di resistenza. Per Davisson il fattore Rg ha una grandezza fisica diversa.

$$ u=(\cos\theta,\sin\theta),\qquad v=(-\sin\theta,\cos\theta)

$$ q_i=r_i\cdot u,\qquad t_i=r_i\cdot v

La fila 1 corrisponde al massimo q, quindi al lato avanzato nel verso H. Le coordinate sono proiettate dopo una traslazione dell'origine per migliorare la stabilità numerica. La tolleranza di raggruppamento delle file è D per 10 alla meno 6. Gli interassi automatici di Davisson, AASHTO, FHWA e Rollins richiedono un reticolo completo con interassi uniformi nei due assi del carico. Per un reticolo obliquo o irregolare non esiste un interasse equivalente automatico giustificato da queste formule: il programma richiede accettazione esplicita della schematizzazione proiettata e interassi rappresentativi assegnati.

### Davisson

$$ R_g=\frac{k_{h,g}}{k_{h,1}}=\frac{n_{h,g}}{n_{h,1}}

$$ k_{h,g}=R_g k_{h,1},\qquad n_{h,g}=R_g n_{h,1}

| S parallelo su D | Rg |
| --- | --- |
| 3 | 0.25 |
| 4 | 0.40 |
| 5 | 0.55 |
| 6 | 0.70 |
| 7 | 0.85 |
| 8 e oltre | 1.00 |

$$ R_g=0.15\frac{S_{\parallel}}{D}-0.20\quad\text{per }3\leq\frac{S_{\parallel}}D\leq8

La riduzione del modulo è cento volte uno meno Rg, in percentuale. Sotto 3D il calcolo è indisponibile per default; l'opzione di estrapolazione usa la stessa retta, con limite zero–uno e avviso. L'interazione trasversale può essere trascurata nella schematizzazione citata per S trasversale almeno 2.5D. Sotto tale soglia il calcolo rimane non disponibile: non viene inventato un secondo fattore da moltiplicare. In assenza di file successive si assume assenza di riduzione longitudinale, sempre subordinata alla condizione trasversale.

Rg non coincide automaticamente con eta H. Una futura integrazione con il solutore del palo singolo dovrà ricalcolare il comportamento usando kh o nh ridotti, a parità di EI, lunghezza, vincolo, stratigrafia e criterio di capacità o spostamento. Solo il rapporto fra le capacità ricalcolate potrà essere chiamato efficienza di capacità. Nessuna potenza o conversione empirica di Rg viene utilizzata.

### AASHTO e FHWA

Le due tabelle sono mantenute come metodi distinti e con edizione esplicita. S è l'interasse delle file nella direzione del carico.

| Metodo | S su D | Fila 1 | Fila 2 | Fila 3 e successive |
| --- | --- | --- | --- | --- |
| AASHTO 2014 | 3 | 0.80 | 0.40 | 0.30 |
| AASHTO 2014 | 5 | 1.00 | 0.85 | 0.70 |
| FHWA 2018 | 3 | 0.70 | 0.50 | 0.35 |
| FHWA 2018 | 4 | 0.85 | 0.65 | 0.50 |
| FHWA 2018 | 5 | 1.00 | 0.85 | 0.70 |
| FHWA 2018 | 6 e oltre | 1.00 | 1.00 | 1.00 |

Interpolazione lineare fra nodi. Per AASHTO oltre 5D il software conserva i valori dell'ultimo nodo, con avviso: è una scelta conservativa dichiarata del modulo, non un'estrapolazione normativa fino a uno. Per FHWA da 6D i valori sono unitari. Sotto 3D il calcolo richiede l'opzione esplicita di estrapolazione. Non viene applicata una legge aggiuntiva di interazione trasversale: la sua assenza dalla tabella non prova l'assenza fisica dell'effetto.

### Rollins e FEMA

Ponendo lambda uguale a S su D, il modulo usa logaritmi naturali.

$$ P_{m,1}=\min(1,0.26\ln\lambda+0.50)

$$ P_{m,2}=\min(1,0.52\ln\lambda)

$$ P_{m,\geq3}=\min(1,0.60\ln\lambda-0.25)

Le prove di riferimento riguardano principalmente argilla rigida e interassi circa 3.3D–5.65D. Il programma richiede estrapolazione esplicita al di fuori di tale intervallo e impone anche un limite inferiore nullo. A 3D i tre valori sono 0.785639, 0.571278 e 0.409167; la media su tre file di pari numero di pali è 0.588695. L'indicazione del campo sperimentale non sostituisce la verifica della pertinenza geotecnica.

### Reese e Van Impe

Si considerano tutte le coppie distinte, senza limitarsi ai vicini. La distanza s è quella reale fra i centri, anche per coppie oblique. Per ciascuna coppia, il fattore affiancato è il seguente.

$$ \beta_a=0.64(s/D)^{0.34}\quad\text{per }1\leq s/D<3.75

Da 3.75D il fattore affiancato è uno. Per l'allineamento nel verso del carico si distinguono palo avanzato e arretrato.

$$ \beta_{bl}=0.70(s/D)^{0.26}\quad\text{per }1\leq s/D<4

$$ \beta_{bt}=0.48(s/D)^{0.38}\quad\text{per }1\leq s/D<7

Da 4D il fattore leading è uno; da 7D quello trailing è uno. I rami sono limitati a uno anche immediatamente prima delle soglie, per evitare lievi superamenti dovuti ai coefficienti empirici arrotondati. L'angolo phi è fra congiungente dei pali e carico; leading e trailing dipendono dal segno della differenza di q.

$$ \beta_{ji}=\sqrt{\beta_b^2\cos^2\phi+\beta_a^2\sin^2\phi}

$$ \beta_i=\prod_{j\ne i}\beta_{ji},\qquad P_{m,i}=\beta_i

L'interazione è direzionale: beta ji non coincide in generale con beta ij. Geometrie irregolari e carichi obliqui sono trattati direttamente. I pali sovrapposti sono rifiutati. Un solo palo ha prodotto vuoto unitario. La media finale pesa ciascun palo una volta, quindi rispetta anche file con popolazioni diverse.

### Caltrans Modified Reese

Il metodo applica alle interazioni precedenti il coefficiente alfa delle California Amendments di settembre 2025, riferite all'ottava edizione AASHTO LRFD. La fonte attribuisce la base di interazione a Reese et al. 2006. Non è una prescrizione delle NTC italiane.

$$ P_{m,i}=\alpha_i\beta_i

| Interasse medio fra file su D | Fila 1 | Fila 2 | Fila 3 | Oltre fila 3 |
| --- | --- | --- | --- | --- |
| 2 | 1.0 | 1.0 | 1.0 | 1.0 |
| 3 | 0.9 | 1.0 | 0.8 | 0.8 |
| 5 | 1.0 | 1.0 | 0.8 | 0.9 |
| 7 | 1.0 | 1.0 | 0.9 | 1.0 |
| 8 | 1.0 | 1.0 | 1.0 | 1.0 |

L'interasse è misurato nella direzione del carico; per interassi fra file non uniformi la fonte richiede la media aritmetica degli intervalli fra file. Non si usa la media delle distanze fra tutte le coppie. Si interpola linearmente. Oltre 8D il modulo mantiene alfa unitario; per una sola fila adotta alfa unitario, dichiarandolo. Sotto 2D occorre abilitare l'estrapolazione; alfa è limitato a zero–uno. Per disposizioni oblique o irregolari occorre accettare esplicitamente l'estensione per file proiettate prima di applicare la correzione per fila.

### Fonti e verifica

Il testo allegato dall'utente è la specifica delle formule e delle tabelle implementate. Il collegamento condiviso ChatGPT non era recuperabile durante l'implementazione. Le fonti ufficiali consultabili sono indicate per consentire il controllo dell'edizione: l'applicabilità resta distinta dal semplice corretto calcolo numerico.

- Davisson M T, 1970, Lateral Load Capacity of Piles, Highway Research Record 333, pagine 104–112. La tabella di riduzione è quella ripresa nella specifica allegata; non si afferma una verifica diretta della sua presenza nel lavoro originale.
- Davisson M T e Salley J R, 1970, Model Study of Laterally Loaded Piles, ASCE, volume 96, numero 5, pagine 1605–1627.
- [FHWA GEC 12 volume I, 2016, Design and Construction of Driven Pile Foundations](https://www.fhwa.dot.gov/engineering/geotech/pubs/gec12/nhi16009_v1.pdf): valori AASHTO 2014.
- [FHWA GEC 10, 2018, Drilled Shafts Construction Procedures and Design Methods](https://www.fhwa.dot.gov/engineering/geotech/nhi18024.pdf): tabella 10-41 dei p-multiplier.
- Rollins et al., 2006; FEMA P-751, 2012: relazioni logaritmiche nella specifica allegata.
- [California Amendments settembre 2025](https://dot.ca.gov/-/media/dot-media/programs/engineering/documents/caamendments/202509-aashto-lrfd-ca-amendments-a11y.pdf), paragrafo 10.7.2.4, equazioni 1–6 e tabella 10.7.2.4-2: interazioni palo-palo e modifica Caltrans.

La validazione automatica verifica nodi e interpolazioni, esempi numerici, leading e trailing, soglie, palo isolato, geometrie irregolari, simmetria per inversione del carico, invarianza per traslazione e rotazione congiunta di geometria e carico, archiviazione e rappresentazione WPF. I test sono conservati in supporto/test/HorizontalPileGroup.Checks e le evidenze in supporto/artefatti/efficienza-orizzontale.

## Risposta elastica del palo orizzontale

Il motore ElasticPile in GPCChecker.Geotechnics risolve una trave di Euler–Bernoulli su fondazione elastica di Winkler sotto azioni assegnate. L'esame del metodo stratificato esistente ha accertato che esso costruisce diagrammi limite ed equilibri di capacità, senza un solutore elastico riutilizzabile. Rimane quindi invariato; non è stato creato un secondo motore per la stessa formulazione. ANTHEA gestisce soltanto archivio, presentazione e adattamento degli input.

### Fonti consultate e attribuzione

La formulazione FEM di riferimento consultata il 2 ottobre 2026 è TU Delft, Computational Modelling, capitolo 4.1 Euler–Bernoulli beam elements, pagina web senza numerazione di pagina: https://interactivetextbooks.citg.tudelft.nl/computational-modelling/structural_linear/euler_bernouilli.html . Sono state consultate la relazione cinematica, l'ipotesi di trascurare le deformazioni da taglio e la discretizzazione Hermite con spostamento e rotazione nodali. La convenzione dei segni qui dichiarata viene usata coerentemente nell'implementazione.

Carlo Viggiani, Fondazioni, scansione locale fornita dall'utente: pagina PDF 237, pp. stampate 464–465 (§14.4.1); PDF 238, pp. 466–467; PDF 244, pp. 478–479 (equazione 14.25 e tabelle 14.5–14.6). Sono state lette visivamente anche le pagine adiacenti PDF 243 e 245, pp. 476–477 e 480–481. L'edizione non è identificabile nella scansione: la prima pagina contiene la fine della prefazione, datata dicembre 1998, senza frontespizio o colophon. Tale data non viene usata per dedurre l'edizione. La trattazione è di Viggiani; le correlazioni sono attribuite agli autori indicati nel libro. Gli articoli originali non sono stati consultati.

### Modulo di reazione e conversioni

Si definisce kh come pressione orizzontale divisa per spostamento: unità F/L³. La larghezza di interazione adottata è il diametro geotecnico D, costante. La pressione elastica sul palo è opposta allo spostamento; la reazione per unità di lunghezza q ha unità F/L.

$$ k = D k_h,\quad q=-k y

La rigidezza distribuita k ha unità F/L². Se l'utente assegna direttamente k, il diametro non viene moltiplicato una seconda volta. In tabella si mostra anche kh equivalente = k/D. Per una discretizzazione a molle concentrate sarebbe K_i = integrale di k sulla lunghezza tributaria, con unità F/L. Qui si calcola K* esclusivamente per la rappresentazione e la tabella dei nodi: il solutore continua a usare la matrice consistente. La lunghezza tributaria va dai punti medi dei due elementi adiacenti; agli estremi è una sola mezza lunghezza. Checker integra k su questi tratti, separatamente sui due lati di eventuali discontinuità. Non si assegnano molle scalari al posto della matrice FEM.

Sono disponibili kh costante manuale, nh manuale nella legge Reese–Matlock, selezione assistita di nh nelle tabelle 14.5 e 14.6, correlazione A γ/1,35 e k distribuito manuale. Viggiani scrive p = kh y, P = p d ed Es = kh d; il nostro k corrisponde a Es. p e P nel testo sono le intensità resistenti; la reazione sul palo qui ha il segno q = −k y.

$$ k_h(z)=n_h\frac{z}{D},\quad k(z)=E_s=n_h z

kh e nh hanno entrambi dimensioni F/L³, k ha F/L². La legge lineare è attribuita a Reese e Matlock (1956). Il testo la associa a terreni incoerenti, argille normalmente consolidate o debolmente sovraconsolidate; kh costante è associato alle argille sovraconsolidate. Le correlazioni costanti con E50 e cu discusse a p. 466 non sono assegnate automaticamente: questa implementazione mantiene kh costante come input manuale. L'utente documenta drenaggio, stato tensionale e livello di deformazione; non si deduce kh dalla sola classificazione del terreno.

### Parametri delle tabelle di Viggiani

La tabella 14.5 separa A, adimensionale, dai valori direttamente consigliati di nh. Gli intervalli di A non sono intervalli di nh. I valori tabellati di nh sono in N/cm³:

$$ 1\,\mathrm{N/cm^3}=1000\,\mathrm{kN/m^3}

| Addensamento | A orientativo | A consigliato | nh sabbie non immerse [N/cm³] | nh sabbie immerse [N/cm³] |
| --- | --- | --- | --- | --- |
| Sciolto | 100–300 | 200 | 2,5 | 1,5 |
| Medio | 300–1000 | 600 | 7,5 | 5 |
| Denso | 1000–3000 | 1500 | 20 | 12 |

Per la correlazione, equazione 14.25:

$$ n_h=\frac{A\gamma}{1,35},\quad \gamma'=\gamma_{sat}-\gamma_w

Con γ in kN/m³ si ottiene nh in kN/m³. Sotto falda si usa γ′. A è inizializzato alla media aritmetica dell’intervallo della singola riga, per preferenza esplicita dell’utente: 200, 650 o 2000. Il valore consigliato rimane distinto: 200, 600 o 1500. La media iniziale è una convenzione del software, non un valore consigliato da Viggiani. L’utente può modificarla e il ricalcolo conserva la scelta. La modalità correlazione è distinta dalla selezione dei valori di nh tabellati: ad esempio A=600 e γ=18 danno nh=8000 kN/m³, mentre la riga Medio non immerso della tabella dà 7500 kN/m³. Con γsat=20 e γw=9,81, la correlazione dà nh=4528,888889 kN/m³ sotto falda.

La tabella 14.6 conserva separatamente tutte le righe e gli autori come stampati nel libro. I valori sono orientativi e non intercambiabili fra fonti. Per ogni intervallo si adotta inizialmente la sua media aritmetica, modificabile. Una riga a valore unico conserva quel valore. Intervallo, media iniziale, valore adottato e autore sono distinti; non si mediano righe o autori diversi. Cambiando fonte, la scelta precedente deve essere mantenuta o sostituita esplicitamente.

| Terreno | nh [N/cm³] | Fonte indicata nella tabella 14.6 |
| --- | --- | --- |
| Argilla n.c. o lievemente o.c. | 0,2–3,5 | Reese, Matlock, 1956 |
| Argilla n.c. o lievemente o.c. | 0,3–0,5 | Davisson, Prakash, 1963 |
| Argilla organica n.c. | 0,1–1 | Peck, Davisson, 1970 |
| Argilla organica n.c. | 0,1–0,8 | Davisson, 1970 |
| Torba | 0,05 | Davisson, 1970 |
| Torba | 0,03–0,1 | Wilson, Hilts, 1967 |
| Loess | 8–10 | Bowles, 1968 |

Gli override manuali delle modalità assistite richiedono una motivazione e conservano valore di base, valore adottato, intervallo, fonte e condizioni. Il singolo valore della torba non è trasformato in un intervallo. Le considerazioni del testo su non linearità, effetti ciclici e durata del carico non introducono riduzioni automatiche nel modello elastico.

### Estensione a strati e falda

La formulazione di riferimento del libro è riferita a terreno uniforme. Nell'estensione numerica implementata si assegna il parametro locale di ciascuno strato, mantenendo z globale dal piano campagna: k(z)=nh,strato z. Non si azzera z e non si forza la continuità di k. Questa scelta di raccordo non è attribuita a una prescrizione originale di Viggiani. I nodi coincidono con le interfacce; per sabbie tabellate e correlazione A γ/1,35, la falda può suddividere uno strato in due tratti con lo stesso identificativo originario e parametri distinti. Non si modifica il valore manuale o il parametro della tabella 14.6 alla falda. I due lati di ogni salto sono conservati nei risultati. Non si moltiplica nuovamente nh z per D.

### Modello e condizioni al contorno

x cresce dalla testa verso la punta; z = x − Llibero. Ltotale > Llibero ≥ 0. Il tratto libero ha k = 0; gli strati coprono almeno tutta la lunghezza immersa. Si assume EJ positivo e costante, calcolato in Checker dai dati comuni della sezione e del materiale, con origine esplicita. Piccoli spostamenti, sezioni piane, deformabilità a taglio trascurata e molle bilaterali lineari; nessun accoppiamento assiale nel FEM laterale. N è ricavato separatamente dall'equilibrio assiale.

La convenzione adottata è H e y positivi verso destra, θ = y′, C positivo nel verso della rotazione nodale, M = EI y″, V = M′ e q = −k y. Ne consegue:

$$ EI y^{(4)} + k y = 0,\quad V'=q,\quad M'=V

H agisce sulla traslazione della testa. Nel solutore generale C può essere assegnato oppure ottenuto da un’eccentricità riferita alla testa, senza sommare i due input. Nell’interfaccia con dati condivisi l’eccentricità esistente e è invece la quota della forza sopra il piano campagna. Checker adatta tale convenzione con C = H (Llibero − e); la lunghezza totale è Linfissa + Llibero. Il momento interno al piano campagna dovuto a H è quindi H e, indipendentemente dalla ripartizione del braccio. La migrazione conserva esplicitamente la convenzione dei vecchi modelli separati.

Testa libera: spostamento e rotazione incogniti. Testa con rotazione impedita: θ = 0, traslazione libera e reazione rotazionale calcolata. Punta libera: nessun vincolo cinematico e azioni terminali nulle; cerniera: y = 0; incastro: y = θ = 0. Nessun incastro viene aggiunto per eliminare una labilità. In assenza di terreno il modello è stabile con punta incastrata, oppure con cerniera alla punta e rotazione impedita in testa; negli altri casi viene rifiutato. La fattorizzazione controlla inoltre singolarità e cattivo condizionamento.

Alla testa M = −C − Rθ e V = H. Alla punta i segni delle azioni interne sono coerenti con le reazioni esterne. Tutte le reazioni restituite sono azioni esercitate sul palo. Un carico negativo inverte i segni dell'intera risposta elastica.

### Rigidezza della sezione e dati condivisi

ElasticPileSection in Checker riutilizza ConcreteMaterialEN1992.Ecm e le inerzie SectionCircular e SectionCHS di Model. Per la sezione circolare in calcestruzzo:

$$ E_{cm}=22000\left(\frac{f_{ck}+8}{10}\right)^{0.3},\quad J=\frac{\pi D^4}{64}

Ecm è in MPa; con D in mm, J è in mm⁴. Si usa la sezione integra lorda in calcestruzzo, senza aggiungere il contributo delle armature. Fessurazione e viscosità non sono introdotte automaticamente. Per CHS, J = π (De⁴ − Di⁴)/64 ed E è il modulo dell’acciaio assegnato nell’editor iniziale; il contributo della malta è escluso. Non è attiva un’ipotesi di collaborazione composta implicita.

$$ EJ\,[\mathrm{kNm^2}]=\frac{E\,[\mathrm{MPa}]\,J\,[\mathrm{mm^4}]}{10^9}

L’override avanzato richiede un valore positivo e una motivazione. I risultati conservano EJ di base, EJ adottato, E, J, geometria e riferimento della sezione. È una rigidezza assegnata del modello elastico: My della capacità laterale è una grandezza diversa.

Geometria, carichi e strati sono unici nell’archivio ANTHEA. La risposta memorizza riferimenti e un’istantanea dei dati effettivamente risolti per consentire esportazioni verificabili; tale istantanea non è una seconda sorgente modificabile. La selezione della stratigrafia è esplicita. Un archivio legacy conserva i valori originali fino alla scelta dell’utente e archivia lo stato precedente alla migrazione.

Checker restituisce SectionDemands per ascissa e lato, con N, V e M concomitanti, riferimento a geometria e materiale e riferimenti agli estremi. Questi dati alimentano i verificatori di sezione descritti di seguito.

### Sforzo normale e verifica per ascissa

Checker aggiunge N ai risultati FEM e alle azioni strutturate per sezione. Con x dalla testa verso la punta e compressione positiva:

$$ N(x)=N_0+\int_0^x w(s)\,ds=N_0+w x

Il modello corrente ha geometria e materiali comuni lungo il palo e peso w costante. Per c.a. w=γca πD²/4; γca include l'armatura, che non viene sommata nuovamente. Per il CHS, Ast=πt(De−t) e w=γs Ast+γiniezione(πDgeo²/4−Ast), dopo conversione in metri. Il valore zero del peso dell'iniezione la esclude esplicitamente. I componenti del peso non vengono dedotti da EJ. Non sono introdotti resistenza assiale del terreno, spinta idrostatica o rigidezza geometrica.

PileSegments valida la copertura completa, gli identificativi univoci e le quote crescenti. I confini entrano nella mesh senza spostare interfacce o azzerare la profondità geotecnica. La sezione iniziale è un riferimento ai dati comuni; un tratto personalizzato contiene soltanto le differenze di armatura.

PileReinforcement, in GPCChecker.Concrete, orchestra SectionSolver a N costante, SectionShearCalculator e AnchorageCalculator. N positivo del palo viene convertito nel segno negativo e nei newton del solutore di sezione; M da kNm a Nmm. MRd è cercato nei due versi a ciascun N effettivo. Una soluzione con residuo assiale eccessivo, verso errato o momento trasversale non trascurabile non produce una resistenza valida. I rapporti usano azioni concomitanti e tutti i campioni disponibili, inclusi gli estremi FEM; non si combinano massimi indipendenti.

Per il taglio si conserva il verificatore NTC esistente. L'adattamento circolare esplicito usa bw=D e d dal baricentro delle barre del semicerchio teso; si adotta il minore d dei due versi. Staffe chiuse a 90° hanno due bracci; z/d è assegnato e deve essere confermato dall'utente. Non si trasferiscono automaticamente ai pali le riduzioni specifiche delle pile da ponte. Il risultato soddisfatto N–M–V attesta soltanto il perimetro dichiarato; SLE, instabilità, sisma, duttilità e dettagli completi sono separati.

### Continuità delle barre e proposta costruttiva

Le lunghezze di ancoraggio e sovrapposizione richiamano il motore Checker già validato, con barre ad aderenza migliorata, σsd=fyd e nessuna riduzione favorevole di forma o confinamento (α1…α5=1). La resistenza a trazione usata per l'aderenza è limitata a C60/75. Buona aderenza è una scelta esplicita. I risultati conservano lbd, l0, percentuale assegnata e controllo della distanza libera.

Per la proposta si aggiunge lo spostamento del diagramma delle forze di trazione a_l=z cotθ/2 per staffe ortogonali (EN1992-1-1 §9.2.1.3, presentazione JRC Arrieta 2011, diapositiva 33). Se il taglio non è confermato si usa il limite superiore cotθ=2,5 a fini preliminari. Per una barra che inizia o termina, l'estensione oltre il confine teorico è lbd+a_l, limitata allo sviluppo fisicamente disponibile. La lunghezza l0 non si aggiunge a un'interruzione senza giunto. Questa convenzione conservativa non sostituisce l'analisi locale del nodo testa o della punta e non certifica l'armatura di confinamento.

Le barre coincidenti per coordinate e diametro proseguono attraverso i tratti, anche quando cambia il numero complessivo delle barre. I gruppi della distinta raccolgono soltanto barre con lo stesso percorso, diametro, quote e sviluppi. Le barre che cessano o iniziano hanno ancoraggi autonomi; le code coincidenti dello stesso diametro che si intersecano attraverso un breve intervallo privo di domanda vengono riunite in una barra continua. I tagli sono ripartiti rispettando la lunghezza commerciale assegnata e sovrapposizioni esplicite; la distinta conteggia la lunghezza fisica, inclusi i tratti sovrapposti. La resistenza nominale non somma la doppia armatura del giunto. Il calcolo della lunghezza richiesta assume il 100% coerentemente con i giunti effettivamente raggruppati, anche se l’utente richiede una percentuale inferiore. La disposizione raggruppata non soddisfa automaticamente tale richiesta: il risultato resta preliminare e segnala la necessità di sfalsamento. Le quote teoriche, effettive, lbd, l0 e a_l sono grandezze distinte.

Al cambio di diametro o raggio nominale si associano soltanto barre sulla stessa direzione radiale, dopo aver mantenuto le coincidenze esatte continue. Per ciascuna coppia si costruisce una sola finestra di giunto. Si definiscono u=lbd,sup+a_l,sup e v=lbd,inf+a_l,inf. La lunghezza adottata è max(l0 iniziale dei due lati, l0 richiesta dei due lati, u+v), arrotondata per eccesso a 0,10 m. Posto e=[l0 adottata−(u+v)]/2, la barra inferiore inizia a b−v−e e quella superiore termina a b+u+e, dove b è il confine teorico. La loro intersezione misura esattamente l0 adottata, senza sommare due l0 indipendenti.

Il termine u+v è una convenzione conservativa del software per conservare il pieno sviluppo delle due armature nominali alla quota b; non è attribuito alla normativa come formula della lunghezza di sovrapposizione. Le formule di aderenza, ancoraggio, l0 richiesta e traslazione restano quelle del verificatore esistente e delle fonti già indicate. I risultati distinguono l0 richiesta, vincolo di sviluppo, lunghezza adottata e lunghezza fisica disponibile. Sviluppi esterni insufficienti non vengono inventati. Finestre di giunto interferenti e distanze trasversali non ammesse sono segnalate; non si accredita la capacità nominale dei gruppi interessati. Le coordinate delle barre sono nominali: il disegno non certifica piegature o spostamenti trasversali per l’accostamento delle barre.

La distinta include le sovrapposizioni una sola volta attraverso le lunghezze dei pezzi reali. Per otto barre lungo 12 m divise in due pezzi con l0=1,50 m, la quantità è 8×(12+1,50)=108 m. Per 16Ø24 ridotte a 8Ø24 a metà palo, le otto comuni restano continue e le altre otto hanno solo lo sviluppo dell’interruzione; non sono generati otto nuovi ferri al cambio. Le regressioni dirette coprono riduzione, aumento, diametri diversi, disposizioni ruotate, giunti commerciali, spazio insufficiente, giunti interferenti e quantità indipendenti dalle partizioni di verifica.

Per non accreditare capacità a barre insufficientemente sviluppate, Checker separa MRd nominale e utilizzabile. La disponibilità è limitata, cautelativamente, alle quote comprese fra inizio fisico+lbd+a_l e fine fisica−lbd−a_l di ciascun gruppo necessario alla sezione, usando gli sviluppi pertinenti ai due estremi. Fuori da questi intervalli MRd utilizzabile è nullo, il grafico si interrompe e il controllo resta parziale. Questo filtro richiede il pieno sviluppo a trazione anche nei punti dove potrebbe non occorrere: non è un modello di resistenza ridotta per aderenza e non certifica giunti o nodi speciali.

Il controllo opzionale dei minimi pali è limitato a NTC 2018 §7.2.5, testo della Gazzetta Ufficiale del 20 febbraio 2018, p.213 (pagina 7 del PDF del capitolo 7): As≥0,003Ac, φst≥8 mm e s≤8φL. Le ulteriori prescrizioni per zone dissipative e duttilità non sono attivate da questo controllo. La proposta automatica adotta la griglia, i limiti e il criterio descritti nella sezione sulla suddivisione costruttiva; resta una scelta software dichiarata. La lunghezza commerciale dei tagli è assegnata separatamente. La ricerca discreta delle armature richiama gli stessi verificatori per ogni candidato e per tutte le ascisse, con quantità, diametri longitudinali, diametri delle staffe e passi forniti dall'utente.

### Dettagli dei pali e comportamento adottato

Fonte consultata: D.M. 17 gennaio 2018, Gazzetta Ufficiale n. 42, supplemento ordinario n. 8, §7.2.5, pagina PDF 217, pagina stampata 213, disponibile sul portale ufficiale: https://www.gazzettaufficiale.it/eli/gu/2018/02/20/42/so/8/sg/pdf.

Il paragrafo distingue i pali dalle fondazioni superficiali. Per i pali in calcestruzzo richiede lungo il fusto As almeno 0,3% Ac, diametro trasversale almeno 8 mm e passo non oltre 8 diametri longitudinali. Sono i tre controlli specifici disponibili. In presenza delle condizioni dissipative indicate dal testo, sono richiesti ulteriori dettagli e controlli: estensione delle zone, armatura longitudinale almeno 1%, staffe singole a passo massimo 6 diametri, duttilità e condizioni aggiuntive sulle azioni. Questi ultimi non vengono dedotti dall'analisi elastica e restano esplicitamente esclusi. Non si dichiara quindi completa la verifica sismica del palo.

Il default Pilastro aggiunge, per scelta dell'utente, le regole del verificatore esistente per diametri, interassi, armatura minima e massima, staffe e trattenimento delle barre. La compressione per il minimo longitudinale è la massima positiva del tratto, convertita da kN a N. Se si sceglie Solo controlli comuni, l'esclusione delle regole dell'elemento resta nell'elenco dei controlli da completare. Il controllo della durabilità richiama CoverRequirements di Checker, con esposizione e fck condivisi, vita 50/100 anni e qualità dichiarata; nessuna correlazione è duplicata nell'interfaccia.

### Criterio di cambio sezione e tempi delle verifiche

Il passo iniziale predefinito della discretizzazione FEM è 0,50 m. Le discontinuità e i confini dei tratti introducono nodi aggiuntivi; il confronto di convergenza conserva il raffinamento interno. Il passo è un parametro numerico, indipendente dalla griglia costruttiva della proposta dei tratti, anch'essa di 0,50 m.

La ricerca costruttiva conserva griglia 0,5 m, lunghezza minima e vincoli commerciali già descritti. Quando disponibile, individua il primo attraversamento discendente di |M|max/2 dopo l'ultimo massimo assoluto e lo interpola fra le ascisse calcolate. Fra le partizioni ammissibili sceglie un confine vicino a tale quota; in parità usa il costo costruttivo. La ricerca controlla sia la parte precedente sia la successiva, senza creare un ultimo tratto troppo corto. Non equivale a dimezzare l'armatura: la sezione successiva deve essere dimensionata sulle azioni N-V-M concomitanti.

Il progresso MRd conta coppie di resistenze completate, una per ciascun N esatto distinto, compresi i valori recuperati dalla cache. I tempi esportati separano preparazione della sezione, costruzione dei solutori indipendenti, calcolo parallelo delle resistenze, verifiche N-M-V e dettagli. Modificare il solo passo delle staffe aggiorna taglio e dettagli riutilizzando il dominio N-M; cambiare diametro delle staffe può spostare le barre e invalida invece le resistenze. Le misure riproducibili sono raccolte in supporto/artefatti/palo-chiarezza.

La configurazione custom Coefficienti unitari modifica un'istanza della normativa GPC, impostando a 1 ogni proprietà gamma disponibile e alpha_cc. Materiali, geometria e azioni restano quelli assegnati; la modalità è tracciata. Non è una verifica con i coefficienti ordinari NTC e non altera EJ lordo né il peso unitario adottato.

### Calcolo parallelo e dipendenze dei risultati

La risposta FEM dipende da geometria, EJ, terreno, vincoli, carichi, peso e discretizzazione. Con EJ lordo e peso unitario complessivo del c.a. già adottati, cambiare l'armatura non cambia N–V–M. Le verifiche sono un secondo stadio. Il codice confronta una firma degli input fisici prima di riutilizzare la risposta e blocca le esportazioni fino al completamento delle nuove verifiche. Le quote dei tratti appartengono alla discretizzazione e possono richiedere il ricalcolo FEM.

Per ogni sezione sono raccolti gli N distinti esatti, senza arrotondamenti né interpolazioni del dominio. Ogni worker usa un'istanza indipendente del solutore di sezione; la costruzione delle mesh avviene in sequenza, poi le ricerche MRd nei due versi sono parallele. Un batch viene pubblicato soltanto se completato e ancora valido. I valori già calcolati sono riutilizzati soltanto con la medesima configurazione di sezione/materiali/solutore. Cancellazione e controllo di revisione impediscono la pubblicazione di risultati superati. Il numero di worker è limitato alle CPU disponibili, lasciandone una libera quando possibile.

### Sovrapposizioni iniziali e suddivisione costruttiva

La convenzione software autorizzata dall'utente è l0,iniziale = arrotondamento superiore a 0,10 m di 60φ, con φ convertito in metri. Il fattore è modificabile e registrato. La lunghezza adottata è max(l0,iniziale; l0,richiesta dal motore di aderenza); il default non sostituisce la verifica normativa. I risultati conservano separatamente le tre grandezze e lbd. Le regole del motore di aderenza, il trattamento della percentuale di barre sovrapposte e lo spostamento a_l già descritti restano invariati.

La proposta dei tratti è una ricerca discreta su quote multiple di 0,5 m, oltre alla punta esatta. Ogni tratto misura almeno max(3 m; minimo assegnato). La lunghezza teorica e lo sviluppo max(lbd,l0)+a_l riservato conservativamente a entrambe le estremità devono entrare in una delle barre 6/8/10/12 m consentite dal limite assegnato. Il criterio penalizza sfridi e variazioni interne della domanda, oltre al numero di tratti. Si tratta di una convenzione esecutiva preliminare del software, non di una prescrizione della fonte. L'assenza di soluzione viene segnalata senza ridurre il minimo.

La distinta mantiene continue le barre con identica disposizione su tratti adiacenti. I pezzi preferiscono lunghezze commerciali 6/8/10/12 m; l'ultimo può essere tagliato alla quota fisica richiesta. Gli sfridi non aggiungono capacità. I giunti sono raggruppati e restano da completare per sfalsamento e confinamento. Le posizioni delle staffe hanno intervalli uniformi non maggiori del passo assegnato; il confine interno appartiene al tratto successivo e solo l ultimo comprende la punta. La distinta conserva quantità e quote, senza inventare sviluppi dei ganci: la sagomatura esecutiva delle staffe resta da completare.

Interferro e copriferro richiamano le regole comuni di MemberDetailingCalculator. Le regole dei pilastri sono il default modificabile richiesto dall’utente, distinto dai minimi specifici dei pali. L'interferro minimo è max(20 mm, φmax, dg+5 mm); la distanza effettiva viene calcolata su tutte le coppie di barre. Copriferro nominale e margine geometrico richiedono cmin,dur e Δcdev. I minimi pali già documentati rimangono distinti, con conferma del campo applicabile. Il loro mancato rispetto o l'interferro insufficiente impediscono l'accettazione del candidato nel dimensionamento. Le verifiche di sezione, le prescrizioni costruttive e gli esiti pendenti sono presentati separatamente.

I test in supporto/test/ElasticPile.Checks e ElasticPile.UiChecks confrontano MRd seriale/parallelo, conservazione del FEM al cambio armatura, cancellazione, conversione dei segni nel foglio c.a., azioni personali persistenti, arrotondamento di 60φ, griglia e lunghezze minime, sovrapposizioni fisiche e interferro con riferimento indipendente sulla corda di una corona circolare. Le misure riproducibili seriale/parallelo/cache sono in ElasticPile.Performance; evidenze in supporto/artefatti/palo-parallelo.

### Fonti e validazione dell'estensione strutturale

Fonti consultate il 5 ottobre 2026: DM 17 gennaio 2018, NTC, Gazzetta Ufficiale, capitolo 4 (§4.1.2.3.5, §4.1.6.1.4) e capitolo 7 (§7.2.5); José M. Arrieta, Eurocode 2 Background and Applications, workshop JRC Bruxelles 20–21 ottobre 2011, diapositive 7–8, 16 e 33. Il materiale JRC è un supporto formativo, non una nuova edizione della norma. Le fonti Viggiani e le distinzioni PDF/pagina stampata riportate sopra restano invariate.

[NTC nella Gazzetta Ufficiale](https://www.gazzettaufficiale.it/eli/id/2018/02/20/18A00716/sg) · [Materiale formativo JRC](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/05_EC2WS_Arrieta_Detailing.pdf)

I test diretti dell'estensione controllano pesi su aree disgiunte, carico assiale nullo e invertito, azioni concomitanti, confini esatti della mesh, continuità dei tratti, MRd nei due versi e dipendenza da N, rifiuto dei casi fuori dominio, minimi selezionati, ancoraggi, sovrapposizioni e distinta. Riferimento indipendente per l'aderenza: φ16, σsd=400 MPa, fctk05=2 MPa, γc=1,5 e buona aderenza danno fbd=3 MPa, lbd=533,333 mm e l0=800 mm con tutte le barre sovrapposte. Restano attivi i confronti analitici FEM e Viggiani già documentati. Evidenze in supporto/artefatti/palo-armature.

### Discretizzazione e recupero delle sollecitazioni

Ogni elemento ha due nodi e quattro gradi di libertà, y1, θ1, y2, θ2. Le funzioni Hermite cubiche interpolano y; la rotazione è la derivata analitica del polinomio. Si dispone sempre un nodo al piano campagna, a ogni interfaccia e alle estremità dove agiscono i carichi. Ciascun intervallo è suddiviso in elementi uniformi con lunghezza non superiore al passo richiesto.

$$ K_e=\int_0^{l_e} EI B^T B\,dx+\int_0^{l_e} k N^T N\,dx

B contiene le derivate seconde delle funzioni di forma. Quattro punti di Gauss integrano esattamente la matrice della fondazione per k costante o lineare, perché il massimo grado dell'integrando è sette. Le componenti traslazione/rotazione hanno unità diverse coerenti con i rispettivi gradi di libertà. La matrice cambia con la mesh; non si assegna la stessa molla a ciascun nodo. La soluzione usa Cholesky simmetrica a banda, con scala diagonale e vincoli imposti senza penalità. Sono ammessi al massimo 4000 elementi per soluzione, inclusa la mesh raffinata.

Le forze terminali dell'elemento provengono dalla matrice completa dell'elemento moltiplicata per i suoi gradi di libertà. Da esse si ricostruiscono V e M integrando analiticamente q = −k y e le equazioni di equilibrio. Non si derivano ripetutamente gli spostamenti campionati. Il recupero equilibrato converge alla relazione costitutiva della trave; a mesh finita è distinto dalla semplice curvatura cubica elementare.

Gli estremi vengono ricercati agli estremi degli elementi e alle radici delle derivate dei polinomi di y, θ, V, M e q. Le radici sono isolate mediante gli intervalli monotoni determinati dalle radici della derivata e bisezione. Otto intervalli per elemento servono al disegno, non limitano la ricerca degli estremi. I lati superiore e inferiore sono conservati separatamente: y e θ sono continui, V e M sono continui alle interfacce senza carichi concentrati salvo residui numerici, mentre k, kh e q possono saltare.

### Equilibrio e convergenza

Le reazioni dei vincoli sono ricavate dal residuo della matrice completa prima dell'eliminazione dei gradi di libertà vincolati. Le risultanti del terreno e i loro momenti rispetto alla testa sono integrali analitici dei polinomi elementari.

$$ R_F=H+\int q\,dx+R_{H,p}
$$ R_M=C+R_{\theta,t}+R_{\theta,p}+L R_{H,p}+\int xq\,dx

Entrambi i residui devono tendere a zero. Il report mostra inoltre numero degli elementi e confronto fra mesh h e h/2. Il risultato conservato appartiene alla mesh fine. Si confrontano ytesta, |M|max e |V|max; variazione relativa non superiore a 0,001 per tutti e tre identifica la soglia soddisfatta. Per un valore nullo si usa una scala riferita all'estremo del diagramma; un modello a carico nullo dà variazioni nulle. Il controllo fra due mesh non sostituisce una stima dell'errore geotecnico. Se la soglia non è soddisfatta i risultati sono segnalati come da raffinare, senza trasformarli in una verifica positiva.

### Riferimenti indipendenti e prove riproducibili

Per un palo semi-infinito con k costante, testa libera e solo H, la soluzione decadente dell'equazione differenziale fornisce un riferimento indipendente dalla discretizzazione:

$$ \beta=\left(\frac{k}{4EI}\right)^{1/4},\quad y_0=\frac{H}{2EI\beta^3}
$$ y(x)=y_0 e^{-\beta x}\cos(\beta x)
$$ V(x)=H e^{-\beta x}\left[\cos(\beta x)-\sin(\beta x)\right]
$$ M(x)=\frac{H}{\beta}e^{-\beta x}\sin(\beta x)

Il primo massimo di M è a x = π/(4β). Con rotazione impedita in testa ytesta è metà del valore libero e la reazione rotazionale è H/(2β). Il test usa D=1 m, L=30 m, EI=50000 kNm², H=100 kN, kh=10000 kN/m³; la lunghezza corrisponde a circa 14,19/β e l'influenza del bordo distante è trascurabile rispetto alle tolleranze adottate. Un ulteriore riferimento è la mensola senza molle con punta incastrata: ytesta = HL³/(3EI) − CL²/(2EI), θtesta = −HL²/(2EI) + CL/EI.

| Passo effettivo [m] | ytesta [m] | Mmax [kNm] | Vmax [kN] |
| --- | --- | --- | --- |
| 1,00 | 0,00945552714 | 68,1749852 | 100 |
| 0,50 | 0,00945729422 | 68,1783709 | 100 |
| 0,25 | 0,00945740841 | 68,1786341 | 100 |
| Analitico semi-infinito | 0,0094574161 | 68,178652 | 100 |

Fra 0,50 e 0,25 m le variazioni relative sono 1,2075×10⁻⁵ per y e 3,8612×10⁻⁶ per M; V resta H entro l'errore numerico. Il confronto analitico richiede errori relativi 2×10⁻⁶ per y e θ, 2×10⁻⁵ per Mmax e 10⁻⁴ per la sua profondità; la forma del taglio è controllata sull'intero tratto attivo con errore normalizzato a H inferiore a 10⁻⁶. Le prove di equilibrio impongono residui inferiori a 10⁻⁷ kN e 10⁻⁶ kNm sul benchmark. La conversione N/mm rispetto a kN/m è controllata con tolleranza relativa 10⁻⁸.

I sorgenti supporto/test/ElasticPile.Checks referenziano direttamente il progetto Checker. Coprono anche carico nullo, linearità, inversione, tratto libero, discontinuità degli strati, equivalenza di sottostrati identici, conversione kh–k, origine globale z, unità, vincoli, dati invalidi e labilità. supporto/test/ElasticPile.UiChecks verifica instradamento, invalidazione, interfaccia ed esportazioni. Le evidenze originarie sono in supporto/artefatti/palo-elastico; la revisione con dati comuni usa supporto/artefatti/palo-condiviso. I nuovi controlli verificano medie distinte dai valori consigliati, provenienza e override, EJ circolare e tubolare rispetto a riferimenti analitici, conversioni, lunghezze tributarie, risultati per sezione e assenza di doppio conteggio dell’eccentricità. Le prove ANTHEA coprono dati comuni, migrazione con confronto della risposta legacy, ricalcolo, invalidazione, visibilità ed esportazioni; le regressioni esistenti comprendono 1080 controlli del palo orizzontale, CHS e 1645 controlli stratificati. I test preesistenti Checker filtrati sulle classi Pile sono stati eseguiti: 32 superati, zero fallimenti. Si tratta di validazione numerica del modello elastico dichiarato; non di taratura sperimentale delle leggi del terreno.

### Confronto con le soluzioni di Reese e Matlock riportate nel libro

Alle pp. stampate 466–467 (PDF 238) la lunghezza caratteristica è λ=(EI/nh)^(1/5). Per L/λ > 4, testa libera e solo H, il testo riporta y0=2,40 H/(nh^0,6 EI^0,4) e |θ0|=1,60 H/(nh^0,4 EI^0,6). Per testa con rotazione impedita il coefficiente di y0 è 0,93. Il segno della rotazione del testo viene adattato alla convenzione θ=y′ adottata qui. Le soluzioni pubblicate usano coefficienti approssimati e costituiscono un riferimento distinto dal test di convergenza.

Il test usa EI=50000 kNm², nh=5000 kN/m³, L=30 m, H=100 kN e punta libera. Si ottengono ytesta=0,0193414613 m contro 0,0191091442 m della formula (scarto 1,216%); |θtesta|=0,00813548843 rad contro 0,00803803658 rad (1,212%). La tolleranza dichiarata è 2%. A testa bloccata ytesta=0,00738777995 m contro 0,00740479337 m (0,230%, tolleranza 1%). Il test con solo momento verifica anche i coefficienti 1,60 e 1,74: scarti 1,212% e 0,389%, tolleranza 2%. Questi scarti non sono errori di mesh.

Per L/λ < 2, il riferimento rigido con solo H è y0=18H/(nh L²), |θ0|=24H/(nh L³); con rotazione impedita y0=2H/(nh L²). Il test L=0,2 m, H=0,001 kN confronta il solutore con le espressioni del libro nel limite rigido, con tolleranza 0,1%.

| Passo [m] | ytesta [m] | massimo assoluto M [kNm] | massimo assoluto V [kN] |
| --- | --- | --- | --- |
| 0,50 | 0,0274768019 | 200,317922 | 100 |
| 0,25 | 0,0274769497 | 200,317875 | 100 |
| 0,125 | 0,0274769590 | 200,317872 | 100 |

La tabella usa lo stesso palo lungo con H=100 kN e C=−100 kNm. Le variazioni relative fra le due mesh più fini sono 3,38×10⁻⁷ per ytesta, 1,33×10⁻⁸ per M e meno di 10⁻¹⁰ per V. Il nucleo di test FEM, Viggiani e dati condivisi comprende 124 asserzioni, oltre ai controlli aggiunti per N, sezioni e dettagli delle armature: cataloghi, conversione, correlazione e falda, equivalenza manuale/assistita, origine globale di z, discontinuità, equilibrio, riferimenti analitici, mesh, unità, vincoli e labilità. Restano distinti i 32 test preesistenti delle classi Pile, inclusi Broms e capacità stratificata. Le prove non costituiscono una taratura sperimentale delle rigidezze.

## Elementi Beam

Una trave reale occupa un volume; un elemento beam descrive invece come si muove una linea e come ruotano le sezioni lungo quella linea. Questa riduzione rende leggibile il percorso dei carichi e permette di analizzare telai con molti elementi, purché le dimensioni trasversali e i fenomeni locali non governino la risposta cercata. Il capitolo accompagna la scelta del modello, la lettura dei risultati e il trasferimento delle azioni alla verifica della sezione in Anthea.

### Cos'è un elemento beam

Il beam è un elemento finito monodimensionale con una sezione associata. L'asse geometrico collega due nodi, mentre area, inerzie e rigidezza torsionale rappresentano la parte trasversale. Il calcolo cerca spostamenti e rotazioni compatibili con equilibrio, vincoli e legame costitutivo. Le tensioni tridimensionali complete non sono tutte incognite indipendenti: si ricostruiscono a partire dalle ipotesi di trave.

Una trave trasferisce un carico trasversale agli appoggi soprattutto mediante flessione e taglio. La flessione produce una coppia di risultanti di trazione e compressione. Allontanare queste risultanti aumenta il braccio della coppia: per questo una sezione alta può essere molto più rigida di una bassa con la stessa area. Una forza allineata all'asse produce invece sforzo normale; un carico eccentrico rispetto al centro di taglio può introdurre torsione.

### Idealizzazione della struttura reale

Scegliere la linea d'asse significa decidere dove si trasferiscono le azioni. In una trave omogenea spesso si usa il baricentro; in sezioni composte o con eccentricità bisogna dichiarare il riferimento effettivo. Larghezza dell'appoggio, nodi estesi e zone di collegamento non scompaiono fisicamente: sono condensati in vincoli, offset o legami. Un modello è adeguato se restituisce le grandezze necessarie alla decisione, non perché assomiglia al disegno architettonico.

Confronta prima un modello semplice con equilibrio manuale e poi aggiungi dettagli. Per un telaio, controlla quali nodi sono realmente rigidi, quali consentono rotazione e quali impediscono traslazione. Una connessione bullonata non è automaticamente una cerniera ideale; una connessione gettata non è automaticamente infinitamente rigida.

### Nodi e asse dell'elemento

I nodi iniziale e finale definiscono lunghezza e verso dell'asse locale x. Due punti con coordinate uguali non sono necessariamente lo stesso nodo: se i rispettivi gradi di libertà non sono condivisi o collegati, gli elementi restano scollegati. Nei modelli importati controlla la connettività numerica, oltre alla coincidenza visiva.

![Fig. 6.1 Elemento beam tra i nodi i e j con asse locale x e direzioni trasversali y e z.](../../X.Desktop/Assets/Wiki/beam-axes.png)

La linea del beam rappresenta un riferimento strutturale. Non usare la sola linea inferiore del disegno come asse senza verificare l'eccentricità rispetto alla sezione. Cambiare il verso i-j richiede controllare assi, carichi locali e convenzioni dei risultati.

### Sistema locale e globale

Il sistema globale permette di assemblare elementi orientati diversamente. Il sistema locale permette di descrivere la risposta assiale e le due flessioni della singola trave. La trasformazione tra i due è una rotazione delle componenti, non una modifica del fenomeno fisico. Le inerzie Iy e Iz devono corrispondere alla sezione orientata nei rispettivi assi locali.

Definisci sempre una terna destrorsa. L'asse x coincide normalmente con i-j; gli altri due assi dipendono dalla regola di orientamento adottata dal solutore. Una trave verticale richiede particolare attenzione: una regola basata su un vettore parallelo alla trave può diventare indeterminata. Visualizza la terna e controllala su un elemento rappresentativo prima di assegnare carichi locali a un gruppo.

### Gradi di libertà

Nel beam spaziale classico ogni nodo ha tre traslazioni e tre rotazioni. Le dodici componenti dell'elemento descrivono movimento assiale, due flessioni e torsione. Un modello piano ne conserva normalmente tre per nodo: due traslazioni e una rotazione. Formulazioni con ingobbamento possono aggiungere altre incognite: non assumere che tutti i beam abbiano esattamente gli stessi gradi di libertà.

![Fig. 6.2 Traslazioni e rotazioni nodali nel piano; deformata amplificata a solo scopo illustrativo.](../../X.Desktop/Assets/Wiki/beam-dof.png)

Una rotazione nodale è un'incognita cinematica. Non equivale sempre alla pendenza della linea deformata: questa uguaglianza vale nella cinematica Euler-Bernoulli, mentre nel modello Timoshenko la differenza rappresenta la deformazione a taglio.

### Rigidezza assiale

```math
k_a = \frac{EA}{L}
\delta = \frac{NL}{EA}
```

E è il modulo elastico, A l'area resistente e L la lunghezza; N è uno sforzo assiale costante. Con E in N/mm², A in mm² e L in mm, kₐ è in N/mm e δ in mm. La seconda espressione vale per un'asta prismatica elastica con forza costante. Raddoppiare la lunghezza dimezza la rigidezza e raddoppia l'allungamento, a parità di N, E e A.

Un'area sovrastimata irrigidisce il percorso assiale e può attirare carico da elementi paralleli. In un sistema iperstatico non basta che la verifica della singola sezione sia soddisfatta: la distribuzione delle azioni dipende dalle rigidezze relative.

### Dalla cinematica alla matrice

Nel problema flessionale piano Euler-Bernoulli le incognite di un elemento a due nodi sono spostamento trasversale e rotazione ai due estremi. L'interpolazione cubica di Hermite garantisce continuità di spostamento e pendenza. Indicando con $B$ la matrice delle derivate seconde delle funzioni di forma e con $K_e$ la matrice di rigidezza elementare:

```math
K_e=\int_0^LB^T EI B\,dx
```

La matrice trasforma spostamenti e rotazioni nodali nelle azioni nodali elastiche. Il carico distribuito deve entrare anche nel vettore nodale coerente: applicare soltanto metà della risultante a ogni nodo può perdere le coppie equivalenti. Un elemento libero possiede moti rigidi; la singolarità scompare soltanto con vincoli fisicamente sufficienti, non aumentando artificialmente la rigidezza.

![Figura 09.1 — Trave di 8 m con carico 25 kN/m, cerniera e carrello. Le reazioni sono 100 kN; il momento positivo è parabolico e raggiunge 200 kNm in mezzeria.](../../X.Desktop/Assets/Wiki/beam-benchmark.svg)

Nello schema, il momento positivo tende le fibre inferiori. L'area sotto il carico è 200 kN e coincide con la somma delle reazioni. Il diagramma nullo agli appoggi è una verifica indipendente dei rilasci terminali. L'esempio è statico, elastico e del primo ordine; non rappresenta fessurazione, viscosità o variazioni di rigidezza.

La formulazione variazionale e l'interpolazione sono documentate nel capitolo *Euler-Bernoulli beam elements* del manuale di modellazione computazionale TU Delft. Il segno di curvatura può differire fra testi: la relazione costitutiva deve essere letta con la stessa convenzione del momento, senza combinare definizioni provenienti da solutori diversi.

### Flessione

```math
\kappa = \frac{M}{EI}
\sigma = \frac{M}{W}
```

κ è la curvatura elastica, M il momento e I l'inerzia rispetto all'asse di flessione. W è il modulo resistente elastico relativo alla fibra considerata. Con M in Nmm e EI in Nmm², κ è in 1/mm; σ risulta in N/mm². La formula della tensione descrive una sezione omogenea in regime elastico e non sostituisce la verifica non lineare di una sezione in c.a.

Le componenti della matrice flessionale contengono termini proporzionali a EI/L³, EI/L² ed EI/L: traslazioni e rotazioni hanno unità differenti. Non confrontare direttamente i numeri di due termini senza considerare le relative incognite. Nei telai, la rigidezza flessionale governa anche la ripartizione dei momenti fra travi e pilastri.

### Taglio

Il taglio è la risultante delle tensioni tangenziali sulla sezione. Nel modello Timoshenko la rigidezza trasversale è rappresentata da GAs, dove G è il modulo a taglio e As un'area efficace dipendente dalla formulazione e dalla forma della sezione. L'area efficace non coincide necessariamente con l'area geometrica totale.

```math
\gamma = \frac{V}{GA_s}
```

V è la forza di taglio e γ la deformazione angolare, adimensionale. Trascurare questa deformazione può sottostimare gli spostamenti di travi corte, sezioni con anima deformabile o materiali con basso modulo a taglio. La presenza di un risultato V nel solutore non dimostra che la deformabilità a taglio sia stata inclusa: anche un beam Euler-Bernoulli fornisce il taglio per equilibrio.

### Torsione

```math
\theta = \frac{TL}{GJ}
```

Per una barra prismatica in torsione uniforme di Saint-Venant, T è il momento torcente e J la costante torsionale; con unità coerenti θ è una rotazione in radianti. J non coincide generalmente con il momento polare d'inerzia: l'identificazione è corretta per sezioni circolari, ma può essere gravemente errata per sezioni aperte sottili.

Il beam ordinario non descrive automaticamente bimomento, torsione non uniforme e ingobbamento impedito. Vicino a un incastro o a un diaframma può essere necessario un modello con ingobbamento oppure una modellazione locale shell. Se il carico non passa per il centro di taglio, controlla la torsione generata dall'eccentricità.

### Euler-Bernoulli e Timoshenko

Euler-Bernoulli assume che le sezioni restino piane e ortogonali all'asse deformato, trascurando la deformazione a taglio. Timoshenko mantiene una rotazione di sezione indipendente dalla pendenza della linea d'asse e include il contributo a taglio. La scelta dipende dalla precisione richiesta e dal rapporto fra contributi deformativi; non esiste un unico rapporto L/h che renda corretto ogni modello.

| Aspetto | Euler-Bernoulli | Timoshenko |
| --- | --- | --- |
| Cinematica | Rotazione legata alla pendenza | Rotazione indipendente |
| Taglio deformabile | Trascurato | Incluso mediante area efficace |
| Controllo | Adeguatezza della snellezza e delle ipotesi | Parametri di taglio e possibili fenomeni di locking |

Un elemento numerico mal formulato può diventare artificialmente rigido nel limite di trave snella: è il locking a taglio. Raffinare la mesh senza comprendere il fenomeno non è sempre sufficiente. Verifica il benchmark della formulazione e confronta una soluzione analitica coerente con le stesse ipotesi.

### Proprietà della sezione

L'area governa la risposta assiale; Iy e Iz governano le flessioni; J governa la torsione di Saint-Venant; le aree efficaci governano il taglio deformabile. Il baricentro e il centro di taglio descrivono riferimenti differenti. Per sezioni non simmetriche occorre considerare anche il prodotto d'inerzia o usare assi principali.

Nel c.a. l'inerzia geometrica integra non è automaticamente la rigidezza efficace di esercizio di una sezione fessurata. Per sezioni composte, la rigidezza dipende da materiali, omogeneizzazione, collaborazione e fasi. Specifica la rigidezza adottata nel modello globale e non scambiarla con la resistenza ultima calcolata separatamente.

### Orientamento

Ruotare una sezione rettangolare di 90° scambia i ruoli delle due inerzie. Nel caso di una sezione b × h, l'inerzia rispetto all'asse parallelo alla base è bh³/12: il cubo dell'altezza rende l'orientamento decisivo. Il disegno della sezione e la freccia del carico devono essere coerenti con gli assi usati per i risultati.

Controlla il segno dei momenti dopo ogni cambio di orientamento. Nella sezione in c.a. Anthea, il piano della sezione è x-y e Mx è associato alla coordinata y; non coincide automaticamente con la terna x-y-z di un beam, il cui x è longitudinale. Il trasferimento richiede una mappatura esplicita.

### Offset ed eccentricità

Un offset collega il nodo alla sezione terminale tramite un tratto rigido o una trasformazione cinematica. Non equivale a spostare soltanto il disegno. Trasferisce anche momenti quando la risultante di forza agisce fuori dal riferimento. Una forza F con eccentricità e introduce una coppia di modulo Fe nel caso piano.

![Fig. 6.3 Offset tra asse del beam e punto di applicazione della forza; la coppia aggiuntiva dipende dal braccio e.](../../X.Desktop/Assets/Wiki/beam-offset.png)

Usare contemporaneamente un offset rigido e una coppia già inclusa nei carichi può contare due volte l'eccentricità. Controlla il punto rispetto al quale sono fornite le azioni. Non modellare come rigidissimo un collegamento realmente deformabile solo per risolvere un problema di connettività.

### Releases

Una release elimina il trasferimento di una specifica azione terminale rendendo libero il grado di libertà coniugato. Una cerniera flessionale non elimina per forza taglio, sforzo normale e torsione. In un modello spaziale occorre dire quale rotazione è rilasciata e rispetto a quale asse.

Rilasciare la stessa rotazione su tutti gli elementi di un nodo senza un controllo dei vincoli può creare un meccanismo. Il messaggio di matrice singolare è una conseguenza del modello: non va eliminato aggiungendo molle arbitrarie. Disegna la cinematica consentita e controlla che non esista uno spostamento rigido senza energia.

### Discretizzazione

Inserisci nodi dove cambiano carico, sezione, connessione o vincolo. Per carichi uniformi usa il vettore di carico coerente previsto dall'elemento, anziché sostituire il carico con forze concentrate senza verificarne l'equivalenza. Una mesh più fitta migliora alcune approssimazioni, ma non corregge un vincolo sbagliato o un'ipotesi costitutiva inadatta.

Confronta almeno due discretizzazioni e osserva la stabilizzazione delle grandezze decisive: freccia, reazioni, momenti critici. Il numero di elementi necessario dipende dall'interpolazione e dal risultato cercato. Una soluzione nodale esatta in un caso particolare non garantisce la ricostruzione esatta di tutto il diagramma interno.

### Risultati FEM

Prima delle tensioni controlla deformata e reazioni. Le reazioni devono equilibrare le azioni esterne; la deformata deve rispettare i vincoli; il percorso dei carichi deve essere plausibile. Poi leggi N, Vy, Vz, T, My e Mz nel sistema dichiarato dal solutore.

Le azioni terminali, le azioni sulle facce di una sezione e le risultanti interne possono usare segni diversi. Un salto del diagramma di taglio può essere fisico in corrispondenza di una forza concentrata. Una discontinuità di momento senza coppia applicata richiede invece controllare la ricostruzione, il verso e la connettività.

### Convenzioni di segno

Indica su uno schizzo un momento positivo e le fibre che comprime. Definisci il verso positivo dello sforzo normale. Nell'interfaccia corrente della sezione in c.a. Anthea usa N negativo a compressione; gli archivi precedenti con compressione positiva vengono migrati dal workspace. Il motore legacy può adottare una convenzione diversa da quella esposta nella UI. Confronta il riferimento del solutore FEM e quello della scheda, converti il segno una volta nel passaggio dei dati e mantieni traccia del riferimento originale.

Le verifiche biassiali richiedono conservare l'accoppiamento fra N, Mx e My della stessa combinazione. Non comporre una terna prendendo separatamente i massimi assoluti da combinazioni diverse senza dichiarare la scelta conservativa e controllarne il significato fisico.

### Limiti del modello

Un beam non risolve automaticamente concentrazioni di tensione, diffusione su appoggi, instabilità locale di piastre, ovalizzazione o dettagli delle saldature. Un modello elastico del primo ordine non include da solo plasticità, fessurazione evolutiva, grandi rotazioni e instabilità geometrica. Ogni estensione richiede una formulazione e parametri coerenti.

Quando una zona locale governa, usa un modello dedicato e condizioni al contorno derivate dal modello globale. Il passaggio beam-shell-solid richiede compatibilità del trasferimento di forza e momento: collegare un solo nodo può non rappresentare il comportamento del giunto reale.

### Errori comuni

> ERRORI COMUNI
> Nodi coincidenti ma scollegati; elementi duplicati che raddoppiano la rigidezza; inerzie scambiate; J sostituito dal momento polare; release che generano un meccanismo; metri mescolati a millimetri; carico locale applicato nel verso globale; offset ed eccentricità conteggiati due volte.

Per trovare un errore riduci il modello al percorso coinvolto. Applica un carico unitario, visualizza la deformata e confronta le reazioni con l'equilibrio. Un risultato numerico convergente può essere perfettamente coerente con un modello fisicamente sbagliato.

### Regole pratiche

> REGOLA PRATICA
> Prima verifica equilibrio, connettività e assi; poi raffina la mesh. Usa una trave appoggiata o una mensola come controllo della catena unità-carichi-rigidezza. Queste sono procedure di controllo del modello e non verifiche normative.

Per la trave appoggiata prismatica elastica, il momento massimo cresce con L² e la freccia flessionale con L⁴ a carico lineare costante. Piccole modifiche della luce possono quindi influenzare gli spostamenti più dei momenti. Questo ordine di grandezza aiuta a individuare errori di scala, ma non sostituisce il calcolo del sistema effettivo.

### Esempio concettuale

Considera una trave semplicemente appoggiata di luce L = 8 m con carico uniforme q = 25 kN/m. Per questo esempio q è già un'azione di progetto assegnata: non stiamo ricavando una combinazione normativa. Le reazioni sono ciascuna qL/2 = 100 kN; la somma 200 kN equilibra il carico totale.

```math
M_{\max} = \frac{qL^2}{8} = \frac{25\cdot8^2}{8} = 200\ \mathrm{kN\,m}
V_{app} = \frac{qL}{2} = 100\ \mathrm{kN}
```

![Fig. 6.4 Trave appoggiata con carico uniforme; diagramma qualitativo del momento positivo massimo in mezzeria.](../../X.Desktop/Assets/Wiki/beam-load.png)

Per una sezione rettangolare integra b = 600 mm e h = 800 mm, $I = \frac{bh^3}{12} = 25{,}6\cdot10^9\ \mathrm{mm^4}$. Se si assume, solo per il controllo elastico del modello, E = 30.000 N/mm², la freccia flessionale è $v_{\max} = \frac{5qL^4}{384EI} \simeq 1{,}74\ \mathrm{mm}$, usando q = 25 N/mm e L = 8.000 mm. È la freccia del modello omogeneo non fessurato: non è una verifica SLE del c.a.

Confronta la reazione e il momento analitico con il modello beam. Poi trasferisci la terna N = 0, Mx = 200 kNm, My = 0 a una sezione in c.a. la cui altezza è lungo y. Il valore 200 kNm deve essere associato all'asse della sezione che produce compressione e trazione lungo l'altezza. Il segno definisce quale bordo è compresso.

### Interazione con Anthea

Il pulsante Apri esempio crea una Sezione in c.a. con geometria rettangolare 600 × 800 mm, materiale C35/45 e armature iniziali del modulo: 4Ø20 superiori, 6Ø24 inferiori, 2Ø16 per lato, staffe Ø10/200 e copriferro netto 70 mm. La sola combinazione plastica contiene N = 0, Mx = 200 kNm, My = 0. Le altre famiglie restano vuote, perché non sono state definite azioni di esercizio o accidentali.

Questi valori costituiscono un esempio didattico, non una proposta esecutiva. Modifica l'altezza mantenendo luce e azione assegnata; osserva come cambiano dominio resistente e deformazioni di sezione. Modifica poi il segno di Mx per vedere il ruolo delle diverse armature ai due bordi. Il modulo verifica sezioni, non risolve la trave generale FEM né determina automaticamente il carico q.

[Guida del modulo Sezione in c.a.](/wiki/guide/moduli/sezione-ca)

### Riepilogo

Un beam concentra il comportamento di una struttura allungata in una linea con proprietà di sezione. La qualità del risultato dipende da cinematica, collegamenti, orientamento, rigidezze e unità. Verifica prima il comportamento globale; trasferisci poi azioni e convenzioni a una verifica locale appropriata. Raffinare una discretizzazione corretta e scegliere una formulazione adeguata sono due controlli distinti.

### Argomenti correlati

[Elementi Shell](/wiki/manuale/fem-e-modellazione/elementi-shell)

[Releases e connettività](/wiki/manuale/fem-e-modellazione/releases-e-connettivita)

[Instabilità delle aste compresse](/wiki/manuale/acciaio/instabilita-delle-aste-compresse)

### Riferimenti

- TU Delft, Computational Modelling, Euler-Bernoulli beam elements: derivazione della cinematica e dell'interpolazione. https://teachbooks.tudelft.nl/computational-modelling/structural_linear/euler_bernouilli.html
- OpenSees, documentazione Elastic Beam Column Element: esempio verificabile delle proprietà richieste da una formulazione beam. https://opensees.berkeley.edu/wiki/index.php/Elastic_Beam_Column_Element
- Il capitolo spiega un modello meccanico e non introduce coefficienti di verifica normativa. Le prescrizioni dei materiali e gli stati limite si consultano nella guida teorica globale, nelle sezioni dei moduli corrispondenti.

## Elementi Shell

Una shell descrive una superficie strutturale con comportamento membranale e flessionale. È utile per solette, pareti e piastre quando lo spessore è piccolo rispetto alle dimensioni nel piano e quando la variazione delle azioni nella superficie è decisiva. La superficie media sostituisce il volume, mentre lo spessore partecipa alle rigidezze di membrana e flessione in modo differente.

### Membrana e flessione

Le risultanti membranali sono forze per unità di lunghezza; le risultanti flessionali sono momenti per unità di lunghezza. Non importare un momento di piastra espresso in kNm/m come momento totale di una trave. Occorre dichiarare la striscia resistente e integrare il risultato sul tratto interessato. La somma delle azioni sulle strisce deve restare coerente con equilibrio e percorso del carico.

### Assi e connessioni

La normale locale distingue le facce superiore e inferiore. Normali invertite possono cambiare la lettura dei segni senza cambiare la fisica. Controlla la continuità della mesh, le connessioni con beam e la rappresentazione degli appoggi. Un bordo vincolato su tutte le rotazioni può irrigidire artificialmente una soletta appoggiata.

### Errori e limiti

Un carico puntuale o un vincolo puntuale possono generare picchi che crescono al raffinarsi della mesh. La convergenza della reazione globale non garantisce la convergenza della tensione puntuale. Rappresenta la superficie di contatto reale quando la verifica locale lo richiede. Una shell non sostituisce automaticamente un modello tridimensionale dei nodi massicci.

### Riepilogo

Scegli la shell quando devi descrivere distribuzioni nella superficie; mantieni espliciti assi, spessore, unità delle risultanti e trasferimento ai dettagli. Anthea non dispone di un solutore shell generale: questa pagina aiuta a interpretare dati provenienti da un modello esterno.

### Controllo delle unità e fonte

Se il momento di piastra è uniforme e pari a 12 kNm/m, una striscia larga 2,5 m porta 30 kNm. Per una distribuzione variabile occorre integrare lungo la striscia. La documentazione [SCIA sulle risultanti 1D e 2D](https://www.scia.net/en/support/faq/scia-engineer/results/calculation-1d-and-2d-results) distingue le componenti di membrana da quelle flessionali; le convenzioni specifiche vanno lette nel solutore di origine. [COMSOL, Singular Loads](https://doc.comsol.com/6.4/doc/com.comsol.help.sme/sme_ug_modeling.05.071.html) illustra perché un carico concentrato può rendere la tensione locale dipendente dalla mesh.

## Releases e connettività

La connettività stabilisce quali incognite sono condivise fra elementi; una release stabilisce quali azioni non vengono trasmesse a un'estremità. Queste due decisioni determinano il percorso dei carichi prima ancora di assegnare una rigidezza.

### Controllare un nodo

Individua sul disegno le traslazioni e rotazioni possibili, quindi verifica se il modello permette proprio quei movimenti. Una cerniera ideale nel piano trasmette due forze e non trasmette il momento coniugato alla rotazione libera. Un link elastico trasmette una forza proporzionale allo spostamento relativo: una rigidezza enorme è un'approssimazione che può peggiorare il condizionamento numerico.

### Meccanismi e collegamenti rigidi

Se una parte può muoversi rigidamente senza deformare nessun elemento, il modello ha un meccanismo. Non correggerlo con una molla casuale. Verifica prima i vincoli fisici e le release. Al contrario, troppe connessioni rigide possono impedire deformazioni reali e generare azioni spurie. Un collegamento rigido deve trasferire anche i momenti prodotti dal braccio geometrico.

### Riepilogo

Confronta il modello numerico con uno schizzo cinematico. Usa un carico unitario per verificare il movimento consentito e la distribuzione delle reazioni. Conserva una descrizione delle ipotesi del nodo accanto ai risultati.

### Riferimento del modello

La formulazione [TU Delft dei telai piani](https://interactivetextbooks.citg.tudelft.nl/computational-modelling/structural_linear/space_frame.html) distingue traslazioni e rotazione del nodo e l’assemblaggio delle rigidezze. Questa pagina riguarda l’interpretazione del modello esterno: non introduce un editor generale di release in Anthea.

## Instabilità delle aste compresse

Un'asta compressa può perdere stabilità senza che tutta la sezione raggiunga la resistenza del materiale. Una piccola deviazione laterale produce un momento aggiuntivo dovuto alla forza assiale; il momento aumenta la deviazione, che a sua volta amplifica il momento. La rigidezza flessionale contrasta questa retroazione.

### Modello di Eulero

Si considera un'asta rettilinea prismatica, elastica, caricata al baricentro, con rigidezza costante e piccoli spostamenti all'innesco dell'instabilità. La forza di compressione $N$ è positiva in questa pagina; non è la convenzione della scheda Sezione in c.a. Le estremità ideali impediscono la traslazione trasversale ma permettono la rotazione; una consente lo scorrimento assiale.

![Figura 05.1 — Asta cerniera-carrello: carichi assiali opposti e primo modo sinusoidale. La deformata è amplificata e non rappresenta un'ampiezza calcolata.](../../X.Desktop/Assets/Wiki/euler-mode.svg)

L'equilibrio linearizzato nella configurazione deformata conduce al problema agli autovalori seguente. $x$ misura la distanza lungo l'asta e $v$ lo spostamento trasversale; gli apici indicano derivate rispetto a $x$.

```math
EI v''''+N v''=0
v(0)=v(L)=v''(0)=v''(L)=0
v_n(x)=a_n\sin\left(\frac{n\pi x}{L}\right)
N_n=\frac{n^2\pi^2 EI}{L^2}
```

Il primo autovalore positivo corrisponde a $n=1$. L'ampiezza $a_n$ resta indeterminata nell'analisi lineare: un grafico del modo non è una previsione dello spostamento reale. La forma generalizzata con lunghezza efficace è:

```math
N_{cr} = \frac{\pi^2 EI}{L_0^2}
```

E è il modulo elastico, I l'inerzia nel piano della deformata e L₀ la lunghezza efficace. Con E in N/mm², I in mm⁴ e L₀ in mm, Ncr è in N. La formula descrive una biforcazione elastica di un'asta ideale; non è direttamente la resistenza di progetto di un'asta reale imperfetta. Raddoppiare L₀ riduce il carico critico a un quarto.

### Lunghezza libera di inflessione

L₀ rappresenta il vincolo efficace nel modo di instabilità considerato, non semplicemente la lunghezza disegnata. Per un'asta ideale isolata incernierata a entrambe le estremità coincide con la lunghezza fra cerniere. Per telai e vincoli elastici dipende dal comportamento del sistema. Un ritegno è efficace solo se ha rigidezza, resistenza e percorso del carico adeguati.

| Simbolo | Significato | Unità nell'esempio |
| --- | --- | --- |
| $E$ | Modulo elastico longitudinale | N/mm² |
| $I$ | Inerzia nel piano del modo | mm⁴ |
| $A$ | Area della sezione | mm² |
| $L$ | Distanza fra i vincoli | mm |
| $K$ | Fattore di lunghezza efficace | adimensionale |
| $L_0=KL$ | Lunghezza efficace | mm |
| $N_{cr}$ | Carico di biforcazione | N |
| $i=\sqrt{I/A}$ | Raggio d'inerzia | mm |
| $\lambda=L_0/i$ | Snellezza geometrica | adimensionale |

Per aste isolate con vincoli ideali, $K=1$ per cerniera-carrello, $K=2$ per incastro-estremo libero e $K=0{,}5$ per due incastri senza traslazione relativa. Questi valori non si trasferiscono automaticamente a una colonna di telaio: la rotazione dei nodi e la traslazione di piano modificano il modo.

### Snellezza e tensione critica

```math
\sigma_{cr}=\frac{N_{cr}}{A}=\frac{\pi^2E}{\lambda^2}
```

![Figura 05.2 — Tensione critica ideale per E = 210000 MPa. La linea orizzontale indica fy = 355 MPa; il ramo sopra tale valore non è una resistenza elastica utilizzabile.](../../X.Desktop/Assets/Wiki/euler-curve.svg)

La tensione critica cala con il quadrato della snellezza. Il confronto con lo snervamento è un controllo di campo, non una curva normativa di resistenza: tensioni residue e imperfezioni riducono la capacità prima della previsione ideale.

### Esempio numerico verificabile

Si assegnano $E=210000\,\mathrm{N/mm^2}$, $I=8\cdot10^6\,\mathrm{mm^4}$, $A=4000\,\mathrm{mm^2}$, $L=4000\,\mathrm{mm}$ e $K=1$. L'inerzia è un dato didattico riferito al piano studiato, non una sezione commerciale selezionata.

```math
N_{cr}=\frac{\pi^2\cdot210000\cdot8\cdot10^6}{4000^2}
N_{cr}=1036308\,\mathrm{N}=1036{,}31\,\mathrm{kN}
i=44{,}72\,\mathrm{mm},\quad\lambda=89{,}44
\sigma_{cr}=259{,}08\,\mathrm{MPa}
```

Con $f_y=355\,\mathrm{MPa}$ la tensione critica resta sotto lo snervamento, condizione necessaria ma non sufficiente per applicare il modello ideale a un'asta reale. Se il medesimo elemento è una mensola con $K=2$, il carico critico scende a $259{,}08\,\mathrm{kN}$. Confondere i vincoli produce qui un errore di fattore quattro.

### Dalla teoria alla verifica

Disegna i ritegni nei due piani, scegli le rispettive inerzie e determina il modo governante. Solo dopo valuta la procedura normativa applicabile: imperfezioni, analisi del secondo ordine, classificazione della sezione e instabilità locale possono richiedere controlli distinti. L'instabilità flesso-torsionale, spesso indicata LTB, riguarda accoppiamento di spostamento laterale e torsione di un elemento inflesso: non si verifica sostituendo semplicemente un'inerzia nella formula di Euler.

La derivazione è un modello teorico, verificato sulle lezioni MIT *Buckling of Beams*, corso 16.001, 2021. Le verifiche dell'acciaio ricadono nel quadro NTC ed Eurocodice 3 adottato dal progetto; qui non sono assegnati coefficienti parziali o curve di buckling normative. Bibliografia per approfondire: Timoshenko e Gere, *Theory of Elastic Stability*, stabilità delle aste.

### Errori comuni

> ATTENZIONE
> Il carico critico elastico non include automaticamente imperfezioni, tensioni residue, plasticità e instabilità locale. La verifica normativa richiede il metodo applicabile al materiale e al sistema. La presente versione di Anthea non contiene un modulo autonomo Instabilità dell'acciaio.

### Riepilogo

Controlla il piano debole, i ritegni reali e il modo di instabilità. L'area da sola non descrive la vulnerabilità: due aste con la stessa area possono avere inerzie e capacità di stabilità molto differenti. Cerca anche i termini buckling, snellezza ed Euler nella Wiki per trovare i riferimenti presenti nelle guide globali.

## Fondamenti del percorso dei carichi

Il primo controllo di un modello strutturale è il percorso del carico dalla sua applicazione fino ai vincoli. Una soletta trasferisce il carico alle travi; le travi a pilastri o pareti; questi alle fondazioni e al terreno. Ogni passaggio richiede equilibrio e compatibilità delle deformazioni.

### Equilibrio e rigidezza

Le reazioni equilibrano risultanti e momenti dei carichi esterni. Nei sistemi isostatici l'equilibrio determina le reazioni; nei sistemi iperstatici intervengono anche rigidezze e compatibilità. Irrigidire un elemento può aumentare la quota di carico che gli compete, anche se il carico totale non cambia. Una verifica locale non dimostra quindi da sola la correttezza della ripartizione globale.

### Modello e verifica

Il modello calcola azioni e spostamenti nel quadro delle proprie ipotesi. La verifica confronta domande e capacità per specifici meccanismi e stati limite. Dichiarare materiale, geometria, vincoli, carichi, unità e convenzioni permette di controllare il trasferimento fra i due. Mantieni separati risultati elastici, predimensionamento e verifica di resistenza.

### Riepilogo

Prima di leggere un coefficiente di utilizzo, controlla dove passa il carico e quale meccanismo rappresenta il calcolo. Confronta sempre un equilibrio globale e un ordine di grandezza indipendente con i risultati del software.

## Azioni e combinazioni del modello

Una combinazione raccoglie azioni che possono agire insieme nel contesto dello stato limite considerato. Il valore di un carico assegnato non dice da solo se sia caratteristico, rappresentativo o di progetto. Prima di inserire N, M e V nel modulo, annota la provenienza della combinazione e i fattori già applicati.

### Percorso dal carico alla domanda

Il peso proprio dipende dalla geometria e dal peso per unità di volume. Un carico di superficie va trasformato in carico lineare dichiarando la larghezza tributaria. La trasformazione di unità non applica coefficienti di sicurezza: passare da kN/m a N/mm conserva lo stesso valore numerico, mentre passare da kNm a Nmm moltiplica per un milione.

La terna N-Mx-My deve provenire dalla stessa sezione e dalla stessa combinazione. Un inviluppo può essere utile per individuare sezioni critiche, ma gli estremi di componenti diverse non sono necessariamente simultanei. Conserva l'identificativo della combinazione originale per poter ricostruire il risultato.

### Stati limite distinti

Resistenza, deformazione, fessurazione e fatica rispondono a domande differenti. Una combinazione di progetto per resistenza non è automaticamente una combinazione di esercizio. Le famiglie di azione disponibili nel modulo vanno compilate solo quando sono state definite nel modello: non duplicare una terna in ogni famiglia per riempire le tabelle.

### Errori comuni e riferimenti

> ERRORI COMUNI
> Moltiplicare nuovamente un'azione già fattorizzata; includere due volte il peso proprio; confondere carico superficiale e lineare; usare un inviluppo di massimi indipendenti come se fosse una combinazione reale.

Per la definizione delle combinazioni usa il quadro normativo applicabile, i parametri nazionali e la destinazione d'uso. Questo capitolo non assegna coefficienti normativi. Il riferimento italiano di base è il DM 17 gennaio 2018; le opzioni e i limiti della singola verifica sono descritti nei capitoli dei moduli della guida globale.

## Dinamica e sisma del modello

La risposta dinamica dipende da come massa e rigidezza sono distribuite e da come l'azione varia nel tempo. Un modello corretto per un carico statico può essere incompleto per un'analisi dinamica se mancano masse, collegamenti o gradi di libertà rilevanti.

### Massa rigidezza e modi

Una forma modale descrive un movimento possibile del sistema linearizzato; la frequenza associata dipende da rigidezza e massa. Nel singolo oscillatore elastico non smorzato, T = 2π√(m/k), con unità coerenti. Raddoppiare la massa aumenta il periodo di un fattore √2, mentre raddoppiare la rigidezza lo riduce dello stesso fattore. In un sistema con molti gradi di libertà bisogna considerare i modi che partecipano alla direzione del carico.

La deformata modale è scalata convenzionalmente: la sua ampiezza grafica non è uno spostamento sismico di progetto. Controlla le masse effettivamente assegnate, le direzioni dei modi e l'effetto dei vincoli. Una rotazione locale inattesa può rivelare una release o una connessione non coerente.

### Azione e risposta

Uno spettro collega la risposta di oscillatori alla loro frequenza e allo smorzamento assunto. Un accelerogramma descrive invece una storia temporale: passo, unità, durata e trattamento del segnale influenzano l'analisi. Lo smorzamento non va aggiunto soltanto per ridurre un picco indesiderato; deve rappresentare un'ipotesi motivata del modello.

### Limiti e applicazione

Un'analisi modale elastica non dimostra capacità dissipativa, duttilità dei dettagli o stabilità durante grandi spostamenti. Anthea non espone un solutore dinamico generale; eventuali procedure sismiche o Newmark dei muri hanno campo e input specifici, documentati nei relativi capitoli. Per spettro, combinazioni e verifiche usa la normativa applicabile, senza trasferire coefficienti da un meccanismo all'altro.

### Controllo dell’oscillatore

Per m = 1000 kg e k = 40000 N/m si ottengono frequenza angolare 6,3246 rad/s e periodo 0,99346 s. Con massa raddoppiata il periodo diventa 1,40496 s. Questo è un oscillatore elastico non smorzato, non lo spettro di progetto di un sito.

```math
\omega_n=\sqrt{\frac{k}{m}}
T=\frac{2\pi}{\omega_n}
```

La derivazione è nelle [lezioni MIT di dinamica strutturale, Unit 20](https://ocw.mit.edu/courses/16-20-structural-mechanics-fall-2002/609687cf29516e13e864ff310af328a7_unit20.pdf). I coefficienti normativi e lo smorzamento vanno definiti separatamente.

## Fasi costruttive e percorso dei carichi

La struttura durante il montaggio può lavorare in modo diverso dalla struttura completata. Un puntello, un getto successivo o la collaborazione di una soletta modificano i vincoli e le rigidezze disponibili quando il carico viene applicato. Per questo l'ordine delle fasi fa parte del modello, non soltanto del programma dei lavori.

### Carichi e sezioni attive

In una trave composta, il peso del calcestruzzo fresco può essere portato dall'acciaio prima che la soletta collabori. I carichi successivi agiscono su una sezione diversa. Sommare tutti i carichi e applicarli direttamente alla sezione finale può sottostimare tensioni accumulate nella parte inizialmente resistente.

Dichiara per ciascuna fase elementi attivi, vincoli, materiali, carichi introdotti e carichi rimossi. Il disarmo e la rimozione di un appoggio provvisorio producono ridistribuzioni. La deformazione già maturata non è necessariamente cancellata dalla modifica del sistema.

### Fenomeni differiti

Ritiro e viscosità dipendono dal tempo e dalla storia di carico; nella sezione composta possono produrre tensioni interne anche senza aumentare la risultante esterna. Un coefficiente di omogeneizzazione unico non descrive automaticamente tutte le fasi. Controlla età, durata e metodo previsto dal modulo.

### Controlli e riepilogo

> REGOLA PRATICA
> Scrivi una tabella delle fasi prima di compilare il modello. Per ogni carico identifica la sezione che lo porta al momento dell'applicazione e il percorso fino agli appoggi disponibili.

Il modulo Sezione composta da ponte di Anthea documenta l'analisi per fasi nella guida globale. I dettagli costruttivi, i vincoli provvisori e le verifiche del montaggio richiedono comunque i dati del progetto reale. Una verifica favorevole della configurazione finale non implica che ogni fase intermedia sia sicura.

### Riferimento e campo

La trattazione [SCI sulla costruzione composta](https://steelconstruction.info/topics/design/composite-construction/) distingue la fase non puntellata, in cui l’acciaio sostiene il calcestruzzo fresco, dalla fase composta. È un riferimento sul modello; non sostituisce le norme applicabili al ponte né verifica puntelli, stabilità laterale o procedure di montaggio del progetto concreto.

## Cos'è un ponte?

Un ponte mantiene la continuità di un percorso sopra un ostacolo lasciando uno spazio libero sottostante. La scelta strutturale parte da questo spazio: posizione degli appoggi, franco richiesto, terreno e accesso al cantiere definiscono le possibilità prima del dimensionamento delle travi.

### Leggere l'opera prima del modello

![Figura 07.1 — Taccuino di un viadotto a travata: campate, pila centrale e spalle. Il disegno identifica i componenti, senza attribuire loro dimensioni di progetto.](../../X.Desktop/Assets/Wiki/bridge-notebook.svg)

L'impalcato sostiene il piano viabile. Soletta, travi longitudinali, traversi e diaframmi ne distribuiscono le azioni; pile e spalle le trasferiscono alle fondazioni. Gli appoggi costituiscono l'interfaccia meccanica fra impalcato e sottostruttura. I giunti consentono movimenti relativi del piano viabile: non sono appoggi e non si modellano come sostegni verticali.

La luce si misura fra i riferimenti strutturali degli appoggi della campata; la lunghezza totale dell'opera comprende più campate e dipende dalla convenzione geometrica adottata. Nel disegno di calcolo annota entrambi i riferimenti: una quota architettonica fra facce non coincide necessariamente con la luce fra assi.

### Seguire il carico

Una ruota produce una domanda locale nella soletta; la soletta la ripartisce alle travi secondo rigidezze e geometria. Gli appoggi raccolgono reazioni concentrate, le pile portano azioni assiali e momenti, le fondazioni mobilitano il terreno. Il modello a trave equivalente condensa questo percorso, ma perde la distribuzione trasversale e gli effetti locali.

![Figura 07.2 — Percorso verticale in una campata ideale: carichi discendenti sull'impalcato, reazioni ascendenti agli appoggi e trasferimento attraverso pila e fondazione.](../../X.Desktop/Assets/Wiki/bridge-load-path.svg)

Per frenatura, temperatura e sisma il percorso cambia. Un appoggio mobile longitudinalmente non riceve nel modello ideale la stessa forza di un appoggio fisso; l'attrito e la rigidezza reali possono rendere meno netta questa distinzione. Occorre una pianta dei vincoli che mostri anche le direzioni libere. Un ritegno orizzontale senza fondazione capace di riceverne la reazione non completa il percorso resistente.

### Forma strutturale e conseguenze

| Sistema | Meccanismo prevalente | Decisione da esplicitare |
| --- | --- | --- |
| Travata | Flessione e taglio | Continuità, controventi e rigidezza trasversale |
| Arco | Compressione con possibili momenti | Destinazione della spinta orizzontale |
| Reticolare | Azioni assiali nelle aste del modello ideale | Eccentricità e comportamento dei nodi reali |
| Strallato | Stralli tesi e impalcato compresso/inflesso | Tesatura e sequenza di attivazione |
| Sospeso | Funi principali tese, pendini e trave irrigidente | Ancoraggi e risposta ai carichi non simmetrici |

Questa classificazione descrive il meccanismo, non prescrive intervalli universali di luce. Un cassone è una forma di sezione, non un sistema alternativo alla travata: può far parte di una travata, di un ponte strallato o di un ponte ad arco.

### Il ponte durante la costruzione

Prima del getto della soletta collaborante una trave metallica ha rigidezza, peso portato e ritegni diversi da quelli finali. Nei conci a sbalzo l'avanzamento modifica lo schema a ogni fase; la chiusura introduce continuità. Un'analisi della sola configurazione finale non ricostruisce la storia tensionale. Individua sezioni attive, appoggi provvisori e carichi di montaggio nella stessa sequenza temporale.

### Dettagli che decidono la vita utile

L'acqua raccolta da un giunto può raggiungere testate, appoggi e pulvini. Il progetto deve permettere drenaggio, ispezione e sostituzione degli appoggi con un percorso temporaneo dei carichi. Non si deduce la durabilità dal solo copriferro: contano esposizione, accessibilità, protezioni e manutenzione programmata.

Durante un'ispezione un quadro fessurativo o una corrosione osservata costituiscono evidenze; la capacità residua richiede rilievi, modello e verifiche. La stessa lesione può avere interpretazioni diverse in una fase costruttiva e nell'esercizio definitivo.

### Dall'anatomia al progetto in Anthea

Prima del predimensionamento prepara uno schema degli appoggi, una sezione trasversale e una sequenza delle fasi. Nel modulo Bridge Design verifica poi le ipotesi della famiglia scelta: il confronto di alternative non sostituisce le verifiche locali, geotecniche o sismiche. Prosegui con [il percorso dei carichi](wiki:load-path), [gli elementi beam](wiki:beam) e con il capitolo sulle fasi costruttive.

Riferimento di terminologia e componenti: FHWA, *Bridge Inspector's Reference Manual*. Il Book di TheBridgeEng è stato usato come riferimento editoriale; testi e figure qui sono originali.

## Fessurazione del calcestruzzo armato

Il controllo di apertura delle fessure è una verifica di esercizio. Una sezione può soddisfare la resistenza ultima e avere fessure incompatibili con il requisito assegnato. Il calcolo richiede tensioni di esercizio, geometria dell'armatura e zona efficace di calcestruzzo teso; non basta conoscere il momento resistente.

### Meccanismo e campo

Alla fessura il contributo teso del calcestruzzo è interrotto; l'aderenza trasferisce forza fra acciaio e calcestruzzo nei tratti adiacenti. Perciò la deformazione media dell'acciaio fra le fessure non coincide necessariamente con la deformazione nella sezione fessurata. Il tension stiffening rappresenta questa collaborazione media.

![Figura 04.1 — Tirante fessurato: due fessure attraversano la zona tesa e l'armatura continua trasferisce forza per aderenza. La distanza tra fessure e l'apertura sono grandezze diverse.](../../X.Desktop/Assets/Wiki/crack-transfer.svg)

Le relazioni qui riportate riguardano la formulazione classica di EN 1992-1-1 illustrata da Walraven nel materiale JRC del 2008, per armatura ordinaria aderente, senza precompressione e con barre ravvicinate. È una relazione normativa empirico-meccanica, non una legge universale né una dichiarazione sull'edizione applicabile oggi al singolo progetto.

### Relazioni e simboli

```math
w_k=s_{r,max}(\varepsilon_{sm}-\varepsilon_{cm})
\rho_{eff}=\frac{A_s}{A_{c,eff}},\quad\alpha_e=\frac{E_s}{E_{cm}}
\Delta\varepsilon=\max\left(\frac{\sigma_s-k_t\frac{f_{ct,eff}}{\rho_{eff}}(1+\alpha_e\rho_{eff})}{E_s},\frac{0{,}6\sigma_s}{E_s}\right)
s_{r,max}=3{,}4c+0{,}425k_1k_2\frac{\phi}{\rho_{eff}}
```

| Simbolo | Definizione | Unità |
| --- | --- | --- |
| $w_k$ | Apertura caratteristica calcolata | mm |
| $s_{r,max}$ | Distanza massima convenzionale fra fessure | mm |
| $\Delta\varepsilon$ | Differenza delle deformazioni medie | adimensionale |
| $\sigma_s$ | Tensione di trazione nell'acciaio, sezione fessurata | MPa |
| $E_s,E_{cm}$ | Moduli elastici di acciaio e calcestruzzo | MPa |
| $f_{ct,eff}$ | Resistenza media a trazione al tempo di fessurazione | MPa |
| $A_s,A_{c,eff}$ | Area di armatura e area efficace tesa | mm² |
| $c,\phi$ | Copriferro dell'armatura considerata e diametro | mm |
| $k_t,k_1,k_2$ | Coefficienti di durata, aderenza e distribuzione deformativa | adimensionali |

Il copriferro della formula va riferito alle barre che controllano la fessura: non sostituire senza controllo il copriferro netto alla staffa impostato nella UI. L'area efficace non è l'intera sezione né tutta la zona geometricamente tesa. Per flessione semplice della sezione rettangolare, la definizione classica limita l'altezza efficace con il minimo dei tre contributi seguenti, dove $d$ è l'altezza utile e $x$ la profondità dell'asse neutro dal bordo compresso:

```math
h_{c,eff}=\min\left(2{,}5(h-d),\frac{h-x}{3},\frac{h}{2}\right)
```

### Procedura per un caso ordinario

Identifica combinazione SLE, esposizione e requisito di apertura dal quadro normativo del progetto. Risolvi la sezione fessurata e determina l'armatura efficace. Controlla che l'interasse delle barre non superi $5(c+\phi/2)$ prima di usare la relazione di distanza qui mostrata. Per flessione semplice usa $k_2=0{,}5$, per trazione uniforme $k_2=1$; casi intermedi richiedono la distribuzione delle deformazioni. Per barre ad aderenza migliorata $k_1=0{,}8$. Il coefficiente $k_t$ è 0,6 a breve e 0,4 a lunga durata nella formulazione citata.

### Esempio numerico

Dati assegnati: $\sigma_s=200\,\mathrm{MPa}$, $E_s=200000\,\mathrm{MPa}$, $E_{cm}=33000\,\mathrm{MPa}$, $f_{ct,eff}=2{,}9\,\mathrm{MPa}$, $\rho_{eff}=0{,}02$, $\phi=16\,\mathrm{mm}$, $c=30\,\mathrm{mm}$. Barre ad aderenza migliorata, flessione semplice, lunga durata e interasse 150 mm. L'area efficace è qui un dato assunto: l'esempio verifica le relazioni di apertura, non una sezione completa.

```math
\alpha_e=6{,}0606
\Delta\sigma=0{,}4\frac{2{,}9}{0{,}02}(1+6{,}0606\cdot0{,}02)=65{,}0303\,\mathrm{MPa}
\Delta\varepsilon=\max(0{,}00067485;0{,}00060000)=0{,}00067485
s_{r,max}=3{,}4\cdot30+0{,}425\cdot0{,}8\cdot0{,}5\frac{16}{0{,}02}=238\,\mathrm{mm}
w_k=238\cdot0{,}00067485=0{,}1606\,\mathrm{mm}
```

La soglia d'interasse è 190 mm: il ramo adottato è coerente con i 150 mm assegnati. Per un requisito ipotetico di 0,30 mm il valore calcolato sarebbe inferiore al limite; quel limite è un dato d'esempio, non una prescrizione valida per ogni ambiente. Il controllo non copre fessure da ritiro impedito, gradienti termici o dettagli non rappresentati.

### Limiti e confronto con il software

Armature rade, trazione integrale, sezioni non rettangolari, tendini e fasi iniziali richiedono le rispettive regole. Non estendere la formula ravvicinata a tutti questi casi. Nel motore Anthea esistono scelte implementative specifiche per regioni efficaci e ramo distante: la guida teorica della sezione le documenta separatamente, senza attribuirle automaticamente al testo normativo.

Apri [la guida del modulo](wiki:guida-sezione-ca) per le convenzioni degli input. Conserva nel report combinazione, parametri effettivi e ramo usato; un singolo numero di apertura non rende il risultato riproducibile.

## Capacità portante delle fondazioni superficiali

La capacità portante descrive il collasso del terreno sotto la fondazione. Non coincide con una pressione ammissibile universale: la stessa fondazione può raggiungere cedimenti incompatibili molto prima del collasso. Questa pagina sviluppa un riferimento statico per carico verticale centrato, terreno omogeneo e piano di posa orizzontale; non implementa un nuovo solutore Anthea.

### Geometria, drenaggio e azioni

![Figura 06.1 — Fondazione nastriforme: larghezza B, profondità D, carico verticale e pressione di contatto. La regione tratteggiata indica il terreno coinvolto, non una superficie critica calcolata.](../../X.Desktop/Assets/Wiki/footing.svg)

La risultante verticale deve comprendere i pesi pertinenti alla convenzione scelta. Distingui la pressione totale al contatto dalla pressione netta rispetto alla tensione preesistente alla quota di posa. Dichiarare una capacità netta e confrontarla con un'azione lorda altera il confronto. Prima dei coefficienti scegli il tipo di analisi: tensioni efficaci per condizioni drenate, tensioni totali e resistenza non drenata per il caso idealizzato non drenato.

### Struttura della relazione drenata

Per fondazione nastriforme ideale, terreno orizzontale e azione verticale centrata, la struttura classica della relazione è:

```math
q_{ult}=c'N_c+q'N_q+\frac{1}{2}\gamma'BN_\gamma
```

I tre termini rappresentano il contributo di coesione, sovraccarico efficace alla base e peso del terreno nel meccanismo. Le estensioni per forma, profondità, inclinazione del carico, base e pendio dipendono dal metodo scelto. In particolare $N_\gamma$ e i correttivi non vanno prelevati da autori diversi per costruire una formula ibrida. Qui non si propone una formulazione completa di Brinch Hansen.

| Simbolo | Definizione | Unità |
| --- | --- | --- |
| $q_{ult}$ | Pressione ultima lorda | kPa |
| $c'$ | Intercetta di resistenza in tensioni efficaci | kPa |
| $q'$ | Tensione verticale efficace alla quota di posa | kPa |
| $\gamma'$ | Peso efficace pertinente al terreno sotto base | kN/m³ |
| $B$ | Larghezza della fondazione nastriforme | m |
| $N_c,N_q,N_\gamma$ | Fattori del metodo dichiarato | adimensionali |
| $c_u$ | Resistenza non drenata | kPa |
| $D$ | Profondità del piano di posa | m |

Il simbolo $\gamma'$ non significa che tutta la zona sia necessariamente immersa: sopra falda occorre il peso appropriato, sotto falda il peso immerso. Una falda che attraversa il meccanismo richiede un trattamento coerente, non una media scelta senza giustificazione. Anche il sovraccarico alla base va costruito con tensioni efficaci nel caso drenato.

### Caso non drenato completamente specificato

Per una striscia su terreno omogeneo idealmente puramente coesivo, carico verticale centrato e correzioni geometriche non applicate, il riferimento limite è:

```math
\phi_u=0,\quad N_q=1,\quad N_\gamma=0,\quad N_c=2+\pi
q_{ult,net}=(2+\pi)c_u
q_{ult,gross}=(2+\pi)c_u+q
```

Qui $q$ è il sovraccarico totale alla quota di posa, non $q'$ del modello drenato. Non sottrarre la pressione interstiziale una seconda volta da una resistenza espressa in tensioni totali. Il valore $2+\pi$ è riferito a questo modello limite e non si identifica con tutti i fattori chiamati $N_c$ in letteratura.

### Esempio per metro di fondazione

Assegna $c_u=50\,\mathrm{kPa}$, $B=2\,\mathrm{m}$, $D=1\,\mathrm{m}$ e peso totale del terreno sopra base $\gamma=18\,\mathrm{kN/m^3}$. Si modella l'interramento soltanto mediante sovraccarico uniforme, senza incremento di resistenza da profondità. La risultante applicata lorda è $V=400\,\mathrm{kN/m}$.

```math
q=\gamma D=18\,\mathrm{kPa}
q_{ult,net}=(2+\pi)50=257{,}08\,\mathrm{kPa}
q_{ult,gross}=257{,}08+18=275{,}08\,\mathrm{kPa}
q_{appl}=\frac{400}{2}=200\,\mathrm{kPa}
R_{ult}=q_{ult,gross}B=550{,}16\,\mathrm{kN/m}
```

La pressione applicata è inferiore alla pressione ultima ideale, ma questo confronto non costituisce una verifica SLU di progetto: non sono stati applicati coefficienti parziali, né esaminati eccentricità, scorrimento, stabilità globale e cedimenti. I valori permettono invece di controllare unità e distinzione lordo/netto in un modello elementare.

### Quando cambiare modello

Una lente debole sotto uno strato resistente può spostare il meccanismo e rendere inadeguata l'ipotesi omogenea. Un carico eccentrico modifica area efficace e contatto; il terreno non offre una trazione illimitata alla base. Un pendio vicino rompe la simmetria; il sisma aggiunge azioni inerziali e può modificare la resistenza. In questi casi serve una formulazione esplicitamente documentata per il problema.

Il riferimento tecnico consultato è FHWA GEC 6, *Shallow Foundations*, per fattori, convenzioni e campo. L'eventuale verifica italiana richiede NTC, Circolare e quadro Eurocodice 7 adottati nel progetto: FHWA non è una prescrizione italiana. Il modulo Muri di sostegno tratta la fondazione del muro con le proprie ipotesi; non è presentato come modulo universale per plinti.

## BIM e controllo dello scambio informativo

Un oggetto geometrico diventa utile nello scambio quando conserva identità, significato e proprietà verificabili. Il modello architettonico di una trave non definisce da solo asse analitico, rigidezza, svincoli e fase strutturale. Il passaggio al calcolo è una trasformazione controllata, non un cambio di formato.

### Definire prima il requisito

Per uno scambio di travi inizia dal risultato necessario: elenco degli elementi, materiale, sezione, posizione e identificativo persistente. Specifica unità, riferimento geografico, convenzione di orientamento e trattamento degli oggetti esclusi. Una proprietà assente deve restare distinguibile da un valore nullo. Lo zero assegnato automaticamente a una rigidezza mancante può produrre un meccanismo; un default non dichiarato può nascondere l'assenza dell'informazione.

### IFC e modello analitico

IFC descrive entità e relazioni in uno schema aperto buildingSMART. La classe di un elemento, le sue proprietà e la sua collocazione hanno ruoli differenti. La presenza di una geometria visualizzabile non dimostra che il ricevente abbia importato associazioni, unità o proprietà. Conserva il collegamento fra identificativo originale ed elemento analitico generato, compresi gli eventuali molti-a-uno.

| Controllo | Evidenza richiesta | Errore intercettato |
| --- | --- | --- |
| Unità | Lunghezza nota confrontata prima e dopo | Scala metri/millimetri |
| Posizionamento | Coordinate di almeno due punti e orientamento | Traslazione o rotazione inattesa |
| Identità | Mappa degli identificativi | Elementi duplicati o persi |
| Materiale | Proprietà e fonte | Nome senza parametri utilizzabili |
| Connettività | Nodi e relazioni analitiche | Solidi che si toccano ma aste scollegate |

### CDE e revisione

Un ambiente di condivisione dei dati, CDE, gestisce stati e responsabilità della documentazione. Il nome del file non basta a distinguere un modello di lavoro da una revisione autorizzata per lo scambio. Registra autore, revisione, scopo e data del pacchetto ricevuto; conserva un rapporto degli elementi modificati anziché sovrascrivere silenziosamente gli input di calcolo.

### Procedura di accettazione

Prova prima un campione con una trave ruotata, un materiale personalizzato e un elemento escluso. Confronta quantità e coordinate con il sorgente, poi verifica la mappa dei dati. Una clash detection geometrica non certifica equilibrio o validità meccanica. Lo schema informativo va controllato separatamente dal modello strutturale, discusso in [Elementi beam](wiki:beam).

Anthea conserva i suoi dati di progetto e modulo; questa pagina non dichiara disponibile un importatore IFC. Il riferimento per lo schema IFC è la documentazione ufficiale buildingSMART IFC 4.3. I requisiti contrattuali di scambio e il livello di fabbisogno informativo vanno concordati per l'attività concreta.

## Progettazione parametrica e controllo delle dipendenze

Un modello parametrico conserva la relazione fra dati e risultati. Se una luce cambia, devono aggiornarsi geometria, carichi dipendenti e quantità; una variabile non collegata resta invece un input separato. L'automazione è affidabile solo quando queste dipendenze sono esplicite e verificabili.

### Separare quattro passaggi

La generazione definisce geometria e identificativi. L'analisi determina la risposta per le ipotesi e le azioni assegnate. La verifica confronta domanda e capacità secondo il metodo pertinente. L'ottimizzazione ordina le alternative ammissibili rispetto a obiettivi dichiarati. Un valore basso di costo non dimostra che una soluzione sia ammissibile; una verifica favorevole non prova l'ottimalità.

### Un esempio di dipendenza

In una trave semplicemente appoggiata con carico uniforme assegnato $q$, il momento massimo cresce col quadrato della luce. Se invece $q$ include un peso proprio che cambia con la sezione, modificare la sezione richiede ricalcolare anche il carico. Una cache indicizzata solo dalla luce conserva allora risultati obsoleti.

```math
M_{max}=\frac{qL^2}{8}
\frac{M_{max}(1{,}1L)}{M_{max}(L)}=1{,}21
```

Il secondo rapporto vale soltanto a $q$ costante. Per $q=25\,\mathrm{kN/m}$ e $L=8\,\mathrm{m}$ il momento è 200 kNm; a 8,8 m diventa 242 kNm. Il 10% in più di luce introduce il 21% in più di domanda flessionale in questo modello. Il confronto discende dall'equilibrio della trave, non da una regola generale di dimensionamento.

### Dati strutturati e identità

In un grafo parametrico o in uno script, ogni record deve associare identificativo, unità e provenienza. Non accoppiare carichi e sezioni soltanto per posizione in una lista se un filtro può cambiarne l'ordine. Nei Data Trees di Grasshopper anche il percorso del ramo è informazione: appiattire i rami può accoppiare campate e combinazioni estranee. Controlla le cardinalità prima e dopo ogni join o filtro.

### Riproducibilità e limiti

Salva versione degli input, versione del metodo, criteri di arresto e motivi di esclusione. Confronta un caso semplice con soluzione indipendente e ripeti il processo cambiando un parametro per volta. Le sensibilità dipendono dal punto di partenza; una graduatoria fra candidati campionati non è la prova di un minimo globale.

In Anthea il motore numerico resta nei progetti di calcolo e nelle librerie previste. La Wiki espone esempi didattici e collega i moduli, senza diventare una seconda implementazione del solutore. Per formulazione e benchmark della trave, prosegui con [Elementi beam](wiki:beam); per la struttura dei dati in Grasshopper, consulta la guida ufficiale McNeel sui Data Trees.

## Acciaio per armature: proprietà e diagrammi

Il materiale definisce la risposta delle barre; non verifica da solo una sezione. La scheda propone B450C, B450A, classi storiche FeB e un materiale personalizzato. I valori storici di catalogo non sostituiscono le prove su una struttura esistente.

### Grandezze e unità

| Simbolo | Significato | Unità |
| --- | --- | --- |
| Es | Modulo elastico | MPa |
| fyk, fu | Snervamento caratteristico e resistenza ultima | MPa |
| γs | Coefficiente parziale assegnato | adimensionale |
| εyd, εu | Deformazione di snervamento di progetto e ultima | adimensionale nelle formule |

```math
f_{yd}=\frac{f_{yk}}{\gamma_s}
\varepsilon_{yd}=\frac{f_{yd}}{E_s}
```

Con fyk = 450 MPa, γs = 1,15 ed Es = 200000 MPa, fyd = 391,304 MPa ed εyd = 0,00195652, cioè 1,95652 per mille. Nei campi che chiedono per mille si inserisce quest'ultimo valore; una deformazione adimensionale non si incolla senza conversione.

### Diagramma e classe storica

Il grafico della scheda materiali rappresenta il legame caratteristico. Il diagramma utilizzato dal calcolo dipende anche dal coefficiente parziale e dalle opzioni della verifica. Per un materiale personalizzato occorrono dati coerenti di modulo, snervamento, resistenza ultima, deformazione ultima e incrudimento.

L'allungamento storico A5 non viene convertito automaticamente nella deformazione ultima εu. Se εu manca, una funzione che la richiede non è completa. La scelta di una classe FeB non dimostra la corrispondenza del materiale esistente ai valori nominali del catalogo.

### Trasferimento fra moduli

Le proprietà compatibili possono essere condivise nel progetto; geometria, barre e carichi restano dati separati. Il tubo CHS del micropalo usa una definizione specifica. Il momento automatico del palo orizzontale conserva un modello elastico perfettamente plastico: l'incrudimento del catalogo non diventa automaticamente resistenza del meccanismo di Broms.

Consulta [Materiali e durabilità](wiki:guida-materiali-e-durabilita) per la compilazione e [Capacità orizzontale con Broms](wiki:capacita-orizzontale-con-broms) per il ruolo del momento resistente.

## Profili di calcolo del calcestruzzo

Questa matrice descrive le implementazioni presenti nel software, non certifica la conformità dell’intero progetto o la vigenza di ogni edizione nazionale. Il selettore propone NTC 2018, Model Code 2010,
EN 1992-1-1 e le varianti UNI, DIN, DS e NS. CNR-DT 204 e TR34 restano leggibili
negli archivi storici, ma non sono proposti per nuovi calcoli: FRC e pavimentazioni
non fanno parte del modello di calcestruzzo ordinario qui descritto.

### Implementazioni

Le classi GPC.Model.Standards restano la sorgente dei coefficienti. Domini,
tensioni e piani di deformazione usano GPC Checker; geometrie e baricentri
usano GPC Geometry. Le nuove formule sono in ANTHEA.Calculations, indipendente
 da WPF ed esportabile con il progetto già predisposto.

| Profilo | Taglio senza / con staffe | Apertura delle fessure |
| --- | --- | --- |
| NTC 2018 | §§4.1.2.3.5.1–2; formulazione esistente | NTC e Circolare 2019; frequente e quasi permanente |
| EN 1992-1-1 | Prima generazione, §§6.2.2–3 | §7.3.4; quasi permanente |
| UNI EN 1992-1-1 | DM 31/07/2012: ν nazionale; αcw=1 per c.a. non precompresso | Formule EC2 e criteri ambientali italiani |
| DIN EN 1992-1-1 | CRd,c, vmin, ν1, z e intervallo di cotθ nazionali | Altezza efficace e distanza tra fessure DIN; kt=0,4 |
| DS EN 1992-1-1 | DK NA:2024; vmin e ν1 nazionali, cotθ≤2 cautelativo, duttilità almeno B | k3 e area efficace DK; sistemi fine e grossolano in trazione |
| NS EN 1992-1-1 | NA:2010: granulometria, trazione assiale, limite C60 a taglio | XD3/XS3 in frequente; kc=1 senza incremento favorevole |
| Model Code 2010 | Livello II: εx da N–M–V, Asl e granulometria | Lunghezza di trasferimento; durata breve/lunga; wlim assegnato |

DIN usa i parametri di prima generazione riscontrati nella documentazione NA:2011
e nel benchmark SOFiSTiK; il materiale GPC dichiara NA:2013-04. Questa distinzione
è intenzionale: non è un'attestazione di copertura di ogni aggiornamento
 dell'annesso. NS è NA:2010. Non sono implementati EC2:2023, Model Code 2020
 o il progetto di aggiornamento delle appendici italiane.

Predefiniti corretti nell'adattatore: UNI αcc=0,85; DIN αct=0,85;
NS αct=0,85 e εud/εuk=0,4. DS usa γc=1,45 e γs=1,20 della DLL corrente.
I coefficienti salvati restano assegnazioni dell'utente: consultare il confronto
con i predefiniti e usare il ripristino per aggiornare un vecchio foglio.

### Dati condivisi e aggiornamento

- Le combinazioni di taglio conservano nome, N, Mx, My, Vx, Vy, T anche in
  archivio, Excel e report. I vecchi file restano leggibili; momenti assenti
  vengono inizializzati a zero nell'interfaccia.
- N negativo a compressione. Mx è associato a Vy, My a Vx nel livello II MC.
  Le azioni sono già combinate/coefficientate: non vengono moltiplicate ancora.
  γc e γs operano sulle resistenze.
- dg è condiviso con Dettagli → Copriferro e durabilità; modificarlo invalida il
  taglio. Δe MC è distinto per asse e visibile solo per quel modello. Asl
  automatica richiede conferma dell'ancoraggio, anche con staffe nel Model Code;
  cambiare la geometria revoca la conferma.
- wlim è comune alle SLE. Vuoto: criterio del codice; MC e classi ambientali
  non tabulate richiedono un valore esplicito. NTC/UNI conservano i propri
  criteri e ignorano l'override. Modificare wlim aggiorna la verifica senza
  ricostruire l'equilibrio tensionale invariato.

### Geometrie

Rettangolo, T, cerchio, rettangolo cavo e cerchio cavo sono collegati a taglio
 e fessurazione. Nel taglio automatico si impiegano larghezza resistente minima
 e armatura efficace nei due versi. I fori sono sottratti. Per i cerchi occorre
scegliere il modello: le riduzioni per pile NTC non vengono trasferite agli altri
codici, che richiedono z/d assegnato e una schematizzazione verificata.

wk richiede analisi lineare con CLS teso escluso. Le fasce efficaci sono tagli
 geometrici della sezione reale, verificati separatamente senza sommare aree di
 facce diverse. La trazione uniforme/disuniforme è inclusa. DS usa il baricentro
 della fascia per il sistema fine e controlla anche il sistema grossolano, con
 intera area tesa e fattore 0,5 DK.

Le superfici interne hanno fasce di parete o anello limitate a metà spessore.
Nel rettangolo si limita anche l'estensione tangenziale alla faccia del foro:
le barre esterne agli angoli non diventano armatura della parete interna.
Un foro compresso non richiede verifica di apertura; una superficie tesa senza
armatura efficace/interasse definito lascia il controllo incompleto. Un
superamento noto resta negativo. Report e vista mostrano lo stesso inviluppo.
L'estensione a pareti/anelli è una schematizzazione geometrica del modulo,
esplicitata nelle tracce; non è una validazione sperimentale di tutte le cavità.

### Campo ancora non coperto

Il modulo non costituisce una verifica normativa completa. Restano esclusi:

- taglio CAP con componenti dei cavi e apertura con aderenza dei trefoli;
- torsione accoppiata e dettagli/ancoraggi nazionali diversi dal modello NTC
  disponibile; torsione automatica della T;
- armatura minima per deformazioni imposte, fatica, fuoco, gerarchia sismica,
  punzonamento, appoggi, perdite e secondo ordine;
- interazione generale Vx–Vy: le resistenze sono controllate separatamente;
- wk non lineare, barre lisce MC/DIN, durabilità nazionale completa e incremento
  favorevole del limite NS legato al copriferro;
- normative di seconda generazione e FRC.

Per usare il modulo consulta [Sezione in c.a.](wiki:guida-sezione-ca); per le formule implementate consulta [Teoria della sezione](wiki:sezione-in-calcestruzzo-armato).

Le edizioni indicate identificano il codice implementato. La scelta dell’edizione e dell’annesso nazionale applicabili all’opera resta un passaggio distinto. Le attribuzioni DIN/DK/NS provenienti dalla documentazione precedente richiedono riscontro sull’annesso applicabile prima dell’uso progettuale: questa integrazione verifica il comportamento del codice, non completa una validazione indipendente di tutti gli annessi.
