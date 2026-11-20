# Importazione DXF della sezione in c.a. — 19 novembre 2026

## Base e librerie

Main aggiornato con fast-forward da b31bd396 a **7ee431c917da3f9e8f8d0f5303ebc96ec3009945**, commit di Gabriele che installa il set ufficiale BB. Il lavoro locale della sezione composta rapida è conservato; cattura precedente in `supporto/artefatti/import-dxf-20261119/prima-librerie.patch` e `stato-prima.txt`. Nessun commit nuovo o push.

Set coerente con manifest e attestazione `release-set.json`: Model 55c15ed5, Checker 46960c89, commit dichiarati su origin/master, GPC.Design 0.48.0.0. Le 14 suite delle librerie sono quelle attestate dalla build ufficiale; non vengono dichiarate rieseguite localmente, dove i sorgenti Model/Checker non sono disponibili. SHA-256 release-set: `9ECF0A55785488AD030FB907FC58E76AE7961075F99B71EF328E866E3405E2D9`; manifest: `500477C03B75ED40E0C501426E65D36ACD7A56BC377215265C7A1D3B6FDA8CE5`.

## Responsabilità

La nuova API `ConcreteSectionTemplates.CreateFree` riceve poligoni e barre, non un file CAD. `ANTHEA.Application` legge il DXF tramite netDxf 2023.11.10 (MIT, avviso distribuito con l’applicazione), gestisce scelta dei layer e unità e prepara una copia del documento. netDxf esegue conversioni di unità, trasformazioni dei blocchi e discretizzazione degli archi. La libreria GPC valida contorni, fori e barre, centra la sezione e costruisce proprietà e mesh. Nessuna nuova formula strutturale e nessuna modifica a X.Core/X.Calculations.

`ANTHEA.ConcreteWorkspace/ConcreteFreeSectionAdapter` ricostruisce la geometria salvata dalla chiave `input.sezione_dxf`, versione 1, attraverso GPC. La scelta `DXF` riusa l’adattatore delle tipologie della sezione: rapido e completo mantengono lo stesso motore. Le barre manuali del completo hanno precedenza e sono validate; il ritorno dalla forma parametrica conserva barre e metadati manuali. Le sezioni usuali continuano sul percorso precedente.

## Contratto di importazione

- DXF 2000 e successivi, ASCII/binario; piano XY a quota zero. Un contorno esterno chiuso; fori su un layer selezionato; barre come cerchi con diametro reale, su un layer distinto dai fori.
- Polilinee 2D chiuse, archi bulge e cerchi; blocchi con trasformazioni gestite dal lettore. Entità non supportate segnalate, non raccordate automaticamente. Le entità non importate sono conteggiate nell’anteprima.
- Unità mm/cm/m/in/ft; unità non riconosciute richiedono selezione. Archi dei contorni con 96 suddivisioni per giro; diametri delle barre nativi.
- Limite di trasporto 20 MiB, 10000 entità, 8 livelli di blocchi. I limiti geometrici sono quelli GPC, con al massimo 100 fori nel documento.
- Anteprima isolata: annullamento, errore, chiusura del foglio o passaggio in sola lettura non applicano dati. Importazione accettata sostituisce la geometria in un’unica modifica.
- Geometria e barre nel documento, con nome file, hash, unità e origine CAD. Nessuna dipendenza dal file originale alla riapertura. Staffe non importate; nessuna rigenerazione dagli strati parametrici.

## Prove e documentazione

`ConcreteDxfChecks` verifica area e baricentro indipendenti, unità e origine CAD, ASCII/binario, cerchio fuori origine, blocchi, archi, geometrie non ammesse, transazioni del ViewModel, conservazione delle barre manuali, identità del rapporto della combinazione con il completo e salvataggio senza il file esterno. La fixture versionata `tests/fixtures/dxf/sezione-forata-cm.dxf` alimenta anche anteprima/foglio WPF e relazioni completa/breve Word/PDF. Le suite WPF restano 74.

