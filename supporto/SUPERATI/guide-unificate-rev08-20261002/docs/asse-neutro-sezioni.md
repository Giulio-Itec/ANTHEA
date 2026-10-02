# Asse neutro nelle viste di sezione

La casella **Asse neutro**, attiva inizialmente, mostra una linea magenta tratteggiata con fondo bianco per conservarne la leggibilità sul contouring. La preferenza è salvata; modificarla non richiede un nuovo calcolo.

| Vista | Risultato utilizzato | Significato della linea |
| --- | --- | --- |
| CLS, tensioni SLE e ispezione del dominio | `StressAnalysisResult.StrainPlane.GetNeutralAxis()` di Checker | ε = 0 della combinazione o del punto limite selezionato, anche in flessione deviata |
| Ponte, metodo cumulativo | `BridgeStage.SteelNeutralAxis` | σa = 0 delle tensioni cumulate nell’acciaio |
| Ponte, storico lineare e non lineare | `HistoryStageResult.TotalPlane`, convertito in `StrainPlane` di Checker | ε totale = 0 alla fase selezionata |

Nello storico, attivazione dei materiali, ritiro e plasticità possono separare lo zero della deformazione totale dagli zeri delle tensioni. La linea non è quindi presentata come uno zero tensionale comune a tutti i materiali. Nei trefoli anche la predeformazione può separare i due riferimenti.

Con campo uniforme non si disegna un asse arbitrario: compare «non definito (campo uniforme)». Un asse fuori dall’ingombro della sezione è segnalato; se fuori dal riquadro grafico compare anche «fuori vista». L’indicazione riguarda l’ingombro, non l’intersezione con il materiale di sezioni cave o concave.

Il pannello geometrico del ponte resta privo di risultati tensionali. L’asse segue la fase nella pagina dei risultati, nella vista staccata e nelle immagini esportate.

## Verifica

Harness: `supporto/test/Desktop/NeutralAxisSmokeChecks.cs`, avviabile con `ANTHEA.exe --smoke-neutral-axis <cartella-output>`.

Controlli su campi affini noti (assi orizzontali, verticali, inclinati, origine traslata), intersezione ε = 0 dopo il ritaglio grafico, campi uniformi, asse esterno, stato CLS reale di Checker, tutti e tre i metodi del ponte, cambio fase, preferenze e vista staccata. Non si tratta di una nuova validazione dei solutori strutturali: i test verificano l’adattamento e la rappresentazione dei risultati esistenti.

Schermate e risultati in `supporto/artefatti/assi-neutri/`.
