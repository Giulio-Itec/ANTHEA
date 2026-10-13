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
