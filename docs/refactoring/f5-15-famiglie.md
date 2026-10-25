# F5.15: le voci aperte per famiglie

Questa scheda prepara le decisioni sulle voci di F5.15 ancora aperte, dopo le 13 del 24/10 ([f5-15-prioritarie.md](f5-15-prioritarie.md)).
Richiesta dell'utente del 25/10: «prepara la scheda delle voci aperte di F5.15 per famiglie, con le raccomandazioni […] L'attuazione dopo
il 28/10». I dettagli e le fonti di ogni voce sono in [f5-15-revisione.md](f5-15-revisione.md); qui ogni voce ha solo la riga che serve per
decidere. Questa scheda non cambia nessuna scelta.

Stato: `main` a 79636c45, con ANTHEA 1.14.0 pubblicata. Le viste storiche sono archiviate dal 23/10 in `supporto/SUPERATI/ui-storica/`,
la Release apre solo la shell nuova e tutti i moduli usano le viste MVVM. Molte domande scritte prima del 23/10 sono cambiate per questo.

**Contenuto.** Sono le 202 righe di [f5-15-revisione.md](f5-15-revisione.md) senza una decisione dell'utente, più 4 righe già fatte da
chiudere (SM9, BD13, G9, G10): 206 righe in 11 famiglie. Il conteggio di 207 voci aperte del piano comprende anche R5 e R10, che restano
aperte nel registro ma sono decise il 24/10, e SM9 e BD13, che sono passi fatti.

**Come rispondere.** Basta una riga, per esempio «come raccomandato, tranne 5A e MU14A». La lettera A è sempre l'opzione presa oggi, B
l'alternativa. Una famiglia si può accettare o cambiare tutta insieme («famiglia 6 tutta A»).

## Riepilogo

| N. | Famiglia | Righe | Raccomando | Lavoro dopo il 28/10 | Cosa tocca |
| ---: | --- | ---: | --- | --- | --- |
| 1 | Superate dai fatti | 18 | **chiudere** | nessuno | nulla |
| 2 | Solo prove e organizzazione del codice | 81 | **A**, conferma in blocco | nessuno | nulla per l'utente |
| 3 | Aspetto: corpi, larghezze, disposizione | 19 | **A**, tranne **V-c B** | poche ore | solo interfaccia |
| 4 | Colori senza un token vicino | 9 | **A**, conferma in blocco | nessuno | solo interfaccia |
| 5 | Dimensione dei dialoghi | 6 | **B**: dialoghi ridimensionabili con il minimo storico | mezza giornata | solo interfaccia |
| 6 | Comportamenti storici conservati (C5, T7) | 28 | **B** per 10 difetti visibili, **A** per gli altri 18 | 1 giorno, con la ricattura di V-a | comportamento; riferimenti congelati, una volta |
| 7 | Comandi durante il lavoro e in sola lettura | 3 | **B** per S6-a/S8-b e MU14, **A** per S3-b | con T6 | comportamento |
| 8 | Schede interne e stato della vista | 4 | **B** per S4-c/S7-b, **A** per gli altri | con SH49 | comportamento |
| 9 | Shell e funzioni già in uso nella 1.14.0 | 23 | **A**, conferma; **G11 B** | poche ore | comportamento già in uso |
| 10 | Taglio sul thread dell'interfaccia | 1 | **B**, con D-2 e D-5 | voce di infrastruttura | nulla sui risultati |
| 11 | Librerie: R6–R9, R11–R14, R16–R21 | 14 | **correggere o dichiarare**, voce per voce | con il set di R4, R5 e R10 | risultati di calcolo |

**Lavoro stimato.** Con le raccomandazioni, oltre alle voci già decise il 24/10 (V-a, BD4, SH49 ridotta, T6), le famiglie 3, 5, 6, 7, 8 e 9
aggiungono circa 2–3 giorni. Le ricatture dei riferimenti congelati (V-a e famiglia 6) si fanno in un solo commit motivato. La famiglia 10
va con la voce di infrastruttura dei calcoli fuori dal limitatore; la famiglia 11 con il set di librerie dopo l'unione di Codex.

