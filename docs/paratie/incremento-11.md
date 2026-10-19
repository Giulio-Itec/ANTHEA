# Incremento 11 — ricerca discreta dell'infissione

Base ANTHEA del passo `f5b3d5d281dfe0f04dc28a3dbb14b49c7069bb15`;
Model `80a5463b1e09df43f781f3406b0153c7c1f9be58`, Checker
`924f6e7689f17d9b49a72f2aee0a9ed6f60e3989`. Rami dedicati invariati.

## Ambito implementato e collaudato

Ricerca esaustiva su griglia costruttiva finita, senza ipotesi di monotonia.
Quota di riferimento, intervallo, passo e criteri sono espliciti. GPC Design
richiede tutte le storie e i criteri previsti; il foglio corrente ricerca
sulla combinazione assegnata, con tutta la sequenza delle fasi.
GPC supporta e collauda anche due storie indipendenti nella stessa ricerca.

Geotechnics genera ogni geometria dal modello più lungo, senza estrapolare
proprietà o eliminare carichi/sostegni incompatibili. Vietato spostare
vincoli artificiali al piede. Il percorso ANTHEA ricalcola filtrazione,
meccanica e verifiche a ogni quota. Il coordinatore GPC aggrega i risultati
esistenti senza formule o coefficienti duplicati nell'applicazione.

Il limite assegnato riguarda il massimo assoluto in testa fra le fasi,
non tutta la deformata. Si includono tutte le famiglie richieste nel
documento, compresi i dettagli dei pali in c.a.; una famiglia richiesta
ma priva di esiti resta Non valutata. Un criterio sicuramente non
soddisfatto esclude il candidato. La mancata convergenza non dimostra
inammissibilità: se riguarda una quota meno profonda del miglior candidato,
non si dichiara il minimo dimostrato. La conclusione vale solo sulla griglia
e per i criteri assegnati, non come verifica completa dell'opera.

Schema documento 10: campi della ricerca inizialmente non assegnati,
senza inventare limiti. Snapshot di tutti i candidati, diagnostica e
provenienza, tabella dei criteri, diagrammi del candidato accettabile meno
profondo, CSV e relazione. Il report usa gli stessi risultati della vista.
Annullamento: si conservano solo i candidati conclusi, senza minimo finale;
modifica degli input e chiusura impediscono pubblicazioni tardive.

## Attesi e verifiche eseguite

Atteso analitico preparato prima del coordinatore: trave libera su
Winkler uniforme, EI=2e14 Nmm², k=2 N/mm², H=10000 N in testa,
M nullo agli estremi e V nullo al piede. Quattordici lunghezze e tredici
sezioni; Decimal 60/80 cifre e controllo separato delle reazioni mediante
Simpson. SHA-256 della fixture
`A6EE11220A0A1E064D9EC657F53746835EA1DEE4CD592676F01D5D5898D8767A`.
Metodo e tolleranze in `Checker/docs/metodi/ricerca-infissione-paratie.md`.

- Dieci nuovi test GPC; 24 Design paratie PASS. Minimo 6500 mm per limite
  3.25 mm; 7500 mm aggiungendo la seconda storia H=15000 N, limite 4.5 mm.
- Provati memoria delle fasi, elasticità e inversione, equilibrio,
  raffinamento, griglia non monotona, meccanismi, input invalidi,
  criteri mancanti, geometrie incoerenti, isolamento e annullamento.
- Primo criterio del test di raffinamento troppo restrittivo sul regime
  di arrotondamento: errore M 0.00187→0.01533 Nmm da 12 a 48 elementi,
  L=3500. Separata convergenza 6→24 da accuratezza assoluta a 96 elementi.
  Attesi, tolleranze assolute e motore preesistente invariati.
- `app-coupled`: 1279 controlli applicativi/MVVM PASS. Ogni candidato
  confrontato con il calcolo singolo; campi FEM di due quote ricalcolati e
  confrontati separatamente; trasferimento a Concrete e Steel identico.
- Cattura prima sul binario Rev48: 1210 controlli PASS. Nove CSV, 5099 righe,
  identici dopo l'incremento esclusa l'impronta: nessuna differenza numerica.
