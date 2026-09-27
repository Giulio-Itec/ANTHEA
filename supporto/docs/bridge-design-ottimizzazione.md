# Bridge Design — sezioni tecniche e ottimizzazione

Aggiornamento del 27 settembre 2026. Questa guida descrive le quote adottate dal modello e il comando **Ottimizza**. L'obiettivo è scegliere una configurazione ragionevole da approfondire, a parità di sito, larghezza e listino. Il risultato resta un predimensionamento: non è una verifica normativa e non dimostra la realizzabilità di un progetto esecutivo.

## Procedura pratica

1. Aprire **Bridge Design** e inserire lunghezza totale, quota sul terreno, ostacolo e terreno convenzionale. Controllare corsie, banchine, spartitraffico e barriere: questi dati definiscono la larghezza e restano costanti nella ricerca.
2. Controllare i **Prezzi unitari**. Il motore usa questi prezzi anche durante l'ottimizzazione. Un cassone può risultare conveniente con un listino e meno conveniente con un altro; i valori iniziali sono convenzionali, non un'offerta di impresa.
3. Premere **Ottimizza** sopra il disegno oppure aprire la scheda principale **Ottimizzazione**, accanto a **Progetto**. La ricerca ha uno spazio autonomo, separato dalle tabelle dei risultati del ponte.
4. Scegliere **Costo minimo**, **CO₂ minima** o **Compromesso costo / CO₂**. Selezionare le caselle delle scelte da mantenere costanti.
5. Definire il numero minimo e massimo di campate. **Suggerisci campate dalla lunghezza** ricava un intervallo compatibile con le luci usuali delle famiglie libere, fra 1 e 30 campate. È una prima selezione basata sulla luce media: il calcolo controllerà le luci effettive e gli ostacoli. Impostare eventualmente altezza minima in campata e massima anche sulle pile; zero significa nessun limite. Definire le griglie percentuali descritte sotto.
6. Premere **Avvia ottimizzazione**. La preview segue il migliore provvisorio senza modificare il progetto. I grafici si popolano durante il calcolo. **Interrompi** annulla la ricerca senza applicare una soluzione.
7. Leggere graduatoria, differenze rispetto al ponte corrente e motivi di esclusione. Impostare **Mostra le prime N** e premere **Aggiorna elenco**: la graduatoria completa è già conservata, non si ripete il calcolo. Selezionare una riga oppure un punto nella nuvola; esaminare prospetto, sezione e tabella **Corrente / Selezionata / Δ**, quindi premere **Applica soluzione selezionata**.
8. La scheda **Sezioni e quote** mostra le dimensioni della soluzione applicata. **Annulla** ripristina gli input precedenti. Se non esiste già un confronto A, l'applicazione salva come A la configurazione di partenza; un'A già presente viene conservata.

La ricerca viene invalidata quando cambiano geometria, prezzi o coefficienti. Anche cambiare obiettivo, caselle o limiti richiede una nuova ricerca. Il paesaggio e il confronto A non modificano gli indicatori della configurazione corrente.

## Cosa viene mantenuto costante

Restano sempre invariati lunghezza, quota, ostacolo, terreno, composizione della piattaforma stradale, resistenze del calcestruzzo, estremi su pila o spalla, coefficienti ambientali, carichi, ipotesi e listino. La ricerca non riduce le corsie e non sceglie materiali meno resistenti per ottenere un costo minore.

