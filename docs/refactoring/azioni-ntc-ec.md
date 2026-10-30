# Azioni NTC / EN — lavoro del 29–30 ottobre 2026

Branch ANTHEA `refactoring/f5-azioni-climatiche`, da `refactoring/f5` 61dc4ff3. Model e Checker: `codex/azioni-climatiche-ntc-ec`. Il divieto temporaneo di merge del 30/10 è stato successivamente revocato dall'utente: «poi merge e versione delle varie librerie. porta le tue modifiche sul master anche di anthea». Il ramo principale di ANTHEA si chiama `main`. L'allineamento neve EN A1:2015 resta **da completare**, come confermato dall'utente. Il precedente tentativo di pubblicare le librerie era stato rifiutato dalla revisione automatica prima dell'esecuzione; fino all'autorizzazione si sono usati soltanto snapshot candidati esterni a `lib/Checker`.

## Perimetro autorizzato

- Cinque fogli climatici: neve, vento e temperatura negli edifici; vento CNR e temperatura negli impalcati.
- Immagini, schemi e grafici necessari, anche nelle relazioni e nelle due guide globali.
- Combinazioni SLU/SLE, permanenti e sovraccarichi, traffico sui ponti e azioni eccezionali.
- Ripartizione trasversale del traffico fra le travi: entrambi i metodi confermati dall'utente, traverso rigido e modello deformabile con rigidezze longitudinali e trasversali. Corsie e tandem posizionabili, diagrammi, percentuali, valori per trave ed equilibrio.
- Azioni sismiche esplicitamente escluse per ora. Gli Spettri esistenti non cambiano.

## Stato e verifiche

- [x] Dieci moduli MVVM e calcoli GPC; persistenza JSON delle classi GPC, tabelle, DOCX/PDF/CSV e sola lettura.
- [x] Tredici schemi originali SVG/PNG; pianta, sezione e linee d'influenza per la ripartizione.
- [x] Venti esempi, due per modulo, ANTHEA/CSV/DOCX/PDF nel catalogo centrale; 72 esempi per 25 moduli complessivi.
- [x] Set candidato 04: Model b961d663, Checker 5e0bca33; tutte le suite GPC, comprese net472. Nuove prove generali/ripartizione: 37 net8 + 37 net472. Oracoli Python indipendenti climatici (80 punti) e della ripartizione conservati.
- [x] ANTHEA mirate: 442 controlli dei venti scenari; architettura 583; catalogo 359. WPF dieci moduli/tre temi 168, compreso il disegno deformabile; catalogo, host e Fondazioni superati.
- [x] Primo full 20261030-060827-climate-full: 151 PASS e 8 rilievi durante lo sviluppo. Diagnosi: conteggi storici, ordine dei moduli, nuove occorrenze architetturali, Wiki e versione DLL Fondazioni. Corretti senza deroghe nuove.
- [x] Guide Rev63: 123 e 200 pagine, 26 parti ITEC identiche, 1.502 formule conservate. Capitoli nuovi ispezionati, originali Rev62 archiviati con SHA-256.
- [x] F3 completo: 1.201 file in 122 gruppi, 80.836 transizioni ammesse, zero residue. Quattro articoli nuovi (82 totali): 102 transizioni editoriali aggiunte, 58 superate sostituite, 2.824 conservate. Nessun atteso numerico modificato.
- [x] Full conclusivo su 4e2f29cc: 161 PASS e un difetto grafico; correzione isolata fa09abc8 e nove suite interessate PASS. Consolidamento: 162 PASS, 73 suite UI, 1.520 catture di contrasto e zero difetti. Nessuna baseline modificata.
- [x] Build locale fa09abc8 pronta per revisione: 256 file con SHA-256, 13 DLL GPC coerenti, 72 esempi e quattro PDF. Controllo assenza codice di prova PASS. Nessun installer o rilascio ufficiale nuovo.
- [ ] Neve EN: allineamento ad A1:2015 aperto per indisponibilità del testo integrale. Il 30/10 l'utente ha fornito la UNI EN 1991-1-3:2004 e confermato di non possedere l'A1:2015; acquisizione e lettura riuscite, ma il PDF non include l'emendamento. La ricerca presso fonti ufficiali ha reperito soltanto un'anteprima incompleta. Il modulo dichiara la sola EN 1991-1-3:2003+AC:2009 implementata; il limite non blocca la revisione degli altri moduli.

### Collaudo conclusivo e copia avviabile

Il full completo è conservato in supporto/artefatti/ci/20261030-075003-actions-full-final; i sorgenti erano puliti e sono rimasti congelati. L'unico fallimento era il fondo bianco della scheda Impalcato e influenza nei temi scuri: venti occorrenze della stessa causa in dieci viste della ripartizione. Il solo cambiamento di codice successivo è l'ereditarietà dello stile TabItem globale. La corsa supporto/artefatti/ci/20261030-091138-actions-contrast-final, su fa09abc8 pulito, ripete build, architettura, WPF Azioni, temi, layout e scansione completa del contrasto: nove PASS. Gli esiti originali restano conservati; non si presenta la seconda corsa filtrata come un nuovo full integrale.

