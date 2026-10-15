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
    **Ripensamento dell'utente, 8/10 tarda mattina**: «il calcolo viene fatto secondo legame costitutivo coefficientato
    con alpha e gamma. quindi valori caratteristici + coeff x il design. ad ora lascialo così. ritiriamo fuori questo
    argomento a fine refactoring». U2 è sospesa: l'analisi non lineare resta con i legami di progetto, senza legami
    nuovi in Model e senza modifiche al solutore; R5 resta «da ridiscutere» in F5.15. Subito dopo: «metti il rimando
    alla guida accanto al tipo di analisi. l'avviso mettilo». Nella scheda Tensioni il campo «Analisi» ha il
    pulsante «?» verso la guida teorica («Materiali tendini e analisi di esercizio») e con «Non lineare» compare
    l'avviso: «Analisi non lineare: le tensioni di esercizio sono calcolate con i legami costitutivi di progetto
    (valori caratteristici ridotti con αcc e γ). La norma non prescrive il metodo dell'analisi tensionale; per le
    verifiche SLE il riferimento abituale è l'analisi lineare a sezione fessurata.» Solo interfaccia: calcolo e
    relazioni invariati.
  - U3 «correggi»: R22 si risolve applicando il fattore 0,8 dei getti sottili anche con UNI/DM 2012: limiti SLE del
    calcestruzzo, αcc e fcd con `gettato_sottile` = Sì.
  - U4 «tieni i limiti come oggi»: con CS-TR34 ANTHEA continua a calcolare i limiti tensionali SLE con i coefficienti
    di MC2010 da cui deriva `StandardCSTR34`. La proposta di allinearsi alla libreria (nessun limite) è respinta: la
    motivazione normativa non è verificata e togliere i limiti è meno cautelativo. In F2.7 l'adattatore chiama
    `StressLimitCheck.Evaluate` con lo standard dell'analisi, che per CS-TR34 calcola i limiti con i coefficienti
    della classe, e non usa mai `NotApplicableReason` (regola W5 del progetto): non serve una modifica di libreria.
  - U5 «correggi»: la scheda WPF rifiuta φ < 0, come già fanno i calcoli (`CheckerSection.cs:95`, `:127`).
  - U6 «metti in coda ad altre cose. non da fare ora»: la revisione delle pagine dei metodi ca.sle-tensioni e
    ca.fessurazione si sposta in coda.
  - U7 «correggi»: `staffe_presenti` si normalizza a Sì/No (riguarda solo documenti scritti a mano).
  Le correzioni U1, U3, U5 e U7 cambiano risultati o comportamenti: si fanno in F2.7 dopo l'interruttore, una per
  commit, con cattura prima e confronto dopo (regola delle correzioni in due commit), e si registrano nel registro
  delle differenze.
- **Dopo la prova della build** (8/10, mattina):
  - «l'aspetto è molto buono, ad ora mi sembra corretto. nel caso trovassi bug te li segnalerò».
  - «hai messo chiaro/scuro dentro Aspetto. rimuovila dalla finestra wiki in alto»: il selettore dell'aspetto non è
    più nella barra della Wiki; resta il menu Aspetto della finestra principale.
  - «non toccare più i report di calcolo. saranno corretti successivamente. ti dico io quando sarà fatto»: il codice e
    i testi dei report di calcolo (X.Core Report*, ReportWord, report WPF) non si modificano finché l'utente non lo
    dice. Le correzioni di calcolo approvate possono cambiare i valori stampati, ma non i testi né l'impaginazione.
    (U2, poi sospesa, avrebbe messo il suo avviso nell'interfaccia e nei risultati, non nella relazione.)
  - «anche la wiki mettila in coda ad altre cose»: la traccia Wiki (W) va in coda, dopo le fasi in corso.
- **Proposte del progetto F2.8 rivisto** (`supporto/artefatti/refactoring/f27-f28-progetto/F28-progetto-rivisto.md`,
  §12.1), risposte dell'utente dell'8/10 pomeriggio:
  - F2.8-U1 «si» (alla domanda se metterla in coda come U6): la revisione delle pagine dei metodi ca.dettagli,
    ca.ancoraggi e ca.momento-curvatura va in coda con U6.
  - F2.8-U2 «confermo b»: `rami_y` si valida nel punto d'ingresso dei dettagli con `ValidateStirrups` (intero fra 2 e
    100), come già fanno taglio e progetto delle armature. Si fa dopo l'interruttore I2, in due commit.
  - F2.8-U3 «ok»: tetto C60/75 di fctk,0,05 anche nei muri (R10), nel momento proposto dal progetto (F5.15).
  - F2.8-U4 «ok»: l'incoerenza di NEd fra la scheda dei dettagli e il progetto delle armature si tratta in F4.12.
  - F2.8-U5 «ok»: il M–χ nel report di progetto resta a F3.4, perché i report sono fermi.
  - F2.11: senza la revisione delle pagine dei metodi (U6 e F2.8-U1, in coda), il legacy di quelle famiglie resta
    finché l'utente non decide.
- **Scostamento della libreria F2.8 da §4 L3** (Checker `anthea-f2-8`, 53fb0011 e 05eb3cd2), solo informativo: la
  nuova `MomentCurvatureException` la lancia solo il sovraccarico con `MomentCurvatureUnits`. I metodi della 0.0.17.0
  lanciano ancora `ArgumentException` di tipo esatto, con lo stesso messaggio e il motivo in
  `Data["GPC.MomentCurvatureRejection"]`, perché il contratto L0 registra il tipo esatto (come K2 di F2.7). ANTHEA usa
  il sovraccarico con le unità (B3) e passa sempre la tolleranza su N nelle proprie unità: il default 1000 varrebbe
  1000 kN.
- **Crediti**: «supera il 15% ma cerca di non arrivare sopra i 90%. vorrei tenere un 8-10% a build chiusa».

## F2.9, durabilità e copriferri (8 ottobre 2026)

Progetto: `supporto/artefatti/refactoring/f27-f28-progetto/F29-progetto.md` del checkout principale. La parte ANTHEA (F2.9b,
passi E1-E6) è sul branch `refactoring/f2-9-durabilita` (worktree `Temp\aw-f29`), non unita e non pushata.

Le decisioni F2.9-D1…D14 sono **decise dal coordinatore su delega dell'utente («esegui tutto te», 7/10), da ratificare**
(proposta F2.9-U7). In ogni punto si è scelta l'opzione che lascia invariati comportamento, contratto JSON, testi e report.

| Decisione | Scelta | Dove |
| --- | --- | --- |
| F2.9-D1 Facciate | `Durability`, `NtcCover`, `MinimumConcrete` e `AtecapMix` restano con gli stessi nomi e firme, più il motore facoltativo finale `DurabilityEngine? engine = null`, e delegano all'adattatore. I corpi legacy sono in `DurabilityLegacy`, invariati. | E1, E2 |
| F2.9-D2 Parametro del motore | Solo su adattatore, facciate e punti d'ingresso propri della durabilità: `MaterialCover.Required`, `ConcreteCoverAnalysis.Calculate`, `ProjectValidation.CoverChecks` e `Warnings`. Non su `CalculationService`, muri, dettagli e progetto delle armature (lì prova la sonda). | E2 |
| F2.9-D3 Rifiuti | Tradotti per messaggio con un dizionario e due espressioni regolari, come per il taglio; nessun codice di rifiuto nuovo in libreria; un testo senza traduzione dà `InvalidOperationException`. | E2, registro F2-13 |
| F2.9-D4 Palo elastico | `PileDurability` resta com'è: chiama la libreria direttamente, con testi inglesi (proposta U3). Unica voce della libreria nell'elenco ammesso. | 11i |
| F2.9-D5 Funzioni senza chiamanti | `Durability.EffectiveWater`, `Durability.Strength` e `NtcCover.DefaultCmin` restano solo nel legacy fino a F2.11 (elenco ammesso). | E2, `f2-matrice-api.md` |
| F2.9-D6 e4 | Esclusa: serve una copia con licenza della UNI 11104:2025. Restano i valori della 2016 (prospetto 5); nessun atteso, testo o fixture cambia. | registro D7-e |
| F2.9-D7 Citazioni della libreria | Testi «senza e4» nei commenti, nel README e in `MIGRAZIONE_ANTHEA.txt` (CD1 di F2.9a, in Checker); il riferimento restituito a `ExposureClasses.cs:107` non cambia (proposta U2). | F2.9a: branch locale di Checker `anthea-f2-9-durabilita` (CD1 975cf006), non in develop |
| F2.9-D8 Interruttore | Uno solo: `ConcreteDurabilityAdapter.Default`, sulla libreria dal passo E5. | E5 |
| F2.9-D9 Baseline | B6 = base (B3) più il corpus di durabilità, catturata col legacy prima dell'interruttore; confronti esatti, nessuna tolleranza nuova. | E4 |
| F2.9-D10 Regole normative nelle viste | Dichiarate (progetto, §10); spostamento con CD3 ed E7 in F4.13. Nessun file WPF in F2.9b. | F4.13 |
| F2.9-D11 Release | Parte di libreria (CD0-CD2) in S3 se entra in develop prima del commit delle versioni (C5); altrimenti nella release successiva. ANTHEA usa solo l'API 0.0.17.0. | F2.9a |
| F2.9-D12 Stato statico | Catalogo delle esposizioni `ImmutableArray`; array dei copriferri dei muri `ImmutableArray`; `ExposureClasses.All` di sola lettura in libreria (CD2). | E1, 11g |
| F2.9-D13 Etichette delle classi minime | Con la libreria, `MinimumConcrete.Label` prende il nome dal catalogo dei calcestruzzi della scheda, solo per le classi minime del catalogo delle esposizioni (12, 25, 30, 32, 35 MPa); per gli altri valori lo stesso rifiuto di prima. | E2, 11d |
| F2.9-D14 Sonda | La base non ha la sonda generalizzata di F2.7 (A4): l'adattatore ha una sonda propria con la stessa forma (adattatore, operazione, motore), da unificare in F2.11. | E2 |

Proposte all'utente, non attuate (progetto, §12): **U1** e4 dopo il riscontro del prospetto 6 su una copia con licenza;
**U2** testo del riferimento restituito a `ExposureClasses.cs:107`; **U3** traduzione dei testi del palo elastico, quando i
report si potranno toccare; **U4** ripieghi silenziosi della scheda Materiali (F3.1); **U5** testi di origine esterna della
scheda (ATECAP 2020); **U6** revisione della pagina del metodo `ca.durabilita-copriferri` prima di F2.11; **U7** ratifica di
F2.9-D1…D14.

Restano al coordinatore: F2.9a in Checker (CD0-CD2), profilo full a schermo per chiudere E5 (insieme a F2.7 e F2.8), copia della
baseline B6 nel checkout principale (`supporto/artefatti/baseline/F2-B6`), unione su main dopo l'approvazione dell'utente.
F2.9a l'ha già fatta un'altra corsia sul branch locale di Checker `anthea-f2-9-durabilita` (cinque commit b4ec06df…6543860d da
develop 4f54139a, worktree `Temp\gpc-s3-dev-d\Checker`; non in develop, non pushato). La corsia di F2.9b non l'ha verificata:
restano la revisione e l'unione in develop prima di C5.

Da far presente nella ratifica (revisione di F2.9b, 8/10):
- **Ripieghi del corpus B6.** Il progetto (§8.2) dice che ogni documento del corpus dichiara le chiavi lette, così non nascono
  ripieghi nuovi. In realtà i casi nuovi aggiungono 369 voci a `fallbacks.json`: 249 dei muri W1-W3 e 120 delle sezioni S1-S3,
  nessuna delle schede Materiali. Tutte stanno nei 133 gruppi di B3 e nessun gruppo è nuovo; le 2848 voci di B3 sono invariate e
  dopo E5 `fallbacks.json` resta identico byte per byte. Lo scopo è raggiunto, la formulazione del progetto no.
