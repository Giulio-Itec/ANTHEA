# Modulo autonomo Stratigrafia del terreno

Attività richiesta il 15 ottobre 2026: partire da `main` e realizzare il solo modulo
stratigrafico. Base: `9df7e475bc88838930d766ff412b99992907b55c`; branch
`codex/modulo-stratigrafia`. Non avvia la fase F5 del refactoring.

## Confini e contratto

- Modulo `geo_stratigrafia` registrato in `ANTHEA.Application/Modules/ModuleDirectory`.
  Non è un motore e non entra nel catalogo dei calcoli di `X.Calculations`.
- `ANTHEA.Application/Stratigraphy/StratigraphyDocument` gestisce il documento JSON,
  schema 2 (lettura compatibile con lo schema 1), i controlli di input e le operazioni sugli strati. Non definisce leggi
  costitutive, correlazioni, coefficienti normativi o nuovi tipi fisici GPC.
- `X.Desktop/Wpf/StratigraphyWorkspace` presenta i dati e disegna il profilo. Le
  somme degli spessori servono solo alle coordinate del disegno, non a verifiche.
- `ANTHEA.Reports/Generators/StratigraphyReport` produce la scheda dati. Il report
  di progetto riceve i nomi dei moduli dall’Application, mantenendo il comportamento
  precedente per gli altri moduli.
- Salvataggio e riapertura tramite il normale formato ANTHEA, sia come documento
  singolo sia come foglio del progetto. Nessuna migrazione dei profili preesistenti.

Profondità e spessori in m, origine al piano campagna e verso positivo discendente;
quota altimetrica facoltativa distinta. Parametri `gamma`, `gamma_sat` in kN/m³,
`phi` in gradi, `c`, `cu` in kPa. Le proprietà numeriche ammettono numeri JSON o
testo numerico con punto/virgola decimale; null e testo vuoto significano assenza.
Zero resta distinto da assenza. Non si applicano valori geotecnici predefiniti.

Da 1 a 100 strati; spessore positivo per uno strato finito, `infinite=true` e
`thickness=null` per l’ultimo indefinito. Il limite di 100 è un limite applicativo,
non normativo. La geometria deriva esclusivamente dagli spessori ordinati.
Gli strati hanno identificativo stabile, litologia descrittiva e colore RGB.
La duplicazione crea un identificativo nuovo e una copia indipendente; l’ultimo
indefinito non si sposta o duplica. Inserire uno strato lo colloca prima di esso.
I dati dei materiali appartengono allo strato; non esiste un catalogo condiviso.

`soil_type` è una famiglia descrittiva: Non classificato (default e documenti
precedenti), Riporto, Ghiaia, Sabbia, Limo, Argilla, Terreno organico, Roccia, Altro.
Il menu lascia la descrizione libera per miscele e particolarità. Non assegna
parametri; l’importatore propone la famiglia solo dalla descrizione esplicita.
`SoilHatchBrush` disegna simboli schematici propri di ANTHEA con DrawingBrush
vettoriale ripetuto a passo costante in pixel: punti, ciottoli, tratti brevi,
orizzontali, crocette, vegetazione, diagonali o segni a più. Il colore è separato;
contrasto del retino adeguato al fondo. Non sono simboli normativi né una
classificazione granulometrica. Il report espone la famiglia; non incorpora
ancora il disegno del profilo.

La qualificazione riguarda l’insieme dei parametri dello strato; differenze fra
parametri vanno nelle note. La falda ha stati distinti Non nota, Non considerata,
Assegnata; solo quest’ultimo rende attiva la profondità. Il modello esclude falde
sospese, acquiferi multipli e condizioni artesiane. I parametri mancanti sono
avvisi, i dati numerici invalidi e le geometrie non valide bloccano il salvataggio.

## Uso dai moduli geotecnici

I campi scalari dello strato contengono i medi; `minimum` è null oppure un oggetto
con gli stessi cinque parametri, fonte, qualificazione e note. `scenario` ammette
Medi (default anche per i documenti iniziali), Minimi, Entrambi. Nei documenti
iniziali i parametri assegnati sono presentati nella colonna principale: verificare
che la qualificazione pregressa sia coerente con il significato di medio.
`ForVerification(data, required)` richiede sempre i medi pertinenti dichiarati dal
consumatore e anche i minimi se selezionati. Restituisce copie indipendenti per
scenario, senza fallback. Il salvataggio di bozze incomplete resta possibile.
La scelta Entrambi non esegue due calcoli automaticamente: inviare due fogli distinti con scelta Medi e Minimi.

## Importazione AI

