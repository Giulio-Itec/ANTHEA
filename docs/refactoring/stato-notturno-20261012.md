# Stato del lavoro notturno — checkpoint 12 ottobre, 23:27

## Mandato

Nove ore dal 12/10 alle 22:19 Europe/Rome: termine 13/10 alle 07:19 (05:19 UTC). Ordine: blocco F2 punti 1–5 e 5.2, STG, SEZ, F3, F4 se possibile. F5 esclusa. Un commit per punto chiuso, test/versioni/merge/push autorizzati. Nessun agente parallelo richiesto. Non eliminare legacy attivo senza equivalenza e autorizzazione specifica.

## Blocco F2

Punti 1–4 e 5.2 committati (66a9d351, cf33c16e, fbedb8ca, c53fd9b, 71aaa60b); punto 5 collaudato e pronto al commit. Set T installato e librerie pubblicate: Model 5.1.0.0, Concrete 0.0.27.2. ANTHEA 1.3.0; guide Rev37 completate. Full: 102 PASS; unico confronto fixture inizialmente senza le 13 classificazioni F2-22, ora risolto con 3 PASS nel runner 20261012-232515-f2-set-t-fixture-final. Nessun fallimento residuo, nessuna eccezione aggiunta. Relazione di sviluppo: chiusura-blocco-f2-20261012.md.

La build dell’installer è separata nelle cartelle ignorate supporto/artefatti/installer e rilascio-1.3.0. Main primario ancora 9b42f093; verificato pulito e allineato a origin/main prima del merge. Il worktree attivo è model-viewer-draft/ANTHEA, branch refactoring/f2-chiusure-pendenti.

## Stage in Model

Ramo codex/stg-stages dal master 94efc9bc. STG-1 studio in 6ede622c; dominio in cffd4879; lettore MIDAS in b89ce317. Model 5.2.0.0, Converter 2.1.0.0, ModelData 0.0.2.10, Persistence 3.0.0.3. Non ancora distribuiti in ANTHEA. Riutilizzati ResultState, mapper di risultati, unità/assi/segni, archivio compatto e filtro esistenti. Fasi e passi distinti; vincoli staged conservati come metadati, non permanenti; casi CS esclusi dalle combo statiche. Full Model: 12 + 1212 PASS, due esclusioni native già note; 54 prove mirate finali sulle versioni. Documentazione e limiti nel Model/docs/construction-stages.md.

MIDAS aperto risponde ma STAG/STCT vuoti: confronto reale a stage ancora da completare. Evidenza worktree supporto/artefatti/refactoring/stages/study-20261012. Non simulare un collaudo reale con fixture sintetiche. Prossimo STG-3: selezione fase/passo nell’importazione e nella vista; contratti degli altri adattatori dichiarati.

## Precauzioni

Non modificare sorgenti ANTHEA durante un runner attivo. Non chiudere la finestra primaria ANTHEA con documento non salvato. Il rifiuto combo ENV_SLE_R è stato diagnosticato (somma di inviluppi oltre limite 4096): nessuna modifica al documento; preflight/scalabilità restano tracciati. Il modello di prova non ha stage. Per le librerie usare sempre un set coerente da checkout puliti e commit pubblicati; per prove preliminari solo candidati espliciti.