| Casella | Effetto |
|---|---|
| Mantieni tipologia | Limita la ricerca alla famiglia corrente. |
| Mantieni numero campate | Conserva il numero attualmente adottato, anche se l'input era zero. I limiti min/max devono comunque comprenderlo. |
| Mantieni altezza in campata | Fissa il valore attualmente adottato. Per il cassone variabile l'altezza sulle pile segue ancora la relativa regola del modello. |
| Mantieni dimensioni della sezione | Conserva i parametri trasversali e risolve nel valore corrente quelli automatici. Richiede la tipologia bloccata; la casella la seleziona automaticamente. L'altezza totale ha una casella separata. |
| Mantieni continuità | Conserva lo schema continuo oppure a campate indipendenti. |
| Mantieni schema pila e valori imposti | Conserva tipologia e diametro/spessore eventualmente imposto. Se il diametro/spessore è zero, il dimensionamento automatico resta attivo. |
| Mantieni schema fondazione e lunghezza pali | Conserva il tipo effettivamente adottato e la lunghezza dei pali. Numero pali e dimensione plinto esplicitamente imposti restano identici; quelli a zero vengono ricalcolati per le reazioni della nuova soluzione. |

Bloccare lo schema di fondazione non significa congelare una distinta diversa per ogni appoggio. Il modello dispone di parametri comuni e di dimensioni automatiche per appoggio; la ricerca rispetta questa rappresentazione. Per mantenere un numero o una dimensione, inserirli esplicitamente prima della ricerca.

## Quali soluzioni sono esplorate

La ricerca è discreta e deterministica. Con gli stessi input e vincoli produce lo stesso ordine dei risultati. Include la configurazione corrente, purché rispetti i filtri, e combina:

- le otto famiglie, oppure la sola famiglia bloccata;
- i numeri interi di campate nel campo scelto, da 1 a 30; inizialmente il massimo è 12;
- altezza automatica e una variante aumentata del 15% nella griglia iniziale, modificabile come descritto sotto, oppure l'altezza bloccata;
- continuità o campate indipendenti, se la continuità è libera;
- quattro schemi di pila, se lo schema è libero;
- fondazione diretta, pali da 1,0 m o pali da 1,5 m, se la fondazione è libera;
- per i pali liberi, lunghezza convenzionale del terreno e aumenti del 25% e del 50% nella griglia iniziale, modificabile come descritto sotto, entro 80 m.

Se le dimensioni della sezione sono libere, il motore usa i parametri standard della famiglia. Non prova una griglia di ogni possibile spessore, interasse, inclinazione o resistenza. In particolare non riduce le lamiere fino a una presunta resistenza limite: il modello non comprende le verifiche necessarie per farlo. Il messaggio «migliore soluzione» significa quindi migliore fra le combinazioni effettivamente esplorate.

Le rappresentazioni automatica e numerica della medesima geometria possono generare due combinazioni di input. La graduatoria elimina queste ripetizioni. Il conteggio delle combinazioni ammesse precede la deduplicazione della lista presentata.

## Intervalli di ricerca modificabili

Le griglie iniziali sono 100–115% con passo 15 punti percentuali per l'altezza e 100–150% con passo 25 punti per i pali. Non sono intervalli di confidenza: definiscono precisamente quali varianti vengono provate. L'utente può modificarle nell'intervallo 100–200%, con un massimo di 11 valori per griglia. Il valore massimo viene sempre incluso, anche se il passo non divide esattamente l'intervallo: 100–115 con passo 10 produce 100, 110 e 115%.

Per ogni famiglia, schema e numero di campate, il 100% dell'altezza corrisponde alla quota ricavata dalla regola di predimensionamento. Le varianti maggiorate sono arrotondate verso l'alto a multipli di 5 cm. Ad esempio, con altezza automatica 1,83 m e griglia 100, 110, 120%, le quote provate sono 1,83, 2,05 e 2,20 m. La griglia non scende sotto la quota di riferimento: il modello non comprende le verifiche necessarie per cercare una sezione più snella della regola adottata. Se l'altezza è bloccata, i campi percentuali vengono disattivati e si conserva la quota del progetto.

Per i pali, il 100% è la lunghezza convenzionale associata al terreno nel modello: 10, 15, 22 o 30 m. La percentuale modifica questa lunghezza per le due famiglie di fondazioni su pali. Per i plinti diretti non introduce varianti. Bloccando la fondazione si conserva la lunghezza adottata e la griglia viene disattivata. Non si sta calcolando una stratigrafia geotecnica ottima.

