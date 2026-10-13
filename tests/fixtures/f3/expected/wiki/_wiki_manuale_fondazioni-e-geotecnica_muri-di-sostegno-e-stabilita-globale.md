## Muri di sostegno e stabilità globale

Il modulo tratta muri a mensola e a gravità. Le verifiche locali e la stabilità globale hanno modelli e combinazioni distinti. La [guida ai muri](wiki:guida-muri-di-sostegno-con-stratigrafie-di-monte-e-valle) descrive il percorso Input e Verifiche, le stratigrafie di monte e valle, gli attriti, i valori di calcolo e la preparazione del profilo globale; [portanza, cedimenti e armature](wiki:portanza-sismica-cedimenti-spostamenti-e-armature-rev07) approfondisce i modelli aggiuntivi.

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

Il caso stratificato della guida pratica ha H=3 m, t=0,45 m, strati profondi fino a y=−10 m e ricerca automatica fino a 6,90 m. Il modello completo è salvato in supporto/artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato.anthea; i valori di riferimento dei due casi statici sono F=1,176315 e F=1,075737, con γR=1,10. Il secondo tasso è 1,022555 e non soddisfa il confronto.

Nel motore Bishop restano esclusi i meccanismi non circolari e la liquefazione. La ricerca circolare nel dominio assegnato non esaurisce la stabilità generale del versante.

