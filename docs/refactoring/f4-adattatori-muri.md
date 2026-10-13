# F4 — integrazione degli adattatori dei muri

## Dettagli e distinta: F4.8b.3 / F4.10a.1

La build del branch usa `WallReinforcementDetailing` e `WallBarScheduling` del
set candidato coerente A. L'adattatore converte documenti e testi dei risultati;
non contiene nuove formule. Ancoraggi, copriferri, quantità e sagome vengono
dalle API GPC già confrontate. Il selettore diagnostico esplicito
`WallCalculationEngine.Legacy` conserva il percorso precedente per le prove.
Nessun motore è stato eliminato.

L'input usa la lettura obbligatoria `Required` già presente in Serialization.
Un valore non numerico non diventa zero. Il metodo di copriferro sconosciuto
è rifiutato; resta il rifiuto storico delle vite diverse da 50/100 anni.
Le migrazioni dei documenti vengono prima della proiezione tipizzata.
La costruzione dell'input Concrete di durabilità è condivisa con l'adattatore
esistente; il pertinente Cmin UNI 11104 viene passato esplicitamente.

Runner `20261013-f4-detail-final`: **5 PASS**, incluse build Desktop Release,
architettura (273 controlli) e confronto dell'adattatore. **56 modelli,
30 distinte e CSV, 26127 valori**, zero differenze non ammesse, scarto massimo
5.684341886080802e-14. Tre rifiuti espliciti senza modificare il documento
chiamante. Nessuna eccezione aggiunta alla baseline architetturale: le letture
obbligatorie evitano i nuovi default numerici impliciti.

## Set candidato A e provenienza

Cloni locali puliti in `supporto/artefatti/f4-set-a`, Checker `8f823b54`,
Model `7fd1fe4c`, Geometry `6734eee1`, Utilities `e57b440b`.
`Release-Set.ps1 -Candidate` ha eseguito tutte le suite di Model e Checker;
API compatibili, soli 17 fallimenti Steel già censiti. Una prima preparazione
falliva in compilazione per la dipendenza di test GMsh.Net assente nel clone:
fornito il runtime già usato dal banco locale esclusivamente sotto le cartelle
di test. Gmsh non entra nello snapshot né nel prodotto.

Manifest completo in `coherent-complete/release-set.json`, snapshot ANTHEA
in `snapshot/manifest.json`; **12 DLL applicative**, incluse Design e Masonry.
Lo script Update-Snapshot supporta ora i due nuovi assembly e indica i target
netstandard2.0/net8.0. Il runner registra lo SHA del manifest effettivamente
usato, l'architettura confronta la build Desktop con il medesimo snapshot.
Il riferimento immutabile F3 e le impronte delle DLL restano verificati.

La distribuzione `lib/Checker` è ancora il set V. Questo branch in corso si
compila con `build/ci.ps1 -GpcLibDir <snapshot candidato>`, con i controlli SHA
attivi. Il set candidato non sostituisce il treno da commit pushati richiesto
per F4.9/F4.16; l'attivazione distribuita e il collaudo full restano aperti.

Prossimi: collegamento delle sezioni in c.a. e dei muri a gravità, esiti
condivisi, ricerca delle armature. Le guide utente saranno aggiornate con il
comportamento finale prima della chiusura F4.


## Percorso completo delle sezioni in c.a. (F4.7c.1)

`RetainingWall.Library.Structures.cs` adatta documenti, unità, etichette e risultati. Ogni sezione preparata con il percorso storico passa in proprietà esclusiva alla sessione GPC; nessuna nuova mesh. Aree minime/massime, N–M, taglio, SLE, curvature e massa principale provengono dai servizi condivisi. Il selettore `WallCalculationEngine.Legacy` conserva il percorso precedente; un `ServiceabilityEngine` esplicito mantiene il banco F2 storico.

Il confronto `baseline/f4-structural-adapter` (`-F4StructuralAdapter`) verifica l'intero risultato contro la cattura immutabile: 26 muri, 285743 numeri. Solo due predizioni sono applicate in memoria, senza riscrivere attesi: i 12512 tagli dello studio Python con SHA protetto (F4-03), e 1572 curvature moltiplicate per (1+φ), come dimostrato in F4-08. Una prova separata confronta tre forme SLE e lo spostamento di testa mantenendo separato il contributo della base; provato anche l'annullamento.

Prima corsa: 2341 scostamenti, massimo 0.00010763292549142989, dovuti alla conversione del momento in due moltiplicazioni successive. Usando i fattori nominati risolti da `Units` e una sola moltiplicazione si conserva l'ordine della cattura e del solutore. Seconda corsa: zero differenze, massimo errore numerico 1.8189894035458565e-12. Tolleranze invariate (1e-9 assoluta + 1e-9 relativa); nessun nuovo fallimento ammesso. Evidenza: CI `20261013-f4-structures-b`, 6 PASS, entrambe le suite architetturali comprese.


## Percorso completo dei muri a gravità (F4.8a.4)

La candidata B usa Checker `0b2285bf`, Design 0.4.0.2. Set coerente in `supporto/artefatti/f4-set-a/coherent-gravity-b`, snapshot esterno a lib in `snapshot-b`; tutte le suite Model e Checker eseguite, 1668 Checker PASS e i soli 17 Steel storici, API compatibili. Il set A resta conservato come evidenza.

`RetainingWall.Library.Gravity.cs` mappa materiali, casi, unità e testi verso i servizi PlainConcrete, Masonry e GravityWallAnalysis. Il ramo assegnato non richiede E e ignora i parametri di instabilità inapplicabili, come il percorso storico. Dati mancanti di muratura producono la verifica materiale non disponibile. Resistenze negative non diventano verifiche soddisfatte. Conservato il ramo legacy per le prove.

Runner `20261014-f4-gravity-adapter-a`: 7 PASS, include i 26 muri c.a. ancora equivalenti. Ultima corsa `20261014-f4-gravity-adapter-b`: 6 PASS, sei muri a gravità congelati più due casi di contratto, 82568 numeri e 240 curvature, zero differenze; errore massimo 1.4551915228366852e-11 con tolleranze invariate. `-F4GravityAdapter` abilita il banco sul runner. Produzione sempre set V; il rilascio completo rimane aperto.


## Esiti dei consumatori (F4.4b, 14/10)

I controlli dei muri trasportano Outcome/Execution/Data dalle librerie e offrono tre proprietà di presentazione ad Application; i testi non determinano il giudizio. Nei pali la progressione del tratto e i riepiloghi usano esiti/criticità tipizzati. Le etichette storiche restano compatibili; il selettore legacy del riepilogo è solo diagnostico. Un risultato vecchio senza le criticità strutturate non riceve un esito positivo ricostruito dalle parole: viene richiesto il ricalcolo. Nessun calcolo numerico nuovo.

Differenze intenzionali UI: capacità nulla con domanda positiva è un fallimento, non un dato mancante; un contatto valido senza rapporto numerico non è incompleto. Le proprietà nuove dei muri sono JsonIgnore, senza variazioni degli archivi storici. Il riepilogo dei pali salva anche le criticità tipizzate.

Runner `20261014-f4-outcomes-final`: 10 PASS, zero nuovi fallimenti, comprese Application (1358 controlli), architettura e ElasticPile.UiChecks. Venti pali congelati, 28 tratti, 3698 verifiche e 22188 numeri: zero differenze; provate traduzioni arbitrarie e rifiuto dell'esito positivo di risultati non tipizzati. La baseline architetturale è stata ridotta di 13 occorrenze (11 confronti testuali e due default numerici), senza aggiunte né indebolimenti dei controlli.
