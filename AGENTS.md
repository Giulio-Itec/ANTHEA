# Organizzazione del materiale di supporto

- Salvare tutti i nuovi sorgenti di test in `supporto/test/`.
- Salvare schermate, immagini di verifica, report generati, log e altri output di test in `supporto/artefatti/`, usando una sottocartella per ogni attività.
- Conservare documentazione, esempi e script di supporto nelle rispettive sottocartelle di `supporto/`.
- Non creare nuove cartelle `verifiche_*`, `tmp` o file di confronto nella radice del repository.
- Le risorse grafiche utilizzate dall'applicazione restano nei progetti che le utilizzano.

# Guide, esempi e revisioni

- Mantenere soltanto due guide globali: `supporto/docs/guida-pratica-anthea.md` (uso, UI e procedure di tutti i moduli) e `supporto/docs/guida-teorica-anthea.md` (teoria, formule, ipotesi e limiti di tutti i moduli), con edizioni Word e PDF in `supporto/documentazione/Guide_ANTHEA/`.
- Integrare ogni nuovo argomento nelle due guide globali, secondo la sua natura, aggiornando anche i PDF e gli indici. Non creare guide autonome per singoli moduli. Per contenuti particolari che richiedano una diversa organizzazione, chiedere all'utente prima di derogare.

- Ogni guida, esempio documentato o rapporto destinato all'utente deve avere anche una versione PDF, oltre al sorgente modificabile. Salvare il PDF accanto al documento con lo stesso nome di base e controllarne la resa grafica.
- Quando si aggiorna un documento, aggiornare anche il PDF corrispondente.
- Spostare guide e documenti duplicati o superati in `supporto/SUPERATI/`, conservando la struttura relativa originale e annotando origine, motivo e revisione sostitutiva. Non eliminarli definitivamente.
- Lasciare nelle cartelle correnti soltanto le revisioni in uso e aggiornare indici e collegamenti. Non spostare automaticamente modelli o evidenze di calcolo ancora usati da un'attività in corso.
