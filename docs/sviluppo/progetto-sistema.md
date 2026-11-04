# Riordino del sistema di progetto

Richiesta dell'utente del 3 novembre 2026: «portiamo avanti il modulo di progetto. fai una analisi critica del sistema di gestione», poi
«Annulla leggero», «come organizzi il sistema di progetto?», «fai queste modifiche. dove metti il progetto della libreria?».

## Analisi, in breve

Il progetto è un unico albero JSON in memoria (`X.Core`, `ProjectDocuments`), copiato, confrontato e ricostruito per intero.

Misura del 3/11 (`supporto/artefatti/analisi-progetto`): 40 fogli e un modello 500 × 100 con 20 combinazioni.

| | Senza modello | Con il modello |
| --- | ---: | ---: |
| Controllo dell'annulla a ogni modifica | 1 ms | 26–78 ms, fino a 542 |
| Memoria per 30 passi | 9 MB | 1,1 GB |
| Annulla / Salva / Apri | 1 / 156 / 53 ms | 149 / 317 / 239 ms |

Altri problemi trovati (dettagli nelle due letture del 3/11):
- Salva e Apri bloccano l'interfaccia, senza avanzamento.
- Nessun blocco del file: due istanze sovrascrivono, e i blocchi mancanti dell'archivio vengono ignorati.
- Nessun recupero dopo un crash.
- L'apertura è tutto-o-niente e i messaggi non dicono dove sta il problema.
- Albero e coerenza vengono ricostruiti a ogni cambio.
- Manca uno stato del progetto: verificato, non verificato, da ricalcolare.
- Dal modello alle verifiche passa solo un riferimento.

## Decisioni dell'utente (3/11)

- **D1 · Dati comuni ereditati dalla sezione (passo 6).** Dopo i passi 1–5: cambia il modo di lavorare e il formato del file.
- **D2 · Copia di recupero.** Sì: copia automatica in `%LOCALAPPDATA%`, proposta all'avvio dopo un crash.
- **D3 · File del progetto.** Per ora file di progetto più archivio dei risultati accanto, come deciso il 2/11 («per ora così. poi vedremo
  un sistema di salvataggio diverso»).

## Dove sta il codice

Nuova libreria **`src/ANTHEA.Projects`**: .NET 8, senza WPF (AGENTS.md). Dipende da `ANTHEA.Application`, per i moduli e i dati comuni, e
da `ANTHEA.ModelWorkspace`, per i modelli e l'archivio dei risultati. La usa `ANTHEA.Presentation`.

Contiene:
- il file di progetto (`Apri/Salva`, blocco, recupero);
- lo storico dell'annulla;
- poi l'editor con le transazioni e lo stato dei fogli.

Le prove stanno in **`tests/ANTHEA.Projects.Checks`** e sono registrate nella CI. Le regole R1–R7 di `ANTHEA.Architecture.Tests` valgono
anche per la libreria nuova, con baseline vuota.

## Passi

Ogni passo si distribuisce da solo, con le suite della shell e della presentazione verdi.

1. **Annulla leggero** (fatto, 82530caa). Lo storico tiene le copie dei modelli una volta sola, con la loro impronta: 30 passi 1,1 GB →
   16 MB, controllo per modifica 26–78 ms → 1 ms.
