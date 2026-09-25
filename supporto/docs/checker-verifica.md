# Esito integrazione Checker — 21 settembre 2026

Configurazione: Windows, .NET 8, Release; DLL locali registrate in
[`lib/Checker/manifest.json`](../../lib/Checker/manifest.json).

## Prove eseguite

| Prova | Esito |
| --- | --- |
| Compilazione `ANTHEA.sln` | 0 errori, 0 avvisi |
| Test mirati `--checker` | 143 controlli superati: 72 Checker/NTC, 18 Excel, 53 estensioni CA |
| Regressione sui casi storici | 462 casi superati, 2 falliti (`palo_storico_0`, `palo_storico_3`) |
| Controlli software complessivi | 1.705 superati, inclusi Checker, Excel ed estensioni CA |
| Confronti numerici storici | 999.103 valori; delta massimo assoluto 0,407289 nel calcolo geotecnico del palo |
| Interfaccia CA WPF | 163 controlli, inclusi menu contour reale, scorrimento finestra ridotta, numeri centrati, cache dei criteri, SLE comuni, scale, taglio automatico, Excel, report e riapertura degli archivi |

Le prove dedicate includono confronto dei risultati dell'adattatore con le API
della DLL, cinque percorsi di resistenza, plastico/elastico, lineare/non lineare,
tagli 2D e proiezioni, assi ruotati/eccentrici, trefoli, benchmark VCA_N_1,
valori analitici a taglio e fessurazione, area efficace rettangolare, controlli
senza esito automatico e riduzione dei limiti per elementi piani sottili.

I due scostamenti della regressione storica sono nelle curve
`drenante_compressione.media` dei pali e non attraversano le DLL Checker/Geometry
aggiornate. I valori attesi non sono stati modificati automaticamente.

Le estensioni verificano anche il collegamento delle nove classi normative,
l'applicazione dei coefficienti personalizzati, vertici e raster tensionali
nativi, materiali custom, input geometrici non validi e precisione dello scambio
Excel. Non attestano la completezza normativa delle classi nazionali.

Le schermate sono state controllate visivamente; i messaggi di taglio e
fessurazione rimangono leggibili nelle righe dei risultati.
Il programma pubblicato è in `app/ANTHEA.exe`, avviato da `Avvia ANTHEA.cmd`.
Gli output grezzi delle rifiniture sono sotto `supporto/artefatti/verifiche_ca_refinements/` e il
rapporto numerico è `supporto/artefatti/verifiche_ca_refinements/regressione.json` (ignorati da Git).

## Aggiornamento automatico, Excel e visualizzazioni

Il pacchetto pubblicato in `app` è stato verificato dopo l'ultima correzione.
I test WPF controllano che la digitazione non modifichi il modello fino al
cambio di focus e che la conferma avvii il ricalcolo senza timer. Coprono anche
le modifiche ravvicinate, la cancellazione di un risultato
superato, gli input ancora modificabili durante il calcolo, il riuso del dominio,
la gestione di azioni non numeriche e il recupero automatico dopo la correzione.
Controllano anche l'assenza dei pulsanti Calcola nelle schede CA, le opzioni
richiudibili, copia/incolla senza modifiche parziali su dati invalidi, selezione
delle forze, trasparenza e otto modalità di contouring. L'importazione viene
provata con sostituzione selettiva delle famiglie e ricalcolo automatico.

Sono verificati gli interruttori grafici indipendenti senza ricalcolo, l'origine
centrata e la scala arrotondata del 2D, filtri e ordinamento senza perdita di
righe, n/φ con precisione conservata, dettagli dei vertici e sincronizzazione
delle staffe fra pannello e Taglio. La normativa DS usa i propri coefficienti:
taglio e fessurazione non vengono impropriamente dichiarati verificati.
Il salvataggio e la riapertura provano anche le nuove opzioni grafiche,
i dati condivisi delle staffe e il percorso Excel.

Le rifiniture aggiungono confronti fra cambio del criterio sul dominio nativo
esistente e costruzione ex novo per tutti i cinque criteri: mesh e dominio
rimangono gli stessi, punti e tassi coincidono. In WPF si verifica anche che
le SLE e l'altro dominio non vengano ricalcolati per una sola modifica del criterio.
Prove geometriche indipendenti coprono bw, d e Asl del wizard rettangolare e
le larghezze minime della T; il modello circolare non viene abilitato implicitamente.
Sono provati condivisione SLE, opzioni nascoste senza trefoli, riepiloghi dei casi
peggiori, zoom continuo, fit con/senza azioni e scala 3D senza ricalcolo.

