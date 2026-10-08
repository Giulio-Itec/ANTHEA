# Refactoring di ANTHEA: decisioni

Registro delle decisioni prese con l'utente. Il piano operativo per fasi è in
[piano.md](piano.md); le differenze numeriche e gli scostamenti tecnici vanno nel
registro unico `registro-differenze.json` di questa cartella (creato alla prima voce).

## Obiettivo (6 ottobre 2026)

- ANTHEA è solo interfaccia. Calcoli, formule e coefficienti normativi stanno nelle
  librerie GPC (Utilities, Geometry, Model, Checker e la nuova GPC.Design).
- Il codice non grafico specifico di ANTHEA (formato dei documenti, migrazioni, progetti,
  adattatori verso le librerie, report) diventa un insieme di librerie senza WPF nello
  stesso repository: ANTHEA.Application, ANTHEA.Reports, ANTHEA.Wiki.
- La Wiki contiene solo contenuti tecnici e precisi, scritti da noi.

## Architettura approvata

| Livello | Contenuto | Può dipendere da |
| --- | --- | --- |
| L1 | Utilities, Geometry, Model, ModelData: unità, sezioni, materiali, terreni, norme tipizzate | — |
| L2 | GPCChecker.Concrete, .Geotechnics, .CompositeBridge: tutte le verifiche | L1 |
| L3 | GPC.Design (nuova): predimensionamento, ottimizzazioni, distinte | L1, L2 |
| L4 | ANTHEA.Application, ANTHEA.Reports, ANTHEA.Wiki (net8, senza WPF) | L1–L3 |
| L5 | ANTHEA.Desktop (WPF, MVVM) | L4 |

Regole, verificate in automatico man mano che le fasi le rendono possibili:

1. Nessuna formula né coefficiente normativo in L4 e L5.
2. La norma diventa un tipo al confine dell'adattatore; nessun ripiego silenzioso su NTC 2018.
3. Unità dichiarate nelle firme; conversioni solo negli adattatori, con costanti nominate.
4. Esito comune (esecuzione, completezza dei dati, esito ingegneristico); i testi solo in report e interfaccia.
5. Librerie solo da commit versionati, senza wildcard.
6. Nessun codice di test nell'eseguibile.

## Decisioni D1–D8 (6 ottobre 2026)

- **D1** Confine "solo UI": ANTHEA.Desktop solo interfaccia; librerie non grafiche di ANTHEA nello stesso repository.
- **D2** Codice di progetto non di verifica (BridgeConcept, progetto delle armature, distinte) nella nuova libreria GPC.Design. Collocazione precisa decisa in F4.
- **D3** Ordine: F0 messa in sicurezza, F1 test fuori dall'eseguibile, F2 chiusura del c.a., F3 strato applicativo, F4 calcoli residui nelle librerie, F5 interfaccia MVVM. Wiki e infrastruttura in parallelo.
- **D4** Subito uno snapshot `lib/Checker` riproducibile da commit; più avanti feed NuGet con uno switch verso i progetti delle librerie.
- **D5** Corpus esterno della Wiki eliminato (vedi sotto).
- **D6** Teoria per metodo accanto alla libreria che lo implementa, distribuita con le DLL; guida d'uso per modulo in ANTHEA; le due guide globali diventano documenti generati dalle stesse fonti.
- **D7** Sei scostamenti tecnici (a)–(f): per ciascuno l'utente decide se correggere o dichiarare, dopo una scheda con l'effetto quantificato (passo F0.6).
- **D8** `supporto/tmp` e `SUPERATI` fuori dall'indice; PDF e DOCX generati invece che versionati.

## Decisioni di avvio (6 ottobre 2026)

- **node_modules nel commit 2cdf9fd**: opzione B. Il commit F0.1 lo toglie dall'indice; la
  riscrittura della storia remota si fa più avanti, in modo coordinato. L'utente è l'unico a
  lavorare sul repository.
- **Branch**: un branch per fase (`refactoring/<fase>-<nome>`), merge su main a fine fase con
  l'approvazione dell'utente. Tag locali `pre-refactoring` (2cdf9fd), `pre-<fase>`,
  `post-<fase>`, `pre-lib-<versione>`. Per tornare indietro si usa `git revert`, mai un
  checkout di commit vecchi. Push solo su richiesta esplicita.
- **Layout**: `src/` (produzione), `tests/` (test), `tools/` (strumenti e script), `build/`
  (runner e configurazione di build), `docs/` (documentazione di sviluppo). I file nuovi
  nascono lì; i progetti esistenti si spostano in una finestra concordata tra F2 e F3.
