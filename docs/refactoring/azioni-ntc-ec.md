# Azioni NTC / EN — lavoro del 29–30 ottobre 2026

Branch ANTHEA `refactoring/f5-azioni-climatiche`, da `refactoring/f5` 61dc4ff3. Model e Checker: `codex/azioni-climatiche-ntc-ec`. Il 30/10 l'utente ha chiesto di non eseguire merge fino a sera; nessun merge o push eseguito. Il tentativo precedente di pubblicare le librerie, motivato dalla regola presente nei loro AGENTS, è stato rifiutato dalla revisione automatica prima dell'esecuzione. Si usano soltanto snapshot candidati esterni a `lib/Checker`.

## Perimetro autorizzato

- Cinque fogli climatici: neve, vento e temperatura negli edifici; vento CNR e temperatura negli impalcati.
- Immagini, schemi e grafici necessari, anche nelle relazioni e nelle due guide globali.
- Combinazioni SLU/SLE, permanenti e sovraccarichi, traffico sui ponti e azioni eccezionali.
- Ripartizione trasversale del traffico fra le travi: entrambi i metodi confermati dall'utente, traverso rigido e modello deformabile con rigidezze longitudinali e trasversali. Corsie e tandem posizionabili, diagrammi, percentuali, valori per trave ed equilibrio.
- Azioni sismiche esplicitamente escluse per ora. Gli Spettri esistenti non cambiano.

## Stato e verifiche

- [x] Contratti climatici GPC tipizzati e vocabolario esplicito degli enum negli archivi Model.
- [x] Calcoli GPC, riferimenti per ogni risultato e casi di carico distinti; limiti delle edizioni EN espliciti.
- [x] Cinque moduli MVVM, persistenza JSON delle classi GPC, formule e report DOCX/PDF/CSV.
- [x] Otto schemi originali SVG/PNG, condivisi da UI, report e guide; diagrammi con unità omogenee per caso.
- [x] Dieci esempi nel catalogo, due per modulo.
- [x] Prove mirate ANTHEA: 199 controlli Presentation e 76 WPF, cinque moduli e tre temi (`20261030-055610-climate-graphics`).
- [x] Set candidato GPC completo `gpc-candidate-03`: Model f351653e, Checker f75b5d35; tutte le suite di Model e Checker verdi, comprese net472. Fixture climatica indipendente Python con 80 punti; 33 prove climatiche iniziali, incluse in Design.Checks.
- [ ] Collaudo full ANTHEA sul candidato, avviato in `20261030-060827-climate-full`.
- [ ] Guide Rev63 Word/PDF finalizzate e ispezionate; originali archiviati con hash in `supporto/SUPERATI/azioni-climatiche-rev63-20261030`.
- [ ] Moduli aggiuntivi autorizzati il 30/10, relativa UI, esempi, formule e collaudi indipendenti.
- [ ] Build locale finale e verbale consolidato.

Il 30/10, alla ripresa, quattro sorgenti non committati risultavano costituiti soltanto da byte nulli: ClimateDocument, ClimateSheetViewModel, ClimateSheetView.xaml e ClimateCaseChart. Ripristinati dai patch originali della stessa chat; originali azzerati e registro del recupero in `supporto/artefatti/azioni-climatiche/recupero`. Calcoli GPC già committati integri; prove mirate ripetute dopo il recupero. Nessuna fixture indipendente modificata.

## Architettura e riproduzione

Calcoli esclusivamente in Model/Checker/GPC.Design. Application adatta contratti e risultati; Presentation non contiene tipi di calcolo o WPF; Desktop contiene viste e disegni; Reports compone i documenti senza calcoli ingegneristici. Le immagini sono risorse locali, rigenerabili con `tools/figures/climate_schematics.py` (Matplotlib di solo sviluppo).

Lo snapshot candidato è `supporto/artefatti/azioni-climatiche/candidate-libs`. Per compilare sul branch usare `-p:GpcLibDir=<percorso assoluto dello snapshot>`; per il runner `build/ci.ps1 -Profile full -GpcLibDir <percorso> -Keep`. Le DLL ufficiali non sono sostituite. Le suite UI passano da 72 a 73 con `ui/check-climate-view`; nessuna rimossa.

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
