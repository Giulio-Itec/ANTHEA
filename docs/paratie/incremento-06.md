# Incremento 6 — sfilamento geotecnico dei tiranti

Model definisce geometria, indagini e profilo normativo; Geotechnics predice la
resistenza del bulbo e verifica lo sfilamento; Design conserva le azioni del
singolo tirante per combinazione e fase. ANTHEA gestisce dati e risultati,
senza formule geotecniche o coefficienti duplicati.

## Campo collaudato e fonti

Il percorso Bustamante–Doix riutilizza le curve già digitalizzate in Checker,
passando la **pressione limite pressiometrica del terreno pl**. La pressione
d'iniezione pi è distinta e controllata rispetto alla tecnologia IGU/IRS.
Il diametro del bulbo è α volte il diametro di perforazione; la capacità somma
la resistenza laterale dei soli tratti cementati, senza resistenza di punta.
I profili indipendenti devono descrivere lo stesso tirante fisico completo.

Fonti effettivamente consultate: scansioni locali Viggiani pp.392–397,
`supporto/documentazione/riferimenti_micropali/Viggiani_392_393.png` e successive;
NTC 2018 §6.6, tabelle 6.6.I–III, G.U.20/02/2018 pp.198–199, PDF ufficiale
conservato negli artefatti `fonti/ntc2018-cap6.pdf`, pagine 16–17 controllate
visivamente. L'articolo originale Bustamante–Doix è stato censito
bibliograficamente, ma non ottenuto integralmente: non si dichiara verificato.
Formule, coefficienti e fonti sono in
`Checker/docs/metodi/sfilamento-tiranti-paratie.md`.

NTC: storia A1+M1 esplicita e indipendente; R3 con γR=1.1 temporaneo o 1.2
permanente. Coefficienti di correlazione distinti per indagini calcolate e
prove preliminari, applicati alla media e al minimo delle capacità complete.
Non si usano i coefficienti dei pali e non si rifattorizzano reazioni non lineari.

L'implementazione empirica corrente richiede ricoprimento minimo di 5 m da
tutte le superfici libere e lunghezza cementata di almeno 4 m: **limiti scelti
per questo percorso**, non limiti universali attribuiti al metodo originale.
Confinamento, zona stabile, isolamento, esecuzione e validità nelle fasi devono
essere documentati. Curve fuori campo non vengono estrapolate.

Senza conferma sperimentale in sito una capacità numericamente sufficiente
resta Non valutato, pur conservando il valore preliminare. Una capacità
insufficiente resta Non soddisfatto. Dati incompleti, prove duplicate, profilo
normativo non disponibile o storia incompleta non producono verifiche soddisfatte.
Sono ancora aperti: dimensionamento automatico della lunghezza, bulbi prossimi
alla superficie o in gruppo, aderenza armatura–miscela, testa, durabilità,
collaudo e geometrie avanzate. PAR-11 resta in corso.

## Applicazione e documenti

Schema 6: tre tabelle per definizione del tirante, stratigrafie lungo l'asse e
prove preliminari. La migrazione 5→6 mantiene le nuove verifiche disattivate.
Vista WPF con esiti per fase, coefficienti, campioni e tratti, pressioni pl/pi
distinte; CSV con 29 colonne e provenienza. Persistenza, invalidazione e report
leggono lo stesso risultato. Le indagini identiche fra fasi sono riportate una
volta nel PDF, con richiamo esplicito; il CSV conserva tutte le righe per fase.

## Evidenze eseguite

Artefatti in `supporto/artefatti/paratie/` e nel runner `supporto/artefatti/ci/`:

- Attesi Python indipendenti creati prima del kernel, senza sovrascrittura:
  `Checker/tools/ParatieAnchorExpected.py` e fixture `anchor-expected.json`.
  Bulbo a due strati: 471238.898038469 + 814300.815810475 =
  1285539.7138489434 N. Controlli di conversione in ANTHEA entro 1e-9 kN,
  azioni entro 1e-8 kN; formule chiuse e arrotondamenti double.
- La prima compilazione ha intercettato API non disponibili nel target
  netstandard; sostituite senza modificare framework. `anchor-kernel-03`:
  50 test Geotechnics PASS; `anchor-design-01`: due test Design PASS.
- `candidate-13`: 3128 test PASS, soltanto i 17 fallimenti storici Steel ammessi;
  API compatibili. Model `0fcdf601`, Checker `03c5d6a2`; Model 5.11.0.0,
  Geotechnics 0.9.0.0, Design 0.24.0.0. Versioni e SHA-256 di tutti gli
  assembly nel manifest del set. Staging integrale `snapshot-13`;
  `lib/Checker` invariata, nessun rilascio ufficiale o push.
- `anchor-app-01`: 124 controlli PASS. `anchor-app-02`: 128 controlli PASS,
  aggiunti i casi di prova incompleta/duplicata. Incluse migrazione,
  riapertura, isolamento, annullamento, unità, coefficienti, casi non valutati,
  origine delle azioni, rimozione sostegno, CSV e coerenza del report.
- Runner `ci/20261019-114029-paratie-inc06-ui1`: build Release/UiTests,
  architettura e 27 controlli WPF PASS. Tre nuove schermate controllate:
  tema chiaro, scuro e predizione senza conferma sperimentale.
- PDF nativo `anchor-app-02/paratie/sfilamento-tiranti.pdf`: tre pagine A4,
  tutte renderizzate e controllate in `anchor-report-render-02`. Eliminata
  la quarta pagina quasi vuota del primo rendering, senza ricalcolare o
  modificare risultati. Nessuna attestazione di rendering Word.

## Rilievo sul percorso storico dei micropali

L'audit ha individuato che `AxialPileCapacity` passa `pile.Pressure` (iniezione)
a `BustamanteDoix.Segments`, che la usa come pressione limite del terreno.
La fonte distingue le due grandezze. Il nuovo percorso tiranti le tiene
separate e non chiama `Segments`. **Il percorso storico non è corretto da
questo incremento**: richiede cattura preventiva, dati pressiometrici espliciti,
migrazione dei documenti e confronto motivato. Il rilievo resta aperto e non
viene nascosto né risolto cambiando gli attesi storici.

Regressione standard `ci/20261019-114831-paratie-inc06-standard` sul commit
ANTHEA `03da426c` e `snapshot-13`: **74 PASS, 0 KNOWN, 0 NEW-FAIL,
0 BLOCKED**, sorgenti ANTHEA invariati durante tutta la corsa. Presentation:
2114 controlli, 329.7 s; ConcreteLibraryAdapter: 314.9 s. Le sole tre suite
non eseguite sono le esclusioni infrastrutturali dichiarate dal runner:
ElasticPile.Checks e BridgeValidationCurrent (suite GPC), ElasticPile.Performance.

Guide globali, rilascio e restante matrice non sono dichiarati conclusi da
queste evidenze.
