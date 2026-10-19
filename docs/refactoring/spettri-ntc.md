# Spettri NTC e isolamento sismico

Richiesta: partire sempre da refactoring/f5; aggiungere Moduli singoli → Azioni → Spettri NTC, equivalente al riferimento CSLP 1.0.3, poi l'isolamento dall'Excel fornito.

## Branch e architettura

ANTHEA: `refactoring/f5-azioni-spettri`, da `refactoring/f5` b9c2d4b1. Checker: `codex/azioni-spettri`, checkout isolato `../SpettriWork/Checker`, da master bce7aaae. Primo motore 479b07c7, GPC.Design 0.22.0.0: nuova funzionalità, incremento del secondo componente. Correzione del limite di periodo ca4eba14; commit versioni 5dc39d91, GPC.Design 0.22.0.1 (fix, ultimo componente).

Calcoli e pericolosità in GPC.Design.Actions; documento, importazione e adattatore in ANTHEA.Application; report in ANTHEA.Reports; ViewModel senza WPF in ANTHEA.Presentation; vista XAML e grafici in X.Desktop. Nessun motore preesistente spostato o eliminato; X.Core e X.Calculations invariati. Nessun push o merge autorizzato.

## Riferimenti e confronto

Originali e catture: `supporto/artefatti/spettri-ntc`. L'XLS CSLP originale e l'XLSX utente sono conservati; solo una copia di analisi è resa editabile per le prove. Excel opera in un'istanza separata e nascosta, con macro/eventi disabilitati e collegamenti esterni non aggiornati.

Reticolo di 10751 nodi, nove tempi di ritorno, 99 ordinate tabulate per nodo; 8100 comuni dell'edizione originale. Le due interpolazioni conservano le operazioni del foglio. Il generatore compilato mantiene gli identificativi delle celle per audit, senza eseguire formule caricate dai documenti.

Primo confronto: 20 casi Excel, 17796 valori, entrambe le interpolazioni e tutti gli stati limite. Massimo scarto assoluto 1.3322676295501878e-14, tolleranza 1e-10 × max(1, |atteso|). Le fixture indipendenti sono in Checker/tests/Design.Checks/Fixtures/spettri-*.json. Anche esempio isolamento, formula analitica, input non validi e concorrenza sono verificati sulla DLL completa.

Confronto aggiuntivo: 14 casi, di cui due esterni, su nodo esatto, quadrilatero e tutte le quattro configurazioni triangolari; entrambe le interpolazioni, nove TR e 99 ordinate tabulate. Altri 1524 confronti numerici passati, scarto massimo 9.325873406851315e-15. Totale 19320; nessun atteso precedente riscritto. Catture congelate in `tests/fixtures/spettri/cslp103-grid-*.json`, confronto permanente in `SpectraEdgeChecks`, acquisizione indipendente con `tools/spettri/Capture-GridEdges.ps1`. La cattura include il SHA-256 della copia Excel analizzata.

Il riferimento ufficiale è per NTC 2008: l'interfaccia lo dichiara. Si conservano VR minimo 35 anni, limiti TR peculiari del file, pavimento 0.2 ag negli SLU e ordinata verticale iniziale del riferimento. L'equivalenza non è un aggiornamento automatico alle NTC 2018.

## Differenze dell'isolamento

NTC Italia 2018, §7.10.5.3.2, verificato sulla G.U. 42, supplemento ordinario 8 del 20 febbraio 2018: smorzamento dell'isolamento per T ≥ 0.8 Tis. L'Excel titola il §7.10.5.3.1 e scambia le etichette Tbf/Tis.

Il §3.2.3.2 della stessa fonte (pagina 47, pagina PDF 51) ammette questi spettri per periodi fondamentali fino a 4 s; oltre richiede analisi specifiche o storie temporali. Il primo candidato estendeva ancora la formula: GPC.Design 0.22.0.1 rifiuta Tis > 4 s per qualsiasi stato limite e limita il grafico a 4 s (SPETTRI-03). Prova indipendente del bordo con M = 4000/π² e K = 1 kN/mm, casi inferiori/superiori e superamento nei soli SLU. Le cinque prove Spettri della libreria sono passate; nessun risultato dell'esempio cambia.

