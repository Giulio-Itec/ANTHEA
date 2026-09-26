# Validazione della separazione della libreria di calcolo

Data: 26 settembre 2026. Ambito: `X.Calculations`, progetti, coefficienti condivisi e collegamenti a interfaccia e report. Questa revisione verifica la separazione e la conservazione del comportamento; non costituisce una certificazione generale delle formulazioni ingegneristiche.

## Esito

La libreria è compilabile e utilizzabile fuori dal progetto desktop. Il pacchetto esportato contiene sorgenti della libreria, dipendenze Checker/Model/Geometry e test autonomi. Nessun riferimento a `X.Core`, WPF o archivi è richiesto dai test autonomi. La compilazione desktop termina senza errori o avvisi.

| Prova | Esito |
|---|---|
| Libreria compilata dalla cartella esportata | 57 controlli superati |
| Progetti e servizi comuni | 114 controlli superati, di cui 34 aggiunti per coefficienti, normativa e nomi |
| Checker CA, Excel, estensioni e dati | 102 + 18 + 174 + 27 controlli superati |
| Modulo CA ampliato | 78 controlli superati |
| Palo orizzontale | 1.079 controlli superati, oltre alla suite CHS |
| Coesione efficace | 312 controlli superati |
| Peso specifico saturo | 57 controlli superati |
| Sezione da ponte | 117 controlli superati |
| Micropalo verticale | 34 casi di riferimento e 101.883 confronti superati |
| Vecchi riferimenti Python | 414 superati, 50 differenze preesistenti su 464 casi |
| UI progetti | Creazione, riapertura, workspace, gerarchie e condivisione completati |
| UI moduli | Materiali, acciaio, CA, estensioni CA, palo orizzontale e curva ponte completati |
| UI ponte completa | Geometria, tensioni, opzioni, ritiri, taglio/pioli, dettagli, storico, curve e report del ponte completati |
| Report materiali | Generazione e controlli del pacchetto Word completati |
| Applicazione pubblicata in app | Creazione/riapertura progetti e report materiali completati; DLL di calcolo identica a quella compilata |
| Report complessivo del progetto | **Non completato**: interruzione durante il salvataggio dopo l’impaginazione |
| Aggregatore storico archivi/report | **Non completato**: interruzione durante il salvataggio del report micropalo |

Le attestazioni finali dei test UI sono necessarie: un processo terminato con codice zero e un file di avanzamento non costituiscono una prova superata. Il nuovo runner `Test-CalculationUi.ps1` verifica l’attestazione specifica di ogni suite e richiede cartelle vuote, per evitare di riutilizzare esiti precedenti.

## Provenienza dei riscontri

I test autonomi confrontano area e inerzia del rettangolo, area tagliata, distanza dai bordi/fori, rapporto di omogeneizzazione, coefficiente di spinta passiva e aderenza con risultati analitici. Coprono inoltre zero, NaN, valori fuori campo, coefficienti non pertinenti, normativa sconosciuta, input immutati e avvio del calcolo ponte senza UI. Le proprietà geometriche utilizzano direttamente la DLL GPC.Geometry fornita.

I 114 controlli di progetto sono regressioni e verifiche di comportamento: non sono 114 benchmark ingegneristici indipendenti. Verificano priorità del riferimento gerarchico, creazione atomica, ID indipendenti, aggiornamento degli alias dei coefficienti CA, assenza di duplicati, incompatibilità fra normative, ereditarietà del ponte e conservazione delle azioni locali.

I riferimenti Python sono esterni all’esecuzione C# ma derivano dalla precedente implementazione del progetto. Le 50 differenze non vengono nascoste modificando tolleranze o attesi. Il riepilogo di questa revisione ha lo stesso SHA-256 della baseline già confrontata:

`3BBD26EDE2F4AEA7CDDDE252F9E4588C5D8D5D0D0994BDCC4CDD0498C7E3DF4B`

Rimangono 2 casi del palo, 24 delle sezioni, 16 dell’analisi elastica, 4 della resistenza elastica e 4 dei domini. Per stabilire quale formulazione sia corretta serve l’analisi ingegneristica dei singoli casi descritta nel precedente audit `unificazione-calcoli-progetti.md`.

## Anomalie aperte dei report

Il test del report ricorsivo prepara i sette capitoli e arriva alla fase «Unione dei dati comuni e impaginazione», ma termina con codice zero prima di produrre `smoke.txt`. Resta il file temporaneo. Questo esito è incompleto, anche se le schede materiali e il report del ponte vengono esportati correttamente in prove separate.

Anche `Test-SoftwareReports.ps1` conferma l’interruzione dell’aggregatore dopo «Report: micropalo» e «Tabelle completate». Nella diagnostica precedente il punto era `File.Move` del salvataggio atomico. Non è stata identificata una causa certa; non è stata modificata la persistenza atomica per aggirare l’interruzione.

La verifica visiva Word mediante `render_docx.py` non è eseguibile su questa macchina: LibreOffice `soffice.exe` è assente. I controlli XML, relazioni e contenuti del report materiali sono conclusi, ma non sostituiscono il controllo dell’impaginazione stampata. Nessuna impaginazione Word viene dichiarata validata visivamente da questa revisione.

## Ripetizione e artefatti

Tutti gli output sono in `supporto/artefatti/calculation-library/`; sorgenti dei test in `supporto/test/`. La copia trasferibile è in `portable/`; il suo log finale è `standalone-checks.log`. I file di confronto sono `riferimenti-python.json` e `.log`; le prove UI sono in `ui/` e `ui-final/`. Per il report progetto consultare `ui/project-report/progress.txt`, non un esito di successo.

```powershell
dotnet run --project supporto/test/CalculationLibrary.Checks -c Release
dotnet run --project supporto/test/X.Verifiche -c Release -- --project-calculations supporto/artefatti/calculation-library/nuovo-test
./supporto/scripts/Test-CalculationUi.ps1
./supporto/scripts/Test-SoftwareReports.ps1
./supporto/scripts/Export-CalculationLibrary.ps1
```

I due aggregatori dei report devono continuare a fallire in modo esplicito se non raggiungono le rispettive attestazioni finali. Le formule e i solver alternativi conservano i limiti già documentati nei relativi dossier.