## 1. Superate dai fatti: chiudere

Domande che non si pongono più: le viste storiche sono archiviate, F5.14 è fatta, il passaggio della Release è fatto. Raccomando di
chiuderle senza lavoro.

| Voce | Perché è superata |
| --- | --- |
| C3 | `SectionActionsViewModel` è già in Presentation (S2). |
| C10, S1-d | Sottopassi e vista unica fatti; la Release usa la vista nuova dal 23/10. |
| CA-P2 | La `ConcreteWorkspace` storica è archiviata con il passaggio. |
| CA-P3 | Le prove storiche restano con la vista archiviata; l'import è provato da `ui/smoke-section-actions`. |
| S2a-1 | Copertura dei wizard fatta in S1b. |
| S7-a | «Da confermare all'unione»: l'unione è fatta, tracce di S3 uguali. |
| SH41 | Nessun modulo è più storico nella shell. |
| SH47 | Nessun foglio storico nella shell: F1 e «?» dei fogli MVVM aprono la scheda Wiki. |
| S11 (T11) | `MessageBoxHook` serviva alle viste storiche, archiviate. |
| Q14 | F5.14 fatta con la (a): `src/ANTHEA.Desktop`, `ANTHEA.exe`, namespace `X.Desktop` invariato. |
| SM16 | `BridgeInformation.cs` è archiviato: i testi di «Info modello…» stanno solo nella vista XAML. |
| §5.4-3 | La parte di F5.6 è corretta con F5-62 (scelte degli strati nei temi scuri). |
| S17 | Corretto con `BasedOn`, anche nel palo verticale. |
| T5 | Fatto: ViewModel unico del trasferimento e guida comune in `Modules/Shared`. |
| SM9, BD13 | Contrasto a 96 DPI fatto il 24/10 con la G2. |
| G9, G10 | Difetti delle prove, corretti il 24/10 (5844a146, bd8807ca). |

## 2. Solo prove e organizzazione del codice: conferma in blocco

Nessun effetto per l'utente: come sono scritte le prove, dove sta il codice, come si confrontano le tracce. Raccomando **A** per tutte.
Sono quasi tutte scelte che le prove della 1.14.0 (full PASS 155) già usano; cambiarle vorrebbe dire riscrivere prove senza beneficio
per l'utente.

- **Regole comuni:** O9 (fixture del palo orizzontale da 4 MB in JSON indentato); D14 (banco `--interactive` solo nella build UiTests).
- **Palificata:** D3 (`PreparePileGroup` invariata).
- **Materiali:** M18 (stato dei campi del catalogo nel `FieldForm`); M19 (report del foglio materiale come l'ospite MVVM, percorso non
  usato).
- **Stratigrafia:** S7 (`Multiline` additiva di `FieldSpec`).
- **Risposta elastica:** §6.4-4 («Esporta PNG…» con scrittura atomica); §6.4-6 (cataloghi passati al ViewModel a ogni tasto).
- **Muri:** MU10 (finestra dei valori e distinta come dialoghi); MU19 (lettura delle tracce dal ViewModel).
- **Fondazioni:** FN5 (componente esplicito delle scelte del metodo).
- **Sezione c.a., punto di controllo:** C1 (coordinatore del calcolo in Presentation); C4 (verificatore c.a. del palo elastico come
  dialogo di `IDialogService`, ora sul ViewModel della sezione); C7 (tracce lunghe nello stadio `ui`); C8 (disegni WPF dal ViewModel);
  C9 (trefoli solo nelle tracce).