`StratigraphyAiImporter` in Application usa Responses API con PDF base64 e JSON
Schema rigoroso, `store=false`, senza tools. Endpoint fisso OpenAI; il client WPF
disabilita redirect. Limiti applicativi: PDF 20 MiB, risposta 4 MiB, 20 profili,
100 strati/profilo, timeout tre minuti; annullamento propagato. Nessun retry
automatico o log del PDF, della chiave o della risposta. HTTP non riuscito,
refusal e risposta incompleta non producono bozze applicabili. Nome modello
configurabile, nessun modello imposto. Chiave solo in memoria; opzionale lettura
da OPENAI_API_KEY e ANTHEA_AI_MODEL. Nessuna chiave condivisa incorporata.

La finestra dichiara trasferimento remoto e possibili costi prima del comando di
invio. Il prompt vieta istruzioni dal documento, inferenze e fusione di sondaggi;
questa istruzione non garantisce correttezza dell’estrazione. I valori estratti
richiedono una fonte non vuota e l’utente confronta pagine, unità e significato.
Geometria incompleta resta correggibile in anteprima ma impedisce l’applicazione.
I minimi assenti rimangono null. Valori non classificabili restano nelle note.
La revisione viene azzerata al cambio profilo o dopo modifiche. Applicare sostituisce
solo dopo l’attestazione esplicita; chiudere mantiene il documento originale.
Persistono nome PDF, SHA-256, modello, data UTC, avvisi e fonti; non il PDF o la chiave.

Riferimenti API verificati il 15/10/2026:
- https://developers.openai.com/api/docs/guides/file-inputs
- https://developers.openai.com/api/docs/guides/structured-outputs

Test del trasporto tramite HttpMessageHandler simulato: nessuna richiesta reale
eseguita durante lo sviluppo e nessuna attestazione di qualità su relazioni reali.
Per il collaudo reale servono un modello abilitato, una chiave e un PDF scelto
dall’utente. Lo schema strutturato non certifica la correttezza geotecnica.

## Contratto snapshot

`StratigraphyDocument.Snapshot(data)` valida e restituisce `StratigraphySnapshot`:
identificativo, impronta SHA-256 del JSON e contenuto immutabile. `Read()` produce
una copia indipendente. Il consumatore potrà salvare id e impronta con il proprio
risultato e confrontarli prima di riutilizzarlo. L’impronta è sensibile anche alla
forma testuale dei numeri: può invalidare cautelativamente un risultato pur con
valore fisico identico.

Il futuro adattatore dovrà usare i tipi `GPC.Model.Geotechnics` già disponibili,
convertire esplicitamente unità e convenzioni e richiedere i parametri pertinenti
alla verifica. Non tradurre una falda ignota in assente, non prolungare un’indagine
finita e non trasformare c′ in cu. Questa attività non collega né modifica i
profili già usati da fondazioni, pali e muri e non aggiorna `lib/Checker`.

## Verifiche e documentazione

`StratigraphyChecks` nella suite Application verifica dati mancanti e zero,
valori non finiti, spessori, identificativi, ordine, copia, semispazio, falda,
snapshot, documenti/progetti e report DOCX/PDF. Le fixture storiche della suite
verificano ancora l’apertura e il round trip dei documenti precedenti.

`Stratigraphy.UiChecks`, registrata nel runner, apre l’editor reale e acquisisce
schermate a 1280 e 900 px nei tre temi. La prova richiede l’attestazione specifica
`controlli stratigrafia`, per non accettare un vecchio eseguibile del banco WPF.

Guide globali Rev46, capitoli della Wiki e indici aggiornati. Rev45 conservata
in `supporto/SUPERATI/stratigrafia-rev46-20261015`, con origine e SHA-256.
La pipeline Word ha verificato 964 frammenti della pratica e 1963 frammenti e
345 formule della teorica, preservando 26 parti ITEC per ciascuna guida.
Il renderer della skill non trova LibreOffice: rendering eseguito con la pipeline
Word esistente e rasterizzazione Poppler. Evidenze in `supporto/artefatti/stratigrafia`
e nelle corse `supporto/artefatti/ci/*stratigrafia*`.

Il primo quick è diagnostico: segnalazioni sulle costanti grafiche e sulla voce
editoriale Wiki corrette, senza eccezioni nuove; durante quella corsa sono proseguite
le modifiche, quindi non costituisce il collaudo finale. La prima corsa UI senza
build ha usato il vecchio banco ed è esclusa dalle evidenze specifiche del modulo.
Il collaudo finale è registrato nel piano di refactoring.

