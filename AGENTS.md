# Architettura e refactoring

- ANTHEA è solo interfaccia. Calcoli, formule e coefficienti normativi stanno nelle librerie GPC (Utilities, Geometry, Model, Checker e GPC.Design). Il codice non grafico specifico di ANTHEA (documenti, migrazioni, progetti, adattatori, report) va nelle librerie senza WPF del repository. Non aggiungere calcoli nuovi a `src/ANTHEA.Desktop`, `src/ANTHEA.Presentation`, X.Core o X.Calculations.
- Il refactoring segue `docs/refactoring/piano.md`; le decisioni sono in `docs/refactoring/decisioni.md`. Aggiornare lo stato del piano a ogni passo concluso.
- Il codice esistente si sposta solo con cattura prima, modifica e confronto dopo. Ogni differenza va registrata e classificata nel registro delle differenze di `docs/refactoring`. Il motore legacy non si elimina: si sposta in `tests/ANTHEA.Legacy`, dove resta come riferimento delle prove di equivalenza.
- Gli attesi indipendenti (calcoli Python, calcoli a mano, esempi pubblicati) non si sovrascrivono mai con risultati del codice.
- Interfaccia MVVM (F5, decisioni del 16/10 in `docs/refactoring/decisioni.md`, «punto di controllo F5.0»): i ViewModel stanno in `src/ANTHEA.Presentation`, senza WPF né tipi di calcolo; le viste in `src/ANTHEA.Desktop`, in XAML con un DataTemplate per tipo di ViewModel (i disegni tecnici restano in codice). CommunityToolkit.Mvvm con uno strato nostro, mai `Messenger.Default` né `Ioc.Default`; radice di composizione scritta a mano, senza contenitore. Le regole sono verificate da `tests/ANTHEA.Architecture.Tests` (P1–P7), le cui baseline possono solo scendere.

# Struttura del repository

- Il codice di produzione sta in `src/`: l'interfaccia WPF in `src/ANTHEA.Desktop` (eseguibile `ANTHEA.exe`, namespace `X.Desktop`), i ViewModel in `src/ANTHEA.Presentation`, gli altri progetti `ANTHEA.*`. Fuori da `src/` restano soltanto i progetti storici `X.Core` e `X.Calculations`. Le figure delle guide stanno in `src/ANTHEA.Desktop/Assets/Wiki`. I nuovi progetti di produzione vanno in `src/`, i nuovi test in `tests/`, script e strumenti in `tools/`, runner e configurazione di build in `build/`, la documentazione di sviluppo in `docs/`.
- Le prove WPF fuori schermo stanno in `tests/ANTHEA.Desktop.UiTests` e si compilano soltanto nella configurazione `UiTests` di `src/ANTHEA.Desktop` (`dotnet build src/ANTHEA.Desktop/ANTHEA.Desktop.csproj -c UiTests`); la Release non contiene codice di prova (`qa/no-test-code`). I progetti ancora attivi di `supporto/test` restano dove sono finché non passano nei test delle librerie o in `tests/` (`docs/refactoring/progetti-di-test.md`); non se ne aggiungono di nuovi.
- Salvare schermate, immagini di verifica, report generati, log e altri output di prova in `supporto/artefatti/`, con una sottocartella per attività. La cartella non è versionata. Le corse di `build/ci.ps1` in `supporto/artefatti/ci` oltre le ultime 10 vengono snellite in automatico (`build/Prune-CiRuns.ps1`: restano riepiloghi, log e file piccoli); per tenere intera una corsa usare `-Keep` o aggiungere `CONSERVA.txt`.
- Non creare cartelle `verifiche_*`, `tmp` o file di confronto nella radice né in `supporto/`. `supporto/tmp` non è versionata.
- Le risorse grafiche usate dall'applicazione restano nei progetti che le usano.

# Git

- Il refactoring si fa su un branch per fase (`refactoring/<fase>-<nome>`), con merge su main a fine fase dopo l'approvazione dell'utente. Push solo su richiesta esplicita, in ANTHEA e nelle librerie.
- Commit piccoli e revisionabili. Se nell'albero c'è lavoro di altri, committare solo le proprie modifiche.
- Nessun collegamento (junction o symlink) dentro il repository. Un collegamento si toglie con `cmd /c rmdir <percorso>`, mai con una cancellazione ricorsiva, che agirebbe sulla destinazione.
- Per tornare indietro usare `git revert`, non il checkout di commit vecchi: il commit 2cdf9fd contiene ancora `node_modules`.