Il riepilogo per suite, con provenienza, è supporto/artefatti/azioni-climatiche/consolidated-verification.json. Restano non eseguite dal runner le tre voci infrastrutturali storiche: ElasticPile.Checks e BridgeValidationCurrent, coperte dalle suite GPC, ed ElasticPile.Performance, misura facoltativa con input negli artefatti. Le suite GPC del set candidato 04 hanno zero fallimenti.

La copia locale è supporto/artefatti/azioni-climatiche/app/ANTHEA.exe, versione 1.16.0+fa09abc8, con runtime .NET Desktop 8. La provenienza sta in app/provenance.json e le impronte in app-sha256.json. Esempi e Guide sono nelle cartelle risolte dall'applicazione. Il verbale visivo dei PDF è pdf-qa/visual-review.json: tutte le miniature esaminate, nuovi capitoli pratica 112–123 e teoria 190–200 letti a piena scala; venti relazioni e quattro indici controllati. L'aggiornamento A1:2015 resta distinto dal collaudo delle funzionalità implementate.

Il 30/10, alla ripresa, quattro sorgenti non committati risultavano costituiti soltanto da byte nulli: ClimateDocument, ClimateSheetViewModel, ClimateSheetView.xaml e ClimateCaseChart. Ripristinati dai patch originali della stessa chat; originali azzerati e registro del recupero in `supporto/artefatti/azioni-climatiche/recupero`. Calcoli GPC già committati integri; prove mirate ripetute dopo il recupero. Nessuna fixture indipendente modificata.

## Architettura e riproduzione

### Integrazione autorizzata del 30 ottobre

- Integrati sui rami di lavoro `origin/master` Model 8fb97ef8 e Checker f253ee4e, e `origin/main` ANTHEA 25f05436 (1.16.1). Conservati i cambiamenti già pubblicati su combinazioni, steel, Concrete e CompositeBridge.
- Versioni assegnate: Model 5.32.0.0 e Design 0.44.0.0 per le nuove funzionalità; ModelData 0.3.0.17, Persistence 3.4.0.7, Converter 2.10.0.4, Concrete 0.32.0.3, Steel 2.127.0.3, CompositeBridge 1.21.0.2, Geotechnics 0.27.0.5, ModelChecker 2.35.0.3 e Masonry 0.10.0.20 per ricompilazione coerente. Utilities, Geometry e DelaunayMesh invariati.
- Merge ANTHEA: conservate tutte le voci del registro; confronto Fondazioni sulle impronte AN già presenti su `main`, normalizzando soltanto `library_version` a 0.27.0.1 dopo la verifica della DLL caricata. Nessun atteso numerico rigenerato. Indice installer unito e PDF ricontrollato; versioni precedenti archiviate con SHA-256.
- [x] Suite Model sul commit a6f664c9: 62 Core e 1.601 UnitTest superati; dodici prove non eseguite (campagna dei tempi e casi con archivi nativi esterni). Suite Checker complete sul commit 0d2dec71, inclusa la successiva correzione CHS pubblicata da un’altra sessione (94b92533): 954 Concrete, 122 Steel, 411 CompositeBridge, 202 Geotechnics, 175 Calculation, 29 ShallowFoundation, 99 EmbeddedWalls, 6.990 Steel.Members, 192 Design net8, 191 Design net472, 655 ModelChecker; zero fallimenti, API compatibili. Merge locali conclusi sui rispettivi master.
- [x] Push esplicitamente autorizzati dall’utente dopo il blocco automatico: Model a6f664c9 e Checker 0d2dec71 pubblicati sui rispettivi `origin/master`. Evidenze pre-push in `supporto/artefatti/azioni-climatiche/merge-20261030/prepush-verification.json`.
- [ ] Produzione del set ufficiale con `Release-Set.ps1 -Previous` e installazione dello snapshot. La corsa full candidata `20261030-180108-azioni-merge-candidate-full` è stata interrotta intenzionalmente dopo 32 compilazioni e 45 suite PASS per passare al full sul set ufficiale: è una corsa parziale, non un full concluso. Log e motivazione conservati in `interruption.json`.
- [ ] Snapshot ufficiale in ANTHEA, profilo full, integrazione su `main`. Suite UI prima dell'integrazione: 73; nessuna rimossa.

### Riproduzione della build candidata precedente

Calcoli esclusivamente in Model/Checker/GPC.Design. Application adatta contratti e risultati; Presentation non contiene tipi di calcolo o WPF; Desktop contiene viste e disegni; Reports compone i documenti senza calcoli ingegneristici. Le immagini sono risorse locali, rigenerabili con `tools/figures/climate_schematics.py` (Matplotlib di solo sviluppo).

