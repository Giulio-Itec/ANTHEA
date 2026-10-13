## Muri di sostegno con stratigrafie di monte e valle

La scheda Input definisce muro, terreni e azioni; Verifiche raccoglie gli esiti locali e globali. Prepara separatamente stratigrafie delle spinte, terreno di fondazione e profilo profondo della stabilità globale. Un modello completo per una verifica può essere insufficiente per le altre.

Le stratigrafie di monte e valle sono affiancate, indipendenti o collegate per spessori e proprietà; le profondità partono dalle rispettive superfici. Hlib è la distanza dalla sommità al terreno di valle: Dv=H+t−Hlib. Il terreno davanti al muro entra nei pesi, nei momenti, nelle sollecitazioni della mensola e nel ricoprimento efficace della portanza. La passiva richiede attivazione e frazione mobilitata; è esclusa nel sisma. Gli attriti del muro e della fondazione sono assegnabili oppure ricavati da φcv,k e tipo di interfaccia. Il valore a volume costante va caratterizzato, senza sostituirlo automaticamente con quello di picco.

Valori di calcolo consente di interrogare e modificare gli input e leggere coefficienti effettivi per combinazione, pesi, attriti, pressioni e sollecitazioni. Le forze risultanti dipendono dagli input e non si possono forzare. Le combinazioni sono modificabili: il preset locale è A1+M1+R3, quello globale A2+M2+R2. Azioni eccezionali e sisma Mononobe–Okabe o Wood semplificato sono espliciti. Il terreno del lato selezionato può essere trasferito ai moduli dei pali; la sezione selezionata può essere inviata al modulo c.a.

La stabilità globale Bishop ha un motore separato e un proprio profilo esteso, anche con due colonne profonde e confine verticale assegnato. Il pulsante Stabilità globale apre il percorso; al primo accesso a un profilo vuoto ne prepara i dati e attiva la verifica. Gli strati si inseriscono per spessore, con quota del fondo calcolata automaticamente. La precompilazione non prolunga le indagini: rilievo, terreni profondi e falda del sito vanno controllati e confermati. Il disegno rappresenta anche il terreno sotto il piano di posa. Parametri, dominio e combinazioni restano interrogabili e modificabili.

Conserva nel report le due stratigrafie e i coefficienti effettivi. Per le verifiche aggiuntive consulta [Portanza, cedimenti e armature](wiki:guida-portanza-sismica-cedimenti-spostamenti-e-armature-rev07).

### Materiali e copriferro del muro

La scheda Materiali del muro usa gli stessi cataloghi GPC del verificatore delle sezioni in calcestruzzo. Selezionare la classe del CLS e dell'acciaio; le proprietà di catalogo sono protette. Personalizzato consente di assegnare resistenze, modulo dell'acciaio, deformazione ultima e nome. Il diagramma costitutivo e i coefficienti αcc, γc e γs sono interrogabili; fcd, fyd ed Ecm mostrano i valori effettivi. Il trasferimento Apri sezione in c.a. conserva questi dati.

Nei progetti, le schede Calcestruzzo e Acciaio del ramo forniscono i materiali al muro secondo le normali regole di ereditarietà. Il confronto dei dati comuni include esposizione, aggregato, vita utile, tolleranza e scelte di durabilità. Il copriferro adottato è condivisibile con le sezioni e viene confrontato con il minimo della scheda Materiali, usando il diametro massimo effettivamente presente nel muro. Adotta copriferro minimo applica esplicitamente la proposta. Il controllo non aumenta silenziosamente il copriferro e non modifica geometria, carichi o quantità di barre.

Gli archivi precedenti mantengono le resistenze e il legame dell'acciaio con cui erano stati calcolati; l'apertura non li riclassifica automaticamente come B450C. Nei muri a gravità le proprietà del CLS sono attive quando si sceglie Calcestruzzo non armato; la muratura non eredita proprietà meccaniche del CLS o dell'armatura non utilizzate.

### Avviare la verifica globale

