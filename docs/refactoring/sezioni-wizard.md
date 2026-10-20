# Sezioni in c.a.: wizard tipologici

Attività del 20 ottobre 2026 sul ramo `refactoring/f5-sezioni-persistenza`,
derivato da `refactoring/f5` a `aa70a263`. La persistenza nativa è descritta
in [sezioni-persistenza.md](sezioni-persistenza.md).

## Funzioni e ripartizione

Il comando «Wizard tipologie / modifica disposizione…» propone dodici
configurazioni:

1. rettangolo con doppia fila superiore e inferiore;
2. rettangolo cavo con armature sui due contorni;
3. cerchio con due anelli;
4. cerchio cavo con anello interno ed esterno;
5. T;
6. I, con dimensioni indipendenti delle due ali;
7. ellisse con armatura radiale;
8. capsula con due semicirconferenze e tratto centrale rettilineo;
9. L;
10. C aperta, con ali sullo stesso lato;
11. trapezio;
12. poligono regolare da 3 a 32 lati.

Le ultime due sono aggiunte nell'autonomia concessa dall'utente. I primi
generatori ordinari di rettangolo, cerchio e T rimangono quelli esistenti.
I nuovi generatori sono in `GPC.Design/Sections/ConcreteSectionTemplates.cs`
nel repository Checker: contorni esterni e fori, disposizione delle barre,
staffe, controlli geometrici e centratura. Non sono nuove formule in
X.Calculations: `SezioneCA` adatta il risultato GPC e gli altri adattatori
escludono esplicitamente le automatizzazioni non estese.

ANTHEA.ConcreteWorkspace converte la ricetta in parametri tipizzati GPC.
Application gestisce bozza e applicazione transazionale, Presentation
gestisce campi e validazione; WPF rende contorni, barre e staffe. La selezione
e l'annullamento non modificano la sezione corrente. Prima della conferma si
prepara l'intero modello, inclusi eventuali trefoli già presenti.

Il documento conserva modello GPC e ricetta. Il reload nell'editor mantiene
gli identificativi nativi: il calcolo riusa effettivamente la sezione
archiviata. L'editor ordinario non mostra campi di geometria o numero di
barre che non governano la tipologia avanzata; la modifica passa dal wizard,
mentre l'elenco delle coordinate mantiene il flusso delle barre manuali.
I report mostrano dimensioni pertinenti, barre effettive e note sulle staffe.

## Ipotesi e limiti delle armature trasversali

Quote in mm, copriferro netto misurato alla superficie esterna delle staffe.
Barre longitudinali disposte sul contorno arretrato e controllate per
contenimento e sovrapposizione. I collegamenti chiusi delle forme composte
interessano i singoli elementi; le sezioni cave hanno contorni distinti.
Le barre dei profili curvi seguono raggi ad angolo uniforme.

La generazione è una disposizione geometrica iniziale: non dettaglia
uncini, raggi di piegatura e ancoraggi e non dimostra il trattenimento
costruttivo di ogni barra. Non si attribuisce un numero di bracci efficaci
alla sola intersezione grafica delle staffe.

Per le tipologie avanzate il wizard seleziona il taglio manuale e svuota
`bw`, `d`, `Asl` e i bracci efficaci nelle due direzioni. Si azzerano gli
schemi trasversali incompatibili ereditati dalla sezione precedente.
Ancoraggio e chiusura torsionale tornano da confermare. L'utente deve
assegnare i parametri del modello di taglio pertinente: nessuna verifica
automatica completa di taglio/torsione dei profili aperti è introdotta.
Anche ottimizzazione dell'armatura e dettagli costruttivi automatici non
supportati vengono rifiutati esplicitamente.

La griglia di fibre 48 × 48 dei nuovi profili serve alla rappresentazione
delle tensioni. L'equilibrio usa la sezione e la mesh native GPC. Non sono
modificati DelaunayMesh, Geometry o Utilities.

## Librerie e riproduzione locale

Checker, ramo `feature/concrete-section-wizards`, commit `894892c1`.
GPC.Design passa a 0.29.0.0 per la nuova funzionalità. Le altre DLL Checker
ricompilate passano al successivo componente di fix: Concrete 0.12.0.5,
Steel 2.2.0.6, Geotechnics 0.13.0.2, CompositeBridge 1.7.0.15,
ModelChecker 2.1.0.23 e Masonry 0.1.0.18. Model, Geometry e Utilities restano
invariati. La prima preparazione di staging è stata respinta dal controllo
versioni/hash: si è corretta la versione delle DLL ricompilate, senza
aggirare il controllo.