Lo snapshot candidato è `supporto/artefatti/azioni-climatiche/candidate-actions-libs`. Per compilare sul branch usare `-p:GpcLibDir=<percorso assoluto dello snapshot>`; per il runner `build/ci.ps1 -Profile full -GpcLibDir <percorso> -Keep`. Le DLL ufficiali non sono sostituite. Le suite UI passano da 72 a 73 con `ui/check-climate-view`; nessuna rimossa.

Si tratta di funzionalità nuove: nessun motore legacy spostato o eliminato. Le modifiche agli indici Wiki sono editoriali; gli attesi indipendenti numerici non si rigenerano.

## Fonti tecniche acquisite

Testi consultati e figure normative verificate prima della trascrizione delle tabelle. I PDF delle norme restano negli artefatti e non sono redistribuiti. Nelle guide sono presenti soltanto elaborazioni originali e citazioni normative.

- NTC 2018, capitoli 3 e 5: Gazzetta Ufficiale, DM 17 gennaio 2018, 18A00716, Supplemento ordinario n. 8 del 20 febbraio 2018.
- Circolare 21 gennaio 2019 n. 7, capitoli 3 e 5: Gazzetta Ufficiale, 19A00855, 11 febbraio 2019.
- CNR-DT 207 R1/2018, fonte CNR: https://www.cnr.it/it/node/9550.
- EN 1991-1-3:2003 + AC:2009: testo normativo consultato; **A1:2015 non implementato**.
- EN 1991-1-4:2005 + A1:2010 + AC:2010; EN 1991-1-5:2003 + AC:2009: testi normativi consultati.
- Le copie EN provengono dall'archivio https://www.phd.eng.br/wp-content/uploads/2015/12/; fanno fede le edizioni stampate sulle copie, non la data del sito.

Le edizioni di seconda generazione e la selezione automatica delle appendici nazionali non sono comprese. Nei rami EN il dato climatico e la sua fonte sono obbligatori.

L'[anteprima ufficiale SIST dell'A1:2015](https://preview.sist.si/sist-preview/40457/e5bb9d96e0c140539f6065fd0f2b2989/SIST-EN-1991-1-3-2004-A1-2015.pdf), consultata il 30/10, arriva solo all'inizio delle modifiche alle coperture a due falde. Documenta modifiche anche ai coefficienti di forma, ma non consente il riscontro completo degli altri casi. Non si estende la dichiarazione di conformità sulla base di un'anteprima parziale.

Il PDF fornito dall'utente, `X:/ITEC-SGI-CONSULTAZIONE/UNI/UNI EN 1991 - Azioni sulle strutture/EN1991-1-3-2004 - Carichi da neve.pdf`, è stato letto il 30/10: 92 pagine, tutte con testo estraibile; frontespizi verificati anche visivamente. Si tratta della UNI EN 1991-1-3:2004, edizione bilingue febbraio 2005, che recepisce EN 1991-1-3:2003, senza A1:2015. Copia ed evidenze restano negli artefatti non distribuiti `supporto/artefatti/azioni-climatiche/fonti/uni-neve-utente`; SHA-256 `b3a7b1b241f0e11d8b9364249ba086ebf96282343ef7ac414d30e190fb6f89e0`. L'originale non è stato modificato. La conferma dell'utente di non possedere l'emendamento chiude la richiesta di reperimento a lui rivolta, non l'allineamento normativo ancora da eseguire.

## Campo dei moduli aggiuntivi

- Carichi: strati G1/G2; partizioni permanenti NTC e mobili EN distinte; categorie NTC e valori EN assegnati; riduzioni, concentrati locali e orizzontali separati.
- Combinazioni: edifici, NTC Tab. 2.6.I ed EN Allegato A1; SLU/SLE/eccezionali, dominanti, correlazioni ed esclusioni. EN 6.10 oppure entrambe 6.10a/b; effetti scalari con unità comune. Non genera le combinazioni specifiche dei ponti dell'Allegato A2.
- Traffico: corsie convenzionali, LM1/LM2, frenatura, centrifuga, folla e marciapiedi. La ricerca delle posizioni più gravose resta del progetto; gruppi simultanei documentati.
- Ripartizione: Courbon pesato con EI oppure travi flesso-torsionali e soletta trasversale con sviluppo di Fourier. Travi rettilinee prismatiche in semplice appoggio, torsione impedita agli estremi. Percentuali del momento nella sezione scelta, non della forza verticale totale. Residuo, equilibrio e convergenza su momenti e influenze; torsioni accessorie senza la medesima attestazione di convergenza. Esclusi impalcati obliqui, curvi, continui e traversi discreti. Oracolo indipendente in Checker.
- Urti: forze statiche equivalenti eccezionali per piedritti, impalcati e carrelli; non analisi dinamica né verifica di barriere.

Fonti aggiuntive: NTC 2018 capitoli 2/3/5, EN 1990:2002+A1:2005, EN 1991-1-1:2002, EN 1991-1-7:2006, EN 1991-2:2003. Copie e impronte negli artefatti, non distribuite. Nessuna selezione automatica delle appendici nazionali né implementazione delle edizioni EN di seconda generazione.
