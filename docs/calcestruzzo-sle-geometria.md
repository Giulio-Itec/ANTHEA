# Aggiornamento SLE, geometria e trefoli

## Calcolo e cache

Le famiglie Rara, Frequente e Quasi permanente restano parallele. All'interno
di ciascuna famiglia le combinazioni sono distribuite su un massimo di due
worker, limitati in funzione dei processori disponibili. Ogni worker utilizza
il proprio solver; geometria e materiali condividono la sezione preparata.
L'inizializzazione dei solver resta serializzata perché la triangolazione
interna della DLL non è thread-safe.

Esposizione, sensibilità dell'armatura, durata, aderenza, copriferro per le
fessure e interasse non appartengono alla chiave della cache tensionale.
Cambiare questi dati aggiorna la fessurazione senza ripetere l'equilibrio SLE.
Per decompressione/formazione delle fessure rimane necessaria l'analisi
ausiliaria della sezione omogeneizzata interamente reagente, distinta
dall'analisi SLE principale.

## Interasse delle barre tese

Campo vuoto: automatico. Un valore positivo inserito dall'utente prevale.
La selezione delle barre è fatta per combinazione, usando il piano di
deformazione della DLL e l'area efficace già utilizzata dalla fessurazione.
Per le sezioni circolari si misura l'arco fra barre consecutive sulla corona.
Per rettangolari e T si considerano le coppie consecutive delle file
orizzontali e verticali, evitando collegamenti che attraversano i vuoti della
T. Si prende il massimo delle distanze valide, anche fra file allineate:
quest'ultima scelta può essere conservativa nelle disposizioni multistrato.
Non si sostituisce la spaziatura massima con il minimo del vicino più prossimo.
Se non esistono coppie idonee si richiede il dato manuale.

Il risultato riporta interasse e provenienza. La formula di apertura delle
fessure preesistente non è stata modificata. Riferimento: Circolare 21 gennaio
2019 n. 7, § C4.1.2.2.4.5 e figura C4.1.11.

Il contratto `ITensionBarSpacing`, la classe `TensionBarSpacing` e il punto di
iniezione `Ntc2018Checks.SpacingCalculator` separano la geometria dalla UI.
Un futuro adattatore Checker potrà sostituire questo servizio, convertendo
la geometria in ingresso nei tipi Geometry della libreria.

## Geometria e grafica

- Gli identificativi longitudinali sono B01, B02, … (senza troncamento oltre 99).
- La fila all'intradosso dell'ala della T ha numero, diametro e offset
  intradosso–asse barra; numero zero mantiene i vecchi fogli senza aggiunte.
  È distribuita sull'intera larghezza dell'ala. Copriferro, spessore e
  sovrapposizioni vengono controllati.
- Per le staffe circolari sono disponibili bracci interni paralleli e staffe
  chiuse interne sovrapposte, ruotabili. Nel primo schema uno/due bracci
  aggiuntivi corrispondono a tre/quattro braccia nella direzione scelta.
  Nel secondo schema il numero indica le staffe chiuse aggiuntive.
  Il disegno è indicativo; il modello resistente a taglio circolare si sceglie
  esplicitamente nella relativa scheda. Vedere le
  [estensioni del modulo](calcestruzzo-estensioni.md) per campo e ipotesi.
- Rosso indica compressione negativa, blu trazione positiva.
- Le scale di utilizzo distinguono cinque fasce: fino a 0,50; 0,50–0,70;
  0,70–0,90; 0,90–1,00; oltre 1,00. Legende verticali.
- “Tutti i punti resistenti” è indipendente dalla selezione delle azioni:
  rispetta i filtri della tabella e le righe visibili. “Resistenze” nasconde
  tutti i punti resistenti.

## Trefoli

Riferimento implementativo: CheckerUI, ModuleConcreteSection,
`MainViewModel.AddTendon`, tabella Tendons e pannello TendonMaterialView.
Un cavo con n trefoli viene rappresentato con Ø equivalente = Ø singolo × √n.
Il diametro singolo è quello equivalente all'area metallica, non il diametro
esterno nominale. La tabella conserva diametro equivalente del cavo, area
totale, coordinate, σp0 e proprietà del materiale; area e diametro si
aggiornano reciprocamente.

