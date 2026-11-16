# ANTHEA 1.19.1 - note di rilascio

Note del 16 novembre 2026, predisposte per la distribuzione dopo l'approvazione dell'utente.
La 1.19.1 sostituisce il set di librerie GPC AV della 1.19.0 con il set ufficiale **AW**. Comprende
anche l'avviso sui Materiali più vicini nel controllo del copriferro, la protezione degli strati del
terreno condivisi quando cambia la suddivisione, le correzioni dei blocchi con le schede senza dati e
le guide Rev72. Non aggiunge comandi all'interfaccia e non introduce un nuovo formato dei documenti:
l'incremento resta di tipo patch.

Il testo è anche l'annotazione prevista del tag `v1.19.1`. Il verbale tecnico è in
[installer-1.19.1.md](installer-1.19.1.md).

## Librerie e comportamento

ANTHEA mantiene un insieme coerente di quattordici librerie. Lo snapshot è quello ufficiale
`gpc-release-20261114-aw`, da Model `c2abf222`, Checker `9c71ca65`, Geometry `6734eee1` e
Utilities `e57b440b`, con manifest non candidato e provenienza dichiarata da origin/master.

- GPCModel 5.51.0.1, GPCModelData 0.3.0.31, GPC.Model.Persistence 3.15.0.3, GPC.Archive 1.2.0.4 e GPC.Converter 2.22.0.3.
- GPCChecker.Concrete 0.37.0.2, GPCChecker.Steel 2.134.0.1, GPCChecker.CompositeBridge 1.31.0.2, GPCChecker.Geotechnics 0.28.0.1, GPCChecker.Masonry 0.10.0.36 e GPC.Design 0.45.0.10.
- GPCUtilities 3.0.0.0, GPCGeometry 2.3.0.0 e DelaunayMesh 2.0.0.14 invariati.

Il cambio del set non attiva nell'importatore ANTHEA le nuove opzioni del writer Straus7
destinate alla compatibilità di BridGHe. Non aggiunge GPCModelChecker al programma e non
include i candidati successivi ad AW per le sezioni T/C/Angle.

Nei sei confronti storici ancora differenti, gli scarti osservati con AW erano già presenti
con AV. Non sono quindi presentati come nuove correzioni introdotte dalla 1.19.1. Questa
prova non dimostra, da sola, che ogni modello dell'utente dia risultati identici.

## Copriferro e dati condivisi nel progetto

- **Materiali più vicini e copriferro.** Il controllo del copriferro usa, come prima, i Materiali del livello più alto che ne contiene, dal progetto alla sezione del foglio. Dopo ogni controllo un avviso segnala i Materiali di un livello più vicino al foglio che darebbero un esito o un minimo diverso. L'avviso nomina il foglio, i due Materiali e i loro livelli e riporta minimo ed esito di ciascuno; se il Materiali più vicino non è valutabile, ne indica il motivo. Materiali con lo stesso minimo, al centesimo di mm, e lo stesso esito non danno avviso. L'esito del controllo resta quello dei Materiali del livello più alto: **RISPETTATO** non vale automaticamente per un requisito locale più severo. L'avviso compare nella riga di stato del foglio, nel confronto, nel riepilogo e negli avvisi del report, e conta fra le segnalazioni del controllo prima di generare il report. Dopo la rinomina di una sezione o di un Materiali l'avviso usa il nome nuovo.
- **Strati del terreno condivisi tra i fogli.** Togliere o inserire uno strato in un foglio e confermare non sposta più gli strati degli altri fogli. Nelle versioni precedenti gli strati si copiavano per posizione: in un foglio di una sezione con sottosezioni, togliendo il primo strato i fogli sotto ricevevano, senza domanda, i valori dello strato successivo, spessori compresi, e tenevano il loro ultimo strato (per esempio 2 m coesivo · 5 m · 10 m diventava 5 m · 10 m · 10 m). Ora un sondaggio di cui cambia il numero di strati non si copia negli altri fogli; se cambia il numero di sondaggi, non si copia lo strato di nessun sondaggio. Un sondaggio si copia solo nei fogli che hanno lo stesso numero di strati. Gli altri fogli restano come sono e la differenza si vede nella riga sopra il foglio e nel confronto della sezione; per allinearli togliere o aggiungere lo stesso strato anche in quei fogli. Le modifiche che non cambiano il numero di strati (spessori, tipologie, parametri) si condividono come prima.
- **Limiti degli strati condivisi.** «Uniforma a questo», «Collega stratigrafie», la copia degli strati mancanti, i fogli nuovi e gli spostamenti seguono le regole di prima. I sondaggi e gli strati si corrispondono ancora per posizione: togliere uno strato e aggiungerne un altro nella stessa conferma lascia lo stesso numero di strati e li sposta ancora. Togliere l'ultimo strato non lascia conflitti visibili, perché gli strati rimasti sono uguali, e i fogli sotto tengono uno strato in più: controllare i profili. Se nella stessa conferma cambia il numero di strati e si modifica uno strato esistente, anche la modifica resta nel solo foglio e si vede come conflitto. Nei progetti in cui è stato tolto uno strato con una versione precedente, controllare il confronto delle sezioni con pali e fondazioni: gli strati dei fogli sotto possono essere già stati spostati.

