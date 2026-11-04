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

- I rilievi di gravità bassa della revisione (12–16) sono rimasti aperti fino al 4/11, poi sono stati corretti (sezione «Rilievi bassi» qui sotto).
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

## Rilievi bassi — 4 novembre 2026

Richiesta dell'utente, riportata dalla sessione principale: correggere anche i rilievi bassi («Tutti i 16»). Ramo `refactoring/f5-azioni-ui-bassi`, da `main` b05238fa (dopo la 1.18.0). Stessa regola: nessun motore GPC, contratto, attesi numerici, formula o relazione modificati; le DLL di `lib/Checker` restano quelle di main. Voce `AZIONI-UI-BASSI-20261104` nel registro delle differenze.

12. **Reologia: consultazione e età letta.**
    - «Grandezza», «Asse X log» e «Asse Y log» sono consultazione, come la scheda o il caso scelti nei fogli Azioni (`RheologyDocument.ViewKeys`). Il foglio le legge dal documento all'apertura e non le scrive più: cambiarle non segna il documento come modificato e non aggiunge un passo ad Annulla. Non entrano nei risultati né nella relazione.
    - L'età di lettura resta un dato: la relazione stampa la «Lettura puntuale» a quell'età. Sceglierla, anche cliccando una riga della tabella, è una modifica del foglio, come prima.
    - Una riga della tabella porta l'età con le cifre della tabella («0.###», per esempio 0,932), non con le 17 cifre del double.
13. **Diagramma dei valori.** Le unità adimensionali («–», «−», «-», «—») non sono più l'unità predefinita: in Neve e Vento edifici il diagramma parte dai carichi in kN/m². I coefficienti compaiono solo quando il caso non ha altre grandezze; prima, con «−» e «-», il diagramma restava vuoto. Il selettore ha l'etichetta «Unità».
14. **Reologia come gli altri fogli.**
    - Dopo una modifica le curve si ricalcolano da sole dopo 500 ms, come nei fogli Azioni. Durante l'attesa il grafico dice «Aggiornamento delle curve…». «Aggiorna le curve» resta per l'aggiornamento immediato ed è disattivato in sola lettura.
    - «Esporta grafico…» passa da `IFileDialogService`; un errore di scrittura va al servizio dei messaggi.
    - La finestra aperta dalla sezione c.a. si comporta allo stesso modo.
    - Il passaggio degli ingressi a `FieldForm` non è stato fatto: la revisione lo indicava come facoltativo.
15. **Automazione.**
    - Nella vista Azioni hanno un nome: il selettore del caso («Caso di carico»), quello dell'unità, le liste delle righe («Righe · titolo»), le due tabelle, i due schemi, il diagramma e il selettore dell'ascissa della precompressione.
    - Nel dialogo di trasferimento hanno un nome il cavo di destinazione e la casella di conferma.
    - Le prove trovano selettori e casella con `AutomationId` al posto di `Tag`.
16. **Tipografia e testi.**
    - Le dimensioni 21, 12, 11 e 15 della vista Azioni sono ora i token `Font.Title`, `Font.Small`, `Font.Caption` e `Font.Subheading`.
    - Pannello di assistenza:
      - le istruzioni sono all'infinito («Selezionare un campo…»), come gli altri messaggi dei fogli Azioni;
      - «quote e componenti associati»;
      - per i parametri generali: «non è una dimensione dello schema né un suo componente».
    - La legenda dello schema indica anche la forma dei tratti: le quote sono linee a doppia freccia, le azioni frecce.
    - Nella reologia, l'intestazione «Tabella · selezionare un'età» e i messaggi di stato sono all'infinito.

### Verifiche dei rilievi bassi

Corse nel worktree del ramo, spostate in `supporto/artefatti/ci` del checkout principale e conservate con `-Keep`. Stesse suite mirate prima e dopo: Architecture, Presentation, Climate.Tests, Rheology, ConcreteTimeEffects (dominio e WPF), ModuleHost, materiali, Azioni WPF, parità di FieldForm, token, decisioni di layout e bozza della shell.

- **`20261104-115813-azioni-bassi-prima`**, b05238fa prima delle modifiche: 18 PASS, 0 NEW-FAIL; Azioni WPF 1125 controlli.
- **`20261104-121256-azioni-bassi-dopo-1`**, modifiche non ancora committate: 18 PASS, 0 NEW-FAIL; Azioni WPF 1165 controlli. Uscita 1 solo perché questo verbale è stato scritto durante la corsa.
- **`20261104-122457-azioni-bassi-pulita`**, commit ebf55579: 19 PASS, 0 NEW-FAIL, contrasto completo compreso (1429 s).

Confronto fra la corsa «prima» e quella pulita:
- I 62 CSV dei risultati (41 di Climate.Tests, 21 di Presentation.Tests) e le 99 figure di riferimento della suite Azioni WPF sono identici byte per byte. Gli attesi numerici non sono stati toccati.
- Le catture intere dei fogli Azioni cambiano tutte, per il titolo (22 invece di 21) e la legenda. In Neve e Vento edifici il diagramma parte da kN/m² invece che da «–», con l'etichetta «Unità».
- Le catture della reologia cambiano solo nell'intestazione «Tabella · selezionare un'età»: valori, grafico e tabella sono identici.

Suite UI: 73 prima e dopo, nessuna rimossa. Full non eseguita. Le guide Rev68 non descrivono ancora il ricalcolo automatico della reologia né le opzioni del grafico non salvate: vanno nella prossima edizione, insieme al calcolo automatico e al «?» dei fogli Azioni.
