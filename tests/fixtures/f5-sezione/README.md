# Tracce e disegni della vista storica della sezione c.a. (F5.10)

Proposta F5, sezione 7.2: prima di migrare un modulo si cattura il comportamento della vista storica. Il riferimento F3
confronta i dati in forma canonica (chiavi ordinate) e non vede la scheda. Queste tracce conservano invece, dopo l'apertura e
dopo ogni passo di uno scenario, quello che l'utente vede nella scheda della sezione c.a. (`str_palo`, `ConcreteWorkspace` in
`SheetEditor`), aperta come la apre la finestra, in una finestra fuori schermo.

| Cosa | Dove |
| --- | --- |
| Scenari e formato | `tests/Shared/ConcreteSectionTrace.cs` |
| Vista storica | `tests/Desktop/ConcreteSectionHistoricTrace.cs` (comando, finestre fuori schermo, disegni) e `tests/Desktop/ConcreteSectionHistoricState.cs` (passi sui controlli e stato) |
| Comando | `ANTHEA.exe --check-sezione-historic <radice> <cartella>` (confronto) e `--check-sezione-historic-capture` (cattura), solo build UiTests |
| Suite | `ui/check-sezione-historic` |
| ViewModel | dai sottopassi di F5.10, in `tests/ANTHEA.Presentation.Tests` (senza WPF) |
| Vista XAML | `ui/check-sezione-view` (`tests/Desktop/ConcreteSectionViewChecks*.cs`) |
| Valori registrati | `vista-storica.json`: i valori che `ui/check-sezione-view` confrontava con la vista storica aperta accanto, registrati dalla stessa prova (sotto) |

## Che cosa contiene una traccia

Ogni riga di stato inizia con la parte a cui appartiene. La parte comune è sempre scritta:
- i dati: impronta dei byte nell'ordine in memoria e, rispetto allo stato precedente, i valori cambiati, aggiunti o tolti
  (alla prima riga tutti i dati);
- `Modified` contati dall'apertura, la sola lettura, calcolo in corso, calcolo in coda, risultati, barra di avanzamento, scheda
  principale selezionata;
- il risultato esportato (impronta), lo stato della scheda;
- il messaggio del passo: quello che la vista mostra (incolla rifiutato, report non disponibile) e, per le finestre modali, lo
  stato della finestra dopo ogni passo fatto dentro di essa.

Le parti dei sottopassi, scritte dagli scenari che le chiedono (gli scenari `percorso-f3` le scrivono tutte):

| Parte | Contenuto |
| --- | --- |
| `[ingressi]` | nota della normativa, valori integrati, form (normativa, coefficienti, materiali, geometria, foro, armatura, secondi strati, staffe, cavo, trefolo, resistenze rapide) campo per campo (etichetta, valore grezzo, voci, attivo, attenuato, visibile, sola lettura, nota) con i gruppi; foro, armatura bloccata dalle coordinate, strati aggiuntivi; materiali dei cavi, trefolo standard, materiali salvati; testo e riquadri delle resistenze rapide; sezione disegnata (proprietà); tabelle dei trefoli e delle barre; riquadri del riepilogo |
| `[azioni]` | sollecitazioni uniche o storiche, percorso del file Excel, righe di ogni stato limite e del taglio con tutti i valori (ingressi e uscite) |
| `[domini]` | per 3D e 2D: form, tabella (colonne visibili, righe mostrate in ordine, selezione, filtro), dettaglio, riepilogo, comandi e caselle della barra, cursori; il dominio 3D come dati semplici (mesh, punti, selezione, opzioni), il grafico 2D (serie, segmenti, punti) |
| `[sle]` | famiglia selezionata; per Rara, Frequente e Quasi permanente: form, tabella, schede, dettaglio, passaggi della fessurazione, tabelle delle barre e del calcestruzzo, zone efficaci, mappa (proprietà e legenda), riepilogo |
| `[taglio]` | form del taglio, della torsione e delle staffe in sola lettura, nota dei parametri automatici, tabella e comandi, dettaglio, sintesi, riquadri, sezione disegnata |
| `[dettagli]` | form dei dettagli, del copriferro e degli ancoraggi, riquadri del riepilogo, dei temi e degli ancoraggi, nota della durabilità |
| `[armature]` | form della ricerca, dei limiti e delle prime alternative, pulsanti, stato e scarti (tempi misurati sostituiti da «‹t› s»), proposta, ambito, riquadri, alternative, anteprima |
| `[ispezione]` | sezione σ/ε di 3D e 2D (scheda, stato, messaggio, valori, mappa, piano delle deformazioni), form, testo e grafico del momento–curvatura |
| `[wizard]` | solo negli scenari del sottopasso S1b (sezioni da wizard): comando «Wizard tipologie / modifica disposizione…» (spento in sola lettura), form della geometria e dell'armatura nascosti o spenti con le due note del wizard, campi del foro nel loro insieme, nota dei parametri del taglio (`taglio.nota_wizard`) |

