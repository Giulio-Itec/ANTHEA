## Modulo Sezione in c.a.

### Controllare norma e dati ereditati

La norma selezionata governa i coefficienti iniziali. Cambiarla richiede il controllo dei parametri assegnati e il ricalcolo. Un documento salvato da una versione di ANTHEA che non registrava ancora la norma si apre con NTC 2018, la norma con cui era stato calcolato. Un documento attuale senza norma è incompleto, perché ANTHEA la registra sempre: l'apertura si ferma con un messaggio e occorre ripristinare una copia del documento. I campi assenti integrati dalla preparazione del documento sono elencati in Valori integrati dal documento precedente. Valori nulli, testi errati e norme sconosciute richiedono invece una correzione.

Il report usa i dati e la norma della copia che ha prodotto il risultato. Se la provenienza manca o è incoerente, occorre ricalcolare; cambiare un campo nella scheda non aggiorna retroattivamente un risultato salvato.


Questo modulo verifica una sezione in calcestruzzo armato a partire da geometria, materiali, armature e azioni assegnate. Permette di esplorare domini resistenti, stati tensionali e deformativi, taglio, torsione e dettagli nei limiti indicati dalle rispettive schede. Non determina le azioni di un telaio completo e non sostituisce l'analisi globale della struttura.

### A cosa serve e quando usarlo

Usalo quando disponi delle terne N, Mx e My per le combinazioni rilevanti e vuoi confrontarle con la risposta della sezione. È adatto anche al controllo di sensibilità: mantenendo le azioni, modifica una dimensione o l'armatura e osserva il dominio. Per un elemento lungo e compresso la verifica della sezione non esaurisce l'eventuale problema di instabilità del sistema.

[Teoria del modello Beam e trasferimento delle azioni](/wiki/manuale/fem/elementi-beam)

### Input geometria e unità

| Dato | Unità | Significato e controllo |
| --- | --- | --- |
| Sezione | scelta | Circolare, Rettangolare, A T; la sezione generica richiede un contorno definito |
| b e h | mm | Larghezza e altezza positive della sezione rettangolare |
| D | mm | Diametro positivo della sezione circolare |
| bf, bw, hf | mm | Ala, anima e spessore ala della T; bw non supera bf e hf è inferiore a h |
| Copriferro netto | mm | Distanza dal bordo alla staffa; non è la distanza al centro della barra longitudinale |
| Discretizzazione circolare | numero | Da 12 a 720 lati, multipli di 4 |

Le dimensioni sono in millimetri anche se le azioni provengono da un modello in metri. Controlla nel disegno che il contorno e tutte le barre restino nella posizione attesa. Un foro o una forma generica devono avere geometria compatibile con le opzioni realmente abilitate, non semplicemente un nome selezionato.

### Input materiali armature e default

Il nuovo foglio rettangolare parte da b = 600 mm, h = 800 mm, copriferro netto 70 mm, calcestruzzo C35/45 e acciaio B450C. L'armatura iniziale è 4Ø20 superiori, 6Ø24 inferiori e 2Ø16 per lato; le staffe sono Ø10 a passo 200 mm. Sono valori iniziali modificabili, non dimensioni minime normative né una proposta esecutiva.

Le resistenze dei materiali sono in MPa, equivalenti a N/mm². La selezione dal catalogo trasferisce i parametri effettivi della voce scelta; controlla il riepilogo dopo una selezione. Numero e diametro delle barre devono descrivere l'armatura presente; le barre devono rimanere nel contorno con copriferro e spaziatura coerenti. Le opzioni specifiche per pali o zone dissipative richiedono il contesto corrispondente.

Scegli la normativa prima di personalizzare i coefficienti. Cambiarla ripristina i coefficienti della nuova classe normativa: ricontrolla eventuali modifiche manuali. I default non attestano l'applicabilità della norma al caso concreto. Le funzionalità predisposte nei coefficienti non implicano che ogni meccanismo sia implementato.

### Input azioni e convenzioni

N è in kN; Mx e My in kNm. Nell'interfaccia corrente N è negativo a compressione. Il workspace migra gli archivi precedenti con compressione positiva; l'importazione delle azioni da un modello esterno richiede invece il controllo esplicito dei segni. Nel piano della sezione, Mx è associato alla coordinata y e My alla coordinata x con la convenzione indicata dal modulo. Mantieni la terna della stessa combinazione e documenta la trasformazione dagli assi del modello esterno.

