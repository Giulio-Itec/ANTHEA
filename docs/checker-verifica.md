# Esito integrazione Checker — 21 settembre 2026

Configurazione: Windows, .NET 8, Release; DLL locali registrate in
[`lib/Checker/manifest.json`](../lib/Checker/manifest.json).

## Prove eseguite

| Prova | Esito |
| --- | --- |
| Compilazione `ANTHEA.sln` | 0 errori, 0 avvisi |
| Test mirati `--checker` | 143 controlli superati: 72 Checker/NTC, 18 Excel, 53 estensioni CA |
| Regressione sui casi storici | 464 casi superati, 0 falliti |
| Controlli software complessivi | 1.581 superati, inclusi Checker, Excel ed estensioni CA |
| Confronti numerici storici | 1.025.450 valori; delta massimo assoluto 1,819×10⁻¹² |
| Interfaccia CA WPF | 161 controlli, inclusi menu contour reale, scorrimento finestra ridotta, numeri centrati, cache dei criteri, SLE comuni, scale, taglio automatico, Excel, report e riapertura degli archivi |

Le prove dedicate includono confronto dei risultati dell'adattatore con le API
della DLL, cinque percorsi di resistenza, plastico/elastico, lineare/non lineare,
tagli 2D e proiezioni, assi ruotati/eccentrici, trefoli, benchmark VCA_N_1,
valori analitici a taglio e fessurazione, area efficace rettangolare, controlli
senza esito automatico e riduzione dei limiti per elementi piani sottili.

Le estensioni verificano anche il collegamento delle nove classi normative,
l'applicazione dei coefficienti personalizzati, vertici e raster tensionali
nativi, materiali custom, input geometrici non validi e precisione dello scambio
Excel. Non attestano la completezza normativa delle classi nazionali.

Le schermate sono state controllate visivamente; i messaggi di taglio e
fessurazione rimangono leggibili nelle righe dei risultati.
Il programma pubblicato è in `app/ANTHEA.exe`, avviato da `Avvia ANTHEA.cmd`.
Gli output grezzi delle rifiniture sono sotto `verifiche_ca_refinements/` e il
rapporto numerico è `verifiche_ca_refinements/regressione.json` (ignorati da Git).

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