Una parte uguale a quella scritta dopo il passo precedente è scritta come `[parte] invariata`.

Gli identificativi creati dalla scheda (righe delle azioni, materiali) diventano `<nuovo-n>` nell'ordine in cui la traccia li
incontra; quelli delle fixture restano.

La sezione nativa dei documenti di versione 3 (`input.sezione_gpc`, esempi della distribuzione) conserva l'archivio XML di
`ModelArchive`, di centinaia di kB: nelle righe dei valori cambiati l'archivio è scritto come «‹archivio GPC · byte n · impronta›»;
l'impronta della riga `dati` copre comunque tutti i byte. Le tracce precedenti non hanno sezione nativa e non cambiano.

La finestra «Wizard · Sezione in cemento armato» ospita dal 20/10 `ConcreteTemplateWizardViewModel` (isola MVVM, come l'editor delle
sollecitazioni): il suo stato dopo ogni passo (tipologia, «Applica sezione» disponibile, errore, nota, ogni campo con valore, nascosto
e spento, anteprima con i percorsi delle staffe, impronta della bozza) e i suoi campi si leggono e si scrivono dal ViewModel, i pulsanti
si premono nella finestra.

## Scenari

Organizzati per sottopasso di F5.10, una cartella per sottopasso in `storica/`:

| Cartella | Scenari |
| --- | --- |
| `percorso-f3` | uno per ognuna delle 11 fixture F3 `str_palo`: apertura, calcolo automatico dell'apertura, conferma, la visita della cattura F3 (espansori aperti, ogni scheda di ogni controllo a schede), calcolo, riapertura |
| `ingressi` | forme e foro, secondi strati, armatura della T (`geometria-forme`); coordinate delle barre e ripristino (`barre-manuali`); classi, leggi, normativa e coefficienti (`materiali-normativa`); finestra «Nuovo materiale» e materiali salvati (`materiali-utente`); staffe e cavi da precompressione (`staffe-trefoli`); resistenze rapide |
| `azioni` | tabelle storiche (righe, duplicati, visibilità, stato del dominio), incolla (anche rifiutato), la finestra «Sollecitazioni della sezione» (isola MVVM di `SectionActionsViewModel`: annulla, applica, conversione dei dati precedenti, righe nuove, duplicati, eliminazione), sollecitazioni uniche |
| `domini` | opzioni del dominio 3D e 2D, mesh, forze, punti resistenti, cursori, vista, sezione sull'azione selezionata, filtri e ordinamenti delle tabelle |
| `sle` | opzioni comuni, omogeneizzazione, analisi non lineare, assi, contorni ed etichette; fessurazione e zone efficaci |
| `taglio` | righe di taglio e torsione, parametri manuali e automatici, modello senza staffe, cambio di normativa; modello circolare e staffe assenti |
| `dettagli` | elementi, durabilità, solette, ancoraggi e sovrapposizioni |
| `armature` | opzioni della ricerca e ricerca rifiutata (dati non validi, riserva torsionale 0 %, sezione in zona di sovrapposizione); ricerca completa con configurazioni scartate e ammissibili, alternative, limiti, applicazione della proposta (`ricerca-armature`: sulla soletta della fixture `soletta-dettagli-ricerca`, con copriferro 60 mm e sovrapposizione di 900 mm, senza i quali tutte le configurazioni sono scartate) |
| `ispezione` | sezione σ/ε al punto limite e all'azione inserita, contorni, momento–curvatura |
| `report` | disponibilità del report completo e breve (con calcolo in coda o bloccato) |
| `calcolo` | ricalcolo con il motore trattenuto (calcolo superato, «Calcola» durante un calcolo, chiusura durante un calcolo), revisione in sola lettura |
| `muri` | le due sezioni che i muri mandano al modulo c.a. («Apri sezione in c.a.» dello scenario `risultati-sezione` di F5.9), aperte come le apre la richiesta dei muri: lo stesso percorso di `percorso-f3` (aggiunte nel sottopasso S1, 18/10) |
| `esempi` | i 15 esempi `supporto/esempi/distribuzione/str_palo/*.anthea` (documenti di versione 3, 12 fatti dal wizard), aperti come la finestra apre un esempio (`DocumentService.Open`, poi i dati del foglio): lo stesso percorso di `percorso-f3`, tutte le parti e `[wizard]` (sottopasso S1b, 21/10) |
| `ingressi` (S1b) | la finestra del wizard: annullata, tipologia con un valore rifiutato e poi accettato, controlli dell'editor bloccati dal wizard, «modifica disposizione», rettangolo cavo a doppio contorno, di nuovo un cerchio ordinario (`wizard-tipologie`); capsula degli esempi con barre per coordinate e le due disposizioni (`wizard-capsula`); cerchio cavo degli esempi con foro e staffe circolari bloccati, wizard riapplicato, sola lettura (`wizard-sola-lettura`) |

