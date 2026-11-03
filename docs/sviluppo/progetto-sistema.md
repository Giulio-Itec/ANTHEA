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
   - 2b. Apertura con diagnosi: ogni problema ha il suo percorso (progetto › sezione › foglio). Un foglio o una revisione che non si leggono
     si aprono in sola lettura, con il motivo, invece di rifiutare il progetto.
   - 2c. Salvataggio in background su uno stato congelato (lo scheletro dello storico con le copie dei modelli), con avanzamento.
     L'interfaccia resta usabile; le modifiche fatte durante il salvataggio restano da salvare.
   - 2d. Blocco (fatto): il file nascosto `~$nome` accanto al documento, con utente, computer e ora (`DocumentLock`), come fa Office.
     Resta aperto finché il documento è aperto e Windows lo cancella alla chiusura, anche dopo un crash; un blocco che nessuno tiene aperto
     (mancanza di corrente, copia) viene ripreso. Un file aperto da un altro ANTHEA chiede «Apri una copia» (senza nome, i risultati letti
     dall'archivio del file, che non cambia), «Apri comunque» o «Annulla». «Salva con nome» su un file aperto altrove viene rifiutato.
     Prima di «Salva» si confronta il file su disco con quello aperto o salvato (`FileStamp`): se è cambiato, ANTHEA chiede se
     sovrascriverlo.
   - 2e. Copia di recupero (D2): documento e blocchi non salvati in `%LOCALAPPDATA%\ANTHEA\Recupero`, scritti in background dopo le
     modifiche. All'avvio ANTHEA propone di riprenderla; la copia si cancella al salvataggio o alla chiusura senza modifiche.
3. **Editor con transazioni ed eventi.** Ogni operazione sul progetto è un comando che produce una transazione: l'annulla lavora per
   transazioni e l'interfaccia aggiorna solo i nodi cambiati.
4. **Descrittori dei moduli e stato del progetto.** Ogni modulo dichiara dati comuni, etichette e stato di un foglio. Ogni foglio registra
   l'impronta dei dati con cui è stato calcolato; albero e panoramica mostrano verificato / non verificato / da ricalcolare / da calcolare.
5. **Sollecitazioni dai modelli alle verifiche.** Il collegamento come sorgente di sollecitazioni, con l'impronta del modello, anteprima e
   «Aggiorna» per più fogli.
6. **Dati comuni ereditati** (D1, dopo i passi 1–5).