Il template XLSX incorporato è stato creato e controllato con la skill
Spreadsheets: anteprima renderizzata, unità e segno espliciti, lista delle
famiglie, input vuoti senza combinazioni fittizie. I test del lettore coprono
le sei famiglie, numeri decimali, trazione/compressione/zero, dati mancanti,
unità errate, colonne inattese e formule con/senza risultato memorizzato.
L'esportazione delle azioni è stata nuovamente renderizzata e ispezionata:
mantiene lo stile del template, i nomi delle famiglie e la precisione numerica.
Test aggiuntivi coprono il round trip, un nome che inizia con `=` trattato come
testo e un'esportazione vuota reimportabile.

Il report Word CA è stato controllato seguendo la skill Documents: sezioni
selezionabili, limiti sempre presenti, header di tabella ripetibili, didascalie
solidali con le immagini e ispezione visiva di tutte le 14 pagine dell'esempio
con grafici e delle 7 pagine della variante completa SLE con dettagli.
Il default è l'inviluppo con origine separata degli estremi; i test mirati
verificano segni, governanti diversi per tensioni/fessurazione e risultati
mancanti. Le immagini sono inserite nella sezione di pertinenza; quelle SLE
usano il risultato della combinazione governante, non la selezione dell'utente.
Il renderer previsto non ha trovato LibreOffice nel runtime incluso; non è
stato usato un LibreOffice installato dall'utente. Per il collaudo si è convertito
il solo DOCX di prova in PDF tramite un'istanza Word privata e non visibile,
con apertura in sola lettura, quindi si sono renderizzate e ispezionate le pagine.
ANTHEA produce il DOCX direttamente e non richiede Word per generarlo.

Durante le prove sono stati corretti due casi della nuova presentazione:
normalizzazione dei valori resistenti del CLS (la DLL usa valori negativi
per la compressione) e riepilogo senza azioni verificabili. Non sono stati
cambiati i valori resistenti né i tassi restituiti dalla DLL.

## Import massivi e grafici differiti

Il raster SLE viene campionato al primo utilizzo grafico e memorizzato per
risultato; le verifiche e l'esportazione JSON non lo generano. La mesh grafica
3D viene creata alla prima apertura del dominio o alla richiesta del report.
Le schede nascoste non ricostruiscono grafici e dettagli di selezione; i report
possono richiedere esplicitamente i grafici anche senza aprire le schede.

L'importazione e l'incolla aggiornano le collezioni in blocco, con una notifica
Reset per famiglia. Gli aggiornamenti dei risultati sospendono le notifiche
dei singoli campi ed emettono una sola notifica finale per riga modificata.
I test coprono 10.000 inserimenti, scope annidati, risultati invariati,
importazione SLE/taglio, selezione successiva all'import e assenza di raster
per le schede nascoste. Il test numerico confronta il raster differito con
quello di un'analisi indipendente dopo altre analisi sullo stesso motore.

## Ricalcolo selettivo

La coda mantiene le singole verifiche da aggiornare: 3D/2D per SLU e SLV,
le tre famiglie SLE e il taglio. Le modifiche durante il calcolo annullano
lo snapshot in esecuzione e conservano nella coda le verifiche non completate.
I risultati indipendenti restano disponibili e non vengono ricostruiti.

- Modello e impostazioni SLE condivise: solo le tre famiglie SLE.
- Passo, braccia e opzioni del taglio: solo taglio.
- Diametro staffe: aggiornamento completo, perché modifica la posizione
  delle barre longitudinali in `SezioneCA`.
- Azioni e importazioni: solo le famiglie modificate; SLU/SLV aggiornano
  i rispettivi controlli 3D e 2D conservando i domini compatibili.
- Opzioni dominio: solo il pannello 3D oppure 2D interessato.
- Geometria, materiali, normativa e trefoli: aggiornamento completo.

184 controlli WPF superati, inclusa la conservazione per identità dei
risultati indipendenti e l'accodamento contemporaneo di modifiche SLE/staffe.
Sul file reale da 9.994 righe e rettangolare predefinita: cambio a SLE non
lineare 2,303 s; cambio passo staffe 0,194 s, senza errori di calcolo.
Tempi indicativi della macchina di prova, comprensivi di aggiornamento UI.

## Cosa attestano queste prove

I confronti con la DLL verificano il collegamento, le unità, i segni e le opzioni;
non sono una validazione indipendente dell'intero motore Checker. I confronti
storici riguardano il motore preesistente e gli altri moduli, non l'equivalenza
dei vecchi domini con quelli nuovi. I benchmark analitici coprono i casi indicati,
non tutte le possibili sezioni o situazioni strutturali.

Nessuna certificazione globale: prima dell'uso progettuale occorrono ulteriori
benchmark concordati con il progettista, specialmente per precompressione,
sezioni complesse, fessurazione e taglio combinati. I casi esclusi restano senza
esito automatico. Correzioni, fonti normative e limiti operativi sono documentati
in [calcestruzzo-interfaccia.md](calcestruzzo-interfaccia.md). La copertura delle
classi normative e il lavoro futuro sono in
[normative-calcestruzzo.md](normative-calcestruzzo.md).