Ogni cavo può avere un materiale distinto e una legge elastoplastica o
incrudente. Il catalogo materiali è salvato nel foglio. Applicare un materiale
agisce sul cavo selezionato e sul predefinito dei nuovi inserimenti.
I vecchi record con area e proprietà esplicite restano leggibili.
L'interfaccia non aggiunge nuove verifiche CAP: apertura delle fessure con
trefoli e identificazione SLE dei trefoli con σp0 nullo conservano i limiti
espliciti già presenti.

## Dettaglio diagnostico della fessurazione

La scheda «Dettagli combinazione → Fessurazione · passaggi» e il report con
opzione dettagli condividono un riepilogo di massimo 30 valori, a due decimali
(notazione scientifica per valori molto piccoli): geometria efficace, materiali, coefficienti, deformazioni,
distanze fra fessure, apertura e tasso di lavoro. Ogni riga comprende unità e
una breve descrizione o formula, con riferimenti alla Circolare 2019
§ C4.1.2.2.4.5. Decompressione e formazione mostrano soltanto i valori pertinenti.
La selezione modifica esclusivamente la presentazione, non i calcoli.
Il testo resta selezionabile e copiabile; nel JSON rimane la traccia completa
non arrotondata, incluse le singole barre e i passaggi intermedi.

k₂ viene selezionato per ogni combinazione considerando tutte le armature
ordinarie, comprese quelle esterne alla fascia efficace: almeno una tensione
negativa determina k₂ = 0,50 (flessione); in assenza di barre compresse si usa
k₂ = 1,00 (trazione). Le barre a tensione esattamente nulla non sono compresse.
Il riepilogo riporta il criterio; i conteggi restano nel JSON. I limiti del modello di area
efficace per sezione interamente tesa rimangono espliciti.
Le altre scelte restano visibili: fct,eff è assunto uguale a fctm,
σs è il massimo sulle barre efficaci e
αe = Es/Ecm è distinto da n dell’analisi con viscosità. Queste sono assunzioni
del percorso implementato, da controllare nel confronto con altri calcoli;
il riepilogo non costituisce una nuova validazione normativa del metodo.

## Verifica

### Pannello, materiali e grafici (settembre 2026)

- Nuovi fogli: sezione rettangolare, staffe passo 200 mm, CLS C35/45 e barre
  B450C dal catalogo DLL; trefoli Y1860 predefiniti per i nuovi cavi.
  I fogli esistenti conservano i propri dati.
- Materiali salvati e standard sono immutabili nel pannello e nella tabella
  dei cavi. «Nuovo materiale» crea una copia modificabile, da salvare nel foglio;
  il database condiviso non è ancora implementato. NTC propone B450A/B450C;
  gli altri codici disponibili usano il catalogo EN 1992 (Model Code usa il suo
  catalogo CLS). Non esistono cataloghi specifici distinti per ogni annesso.
- Il materiale predefinito dei trefoli riguarda i nuovi cavi; il menu della
  singola riga assegna il materiale a quel cavo senza modificare gli altri.
- La modifica di x/y/Ø nella tabella barre salva una disposizione manuale,
  usata anche dal motore Checker. Il wizard torna autorevole solo con
  «Ripristina barre da wizard». Posizioni e sovrapposizioni restano validate;
  se le barre manuali di una sezione circolare non formano più un unico anello,
  la spaziatura automatica per arco non è applicabile: inserirla manualmente.
- «Proprietà sezione» riporta geometria CLS, proprietà omogeneizzate della DLL
  (sezione integra), quantità e diametri delle armature, area dei trefoli.
  Il comando «Proprietà / report…» è disponibile anche sopra la preview;
  la finestra consente copia del testo ed esportazione del report TXT.
  Si può impostare φ oppure n delle armature ordinarie: n = Es(1+φ)/Ecm,
  con φ ≥ 0. Per i trefoli è mostrato n relativo a ciascun Ep, a φ comune.
  Le opzioni sono salvate nel foglio ma non modificano le analisi SLE.
  Le proprietà sono calcolate dall'overload della DLL con φ, riutilizzando
  la stessa sezione preparata per la finestra.