Il foglio ordinario iniziale mostra N = -2500 kN, Mx = 500 kNm e My = 250 kNm nelle famiglie iniziali, dopo la preparazione del workspace. Il pulsante Apri esempio della Wiki sostituisce questi valori con la combinazione didattica descritta sotto e lascia vuote le altre famiglie. Non interpretare una tabella vuota come una verifica superata.

### Descrizione del calcolo

La sezione integra il contributo del calcestruzzo e delle barre con i legami costitutivi previsti dal metodo selezionato. I domini confrontano le terne di azione con la capacità resistente. Le analisi di esercizio usano invece le proprie ipotesi per tensioni, fessurazione e omogeneizzazione. Non trasferire un coefficiente di utilizzo di un dominio a un controllo di taglio o di fessurazione.

I ricalcoli e le viste dipendono dalla scheda attiva. Aspetta la conclusione del calcolo e controlla warning e combinazione selezionata prima di esportare. Un input visivamente corretto ma non ancora calcolato non rende corrente un risultato precedente.

### Output e lettura dei risultati

| Output | Interpretazione | Controllo dell'utente |
| --- | --- | --- |
| Dominio 3D | Capacità nello spazio N-Mx-My | Assi, segni e posizione della terna di azione |
| Dominio 2D | Sezione del dominio per la scelta attiva | Non confonderla con ogni direzione possibile |
| Tensioni e deformazioni | Risposta delle fibre e delle barre | Fibra compressa, regime e ipotesi costitutive |
| Fessurazione | Risultato della combinazione SLE attiva | Azioni SLE, esposizione e parametri effettivi |
| Taglio e torsione | Controlli dedicati della scheda | Staffe presenti, braccia resistenti e modello |

Leggi il nome della combinazione e il metodo prima del colore o del rapporto. Un esito favorevole riguarda quel controllo con quegli input. Guarda anche la deformata di sezione e il bordo più sollecitato: un segno sbagliato può produrre un risultato plausibile ma riferito all'armatura opposta.

### Warning e problemi frequenti

> ATTENZIONE
> La sezione non verifica da sola instabilità globale, dettagli del nodo, aderenza in ogni possibile giunto o azioni mai assegnate. I limiti delle schede e le funzioni predisposte restano quelli documentati nella guida teorica globale.

Se il contorno non è valido controlla dimensioni, fori e geometria della T. Se le barre non sono dove previsto controlla copriferro netto, diametro staffa e diametro longitudinale. Se le azioni sembrano invertite confronta la terna degli assi e N negativo a compressione. Se un risultato SLE manca verifica che la famiglia abbia combinazioni assegnate. Se un ricalcolo non termina, conserva il file e annota messaggio, input e scheda, senza considerare corrente l'ultimo grafico disponibile.

### Esempio completo dalla Wiki

La trave appoggiata del capitolo Beam ha L = 8 m e q = 25 kN/m, assunto già come azione di progetto. Il momento massimo è qL²/8 = 200 kNm. Apri esempio crea una sezione rettangolare 600 × 800 mm, C35/45, B450C, con le armature iniziali sopra descritte e una sola terna plastica N = 0, Mx = 200 kNm, My = 0.

1. Apri l'esempio e verifica geometria, copriferro e armature nel disegno.
2. Controlla che la combinazione Wiki contenga esattamente la terna 0, 200, 0.
3. Esegui l'analisi del dominio prevista dalla scheda e leggi il punto di domanda rispetto alla capacità.
4. Modifica h mantenendo b e le azioni: confronta il dominio e la posizione delle armature.
5. Inverti Mx e osserva il diverso ruolo dei due bordi armati.
6. Salva il foglio con nome; assegna separatamente combinazioni di esercizio se vuoi studiare fessurazione o tensioni SLE.

Il taglio di appoggio è 100 kN ma non è il taglio della stessa sezione di mezzeria a momento massimo. La Wiki non inserisce una terna simultanea artificiale di massimi provenienti da sezioni diverse. Per il controllo di taglio usa una sezione e una combinazione coerenti, inserendo i dati nella scheda dedicata.

### Teoria collegata e riepilogo

