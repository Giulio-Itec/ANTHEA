# Guide Rev72 — compito 205

Base: consegna 191b `3120a64d02dea2002cae64e3669c60c420e1afd3`, ANTHEA 1.19.1 sul set AW. La revisione è documentale: nessuna modifica al calcolo, alle librerie o al comportamento dell'applicazione. Esclusi i lavori successivi sulle paratie, sul copriferro, sul passo 5 e sul metodo Stratificato. L'articolo autonomo Rev01 resta invariato.

## Riscontro dei testi G202

| Tema | Riscontro nel codice della consegna | Intervento editoriale |
|---|---|---|
| A1 · MIDAS e carichi | `MidasLiveNormalization.Apply/Pending`, `ModelSnapshotUpdate.Normalizes/EnsureCompatible`, `MidasGraphicalLoads.Uniform` | Rimossa la descrizione superata della normalizzazione incompleta. «Solo risultati» richiede anche la coincidenza di attributi e tabelle. Pressioni variabili e proiettate restano non rappresentate. |
| A2 · Planarità e classe CLS | Model AW, `ShellPlanarity.ToleranceMm/DeviationMm`, `PropertyMapper.Material`, `NonUniformPlatePressure` | Tolleranza sul piano medio dei vertici, nessuno spostamento delle coordinate. Riconoscimento fc/f'c/fck con unità e catalogo ammessi; classe esplicita prioritaria. Nessuna nuova prescrizione normativa. |
| A3 · Combinazioni | `GpcModelCombinationService.Calculate`, `ModelCombinations.Attach/ResultLength/SerializedSize`, comandi `FreeCombinationResultsAsync` e pubblicazione della copia nel progetto | Ricostruzione dai casi base; 256 MiB riferiti al JSON non compresso del blocco, non al file compresso. Annullamento della liberazione nel progetto. |
| A4 · Documenti e archivio | `WorkspaceSession`, `WpfDesktopServices`, `ModelResultArchive`; Model AW `GPC.Archive/BlockArchive.cs`; `ResultArchiveSharedChecks` | Distinti errore I/O e blocco danneggiato riconosciuto. Messaggi e scelte reali; recupero limitato all'ora mostrata; compatibilità del solo archivio risultati, non di ogni progetto. |
| A5 · Materiali vuoti | `ProjectValidation.CoverMaterials/CoverChecks` | «Non verificabile» e prosecuzione con le schede applicabili. Non si promette l'uso di tutte le schede dei livelli superiori né il completamento automatico della scheda. |
| A6 · Template non valido | `ReportTemplateStore.Select/Current`, `ReportTemplateMenuViewModel` | La nuova selezione fallita conserva il precedente; una copia persistita illeggibile richiede una nuova scelta o il ripristino. |
| A7 · Lunghezze e residui | `EngineeringFormat.Length`, `ReportBridge`; esposizione dei residui del procedimento iterativo | Due decimali senza esponente per le lunghezze del report. Scartata la promessa generale che tutti i valori alla precisione normale siano identici fra versioni: non è un contratto dimostrabile dal solo formatter. Nessuna modifica alle tolleranze. |
| A8 · Hansen | Checker AW `ShallowFoundationBearing.HansenDrainedInclined/HansenUndrainedInclined/HansenComponents` | Con H_L = 0 solo B′; con H_L > 0 anche L′. H_L è il modulo della componente negli assi efficaci. Restano le discontinuità descritte nel codice. Corretto anche l'esempio numerico superato. |
| A9 · Azioni | `ActionFieldPresentation`, `ClimateSheetViewModel`, `ClimateSheetView`, prove `ClimateViewChecks` | Testo numerico non valido conservabile ma non valido per il calcolo; stato di consultazione in memoria durante la sessione, non persistito nel documento; focus e disposizione compatta. |
| A10 · Reologia | `ConcreteTimeEffectsViewModel`, XAML e `ConcreteTimeEffectsView.RefreshPlot` | Cultura della sessione e colonna scorrevole; nessuna promessa di aggiornamento immediato dopo il cambio del separatore. |
| A11 · Pali verticali | `VerticalPileSheetViewModel`: inizializzazione `capacityTitle` e `ScheduleCalculationAsync` | Titolo fin dall'apertura e calcolo automatico subordinato a dati validi. Non riguarda il modulo orizzontale. |
| A12 · Verifiche modello | `ModelViewerControl.xaml`, `ModelViewerViewModel.Verifications.cs` | Collegamento al foglio, configurazione futura. «Esegui verifiche» disabilitato; nessuna procedura GPCModelChecker inventata. |
| A13 · Pressioni verso Straus7 | Model AW `Straus7ModelWritePlan.LoadsPlates.cs`, `PlatePressureNodeForces` | Capacità del convertitore, con vertici coincidenti e somma per nodo/caso. Esplicita assenza del comando di esportazione .st7 in ANTHEA. Nessun nativo eseguito. |

