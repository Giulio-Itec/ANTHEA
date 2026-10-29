# Guida pratica di ANTHEA

Manuale operativo dei moduli disponibili

Edizione 6 aggiornata il 29 ottobre 2026 — revisione documentale 62

Questa guida descrive uso, interfaccia e procedure di tutti i moduli di ANTHEA. Modelli, formule, ipotesi e limiti sono descritti nella guida teorica.

## Spettri NTC e isolamento sismico

Aprire Moduli singoli → Azioni → Spettri NTC. Il foglio determina la pericolosità del sito, gli spettri dei quattro stati limite e, quando attivata, la risposta di un sistema equivalente con isolamento alla base. Può essere salvato autonomamente o aggiunto a un progetto.

### Sito e spettri

Selezionare regione, provincia e comune, eventualmente filtrando il nome, oppure inserire longitudine e latitudine in gradi decimali. Il catalogo comunale appartiene all’edizione del riferimento: per il sito effettivo controllare le coordinate. La modifica manuale delle coordinate sostituisce l’etichetta del comune con Coordinate assegnate. Scegliere Superficie rigata oppure Distanza inversa per l’interpolazione spaziale. Un sito esterno al reticolo genera un errore e non produce spettri.

Assegnare vita nominale VN e coefficiente d’uso CU; quindi categoria di sottosuolo A–E, categoria topografica T1–T4 e quota relativa h/H, compresa tra 0 e 1. Per lo spettro orizzontale scegliere Elastico, con smorzamento ξ in percentuale, oppure Progetto con q, con q0 e regolarità in altezza. Il fattore q verticale è indipendente. Premere Calcola; le modifiche successive aggiornano automaticamente il calcolo. Un ingresso non valido invalida i risultati e mostra il motivo.

Il selettore dei risultati presenta quattro fasi:

| Fase | Contenuto |
| --- | --- |
| 1 · Pericolosità del sito | Reticolo locale, parametri ag, F0 e Tc* per nove tempi di ritorno, spettri parametrizzati e spettro tabulato per il TR selezionato |
| 2 · Periodo di riferimento | Parametri e spettri su sottosuolo A dei quattro stati limite, con VR e TR adottati |
| 3 · Spettro di progetto | Componenti orizzontale e verticale e spettro di riferimento per SLO, SLD, SLV o SLC |
| 4 · Isolamento sismico | Spettro con due smorzamenti e risultati del sistema equivalente, quando attivato |

I grafici riportano periodo T in secondi e accelerazione Sa in g. La tabella espone coefficienti e risultati; Ordinate degli spettri visualizzati mostra i punti delle curve. Esporta CSV e Copia ordinate includono tutte le fasi calcolate, con separatore punto e virgola e punto decimale. Il cambio di fase, stato limite, TR visualizzato o tema non modifica i dati del documento.

La parte Spettri conserva le convenzioni numeriche del riferimento NTC 2008: VR minimo 35 anni, intervallo dei TR del reticolo, campionamento fino a 4 s e particolarità della componente verticale. L’identificazione del riferimento è visibile nel foglio e nel report. Per un impiego progettuale diverso verificare l’applicabilità delle prescrizioni; la coincidenza numerica con il riferimento non equivale a un aggiornamento normativo.

### Isolatori e modello equivalente

Espandere Isolamento sismico e selezionare Attiva isolamento. Inserire la massa sismica in kN·s²/m, gli smorzamenti della struttura e dell’isolamento in percentuale e i fattori di rigidezza per SLE e SLU. Sono previsti due gruppi di dispositivi in parallelo: ciascuno richiede nome, numero intero e rigidezza del singolo isolatore in kN/mm. Un numero nullo esclude il gruppo. La somma delle rigidezze attive deve essere positiva.

La fase 4 mostra, per ogni stato limite, rigidezza totale K, periodo Tis, soglia 0,8 Tis, accelerazione a Tis, forza globale F e spostamento d. La forza per dispositivo è ripartita secondo la rigidezza del gruppo. SLO e SLD usano il fattore SLE; SLV e SLC quello SLU. Il ramo oltre la soglia adotta lo smorzamento dell’isolamento; quello precedente adotta lo smorzamento della struttura. Gli smorzamenti ammessi per questa estensione vanno dal 5% al 30%.

Importa Excel di isolamento acquisisce i valori salvati nei fogli Isolatori, Modellazione_isolatori_pendolo e Catalogo FIP dell’esempio predisposto. Non esegue formule, macro o collegamenti esterni. Se manca un valore memorizzato, ricalcolare e salvare il file con Excel prima di importarlo. L’importazione sostituisce i dati dell’isolamento, attiva i parametri sismici assegnati e conserva nome e impronta del file. Le coordinate e la vita di riferimento del documento corrente restano da controllare: l’esempio non fornisce una geolocalizzazione verificata.

Il catalogo acquisito serve a scegliere nome e rigidezza nei due gruppi. La corsa visualizzata è informativa; scegliere un dispositivo non verifica capacità, spostamenti, stabilità o carichi verticali. Il programma non aggiorna il catalogo attraverso servizi esterni.

Il valore Sa campione Excel consente il confronto con la ricerca tabellare dell’esempio. Il risultato principale usa invece la valutazione analitica dello spettro esattamente a Tis. Nell’esempio, allo SLC, il campione vale circa 0,176284373 g, mentre Sa(Tis) vale 0,170127557 g: la conseguente differenza di forza e spostamento è esplicita. Il calcolo richiede Tis non superiore a 4 s in ogni stato limite; oltre questo limite si interrompe, perché le NTC 2018 §3.2.3.2 richiedono una definizione specifica dell’azione sismica.

### Pendolo e parametri assegnati

In Modellazione del pendolo inserire carico verticale N in kN, coefficiente d’attrito μ, spostamento d e raggio equivalente R in mm. Calcola pendolo restituisce forza d’attrito, rigidezza equivalente, smorzamento e periodo. R è un dato del dispositivo, da assegnare; non deriva automaticamente dalla corsa. Usa Ke e smorzamento nel gruppo 1 trasferisce esplicitamente i due risultati e invalida il calcolo precedente. Numero di dispositivi e massa restano quelli del foglio: verificarne la coerenza con N.

Parametri sismici assegnati permette di indicare ag, F0 e Tc* per ciascuno dei quattro stati limite. Quando attivi, questi valori sostituiscono quelli interpolati nelle fasi 2–4; la fase 1 continua a descrivere il reticolo delle coordinate inserite. L’origine dei parametri è indicata accanto ai risultati. Disattivare l’opzione per tornare al calcolo dal sito.

### Salvataggio e report

Salva conserva input, catalogo importato e provenienza. Le revisioni di progetto in sola lettura impediscono modifiche e importazioni. Report Word produce DOCX e PDF con sito, nodi, pesi, parametri, ordinate dei quattro stati limite e risultati dell’isolamento e del pendolo, se attivi. Il capitolo è disponibile anche nel report di progetto.

Il foglio non sostituisce il modello strutturale e le verifiche dei dispositivi. Restano da controllare, secondo il progetto, condizioni di applicabilità dell’analisi, distribuzione delle masse e rigidezze, torsione ed eccentricità, risposta verticale, spostamenti di progetto e requisiti dei singoli isolatori.

## Stratigrafia del terreno

Il modulo autonomo Stratigrafia del terreno si apre dal catalogo Geotecnica e può essere aggiunto come scheda a un progetto. Gestisce un profilo di strati orizzontali; non esegue verifiche geotecniche. Il comando Invia / importa terreno da ANTHEA permette di riutilizzare i dati negli altri fogli compatibili.

Assegnare nome, descrizione e, se disponibile, quota altimetrica del piano campagna. Le profondità sono espresse in metri e crescono verso il basso a partire dal piano campagna. La quota altimetrica è un dato distinto e facoltativo.

Inserire gli strati dall’alto verso il basso con + Strato. Duplica crea uno strato indipendente; Elimina conserva almeno uno strato; le frecce cambiano l’ordine. Selezionare uno strato nell’elenco o nel profilo per modificarne nome, descrizione litologica, colore e spessore. Solo l’ultimo strato può essere esteso indefinitamente: per duplicarlo o spostarlo occorre prima renderlo finito e assegnargli lo spessore corretto. Un nuovo strato viene inserito prima dello strato indefinito.

Per ciascuno strato sono disponibili γ e γsat in kN/m³, φ′ in gradi, c′ e cu in kPa. Lasciare vuoti i parametri non disponibili: zero è un valore distinto. Registrare provenienza, qualificazione e note; se i singoli parametri hanno qualificazioni diverse, specificarlo nelle note. Il programma non attribuisce valori in base alla litologia e non applica coefficienti parziali.

Per la falda scegliere Non nota, Non considerata oppure Assegnata. Nel terzo caso inserire la profondità, nulla al piano campagna o positiva al di sotto. La rappresentazione ammette un solo livello; non descrive falde sospese o condizioni artesiane. Un valore eventualmente conservato nel campo profondità è inattivo quando la falda non è assegnata.

Salva conserva il documento autonomo o il progetto. I parametri incompleti generano avvisi e non impediscono il salvataggio; geometria invalida, numeri non validi e falda assegnata senza profondità devono essere corretti. Il grafico evidenzia lo strato selezionato e la falda; la profondità visualizzata di uno strato indefinito non ne rappresenta il limite fisico. Report Word produce la scheda dei dati in DOCX e PDF affiancati, anche senza calcoli.

### Famiglie di terreno e retini

Il menu Famiglia prevalente propone Non classificato, Riporto, Ghiaia, Sabbia, Limo, Argilla, Terreno organico, Roccia e Altro. Per terreni misti scegliere la componente prevalente e conservare la denominazione completa: ad esempio Sabbia per una sabbia limosa. Il colore resta modificabile separatamente.

Il profilo usa punti per Sabbia, ciottoli per Ghiaia, tratti orizzontali brevi per Limo e continui per Argilla, crocette per Riporto, simboli vegetali per Terreno organico, tratti diagonali per Roccia e piccoli segni a più per Altro. Non classificato conserva il solo colore. I retini distinguono le famiglie anche senza colore e mantengono dimensione costante sullo schermo. Il nome della famiglia appare accanto allo strato e nella scheda dati. Sono convenzioni grafiche di ANTHEA: scegliere una famiglia non assegna parametri, falda o spessore.

L’importatore AI propone la famiglia solo quando la componente prevalente è riconoscibile nella descrizione della relazione, lasciando Non classificato nei casi incerti. Il menu permette di correggere la proposta in anteprima; non è una classificazione granulometrica automatica.

### Valori medi e minimi

I valori medi costituiscono l’insieme principale. Attivare Gestisci anche i valori minimi nello strato per inserire i minimi eventualmente forniti dalla relazione geologica; questi campi sono opzionali e non vengono riempiti copiando i medi. Le due colonne hanno fonte e qualificazione distinte. La rimozione dei minimi richiede conferma.

La scelta Parametri da usare nelle verifiche ammette Medi, Minimi oppure Entrambi. La scelta è salvata insieme alla geometria comune e riportata nella scheda dati. Non avvia calcoli: nei moduli con un solo insieme di parametri il trasferimento richiede la scelta esplicita Medi o Minimi; per entrambi creare due fogli distinti. I medi pertinenti saranno comunque richiesti dal modulo di verifica; scegliendo Minimi o Entrambi saranno richiesti anche i corrispondenti minimi. Non occorre compilare cu per una verifica che richiede soltanto parametri efficaci. Le bozze incomplete possono essere salvate, con avviso. Un minimo superiore al medio richiede controllo della fonte e del significato del dato.

### Scambio del terreno fra fogli ANTHEA

Creare un foglio Stratigrafia distinto per ciascun sondaggio o profilo e assegnargli un nome riconoscibile. Aprire Invia / importa terreno da ANTHEA, scegliere il foglio sorgente nella sezione Da un altro foglio del progetto e, se presente, il sondaggio oppure Monte/Valle. Importa dal foglio selezionato carica soltanto l’anteprima: Sostituisci il terreno del foglio corrente richiede conferma prima di applicare i dati. In alternativa scegliere un modulo e Invia a un nuovo foglio. Nel progetto il nuovo foglio viene aggiunto alla sezione corrente; da un file singolo si apre un nuovo file, non ancora salvato, in una scheda.

Lo scambio è disponibile per Stratigrafia, palo verticale, palo orizzontale, micropalo orizzontale, muri e fondazioni. Il micropalo verticale con schema specifico non è incluso. La copia è indipendente: modificare la sorgente non aggiorna le destinazioni. Salva terreno e Carica terreno consentono anche il riutilizzo mediante file terreno ANTHEA.

Fra due fogli Stratigrafia si conservano geometria, falda, medi e minimi, fonti, descrizioni, colori e retini, anche con dati incompleti o ultimo strato indefinito. Per inviare a un modulo di verifica scegliere Medi oppure Minimi. La scelta non copia i medi nei minimi mancanti. Specificare nello strato il comportamento Granulare o Coesivo: non viene dedotto dalla famiglia grafica. Definire la falda e spessori finiti; se l’ultimo strato è indefinito, predisporre una copia con l’estensione motivata per la verifica. Non vengono prolungati automaticamente i sondaggi.

I dati mancanti restano vuoti e i moduli mantengono i propri controlli di compatibilità. Completare i parametri specifici, geometria e azioni nel foglio di destinazione. Le profondità mantengono lo stesso riferimento: nessuna traslazione automatica della quota. Nel trasferimento dai vecchi moduli a Stratigrafia verificare la qualificazione dell’insieme unico riportato nei campi principali; minimi e litologia non vengono ricostruiti.

### Importazione assistita della relazione

Il pulsante Importa relazione PDF con AI è attualmente visibile ma disattivato, in attesa della configurazione e della gestione della fatturazione aziendale. Lo scambio interno fra fogli ANTHEA resta disponibile senza servizi AI. La procedura seguente descrive la funzionalità predisposta per una successiva attivazione.

Aprire Importa relazione PDF con AI, selezionare un PDF fino a 20 MiB e inserire il nome di un modello OpenAI compatibile con PDF e output strutturato e la propria chiave API. I campi possono essere predisposti tramite le variabili di ambiente ANTHEA_AI_MODEL e OPENAI_API_KEY. La chiave non viene salvata nei documenti ANTHEA.

Il comando Invia PDF a OpenAI e prepara bozze trasmette l’intero documento al servizio remoto e può comportare costi sul proprio account API. Non effettua elaborazione AI locale. Interrompi annulla l’attesa; nessun dato viene applicato automaticamente alla scheda aperta. Errori del servizio o risposte incomplete richiedono una nuova richiesta, eventualmente con un estratto più breve della relazione.

Selezionare il sondaggio o profilo dall’elenco, controllare gli avvisi e correggere la bozza nell’editor. Profili differenti restano separati e se ne applica uno alla volta. Le fonti riportano per parametro le pagine PDF indicate dal modello: confrontarle con il documento originale. I valori non riconosciuti come medi o minimi restano nelle note da classificare. Dati assenti, geometrie ambigue e unità non determinabili non devono essere completati per supposizione; l’estrazione AI può contenere errori.

Dopo il confronto con la relazione, selezionare Ho confrontato dati, unità e pagine della bozza con la relazione e applicare il profilo revisionato. Ogni modifica o cambio di profilo richiede di ripetere questa attestazione. Il comando sostituisce la stratigrafia della scheda aperta; chiudere la finestra conserva i dati precedenti. Geometrie e valori invalidi impediscono l’applicazione; i parametri mancanti restano una bozza salvabile. Il documento conserva nome e impronta del PDF, modello, data, fonti e avvisi; non incorpora il PDF originale. La scheda Word/PDF espone medi, minimi e provenienza dell’importazione.

## Avvio e scelta del modulo

### Avviare la versione aggiornata

Per installare ANTHEA eseguire il setup e scegliere l'installazione per l'utente corrente oppure per tutti gli utenti. Il runtime .NET 8 è incluso. Se ANTHEA è aperta, anche da un'altra cartella, il setup si blocca prima di rimuovere la versione precedente: chiudere tutte le finestre e riavviare il setup.

Prima di copiare la nuova versione il setup elimina tutto il contenuto della cartella di installazione, incluse vecchie DLL, sottocartelle ed eventuali file aggiunti dall'utente. Salvare quindi progetti, modelli e report in una cartella esterna. Usare una destinazione dedicata di nome ANTHEA oppure la cartella riconosciuta di una precedente installazione. Percorsi di sistema, collegamenti a cartelle e file che impediscono la pulizia arrestano l'installazione prima della copia dei nuovi file.

Nel repository, Avvia ANTHEA.cmd ricompila il progetto in Release, pubblica nella cartella app e avvia l'eseguibile soltanto se i passaggi riescono. Richiede quindi l'SDK .NET e ANTHEA chiuso durante l'aggiornamento. La distribuzione già pubblicata può essere avviata direttamente da app/ANTHEA.exe. La distribuzione dipendente dal framework richiede il runtime desktop .NET 8. Il sorgente e la distribuzione sono due cose distinte: modificare un file C# non aggiorna un eseguibile già pubblicato.