Le prove iniziali hanno evidenziato una dichiarazione di dimensione file intercettata dal rilevatore sintattico F4, un riferimento di report alla geometria nativa non esposta dal progetto e un confronto di prova non pertinente fra il contenitore rapido e quello completo, poi fra rapporti su azioni nulle. Correzioni senza allentare baseline, attesi indipendenti o regole. Evidenze complete in `supporto/artefatti/import-dxf-20261119/`.

Guide globali revisione 77: procedura pratica, ipotesi e limiti geometrici, Word/PDF con indici aggiornati e Wiki rigenerata. Revisione 76 archiviata con manifest in `supporto/SUPERATI/import-dxf-20261119/`.

## Verifiche del 19 novembre, dopo l'aggiornamento BB

Evidenze in `supporto/artefatti/import-dxf-20261119`. La corsa `finale-full-baseline` ha eseguito gli stadi build, fast, regression, wiki, baseline e ui: 163 PASS e 23 NEW-FAIL iniziali, senza modificare file versionati. I fallimenti sono conservati; le correzioni sono state riprovate nelle corse `correzioni-finali`, `relazioni-e-contrasto-finali` e `conclusione-f3-esempi`. Non sono state aggiunte eccezioni a `build/known-failures.json`.

| Verifica | Ultimo esito concluso |
|---|---|
| Build Release/UiTests; assenza di codice di prova nella Release | PASS |
| Architettura | PASS 643 |
| DXF; c.a. rapido; sezione composta rapida | PASS 45; 180; 50 |
| Report; Presentation completa | PASS 545; 5726 |
| Catalogo esempi | PASS 498, 100 esempi di 34 moduli |
| WPF shell; ospite dei moduli e report | PASS 103; 87 |
| Cinque suite WPF del visore Modelli; vista muri | PASS |
| Figure c.a. storiche | PASS, 11 figure e punti resistenti identici |
| Equivalenza F3, dopo l'approvazione Wiki | PASS, 1210 file, zero differenze non ammesse |
| Contrasto mirato fondazioni/ponte a 96 DPI | PASS, 58 catture, zero difetti nei tre temi |
| Contrasto globale a 96 DPI | KNOWN, 1834 catture: 12 difetti già registrati del c.a. rapido (6 Scuro, 6 Molto scuro); zero nuovi difetti |

Le transizioni editoriali Rev77 (102 nuove, 49 superate, 96 articoli) sono state applicate soltanto dopo l'approvazione dell'utente. Le catture e gli attesi ingegneristici rimangono invariati. `conclusione-f3-esempi` termina con 13 PASS e zero nuovi fallimenti.

### Correzioni emerse nelle prove

- I nomi CAD `Nessuno` e `— scegliere —` sono distinti dalle opzioni UI attraverso etichette non ambigue; nel documento rimangono i nomi originali. Un layer dei fori inesistente è rifiutato. La prova prima della correzione (`layer-omonimi-prima`) riproduce tre fallimenti con i precedenti 42 controlli superati; dopo (`layer-omonimi-finale`) passano tutti i 45 controlli DXF. Le aree indipendenti rimangono 460000 mm² con il foro e 480000 mm² senza.

- I report DXF riportano le posizioni effettive delle barre, il riferimento al baricentro e «staffe non importate». Disegni e report non rappresentano copriferro o dimensioni del foro provenienti dai precedenti campi parametrici. Una prova SLE varia quei campi residui (25/123/321) e verifica l'identità dei risultati.
- Due esempi distribuiti del ponte: `trave-tre-stati` e `trave-momenti-negativi`, con documento ANTHEA, Word e PDF. L'esempio negativo usa la relazione completa: la protezione delle relazioni brevi oltre due pagine resta attiva.
- Il test del catalogo storico esclude esplicitamente il nuovo rapido, già provato separatamente; il banco del visore passa l'ottavo parametro della radice di composizione aggiornata; la prova del muro controlla le dimensioni richieste prima che Windows le adatti all'area disponibile, registrando anche quelle effettive.
- Lo stile ComboBox del ponte eredita il tema; il popup «Approfondimenti» segue esplicitamente il tema del proprietario. Il controllo del ponte passa anche a 120 DPI, ma non sostituisce il confronto storico a 96 DPI.
- Il primo audit globale ha superato il vecchio timeout di 2400 s; il limite del runner è 3600 s. Una corsa diagnostica a 120 DPI è stata interrotta e conservata: le caselle tagliate dallo scorrimento producevano falsi rilievi rispetto al riferimento a 96 DPI. L'ipotesi temporanea di accettare una baseline vuota a DPI diversi è stata rimossa. La guardia originale rimane integrale. `ContrastDpiContext`, compilato solo in UiTests e attivo solo per `--check-contrast`, prepara il thread a 96 DPI prima delle finestre; il DPI effettivo viene comunque verificato. Nessuna preferenza Windows, manifest di produzione o tolleranza è cambiata. Il meccanismo usa l'API documentata [SetThreadDpiAwarenessContext](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setthreaddpiawarenesscontext).

