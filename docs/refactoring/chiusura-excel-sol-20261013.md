# Chiusura EXCEL-SOL — 13 ottobre 2026

Attività richiesta in coda a F3, conclusa sul ramo codex/section-actions-excel. F4 e F5 non iniziate. L’integrazione su main e il push sono già autorizzati dall’utente e seguono questo verbale.

## Risultato

La finestra «Sollecitazioni…» del foglio Sezione CA gestisce un solo elenco: Elemento facoltativo, Nome combo, Stato limite, N, Vx, Vy, T, Mx, My. Modifica, aggiunta, duplicazione, cancellazione, template Excel, importazione con sostituzione o aggiunta ed esportazione. Le modifiche diventano effettive soltanto con Applica; un errore non lascia importazioni parziali.

SLU e SLV alimentano domini e taglio/torsione dalla stessa riga; Rara, Frequente e Quasi permanente alimentano i controlli SLE pertinenti. Il criterio resistente Plastico/Elastico è scelto nella verifica, indipendentemente dallo stato limite. Componenti e riferimenti elemento sono conservati nelle viste e nei report.

Elemento identifica la provenienza delle azioni: tutte le righe sono verificate sulla sezione del foglio corrente. Non seleziona altre geometrie automaticamente. Le sei componenti sono richieste; gli zeri devono essere espliciti, senza riapplicare coefficienti alle azioni. Le formule Excel sono lette tramite i valori salvati, con segnalazione; non vengono eseguite.

I documenti precedenti mantengono il comportamento originale fino alla conversione esplicita. I gruppi precedenti Plastico/Elastico e Taglio non permettono di dedurre lo stato limite: l’editor lo richiede e segnala le componenti da completare. Le righe omonime non vengono fuse. I nuovi fogli partono senza combinazioni di esempio.

## Architettura e commit

- 504de76e: contratto unico, instradamento, criterio resistente separato.
- 9e1ac2ca: Excel, editor MVVM, collegamento delle viste e report.
- 6903923d: sfondo e spaziatura della finestra nelle catture.
- b6fac621: guide Rev41, archivio Rev40, Wiki e versione 1.7.0.
- 93a1eca9: continuità del percorso Excel e avviso di migrazione.
- 68330851: posizionamento corretto delle due voci nel registro delle differenze.

La vista WPF contiene presentazione e dialoghi file. Stato, validazione e instradamento sono in Application senza WPF. I calcoli restano nel motore e nelle librerie GPC; nessuna nuova formula, mesh, DLL o copia di un algoritmo. La proiezione per gli utilizzatori del vecchio contratto taglio è generata soltanto in una copia privata, mai salvata come seconda fonte delle azioni.

## Collaudo

| Corsa | Esito |
| --- | --- |
| 20261013-142103-excel-sol-before | Cattura preventiva: Application e ca-data PASS |
| 20261013-142457-excel-sol-contract | Contratto e instradamento PASS |
| 20261013-143704-excel-sol-ui | Excel/editor e prove grafiche PASS |
| 20261013-144104-excel-sol-ready | 1229 controlli Application e regressioni grafiche PASS |
| 20261013-144358-excel-sol-window-final | Finestra corretta e catture PASS |
| 20261013-145510-excel-sol-final | Profilo standard: 66 PASS, un errore documentale nel registro |
| 20261013-150258-excel-sol-final-ui | Quattro suite WPF PASS: editor, smoke generale, estensioni e funzionalità CA |
| 20261013-150557-excel-sol-registry | Ripresa architettura: 239 controlli PASS |

La prima corsa standard resta conservata. Il controllo fallito ha correttamente rilevato che EXCEL-SOL-01/02 erano inserite nella lista degli stati del registro anziché nella lista delle voci. Il fix sposta le stesse due voci; non modifica test, attesi, soglie o tolleranze. Non sono aggiunte eccezioni a known-failures.json.

Copertura finale: 71 voci con esito PASS, tra standard e quattro suite grafiche. Non è presentata come una nuova corsa full. Evidenza consolidata in supporto/artefatti/excel-sol/final-validation-coverage.json, con SHA-256 delle sintesi e provenienza di ogni risultato.

L’ultima suite Application riporta 1226 controlli, contro 1229 della corsa precedente: il banco dei contratti CA conta anche le notifiche di avanzamento della ricerca armature, variabili con i tempi. Sono invariati 32 candidati e 12 punti della curva; ciascuna notifica osservata è confrontata esattamente. Le nuove prove Excel e criterio sono sempre eseguite. Banco adattatore: 158022 controlli taglio/torsione, 673640 SLE, ulteriori 30 e 583; gli attesi indipendenti sono invariati. Nessun file versionato alterato dalle suite.

Restano fuori dal runner le tre prove esterne già dichiarate: ElasticPile.Checks e BridgeValidationCurrent appartengono al treno delle librerie; ElasticPile.Performance è un banco prestazionale separato. Le librerie non cambiano in questa attività.

## Documentazione e pacchetto

ANTHEA 1.7.0 incrementa il secondo componente per la nuova funzione. Guide Rev41: pratica 78 pagine, teoria 144, 337 formule e 26 parti ITEC conservate per documento. Rendering Word controllato sulle 222 pagine; copie precedenti conservate con impronte, PDF e indici aggiornati. Wiki: 71 capitoli.

Installer self-contained win-x64 costruito dal commit pulito e pubblicato 68330851687f0650b1588de6b4b51edab6d35c68. ProductVersion: 1.7.0+68330851687f0650b1588de6b4b51edab6d35c68.

Percorso: supporto/artefatti/rilascio-1.7.0/ANTHEA-1.7.0-Setup-x64.exe. Dimensione: 62673999 byte. SHA-256:

8BD97D7B98B61239210D0AEEFAB2F01C5A3439B64D2AAF265E9D664ECA23A8D0

Dieci DLL corrispondenti al manifest, nessun Gmsh/UnsafeEx e nessun codice di prova negli assembly pubblicati. Manifest del rilascio e controlli runtime conservati negli artefatti. Il setup non viene installato e il programma dell’utente non viene chiuso o riavviato.

STG-4, SEZ-3b e le altre attività precedentemente aperte non sono dichiarate chiuse da EXCEL-SOL. F4 resta ferma.