- **Muri nel progetto.** Il progetto è incoerente: §13.1 dice che `RetainingWall.*` resta invariato, mentre E1 e §9 chiedono
  `ImmutableArray` in `RetainingWall.Materials.cs`. La corsia ha seguito E1 (`RetainingWall.Materials.cs:47-52`). I branch di F2.7
  (`refactoring/f2-7-sle-fessurazione`, `refactoring/f2-7-prototipo`) e di F2.8 (`refactoring/f2-8-a0-cattura`) partono anch'essi da
  98a21d4 e non toccano quel file: nessun conflitto atteso.
- **Durata delle prove dell'adattatore.** Con la sezione 11 `tests/ConcreteLibraryAdapter.Checks` passa da circa 7 s a circa 60 s
  (143 s il 9/10, con le prove SLE di F2.7). Decisione dell'utente del 9/10 («confermo la 2»): nello stadio fast resta
  `ConcreteLibraryAdapter.Checks/rapido` (`--rapido`: sezioni 1-4 e interruttori della durabilità e delle SLE, 7,7 s); la corsa
  completa con `misura.json` passa nello stadio regression (profili standard, baseline e full).

## Dipendenze esterne previste

Da approvare una volta; ogni variazione si aggiunge qui.

| Pacchetto o strumento | Licenza | Uso | Fase |
| --- | --- | --- | --- |
| MSTest.TestFramework, MSTest.TestAdapter, Microsoft.NET.Test.Sdk | MIT | test automatici | INF (dopo F1) |
| Nerdbank.GitVersioning | MIT | versione derivata da git (non richiede git nel PATH) | INF |
| Microsoft.CodeAnalysis.CSharp | MIT | test di architettura (regole L4–L5) | F3–F4 |
| CommunityToolkit.Mvvm 8.3.2 | MIT | ViewModel del nuovo modulo Modelli; altri moduli in F5 | Modelli / F5 |
| HelixToolkit.Wpf.SharpDX 3.1.2 | MIT | Renderer WPF/Direct3D del nuovo modulo Modelli | Bozza Modelli |
| Microsoft.Extensions.DependencyInjection | MIT | composizione dell'applicazione | F5 |
| Microsoft.VisualStudio.Threading.Analyzers | MIT | analisi di async void e attese | F5 |
| Markdig, YamlDotNet | BSD-2, MIT | fonti della Wiki con front-matter | W2 |
| DocumentFormat.OpenXml oppure Pandoc | MIT / GPL (strumento esterno) | guide Word e PDF dalle stesse fonti | W2.5 |
| python-docx, lxml, pypdf, pypdfium2, Pillow, reportlab | varie (MIT, BSD, Apache) | pipeline attuale delle guide, fino a W2.5 | W0 |

## Modelli di calcolo — decisione del 9 ottobre 2026

L'utente autorizza una bozza integrata in ANTHEA su branch autonomo, richiede WPF con MVVM e chiede di non modificare
per ora gli altri progetti. Il modulo Modelli è distinto dai fogli; ogni contenitore del progetto ospita più modelli
e più fogli. La Wiki resta trasversale. ViewModel e adattatore sono senza WPF; il renderer è sostituibile e non esegue
verifiche né combinazioni. Gli interventi nelle librerie GPC e nei moduli esistenti sono soltanto elencati come seguito.
Vedi [specifica della bozza](../sviluppo/modelli-visualizzatore.md), registro MOD-01.

Le due dipendenze della bozza sono nella tabella unica precedente, comprese nell'autorizzazione al visualizzatore e a MVVM.
Le licenze sono quelle dichiarate nei pacchetti. CommunityToolkit.Mvvm 8.3.2 è anche la versione richiesta da Helix 3.1.2.
Versioni fissate per la bozza; non è un aggiornamento delle altre dipendenze di ANTHEA. Restano da valutare nel collaudo
per il rilascio anche ciclo di vita e manutenzione delle dipendenze transitive del renderer.

### Estensione del 9 ottobre: gestione del modello e tabelle

L'utente autorizza successivamente anche le integrazioni necessarie per assi, restrain, constraint, release e link
nel repository Model, purché in branch separato. Il Converter viene quindi esteso nel ramo
codex/viewer-boundary-data (14a0c859); il checkout delle sezioni e il bundle lib/Checker restano invariati.
Il ponte tools/ModelViewer.Prepare usa esplicitamente quel checkout e produce un pacchetto di presentazione.

ANTHEA mantiene uno spazio Modelli distinto dalla navigazione del progetto, tre tab a sinistra, proprietà a destra
e tabelle associate alla scena. Colori/ID/visibilità e unità sono stato della vista. Le tabelle proiettano i dati
salvati, senza imporre la struttura di un solutore e senza modificarli. I campi di risultato conservano famiglia,
assi, campioni e convenzioni; componenti mancanti o non simultanee non vengono ricostruite nella UI.
Il confronto funzionale con il manuale del solutore non introduce testi o figure di terzi nelle guide.
Guide globali e PDF rimangono attività di rilascio dopo il collaudo e l'assestamento del refactoring.
Vedi registro MOD-02 e specifica del modulo per copertura effettiva, evidenze e integrazioni ancora necessarie.

### Estensione dell'11 ottobre: filtri e scale di visualizzazione

Su richiesta dell'utente si considera anche RFEM come riferimento funzionale per navigazione, visibilità e pannello
dei risultati. Nessun testo, figura o contenuto della documentazione esterna è copiato nella UI o nelle guide.
Nel modulo vengono aggiunti criteri combinabili per attributi, visibilità con nome temporanee e scale di colore
globali, simmetriche e manuali. Sono stato della vista: non mutano i dati del modello né i campioni delle sollecitazioni.
Le proprietà non assegnate compaiono nelle tabelle senza filtri. Clipping, viste persistenti con camera, diagrammi,
sezioni di risultato e filtri numerici restano nel seguito della specifica.

Il bundle S4/Model 4 di main è stato acquisito senza ricompilarne o sostituirne le DLL. Il collaudo completo di ANTHEA
ha dato 79 PASS e un solo KNOWN già censito; le suite GPC 1196 PASS. Il merge 7852f67 del precedente lavoro è già
presente in main alla ripresa; le nuove aggiunte continuano sul ramo codex/model-viewer-draft, senza push o merge inverso.

### Estensione dell'11 ottobre: pannelli riducibili e terne nodali

L'utente richiede tutti gli alberi della vista riducibili a barra laterale, selezione/isola/nascondi per
esplorare gruppi e proprietà, vincoli e assi locali nodali e degli elementi disattivabili. La navigazione
delle tre schede e le proprietà collassano separatamente; la tabella si apre sugli elementi associati.
Le terne nodali entrano nel contratto come dati opzionali normalizzati da GPC. Non si ricavano nella UI
né si sostituiscono con assi globali quando il Converter segnala un orientamento irrisolto.
Interruttori individuali, ambito della selezione e spegnimento collettivo restano stato della vista.
Il ponte di sviluppo usa GPC esistente senza cambiare altri repository o il bundle distribuito.
Alla domanda sul pulsante Aggiorna modello si chiarisce che ora rilegge una copia da file; l'acquisizione
diretta dal software e il controllo delle revisioni modello/analisi rimangono nel piano del modulo.
L'utente approva e precisa quattro modalità per quel flusso: Solo geometria, Geometria + attributi + carichi,
Solo risultati, Tutto. Solo geometria abilita anche visualizzazioni semplici senza risultati.
La specifica distingue questa decisione dalle funzioni oggi operative ed esplicita la coerenza di revisione
e l'invalidazione dei dati incompatibili nei caricamenti parziali.

### Avanzamento delle verifiche e punto di arresto

L'utente richiede la progress bar per il futuro lancio delle verifiche e chiede di fermarsi a una vista
utilizzabile per provare il modello. Si prepara quindi soltanto il contratto di avanzamento e la vista WPF,
con annullamento cooperativo, notifiche sul contesto UI, stato finale distinto dall'esito ingegneristico
e nessuna esecuzione fittizia nel prodotto. Le assegnazioni delle sezioni alla geometria restano una proposta
nella specifica; il pulsante Esegui è disabilitato fino al collegamento GPC. Il runner WPF può aprire il vero
controllo in modalità interattiva su una copia di prova, senza avviare le suite o le verifiche.

### Vista e indisponibilità del solutore — 11 ottobre

L'utente richiede controllo della camera e della qualità grafica, Esc e confronto delle revisioni.
Si aggiungono impostazioni di sessione e comandi nel ViewModel; il code-behind applica solo le proprietà
del renderer e gli eventi WPF. Il confronto documentale nasce nella libreria senza WPF; interfaccia e
storico restano da completare dopo la nuova priorità assegnata all'esperimento sui casi base.
Le interruzioni reali della sessione MIDAS motivano un contratto tipizzato di errore, con recupero leggibile
e conservazione della copia valida. La vista non riceve credenziali né il corpo del messaggio HTTP.
È una predisposizione collaudata: il connettore diretto nel prodotto rimane nel piano. Registro MOD-06.

### Esperimento sui casi base — 11 ottobre

Su richiesta esplicita dell'utente si interrompe l'acquisizione massiva di tutti gli inviluppi e si acquisiscono
i 49 casi statici per misurare la costruzione locale delle 73 combinazioni/inviluppi. Il prototipo C# resta
in tools, senza dipendenze dai progetti di produzione: non anticipa una release del motore GPC.
Il dataset completo è acquisito e il confronto della combinazione sulle plate supera 109.744 campioni;
il confronto completo dell'inviluppo è limitato dal timeout MIDAS di 180 secondi, dichiarato nel rapporto.
Si conservano i risultati e le prove senza sostituire gli attesi. Prima dell'integrazione occorrono il contratto
GPC, i casi governanti, la semantica di simultaneità e l'archivio dei grandi campi. L'utente vuole partire da
queste misure per le considerazioni successive. Vedi [benchmark](../sviluppo/modelli-benchmark-casi-base.md), MOD-07.

### Media ai vertici del contouring — 11 ottobre

L'utente chiede una modalità aggiuntiva con i punti mediati sui vertici. Si implementa come operazione di
presentazione senza WPF, opzionale e reversibile: media aritmetica dei contributi delle plate compatibili,
con separazione per proprietà, materiale disponibile e terna locale importata. Niente trasformazione dei
risultati tra assi diversi, ricostruzione di campioni mancanti o modifica degli estremi nei dati salvati.
Il vicinato è quello del modello intero, indipendente dalla visibilità; tabelle e riferimenti restano originali,
la legenda usa il campo grafico e dichiara la media. Gli inviluppi restano estremi non simultanei.
Il controllo e le impostazioni condividono lo stato MVVM. Nessuna modifica degli altri progetti o delle
librerie GPC. Evidenze prima/dopo e collaudi sintetici/reali nel registro MOD-08; guide globali al rilascio.


### Esplorazione della vista e prova autonoma — 11 ottobre

L'utente autorizza le sistemazioni grafiche proposte e chiede una posizione coerente nelle schede,
rimandando le verifiche. Si distinguono dati del modello, campo di risultato e impostazioni della
camera/rappresentazione; gli strumenti frequenti di selezione restano sopra la scena.
Stato e comandi rimangono MVVM senza WPF, mentre hit testing, proiezione, ritaglio e GPU sono nel renderer.
Preferenze persistenti e lettura dei carichi sono servizi documentali senza WPF.
La deformata usa esclusivamente spostamenti globali importati, con contratto opzionale retrocompatibile:
non si inferiscono deformazioni dalle sollecitazioni. Carichi non riconosciuti sono dichiarati, non trasformati
in forze equivalenti. Il confronto affiancato non interpreta l'inviluppo come stato simultaneo.
Su richiesta successiva si apre subito una copia autonoma della build di test, mantenendola separata
dai collaudi e dalle ricompilazioni. Nessun push, merge su main o cambiamento agli altri repository.
Evidenze e limiti nel registro MOD-09 e nella specifica del modulo; guide globali al rilascio.


