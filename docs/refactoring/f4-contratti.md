# F4.3a — collocazione e contratti prima dei port

Decisioni operative del 13 ottobre 2026, a valle delle catture F4.1 e delle
protezioni F4.2. Questo passo definisce i confini; non introduce assembly vuoti
e non dichiara trasferiti i motori ancora in ANTHEA.

## Dipendenze

- `GPC.Design` sarà nel repository Checker, nello stesso treno di rilascio:
  orchestrerà progetto e ricerca discreta tramite Model e i checker esistenti.
  Model non dipenderà da Checker o Design; i checker non dipenderanno da Design.
- Verifiche delle sezioni dei muri CA e CLS non armato in Concrete/Walls.
  Riuso di solutori di sezione, taglio, dettagli, aderenza e durabilità esistenti.
- Azioni, equilibrio, contatto, portanza e terreno restano in Geotechnics/Walls.
  Il materiale muratura non ha un verificatore equivalente nel codice censito:
  il suo kernel avrà un modulo dedicato `GPCChecker.Masonry`, a valle di Model,
  aggiunto con il port F4.8, senza dipendere da Concrete o Geotechnics.
  La composizione delle verifiche del muro spetta a Design; le resistenze
  assegnate rimangono un modo esplicito del relativo verificatore.
- Application legge e migra documenti, valida dati richiesti, converte unità
  e coordina le chiamate. Reports compone testo/figure; Desktop presenta.
  Nessun contratto GPC usa JSON di ANTHEA, finestre, dispatcher o callback WPF.

## Contratti da mantenere comuni

Input immutabili per chiamata, norma/profilo espliciti, collezioni ordinate e
identità stabili di elemento, sezione, faccia, zona, combinazione e posizione.
Prima di introdurre un DTO di azioni riusare `ResultBeamForces` e i contratti
di Model quando conservano le convenzioni necessarie; non duplicare i cataloghi
di materiali, le geometrie o gli esiti comuni.

I nuovi servizi di sezione ricevono N, mm, N·mm e MPa; N negativo a compressione
come i solutori esistenti. Le azioni del muro geotecnico sono invece per unità
di lunghezza, con compressione positiva. L'adattatore deve dichiarare larghezza
della striscia e inversione di segno una sola volta, mantenendo le due facce.
Una striscia di 1000 mm riceve, ad esempio, 1 N/mm come 1000 N e 1 N·mm/mm
come 1000 N·mm. I valori per metro esposti all'utente sono una proiezione.

`PileAction` oggi documenta kN/kNm e profondità in m: resta compatibile.
Le conversioni ai solutori non cambiano nel commit degli esiti. F4.6 deve
verificare tutte le soglie prima di introdurre nuovi ingressi o conversioni.

I risultati riusano `EngineeringOutcome`, `ExecutionStatus` e `DataStatus`
di Model. Metodo, profilo, unità, domanda, capacità, rapporto, dettaglio e
diagnostica viaggiano separati dal testo tradotto. Rapporto nullo non significa
zero: indisponibilità, errore e annullamento non possono produrre un PASS.
Una violazione già dimostrata resta negativa anche se un altro controllo è
incompleto. I dettagli costruttivi non sono certificati dal solo PASS N–M–V.

Le ricerche riusano i checker e i selettori esistenti. Catalogo, vincoli,
budget, priorità e ordinamento sono input espliciti; nessun default normativo
aggiunto nell'ottimizzatore. `CancellationToken` e avanzamento sono indipendenti
dalla UI. Annullamento non pubblica un lotto resistente incompleto; un risultato
parziale, se previsto dal contratto, dichiara chiaramente i candidati valutati.

## F4.3b conclusa; modifiche numeriche runtime ancora aperte

Il taglio senza staffe del muro in `RetainingWall.Structures` contiene ancora
una formula locale con CRd,c=0.18/γc e vmin=0.035 k^1.5 √fck, senza σcp.
`SectionShearCalculator` include il contributo di σcp e rifiuta alcuni casi in
trazione. Sostituire direttamente quella chiamata cambierebbe quindi risultati
e ammissibilità. Non è un port equivalente e non viene eseguito qui.

Il [confronto F4.3b](f4-taglio-muri.md) ora copre N nullo, compressione,
trazione e limite di σcp, con fonte primaria, attesi indipendenti ed effetto
sui muri catturati. La decisione di riusare la API completa va attuata in
F4.5/F4.7, separando correzione e port equivalente. D7-c e D7-d restano valide.

F4.4 viene articolata in **a)** API tipizzate additive nelle librerie e test,
**b)** set coerente e adozione nei consumatori ANTHEA/report. Le stringhe legacy
rimangono compatibili nella prima parte; non alimentano le nuove decisioni.
Il numero di versione delle librerie con API additive incrementa il secondo
componente, secondo AGENTS.md. Nessuna DLL candidata sostituisce il set V.
