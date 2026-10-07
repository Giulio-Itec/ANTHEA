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
- **(d) γb dei pali**: l'utente chiede quali archivi toccherebbe la migrazione d2; resta d1 fino alla risposta.
- **Voci sfavorevoli R4-R14**: "le guardiamo a fine refactoring. segna di ridiscuterne". Restano da decidere,
  con discussione fissata alla chiusura del refactoring (dopo F5).
- **Guide**: "ok" ai default dichiarati come «convenzione di ANTHEA» e alla Rev30 lasciata in SUPERATI.
- **Fixture di Checker (F2.1)**: "B: rifai la fotografia del comportamento attuale, appena sicuri del codice
  dietro". Le fixture congelate il 1/10 dal legacy di ANTHEA si ricatturano dal comportamento attuale dopo la
  verifica di D7-b; spariscono le classificazioni FC-1…FC-8 e i casi speciali dei test di migrazione; le
  fixture precedenti restano nella storia di git.

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
