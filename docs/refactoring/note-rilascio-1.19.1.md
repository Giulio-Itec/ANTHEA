# ANTHEA 1.19.1 - note di rilascio

Note del 15 novembre 2026, preparate per la distribuzione dopo F8 e le decisioni dell'utente.
La 1.19.1 sostituisce il set di librerie GPC AV della 1.19.0 con il set ufficiale **AW**.
Il cambiamento riguarda le librerie di calcolo e dei dati: non aggiunge comandi all'interfaccia
e non introduce un nuovo formato dei documenti. Per questo l'incremento è di tipo patch.

Il testo è anche l'annotazione prevista del tag `v1.19.1`. Il verbale tecnico è in
[installer-1.19.1.md](installer-1.19.1.md).

## Librerie e comportamento

ANTHEA mantiene un insieme coerente di quattordici librerie. Lo snapshot è quello ufficiale
`gpc-release-20261114-aw`, da Model `c2abf222`, Checker `9c71ca65`, Geometry `6734eee1` e
Utilities `e57b440b`, con manifest non candidato e provenienza dichiarata da origin/master.

- GPCModel 5.51.0.1, GPCModelData 0.3.0.31, GPC.Model.Persistence 3.15.0.3,
  GPC.Archive 1.2.0.4 e GPC.Converter 2.22.0.3.
- GPCChecker.Concrete 0.37.0.2, GPCChecker.Steel 2.134.0.1,
  GPCChecker.CompositeBridge 1.31.0.2, GPCChecker.Geotechnics 0.28.0.1,
  GPCChecker.Masonry 0.10.0.36 e GPC.Design 0.45.0.10.
- GPCUtilities 3.0.0.0, GPCGeometry 2.3.0.0 e DelaunayMesh 2.0.0.14 invariati.

Il cambio del set non attiva nell'importatore ANTHEA le nuove opzioni del writer Straus7
destinate alla compatibilità di BridGHe. Non aggiunge GPCModelChecker al programma e non
include i candidati successivi ad AW per le sezioni T/C/Angle.

Nei sei confronti storici ancora differenti, gli scarti osservati con AW erano già presenti
con AV. Non sono quindi presentati come nuove correzioni introdotte dalla 1.19.1. Questa
prova non dimostra, da sola, che ogni modello dell'utente dia risultati identici.

## Salvataggio e compatibilità

Restano le regole della 1.19.0. I progetti salvati con sezioni c.a. compatte non si aprono
con la 1.18.0 o con versioni anteriori. La compatibilità dell'archivio dei risultati
`.anthea-risultati` non va estesa automaticamente a tutti i documenti di progetto.

Conservare gli originali e i relativi file dei risultati prima di ricalcolare e salvare
con la nuova distribuzione. Un risultato mancante o non leggibile richiede recupero
o ricalcolo; il salvataggio del progetto non equivale al recupero del calcolo.

## Guide

Guide pratica e teorica **Rev71**, come nella 1.19.0. Restano distribuiti l'indice e
l'articolo sul palo orizzontale in terreno stratificato. Questo allineamento delle librerie
non introduce nuove procedure operative.

## Prove e correzioni dei test

La full ANTHEA sul set AW ha eseguito tutte le **130 suite**, comprese le **73 UI**:
129 PASS e un FAIL dovuto all'ora stampata in due PDF generati a cavallo del cambio di minuto.
La correzione riguarda il solo test materiali: la coppia di esportazioni può essere
ripetuta al massimo due volte, mantenendo lo stesso confronto finale. La verifica mirata
successiva ha superato due build e due suite, con 229 controlli materiali su 15 fixture.
Non si dichiara una seconda full completamente verde, né una correzione dei calcoli.

I dodici confronti F4 sono conclusi: **sei PASS e sei FAIL storici**. Gli oggetti di confronto
dei sei FAIL coincidono con quelli AW già confrontati con AV. Gli attesi e le tolleranze
non sono stati modificati. Il test di conservazione degli artefatti CI usa il TEMP
configurato; anche le fixture dei collaudi installer usano cartelle temporanee dedicate sotto CX.
Queste correzioni delle prove non cambiano il comportamento del programma distribuito.

Collaudi installer: safety 26 PASS ed esempi 206 PASS. Il collaudo completo delle guide
resta FAIL nella destinazione temporanea sotto CX: la rimozione dei PDF non termina dopo
la verifica degli hash. Il problema va chiarito prima della distribuzione; il controllo
di disinstallazione e i confronti dei contenuti restano attivi.

## Limiti noti

Restano aperti i confronti F4 relativi agli ultimi adattatori, ai dati residui, ai contratti
di ingresso, al progetto della sezione, ai risultati dei pali e all'adattatore strutturale
dei muri. Non sono dichiarati superati.

Una parte delle differenze è ricondotta a modifiche precedenti ad AV: criterio di taglio
MC2010, default DIN, interassi delle armature, taglio dei pali e dettagli delle armature
dei muri. La ricostruzione della storia del codice non costituisce una nuova verifica
dei testi normativi; per il default DIN la fonte normativa originale resta da verificare.

Altri scarti, relativi alla resistenza e ai rapporti delle sezioni e dei muri e ai momenti
resistenti dei pali, richiedono ancora controlli indipendenti di equilibrio, convergenza
e discretizzazione. Non vengono accettati aggiornando automaticamente i riferimenti
o allargando le tolleranze. Le variazioni dovute allo sviluppo dell'armatura dei pali
restano distinte dai residui numerici non spiegati.

Questa preparazione non comprende nuove prove native Straus7 o MIDAS. La gestione formale
dei sei FAIL nel gate di rilascio e le eventuali prove native restano esplicite nel verbale;
la decisione di passare ad AW non autorizza a sostituire gli attesi.

## Aggiornamento

Usare l'installer completo della distribuzione approvata e chiudere ANTHEA prima di avviarlo.
Conservare progetti, modelli e report fuori dalla cartella del programma. Non aggiornare
l'installazione sostituendo singole DLL.

## Provenienza delle verifiche

I compiti 183 e 188 documentano il confronto AV/AW e la classificazione delle differenze.
La full e i confronti F4 completati sono nel rapporto del compito 191, `anthea-aw-191.md`;
la costruzione e il collaudo del pacchetto sono documentati nel verbale
[installer-1.19.1.md](installer-1.19.1.md). Le note derivano dalla bozza G-201 aggiornata
con questi esiti, senza attribuire alla 1.19.1 le funzioni già pubblicate nella 1.19.0.