La verifica globale riguarda il possibile scivolamento del muro insieme al terreno sottostante. I soli parametri del terreno di fondazione utilizzati per la portanza non definiscono la stratigrafia necessaria per questa analisi.

1. Premere Stabilità globale nella barra superiore. Se il profilo globale è vuoto, il programma copia i dati locali disponibili, propone superfici orizzontali sui due lati e attiva la verifica. Riaprire il pannello conserva il lavoro già impostato. Prepara dal muro sostituisce invece profilo e strati con una nuova proposta: usarlo solo quando si vuole ripartire dai dati locali.

2. Controllare gli strati di monte e valle, anche sotto la fondazione. Selezionare una riga per completare le proprietà; correggere il rilievo e la linea di falda se diversi dalla proposta. Controllare anche la condizione Drenata o Non drenata e l’opzione Includi il sisma SLV. Il messaggio in alto indica il primo dato mancante; il suggerimento sul messaggio elenca gli altri.

3. Dopo il controllo del sito, spuntare Ho controllato profilo, strati e falda del sito e premere Calcola globale. Il risultato si apre in Verifiche, vista Stabilità globale. Le modifiche geotecniche del percorso annullano la conferma e il risultato precedente. Calcola globale esegue la sola analisi globale anche quando le verifiche locali hanno input fuori campo.

### Inserire spessori e proprietà dei terreni

La tabella globale mostra Terreno, Spessore e Fondo y. Gli strati vanno dall’alto verso il basso e ogni colonna parte dalla propria superficie presso il muro. Il fondo è una quota calcolata, non un secondo spessore da inserire. L’origine (0;0) è al bordo di valle del piano di posa: x cresce verso monte e y verso l’alto. Fondo y=−2 m significa 2 m sotto la fondazione.

Esempio: H=3 m, t=0,45 m, terreno di valle al piano di posa. A monte si può descrivere un riempimento fino a y=0, seguito da due terreni profondi. A valle non occorre ripetere il riempimento che si trova soltanto sopra la fondazione sul lato di monte.

| Colonna | Terreno | Spessore in m | Fondo y in m |
| --- | --- | --- | --- |
| Monte | Riempimento | 3,45 | 0 |
| Monte | Alluvioni | 2 | −2 |
| Monte | Ghiaia | 8 | −10 |
| Valle | Alluvioni | 2 | −2 |
| Valle | Ghiaia | 8 | −10 |

Selezionando una riga compaiono γ e γsat in kN/m³ e le resistenze caratteristiche: φ′k in gradi e c′k in kPa per Drenata; cu,k in kPa per Non drenata. Un nuovo strato ha proprietà da completare. Stesso nome del terreno significa stesso colore nelle due colonne e nel disegno, ma non collega automaticamente i valori.

![Profilo dell’esempio stratificato con terreno sotto la fondazione e limiti di ricerca evidenziati in blu.](../artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato-profilo.png)

La vista principale del muro rappresenta il terreno sotto il piano di posa. Prima di definire la globale mostra il terreno di fondazione con spessore grafico indicativo; con il modello globale attivo usa gli strati profondi. La vista globale completa la lettura nell’area di ricerca e permette di interrogare i dati. Una porzione disegnata non estende la profondità conosciuta delle indagini.

### Controllare rilievo falda sisma e ricerca

Rilievo e falda si modificano nel gruppo dedicato tramite punti x, y. La falda è una linea di quote y assolute rispetto al piano di posa, non una profondità dalla superficie. Il modello proposto va adattato al rilievo reale: non ricava automaticamente pendenze o terreni profondi.

L’attivazione del sisma globale viene ripresa dal muro durante la precompilazione. Il gruppo Falda e sisma espone l’opzione e la sorgente dei coefficienti. Da sito usa i dati del progetto con βs=0,38; in alternativa si assegnano kh e il modulo di kv. Il coefficiente globale non va confuso con quello delle spinte del muro. Se i dati del sito non sono disponibili, occorre completare la sorgente scelta; non viene assunto un valore sismico implicito.

