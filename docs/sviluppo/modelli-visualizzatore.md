# Modelli di calcolo: visualizzatore integrato

Stato: bozza di sviluppo del 9 ottobre 2026, branch `codex/model-viewer-draft`.
La base ANTHEA 1.1.0 (`main`, 5c324ef) è stata incorporata nel branch con ff9d4c4.
Nessun merge verso main e nessun push. Questo è un documento di sviluppo; Wiki e guide globali con PDF si aggiornano al rilascio del modulo.

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
Nessun calcolo aggiunto a X.Core, X.Calculations o X.Desktop. Il bundle lib/Checker non cambia.

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
  è soltanto contesto: non contribuisce a selezione, risultati o scala. Inquadratura con inclusione dei nascosti opzionale.
- ID di nodi, elementi e proprietà, su tutti i visibili o soltanto sulla selezione; dimensione testo e linee.
  Le etichette sono in primo piano; su zone dense è utile limitarle alla selezione o ingrandire.
- Colori uniformi, per tipo, materiale, sezione/spessore e gruppo. Legenda stabile rispetto ai filtri.
  Per appartenenze multiple prevale il gruppo con meno elementi; parità risolta per ID crescente.
- Assi locali da GPC, restrain, constraint, link e release. Simboli distinti, senza sostituzioni meccaniche.
- Preview proporzionata della sezione rettangolare, proprietà ridimensionabili, tema chiaro e scuro.
- Tabelle dei dati salvati: nodi, elementi, sezioni, spessori/offset, vincoli, risultato corrente e attributi.
  Tipi numerici ordinabili, ricerca, filtri di visibilità/selezione, riga collegata alla scena, copia Ctrl+C e CSV.
  Il risultato corrente usa esattamente campo, campioni e unità della vista; non media nodi condivisi.
  CSV con separatore punto e virgola, decimale invariant, valori numerici integri e testi protetti da formule.
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

Contouring: valori distinti per elemento, interpolazione bilineare dei quad e lineare dei triangoli,
qualità 1/4/8 suddivisioni, palette continua, isolinee. Nessuna media tra elementi. La suddivisione è grafica,
non accresce l'accuratezza del solutore. Beam: segmenti colorati tra campioni importati, senza inventare valori
interni oltre la rappresentazione lineare; la scelta di una stazione colora l'asta con quel campione.

I risultati della copia MIDAS mantengono componenti e segni MIDAS, convertendo solo le unità.
Non sono risultati già normalizzati alle convenzioni del Checker GPC (in particolare il momento locale beam).
Gli estremi del caso SLU_Q1_1(max) sono importati, non simultanei e non calcolati dentro ANTHEA.
Non è certificata la compatibilità analisi/modello per le verifiche. La scheda Verifiche conserva solo riferimenti.

Le assegnazioni non interpretabili restano nei record originali e nella diagnostica. Il simbolo non sostituisce
l'ispezione dei gradi di libertà. La copia di prova contiene un vincolo con settimo DOF: la vista mostra i sei DOF
supportati e mantiene il dato warping originale con avviso.

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

## Collaudo e prestazioni

- Runner `build/ci.ps1 -Profile quick -Tag modelli-workspace-v2`: 25 PASS, nessun nuovo fallimento.
- ModelWorkspace: 46 controlli rapidi, inclusi colori sovrapposti, isolamento, unità, campioni, tabelle,
  export, distinzione mancante/vuoto e rifiuto di momenti su truss.
- WPF sintetico integrato: 20 controlli, comprese quote offset -0,85/-0,55 m attese indipendenti e valore bilineare 25.
- WPF sul modello completo: 38 controlli su ray picking, tre viste, sezioni, assi/vincoli, principali, beam, ID, colori, tabella,
  export e temi. Screenshot reali in `supporto/artefatti/model-viewer/delivery`.
- GPC: 90 test mirati di importazione, assegnazioni e persistenza. Gli svincoli e link sopravvivono
  all'archivio esplicito e contribuiscono all'impronta degli input; errori atomici e leggi non supportate verificati.

Sei cambi di componente sul modello completo già caricato, qualità Alta: da 98,97 a 122,22 ms,
mediana 108,70 ms fino all'evento Viewport.OnRendered. La misura include cambio dello stato, preparazione
e notifica del fotogramma; non misura il monitor fisico, l'acquisizione, il solutore o il calcolo dell'inviluppo.
I dati grezzi sono in `review-measured/tempi-campi.json`. Non è una garanzia per modelli arbitrariamente grandi.

## Passi successivi

| Priorità | Lavoro rimasto | Sede |
| --- | --- | --- |
| 1 | Rilascio coerente GPC da commit pushati e aggiornamento bundle con manifest e profilo full | Librerie / build ANTHEA |
| 1 | Adattatore comune definitivo per tutti i solutori; copertura esplicita di solidi, cavi, link non lineari, PRLS e fasi | GPC Model / Converter |
| 1 | Profili solidi generici/variabili, offset di estremità e sezioni orientate, provenienza delle convenzioni | GPC Geometry/Model e renderer |
| 1 | Combinazioni e inviluppi locali con caso governante, coerenza simultanea e cache | GPC, non WPF |
| 1 | Archivio compresso/grandi dati e confronto completo delle revisioni modello | Librerie documenti/progetti |
| 2 | Carichi grafici, deformate, tensioni, volumi, diagrammi beam, clipping, scala manuale/simmetrica | Contratti GPC e vista |
| 2 | Controlli ingegneristici, editor del modello e trasferimento controllato ai fogli | GPC e libreria applicativa |
| 2 | Selezione rettangolare, etichette senza sovrapposizioni, ordinamento avanzato, grafici tabellari, preferenze persistenti | Modulo Modelli |
| 2 | Ampliare prove su software reali diversi, modelli grandi, DPI multipli e accessibilità | Test |
| 3 | Guide globali/Wiki/PDF e distribuzione dopo il collaudo del modulo | Documentazione |

Il formato .antheamodel resta un contratto temporaneo di presentazione, non il formato definitivo GPC Model.
Nessun dato di calcolo legacy è stato spostato o sostituito; confronto delle revisioni e archivio grandi dati
restano prerequisiti per distribuire il modulo negli archivi operativi.