I riferimenti AW sono quelli del manifest distribuito: Model `c2abf2221262fcb38358609d45b4909d82d37d6b`, Checker `9c71ca658ed43d6211d901ead2bef29ee85b1506`, Geometry `6734eee1004142576b96759e34c15dec48776e23`, Utilities `e57b440b16a0a311a7bf0af2f9cf4d2681b83491`. L'inventario con righe e hash dei 32 sorgenti è nella consegna esterna `chatgpt-compiti/evidenze-205/source-evidence.json`.

## Esempio Hansen

`tests/GuideRev72.Checks` esegue le DLL del set AW con input espliciti in N, mm, MPa e confronta sei casi con espressioni indipendenti in kN, m, kPa; tolleranza assoluta 10⁻⁸ kPa. Non legge né riscrive attesi congelati. B = 2 m, L = 3 m, D = 1 m, N = 600 kN, γ = 18 kN/m³, nessuna falda, nessun momento o sovraccarico aggiuntivo, profilo caratteristico, fattori di profondità attivi.

| Condizione | H_B | H_L | q_lim |
|---|---:|---:|---:|
| Drenata, φ′ = 30°, c′ = 0 | 0 | 0 | 704,29 kPa |
| Drenata | 60 kN | 0 | 522,63 kPa |
| Drenata | 1 N | 0 | 704,29 kPa |
| Non drenata, c_u = 75 kPa | 0 | 0 | 532,16 kPa |
| Non drenata | 60 kN | 0 | 469,20 kPa |
| Non drenata | 1 N | 0 | 480,74 kPa |

Il limite non drenato per H_B → 0⁺ con H_L = 0 è 480,743338823 kPa. Il precedente 455,04 kPa e il riferimento alla direzione L′ non descrivono più questo caso. Nessun risultato di altri esempi viene aggiornato per analogia. La formula dell'esempio è espressa su più righe per mantenerla leggibile nel Word e nel PDF.

## Pipeline e approvazioni

Si conserva la pipeline ITEC della Rev71: Build → Word aggiornamento campi → Finalize con audit → Word VerifyFinal. La Rev71, i sorgenti e gli indici sono conservati in `supporto/SUPERATI/guide-rev72-20261115/manifest.json`. Le figure nuove usano soltanto controlli di produzione renderizzati fuori schermo dal percorso di prova `--check-guide-rev72-figures`; gli attesi grafici esistenti non vengono aggiornati.

La Wiki continua a derivare dalle due guide globali, con 94 articoli. L'indice e le posizioni nei sorgenti sono dati di produzione derivati, da aggiornare insieme ai testi. Gli attesi F3 e il registro delle transizioni approvate restano protetti: le nuove differenze editoriali vengono elencate separatamente per l'OK dell'utente. La consegna `chatgpt-compiti/guide-rev72-205.md` riporta esiti effettivi, conteggi, commit, figure mancanti e lista esatta per l'approvazione.

La selezione delle prove conserva tutte le 67 suite della Rev71 e aggiunge `GuideRev72.Checks`: 40 fast, 16 regression, 2 Wiki e 10 UI. Le 73 suite dell'inventario UI generale restano presenti. Il compito 205 richiede espressamente queste verifiche documentali e le figure offscreen; il profilo full non viene eseguito. Nessuna prova installer viene aggiunta: il FAIL installer/guides del 191b resta di competenza del coordinatore.

