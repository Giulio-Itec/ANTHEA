# Tracce e disegni della vista storica della risposta elastica del palo e delle armature (F5.8)

Proposta F5, sezione 7.2: prima di migrare un modulo si cattura il comportamento della vista storica. Il riferimento F3
confronta i dati in forma canonica e non vede la scheda; le tracce F5.7 (`tests/fixtures/f5-palo-orizzontale`) seguono la
scheda della capacità. Queste tracce conservano invece, dopo l'apertura e dopo ogni passo di uno scenario, lo stato della
seconda scheda del palo e del micropalo orizzontale, «Risposta elastica · trave su molle» (`ElasticPileWorkspace` con
l'editor delle armature `PileReinforcementEditor`):

- i byte dei dati nell'ordine in memoria; il numero di `Modified`, la sola lettura, la scheda delle analisi, l'esportabilità;
- la risposta FEM e il risultato con le armature (impronte SHA-256 senza i tempi misurati `tempi_ms`), lo stato con la barra di
  avanzamento, la rigidezza EJ;
- il confronto con un archivio elastico precedente (testo e comandi) oppure l'intestazione: stratigrafia della risposta, comandi,
  i campi delle opzioni (passo, vincolo alla punta), le caselle di quello che il disegno mostra;
- quello che il disegno mostra (risposta o risultato) e la quota selezionata, la riga della selezione, la sezione disegnata alla
  quota (contorno, barre, staffa, assi, didascalie) con il suo dettaglio, gli estremi, la tabella «Valori per quota» (impronta e
  prime due righe), «Esporta CSV», il riepilogo delle verifiche per tratto, la tavola delle armature (messaggio, pezzi, risultato);
- l'editor delle armature: fase e avanzamento, messaggio, comandi, armatura trasversale principale; per ogni tratto
  l'intestazione, la fine, «Usa principale», i campi delle armature e dell'anello interno con le note, il tipo trasversale, i
  comandi e la distinta; i tre gruppi di ipotesi (verifica, sisma con i suoi comandi, sovrapposizioni e copriferro) campo per
  campo; «Controlla minimi pali»; il pannello «Sezione e verifiche», un elemento per riga (testi con il tono del colore, scelte,
  sezione, esito, gruppi, comandi);
- dopo un passo con un dialogo, quello che il dialogo mostrava (errore della divisione, proposta, nota del verificatore c.a.,
  stato del dimensionamento).

Gli identificativi casuali dei dati (tratti divisi, azioni dei fogli del verificatore c.a.) sono sostituiti da segnaposto
nell'ordine in cui compaiono (`ElasticPileIds`), così due catture danno le stesse righe.

| Cosa | Dove |
| --- | --- |
| Scenari e formato | `tests/Shared/ElasticPileTrace.cs` |
| Vista storica | `tests/Desktop/ElasticPileHistoricTrace.cs` (`SheetEditor` con `HorizontalWorkspace`, fuori schermo) |
| ViewModel | `tests/ANTHEA.Presentation.Tests` (senza WPF) |

Ogni passo è quello che fa l'utente con un controllo: un controllo che l'utente non può usare (spento, nascosto o protetto
dalla revisione in sola lettura) non fa nulla. I campi dei gruppi chiusi si usano aprendo prima il gruppo, come l'utente; in sola
lettura i campi dei gruppi chiusi si leggono come l'utente li trova aprendoli (la vista protegge i controlli che mostra). I
dialoghi (divisione, proposta, verificatore c.a., dimensionamento) sono le finestre della vista, create dai loro metodi
`Create…Window`, mostrate fuori schermo e usate con i loro pulsanti. Il calcolo della risposta elastica è quello della vista
(`ElasticPileAnalysisSession`) dietro la barriera della traccia (`HeldElasticAnalyses`), che lo trattiene quando lo scenario lo
chiede.

Gli scenari sono 20:
- `palo-<id>` e `micropalo-<id>`, uno per ognuna delle 11 fixture F3 dei due moduli aperte sulla risposta elastica: apertura,
  conferma, la visita delle due schede delle analisi come `ui/f3-equivalence`, attesa, calcolo, conferma, riapertura, conferma,
  attesa del ricalcolo automatico;
- `modifiche-risposta`: quota selezionata due volte all'interfaccia di due strati, stratigrafia della risposta, passo (anche non
  numerico), vincolo alla punta, caselle del disegno, forza orizzontale dalla scheda della capacità, «Modifica dati e strati»;
- `armature-tratti`: fine di un tratto, «Usa principale» tolto e rimesso, campi delle armature e dell'anello interno, tipo
  trasversale del tratto e principale, ipotesi di verifica, sisma e copriferro, minimi, «Approva tutto» ordinario e sismico, le
  dieci viste del riepilogo, scelta del tratto, «Distinta ferri», «Dividi…» con un testo non numerico e con una quota, «Unisci ←»;
- `armature-dialoghi`: «Proponi suddivisione…» applicata, verificatore c.a. del primo tratto con «Applica armatura» e del secondo
  chiuso, «Dimensiona…» senza e con le ipotesi approvate;