### Impostazioni uniche e composizione dei report — 11 ottobre

Su richiesta dell'utente si eliminano i pannelli concorrenti: Vista e Risultati sono le sedi
uniche delle rispettive impostazioni, mentre Report salva i riferimenti grafici necessari
alla rigenerazione e non propone copie degli stessi editor. Le proprietà del documento
(risoluzione e sfondo da stampa) sono override espliciti e non modificano la vista di lavoro.
Si introduce ANTHEA.ModelReports senza WPF per contratti, persistenza e composizione, usando
PDFsharp/MigraDoc Core con licenza MIT. Il rendering resta nell'adattatore WPF ed è isolato
dalla scena interattiva. L'esportazione comprende sempre il sorgente modificabile e il PDF.
Le verifiche restano rinviate: il capitolo conserva il riferimento al foglio e indica
esplicitamente l'assenza degli esiti. Nessun nuovo calcolo ingegneristico in ANTHEA.
La prima ricetta è portabile, esterna al progetto; integrazione nell'archivio, tabelle e
aggiornamenti incrementali sono passi successivi dichiarati. Nessun push, merge su main,
modifica delle librerie GPC o pubblicazione delle guide del prototipo. Registro MOD-10.


### Collocazione delle modalità geometriche — 11 ottobre

Geometria e Volumi e offset passano dalla barra superiore alla scheda Vista, su richiesta
dell’utente. Il comando resta nel ViewModel e mantiene aperta Vista; la scheda Risultati è
l’accesso alla rappresentazione delle sollecitazioni. Nessuna modifica del renderer. MOD-11.

### Risultati sovrapposti a geometria e volumi — 11 ottobre

Risultati è un livello attivabile sopra la rappresentazione geometrica. Il cambio tra superficie
analitica ed estrusione non spegne il campo. Plate: le facce estruse ripetono la sollecitazione
importata, senza interpretarla come distribuzione nello spessore. Aste: diagrammi e contouring
hanno interruttori distinti; si interpolano solo stazioni importate. Il volume dipende dai profili
reali disponibili nella copia; per profili non supportati si mantiene l'asse. Nessun calcolo
strutturale né aggiornamento delle librerie GPC. Stato interamente MVVM, geometria e materiali
nel renderer WPF, persistenza delle preferenze nella libreria non WPF. MOD-12.

### Deformate e temi del visualizzatore — 11 ottobre

La deformata usa esclusivamente traslazioni nodali globali importate. Forma, campo colorato e
rappresentazione geometrica sono indipendenti. Spessori e offset conservano la direzione
originaria, perché il contratto attuale non contiene rotazioni di sezione; nessuna ricostruzione
di curvature dalle sollecitazioni. Gli elementi con dati incompleti restano come contorno grigio
nella posizione originale. Un inviluppo per componenti non genera una forma deformata né un
modulo vettoriale da estremi non simultanei.

L'animazione interpola graficamente tra due configurazioni riutilizzando gli stessi buffer;
non rappresenta il tempo dell'analisi. Colori e valori non vengono scalati. La pausa ricostruisce
la vista completa alla fase corrente; esportazione e salvataggio congelano una fase esplicita.
Taglio e confronto affiancato restano disponibili in forma statica. Preferenze, comandi e stato
sono MVVM; timer e buffer appartengono alla vista/renderer. Import e persistenza restano senza WPF.

Si riusano i tre aspetti globali di ANTHEA, senza un secondo sistema di temi nel modulo. I test
non modificano la preferenza salvata. Lo sfondo del modello rimane una scelta grafica indipendente
e i colori ingegneristici non cambiano con l'aspetto. Nessuna modifica alle librerie GPC. MOD-13.

## Pacchetti Modelli con più casi (11 ottobre 2026)

Il limite preliminare di 64 MB impediva di aprire il pacchetto reale dei 49 casi, pur avendo
solo 3.755 nodi e 3.616 elementi. Il limite esplicito è ora 256 MiB e la lettura del file usa
lo stream, con cancellazione. Anche SHA-256 viene alimentato in streaming: ordinamento e
serializzazione UTF-8 rimangono identici, senza invalidare i riferimenti salvati. Il formato
del progetto non cambia; un archivio dedicato dei grandi risultati rimane un'evoluzione distinta.

## Diagrammi delle aste e report (11 ottobre 2026)

Il dettaglio 2D usa gli stessi selettori di caso, componente e unità della vista 3D. Il pannello
inferiore alterna tabella e diagrammi; non duplica la configurazione. È disponibile per beam,
truss e altre famiglie lineari riconosciute, limitatamente ai campioni importati. Segmenti
rettilinei fra stazioni, nessuna estrapolazione e nessuna curva di analisi ricostruita. Un valore
isolato resta un punto; stazioni ambigue non vengono mediate. Il cursore distingue il campione
originale dall'interpolazione grafica. I sei grafici mantengono le rispettive unità.

Le figure di report salvano elemento, caso, componente o tutte le componenti, unità e cursore.
Alla rigenerazione i grafici vengono ricreati dai valori correnti; riferimenti mancanti bloccano
la figura. La forma d'onda e i dati strutturali non si modificano. Registro MOD-15.

## Volume di ritaglio (11 ottobre 2026)

Il volume conserva la porzione interna a sei piani globali. Mesh e linee sono tagliate, non solo
nascoste per baricentro; ID e coordinate del colore rimangono associati alla geometria. Nessuna
faccia di chiusura o distribuzione nello spessore viene inventata. Il contorno si può spegnere.
La scala del campo resta riferita ai campioni degli elementi attivi, anche fuori dal taglio,
con indicazione nella vista e nel report. Deformata statica ammessa, animazione sospesa. MOD-16.

## Misure e camera (11 ottobre 2026)

Le operazioni distanza/angolo sono delegate a GPC.Geometry già distribuita; ModelWorkspace
adatta i punti importati e le traslazioni relative, senza nuove formule geometriche in WPF
o Presentation. Nessuna DLL viene aggiornata. Le misure reali non dipendono dalla scala
grafica e richiedono dati simultanei completi. Inviluppi e riferimenti mancanti restano
indisponibili; il report rifiuta riferimenti invalidi. Il disegno segue la forma visualizzata
con una base della misura sempre esplicita. Esc termina lo strumento e deseleziona.

Inquadra mantiene orientamento e tipo di proiezione dopo la prima apertura; sul taglio usa
i limiti grafici attivi. Proiezione di etichette e selezione dalla camera corrente, non dal
contesto GPU del fotogramma precedente. MOD-17.

## Confini dei plate visibili (11 ottobre 2026)

La topologia del renderer dipende dai plate effettivamente rappresentati, inclusa la completezza
dei dati nodali della deformata. La cache si aggiorna solo quando cambia questo insieme.
Le facce interne vengono soppresse solo fra due plate complanari con stessa proprieta;
confini diversi o non manifold rimangono. Il taglio grafico conserva il contratto separato
senza facce artificiali. Cattura prima/modifica/confronto in `visible-boundaries`. MOD-18.

## Check della revisione (11 ottobre 2026)

Si collega il servizio documentale esistente alla UI. Confronto automatico fra copie al termine
di una importazione valida, oppure caricamento esplicito della copia precedente senza cambiare
il modello corrente. Solo ID corrispondenti, tolleranza coordinate 1 micrometro. Si conserva
in memoria l’ultimo confronto; annullamento e fallimento mantengono quello valido.

Il delta e asincrono, per il campo scelto nei Risultati e soltanto su richiesta. Le geometrie
cambiate sono escluse inizialmente, includibili con indicazione esplicita. Si confrontano valori
importati, senza medie grafiche; su inviluppi si sottraggono estremi, non si crea un inviluppo
della differenza. Le annotazioni salvano provenienza e sagome precedenti; il PDF ne verifica
la revisione corrente, bloccando annotazioni obsolete. Per ricomputare il delta dopo riapertura
serve la copia precedente con i risultati: non viene incorporata nelle preferenze grafiche.
Archivio storico e connessioni live rimangono distinti. MOD-19.

## Colori per categoria e aggiornamenti parziali (11 ottobre 2026)

I colori personalizzati sono preferenze della vista, identificati da modalità e chiave della
categoria, separati dai dati importati. La legenda mostra le categorie visibili; l’editor
mantiene anche quelle nascoste. Seleziona agisce sui visibili; Isola/Nascondi/Mostra su tutta
la categoria. In caso di gruppi sovrapposti resta la regola deterministica del gruppo più
specifico. L’albero continua a mostrare tutte le appartenenze. Ricette, confronto e report
usano gli stessi colori. MOD-20.

L’aggiornamento delle copie distingue solo geometria, geometria/attributi/carichi, soli risultati
e tutto. Solo geometria include le dimensioni reali delle sezioni, spessori, offset e terne
necessarie alla rappresentazione, ma esclude materiali, gruppi, condizioni al contorno e carichi.
I primi due modi eliminano i risultati dalla nuova copia: non si conserva un campo precedente
su un modello cambiato. I soli risultati sostituiscono tutti i casi e richiedono identità di
nodi, elementi, proprietà, terne, vincoli e tabelle acquisite. Si tollera soltanto l’ordine
degli oggetti JSON e la scrittura numerica equivalente; non si rimappano nodi o elementi.
Provenienza e ambito sono salvati nel pacchetto. Annullamento/rifiuto conservano la copia valida.
La lettura diretta dai software resta un adattatore da integrare. MOD-21.

## Avvio di prova, temi e navigazione (11 ottobre 2026)

La preview può aprire direttamente un pacchetto, senza costruire un archivio Progetti temporaneo.
Ospita sempre il controllo di produzione; non introduce un renderer alternativo. Le risorse
di stile dell’applicazione sono incorporate nel runner per renderlo avviabile da altre cartelle.
Lettura in background con indicatore e annullamento alla chiusura della finestra di caricamento.
Le modifiche del modello nella preview rimangono in memoria. MOD-22.

CheckBox ed Expander del modulo hanno template con le risorse del tema, mantenendo binding,
tastiera e automazione WPF. Il cubo Helix usa una scala 1,1: il precedente 110 era stato inteso
erroneamente come pixel. Posizione adattiva, texture propria, selezione distinta dal modello.
Dopo la sostituzione della camera si invalida esplicitamente il frame: il solo cambio della
proprietà Camera non aggiorna il contesto usato dal cubo nella versione distribuita.
Tabelle/diagrammi/revisioni riducono la propria altezza per conservare la viewport. MOD-23.

## Lettura dei versi e delle condizioni al contorno (11 ottobre 2026)

Le frecce seguono la connettività importata I→J / N1→N2; non sostituiscono le terne locali.
Le etichette dei vincoli riportano il testo dei DOF salvato, senza dedurre rigidezze o cerniere.
I collegamenti con stesso primo nodo, tipo, elemento e testo sono raggruppati soltanto per
l'etichetta, con conteggio esplicito; la tabella conserva ogni assegnazione. Massimo 40 etichette,
limite anti-sovrapposizione e clipping; disabilitate durante l'animazione. Scala dei simboli
separata da geometria e risultati. Stato conservato in viste, ricette e confronto. MOD-24.

Le aree degli strumenti sopra e sotto l'albero hanno scorrimento e altezza massima relativa;
l'apertura degli espansori non può più eliminare l'albero o rendere irraggiungibili i comandi.
La cattura precedente e quella corretta restano fra le evidenze del passo.

## Letture geometriche nel visualizzatore (11 ottobre 2026)

