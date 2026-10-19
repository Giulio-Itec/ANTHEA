# Paratie — incremento 13, 19 ottobre 2026

Il foglio gestisce più storie assegnate, con calcolo, verifiche e filtrazione
indipendenti, inviluppi comuni e ricerca dell'infissione su tutti i casi.
Non genera combinazioni normative e non somma risposte non lineari.
La richiesta di una prima candidata testabile anticipa la consegna locale,
senza escludere le funzioni residue della matrice.

## Contratto e architettura

Schema 11: tabella `cases`, fino a 32 identificativi distinti. Il caso
principale conserva `case_id`; il campo facoltativo `case` delle tabelle
di storia associa ogni riga a un caso. Vuoto significa caso principale.
Fasi omonime sono ammesse in casi diversi; riferimenti inesistenti e storie
senza fasi sono rifiutati. Migrazione 10→11 additiva e senza nuovi casi.

Geometria, materiali, sezioni, sostegni e parametri geotecnici sono comuni.
Fasi, carichi, falde, attivazioni e verifiche per fase sono separate.
Ogni caso dichiara provenienza delle azioni e verificatori richiesti.
Parametri geotecnici diversi richiedono ancora fogli distinti.

`EmbeddedWallHistories` in Application separa gli input e richiama il
percorso GPC esistente per ogni storia, da uno stato indipendente. GPC
calcola gli inviluppi; il coordinatore Design decide accettabilità e minimo
comune dell'infissione. Nessuna formula aggiunta ad ANTHEA e nessun sorgente
GPC modificato in questo incremento. Riutilizzato il set candidato 19.

La ricerca ricalcola tutti i casi, FEM e verificatori a ogni profondità.
Limiti di spostamento in testa e fonti possono essere assegnati per caso.
Tutti i criteri conservano caso, fase e azioni concomitanti.

## Interfaccia, persistenza e report

Editor degli altri casi e campo Caso nelle tabelle della storia. Il
selettore del risultato aggiorna diagrammi, fasi, sostegni, filtrazione e
verifiche; il confronto fra fasi resta interno al caso selezionato.
Inviluppi e ricerca riguardano l'intero insieme. Modificare un input invalida
il risultato del gruppo. Il riepilogo conta le fasi di tutte le storie.

Salvataggio e riapertura conservano il risultato completo. Report e CSV
leggono quello stesso snapshot, indipendentemente dal caso visualizzato.
Un caso non convergente rende incompleto il gruppo e parziale l'inviluppo;
non impedisce agli altri casi di concludersi. Il CSV segnala anche casi
senza alcuna fase meccanica conclusa. Annullare il calcolo ordinario non
pubblica un gruppo parziale; la ricerca conserva i candidati già conclusi.

## Attesi e collaudi eseguiti

Fixture indipendente preparata prima della validazione: integrazione
razionale della curvatura di due mensole, 26 stazioni. EI=2e14 Nmm²,
L=6000 mm; C1: H=10000 N, C=0, N=100000 N; C2: H=−6000 N,
C=12e6 Nmm, N=−30000 N. Attesi in testa +3,6/−3,24 mm e momenti
estremi +60/−48 kNm. Tolleranze assolute: 2e−8 mm, 2e−11 rad,
2e−7 N per N, 2e−5 N per V, 0,05 Nmm per M, per arrotondamento del
benchmark. Non sono limiti di progetto. Fixture immutabile, SHA-256:
`715cb45c1829a1aebc94bbaf885c2286d97c4bb33fdbeb319081c41c0a87ff14`.

- `paratie_storie.py --check`: 26 stazioni indipendenti invariate.
- 1419 controlli applicativi/MVVM PASS (`incremento-13/app-release`),
  compresi isolamento plastico con inversione, trasferimenti separati a
  Concrete/Steel/tiranti/FEM/UPL/HYD/stabilità, annullamento, non convergenza,
  migrazione, persistenza, invalidazione, export e otto esempi `.anth`.
- Ricerca comune: primo caso richiede 6,5 m, secondo caso 7,5 m; minimo
  comune 7,5 m. Tutti i candidati conservano entrambe le storie e i criteri.
- Runner `ci/20261019-170434-paratie-inc13-ui-final`: 6 PASS, 0 NEW-FAIL,
  58 controlli WPF, architettura e assenza di codice di prova in Release.
  Sorgenti invariati durante la corsa. Schermate chiare/scure controllate.
- Nove CSV, 5099 righe precedenti identiche esclusa l'impronta dello schema.
- Relazione multi-caso di otto pagine e PDF delle guide controllati
  visivamente; nessun ricalcolo introdotto nei report.

Rilievi incontrati e risolti: prefisso del caso applicato inizialmente anche
al vecchio avanzamento singolo (ripristinato il contratto); conversione di
un intero JSON nel solo generatore di test; fonte delle azioni C1 omessa
nel nuovo caso WPF (completato l'input, conservata la validazione). La prima
corsa WPF ha un NEW-FAIL documentato; la riesecuzione sopra è positiva.
Nessun atteso numerico, tolleranza o fallimento ammesso modificato.

## Guide e candidata locale

Guide globali Rev51: pratica 92 pagine/1028 frammenti; teoria 159 pagine/
2051 frammenti e 373 formule. Conservate 26 parti ITEC byte per byte per
guida; 251 pagine rasterizzate e controllate, dettaglio dei capitoli nuovi
e quattro PDF di indice. Wiki: 75 capitoli, 7091 termini, hash verificati.
Corretto anche il numero 49 rimasto nell'intestazione Markdown della Rev50.
Sedici file Rev50 conservati byte per byte in
`supporto/SUPERATI/paratie-rev51-20261019/`, con manifest.

PDF pratica SHA-256:
`d1d1c901c9f65620299194fe57beac5dc6d11322d649489d7d211604aec3a806`.
PDF teoria SHA-256:
`2348f6771cba41e8ab5ec87a22ce975a1ad3eac080befb34fc2bf7e031d6ac59`.

`tools/paratie/New-LocalPreview.ps1` prepara la cartella di prova soltanto
da revisione pulita, standard completato sulla medesima revisione e DLL
identiche al manifest candidato. Comprende avviatori, esempi, due guide
globali e impronte dei file. Non installa DLL in `lib/Checker` e non
costituisce un rilascio ufficiale. Regressione standard e confezionamento
di questo incremento da registrare a conclusione.

Restano aperti i requisiti avanzati della matrice, l'approfondimento di
pali/micropali in coda e i merge finali condizionati al completamento.
Nessun push eseguito e nessuna esclusione implicita.
