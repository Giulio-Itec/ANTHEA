# Dati e risultati del modulo in cemento armato

Controllo del 25 settembre 2026 sull'interfaccia, sulle sorgenti dei dati e sul confronto dei fogli di progetto. Le formule dei verificatori e il solver Checker non sono stati modificati.

## Dati comuni e dati specifici

| Gruppo | Sorgente e comportamento |
| --- | --- |
| Geometria e copriferro | `input`, modificabili nel pannello di controllo. Il copriferro nei dettagli costruttivi è un richiamo in sola lettura. Sono confrontati solo i parametri della forma attiva, compreso il foro centrale. |
| Armatura ordinaria | Wizard oppure coordinate manuali. Con coordinate manuali il wizard è disabilitato e i suoi valori non partecipano alla validazione né al confronto. I valori sono conservati per il ripristino del wizard. |
| Staffe | Presenza, diametro, passo e schema hanno un solo punto di modifica, nel pannello di controllo. Taglio e dettagli richiamano gli stessi dati in sola lettura. I rami non sono ripetuti nei parametri delle singole direzioni di taglio. |
| Materiali | Le proprietà numeriche adottate restano la sorgente del calcolo. Cataloghi e materiali personalizzati sono modalità di assegnazione. I valori derivati visualizzati non sono ingressi indipendenti. |
| Coefficienti | Un solo pannello di modifica. `alpha_cc`, `gamma_c` e `gamma_s` in `input` sono autorevoli; i corrispondenti campi del workspace sono sincronizzati per compatibilità con gli archivi. |
| Azioni | Le famiglie plastica, elastica e SLE conservano azioni distinte. Domini 2D e 3D condividono le combinazioni della stessa famiglia. I carichi non partecipano alla propagazione fra fogli. |
| SLE | `sle_comuni` guida analisi, omogeneizzazione, ambiente e opzioni comuni di fessurazione. Le copie per famiglia sono sincronizzate. I campi n e φ sono legati fra loro, non costituiscono due ingressi indipendenti. Il report riporta le impostazioni comuni una sola volta. |
| Domini | Piano 2D, discretizzazione, strategie e riferimenti delle azioni rimangono specifici dell'analisi. Opzioni grafiche e filtri non cambiano le azioni verificate. |
| Taglio e torsione | Geometria derivata oppure assegnazione manuale esplicita. Modello di calcolo, cotangenti, ancoraggio e armatura disponibile per torsione sono dati specifici. Le staffe fisiche provengono dai dati comuni. |
| Trefoli | Materiale predefinito per i nuovi cavi e materiale assegnato a ciascun cavo hanno scopi distinti. Area e diametro equivalente sono collegati. |
| Durabilità | Aggregato, vita utile, qualità e tolleranza sono confrontati anche con la scheda Materiali, convertendo unità e valori booleani. Modelli EC2 e NTC differenti non sono uniformati implicitamente. |
| Dettagli, ancoraggi e curvatura | Conservano i parametri propri dell'elemento, della giunzione e del percorso di carico; geometria e materiali sono quelli comuni della sezione. Le conferme sul disegno restano esplicite. |

## Correzioni

- Eliminati i doppi ingressi dei rami delle staffe e del copriferro nei dettagli.
- La modifica dello schema delle staffe aggiorna anche i dettagli costruttivi.
- I parametri del wizard inattivo non bloccano più le barre manuali; il diametro delle barre laterali assenti non blocca la sezione.
- Il confronto di progetto ignora dimensioni di altre forme, file disattivate, armature automatiche sostituite da quelle manuali e dimensioni delle staffe assenti. Rileva invece presenza delle staffe, foro e dimensioni effettive del foro, prima incompleti nel confronto.
- I campi booleani opzionali assenti e falsi non producono conflitti spuri sulle seconde file.
- Confronto e report di progetto usano la stessa regola di pertinenza e lo stesso catalogo di etichette. I valori derivati dell'interfaccia e il vecchio n di riferimento non vengono ristampati come ingressi del progetto.
- Il controllo del copriferro usa il diametro massimo effettivo delle barre della sezione. Differenze nei parametri di durabilità impediscono un esito positivo basato su dati diversi.
- `VerificationSummary` uniforma gli esiti dei riquadri, inclusa la sezione composta. Per il CA lo stesso riepilogo viene esportato in JSON e riportato in Word per le verifiche selezionate. Un tasso non finito senza esito booleano valido è incompleto.
- Nel report CA la figura delle staffe compare una sola volta; la figura momento–curvatura viene inclusa anche quando Taglio non è selezionato.

## Compatibilità e verifiche

Gli archivi mantengono i parametri inattivi, le combinazioni e le opzioni delle verifiche. La revisione della sezione composta migra le vecchie quattro schede nelle nuove tre senza perdere fase, modalità della viewport o separatori.

Regressioni dedicate: `supporto/test/X.Verifiche --ca-data`. Regressioni numeriche esistenti: `--checker`. Controlli WPF: `--smoke-ca-features`, `--smoke-ca-extensions`, `--smoke-sharing`, `--smoke-hierarchy`, `--smoke-project-report` e `--smoke-bridge`.

I tre comandi di progetto e quello dell'acciaio avevano già un'implementazione, ma non erano raggiungibili dal selettore di avvio dei controlli automatici: ora usano lo stesso elenco di modalità del gestore degli errori.

Restano distinti gli esiti delle diverse verifiche: il riepilogo comune non somma tassi né tensioni di combinazioni diverse. Restano valide le segnalazioni del precedente audit del solver da ponte nella libreria Checker.