- **Commit di riferimento delle librerie** per lo snapshot: l'ultimo commit dei rami
  locali scelti dall'utente. Al 6/10: Utilities master 91574c1, Geometry master 296d05d,
  Model master c85b70a6, Checker develop 5cc315f2.
- **SDK**: ANTHEA resta su .NET SDK 8 (global.json esistente). Le librerie si fissano con un
  global.json sull'SDK 9.0.318 (rollForward latestPatch), lo stesso con cui sono state
  compilate le DLL in uso: così la ricostruzione cambia il meno possibile. L'SDK usato è
  registrato nel manifest di `lib/Checker`.
- **Esempi e template**: gli esempi `.anthea` e `.programma` di `supporto/esempi` e il
  template Word ITEC si possono versionare (dati sintetici).
- **Wiki**: eliminare tutti i contenuti e i riferimenti esterni (articoli adattati, letture
  tecniche, rimandi a blog e siti, riferimenti a programmi concorrenti) e tenere solo i
  contenuti nostri. Restano le citazioni normative (NTC, Circolare, Eurocodici) e la
  bibliografia tecnica che documenta i metodi implementati.

## Decisioni sugli scostamenti tecnici (6 ottobre 2026, sera)

Schede ed effetti in `scostamenti.md`; stato delle voci in `registro-differenze.json`. Correzioni
sempre in due commit (collegamento con opzione legacy identico, poi cambio con effetto atteso).

- **(a) Fessurazione, barre distanziate**: "correggi se è sbagliato, controlla EC, NTC, annessi e
  Model Code". Esito della ricerca (`supporto/artefatti/refactoring/ricerca-a/rapporto.md`): il ramo
  NTC è corretto (Circolare 2019 [C4.1.10], 0,75(h − x)); corretti anche EN, UNI, CNR-DT 200, DS, NS.
  Da correggere: MC2010 a lunga durata (τbms = 1,8 fctm in fessurazione stabilizzata, Tab. 7.6-2) e
  DIN (kt = 0,6/0,4 secondo la durata invece di 0,4 fisso); citazioni "7.3.4(4)" → "7.3.4(3), eq. (7.14)".
- **(b) k2 nel ramo NTC**: da discutere con l'utente.
- **(c) EN 1998-5 Annesso F**: seguire la norma (γRd non applicato a F̄).
- **(d) γb dei pali**: seguire la norma (Tab. 6.4.II per tecnologia).
- **(e) Durabilità**: l'utente chiede entrambi i riferimenti con scelta dell'utente. La revisione
  tecnica ha poi trovato che C30/37 è il valore di UNI 11104:2016 (e della 2025) e C28/35 quello
  dell'edizione 2004: da confermare con l'utente prima di implementare la scelta.
- **(f) Bridge Design**: cercare il metodo semplificato più vicino al reale. Esito della ricerca
  (`supporto/artefatti/refactoring/ricerca-f/rapporto.md`): nessun carico uniforme resta
  cautelativo entro il 10%; raccomandato lo Schema 1 NTC con linee di influenza e γQ = 1,35, in GPC.Design.
- **(g) Metodo nei report del ponte**: ripristinare metodo, norma e versione della libreria.

## Decisioni del 7 ottobre 2026, mattina

- **Fasi approvate**: F0, F1, D7-c/D7-d/R3 e W0.4/W0.5 (guide Rev31), comprese le due correzioni del disegno
  del ponte della diagnosi UI ("approvo le modifiche"). Merge su main in locale. Push di ANTHEA da confermare.
- **Librerie**: "pusha le librerie". Pushati Utilities master (df3b3e7), Geometry master (6a0d1c1), Model
  master (d6631635), Checker develop (7c15cf4f), da cui è costruito lo snapshot S1 di `lib/Checker`.
- **(b) k2**: "hai ragione. fallo come lo hai previsto. se l'asse neutro taglia la sezione. occhio però che non
  si cada nel caso di pura compressione". k2 = 0,5 quando l'asse neutro taglia la sezione, per ogni normativa;
  la sezione interamente compressa resta con wk = 0 prima di ogni uso di k2. Attuazione sul branch
  `refactoring/d7b-k2-flessione` e in Checker `anthea-d7b-k2`.