Le corse usano l'handle esclusivo di `INT/pesante.lock` nello stesso processo PowerShell per tutta l'esecuzione, priorità BelowNormal, controllo di processi, Rhino, RAM e spazio. Una copia locale del runner CI aggiunge soltanto `-m:1 -p:UseSharedCompilation=false` alle due invocazioni di build; non cambia le asserzioni e non viene consegnata come sorgente del progetto. Gli output restano nel clone, con controllo del limite di 5 GB, e TEMP/TMP sotto CX. I comandi non abilitano nativi Straus7 o MIDAS.

## Esiti conclusivi — 16 novembre 2026

| Documento | Pagine Word/PDF | Corpo | Frammenti | Formule modificabili |
|---|---:|---:|---:|---:|
| Pratica Rev72 | 140 | 133 | 1.406 | 9 |
| Teorica Rev72 | 214 | 202 | 2.555 | 1.579 |

Audit della pipeline PASS: nessun contenuto mancante, cinque sezioni per guida e 26 parti del modello identiche byte per byte. Copie PDF accanto ai Markdown identiche a quelle ufficiali; aggiornati anche cinque PDF degli indici/README. Tutte le 354 pagine sono state controllate nelle tavole di insieme; le pagine modificate sono state esaminate singolarmente. Dopo la correzione editoriale la rigenerazione è stata confrontata pagina per pagina con gli hash delle immagini già verificate, riesaminando le pagine cambiate. Elenchi e metodo nel registro esterno `evidenze-205/visual-review.json`.

F10–F13 PASS: quattro controlli di produzione renderizzati offscreen, senza cattura dello schermo. Le figure finali sono presenti nei sorgenti e nelle guide. Il dato UI Hansen 469,20485129576 kPa concorda con il calcolo indipendente a 10⁻⁸ kPa. Le figure G202 non acquisite sono motivate nella consegna, senza presentarle come impossibili offscreen.

Prima CI `20261116-002050-guide-rev72-205`, 00:20–00:48: 34 build PASS, 66 suite PASS e due NEW-FAIL. `wiki/manuale` rilevava la duplicazione dei metadati e del paragrafo di planarità, oltre al nome Straus7 fuori dal capitolo d’importazione ammesso. Correzione dei soli testi della teoria: metadati specifici, criterio geometrico con formulazione distinta dalla procedura pratica e titolo riferito al formato .st7. Nessun test indebolito. Documenti rigenerati e audit ripetuti.

Corsa mirata finale `20261116-010613-guide-rev72-205-final`: 3 build PASS, cinque suite PASS e il solo `ui/f3-equivalence` NEW-FAIL. Ripetuti Reports.Tests, Wiki.Tests, wiki/indice, wiki/manuale, F3 e Wiki offscreen. Il riferimento ITEC opzionale è stato collocato nel percorso già previsto da Reports.Tests: nella prima corsa era assente e il corpus reale non era incluso. La ripetizione ha dato 733 controlli Reports PASS, inclusi 238 sugli stili e 217 report applicati al modello ITEC, oltre ai 217 PDF ordinari. L’esito delle 68 suite selezionate, integrando la ripetizione mirata, è 67 PASS e un FAIL editoriale F3 da approvare; non si dichiara una singola corsa integrale verde. In entrambe le corse: zero file segnalati come modificati e zero avvisi del runner. I warning dei compilatori sono conservati nei log.

F3 finale: 1.201 file; 987 differenze non ammesse, tutte nella Wiki, nessuna fuori da essa. Proposta esatta esterna: 561 nuove voci e 426 precedenti superate; 291 spostamenti e 80 sostituzioni univoche. Tutte le nuove classi sono già ammesse da `Transition.WithEditorialUpdate`. Il registro candidato e il delta rimangono in `chatgpt-compiti/evidenze-205`, chiaramente NON APPROVATI; nessun aggiornamento di attesi o catture congelate e nessuna aggiunta ai known failures. Dopo l’OK occorre applicare solo le voci approvate e rieseguire F3.

