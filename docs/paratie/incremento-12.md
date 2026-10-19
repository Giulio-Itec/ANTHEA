# Paratie, incremento 12: inviluppi e azioni concomitanti

19 ottobre 2026. Base applicativa `3896e0c738869eca80259da6a3b4220603e7fd3e`;
Model invariato `064b9214ed3e0f51de03a5476c8a7d0dfc71b9dd`;
Checker `c78e01288abdd0e06eaf3d7f407772b19305cdde`.

## Comportamento concluso

GPC Geotechnics conserva i campi recuperati di ogni elemento e fase conclusa:
spostamento cubico, rotazione, N lineare, V quartico, M quintico e pressioni.
Il campionamento usa questi campi congelati, senza risolvere nuovamente la
struttura. Le formule e le convenzioni sono documentate in
`Checker/docs/metodi/inviluppi-paratie.md`.

Gli inviluppi minimo/massimo conservano caso, fase, sezione, quota, lato e
intera osservazione concomitante. Le stazioni comprendono tutti i nodi,
entrambi i lati delle discontinuità, suddivisioni regolari e radici delle
derivate: anche gli estremi interni del campo recuperato sono individuati.
Mesh differenti sono ammesse con identiche estremità e larghezza della fascia.
Non si sommano risultati non lineari e non si crea una terna N–M–V artificiale.
Le verifiche continuano a usare le azioni delle rispettive fasi.

Il foglio ANTHEA presenta otto grandezze nella scheda Inviluppi, due curve,
tabella delle stazioni, provenienza ed azioni concomitanti della selezione.
Il rapporto, il CSV a 19 colonne e la persistenza leggono lo stesso snapshot.
Lo schema degli input resta 10; le versioni delle librerie invalidano i vecchi
risultati. Gli inviluppi sono presenti anche nei candidati della ricerca
dell'infissione. Modifiche degli input invalidano risultati e comandi di export.
Una storia non convergente produce un inviluppo parziale delle sole fasi concluse.

## Attesi e verifiche eseguite

Nuovo atteso indipendente con aritmetica razionale, equilibrio e integrazione
della curvatura di una mensola di 6000 mm: tre fasi, due casi, 49 quote e
limiti interni. Nessun risultato del solver è usato per costruire la fixture.
SHA-256 della fixture nel formato CRLF del repository:
`C9A7ED23BC4485BFB80FE7203F83CDD6966E378C354E67636FDA662DB249C55D`.
Tolleranze assolute: 2e-8 mm, 2e-11 rad, 2e-7 N per N, 2e-5 N per V,
0,05 Nmm per M. Coprono gli arrotondamenti di questo benchmark; non sono
limiti progettuali. Una prova distinta individua il massimo interno
12,5e6 Nmm a 2500 mm anche con una sola suddivisione per il disegno.

- Sette nuovi test GPC; tutti i 67 EmbeddedWalls e i 24 Design paratie PASS.
- Set coerente candidato 19: 3167 PASS, gli stessi 17 fallimenti Steel
  storici ammessi, compatibilità API confermata. Geotechnics 0.13.0.0 per
  la nuova funzionalità; Design 0.27.0.1 per la dipendenza aggiornata.
- Integrazione applicativa: 1305 controlli PASS, inclusi persistenza,
  isolamento, annullamento, non convergenza, invalidazione, CSV e rapporto.
- Runner `ci/20261019-161359-paratie-inc12-ui`: cinque suite PASS,
  52 controlli WPF e architettura, nessun NEW-FAIL. Sorgenti invariati durante
  la corsa. Schermate chiare/scure controllate visivamente.
- Sette storie GPC precedenti identiche in tutti i campi originari;
  aggiunti soltanto metadati e campi congelati degli elementi. Nove CSV
  applicativi precedenti, 5099 righe identiche esclusa l'impronta delle DLL.
- Relazione campione di quattro pagine generata e controllata visivamente;
  azioni concomitanti e stati coerenti con lo snapshot dell'interfaccia.

La prima invocazione di build applicativa è stata rifiutata da MSBuild per
un argomento PowerShell composto erroneamente (`MSB1008`); corretta
l'invocazione, build e prove sono state eseguite con esito positivo.
Nessun atteso numerico, tolleranza o elenco di fallimenti ammessi modificato.

## Guide e distribuzione

Guide globali Rev50: pratica 91 pagine e 1022 frammenti, teoria 158 pagine,
2046 frammenti e 373 formule; nessuna perdita rilevata. Tutte le 249 pagine
rasterizzate e controllate, con esame di dettaglio del nuovo capitolo.
Word, PDF, indici e Wiki aggiornati. Sedici file della Rev49 conservati
byte per byte in `supporto/SUPERATI/paratie-rev50-20261019/`, con manifest.

SHA-256 PDF pratica:
`4523cd8a84366727a015376998882b247984d3f9f1bfe0c364c15f40f0f64d5a`.
SHA-256 PDF teoria:
`6dbc06ad78fa70058415990146f1ddbcaa3d42671b3e2a942a9e48f6e470037d`.

Il set candidato è validato soltanto in `snapshot-19`; `lib/Checker` resta
invariata. Nessun push o merge eseguito. Rilascio ufficiale ancora aperto.
Evidenze: `supporto/artefatti/paratie/incremento-12/`,
`supporto/artefatti/paratie/guide-rev50/`, `candidate-19/` e cartella CI sopra.

## Limiti e seguito

L'interfaccia riunisce le fasi del caso corrente; l'API GPC collaudata
ammette più storie indipendenti. La loro gestione integrata nel foglio è
il passo successivo. Il disegno interpola fra stazioni: non ricerca tutte
le intersezioni fra campi di casi diversi e non attribuisce un unico caso
governante all'intero segmento disegnato. La provenienza è del singolo punto.
Questa interpolazione non cambia gli estremi globali dei campi recuperati.
Il recupero non elimina la necessità di verificare la convergenza della mesh.

PAR-27, la matrice complessiva, l'approfondimento di pali/micropali e i
merge finali autorizzati restano aperti. Nessuna esclusione implicita.
