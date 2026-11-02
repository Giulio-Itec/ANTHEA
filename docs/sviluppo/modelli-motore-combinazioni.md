# Piano: motore a matrici per le combinazioni

Piano del 2 novembre 2026, richiesto dall'utente («non è possibile migliorare la gestione delle combo? spacchettando delle parti
comuni?», poi «Sì, prepara il piano»). Documento di sviluppo: nessun codice è ancora cambiato. Le decisioni aperte sono in fondo.

## Il problema, misurato

Modello sintetico 500 × 100 (500 elementi, 472 nodi, 100 casi statici, 951 definizioni NTC generate), main e707bc4c, build Release,
`ModelWorkspace.Checks --combination-timing --models 500x100` (`supporto/artefatti/combo-inviluppi/tempi-prima/tempi.md`):

| Calcolo | Totale | Somma GPC | Campi ANTHEA | Attach |
| --- | ---: | ---: | ---: | ---: |
| Preparazione dei casi base (una volta) | 6,9 s | — | — | — |
| Combinazione lineare generata | 0,30 s | 0,23 s | 0,01 s | 0,10 s |
| Inviluppo SLU (tutte le righe SLU) | 1,89 s | 1,61 s | 0,15 s | 0,20 s |
| Inviluppo SLV | 1,02 s | 0,84 s | 0,11 s | 0,29 s |

- **Le somme non sono il costo.** Una combinazione lineare richiede, per ciascuno dei circa 2 500 punti di risultato, qualche
  centinaio di moltiplicazioni e somme: meno dell'1% dei 90 µs che GPC spende per punto.
- **Il costo è l'oggetto completo per ogni stato.** Per ogni punto e per ogni stato governante, `CombinationRebuild` crea un
  `ResultLocation` con lo stato copiato, la trasformazione scritta con tutti i termini (coefficienti formattati), il testo dei
  governanti e la provenienza. ANTHEA poi rilegge quel testo (`Roles`) per sapere chi governa e converte i campioni per la vista.