La barra a sinistra, sempre visibile, porta a Home, Progetti, Modelli, Moduli singoli, Esempi e Wiki e contiene Apri (l'icona della cartella), Salva e Salva con nome; in alto restano i menu File, Strumenti e Aspetto. Ogni foglio aperto ha una scheda: si possono tenere aperti insieme più file di calcolo singoli. Un foglio nuovo, dal menu File o da Moduli singoli, apre una scheda; un file già aperto si attiva; il punto «•» segnala un foglio non salvato. Nella Home, Riprendi · nome torna all'ultimo foglio. Dal menu della linguetta, Sposta in una nuova finestra porta la scheda in una finestra separata e Riaggancia la riporta; Pagina intera, nella barra del foglio, le fa occupare tutta la finestra, ed Esc o Esci da pagina intera ripristinano navigazione e albero. La Wiki si apre in una scheda; F1 e i pulsanti «?» la aprono sull'argomento del foglio. Le schede restano aperte finché l'applicazione è aperta, ma non salvano su disco: prima di chiudere usare Salva o Salva con nome. Alla chiusura ANTHEA chiede che cosa fare dei fogli non salvati.

### Scegliere l’aspetto

Il menu Aspetto, accanto a File, offre Chiaro, Scuro e Molto scuro. Chiaro mantiene i colori originali; Scuro usa superfici blu; Molto scuro usa grigio antracite e nero. La scelta si applica a Home, Progetti, moduli e Wiki senza chiudere il foglio o modificare i dati del calcolo. Nella Wiki la tendina dell’aspetto controlla la stessa preferenza globale.

La scelta è salvata nel profilo Windows e ripristinata al prossimo avvio. Non fa parte dei documenti o delle revisioni di progetto. Le immagini incorporate e le tavole dei grafici conservano i propri colori per mantenere leggibili legende e rappresentazioni tecniche.

Nello stesso menu, Separatore decimale offre Sistema, Virgola e Punto. Sistema, predefinito, segue l'impostazione di Windows; Virgola e Punto la sostituiscono, con il separatore delle migliaia opposto. La scelta vale per i numeri di campi, tabelle, stati e dettagli di tutti i moduli; nella sezione c.a. anche il dettaglio dei domini, la legenda della mappa SLE e i parametri del taglio la seguono. È salvata per l'utente del PC e vale dal prossimo avvio di ANTHEA, come ricorda il messaggio dopo il cambio: i testi e i disegni già aperti non cambiano. Dati salvati, calcoli e risultati non dipendono dal separatore; i report e i disegni che scrivono sempre la virgola restano come sono.

### Scegliere il foglio adatto

| Foglio disponibile | Domanda alla quale risponde | Informazione da preparare |
| --- | --- | --- |
| Palo verticale | Quale resistenza assiale geotecnica si ottiene con questi terreni e questa lunghezza | Stratigrafie, falda, tecnologia, azioni e coefficienti |
| Micropalo verticale | Quale resistenza del bulbo iniettato è compatibile con l'abaco scelto | Terreni, p_l, iniezione, diametri, inclinazione e tratto attivo |
| Palo orizzontale | Quale carico limite laterale risulta dal meccanismo di Broms | Terreno, diametro, lunghezza, vincolo e momento resistente |
| Micropalo orizzontale | Quale capacità laterale risulta usando una sezione tubolare CHS | Diametro geotecnico, tubo, materiale, N e terreno |
| Palificata orizzontale | Come cambiano i fattori di riduzione con geometria e direzione del carico | Posizioni, diametro, teoria e interassi rappresentativi |
| Muri di sostegno | Quali verifiche locali e globali sono applicabili al muro | Geometria, materiali, terreni, acqua, azioni e combinazioni |
| Sezione in c a | Come risponde e si verifica una sezione assegnata | Geometria, barre, materiali e combinazioni N M V T |
| Sezione composta | Come si ripartiscono le tensioni in una sezione da ponte attraverso le fasi | Tipo e geometria, soletta, barre, fasi e metodo |
| Bridge Design | Quale configurazione preliminare, quantità, costo e CO₂ sono plausibili | Sito, campate, larghezza, famiglia, fondazioni e listino |
| Calcestruzzo e durabilità | Quali requisiti del materiale e del copriferro derivano dalle scelte assegnate | Esposizioni, vita, materiale, diametri e condizioni esecutive |
| Acciaio per armature | Quali proprietà e diagramma usare per l'armatura | Classe o dati personalizzati e coefficienti |

Le altre voci eventualmente presenti nel catalogo possono essere predisposizioni. Una scheda di materiali non verifica una sezione. La sezione composta non analizza un intero ponte; Bridge Design non trasferisce automaticamente un modello verificato alla sezione composta. Per passare dall'uno all'altro occorre scegliere la sezione locale, assegnare le azioni da un'analisi appropriata e controllare nuovamente le unità.

### Esempi organizzati per modulo

Aprire **Esempi** dalla barra laterale oppure **File → Apri esempio…**. La colonna sinistra raggruppa i file per modulo; quella destra mostra i casi disponibili. Selezionare un caso e leggere la descrizione prima di premere **Apri una copia**. Il catalogo comprende almeno due casi per ognuno dei quindici moduli, tre casi Spettri con isolamento e pendolo, nove esempi di paratie e dodici modelli geometrici da wizard per le sezioni in calcestruzzo armato.

Il documento aperto è una copia indipendente senza percorso di salvataggio. **Salva** richiede una destinazione nella cartella dei propri lavori. Anche il doppio clic su un esempio installato apre una copia; il programma impedisce di salvare dentro la cartella degli esempi distribuiti. Prima di sostituire un documento modificato viene chiesta la conferma consueta.

Gli esempi di calcolo illustrano gli input e i risultati del relativo modulo: un controllo non soddisfatto resta visibile e non viene trasformato in un esito positivo. Le stratigrafie sono esempi di dati. I casi **Wizard** sono modelli geometrici con barre e staffe schematiche: completare le azioni, i parametri manuali del taglio e le verifiche dei dettagli secondo le indicazioni del capitolo sulle sezioni. Non sono progetti esecutivi.

Nell’installer il componente **Esempi per tutti i moduli** è selezionato inizialmente e può essere escluso. I file sono raccolti nella cartella **Esempi**, suddivisi per modulo, accanto al programma; il menu Start contiene il collegamento alla cartella. Se il componente è assente, la pagina indica di rieseguire l’installer selezionandolo. Gli esempi e le due guide sono utilizzabili senza connessione.

### Leggere e compilare i campi

Controllare sempre l'unità a fianco del valore. Nei moduli geotecnici le lunghezze sono generalmente in metri; nelle sezioni strutturali e nelle barre sono in millimetri. Una sezione alta 600 mm non va inserita come 0,60. MPa e N/mm² sono numericamente equivalenti; kPa e kN/m² lo sono altrettanto, e 1 MPa = 1000 kPa: per esprimere in kPa un valore dato in MPa occorre moltiplicarlo per 1000.

I campi vuoti servono anche a rappresentare una bozza incompleta. Non sostituire sistematicamente un dato sconosciuto con zero: in alcuni campi zero ha un significato fisico, in altri attiva un automatismo, in altri è un errore. Bridge Design indica esplicitamente «0 = auto» per i parametri che lo consentono. Quando un campo è temporaneamente incompleto, leggere il messaggio della scheda prima di considerare i risultati.

Tab e Maiusc+Tab spostano il cursore sulla casella successiva o precedente e ne selezionano il contenuto: il valore digitato sostituisce quello presente. Con un clic il cursore resta nel punto indicato, per correggere solo una parte del valore. Le caselle di testo su più righe non selezionano il contenuto; nelle tabelle la modifica da tastiera segue il comportamento proprio della tabella.

La finestra segue l'ingrandimento di Windows. Sotto 1120 × 680 la finestra scorre; i fogli che non si adattano alla larghezza tengono 1120 × 600 e scorrono nella loro scheda. Una pagina più alta della scheda scorre al suo interno: a 1920 × 1080 succede, per esempio, alla pagina principale del palo orizzontale. I pannelli più stretti si dispongono verticalmente; le tabelle possono mantenere uno scorrimento proprio. Nei progetti l'albero è una colonna della finestra, che «▸» chiude e riapre. Cercare prima la barra del pannello che contiene il dato: lo scorrimento della finestra e quello della tabella non sono necessariamente lo stesso comando.

## Progetti e gestione del lavoro

### Costruire una struttura leggibile

Nella pagina Progetti l'albero del progetto è la colonna accanto alla barra di navigazione; al centro c'è il riepilogo della sezione selezionata, con Schede da aggiungere. Creare il progetto, assegnargli un nome utile e aggiungere sezioni. Una possibile struttura per un ponte comprende Materiali, Impalcato, Pila 1 e Fondazioni. Ogni sezione può contenere sia fogli sia sottosezioni; anche il progetto può contenere fogli direttamente.

Trascinare una scheda da Schede da aggiungere nel contenitore desiderato, oppure fare doppio clic per aggiungerla alla sezione selezionata. Durante il trascinamento un indicatore e un suggerimento («Inserisci in», «Prima di», «Dopo», «Sposta dentro») mostrano dove andrà l'elemento. Il pulsante con l'icona del modulo, oppure Invio, apre il foglio nella sua scheda. Un clic sul nome di una sezione ne apre il riepilogo; doppio clic o F2 consentono di rinominare; Invio, Esc o un clic fuori dal nome chiudono la rinomina. Il menu di un nodo offre Apri, Rinomina, Duplica, Sottosezione, Report Word della sezione ed Elimina; per i fogli anche Apri in una nuova finestra e Apri a pagina intera. Il simbolo di coerenza accanto a una sezione apre il confronto dei dati comuni. Nei fogli «▸» mostra o nasconde l'albero. Torna al progetto riapre il riepilogo.

Usare nomi che distinguano oggetto e condizione: «Pila 2 sezione base», «Palo 1000 sondaggio S3» o «Impalcato campata positiva». Nomi generici ripetuti rendono difficile capire quale dato sia stato modificato e quale foglio stia governando la condivisione.

### Comprendere i dati comuni

La condivisione è gerarchica e avviene per proprietà compatibili. Per una proprietà prevale il livello più alto che la definisce e che può trasferirla al destinatario. La regola attraversa anche sezioni intermedie vuote. I rami paralleli condividono i dati dei loro antenati comuni, non tutti i dati l'uno dell'altro.

Per esempio, un materiale CLS definito nel progetto può governare i fogli compatibili delle pile e dell'impalcato. Se si vuole deliberatamente un materiale diverso per un ramo, occorre verificare come è organizzato il riferimento superiore: scrivere un valore diverso in basso può produrre un conflitto, senza modificare il riferimento. Il confronto mostra percorso, valore e provenienza dei dati. «Uniforma a questo» agisce nel ramo governato dal riferimento e non promuove dal basso una proprietà già governata da un antenato.

Quando si confermano o si salvano modifiche a un riferimento, le proprietà compatibili possono propagarsi ai discendenti. In assenza di un riferimento superiore, resta la scelta fra aggiornare i fogli dello stesso livello e mantenere il dato locale. I fogli nuovi ereditano ciò che è definito senza ambiguità. Se due riferimenti dello stesso livello sono discordanti, ANTHEA non sceglie silenziosamente uno dei due.

Prima di produrre un report di progetto aprire il riepilogo Controlli e Dati comuni. Un avviso sul copriferro o un conflitto non è necessariamente un errore numerico, ma richiede una decisione esplicita. La condivisione non deve essere confusa con una sincronizzazione universale di ogni campo: gli adattatori dei moduli preservano unità, forme ammesse e significato delle proprietà.

### Spostare duplicare e annullare

Il bordo di un'intestazione indica il riordino prima o dopo un elemento dello stesso gruppo. Il centro di una sezione consente di trasferirvi un'altra sezione con il suo contenuto. Non si possono creare cicli. Un trasferimento fra rami può cambiare i riferimenti comuni: l'anteprima mostra gli effetti su una copia del documento prima di applicarli. Annullare l'anteprima lascia invariato il progetto.

Duplica sezione crea nuovi identificativi per tutti i fogli e le sottosezioni. La copia non acquisisce la cronologia delle revisioni dell'originale. I dati locali sono indipendenti, ma continuano a essere soggetti alle normali regole di condivisione del ramo in cui si trovano.

Annulla e Ripristina del progetto conservano fino a 30 stati della sessione. Ctrl+Z e Ctrl+Y operano sul progetto quando il cursore non è in un campo di testo; nei campi prevale l'annullamento del testo. La cronologia non viene salvata nel file. Bridge Design possiede inoltre un proprio Annulla per le modifiche alla scheda; non coincide con una revisione del progetto. Dopo Annulla o Ripristina le schede aperte restano aperte e mostrano i dati ripristinati; una scheda il cui foglio non esiste più si chiude con un avviso. Quando un aggiornamento dei dati comuni ricarica una scheda aperta, la scheda lo dice in una riga sotto la barra («Aggiornato da «foglio»: …»).

### Usare le revisioni

Nuova revisione archivia la versione attuale e apre la successiva. Alla prima operazione si conserva Rev. 0 e si passa a Rev. 1. Ogni sezione può avere una numerazione indipendente. Nei fogli viene usata la sezione revisionata più vicina. La nota serve a ricordare il motivo della revisione, per esempio «Aggiornamento falda da indagini» o «Riduzione larghezza impalcato».

Le revisioni archiviate si scelgono nella barra delle revisioni, sotto quella del progetto («Rev. n»), oppure con Mostra questa revisione nel riepilogo; si consultano con gli input protetti e i comandi di visualizzazione ed esportazione disponibili. Mentre si consulta una revisione archiviata le schede del progetto si aprono in sola lettura; le schede dei fogli che nella revisione non esistono si chiudono e si riaprono, nel loro ordine, tornando alla versione corrente. Tornando alla revisione attuale si ritrovano le modifiche non ancora salvate. Il contesto storico comprende gli antenati necessari; non è un collegamento mutabile ai materiali correnti. Salva, anche mentre si consulta lo storico, conserva l'intero documento corrente con le revisioni.

Eliminare una revisione archiviata rimuove quella versione senza rinumerare le altre. Eliminare l'attuale ripristina l'ultima rimasta e la rende modificabile; riguarda il ramo scelto. I materiali degli antenati del progetto corrente possono differire da quelli del ramo ripristinato: controllare quindi il confronto. L'unica versione rimasta non è eliminabile con il comando delle revisioni.

### Report del ramo e compatibilità degli archivi

Genera report esporta il progetto o la sezione selezionata con i fogli diretti e le sottosezioni nell'ordine dell'albero. Include i riferimenti necessari degli antenati, senza aggiungere i rami esterni. I dati comuni compatibili vengono riuniti; valori discordanti mantengono provenienza e segnalazioni. Le schede incomplete restano riconoscibili nel documento. Il report si chiede con Genera report nel riepilogo oppure con Report Word della sezione nel menu del nodo. Se ci sono differenze o avvisi, un controllo prima del report propone Apri confronto, Genera con segnalazioni e Annulla. Una finestra mostra l'avanzamento; Annulla interrompe al termine del calcolo in corso e non sovrascrive un file esistente.

Il report di progetto ricalcola su una copia dei dati. Il report del singolo modulo segue invece le condizioni del suo editor: nella Sezione in c.a. occorre attendere il completamento del calcolo corrente. Prima dell'esportazione controlla il riepilogo e conserva il file riapribile oltre al Word.

Gli archivi con revisioni usano il formato 2; quelli precedenti senza revisioni restano leggibili. I risultati dei report storici sono ricalcolati sui dati della revisione selezionata: conservare anche il report originale è necessario per documentare un risultato ottenuto con un'altra versione del motore.

### Controllo della condivisione

Prova la gerarchia su un progetto semplice: materiale al livello progetto, due sottosezioni e un foglio compatibile in ciascuna. Cambia il riferimento superiore e controlla la propagazione; modifica poi un discendente e verifica che il conflitto resti visibile. Un ramo parallelo non deve diventare riferimento dell'altro.

Consulta [convenzioni e confini del calcolo](wiki:architettura-del-calcolo-e-convenzioni) prima di trasferire risultati fra moduli. La condivisione di un materiale non trasferisce automaticamente combinazioni o verifiche.

## Materiali e durabilità

### Preparare il calcestruzzo

Selezionare una o più classi di esposizione realmente pertinenti alla superficie e all'ambiente. X0, XC, XD, XS, XF e XA descrivono fenomeni differenti; non sono livelli successivi di un'unica scala. In presenza di esposizioni combinate il modulo ricerca i requisiti più gravosi fra quelli applicabili. Non scegliere semplicemente la classe con il nome alfabeticamente maggiore.

Controllare la classe resistente proposta, il rapporto acqua cemento massimo, il contenuto minimo di cemento e le eventuali indicazioni sull'aria inglobata. I prospetti adottati sono identificati nella guida teorica e nei riferimenti del modulo. La scheda aiuta a specificare i requisiti; non determina da sola una ricetta di produzione del calcestruzzo, acqua totale, additivi o granulometria ottimizzata.

Per il copriferro assegnare norma selezionata, vita nominale, tipo di elemento, diametro delle barre, dimensione dell'aggregato, tolleranza esecutiva e condizioni aggiuntive. Distinguere il minimo dal nominale. Quest'ultimo comprende il margine esecutivo previsto dalla scelta del foglio. Un diametro maggiore o un requisito di getto controterra può governare anche quando la durabilità richiederebbe meno.

Il copriferro del disegno della sezione e quello richiesto dalla scheda materiali devono riferirsi alla stessa superficie: esterno staffa, superficie della barra longitudinale e asse barra non coincidono. Nel progetto il confronto evidenzia le incoerenze, ma la lettura del dettaglio resta necessaria quando vi sono staffe, più strati o fasci.

### Preparare l'acciaio per armature

Scegliere B450C, B450A, una voce storica o un materiale personalizzato. Verificare E, fy, fu, deformazioni e coefficiente parziale. Il grafico mostra il diagramma associato ai dati: controllare in particolare la presenza o meno di incrudimento e il tratto ultimo. Un materiale personalizzato va nominato in modo da poter risalire alla sua provenienza.

Le classi storiche FeB non sostituiscono la caratterizzazione di un acciaio esistente. L'allungamento a rottura su base A5 non è automaticamente la deformazione ultima utilizzabile nel diagramma costitutivo. Se un dato richiesto non è noto, il foglio deve restare incompleto per quella funzione, anziché completarlo con un valore scelto per ottenere un esito favorevole.

Il trasferimento dei materiali ai fogli compatibili riguarda le proprietà comuni. Il tubo del micropalo orizzontale è un materiale strutturale specifico della sezione CHS. Inoltre un motore può adottare una legge semplificata propria: il calcolo automatico di My del palo orizzontale usa acciaio elastico perfettamente plastico anche se il catalogo dell'armatura contiene informazioni più estese.

## Palo verticale

### Compilazione ordinata

Impostare tecnologia del palo, diametro, lunghezza, peso del materiale, azioni assiali e coefficienti. Il Peso specifico CLS / palo può valere zero: il peso proprio non entra allora nelle azioni e il risultato lo segnala con un avviso; un valore negativo è rifiutato. Compilare poi le stratigrafie. Ogni nuovo sondaggio parte con una riga da completare, non con un terreno automaticamente valido. Inserire nome, spessore, famiglia del terreno, addensamento dove richiesto, pesi di volume e parametri di resistenza.

Profondità della testa sotto il piano campagna d, in metri, nei dati generali dopo la lunghezza, descrive un palo con la testa interrata, per esempio sotto un plinto. Il campo mostra 0, cioè testa al piano campagna, e un foglio esistente dà gli stessi risultati di prima. Con d > 0 la stratigrafia resta riferita al piano campagna e deve arrivare almeno a d + L: il terreno sopra la testa dà le tensioni verticali, anche con la falda, ma nessuna resistenza laterale. Quote, curve e diagrammi partono dalla testa. Il risultato riporta d con un avviso, il report aggiunge una nota e il profilo disegna la testa alla sua profondità. Un valore negativo è rifiutato. Lo stesso campo è nel micropalo verticale.

Per un calcolo drenato occorrono i parametri efficaci. Per il ramo non drenato degli strati coesivi occorre Cu; il programma considera anche la posizione della falda per decidere il tratto nel quale applicare quel ramo. Non usare c′ e Cu come sinonimi. Negli strati granulari il contributo c′ alla resistenza laterale drenata è nullo, anche se il campo contiene un numero.

Indicare la presenza e la profondità della falda. L'effetto sulle tensioni del terreno e l'opzione di sottospinta sul peso proprio sono aspetti distinti. Spuntare la sottospinta solo secondo l'ipotesi di peso immerso che si vuole applicare al palo; non aspettarsi che questo comando definisca da solo la stratigrafia satura. L'opzione riguarda soltanto la compressione drenata e quella non drenata senza base lorda: a trazione il peso sotto falda è sempre alleggerito, nella compressione non drenata con base lorda è sempre intero. Riepilogo, grafici e report confrontano ogni condizione con la propria azione.

«Copia in» e «Copia da» permettono di riutilizzare gli strati fra sondaggi creando copie indipendenti. Controllare la destinazione prima di sovrascriverla. Il pulsante meno della riga elimina uno strato; quello della linguetta elimina la stratigrafia. Il colore identifica il terreno nel profilo e non costituisce un parametro di calcolo.

Estendi, sulla scheda Stratigrafia, allarga la tabella degli strati e lascia a destra, per tutta l'altezza, la scheda Profilo stratigrafico con il palo: il disegno resta visibile mentre si modificano gli strati. Riduci ripristina la disposizione normale. Se la finestra è troppo stretta per affiancare le due schede, la stratigrafia estesa occupa da sola tutta la larghezza. La stessa disposizione vale per il micropalo verticale, per i pali e micropali orizzontali e per Modifica dati e strati della risposta elastica, che apre la stratigrafia estesa.

### Coefficienti e gruppo di pali

Verificare K e μ proposti per la tecnologia e l'addensamento. Controllare l'abaco Nq e la posizione del punto: un parametro sul bordo dell'abaco non dimostra che il terreno appartenga al campo sperimentale. Il numero di indagini scelto per ξ non è semplicemente il numero di linguette visibili. Deve rappresentare la base conoscitiva che si intende utilizzare.

Selezionare nessuna riduzione, Converse Labarre, Feld oppure efficienze definite dall'utente. Per i metodi geometrici servono numeri di pali e interassi coerenti. Il risultato è una riduzione della capacità per palo nel modello: non è una verifica completa del blocco di terreno, della distribuzione dei carichi nel plinto o dei cedimenti di gruppo.

### Leggere i risultati

Le curve drenate sono verdi e quelle non drenate viola; compressione e trazione si distinguono anche per tono e tratteggio. Le azioni sono rappresentate separatamente. Alla lunghezza di progetto leggere contributo laterale, punta, resistenza in compressione, resistenza in trazione e relativi coefficienti. La trazione non utilizza automaticamente gli stessi fattori del ramo di compressione.

La punta e il laterale minimo possono provenire da sondaggi diversi. La curva costruita con i minimi delle componenti non rappresenta necessariamente un unico sondaggio reale. Confrontare anche le curve delle singole stratigrafie prima di interpretare il motivo della riduzione.

Se una stratigrafia non raggiunge la quota richiesta, il calcolo non inventa gli strati mancanti. Integrare l'indagine o rivedere la lunghezza. La casella che disattiva il laterale elimina il contributo resistente di quel tratto, ma ne conserva il peso e l'effetto sulle tensioni negli strati sottostanti. È utile per escludere un tratto non affidabile, non per modellare automaticamente l'attrito negativo.

### Esempio di controllo manuale

Per una sola tratta con D = 1 m, lunghezza attiva 10 m e resistenza laterale uniforme τ = 50 kPa, il contributo laterale è π × 1 × 10 × 50 = 1570,8 kN. Questo è un controllo della geometria e delle unità, prima dei coefficienti ξ, γ ed η. Se il foglio restituisce un ordine di grandezza mille volte diverso, verificare MPa contro kPa e metri contro millimetri.

Procedere poi con i parametri realmente variabili del terreno. Non inserire τ = 50 nel campo Cu o c′ aspettandosi lo stesso risultato: quei parametri entrano in formule differenti. L'esempio serve a leggere il contributo restituito, non a sostituire il modello geotecnico.

## Micropalo verticale

### Dati del bulbo e dell'iniezione

Assegnare diametro di perforazione, iniezione IGU o IRS, famiglia di terreno, coefficiente di espansione α e p_l utilizzato dall'abaco. L'interfaccia espone il campo «Pressione p_i = p_l» e il codice assume quell'uguaglianza. p_l è però la grandezza geotecnica dell'abaco: usare il valore della pompa richiede una giustificazione della correlazione, non la sola coincidenza delle unità. Documentare quindi quale misura o interpretazione abbia prodotto il dato inserito.

Definire l'inizio del bulbo resistente. La lunghezza libera o il tratto escluso non produce resistenza laterale del bulbo. La resistenza viene integrata soltanto nelle tratte attive, considerando il terreno attraversato. L'eventuale contributo di punta è una percentuale del laterale: attivarlo non introduce una verifica autonoma della base.

Inserire l'inclinazione rispetto alla verticale e verificare la lunghezza lungo l'asse. I passaggi di strato sono definiti in profondità verticale e vengono convertiti nella lunghezza percorsa dal micropalo. Con la testa interrata, Profondità della testa sotto il piano campagna d è verticale: gli strati sopra la testa non hanno aderenza, s e le curve partono dalla testa e la punta è a d + L cos θ dal piano campagna; 0 indica la testa al piano campagna, come nei fogli precedenti. Controllare quindi sia il profilo del terreno sia il riepilogo delle tratte, soprattutto per inclinazioni elevate.

### Tubo e peso

Le dimensioni del tubo servono al calcolo dell'area di acciaio, del volume di boiacca e del peso. Nel modulo verticale non equivalgono a una verifica automatica di instabilità del tubo, della sezione composta acciaio boiacca o del collegamento in testa. Diametro esterno del tubo, diametro di perforazione e diametro espanso del bulbo sono tre misure diverse.

Verificare che p_l rientri nel campo della curva selezionata: il motore rifiuta l'estrapolazione. Un messaggio di fuori abaco va risolto cambiando la base di calcolo o il dato, non forzando il valore al bordo senza motivazione. Nel report conservare curva, tipo di iniezione, α, pressione e tratte: sono le informazioni che consentono di ricostruire la stima.

## Pali e micropali caricati orizzontalmente

### Preparare un modello coerente

Il modulo applica meccanismi limite di Broms. Definire il diametro geotecnico, la lunghezza infissa, la famiglia del terreno, il vincolo in testa e l'eccentricità della forza. Per la testa libera il momento esterno è H × e. La testa impedita richiede e = 0 e tratto libero nullo, perché i meccanismi di Broms pongono il vincolo al piano campagna; con il plinto rialzato, cioè con un tratto libero sopra il terreno, il calcolo è rifiutato: usare la risposta elastica, che vincola la testa sopra il terreno. Un momento indipendente aggiunto alla forza non è gestito come azione generica.

Profondità della testa sotto il piano campagna d, nei dati generali, vale per Broms, Stratificato e risposta elastica, nel palo e nel micropalo orizzontale; 0 indica la testa al piano campagna, come nei fogli precedenti. Con d > 0 la stratigrafia resta riferita al piano campagna e il terreno sopra la testa dà le tensioni verticali ma nessuna reazione laterale; l'eccentricità e, le quote z e i diagrammi partono dalla testa. Nei coesivi la reazione resta nulla fino a 1,5D dal piano campagna, cioè fino a 1,5D − d dalla testa. Con la testa impedita vincolo e forza sono alla testa, con e = 0. Una testa interrata con un tratto libero sopra il terreno è rifiutata: azzerare uno dei due dati. Risultato, report e disegni riportano la profondità della testa.

Lo sforzo normale N è positivo a compressione in questi fogli. È una convenzione diversa dalla sezione in c.a. Non copiare il segno senza controllarlo. N entra nel calcolo del momento resistente della sezione quando questo è automatico; non trasforma Broms in un'analisi completa del secondo ordine del palo nel terreno.

I casi omogenei costituiscono il campo più direttamente interpretabile. La stratificazione della stessa famiglia e la falda sono indicate come estensioni sperimentali. Le alternanze di terreni coesivi e granulari non vengono assimilate silenziosamente a un unico terreno equivalente.

Metodo di calcolo sceglie fra Broms e Stratificato. Broms chiude le reazioni dei terreni granulari con una forza concentrata al piede e accetta solo strati della stessa famiglia. Stratificato, il metodo di G. Pacini, costruisce il diagramma limite strato per strato e chiude le reazioni con un tratto rovesciato distribuito, anche per successioni di argille e sabbie. Nei terreni coesivi omogenei i due metodi danno lo stesso risultato; nei granulari Stratificato dà capacità minori per il palo corto a testa libera e per l'intermedio a testa impedita. La teoria è riassunta nel capitolo [Capacità orizzontale in terreno stratificato](wiki:capacita-orizzontale-in-terreno-stratificato) e svolta per intero nell'articolo installato nella cartella Guide.

### Momento resistente del palo in calcestruzzo

Nel ramo automatico assegnare sezione circolare, diametro, copriferro, staffa, barre longitudinali distribuite uniformemente e materiali. Verificare la posizione degli assi delle barre e il valore di N. La procedura confronta due discretizzazioni; se non raggiunge l'accordo richiesto o esce dal campo ammesso, il momento non deve essere considerato disponibile.

Secondo anello interno aggiunge una seconda corona di barre all'interno della prima, come nella sezione in c.a.: Numero barre dell'anello interno, Diametro barre dell'anello interno, anche diverso da quello della prima fila, e Distanza libera dalla prima fila; i campi vuoti partono dalle barre della prima fila e da 30 mm. L'anello entra nella sezione e nelle verifiche; nella distinta della risposta elastica ogni diametro ha il proprio gruppo, per esempio G02A e G02B, con ancoraggio e sovrapposizione propri. Nei report di Broms e Stratificato la frase che segue la tabella Sezione circolare e materiali comincia con «Secondo anello interno:» e riporta numero, diametro e distanza libera; nel report della risposta elastica la riga Armatura di ogni tratto riporta il secondo anello del tratto, quando c'è. Senza anello i report non cambiano.

È possibile assegnare My manualmente, corredandolo della provenienza. Registrare quale sezione, N, diagrammi e coefficienti abbiano prodotto il valore. My non è un parametro da aumentare fino a far passare il controllo. La formazione di una cerniera nel modello richiede inoltre una valutazione separata della capacità rotazionale del dettaglio.

### Sezione CHS del micropalo

Inserire il diametro esterno e lo spessore del tubo in millimetri, distinti dal diametro geotecnico in metri. Il momento automatico è disponibile nel campo di classe 1 adottato. La riduzione con N utilizza l'interazione semplificata specificata nella guida teorica. La boiacca non incrementa la resistenza della sezione CHS in questo ramo.

Un tubo classificato fuori campo richiede un altro calcolo di sezione oppure un My documentato. Non basta che l'area del tubo sia grande: snellezza locale, sforzo normale e possibile instabilità globale pongono problemi differenti.

### Interpretare diagrammi e capacità

Leggere Hu, il regime governante, My usato, profondità caratteristiche e diagrammi di pressione, taglio e momento. La pressione limite rappresentata lungo il palo ha unità kN/m; non è una pressione superficiale in kPa. Il passaggio da Hu a resistenza di progetto introduce separatamente coefficienti statistici, parziale di resistenza ed efficienza di gruppo.

Con il metodo Broms, nei regimi granulari corti o intermedi può comparire una reazione concentrata al piede, necessaria per l'equilibrio del modello; con Stratificato la stessa chiusura è distribuita sotto la profondità d'inversione. Un salto nel taglio in quel punto non è necessariamente un errore grafico. Il risultato non include spostamenti laterali di esercizio e non è una soluzione con molle p y. Se il problema principale è limitare la rotazione in testa o lo spostamento, occorre un modello di deformabilità ulteriore.

Diagrammi e dettagli riporta nella scheda Riferimenti le fonti del metodo con il loro ruolo nel modello. «Apri riferimento» apre i riferimenti pubblicati e, per «G. Pacini — approccio al terreno stratificato», l'articolo [Capacità orizzontale del palo in terreno stratificato](guida:Palo orizzontale in terreno stratificato.pdf), che si apre anche dal menu Start, nella cartella Articoli di ANTHEA.

### Separare tubo e diametro geotecnico

Nel micropalo il diametro geotecnico D è in metri e governa il contatto con il terreno. Diametro esterno De e spessore t del tubo CHS sono in millimetri e governano la sezione metallica. De deve essere minore di D dopo la conversione delle unità. Il catalogo dimensionale non assegna automaticamente l'acciaio né garantisce disponibilità commerciale.

Il momento automatico richiede un CHS di classe 1 e una forza assiale inferiore alla resistenza assiale plastica. Il riempimento non contribuisce al momento resistente adottato. Per classi diverse il programma non attribuisce automaticamente la duttilità necessaria al meccanismo plastico; il momento manuale richiede una provenienza motivata.

Il calcolo di capacità con Broms è distinto dalla [risposta elastica del palo](wiki:guida-palo-elastico). Non usare un carico limite per dedurre direttamente lo spostamento di esercizio.

## Sezione composta da ponte

### Definire la sezione locale

Nel Pannello di controllo aprire Geometria e scegliere il Tipo di sezione: H saldato, H con anima inclinata oppure Cassoncino. Inserire soletta, carpenteria e armature opzionali. La scelta cambia la geometria, i campi visibili e l'interpretazione delle larghezze; non è una semplice variante del disegno. Queste sezioni appartengono al modulo Sezione composta da ponte, distinto dalle famiglie parametriche del predimensionamento Bridge Design.

L'Altezza libera anima è la distanza verticale netta fra le flange. Lo Spessore anima è misurato perpendicolarmente alla lamiera anche quando questa è inclinata: inserire lo spessore nominale, non la sua proiezione orizzontale. Il programma calcola la lunghezza inclinata e la sezione equivalente necessaria all'analisi. Tutte queste dimensioni sono in millimetri.

La Seconda piattabanda inferiore è disponibile solo per H saldato. Le due piastre sono reali e distinte, con spessore e larghezza propri; la seconda non deve essere più larga della prima. Passando all'anima inclinata o al cassoncino il riquadro viene nascosto e la seconda piastra non partecipa al calcolo, anche se un valore precedente resta nell'archivio. Tornando all'H controllare nuovamente l'opzione prima di ricalcolare.

La larghezza efficace della soletta beff è un dato assegnato. Va determinata esternamente per la sezione e la situazione considerate. Il foglio non ricostruisce dalla sola geometria trasversale tutte le luci equivalenti e le condizioni longitudinali necessarie. Le quote delle barre seguono la convenzione faccia asse mostrata dal controllo: non confonderle con il copriferro esterno della staffa. Nelle fasi della sezione la compressione è negativa e la trazione positiva; le reazioni assegnate per gli appoggi hanno invece l'etichetta specifica positiva a compressione.

### Compilare la sezione con anima inclinata

Selezionare H con anima inclinata e compilare Scostamento anima al piede. Il valore è lo spostamento orizzontale del piede rispetto alla sommità: positivo verso destra, negativo verso sinistra. Non è un angolo in gradi. La piattabanda superiore è centrata sulla sommità dell'anima e quella inferiore sul piede. Le larghezze superiore e inferiore rimangono quelle delle rispettive piattabande.

Come esempio, impostare Altezza totale H = 1855 mm, da cui il motore ricava altezza libera dell’anima 1800 mm, spessore anima 14 mm, scostamento +300 mm, piattabanda superiore 500 × 25 mm e inferiore 700 × 30 mm. Il disegno deve mostrare il piede spostato a destra, lunghezza della lamiera 1824,829 mm e inclinazione 9,462° dalla verticale. Lo spessore orizzontale equivalente è 14,193 mm; questo valore è un risultato, mentre nel campo Spessore anima devono rimanere 14 mm. Cambiando lo scostamento a −300 mm si ottiene la configurazione speculare.

![H con anima inclinata e scostamento positivo di 300 mm](../artefatti/guide_anthea_itec_rev03/interfaccia/sezione_anima_inclinata.png)

Il programma accetta inclinazioni fino a 45° dalla verticale, comprese quelle negative: il valore assoluto dello scostamento non deve superare l'altezza libera. Questo è il campo geometrico dell'implementazione, non una verifica di stabilità della trave. Se compare un errore, correggere il dato senza confondere altezza verticale e lunghezza inclinata.

### Compilare il cassoncino

Selezionare Cassoncino. La carpenteria comprende due anime simmetriche, due piattabande superiori separate e un fondo; la soletta chiude superiormente la cella nella configurazione composta. Interasse anime in sommità indica la distanza fra gli assi delle anime sotto le piattabande superiori. Scostamento anima al piede indica il rientro di ciascuna anima: un valore positivo restringe il fondo, uno negativo lo allarga. L'interasse al piede è quello superiore meno due volte lo scostamento.

Larghezza superiore è la larghezza di ciascuna delle due piattabande. Larghezza inferiore 1 è la larghezza dell'intero fondo. Non inserire nella prima casella la somma delle due flange né nella seconda metà del fondo. Il numero di pioli per fila e i relativi dettagli si riferiscono a ciascuna piattabanda superiore; il modello ripartisce fra le due piattabande il flusso totale di connessione. Per i dati di fatica assegnare i flussi per piattabanda, senza dividere una seconda volta un valore già ripartito.

Per riprodurre l’esempio usare Altezza totale H = 1850 mm, che con le due flange da 25 mm produce altezza libera 1800 mm, spessore anima 14 mm, interasse superiore 1800 mm, scostamento 250 mm, ciascuna piattabanda superiore 450 × 25 mm e fondo 1400 × 25 mm. L'interasse al piede è 1300 mm; le due anime sono lunghe 1817,278 mm e inclinate di 7,907°. Il disegno deve indicare due piattabande da 450 mm, non una sola piattabanda da 450 mm. Nel calcolo N–Mx la larghezza superiore complessiva è 900 mm.

![Cassoncino con due anime inclinate e due piattabande superiori](../artefatti/guide_anthea_itec_rev03/interfaccia/sezione_cassoncino.png)

Il fondo deve contenere gli appoggi delle due anime, considerate con il loro spessore orizzontale; le anime devono restare separate e le piattabande superiori non devono sovrapporsi. Nell'esempio il fondo interno netto è 1285,866 mm e ogni sbalzo esterno è 42,933 mm. Queste larghezze dipendono dallo spessore e dall'inclinazione: non coincidono esattamente con 1300 e 50 mm. Il programma respinge le geometrie incompatibili, ma la loro accettazione non certifica saldature o montaggio. Il comportamento torsionale si verifica soltanto attivando le opzioni di torsione del cassoncino descritte più avanti.

### Leggere le proprietà e riconoscere i limiti

Nel pannello Proprietà della sezione distinguere Carpenteria asse orizzontale calcolo da Sezione reale nel piano Model. Il primo gruppo descrive il modello usato per N–Mx: area, quota del baricentro e inerzia rispetto all'asse orizzontale corrispondono alla carpenteria reale; le proprietà rispetto all'altro asse sono quelle della rappresentazione equivalente. Il secondo gruppo permette di consultare le proprietà piane della geometria effettiva, compresi prodotto d'inerzia e assi principali. Un valore Ixy nullo nel primo gruppo non dimostra che la carpenteria inclinata sia simmetrica.

| Controllo dell'esempio | H inclinata di 300 mm | Cassoncino con rientro 250 mm |
| --- | --- | --- |
| Area della sola carpenteria | 590,476 cm² | 1083,838 cm² |
| Quota del baricentro dalla sommità dell'acciaio | −1057,245 mm | −1030,239 mm |
| Inerzia orizzontale baricentrica | 3385733,876 cm⁴ | 6041896,480 cm⁴ |
| Anime e piattabande superiori | Una e una | Due e due |

I valori in tabella sono geometrici e lordi: non includono soletta, armature o riduzioni di efficacia. I risultati di fase possono essere differenti perché cambiano collaborazione, omogeneizzazione e parti efficaci. Ricordare che 1 cm² equivale a 100 mm² e 1 cm⁴ a 10000 mm⁴.

L'analisi assume flessione retta attorno all'asse orizzontale e un vincolo laterale fornito da soletta e controventi. Per l'H inclinata non risolve l'accoppiamento della flessione dovuto al prodotto d'inerzia della sola carpenteria. Occorre verificare separatamente che l'ipotesi sia rappresentativa, soprattutto durante getto e montaggio: selezionare una fase Solo acciaio non crea da sé un vincolo laterale reale.

Per il cassoncino torsione e distorsione della cella e diaframmi sono verificati soltanto se si attivano le verifiche a torsione. Restano esclusi gli irrigidimenti longitudinali del fondo: il fondo non viene verificato come piastra irrigidita longitudinalmente. L'H con anima inclinata resta in flessione retta e un ΔT salvato nelle sue fasi non viene considerato. La rappresentazione grafica chiusa e un esito positivo delle verifiche disponibili non coprono i fenomeni esclusi. Leggere Info modello, gli avvisi e la descrizione del tipo di sezione nel report prima di utilizzare i risultati.

Gli archivi precedenti privi del tipo di sezione vengono interpretati come H saldato. Dopo il cambio di tipologia salvare con un nome riconoscibile, riaprire il foglio e controllare tipo, scostamento e interasse. Il report delle sezioni con anima inclinata e del cassoncino distingue le dimensioni equivalenti impiegate nel calcolo dalle lamiere reali; confrontare entrambe le tabelle con il disegno.

### Scegliere consapevolmente il metodo

| Metodo | Impiego operativo | Aspetto da controllare |
| --- | --- | --- |
| Cumulativo | Somma di contributi elastici delle fasi sulla geometria efficace comune | Non rappresenta una memoria costitutiva cronologica |
| Storico lineare | Successione di fasi con deformazioni e riferimento al getto | φ o n del nuovo incremento non rilassa automaticamente tutto il passato |
| Storico non lineare | Evoluzione a fibre con plasticità dell'acciaio e scarico elastico | Sezione lorda e caratteristiche istantanee; niente riduzione locale di classe 4 |

La scelta va fatta prima di interpretare le differenze fra due risultati. Uno storico non lineare non è semplicemente un cumulativo «più preciso» per ogni scopo: introduce fenomeni diversi e non include tutti i controlli locali del cumulativo. I controlli di taglio, connessione e accessori non vengono valutati dai due metodi storici.

Con Storico non lineare compare il campo «CLS teso (non lineare)»: Trascurato, predefinito anche per i fogli salvati prima, oppure Tension stiffening. La stessa scelta vale per le curve di risposta; il legame è descritto nella guida teorica.

### Costruire le fasi

Aprire la scheda fasi e tensioni e inserire gli incrementi in ordine. Un percorso comune comprende peso della carpenteria e getto sulla sezione di acciaio, permanenti successivi sulla sezione composta a lungo termine e variabili sulla sezione composta a breve termine. Il primo carico applicato prima della maturazione non deve beneficiare della soletta che ancora non collabora.

Ogni riga rappresenta un incremento, non necessariamente il totale della situazione. Inserire un totale in ciascuna riga può duplicare i carichi. Assegnare φ oppure n, controllando che il valore di uno derivi coerentemente dall'altro. Scegliere il punto di applicazione di N: baricentro lordo della fase, baricentro efficace aggiornato o riferimento comune. Il momento riportato al riferimento cambia con questa scelta.

Le azioni di SLU devono arrivare già combinate e fattorizzate. Il selettore SLU o SLE modifica i limiti di controllo, non moltiplica automaticamente le azioni per tutti i coefficienti delle combinazioni. Anche una variazione di segno di V nelle fasi si somma algebricamente nella domanda di connessione: verificare il significato delle situazioni cumulate.

Per il ritiro assegnare la deformazione con il segno corretto: un accorciamento è negativo. Se l'unità è microdeformazione, −250 corrisponde a −0,25 per mille. Il modulo tratta l'effetto locale di sezione. Le azioni dovute a vincoli longitudinali dell'intero ponte devono provenire da un modello globale.

### Leggere efficacia tensioni e controlli locali

Selezionare la situazione da visualizzare e distinguere tensioni della fase, contributi precedenti e stato cumulato. Le parti inefficaci dell'acciaio evidenziano la riduzione per instabilità locale sotto tensioni normali. L'anima resistente a taglio non viene automaticamente ridotta nello stesso modo. Leggere area, baricentro, inerzia, coefficienti di omogeneizzazione e residui di convergenza.

Negli SLE con la soletta esclusa la tabella dei contributi mostra la colonna «Δσs tension stiff.» per le barre tese: la loro tensione totale è la somma dei contributi più Δσs.

Nel metodo cumulativo aprire i controlli di taglio, irrigidimenti, appoggi e pioli. Inserire geometrie reali, pannelli, reazioni, eccentricità, saldature, passi e materiali. Un irrigidimento non idoneo non deve produrre il beneficio di un pannello corto. La connessione richiede anche dettagli costruttivi e, se pertinente, dati di fatica; un rapporto di resistenza favorevole del singolo piolo non esaurisce questi controlli.

La colorazione principale della sezione riguarda le tensioni normali rapportate ai limiti. Non è una mappa completa di instabilità, fatica, torsione o sollevamento della soletta. Per conoscere lo stato di quelle verifiche occorre leggere le rispettive tabelle e gli avvisi.

Con anime inclinate il taglio V inserito nelle fasi rimane il taglio verticale totale della sezione. Non trasformarlo preventivamente nel taglio della singola lamiera: il programma applica V/cos α per l'H inclinata e V/(2 cos α) per ciascuna anima del cassoncino. Con V = 600 kN negli esempi precedenti le domande nel piano delle lamiere sono rispettivamente 608,276 kN e 302,880 kN per anima. Il risultato di resistenza globale viene riportato alla componente verticale totale.

### Torsione distorsione e diaframmi del cassoncino

Con Cassoncino selezionato, in fondo al Pannello di controllo compare il riquadro Cassoncino · torsione, distorsione e diaframmi. Attivare Verifiche a torsione del cassoncino: nella tabella Sollecitazioni diventa modificabile la colonna ΔT [kNm] e ogni scheda di fase mostra Momento torcente T. ΔT è l'incremento del momento torcente della fase nella sezione, ricavato dal modello globale e già combinato come N, Mx e V. Il ritiro non ha momento torcente. Per H saldato e H con anima inclinata la colonna non compare e un valore rimasto nell'archivio non partecipa al calcolo.

Controvento superiore · spessore equivalente t* chiude la cella del cassone di acciaio nelle fasi Solo acciaio: inserire lo spessore della lamiera equivalente al controvento orizzontale posto fra le piattabande superiori. Con t* = 0 il cassone è aperto e la torsione delle fasi di solo acciaio resta da completare, perché la torsione non uniforme della sezione aperta non viene calcolata. Nelle fasi composte la cella è chiusa dalla soletta e t* non interviene. Il flusso nel controvento viene riportato, ma le sue aste vanno verificate a parte.

Per la distorsione assegnare la luce della campata e, se presenti, il passo dei diaframmi intermedi e il loro tipo: Piastra con il suo spessore, oppure Controvento a X con area, raggio d'inerzia minimo e rapporto Lcr/L di una diagonale. Il torcente distribuito m_t e il torcente concentrato T_c derivano dai carichi eccentrici della combinazione esaminata, per esempio corsie caricate da un solo lato. Il programma applica m_t su tutta la luce e T_c nella posizione più sfavorevole, con lo stesso segno. Con luce nulla la distorsione non viene analizzata e resta da completare.

All'appoggio assegnare il torcente trasferito agli apparecchi, l'interasse trasversale degli apparecchi e lo spessore del diaframma d'appoggio. Come la reazione R, questi dati sono inviluppi indipendenti dalle fasi. Quando la verifica dell'appoggio è attiva, la coppia T/e_b si somma a metà della reazione sull'irrigidimento dell'anima più caricata.

![Riquadro delle verifiche a torsione del cassoncino](../artefatti/guide_anthea_itec_rev04/interfaccia/cassoncino_torsione_ingressi.png)

Per riprodurre l'esempio della guida teorica usare il cassoncino descritto sopra con le tre fasi iniziali del foglio, t* = 4 mm e ΔT pari a 200, 300 e 1000 kNm. Nella scheda Verifiche il gruppo Cassoncino · torsione, distorsione e diaframmi riporta per la fase di solo acciaio A0 = 2,829 m² e q = 35,351 kN/m; per le due fasi composte A0 = 3,079 m² e q = 48,712 e 162,372 kN/m. Il flusso cumulato nelle anime e nel fondo è 246,435 kN/m, quello della soletta 211,083 kN/m. Le tensioni tangenziali di torsione sono 17,602 MPa nelle anime da 14 mm e 9,857 MPa nel fondo da 25 mm.

![Flussi di torsione per fase nella scheda Verifiche](../artefatti/guide_anthea_itec_rev04/interfaccia/cassoncino_torsione_risultati.png)

Aggiungendo luce 40000 mm, diaframmi a piastra da 12 mm ogni 5000 mm, m_t = 60 kNm/m e T_c = 600 kNm, il programma individua 7 diaframmi intermedi e calcola al fondo σdw = 14,946 MPa, pari al 20,2% della tensione di flessione: la tensione di distorsione viene quindi sommata nelle verifiche del fondo. Il diaframma intermedio più sollecitato ha τ = 8,394 MPa.

Nella tabella Verifiche la riga Anima · resistenza a taglio comprende il flusso torsionale sull'anima più caricata; le righe Fondo, Soletta, Distorsione e Diaframma riportano i controlli specifici. Restano da completare i controlli privi di dati, per esempio il taglio da torsione nella soletta senza armatura trasversale attiva o la torsione del cassone aperto. I metodi con storico non eseguono queste verifiche e lo segnalano negli avvisi.

### Curve di risposta

La terza scheda permette curve M κ a N fissato oppure N ε a curvatura fissata. Si può partire da uno stato vergine oppure dalla storia di una fase ricostruita dal motore non lineare. Quando si parte da una fase caricata, il ramo di risposta conserva la memoria prevista dal modello e non riparte da zero tensioni.

Controllare la variabile mantenuta costante, direzione dei passi, grandezza degli incrementi e criterio di arresto. Una curva interrotta per mancata convergenza non va completata idealmente a mano per dichiarare una resistenza. Le curve sono caratteristiche e su sezione lorda; non costituiscono da sole una capacità di progetto con instabilità locale inclusa.

La vista separata segue la fase selezionata. F11 la porta a schermo intero ed Esc esce da quella modalità. CSV ed esportazioni delle fibre consentono un controllo numerico dei dati; la precisione del file non è limitata ai decimali mostrati a video.

## Bridge Design

Questa guida accompagna la costruzione di un progetto preliminare, la ricerca delle alternative e la lettura dei risultati di Bridge Design in ANTHEA. Il percorso è pensato per chi deve confrontare tipologie, campate e quantità prima di sviluppare il modello strutturale e il computo completo. Il [modello teorico di Bridge Design](wiki:bridge-design) spiega formule e limiti; qui ogni passo indica cosa inserire, cosa controllare e come interpretare il risultato.

La parola ottimo significa migliore fra le combinazioni esplorate con i dati e i criteri scelti. La finestra non fornisce un’autorizzazione a costruire la soluzione e non esegue automaticamente le verifiche dei moduli GPC Engine. Usa il risultato per selezionare poche alternative motivate e conserva insieme geometrie, prezzi, ipotesi e avvisi.

### Preparare i dati prima di aprire la ricerca

Servono almeno lunghezza totale del ponte, larghezza funzionale e quota sul terreno. Per ottenere un confronto utile aggiungere l’ingombro dell’ostacolo, un’ipotesi di terreno, gli estremi su spalla o pila e un listino coerente con l’opera. Se un’informazione non è disponibile, scegliere un valore convenzionale dichiarato e pianificare una seconda ricerca con un’ipotesi diversa.

| Informazione | Dove inserirla | Controllo da fare |
| --- | --- | --- |
| Lunghezza e quota | Sito | Non confondere quota impalcato e altezza della trave |
| Corsie e larghezze | Sito | La larghezza totale comprende banchine spartitraffico e barriere |
| Ostacolo | Sito | Il modello lo centra e aggiunge un margine geometrico di 1 m |
| Tipologia e continuità | Campate | Lo schema deve essere compatibile con la famiglia |
| Altezza e spessori | Sezione | Zero significa automatico soltanto nei campi che lo dichiarano |
| Terreno pile fondazioni | Pile | Distinguere quote manuali e quote automatiche |
| Prezzi e carichi | Risultati del progetto | Adeguare le ipotesi prima di classificare le alternative |

I metri e i millimetri sono indicati accanto ai campi. Non inserire 250 in una soletta richiesta in metri: il valore corretto è 0,25. I prezzi dell’armatura sono in euro per tonnellata; 1,66 euro per kg corrispondono a 1.660 euro per tonnellata. Una differenza di unità può alterare la graduatoria più di una scelta geometrica.

### Creare il progetto di riferimento

Apri il modulo Bridge Design e lavora nella scheda Progetto. Le schede di ingresso sono Sito, Campate, Sezione, Pile e Traffico. I risultati comprendono Quantità e costi, Dettagli del predimensionamento, Confronto A / B, Prezzario, Ipotesi e coefficienti e Sezioni e quote. Il pulsante Ottimizza porta alla scheda separata Ottimizzazione.

1. In Sito imposta lunghezza, quota, corsie e larghezze accessorie. Controlla la larghezza complessiva restituita dal programma, non soltanto il numero di corsie.
2. Seleziona l’ostacolo e la larghezza. Osserva il prospetto: il disegno è schematico e non costituisce una verifica del franco, dell’alveo o della posizione reale degli appoggi.
3. In Campate scegli una famiglia iniziale e la continuità. Inserisci un numero di campate ragionevole oppure usa zero per la scelta automatica. Leggi poi le luci effettive, che non sono necessariamente tutte uguali.
4. In Sezione imposta i soli dati noti. Lascia zero nei campi automatici se vuoi che il motore li predimensioni. Per le lamiere usa gli spessori in mm; per anime e solette in cls usa le unità mostrate.
5. In Pile scegli terreno, schema e fondazione. Una misura a zero resta automatica; una misura positiva è una richiesta esplicita. Verifica gli avvisi su snellezza, compressione e capacità assiale.
6. In Traffico scegli il modello e, in fondo alla scheda, i Coefficienti parziali SLU. Apri Prezzario e Ipotesi e coefficienti per tariffe, resistenze geotecniche di riferimento e fattori ambientali.
7. Salva il progetto prima della ricerca. Se non è calcolabile, correggi l’errore prima di avviare l’ottimizzazione: il motore deve poter costruire il riferimento.

Una configurazione calcolabile può essere fuori dal campo usuale della famiglia. La scheda Progetto può mostrarla con un avviso, mentre l’ottimizzazione la esclude dai candidati ammessi. Perciò un costo visibile nella scheda Progetto non basta a dimostrare che il riferimento appartenga all’insieme esplorato.

### Scegliere e cambiare prezzario

Da Progetto apri Prezzario e premi Gestisci prezzario. La finestra conserva una bozza: il documento cambia soltanto con Applica prezzario. Assegna nome ed edizione al catalogo; gli importi sono in EUR, IVA esclusa. Nella scheda Voci del prezzario puoi aggiungere, modificare ed eliminare voci, oppure importare CSV, XLSX o un catalogo JSON esportato da ANTHEA.

Per Excel usa il foglio Prezzario e cinque colonne, in quest’ordine: Codice, Descrizione, Unita, Prezzo, Inclusioni. Le medesime intestazioni valgono per CSV; Modello CSV prepara il tracciato. Il codice deve essere univoco, la descrizione compilata e il prezzo non negativo. Sono ammesse m, m², m³, kg, t, cad e h (ore, per manodopera e noli delle analisi composte). Scrivi i numeri senza separatori delle migliaia: 1660 oppure 1660,50. Una cella prezzo vuota significa dato mancante; zero è un importo assegnato consapevolmente.

In Associazioni e anteprima collega ogni lavorazione al codice del catalogo. Le colonne Prima e Dopo mostrano il prezzo adottato nella stessa unità della lavorazione. Puoi assegnare un Prezzo personalizzato, accompagnato dal Motivo personalizzazione; lasciandolo vuoto si usa la voce associata. Se importi un altro catalogo, scegli prima se mantenere questi prezzi: l’opzione non è attiva per impostazione iniziale. Verifica sempre le associazioni, perché lo stesso codice può avere un significato diverso nel nuovo prezzario.

Inclusioni contiene codici di lavorazioni separati da |. Attualmente è supportato rebar nelle tariffe dei pali pile_1 e pile_15: l’armatura del relativo palo non viene addebitata due volte. L’inclusione non elimina le armature dell’impalcato o delle altre parti. Le inclusioni non rappresentate dal computo vengono rifiutate; non dichiarare montaggio o altre prestazioni come comprese se il modello non le distingue.

Nella scheda Analisi composte un prezzo si ricava dai suoi componenti. Per ogni analisi indica codice, descrizione, unità e componenti nella forma codice = quantità, separati da punto e virgola: per un metro cubo di getto, per esempio, CLS = 1,05; MO = 0,8; POMPA = 0,1. Le quantità sono nell'unità del componente; un componente può essere una voce del prezzario o un'altra analisi. Selezionando un'analisi, sotto la tabella compaiono il totale dei componenti, le spese generali, l'utile d'impresa e il prezzo unitario, come voci distinte del totale.

Spese generali e utile d'impresa in alto valgono per tutto il prezzario; compilando le colonne dell'analisi li sostituisci solo per quella riga. Alla prima analisi ANTHEA propone 15% e 10%: sono valori da confermare. Il codice di un'analisi si associa alle lavorazioni come una voce. Componenti mancanti o senza prezzo, analisi che si richiamano a vicenda e percentuali fuori da 0–100 impediscono di applicare il prezzario; l'esito in basso indica la causa.

L’anteprima verifica prezzi, unità e completezza per la geometria corrente. Un’associazione non risolta può non intervenire sul ponte corrente ma escludere una diversa alternativa: controlla anche l’avviso sul catalogo complessivo. Applica prezzario registra una revisione e rivaluta il costo a quantità costanti. La precedente ricerca deve essere rieseguita, perché può cambiare la graduatoria. Puoi scegliere Rivaluta anche l’alternativa A con questo prezzario; altrimenti A conserva la propria base economica, da leggere prima del confronto.

La scheda Revisioni conserva le copie precedenti. Prepara ripristino carica una nuova bozza da controllare e applicare, senza cancellare la cronologia. Esporta catalogo JSON permette il riuso in un altro progetto. Le modifiche ai fattori CO₂ hanno una revisione distinta: cambiare prezzario non aggiorna automaticamente l’indicatore ambientale. Report e CSV identificano i dati effettivamente usati dal risultato.

### Impostare il traffico del ponte

I nuovi progetti adottano Schema 1 NTC 2018. Nella scheda Traffico controlla le carreggiate e il modello. «Coefficienti parziali SLU» chiude la scheda Traffico. Con «NTC 2018 Tab. 5.1.V (Model)», la scelta dei documenti nuovi, gli inviluppi SLU sono gli estremi delle combinazioni A1 dei ponti stradali generate dalla libreria Model: γG1 1,35/1,00 su impalcato e strutture, γG2 1,50/0 su finiture e barriere, γQ 1,35/0 sul traffico; nei minimi i permanenti sono favorevoli. Con «Moltiplicatori del progetto», la scelta dei documenti salvati prima della 1.16.0, valgono γG e γQ del progetto, gli stessi nei massimi e nei minimi, come prima: solo con questa scelta la scheda mostra γQ e il comando per il coefficiente consigliato, che propone 1,35; un valore assegnato rimane una scelta esplicita dell’utente. La nota (2) della Tab. 5.1.V (permanenti non strutturali ben definiti) si applica con i moltiplicatori. Il diagramma mostra estremi positivi e negativi; la reazione minima aiuta a riconoscere possibili sollevamenti.

I documenti precedenti mantengono Uniforme equivalente (legacy), i Moltiplicatori del progetto e il proprio coefficiente, quindi i loro risultati non cambiano. Il passaggio a Schema 1 è volontario e richiede un nuovo calcolo e una nuova ricerca. Gli estremi di momento, taglio, freccia e reazione possono provenire da posizioni differenti del traffico: non sono un unico stato simultaneo. Per arco, reticolare, strallato e sospeso leggi l’ambito parametrico dichiarato dal risultato; il predimensionamento non sostituisce un’analisi spaziale del ponte.

### Leggere sezioni spessori e lunghezze

Apri Sezioni e quote prima di valutare il solo costo totale. Il prospetto riporta componenti, simboli, valori, unità, origine e significato. Impostato identifica un dato assegnato; Automatico una regola del motore; Derivato una conseguenza degli ingressi. Nelle famiglie speciali compaiono anche Adottato e Predimensionato. Nessuna di queste etichette significa sezione verificata.

d è l’altezza complessiva in campata, con soletta. dpila è l’altezza dell’impalcato sulle pile e può essere maggiore per il cassone variabile. h è l’altezza sotto soletta e rialzo; hw è l’altezza netta dell’anima; lw è lo sviluppo reale di un’anima inclinata. Le tabelle distinguono larghezza della piattabanda, spessore, numero di travi, luce di ogni campata e sviluppo complessivo degli elementi longitudinali.

Per esempio, H per 4V uguale a 1 indica uno scarto orizzontale pari a un quarto dell’altezza verticale dell’anima; non indica una pendenza di 45 gradi. Il computo usa lo sviluppo inclinato per l’area e la proiezione verticale per l’inerzia. Per le travi a U controlla che la somma delle larghezze superiori non provochi sovrapposizioni.

Nel prospetto delle sottostrutture leggi separatamente altezza e dimensione del fusto, numero di colonne, dimensioni del pulvino, lati e spessore del plinto, numero, diametro e lunghezza dei pali. Una fondazione bloccata con dimensioni a zero può essere ridimensionata dopo una variazione delle reazioni: controlla sempre le quote effettivamente adottate.

### Scegliere obiettivo e costanti

La scheda Ottimizzazione propone Costo minimo, CO₂ minima e Compromesso costo / CO₂ 50 / 50. Il compromesso normalizza i due indicatori sui minimi della ricerca. Non valuta sicurezza, paesaggio, tempi, manutenzione o interferenze oltre ai filtri presenti. Se uno di questi aspetti governa la scelta, traduci ciò che puoi in vincoli e confronta esternamente le alternative rimaste.

| Blocco | Quando usarlo | Significato operativo |
| --- | --- | --- |
| Tipologia | Hai già scelto una famiglia | Confronta geometrie della stessa famiglia |
| Numero campate | Gli appoggi sono vincolati | Conserva il numero non una libera lista di luci |
| Altezza in campata | Hai un limite già assegnato | Congela la quota adottata in campata |
| Dimensioni della sezione | Vuoi mantenere la sezione ordinaria | Richiede anche il blocco della tipologia |
| Continuità | Vuoi confrontare lo stesso schema | È bloccata all’avvio |
| Schema pila e quote imposte | Le sottostrutture sono già definite | Le quote automatiche restano automatiche |
| Fondazione e lunghezza pali | Vuoi un confronto a fondazione fissata | Quote e conteggi a zero possono ricalcolarsi |

Per un primo confronto ampio puoi liberare tipologia e numero di campate, conservando continuità, schema di pila e fondazione. In una seconda ricerca puoi liberare anche le sottostrutture. Fare entrambe le ricerche aiuta a capire se il risparmio proviene dall’impalcato o da una modifica sostanziale degli appoggi.

La lunghezza totale e la larghezza non vengono ridotte dall’ottimizzatore per ottenere un costo inferiore. Carichi, prezzi, coefficienti e materiali restano quelli del progetto. Liberare la sezione richiama dimensioni standard; non avvia una ricerca sistematica su tutti gli spessori possibili. I parametri avanzati di archi, cavi e ortotropi rimangono quelli impostati nel progetto.

### Impostare range utili

Il pulsante Suggerisci campate dalla lunghezza propone l’intervallo ricavato dai campi usuali delle famiglie. È un punto di partenza: il controllo preventivo usa la luce media e non garantisce che le luci finali, dopo la distribuzione e l’ostacolo, siano ammesse. Se hai bloccato il numero delle campate, mantieni minimo e massimo compatibili con quel numero.

Il minimo di altezza si riferisce alla campata; il massimo si applica anche all’altezza dell’impalcato sopra le pile. Zero significa nessun limite aggiuntivo, non una sezione di altezza nulla. Per un cassone variabile un limite massimo può quindi escludere una geometria la cui altezza in mezzeria sembra compatibile.

Per le griglie percentuali, 100 significa la regola automatica della combinazione. Con altezza 100–120 e passo 10 si esaminano 100, 110 e 120%. Con pali 100–150 e passo 25 si esaminano tre lunghezze riferite alla classe di terreno. Se la fondazione è bloccata, la relativa griglia è disabilitata. Non sono esplorati valori inferiori al 100%.

Come percorso iniziale, usa 2 o 3 valori di altezza e un intervallo di campate motivato dal sito. Se il vincitore cade all’estremo del range, amplia il campo solo dove fisicamente plausibile e ripeti. Se interessa affinare l’altezza, riduci il passo vicino ai risultati senza superare 11 valori nella griglia. Non è necessario usare subito tutti i 50.000 tentativi ammessi.

Una griglia più fitta aumenta il numero di combinazioni ma non elimina i limiti del modello. Se mancano dati su varo, pali o traffico, è spesso più utile ripetere scenari con ipotesi diverse che usare molti decimali sulla stessa ipotesi incerta.

### Avviare e leggere l’avanzamento

Premi Avvia ottimizzazione. Il motore comunica i nuovi tentativi in gruppi; la preview segue il migliore provvisorio quando cambia, al massimo due volte al secondo. Non viene ridisegnato ogni candidato. Il progetto corrente cambia soltanto quando premi Applica soluzione selezionata.

La barra indica quante combinazioni sono state calcolate e quante sono state ammesse rispetto alla griglia prevista. Il totale previsto può diminuire di fatto per salti e duplicati. Alla fine sono esposti tentativi calcolati, ammessi, geometrie distinte ed esclusi. Non sommare le occorrenze dei motivi di esclusione per ricavare il numero degli esclusi, perché uno stesso tentativo può violare più condizioni.

Interrompi annulla la ricerca senza applicare una soluzione. Una modifica agli input, al listino o ai vincoli rende obsoleta la ricerca: ANTHEA richiede di ripeterla. Non usare una graduatoria calcolata con prezzi vecchi per interpretare il costo di un progetto modificato.

### Usare i grafici

Il grafico Variabili esplorate ha in ascissa il numero del tentativo e permette di visualizzare costo, CO₂, altezza, campate, lunghezza pali, tipologia, schema di pila, fondazione e continuità. I punti grigi identificano tentativi esclusi; le quote non calcolabili non sono disegnate. Nei grafici economico e ambientale la linea scura indica il minimo progressivo, non una curva di convergenza strutturale.

L’ordine dei tentativi deriva dall’enumerazione del programma. Non interpretare una successione di punti come un percorso continuo di trasformazione del ponte, né come una misura della sensibilità di una sola variabile se ne cambiano più di una. Per studiare una variabile, blocca le altre e ripeti una ricerca dedicata.

La Famiglia di soluzioni plausibili rappresenta costo e CO₂ delle geometrie ammesse. In basso a sinistra si trovano i valori minori. L’anello identifica Pareto, la stella il migliore per l’obiettivo e la croce il progetto corrente. Filtra per famiglia o attiva Solo frontiera Pareto; clicca un punto per esaminarlo anche quando non compare fra le prime N righe.

![Nuvola costo e CO₂ ricavata dai risultati riproducibili dell’esempio](../documentazione/Bridge_Design/figure/costo-co2.png)

Il grafico qui riportato è generato dai dati della ricerca descritta nel capitolo successivo. Nella finestra gli stessi concetti sono interattivi. Una nuvola non sostituisce il controllo del prospetto: due punti vicini possono avere numeri di appoggi, dimensioni o difficoltà costruttive molto diversi.

![Variazione di altezza e costo durante l’enumerazione dell’esempio](../documentazione/Bridge_Design/figure/traccia.png)

### Esempio guidato da centoventi metri

Questo esempio conserva i risultati della revisione storica con traffico uniforme. Se parti da un nuovo documento, seleziona Uniforme equivalente (legacy) e «Moltiplicatori del progetto» in Coefficienti parziali SLU, poi imposta γQ = 1,50 e mantieni il listino iniziale riportato nella guida teorica. Schema 1 o un prezzario diverso producono un altro confronto: non devono restituire i numeri di questo esempio.

| Campo | Valore |
| --- | --- |
| Lunghezza e quota | 120 m e 12 m |
| Corsie e larghezza corsia | 4 e 3,65 m |
| Banchine spartitraffico barriere | 1,50 m per lato 1,60 m 0,50 m per lato |
| Larghezza totale attesa | 20,20 m |
| Ostacolo e terreno | Nessuno e Roccia |
| Famiglia e campate | Travi a I in c.a.p. e 3 |
| Continuità | Attiva |
| Schema pila | Setto |
| Fondazione | Automatica che in questo caso adotta plinto diretto |
| Quote automatiche | Altezza sezione pila e fondazione a zero |
| Estremi | Entrambi su spalla |

Il riferimento restituisce costo 2.332.503,86 euro e CO₂ 1.371,939 t. Le tre luci non sono 40 m ciascuna: con continuità e tre campate le terminali hanno peso 0,8, quindi le luci sono circa 36,923 + 46,154 + 36,923 m. Questo è il primo controllo da fare per capire quale ponte si sta confrontando.

Nella ricerca scegli Costo minimo; blocca Continuità, Schema pila e Fondazione. Lascia libere Tipologia, Numero campate, Altezza e Dimensioni della sezione. Imposta campate da 1 a 8, altezze da 100 a 120% con passo 10, limiti assoluti d’altezza a zero. La griglia dei pali non interviene perché la fondazione è bloccata.

Il risultato atteso è 91 tentativi, 70 ammessi e 69 geometrie distinte. Il primo candidato è Cassone in c.a.p., due campate da 60 m, altezza 2,565257 m, costo 2.049.525,73 euro e CO₂ 1.287,035 t. Il risparmio rispetto al riferimento è circa 282.978 euro, pari al 12,13%; la riduzione ambientale è circa 84,904 t, pari al 6,19%. Le molte cifre servono a ripetere l’esempio, non descrivono la precisione della stima reale.

Ripeti con CO₂ minima e poi con Compromesso. In questo caso vince la stessa geometria; non aspettarti sempre questa coincidenza. Puoi cambiare N per osservare altre soluzioni senza eseguire nuovamente il calcolo. Apri il prospetto della selezione e confronta altezza, sezioni, numero di pile e quantità prima di applicarla.

![Differenze di costo fra il riferimento e alcuni scenari dell’esempio](../documentazione/Bridge_Design/figure/scenari.png)

Se imposti un minimo di cinque campate, il riferimento a tre campate è escluso. Vince una soluzione a T in c.a. con cinque campate e costo 2.657.209,43 euro. Un vincitore più caro del riferimento è quindi possibile quando il riferimento non soddisfa i nuovi vincoli. Leggi sempre la riga che descrive le esclusioni del progetto corrente.

Moltiplicando tutti i prezzi per 0,8 o per 1,2, senza cambiare altro, la geometria vincente deve restare la stessa, la CO₂ deve restare invariata e il costo deve diventare rispettivamente 1.639.620,59 e 2.459.430,88 euro. Questo è un controllo di coerenza economica, non uno studio completo della sensibilità: per quest’ultimo occorre variare separatamente le voci più incerte, non soltanto tutto il listino dello stesso fattore.

### Selezionare applicare e conservare le alternative

La tabella ordina le soluzioni secondo il punteggio non arrotondato. Se due righe mostrano lo stesso punteggio stampato, possono differire nelle cifre successive. Sono mostrati posizione, famiglia, campate, altezza, costo, differenza dal riferimento, CO₂, punteggio e appartenenza a Pareto.

Seleziona una riga o un punto della nuvola e leggi Variazioni rispetto al progetto corrente. La tabella confronta luci, continuità, fondazione e quote tecniche adottate. Controlla anche gli avvisi: un candidato ammesso conserva comunque i limiti generali del modello e le voci di computo mancanti.

Premendo Applica soluzione selezionata, il progetto adotta gli input del candidato. Se non esiste già un’alternativa A, ANTHEA conserva il progetto precedente come A. Se A esiste, non viene sostituita automaticamente: controlla che il confronto A / B rappresenti ancora ciò che vuoi confrontare. Dopo l’applicazione rivedi Sezioni e quote e salva il documento con un nome che renda riconoscibile la variante.

La ricerca completa non va considerata un registro permanente nel file di progetto. Per documentare la decisione salva il progetto di riferimento e le alternative scelte, esporta i loro computi e annota obiettivo, vincoli e griglie usati. L’export del progetto descrive la soluzione corrente; non sostituisce automaticamente un report dell’intera nuvola di ottimizzazione.

### Prezzi geotecnica e scenari da confrontare

Prima di concludere prepara almeno uno scenario di riferimento e uno cautelativo motivato dai dati disponibili. Varia, per esempio, lunghezza dei pali, resistenze geotecniche di riferimento, costo della carpenteria, incidenza di montaggio e necessità di opere provvisionali. Se il vincitore cambia facilmente, conserva più alternative da approfondire invece di attribuire precisione eccessiva al primo posto.

Non usare la pressione convenzionale del terreno come se fosse automaticamente una portanza certificata. Un aumento della lunghezza del palo accresce l’attrito laterale nel modello, ma non simula il passaggio a uno strato reale. Il progetto geotecnico deve confermare lunghezza, capacità, gruppo, cedimenti e risposta trasversale. Per un ponte in alveo, il fatto che le pile evitino l’ostacolo disegnato non dimostra compatibilità idraulica.

Leggi le note accanto ai prezzi. Pali e cls hanno inclusioni diverse; appoggi e giunti dipendono da forze e movimenti. Gli oneri aggiuntivi e gli imprevisti sono riserve, non l’insieme delle lavorazioni mancanti. Per le famiglie speciali un’offerta di sistema può includere montaggio, terminali e protezioni già computati in altre righe: evita duplicazioni.

### Usare le famiglie speciali

Arco metallico con catena e reticolare richiedono campate indipendenti. Se tieni bloccata una continuità attiva, queste famiglie non entrano nella ricerca. Per confrontarle devi liberare la continuità o impostare correttamente il riferimento. Il modello comprende arco con catena, non tutte le possibili configurazioni di arco con spinta sulle spalle.

Strallato e sospeso richiedono tre campate, impalcato continuo ed estremi su spalla. Le campate sono L/4, L/2, L/4. I campi di luce usuale si riferiscono alla campata centrale; un ponte complessivamente lungo 150 m non diventa uno strallato ammesso solo perché la lunghezza totale supera 100 m. Le antenne e i cavi hanno parametri avanzati da impostare prima della ricerca.

Per la piastra ortotropa controlla lamiera, altezza e larghezze delle canalette, interasse e spessori del cassone. Per le travi incorporate controlla spazio fra profili e cls sopra e sotto le piattabande. Il prospetto rappresenta un profilo ideale, non un componente commerciale già selezionato.

Su archi, reticolari, strallati e sospesi la mancanza di diagrammi globali non è un errore grafico. Il motore stima equilibri e quantità senza risolvere la deformabilità globale di queste strutture. Se il loro vantaggio economico è interessante, il passo successivo è un modello specialistico, non l’interpretazione della sola inerzia dell’impalcato come rigidezza del ponte completo.

### Risolvere i messaggi più comuni

| Messaggio o situazione | Significato | Azione utile |
| --- | --- | --- |
| Nessuna soluzione ammessa | Tutti i candidati violano almeno un filtro | Apri i motivi e modifica il vincolo pertinente |
| Griglia troppo estesa | Superati 50.000 tentativi previsti | Blocca variabili o aumenta il passo |
| Altezza inferiore alla regola | Quota bloccata troppo bassa | Rivedi altezza o numero di campate |
| Pile interferenti con ostacolo | Appoggi nella fascia esclusa | Riduci campate o cambia schema |
| Fondazione oltre soglia | Assiale superiore al riferimento | Rivedi dati terreno e fondazione |
| Plinto non contiene pali | Griglia dei pali incompatibile | Aumenta il lato imposto o torna in automatico |
| Pila troppo snella o compressa | Soglia orientativa superata | Aumenta la sezione e verifica il modello reale |
| Sollevamento | Reazione negativa non gestita | Rivedi schema oppure analizza dispositivi dedicati |
| Prezzi o dati modificati | La graduatoria è obsoleta | Ripeti la ricerca |
| Vincitore più caro del riferimento | Il riferimento può essere escluso | Leggi le esclusioni prima di valutare il risparmio |

Non eliminare un messaggio aumentando arbitrariamente una resistenza geotecnica o azzerando una voce di costo. Correggi un dato solo quando hai una ragione documentabile. Se la combinazione resta incompatibile, conserva il rifiuto fra gli esiti dello studio.

### Esportare un confronto leggibile

Usa Esporta quantità CSV per il computo della soluzione corrente e l’export delle quote per ricostruirne dimensioni e sviluppi. Il pulsante Immagine PNG conserva il disegno; la relazione Word del progetto riporta geometria, ipotesi, quantità e avvisi. Per una consegna tecnica aggiungi il PDF e controlla che tabelle, unità e figure siano leggibili.

Accompagna ogni alternativa con obiettivo, dati del sito, blocchi attivati, griglie, listino, fattori ambientali e versione del motore. Riporta numero dei tentativi, delle geometrie ammesse e motivo della selezione. Se scegli la terza alternativa perché più costruibile, scrivilo esplicitamente: la posizione in classifica descrive soltanto il criterio scelto dal software.

### Quanto fidarsi dei risultati

La revisione documentata ha superato 44.259 asserzioni della suite generale. Una verifica indipendente, senza librerie ANTHEA nel calcolo dei valori attesi, ha controllato 100 travi con un secondo solutore FEM e quantità, costi e graduatoria di 26 geometrie di un caso imposto, con 80.133 confronti numerici.

Questi riscontri aumentano la fiducia nella correttezza delle equazioni implementate e della ricerca discreta. Non validano automaticamente le incidenze convenzionali, i costi consuntivi o la sicurezza di un ponte reale.

Prima di scegliere definitivamente verifica che la configurazione sia fisicamente costruibile, che il modello strutturale dedicato confermi le sezioni e che la valutazione geotecnica confermi le fondazioni. Aggiorna poi quantità e prezzi con le informazioni nuove e ripeti il confronto. Questo passaggio permette al predimensionamento di accompagnare il progetto senza attribuirgli prestazioni che non calcola.

## Salvataggio e report

### Conservare dati modificabili

Usare il formato proposto dal modulo per il foglio singolo e .programma per il progetto. I formati dei fogli possono variare per modulo; Bridge Design usa l'archivio .anthea. Gli archivi mantengono input e impostazioni necessarie, mentre CSV, PNG, Word e PDF sono esportazioni destinate a lettura, controllo o presentazione.

Utilizzare Salva con nome prima di una variante che si desidera mantenere indipendente. Per la storia interna di un progetto usare le revisioni. I file di progetto con revisioni utilizzano un formato aggiornato e richiedono una versione di ANTHEA compatibile; non risalvarli con un eseguibile precedente senza averne verificato il supporto.

### Report del singolo foglio e del progetto

Il report di un foglio riguarda i dati e i risultati di quel modulo. «Genera report» su progetto o sezione comprende invece i fogli diretti e tutte le sottosezioni, nell'ordine dell'albero. Include indice navigabile, dati comuni, dati specifici, risultati e immagini. I riferimenti degli antenati necessari vengono richiamati anche esportando un solo ramo.

La generazione ricalcola i fogli su copie dei dati. Le schede incomplete restano nel documento con segnalazione; non vengono trasformate in verifiche favorevoli. In presenza di conflitti si può aprire il confronto oppure proseguire conservando le segnalazioni. Prima della consegna leggere sempre le pagine degli avvisi e verificare il ramo e la revisione indicati nel titolo.

Ogni report si salva in Word e in PDF affiancati, con lo stesso nome nella stessa cartella. ANTHEA produce il PDF dallo stesso contenuto del file Word, senza usare Microsoft Word; se il PDF non può essere generato, nessuno dei due file viene scritto. Il file Word resta modificabile, il PDF è la copia da consegnare o archiviare. L'impaginazione del PDF può differire leggermente da quella che Word mostra aprendo il documento.

Un report storico ricalcola i dati della revisione selezionata con il motore della versione in uso. Per la riproducibilità conservare quindi anche versione dell'applicazione, data del report e archivio originario. Lo snapshot dei dati protegge gli input; non contiene una copia autonoma di ogni vecchio motore di calcolo.

### Ricalcolo e proposte da applicare

Dopo una modifica il foglio invalida i risultati precedenti e programma il ricalcolo. Una richiesta più recente sostituisce quella in attesa; un calcolo già avviato può terminare internamente, ma il suo risultato non sostituisce quello della revisione corrente. Chiudere una finestra di approfondimento impedisce che un risultato tardivo la aggiorni.

Le proposte di suddivisione e armatura dei tratti del palo restano esplicite. Se il foglio cambia mentre la finestra è aperta, chiudere e riaprire la proposta prima di ripetere la ricerca: così si acquisiscono dati e sollecitazioni aggiornati. L’applicazione della suddivisione conserva nello storico i tratti precedenti. Una proposta incompleta non applica soltanto una parte dei campi.

Anche il riepilogo modificabile dei parametri dei muri ricontrolla la revisione dopo la validazione. Se nel frattempo il foglio è cambiato, mantiene i dati correnti e chiede di riaprire il riepilogo.

Il report di un ramo prepara i capitoli su una copia dei dati, usando gli stessi calcoli e le stesse figure dei fogli. Non occorre visitare prima tutte le schede. Eventuali capitoli incompleti e limiti di verifica restano indicati nel report; il completamento della generazione non equivale al superamento di tutte le verifiche.

ANTHEA esegue insieme al più tanti calcoli quanti sono i processori del computer meno uno; gli altri aspettano il proprio turno, e intanto la finestra resta utilizzabile. Seguono questa regola anche la sezione in c.a., cioè verifiche, taglio, resistenze rapide, sezione σ/ε, curva M–χ, colonna modello e ricerca delle armature, e la risposta elastica del palo orizzontale. Con molte schede aperte un foglio può quindi restare per qualche istante «in attesa» o «in coda»: modificare i dati o chiudere la scheda annulla anche l'attesa. I risultati non dipendono dall'ordine in cui i calcoli partono.

### Conservare dati e risultati insieme

Il file riapribile conserva gli input e, per i progetti, le revisioni. Word, PDF e immagini documentano risultati e configurazione al momento dell'esportazione. Non sostituire il file riapribile con un'immagine della schermata.

Per esportare un ramo usa [Progetti e gestione del lavoro](wiki:guida-progetti-e-gestione-del-lavoro#report-del-ramo-e-compatibilita-degli-archivi). Nella Sezione in c.a. il report distingue gli estremi di tensione dai casi governanti: un inviluppo di massimi non è uno stato simultaneo.

## Percorso completo per un primo progetto

1. Avviare ANTHEA, creare un progetto e assegnargli un nome riconoscibile. Salvare subito l'archivio in una cartella di commessa.
2. Definire i materiali comuni e organizzare i rami prima di moltiplicare i fogli. Controllare il significato della condivisione ai livelli scelti.
3. Per un ponte preliminare aprire Bridge Design, impostare sito, larghezza e famiglia, quindi sostituire il listino. Fissare A e costruire una seconda configurazione.
4. Leggere gli avvisi, le campate effettive e il dettaglio delle quantità. Salvare la soluzione prescelta con una nota che descriva le ipotesi ancora da consolidare.
5. Creare i fogli di verifica locale necessari. Per la sezione composta assegnare geometria e azioni locali da analisi; per le fondazioni inserire indagini e dati geotecnici effettivi. Non trasferire indiscriminatamente i carichi uniformi di Bridge Design.
6. Esaminare risultati, convergenza e verifiche mancanti. Fare almeno un controllo manuale di unità ed equilibrio per ciascun tipo di modello.
7. Risolvere i conflitti dei dati comuni, creare una revisione e generare il report del ramo o del progetto. Conservare insieme archivio, report e identificazione della versione.

Questo percorso può essere abbreviato per un controllo isolato: il progetto non è obbligatorio per usare un singolo foglio. Diventa utile quando più calcoli devono restare coerenti e quando si vogliono confrontare modifiche nel tempo.

## Problemi frequenti e controlli finali

| Situazione | Interpretazione e intervento |
| --- | --- |
| Risultati assenti dopo una modifica | Completare il campo segnalato e verificare intervalli e selezioni obbligatorie |
| Risultati marcati DA AGGIORNARE | Attendere o avviare il ricalcolo previsto; non riutilizzare il valore precedente |
| Valori differiscono di 1000 | Controllare MPa e kPa, N e kN, mm e m, momenti in Nmm e kNm |
| Modifica locale genera un conflitto | Individuare il riferimento superiore che governa quella proprietà |
| Un parametro non cambia il risultato atteso | Controllare famiglia, metodo attivo, automatismi e campo di applicazione |
| File riaperto senza alcuni comandi | Controllare versione dell'eseguibile e compatibilità dell'archivio |
| Fessurazione o verifica locale non disponibile | Leggere il motivo specifico; la soluzione tensionale non garantisce la disponibilità di ogni controllo |
| Ponte con costo inatteso | Controllare larghezza totale, numero di campate, fondazioni, voci del listino e maggiorazioni |
| Curva non lineare interrotta | Controllare convergenza, incremento, stato iniziale e limite del materiale |

Prima di usare un risultato, verificare che il nome del foglio e la revisione siano corretti, che il disegno corrisponda ai dati, che azioni e segni siano coerenti, che i coefficienti siano quelli voluti e che gli avvisi siano stati letti. Prima di consegnare un report, riaprirlo e controllare titoli, unità, immagini e presenza delle conclusioni realmente supportate dal modello.

### Riferimenti per approfondire

La [guida teorica](wiki:architettura-del-calcolo-e-convenzioni) descrive i calcoli della stessa edizione. Prosegui con [Sezione in c.a.](wiki:sezione-in-calcestruzzo-armato), [Sezione composta](wiki:sezione-composta-da-ponte), [Bridge Design](wiki:bridge-design) o [Broms](wiki:capacita-orizzontale-con-broms). Fonti e criteri di tracciabilità sono descritti in [Fonti e archivio](wiki:tracciabilita-e-riferimenti). I riferimenti normativi vanno letti nelle edizioni identificate dal modulo, insieme alle relative condizioni di applicabilità.

## Muri di sostegno con stratigrafie di monte e valle

La scheda Input definisce muro, terreni e azioni; Verifiche raccoglie gli esiti locali e globali. Prepara separatamente stratigrafie delle spinte, terreno di fondazione e profilo profondo della stabilità globale. Un modello completo per una verifica può essere insufficiente per le altre.

Le stratigrafie di monte e valle sono affiancate, indipendenti o collegate per spessori e proprietà; le profondità partono dalle rispettive superfici. Hlib è la distanza dalla sommità al terreno di valle: Dv=H+t−Hlib. Il terreno davanti al muro entra nei pesi, nei momenti, nelle sollecitazioni della mensola e nel ricoprimento efficace della portanza. La passiva richiede attivazione e frazione mobilitata, non oltre 0,5: un documento con un valore maggiore è rifiutato con il motivo. È esclusa nel sisma. Gli attriti del muro e della fondazione sono assegnabili oppure ricavati da φcv,k e tipo di interfaccia. Il valore a volume costante va caratterizzato, senza sostituirlo automaticamente con quello di picco.

Valori di calcolo consente di interrogare e modificare gli input e leggere coefficienti effettivi per combinazione, pesi, attriti, pressioni e sollecitazioni. Le forze risultanti dipendono dagli input e non si possono forzare. Le combinazioni sono modificabili: il preset locale è A1+M1+R3, quello globale A2+M2+R2. Con la mensola a monte le righe con il sovraccarico variabile hanno la gemella «b»: la colonna «q var. mensola» non spuntata lascia il sovraccarico nella spinta e lo toglie dalla mensola. La colonna «γR,e passiva» riporta il coefficiente della passiva; nella combinazione selezionata e nei valori di calcolo si leggono Ed e resistenze di scorrimento e ribaltamento. Azioni eccezionali e sisma Mononobe–Okabe o Wood semplificato sono espliciti. Il terreno del lato selezionato può essere trasferito ai moduli dei pali; la sezione selezionata può essere inviata al modulo c.a. Nelle revisioni in sola lettura la finestra Valori di calcolo si consulta soltanto: le righe sono bloccate e Applica e ricalcola è disattivato.

Le combinazioni generate per il muro comprendono righe sismiche ed eccezionali a gruppi: una con le azioni variabili e le altre senza uno o più gruppi variabili, perché nel sisma e nelle eccezionali le variabili entrano con ψ2·Qk solo se sfavorevoli. Il numero delle combinazioni cresce di conseguenza e la numerazione delle righe dopo la prima cambia rispetto alle versioni precedenti; i rapporti governanti si leggono come sempre nella tabella delle verifiche.

Un documento dei muri della prima versione si aggiorna alla seconda versione con le combinazioni automatiche sia quando si apre sia quando si calcola senza aprirlo, per esempio nel calcolo del progetto: i tre carichi diventano un gruppo correlato con ψ1 e ψ2 del documento e ψ0 = 0,7, oppure ψ0 = 1 se ψ1 supera 0,7, come nella categoria E della tabella 2.5.I delle NTC 2018. Con un solo gruppo ψ0 non entra nelle combinazioni; conta solo se si aggiungono altre azioni variabili. Le combinazioni hanno i nomi della seconda versione («SLE 1», «SLU 4», «Sisma kv− 12», …). Senza sisma i rapporti governanti sono quelli di prima; con il sisma le righe sono di più e usano i γR della tabella 7.11.III, quindi dove governa una riga sismica i rapporti di scorrimento e di ribaltamento scendono, fino al 9 % e al 13 %. I documenti con ψ1 maggiore di 0,7, che le versioni precedenti aprivano come non validi, ora si aprono.

La stabilità globale Bishop ha un motore separato e un proprio profilo esteso, anche con due colonne profonde e confine verticale assegnato. Il pulsante Stabilità globale apre il percorso; al primo accesso a un profilo vuoto ne prepara i dati e attiva la verifica. In una revisione in sola lettura apre la pagina e le schede senza preparare il profilo. Gli strati si inseriscono per spessore, con quota del fondo calcolata automaticamente. La precompilazione non prolunga le indagini: rilievo, terreni profondi e falda del sito vanno controllati e confermati. Il disegno rappresenta anche il terreno sotto il piano di posa. Parametri, dominio e combinazioni restano interrogabili e modificabili.

Conserva nel report le due stratigrafie e i coefficienti effettivi. Per le verifiche aggiuntive consulta [Portanza, cedimenti e armature](wiki:guida-portanza-sismica-cedimenti-spostamenti-e-armature-rev07).

### Materiali e copriferro del muro

La scheda Materiali del muro usa gli stessi cataloghi GPC del verificatore delle sezioni in calcestruzzo. Selezionare la classe del CLS e dell'acciaio; le proprietà di catalogo sono protette. Personalizzato consente di assegnare resistenze, modulo dell'acciaio, deformazione ultima e nome. Il diagramma costitutivo e i coefficienti αcc, γc e γs sono interrogabili; fcd, fyd ed Ecm mostrano i valori effettivi. Il trasferimento Apri sezione in c.a. conserva questi dati.

Nei progetti, le schede Calcestruzzo e Acciaio del ramo forniscono i materiali al muro secondo le normali regole di ereditarietà. Il confronto dei dati comuni include esposizione, aggregato, vita utile, tolleranza e scelte di durabilità. Il copriferro adottato è condivisibile con le sezioni e viene confrontato con il minimo della scheda Materiali, usando il diametro massimo effettivamente presente nel muro. Adotta copriferro minimo applica esplicitamente la proposta. Il controllo non aumenta silenziosamente il copriferro e non modifica geometria, carichi o quantità di barre.

Gli archivi precedenti mantengono le resistenze e il legame dell'acciaio con cui erano stati calcolati; l'apertura non li riclassifica automaticamente come B450C. Se l'acciaio salvato ha il nome di un materiale del catalogo ma valori diversi, per esempio B450A con εu = 30 ‰ contro i 25 ‰ del catalogo, la scheda Materiali lo segnala sotto «Acciaio da normativa»: il calcolo usa i valori salvati; riselezionare il materiale per adottare quelli del catalogo. Nei muri a gravità le proprietà del CLS sono attive quando si sceglie Calcestruzzo non armato; la muratura non eredita proprietà meccaniche del CLS o dell'armatura non utilizzate.

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

## Portanza, cedimenti e armature dei muri: procedura

Le verifiche usano modelli distinti. Prima di attivarle completa geometria, stratigrafie, falda e combinazioni del muro. Un risultato favorevole a scorrimento non sostituisce portanza, cedimenti, stabilità globale o verifica strutturale.

### Scegliere il controllo

| Domanda | Dati da controllare | Risultato da leggere |
| --- | --- | --- |
| Portanza della fondazione | Terreno di posa, carico verticale efficace, eccentricità, inclinazione e sisma | Campo di applicabilità e resistenza per combinazione |
| Cedimento | Parametri deformativi e modello del terreno, combinazione di esercizio | Spostamento e ipotesi, separatamente dalla resistenza |
| Spostamento permanente | Accelerogramma e parametri Newmark richiesti | Spostamento accumulato, soglia e unità temporali |
| Armature | Materiali, sezioni critiche, sollecitazioni, copriferro e disposizione | Proposta, controlli eseguiti e condizioni incomplete |
| Muro a gravità | Materiale, peso e resistenze pertinenti | Equilibrio e verifiche coerenti con calcestruzzo o muratura |

### Procedura e lettura

Apri i controlli pertinenti nella scheda Verifiche e leggi le ipotesi prima dell'esito. Inserisci i dati mancanti; non sostituire un parametro deformativo con un parametro di resistenza. Per il sisma distingue i coefficienti delle spinte da quelli del terreno di fondazione e della stabilità globale.

Calcola armature produce una proposta da controllare, non il dettaglio esecutivo completo. Verifica le sezioni critiche, l'armatura disponibile e le condizioni non coperte. Se trasferisci una sezione al modulo c.a., controlla segni, assi e combinazione del foglio risultante.

Conserva nel report la combinazione, i parametri effettivi e l'eventuale stato fuori campo. Consulta [modelli di portanza, cedimenti e armature](wiki:portanza-sismica-cedimenti-spostamenti-e-armature-rev07) per formule e limiti e [stabilità globale](wiki:muri-di-sostegno-e-stabilita-globale) per il modello Bishop.

### Dati della portanza sismica

In Terreno aprire Portanza sismica. Con Da sito si usa ah/g=ag/g·Ss·St, prima della riduzione β del muro; av/g=±0,5ah/g. In alternativa assegnare entrambe le accelerazioni. Il fattore γRD è modificabile: 1 per sabbia medio densa, 1,15 per sabbia sciolta asciutta. Non è un valore ricavato automaticamente dal solo angolo di attrito.

In Verifiche scegliere una combinazione SISMA e Portanza sismica nel riepilogo. Sono leggibili Nmax, F, N̄, V̄, M̄, limite verticale, interazione, tasso ed esito. Un’accelerazione mancante, un terreno fuori campo o una risultante non ammissibile restano esplicitamente non verificati.

### Dati dei cedimenti

In Terreno attivare Calcola cedimenti finali e inserire, a partire dal piano di posa, nome, spessore e modulo edometrico M di ciascuno strato. M è espresso in kPa: per esempio 30 MPa corrispondono a 30000 kPa. Non viene dedotto da φ o riempito con un valore presunto. La pressione del terreno rimosso è il carico geostatico eliminato con lo scavo, da valutare nel modello scelto; zero è una scelta esplicita.

Limiti iniziali modificabili: 25 mm per cedimento, 0,002 rad per rotazione e 20 mm per spostamento in testa. Sono valori di avvio da valutare per l’opera, non limiti normativi universali. In Verifiche scegliere Cedimenti e spostamenti per la tabella per combinazione, i contributi degli strati e le curvature.

### Importare una storia Newmark

In Azioni aprire Spostamenti permanenti e aggiungere una storia. Scegliere SLD o SLV, inserire ky/g, fattore di scala e limite di spostamento. ky/g è la soglia di inizio scorrimento del muro, da ricavare da un’analisi di equilibrio: non coincide con ag/g e non viene dedotta automaticamente dal coefficiente kh.

Importare un CSV a due colonne separate da punto e virgola: tempo in secondi e accelerazione verso valle in g. È ammessa una prima riga t;a_g e il separatore decimale italiano. I tempi devono essere crescenti. Confermare che storia e scala siano compatibili con sito e stato limite. I campioni restano salvati nel file del muro.

### Disporre e proporre le armature

In Geometria si possono mantenere le facce simmetriche o assegnare due armature indipendenti. La prima faccia è monte nel fusto e inferiore nelle solette; la seconda è valle nel fusto e superiore nelle solette. Rimangono disponibili le due zone verticali separate da h₁.

Ogni zona contiene barre principali, diametro e passo delle secondarie, lunghezza di ancoraggio, sovrapposizione e mandrino. Zero nelle lunghezze significa calcolo automatico, non lunghezza nulla. Il pannello dei dettagli espone aggregato, aderenza, vita nominale, tolleranza del copriferro e collegamenti della giunzione.

Calcola armature cerca diametri e numeri interi di barre entro i limiti impostati. Ogni candidato viene controllato con GPC a N–M, a taglio e in SLE; la proposta usa armature simmetriche per zona, poi modificabili. L’area stimata dalla flessione serve soltanto a scartare candidati impossibili. La verifica finale include i dettagli: una sezione resistente può avere una piega o una giunzione che non entra. In tal caso l’esito lo segnala e può occorrere aumentare lo spessore. La ricerca è interrompibile. Premere Applica proposta per sostituire le barre inserite; prima di applicare restano conservate.

In Vista dei risultati scegliere Armature: si vedono i percorsi delle barre, le pieghe, la fascia di sovrapposizione e i numeri delle posizioni della tavola. Le barre giuntate sono affiancate lungo lo sviluppo del muro; le proiezioni sono leggermente distanziate sul disegno per leggibilità. Dettagli armature riporta per posizione e zona la faccia, le barre, l'area, la lunghezza, lbd e l0 richiesti e usati, il mandrino, fbd e tutti i controlli. I pesi sono stime per metro comprensive di ancoraggi, giunzioni e secondarie; sagome e quantità del tratto sono nella tavola delle armature. Restano da definire i giunti di costruzione, le interferenze tridimensionali e gli sfridi.

### Tavola delle armature e distinta ferri del muro

In Input, sotto le armature, aprire Distinta ferri del tratto di muro. Inserire la lunghezza reale del tratto in «Lunghezza del tratto da armare», il copriferro alle sue estremità e la lunghezza commerciale delle barre. La lunghezza del tratto cambia soltanto il computo: le verifiche strutturali rimangono riferite a una striscia di 1 m. Con due zone, indicare anche gli spilli per fila e per metro di muro.

Dopo il ricalcolo, «Tavola armature e distinta ferri…» nel riquadro di input oppure «Distinta ferri…» in Verifiche apre la tavola delle armature del tratto di muro lungo quanto «Lunghezza del tratto da armare»; la finestra ha selezione della pagina e zoom. La prima pagina è la sezione trasversale con tutte le posizioni, numerate nell'ordine del disegno (pos. 1, 2, …): le verticali del fusto (blu a monte, rosse a valle), le barre di fondazione con i risvolti ai bordi liberi, la forcella in testa al fusto, gli spilli della giunzione e i ferri longitudinali, disegnati come punti neri nella loro posizione. Ogni posizione ha un richiamo: «pos. 1 Ø16/16,7» è una barra Ø16 a passo 16,7 cm; «pos. 9 21 Ø10» sono 21 ferri longitudinali Ø10. A destra il riepilogo per diametro (lunghezza totale, kg/m e peso), il peso totale, il volume di calcestruzzo del tratto e l'incidenza in kg/m³.

Le pagine seguenti sono la distinta ferri, sei posizioni a pagina: Pos., sagoma con le lunghezze dei tratti in cm, Ø, numero dei pezzi nel tratto, lunghezza unitaria in cm, lunghezza totale in m e peso in kg. Le sagome non sono in scala: le barre alte del fusto sono disegnate coricate e i tratti corti ingranditi. Le quote sono all'asse della barra; gli archi delle pieghe sono sviluppati e compresi nella lunghezza unitaria, che è la somma dei tratti arrotondati al cm. Accanto al tratto inclinato della barra di valle compare l'angolo della piega della scarpa. Per i ferri longitudinali più lunghi della barra commerciale la sagoma riporta il numero di pezzi per filo e la sovrapposizione l₀. L'ultima pagina riporta i criteri e gli avvisi; resta l'avviso quando le verifiche del muro non sono tutte soddisfatte.

Il peso è netto, senza sfridi. «Sviluppo spillo (0 = calcolato)»: con 0 la sagoma e lo sviluppo degli spilli sono calcolati; un valore diverso da zero è una forzatura. Le stesse informazioni sono nel Word, nel PDF, nei PNG e nel CSV (con le righe RIEPILOGO, TOTALE, CALCESTRUZZO e INCIDENZA): PDF completo esporta tutte le pagine in A3 orizzontale, Word completo aggiunge tabelle modificabili, CSV distinta conserva posizioni, impostazioni e note, PNG pagina esporta la pagina visualizzata. Anche la relazione Word del muro contiene la distinta del tratto e la tavola. Se si modificano i dati occorre attendere il nuovo calcolo prima di esportare.

I ferri longitudinali più lunghi della barra commerciale si dividono in pezzi uguali con la sovrapposizione l₀; le barre di fondazione che non stanno in una barra commerciale diventano due, una per mensola, prolungate oltre la radice per l'ancoraggio. Una posizione che resta più lunga della barra commerciale, per esempio una verticale alta del fusto, è segnalata: il programma non introduce giunti nelle barre trasversali. Se la mensola dalla parte opposta non contiene il piede di una verticale, la barra piega dalla sua parte e la distinta lo segnala. Un computo completo non equivale a verifiche soddisfatte: gli esiti mancanti o sfavorevoli restano indicati nell'ultima pagina.

![Prima pagina della tavola delle armature del muro con due zone e tratto di quattro metri: sezione trasversale con le posizioni, riepilogo per diametro e incidenza](../../src/ANTHEA.Desktop/Assets/Wiki/muri-tavola-armature.png)

Esempio riapribile: supporto/artefatti/muri-materiali-distinta-20261005/interfaccia/distinta-due-zone.anthea. Il muro ha H=3 m, due zone con cambio a 1,5 m e un tratto lungo 4 m. La tavola ha 14 posizioni: le quattro verticali del fusto, le barre inferiori e superiori della fondazione, la forcella, gli spilli della giunzione con la sagoma calcolata e sei file di ferri longitudinali; peso totale 847,1 kg, 9,30 m³ di calcestruzzo, incidenza 91 kg/m³. L'esempio illustra input e computo, non costituisce un muro esecutivo verificato.


## Efficienza orizzontale della palificata

Il modulo Palificata orizzontale confronta sei modelli di effetto di gruppo a partire da un'unica geometria di pali identici e da una direzione dell'azione orizzontale. Il risultato è un fattore di riduzione: per Davisson riguarda il modulo di reazione kh o nh; per gli altri metodi è il p-multiplier medio. La scheda non esegue un'analisi laterale completa e non fornisce direttamente la resistenza della palificata.

### Apertura e geometria

Aprire Moduli singoli → Geotecnica → Palificata orizzontale, oppure File → Nuova palificata orizzontale. La scheda può essere inserita anche in un progetto. Il motore numerico è GPCChecker.Geotechnics; ANTHEA gestisce input, risultati, rappresentazione e archivio.

Scegliere in alto a sinistra Rettangolare, Quinconce, Triangolare, Pentagonale, Esagonale oppure Generica. Gli input sottostanti generano automaticamente la disposizione: Nx/Ny e Sx/D/Sy/D per le file; numero di pali sul lato e S/D per il triangolo; anelli, suddivisioni, centro e S/D per i poligoni. Quinconce alterna file di N e N−1 pali con sfalsamento di mezzo interasse. Il triangolo è pieno; i poligoni usano anelli concentrici con suddivisioni crescenti. Le coordinate sono modificabili solo in Generica. X e Y sono attive inizialmente; X− (180°), Y− (270°) e la direzione personalizzata sono facoltative e inizialmente disattivate. Ogni direzione ha risultati e interassi rappresentativi distinti; invertire H può cambiare i coefficienti dei singoli pali anche quando la media della palificata simmetrica resta uguale. Il suo angolo è antiorario da +X. La rotazione della geometria è un input distinto. Sono ammessi da 1 a 500 pali, identificativi univoci e distanze fra centri almeno pari a D.

La pianta mostra pali, coefficienti del metodo selezionato, direzione H, assi parallelo e ortogonale, diametro e interassi disponibili. La fila 1 è quella con maggiore proiezione lungo il verso del carico. Invertendo H si scambiano file anteriori e posteriori. La geometria non viene ruotata quando cambia soltanto il carico.

### Metodo e applicabilità

| Metodo | Risultato | Impiego nella scheda |
| --- | --- | --- |
| Davisson 1970 | Rg di kh/nh | Griglia allineata; interasse parallelo almeno 3D, trasversale almeno 2.5D |
| AASHTO 2014 | p-multiplier per fila | Interpolazione 3D–5D; oltre 5D mantenimento del valore di 5D segnalato |
| FHWA 2018 | p-multiplier per fila | Interpolazione 3D–6D; oltre 6D coefficienti unitari |
| Rollins e FEMA | p-multiplier per fila | Formule logaritmiche; campo sperimentale circa 3.3D–5.65D |
| Reese e Van Impe | p-multiplier palo per palo | Tutte le coppie, anche per geometrie irregolari e H obliqua |
| Caltrans 2025 | p-multiplier modificato | Interazione palo-palo e correzione alfa per fila |

Il riconoscimento automatico accetta una griglia cartesiana completa e uniforme nel riferimento del carico, anche se ruotata rispetto agli assi globali. Le file vengono distinte con tolleranza D per 10 alla meno 6. Per geometrie irregolari o H obliqua, i metodi per file non vengono applicati automaticamente. Aprire Applicabilità e interassi rappresentativi e, solo dopo una valutazione ingegneristica, accettare l'estensione per file proiettate e assegnare S parallelo e S trasversale. Per Caltrans, accettata l'estensione, la media degli interassi fra file proiettate è disponibile automaticamente; si può assegnare un interasse rappresentativo diverso. L'estensione resta identificata negli avvisi.

I due interassi rappresentativi vuoti mantengono il comportamento automatico. Non sono dimensioni che modificano la pianta: cambiano soltanto la schematizzazione per file. Reese usa sempre le distanze reali fra tutti i pali. Il riepilogo elenca anche gli intervalli geometrici proiettati, per rendere verificabile la scelta.

Consenti estrapolazioni è disattivato inizialmente. Abilitarlo permette di ottenere un risultato fuori dai limiti inferiori delle tabelle o dal campo sperimentale di Rollins, con avviso esplicito e coefficienti limitati a zero–uno. Non supera la condizione trasversale di Davisson: sotto 2.5D il metodo resta non disponibile, perché manca una correzione documentata. Per un solo palo tutti i fattori valgono uno. Per un'unica fila trasversale AASHTO, FHWA e Rollins richiedono un interasse parallelo rappresentativo; preferire Reese quando manca una schematizzazione per file giustificabile.

### Risultati e confronto

Il ricalcolo è automatico per tutti i sei metodi e tutte le direzioni attive. Selezionare una riga del confronto per vedere sulla pianta il metodo e la direzione corrispondenti. Il riepilogo mostra minimo, media, massimo e riduzione media. La legenda va da 0 rosso, riduzione 100%, a 1 verde, nessuna riduzione. Il basamento è il contorno convesso esterno oppure un rettangolo; il margine asse–bordo è espresso in multipli di D, inizialmente 1D, equivalente a 0,5D libero dal palo. Il basamento è solo rappresentativo e non modifica i coefficienti. Un trattino significa grandezza non definita, non valore nullo.

La tabella di confronto indica separatamente grandezza, valore e indisponibilità. Selezionare ciascun metodo per leggere fonte, avvisi e dettaglio dei pali. Il dettaglio riporta fila, coordinate proiettate, beta, alfa e coefficiente finale. Alfa riguarda la modifica Caltrans; per gli altri p-multiplier vale uno. I coefficienti di Davisson non devono essere letti come p-multiplier.

Esempio di controllo: griglia 3 per 3, D = 1 m, Sx = Sy = 3 m, H = 0 gradi. Davisson restituisce Rg = 0.25; AASHTO restituisce Pm medio = 0.50; FHWA restituisce circa 0.5167. Rollins a 3D richiede l'abilitazione esplicita dell'estrapolazione e restituisce circa 0.5887. Questi valori non sono quattro stime equivalenti della capacità del gruppo.

Le modifiche invalidano i risultati precedenti; attendere il ricalcolo automatico prima di esportare. Salva conserva coordinate, opzioni, metodo e interassi rappresentativi nel normale archivio ANTHEA. Risultati JSON esporta il confronto completo. Report Word include geometria, risultati per metodo e per palo, fonti e limiti; può essere convertito in PDF con il normale flusso documentale. Le due guide globali sono distribuite anche in PDF.

## Risposta elastica del palo orizzontale

Il modulo Palo orizzontale comprende la scheda Risposta elastica · trave su molle. Calcola deformazioni e sollecitazioni sotto la forza assegnata con il motore ElasticPile di Checker. La scheda Dati comuni e capacità · Broms / stratificato conserva le verifiche di capacità limite. Le due analisi usano gli stessi input di geometria, carico e terreno; i risultati restano distinti. Il percorso è Moduli singoli → Geotecnica → Palo → Orizzontale; la risposta è disponibile anche nel Micropalo orizzontale.

### Dati iniziali condivisi

Compilare geometria, materiale, carico, falda e stratigrafie nella scheda iniziale. Ogni dato ha un solo punto di modifica. Nella scheda elastica rimangono la scelta esplicita della stratigrafia analizzata, il passo FEM e il vincolo alla punta. Modifica dati e strati richiama l'editor iniziale.

La lunghezza del campo principale rimane la lunghezza infissa. Il tratto libero è una quantità aggiuntiva: la lunghezza totale del modello è infissa più libera. L'eccentricità condivisa indica la quota della forza sopra il piano campagna, come nel modulo precedente. Checker ricava il momento equivalente alla testa C = H (Llibero − e_da_pc), nella convenzione con θ = dy/dx: il braccio del tratto libero non viene contato due volte. Con tratto libero nullo ed eccentricità positiva il momento interno al piano campagna è H e. Non è presente un secondo campo modificabile C nella scheda elastica. Con la testa sotto il piano campagna, d dei dati iniziali, il tratto libero è nullo: gli strati sopra la testa non entrano nel modello, x parte dalla testa e z = d + x dal piano campagna, anche nelle leggi di kh che crescono con la profondità.

EJ viene mostrato in sola lettura con E, J, unità e origine. Per il palo in calcestruzzo si usa la sezione circolare integra lorda, con Ecm del materiale: sono esclusi il contributo aggiuntivo delle armature, fessurazione e viscosità. Per il micropalo CHS si usa il solo tubo di acciaio; il contributo della malta non è incluso. Il modulo elastico del tubo è nei dati iniziali di sezione. L'opzione avanzata Override EJ, nello stesso editor, richiede valore e motivazione; conserva anche il valore geometrico di base. Il momento resistente My della verifica Broms non sostituisce EJ.

La testa può ruotare liberamente oppure avere rotazione impedita; lo spostamento orizzontale rimane libero. La punta è libera per impostazione iniziale; cerniera e incastro richiedono una scelta esplicita. Carichi negativi invertono i segni della risposta elastica; la capacità Broms mantiene i propri limiti di applicabilità.

### Parametri nella riga dello strato

La tabella iniziale contiene anche i parametri della risposta orizzontale. Strato e terreno rimangono riconoscibili durante lo scorrimento orizzontale interno. Nella stessa riga si scelgono addensamento o categoria, determinazione e autore; minimo e massimo della fonte sono in sola lettura, il valore adottato è modificabile. Info della riga riporta origine, condizioni, bibliografia e motivazione dell'eventuale override.

Per un nuovo terreno granulare la modalità iniziale è A γ/1,35. Sciolto usa A 100–300, media 200; Medio 300–1000, media 650; Denso 1000–3000, media 2000. A è adimensionale. I valori consigliati nel libro restano separati: 200, 600 e 1500. La modalità nh direttamente da tabella 14.5 mostra nella riga i valori non immerso / immerso e usa la falda condivisa; nh* abilita un override esplicito in kN/m³ con motivazione. Il valore adottato da una tabella in N/cm³ viene convertito in Checker.

Per i coesivi scegliere categoria e singolo autore della tabella 14.6. Le categorie filtrano le righe bibliografiche; una categoria cambiata richiede una teoria pertinente prima del calcolo. Per l'argilla n.c. Reese–Matlock il valore iniziale è 1,85 N/cm³, media di 0,2 e 3,5. Inserire 1,0 registra una scelta manuale. Argilla sovraconsolidata consente kh costante o k distribuito assegnato. Le modalità manuali restano disponibili. I valori manuali non sono sovrascritti dal ricalcolo; se cambia la fonte occorre scegliere se mantenerli o adottare la nuova media. Una media ancora intatta si aggiorna alla nuova riga.

### Sforzo normale e lettura dei diagrammi

Il peso unitario adottato è nei dati iniziali della sezione. Il valore iniziale del c.a. è 25 kN/m³ e include già l'acciaio. Per CHS si usa il peso dell'anello di acciaio; l'iniezione è esclusa inizialmente (peso unitario zero) e può essere inclusa esplicitamente sull'area restante. Il peso unitario e N in testa devono essere coerenti con il livello delle azioni usate per la verifica. Non vengono applicati automaticamente coefficienti alle azioni.

I pannelli sono ordinati: profilo, k, y, θ, q, N, V, M, tratti. La scala verticale e la selezione della quota sono comuni. N è positivo a compressione e cresce del peso tra testa e quota considerata. Non sono modellati trasferimento assiale al terreno, galleggiamento o secondo ordine. I passi degli assi seguono valori 1, 2, 5 per potenze di dieci; i valori selezionati conservano la precisione numerica. Alle interfacce il clic ripetuto legge i due lati.

### Materiali e parametri per strato

Il passo iniziale FEM predefinito è 0,50 m, modificabile nelle opzioni della risposta elastica. I nodi alle interfacce, alla falda quando cambia la legge e ai confini dei tratti restano obbligatori. La verifica di convergenza usa internamente una mesh più fitta. Riaprendo un archivio si conserva il passo già assegnato esplicitamente.

Nel pannello Momento plastico resistente scegliere i materiali dalle tendine Calcestruzzo e Acciaio. Nei nuovi fogli del palo è preselezionato B450C con tutte le proprietà del catalogo, come nel modulo della sezione in c.a.; i materiali personalizzati degli archivi esistenti rimangono conservati. Il riferimento Motore di calcolo: GPC Engine è in alto a destra del modulo, come nella scheda della sezione in c.a., ed è visibile per entrambe le analisi. Le proprietà numeriche sono raccolte nei dati opzionali, chiusi inizialmente, insieme a esposizione, vita utile, qualità del copriferro e legami. Gli archivi personalizzati conservano i valori precedenti. N e MRd sono sotto il disegno della sezione. Le opzioni di resistenza manuale ed EJ rimangono accessibili in un pannello chiuso.

Coefficienti unitari è disattivato inizialmente. Attivandolo si usa una configurazione custom della normativa di sezione con tutti i fattori gamma e alpha_cc pari a 1. Il foglio c.a. di approfondimento eredita la stessa normativa: anche taglio, torsione, ancoraggi, dettagli costruttivi e primo snervamento della curva M–χ usano γc = 1 e fyd = fyk. Disattivandolo si recuperano i coefficienti ordinari conservati nell'archivio. Il comando riguarda la resistenza strutturale: non modifica i fattori geotecnici di Broms. La modalità custom compare nei risultati e nel report.

Le colonne della stratigrafia sono Addensamento, Determinazione, Parametro, Minimo, Massimo e Adottato. Per i granulari si scelgono Sciolto, Medio o Denso; per i coesivi la categoria riporta il terreno effettivo della tabella 14.6. Gli autori sono filtrati per quella categoria. Il pulsante Tabelle nella riga mostra integralmente i cataloghi 14.5 e 14.6 usati da Checker, con unità, intervalli, media software e valori consigliati bibliografici distinti. Cambiare categoria aggiorna una media automatica; un valore manuale è conservato e richiede la conferma della nuova fonte. Il cambio tra granulare e coesivo conserva le scelte precedenti nello storico e propone una modalità compatibile.

### Avanzamento e controlli da completare

La barra indica analisi FEM e convergenza, quindi sezione in elaborazione e coppie MRd positivo/negativo completate rispetto agli N distinti locali. I valori già disponibili nella cache sono conteggiati. Proponi e Dimensiona sono disponibili soltanto con verifiche aggiornate. Anche Dimensiona mostra disposizione in esame e avanzamento della relativa ricerca MRd. Un errore indica la causa e interrompe la barra, senza esportare risultati obsoleti.

Il riepilogo di ogni tratto elenca sempre i controlli non soddisfatti, quelli da completare e quelli esclusi. Il comportamento iniziale è Pilastro, modificabile in Trave oppure Solo controlli comuni nelle ipotesi. È una scelta esplicita dei dettagli da controllare. I minimi specifici dei pali rimangono separati. Esposizione, resistenza del calcestruzzo, vita utile e qualità del copriferro provengono dalla sezione principale; il cmin,dur è calcolato con Checker. Un valore manuale precedente viene conservato come override esplicito. Il foglio c.a. aperto dal tratto riceve esposizione e comportamento impostati.

Fare clic a una quota sul profilo o sui diagrammi: una linea arancione identifica il taglio. La sezione trasversale mostra x dalla testa, z dal piano campagna e il lato dell'interfaccia; la selezione resta invariata dopo il ricalcolo delle armature. Senza clic non viene mostrata una quota implicita. Nel disegno la staffa è verde con richiamo del diametro e assi u/v; ganci e chiusure non sono dettagli esecutivi verificati.

La proposta cerca il primo attraversamento discendente di metà del massimo assoluto del momento, dopo l'ultimo massimo. La quota effettiva è quella realizzabile più vicina su griglia di 0,5 m, con tratti almeno di 3 m e sviluppo entro le barre commerciali assegnate. Oltre il cambio è predisposta una sezione personalizzabile con armatura iniziale copiata dalla principale: usare Dimensiona o modificarla e controllare gli esiti. In assenza di attraversamento realizzabile resta una proposta costruttiva senza riduzione automatica. Quota teorica, quota proposta e criterio sono salvati.

### Tratti colorati e verificatore della sezione

Le schede dei tratti mostrano quote iniziale e finale, lunghezza, origine dell'armatura, numero e diametro delle barre, diametro e passo delle staffe. Ogni tratto ha un colore, ripreso nel pannello verticale dei diagrammi. A destra, il riquadro Sezione e verifiche permette di selezionare il tratto e il controllo: N–M, taglio, interferro, minimi, copriferro, sviluppo oppure distinta. Gli esiti mancanti restano indicati come da completare. Un esito N–M–V favorevole non certifica automaticamente i dettagli costruttivi.

Apri verificatore c.a. apre il modulo esistente con la sezione selezionata. Le azioni iniziali provengono dalla quota critica e rimangono modificabili nella scheda di approfondimento; N è negativo a compressione in questo foglio. Le combinazioni proprie sono conservate alla chiusura e riutilizzate alla riapertura. Geometria e materiali vengono riallineati al palo all'apertura. Il comando Applica armatura trasferisce esplicitamente soltanto numero/diametri delle barre e diametro/passo delle staffe. Sul primo tratto aggiorna la sezione principale; sugli altri crea un'armatura personalizzata. Le azioni inserite nel foglio c.a. non modificano i carichi del FEM.

Dimensiona espone quattro cataloghi modificabili: quantità di barre, diametri longitudinali, diametri delle staffe e passi. Checker ordina per area longitudinale crescente, quindi per quantità di staffe per metro; la prima disposizione accettabile è verificata con N–V–M concomitanti a tutte le ascisse. Interferro e minimi applicabili partecipano alla scelta. Il copriferro usa cmin,dur calcolato dalla durabilità condivisa o un override esplicito; con dati mancanti rimane da completare.

Proponi suddivisione usa quote su griglia di 0,5 m, mantenendo esatta la punta, e non genera tratti inferiori a 3 m. Considera domanda, sviluppo delle barre e lunghezze commerciali preferite 6/8/10/12 m, comprensive della sovrapposizione. Se i vincoli sono incompatibili presenta una spiegazione, senza introdurre tratti corti. La proposta deve essere applicata esplicitamente e conserva nell'archivio i tratti sostituiti. Le quote e il numero dei tratti restano modificabili.

La sovrapposizione iniziale è 60φ, arrotondata per eccesso al decimetro, per convenzione di ANTHEA; non è attribuita a una norma. Il riquadro distingue lunghezza iniziale, richiesta dal verificatore, adottata ed effettiva. Per ogni giunto si adotta il massimo fra lunghezze iniziali e richieste delle armature collegate. La barra entrante risale di questa lunghezza; la barra superiore termina alla fine del suo tratto. φ24 dà 1,44 m, arrotondati a 1,50 m. Il pulsante Distinta ferri di ogni tratto apre la distinta e la seleziona nel riepilogo a destra. Barre e staffe sono visibili inizialmente; la scelta di richiudere il pannello viene conservata durante il ricalcolo. I testi si aggiornano anche mantenendo il cursore in un campo. Quando la distinta non può essere prodotta, la vista delle armature e il tratto mostrano il motivo specifico, senza un riquadro bianco o un’attesa permanente. La distinta di ciascun tratto indica il proprio gruppo, quantità, diametro, quote fisiche, lunghezza di taglio, barra commerciale e sovrapposizione. Ogni pezzo è conteggiato una sola volta; i giunti richiamano i pezzi collegati senza aggiungere altre quantità. Le staffe riportano diametro, passo massimo, quantità e quote nominali, assegnando il confine al tratto successivo senza doppi conteggi; forma di chiusura, sviluppo e distinta esecutiva delle staffe restano da definire.

### Approvazione delle ipotesi ed esito delle armature

In Tratti di armatura, Approva tutto · ipotesi conferma in un solo passaggio azioni di progetto, modello di taglio circolare, trattenimento delle barre compresse, dettagli delle estremità e buona aderenza. I valori numerici, le armature, i coefficienti e i vincoli della proposta restano quelli assegnati. Il pulsante Approva tutto · sisma, disponibile quando la modalità sismica è attiva, conferma anche le ipotesi ordinarie, la provenienza sismica di N e V e il momento elastico non ridotto con N concomitante. Le singole conferme rimangono modificabili. Il comando registra una scelta dell'utente e avvia il ricalcolo delle verifiche; non sostituisce un esito negativo con uno positivo.

Il riepilogo a destra indica quanti tratti sono non verificati, incompleti o hanno tutti i controlli eseguiti soddisfatti. Selezionando un tratto, il riquadro rosso segnala controlli non soddisfatti; quello ambra indica dati o verifiche da completare; quello verde vale esclusivamente per i controlli eseguiti. Le verifiche fuori dal perimetro sono elencate separatamente. Ancoraggi insufficienti, giunti non utilizzabili, resistenze mancanti e controlli sismici non soddisfatti restano riconoscibili anche dopo Approva tutto. Cambiare un input invalida subito gli esiti; la risposta FEM viene riutilizzata quando i dati fisici non cambiano.

NTC §7.2.5 apre una finestra interna ad ANTHEA con la sintesi dei criteri e una seconda scheda contenente la pagina originale consultata, PDF 217 / stampata 213. Zoom e scorrimento consentono di leggerla anche senza Internet. Non viene aperto un browser.

L'Armatura trasversale principale può essere Staffe singole, Spirale oppure Da definire. I tratti collegati ereditano la scelta; i tratti personalizzati possono scegliere una tipologia diversa nella propria riga. Gli archivi conservano le scelte precedenti; per nuovi dati il tipo iniziale è Staffe singole. Diametro e passo rimangono quelli della sezione o del tratto. La spirale è rappresentata in elevazione e nella vista laterale; la distinta riporta la marca SP, il numero di spire, il passo effettivo e la lunghezza geometrica. Le staffe hanno marca S e quantità di anelli. Le note riportano anche i diametri, il passo massimo e le quote.

La lunghezza geometrica non comprende ganci, chiusure, ancoraggi, giunti o sfridi e non è una lunghezza di taglio esecutiva. Il verificatore attuale del taglio usa staffe singole: selezionando Spirale il taglio resta non verificato e il dimensionamento resistente automatico non viene eseguito. Nella zona dissipativa di testa il controllo NTC delle staffe singole non è soddisfatto da una spirale. Disegno, verifica e dettagli costruttivi rimangono distinti.

### Controlli sismici della testa del palo

Nella Risposta elastica, aprire Tratti di armatura → Sisma · testa del palo · NTC §7.2.5. Attivare il controllo quando si considera una zona dissipativa presso la testa, perché non è escluso il raggiungimento della capacità. Gli archivi precedenti mantengono la modalità disattivata: non si attribuisce automaticamente natura sismica al carico esistente.

Lasciando vuota l'estensione, Checker usa 10D dalla testa fisica del palo, comprendendo l'eventuale tratto libero. È possibile assegnare una zona più lunga; una più corta non supera il controllo di estensione. Se il palo è più corto di 10D, il programma controlla tutta la lunghezza e dichiara questa convenzione. Il confine della zona entra nella mesh. Ogni tratto che la interseca viene controllato con la propria armatura uniforme; per differenziare la staffatura, dividere il tratto alla quota voluta. L'attivazione non cambia automaticamente barre, passi o quote di taglio.

Scegliere Da definire, Staffe singole oppure Spirale. In zona dissipativa una spirale non soddisfa la prescrizione delle staffe singole. Le conferme della combinazione sismica per N e V e del momento elastico non ridotto, con N concomitante, sono separate. Un modello FEM elastico non dimostra da solo che i carichi derivino da un'analisi sismica con q=1. Senza tali conferme restano disponibili i controlli geometrici, mentre quelli sulle azioni sono indicati come da completare.

Nel riepilogo a destra selezionare Sisma · testa palo. Sono mostrati estensione, minimo longitudinale, diametro e passo delle staffe, tipologia trasversale, margine a taglio, compressione media e momento elastico, con valore, limite, quota critica e criterio. Dentro la zona valgono As almeno 1% Ac e passo massimo 6φL; fuori valgono 0,3% e 8φL. Il diametro trasversale minimo è 8 mm. La modalità sismica applica questi minimi anche se il controllo ordinario dei minimi pali è disattivato. I coefficienti unitari/custom non possono dare un esito di conformità NTC.

Dimensiona usa anche i controlli sismici attivi per selezionare i candidati; richiede le conferme necessarie. Barre, ancoraggi e giunti rimangono da verificare nella disposizione reale. Modificare armature o conferme riutilizza la risposta FEM quando gli input fisici restano invariati; modificare il confine della zona aggiorna la discretizzazione. Sul profilo palo-terreno e su tutti i diagrammi la zona è evidenziata con una fascia viola, il contorno presso il palo e una linea orizzontale alla quota minima 10D. La legenda superiore indica 10D in metri. Se la zona assegnata è più lunga, sono distinti il limite minimo e la fine assegnata; le quote vicine hanno richiami separati per evitare sovrapposizioni. Il controllo Zona sismica 10D permette di nascondere solo la rappresentazione. La fascia è disponibile già con i risultati FEM, prima del completamento di MRd. La tavola armature conserva il contorno arancione della zona. Archivio, JSON, CSV e report conservano impostazioni, fonte ed esiti.

Il controllo riguarda le prescrizioni di testa e le condizioni semplificate in assenza di una valutazione specifica di duttilità. Non genera combinazioni sismiche, azioni cinematiche, zone dissipative profonde, verifiche del nodo palo-plinto o dettagli esecutivi. I relativi limiti sono elencati nel riepilogo. Il pulsante NTC apre la finestra interna con sintesi e pagina originale: NTC 2018 §7.2.5, Gazzetta Ufficiale, pagina PDF 217, stampata 213.

### Ricalcolo delle sollecitazioni e delle resistenze

Il FEM viene reso disponibile prima delle verifiche resistenti. Modificare solo barre o staffe conserva N, V, M, spostamenti e mesh, ricalcolando MRd e i dettagli. Modificare terreni, carichi, geometria, EJ o quote di partizione aggiorna anche il FEM. Le quote di partizione sono nodi del modello numerico. Le verifiche e le esportazioni complete vengono invalidate immediatamente; un calcolo superato non può sostituire quello corrente. MRd usa calcoli paralleli con solutori indipendenti e conserva i risultati già disponibili per la stessa sezione e gli stessi valori esatti di N.

Con molte schede aperte il ricalcolo resta «in coda» finché non termina uno dei calcoli in corso, come il calcolo della capacità.

### Tratti e verifica delle sezioni

Il primo utilizzo e gli archivi senza tratti hanno un solo tratto collegato alla sezione principale di Broms. Le quote sono misurate dalla testa; una fine vuota indica la punta. Fine tratto / barre è la quota fisica finale del gruppo: il primo inizia a zero, ogni successivo risale dal proprio inizio della sovrapposizione adottata. Dividi aggiunge un confine interno, Unisci conserva l'armatura del tratto precedente. La partizione deve coprire il palo senza vuoti. I tratti successivi possono restare collegati oppure avere quantità e diametri delle barre, diametro e passo delle staffe personalizzati. Geometria e materiali restano comuni. La modifica della sezione principale aggiorna soltanto i tratti collegati.

Confermare nelle ipotesi la natura delle azioni di progetto e il modello di taglio circolare equivalente, con z/d assegnato. Le verifiche impiegano N, V e M alla stessa quota, includendo interfacce, confini dei tratti, campioni interni ed estremi dei polinomi FEM. Il riepilogo mostra la sezione critica e la sezione trasversale segue la quota selezionata. Gli esiti distinguono non verificabile, non soddisfatto, parziale e soddisfatto N–M–V con dettagli ancora da completare. Il verificatore c.a. non si applica ai CHS.

MRd positivo e negativo sono calcolati al valore locale di N e sovrapposti a MEd con linea tratteggiata. Le zone prive dello sviluppo completo delle barre, vicino alle loro estremità, sono evidenziate in colore chiaro. Dove la lunghezza ancorata oltre a_l è minore di lbd, MRd usa fyd·r, con r la lunghezza ancorata oltre a_l divisa per lbd e arrotondata per difetto a 0,05: la sezione critica e il report scrivono «MRd con fyd·r» invece di «MRd nominale», e un tratto che così diventa non soddisfatto lo dice nello stato. La curva si interrompe e la verifica resta parziale solo dove r è minore di 0,05 o un giunto non è utilizzabile. Il valore nominale rimane consultabile separatamente e non certifica il confinamento dei giunti. Fuori dal dominio resistente non viene disegnata una resistenza valida. La capacità ultima di Broms non è utilizzata come azione del FEM o delle sezioni.

### Proposte e vista delle armature

La proposta dei tratti e Dimensiona adottano i cataloghi e i criteri descritti sopra. Per il primo tratto applicare una proposta aggiorna la sezione principale e i tratti collegati; per gli altri crea una personalizzazione.

L'opzione minimi pali controlla il sottoinsieme NTC 2018 §7.2.5: As almeno 0,3% Ac, staffe di almeno 8 mm e passo non superiore a 8 diametri longitudinali. Non attiva una verifica sismica completa né definisce le zone dissipative. Il dimensionamento può non trovare una soluzione nel catalogo assegnato: ampliare consapevolmente il catalogo o modificare la sezione.

Tavola armature e distinta ferri presenta un solo palo in elevazione a sinistra, le singole barre sviluppate lateralmente con marche B01, B02 e successive al centro e le sezioni trasversali dei tratti a destra. Tutte le quote si riferiscono alla testa e condividono la scala verticale; le quote fisiche dei gruppi sono leggibili accanto alle barre. I richiami S01, S02 e successivi identificano le zone di staffatura e le righe della distinta. La tabella inferiore collega marche, tratti, quantità, diametri, quote, lunghezze di taglio, barre commerciali e giunti. I gruppi dei tratti e i relativi tagli sono riportati una sola volta. Apri tavola armature apre la vista ingrandita con zoom ed esportazione PNG.

La fine del tratto è la fine fisica delle barre. Con un palo di 20 m, tratti 0–12, 12–18 e 18–20 m e sovrapposizione adottata di 1,20 m, i gruppi risultano 0–12, 10,80–18 e 16,80–20 m: lunghezze 12,00, 7,20 e 3,20 m. Il primo gruppo parte dalla testa; ogni gruppo seguente risale di l0 rispetto al proprio inizio. Anche due tratti con identica armatura rimangono gruppi separati. Il calcolo non sposta le estremità per soddisfare ancoraggi o sviluppo: segnala gli eventuali controlli da completare.

Un gruppo è spezzato internamente soltanto se non entra nelle lunghezze commerciali ammesse; i tagli conservano il primo inizio e l'ultima fine, con sovrapposizioni intermedie comprese nelle quantità. Un tratto netto di 12 m dopo il primo richiede più di 12 m di barra per il giunto entrante: con limite commerciale 12 m viene quindi spezzato. Le barre commerciali più lunghe vengono tagliate alla lunghezza richiesta, senza allungare il gruppo.

I giunti J mostrano coppie effettivamente allineate rispetto a quelle previste, quote, lunghezze iniziale, richiesta, adottata ed effettiva. Passando da 20 barre equidistanti a 12, non tutte le direzioni coincidono: il programma conserva quantità e tagli ma segnala la disposizione trasversale da definire. Finestra troppo corta, giunti interferenti e distanza non ammessa impediscono di considerare il dettaglio soddisfatto. Le sezioni sono nominali; accostamento, confinamento, sfalsamento e chiusure delle staffe restano da completare. I vecchi valori archiviati di sviluppo esterno sono conservati ma non modificano i tagli di questa modalità.

La vista finale deriva dai risultati e rimane preliminare quando mancano dettagli. Non somma l'armatura sovrapposta alla resistenza nominale del tratto. La distinta separa barre, giunti e staffe, con le relative unità.

L’esempio [Palo 12 m con quattro tratti](../esempi/palo-orizzontale-armature/palo-12m-quattro-tratti.programma) mostra quattro gruppi distinti; il [riepilogo](../esempi/palo-orizzontale-armature/palo-12m-quattro-tratti.pdf) descrive input e limiti. L’esempio [Palo 20 m, tagli 12–18–20](../esempi/palo-orizzontale-armature/palo-20m-tagli-12-18-20.programma) usa 20Ø20 nel primo tratto e 12Ø16 negli altri: le sovrapposizioni adottate sono rispettivamente 1,20 e 1,00 m. Le barre risultano 0–12, 10,80–18 e 17–20 m. Il [riepilogo degli input](../esempi/palo-orizzontale-armature/palo-20m-tagli-12-18-20.pdf) esplicita anche le coppie da sistemare al cambio 20→12.

Salvataggio, JSON, CSV e report conservano parametri e origine, modello di N, azioni concomitanti, collegamenti, verifiche, resistenze, sviluppi e distinta preliminare. Un cambiamento invalida subito risultati e armature, bloccando l'esportazione fino al ricalcolo.

### Esempio riproducibile e limiti

Nei dati iniziali: D = 1 m, lunghezza infissa 30 m, tratto libero zero, H = 100 kN, e da piano campagna zero, testa libera. Attivare Override EJ di 50000 kN m² con motivazione Benchmark numerico. Uno strato spesso 30 m usa kh costante = 10000 kN/m³, fonte Esempio numerico assegnato. Nella scheda elastica selezionare la stratigrafia, punta libera e passo iniziale 0,50 m. La soluzione fine a 0,25 m fornisce ytesta = 0,0094574084 m e Mmax = 68,178634 kNm a x circa 1,660914 m. Sono parametri per controllare il codice, non valori consigliati per un terreno reale.

Il modello esclude plasticità, distacco, curve p-y, trasferimento assiale al terreno, secondo ordine, ciclicità e interazione di gruppo. Le molle reagiscono nei due versi. EJ integra non descrive automaticamente la fessurazione. Nessun fattore di efficienza della palificata è applicato al palo singolo. La convergenza non attesta resistenza strutturale o capacità del terreno.


## Wiki e centro della conoscenza

### Wiki del modulo e aiuti sui singoli dati

Ogni modulo dispone del pulsante Wiki del modulo. Il comando Approfondimenti apre una scelta di argomenti e sezioni pertinenti. Accanto ai dati per cui esiste una spiegazione specifica, il pulsante ? raggiunge il punto della Wiki: per esempio copriferro nominale, copriferro per fessurazione, materiali, rigidezza del palo, armature e sisma. F1 nell’editor apre l’aiuto del dato, quando disponibile, oppure la guida del modulo.

La consultazione conserva il foglio di lavoro: la Wiki si apre in una sua scheda e il comando Torna al lavoro riporta ai dati correnti. Anche le revisioni in sola lettura consentono di consultare gli aiuti. I collegamenti riguardano il tema del dato, non avviano una modifica dei parametri o un diverso metodo di verifica.

### Consultare la nuova biblioteca tecnica

La [biblioteca tecnica](wiki:biblioteca-tecnica) raccoglie le lezioni di ANTHEA con esempi numerici svolti su geotecnica, calcestruzzo armato e ponti. Cercare un termine come taglio, copriferro, consolidazione, MASW o cassoni. Ogni lezione dichiara ipotesi, unità e limiti; la data di revisione non significa aggiornamento normativo automatico del contenuto.


### Engineering Handbook

La copertina presenta dodici capitoli numerati: Fondamenti, Scienza e tecnica delle costruzioni, Materiali, Calcestruzzo armato, Acciaio, Geotecnica, Ponti e infrastrutture, Ingegneria sismica, FEM, BIM, Computational Design e Anthea. Le schede mostrano anteprime degli articoli e il collegamento Esplora il capitolo; si dispongono su una, due o tre colonne secondo lo spazio. Inizia dai fondamenti apre il primo percorso. Ogni capitolo presenta introduzione e articoli numerati con livello e tempo di lettura. Le pagine distinguono introduzione, metodo e approfondimento tecnico; i sei piloti revisionati sono Cos'è un ponte, Instabilità di Euler, Fessurazione, Capacità portante, Elementi Beam e guida della Sezione in c.a.

Le pagine correnti consolidano istruzioni e teoria; Fonti e archivio spiega la provenienza. I vecchi collegamenti aprono la destinazione corrente pertinente. Contenuto integrato indica una revisione editoriale; Riscontri sulle fonti da completare segnala attribuzioni normative o bibliografiche non ancora confermate sui testi primari.

Usa la tendina Chiaro, Scuro o Molto scuro per cambiare l’aspetto globale di Anthea, condiviso con il menu Aspetto. Formule e nuovi schemi vettoriali seguono il tema; le immagini storiche conservano la propria tavola chiara. A finestra stretta Indice e In questa pagina sono pannelli alternativi; formule e tabelle larghe scorrono orizzontalmente. Il glossario raccoglie anche SLU, SLE, FEM, DOF, MPC, SRSS, CQC, LTB, p-y e M-N. Gli acronimi alimentano la ricerca; una definizione può rimandare a una sezione che ne spiega i limiti, senza simulare un modulo di calcolo assente.

I prerequisiti precedono il testo; fonti, correlati e navigazione precedente/indice/successivo chiudono la lettura. I collegamenti usano identificativi centrali: titolo e destinazione vengono risolti dal catalogo. La validazione controlla identità, categorie, capitoli, sezioni, figure e riferimenti; le prove WPF verificano formule e pagine in entrambi i temi.

Wiki è il terzo ambiente di Anthea insieme a Progetti e Moduli singoli. Raccoglie il Manuale di ingegneria e le Guide Anthea in una navigazione e una ricerca comuni. I contenuti sono ricavati dalle due guide globali, evitando una seconda documentazione indipendente.

### Cercare e scegliere un percorso

Apri Wiki dalla navigazione principale o dalla Home. La ricerca considera titoli, testo, formule, categorie e sinonimi italiano-inglese; per esempio buckling trova contenuti sull'instabilità. Ogni risultato indica se appartiene al manuale teorico o alla guida applicativa. I dodici capitoli aprono indici ordinati delle voci disponibili.

### Leggere e riprendere

Nella copertina il menu laterale è raccolto nel pulsante Indice. Durante la lettura, il menu a sinistra espande gli articoli del capitolo corrente; quello a destra raccoglie le sezioni della pagina. Seleziona una voce per raggiungerla; la sezione corrente è evidenziata durante lo scorrimento. Copia collegamento produce un indirizzo Wiki interno leggibile. Segna sezione letta registra una scelta esplicita: la posizione di lettura non equivale a un apprendimento verificato.

Riprendi la lettura mostra le ultime tre pagine; riaprendo una pagina viene ripristinata la sezione visitata. Il progresso è memorizzato localmente nel profilo Windows, separato dai documenti di calcolo. Su finestre strette i pulsanti Indice e In questa pagina mostrano i menu laterali. Non occorre una connessione per consultare i contenuti incorporati; i riferimenti web aprono il browser.

### Passare dalla teoria al calcolo

Nel riquadro Applica nel modulo, Apri · seguito dal nome del modulo apre il modulo, mentre Apri esempio · crea un foglio con i dati dell'esempio. Il documento corrente segue il normale controllo di salvataggio. Aprire il modulo senza esempio riprende la scheda già attiva, quando disponibile. Il comando Wiki del modulo nella barra del foglio apre la guida pertinente; Torna al lavoro riporta al calcolo con i dati conservati.

I pulsanti ? accanto a copriferro e azioni della sezione offrono una definizione breve e aprono la sezione specifica della Wiki. Le procedure della gestione del lavoro sono nell'articolo Progetti e gestione del lavoro delle Guide Anthea. Il comando Modello e dati comuni raggiunge la teoria pertinente nella stessa Wiki.

Per gli elementi Beam, il percorso consigliato è leggere il modello, confrontare reazioni e momento con l'esempio manuale, aprire la Sezione in c.a. e modificare altezza o verso del momento. La Wiki distingue l'analisi della trave dalla verifica della sezione: non introduce un nuovo solutore FEM generale.

### Progetti e problemi frequenti

Le procedure di creazione, rinomina, duplicazione e revisioni restano nei capitoli Progetti della stessa guida globale e sono indicizzate nella Wiki. Per un calcolo usa sempre Salva o Salva con nome: il progresso della Wiki non salva i dati del modulo. Se una ricerca non trova il termine, prova un sinonimo o una parte della parola; il campo di validità è dichiarato nelle pagine correnti.

## Modulo Sezione in c.a.

### Controllare norma e dati ereditati

La norma selezionata governa i coefficienti iniziali. Cambiarla richiede il controllo dei parametri assegnati e il ricalcolo. Un documento salvato da una versione di ANTHEA che non registrava ancora la norma si apre con NTC 2018, la norma con cui era stato calcolato. Un documento attuale senza norma è incompleto, perché ANTHEA la registra sempre: l'apertura si ferma con un messaggio e occorre ripristinare una copia del documento. I campi assenti integrati dalla preparazione del documento sono elencati in Valori integrati dal documento precedente. Valori nulli, testi errati e norme sconosciute richiedono invece una correzione.

Il report usa i dati e la norma della copia che ha prodotto il risultato. Se la provenienza manca o è incoerente, occorre ricalcolare; cambiare un campo nella scheda non aggiorna retroattivamente un risultato salvato.


Questo modulo verifica una sezione in calcestruzzo armato a partire da geometria, materiali, armature e azioni assegnate. Permette di esplorare domini resistenti, stati tensionali e deformativi, taglio, torsione e dettagli nei limiti indicati dalle rispettive schede. Non determina le azioni di un telaio completo e non sostituisce l'analisi globale della struttura.

### A cosa serve e quando usarlo

Usalo quando disponi delle terne N, Mx e My per le combinazioni rilevanti e vuoi confrontarle con la risposta della sezione. È adatto anche al controllo di sensibilità: mantenendo le azioni, modifica una dimensione o l'armatura e osserva il dominio. Per un elemento lungo e compresso la verifica della sezione non esaurisce l'eventuale problema di instabilità del sistema: la scheda Colonna modello aggiunge gli effetti del secondo ordine di un pilastro isolato, mentre telai e strutture non riconducibili a elementi isolati richiedono un'analisi globale.

[Teoria del modello Beam e trasferimento delle azioni](/wiki/manuale/fem/elementi-beam)

### Salvataggio e riapertura della sezione

Il documento conserva il modello della sezione con geometria, materiali, barre e trefoli, insieme ai parametri necessari a modificarlo. Le combinazioni e le impostazioni delle verifiche rimangono nel foglio; le mesh e i risultati temporanei vengono ricalcolati. Una modifica della ricetta geometrica o dei trefoli richiede l'aggiornamento del modello.

I documenti precedenti restano apribili e vengono convertiti al nuovo formato al salvataggio. Conserva una copia se devi riaprirli con una versione precedente di ANTHEA. I dati incompleti si salvano come bozza: il foglio mantiene i valori digitati e il motivo per cui non può ancora costruire una sezione valida. Anche le revisioni storiche conservano la propria sezione.

### Wizard delle tipologie e delle armature

Nel pannello di controllo apri **Wizard tipologie / modifica disposizione…**. Scegli la tipologia, assegna dimensioni e armature e controlla l'anteprima affiancata. **Applica sezione** valida anche la compatibilità con i trefoli già presenti nel foglio. **Annulla** lascia invariati i dati. Per modificare una disposizione tipologica riapri lo stesso wizard; le coordinate delle barre restano modificabili nella tabella.

| Tipologia | Disposizione iniziale |
| --- | --- |
| Rettangolare | Due file superiori e due inferiori; numero, diametro e interferro delle seconde file indipendenti |
| Rettangolare cava | Barre sui contorni esterno e interno; staffe nelle quattro pareti |
| Circolare | Due anelli con numero, diametro e interferro radiale assegnati |
| Circolare cava | Anello esterno e interno rispetto al foro; due staffe circolari |
| A T | Barre superiori, inferiori e laterali; staffe di anima e ala |
| A I | Ali superiore e inferiore anche diverse; armatura perimetrale e staffe distinte di anima e ali |
| Ellittica | Barre a incrementi angolari uguali, sul contorno interno utile |
| Capsula | Numero e diametro distinti per i due archi e per i due rettilinei; disponibile anche la disposizione radiale precedente |
| A L | Due bracci ortogonali; armatura perimetrale e staffe dei singoli bracci |
| A C | Canale aperto, anima e due ali sullo stesso lato |
| Trapezoidale | Basi parallele centrate; barre perimetrali e staffa aderente |
| Poligonale regolare | Da 3 a 32 lati; diametro circoscritto, barre perimetrali e staffa aderente |

Per le armature perimetrali assegna il passo massimo tra gli assi; il generatore inserisce le barre agli spigoli e suddivide i lati. Per le disposizioni radiali assegna il numero di barre. Il copriferro è netto, misurato alla superficie esterna della staffa. Il disegno verifica la collocazione geometrica; i tratti di staffa sono schematici e non rappresentano ganci e raggi di piegatura esecutivi.

Le nuove tipologie e le disposizioni cave a doppio contorno richiedono **parametri manuali del taglio**: assegnare bw, d e, quando richiesto, Asl, oltre ai bracci efficaci nelle due direzioni. Il wizard svuota i valori della sezione precedente. Ripartizione del taglio tra pareti, ancoraggio e trattenimento delle barre vanno verificati esplicitamente. Per la capsula impostata come **Pilastro** sono disponibili i dettagli generali NTC, con interasse misurato lungo il contorno delle barre. Per le altre tipologie avanzate i dettagli automatici restano fuori campo; la ricerca automatica delle armature non si applica alle disposizioni avanzate. Il modello torsionale automatico rimane limitato a rettangolo e cerchio; un profilo aperto richiede un modello dedicato.

### Armature della capsula

Scegliere **Curve e rettilinei separati**. Il numero per semicirconferenza comprende le due barre agli estremi di tangenza; il numero per rettilineo comprende soltanto le barre interne. I conteggi valgono per ciascuno dei due archi e per ciascuno dei due rettilinei: 17 barre per arco e 8 interne per rettilineo producono 50 barre complessive. Diametri delle barre curve e rettilinee sono indipendenti. Le barre curve sono radiali rispetto al centro della rispettiva semicirconferenza.

La dimensione maggiore orienta la capsula: b = 5000 mm e h = 2500 mm danno due semicirconferenze laterali e due rettilinei orizzontali. Per b = h usare la sezione circolare. Le capsule già salvate conservano **Radiale dal centro (precedente)**; aprire il wizard non cambia automaticamente le loro armature. Il passaggio alla nuova disposizione richiede la scelta del modo e **Applica sezione**.

Nella scheda dei dettagli selezionare **Pilastro**. Sono disponibili i controlli generali su armatura longitudinale, interassi, copriferro e staffe; una barra spostata manualmente fuori dal percorso lascia l'interasse da verificare, mantenendo gli altri controlli. La staffa perimetrale disegnata non conferma il trattenimento delle barre: progettare e controllare anche le legature. Il confinamento sismico specifico della pila non è verificato da questi controlli generali. Le grandezze resistenti e i bracci delle staffe per il taglio restano da assegnare nelle due direzioni.

### Input geometria e unità

| Dato | Unità | Significato e controllo |
| --- | --- | --- |
| Sezione | scelta | Sezioni usuali e dodici tipologie da wizard; contorno e armatura devono essere compatibili |
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
| Colonna modello | Capacità del primo ordine del pilastro isolato e, con M0Ed, equilibrio del secondo ordine | l0, l, imperfezione, viscosità e modo di crisi |

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

[Elementi Beam](/wiki/manuale/fem/elementi-beam) spiega come ottenere e trasferire le azioni. I capitoli Sezione in c.a. e Calcestruzzo armato della guida teorica descrivono i modelli costitutivi e i limiti del motore. Il comando Apri · del riquadro Applica nel modulo raggiunge lo stesso strumento disponibile in Moduli singoli; Wiki del modulo nella barra del foglio torna a questa guida.

Il percorso completo richiede un modello globale plausibile, azioni coerenti, una sezione fisicamente realizzabile e verifiche appropriate ai diversi stati limite. Un esempio numerico aiuta a imparare il metodo; la verifica del progetto richiede i dati reali e tutti i meccanismi rilevanti.

### Domini tridimensionali e bidimensionali

Le schede dominio 3D e dominio 2D permettono di esaminare le resistenze della sezione e il punto richiesto. Scegliere il criterio di ricerca coerente con il problema: mantenere N costante e aumentare il momento non è lo stesso percorso che mantenere costante l'eccentricità. Il coefficiente risultante dipende anche da questo percorso.

Un dominio bidimensionale è una sezione o una proiezione del problema. Se il punto ha componenti fuori dal piano rappresentato, leggere l'indicazione di proiezione e non trattare l'immagine come una verifica completa della pressoflessione deviata. Con «Proietta azioni sul piano» il grafico N–M mostra il punto verificato: Mθ = Mx cos θ + My sin θ con N invariato, sulla retta dall'origine a Rd. I quattro momenti rapidi Mx positivo e negativo, My positivo e negativo sono utili per orientarsi, ma non descrivono da soli l'intero dominio N Mx My.

Nei nuovi fogli scegliere separatamente lo Stato limite (SLU o SLV) e il Criterio resistente (Plastico o Elastico). Il criterio si imposta nella verifica, nelle schede dominio 3D e dominio 2D, e si applica alle combinazioni dei due stati limite. Cambiarlo aggiorna i domini e i risultati. Non vengono calcolati sempre entrambi i criteri. La ricerca della resistenza, ad esempio N costante, rimane una scelta distinta.

I documenti precedenti mantengono i gruppi Plastico ed Elastico fino alla conversione esplicita delle sollecitazioni. In quei documenti le chiavi interne SLU e SLV indicavano tali gruppi: non assegnavano automaticamente uno stato limite sismico.

Nella **Scala del dominio 3D** i fattori N, MX e MY sono adimensionali e hanno l'unità «−», come gli altri fattori adimensionali del foglio; il cambiamento riguarda solo la presentazione.

### Tensioni e fessurazione

Aprire la scheda dedicata, scegliere la combinazione e leggere prima lo stato tensionale: zona compressa, barre tese, tensione massima, asse neutro e deformazioni. Le deformazioni a video possono essere in per mille; i coefficienti di curvatura hanno un'altra dimensione. Non confrontare i due numeri senza conversione.

La fessurazione dipende anche da durata, aderenza, copriferro alle barre, diametri, interassi e area efficace di calcestruzzo teso. Con il campo «c barra» vuoto il copriferro della formula è misurato sulle barre efficaci verso la faccia tesa; un valore assegnato lo sostituisce. Aprire il dettaglio del calcolo di wk: permette di capire se governa il termine di deformazione minima o quello corretto per il tension stiffening, e se si applica il ramo di barre ravvicinate o distanziate.

La trazione dell'intera sezione segue un ramo specifico con regioni di bordo. Le superfici interne dei fori compatibili hanno controlli dedicati; una superficie tesa priva di armatura efficace rende la verifica non soddisfatta, una priva di interasse definito la lascia incompleta. Il disegno completo non dimostra la completezza dei controlli. Analogamente, una soluzione tensionale non lineare o una sezione con tendini può avere disponibilità diversa per il controllo di fessurazione. Leggere lo stato della singola verifica, non soltanto il grafico delle tensioni.

### Taglio torsione e dettagli

Nella scheda taglio e torsione impostare staffe, passo, inclinazione e armatura longitudinale pertinente. Le azioni dei nuovi fogli provengono dalle stesse righe SLU e SLV usate nei domini; modificarle con Gestisci sollecitazioni. Controllare bw, d e braccio interno ricavati. Per sezioni circolari il ramo adottato è quello specifico per pali descritto nella guida teorica; non coincide automaticamente con una formula generica per travi rettangolari. Le verifiche a taglio e torsione si calcolano dopo le verifiche SLU e SLE, sullo sfondo: mentre la riga di stato mostra «GPC Engine · in corso: Taglio» le righe della tabella del taglio restano a «—» e la finestra resta utilizzabile.

La torsione richiede un dettaglio chiuso compatibile e armatura longitudinale disponibile dopo le esigenze di flessione. Non assegnare come disponibile tutta l'armatura senza aver verificato questa condizione. La verifica combinata usa anche un'interazione conservativa fra torsione e i due tagli, esplicitata nel dettaglio.

Nella scheda dettagli costruttivi scegliere il tipo di elemento e completare le informazioni su diametri, passi, sovrapposizioni, ancoraggi e copriferro. Gli ancoraggi seguono il campo Barre della sezione: con Liscia restano da completare, anche nelle proposte della ricerca delle armature. Per i minimi di armatura dei pilastri la scheda usa la compressione più gravosa fra tutte le combinazioni del foglio e indica quella che governa. Se governa una combinazione SLE, il valore è segnalato come di esercizio: controllare che le combinazioni SLU e SLV siano complete. «Da completare» non equivale a «Verificato». I controlli automatici riguardano le condizioni esplicitamente rappresentate; ganci, nodi, confinamento e situazioni esecutive speciali possono richiedere valutazioni aggiuntive.

### Curva momento curvatura e aggiornamento

La curva M χ si avvia con il proprio comando. Assegnare N, direzione del momento, numero di passi e distribuzione dei punti. Il campo «CLS teso nelle risposte», Trascurato oppure Tension stiffening, riguarda solo i punti della curva: punto limite e verifiche non lo usano. Con Tension stiffening la sezione resta interamente reagente finché il calcestruzzo non si fessura; dopo, il calcestruzzo teso non lavora e le barre tese dell'area efficace di fessurazione seguono il modello del tirante, con β = 0,4 dei carichi di lunga durata e i valori medi Ecm e fctm, mentre le altre barre restano nude. Il primo snervamento e |εs|max sono quelli della barra nella fessura, e lo stato della curva lo dichiara. Fino alla 1.14.3 l'opzione applicava una legge media del calcestruzzo teso a tutta la zona tesa: ricalcolate, le curve di quei fogli risultano più cedevoli dopo la fessurazione e snervano prima. Esaminare la progressione della risposta e le deformazioni terminali. La curva arriva al limite trattato dal motore e non descrive automaticamente un ramo post picco di degradazione della struttura.

Nel report di progetto la curva viene ricalcolata automaticamente con N, direzione, passi e opzioni salvati nel foglio. Il capitolo comprende valori limite, tabella dei punti e grafico; non serve aprire la scheda né avviare prima il comando Calcola curva. La preparazione lavora su una copia dei dati. Se il percorso non è valido o contiene trefoli, la curva è segnalata come non disponibile con il motivo e le altre verifiche restano nel report. Interrompere la preparazione conserva il documento precedente. Il report del singolo foglio continua a usare la curva calcolata nella scheda.

Se si modifica l'input mentre sono visibili risultati precedenti, le sezioni strutturali possono segnalarli come «DA AGGIORNARE». Quel risultato non deve essere esportato o letto come riferito ai nuovi dati. Attendere il completamento del ricalcolo e controllare lo stato. La conservazione grafica serve a orientarsi durante l'editing, non a certificare la validità di valori obsoleti.

Le tabelle CA consentono copia e incolla e dispongono dei comandi di template e reimportazione previsti dall'interfaccia. Prima di un'importazione estesa salvare il foglio, controllare intestazioni e unità, quindi verificare numero di righe, barre e combinazioni importate. La semplice riuscita dell'importazione non dimostra che l'ordine delle colonne fosse quello voluto.

### Colonna modello: dati e calcolo

La scheda 08 Colonna modello, dopo 07 Momento–curvatura, verifica un pilastro isolato snello con il metodo della colonna modello di EN 1992-1-1 5.8.6(6) e 5.8.8.2, al quale rinvia NTC 2018 §4.1.2.3.9.3: gli effetti del secondo ordine sono concentrati nella sezione critica, con sezione e sforzo normale costanti lungo il pilastro. La sezione è quella del foglio. Calcola armature diventa la scheda 09. I documenti salvati con otto schede si riaprono sulla stessa scheda, senza conversione dei dati.

Nel riquadro Dati della colonna assegnare **N costante (− compressione)**, in kN e negativo a compressione, e **Direzione del momento θ**, con la stessa convenzione della scheda Momento–curvatura: (Mx, My) = M (cos θ, sin θ) negli assi locali. **M0Ed nella sezione critica** è il momento del primo ordine, positivo nella direzione θ e senza imperfezione; per un elemento controventato con momenti d'estremità diversi assegnare il momento equivalente M0e di EN 1992-1-1 (5.32) e verificare a parte M02 all'estremità. Con il campo vuoto la scheda calcola la sola capacità. **Lunghezza libera di inflessione l0** e **Lunghezza dell'elemento l**, in metri, sono obbligatorie: l0 dipende dallo schema di vincolo (EN 1992-1-1 5.8.3.2 e Fig. 5.7: 2 l per la mensola, l fra due cerniere, 0,7 l fra incastro e cerniera, 0,5 l fra due incastri); l serve per il fattore αh dell'imperfezione e per l/300. **Fattore della curvatura c** vale 10, adatto alla deformata sinusoidale, e scende fino a 8 quando il momento del primo ordine è costante.

Nel gruppo Imperfezione ed eccentricità minima il valore iniziale di **Imperfezione geometrica** è quello di EN 1992-1-1 5.2(7)a con θ0 = 1/200, usato anche con le NTC; le alternative sono l0/400 di EN 1992-1-1 5.2(7), per i pilastri isolati dei sistemi controventati, l/300 di NTC 2018 §4.1.2.3.9.3, per i pilastri dei telai a nodi fissi, un valore in **Eccentricità assegnata ei** oppure nessuna imperfezione. L'imperfezione agisce nella direzione del momento. **Eccentricità minima** Della norma vale max(l0/200; 20 mm) con NTC 2018 e CNR-DT 200 e max(h/30; 20 mm) con EN 1992-1-1 e UNI EN 1992-1-1. È un minimo del momento totale del primo ordine e non si somma all'imperfezione: il riquadro dei risultati indica quale dei due governa.

Nel gruppo Viscosità il valore iniziale è Trascurata: la scheda controlla le tre condizioni di EN 1992-1-1 5.8.4(4) e avvisa quando non sono dimostrate; per il controllo di φ(∞, t0) ≤ 2 inserire anche **Coefficiente di viscosità φ(∞, t0)**. Con φef assegnato si compila **Coefficiente di viscosità efficace φef**. Con Da φ(∞, t0) con la (5.19) servono φ(∞, t0), **M0Eqp quasi permanente** e **NEqp quasi permanente**, della combinazione quasi permanente nella stessa direzione θ e senza imperfezione. Il gruppo Calcolo, chiuso all'apertura, contiene rm = M01/M02 per la snellezza limite (vuoto: C = 0,7), i passi di curvatura (200, da 10 a 500), la tolleranza su N del confronto con MRd(N) e il momento trasversale ammesso (1 %).

**Calcola colonna** conferma i dati del foglio e avvia il calcolo, che non parte da solo; con gli stessi dati il risultato non viene ripetuto. **Interrompi** abbandona il calcolo in corso senza pubblicare risultati parziali. Ogni modifica dei dati del foglio o della scheda cancella il risultato e chiede di ricalcolare la colonna. La scheda usa sempre il dominio plastico con il calcestruzzo teso trascurato, qualunque sia il criterio scelto nelle verifiche, e non tratta i trefoli. È disponibile con NTC 2018, CNR-DT 200, EN 1992-1-1 con i valori raccomandati e UNI EN 1992-1-1 con l'appendice nazionale del DM 31/07/2012, per sezioni di calcestruzzo ordinario con barre. Sono rifiutati N di trazione, una compressione oltre la resistenza a compressione centrata NRd,c e un momento trasversale oltre il limite ammesso, che compare quando la sezione non è simmetrica rispetto al piano di flessione: scegliere allora una direzione principale. Nelle revisioni in sola lettura i dati si consultano, ma il calcolo non è disponibile.

**Azioni** sceglie che cosa verificare. «Azione singola (N, M0Ed)» calcola la colonna sui campi N e M0Ed. «Combinazioni SLU» e «Combinazioni SLU e SLV» verificano tutte le combinazioni di quegli insiemi della scheda Combinazioni: ognuna è una colonna modello con il proprio N costante e con il momento del primo ordine nella direzione θ, $M_\theta = M_x\cos\theta + M_y\sin\theta$; con $M_\theta$ negativo la colonna si verifica nella direzione opposta, θ + 180°. Con le combinazioni i campi N e M0Ed si nascondono. Nei fogli con gli insiemi storici SLU è «Plastico» e SLV «Elastico». I progetti salvati con le versioni precedenti si riaprono sull'azione singola, senza modifiche al documento.

**Intervalli del dominio ridotto**, nel gruppo Calcolo, fissa il numero dei punti del dominio ridotto fra 0 e la resistenza a compressione centrata $N_{Rd,c}$: 40 per default, da 4 a 200; gli N delle azioni si aggiungono ai punti. Durante il calcolo il testo mostra la combinazione e il punto del dominio in corso; **Interrompi** abbandona il calcolo senza risultati parziali.

### Colonna modello: risultati, CSV e relazione

Il grafico M(χ) a N costante e momento esterno riporta la curva della sezione, la retta M2 = k·χ con k = NEd·l0²/c, la tangente al massimo M1Rd,max + k·χ e, se M0Ed è assegnato, la retta del momento esterno M0Ed,tot + k·χ; i punti segnano il massimo, l'equilibrio, il primo snervamento χy e la curvatura ultima χu. Il grafico Capacità del primo ordine M − M2 mostra la differenza fra la curva e M2, con il massimo M1Rd,max e, con M0Ed, la retta M0Ed,tot. Adatta, PNG ed Espandi funzionano come negli altri grafici della sezione.

I riquadri riportano la snellezza λ = l0/i con λlim della norma, solo informativa perché il calcolo si esegue comunque; il momento del primo ordine M0Ed,tot con imperfezione ed eccentricità minima; la viscosità con le condizioni di 5.8.4(4); la capacità M1Rd,max con il modo di crisi, instabilità o rottura della sezione; con M0Ed, la verifica η = M0Ed,tot/M1Rd,max con MEd = M0Ed,tot + M2 all'equilibrio; la curvatura nominale di EN 1992-1-1 5.8.8, come confronto che non decide l'esito; gli avvisi. Il testo accanto ripete stato ed esito e riporta i passaggi del calcolo con i riferimenti normativi e la tabella dei punti. L'esito è Verificata, Non verificata (instabilità) quando M0Ed,tot supera M1Rd,max e non esiste equilibrio, oppure Solo capacità senza M0Ed.

**Esporta CSV…** salva i punti del risultato pubblicato, con il nome proposto ANTHEA_Colonna_modello.csv: un punto per riga, colonne separate da punto e virgola, numeri con il punto decimale e fino a diciassette cifre significative, qualunque sia il separatore scelto nel menu Aspetto. Le colonne sono curvatura in 1/m, M, M2, M − M2 e momento trasversale in kNm, deformazioni ε0, εc ed εs in per mille e i tre indicatori di snervamento, curvatura ultima e massimo.

Le opzioni della colonna entrano nel foglio alla prima modifica di un dato della scheda: un foglio che non le ha si apre con i valori iniziali, non risulta modificato e le sue relazioni restano quelle di prima. Per i fogli con le opzioni la relazione Word ha il capitolo Colonna modello dopo il momento–curvatura, con dati, stato, esito, avvisi, passaggi del calcolo, i due grafici quando sono scelti i grafici e la tabella dei punti. Il capitolo dipende dalla scelta Momento–curvatura e colonna modello del report. Nel report di progetto la colonna viene ricalcolata su una copia dei dati, senza aprire la scheda; il report del singolo foglio usa il risultato della scheda e, se manca, lo dichiara. La teoria, le formule e gli scostamenti dichiarati sono nella [guida teorica della sezione](wiki:sezione-in-calcestruzzo-armato#colonna-modello-metodo-e-campo-di-applicazione).

Con le combinazioni il riquadro **Inviluppo delle combinazioni** dà il rapporto massimo $\eta = M_{0Ed,tot}/M_{1Rd,max}$ e la combinazione che governa, con il numero delle combinazioni verificate, non verificate e di trazione. Le combinazioni di trazione non sono pertinenti, perché la sezione si verifica con i domini; quelle con $|N|$ oltre $N_{Rd,c}$ non sono verificate (η infinito). I riquadri seguenti, i grafici M(χ) e M − M2 e i passaggi riguardano la combinazione peggiore fra quelle calcolate; il testo elenca tutte le combinazioni con N, Mθ, M⊥, M0Ed,tot, M1Rd,max, η ed esito.

La componente normale a θ, $M_\perp = -M_x\sin\theta + M_y\cos\theta$, non è verificata: quando supera l'1 % di Mθ compare un avviso. In flessione deviata ripetere il calcolo con θ + 90° e combinare le due direzioni con EN 1992-1-1 5.8.9.

Il terzo grafico, **Dominio ridotto N–M1Rd,max**, con Adatta, PNG ed Espandi, mostra in blu la capacità del primo ordine M1Rd,max al variare di N fra 0 e $N_{Rd,c}$, in grigio tratteggiato il momento resistente della sezione MRd(N) senza effetti del secondo ordine e in arancio la retta $N_{Ed}\,e_0$ dell'eccentricità minima; i punti sono le azioni (N, M0Ed,tot), verdi se verificate e rossi se no. Con momenti negativi compare anche il dominio di θ + 180°, a sinistra. Un'azione è verificata se il suo punto sta dentro la curva blu al suo N.

![Dominio ridotto N–M1Rd,max della colonna modello: capacità del primo ordine in blu, momento resistente della sezione in grigio, retta dell'eccentricità minima in arancio e punto dell'azione.](../../src/ANTHEA.Desktop/Assets/Wiki/colonna-modello-dominio-ridotto.png)

**Esporta CSV…** scrive i punti della curva, poi, dopo una riga vuota, i punti del dominio ridotto e, con le combinazioni, una riga per combinazione. Nella relazione il capitolo «Colonna modello» comprende la tabella «Combinazioni», con la combinazione che governa, e il paragrafo «Dominio ridotto N–M1Rd,max» con la figura e la tabella dei punti.

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

Il report short non comprende torsione e interazioni, dettagli costruttivi, ancoraggi, domini 2D, momento–curvatura e colonna modello. Se dati geometrici molto estesi o nomi lunghi impediscono di rispettare due pagine, l'esportazione viene fermata senza tagliare dati: abbreviare i nomi o utilizzare il report completo. Il file di calcolo resta il riferimento riapribile.

### Leggere asse neutro, mappe e report

La linea dell'asse neutro rappresenta deformazione nulla dello stato selezionato. Nei domini il piano è quello del punto resistente; nelle SLE è quello dell'azione assegnata. Una mappa normalizzata alla resistenza del materiale non sostituisce il controllo dei limiti SLE. Con campo uniforme l'asse può non essere definito; un asse esterno non indica da solo un errore.

Nell'analisi elastica con viscosità il diagramma delle deformazioni mostra le deformazioni reali della sezione, comprese quelle differite: in una barra la deformazione vale σs/Es. Le tensioni sono le stesse del calcolo; la posizione dell'asse neutro non cambia.

Il report raccoglie input, materiali, coefficienti, azioni ed esiti scelti. Gli estremi di tensione/deformazione e i casi governanti possono provenire da combinazioni diverse. Non leggerli come un unico stato. Dati globalmente invalidi o un aggiornamento in corso impediscono l'esportazione dei risultati correnti.

Prima di scegliere una norma consulta [Profili di calcolo del calcestruzzo](wiki:profili-calcestruzzo): il selettore non attesta la copertura di tutte le verifiche della norma.

## Interpretazione dei risultati e controlli indipendenti

Un risultato è utilizzabile quando sono chiari input, stato limite, metodo e combinazione. Comincia dal titolo della scheda e dagli avvisi, poi confronta il dato decisivo con un controllo semplice. La precisione delle cifre mostrate non misura la precisione del modello.

### Dalla domanda alla capacità

Un rapporto fra domanda e capacità ha significato solo se le due grandezze descrivono lo stesso meccanismo. Non confrontare un momento elastico, una capacità plastica e un limite di fessurazione come se fossero tre versioni dello stesso controllo. Un esito verde riguarda il controllo indicato nella scheda e non l'intera opera.

### Controllo numerico e fisico

Controlla unità e segni con un valore manuale. Per la trave appoggiata dell'esempio Beam, la somma delle reazioni è qL e il momento di mezzeria è qL²/8. Per una sezione osserva quale bordo è compresso. Per un problema geotecnico confronta quote, strati attraversati e verso dell'azione. Se il risultato cambia radicalmente per una piccola modifica, verifica se hai attraversato un limite fisico o una scelta discreta del modello.

### Archiviazione

Conserva il documento modificabile insieme al report e alla revisione degli input. Se un warning dichiara una parte fuori campo, documenta quale controllo aggiuntivo serve. Una tabella vuota o un risultato non aggiornato non costituiscono una verifica favorevole.

## Tutorial dal modello Beam alla verifica di sezione

Questo percorso unisce teoria, controllo manuale e utilizzo del software senza confondere analisi globale e verifica locale. Richiede circa venti minuti e usa solo strumenti presenti in Anthea.

### Capisci e controlla

Apri [Elementi Beam](/wiki/manuale/fem/elementi-beam#esempio-concettuale). Disegna la trave appoggiata, indica L = 8 m e q = 25 kN/m e calcola due reazioni da 100 kN e un momento di 200 kNm. Il carico è già assegnato come azione di progetto dell'esempio. Controlla separatamente che 25 kN/m equivalga a 25 N/mm.

### Calcola e modifica

Usa Apri esempio in Anthea. Nella Sezione in c.a. verifica la terna 0, 200, 0 e le dimensioni 600 × 800 mm. Leggi il dominio della combinazione attiva, poi modifica l'altezza e ripeti l'analisi. Conserva le azioni per isolare l'effetto della geometria. Inverti il segno del momento per osservare il ruolo delle armature superiori e inferiori.

### Osserva e archivia

Confronta i due risultati descrivendo quale ipotesi è cambiata e quale è rimasta assegnata. Non usare il controllo di resistenza per concludere sulla freccia o sulla fessurazione: richiedono analisi e combinazioni proprie. Salva il documento modificabile e annota il riferimento alla pagina Wiki. La [guida Sezione in c.a.](/wiki/guide/moduli/sezione-ca) descrive input, default e problemi frequenti.

## Fondazioni superficiali

Il foglio Geotecnica → Fondazioni superficiali verifica la capacità portante statica di una fondazione su strato portante omogeneo, con piano campagna e base orizzontali. Esamina una combinazione e un sondaggio alla volta: la condizione governante dell'opera deve essere individuata confrontando tutti i casi pertinenti. Non esegue verifiche di scorrimento, cedimento, stabilità globale, sisma o resistenza strutturale.

La pagina mantiene insieme dati, tabella degli strati ed esito. Le schede cambiano soltanto il riquadro grafico: Geometria mostra vista laterale e pianta; Stratigrafia amplia il profilo; Area efficace, Tensioni, Andamenti e Rottura mostrano le letture del calcolo. Nei riquadri più piccoli i campi scorrono senza cambiare pagina. I gruppi Metodo e verifica, Geometria, Azioni e Stratigrafia si possono richiudere. Raccogli input nasconde temporaneamente i pannelli e amplia il grafico; Mostra input li ripristina. La disposizione viene salvata nel foglio e non invalida un calcolo già eseguito. I dettagli della verifica si espandono sotto il riepilogo.

### Preparazione e scelta del metodo

Scegliere geometria, drenaggio, metodo e profilo. Eurocodice 7 indica le espressioni dell'allegato D della EN 1997-1:2004/AC:2009. Il profilo NTC 2018 applica M1 e R3 alla portanza; richiede azioni A1 già combinate. La modalità Confronto caratteristico non produce un esito di conformità normativa. Non confrontare la resistenza caratteristica con azioni di progetto attribuendo al rapporto il significato di una verifica SLU.

Le formulazioni disponibili hanno campi differenti. Terzaghi accetta carichi verticali centrati e una striscia, un quadrato o un cerchio con D/B non maggiore di 1. Brinch Hansen accetta anche azioni orizzontali, con il calcolo nelle due direzioni descritto sotto. Meyerhof accetta carichi inclinati soltanto sulla striscia. Eurocodice 7 e Vesić trattano l'inclinazione delle azioni nei due assi. Il cerchio deve essere centrato; la sua larghezza è il diametro. Queste restrizioni sono controllate dal calcolo.

**Brinch Hansen con azioni orizzontali.** Il metodo «Brinch Hansen» accetta anche Hx, parallela a B, e Hy, parallela a L. Con un'azione orizzontale la verifica calcola la pressione limite per rottura nella direzione di B′ e in quella di L′ e adotta la minore; nella fondazione nastriforme c'è solo la direzione di B′, nel cerchio l'azione orizzontale si considera diretta come B′. Gli avvisi della verifica riportano le due pressioni, la direzione che governa e la pressione limite che si avrebbe con H = 0. Nel non drenato la tabella «Hansen non drenato · correzioni additive» mostra s′, d′ e i′ della direzione che governa.

Anche un'azione orizzontale molto piccola può ridurre la pressione limite rispetto al solo carico verticale: nel non drenato fino a circa il 15 % per una pianta 2 × 3 m, nel drenato soprattutto per fondazioni poco profonde su terreni granulari. È un effetto del metodo, a favore di sicurezza, descritto nella guida teorica. Se l'azione orizzontale è trascurabile per il progetto, conviene valutarlo esplicitamente invece di inserire valori minimi.

### Geometria terreno falda e azioni

Inserire B e L in metri e D come profondità positiva dal piano campagna. X è parallelo a B e Y a L. My produce eccentricità lungo B; Mx lungo L. La compressione N è positiva; le azioni trasversali e i momenti possono avere entrambi i segni. Per la striscia tutti i valori si riferiscono a un metro di sviluppo: Hy e Mx devono essere nulli. B′ e L′ nei risultati sono ordinati dal lato efficace minore al maggiore; ex ed ey mantengono gli assi originali.

La tabella usa la gestione degli strati del palo a portanza verticale: sondaggi selezionabili, aggiunta e rimozione degli strati, colori, spessore in metri, tipologia Granulare o Coesivo, γ e γsat in kN/m³, φ′ in gradi, c′ e cu in kPa. Lo spessore è verticale dal piano campagna verso il basso. Un γsat vuoto segue γ dello stesso strato, come nel palo verticale; non equivale a un valore saturo misurato. Assegnarlo quando disponibile. La tipologia non converte automaticamente c′ in cu: il drenaggio scelto stabilisce quali resistenze usa il calcolo.

La falda è idrostatica e la sua profondità è non negativa dal piano campagna. Sopra la base sono ammessi materiali diversi, integrati nel confinamento q. Sotto la base tutti gli strati devono avere proprietà resistenti e pesi omogenei: confermare l'ipotesi di semispazio, estesa oltre la profondità rappresentata. All'interfaccia il terreno portante è quello inferiore. Un profilo eterogeneo sotto la base viene conservato, ma il calcolo si arresta senza ricorrere a medie. Il profilo deve raggiungere una quota inferiore al piano di posa.

Invia / carica terreno trasferisce una copia del sondaggio verso gli altri fogli compatibili, mantenendo parametri, falda e quota di riferimento. Nei Progetti gli stessi campi partecipano al confronto e all'applicazione dei dati comuni; spessori e numero degli strati devono essere coerenti. Non vengono trasferite azioni o formule specifiche del palo. La classe del calcestruzzo, fck e il diagramma usano catalogo e denominazioni comuni. Restano dati descrittivi del progetto: non modificano la capacità portante e non introducono una verifica strutturale o un peso proprio automatico.

N, Hx, Hy in kN e Mx, My in kNm sono risultanti al piano di posa già combinate, comprensive dei pesi della fondazione e del ricoprimento. Nel drenato N è efficace al netto della sottospinta; nel non drenato è totale, senza sottrarre la pressione interstiziale al piano di posa. Il foglio non genera le combinazioni né aggiunge automaticamente questi pesi. Il sovraccarico esterno inserito in kPa contribuisce al confinamento laterale q: non viene aggiunto a N. Per NTC 2018 confermare esplicitamente la preparazione delle azioni A1.

Il meccanismo di rottura è una scelta del progettista. Le riduzioni di Terzaghi e Vesić modificano i parametri drenati; non costituiscono un calcolo dei cedimenti o una diagnosi della compressibilità. La riduzione per densità relativa ammette solo sabbie con c′ nullo e Dr tra 0 e 2/3. I parametri pertinenti compaiono dopo la selezione. Azioni non ammissibili già presenti rimangono visibili per essere corrette.

### Lettura delle viste grafiche

Area efficace mostra l'impronta geometrica, il rettangolo efficace traslato sulla risultante e il contorno tratteggiato della zona compressa. Le dimensioni B′X e L′Y mantengono gli assi della pianta, anche quando i lati vengono riordinati nel calcolo dei coefficienti. L'area efficace A′ della portanza non coincide in generale con la zona a pressione positiva.

Tensioni mostra la mappa in kPa, i valori ai vertici, minimo e massimo; il puntatore legge il campione più vicino. Andamenti presenta due tagli passanti per la risultante. Sono pressioni efficaci nel drenato e totali nel non drenato, coerenti con N assegnato. Il modello convenzionale è affine senza trazione: rappresenta l'equilibrio del contatto, non calcola cedimenti. Il confronto di portanza usa N/A′, non il massimo della mappa.

Rottura mostra un riferimento di Prandtl per striscia su terreno privo di peso, con cuneo e ventagli logaritmici nelle sezioni X e Y. È uno schema illustrativo; non è la superficie critica calcolata per il metodo selezionato, non descrive una rottura tridimensionale e non determina profondità o estensione dell'indagine.

Come controllo delle pressioni, usare B = 2 m, L = 3 m, N = 600 kN, My = 300 kNm e Mx = 0. L'eccentricità X vale 0,5 m; A′ = 3 m². Il contatto triangolare ha larghezza 1,5 m e area compressa 4,5 m², con pmin = 0 e pmax = 266,667 kPa. Il valore N/A′ è invece 200 kPa. Il confronto distingue quindi tre grandezze con significato diverso.

### Esempio verificato e lettura dei risultati

Per il metodo Eurocodice 7 usare B = 2 m, L = 3 m, D = 1 m, φ′ = 30°, c′ = 0, γ = 18 kN/m³ sopra e sotto la base, assenza di falda e sovraccarico esterno, N = 600 kN e altre azioni nulle. Assumere base ruvida, rottura generale e profilo NTC 2018; confermare che i 600 kN siano la risultante A1 completa.

| Quantità | Atteso indipendente |
|---|---:|
| Nq | 18,401122 |
| Nγ | 20,093085 |
| sq / sγ | 1,333333 / 0,8 |
| q efficace | 18 kPa |
| Pressione limite lorda | 730,967360 kPa |
| Area efficace | 6 m² |
| γR | 2,3 |
| Resistenza di progetto | 1906,871374 kN |
| Indice N/R | 0,314651 circa |

Il caso risulta verificato per la sola capacità portante. La tabella dei contributi separa coesione, confinamento e peso del terreno. Nei casi Hansen non drenati la tabella dedicata espone gli incrementi s′, d′ e i′ e il fattore 1 + s′ + d′ − i′: non moltiplicare gli incrementi. La pressione netta è un dettaglio informativo; il confronto del foglio usa la resistenza lorda e la risultante coerente.

### Salvataggio errori e report

Il salvataggio del foglio o del progetto conserva scelte, azioni e nome della combinazione. Dopo una modifica i risultati precedenti sono invalidati; alla riapertura premere Calcola. Report Word e PDF produce le due edizioni affiancate, con dati, coefficienti, esito, campo di applicazione e versione della libreria. F1 o Guida apre questa pagina; la [teoria della portanza](wiki:fondazioni-superficiali) descrive le formule.

Una risultante esterna alla base o azioni oltre il dominio resistente del metodo producono resistenza nulla e, nel profilo normativo, esito Non verificata. Con area efficace nulla la pressione agente è indefinita; con resistenza nulla lo è l’indice N/R. Il foglio mostra un trattino e gli avvisi, senza sostituire tali rapporti con zero. Risultante normale non compressiva, numeri non finiti, dati incoerenti e ipotesi non implementate arrestano invece il calcolo. Le mappe non disponibili sono segnalate. Un messaggio sulla libreria mancante indica che occorre installare il rilascio approvato previsto per il foglio: i dati restano salvabili, ma non viene prodotto alcun risultato.

<!-- pagebreak -->

## Modelli e importazione da MIDAS

Questa procedura riguarda il modulo Modelli in prova, nella versione distribuita con ANTHEA 1.15.0. Il modulo viene sviluppato separatamente: una distribuzione precedente di ANTHEA può non avere i comandi descritti. L'importazione acquisisce una copia del modello aperto in MIDAS Civil NX con i risultati selezionati, di un file di testo MIDAS Civil .mct o MIDAS Gen .mgt, oppure di un modello Straus7 .st7 con i risultati statici lineari .lsa; non esegue l'analisi e non modifica il modello sorgente.

### Entrare nel modulo

In ANTHEA apri **Modelli** dalla navigazione principale. Se non hai ancora un archivio progetti, usa **Nuovo archivio progetti**, crea il progetto e organizza le eventuali fasi o sottofasi; in alternativa usa **Apri progetto…**. Torna a **Modelli** e premi **+ Modello** nella scheda del contenitore desiderato. Puoi arrivare allo stesso spazio anche dal riepilogo della fase o sottofase, con **+ Importa un modello**.

Ogni contenitore può ospitare più modelli e più fogli. Il visualizzatore ha uno spazio dedicato, mentre la copia importata appartiene all'archivio progetti. Un archivio aperto in sola lettura non permette l'importazione. Per il primo modello compare **Importa modello…**; su una copia già presente compare **Aggiorna modello…**.

### Preparare la connessione

Apri in MIDAS il modello da acquisire. Per leggere le sollecitazioni e la deformata occorrono risultati di analisi già disponibili per i casi che intendi importare. Lascia terminare le operazioni in corso e mantieni aperto il modello durante la lettura.

Attiva il collegamento API della sessione MIDAS e recupera i due valori richiesti dal pannello di ANTHEA: **Base URL** e **MAPI-Key**. Usa i valori della sessione corrente, anche se hai già importato lo stesso modello in precedenza. Non modificare il modello e non avviare nuove analisi mentre ANTHEA lo sta leggendo.

### Eseguire la prima importazione

1. Premi **Importa modello…**. Nel pannello seleziona **MIDAS Civil NX · modello aperto** in **Sorgente del modello**.

2. Incolla l'indirizzo in **Base URL** e la chiave nel campo **Chiave MAPI · solo per questa sessione**. La chiave resta nella sessione del visualizzatore e non viene salvata nel progetto.

3. Premi **Controlla connessione e casi**. Attendi il riepilogo del modello e l'elenco dei casi statici e delle eventuali fasi costruttive. Dopo un controllo riuscito i casi risultano tutti selezionati; se cambi URL o chiave devi ripetere il controllo.

4. In **Contenuto da acquisire** scegli **Tutto** per ottenere geometria, attributi, carichi e risultati. Per una prima prova limitata alla forma del modello puoi scegliere **Solo geometria**.

5. Se acquisisci i risultati, lascia attive **Sollecitazioni locali · beam e plate** e **Spostamenti globali · deformata** secondo ciò che vuoi visualizzare. Usa **Tutti**, **Nessuno** o le caselle dei singoli casi. Occorrono almeno un caso e almeno un tipo di risultato selezionati.

6. Premi **Importa dal modello aperto**. Il pannello mostra avanzamento e operazione corrente. **Annulla importazione** interrompe la lettura: una copia precedente valida rimane disponibile, senza un aggiornamento parziale.

7. Al termine controlla il modello, i messaggi di acquisizione e i casi disponibili. Usa **Inquadra** se la geometria è fuori dalla vista. Per assegnare un nome riconoscibile apri **Nome del modello** e premi **Rinomina**.

8. Premi **Salva progetto**. L'acquisizione nel visualizzatore non equivale al salvataggio dell'archivio su disco. Dopo il salvataggio puoi riaprire la copia e visualizzarla senza tenere MIDAS collegato.

Per collaudare il collegamento puoi iniziare con **Tutto**, il solo caso **G1** e i due tipi di risultato attivi. Successivamente ripeti l'aggiornamento scegliendo tutti i casi necessari. Il numero dei casi dipende dal modello aperto: i 49 casi del modello di prova non sono un limite dell'interfaccia.

### Scegliere il contenuto da acquisire

| Modalità | Contenuto della copia dopo l'importazione |
| --- | --- |
| Solo geometria | Nodi, elementi e dati geometrici disponibili per sezioni, spessori, offset e terne. Materiali, gruppi, vincoli e carichi esclusi. **Risultati precedenti rimossi.** |
| Geometria + attributi + carichi | Geometria e dati del modello acquisiti dalla sorgente, senza risultati. **Risultati precedenti rimossi.** |
| Solo risultati | Risultati dei casi selezionati. Richiede una copia già aperta con geometria, attributi e tabelle compatibili. **Risultati precedenti sostituiti integralmente.** |
| Tutto | Geometria, attributi, carichi e risultati selezionati della sorgente. **Risultati precedenti sostituiti integralmente.** |

**La selezione dei casi sostituisce l'intero insieme dei risultati della copia.** Se prima erano presenti 49 casi e ora selezioni soltanto G1, al termine resteranno i risultati di G1. Per conservare l'insieme completo seleziona tutti i casi necessari a ogni aggiornamento. Le modalità prive di risultati eliminano le vecchie sollecitazioni per evitare di mostrarle su una geometria diversa.

### Controllare il modello acquisito

Le schede laterali seguono l'ordine **Vista**, **Modello**, **Combinazioni**, **Risultati**, **Verifiche**, **Report**. In **Vista** scegli rappresentazione e visibilità; in **Modello** esplora le proprietà e controlla i dati acquisiti. Le informazioni presenti nelle tabelle originali possono essere più complete dei simboli già disponibili nella scena.

In **Risultati** seleziona caso, famiglia di elementi, componente e unità. Per controllare un plate scegli, ad esempio, G1, la famiglia plate e Mxx negli assi locali. Confronta un elemento noto, tenendo conto della posizione di lettura e dell'eventuale media ai vertici: un colore mediato non è il valore grezzo di ogni campione. Attiva **Mostra modello deformato** per la deformata; controlla il fattore applicato, perché la scala automatica amplifica gli spostamenti per renderli leggibili.

Le combinazioni vengono acquisite come definizioni. La loro presenza nella scheda **Combinazioni** non significa che tutti i relativi campi di risultato siano già stati scaricati o calcolati. Il calcolo locale richiede i risultati dei casi base richiamati e un tipo di combinazione supportato. Puoi quindi acquisire i casi base e gestire successivamente le combinazioni in ANTHEA.

### Importare e visualizzare i risultati per fase

Se il modello contiene fasi costruttive, il pannello di importazione mostra **Fasi costruttive · stage**. Attiva **Acquisisci anche risultati per fase**, seleziona le fasi e indica le etichette esatte dei passi disponibili nella sorgente. Per esempio, `001(last)` identifica un passo con quella precisa etichetta; non è un valore proposto automaticamente. Per più passi della stessa fase usa il punto e virgola. La durata della fase non permette di dedurre con certezza i numeri dei passi salvati.

Seleziona i casi di risultato degli stage elencati e il riferimento degli spostamenti: **Accumulative**, **Current** oppure **Real**. Il riferimento rimane associato ai dati acquisiti. ANTHEA non somma gli spostamenti di fasi diverse e non ricostruisce i riferimenti mancanti. Per importare soltanto stage puoi deselezionare tutti i casi statici, lasciando almeno una fase, un passo, un caso stage e un tipo di risultato.

Nella scheda **Risultati**, **Ambito dei risultati** separa tutti i risultati, casi statici e combinazioni, oppure fasi costruttive. I filtri della fase e del passo restringono l'elenco dei campi già acquisiti; non interrogano nuovamente il software sorgente. Ogni campo conserva caso, fase, passo e riferimento della deformata. La selezione resta riconoscibile anche nelle viste salvate e nei report.

La geometria completa rimane in contesto. Questa versione non simula l'attivazione degli elementi, dei vincoli e dei carichi lungo la costruzione. Un oggetto privo di risultati non riceve valori nulli inventati; le deformate richiedono gli spostamenti effettivamente importati. Le definizioni delle fasi sono conservate, mentre i risultati di fase sono esclusi dalle somme statiche del gestore combinazioni.

I modelli con risultati per fase usano il formato modello 2. Le versioni precedenti di ANTHEA lo rifiutano; questa versione continua a leggere i modelli statici nel formato 1. **Solo risultati** controlla anche le tabelle delle fasi: se la copia precedente non contiene i metadati necessari, usa **Tutto** e verifica la revisione. Gli export tabellari privi di un contesto stage esplicito non vengono confusi con casi statici.

La lettura per fasi è stata collaudata sul protocollo e su dati sintetici. Il confronto con un modello reale dotato di stage resta da completare.

### Aggiornare una copia esistente

Apri il modello dall'archivio e premi **Aggiorna modello…**. Seleziona la sorgente MIDAS, controlla nuovamente la connessione e scegli contenuto e casi. Per aggiornare soltanto le sollecitazioni usa **Solo risultati**: ANTHEA controlla la corrispondenza dei dati del modello prima di sostituirli. Se sono cambiati geometria, attributi o tabelle, l'operazione può essere rifiutata; usa **Tutto** per acquisire la nuova revisione e controllare le differenze.

Il comando **Revisione** serve a esaminare il confronto disponibile dopo l'aggiornamento; non avvia una nuova lettura da MIDAS. Controlla gli elementi aggiunti, modificati o eliminati e salva il progetto quando hai verificato la nuova copia. Le definizioni di combinazione create in ANTHEA vengono conservate, ma i risultati derivati possono dover essere ricalcolati sui nuovi casi base.

### Aprire un file di testo MIDAS Civil o Gen

1. Premi **Importa modello…** oppure **Aggiorna modello…** e seleziona **File · .antheamodel, MIDAS .mct/.mgt o copia acquisita** in **Sorgente del modello**.

2. In **Contenuto da acquisire** scegli **Tutto** oppure **Geometria + attributi + carichi**; **Solo geometria** limita la copia alla forma del modello.

3. Premi **Scegli il file e importa…** e seleziona il file .mct di MIDAS Civil o .mgt di MIDAS Gen. Il dialogo propone tutti i modelli leggibili; il filtro **File di testo MIDAS Civil / Gen** mostra soltanto questi file.

4. Leggi i messaggi di importazione, controlla il modello e premi **Salva progetto**.

La libreria GPC traduce i comandi del file nelle stesse tabelle della copia acquisita dal modello aperto, in N e mm; il file è letto con la codifica Windows-1252, oppure UTF-8 quando inizia con il BOM. La copia contiene geometria, sezioni, spessori e offset, terne locali e nodali, vincoli, molle, link, rilasci, carichi, casi e combinazioni, con terne e simboli già normalizzati dalla libreria. Prende il nome del file; la provenienza riporta programma, estensione, nome del file e le prime dodici cifre della sua impronta SHA-256.

Il file non contiene risultati e la copia non ne ha: **Solo risultati** è rifiutato e, con **Tutto**, i risultati di una copia precedente vengono rimossi. Restano fuori, con un messaggio, gli elementi senza equivalente nella vista, come pareti, elementi piani o assialsimmetrici e solidi, con i loro carichi e gruppi; i loro nodi restano nella copia. Restano fuori anche le piastre con spessore irrigidito, diverso nel piano e fuori piano o non definito nel file. Le aste di una sezione non definita sono rappresentate ad asse, senza profilo. Se la libreria GPC rifiuta il modello, per esempio per piastre quadrangolari non piane oltre la sua tolleranza o per un'esportazione incompleta, l'importazione è rifiutata e la copia aperta resta com'era.

### Importare un modello Straus7

Per leggere un modello Straus7 serve Straus7 R3 installato sul computer nella versione a 64 bit, con la licenza API attiva. ANTHEA usa St7api.dll dell'installazione, in sola lettura, con il convertitore GPC, in un processo dedicato a 64 bit, ANTHEA.Strand7Worker.exe, installato accanto ad ANTHEA.exe. Modello e risultati restano nei file originali: in Straus7 nulla viene salvato, calcolato o modificato.

1. Premi **Importa modello…** oppure **Aggiorna modello…** e seleziona **Straus7 / Strand7 R3 · file .st7 e .lsa** in **Sorgente del modello**.

2. Con **Sfoglia…** scegli **Modello .st7** e, per **Solo risultati** e **Tutto**, **Risultati statici lineari .lsa** dello stesso modello oppure, per un'analisi costruttiva per fasi, il file dei risultati statici non lineari .nla. **API R3 a 64 bit · St7api.dll** è proposta dalla cartella di installazione, se ANTHEA la trova; altrimenti seleziona St7api.dll nella cartella Bin64 dell'installazione.

3. Premi **Controlla file e casi**. Il controllo verifica che i file esistano e che la libreria sia quella nativa a 64 bit, poi apre il modello, verifica che i risultati corrispondano al modello e ne legge il catalogo: nodi, aste, piastre, casi di carico, combinazioni LSA e casi di risultato statici lineari abbinati ai casi di carico. **Annulla controllo** lo interrompe senza leggere dati. Se cambi un file o la sorgente, catalogo e casi vengono scartati: ripeti il controllo.

4. Scegli il contenuto, i casi con **Tutti**, **Nessuno** o le singole caselle e i tipi di risultato **Sollecitazioni locali · beam e plate** e **Spostamenti globali · deformata**, come per la sorgente MIDAS.

5. Premi **Importa da Straus7**. Il pannello mostra l'avanzamento; **Annulla importazione** chiude il processo di lettura. Un'importazione annullata, fallita o incompleta non modifica la copia aperta. Se i file sono cambiati dopo il controllo, la lettura è rifiutata: ripeti **Controlla file e casi**.

6. Leggi i messaggi, compresi gli avvisi del convertitore GPC, controlla il modello e premi **Salva progetto**.

La copia contiene geometria, proprietà con numero e nome di Straus7, terne locali e nodali, vincoli, molle, link e rilasci. Dei casi scelti acquisisce i risultati statici lineari: piastre ai nodi di ogni elemento, senza media fra elementi; aste alle stazioni restituite da Straus7; spostamenti e rotazioni globali dei nodi. I casi hanno i nomi dei casi di risultato di Straus7, per esempio «1: Dead». Nodi e piastre conservano i numeri di Straus7; le aste diventano 1 000 000 + il numero di Straus7, perché Straus7 numera aste e piastre separatamente. Una proprietà di piastra usata con un altro spessore o offset riceve una proprietà in più, numerata da 1 000 001, con l'offset nel nome. Le sezioni diverse dal rettangolo pieno non hanno volume nella vista.

Le sollecitazioni si leggono con la convenzione della copia MIDAS, uguale per ogni sorgente: Mxx, Myy e Mxy delle piastre e My delle aste hanno il segno opposto a quello che Straus7 mostra per gli stessi risultati, e Mxx positivo tende la faccia −z della piastra. Le unità sono kN/m e kN·m/m per le piastre, kN e kN·m per le aste, m e rad per spostamenti e rotazioni.

Le combinazioni LSA compaiono in **Combinazioni** come definizioni della sorgente, non modificabili; si escludono o si ridefiniscono come quelle MIDAS. I loro risultati non sono letti dal file: li costruisce ANTHEA con la libreria GPC dai casi importati, che devono quindi comprendere tutti i casi richiamati. Le combinazioni ricostruite comprendono lo sforzo normale delle aste reticolari e dei cavi e le reazioni vincolari. Le combinazioni definite su più insiemi di vincoli o su altre combinazioni restano con un avviso.

Controlla file e casi riporta anche il numero delle fasi del modello. Con un file .lsa risolto su una fase attiva il riepilogo lo segnala: i casi sono quelli di quella configurazione e gli elementi non attivi non hanno risultati. Con un file .nla l'elenco mostra uno stato per ogni fase e incremento, con il nome «NLA · fase · incremento». I risultati di un file .nla sono risultati per fase: in **Risultati** si scelgono con **Fasi costruttive** e i selettori di fase e passo, come le fasi MIDAS, e non entrano nelle combinazioni.

Il pulsante **Tabella** offre anche **Reazioni vincolari** del caso corrente, con nel riepilogo la risultante delle reazioni, quella dei carichi e l'esito del controllo di equilibrio, **Masse**, **Temperature** e **Fasi**, con i gruppi e gli elementi attivi di ogni fase. Masse e temperature sono dati del modello: ANTHEA non ne ricava carichi. Le colonne delle tabelle riportano l'unità nell'intestazione.

La lettura riguarda l'analisi statica lineare dei casi primari, anche su una fase attiva, e l'analisi statica non lineare per fasi del file .nla, del quale si legge solo questa. Restano nel file, con un avviso del convertitore, elementi brick, link diversi da quelli rigidi, master-slave e a cerniera, elementi cut-off, contatti e aste di tipo definito dall'utente; la geometria trasformata delle fasi «morphed» non è mostrata e si vede quella iniziale. Le combinazioni della sorgente sono un dato nuovo del progetto: una versione precedente di ANTHEA può non riaprire un progetto che contiene una copia Straus7.

### Risolvere le interruzioni e conoscere i limiti

| Messaggio o situazione | Operazione da eseguire |
| --- | --- |
| Collegamento MIDAS non più disponibile | Riattiva il collegamento API; se la chiave è cambiata incolla quella nuova. Ripeti Controlla connessione e casi. |
| Nessun modello aperto in MIDAS | Apri il modello, attendi che sia pronto e ripeti il controllo. |
| Accesso a MIDAS rifiutato | Verifica Base URL e chiave della sessione corrente. |
| MIDAS non ha risposto entro il tempo previsto | Attendi che MIDAS sia nuovamente disponibile; prova un insieme più piccolo di casi. |
| Importazione o pulsante non disponibile | Verifica che l'archivio sia modificabile, il controllo della connessione sia riuscito e, se richiesti, siano selezionati casi e tipi di risultato. |
| File MIDAS non importato o modello rifiutato dal convertitore GPC | Correggi il file o ripeti l'esportazione da MIDAS; la copia precedente resta aperta. |
| Processo di lettura Straus7 non trovato | Reinstalla ANTHEA: ANTHEA.Strand7Worker.exe deve stare accanto ad ANTHEA.exe. |
| API Straus7 non disponibile | Verifica l'installazione a 64 bit di Straus7 R3, il percorso di St7api.dll e la licenza API, poi ripeti Controlla file e casi. |
| File del modello o dei risultati non disponibili o cambiati | I file sono stati spostati o modificati dopo il controllo: ripeti Controlla file e casi. |

Quando una lettura fallisce, ANTHEA mantiene l'ultima copia valida e indica il recupero possibile. Dopo aver ripristinato la sessione ripeti il controllo e avvia nuovamente l'importazione. Un'operazione annullata o fallita non produce una copia parzialmente aggiornata.

**Limiti della versione di prova.** Il collegamento al modello aperto in MIDAS acquisisce casi statici e stati costruttivi esplicitamente selezionati, le sollecitazioni locali beam e plate e gli spostamenti globali disponibili. Non costituisce un importatore completo di ogni famiglia di risultato. Nel primo import diretto la trasformazione delle tabelle in terne locali, vincoli, rilasci e link del visualizzatore è ancora incompleta: i dati originali vengono conservati, ma i relativi simboli possono non essere presenti. Le informazioni già normalizzate di una copia precedente vengono riutilizzate soltanto quando i dati confrontati coincidono. Leggi gli avvisi prima di interpretare l'assenza di un simbolo come assenza del relativo attributo nel modello. Dai file di testo MIDAS e da Straus7, invece, terne, vincoli, molle, link e rilasci sono normalizzati dalla libreria GPC durante l'importazione.

Per aprire una copia già acquisita scegli **File · .antheamodel, MIDAS .mct/.mgt o copia acquisita**: sono previsti il pacchetto **.antheamodel** oppure **NODE.json** della cartella di acquisizione MIDAS completa. La stessa scelta apre i file di testo MIDAS descritti sopra. Il file nativo del modello MIDAS non si apre direttamente: esporta il modello come testo .mct o .mgt, oppure usa il collegamento al modello aperto.

## Reologia del calcestruzzo

La finestra di supporto calcola il coefficiente di viscosità nel tempo, il suo limite a infinito e la maturazione del modulo elastico. Si apre da **Strumenti → Reologia del calcestruzzo…**, oppure dai materiali e dalle opzioni SLE della sezione in calcestruzzo. Il richiamo dalla sezione propone la resistenza del materiale corrente. Gli altri parametri richiedono la scelta del progettista.

### Impostare materiale ambiente ed età

Inserisci fck in MPa, classe di cemento S, N o R, umidità relativa in percentuale, dimensione nominale h₀ in mm ed età di applicazione del carico t₀ in giorni. La dimensione nominale è h₀ = 2 Ac / u, dove Ac è l'area di calcestruzzo e u il solo perimetro esposto all'essiccamento. La finestra non ricava automaticamente questo perimetro dalla sezione.

L'età t è misurata dal getto, non dall'applicazione del carico. Scegli età iniziale, età finale e risoluzione del campionamento, poi premi **Aggiorna le curve**. Sono ammessi fck fra 12 e 90 MPa, umidità fra 40 e 100 %, h₀ positivo e t₀ almeno pari a 0,5 giorni. L'intervallo deve avere età positive crescenti; la risoluzione è un intero fra 20 e 1000. Sono accettati punto e virgola come separatore decimale, senza separatori delle migliaia.

### Leggere grafico tabella e limite a infinito

Le due schede superiori mostrano **φ(∞, t₀)**, limite analitico, ed **Ecm,28**, modulo secante a 28 giorni. La curva si sceglie fra viscosità φ(t, t₀), modulo Ecm(t) e fattore Ecm(t)/Ecm,28. L'asse del tempo e quello delle ordinate possono essere logaritmici. In scala logaritmica verticale φ = 0 non può essere disegnato; questi campioni restano nella tabella.

La scheda **Lettura puntuale** valuta direttamente un'età positiva, anche se non coincide con un punto del grafico. Nella **Tabella** sono presenti età, φ, Ecm e fattore: selezionare una riga aggiorna la lettura. La risoluzione regola il campionamento grafico; sono aggiunti i punti di carico e a 28 giorni, quando interni all'intervallo. Non regola l'accuratezza delle formule.

La rotella ingrandisce il grafico; trascinando lo si sposta; il doppio clic o **Adatta** ripristina l'inquadratura. **Esporta grafico…** salva una PNG con fondo chiaro, anche se la finestra usa l'aspetto scuro. Il separatore fra grafico e letture consente di ridimensionare i pannelli.

Una modifica a materiale, ambiente, età o campionamento svuota i risultati precedenti finché non si aggiornano le curve. Un'età di lettura errata svuota soltanto la lettura puntuale. I messaggi indicano il campo da correggere; nessun valore non valido è trasformato implicitamente in zero.

### Usare il risultato nel progetto

Ecm(t) è il modulo per maturazione, non il modulo efficace ridotto per viscosità. La finestra non modifica automaticamente φ, n, materiali, combinazioni o revisioni del foglio aperto. Il coefficiente da trasferire alle verifiche richiede coerenza con l'età di carico, la durata e il modulo di riferimento descritti nella [teoria della reologia](wiki:reologia-calcestruzzo).

Esempio: fck = 25 MPa, cemento N, RH = 50 %, h₀ = 200 mm e t₀ = 7 giorni danno φ∞ = 3,442704. A 365 giorni si ottengono φ = 2,603961, Ecm = 33229,780 MPa e fattore E = 1,055724. A 28 giorni il fattore E vale esattamente 1; prima del carico φ vale zero.

Nei verificatori SLE, φ e n errati sono segnalati e i valori derivati sono svuotati. Le staffe richiedono Sì/No e, quando attivi, 2–100 rami interi. NTC espone l'interasse anche nelle sezioni interamente tese.

## Esaminare le sezioni del modello

Nella scheda **Modello**, premi **Sezioni del modello…**. Puoi anche selezionare una sezione nell'albero o un'asta nella scena e premere **Dettaglio sezione…** nell'ispettore. La finestra si apre separatamente dal modello, è ridimensionabile e conserva il collegamento alla vista. L'elenco superiore permette di passare alle altre sezioni della copia salvata.

![Finestra di dettaglio con una sezione composta di esempio](../../src/ANTHEA.Desktop/Assets/Wiki/modelli-sezioni-dettaglio.png)

### Leggere geometria proprietà e attributi

L'anteprima rappresenta il piano locale Y–Z e indica le dimensioni in millimetri. Gli interruttori **Quote**, **Assi locali**, **Assi principali**, **Baricentro** e **Inserimento** sono indipendenti. Il simbolo G individua il baricentro disponibile; O il riferimento dell'asta rispetto al contorno. Nelle sezioni composte G è il baricentro geometrico delle parti, distinto da quello della sezione omogeneizzata.

La scheda **Proprietà** contiene area, inerzie e altre grandezze disponibili. La colonna **Origine** distingue valori calcolati, assegnati, approssimati o non disponibili. Il trattino non equivale a zero. I calcoli numerici differiti, come alcune proprietà di torsione, non vengono avviati per aprire la finestra. Sono indicati anche G e O nel sistema di coordinate dell'anteprima e le dimensioni delle parti riconosciute.

**Dati importati** conserva gli attributi originali, con le unità della sorgente nelle righe UNIT. I rapporti elastici o altri parametri presenti in questi dati non trasformano automaticamente le proprietà geometriche in proprietà omogeneizzate. **Disponibilità** elenca le limitazioni della ricostruzione. Le sezioni definite soltanto da valori numerici possono avere proprietà senza un contorno disegnabile.

### Individuare gli elementi e leggere i volumi

**Seleziona elementi** evidenzia gli oggetti associati alla sezione; **Isola elementi** limita la vista agli stessi oggetti. Per ripristinare il modello usa **Mostra tutto**. Chiudere la finestra di dettaglio conserva la selezione e la visibilità impostate.

In **Vista**, la rappresentazione **Volumi e offset** usa gli stessi contorni del dettaglio 2D, compresi fori e rientranze. Il punto di inserimento è collocato rispetto agli assi locali dell'asta. Se il contorno, il riferimento o un offset di estremità non sono risolti, l'elemento conserva la vista ad asse e la limitazione è segnalata: non viene applicato un centraggio presunto.

Il contouring può essere riportato sul volume dell'asta. Il colore varia lungo i campioni importati della sollecitazione e non rappresenta una tensione distribuita sulla sezione. Le porzioni prive di campioni restano neutre; un unico campione rimane puntuale.

La rappresentazione delle composte comprende il profilo a I con soletta centrata, senza raccordi verticali o irrigidimenti non interpretati. Le altre geometrie conservano gli attributi disponibili e dichiarano i propri limiti. La variazione della sezione per fase costruttiva non aggiorna ancora il volume della vista. Questa finestra è di ispezione e non modifica le sezioni del modello.

## Paratie e sostegni flessibili

Il modulo Paratie è in sviluppo. Questa revisione documenta le funzioni collaudate: analisi piana per fasi, sostegni assiali, carichi distribuiti variabili, falda e filtrazione stazionaria, verifiche idrauliche, stabilità globale per pareti continue e verifiche locali delle sezioni disponibili. La convergenza dell'analisi non certifica tutte le verifiche dell'opera. I campi non disponibili sono indicati negli esiti e nelle limitazioni seguenti.

### Navigare nell’albero di lavoro

L’**Albero di lavoro** sul lato sinistro raccoglie i dati presenti nel foglio: parete e sezioni, terreni, sostegni e relative verifiche, casi e fasi, falda e filtrazione. I gruppi mostrano le righe effettivamente inserite. Passando il puntatore su una voce si leggono i valori e le unità dei suoi campi.

Selezionare una riga per aprirla nell’editor. Se la riga appartiene a una fase, si aggiornano anche il caso e la fase del disegno. Le fasi con lo stesso nome in casi diversi rimangono separate. Sotto ciascuna fase compaiono sostegni presenti, carichi e verifiche riferiti a quella fase. Le righe con fase mancante, inesistente o duplicata sono raccolte in **Riferimenti di fase da completare**: selezionarle per correggere i dati. Un caso citato dalle righe ma assente dalle definizioni è indicato come **non definito**.

Il pulsante **‹** richiude il pannello in una barra sottile; **›** lo riapre. Trascinare il separatore per cambiarne la larghezza. I rami si possono espandere o richiudere singolarmente; modificare o riordinare una riga conserva il ramo e la sua apertura. L’albero si aggiorna anche dopo aggiunte, eliminazioni e riapertura dei dati. La navigazione non modifica gli input né avvia il calcolo.

I nodi **Falda e filtrazione** e **Ricerca dell’infissione** aprono i rispettivi parametri. Il pannello è disponibile anche nelle altre schede del modulo e nei documenti in sola lettura; in questi ultimi gli editor mantengono il blocco delle modifiche.

### Definire il modello e le unità

La scheda **Modello** affianca la sezione grafica e il pannello di modifica: il disegno rimane visibile mentre si cambiano i dati. I pulsanti numerati accompagnano la preparazione: **Geometria**, **Terreno sx/dx**, **Fasi e falda**, **Sostegni**, **Carichi**, **Risultati**. **Tratti / EI** apre le rigidezze della parete; **Presenza nelle fasi** apre installazione e pretensione dei sostegni. Il menu **Altri dati e verifiche** permette di raggiungere anche gli altri editor mantenendo la sezione a fianco. La scheda **Dati e fasi** conserva la vista tabellare completa.

Scegliere caso e fase sopra il disegno oppure usare le frecce precedente e successiva. La sezione mostra strati, quote dei terreni, falde, sostegni presenti e azioni di quella fase, anche prima di calcolare. Fare clic su uno strato, un tratto della parete, un sostegno o un carico per aprirne la riga nel pannello laterale. Le linee del terreno e della falda aprono i dati della fase. Le stesse operazioni sono accessibili con i pulsanti e gli elenchi, senza usare il disegno.

In **Fasi e falda**, **Aggiungi** crea e seleziona una fase del caso corrente con identificativo disponibile. Compilare i suoi livelli: ad esempio, cambiando **Terreno destra** da 0 a −4 m, il disegno rimuove il terreno corrispondente a destra della parete. La nuova fase parte dai valori iniziali del modulo; non duplica automaticamente azioni e sostegni della precedente. Assegnare tutti gli elementi presenti in quella fase; un sostegno rielencato con la pretensione vuota conserva quella della fase precedente. I nuovi carichi e le nuove attivazioni vengono riferiti al caso e alla fase visibili; controllare i riferimenti nell'editor.

![Modifica delle fasi con sezione grafica sempre visibile](../../src/ANTHEA.Desktop/Assets/Wiki/paratie-modello-fasi.png)

La sezione è un'anteprima degli **input**, non un risultato numerico. Quote verticali in scala; spessore della parete, lunghezze dei sostegni e frecce sono simbolici. La linea blu tratteggiata indica la falda assegnata oppure, nel modo FEM, il carico idraulico imposto al bordo: non rappresenta la superficie freatica calcolata. Gli input geometrici non validi sono segnalati e non diventano quote zero. Le deformate e le sollecitazioni richiedono **Calcola tutti i casi**; modificare un input invalida i risultati precedenti.

Aprire il foglio Paratie nel catalogo Geotecnica o aggiungerlo al progetto. In **Modello**, assegnare nome del caso, provenienza delle azioni, quota superiore e inferiore della parete, larghezza della fascia rappresentata e lunghezza massima degli elementi. Le quote sono in metri e crescono verso l'alto. Sinistra e destra si riferiscono alla sezione disegnata. I blocchi di traslazione e rotazione alle estremità sono vincoli fisici assegnati: non usarli per eliminare un meccanismo che il progetto non contrasta.

I campi specialistici sono raccolti nei pannelli **Discretizzazione e vincoli** e **Verifiche e combinazioni**. Per tornare ai dati generali premere **Geometria**. Il divisore fra disegno ed editor è ridimensionabile; il pannello dei campi scorre senza nascondere la sezione.

La tabella **Tratti e rigidezze** richiede quote contigue, identificativo della sezione, peso lineare e origine della rigidezza. EI assegnata è espressa in kNm² e deve riferirsi all'intera fascia. Per una fila unica di tubi CHS o pali circolari in c.a. si può scegliere EI dalla sezione, dopo avere assegnato materiale, geometria e interasse nelle tabelle corrispondenti. La ripartizione uniforme sul fronte deve essere confermata e motivata; l'equivalenza non rappresenta più file o effetto arco.

Le forze concentrate sono in kN e le coppie in kNm. Il carico distribuito sulla struttura è in kN/m di lunghezza verticale, riferito all'intera fascia; il sovraccarico sul terreno è invece una pressione in kPa. Le azioni N, V e M della parete sono quelle della fascia. Le verifiche dei pali, tubi e sostegni mostrano le azioni del singolo elemento e il fattore di trasferimento adottato.

### Riutilizzare la stratigrafia

In **Modello**, aprire **Importa stratigrafia del progetto** nel pannello laterale, aggiornare l'elenco dei terreni, scegliere la stratigrafia, il lato e lo scenario, quindi premere **Importa terreno**. Il trasferimento crea una copia indipendente con provenienza; una successiva modifica della stratigrafia sorgente non aggiorna silenziosamente la paratia.

In **Dati e fasi**, completare le colonne del terreno a sinistra e a destra. Servono pesi naturale e saturo, parametri resistenti, condizioni drenate o non drenate, coefficiente a riposo K0 e modulo di reazione kh con fonte. K0 e kh non vengono dedotti dalla sola litologia. kh è in N/mm³; per l'analisi non drenata occorre cu esplicito. Un dato assente non equivale a zero. La falda è definita separatamente per lato e fase.

### Costruire la storia delle fasi

Ordinare le righe di **Fasi costruttive** secondo la sequenza reale. Ogni fase definisce piani campagna, falde, sovraccarichi, carico uniforme sulla struttura e moltiplicatore di EI. La prima fase costruisce lo stato iniziale idealizzato. Installazione di un contrasto e scavo devono occupare fasi distinte quando il loro ordine influenza il risultato.

Definire geometria e rigidezza dei tiranti e puntoni in **Tiranti e puntoni**. L'inclinazione è positiva verso il basso; interasse e capacità plastica sono riferiti al singolo elemento. **Sostegni presenti nelle fasi** elenca tutti i sostegni attivi, con pretensione. Un sostegno omesso nella fase successiva viene rimosso; una reinstallazione assume un nuovo riferimento. Il sostegno appena installato si riferisce alla deformata già raggiunta, senza azzerare gli spostamenti della parete.

La riga aggiunta con **Aggiungi** nasce con la pretensione vuota. Per un sostegno già presente nella fase precedente la cella vuota conserva la sua pretensione: per mantenere un tirante tesato basta rielencarlo nella nuova fase senza riscrivere il valore. All'installazione, o alla reinstallazione dopo una rimozione, la cella vuota vale 0. Un numero assegna una nuova pretensione (ritesatura) e 0 la rilascia. Il disegno mostra P₀ in vigore nella fase, anche quando è mantenuta, e l'albero di lavoro indica «pretensione mantenuta». Se la pretensione scende rispetto alla fase precedente, il disegno e i risultati lo segnalano, per esempio «T1 da 50 a 0 kN nella fase F3». I documenti salvati prima di questa revisione conservano i valori scritti: uno 0 nelle fasi successive resta un rilascio ed è segnalato.

**Carichi concentrati sulla struttura** contiene quota, forza orizzontale positiva verso destra, coppia e forza verticale positiva verso il basso. **Carichi distribuiti sulla struttura** contiene fase, identificativo, quota superiore e inferiore, intensità ai due estremi e fonte. L'andamento è lineare nell'intervallo; sono ammessi valori negativi e cambi di segno. Gli intervalli sovrapposti si sommano al carico uniforme della fase. Ogni fase deve elencare l'intero campo presente: gli intervalli omessi vengono rimossi. Scorrere l'elenco verticale per raggiungere tutte le tabelle.

### Impostare falda e filtrazione

Con **Idrostatica**, assegnare i livelli di falda nelle fasi; il campo vuoto indica falda assente nel modello. Con **FEM 2D**, completare le estensioni laterali, la quota del fondo impermeabile, lo spessore della parete e il passo della mesh. Per ciascun bordo laterale, in **Bordo sinistro del dominio** e **Bordo destro del dominio**, scegliere **Carico imposto**, la scelta predefinita anche per i file precedenti, o **Flusso nullo**. Con il carico imposto il bordo verticale ha il carico idraulico della fase di quel lato; con il flusso nullo il bordo è impermeabile. Per uno scavo simmetrico largo 2B si modella metà sezione: dal lato dello scavo l'estensione vale B e il bordo è a flusso nullo, perché rappresenta l'asse di simmetria. Con il carico imposto l'asse farebbe da dreno e il gradiente lungo la parete risulterebbe sottostimato. Il carico idraulico della fase va assegnato anche sul lato a flusso nullo: decide se il terreno di quel lato è sommerso o è una superficie di efflusso. Il dominio deve estendersi sotto il piede; la posizione dei bordi va verificata mediante sensibilità.

Le tabelle di permeabilità richiedono Kxx, Kzz e Kxz in m/s e la loro fonte. I parametri non saturi α in 1/m e n sono facoltativi, ma vanno forniti insieme quando si adotta quella legge. La scheda **Filtrazione** mostra pressioni, carichi, bilancio delle portate e diagnostica. Un calcolo idraulico non convergente non fornisce pressioni valide alla fase meccanica.

La scelta **Trascurare la suzione** riguarda il trasferimento meccanico: il risultato idraulico conserva le pressioni negative. Per i pesi del terreno scegliere esplicitamente quota di falda assegnata oppure pressione FEM positiva. Quest'ultima opzione usa una classificazione naturale/satura; non simula una frangia capillare o la consolidazione nel tempo.

### Richiedere le verifiche

Ogni verifica richiede una norma disponibile, una situazione pertinente e dati con provenienza. Le azioni delle sezioni provengono dalla stessa storia non lineare: non si combinano i massimi N, V e M di casi diversi e non si applicano coefficienti alle reazioni già calcolate per trasformarle in un'altra combinazione.

- **UPL / HYD**: definire corpi, azioni caratteristiche o punti e colonne di controllo. Distinguere sollevamento idraulico e sifonamento. Applicabilità del flusso verticale, frontiera e saturazione devono essere motivate. Queste verifiche non coprono l'instabilità geotecnica del fondo scavo.
- **Stabilità globale**: assegnare parete continua, estensioni, dominio di ricerca e classificazione delle azioni caratteristiche. L'adattatore corrente esclude sostegni esterni e superfici che attraversano la parete. Per ogni carico variabile sulla struttura usare l'identificativo `StructureInterval-<Id>` nella tabella delle combinazioni globali. Il risultato espone cerchio, conci, coefficienti e limiti della ricerca.
- **Acciaio sostegni**: completare materiale, sezione piena o CHS, area netta e lunghezze libere d'inflessione quando pertinenti. Il percorso corrente verifica aste assiali statiche; un parametro mancante resta Non valutato anche con forza nulla.
- **Sfilamento tiranti**: definire tratto libero e cementato, indagini per profilo oppure prove preliminari, dati d'iniezione e fonti. Pressione limite pressiometrica pl e pressione d'iniezione pi sono distinte. Una predizione analitica sufficiente senza la conferma sperimentale richiesta resta preliminare.
- **Sezioni acciaio**: le verifiche locali CHS riguardano classi 1 e 2 nel campo statico documentato. L'instabilità dell'intera membratura e i collegamenti sono controlli separati.
- **Pali in c.a.**: scegliere SLU statico oppure combinazione SLE; definire corona di barre, staffe e materiali. Il modello circolare di taglio richiede conferma esplicita. Resistenza nominale e dettagli costruttivi hanno esiti separati: un controllo N–M soddisfatto non nasconde staffatura insufficiente. Restano distinti ancoraggi, giunti, instabilità e dettagli sismici.

### Calcolare e leggere gli esiti

Premere **Calcola le fasi**; l'avanzamento indica fase e iterazioni. **Annulla** interrompe cooperativamente il lavoro e impedisce la pubblicazione di un nuovo risultato incompleto. Una mancata convergenza conserva soltanto le fasi concluse, con diagnostica esplicita; non produce una verifica soddisfatta.

In **Diagrammi e risultati**, scegliere fase e grandezza, poi l'eventuale fase di confronto. Il diagramma **Carico netto** comprende azioni distribuite e reazioni del terreno; non coincide in generale con il solo carico assegnato. I due lati di una discontinuità sono conservati nella tabella. N è positivo a compressione nella parete; nelle verifiche degli elementi il testo indica le convenzioni del verificatore.

![Diagramma del carico trapezoidale e risultati della mensola di prova](../../src/ANTHEA.Desktop/Assets/Wiki/paratie-carichi-distribuiti.png)

Modificare un ingresso rende superati i risultati interessati e disabilita le esportazioni corrispondenti. Il salvataggio conserva input e risultato con la loro impronta; alla riapertura un risultato incompatibile non viene mostrato come corrente. Report Word, PDF e CSV leggono lo stesso risultato dell'interfaccia. Le tabelle sintetiche mantengono le azioni concomitanti; il CSV contiene tutte le sezioni campionate.

### Esempio riproducibile del carico trapezoidale

Assegnare testa a 0 m, piede a −6 m, fascia 2,5 m, EI=200000 kNm² e mesh 0,5 m. Bloccare traslazione e rotazione soltanto al piede; lasciare vuote le due stratigrafie e non assegnare altri carichi o pesi. Nella fase F1 impostare i piani campagna a −6 m e un carico da 1 a 4 kN/m fra −1 e −4,5 m.

Si ottengono spostamento in testa 0,984393229 mm, taglio al piede 8,75 kN e momento al piede 25,375 kNm. La fase F2, senza intervalli di carico, torna elasticamente a zero; F3 con intensità −1 e −4 kN/m dà valori opposti. È una prova analitica del trasferimento delle azioni, non un esempio completo di dimensionamento geotecnico.

### Ricercare l'infissione sulla griglia assegnata

In **Ricerca infissione** assegnare quota di riferimento, infissioni minima e massima, passo, limite dello spostamento assoluto in testa in mm e fonte. L'infissione è positiva sotto il riferimento. Il limite è assegnato dal progettista, senza prescrizioni normative implicite.

Il modello deve coprire la parete più lunga. Ogni candidato taglia i tratti, conserva il terreno sottostante e non estrapola proprietà. Carichi o sostegni sotto il nuovo piede impediscono la valutazione; non vengono eliminati. I vincoli al piede sono esclusi per evitare lo spostamento di un vincolo artificiale.

**Cerca infissione** ricalcola per ciascun candidato tutte le fasi dei casi assegnati, filtrazione FEM e verifiche richieste. Le storie sono indipendenti. Si esplora l'intera griglia senza presumere miglioramenti monotoni; l'estremo superiore è incluso anche quando il passo non divide l'intervallo.

Selezionare una riga per leggere criteri e fase governante. Diagrammi e verifiche principali restano riferiti al candidato accettabile meno profondo, dichiarato nello stato. Il **minimo dimostrato sulla griglia** richiede che tutti i candidati meno profondi abbiano un criterio sicuramente non soddisfatto: una quota inferiore non convergente o non valutabile impedisce questa conclusione. Le verifiche non richieste restano aperte.

**Annulla** conserva solo i candidati conclusi, come risultato parziale senza minimo definitivo. Modificare gli input o chiudere il foglio impedisce pubblicazioni tardive. Salvataggio, riapertura e report usano gli stessi snapshot: il CSV contiene criteri e sezioni di ogni candidato; la relazione include griglia, diagnostica, criteri e risultati del candidato selezionato.

Per riprodurre il benchmark, assegnare testa 0 m, piede iniziale −10 m, fascia 1 m, EI=200000 kNm², peso nullo e nessun vincolo strutturale. Su entrambi i lati: terreno da 0 a −20 m, γ=18 e γsat=20 kN/m³, φ=30°, c′=0, K0=0,5 e kh=0,001 N/mm³, con fonti esplicite; piano campagna 0 m, sovraccarico 100 kPa e falda assente. Tre fasi con forza in testa 0, +10 e −5 kN; mesh 0,125 m, due incrementi iniziali per fase. Ricercare da 3,5 a 10 m sotto quota 0, passo 0,5 m, limite 3,25 mm. Governa la fase +10 kN: u=3,433970786 mm a 6 m e 3,203845845 mm a 6,5 m, primo candidato accettabile. È un benchmark elastico con sola verifica dello spostamento in testa, non un progetto completo di paratia.

![Ricerca dell'infissione e criterio governante nel benchmark elastico](../../src/ANTHEA.Desktop/Assets/Wiki/paratie-ricerca-infissione.png)

### Leggere gli inviluppi delle fasi

Dopo il calcolo aprire **Inviluppi** e scegliere spostamento, rotazione, N, V, M, pressioni o carico netto. Il foglio confronta tutte le fasi concluse dei casi assegnati. Dopo una ricerca dell'infissione, l'inviluppo principale riguarda il candidato accettabile dichiarato nello stato; non mescola pareti di lunghezza diversa.

La curva blu rappresenta il minimo e quella arancio il massimo. La tabella indica quota, lato, valore, caso e fase di ciascun estremo. **Above** è il limite sopra la quota, **Below** quello sotto: i due valori rimangono separati in presenza di forze o coppie concentrate. **Interior** identifica una stazione interna al tratto. La rotazione è espressa in radianti.

Selezionare una riga per leggere le due osservazioni concomitanti. N, V e M della stessa osservazione appartengono alla sua fase, sezione e quota. Il massimo di N può governare in una fase diversa dal massimo di M: non vanno riuniti in una terna artificiale. Le verifiche strutturali restano quelle delle singole fasi. I due estremi globali, con la loro provenienza, sono indicati sopra il grafico.

Il grafico interpola le stazioni restituite dal calcolo, comprendendo discontinuità ed estremi interni. Una storia interrotta rende **parziale** l'inviluppo: le fasi mancanti non sono considerate soddisfatte. Una modifica degli input invalida grafico, tabelle ed esportazione; salvataggio e riapertura conservano lo stesso risultato mostrato. **Esporta CSV inviluppi** include tutte le stazioni e le azioni concomitanti. La relazione riporta gli estremi globali e le fasi incluse, senza ricalcolo.

Per un controllo indipendente usare la mensola di 6 m, EI=200000 kNm², senza terreno, incastrata al piede. La fase A applica H=+10 kN in testa; la fase B sostituisce le azioni con H=−6 kN e coppia +12 kNm, positiva nel verso della rotazione du/d(profondità). Si ottengono spostamenti in testa +3,60 e −3,24 mm; gli estremi globali di M sono +60 kNm in A e −48 kNm in B, entrambi al piede. L'esempio verifica lettura e provenienza degli estremi, senza costituire una paratia progettata.

![Inviluppi di momento delle fasi nel benchmark su fondazione elastica](../../src/ANTHEA.Desktop/Assets/Wiki/paratie-inviluppi.png)

### Gestire casi e combinazioni assegnate

Il caso principale si definisce in **Modello**. In **Dati e fasi → Altri casi / combinazioni** si aggiungono identificativo, provenienza delle azioni e verifiche richieste di ciascun altro caso. Sono ammesse fino a 32 storie indipendenti. Geometria, materiali, sezioni, definizione dei sostegni e parametri del terreno sono comuni; per parametri geotecnici differenti occorrono fogli distinti. Il comando non genera automaticamente combinazioni normative: assegnare azioni e coefficienti già definiti per ogni storia, documentandone l'origine.

Nelle tabelle delle fasi, delle attivazioni, dei carichi e delle verifiche per fase, il campo **Caso** associa la riga alla storia scelta. Vuoto significa caso principale. Ciascun caso richiede tutte le proprie fasi nell'ordine costruttivo; gli identificativi delle fasi possono ripetersi in casi differenti. Una riga non viene copiata automaticamente negli altri casi. Installazione, pretensione, scavo, falda e rimozione dei sostegni seguono esclusivamente la storia dichiarata.

**Calcola tutti i casi** esegue le storie separatamente. Il selettore dei risultati cambia fasi, diagrammi, pressioni e verifiche visualizzati; il confronto fra fasi rimane interno al caso selezionato. **Inviluppi** riunisce invece i casi calcolati mantenendo la provenienza di ogni estremo e le azioni concomitanti. **Esporta tutti i casi CSV** e la relazione includono tutte le storie, indipendentemente dalla selezione corrente.

La ricerca dell'infissione esamina tutte le storie a ogni profondità. Il limite in testa del singolo caso, se compilato, sostituisce quello comune; richiede una fonte esplicita. Le tabelle dei criteri identificano caso e fase governanti. Nel benchmark su fondazione elastica, il caso H=10 kN con limite 3,25 mm richiede 6,50 m; aggiungendo H=15 kN con limite 4,50 mm il minimo comune sulla griglia è 7,50 m.

Un caso non convergente rende incompleto il gruppo e parziale l'inviluppo; gli altri casi possono comunque concludersi. Anche un caso senza fasi meccaniche concluse compare esplicitamente nel CSV. L'annullamento del calcolo ordinario non pubblica un gruppo incompleto; l'annullamento della ricerca conserva soltanto i candidati già conclusi. Una modifica degli input invalida l'intero risultato. Il salvataggio conserva insieme tutti i casi e le rispettive verifiche.

### Funzioni ancora da completare

Restano aperti: sezioni complesse e più file; effetto arco e cedimenti; interazione tra pareti e strutture interne; cerniere plastiche; criteri aggiuntivi per la ricerca dell’infissione; instabilità del fondo e tampone; combinazioni automatiche e sisma avanzato; progetto automatico completo delle armature; DXF e integrazioni esterne. Le opzioni non disponibili non producono verifiche sostitutive. I successivi incrementi saranno documentati dopo il collaudo.