Con Area di ricerca Automatica i limiti orizzontali seguono il rilievo e la profondità si adatta agli strati noti di entrambi i lati, con massimo iniziale pari a 2(H+t). La proposta non dimostra che l’estensione sia sufficiente. Per cambiare i limiti scegliere Assegnata e aprire Limiti e precisione; nodi, conci e raffinamenti sono sempre modificabili. Gli archivi precedenti conservano i limiti già salvati come Assegnati.

### Leggere il risultato e riprodurre l’esempio

Controllare l’Esito di tutte le combinazioni. F è il fattore trovato con i parametri di progetto; il tasso di lavoro è η=γR/F. Il caso iniziale è quello con tasso maggiore, non necessariamente quello con F minore se γR cambia; un caso privo di superficie valida ha priorità. La tabella e il cerchio restano interrogabili per ogni combinazione.

Soddisfatta nel dominio esplorato richiede η≤1 e controlli della ricerca superati. Minimo sul bordo richiede di ampliare l’area, sempre entro rilievo e indagini disponibili. Ricerca incompleta o discretizzazione non convergente impediscono una conclusione favorevole anche con η≤1. Non modificare arbitrariamente i parametri del terreno per ottenere un esito favorevole.

La scala comune è 0–0,50 blu, 0,50–0,70 verde, 0,70–0,90 giallo, 0,90–1,00 arancio, oltre 1,00 rosso; grigio per controlli incompleti. Nel disegno le linee verticali individuano i conci della superficie critica. La selezione nella tabella permette di leggere pesi, pressioni interstiziali, parametri ridotti, resistenze e azioni.

Per riprodurre il caso illustrato aprire supporto/artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato.anthea e premere Calcola globale, senza ripreparare i dati locali. Le proprietà e il dominio completi sono riportati nell’esempio seguente. Il secondo caso statico ha F=1,075737 e η=1,022555: l’esito non soddisfatto è conservato nell’esempio. È un controllo interno ANTHEA.

### Esempio salvato e risultati ripercorribili

Apri `supporto/artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato.anthea`. Premi Stabilità globale e poi Calcola globale. Il file contiene già input e conferma per questo esempio didattico. Non premere Prepara dal muro, che sostituirebbe gli strati globali con quelli locali.

Muro a mensola: H=3 m, t=0,45 m, fusto 0,40/0,25 m, mensola di valle 0,80 m e di monte 1,80 m; peso del muro 25 kN/m³. Terreno di valle fino al piano di posa, qk=10 kPa uniforme a monte. Profilo da x=−13,80 m a valle a x=16,80 m a monte; confine delle colonne x=1,20 m. I pesi γ/γsat sono 18/20, 19/21 e 20/22 kN/m³ rispettivamente per Riempimento, Alluvioni e Ghiaia; φ′k è 30°, 28° e 36°, con c′k nullo. Non sono presenti falda e sisma.

Ricerca: uscite x=−13,80/−0,10 m, ingressi x=3,10/16,80 m, profondità 0,10/6,90 m; 9 nodi per direzione, 60 conci iniziali, 4 raffinamenti. Preset statico A2–M2–R2, γR=1,10. Si ottengono:

| Combinazione | F | η=γR/F | Esito |
| --- | --- | --- | --- |
| Globale A2–M2–R2 1 | 1,176315 | 0,935124 | Soddisfatta nel dominio esplorato |
| Globale A2–M2–R2 2 | 1,075737 | 1,022555 | Non soddisfatta |

![Superficie della seconda combinazione: il tasso supera 1 e il cerchio è rosso. L’esempio conserva l’esito sfavorevole per mostrare la lettura della verifica.](../artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato-esito.png)

Le tabelle dei conci sono in `esempio-stratificato-conci.csv`, accanto al modello. Questo è un controllo interno ANTHEA. Cambiando precisione o dominio possono cambiare leggermente la superficie critica e i valori.