- **(e) Durabilità**: "confermo la norma più aggiornata". Si tiene la UNI 11104 nell'edizione in vigore (2025,
  che per XC3, XD1, XF4 e XA1 conferma C30/37 come la 2016); niente scelta del riferimento 2004. Da fare:
  riscontrare sul testo della 2025 le classi minime di tutte le esposizioni e aggiornare le citazioni
  («UNI 11104 prospetto 5» dipende dall'edizione). Esito del 7/10 pomeriggio: il codice segue la 2016
  (prospetto 5); secondo un estratto la 2025 (prospetto 6) differisce per XF1 (C30/37, a/c 0,55) e per il
  cemento minimo. Vedi D7-e: opzione e4 in F2.9, dopo il riscontro su una copia con licenza.
- **(f) Bridge Design**: "se fattibile come costo computazionale (deve essere rapido) prendiamo in
  considerazione il tandem, linee di influenza, γQ 1,35". Schema 1 con tandem e linee di influenza in forma
  chiusa e γQ = 1,35 in GPC.Design (F4.11), con il vincolo di un costo di calcolo che non rallenti in modo
  percepibile esplorazione e ottimizzazione (da misurare contro il tempo attuale).
- **(d) γb dei pali**: "in questo momento non ci sono ancora file salvati di anthea. quindi puoi effettuare
  direttamente d2. non è ancora attivo come salvataggio per gli utenti". Opzione d2: il valore della tecnologia
  diventa anche il valore di riserva (foglio senza γb) in calcolo, relazione ed editor, e i fogli salvati prima
  di d2 con palo battuto o a elica e γb 1,35 lo prendono con una migrazione una tantum (marcatore nel foglio,
  così un 1,35 scelto dopo resta). I casi di regressione dichiarano 1,35 nell'input, ipotesi dei loro attesi
  Python, che non cambiano. Branch `refactoring/d7d-d2-coefficienti-pali`.
- **Voci sfavorevoli R4-R14**: "le guardiamo a fine refactoring. segna di ridiscuterne". Restano da decidere,
  con discussione fissata alla chiusura del refactoring (dopo F5).
- **Guide**: "ok" ai default dichiarati come «convenzione di ANTHEA» e alla Rev30 lasciata in SUPERATI.
- **Fixture di Checker (F2.1)**: "B: rifai la fotografia del comportamento attuale, appena sicuri del codice
  dietro". Le fixture congelate il 1/10 dal legacy di ANTHEA si ricatturano dal comportamento attuale dopo la
  verifica di D7-b; spariscono le classificazioni FC-1…FC-8 e i casi speciali dei test di migrazione; le
  fixture precedenti restano nella storia di git.
- **Fotografie a fine lavoro**: "a fine lavoro ricattura tutte le foto di anthea refactorata". Alla chiusura
  del refactoring si ricatturano tutti i riferimenti presi da ANTHEA: fixture c.a. e geotecniche di Checker,
  baseline headless e catture dense.

## Decisioni del 7 ottobre 2026, giorno

- **Push e prosecuzione**: "fai push su main, poi continua il refactoring. quando hai finito, aggiornami e fammi
  una tabella di recap. se hai finito e tutto funziona, test, commit e push". Main pushato (ebcab0e).
- **R15, fasce interne dei fori** (h − x senza limite in trazione quasi uniforme, wk fino a 10⁹ mm): scelta fra
  due limiti, nessuno continuo ovunque. Decisione: "Altezza lungo il gradiente" — h − x non supera l'altezza
  della sezione lungo il gradiente; continua quando l'asse neutro entra nella sezione, salto residuo solo alla
  soglia della trazione uniforme dove l'altezza normale alla faccia e quella lungo il gradiente differiscono.
  La regola alternativa (altezza normale alla faccia in sezione interamente tesa) è ritirata.
- **k2 delle fasce interne dei fori** con l'asse neutro che taglia la sezione: "(7.13) della fascia" — ogni
  fascia usa k2 = (ε1 + ε2)/(2 ε1) della propria distribuzione, non 0,5.
- **Prove WPF**: "Esegui ora il full"; poi "sto andando a pranzo. ti lascio mezz'ora di schermo. a fine lavori
  report di lavori, test, committa e continua con il refactoring". Profilo full con prove WPF eseguito durante
  la pausa sulla punta dell'integrazione.

## Decisioni del 7 ottobre 2026, pomeriggio (coordinatore su delega dell'utente «esegui tutto te», da ratificare)

L'utente ha delegato le decisioni del pomeriggio al coordinatore dei workflow ("esegui tutto te"). Le decisioni
seguenti sono state prese dal coordinatore su delega e valgono fino alla ratifica dell'utente. Voci del registro: R7
e R16-R21 (F2.2), D7-e, F2-1…F2-4 (F2.5-F2.6).

### F1.4, quattro scenari sull'exe Release

- Eseguiti dal coordinatore con UI Automation e mouse reale sull'exe Release compilato da main 17b6c98, con i dialoghi
  di produzione, invece che a mano con l'utente. Esito in `supporto/artefatti/refactoring/f1.4/esito.md`: avvio con la
  Home e 4 scenari superati.

### F1.6, destino dei progetti di test e seguiti

- **Destino dei progetti di test**: la tabella di [progetti-di-test.md](progetti-di-test.md) era da approvare;
  l'utente ha delegato la decisione ("esegui tutto te"); decisa dal coordinatore su delega, da ratificare. Si
  archiviano in `supporto/SUPERATI/test/<nome>`, con `git mv` e contenuto invariato, i progetti che dipendono
  da servizi o programmi di terzi, gli strumenti una tantum e quello che non compila:
  BridgeDesign.SiteComparison, MaxRetainingWall.Cases, MaxRetainingWall.Compare, ConcreteStressDiagnosis,
  ValidazioneCA20260925, ElasticPile.Performance (riportato in `supporto/test`, vedi sotto) e ProgrammaAnthea
  (`qa.py`). Nessuno era nel runner. Gli altri progetti mantengono la destinazione già scritta in tabella.
  Origine, motivo e sostituzione di ogni file sono in `supporto/SUPERATI/registro-20261007-test.json`.
- **Seguito di F1.6 dopo la verifica dell'integrazione 2**:
  - si archivia anche `supporto/script/validazione_ca_2026_09_25`, generatore una tantum della relazione del
    25/9 che importa `reference_base.py` e `reference_extra.py` di ValidazioneCA20260925, in
    `supporto/SUPERATI/script/validazione_ca_2026_09_25`, con voci nel registro di SUPERATI. `build_document.py`
    torna al testo di prima di F1.6: l'import da `SUPERATI/test` era rotto, perché `reference_base.py` calcola
    la cartella degli artefatti della campagna dalla propria posizione (`parents[2]`);
  - ElasticPile.Performance torna in `supporto/test` con `git mv` e nell'elenco delle suite non eseguite di
    `build/ci.ps1`: la tabella gli assegnava "Archiviare dopo F5, o spostare in `tools/`", destinazione che
    resta invariata, e la decisione sopra conserva le destinazioni già scritte;
  - MaxRetainingWall.Cases e .Compare, per cui la tabella rimandava la scelta all'utente, restano archiviati: la
    scelta è reversibile con `git mv` ed è documentata in [progetti-di-test.md](progetti-di-test.md) e nel
    README di SUPERATI;
  - le cartelle `bin` e `obj` ignorate rimaste nelle vecchie posizioni dei progetti restano per ora; si
    tolgono a fine refactoring con l'autorizzazione dell'utente (sotto, «Autorizzazioni dell'utente»).
- **Riepilogo con i commit** (dettaglio in [progetti-di-test.md](progetti-di-test.md)):

  | Progetto o cartella | Decisione | Commit |
  | --- | --- | --- |
  | ConcreteDesign.DesktopChecks, ValidationIllustrations | codice nella configurazione `UiTests` (`--check-concrete-design`, `--capture-validation`); vecchi progetti in `supporto/SUPERATI/test` | cdd56ea |
  | BridgeDesign.SiteComparison, ConcreteStressDiagnosis, ValidazioneCA20260925, ProgrammaAnthea (`qa.py`) | archiviati in `supporto/SUPERATI/test/<nome>` con `git mv`, invariati, con registro `registro-20261007-test.json` | 4ff5026, f5647f5, d01caf7 |
  | MaxRetainingWall.Cases e .Compare | archiviati come sopra; la tabella rimandava la scelta all'utente: restano archiviati, scelta reversibile con `git mv` e documentata | 4ff5026 |
  | `supporto/script/validazione_ca_2026_09_25` | archiviato anche il generatore una tantum della relazione del 25/9, che importa le parti Python di ValidazioneCA20260925, in `supporto/SUPERATI/script/validazione_ca_2026_09_25`, con voci nel registro di SUPERATI; `build_document.py` al testo di prima di F1.6 | 3ff710e, a57aff6, a2a3ac1 |
  | ElasticPile.Performance | riportato in `supporto/test` con `git mv`; destinazione della tabella invariata: «Archiviare dopo F5, o spostare in `tools/`» | cb9b750 |
  | Cartelle `bin` e `obj` ignorate rimaste nelle vecchie posizioni | non si cancellano; le segnala il coordinatore all'utente | — |
  | Tabella, decisioni e piano | allineati ai seguiti | 99b2ae7 |

### D7-e, durabilità e UNI 11104

- **(e) Durabilità, attuazione di «confermo la norma più aggiornata»**: UNI 11104:2025 in vigore dal
  24/07/2025 al posto della 2016 (schede del catalogo UNI); secondo un estratto pubblicato il 28/07/2025
  (fonte secondaria) il prospetto 6 «Valori limite per la composizione e le proprietà del calcestruzzo»
  dà C30/37 per XC3, XD1, XF4 e XA1, come il codice. Le pagine visibili dell'anteprima UNI (indice,
  introduzione, punti 1 e 2) confermano edizione e struttura, non i valori; la lettura «a p. 12
  dell'anteprima» registrata in un primo tempo veniva da contenuti del file non mostrati (fonti
  riscritte il 7/10, punto «seguito su XF1 e composizione» sotto). C30/37 resta, nessun valore cambia; in ANTHEA le
  citazioni indicano ora edizione e prospetto (scheda Materiali, report dei materiali, commenti, README, guida
  teorica; branch `refactoring/d7e-uni11104`). La libreria (`ExposureClasses.cs`) si corregge nella prossima release
  (F2.9), con il testo proposto in `scostamenti.md`. D7-e passa a dichiarato.