Il ViewModel richiede il modulo delle traslazioni e la lunghezza dell'asta all'adattatore
senza WPF ModelMeasurements, che riusa Vector3d.Length della DLL GPC già distribuita.
Per i vettori si mantiene la normalizzazione numerica di ingresso; nessun nuovo algoritmo
strutturale e nessun aggiornamento del bundle. Le formule di interpolazione/animazione e
inquadratura rimangono operazioni esclusivamente grafiche. Catture prima/dopo identiche
per vettori nulli, ordinari, molto grandi e molto piccoli e per la posizione lungo l'asta.
Attesi indipendenti invariati. MOD-25.

## Preview locale ed esportazione del controllo (11 ottobre 2026)

La preview confezionata riusa il controllo di produzione, richiede il runtime Desktop .NET 8,
apre la copia offline e mantiene le importazioni in memoria. Tre avviatori: contouring,
deformata e aste con grafici 2D. Le viste salvate sono nella cartella della preview; il servizio
WPF riceve uno store opzionale, mantenendo il percorso dell'applicazione quando non fornito.
Manifest con commit, stato dell'albero e SHA-256 dei file, rifiuto di cartelle già popolate.
Nessuna credenziale, connessione MIDAS, installazione o pubblicazione esterna. MOD-26.

La cattura del controllo deve usare l'area effettivamente disposta da WPF, inclusa la sua
traslazione rispetto al contenitore. I limiti dei discendenti Direct3D possono conservare
le dimensioni precedenti: non sono adatti a determinare la scala dell'immagine. Bitmap
caricata da stream, senza dipendere dalla cache degli URI. Prima/dopo e prova con margini
e ridimensionamento conservati. I nuovi host di prova sono registrati con Appearance.Watch
prima di mostrarli, come le finestre dell'applicazione, per applicare anche le barre di
scorrimento al primo avvio scuro. Nessuna modifica al gestore dei temi dell'applicazione.

## Cattura dopo un cambio di disposizione (11 ottobre 2026)

Prima di acquisire il frame Direct3D si completa il layout WPF e si aggiorna la dimensione
del render host. Altrimenti l'apertura dei diagrammi può lasciare una cattura alta 870 pixel
ridotta dentro una viewport alta 270 pixel fisici. Il controllo immediato ora acquisisce
le dimensioni corrette, senza dover aspettare manualmente un fotogramma. MOD-27.

Il collaudo standard sulla build e25b7ed chiude con 43 PASS, un solo KNOWN già censito e
nessun nuovo fallimento. La prima batteria intensiva di schermate non ha prodotto l'esito
finale durante l'esecuzione parallela e non è conteggiata come superata. La ripetizione
isolata, con attesa esplicita del processo, termina con codice 0 e 138 controlli PASS.
Diagrammi/report: 17 PASS; deformate/report: 9 PASS; otto pagine PDF ispezionate.

## Collegamenti dei modelli — 12 ottobre 2026

Su richiesta dell'utente, il ramo `codex/model-live-import` aggiunge il collegamento MIDAS
nel modulo e predispone Straus/Strand7. Trasporto, lettura, preparazione dei file e confronto
dei dati rimangono in ANTHEA.ModelWorkspace; stato e comandi nella Presentation, dialoghi e
PasswordBox nella WPF. Nessun nuovo calcolo strutturale, modifica del set GPC o degli altri progetti.

Una lettura fallita non aggiorna il documento. Non si ritenta automaticamente una query risultati
che potrebbe essere ancora in esecuzione in MIDAS. Il confronto finale delle tabelle intercetta
modifiche dell'input, ma non certifica l'identità di un'analisi rieseguita a input invariato.
La selezione dei casi sostituisce l'intero insieme dei risultati, come le modalità già concordate.

Non distribuire DLL del convertitore prese dal checkout locale per aggirare il rilascio coerente.
Se i dati sorgente sono identici e completi, conservare le normalizzazioni della copia precedente;
in caso contrario dichiarare le informazioni non normalizzate. Straus R3 viene predisposto con
controllo dei file e contratto del worker, senza simulare disponibilità di importazione, ABI o licenza.
Il primo collaudo reale di tutti i 49 casi MIDAS ha incontrato una sessione interrotta; la nuova
acquisizione completa del solo G1 coincide esattamente con il riferimento. MOD-28 e MOD-29.

## Gestione delle combinazioni — 12 ottobre 2026

Il ramo `codex/model-combinations` integra il catalogo delle combo nel modello, con editor e griglia
WPF, pannello riducibile e finestra staccabile. Le modifiche alle definizioni importate sono
sostituzioni locali; le cancellazioni conservano le tabelle originali e possono essere ripristinate.
Il generatore automatico è rinviato come richiesto. Il calcolo delle principali resta il passo dopo
combinazioni e inviluppi, senza sommare principali o vettori di estremi indipendenti.

Si usa GPC.Converter 2.0.3.0 del set R già rilasciato, con Model 5 e tutte le altre DLL immutate:
nessuna versione o sorgente esterna modificata. Preparazione di geometria/casi base una sola volta
per input; calcoli GPC senza attaccare risultati al modello preparato, pubblicazione atomica della
copia ANTHEA. Le definizioni sono separate dall'identità della cache di normalizzazione.
Si conservano riferimenti, alternativi governanti e vettori simultanei. Le rotazioni mancanti dei
vecchi pacchetti non vengono inventate. Contratti e limiti in `docs/sviluppo/modelli-combinazioni.md`.

La prova completa SLU_Q1_1(max) plate coincide con MIDAS su 109744 valori. La ricostruzione a caldo
misurata è 7,606 s: non è dichiarato raggiunto l'obiettivo di 5 s. Ottimizzazioni ulteriori del motore
GPC richiedono una fase nelle librerie e un successivo rilascio coerente; nessuna scorciatoia numerica
nella UI o dichiarazione di supporto per famiglie/analisi non collegate. MOD-30.

## Riuso delle API distribuite e rimozione dei sei motori esclusi (12 ottobre 2026)

L'utente chiede di usare le API di Model/Checker, eliminare i duplicati sostituibili,
segnalare i residui e compilare tutto. Autorizza poi espressamente la cancellazione
di BishopSolver, FoundationSettlement, NewmarkSliding, ShallowFoundationSeismic,
SlopeGeometry e SlopeStability. Erano già esclusi dalla compilazione: le funzioni
sono in GPCChecker.Geotechnics con test di migrazione. Rimossi solo questi sei file.

Riuso completato entro il perimetro provato nell'[audit](audit-contratti-moduli-20261012.md),
registro API-01. La cattura estesa rivela un difetto di raggruppamento degli interassi
circolari (API-02): nessuna tolleranza allargata per accettarlo. Conservo interassi,
dettagli e fessurazione circolari sul percorso precedente fino alla correzione della
libreria e al rilascio coerente. È una scelta conservativa dell'intervento, non
un'approvazione dell'utente del valore errato. Il confronto non è una nuova
validazione normativa dei metodi e non chiude i residui F4.

Verifica conclusiva: runner baseline 52 PASS, 1 KNOWN già censito, 0 NEW-FAIL;
548 file e 50.996 righe confrontati, nessuna differenza non ammessa. Tolleranze
relative 1e-12 limitate ai campi mutati, registrate nei commit 1b3c38f e d69b97d
prima dell'implementazione. Attesi indipendenti immutati. Build soluzione e app
Release completate. Nessun push, cambio di versione o aggiornamento di lib/Checker;
resta l'attesa delle versioni esplicitamente riservate all'utente.

## Integrazione F4 e combinazioni (12 ottobre 2026)

L'utente autorizza l'unione dei rami, l'aggiornamento coerente delle DLL, la compilazione e, solo con verifiche positive, il merge su main e il push. Le versioni delle librerie sono curate direttamente dall'utente: nessuna versione assegnata dall'agente e nessun nuovo snapshot installato prima del suo avviso.

I conflitti riguardano la documentazione condivisa: si conservano entrambi i contributi e si rigenera l'indice Wiki. La guida pratica Rev35 riunisce fondazioni e importazione Modelli; la teorica resta alla Rev34. Le revisioni di origine sono archiviate, con impronte, in `supporto/SUPERATI/merge-f4-modelli-rev35-20261012`. Il tentativo di merge avviato anche nel checkout principale è conservato in `supporto/artefatti/merge-f4-combinations-20261012/backup-tentativo-utente`; il checkout sarà allineato all'unico commit di integrazione completato.

## Chiusure pendenti — 12 ottobre 2026, sera

L’utente chiede di completare i punti già avviati, con un commit per punto chiuso e
un aggiornamento dopo ogni passaggio; al termine si concordano le attività nuove.
Si lavora su `refactoring/f3-chiusure-pendenti`, senza merge o push di questa fase.

F3.3a: il test dei default confronta i dati, mantenendo casuali e indipendenti gli
identificativi delle azioni create. Solo `actions[*].id` dei nuovi default dei muri
viene proiettato per posizione, dopo il controllo di GUID non vuoti e univoci.
Tutti gli altri valori e riferimenti restano soggetti a uguaglianza esatta. Il
confronto opera su copie e non modifica né il documento né gli attesi numerici.
La chiusura elimina l’eccezione del runner; non anticipa lo spostamento in Application.

F2.9: chiusa la migrazione tecnica CD0-CD2/E1-E6 dopo verifica del set S,
del full già eseguito e della baseline B6 presente nel checkout principale.
Le attese di merge, rilascio e copia della baseline annotate l’8 ottobre sono
superate. Le proposte normative e di contenuto restano aperte; la chiusura non
vale come ratifica. Evidenze in `chiusura-f29-20261012.md`.

Rilascio Modelli: ricondotte allo stato corrente le vecchie attese di integrazione,
rilascio, guide e merge. Queste attività sono già concluse in main/origin
`9b42f093`, collaudate con il set S e documentate dalle guide Rev36. Il connettore
MIDAS e le combinazioni locali sono disponibili; storico completo, worker Straus,
prestazioni e componenti principali restano voci distinte e aperte. Questo
aggiornamento del piano non esegue nuovi merge, push o rilasci.

API-02a: correzione della libreria pronta e verificata, commit Checker `e1a9c78e`.
La prova geometrica riproduce il difetto prima della modifica e controlla il
risultato indipendente dopo. Non si assegnano versioni e non si installano DLL
intermedie: API-02b resta aperto per il rilascio coerente e l’attivazione in ANTHEA.

F2.7-A11: prosecuzione del codice di fase 2 su `refactoring/f2-chiusure-pendenti`,
derivato dalle prime chiusure; il ramo F3 resta disponibile. Le viste e la
validazione dei muri leggono il catalogo di sola lettura della libreria tramite
la mappatura. Il riferimento legacy resta indipendente e immutabile. Nessun
cambiamento di risultato nei 548 file e nelle 50.996 righe dense; tre suite WPF
superate. Rimossa la relativa eccezione del controllo sullo stato statico.

## Delega delle versioni — 12 ottobre 2026

L’utente revoca la riserva sull’assegnazione dei numeri e chiede di aggiornare AGENTS.md.
Gli agenti incrementano l’ultimo componente per i fix e il secondo da sinistra negli
altri casi, azzerando i successivi. API-02 è un fix geometrico: Concrete passa da
0.0.27.1 a 0.0.27.2, senza cambi di API o di metodo normativo. La delega riguarda i
numeri; non sostituisce i controlli del rilascio coerente né autorizza nuovi merge
o push. Le precedenti annotazioni sulla riserva delle versioni descrivono lo stato
storico e sono superate da questa decisione.

F2.11a: riconciliata la voce dei sei file geotecnici con il commit `83081a67`,
già integrato in main e collaudato col set S. La loro eliminazione era autorizzata
dall’utente il 12 ottobre. Gli altri residui di F2.11 diventano sottopunti distinti;
non si estende l’autorizzazione alla rimozione dei motori legacy ancora attivi.