### Prova nell'applicazione e documenti

Importazione eseguita nell'applicazione reale, calcolata e salvata in `prova-importazione-reale.anthea`: sezione 600 × 800 mm, un foro, quattro barre Ø20, origine CAD in cm e SHA-256 coerente. Anteprima, annullamento e geometria calcolata sono coperti anche dalle prove WPF.

Controllate tutte le pagine delle cinque relazioni sia nel PDF nativo (37 pagine complessive) sia esportate da Microsoft Word (39). Le due brevi occupano due pagine in entrambi i renderer. Nessun taglio o pagina vuota; controllo ravvicinato di geometria DXF, barre, gradienti e avvisi del ponte con momenti negativi. Evidenza `qa-relazioni-esito.json`. Guide Rev77: pratica 147 pagine, teoria 216, tutte renderizzate e controllate; indice aggiornato.

Release inizialmente pubblicata in `app`, con 22 impronte concordi (`eseguibile-locale-finale.json`). Dopo la correzione dei layer, copia finale in `supporto/artefatti/import-dxf-20261119/app-finale`, con tutte le DLL confrontate con build/set GPC, licenza netDxf, 169 file di esempio verificati e controllo assenza codice di prova superato; impronte in `eseguibile-dopo-layer.json`. Il comando `Apri-sezione-composta.cmd` apre direttamente il modulo. L’istanza precedente in `app` è lasciata aperta: Windows è bloccato e non è stato possibile osservare sul desktop la nuova finestra. Le viste fuori schermo sono verificate. Nessun installer o installazione separata aggiornato.

### Verifiche ancora aperte

- Dodici confronti F4 non eseguibili: mancano le catture originali in `supporto/artefatti/refactoring/f4/` (before-port-1.7.1, last-adapters-before, residual-before, bridge-before, input-contracts-before e wall-sigma-study/independent.json). Sono richiesti gli originali, di cui il repository conserva le impronte: non vengono rigenerati con il codice corrente. Le catture correnti `baseline/f4` e `baseline/cattura` passano, ma non sostituiscono questi confronti.
- Restano NOT-RUN ElasticPile.Checks, BridgeValidationCurrent, ElasticPile.Performance e banco-ca per sorgenti/fixture esterne mancanti. Le 14 suite delle librerie sono attestate dal set ufficiale, non ripetute localmente. Nessuna verifica dell'installer, nessun rilascio.

La corsa mirata finale `layer-omonimi-finale` termina con tutti i controlli selezionati PASS; l’audit `contrasto-96-globale` termina con 1 PASS, 1 KNOWN e zero NEW-FAIL (2715,2 s di audit). Tutte queste corse conservano `trackedModified: []`.

Le suite WPF restano **74 prima e dopo**. `verifiche-finali.json` riunisce l'ultimo esito disponibile per suite e conserva la provenienza di ogni corsa; non rappresenta una nuova full integralmente passata. Nessun commit o push.

## Registrazione Git del 20 novembre 2026

Su richiesta esplicita dell'utente, codice registrato in 1b9c7c4f, infrastruttura delle prove in d4a194a9 e guide/Wiki/esempi in 60d351a7. Il fetch ha confermato origin/main fermo alla base 7ee431c9, senza nuove modifiche da integrare. Il contenuto del codice resta quello verificato il 19/11; non viene dichiarata una nuova corsa completa. Commit e push autorizzati, con le limitazioni F4 e delle suite esterne conservate.