La configurazione corrente viene comunque aggiunta alla ricerca, anche se le sue quote non sono un punto delle griglie percentuali. Deve rispettare i limiti assoluti e tutti i filtri di ammissibilità. Questo consente di confrontare le proposte con un riferimento effettivo senza perdere una configurazione corrente già conveniente.

Prima del calcolo viene stimato il numero di tentativi della griglia, filtrando le coppie famiglia/numero di campate la cui luce media è incompatibile. Il limite è 50.000 tentativi, compreso il riferimento iniziale. Una griglia eccessiva viene rifiutata con un messaggio: restringere gli intervalli, aumentare i passi o bloccare alcune scelte. Il numero finale di calcoli può essere inferiore alla stima perché gli input duplicati non vengono ricalcolati.

## Grafico delle variazioni

L'asse orizzontale è il numero progressivo del tentativo realmente calcolato, non un numero di generazione di un algoritmo genetico. Il selettore cambia l'asse verticale fra costo, CO₂, altezza in campata, numero di campate, lunghezza pali, tipologia, schema di pila, fondazione e continuità. Le ultime quattro variabili sono categorie: la distanza verticale fra due categorie non ha significato numerico.

I punti colorati sono ammessi dai filtri; quelli grigi sono esclusi. Passando il mouse si leggono i dati del tentativo e i motivi di esclusione. Se un calcolo fallisce prima di produrre una quota, quella quota manca e non viene rappresentata come zero. Il tentativo resta nella traccia e nel conteggio delle esclusioni. Nei grafici di costo e CO₂, la linea scura mostra il minimo progressivo fra i tentativi ammessi: la sua unità rimane costante durante la ricerca.

La successione dei punti mostra l'ordine della griglia, non un percorso continuo fra progetti. Un salto del costo o dell'altezza può corrispondere a un cambio di tipologia o di schema. Non è necessariamente un peggioramento dell'ottimizzazione: la ricerca deve esplorare anche soluzioni meno efficienti per costruire il confronto.

## Nuvola delle soluzioni e alternative ordinate

Il secondo grafico usa costo in milioni di euro in orizzontale e CO₂ in tonnellate in verticale. A ricerca conclusa include tutte le geometrie distinte ammesse, anche quelle oltre le prime N righe. I colori distinguono le otto famiglie; un filtro permette di visualizzare una sola tipologia. La croce identifica il ponte corrente, la stella l'ottimo secondo l'obiettivo scelto e il cerchio scuro la soluzione selezionata. La selezione può quindi essere diversa dall'ottimo.

Gli anelli indicano la frontiera Pareto. Il filtro **Solo frontiera Pareto** conserva le alternative non dominate globalmente nella ricerca, anche quando si filtra una tipologia. Le linee che collegano i punti Pareto sono una guida visiva; non rappresentano progetti intermedi calcolati. Più geometrie possono avere lo stesso costo e la stessa CO₂ e apparire sovrapposte: la graduatoria consente di selezionarle singolarmente. Cliccando su punti coincidenti si seleziona il rango migliore fra quelli più vicini.

Durante il calcolo la nuvola mostra i tentativi ammessi fino a quel momento; la deduplicazione geometrica e la frontiera definitiva sono disponibili alla fine. Il migliore provvisorio può cambiare. Con il compromesso 50/50 cambiano anche i minimi usati nella normalizzazione man mano che la ricerca avanza: il punteggio provvisorio non va interpretato come un indicatore assoluto di convergenza.

La tabella ordina l'intera famiglia con lo stesso criterio del motore. N regola soltanto quante righe visualizzare, da 1 a 50.000, limitate alle soluzioni disponibili. Il punteggio viene ordinato prima dell'arrotondamento visualizzato. In caso di parità, si confrontano costo, CO₂, identificativo di famiglia e numero di campate; l'ordine residuo è quello deterministico di esplorazione. Il costo minimo è in euro, l'obiettivo ambientale in tonnellate e il compromesso è adimensionale.