Estensioni AI/medi-minimi/retini: collaudo finale tramite runner
`20261015-221211-stratigrafia-completa-finale`, 11 PASS, zero NEW-FAIL e BLOCKED.
Il quick generale precedente ha 46 PASS e un fallimento del lint editoriale:
il servizio OpenAI era vietato anche nel capitolo della funzionalità richiesta.
L’eccezione è ora circoscritta a quel capitolo operativo e alle sue occorrenze
nell’indice di ricerca; casi negativi verificano che restino esclusi altri
strumenti AI, link e riferimenti fuori ambito. Il banco dei retini distingue
metodi statici e istanza; nessuna modifica ai retini per adattarli ai test.
Campionario, profilo e anteprima controllati visivamente, scheda dati PDF con
medi/minimi controllata. Guide finali: 85 pagine fisiche pratica, 148 teorica;
conteggio delle pagine interne distinto da copertina e indici. Nessun collaudo
API reale e nessun aggiornamento di installer, motori o DLL GPC.

## Coda evolutiva

L’importazione AI è temporaneamente disattivata su richiesta dell’utente:
il pulsante resta visibile ma non premibile, con spiegazione nel suggerimento.
La riattivazione è rinviata alla definizione della configurazione e fatturazione
aziendale. L’inserimento manuale resta disponibile; il codice dell’importatore
è conservato per la successiva riattivazione.

- STRAT-2 — Importazione assistita di relazioni geologiche: implementata su richiesta
  dell’utente del 15/10/2026. Estrarre una bozza modificabile, con pagina e fonte
  per ogni dato, distinguendo sondaggi e profili. Servizio OpenAI remoto, scelta del
  modello e della chiave nella finestra. Collaudo su relazioni reali ancora da svolgere.
- STRAT-3 — Parametri medi e minimi: gestione di entrambi richiesta dall’utente
  il 15/10/2026; implementati inserimento e scelta degli scenari.
  Per ciascun parametro e strato conservare valore medio, minimo e scelta dello scenario,
  unità, fonte e natura del dato (dichiarato, osservato o derivato).
  Prima versione con inserimento esplicito dei valori; successivamente osservazioni
  con profondità, prova, numerosità e criteri di selezione omogenei. Eventuali
  statistiche vanno nelle librerie GPC. Il minimo non è automaticamente un valore
  caratteristico e non è sempre la scelta più cautelativa. Nessuna conversione
  automatica in parametro caratteristico o di progetto. Conservare entrambi gli
  insiemi sulla stessa geometria; l’adozione avviene per verifica/scenario, senza
  sostituire uno dei due con un unico valore globale. I futuri moduli devono poter
  elaborare entrambi e identificare nei risultati l’insieme usato. Separare i
  parametri del terreno dai coefficienti parziali di sicurezza. L’eventuale
  obbligo normativo di doppia verifica va associato al metodo specifico e al
  riferimento verificato, non imposto genericamente. I dati mancanti restano
  mancanti e non sono completati copiando automaticamente l’altro insieme.
- STRAT-4 — Famiglie e retini richiesti e implementati; menu limitato alle famiglie
  prevalenti, descrizione libera per miscele, proposta AI revisionabile. Nessun
  valore geotecnico attribuito dal tipo di terreno.

## STRAT-5 — scambio interno fra fogli

Esteso SoilProfileTransfer con pacchetto ANTHEA.Stratigrafia versione 1 e adattatore
StratigraphyTransfer senza WPF. Il selettore legge gli altri fogli compatibili del
progetto con identità sorgente e sondaggio. Anteprima senza mutazioni, conferma
prima della sostituzione, invio in una nuova scheda o finestra. Copie indipendenti.
Fra stratigrafie si conservano tutti i dati con nuovi identificativi; verso i
moduli esistenti si sceglie Medi o Minimi. I minimi mancanti restano mancanti.
Comportamento Granulare/Coesivo esplicito, falda definita e spessori finiti: nessuna
correlazione o estensione automatica. Nei moduli legacy null è rappresentato da
stringa vuota, come richiesto dal contratto documentale, senza assegnare zero.
Il modulo di destinazione valida i propri limiti e conserva il pacchetto sorgente
come snapshot di provenienza. Il micropalo verticale resta escluso.
Importatore AI visibile ma disattivato su richiesta dell’utente.

Verifica dello scambio: Application 1582 controlli e UI 25 controlli superati.
Prime esecuzioni diagnostiche: namespace mancante nel test corretto; versione
letta esplicitamente per il controllo architetturale; campi null adattati a vuoto
nel formato legacy. Stato della voce STRAT-5 allineato agli stati ammessi dal
registro. Nessuna deroga ai controlli e nessun atteso indipendente modificato.

Chiusura: `20261015-225746-stratigrafia-scambio-conclusione` conferma build,
Application e UI finali; `20261015-230122-stratigrafia-scambio-documenti`
conferma Architecture, Wiki e lint (4 PASS, zero NEW-FAIL/BLOCKED).
Guide Word/PDF controllate, indici aggiornati, edizione precedente archiviata.