[Elementi Beam](/wiki/manuale/fem/elementi-beam) spiega come ottenere e trasferire le azioni. I capitoli Sezione in c.a. e Calcestruzzo armato della guida teorica descrivono i modelli costitutivi e i limiti del motore. Il comando Apri modulo nella Wiki raggiunge lo stesso strumento disponibile in Moduli singoli; Come funziona nella barra del modulo torna a questa guida.

Il percorso completo richiede un modello globale plausibile, azioni coerenti, una sezione fisicamente realizzabile e verifiche appropriate ai diversi stati limite. Un esempio numerico aiuta a imparare il metodo; la verifica del progetto richiede i dati reali e tutti i meccanismi rilevanti.

### Domini tridimensionali e bidimensionali

Le schede dominio 3D e dominio 2D permettono di esaminare le resistenze della sezione e il punto richiesto. Scegliere il criterio di ricerca coerente con il problema: mantenere N costante e aumentare il momento non è lo stesso percorso che mantenere costante l'eccentricità. Il coefficiente risultante dipende anche da questo percorso.

Un dominio bidimensionale è una sezione o una proiezione del problema. Se il punto ha componenti fuori dal piano rappresentato, leggere l'indicazione di proiezione e non trattare l'immagine come una verifica completa della pressoflessione deviata. Con «Proietta azioni sul piano» il grafico N–M mostra il punto verificato: Mθ = Mx cos θ + My sin θ con N invariato, sulla retta dall'origine a Rd. I quattro momenti rapidi Mx positivo e negativo, My positivo e negativo sono utili per orientarsi, ma non descrivono da soli l'intero dominio N Mx My.

Nei nuovi fogli scegliere separatamente lo Stato limite (SLU o SLV) e il Criterio resistente (Plastico o Elastico). Il criterio si imposta nella verifica, nelle schede dominio 3D e dominio 2D, e si applica alle combinazioni dei due stati limite. Cambiarlo aggiorna i domini e i risultati. Non vengono calcolati sempre entrambi i criteri. La ricerca della resistenza, ad esempio N costante, rimane una scelta distinta.

I documenti precedenti mantengono i gruppi Plastico ed Elastico fino alla conversione esplicita delle sollecitazioni. In quei documenti le chiavi interne SLU e SLV indicavano tali gruppi: non assegnavano automaticamente uno stato limite sismico.

### Tensioni e fessurazione

Aprire la scheda dedicata, scegliere la combinazione e leggere prima lo stato tensionale: zona compressa, barre tese, tensione massima, asse neutro e deformazioni. Le deformazioni a video possono essere in per mille; i coefficienti di curvatura hanno un'altra dimensione. Non confrontare i due numeri senza conversione.

La fessurazione dipende anche da durata, aderenza, copriferro alle barre, diametri, interassi e area efficace di calcestruzzo teso. Con il campo «c barra» vuoto il copriferro della formula è misurato sulle barre efficaci verso la faccia tesa; un valore assegnato lo sostituisce. Aprire il dettaglio del calcolo di wk: permette di capire se governa il termine di deformazione minima o quello corretto per il tension stiffening, e se si applica il ramo di barre ravvicinate o distanziate.

La trazione dell'intera sezione segue un ramo specifico con regioni di bordo. Le superfici interne dei fori compatibili hanno controlli dedicati; una superficie tesa priva di armatura efficace rende la verifica non soddisfatta, una priva di interasse definito la lascia incompleta. Il disegno completo non dimostra la completezza dei controlli. Analogamente, una soluzione tensionale non lineare o una sezione con tendini può avere disponibilità diversa per il controllo di fessurazione. Leggere lo stato della singola verifica, non soltanto il grafico delle tensioni.

### Taglio torsione e dettagli

Nella scheda taglio e torsione impostare staffe, passo, inclinazione e armatura longitudinale pertinente. Le azioni dei nuovi fogli provengono dalle stesse righe SLU e SLV usate nei domini; modificarle con Gestisci sollecitazioni. Controllare bw, d e braccio interno ricavati. Per sezioni circolari il ramo adottato è quello specifico per pali descritto nella guida teorica; non coincide automaticamente con una formula generica per travi rettangolari.

La torsione richiede un dettaglio chiuso compatibile e armatura longitudinale disponibile dopo le esigenze di flessione. Non assegnare come disponibile tutta l'armatura senza aver verificato questa condizione. La verifica combinata usa anche un'interazione conservativa fra torsione e i due tagli, esplicitata nel dettaglio.

