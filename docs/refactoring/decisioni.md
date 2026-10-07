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
  («UNI 11104 prospetto 5» dipende dall'edizione).
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

## Decisioni del 7 ottobre 2026, pomeriggio: F1.6

- **Destino dei progetti di test**: la tabella di [progetti-di-test.md](progetti-di-test.md) era da approvare;
  l'utente ha delegato la decisione ("esegui tutto te"); decisa dal coordinatore su delega, da ratificare. Si
  archiviano in `supporto/SUPERATI/test/<nome>`, con `git mv` e contenuto invariato, i progetti che dipendono
  da servizi o programmi di terzi, gli strumenti una tantum e quello che non compila:
  BridgeDesign.SiteComparison, MaxRetainingWall.Cases, MaxRetainingWall.Compare, ConcreteStressDiagnosis,
  ValidazioneCA20260925, ElasticPile.Performance (riportato in `supporto/test`, vedi sotto) e ProgrammaAnthea
  (`qa.py`). Nessuno era nel runner. Gli altri progetti mantengono la destinazione già scritta in tabella.
  Origine, motivo e sostituzione di ogni file sono in `supporto/SUPERATI/registro-20261007-test.json`.
- **Seguito di F1.6 dopo la verifica dell'integrazione 2** (decise dal coordinatore su delega, da ratificare):
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
  - le cartelle `bin` e `obj` ignorate rimaste nelle vecchie posizioni dei progetti non si cancellano; le
    segnala il coordinatore all'utente.

## Decisioni del 7 ottobre 2026, pomeriggio: D7-e

- **(e) Durabilità, attuazione di «confermo la norma più aggiornata»**: UNI 11104:2025 in vigore dal
  24/07/2025 al posto della 2016 (schede del catalogo UNI); secondo un estratto pubblicato il 28/07/2025
  (fonte secondaria) il prospetto 6 «Valori limite per la composizione e le proprietà del calcestruzzo»
  dà C30/37 per XC3, XD1, XF4 e XA1, come il codice. Le pagine visibili dell'anteprima UNI (indice,
  introduzione, punti 1 e 2) confermano edizione e struttura, non i valori; la lettura «a p. 12
  dell'anteprima» registrata in un primo tempo veniva da contenuti del file non mostrati (fonti
  riscritte il 7/10, sezione seguente). C30/37 resta, nessun valore cambia; in ANTHEA le citazioni
  indicano ora edizione e prospetto (scheda Materiali, report dei materiali, commenti, README, guida
  teorica; branch `refactoring/d7e-uni11104`). La libreria (`ExposureClasses.cs`) si corregge nella prossima release
  (F2.9), con il testo proposto in `scostamenti.md`. D7-e passa a dichiarato.
- **Da decidere (seguito di D7-e)**: il prospetto 6 della 2025 differisce dai valori del codice (2016,
  prospetto 5) per XF1 (C30/37 contro C32/40, A/C 0,55 contro 0,50) e per il cemento minimo, inferiore
  in tutte le classi; il codice è più restrittivo. Opzioni e4 (allineare alla 2025 in F2.9, raccomandata)
  ed e5 (tenere la 2016 come scelta cautelativa dichiarata). Decisa poi e4 (sezione seguente).

## 7 ottobre 2026, pomeriggio: decisioni del coordinatore su delega dell'utente («esegui tutto te»), da ratificare

L'utente ha delegato le decisioni del pomeriggio al coordinatore dei workflow ("esegui tutto te"). Le decisioni
seguenti sono state prese dal coordinatore su delega e restano da ratificare dall'utente.

- **F1.6, destino dei progetti di test e seguiti** (dettaglio in [progetti-di-test.md](progetti-di-test.md) e
  nella sezione «F1.6» sopra):

  | Progetto o cartella | Decisione | Commit |
  | --- | --- | --- |
  | ConcreteDesign.DesktopChecks, ValidationIllustrations | codice nella configurazione `UiTests` (`--check-concrete-design`, `--capture-validation`); vecchi progetti in `supporto/SUPERATI/test` | cdd56ea |
  | BridgeDesign.SiteComparison, ConcreteStressDiagnosis, ValidazioneCA20260925, ProgrammaAnthea (`qa.py`) | archiviati in `supporto/SUPERATI/test/<nome>` con `git mv`, invariati, con registro `registro-20261007-test.json` | 4ff5026, f5647f5, d01caf7 |
  | MaxRetainingWall.Cases e .Compare | archiviati come sopra; la tabella rimandava la scelta all'utente: restano archiviati, scelta reversibile con `git mv` e documentata | 4ff5026 |
  | `supporto/script/validazione_ca_2026_09_25` | archiviato anche il generatore una tantum della relazione del 25/9, che importa le parti Python di ValidazioneCA20260925, in `supporto/SUPERATI/script/validazione_ca_2026_09_25`, con voci nel registro di SUPERATI; `build_document.py` al testo di prima di F1.6 | 3ff710e, a57aff6, a2a3ac1 |
  | ElasticPile.Performance | riportato in `supporto/test` con `git mv`; destinazione della tabella invariata: «Archiviare dopo F5, o spostare in `tools/`» | cb9b750 |
  | Cartelle `bin` e `obj` ignorate rimaste nelle vecchie posizioni | non si cancellano; le segnala il coordinatore all'utente | — |
  | Tabella, decisioni e piano | allineati ai seguiti | 99b2ae7 |