- **Da decidere (seguito di D7-e)**: il prospetto 6 della 2025 differisce dai valori del codice (2016,
  prospetto 5) per XF1 (C30/37 contro C32/40, A/C 0,55 contro 0,50) e per il cemento minimo, inferiore
  in tutte le classi; il codice è più restrittivo. Opzioni e4 (allineare alla 2025 in F2.9, raccomandata)
  ed e5 (tenere la 2016 come scelta cautelativa dichiarata). Decisa poi e4 (punto seguente).
- **D7-e, seguito su XF1 e composizione**: si adotta l'opzione e4, allineamento alla UNI 11104:2025, coerente con
  «confermo la norma più aggiornata». Si attua in F2.9 insieme allo spostamento della durabilità nella libreria,
  dopo il riscontro del prospetto 6 su una copia con licenza della norma: la variazione su XF1 (C30/37 e A/C 0,55
  invece di C32/40 e 0,50) e sul cemento minimo è a sfavore rispetto al codice attuale. Fonti di oggi, descritte
  in modo riproducibile nel registro e in [scostamenti.md](scostamenti.md): pagine visibili dell'anteprima UNI
  (indice alle pp. III-IV, introduzione e punti 1 e 2 alle pp. 1-2; frontespizio e prospetto 6 non visibili) ed
  estratto pubblicato il 28/07/2025 (fonte secondaria). La lettura «a p. 12 dell'anteprima» è ritirata: la p. 12
  non è visibile, era un residuo di una versione precedente dentro il file. Se il testo smentisse l'estratto, la
  scelta fra e4 ed e5 torna all'utente.

