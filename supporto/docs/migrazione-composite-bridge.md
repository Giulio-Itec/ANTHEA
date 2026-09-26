# Migrazione del calcolo ponte in Checker — 25 settembre 2026

Il progetto [GPCChecker.CompositeBridge](../../../Checker/GPCChecker.CompositeBridge/README.md)
contiene ora il motore numerico. È referenziato da ANTHEA tramite
`lib/Checker/GPCChecker.CompositeBridge.dll`; rimossi i collegamenti diretti ai
sorgenti del progetto Steel.

In `X.Calculations/BridgeSection*` (dal riordino del 26 settembre; precedentemente `X.Core`) rimangono cataloghi, predefiniti, lettura degli archivi,
conversione in `HBridgeInput`, esportazione e deleghe alla libreria. La vista
gestisce inserimento, aggiornamento, formattazione e disegno dei risultati.
Il costruttore del solver condivide il medesimo lock con il modulo cemento armato.

Il metodo numerico precedente è conservato. Le 8 istantanee di regressione
in Checker sono state acquisite **prima** dello spostamento e confrontano tutti
i campi esportati, incluse le verifiche accessorie. Nessuna correzione dei
difetti già documentati è inclusa in questo intervento.

La libreria separa il motore iterativo, le proprietà, l'omogeneizzazione, le
riduzioni dei pannelli e l'accesso al solver dall'adattatore H. È provato anche
un modello N–Mx a due anime; il calcolo completo dei cassoni resta da sviluppare.
Il secondo metodo per fasi non è stato implementato.

I sorgenti dei test numerici sono nei progetti Checker
`GPCChecker.Test.CompositeBridge` e `GPCChecker.Test.BridgeAudit`.
Log, TRX e schermate di questa migrazione sono in
`supporto/artefatti/ponte_migrazione_checker/` (non versionati).

## Esiti

- Checker ordinario: 267 test superati, di cui 8 confronti completi prima/dopo.
- API autonoma CompositeBridge: 28 test superati sia con le DLL dell'app sia
  con le dipendenze sorgenti. Non sono 56 casi distinti.
- ANTHEA ponte: 117 controlli superati; UI: 381 controlli più lo scenario completo
  di apertura, archivio, esportazioni JSON/Word e relazione di progetto.
- Modulo cemento armato: 102 controlli Checker, 18 Excel, 174 estensioni,
  27 dati/riepiloghi, tutti superati.
- Audit completo: 267 passati, 5 falliti attesi, 10 già ignorati. I fallimenti
  sono quattro casi del costruttore H nullo sulle DLL Model precedenti alla
  correzione e il metadato di inerzia a carico nullo. Non modificati.
- Build ANTHEA e libreria sullo snapshot: nessun errore o avviso. La build
  sorgente autonoma passa con i due avvisi già presenti in Checker.Concrete
  sui riferimenti UnsafeEx e sull'architettura GMsh.Net.
