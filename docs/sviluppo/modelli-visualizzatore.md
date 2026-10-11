# Modelli di calcolo: visualizzatore integrato

Stato: bozza di sviluppo, aggiornata l'11 ottobre 2026, branch `codex/model-viewer-draft`.
La base ANTHEA 1.1.0 (`main`, 5c324ef) è stata incorporata con ff9d4c4; il successivo bundle S4/Model 4
di main 735848f è entrato con a83c92d. Alla ripresa dell'11 ottobre, main conteneva già il merge 7852f67
del visualizzatore, con albero identico: il branch è stato allineato a quel commit prima delle nuove aggiunte.
Le aggiunte dell'11 ottobre rimangono sul ramo separato, senza push né nuovo merge verso main.
Questo è un documento di sviluppo; Wiki e guide globali con PDF si aggiornano al rilascio del modulo.

## Organizzazione e confini

Il progetto organizza i contenuti. Ogni progetto, fase e sottofase può contenere più modelli e più fogli.
I modelli sono risorse autonome. La voce Modelli raccoglie quelli dell'archivio aperto e apre la vista
senza il pannello del progetto; la collocazione nella gerarchia rimane conservata.

Un foglio può riferirsi a un modello della propria sezione o di un antenato. Il riferimento conserva identità,
impronta, elemento, caso e componente. Sostituire il modello invalida il riferimento; rinominarlo no.
Duplicare una sezione assegna nuove identità alle copie e aggiorna i riferimenti interni. Spostare un foglio
fuori dal ramo rende visibile il riferimento non risolto. Collegare non trasferisce azioni né esegue verifiche.

| Progetto | Responsabilità |
| --- | --- |
| src/ANTHEA.ModelWorkspace | Snapshot normalizzato, validazione, adattatore offline, archivio e riferimenti; nessuna WPF |
| src/ANTHEA.ModelViewer.Presentation | ViewModel, albero, selezioni, colori, tabelle, unità e comandi; nessuna WPF |
| src/ANTHEA.ModelViewer.Wpf | Controlli XAML, servizi di dialogo, renderer Helix, interpolazione esclusivamente grafica |
| X.Desktop/Wpf/ProjectModels.cs | Navigazione e aggancio al documento, limitati al modulo Modelli |
| tools/ModelViewer.Prepare | Ponte di sviluppo verso un checkout GPC esplicito, fuori dal normale prodotto e dalla soluzione |

Comandi con CommunityToolkit.Mvvm 8.3.2; renderer HelixToolkit.Wpf.SharpDX 3.1.2. Il code-behind collega soltanto
eventi WPF, selezione e ciclo di vita. La chiusura annulla l'importazione pendente e libera le risorse GPU.
Nessun calcolo aggiunto a X.Core, X.Calculations o X.Desktop. Il bundle lib/Checker è quello S4 acquisito da main,
con manifest e DLL invariati rispetto al rilascio; il Converter sperimentale non è distribuito nel bundle.

## Funzioni disponibili

- Albero Modello: nodi, famiglie di elementi, materiali, sezioni, spessori/offset, gruppi, carichi e combinazioni,
  condizioni al contorno, fasi, tabelle originali e rapporto GPC. Ricerca, pagine di 100 elementi e virtualizzazione.
  Il pannello proprietà mostra gli attributi acquisiti e distingue unità normalizzate da quelle originali.
- Sollecitazioni: famiglia, caso, assi locali/principali, componente, unità forza/lunghezza, qualità, isolinee e contesto.
  Plate: otto componenti locali e quattro principali importate. Beam: sei componenti e cinque stazioni.
  Non si ricavano componenti principali dagli estremi non simultanei di un inviluppo.
- Verifiche: riferimento versionato ai fogli e abbozzo della configurazione futura. Non esegue verifiche.
- Geometria, volumi con offset, risultati; selezione dalla scena tramite ray picking, Ctrl per aggiungere/rimuovere.
- Isola, nascondi, mostra selezione, mostra tutto e inverti visibilità. Il wireframe degli oggetti nascosti
  è soltanto contesto e non è selezionabile. L'interruttore wireframe non cambia risultati o scala;
  la media ai vertici, quando attiva, usa il vicinato del modello intero. Inquadratura con inclusione dei nascosti opzionale.
- Filtri per famiglia, materiale, sezione, spessore e gruppo, combinabili per intersezione o unione.
  Gli insiemi vuoti restano vuoti. I comandi espliciti di isolamento/inversione sostituiscono i criteri attivi;
  scena e tabelle seguono la stessa visibilità. «Mostra tutto» azzera filtri e nascondimenti.
  Visibilità con nome memorizzabili e richiamabili nella sessione; contengono gli ID, non camera o risultati.
  La sostituzione del modello e la chiusura della vista le azzerano; nomi duplicati non sovrascrivono altre viste.
- ID di nodi, elementi e proprietà, su tutti i visibili o soltanto sulla selezione; dimensione testo e linee.
  Le etichette sono in primo piano; su zone dense è utile limitarle alla selezione o ingrandire.
- Colori uniformi, per tipo, materiale, sezione/spessore e gruppo. Legenda stabile rispetto ai filtri.
  Per appartenenze multiple prevale il gruppo con meno elementi; parità risolta per ID crescente.
- Assi locali da GPC, restrain, constraint, link e release. Simboli distinti, senza sostituzioni meccaniche.
- Preview proporzionata della sezione rettangolare, proprietà ridimensionabili, tema chiaro e scuro.
- Tabelle dei dati salvati: nodi, elementi, sezioni, spessori/offset, vincoli, risultato corrente e attributi.
  Tipi numerici ordinabili, ricerca, filtri di visibilità/selezione, riga collegata alla scena, copia Ctrl+C e CSV.
  Il risultato corrente usa lo stesso caso, componente e unità della vista, conservando sempre i campioni
  originali per elemento, anche quando la grafica usa la media ai vertici.
  CSV con separatore punto e virgola, decimale invariant, valori numerici integri e testi protetti da formule.
  Disattivando sia Visibili sia Selezionati, sezioni e spessori includono anche le definizioni non assegnate.
- Esportazione PNG del controllo completo, comprendente scena Direct3D, titolo, legenda e opzioni.

## Copertura e limiti dei dati

Snapshot schema 1: coordinate in m; plate in kN/m e kN·m/m; beam in kN e kN·m.
Campi, terne, tabelle e diagnostica sono estensioni opzionali omesse dalla serializzazione delle vecchie copie,
per conservarne l'impronta. Gli archivi .programma includono la copia; non contengono credenziali.

Geometrie rappresentate: plate triangolari/quadrilatere e linee a due nodi BEAM, TRUSS, TENSTR, COMPTR, CABLE.
Le formulazioni restano distinte. Il contratto dei risultati truss/affini ammette solo Fx locale, se importato.
La presenza di un contratto di visualizzazione non certifica l'adattatore di ogni software:
il modello reale collaudato contiene soltanto BEAM e PLATE, oltre ai link nelle tabelle di assegnazione.
Formulazioni e connettività diverse sono rifiutate esplicitamente; non si eliminano entità silenziosamente.

Preview solida: plate con spessore fisico unico e offset; aste con sezione rettangolare piena centrata.
Le altre sezioni restano ispezionabili in connettività, con dati originali; nessun rettangolo equivalente inventato.
Per cavi e formulazioni speciali la connettività non rappresenta la forma di equilibrio.
Offset di estremità, profili generici/variabili e assi mancanti richiedono il completamento del ponte GPC.
Le facce interne coincidenti di plate coplanari della stessa proprietà sono omesse solo quando entrambe visibili.

Contouring: scelta fra valori distinti per elemento (predefinita) e media ai vertici condivisi delle plate;
interpolazione bilineare dei quad e lineare dei triangoli, qualità 1/4/8 suddivisioni, palette continua,
isolinee alimentate dallo stesso campo grafico. La suddivisione è grafica,
non accresce l'accuratezza del solutore. Beam: segmenti colorati tra campioni importati, senza inventare valori
interni oltre la rappresentazione lineare; la scelta di una stazione colora l'asta con quel campione.

