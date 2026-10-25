# F5.15 — Revisione delle scelte «da decidere» e delle voci R4–R21

Questo documento raccoglie in un solo posto quello che l'utente rivede in F5.15 ([piano](piano.md), voce «F5.15 [CP]»):
- le scelte provvisorie degli agenti e del coordinatore sulle voci «da decidere» di F5. È la decisione dell'utente del 17/10: «dove
  diceva "da decidere" a fine refactoring rivediamo tutte le scelte» ([decisioni](decisioni.md#17-ottobre-tarda-mattina--le-scelte-da-decidere-si-rivedono-a-fine-refactoring));
- le voci sfavorevoli o aperte del [registro delle differenze](registro-differenze.json): R4–R14 e R16–R21. R15 è corretta.

Le voci ancora aperte sono raccolte per famiglie, con le raccomandazioni, nella [scheda del 25/10](f5-15-famiglie.md).

Stato al 23/10/2026, su `refactoring/f5` f4af0ae9: F5 è unita tranne le parti (f) e (g) di F5.13 e F5.14. Questo documento non cambia
nessuna scelta. Le scelte provvisorie valgono finché l'utente non risponde.

**Come rispondere.** Come nella [pagina dei punti di controllo](f5-punti-di-controllo.md): basta elencare i codici accettati e quelli da
cambiare. Le voci della stessa famiglia si possono decidere insieme ([sezione 15.5](#155-voci-da-decidere-insieme)).

**Colonne delle tabelle.**
- **Opzione presa**: la scelta provvisoria in vigore. Se l'utente ha già risposto c'è scritto «Decisa», con la data.
- **Alternative**: quelle scritte nel documento d'origine. «Non indicata» vuol dire che il documento non ne dà.
- **Impatto** dell'opzione presa rispetto alla vista storica (per la shell, rispetto a `MainWindow`):
  - «nessuno sui risultati»: prove, codice, organizzazione; nulla di visibile per l'utente;
  - «solo interfaccia»: aspetto o disposizione (corpi, colori, misure), senza cambiare dati, risultati o comandi;
  - «comportamento diverso dalla vista storica»: l'utente vede un comportamento diverso;
  - «tocca riferimenti congelati»: fixture, tracce o disegni congelati da ricatturare;
  - «risultati di calcolo»: solo nel registro, cambiano valori o esiti delle verifiche.

  Fra parentesi l'impatto dell'alternativa, quando è diverso.
- **Fonte**: la sezione del documento d'origine. «Registro F5-nn» è la voce del [registro delle differenze](registro-differenze.json):
  le voci F5-03…F5-59 sono del 23/10 (`nota_f5_23_10` del registro).

Il modulo è nel titolo del gruppo. I codici sono quelli dei documenti d'origine e in parte si ripetono, quindi conta il gruppo:
- S1–S17 sono della stratigrafia; S1–S9 e S1-a…S9-c dei sottopassi della sezione c.a.; S1–S6 delle suite UI;
- V1–V12 sono del palo verticale, V-a…V-h della vista della sezione c.a.;
- E1–E10 sono del palo elastico.

Le voci senza codice hanno il numero della sezione d'origine, per esempio «§5.3-1».

Non sono nell'elenco:
- le voci con una proposta accettata dall'utente il 17/10 mattina («accetto tutte le proposte, continua col piano»): le regole T1–T12
  con proposta e le domande dei moduli con proposta;
- i passi da fare che non chiedono una scelta: il contrasto a 96 DPI (T10, MU13, FN11). SM9 e BD13 restano nei loro gruppi, perché i
  documenti li elencano fra le domande.

## Da guardare per primi

**Decise il 24/10.** L'utente ha accettato le raccomandazioni della [scheda delle 13 voci](f5-15-prioritarie.md): «accetto le 13
raccomandazioni della scheda». Le righe interessate (21) riportano «Decisa 24/10»; quelle da attuare lo dicono.

**Decise il 25/10.** L'utente ha accettato tutte le raccomandazioni della [scheda per famiglie](f5-15-famiglie.md): «Come raccomandato in
tutte le famiglie della scheda f5-15-famiglie.md. Registra le decisioni; l'attuazione dopo il 28/10». Le 202 righe ancora aperte
riportano «Decisa 25/10» o «Chiusa 25/10» con la famiglia; quelle da attuare lo dicono. Non resta nessuna voce aperta.

Voci la cui opzione cambia il comportamento rispetto alla vista storica, o tocca riferimenti congelati, o che gli agenti hanno
segnalato come le più bisognose dell'utente.

1. **V-a e separatore decimale** ([15.1](#151-separatore-decimale-e-v-a), [15.2](#152-proposta-impostazione-dei-separatori)). Le liste dei
   disegni e le tracce congelate della sezione c.a. dipendono dal separatore decimale della macchina che le ha catturate. Il 22/10
   l'utente ha proposto un'impostazione di ANTHEA per i separatori. Tocca riferimenti congelati.
2. **V-g** ([10.4](#104-vista-xaml)). La traccia storica congelata ha scritto un campo che l'utente non può scrivere. L'alternativa è
   ricatturarla con i form disposti. Tocca riferimenti congelati.
3. **V-e** ([10.4](#104-vista-xaml)). Una riga di una traccia congelata dipende dai tempi: `ui/check-sezione-historic` cade una volta su
   tre con la macchina carica. L'alternativa è ricatturare la traccia con l'attesa dell'ispezione. Tocca riferimenti congelati.
4. **D-5 e D-2** ([13.5](#135-parte-d-d-1d-5)). La sezione c.a. e la risposta elastica calcolano fuori dal limitatore
   dell'applicazione. Con molte schede aperte i calcoli si sommano a quelli degli altri moduli (rischio R6 della proposta).
5. **BD3** ([12](#12-f512--bridge-design-e-prezzari)). Durante il calcolo la finestra vede il foglio «occupato» e chiede di attendere. Con
   la vista storica il foglio non era mai occupato e il report usciva con il risultato mostrato. Comportamento diverso.
6. **BD4** ([12](#12-f512--bridge-design-e-prezzari)). In una revisione in sola lettura il prezzario non si apre più. Con la vista
   storica si apriva e «Applica prezzario» scriveva nella revisione. Comportamento diverso.
7. **SH5/SH18, SH6, SH12, SH22, SH28 e SH17** ([13.1](#131-parti-a-e-b-sh1sh22), [13.2](#132-bozza-della-disposizione-sh23sh32)). Sono
   decise il 22/10 con la bozza, ma conviene una conferma:
   - le prime cinque cambiano il comportamento rispetto a `MainWindow`: schede chiuse dall'annulla con un avviso, anteprima delle
     revisioni con più schede, «Nuovo …», pagina «Progetti» ed esempi senza domanda di salvataggio;
   - SH17 conserva il comportamento di oggi, ma insieme a SH49 decide se lo stato della vista si perde.
8. **SH49** ([13.4](#134-parte-e-sh48sh54)). Quando una scheda torna a schermo, lo stato tenuto solo dalla vista si perde: per esempio la
   scheda dei risultati del palo verticale torna alla prima. `MainWindow` teneva l'editor. Comportamento diverso.
9. **S1-b e V-f** ([10.3](#103-domande-dei-sottopassi-s1s9)). «Calcola» dell'ospite MVVM conferma il foglio prima di calcolare e scrive
   `visible` nelle righe delle azioni. L'ospite storico della sezione c.a. non lo fa.
10. **S9-a** ([10.3](#103-domande-dei-sottopassi-s1s9)). Le figure del report della sezione c.a. si disegnano adattate, senza lo zoom e lo
    spostamento che l'utente ha fatto sull'anteprima. È come V8 del palo verticale, già accettata. Comportamento diverso.
11. **Muri, kh e kv del capitolo autonomo** ([8](#8-f59--muri-di-sostegno)). È l'unica voce del 17/10 rinviata perché «riguarda il
    calcolo». Non c'è un'opzione presa.
12. **T6 e la famiglia «sola lettura che scrive»** ([1](#1-regole-comuni-dei-moduli-1710), [10.2](#102-comportamenti-storici-c5)). Serve una
    regola unica per tutti i moduli. Oggi una revisione precedente risulta «modificata» senza cambiamenti dei dati, mentre MU3, MU4 e
    BD4 rifiutano già le scritture. Nella sezione c.a. «Esporta Excel» in sola lettura lascia ancora cambiare i dati. FN4 è già diversa
    dalla vista storica.
13. **R4 e R5** ([14](#14-registro-delle-differenze-r4r21)). Sono a sfavore di sicurezza, con priorità alta, e toccano ANTHEA: il profilo
    Model Code 2010 e l'analisi SLE non lineare. Insieme a loro **R10**: decisa l'8/10 per i muri, va attuata in F5.15.

## Conteggio

| Gruppo | Voci | Decise fino al 24/10 | Decise il 25/10 | Aperte |
| --- | ---: | ---: | ---: | ---: |
| 1. Regole comuni dei moduli (17/10) | 6 | 2 | 4 | 0 |
| 2. F5.4 palificata orizzontale | 1 | 0 | 1 | 0 |
| 3. F5.5 materiali | 7 | 0 | 7 | 0 |
| 4. F5.6 palo e micropalo verticale | 4 | 0 | 4 | 0 |
| 5. F5.6b stratigrafia | 7 | 0 | 7 | 0 |
| 6. F5.7 palo e micropalo orizzontale | 4 | 0 | 4 | 0 |
| 7. F5.8 risposta elastica e armature | 8 | 0 | 8 | 0 |
| 8. F5.9 muri di sostegno | 8 | 1 | 7 | 0 |
| 9. F5.9b fondazioni superficiali | 2 | 0 | 2 | 0 |
| 10. F5.10 sezione c.a. | 73 | 7 | 66 | 0 |
| 11. F5.11 sezione composta da ponte | 16 | 0 | 16 | 0 |
| 12. F5.12 Bridge Design e prezzari | 22 | 2 | 20 | 0 |
| 13. F5.13 shell | 59 | 31 | 28 | 0 |
| 14. Registro delle differenze R4–R21 | 17 | 3 | 14 | 0 |
| 15. Voci trasversali | 27 | 8 | 19 | 0 |
| **Totale** | **261** | **54** | **207** | **0** |

Note sul conteggio:
- La colonna del 25/10 riprende le voci aperte del 24/10. Comprende SM9 e BD13, già fatte, e R5 e R10, decise il 24/10 e da attuare con
  il set di librerie: per questo è maggiore delle 202 righe segnate il 25/10.
- Una riga è una voce. Alcune righe uniscono codici che pongono la stessa domanda, per esempio «C2, S5-c» o «S6-a, S8-b».
- Nel gruppo 10: 9 voci del punto di controllo, 19 di comportamenti storici, 33 dei sottopassi, 12 della vista XAML.
- Le righe «C5·S1…C5·S9» raggruppano i comportamenti storici di un sottopasso (da 1 a 4 per riga, 27 in tutto).
- In «Già decise» ci sono le voci con una risposta dell'utente su quella scelta: SH5–SH32, la S6 delle suite UI, P3 e i passaggi della
  Release. R10 è decisa solo per i muri e resta fra le aperte, insieme a R5. Il dubbio su SH1–SH4 è nella [sezione 13.1](#131-parti-a-e-b-sh1sh22).
- SM9 e BD13 (contrasto a 96 DPI) sono passi da fare, non scelte, ma sono contati. Sono fatti dal 24/10, con la G2 (terza tappa di F5.13 g).

## Voci già decise dall'utente

| Data | Voce | Parole dell'utente | Esito |
| --- | --- | --- | --- |
| 8/10 | R10, muri | «ok» alla proposta F2.8-U3 del progetto F2.8 rivisto | Tetto C60/75 di fctk,0,05 anche nei muri, da attuare in F5.15. Resta aperta la parte della libreria (gruppo 14). |
| 8/10 | R5 | «ad ora lascialo così. ritiriamo fuori questo argomento a fine refactoring; poi: metti il rimando alla guida accanto al tipo di analisi. l'avviso mettilo» | Avviso e «?» fatti; il calcolo resta con i legami di progetto. R5 resta da decidere (gruppo 14). |
| 15/10 | Rinvio di R16–R21 a F5.15 | «E1–E8 come nel foglio» | Ratificato il rinvio esteso dal coordinatore il 7/10 (E4). |
| 16/10 | Q1–Q5, Q7, Q11, Q12, Q15, Q16, Q19, Q20 della [proposta](f5-proposta-architettura.md#10-domande-per-lutente-checkpoint-f50); nel pomeriggio Q6 | «sisi. pagine dei metodi in coda. fai un merge di tutto in main per partire con F5», poi le risposte a scelta multipla | Tutte sulle raccomandazioni. Q8 (a) accettata il 17/10 con D1. Q9, Q10, Q13 e Q18 diventano SH1–SH4; Q17 diventa C3 e BD5; Q14 resta aperta (gruppo 15). |
| 17/10 mattina | Voci con proposta della pagina dei punti di controllo | «accetto tutte le proposte, continua col piano» | Non sono in questo elenco. Le voci senza proposta hanno la scelta del coordinatore «da ratificare» (gruppi 1–10). |
| 21/10 sera | P3, selettore del metodo del palo orizzontale | «tieni 120 px come le altre scelte» | [Decisioni, 21/10](decisioni.md#21-ottobre-sera--selettore-del-metodo-del-palo-orizzontale-p3) (gruppo 15). |
| 22/10 | S6 delle suite UI, antivirus e immagini | «1 a» | `DocumentFile.Write` copia anche le immagini invece di rinominarle, merge 78ccba8e ([ripresa](f5-ripresa.md), passo 3) (gruppo 15). |
| 22/10 | Passaggi della Release dei moduli F5 | «i test per il passaggio alla release li facciamo a fine refactoring» | I passaggi e le loro prove si fanno a fine refactoring ([f5-10 §19](f5-10-sezione.md#19-s9-fatto-2210)) (gruppo 15). |
| 22/10 sera | SH5–SH32, bozza della shell | «le 10 immagini vanno bene. dall'albero si possono aprire a pagina intera o staccare dalla finestra? se si, ok. altrimenti va implementato» | SH5–SH32 con le opzioni della bozza; aggiunte l'apertura dall'albero in una nuova finestra e a pagina intera, con le scelte nuove SH33–SH37 ([f5-13 §9.2](f5-13-shell.md#92-scelte-per-lutente)) (gruppo 13). |

## 1. Regole comuni dei moduli (17/10)

Sono le voci senza proposta della [pagina dei punti di controllo](f5-punti-di-controllo.md). La scelta è del coordinatore, «da
ratificare» ([decisioni, 17/10 mattina](decisioni.md#17-ottobre-mattina--punti-di-controllo-f54f510-parte-a)), con il criterio prudente:
si conserva il comportamento di oggi e non si aggiungono funzioni nuove. Le voci T7 di un solo modulo (S6, O4, E4, E5) sono nei gruppi
dei moduli.

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| T5 | Come e quando si fanno i dialoghi condivisi «Invia / carica terreno…» e «Modello e dati comuni…»? | **Chiusa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 1): superata dai fatti. Prima: Un ViewModel unico del trasferimento su `IDialogService` e la guida comune con `IWikiNavigator`. Scritti con la prima vista che li usa (F5.6) e spostati in `Modules/Shared`. Fatto. | Un passo dedicato insieme a F5.6, F5.7, F5.9 e F5.9b, oppure con F5.6b | solo interfaccia (dialoghi a dimensione fissa, §5.3-3 del gruppo 4) | [T5](f5-punti-di-controllo.md#t5-dialoghi-condivisi) |
| T6 · V5, O3, FN3 | In una revisione in sola lettura le curve del palo verticale, la scheda «Risposta elastica» e i gruppi delle fondazioni scrivono chiavi di stato con `Modified`: si conserva? | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 12): regola unica B: in sola lettura non si scrive nel documento; lo stato della vista resta in memoria. Da attuare dopo il 28/10. Prima: Conservato, come oggi | Regola unica «in sola lettura non si scrive»: stato della vista non salvato, differenza dichiarata | nessuno sui risultati (alternativa: comportamento diverso dalla vista storica) | [T6](f5-punti-di-controllo.md#t6-chiavi-di-stato-scritte-in-sola-lettura) |
| T6 · FN4 | Scelta «Sezione X / Sezione Y» della rottura delle fondazioni in sola lettura: oggi è bloccata solo in certi casi | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 12): regola unica B, come T6 (scelta di vista non salvata). Da attuare dopo il 28/10. Prima: Il ViewModel la rifiuta sempre (proposta della nota) | Ammetterla sempre, perché è stato della vista non salvato | comportamento diverso dalla vista storica (marginale) | [T6](f5-punti-di-controllo.md#t6-chiavi-di-stato-scritte-in-sola-lettura); registro F5-11 |
| O9 (T9) | I diagrammi del palo orizzontale sono congelati come JSON indentato, circa 4 MB in 6 fixture: il peso va bene? | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Peso accettato | Un'impronta per diagramma | nessuno sui risultati (alternativa: tocca riferimenti congelati) | [T9](f5-punti-di-controllo.md#t9-peso-delle-tracce-e-dipendenza-da-font-e-dpi) |
| D14 (T11) | Banco interattivo `ANTHEA.exe --interactive --module-impl` della build UiTests: fino a quando resta? | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Fino a F5.14 | Non indicata | nessuno sui risultati | [T11](f5-punti-di-controllo.md#t11-strumenti-di-prova) |
| S11 (T11) | `MessageBoxHook`, che risponde ai `MessageBox` delle viste storiche fuori schermo, diventa uno strumento comune dell'harness? | **Chiusa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 1): superata dai fatti. Prima: Sì, strumento comune | Non indicata | nessuno sui risultati | [T11](f5-punti-di-controllo.md#t11-strumenti-di-prova) |

## 2. F5.4 · Palificata orizzontale

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| D3 | Un archivio senza l'oggetto degli interassi della direzione mostrata: la vista lo crea vuoto, la preparazione del capitolo autonomo no. Si allinea `ModuleSheetPreparation.PreparePileGroup`? | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. `PreparePileGroup` resta com'è | Allineare la preparazione (modifica dell'Application, non dei report) | nessuno sui risultati (report uguale, dati preparati diversi; nessuna fixture lo mostra) | [f5-4 §4.4](f5-4-pilota.md#44-domande-d1d8-con-quello-che-si-sa-ora-e-domande-nuove) |

## 3. F5.5 · Materiali

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| M9 | Serve un comando «Reologia del calcestruzzo» nella scheda del calcestruzzo? | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. Nessun comando, perché sarebbe una funzione nuova. La finestra si apre dal menu Strumenti e dalla sezione c.a. | Aggiungere il comando | nessuno sui risultati | [f5-5 §3](f5-5-materiali.md#3-domande-per-il-punto-di-controllo-f55) |
| M15 | Titoli dei riquadri e delle colonne del calcestruzzo, oggi a 15 punti | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. `Font.Heading` (16) | `Font.Subheading` (14), come le intestazioni delle schede dell'acciaio | solo interfaccia | [f5-5 §4.6](f5-5-materiali.md#46-domande-nuove) |
| M16 | Valori in sola lettura del calcestruzzo (`#EDF5FF`) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 4): A, confermata. `Token.Calculated` (ΔE 1,5) | `Token.Field.Background` (ΔE 4,9), che conserva anche gli aspetti scuri | solo interfaccia | [f5-5 §4.6](f5-5-materiali.md#46-domande-nuove) |
| M17 | Colori dello stato dell'acciaio, senza un token entro ΔE 5 | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 4): A, confermata. Due token nuovi, `Status.Coherent` e `Status.Caution` (insieme semantico da 36 a 38) | Accorpare a `Status.Ok` e `Warning.Foreground`, con un cambiamento visibile (ΔE 27,8 e 17,5) | solo interfaccia | [f5-5 §4.6](f5-5-materiali.md#46-domande-nuove) |
| M18 | Dove sta lo stato dei campi fissati dal catalogo (M6)? | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. `CalculatedKeys` e `ReadOnlyKeys` in `FieldForm` (vista), non in `FieldViewModel` | Sola lettura per campo in `FieldViewModel` | nessuno sui risultati | [f5-5 §4.6](f5-5-materiali.md#46-domande-nuove) |
| M19 | `BuildReport` di un foglio materiale: la vista storica fallisce («Premere Calcola prima di esportare.»), l'ospite MVVM restituisce il Word | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Come l'ospite MVVM. Nessuno lo chiama per i materiali. | Non indicata | nessuno sui risultati (percorso non usato) | [f5-5 §4.6](f5-5-materiali.md#46-domande-nuove) |
| M20 | Rotella del mouse sulle scelte | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. Come oggi: comportamento predefinito di WPF per il calcestruzzo; il tipo dell'acciaio chiuso passa la rotella alla pagina | Non indicata | solo interfaccia | [f5-5 §4.6](f5-5-materiali.md#46-domande-nuove) |

## 4. F5.6 · Palo e micropalo verticale

Le quattro scelte della sezione 5.3 non hanno codice.

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| §5.3-1 | Colori dell'esito della verifica: `#16703B` e `#B42318` non hanno un token. Vale anche per F5.7 (§5.4). | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 4): A, confermata. `Status.Ok` e `Status.Fail`: i colori cambiano di poco | Non indicata (in BD15: token nuovi con i colori storici) | solo interfaccia | [f5-6 §5.3](f5-6-palo-verticale.md#53-scelte-da-confermare) |
| §5.3-2 | Corpi 15, 18 e 10 della vista storica | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. `Font.Subheading` (14), `Font.Heading` (16), `Font.Caption` (11) | Non indicata | solo interfaccia | [f5-6 §5.3](f5-6-palo-verticale.md#53-scelte-da-confermare) |
| §5.3-3 | Finestre dei dialoghi («Copia in…», «Copia da…», «Terreno · invio e riutilizzo») | **Decisa 25/10, attuata 25/10 (passo d)** ([famiglie](f5-15-famiglie.md), famiglia 5): B: regola unica di `WpfDialogService`, dialogo ridimensionabile con una misura minima. Da attuare dopo il 28/10. Prima: Misura della finestra storica (940×660 e 430×210), fissa: `WpfDialogService` adatta la finestra al contenuto | Finestra ridimensionabile con minimo 720×520, come la storica | solo interfaccia | [f5-6 §5.3](f5-6-palo-verticale.md#53-scelte-da-confermare) |
| §5.3-4 | «Modello e dati comuni…» | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. Apre la Wiki della finestra principale con `IWikiNavigator` | Un dialogo con il testo, come la vista storica | comportamento diverso dalla vista storica | [f5-6 §5.3](f5-6-palo-verticale.md#53-scelte-da-confermare); registro F5-06 |

## 5. F5.6b · Stratigrafia

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| S6 (T7) | «No» alla rimozione dei minimi solleva `Modified` senza cambiamenti | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Conservato | Correggere come differenza dichiarata | nessuno sui risultati (alternativa: comportamento diverso, tracce da ricatturare) | [f5-6b §4](f5-6b-stratigrafia.md#4-domande-per-il-punto-di-controllo-f56b); [T7](f5-punti-di-controllo.md#t7-difetti-storici-correggere-come-differenza-dichiarata-o-conservare) |
| S7 | Campi su più righe (descrizioni, fonti, note) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Proprietà `Multiline` additiva di `FieldSpec`/`FieldForm`, riusabile dagli altri moduli | Un modello di campo locale nella vista della stratigrafia | nessuno sui risultati | [f5-6b §4](f5-6b-stratigrafia.md#4-domande-per-il-punto-di-controllo-f56b) |
| S13 | Allineamento del testo dei campi a una riga | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. A destra, come `FieldForm`/`InputForm`; nessuna API comune nuova | Proprietà additiva di `FieldSpec` per allineare i testi a sinistra, come la vista storica | solo interfaccia | [f5-6b §5.5](f5-6b-stratigrafia.md#55-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S14 | Suggerimento di φ′: `CalculationHelp.Field("phi")` dà l'aiuto della viscosità φ del calcestruzzo | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. Proprietà additiva `FieldSpec.ToolTip`; la stratigrafia usa l'etichetta come suggerimento | Suggerimenti per modulo in `CalculationHelp` | solo interfaccia (la vista storica dei muri ha lo stesso difetto: segnalato, non corretto) | [f5-6b §5.5](f5-6b-stratigrafia.md#55-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S15 | Nota sui terreni misti fra comportamento e descrizione (S8) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. Due form: `LayerForm` (nome, famiglia, comportamento) e `DescriptionForm` (descrizione, colore) | La nota come `FieldSpec.Note` del comportamento: riquadro giallo, testo diverso dalla storica | solo interfaccia | [f5-6b §5.5](f5-6b-stratigrafia.md#55-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S16 | Finestra del dialogo dell'importazione IA | **Decisa 25/10, attuata 25/10 (passo d)** ([famiglie](f5-15-famiglie.md), famiglia 5): B: regola unica di `WpfDialogService`, dialogo ridimensionabile con una misura minima. Da attuare dopo il 28/10. Prima: Fissa 1200×850, come i dialoghi di F5.6 | Ridimensionabile con minimo 850×600 in `WpfDialogService` | solo interfaccia | [f5-6b §5.5](f5-6b-stratigrafia.md#55-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S17 | Pulsanti del dialogo condiviso e della vista del palo verticale senza lo stile del tema | **Chiusa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 1): superata dai fatti. Prima: Corretti con `BasedOn`, anche in `VerticalPileSheetView` (dal coordinatore dopo l'unione) | Non indicata | solo interfaccia | [f5-6b §5.5](f5-6b-stratigrafia.md#55-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |

## 6. F5.7 · Palo e micropalo orizzontale

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| O4 (T7) | Due `Modified` per ogni modifica della scheda della capacità | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Conservati | Uno solo: nessun effetto sui dati, lo vedrebbe solo il contatore delle modifiche | nessuno sui risultati | [f5-7 §4](f5-7-palo-orizzontale.md#4-domande-per-il-punto-di-controllo-f57); [T7](f5-punti-di-controllo.md#t7-difetti-storici-correggere-come-differenza-dichiarata-o-conservare) |
| §5.4-1 | Editor di «Opzioni avanzate» (metodo, passo, tolleranza) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. 82 px del `FieldForm` compatto (D10) invece di 150 px | La larghezza storica | solo interfaccia | [f5-7 §5.4](f5-7-palo-orizzontale.md#54-differenze-visibili-e-scelte-da-confermare-da-decidere-con-la-scelta-fatta) |
| §5.4-2 | Titoli delle tabelle 14.5 e 14.6 nel dialogo dei dettagli del terreno | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. Da 15 a 14 punti (`Font.Subheading`) | Non indicata | solo interfaccia | [f5-7 §5.4](f5-7-palo-orizzontale.md#54-differenze-visibili-e-scelte-da-confermare-da-decidere-con-la-scelta-fatta) |
| §5.4-3 | Stili delle caselle della tabella degli strati negli aspetti scuri. Probabile stesso difetto latente nella vista F5.6 (`LayerChoice`, `LayerText`). | **Chiusa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 1): superata dai fatti. Prima: Corretto in F5.7 con lo stile del tipo (`BasedOn`); in F5.6 segnalato, non corretto | Correggere anche la vista F5.6 | solo interfaccia | [f5-7 §5.4](f5-7-palo-orizzontale.md#54-differenze-visibili-e-scelte-da-confermare-da-decidere-con-la-scelta-fatta) |

## 7. F5.8 · Risposta elastica e armature

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| E4 (T7) | Archivio da migrare: lo stato dice «analisi FEM e convergenza in coda» con la barra indeterminata, ma nessun calcolo è in coda | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): B: stato e barra coerenti. Correzione come differenza dichiarata, con la ricattura dei riferimenti nel commit di V-a. Da attuare dopo il 28/10. Prima: Conservato | Stato e barra coerenti, come differenza dichiarata | nessuno sui risultati (alternativa: comportamento diverso dalla vista storica) | [f5-8 §4](f5-8-palo-elastico.md#4-domande-per-il-punto-di-controllo-f58); [T7](f5-punti-di-controllo.md#t7-difetti-storici-correggere-come-differenza-dichiarata-o-conservare) |
| E5 (T7) | Quando la quota selezionata si perde, la sezione conserva didascalia, diametro della staffa e assi | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): B: didascalia, staffa e assi azzerati quando la quota si perde. Correzione come differenza dichiarata, con la ricattura dei riferimenti nel commit di V-a. Da attuare dopo il 28/10. Prima: Conservato | Correggere | nessuno sui risultati | [f5-8 §4](f5-8-palo-elastico.md#4-domande-per-il-punto-di-controllo-f58) |
| §6.4-1 | Titoli: scheda 18, confronto con un archivio vecchio 17 | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. `Font.Heading` (16) | Non indicata | solo interfaccia | [f5-8 §6.4](f5-8-palo-elastico.md#64-differenze-visibili-e-scelte-da-confermare-da-decidere-con-la-scelta-fatta) |
| §6.4-2 | Toni del pannello «Sezione e verifiche» | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 4): A, confermata. `Status.Fail`, `Status.Ok`, `Ink`, `Muted`; l'ambra `#B8860B` diventa `Status.Caution`, il grigio `#708090` `Muted` | Non indicata | solo interfaccia | [f5-8 §6.4](f5-8-palo-elastico.md#64-differenze-visibili-e-scelte-da-confermare-da-decidere-con-la-scelta-fatta) |
| §6.4-3 | Colori delle schede dei tratti | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 4): A, confermata. Token `Segment.1–6` in un gruppo nuovo fuori dall'insieme semantico (che resta 38 ≤ 40) e 6 voci nuove in `tests/fixtures/f5/token-colori.json` | Non indicata | solo interfaccia (colori uguali; 6 voci nuove nella tabella congelata dei colori) | [f5-8 §6.4](f5-8-palo-elastico.md#64-differenze-visibili-e-scelte-da-confermare-da-decidere-con-la-scelta-fatta) |
| §6.4-4 | Scrittura di «Esporta PNG…» | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Atomica (`Archivio.ScriviAtomico`) | `File.WriteAllBytes`, come la vista storica | nessuno sui risultati | [f5-8 §6.4](f5-8-palo-elastico.md#64-differenze-visibili-e-scelte-da-confermare-da-decidere-con-la-scelta-fatta) |
| §6.4-5 | Dialoghi della vista (NTC §7.2.5, «Info sezioni», proposta, divisione, dimensionamento, «Info», confronto, tavola) | **Decisa 25/10, attuata 25/10 (passo d)** ([famiglie](f5-15-famiglie.md), famiglia 5): B: regola unica di `WpfDialogService`, dialogo ridimensionabile con una misura minima. Da attuare dopo il 28/10. Prima: Finestre della vista con le misure storiche; il verificatore c.a. è il dialogo dei servizi e si adatta al contenuto | Non indicata | solo interfaccia | [f5-8 §6.4](f5-8-palo-elastico.md#64-differenze-visibili-e-scelte-da-confermare-da-decidere-con-la-scelta-fatta) |
| §6.4-6 | Cataloghi del dimensionamento | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Passati al ViewModel a ogni tasto (`UpdateSourceTrigger=PropertyChanged`) | Non indicata | nessuno sui risultati | [f5-8 §6.4](f5-8-palo-elastico.md#64-differenze-visibili-e-scelte-da-confermare-da-decidere-con-la-scelta-fatta) |

## 8. F5.9 · Muri di sostegno

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| MU10 | Distinta ferri e finestra dei valori | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. La finestra dei valori è un dialogo modale su `RetainingWallParametersViewModel`; la distinta è un dialogo della vista su `CreateBarSchedule`, senza ViewModel proprio | Non indicata | solo interfaccia | [f5-9 §4](f5-9-muri.md#4-domande-per-il-punto-di-controllo-f59); [decisioni, 17/10](decisioni.md#17-ottobre-mattina--punti-di-controllo-f54f510-parte-a) |
| kh e kv | Il capitolo autonomo non ha kh e kv del sito: la scheda li ricava dai parametri del sito all'apertura e li scrive, il capitolo autonomo no. Per un documento non salvato dalla scheda i dati preparati differiscono. | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 11): A, voce chiusa senza cambiamenti. Prima: Nessuna opzione presa: la voce «resta da guardare in F5.15, perché riguarda il calcolo». Oggi resta com'è. | Allineare la preparazione del capitolo autonomo alla scheda | nessuno sui risultati (il calcolo li ricava comunque; le fixture salvano i valori ricavati) | [punti, difetti F5.9](f5-punti-di-controllo.md#difetti-trovati-nelle-viste-attuali); [decisioni, 17/10](decisioni.md#17-ottobre-mattina--punti-di-controllo-f54f510-parte-a) |
| MU14 | «Ricalcola», «Calcola globale», «Calcola armature» e «Valori di calcolo…» avviano un lavoro sullo sfondo e tornano | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 7): B: spenti durante il lavoro, come `GuardedCommands`. Da attuare dopo il 28/10. Prima: Restano attivi durante il lavoro, come la vista storica; un secondo clic non è rifiutato | Spenti durante il lavoro, come vuole la convenzione di `GuardedCommands` | nessuno sui risultati | [f5-9 §5.4](f5-9-muri.md#54-scelte-da-confermare-da-decidere) |
| MU15 | Righe alterne delle tabelle degli input: `#F3F6FA` non segue l'aspetto | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 4): A, confermata. `Token.Surface` | `Token.PaperAlt` (`#F8FAFC`), il token delle righe alterne | solo interfaccia | [f5-9 §5.4](f5-9-muri.md#54-scelte-da-confermare-da-decidere) |
| MU16 | «DarkGoldenrod» dello stato con avvisi | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 4): A, confermata. `Token.Status.Caution` (`#9A6700`) | Un token nuovo con il colore storico | solo interfaccia | [f5-9 §5.4](f5-9-muri.md#54-scelte-da-confermare-da-decidere) |
| MU17 | Dialogo dei valori | **Decisa 25/10, attuata 25/10 (passo d)** ([famiglie](f5-15-famiglie.md), famiglia 5): B: regola unica di `WpfDialogService`, dialogo ridimensionabile con una misura minima. Da attuare dopo il 28/10. Prima: Dimensione fissa della finestra storica (1150×780) | Ridimensionabile con minimo 780×530, come la storica | solo interfaccia | [f5-9 §5.4](f5-9-muri.md#54-scelte-da-confermare-da-decidere) |
| MU18 | Gruppi interni riportati allo stato iniziale quando il foglio ricostruisce gli input | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Come la vista storica, nel code-behind | Lasciarli come li ha messi l'utente | nessuno sui risultati | [f5-9 §5.4](f5-9-muri.md#54-scelte-da-confermare-da-decidere) |
| MU19 | Lettura delle tracce attraverso la vista | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Campi non disposti e blocchi non generati letti dal ViewModel; il valore di un campo di testo è il testo grezzo del `FieldViewModel`, come nel palo verticale | Non indicata | nessuno sui risultati (solo prove) | [f5-9 §5.4](f5-9-muri.md#54-scelte-da-confermare-da-decidere) |

## 9. F5.9b · Fondazioni superficiali

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| FN5 | Le cinque scelte del metodo scrivono l'indice della voce, non il testo | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Componente esplicito `ShallowFoundationChoiceViewModel` | Una regola di scrittura «indice» in `FieldWriteRule` (componente comune) | nessuno sui risultati | [f5-9b §4](f5-9b-fondazioni.md#4-domande-per-il-punto-di-controllo-f59b) |
| FN9 | I campi numerici non hanno «?» né F1 per campo; la falda e il calcestruzzo sì | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. L'aiuto resta com'è, senza estenderlo | L'aiuto a tutti i campi con `FieldForm` | nessuno sui risultati (alternativa: comportamento diverso dalla vista storica) | [f5-9b §4](f5-9b-fondazioni.md#4-domande-per-il-punto-di-controllo-f59b) |

## 10. F5.10 · Sezione c.a.

Fonte: [f5-10-sezione.md](f5-10-sezione.md). La Release resta storica fino ai passaggi di fine refactoring.

### 10.1 Punto di controllo della parte A

Scelte del coordinatore del 17/10 «da ratificare» ([decisioni](decisioni.md#17-ottobre-mattina--punti-di-controllo-f54f510-parte-a)).
C6 aveva una proposta, accettata: ordine fisso degli errori parziali e riga evidenziata come nella vista (registro F5-12).

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| C1 | Calcolo del foglio: la vista ha un'orchestrazione propria (famiglie, giro del dispatcher, lotto in parallelo, poi il taglio) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Coordinatore in Presentation sopra `RecalculationScheduler` e `ConcreteAnalysisSession`, foglio in modalità `None` | Una quarta modalità del componente comune | nessuno sui risultati | [f5-10 §7](f5-10-sezione.md#7-domande-per-il-punto-di-controllo-f510) |
| C2, S5-c | Taglio sul thread dell'interfaccia | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 10): B: taglio sullo sfondo, nella voce di infrastruttura con D-2 e D-5. Prima: Conservato in F5.10, confermato in S5 | Sullo sfondo dopo S9, come differenza dichiarata | nessuno sui risultati | [f5-10 §7](f5-10-sezione.md#7-domande-per-il-punto-di-controllo-f510); [§14.5](f5-10-sezione.md#145-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| C3 | `SectionActionsViewModel` da Application a Presentation (Q17 a) | **Chiusa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 1): superata dai fatti. Prima: Spostato a S2 | Resta in Application | nessuno sui risultati | [f5-10 §7](f5-10-sezione.md#7-domande-per-il-punto-di-controllo-f510) |
| C4 | Verificatore c.a. del palo elastico (con E1) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Dialogo di `IDialogService` con ritorno dei dati. Fino al passaggio ospita la `ConcreteWorkspace` storica, poi il ViewModel della sezione. | Un membro nuovo di `ISheetHost`; la copia senza ritorno come differenza dichiarata | nessuno sui risultati | [f5-10 §5](f5-10-sezione.md#5-palo-elastico-f58-e-muri-f59); [f5-8 §6.2](f5-8-palo-elastico.md#62-decisioni-applicate) |
| C5 | Comportamenti storici trovati con le tracce (righe 10.2) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): ogni comportamento ha la sua decisione nella famiglia 6: 10 da correggere, gli altri conservati. Prima: Conservati per avere tracce identiche. Diventano voci del registro da decidere al passaggio. | Correggerli nella vista nuova come differenze dichiarate, con una nuova cattura | nessuno sui risultati (alternativa: comportamento diverso, tocca riferimenti congelati) | [f5-10 §7](f5-10-sezione.md#7-domande-per-il-punto-di-controllo-f510); registro F5-15…F5-57 |
| C7 | Peso delle tracce (alla parte A 37 scenari, 7,3 MB, 165 s; oggi 57 scenari) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Tracce lunghe nello stadio `ui`, prove rapide in `Presentation.Tests` | La proposta della nota: `Presentation.Tests` (stadio `fast`) con le cartelle dei sottopassi e tre fixture, il resto in `ui` o `full` | nessuno sui risultati (solo prove) | [f5-10 §7](f5-10-sezione.md#7-domande-per-il-punto-di-controllo-f510) |
| C8 | Disegni (Q3) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Restano WPF alimentati dal ViewModel; il 3D si confronta solo come dati | Non indicata | nessuno sui risultati | [f5-10 §7](f5-10-sezione.md#7-domande-per-il-punto-di-controllo-f510) |
| C9 | Trefoli e calcolo fermato per intero sono solo nelle tracce, non nelle fixture F3 | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Accettato | Chiedere alla libreria la curva M–χ con trefoli | nessuno sui risultati | [f5-10 §7](f5-10-sezione.md#7-domande-per-il-punto-di-controllo-f510) |
| C10, S1-d | Strategia per sottopassi e vista XAML | **Chiusa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 1): superata dai fatti. Prima: Sottopassi in sequenza; una vista unica dopo S9, fatta il 22/10; Release storica fino ai passaggi di fine refactoring | Una vista XAML per sottopasso, ospitata nella vista storica | nessuno sui risultati | [f5-10 §7](f5-10-sezione.md#7-domande-per-il-punto-di-controllo-f510); [§8.5](f5-10-sezione.md#85-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |

### 10.2 Comportamenti storici (C5)

Sono tutti conservati (C5). L'alternativa comune è correggerli al passaggio della Release, come differenze dichiarate, con una nuova
cattura. Le righe «C5·S1…C5·S9» elencano i comportamenti di un sottopasso che non hanno una domanda propria in 10.3. Gli artefatti
delle sole tracce (§8.4 punto 4, §11.5 punto 4) non sono comportamenti del prodotto.

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| §2-3 | Un documento di versione 2 senza `dominio2d` riceve N = +1000 (trazione). ANTHEA scrive sempre `dominio2d`: capita solo con documenti costruiti a mano. | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Conservato | Correggere il valore predefinito | nessuno sui risultati dei documenti scritti da ANTHEA | [f5-10 §2](f5-10-sezione.md#2-comportamenti-della-vista-storica-trovati); registro F5-15 |
| §2-4 | Nel dominio 2D SLV compare il messaggio grezzo della libreria «Value cannot be null. (Parameter 'failureDomain')» | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): B: messaggio di ANTHEA. Correzione come differenza dichiarata, con la ricattura dei riferimenti nel commit di V-a. Da attuare dopo il 28/10. Prima: Conservato | Un messaggio di ANTHEA | nessuno sui risultati | [f5-10 §2](f5-10-sezione.md#2-comportamenti-della-vista-storica-trovati); registro F5-16 |
| §2-5 e famiglia | In sola lettura la revisione scrive con `Modified` scheda, filtro dei domini, «Colori η», «Forze», contorno SLE, opzioni dei pannelli dei domini (S3 punto 2), contorno della mappa e «Asse neutro» (S4 punto 5), `report_sezioni` (S9 punto 3) | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 12): regola unica B di T6. Da attuare dopo il 28/10. Prima: Conservato, come T6 | La regola unica di T6 | nessuno sui risultati (alternativa: comportamento diverso dalla vista storica) | [f5-10 §2](f5-10-sezione.md#2-comportamenti-della-vista-storica-trovati); [§12.4](f5-10-sezione.md#124-comportamenti-storici-trovati-in-s3-conservati-c5); [§13.4](f5-10-sezione.md#134-comportamenti-storici-trovati-in-s4-conservati-c5); [§19.4](f5-10-sezione.md#194-comportamenti-storici-trovati-in-s9-conservati-c5); registro F5-17 |
| §10.4-1 | «Esporta Excel» resta attivo in sola lettura: apre la finestra delle sollecitazioni, dove «Applica» cambia i dati della revisione con `Modified` e riapre il foglio. Nei casi simili MU3 e BD4 la vista nuova rifiuta la scrittura. | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 12): regola unica B di T6: «Applica» rifiutato in sola lettura. Da attuare dopo il 28/10. Prima: Conservato | Rifiutare «Applica» in sola lettura, come MU3 e BD4 | nessuno sui risultati (alternativa: comportamento diverso dalla vista storica) | [f5-10 §10.4](f5-10-sezione.md#104-comportamenti-storici-trovati-in-s2-conservati-c5); registro F5-18 |
| S7-d | «Calcola curva» è disponibile in sola lettura, conferma il foglio e la curva entra nel risultato esportato | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 12): regola unica B di T6: spento in sola lettura. Da attuare dopo il 28/10. Prima: Conservato, come T6 | Spegnerlo in sola lettura | nessuno sui risultati | [f5-10 §16.5](f5-10-sezione.md#165-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515); registro F5-19 |
| §2-6 | «Annulla» dell'editor delle sollecitazioni cambia i dati (chiavi di visibilità scritte all'apertura) senza `Modified` | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Conservato | Non indicata | nessuno sui risultati | [f5-10 §2](f5-10-sezione.md#2-comportamenti-della-vista-storica-trovati); registro F5-20 |
| §2-7 | L'impronta del risultato esportato cambia dopo la visita delle schede. Causa trovata in S1: l'esportazione resta in cache e `Commit` non la azzera. | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Conservato | Non indicata | nessuno sui risultati | [f5-10 §2](f5-10-sezione.md#2-comportamenti-della-vista-storica-trovati); [§8.4](f5-10-sezione.md#84-comportamenti-storici-trovati-in-s1-conservati-c5); registro F5-21 |
| §2-8 | Barre per coordinate: l'armatura guidata è bloccata solo se `barre_manuali` è un elenco, i secondi strati se la chiave c'è, qualunque valore | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Conservato | Non indicata | nessuno sui risultati | [f5-10 §2](f5-10-sezione.md#2-comportamenti-della-vista-storica-trovati); registro F5-22 |
| §2-9 | La ricerca delle armature rifiuta la riserva torsionale 0 % e la sezione in zona di sovrapposizione | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Conservato | Non indicata | nessuno sui risultati | [f5-10 §2](f5-10-sezione.md#2-comportamenti-della-vista-storica-trovati); registro F5-23 |
| C5·S1 | Il trefolo standard invalida il foglio due volte (due `Modified`); la selezione delle tabelle segue l'elemento corrente della vista di raccolta | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Conservati | Correggere al passaggio | nessuno sui risultati | [f5-10 §8.4](f5-10-sezione.md#84-comportamenti-storici-trovati-in-s1-conservati-c5); registro F5-24, F5-25 |
| C5·S2 | «−» del taglio senza selezione salva e ricalcola; «Duplica» non conferma la cella in modifica; le righe incollate nel taglio si chiamano «Combo n»; dopo la finestra delle azioni il calcolo parte quando la vista è caricata | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Conservati | Correggere al passaggio | nessuno sui risultati | [f5-10 §10.4](f5-10-sezione.md#104-comportamenti-storici-trovati-in-s2-conservati-c5); registro F5-26…F5-29 |
| C5·S1b | Esempi ordinari salvati senza i valori dell'editor (la sezione GPC archiviata non si riusa); dopo il wizard il taglio dà «rami_x: inserire un numero finito.» finché l'utente non assegna i parametri (CA-WIZARD-01); la tipologia del wizard resta fra le voci di «Sezione» | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Conservati | Per gli esempi: salvarli di nuovo dall'editor, fuori da F5 e da proporre all'utente | nessuno sui risultati | [f5-10 §11.5](f5-10-sezione.md#115-comportamenti-storici-trovati-in-s1b-conservati-c5); registro F5-30…F5-32 |
| C5·S3 | «Adatta» del 2D scrive due volte i fattori x e y (due `Modified`) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Conservato | Correggere al passaggio | nessuno sui risultati | [f5-10 §12.4](f5-10-sezione.md#124-comportamenti-storici-trovati-in-s3-conservati-c5); registro F5-33 |
| C5·S4 | Dopo una modifica di n o di φ le altre famiglie mostrano i valori grezzi dei dati («17.607108191890596» contro «17,61»); una famiglia nascosta conserva la legenda dell'ultimo disegno | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): B: valori formattati e legenda azzerata. Correzione come differenza dichiarata, con la ricattura dei riferimenti nel commit di V-a. Da attuare dopo il 28/10. Prima: Conservati | Correggere al passaggio | nessuno sui risultati | [f5-10 §13.4](f5-10-sezione.md#134-comportamenti-storici-trovati-in-s4-conservati-c5); registro F5-36, F5-37 |
| C5·S5 | La torsione invalida due volte; con le staffe assenti il modello passa a «Senza staffe» solo come valore mostrato; il disegno della sezione del taglio non riceve il messaggio; il riepilogo si scrive due volte (il ViewModel scrive solo le schede) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Conservati | Correggere al passaggio | nessuno sui risultati | [f5-10 §14.4](f5-10-sezione.md#144-comportamenti-storici-trovati-in-s5-conservati-c5); registro F5-39…F5-42 |
| C5·S6 | L'esposizione condivisa resta vuota all'apertura finché l'elemento è «Da scegliere»; «Aggiorna verifiche ancoraggi» ricalcola due volte | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Conservati | Correggere al passaggio | nessuno sui risultati | [f5-10 §15.4](f5-10-sezione.md#154-comportamenti-storici-trovati-in-s6-conservati-c5); registro F5-44, F5-45 |
| C5·S7 | La mappa σ/ε tiene la sezione precedente finché il pannello non la aggiorna; dopo una modifica durante il calcolo della curva «Calcolo interrotto…» sostituisce «Dati modificati · ricalcolare la curva.» | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): B: messaggio della sezione del pannello e azzeramento. Correzione come differenza dichiarata, con la ricattura dei riferimenti nel commit di V-a. Da attuare dopo il 28/10. Prima: Conservati | Correggere al passaggio | nessuno sui risultati | [f5-10 §16.4](f5-10-sezione.md#164-comportamenti-storici-trovati-in-s7-conservati-c5); registro F5-48, F5-49 |
| C5·S8 | All'avvio della ricerca lo stato resta il precedente fino al primo avanzamento; i campi della ricerca si rinnovano a ogni `Modified`; un avanzamento in ritardo è scartato | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Conservati | Correggere al passaggio | nessuno sui risultati | [f5-10 §17.4](f5-10-sezione.md#174-comportamenti-storici-trovati-in-s8-conservati-c5); registro F5-51…F5-53 |
| C5·S9 | Il report cambia l'esportazione in cache e la sua impronta; aggiorna il pannello nascosto del dominio 2D; tre messaggi diversi per lo stesso rifiuto; `Modified` anche con le stesse sezioni | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Conservati | Correggere al passaggio | nessuno sui risultati | [f5-10 §19.4](f5-10-sezione.md#194-comportamenti-storici-trovati-in-s9-conservati-c5); registro F5-54…F5-57 |

### 10.3 Domande dei sottopassi S1–S9

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| S1-a | Finestre non modali «Reologia…» e «Proprietà / report…» | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Chieste alla vista con eventi del ViewModel; nessun servizio comune nuovo | Un servizio in `PresentationServices` | nessuno sui risultati | [f5-10 §8.5](f5-10-sezione.md#85-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S1-b, V-f | «Calcola» dell'ospite: `MvvmSheetEditor.CalculateAsync` conferma il foglio prima di calcolare e scrive `visible` nelle righe delle azioni; `SheetEditor` per la sezione storica no. Nessun ospite mostra «Calcola» per la sezione c.a.: il comando è della finestra e delle prove. | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 9): A, confermata. Ospite invariato (conferma, come D7 per tutti i moduli); le prove chiamano `CalculateAsync(commitEdits: false)`. Da conservare o dichiarare al passaggio. | `MvvmSheetEditor` senza conferma per la sezione c.a. (file condiviso dell'ospite) | comportamento diverso dalla vista storica (dati confermati prima del calcolo; tracce e risultati uguali) | [f5-10 §8.5](f5-10-sezione.md#85-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515); [§22.5](f5-10-sezione.md#225-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515); registro F5-13 |
| S1-c | C6 per fasi: un calcolo annullato a metà di una fase non pubblica gli esiti già arrivati di quella fase | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Così | Non indicata | nessuno sui risultati (visibile solo con il motore trattenuto) | [f5-10 §8.5](f5-10-sezione.md#85-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S1-e | Chiavi di stato dell'interfaccia | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Dichiarate per sottopasso: `tab`, `layout`, `quote_*` in S1, poi filtri, contorni e opzioni dei grafici | Non indicata | nessuno sui risultati | [f5-10 §8.5](f5-10-sezione.md#85-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S2a-1 | Copertura delle sezioni da wizard | **Chiusa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 1): superata dai fatti. Prima: Tracce nuove della vista storica come scenari nuovi con la parte `[wizard]`, fatte in S1b; le 39 tracce congelate invariate | Rinviare la copertura a S9 | nessuno sui risultati (solo prove) | [f5-10 §9.6](f5-10-sezione.md#96-domande-da-decidere) |
| S2-a, S3-a | Chi scrive `dominio3d.stato` e `dominio2d.stato`? | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Il campo «stato» del form delle opzioni; `ConcreteActionTableViewModel.State` passa da lì (S3-a chiude S2-a) | Il form scrive e chiama `Reattach` | nessuno sui risultati | [f5-10 §10.5](f5-10-sezione.md#105-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515); [§12.5](f5-10-sezione.md#125-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S2-b | Il giro di ritardo vale solo per la riapertura dopo la finestra delle azioni; `Reload` per i dati condivisi mette il calcolo subito in coda | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Così, da decidere al passaggio | La stessa regola per i due casi | nessuno sui risultati (nessuna traccia copre `Reload`) | [f5-10 §10.5](f5-10-sezione.md#105-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S2-c | Comandi Excel della finestra delle azioni | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Nel `SectionActionsViewModel`, con `IFileDialogService` e il file proposto; nessun ViewModel della finestra a parte | Non indicata | nessuno sui risultati | [f5-10 §10.5](f5-10-sezione.md#105-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S2-d | «Elimina» nella finestra delle azioni lascia `Selected` sulla riga tolta; la griglia storica lo azzera con il binding | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. La vista XAML lega la selezione come la storica | Non indicata | nessuno sui risultati | [f5-10 §10.5](f5-10-sezione.md#105-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S1b-a | Cartella dei 15 esempi nelle tracce | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. `esempi/`, con il percorso completo e tutte le parti | Solo la parte `[ingressi]` | nessuno sui risultati (solo prove) | [f5-10 §11.6](f5-10-sezione.md#116-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S1b-b | Archivio nelle tracce | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Dimensione e impronta nelle righe dei valori cambiati | Non indicata | nessuno sui risultati (solo prove) | [f5-10 §11.6](f5-10-sezione.md#116-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S1b-c | Finestra del wizard nella traccia storica | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Campi guidati dal ViewModel (isola MVVM), pulsanti premuti nella finestra | Non indicata | nessuno sui risultati (solo prove) | [f5-10 §11.6](f5-10-sezione.md#116-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S1b-d | Disegni delle sezioni da wizard | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Nessun disegno nuovo congelato: la sezione disegnata è nelle tracce come dati | Elenchi di disegno degli esempi in un file a parte di `disegni/` | nessuno sui risultati (solo prove) | [f5-10 §11.6](f5-10-sezione.md#116-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S3-b | Esenzioni della sola lettura nei pannelli dei domini | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 7): A, confermata. `FormViewModel.AllowWhenReadOnly`, aggiunta al componente comune | Due form separati | nessuno sui risultati | [f5-10 §12.5](f5-10-sezione.md#125-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S3-c | Zoom e camera | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Restano della vista; la vista del 3D («Isometrica», «N–Mx»…) non passa dal ViewModel | Non indicata | nessuno sui risultati | [f5-10 §12.5](f5-10-sezione.md#125-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S3-d | Elenchi di disegno del dominio 2D | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Confrontati solo con la vista XAML; nel ViewModel il grafico è confrontato come dati | Non indicata | nessuno sui risultati (solo prove) | [f5-10 §12.5](f5-10-sezione.md#125-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S4-a | Legenda della mappa | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Dato del ViewModel (`Legend`), scritto quando la famiglia è mostrata; la vista XAML la disegna con `ContourScale` | Nessuna proprietà: la legenda solo nel disegno | nessuno sui risultati | [f5-10 §13.5](f5-10-sezione.md#135-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S4-b | Colonne SLE nascoste scelte per indice: con le sollecitazioni uniche cadono sulle colonne sbagliate | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): B: colonne scelte per chiave. Correzione come differenza dichiarata, con la ricattura dei riferimenti nel commit di V-a. Da attuare dopo il 28/10. Prima: Conservate | Scegliere le colonne per chiave al passaggio, come differenza dichiarata | nessuno sui risultati (alternativa: comportamento diverso, tracce da ricatturare) | [f5-10 §13.5](f5-10-sezione.md#135-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515); registro F5-35 |
| S4-c, S7-b | Schede interne a −1 finché non sono mostrate (dettagli SLE; scheda interna del dominio) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 8): B: la prima scheda fin dall'apertura, con la ricattura del passo a (cambia le tracce congelate). Da attuare dopo il 28/10. Prima: Il ViewModel espone −1 come i `TabControl` storici; la vista XAML lega così | Fissare la prima scheda («Dominio») fin dall'apertura, come differenza dichiarata | nessuno sui risultati | [f5-10 §13.5](f5-10-sezione.md#135-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515); [§16.5](f5-10-sezione.md#165-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515); registro F5-38 |
| S4-d | Arresto atteso nelle tracce | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Passo e occorrenza del passo (`ExpectedStops`) invece del solo nome | Non indicata | nessuno sui risultati (solo prove) | [f5-10 §13.5](f5-10-sezione.md#135-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S4-e | Errori dell'omogeneizzazione | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Dizionario condiviso nel foglio, letto dai form negli stessi momenti della vista storica | Non indicata | nessuno sui risultati | [f5-10 §13.5](f5-10-sezione.md#135-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S5-a | Staffe in sola lettura della scheda 06 | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Create con la scheda 05 (`DetailingStirrups`), poi spostate nel ViewModel di S6 | Una sola coppia condivisa dalle due schede (la storica ne ha due) | nessuno sui risultati | [f5-10 §14.5](f5-10-sezione.md#145-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S5-b | Sintesi del taglio nel pannello di controllo | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Il foglio la scrive prima che i riepiloghi la sostituiscano, come la storica | Non indicata | nessuno sui risultati | [f5-10 §14.5](f5-10-sezione.md#145-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S5-d | Dopo il calcolo dell'apertura il dettaglio del taglio dice «Dati modificati · aggiornamento automatico in attesa» finché la scheda non è mostrata | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): A, conservato. Prima: Conservato | Non indicata | nessuno sui risultati | [f5-10 §14.5](f5-10-sezione.md#145-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515); registro F5-43 |
| S6-a, S8-b | Pulsanti di navigazione («Modifica … nel pannello di controllo →») in sola lettura | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 7): B: attivi in sola lettura, con la regola T6. Da attuare dopo il 28/10. Prima: Spenti come oggi, anche se cambiano solo la scheda | Attivi, come differenza dichiarata | nessuno sui risultati | [f5-10 §15.5](f5-10-sezione.md#155-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515); [§17.5](f5-10-sezione.md#175-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515); registro F5-46 |
| S6-b | Temi dei dettagli | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Elenco ordinato (`Topics`) al posto del dizionario storico; la vista XAML li dispone in righe come oggi | Non indicata | nessuno sui risultati | [f5-10 §15.5](f5-10-sezione.md#155-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S7-a | Correzione di S3: `IsShown` dei pannelli dei domini non dipende più dalla scheda interna | **Chiusa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 1): superata dai fatti. Prima: Tracce di S3 uguali; da confermare all'unione | Non indicata | nessuno sui risultati | [f5-10 §16.5](f5-10-sezione.md#165-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S7-c | Messaggi rimasti della mappa σ/ε e dopo un aggiornamento abbandonato (comportamenti 1 e 2 di S7) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): B: messaggio della sezione del pannello e azzeramento. Correzione come differenza dichiarata, con la ricattura dei riferimenti nel commit di V-a. Da attuare dopo il 28/10. Prima: Conservati | Il messaggio della sezione del pannello di controllo e l'azzeramento quando la scheda interna non è «Sezione σ / ε» | nessuno sui risultati | [f5-10 §16.5](f5-10-sezione.md#165-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515); registro F5-47 |
| S8-a | Ascolto di `Modified` | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Il ViewModel si iscrive a `Modified` del foglio come la vista storica e si disiscrive alla chiusura e al `Reload` | Una chiamata esplicita del foglio | nessuno sui risultati | [f5-10 §17.5](f5-10-sezione.md#175-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S8-c | Dopo «Applica» lo stato dice «Configurazione applicata…» e non che la ricerca è superata | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): B: lo stato dice che la ricerca va ripetuta. Correzione come differenza dichiarata, con la ricattura dei riferimenti nel commit di V-a. Da attuare dopo il 28/10. Prima: Conservato | Dire nello stato che la ricerca va ripetuta | nessuno sui risultati | [f5-10 §17.5](f5-10-sezione.md#175-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515); registro F5-50 |
| S9-a | Zoom e spostamento dei disegni nel report: la vista storica disegna il report dai suoi controlli | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 10): A, confermata. Il ViewModel dà i dati dei disegni: figure adattate, come nel capitolo autonomo; il grafico 2D porta lo zoom del pannello. Nessuna traccia lo copre. | La vista registra i suoi disegni vivi presso il disegnatore | comportamento diverso dalla vista storica (figure del report senza lo zoom dell'utente) | [f5-10 §19.5](f5-10-sezione.md#195-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515); registro F5-14 |
| S9-b | Report breve | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Interfaccia nuova `IModuleShortReport` di Presentation, accanto a `IModuleReportPdf` (T8) | Un membro di `IModuleReportSource` con un'implementazione predefinita | nessuno sui risultati | [f5-10 §19.5](f5-10-sezione.md#195-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| S9-c | Foglio senza disegnatore (solo prove) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Rifiuta i report con «grafici» e scrive gli altri senza figure; il modulo lo richiede sempre | Non indicata | nessuno sui risultati (solo prove) | [f5-10 §19.5](f5-10-sezione.md#195-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |

### 10.4 Vista XAML

V-a è nella [sezione 15.1](#151-separatore-decimale-e-v-a); V-f è con S1-b in 10.3.

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| SV-a | Schede della famiglia SLE | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Senza intestazioni visibili, come `sleTabs` storico (la famiglia si sceglie con «Combinazione SLE»); intestazione con il nome per l'accessibilità | Non indicata | nessuno sui risultati | [f5-10 §20.4](f5-10-sezione.md#204-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| SV-b | «PNG» ed «Espandi» | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Nella vista, come la cornice storica (`ViewportFrame`) | Un comando del foglio con `IFileDialogService`, come F5.11 | nessuno sui risultati | [f5-10 §20.4](f5-10-sezione.md#204-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| SV-c | «Proprietà / report…» | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Finestra non modale aperta dalla vista (`PropertiesRequested`), come la storica; nessun servizio comune (S1-a) | Non indicata | nessuno sui risultati | [f5-10 §20.4](f5-10-sezione.md#204-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| SV-d | Gruppi del pannello per i binding | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. `Inputs.Group` per chiave corta; l'elenco `Groups` resta | Non indicata | nessuno sui risultati | [f5-10 §20.4](f5-10-sezione.md#204-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| SV-e | Dialoghi «Nuovo materiale …», wizard, «Sollecitazioni…» | **Decisa 25/10, attuata 25/10 (passo d)** ([famiglie](f5-15-famiglie.md), famiglia 5): B: regola unica di `WpfDialogService`, dialogo ridimensionabile con una misura minima. Da attuare dopo il 28/10. Prima: Viste XAML nuove con le misure delle finestre storiche, sulla vista; il servizio dei dialoghi adatta la finestra | Non indicata | solo interfaccia | [f5-10 §20.4](f5-10-sezione.md#204-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| SV-f | Ospite del report breve e dell'esportazione | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. `MvvmSheetEditor.BuildShortReport` passa da `IModuleShortReport`; `ExportResult` ha il ramo della sezione c.a. Spostati in `SheetFileExports` con F5.13 (c). | Non indicata | nessuno sui risultati | [f5-10 §20.4](f5-10-sezione.md#204-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| V-b | Pulsanti e scelte che nella storica occupano tutta la colonna; il messaggio «In attesa dei dati dell'elemento.» centrato in altezza | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. Lasciati alla loro larghezza | Allargarli come la storica | solo interfaccia | [f5-10 §21.4](f5-10-sezione.md#214-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| V-c | Intestazioni della tabella del taglio tagliate a 1600 px, nelle due viste | **Decisa 25/10, attuata 25/10 (passo d)** ([famiglie](f5-15-famiglie.md), famiglia 3): B: larghezze dal contenuto. Da attuare dopo il 28/10. Prima: Come la storica | Larghezze dal contenuto | solo interfaccia | [f5-10 §21.4](f5-10-sezione.md#214-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| V-d | Etichette delle voci delle scelte | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. `FieldForm.ChoiceLabels`, proprietà additiva del file condiviso | Le etichette nel ViewModel (`FieldSpec` con le etichette delle voci) | nessuno sui risultati | [f5-10 §21.4](f5-10-sezione.md#214-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| V-e | La riga 1120 di `percorso-f3/sle-fessurazione-assi` (congelata) contiene un messaggio transitorio: con la macchina carica `ui/check-sezione-historic` cade (una volta su tre il 22/10) | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 3): A, voce chiusa: la prova storica è archiviata dal 23/10. Nessun cambiamento: si ripete da sola | La cattura storica aspetta l'ispezione prima di scrivere lo stato; traccia da ricatturare (decisione dell'utente) | nessuno sui risultati (alternativa: tocca riferimenti congelati) | [f5-10 §21.4](f5-10-sezione.md#214-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| V-g | Con le barre per coordinate il form dell'armatura è spento, ma la traccia storica fuori schermo, con il gruppo chiuso e mai disposto, ha scritto `bottom_bar_count` | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 2): A, voce chiusa: la vista storica è archiviata dal 23/10. Il passo si fa sul campo del ViewModel ed è elencato; la traccia congelata non cambia | Ricatturare la traccia storica con i form disposti (decisione dell'utente) | nessuno sui risultati (alternativa: tocca riferimenti congelati) | [f5-10 §22.5](f5-10-sezione.md#225-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| V-h | Per guidare un controllo non mostrato la prova lo dispone fuori dall'albero visuale | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Tenuta, limitata al controllo cercato, come F5.11 e i muri | Non indicata | nessuno sui risultati (solo prove) | [f5-10 §22.5](f5-10-sezione.md#225-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |

## 11. F5.11 · Sezione composta da ponte

Le decisioni del 17/10 valgono anche qui (T2, T6, T7 e le convenzioni delle viste). Fonti: [f5-11 §4](f5-11-sezione-mista.md#4-domande-per-il-punto-di-controllo-f511-da-decidere)
(SM1–SM9) e [§5.6](f5-11-sezione-mista.md#56-differenze-visibili-e-scelte-da-confermare-da-decidere-con-la-scelta-fatta) (SM10–SM16).

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| SM1 | Calcolo | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Sullo sfondo: 500 ms dopo l'ultima modifica, «Calcola» immediato, limitatore dell'applicazione, «occupato» fino al ritorno del motore, come oggi | Non indicata | nessuno sui risultati | §4 |
| SM2 | Riferimento F3 | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. `expected/esito.txt` da 104 a 111, nel manifest (`fixture_aggiunte_f5_11`), come T1 | Una voce in `transitions-f5.json` (T1) | nessuno sui risultati | §4 |
| SM3 | Vista staccata | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. Il ViewModel sa solo se il disegno è staccato e che cosa cambia nel pannello; la finestra (titolo, misure, F11, «Porta in primo piano», «Riaggancia») è della vista, senza membri nuovi di `ISheetHost` | Non indicata | solo interfaccia | §4 |
| SM4 | Schede del riepilogo | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Riusa `ConcreteCardsViewModel` di F5.10 | Spostarlo in `Modules/Shared` con un nome comune | nessuno sui risultati | §4 |
| SM5 | Comportamenti storici (sezione 2, punti 1–5 e 7) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): B per il punto 3 (risultati e disegno della situazione scelta durante un calcolo), A per gli altri punti. Correzione come differenza dichiarata, con la ricattura dei riferimenti nel commit di V-a. Da attuare dopo il 28/10. Prima: Conservati (T7) | Correggerli dopo il passaggio, con una nuova cattura | nessuno sui risultati (alternativa: comportamento diverso, tocca riferimenti congelati) | §4; [§2](f5-11-sezione-mista.md#2-comportamenti-della-vista-storica-trovati) |
| SM6 | Prove nello stadio `fast` | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. `CompositeSectionSheetChecks` (circa un minuto, tracce e F3 compresi) nello stadio `fast`, come i muri | Tracce e F3 nello stadio `ui` con un'opzione, come C7 | nessuno sui risultati (solo prove) | §4 |
| SM7 | Suggerimenti degli editor (`EditorToolTips`, ΔT delle fasi), che la storica sostituisce senza toccare l'etichetta | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. La vista XAML li legge da `CompositeSectionFields` | `FieldSpec.ToolTip`, che cambierebbe anche l'etichetta | solo interfaccia | §4 |
| SM8 | Baseline F4 dei disegni | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Resta a 3221 (Q3, come MU11) | Non indicata | nessuno sui risultati | §4 |
| SM9 | Contrasto a 96 DPI | Passo da fare, non una scelta. **Fatto il 24/10** con la G2: voci della Chiara alle chiavi della vista nuova, Scuro e Molto scuro 0 ([f5-13-shell §14.5](f5-13-shell.md#145-contrasto-d12)) | — | — | §4 |
| SM10 | Celle della tabella delle azioni | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Scritte a ogni tasto (`UpdateSourceTrigger=PropertyChanged`, convenzione delle tabelle MVVM di F5.4): risultato uguale, schede delle fasi ricostruite a ogni tasto | Scrittura alla conferma della cella, come la storica, con un modo per confermarla al `Commit` del foglio | nessuno sui risultati | §5.6 |
| SM11 | Parti mostrate nell'albero visuale | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Solo la pagina mostrata, la sua colonna degli ingressi e il suo pannello laterale, come la storica | Tutto nell'albero con la visibilità: cambierebbe la visita F3 | nessuno sui risultati (alternativa: tocca riferimenti congelati) | §5.6 |
| SM12 | Casella «CLS ×» | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Non legata al ViewModel: il code-behind mostra il fattore del foglio quando la casella non ha il fuoco, come la storica | Un legame semplice, che sovrascriverebbe il testo mentre si scrive | nessuno sui risultati | §5.6 |
| SM13 | «Calcola curva» è un comando asincrono | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. Resta spento fino alla fine del comando, un istante dopo la fine del calcolo | Non indicata | solo interfaccia (non visibile) | §5.6 |
| SM14 | Tabelle dei pannelli dei risultati | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Riusate con le righe nuove | Tabelle nuove a ogni risultato, come la storica | nessuno sui risultati | §5.6 |
| SM15 | Schede delle proprietà | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 8): A, confermata. Rigenerate tutte a ogni modifica dei dati; la scheda scelta resta | Tenere le prime due e rifare quelle delle fasi, come la storica | nessuno sui risultati | §5.6 |
| SM16 | Testi fissi di «Info modello…» | **Chiusa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 1): superata dai fatti. Prima: Nella vista XAML, confrontati con `BridgeInformation.cs` finché esiste | Spostarli in Application e farli leggere anche alla vista storica | nessuno sui risultati | §5.6 |

## 12. F5.12 · Bridge Design e prezzari

Le decisioni del 17/10 valgono anche qui. Fonti: [f5-12 §4](f5-12-bridge-design.md#4-domande-per-il-punto-di-controllo-f512-da-decidere) (BD1–BD13)
e [§5.6](f5-12-bridge-design.md#56-differenze-visibili-e-scelte-da-confermare-da-decidere-con-la-scelta-fatta) (BD14–BD22).

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| BD1 | Calcolo | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Sullo sfondo: 450 ms dopo l'ultima modifica, subito all'apertura e con «Calcola», limitatore dell'applicazione, «occupato» fino al ritorno del motore | Non indicata | nessuno sui risultati | §4 |
| BD2 | Riferimento F3 | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. `expected/esito.txt` da 111 a 119, nel manifest (`fixture_aggiunte_f5_12`), come T1 | Una voce in `transitions-f5.json` (T1) | nessuno sui risultati | §4 |
| BD3 | «Occupato» per la finestra: la vista storica non dà a `SheetEditor` il suo stato occupato | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 5): A, confermata. `IsBusy` vero durante il calcolo, come gli altri moduli: con l'ospite MVVM la finestra chiede di attendere | `IsBusy` sempre falso, come oggi | comportamento diverso dalla vista storica | §4; [§2](f5-12-bridge-design.md#2-comportamenti-della-vista-storica-trovati); registro F5-58 |
| BD4 | Prezzario in una revisione in sola lettura: la storica ammette l'editor e «Applica prezzario» scrive nella revisione | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 6): B: il prezzario si apre in sola lettura, senza «Applica». Da attuare dopo il 28/10. Prima: Il ViewModel rifiuta l'editor, come MU3 | Aprirlo in sola lettura, senza «Applica» | comportamento diverso dalla vista storica | §4; [§2](f5-12-bridge-design.md#2-comportamenti-della-vista-storica-trovati); registro F5-59 |
| BD5 | Editor dei prezzari in Presentation (Q17 a) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Il ViewModel passa in Presentation; la logica resta in Application (`PriceCatalogueEditing`); Application senza CommunityToolkit | Tipi specchio dei prezzi in Application e logica nel ViewModel | nessuno sui risultati | §4 |
| BD6 | Parametri con il cursore | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. Elementi espliciti (`BridgeDesignParameter`: testo grezzo, bordo rosso, cursore), non `FieldForm`; F1 apre la guida del modulo | Estendere `FieldSpec` con il cursore | solo interfaccia | §4 |
| BD7 | `dati` nel JSON del risultato esportato | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. `Result` del ViewModel invariato; l'ospite MVVM aggiunge `dati` come `SheetEditor` | Non indicata | nessuno sui risultati | §4 |
| BD8 | «Stampa scheda» | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Resta alla vista (stampa di WPF); il ViewModel dà i testi della pagina (`PrintLines`) | Non indicata | nessuno sui risultati | §4 |
| BD9 | Chiave `scene` («Nuovo paesaggio», «Ponte casuale») | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Stato dell'interfaccia del modulo, scritta come una modifica dei dati (ricalcolo, «Annulla»), come oggi | Non indicata | nessuno sui risultati | §4 |
| BD10 | Comportamenti storici (sezione 2, punti 3–8) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 6): B per il punto 3 (bordo rosso dopo «Annulla») e il punto 5 (con T6), A per gli altri punti. Correzione come differenza dichiarata, con la ricattura dei riferimenti nel commit di V-a. Da attuare dopo il 28/10. Prima: Conservati (T7) | Correggerli dopo il passaggio, con una nuova cattura | nessuno sui risultati (alternativa: comportamento diverso, tocca riferimenti congelati) | §4 |
| BD11 | Prove nello stadio `fast` | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. `BridgeDesignSheetChecks` nello stadio `fast`: circa 40 s in più | Tracce e F3 nello stadio `ui`, come C7 | nessuno sui risultati (solo prove) | §4 |
| BD12 | Baseline F4 dei disegni | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Le voci dei disegni restano (Q3, come MU11 e SM8) | Non indicata | nessuno sui risultati | §4 |
| BD13 | Contrasto a 96 DPI | Passo da fare, non una scelta. **Fatto il 24/10** con la G2: voci della Chiara alle chiavi della vista nuova, Scuro e Molto scuro 0 ([f5-13-shell §14.5](f5-13-shell.md#145-contrasto-d12)) | — | — | §4 |
| BD14 | Corpi | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. Valori delle metriche da 26 a `Font.Title` (22), per non tagliarli; 15 e 17 a `Font.Heading` (16) | `Font.Display` (30) per le metriche | solo interfaccia | §5.6 |
| BD15 | Colori | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 4): A, confermata. I token più vicini: `Token.Border`, `Token.Accent`, `Token.Note.Foreground`, `Token.Status.Fail`, `Token.OnCommandBar.Muted` | Token nuovi per `#BCCFE2` e `#CD5C5C` | solo interfaccia | §5.6 |
| BD16 | Riquadri dei pannelli | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. Misure delle tabelle storiche riconosciute dalle colonne (informazione della vista); titolo e nota del prezzario nel riquadro che scorre | Le misure come dati degli elementi del ViewModel | solo interfaccia | §5.6 |
| BD17 | File del prezzario | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Comandi del ViewModel dell'editor sul servizio dei file del foglio; la finestra storica tiene i suoi dialoghi | Dialoghi nel code-behind della vista | nessuno sui risultati | §5.6 |
| BD18 | Pagina di «Stampa scheda» | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. `BridgeDesignPrintPage` in `X.Desktop/Wpf`, con i corpi e i colori storici: è un documento stampato in chiaro | Una pagina XAML con i token | nessuno sui risultati | §5.6 |
| BD19 | Blocchi e limiti della ricerca | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Dizionari del ViewModel, legami a una via, scrittura con `SetLock`, `SetLimit` e `LeaveLimit` | Un elemento del ViewModel per blocco e per limite | nessuno sui risultati | §5.6 |
| BD20 | Cursore | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. `BridgeDesignSlider` senza legame a due vie sul valore, con l'intervallo impostato prima del valore | Legame a due vie, affidato all'ordine degli attributi | nessuno sui risultati | §5.6 |
| BD21 | «Occupato» nella vista | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. Nessuna barra di avanzamento, come la storica; la riga di stato dice «Aggiornamento del ponte e delle alternative…» | La barra indeterminata delle altre viste | solo interfaccia | §5.6 |
| BD22 | Finestra del prezzario | **Decisa 25/10, attuata 25/10 (passo d)** ([famiglie](f5-15-famiglie.md), famiglia 5): B: regola unica di `WpfDialogService`, dialogo ridimensionabile con una misura minima. Da attuare dopo il 28/10. Prima: Misura dalla vista (1334 × 741), finestra adattata al contenuto (`SizeToContent`), come i dialoghi dei muri | Non indicata | solo interfaccia | §5.6 |

## 13. F5.13 · Shell

Fonte: [f5-13-shell.md](f5-13-shell.md). La Release apre ancora `MainWindow`; l'impatto è rispetto a `MainWindow`.

### 13.1 Parti (a) e (b): SH1–SH22

Le scelte di [§6](f5-13-shell.md#6-domande-da-decidere) sono provvisorie con il criterio prudente. SH5–SH22 sono decise dall'utente il
22/10 sera con la bozza ([§9.2](f5-13-shell.md#92-scelte-per-lutente)).

**Dubbio su SH1–SH4 (Q9, Q10, Q13, Q18).** Dovevano passare dal punto di controllo della bozza. La frase dell'approvazione in §9.2 dice
«SH5–SH32 valgono con le opzioni prese nella bozza (… SH1–SH4, SH7, SH8, SH10–SH14, SH16 come nella sezione 6)». Il commit bfe1f875
cita solo SH5–SH32. La bozza approvata presuppone SH2 (finestre staccate, SH29) e SH4 (Wiki in una scheda, SH26). Qui restano aperte,
da confermare.

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| SH1 | Q9, dati condivisi con più schede | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. (a) della proposta: conferma alla perdita del fuoco, al più un foglio con modifiche non confermate | (b) propagazione per campo anche verso schede con bozze, con la gestione dei conflitti | nessuno sui risultati | §6 |
| SH2 | Q10, schede staccabili | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. (a) realizzazione nostra (`IWindowService`) | (b) una libreria di docking | solo interfaccia (funzione chiesta dall'utente l'8/10) | §6 |
| SH3 | Q13, annulla per campo | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. (a) no: resta l'annulla/ripeti del progetto | (b) annulla per foglio sulle modifiche dei campi (funzione nuova) | nessuno sui risultati | §6 |
| SH4 | Q18, Wiki nella shell | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. (a) scheda | (b) pagina della navigazione come oggi; (c) pannello laterale | comportamento diverso dalla vista storica | §6 |
| SH5 | Annulla/ripeti con una scheda il cui foglio non esiste più | **Decisa 22/10, confermata 24/10**: la scheda si chiude con l'avviso «Il foglio «nome» non esiste più nel progetto: la scheda è stata chiusa.»; le altre schede restano aperte | `MainWindow`: annulla chiude sempre il foglio aperto | comportamento diverso dalla vista storica | §6; §9.2 |
| SH6 | Anteprima di una revisione con più schede | **Decisa 22/10, confermata 24/10**: di tutto il progetto; le schede passano in sola lettura al foglio con lo stesso identificativo; quelle senza foglio nell'istantanea si chiudono senza avviso | Non indicata | comportamento diverso dalla vista storica (più schede) | §6; §9.2 |
| SH7 | Navigazione durante l'anteprima | **Decisa 22/10**: ogni pagina esce dall'anteprima, come `SelectNavigation` | — | nessuno sui risultati | §6 |
| SH8 | Chiusura con più documenti non salvati | **Decisa 22/10**: una domanda per documento, prima il progetto e poi i file singoli nell'ordine di apertura, con il foglio attivato prima della domanda; testo invariato | — | nessuno sui risultati | §6 |
| SH9 | Avviso nella scheda ricaricata per i dati condivisi | **Decisa 22/10**: il ViewModel espone foglio di origine e fogli ricaricati; testo e forma in SH42 | — | comportamento diverso dalla vista storica (avviso nuovo) | §6; §9.2 |
| SH10 | Copia verso un altro modulo fuori da un progetto | **Decisa 22/10**: scheda singola non salvata, con il nome e lo stato «non salvato» di oggi | — | nessuno sui risultati | §6 |
| SH11 | Stesso file aperto due volte | **Decisa 22/10**: si attiva la sua scheda; percorsi confrontati per intero, senza distinguere le maiuscole | — | nessuno sui risultati | §6 |
| SH12 | «Nuovo …» con un file singolo aperto | **Decisa 22/10, confermata 24/10**: scheda nuova, nessuna domanda di salvataggio | `MainWindow`: il documento si sostituisce dopo la domanda | comportamento diverso dalla vista storica | §6 |
| SH13 | «Riprendi» della Home e del catalogo con più schede | **Decisa 22/10**: riferito alla scheda attiva | — | nessuno sui risultati | §6 |
| SH14 | Barra delle revisioni con più schede | **Decisa 22/10**: segue la scheda attiva del progetto o la sezione selezionata | — | nessuno sui risultati | §6 |
| SH15 | Marcatore del foglio non salvato | **Decisa 22/10**: «•» dopo il titolo, nella scheda e nell'albero, colore `Token.Accent` | — | solo interfaccia | §6; §9.2 |
| SH16 | Annulla/ripeti dei file singoli | **Decisa 22/10**: nessuno, come oggi | — | nessuno sui risultati | §6 |
| SH17 | Scheda i cui dati cambiano fuori da essa | **Decisa 22/10, confermata 24/10**: ViewModel ricreato sui dati del documento, come oggi l'editor | `Reload` (proposta 5.1): conserverebbe lo stato della vista, ma salterebbe le preparazioni che alcuni moduli fanno all'apertura | nessuno sui risultati (lo stato della vista si perde: vedi SH49) | §6; §9.2 |
| SH18 | Avviso delle schede chiuse dall'annulla | **Decisa 22/10, confermata 24/10**: un solo messaggio «ANTHEA» con una riga per scheda, testo di SH5 | — | comportamento diverso dalla vista storica | §6; §9.2 |
| SH19 | Finestra in cui si apre una scheda nuova | **Decisa 22/10**: quella con il fuoco, la principale o una staccata | — | solo interfaccia | §6; §9.2 |
| SH20 | Finestra secondaria rimasta senza schede | **Decisa 22/10**: si chiude; chiudere una finestra con schede le riporta nella principale | — | solo interfaccia | §6; §9.2 |
| SH21 | Destinatario di Salva, report, risultati ed esporta foglio | **Decisa 22/10**: il foglio con il fuoco e il suo documento; senza fogli Salva agisce sul progetto, senza documenti non fa nulla | — | nessuno sui risultati | §6; §9.2 |
| SH22 | Pagina «Progetti» senza progetto | **Decisa 22/10, confermata 24/10**: apre un archivio nuovo senza domande, perché i file singoli restano aperti | `MainWindow`: `NewProjects` chiede di salvare il documento sostituito | comportamento diverso dalla vista storica | §6; §9.2 |

### 13.2 Bozza della disposizione: SH23–SH32

Tutte **decise il 22/10 sera** con la bozza ([§9.2](f5-13-shell.md#92-scelte-per-lutente)).

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| SH23 | Barra di navigazione | **Decisa 22/10**: sempre visibile | `MainWindow`: solo nelle pagine; nel foglio e nel progetto «← Torna…» | solo interfaccia | §9.2 |
| SH24 | Albero del progetto | **Decisa 22/10**: colonna della shell, visibile con un archivio aperto anche sulle pagine, «▸» per chiuderla | `MainWindow`: a sinistra nell'area progetti, ripiegabile nel foglio | solo interfaccia | §9.2 |
| SH25 | Pagine e schede | **Decisa 22/10**: la pagina scelta prende il centro; le schede restano nella barra, senza fuoco; nessuna scheda «Home» | — | solo interfaccia | §9.2 |
| SH26 | Wiki | **Decisa 22/10**: scheda (Q18 a); mentre è mostrata nessuna voce della navigazione è evidenziata | — | comportamento diverso dalla vista storica | §9.2 |
| SH27 | Modelli | **Decisa 22/10**: una scheda per modello; «+ Modello» apre una scheda «Nuovo modello» per sezione | `MainWindow`: una pagina che sostituisce la precedente | comportamento diverso dalla vista storica | §9.2 |
| SH28 | Esempio della Wiki che apre un modulo | **Decisa 22/10, confermata 24/10**: scheda nuova non salvata, senza domanda, come SH12 | `MainWindow`: sostituisce il documento dopo la domanda | comportamento diverso dalla vista storica | §9.2 |
| SH29 | Finestre staccate | **Decisa 22/10**: non possedute dalla principale (restano su un altro monitor anche con la principale ridotta a icona); si chiudono con la principale | — | solo interfaccia | §9.2 |
| SH30 | «← Torna…» nella barra del foglio | **Decisa 22/10**: resta, anche con la navigazione sempre visibile | — | nessuno sui risultati | §9.2 |
| SH31 | Annulla e ripristina | **Decisa 22/10**: nella barra del progetto e, con un foglio del progetto, anche in quella del foglio | `MainWindow`: nell'una o nell'altra | solo interfaccia | §9.2 |
| SH32 | Archivio aperto | **Decisa 22/10**: mostra la pagina «Progetti», come oggi | — | nessuno sui risultati | §9.2 |

### 13.3 Dopo il punto di controllo: SH33–SH47

Scelte provvisorie con il criterio prudente. SH33–SH37 sono della sera del 22/10 ([§9.5](f5-13-shell.md#95-dopo-il-punto-di-controllo-dallalbero-in-una-nuova-finestra-o-a-pagina-intera)),
SH38–SH47 della notte del 22–23/10 ([§9.5](f5-13-shell.md#95-dopo-il-punto-di-controllo-dallalbero-in-una-nuova-finestra-o-a-pagina-intera) e
[§10.2](f5-13-shell.md#102-il-resto-della-parte-c-notte-del-222310)).

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| SH33 | Che cosa nasconde la pagina intera | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. Solo navigazione e albero, come chiesto; restano menu, schede, barre del progetto, delle revisioni e del foglio, riga di stato e «▸» | Non indicata | solo interfaccia (funzione chiesta dall'utente il 22/10) | §9.5 |
| SH34 | «Apri a pagina intera» di un foglio in una finestra staccata | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. Quella finestra in primo piano, senza riagganciare né cambiare la principale | Non indicata | solo interfaccia | §9.5 |
| SH35 | Quando la pagina intera finisce da sola | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. Quando sparirebbe la barra con «Esci»: una pagina della navigazione, «← Torna…», una scheda senza foglio, nessun foglio con il fuoco. Il fuoco su una finestra staccata non la fa finire. | Non indicata | solo interfaccia | §9.5 |
| SH36 | Pagina intera e cambio di scheda | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 8): A, confermata. Stato della finestra, non del foglio: resta passando a un'altra scheda di un foglio; non si ricorda fra le sessioni | Non indicata | solo interfaccia | §9.5 |
| SH37 | Voci nuove nel menu dei nodi che non sono fogli | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. Nascoste | Disabilitate, come le altre voci della bozza | solo interfaccia | §9.5 |
| SH38 | Badge di coerenza nell'albero | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. Pulsante con il numero di conflitti e avvisi o «✓», che apre il confronto della sezione, come `AddCoherenceBadge` | Il titolo come testo senza comando, come nella bozza | nessuno sui risultati (come `MainWindow`) | §9.5 |
| SH39 | Elenco dei moduli la cui vista segue la finestra | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Tabella della shell in Presentation (`SheetLayout.FitsWindow`) | Un membro dei descrittori dei moduli (proposta 4.3), spostabile al passaggio della Release | nessuno sui risultati | §9.5 |
| SH40 | Finestre piccole | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. La shell tiene 1120 × 680 e scorre; le pagine 870 × 680; i fogli che non seguono la finestra 1120 × 600 dentro la scheda | Chiudere da solo l'albero o passare alla pagina intera sotto una certa larghezza | solo interfaccia | §9.5 |
| SH41 | Moduli ancora storici nella shell nuova (R5 della proposta) | **Chiusa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 1): superata dai fatti. Prima: Si aprono come li sceglie la Release: i 12 storici nel loro `SheetEditor` dentro la scheda (`HistoricSheetViewModel`), gli altri con il ViewModel | Non indicata | nessuno sui risultati | §9.5; §10.2 |
| SH42 | Testo e forma dell'avviso nella scheda ricaricata (SH9) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. «Aggiornato da «foglio»: campo, campo e altre n proprietà.» in una riga sotto la barra del foglio, fondo `Token.Calculated`; solo dopo la conferma di un altro foglio con i dati condivisi; resta finché la scheda non è confermata | Non indicata | comportamento diverso dalla vista storica (avviso nuovo) | §9.5 |
| SH43 | Clic singolo sul nome di un modello nell'albero | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. Lo apre dopo il tempo del doppio clic, come le sezioni; il doppio clic lo apre comunque | `MainWindow`: il nodo era un pulsante e si apriva subito | comportamento diverso dalla vista storica | §9.5 |
| SH44 | Indicatore del trascinamento | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. Linea di due pixel `Token.Accent`; per l'inserimento in una sezione il contorno della riga invece del fondo `#D4E6F7` | Il fondo storico | solo interfaccia | §9.5 |
| SH45 | Grandezza dei nomi nell'albero | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 3): A, confermata. Sui token: progetti 22 (oggi 20), sezioni di primo livello 16 (17), altre sezioni e fogli 14 (15 al secondo livello), fogli non in grassetto | I corpi storici | solo interfaccia | §9.5 |
| SH46 | Dialoghi della shell (confronto, collegamento dei sondaggi, revisioni, nuova revisione, contenuti del report Word) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. In `ShellDialogs.xaml` nel dizionario dell'applicazione, mostrati dal servizio dei dialoghi; il confronto conferma prima la scheda attiva, come `ShowCoherence` | Non indicata | solo interfaccia | §9.5 |
| SH47 | F1 e «?» dei fogli storici nella shell | **Chiusa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 1): superata dai fatti. Prima: `WikiContextHelp` cerca dopo `MainWindow` una finestra della shell e apre la scheda Wiki; nella Release nulla cambia | Non indicata | nessuno sui risultati | §9.5 |

### 13.4 Parte (e): SH48–SH54

Fonte: [§12.6](f5-13-shell.md#126-domande-da-decidere).

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| SH48 | Albero sul riepilogo dopo che l'utente l'ha chiuso su un foglio | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 8): A, confermata. Resta chiuso: lo stato è della finestra (SH24) e «▸» lo riapre | Riaprirlo da solo sul riepilogo, come `MainWindow` | comportamento diverso dalla vista storica | §12.6 |
| SH49 | Stato tenuto solo dalla vista XAML quando la scheda torna a schermo | **Decisa 24/10, attuata 25/10 (passo c)** ([scheda](f5-15-prioritarie.md), voce 8): B ridotta: la sotto-scheda scelta resta, in tutti i moduli. Da attuare dopo il 28/10. Prima: La vista si ricostruisce; resta lo stato del ViewModel (dati, risultati, pannello esteso, vista della capacità); la scheda dei risultati del palo verticale torna alla prima | Portare nel ViewModel del modulo lo stato da conservare, modulo per modulo (proposta della nota) | comportamento diverso dalla vista storica (`MainWindow` teneva l'editor) | §12.6 |
| SH50 | Report di una sezione dall'albero | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. «Report Word della sezione» nel menu del nodo e «Genera report» nel riepilogo, come la bozza approvata | Un pulsante per nodo, come `MainWindow` | solo interfaccia | §12.6 |
| SH51 | Chiusura della finestra «Generazione del report» mentre lavora | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Annulla al termine del calcolo in corso e la finestra resta, come `MainWindow` (`IGuardedDialog`) | Non indicata | nessuno sui risultati | §12.6 |
| SH52 | Schede chiuse dall'anteprima di una revisione | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. Si riaprono tutte sulla versione corrente, nel loro ordine; quella con il fuoco lo riprende dopo un cambio della barra | Non indicata | nessuno sui risultati (come `MainWindow`, esteso alle schede) | §12.6 |
| SH53 | Foglio con il fuoco chiuso da una revisione o da annulla | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. Riepilogo della sezione delle revisioni, altrimenti di quella del foglio, come `MainWindow` | — | nessuno sui risultati | §12.6 |
| SH54 | Finestra principale attivata mentre mostra una pagina | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A, confermata. La pagina resta e nessun foglio ha il fuoco; quello di una finestra staccata è confermato | Non indicata | nessuno sui risultati | §12.6 |

### 13.5 Parte (d): D-1…D-5

Fonti: [§11.6](f5-13-shell.md#116-domande-da-decidere) e [§11.7.2](f5-13-shell.md#1172-domanda-da-decidere).

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| D-1 | Liste di sezioni dei report di ponte e palificata (R1 e R2 di P3): array statici pubblici nel codice congelato dei report | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Non toccate: si rendono immutabili con la revisione dei report, a fine refactoring | Correggerle ora, toccando il codice congelato dei report | nessuno sui risultati | §11.6 |
| D-2 | La risposta elastica del palo orizzontale calcola su un thread di fondo senza passare dal limitatore (rischio R6) | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 4): A ora; il passaggio dal limitatore è una voce di infrastruttura dopo F5. Lasciata come oggi, come la vista storica | Farla passare dal limitatore, con le parti (e)–(g) o in F5.15 | nessuno sui risultati (carico con più schede aperte) | §11.6 |
| D-3 | Istanze condivise di tipi mutabili delle librerie, lette e mai scritte (§11.3) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Lasciate; seguito per le librerie | Norme e materiali immutabili, oppure un'istanza per calcolo | nessuno sui risultati | §11.6 |
| D-4 | Istante scritto all'apertura di Bridge Design nei prezzi fissati: non è un risultato, ma cambia a ogni esecuzione | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Escluso dal confronto della prova di uscita, come nella cattura F3 | Non indicata | nessuno sui risultati (solo prove) | §11.6 |
| D-5 | La sezione c.a. calcola i suoi lotti fuori dal limitatore: `Task.Run` per preparazione, domini e tensioni; resistenze rapide, σ/ε, M–χ e ricerca delle armature con pianificatori propri | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 4): A ora; il passaggio dal limitatore è una voce di infrastruttura dopo F5. Lasciata come oggi (C1 conserva l'orchestrazione storica); la prova conta i lotti a parte e prova che i risultati non cambiano | Far passare i lotti dal limitatore, con le parti (e)–(g) o in F5.15 | nessuno sui risultati (carico con molte sezioni aperte) | §11.7.2 |

## 14. Registro delle differenze R4–R21

Fonte: [registro-differenze.json](registro-differenze.json), voci R4–R14 e R16–R21, tutte in stato «da-decidere». La decisione del 7/10
è «le voci sfavorevoli le guardiamo a fine refactoring. segna di ridiscuterne». R16–R21 sono state aggiunte dal coordinatore su delega;
il rinvio è ratificato dall'utente il 15/10 (E4). R15 è corretta con la regola scelta dall'utente il 7/10 («Altezza lungo il gradiente»)
e non si rivede.

L'opzione presa è il codice com'è oggi. L'alternativa è la raccomandazione del registro. Quasi tutte toccano le librerie GPC (Checker o
Model): una correzione chiede un set coerente nuovo e la ricattura delle catture congelate coinvolte.

| ID | Famiglia | Domanda | Opzione presa | Alternative | Segno ed effetto in ANTHEA | Impatto |
| --- | --- | --- | --- | --- | --- | --- |
| R4 | taglio | θmin del profilo MC2010 fisso a 20° invece di 20° + 10 000 εx (MC2010 §7.3.3.3)? | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 13): correggere nella libreria con opzione legacy, dopo il riscontro del testo, nel set successivo all'unione di Codex. Prima: θmin = 20° | Riscontrare il testo e correggere, con opzione legacy (priorità alta) | NON cautelativo: VRd +38,8 % nell'esempio S5. Tocca ANTHEA con il profilo «Model Code 2010»; NTC non coinvolto. | risultati di calcolo |
| R5 | SLE | Tensioni di esercizio con l'analisi non lineare calcolate con le leggi di progetto, senza avviso | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 13): leggi di esercizio nell'analisi non lineare, nella libreria, nel set successivo all'unione di Codex. Prima: Legami di progetto. L'8/10 l'utente ha detto «ad ora lascialo così» e ha chiesto avviso e «?», fatti. | Leggi di esercizio nell'analisi non lineare, oppure rifiuto con avviso (priorità alta) | NON cautelativo sul calcestruzzo: σc −31 % a 80 kNm. In ANTHEA solo se l'utente sceglie «Non lineare». | risultati di calcolo |
| R6 | SLE | DIN, k5 delle tensioni nei trefoli 0,75 invece di 0,65 (da una fonte secondaria) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 11): riscontrare il NA DIN, poi correggere in Model. Nel set di librerie con R4, R5 e R10, con opzione legacy, voce del registro e full. Prima: 0,75 | Riscontrare l'NA; se confermato, correggere in Model | NON cautelativo se confermato; effetto in ANTHEA non indicato | risultati di calcolo |
| R7 | calcestruzzo | Interpretazioni cautelative o dichiarate: interazione delle bielle in torsione, k3 sul valore assoluto di σ, k1 fck, k2 fck, NS senza kc, DS cot θ ≤ 2, wk = 0 con l'asse neutro nel copriferro, 1,3 (h − x) con barre fuori da Ac,eff | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 11): dichiararle anche nella guida teorica; stabilire il segno di F-4. Nel set di librerie con R4, R5 e R10, con opzione legacy, voce del registro e full. Prima: Conservate e dichiarate nei riquadri delle pagine dei metodi | Dichiararle anche nella guida teorica; per il limite 1,3 (h − x) (F-4) stabilire il segno | Cautelative, tranne F-4 (segno da stabilire). Banco F2.1: il legacy applica già le due interpretazioni della fessurazione. | nessuno sui risultati (se solo dichiarate) |
| R8 | fessurazione | Punti da riscontrare: MC2010 senza ritiro εcs, coefficiente DIN di hc,ef (Bild 7.1DE), EN wmax 0,3 mm per XD3 | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 11): riscontrare sui testi primari, poi decidere voce per voce. Nel set di librerie con R4, R5 e R10, con opzione legacy, voce del registro e full. Prima: Codice com'è | Riscontrare sui testi primari | Segno da stabilire | risultati di calcolo (dopo il riscontro) |
| R9 | taglio | Limiti non controllati: VEd ≤ 0,5 bw d ν fcd (EN 6.2.2(6)), ν1 = 0,6 della nota 2 di 6.2.3(3), staffe di classe A (DK) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 11): aggiungere il controllo di EN 6.2.2(6); dichiarare ν1 e le staffe di classe A. Priorità alta. Nel set di librerie con R4, R5 e R10, con opzione legacy, voce del registro e full. Prima: Non controllati | Aggiungere il controllo 6.2.2(6); dichiarare gli altri due | NON cautelativo per carichi vicini agli appoggi; effetto in ANTHEA non indicato | risultati di calcolo |
| R10 | dettagli | fctk,0,05 non limitato alla classe C60/75 (EN 8.4.2(2)) negli ancoraggi | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 13): attuare il tetto anche nei muri, nel set successivo all'unione di Codex. Prima: Il limite è lasciato al chiamante: sezione c.a. e palo elastico lo applicano, i muri no. Per i muri l'utente ha già deciso l'8/10 («ok», proposta F2.8-U3): tetto anche nei muri, da attuare in F5.15. | Limite nella libreria, con opzione legacy | NON cautelativo oltre C60/75: lb,rqd −11 % con C90/105. In ANTHEA solo i muri oltre C60/75. | risultati di calcolo (muri oltre C60/75) |
| R11 | durabilità | NTC: Cmin di default uguale al valore di tabella, se il chiamante non passa la classe minima pertinente | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 11): correggere il default di Cmin. Priorità bassa. Nel set di librerie con R4, R5 e R10, con opzione legacy, voce del registro e full. Prima: Codice com'è | Default = classe minima pertinente dell'esposizione | NON cautelativo, fino a −5 mm, ma solo per gli altri utenti della libreria: tutti i chiamanti di ANTHEA passano la classe | nessuno sui risultati di ANTHEA |
| R12 | dettagli | Pilastri NTC: interasse delle barre misurato anche attraverso il nucleo | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 11): misura dell'interasse lungo il perimetro. Nel set di librerie con R4, R5 e R10, con opzione legacy, voce del registro e full. Prima: Codice com'è | Misura lungo il perimetro, con opzione legacy | Cautelativo: esito non soddisfatto indebito (498 mm calcolati contro 249 mm lungo le facce, limite 300). Per la capsula come pilastro l'interasse si misura già lungo il percorso delle barre ([capsula-pilastro.md](capsula-pilastro.md)). | risultati di calcolo (esiti dei pilastri) |
| R13 | dettagli | Interpretazioni cautelative: riduzione 0,6 delle staffe nelle sovrapposizioni EC anche con Ømax ≤ 14 mm; sovrapposizione NTC minima 200 mm; limite 0,08 Ac in NTC; nota 2 del prospetto 4.3N non implementata | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 11): correggere la riduzione 0,6; dichiarare le altre tre. Nel set di librerie con R4, R5 e R10, con opzione legacy, voce del registro e full. Prima: Codice com'è | Correggere la riduzione 0,6 (esito indebito); dichiarare gli altri tre | Cautelativo | risultati di calcolo |
| R14 | dettagli, M–χ | Regola dei 15Ø delle barre compresse sostituita da una conferma; M–χ senza bilineare equivalente né φ(M85 %); citazioni da correggere | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 11): correggere le citazioni e dichiarare il M–χ; i 15Ø come controllo dopo il riscontro. Nel set di librerie con R4, R5 e R10, con opzione legacy, voce del registro e full. Prima: Codice com'è | Implementare i 15Ø come controllo; dichiarare le definizioni del M–χ con la bilineare come opzione; correggere le citazioni | 15Ø: segno da stabilire; citazioni senza effetto | risultati di calcolo (15Ø) |
| R16 | torsione | DS: fattore delle bielle νv del taglio invece di νt (5.104 NA) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 11): correggere (νt). Priorità bassa. Nel set di librerie con R4, R5 e R10, con opzione legacy, voce del registro e full. Prima: νv | νt per i profili non armati sulle due facce e media pesata con taglio e torsione insieme, con opzione legacy | NON cautelativo: TRcd da 1,43 a 1,61 volte. Nessuno oggi in ANTHEA: la torsione è solo NTC. | nessuno sui risultati di ANTHEA (oggi) |
| R17 | durabilità | Classe indicativa di XC3 nei profili EN e UNI: C25/30 invece di C30/37 (prospetto E.1N) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 11): correggere dopo il riscontro dell'impaginazione della EN. Priorità bassa. Nel set di librerie con R4, R5 e R10, con opzione legacy, voce del registro e full. Prima: C25/30 | C30/37 dopo il riscontro dell'impaginazione della EN, con opzione legacy; attesi dei test corretti dal testo | NON cautelativo. Nessuno in ANTHEA, che usa la classe UNI 11104. | nessuno sui risultati di ANTHEA |
| R18 | M–χ | Acciaio di progetto con incrudimento: ramo superiore riferito a fyk invece che a fyd (EN 3.2.7(2)) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 11): correggere in Model, con la ricattura delle fixture coinvolte. Priorità alta. Nel set di librerie con R4, R5 e R10, con opzione legacy, voce del registro e full. Prima: Codice com'è | Ramo inclinato della norma in Model, con opzione legacy, e ricattura delle fixture coinvolte | NON cautelativo: circa 8 MPa (+2 %) in εud per B450C. Effetto in ANTHEA da verificare. | risultati di calcolo; tocca riferimenti congelati |
| R19 | torsione | Citazioni restituite per NTC 2018 ([4.1.27]-[4.1.32] invece di [4.1.34]-[4.1.40]) e DS (6.7a invece di 6.7b NA) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 11): correggere il testo delle citazioni. Nel set di librerie con R4, R5 e R10, con opzione legacy, voce del registro e full. Prima: Testo com'è | Correggere il testo nella prossima release di GPCChecker.Concrete | Nessun effetto numerico | nessuno sui risultati (solo testo) |
| R20 | dettagli | DS, travi: ρw,min nazionale non implementato (`NotImplemented`) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 11): implementare ρw,min quando serve il DS delle travi. Priorità bassa. Nel set di librerie con R4, R5 e R10, con opzione legacy, voce del registro e full. Prima: In sospeso | Implementare ρw,min = 0,063 √fck/fyk, poi As,min di 9.2.1.1(1) e la regola 9.5.3(6) | Nessun esito errato: l'esito DS delle travi resta in sospeso | risultati di calcolo (DS) |
| R21 | fessurazione | DIN, hc,eff: la condizione (h − x)/3 ≥ c + 20 mm usa `NominalCover` anche con `CoverOverride` | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 11): correggere (stesso copriferro della formula di wk). Nel set di librerie con R4, R5 e R10, con opzione legacy, voce del registro e full. Prima: Codice com'è | Lo stesso copriferro della formula di wk, con opzione legacy | Segno da stabilire; nessun effetto senza copriferro assegnato | risultati di calcolo (DIN con copriferro assegnato) |

## 15. Voci trasversali

### 15.1 Separatore decimale e V-a

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| V-a | Riferimenti congelati e separatore decimale dell'utente. La lista dei disegni e le tracce congelate della sezione c.a. contengono `θ = 30.00°`: la cattura storica imposta `it-IT` solo nel flusso e la macchina del banco usa il punto. Su una macchina con la virgola cattura storica, tracce del ViewModel e disegni darebbero `θ = 30,00°` e cadrebbero. | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 1): B: impostazione «Sistema / Virgola / Punto», prove con «Virgola» e ricattura motivata dei riferimenti con il punto. Da attuare dopo il 28/10. Prima: Nessun riferimento cambiato; la suite riproduce lo stato di cultura della cattura | Catture e prove con `it-IT` anche sul thread, con i riferimenti ricatturati (decisione dell'utente) | nessuno sui risultati (alternativa: tocca riferimenti congelati) | [f5-10 §21.4](f5-10-sezione.md#214-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) |
| §12.4-1, §13.4-3, §14.4-1 | Cultura dei numeri nella sezione c.a.: il dettaglio, la legenda della mappa e i parametri automatici del taglio hanno la virgola nei passi dell'utente della traccia e il punto dopo la pubblicazione di un calcolo (sulla macchina del banco) | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 1): B di V-a: la cultura fissata dalle prove all'avvio. Da attuare dopo il 28/10. Prima: Il ViewModel segue gli stessi percorsi e dà gli stessi testi; la vista XAML «dovrà fissare la cultura (da decidere in F5.15)» | Fissare la cultura dell'applicazione (15.2) | nessuno sui risultati (alternativa: tocca riferimenti congelati) | [f5-10 §12.4](f5-10-sezione.md#124-comportamenti-storici-trovati-in-s3-conservati-c5); [§13.4](f5-10-sezione.md#134-comportamenti-storici-trovati-in-s4-conservati-c5); [§14.4](f5-10-sezione.md#144-comportamenti-storici-trovati-in-s5-conservati-c5); registro F5-34 |
| Separatori | Proposta dell'utente del 22/10: «facciamo una impostazione di anthea per i separatori?» | **Decisa 24/10** ([scheda](f5-15-prioritarie.md), voce 1): B: impostazione «Sistema / Virgola / Punto», strada «a» della nota 15.2. Da attuare dopo il 28/10. Prima: Nessuna: proposta, non attuata | Nota di progetto in 15.2 | vedi 15.2 | richiesta dell'utente del 22/10 |

Anche [f5-4 §4.4](f5-4-pilota.md#44-domande-d1d8-con-quello-che-si-sa-ora-e-domande-nuove) annota che le operazioni accodate da WPF al
dispatcher usano la cultura del thread, non quella del flusso asincrono delle prove.

### 15.2 Proposta: impostazione dei separatori

Nota di progetto, da discutere. Non è attuata.

**Dove si applica oggi la cultura.**
- ANTHEA non imposta la cultura. `App.OnStartup` non la tocca e non c'è nessun `DefaultThreadCurrentCulture`: il thread
  dell'interfaccia e quelli di lavoro usano la cultura dell'utente di Windows.
- Seguono la cultura dell'utente:
  - i numeri dei campi senza fuoco e delle tabelle (`NumericDisplay.Format`, `EngineeringFormat.Number`);
  - alcune presentazioni, per esempio `PileGroupPresentation.Format` e `MaterialSheetPresentation.Number`;
  - pochi testi dei report, per esempio `ReportPileGroup`;
  - ogni `ToString("…")` o testo interpolato scritto senza cultura.
- Fissano `it-IT`, quindi la virgola: la maggior parte dei disegni WPF (21 file in `X.Desktop`) e molte presentazioni e generatori di
  report (25 file in `src`), per esempio `WallSheetPresentation.Number`, `BridgeDesignDrawing`, `EmbeddedWallReport`. I CSV esportati
  sono in parte `it-IT` e in parte invarianti.
- La lettura accetta i due separatori. `J.Number` e `ModuleSheetPreparation.NumericText` cambiano la virgola in punto e leggono con la
  cultura invariante. I documenti salvano il testo digitato (`FieldWriteRule.RawText`) o un numero JSON. Il separatore delle migliaia
  non è ammesso, come dicono le guide.
- Le prove impostano `it-IT` nel flusso asincrono (catture F3, tracce), alcune la cultura invariante. Le operazioni del dispatcher
  usano la cultura dell'utente.
- Effetto per l'utente: su una macchina con il punto i campi mostrano «0.5», mentre i disegni e gran parte dei report mostrano «0,5».

**Come funzionerebbe un'impostazione «Sistema / Virgola / Punto».**
- Una preferenza dell'utente in `%LOCALAPPDATA%\ANTHEA\`, come `appearance.json` dell'aspetto. Una voce con tre scelte nel menu
  «Aspetto» o in un menu «Impostazioni». Predefinita «Sistema», cioè il comportamento di oggi.
- All'avvio, prima della prima finestra, si costruisce la cultura:
  - con «Sistema», la cultura dell'utente;
  - con «Virgola» o «Punto», una copia della cultura dell'utente con `NumberDecimalSeparator` cambiato e il separatore delle migliaia
    opposto.

  La cultura diventa `CultureInfo.DefaultThreadCurrentCulture` e la cultura del thread dell'interfaccia. La regola (preferenza →
  formato dei numeri) sta senza WPF in Presentation; `X.Desktop` la applica in `App.OnStartup`.
- Un cambio a programma aperto vale al riavvio, con un avviso: i testi già scritti e i disegni non si aggiornano da soli. Ricostruire i
  fogli aperti è possibile, ma costa di più.
- La lettura non cambia: accetta già i due separatori.
- Per i punti che fissano `it-IT` ci sono due strade:
  - (a) restano come sono: l'impostazione governa solo ciò che oggi segue Windows (campi, tabelle, stati, dettagli), mentre report e
    disegni restano con la virgola;
  - (b) tutto passa a un formato unico dell'applicazione: tocca il codice dei report, congelato dall'8/10, e i disegni.

  Proposta: (a) adesso, (b) con la revisione dei report di fine refactoring.
- Un limite di WPF: `FrameworkElement.Language`, la lingua dei binding, porta solo il nome della cultura e non un separatore
  personalizzato. I binding con un `StringFormat` numerico, oggi quasi assenti, vanno formattati nel ViewModel o con `ConverterCulture`.

**Che cosa vuol dire per i riferimenti congelati e per i report.**
- Con «Sistema» non cambia nulla: catture, tracce e report restano quelli di oggi.
- Le prove dovrebbero fissare l'impostazione all'avvio dell'harness, per esempio «Virgola», sul thread e quindi anche nel dispatcher. Le
  catture non dipenderebbero più dalla macchina: è l'alternativa di V-a.
  - Si ricatturano una volta, in un commit motivato, i riferimenti che oggi contengono il punto: `disegni/elenco.txt` della sezione
    c.a. e le righe delle tracce scritte dal dispatcher (§12.4-1, §13.4-3, §14.4-1, e la cella annullata di F5.4).
  - Il riferimento F3 `expected` dovrebbe restare uguale, perché la cattura usa già `it-IT` nel flusso. Va verificato con il banco.
- Report:
  - con la strada (a) i generatori che fissano `it-IT` restano uguali in ogni impostazione;
  - i pochi testi che usano la cultura corrente seguirebbero l'impostazione; con «Virgola» sono uguali alle catture F3;
  - la strada (b) chiede la revisione dei report e una voce del registro.
- I documenti salvati non cambiano formato.

### 15.3 Suite UI e passaggi della Release

Fonti: [f5-suite-ui.md §5](f5-suite-ui.md#5-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) (S1–S5), [§6](f5-suite-ui.md#6-esiti)
(S6) e [f5-suite-passaggi.md §3](f5-suite-passaggi.md#3-domande-da-decidere-scelte-provvisorie-da-rivedere-in-f515) (P1–P3).

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| S1 (suite UI) | `VerifyPileOutcomes` sulla vista XAML: un ViewModel non accetta un risultato scritto da fuori | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Gli stessi risultati sintetici passano da `VerticalPilePresentation.Verification` con le stesse attese; in più la tabella della vista deve mostrare le righe di quella funzione | Un servizio di calcolo di prova che pubblica il risultato sintetico attraverso il ViewModel | nessuno sui risultati (solo prove) | f5-suite-ui §5 |
| S2 (suite UI) | Grigio del γsat automatico | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 4): A, confermata. La vista XAML usa `Token.Disabled` (P7) e la prova confronta con quel token | Non indicata | solo interfaccia | f5-suite-ui §5 |
| S3 (suite UI) | Domande dei fogli MVVM nelle suite | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Arrivano a `BenchSheetServices.Confirmations`: le suite rispondono «Sì» e controllano il testo | Saltarle, come le suite storiche (`confirm: false`) | nessuno sui risultati (solo prove) | f5-suite-ui §5 |
| S4 (suite UI) | Combinazioni di esposizioni nel controllo del calcestruzzo: la vista XAML ha solo l'esposizione del menu (M10) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Le stesse attese sulle funzioni che le due viste mostrano | Togliere i controlli, perché nessun utente arriva a quello stato | nessuno sui risultati (solo prove) | f5-suite-ui §5 |
| S5 (suite UI) | Vista nuova del calcestruzzo nelle suite dei materiali | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Un foglio aperto dalla fabbrica della finestra, nei due modi; cambia anche la suite con la Release | `RestoreState` per la storica e `Reload` del ViewModel per la XAML | nessuno sui risultati (solo prove) | f5-suite-ui §5 |
| S6 (suite UI) | L'antivirus chiude `ANTHEA.exe` mentre `DocumentFile.Write` rinomina i PNG | **Decisa 22/10** («1 a»): `DocumentFile.Write` copia anche le immagini, merge 78ccba8e | Far partire le suite con `dotnet` | nessuno sui risultati | f5-suite-ui §6; [ripresa](f5-ripresa.md), passo 3 |
| P1 | Scritture dirette dei dati nelle prove storiche del palo orizzontale | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Sulla vista XAML i dati passano dal foglio con `SheetViewModelBase.Reload` | Digitare gli strati nelle celle (dati non identici) | nessuno sui risultati (solo prove) | f5-suite-passaggi §3 |
| P2 | Colori dell'esito del palo orizzontale nelle prove | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Risultati sintetici da `HorizontalPilePresentation.Verification`; in più righe, testo e colore per tre risultati veri | Non indicata | nessuno sui risultati (solo prove) | f5-suite-passaggi §3 |
| P3 | Larghezza del selettore del metodo stratificato | **Decisa 21/10**: «tieni 120 px come le altre scelte» | Allargare la scelta nella vista (180 px) | solo interfaccia | f5-suite-passaggi §3; [decisioni](decisioni.md#21-ottobre-sera--selettore-del-metodo-del-palo-orizzontale-p3) |
| Passaggi | Quando si fanno i passaggi della Release dei moduli F5 e le loro prove | **Decisa 22/10**: «i test per il passaggio alla release li facciamo a fine refactoring» | — | — | [f5-10 §19](f5-10-sezione.md#19-s9-fatto-2210) |
| CA-P1 | Prove storiche della sezione c.a. sull'identità degli oggetti nativi in cache (`ConcreteSmokeChecks.VerifyWorkspace` e simili: domini, mesh, stati tensionali), che il ViewModel non espone | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. **Decisa provvisoriamente dal coordinatore (23/10)**: si riscrivono sugli effetti osservabili (verifiche accodate subito dopo la modifica, `Calculation.Pending`; risultato conservato o invalidato; valori delle verifiche non ricalcolate uguali; righe di stato «preparazione unica della sezione»), senza accessi di prova nel ViewModel | Accessi di prova in sola lettura nel ViewModel | nessuno sui risultati (solo prove) | [f5-10 §23](f5-10-sezione.md#23-passaggio-della-release-2310) |
| CA-P2 | Archiviazione della `ConcreteWorkspace` storica | **Chiusa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 1): superata dai fatti. Prima: **Decisa provvisoriamente dal coordinatore (23/10)**: nel commit del passaggio, dopo aver spostato sulla vista nuova ogni altro uso; `InputForm`, `SheetEditor` e `MainWindow` restano per la parte (g) | Non indicata | nessuno sui risultati | [f5-10 §23](f5-10-sezione.md#23-passaggio-della-release-2310) |
| CA-P3 | Prove storiche della sezione c.a. che controllavano codice esistente solo per le prove (D3): `ParsePaste`, `ApplyImport`, `ExportActionRows` (l'import Excel del prodotto passa dalla finestra «Sollecitazioni della sezione») | **Chiusa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 1): superata dai fatti. Prima: Restano con la vista storica in `supporto/SUPERATI/ui-storica/sezione-ca`; l'import del prodotto è provato da `ui/smoke-section-actions` e dalle tracce `azioni/*` | Rifare l'import di `ui/smoke` con la finestra (converte le azioni nel formato unificato e cambia il resto della prova) | nessuno sui risultati (solo prove) | [f5-10 §23](f5-10-sezione.md#23-passaggio-della-release-2310) |
| G1 (F5.13 g) | `SheetImplementation.Historic` senza viste storiche | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Resta come valore dei moduli non elencati, rifiutati con il testo del `SheetEditor` archiviato (`SheetEditorFactory.NoView`) | Togliere l'enumerazione e ridurre `ModuleImplementations` a un elenco di moduli | nessuno (solo codice) | [f5-13-shell §14.7](f5-13-shell.md#147-domande-nuove-e-scelte-provvisorie) |
| G2 (F5.13 g) | Voci della Chiara con chiave `MainWindow` (162 voci, 559 occorrenze, quasi tutte delle viste storiche già archiviate) | **Decisa dall'utente (24/10, 00:30): «Correggi nei token».** Fatta nella terza tappa (`f5-13-g3`): `ContrastAudit` sulla shell, corsa a 96 DPI, baseline da 699 a 629 occorrenze senza chiavi `MainWindow`; il difetto nuovo delle modalità scure (scelte degli strati del palo verticale) corretto nel tema, F5-62 | — | solo prove; F5-62 solo aspetto | [f5-13-shell §14.5](f5-13-shell.md#145-contrasto-d12) |
| G5 (F5.13 g) | `CheckBridgeOptions` vuole il fuoco della tastiera: la facciata delle suite attiva la finestra della shell fuori dallo schermo | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Come prima con `MainWindow` | Rinunciare al controllo del fuoco | solo prove | [f5-13-shell §14.7](f5-13-shell.md#147-domande-nuove-e-scelte-provvisorie) |
| G6 (F5.13 g) | A 1920 × 1080 la pagina del palo orizzontale scorre nella shell (F5-61) | **Decisa 24/10** (00:15): «accetto che la pagina del palo orizzontale scorra nella shell a Full HD; le due suite controllano l'area della scheda, come per SH40» | — | solo interfaccia | [f5-13-shell §14.7](f5-13-shell.md#147-domande-nuove-e-scelte-provvisorie) |
| G7 (F5.13 g3) | Le occorrenze della Chiara si contano per cattura: gli elementi fissi tornano in ogni pagina di scorrimento, e la scheda della shell ha pagine in più (80 occorrenze in 28 voci nuove) | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 2): A, confermata. Registrate come le stesse voci con un nome di vista nuovo (D12); nessun cambio dell'interfaccia | Contare un elemento fisso una volta per vista (strumento); rendere leggibile `Token.Muted` su `Token.Surface` in Chiara (contro D12) | solo prove | [f5-13-shell §14.7](f5-13-shell.md#147-domande-nuove-e-scelte-provvisorie) |
| G8 (F5.13 g3) | Pagine dei progetti fuori da `ui/check-contrast` (manca `prova-f1.4.programma` dal F5.3); la prova a parte trova in Chiara il numero di coerenza dell'albero (2,08:1) e gli avvisi del confronto (2,22:1) in `Token.Status.Warn`, colori storici | **Decisa dall'utente (24/10, 09:15): «tutte e due: archivio di prova versionato, così le pagine dei progetti entrano nella prova del contrasto, e un arancione più scuro e leggibile per il testo degli avvisi in Chiara».** Fatta nel ramo `refactoring/f5-g8`: testo dei due elementi in `Token.Warning.Foreground` (F5-63); archivio `tests/fixtures/f5/contrasto-progetti.programma`; baseline della Chiara da 629 a 645 occorrenze (2 voci nuove, della G7) | — | solo prove; F5-63 solo aspetto (colore del testo) | [f5-13-shell §14.7](f5-13-shell.md#147-domande-nuove-e-scelte-provvisorie) |
| G9 (F5.13 g3) | `FoundationWorkspace.UiChecks` appesa con «Operazione non completata» sullo schermo | **Corretta (5844a146, difetto della prova).** La prova creava `X.Desktop.App` per le risorse: WPF mette in coda l'avvio quando l'applicazione nasce e le attese della prova lo eseguivano; dal 23/10 `App.OnStartup` apre la shell sullo schermo e leggeva `<cartella> --ui` come documento («Access to the path … is denied»). Ora un'applicazione con le sole risorse di App.xaml (come `ModelViewer.UiChecks`), `ShutdownMode` esplicito. PASS 98 controlli | — | solo prove | [f5-13-shell §14.7](f5-13-shell.md#147-domande-nuove-e-scelte-provvisorie) |
| G10 (F5.13 g3) | `ui/check-sezione-view` FAIL dal 24/10: report breve diverso dal registrato | **Corretta (bd8807ca, difetto della prova).** La riga «ANTHEA · gg/mm/aaaa · esito» del report breve ha la data senza l'ora, che `NormalizeReportTimestamps` non sostituisce: le impronte registrate il 23/10 contengono quella data. Con 23/10 al posto di 24/10 il report breve di oggi ha esattamente l'impronta registrata. La prova riporta il giorno della corsa (± 1) al giorno della registrazione. Report e riferimento invariati | — | solo prove | [f5-13-shell §14.7](f5-13-shell.md#147-domande-nuove-e-scelte-provvisorie) |
| G11 (Rev59) | Il pulsante «Guida progetti» della barra dei progetti di `MainWindow` non c'è nella shell (F5-64, «da-decidere») | **Decisa 25/10, attuata 25/10 (passo d)** ([famiglie](f5-15-famiglie.md), famiglia 9): B: un «?» nella barra dei progetti della shell apre l'articolo della Wiki «Progetti e gestione del lavoro». Da attuare dopo il 28/10. Prima: **Provvisoria del coordinatore (24/10, utente assente)**: non si rimette; la guida pratica Rev59 rimanda all'articolo della Wiki «Progetti e gestione del lavoro» (pagina Wiki e ricerca; F1 e «?» aprono la Wiki dai fogli, dalla pagina Progetti non verificato) | Un pulsante o una voce di aiuto nella barra dei progetti della shell | solo interfaccia (aiuto) | [f5-13-shell §14.6](f5-13-shell.md#146-guida-pratica-e-wiki-testo-attuale-e-testo-proposto); registro F5-64 |
| W-idx (Rev59) | Il percorso delle figure entra nell'indice di ricerca della Wiki (`build-wiki-index.py` indicizza anche le righe `![…](percorso)`): con le figure in `src/ANTHEA.Desktop/Assets/Wiki` compare il termine «src» e la ricerca «anthea» trova anche gli articoli con figure. Difetto già presente con `X.Desktop` («x», «desktop», «assets») | **Decisa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 9): A ora; B (percorsi delle figure fuori dall'indice) con la revisione della Wiki. Prima: Nota aperta per la revisione della Wiki; l'indice non si tocca (coordinatore, 24/10). Le 30 ricerche F3 non cambiano | Escludere dall'indice i percorsi delle immagini (cambiano `search.json` e le ricerche che li trovano) | solo ricerca della Wiki | registro F5-REL-1140-01 |

### 15.4 Altre voci

| ID | Domanda | Opzione presa | Alternative | Impatto | Fonte |
| --- | --- | --- | --- | --- | --- |
| Q14 | Nomi a F5.14 | **Chiusa 25/10** ([famiglie](f5-15-famiglie.md), famiglia 1): superata dai fatti. Prima: Nessuna presa (F5.14 non avviata). Raccomandata la (a): cartella e progetto `ANTHEA.Desktop`, eseguibile `ANTHEA.exe`, namespace `Anthea.Desktop` per il codice nuovo | (b) rinomina completa anche dei namespace residui | nessuno sui risultati | [proposta §10](f5-proposta-architettura.md#10-domande-per-lutente-checkpoint-f50) |

### 15.5 Voci da decidere insieme

Non sono voci nuove: sono famiglie di voci dei gruppi sopra che pongono la stessa domanda.
- **Sola lettura che scrive**: T6 (V5, O3, FN3, FN4), §2-5 e famiglia, §10.4-1 «Esporta Excel», S7-d. MU3, MU4 e BD4 invece
  rifiutano già (registro F5-08, F5-09, F5-59).
- **Comportamenti storici conservati**: T7 (S6, O4, E4, E5), C5 (gruppo 10.2), SM5, BD10. Si conservano fino al passaggio, oppure si
  correggono con una nuova cattura.
- **Dimensione dei dialoghi**: §5.3-3 del palo verticale, S16, MU17, §6.4-5, SV-e, BD22. `WpfDialogService` adatta la finestra al
  contenuto.
- **Colori senza un token vicino**: M17, §5.3-1, MU16, §6.4-2, BD15.
- **Schede interne a −1 alla prima apertura**: S4-c, S7-b.
- **Pulsanti di navigazione spenti in sola lettura**: S6-a, S8-b.
- **Prove nello stadio `fast` o `ui`**: C7, SM6, BD11.
- **Calcoli fuori dal limitatore**: D-2, D-5.
- **Ricatture dei riferimenti congelati**: V-a, V-e, V-g, e §12.4-1, §13.4-3, §14.4-1. Conviene farle in un solo commit, se l'utente
  sceglie le alternative.

## Fuori da questo elenco

Voci ancora aperte che il piano non mette in F5.15. Sono elencate solo per completezza:
- **Bug da decidere** del 16/10 ([decisioni](decisioni.md#16-ottobre-notte--correzioni-dei-bug-decisioni-dellutente)): BUG-MUR-2b,
  BUG-PO-5b, BUG-FES-1b, BUG-FES-1c, BUG-SLU-2b, BUG-TS-2 e BUG-N7. Restano nella coda «decisioni sui bug ancora da decidere»
  ([ripresa](f5-ripresa.md), passo 8).
- **Scelte del coordinatore del 16/10 «da confermare»** ([decisioni](decisioni.md#16-ottobre-notte--rilascio-z-anthea-1100-e-avvio-di-f5)):
  - i documenti dei muri con ηp = 1 rifiutati da MUR-2;
  - la chiave `cls_teso_risposte` non scritta se l'utente non la cambia;
  - il report di progetto confrontato per posizione nel confronto UI F3, perché supera il limite della LCS.
- **Difetti di contrasto della sola Chiara** (719 in 28 gruppi), «da decidere con l'utente» ([piano](piano.md), seguiti del tema scuro).
- **Differenze delle combinazioni rispetto a Model**, «da decidere prima o dopo la migrazione» ([censimento](combinazioni-model-censimento.md)).