2. **File di progetto in libreria.**
   - 2a. `ANTHEA.Projects` con `ProjectFile` (`Open`, `Save`): la sequenza archivio → documento di `WorkspaceSession.Save` e
     `DocumentService` in un punto solo. I blocchi mancanti dell'archivio diventano un avviso e lo storico vi si sposta.
   - 2b. Apertura con diagnosi (fatto): `DocumentService.Open(path, problems)` legge foglio per foglio. Un foglio di c.a. con la sezione
     GPC che non corrisponde più ai parametri, dati di un foglio mancanti nell'archivio del progetto, un foglio non valido o una revisione
     non leggibile non rifiutano più il progetto. Il nodo resta com'era nel file, con il motivo nella proprietà `#non-leggibile`
     (`DocumentQuarantine`, solo in memoria). All'apertura un avviso elenca i problemi con il percorso (Progetto › Sezione › Foglio).
     Il foglio si apre in sola lettura, nell'albero ha «⚠» e il motivo nel suggerimento, e i dati comuni della sezione non lo aggiornano.
     Il salvataggio lo riscrive come era, senza controlli né conversioni, quindi alla riapertura il problema viene ritrovato. Resta la
     regola di `ConcreteSectionArchive`: una sezione danneggiata non viene mai sostituita da una ricostruzione. Il formato del file, la
     versione e la struttura dei progetti devono restare leggibili.
   - 2c. Salvataggio in background (fatto): «Salva» del menu e della pagina «Modelli» congela il progetto (`FrozenDocument`, lo stato
     dello storico) e lo scrive su un thread del pool, con l'avanzamento dei risultati nel titolo («· salvataggio 40%»). L'interfaccia
     resta usabile e le modifiche fatte intanto restano da salvare. Va in background solo un progetto con più di 64 MiB di risultati
     nuovi da scrivere (`WorkspaceSession.BackgroundSaveBytes`): sotto, il salvataggio dura una frazione di secondo e resta sincrono.
     Chiusura, «Annulla» delle domande e un nuovo «Salva» aspettano il salvataggio in corso. Limite: aprire un modello o prepararne una
     copia durante la scrittura dell'archivio attende la fine della scrittura (l'archivio ha un solo lucchetto).
   - 2d. Blocco (fatto): il file nascosto `~$nome` accanto al documento, con utente, computer e ora (`DocumentLock`), come fa Office.
     Resta aperto finché il documento è aperto e Windows lo cancella alla chiusura, anche dopo un crash; un blocco che nessuno tiene aperto
     (mancanza di corrente, copia) viene ripreso. Un file aperto da un altro ANTHEA chiede «Apri una copia» (senza nome, i risultati letti
     dall'archivio del file, che non cambia), «Apri comunque» o «Annulla». «Salva con nome» su un file aperto altrove viene rifiutato.
     Prima di «Salva» si confronta il file su disco con quello aperto o salvato (`FileStamp`): se è cambiato, ANTHEA chiede se
     sovrascriverlo.
   - 2e. Copia di recupero (D2, fatto): ogni minuto (`RecoveryWriter`) un documento cambiato dall'ultima copia viene congelato sul
     thread dell'interfaccia (`FrozenDocument`; per un progetto è lo stato dello storico dell'annulla, circa 1 ms) e scritto in background
     in `%LOCALAPPDATA%\ANTHEA\Recupero\<cartella>`. La cartella contiene il documento, i risultati dei modelli non ancora salvati (un
     archivio per scrittura, solo i blocchi nuovi) e `recupero.json` (file, nome, ora, impronta del file). Una cartella in uso è bloccata;
     dopo un crash Windows rilascia il blocco e all'avvio ANTHEA propone «Riprendi», «Scarta» o «Più tardi». «Riprendi» riapre il
     documento sul suo file, da salvare, con i risultati non salvati; se il file è aperto altrove lo apre come copia senza nome. La copia si
     cancella al salvataggio, alla chiusura e all'uscita.
