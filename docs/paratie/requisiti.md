# Paratie: requisiti e verifiche di accettazione

Incarico del 18 ottobre 2026. Questa matrice conserva l'intero ambito richiesto; un
incremento utilizzabile non costituisce la chiusura del modulo. Stato iniziale:
**in sviluppo, non rilasciato**. Nessuna equivalenza numerica con prodotti esterni
è dichiarata senza un confronto riproducibile a parità di ipotesi.

## Basi isolate

| Repository | Revisione iniziale | Ramo di lavoro |
| --- | --- | --- |
| ANTHEA | b9c2d4b1cd54b4a64c8d6fa2c8c0b20207f8dc73 (`refactoring/f5`) | refactoring/f5-paratie |
| Model | bf8563f56a947c61d5f681fbd362059a35de1bf4 | codex/paratie |
| Checker | bce7aaaef9eabbfe359a79f4bb4ed1ee2aaa4b2e | codex/paratie |
| Geometry | 6734eee1004142576b96759e34c15dec48776e23 | sola lettura |
| Utilities | e57b440b16a0a311a7bf0af2f9cf4d2681b83491 | sola lettura |

Cartelle sorelle in `C:/Users/g.pacini/Documents/Repository/ParatieWork`.
I quattro master GPC coincidono con origin/master dopo fetch del 18/10/2026.
Le cartelle originali non vengono usate per compilare o modificare il modulo.

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
| PAR-03 | P | Diaframmi, pali, micropali, T, berlinesi e palancole | sezioni Model, Steel, Concrete | editor sezioni | rigidezze e azioni per interasse | aperto |
| PAR-04 | P,H | Più file, quinconce, rigidezza variabile, muro in testa | Model/Geotechnics | geometria | casi limite e riduzioni documentate | in corso |
| PAR-05 | P,H | Stratigrafia, K0, attiva/passiva, drenato/ND | Soil, SoilProfile, EarthPressure | scambio terreno F5 | pressioni analitiche e falda | in corso |
| PAR-06 | P | Culmann; rottura statica/cinematica | Geotechnics nuovo | scelta metodo | casi indipendenti | aperto |
| PAR-07 | P,H | Trave FEM e molle elastoplastiche | ElasticPile come riferimento, solver nuovo | opzioni/diagnostica | equilibrio, patch, convergenza mesh | in corso |
| PAR-08 | H,U | Fasi, scavi, riporti, memoria plastica, scarico/ricarico | Geotechnics nuovo | sequenza fasi | percorso ciclico e rollback | in corso |
| PAR-09 | P,H | Tiranti attivi/passivi, inclinazione, pretensione e rimozione | Geotechnics nuovo | editor sostegni | installazione sulla deformata, unilateralità | in corso |
| PAR-10 | P,H | Puntoni, vincoli rigidi/elastoplastici, elementi interni | Geotechnics nuovo | vincoli/collegamenti | casi simmetrici e capacità limite | in corso |
| PAR-11 | P | Dimensionamento geotecnico tiranti | BustamanteDoix da estendere con campo verificato | dettaglio tirante | attesi indipendenti per bulbo | aperto |
| PAR-12 | P,H | Carichi terreno/struttura, concentrati/distribuiti/coppie | Geotechnics | editor carichi | integrali e salti di V/M | in corso |
| PAR-13 | P,H | Falda per fase e per lato | SoilProfile, Geotechnics | falde | pressione idrostatica, equilibrio | in corso |
| PAR-14 | H | Filtrazione FEM 2D stazionaria anche non lineare | Geotechnics nuovo | dominio idraulico | Darcy, bilancio, raffinamento | aperto |
| PAR-15 | H,U | Sifonamento e sollevamento idraulico | Geotechnics, norme Model | verifiche idrauliche | gradiente/forze con fonte | aperto |
| PAR-16 | H,U | Instabilità del fondo e tampone | Geotechnics | fondo scavo | benchmark distinto da UPL/HYD | aperto |
| PAR-17 | P,H,U | Stabilità globale per fase e combinazione | SlopeStability, BishopSolver esistenti | profili/superfici | trasferimento azioni, non doppio conteggio | aperto |
| PAR-18 | P,H | Sisma e combinazioni pertinenti | profili normativi Model | combinazioni | coefficienti da fonte primaria | in corso |
| PAR-19 | H | Pushover paratie affacciate | Geotechnics/Design nuovo | curva capacità/domanda | metodo e campo verificati | aperto |
| PAR-20 | H | Interazione fra paratie attraverso il terreno | Geotechnics nuovo | sistema multi-paratia | simmetria e lavoro reciproco | aperto |
| PAR-21 | H | Effetto arco, cedimenti a tergo | Geotechnics nuovo | diagrammi terreno | modello e benchmark specifici | aperto |
| PAR-22 | H | Cerniere plastiche e risposta strutturale non lineare | Concrete/Steel/Design | leggi/sezioni | cicli e bilancio energetico | aperto |
| PAR-23 | H | Ottimizzazione infissione | Design | ricerca con limiti | ogni candidato ricalcolato | aperto |
| PAR-24 | P,H,U | Verifiche e progetto c.a. SLU/SLE/dettagli | Concrete, Design | armature/esiti | azioni concomitanti, entrambi i segni | in corso |
| PAR-25 | H,U | Acciaio, tiranti, puntoni | Steel | sezioni/esiti | capacità e instabilità pertinenti | aperto |
| PAR-26 | P,H,U | Altre norme dichiarate (storiche, EC, ACI, AISC) | profili Model/Checker | profilo esplicito | fonte e prove per ogni norma | aperto |
| PAR-27 | P,H,U | Diagrammi, inviluppi, provenienza e stati risultato | risultati GPC | MVVM/XAML | UI/report corrispondenti | in corso |
| PAR-28 | U | Persistenza, migrazioni, invalidazione, annullamento | Application/Serialization | flussi F5 | round trip, race e riapertura | in corso |
| PAR-29 | P,H | DXF, immagini, stampa e report personalizzabile/inglese | Application/Reports | export | round trip DXF e resa PDF | in corso |
| PAR-30 | U | Guide globali DOCX/PDF, wiki | Reports/Wiki | aiuto contestuale | render e controllo collegamenti | aperto |
| PAR-31 | H | Collegamento FLAC e analisi dinamica esterna | adattatore da specifica pubblica | interoperabilità | licenza e prova con eseguibile reale | dipendenza esterna |
| PAR-32 | H | Monitoraggio Tokbo e confronto misura/calcolo | adattatore da API documentata | serie temporali | credenziali e dati reali autorizzati | dipendenza esterna |
| PAR-33 | H | Assistenza chatbot e servizi esterni | da specificare rispetto all'incarico ingegneristico | supporto | servizio/documentazione necessari | dipendenza esterna |
| PAR-34 | U | Set coerente, test completi e rilascio | runner GPC e ANTHEA | build candidata | manifest, tutte le suite, autorizzazione push | in corso |

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
Le righe in corso non sono soddisfatte integralmente: tipi complessi, verifiche
locali e globali complete, combinazioni automatiche, inviluppi e migrazioni
richiedono ancora integrazioni. Nessuna funzione avanzata viene esclusa.