- **D7-e, seguito su XF1 e composizione**: si adotta l'opzione e4, allineamento alla UNI 11104:2025, coerente con
  «confermo la norma più aggiornata». Si attua in F2.9 insieme allo spostamento della durabilità nella libreria,
  dopo il riscontro del prospetto 6 su una copia con licenza della norma: la variazione su XF1 (C30/37 e A/C 0,55
  invece di C32/40 e 0,50) e sul cemento minimo è a sfavore rispetto al codice attuale. Fonti di oggi, descritte
  in modo riproducibile nel registro e in [scostamenti.md](scostamenti.md): pagine visibili dell'anteprima UNI
  (indice alle pp. III-IV, introduzione e punti 1 e 2 alle pp. 1-2; frontespizio e prospetto 6 non visibili) ed
  estratto pubblicato il 28/07/2025 (fonte secondaria). La lettura «a p. 12 dell'anteprima» è ritirata: la p. 12
  non è visibile, era un residuo di una versione precedente dentro il file. Se il testo smentisse l'estratto, la
  scelta fra e4 ed e5 torna all'utente.
- **F2.2, pagine dei metodi c.a.**: revisione tecnica accettata. Le pagine sono unite in Checker develop con i
  merge locali c555a3b8 (taglio, torsione, SLE, fessurazione) e 1fbaea61 (ancoraggi, dettagli, durabilità,
  momento-curvatura) più le correzioni del verificatore (5675492f e 398eb36a con i merge 9eb800d4 e 26fb6b8b;
  bf23a16d, 7d86ffc7, bd9938ac, e41a803a). Il push di Checker develop (punta e41a803a, 35 commit oltre
  origin/develop) resta all'utente. Le voci nuove a sfavore o da chiarire diventano R16-R21 del registro, stato
  «da-decidere», con la stessa decisione di R4-R14 («le voci sfavorevoli le guardiamo a fine refactoring», F5.15):
  R16 torsione DS con νv invece di νt = 0,7 (0,7 − fck/200); R17 classe indicativa di XC3 C25/30 contro C30/37 del
  prospetto E.1N del DM 2012; R18 incrudimento dell'acciaio di progetto riferito a fyk; R19 citazioni della
  torsione; R20 ρw,min DS non implementato; R21 condizione DIN (h − x)/3 con NominalCover invece del copriferro
  assegnato. R7 rettificata: NTC 2018 [4.1.38] dà 1 ≤ cot θ ≤ 2,5 anche in torsione (0,4 era della NTC 2008),
  quindi «NTC torsione: cot θ ≥ 1 anche in torsione pura» non è uno scostamento; resta da stabilire il segno del
  limite 1,3 (h − x) con barre tese fuori da Ac,eff (riquadro F-4).
- **F2.4, lib/Checker da commit pushati**: lo snapshot si compila dai commit dei rami remoti con
  `tools/libs/Update-Snapshot.ps1 -FromUpstream` (8d6a8ec): worktree temporanei sotto una radice fissa, così gli
  SHA-256 sono ripetibili. S2 ricompilata così (edc4fce, branch `refactoring/f2-4-snapshot-s2`) da Model
  origin/master 5ad56681 e Checker origin/develop 0d7ba50b, con le stesse versioni del candidato e `pushed: true`:
  la release non dipende più dal push di Checker develop, che dopo 0d7ba50b cambia solo `docs/metodi`.
- **F2.6, taglio e torsione nella libreria** (voci F2-3 e F2-4 del registro e sezione delle decisioni del branch
  `refactoring/f2-taglio-torsione`, da unire): la torsione senza staffe chiuse resta un rifiuto con il messaggio di
  oggi, controllato dall'adattatore prima della libreria (F2-3); i rifiuti e i limiti propri della libreria non
  raggiungibili dal modulo sono accettati, nessuna azione (F2-4); il corpus headless riceve casi c.a. con taglio e
  torsione (`tests/ANTHEA.Testing/corpus`) e la baseline B3 si cattura con il motore legacy prima dell'interruttore
  sulla libreria (`supporto/artefatti/baseline/F2-B3`, dcde952).
- **Tema scuro**: i difetti di colore della modalità scura (fra cui le righe di sezione non selezionate dell'albero
  dei progetti, chiare su chiaro, `ProjectHierarchy.cs:155`, segnalate anche dall'utente) si correggono sul branch
  dedicato `refactoring/ui-tema-scuro` (worktree Temp\aw-tema), da unire dopo verifica.
- **F1.4, quattro scenari sull'exe Release**: eseguiti dal coordinatore con UI Automation e mouse reale sull'exe
  Release compilato da main 17b6c98, con i dialoghi di produzione, invece che a mano con l'utente. Esito in
  `supporto/artefatti/refactoring/f1.4/esito.md`: avvio con la Home e 4 scenari superati.

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
