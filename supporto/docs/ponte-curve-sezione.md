# Curve della sezione composta

La scheda **03 Curve della sezione** raccoglie M–κ a N costante e N–ε a κ
costante, insieme a σ–ε della fibra selezionata. Il calcolo è nella libreria
Checker, separato dai metodi per fasi; ANTHEA converte le unità e presenta i dati.

Si può partire dalla sezione vergine o da una fase attiva. Nel secondo caso
viene rieseguita la storia fino alla fase selezionata con le leggi non lineari,
conservando getto, ritiri e plasticità. La scelta del metodo nelle altre schede
rimane memorizzata e non cambia il significato di questa curva non lineare.

Scegliere tipo di curva, origine, quota di riferimento, N oppure κ costante,
escursione complessiva e numero di punti. La scelta di mantenere N/κ della
storia conserva il vincolo corrispondente dello stato iniziale. Gli ingressi
della vista usano kN, kNm, mm, 1/m e µε. L'escursione può avere entrambi i segni.
Nel controllo N–ε il momento risultante è anche la reazione necessaria a
mantenere la curvatura: non è imposto nullo.

Il cursore collega il punto della curva alla fibra e alla riga di risultati.
Il pannello σ–ε permette di leggere deformazione totale, imposta, meccanica e
plastica. La tabella contiene piani di deformazione, risultanti, estremi e
residui. Il CSV esporta tutti i punti e tutte le fibre con le componenti di
deformazione e lo stato plastico, oltre all'esito del percorso.

Il calcolo si avvia esplicitamente ed è annullabile. Dopo una modifica degli
ingressi la vecchia curva resta leggibile come risultato da aggiornare e non
può essere esportata come corrente. Le opzioni sono salvate nell'archivio.
In caso di limite materiale o mancata convergenza sono mostrati soltanto i
punti validi, con la causa di arresto.

Le curve usano sezione lorda e legami caratteristici. Non modellano
post-instabilità di classe 4, connessione parziale, creep nel tempo o danno
ciclico del CLS. Mesh e impostazioni dei materiali provengono dalla sezione;
le opzioni di classe 4 delle altre analisi restano conservate.

## Validazione eseguita

154 test CompositeBridge e 287 test ordinari BridgeAudit: **441 superati**.
Gli 8 nuovi test di integrazione sono inclusi nei 287; i 17 controlli della
nuova vista fanno parte del collaudo grafico separato.
Sono stati confrontati 64 stati con OpenSees 3.8.0:
29 dello storico e 35 delle curve. Il massimo scarto di σ sulle curve è
0,000109108 MPa nel modello a fibre concordato.

I difetti delle vecchie DLL sono tenuti separati: 5 casi riproducono un errore
e 10 restano ignorati; non fanno parte dei 441 esiti verdi. Dati di ingresso,
atteso/ottenuto, tolleranze, riferimenti esterni, ipotesi e limiti sono nel
documento `supporto/documentazione/Validazione_Sezione_Ponte/ANTHEA_Validazione_Sezione_Ponte_Rev01.docx`.
Gli artefatti del collaudo sono in `supporto/artefatti/ponte_curve_validazione/`.
