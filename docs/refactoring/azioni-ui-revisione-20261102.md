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
- Spettri e Stratigrafia non calcolano all'apertura nella shell, come i fogli Azioni prima della correzione: fuori perimetro. Chiuso il 4/11: Spettri calcola all'apertura; la Stratigrafia non ha calcolo (sezione «Punti aperti chiusi» qui sotto).
- La guida pratica descrive già messaggio e invalidazione; una frase sul calcolo automatico e sul «?» dei fogli Azioni andrà nella prossima edizione Word/PDF delle guide. Chiuso il 4/11 con le guide Rev69.

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

Suite UI: 73 prima e dopo, nessuna rimossa. Full non eseguita. Le guide Rev68 non descrivono ancora il ricalcolo automatico della reologia né le opzioni del grafico non salvate: vanno nella prossima edizione, insieme al calcolo automatico e al «?» dei fogli Azioni (fatto: sezione seguente).

## Punti aperti chiusi — 4 novembre 2026

Richiesta dell'utente: «correggi gli altri due punti aperti». Ramo `refactoring/f5-spettri-guide`, da `main` 6710cf59. Voce `SPETTRI-GUIDE-REV69-20261104` nel registro delle differenze.

1. **Spettri e Stratigrafia all'apertura.**
   - **Spettri** aveva lo stesso difetto dei fogli Azioni: nessun calcolo all'apertura e il testo «Premere Calcola per determinare gli spettri.», mentre né la shell né la vista hanno un comando «Calcola». Ora il foglio avvia il calcolo alla creazione: nuovo foglio, file, revisione, copia ripristinata da Annulla. Il testo descrive lo stato del calcolo (in corso, dati da correggere, calcolo automatico), e il motivo resta nel messaggio del calcolo già mostrato dalla vista. Anche il testo dell'isolamento non invita più a «calcolare».
   - **Stratigrafia:** nessuna correzione, perché è un foglio di dati senza calcolo (`SheetCalculationMode.None`), in cui «Calcola» conferma e valida soltanto. Il «risultati=False» della prova del 2/11 era il comportamento previsto, non un difetto.
2. **Guide Rev69** (edizione 6, 4 novembre 2026), con la stessa procedura e lo stesso modello ITEC della Rev68.
   - **Guida pratica:**
     - Spettri senza «Calcola»;
     - procedura comune dei fogli Azioni:
       - calcolo all'apertura e dopo le modifiche;
       - motivo del calcolo mancato;
       - testo nei campi numerici;
       - separatore decimale;
       - «?» e F1;
       - unità iniziale del diagramma e selettore Unità;
     - reologia:
       - ricalcolo automatico;
       - «Aggiorna le curve» spento in sola lettura;
       - opzioni del grafico non salvate;
       - età della tabella.
   - **Guida teorica:** cambia solo la riga della revisione.
   - **Esito della costruzione:**
     - pratica 132 pagine, 1364 frammenti, 9 formule;
     - teorica 212 pagine, 2552 frammenti, 1566 formule;
     - 26 parti ITEC identiche per documento, nessun glifo sostitutivo;
     - frasi nuove alle pagine 8, 101, 102 e 115 della pratica.
   - **Archivio:** le Rev68 (16 file) sono in `supporto/SUPERATI/azioni-spettri-rev69-20261104`, con manifest.
   - **Wiki:**
     - indice della Wiki rigenerato;
     - 10 transizioni editoriali nuove della Wiki per gli articoli Spettri e reologia;
     - 2 righe già elencate aggiornate al testo nuovo (`editorial_transitions_rev69`).

### Verifiche

- **`20261104-131705-spettri-guide-prima`**, 6710cf59: 20 PASS, 0 NEW-FAIL (Spettri, Wiki, guide, f3-equivalence, shell, Azioni WPF).
- **`20261104-133159-spettri-dopo-1`**, Spettri modificato: 11 PASS, 0 NEW-FAIL; uscita 1 solo per le guide modificate durante la corsa.
- **`20261104-134003-guide-rev69-1`**, guide Rev69: 11 PASS. f3-equivalence segnala soltanto le 14 differenze editoriali dei due articoli: 12 righe di testo e 2 transizioni con il testo vecchio.
- **`20261104-134644-guide-rev69-f3`**, con le transizioni nuove: f3-equivalence PASS.
- **`20261104-135033-spettri-guide-pulita`**, commit 3f6b16a1, stesse suite della corsa «prima»: 20 PASS, 0 NEW-FAIL, uscita 0. Presentation.Tests e Spettri.Tests danno gli stessi attesi numerici (Agliè SLV), e f3-equivalence passa con le sole transizioni editoriali.

