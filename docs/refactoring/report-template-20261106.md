# Report e template di stili — 6 novembre 2026

Richiesta: partire da `main` su un ramo proprio, allineare le relazioni alle funzioni correnti e permettere la scelta di un template nelle impostazioni. Ramo `refactoring/report-template`, da `af8009fb36fa13f19748c5e46a58fd2513f90089`, nel worktree `ANTHEA-report-template`. L'albero del repository principale conteneva lavoro di un'altra sessione ed è rimasto intatto.

## Comportamento

`Aspetto → Template dei report → Scegli template Word…` accetta `.docx` e `.dotx`. La scelta è per utente Windows, vale subito e si conserva in `%LOCALAPPDATA%/ANTHEA/report-template.json`: copia normalizzata degli stili e nome del documento, senza dipendere dal percorso originale. È disponibile il ripristino dello stile ANTHEA. Una selezione non valida conserva la scelta precedente; un'impostazione corrotta è segnalata e si può ripristinare.

L'importazione legge esclusivamente stili e tema dei caratteri. Risolve l'ereditarietà e associa Normale, Titolo, Sottotitolo, Titolo 1–9, Didascalia e paragrafi delle tabelle. Esclude testo, cartiglio, sommario, campi, intestazioni, piè di pagina, numerazioni, immagini e geometria del riferimento. Tabelle, figure, larghezze, valori, unità e impaginazione tecnica appartengono al report. In assenza di stili dedicati, le tabelle usano la famiglia del modello fino a 10 pt; le intestazioni sono in grassetto.

DOCX e PDF derivano dallo stesso documento dopo l'applicazione degli stili. La scelta raggiunge fogli singoli, report short, progetti e revisioni, stabilità globale del muro e PDF/HTML Modelli. Senza template rimangono i percorsi predefiniti, incluso il PDF storico delle fondazioni superficiali. Il report short conserva il controllo delle due pagine; se lo supera non viene scritto. Il resolver PDF comprende anche i font del profilo Windows, necessari per Manrope, e conserva il ripiego per i glifi mancanti.

## Revisione dei contenuti

| Ambito | Riscontro e intervento |
| --- | --- |
| Palo e micropalo verticali | Gli `avvisi` del risultato non entravano nella relazione. Ora compaiono sempre, anche senza capitoli facoltativi: ipotesi, copertura, coefficienti modificati e limiti. |
| Sezione in c.a. | La duttilità era esclusa dal contratto JSON storico. Il report completo riceve una copia separata dei valori della libreria: primo snervamento, bilineare, momento resistente e massimo, curvatura ultima e μφ, oppure il motivo di indisponibilità. Le curvature nuove hanno sei cifre significative. |
| Progetto della sezione | Richiede la duttilità alla stessa analisi della scheda. Il report del foglio usa il risultato corrente. JSON dei risultati, CSV dei punti e archivi conservano il contratto. |
| Altri generatori | Conservati capitoli e risultati già presenti (materiali, criteri dei domini, colonna modello, geotecnica, azioni, sezioni composte, Bridge Design, reologia e paratie); stili applicati nel punto comune di esportazione. |
| Modelli | Stili del testo e dei titoli nel PDF e nell'HTML; ricetta, figure e dati conservati. Resolver PDF condiviso anche nell'uso autonomo. |

La logica sta in `ANTHEA.Reports` e nelle librerie di applicazione/reportistica; il menu è un ViewModel di Presentation con vista XAML e composizione esplicita. Nessuna DLL GPC, formula, mesh o baseline di architettura modificata.

## Evidenze

- Prima: `supporto/artefatti/ci/20261106-151153-report-template-prima`, 13 suite selezionate PASS. La corsa non attesta un albero fermo: le modifiche sono cominciate dopo le build; binari e report catturati sono quelli precedenti.
- Catture successive: `20261106-154742-report-template-cattura2` e `20261106-155616-report-template-finale-cattura`. F3 confronta 1201 file. Le aggiunte e gli spostamenti dei paragrafi sono registrati esattamente nelle transizioni; nessun atteso indipendente viene rigenerato. Le corse intermedie hanno esposto errori nei nuovi test e differenze attese da classificare, corretti senza aggiungere known failures.
- Riferimento ITEC originale, solo negli artefatti: SHA-256 `f6f3f04b0bafeea09e4cca1b19fa74e95fc540c264aa518f3d93dee9eebbcd92`. Non distribuito né versionato come template dell'applicazione.
- Corpus PDF: 217 report; applicazione ITEC con controllo di identità del testo DOCX. Le 24 varianti short rimangono di due pagine. Prove sintetiche per esclusioni, ereditarietà, idempotenza, persistenza, annullamento, selezione errata e preferenza corrotta.
- Report aperti in Word in sola lettura e PDF dell'applicazione controllati; immagini in `supporto/artefatti/report-template/`. LibreOffice assente: per verificare i DOCX è stata usata l'esportazione PDF di Word installato.
- Guide Rev71: pratica 135 pagine, teorica 212; rispettivamente 1377/2552 frammenti e 9/1566 formule verificati; 26 parti del modello delle guide invariate. Controllate tutte le pagine nelle tavole di insieme e quelle modificate a risoluzione leggibile. Rev70 archiviate con manifest in `supporto/SUPERATI/report-template-rev71-20261106/`.
- Suite UI: 73 prima e dopo, nessuna rimossa.

## Verifica conclusiva

Corsa `supporto/artefatti/ci/20261106-161125-report-template-verifica`, 6 novembre 2026, 16:11–16:34, durata 1374 s. Eseguita sull'albero finale prima dei commit; nessun file tracciato modificato durante la corsa. Dopo la corsa sono stati aggiornati soltanto questo verbale, il piano e il registro con gli esiti.

```powershell
.\build\ci.ps1 -Profile standard -Stage build,fast,regression,wiki,ui -Only '^(?!ui/)(?!.*\.UiChecks$)|^ModelViewer\.UiChecks$|^ui/(f3-equivalence|f3-report-chapters|check-shell-draft|shell-smoke-project-report|check-wiki-offscreen)$' -Tag report-template-verifica -Keep
```

- 100 esiti PASS: 33 build e 67 suite (39 fast, 16 regression, 2 Wiki, 10 UI). Zero KNOWN, NEW-FAIL, BLOCKED, avvisi del runner o file tracciati alterati dai test.
- `Reports.Tests`: 714 controlli, inclusi 238 sugli stili e il corpus ITEC. `Application.Tests`: 3347 controlli; curva e duttilità, tre percorsi, rifiuti e annullamento.
- F3: 1201 file, 119 fixture, 94 articoli e 30 ricerche; zero differenze non ammesse. Transizioni report: 51 aggiunte e 329 spostate a valori invariati; Wiki: 14 aggiunte, 21 spostate, 4 aggiornate e 2 sostituite.
- Passati architettura, attesi indipendenti, tracce c.a., comandi e menu della shell, esportazione del progetto, capitoli dei report e Wiki fuori schermo.
- Verificati anche i tre PDF ITEC della curva M–χ: 9, 9 e 8 pagine, rispettivamente N = 0, N = −350 kN con direzione 27°, N = 0 con tension stiffening. Esempio Word/PDF in `supporto/artefatti/report-template/review/`.
- Full WPF non eseguita. Restano le tre esclusioni infrastrutturali già dichiarate dal runner: `ElasticPile.Checks`, `BridgeValidationCurrent`, `ElasticPile.Performance`; nessuna nuova esclusione.

Nessun merge, push o rilascio eseguito.