- **Sezione c.a., sottopassi:** S1-a, S1-c, S1-e, S2-a/S3-a, S2-b, S2-c, S2-d, S1b-a, S1b-b, S1b-c, S1b-d, S3-c, S3-d, S4-a, S4-d,
  S4-e, S5-a, S5-b, S6-b, S8-a, S9-b, S9-c: finestre non modali chieste alla vista, chiavi di stato, scrittura degli stati dei domini,
  comandi Excel, cartelle e archivi delle tracce, zoom e camera nella vista, legenda come dato, staffe e sintesi del taglio, temi dei
  dettagli, ascolto di `Modified`, report breve con `IModuleShortReport`, foglio senza disegnatore solo nelle prove.
- **Sezione c.a., vista XAML:** SV-a, SV-b, SV-c, SV-d, SV-f, V-d, V-h: schede SLE senza intestazioni visibili, «PNG» ed «Espandi» nella
  vista, «Proprietà / report…» non modale, gruppi per chiave, ospite del report breve, etichette delle scelte, controlli disposti fuori
  dall'albero nelle prove.
- **Sezione composta:** SM1 (calcolo sullo sfondo dopo 500 ms); SM2 (riferimento F3 da 104 a 111); SM4 (riuso delle schede del
  riepilogo); SM6 (prove nello stadio `fast`); SM8 (baseline F4 dei disegni a 3221); SM10 (celle scritte a ogni tasto, risultato
  uguale); SM11 (solo la pagina mostrata nell'albero visuale); SM12 (casella «CLS ×» nel code-behind); SM14 (tabelle dei risultati
  riusate).
- **Bridge Design:** BD1 (calcolo sullo sfondo dopo 450 ms); BD2 (riferimento F3 da 111 a 119); BD5 (editor dei prezzari in
  Presentation); BD7 (`dati` nel JSON esportato); BD8 («Stampa scheda» della vista); BD9 (chiave `scene` come modifica dei dati); BD11
  (prove nello stadio `fast`); BD12 (baseline F4 dei disegni); BD17 (file del prezzario dal servizio dei file); BD18 (pagina di stampa
  con i colori storici); BD19 (blocchi e limiti come dizionari); BD20 (cursore senza legame a due vie).
- **Shell:** SH39 (elenco dei moduli che seguono la finestra in Presentation); SH51 (chiusura della generazione del report come
  `MainWindow`).
- **Parte (d) di F5.13:** D-1 (liste delle sezioni dei report rese immutabili con la revisione dei report); D-3 (istanze condivise delle
  librerie, seguito per le librerie); D-4 (istante dei prezzi fissati escluso dal confronto).
- **Suite UI e passaggi:** S1, S3, S4, S5 (suite UI); P1, P2; CA-P1 (prove storiche della sezione c.a. riscritte sugli effetti
  osservabili); G1 (`SheetImplementation.Historic` resta come valore dei moduli non elencati); G5 (fuoco della tastiera nelle prove di
  Bridge Design); G7 (conteggio delle occorrenze della Chiara per cattura).

## 3. Aspetto: corpi, larghezze, disposizione

Le viste nuove usano i corpi e le misure del sistema dei token al posto dei valori sparsi delle viste storiche. Le differenze sono di uno
o due punti e sono già nella 1.14.0. Raccomando **A** per tutte, tranne **V-c B**.

