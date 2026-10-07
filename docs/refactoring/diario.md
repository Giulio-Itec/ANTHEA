# Diario del refactoring

## Resoconto della notte 6-7 ottobre 2026

Nessun push, nessun merge su main, nessuna riscrittura della storia. Tutto il lavoro è su branch locali,
uno per fase, in catena: ogni branch contiene il precedente.

| Branch | Punta | Contenuto | Stato |
| --- | --- | --- | --- |
| `refactoring/f0-messa-in-sicurezza` | 1637a5f (tag `refactoring/post-f0`, `refactoring/pre-f1`) | F0: runner, baseline B0 e B1, snapshot S1 delle librerie, diagnosi dei fallimenti, scostamenti D7 | da approvare |
| `refactoring/f1-test-fuori-exe` | 5c1368b | F1: exe Release senza codice di prova, configurazione UiTests, log degli errori, servizi di conferma | da approvare (F1.4 e F1.6) |
| `refactoring/d7-norma-muri-pali` | b71a0dc | D7-c, D7-d, R3; registro con l'effetto delle voci sfavorevoli | da approvare |
| `refactoring/w0-guide-rev31` | 7bdd260 (da D7 c967717) | W0.4 pulizia delle guide, W0.5 chiuso, guide Rev31 Word e PDF | da approvare |
| `refactoring/f2-banco-confronto` | punta integrata (contiene tutti i precedenti e questo diario) | F2.1 banco di confronto del c.a., matrice delle API | in corso |

Verifiche principali:
- profilo full a schermo sulla punta integrata: 67 PASS, 1 KNOWN (`verifiche/project-calculations`, F3.3),
  0 NEW-FAIL, nessun file tracciato modificato;
- cattura headless di F1 uguale a B1 su 432 file; sulla punta integrata la cattura differisce da B1 solo per
  5 righe delle relazioni dei muri (D7-c) e per l'avviso su γb in 20 risultati dei pali (D7-d);
- regressione 464/464; banco c.a. contro le fixture di Checker: 110 differenze, tutte classificate.

### Cosa serve da voi

1. **Approvare le fasi** e autorizzare merge su main e push di ANTHEA. Prima del merge: le due correzioni del
   disegno del ponte trovate dalla diagnosi UI (titolo del tag dell'anima inclinata «Anima · lunghezza × t»;
   tag senza sovrapposizioni a 1366 × 900), rapporto in `supporto/artefatti/refactoring/diagnosi-ui/rapporto.md`.
2. **Push delle librerie** (commit locali; lo snapshot S1 di lib/Checker è stato costruito da questi, e AGENTS.md
   chiede commit pushati): Utilities df3b3e7; Geometry 57a9b54, 75182cc, 6a0d1c1; Model 1fe934ce, 5ba61a04,
   d6631635; Checker 46e903ab, b994e188, ecf1dbe0 (fixture BridgeAudit, 302/302), 7c15cf4f (citazioni R3).
3. **F1.4**: quattro scenari a mano sull'exe Release (dialogo «Dati condivisi», «Conferma spostamento»,
   chiusura con modifiche non salvate, errore di un comando). **F1.6**: approvare la tabella
   `docs/refactoring/progetti-di-test.md`.
