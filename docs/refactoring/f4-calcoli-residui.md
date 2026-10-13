# F4 — calcoli residui nelle librerie

Piano operativo aggiornato il 13 ottobre 2026, dopo la chiusura F3 e il rilascio
ANTHEA **1.7.1**, integrato in `main` con `46b4c918`. Ramo della fase:
`refactoring/f4-calcoli-residui`. La richiesta «poi parti con la F4.
pianifichiamo i punti» supera il precedente arresto; **F5 resta esclusa**.

Questo documento precisa i punti F4.1–F4.16 di [piano.md](piano.md), senza
rinumerare le attività storiche. Stato iniziale: pianificazione e inventario;
nessun trasferimento di calcolo o aggiornamento GPC eseguito in questo passo.

**Istruzione successiva del 13/10:** procedere sugli altri punti, sospendendo
temporaneamente il blocco 5 (F4.11, Bridge Design e prezzari) e la chiusura
F4.16. Finestra operativa di 90 minuti dalle 19:40 circa, ora italiana.
Le nuove catture coprono quindi cinque ambiti; Bridge Design rimane separato.

## Obiettivo e confini

Portare nelle librerie GPC il calcolo ingegneristico ancora attivo in ANTHEA.
Application conserva documenti, migrazioni, validazione degli input, adattatori
e coordinamento; Reports compone i documenti; Desktop disegna e gestisce
l’interazione. Formule, coefficienti normativi, verifiche e ricerca delle
armature non si aggiungono a questi livelli.

Prima di creare un metodo: controllare API esistenti, chiamanti reali, unità,
convenzioni e disponibilità nella DLL distribuita. Un nome simile non dimostra
equivalenza; una API già presente non va riscritta. Gli adattatori si unificano
dove il contratto è lo stesso, mantenendo separati documenti e modelli GPC.

STG-4, SEZ-3b, principali dei plate, prestazioni delle combinazioni, residui F2
e riordino delle cartelle rimangono tracciati nelle rispettive voci. Non si
dichiarano conclusi né si incorporano implicitamente in F4. Se una dipendenza
blocca un punto F4, va indicata su quel punto; gli altri possono procedere.

## Stato verificato all’avvio

- ANTHEA 1.7.1 include il fix M–χ `4a3044cb`; versione `2c06ab46`, merge
  `46b4c918`. Set GPC V invariato, dieci DLL verificate. I 15 PASS del fix e i
  4 PASS del controllo Release restano prove di F3/rilascio, non una baseline F4.
- Inventario ripetuto con `tools/qa/Audit-DomainBoundary.py`: **13 progetti,
  507 voci Compile, 606 sorgenti non di test censiti in Model/Checker**.
  Artefatto: `supporto/artefatti/refactoring/f4/avvio-20261013/inventory.json`.
  È un inventario lessicale e di compilazione, non una certificazione semantica.
- Sorgenti letti: Model `7fd1fe4c`, Checker `75e9adaa`. Nel checkout Checker
  esiste il file non tracciato `diff-piano.txt`, lasciato intatto. Nessuna
  modifica o commit nelle librerie durante la pianificazione.
- `tests/ANTHEA.Architecture.Tests` esiste già: va esteso, non duplicato.
  Il vincolo sul manifest congelato F3 deve diventare un controllo sulla
  provenienza del set della fase; non va semplicemente eliminato al primo rilascio.
- γb dei pali usa già `PileExecution` in `Calcolo.cs`; le decisioni D7-c/D7-d
  sono registrate. F4.5 deve chiudere i residui, non ripetere quelle correzioni.
- `ProjectValidation.CoverChecks` è già in Application; F4.15 riguarda
  uniformità del contratto, riuso del controllo GPC e assenza di regole duplicate.
- `EngineeringOutcome`, `ExecutionStatus` e `DataStatus` esistono in Model,
  rispettivamente sotto `Checking/Contracts` e `Core/Diagnostics`.
- `ElasticPile` dichiara unità forza/lunghezza coerenti. F4.6 deve verificare
  prima le soglie dimensionali e i chiamanti: non si presume necessario
  riscrivere il solutore o rompere una API già indipendente dalle unità.
- `build/known-failures.json` di ANTHEA non contiene eccezioni. Le vecchie
  istruzioni che ammettevano smoke o project-calculations falliti sono superate.
  I fallimenti delle librerie vanno verificati sul loro registro attuale.

## Ordine di lavoro proposto

### 1. Riferimenti e protezioni — F4.1–F4.2

**F4.1:** completare la matrice «funzione attiva → API disponibile → lacuna →
destinazione → prova». Riutilizzare corpus, catture dense, contratti Application
e confronti dei report già esistenti. Aggiungere sorgenti di test soltanto in
`tests/`, collegandoli al runner unico e agli strumenti esistenti.