F2.3a: chiusa la vecchia attesa delle API dei dettagli di solette e pareti, già
disponibili nel set S e collegate dal riuso API-01. Verificati l’adattatore, la prova
delle chiamate IL e i 266 casi congelati della suite, passata anche nella corsa
standard del 12 ottobre sera. La matrice F2.1 resta una fotografia storica di S1,
con un rimando esplicito allo stato attuale. Mesh, tracce residue e scostamenti
normativi restano aperti; il caso circolare API-02 non viene considerato risolto
nel prodotto per il solo fatto che la correzione è pronta in Checker.

F2.11b: unificata la sonda di durabilità con ServiceabilityProbe, mantenendo le
API StartProbe/StopProbe e le voci tipizzate. Un solo AsyncLocal segue il contesto
di esecuzione; uno Stop della facciata ripristina la sonda precedente soltanto
se la corrente le appartiene. Le sonde annidate mantengono registri separati.
15 prove, di cui 10 fallivano prima, coprono contesti comuni, annidati, concorrenti,
istantanee, errori e disattivazione. Regressione completa degli adattatori passata;
548 file e 50.996 righe dense invariati, salvo tempi e identificativi casuali.
Registro F2-19. Nessuna eliminazione del legacy di calcolo o variazione normativa.

## Blocco F2 e finestra Reologia — autorizzazione del 12 ottobre 2026

L’utente approva i cinque passi proposti (API-02, validazioni, residui SLE,
mesh/parallelismo e collaudo) e chiede il punto 5.2: finestra di supporto alla
progettazione per φ nel tempo, φ a infinito e modulo elastico nel tempo, usando
il codice di Model. La UI è separata e riutilizzabile, con parametri, grafici e
tabella; E(t)/E₂₈ è mostrato insieme a E(t) per chiarire il significato del fattore.
Ogni chiusura ha commit e aggiornamento. Le immagini fornite sono riferimenti
funzionali della finestra, non fonti dei calcoli o contenuti da copiare nelle guide.

### 12 ottobre 2026 — completamento, rilascio e prosecuzione F3

L’utente conferma la chiusura dei punti 1–5 e 5.2 con un commit per punto; autorizza, a collaudo finale superato, versioni, merge e push di ANTHEA e delle librerie coinvolte. Al termine autorizza l’avvio della fase F3 e delega le decisioni operative, con aggiornamenti ai passaggi chiave. Restano obbligatori i confronti prima/dopo e il registro delle differenze.

### 12 ottobre — stage prima di F3 e riuso dei metodi

L’utente richiede di evitare duplicati: controllare le API esistenti e unificare i metodi comuni. Aggiunge lo studio e la prima implementazione degli stage e della lettura dei risultati MIDAS dopo F2 e prima di F3, includendo il contratto per gli altri software. Questo blocco precede l’avvio di F3 precedentemente autorizzato.

### 12 ottobre — visualizzazione delle sezioni dopo gli stage

L’utente aggiunge in coda la visualizzazione delle sezioni, fornendo come riferimento funzionale una sezione composta acciaio-calcestruzzo con dimensioni, materiali, assi e offset. La coda intermedia diventa F2 (incluso 5.2), STG, SEZ, quindi F3. Il primo obiettivo è la consultazione dei dati presenti in Model, collegata agli elementi e agli stage: l’immagine non estende automaticamente il perimetro a un nuovo solutore o a tutte le opzioni di modifica del programma di riferimento. Riusare calcoli e anteprime esistenti; le capacità mancanti degli importatori devono restare esplicite.

### 12 ottobre — chiusura validazioni U5, U7 e F2.8-U2

La vista SLE usa Homogenization.Resolve di GPC e le note del form esistente: conserva l’ingresso errato per correggerlo e svuota soltanto il dato derivato; messaggio condiviso fra Rara, Frequente e Quasi permanente. La presenza delle staffe è validata alla costruzione della sezione: il campo assente conserva il default storico Sì, mentre null, valori vuoti, numeri, booleani e grafie diverse sono rifiutati. Il numero di rami usa il parser intero esistente; viene richiesto nei dettagli delle travi con staffe e nel taglio con staffe, non nei dettagli in cui è inattivo. L’opzione legacy dei rami negativi è disattivata nel percorso di produzione.

Confronto: 548 file headless invariati salvo tempi; suite estesa degli adattatori passata. Il banco conserva i riferimenti originali: due fogli e tre chiamate dirette con −2 rami producono ora il rifiuto esplicito. Il catalogo diagnostico dei 58 testi cambia ordine perché segue la prima comparsa: un audit separato verifica testi/unità/riferimenti invariati e conteggi precedenti meno i soli cinque casi rifiutati; nessuna esclusione globale dai confronti. Registro F2-21.

## Diagnosi combinazioni nella finestra aperta — 12 ottobre 2026

La build principale 1.2.0.0 con Converter 2.0.3.1 mostra `CombinationExpansionLimit: ENV_SLE_R`. Letti operazione e termini senza modificarli: somma lineare, con G1 al posto di SLE_R_3. Nella copia MIDAS di prova conservata l’originale è un inviluppo di cinque SLE_R: 240 + 240 + 240 + 12 + 4 = 736 alternative. La somma fra inviluppi richiede invece il prodotto cartesiano. Il limite esplicito dell’adattatore è 4096: va diagnosticato prima del calcolo, senza troncare risultati o reinterpretare la definizione. Anche ENV_SLU originale (26168 alternative) richiede il seguito prestazionale già aperto. Il documento attivo, non salvato, è stato lasciato invariato; griglia ripristinata.

## Chiusura U1/U3 — punto 3, 12 ottobre 2026

U1 disattiva l’opzione di compatibilità che nascondeva BarSpacing nelle sezioni NTC interamente tese. Il valore nativo è confrontato con `s (formula)` nella traccia storica, non con un nuovo atteso generato dal codice; apertura delle fessure, rapporto e stato restano identici. U3 sceglie la regola GPC già esistente `Ntc2018AndItalianAnnex`: la riduzione approvata riguarda UNI/DM 2012 oltre a NTC, per il limite SLE del CLS e αcc/fcd. La griglia conserva l’espressione storica e dichiara separatamente l’atteso UNI pari a 0,8. Il legacy di confronto resta intatto. Il banco denso cambia soltanto nei 13 BarSpacing classificati; i casi UNI sottili sono verificati nella griglia dedicata, non presenti nel corpus headless. Nessuna formula nuova in ANTHEA.


## 12 ottobre — concorrenza e prosecuzione notturna

Il lock globale di costruzione è rimosso soltanto dopo la cattura con il lock attivo. Si riusa il catalogo di 80 mesh e si confrontano esattamente coordinate, numerazione, connettività e ordine. Le sezioni condivise e indipendenti producono gli stessi 36 stati del calcolo seriale. Il lock GPC per la cache della sezione, quello del singolo solutore e quello della cache grafica restano pertinenti. Nessun cambiamento numerico, registro F2-23.

L’utente assegna nove ore di lavoro dal 12/10 alle 22:19 (Europe/Rome), richiede tracciamento persistente e riepilogo a ogni fine fase. Conferma la prosecuzione fino a F4 se il tempo basta; **F5 non deve iniziare**. Ordine: chiusure F2 e finestra 5.2, stage, sezioni, F3, F4. Il punto 5 di collaudo e il punto 5.2 della finestra appartengono al blocco F2, non alla fase F5 esclusa. Restano autorizzati test, versioni, merge e push al completamento dei blocchi. Stato operativo in `stato-notturno-20261012.md`.


## F3 — decisioni operative del 13 ottobre

Sulla delega del 12 ottobre si adottano i confini descritti in f3-strato-applicativo.md: migrazioni pigre su copie, decisione elastica pendente, formati conservati, servizi senza WPF, figure tramite renderer e test di equivalenza prima degli spostamenti. Riusati il toolkit e la configurazione UiTests esistenti; 36 fixture coprono i 12 moduli attuali. Il set V resta congelato. Le dipendenze e la compatibilità del test esterno Checker sono considerate prima di eliminare X.Core. Nessuna autorizzazione implicita alla fase F5.

## 13 ottobre — rilascio intermedio dei servizi F3

Sulla delega operativa e di rilascio già concessa, si prepara ANTHEA 1.6.0: incremento del secondo componente per la separazione applicativa e i nuovi servizi senza interfaccia, con protezioni delle revisioni. Il set GPC V resta invariato. Si integrano soltanto i punti conclusi e collaudati; F3.7b resta aperto sui contratti concreti e sugli accessi residui delle viste, censiti nei metadati della Release. Non si dichiara chiusa la fase 3 e non si anticipa F4 prima di quel confine. F5 resta esclusa. L’integrazione e il push dipendono dal collaudo completo; l’applicazione primaria con dati non salvati non viene riavviata o ricompilata.

Le guide Rev40 aggiornano procedure e descrizione dell’architettura, senza formule nuove. Le modifiche dei soli riferimenti Wiki nel banco F3 saranno tracciate separatamente dalla cattura iniziale; gli attesi ingegneristici rimangono immutati.

## 13 ottobre — chiusura F3 richiesta

L’utente richiede di completare F3, con un commit per ogni fix, e vieta di iniziare F4. La candidata 1.6.0 resta sul ramo della fase fino al collaudo finale. Restano in vigore il confronto prima/dopo, il set V congelato e la conservazione del legacy di calcolo: F3 separa accessi e contratti applicativi, senza anticipare il trasferimento ingegneristico di F4.


## 13 ottobre — input unificato delle sollecitazioni da Excel

Attività EXCEL-SOL accodata dopo i punti F3 già concordati; nessun avvio di F4. Requisiti espressi dall'utente:

- Colonne: `Elemento`, `Nome combo`, `Stato limite`, `N`, `Vx`, `Vy`, `T`, `Mx`, `My`, in questo ordine. `Elemento` è facoltativo.
- Ogni riga conserva tutte e sei le componenti della combinazione, disponibili insieme a tutte le verifiche pertinenti. SLU alimenta domini 2D/3D e taglio/torsione; SLV alimenta i controlli associati e anche taglio/torsione. Rara, Frequente e Quasi permanente alimentano i rispettivi controlli SLE secondo le regole già implementate.
- Eliminare la famiglia di input `Taglio`: taglio e torsione operano sulle combinazioni SLU e SLV. Le viste e i risultati delle verifiche restano distinti dove necessario.

Impostazione applicativa proposta: una sola registrazione per riga, con filtri per le verifiche, senza duplicare i dati né ricombinare o coefficientare sollecitazioni già combinate. Un elemento vuoto usa il contesto del foglio di verifica destinatario e non assegna la riga indiscriminatamente a tutti gli elementi. Un elemento valorizzato deve mantenere la propria associazione; combinazioni omonime di elementi diversi non vanno fuse. Unità, assi locali e convenzione dei segni devono essere espliciti nel template e nell'anteprima. Un valore numerico mancante non va confuso silenziosamente con uno zero.

Compatibilità da risolvere in implementazione: i vecchi file con famiglia Taglio non indicano se la riga appartiene a SLU o SLV. Conservare i dati e rendere esplicita l'assegnazione, senza scartarla né indovinarne lo stato limite. Inoltre oggi SectionWorkspace presenta SLU come Plastico e SLV come Elastico e CheckerSection seleziona così il dominio: classificazione della combinazione e criterio del dominio vanno tenuti distinti, senza introdurre implicitamente nuove regole sismiche nella sola importazione. Riutilizzare parser, validazioni e servizi esistenti. Collaudare lo stesso insieme di componenti nei diversi controlli e il percorso di migrazione, senza sostituire gli attesi indipendenti.

Sono registrati requisiti e proposta, non una funzionalità già realizzata. Al termine di F3 chiedere soltanto le decisioni residue necessarie per la riscrittura.

## 13 ottobre — chiusura effettiva F3

