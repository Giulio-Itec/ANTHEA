# Dieci esempi ANTHEA — stabilità globale

Aprire i modelli con ANTHEA, quindi Input → Terreno → Stabilità globale. Il profilo, gli strati, la falda, i parametri di ricerca e le combinazioni sono salvati nel modello. In Verifiche → Stabilità globale si consultano cerchio critico e conci.

Ogni cartella contiene il modello `.anthea`, i risultati JSON, tutti i conci CSV, la relazione Word e le istruzioni per ripetere il calcolo.

**I controlli interni non sostituiscono il confronto con MAX.** Consultare [stato del confronto](stato-max.json) e [rapporto Word](Rapporto-controllo-stabilita-globale.docx).

| Caso | Modello | Descrizione |
|---|---|---|
| 01-mensola-base | [Apri modello](esempi/01-mensola-base/modello.anthea) | Mensola H=3 m, terreno granulare asciutto φ=30°, q=10 kPa. |
| 02-mensola-alta | [Apri modello](esempi/02-mensola-alta/modello.anthea) | Mensola H=5 m; effetto dell’altezza con profilo riallineato. |
| 03-gravita | [Apri modello](esempi/03-gravita/modello.anthea) | Muro a gravità, sezione trapezia; peso reale del corpo rigido. |
| 04-fondazione-debole | [Apri modello](esempi/04-fondazione-debole/modello.anthea) | φ=30° nel riempimento; φ=22° sotto il piano di posa. |
| 05-coesione-profonda | [Apri modello](esempi/05-coesione-profonda/modello.anthea) | Come 04, c′=10 kPa nello strato di fondazione; drenato. |
| 06-tre-strati | [Apri modello](esempi/06-tre-strati/modello.anthea) | Riempimento, strato debole fino a −2 m e substrato φ=38°. |
| 07-falda | [Apri modello](esempi/07-falda/modello.anthea) | Falda da y=−0,10 m a valle a y=1,50 m al tallone e a monte; pesi saturi e pressioni interstiziali. |
| 08-sisma | [Apri modello](esempi/08-sisma/modello.anthea) | SLV: ag/g=0,20, F0=2,5, categoria C, St=1; kh=0,1064 e kv=±0,0532. |
| 09-urto | [Apri modello](esempi/09-urto/modello.anthea) | Urto equivalente H=50 kN/m in testa; combinazione eccezionale separata. |
| 10-non-drenato | [Apri modello](esempi/10-non-drenato/modello.anthea) | cu=50 kPa, φu=0; analisi globale a tensioni totali. Conci in trazione segnalati, nessun taglio automatico. |

Il caso non drenato segnala una discretizzazione non convergente e non produce un esito favorevole. I casi non soddisfatti restano esplicitamente tali: la raccolta serve a controllare il comportamento del motore, non a certificare dieci muri verificati.

Rigenerazione: `dotnet run --project supporto/test/GlobalStability.Checks -c Release -- <cartella output nuova>`. Il marcatore `stato-esecuzione.txt` deve riportare PASS; il solo codice di uscita del processo non prova che tutti gli export siano stati completati.