Ogni passo è quello che fa l'utente con un controllo: un controllo che l'utente non può usare (spento, nascosto o protetto dalla
revisione in sola lettura) non fa nulla. Il motore trattenuto è il cancello dei lotti della sessione di analisi
(`ConcreteAnalysisSession`): il calcolo della scheda lo aspetta finché la traccia non lo rilascia. Le finestre modali sono guidate
da un'operazione del dispatcher dentro il loro ciclo; ogni finestra resta fuori schermo e non viene mai attivata.

Non sono nelle tracce: «Copia» e i comandi Excel (appunti e file del sistema), le finestre «Proprietà / report…», «Scala…» ed
«Espandi», «PNG», gli spostamenti dei divisori e la navigazione con il mouse nei disegni.

## Sezioni dei muri

`muri/*.json` (sottopasso S1 di F5.10, sezione 5 di `docs/refactoring/f5-10-sezione.md`): i dati che il ViewModel dei muri manda al
modulo c.a. con `ISheetHost.OpenModuleCopy("str_palo", …)` nello scenario `risultati-sezione` di F5.9, nel formato delle fixture F3
(`data`). `Presentation.Tests --sezione-tracce` li produce di nuovo dal ViewModel dei muri e prova che sono uguali a questi file (a meno
degli identificativi nuovi) e che i loro nomi e le loro impronte sono quelli della traccia congelata dei muri
(`tests/fixtures/f5-muri/storica/risultati-sezione.txt`). Gli scenari `muri/…` li aprono nella vista storica (tracce in `storica/muri`,
catturate il 18/10/2026 sul codice di `68668f1b`) e nel ViewModel della sezione.

## Disegni

