# Tracce e disegni della vista storica del palo e del micropalo orizzontale (F5.7)

Proposta F5, sezione 7.2: prima di migrare un modulo si cattura il comportamento della vista storica. Il riferimento F3
confronta i dati in forma canonica (chiavi ordinate) e non vede la scheda. Queste tracce conservano invece, dopo
l'apertura e dopo ogni passo di uno scenario, lo stato della scheda della capacità («Dati comuni e capacità · Broms /
stratificato») di `HorizontalWorkspace`:

- i byte dei dati nell'ordine in memoria;
- il numero di `Modified`, la sola lettura, la possibilità di esportare (risultato della capacità presente e nessun calcolo
  in corso), i pulsanti «Diagrammi e dettagli» ed «Esporta CSV», la scheda delle analisi selezionata;
- il risultato della capacità (impronta SHA-256), lo stato, la sintesi, il momento con la sua nota, il modello automatico con
  la sua nota, gli avvisi;
- i campi dei sette form (generali, modello di calcolo, coefficienti, efficienza, momento, sezione c.a. o CHS, EJ):
  etichetta, valore, voci, attivo, attenuato, visibile; i gruppi espandibili con le loro chiavi;
- la tabella e l'esito della verifica, con il suo colore (soddisfatta, non soddisfatta, attenuato);
- le stratigrafie: righe come le mostra la tabella (copia di presentazione di ogni strato, con lo strato e Kp calcolati; il
  colore dello strato è escluso, perché nella vista nuova è un token), stratigrafia selezionata e profilo disegnato; per la
  stratigrafia selezionata, l'editor del terreno di ogni riga (reazione orizzontale: categoria o addensamento, legge, parametro,
  minimo, massimo, valore o valore tabellato, «nh*» e nh, impronta della descrizione);
- la sezione disegnata (contorno, barre, staffa, assi, didascalia) o le proprietà del CHS;
- quello che mostra «Diagrammi e dettagli» per ogni sondaggio: le tabelle «Tabella terreno», «Tabella» e «Meccanismi» e la riga
  del meccanismo, dei residui e delle cerniere.

| Cosa | Dove |
| --- | --- |
| Scenari e formato | `tests/Shared/HorizontalPileTrace.cs` |
| Vista storica | `tests/Desktop/HorizontalPileHistoricTrace.cs` (`SheetEditor` con `HorizontalWorkspace`, fuori schermo) |
| ViewModel | `tests/ANTHEA.Presentation.Tests/HorizontalPileSheetChecks.cs` (senza WPF) |