L'esempio valuta Sa con VLOOKUP approssimato sul campione precedente a Tis. Il modulo mostra quel valore per confronto e valuta il risultato analiticamente a Tis, senza il vuoto di 0.0001 s dei campioni. Caso SLC: Tis 1.419838014727065 s; Sa tabulata 0.17628437253944718 g; Sa analitica 0.170127556597669 g; forza analitica 688.6093188500648 kN e spostamento 85.22392560025554 mm, contro 713.5296839969017 kN e 88.30812920753733 mm nel foglio. Sono differenze intenzionali, non floating point.

Il catalogo è letto dal file scelto dall'utente, senza attribuirgli aggiornamento o validità commerciale. Si importano valori memorizzati, non formule; nome e SHA-256 sono salvati. Non si replicano i riferimenti errati a celle vuote nelle ricerche dell'esempio. Il pendolo richiede R esplicito e conserva g=9806 mm/s²; le forze di isolamento usano g=9.81 m/s².

## Stato

- [x] Catture indipendenti e motore Spettri con primo confronto numerico.
- [x] Isolamento e pendolo con confronto dell'esempio e formula analitica.
- [x] Documento, importazione, ViewModel, XAML, catalogo Azioni e report predisposti.
- [x] Set candidato completo da commit locali, senza installazione in lib/Checker.
- [x] Collaudo mirato ANTHEA, salvataggio/riapertura, report e WPF.
- [x] Guide globali Rev48, Word/PDF e indici verificati.
- [~] Runner full: esecuzioni 01/02/03 interrotte per completare le correzioni; ripartenza completa con il set aggiornato.
- [ ] Pubblicazione del set definitivo, soggetta ad autorizzazione Git.

Il primo treno si è fermato per GMsh.Net assente nei test del checkout isolato di Model. La dipendenza è stata predisposta soltanto nei percorsi di test; il secondo treno ha prodotto il primo set candidato completo. Il set finale 03 è preparato con `Update-Snapshot.ps1 -AllowCandidate -Staging`, senza `-Lib` né `-Install`: il controllo versione/impronta contro lo snapshot corrente è passato e `lib/Checker` rimane invariata.

Il treno candidato 02 ha 3065 PASS; il set finale 03 ha 3066 PASS, 17 fallimenti Steel già censiti dal runner delle librerie e due prove Model ignorate, senza nuovi fallimenti. Utilities, Geometry, DelaunayMesh e le DLL Model sono identiche allo snapshot attuale. Nel set 03 hanno versioni superiori anche le DLL Checker ricompilate con nuovi metadati: Concrete 0.11.0.1, Geotechnics 0.4.0.1, CompositeBridge 1.7.0.1, Steel 2.0.4.9, ModelChecker 2.1.0.9 e Masonry 0.1.0.4. Nessuna formula di questi motori cambia; si rispetta il controllo versione/impronta senza aggirarlo. `Release-Set.ps1` richiede commit presenti su `origin/master`: il solo push del branch non basta al rilascio definitivo. La prima anteprima, basata sul set 02, è superata e non è il pacchetto da consegnare.

Collaudo mirato `20261019-005541-spettri-target-03`: 4 PASS (due build e due suite). Integrazione `20261019-011055-spettri-integration-02`: architettura, applicazione, Wiki, Spettri e vista passati; registro di prova del ModuleHost adeguato ai due moduli MVVM della Release. I confronti con il catalogo storico escludono esplicitamente i nuovi moduli, mantenendo invariati tutti gli attesi precedenti. Le segnalazioni sintattiche F4 sui numeri di riga Excel e sulla larghezza OpenXML sono risolte rendendo espliciti gli indirizzi e l'unità tipografica; nessuna baseline o regola indebolita.