`disegni/elenco.txt`: per ogni fixture F3 aperta e calcolata, una riga per disegno (primitivi e impronta) degli elenchi di disegno
(linee, forme, penne, riempimenti, testi con la loro origine, immagini con l'impronta dei pixel) delle viste 2D a dimensioni fisse:
sezione del pannello di controllo, mappa tensionale di ogni famiglia SLE con una riga calcolata (per due fixture anche gli otto
contorni della Rara), sezione del taglio, sezione σ/ε e piano delle deformazioni del dominio 3D (punto limite e azione inserita),
dominio 2D, momento–curvatura. Gli elenchi completi restano negli artefatti della corsa.

`disegni/domini-3d.txt`: il dominio 3D di ogni fixture come dati semplici (mesh, punti delle azioni, selezione, resistenze, opzioni),
senza la camera.

Gli elenchi dipendono dai font installati (Segoe UI): valgono sulla macchina del banco, come le prove di contrasto.

## Cattura e controllo

- Catturate il 17/10/2026 sul codice di produzione di `029d5d0b` (vista storica invariata da `743d7aec`: il commit aggiunge solo
  fixture e prove) con:

  ```
  dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-sezione-historic-capture <radice> <cartella>
  ```

  Due catture (`supporto/artefatti/refactoring/f5/f5-10/traccia-18` e `traccia-19`) sono uguali byte per byte: 37 scenari,
  79826 righe, 120 disegni 2D, 33 righe dei domini 3D.
- Il 18/10/2026 (sottopasso S1) si aggiungono i due scenari `muri/…`, catturati sul codice di `68668f1b` (vista storica invariata):
  39 scenari, 84832 righe; la seconda cattura, in controllo, è uguale alla prima e ai 37 scenari e ai disegni già congelati.
- Il 21/10/2026 (sottopasso S1b, sezione 9.5 di `docs/refactoring/f5-10-sezione.md`) si aggiungono i 15 scenari `esempi/…` e i tre
  `ingressi/wizard-…`, con la parte `[wizard]`, catturati sul codice di `3e1b86de` (vista storica invariata dal 20/10): 57 scenari,
  118860 righe. Due catture (`supporto/artefatti/refactoring/f5/f5-10-s1b/cattura-a` e `cattura-b`) sono uguali byte per byte; nelle
  due i 39 scenari e i disegni già congelati sono uguali a questi file.
- La suite `ui/check-sezione-historic` dello stadio `ui` ricattura la vista storica e la confronta con queste tracce e con gli
  elenchi dei disegni, finché la vista storica esiste.
- Le tracce non si rigenerano: una differenza della vista storica o del ViewModel si spiega e si registra.
- `vista-storica.json` (23/10/2026): registrato da `ui/check-sezione-view` sulla vista storica, sul commit `879e6b71`, parti
  `esportazioni,sola-lettura,nomi,finestre` (PASS 167: la vista XAML dava gli stessi valori), per il passaggio della Release. Per le 11
  fixture le impronte SHA-256 dei testi confrontati nell'ordine dei confronti (dati, risultato e risultati esportati con gli identificatori
  numerati e gli esiti dei passi paralleli nell'ordine fisso, C6), le parti dei report Word, breve e della curva M–χ (core.xml e document.xml
  normalizzati come in F3, le altre byte per byte), i `Modified` del report; i controlli liberi e bloccati in sola lettura (172), i nomi di
  automazione di 4 fixture, i valori delle finestre «Scala…» e «Proprietà / report…» di 2 fixture. Da allora `ui/check-sezione-view` non apre
  più la vista storica.

## Aggiornamento per il set di librerie AG (24/10/2026, registro AG-CA-01)

Decisione dell'utente del 24/10/2026 sulle differenze delle librerie nuove: «registra e aggiorna». Con il set AG (Model 2d59bbc6,
GPCModel 5.21.0.0, e Checker f6d87d28, GPCChecker.Concrete 0.20.0.0: parabola-rettangolo analitica come legge predefinita,
integrazione esatta) cambiano i numeri dei punti resistenti, delle resistenze rapide, della sezione σ/ε e della curva M–χ. La vista
storica è archiviata, quindi le righe cambiate vengono dal ViewModel (`Presentation.Tests/sezione-ca`, uguale alla vista storica sul
set precedente), riportate una per una nelle tracce: stesso numero di righe, nessuna riga aggiunta o tolta, BOM e fine riga invariati.
- 53 tracce, 1035 righe: 689 impronte di «risultato», 342 righe con soli numeri diversi, 4 testi della curva M–χ (My, N e residuo del
  punto limite); `disegni/domini-3d.txt`: 20 righe di 10 fixture (impronte della mesh e dei punti resistenti; la sezione a T passa da 1570
  a 1540 vertici con gli stessi 3136 triangoli).
- Non aggiornate (strategia «Intersezione», voce AG-CA-03, in attesa del set di Checker con la correzione): `domini/opzioni-domini.txt`,
  `percorso-f3/rettangolare-materiali-utente.txt`, `esempi/rettangolare-materiali-utente.txt` e la riga `rettangolare-materiali-utente`
  di `disegni/domini-3d.txt`. Fino al set nuovo `Presentation.Tests/sezione-ca` si ferma su queste.
- `ingressi/materiali-utente.txt` (voce AG-CA-04, accettata dall'utente il 24/10 con «5A»): aggiornate 13 righe (resistenze rapide, curve
  dei materiali, stress block con My+ = |My−|, riepilogo «Rara · tensioni» NON VERIFICATA η 1,19 e 3 impronte di «risultato» dei passi
  senza punti rifiutati); restano 11 righe di AG-CA-03 (3 riepiloghi SLU 3D e 8 impronte dei passi con i punti rifiutati).
