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

Rg non coincide automaticamente con eta H. L'efficienza di capacità richiede di ricalcolare il palo singolo con kh o nh ridotti, a parità di EI, lunghezza, vincolo, stratigrafia e criterio di capacità o spostamento; il modulo non esegue questo ricalcolo. Solo il rapporto fra le capacità ricalcolate può essere chiamato efficienza di capacità. Nessuna potenza o conversione empirica di Rg viene utilizzata.

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

Formule e tabelle implementate seguono una specifica interna di ANTHEA. Le fonti ufficiali consultabili sono indicate per consentire il controllo dell'edizione: l'applicabilità resta distinta dal semplice corretto calcolo numerico.

- Davisson M T, 1970, Lateral Load Capacity of Piles, Highway Research Record 333, pagine 104–112. La tabella di riduzione è quella ripresa nella specifica interna; non si afferma una verifica diretta della sua presenza nel lavoro originale.
- Davisson M T e Salley J R, 1970, Model Study of Laterally Loaded Piles, ASCE, volume 96, numero 5, pagine 1605–1627.
- FHWA GEC 12 volume I, 2016, Design and Construction of Driven Pile Foundations, FHWA-NHI-16-009: valori AASHTO 2014.
- FHWA GEC 10, 2018, Drilled Shafts Construction Procedures and Design Methods, FHWA-NHI-18-024: tabella 10-41 dei p-multiplier.
- Rollins et al., 2006; FEMA P-751, 2012: relazioni logaritmiche riprese nella specifica interna.
- Caltrans, California Amendments ad AASHTO LRFD, settembre 2025, paragrafo 10.7.2.4, equazioni 1–6 e tabella 10.7.2.4-2: interazioni palo-palo e modifica Caltrans.

La validazione automatica verifica nodi e interpolazioni, esempi numerici, leading e trailing, soglie, palo isolato, geometrie irregolari, simmetria per inversione del carico, invarianza per traslazione e rotazione congiunta di geometria e carico, archiviazione e rappresentazione WPF.

