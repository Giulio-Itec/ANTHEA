# Paratie con sezione grafica ed editor affiancato

Richiesta del 20 ottobre 2026: rendere il modulo leggibile e consentire di
modificare gli input vedendone l'effetto, senza passare a una scheda separata.
Prosecuzione locale in `refactoring/f5-paratie-seguito`, allineata alla
versione rilasciata tramite il merge `f7fad388`. La cartella principale
rimane su `refactoring/f5`.

## Comportamento

- Disegno e campi nella stessa scheda Modello, con divisore ridimensionabile.
  Percorso numerato, impostazioni specialistiche raggruppate, importazione
  della stratigrafia disponibile nel pannello laterale.
- Sezione degli input della fase corrente: strati, terreni, falde assegnate,
  tratti della parete, sostegni presenti e azioni. Navigazione dei casi e
  delle fasi indipendente dal calcolo; nessuna selezione cambia gli input.
- Clic su un elemento per aprire la riga corrispondente; pulsanti, elenchi
  e menu consentono le stesse operazioni senza usare il disegno.
- Aggiunta di una fase con identificativo disponibile e caso corrente;
  selezione immediata della nuova fase. I nuovi carichi e le nuove
  attivazioni prendono il caso e la fase correnti. Nessun parametro
  ingegneristico è inventato o dedotto: la nuova fase usa i valori iniziali
  del modulo, da compilare, senza copiare azioni/sostegni della precedente.
- La modifica del dato aggiorna il disegno e conserva l'invalidazione
  preesistente dei risultati. Dati geometrici mancanti/non finiti non
  diventano quote zero. Storie con fasi omonime restano separate.

La rappresentazione è piana. Quote verticali in scala; larghezze della
sezione disegnata, spessori, lunghezze dei sostegni e frecce simbolici.
La linea idraulica rappresenta il dato assegnato, non una superficie
freatica ottenuta dal FEM. Il carico uniforme della fase è indicato anche
nel riepilogo testuale. Diagrammi, deformate e verifiche rimangono risultati
del calcolo, distinti dall'anteprima. Non è un editor CAD o DXF.

## Distribuzione del codice

Application assegna identificativi e proprietà delle nuove righe;
Presentation proietta gli input e governa la selezione; WPF disegna,
trasforma coordinate grafiche e individua l'elemento selezionato.
Le costanti del renderer sono distanze in pixel indipendenti dal DPI.
Nessuna equazione geotecnica o strutturale è introdotta in ANTHEA.
Il set GPC AA, le verifiche Concrete/Steel e gli attesi numerici restano
invariati. Non viene anticipata l'integrazione del kernel fondo scavo.

## Collaudi ed evidenze

Output in `supporto/artefatti/paratie/grafica-*`.

- Cattura precedente `grafica-prima`: Presentation 3411 e WPF 59 controlli
  PASS. Il runner segnala tre nuovi sorgenti creati durante la corsa, dopo
  la compilazione: è una cattura del codice precedente, non un'attestazione
  di checkout invariato. Il riepilogo originale è conservato.
- `grafica-check02`: Presentation 3430 controlli PASS, comprensivi di 19
  nuove asserzioni; nove JSON paratie identici byte per byte alla cattura
  precedente. Il controllo architetturale ha segnalato le costanti del
  disegno, poi nominate e sostituite con trasformazioni WPF dove pertinente.
- `grafica-check04`: 557 controlli architetturali e 69 WPF PASS. Comprende
  selezione grafica, aggiunta di F3, modifica del TextBox della quota a −6 m
  con disegno visibile, cambio fase, reload, tema scuro, finestra 1100×760
  e calcolo dell'esempio con tirante e falda. Nessuna baseline architetturale
  o lista di fallimenti ammessi è stata ampliata.
- I tentativi intermedi hanno individuato una selezione WPF nulla con
  indice apparentemente valido dopo reload. La selezione viene ora
  azzerata prima di sostituire le righe e poi ripristinata; il controllo
  verifica sia l'indice sia l'elemento effettivamente selezionato.

L'esempio `paratia-grafica.anthea` viene generato dal driver WPF con risultati
convergenti; valori sintetici per provare il flusso, non un progetto reale.

## Guide e tracciabilità

Guide globali Rev53, datate 20 ottobre 2026: pratica 96 pagine e teorica
164 pagine, comprese copertine e indici. Wiki rigenerata con 77 capitoli
e 7196 termini; nuova figura originale ottenuta dalla prova WPF della
modifica di F3. Aggiornati i quattro PDF degli indici/README.

Usata la catena ITEC del repository con Word: costruzione, aggiornamento
dei campi, finalizzazione e verifica finale. Il renderer aggiuntivo della
skill documenti non è disponibile perché manca LibreOffice; la conversione
Word prevista dal repository è invece conclusa. Audit delle 387 formule
modificabili e delle 26 parti ITEC conservate identiche per ciascun DOCX.
Tutte le pagine PDF sono state renderizzate: corpi invariati confrontati
per pixel con la revisione precedente, pagine variate controllate
visivamente, incluse figura, copertine, indici e formule.

I 18 originali precedenti sono conservati in
`supporto/SUPERATI/paratie-grafica-rev53-20261020/`, ciascuno con origine,
motivo e SHA-256 verificato; archivio escluso dalla conversione dei fine
riga. Le copie correnti dei PDF globali coincidono con l'edizione ITEC.
Evidenze in `supporto/artefatti/paratie/grafica-guide53/`.

## Chiusura del collaudo locale

Commit di implementazione `7a06492c`; guide e indici `b60d5aa4`.
Su quest'ultima revisione pulita e invariata è stato eseguito:

```powershell
./build/ci.ps1 -Profile full `
  -Only '^(Presentation.Tests|Architecture.Tests|Wiki.Tests|wiki/indice|wiki/manuale|qa/no-test-code|ui/check-paratie-view|ui/check-wiki-offscreen)$' `
  -Tag paratie-grafica-finale -Output supporto/artefatti/paratie/grafica-finale
```

13 suite PASS, nessun fallimento nuovo o ammesso. È un collaudo mirato,
non l'esecuzione dell'intero profilo full. Include build UiTests e Release,
3433 controlli Presentation (22 nuove asserzioni), 557 architetturali,
340 Wiki senza WPF, 3439 Wiki WPF e 69 WPF paratie.
Nove JSON paratie identici byte per byte rispetto a `grafica-prima`;
le 13 DLL nella build Release coincidono per SHA-256 con il set AA.
Baseline numeriche e lista dei fallimenti ammessi invariate.

La build locale è `src/ANTHEA.Desktop/bin/Release/net8.0-windows/ANTHEA.exe`.
`supporto/artefatti/paratie/grafica-finale/Apri-Paratia.cmd` apre questa
build con l'esempio `ui_check-paratie-view/vista/paratia-grafica.anthea`,
prodotto dalla prova WPF dopo un calcolo convergente.
Il collegamento non installa pacchetti né modifica associazioni di file.

Il checkout principale resta pulito su `refactoring/f5`, SHA `5010b46e`.
L'incremento resta locale in `refactoring/f5-paratie-seguito`.
La matrice complessiva del modulo e i vincoli del rilascio 1.11.0
rimangono aperti dove già dichiarato. Questo incremento non produce
un nuovo set GPC né sostituisce l'installer 1.11.0.

Successivamente l'utente ha richiesto il rilascio 1.11.1, con ricompilazione,
push e installer: [verbale della nuova distribuzione](../refactoring/installer-1.11.1.md).