- `sola-lettura`: revisione in sola lettura, ogni comando rifiutato; quota, schede e calcolo ammessi;
- `migrazione-comuni` e `migrazione-legacy`: i due comandi del confronto con l'archivio elastico precedente;
- `scelta-stratigrafia`: la stratigrafia della risposta da scegliere, poi scelta, poi una stratigrafia aggiunta dalla capacità;
- `micropalo`: verificatore c.a. rifiutato per il CHS, proposta senza risultato, tratti e caselle del disegno;
- `calcolo-sullo-sfondo`: ricalcolo trattenuto e superato da una modifica, «Calcola» durante un calcolo con il FEM conservato,
  passaggio alla capacità durante un calcolo (non annulla la risposta elastica), chiusura e riapertura durante un calcolo.

Non sono nelle tracce: le finestre «Info», «Mostra confronto completo», «NTC §7.2.5», «Info sezioni» e «Apri tavola
armature…» (testi fissi o disegno), l'esportazione CSV e PNG (dialoghi dei file), il contenuto del foglio c.a. del verificatore
oltre a quello che scrive nei dati.

## Disegni

`disegni/elenco.txt` conserva, per ogni fixture F3 dei due moduli aperta sulla risposta elastica con una risposta dopo «Calcola»,
gli elenchi di disegno (linee, forme, penne, riempimenti, testi) di:
- `ElasticPileDrawing` con il risultato e le opzioni della fixture, con la sola risposta, con tutte le opzioni accese, a
  1750 × 650 (la vista) e a 1200 × 800, e con una quota selezionata;
- `PileReinforcementDrawing` (la tavola delle armature) alla sua dimensione, e vuota con un messaggio.

Ogni riga dà il numero dei primitivi e l'impronta dell'elenco; gli elenchi completi sono nella cartella della corsa
(`disegni/*.txt`). Sono congelati **prima** di toccare i due disegni. I testi dipendono dai font installati: gli elenchi valgono
sulla macchina del banco, come quelli di F5.7. La cattura con `ANTHEA.exe` e con `dotnet ANTHEA.dll` dà gli stessi elenchi.

## Cattura e controllo

- Catturate il 17/10/2026 sul codice di produzione di `50b0a547` più le aperture per le prove, senza cambiare il comportamento:
  l'interfaccia `IElasticPileAnalysis` (Application) implementata da `ElasticPileAnalysisSession`, il parametro facoltativo che la
  porta da `SheetEditor` a `ElasticPileWorkspace` (nullo in produzione), `partial` su `ElasticPileWorkspace` e i metodi
  `CreateSplitWindow`, `CreateProposalWindow`, `CreateConcreteWindow` di `PileReinforcementEditor`, che creano i dialoghi come
  prima e che i comandi mostrano come prima. Comando:

  ```
  dotnet X.Desktop/bin/UiTests/net8.0-windows/ANTHEA.dll --check-palo-elastico-historic-capture <radice> <cartella>
  ```

  Quattro catture (`supporto/artefatti/refactoring/f5/f5-8/elastico-c`, `-d`, `-e` con `ANTHEA.exe`, `-f`) sono uguali byte per
  byte, dopo la normalizzazione degli identificativi: 20 scenari, 28884 righe, 49 disegni.
- La suite `ui/check-palo-elastico-historic` dello stadio `ui` ricattura la vista storica e la confronta con queste tracce e con
  l'elenco dei disegni, finché la vista storica esiste.
- `Presentation.Tests`, nello stadio `fast`, confronta le stesse tracce con il ViewModel.
- Le tracce non si rigenerano: una differenza della vista storica o del ViewModel si spiega e si registra.
- 22/10/2026, voce PARATIE-19 (set candidato del 22/10, Concrete 0.18.0.0, taglio dei pali circolari con d = R + 2rs/π), con
  l'ok dell'utente: ricatturate fuori schermo, con il comando sopra, le nove tracce che cambiano (`armature-dialoghi`,
  `armature-tratti`, `calcolo-sullo-sfondo`, `modifiche-risposta`, `palo-elastico-sisma-testa-impedita`,
  `palo-elastico-tratti-anello-spirale`, `palo-single-soil-elastic`, `scelta-stratigrafia`, `sola-lettura`) e le due righe
  delle tavole delle armature in `disegni/elenco.txt` (stesso numero di primitive). Nelle tracce la risposta elastica è
  identica in tutti i 117 passi in cui compare; cambiano solo l'impronta del risultato e VRd nelle righe delle verifiche
  (dallo 0,36 all'1,86 % in più). Le altre undici tracce e gli altri 47 disegni non cambiano.
- 24/10/2026, voce AG-PO-02 (librerie del 24/10: Model 2d59bbc6, GPCModel 5.21.0.0; Checker f6d87d28, GPCChecker.Concrete
  0.20.0.0; parabola del calcestruzzo analitica come legge predefinita), con la decisione dell'utente del 24/10 («registra e
  aggiorna»). La vista storica è archiviata dal 23/10 e non si ricattura: ricattura parziale dal ViewModel. Nelle stesse nove
  tracce di PARATIE-19 sono sostituite solo le 236 righe cambiate, con il valore del ViewModel (`Presentation.Tests`, corsa
  `20261024-133916-ag-po-raccolta2`): la riga con le impronte della risposta e del risultato (117, impronta della risposta
  identica in tutti i passi), il testo delle verifiche dei tratti (117: MRd, rapporti e, dove compare, la sezione critica che
  passa al punto di calcolo adiacente perché i rapporti dei due punti erano quasi uguali) e due testi
  della tavola delle armature (η, MRd). Tolti numeri e impronte ogni riga è uguale a quella storica: nessun esito cambia. MRd
  cambia fra −0,02 % e +0,02 %. `disegni/elenco.txt` non è stato toccato: va ricatturato con lo stadio `ui`.