Selezionare una soluzione aggiorna la preview e il confronto delle quote senza cambiare gli input del progetto, anche scegliendo un punto fuori dalla top N. La tabella include costi, CO₂, luci, schema, continuità e le dimensioni tecniche adottate. Il simbolo «n.a.» indica che una quota della famiglia selezionata non ha una controparte nella famiglia corrente; non significa zero.

## Aggiornamenti della preview

L'aggiornamento a ogni tentativo renderebbe difficile leggere il disegno. Il motore comunica il progresso in gruppi, circa ogni 200 ms. La UI aggiorna la preview solo quando cambia il migliore provvisorio e non più di due volte al secondo. Il risultato finale viene sempre mostrato. La ricerca rimane in un'attività separata dal thread grafico e può essere interrotta; un'interruzione cancella i risultati provvisori e non applica cambiamenti al ponte.

Cambiare progetto, prezzi o vincoli invalida risultati e preview. Il cambio di N, del parametro rappresentato, dei filtri del grafico o della vista prospetto/sezione non modifica la ricerca. Il disegno usa lo stesso componente grafico del progetto, mentre intervalli, tentativi, ammissibilità, punteggi e Pareto sono calcolati nella libreria indipendente dall'interfaccia.

## Filtri di ammissibilità

Una soluzione è esclusa se il calcolo non è possibile, se supera i limiti dell'utente o se non soddisfa i seguenti criteri orientativi:

- tutte le luci devono rientrare nel campo usuale della famiglia;
- l'altezza in campata non deve essere inferiore alla regola automatica della famiglia, anche quando è imposta manualmente;
- le pile interne devono rimanere fuori dall'ostacolo con il margine geometrico di 1 m per lato;
- rapporto massimo fra carico assiale e riferimento di fondazione non superiore a 1;
- snellezza convenzionale massima delle pile non superiore a 100;
- compressione media nelle pile non superiore a 0,30 della resistenza convenzionale del cls;
- non più di 64 pali per appoggio e plinto compatibile con la disposizione convenzionale a interasse 3 diametri;
- nessuna reazione verso l'alto, poiché i dispositivi antisollevamento non sono dimensionati;
- nessuna sovrapposizione trasversale delle travi a U o eccedenza dell'ingombro dei cassoni metallici.

Una configurazione può violare più criteri: le occorrenze dei motivi di esclusione non vanno sommate per ricavare il numero di soluzioni escluse. Se non rimane alcuna alternativa, la finestra presenta i motivi e non applica cambiamenti. Occorre valutare se liberare uno schema, aumentare l'altezza consentita o rivedere i dati di sito.

Questi filtri non sostituiscono resistenza a flessione/taglio, instabilità, fatica, esercizio, fasi costruttive, precompressione, sisma o geotecnica completa. Una soluzione ammessa è una proposta di studio, non una struttura dichiarata sicura.

## Criteri economici e ambientali

Il costo deriva dal computo del modello: somma delle quantità moltiplicate per i prezzi, maggiorata degli oneri di cantiere e degli imprevisti. Gli importi sono in EUR, IVA esclusa. Prezzi nulli sono consentiti dal modello e comportano costi nulli per le relative voci: devono essere una scelta consapevole, non dati dimenticati.

L'impronta comprende cls e acciai e la maggiorazione convenzionale per trasporti/cantiere. È un indicatore parziale, privo di EPD specifiche; non è l'impronta completa dell'intero ciclo di vita del ponte.

Nel compromesso, per ogni candidato si calcola:

`punteggio = 0,5 × costo / costo_minimo + 0,5 × CO₂ / CO₂_minima`

I due minimi sono ricavati dalle soluzioni ammesse della stessa ricerca. Il costo minimo al denominatore è limitato inferiormente a 1 EUR e la CO₂ minima a 10⁻⁹ t, così il calcolo resta definito anche con indicatori nulli. Il punteggio è adimensionale: inferiore significa migliore secondo il criterio scelto. Cambiare i vincoli può cambiare i minimi di normalizzazione; non confrontare punteggi di ricerche diverse come valori assoluti.