Ogni passo è quello che fa l'utente con un controllo: un controllo che l'utente non può usare (spento, nascosto o protetto
dalla revisione in sola lettura) non fa nulla. Il servizio di calcolo della capacità è quello del modulo con una barriera
(`HeldModuleService`), che trattiene il calcolo sullo sfondo quando lo scenario lo chiede. La risposta elastica (F5.8) non è
nelle tracce: resta la sua parte che agisce sui dati e su `Modified` anche dalla scheda della capacità (preparazione
all'apertura, `elastico.stratigrafia` quando cambia il numero delle stratigrafie, un secondo `Modified` per ogni modifica).

Gli scenari sono 27:
- `palo-<id>` e `micropalo-<id>`, uno per ognuna delle 21 fixture F3 dei due moduli (16 di F5.7, più le 5 aggiunte in F5.8 sulla
  risposta elastica, `elastico-*`): apertura, conferma, la visita delle due
  schede delle analisi come `ui/f3-equivalence` (andata e ritorno; una fixture aperta sulla risposta elastica torna alla
  capacità), attesa, calcolo, conferma, riapertura, conferma, attesa del ricalcolo automatico;
- `modifiche-palo`: campi, scelte e opzioni dei sette form (testa impedita con e senza eccentricità, falda, metodo, verticali,
  efficienza di Reese e Van Impe e manuale, momento manuale, materiali della sezione dal catalogo e personalizzati, secondo
  anello interno, coefficienti unitari, EJ manuale), testo non numerico, campo vuoto;
- `modifiche-micropalo`: CHS manuale e di catalogo, acciaio, pesi, spessore vuoto, testa impedita, momento manuale e del CHS,
  metodo stratificato;
- `stratigrafie-palo`: celle degli strati (tipologia, testi, Cu vuota), editor del terreno (categoria, legge, valore, «nh*»
  e nh), strato aggiunto e tolto, stratigrafia aggiunta, «Copia in…» un'altra e una nuova, «Copia da…», tolta dalla scheda e
  con «− Stratigrafia»;
- `sola-lettura`: revisione in sola lettura, modifiche rifiutate dai controlli; la scheda delle analisi resta selezionabile e
  scrive `vista_orizzontale` con `Modified` (comportamento storico);
- `scheda-elastica`: andata e ritorno con un risultato, una modifica seguita dal passaggio alla risposta elastica (il ricalcolo
  in attesa è annullato) e il ritorno (ricalcolo), riapertura;
- `calcolo-sullo-sfondo`: ricalcolo automatico trattenuto e superato da una modifica, «Calcola» durante un calcolo, passaggio
  alla risposta elastica durante un calcolo (mai pubblicato), chiusura e riapertura durante un calcolo.

Non sono nelle tracce: il dialogo «Parametro dello strato · origine e applicabilità» (modale: fonte, motivazione, «Adotta nuova
media», «Mantieni valore…»), l'esportazione CSV (dialogo dei file), «Estendi»/«Riduci» delle schede e l'espansore «Opzioni
avanzate» (stato della vista). Le provano le prove del ViewModel con valori scritti a mano.

## Disegni

`disegni/elenco.txt` conserva, per ogni fixture F3 dei due moduli con un risultato della capacità, gli elenchi di disegno
(linee, forme, penne, riempimenti, testi con la loro origine) di:
- `HorizontalDiagrams` di ogni sondaggio, terreno e palo, alla dimensione delle figure del report (1000 × 660) e a una più
  grande (1280 × 820);
- il profilo stratigrafico con le forze (`StratigraphyDrawing` con `HorizontalResult`) di ogni sondaggio (420 × 640).

Ogni riga dà il numero dei primitivi e l'impronta dell'elenco; gli elenchi completi sono nella cartella della corsa
(`disegni/*.txt`). Servono a provare che lo spostamento dei dati dei diagrammi in Application lascia il disegno identico. I testi
dipendono dai font installati: gli elenchi valgono sulla macchina del banco, come le prove di contrasto.

`diagrammi/<modulo>--<id>.json` (aggiunti con il ViewModel, commit di F5.7 successivo alle tracce) conserva gli stessi diagrammi come
dati semplici (`HorizontalPileDiagrams`: pannelli, curve con i segmenti e il tratteggio, campi degli assi, strati, quote, note), quelli
che `HorizontalDiagrams` disegna dopo lo spostamento in Application. Gli elenchi di disegno, congelati prima dello spostamento e
invariati dopo, provano che questi dati danno il disegno storico; `Presentation.Tests` confronta con questi file i diagrammi esposti dal
ViewModel (`HorizontalPileSheetViewModel.Diagram`). La suite `ui/check-palo-orizzontale-historic` li ricalcola dalla vista storica e
li confronta a ogni corsa.

## Cattura e controllo

- Catturate il 16/10/2026 sul codice di produzione di `d3b0750a` (vista storica invariata da `92613e60`; l'unica modifica di
  produzione è il servizio della capacità iniettabile, nullo in produzione) con:

  ```
  dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-palo-orizzontale-historic-capture <radice> <cartella>
  ```

  Tre catture (`supporto/artefatti/refactoring/f5/f5-7/traccia-a`, `-b`, `-c`) sono uguali byte per byte: 22 scenari, 36211
  righe, 40 disegni.
- Il 17/10/2026 (F5.8) si sono aggiunte le tracce delle 5 fixture F3 nuove della risposta elastica, catturate con la vista storica
  sul codice di `4b6a59de` (le fixture si aprono sulla risposta elastica e il percorso F3 le porta alla capacità, dove la traccia le
  segue). Due catture (`supporto/artefatti/refactoring/f5/f5-8/traccia-orizzontale-a`, `-b`) sono uguali byte per byte: 27
  scenari, 40277 righe, gli stessi 40 disegni; le 22 tracce precedenti sono invariate.
- La suite `ui/check-palo-orizzontale-historic` dello stadio `ui` ricattura la vista storica e la confronta con queste
  tracce e con l'elenco dei disegni, finché la vista storica esiste.
- `Presentation.Tests`, nello stadio `fast`, confronta le stesse tracce con il ViewModel.
- Le tracce non si rigenerano: una differenza della vista storica o del ViewModel si spiega e si registra.