Set candidato completo:
`supporto/artefatti/sezioni-wizard/gpc-candidate-02`, prodotto da
`Checker/build/Release-Set.ps1 -Candidate`, con manifest e suite delle
librerie. Esito: 3.176 PASS e i 17 fallimenti Steel già ammessi dalla
baseline della libreria; nessuna nuova deroga. GPC.Design 95 PASS,
Concrete 795 PASS. Controlli indipendenti di aree/baricentri, posizioni
radiali, copriferro lungo i collegamenti e input invalidi.

Staging coerente per ANTHEA:
`supporto/artefatti/sezioni-wizard/snapshot-02`, ottenuto con
`tools/libs/Update-Snapshot.ps1 -AllowCandidate -Staging`, senza installazione.
Per compilare e verificare questa funzionalità:

```powershell
& ./build/ci.ps1 -Profile standard `
  -GpcLibDir "$PWD/supporto/artefatti/sezioni-wizard/snapshot-02" `
  -Tag sezioni-wizard
```

Il set AA ufficiale in `lib/Checker` non cambia. La compilazione delle nuove
funzioni richiede lo staging indicato. Il rilascio ufficiale resta separato:
richiede commit pushati, set coerente e profilo full secondo AGENTS.md.
Nessun push, merge, installer o modifica all'installazione dell'utente
eseguito per questa attività.

## Catture, confronti e collaudo

Evidenze sotto `supporto/artefatti/`.

- Prima della persistenza: `ci/20261020-122913-sezioni-persistenza-prima`.
- Primo wizard: `ci/20261020-131414-sezioni-wizard-01`, 7 PASS.
- Standard completo: `ci/20261020-132024-sezioni-wizard-standard`,
  72 PASS e tre fallimenti di confronto del formato documentale, nei
  controlli regressione/software/project-calculations. Nessuna differenza
  numerica fuori tolleranza: 942.254 valori confrontati, massimo scarto
  assoluto 5,4569682106375694e-12 e relativo scalato
  2,6489527275427796e-14. Adattatore Concrete completo superato, comprese
  172.700 prove taglio/torsione e 677.561 SLE; Presentation 3.433,
  Spettri 1.572, Reports 328 e 151 PDF con testo corrispondente al DOCX.
- I tre confronti documentali ora usano
  `tests/Shared/ConcretePersistenceComparison.cs`: si proietta solo
  `input.sezione_gpc` dentro un workspace c.a., lasciando invariati tutti
  gli altri campi. Autocontrolli assicurano che quote alterate o campi
  omonimi di altri documenti continuino a produrre differenze. Il grafo
  nativo ha controlli separati esatti; fixture e attesi indipendenti
  non sono cambiati.
- `ci/20261020-134255-sezioni-wizard-02` si è fermato in build per il
  riferimento esplicito GPC.Design mancante in Reports; riferimento
  aggiunto prima della riesecuzione.
- `ci/20261020-134337-sezioni-wizard-03`: 11 PASS, nessun nuovo fallimento.
  Application 3.045 controlli; Architecture, regressione, software e
  project-calculations superati. WPF 82 controlli: selezione reale delle
  dodici tipologie, anteprima, annullamento/invalidi, applicazione,
  salvataggio, riapertura nel vero editor e riuso dell'identità GPC.
- Per tutte le dodici tipologie: proprietà e barre identiche dopo reload,
  tensioni SLE di barre e fibre identiche per N = −500 kN,
  Mx = 10 kNm, My = 5 kNm. È una prova di persistenza, non un'attestazione
  generale delle verifiche delle nuove geometrie.
- Dodici report geometrici DOCX/PDF, 29 pagine, controllati visivamente:
  nessun esito strutturale inventato nelle fixture. Anteprime di tutte le
  forme controllate; selettore successivamente allargato e verificato
  dalla prova WPF e dalla cattura finale della sezione I.

Le differenze funzionali intenzionali sono registrate in CA-PERSIST-01
e CA-WIZARD-01. I controlli delle mesh usuali dello standard e le impronte
in `sezioni-wizard/librerie-geometria-invariate.json` confermano l'assenza
di modifiche alle librerie geometriche.

## Guide

Guide globali Rev54: pratica 98 pagine, teoria 164 pagine. Word, PDF,
indici e Wiki aggiornati; 387 formule Word e parti non testuali conservate.
Controllo visivo delle 262 pagine mediante 14 tavole, pagine nuove anche
a risoluzione completa e dieci pagine degli indici. Nessun taglio o
sovrapposizione rilevato. Evidenze in
`supporto/artefatti/guide_anthea_itec_rev54/`.

Diciotto originali conservati con SHA-256 in
`supporto/SUPERATI/sezioni-rev54-20261020/`, con struttura relativa e
registro della sostituzione. Nelle cartelle correnti restano le guide Rev54.