# Verifiche

- Il runner unico è `build/ci.ps1`: profilo `quick` (build e suite rapide), `standard` (tutto tranne le prove WPF, come `supporto/Verifica.cmd`), `full` (anche le prove WPF fuori schermo, circa un'ora). Le prove WPF si eseguono con `-Stage build,ui` (con `-Only` per sceglierle); `-List` elenca e conta le suite senza eseguirle. Nessuna suite dello stadio `ui` si toglie senza la sostituta: il numero delle suite si riporta prima e dopo.
- Sono ammessi solo i fallimenti elencati in `build/known-failures.json`, ognuno con motivo, fonte e fase che lo chiude. Un fallimento nuovo si aggiunge solo dopo averlo spiegato all'utente.
- Salvare tutti i nuovi sorgenti di test in `tests/`.

# Librerie GPC

- `lib/Checker` si aggiorna solo con un insieme coerente di DLL della stessa build, compilato da commit pushati, con manifest (versioni, commit e SHA-256) e voce nel README. Ogni DLL con sorgente cambiato ha una versione nuova. Dopo l'aggiornamento: profilo `full` del runner e test delle librerie.
- Il set coerente lo produce `Checker/build/Release-Set.ps1` con tutte le suite di Model e Checker (treno di rilascio in `Checker/build/RILASCIO.txt`) e si installa con `tools/libs/Update-Snapshot.ps1 -ReleaseSet <cartella>`. L'utente delega agli agenti le versioni: per i fix incrementare l'ultimo componente; negli altri casi incrementare il secondo da sinistra e azzerare i successivi (0.0.27.1 -> 0.0.27.2 per un fix; -> 0.1.0.0 negli altri casi). Motivare il numero nel commit, senza chiedere conferma; restano i controlli del set coerente e le autorizzazioni Git.
- Le mesh DelaunayMesh delle sezioni usuali devono restare identiche bit per bit.
- Gmsh (GMsh.Net, UnsafeEx, le DLL native gmsh-*.dll e il wrapper GMesh) va solo negli unit test: mai in `lib/Checker`, nell'installer o nelle DLL delle librerie.

# Guide, esempi e revisioni

- Le guide contengono solo contenuti nostri: niente testi, figure o rimandi di terzi (articoli, blog, siti, programmi concorrenti). Sono ammesse le citazioni normative e la bibliografia tecnica dei metodi implementati.
- Mantenere soltanto due guide globali: `supporto/docs/guida-pratica-anthea.md` (uso, UI e procedure di tutti i moduli) e `supporto/docs/guida-teorica-anthea.md` (teoria, formule, ipotesi e limiti di tutti i moduli), con edizioni Word e PDF in `supporto/documentazione/Guide_ANTHEA/`. Questa regola cambierà con la ristrutturazione della Wiki.
- Integrare ogni nuovo argomento nelle due guide globali, secondo la sua natura, aggiornando anche i PDF e gli indici. Non creare guide autonome per singoli moduli. Per contenuti particolari che richiedano una diversa organizzazione, chiedere all'utente prima di derogare.
- Ogni guida, esempio documentato o rapporto destinato agli utenti di ANTHEA deve avere anche una versione PDF, oltre al sorgente modificabile. Salvare il PDF accanto al documento con lo stesso nome di base e controllarne la resa grafica. I documenti di sviluppo in `docs/` sono solo Markdown.
- Quando si aggiorna un documento, aggiornare anche il PDF corrispondente.
- Spostare guide e documenti duplicati o superati in `supporto/SUPERATI/`, conservando la struttura relativa originale e annotando origine, motivo e revisione sostitutiva. Non eliminarli definitivamente.
- Lasciare nelle cartelle correnti soltanto le revisioni in uso e aggiornare indici e collegamenti. Non spostare automaticamente modelli o evidenze di calcolo ancora usati da un'attività in corso.
