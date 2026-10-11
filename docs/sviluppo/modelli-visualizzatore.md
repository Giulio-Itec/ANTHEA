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
senza mediare i valori tra elementi. Esc deseleziona. Le impostazioni sono della sessione, non ancora persistenti.

Il contratto di errore della sorgente distingue sessione MIDAS assente, documento chiuso, accesso rifiutato,
timeout e indisponibilità. La vista espone il motivo e il recupero, conserva l'ultima copia valida e non
applica importazioni parziali. I corpi HTTP e le credenziali non diventano testo della vista. Il collegamento
diretto MIDAS nel prodotto è ancora da integrare: il banner è collaudato con un adattatore simulato, mentre
le acquisizioni reali di questa fase usano gli strumenti di laboratorio.

Preparato nella libreria senza WPF il confronto di snapshot per ID, con tolleranza sulle coordinate,
attributi, aggiunte/rimozioni e delta di un campo esplicitamente richiesto. La visualizzazione dei cambiamenti,
la scelta della revisione di riferimento e la conservazione dello storico NON sono ancora collegate:
l'utente ha dato precedenza all'esperimento sui 49 casi base. Non si dichiara completato il check revisione.

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
| 2 | Carichi grafici, deformate, tensioni, volumi, diagrammi beam, clipping e filtri per intervalli di risultato | Contratti GPC e vista |
| 2 | Controlli ingegneristici, editor del modello e trasferimento controllato ai fogli | GPC e libreria applicativa |
| 2 | Selezione rettangolare/per numeri, etichette senza sovrapposizioni, ordinamento avanzato, grafici tabellari, viste con camera e preferenze persistenti | Modulo Modelli |
| 2 | Ampliare prove su software reali diversi, modelli grandi, DPI multipli e accessibilità | Test |
| 3 | Guide globali/Wiki/PDF e distribuzione dopo il collaudo del modulo | Documentazione |

Il formato .antheamodel resta un contratto temporaneo di presentazione, non il formato definitivo GPC Model.

Per la scelta fra importazione di tutte le combinazioni e soli casi base, vedere il
[benchmark sul modello completo dell'11 ottobre](modelli-benchmark-casi-base.md): 49 casi acquisiti,
73 definizioni ricostruite in un prototipo separato, tempi e limiti di validazione espliciti.
Nessun dato di calcolo legacy è stato spostato o sostituito; confronto delle revisioni e archivio grandi dati
restano prerequisiti per distribuire il modulo negli archivi operativi.