La media è aritmetica, con uguale peso per ogni elemento adiacente con tutti i campioni ai vertici disponibili.
Si condivide solo l'ID nodale, senza saldare nodi coincidenti distinti, e si separano proprietà (anche spessori/offset)
e materiali acquisiti. Le terne locali importate devono coincidere entro 1e-6 per componente cartesiana:
non si ruotano tensori né si scambiano assi, anche quando le componenti selezionate sono principali importate.
Senza terna il contributo resta originale; una plate con campioni incompleti resta senza contour e non influenza
le adiacenti. Se il materiale non è importato non si può distinguere il confine materiale: restano le altre separazioni.
Il vicinato include gli elementi nascosti, quindi a scala fissa isolare o nascondere non modifica i colori rimanenti.
La legenda indica «MEDIA AI VERTICI» e le quattro modalità di scala usano il campo effettivamente disegnato;
il pannello distingue l'intervallo grafico da quello dei dati importati. Tabelle, query, CSV, riferimenti e snapshot
restano originali. Negli inviluppi la media degli estremi è solo grafica e non acquisisce simultaneità.
Il servizio di presentazione senza WPF conserva una sola cache di campo mediato; la sostituzione del modello
invalida campo e vicinato. Non vengono aggiunti calcoli ingegneristici o modifiche alle librerie GPC.

Collaudo MOD-08: 129 controlli di logica e 74 WPF sul modello reale. Attesi manuali sui nodi condivisi,
triangoli/quad, proprietà, materiali, terne e dati incompleti; nel buffer GPU due campi costanti 10 e 30
condividono l'intero bordo al valore 20 (UV 0,5). Copia della spalla: 3.531 vertici mediati e 8.244 contributi
modificati per Myy su 13.718; prima attivazione 53,51 ms per preparazione sincrona del campo e della scena,
esclusi completamento GPU e acquisizione. Le immagini prima/dopo usano la stessa camera e scala.
Evidenze in `supporto/artefatti/model-viewer/nodal-averaging`. La preview accetta `--averaged` per partire
con questa opzione, sempre modificabile dalla scheda Sollecitazioni o dalle impostazioni vista.
Runner unico: quick `20261011-111122-modelli-media-vertici`, 27 PASS e nessun nuovo fallimento;
suite WPF sintetica `20261011-111314-modelli-media-vertici-wpf`, 32 controlli superati senza errori di binding.

Scale dei colori: automatica sui visibili, globale sul caso/componente/stazione, simmetrica rispetto a zero,
oppure manuale. La scala globale permette di confrontare sottoinsiemi senza ricolorare lo stesso valore.
I limiti manuali rimangono costanti cambiando caso e si convertono con le unità; cambiare componente/famiglia/assi
riporta all'automatico. Input non finiti o invertiti sono rifiutati conservando la scala precedente.
La saturazione grafica avviene al pixel dopo l'interpolazione, senza troncamento dei valori ai vertici;
le etichette ≤/≥ segnalano gli estremi saturati. Tabelle, interrogazione e snapshot restano integri.

I risultati della copia MIDAS mantengono componenti e segni MIDAS, convertendo solo le unità.
Non sono risultati già normalizzati alle convenzioni del Checker GPC (in particolare il momento locale beam).
Gli estremi del caso SLU_Q1_1(max) sono importati, non simultanei e non calcolati dentro ANTHEA.
Non è certificata la compatibilità analisi/modello per le verifiche. La scheda Verifiche conserva solo riferimenti.

Le assegnazioni non interpretabili restano nei record originali e nella diagnostica. Il simbolo non sostituisce
l'ispezione dei gradi di libertà. La copia di prova contiene un vincolo con settimo DOF: la vista mostra i sei DOF
supportati e mantiene il dato warping originale con avviso.

Navigazione (Modello, Sollecitazioni, Verifiche) e proprietà si riducono separatamente a barre da 44 pixel.
Le schede si riaprono direttamente dalla barra, conservando selezione e visibilità. Il menu contestuale
dell'albero seleziona la voce sotto il puntatore, anche nei rami annidati: esamina gli elementi associati,
isola, nascondi, mostra selezione, mostra tutto. L'esame apre proprietà e tabella degli elementi associati.

Assi nodali e degli elementi, restrain, constraint, link e release hanno interruttori indipendenti.
L'ambito è tutti gli oggetti visibili oppure la sola selezione; un nodo selezionato mostra la propria terna,
non quelle degli altri nodi degli elementi adiacenti. Un comando spegne insieme questi sei livelli.
Assi globali e cubo di orientamento si disattivano separatamente nelle opzioni di visualizzazione.
Le terne nodali sono dati opzionali espliciti, validati e salvati nello snapshot; la loro assenza non modifica
l'impronta delle copie precedenti. Il ponte usa gli orientamenti già interpretati da GPC, esclude quelli con
diagnostica di orientamento irrisolto e usa la terna globale solo per nodi senza assegnazione in una SKEW acquisita.
Se SKEW non è stata acquisita, le terne nodali rimangono non importate. La UI non ricava orientamenti dalla mesh.

## Ponte GPC e acquisizione

Autorizzazione successiva dell'utente: completare dove necessario release e link in Model, sempre su branch separato.
Il dominio GPC disponeva già dei contratti. Il branch `codex/viewer-boundary-data` nel checkout ModelViewerBoundary
aggiunge il mapping Civil NX FRLS/RIGD/ELNK nel Converter 2.0.2; commit 14a0c859.
Non modifica il checkout Model con le sezioni in lavorazione, né distribuisce nuove DLL in ANTHEA.

`tools/model-viewer/Read-MidasSnapshot.ps1`: acquisizione HTTPS in sola lettura e verifica di stabilità delle
tabelle geometriche principali. `Read-MidasResults.ps1`: lettura delle tabelle beam/plate in blocchi da 500.
La chiave è un parametro runtime, mai salvata. Nessun comando di analisi o modifica del modello.
La corrispondenza con la revisione dell'analisi richiede ancora il contratto di provenienza definitivo.

`tools/ModelViewer.Prepare` richiede GpcModelRoot esplicito. Legge la copia, esegue il mapping completo GPC,
ricava le terne dal dominio e conserva la diagnostica nel pacchetto. Non appartiene alla build ordinaria.

Il pacchetto `supporto/artefatti/model-viewer/midas-complete/spalla-validata.antheamodel` contiene:
3.755 nodi, 181 beam, 3.435 plate, 3.616 terne, un restrain, 381 ELNK rigidi, 1.263 relazioni master/slave
da due record RIGD. Risultati: 13.718 campioni plate e 905 campioni beam (cinque stazioni per asta).
Due materiali, due sezioni, sette spessori, 21 gruppi, 49 casi statici e 73 combinazioni sono ispezionabili.
FRLS e altri insiemi realmente vuoti restano distinguibili dagli insiemi non acquisiti.

Il pacchetto successivo `panels-node-axes/spalla-assi-nodali.antheamodel`, ottenuto dalle stesse fonti salvate,
aggiunge le 3.755 terne nodali. Nel modello reale SKEW è vuota e le terne sono globali; una fixture indipendente
con X=(0,1,0), Y=(-1,0,0), Z=(0,0,1) verifica in WPF l'orientamento ruotato.

Il pulsante «Aggiorna modello» della bozza rilegge un pacchetto .antheamodel o una copia MIDAS già acquisita;
non è ancora collegato direttamente a MIDAS/Straus. La rilettura dal software sorgente è il flusso previsto.
Modalità concordate con l'utente l'11 ottobre:

| Modalità | Contenuto acquisito | Uso |
| --- | --- | --- |
| Solo geometria | Nodi, elementi, connettività e orientamenti geometrici | Vista leggera di nodi, elementi e mesh, senza richiedere risultati |
| Geometria + attributi + carichi | Geometria, materiali, sezioni/spessori/offset, vincoli, collegamenti, gruppi e carichi | Esame completo del modello di input |
| Solo risultati | Campi disponibili e relativa provenienza dell'analisi | Aggiornamento delle sollecitazioni sulla medesima revisione del modello |
| Tutto | Modello di input completo e risultati disponibili | Aggiornamento completo della copia |

Le quattro modalità sono specifica del prossimo flusso, non opzioni già operative del pulsante della bozza.
Il primo caricamento dei soli risultati richiede un modello compatibile già presente. Una variazione geometrica
deve invalidare i risultati precedenti incompatibili e segnalare i riferimenti ai fogli da aggiornare.
Anche attributi/carichi appartengono alla revisione di analisi: la sola geometria coincidente non basta a
certificare la validità dei risultati. L'aggiornamento parziale deve conservare o invalidare esplicitamente
gli insiemi non riletti; una vista geometrica non inventa sezioni o spessori per ottenere una preview solida.
Nessuna analisi del solutore viene avviata dalla semplice rilettura.

## Collaudo e prestazioni

- Runner `build/ci.ps1 -Profile quick -Tag modelli-workspace-v2`: 25 PASS, nessun nuovo fallimento.
- ModelWorkspace: 46 controlli rapidi, inclusi colori sovrapposti, isolamento, unità, campioni, tabelle,
  export, distinzione mancante/vuoto e rifiuto di momenti su truss.
- WPF sintetico integrato: 20 controlli, comprese quote offset -0,85/-0,55 m attese indipendenti e valore bilineare 25.
- WPF sul modello completo: 38 controlli su ray picking, tre viste, sezioni, assi/vincoli, principali, beam, ID, colori, tabella,
  export e temi. Screenshot reali in `supporto/artefatti/model-viewer/delivery`.
- GPC: 90 test mirati di importazione, assegnazioni e persistenza. Gli svincoli e link sopravvivono
  all'archivio esplicito e contribuiscono all'impronta degli input; errori atomici e leggi non supportate verificati.

Completamento del collaudo S4, verificato alla ripresa dell'11 ottobre:
- `build/ci.ps1 -Profile full -Tag modelli-s4-completo` su a83c92d: 79 PASS, 1 KNOWN, 0 NEW-FAIL, 0 BLOCKED.
  Il KNOWN è il confronto dei Guid dei default dei muri, già censito in build/known-failures.json.
  Le tre suite non eseguite dal runner restano quelle esterne/infrastrutturali indicate nel suo riepilogo.
- GPC sul ramo del Converter: UnitTest 1024 PASS (escluse le due prove native Straus7), Model.Core.Tests 6 PASS,
  ModelChecker.Tests 166 PASS; totale 1196. Evidenze in `gpc-boundary/model4-completo`.
- Prova reale con S4: 38 PASS, immagini in `delivery-s4`.

Aggiunte dell'11 ottobre: ModelWorkspace 67 PASS, WPF sintetico 21 PASS, WPF reale 44 PASS.
Runner quick `modelli-filtri-scale`: 25 PASS, nessun nuovo fallimento (11 ottobre).
Evidenze in `supporto/artefatti/model-viewer/visibility-scale`: `logic`, `synthetic`, `real`.
La prova `before-table-fix` cattura il difetto delle proprietà non assegnate prima della correzione;
gli attesi di offset e interpolazione esistenti non sono cambiati. Registro MOD-03.

Estensione barre laterali e terne nodali: ModelWorkspace 78 PASS sulla copia reale (76 senza fonti esterne),
WPF sintetico 27 PASS e WPF reale 49 PASS. Evidenze `panels-node-axes/logic`, `synthetic-final`, `real-final`.
Le prove verificano riduzione di entrambi i pannelli, comandi contestuali sul ramo annidato, tabella associata,
terna nodale ruotata, ambito della selezione e spegnimento dei livelli; nessun errore di binding WPF.
Le prove intermedie hanno evidenziato la necessità di usare l'evento mouse tunneling nel test contestuale
e attendere il layout dopo la riapertura della scheda nel runner. Attesi numerici precedenti invariati. Registro MOD-04.
Runner quick `modelli-barre-assi-finale`: 25 PASS, nessun nuovo fallimento e uscita 0.

Sei cambi di componente sul modello completo già caricato, qualità Alta: da 98,97 a 122,22 ms,
mediana 108,70 ms fino all'evento Viewport.OnRendered. La misura include cambio dello stato, preparazione
e notifica del fotogramma; non misura il monitor fisico, l'acquisizione, il solutore o il calcolo dell'inviluppo.
I dati grezzi sono in `review-measured/tempi-campi.json`. Non è una garanzia per modelli arbitrariamente grandi.
Ripetizione con terne nodali e barre laterali, 11 ottobre: 120,74–150,78 ms, mediana 130,64 ms,
stesso ambito di misura; dati in `panels-node-axes/real-final/tempi-campi.json`.

## Passi successivi

### Impostazioni vista e aggiornamenti interrotti — 11 ottobre

Menu Impostazioni vista: sfondo libero/preset e contrasto delle etichette, sensibilità separate di rotazione,
pan e zoom, rotazione con verticale Z stabile o libera, inerzia e centro sul punto cliccato opzionali,
proiezione ortografica, sei viste standard e quattro viste con azimut/elevazione a 45°. Il cubo permette
anche i clic sugli spigoli. Antialiasing disattivato/FXAA/MSAA 4×/8× (nei limiti della GPU), materiali piatti,
shaded o PBR con occlusione ambientale per la geometria. Risultati con palette continue e isolinee opzionali,
con media fra elementi aggiunta successivamente in MOD-08. Esc deseleziona. Persistenza delle impostazioni
grafiche nelle viste e ricette aggiunta nei passi MOD-09/10.

Il contratto di errore della sorgente distingue sessione MIDAS assente, documento chiuso, accesso rifiutato,
timeout e indisponibilità. La vista espone il motivo e il recupero, conserva l'ultima copia valida e non
applica importazioni parziali. I corpi HTTP e le credenziali non diventano testo della vista. Il collegamento
diretto MIDAS nel prodotto è ancora da integrare: il banner è collaudato con un adattatore simulato, mentre
le acquisizioni reali di questa fase usano gli strumenti di laboratorio.

Preparato nella libreria senza WPF il confronto di snapshot per ID, con tolleranza sulle coordinate,
attributi, aggiunte/rimozioni e delta di un campo esplicitamente richiesto. La visualizzazione dei cambiamenti
e la scelta della copia di riferimento sono state collegate in MOD-19; resta successivo l’archivio storico
completo delle copie nel progetto.

Collaudo: 105 controlli ModelWorkspace, 65 WPF sul modello reale, senza errori di binding. Nuove prove per
menu, camera, proiezione, palette, Esc e banner. Evidenze `supporto/artefatti/model-viewer/base-case-benchmark/ui`
e runner quick `20261011-104113-modelli-casi-base` (27 PASS, nessun nuovo fallimento).
Nella prova WPF, sei cambi di componente già caricato fino a OnRendered: mediana 92,89 ms, massimo 104,79 ms;
acquisizione e costruzione delle combinazioni escluse. Registro MOD-06.

La modalità `--viewer-only --interactive` del runner ospita il controllo reale in una finestra utilizzabile,
senza eseguire le suite. Una barra del solo host di prova apre le impostazioni e permette di simulare
i cinque avvisi MIDAS, sempre dichiarati come simulazione e senza richieste di rete. La copia importata
e le sue sollecitazioni rimangono consultabili. Acquisizioni e collegamenti restano nella memoria della
finestra; il file sorgente non viene riscritto. La preview registra una schermata e il segnale di avvio
in `supporto/artefatti` per confermare che la vista e il renderer siano effettivamente pronti.