Congelare sei ambiti dopo F3: strutture dei muri, distinte dei muri, progetto
dei muri, pali elastici e loro armature, Bridge Design, progetto delle armature
di sezione. Coprire materiali, combinazioni, segni, unità, casi limite, dati
mancanti, mancata convergenza e annullamento. Per le ricerche includere assenza
di soluzione e confronto sequenziale/parallelo. Conservare input, risultati,
esiti, report, commit, SHA delle DLL e tempi; gli input devono restare invariati.

Ripetere le catture per dimostrarne la stabilità. Eventuali campi volatili
(tempi, identificativi generati) vanno censiti e confrontati separatamente:
nessuna normalizzazione indiscriminata dei dati ingegneristici. Gli attesi
indipendenti rimangono immutati. I confronti con vecchie catture devono tenere
distinte le differenze già ratificate da quelle nuove.

**F4.2:** estendere i test di architettura esistenti con controlli per formule
e coefficienti fuori dalle librerie, confronti di esito mediante testo,
conversioni senza unità dichiarate, ripieghi impliciti sulla norma e letture
numeriche che sostituiscono dati obbligatori con zero. Ogni eccezione per
geometria grafica o presentazione deve essere puntuale e motivata. I controlli
devono fallire su violazioni introdotte nelle prove; il numero dei residui
ammessi può soltanto diminuire. La scansione automatica affianca la lettura
dei percorsi, senza pretendere di riconoscere da sola tutte le formule.

**Uscita:** matrice verificata, riferimenti ripetibili, elenco dei residui
controllato automaticamente e tempi iniziali registrati. Nessun port del
singolo ambito prima della sua cattura e dei relativi controlli.

### 2. Contratti, coefficienti e unità — F4.3–F4.6

**F4.3:** fissare contratti e dipendenze prima dei port. Proposta: assembly
`GPC.Design` nel repository Checker, a valle di Model e dei checker, nello
stesso treno di rilascio. Nessuna dipendenza inversa da Model a Checker/Design.
Verifiche strutturali CA in `GPCChecker.Concrete/Walls`; geotecnica nei servizi
Walls già disponibili. Per la muratura scegliere la collocazione dopo il
censimento dei metodi, senza introdurre una dipendenza circolare. Input e
risultati GPC tipizzati, senza JSON di ANTHEA né riferimenti WPF.

Distinguere decisioni già prese e decisioni ancora aperte: γb e D7-c non vanno
rimessi in discussione. Per σcp nel taglio dei muri e per eventuali coefficienti
ancora discordanti, preparare fonte, casi numerici ed effetto prima della
decisione sul metodo. La sola equivalenza al legacy non prova la correttezza
normativa. Nessun valore normativo viene modificato nel commit del trasferimento.

**F4.4:** adottare gli esiti comuni di Model in pali e muri. Separare esito
ingegneristico, disponibilità dei dati ed esecuzione. «Non calcolato», errore
e annullamento non possono diventare «verificato»; le traduzioni restano in ANTHEA.

**F4.5:** completare coefficienti provenienti dalle norme, profili di taglio
e override espliciti. Verificare la migrazione γb esistente e conservarne
provenienza e valori scelti dall’utente. Una correzione numerica richiede una
voce nel registro e un commit distinto dal port equivalente.

**F4.6:** inventariare soglie dimensionali e adottare N/mm ai confini dei
calcoli dei pali dove necessario, con conversioni nominate negli adattatori.
Provare invarianti su forze, momenti, rigidezze, spostamenti e rotazioni,
compresi i casi prossimi alle soglie. Conservare il solutore indipendente
dalle unità se questa proprietà è confermata dalle prove.

**Uscita:** contratti senza ambiguità su unità, esiti e norme; equivalenza
dei casi esistenti o differenze intenzionali quantificate e registrate.

### 3. Verifiche dei muri — F4.7–F4.9

**F4.7:** tre commit logici, se separabili: input/risultati e adattatore;
verifiche N–M, taglio, SLE, curvature e minimi mediante API condivise;
collegamento del percorso completo e confronto. I dettagli per pareti e
solette già disponibili in Checker si riusano dopo averne verificato il campo.

**F4.8:** muri a gravità, resistenze assegnate, calcestruzzo non armato,
muratura e dettagli. Coprire entrambi i segni, le facce, le zone e le
combinazioni; uniformare i copriferri al servizio di durabilità già usato.

**F4.9:** collegare i servizi ai moduli e ai report, quindi rilasciare un set
GPC coerente. Confrontare numeri, esiti, rifiuti, figure e documenti con F4.1.
Eseguire le suite delle librerie e il runner ANTHEA `full` dopo l’installazione
del set. Nessuna DLL candidata copiata isolatamente in `lib/Checker`.

**Uscita:** calcolo strutturale dei muri in GPC, ANTHEA limitata ad adattamento
e presentazione; nessuna differenza non spiegata e nessun nuovo fallimento.

### 4. Progetto di armature e distinte — F4.10 e F4.12

**F4.10:** portare in GPC.Design ricerca delle armature dei muri, distinte e
progetto dei tratti dei pali. Prima confrontare `DesignSegment` con i servizi
di selezione già presenti in `PileReinforcement` e unificare il codice comune.
Preservare annullamento, avanzamento, criteri di esclusione, cataloghi e
ordinamento deterministico dei candidati.