`20261019-011432-spettri-full-01` è un'esecuzione parziale, interrotta dopo lo stesso errore nelle due prove di navigazione delle revisioni di progetto: il campo di ricerca del comune era disabilitato ma senza `IsReadOnly`. Aggiunto il binding alla vista e il controllo diretto di tutti i TextBox visibili; i test generali restano invariati. `20261019-014257-spettri-readonly-01`: 6 PASS, comprendenti due build, Spettri, entrambe le navigazioni di progetto e la vista. Segue un nuovo full su commit pulito; il primo non viene dichiarato completato.

Anche `20261019-014508-spettri-full-02` è parziale: interrotto dopo build, architettura, report, applicazione e Wiki passati, per applicare il limite normativo a 4 s e ricostruire l'intero set. Non è un full completato né un fallimento numerico. Il collaudo finale deve riferirsi al nuovo set 03.

Guide: 87 e 153 pagine, indici Word aggiornati, 26 parti del template ITEC identiche; 359 formule Office Math conservate nella guida teorica. Controllo visivo di tutte le pagine, con dettaglio dei nuovi capitoli. Corrette l'enfasi involontaria di Tc* e la resa degli esponenti. Il renderer Wiki non accetta l'ambiente LaTeX `cases`: la forma spettrale è scritta in quattro formule separate, con le stesse condizioni. Dopo la correzione `ui/check-wiki-offscreen` passa. Il confronto per pixel ha identificato soltanto le pagine modificate, tutte controllate; le altre rimangono identiche alla verifica integrale iniziale. `render_docx.py` non utilizzabile perché il runtime non include LibreOffice; adottata la pipeline Word del repository e rasterizzazione PDFium. Il report del modulo è verificato sia con il renderer interno (7 pagine) sia con Word (8 pagine). Evidenze in `supporto/artefatti/spettri-ntc/qa`, `qa-formule-wiki` e `supporto/artefatti/guide_anthea_itec_rev48`.

Il mirato finale `20261019-020547-spettri-final-target-03` ha 7 PASS e il solo errore Wiki della formula; Spettri conta 1572 controlli passati. Dopo la correzione, `20261019-021157-spettri-wiki-final-01` completa la cattura F3 ma segnala 57 differenze. Analisi completa con `f3_confronto_completo.py`: 16 numeri di versione Geotechnics, un conteggio articoli, 35 differenze editoriali e cinque transizioni precedenti da sostituire. Nessun nuovo cambiamento dei risultati di calcolo. Registro SPETTRI-04: sostituite cinque transizioni e aggiunte dodici, tutte con valori prima/dopo esatti; quattro nuovi file Wiki e ricerca aggiornati con impronte nel manifest (F3-06). Gli attesi numerici rimangono invariati.

Ripetizione del confronto completo sulla stessa cattura immutabile, con il registro aggiornato: 1070 file, 107 gruppi, 22138 transizioni elencate e riscontrate, zero differenze non ammesse (`f3-completo-dopo.json`). Segue la corsa full su commit pulito con nuova cattura: questa verifica isolata non la sostituisce.

`20261019-021839-spettri-full-03`, su 6dd9d824, è interrotto dopo un errore di Presentation.Tests: l'impronta del risultato nelle tracce Fondazioni F5 include anche `library_version`. I risultati F3 avevano già dimostrato l'assenza di altre differenze. Il generatore condiviso delle tracce riconduce soltanto il metadato esatto 0.4.0.1 alla versione 0.4.0.0 del riferimento, su una copia; ogni altro campo e ogni altra versione restano inclusi nell'impronta. Quattro controlli verificano corrispondenza, immutabilità e rilevazione di una variazione numerica o di una versione non dichiarata. Nessuna traccia congelata è riscritta. Anche questa esecuzione resta parziale.