### Assegnazioni e avanzamento delle verifiche

Impostazione proposta: assegnazione esplicita fra un ambito geometrico (elementi selezionati, gruppo o zona)
e una sezione di verifica con materiale, armature e metodo. La sezione dell'analisi e i dati aggiunti per la
verifica restano distinti; incongruenze di geometria, orientamento o revisione vanno esposte, non corrette
silenziosamente. Per le aste: asta/tratto e stazioni; per le superfici: zone di verifica e, separatamente,
sezioni di taglio con risultanti elaborate da GPC. Un albero delle assegnazioni collegherà selezione grafica,
isolamento, definizione e riferimenti ai fogli. Oggi è operativo solo il riferimento elemento–foglio:
nessun assegnatore o nuovo calcolo è incluso in questa bozza.

Su richiesta dell'utente si prepara intanto l'avanzamento: barra persistente sotto la scena, visibile anche
con entrambi i pannelli chiusi, attività corrente, conteggio, annullamento e chiusura dell'esito.
La fase iniziale è indeterminata; la percentuale usa soltanto attività completate/totali comunicate dal runner.
Il ViewModel offre un aggancio asincrono senza motore, svolge il lavoro fuori dal thread grafico e riporta
le notifiche sul contesto della vista. Si impediscono doppie esecuzioni e sostituzione del modello durante
il lavoro; la chiusura della vista richiede annullamento e scarta notifiche tardive. Una richiesta di
annullamento resta in attesa finché termina il lavoro; non equivale a un'interruzione forzata.
«Esecuzione completata» non significa «verifiche soddisfatte». Errori e annullamento non forzano il 100%.
Il pulsante Esegui resta disabilitato fino al collegamento del motore GPC; nessuna verifica simulata nel prodotto.
La cattura `verification-progress/validated/modelli-anteprima-avanzamento.png` è esplicitamente un'anteprima
grafica alimentata dal test, senza calcolo ingegneristico.
Collaudo: 87 controlli ModelWorkspace (11 aggiunti per il ciclo di esecuzione), 56 WPF sul modello reale,
senza errori di binding. Evidenze in `verification-progress/logic` e `verification-progress/validated`.
Runner quick `modelli-avanzamento`: 25 PASS, nessun nuovo fallimento e uscita 0.
Le prove preliminari hanno corretto la risoluzione delle risorse del nuovo controllo e il test di lancio:
il runner entra tramite Dispatcher come il futuro comando WPF, preservando il contesto della presentazione.

| Priorità | Lavoro rimasto | Sede |
| --- | --- | --- |
| 1 | Rilascio del Converter con le nuove assegnazioni da commit pushati e integrazione del ponte nel prodotto; S4/Model 4 è già acquisito da main | Librerie / build ANTHEA |
| 1 | Collegare Aggiorna modello ai software sorgente, controllare revisioni e invalidare risultati/riferimenti incompatibili | GPC / libreria applicativa e vista |
| 1 | Adattatore comune definitivo per tutti i solutori; copertura esplicita di solidi, cavi, link non lineari, PRLS e fasi | GPC Model / Converter |
| 1 | Profili solidi generici/variabili, offset di estremità e sezioni orientate, provenienza delle convenzioni | GPC Geometry/Model e renderer |
| 1 | Combinazioni e inviluppi locali con caso governante, coerenza simultanea e cache | GPC, non WPF |
| 1 | Archivio compresso/grandi dati e confronto completo delle revisioni modello | Librerie documenti/progetti |
| 2 | Ampliare i carichi grafici a HYDRO, carichi variabili/proiettati/eccentrici e altre formulazioni; importare spostamenti e tensioni con provenienza esplicita | Contratti GPC e adattatori |
| 2 | Controlli ingegneristici, editor del modello e trasferimento controllato ai fogli | GPC e libreria applicativa |
| 2 | Ordinamento avanzato, grafici tabellari, soglie arbitrarie delle singole fasce e preselezione delle facce solide con offset | Modulo Modelli |
| 2 | Ampliare prove su software reali diversi, modelli grandi, DPI multipli e accessibilità | Test |
| 3 | Guide globali/Wiki/PDF e distribuzione dopo il collaudo del modulo | Documentazione |

Il formato .antheamodel resta un contratto temporaneo di presentazione, non il formato definitivo GPC Model.

