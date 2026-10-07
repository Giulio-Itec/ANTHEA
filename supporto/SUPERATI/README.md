# Documenti e raccolte superati

Questo archivio conserva le revisioni sostituite senza eliminazioni definitive. I percorsi sotto questa cartella riproducono quelli originali relativi a `supporto`. I registri `registro-<data>.json` di questa cartella riportano per ogni file origine, destinazione, motivo e impronta SHA256.

## Guide e rapporti generali

Le guide pratica e teorica Rev01, Rev02, Rev03, Rev04 e Rev05 sono sostituite dalle edizioni ITEC Rev06 in `supporto/documentazione/Guide_ANTHEA`, ora affiancate dai PDF. Le precedenti relazioni di validazione del calcestruzzo e dei ponti sono sostituite dalla relazione software CA e ponti Rev03.

## Stabilità globale

Le raccolte `esempi`, `validazione-20260929` e `validazione-finale`, i relativi PDF di controllo e il vecchio ZIP sono anteriori alla correzione della ricerca dei cerchi tangenti. Sono conservati qui come evidenza storica, con i propri modelli e dati, e non devono essere usati come risultati correnti.

Anche le dieci relazioni Word della serie `confronti-max` erano state generate prima della correzione e sono archiviate qui. I modelli MAX, gli input ANTHEA, i registri originali e i confronti numerici restano nei percorsi di lavoro originali. Le nuove relazioni di quella serie verranno generate alla ripresa dei test.

## Dove trovare i documenti correnti

Consultare `supporto/artefatti/stabilita-globale/DOCUMENTI-CORRENTI.md` oppure il PDF omonimo. Il sommario MAX documenta due confronti completati, un terzo calcolato ma ancora da consolidare e sette confronti non eseguiti. La raccolta interna `regressione-tangenti-max` comprende dieci esempi ANTHEA e non equivale a dieci confronti MAX completati.

I test MAX restano sospesi. L'archiviazione e la produzione dei PDF non hanno eseguito nuovi calcoli.

## Revisione dei muri del 30 settembre 2026

La guida dei muri precedente e le guide generali Rev04 sono sostituite dalla documentazione sulle due colonne, gli attriti e il riepilogo dei valori di calcolo. I report intermedi della stessa attività sono conservati con i loro percorsi originali; le evidenze e i modelli rimangono disponibili. Gli esiti della revisione corrente sono in `supporto/artefatti/muri-due-colonne-20260930/CONTROLLO.md`, anche in PDF. Le prove interne della revisione sono state eseguite; non sono stati ripresi i confronti manuali MAX.


## Percorso guidato della stabilità globale

La revisione immediatamente precedente della guida dei muri e degli indici è conservata in `globale-guidata-20260930`, mantenendo sotto questa cartella i percorsi originali relativi a `supporto`. `globale-guidata-20260930/registro.json` riporta origine, motivo, sostituzione e SHA256. Il nuovo documento operativo è `supporto/docs/stabilita-globale-guida-rapida.md` e PDF; il rapporto corrente dell’interfaccia è `supporto/artefatti/globale-guidata-20260930/CONTROLLO.md` e PDF. Modelli ed evidenze dei confronti MAX restano nelle cartelle originali.


## Allineamento delle guide generali Rev06

Le guide pratica e teorica Rev05, i corrispondenti sorgenti e PDF, la guida dei muri e gli indici sostituiti sono conservati in guide-anthea-rev06-20260930. I percorsi relativi a supporto sono mantenuti; registro.json contiene origine, motivo, SHA256 e revisione sostitutiva. Le guide generali di quella revisione erano ANTHEA_Guida_pratica_ITEC_Rev06 e ANTHEA_Guida_teorica_ITEC_Rev06, ora sostituite dalla Rev07 e conservate nell’archivio indicato sotto. Nessun modello o risultato storico MAX è stato spostato.

## Completamento muri Rev07

Le guide generali Rev06 e i documenti sostituiti sono conservati in muri-completamento-rev07-20260930, con struttura relativa e registro.json. La revisione 07 riguarda portanza sismica, cedimenti, spostamenti e armature. I modelli e le evidenze MAX non sono stati spostati.

## Guide globali Rev08 — 2 ottobre 2026

La revisione corrente delle due guide globali è Rev08. Le guide autonome, i 35 sorgenti integrati, le edizioni Rev07 e gli indici sostituiti sono conservati in `guide-unificate-rev08-20261002`, con la struttura relativa originale. `registro.json` riporta origine, motivo, sostituzione e SHA-256. I documenti correnti sono `supporto/docs/guida-pratica-anthea.md` e `supporto/docs/guida-teorica-anthea.md`, con PDF omonimi ed edizioni Word/PDF in `supporto/documentazione/Guide_ANTHEA`. Modelli ed evidenze di calcolo restano nelle loro cartelle.

Le edizioni successive, fino alla Rev29, sono conservate nelle cartelle `<attività>-rev<NN>-<data>`, dove NN è la revisione che le ha sostituite; ogni cartella ha il proprio `registro.json`.

## Guide globali Rev31 — 7 ottobre 2026