| Voce | Oggi (A) | Alternativa (B) |
| --- | --- | --- |
| M15 | titoli del calcestruzzo a 16 (`Font.Heading`) | 14, come l'acciaio |
| M20 | rotella del mouse sulle scelte come WPF | non indicata |
| §5.3-2 | corpi del palo verticale 14, 16, 11 | non indicata |
| S13 | testo dei campi allineato a destra | a sinistra, come la storica |
| S14 | suggerimento di φ′ dall'etichetta, non dall'aiuto della viscosità | suggerimenti per modulo |
| S15 | nota dei terreni misti in due form | riquadro giallo con testo diverso |
| §5.4-1 | editor delle opzioni avanzate a 82 px | 150 px storici |
| §5.4-2 | titoli delle tabelle 14.5 e 14.6 a 14 | non indicata |
| §6.4-1 | titoli della risposta elastica a 16 | non indicata |
| V-b | pulsanti e scelte della sezione c.a. alla loro larghezza | larghi quanto la colonna |
| **V-c** | intestazioni della tabella del taglio tagliate a 1600 px | **larghezze dal contenuto** |
| SM7 | suggerimenti degli editor della sezione composta dalla vista | `FieldSpec.ToolTip` (cambia l'etichetta) |
| SM13 | «Calcola curva» spento un istante dopo il calcolo, non visibile | non indicata |
| BD6 | parametri con il cursore come elementi espliciti | estendere `FieldSpec` |
| BD14 | metriche di Bridge Design a 22 per non tagliarle | 30 |
| BD16 | misure dei riquadri riconosciute dalle colonne | misure come dati del ViewModel |
| BD21 | nessuna barra di avanzamento in Bridge Design, come la storica | barra indeterminata |
| SH44 | indicatore del trascinamento con una linea e un contorno | fondo storico |
| SH45 | nomi dell'albero sui corpi dei token (22, 16, 14) | corpi storici (20, 17, 15) |

**V-c, raccomando B.** Le intestazioni tagliate sono un difetto visibile a 1600 px, anche nella vista storica. Le larghezze dal contenuto
lo tolgono senza toccare dati o risultati. È un cambio piccolo, da fare dopo il 28/10 con una prova a 1600 px.

## 4. Colori senza un token vicino

I colori storici fissi diventano il token più vicino, che segue l'aspetto (Chiara, Scuro, Molto scuro) e la prova del contrasto. Le
differenze sono già nella 1.14.0. Raccomando **A** per tutte: un colore fisso fuori dai token non seguirebbe i temi scuri.

| Voce | Oggi (A) | Alternativa (B) |
| --- | --- | --- |
| M16 | valori in sola lettura del calcestruzzo `Token.Calculated` | `Token.Field.Background` |
| M17 | stato dell'acciaio con due token nuovi, `Status.Coherent` e `Status.Caution` | accorpare a token esistenti, colore visibilmente diverso |
| §5.3-1 | esito del palo verticale e orizzontale con `Status.Ok` e `Status.Fail` | token nuovi con i colori storici |
| §6.4-2 | toni del pannello «Sezione e verifiche» sui token di stato | non indicata |
| §6.4-3 | colori dei tratti con i token `Segment.1–6` | non indicata |
| MU15 | righe alterne dei muri `Token.Surface` | `Token.PaperAlt` |
| MU16 | «DarkGoldenrod» dei muri come `Status.Caution` | token nuovo con il colore storico |
| BD15 | colori di Bridge Design sui token più vicini | token nuovi per due colori |
| S2 (suite UI) | grigio del γsat automatico `Token.Disabled` | non indicata |

## 5. Dimensione dei dialoghi

Oggi i dialoghi si aprono con la misura delle finestre storiche e `WpfDialogService` li adatta al contenuto, senza ridimensionamento. Le
finestre storiche di §5.3-3 e MU17 invece si potevano ridimensionare, con una misura minima.

**Raccomando B per tutte e sei, come regola unica di `WpfDialogService`:** il dialogo si apre con la misura storica e si ridimensiona,
con una misura minima: quella storica per §5.3-3 (720×520) e MU17 (780×530), quella dell'alternativa per S16 (850×600), altrimenti
quella del contenuto. Riporta il comportamento storico dove era migliore e serve sugli schermi piccoli (SH40). Circa mezza giornata con
le prove.

| Voce | Dialoghi |
| --- | --- |
| §5.3-3 | «Copia in…», «Copia da…», «Terreno · invio e riutilizzo» del palo verticale |
| S16 | importazione IA della stratigrafia |
| §6.4-5 | dialoghi della risposta elastica (NTC §7.2.5, «Info sezioni», proposta, divisione, dimensionamento, confronto, tavola) |
| MU17 | valori dei muri |
| SV-e | «Nuovo materiale …», wizard, «Sollecitazioni…» della sezione c.a. |
| BD22 | prezzario di Bridge Design |

## 6. Comportamenti storici conservati (C5, T7)

Le viste nuove conservano i difetti delle viste storiche, perché le tracce restassero identiche (C5 e T7). La regola diceva: «diventano
voci del registro da decidere al passaggio». Il passaggio è fatto e le viste storiche sono archiviate. Le tracce restano come
riferimenti congelati delle prove `ui/check-*-view`.

**Raccomando B per i 10 difetti che l'utente vede, A per gli altri 18.** Le correzioni cambiano poche righe delle tracce: si ricatturano
una volta, nello stesso commit motivato della ricattura di V-a, con una voce del registro per ognuna. Circa un giorno.

### 6.1 Da correggere (B)

| Voce | Difetto visibile | Correzione |
| --- | --- | --- |
| §2-4 | Nel dominio 2D SLV compare il messaggio grezzo «Value cannot be null. (Parameter 'failureDomain')» | messaggio di ANTHEA |
| C5·S4 | Dopo una modifica di n o φ le altre famiglie mostrano «17.607108191890596» invece di «17,61»; una famiglia nascosta tiene la legenda vecchia | valori formattati, legenda azzerata |
| S4-b | Colonne SLE nascoste scelte per indice: con le sollecitazioni uniche si nascondono le colonne sbagliate | colonne scelte per chiave |
| C5·S7, S7-c | La mappa σ/ε tiene la sezione precedente; dopo un aggiornamento abbandonato restano messaggi vecchi | messaggio della sezione del pannello e azzeramento |
| S8-c | Dopo «Applica» lo stato dice «Configurazione applicata…» e non che la ricerca va ripetuta | dirlo nello stato |
| E4 | Archivio da migrare: lo stato dice «in coda» con la barra che gira, ma nessun calcolo è in coda | stato e barra coerenti |
| E5 | Quando la quota scelta si perde, la sezione mostra ancora didascalia, staffa e assi della quota vecchia | azzerarli |
| SM5 | Punto 3: una situazione scelta durante un calcolo mostra risultati e disegno della precedente | aggiornare alla fine del calcolo |
| BD10 | Punto 3: il bordo rosso resta dopo «Annulla»; punto 5: «Suggerisci campate» scrive in sola lettura | bordo azzerato; il punto 5 con T6 |

### 6.2 Da conservare (A)

Effetti invisibili all'utente (un `Modified` in più, un ordine di scrittura) o casi che ANTHEA non produce:
- S6 (T7), O4 (T7), MU18, C5·S1, C5·S3, C5·S5, C5·S6, C5·S9: `Modified` doppi, ricalcoli doppi, gruppi riportati allo stato iniziale.
  Nessun dato cambia; S6 segna il foglio modificato senza cambiamenti, un caso raro.
- §2-3: N = +1000 solo per documenti scritti a mano senza `dominio2d`.
- §2-6, §2-7, §2-8, §2-9: chiavi di visibilità all'apertura, cache dell'esportazione, blocco delle barre per coordinate, limiti della
  ricerca delle armature. Comportamenti interni o limiti voluti della ricerca.
- C5·S2, C5·S1b, C5·S8, S5-d: comandi della tabella del taglio, esempi e wizard (CA-WIZARD-01, già nel registro), avanzamenti della
  ricerca, stato del taglio prima di mostrare la scheda.
- SM5 e BD10, gli altri punti: pannelli vuoti prima del primo risultato, gruppo della torsione, «Ripristina A», ricerca senza risultato.
- C5: la regola stessa. Con questa scheda ogni comportamento ha la sua decisione.

## 7. Comandi durante il lavoro e in sola lettura

Vanno con la regola unica T6 decisa il 24/10: in sola lettura non si scrive nel documento e lo stato della vista resta in memoria.

| Voce | Oggi (A) | Raccomando | Perché |
| --- | --- | --- | --- |
| S3-b | i pannelli dei domini ammettono in sola lettura le scelte di vista (`AllowWhenReadOnly`) | **A** | è proprio lo «stato della vista in memoria» di T6 |
| S6-a, S8-b | i pulsanti «Modifica … nel pannello di controllo →» sono spenti in sola lettura, anche se cambiano solo la scheda | **B**: attivi | con T6 cambiare scheda non scrive nulla; oggi l'utente non può navigare una revisione |
| MU14 | «Ricalcola», «Calcola globale», «Calcola armature» e «Valori di calcolo…» restano attivi durante il lavoro; un secondo clic non è rifiutato | **B**: spenti durante il lavoro | è la convenzione di `GuardedCommands` negli altri moduli ed evita due lavori sovrapposti |

## 8. Schede interne e stato della vista

Vanno con SH49 ridotta, decisa il 24/10: la sotto-scheda scelta resta quando la scheda torna a schermo.

| Voce | Oggi (A) | Raccomando |
| --- | --- | --- |
| S4-c, S7-b | le schede interne (dettagli SLE, scheda del dominio) valgono −1 finché non sono mostrate | **B**: la prima scheda fin dall'apertura. Con SH49 l'indice va conservato, e −1 non è una scheda. |
| SM15 | le schede delle proprietà della sezione composta si rigenerano a ogni modifica; la scheda scelta resta | **A** |
| SH36 | la pagina intera è uno stato della finestra e non si ricorda fra le sessioni | **A** |
| SH48 | l'albero chiuso dall'utente resta chiuso anche sul riepilogo | **A** |

## 9. Shell e funzioni già in uso nella 1.14.0

Comportamenti nuovi o diversi dalla vista storica che la 1.14.0 ha già distribuito e che le note di rilascio descrivono. Raccomando la
**conferma (A)** di tutti, tranne **G11 B**.

- **Shell:**
  - SH1, dati condivisi confermati alla perdita del fuoco;
  - SH2, finestre staccate fatte da noi;
  - SH3, nessun annulla per campo;
  - SH4, Wiki in una scheda;
  - SH33–SH35 e SH37, pagina intera e menu dei nodi;
  - SH38, badge di coerenza che apre il confronto;
  - SH40, finestre piccole che scorrono;
  - SH42, avviso nella scheda ricaricata;
  - SH43, clic singolo sul modello;
  - SH46, dialoghi della shell;
  - SH50, report della sezione dall'albero;
  - SH52–SH54, schede riaperte dopo un'anteprima, foglio con il fuoco, pagina all'attivazione.
- **Moduli:**
  - M9, nessun comando «Reologia» nella scheda del calcestruzzo;
  - §5.3-4, «Modello e dati comuni…» apre la Wiki;
  - FN9, aiuto per campo solo dove c'era;
  - SM3, vista staccata della sezione composta.
- **W-idx: A adesso, B con la revisione della Wiki.** Togliere i percorsi delle figure dall'indice cambia `search.json` e i risultati di
  ricerca: va fatto con la ristrutturazione della Wiki, non da solo.
- **G11: raccomando B.** Nella barra dei progetti della shell un «?» che apre l'articolo della Wiki «Progetti e gestione del lavoro», al
  posto del pulsante «Guida progetti» di `MainWindow`. Chi cercava il pulsante oggi non trova l'aiuto da quella pagina. È un cambio
  piccolo.

## 10. Taglio sul thread dell'interfaccia

| Voce | Oggi (A) | Raccomando |
| --- | --- | --- |
| C2, S5-c | il taglio della sezione c.a. si calcola sul thread dell'interfaccia, come la vista storica | **B**: sullo sfondo, nella stessa voce di infrastruttura di D-2 e D-5 (calcoli fuori dal limitatore) |

Con molte azioni il taglio blocca la finestra per qualche istante. Portarlo sullo sfondo non cambia i risultati, e la prova `--paralleli`
che serve a D-2 e D-5 copre anche questo caso. Non blocca F5.15.

## 11. Librerie: R6–R9, R11–R14, R16–R21

Voci del registro delle differenze, nelle librerie GPC (Checker e Model), non nell'interfaccia. Raccomando di trattarle tutte nel set di
librerie successivo all'unione di Codex, insieme a R4, R5 e R10 già decise. Ogni correzione ha l'opzione legacy, una voce del registro
con il tuo ok e una full. L'ordine viene dall'effetto in ANTHEA.

| Voce | Cosa | Segno | In ANTHEA | Raccomando | Priorità |
| --- | --- | --- | --- | --- | --- |
| R9 | taglio: VEd ≤ 0,5 bw d ν fcd (EN 6.2.2(6)) non controllato | non cautelativo vicino agli appoggi | da verificare | **aggiungere il controllo**; dichiarare ν1 e le staffe di classe A | alta |
| R18 | M–χ: ramo incrudente dell'acciaio riferito a fyk invece di fyd | non cautelativo, +2 % in εud per B450C | da verificare; tocca riferimenti congelati | **correggere** in Model, con la ricattura | alta |
| R12 | pilastri NTC: interasse delle barre misurato attraverso il nucleo | cautelativo: esito non soddisfatto indebito | sì, esiti dei pilastri | **misura lungo il perimetro** | media |
| R13 | riduzione 0,6 delle staffe nelle sovrapposizioni EC anche con Ømax ≤ 14 mm, più tre interpretazioni | cautelativo | sì | **correggere la riduzione**, dichiarare le altre tre | media |
| R14 | 15Ø delle barre compresse come conferma; definizioni del M–χ; citazioni | 15Ø da stabilire | sì | **correggere le citazioni e dichiarare il M–χ** subito; 15Ø come controllo dopo il riscontro | media |
| R6 | DIN: k5 dei trefoli 0,75 invece di 0,65, da una fonte secondaria | non cautelativo se confermato | non indicato | **riscontrare l'NA**, poi correggere | media |
| R8 | fessurazione: MC2010 senza εcs, coefficiente DIN di hc,ef, wmax EN per XD3 | da stabilire | da verificare | **riscontrare sui testi**, poi decidere voce per voce | media |
| R7 | interpretazioni cautelative o dichiarate del calcestruzzo | cautelative, tranne F-4 | già dichiarate nelle pagine dei metodi | **dichiararle anche nella guida teorica**; stabilire il segno di F-4 | bassa |
| R19 | citazioni della torsione NTC 2018 e DS sbagliate | nessun effetto numerico | solo testo | **correggere il testo** | bassa, ma facile |
| R21 | DIN: hc,eff con `NominalCover` anche con `CoverOverride` | da stabilire | solo DIN con copriferro assegnato | **correggere**, opzione legacy | bassa |
| R11 | NTC: Cmin di default senza la classe minima | non cautelativo per altri utenti | nessuno: ANTHEA passa sempre la classe | **correggere il default** | bassa |
| R16 | DS: νv del taglio invece di νt nella torsione | non cautelativo | nessuno: in ANTHEA la torsione è solo NTC | **correggere**, opzione legacy | bassa |
| R17 | classe indicativa di XC3: C25/30 invece di C30/37 | non cautelativo | nessuno: ANTHEA usa la UNI 11104 | **correggere dopo il riscontro** | bassa |
| R20 | DS, travi: ρw,min non implementato | nessun esito errato, esito in sospeso | solo DS | **implementare** quando serve il DS delle travi | bassa |

Le voci a priorità bassa senza effetto in ANTHEA (R11, R16, R17, R20) si possono anche lasciare alle librerie come lavoro proprio, fuori
da F5.15: dimmelo se preferisci così.