- **Ogni combinazione è un passaggio completo sul modello**; «Calcola tutte» sulle 951 righe fa 951 passaggi (6–7 minuti con
  l'Attach) e i risultati salvati spingono verso il limite di 256 MiB del pacchetto.
- **Il banco di ottobre** (`docs/sviluppo/modelli-benchmark-casi-base.md`, `tools/ModelViewer.CombinationBench`) tiene i casi base in
  array e valuta le definizioni come un grafo: sulla spalla reale (3 755 nodi, 49 casi, 115 174 valori per caso) l'inviluppo SLU in
  58 ms e tutte le 73 definizioni in 140 ms. Non dava però governanti, concomitanti e componenti principali, e non era in GPC.

## Le parti comuni

| Parte comune | Oggi | Nel motore |
| --- | --- | --- |
| Casi base allineati e ruotati in ogni punto | condivisi (preparazione e `CombinationRebuildCache`) | una matrice casi × componenti per punto, costruita una volta |
| Passaggio sul modello | uno per combinazione | uno per gruppo di combinazioni, punti in parallelo |
| Stati completi con provenienza | creati per tutti i punti e tutti i governanti | creati solo su richiesta, per un punto o un elemento |
| Definizioni annidate | espanse da capo a ogni chiamata | espanse una volta per revisione delle definizioni (memo) |
| Parte permanente comune alle righe | ripetuta | ripetuta: sono somme, il loro costo è trascurabile |

Una precisazione sullo «spacchettare» le combinazioni annidate. Oggi GPC somma prima i **coefficienti** per caso (l'espansione) e
poi moltiplica i casi base, nell'ordine dei casi della chiamata. Ricavare un inviluppo dai vettori già calcolati delle sue righe darebbe
gli stessi valori a meno delle ultime cifre, quindi risultati diversi da quelli di oggi. Il motore a matrici non ne ha bisogno: con le
somme che costano meno dell'1%, rifare l'aritmetica esatta di oggi è veloce. Il riuso dei vettori resta utile solo per le combinazioni
oltre il limite di espansione, che GPC già tratta per termini (`CombinationTreeNode`).

## Il motore

Classe nuova di GPCModel, `CombinationMatrix` (in `Model/Results/Processing`), accanto a `CombinationRebuild`, che resta invariata.

1. **Preparazione**: dalla cache di oggi (campioni statici abbinati, validati e ruotati) una matrice di double per punto, casi ×
   componenti, con il tipo del punto (piastra, stazione d'asta, nodo, reazione) e il suo proprietario. Nessun calcolo nuovo: sono i
   valori che `CombinationRebuildCache.Find` già restituisce.
2. **Valutazione** di una o più combinazioni, ciascuna data come oggi (alternative espanse, o albero oltre il limite):
   - stessa aggregazione dei coefficienti, stesso ordine dei casi nella somma, stessa regola sui pari merito (vince la prima
     alternativa), quindi **gli stessi valori bit per bit** di `CombinationRebuild`;
   - per una combinazione lineare: il vettore di ogni punto;
   - per un inviluppo: per ogni componente il minimo e il massimo, l'indice dell'alternativa che governa e il suo vettore
     concomitante, calcolato solo per le alternative che governano;
   - componenti principali delle piastre come oggi (`Principals`, su ogni stato), con i loro governanti;
   - punti in parallelo; ogni punto è indipendente, quindi il risultato non dipende dal numero di thread.
3. **Uscita compatta**: array di valori, indici e vettori per punto, senza `ResultLocation`. Le etichette delle alternative sono quelle
   dell'espansione, come oggi.
4. **Stato tracciato su richiesta**: per un punto o un elemento (una verifica, un'interrogazione) si chiama `CombinationRebuild` su quel
   solo elemento, che dà lo stato completo con provenienza di oggi.

## Fasi

| Fase | Lavoro | Fatto quando |
| --- | --- | --- |
| F0 · Riferimenti | Sul codice di oggi, per i modelli 24 × 12, 100 × 50 e 500 × 100 e per la copia Straus7 di prova: per ogni combinazione valori, governanti, vettori concomitanti e principali, salvati come riferimento congelato. | Impronte registrate; le impronte già note (`CombinationPipelineValues*`) coincidono. |
| F1 · Motore lineare e inviluppi | `CombinationMatrix` con preparazione, lineari, inviluppi espansi e principali; prove in `UnitTest` contro `CombinationRebuild` su modelli casuali e sui casi di `CombinationRebuildTest` (coefficienti negativi, alternative senza carico, termini ripetuti, nodi con spostamenti e reazioni). | Uguale bit per bit su tutte le prove; misura del tempo per punto. |
| F2 · Albero | Inviluppi oltre il limite come l'overload ad albero di oggi (estremi per termini, principali assenti). | Uguale bit per bit all'albero di oggi. |
| F3 · Librerie | GPCModel 5.38.0.0 (funzione nuova); ricompilazione di Checker per le revisioni; set ufficiale nuovo; tutte le suite di Model e Checker, Concrete net472 compresa; aggiornamento di Arachne e TensileLib se il set va anche a ReBear e BridGHe. | Set installato in ANTHEA; suite di libreria verdi. |
| F4 · ANTHEA | `GpcModelCombinationService` sul motore; `DisplayResult` dagli array invece che dal testo dei governanti; «Calcola tutte» in un passaggio per gruppi. Formato del documento invariato. | Impronte F0 uguali; `--combination-timing` prima/dopo; suite del modulo Modelli e full. |
| F5 · Da decidere | Combinazioni calcolate quando servono, senza salvarne i risultati (vedi D4). | Dopo la decisione. |

Obiettivi di tempo sul 500 × 100, da confermare in F1: combinazione lineare sotto 20 ms (oggi 0,30 s), inviluppo SLU sotto 150 ms
(oggi 1,9 s), «Calcola tutte» sulle 951 righe in meno di un minuto, che a quel punto è quasi tutto Attach e salvataggio.

## Rischi

- **Uguaglianza bit per bit.** Si ottiene solo rifacendo le stesse operazioni nello stesso ordine; il confronto F0 lo verifica su ogni
  valore, non su un campione.
- **Memoria.** Un inviluppo con migliaia di alternative non si materializza per tutto il modello: le alternative si valutano punto per
  punto, una matrice per thread.
- **Due motori da mantenere.** `CombinationRebuild` resta il riferimento e serve per lo stato tracciato; le prove di F1 tengono i due
  allineati.
- **Lavoro in Model in parallelo.** origin/master di Model si muove (5.37.0.2 di oggi): il ramo parte dall'ultimo master e si
  riallinea prima del rilascio.

## Decisioni per l'utente

- **D1 · Valori uguali a oggi bit per bit.** Consigliato sì: nessun riferimento cambia e il confronto è automatico. L'alternativa
  (riusare i vettori delle righe negli inviluppi) cambierebbe le ultime cifre senza un guadagno misurabile.
- **D2 · Dove sta il motore.** Consigliato GPCModel (`Model/Results/Processing`), come prevede l'architettura: ANTHEA riceve i campi e
  li rappresenta.
- **D3 · Versione.** GPCModel 5.38.0.0 (funzione nuova), le altre librerie solo ricompilate; poi una versione di ANTHEA quando F4 è
  verde.
- **D4 · Salvare o calcolare quando serve (F5).** Con il motore una combinazione si calcola in millisecondi: si potrebbe non salvarne
  più i risultati, tenendo solo quelle collegate alle verifiche. Cambia il documento (revisioni, collegamenti delle verifiche, report),
  quindi consiglio di decidere dopo F4, con le misure in mano.
- **D5 · «Calcola tutte».** Resta, ma diventa un solo passaggio per gruppi; con D4 potrebbe sparire.
- **D6 · Stato tracciato.** Consigliato su richiesta (per una verifica o un punto interrogato) invece che per tutto il modello: è lì che
  si perde oggi il 99% del tempo.

## Prossimo passo

Con le risposte su D1, D2, D3 e D6 si parte da F0 (riferimenti) e F1 (motore in GPCModel) su un ramo di Model dall'ultimo master;
D4 e D5 si decidono dopo F4.

## La serie dopo la 1.17.4 (2 novembre 2026, sera)

Richiesta dell'utente («Sì, procedi»: preparazione e Attach una volta per la serie). Con il motore la somma GPC di una lineare costa
circa 5 ms; il resto del tempo di una combinazione della serie era di ANTHEA, misurato con marcatori provvisori su 120 righe generate del
500 × 100 (`supporto/artefatti/combo-serie/serie-prima-marcatori`):

| Passo, per combinazione | Prima | Dopo |
| --- | ---: | ---: |
| Controlli della copia (`ModelSnapshot.Validate`, nel calcolo e nell'Attach) | 2 × 125 ms | 2 × 4–5 ms |
| Lunghezza del pacchetto (limite di 256 MiB) | da 0,24 a 0,74 s, cresce con la serie | 4 ms |
| Revisioni delle definizioni dei risultati derivati (due volte nell'Attach) | 2 × 16 ms, cresce con la serie | 2 × 1 ms |

Nessun calcolo nuovo: ognuno di questi passi controllava, misurava o firmava di nuovo dati che non erano cambiati. Ora lo fa una volta per
riferimento, con la stessa regola di `ModelCombinations.Revisions` (le copie condividono array e record e non li cambiano mai):

- `ModelSnapshot.Validate` salta un risultato già controllato con gli stessi nodi ed elementi;
- `ModelCombinations.SerializedSize` misura una volta ogni risultato derivato e ogni elenco di definizioni;
- `ModelCombinations.Read` legge una volta le tabelle MIDAS di un dizionario, quindi le definizioni restano gli stessi record;
- `DefinitionRevisions` tiene la revisione di una definizione con la preparazione e i record da cui è calcolata.

La preparazione GPC era già una sola per la serie, e la pubblicazione ogni 20 secondi resta com'è.

Prove (`supporto/artefatti/combo-serie`):
- `ModelWorkspace.Checks` PASS 710, con le 5 prove nuove di `CombinationSeriesChecks`:
  - controlli ripetuti con altri nodi ed elementi;
  - stessi record delle tabelle;
  - definizioni e revisioni uguali a quelle rilette dal documento;
  - lunghezza uguale alla serializzazione dopo ogni Attach;
  - «Calcola selezionate» uguale al calcolo una per una.
- Impronte del banco `--serie 120` uguali prima e dopo: valori `342829B4…EDB4`, completa `16AF3A90…D062`, serie `54BB22FC…51BD`.

Tempi sul 500 × 100 (`tempi-dopo`, mediana di 2 corse dopo una di riscaldamento):

| Operazione | Prima | Dopo |
| --- | ---: | ---: |
| «Calcola selezionate» di 120 combinazioni | 98,6 s (821 ms l'una) | 4,8 s (40 ms l'una) |
| Lineare calda, calcolo e Attach | 163 ms | 24 ms |
| Inviluppo SLU, calcolo e Attach | 506 ms | 291 ms |
| Quattro salvataggi di definizioni | 540 ms | 27 ms |
