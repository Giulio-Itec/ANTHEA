# Paratie: requisiti e verifiche di accettazione

Incarico del 18 ottobre 2026. Questa matrice conserva l'intero ambito richiesto; un
incremento utilizzabile non costituisce la chiusura del modulo. Stato attuale:
**versione testabile 1.11.0 integrata in F5 con set GPC ufficiale AA;
modulo ancora in sviluppo e tre prove ambientali aperte autorizzate**.
Nessuna equivalenza numerica con prodotti esterni
è dichiarata senza un confronto riproducibile a parità di ipotesi.

## Basi isolate

| Repository | Revisione iniziale | Ramo di lavoro |
| --- | --- | --- |
| ANTHEA | b9c2d4b1cd54b4a64c8d6fa2c8c0b20207f8dc73 (`refactoring/f5`) | refactoring/f5-paratie |
| Model | bf8563f56a947c61d5f681fbd362059a35de1bf4 | codex/paratie |
| Checker | bce7aaaef9eabbfe359a79f4bb4ed1ee2aaa4b2e | codex/paratie |
| Geometry | 6734eee1004142576b96759e34c15dec48776e23 | sola lettura |
| Utilities | e57b440b16a0a311a7bf0af2f9cf4d2681b83491 | sola lettura |

Ricognizione iniziale: cartelle sorelle in
`C:/Users/g.pacini/Documents/Repository/ParatieWork`; i quattro master GPC
coincidevano con origin/master dopo fetch del 18/10/2026. Il lavoro iniziale
non usava le cartelle originali. Il merge anticipato autorizzato il 19/10 ha
poi riportato ANTHEA su `refactoring/f5` e integrato Model/Checker in master;
revisioni e collaudi sono nel [verbale di integrazione](integrazione-rami.md).

## Fonti funzionali