Nella scheda dettagli costruttivi scegliere il tipo di elemento e completare le informazioni su diametri, passi, sovrapposizioni, ancoraggi e copriferro. Gli ancoraggi seguono il campo Barre della sezione: con Liscia restano da completare, anche nelle proposte della ricerca delle armature. Per i minimi di armatura dei pilastri la scheda usa la compressione più gravosa fra tutte le combinazioni del foglio e indica quella che governa. Se governa una combinazione SLE, il valore è segnalato come di esercizio: controllare che le combinazioni SLU e SLV siano complete. «Da completare» non equivale a «Verificato». I controlli automatici riguardano le condizioni esplicitamente rappresentate; ganci, nodi, confinamento e situazioni esecutive speciali possono richiedere valutazioni aggiuntive.

### Curva momento curvatura e aggiornamento

La curva M χ si avvia con il proprio comando. Assegnare N, direzione del momento, numero di passi e distribuzione dei punti. Il campo «CLS teso nelle risposte», Trascurato oppure Tension stiffening, riguarda solo i punti della curva: punto limite e verifiche non lo usano. Esaminare la progressione della risposta e le deformazioni terminali. La curva arriva al limite trattato dal motore e non descrive automaticamente un ramo post picco di degradazione della struttura.

Nel report di progetto la curva viene ricalcolata automaticamente con N, direzione, passi e opzioni salvati nel foglio. Il capitolo comprende valori limite, tabella dei punti e grafico; non serve aprire la scheda né avviare prima il comando Calcola curva. La preparazione lavora su una copia dei dati. Se il percorso non è valido o contiene trefoli, la curva è segnalata come non disponibile con il motivo e le altre verifiche restano nel report. Interrompere la preparazione conserva il documento precedente. Il report del singolo foglio continua a usare la curva calcolata nella scheda.

Se si modifica l'input mentre sono visibili risultati precedenti, le sezioni strutturali possono segnalarli come «DA AGGIORNARE». Quel risultato non deve essere esportato o letto come riferito ai nuovi dati. Attendere il completamento del ricalcolo e controllare lo stato. La conservazione grafica serve a orientarsi durante l'editing, non a certificare la validità di valori obsoleti.

Le tabelle CA consentono copia e incolla e dispongono dei comandi di template e reimportazione previsti dall'interfaccia. Prima di un'importazione estesa salvare il foglio, controllare intestazioni e unità, quindi verificare numero di righe, barre e combinazioni importate. La semplice riuscita dell'importazione non dimostra che l'ordine delle colonne fosse quello voluto.

### Importare ed esportare le azioni

Aprire Sollecitazioni nella barra inferiore del foglio, oppure Gestisci sollecitazioni nelle schede di verifica. La finestra contiene un unico elenco per tutti gli stati limite. I nuovi fogli partono senza combinazioni precompilate.

Il comando Template Excel salva una cartella con il foglio Azioni. Le intestazioni sono, nell’ordine: Elemento, Nome combo, Stato limite, N [kN], Vx [kN], Vy [kN], T [kNm], Mx [kNm], My [kNm]. Elemento è facoltativo: vuoto significa il contesto del foglio corrente. Se indicato, conserva il riferimento dell’elemento nelle tabelle e nei report; non cambia automaticamente geometria o sezione. Tutte le righe dell’elenco sono verificate con la sezione del foglio. Combinazioni con lo stesso nome e riferimenti elemento diversi restano righe distinte.

Sono ammesse SLU, SLV, Rara, Frequente e Quasi permanente. Ogni riga deve contenere tutte le sei componenti, anche se alcune non sono utilizzate da una specifica verifica. Scrivere 0 per una componente nulla; una cella vuota non è uno zero. N è negativo a compressione. Le azioni devono essere già combinate e coefficientate: il programma non riapplica i coefficienti dei carichi.

SLU e SLV alimentano i domini 2D e 3D e le verifiche di taglio e torsione. Le tre combinazioni SLE alimentano i rispettivi controlli tensionali e di fessurazione previsti dalle impostazioni. La famiglia di input Taglio non esiste nel nuovo schema; la scheda Taglio e torsione resta disponibile per impostazioni ed esiti.