**F4.12:** proseguire con la ricerca delle armature di sezione dopo i servizi
condivisi. Preservare l’indipendenza fra stato limite della combinazione e
criterio resistente Plastico/Elastico introdotta in EXCEL-SOL. Risolvere e
collaudare anche l’incoerenza NEd già accodata in F2.8-U4.

**Uscita:** stesso candidato migliore, stessi candidati ammissibili e motivi
di esclusione; quantità intere delle distinte esatte, lunghezze e masse
confrontate con tolleranze motivate prima del port. Tempi e parallelismo
misurati sugli stessi casi. F4.12 segue F4.10 prima di F4.11 per completare
insieme i servizi di ricerca comuni; gli identificativi originali restano.

### 5. Bridge Design — F4.11

Spostare BridgeConcept in GPC.Design con contratti propri, riusando geometrie,
materiali, analisi e verifiche disponibili. Separare cataloghi di famiglie,
prezzi e fattori CO₂ dal motore; i dati sono versionati e gli archivi conservano
il riferimento usato. Prima port equivalente con confronto di quantità,
reazioni, candidati e ordinamento dell’ottimizzazione.

In un commit successivo, attuare la decisione D7-f già registrata sul tandem,
linee di influenza e γQ, con fonti, attesi indipendenti ed effetti misurati.
Confrontare anche tempi di esplorazione e ottimizzazione: non introdurre
la modifica senza averne verificato il costo computazionale.

### 6. Ultimi adattatori e regole residue — F4.13–F4.15

**F4.13:** riesaminare le vecchie voci relative alle viste sul codice dopo F3.
Spostare soltanto la logica ingegneristica ancora presente verso GPC e le
operazioni sui documenti verso Application. Completare anche i contratti di
proposta della geometria di taglio, riferimento della predalle e dati di terreno
riempitivi censiti dall’audit, se ancora attivi e non coperti dalle API correnti.
Nessun ridisegno generale delle viste: appartiene a F5.

**F4.14:** norme tipizzate, input obbligatori validati, default solo dove
dichiarati e appropriati. La migrazione dei vecchi documenti è esplicita e
tracciata; un dato assente non diventa uno zero o NTC per ripiego silenzioso.
I report leggono metodo e norma dal risultato, senza ricostruirli dal testo UI.

**F4.15:** completare il validatore dei copriferri già in Application con
contratto e controllo condivisi fra progetto, foglio, materiali e report.
Formule e coefficienti restano in GPC; nessun secondo calcolo nel validatore.

**Uscita:** mappa dei residui chiusa o eccezioni esplicite, test di architettura
senza arretramenti, nessuna regola ingegneristica duplicata nei punti migrati.

### 7. Chiusura e rilascio — F4.16

Suite complete delle librerie, release set da commit pushati, installazione
con manifest e SHA-256, runner ANTHEA `full`, confronto numerico e grafico,
prove di apertura dei documenti precedenti, report, annullamento e prestazioni.
Aggiornare piano, registro delle differenze, pagine dei metodi, guide globali
Word/PDF e Wiki. Versioni motivate dalle modifiche, senza fissare oggi numeri
GPC che potrebbero essere superati da altri lavori.

La rimozione dei motori legacy rimane subordinata a equivalenza dimostrata e
accordo dell’utente; preparare prima l’elenco concreto e i chiamanti rimasti.
Merge di fine F4 e push seguono le autorizzazioni Git della fase. L’approvazione
del merge 1.7.1 non è un’approvazione anticipata di qualsiasi futuro merge F4.

## Tracciamento e prossimo passo

- [x] F4-P0: piano aggiornato e inventario iniziale sul ramo dedicato.
- [x] F4.1a: [matrice verificata dei servizi/API/prove](f4-matrice-api.md) e copertura mancante; API e chiamanti riscontrati sul set V, nessun calcolo trasferito.
- [x] F4.1b: [catture dei cinque ambiti autorizzati](f4-catture.md), 115 casi, 14 rifiuti, 234 file identici, determinismo con uno/quattro worker e tempi; Bridge Design sospesa.
- [x] F4.2: [controlli automatici estesi](f4-architettura.md), baseline per espressione e 26 prove del rilevatore; 5 PASS del runner.
- [ ] F4.3–F4.15: secondo ordine e criteri sopra descritti, esclusa F4.11 sospesa.
- [ ] F4.16: chiusura sospesa su richiesta dell’utente, anche se altri punti risultassero conclusi.

F4.1 e F4.2 sono concluse nel perimetro autorizzato; si procede con **F4.3–F4.4**. Ogni punto
chiuso ha il proprio commit con evidenze e aggiornamento del piano; un punto
ampio può avere sottopassi autonomi. Dopo ciascuno si comunica all’utente
cosa è cambiato, quali prove sono passate e cosa resta aperto.