### F2.2, pagine dei metodi c.a., voci R16-R21 e rettifica di R7

- **Revisione tecnica accettata**. Le pagine sono unite in Checker develop con i merge locali c555a3b8 (taglio,
  torsione, SLE, fessurazione) e 1fbaea61 (ancoraggi, dettagli, durabilità, momento-curvatura) più le correzioni del
  verificatore (5675492f e 398eb36a con i merge 9eb800d4 e 26fb6b8b; bf23a16d, 7d86ffc7, bd9938ac, e41a803a, poi
  4f54139a). Dopo l'autorizzazione dell'utente (sotto) il coordinatore ha fatto il push di Checker develop a 4f54139a
  alle 17:59 (diff verso origin/develop solo su 9 file di `docs/metodi`).
- **Voci nuove R16-R21**: le voci nuove a sfavore o da chiarire diventano R16-R21 del registro, stato «da-decidere»,
  con la stessa decisione di R4-R14 («le voci sfavorevoli le guardiamo a fine refactoring», F5.15): R16 torsione DS
  con νv invece di νt = 0,7 (0,7 − fck/200); R17 classe indicativa di XC3 C25/30 contro C30/37 del prospetto E.1N del
  DM 2012; R18 incrudimento dell'acciaio di progetto riferito a fyk; R19 citazioni della torsione; R20 ρw,min DS non
  implementato; R21 condizione DIN (h − x)/3 con NominalCover invece del copriferro assegnato.
- **R7 rettificata**: NTC 2018 [4.1.38] dà 1 ≤ cot θ ≤ 2,5 anche in torsione (0,4 era della NTC 2008), quindi «NTC
  torsione: cot θ ≥ 1 anche in torsione pura» non è uno scostamento; resta da stabilire il segno del limite
  1,3 (h − x) con barre tese fuori da Ac,eff (riquadro F-4).