Le edizioni Word e PDF Rev30 delle due guide globali sono conservate in `documentazione/Guide_ANTHEA` di questa cartella, con il percorso originale relativo a `supporto`. `registro-20261007.json` riporta origine, motivo, SHA-256 e revisione sostitutiva. La Rev31 contiene solo contenuti propri: corpus esterno tolto con W0.5, diario di sviluppo, strumenti di IA, concorrenti e paragrafi duplicati tolti con W0.4 (elenco in `docs/refactoring/w0.4-pulizia-guide.md`). La Rev30 della guida teorica contiene ancora il corpus esterno, come le altre copie di questa cartella. I PDF accanto ai sorgenti Markdown erano copie identiche della Rev30 e non sono stati archiviati una seconda volta.

## Progetti di test e script del 25/9 — 7 ottobre 2026 (F1.6)

La cartella `test` conserva otto progetti tolti da `supporto/test` durante il passo F1.6 del refactoring; la cartella `script/validazione_ca_2026_09_25` conserva il generatore della relazione di validazione c.a. del 25/9, tolto da `supporto/script`. Entrambe mantengono il percorso originale relativo a `supporto`. `registro-20261007-test.json` riporta per ogni file origine, destinazione, SHA-256 (del file estratto su Windows, con fine riga CRLF), motivo e sostituzione; la tabella dei progetti è in `docs/refactoring/progetti-di-test.md`. Le destinazioni sono state decise dal coordinatore del refactoring su delega dell'utente e sono da ratificare (`docs/refactoring/decisioni.md`).

- `ConcreteDesign.DesktopChecks` e `ValidationIllustrations` ricompilavano X.Desktop con un proprio App e non compilavano più. Il loro codice è passato nell'exe di prova UiTests: `supporto/test/Desktop/ConcreteDesignDesktopChecks.cs` e `ValidationCaptureChecks.cs`.
- `BridgeDesign.SiteComparison`, `MaxRetainingWall.Cases` e `MaxRetainingWall.Compare` dipendevano da un sito web esterno o dal programma MAX; `ConcreteStressDiagnosis` e `ProgrammaAnthea` (`qa.py`) erano strumenti una tantum; `ValidazioneCA20260925` non compilava. Nessuno era eseguito dal runner `build/ci.ps1` e nessuno ha una revisione sostitutiva diretta; dove esistono, il registro indica le verifiche correnti degli stessi calcoli. L'archiviazione è reversibile con `git mv`, anche per i tre progetti per i quali la tabella rimandava la scelta all'utente (il confronto con il sito e i due progetti MAX).
- `script/validazione_ca_2026_09_25` (`build_document.py`, `check_final.py`, `finalize.py`, `render_pages.py`, `render_word.ps1`) ha prodotto la relazione `ANTHEA_Validazione_Calcestruzzo_Armato_Rev01`, archiviata il 29/9. `build_document.py` importa `reference_base.py` e `reference_extra.py` di `ValidazioneCA20260925` ed è archiviato con quel progetto, con il testo di prima di F1.6.

`ElasticPile.Performance`, spostato qui nello stesso passo, è tornato in `supporto/test`: la tabella gli assegna "Archiviare dopo F5, o spostare in `tools/`".

I file sono invariati. Questi percorsi interni valgono solo nella posizione originale:

- i riferimenti `../../../X.*` dei csproj e `../Desktop/*.cs` di `ConcreteDesign.DesktopChecks` e `ValidationIllustrations`;
- i comandi con `supporto/test/<progetto>` dei README di `BridgeDesign.SiteComparison` e `MaxRetainingWall.Compare` e del testo scritto da `compare.py`;
- la radice del repository, che `qa.py`, `compare.py` di `BridgeDesign.SiteComparison`, `check_campaign.py`, `build_document.py`, `check_final.py` e `finalize.py` calcolano come `parents[3]` del proprio file e `render_word.ps1` come `../../..` della propria cartella;
- `ROOT` di `reference_base.py`, uguale a `parents[2]/'artefatti/validazione_ca_2026_09_25'`, che è `supporto/artefatti/validazione_ca_2026_09_25` solo da `supporto/test`; `reference_extra.py` lo usa (`O = rb.ROOT`);
- la cartella `supporto/test/ValidazioneCA20260925` aggiunta a `sys.path` da `build_document.py` per importare i due riferimenti Python;
- il comando `dotnet run --project supporto/test/ValidazioneCA20260925 -c Release -- percorso_input.json percorso_output.json` e le cartelle citate nel capitolo "Riproducibilità della campagna" della relazione. Il testo è scritto da `build_document.py` ed è ripreso nella Rev02 e nella relazione corrente `ANTHEA_Validazione_Software_CA_e_Ponti_Rev03.docx`, in `supporto/documentazione/Validazione_CA_ANTHEA`, che non è stata modificata.

Per rieseguire un progetto lo si riporta nella posizione originale con `git mv`; per la campagna del 25/9 vanno riportate entrambe le cartelle. Gli ingressi della campagna in `supporto/artefatti/validazione_ca_2026_09_25` non sono versionati e `build_document.py` legge il modello Word dal Desktop dell'autore. Nessuno di questi progetti è in `ANTHEA.sln`.