3. **Editor con transazioni ed eventi.** Ogni operazione sul progetto è un comando che produce una transazione: l'annulla lavora per
   transazioni e l'interfaccia aggiorna solo i nodi cambiati.
   - 3a. Coerenza per contenuto (fatto, 4/11). Misura (`Presentation.Tests --banco-progetto`, 4 progetti con 576 fogli, 6 schede aperte):
     ogni modifica ricostruiva l'albero ricalcolando la coerenza di tutte le sezioni, 640 ms di 660. Una stessa coppia di fogli veniva
     confrontata una volta per ogni livello che la contiene. `ProjectCoherenceCache` tiene i campi di un foglio per l'impronta SHA-256
     del suo JSON, il confronto di due fogli per le due impronte, il copriferro per il foglio e i suoi Materiali e il riepilogo di una
     sezione per le impronte di tutti i fogli che vede. Il risultato è identico, perché sui mancati usa lo stesso codice (prova
     `CoherenceCacheChecks`). Vale anche dopo un annulla, che ricrea gli oggetti.
   - 3b. Transazioni (fatto, 4/11). Ogni operazione di `ProjectSession` è un passo dello storico con il suo nome («Rinomina «Pila P1» in
     «Pila P2»», «Nuovo foglio…», «Elimina…», «Modifica di «foglio»»…). Il suggerimento di «↶ Annulla» e «↷ Ripristina» dice quale
     modifica toglie o rifà. Una scheda confermata prima di un'operazione diventa un passo a sé, quindi «Annulla» toglie una cosa alla
     volta. Una rinomina aggiorna sul posto il nodo dell'albero e i badge delle sezioni sopra, senza ricostruire l'albero. Lo storico
     tiene anche i dati dei fogli una volta sola, condivisi tra i passi (per l'SHA-256 del JSON; le voci dell'archivio delle revisioni
     per il loro nome).

   Misura sullo stesso banco (`Presentation.Tests --banco-progetto`): 4 progetti, 576 fogli con i dati dei loro moduli, 1,5 MB di JSON,
   6 schede aperte, riepilogo aperto; mediane di 21 prove. «Prima» è main prima del passo 3 (7763b6a6).

   | Operazione | Prima | Dopo 3a + 3b |
   | --- | ---: | ---: |
   | Rinomina di un foglio | 709 ms | 50 ms |
   | Nuovo foglio | 781 ms | 79 ms |
   | Annulla | 893 ms | 80 ms |
   | Conferma di un foglio modificato | 68 ms | 39 ms |
   | Albero ricostruito | 655 ms | 9 ms |
   | Memoria dello storico pieno (30 passi) | 240 MB | 21 MB |

   La ricostruzione dell'albero in WPF non è misurata qui. Il resto di una modifica è lo storico (2 confronti da 6 ms e un passo da 13
   ms), la coerenza delle sezioni toccate e il riepilogo.
4. **Stato dei calcoli del progetto** (fatto, 4/11). Decisioni dell'utente del 4/11:
   - D4 «Solo in memoria»: calcolare un foglio non rende il progetto da salvare, non entra nell'annulla e non si scrive nel file. Alla
     riapertura ogni foglio è «da calcolare».
   - D5 «Calcolato per ora»: verificato o non verificato solo dove l'esito uniforme esiste già, cioè la sezione in c.a. (riepiloghi SLU,
     SLV, SLE, taglio, torsione) e la sezione composta (riepiloghi di ogni situazione). Gli altri moduli mostrano «calcolato»; si
     aggiungono uno alla volta, con le regole dell'utente.

   Quando una scheda di progetto pubblica un risultato, `ProjectCalculations` registra l'impronta SHA-256 dei dati calcolati e l'esito
   (`ISheetOutcome`). Lo stato confronta l'impronta con i dati della scheda aperta, oppure con quelli del documento. Nell'albero, dopo il
   primo calcolo, accanto al nome compare ✓ verificato, ✗ non verificato, ● calcolato o ↻ da ricalcolare, con il colore dello stato. Nel
   riepilogo della sezione una riga conta gli stati («Calcoli: 1 verificato · 2 da calcolare»), da quando un foglio della sezione è
   stato calcolato.
5. **Sollecitazioni dai modelli alle verifiche** (rimandato dall'utente il 4/11, «Rimandiamo il passo 5»). Il collegamento diventa sorgente
   di sollecitazioni, con l'impronta del modello, l'anteprima e «Aggiorna» per più fogli. Se si riprende, l'utente preferisce le
   combinazioni scelte nel foglio: ognuna diventa una riga di azioni, con l'anteprima prima di «Aggiorna». Il 4/11 l'utente lo ha unito
   alla fase F5 delle combinazioni: piano in [sollecitazioni-dai-modelli.md](sollecitazioni-dai-modelli.md), da discutere.
6. **Dati comuni ereditati** (D1, dopo i passi 1–5).