### F2.4, lib/Checker da commit pushati (`-FromUpstream`)

- Lo snapshot si compila dai commit dei rami remoti con `tools/libs/Update-Snapshot.ps1 -FromUpstream` (8d6a8ec):
  worktree temporanei sotto una radice fissa, così gli SHA-256 sono ripetibili. S2 ricompilata così (edc4fce, branch
  `refactoring/f2-4-snapshot-s2`) da Model origin/master 5ad56681 e Checker origin/develop 0d7ba50b, con le stesse
  versioni del candidato e `pushed: true`: la release non dipende più dal push di Checker develop, che dopo 0d7ba50b
  cambia solo `docs/metodi`.

### F2.5 e F2.6, taglio e torsione nella libreria

Decisioni prese durante la verifica avversaria di F2.5-F2.6 (branch `refactoring/f2-taglio-torsione`, unito
nell'integrazione 2 con il merge ea4c51e); voci F2-1…F2-4 del registro.

- **F2-3, torsione senza staffe chiuse**: resta un dato non valido, rifiutato con il messaggio di oggi; l'adattatore lo
  controlla prima della libreria, che darebbe invece una verifica non soddisfatta con resistenza nulla.
- **F2-4, limiti propri della libreria non raggiungibili dal modulo** (torsione oltre C90/105, componenti di taglio
  negative o non finite, spessore del profilo circolare confrontato con il diametro, norme CNR-DT rifiutate dalla
  mappatura): accettati, nessuna azione.
- **F2-2, traccia NTC della libreria**: non esposta nel JSON 'taglio' né nelle relazioni, come oggi; si riprende con le
  tracce in italiano per i report (F2.3).
- **Baseline B3**: il corpus della cattura headless di B2 non contiene fogli c.a. con taglio e torsione. Si aggiungono
  casi scritti per la cattura (`tests/ANTHEA.Testing/corpus`: rettangolare, circolare e circolare cava NTC con torsione,
  rettangolare DIN con cot θ assegnato e rifiuti) e si cattura la baseline B3 con il motore legacy, prima
  dell'interruttore (`supporto/artefatti/baseline/F2-B3`, commit dcde952). Riferimento headless dei passi F2.6-F2.9.
- **F2-1, ripieghi di `foro_presente`**: si ratifica F2-1 così com'è. Con il motore della libreria cambia solo il
  chiamante registrato del ripiego J.B("foro_presente") = False dei fogli senza la chiave
  (`ConcreteShearTorsionAdapter.TorsionGeometryOf` invece di `ConcreteTorsionCalculator.Geometry`), con la stessa
  chiave e lo stesso valore; il registro dei ripieghi (`fallbacks.json`) non è un risultato. Non si aggiunge al
  corpus di B3 un caso senza la chiave (l'altra scelta lasciata aperta dal registro): i casi di B3 dichiarano
  `foro_presente` (dcde952) e l'effetto sui documenti senza la chiave resta dichiarato in F2-1.

### Tema scuro

- I difetti di colore della modalità scura (fra cui le righe di sezione non selezionate dell'albero dei progetti,
  chiare su chiaro, `ProjectHierarchy.cs:155`, segnalate anche dall'utente) si correggono sul branch dedicato
  `refactoring/ui-tema-scuro` (worktree Temp\aw-tema), da unire dopo verifica.
- Esito (8/10, unito nell'integrazione 2 con 09142a9 e 356d9b9):
  - **Misura**: verifica automatica del contrasto (`--check-contrast`, c927ef1) su 56 viste e dialoghi in tre aspetti.
    I difetti per modalità scendono da circa 1800 a 0 in Scuro e in Molto scuro.
  - **Chiara**: identica al riferimento salvo la cattura dei terreni, che varia per un GUID. Il riferimento è a
    144 DPI, `riferimento-c927ef1-dpi144`, perché lo schermo è passato da 120 a 144 DPI.
  - **Cause principali** e relative correzioni:
    - colori fissi nel codice invece dei ruoli della tavolozza: sfondo, primo piano, bordo e i ruoli aggiunti alla
      chiusura (selezione, selezionato, calcolato, serie, campione, disattivato, fondo tinto);
    - elementi aggiunti dopo il caricamento: gestore di classe su SizeChanged;
    - stato selezionato e pulsanti primari Navy, indistinguibili in Molto scuro;
    - disegni OnRender senza fondo proprio: fondo bianco negli aspetti scuri.
  - **Report, esportazioni e stampa**: immagini sempre chiare con `Appearance.Document` e `Ui.DocumentSnapshot`. Le
    ha corrette una sessione separata avviata dall'utente sullo stesso branch (eabae27, fd14c94).
  - **Chiusura**: dieci correzioni, 16b646b…4fb8c85, sulle segnalazioni di due revisori visivi. Sette sono scartate
    con motivo, elencate in `supporto/artefatti/tema-scuro/esito.md` del branch.
  - **Eccezioni accettate**:
    - disegni tecnici e grafici su fondo bianco;
    - controlli disabilitati attenuati;
    - icone bitmap e figure della Wiki su fondo chiaro;
    - difetti identici in Chiara, come il titolo verticale del foglio «Verifica sezione».
  - **Seguiti da decidere con l'utente**:
    - i 719 difetti di contrasto della sola Chiara, lasciati per non cambiarla;
    - il testo disegnato in OnRender, non ancora misurato dalla verifica automatica;
    - la condizione di tempo delle catture di `str_mista_ponte`.
  - **Prova `ui/smoke-display`**: confrontava il pennello del γsat automatico per riferimento; ora confronta il colore
    (d69a5bb).

### Autorizzazioni dell'utente (7/10 sera)

Messaggi dell'utente nella sessione del coordinatore, riportati con le sue parole:

- **Cancellazioni**: «ti autorizzo a cancellare le cartelle non necessarie a fine refactoring». A fine refactoring il
  coordinatore toglie le cartelle che non servono più (le `bin` e `obj` ignorate rimaste nelle vecchie posizioni dei
  progetti archiviati, i worktree temporanei `Temp\aw-*` e `cw-metodi*`, i branch locali già uniti) e le elenca nel
  diario; il contenuto versionato si archivia in SUPERATI, le prove ancora citate restano. Alla domanda sullo spazio
  su disco (4,2 GB liberi) l'utente ha scelto «Rimuovi ora (Raccomandato)»: il coordinatore ha copiato nel checkout
  principale le prove citate nei documenti che erano solo nei worktree (S2-upstream, s2, s2u) e ha rimosso con
  `git worktree remove` i 20 worktree già uniti e puliti (aw-b0, aw-d7, aw-d7b, aw-d7d, aw-d7e, aw-dense, aw-f1,
  aw-f16, aw-f2, aw-fascia-prima, aw-guide, aw-int, aw-norma, aw-pc, aw-pre, aw-s1, aw-s2, aw-ui, aw-uifull,
  aw-wiki); i branch restano. Spazio libero dopo: 17,4 GB.
- **Push**: «ti abilito al push dei commit» e «abilitato al push». Il coordinatore fa i push dalla propria sessione
  dopo le prove, mai forzati (i workflow non fanno push). Eseguiti: Checker develop 4f54139a.

## Decisioni dell'utente dell'8 ottobre 2026, mattina

- **Priorità**: «voglio riuscire ad avere, prima della fine dei crediti, la possibilità di provare l'interfaccia […]
  voglio poter vedere se tutto il refactoring sta andando nel verso giusto». Prima si chiude il tema scuro e si compila
  una build da provare, poi il coordinatore si ferma per decidere i passi successivi con l'utente («finito il tema scuro,
  compila e fermati un attimo»).
- **Analisi dei bug**: «per ora stoppa la ricerca di bug. metti quei controlli in coda a tutti gli altri». La corsa
  `wf_9c55965c-68b` è ferma con 85 agenti conclusi in cache: due aree verificate per intero, fessurazione in parte, le
  altre aree solo con le segnalazioni dei ricercatori. Si riprende dopo tutte le altre attività.
- **Proposte U1–U7 del progetto F2.7 rivisto** (`supporto/artefatti/refactoring/f27-f28-progetto/F27-progetto-rivisto.md`, §12):
  - U1 «correggi»: il difetto di BarSpacing nella sezione NTC interamente tesa si corregge
    (`ConcreteTensionCracking.cs:73` cerca «s», la formula scrive «s (formula)»). Su 13 righe NTC di `crack-legacy.csv`
    il passo delle barre torna a comparire; wk, ηw ed esiti restano invariati.
  - U2 (R5): «teoricamente la norma non specifica il modo come effettuare l'analisi tensionale, se lineare o non
    lineare. l'analisi non lineare non è nata per la verifica tensionale agli SLE. quindi metterei un avviso […] e però
    lascerei la verifica. quindi opzione c ma con avviso tipo la a». Con «Non lineare» le tensioni di esercizio si
    calcolano con i legami di esercizio dei materiali, non con quelli di progetto (proposta del coordinatore, da
    confermare quando si implementa: calcestruzzo secondo EN 1992-1-1 §3.1.5, eq. 3.14, con fcm ed Ecm; acciaio
    bilineare con fyk ed Es, senza coefficienti parziali). La verifica resta e la relazione riporta un avviso, per
    esempio: «Tensioni di esercizio calcolate con analisi non lineare e legami di esercizio dei materiali. La norma
    non prescrive il metodo dell'analisi tensionale; per le verifiche SLE il riferimento abituale è l'analisi lineare
    a sezione fessurata.» È lavoro di libreria (SectionSolverModelCode2010, materiali di Model, StressLimitCheck):
    passo dedicato dopo l'interruttore di F2.7, con opzione legacy nominata e due commit.
  - U3 «correggi»: R22 si risolve applicando il fattore 0,8 dei getti sottili anche con UNI/DM 2012: limiti SLE del
    calcestruzzo, αcc e fcd con `gettato_sottile` = Sì.
  - U4 «tieni i limiti come oggi»: con CS-TR34 ANTHEA continua a calcolare i limiti tensionali SLE con i coefficienti
    di MC2010 da cui deriva `StandardCSTR34`. La proposta di allinearsi alla libreria (nessun limite) è respinta: la
    motivazione normativa non è verificata e togliere i limiti è meno cautelativo. In F2.7 la libreria deve quindi
    poter calcolare i limiti anche per CS-TR34 (opzione richiesta dall'adattatore).
  - U5 «correggi»: la scheda WPF rifiuta φ < 0, come già fanno i calcoli (`CheckerSection.cs:95`, `:127`).
  - U6 «metti in coda ad altre cose. non da fare ora»: la revisione delle pagine dei metodi ca.sle-tensioni e
    ca.fessurazione si sposta in coda.
  - U7 «correggi»: `staffe_presenti` si normalizza a Sì/No (riguarda solo documenti scritti a mano).
  Le correzioni U1, U2, U3, U5 e U7 cambiano risultati o comportamenti: si fanno in F2.7 dopo l'interruttore, una per
  commit, con cattura prima e confronto dopo (regola delle correzioni in due commit), e si registrano nel registro
  delle differenze.
- **Dopo la prova della build** (8/10, mattina):
  - «l'aspetto è molto buono, ad ora mi sembra corretto. nel caso trovassi bug te li segnalerò».
  - «hai messo chiaro/scuro dentro Aspetto. rimuovila dalla finestra wiki in alto»: il selettore dell'aspetto non è
    più nella barra della Wiki; resta il menu Aspetto della finestra principale.
  - «non toccare più i report di calcolo. saranno corretti successivamente. ti dico io quando sarà fatto»: il codice e
    i testi dei report di calcolo (X.Core Report*, ReportWord, report WPF) non si modificano finché l'utente non lo
    dice. Le correzioni di calcolo approvate possono cambiare i valori stampati, ma non i testi né l'impaginazione.
    L'avviso di U2 va quindi per ora nell'interfaccia e nei risultati, non nella relazione.
  - «anche la wiki mettila in coda ad altre cose»: la traccia Wiki (W) va in coda, dopo le fasi in corso.

## Dipendenze esterne previste

Da approvare una volta; ogni variazione si aggiunge qui.

| Pacchetto o strumento | Licenza | Uso | Fase |
| --- | --- | --- | --- |
| MSTest.TestFramework, MSTest.TestAdapter, Microsoft.NET.Test.Sdk | MIT | test automatici | INF (dopo F1) |
| Nerdbank.GitVersioning | MIT | versione derivata da git (non richiede git nel PATH) | INF |
| Microsoft.CodeAnalysis.CSharp | MIT | test di architettura (regole L4–L5) | F3–F4 |
| CommunityToolkit.Mvvm | MIT | ViewModel | F5 |
| Microsoft.Extensions.DependencyInjection | MIT | composizione dell'applicazione | F5 |
| Microsoft.VisualStudio.Threading.Analyzers | MIT | analisi di async void e attese | F5 |
| Markdig, YamlDotNet | BSD-2, MIT | fonti della Wiki con front-matter | W2 |
| DocumentFormat.OpenXml oppure Pandoc | MIT / GPL (strumento esterno) | guide Word e PDF dalle stesse fonti | W2.5 |
| python-docx, lxml, pypdf, pypdfium2, Pillow, reportlab | varie (MIT, BSD, Apache) | pipeline attuale delle guide, fino a W2.5 | W0 |
