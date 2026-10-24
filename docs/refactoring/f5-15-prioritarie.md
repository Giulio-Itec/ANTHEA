# F5.15: le 13 voci da guardare per prime

Questa scheda prepara la seduta del 24/10 su F5.15 (decisione dell'utente: «domani iniziamo F5.15 dalle 13 voci da guardare per prime»).
Le voci sono quelle della sezione «Da guardare per primi» di [f5-15-revisione.md](f5-15-revisione.md); i dettagli e le fonti sono lì.
Questa scheda non cambia nessuna scelta: per ogni voce dice com'è oggi, quali sono le opzioni e che cosa raccomando.

Stato: `refactoring/f5` a 575a7481, con tutte le viste MVVM nella Release e la shell nuova al posto di `MainWindow`. Le viste
storiche sono archiviate in `supporto/SUPERATI/ui-storica/`: è il fatto che cambia di più le voci 2 e 3.

**Decisione dell'utente del 24/10:** «accetto le 13 raccomandazioni della scheda». Le voci 1, 6, 8 e 12 si attuano dopo il
28/10; la 4 (B) è una voce di infrastruttura dopo F5; la 13 va con il set di librerie successivo all'unione di Codex.

**Come rispondere.** Basta una riga, per esempio «come raccomandato, tranne 6A e 8A». La lettera A è sempre l'opzione presa oggi.

## Riepilogo

| N. | Voce | Opzione presa (A) | Raccomando | Quando si fa | Cosa tocca |
| ---: | --- | --- | --- | --- | --- |
| 1 | V-a e separatori decimali | nulla cambia | **B**: impostazione «Sistema / Virgola / Punto», prove con «Virgola» | dopo il 28/10 | riferimenti congelati, una volta |
| 2 | V-g, traccia storica con un campo spento | traccia invariata | **A**, voce chiusa | subito | nulla |
| 3 | V-e, riga transitoria della traccia | nessun cambiamento | **A**, voce chiusa | subito | nulla |
| 4 | D-5 e D-2, calcoli fuori dal limitatore | come oggi | **A** ora, B come voce di infrastruttura dopo F5 | dopo F5 | nulla sui risultati |
| 5 | BD3, Bridge Design «occupato» durante il calcolo | occupato | **A** | già fatto | comportamento |
| 6 | BD4, prezzario in sola lettura | rifiutato | **B**: si apre in sola lettura, senza «Applica» | dopo il 28/10 | comportamento |
| 7 | SH5/SH18, SH6, SH12, SH22, SH28, SH17 | decise il 22/10 | **conferma** | già fatto | comportamento |
| 8 | SH49, stato della vista quando la scheda torna a schermo | si perde | **B ridotta**: la sotto-scheda scelta resta, modulo per modulo | dopo il 28/10 | comportamento |
| 9 | S1-b e V-f, «Calcola» conferma il foglio | conferma | **A** | già fatto | comportamento |
| 10 | S9-a, figure del report senza lo zoom | figure adattate | **A** | già fatto | comportamento |
| 11 | Muri, kh e kv del capitolo autonomo | come oggi | **A**, voce chiusa | subito | nulla sui risultati |
| 12 | T6, la sola lettura che scrive | come oggi, caso per caso | **B**: regola unica «in sola lettura non si scrive» | dopo il 28/10 | comportamento |
| 13 | R4, R5 e R10, voci di calcolo a sfavore di sicurezza | come oggi | **correggere** nelle librerie, con opzione legacy | col prossimo set di librerie | risultati di calcolo |

«Dopo il 28/10» vuol dire dopo il rinnovo della quota settimanale, come concordato. Le voci 2, 3 e 11 si chiudono senza lavoro. Le
voci 5, 7, 9 e 10 sono già attuate: serve solo la conferma.

## 1. V-a e separatori decimali

**Oggi.** ANTHEA non imposta la cultura dei numeri:
- i campi e le tabelle seguono Windows;
- la maggior parte dei disegni e dei report fissa l'italiano, cioè la virgola;
- su un PC con il punto, i campi mostrano «0.5» e i disegni «0,5».

Le prove fissano l'italiano solo in parte. Alcuni riferimenti congelati della sezione c.a. contengono il punto (`θ = 30.00°`), perché la
macchina delle catture usa il punto per le operazioni della finestra. Su una macchina con la virgola quelle prove cadrebbero.

**Opzioni.**
- **A.** Nulla cambia: le prove riproducono lo stato delle catture.
- **B.** L'impostazione che hai proposto il 22/10: «Sistema / Virgola / Punto» nel menu «Aspetto», predefinita «Sistema», cioè com'è oggi.
  Le prove fissano «Virgola» all'avvio, così non dipendono più dalla macchina. I riferimenti con il punto (l'elenco dei disegni della
  sezione c.a. e qualche riga delle tracce) si ricatturano una volta, in un commit motivato. Report e disegni restano con la virgola
  (strada «a» della [nota 15.2](f5-15-revisione.md#152-proposta-impostazione-dei-separatori)).
- **C.** Solo la parte delle prove, senza l'impostazione per l'utente.

**Raccomando B.** Risponde alla tua richiesta e chiude il problema delle prove con una sola ricattura dichiarata. Il formato unico anche
nei report (strada «b») va con la revisione dei report di fine refactoring.

## 2. V-g: la traccia storica ha scritto un campo spento

**Oggi.** Con le barre per coordinate il form dell'armatura è spento. La traccia storica, catturata fuori schermo con il gruppo chiuso, ha
scritto lo stesso `bottom_bar_count`. La vista nuova fa quel passo sul campo del ViewModel e lo elenca; la traccia non cambia.

**Opzioni.**
- **A.** Traccia invariata.
- **B.** Ricatturare la traccia storica con i form disposti.

**Raccomando A e chiudere la voce.** Dal 23/10 la vista storica della sezione c.a. è archiviata e non si compila più. Ricatturare
vorrebbe dire rimetterla in piedi solo per questo. La differenza è nota e già elencata nella prova.

## 3. V-e: riga transitoria in una traccia congelata

**Oggi.** Una riga della traccia `sle-fessurazione-assi` contiene un messaggio transitorio. Con la macchina carica, la prova della vista
storica (`ui/check-sezione-historic`) cadeva una volta su tre.

**Opzioni.**
- **A.** Nessun cambiamento.
- **B.** Ricatturare la traccia aspettando l'ispezione.

**Raccomando A e chiudere la voce.** La prova storica non esiste più dal 23/10 (archiviata con la vista). La prova della vista nuova
(`ui/check-sezione-view`) non ha mostrato il problema nelle corse del 23/10. Se lo mostrasse, si riapre.

## 4. D-5 e D-2: calcoli fuori dal limitatore

**Oggi.** La sezione c.a. (domini, tensioni, M–χ, ricerca delle armature) e la risposta elastica del palo orizzontale calcolano su thread
propri, senza passare dal limitatore che regola i calcoli degli altri moduli. I risultati sono uguali, lo prova la suite `--paralleli`.
Il rischio è solo di carico: con molte schede aperte i calcoli si sommano e la macchina rallenta.

**Opzioni.**
- **A.** Come oggi.
- **B.** Far passare quei lotti dal limitatore.

**Raccomando A adesso e B come voce di infrastruttura dopo F5.** Non cambia nessun risultato, non blocca la chiusura di F5, e la
prova per farla bene esiste già.

## 5. BD3: Bridge Design «occupato» durante il calcolo

**Oggi.** Durante il calcolo la finestra vede il foglio occupato: salvataggio, chiusura e report chiedono di attendere. Con la vista
storica il foglio non risultava mai occupato, e il report poteva uscire mentre il calcolo cambiava il risultato.

**Raccomando A**, cioè come adesso: è la regola di tutti gli altri moduli ed evita un report con un risultato a metà.

## 6. BD4: prezzario in una revisione in sola lettura

**Oggi.** La vista nuova rifiuta l'editor del prezzario in sola lettura, come MU3 per i muri. La storica lo apriva e «Applica prezzario»
scriveva nella revisione passata.

**Opzioni.**
- **A.** Rifiuto.
- **B.** Si apre in sola lettura, senza «Applica».

**Raccomando B.** Permette di consultare il prezzario di una revisione passata senza poterla modificare. È un lavoro piccolo. Anche A va
bene: la differenza importante, non scrivere nella revisione, c'è già.

## 7. SH5/SH18, SH6, SH12, SH22, SH28 e SH17: decise il 22/10, da confermare

Le hai decise il 22/10 sulla bozza; le cinque che seguono cambiano il comportamento rispetto a `MainWindow`:
- **SH5/SH18:** annulla o ripeti con una scheda il cui foglio non esiste più. La scheda si chiude con un solo avviso; le altre restano
  aperte.
- **SH6:** l'anteprima di una revisione con più schede vale per tutto il progetto. Le schede passano in sola lettura; quelle senza foglio
  si chiudono.
- **SH12 e SH28:** «Nuovo …» e un esempio della Wiki aprono una scheda nuova senza chiedere di salvare. `MainWindow` sostituiva il
  documento.
- **SH22:** la pagina «Progetti» senza progetto apre un archivio nuovo senza domande, perché i file singoli restano aperti.

SH17 (una scheda i cui dati cambiano fuori si ricrea) conserva il comportamento di oggi, ma insieme a SH49 decide se lo stato della vista
si perde.

**Raccomando la conferma di tutte.** Sono la conseguenza diretta delle schede multiple che hai chiesto l'8/10. La prova della f5f le
mostra.

## 8. SH49: stato della vista quando una scheda torna a schermo

**Oggi.** Quando una scheda torna visibile, la vista si ricostruisce. Restano dati, risultati e pannelli tenuti dal ViewModel. Si perde
lo stato che solo la vista teneva: per esempio, la scheda dei risultati del palo verticale torna alla prima. `MainWindow` teneva vivo
l'editor e quindi non lo perdeva.

**Opzioni.**
- **A.** Accettare.
- **B.** Portare nel ViewModel, modulo per modulo, lo stato da conservare.

**Raccomando una B ridotta**: conservare la sotto-scheda scelta (il caso che si nota) in tutti i moduli, non ogni dettaglio della vista.
È un lavoro contenuto, da fare dopo il 28/10.

## 9. S1-b e V-f: «Calcola» conferma il foglio prima di calcolare

**Oggi.** Con l'ospite nuovo, «Calcola» conferma il campo in modifica prima di calcolare e scrive `visible` nelle righe delle azioni. La
vista storica della sezione c.a. non lo faceva. Tracce e risultati sono uguali.

**Raccomando A**, cioè come adesso: è la regola D7, uguale per tutti i moduli. Calcolare senza confermare il campo in modifica darebbe un
risultato diverso da quello che l'utente vede.

## 10. S9-a: figure del report della sezione c.a. senza lo zoom dell'utente

**Oggi.** Le figure del report si disegnano adattate al riquadro, senza lo zoom e lo spostamento fatti sull'anteprima. La vista storica
copiava i suoi controlli.

**Raccomando A**, cioè come adesso: è la stessa scelta di V8 del palo verticale, già accettata, e rende il report indipendente da come
era disposta la finestra.

## 11. Muri: kh e kv del capitolo autonomo

**Oggi.** La scheda dei muri ricava kh e kv dai parametri del sito all'apertura e li scrive nel documento; il capitolo autonomo no. Per un
documento mai aperto dalla scheda i dati preparati sono diversi. Il calcolo li ricava comunque, quindi i risultati sono uguali, e le
fixture salvano i valori ricavati.

**Opzioni.**
- **A.** Come oggi.
- **B.** Allineare la preparazione del capitolo autonomo a quella della scheda.

**Raccomando A e chiudere la voce.** Non cambia nessun risultato. Allineare cambierebbe i dati salvati dei documenti di prova senza un
beneficio per l'utente.

## 12. T6: la sola lettura che scrive

**Oggi** non c'è una regola unica:
- **Scrivono nella revisione:** curve del palo verticale, scheda «Risposta elastica», gruppi delle fondazioni, pannelli della sezione c.a.
  Lo fanno con chiavi di stato della vista, quindi una revisione passata risulta «modificata» senza cambiamenti dei dati.
- **Rifiutano la scrittura:** MU3, MU4, BD4 e FN4.
- **Cambia i dati in sola lettura:** nella sezione c.a. «Esporta Excel» → «Applica» (§10.4-1).

**Opzioni.**
- **A.** Come oggi, caso per caso.
- **B.** Regola unica: in sola lettura non si scrive nulla nel documento. Lo stato della vista (scheda, filtri, opzioni) resta in memoria e
  non si salva; i comandi che cambiano i dati sono spenti.

**Raccomando B.** Una revisione passata non deve risultare modificata, e il caso di «Applica» oggi cambia davvero i dati. Tocca più moduli,
quindi va fatta dopo il 28/10 come un passo unico, con le prove.

## 13. R4, R5 e R10: voci di calcolo a sfavore di sicurezza

Sono voci del registro delle differenze, nelle librerie (Checker), non nell'interfaccia.

| Voce | Cosa | Effetto oggi | In ANTHEA | Raccomando |
| --- | --- | --- | --- | --- |
| R4 | Taglio con il profilo Model Code 2010: θmin fisso a 20° invece di 20° + 10 000 εx (MC2010 §7.3.3.3) | VRd +38,8% nell'esempio S5, non cautelativo | solo con il profilo «Model Code 2010»; NTC non coinvolto | correggere dopo il riscontro del testo, con opzione legacy |
| R5 | SLE con l'analisi non lineare: tensioni calcolate con le leggi di progetto | σc −31% a 80 kNm, non cautelativo | solo se l'utente sceglie «Non lineare»; oggi c'è l'avviso con «?» | leggi di esercizio nell'analisi non lineare |
| R10 | Ancoraggi: fctk,0,05 non limitato alla classe C60/75 (EN 8.4.2(2)) | lb,rqd −11% con C90/105, non cautelativo | solo muri oltre C60/75 | tetto anche nei muri: l'hai già deciso l'8/10, va solo attuato |

**Raccomando di correggerle tutte e tre in un solo passo delle librerie**, dopo l'unione del lavoro di Codex e nel set successivo. Ogni
differenza va registrata nel registro con il tuo ok, poi una full.

## Dopo le 13 voci

Restano circa 210 voci aperte di [f5-15-revisione.md](f5-15-revisione.md): quasi tutte scelte provvisorie «nessuno sui risultati» o «solo
interfaccia», che conservano la vista storica. Propongo:
- accettarle in blocco per gruppo;
- per ogni gruppo ti segnalo prima le poche che cambiano il comportamento;
- tu indichi solo le eccezioni.