- `vista-storica.json` (impronte di `ui/check-sezione-view`) non è stato toccato: il coordinatore lo verifica con lo stadio `ui`.

## Aggiornamento per il set AG ufficiale (24/10/2026, sera; registro AG-CA-03, AG-CA-01 e AG-CA-05)

Con la release AG ufficiale in `lib/Checker` (4dd8018a: Model 2d59bbc6, Checker 22dabe69, GPCChecker.Concrete 0.20.0.1) i punti della
strategia «Intersezione» non sono più rifiutati (AG-CA-03 corretta in libreria). Righe riportate dal ViewModel, una per una, dalla corsa
`ANTHEA\supporto\artefatti\ci\20261024-221519-ag-ufficiale-mirata` (`Presentation.Tests/sezione-ca`): stesso numero di righe, BOM e fine
riga invariati, nessun testo diverso oltre ai numeri e alle impronte.
- Tracce (AG-CA-01 e AG-CA-03): `domini/opzioni-domini.txt` 181 righe (113 di sole impronte, 68 con soli numeri),
  `esempi/rettangolare-materiali-utente.txt` 13 righe (9 impronte di «risultato», 4 con numeri), `percorso-f3/rettangolare-materiali-utente.txt`
  3 righe (resistenze rapide). Rispetto ad AF: resistenze dei domini fino a +0,25 % (Mx,Rd 719,80 → 721,57 kNm, dominio elastico 2D;
  strategia «Intersezione» NRd −4597,54 → −4602,72 kN, Mx,Rd 919,51 → 920,54 kNm, +0,11 %), η stampati 0,54 → 0,53 e 0,70 → 0,69 per
  arrotondamento, verdetti invariati; resistenze rapide di rettangolare-materiali-utente fra −0,019 % e −0,009 % (Mx+ 458,66 → 458,62 kNm).
- Senza modifiche, ora uguali alla cattura con i valori AF: le 11 righe di `ingressi/materiali-utente.txt` lasciate per AG-CA-03 e la riga
  `rettangolare-materiali-utente` di `disegni/domini-3d.txt`.
- `disegni/elenco.txt` (AG-CA-05, corsa `ANTHEA\…\20261024-221519-ag-ufficiale-mirata\ui_check-sezione-view`): 56 righe di 11 fixture
  (ispezione e piano 3D, dominio 2D, curva M–χ), con lo stesso numero di primitivi; negli elenchi completi, confrontati con quelli della
  corsa AF `20261023-193404-merge-muri-ui`, cambiano solo coordinate e testi numerici, l'impronta dell'immagine della mappa 3D (17 disegni
  d'ispezione) e in due disegni il colore di un riquadro della scala (#FFD1605D ↔ #FFD1605E).
- `vista-storica.json` (AG-CA-05): nelle esportazioni delle 11 fixture 50 valori della sequenza (solo i confronti dei risultati: 3, 7, 11,
  14 e 17; mai i dati) e 62 parti dei report (word/document.xml del report, del breve e della curva M–χ, figure dei domini e della curva),
  presi dalla vista XAML con una registrazione temporanea di `ui/check-sezione-view`, non committata (corsa
  `supporto/artefatti/ci/20261024-225640-ag-ca2-cattura` del worktree `aw-ag-ca2`). Nei report Word, confrontati con quelli della corsa AF,
  nessun testo cambia oltre ai numeri (fino a ±0,37 %, un η stampato 2,69 → 2,68); `rettangolare-materiali-utente` cambia solo nel report
  della curva M–χ. Sola lettura, nomi e finestre invariati.
- 27/10/2026, librerie della 1.14.2 (Checker 4a1b0090, AJ-LIB-01, voce R9): il taglio senza staffe ha il dettaglio VEd,max (0,5 bw d ν fcd). In `percorso-f3/taglio-senza-staffe-manuali.txt` sostituite 11 righe (9 impronte del risultato e 2 dettagli del taglio); in `vista-storica.json` 5 valori della sequenza della stessa fixture (risultati e JSON esportato), dalla vista XAML con una registrazione temporanea di `ui/check-sezione-view`, non committata (corsa `supporto/artefatti/lib-1142r/ui-sezione-registrazione`). Report Word, breve e della curva M–χ e le altre 10 fixture invariati.
