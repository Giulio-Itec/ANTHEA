# ANTHEA 1.19.0 — note di rilascio

Bozza dell'8 novembre 2026 (corsia F7d, aggiornata da F7e con le decisioni dell'utente della mattina), dal testo preparato da
ChatGPT il 6–7/11 (`chatgpt-compiti/ANTHEA-1.19.0-note-bozza.md`).
**Data di rilascio: da completare in F8.** **Librerie: set candidato F7d, solo prove; F8 le sostituisce con il set ufficiale da
origin/master (versioni, commit e manifest da riscrivere).**

La 1.19.0 migliora il salvataggio dei progetti e dei risultati dei modelli, introduce le sezioni c.a. in formato compatto, rende espliciti
i problemi dell'archivio con **Salvataggio con avvisi** e permette di scegliere un **template di stili per i report**. Con le librerie
nuove cambiano alcuni risultati delle verifiche: sono elencati in [Cambi di comportamento delle librerie](#cambi-di-comportamento-delle-librerie).
È una versione con funzioni nuove, quindi aumenta il secondo componente del numero.

Il testo è scritto per chi usa ANTHEA; in F8 diventa anche l'annotazione del tag `v1.19.0`. Il verbale tecnico del rilascio sarà in
`installer-1.19.0.md`.

## Salvataggio e compatibilità dei file

- **Risultati dei modelli con GPC.Archive.** Il file dei risultati `.anthea-risultati` conserva il formato 1 della 1.18.0: gli archivi
  validi si leggono in entrambe le direzioni e si riscrivono uguali byte per byte. La compatibilità riguarda l'archivio dei risultati, non
  tutti i documenti di progetto.
- **Sezioni c.a. compatte.** Al primo salvataggio le sezioni valide nel formato XML della 1.18 passano al formato compatto di GPC, con la
  stessa identità, gli stessi materiali, armature e dati. I dati incompleti restano una bozza con la sua spiegazione; le revisioni storiche
  tengono la propria sezione.
- **Documenti precedenti.** La 1.19.0 legge i documenti delle versioni precedenti. Dopo il salvataggio con sezioni c.a. compatte il file non
  si apre più con la 1.18.0 o con versioni anteriori, che segnalano «Formato o versione del file non supportati.». Per continuare a usare
  una versione precedente conservare una copia del file originale.
- **Salvataggio con avvisi.** Se il progetto si salva ma i risultati hanno problemi, il messaggio elenca l'archivio danneggiato messo da
  parte con il suffisso `.danneggiato-<data>`, i blocchi danneggiati non ricopiati e quelli citati dal progetto ma mancanti. Il progetto
  risulta salvato; i risultati non disponibili vanno ricalcolati. Un riferimento a un risultato danneggiato non impedisce da solo di aprire
  il progetto.
- **Errori che impediscono di salvare.** Un archivio in uso da un altro programma, o un errore di lettura durante il controllo dei blocchi,
  ferma il salvataggio. L'avviso dopo un salvataggio riuscito è distinto da «Salvataggio non riuscito».
- **Documenti aperti altrove.** Aprendo un progetto già aperto da un'altra istanza si sceglie fra una copia, l'apertura comunque o
  l'annullamento. Prima di sovrascrivere un file cambiato sul disco viene chiesta conferma; «Salva con nome» rifiuta una destinazione aperta
  altrove.
- **Recupero del lavoro non salvato.** Copie periodiche dei documenti modificati e dei risultati nuovi; dopo un'interruzione, all'avvio si
  può riprendere il lavoro, scartarlo o rimandare la scelta.
- **Fogli non leggibili.** Quando il problema riguarda un solo foglio, il progetto si apre indicando il percorso e il motivo. Il foglio resta
  in sola lettura con i dati originali; una sezione danneggiata non viene ricostruita.

## Template dei report

**Aspetto › Template dei report › Scegli template Word…** accetta un `.docx` o un `.dotx` e ne prende **solo gli stili** (Normale, Titolo,
Sottotitolo, Titolo 1–9, Didascalia, paragrafi delle tabelle) e il tema dei caratteri. Testo, cartiglio, sommario, campi, intestazioni, piè
di pagina, numerazioni, immagini e geometria del documento scelto non entrano nei report; tabelle, figure, valori, unità e impaginazione
tecnica restano quelli di ANTHEA. La scelta vale per l'utente Windows, si applica subito ai report Word/PDF dei fogli, ai report short, ai
progetti e alle revisioni, alla stabilità globale del muro e ai PDF/HTML del modulo Modelli, e si conserva anche se il file originale viene
spostato. **Ripristina stile ANTHEA** torna allo stile di sempre; una scelta non valida lascia quella precedente. Senza template i report
sono quelli della 1.18.

Contenuti dei report allineati ai risultati correnti:
- **Palo e micropalo verticali**: gli avvisi del risultato (ipotesi, copertura, coefficienti modificati, limiti) entrano sempre nella
  relazione, anche senza capitoli facoltativi.
- **Sezione in c.a.**: la relazione completa riporta la duttilità in curvatura calcolata dalla libreria (primo snervamento, bilineare,
  momento resistente e massimo, curvatura ultima e μφ) o il motivo per cui non è disponibile; il progetto della sezione chiede la stessa
  analisi della scheda. JSON dei risultati, CSV dei punti e archivi non cambiano.
- **Sezione composta da ponte**: nella sezione efficace di classe 4 della relazione le lunghezze in mm (larghezze e spessori dei
  pannelli, bc, b1 eff, b2 eff, anima inefficace e sue quote) non si scrivono più in notazione esponenziale: un residuo di
  arrotondamento come «Anima inefficace: 4,55E-13 mm» si legge «0,00 mm». Le altre grandezze e i valori da 0,01 mm in su non cambiano.

Nessun calcolo nuovo: cambiano la forma e i capitoli dei report.

## Copertina dei progetti

Un foglio **Materiali senza dati** non interrompe più l'apertura e la validazione della copertina: il controllo del copriferro segnala che
la verifica non è eseguibile e continua con gli altri materiali, anche ereditati dai contenitori superiori. Il foglio incompleto non viene
riempito né considerato verificato.

## Modelli

Il salvataggio di un progetto con molti risultati nuovi può avvenire in background: le modifiche fatte durante la scrittura restano da
salvare e la chiusura attende il salvataggio in corso. L'avanzamento dell'archivio non si aggiorna a ogni blocco scritto.

### Piastre: tolleranza di planarità

Le piastre con piccoli scostamenti dovuti all'arrotondamento delle coordinate non vengono più rifiutate per una tolleranza assoluta troppo
stretta: il controllo usa il maggiore fra 0,0001 mm e 0,00001 volte la massima distanza fra i vertici, misurando lo scostamento dal piano
medio. La stessa regola vale per le aree di pressione non uniforme. I vertici non si spostano; le piastre davvero fuori piano restano
segnalate.

### Esportazione delle pressioni non uniformi in Straus7

Le pressioni variabili sulle piastre a tre o quattro nodi si esportano come **forze nodali equivalenti** (stessa risultante e stesso
momento), invece di fermare l'esportazione con «UnsupportedStrausPlateLoad»; si sommano ai carichi concentrati già presenti sullo stesso
nodo e caso. I vertici dell'area caricata devono coincidere con gli angoli della piastra. Rileggendo il `.st7` questi carichi compaiono
come forze nodali.

### Classe del calcestruzzo dalla resistenza dichiarata

In importazione una denominazione come «Concrete: Compressive Strength fc = 32 MPa» dà **C32/40** («fc = 20 MPa» dà C20/25), con l'avviso
**MaterialGradeFromStrength**, che dichiara la lettura del valore come `fck` cilindrica. Una classe esplicita ha la precedenza; resistenze
senza classe corrispondente, `fcm`, unità diverse o materiali in acciaio non si convertono.

La guida Rev70 descrive **Calcola tutte**, **Calcola selezionate**, il pannello **Spazio delle combinazioni** e lo spessore delle piastre in
**Sezioni del modello**, funzioni già presenti prima della 1.19.0.

## Pali

Il grafico della capacità del palo e del micropalo verticale mostra il nome del modulo fin dall'apertura e non invita più a premere
«Calcola» mentre il foglio calcola da solo. Corretto anche l'ordine di pubblicazione dei calcoli in background: un risultato già pronto
non compare prima degli aggiornamenti dell'interfaccia che lo precedono. Curve, azioni e risultati non cambiano.

## Prestazioni

Misure dello sviluppo, non garanzie su ogni progetto:
- **Sezioni compatte**: file da 5 a 27 volte più piccoli (esempi da 70–427 kB a 9–23 kB, un progetto da 531 a 65 kB); apertura degli
  esempi da 4–133 ms a 1,5–16 ms.
- **Annulla con modelli nel progetto** (banco 500 × 100): memoria dello storico di 30 passi da 1,1 GB a 16 MB, Annulla da 149 a 45 ms.
- **Progetti con molti fogli** (576 fogli): rinomina da 752 a 74 ms, riepilogo della sezione da 795 a 15 ms.

## Cambi di comportamento delle librerie

Con Checker `codex/steel-consolidation-20261105` (`3b40e2a0`), ammesso nel rilascio dall'utente l'8/11, cambiano i risultati seguenti.
Ognuno è spiegato dal commit indicato; nessun altro risultato di ANTHEA cambia (full della 1.19.0 sul set candidato F7d confrontata con
quella della prova F7c, 8/11).

### Fondazioni superficiali: Brinch Hansen con carico inclinato e H_L = 0 (Geotechnics 0.28.0.0, `f424383b`)

Con la forza orizzontale solo lungo B′ (H_L = 0, sempre così per una fondazione circolare) la rottura si calcola solo nella direzione di
B′; prima si calcolava anche quella di L′ (senza inclinazione), che poteva governare e allontanava la capacità dal valore a carico normale
quando H tende a zero. Decisione dell'utente del 5/11 (punto Q1 delle domande del 29/10), confermata nella 1.19.0 l'8/11 («ok»). Con
H_L > 0 non cambia nulla: si calcolano le due direzioni e si adotta la minore.

| Caso (B = 2 m, L = 3 m, D = 1 m, N = 600 kN, Hx = 60 kN) | Grandezza | 1.18 / F7c | 1.19.0 |
|---|---|---|---|
| drenato, φ′ = 30° (manuale, caso 426) | qlim | 522,63 kPa | **522,63 kPa** (invariato: governava già B′) |
| | relazione | «direzioni di B′ e di L′ … si adotta la minore (B′)» | «direzione di B′ (HL = 0)», con l'avviso «HL = 0, rottura nella sola direzione di B′. La direzione di L′ si calcola solo in presenza di HL (§3.4.2).» |
| non drenato, cu = 75 kPa (manuale, caso 459) | qlim | 455,04 kPa | **469,20 kPa** (+3,1 %) |
| | resistenza di progetto (qlim · A′ / γR, γR = 2,3) / indice N/R | 1187,0 kN / 0,505 | **1224,0 kN / 0,490** |
| | correzioni additive della coesione (forma, profondità, inclinazione, fattore) | 0 · 0,133 · 0 · 1,133 (direzione di L′) | **0,005 · 0,2 · 0,035 · 1,170** (direzione di B′) |

Nel manuale di riferimento cambiano anche i casi 456 (drenato, D = 0, Hx = 6 kN: 162,75 → 194,47 kPa) e 462 (non drenato, Hx = 6 kN:
455,04 → 479,63 kPa). I progetti con H solo lungo B′ e la direzione di L′ prima governante danno quindi una capacità più alta.

### Sezione composta da ponte: arrotondamenti del solutore (CompositeBridge 1.31.0.1, `1092c29f`, `7b726fc8`, `87985942`)

Il solutore elastico della sezione composta risolve ora i campi direttamente e riusa momenti e fibre preparate. Tensioni, larghezze
efficaci, inerzie e verifiche cambiano solo nelle ultime cifre (scarto relativo ≤ 1·10⁻¹³ sulle grandezze del calcolo, 3·10⁻⁹ sul residuo
dell'iterazione di classe 4, 4,454630545·10⁻⁸ → 4,454630531·10⁻⁸): nessun valore mostrato con le cifre normali cambia. Si vedono soltanto
i numeri che sono già arrotondamenti:
- colonna **Residuo equilibrio** e testo «Residuo massimo dell'equilibrio N–Mx» della scheda e della relazione: per esempio 3,579E-15 →
  0, 5,587E-15 → 1,589E-16;
- la lunghezza dell'anima inefficace di una sezione con l'anima tutta efficace è un residuo di arrotondamento (2,27·10⁻¹³ mm nella
  relazione di un progetto di prova): con il formato nuovo delle lunghezze (sopra, «Template dei report») la relazione scrive sempre
  «Anima inefficace: 0,00 mm» (Aeff/Alorda e Ieff/Ilorda 1,00), come prima.

### Nessun cambio

Acciaio (Steel 2.134, ModelChecker 2.43: classe 4, anime RHS, tagli concorrenti, ingobbamento) e calcestruzzo (Concrete 0.37, fasi senza
memoria) non cambiano alcun risultato di ANTHEA: le verifiche dei puntoni e dei tiranti delle paratie usano metodi dell'acciaio non toccati
e il contratto K0 del calcestruzzo è invariato.

## Librerie

**Set candidato F7d, solo prove** (`gpc-candidate-20261108-f7d`, manifest `05005D29…86F5`), da sostituire in F8 con il set ufficiale da
origin/master: GPCModel 5.50.0.2, GPCModelData 0.3.0.30, GPC.Model.Persistence 3.15.0.2, GPC.Archive 1.2.0.3, GPC.Converter 2.20.0.2
(Model `d28ce8c2` + versioni `ff318f87`); Concrete 0.37.0.1, Steel 2.134.0.0, CompositeBridge 1.31.0.1, Geotechnics 0.28.0.0, Masonry
0.10.0.35, GPC.Design 0.45.0.9 (Checker `3b40e2a0` + versioni `9e513417`); GPCUtilities 3.0.0.0, GPCGeometry 2.3.0.0, DelaunayMesh 2.0.0.14
invariati.

## Guide

Guide pratica e teorica **Rev71** (template dei report), con la Rev70 archiviata in `supporto/SUPERATI/report-template-rev71-20261106`.
La Rev70 aveva aggiunto sezioni compatte, compatibilità dei file, «Salvataggio con avvisi» e le funzioni di Modelli elencate sopra.

## Limiti noti

- Il salvataggio in background può far attendere l'apertura di un modello o la preparazione della sua copia fino alla fine della scrittura
  dell'archivio.
- Restano quelli della 1.18.0.

## Riferimenti delle prove rinnovati

Decisione dell'utente dell'8/11 sui riferimenti congelati: «aggiorna» (byte rinnovati, nessuna tolleranza nuova). Solo quelli dei cambi
elencati sopra, ognuno con la sua voce nel registro delle differenze:
- **Fondazioni superficiali** (`FON-HANSEN-02`): attesi di Brinch Hansen inclinato con H_L = 0 in `FoundationWorkspace.Checks` e
  `UiChecks`, dal riferimento indipendente rigenerato in Checker (caso 459 469,20 kPa).
- **Sezione composta da ponte** (`SM-CB131-ARROTONDAMENTI`, `REPORT-LUNGHEZZE-MM-20261108`): 12 attesi F3 (risultati e relazioni di 6
  fixture), 3 transizioni del report di progetto, 15 tracce della vista, 14 impronte dei disegni e i valori registrati di 6 fixture: solo
  arrotondamenti e la riga «Anima inefficace» a 0,00 mm.
- **Sezione in c.a.** (`REPORT-TEMPLATE-CURVA-20261108`): il report della curva M–χ registrato con la «Duttilità in curvatura» (11 fixture).

## Fonti

- Bozza di ChatGPT con le fonti S0–S18 e R1–R12 (commit ANTHEA e Model di ogni novità):
  `Repository/chatgpt-compiti/ANTHEA-1.19.0-note-bozza.md`; resoconto d'integrazione
  `Repository/chatgpt-compiti/anthea-1190-integrazione-resoconto.md`.
- Ramo di rilascio `claude/anthea-1190-rilascio`: `codex/anthea-1190-integrazione` `7bfb3a95` + merge di `refactoring/report-template`
  `dc1b8d24` (`95242989`); template dei report: `docs/refactoring/report-template-20261106.md`, voce `REPORT-TEMPLATE-20261106`.
- Registro: `docs/refactoring/registro-differenze.json`, 433 voci (416 al tag 1.18.0).
- Corsie F7d ed F7e: `gpc-unification-20261102/revisione-claude-20261103/correzioni-F7d.md` (F7e nella sua sezione).
