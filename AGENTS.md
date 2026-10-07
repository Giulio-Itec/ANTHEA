# Architettura e refactoring

- ANTHEA è solo interfaccia. Calcoli, formule e coefficienti normativi stanno nelle librerie GPC (Utilities, Geometry, Model, Checker e GPC.Design). Il codice non grafico specifico di ANTHEA (documenti, migrazioni, progetti, adattatori, report) va nelle librerie senza WPF del repository. Non aggiungere calcoli nuovi a X.Desktop, X.Core o X.Calculations.
- Il refactoring segue `docs/refactoring/piano.md`; le decisioni sono in `docs/refactoring/decisioni.md`. Aggiornare lo stato del piano a ogni passo concluso.
- Il codice esistente si sposta solo con cattura prima, modifica e confronto dopo. Ogni differenza va registrata e classificata nel registro delle differenze di `docs/refactoring`. Il motore legacy si elimina solo a equivalenza provata e con l'accordo dell'utente.
- Gli attesi indipendenti (calcoli Python, calcoli a mano, esempi pubblicati) non si sovrascrivono mai con risultati del codice.

# Struttura del repository

- I nuovi progetti di produzione vanno in `src/`, i nuovi test in `tests/`, script e strumenti in `tools/`, runner e configurazione di build in `build/`, la documentazione di sviluppo in `docs/`.
- I progetti esistenti (X.*, `supporto/test`, `supporto/scripts`) restano dove sono fino alla finestra di spostamento concordata tra le fasi F2 e F3.
- Salvare schermate, immagini di verifica, report generati, log e altri output di prova in `supporto/artefatti/`, con una sottocartella per attività. La cartella non è versionata.
- Non creare cartelle `verifiche_*`, `tmp` o file di confronto nella radice né in `supporto/`. `supporto/tmp` non è versionata.
- Le risorse grafiche usate dall'applicazione restano nei progetti che le usano.

# Git

- Il refactoring si fa su un branch per fase (`refactoring/<fase>-<nome>`), con merge su main a fine fase dopo l'approvazione dell'utente. Push solo su richiesta esplicita, in ANTHEA e nelle librerie.
- Commit piccoli e revisionabili. Se nell'albero c'è lavoro di altri, committare solo le proprie modifiche.
- Nessun collegamento (junction o symlink) dentro il repository. Un collegamento si toglie con `cmd /c rmdir <percorso>`, mai con una cancellazione ricorsiva, che agirebbe sulla destinazione.
- Per tornare indietro usare `git revert`, non il checkout di commit vecchi: il commit 2cdf9fd contiene ancora `node_modules`.

# Verifiche

- Il runner unico è `build/ci.ps1`: profilo `quick` (build e suite rapide), `standard` (tutto tranne le prove WPF, come `supporto/Verifica.cmd`), `full` (anche le prove WPF, che occupano il desktop per quasi un'ora).
- Sono ammessi solo i fallimenti elencati in `build/known-failures.json`, ognuno con motivo, fonte e fase che lo chiude. Un fallimento nuovo si aggiunge solo dopo averlo spiegato all'utente.
- Salvare tutti i nuovi sorgenti di test in `tests/`; i progetti esistenti in `supporto/test/` restano fino alla finestra di spostamento.

# Librerie GPC

- `lib/Checker` si aggiorna solo con un insieme coerente di DLL della stessa build, compilato da commit pushati, con manifest (versioni, commit e SHA-256) e voce nel README. Ogni DLL con sorgente cambiato ha una versione nuova. Dopo l'aggiornamento: profilo `full` del runner e test delle librerie.
- Le mesh DelaunayMesh delle sezioni usuali devono restare identiche bit per bit.

# Guide, esempi e revisioni

- Le guide contengono solo contenuti nostri: niente testi, figure o rimandi di terzi (articoli, blog, siti, programmi concorrenti). Sono ammesse le citazioni normative e la bibliografia tecnica dei metodi implementati.
- Mantenere soltanto due guide globali: `supporto/docs/guida-pratica-anthea.md` (uso, UI e procedure di tutti i moduli) e `supporto/docs/guida-teorica-anthea.md` (teoria, formule, ipotesi e limiti di tutti i moduli), con edizioni Word e PDF in `supporto/documentazione/Guide_ANTHEA/`. Questa regola cambierà con la ristrutturazione della Wiki.
- Integrare ogni nuovo argomento nelle due guide globali, secondo la sua natura, aggiornando anche i PDF e gli indici. Non creare guide autonome per singoli moduli. Per contenuti particolari che richiedano una diversa organizzazione, chiedere all'utente prima di derogare.
- Ogni guida, esempio documentato o rapporto destinato agli utenti di ANTHEA deve avere anche una versione PDF, oltre al sorgente modificabile. Salvare il PDF accanto al documento con lo stesso nome di base e controllarne la resa grafica. I documenti di sviluppo in `docs/` sono solo Markdown.
- Quando si aggiorna un documento, aggiornare anche il PDF corrispondente.
- Spostare guide e documenti duplicati o superati in `supporto/SUPERATI/`, conservando la struttura relativa originale e annotando origine, motivo e revisione sostitutiva. Non eliminarli definitivamente.
- Lasciare nelle cartelle correnti soltanto le revisioni in uso e aggiornare indici e collegamenti. Non spostare automaticamente modelli o evidenze di calcolo ancora usati da un'attività in corso.