- Runner `ci/20261019-151810-paratie-inc11-ui`: cinque PASS, 0 NEW-FAIL;
  build Release/UiTests, architettura e 46 controlli WPF. Albero ANTHEA
  non ancora committato rispetto alla base, invariato durante la corsa.
  Schermate chiare/scure della ricerca esaminate. Relazione di sei pagine
  rasterizzata e controllata, senza sovrapposizioni introdotte.

Il primo build applicativo ha intercettato un tipo GPC di avanzamento
nella presentazione: sostituito con un DTO applicativo, senza aggiungere
dipendenze. Il primo test MVVM usava il separatore italiano in una stringa
formattata con la cultura del runner: corretto il test verificando stato
e quota numerica; nessuna tolleranza ingegneristica modificata.

## Revisioni GPC e rilascio

Model `064b9214ed3e0f51de03a5476c8a7d0dfc71b9dd`, Checker
`785301df5358d387a7cc2379d491884c6b948232`. Model 5.15.0.0,
Geotechnics 0.12.0.0, Design 0.27.0.0 per nuove API; le librerie
dipendenti aumentano l'ultima revisione per il nuovo treno di compilazione.
`Release-Set.ps1 -Candidate`, `candidate-18`: 3160 PASS, soli 17 fallimenti
Steel già ammessi, compatibilità API verificata. Set validato con
`Update-Snapshot.ps1 -AllowCandidate` in `snapshot-18`.
Nessuna installazione in `lib/Checker`, nessun push o merge.

## Documentazione e stato del passo

Guide globali Rev49 finalizzate: pratica 90 pagine/1015 frammenti;
teoria 157 pagine/2038 frammenti e 370 formule modificabili. Nessun
frammento mancante, 26 parti ITEC per guida identiche. Prima esportazione
teorica interrotta da Word sull'aggiornamento dei numeri dell'indice;
riesecuzione dedicata conclusa senza modifiche al contenuto.
Controllate visivamente le 247 pagine rasterizzate, con ispezione di dettaglio
del nuovo capitolo, dei quattro indici PDF e del report di ricerca di sei
pagine. Corretta l'impaginazione pratica spostando la figura dopo il
benchmark: nessuna riga isolata nella pagina finale. Sorgenti e PDF
corrispondenti aggiornati; 16 file Rev48 conservati byte per byte in
`supporto/SUPERATI/paratie-rev49-20261019/`, con origine e SHA-256.
Wiki rigenerata: 75 capitoli, 7050 termini e otto destinazioni di aiuto
del modulo.

Regressione consolidata `ci/20261019-154122-paratie-inc11-consolidato`,
commit `6e4b47f010153b0ebc950845751299578ecdbc83`, snapshot-18:
93 PASS, un NEW-FAIL Wiki.Tests, sorgenti invariati durante la corsa.
Il controllo storico contava anche i due nuovi capitoli e quindi falliva
sul numero atteso degli articoli originari. Verificate le 73 identità
(id/chiave) direttamente dalla base F5: nessuna persa, aggiunte soltanto
`guida-paratie` e `analisi-paratie`. Il test ora conserva queste identità
in una fixture derivata da F5 e controlla separatamente le due aggiunte.
Non sono stati modificati attesi numerici o fallimenti ammessi.
Riesecuzione `ci/20261019-160423-paratie-inc11-wiki-identita`: quattro
suite PASS, 0 NEW-FAIL, 331 controlli Wiki; sorgenti invariati durante
la corsa. Le altre 93 suite non sono state ripetute per la sola correzione
del test Wiki.

PAR-23 resta in corso: combinazioni multiple nel foglio e criteri ulteriori
di infissione vanno integrati con le funzionalità ancora aperte. La matrice
complessiva, il seguito pali/micropali interrati, il rilascio ufficiale e i
merge autorizzati a lavoro concluso restano aperti.

Evidenze: `supporto/artefatti/paratie/incremento-11/`,
`supporto/artefatti/paratie/guide-rev49/` e cartelle CI indicate.
