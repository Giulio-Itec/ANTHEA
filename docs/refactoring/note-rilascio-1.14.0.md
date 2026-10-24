# ANTHEA 1.14.0 — note di rilascio

Note del 24 ottobre 2026. La 1.14.0 chiude la fase F5 del refactoring: l'utente ha deciso che la chiusura comprende il rilascio
(«Anche rilascio 1.14.0») e ha approvato la shell nuova («Shell approvata»). Il testo è scritto per chi usa ANTHEA ed è anche
l'annotazione del tag `v1.14.0`, sul commit d7e6b6a7 del setup. Il verbale tecnico del rilascio è in [installer-1.14.0.md](installer-1.14.0.md).

## Novità nell'uso

- **Finestra principale nuova.** ANTHEA si apre con una barra di navigazione sempre visibile (Apri, Salva, Salva con nome) e
  i menu File, Strumenti e Aspetto. I fogli si aprono in schede; un «•» segnala quelli non salvati e, nella Home,
  «Riprendi · nome» torna all'ultimo foglio. Dal menu della linguetta un foglio si sposta in una nuova finestra e poi si riaggancia; «Pagina intera»
  lo allarga a tutta la finestra (Esc per uscire). Alla chiusura ANTHEA chiede se salvare i fogli modificati.
- **Wiki in una scheda.** F1 e «?» aprono la Wiki in una scheda accanto ai fogli. «Applica nel modulo», con «Apri ·» e
  «Apri esempio ·», sostituisce «Prova in Anthea»; la Wiki del modulo sostituisce «Come funziona». Nella palificata
  orizzontale e nel calcestruzzo F1 su un campo apre la sezione della Wiki di quel campo.
- **Progetti.** L'albero sta in una colonna a sinistra. Il riepilogo propone le schede da aggiungere; un doppio clic o il
  trascinamento aggiunge un foglio, Invio o il pulsante del modulo lo apre e il nome si cambia sul posto. Il menu del nodo
  apre un foglio in una nuova finestra o a pagina intera. Il simbolo di coerenza apre il confronto tra fogli. Le procedure dei
  progetti sono nell'articolo della Wiki, che sostituisce «Guida progetti».
- **Revisioni.** La barra delle revisioni e «Mostra questa revisione» aprono le schede di una revisione in sola lettura,
  chiuse e riaperte nel loro ordine. Nei muri i «Valori di calcolo» si consultano senza «Applica e ricalcola» e la stabilità
  globale non prepara il profilo.
- **Report del ramo.** «Genera report» e il report Word della sezione controllano i fogli prima di scrivere e mostrano
  l'avanzamento con «Annulla».
- **Moduli.** Tutti i moduli usano le viste nuove, con gli stessi campi e gli stessi calcoli delle viste precedenti. Le
  piccole differenze di comportamento sono elencate nel registro delle differenze (F5-01…F5-14, F5-58, F5-60, F5-61). Per
  esempio, un foglio del palo verticale non resta «occupato» dopo una modifica fatta durante un calcolo. Lo scambio del
  terreno da un file singolo apre il nuovo file in una scheda, invece che in una nuova finestra.
- **Schermi piccoli.** Sotto 1120 × 680 la finestra scorre. Una pagina più alta della scheda scorre al suo interno: a
  1920 × 1080 succede alla pagina principale del palo orizzontale (F5-61).

## Aspetto

- **Avvisi leggibili nel tema Chiaro.** Il numero di coerenza dell'albero e gli avvisi del «Confronto tra fogli» hanno il
  testo in un arancione più scuro, leggibile (F5-63).
- **Temi scuri.** Nel palo e nel micropalo verticale, in Scuro e Molto scuro, le scelte degli strati seguono il tema (F5-62).

## Calcoli

Librerie GPC: release AG (Model 5.21, Concrete 0.20.0.1, Steel 2.84, Design 0.36, CompositeBridge 1.13, Geotechnics 0.19.0.1,
Masonry 0.6.0.1). Ogni differenza nei risultati di prova è registrata (voci AG-* del registro delle differenze) e approvata
dall'utente.