Importa e sostituisci sostituisce l’intero elenco della finestra; Importa e aggiungi conserva le righe già presenti. Aggiungi, Duplica ed Elimina permettono l’editing manuale. Le modifiche restano nella finestra fino ad Applica; Annulla conserva il foglio. Dopo Applica le verifiche vengono aggiornate. Esporta Excel salva tutte le righe del nuovo elenco.

I vecchi file Excel con Famiglia e i vecchi documenti restano leggibili. Nell’editor la colonna Origine precedente identifica Plastico, Elastico o Taglio: assegnare esplicitamente SLU o SLV alle rispettive righe e completare le componenti mancanti. Per la riga selezionata, Vuoti a zero è un’assegnazione esplicita da usare soltanto quando le componenti sono effettivamente nulle. Le righe storiche non vengono fuse dal nome e il criterio resistente rimane selezionabile nella verifica.

L’importazione controlla tutte le righe prima di modificare l’elenco. Sono ammessi fino a 10.000 combinazioni e file fino a 20 MB. Le formule Excel non vengono eseguite: vengono segnalati e letti i valori memorizzati nel file. Ricalcolare e salvare il file in Excel prima dell’importazione. Errori di cella e formule senza risultato memorizzato impediscono l’importazione.

Filtri e ordinamenti delle tabelle di verifica cambiano la vista, non eliminano azioni dal calcolo. Controllare numero di righe, stato limite, unità, segni e riferimenti elemento. Non importare un inviluppo di massimi indipendenti come se fosse un insieme simultaneo di sei componenti.

### Report short della sezione in calcestruzzo armato

Accanto a Report Word, il pulsante Report short esporta il foglio corrente in Word e PDF omonimi, entro due pagine. Attendere il completamento del calcolo automatico. ANTHEA impagina e produce il PDF da sé: Microsoft Word non serve. Il report completo rimane disponibile dal pulsante precedente.

La prima pagina raccoglie geometria, coordinate delle armature o definizione degli anelli regolari, staffe, materiali, coefficienti e opzioni di calcolo. La seconda contiene una combinazione governante per ciascuna verifica: SLU N–Mx–My, SLV N–Mx–My, taglio, tensioni rara, fessurazione frequente, tensioni quasi permanente e fessurazione quasi permanente. Azioni, resistenze o limiti, tasso ed esito sono racchiusi in due tabelle.

La scelta considera tutte le combinazioni inserite, comprese quelle nascoste nei grafici. Tensioni e fessurazione possono avere combinazioni governanti diverse. Per il taglio sono riportate entrambe le direzioni della stessa combinazione. Una famiglia senza azioni è indicata come non richiesta; risultati mancanti o privi di tasso numerico producono un esito incompleto. I criteri senza rapporto numerico, come la decompressione, richiedono il report completo.

Il report short non comprende torsione e interazioni, dettagli costruttivi, ancoraggi, domini 2D e momento–curvatura. Se dati geometrici molto estesi o nomi lunghi impediscono di rispettare due pagine, l'esportazione viene fermata senza tagliare dati: abbreviare i nomi o utilizzare il report completo. Il file di calcolo resta il riferimento riapribile.

### Leggere asse neutro, mappe e report

La linea dell'asse neutro rappresenta deformazione nulla dello stato selezionato. Nei domini il piano è quello del punto resistente; nelle SLE è quello dell'azione assegnata. Una mappa normalizzata alla resistenza del materiale non sostituisce il controllo dei limiti SLE. Con campo uniforme l'asse può non essere definito; un asse esterno non indica da solo un errore.

Nell'analisi elastica con viscosità il diagramma delle deformazioni mostra le deformazioni reali della sezione, comprese quelle differite: in una barra la deformazione vale σs/Es. Le tensioni sono le stesse del calcolo; la posizione dell'asse neutro non cambia.

Il report raccoglie input, materiali, coefficienti, azioni ed esiti scelti. Gli estremi di tensione/deformazione e i casi governanti possono provenire da combinazioni diverse. Non leggerli come un unico stato. Dati globalmente invalidi o un aggiornamento in corso impediscono l'esportazione dei risultati correnti.

Prima di scegliere una norma consulta [Profili di calcolo del calcestruzzo](wiki:profili-calcestruzzo): il selettore non attesta la copertura di tutte le verifiche della norma.