Completati contratti, servizi, report e confine Desktop. Verifica sorgenti oltre ai metadati, compilazione Release senza riferimenti GPC diretti e distribuzione dal manifest. Set V e legacy conservati, nessun avvio di F4. ANTHEA 1.6.0 pronta con guide Rev40; 122 voci del full risolte tramite riprese mirate tracciate, installer verificato. Merge e push restano quelli già autorizzati; nessun intervento sul processo utente. EXCEL-SOL rimane la prossima attività separata: Elemento facoltativo e distinzione fra stato limite e criterio resistente, come specificato sopra.


### 13 ottobre — criterio resistente selezionabile (EXCEL-SOL)
L’utente sceglie Plastico/Elastico nella verifica, indipendentemente dallo stato limite della riga. Sei componenti memorizzate una volta; proiezioni per SLU/SLV, SLE e taglio/torsione. Elemento è un riferimento facoltativo: tutte le righe sono verificate sulla sezione del foglio, anche se riferite a elementi differenti. Non si assegnano automaticamente geometrie del modello. I nomi uguali restano righe distinte con ID propri.
Conversione dei dati storici esplicita: le vecchie famiglie Plastico/Elastico e Taglio richiedono l’assegnazione dello stato limite; le componenti mancanti devono essere compilate. Nessun coefficiente o cambio di segno aggiuntivo. L’apertura dei documenti precedenti non cambia il comportamento.
Cattura preventiva: `20261013-142103-excel-sol-before`, Application.Tests e verifiche/ca-data PASS. F4 non avviata.

## 13 ottobre — ANTHEA 1.7.1 e ripresa della pianificazione F4

Dopo la correzione F3.4-U5 l’utente richiede «merge in master, versione 1.7.1».
ANTHEA usa `main`: integrazione completata in `46b4c918`, versione patch
`2c06ab46`, installer e DLL verificati come nel verbale F3. Nessun push in
questo passaggio. Non sono state modificate le librerie GPC.

La successiva richiesta «poi parti con la F4. pianifichiamo i punti» supera
l’arresto della fase. Si apre `refactoring/f4-calcoli-residui` da quella 1.7.1.
Il [piano aggiornato](f4-calcoli-residui.md) mantiene F4.1–F4.16, propone F4.12
dopo F4.10 per completare la ricerca delle armature prima di Bridge Design,
e parte da inventario, catture e protezioni automatiche. L’inventario iniziale
è eseguito, ma non costituisce equivalenza né chiusura di F4.1.

GPC.Design nel repository Checker è la proposta di collocazione da rendere
concreta in F4.3; non è una libreria già creata. Si riusano esiti, coefficienti,
servizi Application e test presenti. Le decisioni normative già registrate
non si richiedono di nuovo; le questioni ancora aperte richiedono prima una
scheda con fonte ed effetto numerico. Trasferimento equivalente e correzione
numerica hanno commit distinti. Un aggiornamento per punto concluso; F5 e
la rimozione indiscriminata del legacy restano esclusi.

### F4 — finestra operativa e sospensione Bridge Design

L’utente autorizza l’esecuzione degli altri punti per 1,5 ore e sospende il
blocco 5 del piano operativo (F4.11): vuole definire prima la gestione dei
prezzari. Chiede inoltre di attendere per la chiusura F4.16, che potrebbe
includere successivamente Bridge Design. La sospensione non riguarda F4.5,
che resta il punto tecnico dei coefficienti normativi. Nessun rilascio finale
di fase né avvio F5; commit e aggiornamento per ogni passo concluso.

### F4 — completamento notturno e prezzari sostituibili

Il 13 ottobre sera l'utente approva la gestione dei prezzari e richiede F4
completata per la mattina successiva, con commit per punto e aggiornamenti.
Sono superate le sospensioni di F4.11 e F4.16; F5 resta esclusa.

Cataloghi versionati, import Excel/CSV, associazioni riutilizzabili alle
lavorazioni, analisi composte, prezzi di progetto e confronto fra revisioni.
Il cambio di prezzario conserva quantità e revisioni precedenti; segnala voci
mancanti/incompatibili senza prezzi zero impliciti. La scelta di conservare
le personalizzazioni è esplicita. I prezzi correnti migrano senza ricalcolo.
Prezzi e CO₂ hanno fonti/versioni indipendenti.

GPC.Design esegue calcolo e ottimizzazione del costo con dati immutabili della
chiamata, senza leggere controlli WPF o archivi ANTHEA. ANTHEA gestisce prezzari,
documenti, associazioni, avvio/annullamento e presentazione. Dopo il cambio dei
prezzi si rivalutano le alternative note e si può rilanciare la ricerca: la
nuova soluzione migliore non è necessariamente fra quelle già esplorate.

Prima del collegamento dei nuovi servizi si completa il confronto isolato.
Il set distribuito si aggiorna soltanto con il treno coerente; i checkpoint
Git e la rimozione del legacy restano soggetti alle regole del repository.

## 14 ottobre — audit indipendente di F0-F4 e decisioni punto per punto

Il 14/10 l'utente chiede un controllo di tutte le sottofasi fino a F4 («secondo me non ha lavorato bene»). Quattro
verifiche indipendenti in sola lettura (F0-F2, F3 ed EXCEL-SOL, F4 e librerie, regole trasversali) e il profilo
standard sul ramo F4 (7569f6fc: 66 PASS, un fallimento intermittente di `Application.Tests` per un file bloccato,
ripetuta da sola PASS). Esito: F4 non è chiusa; codice delle librerie reale e attesi indipendenti intatti, ma report
modificati durante il blocco, push e merge senza richiesta, [CP] chiusi dall'agente e rilascio W e installer 1.8.0
senza full conforme. L'utente: «parti pure a correggere gli errori che hai segnalato… scrivimi per ogni cosa che ti
serve la mia autorizzazione. vediamo punto per punto». Correzioni sul ramo `refactoring/f4-correzioni-audit`.

Decisioni dell'utente, una per punto:

