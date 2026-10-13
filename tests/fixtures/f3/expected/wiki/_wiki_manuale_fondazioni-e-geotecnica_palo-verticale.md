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

I coefficienti parziali sono quelli della NTC 2018 Tab. 6.4.II (R3): γs = 1,15 per la laterale in compressione e γt = 1,25 per la laterale in trazione per ogni tecnologia; γb della punta dipende dalla tecnologia.

| Tecnologia | γb |
| --- | --- |
| Battuto (profilato d'acciaio, tubo d'acciaio chiuso, calcestruzzo prefabbricato o gettato in opera) | 1,15 |
| Trivellato | 1,35 |
| Elica continua | 1,30 |
| Micropalo (perforato e iniettato, senza valori propri nella norma) | 1,35 |

Il foglio nuovo parte dal palo trivellato con γb = 1,35. Quando si cambia la tecnologia, e con Reset, il foglio propone il γb della tabella. Il valore resta modificabile e il foglio conserva quello scelto. Un foglio senza γb usa quello della tecnologia nel calcolo, nella relazione e nei progetti. Se γb differisce da quello della tecnologia, il calcolo lo usa e lo dichiara negli avvisi. Nei progetti γb si confronta e si condivide solo tra fogli con la stessa esecuzione della tabella: pali battuti con pali battuti, elica continua con elica continua, trivellati con trivellati e micropali. Gli altri coefficienti si confrontano tra tutti i fogli dei pali verticali.

I fogli del formato precedente, privi del numero di versione del foglio, proponevano 1,35 per ogni tecnologia. Per i pali battuti e a elica continua di questi fogli, un γb di 1,35 o mancante vale come il γb della tabella. Il foglio è aggiornato una sola volta, all'apertura o quando un progetto vi trascrive dei valori, e riceve il numero di versione: un 1,35 scelto in seguito resta quindi 1,35. Il calcolo e la relazione applicano la stessa regola senza modificare il documento. I pali trivellati e i micropali non cambiano, e ogni altro valore di γb resta quello memorizzato. Sui casi di prova, con il γb della tabella la resistenza di progetto drenata alla profondità massima aumenta del 4,3–7,0 % per i pali battuti e dell'1,0–1,2 % per l'elica continua.

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

Feld conta le coppie adiacenti ortogonali e diagonali: P = (nx−1)ny + nx(ny−1) + 2(nx−1)(ny−1), poi η = 1 − 2P/(16 nx ny). Per questi due metodi il modulo applica la stessa efficienza a compressione e trazione. L'opzione manuale permette valori distinti. Un risultato non positivo viene rifiutato.

La profondità analizzabile deve essere coperta dalle stratigrafie necessarie. Sono fuori dal modello automatico cedimenti, attrito negativo, resistenza del blocco di gruppo e interazione completa terreno struttura. Il rapporto Ed/Rd riguarda la resistenza assiale considerata e non esprime da solo la prestazione di esercizio.

