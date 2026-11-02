# Revisione dell'interfaccia dei fogli Azioni — 2 novembre 2026

Richiesta dell'utente dopo la revisione indipendente dell'interfaccia dei fogli Azioni scritti da Codex (1.16.2–1.17.1): «risolvi tutti i punti che hai segnalato (sia alta che media)». Ramo `refactoring/f5-azioni-ui-revisione`, da `main` f9d0f354 (1.17.1). La relazione della revisione, con catture e prove, è in `supporto/artefatti/revisione-azioni-ui-20261102/` del checkout principale (non versionata).

Nessun motore GPC, contratto, attesi numerici, formula o relazione modificati. Le DLL di `lib/Checker` restano il set AT.

## Rilievi corretti

1. **Scheda nascosta selezionata all'apertura** (9 fogli). Le sette schede dei moduli, nascoste da uno stile, sono diventate una sola scheda il cui titolo e contenuto vengono dal foglio (`ModuleTabTitle` e un template per modulo). Un foglio senza scheda propria apre «Casi di carico».
2. **Vista condivisa dalla shell**. Tutti i fogli Azioni hanno lo stesso tipo di ViewModel e la shell riusa la stessa `ClimateSheetView`. La scheda scelta, l'assistenza e il diagramma aperti o chiusi stanno ora nel foglio (`ResultTabIndex`, `IsAssistanceExpanded`, `IsChartExpanded`): in memoria, mai nel documento (regola T6).
3. **Motivo del calcolo mancato**. Il messaggio del motore e gli avvisi compaiono sotto il titolo di destra (`CalculationNotice`, token `Warning.*`). Il testo «quindi Calcola» è sostituito da uno stato del calcolo.
4. **Calcolo all'apertura**. Il foglio avvia il ricalcolo automatico alla creazione, come il palo verticale: nuovo foglio, file riaperto, revisione, copia ripristinata da Annulla/Ripristina. I testi dei grafici vuoti non invitano più a «Calcolare».
5. **Testo in un campo numerico**. Il testo resta nel campo e nel documento, come negli altri moduli. Il validatore dei campi Azioni (`ActionFieldPresentation.NumberValidator`) mette l'errore sotto il campo; per i campi interi anche un decimale è un errore (`ClimateField.Integer`). `ClimateDocument.Validate` controlla la struttura del documento e accetta questi valori: conferma, cambio di scheda, salvataggio e riapertura non sollevano più eccezioni. Il calcolo li segnala con l'etichetta del campo (e della riga di tabella) invece del percorso JSON.
6. **Area minima del foglio (1120 × 600)**. `CappedSplitPanel`, un pannello di solo layout, dà all'assistenza al massimo il 45 % della colonna e al diagramma il 35 % della scheda dei casi; la tabella dei casi resta leggibile. La riga della descrizione del caso si nasconde quando è vuota.
7. **Reologia nell'area minima**. La colonna del grafico è in un `MinimumSizeViewport` (altezza minima 640) e scorre invece di schiacciare il grafico.
8. **Separatore decimale**. I numeri dei campi Azioni e i nomi delle righe usano il separatore scelto in «Aspetto». La reologia (valori, tabella, assi) usa la cultura corrente invece di it-IT fisso, senza separatore delle migliaia.
9. **«?» Wiki**. Sedici argomenti in `HelpTopics`, uno per foglio, aprono la sezione della guida pratica del modulo (`ActionsGuide`). Ogni campo ha «?» e F1.
10. **Focus dopo una scelta**. Quando una scelta o una spunta ricostruisce i moduli, il focus torna allo stesso campo (`FormViewModel.PendingFocusKey`, consumato da `FieldForm`; additivo, gli altri moduli non lo usano).
11. **Test**:
    - `ClimateViewChecks` e `SpectraViewChecks` attivano davvero la traccia dei binding (`PresentationTraceSources.Refresh`); un binding volutamente rotto prova che la traccia lo cattura.
    - La suite Azioni WPF controlla anche: calcolo e scheda visibile all'apertura, «?», separatore, focus e area minima.
    - Nella shell vera (`ShellComposition`) prova il passaggio fra due fogli Azioni, il messaggio di un calcolo senza risultati e un testo in un campo numerico con cambio di scheda.
    - Per la reologia prova area minima, separatore «Punto» e sola lettura della vista.
    - `ClimateSheetChecks` e `HelpTopicsChecks` coprono le stesse regole senza WPF.

## Non compresi

- I rilievi di gravità bassa della revisione (12–16) restano aperti: età letta con 17 cifre e scelte di consultazione che segnano modifiche nella reologia, unità predefinita «–» del diagramma, nomi di automazione, dimensioni dei caratteri fuori dai token.
- Spettri e Stratigrafia non calcolano all'apertura nella shell, come i fogli Azioni prima della correzione: fuori perimetro.
- La guida pratica descrive già messaggio e invalidazione; una frase sul calcolo automatico e sul «?» dei fogli Azioni andrà nella prossima edizione Word/PDF delle guide.

## Verifiche

Le corse sono state eseguite nel worktree del ramo e spostate in `supporto/artefatti/ci` del checkout principale, conservate con `-Keep`.

- **`20261102-151931-azioni-ui-mirata-1`**, codice di 9837c8d7 prima dei commit: 28 PASS e 1 NEW-FAIL.
  - PASS: Architecture, Application, Wiki, Presentation (4987 controlli), Spettri, Climate.Tests, Headless/help, Rheology, f3-equivalence (nessuna differenza non ammessa), materiali, Spettri WPF, Azioni WPF, parità di FieldForm, token, decisioni di layout, contrasto completo (1399 s), bozza della shell, shell-smoke-project-workspace, ModuleHost.
  - NEW-FAIL `ConcreteTimeEffects.Checks`: il controllo attendeva la virgola fissa («1,000»). Questa macchina ha Windows it-IT con punto decimale, cioè proprio il caso del rilievo 8. L'atteso ora segue il separatore dell'applicazione.
- **`20261102-155845-azioni-ui-pulita-2`**, commit 9837c8d7: 12 PASS, 0 NEW-FAIL, comprese ConcreteTimeEffects.Checks/UiChecks e le due suite WPF di Azioni e Spettri. La suite Azioni WPF ripetuta tre volte: 1125 controlli ogni volta.
- **`20261102-160747-azioni-ui-pulita-3`**, commit 6ac9f5db (correzione di CappedSplitPanel): 9 PASS, 0 NEW-FAIL, tra cui Architecture, Climate.Tests (1536 controlli), Azioni WPF (1125), token e decisioni di layout.
- **`20261102-161130-azioni-ui-dopo-1172`**, merge 289cfe78 con main 1.17.2 (1272b85c): 10 PASS, 0 NEW-FAIL (Architecture, Climate.Tests, Rheology, ConcreteTimeEffects.Checks, Azioni WPF).

Il contrasto completo è stato eseguito prima di 6ac9f5db, che cambia solo le altezze del pannello e non i colori.

Suite UI: 73 prima e dopo, nessuna rimossa. Full non eseguita; resta quella prevista dall'utente per la notte.

## Integrazione

L'utente ha approvato merge e push («Sì, merge e push»). Avanzamento rapido di main sul ramo, che contiene già 1.17.2; nessuna nuova versione né tag: il numero della prossima versione resta da decidere.
