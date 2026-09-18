# Esito integrazione Checker — 18 settembre 2026

Configurazione: Windows, .NET 8, Release; DLL locali registrate in
[`lib/Checker/manifest.json`](../lib/Checker/manifest.json).

## Prove eseguite

| Prova | Esito |
| --- | --- |
| Compilazione `ANTHEA.sln` | 0 errori, 0 avvisi |
| Test mirati `--checker` | 72 controlli Checker/NTC + 18 Excel superati |
| Regressione sui casi storici | 464 casi superati, 0 falliti |
| Controlli software complessivi | 1.528 superati, inclusi Checker ed Excel |
| Confronti numerici storici | 1.025.450 valori; delta massimo assoluto 1,819×10⁻¹² |
| Interfaccia CA WPF | 82 controlli, incluse le cinque schede, ricalcolo automatico, mappe, Excel e copia/incolla |

Le prove dedicate includono confronto dei risultati dell'adattatore con le API
della DLL, cinque percorsi di resistenza, plastico/elastico, lineare/non lineare,
tagli 2D e proiezioni, assi ruotati/eccentrici, trefoli, benchmark VCA_N_1,
valori analitici a taglio e fessurazione, area efficace rettangolare, controlli
senza esito automatico e riduzione dei limiti per elementi piani sottili.

Le schermate sono state controllate visivamente; è stata corretta l'altezza delle
righe dei risultati per non troncare i messaggi di taglio e fessurazione.
Il programma pubblicato è in `app/ANTHEA.exe`, avviato da `Avvia ANTHEA.cmd`.
Gli output grezzi più recenti sono sotto `verifiche_ca_auto/release2/` e il
rapporto numerico è `verifiche_ca_auto/regressione3.json` (ignorati da Git).

## Aggiornamento automatico, Excel e visualizzazioni

Il pacchetto pubblicato in `app` è stato verificato dopo l'ultima correzione.
I test WPF coprono le modifiche ravvicinate, la cancellazione di un risultato
superato, gli input ancora modificabili durante il calcolo, il riuso del dominio,
la gestione di azioni non numeriche e il recupero automatico dopo la correzione.
Controllano anche l'assenza dei pulsanti Calcola nelle schede CA, le opzioni
richiudibili, copia/incolla senza modifiche parziali su dati invalidi, selezione
delle forze, trasparenza e otto modalità di contouring. L'importazione viene
provata con sostituzione selettiva delle famiglie e ricalcolo automatico.

Il template XLSX incorporato è stato creato e controllato con la skill
Spreadsheets: anteprima renderizzata, unità e segno espliciti, lista delle
famiglie, input vuoti senza combinazioni fittizie. I test del lettore coprono
le sei famiglie, numeri decimali, trazione/compressione/zero, dati mancanti,
unità errate, colonne inattese e formule con/senza risultato memorizzato.

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
in [calcestruzzo-interfaccia.md](calcestruzzo-interfaccia.md).