- P: [PAC, scheda ufficiale](https://www.aztec.it/pac-paratie/), consultata il 18/10/2026.
- PM: [Manuale PAC ufficiale](https://www.aztec.it/risorse/manuali/manualepac.pdf), consultato il 18/10/2026; include cordoli, carichi assiali, armature e verifiche SLE.
- H: [Paratie Plus, scheda ufficiale](https://www.harpaceas.it/paratie-plus), consultata il 18/10/2026.
- U: requisiti espliciti dell'utente del 18/10/2026.

Le fonti commerciali identificano le funzioni; non definiscono integralmente
algoritmi, coefficienti o criteri di accettazione. I metodi implementati hanno
documentazione propria in Checker. I riferimenti commerciali restano qui, fuori
dalle guide per l'utente.

## Matrice

Stati: aperto, in corso, verificato, dipendenza esterna. «Esistente» nella colonna
riuso significa candidato verificato nei sorgenti, non integrazione già collaudata.

| ID | Fonte | Comportamento richiesto | Riuso / sede del calcolo | Integrazione ANTHEA | Accettazione | Stato |
| --- | --- | --- | --- | --- | --- | --- |
| PAR-01 | U | Contratti condivisi, unità e convenzioni | Model.Geotechnics | documento/adattatore | round trip, input invalidi | in corso |
| PAR-02 | P,H | Paratia a mensola, tirantata, puntellata | Geotechnics nuovo | geometria e sostegni | mensola analitica, reazioni | in corso |
| PAR-03 | P | Diaframmi, pali, micropali, T, berlinesi e palancole | sezioni Model, Steel, Concrete | schema 8: file singole CHS e pali circolari c.a., EI e azioni per interasse | mensola analitica e fattore fascia/elemento: incrementi 7/8; altre famiglie e rappresentazioni aperte | in corso |
| PAR-04 | P,H | Più file, quinconce, rigidezza variabile, muro in testa | Model/Geotechnics | geometria | casi limite e riduzioni documentate | in corso |
| PAR-05 | P,H | Stratigrafia, K0, attiva/passiva, drenato/ND | Soil, SoilProfile, EarthPressure | scambio terreno F5 | pressioni analitiche e falda | in corso |
| PAR-06 | P | Culmann; rottura statica/cinematica | Geotechnics nuovo | scelta metodo | casi indipendenti | aperto |
| PAR-07 | P,H | Trave FEM e molle elastoplastiche | ElasticPile come riferimento, solver nuovo | opzioni/diagnostica | equilibrio, patch, convergenza mesh | in corso |
| PAR-08 | H,U | Fasi, scavi, riporti, memoria plastica, scarico/ricarico | Geotechnics nuovo | sequenza fasi | percorso ciclico e rollback | in corso |
| PAR-09 | P,H | Tiranti attivi/passivi, inclinazione, pretensione e rimozione | Geotechnics nuovo | editor sostegni | installazione sulla deformata, unilateralità | in corso |
| PAR-10 | P,H | Puntoni, vincoli rigidi/elastoplastici, elementi interni | Geotechnics nuovo | vincoli/collegamenti | casi simmetrici e capacità limite | in corso |
| PAR-11 | P | Dimensionamento geotecnico tiranti | Geotechnics.Anchors, curve BustamanteDoix, profilo NTC Model, Design | schema 6, indagini/prove, esiti per fase, CSV/report | attesi indipendenti e pl distinta da pi: incremento 6; dimensionamento automatico, superficie/gruppo aperti | in corso |
| PAR-12 | P,H | Carichi terreno/struttura, concentrati/distribuiti/coppie | Geotechnics, carichi consistenti Hermite | schema 9, intervalli lineari per fase, q netto e CSV/report | sei attesi razionali, memoria/rimozione, equilibrio e mesh: incremento 9; diffusione sul terreno e Culmann aperti | in corso |
| PAR-13 | P,H | Falda per fase e per lato | SoilProfile, Geotechnics | falde | pressione idrostatica, equilibrio | in corso |
| PAR-14 | H | Filtrazione FEM 2D stazionaria anche non lineare | Geotechnics.Seepage, Model.Seepage | dominio a due colonne, materiali, fasi e campo nodale | Darcy, bilancio, raffinamento, mancata convergenza: incremento 2 | in corso |
| PAR-15 | H,U | Sifonamento e sollevamento idraulico | HydraulicSafety, SeepageSampling, profilo NTC Model | corpi UPL, azioni, punti/colonne HYD, snapshot e report | attesi manuali, campo affine, esiti indisponibili: incremento 3 | in corso |
| PAR-16 | H,U | Instabilità del fondo e tampone | Geotechnics | fondo scavo | benchmark distinto da UPL/HYD | aperto |
| PAR-17 | P,H,U | Stabilità globale per fase e combinazione | SlopeStability, BishopSolver, adattatore parete continua e campo FEM | geometria/azioni globali, anche carichi variabili per fase, cerchi/conci, report | cattura legacy identica, FEM e fattori; forze/momenti dei carichi variabili conservati: incrementi 4/9; sostegni e altre geometrie aperti | in corso |
| PAR-18 | P,H | Sisma e combinazioni pertinenti | profili normativi Model | combinazioni | coefficienti da fonte primaria | in corso |
| PAR-19 | H | Pushover paratie affacciate | Geotechnics/Design nuovo | curva capacità/domanda | metodo e campo verificati | aperto |
| PAR-20 | H | Interazione fra paratie attraverso il terreno | Geotechnics nuovo | sistema multi-paratia | simmetria e lavoro reciproco | aperto |
| PAR-21 | H | Effetto arco, cedimenti a tergo | Geotechnics nuovo | diagrammi terreno | modello e benchmark specifici | aperto |
| PAR-22 | H | Cerniere plastiche e risposta strutturale non lineare | Concrete/Steel/Design | leggi/sezioni | cicli e bilancio energetico | aperto |
| PAR-23 | H | Ottimizzazione infissione | Design | ricerca con limiti | ogni candidato ricalcolato | in corso: ricerca esaustiva su tutti i casi assegnati, FEM e verificatori ricalcolati per ogni storia; minimo comune distinto da candidato accettabile (incremento 13); criteri ulteriori aperti |
| PAR-24 | P,H,U | Verifiche e progetto c.a. SLU/SLE/dettagli | Concrete, Design | fasce rettangolari; schema 8 pali circolari, controlli locali e dettagli separati, CSV/report | fibre indipendenti, azioni concomitanti, entrambi i segni e SLE: incremento 8; progetto automatico e dettagli completi aperti | in corso |
| PAR-25 | H,U | Acciaio, tiranti, puntoni | Steel, trasferimento per fase in Design | aste circolari piene/CHS assiali; schema 7 CHS classe 1–2 N–M–V, esiti/report/CSV | attesi indipendenti e unità: incrementi 5/7; altre famiglie/classi, instabilità della parete e collegamenti aperti | in corso |
| PAR-26 | P,H,U | Altre norme dichiarate (storiche, EC, ACI, AISC) | profili Model/Checker | profilo esplicito | fonte e prove per ogni norma | aperto |
| PAR-27 | P,H,U | Diagrammi, inviluppi, provenienza e stati risultato | campi recuperati e inviluppi GPC | otto grandezze, estremi con azioni concomitanti, CSV/report | attesi razionali indipendenti, due lati e massimo interno; gestione multi-caso, inviluppi comuni e storie incomplete collaudati: incrementi 12–13 | in corso |
| PAR-28 | U | Persistenza, migrazioni, invalidazione, annullamento | Application/Serialization | flussi F5 | round trip, race e riapertura | in corso |
| PAR-29 | P,H | DXF, immagini, stampa e report personalizzabile/inglese | Application/Reports | export | round trip DXF e resa PDF | in corso |
| PAR-30 | U | Guide globali DOCX/PDF, wiki | Reports/Wiki | aiuto contestuale | render e controllo collegamenti | in corso: Rev52 integrata con Spettri, incrementi 1–13; 257 pagine e indici controllati; estensioni successive aperte |
| PAR-31 | H | Collegamento FLAC e analisi dinamica esterna | adattatore da specifica pubblica | interoperabilità | licenza e prova con eseguibile reale | dipendenza esterna |
| PAR-32 | H | Monitoraggio Tokbo e confronto misura/calcolo | adattatore da API documentata | serie temporali | credenziali e dati reali autorizzati | dipendenza esterna |
| PAR-33 | H | Assistenza chatbot e servizi esterni | da specificare rispetto all'incarico ingegneristico | supporto | servizio/documentazione necessari | dipendenza esterna |
| PAR-34 | U | Set coerente, test completi e rilascio | runner GPC e ANTHEA | installer 1.11.0 con set AA ufficiale | manifest e payload verificati; full e riesecuzioni documentati nel verbale, tre prove ambientali rinviate con autorizzazione | in corso: versione testabile consegnata; chiusura integrale ancora aperta |

## Sequenza e chiusura

1. Contratti e riferimento indipendente; kernel meccanico e verifiche numeriche.
2. Analisi per fasi e interazione, verificatori e servizi applicativi.
3. Interfaccia e report; prove integrate e documentazione globale.
4. Funzioni avanzate e connettori, con stessa tracciabilità.
5. Set candidato completo e collaudi. Rilascio ufficiale solo dopo autorizzazioni Git.

Nessuna riga aperta è implicitamente esclusa. Le dipendenze esterne non impediscono
lo sviluppo delle parti indipendenti. Una riga passa a verificato soltanto con
collegamento all'evidenza eseguita e descrizione dei limiti.

## Avanzamento verificato

Il [primo incremento](incremento-01.md) documenta implementazione, prove e limiti.
Il [secondo incremento](incremento-02.md) aggiunge filtrazione FEM stazionaria,
trasferimento delle pressioni, persistenza, vista WPF e report collaudati.
Il [terzo incremento](incremento-03.md) aggiunge UPL/HYD locali, dati assegnati
o campionati dal FEM e criterio esplicito dei pesi da pressioni.
Il [quarto incremento](incremento-04.md) integra la stabilità globale con il
verificatore esistente e il campo idraulico FEM.
Il [quinto incremento](incremento-05.md) collega le verifiche dei sostegni
assiali circolari a Checker Steel, con azioni e risultati tracciati per fase.
Il [sesto incremento](incremento-06.md) integra lo sfilamento geotecnico dei
tiranti, indagini pressiometriche e prove preliminari, con esiti normativi
distinti dal predimensionamento privo di conferma sperimentale.
Il [settimo incremento](incremento-07.md) integra EI della fila di tubi CHS
e resistenza locale N–M–V in Checker Steel, conservando le azioni concomitanti.
L'[ottavo incremento](incremento-08.md) integra pali circolari in c.a.,
SLU/SLE in Checker Concrete, controlli costruttivi separati ed EI equivalente.
Il [nono incremento](incremento-09.md) integra carichi distribuiti lineari
per intervalli e fasi, trasferimento globale, diagramma q netto e report.
Il [decimo incremento](incremento-10.md) aggiorna le due guide globali Word/PDF,
la Wiki e l'aiuto contestuale degli editor, con collaudo applicativo e WPF.
L'[undicesimo incremento](incremento-11.md) aggiunge ricerca esaustiva
dell'infissione, ricalcolo indipendente dei candidati, verifiche governanti,
distinzione fra minimo e accettabilità, editor, persistenza e guide Rev49.
Il [dodicesimo incremento](incremento-12.md) aggiunge campi recuperati,
inviluppi con azioni concomitanti, otto grandezze, CSV/report e guide Rev50.
Il [tredicesimo incremento](incremento-13.md) integra storie assegnate
indipendenti nello stesso foglio, inviluppi comuni, ricerca su tutti i casi,
selettore dei risultati, persistenza e guide Rev51. La prima candidata locale
su `2f60b336` è disponibile per la prova utente con otto esempi: standard
74 PASS sul codice applicativo `6425927e`, successivo collaudo della vista
59 controlli WPF PASS, impronte e provenienza complete nel pacchetto.
La candidata integrata successiva su `0f989159` comprende anche Spettri e
guide Rev52: standard 75 PASS e dieci suite UI PASS sulla medesima revisione
pulita. Nove esempi e 135 file verificati per SHA-256; non sostituisce lo
snapshot ufficiale. [Revisioni, evidenze e prosecuzione](integrazione-rami.md).
Le righe in corso non sono soddisfatte integralmente: tipi complessi, verifiche
locali e globali complete, combinazioni automatiche e funzioni avanzate
richiedono ancora integrazioni. Nessuna funzione avanzata viene esclusa.

La versione testabile 1.11.0 usa ora il set AA ufficiale: [collaudi e deroghe
ambientali](rilascio-f5.md), [installer e impronte](../refactoring/installer-1.11.0.md).
La deroga sul rilascio non approva esclusioni funzionali dalla matrice.