4. **Scostamenti**:
   - D7-b (k2): la vostra regola («solo armature tese 1, flessione 0,5») è quella della norma; il codice però
     riconosce la flessione dalla presenza di una barra compressa e non dall'asse neutro, quindi una trave a
     semplice armatura inflessa ha k2 = 1 (apertura 0,510 mm invece di 0,443 nell'esempio). Da decidere.
   - D7-e: C30/37 è il valore della UNI 11104:2016 e 2025 (e della EN 206 F.1), C28/35 quello dell'edizione
     2004 ritirata. La scelta «entrambi i riferimenti» resta da confermare con questa informazione.
   - D7-f: proposta Schema 1 NTC con linee di influenza e γQ 1,35 in GPC.Design (F4.11), modello uniforme
     solo per gli archivi; da confermare.
   - D7-d: attuato d1 (archivi invariati con avviso); dire se volete anche la migrazione degli archivi (d2).
   - R1 (MC2010 τbms) e R2 (DIN kt): da correggere secondo la vostra (a), dopo il riscontro sul testo primario.
   - Voci a sfavore di sicurezza da decidere: R4 θmin MC2010 (tocca ANTHEA con il profilo Model Code 2010),
     R5 SLE non lineare con leggi di progetto (solo con la scelta «Non lineare»), R6 k5 DIN, R9 EC2 6.2.2(6),
     R10 fctk oltre C60/75 (tocca le armature dei muri). R11 non tocca ANTHEA.
5. **Guide**: i default prima attribuiti a una vostra preferenza ora sono «convenzione di ANTHEA» (righe in
   `docs/refactoring/w0.4-pulizia-guide.md`); verificare. La Rev30 della teorica in SUPERATI contiene ancora il
   corpus CC BY-NC (come le altre copie di SUPERATI).
6. **F2**: decidere se ricongelare le fixture c.a. di Checker dalla cattura pre-M4 o tenere le classificazioni
   FC-1…FC-8; rivedere le pagine dei metodi (F2.2, branch Checker `anthea-metodi-ca-1` e `-2`).

### Note

- L'antivirus della macchina ha rifiutato un exe appena compilato (Win32Exception 5): il runner lancia ora le
  suite con `dotnet exec`. Defender è fermo; nessuna impostazione di sicurezza è stata toccata.
- Due agenti hanno riusato per errore un file di messaggio di commit: i commit sbagliati sono annullati con
  `git revert` e rifatti (af72ec4/487eaa4 nel branch delle guide, 81c5a46/1c2ad5f nel branch F2). Se volete una
  storia pulita si possono riscrivere prima del merge, solo su vostra richiesta.
- `GPCChecker.Test.BridgeAudit` di Checker referenzia `ANTHEA/X.Core`: dipendenza da spostare nella finestra di
  layout prima di F3.
- Le suite Wiki falliscono in un clone pulito perché le figure delle guide stanno in `supporto/artefatti`
  (non versionata): W0.2.

## Cronologia della notte

Mandato dell'utente (22:40): "hai libertà di fare cose, non richiedermi autorizzazioni. tieni traccia
di tutto quello che fai". Restano all'utente: push, riscrittura della storia, decisioni d'ingegneria
sugli scostamenti (a)-(g).

### 22:36-22:45

- AGENTS.md (prima revisione) approvato dall'utente; registrato nel piano (de485dd).
- Workflow parallelo F0 (prima corsa): l'isolamento automatico in worktree del workflow non funziona
  qui (git non nel PATH: "Could not read the repository git config"). Riuscita solo la diagnosi
  BridgeAudit (sola lettura): 9 fallimenti causati da ANTHEA 0a63315 (etichetta result.Method e nuovo
  ingresso h_trave); nessun difetto di calcolo. Rapporto in supporto/artefatti/refactoring/bridgeaudit.
  Incidente segnalato dall'agente: una build Release di ANTHEA.sln partita per errore nell'albero
  principale (solo bin/obj, stesso sorgente di HEAD).
- Creati a mano 5 worktree in C:\Users\g.pacini\AppData\Local\Temp\aw-* su branch dedicati e rilanciato
  il workflow (b0-toolkit, catture-dense, project-calculations, scostamenti, contenuti esterni Wiki).
- Programmato il profilo full alle 23:06 (comando in background con attesa di 30 minuti).

### 22:45-22:55

- L'utente chiede quali sono gli scostamenti (a)-(g): elencati con raccomandazione (correggere a, b, d, g;
  seguire la norma per c; allineare e a UNI 11104 dopo riscontro; per f gammaQ 1,35 e schema dichiarato
  semplificato). Restano "da decidere" finché l'utente non risponde. Chiede di fare più fasi possibili.