Suite UI 73, nessuna rimossa. Full non eseguita: l'utente la lancia stanotte.

## Messaggio dei risultati senza calcolo — 4 novembre 2026

Richiesta dell'utente: «Sì, correggilo». Ramo `refactoring/f5-messaggio-calcola`, da `main` 397d5cfc. Voce `SHELL-RISULTATI-SENZA-CALCOLA-20261104` nel registro delle differenze.

- **Il problema.** «Risultati JSON…» della shell rispondeva «Premere Calcola prima di esportare i risultati.», il testo della finestra storica. La shell non ha un comando «Calcola». La maggior parte dei fogli calcola da sola; pochi hanno un pulsante di calcolo proprio («Calcola» delle fondazioni superficiali, «Calcola tutti i casi» delle paratie, le curve).
- **La correzione, nella shell:**
  - `ShellCommandsViewModel.CalculateFirst` dice «Nessun risultato da esportare: attendere il calcolo del foglio, o avviarlo dal foglio se ha un comando di calcolo, e correggere i dati segnalati.»;
  - lo stesso messaggio vale per un foglio che sta ricalcolando. Prima la shell apriva il dialogo e poi riceveva il rifiuto storico di `SheetFileExports.WriteResult`;
  - la Stratigrafia, che non ha calcolo, spiega come conservare il foglio, come già i materiali;
  - la sezione c.a. conserva il suo messaggio sull'aggiornamento automatico.
- **Il testo storico resta dov'è congelato e dove l'utente non lo vede dalla shell:**
  - il rifiuto di `SheetFileExports.WriteResult`, confrontato dalla traccia di Bridge Design e da cinque suite UI;
  - il rifiuto interno dei report dei pali, che è il motivo di 15 `status.json` F3.

  Una prima versione cambiava anche questi testi: la corsa `messaggio-calcola-pulita` l'ha mostrato con il confronto F3 del palo verticale, ed è stata ristretta (f414f2f1).
- **Le prove:** `CommandsChecks` prova la Stratigrafia e che nessun messaggio dei risultati della shell chieda «Calcola».
- **Non compreso:** il titolo «Premere Calcola» dei grafici del palo verticale prima del primo risultato (`VerticalPileViews.InitialTitle`), legato alle figure F3. Si vede solo finché il calcolo automatico non pubblica, o con dati non validi.

### Verifiche

- **`20261104-143636-messaggio-calcola-prima`**, 397d5cfc: 12 PASS e Presentation.Tests FAIL. Il fallimento è in `BridgeDesignSheetChecks`, scenario «ottimizzazione», passo «riapri»: la traccia attendeva il calcolo ancora occupato e senza risultato, mentre il risultato era già pubblicato. È una corsa critica sui tempi: avviene sul commit di partenza, con la macchina carica (palo orizzontale 400 s), e non riguarda questo messaggio. Mai vista prima nelle corse del checkout principale.
- **`20261104-145940-messaggio-calcola-pulita`**, prima versione (1ef3e26e):
  - Presentation.Tests FAIL, perché i 15 `status.json` F3 dei pali portano il testo storico;
  - `ui/check-palo-verticale-view` FAIL;
  - 11 PASS.

  Da qui la correzione ristretta alla shell.
- **`20261104-151530-messaggio-calcola-pulita-2`**: non eseguita. La corsa precedente era ancora in esecuzione e bloccava le DLL della configurazione UiTests.
- **`20261104-151651-messaggio-calcola-pulita-3`**, commit f414f2f1, stesse suite più f3-equivalence: 14 PASS, 0 NEW-FAIL, uscita 0. Anche Presentation.Tests passa, compresa la traccia di Bridge Design.

Suite UI 73, nessuna rimossa. Full non eseguita.
