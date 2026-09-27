# Campagna live Bridge Design

`capture-site.mjs` si carica nella sessione CUA e riceve il tab supportato dal browser. Non crea driver, non legge il solver del sito e non accede a stato JavaScript privato. Ogni iterazione aziona la UI pubblica, legge i campi e la tabella dei risultati, attende stabilità delle schede animate e salva una configurazione unica. Il dataset finale ha 125 casi per ciascuna delle otto famiglie.

Le variazioni deliberate sono altezza, numero campate e classe del calcestruzzo; gli altri dati sono quelli prodotti dal generatore casuale pubblico. Non è una prova esaustiva di ogni cursore, ogni valore reale o del Detailed check AASHTO. Il campione è riproducibile come insieme di input congelati; una nuova acquisizione casuale genera un campione diverso.

Gli output sono in `supporto/artefatti/bridge_design_site_1000`.

```powershell
python supporto/test/BridgeDesign.SiteComparison/verify_capture.py
python supporto/test/BridgeDesign.SiteComparison/compare.py prepare
dotnet run --project supporto/test/BridgeDesign.SiteComparison -c Release -- --run supporto/artefatti/bridge_design_site_1000/engine-inputs.jsonl supporto/artefatti/bridge_design_site_1000/engine-results.jsonl
python supporto/test/BridgeDesign.SiteComparison/compare.py analyze
```

Il runner invoca `BridgeConcept.Calculate`, senza implementare una seconda copia del calcolo. Non modifica input, listini o risultati per ottenere una corrispondenza. Registra separatamente calcoli, rifiuti con motivazione e numeri non finiti. Il codice di uscita zero significa esecuzione completata, **non parità con il sito**: questa si legge in `case-verdicts.csv` e `REPORT.md`.

Due modalità per ogni caso: `native` mantiene gli automatismi ANTHEA supportati; `resolved` imposta le dimensioni adottate dal sito dove il motore ha un parametro corrispondente. I dettagli e i limiti della traduzione sono nel report e in `mappingNotes` di ogni input.

`verify_capture.py` controlla conteggi, unicità, quote per famiglia/modalità, hash di input e DOM, marker manuali, valuta, stabilità e coerenza fra schede visibili. Le incoerenze interne ai valori visualizzati dal sito sono segnalate, non corrette artificialmente.

`baseline-runner` conserva il binario precedente al controllo sul sollevamento. Per ripetere il confronto prima della correzione usare quel DLL con gli stessi input e salvare `engine-results-before-fix.jsonl`; il report confronta poi le classificazioni prima/dopo. `Render-Report.ps1` è un supporto al controllo visivo dei report Word dell'applicazione quando LibreOffice non è disponibile.
