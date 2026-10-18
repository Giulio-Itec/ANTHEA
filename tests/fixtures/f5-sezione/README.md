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

Una parte uguale a quella scritta dopo il passo precedente è scritta come `[parte] invariata`.

Gli identificativi creati dalla scheda (righe delle azioni, materiali) diventano `<nuovo-n>` nell'ordine in cui la traccia li
incontra; quelli delle fixture restano.

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
- La suite `ui/check-sezione-historic` dello stadio `ui` ricattura la vista storica e la confronta con queste tracce e con gli
  elenchi dei disegni, finché la vista storica esiste.
- Le tracce non si rigenerano: una differenza della vista storica o del ViewModel si spiega e si registra.
