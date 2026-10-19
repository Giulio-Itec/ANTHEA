# Incremento 4 — stabilità globale per fase

Integrazione collaudata nel campo dichiarato. La matrice completa resta
vincolante; questa pagina registra soltanto prove effettivamente eseguite.

## Motore GPC

- `EmbeddedWallGlobalStability` riutilizza `SlopeStability` e Bishop: geometria
  della parete continua, due colonne stratificate, peso e azioni esterne
  caratteristiche della fase. Conferma esplicita del campo di applicazione.
- Model classifica G1/G2/Q, favorevolezza e psi per azione. Profilo globale NTC
  2018 A2+M2+R2 distinto da quello locale: coefficienti applicati una sola volta.
- `ISlopeHydraulicField` / `SeepageSlopeField`: pressioni del FEM convergente e
  pesi naturale/saturo dalle intersezioni con u=0, corpi esclusi prima del
  campionamento. Nessuna extrapolazione o resistenza aggiuntiva da suzione.
- Non drenato in tensioni totali: pressione nulla nel bilancio resistente,
  conservando il peso del terreno coerente con il campo.
- Campo iniziale: parete rettangolare continua, terreni orizzontali; sostegni e
  vincoli esterni senza geometria globale restano non valutabili. Dislivelli
  piezometrici richiedono FEM o un profilo idraulico esplicitamente assegnato
  nelle API GPC. L'editor corrente usa il campo FEM o la falda costante comune.

Teoria accanto al calcolo: `Checker/docs/metodi/stabilita-globale-paratie.md`.

## Catture e verifiche concluse

Artefatti sotto `supporto/artefatti/paratie/`:

- Cattura del percorso originario prima di modificare Slopes: nove casi,
  terreno asciutto, falda e corpo/carichi; combinazioni statiche, non drenate e
  sismiche. `slope-legacy-before.json` e `slope-legacy-after-01.json`,
  `slope-legacy-after-02.json`: SHA-256 identico
  `81BC1F0B8878858D9AAADC734315F0AECFB23A73A13E2B0694F53C77CA2CF53A`.
  Strumento di cattura in `Checker/tools/ParatieSlopeCapture`, scrittura CreateNew.
- `global-adapter-04`: 39 test paratie PASS, comprese nove nuove prove globali
  e di trasferimento idraulico. Confronti con campo idrostatico e affine, massa
  satura analitica, riflessione geometrica, unità, categorie e annullamento.
- Errore nella prima bozza del nuovo test: G2 favorevole in A2 scritto zero.
  Corretto a 0.8 dalla tabella 6.2.I NTC (G.U.20/02/2018 p.186), senza cambiare
  produzione né attesi storici. Conservati `global-adapter-first-draft.cs` e
  `global-adapter-03.log` (38 PASS, 1 FAIL), oltre alla nuova esecuzione.
- `candidate-09`: interrotto in compilazione della suite Geotechnics per CS0104
  sul nuovo enum Model omonimo a un enum storico Checker. Corretto il solo nuovo
  contratto in `GeotechnicalCharacteristicActionCategory`, test storico invariato.
- Model `4e1d6bbe`, Checker `9ea745cc`: set `candidate-10` concluso,
  **3104 PASS**, soli **17 fallimenti Steel già ammessi**. API compatibili.
  Versioni principali Model 5.9.0.1 e Geotechnics 0.8.0.1; tutte le DLL e
  SHA-256 nel manifest del set. Staging `snapshot-10` validato dal comando
  `Update-Snapshot.ps1 -AllowCandidate`, senza installazione in `lib/Checker`.
- `global-app-01`: **80 controlli applicativi/MVVM PASS**. Conversioni e
  coefficienti, confronto diretto con GPC, campi FEM e fuori dominio, migrazioni
  1/2/3→4, riapertura, esiti non valutabili, annullamento e isolamento.
- Runner `ci/20261019-101720-paratie-inc04-ui3`: build Release/UiTests,
  architettura e **19 controlli WPF PASS**. Le due corse precedenti hanno
  individuato un letterale grafico da nominare e colori fissi da sostituire con
  i token del tema. Corretta la vista; regole e fallimenti ammessi invariati.
- Runner `ci/20261019-102023-paratie-inc04-standard`, sul commit `212ce340`
  e set `snapshot-10`: **74 PASS, 0 KNOWN, 0 NEW-FAIL, 0 BLOCKED**.
  Inclusa Presentation.Tests (336,8 s), con il driver corretto nell'incremento 2.
  Le tre esclusioni infrastrutturali dichiarate dal runner sono ElasticPile.Checks,
  BridgeValidationCurrent (coperte dai runner delle librerie) ed ElasticPile.Performance.
  Sorgenti ANTHEA mantenuti invariati durante l'intera corsa; nessun atteso
  o elenco dei fallimenti ammessi modificato.
- PDF nativo `global-app-01/paratie/stabilita-globale.pdf`, tre pagine A4,
  renderizzato in `global-report-render-01`, tutte le pagine controllate.
  Grafici con nessuna superficie e con cerchio/conci controllati in chiaro/scuro.
  Il secondo esempio WPF conserva esplicitamente la mancata convergenza locale:
  il modello globale usa geometria e azioni esterne della fase, senza reazioni
  locali, e resta consultabile separatamente. Il suo minimo al bordo è Non
  valutato, non una verifica soddisfatta. Nessuna attestazione di rendering Word.

## Integrazione ANTHEA

Schema 4 con verifiche globali inattive per i documenti precedenti, tabelle di
geometria/ricerca e azioni, esiti con provenienza, conci, superficie e coefficienti;
report e CSV dallo stesso snapshot. L'esito viene dal verificatore GPC già
esistente, senza formule duplicate nel livello applicativo. Risultati, grafici
e comandi vengono invalidati quando cambia un dato del documento.

Il verdetto globale è distinto dalla convergenza della storia meccanica; un
rapporto di ricerca non conclusivo è conservato come valore tentativo ed è
separato dall'utilizzo conclusivo. Domanda e capacità della verifica globale
sono rispettivamente gammaR e F, adimensionali, come nel verificatore GPC.

Guide globali DOCX/PDF, estensione ai sostegni e alle altre geometrie, altri
profili normativi, collaudo complessivo e rilascio ufficiale restano aperti.
Il seguito sui pali interrati è confermato in coda al modulo, prima dei merge.