## Schede senza dati nei progetti

Un foglio può restare senza dati quando il progetto si apre con **Progetto aperto con fogli non leggibili**,
per esempio per un riferimento non risolto. Nelle versioni precedenti alcune operazioni su un progetto con un
foglio così si fermavano con «Operazione non completata» e un messaggio tecnico. Ora il foglio senza dati o in
sola lettura resta come nel file e le altre schede funzionano.

- **Copriferro.** Una sezione c.a. o un palo orizzontale senza dati dà l'avviso «copriferro non verificabile — dati della scheda mancanti o non validi», come i Materiali senza dati; le altre schede e gli altri Materiali mantengono il proprio esito.
- **Terreni.** Due fogli che condividono i terreni, uno dei quali senza dati, non bloccano più avvisi, coerenza e report: la coppia non si confronta.
- **Muri.** Un muro senza dati non si confronta con le altre schede. Inserire una scheda nella stessa sezione non lo riempie.
- **Report.** Un foglio senza dati non porta valori nel report, nemmeno i predefiniti del modulo.
- **Comandi del confronto.** «Uniforma a questo» sullo strato mancante salta il foglio senza dati o in sola lettura e lo elenca con «non leggibile, non viene aggiornato». Il collegamento delle stratigrafie e la scelta della tipologia del terreno lo rifiutano con un messaggio, per esempio «… non è leggibile: collegamento non eseguito. Nessun dato è stato sostituito.». Nel confronto il pulsante di quel foglio diventa **Non leggibile**, disattivato.
- **Dati condivisi.** «Aggiorna tutti i fogli collegati» non scrive più i dati comuni in un foglio senza dati (prima lo riempiva con i predefiniti del modulo) né in un foglio in sola lettura. La domanda **Dati condivisi della sezione** li nomina nella riga «Esclusi perché non leggibili, restano come nel file: …». Un foglio senza dati non può essere l'origine dell'aggiornamento.
- **Schede nuove.** Una scheda nuova inserita in una sezione eredita i valori comuni soltanto dai fogli leggibili: un vicino senza dati o in sola lettura non lascia più il campo non risolto (per esempio il diametro dei pali).

Limiti: la riga degli esclusi compare solo quando la domanda si pone; con l'aggiornamento automatico verso le
sottosezioni i fogli esclusi restano come nel file senza elenco. La coerenza e la tabella dei conflitti
confrontano ancora un foglio in sola lettura, diverso da un muro, con i predefiniti del suo modulo, come prima.

## Salvataggio e compatibilità

Restano le regole della 1.19.0. I progetti salvati con sezioni c.a. compatte non si aprono
con la 1.18.0 o con versioni anteriori. La compatibilità dell'archivio dei risultati
`.anthea-risultati` non va estesa automaticamente a tutti i documenti di progetto.

Conservare gli originali e i relativi file dei risultati prima di ricalcolare e salvare
con la nuova distribuzione. Un risultato mancante o non leggibile richiede recupero
o ricalcolo; il salvataggio del progetto non equivale al recupero del calcolo.

## Guide

Guide pratica e teorica **Rev72**, in Word, PDF e nella Wiki. Rispetto alla Rev71 descrivono il
comportamento attuale dei metodi già disponibili e i loro limiti: importazione dei modelli e dei carichi,
planarità delle piastre e classe del calcestruzzo, combinazioni, archivio dei risultati, template dei
report, formato delle lunghezze nei report, Hansen con carico inclinato, fogli delle Azioni e della
Reologia, pali verticali e verifiche del modello. Descrivono inoltre la scelta dei Materiali nel controllo
del copriferro, il nuovo avviso sui Materiali più vicini e la protezione degli strati condivisi. È corretto
l'esempio numerico di Hansen non drenato con carico orizzontale molto piccolo. Nessuna formula nuova.
Restano distribuiti l'indice e l'articolo sul palo orizzontale in terreno stratificato, che non cambia.

## Prove