1. **Report di calcolo** (blocco dell'8/10): «Ratifico e sblocco». Restano le modifiche già fatte ai report
   (EXCEL-SOL 504de76e e 9e1ac2ca, Bridge 440db3fd, 3ecaf795 e d4762582, U5 4a3044cb, F4.14 9fee7a47 e 575e2396) e il
   blocco è tolto: i report si possono modificare, sempre con cattura prima e confronto dopo e con le differenze nel
   registro.
2. **Scelte ingegneristiche prese dall'agente in F4**, una alla volta:
   - (a) F4-03, σcp nel taglio dei muri: «il punto a te lo confermo». Resta la correzione.
   - (b) F4-08, curvatura dei muri con viscosità: «confermo b». Resta la curvatura totale χ·(1+φ). Il solutore
     risolve con Ec pieno ed Es·(1+φ): le tensioni dipendono solo da n, ma il piano è (1+φ) volte più piccolo della
     deformazione reale. Prova: una barra a σs ha deformazione σs/Es, il piano le dà σs/(Es·(1+φ)). L'utente ha
     chiesto se convenga spostare la riduzione su Ec nel solutore. Deciso («sì a entrambe»): il solutore resta com'è
     (il significato del piano cambierebbe in silenzio per tutti gli utilizzatori, che moltiplicano già per (1+φ); la
     libreria ammette φ diversi per armature e cavi; nessun guadagno sulle tensioni). Si aggiunge invece in Concrete il
     piano totale esplicito, con il piano attuale documentato come equivalente; push della libreria su OK dell'utente.
     Approvata anche la correzione del modulo sezioni: equazione del piano e diagramma delle deformazioni SLE con φ > 0
     mostrano il piano equivalente, (1+φ) volte più piccolo delle deformazioni di barre e vertici nella stessa vista.
     Solo visualizzazione; nuova voce del registro, cattura prima e confronto dopo.
   - (c) F4-31, documenti c.a. senza norma: l'utente precisa che non esistono file salvati senza norma e che la norma
     è effettivamente NTC. Deciso («sì a entrambe»): calcoli rigorosi senza ripiego su NTC; nota di migrazione tolta
     (era anche contraddittoria se poi si sceglie un'altra norma); NTC assegnata solo ai file del formato precedente al
     selettore, un file del formato attuale senza norma si rifiuta. In coda, dopo il refactoring: Eurocodici con una
     norma di progetto (non per modulo), annesso nazionale come dato del profilo, profili tipizzati per disciplina nelle
     librerie (combinazioni EN 1990, geotecnica EN 1997, sisma EN 1998, traffico EN 1991-2, copriferri), rifiuto
     esplicito dei moduli che non supportano la norma scelta, attesi indipendenti per ogni modulo e norma.
   - (d) F4-20, NEd dell'armatura longitudinale minima: «compressione massima tra tutte le combo. slu, slv e taglio
     (eventualmente sle se le altre sono nulle)»; precisato: «se dovessero mancare considera gli sle. in generale, usa
     le N più gravose di tutte le combo». Regola: NEd = compressione massima fra tutte le combinazioni (SLU, SLV, righe
     del taglio, SLE rara, frequente e quasi permanente), uguale in scheda dettagli e progetto armature; il risultato
     indica la combinazione che governa e segnala quando è di esercizio. Oggi entrano SLU, SLV e taglio: si aggiungono
     le SLE. La regola va nella libreria (GPC.Design, `SectionDesignDemand`), non in ANTHEA.
   - Seguito di (b), viscosità nelle combinazioni SLE dei muri: «confermo la 1. ad ora teniamo quella». Resta (1+φ)
     in tutte e tre le combinazioni SLE, come per le tensioni (stesso n): a favore di sicurezza, sovrastima modesta
     perché la spinta è quasi tutta permanente. φ solo nella quasi permanente è escluso (perderebbe la viscosità dei
     permanenti). La sovrapposizione (quasi permanente con viscosità più incremento variabile senza) «in un futuro
     eventuale, ma non in questo refactoring».
3. **Legacy sostituito in b7339f4a** (12/10): «ratifico entrambe».
   - (3a) Aderenza/ancoraggi, dettagli non circolari, M–χ e interassi non circolari restano sulla libreria, senza
     interruttore; il codice precedente si recupera da git. Equivalenza: 548 file e 50 996 righe, scarto massimo
     1,1e-15 relativo. Superata per queste famiglie la condizione di F2.11 («il legacy resta finché l'utente non
     decide»); la revisione delle pagine dei metodi (F2.8-U1) resta in coda come documentazione.
   - (3b) Tensioni e fessurazione SLE: libreria come motore predefinito (A8), legacy selezionabile fino alla revisione
     delle pagine dei metodi, poi rimozione con l'ok dell'utente. Il coordinatore completa le evidenze mancanti
     (cattura B4 del corpus SLE con i due motori, voci F2-7 e F2-8).
4. **Ratifiche a posteriori**: «ratifico entrambe».
   - (4a) Push dei master di Model e Checker del 14/10 alle 04:04 (set W); merge e push di EXCEL-SOL su main (1.7.0,
     13/10; il verbale dichiarava un'autorizzazione non registrata); merge e push di F3 del 13/10 alle 13:45, dopo la
     scadenza del mandato notturno.
   - (4b) [CP] F1.4, F1.6, F2.2 (la rilettura dei contenuti delle pagine dei metodi resta in coda), F2.9-D1…D14,
     F2.7-D1, F3.1, F3.4, F3.6 limitatamente al ricalcolo ritardato del ponte (450 ms), F3.7, F4.3 e F4.14.
   - Restano aperti (4c): «report short senza Word» di F3.6 e proposte F2.9-U1…U6.
   - (4c-1) Report sintetico senza Word: «predisponilo ora in questo refactoring. fai che faccia entrambi i file: docx
     e pdf. export in pdf aggiungilo anche agli altri report. per ora però prendi i report per come sono. li dovremo
     rivedere appena finito il refactoring». Il PDF si genera in ANTHEA.Reports dallo stesso modello in memoria del
     DOCX, con una libreria MIT e senza Word; il report sintetico scrive sempre DOCX e PDF e il controllo delle due
     pagine lo fa ANTHEA; l'esportazione PDF si aggiunge a tutti gli altri report. Contenuti e impaginazione dei DOCX
     restano quelli attuali (cattura prima e confronto dopo: DOCX invariati); la revisione dei report è in coda dopo la
     fine del refactoring. Dipendenza: «va bene aggiungere un nuget» (PDFsharp/MigraDoc, MIT, da nuget.org; DLL e nota
     di licenza anche nell'installer).
   - (4c-2…7) Proposte F2.9: U1 «non ho la norma. resta sulla 2016» (scritto «2026», refuso): classi minime e
     composizione restano UNI 11104:2016 prospetto 5, la 2025 non si adotta senza il testo primario; U2, che segue U1:
     l'etichetta della libreria diventa «UNI 11104:2016 prospetto 5»; U3 «correggi»: si traducono i testi inglesi del
     palo elastico (riferimento e messaggi di `PileDurability`, 7 righe di relazione); U4 «ok»: i valori predefiniti
     della scheda Materiali si scrivono nel documento e si mostrano, nessun numero cambia; U5 «ok»: descrizioni, esempi e
     note riprese da ATECAP 2020 si riscrivono con testi nostri, citando solo le norme; U6 «ok»: la pagina del metodo
     `ca.durabilita-copriferri` resta in coda con le altre.
5. **Autorizzazioni operative**: «autorizzo tutte le 5 che hai mandato». Eseguite il 14/10:
   - (5a) junction `supporto/artefatti/verifiche_ca_auto/template/node_modules` (dal 18/09, verso il runtime Codex)
     tolta con `cmd /c rmdir`; destinazione intatta;
   - (5b) `supporto/tmp/f2-libs` (1,34 GB): quattro worktree di Checker, Geometry, Model e Utilities, puliti e su commit
     già in origin/master, rimossi con `git worktree remove`; cancellata la copia di Gmsh.Net (il repository resta);
   - (5c) Temp: `gpc-rev` (2,27 GB, revisione delle librerie chiusa il 12/10), `anthea-uitests-a5` e 38 cartelle vuote
     (AntheaShort-*, ANTHEA-InstallerTest-*, ANTHEA-GuideTest-*, ANTHEA-GuideCheck-*, anthea-midas-*, d7d-harness-*);
     controllato prima che non contenessero collegamenti; `anthea-guides` del 28/09 non toccata;
   - (5d) tag annotati delle versioni sul commit da cui è costruito l'installer, dove registrato (1.2.0 fc632464, 1.6.0
     8b0da2a2, 1.7.0 68330851, 1.7.1 2c06ab46, 1.8.0 d66379ac), altrimenti sul commit che porta la versione (1.3.0
     71aaa60b, 1.4.0 22f6a1aa, 1.5.0 bce1e337); push dei tag e del ramo `refactoring/f4-correzioni-audit`.
6. **Profilo full**: «profilo full lo facciamo stanotte. non ora. da mezzanotte in poi»; poi spostato: «lavora uno alla
   volta. spostiamo il full a domattina alle 6.00. se non riesci a finire le cose, spostiamo il full». Corsa sul ramo
   delle correzioni il 15/10 alle 06:00 solo se le correzioni concordate sono finite e committate, altrimenti si
   rinvia; comprende il nuovo confronto UI con la cattura F3 sul commit corrente. Lavoro senza agenti in parallelo.
8. **Voci anticipate a prima del full** (A12, avviso di migrazione EXCEL-SOL, pagine dei metodi F4, analisi composte).
   Analisi composte dei prezzari: prezzo unitario = Σ quantità × prezzo dei componenti (materiali, manodopera, noli,
   trasporti) più spese generali e utile d'impresa, «che siano chiare voci presenti nel totale» e impostabili;
   percentuali predefinite nel prezzario, modificabili per singola analisi in modo esplicito; un'analisi può usare
   altre analisi come componenti, con rifiuto dei riferimenti circolari. Calcolo in GPC.Design, gestione in ANTHEA.
7. **Riferimenti di terzi nelle guide**: «punto 7 ok nella Rev44». Restano citazioni normative e bibliografia dei metodi
   implementati (FHWA GEC 10 e AASHTO per i p-multiplier, USGS e USACE per Newmark, Timoshenko e Gere, schema IFC); si
   tolgono TU Delft, MIT, diapositive JRC (al loro posto la norma, es. EN 1992-1-1 §9.2.1.3), SCI, esempi LRFD e
   manuali FHWA di metodi non implementati, con spiegazioni riscritte in testo nostro. La frase su Straus si toglie
   finché l'importazione non è attiva; il controllo delle guide intercetta anche questi riferimenti. Tutto nella
   Rev44, insieme a PDF dei report, regola di NEd e testi ATECAP riscritti.
9. **Seguiti della sera del 14/10**: «ok push di checker e anthea»; «lascia pure quel minor, non dà fastidio».
   - Pubblicati master del Checker (26cef01a, pagine dei metodi F4) e il ramo `refactoring/f4-correzioni-audit`.
   - Il commit vuoto 2bbdcbce «minor» del Checker (aggiunge solo `diff-piano.txt`) resta, senza revert.
   - F2-7, uscite della fessurazione non raggiunte ([f2-7-rifiuti-sle.md](f2-7-rifiuti-sle.md) §4): «ok 1 e 2, la 3
     mettila in coda dopo F4». Le sette uscite impossibili o degeneri restano difese della libreria; i due casi
     raggiungibili con le armature libere diventano stati mirati con il confronto fra i due motori; l'indicazione
     all'utente sulla zona tesa non armata è in coda dopo F4.

## 15 ottobre — decisioni per la chiusura di F4

Proposte di [f4-chiusura-decisioni.md](f4-chiusura-decisioni.md), dopo il full del 14/10 sera (136 PASS; confronto UI
F3 con 7214 differenze tutte nella previsione).

- **D1** «ok punto 1»: i 17 gruppi di differenze del confronto UI F3 sono transizioni attese, insieme alle variazioni
  misurate nel full del 14/10 (F4-40, F4-47, versione di Geotechnics, Wiki e ricerca F4-48). La stazione Bridge con
  V = 9,1e-13 (V6) resta com'è.
- **D2** «ok punto 2»: elenco versionato delle transizioni ammesse, per file e percorso, con valore prima e dopo e voce
  del registro; ogni altra differenza fallisce; nessuna tolleranza nuova; Wiki e ricerca come F3-06, con impronte
  prima e dopo. Confermata la modifica dei due input F3 fatta con F4-40. Poi un full conforme.
- **D3** «sposterei il vecchio codice in un progetto parallelo o in una parte di progetto che non viene rilasciata.
  non vorrei rilasciare doppio codice inutile. ma vorrei rimanesse per storico»: il codice legacy non più usato dal
  prodotto esce dal rilascio e si conserva in un progetto non distribuito, usato solo dalle prove come riferimento
  storico. Resta nel prodotto solo ciò che il prodotto usa davvero (il nome «Legacy» da solo non basta: per esempio
  `LegacyNumerics` è la somma usata dal calcolo attuale). La scelta del motore SLE oggi è solo un parametro delle prove,
  senza comandi nell'interfaccia. Precisato subito dopo, alla domanda se conservarlo in un progetto parallelo: «io
  onestamente eliminerei tutto quello che non serve ed è servito per test. vorrei rilasciare un codice relativamente
  pulito senza codice inutile e test dentro». Quindi: si elimina il codice legacy che il prodotto non usa e il codice
  che serve solo alle prove dentro gli assembly distribuiti; nessun progetto parallelo. Supera la parte di 3b che teneva
  il legacy SLE selezionabile fino alla revisione delle pagine dei metodi. Modo (proposta dell'agente, coerente con le
  regole del refactoring): tag git sul commit precedente alla rimozione, per ritrovare il codice; dove una prova
  confronta la libreria con il legacy, prima si congelano le uscite del legacy sui corpus esistenti (fixture con
  SHA-256) e la prova confronta poi la libreria con la cattura congelata; gli attesi indipendenti restano.
  **Decisione finale**, dopo la spiegazione di come si replicherebbero le prove: «allora optiamo per tenere tutto il
  codice e spostarlo dentro dei file che non finiscono nell'installer. non vorrei cancellarlo dal progetto, per quanto
  versionato». Quindi nessuna cancellazione: il codice legacy non usato dal prodotto e il codice che serve solo alle
  prove escono dagli assembly distribuiti e vanno in un progetto del repository che l'installer non contiene, usato
  dalle prove; i confronti vivi fra libreria e legacy restano. Superate le due formulazioni precedenti (progetto
  parallelo come proposta e poi eliminazione).
  **Modo per le prove di modulo** («confermo la scelta b», 15/10): l'inventario
  (21 blocchi da spostare, circa 3000 righe di `X.Calculations`) proponeva (a) prove che chiamano il legacy solo
  funzione per funzione, perdendo i giri di modulo col motore vecchio, o (b) un punto d'aggancio interno nel prodotto,
  senza formule, con il motore vecchio in `tests/ANTHEA.Legacy` attivato dalle prove in un ambito. Scelta (b): le prove
  di modulo (sezione c.a. completa, muri, durabilità di progetto, taglio e torsione) continuano a girare dal vivo col
  legacy. Punti minori decisi dall'agente: la traccia dei ripieghi del JSON non si attiva più da variabile d'ambiente
  nel prodotto ma dal banco di prova; l'opzione `--models` e `RunVerificationAsync` restano.
- **D4** «ok, finito questa correzione chiusura F4»: chiusura dei punti di F4 e dei [CP] F4.9 e F4.16 dopo D3 e il
  full conforme, con verbale.
- **D5** «D5 ok»: merge senza avanzamento rapido in `main`, tag `refactoring/post-f4`, ANTHEA 1.9.0 con installer e
  suite dell'installer, tag `v1.9.0`, push di `main` (che pubblica anche il merge 1.7.1) e dei tag, cancellazione dei
  rami F4 uniti anche su origin.
  **Guide per la 1.9.0** («Rev45, frase senza versione», 15/10): la guida pratica Rev44 diceva «Per installare ANTHEA
  1.8.0…»; la Rev45 toglie il numero dalla frase, così non va cambiata a ogni versione (registro F4-50).
- **Attuazione di D4 e D5** (15/10): full conforme `20261015-105444-f4-full-190` (140 PASS, 0 NEW-FAIL), installer
  1.9.0 da `a46e91fb`, F4 chiusa con il [verbale](chiusura-f4-20261015.md); merge, tag e push come da D5.

## 15 ottobre — residui di F2, decisioni E1–E8

Proposte del [foglio dei residui di F2](f2-residui-decisioni.md), preparato dopo la chiusura di F4 sul ramo
`refactoring/f2-residui`. Lo stesso giorno, prima del foglio, l'utente ha deciso R1, R2 e il punto 3 di F2-7; poi ha
approvato tutte le proposte del foglio: «E1–E8 come nel foglio». Ha risposto a parte su due punti: i muri (E1-D4) e le
superfici interne delle sezioni cave.

- **R1 e R2** «Correggi su fonti secondarie» (alla domanda: testi delle norme non disponibili). Si correggono τbms e β per
  stadi del fib MC2010 (Tab. 7.6-2) e kt della DIN secondo la durata, come nel rapporto
  `supporto/artefatti/refactoring/ricerca-a/rapporto.md` §6.7, con certezza dichiarata «probabile».
  - In libreria sono opzioni di `SectionCrackOptions` spente per default: il contratto K0 di ModelChecker non cambia.
  - L'adattatore SLE di ANTHEA le attiva. Le prove di equivalenza con il motore storico girano con le regole precedenti
    (aggancio di sola prova `PreviousCrackRules`).
  - Distribuite con la release Y: GPCChecker.Concrete 0.10.0.0, Checker master `6f15f185`, ANTHEA `2257e991`.
  - Registro R1 e R2, attuate.
- **F2-7 punto 3**, zona tesa senza armatura: «Non soddisfatta», sull'esempio della R300x500 armata solo in basso.
  - Con le barre tutte compresse, o con una faccia tesa senza barre, la verifica a fessurazione è non soddisfatta, con il
    motivo.
  - Con l'asse neutro nel copriferro e nessuna barra tesa la sezione non fessura: wk = 0, come prima.
  - Il calcestruzzo teso non entra nella verifica wk (domanda dell'utente: «dovrebbe reagire a trazione il cls???»).
  - Registro F2-25 e nota in F2-7; distribuito con la release Y.
- **Superfici interne delle sezioni cave**, estensione di F2-25 decisa dall'agente: 33 stati della griglia, con le pareti
  interne tese e senza barre vicino al foro. Confermata la verifica **non soddisfatta**, come per la zona tesa esterna.
  Scartata l'alternativa: superfici interne senza esito.
- **E1**, ratifiche a comportamento invariato: «come nel foglio».
  - F2-1 con T-1: taglio e torsione della libreria con differenze nelle ultime cifre (tolleranza 1e-9). Con VEd = VRd e
    senza staffe l'esito può cambiare per 1 ulp; nessuna tolleranza sul verdetto.
  - F2-3: la torsione senza staffe chiuse resta un rifiuto, con il messaggio del legacy.
  - F2-4: limiti propri della libreria non raggiungibili dal modulo (torsione oltre C90/105, CNR-DT): nessuna azione.
  - B3: quattro casi di taglio e torsione nel corpus, baseline catturata con il legacy.
  - A7 e T1: i commit dedicati delle tolleranze sono sostituiti dalle regole `api_arrotondamento` di `1b3c38f1`
    (relative 1e-12, più strette di quelle previste).
  - D7 / B4: la B4 attuale sostituisce il corpus SLE del micro-piano. Ha 17 documenti, catturati con i due motori dopo
    l'interruttore; con la corsa baseline del 15/10 contro B4 (`20261015-124031-f2res-b4`).
  - F2.7-D1…D12, scelte del progetto F2.7 rivisto (`supporto/artefatti/refactoring/f27-f28-progetto/F27-progetto-rivisto.md`,
    §2), trascritte qui come chiedeva l'audit del 14/10:

    | Decisione | Scelta del progetto | Esito |
    | --- | --- | --- |
    | F2.7-D1 Traccia | Chiavi stabili in libreria; la mappatura restituisce simboli, espressioni e note del legacy (K3) | Ratificata il 14/10 (punto 4b) |
    | F2.7-D2 Getti sottili | Regola per profilo in libreria, con i valori di allora (NTC 2018 → 0,8, altri → 1) | Superata da U3: 0,8 anche con UNI/DM 2012 (R22) |
    | F2.7-D3 SteelStressLimit | Limite dell'acciaio per ogni stato, JSON invariato (K1) | Attuata |
    | F2.7-D4 «Non fessurata» dei muri | Resta nel modulo muri fino a F4.7 | Superata da E1-D4 (sotto) |
    | F2.7-D5 BarSpacing della sezione NTC interamente tesa | Difetto conservato con un'opzione legacy nominata | Superata da U1: corretto |
    | F2.7-D6 Omogeneizzazione | In GPCChecker.Concrete, con il comportamento di allora anche per φ < 0 (K4) | Superata da U5: φ < 0 rifiutato |
    | F2.7-D7 Baseline | B4 con il corpus SLE e la cattura del progetto armature (A6) | Superata dalla B4 attuale (punto D7 / B4) |
    | F2.7-D8 Interruttori | Uno solo (A8) | Superata da 3b (libreria predefinita) e da D3 (motore storico fuori dal prodotto) |
    | F2.7-D9 R5 | Non si anticipa | Attuata: R5 in F5.15 |
    | F2.7-D10 R1, R2, a4 | Fuori da F2.7 | Attuata: R1 e R2 decise a parte il 15/10 (sopra) |
    | F2.7-D11 Array statici | F2.7c, dopo il merge del tema | Attuata con A12 (14/10) |
    | F2.7-D12 Validazioni | Al punto d'uso, attivate da un'opzione (K2): ModelChecker invariato | Attuata |
- **E1-D4, muri**: l'utente sceglie la proposta. La faccia tesa del fusto o della fondazione senza barre dà la verifica a
  fessurazione **non soddisfatta**, come nelle sezioni, senza la prova di sezione non fessurata con fctk,0,05 di
  `WallSectionSession`. Si attua nella release delle correzioni dei bug: Checker `8d547829` sul ramo di integrazione
  `anthea-bug-1015`, parte ANTHEA sul ramo `refactoring/bug-1015-geo`.
- **E2**, traccia del calcolo a taglio NTC (F2-2): rinviata alla revisione dei report, in coda dopo il refactoring
  (decisione del 14/10, punto 4c-1: «per ora però prendi i report per come sono»). Con R1 e R2 attuate, il punto
  F2.3b-tracce si chiude così.
- **E3**, chiusura di F2.8 (dettagli, ancoraggi, aderenza, M–χ).
  - Al posto di B1–B3, T1 e I1–I3 si accettano le prove di equivalenza già misurate:
    - confronto denso del riuso API: ancoraggi 468/468 e aderenza 1918/1918 identici, dettagli e M–χ entro 1e-12;
    - controllo delle chiamate reali alla DLL e 266 casi congelati;
    - catture prima e dopo D3 uguali e full del 15/10.
  - Nel registro la voce F2-9 rimanda ad API-01, invece di ricostruire le voci F2-9…F2-12 previste dal micro-piano.
  - Alle suite di ANTHEA si aggiungono gli attesi indipendenti delle pagine dei metodi che mancano: ancoraggi (AN-01/02,
    SO-01/02), dettagli (As,min, As,max, Ast/s) e M–χ.
  - F2.8-D1…D12, scelte del progetto F2.8 rivisto (`F28-progetto-rivisto.md`, §2), trascritte e ratificate:

    | Decisione | Scelta del progetto | Esito |
    | --- | --- | --- |
    | F2.8-D1 Ordine e release | Dopo F2.7, una sola release S3 per la libreria di F2.7 e F2.8 | Attuata (S3, 8/10) |
    | F2.8-D2 Muri | Ancoraggi dei muri dall'adattatore di F2.8, con l'interruttore I1 | Superata: libreria senza interruttore (3a, 14/10), poi D3 |
    | F2.8-D3 Tetto R10 | Tetto C60/75 di fctk,0,05 a richiesta; muri senza tetto fino a F5.15 | Resta (F2.8-U3) |
    | F2.8-D4 Soletta e parete | Solo NTC; gli altri profili rifiutati con il motivo | Attuata (L1) |
    | F2.8-D5 Unità del M–χ | Unità del chiamante, tolleranza 1e-9 | Attuata (L3) |
    | F2.8-D6 Deformazioni del M–χ | Dal `CheckerStressState` | Attuata (L3) |
    | F2.8-D7 Punto limite del M–χ | Valutato in modo pigro al passo limite | Attuata (L3) |
    | F2.8-D8 Testi | La mappatura restituisce i testi del legacy | Attuata |
    | F2.8-D9 Ambito delle norme | Come allora: dettagli e ancoraggi solo NTC, M–χ con tutte le norme | Attuata |
    | F2.8-D10 Baseline | B4 unica per F2.7 e F2.8, altrimenti B5 | Superata dalla B4 attuale (E1) |
    | F2.8-D11 Rifiuti nuovi | Accettati solo se misurati non raggiungibili; LinkLegs < 0 conservato con l'opzione legacy | Superata: LinkLegs < 0 rifiutato (F2.8-U2, registro F2-21) |
    | F2.8-D12 Release e push | Dalla sessione del coordinatore, mai dai workflow | Attuata |
- **E4**, F2.10, accettazione dell'equivalenza.
  - Accettata l'equivalenza di F2 sul full conforme del 15/10 (`20261015-105444-f4-full-190`, 140 PASS), sulle catture
    prima e dopo D3 e sulla corsa baseline contro B4 del 15/10.
  - Ratificato il rinvio di R16–R21 a F5.15, esteso dal coordinatore il 7/10.
  - Registro allineato: D7-e e D7-f attuate; F2-13 e F2-14 ratificate con 4b; percorsi di codice spostati da D3, F3 e F4.
- **E5**, F2.11c dopo D3.
  - Si spostano in `tests/` i membri usati solo dalle prove, con cattura prima e dopo: `TensionBarSpacing`,
    `SectionRegions.Clip/Area/Region`, `RetainingWall.SoilMass` e `ValleyBand`, `ConcreteReinforcementDesign.SearchIndices`,
    `Durability.ValidateExposure` e due interfacce senza utilizzatori.
  - C.a.: array e dizionari statici pubblici resi immutabili, come in A12. `JsonFallbackHook.Observer`, aggancio di sola
    prova pubblico e globale, diventa interno e per contesto.
  - Il resto dello stato statico (ponte, muri, pali, fondazioni, Wiki) va in F5.13, che chiede già calcoli in parallelo
    senza stato statico.
  - Lock: punto chiuso. Nessun lock globale sul calcolo; restano i lock di istanza, quelli dei font PDF e quello del
    registro degli errori.
- **E6**, F2.12: chiusa con D3 (F4-49).
  - Il solutore legacy a fibre è in `tests/ANTHEA.Legacy/Section`; i 90 casi di `X.Verifiche` girano ancora contro di esso.
  - L'oracolo Python `tools/attesi/sezione_ca.py` (464 casi) è conservato. `sezione_ca.py --valida` e `--confronta`
    vanno nello stadio fast del runner.
  - Il confronto dei 90 casi fra libreria e Python si fa dopo il refactoring.
- **E7**, F2.13, registri di migrazione.
  - Voce «M4 (c.a.)» in `Checker/docs/migrazione-anthea/MIGRAZIONE_ANTHEA.txt` (Checker `14673564`) e riga in
    `Model/CHECKER_PASSO_4.txt` (Model `2ebc01e2`), che diceva ancora «ANTHEA non usa ancora le nuove API». Fatte il 15/10
    sui rami di integrazione delle correzioni (Checker `anthea-bug-1015`, Model `bugint-model`): si uniscono con la
    prossima release.
  - Nessuna `SCHEDA_SCOSTAMENTI_FESSURAZIONE.txt`: la sostituisce il registro delle differenze.
  - `AGENTS.md`: la regola sul motore legacy segue D3. Il motore legacy non si elimina: si sposta in `tests/ANTHEA.Legacy`.
  - Ratificato F2.3 [CP]: API e versioni sono distribuite dai set S, T e X.
- **E8**, STG-4 e SEZ-3b fuori da F2. Il confronto numerico per fase sul ponte reale e la sezione variabile per fase non
  dipendono dal refactoring: oggi MIDAS restituisce tabelle per fase vuote. Passano nella traccia del modulo Modelli, da
  riprendere quando ci sarà un modello MIDAS con i risultati per fase.
- **Rettifica di F2-17**, dall'[analisi dei bug](analisi-bug-20261015.md) del 15/10 (SLU-2). L'opzione B della voce diceva
  che con N costante ed eccentricità costante la libreria trova sempre un punto convergente: è falso. Con N costante non
  c'è punto per una domanda a momento nullo né con N fuori da [NRd,c; NRd,t]. La decisione dell'11/10 (opzione A, solo la
  libreria) non cambia. La correzione è nella libreria, con esiti tipizzati (GPCChecker.Concrete 0.11.0.0, nella prossima
  release). Registro F2-17.