La colonna **Pareto** indica che nessun'altra soluzione ammessa presenta costo e CO₂ entrambi non maggiori, con almeno uno strettamente minore. Non introduce una verifica strutturale aggiuntiva. Una soluzione più economica può emettere più CO₂; la graduatoria rende visibile lo scambio attraverso le differenze rispetto al riferimento.

## Leggere sezioni e quote

La tabella della sezione riporta componente, simbolo, valore, unità, origine e significato. **Impostato** identifica un input esplicito, **Automatico** una regola del modello, **Derivato** una conseguenza degli input. Il valore visualizzato è adottato dal calcolo, anche quando nell'editor rimane zero per richiedere l'automatismo.

Le altezze `d` e `d_pila` comprendono la soletta. `h` è l'altezza della trave sotto soletta e rialzo. `h_w` è l'altezza netta dell'anima; `l_w` è il suo sviluppo reale quando è inclinata. Le quote di piattabande, anime e solette sono in mm; luci, larghezze e altezze generali in m. Il prospetto mantiene precisione numerica utile a ricostruire il computo, senza attribuire tale precisione alle stime di progetto.

Per i cassoni metallici, `H/4V = 1` significa inclinazione 1 orizzontale su 4 verticali. Lo scarto orizzontale è `Δx = h_w × H/4V / 4` e lo sviluppo dell'anima è `sqrt(h_w² + Δx²)`. La massa di carpenteria include un'aggiunta del 15% per diaframmi, irrigidimenti e collegamenti; questa aggiunta non aumenta la rigidezza flessionale.

Per le travi a I in c.a.p. le flange e l'anima sono rettangoli equivalenti del modello ANTHEA, non un profilo prefabbricato AASHTO selezionato da catalogo. Per i cassoni a conci, `d_eq = d + (d_pila − d)/3` è l'altezza media usata nelle quantità e nella rigidezza. Questa approssimazione non analizza le fasi a sbalzo.

La tabella delle campate associa a ciascuna luce gli assi degli appoggi iniziale e finale. Lo sviluppo `n × Li` è la lunghezza teorica degli elementi longitudinali: non comprende giunti costruttivi, sovrapposizioni, sfridi e pezzature di officina. L'interasse `s_rif` serve a ricavare il numero delle travi; non determina da solo la posizione esecutiva degli sbalzi laterali.

La tabella delle fondazioni riporta dimensioni per ciascun appoggio, non solo un valore medio. Le misure del plinto sono B longitudinale × W trasversale × t spessore. I pali sono numero × diametro × lunghezza. Le spalle sono equivalenti volumetrici: il modello non produce una carpenteria esecutiva di muri frontali, paraghiaia e muri d'ala.

**Esporta sezioni e quote CSV** salva questi dati con unità e significato. Il report Word singolo e quello di progetto includono gli stessi prospetti. L'esportazione delle quantità rimane disponibile per il computo economico.

## Confronto con il sito e verifiche del software

La campagna sul sito e i test del programma rispondono a domande diverse. I test interni verificano equilibrio, formule, geometrie, prezzi, vincoli e funzionamento della finestra. Il confronto live verifica invece come i risultati di ANTHEA differiscono da quelli effettivamente visualizzati dal sito.

Il dataset, gli input tradotti, gli output del motore, gli scostamenti e il report sono in `supporto/artefatti/bridge_design_site_1000`. Il motore non viene calibrato forzando costi o quantità sui risultati di riferimento. Le differenze fra famiglie, fondazioni, listini e perimetro delle opere provvisionali restano identificabili.

Per ripetere i test numerici e dell'interfaccia:

```powershell
dotnet run --project supporto/test/BridgeDesign.Checks -c Release -- supporto/artefatti/bridge_optimization_explorer/calcoli
dotnet build X.Desktop/X.Desktop.csproj -c Release --no-restore
dotnet X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll --smoke-bridge-design supporto/artefatti/bridge_optimization_explorer/ui
```
