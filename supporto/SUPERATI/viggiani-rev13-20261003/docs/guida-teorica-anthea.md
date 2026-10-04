# Guida teorica dei calcoli di ANTHEA

Modelli formule ipotesi ed esempi dei moduli disponibili

Edizione 3 del 2 ottobre 2026 — revisione documentale 12

Questa edizione unifica la documentazione di ANTHEA in due volumi globali. Il volume pratico comprende uso, interfaccia e procedure; quello teorico comprende modelli, formule, ipotesi, limiti e approfondimenti di tutti i moduli. I capitoli di approfondimento conservano integralmente i contenuti delle precedenti schede. Audit, migrazioni e studi conservano la loro data e il loro ambito storico: non descrivono automaticamente lo stato attuale del programma.


Questa guida descrive il comportamento dei motori di ANTHEA documentati il 26 settembre 2026, integrando nella revisione 03 le sezioni da ponte H con anima inclinata e cassoncino disponibili il 27 settembre 2026 e nella revisione 04 la torsione, la distorsione e i diaframmi del cassoncino disponibili il 28 settembre 2026. Le revisioni 05 e 06 includono i muri con due stratigrafie e il percorso guidato della stabilità globale, distinguendo le proposte di input dal calcolo Bishop. Spiega come i dati diventano geometrie, azioni, resistenze, tensioni e stime; chiarisce inoltre quali risultati appartengono a un modello semplificato e quali controlli richiedono informazioni ulteriori. Il manuale pratico separato illustra i comandi dell'interfaccia.

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

```math
f_{cd} = \frac{\alpha_{cc} f_{ck}}{\gamma_c}
f_{yd} = \frac{f_{yk}}{\gamma_s}
\varepsilon_{yd} = \frac{f_{yd}}{E_s}
```

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

```math
c_{\min} = \max(10; c_{bond}; c_{dur}) + c_{rugosita} + c_{abrasione}
c_{nom} = \max(c_{\min}+\Delta c_{dev}; c_{controterra})
```

Il ramo EC2 determina la classe strutturale secondo esposizione, vita e opzioni ammesse, poi consulta la tabella di durabilità. Le esposizioni che non definiscono da sole quel requisito richiedono un'associazione pertinente, anziché essere trasformate in una classe equivalente arbitraria.

Il ramo NTC utilizza tre livelli di severità. Il valore tabellare di base è (15 mm per elemento a piastra, 20 mm negli altri casi) più 10 mm per livello di severità; si aggiungono 5 mm se fck è inferiore a C0 = 35 + 5 × severità. La vita di 100 anni aggiunge 10 mm, una resistenza sotto il minimo pertinente aggiunge 5 mm e l'opzione di qualità del copriferro riduce di 5 mm. Il minimo pertinente può essere assegnato nel campo ammesso dal motore e non coincide necessariamente con tutte le prescrizioni di composizione.

Come esempio del ramo NTC, XF2, fck = 30 MPa, elemento non a piastra, vita 50 anni, barra 16 mm, aggregato 20 mm e Δcdev = 10 mm danno 45 mm nominali senza riduzione di qualità. Se si passa a vita 100 anni, lo stesso caso dà 55 mm. I valori sono esempi riproducibili del codice NtcCover, utili per controllare l'input.

## 3 Palo verticale

### 3 1 Geometria e tensioni geostatiche

Per un palo circolare di diametro D si usano area di base Ab e perimetro u. La stratigrafia è integrata per tratte; la falda può suddividere una stessa tratta in una parte asciutta e una immersa. Sotto falda la tensione efficace cresce con il peso sommerso, mentre la tensione totale continua a usare il peso saturo.

```math
A_b = \frac{\pi D^2}{4}
u = \pi D
\gamma' = \max(0; \gamma_{sat}-9{,}81)
\sigma'_v(z) = \int \gamma'(z)\,dz
```

La media della tensione efficace in una tratta è ottenuta integrando il profilo effettivo, compreso il cambio di pendenza in corrispondenza della falda. Usare la sola tensione al centro dello strato può dare un valore diverso se il tratto attraversa quella discontinuità. Il peso del terreno di uno strato continua a influire sugli strati sottostanti anche quando la sua resistenza laterale viene esclusa.

### 3 2 Laterale drenato

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

### 3 3 Laterale non drenato

Negli strati coesivi interessati dal ramo non drenato sotto falda si impiega τs = α Cu. Per pali battuti α vale 1 fino a Cu = 25 kPa, decresce secondo 1 − 0,0111(Cu − 25) fra 25 e 70 kPa e vale 0,5 da 70 kPa. Per le altre tecnologie vale 0,7, poi 0,7 − 0,008(Cu − 25), poi 0,35 negli stessi intervalli. Queste sono le espressioni a tratti effettivamente implementate.

I tratti sopra falda che non soddisfano le condizioni del ramo non drenato riutilizzano il calcolo drenato; gli strati granulari conservano la propria formulazione. La curva denominata non drenata può quindi essere composta da contributi calcolati con rami diversi. Occorre leggere la stratigrafia e non attribuire Cu a tutto il profilo.

### 3 4 Resistenza di punta e abaco Nq

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

### 3 5 Più indagini e coefficienti

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

### 3 6 Efficienza di gruppo e limiti

Converse Labarre usa la geometria del reticolo nelle due direzioni. Gli angoli della seguente espressione sono in gradi; ciascun termine è nullo quando in quella direzione esiste un solo palo.

```math
\eta = 1-\frac{\arctan(D/s_x)}{90}\frac{n_x-1}{n_x} -\frac{\arctan(D/s_y)}{90}\frac{n_y-1}{n_y}
```

Feld conta le coppie adiacenti ortogonali e diagonali: P = (nx−1)ny + nx(ny−1) + 2(nx−1)(ny−1), poi η = 1 − 2P/(16 nx ny). Per questi due metodi la versione corrente applica la stessa efficienza a compressione e trazione. L'opzione manuale permette valori distinti. Un risultato non positivo viene rifiutato.

La profondità analizzabile deve essere coperta dalle stratigrafie necessarie. Sono fuori dal modello automatico cedimenti, attrito negativo, resistenza del blocco di gruppo e interazione completa terreno struttura. Il rapporto Ed/Rd riguarda la resistenza assiale considerata e non esprime da solo la prestazione di esercizio.

## 4 Micropalo verticale

### 4 1 Correlazione del bulbo

La resistenza laterale usa curve digitalizzate Bustamante Doix documentate nel materiale di riferimento di Viggiani, sezione 13.1.6. Le famiglie SG, AL, MC e R e le curve 1 IRS e 2 IGU individuano la correlazione applicabile. La grandezza p_l, in MPa, viene interpolata linearmente fra i punti della curva; l'ordinata viene convertita in kPa. Il codice rifiuta valori esterni al campo disponibile. Il campo storico dell'interfaccia è «Pressione p_i = p_l»: l'uguaglianza è un'assunzione dell'integrazione e non dimostra che la pressione della pompa coincida fisicamente con il parametro geotecnico dell'abaco.

Il diametro del bulbo viene stimato come Ds = α D, dove D è il diametro di perforazione. La resistenza di ogni tratta attiva vale π Ds Lj τj. α rappresenta l'espansione convenzionale e va scelto in relazione a terreno e iniezione; non è un incremento di resistenza indipendente dalla geometria.

```math
D_s = \alpha D
R_s = \sum_j\pi D_s L_j\tau_j
```

Per esempio, D = 0,20 m, α = 1,30, lunghezza attiva 8 m e τ = 150 kPa producono Ds = 0,26 m e Rs = 980,18 kN prima dei coefficienti. Aumentare α del 10% aumenta linearmente la superficie resistente, a parità delle altre ipotesi. Il risultato resta condizionato alla validità della correlazione e della realizzazione del bulbo.

### 4 2 Inclinazione punta e peso

Per un'inclinazione θ dalla verticale, una differenza di quota Δz corrisponde a una tratta lungo l'asse Δs = Δz/cos θ. La resistenza viene sommata lungo quell'asse. Non deve essere letta direttamente come componente verticale di una capacità di gruppo senza risolvere la geometria delle azioni.

La punta opzionale è una frazione assegnata del laterale; non deriva da una capacità portante indipendente. Per il peso si separano area del tubo e area di boiacca. Il diametro del tubo è distinto da quello del bulbo. La componente adottata nella procedura segue il fattore cos θ previsto dal modello.

```math
A_{s,tubo} = \frac{\pi(D_e^2-D_i^2)}{4}
q = \frac{9{,}81\cdot7850 A_{s,tubo}}{1000}+\gamma_{boiacca}A_{boiacca}
W = q s\cos\theta
```

Diametri e aree devono essere riportati in metri e metri quadrati nella formula del peso. Media, minimi, ξ, γ ed efficienze seguono la struttura del calcolo verticale. Instabilità del tubo, flessione, sfilamento del collegamento e trasferimento locale fra tubo e boiacca restano verifiche distinte.

## 5 Capacità orizzontale con Broms

### 5 1 Idealizzazione e pressione limite

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

```math
R_k = \min\left(\frac{H_{u,medio}}{\xi_3};\frac{H_{u,\min}}{\xi_4}\right)
R_d = \frac{\eta R_k}{1{,}3}
```

HEd è già un'azione di progetto assegnata. Il motore non genera l'intero percorso delle combinazioni normative. L'efficienza di gruppo basata su Reese e Van Impe usa fattori direzionali: davanti min[1; 0,7(s/D)^0,26], dietro min[1; 0,48(s/D)^0,38], lateralmente min[1; 0,64(s/D)^0,34]. I contributi diagonali combinano i fattori longitudinali e laterali secondo l'angolo; il prodotto dei vicini attivi dà η. Questa riduzione agisce su Rd, senza ridisegnare il meccanismo Hu del singolo palo.

### 5 5 Momento resistente del palo in c a

La procedura automatica considera una sezione circolare con 4–512 barre uniformemente distribuite. La distanza dell'asse delle barre dal bordo comprende copriferro, diametro della staffa e mezzo diametro longitudinale. N è positivo a compressione in questo motore.

Si assume conservazione delle sezioni piane, calcestruzzo compresso a parabola rettangolo senza contributo teso e acciaio elastico perfettamente plastico. Si sottrae il calcestruzzo sostituito dalle barre. L'equilibrio assiale viene ricercato nel campo con asse neutro interno, 0 < x < D. Non è quindi una procedura generale per qualsiasi stato assiale, inclusi i campi interamente tesi o compressi.

Due integrazioni, 28 × 96 e 56 × 192, vengono confrontate. Se lo scarto supera il 2% il momento automatico non è accettato. Un My manuale può essere utilizzato con provenienza esplicita, ma il meccanismo di Broms non dimostra la duttilità necessaria a sviluppare la cerniera.

### 5 6 Tubo CHS del micropalo

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

## 6 Sezione in calcestruzzo armato

### 6 1 Geometria e deformazioni piane

Sono disponibili sezioni rettangolari, circolari e a T, con fori centrali per le forme supportate. Il contorno circolare è discretizzato mediante un poligono con numero di lati configurabile. Le aree dei vuoti sono escluse e le barre incompatibili con il contorno o interne ai vuoti vengono rifiutate.

L'ipotesi cinematica è una distribuzione piana delle deformazioni. Indicando con ε0 la deformazione al riferimento e con kx e ky i gradienti, la deformazione in un punto è una funzione affine delle coordinate. Le convenzioni dei gradienti e dei momenti sono trasformate nel sistema di riferimento esposto dal foglio.

```math
\varepsilon(x,y) = \varepsilon_0+k_x x+k_y y
N = \int_{A_c}\sigma_c\,dA+\sum A_s\sigma_s+\sum A_p\sigma_p
```

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

L'area efficace viene costruita sulla zona tesa e sulle barre pertinenti. Nella trazione integrale sono esaminate regioni di bordo o radiali e governa il risultato massimo; non si sommano le aree sovrapposte come se fossero indipendenti. Disposizioni e superfici interne non supportate restano fuori campo. La disponibilità della formula va verificata anche rispetto al metodo tensionale e alla presenza di tendini.

### 6 5 Esempio di fessurazione

Assumere σs = 200 MPa, Es = 200000 MPa, Ecm = 33000 MPa, fctm = 2,9 MPa, ρeff = 0,02, φeq = 16 mm, c = 30 mm, k1 = 0,8, k2 = 0,5 e lunga durata. Si ottiene αe = 6,0606 e Δσ = 65,03 MPa. La deformazione calcolata è 0,0006748, maggiore del minimo 0,0006000.

Il termine vicino vale (102 + 136)/1,7 = 140 mm. La soglia di interasse è 190 mm. Con s = 150 mm, wk = 1,7 × 140 × 0,0006748 = 0,1606 mm. Se s supera 190 mm e h−x = 300 mm, il termine distante vale 225 mm e wk aumenta a 0,2581 mm. Cambiano quindi apertura e ramo governante, pur conservando σs e area efficace nell'esempio.

### 6 6 Taglio

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

### 6 7 Torsione e interazione

La sezione resistente a torsione è un circuito periferico chiuso con area Ak, perimetro uk e spessore efficace t. La geometria automatica è disponibile per rettangolo e cerchio, pieni o con foro compatibile. Sono richieste staffe chiuse e armatura periferica adeguata; lo spessore deve contenerne gli assi.

```math
T_{Rcd} = \frac{2A_k t\,0{,}5 f_{cd}\cot\theta}{(1+\cot^2\theta)10^6}
T_{Rsd} = \frac{2A_k(A_{sw}/s)f_{yd}\cot\theta}{10^6}
T_{Rld} = \frac{2A_k(A_{sl,disp}/u_k)f_{yd}}{\cot\theta\,10^6}
T_{Rd} = \min(T_{Rcd};T_{Rsd};T_{Rld})
```

Asl,disp è l'armatura longitudinale disponibile per la torsione dopo la flessione, da assegnare consapevolmente. Taglio e torsione devono usare lo stesso cot θ. Il programma controlla anche la somma |T|/TRcd + |Vx|/VRcd,x + |Vy|/VRcd,y per il calcestruzzo e |T|/TRsd + max(|Vx|/VRsd,x; |Vy|/VRsd,y) per le staffe. L'estensione a due componenti di taglio è una combinazione conservativa del modello, non un dominio normativo generale ricostruito in ogni dettaglio.

### 6 8 Dettagli e curva momento curvatura

Le verifiche costruttive dipendono dal tipo di elemento scelto. Il modulo confronta geometria, armature e parametri necessari con i limiti implementati e mantiene «Da completare» quando mancano informazioni. La lunghezza di ancoraggio rettilineo parte da lbrqd = φσsd/(4fbd), con i coefficienti di forma e condizioni α1–α5 assunti unitari nel ramo documentato. Le sovrapposizioni introducono α6 e i relativi minimi; i dettagli speciali non vengono ricavati da un disegno ideale della sezione.

La curva M χ viene costruita a N fissato nella direzione assegnata, con passi uniformi o quadratici fino al limite resistente. Ogni punto richiede equilibrio di sezione. In una sezione asimmetrica la direzione del gradiente di deformazione può non coincidere con quella del momento. Non sono inclusi automaticamente softening strutturale, lunghezza della cerniera, rotazione globale o interazione con instabilità dell'elemento.

## 7 Sezione composta da ponte

### 7 1 Geometria e omogeneizzazione

Il modello rappresenta una sezione locale composta da carpenteria, soletta e barre opzionali. La carpenteria può essere un H saldato con anima verticale, un H con anima inclinata oppure un cassoncino con due anime simmetriche, due piattabande superiori e un fondo. La seconda piattabanda inferiore è disponibile solo per l'H verticale. Le piastre restano elementi reali, con posizione e geometria proprie. La larghezza efficace beff della soletta è un dato esterno. La piena collaborazione è assunta nel calcolo N–Mx; lo scorrimento non viene introdotto come un grado di libertà del solver di sezione.

Nel metodo elastico si trasforma il contributo del calcestruzzo mediante n = Ea/Ec,eff. La relazione implementata per gli effetti differiti è n = n0(1+ψLφ), con n0 = Ea/Ecm. L'inversione permette di assegnare direttamente n. È richiesto n ≥ n0.

```math
n_0 = \frac{E_a}{E_{cm}}
n = n_0(1+\psi_L\varphi)
\varphi = \frac{n/n_0-1}{\psi_L}
```

Il coefficiente ψL distingue la natura dell'effetto: nel percorso documentato G2 usa 1,1 e il ritiro 0,55. Questi coefficienti non costituiscono una legge completa nel tempo. Un φ assegnato a una fase descrive la rigidezza efficace di quel contributo secondo il metodo scelto.

### 7 2 Geometria delle anime inclinate e del cassoncino

Indichiamo con hw l'altezza libera verticale, tw lo spessore normale alla lamiera e δ lo scostamento orizzontale fra sommità e piede. Per l'H inclinata δ è positivo verso destra; per il cassoncino è il rientro simmetrico di ogni anima verso l'interno. L'angolo α è misurato dalla verticale. Per evitare confusione con il coefficiente di omogeneizzazione n, il numero di anime è indicato con nw: vale uno per l'H e due per il cassoncino.

```math
\alpha = \arctan\left(\frac{\delta}{h_w}\right)
\ell_w = \sqrt{h_w^2+\delta^2} = \frac{h_w}{\cos\alpha}
t_{w,h} = \frac{t_w}{\cos\alpha}
```

Le anime sono rappresentate come lamiere di spessore normale costante tagliate alle quote orizzontali delle flange. La loro larghezza orizzontale è tw,h, non tw. I valori immessi restano hw e tw: non si deve anticipare nell'input la trasformazione, che il motore esegue internamente. Il campo implementato impone |α| ≤ 45°, equivalente a |δ| ≤ hw. È un limite dell'implementazione geometrica, non una soglia normativa di sicurezza.

Nel cassoncino s_top e s_bottom sono gli interassi fra gli assi delle anime in sommità e al piede. La larghezza bt è quella di ciascuna piattabanda superiore; bb è quella dell'intero fondo. Un valore positivo di δ restringe il fondo, mentre un valore negativo lo allarga, purché la geometria sia valida.

```math
s_{bottom} = s_{top}-2\delta
b_{interno} = s_{bottom}-t_{w,h}
b_{sbalzo} = \frac{b_b-s_{bottom}-t_{w,h}}{2}
```

Per la geometria accettata devono risultare s_top ≥ bt, s_bottom > tw,h e bb ≥ s_bottom + tw,h; inoltre ciascuna piattabanda deve essere più larga dello spessore orizzontale dell'anima. Queste condizioni impediscono sovrapposizione delle flange superiori, contatto delle anime e fondo insufficiente a contenerle. La larghezza interna e gli sbalzi sono netti rispetto agli ingombri delle anime. Il programma applica una piccola tolleranza numerica al controllo di contenimento, che non modifica il significato geometrico delle disuguaglianze.

### 7 3 Equivalenza per sforzo normale e flessione retta

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

### 7 4 Esempi numerici delle nuove sezioni

Gli esempi riguardano esclusivamente la carpenteria lorda. Nel primo caso si usano hw = 1800 mm, tw = 14 mm, δ = 300 mm, piattabanda superiore 500 × 25 mm e inferiore 700 × 30 mm. Nel secondo caso si mantengono hw e tw, si assegnano δ = 250 mm, s_top = 1800 mm, due piattabande superiori da 450 × 25 mm e fondo 1400 × 25 mm.

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

### 7 5 Campo del modello e compatibilità dei dati

Le geometrie inclinate sono disponibili attraverso lo stesso ingresso dei tre metodi e delle curve di risposta; l'adozione della sezione equivalente non cambia il significato di fasi, carichi incrementali e riferimento al getto. Nei metodi storici i controlli locali di taglio, connessione e accessori restano non valutati; il metodo non lineare conserva il proprio campo istantaneo e lordo. La presenza della nuova forma non estende il campo di verifica del metodo selezionato.

La chiusura superiore del cassoncino mediante soletta attiva il modello torsionale di cella chiusa soltanto quando si abilitano le verifiche a torsione; in tal caso sono calcolati anche distorsione e diaframmi. Restano fuori dal calcolo gli irrigidimenti longitudinali del fondo, la verifica del fondo compresso come piastra irrigidita e la torsione non uniforme del cassone aperto. Il fondo viene trattato come lamiera interna non irrigidita longitudinalmente, con i suoi sbalzi esterni. Instabilità globale e comportamento dell'intero ponte richiedono altri modelli.

Gli archivi senza le chiavi del tipo di sezione continuano a rappresentare H saldato. Il dato di seconda piastra inferiore viene escluso dall'adattatore per H inclinata e cassoncino, anche se era salvato in un precedente H. Il risultato espone sia i parametri equivalenti sia quelli reali; i report riportano le ipotesi e una tabella delle lamiere. Non si deve usare un valore equivalente come dimensione esecutiva della singola lamiera.

### 7 6 Trasporto delle azioni

Il riferimento del momento deve essere coerente con il punto di applicazione di N. Il codice riporta il momento al riferimento comune attraverso la quota yN espressa in millimetri.

```math
M_{x0} = M_x-\frac{N y_N}{1000}
```

Con N = 1000 kN e una differenza di quota di 200 mm, il trasporto modifica il momento di 200 kNm con il segno stabilito dalla formula. Trascurare questa operazione può spiegare differenze rilevanti fra due calcoli che hanno la stessa sezione e gli stessi valori nominali di N e M.

È possibile riferirsi al baricentro lordo della fase, a quello efficace aggiornato o a un riferimento comune. Nel secondo caso l'eccentricità cambia durante l'iterazione delle larghezze efficaci. La scelta fa parte del problema fisico e deve essere riportata insieme alle azioni.

### 7 7 Metodo cumulativo

Ogni incremento viene analizzato con il proprio coefficiente di omogeneizzazione e la propria situazione di collaborazione. Le tensioni vengono sommate sulla configurazione efficace comune risultante dall'iterazione. Questo metodo è adatto alla sovrapposizione elastica prevista dall'implementazione, ma non conserva la stessa memoria delle deformazioni di un'analisi cronologica.

La riduzione locale delle piastre dipende dalle tensioni complessive, per cui geometria efficace e tensioni vengono aggiornate iterativamente. L'aggiunta di una fase modifica anche la geometria efficace comune sulla quale sono valutati i contributi. Non è quindi corretto aspettarsi che le tensioni di una fase precedente restino sempre identiche al calcolo eseguito isolatamente prima dell'aggiunta.

Il cumulativo dispone dei controlli aggiuntivi di taglio, appoggi, irrigidimenti e connessione. Le azioni in ingresso sono già quelle della combinazione da verificare; la scelta SLU o SLE seleziona limiti e percorsi di controllo, senza costruire i carichi fattorizzati.

### 7 8 Metodo storico lineare

Il metodo conserva il riferimento di deformazione al momento del getto e gli stati incrementali. La soletta attivata in una fase non acquisisce retroattivamente le tensioni dovute ai carichi applicati alla carpenteria prima della sua collaborazione. Il percorso cronologico diventa quindi parte dei dati del problema.

φ e n si applicano ai nuovi incrementi. Cambiare il coefficiente di una fase futura non produce automaticamente il rilassamento nel tempo di tutti gli stati precedenti. Il metodo non è un integratore reologico completo con storia di età, umidità, maturazione e viscosità per ogni giorno. La riduzione locale di classe 4 è disponibile, mentre i controlli accessori di taglio e connessione non sono valutati in questo percorso.

### 7 9 Metodo storico non lineare

La sezione è discretizzata a fibre e l'equilibrio N Mx viene risolto seguendo la storia. L'acciaio adotta una legge bilineare con incrudimento isotropo, memoria plastica e scarico elastico. Il calcestruzzo usa l'inviluppo del materiale tabulato disponibile nella libreria, senza una legge completa di danno ciclico e degradazione.

Il calcolo è istantaneo, su sezione lorda e con proprietà caratteristiche. Non applica la riduzione locale di classe 4 e non sostituisce le verifiche di instabilità con la plasticità delle fibre. Il percorso non ammette di simulare la viscosità alterando arbitrariamente il modulo di un materiale plastico attraverso n. Le impostazioni iniziali usano 160 suddivisioni nell'anima, 8 nelle flange, 64 nel cls e 8 sottopassi per incremento; la sensibilità numerica va controllata nei casi impegnativi.

La memoria plastica comporta che due sequenze con la stessa risultante finale possano produrre stati diversi. Un ciclo carico scarico può lasciare deformazioni e tensioni residue. Ciò non implica che il modello descriva automaticamente una prova a fatica, la rottura oligociclica o il degrado del calcestruzzo confinato.

### 7 10 Ritiro

Il ritiro viene assegnato come deformazione propria del calcestruzzo, negativa per accorciamento. La procedura elastica costruisce una forza equivalente Ec,eff Ac Δεcs applicata al baricentro del cls netto e una correzione di tensione propria −Ec,eff Δεcs. L'insieme riproduce l'incompatibilità locale mantenendo l'equilibrio della sezione con le risultanti esterne previste.

```math
N_{eq} = E_{c,eff}A_c\Delta\varepsilon_{cs}
\Delta\sigma_{c,propria} = -E_{c,eff}\Delta\varepsilon_{cs}
```

Le unità vanno rese coerenti prima della conversione in kN. Il solo stato della sezione non determina le forze secondarie causate da vincoli longitudinali di una trave continua. Analogamente, gli scorrimenti concentrati presso le estremità richiedono un modello lungo l'asse del ponte o una domanda aggiuntiva assegnata.

### 7 11 Larghezze efficaci e convergenza

Per le anime inclinate la riduzione si calcola sulla lamiera di lunghezza ℓw e spessore normale tw, usando le tensioni ai due estremi. Se la fascia efficace lungo la lamiera misura beff,w, l'altezza verticale corrispondente è beff,w cos α. Associare questa altezza alla larghezza equivalente nw tw/cos α conserva l'area efficace delle nw lamiere. Non si deve calcolare la snellezza locale usando hw e tw,eq: sarebbe una piastra diversa da quella reale.

Nel cassoncino ogni piattabanda superiore ha due sbalzi rispetto alla propria anima. Il fondo è suddiviso in una lamiera interna fra le anime e due sbalzi esterni. Alla lamiera interna uniformemente compressa il modello applica kσ = 4; la sua riduzione è distinta da quella degli sbalzi. Le porzioni occupate dagli ingombri orizzontali delle anime vengono ricomposte con le larghezze efficaci delle parti libere. Non viene simulato un eventuale sistema di irrigidimenti longitudinali del fondo.

Per le piastre compresse si valuta il rapporto di tensione ψ, il coefficiente di instabilità kσ, la snellezza adimensionale e il fattore ρ. Le porzioni efficaci vengono poi ricollocate nella sezione e si ricalcolano proprietà e tensioni. La snellezza locale usa fy caratteristico, non fyd. Le piattabande aggiunte vengono valutate con i propri sbalzi.

Il significato di ρ è una riduzione della porzione resistente nella verifica elastica della piastra snella. Non è una riduzione fisica del peso e non comporta rimozione di acciaio dal disegno costruttivo. La sezione lorda continua a governare i quantitativi e alcune altre grandezze.

L'iterazione utilizza una tolleranza dell'ordine di 10⁻⁷ e massimo 120 passi, con rilassamento e accelerazione secondo la procedura corrente. Il rilassamento iniziale è 0,55, limitato nel campo ammesso. Si controlla anche un residuo di equilibrio relativo dell'ordine di 10⁻⁵. Un arresto al numero massimo di iterazioni non è una convergenza positiva.

### 7 12 Limiti di tensione e interpretazione delle curve

Il percorso elastico di esercizio confronta l'acciaio strutturale con fy, il cls con i limiti 0,60fck o 0,45fck secondo la situazione e l'armatura con 0,80fyk nel percorso previsto. L'esclusione della soletta tesa è una scelta del modello; l'armatura può restare attiva. Nel cumulativo non viene introdotta automaticamente una fessurazione parziale della soletta tale da risolvere ogni distribuzione tesa.

Le curve M κ a N costante e N ε a curvatura costante possono partire da stato vergine o dalla ricostruzione della storia fino a una fase. Il motore conserva la memoria prevista dallo storico non lineare. Il risultato è una risposta caratteristica della sezione lorda: per trasformarlo in una capacità di progetto servono le verifiche e i coefficienti pertinenti. Punti non convergenti non vengono sostituiti da un inviluppo artificiale.

## 8 Taglio irrigidimenti e connessione della sezione composta

### 8 1 Resistenza a taglio

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

### 8 2 Interazione con il momento e tensioni tangenziali

Oltre 0,5 VRd il taglio può ridurre il margine flessionale. Per il campo N = 0, fy ≤ 355 MPa e anima non interamente compressa, si calcolano capacità plastiche di riferimento integrando flange efficaci, anima intera e soletta compressa. La capacità Mf omette l'anima. Il cls teso è nullo e le barre sono omesse cautelativamente in queste capacità di riferimento. Le verifiche elastiche della sezione non vengono sostituite da tali integrazioni plastiche.

Negli altri casi ad alto taglio viene usato un inviluppo elastico cautelativo con Mf = 0, segnalato esplicitamente. Non è un dominio plastico esatto N M V per qualunque sezione. Le tensioni tangenziali includono la media già definita e il massimo del campo elastico, trasformato nel piano dell'anima reale, con somma algebrica dei contributi di fase.

L'inviluppo √(max|σ|² + 3 max|τ|²) è un controllo aggiuntivo conservativo; i due massimi possono trovarsi in punti diversi. Non sostituisce instabilità del pannello, verifiche degli appoggi o fatica.

### 8 3 Irrigidimenti appoggi e saldature

Il modello ammette piatti mono o bilaterali anche differenti, pannelli adiacenti diversi, appoggi interni o terminali, eccentricità della reazione, montante rigido a due coppie e saldature continue. Le verifiche di pressoflessione elastica includono un'amplificazione del secondo ordine e imperfezioni equivalenti nel campo del metodo implementato. La lunghezza critica parte dal rapporto Lcr/L assegnato, inizialmente 1,00.

Un irrigidimento insufficiente non aumenta la resistenza del pannello a taglio. Le azioni di traversi, intagli, concentrazioni locali o dettagli non inseriti non vengono ricavate dalla sola sezione trasversale. La capacità di un piatto non dimostra da sola che anima, flangia e saldature trasferiscano l'intera reazione.

Con anima inclinata le dimensioni di controllo di irrigidimenti e saldature seguono la lamiera reale, non l'altezza dell'anima equivalente. Per il cassoncino l'implementazione ripartisce la reazione d'appoggio assegnata fra le due anime e usa dettagli per anima; questa ipotesi non sostituisce l'analisi di un appoggio eccentrico che solleciti in modo diverso le due pareti. Con le verifiche a torsione attive il torcente trasferito agli apparecchi aggiunge la coppia T/e_b alla metà della reazione sull'anima più caricata; altre distribuzioni trasversali devono essere valutate separatamente.

### 8 4 Resistenza e domanda dei pioli

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

### 8 5 Servizio dettagli e fatica

Il limite 0,75 PRd viene controllato nella situazione caratteristica di esercizio prevista; non viene trasferito automaticamente a una combinazione quasi permanente. I dettagli comprendono passi longitudinali e trasversali, bordi, testa, copriferro, posizione rispetto alle barre, rapporto fra diametro e spessore della flangia e armature trasversali.

I passi minimi implementati sono 5d longitudinale e 2,5d trasversale, con massimo longitudinale min(800 mm; 4hc). Le condizioni di azioni ripetute e fatica attivano il limite pertinente d ≤ 1,5tf; per il campo statico disponibile è adottato cautelativamente 2,5tf. Testa e distanze non sono dettagli ornamentali: possono governare la validità del collegamento anche con PEd basso.

Sono presenti controlli dell'armatura trasversale, superfici di scorrimento, ancoraggio e fatica resistente dei pioli con interazione della flangia tesa, quando i dati necessari sono assegnati. Restano fuori campo, fra gli altri, sollevamento, splitting attraverso lo spessore, gruppi non uniformi, mensole locali e lamiere grecate. Una verifica non alimentata con le escursioni e i dati di fatica non ricava autonomamente lo spettro di traffico.

### 8 6 Torsione del cassoncino

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

### 8 7 Distorsione e diaframmi

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

## 9 Bridge Design

### 9 1 Natura del predimensionamento

Bridge Design è un modello parametrico di ordine di grandezza, con geometrie idealizzate e criteri espliciti. Il sito thebridgeeng.com/design è stato usato come riferimento funzionale e osservato variando gli input. Le regole ricostruibili dalla risposta sono state affiancate da assunzioni autonome dichiarate. Non viene affermata identità con il motore proprietario del sito, con il suo listino o con la sua verifica AASHTO dettagliata.

Il modulo separa le scelte geometriche dalle ipotesi di costo e ambientali. Per questa ragione un nuovo prezzo non cambia la resistenza del terreno o l'inerzia dell'impalcato, e un nuovo paesaggio non cambia alcuna quantità. Le otto famiglie condividono il procedimento, ma utilizzano sezioni e rapporti di snellezza diversi.

### 9 2 Larghezza e ripartizione delle campate

La larghezza totale W comprende tutte le corsie, le due banchine, lo spartitraffico e le due fasce delle barriere. Non è la sola superficie caricabile dalle corsie normative.

```math
W = n_c b_c+2\,banchina+spartitraffico+2\,barriera
```

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

```math
d = \max\left[d_{\min};\frac{L_{\max}}{r}k\right]
```

k vale 0,95 per continuità e 1,10 per campate appoggiate. Un'altezza positiva inserita dall'utente sostituisce la formula. Nel cassone variabile l'altezza sulla pila è max(d; Lmax/18), mentre quantità e rigidezza sono valutate con altezza media d + (dpila−d)/3. Il modello non analizza le fasi costruttive a sbalzo.

I campi usuali producono avvisi, non una dimostrazione di fattibilità. Il fatto che una luce rientri nella tabella non verifica flessione, taglio, vibrazioni o trasporto dei prefabbricati. Il numero di travi deriva dalla larghezza e dall'interasse con arrotondamento intero e minimo di due per le famiglie a travi. Per cassoni e soletta il conteggio segue la geometria specifica.

### 9 4 Sezioni quantità e rigidezza

Le sezioni sono composte da rettangoli ideali. La soletta occupa W per il proprio spessore. Le travi a T aggiungono anime; le I in c.a.p. usano due flange e un'anima semplificate, con rialzo sotto soletta; le U e i cassoni aggiungono fondo e anime. Per le U la lunghezza inclinata delle anime è usata nella quantità, mentre la rigidezza conserva una rappresentazione rettangolare semplificata.

Le travi metalliche utilizzano spessori in millimetri convertiti in metri, piattabande e anime. Nei cassoni metallici l'inclinazione delle anime è espressa come H per 4V e incide sulla lunghezza reale della lamiera. La massa della carpenteria è aumentata del 15% per rappresentare diaframmi, irrigidimenti e connessioni; tale maggiorazione non viene applicata all'inerzia flessionale.

```math
E_c = 22000\left(\frac{f_c+8}{10}\right)^{0{,}3}\ \mathrm{MPa}
n = \frac{200000}{E_c}
y_G = \frac{\sum_i n_i A_i y_i}{\sum_i n_i A_i}
I_{eq} = \sum_i n_i\left[I_i+A_i(y_i-y_G)^2\right]
```

ni vale 1 per il cls e n per l'acciaio. Ieq è lordo e non fessurato, senza scorrimento. Per i cassoni variabili è riferito alla sezione media, non a una trave con EI variabile lungo l'asse. Le incidenze di armatura e precompressione sono quantità parametriche e non modificano l'inerzia attraverso una disposizione reale di barre e cavi.

### 9 5 Carichi ed equilibrio della trave

Il permanente comprende peso del cls a 25 kN/m³, peso della carpenteria, permanenti portati per superficie e 8 kN/m per ogni linea di barriera, due oppure tre se esiste spartitraffico. Il traffico è un carico uniforme equivalente esteso a tutta la larghezza. Le azioni si riferiscono all'intero impalcato.

```math
g = 25A_{cls}+\frac{9{,}81m_{acciaio}}{L}+g_2 W+8n_{barriere}
q_{servizio} = g+q_{traffico}W
q_{fattorizzato} = \gamma_G g+\gamma_Q q_{traffico}W
```

I valori iniziali sono g2 = 2,5 kN/m², traffico = 9 kN/m², γG = 1,35 e γQ = 1,50. Sono coefficienti di un modello equivalente e non identificano una combinazione normativa completa. In particolare il carico simultaneo su tutte le campate non produce l'inviluppo peggiore di traffico alternato e non rappresenta gli assi mobili.

Per continuità si risolve il sistema tridiagonale dei tre momenti con EI costante e momenti nulli alle estremità. Per campate appoggiate tutti i momenti agli appoggi sono nulli. Per due luci adiacenti L₁ e L₂, con tre momenti agli appoggi M₀, M₁ e M₂, l'equazione interna è la seguente.

```math
L_1 M_0+2(L_1+L_2)M_1+L_2 M_2 = -\frac{q(L_1^3+L_2^3)}{4}
```

In ogni campata, con ascissa locale x e momenti estremi ML e MR, la reazione sinistra del tratto, il momento e il taglio derivano direttamente dall'equilibrio. Le reazioni dei tratti adiacenti vengono sommate sull'appoggio comune.

```math
R_L = \frac{qL}{2}+\frac{M_R-M_L}{L}
M(x) = M_L+R_L x-\frac{qx^2}{2}
V(x) = R_L-qx
```

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

```math
R_{palo} = \pi D L q_s+\frac{\pi D^2 q_b}{4}
```

Il numero automatico dei pali è almeno quattro, arrotondato al numero pari superiore richiesto dall'assiale. La disposizione usa interasse indicativo 3D. Un numero manuale o un plinto manuale può risultare insufficiente: vengono controllati rapporto assiale e ingombro della disposizione. Il lato longitudinale del plinto è arrotondato al quarto di metro, mentre la larghezza trasversale accoglie la sottostruttura.

Nel rapporto di fondazione si includono reazione G+Q, peso della sottostruttura e del plinto. L'automatismo iniziale non risolve iterativamente un progetto geotecnico completo: un rapporto maggiore di uno richiede una revisione della configurazione. Non sono considerati momenti, taglio, sisma, erosione, cedimenti e interazione di gruppo.

### 9 8 Costi carbonio e durata

Il costo diretto è la somma dei prodotti fra quantità e prezzi. Le armature derivano da incidenze in kg/m³. Il costo dei pali è al metro e comprende cls e perforazione, mentre l'armatura è separata: il cls dei pali entra nel volume complessivo e nelle emissioni, ma non viene addebitato nuovamente come calcestruzzo di plinto.

```math
C_{diretto} = \sum_i Q_i p_i
C_{totale} = C_{diretto}(1+oneri/100)(1+imprevisti/100)
```

Il listino iniziale è in euro ed è puramente indicativo. L'intervallo basso alto usa la percentuale ± assegnata; non proviene da una distribuzione probabilistica. Appoggi e giunti dipendono anche dalla continuità, quindi cambiare schema può modificare le finiture oltre alle sollecitazioni.

La CO₂ somma cls, carpenteria, armature e precompressione, poi applica la maggiorazione per trasporti e cantiere. I fattori del cls sono in kg/m³; quelli degli acciai in kg/kg. Una massa espressa in tonnellate moltiplicata per kg/kg dà numericamente tonnellate di CO₂. Le opzioni a ridotta CO₂ e riciclato moltiplicano rispettivamente il fattore del cls e quello della carpenteria.

```math
CO_2 = \left[\frac{V_{cls}f_{cls}}{1000}+\sum m_{acciaio}f_{acciaio}\right](1+cantiere/100)
```

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

La revisione documentale 03 aggiunge una campagna mirata agli esempi di H inclinata e cassoncino. Sono stati rieseguiti 143 controlli della sezione composta e 2957 dei metodi e delle curve; sono stati aggiunti e superati 164 controlli in BridgeInclinedGuideChecks.cs. Questi ultimi confrontano area, baricentro e Ix con integrali geometrici indipendenti, tensioni elastiche con N/As e Mc/Ix nei tre metodi, segni dello scostamento, caso limite verticale, taglio medio, tensione critica della lamiera reale, fondo interno, salvataggio e limite geometrico di 45°. Le tolleranze sono numeriche e non margini di sicurezza progettuali: per esempio 0,01 mm⁴ su Ix e 0,0001 MPa sulle tensioni dei casi elastici.

La revisione documentale 04 aggiunge le prove della torsione del cassoncino. La libreria CompositeBridge 1.4 ha 17 test dedicati su 254: forme chiuse del rettangolo, modello a telaio indipendente e formule di Yoo et al. per il trapezio, trave su suolo elastico di Hetényi infinita e appoggiata, flussi e verifiche calcolati a mano. BridgeAudit supera 302 test, con risultati numerici identici ai precedenti per le sezioni ad H. In ANTHEA i controlli della sezione composta sono 172 e quelli degli esempi delle guide 189: i 25 nuovi ricalcolano a mano A0, q, J, τ e il diaframma d'appoggio dell'esempio e confrontano i valori della distorsione riportati nella guida. La prova dell'interfaccia controlla campi, colonna ΔT, risultati e relazione del cassoncino.

Il test dell'interfaccia ha superato 13 controlli specifici su scelta del tipo, visibilità dei campi, seconda piastra, calcolo, annotazioni del disegno e report. Le due schermate nella guida pratica provengono da questa esecuzione. I controlli aggiunti non sono una validazione generale di torsione, distorsione, appoggi asimmetrici, fatica o comportamento oltre il campo elastico delle nuove forme. Le esclusioni del modello rimangono quelle dichiarate nei capitoli precedenti.

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
| H inclinata e cassoncino | BridgeSection.CheckerInput.cs, RealSteelSection e ReportBridge.SectionType.cs; librerie CompositeBridge e Model |
| Esempi delle nuove sezioni | supporto/test/X.Verifiche/BridgeInclinedGuideChecks.cs e supporto/test/Desktop/BridgeSectionTypeSmokeChecks.cs |
| Torsione e distorsione del cassoncino | Box/BoxDistortion.cs e HSections/HBridgeSection.Torsion.cs in CompositeBridge; X.Desktop/Wpf/BridgeTorsion.cs; esempio in BridgeInclinedGuideChecks.cs |
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

## Muri di sostegno e stabilità globale

La revisione 07 mantiene l’allineamento del modulo muri a mensola e a gravità al percorso guidato della stabilità globale, mantenendo le sole schede Input e Verifiche. La guida specifica muri-sostegno.md e il PDF omonimo in supporto/docs descrivono input, formule e limiti. I documenti vanno usati insieme per controllare il significato dei risultati.

Le stratigrafie di monte e valle sono affiancate, indipendenti o collegate per spessori e proprietà; le profondità partono dalle rispettive superfici. Hlib è la distanza dalla sommità al terreno di valle: Dv=H+t−Hlib. Il terreno davanti al muro entra nei pesi, nei momenti, nelle sollecitazioni della mensola e nel ricoprimento efficace della portanza. La passiva richiede attivazione e frazione mobilitata; è esclusa nel sisma. Gli attriti del muro e della fondazione sono assegnabili oppure ricavati da φcv,k e tipo di interfaccia. Il valore a volume costante va caratterizzato, senza sostituirlo automaticamente con quello di picco.

Valori di calcolo consente di interrogare e modificare gli input e leggere coefficienti effettivi per combinazione, pesi, attriti, pressioni e sollecitazioni. Le forze risultanti dipendono dagli input e non si possono forzare. Le combinazioni sono modificabili: il preset locale è A1+M1+R3, quello globale A2+M2+R2. Azioni eccezionali e sisma Mononobe–Okabe o Wood semplificato sono espliciti. Il terreno del lato selezionato può essere trasferito ai moduli dei pali; la sezione selezionata può essere inviata al modulo c.a.

La stabilità globale Bishop ha un motore separato e un proprio profilo esteso, anche con due colonne profonde e confine verticale assegnato. Il pulsante Stabilità globale apre il percorso; al primo accesso a un profilo vuoto ne prepara i dati e attiva la verifica. Gli strati si inseriscono per spessore, con quota del fondo calcolata automaticamente. La precompilazione non prolunga le indagini: rilievo, terreni profondi e falda del sito vanno controllati e confermati. Il disegno rappresenta anche il terreno sotto il piano di posa. Parametri, dominio e combinazioni restano interrogabili e modificabili.

L’attrito automatico usa δd=k·atan(tanφcv,k/γMφ), con k=1 per gettato in opera e 2/3 per prefabbricato liscio. L’assegnazione usa tanδd=tanδk/γMφ. Coulomb/MO sul fusto tiene conto di δ muro; l’equilibrio del blocco muro e terreno sulla mensola usa il piano virtuale a δ=0, evitando il doppio conteggio delle forze interne. La portanza drenata include q′B′Nq iq e 0,5γ′B′²Nγ iγ; rimane fuori campo con base non ruvida, eccentricità eccessiva o inerzia sismica del terreno di fondazione. Lo scorrimento usa V′tanδb,d/γR.

Le relazioni Word includono le due stratigrafie e i coefficienti utilizzati. Le guide e gli esempi conservati per l’utente hanno anche PDF verificati graficamente. Cedimenti e spostamenti sono disponibili nei modelli separati descritti in Rev07; verifiche idrauliche, liquefazione e completamento esecutivo richiedono analisi dedicate: il modulo non emette una verifica complessiva dell’opera. I controlli delle due colonne sono documentati in supporto/artefatti/muri-due-colonne-20260930/CONTROLLO.pdf; quelli del percorso guidato, in supporto/artefatti/globale-guidata-20260930/CONTROLLO.pdf. La guida illustrata stabilita-globale-guida-rapida.pdf, in supporto/docs, contiene un modello stratificato salvato e i passi per riprodurre gli esiti. I confronti MAX precedenti restano parziali e non costituiscono validazione delle nuove opzioni.


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

La revisione ha superato 213 controlli del muro, 61 globali e 30 dell’interfaccia senza finestre native; non sono nuovi confronti MAX. Nel motore Bishop restano esclusi i meccanismi non circolari, la stabilità generale del versante e la liquefazione. I cedimenti sono ora trattati da un motore separato. Non è una certificazione complessiva dell’opera.


## Portanza sismica cedimenti spostamenti e armature Rev07

La revisione 07 aggiunge i calcoli dei punti 2, 3 e 5 nel campo dichiarato: inerzia del terreno nella portanza sismica; cedimenti e spostamenti; dettagli e predimensionamento delle armature e modelli strutturali per gravità. Rimangono le due schede Input e Verifiche. Il report Word contiene input, ipotesi, coefficienti, risultati e distinta delle barre; una quinta figura mostra le armature della sezione.

### Portanza sismica

In Terreno aprire Portanza sismica. Con Da sito si usa ah/g=ag/g·Ss·St, prima della riduzione β del muro; av/g=±0,5ah/g. In alternativa assegnare entrambe le accelerazioni. Il fattore γRD è modificabile: 1 per sabbia medio densa, 1,15 per sabbia sciolta asciutta. Non è un valore ricavato automaticamente dal solo angolo di attrito.

Si applica EN 1998-5:2004 allegato F alla fondazione nastriforme su terreno granulare asciutto, omogeneo e con base ruvida. Nmax=0,5γ(1−av/g)B²Nγ, con Nγ=2(Nq−1)tanφd. Si trascura il contributo favorevole del ricoprimento. N, V e M sono normalizzati con γRD·γR; F=γRD·ah/(g tanφd). Il γR della combinazione è applicato separatamente e dichiarato nella relazione.

Il dominio usa a=c=0,92; b=d=1,25; e=0,41; f=0,32; m=0,96; k=1; k′=0,39; cT=1,14; cM=c′M=1,01; β=2,90; γ=2,80. La somma dei termini di interazione deve essere ≤1, con 0<N̄<(1−0,96F)^0,39. La capacità è cercata lungo il raggio N,V,M: il tasso η è l’inverso del moltiplicatore limite, non il valore della funzione di interazione. Non si applicano una seconda volta larghezza efficace e fattori di inclinazione.

In Verifiche scegliere una combinazione SISMA e Portanza sismica nel riepilogo. Sono leggibili Nmax, F, N̄, V̄, M̄, limite verticale, interazione, tasso ed esito. Un’accelerazione mancante, un terreno fuori campo o una risultante non ammissibile restano esplicitamente non verificati.

### Cedimenti e spostamenti di esercizio

In Terreno attivare Calcola cedimenti finali e inserire, a partire dal piano di posa, nome, spessore e modulo edometrico M di ciascuno strato. M è espresso in kPa: per esempio 30 MPa corrispondono a 30000 kPa. Non viene dedotto da φ o riempito con un valore presunto. La pressione del terreno rimosso è il carico geostatico eliminato con lo scavo, da valutare nel modello scelto; zero è una scelta esplicita.

Si integra s=∫Δσz/M dz con tensioni Boussinesq di una striscia infinita e pressione di contatto lineare. Il calcolo è ripetuto a valle, al centro e a monte. La profondità deve arrivare a Δσz≤10% del carico netto oppure a un substrato rigido documentato. Viene controllata anche la convergenza numerica. Profili insufficienti non producono un esito favorevole né uno spostamento totale valido.

È un cedimento finale con moduli costanti assegnati: non ricostruisce tempi di consolidazione, OCR, scarico e ricarico, variazione di M con le tensioni o degrado ciclico. La rotazione θ=(smonte−svalle)/B deriva dal profilo libero; non è una soluzione accoppiata della fondazione rigida.

Per Calcola spostamenti in testa servono anche la rigidezza orizzontale di fondazione K per metro di muro, in kN/m², e il limite scelto. Il fusto in c.a. usa curvature delle sezioni fessurate GPC con viscosità assegnata. La doppia integrazione fornisce u del fusto con base fissa; la stima disaccoppiata totale è utesta=ufusto+H/K−θHmuro. Il termine di rotazione conserva il segno. Le curvature mancanti impediscono il risultato. La gravità usa il modello elastico del materiale nel campo senza trazione.

Limiti iniziali modificabili: 25 mm per cedimento, 0,002 rad per rotazione e 20 mm per spostamento in testa. Sono valori di avvio da valutare per l’opera, non limiti normativi universali. In Verifiche scegliere Cedimenti e spostamenti per la tabella per combinazione, i contributi degli strati e le curvature.

### Spostamenti permanenti Newmark

In Azioni aprire Spostamenti permanenti e aggiungere una storia. Scegliere SLD o SLV, inserire ky/g, fattore di scala e limite di spostamento. ky/g è la soglia di inizio scorrimento del muro, da ricavare da un’analisi di equilibrio: non coincide con ag/g e non viene dedotta automaticamente dal coefficiente kh.

Importare un CSV a due colonne separate da punto e virgola: tempo in secondi e accelerazione verso valle in g. È ammessa una prima riga t;a_g e il separatore decimale italiano. I tempi devono essere crescenti. Confermare che storia e scala siano compatibili con sito e stato limite. I campioni restano salvati nel file del muro.

Il blocco rigido scorre in una sola direzione. L’integrazione dei tratti lineari di a(t)−ky·g tiene conto degli attraversamenti della soglia, dell’arresto e della coda finale a terreno fermo. Wood è escluso perché presuppone un muro vincolato. Il risultato riguarda ciascuna storia; la scelta e la conformità normativa dell’insieme degli accelerogrammi devono essere documentate. Lo SLD non viene ricavato dal solo ag/g SLV.

### Armature e comando Calcola armature

In Geometria si possono mantenere le facce simmetriche o assegnare due armature indipendenti. La prima faccia è monte nel fusto e inferiore nelle solette; la seconda è valle nel fusto e superiore nelle solette. Rimangono disponibili le due zone verticali separate da h₁.

Ogni zona contiene barre principali, diametro e passo delle secondarie, lunghezza di ancoraggio, sovrapposizione e mandrino. Zero nelle lunghezze significa calcolo automatico, non lunghezza nulla. Il pannello dei dettagli espone aggregato, aderenza, vita nominale, tolleranza del copriferro e collegamenti della giunzione.

Calcola armature cerca diametri e numeri interi di barre entro i limiti impostati. Ogni candidato viene controllato con GPC a N–M, a taglio e in SLE; la proposta usa armature simmetriche per zona, poi modificabili. L’area stimata dalla flessione serve soltanto a scartare candidati impossibili. La verifica finale include i dettagli: una sezione resistente può avere una piega o una giunzione che non entra. In tal caso l’esito lo segnala e può occorrere aumentare lo spessore. La ricerca è interrompibile. Premere Applica proposta per sostituire le barre inserite; prima di applicare restano conservate.

Il predimensionamento usa ancoraggi a fyd e nessuna riduzione favorevole dei coefficienti di forma o confinamento. fbd deriva dalle proprietà GPC e dalle condizioni di aderenza. Le giunzioni sono alla stessa quota, quindi lo schema richiede il 100% delle barre giuntate e numeri compatibili nelle due zone. Si controllano lunghezza comune, interferro tra coppie, ingombro, area e passo dei collegamenti. Il mandrino considera anche la pressione nel calcestruzzo all’interno della piega.

In Vista dei risultati scegliere Armature: si vedono i percorsi delle barre, le pieghe, la fascia di sovrapposizione e le marche. Le barre giuntate sono affiancate lungo lo sviluppo del muro; le proiezioni sono leggermente distanziate sul disegno per leggibilità. Dettagli armature riporta la distinta, fbd, lunghezze richieste e usate, mandrini, quantità e tutti i controlli. I pesi sono stime per metro comprensive di ancoraggi, giunzioni e secondarie. Restano da definire il disegno esecutivo, i giunti di costruzione, i bordi lungo il muro, le interferenze tridimensionali e gli sfridi: la vista non è una distinta di officina.

### Gravità in calcestruzzo o muratura

In Materiali scegliere Calcestruzzo non armato oppure Muratura. Il primo usa fck e proprietà GPC, con compressione e taglio NTC 4.1.11 e fct1d=0,85 fctk,0.05/γc. Per muratura occorrono fk, fvk0, limite caratteristico a taglio, γM, fattore di confidenza e modulo elastico; non si possono usare automaticamente le resistenze del calcestruzzo.

Il fusto è una mensola libera: lunghezza efficace almeno 2H, imperfezione almeno H/200, rigidezza EI minima e amplificazione 1/(1−N/Ncr). La verifica rimane nel campo senza trazione e N<0,8Ncr. Se queste condizioni non sono soddisfatte serve un modello non lineare e l’esito non è dichiarato favorevole. Per muratura si controllano blocco compresso 0,85fk/(γM·FC) e scorrimento dei giunti; la resistenza a trazione è nulla. Le mensole di fondazione dello stesso materiale sono controllate anche a trazione, quindi una mensola in muratura può richiedere una diversa soluzione costruttiva.

La modalità Resistenze assegnate conserva la compatibilità con i file precedenti e i relativi controlli elastici; non diventa automaticamente una verifica normativa completa.

### Esempio ripercorribile e rapporto

Aprire supporto/artefatti/muri-completamento-20260930/interfaccia-finale/esempio-completo.anthea. Il modello dimostrativo ha H=3 m, B=3 m, due zone di armatura, terreno deformabile di spessore 25 m con M=30000 kPa, sisma da sito e una storia triangolare sintetica. Questi dati servono a riprodurre il test e non descrivono un sito reale. La storia sintetica non è un accelerogramma normativamente qualificato.

Nella stessa cartella sono presenti relazione Word e PDF, figure della sezione e risultati JSON. Il rapporto CONTROLLO.md e PDF nella cartella principale dell’attività descrive test, correzioni e limiti. I confronti MAX rimangono sospesi: nessuna delle nuove funzioni è dichiarata validata contro MAX 16.

I nuovi motori ShallowFoundationSeismic, FoundationSettlement e NewmarkSliding sono separati in X.Calculations/Geotechnics; l’adattatore del muro è RetainingWall.Serviceability. Geometria delle barre e predimensionamento sono separati dall’interfaccia. Materiali, equilibrio e tensioni delle sezioni riutilizzano GPC. Sono candidati per una successiva estrazione nelle librerie GPC; nessun repository GPC esterno è stato modificato.

Fonti: [JRC Eurocode 8 Worked Examples](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/EC8_Seismic_Design_of_Buildings-Worked_examples.pdf), §4.8; [JRC Eurocode 2 Detailing](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/05_EC2WS_Arrieta_Detailing.pdf); [USGS Newmark](https://pubs.usgs.gov/sir/2007/5196/sir2007-5196_text.pdf); NTC 2018 §§4.1.11 e 7.8.2.2.3; USACE EM 1110-1-1905, 2025.



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

La pagina del catalogo Edizioni Efesto https://www.edizioniefesto.it/libri/fondazioni/ conferma l'opera Fondazioni di Carlo Viggiani ma non consente di verificare le pagine della legge del modulo di reazione. Non sono state consultate pagine originali idonee a confermare edizione, numerazione, parametri, drenaggio o applicabilità della legge richiamata dall'utente. Non viene pertanto dichiarata alcuna formulazione Viggiani verificata. Sono richiesti frontespizio/edizione, pagina della formula e pagine delle definizioni, tabelle e limiti. I riferimenti Viggiani del precedente capitolo Broms non costituiscono una verifica della legge elastica.

### Modulo di reazione e conversioni

Si definisce kh come pressione orizzontale divisa per spostamento: unità F/L³. La larghezza di interazione adottata è il diametro geotecnico D, costante. La pressione elastica sul palo è opposta allo spostamento; la reazione per unità di lunghezza q ha unità F/L.

$$ k = D k_h,\quad q=-k y

La rigidezza distribuita k ha unità F/L². Se l'utente assegna direttamente k, il diametro non viene moltiplicato una seconda volta. In tabella si mostra anche kh equivalente = k/D. Per una discretizzazione a molle concentrate sarebbe K_i = integrale di k sulla lunghezza tributaria, con unità F/L. Tale procedura non è impiegata qui: non si fornisce un fittizio K nodale scalare al posto della matrice consistente.

Sono implementate tre leggi assegnate: kh costante per strato; kh = kh,rif z/D; k distribuito costante per strato. La seconda dà k = kh,rif z, con kh,rif in F/L³ e z misurata dal piano campagna. Se si definisce nh come gradiente di k, in questa precisa legge assegnata nh = kh,rif e ha unità F/L³. Non si estende questa identità a simboli nh impiegati con altre definizioni nelle fonti.

Le leggi non sono selezionate dal nome del terreno e non contengono coefficienti empirici. La scelta e la fonte devono documentare stato tensionale, condizioni drenate/non drenate, livello di deformazione e campo di validità. Non si assume che la crescita lineare sia applicabile indistintamente a sabbie e argille. In una sequenza stratificata ogni strato può avere parametri diversi, ma l'origine z resta unica al piano campagna. Non si azzera a ogni interfaccia.

### Modello e condizioni al contorno

x cresce dalla testa verso la punta; z = x − Llibero. Ltotale > Llibero ≥ 0. Il tratto libero ha k = 0; gli strati coprono almeno tutta la lunghezza immersa. Si assume EI positivo e costante, assegnato con origine esplicita. Piccoli spostamenti, sezioni piane, deformabilità a taglio trascurata e molle bilaterali lineari; nessuna forza assiale nel modello.

La convenzione adottata è H e y positivi verso destra, θ = y′, C positivo nel verso della rotazione nodale, M = EI y″, V = M′ e q = −k y. Ne consegue:

$$ EI y^{(4)} + k y = 0,\quad V'=q,\quad M'=V

H agisce sulla traslazione della testa; il carico generalizzato rotazionale è C, assegnato direttamente oppure ottenuto da H e. Inserire contemporaneamente C ed e non nulli è vietato. La lunghezza libera produce già il proprio braccio interno: e rappresenta solo un eventuale momento equivalente ulteriore rispetto alla testa.

Testa libera: spostamento e rotazione incogniti. Testa con rotazione impedita: θ = 0, traslazione libera e reazione rotazionale calcolata. Punta libera: nessun vincolo cinematico e azioni terminali nulle; cerniera: y = 0; incastro: y = θ = 0. Nessun incastro viene aggiunto per eliminare una labilità. In assenza di terreno il modello è stabile con punta incastrata, oppure con cerniera alla punta e rotazione impedita in testa; negli altri casi viene rifiutato. La fattorizzazione controlla inoltre singolarità e cattivo condizionamento.

Alla testa M = −C − Rθ e V = H. Alla punta i segni delle azioni interne sono coerenti con le reazioni esterne. Tutte le reazioni restituite sono azioni esercitate sul palo. Un carico negativo inverte i segni dell'intera risposta elastica.

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

I sorgenti supporto/test/ElasticPile.Checks referenziano direttamente il progetto Checker. Coprono anche carico nullo, linearità, inversione, tratto libero, discontinuità degli strati, equivalenza di sottostrati identici, conversione kh–k, origine globale z, unità, vincoli, dati invalidi e labilità. supporto/test/ElasticPile.UiChecks verifica instradamento, invalidazione, interfaccia ed esportazioni. Le evidenze sono in supporto/artefatti/palo-elastico. I test preesistenti Checker filtrati sulle classi Pile sono stati eseguiti: 32 superati, zero fallimenti. Si tratta di validazione numerica del modello elastico dichiarato; non di taratura sperimentale delle leggi del terreno.

## Approfondimenti integrati

- TEORICA A01: Scheda acciaio per armature
- TEORICA A02: Aggiornamento librerie · 24 settembre 2026
- TEORICA A03: Asse neutro nelle viste di sezione
- TEORICA A04: Audit ANTHEA: calcoli, dati comuni e progetti
- TEORICA A05: Dati e risultati del modulo in cemento armato
- TEORICA A06: ANTHEA — Audit generale di Bridge Design
- TEORICA A07: Bridge Design — sezioni tecniche e ottimizzazione
- TEORICA A08: Bridge Design
- TEORICA A09: Validazione — taglio e fessurazione CA
- TEORICA A10: Estensioni del modulo CA · settembre 2026
- TEORICA A11: Aggiornamento SLE, geometria e trefoli
- TEORICA A12: Esito integrazione Checker — 21 settembre 2026
- TEORICA A13: Sezione da ponte: irrigidimenti, appoggi e connessione
- TEORICA A14: ANTHEA.Calculations — separazione e trasferimento
- TEORICA A15: Migrazione del calcolo ponte in Checker — 25 settembre 2026
- TEORICA A16: Muri di sostegno
- TEORICA A17: Calcestruzzo ordinario — normative e verifiche di sezione
- TEORICA A18: Palo singolo: capacità portante orizzontale
- TEORICA A19: Palo orizzontale in terreno stratificato
- TEORICA A20: Curve della sezione composta
- TEORICA A21: Sezione da ponte con storico lineare e non lineare
- TEORICA A22: Metodo della sezione da ponte da rivedere per Checker
- TEORICA A23: Sezione composta da ponte
- TEORICA A24: Studio della condivisione dati tra i fogli ANTHEA
- TEORICA A25: Sezione da ponte — taglio, irrigidimenti e connessione
- TEORICA A26: Test dei metodi per i ponti
- TEORICA A27: Unificazione dei calcoli e controllo dei progetti
- TEORICA A28: Validazione della separazione della libreria di calcolo
- TEORICA A29: Teoria e validazione di Bridge Design


## TEORICA A01 — Scheda acciaio per armature

Modulo `mat_acciaio_armatura`, integrato in Materiali e nei progetti. Le proprietà sono nel nodo `input`; `riferimento` rimane una nota locale della scheda. Gli input incompleti si possono salvare; i risultati e il diagramma vengono sospesi finché i valori non sono validi.

- B450C (predefinito) e B450A: stesso catalogo DLL impiegato dalla verifica in c.a. Un avviso ricorda le limitazioni di impiego del B450A. Riferimenti: [CSLP, acciai](https://cslp.mit.gov.it/acciai), NTC 2018 §§ 7.4.2.2 e 11.3.2.1.
- FeB22k, FeB32k, FeB38k, FeB44k: valori minimi nominali dei prospetti 1-I e 2-I del [D.M. 09/01/1996, parte I, sezione I](https://ordingegneri.it/wp-content/uploads/sites/109/2024/06/DM-090196.pdf), pagina PDF 12. Es iniziale assunto pari a 200000 MPa; per cambiarlo si passa a Personalizzato. I valori non sostituiscono quelli derivanti da prove per strutture esistenti.
- L'allungamento A5 è una descrizione del catalogo storico: non viene convertito in εu. Quest'ultimo va inserito prima di usare il materiale completo nelle verifiche che lo richiedono. Gli acciai storici sono trasferiti alla verifica come materiali personalizzati, mantenendo il nome storico.
- Personalizzato: nome, Es, fyk, fu, εu, diagramma editabili. Valori in MPa e deformazioni in per mille. `gamma_s` è un parametro di calcolo editabile (valore iniziale 1,15); fyd = fyk / gamma_s, εyd = fyd / Es.

I campi condivisi sono nome/classe, Es, fyk, fu, εu, diagramma e gamma_s. Geometria, barre, carichi e materiali CHS restano separati. La scelta «solo questo foglio» mantiene un conflitto visibile. I dati assenti nei vecchi fogli seguono i valori impliciti del motore preesistente senza modificare i file durante il confronto.

Il diagramma mostra il legame **caratteristico** dell'acciaio, non una verifica di duttilità o di ammissibilità normativa. Il calcolo del palo orizzontale conserva il proprio modello elastoplastico: con materiale incrudente viene esposto un avviso, perché l'incrudimento non è usato da quel motore.

Verifica: `ANTHEA.exe --smoke-steel <cartella>` esercita interfaccia, catalogo, unità, errori, persistenza e condivisione; `--smoke-materials`, `--smoke-sharing` e `--smoke-projects` coprono le integrazioni preesistenti.


## TEORICA A02 — Aggiornamento librerie · 24 settembre 2026

Importate le DLL dai bin/Release dei singoli progetti indicati. Percorsi, versioni assembly e SHA-256 sono registrati in lib/Checker/manifest.json. Le versioni assembly sono rimaste invariate; sono cambiati i binari di Model, ModelData, Geometry, DelaunayMesh, Utilities e Checker.Concrete. GMsh.Net e UnsafeEx sono invariati.

### Verifiche

- Compilazione e pubblicazione desktop completate; hash delle DLL pubblicate coincidenti con il manifest.
- 294 controlli Checker/Excel/estensioni CA e 52 controlli del modulo ampliato superati.
- 71 controlli di interfaccia superati.
- Regressione storica: 414 casi e 2550 controlli software superati; 50 casi falliti. Ripetendo lo stesso eseguibile con le DLL precedenti si ottengono gli stessi 50 fallimenti, con messaggi e valori identici. La suite storica non è quindi interamente superata, ma non emergono nuovi fallimenti dovuti alle DLL.

### Prestazioni

Stessi casi e parametri, circolari a 32 lati. Mediana di tre esecuzioni dopo il primo utilizzo. Misure locali indicative, senza prove ANTHEA concorrenti; attività esterne al processo e variabilità della macchina possono influenzare i tempi. Nessuna differenza nei checksum diagnostici dei 96 campioni; questo confronto non sostituisce la validazione numerica.

| Sezione | Operazione | Prima, ms | Dopo, ms | Prima/dopo |
|---|---|---:|---:|---:|
| rettangolare | preparazione | 48.74 | 8.13 | 6 |
| rettangolare | dominio_3d_32 | 32.62 | 38.68 | 0.84 |
| rettangolare | 24_verifiche | 18.11 | 27.05 | 0.67 |
| rettangolare | 24_tensioni_non_lineari | 39.79 | 51.84 | 0.77 |
| rettangolare | 24_tensioni_lineari_fessure | 54.56 | 32.46 | 1.68 |
| rettangolare | curva_30_passi | 79.79 | 114.19 | 0.7 |
| rettangolare_cava | preparazione | 50.43 | 2.82 | 17.89 |
| rettangolare_cava | dominio_3d_32 | 31.83 | 70.62 | 0.45 |
| rettangolare_cava | 24_verifiche | 16.05 | 27.85 | 0.58 |
| rettangolare_cava | 24_tensioni_non_lineari | 39.27 | 45.59 | 0.86 |
| rettangolare_cava | 24_tensioni_lineari_fessure | 55.38 | 27.11 | 2.04 |
| rettangolare_cava | curva_30_passi | 84.83 | 103.03 | 0.82 |
| circolare | preparazione | 61.68 | 3.35 | 18.4 |
| circolare | dominio_3d_32 | 25.92 | 55.62 | 0.47 |
| circolare | 24_verifiche | 24.58 | 44.69 | 0.55 |
| circolare | 24_tensioni_non_lineari | 55.29 | 55.39 | 1 |
| circolare | 24_tensioni_lineari_fessure | 65.94 | 30.5 | 2.16 |
| circolare | curva_30_passi | 112.98 | 143.36 | 0.79 |
| circolare_cava | preparazione | 151.29 | 3.23 | 46.81 |
| circolare_cava | dominio_3d_32 | 78.89 | 33.78 | 2.34 |
| circolare_cava | 24_verifiche | 65.72 | 33.65 | 1.95 |
| circolare_cava | 24_tensioni_non_lineari | 96.2 | 39.77 | 2.42 |
| circolare_cava | 24_tensioni_lineari_fessure | 134.89 | 37.89 | 3.56 |
| circolare_cava | curva_30_passi | 228.15 | 123.23 | 1.85 |

La preparazione risulta più veloce in tutti i casi; alcune operazioni su sezioni rettangolari e circolari piene risultano più lente. Non si conclude un miglioramento uniforme.

### Artefatti locali

- supporto/tmp/ca_extensions/dll_update_before/benchmark.json
- supporto/tmp/ca_extensions/dll_update_after_isolated/benchmark.json
- supporto/tmp/ca_extensions/dll_update_comparison.json
- supporto/tmp/ca_extensions/dll_previous_regression.log
- supporto/tmp/ca_extensions/dll_update_regression.log
- supporto/tmp/ca_extensions/dll_update_ui/esito.txt
- supporto/tmp/ca_extensions/dll_previous: copia delle DLL precedenti e relativo manifest.

### Secondo aggiornamento · build delle 16:10

Importate le nuove versioni dai medesimi bin/Release: Model 1.1.0.3, Checker.Concrete 0.0.12.2, Geometry 2.0.1.10, Utilities 2.0.0.6 e DelaunayMesh 2.0.0.4. Aggiornata anche ModelData, ricompilata con versione invariata 0.0.1.10. Manifest e applicazione pubblicata aggiornati; hash verificati.

Superati 346 controlli CA e 71 controlli di interfaccia. La regressione completa restituisce gli stessi 50 fallimenti storici, con valori identici al precedente aggiornamento. Report: `supporto/tmp/ca_extensions/dll_1610_regression.json`; prova UI: `supporto/tmp/ca_extensions/dll_1610_ui/esito.txt`. I benchmark sopra si riferiscono al primo aggiornamento, non a questa build.


## TEORICA A03 — Asse neutro nelle viste di sezione

La casella **Asse neutro**, attiva inizialmente, mostra una linea magenta tratteggiata con fondo bianco per conservarne la leggibilità sul contouring. La preferenza è salvata; modificarla non richiede un nuovo calcolo.

| Vista | Risultato utilizzato | Significato della linea |
| --- | --- | --- |
| CLS, tensioni SLE e ispezione del dominio | `StressAnalysisResult.StrainPlane.GetNeutralAxis()` di Checker | ε = 0 della combinazione o del punto limite selezionato, anche in flessione deviata |
| Ponte, metodo cumulativo | `BridgeStage.SteelNeutralAxis` | σa = 0 delle tensioni cumulate nell’acciaio |
| Ponte, storico lineare e non lineare | `HistoryStageResult.TotalPlane`, convertito in `StrainPlane` di Checker | ε totale = 0 alla fase selezionata |

Nello storico, attivazione dei materiali, ritiro e plasticità possono separare lo zero della deformazione totale dagli zeri delle tensioni. La linea non è quindi presentata come uno zero tensionale comune a tutti i materiali. Nei trefoli anche la predeformazione può separare i due riferimenti.

Con campo uniforme non si disegna un asse arbitrario: compare «non definito (campo uniforme)». Un asse fuori dall’ingombro della sezione è segnalato; se fuori dal riquadro grafico compare anche «fuori vista». L’indicazione riguarda l’ingombro, non l’intersezione con il materiale di sezioni cave o concave.

Il pannello geometrico del ponte resta privo di risultati tensionali. L’asse segue la fase nella pagina dei risultati, nella vista staccata e nelle immagini esportate.

### Verifica

Harness: `supporto/test/Desktop/NeutralAxisSmokeChecks.cs`, avviabile con `ANTHEA.exe --smoke-neutral-axis <cartella-output>`.

Controlli su campi affini noti (assi orizzontali, verticali, inclinati, origine traslata), intersezione ε = 0 dopo il ritaglio grafico, campi uniformi, asse esterno, stato CLS reale di Checker, tutti e tre i metodi del ponte, cambio fase, preferenze e vista staccata. Non si tratta di una nuova validazione dei solutori strutturali: i test verificano l’adattamento e la rappresentazione dei risultati esistenti.

Schermate e risultati in `supporto/artefatti/assi-neutri/`.


## TEORICA A04 — Audit ANTHEA: calcoli, dati comuni e progetti

Data: 27 settembre 2026. Revisione del collegamento fra librerie, moduli di calcolo, archivi e interfaccia. Le verifiche riportate distinguono regressioni software e riscontri numerici indipendenti; non costituiscono una validazione indipendente di ogni formulazione disponibile nelle DLL.

### Organizzazione e librerie

Il contenitore trasferibile è `X.Calculations`, che produce `ANTHEA.Calculations.dll` (.NET 8) e non dipende da WPF, `X.Core` o Word. `CalculationService.Calculate` espone i nove moduli del catalogo. I servizi tipizzati sono disponibili direttamente per l’uso interattivo e per il riuso della cache. Il calcolo dei report richiama gli stessi servizi.

| Ambito | Punto di ingresso nella libreria | Dipendenze / modello |
|---|---|---|
| Palo verticale | `Calcolo.Calcola` | Integrazione per strati; `Nq`, coefficienti, falda ed efficienza comuni |
| Micropalo verticale | `Calcolo.Calcola(..., true)` | `BustamanteDoix`, `GeometriaMicropalo`, `Chs` |
| Palo orizzontale | `PaloOrizzontale.Calculate` | Broms; resistenza c.a. da `HorizontalConcreteSection` → Checker |
| Micropalo orizzontale | `PaloOrizzontale.Calculate` | `MicropaloOrizzontale`: CHS, classificazione e interazione N–M |
| Sezione c.a. | `ConcreteAnalysis`, `ConcreteAnalysisSession` | `CheckerSection` → GPCChecker.Concrete; materiali GPC.Model |
| Verifiche accessorie c.a. | `ConcreteShearAnalysis`, `ConcreteDetailingAnalysis`, `ConcreteCurvatureAnalysis` | Servizi numerici autonomi; ipotesi e limiti specifici |
| Sezione composta da ponte | `BridgeSection` | GPCChecker.CompositeBridge: fasi, storico, non lineare, M–κ/N–ε |
| Bridge Design | `BridgeConcept` | Predimensionamento, quantità, costi, CO₂ e ottimizzazione nella libreria |
| Materiali | `ConcreteMaterials`, `ConcreteMaterialCatalog`, `RebarMaterial`, `Materials/` | GPC.Model/ModelData; resistenze, legami, copriferro e durabilità |

Le distanze dai contorni e dai fori e l’appartenenza delle barre al calcestruzzo usano `GPC.Geometry.Polygon2d`; area delle barre e fasci da `Circle2d`, distanze fra barre da `Point2d`. La UI mantiene trasformazioni di schermo, disegno, formattazione e scelta degli input. Non è utile spostare le coordinate in pixel dentro il motore strutturale.

`SezioneCA` conserva la geometria parametrica e i metodi del vecchio motore per compatibilità con i confronti storici. Il percorso produttivo di resistenza/tensioni c.a. e del palo non chiama più quel solutore. La rimozione definitiva del codice storico richiede di separare anche il modello geometrico e archiviare i relativi test Python; non va confusa con la centralizzazione già realizzata.

### Correzioni e incoerenze individuate

1. **Resistenza c.a. del palo:** il precedente algoritmo dedicato usava il vecchio legame del calcestruzzo e l’acciaio elastoplastico, mentre il modulo c.a. impiegava Checker. Ora il palo usa la stessa ricerca diretta a N costante del modulo strutturale. Considera entrambi i versi Mx e prende il minimo valore assoluto, coerentemente con la possibile formazione di cerniere di segno opposto. Non costruisce un dominio 3D per ottenere due punti.
2. **Segni e geometria:** N geotecnico positivo a compressione viene convertito esplicitamente in N negativo per Checker. Il cerchio del motore precedente era integrato come cerchio esatto; Checker usa il poligono inscritto impostato nella sezione. Il numero di lati è ora esposto fra le opzioni del palo e nei risultati. La scelta può modificare la resistenza: a 32 lati il confronto a N = 2.500 kN evidenziava circa lo 0,45% rispetto al cerchio esatto.
3. **Risultati del palo:** il residuo N, la tolleranza di accettazione e il motore effettivo sostituiscono il vecchio indicatore di confronto fra mesh. La tolleranza del servizio condiviso è `max(1 kN, |N|·10⁻⁶)`: non si dichiara un equilibrio a 10⁻⁶ kN. Il piano resistente viene letto dal risultato nativo senza risolvere di nuovo l’equilibrio.
4. **Materiali:** `ConcreteMaterials.DesignValues` alimenta i valori fcd/fyd mostrati nella UI e nei risultati del palo; il catalogo CLS del palo usa Model. Sono esposti legami CLS/acciaio, fu, εu e discretizzazione del contorno. Un legame sconosciuto viene rifiutato. I campi assenti nei file storici conservano i default precedenti; campi esplicitamente vuoti o non finiti restano errori, anche per gli acciai storici che richiedono una deformazione ultima assegnata.
5. **Geometria delle armature:** eliminati i duplicati di distanza/appartenenza nel validatore Checker e i calcoli di area/diametro dei fasci nella UI dei trefoli. Restano i controlli di interferenza con bordi, fori e altre barre.
6. **Confronti di progetto:** `ProjectComparison` legge i campi una volta per foglio e alimenta sia il riepilogo UI sia il piano del report. Non conserva una cache globale: una modifica genera una nuova lettura. Nomi descrittivi dell’acciaio non diventano conflitti fisici; i rami delle staffe rettangolari sono inattivi per il cerchio; la modalità manuale delle barre è riconosciuta anche quando il relativo array è vuoto e incompleto.
7. **Confronto JSON:** eliminata la clonazione ricorsiva per ogni confronto. Si conserva l’equivalenza numerica fra stringhe e numeri, anche con virgola decimale. Corrette due peculiarità: zero e zero negativo sono equivalenti; proprietà differenti con valore null non sono equivalenti. L’ordine degli array resta significativo.
8. **Cache c.a.:** attivare/disattivare l’asse neutro non invalida dominio o tensioni. Il cambiamento di materiali e azioni continua a invalidare il risultato pertinente. Le verifiche di fessurazione vengono rivalutate anche quando le tensioni sono riutilizzabili.
9. **Affidabilità dei test UI:** l’app scrive una conferma unica solo dopo il ritorno dell’intera suite asincrona. Il runner la richiede, oltre al codice d’uscita, evitando sia falsi successi per chiusura anticipata sia falsi fallimenti dovuti alla diversa posizione della parola «OK» nei vecchi log.

Le formule geotecniche di palo verticale, micropalo e Broms non sono state sostituite. Le modifiche contemporanee di Bridge Design appartengono a una lavorazione separata e non fanno parte di questo audit.

### Dati comuni e gestione del progetto

La creazione, il confronto, l’uniformazione e il report usano `ModuleCatalog`, `CalculationCoefficients` e le regole `ProjectSharedData`. Restano locali azioni, combinazioni e storico delle fasi. Si confrontano i fogli nello stesso ambito e gli antenati compatibili; non si equiparano automaticamente i rami fratelli. I coefficienti con uguale simbolo ma significato diverso, per esempio γs geotecnico e γs delle armature, restano distinti.

Il comando di uniformazione mantiene i controlli direzionali di compatibilità e prepara le modifiche prima di applicarle. I valori inattivi rimangono salvati, senza apparire come discrepanze fisiche pertinenti. Un conflitto fra riferimenti non viene risolto scegliendo arbitrariamente un foglio. I tre coefficienti base del c.a. sono autorevoli in `input`; gli alias storici vengono sincronizzati.

Tooltip spiegano unità, segni, coefficienti, legami, discretizzazione, azioni già combinate, falda e abachi. «Modello e dati comuni…» è disponibile per sezioni c.a., pali e micropali; la finestra delle differenze include le regole del confronto. Le spiegazioni sono centralizzate in `CalculationHelp`.

### Prove eseguite

| Prova | Esito |
|---|---|
| Audit nuovo: confronti JSON, conflitti, cache, materiali e resistenza palo | 988 controlli superati |
| Progetti, creazione, servizi e coefficienti | 117 controlli superati |
| Libreria autonoma | 69 controlli superati |
| Checker c.a., Excel azioni, estensioni, dati | 102 + 18 + 174 + 27 controlli superati |
| Modulo c.a. ampliato | 78 controlli superati |
| Palo orizzontale | 1.080 controlli superati; suite CHS superata |
| Coesione e γsat | 312 + 57 controlli superati |
| Micropalo verticale | 34 casi di riferimento; 101.883 controlli superati |
| Ponte | 143 controlli di sezione, 2.957 di metodi/curve, 164 di esempi inclinati/cassoncino |
| Riferimenti Python conservati nel repository | 464/464 casi; 1.025.450 valori; delta assoluto massimo 1,82·10⁻¹² |
| Archivi e report software | 1.181 controlli superati; conferma finale presente |
| UI | Palo/CHS, c.a., estensioni, progetti, condivisione, report progetto, materiali, acciaio, gerarchie e workspace completati |

I riferimenti Python derivano dalla precedente implementazione, non da un programma commerciale indipendente. Gli attesi non sono stati modificati in questa revisione. I vecchi documenti del 26 settembre riportavano 50 differenze: **il confronto della revisione corrente non le riproduce**, con le DLL e gli attesi presenti oggi. Non si attribuisce la loro risoluzione a questo intervento senza ricostruire le revisioni intermedie.

Per il nuovo collegamento del palo a Checker il riscontro separato integra per strisce Simpson il contorno poligonale, calcolato autonomamente per intersezione dei lati. Conserva anche il riferimento analitico del cerchio per verificare la convergenza a 128 lati. Non confronta soltanto due chiamate allo stesso motore. Le prove di geometria e resistenze condivise includono valori analitici di area, inerzia, fcd, fyd e copriferro; cache, ereditarietà e UI sono invece prove software.

I report Word sono stati generati e verificati dai test di contenuto/pacchetto. Questo audit non attesta l’impaginazione di ogni pagina stampata. Le schermate UI sono negli artefatti.

### Prestazioni misurate

Stessa macchina, 24 processori logici; cinque esecuzioni, prima esclusa per riscaldamento. Tempi: mediana delle quattro esecuzioni successive. Allocazioni: media, in MB decimali. Nessuna soglia temporale viene usata per dichiarare corretti i risultati.

| Operazione | Prima | Dopo | Riduzione del tempo | Allocazioni prima → dopo |
|---|---:|---:|---:|---:|
| Confronto dati, 24 fogli | 155,5 ms | 83,0 ms | 46,6% | 56,4 → 11,2 MB |
| Preparazione del piano report, 24 fogli | 293,8 ms | 100,4 ms | 65,8% | 111,6 → 16,3 MB |
| Lettura tensioni di 12 righe già in cache | 0,164 ms | 0,132 ms | non significativa su tempi così piccoli | 0,110 → 0,106 MB |

Entrambi i confronti rilevano 144 conflitti prima e dopo. Il riscontro delle tensioni rimane invariato. Il tempo del piano report non comprende impaginazione e scrittura Word. Queste misure sono indicative, non un benchmark in ambiente isolato.

### Ottimizzazioni successive suggerite

1. **Firma semantica del modello c.a.** Oggi la firma include tutto l’input serializzato: nomi e alcuni campi storici possono provocare ricalcoli superflui. Un contratto tipizzato limitato a geometria/materiali/coefficenti/azioni consentirebbe invalidazioni più precise. Servono regressioni per ogni campo influente.
2. **Preparazione riutilizzabile di sezioni e mesh.** Riutilizzare `CheckerSectionModel` per gruppi di azioni dello stesso foglio; non condividere indiscriminatamente solver mutabili. La costruzione nativa Delaunay è già serializzata perché non thread-safe.
3. **Pali verticali con molti strati.** Precalcolare integrali cumulati per falda, tensione efficace e resistenza laterale, per evitare di ripercorrere tutti gli strati a ogni profondità. Verificare prima falda interna, disattivazione laterale e passaggi di strato contro i 464 riferimenti.
4. **Risultati compatti e output su richiesta.** Separare capacità/residui dai punti dei diagrammi e dalle righe Word. Ridurre il passo del grafico non aumenta la precisione della soluzione di Broms, ma moltiplica memoria e costo di esportazione.
5. **Confronti incrementali su progetti molto grandi.** Lo snapshot elimina le letture ripetute dei campi, ma il numero di coppie allo stesso livello resta quadratico. Un indice per chiave/ambito può ridurre il lavoro; deve conservare i conflitti multipli e le priorità gerarchiche.
6. **Calcoli annullabili e misure per fase.** Nei motori sincroni che accettano l’annullamento solo all’ingresso/uscita, aggiungere checkpoint interni e misurare costruzione, soluzione, raster e report separatamente prima di aumentare il parallelismo.

### Limiti effettivi da mantenere visibili

- Il ponte conserva metodi differenti (incrementale, storico, non lineare): non sono formule intercambiabili da fondere indiscriminatamente.
- Broms fornisce capacità ultima, non cedimenti/spostamenti SLE; l’estensione multistrato rimane sperimentale. Duttilità della cerniera e secondo ordine richiedono verifiche specifiche.
- Per c.a. le regole accessorie di taglio/fessurazione non sono implementate per tutte le normative elencate dal catalogo Model. I messaggi esistenti devono continuare a distinguerle dai risultati del dominio.
- Il caso SLE del trefolo con predeformazione nulla resta esplicitamente rifiutato dall’adattatore perché la DLL usa la predeformazione per distinguerlo dall’armatura ordinaria.
- Sono presenti documenti di validazione storici con esiti superati: questo dossier descrive l’esecuzione corrente, mantenendo tracciabile la cronologia.

Riferimenti normativi generali: [NTC 2018](https://www.gazzettaufficiale.it/eli/id/2018/2/20/18A00716/sg), [Circolare 2019](https://www.gazzettaufficiale.it/atto/serie_generale/caricaDettaglioAtto/originario?atto.codiceRedazionale=19A00855&atto.dataPubblicazioneGazzetta=2019-02-11). Questa revisione ha controllato i percorsi software e le regressioni; i collegamenti non sostituiscono una verifica articolo per articolo delle formulazioni.

### Ripetizione e trasferimento

Sorgenti dei nuovi test: `supporto/test/X.Verifiche/ProjectAuditChecks.cs`, `ProjectAuditBenchmark.cs`; integrazioni a `HorizontalChecks.cs` e `CalculationLibrary.Checks`. Output: `supporto/artefatti/audit-calcoli-progetti/`. Gli artefatti sono esclusi da Git; sorgenti e questo dossier sono versionati.

```powershell
dotnet run --project supporto/test/X.Verifiche -c Release -- --project-audit
dotnet run --project supporto/test/X.Verifiche -c Release -- --horizontal
dotnet run --project supporto/test/X.Verifiche -c Release -- --checker
dotnet run --project supporto/test/X.Verifiche -c Release -- --reference-only supporto/test/casi_confronto.json supporto/artefatti/audit-calcoli-progetti/nuovo-confronto.json
dotnet run --project supporto/test/X.Verifiche -c Release -- --audit-benchmark supporto/artefatti/audit-calcoli-progetti/nuovo-benchmark.json
./supporto/scripts/Test-CalculationUi.ps1
./supporto/scripts/Test-SoftwareReports.ps1
./supporto/scripts/Export-CalculationLibrary.ps1
```

La copia esportata contiene `X.Calculations`, i test autonomi e lo snapshot delle DLL GPC con SHA-256. Si compila ed esegue dalla propria cartella, senza riferimenti ai progetti UI o archivio del repository originale.

La copia `portable/` ha completato i 69 controlli autonomi. La cartella `app/` è aggiornata e ha superato nuovamente palo/CHS, progetti e report progetto. Gli SHA-256 delle DLL distribuite coincidono con `lib/Checker/manifest.json`: Utilities 2.0.0.8, Geometry 2.1.0.3, DelaunayMesh 2.0.0.8, Model 1.4.1.0, ModelData 0.0.1.16, Checker.Concrete 0.0.13.0 e Checker.CompositeBridge 1.3.1.0. Anche `ANTHEA.Calculations.dll` coincide fra build e `app/`.

È stata inoltre compilata una copia dei soli file destinati al commit, escludendo le modifiche concorrenti di Bridge Design: libreria autonoma, audit, palo, Checker e ponte superano le stesse prove. La compilazione desktop di questa copia termina senza errori e con un warning nullable preesistente in `BridgeDesignOptimization.cs`; la lavorazione concorrente contiene la relativa modifica, esclusa dal commit dell’audit.


## TEORICA A05 — Dati e risultati del modulo in cemento armato

Controllo del 25 settembre 2026 sull'interfaccia, sulle sorgenti dei dati e sul confronto dei fogli di progetto. Le formule dei verificatori e il solver Checker non sono stati modificati.

### Dati comuni e dati specifici

| Gruppo | Sorgente e comportamento |
| --- | --- |
| Geometria e copriferro | `input`, modificabili nel pannello di controllo. Il copriferro nei dettagli costruttivi è un richiamo in sola lettura. Sono confrontati solo i parametri della forma attiva, compreso il foro centrale. |
| Armatura ordinaria | Wizard oppure coordinate manuali. Con coordinate manuali il wizard è disabilitato e i suoi valori non partecipano alla validazione né al confronto. I valori sono conservati per il ripristino del wizard. |
| Staffe | Presenza, diametro, passo e schema hanno un solo punto di modifica, nel pannello di controllo. Taglio e dettagli richiamano gli stessi dati in sola lettura. I rami non sono ripetuti nei parametri delle singole direzioni di taglio. |
| Materiali | Le proprietà numeriche adottate restano la sorgente del calcolo. Cataloghi e materiali personalizzati sono modalità di assegnazione. I valori derivati visualizzati non sono ingressi indipendenti. |
| Coefficienti | Un solo pannello di modifica. `alpha_cc`, `gamma_c` e `gamma_s` in `input` sono autorevoli; i corrispondenti campi del workspace sono sincronizzati per compatibilità con gli archivi. |
| Azioni | Le famiglie plastica, elastica e SLE conservano azioni distinte. Domini 2D e 3D condividono le combinazioni della stessa famiglia. I carichi non partecipano alla propagazione fra fogli. |
| SLE | `sle_comuni` guida analisi, omogeneizzazione, ambiente e opzioni comuni di fessurazione. Le copie per famiglia sono sincronizzate. I campi n e φ sono legati fra loro, non costituiscono due ingressi indipendenti. Il report riporta le impostazioni comuni una sola volta. |
| Domini | Piano 2D, discretizzazione, strategie e riferimenti delle azioni rimangono specifici dell'analisi. Opzioni grafiche e filtri non cambiano le azioni verificate. |
| Taglio e torsione | Geometria derivata oppure assegnazione manuale esplicita. Modello di calcolo, cotangenti, ancoraggio e armatura disponibile per torsione sono dati specifici. Le staffe fisiche provengono dai dati comuni. |
| Trefoli | Materiale predefinito per i nuovi cavi e materiale assegnato a ciascun cavo hanno scopi distinti. Area e diametro equivalente sono collegati. |
| Durabilità | Aggregato, vita utile, qualità e tolleranza sono confrontati anche con la scheda Materiali, convertendo unità e valori booleani. Modelli EC2 e NTC differenti non sono uniformati implicitamente. |
| Dettagli, ancoraggi e curvatura | Conservano i parametri propri dell'elemento, della giunzione e del percorso di carico; geometria e materiali sono quelli comuni della sezione. Le conferme sul disegno restano esplicite. |

### Correzioni

- Eliminati i doppi ingressi dei rami delle staffe e del copriferro nei dettagli.
- La modifica dello schema delle staffe aggiorna anche i dettagli costruttivi.
- I parametri del wizard inattivo non bloccano più le barre manuali; il diametro delle barre laterali assenti non blocca la sezione.
- Il confronto di progetto ignora dimensioni di altre forme, file disattivate, armature automatiche sostituite da quelle manuali e dimensioni delle staffe assenti. Rileva invece presenza delle staffe, foro e dimensioni effettive del foro, prima incompleti nel confronto.
- I campi booleani opzionali assenti e falsi non producono conflitti spuri sulle seconde file.
- Confronto e report di progetto usano la stessa regola di pertinenza e lo stesso catalogo di etichette. I valori derivati dell'interfaccia e il vecchio n di riferimento non vengono ristampati come ingressi del progetto.
- Il controllo del copriferro usa il diametro massimo effettivo delle barre della sezione. Differenze nei parametri di durabilità impediscono un esito positivo basato su dati diversi.
- `VerificationSummary` uniforma gli esiti dei riquadri, inclusa la sezione composta. Per il CA lo stesso riepilogo viene esportato in JSON e riportato in Word per le verifiche selezionate. Un tasso non finito senza esito booleano valido è incompleto.
- Nel report CA la figura delle staffe compare una sola volta; la figura momento–curvatura viene inclusa anche quando Taglio non è selezionato.

### Compatibilità e verifiche

Gli archivi mantengono i parametri inattivi, le combinazioni e le opzioni delle verifiche. La revisione della sezione composta migra le vecchie quattro schede nelle nuove tre senza perdere fase, modalità della viewport o separatori.

Regressioni dedicate: `supporto/test/X.Verifiche --ca-data`. Regressioni numeriche esistenti: `--checker`. Controlli WPF: `--smoke-ca-features`, `--smoke-ca-extensions`, `--smoke-sharing`, `--smoke-hierarchy`, `--smoke-project-report` e `--smoke-bridge`.

I tre comandi di progetto e quello dell'acciaio avevano già un'implementazione, ma non erano raggiungibili dal selettore di avvio dei controlli automatici: ora usano lo stesso elenco di modalità del gestore degli errori.

Restano distinti gli esiti delle diverse verifiche: il riepilogo comune non somma tassi né tensioni di combinazioni diverse. Restano valide le segnalazioni del precedente audit del solver da ponte nella libreria Checker.


## TEORICA A06 — ANTHEA — Audit generale di Bridge Design

**Data:** 27 settembre 2026. **Ambito:** modulo `str_bridge_design`, motore, computo, geometrie, finestra WPF ed esplorazione delle alternative. Revisione di partenza del repository: `d889d9f`; riferimento precedente all'estensione: `48c431c`.

### 1. Esito e significato dei controlli

Il controllo ha individuato e corretto difetti reali nel predimensionamento automatico, nelle quantità e nell'interfaccia. La suite finale supera **44.259 asserzioni**. Questo numero comprende verifiche ripetute su casi diversi, non 44.259 progetti indipendenti o verifiche normative.

La campagna geometrica comprende **1.008 configurazioni**: **874 calcolate e controllate**, **134 rifiutate esplicitamente** perché il dimensionamento automatico non è compatibile con i parametri adottati. A questi casi si aggiungono le prove analitiche, 224 combinazioni famiglia/terreno/pila, le prove delle famiglie speciali e le prove di ottimizzazione.

Sono state eseguite **27 ricerche di campagna**, per **10.491 valutazioni complessive**, più una ricerca limite con prezzi e CO₂ nulli, oltre ai test di ricerca già presenti e alle prove attraverso la finestra. Sono superati anche build e smoke test WPF, inclusi 100 ponti casuali con semi riproducibili, caricamento del listino, salvataggio, riapertura, export e applicazione delle alternative.

**Giudizio tecnico:** le quantità sono ora più coerenti con le geometrie dichiarate e gli automatismi rispettano le proprie soglie nei casi verificati. Il modulo rimane adatto a confronti preliminari. Non dimostra che impalcato, spalle, pile o pali siano verificati strutturalmente: alcune dimensioni sono regole geometriche, altre derivano da soli equilibri ideali o carichi verticali centrati. Le limitazioni sono descritte per componente nei paragrafi successivi.

### 2. Procedura seguita

1. Lettura del motore, dei parametri, della ricerca e dei prospetti tecnici; controllo della separazione fra calcolo e interfaccia.
2. Conservazione del precedente assembly di calcolo in `baseline/` e delle evidenze originali dei 1.000 casi del sito. Nessuna nuova acquisizione del sito è stata presentata come parte di questo audit.
3. Acquisizione dei PDF ufficiali dei prezzari, estrazione delle voci pertinenti, verifica visiva delle tabelle e conservazione degli hash SHA-256.
4. Ricostruzione di carichi, reazioni, aree, volumi, peso delle sottostrutture, griglie dei pali e costo totale mediante controlli separati dal codice di produzione.
5. Correzione dei difetti individuati e aggiunta di test che esercitano i comportamenti corretti.
6. Esecuzione della campagna parametrica e riesecuzione dei 2.000 input della precedente campagna di confronto col sito.
7. Prove della finestra, dei grafici e delle esportazioni; ispezione delle immagini di prospetto, sezione e graduatoria.
8. Prove finali della ricerca: vincoli, enumerazione diretta di piccole griglie, graduatorie, frontiera Pareto, riproducibilità, cancellazione e applicazione della soluzione.

Gli output finali sono in `supporto/artefatti/bridge_design_general_audit/`. Le cartelle `calcoli_finali/` e `ui_finalissima/` contengono gli esiti validi conclusivi. Le cartelle precedenti conservano anche i tentativi intermedi, compresi gli errori successivamente corretti. L’ultima build usa `app_verificata/`: un tentativo nella cartella ordinaria aveva incontrato DLL occupate da altre sessioni. La build isolata è riuscita con zero errori e zero avvisi.

### 3. Prezzi: fonti, unità e decisioni

Fonti consultate: [ANAS, elenco prezzi ufficiale](https://www.stradeanas.it/it/elenco-prezzi), [PDF NC-MP 2026 Rev.1, giugno 2026](https://www.stradeanas.it/sites/default/files/fornitori/doc/elenco%20prezzi/NC-MP_LISTINO-PREZZI-2026-Rev1.pdf), [Emilia-Romagna, prezzario 2026 corretto](https://territorio.regione.emilia-romagna.it/osservatorio/elenco_regionale_prezzi). Il secondo serve da riscontro territoriale; non implica che il ponte sia localizzato in quella regione.

Tabella di riscontro numerico ANAS; le pagine sono quelle del file PDF, contando la copertina:

| Riferimento | Unità | Prezzo | Pagina PDF |
|---|---:|---:|---:|
| B.03.040.b, C45/55 | €/m³ | 260,21 | 38 |
| B.03.031.d, C35/45 fondazioni | €/m³ | 226,38 | 37 |
| B.03.035.d, C35/45 elevazione | €/m³ | 241,07 | 38 |
| B.05.030, B450C | €/kg | 1,66 | 47 |
| B.05.057, trefoli | €/kg | 2,02 | 48 |
| B.05.000.01.8.b, carpenteria | €/kg | 3,50 | 43 |
| B.05.000.01.9.b, reticolari/archi | €/kg | 4,24 | 44 |
| B.05.000.06.1, extra ortotropo | €/kg | 0,46 | 44 |
| B.04.001, casseri piani | €/m² | 40,34 | 40 |
| B.02.040.b, palo 1.000 mm | €/m | 280,67 | 28 |
| B.02.040.d, palo 1.500 mm | €/m | 542,04 | 28 |
| B.07.006.a/b, appoggi multidirezionali | €/kN | 3,66 / 2,93 | 56 |
| B.07.050.b.1, giunto | €/m | 2.245,44 | 64 |
| G.02.005.3.a, barriera H4 ponte | €/m | 362,73 | 203 |
| B.05.080.1.a/b, funi pendini | €/kg | 14,40 / 15,53 | 48–49 |

ANAS comprende SG 15% e utile 10%; sicurezza specifica esclusa. Pali: armatura separata. Calcestruzzo: casseri e armatura separati. Trefoli: ancoraggi separati. Carpenteria: varo ordinario incluso. Sovrapprezzo ortotropo: sola massa della lastra. [Fonte ANAS](https://www.stradeanas.it/sites/default/files/fornitori/doc/elenco%20prezzi/NC-MP_LISTINO-PREZZI-2026-Rev1.pdf).

Riscontro RER: A02.046.050, gabbia B450C dei pali, **1,59 €/kg**, pagina PDF 132; A03.007.015.d, fondazione C35/45 XC1-XC2, **257,28 €/m³**, pagina 145. Sono lavorazioni poste in opera; il calcestruzzo esclude ponteggi, casseri e armatura. La differenza dal riferimento nazionale non è di per sé un errore: cambiano analisi e ambiti della voce. [Fonte regionale](https://territorio.regione.emilia-romagna.it/osservatorio/elenco_regionale_prezzi).

### Valori ANTHEA aggiornati

| Parametro | Prima | Nuovo iniziale | Valutazione dell'audit |
|---|---:|---:|---|
| Cls impalcato, €/m³ | 240 | **260** | Allineamento al materiale iniziale C45/55; altre classi richiedono adeguamento manuale. |
| Cls sottostrutture/plinti, €/m³ | 200 | **240** | Prezzo medio aggregato; distinguere elevazione/fondazione in un computo di progetto. |
| Armatura ordinaria, €/t | 1.100 | **1.660** | Il vecchio valore era basso per fornitura e posa; conversione kg/t verificata. |
| Precompressione, €/t | 3.600 | 3.600 | Riserva di sistema; non assimilabile al solo materiale. Testate e configurazione non sono computate analiticamente. |
| Carpenteria ordinaria, €/t | 3.500 | 3.500 | Riferimento plausibile; controllare protezione e modalità costruttiva. |
| Cassoni, €/t | 4.000 | 4.000 | Maggiorazione convenzionale, non voce ANAS esatta. |
| Casseforme, €/m² | 50 | 50 | Plausibile ordine di grandezza; sostegni alti e centine non risolti. |
| Pali Ø1 / Ø1,5, €/m | 300 / 550 | 300 / 550 | Vicini ai riferimenti; non includono automaticamente tutte le condizioni di perforazione. |
| Appoggi, €/cad | 1.600 | **5.000** | Indennità media, non dimensionamento né conversione universale dal prezzario. |
| Giunti, €/m | 2.400 | 2.400 | Utilizzabile solo come stima per movimenti moderati; grandi ponti richiedono prezzi specifici. |
| Barriere, €/m | 240 | **360** | Il precedente valore era debole per bordo ponte ad alta capacità. |
| Pavimentazione, €/m² | 32 | 32 | **Non validata come pacchetto completo**: mancano stratigrafia e impermeabilizzazione. |
| Ortotropo, €/t | 5.000 | 5.000 | Prezzo aggregato prudenziale, non somma analitica delle sole masse interessate dal sovrapprezzo. |
| Cavi/pendini installati, €/t | 12.000 | **16.000** | Calibrazione preliminare; grandi stralli/cavi e terminali richiedono offerta specialistica. |
| Complessità speciale, €/t | 1.000 | 1.000 | Riserva aggiuntiva. Azzerare quando già compresa nel prezzo di carpenteria/offerta. |

La tabella dei nuovi valori è una scelta di calibrazione del software, non un nuovo prezzario ufficiale. Non è corretto dichiarare tutti i costi unitari “validati ANAS”. Le note in **Prezzi unitari** rendono visibile questa distinzione. Il pulsante **Applica valori orientativi 2026** carica i nuovi valori; **Annulla** ripristina quelli personali. Gli archivi esistenti mantengono il listino salvato.

La maggiorazione iniziale del 12% è ora denominata **Oneri aggiuntivi non computati**. Non deve essere intesa come nuova applicazione di spese generali e utile ai prezzi ufficiali. Il 15% di imprevisti è una riserva distinta. Entrambe sono modificabili e non sostituiscono il computo delle lavorazioni mancanti.

### Omissioni del computo corrette e residue

Sono state aggiunte le superfici equivalenti di casseratura di pile/spalle, le facce laterali dei plinti e la casseratura della soletta negli impalcati misti. Per i plinti si usa `2(B + L)t`; per i fusti, perimetro per altezza, con fondo e fianchi del pulvino. Le spalle usano una superficie equivalente del paramento. Non si aggiunge il costo del calcestruzzo dei pali una seconda volta: è già nel prezzo al metro; il suo volume entra comunque nelle quantità fisiche e nella CO₂.

Restano non computati analiticamente scavi, rinterri, drenaggi, accessi, protezioni, impermeabilizzazione, centine alte, sicurezza specifica, logistica eccezionale e dettagli degli ancoraggi. L'applicazione ora lo segnala. L'intervallo iniziale ±30% è un'ipotesi modificabile, non un intervallo statistico dimostrato, e può essere insufficiente per opere speciali.

### 4. Impalcati: ricostruzione e limiti

La larghezza è la somma di corsie, due banchine, spartitraffico e due fasce laterali. Le lunghezze sono ricostruite fra assi degli appoggi; la somma deve coincidere con la lunghezza impostata. Nei ponti ordinari continui con più di due campate, quelle terminali hanno peso geometrico 0,8 rispetto alle interne. Gli spostamenti dovuti all'ostacolo non possono produrre campate inferiori a 2 m.

| Famiglia | Regola di altezza automatica | Minimo, m | Campo orientativo luce, m |
|---|---|---:|---:|
| Soletta c.a. | Lmax/18 | 0,35 | 6–15 |
| T c.a. | Lmax/17 | 0,70 | 12–30 |
| I c.a.p. | Lmax/22,22 | 1,00 | 20–50 |
| U c.a.p. | Lmax/22,22 | 1,10 | 25–50 |
| Cassone c.a.p. | Lmax/22,22 | 1,30 | 35–80 |
| Conci variabile | Lmax/45; sulle pile almeno Lmax/18 | 2,00 | 80–200 |
| I acciaio-cls | Lmax/25 | 1,00 | 30–90 |
| Cassone acciaio-cls | Lmax/25 | 1,20 | 40–150 |
| Travi incorporate | Lmax/28 | 0,45 | 8–40 |
| Cassone ortotropo | Lmax/30 | 1,20 | 40–200 |
| Arco con catena | Lmax/120 per il solo impalcato | 0,80 | 40–250 |
| Strallato | Lcentrale/150 per il solo impalcato | 1,00 | 100–700 |
| Sospeso | Lcentrale/200 per il solo impalcato | 1,20 | 200–1.200 |
| Reticolare | Lmax/100 per il solo impalcato | 0,70 | 30–150 |

Per i primi dieci schemi si applica 0,95 al rapporto nelle configurazioni continue e 1,10 nelle indipendenti, prima del minimo. Le quattro strutture superiori non usano questi fattori. Questi numeri sono **regole del modello**, non limiti normativi dimostrati dall'audit. Il limite di lunghezza totale della scheda rimane 2.000 m: non tutte le combinazioni del catalogo sono quindi raggiungibili.

Le quantità derivano dalla somma delle aree dei componenti moltiplicata per la lunghezza. L'acciaio longitudinale usa densità 7,85 t/m³ e un'aggiunta di massa per accessori del 15%, ridotta al 5% nelle travi incorporate. La maggiorazione di massa non incrementa artificialmente la rigidezza. Nelle travi incorporate il calcestruzzo è netto dell'acciaio; nell'ortotropo non compare una soletta di cls inesistente.

**Anime inclinate:** il cassone metallico usa lo sviluppo reale dell'anima per area, massa e contributo all'inerzia. Nella U in c.a.p. era rimasta una discordanza: volume con sviluppo inclinato, rigidezza con altezza verticale. È stata corretta introducendo la stessa area inclinata anche nella sezione resistente equivalente, con contributo lungo l'altezza reale. Si tratta di una schematizzazione a pareti sottili, non della discretizzazione di ogni raccordo del prefabbricato.

Per i ponti ordinari il calcolo longitudinale è Euler-Bernoulli, EI lordo costante, carico uniforme contemporaneo su tutte le campate. Sono stati confrontati reazioni, momento e freccia della campata semplice con `qL/2`, `qL²/8` e `5qL⁴/(384EI)`; per due campate uguali sono controllati reazioni e momenti da soluzione chiusa. Altre 200 travi diseguali verificano equilibrio verticale, momento globale e spostamenti nulli agli appoggi.

Non sono verificati armatura necessaria, pressoflessione, taglio, instabilità locale delle lamiere, fatica, fessurazione, viscosità, precompressione nelle deformazioni, fasi di getto/varo o carichi mobili. Nel cassone a conci variabile la rigidezza equivalente non rappresenta le fasi a sbalzo. **Una freccia piccola o un'altezza conforme al rapporto L/d non certificano l'impalcato.**

### 5. Spalle, pile e fondazioni

### Spalle

L'altezza equivalente è `min(7 m, quota ponte − altezza impalcato)`. Lo spessore equivalente del paramento è `max(0,60 m, H/7)`. Il volume è `W × [H × spessore + 3 m²]`; il termine aggiuntivo rappresenta una riserva geometrica aggregata, non ali e muri definiti in pianta.

Il test ricostruisce esattamente questo volume, ma **la spalla non è dimensionata come opera di sostegno**. Mancano altezza effettiva del rilevato, spinta del terreno, sovraccarichi, acqua, azioni degli appoggi e geometria delle ali. Per quote elevate il limite di 7 m presume una sistemazione del terreno da definire: non dimostra che una spalla alta 7 m sia sufficiente. È uno dei punti da affinare per primi sul progetto reale.

### Pile ordinarie

Il fusto può essere circolare, a setto, a colonne o con testa a martello. Si ricostruiscono area per altezza e volume del pulvino. L'altezza libera tiene conto dell'impalcato, compresa la maggiore altezza del cassone variabile sulla pila.

Prima dell'audit la dimensione automatica dipendeva prevalentemente dall'altezza, e poteva superare le soglie del proprio filtro. Ora soddisfa contestualmente una snellezza convenzionale non superiore a 90 e una compressione media non superiore a `0,30 fc_sub`, includendo il peso del fusto e del pulvino. La misura viene arrotondata a 5 cm. Con `r=Ø/4` o `r=t/√12` e lunghezza efficace convenzionale `2H`, si controlla `λ=2H/r`. Per il carico assiale si ricava l'area da `(R + 25 Vpulvino)/(0,30 fc_sub × 1000 − 25H)`.

Il filtro verifica anche che il fusto sia contenuto nel lato del plinto. Il calcolo tratta fusti pieni e carico centrato. Una pila automatica più grande corregge la coerenza con queste soglie; non equivale a una verifica di secondo ordine, pressoflessione, sisma, vento o urto. Le dimensioni manuali restano quelle richieste, con segnalazione/esclusione quando incompatibili.

### Antenne

Il modello impiega due fusti quadrati pieni e un traverso. L'altezza comprende tratto sotto e sopra impalcato. La sezione automatica considera reazione amplificata e peso proprio amplificato; se il peso proprio per unità di area esaurisce la tensione di riferimento, il calcolo restituisce un'incompatibilità esplicita.

Per le antenne manuali si verifica anche il rapporto con la tensione di riferimento e si escludono quelle oltre soglia dalla ricerca. Questo ha prodotto 18 rifiuti nella campagna, principalmente verso gli estremi delle grandi luci. Non sono crash: un'antenna molto alta non viene resa artificiosamente ammissibile aumentando senza limite una sezione piena. Sezioni cave, rastremazioni e modelli più raffinati richiedono un'estensione specifica.

### Plinti diretti

La pressione di riferimento viene impostata dall'utente o dalla classe convenzionale di terreno: 1.000, 400, 200 o 100 kPa. Sono ipotesi, non risultati di indagini. Il lato iniziale dipende dal carico, poi il ciclo ricalcola peso proprio e pressione. Lo spessore resta `max(0,60 m, lato/6)`; la larghezza deve contenere il sostegno trasversale.

Il controllo è `η=(R + peso sottostruttura + peso plinto + eventuale blocco)/(B L p_rif)`. Il lato automatico cresce a passi di 0,25 m finché `η≤1`. Un aumento del lato aumenta anche il peso: con carichi molto grandi e terreno debole può non esistere una soluzione nello schema assunto. Il ciclo è limitato e segnala la mancata convergenza.

Non vengono controllati eccentricità, pressioni parzializzate, punzonamento, flessione/armatura, scorrimento, ribaltamento, cedimenti o interazione con fondazioni vicine. Lo spessore L/6 resta indicativo.

### Pali

Diametri disponibili: 1,0 e 1,5 m. Lunghezze automatiche per classe: 10, 15, 22, 30 m, modificabili. Il riferimento assiale è `π D L qs + π D² qb/4`; attrito e punta possono essere sostituiti dall'utente. La formula è stata verificata dimensionalmente e nel computo, ma non integra stratigrafia, falda, attrito negativo o coefficienti di una specifica procedura normativa.

Ora il numero automatico comprende il carico aggiuntivo del plinto, cresce per coppie da un minimo di quattro, e ricalcola contemporaneamente la griglia. Si assume una disposizione quadrata con passo 3D e lato minimo `(ceil(√n)−1)3D+2D`. La lunghezza computata è `Σn × Lpalo`; il volume è tale lunghezza per `πD²/4`. La capienza geometrica del plinto è verificata anche quando l'utente impone il numero di pali.

Esempio tratto dal corpus, caso **0004/native**: 20 pali e `η=1,108` prima; 24 pali e `η=0,963` dopo. È la correzione del peso trascurato nella scelta iniziale, non una calibrazione per imitare il sito.

Nella campagna geometrica 116 configurazioni sono state rifiutate per mancata convergenza della fondazione automatica; i dettagli sono registrati. Effetti di gruppo, cedimenti, carichi trasversali e flessione dei pali restano fuori dal modello. Le verifiche di un progetto appartengono a un livello diverso da queste soglie: riferimento generale [NTC, DM 17 gennaio 2018](https://www.gazzettaufficiale.it/eli/id/2018/2/20/18A00716/sg).

### 6. Famiglie speciali

**Arco:** arco metallico con catena, due piani e campate indipendenti. La spinta totale ideale è `H=qL²/(8f)` e viene equilibrata dalle catene. Le aree delle aste derivano dalla forza divisa per tensioni convenzionali modificabili. Non è un arco che scarica liberamente la spinta nel terreno.

**Reticolare:** due piani, correnti, diagonali e montanti ideali. Il predimensionamento dei correnti usa il momento diviso per l'altezza; le diagonali usano un riferimento cautelativo al taglio massimo. Il modello non sceglie profili di catalogo e non controlla instabilità o nodi.

**Strallato:** tre campate simmetriche L/4 + L/2 + L/4, due antenne e due piani di stralli. Per ogni strallo si controlla la componente verticale della trazione rispetto al carico tributario. La somma di tutte le componenti restituisce il carico dell'impalcato.

**Sospeso:** stesso schema di campate. Cavo centrale e cavi di riva sono parabolici sotto carico uniforme; sono presenti pendini anche sulle rive. Il precedente cavo di riva rettilineo non rappresentava correttamente il sostegno di quei tratti ed è stato sostituito. Le reazioni globali soddisfano equilibrio verticale e dei momenti. Il blocco di ancoraggio è stimato per peso mediante `(sollevamento + H/attrito)/25`, per ciascun estremo. Non è verificato a ribaltamento o per pressioni eccentriche.

Il peso delle strutture superiori viene aggiornato iterativamente nel carico fino a tolleranza relativa 10⁻⁸, con limite di 80 iterazioni. Le masse sono distribuite uniformemente per il calcolo ideale. Non si rappresentano carichi mobili asimmetrici, deformazioni dei cavi, redistribuzioni elastiche, fasi costruttive o aerodinamica. Per queste famiglie **non vengono mostrati falsi diagrammi globali di una trave ordinaria**.

**Travi incorporate:** profili ideali completamente inglobati; area cls netta, omogeneizzazione con `(Es/Ec−1)` per non duplicare il volume spostato. **Ortotropo:** lamiera, canalette e cassoni sono computati dalle loro dimensioni, con sviluppo reale delle pareti inclinate. Traversi e connessioni entrano come incidenza di massa; fatica e risposta locale della piastra non sono calcolate. Il ruolo di lamiera, irrigidimenti e fatica è documentato anche nel [manuale FHWA sugli impalcati ortotropi](https://www.fhwa.dot.gov/bridge/pubs/if12027/if12027.pdf); questo audit non ne implementa le verifiche.

### 7. Regressione sui dati del sito

Sono stati riutilizzati i 1.000 casi già acquisiti, ognuno nei due modi `native` e `resolved`. La riesecuzione è una **regressione del motore su dati congelati**, non una nuova prova live né una dimostrazione di equivalenza col sito.

| Esito | Numero |
|---|---:|
| Valutazioni | 2.000 |
| Calcolate prima e dopo | 1.871 |
| Rifiutate prima e dopo | 128 |
| Prima rifiutata, ora calcolabile | 1 |
| NaN / infinito attuali | 0 |
| Larghezza, altezze impalcato, luci o massa acciaio cambiate nei casi comuni | 0 |
| Inerzie cambiate | 250, tutte U in c.a.p. |
| Volume cls cambiato | 1.190 |

Nei 1.871 casi comuni il costo cresce fra 1,13% e 51,48%, mediana 7,66%, usando **gli stessi prezzi salvati nel corpus**. Quindi questi scostamenti non dipendono dai nuovi valori iniziali: derivano dalle sottostrutture/fondazioni corrette e dalle casseforme aggiunte. Le differenze rispetto al sito già documentate restano; non è corretto dire che ANTHEA ne replichi tutti i risultati.

### 8. Test di ottimizzazione

La ricerca confronta solo le combinazioni della griglia scelta. Non è un algoritmo di progetto strutturale e non garantisce l'ottimo al di fuori della griglia. Non ottimizza automaticamente ogni parametro delle strutture speciali: freccia dell'arco, altezza antenna, geometria dei cavi e scelta cls/ortotropo restano parametri del caso. Per confrontarli si modificano gli input e si ripete la ricerca.

L'audit esegue 21 ricerche vincolate su sette famiglie — I c.a.p., arco, reticolare, strallato, sospeso, travi incorporate, ortotropo — e tre obiettivi. Per ciascuna viene enumerata separatamente la griglia di tre altezze e confrontato il minimo. Altre sei ricerche libere, su L=120 e 480 m e tre obiettivi, raggiungono complessivamente tutte le 14 famiglie.

| Ricerca libera, piattaforma del test | Valutazioni per obiettivo | Soluzioni distinte | Costo dell'ottimo costo | CO₂ dell'ottimo costo |
|---|---:|---:|---:|---:|
| L=120 m | 1.308 | 1.074 | 1.359.105 € | 800,13 t |
| L=480 m | 2.161 | 1.932 | 4.723.017 € | 2.590,07 t |

Questi sono risultati riproducibili del test, non stime trasferibili a un altro ponte. A L=120 m l'ottimo CO₂ costa 1.403.834 € e produce 751,53 t; mostra che gli obiettivi possono scegliere configurazioni differenti.

Sono controllati: immutabilità del progetto; permanenza di prezzi/carichi/terreno/larghezza; vincoli di famiglia, campate, sezione e fondazioni; determinismo; ordinamento dei punteggi non arrotondati; normalizzazione dell'obiettivo misto, compreso il caso limite con costo e CO₂ entrambi nulli; frontiera Pareto verificata per dominanza diretta; traccia completa dei tentativi; riproducibilità delle alternative; top N indipendente dalla ricerca; griglie invalide o vuote; rifiuto dei sollevamenti ordinari; contenimento dei pali; annullamento prima e durante la ricerca.

Attraverso WPF sono controllati selezione dal grafico, preview senza modifica del progetto, scelta oltre le prime N righe, applicazione della soluzione, ritorno alle quote tecniche, undo, invalidazione al cambio dei prezzi e interruzione. La preview viene aggiornata a frequenza limitata quando cambia il migliore provvisorio. La graduatoria resta una famiglia di **soluzioni ammesse dalle soglie del modello**, non di progetti già verificati.

### 9. Ripetibilità e file di prova

Sorgenti principali: `supporto/test/BridgeDesign.Checks/AuditChecks.cs`, `OptimizationChecks.cs`, `ExplorationChecks.cs`, `Program.cs`; smoke test in `supporto/test/Desktop/BridgeDesignSmokeChecks.cs`. Il riepilogo della regressione è prodotto da `supporto/test/BridgeDesign.Checks/audit_evidence.py`.

```powershell
dotnet run --project supporto/test/BridgeDesign.Checks -c Release --no-restore -- supporto/artefatti/bridge_design_general_audit/calcoli_finali
dotnet build X.Desktop/X.Desktop.csproj -c Release --no-restore -o supporto/artefatti/bridge_design_general_audit/app_verificata
dotnet supporto/artefatti/bridge_design_general_audit/app_verificata/ANTHEA.dll --smoke-bridge-design supporto/artefatti/bridge_design_general_audit/ui_finalissima
dotnet run --project supporto/test/BridgeDesign.SiteComparison -c Release --no-restore -- --run supporto/artefatti/bridge_design_site_1000/engine-inputs.jsonl supporto/artefatti/bridge_design_general_audit/regression-2000.jsonl
python supporto/test/BridgeDesign.Checks/audit_evidence.py
```

Evidenze: `calcoli_finali/checks.txt`, `audit-cases.json`, `audit-dimensions.txt`, `audit-optimization.json`, `audit-optimization.txt`; `ui_finalissima/smoke.txt` e immagini; `audit-summary.json` con hash dei prezzari e dei corpus. Gli hash identificano esattamente i documenti e i risultati usati, senza dipendere da eventuali aggiornamenti successivi delle pagine online.

### 10. Cosa affinare sul ponte reale

Prima di usare la soluzione come base progettuale servono almeno: geometria reale di impalcato e spalle, schema statico e fasi, inviluppi di traffico, azioni orizzontali, verifiche di sezioni/collegamenti, modello geotecnico e cedimenti, prestazioni di appoggi/giunti, dettagli costruttivi e computo territoriale completo. Per i ponti speciali occorrono analisi dedicate di stabilità, deformabilità, fatica e vento. Il modulo può aiutare a scegliere quali alternative approfondire; non sostituisce questi passaggi.


## TEORICA A07 — Bridge Design — sezioni tecniche e ottimizzazione

Aggiornamento del 27 settembre 2026. Questa guida descrive le quote adottate dal modello e il comando **Ottimizza**. L'obiettivo è scegliere una configurazione ragionevole da approfondire, a parità di sito, larghezza e listino. Il risultato resta un predimensionamento: non è una verifica normativa e non dimostra la realizzabilità di un progetto esecutivo.

### Procedura pratica

1. Aprire **Bridge Design** e inserire lunghezza totale, quota sul terreno, ostacolo e terreno convenzionale. Controllare corsie, banchine, spartitraffico e barriere: questi dati definiscono la larghezza e restano costanti nella ricerca.
2. Controllare i **Prezzi unitari**. Il motore usa questi prezzi anche durante l'ottimizzazione. Un cassone può risultare conveniente con un listino e meno conveniente con un altro; i valori iniziali sono convenzionali, non un'offerta di impresa.
3. Premere **Ottimizza** sopra il disegno oppure aprire la scheda principale **Ottimizzazione**, accanto a **Progetto**. La ricerca ha uno spazio autonomo, separato dalle tabelle dei risultati del ponte.
4. Scegliere **Costo minimo**, **CO₂ minima** o **Compromesso costo / CO₂**. Selezionare le caselle delle scelte da mantenere costanti.
5. Definire il numero minimo e massimo di campate. **Suggerisci campate dalla lunghezza** ricava un intervallo compatibile con le luci usuali delle famiglie libere, fra 1 e 30 campate. È una prima selezione basata sulla luce media: il calcolo controllerà le luci effettive e gli ostacoli. Impostare eventualmente altezza minima in campata e massima anche sulle pile; zero significa nessun limite. Definire le griglie percentuali descritte sotto.
6. Premere **Avvia ottimizzazione**. La preview segue il migliore provvisorio senza modificare il progetto. I grafici si popolano durante il calcolo. **Interrompi** annulla la ricerca senza applicare una soluzione.
7. Leggere graduatoria, differenze rispetto al ponte corrente e motivi di esclusione. Impostare **Mostra le prime N** e premere **Aggiorna elenco**: la graduatoria completa è già conservata, non si ripete il calcolo. Selezionare una riga oppure un punto nella nuvola; esaminare prospetto, sezione e tabella **Corrente / Selezionata / Δ**, quindi premere **Applica soluzione selezionata**.
8. La scheda **Sezioni e quote** mostra le dimensioni della soluzione applicata. **Annulla** ripristina gli input precedenti. Se non esiste già un confronto A, l'applicazione salva come A la configurazione di partenza; un'A già presente viene conservata.

La ricerca viene invalidata quando cambiano geometria, prezzi o coefficienti. Anche cambiare obiettivo, caselle o limiti richiede una nuova ricerca. Il paesaggio e il confronto A non modificano gli indicatori della configurazione corrente.

### Cosa viene mantenuto costante

Restano sempre invariati lunghezza, quota, ostacolo, terreno, composizione della piattaforma stradale, resistenze del calcestruzzo, estremi su pila o spalla, coefficienti ambientali, carichi, ipotesi e listino. La ricerca non riduce le corsie e non sceglie materiali meno resistenti per ottenere un costo minore.

| Casella | Effetto |
|---|---|
| Mantieni tipologia | Limita la ricerca alla famiglia corrente. |
| Mantieni numero campate | Conserva il numero attualmente adottato, anche se l'input era zero. I limiti min/max devono comunque comprenderlo. |
| Mantieni altezza in campata | Fissa il valore attualmente adottato. Per il cassone variabile l'altezza sulle pile segue ancora la relativa regola del modello. |
| Mantieni dimensioni della sezione | Conserva i parametri trasversali e risolve nel valore corrente quelli automatici. Richiede la tipologia bloccata; la casella la seleziona automaticamente. L'altezza totale ha una casella separata. |
| Mantieni continuità | Conserva lo schema continuo oppure a campate indipendenti. |
| Mantieni schema pila e valori imposti | Conserva tipologia e diametro/spessore eventualmente imposto. Se il diametro/spessore è zero, il dimensionamento automatico resta attivo. |
| Mantieni schema fondazione e lunghezza pali | Conserva il tipo effettivamente adottato e la lunghezza dei pali. Numero pali e dimensione plinto esplicitamente imposti restano identici; quelli a zero vengono ricalcolati per le reazioni della nuova soluzione. |

Bloccare lo schema di fondazione non significa congelare una distinta diversa per ogni appoggio. Il modello dispone di parametri comuni e di dimensioni automatiche per appoggio; la ricerca rispetta questa rappresentazione. Per mantenere un numero o una dimensione, inserirli esplicitamente prima della ricerca.

### Quali soluzioni sono esplorate

La ricerca è discreta e deterministica. Con gli stessi input e vincoli produce lo stesso ordine dei risultati. Include la configurazione corrente, purché rispetti i filtri, e combina:

- le quattordici famiglie, oppure la sola famiglia bloccata;
- i numeri interi di campate nel campo scelto, da 1 a 30; inizialmente il massimo è 12;
- altezza automatica e una variante aumentata del 15% nella griglia iniziale, modificabile come descritto sotto, oppure l'altezza bloccata;
- continuità o campate indipendenti, se la continuità è libera;
- quattro schemi di pila, se lo schema è libero;
- fondazione diretta, pali da 1,0 m o pali da 1,5 m, se la fondazione è libera;
- per i pali liberi, lunghezza convenzionale del terreno e aumenti del 25% e del 50% nella griglia iniziale, modificabile come descritto sotto, entro 80 m.

Se le dimensioni della sezione sono libere, il motore usa i parametri standard della famiglia. Non prova una griglia di ogni possibile spessore, interasse, inclinazione o resistenza. In particolare non riduce le lamiere fino a una presunta resistenza limite: il modello non comprende le verifiche necessarie per farlo. Il messaggio «migliore soluzione» significa quindi migliore fra le combinazioni effettivamente esplorate.

Le rappresentazioni automatica e numerica della medesima geometria possono generare due combinazioni di input. La graduatoria elimina queste ripetizioni. Il conteggio delle combinazioni ammesse precede la deduplicazione della lista presentata.

### Intervalli di ricerca modificabili

Le griglie iniziali sono 100–115% con passo 15 punti percentuali per l'altezza e 100–150% con passo 25 punti per i pali. Non sono intervalli di confidenza: definiscono precisamente quali varianti vengono provate. L'utente può modificarle nell'intervallo 100–200%, con un massimo di 11 valori per griglia. Il valore massimo viene sempre incluso, anche se il passo non divide esattamente l'intervallo: 100–115 con passo 10 produce 100, 110 e 115%.

Per ogni famiglia, schema e numero di campate, il 100% dell'altezza corrisponde alla quota ricavata dalla regola di predimensionamento. Le varianti maggiorate sono arrotondate verso l'alto a multipli di 5 cm. Ad esempio, con altezza automatica 1,83 m e griglia 100, 110, 120%, le quote provate sono 1,83, 2,05 e 2,20 m. La griglia non scende sotto la quota di riferimento: il modello non comprende le verifiche necessarie per cercare una sezione più snella della regola adottata. Se l'altezza è bloccata, i campi percentuali vengono disattivati e si conserva la quota del progetto.

Per i pali, il 100% è la lunghezza convenzionale associata al terreno nel modello: 10, 15, 22 o 30 m. La percentuale modifica questa lunghezza per le due famiglie di fondazioni su pali. Per i plinti diretti non introduce varianti. Bloccando la fondazione si conserva la lunghezza adottata e la griglia viene disattivata. Non si sta calcolando una stratigrafia geotecnica ottima.

La configurazione corrente viene comunque aggiunta alla ricerca, anche se le sue quote non sono un punto delle griglie percentuali. Deve rispettare i limiti assoluti e tutti i filtri di ammissibilità. Questo consente di confrontare le proposte con un riferimento effettivo senza perdere una configurazione corrente già conveniente.

Prima del calcolo viene stimato il numero di tentativi della griglia, filtrando le coppie famiglia/numero di campate la cui luce media è incompatibile. Il limite è 50.000 tentativi, compreso il riferimento iniziale. Una griglia eccessiva viene rifiutata con un messaggio: restringere gli intervalli, aumentare i passi o bloccare alcune scelte. Il numero finale di calcoli può essere inferiore alla stima perché gli input duplicati non vengono ricalcolati.

### Grafico delle variazioni

L'asse orizzontale è il numero progressivo del tentativo realmente calcolato, non un numero di generazione di un algoritmo genetico. Il selettore cambia l'asse verticale fra costo, CO₂, altezza in campata, numero di campate, lunghezza pali, tipologia, schema di pila, fondazione e continuità. Le ultime quattro variabili sono categorie: la distanza verticale fra due categorie non ha significato numerico.

I punti colorati sono ammessi dai filtri; quelli grigi sono esclusi. Passando il mouse si leggono i dati del tentativo e i motivi di esclusione. Se un calcolo fallisce prima di produrre una quota, quella quota manca e non viene rappresentata come zero. Il tentativo resta nella traccia e nel conteggio delle esclusioni. Nei grafici di costo e CO₂, la linea scura mostra il minimo progressivo fra i tentativi ammessi: la sua unità rimane costante durante la ricerca.

La successione dei punti mostra l'ordine della griglia, non un percorso continuo fra progetti. Un salto del costo o dell'altezza può corrispondere a un cambio di tipologia o di schema. Non è necessariamente un peggioramento dell'ottimizzazione: la ricerca deve esplorare anche soluzioni meno efficienti per costruire il confronto.

### Nuvola delle soluzioni e alternative ordinate

Il secondo grafico usa costo in milioni di euro in orizzontale e CO₂ in tonnellate in verticale. A ricerca conclusa include tutte le geometrie distinte ammesse, anche quelle oltre le prime N righe. I colori distinguono le otto famiglie; un filtro permette di visualizzare una sola tipologia. La croce identifica il ponte corrente, la stella l'ottimo secondo l'obiettivo scelto e il cerchio scuro la soluzione selezionata. La selezione può quindi essere diversa dall'ottimo.

Gli anelli indicano la frontiera Pareto. Il filtro **Solo frontiera Pareto** conserva le alternative non dominate globalmente nella ricerca, anche quando si filtra una tipologia. Le linee che collegano i punti Pareto sono una guida visiva; non rappresentano progetti intermedi calcolati. Più geometrie possono avere lo stesso costo e la stessa CO₂ e apparire sovrapposte: la graduatoria consente di selezionarle singolarmente. Cliccando su punti coincidenti si seleziona il rango migliore fra quelli più vicini.

Durante il calcolo la nuvola mostra i tentativi ammessi fino a quel momento; la deduplicazione geometrica e la frontiera definitiva sono disponibili alla fine. Il migliore provvisorio può cambiare. Con il compromesso 50/50 cambiano anche i minimi usati nella normalizzazione man mano che la ricerca avanza: il punteggio provvisorio non va interpretato come un indicatore assoluto di convergenza.

La tabella ordina l'intera famiglia con lo stesso criterio del motore. N regola soltanto quante righe visualizzare, da 1 a 50.000, limitate alle soluzioni disponibili. Il punteggio viene ordinato prima dell'arrotondamento visualizzato. In caso di parità, si confrontano costo, CO₂, identificativo di famiglia e numero di campate; l'ordine residuo è quello deterministico di esplorazione. Il costo minimo è in euro, l'obiettivo ambientale in tonnellate e il compromesso è adimensionale.

Selezionare una soluzione aggiorna la preview e il confronto delle quote senza cambiare gli input del progetto, anche scegliendo un punto fuori dalla top N. La tabella include costi, CO₂, luci, schema, continuità e le dimensioni tecniche adottate. Il simbolo «n.a.» indica che una quota della famiglia selezionata non ha una controparte nella famiglia corrente; non significa zero.

### Aggiornamenti della preview

L'aggiornamento a ogni tentativo renderebbe difficile leggere il disegno. Il motore comunica il progresso in gruppi, circa ogni 200 ms. La UI aggiorna la preview solo quando cambia il migliore provvisorio e non più di due volte al secondo. Il risultato finale viene sempre mostrato. La ricerca rimane in un'attività separata dal thread grafico e può essere interrotta; un'interruzione cancella i risultati provvisori e non applica cambiamenti al ponte.

Cambiare progetto, prezzi o vincoli invalida risultati e preview. Il cambio di N, del parametro rappresentato, dei filtri del grafico o della vista prospetto/sezione non modifica la ricerca. Il disegno usa lo stesso componente grafico del progetto, mentre intervalli, tentativi, ammissibilità, punteggi e Pareto sono calcolati nella libreria indipendente dall'interfaccia.

### Filtri di ammissibilità

Una soluzione è esclusa se il calcolo non è possibile, se supera i limiti dell'utente o se non soddisfa i seguenti criteri orientativi:

- tutte le luci devono rientrare nel campo usuale della famiglia;
- l'altezza in campata non deve essere inferiore alla regola automatica della famiglia, anche quando è imposta manualmente;
- le pile interne devono rimanere fuori dall'ostacolo con il margine geometrico di 1 m per lato;
- rapporto massimo fra carico assiale e riferimento di fondazione non superiore a 1;
- snellezza convenzionale massima delle pile non superiore a 100;
- compressione media nelle pile non superiore a 0,30 della resistenza convenzionale del cls;
- non più di 64 pali per appoggio e plinto compatibile con la disposizione convenzionale a interasse 3 diametri;
- nessuna reazione verso l'alto, poiché i dispositivi antisollevamento non sono dimensionati;
- nessuna sovrapposizione trasversale delle travi a U o eccedenza dell'ingombro dei cassoni metallici.

Una configurazione può violare più criteri: le occorrenze dei motivi di esclusione non vanno sommate per ricavare il numero di soluzioni escluse. Se non rimane alcuna alternativa, la finestra presenta i motivi e non applica cambiamenti. Occorre valutare se liberare uno schema, aumentare l'altezza consentita o rivedere i dati di sito.

Questi filtri non sostituiscono resistenza a flessione/taglio, instabilità, fatica, esercizio, fasi costruttive, precompressione, sisma o geotecnica completa. Una soluzione ammessa è una proposta di studio, non una struttura dichiarata sicura.

### Criteri economici e ambientali

Il costo deriva dal computo del modello: somma delle quantità moltiplicate per i prezzi, maggiorata degli oneri di cantiere e degli imprevisti. Gli importi sono in EUR, IVA esclusa. Prezzi nulli sono consentiti dal modello e comportano costi nulli per le relative voci: devono essere una scelta consapevole, non dati dimenticati.

L'impronta comprende cls e acciai e la maggiorazione convenzionale per trasporti/cantiere. È un indicatore parziale, privo di EPD specifiche; non è l'impronta completa dell'intero ciclo di vita del ponte.

Nel compromesso, per ogni candidato si calcola:

`punteggio = 0,5 × costo / costo_minimo + 0,5 × CO₂ / CO₂_minima`

I due minimi sono ricavati dalle soluzioni ammesse della stessa ricerca. Il costo minimo al denominatore è limitato inferiormente a 1 EUR e la CO₂ minima a 10⁻⁹ t, così il calcolo resta definito anche con indicatori nulli. Il punteggio è adimensionale: inferiore significa migliore secondo il criterio scelto. Cambiare i vincoli può cambiare i minimi di normalizzazione; non confrontare punteggi di ricerche diverse come valori assoluti.

La colonna **Pareto** indica che nessun'altra soluzione ammessa presenta costo e CO₂ entrambi non maggiori, con almeno uno strettamente minore. Non introduce una verifica strutturale aggiuntiva. Una soluzione più economica può emettere più CO₂; la graduatoria rende visibile lo scambio attraverso le differenze rispetto al riferimento.

### Leggere sezioni e quote

La tabella della sezione riporta componente, simbolo, valore, unità, origine e significato. **Impostato** identifica un input esplicito, **Automatico** una regola del modello, **Derivato** una conseguenza degli input. Il valore visualizzato è adottato dal calcolo, anche quando nell'editor rimane zero per richiedere l'automatismo.

Le altezze `d` e `d_pila` comprendono la soletta. `h` è l'altezza della trave sotto soletta e rialzo. `h_w` è l'altezza netta dell'anima; `l_w` è il suo sviluppo reale quando è inclinata. Le quote di piattabande, anime e solette sono in mm; luci, larghezze e altezze generali in m. Il prospetto mantiene precisione numerica utile a ricostruire il computo, senza attribuire tale precisione alle stime di progetto.

Per i cassoni metallici, `H/4V = 1` significa inclinazione 1 orizzontale su 4 verticali. Lo scarto orizzontale è `Δx = h_w × H/4V / 4` e lo sviluppo dell'anima è `sqrt(h_w² + Δx²)`. La massa di carpenteria include un'aggiunta del 15% per diaframmi, irrigidimenti e collegamenti; questa aggiunta non aumenta la rigidezza flessionale.

Per le travi a I in c.a.p. le flange e l'anima sono rettangoli equivalenti del modello ANTHEA, non un profilo prefabbricato AASHTO selezionato da catalogo. Per i cassoni a conci, `d_eq = d + (d_pila − d)/3` è l'altezza media usata nelle quantità e nella rigidezza. Questa approssimazione non analizza le fasi a sbalzo.

La tabella delle campate associa a ciascuna luce gli assi degli appoggi iniziale e finale. Lo sviluppo `n × Li` è la lunghezza teorica degli elementi longitudinali: non comprende giunti costruttivi, sovrapposizioni, sfridi e pezzature di officina. L'interasse `s_rif` serve a ricavare il numero delle travi; non determina da solo la posizione esecutiva degli sbalzi laterali.

La tabella delle fondazioni riporta dimensioni per ciascun appoggio, non solo un valore medio. Le misure del plinto sono B longitudinale × W trasversale × t spessore. I pali sono numero × diametro × lunghezza. Le spalle sono equivalenti volumetrici: il modello non produce una carpenteria esecutiva di muri frontali, paraghiaia e muri d'ala.

**Esporta sezioni e quote CSV** salva questi dati con unità e significato. Il report Word singolo e quello di progetto includono gli stessi prospetti. L'esportazione delle quantità rimane disponibile per il computo economico.

### Confronto con il sito e verifiche del software

La campagna sul sito e i test del programma rispondono a domande diverse. I test interni verificano equilibrio, formule, geometrie, prezzi, vincoli e funzionamento della finestra. Il confronto live verifica invece come i risultati di ANTHEA differiscono da quelli effettivamente visualizzati dal sito.

Il dataset, gli input tradotti, gli output del motore, gli scostamenti e il report sono in `supporto/artefatti/bridge_design_site_1000`. Il motore non viene calibrato forzando costi o quantità sui risultati di riferimento. Le differenze fra famiglie, fondazioni, listini e perimetro delle opere provvisionali restano identificabili.

Per ripetere i test numerici e dell'interfaccia:

```powershell
dotnet run --project supporto/test/BridgeDesign.Checks -c Release -- supporto/artefatti/bridge_optimization_explorer/calcoli
dotnet build X.Desktop/X.Desktop.csproj -c Release --no-restore
dotnet X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll --smoke-bridge-design supporto/artefatti/bridge_optimization_explorer/ui
```


### Estensione alle strutture speciali e audit

Arco con catena e reticolare usano campate indipendenti. Strallato e sospeso richiedono tre campate continue simmetriche L/4 + L/2 + L/4 e antenne dedicate. Un vincolo di continuità o schema pila può quindi escludere intere famiglie: per confrontarle occorre liberare le relative scelte. La ricerca non varia automaticamente la freccia dell'arco, la geometria dei cavi, l'altezza delle antenne o la scelta dell'impalcato cls/ortotropo: questi dati restano quelli impostati.

Le fondazioni automatiche ora includono il peso dei plinti nella selezione di dimensioni e numero pali. I fusti automatici rispettano le soglie convenzionali assiali e di snellezza; sono escluse anche antenne manuali oltre la tensione di riferimento e fondazioni che non contengono il fusto. Nessuno di questi filtri sostituisce le verifiche strutturali/geotecniche.

Il [report del 27 settembre 2026](guida-teorica-anthea.md) riporta tutte le prove finali, comprese ricerche libere sulle 14 famiglie, enumerazione indipendente di griglie ristrette e il caso limite con indicatori nulli.


## TEORICA A08 — Bridge Design

Modulo `str_bridge_design`, introdotto il 26 settembre 2026. Predimensionamento parametrico indipendente dall'interfaccia, integrato nel catalogo Strutture, nel menu File, nei fogli e nei report di progetto.

### Riferimento e ambito

Riferimento funzionale osservato nel browser: https://thebridgeeng.com/design. Sono stati esplorati i quattro gruppi di input, le otto famiglie, gli automatismi delle campate, le sezioni, le pile, i confronti e le stime. Fra i riscontri: larghezza iniziale 20,2 m, campate terminali continue pari a 0,8 delle interne, intervalli usuali delle famiglie e altezza indicativa delle sezioni a 45 m. Questi riscontri non dimostrano identità dei motori.

La versione ANTHEA riproduce il flusso con grafica nativa WPF e viste vettoriali proprie. Il dettaglio AASHTO del sito non è replicato: viene fornita un'analisi esplicita della trave sotto carichi uniformi, con quantità e stime. Non costituisce una verifica NTC, EC o AASHTO. Il listino è in EUR ed è modificabile; i valori iniziali sono convenzionali.

### Organizzazione del codice

- `X.Calculations/BridgeConcept.cs`: schema, famiglie, valori iniziali, validazione, contratti dei risultati.
- `X.Calculations/BridgeConcept.Calculation.cs`: dimensioni, quantità, trave continua, fondazioni, costi, CO₂ e avvisi. Nessuna dipendenza da WPF e nessuna modifica dell'input.
- `X.Calculations/BridgeConcept.Technical.cs`: prospetti delle dimensioni adottate, campate e fondazioni.
- `X.Calculations/BridgeConcept.Optimization.cs`: ricerca discreta vincolata per costo, CO₂ o compromesso, indipendente dalla UI.
- `X.Desktop/Wpf/BridgeDesignWorkspace.cs`: editor, ricalcolo, confronto A/B, cronologia, suggerimenti ed esportazioni.
- `X.Desktop/Wpf/BridgeDesignTechnical.cs` e `BridgeDesignOptimization.cs`: quote tecniche, scelta dei vincoli, graduatoria e applicazione delle alternative.
- `X.Desktop/Wpf/BridgeDesignDrawing.cs`: prospetto e sezione schematica, diagramma del momento, immagini delle famiglie.
- `X.Core/BridgeConceptExport.cs`: CSV e report Word, riusati dal report di progetto.

I valori automatici sono richiesti con zero nei campi che lo dichiarano e restano zero nell'archivio. I risultati contengono i valori effettivamente adottati. Il confronto A conserva una copia dei dati, del listino e delle ipotesi; non contiene riferimenti mutabili allo stato B.

### Modello

La [guida a sezioni tecniche e ottimizzazione](guida-teorica-anthea.md) descrive la scheda principale **Ottimizzazione**, i parametri bloccabili, gli intervalli percentuali, la traccia dei tentativi, la nuvola costo–CO₂ con frontiera Pareto e la graduatoria delle prime N soluzioni. La preview segue il migliore provvisorio e consente poi di confrontare le alternative senza modificare il progetto. I report e il CSV tecnico usano gli stessi prospetti dimensionali della finestra.

Le formule, i coefficienti e gli esempi sono descritti nel capitolo Bridge Design della [guida teorica](guida-teorica-anthea.md). Il capitolo corrispondente della [guida pratica](guida-pratica-anthea.md) descrive tutti i comandi.

I principali limiti sono: geometrie ideali, EI costante e lordo, carico uniforme su tutte le campate, assenza di inviluppo mobile, armature per incidenza, geotecnica convenzionale, azioni soltanto verticali, carbonio parziale senza EPD e durata senza cronoprogramma. I prezzi dei pali includono cls e perforazione, con armatura separata. Le famiglie fuori dalle luci usuali sono segnalate. La disposizione rispetto all'ostacolo è un tentativo geometrico limitato e può restare irrisolta con avviso.

### Controlli riproducibili

```powershell
dotnet run --project supporto/test/BridgeDesign.Checks -c Release -- supporto/artefatti/bridge_design/calcoli
dotnet build X.Desktop/X.Desktop.csproj -c Release --no-restore
dotnet X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll --smoke-bridge-design supporto/artefatti/bridge_design/interfaccia
```

La suite numerica controlla formule chiuse di travi appoggiate e continue, equilibrio di 200 travi diseguali, 224 combinazioni di famiglie/terreni/pile, proprietà di indipendenza del listino e del paesaggio, quantità, input invalidi, file, baseline e struttura degli export. Sono inclusi determinismo della ricerca, rispetto dei vincoli, costo e CO₂, cancellazione, assenza di candidati, variazioni dei prezzi e ricostruzione delle quantità dalle quote tecniche. La suite desktop esercita quattro tab di input, quattordici famiglie, sei tab dei risultati e la scheda principale Ottimizzazione, undo/listino, invalidazione, A/B, auto, random, PNG, archivio, Word singolo e di progetto, ricerca/applicazione/annullamento e layout a 1600/1366/960/780 pixel.

La campagna live di confronto con il sito è separata dai test interni: sorgenti in `supporto/test/BridgeDesign.SiteComparison`, dati e report in `supporto/artefatti/bridge_design_site_1000`. Un test interno superato non dimostra equivalenza con il sito.

Gli output sono sotto `supporto/artefatti/bridge_design`. Per le guide i sorgenti mantenibili sono i due Markdown in `supporto/docs`, il builder è `supporto/scripts/Build-AntheaGuides.py` e i Word finali sono sotto `supporto/documentazione/Guide_ANTHEA`. Il file di esempio `supporto/esempi/bridge-design.anthea` contiene anche un confronto A e un prezzo modificato.


### Audit e famiglie aggiuntive — 27 settembre 2026

Il catalogo comprende ora 14 famiglie: alle otto iniziali si aggiungono travi incorporate, cassone ortotropo, arco metallico con catena, strallato, sospeso e reticolare. Per le strutture superiori il motore usa equilibri e aree convenzionali dedicati, non i diagrammi della trave ordinaria. I file `BridgeConcept.Advanced*.cs` ne separano schema, impalcato e struttura principale.

Il [report dell'audit generale](guida-teorica-anthea.md) documenta prezzi, formule, correzioni, limiti, 1.008 configurazioni, regressione dei 2.000 input del sito e test finali di ottimizzazione. `BridgeConcept.Foundation.cs` include il peso del plinto nell'autodimensionamento e `BridgeConcept.Pricing.cs` espone i riferimenti economici. Le nuove tariffe iniziali sono calibrate su riscontri ANAS/RER 2026; i prezzi salvati negli archivi restano invariati. La finestra dispone di un caricamento esplicito dei valori orientativi, annullabile.


## TEORICA A09 — Validazione — taglio e fessurazione CA

Data: 28 settembre 2026. Sette profili normativi, cinque geometrie del catalogo.
[Matrice di copertura e parti ancora mancanti](guida-teorica-anthea.md).

### Confronti indipendenti

`supporto/test/ConcreteCode.Checks/reference.py` esegue funzioni Python pubblicate
da **fib StructuralCodes**, fissate al commit
`3e9c3f5cffb0c28e083257346006c7eac02384fd`. Non richiama ANTHEA e non ricava gli
attesi dal C#. Via AST carica solo funzioni e import standard necessari, lasciando
invariati i corpi matematici. Non usa le funzioni che richiedono SciPy.
URL e SHA256 dei sorgenti sono conservati in `reference.json`.

I 72 casi indipendenti comprendono 48 casi di taglio EC2/MC: quattro resistenze
fck (20, 35, 70, 80 MPa), N compresso/nullo/teso, con/senza staffe. I 24 casi di wk
EC2 variano durata, aderenza, interasse e tensione. Tolleranza C#: 10⁻⁹ moltiplicata
per max(1, |atteso|), numerica e non interpretabile come margine di sicurezza.

- [fib: taglio EC2](https://fib-international.github.io/structuralcodes/api/codes/ec2_2004/shear.html).
- [fib: fessurazione EC2](https://fib-international.github.io/structuralcodes/api/codes/ec2_2004/cracks.html).
- [fib: taglio MC2010](https://fib-international.github.io/structuralcodes/api/codes/mc2010/shear.html).

Il benchmark [SOFiSTiK DCE-EN6](https://docs.sofistik.com/2026/en/verification/_static/verification/pdf/dce-en6.pdf)
fornisce un confronto DIN esterno: b=300 mm, d=450 mm, z=384 mm, fcd=17 MPa,
ν1=0,75, VRd,max=734,4 kN per cotθ=1. Con VEd=343,25 kN il test rifiuta cotθ=2,
oltre il limite dipendente dal carico. Si confrontano questi valori locali;
non si dichiara riprodotto l'intero modello della trave SOFiSTiK.

Per DS, NS, UNI e fessurazione MC/DIN si usano confronti a formula chiusa,
controlli dei limiti e prove di integrazione. Non è disponibile un secondo
software indipendente per ogni variante. Fonti consultate:

- [Appendice italiana DM 31/07/2012, Allegato Eurocodice 2](https://www.gazzettaufficiale.it/atto/serie_generale/caricaArticolo?art.codiceRedazionale=13A02562&art.dataPubblicazioneGazzetta=2013-03-27&art.flagTipoArticolo=3&art.idArticolo=1&art.idGruppo=0&art.idSottoArticolo=1&art.idSottoArticolo1=10&art.progressivo=0&art.versione=1): consultate anche le immagini originali, pagine 82–85.
- [DS/EN 1992-1-1 DK NA:2024](https://www.bygningsreglementet.dk/media/rtdfjh4m/ds-en-1992-1-1-dk-na-2024_2024-07-01-a.pdf): documento normativo originale per taglio, k3 e sistemi di fessure.
- [SCIA: implementazione degli annessi](https://help.scia.net/19.1/en/krs/attachments/theory_na_en_1992_enu.pdf): riscontro DIN e NS; documentazione dell'implementatore, distinta dal testo originale degli annessi.
- [SCIA: NS NA:2010](https://help.scia.net/25.0/en/national_annexes/en1992/norway.htm): riscontro norvegese di combinazioni e coefficienti.
- [Terjesen et al., 2024](https://onlinelibrary.wiley.com/doi/full/10.1002/suco.202300367): confronto pubblicato dei modelli di fessurazione. Non sono stati riutilizzati i dati sperimentali dell'articolo per validare il modulo.

### Altre prove

Taglio: inversione V/M, carico nullo, ricerca automatica di cotθ confrontata con
scansione dell'intervallo, staffe inclinate, dati non finiti, granulometria e forte
trazione NS, αcw UNI del c.a. non precompresso.

Fessurazione: limite DIN della distanza tra fessure, barre distanti, durata,
coefficiente DS del copriferro, profondità efficace calcolabile a mano,
compressione totale, trazione uniforme, sistemi DS fine/grossolano, superfici
interne armate e coerenza tra inviluppo e riepilogo. Le geometrie vengono
esercitate con tutti i profili; sui cerchi il modello di taglio è esplicito.

WPF: persistenza Mx/My nel taglio (il precedente Commit li eliminava), modifica
Mx → aggiornamento εx per Vy nel MC, invalidazione da dg comune, scambio Excel,
salvataggio/riapertura, selezione DS, aggiornamento viste e report.

### Riproduzione

Da radice repository, con Python 3 e .NET 8:

```powershell
python supporto/test/ConcreteCode.Checks/reference.py --download
dotnet run --project supporto/test/ConcreteCode.Checks -c Release
dotnet run --project supporto/test/X.Verifiche -c Release -- --checker
dotnet run --project supporto/test/X.Verifiche -c Release -- --ca-module
dotnet build X.Desktop/X.Desktop.csproj -c Release
./supporto/scripts/Test-CalculationUi.ps1 -Suites ca-features,ca-extensions
```

Il JSON degli attesi è versionato; la suite C# non richiede rete né Python.
La rigenerazione scarica i tre sorgenti fissati in
`supporto/artefatti/ca-normative/fonti`. Controllare il diff prima di accettare
variazioni degli attesi. Log, schermate e report generati stanno sotto
`supporto/artefatti/ca-normative/` e non sono versionati.

| Gruppo | Risultato |
| --- | --- |
| Normative CA, riferimenti e geometrie | 434 controlli superati |
| Checker / Excel / estensioni / dati | 102 + 19 + 174 + 27 superati |
| Modulo CA ampliato | 78 superati |
| WPF funzionalità / estensioni | 143 + 94 superati |
| WPF foglio completo | 201 superati, attestazione finale presente |
| Compilazione integrata | Release: 0 errori, 0 avvisi |

Le prime prove isolate usano un'esportazione della base 91f1061 con i file CA
sovrapposti, separata dalle lavorazioni simultanee sul ponte. La compilazione
integrata verifica anche le DLL aggiornate successivamente nel repository.
I risultati dimostrano i confronti elencati; non attestano l'intero contenuto
degli annessi, né validazione sperimentale del comportamento reale delle fessure.


## TEORICA A10 — Estensioni del modulo CA · settembre 2026

La validazione precedente è assunta come riferimento, secondo l'indicazione del
progettista. I test qui descritti verificano le estensioni e le regressioni del
software; non modificano gli esempi della relazione di validazione.

### Uso delle sette schede

1. **Pannello di controllo**: dati comuni di geometria, materiali, armature e
   staffe. Rettangolare e circolare possono avere un foro centrale della stessa
   forma. La circolare usa **32 lati per contorno** come default, configurabili
   in **Geometria → Lati del contorno**: multipli di 4 fra 12 e 720, uguali per
   contorno esterno e foro. I multipli di 4 mantengono vertici sui due assi e
   quindi il diametro assegnato in entrambe le direzioni. Il valore viene
   salvato nel foglio, indicato nelle proprietà e nel report; i fogli privi del
   parametro adottano 32. La modifica ricostruisce la geometria del verificatore.
   Questo parametro è distinto dalle direzioni angolari del dominio.
   Le dimensioni interne devono essere inferiori alle esterne. Il motore
   esclude il foro da area, inerzie, mesh e integrazione; impedisce barre nel
   vuoto o che ne intersecano il bordo. L'anteprima mostra foro e quote interne.
2. **Dominio 3D** e **Dominio 2D**: nella vista **Sezione σ / ε** si sceglie
   **Azione inserita** oppure **Punto limite**. Sono disponibili mappa, piano
   delle deformazioni e valori copiabili per barre e vertici. Il punto limite
   usa il piano già ottenuto dalla ricerca di resistenza; non viene cercato di
   nuovo. L'equilibrio di Ed è calcolato al primo accesso e memorizzato per la
   combinazione corrente. Il piano usa coordinate geometriche, ε positivo a
   trazione, coefficienti in 1/mm e quote ε in ‰. Le mappe stanno nella finestra;
   il comando **Espandi** rimane disponibile.
3. **Tensioni e fessurazione**: il selettore delle zone efficaci evidenzia area e
   barre considerate. I passaggi indicano Ac,eff, As,eff, ρeff, Øeq, copriferro,
   interasse, coefficienti, distanza tra fessure, differenza di deformazione e
   apertura. Il ramo interamente teso valuta separatamente le quattro facce
   rettangolari; per la circolare valuta fasce radiali nelle direzioni delle
   barre e del gradiente. Governa l'apertura massima, senza sommare aree di facce
   diverse. k2 = (εmax + εmin)/(2 εmax). Restano esclusi wk da analisi non lineare
   e l'aderenza specifica dei trefoli. Dal 28 settembre le superfici dei fori
   hanno controlli indipendenti su fasce interne di parete/anello: quando sono
   tese e prive di armatura efficace, l'esito resta incompleto. L'inviluppo
   comprende aperture esterne e interne; dettagli e limiti nella
   [matrice delle normative](guida-teorica-anthea.md).
4. **Taglio e torsione**: N, Mx, My, Vx, Vy e T nella stessa combinazione; momenti in kNm.
   Mx/My sono conservati anche in Excel e servono al taglio MC2010 livello II.
   Il valore di default dei vecchi archivi è zero. La colonna T è disponibile
   anche nello scambio Excel, che continua ad accettare i vecchi file a sette
   colonne. As,l per torsione è la quota disponibile dopo pressoflessione, da
   assegnare esplicitamente; non si riutilizza automaticamente tutta l'armatura.
5. **Dettagli costruttivi**: scegliere Trave, Pilastro, Soletta piena o Parete.
   I dati comuni restano condivisi: non vengono copiate staffe o geometria.
   Una soletta può avere **Staffe presenti = No**: la geometria delle barre e
   il modello a taglio tengono conto della scelta. Ogni controllo riporta
   valore, limite, formula/riferimento ed esito; i dati non ricavabili dalla
   sezione producono **Da completare**.
6. **Momento–curvatura**: N costante, direzione del momento, numero di passi,
   campionamento uniforme/quadratico, frazione finale di MRd, trazione del CLS,
   discretizzazione del dominio, residuo N ammesso al limite e raffinamento del
   primo snervamento. Usa i materiali comuni. Avvio e interruzione sono espliciti;
   le modifiche invalidano il risultato. Curva e punti sono esportabili in CSV,
   JSON e report. Il ramo è a momento crescente fino al limite plastico: nessun
   ramo discendente. La curvatura è il modulo del gradiente; nelle sezioni
   asimmetriche la sua direzione può differire da quella del momento.

### Schematizzazione resistente

Il taglio circolare richiede una scelta esplicita: **Pile · NTC §7.9.5.2**
(z/d = 0,75 per piena e 0,60 per cava) oppure **Parametri assegnati**. Il modello
delle pile è un'opzione del capitolo 7, non una regola generale del capitolo 4.
bw e d si possono assegnare scegliendo **Parametri geometrici = Manuali**; z/d è
modificabile nel modello assegnato. I suggerimenti geometrici sono bw = D nella
piena e D−Di nella cava, d dal baricentro delle barre dei due semicerchi,
assumendo il valore minore. I rami resistenti a Vx e Vy sono espliciti.

La torsione usa la sezione tubolare equivalente con Ak, uk e t esposti nel
dettaglio. NTC §4.1.2.3.6: TRd è il minimo delle resistenze del puntone, delle
staffe (area di un ramo) e dell'armatura longitudinale. Sono richieste staffe
chiuse a 90°, conferma della disposizione periferica e cot θ comune al taglio.
Per T+V si controllano puntone e armature. La somma
|T|/TRcd + |Vx|/VRcd,x + |Vy|/VRcd,y è un'estensione conservativa esplicita del
controllo monoassiale, non una formula normativa biassiale autonoma. Profili a T,
generici, spirali e precompressione non vengono assimilati automaticamente a
questo modello di torsione.

### Dettagli costruttivi e ancoraggio

Sono disponibili interferro e copriferro, minimi/massimi longitudinali,
diametri e passi di staffatura, armatura secondaria e interassi per solette e
pareti. Si verificano anche le distanze geometriche delle barre dal foro.
cmin,dur è assegnato dal progettista e predisposto per il collegamento alla
durabilità del modulo Materiali; vale per tutte le superfici della sezione.
Per trave/soletta i controlli delle due zone longitudinali usano le due metà
della sezione, rendendo esplicita una possibile inversione di momento.

I controlli locali degli appoggi, la traslazione delle trazioni, le legature,
le distribuzioni fra facce e il punzonamento richiedono informazioni
dell'elemento: sono indicati quando non determinabili dai dati di sezione.
Il modulo non dichiara una conformità globale se tali verifiche sono pendenti.
La gerarchia sismica del capitolo 7 non fa parte di questi dettagli.

Ancoraggi e sovrapposizioni rettilinee hanno dati separati in `ancoraggi`,
con `schema_versione = 1`. Il servizio espone fbd, lb,rqd, lunghezza richiesta
e confronto con la disponibile. Sono assunti α1…α5 = 1, senza riduzioni
favorevoli per confinamento o piegature; per le sovrapposizioni si considera
α6 e l'interferro. I dettagli esecutivi e le cautele per Ø > 32 mm restano da
verificare. Il medesimo contratto può essere richiamato da una futura scheda
dedicata, senza duplicare la formula.

### Separazione del calcolo e prove

`X.Core` contiene i contratti `IConcreteDetailingCalculator`,
`IConcreteAnchorageCalculator`, `IConcreteTorsionCalculator` e
`IMomentCurvatureCalculator`, i rispettivi input/output e l'integrazione delle
zone efficaci. Non dipendono da WPF. L'adattatore `CheckerSection` resta l'unico
collegamento all'equilibrio e ai domini della DLL; la curva riceve le funzioni
di equilibrio/resistenza. Questi confini preparano il trasferimento nella
libreria di calcolo futura. Le DLL distribuite in `lib/Checker` non sono
modificate da questa estensione.

```powershell
dotnet run --project supporto/test/X.Verifiche -c Release -- --ca-module
dotnet run --project supporto/test/X.Verifiche -c Release -- --checker
X.Desktop/bin/Release/net8.0-windows/ANTHEA.exe --smoke-ca-extensions supporto/artefatti/ca_extensions/ui
X.Desktop/bin/Release/net8.0-windows/ANTHEA.exe --smoke-ca-features supporto/artefatti/ca_extensions/features
dotnet run --project supporto/test/X.Verifiche -c Release -- --ca-benchmark supporto/artefatti/ca_extensions/benchmark
```

Il benchmark conserva input, opzioni, versioni e SHA-256 delle DLL, runtime,
tempi e allocazioni per rettangolare/circolare piena/cava. Misura preparazione,
dominio 3D, 24 verifiche, tensioni lineari/non lineari, fessurazione e curva.
Run 0 è il primo uso del caso; le tre ripetizioni successive hanno mediana,
minimo e massimo separati. Confrontare le stesse condizioni della macchina e
non eseguirlo contemporaneamente ad altri calcoli. I checksum servono a
individuare cambiamenti numerici, non sostituiscono la verifica dei risultati.

### Riferimenti

- [NTC 2018, testo ufficiale](https://www.gazzettaufficiale.it/eli/gu/2018/02/20/42/so/8/sg/pdf):
  §§4.1.2.3.5–6, 4.1.2.3.10, 4.1.6.1 e, per il modello circolare scelto, §7.9.5.2.
- Circolare 21 gennaio 2019 n. 7, §C4.1.2.2.4.5: fessurazione.
- [JRC, Eurocode 2 worked examples](https://eurocodes.jrc.ec.europa.eu/doc/1110_WS_EC2/report/1110_WS_EC2.pdf):
  integrazioni EC2 per copriferro, aderenza, ancoraggi, solette e pareti.

Restano rimandati il progetto automatico delle armature, i contorni poligonali
generici e la fessurazione non lineare, come richiesto.

### Esito dei controlli di questa estensione

- 52 controlli mirati del nuovo modulo: piani limite plastico/elastico,
  equilibrio della curva e primo snervamento, geometria cava, copriferro,
  fessurazione tesa, ancoraggi, torsione, scambio Excel e discretizzazione
  circolare configurabile (proprietà confrontate con il poligono analitico).
- 294 controlli precedenti del motore e dello scambio dati.
- 19 controlli integrati WPF delle estensioni (inclusi modello circolare,
  riapertura ed esportazioni) e 137 controlli WPF dei flussi precedenti.
- Schermate controllate: mappe Ed/Rd, piano ε, curve, dettagli costruttivi,
  geometrie cave, torsione e zone efficaci.

Il primo benchmark, eseguito con il precedente contorno circolare a 180 lati,
è in `supporto/tmp/ca_extensions/benchmark_baseline/benchmark.json`
e `.csv`. Mediane in millisecondi, tre ripetizioni dopo il primo uso:

| Caso | Preparazione | Dominio 3D | 24 verifiche | Curva 30 passi |
| --- | ---: | ---: | ---: | ---: |
| Rettangolare | 46,50 | 34,58 | 19,60 | 77,17 |
| Rettangolare cava | 44,22 | 29,91 | 15,46 | 73,48 |
| Circolare | 1144,08 | 167,79 | 205,15 | 665,14 |
| Circolare cava | 5705,77 | 717,49 | 923,39 | 2895,73 |

Questi tempi descrivono i casi registrati su questa macchina, non prestazioni
garantite per sezioni diverse. Le prime misure indicano la preparazione della
circolare cava come fase prioritaria da confrontare dopo l'aggiornamento DLL.

### Dettagli per argomento e resistenze rapide

I dettagli costruttivi sono disposti in fasce orizzontali: interferro/copriferro, armatura, staffe, ancoraggi/appoggi. Ogni fascia affianca dati e verifiche; copriferro e staffe condividono i dati con le altre schede.

Nel pannello di controllo, sopra il riepilogo delle verifiche, il riquadro delle resistenze accetta N (compressione negativa) e la scelta elastico/plastico. Calcola i quattro momenti con segno Mx+, Mx−, My+, My− negli assi locali, mediante ricerche iterative dirette a N costante. Non genera un dominio; N o direzioni non risolti restano senza valore e riportano il motivo. La modifica di questi input non rigenera i domini della sezione.

Le linee di verifica 3D conservano le componenti fissate dal criterio: origine (N,0,0) a N costante; (0,0,0) a eccentricità costante; (N,Mx,0), (N,0,My), (0,Mx,My) per gli altri vincoli. Il tratto dall’origine all’azione è visibile anche sopra la superficie del dominio.

Correzione ancoraggi e durabilità (24 settembre 2026): cmin,dur ora deriva dall'esposizione SLE condivisa usando Materiali.NtcCover e MinimumConcrete, con vita utile 50/100 anni e controllo qualità espliciti. Esposizione mancante lascia pendente il solo copriferro. Ancoraggio rettilineo indipendente dai dati di giunzione; lunghezza vuota non produce un esito. Sovrapposizione con esiti distinti per lunghezza e interferro; coefficienti mostrati, fctk limitato a C60/75 per l'aderenza. Conservato il minimo NTC §4.1.2.3.10 (20Ø, 150 mm). Zona di giunzione riferita ai limiti di armatura della sezione, da modellare con tutte le barre sovrapposte. Dettaglio esecutivo sostituito da riscontri manuali dichiarati (confinamento, posizione/sfalsamento, cautele Ø>32), distinti dal calcolo numerico. Verificati 78 controlli del modulo e 82 controlli interfaccia; app pubblicata in app/.

Legami e allineamento delle schede: diagrammi CLS e acciaio selezionabili anche per classi predefinite, mantenendo nome e proprietà della classe. Il cambio di classe/normativa conserva il legame scelto; i materiali personalizzati applicano il proprio legame. Trefoli: legame predefinito modificabile per nuovi cavi e menu nella riga per i cavi esistenti. Divisori di ingresso, risultati e righe sincronizzati per gruppo e salvati nel foglio; rapporto iniziale verticale 30/70, risultati 60/40, righe 3,7/2. Selettore Rara/Frequente/Quasi permanente nella colonna dati, per allineare l'origine della vista SLE alle altre schede. Pubblicazione Release riuscita, 93 controlli interfaccia superati, comprese selezione legami nativi, conservazione classe, selettore SLE e materiale trefoli.


## TEORICA A11 — Aggiornamento SLE, geometria e trefoli

### Calcolo e cache

Le famiglie Rara, Frequente e Quasi permanente restano parallele. All'interno
di ciascuna famiglia le combinazioni sono distribuite su un massimo di due
worker, limitati in funzione dei processori disponibili. Ogni worker utilizza
il proprio solver; geometria e materiali condividono la sezione preparata.
L'inizializzazione dei solver resta serializzata perché la triangolazione
interna della DLL non è thread-safe.

Esposizione, sensibilità dell'armatura, durata, aderenza, copriferro per le
fessure e interasse non appartengono alla chiave della cache tensionale.
Cambiare questi dati aggiorna la fessurazione senza ripetere l'equilibrio SLE.
Per decompressione/formazione delle fessure rimane necessaria l'analisi
ausiliaria della sezione omogeneizzata interamente reagente, distinta
dall'analisi SLE principale.

### Interasse delle barre tese

Campo vuoto: automatico. Un valore positivo inserito dall'utente prevale.
La selezione delle barre è fatta per combinazione, usando il piano di
deformazione della DLL e l'area efficace già utilizzata dalla fessurazione.
Per le sezioni circolari si misura l'arco fra barre consecutive sulla corona.
Per rettangolari e T si considerano le coppie consecutive delle file
orizzontali e verticali, evitando collegamenti che attraversano i vuoti della
T. Si prende il massimo delle distanze valide, anche fra file allineate:
quest'ultima scelta può essere conservativa nelle disposizioni multistrato.
Non si sostituisce la spaziatura massima con il minimo del vicino più prossimo.
Se non esistono coppie idonee si richiede il dato manuale.

Il risultato riporta interasse e provenienza. La formula di apertura delle
fessure preesistente non è stata modificata. Riferimento: Circolare 21 gennaio
2019 n. 7, § C4.1.2.2.4.5 e figura C4.1.11.

Il contratto `ITensionBarSpacing`, la classe `TensionBarSpacing` e il punto di
iniezione `Ntc2018Checks.SpacingCalculator` separano la geometria dalla UI.
Un futuro adattatore Checker potrà sostituire questo servizio, convertendo
la geometria in ingresso nei tipi Geometry della libreria.

### Geometria e grafica

- Gli identificativi longitudinali sono B01, B02, … (senza troncamento oltre 99).
- La fila all'intradosso dell'ala della T ha numero, diametro e offset
  intradosso–asse barra; numero zero mantiene i vecchi fogli senza aggiunte.
  È distribuita sull'intera larghezza dell'ala. Copriferro, spessore e
  sovrapposizioni vengono controllati.
- Per le staffe circolari sono disponibili bracci interni paralleli e staffe
  chiuse interne sovrapposte, ruotabili. Nel primo schema uno/due bracci
  aggiuntivi corrispondono a tre/quattro braccia nella direzione scelta.
  Nel secondo schema il numero indica le staffe chiuse aggiuntive.
  Il disegno è indicativo; il modello resistente a taglio circolare si sceglie
  esplicitamente nella relativa scheda. Vedere le
  [estensioni del modulo](guida-teorica-anthea.md) per campo e ipotesi.
- Rosso indica compressione negativa, blu trazione positiva.
- Le scale di utilizzo distinguono cinque fasce: fino a 0,50; 0,50–0,70;
  0,70–0,90; 0,90–1,00; oltre 1,00. Legende verticali.
- “Tutti i punti resistenti” è indipendente dalla selezione delle azioni:
  rispetta i filtri della tabella e le righe visibili. “Resistenze” nasconde
  tutti i punti resistenti.

### Trefoli

Riferimento implementativo: CheckerUI, ModuleConcreteSection,
`MainViewModel.AddTendon`, tabella Tendons e pannello TendonMaterialView.
Un cavo con n trefoli viene rappresentato con Ø equivalente = Ø singolo × √n.
Il diametro singolo è quello equivalente all'area metallica, non il diametro
esterno nominale. La tabella conserva diametro equivalente del cavo, area
totale, coordinate, σp0 e proprietà del materiale; area e diametro si
aggiornano reciprocamente.

Ogni cavo può avere un materiale distinto e una legge elastoplastica o
incrudente. Il catalogo materiali è salvato nel foglio. Applicare un materiale
agisce sul cavo selezionato e sul predefinito dei nuovi inserimenti.
I vecchi record con area e proprietà esplicite restano leggibili.
L'interfaccia non aggiunge nuove verifiche CAP: apertura delle fessure con
trefoli e identificazione SLE dei trefoli con σp0 nullo conservano i limiti
espliciti già presenti.

### Dettaglio diagnostico della fessurazione

La scheda «Dettagli combinazione → Fessurazione · passaggi» e il report con
opzione dettagli condividono un riepilogo di massimo 30 valori, a due decimali
(notazione scientifica per valori molto piccoli): geometria efficace, materiali, coefficienti, deformazioni,
distanze fra fessure, apertura e tasso di lavoro. Ogni riga comprende unità e
una breve descrizione o formula, con riferimenti alla Circolare 2019
§ C4.1.2.2.4.5. Decompressione e formazione mostrano soltanto i valori pertinenti.
La selezione modifica esclusivamente la presentazione, non i calcoli.
Il testo resta selezionabile e copiabile; nel JSON rimane la traccia completa
non arrotondata, incluse le singole barre e i passaggi intermedi.

k₂ viene selezionato per ogni combinazione considerando tutte le armature
ordinarie, comprese quelle esterne alla fascia efficace: almeno una tensione
negativa determina k₂ = 0,50 (flessione); in assenza di barre compresse si usa
k₂ = 1,00 (trazione). Le barre a tensione esattamente nulla non sono compresse.
Il riepilogo riporta il criterio; i conteggi restano nel JSON. I limiti del modello di area
efficace per sezione interamente tesa rimangono espliciti.
Le altre scelte restano visibili: fct,eff è assunto uguale a fctm,
σs è il massimo sulle barre efficaci e
αe = Es/Ecm è distinto da n dell’analisi con viscosità. Queste sono assunzioni
del percorso implementato, da controllare nel confronto con altri calcoli;
il riepilogo non costituisce una nuova validazione normativa del metodo.

### Verifica

### Pannello, materiali e grafici (settembre 2026)

- Nuovi fogli: sezione rettangolare, staffe passo 200 mm, CLS C35/45 e barre
  B450C dal catalogo DLL; trefoli Y1860 predefiniti per i nuovi cavi.
  I fogli esistenti conservano i propri dati.
- Materiali salvati e standard sono immutabili nel pannello e nella tabella
  dei cavi. «Nuovo materiale» crea una copia modificabile, da salvare nel foglio;
  il database condiviso non è ancora implementato. NTC propone B450A/B450C;
  gli altri codici disponibili usano il catalogo EN 1992 (Model Code usa il suo
  catalogo CLS). Non esistono cataloghi specifici distinti per ogni annesso.
- Il materiale predefinito dei trefoli riguarda i nuovi cavi; il menu della
  singola riga assegna il materiale a quel cavo senza modificare gli altri.
- La modifica di x/y/Ø nella tabella barre salva una disposizione manuale,
  usata anche dal motore Checker. Il wizard torna autorevole solo con
  «Ripristina barre da wizard». Posizioni e sovrapposizioni restano validate;
  se le barre manuali di una sezione circolare non formano più un unico anello,
  la spaziatura automatica per arco non è applicabile: inserirla manualmente.
- «Proprietà sezione» riporta geometria CLS, proprietà omogeneizzate della DLL
  (sezione integra), quantità e diametri delle armature, area dei trefoli.
  Il comando «Proprietà / report…» è disponibile anche sopra la preview;
  la finestra consente copia del testo ed esportazione del report TXT.
  Si può impostare φ oppure n delle armature ordinarie: n = Es(1+φ)/Ecm,
  con φ ≥ 0. Per i trefoli è mostrato n relativo a ciascun Ep, a φ comune.
  Le opzioni sono salvate nel foglio ma non modificano le analisi SLE.
  Le proprietà sono calcolate dall'overload della DLL con φ, riutilizzando
  la stessa sezione preparata per la finestra.
- I dati del materiale dei trefoli sono campi dedicati (Ep, fpyk, fpk, εpu,
  diagramma), in sola lettura come i materiali CLS/acciaio.
  «Nuovo materiale» consente l'inserimento diretto di fck per il CLS oppure
  fyk/fu e delle altre proprietà dell'acciaio; non propone un materiale di
  partenza da selezionare. Il diagramma affiancato mostra
  le curve caratteristiche e di progetto, campionate dalle leggi della DLL
  usando i coefficienti effettivi del foglio; la compressione è negativa.
  Il salvataggio conserva l'origine e la normativa del materiale.
  Solo il grafico CLS è riflesso: compressione nel primo quadrante, con
  etichette di entrambi gli assi negative. I dati della legge costitutiva e
  i grafici degli acciai mantengono i segni originali.
- La preview dispone di tre checkbox indipendenti, salvate nel foglio:
  dimensioni, copriferro e interferro minimo. Le quote sono in mm e seguono
  la geometria corrente, comprese anima/soletta della T e diametro circolare.
  Il copriferro è quello netto alla staffa; l'interferro è la distanza libera
  minima tra le superfici delle barre ordinarie (non interasse né arco).
  Sono opzioni grafiche, senza ricalcolo strutturale.
- I riquadri delle verifiche condividono la legenda dei punti, mantengono
  separati 3D/2D, tensioni/fessurazione e Vx/Vy e segnalano gli esiti mancanti.
  L'esito senza tasso non viene trasformato artificialmente in un tasso numerico.
  I riquadri sono compatti su due righe, con combinazione governante e tasso;
  i dettagli completi restano nel tooltip. Il pannello destro raccoglie tutte
  le categorie nella stessa schermata alle dimensioni desktop verificate.
- Raggi grafici Ed/Rd separati in 2D/3D; tutti i punti resistenti attivano
  le relative linee. La scelta «Forze: selezionata» resta indipendente.
  I due slider delle dimensioni dei punti sono visibili nel pannello sinistro.
  La discretizzazione angolare ricostruisce il dominio nativo. Interpolazione
  lineare/quadratica e suddivisioni N aggiornano invece soltanto la mesh,
  usando una copia dei punti nativi: risultati, tassi e solver restano gli stessi.
  Le facce trasparenti sono ordinate in profondità rispetto alla telecamera;
  il reticolo usa spigoli unici sul lato visibile, tracciati sullo schermo per
  evitare conflitti di profondità con la superficie. Test incluso a 64 direzioni.
- Il wizard rettangolare permette una seconda fila superiore e inferiore,
  attivabili separatamente. Il circolare permette un secondo anello interno.
  Tutti sono spenti inizialmente; numero, Ø e distanza **libera** dalla prima
  fila sono modificabili. Posizioni, spazio disponibile e sovrapposizioni
  sono validati e la disposizione alimenta la DLL, non soltanto la preview.
  Con barre manuali i comandi restano disabilitati fino al ripristino del wizard.
  Per il taglio rettangolare automatico d e Asl includono i secondi strati;
  il metodo di interasse circolare considera separatamente i due anelli del wizard.
  Nella T l'ultima barra laterale raggiunge la quota interna della staffa alta.
- Tabelle di barre e trefoli nello stesso riquadro, con sottoschede distinte.
  Input a sinistra, viewport e dettagli affiancati, combinazioni in basso:
  proporzioni e divisori uniformi fra dominio 3D, 2D, SLE e taglio.
- I riepiloghi e il report non mostrano verifiche esplicitamente non richieste:
  fessurazione rara NTC e verifica tensionale frequente. Le tensioni frequenti
  continuano a essere calcolate per la fessurazione e consultabili nella vista.
  Non vengono nascosti errori, verifiche applicabili mancanti o normative
  non implementate; queste conservano l'avviso esplicito.

Suite numerica: `dotnet run --project supporto/test/X.Verifiche -c Release -- --checker`.

Campi e tabelle del modulo CA mostrano al massimo due decimali per coordinate,
geometria, materiali, tensioni e azioni. In modifica resta disponibile il valore
completo: il solo cambio di focus non arrotonda i dati e non avvia ricalcoli.
Calcoli, salvataggi e scambi numerici conservano la precisione originale;
le deformazioni molto piccole restano leggibili in notazione scientifica.

Test mirato WPF: `dotnet run --project X.Desktop -c Release -- --smoke-ca-features supporto/artefatti/verifiche_ca_features`.
Quest'ultimo confronta seriale/parallelo lineare e non lineare, controlla
l'identità dei risultati al cambio ambiente, le nuove geometrie, le staffe,
i materiali distinti dei trefoli e il round trip JSON.


## TEORICA A12 — Esito integrazione Checker — 21 settembre 2026

> **Aggiornamento 28 settembre 2026 — torsione del cassoncino** (CompositeBridge 1.4.0.0).
>
> - Nuove verifiche di torsione, distorsione e diaframmi del cassoncino: vedere
>   [sezione-mista-ponte.md](guida-teorica-anthea.md). L'H con anima inclinata resta in flessione retta.
> - Controlli aggiunti: 17 test della libreria con oracoli analitici, controlli X.Verifiche `--bridge` sull'adattatore,
>   la relazione e l'archivio, prova WPF nel `--smoke-bridge` (campi, ΔT, risultati, relazione).
> - BridgeAudit: 302 test superati; le baseline delle sezioni H cambiano solo per testo del campo di validità, nuove chiavi di
>   ingresso e campo `Torsion` nullo, senza differenze numeriche.

> **Aggiornamento 27 settembre 2026 — tutte le suite verdi** (31: regressione, controlli X.Verifiche, libreria di calcolo,
> BridgeDesign e le 17 smoke WPF).
>
> - **Regressione 464/464.**
>   - I 20 casi circolari erano stati calcolati dal programma Python con il contorno fisso di 180 punti. Ora il loro input
>     dichiara `circular_sides = 180`; il default di 32 lati per i nuovi archivi non cambia. Con 180 lati tutti i valori
>     coincidono entro 1e-10.
>   - `palo_storico_0` e `palo_storico_3` hanno c′ = 2 kPa in uno strato granulare. ANTHEA pone c′ = 0 negli strati
>     granulari (guida teorica), il Python lo sommava: i loro valori attesi sono rigenerati dal C#, con la nota `fonte_atteso`.
> - **Smoke generale.** Il test non era aggiornato in cinque punti:
>   - la sezione di default ha 14 barre, non 16;
>   - il riepilogo usa «DA COMPLETARE»;
>   - la fessurazione non è richiesta per la rara in XC1;
>   - il cambio di scheda ricostruisce l'export;
>   - γc danese = 1,45.
> - **Arresto silenzioso durante l'export della relazione CA.** Trend Micro Security Agent (Behavior Monitoring, protezione
>   ransomware) terminava ANTHEA.exe mentre rinominava il terzo .docx temporaneo della sessione. `Archivio.ScriviAtomico` ora
>   copia il temporaneo per i documenti Office; gli archivi mantengono il rinomino atomico. Per l'uso normale conviene comunque
>   chiedere all'IT l'esclusione di ANTHEA.exe dal Behavior Monitoring.

Configurazione: Windows, .NET 8, Release; DLL locali registrate in
[`lib/Checker/manifest.json`](../../lib/Checker/manifest.json).

### Prove eseguite

| Prova | Esito |
| --- | --- |
| Compilazione `ANTHEA.sln` | 0 errori, 0 avvisi |
| Test mirati `--checker` | 143 controlli superati: 72 Checker/NTC, 18 Excel, 53 estensioni CA |
| Regressione sui casi storici | 462 casi superati, 2 falliti (`palo_storico_0`, `palo_storico_3`) |
| Controlli software complessivi | 1.705 superati, inclusi Checker, Excel ed estensioni CA |
| Confronti numerici storici | 999.103 valori; delta massimo assoluto 0,407289 nel calcolo geotecnico del palo |
| Interfaccia CA WPF | 163 controlli, inclusi menu contour reale, scorrimento finestra ridotta, numeri centrati, cache dei criteri, SLE comuni, scale, taglio automatico, Excel, report e riapertura degli archivi |

Le prove dedicate includono confronto dei risultati dell'adattatore con le API
della DLL, cinque percorsi di resistenza, plastico/elastico, lineare/non lineare,
tagli 2D e proiezioni, assi ruotati/eccentrici, trefoli, benchmark VCA_N_1,
valori analitici a taglio e fessurazione, area efficace rettangolare, controlli
senza esito automatico e riduzione dei limiti per elementi piani sottili.

I due scostamenti della regressione storica sono nelle curve
`drenante_compressione.media` dei pali e non attraversano le DLL Checker/Geometry
aggiornate. I valori attesi non sono stati modificati automaticamente.

Le estensioni verificano anche il collegamento delle nove classi normative,
l'applicazione dei coefficienti personalizzati, vertici e raster tensionali
nativi, materiali custom, input geometrici non validi e precisione dello scambio
Excel. Non attestano la completezza normativa delle classi nazionali.

Le schermate sono state controllate visivamente; i messaggi di taglio e
fessurazione rimangono leggibili nelle righe dei risultati.
Il programma pubblicato è in `app/ANTHEA.exe`, avviato da `Avvia ANTHEA.cmd`.
Gli output grezzi delle rifiniture sono sotto `supporto/artefatti/verifiche_ca_refinements/` e il
rapporto numerico è `supporto/artefatti/verifiche_ca_refinements/regressione.json` (ignorati da Git).

### Aggiornamento automatico, Excel e visualizzazioni

Il pacchetto pubblicato in `app` è stato verificato dopo l'ultima correzione.
I test WPF controllano che la digitazione non modifichi il modello fino al
cambio di focus e che la conferma avvii il ricalcolo senza timer. Coprono anche
le modifiche ravvicinate, la cancellazione di un risultato
superato, gli input ancora modificabili durante il calcolo, il riuso del dominio,
la gestione di azioni non numeriche e il recupero automatico dopo la correzione.
Controllano anche l'assenza dei pulsanti Calcola nelle schede CA, le opzioni
richiudibili, copia/incolla senza modifiche parziali su dati invalidi, selezione
delle forze, trasparenza e otto modalità di contouring. L'importazione viene
provata con sostituzione selettiva delle famiglie e ricalcolo automatico.

Sono verificati gli interruttori grafici indipendenti senza ricalcolo, l'origine
centrata e la scala arrotondata del 2D, filtri e ordinamento senza perdita di
righe, n/φ con precisione conservata, dettagli dei vertici e sincronizzazione
delle staffe fra pannello e Taglio. La normativa DS usa i propri coefficienti:
taglio e fessurazione non vengono impropriamente dichiarati verificati.
Il salvataggio e la riapertura provano anche le nuove opzioni grafiche,
i dati condivisi delle staffe e il percorso Excel.

Le rifiniture aggiungono confronti fra cambio del criterio sul dominio nativo
esistente e costruzione ex novo per tutti i cinque criteri: mesh e dominio
rimangono gli stessi, punti e tassi coincidono. In WPF si verifica anche che
le SLE e l'altro dominio non vengano ricalcolati per una sola modifica del criterio.
Prove geometriche indipendenti coprono bw, d e Asl del wizard rettangolare e
le larghezze minime della T; il modello circolare non viene abilitato implicitamente.
Sono provati condivisione SLE, opzioni nascoste senza trefoli, riepiloghi dei casi
peggiori, zoom continuo, fit con/senza azioni e scala 3D senza ricalcolo.

Il template XLSX incorporato è stato creato e controllato con la skill
Spreadsheets: anteprima renderizzata, unità e segno espliciti, lista delle
famiglie, input vuoti senza combinazioni fittizie. I test del lettore coprono
le sei famiglie, numeri decimali, trazione/compressione/zero, dati mancanti,
unità errate, colonne inattese e formule con/senza risultato memorizzato.
L'esportazione delle azioni è stata nuovamente renderizzata e ispezionata:
mantiene lo stile del template, i nomi delle famiglie e la precisione numerica.
Test aggiuntivi coprono il round trip, un nome che inizia con `=` trattato come
testo e un'esportazione vuota reimportabile.

Il report Word CA è stato controllato seguendo la skill Documents: sezioni
selezionabili, limiti sempre presenti, header di tabella ripetibili, didascalie
solidali con le immagini e ispezione visiva di tutte le 14 pagine dell'esempio
con grafici e delle 7 pagine della variante completa SLE con dettagli.
Il default è l'inviluppo con origine separata degli estremi; i test mirati
verificano segni, governanti diversi per tensioni/fessurazione e risultati
mancanti. Le immagini sono inserite nella sezione di pertinenza; quelle SLE
usano il risultato della combinazione governante, non la selezione dell'utente.
Il renderer previsto non ha trovato LibreOffice nel runtime incluso; non è
stato usato un LibreOffice installato dall'utente. Per il collaudo si è convertito
il solo DOCX di prova in PDF tramite un'istanza Word privata e non visibile,
con apertura in sola lettura, quindi si sono renderizzate e ispezionate le pagine.
ANTHEA produce il DOCX direttamente e non richiede Word per generarlo.

Durante le prove sono stati corretti due casi della nuova presentazione:
normalizzazione dei valori resistenti del CLS (la DLL usa valori negativi
per la compressione) e riepilogo senza azioni verificabili. Non sono stati
cambiati i valori resistenti né i tassi restituiti dalla DLL.

### Import massivi e grafici differiti

Il raster SLE viene campionato al primo utilizzo grafico e memorizzato per
risultato; le verifiche e l'esportazione JSON non lo generano. La mesh grafica
3D viene creata alla prima apertura del dominio o alla richiesta del report.
Le schede nascoste non ricostruiscono grafici e dettagli di selezione; i report
possono richiedere esplicitamente i grafici anche senza aprire le schede.

L'importazione e l'incolla aggiornano le collezioni in blocco, con una notifica
Reset per famiglia. Gli aggiornamenti dei risultati sospendono le notifiche
dei singoli campi ed emettono una sola notifica finale per riga modificata.
I test coprono 10.000 inserimenti, scope annidati, risultati invariati,
importazione SLE/taglio, selezione successiva all'import e assenza di raster
per le schede nascoste. Il test numerico confronta il raster differito con
quello di un'analisi indipendente dopo altre analisi sullo stesso motore.

### Ricalcolo selettivo

La coda mantiene le singole verifiche da aggiornare: 3D/2D per SLU e SLV,
le tre famiglie SLE e il taglio. Le modifiche durante il calcolo annullano
lo snapshot in esecuzione e conservano nella coda le verifiche non completate.
I risultati indipendenti restano disponibili e non vengono ricostruiti.

- Modello e impostazioni SLE condivise: solo le tre famiglie SLE.
- Passo, braccia e opzioni del taglio: solo taglio.
- Diametro staffe: aggiornamento completo, perché modifica la posizione
  delle barre longitudinali in `SezioneCA`.
- Azioni e importazioni: solo le famiglie modificate; SLU/SLV aggiornano
  i rispettivi controlli 3D e 2D conservando i domini compatibili.
- Opzioni dominio: solo il pannello 3D oppure 2D interessato.
- Geometria, materiali, normativa e trefoli: aggiornamento completo.

184 controlli WPF superati, inclusa la conservazione per identità dei
risultati indipendenti e l'accodamento contemporaneo di modifiche SLE/staffe.
Sul file reale da 9.994 righe e rettangolare predefinita: cambio a SLE non
lineare 2,303 s; cambio passo staffe 0,194 s, senza errori di calcolo.
Tempi indicativi della macchina di prova, comprensivi di aggiornamento UI.

### Cosa attestano queste prove

I confronti con la DLL verificano il collegamento, le unità, i segni e le opzioni;
non sono una validazione indipendente dell'intero motore Checker. I confronti
storici riguardano il motore preesistente e gli altri moduli, non l'equivalenza
dei vecchi domini con quelli nuovi. I benchmark analitici coprono i casi indicati,
non tutte le possibili sezioni o situazioni strutturali.

Nessuna certificazione globale: prima dell'uso progettuale occorrono ulteriori
benchmark concordati con il progettista, specialmente per precompressione,
sezioni complesse, fessurazione e taglio combinati. I casi esclusi restano senza
esito automatico. Correzioni, fonti normative e limiti operativi sono documentati
in [calcestruzzo-interfaccia.md](guida-pratica-anthea.md). La copertura delle
classi normative e il lavoro futuro sono in
[normative-calcestruzzo.md](guida-teorica-anthea.md).


## TEORICA A13 — Sezione da ponte: irrigidimenti, appoggi e connessione

Revisione 25 settembre 2026. Perimetro concordato: completare i dettagli locali,
l’interazione N–M–V e la connessione, lasciando invariato il calcolo delle fasi.
Questo documento integra [taglio e pioli](guida-teorica-anthea.md).

### Interfaccia e dati

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

### Irrigidimenti intermedi e appoggi

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

### Interazione N–M–V

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

### Soletta e connessione

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

### Sorgenti e verifiche

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

### Fonti primarie

- [NTC 2018, Gazzetta Ufficiale](https://www.gazzettaufficiale.it/eli/id/2018/2/20/18A00716/sg).
- [EN 1993-1-5:2006](https://www.phd.eng.br/wp-content/uploads/2015/12/en.1993.1.5.2006.pdf), §§5, 7 e 9.
- [EN 1993-1-1:2005](https://www.phd.eng.br/wp-content/uploads/2015/12/en.1993.1.1.2005.pdf), §§5.2.2 e 5.3.4.
- [EN 1993-1-8:2005](https://www.phd.eng.br/wp-content/uploads/2015/12/en.1993.1.8.2005-1.pdf), §§4.5 e 4.11.
- [EN 1994-2:2005](https://www.phd.eng.br/wp-content/uploads/2015/12/en.1994.2.2005.pdf), §§6.6 e 6.8.
- [EN 1992-1-1:2004](https://www.phd.eng.br/wp-content/uploads/2015/12/en.1992.1.1.2004.pdf), §§6.2.4, 8.4 e 9.2.2.

Le edizioni sono quelle dichiarate dal modulo; i coefficienti esposti non
costituiscono una scelta automatica dell’Appendice Nazionale.


## TEORICA A14 — ANTHEA.Calculations — separazione e trasferimento

Aggiornamento: 27 settembre 2026. Audit corrente: [calcoli, dati comuni e progetti](guida-teorica-anthea.md).

### Confini

`X.Calculations` produce `ANTHEA.Calculations.dll`, namespace `Anthea.Calculations`, target .NET 8. Contiene motori numerici, adattatori verso Checker/Model, geometrie, modelli di input/risultato, cataloghi, coefficienti, impostazioni e validazioni. I motori Materiali conservano il namespace `Materiali` per compatibilità sorgente. La libreria non dipende da WPF, `X.Core`, archivi o Word.

`X.Core` contiene documenti/progetti, ereditarietà, confronto, salvataggio, importazione Excel e generatori Word. `X.Desktop` e `X.Materiali` contengono controlli, grafica, acquisizione dell’input e presentazione. Conversioni grafiche, formattazione, aggregazione dei risultati già calcolati e impostazioni della vista restano nell’interfaccia.

I metodi dei ponti, compreso lo storico lineare/non lineare e M–κ/N–ε, rimangono in `GPCChecker.CompositeBridge`. `BridgeSection` nella nuova libreria è il confine fra archivi JSON e contratti Checker. La separazione non sostituisce le formulazioni numeriche esistenti e non unifica metodi che rappresentano modelli fisici distinti.

Le forme native usano `GPC.Geometry.Polygon2d` e `Shape2d`; aree, distanze dai contorni e appartenenza al calcestruzzo delegano alla stessa Geometry fornita. Sezioni resistenti, materiali e barre usano i tipi `GPC.Model`. I cataloghi restituiscono istanze nuove per evitare contaminazioni fra calcoli. Il clipping specializzato delle zone efficaci e i contratti JSON degli archivi restano adattatori applicativi. C12/15 e C16/20, già supportate dalle schede materiali ma assenti dal catalogo ModelData dello snapshot, vengono conservate utilizzando il materiale EN1992 di Model.

### API

```csharp
using Anthea.Calculations;
var data = ModuleCatalog.CreateData("str_mista_ponte");
var result = CalculationService.Calculate("str_mista_ponte", data, cancellationToken);
```

- `ModuleCatalog`: ID stabili, un solo nome e descrizione per modulo, factory indipendenti e validazione della struttura.
- `CalculationService.Calculate`: ingresso non visuale per tutti i nove moduli. Usa una copia dell’input; non aggiorna documenti. Restano disponibili i servizi tipizzati; gli adattatori dichiarano le conversioni di unità e segni.
- `HorizontalConcreteSection`: resistenza c.a. del palo a N costante, tramite lo stesso Checker del modulo strutturale. N geotecnico positivo a compressione viene convertito in N negativo; sono espliciti contorno poligonale e residuo assiale.
- `ConcreteMaterials.DesignValues`, `ReinforcementGeometry`, `CalculationHelp`: resistenze di progetto, geometria di barre/fasci e significato dei dati comuni per UI e servizi.
- `ConcreteAnalysisSession`: cache locale alla sessione; invalidazione basata sugli input, nessuna cache globale di risultati fra progetti.
- `ConcreteShearAnalysis`, `ConcreteDetailingAnalysis`, `ConcreteCurvatureAnalysis`, `ConcreteSectionProperties`, `ConcreteCoverAnalysis`, `ConcreteBond`: servizi richiamabili senza controlli WPF.
- `ConcreteCodeChecks`: profili NTC/EC2/UNI/DIN/DS/NS/MC2010 per taglio e fessurazione ordinaria; ingresso tipizzato `ShearInput`, formule pure e tracce numeriche. Gli adattatori di sezione conservano geometria GPC e piano Checker. [Copertura e validazione](guida-teorica-anthea.md).
- `CalculationCoefficients`: percorsi autorevoli, etichette, ambito normativo e rilevanza dei coefficienti.
- `CalculationValidation`: coefficienti finiti positivi e normativa supportata. La validazione strutturale dell’archivio permette di salvare input incompleti; il calcolo li rifiuta o restituisce gli errori dei singoli casi secondo il contratto del motore.

Geometrie delle sezioni: mm; tensioni/moduli: MPa; azioni: kN e kNm. Le funzioni geotecniche conservano m e kN. Per le sezioni, compressione negativa; deformazioni secondo il contratto del servizio, senza conversioni implicite fra adimensionale, ‰ e microdeformazioni. Fare riferimento ai nomi dei campi e alla documentazione del singolo modulo.

### Progetti e coefficienti

Normativa e coefficienti hanno gruppi distinti. I valori condivisi vengono trasferiti solo dove chiave, significato e riferimento sono compatibili. `γM0` del ponte e `γM0` del CHS hanno chiavi distinte. `γs` di una scheda di acciaio assegnato può fungere da riferimento esplicito per le armature. Il coefficiente `γR = 1,3` del palo orizzontale resta una costante del metodo, non viene esposto come falso input modificabile.

Un nuovo foglio eredita prima la normativa, poi geometria, materiali, coefficienti, armature e terreno. Le azioni e lo storico delle fasi restano propri del foglio. Conflitti allo stesso livello non vengono risolti scegliendo arbitrariamente un riferimento. I tre coefficienti base CA sono autorevoli in `input`; gli alias storici in `workspace_ca/coefficienti` vengono sincronizzati.

`ProjectValidation` serve anteprima, stato del foglio e report. `ReportProject` aggiunge i controlli del progetto anche se il chiamante non passa avvisi. Il report usa etichette comuni, omette coefficienti non pertinenti e riporta una sola volta i valori comuni nel riepilogo degli input. I nomi attribuiti dall’utente e gli ID degli archivi esistenti restano invariati.

### Trasferimento

Eseguire dal repository:

```powershell
./supporto/scripts/Export-CalculationLibrary.ps1
```

La cartella esportata contiene sorgenti, DLL Checker/Model/Geometry e test indipendenti, escludendo `bin`/`obj`. Non contiene un collegamento a `X.Core` o all’interfaccia. Dalla cartella esportata:

```powershell
dotnet build X.Calculations/X.Calculations.csproj -c Release
dotnet run --project supporto/test/CalculationLibrary.Checks -c Release
```

La dipendenza NuGet è MathNet.Numerics 5.0.0; occorre accesso al pacchetto o alla cache. Le DLL fornite restano necessarie. Per collocarle altrove, impostare `-p:CheckerLibraryDirectory=percorso-assoluto`. Il pacchetto è destinato al trasferimento interno, non è una pubblicazione NuGet né una ridistribuzione pubblica delle dipendenze.

### Validazione

Le prove della separazione e i relativi limiti sono registrati in `supporto/docs/validazione-libreria-calcolo.md`. Distinguono controlli analitici, confronti indipendenti, regressioni interne e verifiche dell’interfaccia. Lo spostamento del codice non certifica l’intero progetto né sana automaticamente le differenze numeriche già rilevate.


## TEORICA A15 — Migrazione del calcolo ponte in Checker — 25 settembre 2026

Il progetto [GPCChecker.CompositeBridge](../../../Checker/GPCChecker.CompositeBridge/README.md)
contiene ora il motore numerico. È referenziato da ANTHEA tramite
`lib/Checker/GPCChecker.CompositeBridge.dll`; rimossi i collegamenti diretti ai
sorgenti del progetto Steel.

In `X.Calculations/BridgeSection*` (dal riordino del 26 settembre; precedentemente `X.Core`) rimangono cataloghi, predefiniti, lettura degli archivi,
conversione in `HBridgeInput`, esportazione e deleghe alla libreria. La vista
gestisce inserimento, aggiornamento, formattazione e disegno dei risultati.
Il costruttore del solver condivide il medesimo lock con il modulo cemento armato.

Il metodo numerico precedente è conservato. Le 8 istantanee di regressione
in Checker sono state acquisite **prima** dello spostamento e confrontano tutti
i campi esportati, incluse le verifiche accessorie. Nessuna correzione dei
difetti già documentati è inclusa in questo intervento.

La libreria separa il motore iterativo, le proprietà, l'omogeneizzazione, le
riduzioni dei pannelli e l'accesso al solver dall'adattatore H. È provato anche
un modello N–Mx a due anime; il calcolo completo dei cassoni resta da sviluppare.
Il secondo metodo per fasi non è stato implementato.

I sorgenti dei test numerici sono nei progetti Checker
`GPCChecker.Test.CompositeBridge` e `GPCChecker.Test.BridgeAudit`.
Log, TRX e schermate di questa migrazione sono in
`supporto/artefatti/ponte_migrazione_checker/` (non versionati).

### Esiti

- Checker ordinario: 267 test superati, di cui 8 confronti completi prima/dopo.
- API autonoma CompositeBridge: 28 test superati sia con le DLL dell'app sia
  con le dipendenze sorgenti. Non sono 56 casi distinti.
- ANTHEA ponte: 117 controlli superati; UI: 381 controlli più lo scenario completo
  di apertura, archivio, esportazioni JSON/Word e relazione di progetto.
- Modulo cemento armato: 102 controlli Checker, 18 Excel, 174 estensioni,
  27 dati/riepiloghi, tutti superati.
- Audit completo: 267 passati, 5 falliti attesi, 10 già ignorati. I fallimenti
  sono quattro casi del costruttore H nullo sulle DLL Model precedenti alla
  correzione e il metadato di inerzia a carico nullo. Non modificati.
- Build ANTHEA e libreria sullo snapshot: nessun errore o avviso. La build
  sorgente autonoma passa con i due avvisi già presenti in Checker.Concrete
  sui riferimenti UnsafeEx e sull'architettura GMsh.Net.


## TEORICA A16 — Muri di sostegno

Revisione del 30 settembre 2026, allineata alle guide generali Rev07. Due colonne di terreno, attriti, input guidato e terreno sotto la fondazione visibile. Guida operativa illustrata: [Stabilità globale, guida rapida](guida-pratica-anthea.md), anche in PDF.

Il modulo **Geotecnica → Muri di sostegno** calcola mensola in c.a. e gravità per metro di sviluppo. Le altre tipologie sono predisposte nello schema e restano senza calcolo. Il riferimento funzionale è [MAX di Aztec](https://www.aztec.it/max-muri-di-sostegno/); non viene dichiarata equivalenza numerica.

### Interfaccia

Due schede: **Input** e **Verifiche**. Gli input seguono l'ordine Terreno, Materiali, Geometria, Azioni. La stratigrafia usa righe colorate collegate al disegno, spessori, profondità progressive, aggiunta, eliminazione e riordino. Il piano di posa deve essere coperto dalla stratigrafia. Il profilo riporta terreno, dimensioni, armature, falda e carichi.

Si possono aggiungere sovraccarichi uniformi, forze orizzontali, forze verticali, momenti, pressioni laterali su tratti e urti equivalenti statici per metro. Ogni azione ha natura G1/G2/Q/A, ψ₀/ψ₁/ψ₂ e gruppo correlato facoltativo. Gruppo vuoto significa indipendenza; nello stesso gruppo natura e ψ devono coincidere. `Calcola` include l'azione nel motore; `Disegna` ne cambia soltanto la visibilità. Le quote delle azioni sono dal piano di posa; x dal bordo a valle. H e pressioni positive verso valle, N a compressione, M ribaltante. La forza verticale deve ricadere nel fusto alla quota assegnata.

Il disegno Input mostra i carichi caratteristici; le viste Verifiche mostrano quelli della combinazione selezionata. Passare sul disegno legge i dati; cliccare un carico apre il relativo editor. Le viste comprendono N/M/V del fusto e delle due mensole, forze resistenti, tassi di lavoro e armature. I diagrammi sono campionati; fra le stazioni il cursore interpola i valori. Il riepilogo inferiore offre inviluppo, combinazione selezionata, tutti i controlli, sollecitazioni numeriche e audit delle spinte.

I tassi di lavoro usano la scala comune degli altri moduli: 0–0,50 blu, 0,50–0,70 verde, 0,70–0,90 giallo, 0,90–1,00 arancio, oltre 1,00 rosso; grigio per controlli incompleti.

### Due stratigrafie e altezza libera

Le colonne VALLE e MONTE sono affiancate e mostrano nome, spessore, γ, γsat, φ′ e profondità progressiva del fondo. Ogni colonna ha aggiunta, eliminazione e riordino degli strati; la selezione evidenzia il lato nel disegno. Le profondità partono dalla superficie del rispettivo lato. Servono strati sufficienti a raggiungere il piano di posa: H+t a monte, Dv a valle. Entrambe le colonne ammettono fino a 50 strati granulari, con c′=0 nel calcolo delle spinte.

In Tratto libero a valle scegliere Assegnato e inserire Hlib, distanza verticale dalla sommità del muro alla superficie del terreno di valle. Il programma usa Dv=H+t−Hlib. Hlib=H+t significa assenza di terreno sopra il piano di posa; Hlib=0 significa terreno fino alla sommità. Interamente libero segue automaticamente le modifiche di H e t. Esempio: H=3 m, t=0,45 m e Hlib=2 m producono Dv=1,45 m, con 1 m di terreno sopra la mensola.

Modifica insieme le due colonne copia inizialmente monte su valle, poi propaga le modifiche da entrambi i lati. Si collegano spessori e proprietà, non quote assolute: con superfici diverse i fondi degli strati hanno quote diverse. Disattivare il collegamento conserva le due copie e permette modifiche indipendenti. Le falde restano dati separati.

Il peso comprende la mensola di valle e il cuneo di terreno sopra il paramento inclinato. Area e baricentro sono integrati per strato usando GPC.Geometry. Le sezioni del fusto includono il peso del cuneo sopra la sezione; la mensola di valle include il proprio carico distribuito di terreno. Nei casi sismici il terreno a valle contribuisce anche alle inerzie.

Considera la resistenza passiva è disattivato inizialmente. Se attivato richiede una frazione mobilitata ηp fra 0 e 1, inizialmente 0: non viene assunto automaticamente il completo sviluppo della passiva. Occorre motivare permanenza del terreno, assenza di scavi futuri e spostamenti necessari. Kp=1/Ka con φd dello strato di valle; σ′v deriva dalla sua colonna. La risultante usata è limitata alla spinta motrice, non maggiorata da γG favorevoli superiori a 1, e compare una sola volta nel bilancio orizzontale. Nel sisma la passiva è esclusa; non è implementato un modello passivo dinamico. Questa idealizzazione usa un piano verticale esterno a valle e non una ricerca del cuneo passivo multistrato.

La portanza aggiunge q′ B′ Nq iq al termine 0,5 γ′ B′² Nγ iγ, con iq=max(0,1−|H|/V′)² e iγ=max(0,1−|H|/V′)³. q′ è l’integrale dei pesi efficaci di valle fino al piano di posa, senza maggiorazioni favorevoli e senza fattori di profondità aggiuntivi. Il terreno omogeneo di fondazione resta un input distinto, da caratterizzare sotto il piano di posa.

### Attrito al muro e alla fondazione

Attriti e terreno di fondazione distingue due interfacce. Assegnato usa δk inserito e tanδd=tanδk/γMφ. Gettato in opera usa k=1, Prefabbricato liscio k=2/3, Liscio k=0. Nei modi automatici si assegna φcv,k, angolo caratteristico a volume costante del terreno a contatto; δd=k·atan(tanφcv,k/γMφ). La riduzione si applica a φcv prima di moltiplicare per k. φcv non viene dedotto automaticamente da un eventuale angolo di picco φ′ e non può superarlo.

La spinta sul paramento di monte usa Coulomb in statica e Mononobe–Okabe in sisma, con la componente verticale Pverticale=Porizzontale tanδd. Con mensola a monte, l’equilibrio generale considera muro e terreno sulla mensola: la spinta esterna agisce sul piano virtuale dietro la mensola e usa δ=0. L’attrito reale muro–terreno è interno a questo blocco, quindi influenza il fusto ma non si aggiunge nuovamente all’equilibrio esterno. Senza mensola a monte si usa il paramento reale anche per l’equilibrio. Wood conserva K₀ e non mobilita l’attrito di parete.

Per paramento verticale e superficie orizzontale, la componente orizzontale del coefficiente è K=cos²(φd−θ)cosδd / {cosθ cos(δd+θ)[1+√(sin(φd+δd)sin(φd−θ)/cos(δd+θ))]²}. In statica θ=0; nel sisma θ=atan[kh/(1−kv)], con θ<φd. A δ=0 si ritrova il coefficiente liscio; a δ=θ=0 Rankine. Restano i limiti di terreno omogeneo asciutto per le spinte sismiche locali.

Valori di calcolo apre una finestra con gli input modificabili e, per la combinazione selezionata, quote, pesi, attriti ridotti, μ, passiva disponibile e utilizzata, q′, Nq, Nγ, iq, iγ, spinte di monte e del fusto, azioni e sollecitazioni. Per cambiare un valore automatico si modifica la sua origine o si seleziona Assegnato. Le risultanti si ricalcolano dalle cause; non si forzano manualmente valori di verifica. Applica e ricalcola controlla l’intero modello prima di sostituire i dati. Le combinazioni modificate passano a Personalizzate. Con colonne collegate, nella finestra modificare una sola colonna alla volta. Per le opzioni booleane usare true o false.

### Combinazioni e approcci

Preset locale dei muri NTC 2018: **Approccio 2, A1+M1+R3**. A modifica le azioni; M riduce i parametri del terreno; R divide le resistenze. A1+M1+R1 appartiene alla prima combinazione dell'Approccio 1 e non sostituisce A2+M2+R2. La stabilità globale usa il motore Bishop separato e combinazioni A2+M2+R2 proprie.

Il generatore enumera i contributi favorevoli/sfavorevoli di muro, terreno di monte, terreno di valle, acqua e permanenti. Con Dv>0 i pesi di monte e valle hanno fattori indipendenti 1/1,3 nelle combinazioni SLU; γ valle è modificabile nella matrice. Per le variabili alterna la principale, applica ψ₀ alle accompagnatrici e considera l'omissione favorevole. G1: 1/1,3; G2: 0/1,5; Q: 0/1,5 con ψ dove previsto. Rara e frequente alternano la principale; quasi permanente e sisma usano ψ₂. Gli eventi eccezionali indipendenti sono separati, con fattore 1 dell'evento, ψ₂ delle variabili e γR=1. Le resistenze dei materiali rimangono cautelativamente quelle ordinarie.

La matrice permette di modificare abilitazione, nome, stato, γG, fattori γ×ψ di ogni azione, γMφ, γR distinti, kh e kv. La modifica passa a **Personalizzate** e viene conservata nei ricalcoli. Cambiare elenco, natura, ψ o gruppi richiede rigenerazione o conferma di una matrice coerente. `Genera / ripristina automatiche` sostituisce le modifiche col preset. Coefficienti vuoti, matrici incomplete, azioni escluse con fattori non nulli e urti nelle combinazioni ordinarie sono rifiutati. Limiti: 30 azioni e 4096 combinazioni; il generatore non tronca silenziosamente i casi.

GPC.Model.LoadCase e Combination gestiscono le associazioni azione/coefficiente. L'enumerazione è nell'adattatore ANTHEA: il generatore EN1990 disponibile in GPC non espone direttamente ψ individuali, gruppi e gli eventi eccezionali di questa matrice. GPC.Geometry calcola area e baricentro; GPC.Model e GPCChecker.Concrete restano usati per materiali, sezioni, resistenza N–M e tensioni.

### Campo e formule

Fusto trapezio, paramento di monte verticale, riempimento orizzontale granulare drenato, c′=0. Fondazione nastriforme orizzontale con ricoprimento di valle. Il terreno davanti al muro entra con il proprio peso, il momento, il carico sulla mensola e il termine di ricoprimento efficace nella portanza. La passiva Rankine è opzionale e parzializzabile. La formula di portanza richiede δb,d ≥ φf,d/2; con base più liscia si calcola lo scorrimento ma la portanza resta esplicitamente fuori campo. Sovraccarichi di estensione finita, carichi di verso opposto e geometrie aggiuntive richiedono estensioni del motore.

La spinta agisce sul piano virtuale al bordo della mensola a monte, altezza Ht=H+t. Ka=tan²(45°−φd/2), con tanφd=tanφk/γMφ. Si integra σ′v con γ sopra falda e γsat−9,81 sotto falda. Ogni strato applica il proprio Ka: è un'estensione locale di Rankine, non una ricerca del cuneo multistrato. Acqua separata e sottospinta lineare integrale; il peso sopra la mensola usa γsat totale. Spinta e peso della stessa sorgente sono correlati.

- Scorrimento: Rd=V′tanδd/γR, γR=1,1 nel preset ordinario.
- Ribaltamento: Mrib≤Mstab/γR, γR=1,15 nel preset ordinario; sottospinta inclusa nel momento ribaltante.
- Contatto: compressione soltanto, trapezio per |e|≤B/6 e triangolo altrimenti; nessuna reazione fittizia se la risultante è esterna o V′≤0.
- Portanza drenata (EN 1997-1 allegato D, c′=0, ricoprimento q′ di valle): Rd=(q′B′Nq iq+0,5γ′B′²Nγ iγ)/γR; iq=max(0;1−|H|/V′)²; Nq=exp(πtanφd)tan²(45°+φd/2); Nγ=2(Nq−1)tanφd; iγ=max(0;1−|H|/V′)³; B′=B−2|e|. γR=1,4 nel preset ordinario. Fuori campo per |e|>B/3 o V′≤0. Con falda si usa γ′ su tutta la zona di rottura.

Le verifiche strutturali campionano H/20 e aggiungono sezioni presso i carichi e il cambio armatura. Le mensole hanno 21 stazioni ciascuna. Il fusto può avere due zone verticali, separate da h₁ misurata dal piede, con diametro e numero di barre per metro diversi. Le facce possono essere simmetriche o indipendenti. Entrambi i lati della transizione sono verificati anche se lo spessore è costante. Con i dettagli attivi la quantità comprende ancoraggi, sovrapposizioni e armature secondarie.

Per mensola: N–M da GPC, taglio senza staffe NTC senza beneficio della compressione, minimi/massimi di armatura, tensioni SLE e fessurazione. αcc=0,85, γc=1,5, γs=1,15. Per gravità: compressione, assenza di trazione nel fusto, taglio elastico e flessione della fondazione non armata rispetto alle resistenze assegnate. La modalità Resistenze assegnate non è una verifica completa di pietrame o muratura; i nuovi modelli CLS e Muratura sono descritti in Rev07.

### Sisma e audit delle spinte

Scelta fra **Mononobe–Okabe** e **Wood semplificato**, soltanto con terreno omogeneo asciutto. I nuovi muri propongono **Da parametri del sito (SLV)**; gli archivi precedenti conservano **kh e kv assegnati**. Entrambi i segni di kv, direzione orizzontale verso valle. Nessun parametro del sito viene inventato o ricavato dalla sola località.

Il pulsante **Sisma** accanto alla combinazione visualizzata mostra attivazione e numero dei casi SISMA calcolati; apre direttamente **Input → Azioni → Sisma**, all’inizio del pannello Azioni. In modalità automatica l’attivazione genera le combinazioni sismiche. Le matrici personalizzate conservano i propri coefficienti: se non contengono casi SISMA abilitati, il pulsante lo segnala; aggiornare la matrice o rigenerare le automatiche.

Nel percorso guidato si inseriscono **ag/g e F₀ dello SLV del progetto**, categoria di sottosuolo A–E e topografia. La categoria A–E proviene dalla caratterizzazione geotecnica e sismica: non viene dedotta da φ′ o dai pesi degli strati. F₀ non serve per la categoria A. Il periodo T*c non entra nel calcolo di Ss.

Posto x=F₀·ag/g, Ss segue la tabella 3.2.IV NTC 2018: A=1; B=max(1;min(1,2;1,4−0,4x)); C=max(1;min(1,5;1,7−0,6x)); D=max(0,9;min(1,8;2,4−1,5x)); E=max(1;min(1,6;2−1,1x)). È disponibile anche Ss assegnato.

St viene ricavato dalla forma del terreno: pianeggiante o pendenza media ≤15° → T1, St=1; pendio >15° → T2; rilievo a cresta stretta con pendenza >15° e ≤30° → T3, oltre 30° → T4. La cresta stretta e la configurazione prevalentemente bidimensionale sono condizioni da riconoscere nel sito. Per pendii/rilievi oltre 30 m, St=1+(St,max−1)·z/H, con z sopra la base del rilievo, H altezza del rilievo, St,max=1,2 per T2/T3 e 1,4 per T4. Fino a 30 m l’amplificazione topografica semplificata non è richiesta e il preset adotta St=1. Per topografie complesse occorre la risposta sismica locale. È disponibile St assegnato.

amax/g=Ss·St·ag/g; kh=βm·amax/g; kv=±0,5kh. Nel preset SLV: βm=0,38 per muro libero (MO), βm=1 per muro vincolato (Wood). La riduzione presuppone spostamenti compatibili con la funzionalità delle opere interagenti. Per ribaltamento βm,rib=min(1;1,5βm): si generano due casi generali e due casi dedicati al ribaltamento, separati anche nelle verifiche. A kh=kv=0 i casi di segno uguale sono deduplicati. Le resistenze SLV usano la tabella 7.11.III (scorrimento 1; ribaltamento 1; portanza 1,2, con inerzia del terreno nell’allegato F). Il percorso automatico delle spinte riguarda lo SLV; per gli spostamenti SLD/SLV è disponibile il calcolo separato Newmark con accelerogrammi. Il motore mantiene il campo kh≤0,4 e |kv|≤0,2: valori derivati superiori fermano il calcolo, senza troncamento.

Le righe della matrice espongono anche **Uso sisma** (Generale / Ribaltamento). Le modifiche dei dati del sito invalidano la firma delle matrici personalizzate: rigenerare o aggiornare e confermare la matrice. I valori delle righe confermate prevalgono sui coefficienti di riferimento mostrati nel pannello. La modalità manuale conserva per compatibilità il vecchio preset con γR statici e senza incremento automatico di kh per ribaltamento; l’avviso viene riportato negli esiti e nella relazione.

MO con δ=0 sul piano virtuale: θ=atan[kh/(1−kv)]<φd; Kae=cos²(φd−θ)/{cos²θ[1+√(sinφd·sin(φd−θ)/cosθ)]²}. L'incremento Δp=(Kae−Ka)(1−kv)γHt/2 è uniforme, risultante a Ht/2. Il sovraccarico contribuisce con Kae(1−kv)Σfiqi. Sono aggiunte le inerzie di muro, terreno sulla mensola e carichi verticali.

Wood semplificato: muro rigido non cedevole, K₀=1−sinφd per terreno normalmente consolidato; ΔP=khγHt². La distribuzione uniforme a Ht/2 è un'idealizzazione dichiarata del modulo, **non la soluzione elastica completa di Wood**. Il sovraccarico conserva la componente statica K₀q. Quando Wood è attivo si usa K₀ anche nei casi statici; i vincoli necessari a impedire il movimento non sono verificati dal modulo.

L'audit espone z, φk/φd, Ka/K₀/Kae, σ′v, contributi di terreno, sovraccarico, acqua e sisma, totale e integrali. Le pressioni laterali dirette sono integrate separatamente. La portanza sismica comprende l’inerzia del terreno nel modello Annex F descritto in Rev07. La stabilità globale è disponibile come analisi separata; cedimenti e spostamenti richiedono l’attivazione e i dati descritti in Rev07; liquefazione, verifiche idrauliche e completamento esecutivo restano esterni. Nessun esito complessivo dell'opera.

### Trasferimento del terreno

**Invia / carica terreno…**, nel pannello Terreno dei muri e nei moduli compatibili, apre l’anteprima di strati e falda. Si può inviare a un nuovo foglio di **Portanza del palo verticale**, **Palo orizzontale**, **Micropalo orizzontale** o **Muri di sostegno**, oppure salvare e ricaricare un file `*.anthea-terreno.json`. Nel progetto nasce un nuovo foglio nella stessa sezione; per un calcolo autonomo si apre una nuova finestra. La copia è indipendente.

Selezionare Monte o Valle nella finestra di trasferimento; la superficie del lato selezionato diventa z=0. Si trasferiscono spessori, nomi se disponibili, tipologia, γ, γsat, φ′, c′, cu disponibili e falda, mantenendo tutti gli strati, anche sotto il piano di posa del muro. **z=0 deve corrispondere allo stesso riferimento fisico**: sommità del terreno del muro oppure piano campagna/testa palo. Non sono applicate traslazioni di quota e il profilo non viene esteso alla punta del palo. Nel palo vanno completati lunghezza, azioni, addensamento e parametri specifici. Il numero di verticali indagate non viene dedotto dal numero di profili copiati.

L’importazione nel foglio corrente sostituisce il profilo selezionato e la falda; negli altri sondaggi gli strati restano invariati (la falda è un dato generale del modulo pali). Il muro accetta soltanto strati granulari con c′=0 e parametri nel proprio campo: profili coesivi o con coesione non vengono convertiti silenziosamente. Il terreno di fondazione resta separato. Importando Valle si ricava il battente dalla quota Dv e dalla profondità di falda; una falda importata sotto il piano di posa è rifiutata dal modello locale. Controllare la compatibilità con il battente di monte prima del calcolo. Il micropalo verticale Bustamante–Doix ha uno schema specifico ed è escluso dal trasferimento.

### Stabilità globale implementata

Il pulsante **Stabilità globale** nella barra superiore apre direttamente il percorso in Input → Terreno. Al primo accesso, se il profilo è vuoto, precompila e attiva la verifica: copia le due colonne già note, falda e attivazione del sisma e propone le superfici orizzontali sui due lati. Non prolunga le indagini. Riaprire il pannello conserva i dati; **Prepara dal muro** sostituisce invece profilo e strati con una nuova proposta. Le modifiche successive ai terreni locali non sovrascrivono la stratigrafia globale indipendente.

Le tabelle globali mostrano **terreno, spessore e quota del fondo**. Si inseriscono gli spessori dall’alto verso il basso, riferiti alla superficie di ciascun lato presso il muro. La selezione di una riga apre le proprietà dello strato: γ e γsat; φ′k e c′k in Drenata, cu,k in Non drenata. I colori seguono il nome del terreno nelle due colonne e nel disegno; il colore non collega i valori numerici. Un nuovo strato ha proprietà da completare. Nell’archivio rimangono le quote assolute dei fondi, orizzontali e decrescenti in ogni colonna. Origine (0;0) al bordo di valle del piano di posa; x verso monte, y verso l’alto. Fondo −5 m significa 5 m sotto la fondazione. Il raccordo di valle termina in (0;Dv), quello di monte parte da (a+s₀;H+t). Due colonne usa un confine verticale assegnabile, inizialmente sul paramento di monte; Profilo unico mantiene un’unica stratigrafia geologica.

**Automatica** propone l’intera estensione orizzontale del rilievo, uscite fino a x=−0,1 m, ingressi da x=B+0,1 m e profondità da 0,1 m fino al minore fra 2(H+t) e la profondità indagata comune alle due colonne. I limiti vengono aggiornati modificando il modello; non sono una garanzia di sufficiente estensione della ricerca. **Assegnata** permette di modificarli nei dettagli. Conci, nodi e raffinamenti restano sempre modificabili. Negli archivi senza il nuovo campo di modalità, i limiti esistenti rimangono Assegnati. Una colonna incompleta non genera profondità fittizie. Il messaggio iniziale indica il primo dato da completare; il suggerimento sul messaggio elenca gli altri.

Il disegno mostra terreni sotto il piano di posa, nomi e fondi, fasce di ingresso/uscita e profondità massima della ricerca. La vista principale del muro rappresenta già il terreno di fondazione; con il modello globale attivo mostra il dettaglio degli strati globali. Il disegno non estende la validità delle indagini. Rilievo e falda, modello del terreno, limiti numerici e combinazioni sono in gruppi di dettagli. La falda è una polilinea piezometrica in coordinate (x;y), senza acqua esterna sopra il terreno. Le azioni esistenti sono riutilizzate; il sovraccarico uniforme interessa tutto il monte.

Dopo aver controllato rilievo, terreni e falda, selezionare **Ho controllato profilo, strati e falda del sito**, quindi **Calcola globale**. Il programma apre Verifiche → Stabilità globale. Una modifica ai dati geotecnici del percorso annulla la conferma e il risultato precedente. Il caso iniziale è quello con tasso η=γR/F maggiore; i casi senza superficie valida hanno priorità. Un F minimo non coincide necessariamente con il massimo tasso se i γR sono diversi. La guida rapida contiene un esempio ripercorribile, anche con esito non soddisfatto.

Bishop semplificato impone equilibrio verticale dei conci e globale dei momenti; non impone equilibrio orizzontale e trascura il taglio interconcio. Le superfici sono circolari, ramo inferiore, centro fra ingresso e uscita, anche con tangente verticale all’ingresso, sotto l’intero muro e verso valle. Il muro sostituisce il volume del terreno: il suo peso non viene duplicato. Spinte muro–terreno e reazioni di fondazione sono interne alla massa e non entrano come azioni esterne. Sisma applicato alle masse proprie; i sovraccarichi sono azioni esterne senza ulteriore massa sismica associata.

Ricerca deterministica su ingresso, uscita e profondità, con raffinamento da sei minimi distinti. Default: griglia 9³, 60 conci, quattro raffinamenti. Gli spigoli, i confini del muro e le intersezioni con gli strati suddividono ulteriormente i conci. Integrazione dei pesi a quadratura di Gauss, solutore dell’equazione Bishop con bracket lontano dalle singolarità mα. Verifica del minimo con il doppio dei conci, tolleranza del 2%; ampliare anche la griglia e il dominio per controllare la ricerca. Nessuna garanzia del minimo assoluto.

In statica A2–M2–R2: γG1=1, γG2=0/1,3, γQ=0/1,3 con ψ individuali, γMtanφ=γMc′=1,25, γMcu=1,4, γR=1,10. Il coefficiente restituito F usa già i parametri ridotti; il tasso è η=γR/F. SLV del complesso muro–terreno: γA=γM=1, γR=1,20, βs=0,38, kh=βs·amax/g, kv=±0,5kh. βs non cambia scegliendo Wood. È possibile assegnare kh/kv globali; non vengono copiati implicitamente i coefficienti manuali delle spinte. Preset eccezionale separato M1/R=1.

La matrice globale è modificabile; la firma impedisce il riuso inconsapevole dopo cambi di azioni, ψ o parametri sismici. Per il calcolo indipendente usare **Calcola globale**. La vista Verifiche → Stabilità globale ha una propria selezione delle combinazioni, superficie critica ingrandita o intero profilo, conci interrogabili e tabelle complete. Il CSV nella vista globale contiene tutti i conci e tutte le combinazioni; **Word globale** esporta la sola analisi. La relazione completa del muro include il capitolo e il disegno globale.

Le ricerche senza soluzione, con minimo sul bordo, basi in trazione o mancata convergenza non producono un esito favorevole. Una superficie già non verificata conserva il fallimento anche se la ricerca è incompleta. Il caso non drenato della raccolta dimostra la gestione di questo limite. Non sono implementati fessure di trazione, superfici non circolari, strati inclinati, pressioni idrodinamiche, degradazione ciclica, liquefazione o stabilità autonoma del versante naturale.

### Separazione del codice e migrazione GPC

- `X.Calculations/Geotechnics/SlopeGeometry.cs`: sole costruzioni geometriche e interrogazioni di profilo; usa GPC.Geometry per area e baricentro. Candidato per GPC.Geometry.
- `SlopeStability.Models.cs`: contratti indipendenti da JSON e UI; `SlopeSoil` è il candidato da condividere in GPC.Model. Nei sorgenti GPC disponibili non è presente un materiale geotecnico con φ′, c′ e cu; non si riutilizza un materiale strutturale con parametri fittizi.
- `BishopSolver.cs`: solo equilibrio dei conci; `SlopeStability.cs`: pesi, discretizzazione e ricerca. Candidati per un pacchetto GPC di geotecnica.
- `RetainingWall.GlobalStability.cs`: adattatore del documento muro e regole NTC, separati dal solutore. GPC.Model associa azioni e coefficienti. I materiali strutturali del muro restano quelli delle verifiche GPC già esistenti; nella globale serve il loro peso specifico assegnato.

Nessuna modifica è stata apportata ai repository GPC esterni.

### Controlli riproducibili

`supporto/test/GlobalStability.Checks` contiene i controlli analitici, la sensibilità della ricerca e la generazione di dieci archivi con risultati JSON, conci CSV e relazioni. `supporto/test/Desktop/GlobalStabilitySmokeChecks.cs` copre la UI, il calcolo indipendente, le matrici e gli export. Esecuzione: `dotnet run --project supporto/test/GlobalStability.Checks -c Release -- <nuova-cartella-in-supporto/artefatti/stabilita-globale>` e `ANTHEA.exe --smoke-global-stability <cartella output>`.

Le raccolte precedenti alla correzione dei cerchi tangenti sono archiviate in `supporto/SUPERATI/artefatti/stabilita-globale`. I documenti correnti sono elencati in `supporto/artefatti/stabilita-globale/DOCUMENTI-CORRENTI.md`, disponibile anche in PDF. La serie interna ANTHEA e i confronti parziali MAX sono distinti; le prove MAX sono sospese su richiesta dell'utente.

Il confronto con MAX 16 è documentato separatamente dai test interni: prima si confronta lo stesso cerchio, poi la ricerca, uniformando fattori, quote e discretizzazione. `supporto/test/MaxRetainingWall.Compare` ricalcola un cerchio letto da MAX e conserva scarti, fattori e tutti i conci. Richiede il file nativo MAX e il file di riscontro: non genera valori MAX sintetici. Lo stato del confronto è riportato nel rapporto di controllo della raccolta; la sola presenza dei dieci archivi ANTHEA non indica che siano già stati validati con MAX.

### Invio al modulo c.a. e relazione Word

In Verifiche, selezionare una riga strutturale o una stazione nella tabella delle sollecitazioni e premere **Apri sezione in c.a.**. Si copiano fascia di 1000 mm, spessore locale, materiali, armatura effettiva della zona e tutte le combinazioni disponibili nella stessa stazione. N diventa negativo a compressione; Mx è associato a Vy. SISMA ed ECCEZIONALE vanno nel gruppo Plastico (SLU) con lo stato originale nel nome. Sono conservati anche esposizione, viscosità e parametri di fessurazione. È una copia indipendente. In un progetto nasce un nuovo foglio nella stessa sezione; un muro autonomo resta aperto e la sezione c.a. si apre in una nuova finestra. Il trasferimento non è disponibile per muri a gravità o mensole senza reazioni valide.

**Relazione Word** esporta input, stratigrafia, armature, matrice completa, audit delle spinte, equilibrio, inviluppi, sollecitazioni alle radici, metodi e limiti. Include la sezione con i carichi e i diagrammi N/M/V del fusto e delle mensole per la combinazione selezionata; le didascalie identificano il caso rappresentato. JSON e CSV conservano i controlli campionati; JSON contiene anche l'intera matrice e i dettagli. I controlli non disponibili restano espliciti nell'inviluppo.

### Archivi e verifiche

Schema v2 esteso: layers conserva monte; valley contiene modalità di Hlib, collegamento, strati e passiva; interfaces contiene le modalità degli attriti e φcv. I vecchi archivi mantengono terreno di valle assente, δ muro=0 e δ base assegnato. global_stability ammette due colonne profonde con confine verticale. I nuovi campi sono opzionali negli archivi precedenti. Gli archivi v1 restano leggibili e vengono migrati nel workspace, conservando la correlazione q/H/N nel gruppo legacy. Il contenitore loads resta per compatibilità e non alimenta il motore v2. Ogni componente in extensions e le tipologie future bloccano il calcolo finché non esiste il relativo motore.

Supporto: test in `supporto/test/RetainingWall.Checks` e `supporto/test/Desktop/RetainingWallSmokeChecks.cs`; output in `supporto/artefatti/muri_sostegno/revisione-input`. Comandi:

La revisione del sisma guidato e dello scambio terreno è verificata in `supporto/artefatti/muri_sostegno/sisma-terreno`: controlli numerici, flussi dell’interfaccia e relazione Word con dati del sito. La precedente verifica della scala colori è in `supporto/artefatti/muri_sostegno/tassi-sisma`.

Esito 28/09/2026: 163 controlli numerici, 41 controlli dell’interfaccia e 988 controlli dell’audit progetto superati; compilazione Release senza errori né avvisi. Relazione del caso sismico convertita in PDF (10 pagine) e verificata visivamente.

```powershell
dotnet run --project supporto/test/RetainingWall.Checks -c Release -- supporto/artefatti/muri_sostegno/revisione-input/numerica
dotnet build X.Desktop -c Release
X.Desktop/bin/Release/net8.0-windows/ANTHEA.exe --smoke-retaining-wall supporto/artefatti/muri_sostegno/revisione-input/ui
```

### Fonti

- [NTC 2018, §§4.1, 6.2.4, 6.5 e 7.11](https://www.gazzettaufficiale.it/eli/id/2018/2/20/18A00716/sg).
- [JRC, Geotechnical Design Worked Examples](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/2013_06_WS_GEO.pdf).
- [Wood 1973, Earthquake-induced soil pressures on structures](https://authors.library.caltech.edu/records/48499-83239).
- [Yi 2013, Seismic Design of Restrained Rigid Walls](https://www.cfms-sols.org/sites/default/files/Actes/3521-3524.pdf).
- [USACE EM 1110-2-1902, Slope Stability](https://www.publications.usace.army.mil/Portals/76/Publications/EngineerManuals/EM_1110-2-1902.pdf), riferimento del metodo Bishop semplificato.

### Revisione del 30 settembre 2026 e riproduzione

La revisione è accompagnata da 213 controlli numerici del muro (50 nuovi su due colonne e attriti), 55 controlli Bishop, prove WPF e un esempio salvato. Gli esiti dettagliati e i percorsi sono in `supporto/artefatti/muri-due-colonne-20260930/CONTROLLO.md` e nel PDF omonimo. La nuova geometria del terreno è in `RetainingWall.SoilGeometry.cs`, separata dalle leggi del terreno in `RetainingWall.Soils.cs`. Sono candidati per una futura estrazione in GPC; nessun repository GPC esterno è stato modificato. Le nuove opzioni non sono ancora oggetto di confronto MAX.


### Percorso globale guidato: controllo del 30 settembre 2026

La revisione successiva ha superato 213 controlli numerici del muro, 61 della stabilità globale e 30 del percorso e dei disegni in memoria, senza aprire finestre né usare mouse o tastiera. Il rapporto è `supporto/artefatti/globale-guidata-20260930/CONTROLLO.md`, anche in PDF. Per ripetere i controlli senza desktop usare `ANTHEA.exe --check-global-guidance-offscreen <nuova-cartella-output>`. I controlli numerici globali ammettono `--checks-only` per omettere le relazioni e le istruzioni duplicate degli esempi. Il motore Bishop e la geometria restano separati; la proposta di ricerca è nell’adattatore del muro. Non sono stati eseguiti nuovi confronti numerici MAX.


### Portanza sismica cedimenti spostamenti e armature Rev07

La revisione 07 aggiunge i calcoli dei punti 2, 3 e 5 nel campo dichiarato: inerzia del terreno nella portanza sismica; cedimenti e spostamenti; dettagli e predimensionamento delle armature e modelli strutturali per gravità. Rimangono le due schede Input e Verifiche. Il report Word contiene input, ipotesi, coefficienti, risultati e distinta delle barre; una quinta figura mostra le armature della sezione.

### Portanza sismica

In Terreno aprire Portanza sismica. Con Da sito si usa ah/g=ag/g·Ss·St, prima della riduzione β del muro; av/g=±0,5ah/g. In alternativa assegnare entrambe le accelerazioni. Il fattore γRD è modificabile: 1 per sabbia medio densa, 1,15 per sabbia sciolta asciutta. Non è un valore ricavato automaticamente dal solo angolo di attrito.

Si applica EN 1998-5:2004 allegato F alla fondazione nastriforme su terreno granulare asciutto, omogeneo e con base ruvida. Nmax=0,5γ(1−av/g)B²Nγ, con Nγ=2(Nq−1)tanφd. Si trascura il contributo favorevole del ricoprimento. N, V e M sono normalizzati con γRD·γR; F=γRD·ah/(g tanφd). Il γR della combinazione è applicato separatamente e dichiarato nella relazione.

Il dominio usa a=c=0,92; b=d=1,25; e=0,41; f=0,32; m=0,96; k=1; k′=0,39; cT=1,14; cM=c′M=1,01; β=2,90; γ=2,80. La somma dei termini di interazione deve essere ≤1, con 0<N̄<(1−0,96F)^0,39. La capacità è cercata lungo il raggio N,V,M: il tasso η è l’inverso del moltiplicatore limite, non il valore della funzione di interazione. Non si applicano una seconda volta larghezza efficace e fattori di inclinazione.

In Verifiche scegliere una combinazione SISMA e Portanza sismica nel riepilogo. Sono leggibili Nmax, F, N̄, V̄, M̄, limite verticale, interazione, tasso ed esito. Un’accelerazione mancante, un terreno fuori campo o una risultante non ammissibile restano esplicitamente non verificati.

### Cedimenti e spostamenti di esercizio

In Terreno attivare Calcola cedimenti finali e inserire, a partire dal piano di posa, nome, spessore e modulo edometrico M di ciascuno strato. M è espresso in kPa: per esempio 30 MPa corrispondono a 30000 kPa. Non viene dedotto da φ o riempito con un valore presunto. La pressione del terreno rimosso è il carico geostatico eliminato con lo scavo, da valutare nel modello scelto; zero è una scelta esplicita.

Si integra s=∫Δσz/M dz con tensioni Boussinesq di una striscia infinita e pressione di contatto lineare. Il calcolo è ripetuto a valle, al centro e a monte. La profondità deve arrivare a Δσz≤10% del carico netto oppure a un substrato rigido documentato. Viene controllata anche la convergenza numerica. Profili insufficienti non producono un esito favorevole né uno spostamento totale valido.

È un cedimento finale con moduli costanti assegnati: non ricostruisce tempi di consolidazione, OCR, scarico e ricarico, variazione di M con le tensioni o degrado ciclico. La rotazione θ=(smonte−svalle)/B deriva dal profilo libero; non è una soluzione accoppiata della fondazione rigida.

Per Calcola spostamenti in testa servono anche la rigidezza orizzontale di fondazione K per metro di muro, in kN/m², e il limite scelto. Il fusto in c.a. usa curvature delle sezioni fessurate GPC con viscosità assegnata. La doppia integrazione fornisce u del fusto con base fissa; la stima disaccoppiata totale è utesta=ufusto+H/K−θHmuro. Il termine di rotazione conserva il segno. Le curvature mancanti impediscono il risultato. La gravità usa il modello elastico del materiale nel campo senza trazione.

Limiti iniziali modificabili: 25 mm per cedimento, 0,002 rad per rotazione e 20 mm per spostamento in testa. Sono valori di avvio da valutare per l’opera, non limiti normativi universali. In Verifiche scegliere Cedimenti e spostamenti per la tabella per combinazione, i contributi degli strati e le curvature.

### Spostamenti permanenti Newmark

In Azioni aprire Spostamenti permanenti e aggiungere una storia. Scegliere SLD o SLV, inserire ky/g, fattore di scala e limite di spostamento. ky/g è la soglia di inizio scorrimento del muro, da ricavare da un’analisi di equilibrio: non coincide con ag/g e non viene dedotta automaticamente dal coefficiente kh.

Importare un CSV a due colonne separate da punto e virgola: tempo in secondi e accelerazione verso valle in g. È ammessa una prima riga t;a_g e il separatore decimale italiano. I tempi devono essere crescenti. Confermare che storia e scala siano compatibili con sito e stato limite. I campioni restano salvati nel file del muro.

Il blocco rigido scorre in una sola direzione. L’integrazione dei tratti lineari di a(t)−ky·g tiene conto degli attraversamenti della soglia, dell’arresto e della coda finale a terreno fermo. Wood è escluso perché presuppone un muro vincolato. Il risultato riguarda ciascuna storia; la scelta e la conformità normativa dell’insieme degli accelerogrammi devono essere documentate. Lo SLD non viene ricavato dal solo ag/g SLV.

### Armature e comando Calcola armature

In Geometria si possono mantenere le facce simmetriche o assegnare due armature indipendenti. La prima faccia è monte nel fusto e inferiore nelle solette; la seconda è valle nel fusto e superiore nelle solette. Rimangono disponibili le due zone verticali separate da h₁.

Ogni zona contiene barre principali, diametro e passo delle secondarie, lunghezza di ancoraggio, sovrapposizione e mandrino. Zero nelle lunghezze significa calcolo automatico, non lunghezza nulla. Il pannello dei dettagli espone aggregato, aderenza, vita nominale, tolleranza del copriferro e collegamenti della giunzione.

Calcola armature cerca diametri e numeri interi di barre entro i limiti impostati. Ogni candidato viene controllato con GPC a N–M, a taglio e in SLE; la proposta usa armature simmetriche per zona, poi modificabili. L’area stimata dalla flessione serve soltanto a scartare candidati impossibili. La verifica finale include i dettagli: una sezione resistente può avere una piega o una giunzione che non entra. In tal caso l’esito lo segnala e può occorrere aumentare lo spessore. La ricerca è interrompibile. Premere Applica proposta per sostituire le barre inserite; prima di applicare restano conservate.

Il predimensionamento usa ancoraggi a fyd e nessuna riduzione favorevole dei coefficienti di forma o confinamento. fbd deriva dalle proprietà GPC e dalle condizioni di aderenza. Le giunzioni sono alla stessa quota, quindi lo schema richiede il 100% delle barre giuntate e numeri compatibili nelle due zone. Si controllano lunghezza comune, interferro tra coppie, ingombro, area e passo dei collegamenti. Il mandrino considera anche la pressione nel calcestruzzo all’interno della piega.

In Vista dei risultati scegliere Armature: si vedono i percorsi delle barre, le pieghe, la fascia di sovrapposizione e le marche. Le barre giuntate sono affiancate lungo lo sviluppo del muro; le proiezioni sono leggermente distanziate sul disegno per leggibilità. Dettagli armature riporta la distinta, fbd, lunghezze richieste e usate, mandrini, quantità e tutti i controlli. I pesi sono stime per metro comprensive di ancoraggi, giunzioni e secondarie. Restano da definire il disegno esecutivo, i giunti di costruzione, i bordi lungo il muro, le interferenze tridimensionali e gli sfridi: la vista non è una distinta di officina.

### Gravità in calcestruzzo o muratura

In Materiali scegliere Calcestruzzo non armato oppure Muratura. Il primo usa fck e proprietà GPC, con compressione e taglio NTC 4.1.11 e fct1d=0,85 fctk,0.05/γc. Per muratura occorrono fk, fvk0, limite caratteristico a taglio, γM, fattore di confidenza e modulo elastico; non si possono usare automaticamente le resistenze del calcestruzzo.

Il fusto è una mensola libera: lunghezza efficace almeno 2H, imperfezione almeno H/200, rigidezza EI minima e amplificazione 1/(1−N/Ncr). La verifica rimane nel campo senza trazione e N<0,8Ncr. Se queste condizioni non sono soddisfatte serve un modello non lineare e l’esito non è dichiarato favorevole. Per muratura si controllano blocco compresso 0,85fk/(γM·FC) e scorrimento dei giunti; la resistenza a trazione è nulla. Le mensole di fondazione dello stesso materiale sono controllate anche a trazione, quindi una mensola in muratura può richiedere una diversa soluzione costruttiva.

La modalità Resistenze assegnate conserva la compatibilità con i file precedenti e i relativi controlli elastici; non diventa automaticamente una verifica normativa completa.

### Esempio ripercorribile e rapporto

Aprire supporto/artefatti/muri-completamento-20260930/interfaccia-finale/esempio-completo.anthea. Il modello dimostrativo ha H=3 m, B=3 m, due zone di armatura, terreno deformabile di spessore 25 m con M=30000 kPa, sisma da sito e una storia triangolare sintetica. Questi dati servono a riprodurre il test e non descrivono un sito reale. La storia sintetica non è un accelerogramma normativamente qualificato.

Nella stessa cartella sono presenti relazione Word e PDF, figure della sezione e risultati JSON. Il rapporto CONTROLLO.md e PDF nella cartella principale dell’attività descrive test, correzioni e limiti. I confronti MAX rimangono sospesi: nessuna delle nuove funzioni è dichiarata validata contro MAX 16.

I nuovi motori ShallowFoundationSeismic, FoundationSettlement e NewmarkSliding sono separati in X.Calculations/Geotechnics; l’adattatore del muro è RetainingWall.Serviceability. Geometria delle barre e predimensionamento sono separati dall’interfaccia. Materiali, equilibrio e tensioni delle sezioni riutilizzano GPC. Sono candidati per una successiva estrazione nelle librerie GPC; nessun repository GPC esterno è stato modificato.

Fonti: [JRC Eurocode 8 Worked Examples](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/EC8_Seismic_Design_of_Buildings-Worked_examples.pdf), §4.8; [JRC Eurocode 2 Detailing](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/05_EC2WS_Arrieta_Detailing.pdf); [USGS Newmark](https://pubs.usgs.gov/sir/2007/5196/sir2007-5196_text.pdf); NTC 2018 §§4.1.11 e 7.8.2.2.3; USACE EM 1110-1-1905, 2025.


## TEORICA A17 — Calcestruzzo ordinario — normative e verifiche di sezione

Aggiornamento: 28 settembre 2026. Il selettore propone NTC 2018, Model Code 2010,
EN 1992-1-1 e le varianti UNI, DIN, DS e NS. CNR-DT 204 e TR34 restano leggibili
negli archivi storici, ma non sono proposti per nuovi calcoli: FRC e pavimentazioni
sono esclusi da questa attività su indicazione del progettista.

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

Per fonti, confronti indipendenti e comandi vedere
[validazione delle normative CA](guida-teorica-anthea.md).


## TEORICA A18 — Palo singolo: capacità portante orizzontale

Dal 30 settembre 2026 è disponibile anche **Stratificato** nelle
opzioni avanzate: diagrammi locali, equilibrio globale e reazioni distribuite,
anche con alternanze coesivo/granulare. Equazioni, differenze rispetto a Broms,
limiti e test sono nella [guida del metodo stratificato](guida-teorica-anthea.md),
con PDF omonimo. Le sezioni seguenti descrivono il metodo **Broms** mantenuto
per compatibilità; le esclusioni delle sequenze miste si riferiscono a tale metodo.

### Stato e campo di applicazione

Modulo `geo_palo_orizzontale`, motore `Broms-ANTHEA-1`, C#/.NET 8.
La resistenza della sezione c.a. è calcolata tramite GPCChecker.Concrete.
La schermata riprende i sette pannelli del palo verticale: dati, modello,
coefficienti, verifica, stratigrafie, profilo, momento plastico/resistente.
I diagrammi sono accessibili da **Diagrammi e dettagli**.

La revisione del 30 settembre 2026 aggiunge le viste coordinate **Terreno e
tensioni** ed **Equilibrio del palo**: sigma_v, u, sigma'_v, pressioni laterali
equivalenti q=p/D, reazioni p, taglio V, momento M con limiti My e risultante
limite Q. Strati, falda e quote caratteristiche condividono la stessa scala.
Si rappresenta tutta L, con il completamento sotto la cerniera interna
tratteggiato su fondo grigio; l'anteprima compatta conserva il solo ramo superiore.
Si tratta dello stato alla capacità Hu, non delle tensioni sotto HEd.
Le figure vengono incluse nella relazione Word su pagine orizzontali; il CSV
include anche la diagnostica del terreno. Le tensioni locali non sono ridotte
con i coefficienti globali xi e gamma_R.

Implementati e confrontati con soluzioni analitiche: terreno omogeneo granulare
drenato o coesivo non drenato; testa libera/impedita; corto, intermedio e lungo
ove applicabili. Estensione a strati della stessa famiglia e falda interna:
**sperimentale**, verificata per equilibrio e recupero dell'omogeneo, senza
validazione indipendente su stratificazioni reali.

Esclusi: sequenze miste coesivo/granulare, granulare c–φ, testa impedita fuori
dal piano campagna, momento indipendente da H, spostamenti, gruppi, ciclicità,
taglio, secondo ordine, precompressione. Il percorso normativo NTC/EC2 resta
incompleto; nessuna conformità automatica.

### Inventario delle fonti

| Materiale | Uso effettivo |
| --- | --- |
| Viggiani, Fondazioni, PDF fornito, 274 pagine a due facciate | Fonte principale: pp. stampate 400–415, PDF205–212 (conteggio da 1); formule controllate visivamente. |
| Lancellotta, Geotecnica, seconda edizione, PDF fornito, 270 pagine | Consultazione indice e ricerca nel testo OCR; non individuata una trattazione operativa di Broms equivalente a Viggiani. Nessuna formula implementata attribuita a questo testo. |
| Codice originario dell'utente, `Checker/PileCalculator.cs`, righe 256–385 e 610–678 | Algoritmo orizzontale e ricerca della profondità analizzati criticamente. |
| Codice originario dell'utente, `Test/PileHorizontalBearingCapacityTest.cs` | Otto casi omogenei, senza fonte indipendente dei valori attesi. Nessun test multistrato. |
| NTC, Circolare, EN1997 | Testi non allegati: nessun coefficiente attribuito automaticamente a tali norme. |

I libri restano su X:. Le scansioni locali di verifica non vengono distribuite.
Viggiani non ha restituito testo ricercabile con l'estrattore disponibile: la
trascrizione è stata controllata mediante lettura visiva, non presentata come OCR.
Riferimenti originali identificati: Broms (1964), Lateral Resistance of Piles in
Cohesive Soils, DOI 10.1061/JSFEAQ.0000611, e Lateral Resistance of Piles in
Cohesionless Soils, DOI 10.1061/JSFEAQ.0000614. Implementazione riferita alle
equazioni di Viggiani sotto elencate.

Ulteriori riferimenti consultabili nella scheda **Riferimenti** dei risultati
e riportati nella relazione: J. Wood (2021), Cantilever Pole Retaining Walls,
New Zealand Geotechnical Society, Geomechanics News 101,
https://www.nzgs.org/libraries/cantilever-pole-retaining-walls/ (§§2.2-2.3,
Broms semplice e modificato); FHWA (2018), Geotechnical Engineering Circular
No. 9, FHWA-HIF-18-031, https://www.fhwa.dot.gov/engineering/geotech/pubs/hif18031.pdf
(§§6.3 e 6.5, p-y e campo applicativo di Broms).
L'attribuzione delle ipotesi e i limiti dell'estensione sono dettagliati nella
guida stratificata. Consultazione online: 30 settembre 2026.

### Specifica meccanica e convenzioni

Palo circolare, verticale, D diametro, L lunghezza infissa. z=0 al piano campagna,
z positivo verso il basso. H positiva applicata a z=−e, e≥0. N positiva a
compressione, costante mentre H cresce. Testa impedita: forza e vincolo a z=0,
e=0. La capacità della struttura di vincolo di sviluppare il momento necessario
non è verificata. My costante lungo il fusto e uguale nei due versi.

Si assume disponibile la rotazione plastica richiesta; la duttilità non è
verificata. HEd serve al confronto finale, non determina Hu. Il momento
applicato è H·e. Un `momento_applicato` indipendente non nullo viene rifiutato.

p(z), in kN/m, positiva se opposta a H, è una forza distribuita, non una pressione
in kPa. Leggi limite (Viggiani p.400, PDF205):

- Coesivo: p_lim=0 per z<1,5D; p_lim=9CuD al di sotto. La zona nulla è riferita
  al piano campagna, non ripetuta per strato.
- Granulare: p_lim=3KpDσ′v; Kp=(1+sinφ′)/(1−sinφ′). Nell'omogeneo σ′v=γ_eff z.
- Estensione ANTHEA: σ′v integrata dagli strati sovrastanti, con γ sopra falda,
  γsat−9,81 sotto. σ′v continua alle interfacce; p_lim può saltare se cambia
  Kp/Cu. Non si mediano parametri e non si sommano capacità di singoli strati.
  Falda interna nel granulare attiva automaticamente la modalità sperimentale.

Q(z)=∫₀ᶻ p_lim(s)ds; S(z)=∫₀ᶻ s p_lim(s)ds; A(z)=zQ(z)−S(z).
Integrali analitici per tratti costanti/lineari. V=H−∫p;
M=M0+Hz−∫(z−s)p(s)ds. V e M continui alle interfacce ordinarie.

### Coesivo

z_f soddisfa Q(z_f)=H; nell'omogeneo z_f=1,5D+H/(9CuD). Nel tratto inferiore
[z_f,t], +p_lim fino a b, −p_lim dopo b, con Q(b)=[Q(z_f)+Q(t)]/2: risultante
nulla. Coppia C(z_f,t)=S(t)+S(z_f)−2S(b), pari a 9CuD(t−z_f)²/4 nell'omogeneo.
L'estensione di tale coppia a strati è una scelta ANTHEA sperimentale.

| Vincolo / meccanismo | Equilibrio | Viggiani, pagine stampate / PDF |
| --- | --- | --- |
| Libera corto | He+S(z_f)=C(z_f,L) | eq.13.23–13.26, pp.400–402 / 205–206 |
| Libera lungo | He+S(z_f)=My; C(z_f,t)=My, t≤L | eq.13.27–13.29, p.403 / 206 |
| Impedita corto | H=Q(L), M0=−S(L), S(L)≤My | eq.13.30–13.32, pp.405–407 / 207–208 |
| Impedita intermedio | −My+S(z_f)=C(z_f,L), M0=−My | eq.13.33–13.35, p.407 / 208 |
| Impedita lungo | S(z_f)=2My, M0=−My, C(z_f,t)=My | eq.13.36, pp.407–408 / 208–209 |

### Granulare

| Vincolo / meccanismo | Equilibrio | Viggiani, pagine stampate / PDF |
| --- | --- | --- |
| Libera corto | H=A(L)/(L+e), M0=He | eq.13.37–13.38, p.410 / 210 |
| Libera lungo | He+S(z_f)=My, Q(z_f)=H | eq.13.39–13.43, pp.410–411 / 210 |
| Impedita corto | H=Q(L), M0=−S(L) | eq.13.44–13.45, p.413 / 211 |
| Impedita intermedio | H=[My+A(L)]/L, M0=−My | eq.13.46, p.413 / 211 |
| Impedita lungo | S(z_f)=2My, M0=−My | eq.13.47, p.415 / 212 |

Corto/intermedio: F=Q(L)−H concentrata al piede, diretta come H, secondo la
semplificazione di p.409, PDF209. Registrata separatamente, produce un salto
nel taglio e non viene mascherata come pressione distribuita finita.

Nel lungo si completa il diagramma prolungando +p_lim fino alla radice t≥z_f
di M0+Ht−A(t)=0, con F=Q(t)−H a t. È un completamento **idealizzato e non
univoco**, scelto da ANTHEA per esplicitare l'equilibrio sotto la cerniera;
non è un'analisi elastoplastica dell'interazione. La capacità è fissata dal
tratto superiore secondo Broms. Non si impone un limite di pressione locale
alla F concentrata: non è una verifica puntuale del terreno.

### Scelta e controlli

Si sceglie il minimo dei candidati attivabili, senza soglie empiriche L/D.
Le alternative che superano il primo limite non vengono dichiarate
contemporaneamente ammissibili. Si controllano t≤L, |M|max≤My, equilibrio
finale di forze e momenti. Nessuna soluzione apparentemente valida in caso di errore.

Bisezione con massimo 100 iterazioni, tolleranza richiesta default 1e−8,
intervallo ammesso 1e−12…1e−5. Il motore affina la tolleranza a min(richiesta, 1e−12)
per max(1, ampiezza iniziale dell'intervallo). L'inversione di Q è analitica per tratto.
Controlli finali di
equilibrio 1e−5 sulle scale max(1,Q(L)) e max(1,My,Q(L)L). Diagrammi fino a
20.000 intervalli, con nodi aggiunti a strati, falda, cerniere, inversioni ed
estremi. Il passo grafico non cambia la capacità. φ′<60° è un controllo d'input
del software, non una prescrizione normativa.

### Momento della sezione

Origini esplicite: `Sezione c.a.` o `Manuale`. La seconda richiede valore e
natura/provenienza. Il pulsante di calcolo non sovrascrive il valore manuale.
N assegnata deve essere coerente con la fonte manuale. Il numero di barre nel
calcolo automatico deve essere pari (4–512), per mantenere la simmetria nel
piano di flessione senza introdurre un momento ortogonale non richiesto.

Calcolo automatico: `HorizontalConcreteSection` usa il motore comune
GPCChecker.Concrete, come il modulo cemento armato. Diametro e N provengono
dai dati del palo; la compressione geotecnica positiva è convertita in N negativa
per Checker. Si valuta la resistenza a N costante nei due versi Mx e si prende
il minimo dei valori assoluti. Il contorno circolare è discretizzato in un
poligono inscritto; geometria, armature, legami e coefficienti sono quelli inseriti.

Il risultato espone motore, direzioni, momento adottato e residuo assiale con
la relativa tolleranza di accettazione. Non si usa più il precedente confronto
tra mesh polari. Il test indipendente per strisce resta come confronto su casi
specifici. La duttilità delle cerniere non è dedotta dal solo momento resistente.

### Capacità e normativa

Hu per sondaggio; minimo governante. Questo minimo non viene chiamato
automaticamente resistenza caratteristica. HEd/Hu è un rapporto meccanico.
Rk = min(media(Hu)/ξ3; min(Hu)/ξ4); Rd = Rk/1,3, applicati automaticamente.
ξ3 e ξ4 provengono da Calcolo.Verticali, come nel palo verticale; il numero
di verticali indagate è scelto dall'utente, non dedotto dal numero di schede.
I file precedenti senza questa selezione usano una verticale (ξ3 = ξ4 = 1,70);
i vecchi divisori manuali e il relativo interruttore non sono più applicati.
Le chiavi risultato con suffisso _manuale sono mantenute per compatibilità degli export.
Non sono implementate le combinazioni delle azioni: inserire HEd di progetto.
Occorre anche verificare la natura di My e i fattori già
applicati ai materiali. La relazione dichiara sempre verifica normativa incompleta.

### Confronto critico con il codice originario

### Efficienza della palificata

Due opzioni: Manuale (0 < η ≤ 1, default 1), oppure Reese & Van Impe (foglio).
La seconda riproduce il file SMath fornito “Portanza orizzontale palo incastrato
in testa in terreno incoerente - [Viggiani cfr.13.2.5].sm”, revisione 37.
Interassi anteriore, posteriore, sinistro e destro in metri, riferiti alla direzione H.
I contributi diretti sono min(1; 0,7(s_ant/D)^0,26), min(1; 0,48(s_post/D)^0,38)
e min(1; 0,64(s_lato/D)^0,34) sui due lati.
Per ogni diagonale β = atan(s_lato/s_longitudinale) e il contributo è
sqrt(η_longitudinale² cos²β + η_lato² sin²β). η è il prodotto degli otto contributi.
Rd = η Rk / 1,3; Hu, diagrammi del palo singolo e Rk non sono ridotti.
Lo schema implica quattro vicini allineati e quattro diagonali, non una geometria
arbitraria. Come avverte il foglio, maglie fitte possono richiedere altri pali
interferenti. Il programma non determina automaticamente queste interferenze.
Le note del foglio sugli interassi non introducono ulteriori soglie nelle formule:
si riproduce il min(1; ...) effettivamente presente nelle espressioni SMath.

`externalForce` ed `eccentricity` non intervengono nel suo metodo orizzontale.
My proviene da GPC Concrete con conversione Nmm→kNm. Le capacità lunghe sono
sommate per strato riutilizzando My; γsat è usato senza la quota di falda,
mentre σv cresce con γ efficace. Il tratto 1,5D è sottratto dalla lunghezza
residua senza una correzione coerente dello spessore del primo strato.

Confronto riproducibile del solo candidato lungo coesivo: D=1 m, Cu=50 kPa,
My=450 kNm. L'espressione del riferimento
(-13,5+sqrt(182,25+36My/(CuD³)))CuD² restituisce 450 kN per uno strato;
sommandola per due strati identici restituisce 900 kN prima dei fattori.
ANTHEA recupera 450 kN per testa impedita, indipendentemente dalle interfacce.
Non è presentato come confronto completo con l'eseguibile.

La DLL GPC Concrete non è presente al HintPath del progetto fornito. I valori
524/476/491/441/520 kN dei test dipendono da un My esterno non riportato:
non dichiarati riprodotti, nessuna calibrazione per farli coincidere.
Il confronto eseguibile completo resta da fare con dipendenze disponibili.

### Prove riproducibili

`supporto/test/X.Verifiche/HorizontalChecks.cs`: D=1 m, Cu=50 kPa oppure φ′=30°,
γ=18 kN/m³ (Kp=3), e=0.

| Terreno / vincolo / meccanismo | L [m] | My [kNm] | Hu attesa [kN] |
| --- | ---: | ---: | ---: |
| Coesivo libera corto | 2,5+√8 | 10000 | 450 |
| Coesivo libera lungo | 10 | 900 | 450 |
| Coesivo impedita corto | 2,5 | 10000 | 450 |
| Coesivo impedita intermedio | 5,5 | 1800 | 900 |
| Coesivo impedita lungo | 10 | 450 | 450 |
| Granulare libera corto | 2 | 1000 | 108 |
| Granulare libera lungo | 10 | 432 | 324 |
| Granulare impedita corto | 2 | 1000 | 324 |
| Granulare impedita intermedio | 2 | 108 | 162 |
| Granulare impedita lungo | 10 | 216 | 324 |

Tolleranza relativa scalata 1e−6 (più larga della bisezione, non percentuale
di progetto). Residui benchmark ≤0,001 kN/kNm. Sezione a strisce: tolleranza
0,4% tra quadrature diverse. Ulteriori test: eccentricità, falda, interfacce
fittizie, passo grafico, input fuori campo, equilibrio, limiti My, fattori,
immutabilità input, roundtrip, CSV e XML della relazione.

### Avvio e file

`Avvia ANTHEA.cmd` → Moduli singoli → Palo → Capacità portante orizzontale.
`Compila.cmd` pubblica la versione, `Verifica.cmd` esegue tutta la suite.

```powershell
dotnet X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll --smoke-horizontal supporto/artefatti/verifiche_orizzontale_ui
dotnet run --project supporto/test/X.Verifiche -c Release -- --calcola supporto/esempi/palo_orizzontale.json supporto/artefatti/verifiche_orizzontale_risultato.json
```

Lo smoke crea schermate e file di verifica, poi chiude l'app. Usare sempre una
cartella nuova. Contenitore archivio versione 1, dati `versione_orizzontale: 1`.
I risultati non vengono riusati all'apertura: è necessario ricalcolare.


## TEORICA A19 — Palo orizzontale in terreno stratificato

Sviluppo dell'approccio stratificato - revisione 3, 30 settembre 2026. Riferimenti e diagrammi del terreno.

### Scopo e scelta del metodo

Il metodo **Stratificato** conserva l'impostazione del codice dell'utente: costruzione del diagramma per strato, ricerca della profondità a taglio nullo e confronto fra palo corto, intermedio e lungo. Le capacità dei singoli strati sono sostituite da un unico equilibrio del palo, con un solo momento resistente My. Il calcolo ammette anche alternanze fra terreni coesivi non drenati e granulari drenati.

In ANTHEA: **Palo orizzontale > Dati generali > Opzioni avanzate > Metodo di calcolo > Stratificato**. Lo stesso motore è disponibile per il micropalo orizzontale CHS. La scelta è conservata nel file; i file precedenti continuano a usare Broms.

Il metodo resta sperimentale. Le prove verificano equazioni, casi analitici e proprietà numeriche; non costituiscono una validazione sperimentale su pali in terreni reali. La chiusura delle reazioni nella parte inferiore del palo è un'ipotesi esplicita del modello.

| Metodo orizzontale | Terreni | Chiusura inferiore |
| --- | --- | --- |
| Broms | Coesivo oppure granulare; strati della stessa famiglia | Coppia distribuita nel coesivo; risultante concentrata nel granulare |
| Stratificato | Coesivo, granulare o misto | Reazioni distribuite limitate dalla resistenza locale in tutti i terreni |

Nel coesivo omogeneo si recuperano i risultati di Broms implementati in ANTHEA. Nel granulare lungo si recupera il ramo superiore di Broms quando il palo è sufficientemente lungo per chiudere l'equilibrio distribuito. Nel granulare corto e intermedio la diversa ipotesi al piede produce capacità diverse: non si impone una coincidenza artificiale con la soluzione a forza concentrata.

### Parametri e unità

L e D sono lunghezza infissa e diametro in m; z è misurata verso il basso dal piano campagna; e è l'altezza della forza H sopra tale piano. H è in kN, My in kNm. N è positiva a compressione e rimane costante durante l'incremento di H. My può essere assegnato oppure ricavato dal motore della sezione c.a. o CHS.

Ogni strato conserva spessore, tipo, Cu, angolo di attrito e pesi di volume. Nei profili misti occorrono i pesi anche degli strati coesivi: contribuiscono alla tensione efficace nei granulari sottostanti. La stratigrafia deve coprire tutta L; l'ultimo strato viene troncato al piede. Nessuna interfaccia viene traslata dalla zona superficiale 1,5D.

<!-- pagebreak -->

### Diagramma limite e integrali

p_lim è una **forza per unità di lunghezza**, espressa in kN/m. Non è una pressione in kPa. Per ogni tratto coesivo:

```
p_lim(z) = 0                  se z < 1,5 D
p_lim(z) = 9 Cu_i D           se z >= 1,5 D
```

Il taglio superficiale è globale e si applica soltanto ai tratti coesivi. Un nuovo strato coesivo profondo non genera una nuova zona nulla. Per ogni tratto granulare:

```
Kp_i = (1 + sin(phi_i)) / (1 - sin(phi_i))
p_lim(z) = 3 Kp_i D sigma'_v(z)
sigma'_v(z) = integrale da 0 a z di gamma_eff(s) ds
```

Si usa gamma sopra falda e gamma_sat - 9,81 sotto falda. La tensione efficace è continua alle interfacce; p_lim può saltare quando cambiano tipo di terreno, Cu o Kp. Gli intervalli sono spezzati a interfacce, falda, 1,5D e piede.

Definendo Q(z) = integrale di p_lim da 0 a z e S(z) = integrale di s p_lim(s) da 0 a z, le integrazioni sono analitiche. In un segmento che inizia a quota a, con p_lim = p0 + k x e x = z - a:

```
DeltaQ = p0 x + k x^2 / 2
DeltaS = a DeltaQ + p0 x^2 / 2 + k x^3 / 3
```

La profondità z_f soddisfa **Q(z_f) = H**. L'inversione si esegue sul tratto che contiene la risultante richiesta, saltando gli intervalli ad area nulla. Per k diverso da zero si usa x = 2 DeltaQ / [p0 + sqrt(p0^2 + 2 k DeltaQ)], evitando la sottrazione fra numeri quasi uguali.

### Coppia inferiore

Fra z_f e la fine delle reazioni t, si assume +p_lim fino a b e -p_lim dopo b. L'equilibrio delle forze determina Q(b) = [Q(z_f) + Q(t)] / 2. Il momento della coppia resistente è:

```
C(z_f,t) = S(t) + S(z_f) - 2 S(b)
```

Questa costruzione usa le proprietà effettive di tutti gli strati attraversati. L'estensione della chiusura distribuita ai granulari e ai profili misti è una scelta di questa implementazione, aggiunta all'approccio originario dell'utente. Non è attribuita alle formule originali di Broms.

<!-- pagebreak -->

### Meccanismi e diagrammi

| Vincolo e meccanismo | Equazione candidata |
| --- | --- |
| Libera, corto | H e + S(z_f) = C(z_f,L) |
| Impedita, corto | H = Q(L); M0 = -S(L); limite S(L) <= My |
| Impedita, intermedio | -My + S(z_f) = C(z_f,L); M0 = -My |
| Libera, lungo | H e + S(z_f) = My; M0 = H e |
| Impedita, lungo | S(z_f) = 2 My; M0 = -My |

La testa impedita richiede e = 0. Nel lungo si cerca t entro L tale che C(z_f,t) = M0 + S(z_f). La capacità è il minimo dei candidati attivabili. My viene utilizzato come limite della stessa sezione del palo, non sommato per strato.

Indicando con p la reazione con il suo segno, i diagrammi sono ottenuti dall'equilibrio:

```
V(z) = H - integrale da 0 a z di p(s) ds
M(z) = M0 + H z - integrale da 0 a z di (z-s) p(s) ds
```

Il programma controlla equilibrio finale di forze e momenti, profondità delle reazioni entro il palo e valore massimo assoluto del momento entro My. Le interfacce ordinarie possono produrre salti di p, mentre V e M restano continui. I risultati contengono entrambi i lati delle discontinuità e il diagramma limite per strato.

Il passo modifica soltanto il campionamento grafico. La ricerca delle radici usa al massimo 100 bisezioni; la tolleranza effettiva sull'intervallo è min(tolleranza richiesta, 1e-12) per max(1, ampiezza iniziale). Questo affinamento mantiene i residui entro i controlli di equilibrio anche quando si richiede una tolleranza più larga.

La finestra **Diagrammi e dettagli** e la relazione esportata mostrano tutta la lunghezza. Nei pali lunghi il completamento inferiore è tratteggiato su fondo grigio: visualizzarlo non lo rende una soluzione di compatibilità degli spostamenti. L'anteprima compatta del profilo nella schermata principale conserva il solo ramo superiore.

### Limiti applicativi

Cu è usata in condizioni non drenate nei coesivi; nei granulari si assume comportamento drenato con c' = 0. L'applicabilità contemporanea di queste condizioni va valutata per il problema. Sono esclusi strati disattivati, falda sopra il piano campagna, momento applicato indipendente da H, spostamenti, ciclicità, secondo ordine e verifica della duttilità. My è costante lungo il palo e uguale nei due versi.

La capacità Hu è distinta dalla resistenza di progetto: restano le riduzioni del modulo esistente Rk = min(media(Hu)/xi3, min(Hu)/xi4) e Rd = eta Rk / 1,3. Il nuovo modello non introduce ulteriori fattori e non completa automaticamente le verifiche normative.

<!-- pagebreak -->

### Verifiche riproducibili

I nuovi test si trovano in `supporto/test/X.Verifiche/HorizontalStratifiedChecks.cs`. Comprendono quadratura indipendente delle reazioni firmate e dei relativi momenti, interfacce fittizie, strati superficiali sottili, falda interna, eccentricità, continuità di V e M e controllo della relazione esportata.

| Caso, D = 1 m | Risultato analitico Hu |
| --- | --- |
| Coesivo, Cu=50 kPa, testa impedita, L=10 m, My=450 kNm | 450 kN, lungo |
| Coesivo 0-2 m, Cu=40 kPa; granulare 2-4 m, phi=30°, gamma=18; My=10000, testa impedita | 1152 kN, corto; M0=-3339 kNm |
| Caso precedente con falda a 1 m, gamma_sat=20 in entrambi gli strati | 870,84 kN; M0=-2448,66 kNm |
| Granulare 0-2 m, phi=30°, gamma=18; coesivo 2-10 m, Cu=50; My=778,5, testa impedita | 774 kN, lungo; z_f=3 m |
| Stesso profilo, testa libera, e=1 m, My=2331 kNm | 774 kN, lungo |
| Granulare omogeneo, phi=30°, gamma=18, L=10 m, My=216, testa impedita | 324 kN, lungo |

Le prove stratificate verificano anche il corto granulare distribuito contro una soluzione analitica distinta dalla chiusura concentrata. Per p_lim=kz, testa libera ed e=0: Hu = k L^2 [2^(-2/3) - 1/2].

Esecuzione dalla radice del repository:

```
dotnet run --project supporto/test/X.Verifiche -c Release -- --horizontal
```

Il comando esegue i test storici, quelli CHS e quelli stratificati. L'opzione `--horizontal-stratified` esegue soltanto la nuova suite. I log di questa attività sono in `supporto/artefatti/palo-stratificato-20260930/`.

### Uso dal codice

Il nuovo profilo, gli integrali e l'inversione delle risultanti sono in `X.Calculations/PaloOrizzontale.Stratified.cs`. L'equilibrio dei meccanismi resta in `PaloOrizzontale.cs`, condiviso con il metodo precedente.

```
JsonObject risultato = PaloOrizzontale.CalculateStratified(dati);
```

La funzione riceve lo stesso input del modulo orizzontale, lavora su una copia e imposta `generali.metodo_calcolo` a `Stratificato`. In alternativa si assegna quella chiave e si chiama `Calculate`. L'errore è restituito nella chiave `errore`; una stringa vuota indica calcolo riuscito.

I risultati espongono metodo, versione del motore, capacità, meccanismo, cerniere, residui, quota d'inversione e fine delle reazioni. Ogni sondaggio contiene `diagramma_limite`, con intervalli, tipo di terreno, tensione efficace iniziale, p iniziale, pendenza, risultante e momento primo.

La nuova diagnostica `diagramma_terreno` contiene tensione totale, pressione idrostatica, tensione efficace, limite locale in kPa e kN/m, risultante Q e lato dell'interfaccia. Le stesse grandezze sono disponibili nelle tabelle e nelle colonne aggiunte al CSV. I riferimenti sono in `riferimenti`, con URL e ruolo della fonte. I controlli della revisione 2 aggiungono tensioni con falda nello strato coesivo sovrastante, salti del limite locale, conversione q=p/D e pesi mancanti nel solo coesivo. Log e verifiche grafiche della revisione 2: `supporto/artefatti/palo-diagrammi-20260930/`.

<!-- pagebreak -->

### Come leggere i grafici

La vista **Terreno e tensioni** affianca la stratigrafia e tre diagrammi sulla stessa scala delle profondità:

- **Tensioni verticali**: sigma_v totale da peso proprio, pressione interstiziale u = 9,81 max(0,z-z_w), tensione efficace sigma'_v = sigma_v-u. Non si considera suzione sopra falda. I pesi degli strati coesivi contribuiscono al confinamento dei granulari sottostanti.
- **Pressione laterale equivalente**: inviluppo positivo e negativo q_lim = p_lim/D e distribuzione q = p/D adottata all'equilibrio, in kPa. q è una pressione equivalente sulla larghezza D, non la distribuzione delle tensioni sulla circonferenza del palo.
- **Reazione lineare**: inviluppo positivo e negativo p_lim e distribuzione con segno p adottata, in kN/m. Alle interfacce possono comparire salti, anche se sigma'_v è continua.

La vista **Equilibrio del palo** affianca p, V, M e Q. Q è l'integrale della resistenza limite positiva, non della reazione con segno: Q(z_f)=Hu. Il grafico M comprende i limiti +My e -My e le cerniere. Nei diagrammi V e M resta esplicito l'effetto di eventuali forze concentrate del metodo Broms.

Falda, interfacce, taglio superficiale 1,5D, z_f, inversione b e fine delle reazioni t rendono leggibili i cambiamenti. **z_f non è b**: il primo individua il taglio nullo; il secondo cambia il verso della reazione. L'interpretazione di b come centro di rotazione vale nello schema rigido del palo corto; il programma non calcola una rotazione.

I diagrammi rappresentano **lo stato limite alla capacità Hu**, non le tensioni sotto HEd e non una verifica di esercizio. I coefficienti xi e gamma_R riducono la capacità globale e non vengono applicati alle singole pressioni del grafico. Il titolo "tensioni" non implica pertanto un ulteriore diagramma locale di progetto già fattorizzato.

Nel profilo interamente coesivo la capacità non usa gamma. Se i pesi non sono disponibili, sigma_v e sigma'_v sono dichiarate non disponibili, senza sostituirle con zero e senza impedire il calcolo della capacità da Cu. La reazione limite coesiva resta leggibile.

### Esempio con falda e tre strati

D=1 m, L=10 m, testa libera, e=0, My=900 kNm. Coesivo 0-2 m con Cu=40 kPa; granulare 2-5 m con phi'=30°; coesivo 5-10 m con Cu=65 kPa. In tutti gli strati gamma=18 e gamma_sat=20 kN/m³, falda a 1 m.

All'interfaccia z=2 m: sigma_v=38 kPa, u=9,81 kPa e sigma'_v=28,19 kPa. Il limite passa da 360 kPa nel coesivo a 253,71 kPa nel granulare, mentre la tensione verticale efficace rimane continua. Poiché D=1 m, p_lim ha lo stesso valore numerico di q_lim ma unità differenti. Con D diverso da 1 i valori numerici non coincidono.

<!-- pagebreak -->

### Riferimenti e attribuzione

**Codice originario dell'utente** - `Checker/PileCalculator.cs`, metodo orizzontale e `FindDepthForForce`. È il punto di partenza per i diagrammi per strato e la ricerca della profondità a taglio nullo. L'equilibrio globale con un unico My e la chiusura inferiore sono esplicitati nell'implementazione ANTHEA.

**C. Viggiani, Fondazioni** - testo fornito, §§13.2.2-13.2.5, pp. 400-415. Fonte operativa delle leggi locali e dei meccanismi classici. Non si attribuisce al libro l'estensione ai profili misti.

**B. B. Broms (1964), Lateral Resistance of Piles in Cohesive Soils** - ASCE, Journal of the Soil Mechanics and Foundations Division, 90(SM2). DOI: https://doi.org/10.1061/JSFEAQ.0000611. Riferimento originale coesivo, ripreso attraverso Viggiani.

**B. B. Broms (1964), Lateral Resistance of Piles in Cohesionless Soils** - stessa rivista, 90(SM3). DOI: https://doi.org/10.1061/JSFEAQ.0000614. Riferimento originale granulare, ripreso attraverso Viggiani e Wood.

**J. Wood (2021), Cantilever Pole Retaining Walls** - New Zealand Geotechnical Society, Geomechanics News, n. 101, pubblicato il 22 giugno 2021. https://www.nzgs.org/libraries/cantilever-pole-retaining-walls/

I §§2.2-2.3 confrontano Broms semplice e **Broms Modified**: il secondo distribuisce le reazioni opposte sotto l'inversione e soddisfa entrambi gli equilibri. È pertinente al nostro palo corto rigido granulare. Il contributo richiama inoltre i limiti delle distribuzioni pienamente plastiche (§§2.5 e 2.8). Non valida la nostra estensione mista o il completamento dei pali lunghi. Chen e Kulhawy (1994) sono richiamati da Wood; non sono stati usati qui come fonte direttamente consultata.

**FHWA (2018), Geotechnical Engineering Circular No. 9** - Design, Analysis, and Testing of Laterally Loaded Deep Foundations that Support Transportation Facilities, FHWA-HIF-18-031. https://www.fhwa.dot.gov/engineering/geotech/pubs/hif18031.pdf

I §§6.3 e 6.5 inquadrano p-y e Broms. I modelli p-y collegano reazione e spostamento ma richiedono curve appropriate e presentano limiti applicativi, soprattutto per pali corti rigidi e grandi diametri. Servono come riferimento per un futuro confronto dell'interazione terreno-palo; non sono implementati dal presente metodo. Questo documento tecnico statunitense non sostituisce le verifiche normative italiane.

I contributi NZGS e FHWA sono stati consultati online il 30 settembre 2026. I DOI di Broms identificano i lavori originali; le formule operative sono state controllate sul testo Viggiani fornito. Nessun riferimento è presentato come validazione sperimentale dell'intero algoritmo stratificato.


## TEORICA A20 — Curve della sezione composta

La scheda **03 Curve della sezione** raccoglie M–κ a N costante e N–ε a κ
costante, insieme a σ–ε della fibra selezionata. Il calcolo è nella libreria
Checker, separato dai metodi per fasi; ANTHEA converte le unità e presenta i dati.

Si può partire dalla sezione vergine o da una fase attiva. Nel secondo caso
viene rieseguita la storia fino alla fase selezionata con le leggi non lineari,
conservando getto, ritiri e plasticità. La scelta del metodo nelle altre schede
rimane memorizzata e non cambia il significato di questa curva non lineare.

Scegliere tipo di curva, origine, quota di riferimento, N oppure κ costante,
escursione complessiva e numero di punti. La scelta di mantenere N/κ della
storia conserva il vincolo corrispondente dello stato iniziale. Gli ingressi
della vista usano kN, kNm, mm, 1/m e µε. L'escursione può avere entrambi i segni.
Nel controllo N–ε il momento risultante è anche la reazione necessaria a
mantenere la curvatura: non è imposto nullo.

Il cursore collega il punto della curva alla fibra e alla riga di risultati.
Il pannello σ–ε permette di leggere deformazione totale, imposta, meccanica e
plastica. La tabella contiene piani di deformazione, risultanti, estremi e
residui. Il CSV esporta tutti i punti e tutte le fibre con le componenti di
deformazione e lo stato plastico, oltre all'esito del percorso.

Il calcolo si avvia esplicitamente ed è annullabile. Dopo una modifica degli
ingressi la vecchia curva resta leggibile come risultato da aggiornare e non
può essere esportata come corrente. Le opzioni sono salvate nell'archivio.
In caso di limite materiale o mancata convergenza sono mostrati soltanto i
punti validi, con la causa di arresto.

Le curve usano sezione lorda e legami caratteristici. Non modellano
post-instabilità di classe 4, connessione parziale, creep nel tempo o danno
ciclico del CLS. Mesh e impostazioni dei materiali provengono dalla sezione;
le opzioni di classe 4 delle altre analisi restano conservate.

### Validazione eseguita

154 test CompositeBridge e 287 test ordinari BridgeAudit: **441 superati**.
Gli 8 nuovi test di integrazione sono inclusi nei 287; i 17 controlli della
nuova vista fanno parte del collaudo grafico separato.
Sono stati confrontati 64 stati con OpenSees 3.8.0:
29 dello storico e 35 delle curve. Il massimo scarto di σ sulle curve è
0,000109108 MPa nel modello a fibre concordato.

I difetti delle vecchie DLL sono tenuti separati: 5 casi riproducono un errore
e 10 restano ignorati; non fanno parte dei 441 esiti verdi. Dati di ingresso,
atteso/ottenuto, tolleranze, riferimenti esterni, ipotesi e limiti sono nel
documento `supporto/documentazione/Validazione_Sezione_Ponte/ANTHEA_Validazione_Sezione_Ponte_Rev01.docx`.
Gli artefatti del collaudo sono in `supporto/artefatti/ponte_curve_validazione/`.


## TEORICA A21 — Sezione da ponte con storico lineare e non lineare

Implementazione del 25 settembre 2026. Tutto il calcolo è in `Checker/GPCChecker.CompositeBridge/History`. ANTHEA contiene lettura degli ingressi, presentazione ed esportazione; i metodi precedenti restano disponibili.

### Scelta del metodo

- **Cumulativo · metodo precedente**: conserva l'algoritmo precedente, con contributi ricalcolati sulla carpenteria efficace della situazione; include taglio, pioli e dettagli locali. Massimo 20 fasi come nell'API preesistente.
- **Storico lineare**: conserva piano di deformazione al getto, deformazioni imposte e tensioni già accumulate. Fasi e ritiri senza limite numerico prefissato. Classe 4 disponibile tramite le larghezze efficaci elastiche. φ/n modifica il modulo dei nuovi incrementi; non rappresenta una legge di creep dipendente dall'età.
- **Storico non lineare**: equilibrio N–Mx a fibre con la stessa sequenza costruttiva, memoria plastica dell'acciaio e ritiro della soletta. Acciaio bilineare a incrudimento isotropo con scarico elastico; CLS sull'inviluppo tabulato Model, senza danno o isteresi ciclica. Armature con legge plastica propria. Una sola fase permette il caso di carico singolo.

Nel non lineare la sezione è **lorda**, a legami **caratteristici**. L'opzione di classe 4 resta memorizzata per il lineare ma non viene applicata. Non si attribuisce una verifica SLU di classe 4 a un calcolo plastico privo di instabilità locale. V viene registrato; taglio, interazione N–M–V, pioli e irrigidimenti non ricevono esiti dai metodi con storico. I dati accessori e il loro calcolo nel metodo precedente sono conservati.

Il non lineare offre due scelte per la viscosità: analisi istantanea esplicita (φ=0 nel calcolo, φ/n d'archivio conservati), oppure obbligo di φ=0 negli ingressi. Non viene applicata una riduzione arbitraria di E a una legge plastica. I ritiri continuano a essere deformazioni imposte anche nell'analisi istantanea.

### Risultati e uso della vista

La scheda Fasi e tensioni contiene selettore del metodo e gruppo espandibile per strisce di anima/flange/CLS e sottopassi. Due punti di integrazione per striscia. I valori iniziali sono 160/8/64 strisce e 8 sottopassi: sono impostazioni iniziali, non una garanzia di convergenza al risultato continuo.

Si riportano piani totali e incrementali, riferimento di N per fase, risultanti integrate, residui, aree efficaci, tensioni di ciascuna fibra, riferimento al getto, deformazioni imposte/meccaniche e plastiche. I diagrammi e il contouring utilizzano il campo a fibre, non una retta ricavata da My/I. I Δσ sono differenze fra stati successivi, comprese le redistribuzioni. Alle facce il diagramma non lineare usa la fibra prossima, senza estrapolare fuori dal dominio costitutivo; gli estremi integrati sono riportati esplicitamente.

Il CSV dei metodi con storico esporta tutte le fibre e gli stati delle fasi. Il JSON contiene anche gli stati plastici. Il report Word ha contenuti specifici per lo storico e non presenta forze equivalenti di ritiro o rigidezze tangenti fittizie. Le proprietà omogeneizzate sono diagnostiche ai moduli iniziali della fase.

**Stacca vista** sposta il grafico in una finestra indipendente e sincronizzata. È possibile selezionare la fase, modificare scala/contouring, massimizzare o usare F11 per schermo intero. Esc esce dallo schermo intero. Riaggancia o la chiusura della finestra riporta la vista al modulo; la chiusura del modulo chiude anche la finestra. Durante un ricalcolo resta visibile l'ultimo stato con indicazione di risultato da aggiornare.

### Affidabilità e verifiche

Il nuovo solutore ha verifiche analitiche, test di comportamento e un confronto numerico **eseguito con OpenSees 3.8.0**, indipendente dal codice Checker: 7 storie e 29 stati. Il riferimento e lo script sono in `Checker/GPCChecker.Test.CompositeBridge/Validation`, insieme a ipotesi, tolleranze e studio dei sottopassi. Lo scarto massimo misurato è 0,00084431 MPa per la tensione e 7,6281e−9 per la deformazione di fibra.

Le soluzioni analitiche e i test di regressione sono stati scritti durante questo sviluppo: non equivalgono a una validazione di terzi. I dati del confronto OpenSees sono risolti dall'eseguibile esterno e congelati nel repository; il modello di confronto e le scelte dei materiali sono stati predisposti nello stesso lavoro. Non si tratta di una validazione sperimentale o di una certificazione normativa.

La suite CompositeBridge conta 121 casi; l'audit ordinario del modulo con integrazione degli archivi e del report conta 279 casi. L'audit ordinario esclude esplicitamente i difetti noti delle vecchie librerie native (categorie KnownBug e ConstructorRegression), già documentati; non sono stati dichiarati corretti né conteggiati tra gli esiti verdi. Le prove di interfaccia e gli artefatti di questa attività sono in `supporto/artefatti/ponte_non_lineare`.

È stato corretto un difetto di convergenza del nuovo metodo emerso nel confronto: tangenti non coerenti fra fibre equivalenti alla cuspide di snervamento per arrotondamento. Il predittore iniziale è ora elastico e le iterazioni successive usano le tangenti dei materiali; nessuno stato è accettato senza equilibrio.

Prima dell'uso progettuale restano necessarie revisione indipendente del modello e verifiche applicabili alla struttura reale. In particolare non sono coperti post-instabilità plastica di classe 4, evoluzione viscosa nel tempo, comportamento ciclico danneggiato del CLS, connessione parziale o equilibrio globale della trave. Per casi nuovi confrontare discretizzazioni e sottopassi e leggere i residui. Se una fase non converge, il calcolo corrente fallisce e non sostituisce l'ultimo risultato valido con uno stato parziale.


## TEORICA A22 — Metodo della sezione da ponte da rivedere per Checker

Aggiornamento dettagli locali: [irrigidimenti, appoggi e connessione](guida-teorica-anthea.md).
I punti 1–3 sono aggiunti a valle dei risultati; in questa revisione il ciclo delle
fasi descritto qui rimane invariato. I nuovi metodi puri e i loro test sono già in Checker.

Il metodo scritto in ANTHEA è stato inizialmente separato in file dedicati senza
modifiche numeriche. Successivamente sono stati aggiunti, su richiesta, i riferimenti
di N per fase descritti sotto. Le formule delle larghezze efficaci, il ciclo,
le tolleranze e i difetti già documentati restano invariati. Non è stato ancora
trasferito in Checker. Questa separazione rende leggibili il ciclo di
classe 4, l'adattatore del solver e i risultati prima di decidere l'API di libreria.

### Ordine di lettura

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

### Percorso da esaminare insieme

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

### Confine proposto per la futura API

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

### Punti aperti preservati

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

### Prove da usare nella revisione

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


### Nuovi metodi a taglio e pioli

I metodi puri sono già in `Checker/GPCChecker.Steel/CompositeBridges`, temporaneamente inclusi da ANTHEA con collegamento ai sorgenti. Sono ricavati direttamente da NTC/EC, senza utilizzare il precedente metodo a taglio come oracolo. Riferimenti, difetti preesistenti non corretti e campo di validità: [revisione normativa](guida-teorica-anthea.md).

Aggiornamento 26 settembre 2026: i percorsi storici X.Core/BridgeSection citati nel dossier sono ora X.Calculations/BridgeSection; i metodi numerici sono in GPCChecker.CompositeBridge. Vedere migrazione-composite-bridge.md e libreria-calcolo.md per l’architettura corrente.


## TEORICA A23 — Sezione composta da ponte

Modulo `str_mista_ponte`, disponibile in **Moduli singoli → Strutture → Sezione composta**,
nel menu File e nei fogli dei progetti. Analisi elastica delle tensioni normali N–Mx
di una soletta su carpenteria saldata ad H, con trattamento locale di classe 4.

File di esempio: [sezione_mista_ponte.json](../esempi/sezione_mista_ponte.json),
con tre contributi di carico, due piattabande inferiori e due file di armature.
I valori sono dimostrativi e possono essere modificati dopo l'apertura in ANTHEA.

### Interfaccia

### Predalle: riferimento geometrico

Nel **Pannello di controllo → Geometria → Predalle** si possono attivare la predalle,
indicarne lo spessore e scegliere il riferimento della fila inferiore. Lo spessore
totale della soletta **include** la predalle; non viene aumentato automaticamente.
La predalle occupa la fascia tra y=0 e y=t_predalle. Per i ferri inferiori:

- **Estradosso predalle** (scelta iniziale quando si attiva): y=t_predalle+distanza all'asse.
- **Intradosso soletta**: y=distanza all'asse, come nei file precedenti.

La fila superiore resta a y=h_soletta−distanza dall'estradosso. Le distanze sono
all'asse della barra, non copriferri netti. Cambiare riferimento conserva il numero
inserito e quindi può spostare la fila; la quota risultante è esplicita nella UI,
nelle etichette della sezione, nelle proprietà e nei report di tutti i metodi.
Con h=250 mm, predalle=60 mm e distanza inferiore=45 mm, la fila inferiore è a
y=105 mm dal fondo (oppure a 45 mm se si sceglie l'intradosso); la fila superiore
con distanza 45 mm è a y=205 mm.

La predalle è **solo una suddivisione geometrica** della soletta omogenea esistente:
non ha materiale o armature proprie, peso aggiunto, fasi o verifiche dedicate.
Le proprietà globali e le tensioni cambiano soltanto se cambia la posizione dei
ferri. Il disegno usa una fascia distinta e un bordo tratteggiato; il contour
conserva la propria scala e campitura. Le annotazioni si disattivano con le normali
opzioni delle informazioni geometriche e delle armature.

I file precedenti hanno la predalle disattivata. Spessore e riferimento restano
memorizzati quando si disattiva l'opzione. Ogni fila può essere assente. Sono
respinti spessori non positivi o maggiori/uguali all'altezza totale, ferri a cavallo
dell'estradosso quando questo è il riferimento scelto, file sovrapposte o barre
fuori soletta. Le quote effettive sono inviate alle API esistenti di Checker/Model;
non è introdotto alcun nuovo metodo di analisi.

Test dedicati: `dotnet run --project supporto/test/X.Verifiche -c Release -- --bridge-predalle`.
Controllo UI: `supporto/scripts/Test-CalculationUi.ps1 -Suites bridge-predalle`.
I test comprendono quote numeriche note, equivalenza con le stesse barre impostate
manualmente per H, H inclinata e cassoncino nei tre metodi, report, eredità nel
progetto, input invalidi, aggiornamenti UI e persistenza.

### Organizzazione delle schede

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
sono descritti in [irrigidimenti, appoggi e connessione](guida-teorica-anthea.md).
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

### Collegamento a Model e Checker

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

### Geometria e armature

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

### Tipo di sezione: H, H con anima inclinata, cassoncino

Dal 27 settembre 2026 (CompositeBridge 1.3, Model `SectionHInclinedWeb` e `SectionSteelBox`)
il riquadro **Tipo di sezione** offre tre sezioni in acciaio.

- **H saldato**: la sezione precedente; unica con la seconda piastra inferiore.
- **H con anima inclinata**: piattabande centrate sulle estremità dell'anima.
  - *Scostamento anima al piede* è lo spostamento orizzontale del piede rispetto alla
    sommità (positivo verso destra).
- **Cassoncino**: due anime simmetriche, una piattabanda superiore su ciascuna e un fondo;
  la soletta chiude la cella.
  - *Interasse anime in sommità*: distanza tra gli assi delle anime sotto le piattabande
    superiori.
  - *Scostamento*: rientro di ciascuna anima al piede (interasse al piede = interasse − 2 ×
    scostamento).
  - *Larghezza superiore* è quella di ciascuna piattabanda; *larghezza inferiore* è
    l'intero fondo.

In tutti i casi lo spessore d'anima è normale alla lamiera e l'altezza libera è verticale.

Il calcolo è in **flessione retta** attorno all'asse orizzontale, con la sezione vincolata
lateralmente da soletta e controventi.

- **Analisi N–Mx**:
  - le anime sono verticali equivalenti di spessore complessivo n·tw/cos α, e le piattabande
    superiori hanno la larghezza complessiva;
  - area, baricentro e inerzia rispetto all'asse orizzontale sono quelli esatti della
    sezione reale, verificati in Model;
  - per l'anima inclinata il prodotto d'inerzia della sola carpenteria non è considerato. Il
    pannello delle proprietà mostra comunque Iy, Ixy e gli assi principali della sezione
    reale.
- **Verifiche sulle lamiere reali**:
  - instabilità locale dell'anima, lunga hw/cos α, con le tensioni delle estremità;
  - taglio: V/(n cos α) nel piano di ciascuna anima, con resistenza e instabilità a taglio
    sulla lamiera;
  - irrigidimenti e saldature, con la lunghezza della lamiera;
  - pioli e superfici della soletta per ciascuna piattabanda superiore.
- **Fondo del cassoncino**: lamiera interna tra le anime (kσ = 4 in compressione uniforme)
  più gli sbalzi esterni.
- **Esclusi, e dichiarati negli avvisi**:
  - irrigidimenti longitudinali del fondo;
  - fondo compresso come piastra irrigidita;
  - torsione, distorsione e diaframmi quando le verifiche a torsione non sono attive.

L'H con anima inclinata resta in sola flessione retta: il ΔT delle fasi è ignorato.

### Cassoncino: torsione, distorsione e diaframmi

Dal 28 settembre 2026 (CompositeBridge 1.4) il riquadro **Cassoncino · torsione, distorsione
e diaframmi**, visibile solo per il cassoncino, attiva le verifiche torsionali. Con l'opzione
attiva la tabella delle sollecitazioni e le fasi mostrano **ΔT [kNm]**, l'incremento del
momento torcente della fase (inattivo per il ritiro).

- **Torsione di St. Venant** (cella chiusa di Bredt): q = T/(2A0), indipendente dalla
  rigidezza delle pareti.
  - Fasi composte: A0 tra il piano medio della soletta (anime prolungate) e quello del fondo.
    J = 4A0²/Σ(ℓ/t) con la soletta hc/nG, nG = n (1+νc)/(1+νa) con la viscosità della fase,
    dimezzata con soletta esclusa (EN 1994-2 §§5.4.2.2(11), 5.4.2.3(6)).
  - Fasi di solo acciaio: cella chiusa dal controvento superiore di spessore equivalente t*
    (Kollbrunner–Basler) al piano medio delle piattabande; con t* = 0 il cassone è aperto e
    la torsione della fase resta da verificare. Le aste del controvento non sono verificate.
- **Dove entra q**:
  - anima più caricata: V/(2 cos α) + q·hw/cos α nella resistenza a taglio e nell'interazione
    M–V (EN 1993-1-1 §6.2.7(9)); q/tw nell'inviluppo elastico;
  - fondo: tensione equivalente al nodo, imbozzamento a taglio del pannello tra i diaframmi e
    interazione η1 + (2η3 − 1)² (EN 1993-1-5 §7.1(5));
  - connessione: metà del flusso di flessione più q sui pioli di una piattabanda; q sommato
    alle superfici a–a interne e b–b; armatura longitudinale della soletta q·cotθ contro la
    compressione disponibile e la capacità residua delle barre (EN 1992-1-1 §6.3.2(3)).
- **Distorsione** con l'analogia della trave su suolo elastico (Wright et al., 1968), se è
  assegnata la luce della campata:
  - modo distorsivo della cella con scorrimento nullo delle pareti, ingobbamento ortogonale a
    N, Mx e My, I_Dw = ∫ω²t ds con la soletta a breve termine e gli sbalzi;
  - rigidezza a telaio K con nodi rigidi; diaframmi intermedi come molle K_D (piastra: stato
    piano con i bordi mossi dalle pareti; controvento a X: diagonali);
  - campata appoggiata con diaframmi d'estremità rigidi, m_t su tutta la luce e T_c nella
    posizione più sfavorevole, applicati come coppie verticali alla sommità delle anime;
  - σdw oltre il 10% della flessione entra nelle verifiche del fondo (EN 1993-2 §6.2.7(3));
    i nodi anima–fondo e anima–piattabanda sommano σdw, flessione trasversale del telaio e τ.
- **Diaframmi**: intermedi a piastra (taglio con imbozzamento, tensione equivalente) o a X
  (diagonale compressa, curva c, Lcr = β·L); d'appoggio con τ = T/(2A0 tD) e coppia T/e_b
  degli apparecchi sommata a R/2 sull'irrigidimento d'appoggio dell'anima più caricata.

Oracoli dei test della libreria: rettangolo (I_Dw = t(b+h)b²h²/96, K = 24/(b/Dh + h/Dv),
K_D = G t b h e 2EA b²h²/L³, carico generalizzato T/2), trapezio contro Yoo et al. (SSRC 2015),
trave su suolo elastico di Hetényi (infinita e appoggiata), flussi calcolati a mano.
Il metodo con storico non esegue queste verifiche e lo dichiara negli avvisi.

Le due file di armature sono indipendentemente disattivabili; non sono inserite barre
fittizie quando una fila è assente. La quota richiesta è **faccia → asse barra**.
Model determina il numero di barre dal passo, centrandole sulla larghezza della soletta.
Sono respinte file sovrapposte/invertite e dimensioni non valide. Il calcolo non verifica
automaticamente il copriferro minimo di durabilità né l'interferro costruttivo minimo.

### Fasi e omogeneizzazione

Da 1 a 20 contributi, ciascuno attivabile, rinominabile e riordinabile. I valori N, Mx, V
(e T per il cassoncino con torsione) sono **incrementi già combinati**; il programma non applica ulteriori γG/γQ e non genera
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

### Classe 4 e risultati

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
[dettagli locali](guida-teorica-anthea.md).

### Verifiche riproducibili

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
[Preparazione del metodo per Checker](guida-teorica-anthea.md).


### Taglio, pioli e aggiornamento delle fasi

Il taglio ora ha verifiche dedicate dell’anima e degli irrigidimenti trasversali intermedi opzionali. Sono disponibili input, resistenze e flussi per i pioli uniformi. Formule, fonti primarie, differenze NTC/EC e limiti sono raccolti in [Fonti e metodo di taglio e connessione](guida-teorica-anthea.md).

La tabella conserva tutte le righe durante le modifiche. Aggiungendo una fase si segue l’ultima situazione cumulativa; scegliendo esplicitamente una fase precedente la scelta resta conservata. Il comando “Mostra tutte le fasi” riattiva il seguito dell’ultima situazione.

La viewport offre tre comandi indipendenti nel menu Verifiche: colorazione σ/limite sulla sezione, sui diagrammi, e rette dei limiti. Rosso indica superamento, viola CLS teso. Le parti inefficaci dell’anima restano semitrasparenti. Le opzioni grafiche non ricalcolano e sono conservate nell’archivio e nel report.


## TEORICA A24 — Studio della condivisione dati tra i fogli ANTHEA
Data: 23 settembre 2026. Analisi del codice presente nel workspace; non modifica ai calcoli o alla sincronizzazione.
Ambito: sei moduli disponibili. Il micropalo strutturale in preparazione è escluso. Le equivalenze riportate sono un progetto software basato sui significati e sui consumatori del codice; non costituiscono una nuova validazione normativa dei motori.

### Conclusione
La condivisione attuale è parziale e troppo legata ai nomi JSON e alle categorie Materiali/Geometria/Armatura. Occorre un registro di proprietà fisiche con adattatori per modulo, unità, condizioni di applicabilità e dipendenze. Non è corretto estendere indiscriminatamente la copia di campi.
Prima priorità: esposizione Materiali–SLE, diametro palo verticale–orizzontale–CA, identità dell'elemento, staffe complete, falda e terreno. Servono inoltre controlli di capacità del modulo: un dato presente nel JSON non è necessariamente editabile o utilizzato dal calcolo.

### Moduli e sorgenti
| Sigla | Modulo | Identificativo | Dati principali |
|---|---|---|---|
| PV | Palo verticale | geo_palo_verticale | generali, efficienza, stratigrafie |
| PO | Palo orizzontale | geo_palo_orizzontale | generali, sezione, verifica, stratigrafie |
| MV | Micropalo verticale | geo_micropalo_verticale | generali, efficienza, stratigrafie Bustamante–Doix |
| MO | Micropalo orizzontale | geo_micropalo_orizzontale | generali, sezione CHS, verifica, stratigrafie |
| CA | Verifica sezione in c.a. | str_palo | input, combinazioni, workspace_ca |
| MAT | Materiali–Calcestruzzo | mat_calcestruzzo | classe, numeri, scelte, opzioni, esposizioni |

Fonti esaminate:
- X.Core/ProjectSharedData.cs; X.Desktop/Wpf/ProjectSharing.cs: confronto, ereditarietà e propagazione attuali.
- X.Core/Archivio.cs; X.Desktop/Wpf/GeoEditor.cs; X.Core/Calcolo.cs; X.Core/Micropali.cs: moduli verticali, dati visibili, conversione asse/profondità e peso.
- X.Core/PaloOrizzontale.cs; X.Core/MicropaloOrizzontale.cs; X.Desktop/Wpf/HorizontalWorkspace.cs; HorizontalMaterials.cs: modelli orizzontali, sezione efficace, campi UI.
- X.Core/SezioneCA.cs; SectionWorkspace.cs; CheckerSection.cs; ConcreteMaterials.cs; ConcreteMaterialCatalog.cs; ConcreteStandards.cs; Ntc2018Checks.cs.
- X.Desktop/Wpf/ConcreteWorkspace.cs; ConcreteParameters.cs; ConcreteShear.cs; ConcreteStress.cs; ConcreteRefinements.cs; ConcreteTendons.cs.
- X.Materiali/MaterialState.cs; MaterialDetails.cs; Bond.cs; ExposureSelector.cs; MinimumConcrete.cs; MixAutomation.cs.

### Matrice delle proprietà: geometria ed elemento
Stato: “parziale” significa che esiste un collegamento ma non copre tutti i casi o la semantica necessaria.
Tutte le condivisioni presuppongono lo stesso elemento fisico, non soltanto la stessa cartella.

| Proprietà | Percorsi e moduli | Regola proposta | Stato attuale |
|---|---|---|---|
| Diametro esterno palo in CLS | PV/PO generali.diametro [m]; CA input.diameter_mm [mm] | Bidirezionale, Dmm=1000Dm; CA circolare; associare CA al palo | Presente con condizioni; non ancora completo |
| Forma della sezione resistente | CA input.shape; PO circolare imposto dal motore | PO può inizializzare CA circolare; CA rettangolare/T non convertibile in PO | Correzione recente; conflitto di forma ora rilevabile |
| Diametro perforazione micropalo Db | MV generali.diametro [m]; MO generali.diametro [m] etichettato geotecnico | Collegamento MV–MO solo dopo conferma della definizione del diametro resistente lateralmente | Oggi confluisce nella stessa chiave dei pali: da separare |
| Diametro bulbo iniettato Ds | MV: Db × alpha per strato | Derivato per terreno/iniezione, non una dimensione unica da copiare in MO | Non distinto nel registro |
| Tubolare CHS | MV generali.profilo_chs; MO sezione.profilo_chs / modo_chs / diametro_chs_mm / spessore_chs_mm | Catalogo↔catalogo bidirezionale; manuale MO→MV solo se profilo rappresentabile, altrimenti incompatibilità | Non collegato tra MV e MO |
| Lunghezza palo | PV/PO generali.lunghezza [m] | Stessa origine, quota testa/piano campagna e tratto infisso | Copiata senza metadati di riferimento |
| Lunghezza micropalo inclinato | MV lunghezza lungo asse e inclinazione; MO lunghezza infissa senza inclinazione equivalente | Non assumere uguaglianza; proiezione Lcosθ solo con riferimenti dichiarati, altrimenti blocco | La copia attuale non controlla inclinazione |
| Rettangolo/T | CA width_mm, height_mm, flange_width_mm, web_width_mm, flange_thickness_mm | Tra CA dello stesso elemento; campi attivi per forma | Presenti; confronto include anche parametri inattivi |
| Coordinate/assi sezione | CA input barre; workspace_ca.sle_comuni assi, origine_x/y, rotazione | Coordinate fisiche comuni, trasformazioni delle azioni locali | Nessuna distinzione generale tra sistema fisico e sistema di verifica |

Il problema del diametro PV non è semplicemente “campo assente”: è già mappato e la prova 1200 mm↔1,2 m è presente. Possibili condizioni bloccanti accertate nel codice: CA non circolare; fogli in gruppi diversi; categoria geometria in conflitto; aggiornamento non confermato o locale. Senza un file del caso segnalato non è possibile attribuire a una sola di queste cause il comportamento osservato.

### Matrice: materiali, ambiente e durabilità
| Proprietà | Percorsi/moduli | Regola proposta | Stato attuale |
|---|---|---|---|
| Classe CLS/fck | MAT classe; CA input.classe_cls/fck_mpa; PO sezione.classe_cls/fck_mpa | Proprietà unica con nome e resistenza coerenti; custom non rappresentabile in MAT segnalato | Collegamento presente |
| Diagramma CLS | CA cls_diagramma; PO usa SezioneCA, non lo stesso motore costitutivo di CA | Identità del materiale comune; modello costitutivo specifico dichiarato | Copia basata sul campo, non sulla capacità del motore |
| Acciaio ordinario | CA e PO fyk_mpa, steel_modulus_mpa; CA anche classe_acciaio, fu, eps_u, diagramma | Condividere fy/Es; altri parametri solo se realmente consumati; aggiornare nomi e preset insieme | Parziale; campi CA aggiuntivi non garantiti nel destinatario |
| Acciaio CHS | MO fy_chs_mpa, gamma_m0 | Materiale tubolare distinto da barre B450; MV usa profilo per peso, non resistenza fy | Solo tra MO |
| Esposizioni | MAT esposizioni[] + esposizione_principale; CA workspace_ca.sle_comuni.esposizione e repliche sle.* | Lista canonica condivisa. CA attuale accetta una sola classe: valutare tutti i requisiti applicabili e mostrare quello governante senza perdere la lista | Collegamento MAT–CA assente |
| Esposizione scelta nel menu | MAT esposizione_principale | Scelta di visualizzazione, non distinta proprietà ambientale | Oggi confrontata come dato: possibile falso conflitto |
| Vita utile | MAT scelte.life | Dato comune dell'elemento, consumato dove implementato | Solo tra MAT |
| Classe minima richiesta | MAT minimumConcreteClass derivata dalle esposizioni | Requisito, non classe adottata: confrontare con CLS scelto, non sostituirlo silenziosamente | Derivata; nessun collegamento di requisito alle verifiche |
| Dmax | MAT numeri.aggregate | Proprietà materiale comune; influenza copriferro e prescrizione | Solo tra MAT, nessun consumatore esplicito equivalente in CA/PO |
| Copriferro adottato | CA input.cover_mm; PO sezione.cover_mm [mm] | Distanza netta al lato esterno della staffa, bidirezionale | Presente |
| Copriferro richiesto | MAT cnom calcolato, tolleranza, getto, abrasione, qualità | Vincolo c_adottato≥c_richiesto; proposta esplicita per adottarlo | Non collegato, non equiparare input e risultato |
| Diametro barra per durabilità/aderenza | MAT numeri.diameter; CA/PO più diametri barre e staffe | Richiede scelta della barra/superficie verificata; più famiglie non riducibili automaticamente a un singolo numero | Assente |
| Copriferro fessurazione | CA sle_comuni.copriferro_fessure | Nel codice default = cover_mm + Østaffa; valore diverso dal copriferro nominale. Conservare origine auto/manuale | Non incluso |
| Aderenza | MAT scelte.bondCondition buone/altre; CA sle_comuni.aderenza migliorata/liscia | Sono proprietà diverse: condizioni di getto vs superficie barra; NON tradurre l'una nell'altra | Nessun collegamento, correttamente da non unificare |
| αct, γc aderenza | MAT numeri.bondAlpha/bondGamma; CA coefficienti AlphaCT/GammaC | Solo con stessa norma/situazione di progetto; preservare modalità locale | Non collegati |
| αcc, γc, γs | CA input e workspace_ca.coefficienti; PO sezione | Parametri di verifica, non materiale intrinseco; sincronizzare copie e contesto normativo | Parte input condivisa; copie workspace sincronizzate all'apertura CA |
| Composizione/cemento | MAT scelte.cement, cementClass, cementEarly, consistency, numeri.cementName | Tra schede dello stesso materiale; a/c, cemento minimo, aria e cloruri derivati separatamente | Copia di interi oggetti MAT, granularità insufficiente |
| Peso specifico | PV/MV generali.peso_specifico_palo | Densità/peso indipendente da fck; MV usa miscela e CHS per peso lineare | Nessuna mappa dedicata |

La classificazione SLE oggi usa Ntc2018Checks.Exposures/CrackRequirement; Materiali gestisce tutte le esposizioni attive. Non bisogna prendere semplicemente la prima classe della lista o l'ultima nell'ordine alfabetico.

### Matrice: armature
| Proprietà | Percorsi/moduli | Regola proposta | Stato attuale |
|---|---|---|---|
| Corona circolare ordinaria | CA input / PO sezione: longitudinal_bar_count, longitudinal_bar_diameter_mm, cover_mm, transverse_bar_diameter_mm | Stessa distribuzione fisica; confrontare anche barre generate | Coperta per configurazione semplice |
| Barre rettangolo/T | CA top_bar_*, bottom_bar_*, side_bar_*, flange_bottom_* | Solo CA e forma appropriata; non importare in palo circolare | Coperta parzialmente per presenza dei campi |
| Secondi strati | CA second_*; SezioneCA supporta second_inner_* | CA↔CA; PO richiede esplicita capacità UI e verifica simmetria | Esclusi tra moduli, possibile differenza non evidenziata |
| Barre manuali | CA input.barre_manuali; SezioneCA legge barre_manuali | Intero assetto come oggetto atomico; verso PO ammissibilità/simmetria da verificare, non solo coincidenza di campi | CA↔CA; incompatibilità cross-modulo non mostrata come stato separato |
| Staffe: diametro/passo | CA input.transverse_*; PO espone diametro, non passo | Diametro fisico comune; passo usato nel taglio CA, non fingere utilizzo nel calcolo PO | Campi copiabili anche se UI/modello non li usa |
| Staffe: schema e bracci | CA workspace_ca.taglio: tipo_staffa, rami_x/y, rami_interni, schema_interno, rotazione_staffa | Comune tra CA; distinto dai parametri analitici di taglio | Mancanti nel confronto |
| Trefoli | CA workspace_ca.trefoli e relativi materiali | Geometria/materiali comuni fra CA; sigma0 richiede fase di precompressione comune | Array copiato integralmente, include sigma0 |
| Parametri manuali taglio | taglio.bw_*, d_*, asl_*, ancoraggio, alpha_*, cot_* | Separare proprietà fisiche da ipotesi e override di verifica; ricalcolo dei derivati | Non coperti, non copiare il blocco completo |

### Matrice: geotecnica
| Proprietà | Percorsi/moduli | Regola proposta | Stato attuale |
|---|---|---|---|
| Falda | PV/PO/MO generali.presenza_falda, profondita_falda [m] | Stesso sondaggio/origine quote. MV non usa la falda nel motore attuale | Assente |
| Sondaggi e stratigrafie | PV/PO/MO stratigrafie[][] | Identità stabile di sondaggio/strato, origine e ordine quote; non corrispondenza per solo indice | Assente |
| Parametri geotecnici comuni | tipologia, spessore, peso_specifico, peso_specifico_saturo, angolo_attrito, coesione_non_drenata, coesione_efficace | PV↔PO↔MO per medesimo terreno; mantenere parametri extra locali | Assente |
| Campi solo PV | addensamento, nc, laterale_attiva, tipo_palo, sottotipo | Locali al metodo verticale; non eliminare copiando stratigrafie | Non mappati |
| Terreno MV | terreno, spessore, alpha, laterale_attiva | Spessori/geometria sondaggio condivisibili con riferimento verticale; categorie Bustamante non traducibili automaticamente in φ, Cu | Nessuna conversione, da mantenere esplicita |
| Indagini | PV/MV generali.verticali_indagate; PO/MO verifica.verticali_indagate | Numero comune se riferito allo stesso insieme di indagini; ξ derivati | Assente |
| Efficienza gruppo | PV/MV efficienza.*; PO/MO verifica.efficienza_*, interassi direzionali | Layout fisico comune, metodi/η verticali e orizzontali differenti | Non unificare i coefficienti |
| Iniezione micropalo | MV IGU/IRS, pressione, inizio_aderenza, percentuale_punta | Specifico del metodo; può alimentare metadati esecutivi ma non tradurre in parametri Broms | Locale |
| Peso lineare micropalo | MV derivato da CHS e perforazione | Ricalcolo dopo modifica profilo/Db; mai copia di output | Derivato |

### Dati da mantenere locali e risultati da ricalcolare
- Azioni N, H, M, V, combinazioni, eccentricità e vincoli: uno stesso elemento non implica lo stesso caso di carico.
- Compressione positiva in PO contro N negativo a compressione in CA; nessuna copia diretta senza un collegamento di combinazioni esplicito.
- Momento resistente PO: risultato della sezione per una specifica forza assiale/metodo, oppure valore manuale con provenienza. Non è un materiale né il momento sollecitante CA.
- Fattori parziali geotecnici, modello/risoluzione numerica, dominio, limiti e scelte analitiche: locali salvo un profilo di verifica esplicitamente condiviso.
- fcd, fyd, Ecm derivato, area, inerzia, peso lineare, Nq/Kp, capacità, limiti durabilità, esiti: ricalcolare dai dati aggiornati.
- Stato risultati: oggi il destinatario è ricalcolato alla riapertura. Occorre registrare subito “da ricalcolare” e impedire export di risultati obsoleti.
- Normativa, situazione persistente/accidentale e convenzioni devono accompagnare coefficienti e modelli; non basta uguagliare fck.

### Difetti e limiti del meccanismo attuale
1. Common esamina l'intersezione delle chiavi: un parametro assente/incompatibile può non apparire nelle differenze. “Dati comuni coerenti” non significa stessa sezione resistente completa.
2. Forma, armatura avanzata e CHS necessitano stati espliciti: coerente, diverso, non rappresentabile, dato mancante, non utilizzato dal modulo.
3. Tutti i geo condividono diameter_mm: confonde palo, perforazione micropalo e associazione con sezione CA; la famiglia dell'elemento deve diventare parte della chiave.
4. Fields confronta dimensioni/armature inattive presenti nei default, specialmente fra moduli uguali: possibile falso conflitto.
5. MAT confronta numeri/scelte/opzioni come blocchi interi. Una modifica del cemento può propagare anche scelte locali di durabilità.
6. ChangedGroups/Apply propaga l'intera categoria modificata: può sovrascrivere altre eccezioni della stessa categoria. Serve diff delle sole proprietà modificate più anteprima.
7. Mancano proprietà fisiche in workspace_ca (esposizione, dettagli staffe), non solo campi input.
8. Identità materiale acciaio e resistenza possono divergere se un foglio cambia fy manualmente dopo una precedente copia del nome: servono transazioni coerenti.
9. Ereditarietà dipende dai peers e dai conflitti di categoria; l'ordine dei fogli non deve diventare implicitamente un criterio di autorità.
10. Valori null, stringhe vuote e default non hanno uno stato distinto; valori incompleti non vanno propagati come geometrie valide.
11. Registro delle conversioni privo di metadati di origine, coordinata, superficie e unità persistite.
12. Lo studio non ha aperto un progetto reale fornito dall'utente: il caso specifico del diametro va riprodotto sul relativo archivio se persiste dopo le correzioni.

### Struttura proposta
Mantenere modifica nei fogli, senza introdurre una nuova scheda obbligatoria. Aggiungere:
- Identità di elemento (palo, micropalo, sezione generica), materiale, sondaggio e superficie di esposizione.
- Registro di proprietà canoniche: ID, tipo, unità, etichetta, dominio, gruppo atomico, applicabilità e dipendenze.
- Adattatore per ogni modulo: lettura, scrittura, capacità di rappresentazione, conversione, invalidazione e verifica.
- Distinzione tra dato fisico adottato, requisito minimo, ipotesi di calcolo e risultato.
- Collegamento predefinito ai fogli dello stesso gruppo compatibili, con eccezioni esplicite per singola proprietà.
- Provenienza/versione: foglio sorgente, revisione, ultima applicazione e override; non dipendere dal nome o dall'ordine dell'albero.
- Anteprima “questi campi cambiano in questi fogli”, tutti/solo questo; copia atomica dopo validazione, annullabile.
- Nello spostamento di gruppo: conservare i dati e chiedere separatamente se riallinearli.
- Gerarchia visiva e ordine manuale restano indipendenti dalle relazioni dati.

### Piano di intervento e prove di accettazione
1. Registro e adattatori; separare diametro palo/perforazione/CHS; distinguere mancanti, inattivi e incompatibili.
2. Copertura base: dimensioni PV–PO–CA, CHS MV–MO, classe/fy/Es, corona semplice e staffe CA complete.
3. Ambiente: lista esposizioni persistita anche in CA, criterio governante SLE tracciabile, verifica copriferro adottato/richiesto e gestione esplicita della barra di riferimento.
4. Geotecnica: sondaggio/strato con ID e quote, falda, parametri fisici, indagini; preservare dati specifici dei metodi.
5. Armature avanzate: doppie corone, manuali, trefoli e fasi; audit dei motori prima di abilitare collegamenti.
6. Anteprima proprietà, eccezioni, annullamento, migrazione conservativa, stato dei risultati.

Prove necessarie per ogni proprietà:
- A→B e B→A con valori non predefiniti; ricostruzione del dato effettivamente usato dal motore, non solo JSON.
- Unità, decimali con virgola, null/vuoto, input non valido, reset e rimozione.
- Modifica locale, condivisa, annullamento e nessuna richiesta ripetuta.
- Preservazione di carichi, parametri esclusivi e altri elementi.
- Aggiunta/rimozione/riordino/spostamento; indipendenza dall'ordine dei peers.
- Salvataggio/riapertura; vecchi archivi non modificati senza consenso.
- Circolare contro rettangolare/T; perforazione contro CHS; materiale custom; catalogo contro manuale.
- Esposizioni multiple e campi inattivi; copriferro richiesto sotto/sopra quello adottato.
- Confronto di contorno, coordinate/diametri barre, proprietà materiale e sondaggio dopo propagazione.
- Invalidazione immediata dei risultati, aggiornamento UI e blocco export di esiti obsoleti.

### Decisioni da definire nell'implementazione
- Ambito stesso elemento dentro una sezione organizzativa con più elementi: introdurre un ID, senza costringere a duplicare la struttura dei progetti.
- Esposizioni per superficie o per elemento: predisporre la lista con ambito; non appiattire esposizioni diverse di parti diverse.
- Override permanente o singola modifica locale: mostrare l'effetto della prossima sincronizzazione.
- Propagazione dei requisiti di durabilità: proporre l'adozione del valore, senza imporre silenziosamente il copriferro.
- Collegamento terreni condiviso a livello superiore: per ora preservare il comportamento di sottosezioni indipendenti, salvo scelta esplicita.

Esito: fattibile, ma l'estensione va costruita sul modello semantico e verificata a livello dei motori. La sola aggiunta di chiavi all'attuale lista non rende affidabile la condivisione.


### Aggiornamento implementativo — 24 settembre 2026

Decisione dell'utente: una sola classe di esposizione. Materiali conserva la classe principale; negli archivi precedenti le esposizioni multiple sono archiviate in `esposizioni_precedenti` e segnalate. Il valore unico si collega a tutte le famiglie SLE della verifica CA e al palo orizzontale.

Implementati: propagazione dei soli campi modificati con anteprima; gruppo atomico per identità dell'acciaio e cambio forma; copriferro adottato CA↔PO; confronto con il nominale minimo del motore Materiali, riferito alla barra dichiarata nella scheda Materiali, con stato non verificabile in caso di esposizione/CLS incoerenti o input incompleti. Questo avviso non costituisce la verifica di ogni barra/superficie né una verifica al fuoco.

Separati diametro del palo CA e perforazione del micropalo. Collegati CHS da catalogo MV↔MO, falda PV/PO/MO, verticali indagate e dettagli delle staffe tra verifiche CA. Le lunghezze dei micropali inclinati non si propagano verso il modulo orizzontale. Forme incompatibili, armature avanzate e profili manuali non rappresentabili sono segnalati nel confronto.

Stratigrafie PV/PO/MO: collegamento esplicito da Confronto, con anteprima e conferma dello stesso sondaggio e origine delle quote. Su destinatari compilati sono richiesti numero di sondaggi, strati e spessori corrispondenti; vengono preservati i parametri esclusivi. ID persistenti per strato; aggiornamenti successivi per proprietà, mai per solo indice. Cambiare suddivisione/ordine sospende la condivisione fino a un nuovo collegamento. Le categorie Bustamante restano locali.

L'ambito resta il gruppo immediato del progetto: sottosezioni indipendenti. Il registro storico completo, l'annullamento delle sincronizzazioni e un collegamento tra gruppi diversi sono sviluppi ulteriori; non sono introdotti da questa revisione.


### Confronto indipendente dall'ordine dei fogli
Il confronto usa ora `ComparableFields`, con criteri simmetrici separati dalla compatibilità di trasferimento (`Common` / `Apply`). L'esposizione non compilata e il profilo CHS mancante vengono rilevati indipendentemente da quale foglio viene prima, senza rendere tali valori trasferibili a selettori che non li ammettono. Le coppie e gli avvisi sono ordinati per identità stabile; la posizione nell'albero resta una scelta di visualizzazione. Controllati tutti i 24 ordini di Materiali, CA, palo verticale e palo orizzontale, le coppie tra i sei moduli, la conservazione degli input e l'uniformazione in entrambi gli ordini. Verificati anche Test 1 e Test 3, in sola lettura sugli originali.


## TEORICA A25 — Sezione da ponte — taglio, irrigidimenti e connessione

Revisione del 25 settembre 2026. Metodi ricavati dai testi normativi, senza assumere corretto il codice precedente. Le edizioni sono quelle selezionabili nel modulo: NTC 2018, EN 1993-1-5:2006 con corrigendum, EN 1993-2:2006 ed EN 1994-2:2005 con corrigendum. Non si applicano implicitamente le edizioni di seconda generazione.

### Fonti primarie consultate

- [NTC 2018, allegato ufficiale in Gazzetta Ufficiale](https://www.gazzettaufficiale.it/atto/serie_generale/caricaArticolo?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.flagTipoArticolo=1&art.idArticolo=1&art.idGruppo=0&art.idSottoArticolo=1&art.idSottoArticolo1=10&art.progressivo=0&art.versione=1): capitoli 4 e 5, in particolare Tab. 4.2.VII, §§4.3.4.2.2 e 4.3.4.3.1–5.
- [EN 1993-1-5:2006, testo CEN riprodotto](https://www.phd.eng.br/wp-content/uploads/2015/12/en.1993.1.5.2006.pdf): §§5, 7.1, 9.1–9.4, allegato A.3.
- [EN 1993-2:2006, testo CEN riprodotto](https://www.phd.eng.br/wp-content/uploads/2015/12/en.1993.2.2006.pdf): §6.1, coefficienti per ponti.
- [EN 1994-2:2005, testo CEN riprodotto](https://www.phd.eng.br/wp-content/uploads/2015/12/en.1994.2.2005.pdf): §§6.2.2.2–5, 6.6.2.1, 6.6.3.1, 6.6.5, 6.8.1(3), 7.2.2.
- [Commentario JRC a EN 1993-1-5](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2021-12/EUR22898EN.pdf): riscontro del modello di irrigidimento e del caso numerico hw=2720, tw=18, a=8000 mm.

Le copie consultate e le estrazioni di controllo sono sotto `supporto/artefatti/ponte_taglio_pioli/fonti`. Queste ultime non sostituiscono la lettura delle formule grafiche del PDF.

### Scelte implementate e loro campo

**Taglio.** La soletta non contribuisce a V resistente. L’anima intera resiste al taglio anche quando la riduzione per tensioni normali ne rende inefficace una parte. Si calcolano kτ, τcr, λw, χw, Vpl,Rd e Vbw,Rd. VRd è il minore delle ultime due; si omette il contributo favorevole delle flange. Il limite plastico usa cautelativamente Av=hw·tw. Senza intermedi idonei si adotta il pannello lungo. La curva del montante terminale è non rigida salvo verifica positiva del dettaglio rigido a due coppie. Per anime snelle senza appoggi inseriti rimane una segnalazione di incompletezza.

**Coefficienti.** γM1 iniziale 1,10 sia NTC sia EC per ponti, γV=1,25, η=1,20. γM0 rimane 1,05 per NTC e 1,00 per EC. Sono esposti e modificabili in un unico punto. I valori EC sono raccomandati per l’edizione indicata, non una selezione automatica di tutte le Appendici Nazionali.

**Interazione N–M–V.** EN 1994-2 §6.2.2.4(3) rimanda, per classe 3/4, a EN 1993-1-5 §7.1 usando il momento totale e le capacità della sezione composta. Per N=0, fy≤355 MPa e anima non interamente compressa, Mpl è integrato con flange efficaci, anima intera e soletta compressa; Mf omette l’anima. Il calcestruzzo teso è nullo e le barre sono omesse cautelativamente nelle sole capacità di riferimento. L’integrazione dei blocchi rettangolari è esatta; le verifiche elastiche non sono sostituite da queste capacità plastiche. La soglia di applicazione è 0,5 VRd. Negli altri casi ad alto taglio si usa un inviluppo elastico cautelativo con Mf=0, esplicitato in [dettagli locali](guida-teorica-anthea.md). Non si attribuisce al criterio il significato di un dominio plastico esatto sotto N.

**Tensioni tangenziali.** Si espongono V/(hw·tw) e il massimo del campo elastico V·S/(I·tw) sulla sezione lorda omogeneizzata di ciascuna fase. La somma è algebrica. L’inviluppo √(max|σ|²+3 max|τ|²) è un controllo elastico aggiuntivo, non sostituisce l’instabilità o l’interazione. La colorazione della viewport è σ/limite delle sole tensioni normali.

**Irrigidimenti e appoggi.** Sono gestiti piatti mono/bilaterali anche diversi, pannelli adiacenti diversi, appoggi interni e terminali, reazione eccentrica, montante rigido a due coppie e saldature continue. Il metodo corrente usa pressoflessione elastica del secondo ordine con imperfezioni equivalenti, eccentricità reale e Lcr/L iniziale 1,00; sostituisce nell’adattatore la precedente ipotesi dei soli piatti simmetrici. Campo, formule e verifiche sono descritti in [irrigidimenti, appoggi e connessione](guida-teorica-anthea.md). Un irrigidimento non idoneo non aumenta VRd. Intagli e azioni dei traversi non assegnate restano fuori campo.

**Pioli.** Pioli a testa saldata uniformi, soletta piena ordinaria C20/25–C60/75, d=16–25 mm, h≥3d. Resistenza minima dei due meccanismi normativi, fu limitato a 500 MPa. La domanda elastica è q=Σ(Vi·Si/Ii+Δqi), PEd=|q|·passo/numero per fila. Il getto/solo acciaio non carica la connessione e non riceve un esito pioli. La relazione V·S/I assume proprietà costanti nel tratto e N costante lungo la trave; introduzioni locali di N e variazioni di sezione richiedono Δq separato. Ritiro uniforme: nessun V, ma Δq può introdurre gli effetti di estremità ottenuti da un modello longitudinale; non è deducibile dal solo stato di una sezione.

**Distinzione NTC/EC per S e I.** NTC §4.3.4.3.3: medesime proprietà della fase tensionale, inclusa l’esclusione della soletta. EC4 §6.6.2.1(2): soletta non fessurata, mantenendo la carpenteria efficace; la fase con soletta esclusa ha quindi un proprio φ/n per lo scorrimento se la verifica pioli è attiva. Nessuna commutazione nascosta a una geometria d’acciaio lorda. S e I sono riportati per fase.

**Pioli SLE.** 0,75 PRd è il limite sotto combinazione caratteristica, secondo §§6.8.1(3)/7.2.2 EC4. Il selettore quasi permanente non riceve un esito di tale controllo. Il modulo non genera combinazioni né moltiplica le azioni per γF.

**Dettagli.** Passi minimo longitudinale 5d e trasversale 2,5d; massimo longitudinale min(800 mm,4hc). Bordo libero flangia minimo 20 mm NTC, 25 mm EC. Testa almeno 1,5d e 0,4d; copriferro almeno quello assegnato, almeno 20 mm NTC e almeno quello delle barre superiori per EC. Intradosso testa 30 mm sopra l’armatura inferiore. Il limite d≤1,5tf è attivo inizialmente per azioni ripetute ed è obbligatorio con verifica a fatica; il limite statico 2,5tf è adottato cautelativamente anche sopra l’anima. Non si aumenta la classe della flangia grazie ai pioli. Sono aggiunte armature trasversali, superfici a–a/b–b, ancoraggio, bordi e fatica resistente dei pioli con interazione della flangia tesa. Restano fuori campo sollevamento, splitting attraverso lo spessore, gruppi non uniformi, mensole locali e lamiere grecate.

### Organizzazione e prove

- Metodi nuovi in Checker: `GPCChecker.Steel/CompositeBridges/BridgeShearConnection.cs`, `BridgeBendingShear.cs` e `BridgeLocalDetails.cs`. ANTHEA compila temporaneamente gli stessi sorgenti con `Compile Link` poiché i DLL distribuiti non espongono ancora queste classi. Nessuna modifica ai solver precedenti.
- Adattatore per le fasi e unità in ANTHEA `X.Core/BridgeSection.Shear.cs`.
- Test in Checker `GPCChecker.Test.BridgeAudit/BridgeShearConnectionTests.cs` e `BridgeLocalDetailsTests.cs`: valori indipendenti, transizioni, errori di ingresso, segni, fasi, proprietà efficaci, NTC/EC, ritiro, SLE, dettagli e fallback degli irrigidimenti. Suite ordinaria: 259 test passati, con esclusione esplicita dei difetti storici `KnownBug` e `ConstructorRegression`.
- Test della vista in ANTHEA `supporto/test/Desktop/BridgeShearUiSmokeChecks.cs`: aggiunte rapide, 11 fasi, modifiche e inattivazione, scelta dell’ultima situazione, eliminazione e riapertura, contouring e report. Le righe di input sono riconciliate senza svuotare la collezione WPF.

### Difetti del codice preesistente trovati nel confronto

Non corretti in questa attività:

1. `EN1993p11Checker.CalculateVbRd1`, circa riga 1061: nel ramo oltre la soglia di snellezza usa χ=0,83/η, mentre la Tab. 5.1 richiede la dipendenza da λw. Il ramo non rigido deve decrescere all’aumentare della snellezza. Questo può sovrastimare la resistenza di anime molto snelle.
2. `PanelsStability/PanelShearStability.cs`, circa righe 93–101: i rami a/h<1 e a/h>1 non includono l’uguaglianza; un pannello quadrato può lasciare kτ nullo. Quel sorgente è attualmente escluso tramite `#if NEVER`.

I nuovi test non usano questi metodi come riferimento. Restano inoltre aperti i difetti del solver lineare già documentati nella precedente revisione.


## TEORICA A26 — Test dei metodi per i ponti

Aggiornamento del 26 settembre 2026, sulle DLL presenti in `lib/Checker`.
La suite `supporto/test/X.Verifiche/BridgeMethodChecks.cs` verifica l'integrazione
degli ingressi e risultati di ANTHEA con i nuovi metodi di Checker.
Si aggiunge a `BridgeSectionChecks.cs`, senza sostituirne i riferimenti.

### Esecuzione

Dalla radice del repository:

```powershell
dotnet run --project supporto/test/X.Verifiche -c Release -- --bridge
```

Per eseguire soltanto i nuovi test usare `--bridge-methods`.
Gli errori restituiscono codice di uscita 1, con grandezza, risultato e riferimento.
Il log della campagna è in `supporto/artefatti/ponti_metodi_2026_09_26/bridge.log`.

### Copertura

| Funzione | Casi e controlli |
| --- | --- |
| Tre metodi di analisi | Due coppie N–M di segno diverso; confronto elastico indipendente su carpenteria |
| Getto e attivazione | Due momenti iniziali per ciascun metodo storico; deformazione al getto conservata, CLS e barre inizialmente scarichi |
| Scarico elastico | Due cicli carico/scarico per ciascun metodo storico; tensioni finali nulle |
| Ritiro incrementale | 24 eventi da −1 microdeformazione in ciascun metodo storico; somma −24 microdeformazioni e superamento del vecchio limite di venti fasi |
| Momento–curvatura | Rampe positiva e negativa, N = −50 kN, origine nello storico, M = EIκ al baricentro |
| Forza–deformazione | Rampe positiva e negativa, κ = 0, N = EAε |
| Ramo plastico | Trazione uniforme fino a 3000 e 4000 microdeformazioni, acciaio elastoplastico con fy = 235 MPa; tensione al plateau e deformazione plastica analitica |
| Omogeneizzazione | φ = 0,5 e 2, confronto con l'ingresso equivalente n nel metodo storico lineare |
| Passaggio al non lineare | φ e classe 4 salvati non vengono modificati; la soluzione istantanea usa la sezione lorda |
| Risultati | Equilibrio integrato N–M, memoria degli stati, esportazione JSON/CSV e conversioni kN/N, 1/m/1/mm, microdeformazioni/deformazioni |
| Errori | Metodo inesistente, discretizzazioni nulle o frazionarie, punti/sottopassi frazionari, incremento nullo e annullamento dei tre metodi |

### Riferimenti indipendenti

La carpenteria di riferimento è composta da tre rettangoli:
piattabanda superiore 500 × 25 mm a y = −12,5 mm;
anima 14 × 1800 mm a y = −925 mm;
piattabanda inferiore 700 × 30 mm a y = −1840 mm.
Si usano E = 210000 MPa e le formule:

- A = Σ bi hi; yc = Σ Ai yi / A;
- I = Σ [bi hi³/12 + Ai(yi−yc)²];
- Mc = M0 + N yc; κ = Mc/(EI);
- ε0 = N/(EA) + κ yc; σ(y) = E(ε0−κy);
- oltre snervamento, per la legge elastoplastica assegnata: σ = fy,
  εp = ε−fy/E e N = fy A.

Aree, inerzie e risultati attesi sono calcolati nel test senza interrogare
il risolutore di produzione. L'equilibrio viene ricostruito dalle singole fibre:
N = Σ σi Ai e M0 = −Σ σi Ai yi.
Le tolleranze assolute sono esplicite accanto alle asserzioni, nelle rispettive
unità; per l'equilibrio integrato sono 0,01 N e 10 Nmm.

Campagna eseguita: **117 controlli precedenti e 2957 nuovi controlli superati**.
Il conteggio comprende i confronti delle singole fibre, non 2957 esempi distinti.
Queste prove verificano le API e l'integrazione del modulo: non sostituiscono
le prove della libreria su plasticità ciclica, classe 4, confronti OpenSees
o i controlli grafici WPF. Il documento Word di validazione non è rigenerato
da questo comando.


## TEORICA A27 — Unificazione dei calcoli e controllo dei progetti

Revisione del 25 settembre 2026. Repository ANTHEA; riferimento committato: `7d1fcbf91196e4d7d28b8449e3a248b71a4d51b3`.

### Risultato del riordino

Il catalogo, le factory e la validazione dei fogli sono definiti una volta in `X.Core/ModuleCatalog.cs`. La creazione di archivi, progetti, sezioni e fogli usa `ProjectDocuments`. Moduli singoli, albero dei progetti e strumenti senza interfaccia attingono alle stesse definizioni.

`CalculationService.Calculate(module, data, token)` è l’ingresso senza interfaccia. Lavora su una copia dei dati e chiama gli stessi motori dei fogli. I servizi con risultati tipizzati restano disponibili per l’aggiornamento selettivo della UI: l’interfaccia conserva gestione degli eventi, cancellazione, grafici, tabelle ed esportazione.

| Ambito | Motore / servizio condiviso |
|---|---|
| Palo e micropalo verticali | `Calcolo`, con le factory di `ModuleCatalog` |
| Palo e micropalo orizzontali | `PaloOrizzontale`, `MicropaloOrizzontale` e relativi modelli di sezione |
| CA: domini, tensioni, fessurazione | `ConcreteAnalysisSession` → `CheckerSection` / `Ntc2018Checks` |
| CA: taglio e torsione | `ConcreteShearAnalysis` → `Ntc2018Checks.Shear` / `ConcreteTorsionCalculator` |
| CA: momento–curvatura | `ConcreteCurvatureAnalysis` → `MomentCurvatureCalculator` e Checker |
| CA: dettagli e ancoraggi | `ConcreteDetailingAnalysis` → motori di durabilità e `ConcreteDetailingCalculator` / `ConcreteAnchorageCalculator` |
| Calcestruzzo: durabilità, copriferro e prescrizioni | `X.Core/Materials`, mantenendo il namespace `Materiali` |
| Aderenza | Unica espressione in `ConcreteBond`, usata da materiali e ancoraggi |
| Acciaio per armature | `RebarMaterial` e materiali della libreria Model |
| Sezione composta da ponte | Adapter `BridgeSection` e libreria `GPCChecker.CompositeBridge`; mantenuti i diversi metodi di analisi |

`ConcreteCalculationSettings` prepara coefficienti, staffe, taglio, torsione, curva e dettagli prima dell’apertura delle schede. I coefficienti condivisi αcc, γc e γs provengono dall’input della sezione. Le copie delle opzioni normative sono sincronizzate e non costituiscono un secondo dato indipendente.

L’ingresso JSON CA esegue domini 3D/2D SLU/SLV, tensioni/fessurazione SLE e le righe di taglio/torsione. Curve, dettagli e ancoraggi hanno ingressi espliciti separati, perché richiedono scelte aggiuntive dell’utente. Per il modulo materiali CLS, l’ingresso JSON espone proprietà e copriferro; gli altri risultati sono accessibili dai rispettivi servizi. `calcoli_inclusi` dichiara il perimetro dell’output.

### Correzioni e controlli sui dati

- Un identificativo di modulo sconosciuto viene rifiutato; non produce più accidentalmente i dati di un palo.
- Foglio singolo e foglio di progetto nascono dagli stessi valori iniziali. Ogni foglio possiede una copia indipendente dei dati.
- Progetti e sezioni nuovi contengono sia l’elenco dei fogli sia quello delle sottosezioni, con identificativi nuovi e nomi automatici non duplicati fra fratelli.
- La creazione del foglio applica l’eredità dopo il collegamento all’albero reale. Se l’eredità fallisce, annulla l’inserimento senza lasciare un foglio parziale.
- Le combinazioni CA malformate e i contenitori di impostazioni non validi vengono rifiutati prima della migrazione. Non vengono sostituiti silenziosamente con elenchi vuoti. Il controllo della struttura non vieta il salvataggio di testi numerici ancora incompleti nei campi di input.
- La migrazione del segno di N resta quella esistente e si esegue una sola volta. I calcoli senza interfaccia non modificano l’archivio sorgente.
- Cache SLE e domini sono nel servizio comune. Una variazione delle azioni invalida lo stato; una variazione dei soli criteri di fessurazione riusa lo stato tensionale e aggiorna la verifica. Input non validi non restituiscono il precedente stato come corrente.
- La conferma dell’ancoraggio a taglio viene invalidata quando cambia la geometria numerica, non per una diversa rappresentazione testuale dello stesso numero.
- Nei progetti, geometria, materiali e armature dei ponti partecipano a eredità, confronto e report. Seconda piattabanda, armature disattivabili e sovrascrittura fy sono gestite insieme ai rispettivi dati. Fasi, ritiri, carichi e opzioni di analisi restano propri del foglio. Nessuna conversione implicita fra materiali del ponte e legami personalizzati del CA.
- Il test dell’aderenza della scheda Materiali ora segue il separatore decimale della cultura corrente, conservando il benchmark numerico.

### Metodi mantenuti distinti

Unificare il codice comune non equivale a sostituire i modelli fisici. Per il ponte restano i metodi cumulativo, con storico e non lineare, oltre alle curve M–κ/N–ε. Non sono stati fusi in un solo algoritmo.

`SezioneCA`/`SezioneElastica` e `CalcoloSezione` mantengono le API storiche. Il palo orizzontale utilizza ancora il proprio percorso di capacità della sezione, con convenzioni e affinamento numerico esistenti. Il foglio CA corrente e il comando `--calcola` usano invece i servizi Checker. Cambiare il modello del palo richiederebbe una migrazione fisica e una validazione dedicate, non una sostituzione di nomi.

I motori normativi non sono stati riformulati in questa attività. Le prove di parità verificano il riordino e la coerenza fra percorsi, non costituiscono una validazione indipendente delle formule.

### Prove eseguite

Output in `supporto/artefatti/unificazione_progetto/`; nuovi sorgenti in `supporto/test/X.Verifiche/ProjectCalculationChecks.cs`.

| Prova | Esito |
|---|---|
| Compilazione Release desktop | 0 errori, 0 avvisi |
| Nuovi controlli su progetti e servizi | 80 superati |
| Checker e CA: domini/NTC, Excel, estensioni, dati | 102 + 18 + 174 + 27 superati |
| Modulo CA ampliato | 78 superati |
| Palo orizzontale | 1.079 superati; anche la suite CHS superata |
| Coesione efficace | 312 superati |
| Peso specifico saturo | 57 superati |
| Sezione da ponte | 117 superati |
| Micropalo verticale | 34 casi di riferimento, 101.883 confronti superati |
| Interfaccia | Progetti, workspace progetto, gerarchie, condivisione, materiali, acciaio, CA, estensioni CA, curve ponte, palo orizzontale e report progetto superati |
| Eseguibile pubblicato in `app` | Creazione, trascinamento, rinomina, gerarchie, conservazione input e riapertura progetti superati |
| Vecchi riferimenti Python | **414 casi superati, 50 differenze su 464 casi** |
| Aggregatore storico archivi/report | **Non completato: interruzione sul salvataggio del report micropalo** |

I conteggi delle suite comprendono controlli di coerenza e confronti numerici; non rappresentano altrettanti benchmark indipendenti.

### Interruzione da approfondire nel salvataggio del report micropalo

L’aggregatore `--software` si interrompe ripetutamente durante `File.Move(temp, path, true)` di `Archivio.ScriviAtomico`, con destinazione `micro.docx`. Il processo restituisce zero senza completare il metodo; non viene intercettata un’eccezione gestita. La diagnostica temporanea ha verificato che scrittura e `Flush(true)` del file temporaneo terminano. Il pacchetto DOCX generato è leggibile e l’XML risulta valido. Il medesimo flush e spostamento eseguiti separatamente dal terminale sono riusciti.

Questo esito non identifica ancora la causa dell’interruzione, né dimostra un errore delle formule. Il salvataggio atomico di produzione è stato lasciato invariato e la diagnostica temporanea rimossa. La suite non è conteggiata fra quelle superate; le prove UI del report di progetto sono invece concluse. Tracce: `software-io.log`, `software-final.trace.txt` e `software-local.trace.txt`. Gli output intermedi sono conservati sotto `run-*`.

`supporto/scripts/Test-SoftwareReports.ps1` controlla anche la presenza del messaggio finale nella traccia e restituisce un errore se il processo termina senza aver completato le asserzioni, anche con codice zero.

### Differenze storiche riprodotte

Gli stessi 464 casi sono stati eseguiti anche su una copia dei sorgenti ANTHEA committati. I 50 messaggi di differenza sono identici e i due riepiloghi JSON hanno lo stesso SHA-256:

`3BBD26EDE2F4AEA7CDDDE252F9E4588C5D8D5D0D0994BDCC4CDD0498C7E3DF4B`

La baseline è sotto `baseline-head/`. Per renderla compilabile è stato necessario reinserire i tre sorgenti esterni del ponte allora collegati da Checker, usando quelli attuali con il namespace precedente; nessuno dei 464 casi chiama quei tre sorgenti. I motori ANTHEA e le DLL della baseline provengono dal commit indicato.

| Famiglia di confronto | Differenze |
|---|---:|
| Capacità del palo, casi storici | 2 |
| Geometria/risultati delle sezioni | 24 |
| Analisi elastica | 16 |
| Resistenza elastica limite | 4 |
| Domini | 4 |

Sono quindi divergenze preesistenti rispetto ai riferimenti Python, non introdotte da questo riordino. Il confronto da solo non stabilisce quale delle due formulazioni sia corretta. I riferimenti e le tolleranze sono rimasti invariati: serve una revisione mirata dei casi circolari e a T e dei due casi del palo prima di dichiarare interamente superata la vecchia suite.

### Ripetizione delle prove

```powershell
dotnet build X.Desktop/X.Desktop.csproj -c Release
dotnet run --project supporto/test/X.Verifiche -c Release -- --project-calculations supporto/artefatti/unificazione_progetto/core
dotnet run --project supporto/test/X.Verifiche -c Release -- --checker
dotnet run --project supporto/test/X.Verifiche -c Release -- --ca-module
dotnet run --project supporto/test/X.Verifiche -c Release -- --horizontal
dotnet run --project supporto/test/X.Verifiche -c Release -- --coesione
dotnet run --project supporto/test/X.Verifiche -c Release -- --gamma-sat
dotnet run --project supporto/test/X.Verifiche -c Release -- --bridge
dotnet run --project supporto/test/X.Verifiche -c Release -- --micropalo supporto/test/casi_confronto.json
dotnet run --project supporto/test/X.Verifiche -c Release -- --reference-only supporto/test/casi_confronto.json supporto/artefatti/unificazione_progetto/riferimenti-python.json
```

L’ultimo comando restituisce 1 finché sono presenti le differenze documentate. Il runner intercetta esplicitamente le eccezioni per produrre una diagnostica e un codice di uscita non nullo.

Per l’aggregatore archivi/report usare il controllo esterno di completamento:

```powershell
./supporto/scripts/Test-SoftwareReports.ps1 -OutputDirectory supporto/artefatti/unificazione_progetto
```

### Aggiornamento del 26 settembre 2026

I servizi numerici descritti sopra sono stati trasferiti da X.Core a X.Calculations, assembly ANTHEA.Calculations, senza dipendenze dalla UI o dalla gestione degli archivi. Il resoconto aggiornato è in [validazione-libreria-calcolo.md](guida-teorica-anthea.md); architettura e trasferimento in [libreria-calcolo.md](guida-teorica-anthea.md).

La nuova prova del report ricorsivo di progetto è **incompleta**: il processo termina prima dell’attestazione finale. Il solo codice zero e il file di avanzamento non autorizzano a considerare la suite superata. Il nuovo runner controlla espressamente l’attestazione finale. Restano documentate anche le 50 differenze storiche dei riferimenti Python e l’interruzione del report micropalo.


## TEORICA A28 — Validazione della separazione della libreria di calcolo

> Documento storico del 26 settembre. Per la verifica corrente del 27 settembre vedere [Audit calcoli e progetti](guida-teorica-anthea.md): i 464 riferimenti attuali e le suite complete dei report risultano superati. Gli esiti seguenti sono conservati per tracciabilità.

Data: 26 settembre 2026. Ambito: `X.Calculations`, progetti, coefficienti condivisi e collegamenti a interfaccia e report. Questa revisione verifica la separazione e la conservazione del comportamento; non costituisce una certificazione generale delle formulazioni ingegneristiche.

### Esito

La libreria è compilabile e utilizzabile fuori dal progetto desktop. Il pacchetto esportato contiene sorgenti della libreria, dipendenze Checker/Model/Geometry e test autonomi. Nessun riferimento a `X.Core`, WPF o archivi è richiesto dai test autonomi. La compilazione desktop termina senza errori o avvisi.

| Prova | Esito |
|---|---|
| Libreria compilata dalla cartella esportata | 57 controlli superati |
| Progetti e servizi comuni | 114 controlli superati, di cui 34 aggiunti per coefficienti, normativa e nomi |
| Checker CA, Excel, estensioni e dati | 102 + 18 + 174 + 27 controlli superati |
| Modulo CA ampliato | 78 controlli superati |
| Palo orizzontale | 1.079 controlli superati, oltre alla suite CHS |
| Coesione efficace | 312 controlli superati |
| Peso specifico saturo | 57 controlli superati |
| Sezione da ponte | 117 controlli superati |
| Micropalo verticale | 34 casi di riferimento e 101.883 confronti superati |
| Vecchi riferimenti Python | 414 superati, 50 differenze preesistenti su 464 casi |
| UI progetti | Creazione, riapertura, workspace, gerarchie e condivisione completati |
| UI moduli | Materiali, acciaio, CA, estensioni CA, palo orizzontale e curva ponte completati |
| UI ponte completa | Geometria, tensioni, opzioni, ritiri, taglio/pioli, dettagli, storico, curve e report del ponte completati |
| Report materiali | Generazione e controlli del pacchetto Word completati |
| Applicazione pubblicata in app | Creazione/riapertura progetti e report materiali completati; DLL di calcolo identica a quella compilata |
| Report complessivo del progetto | **Non completato**: interruzione durante il salvataggio dopo l’impaginazione |
| Aggregatore storico archivi/report | **Non completato**: interruzione durante il salvataggio del report micropalo |

Le attestazioni finali dei test UI sono necessarie: un processo terminato con codice zero e un file di avanzamento non costituiscono una prova superata. Il nuovo runner `Test-CalculationUi.ps1` verifica l’attestazione specifica di ogni suite e richiede cartelle vuote, per evitare di riutilizzare esiti precedenti.

### Provenienza dei riscontri

I test autonomi confrontano area e inerzia del rettangolo, area tagliata, distanza dai bordi/fori, rapporto di omogeneizzazione, coefficiente di spinta passiva e aderenza con risultati analitici. Coprono inoltre zero, NaN, valori fuori campo, coefficienti non pertinenti, normativa sconosciuta, input immutati e avvio del calcolo ponte senza UI. Le proprietà geometriche utilizzano direttamente la DLL GPC.Geometry fornita.

I 114 controlli di progetto sono regressioni e verifiche di comportamento: non sono 114 benchmark ingegneristici indipendenti. Verificano priorità del riferimento gerarchico, creazione atomica, ID indipendenti, aggiornamento degli alias dei coefficienti CA, assenza di duplicati, incompatibilità fra normative, ereditarietà del ponte e conservazione delle azioni locali.

I riferimenti Python sono esterni all’esecuzione C# ma derivano dalla precedente implementazione del progetto. Le 50 differenze non vengono nascoste modificando tolleranze o attesi. Il riepilogo di questa revisione ha lo stesso SHA-256 della baseline già confrontata:

`3BBD26EDE2F4AEA7CDDDE252F9E4588C5D8D5D0D0994BDCC4CDD0498C7E3DF4B`

Rimangono 2 casi del palo, 24 delle sezioni, 16 dell’analisi elastica, 4 della resistenza elastica e 4 dei domini. Per stabilire quale formulazione sia corretta serve l’analisi ingegneristica dei singoli casi descritta nel precedente audit `unificazione-calcoli-progetti.md`.

### Anomalie aperte dei report

Il test del report ricorsivo prepara i sette capitoli e arriva alla fase «Unione dei dati comuni e impaginazione», ma termina con codice zero prima di produrre `smoke.txt`. Resta il file temporaneo. Questo esito è incompleto, anche se le schede materiali e il report del ponte vengono esportati correttamente in prove separate.

Anche `Test-SoftwareReports.ps1` conferma l’interruzione dell’aggregatore dopo «Report: micropalo» e «Tabelle completate». Nella diagnostica precedente il punto era `File.Move` del salvataggio atomico. Non è stata identificata una causa certa; non è stata modificata la persistenza atomica per aggirare l’interruzione.

La verifica visiva Word mediante `render_docx.py` non è eseguibile su questa macchina: LibreOffice `soffice.exe` è assente. I controlli XML, relazioni e contenuti del report materiali sono conclusi, ma non sostituiscono il controllo dell’impaginazione stampata. Nessuna impaginazione Word viene dichiarata validata visivamente da questa revisione.

### Ripetizione e artefatti

Tutti gli output sono in `supporto/artefatti/calculation-library/`; sorgenti dei test in `supporto/test/`. La copia trasferibile è in `portable/`; il suo log finale è `standalone-checks.log`. I file di confronto sono `riferimenti-python.json` e `.log`; le prove UI sono in `ui/` e `ui-final/`. Per il report progetto consultare `ui/project-report/progress.txt`, non un esito di successo.

```powershell
dotnet run --project supporto/test/CalculationLibrary.Checks -c Release
dotnet run --project supporto/test/X.Verifiche -c Release -- --project-calculations supporto/artefatti/calculation-library/nuovo-test
./supporto/scripts/Test-CalculationUi.ps1
./supporto/scripts/Test-SoftwareReports.ps1
./supporto/scripts/Export-CalculationLibrary.ps1
```

I due aggregatori dei report devono continuare a fallire in modo esplicito se non raggiungono le rispettive attestazioni finali. Le formule e i solver alternativi conservano i limiti già documentati nei relativi dossier.


## TEORICA A29 — Teoria e validazione di Bridge Design
Edizione 30 settembre 2026 · Revisione 01 · ANTHEA BD T01

Questo rapporto descrive come ANTHEA costruisce, confronta e ordina le alternative di Bridge Design. È destinato a chi deve usare il predimensionamento per scegliere soluzioni da approfondire e vuole ricostruire le formule, la provenienza dei dati e il significato delle prove. Il contenuto è ricavato dalle sorgenti e dalle esecuzioni indicate in appendice, non da una ricostruzione ipotetica del sito di riferimento.

La conclusione è precisa: il software individua il minimo del proprio criterio fra le combinazioni ammesse ed effettivamente esplorate. Sono stati verificati calcoli ideali, quantità, ordinamento e vincoli; una prova indipendente ha confrontato 100 travi con un modello FEM separato e un intero computo con una griglia di 26 geometrie. Queste prove non attestano la sicurezza né l’ottimo globale di un ponte reale. Le regole di snellezza, le incidenze di armatura e i parametri geotecnici convenzionali richiedono una valutazione progettuale specifica.

### Ambito del motore e significato di ottimizzazione

Bridge Design usa il motore parametrico BridgeConcept di ANTHEA. I moduli Sezione in c.a. e Sezione composta da ponte impiegano invece le librerie indicate nell’interfaccia come GPC Engine. L’ottimizzazione di Bridge Design non richiama automaticamente quelle verifiche di sezione. Il nuovo riferimento GPC Engine non trasforma il predimensionamento in una verifica strutturale completa.

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

Le altezze aumentate vengono arrotondate verso l’alto a passi di 0,05 m; il valore al 100% conserva la quota automatica non arrotondata. Nella revisione documentata è stata corretta la sensibilità dell’arrotondamento al rumore numerico. Prima della correzione, una quota teorica esatta di 0,80 m poteva essere portata a 0,85 m perché rappresentata internamente come un numero appena superiore a 0,80.

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

### Come sono stati validati i risultati

La verifica è articolata in quattro livelli. Il primo controlla le formule ideali con soluzioni note; il secondo la gestione del problema di ricerca; il terzo usa un’implementazione indipendente; il quarto confronta dati esterni o risultati storici. Tenere distinti questi livelli impedisce di scambiare un test software superato con una validazione fisica sperimentale.

### Suite del motore rieseguita

Il 30 settembre 2026, dopo la correzione dell’arrotondamento, la suite BridgeDesign.Checks ha superato 44.259 asserzioni. Comprende formule chiuse per una e due campate, 200 travi diseguali con equilibrio globale, 224 combinazioni famiglia terreno pila, quantità, fondazioni, prezzi, salvataggio, export e ottimizzazione. Un’asserzione è un singolo confronto: il numero non indica altrettanti progetti distinti.

La campagna geometrica dell’audit comprende 1.008 configurazioni, 874 calcolate e 134 rifiutate esplicitamente. I rifiuti comprendono 116 fondazioni automatiche non convergenti e 18 antenne incompatibili con la tensione di riferimento. Il rifiuto atteso non è un risultato strutturale favorevole: dimostra che il programma segnala il limite anziché produrre un numero non utilizzabile.

Per l’ottimizzazione sono state rieseguite 27 ricerche di campagna, pari a 10.491 valutazioni, oltre a una prova limite con prezzi e CO₂ nulli. Sette famiglie sono state esplorate con tipologia bloccata e tre obiettivi; due lunghezze, 120 e 480 m, sono state esplorate liberamente raggiungendo tutte le 14 famiglie. Le prove verificano riproducibilità, immutabilità dell’input, vincoli, traccia dei tentativi, graduatoria, numero di alternative e Pareto.

L’enumerazione diretta delle piccole griglie nella suite C# è esterna alla funzione Optimize, ma riusa Calculate e i filtri. Verifica quindi il meccanismo di ricerca, non costituisce un secondo modello strutturale indipendente. Questa distinzione è essenziale per interpretarne correttamente il risultato.

### Secondo solutore indipendente dal sito

È stato aggiunto un verificatore Python che non carica librerie ANTHEA e non chiama il sito. Per 100 travi riproducibili, da 1 a 12 campate, costruisce la matrice di rigidezza di elementi Euler Bernoulli, applica i carichi nodali consistenti, blocca gli spostamenti sugli appoggi e risolve le rotazioni. Le lunghezze sono fra 5 e 70 m, q fra 2 e 500 kN/m ed EI fra 10⁵ e 10⁹ kN m²; sono presenti schemi continui e campate indipendenti, con seme 30092026.

Le reazioni si ottengono dal residuo della matrice globale. Taglio e momento sono ricostruiti dalle forze di estremità. La deformata usa funzioni di Hermite con il termine particolare del carico uniforme, qx²(l − x)²/(24EI), che ha spostamento e rotazione nulli agli estremi. Questo evita di confrontare la soluzione esatta con una sola interpolazione cubica approssimata. I segni e le unità sono allineati prima del confronto.

| Grandezza | Scarto assoluto massimo |
| --- | --- |
| Reazione | 1,46 × 10⁻¹¹ kN |
| Momento | 4,88 × 10⁻¹⁰ kNm |
| Taglio | 3,10 × 10⁻¹¹ kN |
| Freccia nei punti confrontati | 3,06 × 10⁻¹⁰ mm |

Gli scarti sono compatibili con l’aritmetica in virgola mobile. La soglia usata è 10⁻⁷ in unità della grandezza più 2 × 10⁻⁸ volte il massimo valore assoluto dei due risultati. La prova riguarda le equazioni elastiche ideali e i punti confrontati, non l’accuratezza della freccia reale di un impalcato fessurato o precompresso.

### Computo e ottimo ricostruiti separatamente

Il secondo controllo indipendente considera una soletta piena lunga 120 m e larga 11,30 m, quota 12 m, campate indipendenti, due spalle, pile circolari di diametro imposto 2 m, plinti diretti con lato longitudinale imposto 6 m e terreno Roccia. Le campate esplorate sono 8–16; per ogni numero si esaminano 100, 110 e 120% dell’altezza di riferimento. Le quote di riferimento e gli arrotondamenti sono costruiti in aritmetica razionale nel verificatore, prima della conversione in decimali.

Il verificatore deriva autonomamente volumi di soletta, fusti, pulvini, spalle e plinti; armature per incidenza; casseforme; appoggi, giunti, barriere e pavimentazione. Ricostruisce il costo con i prezzi esplicitati e la CO₂ con i fattori documentati. Le fondazioni sono imposte per evitare che il controllo riproduca il ciclo automatico del motore. Sono confrontati tutti i costi, le quantità principali, l’ordine e le condizioni di dominanza delle 26 geometrie distinte, ottenute da 27 combinazioni nominali.

Il minimo indipendente è una soletta con 8 campate da 15 m, altezza 0,916667 m, costo 2.270.714,97 euro, CO₂ 1.342,665 t, cls totale 2.266,445 m³ e armatura 315,909 t. Il massimo scarto di costo sull’intera griglia è 2,33 × 10⁻⁹ euro e quello della CO₂ 2,05 × 10⁻¹² t. Complessivamente il nuovo verificatore esegue 80.133 confronti numerici, oltre ai controlli di cardinalità, quote, quantità, ordine e Pareto.

Questo test ha rilevato il difetto 0,80 → 0,85 m relativo alle 11 campate al 120%. È stato corretto l’arrotondamento a 5 cm e sono state ripetute sia la prova indipendente sia la suite generale. Il difetto alterava alcune alternative; nel caso controllato non cambiava il vincitore. La prova certifica il minimo economico della griglia del caso imposto, senza estendere automaticamente tale conclusione a ogni famiglia e ogni combinazione possibile.

### Confronto storico con il sito

La campagna storica contiene 1.000 input unici realmente acquisiti dalla UI, 125 per ciascuna delle otto famiglie originali: 600 casi automatici, 160 variazioni d’altezza, 120 del numero di campate e 120 della resistenza del cls. Ogni caso è stato eseguito nel nostro motore in modalità native e resolved, per 2.000 esecuzioni. Nella seconda modalità alcune dimensioni adottate dal sito sono state imposte in ANTHEA, per separare l’effetto delle regole automatiche dalle altre differenze.

L’esito storico è stato 939 casi calcolati con differenze e 61 rifiutati in native; 932 calcolati con differenze e 68 rifiutati in resolved. Nessun caso coincideva su tutti gli indicatori confrontati. Non era quindi una prova di equivalenza. La mediana assoluta dello scarto relativo sul costo native era 33,28%, ma i listini e i perimetri erano diversi: quel valore non misura da solo l’errore di un solutore.

Una successiva regressione del 27 settembre sugli stessi input congelati ha prodotto 1.872 calcoli e 128 rifiuti, senza risultati non finiti. Questa è una riesecuzione su dati salvati, non una nuova acquisizione del sito e non una nuova dichiarazione di parità. Le sei famiglie aggiunte non appartengono al campione originario. Nel lavoro del 30 settembre la validazione nuova è il confronto indipendente descritto sopra; non sono stati acquisiti altri 1.000 casi live.

### Esempio operativo e lettura critica del vincitore

Un esempio distinto, utile per l’interfaccia, considera 120 m di lunghezza, 20,20 m di larghezza, quota 12 m, terreno Roccia, nessun ostacolo, tre campate iniziali in c.a.p. a I, continuità, pila a setto e fondazione automatica che risolve a plinto. La ricerca conserva pila, fondazione e continuità; libera tipologia e campate fra 1 e 8 e usa altezze 100–120% con passo 10.

Nel calcolo documentato il riferimento costa 2.332.503,86 euro ed emette 1.371,939 t di CO₂. La ricerca produce 91 tentativi, 70 ammessi e 69 geometrie distinte. Vince un cassone in c.a.p. con due campate da 60 m e altezza 2,565257 m: 2.049.525,73 euro e 1.287,035 t. In questo esempio lo stesso candidato minimizza costo e CO₂, quindi coincide anche con il compromesso. Non è un risultato generale della ricerca multiobiettivo.

Imponendo almeno cinque campate, il riferimento a tre campate non è più ammesso. Il nuovo minimo è una soluzione a T in c.a. a cinque campate, con costo 2.657.209,43 euro. Il costo superiore al vecchio riferimento non è un fallimento dell’ottimizzatore: è cambiato l’insieme delle soluzioni consentite. Moltiplicando tutti i prezzi per 0,8 o 1,2, la geometria vincente resta uguale e il costo scala della stessa quantità, mentre la CO₂ resta invariata.

![Costo e CO₂ delle geometrie ammesse nell’esempio da 120 m](../documentazione/Bridge_Design/figure/costo-co2.png)

La nuvola evidenzia alternative spesso vicine. Uno scarto di costo del 2% non basta per scegliere con sicurezza quando alcune voci aggregate hanno incertezza del 20–30% o maggiore. Per decidere serve ripetere la ricerca con ipotesi geotecniche e prezzi plausibili, approfondire i primi candidati e verificare se la scelta resta stabile. La fascia ±30% mostrata dal software non esegue questa analisi: è necessario variare realmente gli input e ricalcolare.

### Cosa rimane da validare prima dell’uso progettuale

Non sono disponibili, in questa attività, confronti sperimentali, consuntivi economici di ponti costruiti, una calibrazione statistica dei rapporti luce altezza, verifiche normative complete di tutte le alternative o una validazione indipendente completa delle strutture speciali. I test delle famiglie speciali controllano geometrie, equilibri ideali, masse, filtri e riproducibilità, ma non sostituiscono un’analisi strutturale dedicata.

Per portare un’alternativa allo studio di fattibilità occorre almeno definire il reale schema statico e costruttivo, l’inviluppo di traffico, i materiali, i dettagli di impalcato, il modello geotecnico, i vincoli territoriali e le lavorazioni mancanti. I controlli di sezioni, fasi, fatica, stabilità, fondazioni e appoggi devono essere eseguiti con modelli adeguati. Se queste verifiche aumentano le quantità, vanno riportate nel computo e nel confronto delle alternative.

Un buon uso del modulo consiste nel restringere un insieme di idee a poche alternative leggibili e riproducibili. Il risultato da conservare è l’insieme dati ipotesi soluzione quantità avvisi, insieme al motivo per cui è stata scelta un’alternativa. La sola etichetta ottimo non è una giustificazione progettuale.

### Tracciabilità e ripetizione delle prove

Le sorgenti principali sono X.Calculations/BridgeConcept.Optimization.cs, BridgeConcept.Calculation.cs, BridgeConcept.Foundation.cs, BridgeConcept.AdvancedDeck.cs, BridgeConcept.AdvancedStructure.cs e BridgeConcept.AdvancedCalculation.cs. La descrizione dei prezzi è in BridgeConcept.Pricing.cs; i prospetti tecnici in BridgeConcept.Technical.cs. La UI della ricerca è separata nei file BridgeDesignOptimization.cs e BridgeDesignOptimizationPanel.cs.

La nuova prova indipendente è in supporto/test/BridgeDesign.IndependentChecks. Program.cs esporta i risultati osservati del motore; verify.py costruisce i risultati attesi separatamente. Le evidenze finali sono in supporto/artefatti/bridge-design-guide-20260930/indipendente-corretto, con osservati.json e independent-summary.json. Il corpus precedente alla correzione è conservato nella sottocartella indipendente. La suite generale finale è nella sottocartella calcoli-corretti; gli esempi operativi sono nella sottocartella esempi.

Per ripetere dalla radice del repository, eseguire nell’ordine i comandi seguenti. Per Python usare un interprete con NumPy; nel lavoro descritto è stato usato il runtime Python fornito da Codex. La destinazione può essere sostituita con una nuova cartella di artefatti per conservare gli esiti precedenti.

dotnet run --project supporto/test/BridgeDesign.Checks -c Release -- supporto/artefatti/nuova-verifica/calcoli

dotnet run --project supporto/test/BridgeDesign.IndependentChecks -c Release -- supporto/artefatti/nuova-verifica/indipendente

python supporto/test/BridgeDesign.IndependentChecks/verify.py supporto/artefatti/nuova-verifica/indipendente

I file sorgente, i risultati e le versioni dei documenti sono identificati nel manifest JSON degli artefatti di questa attività. Il rapporto va riletto quando cambiano formule, filtri, listini o funzioni dell’ottimizzatore: i risultati di una vecchia campagna non validano automaticamente una revisione successiva.

### Riferimenti

[R1] ANAS, Elenco prezzi 2026 Rev 1, Nuove costruzioni e manutenzione programmata, giugno 2026. Consultato il 30 settembre 2026. Usato per il riscontro delle tariffe e delle inclusioni, non per le regole di dimensionamento. https://www.stradeanas.it/it/elenco-prezzi

[R2] Regione Emilia Romagna, Elenco regionale prezzi 2026 e correzioni 2026. Riscontro territoriale delle tariffe; non attribuisce una localizzazione al progetto. https://territorio.regione.emilia-romagna.it/osservatorio/elenco_regionale_prezzi/prezzario-2026

[R3] TheBridgeEng, Bridge Design. Riferimento della campagna storica conservata in bridge_design_site_1000. Non usato nel nuovo verificatore indipendente. https://thebridgeeng.com/design

[R4] FHWA IF 12 027, Manual for Design Construction and Maintenance of Orthotropic Steel Deck Bridges, febbraio 2012. Riferimento tecnico per l’estensione reale delle verifiche ortotrope. https://www.fhwa.dot.gov/bridge/pubs/if12027/if12027.pdf

[R5] TU Delft, Computational Modelling, Euler Bernoulli beam elements, capitolo 4.1. Riferimento della formulazione per rigidezze e funzioni di Hermite del verificatore indipendente. https://teachbooks.tudelft.nl/computational-modelling/structural_linear/euler_bernouilli.html

[R6] Decreto 17 gennaio 2018, Aggiornamento delle Norme tecniche per le costruzioni, pubblicazione in GU 20 febbraio 2018. Riferimento del quadro normativo, non certificazione del predimensionamento. https://www.gazzettaufficiale.it/eli/id/2018/2/20/18A00716/sg

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

Una shell descrive una superficie strutturale con comportamento membranale e flessionale. È utile per solette, pareti e piastre quando lo spessore è piccolo rispetto alle dimensioni nel piano e quando la variazione delle azioni nella superficie è decisiva. L'asse medio sostituisce il volume, mentre lo spessore partecipa alle rigidezze di membrana e flessione in modo differente.

### Membrana e flessione

Le risultanti membranali sono forze per unità di lunghezza; le risultanti flessionali sono momenti per unità di lunghezza. Non importare un momento di piastra espresso in kNm/m come momento totale di una trave. Occorre dichiarare la striscia resistente e integrare il risultato sul tratto interessato. La somma delle azioni sulle strisce deve restare coerente con equilibrio e percorso del carico.

### Assi e connessioni

La normale locale distingue le facce superiore e inferiore. Normali invertite possono cambiare la lettura dei segni senza cambiare la fisica. Controlla la continuità della mesh, le connessioni con beam e la rappresentazione degli appoggi. Un bordo vincolato su tutte le rotazioni può irrigidire artificialmente una soletta appoggiata.

### Errori e limiti

Un carico puntuale o un vincolo puntuale possono generare picchi che crescono al raffinarsi della mesh. La convergenza della reazione globale non garantisce la convergenza della tensione puntuale. Rappresenta la superficie di contatto reale quando la verifica locale lo richiede. Una shell non sostituisce automaticamente un modello tridimensionale dei nodi massicci.

### Riepilogo

Scegli la shell quando devi descrivere distribuzioni nella superficie; mantieni espliciti assi, spessore, unità delle risultanti e trasferimento ai dettagli. Anthea non dispone di un solutore shell generale: questa pagina aiuta a interpretare dati provenienti da un modello esterno.

## Releases e connettività

La connettività stabilisce quali incognite sono condivise fra elementi; una release stabilisce quali azioni non vengono trasmesse a un'estremità. Queste due decisioni determinano il percorso dei carichi prima ancora di assegnare una rigidezza.

### Controllare un nodo

Individua sul disegno le traslazioni e rotazioni possibili, quindi verifica se il modello permette proprio quei movimenti. Una cerniera ideale nel piano trasmette due forze e non trasmette il momento coniugato alla rotazione libera. Un link elastico trasmette una forza proporzionale allo spostamento relativo: una rigidezza enorme è un'approssimazione che può peggiorare il condizionamento numerico.

### Meccanismi e collegamenti rigidi

Se una parte può muoversi rigidamente senza deformare nessun elemento, il modello ha un meccanismo. Non correggerlo con una molla casuale. Verifica prima i vincoli fisici e le release. Al contrario, troppe connessioni rigide possono impedire deformazioni reali e generare azioni spurie. Un collegamento rigido deve trasferire anche i momenti prodotti dal braccio geometrico.

### Riepilogo

Confronta il modello numerico con uno schizzo cinematico. Usa un carico unitario per verificare il movimento consentito e la distribuzione delle reazioni. Conserva una descrizione delle ipotesi del nodo accanto ai risultati.

## Instabilità delle aste compresse

Un'asta compressa può perdere stabilità senza che tutta la sezione raggiunga la resistenza del materiale. Una piccola deviazione laterale produce un momento aggiuntivo dovuto alla forza assiale; il momento aumenta la deviazione, che a sua volta amplifica il momento. La rigidezza flessionale contrasta questa retroazione.

### Modello di Eulero

```math
N_{cr} = \frac{\pi^2 EI}{L_0^2}
```

E è il modulo elastico, I l'inerzia nel piano della deformata e L₀ la lunghezza efficace. Con E in N/mm², I in mm⁴ e L₀ in mm, Ncr è in N. La formula descrive una biforcazione elastica di un'asta ideale; non è direttamente la resistenza di progetto di un'asta reale imperfetta. Raddoppiare L₀ riduce il carico critico a un quarto.

### Lunghezza libera di inflessione

L₀ rappresenta il vincolo efficace nel modo di instabilità considerato, non semplicemente la lunghezza disegnata. Per un'asta ideale isolata incernierata a entrambe le estremità coincide con la lunghezza fra cerniere. Per telai e vincoli elastici dipende dal comportamento del sistema. Un ritegno è efficace solo se ha rigidezza, resistenza e percorso del carico adeguati.

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