- **Set AW (compito 191).** La full ha eseguito 130 suite, comprese 73 UI: 129 PASS e un FAIL dovuto all'ora stampata in due PDF generati a cavallo del cambio di minuto. La correzione del solo test materiali, inclusa anche in questa versione, ripete al massimo due volte la coppia di esportazioni con lo stesso confronto finale; le verifiche mirate successive sono PASS. Non modifica il calcolo.
- **Avviso sul copriferro, strati e guide Rev72.** Sul codice con l'avviso e la correzione degli strati (`68192e18`, guide Rev72 in `69e9f2d2`) sono state eseguite, in corse seriali, tutte le 131 suite del profilo full, comprese 73 UI; la suite nuova è GuideRev72.Checks, con sei esempi Hansen verificati con espressioni indipendenti. Esito: 130 PASS e il confronto F3 con le sole differenze editoriali della Wiki Rev72. Le 1086 transizioni sono state approvate dall'utente il 16/11/2026 e applicate: `ui/f3-equivalence` PASS.
- **Confronti F4.** Sei PASS e i sei FAIL storici, con oggetti di confronto identici al compito 191 e quindi alle evidenze AV/AW già classificate. Su decisione dell'utente del 16/11/2026 i sei sono registrati come differenze dichiarate in `build/known-failures.json`, con il primo messaggio e l'impronta SHA-256 dell'intero confronto: la corsa finale dà 6 PASS e 6 noti, e ogni scarto in più rimette la suite in rosso. Attesi e tolleranze non sono modificati.
- **Schede senza dati.** Dopo l'integrazione delle correzioni sono state ripetute le suite dei progetti e dei report: nove build e dieci suite PASS (Architecture, Application, Presentation, Projects, Reports, FoundationWorkspace, ConcreteLibraryAdapter nelle tre parti e l'audit dei progetti), poi dieci suite UI dei progetti PASS, fra cui la condivisione e la gerarchia. Il confronto F3 dà le stesse 1086 differenze della Wiki, nessuna in più. Le correzioni toccano soltanto il codice dei progetti, dei report e della finestra dei dati condivisi: per questo non è stata ripetuta l'intera full. La prova del contrasto e il collaudo dell'installer sono nel verbale.
- **Straus7 nativo.** Nella full la suite di accesso nativo verificava soltanto la disattivazione delle prove. Il 16/11/2026 la prova nativa è stata eseguita sul codice della 1.19.1, con Straus7 R31 installato e il processo di lettura reale: `Straus7Native.Checks` PASS con 29 controlli nativi sul modello versionato degli elementi (nodi, aste, piastre, casi, reazioni, equilibrio e combinazione ricostruita da GPC) e `ModelViewer.Importers.UiChecks` PASS con 17 controlli, fra cui l'importazione nel visualizzatore attraverso il processo reale. Non è stata eseguita la lettura MIDAS dal vivo.

## Limiti noti

I confronti F4 relativi agli ultimi adattatori, ai dati residui, ai contratti di ingresso, al
progetto della sezione, ai risultati dei pali e all'adattatore strutturale dei muri restano
qualificati come prima: non sono dichiarati superati. Su decisione dell'utente del 16/11/2026
sono registrati come differenze dichiarate in `build/known-failures.json`, con l'impronta del
confronto.

Una parte delle differenze è ricondotta a modifiche precedenti ad AV: criterio di taglio
MC2010, default DIN, interassi delle armature, taglio dei pali e dettagli delle armature
dei muri. La ricostruzione della storia del codice non costituisce una nuova verifica
dei testi normativi; per il default DIN la fonte normativa originale resta da verificare.

Altri scarti, relativi alla resistenza e ai rapporti delle sezioni e dei muri e ai momenti
resistenti dei pali, richiedono ancora controlli indipendenti di equilibrio, convergenza
e discretizzazione. Non vengono accettati aggiornando automaticamente i riferimenti
o allargando le tolleranze. Le variazioni dovute allo sviluppo dell'armatura dei pali
restano distinte dai residui numerici non spiegati.

Questa preparazione non comprende la lettura MIDAS dal vivo. La decisione di passare ad AW
non autorizza a sostituire gli attesi.

## Aggiornamento

Usare l'installer completo della distribuzione approvata e chiudere ANTHEA prima di avviarlo.
Conservare progetti, modelli e report fuori dalla cartella del programma. Non aggiornare
l'installazione sostituendo singole DLL.

## Provenienza delle verifiche

I compiti 183 e 188 documentano il confronto AV/AW e la classificazione delle differenze; la full e i
confronti F4 sul set AW sono nel rapporto del compito 191, `anthea-aw-191.md`. L'avviso sul copriferro e la
correzione degli strati vengono dal rapporto `anthea-p10-avviso-materiali-20261116.md`, le correzioni delle
schede senza dati dal rapporto `copriferro-nre-20261115.md` e dal registro delle differenze; le guide Rev72 dai
compiti 205, 207 e 208 ([guide-rev72-205.md](guide-rev72-205.md), [guide-rev72-208.md](guide-rev72-208.md)).
La costruzione e il collaudo del pacchetto sono nel verbale [installer-1.19.1.md](installer-1.19.1.md). Le
note non attribuiscono alla 1.19.1 le funzioni già pubblicate nella 1.19.0.