Per la scelta fra importazione di tutte le combinazioni e soli casi base, vedere il
[benchmark sul modello completo dell'11 ottobre](modelli-benchmark-casi-base.md): 49 casi acquisiti,
73 definizioni ricostruite in un prototipo separato, tempi e limiti di validazione espliciti.
Nessun dato di calcolo legacy è stato spostato o sostituito; confronto delle revisioni e archivio grandi dati
restano prerequisiti per distribuire il modulo negli archivi operativi.


## Esplorazione e disposizione delle schede — 11 ottobre 2026

La revisione MOD-09 parte dal commit 662e9a7. Le verifiche ingegneristiche rimangono nella loro
predisposizione precedente. I comandi operativi sono organizzati per l'oggetto che modificano:

| Posizione | Contenuti |
| --- | --- |
| Modello | Albero e proprietà, selezione per ID/intervalli, filtri, carichi importati, assi e condizioni al contorno |
| Risultati | Caso, famiglia, assi, componente, unità; media ai vertici; palette continua o 8/12/20 fasce e inversione; scala; estremi e letture fissate; selezione per valore; diagrammi, deformata e confronto |
| Vista | Camera e stile, opacità, taglio X/Y/Z o tre nodi, lato visibile, viste persistenti ed esportazione |
| Barra della scena | Clic/rettangolo/poligono, tipo di oggetto, isolamento, nascondi/mostra, visibilità precedente e zoom selezione |
| Legenda | Valori alle estremità e quartili, precisione, saturazione, unità, natura mediata/originale e accesso alla scala |

La barra laterale ridotta conserva tutte e quattro le schede. La barra superiore mantiene accessibili le
impostazioni generali, gli oggetti e le tabelle. La selezione è indipendente dallo strumento di navigazione:
Ctrl aggiunge e Shift sottrae; rettangolo da sinistra per contenimento, da destra per intersezione;
poligono chiuso con doppio clic. Esc annulla il disegno e deseleziona. I nodi hanno evidenziazione
e inquadratura proprie. La selezione per valore considera almeno un campione del campo grafico corrente;
non valuta estremi interni ricostruiti. Isola/Nascondi agiscono poi sugli elementi selezionati.

Il mouse legge il campione importato più vicino sullo schermo all'interno dell'elemento colpito.
La didascalia lo dichiara: non è una query interpolata della faccia. P fissa fino a 20 letture, con
caso e unità di origine; nel caso mediato espone anche il corrispondente valore originale.
Le letture rimangono riferite al campo di origine quando cambia il campo attivo. Min/max si riferiscono
ai campioni grafici degli elementi attivi, inclusa la porzione coperta dal taglio. Le etichette riservano
rettangoli sullo schermo e vengono ridisposte dopo il movimento della camera.

Il clipping conserva gli ID per il picking e interpola soltanto le coordinate grafiche della texture.
Non modifica elementi, offset, risultati, tabelle, campioni o scala: taglio visuale senza tappi e senza
calcolo delle risultanti di sezione. Le coordinate vengono ricentrate in doppia precisione prima dei
buffer float. Un piano per tre nodi degeneri non viene applicato. Trasparenza e bordi esterni sono livelli
separati dalla mesh; i volumi mantengono la geometria con offset già collaudata.

Il confronto usa due campi indipendenti per caso/componente/media, con famiglia, assi, unità e visibilità
comuni. La posa della camera è sincronizzata dalla prima scena con nuclei di rendering indipendenti; all'apertura si inquadra la geometria attiva.
La scala comune usa l'unione dei due intervalli (oppure i limiti manuali della vista principale) solo
per unità compatibili. Ogni scena dispone della propria legenda. Non è un calcolo di differenze o inviluppi.

Le viste con nome persistono camera, campo, unità, limiti, selezione di elementi, visibilità, taglio,
mesh, bordi, opacità, palette e sfondo. Sono preferenze locali in LocalAppData/ANTHEA/ModelViews,
separate dall'archivio ingegneristico e indicizzate dall'impronta della revisione esatta.
Salvataggio atomico, nomi univoci, validazione e limite di dimensione impediscono di corrompere
silenziosamente l'archivio delle viste. Non si trasferiscono le viste a una revisione diversa.

L'esportazione della scena produce PNG a 1600/2400/3840 pixel con titolo, campo e legenda per ogni vista.
Lo sfondo bianco è temporaneo e la vista aperta viene ripristinata; resta disponibile l'immagine
dell'intero controllo. Le immagini generate sono evidenze di prova, non risorse dell'applicazione.

### Contratti grafici e limiti espliciti

- Diagrammi delle aste: segmenti fra stazioni effettivamente importate, ordinata grafica lungo Z locale
  dichiarata nella scheda, scala visiva regolabile. Nessuna ricostruzione di campioni o deformate beam.
- Carichi: adattatore documentale in ModelWorkspace per CNLD globali, BMLD uniformi sull'intera asta
  senza proiezione/eccentricità/addizionali, PRES uniformi con direzione globale o terna locale importata.
  Conversione delle unità esplicite a kN/m. Frecce e momenti sono normalizzati per famiglia di unità;
  non sono azioni equivalenti. Assegnazioni non rappresentate sono contate e rimangono nelle tabelle.
  HYDRO, distribuzioni variabili, peso proprio e altri contratti non sono ricostruiti dal visualizzatore.
- Deformata: nuovo campo opzionale di spostamenti nodali globali [m], validato e omesso dai vecchi snapshot.
  Si disegna soltanto la geometria nodale spostata, amplificata a video, per elementi con tutti i nodi disponibili.
  La spalla reale non contiene questi spostamenti: comando disabilitato e messaggio esplicito.
- Nessuna modifica alle librerie GPC, al motore legacy, agli attesi indipendenti o alle verifiche.

Collaudi: 163 controlli di logica, 99 WPF nella suite completa, 32 nel percorso sintetico generale,
33 controlli grafici mirati nella revisione finale e runner quick con 27 PASS, nessun nuovo fallimento. Catture prima in nodal-averaging e dopo in
supporto/artefatti/model-viewer/exploration. La prova sintetica misura taglio/UV/ID, selezione attraverso
il piano, coordinate globali di un miliardo di metri, separazione delle legende, opacità, deformata
da spostamenti noti, carichi espliciti e persistenza. Dati originali e impronta restano invariati.
Sei cambi di campo reale già caricato: mediana 71,9955 ms, massimo 97,5869 ms fino a OnRendered;
importazione, costruzione delle combinazioni e acquisizione delle immagini sono escluse.

La prova finale comprende opacità misurata nei pixel, rotazione sincronizzata, uguale area delle due
scene e inquadratura completa sia prospettica sia ortogonale. È stato corretto il ripristino delle
dimensioni del render host dopo l'esportazione ad alta risoluzione: il ridimensionamento usa DIP WPF,
i buffer usano pixel fisici. La prova sullo schermo al 150% conserva dimensioni e binding dello sfondo.
Evidenze finali in exploration/reviewed, exploration/logic-final ed exploration/synthetic;
suite completa in exploration/validated; quick in ci/20261011-120808-modelli-esplorazione.
La build autonoma e il relativo avvio sono in exploration/interactive-final.


## Impostazioni unificate e report dinamico — 11 ottobre 2026

La revisione MOD-10 assegna una sola sede alle impostazioni: **Modello** conserva albero,
filtri, carichi, assi e condizioni al contorno; **Risultati** contiene campo, unità, media,
contouring, scala e confronto; **Vista** contiene camera, navigazione, sfondo, qualità,
oggetti/etichette, taglio, viste salvate ed esportazione PNG. Il pulsante superiore porta
alla scheda Vista. Non esistono più popup o barre concorrenti con copie degli stessi editor.
Le scorciatoie di selezione e inquadratura rimangono comandi rapidi. Il ripristino della
camera non azzera più le scelte della scheda Risultati. Tutta la navigazione, incluso Report,
si può ridurre alla barra laterale.

**Report** è una composizione ordinata di capitoli con titolo, testo/didascalia e inclusione.
Si aggiungono la vista principale corrente, testi liberi e riferimenti ai fogli di verifica.
La generazione in serie incrocia le viste salvate selezionate con i casi/combinazioni
realmente disponibili; senza viste selezionate usa quella corrente. Non acquisisce dati da
MIDAS, non costruisce combinazioni e non ricostruisce campi mancanti. Limite iniziale: 100
capitoli, 30.000 caratteri per testo, figure da 1600/2400/3840 pixel.

Le figure conservano camera/proiezione/proporzioni, visibilità e selezione, famiglia/caso/
componente/assi/unità, media grafica, scala, palette, taglio, superfici/etichette/condizioni
al contorno, carichi e deformata. Questi contratti estendono in modo opzionale le precedenti
viste locali. Richiama figura applica lo stato alla vista; le modifiche passano sempre dagli
editor esistenti e vengono registrate nella figura con un comando esplicito. La composizione
si salva e riapre come `.antheareport`: in questa bozza non è ancora incorporata nell'archivio
`.programma`. I testi non vengono sostituiti dalla rigenerazione.

Architettura:

- `ANTHEA.ModelReports`, nuova libreria senza WPF: contratti versionati, validazione,
  persistenza atomica, composizione PDF/HTML e pubblicazione della cartella completa.
- Presentation: capitoli e scelte osservabili, comandi di aggiunta/riordino/esclusione,
  stato della revisione, annullamento e avanzamento. Nessuna formula o verifica strutturale.
- WPF: finestre di scelta file, rendering delle figure in un host separato, avvio della
  visualizzazione del PDF su comando dell'utente. Il renderer del report non cambia camera,
  campi, selezione o dati della vista interattiva. Accessi WPF confinati al Dispatcher anche
  dopo la composizione PDF sul thread di lavoro.
- PDFsharp/MigraDoc **6.2.4**, build Core senza WPF, licenza MIT con avviso distribuito insieme
  al modulo. I font Segoe UI vengono letti da Windows, non copiati nel repository.

Ogni generazione crea una cartella nuova con PDF, HTML autonomo con immagini incorporate,
figure PNG e composizione modificabile; non sovrascrive il report precedente. La pubblicazione
avviene dopo il completamento; errore o annullamento rimuovono solo i file temporanei della
singola esecuzione. Il report riporta revisione, caso, campo, unità, scala, visibilità e
limiti dei dati disponibili. Le legende e le etichette hanno dimensioni adatte alla stampa,
indipendenti dalla risoluzione raster. Il cubo di navigazione è escluso dalle figure stampate.

Il cambio di revisione è segnalato nella composizione. La rigenerazione verifica gli ID e
rifiuta casi/campi, spostamenti e carichi richiesti ma assenti, senza sostituirli con altri.
Un insieme esplicito di elementi resta tale; una vista salvata su tutti gli elementi include
anche quelli nuovi. La rigenerazione è su richiesta e riguarda tutti i capitoli inclusi;
non esiste ancora un aggiornamento incrementale delle sole figure cambiate.

Predisposizioni e limiti dichiarati:

- Le sezioni di verifica salvano ID e nome del foglio. Il PDF è marcato come bozza quando
  include questi capitoli e dichiara che gli esiti non sono collegati. Il futuro provider
  dovrà ricevere i risultati dalle librerie GPC e i contenuti dai servizi documentali.
- Si acquisisce la vista principale: confronto affiancato e letture fissate non sono ancora
  inclusi nel contratto del report. Estremi automatici e legenda del campo sono disponibili.
- Restano da realizzare l'archiviazione dei report nel progetto, anteprima paginata interna,
  tabelle dinamiche, immagini esterne, rich text e aggiornamenti incrementali.
- La guida pratica globale e le sue edizioni verranno aggiornate alla pubblicazione del
  modulo, insieme al collaudo d'uso già previsto dal piano. Questa è documentazione di sviluppo.

Riferimenti funzionali consultati (solo documentazione di sviluppo): guida ufficiale MIDAS
[Dynamic Report](https://support.midasuser.com/hc/en-us/articles/24721486569369-Dynamic-Report)
e manuale RFEM [Printout Report Manager](https://www.dlubal.com/en/downloads-and-information/documents/online-manuals/rfem-6/002454).
Nessun testo, figura o rimando ai concorrenti è inserito nei documenti prodotti dal modulo.

Verifiche: 177 controlli di logica; regressione WPF completa 106 PASS senza errori di binding;
prova del report reale 12 PASS; runner quick 27 PASS, zero nuovi fallimenti. Evidenze prima in `exploration/reviewed`, dopo in
`reports/regression-final`, `reports/final` e `reports/logic`. Prova di stampa: spalla con
3 figure a 2400 pixel, testi e capitolo di verifica esplicitamente predisposto, 6 pagine.
Misura finale della generazione con dati caricati: 3,430 s, esclusi importazione e calcoli.
Tutte le pagine del PDF sono renderizzate e controllate. Nessuna libreria GPC o altro repository
modificato; nessun push o merge su main.

Runner finale: `supporto/artefatti/ci/20261011-134222-modelli-report-final`.
Build di prova: `supporto/artefatti/model-viewer/reports/interactive/Avvia-Modelli-Report.cmd`.


### Posizione delle modalità di rappresentazione — MOD-11

Su richiesta dell'utente Geometria e Volumi e offset sono nella parte iniziale della scheda
Vista, con evidenza della modalità attiva. Il comando MVVM conserva aperta questa scheda dopo
il cambio di rappresentazione. I risultati si attivano dalla scheda Risultati: rimossi dalla
barra superiore i tre selettori e il separatore. Restano i comandi rapidi di inquadratura,
report, impostazioni, tabella, proprietà ed esportazione. Nessuna modifica al renderer o ai dati.
Prima: `reports/final/scheda-vista-unificata.png`; dopo: `view-mode-tab/ui/vista-taglio.png`,
entrambi in `supporto/artefatti/model-viewer/`.

Collaudo MOD-11: 33 controlli WPF senza errori di binding, suite ModelWorkspace con 177 controlli
tramite runner quick mirato (2 PASS inclusa la build, nessun nuovo fallimento).

### Risultati indipendenti dalla rappresentazione e grafici delle aste — MOD-12

La scelta Geometria / Volumi e offset nella scheda Vista ora è indipendente dal livello
Mostra risultati nella scheda Risultati. Cambiare rappresentazione conserva caso, componente,
media, scala e preferenza della mesh. Il selettore del primo prototipo rimane soltanto come
adattatore di compatibilità per i chiamanti esistenti; UI, confronto e renderer hanno due stati
distinti. Le viste precedenti continuano ad aprirsi con lo stesso significato e le nuove ricette
memorizzano esplicitamente la rappresentazione. Anche le serie dei report conservano l'estrusione.

Plate: contouring sulle facce superiore/inferiore e sui bordi dello spessore, usando gli offset
lungo la normale. La stessa sollecitazione è rappresentata sulle due facce: non si ricavano
nuove tensioni attraverso lo spessore. Media ai vertici, isolinee, selezione per clic e chiusura
dei lati esposti dall'isolamento sono mantenute. L'inquadratura considera gli ingombri estrusi;
i bordi continui non ricevono una linea verticale per ogni elemento della mesh.

Aste: controlli unici in Risultati / Contouring e diagrammi delle aste. Contouring e diagramma
sono indipendenti, combinabili e disattivabili. Geometria e stile wireframe mostrano assi colorati;
Volumi mostra il campo sulle sezioni rettangolari centrate SB disponibili. Altri profili restano
sull'asse; non si inventano sezioni o offset delle aste. Supportate le famiglie BEAM, TRUSS,
TENSTR, COMPTR e CABLE, limitate alle componenti già importate per ciascuna formulazione.

I diagrammi hanno riempimento trasparente, ampiezza grafica, scelta Y/Z locale e valori opzionali
alle stazioni. Automatico usa Y per Fy/Mz, Z per gli altri campi; l'utente può cambiare il piano.
L'orientamento usa le terne importate, con fallback grafico da connettività e rotazione per i
modelli senza terna esplicita. Contouring e diagrammi interpolano soltanto fra stazioni note,
senza prolungare il campo oltre i campioni. Un singolo campione rimane un'ordinata/marker locale.
Nessun nuovo calcolo di sollecitazioni. Visibilità nel report conta gli elementi effettivamente
disegnati quando le altre famiglie sono nascoste.

Collaudo: 191 controlli ModelWorkspace; 138 controlli WPF di regressione; 39 mirati plate/aste;
14 sul report reale, con quattro figure e sette pagine verificate visivamente. Attesi indipendenti
sugli offset (+0.6, -0.7), triangoli orientati, UV delle due facce e campo lineare a tratti
(-10, 20, 0) delle aste. Provati valori non mediati/mediati, stazione singola, tutte le famiglie,
wireframe/estrusione, spegnimenti separati, picking e compatibilità delle ricette.

Evidenze prima: `supporto/artefatti/model-viewer/view-mode-tab/ui`; dopo:
`supporto/artefatti/model-viewer/extruded-results/combined`, `regression`, `report-final`.
Il prototipo resta nel branch separato; nessuna modifica alle DLL GPC o agli altri repository.
Guide globali e pubblicazione restano al collaudo del rilascio già previsto nel piano.

### Deformata completa, spostamenti e aspetti — MOD-13

Confronto funzionale con i manuali ufficiali (riferimenti soltanto di sviluppo):
[MIDAS Deformed Shape](https://support.midasuser.com/hc/en-us/articles/18162691103385-Deformed-Shape),
[tabella API degli spostamenti](https://support.midasuser.com/hc/en-us/articles/36009638400281-Displacements-Analysis-Result-Table),
[RFEM Deformations](https://www.dlubal.com/en/downloads-and-information/documents/online-manuals/rfem-6/002713).
Le lacune principali del prototipo erano la sola linea deformata, l'assenza del campo nodale e
del controllo della scala; ora il modello completo può deformarsi anche con volumi e forze colorate.

Risultati contiene la scelta Sollecitazioni / Spostamenti nodali. Per questi ultimi sono disponibili
ux, uy, uz e modulo ricavato dalle tre traslazioni, con unità mm/cm/m e assi globali. La tabella
elenca ogni nodo una sola volta e segue la selezione nodale. Non si applica la media degli elementi
a un campo già nodale. Il collegamento ai fogli conserva il contratto delle forze precedente.

Forma deformata: scala automatica (spostamento massimo pari all'8% dell'estensione maggiore),
reale ×1 o manuale; sagoma originaria con opacità e mesh opzionale. Gli offset vengono traslati
insieme ai nodi, nella loro direzione originale: non si inventano rotazioni e non si aprono giunti
artificiali tra plate adiacenti. Se mancano traslazioni, l'intero elemento rimane grigio e indeformato.
Gli inviluppi ammettono i contouring delle singole componenti, ma non deformata e modulo vettoriale.

L'animazione 0–100%–0 riutilizza geometrie, indici, colori e materiali; aggiorna posizioni e normali.
Durata del ciclo, pausa, cursore della fase e ritorno al 100% sono nel ViewModel. Il timer WPF si
ferma alla chiusura e al cambio di campo/opzioni. Durante la riproduzione le etichette sono sospese;
la pausa le ripristina. Taglio e confronto affiancato disabilitano soltanto l'animazione. Le figure
e le viste salvano la fase esplicita e non modificano i dati del caso. Il report rifiuta un caso
mancante anche quando la deformata è richiesta senza contouring.

Temi: verificati Light, Dark e VeryDark su tutte le cinque schede, menu a discesa e tabella;
nessun nuovo tema parallelo. La finestra di prova offre un selettore temporaneo, senza salvare
preferenze globali. Le schede restano in una riga anche nella larghezza ordinaria della barra.

Acquisizione reale: 49 casi statici, 3.755 nodi per caso, 183.995 vettori di traslazione. Richieste
MIDAS in sola lettura, circa 139 s complessivi per le tabelle nodali. Nodi ed elementi confrontati
con la cattura precedente; terne/vincoli riutilizzati solo dopo verifica delle rispettive sorgenti.
I file originali e il pacchetto nodale sono in `supporto/artefatti/model-viewer/deformations/`.
La cattura delle forze aggiornata è separata; i tempi di caricamento non sono inclusi nel rendering.

Prime misure sul modello caricato e con volumi: cambio caso a video 0,13–0,24 s; preparazione
animazione circa 0,26 s, aggiornamento CPU mediano circa 7 ms su 678 mila vertici. La frequenza
effettiva viene misurata contando le fasi distinte presentate, non tutti i ridisegni del viewport.
I valori dipendono dalla macchina e dalla qualità selezionata; JSON delle misure conservati.
Con il timer adattato al lavoro grafico, la prova successiva misura 28,8 fasi distinte/s;
cambio caso 0,11–0,15 s, preparazione 0,27 s. Evidenza `deformations/real-timer`.

Collaudo: controlli indipendenti sul vettore 3–4–12 mm, offset delle facce −0,65/−0,35 m dopo
traslazione, riuso dei buffer, ripristino, dati parziali e rifiuto delle deformate d'inviluppo.
Regressione WPF originale 138 PASS; 53 controlli sui tre temi; 14 sulla copia nodale reale.
La suite di logica include import, unità, dati invalidi e compatibilità delle ricette precedenti.
Collaudo conclusivo: 212 controlli logici, 36 deformate sintetiche, 9 report deformate; PDF di
cinque pagine controllato a video in `deformations/report-verified`. Il confronto di stato
attende l'assestamento del layout prima della cattura dell'aspect ratio della camera.
Prima: `deformations/before`; dopo: `static-final`, `themes-check`, `real-refined` e `regression`.
Guide globali e rilascio rimangono nel passaggio di pubblicazione già concordato.

### Pacchetto reale completo (MOD-14)

Acquisizione aggiornata conclusa senza variazioni delle tabelle controllate: 49 casi plate e
beam in 534,8 s, oltre ai 139,4 s delle traslazioni. Il pacchetto normalizzato completo occupa
123.821.818 byte; SHA-256 della cattura prima della modifica alla lettura:
`44364F843E48DFF1865976D084F46C0A96E237FFCA569AC48574B950AB8962D9`.
Nuovo limite 256 MiB, deserializzazione del file e calcolo dell'impronta in streaming.
Formato JSON e impronta delle revisioni conservati, nessun aggiornamento delle DLL GPC.
Runner mirato: 216 controlli logici; apertura in archivio e deformata reale: 14 PASS.
Evidenza `deformations/real-complete`; il pacchetto iniziale rimane conservato.

### Dettaglio delle aste (MOD-15)

Confronto funzionale con i manuali ufficiali [RFEM, Result Diagrams](https://www.dlubal.com/en/downloads-and-information/documents/online-manuals/rfem-6/000484)
e [MIDAS, Beam Diagrams](https://support.midasuser.com/hc/en-us/articles/18165910326809-Beam-Diagrams).
Implementazione originale: grafico per asta selezionata, componente singola o tutte disponibili,
stazione x/L condivisa, distanza dall'estremità I, punti importati distinti dall'interpolazione
lineare, inversione del solo grafico, riempimento opzionale, blocco del cursore e CSV dei campioni.
Il filtro delle stazioni 3D non elimina i dati dal dettaglio. Famiglie assiali mantengono il solo Fx.
Nessuna interpolazione fuori dal dominio importato o attraverso stazioni ambigue. L'inviluppo
rimane identificato come insieme di estremi non simultanei.

Stato MVVM nella Presentation; disegno e layout adattivo in WPF. Scheda Risultati come unica
sede dei selettori del campo. Pannello inferiore alternativo alla tabella; col numero di grafici
si adattano altezza e colonne. Legenda 3D adattata al viewport residuo; etichette quasi coincidenti
non vengono sovrapposte, con estremi esatti disponibili nel tooltip. Temi esistenti riutilizzati.

Report: figure specifiche dei diagrammi con rigenerazione indipendente dalla scena, stessa
ricetta portabile, nessuna modifica dei dati. 235 controlli logici e 17 WPF/report reali;
PDF di tre pagine controllato. Evidenze `member-diagrams/reviewed`, baseline `real` e `final`.

### Volume di esplorazione (MOD-16)

Riferimento funzionale: [RFEM, Clipping Boxes](https://www.dlubal.com/en/downloads-and-information/documents/online-manuals/rfem-6/000217).
Implementazione originale a sei semispazi, con limiti XYZ impostabili e acquisibili dalla selezione
o dal modello. Il taglio conserva ID, colori interpolati, intersezioni delle linee e selezione
grafica. Contorno indipendente; deformate statiche supportate. Non si ricostruiscono facce
o risultati mancanti sul taglio. Scala del campo esplicitamente conservata. Viste, confronto
e report salvano il volume. 16 controlli WPF dedicati, 33 di regressione esplorazione;
PDF di due pagine controllato in `clip-volume/real`. Sorgente e impronta del modello invariati.

### Misure nodali e navigazione (MOD-17)

Riferimento funzionale: [RFEM, Measuring](https://www.dlubal.com/en/downloads-and-information/documents/online-manuals/rfem-6/003183).
Distanza fra due nodi, componenti globali e angolo con vertice nel secondo di tre nodi.
Scelta grafica o ID, venti annotazioni al massimo, interruttore globale e rimozione singola.
Base originale oppure deformata reale del caso selezionato, unità m/cm/mm; valori indipendenti
dalla scala e fase grafiche. Viste e report conservano definizioni e base. GPC.Geometry
esistente esegue distanza e angolo attraverso un adattatore senza WPF; nessuna libreria aggiornata.

Inquadra preserva vista e proiezione e centra il modello tagliato. Risolta anche una proiezione
obsoleta subito dopo il cambio camera, che poteva sovrapporre annotazioni nel primo fotogramma
e nel PDF. Attesi indipendenti 3-4-5, 5-12-13, 90 gradi; 263 controlli logici, 14 WPF/report
reali e 33 di regressione esplorazione. Tre pagine PDF controllate, evidenza `measurements/final`.

### Bordi dopo isolamento (MOD-18)

Prima, una visibilita esplicita disabilitava la soppressione delle facce interne: anche isolare
tutto il modello reintroduceva la mesh dei volumi. Ora la topologia e memorizzata in cache
per il gruppo attivo; i nuovi bordi esterni compaiono quando si nasconde il vicino.
Il caso indipendente di due plate passa dai 24 triangoli precedenti a 20; un solo prisma
ne mantiene 12. Sul modello reale completo e isolamento totale hanno geometria identica
bit per bit e 222212 triangoli. Proprieta 1 isolata: 1612 plate, 103520 triangoli;
53 ms di preparazione osservati nella cattura. 11 controlli WPF, immagini prima e dopo
in `visible-boundaries`; mesh GPC e valori importati non modificati.

### Check della revisione e delta (MOD-19)

La scheda Modello ospita il confronto con una copia precedente; il pannello inferiore alterna
tabella, diagrammi e revisione. Elenco filtrabile, selezione degli oggetti correnti, isolamento
delle modifiche, inquadratura della posizione precedente per i cancellati. Verde: nuovi,
arancio: geometria, viola: attributi, rosso: sagoma originale dei cancellati. Ogni livello
si puo disattivare. Le annotazioni possono accompagnare il contouring senza cambiarne i valori.

Dopo un’importazione valida, il confronto viene predisposto automaticamente. Calcolo dei delta
solo su richiesta, fuori dal thread grafico, con annullamento. Caso/famiglia/assi/componente/unità
restano nella scheda Risultati. Identita dei campioni: elemento, nodo e stazione; geometrie cambiate
escluse inizialmente. Cambio di campo invalida la tabella; cambio di unita riusa i delta acquisiti.
CSV e flag di geometria diversa; nessuna combinazione, trasformazione o media aggiunta.

Le viste e i report conservano annotazioni e impronta delle due copie. La rigenerazione del report
rifiuta una revisione corrente diversa. Le grandi matrici precedenti restano solo nell’ultimo
confronto di sessione; riaprire una vista non finge di averle, il delta richiede la copia precedente.
Prove sul modello reale con differenze simulate dichiarate: 193-274 ms per geometria/attributi,
circa 10 ms per 13718 campioni Myy. Tempi senza lettura dei file. 281 controlli logici, 13 WPF,
PDF di due pagine controllato; evidenze `revision-review/reviewed` e `final`.
### Colori per categoria e aggiornamento delle copie — 11 ottobre 2026

La legenda degli attributi permette selezione dei visibili e, dal menu contestuale, isolamento,
nascondimento e apertura del colore nella scheda Vista. La stessa scheda include tutte le
categorie, anche nascoste, una palette, RGB esadecimale e ripristino. Colori stabili per chiave
e modalità, persistenti nelle viste/ricette e coerenti nel confronto e nelle legende PDF.
Il colore dei risultati resta determinato esclusivamente dal campo scalare. MOD-20.

Il comando Aggiorna apre un’unica sezione di importazione nella scheda Modello. Quattro modi:
solo geometria; geometria, attributi e carichi; solo risultati; tutto. Nei modi senza risultati
le sollecitazioni precedenti vengono rimosse dalla nuova copia. Geometria comprende sezioni,
spessori, offset e terne geometriche, senza attributi non geometrici. Solo risultati conserva
geometria e preferenze della vista, sostituisce l’insieme completo dei casi e richiede identità
delle parti strutturali e delle tabelle. In caso di differenza invita a usare Tutto; nessun
aggiornamento parziale viene applicato prima del controllo. Annullamento cooperativo, provenienza
portabile e confronto della revisione. Resta un flusso su copie salvate, non un connettore live.
MOD-21.

### Preview diretta e rifinitura dei controlli — 11 ottobre 2026

`--viewer-only --direct-model` apre il pacchetto senza passare dal documento Progetti.
Sul pacchetto completo da 123,8 MB: lettura e validazione 2,3–2,4 s; prima vista 8,0–8,4 s,
inclusi inizializzazione WPF/Direct3D e attesa del runner. Sono tempi di avvio, distinti dai
cambi di campo già caricato. Risorse di stile incorporate; caricamento in background e
chiusura annullabile. La preview interattiva conserva le modifiche in memoria. MOD-22.

Tema applicato anche a caselle di scelta e pannelli espandibili, con 62 controlli WPF nei
tre aspetti. Il cubo ora ha dimensione corretta (fattore 1,1), etichette proprie e bersagli
verificati su facce, spigoli e vertici. Un ripristino della camera richiede il ridisegno esplicito
per evitare hit test sul fotogramma precedente. I pannelli inferiori si adattano all’altezza
della finestra. Cattura prima/correzione/confronto in `view-cube`, 13 controlli. MOD-23.

### Versi e dettagli delle condizioni al contorno — 11 ottobre 2026

Scheda Modello: frecce I→J per aste e N1→N2 per superfici; dimensione di assi e simboli
da 0,25 a 3; etichette dei DOF importati, collegate al rispettivo interruttore del vincolo.
Non si reinterpretano gli stati dei rilasci. Collegamenti con stesso nodo iniziale e stessi
dettagli condividono una sola etichetta con conteggio; restano singoli in tabella. Limite 40,
priorità a selezione e vincoli/svincoli, controllo sovrapposizione e taglio grafico.
Versi e simboli seguono la deformata; etichette sospese durante l'animazione.

Il comando della tabella riusa l'ambito visibile/selezionato. Interruttore generale, viste,
report e confronto conservano lo stesso stato. Nessuna modifica del pacchetto strutturale.
Le opzioni superiori e inferiori dell'albero scorrono entro un'altezza relativa: l'albero
rimane accessibile anche aprendo i dettagli. 18 controlli WPF sintetici/reali e suite logica
del runner ufficiale; catture `overlay-details/layout-before` e `overlay-details/final`.
Ispirazione funzionale: [display MIDAS](https://support.midasuser.com/hc/en-us/articles/29198491035417-Display)
e [vincoli nodali RFEM](https://www.dlubal.com/en/downloads-and-information/documents/online-manuals/rfem-6/000006).
Nessun contenuto di terzi incorporato nel modulo. MOD-24.

### Letture geometriche e separazione delle responsabilità — 11 ottobre 2026

Modulo delle traslazioni e lunghezza dell'asta passano dall'adattatore ModelMeasurements
alle operazioni GPC esistenti. Il ViewModel conserva soltanto selezione, scala grafica e
formattazione. Cattura precedente in `ci/20261011-202042-modelli-gpc-letture-baseline`,
successiva e confronto in `ci/20261011-202133-modelli-gpc-letture-dopo`: JSON identico,
SHA-256 `D20AF48B7DD07331B441F8F87CE86FD2BC1654C800C8CF74DDB82C6C62553B1D`.
322 controlli logici; nessun atteso indipendente aggiornato. MOD-25.

### Confezionamento della prova e cattura incorporata — 11 ottobre 2026

`tools/model-viewer/Publish-Preview.ps1 -Model <pacchetto> -OutputDirectory <cartella-vuota>`
pubblica la build Release locale, copia il pacchetto e crea tre avviatori Windows.
`Apri-Contouring.cmd`, `Apri-Deformata.cmd`, `Apri-Aste.cmd`: controllo reale, tema molto
scuro iniziale, cambio tema nella barra della preview. Il terzo apre anche i sei diagrammi
dell'asta. Richiede Desktop Runtime .NET 8 x64; non è un installer del prodotto.
Manifest dei file, nessuna sostituzione di una cartella non vuota, nessuna dipendenza Gmsh.
Viste salvate in `preferenze-viste`, log e schermate in una nuova sottocartella `prove`
per ogni avvio. Gli aggiornamenti del modello restano in memoria. MOD-26.

Controlli sulla build Release aperta fuori dai sorgenti: 14 prove deformate e 62 prove dei
temi. Cambio deformata visibile 97–205 ms, preparazione animazione 170 ms, frame CPU
mediano 6,9 ms e P95 10,9 ms, circa 31 fotogrammi animati distinti/s. Questi tempi sono
misure sul modello di laboratorio già caricato; lettura 2,3 s e prima vista 7,9 s sono
separati. Evidenze `preview-qa-20261011/collaudo/deformate`.

La cattura completa mantiene il corretto allineamento anche quando il controllo è inserito
con margini in un'altra vista e dopo una riduzione della finestra. Catture precedenti in
`export-layout/before`, corrette in `export-layout/preferences`; 10 controlli WPF includono
pixel ai bordi, prima apertura scura, salvataggio e riapertura delle viste dallo store dedicato.
I nuovi host registrano il tema prima di aprirsi; didascalia del tipo di risultato resa esplicita.
