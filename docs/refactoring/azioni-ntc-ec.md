# Azioni NTC / EN — lavoro del 29–30 ottobre 2026

Branch ANTHEA `refactoring/f5-azioni-climatiche`, da `refactoring/f5` 61dc4ff3. Model e Checker: `codex/azioni-climatiche-ntc-ec`. Il 30/10 l'utente ha chiesto di non eseguire merge fino a sera; nessun merge o push eseguito. Il tentativo precedente di pubblicare le librerie, motivato dalla regola presente nei loro AGENTS, è stato rifiutato dalla revisione automatica prima dell'esecuzione. Si usano soltanto snapshot candidati esterni a `lib/Checker`.

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
- [ ] Neve EN: allineamento ad A1:2015 aperto. Il testo integrale non è disponibile nel workspace; il disco Y: non è accessibile. Richiesto all'utente un percorso alla norma integrale o consolidata. Il modulo dichiara la sola EN 1991-1-3:2003+AC:2009 implementata.

### Collaudo conclusivo e copia avviabile

Il full completo è conservato in supporto/artefatti/ci/20261030-075003-actions-full-final; i sorgenti erano puliti e sono rimasti congelati. L'unico fallimento era il fondo bianco della scheda Impalcato e influenza nei temi scuri: venti occorrenze della stessa causa in dieci viste della ripartizione. Il solo cambiamento di codice successivo è l'ereditarietà dello stile TabItem globale. La corsa supporto/artefatti/ci/20261030-091138-actions-contrast-final, su fa09abc8 pulito, ripete build, architettura, WPF Azioni, temi, layout e scansione completa del contrasto: nove PASS. Gli esiti originali restano conservati; non si presenta la seconda corsa filtrata come un nuovo full integrale.

Il riepilogo per suite, con provenienza, è supporto/artefatti/azioni-climatiche/consolidated-verification.json. Restano non eseguite dal runner le tre voci infrastrutturali storiche: ElasticPile.Checks e BridgeValidationCurrent, coperte dalle suite GPC, ed ElasticPile.Performance, misura facoltativa con input negli artefatti. Le suite GPC del set candidato 04 hanno zero fallimenti.

La copia locale è supporto/artefatti/azioni-climatiche/app/ANTHEA.exe, versione 1.16.0+fa09abc8, con runtime .NET Desktop 8. La provenienza sta in app/provenance.json e le impronte in app-sha256.json. Esempi e Guide sono nelle cartelle risolte dall'applicazione. Il verbale visivo dei PDF è pdf-qa/visual-review.json: tutte le miniature esaminate, nuovi capitoli pratica 112–123 e teoria 190–200 letti a piena scala; venti relazioni e quattro indici controllati. L'aggiornamento A1:2015 resta distinto dal collaudo delle funzionalità implementate.

Il 30/10, alla ripresa, quattro sorgenti non committati risultavano costituiti soltanto da byte nulli: ClimateDocument, ClimateSheetViewModel, ClimateSheetView.xaml e ClimateCaseChart. Ripristinati dai patch originali della stessa chat; originali azzerati e registro del recupero in `supporto/artefatti/azioni-climatiche/recupero`. Calcoli GPC già committati integri; prove mirate ripetute dopo il recupero. Nessuna fixture indipendente modificata.

## Architettura e riproduzione

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

## Campo dei moduli aggiuntivi

- Carichi: strati G1/G2; partizioni permanenti NTC e mobili EN distinte; categorie NTC e valori EN assegnati; riduzioni, concentrati locali e orizzontali separati.
- Combinazioni: edifici, NTC Tab. 2.6.I ed EN Allegato A1; SLU/SLE/eccezionali, dominanti, correlazioni ed esclusioni. EN 6.10 oppure entrambe 6.10a/b; effetti scalari con unità comune. Non genera le combinazioni specifiche dei ponti dell'Allegato A2.
- Traffico: corsie convenzionali, LM1/LM2, frenatura, centrifuga, folla e marciapiedi. La ricerca delle posizioni più gravose resta del progetto; gruppi simultanei documentati.
- Ripartizione: Courbon pesato con EI oppure travi flesso-torsionali e soletta trasversale con sviluppo di Fourier. Travi rettilinee prismatiche in semplice appoggio, torsione impedita agli estremi. Percentuali del momento nella sezione scelta, non della forza verticale totale. Residuo, equilibrio e convergenza su momenti e influenze; torsioni accessorie senza la medesima attestazione di convergenza. Esclusi impalcati obliqui, curvi, continui e traversi discreti. Oracolo indipendente in Checker.
- Urti: forze statiche equivalenti eccezionali per piedritti, impalcati e carrelli; non analisi dinamica né verifica di barriere.

Fonti aggiuntive: NTC 2018 capitoli 2/3/5, EN 1990:2002+A1:2005, EN 1991-1-1:2002, EN 1991-1-7:2006, EN 1991-2:2003. Copie e impronte negli artefatti, non distribuite. Nessuna selezione automatica delle appendici nazionali né implementazione delle edizioni EN di seconda generazione.
