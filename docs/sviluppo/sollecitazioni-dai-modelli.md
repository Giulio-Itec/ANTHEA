# Sollecitazioni dai modelli e combinazioni calcolate quando servono

Richiesta dell'utente del 4 novembre 2026: «entrambi. appena posso affrontiamo il discorso sia per il progetto che per le combo. intanto
butta giù un piano. stato attuale, modifiche consigliate, possibili sviluppi e problemi».

Il piano unisce due punti rimasti aperti:
- il passo 5 di [progetto-sistema.md](progetto-sistema.md), le sollecitazioni dai modelli alle verifiche;
- la fase F5 di [modelli-motore-combinazioni.md](modelli-motore-combinazioni.md), le combinazioni calcolate quando servono, con le
  decisioni D4 e D5 di quel piano.

Sono legati: se il modello non salva più i risultati delle combinazioni, ogni foglio deve tenere le combinazioni che ha preso. È una bozza
da discutere con l'utente, e nessun codice cambia prima delle decisioni (sezione 5).

## 1. Stato attuale

### 1.1 Dal modello ai fogli

- **Un solo riferimento per foglio.** Sul nodo del foglio, accanto a `dati`, c'è `riferimento_modello`: `{modello_id, revisione,
  elemento, caso, componente}` (`ProjectModelStore.Link`). Si crea dalla scheda del modello, pannello «Verifiche», con «Collega
  riferimento».
- **Solo piastre.** Il collegamento accetta le 8 componenti delle piastre in assi locali. Le travi (Fx…Mz), che sono quelle che servono
  alla sezione in c.a., non si collegano.
- **Nessun modulo legge il riferimento.** Il foglio mostra «Riferimento aggiornato» o «da aggiornare» e «Apri modello», che apre la scheda
  senza andare all'elemento. Le sollecitazioni si scrivono a mano, si incollano o si importano da Excel. Non esiste importazione da MIDAS,
  Straus7 o SAP verso un foglio.
- **La revisione è troppo grossa.** È l'impronta dell'intera copia del modello. Calcolare una qualsiasi combinazione rende «da
  aggiornare» tutti i riferimenti a quel modello, anche se il valore collegato non cambia.
- **Nessun controllo di esistenza.** Nessuno controlla che caso ed elemento esistano ancora. `Attach` può togliere i risultati più vecchi
  per stare nel limite di spazio.
- **Le righe non dicono da dove vengono.** Le righe di azioni dei fogli non registrano modello, combinazione, elemento o stazione.
- **Lo stato dei calcoli ignora il modello.** Il passo 4 confronta solo i dati del foglio, quindi un modello cambiato non rende il foglio
  «↻ da ricalcolare».
- **I collegamenti non si vedono.** Albero, riepilogo e report del progetto non li mostrano. Il report del modello stampa «SEZIONE
  PREDISPOSTA - RISULTATI NON COLLEGATI». Dopo «Collega riferimento» la lista dei fogli della scheda del modello resta con lo stato vecchio.

Come i moduli ricevono oggi le sollecitazioni:

| Modulo | Forma | Adatto a prenderle dal modello |
| --- | --- | --- |
| Sezione in c.a. (`str_palo`) | Righe per stato limite (SLU, SLV, SLE rara, frequente, quasi permanente): `{nome, azioni [N, Mx, My, Vx, Vy, T]}`, N negativo in compressione, kN e kNm | Sì: è il primo candidato |
| Sezione composta | Fasi costruttive con incrementi (ΔN, ΔMx, V, T, …), non combinazioni | Solo con una regola diversa (sviluppi) |
| Muri | Azioni caratteristiche; le combinazioni le fa il modulo | No |
| Fondazioni superficiali | Un solo insieme di azioni (N, Hx, Hy, Mx, My) | Dalle reazioni vincolari (sviluppi) |
| Pali verticali e orizzontali | Azioni singole | Dalle reazioni vincolari (sviluppi) |

Gli unici passaggi automatici oggi vanno da foglio a foglio: un muro e il palo elastico creano una sezione in c.a. come copia
indipendente, senza sincronizzazione.

### 1.2 Combinazioni

- **Calcolo.** `GpcModelCombinationService` usa la matrice di GPCModel (`CombinationMatrix`) fino a 4096 alternative. Sopra quella
  soglia ricostruisce tutto il modello (`CombinationRebuild`); gli inviluppi sulla matrice anche oltre la soglia sono la fase F2, ancora
  aperta.
- **Primo calcolo, a freddo.** 7 s sul 500 × 100 e circa 23 s sul modello da 3456 piastre e 49 casi. Il tempo va nell'importazione
  della geometria in GPC, nel giro in testo di ogni caso base e nella preparazione della matrice.
- **Calcoli successivi.** Una combinazione lineare richiede circa 40 ms, un inviluppo SLU 0,7 s sul 500 × 100.
- **Salvataggio.** Ogni risultato calcolato entra nella copia del modello (`Attach`). Sopra i 16 MiB diventa un blocco compresso
  dell'archivio `.anthea-risultati`, accanto al file del progetto.
- **Limiti di spazio.**
  - La copia ha un limite di 2, 4, 8 o 16 GiB, scelto nel pannello «Spazio delle combinazioni»; ogni blocco ha un limite di 256 MiB.
  - Oltre il limite, `Attach` toglie i risultati più vecchi.
  - «Libera» li toglie tutti; è un passo dell'annulla.
  - Lo storico tiene 5 copie con i loro blocchi.
- **Dimensioni** (3456 piastre, 49 casi):
  - casi base 165 MiB;
  - una combinazione lineare 6,5 MiB;
  - un inviluppo 97 MiB, perché ogni punto porta lo stato governante con le concomitanti;
  - 73 combinazioni, circa 1,6 GiB.
- **«Calcola tutte»** sullo stesso modello, con il limite a 4 GiB: 581 combinazioni di 875 in 254 s, con un picco di memoria di 18 GB.
- **Chi legge i risultati salvati.** Tutti in modo sincrono, da `Snapshot.Results`:
  - mappe a colori, deformate e diagrammi;
  - tabella dei governanti ed esportazione CSV;
  - confronto affiancato e differenze tra revisioni;
  - report del modello;
  - «Collega riferimento»;
  - lo stato «Calcolata» del catalogo e «Visualizza risultati»;
  - Salva, Apri e copia di recupero. Apri decomprime e calcola l'impronta di ogni blocco.
- **Impronta.** L'impronta dell'intera copia, risultati delle combinazioni compresi, è la chiave dei riferimenti dei fogli, della
  revisione del report, delle viste salvate e delle revisioni.
- **Determinismo.** Matrice e ricostruzione danno risultati identici byte per byte (D1, `CombinationMatrixChecks`). La revisione di una
  definizione (`InputRevision`) non contiene la versione delle librerie, quindi un aggiornamento di GPC potrebbe cambiare i valori
  ricalcolati senza che nessuno se ne accorga.

## 2. Modifiche consigliate

L'idea in una riga: **il modello salva casi base e definizioni, le combinazioni si calcolano quando servono e il foglio salva solo quello
che ha preso, con la provenienza.** È la preferenza dell'utente del 4/11: le combinazioni scelte nel foglio, ognuna una riga di azioni, con
l'anteprima prima di «Aggiorna».

### A. Impronte fini

Prerequisito, senza cambi visibili a parte meno falsi «da aggiornare».
- **Impronta della base del modello.** Copre geometria, casi base e versione del motore di GPCModel, separata da quella della copia.
- **Impronta di una combinazione.** È la revisione della definizione, che esiste già, più l'impronta della base.
- **Versione delle librerie.** Entra nella revisione di una definizione, così un aggiornamento di GPC rende «da aggiornare» le righe che
  dipendono da quel calcolo.
- **Riferimento esistente.** Punta a modello, definizione e revisione della definizione. Calcolare un'altra combinazione non lo cambia
  più.

### B. Sollecitazioni dal modello nella sezione in c.a. (passo 5)

- **Nel foglio, un blocco «Dal modello».** Contiene:
  - il modello;
  - gli elementi trave con le stazioni;
  - le combinazioni scelte, una o più per stato limite;
  - la conversione di segni, assi e unità, in un punto solo e con prove dedicate.
- **Righe con la provenienza.** Ogni combinazione scelta diventa una riga di azioni marcata con `{modello, definizione, revisione,
  elemento, stazione, alternativa}`. Le righe manuali restano accanto e non cambiano.
- **Inviluppi.** Danno una riga per ogni stato governante, con le sollecitazioni concomitanti di quell'alternativa. Il motore le ha già:
  ogni governante porta il vettore locale intero di una sola alternativa, mai composto da estremi indipendenti. Le righe uguali si
  uniscono.
- **«Aggiorna».** Confronta le impronte e mostra l'anteprima delle righe che cambiano, nuove o tolte, con i valori prima e dopo. Le
  applica solo dopo la conferma, come un passo dell'annulla con il suo nome («Aggiorna dal modello «…»»).
- **Stato del foglio (passo 4, sempre in memoria).** Se il modello cambia, le righe prese diventano «da aggiornare» e il foglio diventa
  «↻ da ricalcolare».
- **Riferimento a una piastra.** Il riferimento di oggi resta leggibile. Con la sezione per metro (sviluppi) diventa una sorgente come le
  altre.

### C. Combinazioni quando servono (F5)

- **C1 · Un fornitore di risultati.** Al posto della lettura diretta da `Snapshot.Results`:
  - i casi base vengono dalla copia, le combinazioni dalla matrice;
  - una cache in memoria con un limite, dalla quale escono per prime quelle usate meno di recente;
  - la preparazione a freddo parte in background all'apertura del modello ed è condivisa da visualizzatore, report e confronto: oggi
    ogni modello di vista la ripete.

  In C1 i risultati salvati restano, quindi il comportamento non cambia.
- **C2 · Smettere di salvare.**
  - Il file tiene i casi base, le definizioni e gli inviluppi importati da MIDAS, perché non si possono ricalcolare.
  - «Calcolata» diventa «In memoria».
  - Il pannello «Spazio delle combinazioni» diventa il limite della cache.
  - «Libera» svuota la cache e non entra più nell'annulla.
  - «Calcola tutte» cambia o sparisce (decisione S5).
- **Documenti esistenti.** I risultati di combinazione già salvati si leggono ancora. Al primo salvataggio si tolgono, con un avviso che
  dice quanto spazio si libera (decisione S6).
- **Report del modello.** Si genera dai risultati calcolati e registra l'impronta delle definizioni che usa.

### D. Visibilità

- **Albero e riepilogo.** Nell'albero, un segno sui fogli con righe prese da un modello. Nel riepilogo della sezione, quanti fogli prendono
  da ogni modello e quanti sono da aggiornare.
- **Scheda del modello.** L'elenco dei fogli che lo usano, con «Aggiorna» su più fogli insieme e l'anteprima di tutti.
- **Report del progetto.** Per ogni foglio, la provenienza delle sollecitazioni.

### Ordine e dimensioni (stima)

| Passo | Contenuto | Dimensione |
| --- | --- | --- |
| A | Impronte fini, versione delle librerie | 1–2 giorni |
| C1 | Fornitore dei risultati e cache condivisa; i risultati salvati restano | 3–5 giorni |
| B | Sezione in c.a. dal modello, con anteprima e «Aggiorna» | 5–8 giorni |
| C2 | Smettere di salvare le combinazioni, conversione dei documenti | 3–4 giorni |
| D | Albero, riepilogo, scheda del modello, report | 1–2 giorni |

Ogni passo si distribuisce da solo, con la CI verde. A e C1 non cambiano niente per chi usa ANTHEA; B è il primo passo che si vede.

## 3. Possibili sviluppi

- **Fondazioni superficiali e pali.** Possono prendere le azioni dalle reazioni vincolari dei nodi: le combinazioni le calcolano già
  (famiglia `REACTION`).
- **Sezione per metro dalle piastre.** Una sezione in c.a. larga 1 m prende momenti e forze per unità di larghezza da un elemento piastra.
  Più avanti, la risultante lungo un taglio, per setti e muri del modello.
- **Sezione composta.** Le fasi prendono gli incrementi dai casi base del modello, uno per fase. Serve una regola diversa dalle
  combinazioni.
- **Governanti proposti.** Per l'elemento scelto, ANTHEA propone le combinazioni che governano (N massimo e minimo, |M| massimo, V
  massimo) invece di farle scegliere tutte a mano.
- **Stato tracciato su richiesta** (D6 del piano delle combinazioni). Per una riga presa, mostra quali casi base e coefficienti l'hanno
  data.
- **F2.** Inviluppi sulla matrice anche oltre 4096 alternative, così anche quelli grandi si calcolano in fretta quando servono.
- **Preparazione a freddo più veloce.** I casi base vanno alla matrice come numeri, senza il giro in testo. È l'ostacolo principale di C:
  se la preparazione scende a 1–2 s, aprire un modello e vedere una combinazione è immediato.
- **«Esegui verifiche» nel modello**, oggi disabilitato. Si assegnano le sezioni ai gruppi di elementi e si verificano tutti gli elementi
  su tutte le combinazioni. È l'obiettivo lontano di [modelli-visualizzatore.md](modelli-visualizzatore.md); B e C ne sono la base.

## 4. Problemi e rischi

1. **Convenzioni.** È il rischio più serio per la correttezza: un errore qui dà una verifica sbagliata senza segnali. Le convenzioni da
   conciliare:
   - il My della trave ha il segno opposto all'M1 di GPC;
   - le piastre sono in kN·m/m;
   - la sezione in c.a. vuole N negativo in compressione;
   - gli assi locali dell'elemento non sono necessariamente quelli della sezione.

   Contromisure: una sola conversione, prove con travi semplici risolte a mano e la convenzione scritta nell'anteprima.
2. **Preparazione a freddo a ogni apertura.** Oggi 7–23 s si pagano solo al primo calcolo; con C2 li richiede anche solo vedere una
   combinazione. Contromisure: preparazione in background all'apertura, condivisa, e poi più veloce (sviluppi).
3. **Inviluppi.** 0,3–2 s ciascuno; oltre 4096 alternative si ricostruisce tutto il modello, e quei tempi non sono misurati. Servono
   cache e F2.
4. **Memoria.** Un inviluppo sul modello grande pesa 97 MiB, quindi la cache deve avere un limite.
5. **Lettori sincroni.** Mappe, diagrammi, tabelle, confronto, revisioni e report leggono i risultati subito. Il fornitore asincrono
   tocca tutto il visualizzatore: è la parte più larga di C1.
6. **Versione delle librerie.** Dopo un aggiornamento di GPC un ricalcolo può dare valori diversi. L'impronta con la versione rende
   «da aggiornare» le righe interessate e l'anteprima mostra la differenza; resta la regola D1, identico byte per byte, nella CI.
7. **Documenti esistenti.** L'impronta cambia una volta per tutti i modelli. Senza il passo A ogni riferimento diventerebbe «da
   aggiornare» alla prima apertura.
8. **Quante righe.** Una trave con molte stazioni, per molte combinazioni e governanti, può dare centinaia di righe. Serve una regola
   (estremi e punti governanti, righe uguali unite) e un limite con un avviso.
9. **Risultati che non si possono ricalcolare.** Gli inviluppi importati da MIDAS e le copie senza casi base completi restano salvati;
   per loro «Aggiorna» vale solo dopo una nuova importazione.
10. **Annulla e recupero.** Le righe prese sono dati del foglio, quindi entrano nell'annulla, che li condivide per impronta dal passo 3b.
    La cache dei risultati non va né nell'annulla né nella copia di recupero: dopo un crash si ricalcola. Le copie di recupero e il
    salvataggio diventano molto più piccoli.
11. **Abitudini.** Chi usa «Calcola tutte» per avere tutto pronto dovrà aspettare la prima apertura di ogni combinazione. Da decidere
    con l'utente (S5).

## 5. Decisioni da prendere

| | Domanda | Consiglio |
| --- | --- | --- |
| S1 | Primo modulo e prima sorgente | Sezione in c.a. da elementi trave; la sezione per metro dalle piastre dopo |
| S2 | Da un inviluppo, quali righe | Una per stato governante, con le concomitanti; non il massimo di ogni componente, che mescola alternative diverse |
| S3 | Quali stazioni di una trave | Estremi più i punti governanti, righe uguali unite; tutte le stazioni solo a richiesta |
| S4 | Salvare le combinazioni (D4) | No: nel file restano casi base, definizioni e risultati importati non ricalcolabili; ogni foglio tiene le sue righe |
| S5 | «Calcola tutte» (D5) | Diventa un controllo: calcola tutte le combinazioni, segnala errori e massimi, non salva niente |
| S6 | Risultati già salvati nei documenti | Si leggono ancora; al primo salvataggio si tolgono, con un avviso |
| S7 | Aggiornamento dei fogli | Solo a mano, con l'anteprima; mai automatico |
| S8 | Ordine | A → C1 → B → C2 → D; B prima di C2, così le combinazioni dei fogli funzionano prima di smettere di salvarle |