- I dati del materiale dei trefoli sono campi dedicati (Ep, fpyk, fpk, εpu,
  diagramma), in sola lettura come i materiali CLS/acciaio.
  «Nuovo materiale» consente l'inserimento diretto di fck per il CLS oppure
  fyk/fu e delle altre proprietà dell'acciaio; non propone un materiale di
  partenza da selezionare. Il diagramma affiancato mostra
  le curve caratteristiche e di progetto, campionate dalle leggi della DLL
  usando i coefficienti effettivi del foglio; la compressione è negativa.
  Il salvataggio conserva l'origine e la normativa del materiale.
  Solo il grafico CLS è riflesso: compressione nel primo quadrante, con
  etichette di entrambi gli assi negative. I dati della legge costitutiva e
  i grafici degli acciai mantengono i segni originali.
- La preview dispone di tre checkbox indipendenti, salvate nel foglio:
  dimensioni, copriferro e interferro minimo. Le quote sono in mm e seguono
  la geometria corrente, comprese anima/soletta della T e diametro circolare.
  Il copriferro è quello netto alla staffa; l'interferro è la distanza libera
  minima tra le superfici delle barre ordinarie (non interasse né arco).
  Sono opzioni grafiche, senza ricalcolo strutturale.
- I riquadri delle verifiche condividono la legenda dei punti, mantengono
  separati 3D/2D, tensioni/fessurazione e Vx/Vy e segnalano gli esiti mancanti.
  L'esito senza tasso non viene trasformato artificialmente in un tasso numerico.
  I riquadri sono compatti su due righe, con combinazione governante e tasso;
  i dettagli completi restano nel tooltip. Il pannello destro raccoglie tutte
  le categorie nella stessa schermata alle dimensioni desktop verificate.
- Raggi grafici Ed/Rd separati in 2D/3D; tutti i punti resistenti attivano
  le relative linee. La scelta «Forze: selezionata» resta indipendente.
  I due slider delle dimensioni dei punti sono visibili nel pannello sinistro.
  La discretizzazione angolare ricostruisce il dominio nativo. Interpolazione
  lineare/quadratica e suddivisioni N aggiornano invece soltanto la mesh,
  usando una copia dei punti nativi: risultati, tassi e solver restano gli stessi.
  Le facce trasparenti sono ordinate in profondità rispetto alla telecamera;
  il reticolo usa spigoli unici sul lato visibile, tracciati sullo schermo per
  evitare conflitti di profondità con la superficie. Test incluso a 64 direzioni.
- Il wizard rettangolare permette una seconda fila superiore e inferiore,
  attivabili separatamente. Il circolare permette un secondo anello interno.
  Tutti sono spenti inizialmente; numero, Ø e distanza **libera** dalla prima
  fila sono modificabili. Posizioni, spazio disponibile e sovrapposizioni
  sono validati e la disposizione alimenta la DLL, non soltanto la preview.
  Con barre manuali i comandi restano disabilitati fino al ripristino del wizard.
  Per il taglio rettangolare automatico d e Asl includono i secondi strati;
  il metodo di interasse circolare considera separatamente i due anelli del wizard.
  Nella T l'ultima barra laterale raggiunge la quota interna della staffa alta.
- Tabelle di barre e trefoli nello stesso riquadro, con sottoschede distinte.
  Input a sinistra, viewport e dettagli affiancati, combinazioni in basso:
  proporzioni e divisori uniformi fra dominio 3D, 2D, SLE e taglio.
- I riepiloghi e il report non mostrano verifiche esplicitamente non richieste:
  fessurazione rara NTC e verifica tensionale frequente. Le tensioni frequenti
  continuano a essere calcolate per la fessurazione e consultabili nella vista.
  Non vengono nascosti errori, verifiche applicabili mancanti o normative
  non implementate; queste conservano l'avviso esplicito.

Suite numerica: `dotnet run --project X.Verifiche -c Release -- --checker`.

Campi e tabelle del modulo CA mostrano al massimo due decimali per coordinate,
geometria, materiali, tensioni e azioni. In modifica resta disponibile il valore
completo: il solo cambio di focus non arrotonda i dati e non avvia ricalcoli.
Calcoli, salvataggi e scambi numerici conservano la precisione originale;
le deformazioni molto piccole restano leggibili in notazione scientifica.

Test mirato WPF: `dotnet run --project X.Desktop -c Release -- --smoke-ca-features verifiche_ca_features`.
Quest'ultimo confronta seriale/parallelo lineare e non lineare, controlla
l'identità dei risultati al cambio ambiente, le nuove geometrie, le staffe,
i materiali distinti dei trefoli e il round trip JSON.