Restano fuori dalla selezione le tre prove infrastrutturali ElasticPile.Checks, BridgeValidationCurrent ed ElasticPile.Performance, oltre alla prova opzionale baseline/banco-ca senza CheckerFixtures. Nessuna di queste era fra le 67 suite Rev71 conservate. Straus7Native.Checks ha percorso il ramo senza campioni: zero controlli nativi; nessun nativo Straus7/MIDAS eseguito.

Integrità finale: 1.887 file protetti invariati; 16 originali Rev71 con hash verificati; articolo Rev01 invariato. Per mantenere il clone sotto 5 GB, gli artefatti completi della prima CI sono conservati in `CX/retained/205/ci/20261116-002050-guide-rev72-205`; riepiloghi e rinvio restano nella posizione originale. Le prime bozze Rev72 sono conservate in `CX/retained/205/pre-lint`. Nessuna evidenza eliminata. La copia CI specifica del compito viene conservata fuori dal repository dopo l’esecuzione; differisce dal runner solo per i due argomenti di build prescritti. Nessun push, tag, pubblicazione o installer.

## Compito 207 — chiarimenti sul copriferro, 16 novembre 2026

Recepite R1/R2 della rilettura G209 sulla base documentale `99b715ae`, senza modificare il comportamento 1.19.1. Il controllo sceglie tutte le schede Materiali del primo livello che ne contiene, dal progetto verso il foglio; l’esito positivo non copre automaticamente requisiti locali più severi. Una scheda senza dati non abilita la ricerca in basso: il ciclo prosegue solo sul gruppo scelto. Nessuna anticipazione dell’avviso P10 e nessuna modifica numerica.

- Pratica: 140 → 140 pagine; 1407 frammenti e 9 formule modificabili verificati. Pagine fisiche con immagine diversa: 2–3, 18–27; testo R1/R2 alla pagina 18.
- Teorica: 214 → 214 pagine; 2556 frammenti e 1579 formule modificabili verificati. Pagine fisiche con immagine diversa: 2–3, 27–43; testo R1/R2 alla pagina 27.
- Corsa mirata: 2 build e 4 suite PASS; unico NEW-FAIL `ui/f3-equivalence`, interamente Wiki: 993 differenze non ammesse, 567 nuove voci e 426 precedenti superate. Nessuna differenza non ammessa fuori dalla Wiki.

Rigenerazione con la pipeline ITEC invariata, aggiornamento campi Word, audit, VerifyFinal e rendering Poppler. Cinque sezioni e 26 parti del modello preservate per guida. Controllate visivamente tutte le pagine con immagini diverse; le altre coincidono con quelle verificate nel 205. Tutti i 1.887 file protetti, i 16 originali Rev71, le figure e l’articolo Rev01 sono invariati.

Verificati Wiki.Tests, indice, manuale e Wiki offscreen; F3 è eseguito solo per misurare il delta. La proposta aggiornata e il confronto con quella del 205 sono in `chatgpt-compiti/evidenze-207`, non applicati al registro congelato. `Get-InstallerGuides` valida Rev72 e i quattro PDF: la suite completa `installer/guides` non è eseguita perché crea, installa e disinstalla un eseguibile di prova, incompatibile con i vincoli «nessun installer, nessuna copia» del 207. Non viene dichiarata PASS.

Consegna, SHA e testo futuro P10 nella sezione Compito 207 di `chatgpt-compiti/guide-rev72-205.md`. Un solo commit documentale; nessun push, tag o rilascio.

Anomalia dell’indice Git: alle 02:18:56 è cambiato il file `.git/index`, nella fascia vietata. Il timestamp coincide con il controllo `git diff --stat` / `--check` lanciato con `GIT_OPTIONAL_LOCKS=0`; un refresh automatico è la causa probabile, senza tracciamento indipendente del processo scrivente. Le 5.980 voci dell’indice hanno tutti gli stessi identificatori oggetto e modi di HEAD: nessun contenuto in stage, HEAD, riferimenti, log e configurazione invariati. Il controllo preliminare ha fermato il commit prima dello stage; verificata la natura della variazione, il commit è stato eseguito dopo le 02:45. L’aggiornamento dell’indice resta registrato come eccezione al vincolo, non come sua piena osservanza. Evidenza: `chatgpt-compiti/evidenze-207/git-index-refresh-exception.json`.
