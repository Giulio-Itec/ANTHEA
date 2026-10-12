# Sezioni — rilascio ANTHEA 1.5.0

SEZ-1, SEZ-2 e SEZ-3a sono nei commit d44ba4d, a64a4c1 e f58abeb. Il dettaglio separato MVVM mostra contorno quotato, parti, materiali, proprietà con provenienza, assi e punti G/O. Selezione e isolamento operano sugli elementi associati. I contorni condivisi guidano sia l’anteprima 2D sia i volumi 3D e il contouring delle aste; conservati fori, rientranze e riferimento di inserimento nelle terne locali.

## Collaudo

Il set V proviene da master già pubblicati di Model 7fd1fe4c e Checker 75e9adaa. Tutte le suite delle librerie: 2778 PASS, soli 17 fallimenti Steel già censiti e due esclusioni native Model. Candidato e set finale hanno le stesse impronte; manifest in lib/Checker/release-set.json. DelaunayMesh invariata e nessun Gmsh distribuito.

Full ANTHEA 20261013-004459-sez-set-v-full: **104 PASS, 0 KNOWN, 0 NEW-FAIL**. Compresi 478 controlli ModelWorkspace, 18 WPF sezioni, 33 WPF generali e tutte le prove degli altri moduli. Confronto delle catture headless precedenti, banco denso e concorrenza superati senza modificare attesi o tolleranze. Le tre suite esterne/prestazionali elencate dal runner non sono eseguite dal full; i test delle librerie sono verificati nel rilascio V.

Geometrie attese indipendenti: rettangolo, scatolare, I e composta I/soletta. Confrontati area, orientamento e coordinate dei vertici effettivamente inviati al renderer, compreso il punto di inserimento in testa della composta. La prova sintetica di 212 triangoli non certifica prestazioni di modelli reali. Il modello MIDAS conservato contiene due sezioni quadrate; la composta è una fixture sintetica esplicitamente distinta.

Evidenze grafiche: supporto/artefatti/ci/20261013-003816-sez-volumes-validation/ModelViewer.Sections.UiChecks. Controllati chiaro, scuro, molto scuro, finestra 850×620, scatolare e composta con contouring. Le dimensioni dell’albero provengono dal medesimo catalogo del dettaglio.

## Guide e limiti

Guide globali Rev39: 76 pagine pratiche e 144 teoriche; revisione precedente conservata con impronte in supporto/SUPERATI/sezioni-rev39-20261013. Tutte le pagine e i quattro PDF degli indici controllati graficamente. La pagina vuota dopo l’indice è eliminata compattando soltanto i due paragrafi vuoti di chiusura del campo Word; conservati stili, parti originali e 337 formule editabili. Registro SEZ-03.

La preview della composta rappresenta la geometria fisica supportata, non una sezione omogeneizzata inventata. Offset sconosciuti, offset di estremità non normalizzati e forme non risolte mantengono l’asse e una spiegazione. SEZ-3b (sezione variabile per fase) e STG-4 (confronto reale stage e geometria attiva) restano aperti. Nessun modello utente è stato modificato.

Si prosegue con F3 sul set V congelato; F4 soltanto dopo i prerequisiti effettivamente chiusi. F5 esclusa dal mandato notturno. La finestra primaria con documento non salvato resta aperta.