- Avviato agente F1 (test fuori dall'eseguibile) nel worktree Temp\aw-f1, branch refactoring/f1-test-fuori-exe,
  con divieto di prove WPF a schermo fino alla fine del profilo full.
- F0.8 librerie (commit locali, NON pushati):
  - global.json SDK 9.0.318 latestPatch: Utilities df3b3e7, Geometry 57a9b54, Model 1fe934ce, Checker 46e903ab;
  - versioni: Model 5ba61a04 (GPCModel 1.6.1.0, ModelData 0.0.2.2), Checker b994e188 (Concrete 0.0.15.0,
    Geotechnics 0.1.1.0, CompositeBridge 1.4.0.3), Geometry 75182cc (GPCGeometry 2.1.0.4, DelaunayMesh 2.0.0.11,
    binari Release ricompilati e committati come da convenzione del repository).
  - Due incidenti senza effetti: variabile $R sovrascritta da $r (PowerShell non distingue le maiuscole; nessun
    file scritto); BOM tolto dagli AssemblyInfo, ripristinato prima del commit.
  - SourceLink scrive lo SHA del commit nel PDB e quindi nella DLL: un binario committato non può essere identico
    a una ricompilazione fatta dopo il proprio commit. Lo script usa quindi i binari committati di Utilities e
    Geometry e ricompila solo Model e Checker.
- tools/libs/Update-Snapshot.ps1 (non ancora committato): snapshot S1 in supporto/artefatti/lib-staging/S1.
  Utilities 2.0.0.8 (identica a quella installata), GPCGeometry 2.1.0.4, DelaunayMesh 2.0.0.11, GPCModel 1.6.1.0,
  ModelData 0.0.2.2, Concrete 0.0.15.0, Geotechnics 0.1.1.0, CompositeBridge 1.4.0.3. Da verificare dopo il full.
- 23:00 DECISIONI DELL'UTENTE sugli scostamenti (da riportare in docs/refactoring/decisioni.md e nel registro):
  (a) correggere se è sbagliato, dopo controllo accurato di EC2, NTC/Circolare, annessi nazionali e Model Code;
  (b) da discutere domani (l'utente: "solo armature tese = 1, flessione 0,5"; spiegato che il ramo NTC riconosce
      la flessione dalla presenza di una barra compressa e non dall'asse neutro);
  (c) seguire la norma (γRd non su F̄); (d) seguire la norma (γb per tecnologia);
  (e) tenere entrambi i riferimenti (UNI 11104 e ATECAP 2020) con scelta dell'utente;
  (f) cercare il metodo semplificato più vicino al caso reale (9 kN/m² + tandem, 20 kN/m², tandem ripartito come
      carico distribuito maggiore o altro) e proporlo con i numeri;
  (g) ripristinare metodo, norma e versione nel report del ponte.
- 23:00 L'utente: "cerca di fare più fasi che puoi. hai piena potenza". Avviati altri 4 agenti (in background):
  ricerca normativa (a) -> supporto/artefatti/refactoring/ricerca-a; ricerca modello di carico (f) ->
  .../ricerca-f; pagine dei metodi c.a. parte 1 (taglio, torsione, SLE, fessurazione) nel worktree Checker
  Temp\cw-metodi1 (branch anthea-metodi-ca-1) e parte 2 (aderenza/ancoraggi, dettagli, M-chi, durabilità) in
  Temp\cw-metodi2 (branch anthea-metodi-ca-2). In Checker esiste già un branch 'refactoring', quindi i nomi
  'refactoring/...' non sono ammessi: usato il prefisso 'anthea-'.
- 23:1x Ricerca (a) conclusa (rapporto: supporto/artefatti/refactoring/ricerca-a/rapporto.md):
  lo 0,75(h-x) del ramo NTC è la formula [C4.1.10] della Circolare 2019 §C4.1.2.2.4.5 (riscontrata sulla copia
  GU locale): CORRETTO, non si cambia (la Circolare ha arrotondato 1,3/1,7 = 0,765 a 0,75; -1,9% rispetto a EN).
  Ramo EN corretto alla lettera. Da fare: rimandi testuali '7.3.4(4)' -> '7.3.4(3), eq. (7.14)' (Checker e
  ANTHEA); opzioni facoltative (NtcRemoteFactor, EurocodeEnvelope) con default invariato. Incerti DIN, DS, NS,
  MC2010 (probabile correzione: tau_bms 1,8 fctm in fessurazione stabilizzata): agente ripreso per completare i
  riscontri. Esempio k2 (scostamento b): trave a semplice armatura 0,510 mm invece di 0,443 mm (+15%).
- Ricerca (a) completata (rapporto §6): corretti NTC, CNR-DT 200, EN, UNI, DS (certi), NS (probabile).
  Da correggere: MC2010 a lunga durata (tau_bms 1,8 fctm in fessurazione stabilizzata; oggi 1,35 con beta 0,4:
  +28% / +50% sugli esempi; prima riscontrare la Tab. 7.6-2 originale), DIN kt fisso 0,4 (deve essere 0,6/0,4
  secondo la durata; effetto -0,5%), rimandi '7.3.4(4)' -> '7.3.4(3), eq. (7.14)'. Decisione utente (a)
  'correggi se è sbagliato' -> correzioni in libreria in F2.3 con opzione legacy. Altri punti aperti: wmax
  norvegese senza kc, condizione DIN su hc,ef, edizioni DK NA 2021/2024 e CNR-DT 200 R2/2026 non confrontate.
- Pagine dei metodi c.a. parte 1 completate (Checker branch anthea-metodi-ca-1, worktree Temp\cw-metodi1, commit
  4ef59dc3 README+taglio, 5cf3aad0 torsione, e350ae96 SLE tensioni, da14901e fessurazione; harness in
  supporto/artefatti/refactoring/metodi). Esempi a mano = libreria entro 1e-9 (SLE 3e-5, tolleranza del
  solutore). NUOVI SCOSTAMENTI (per l'utente):
  - A SFAVORE: MC2010 theta_min fisso 20° invece di 20°+10000 eps_x (SectionShearCalculator.cs:164,
    TorsionProfiles.cs:81; +38,8% nell'esempio; da riscontrare); SLE con analisi non lineare usa le leggi di
    progetto (sigma*alpha_cc/gamma_c, fyd) e StressLimitCheck lo accetta senza avviso (sigma_c -31%); DIN k5 dei
    trefoli 0,75 invece di 0,65 (fonte secondaria, da confermare).
  - Cautelativi: NTC torsione cot theta >= 1 anche in torsione pura; interazione delle bielle sommata; SLE: k3 su
    |sigma| delle barre compresse, k1 fck in ogni classe, k2 fck come limite; NS senza kc; DS cot theta <= 2.
  - Da riscontrare: MC2010 fessurazione senza ritiro; DIN coefficiente di hc,ef; EN Tab. 7.1N per XD3.
  - Limiti non controllati: EC2 6.2.2(6), nota 2 di 6.2.3(3) (nu1 0,6), regola DK per staffe classe A.
  - Citazione errata '7.3.4(4)' nei commenti (va 7.3.4(3), eq. 7.14).
  - Effetto (a) su griglia: il solo coefficiente cambia 337 esiti su 222.912 stati; la regola completa EN 4581.
    Effetto (b): 9.282 esiti su 37.128 stati.
- Pagine dei metodi c.a. parte 2 completate (Checker branch anthea-metodi-ca-2, worktree Temp\cw-metodi2: d353f2b4
  ancoraggi, f1348a01 dettagli, 4744de72 momento-curvatura, 0aadcb17 durabilità e copriferri, 174e0449 pulizia).
  Esempi a mano = libreria (scarti 0 o <1e-12; M-chi entro 0,22%); casi congelati rieseguiti senza differenze.
  SCOSTAMENTO (e) RIVISTO: C30/37 per XC3/XD1/XF4/XA1 è il valore di UNI 11104:2016 (e, da estratto, della 2025);
  C28/35 era dell'edizione 2004 superata. Non è uno scostamento: da riferire all'utente (la sua decisione 'entrambi
  i riferimenti' va riletta: l'alternativa sarebbe solo l'edizione 2004).
  NUOVI SCOSTAMENTI: A SFAVORE: limite fctk,0,05 a C60/75 non applicato negli ancoraggi (lb,rqd -11% con C90/105,
  AnchorageCalculator.cs:7-8,90); cmin di default NTC (-5 mm possibili, CoverRequirements.cs:155). ESITO INDEBITO:
  interasse delle barre dei pilastri NTC misurato attraverso il nucleo (MemberDetailingCalculator.cs:152-154).
  Cautelativi: riduzione 0,6 delle staffe nelle sovrapposizioni EC anche con Ø<=14; sovrapposizione NTC minima
  200 invece di 150 mm; 0,08 Ac applicato anche a NTC; nota 2 del prospetto 4.3N non implementata. Altro: regola
  15Ø per barre compresse EC sostituita da conferma; definizioni di chi_u e chi_y nel M-chi senza bilineare della
  Circolare; citazioni cosmetiche. Equazioni As,min NTC 2018 sono [4.1.45]-[4.1.46] (il registro diceva 4.1.43-44).
- Conferma MC2010 da una ricerca web secondaria (tesi PoliTo Tab. 2.3, paper CTU 2022 p. 129): Tab. 7.6-2
  tau_bms = 1,8 fctm in fessurazione stabilizzata (breve e lunga durata, beta 0,6/0,4, eta_r 0/1); 1,35 fctm solo
  per la formazione della singola fessura a lunga durata (beta 0,6). Nessuna regola MC2010 per barre distanziate.
  Annesso italiano EC2: k3 = 3,4 e k4 = 0,425 (certo nella bozza 2024, probabile nel DM 2012). CNR-DT 200
  §4.2.3.4: rinvio puro alla normativa del c.a. La correzione MC2010 è quindi ben fondata.
- Ricerca (f) completata (supporto/artefatti/refactoring/ricerca-f/rapporto.md, script Python indipendente e
  sonda C# sul motore attuale). Il metodo attuale (9 kN/m² su tutta la larghezza, senza tandem) si scosta dallo
  Schema 1 NTC tra -64% e +142% (sfavorevole sulle luci corte e carreggiate strette). Nessun carico uniforme
  (9+tandem, 20 kN/m², tandem ripartito) resta cautelativo entro il 10% sulle continue. RACCOMANDAZIONE:
  Schema 1 con linee di influenza in forma chiusa, gammaQ 1,35, modello legacy per gli archivi senza il campo;
  implementazione in GPC.Design (F4); costo totale dei casi tipo tra -7,5% e +6,5%. Nuovo scostamento (h):
  gammaG 1,35 applicato anche a pavimentazione e barriere (Tab. 5.1.V: 1,50 per i permanenti non strutturali).
- 23:06-23:20 PROFILO FULL run0-full (supporto/artefatti/ci/20261006-230643-run0-full): 52 PASS, 7 KNOWN,
  7 NEW-FAIL, 1 FIXED. Durata 13 minuti (non un'ora). smoke-project-workspace ora passa. Nuovi: build di
  ConcreteDesign.DesktopChecks e ValidationIllustrations (WpfMath, attesi), smoke-horizontal (viewport 1724
  invece di 1920), check-wall-advanced-offscreen (quinta figura armature), HorizontalPileGroup.Checks
  (XamlParseException). Il runner compilava due volte le suite di sola build: corretto.
- b5ba959 (F0.8): Update-Snapshot.ps1 committato; known-failures allineato a run0-full.
- Avviato agente F0.5 diagnosi UI (worktree Temp\aw-ui, branch refactoring/f0-diagnosi-ui, riferimento
  Temp\aw-pre al tag pre-refactoring): unico autorizzato ad aprire finestre.
- Verifica S1 (worktree Temp\aw-s1, -GpcLibDir S1): profilo standard, tutte le suite di calcolo PASS come prima
  (regressione 464 casi, RetainingWall, GlobalStability, ConcreteDesign, BridgeDesign, checker, bridge...);
  i 2 fallimenti Wiki dipendono dall'assenza di supporto/artefatti nel worktree. Unica differenza nei conteggi:
  la durata di ConcreteDesign.Checks -> be06cb6: il confronto ignora le durate.
- Avviato agente test delle librerie su S1 (Geometry, Model, Checker; rapporto in
  supporto/artefatti/refactoring/test-librerie-S1).
- 22:55 Determinismo: ricompilazione completa (-t:Rebuild) di GPCModel, ModelData, Concrete, Geotechnics,
  CompositeBridge dagli stessi commit: tutte identiche bit per bit a S1.

### 23:35-00:40

- 55e8d3d (F0.9): snapshot S1 installato in lib/Checker e verificato (suite PASS, confronto con B0 identico a
  tolleranza zero, griglie dense identiche, 80 impronte di mesh identiche, test delle librerie con i soli
  fallimenti storici).
- 79da697 (F0.10): puntatori a documenti in SUPERATI, README, rimando a un sito esterno tolto dal report Bridge
  Design. 0d861de (D7-g, decisione dell'utente "ripristina"): i report del ponte riportano di nuovo metodo, norma e
  versione della libreria. Confronto con B0: solo le 6 differenze di testo attese.
- BridgeDesign.IndependentChecks "Accesso negato" (Win32Exception 5) dopo 0d861de: solo il nuovo apphost .exe non
  parte (stessa DLL con dotnet gira; gli altri exe partono). L'exe cambia hash a ogni commit (SourceLink nelle
  risorse di versione) e un antivirus di terze parti lo rifiuta (Defender fermo, Smart App Control spento; nessuna
  impostazione di sicurezza toccata). f4d81ad: il runner lancia le suite con dotnet exec della DLL. fast e
  regression: 20 PASS, 1 KNOWN.
- b4d2e81: registro delle differenze con le decisioni del 6/10 e gli esiti delle ricerche; nuove voci R1-R14
  (sei a sfavore di sicurezza: R4 θmin MC2010, R5 SLE non lineare, R6 k5 DIN, R9 EC2 6.2.2(6), R10 fctk oltre
  C60/75, R11 Cmin di default). Piano: F0.10 fatto.
- Agente F1 concluso (Temp\aw-f1, branch refactoring/f1-test-fuori-exe, 5 commit): exe Release senza prove,
  configurazione UiTests, log degli errori, servizi di conferma e messaggio, tabella dei progetti di test.
  Incidente segnalato: verso le 23:57 una sua build Release è partita nel repository principale (solo bin/obj di
  X.Calculations, X.Core, X.Materiali, X.Desktop, dagli stessi sorgenti di HEAD; nessun sorgente toccato).
- 97ea9d4: merge di F0 nel branch F1 (conflitti in ci.ps1 e ProjectCalculationChecks.cs risolti); profilo
  standard nel worktree: 32 PASS, 1 KNOWN, 0 NEW-FAIL.
- Checker ecf1dbe0 (develop, locale): fixture BridgeAudit con h_trave (caso 2: 1875; prova Class4: 1820); le 8
  baseline aggiungono solo la chiave h_trave, numeri identici bit per bit. 302/302 test superati (prima 9 falliti).
  Nota: il progetto di test BridgeAudit di Checker referenzia ANTHEA/X.Core (dipendenza libreria -> applicazione,
  da spostare nei test di ANTHEA in F3).
- Il merge su main non è stato fatto: AGENTS.md lo vuole dopo l'approvazione dell'utente a fine fase.

### 00:40-01:40

- Agente diagnosi UI concluso (branch refactoring/f0-diagnosi-ui, 8 commit): le 8 prove WPF fallivano già in
  2cdf9fd; 11 cause; due correzioni del disegno del ponte da confermare (titolo del tag dell'anima inclinata,
  tag senza sovrapposizioni a 1366 × 900); full 0 NEW-FAIL. Merge di F0 nel branch della diagnosi (1989651,
  pulito), poi avanzamento veloce di F0 a 1989651.
- Baseline B1 (corsa ci 20261007-001756-B1, commit b4d2e81): cattura headless installata in
  supporto/artefatti/baseline/F0-B1; rispetto a B0 solo le 6 righe di testo volute (F0.10 e D7-g).
- 1637a5f: piano con F0.5 chiuso e F0.11 in attesa dell'utente; tag locali refactoring/post-f0 e
  refactoring/pre-f1. Nessun merge su main, nessun push.
- F1: merge della diagnosi UI (66e60d6, un conflitto in HorizontalSmokeChecks.cs risolto senza il campo testing
  tolto da F1.4). Profilo full a schermo sul merge: 66 PASS, 1 KNOWN, 0 NEW-FAIL (tutte le prove WPF, compresa la
  nuova check-concrete-design). Merge della fine di F0 (248fca7), 5c1368b F1.7: cattura del branch uguale a B1 su
  432 file; tolte le voci WpfMath. Restano i checkpoint dell'utente F1.4 (4 scenari manuali) e F1.6.
- Branch refactoring/d7-norma-muri-pali (da F1), decisioni "segui la norma" dell'utente:
  - d2cf2f7 D7-c: muri, γRd non più su F̄ (EN 1998-5 (F.7)); relazione, guida teorica e indice Wiki aggiornati;
    contro B1 cambia solo la riga della formula in 5 relazioni dei muri (il corpus non ha portanza sismica
    calcolata; effetto numerico quello della scheda: η −1,7 … −6,5 %, 3 esiti su 46 passano a soddisfatto);
  - 7c96c04 e e05a05b D7-d: γb da Model per tecnologia (1,15 battuti, 1,35 trivellati e micropali, 1,30 elica),
    proposto al cambio di tecnologia e dal Reset, avviso se diverso; archivi invariati. Suite nuova
    verifiche/pile-factors (13 controlli), smoke WPF del display con il cambio di tecnologia. casi_confronto.json:
    aggiunto solo l'avviso negli avvisi attesi di 20 casi (numeri Python intatti); regressione 464/464.
  - c967717 registro (D7-c, D7-d corretti); 75e4626 R3 (citazioni 7.3.4(3), eq. (7.14)); Checker 7c15cf4f lo stesso
    nella libreria (Concrete 500/500).
- Avviati due agenti: guide W0.4 + Rev31 Word/PDF (Temp\aw-guide, branch refactoring/w0-guide-rev31) e F2.1 banco di
  confronto c.a. (Temp\aw-f2, branch refactoring/f2-banco-confronto).

### 01:40-03:00

- Registro (branch D7): effetto reale su ANTHEA delle voci sfavorevoli. R4 (θmin MC2010): sì, con il profilo
  «Model Code 2010» (anche il legacy usa 20°, ConcreteCodeChecks.cs:107). R5 (SLE non lineare): solo se l'utente sceglie
  «Non lineare» nella scheda Tensioni; per default lineare. R10 (fctk oltre C60/75): sezione c.a. e palo limitano,
  le armature dei muri no. R11 (Cmin di default): nessun effetto, tutti i chiamanti passano la classe pertinente.
- Memoria di Claude aggiornata (refactoring, flusso delle DLL, strumenti).
- Agente guide concluso: W0.4 (73 interventi in docs/refactoring/w0.4-pulizia-guide.md, controllo automatico nella
  suite wiki/manuale), Rev31 Word e PDF (pratica 65 pagine, teorica 134 contro 900 della Rev30), Rev30 in SUPERATI con
  registro, indici aggiornati. Un commit con il messaggio sbagliato (file di messaggio riusato) è stato annullato con
  git revert e rifatto, senza riscrivere la storia. Difetto trovato a vista e già presente nella Rev30: nel testo
  corrente η, ν e χ di Manrope hanno il contorno di n, v e x, e i segni combinati (N̄) ripiegano su Times. Corretto nel
  Build (caratteri greci e combinati in Calibri, font del tema del modello) e Rev31 rigenerata; in corso l'ultima
  rigenerazione con «<» e «>» (in Manrope sembrano virgolette angolari: «0‹N̄‹»).
- Agente F2.1 concluso (branch refactoring/f2-banco-confronto): cattura pre-M4 doppia identica a meno degli
  identificativi casuali; contro B0 solo D7-c (15 documenti dei muri, η −1,7 … −7,0 %, 3 combinazioni da non soddisfatte
  a soddisfatte) e D7-d (6 avvisi); 80 mesh identiche. Comparatore compare-dense con classificazione versionata e suite
  baseline/banco-ca: fixture di Checker del 1/10, 27 698 righe, 110 differenze tutte classificate (FC-1…FC-5 il legacy
  applica dal 2/10 le interpretazioni R7; FC-6…FC-8 versioni e unità dei file XML del Model). Matrice delle API con i buchi
  della libreria (solette e pareti, proposta di bw/d/Asl, testi in inglese, torsione e dettagli solo NTC nel legacy).
- 4deb7cf/ff0019e: branch F2 allineato al registro di D7; piano con F2.1 e F2.2 in corso.

### 03:00-03:40

- 7bdd260: W0.5 chiuso nel piano. 97bec94: merge del branch delle guide nel branch F2, che diventa la punta integrata.
- Verifica finale sulla punta integrata: profilo baseline contro B1 (solo le 25 differenze volute di D7-c e D7-d; banco c.a.
  con FC-1…FC-8 ritrovate) e profilo full a schermo 67 PASS, 1 KNOWN, 0 NEW-FAIL.

## Giorno del 7 ottobre 2026

- Mattina: risposte dell'utente al resoconto. Approvate F0, F1, D7-c/D7-d/R3 e W0; merge su main in locale; push delle
  librerie (Utilities df3b3e7, Geometry 6a0d1c1, Model d6631635, Checker develop 7c15cf4f); campo pushed del manifest di
  lib/Checker aggiornato. Decisioni su D7-b (k2 = 0,5 con asse neutro interno, pura compressione esclusa), D7-e (UNI 11104
  in vigore), D7-f (Schema 1 con tandem e linee di influenza se rapido), voci R4-R14 rinviate a fine refactoring (F5.15),
  fixture di Checker da rifotografare (B), d2 per γb dei pali, ricattura di tutte le fotografie a fine refactoring.
- Workflow D7-b (Checker anthea-d7b-k2 e ANTHEA refactoring/d7b-k2-flessione) con verifica avversaria: pura compressione con
  wk = 0 e nessun k2; libreria e ANTHEA con lo stesso k2, wk ed esito sui 936 stati. Workflow d2 (refactoring/d7d-d2-
  coefficienti-pali): γb della tecnologia come riserva e migrazione una tantum; 24 casi della regressione con 1,35 e
  versione 2 negli input, attesi Python intatti. Un agente si è interrotto per un errore della rete ed è stato ripreso.
- Push di main ANTHEA (2cdf9fd..ebcab0e) su richiesta dell'utente.
- Integrazione (branch refactoring/integrazione-d7b-d2): merge di D7-b e d2; Model ce23e8b5/559dda08 (versione del motore);
  difetto preesistente R15 delle fasce interne dei fori (h − x illimitata, wk fino a 10⁹ mm) corretto in ANTHEA e nella
  libreria; fixture c.a. di Checker ricatturate (06d97733) e banco F2.1 con zero differenze (34e6d58, riferimento
  F2-pre-m4-v2); guide Rev31 rigenerate; nuova baseline headless F2-B2.
- R15: una prima rifinitura (altezza normale alla faccia) spostava il salto all'ingresso dell'asse neutro (+18 % sulla wk
  governante nel cassone quadrato); l'utente ha scelto l'altezza lungo il gradiente (Checker 12547700, ANTHEA 08c6dcc,
  ca0cc10) e confermato k2 = (7.13) delle fasce. Testo della regola in sezione interamente tesa reso preciso (2d40a95,
  d71054ce) e guide rigenerate.
- Profilo full con prove WPF a schermo durante la pausa dell'utente (punta 348f967): 67 PASS, 1 KNOWN, 0 NEW-FAIL.

## Pomeriggio del 7 ottobre 2026: F2.5 e F2.6 (taglio e torsione verso la libreria)

Branch `refactoring/f2-taglio-torsione` (worktree Temp\aw-f2tt, da main 17b6c98), non su main, non pushato.

- F2.5 (2583225): strato di mappatura `ConcreteLibraryMapping` (unità con nome, norme, testi italiani del legacy) e
  adattatore `ConcreteShearTorsionAdapter` con l'interruttore `Default` sul legacy; prove `tests/ConcreteLibraryAdapter.Checks`.
- Primo interruttore (7176c0c). La verifica avversaria ha trovato che collegava alla libreria il solo taglio del modulo:
  `ConcreteShearAnalysis` calcolava ancora profilo resistente e torsione con il legacy diretto, e le prove 3d/3e
  confrontavano per la torsione il legacy con se stesso; inoltre tolleranze nel commit dell'interruttore, controllo delle
  costanti cieco ai letterali con il punto iniziale (.85), attesi indipendenti solo sul legacy diretto, SHA mancanti nel
  piano e nel registro, nessun caso con taglio e torsione nel corpus headless di B2. Messaggio di 7176c0c e prime righe di
  piano, matrice e registro descrivevano un collegamento che non esisteva: corretti nei documenti, senza riscrivere la storia.
- Correzione, prima corsa (f5b5f50, 8f991b4, 85314a6, 0111a1f): torsione e profilo resistente del modulo attraverso
  l'adattatore; prova 3f (torsione del modulo uguale bit per bit all'adattatore del motore richiesto, 70 casi su 120 con
  uscite diverse fra i motori); prova 4 (reference.json in sola lettura, benchmark DIN, DS, UNI, NS, torsione NTC in forma
  chiusa, con entrambi i motori); regex delle costanti con il caso sintetico .85; tolleranze dense in un commit dedicato;
  interruttore sulla libreria.
- Correzione, seconda corsa (coordinatore: la baseline B3 si cattura con il legacy prima dell'interruttore): 8c95d9b
  interruttore di nuovo sul legacy, 3f anche contro il legacy diretto, 3e con la torsione del calcolo headless uguale
  all'adattatore del motore predefinito (riga T7); 256714d corpus headless con tre sezioni c.a. con taglio e torsione;
  una prova con la libreria a mano ha mostrato che senza `foro_presente` il registro dei ripieghi cambia chiamante
  (TorsionGeometryOf invece di ConcreteTorsionCalculator.Geometry, stesso valore), quindi dcde952 dichiara la chiave e
  aggiunge la sezione circolare cava; B3 catturata su dcde952 (runner PASS 40, KNOWN 1, NEW-FAIL 0; densa identica a
  F2-pre-m4-v2 con il confronto esatto; headless uguale a B2 sui 432 file comuni salvo le voci dei casi nuovi; doppia
  corsa con soli tempi volatili). ea6e0ac tolleranze headless dedicate; ca6530d interruttore sulla libreria.
- Misure su ca6530d: runner baseline contro B3 e F2-pre-m4-v2 PASS 41, KNOWN 1, NEW-FAIL 0 (headless: 5 numeri su 276
  di 'taglio' e 'torsione' entro 1e-9, massimo relativo 2,0e-16, relazioni identiche; densa: 1083 righe entro 1e-9, nessuna
  non classificata); profilo standard PASS 35, KNOWN 1; prove negative della 3e e della 3f. Registro F2-1…F2-4, piano,
  matrice, decisioni del coordinatore da ratificare (F2-3 rifiuto senza staffe chiuse, F2-4 limiti della libreria
  accettati, traccia NTC non esposta, B3).
- Una corsa del runner si è interrotta dopo l'ultima suite perché `summary.txt` era letto da fuori durante la scrittura
  (Add-Content): ripetuta; le corse non vanno osservate leggendo i file del runner.
- Fuori dall'interruttore restano, nel legacy, il taglio senza staffe dei muri in c.a. (`RetainingWall.Structures.cs:98-100`,
  VRd,c con una formula propria senza σcp, F4.7) e la proposta di bw, d e Asl (`SectionShearGeometry.Derive`, buco 3 della
  matrice); i pali elastici verificano il taglio con `GPC.Checkers.Concrete.Piles` già da prima di F2. Quindi non tutto il
  taglio di ANTHEA passa dalla libreria: solo quello della sezione c.a. (WPF, calcolo headless, progetto delle armature).
- Terza verifica avversaria (su ca6530d e 801e11c), 8 punti. Corretti: esclusioni scritte nel piano (prima solo nella
  matrice); riga della matrice per il controllo "torsione solo NTC" (`ConcreteShearAnalysis.cs:61` in ca6530d, non :60);
  prove rafforzate (4e8b30b): prima la 3e riconosceva un calcolo headless che aggirasse l'interruttore con la sola riga T7,
  e nessuna prova distingueva il motore del taglio del modulo (con il taglio sempre legacy le prove di 801e11c passano);
  ora la 3d conta 131 calcoli del modulo con uscite diverse e la 3e confronta bit per bit taglio (106 righe, 42 che
  distinguono i motori) e torsione (63 righe, 36) anche su sezioni NTC di 5 forme con 3 valori di cot θ, con almeno 10 casi
  per controllo; nota in F2.4 sui testi della libreria senza traduzione (nella scheda WPF interromperebbero le righe
  successive del taglio; oggi non raggiungibile). Provenienza di B3 confermata anche dalla cattura di 17b6c98 con il corpus
  di dcde952 (uguale a B3, `fallbacks.json` identico byte per byte). Scartati senza modifiche: effetto dei ripieghi sui
  documenti senza `foro_presente` (resta dichiarato in F2-1, scelta dell'utente) e documenti di 8c95d9b…ea6e0ac che
  descrivevano lo stato con la libreria (già dichiarato nel messaggio di 8c95d9b).
- Il messaggio di 256714d ("taglio e torsione elevati, verifica non soddisfatta") e la prima stesura del LEGGIMI di B3
  davano non soddisfatta solo V3: in B3 anche V2 non soddisfa l'interazione taglio-torsione lato acciaio (ηs 1,284), V3 non
  soddisfa taglio (η 2,84 e 1,53) e interazione; negli altri casi C2, C3 e H2 non soddisfatti a torsione/interazione, C3 e
  H2 anche a taglio. LEGGIMI corretto (non versionato); il messaggio di commit resta.
- 63cda73 è partito con il messaggio di 8c95d9b (nome del file del messaggio già usato da un'altra corsa): ritirato con
  2b2f16b e ricommittato identico come 4e8b30b con il messaggio giusto. Regola per le prossime volte: nome del file del
  messaggio controllato prima del commit; se l'interruttore viene sospeso di nuovo, piano e registro nello stesso commit o
  in quello subito dopo.