- **Calcestruzzo, parabola-rettangolo analitica.** La legge σ–ε del calcestruzzo è ora la parabola esatta, integrata in modo
  esatto sulla sezione, al posto della parabola per tratti. La differenza è piccola ma tocca tutti i moduli che verificano
  sezioni in c.a.:
  - sezione c.a.: resistenze dei domini da −0,21 % a +0,26 %, rapporti η da −0,24 % a +0,02 %, tensioni dell'armatura
    all'azione assegnata fino a −0,47 %;
  - curva momento–curvatura: curvature fino a −2,6 % ai carichi bassi, perché la parabola esatta è più rigida all'origine; la
    deformazione del calcestruzzo compresso cala fino al 4,4 % ai momenti bassi; con il tension stiffening a 0,3 MRd la
    deformazione dell'acciaio cala del 3,6 %;
  - palo e micropalo orizzontale: momento di plasticizzazione e carico limite al più +0,03 %;
  - muri di sostegno: resistenze N–M delle verifiche strutturali fino a +0,22 %;
  - sezione mista, storico non lineare: le grandezze che governano la verifica cambiano dello 0,1–1 %; tensioni e deformazioni
    piccole dell'armatura della soletta calano fino al 6,8 %.

  Queste differenze sono leggermente a sfavore di sicurezza, perché il calcolo è più preciso. Nei casi di prova nessun esito
  di verifica cambia, salvo la verifica SLE descritta sotto.
- **Sezione c.a., stress block.** Una sezione simmetrica dà ora lo stesso momento resistente nei due versi (in un caso di prova
  My− da −222,5 a −230,6 kNm, +3,6 %). Prima l'integrazione dello stress block poteva dare valori diversi.
- **Sezione c.a., SLE.** Una verifica SLE rara che prima restava da completare ora si calcola (nel caso di prova NON VERIFICATA,
  η 1,19).
- **Sezione c.a., punti non convergenti.** Se la libreria scarta un punto resistente perché il piano di deformazione trovato non
  rispetta i vincoli della ricerca, il foglio lo segnala con il motivo («punto resistente non convergente») e la verifica resta
  da completare. Prima compariva un errore.

## Guide

Guide pratica e teorica **Rev59**, in Word e PDF, installate con il programma. La guida pratica descrive la finestra nuova:
avvio e navigazione, schede, finestre staccate, pagina intera, Wiki in una scheda, progetti, revisioni e report del ramo. La
guida teorica cambia solo nell'intestazione.

## Lavori interni senza effetti visibili

- La finestra principale storica e l'editor dei fogli sono archiviati: la Release apre solo la shell nuova (F5.13).
- Il progetto del programma è in `src/ANTHEA.Desktop` e le prove WPF in `tests/ANTHEA.Desktop.UiTests` (F5.14). Il nome
  dell'eseguibile, `ANTHEA.exe`, e i file di lavoro non cambiano.
- La prova del contrasto copre anche le pagine dei progetti, su un archivio di prova versionato.

## Ambiente

- ANTHEA usa System.Text.Json 10.0.13, richiesta dalla libreria GPC.Design in .NET Standard 2.0, al posto della versione di
  .NET 8.

## Limiti noti

- **File salvati con la 1.14.0 e versioni precedenti.** Un file con una sezione in c.a. salvato con la 1.14.0 non si apre con
  la 1.13.0 o precedenti, che rifiutano la versione nuova del materiale calcestruzzo. I file delle versioni precedenti si aprono
  con la 1.14.0. Conservare una copia se un file deve tornare a una versione precedente.

- **Revisioni in sola lettura.** Nella sezione c.a. una revisione aperta in sola lettura può ancora segnare il foglio come
  modificato (F5-17). «Esporta Excel» lascia cambiare le azioni della revisione (F5-18), e «Calcola curva» resta
  disponibile (F5-19). In Bridge Design il prezzario è rifiutato in una revisione in sola lettura (F5-59). La regola unica
  per la sola lettura, decisa il 24/10, si attua dopo il 28/10.
- **Separatore decimale.** Nella sezione c.a. il separatore decimale di alcuni testi dipende dal percorso che li scrive (F5-34).
  Si attua l'impostazione «Sistema / Virgola / Punto» decisa il 24/10.
- **Paratie, filtrazione di uno scavo simmetrico.** Il bordo laterale del dominio è sempre a carico imposto. Se come
  estensione si inserisce la semilarghezza di uno scavo simmetrico, l'asse fa da dreno e il gradiente lungo la parete risulta
  sottostimato, a sfavore di sicurezza. Il campo del bordo a flusso nullo in ANTHEA non c'è ancora.
- **Paratie, coefficienti parziali.** Le storie marcate SLU non ricevono coefficienti parziali sulle azioni: vanno inserite già
  fattorizzate.
- **Scelte ancora da rivedere.** La revisione delle scelte di F5 (F5.15) continua come voce a sé: 207 voci aperte, nessuna
  blocca l'uso.
