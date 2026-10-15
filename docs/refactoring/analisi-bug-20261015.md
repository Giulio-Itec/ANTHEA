# Analisi dei bug del codice di calcolo — 15/10/2026

Passo della coda dopo F4 deciso dall'utente il 15/10: «residui minori / F2-7 / analisi dei bug del codice / rilettura
delle pagine, poi F5», con l'indicazione «usa workflow leggero».

## Come è stata fatta

- **Codice esaminato**: librerie GPC sul set candidato di oggi (Checker `anthea-f2-residui` 6f15f185, con R1, R2 e
  F2-25; Model, Geometry e Utilities come in lib/Checker) e adattatori ANTHEA sul ramo `refactoring/f2-residui`
  (9e47e630).
- **Workflow leggero** (wf_42fd8504-6ec, 32 agenti): 10 aree, un cercatore per area con una lente unica (correttezza
  normativa, raggiungibilità, coerenza fra input e calcolo); poi un verificatore indipendente per ciascuna segnalazione
  di gravità alta o media, al massimo 3 per area. Ogni verificatore ha scritto un proprio harness sulle DLL compilate e
  un calcolo Python indipendente dal codice, e ha cercato di smentire la segnalazione (registro, decisioni, pagine dei
  metodi, guida teorica, norma).
- Nessun repository è stato modificato. Harness e script sono nello scratchpad della sessione (`bug\<area>…`).
- **Fuori dall'analisi**: i difetti già registrati (F2-15, F2-17, R4, R7, R12, R14, R18, R21, R22, S4-1, F4-xx); UI WPF
  e report oltre i punti d'uso, perché la revisione è già in coda.

## Esito in sintesi

| | Numero |
|---|---|
| Segnalazioni | 40 |
| Verificate | 22, tutte **confermate** (4 con gravità abbassata da media a bassa) |
| Smentite | 0 |
| Non verificate (limite di 3 per area) | 18: 2 medie, 16 basse |

Per gravità, dopo la verifica:

- **alta**: 3, tutte non cautelative;
- **media**: 17, di cui 15 verificate;
- **bassa**: 20.

Di 40, 17 sono a sfavore di sicurezza o danno un esito favorevole che non spetta.

Quasi tutti i difetti sono **ereditati** dal legacy o dal CheckerUI, e non sono regressioni del refactoring.
Le eccezioni sono queste:

- **regressioni**:
  - SLU-2, di R2/F2-17 per M = 0;
  - TAG-3, testi inglesi, di F4.13b;
- **residui di F2-15**, corretto solo in SectionMomentResistance: SLU-3 e PO-3.

Codici: SLU sezione allo SLU · FES fessurazione · TAG taglio e torsione · DET dettagli e durabilità · ARM progetto delle
armature · PV pali verticali · PO pali orizzontali · MUR muri · GEO geotecnica · PON ponte.
Ambito: L = libreria GPC, A = adattatori ANTHEA, L+A = entrambi.

## A. Gravità alta — non cautelativi, confermati

### SLU-1 · Dominio 2D N–M con θ ≠ 0°: punto resistente sbagliato, η fino a 200 volte più basso (L+A)

**Dove.**
- Checker `GPCChecker.Concrete/Results/FailureDomain2d.cs:138-142, 196-213, 217-231` e `FailureDomain.cs:775-778`;
- ANTHEA `X.Calculations/CheckerSection.cs:165-172, 405-411`.

**Il difetto.** `GetDomainPoint` trova bene l'intersezione nel piano (N, M), ma ricostruisce il punto 3D con
`ToGlobal`, ruotato attorno a Z. Il risultato è N = N·cosα − M·sinα, Mx = N·sinα + M·cosα e My = 0. ANTHEA calcola η
e Rd proprio su quel punto. Il calcolo è giusto solo a θ = 0°. A 180° η è giusto ma Rd ha il segno opposto (trazione
al posto della compressione).

**Esempio.** 300×500, C25/30, 2+2Ø16, θ = 90°, N = −1000 kN, My = 200 kNm:

| Calcolo | η | Esito | Rd |
|---|---|---|---|
| ANTHEA, dominio 2D | 0,0127 | «Entro il dominio» | N = 80 604 kN |
| Verifica 3D | 2,476 | — | (−404 kN; 80,7 kNm) |
| Python indipendente | 2,478 | — | — |

Altri casi: a 45° il 2D dà 0,0135 contro 1,580 del 3D.

**Raggiungibile.** Sì:
- dalla scheda Dominio 2D, anche con il pulsante «Sezione sull'azione selezionata»;
- nel calcolo headless (2D:SLU e 2D:SLV sempre calcolati);
- nei riepiloghi «2D · SLU/SLV» e nel report «dominio2d».

**Difetti collaterali.**
- `FailureDomainForce2d.CalculateWorkingRatio(ConstantMxMy)` ha le scale scambiate. È API pubblica, non usata da
  ANTHEA.
- A θ multiplo di 90° si usa il meridiano dell'asse neutro fisso. Per sezioni non simmetriche questo sovrastima Rd di
  circa il 5%.

**Proposta.**
- Libreria: ricostruire il punto per interpolazione fra i vertici del meridiano (NRd, MxRd, MyRd), con il segno dalla
  proiezione sulla direzione del meridiano; correggere le scale.
- ANTHEA: per N–M usare sempre il meridiano della direzione del momento.
- Prove: θ = 0/30/45/90/180/270 contro il 3D, entro l'1%.

### MUR-1 · Sovraccarico variabile sulla mensola di monte contato come stabilizzante con γQ = 1,5 (L)

**Dove.** Checker `GPCChecker.Geotechnics/Walls/RetainingWallAnalysis.cs:93, 142-147` e `WallCombinations.cs:57-61`.

**Il difetto.** Lo stesso fattore moltiplica q nella spinta, dove è sfavorevole, e nel carico heel·q sulla mensola,
dove è favorevole a scorrimento e ribaltamento. Manca la disposizione con q solo oltre il tallone, cioè con γQ = 0
sulla parte favorevole (NTC 2018 Tab. 2.6.I, EN 1997-1 2.4.2). Nemmeno una matrice personalizzata la può esprimere.

**Esempio.** Muro di default con H = 4, b = 2 e q = 20 kPa:

| Verifica | Ora | Atteso |
|---|---|---|
| Scorrimento | 0,87 «soddisfatta» | 1,10, non soddisfatta |
| Ribaltamento | 0,40 | 0,53 |

Sul muro di default basta portare q da 10 a 20 kPa: lo scorrimento risulta 0,765 «soddisfatta» invece di 1,019.

**Proposta.** In `WallCombination` un fattore distinto per il carico sulla mensola, e per ogni riga SLU con
sovraccarico la gemella con quel carico a 0. La gemella entra nelle verifiche GEO e nel progetto della mensola; la
portanza resta coperta dalle due righe.

### PON-1 · Tensioni SLE delle barre della soletta fessurata senza tension stiffening (L)

**Dove.** Checker `GPCChecker.CompositeBridge/HSections/HBridgeSection.Analysis.cs:202-219`, `Models/BridgeResults.cs:109-110`,
`History/HBridgeHistoryResults.cs:108-122`.

**Il difetto.** Nelle fasi «Soletta esclusa» si usa σs = σa·Es/Ea. EN 1994-2 §7.2.1 chiede di includere il tension
stiffening, cioè EN 1994-1-1 §7.4.3(3): σs = σs,0 + 0,4 fctm/(αst ρs). Manca sia nel metodo cumulativo sia negli
storici, e non è dichiarato come semplificazione.

**Esempio.** Dati di default, SLE rara, momento negativo:

| | σs | η | Esito |
|---|---|---|---|
| Ora | 297,1 MPa | 0,825 | «Entro limite» |
| Atteso (297,1 + 82,6) | 379,6 MPa | 1,055 | non soddisfatta (limite 0,8 fyk = 360 MPa) |

**Proposta.** Sommare Δσs alle barre tese negli stati SLE, quando la situazione contiene fasi «Soletta esclusa», sia
nel cumulativo sia negli storici. Riportare Δσs nei dettagli.

## B. Gravità media — non cautelativi, confermati

### FES-1 · wk = 0 «asse neutro nel copriferro» con la zona tesa profonda fino a h/2 (L+A)

**Il difetto.** `SectionCrackCheck.cs:379-396`: se nessuna barra è tesa, dà wk = 0 ogni volta che la barra più vicina
al lembo teso sta a meno di h/2. Non controlla che l'asse neutro sia davvero nel copriferro.

Con barre di parete o con una rete in mezzeria, la zona tesa senza armatura arriva a 0,35–0,45 h e il programma dice
«soddisfatta». Spostando le barre di 2 mm oltre h/2 il verdetto si ribalta in «non soddisfatta» (F2-25).

**Legame con la decisione del 15/10.** È la regola appena data dall'utente («asse neutro nel copriferro … la sezione
non fessura»): oggi il codice la applica con una soglia molto più larga.

**Proposta.** wk = 0 solo se la barra più vicina sta entro c + Ø/2 dal lembo, misurato lungo il gradiente (c dal
copriferro della formula); altrimenti esito NoTensileReinforcement, che con l'opzione F2-25 risulta non soddisfatta.
Va nella stessa opzione di oggi, `UnreinforcedTensileZoneFails`: K0 non cambia.

### FES-2 · Copriferro c della formula dal valore nominale anche con le armature libere (A)

**Dove.** `ConcreteServiceabilityAdapter.cs:160-163` passa sempre cover_mm + Ø staffa.

**Il difetto.** La sezione interamente tesa usa invece il copriferro geometrico (`SectionCrackCheck.cs:576`).
L'interfaccia mostra il campo vuoto come «auto».

**Esempio.** 300×500 con 3Ø20 libere a 70 mm dal lembo (copriferro reale 60 mm):

| | wk | Esito |
|---|---|---|
| Ora | 0,283 mm | soddisfatta |
| Atteso | 0,361 mm | non soddisfatta |

Con le disposizioni automatiche non cambia nulla.

**Proposta.** Senza CoverOverride, usare c = min(BarCover) sulle barre efficaci, nella libreria o con l'override
calcolato in ANTHEA.

### TAG-1 · MC2010, εx: verso di Δe invertito rispetto all'aiuto di ANTHEA, valori negativi rifiutati (L+A)

**Il difetto.** La libreria calcola N(½ + Δe/z), che è corretto se Δe è positiva verso la corda tesa. L'aiuto di
ANTHEA la dichiara positiva verso il lembo compresso, e il campo accetta solo valori ≥ 0.

**Esempio.** N = −500 kN, Δe = 50 mm come dice l'aiuto: VRd,c = 157,7 kN invece di 118,6 kN, cioè +33% a sfavore.

**Proposta.** Correggere l'aiuto e il report in «positiva verso il lembo teso» e accettare anche i valori negativi.
Dichiarare il verso nella libreria e in ca.taglio (T.6). Aggiungere una prova con N < 0 e Δe = ±50.

### DET-2 · Ancoraggi: le barre lisce verificate con l'aderenza delle nervate (A)

**Il difetto.** Il dato «Barre: Liscia» (sle_comuni.aderenza) arriva alla fessurazione ma non all'ancoraggio. ANTHEA
non passa ribbedBars, che vale true per default, e aggira così il rifiuto della libreria.

**Esempio.** fbd = 2,69 MPa come per le nervate; 600 mm risultano soddisfatti. Il caso tipico sono gli edifici
esistenti con FeB22k o FeB32k.

**Proposta.** Passare ribbedBars dal dato della sezione; tradurre la NotSupportedException in «Ancoraggi: regole
implementate per barre ad aderenza migliorata».

### ARM-1 · Ricerca armature: parametri del taglio «Manuali» scartati senza avviso (A)

**Il difetto.** `ConcreteReinforcementDesign.Library.Search.cs:30` forza «Automatici da sezione» per le forme non
circolari. «Applica» poi cancella i valori manuali nel foglio. Per il cerchio succede il contrario: d resta quello
della disposizione originale.

**Esempio.** bw = 250 manuale: la ricerca calcola con bw = 400 (+35% di VRd) e accetta configurazioni che il foglio
non verifica.

**Proposta.** Con «Manuali» usare bw, d e Asl pari a min(manuale; candidato), con una nota; oppure rifiutare la
ricerca con un messaggio.

### PV-1 · Pali: una sola opzione di sottospinta per tutte le verifiche (L+A)

**Due effetti, opposti.**
- In trazione, con il flag spento (predefinito), il peso del palo non è ridotto della sottospinta. Esempio: Ed,t =
  807 kN invece di 946 kN (−15%), soddisfatta invece di non soddisfatta.
- In compressione non drenata lorda, con il flag acceso, u alla punta è tolta due volte: −14% sull'azione.

**Proposta.** Azioni per condizione e per verso:
- trazione: sempre W′ con la falda;
- compressione non drenata lorda: W pieno;
- compressione drenata: resta il flag.

### PO-1 · Broms con testa impedita ignora il tratto libero (A)

**Il difetto.** `PaloOrizzontale.cs:210-235` non legge generali.tratto_libero, quindi il vincolo resta al piano
campagna.

**Esempio.** D = 1, L = 15, tratto libero di 3 m: Hu = 900 kN come con tratto libero nullo, contro circa 439 kN.
Non cautelativo di un fattore 2.

**Proposta.** Rifiutare Broms con testa impedita e tratto libero > 0, con un messaggio esplicito; oppure estendere i
rami con il braccio L0.

### PO-2 · Efficienza Reese–Van Impe del foglio: fattori leading/trailing scambiati (L)

**Il difetto.** `LateralPileModels.cs:175-182` scambia i due fattori. È diverso da LateralPileGroup della stessa
libreria e dalla guida teorica.

**Esempio.** Palo arretrato con interasse di 3 m: η = 0,931 invece di 0,729, cioè Rd +28%.

**Proposta.** Scambiare i fattori; la diagonale si corregge da sola. Allineare la guida, riga 291.

### MUR-2 · Passiva a valle senza γR e mobilitabile al 100%; con Pp ≥ spinta lo scorrimento ha domanda nulla (L)

**Il difetto.** Scorrimento come H − η·Pp ≤ N·tanδ/1,1, con η fino a 1 e senza γR = 1,4 (NTC 2018 §6.5.3.1.1, Tab.
6.5.I: aliquota ≤ 50%). Si attiva solo con la casella «Considera la resistenza passiva».

**Proposta.** Trattare la passiva come resistenza: domanda = spinta motrice; resistenza = N·tanδ/γR + η·Pp/1,4.
Rifiutare η > 0,5, o almeno dare un avviso.

### MUR-3 · Cuneo di terreno a valle sul paramento inclinato contato da x = a per i tagli del fusto (L)

**Il difetto.** `RetainingWallAnalysis.cs:273`: la striscia sotto il taglio viene sommata al corpo libero del fusto.

**Esempio.** Muro a gravità, taglio a z = 2,25 m: N = 74,9 invece di 68,9 kN/m, e M = +6,07 invece di −0,46 kNm/m
(segno invertito).

**Proposta.** Con wedgeOnly usare left = a + pendenza·(max(t, yc) − t).

## C. Esito mancante, bloccato o sbagliato a favore di sicurezza (gravità media; SLU-3 e TAG-2 abbassate a bassa dalla verifica)

| Codice | Difetto | Effetto | Proposta |
|---|---|---|---|
| SLU-2 (L+A) | Criterio «N costante», il predefinito: nessun punto con M = 0 (direzione nulla) o con N fuori da [NRd,c; NRd,t] | Una sezione schiacciata (η reale 1,24) risulta «DA COMPLETARE» con il testo «punto non convergente»; una combinazione solo assiale interna resta senza esito. Regressione di R2 per M = 0. L'opzione B di F2-17 dichiarava che con N costante il punto si trova sempre: è falso. | Libreria: esiti tipizzati «N fuori campo» (η > 1) e «momento nullo» (η = 0). ANTHEA: «Fuori dominio (\|NEd\| > \|NRd,c\|)». Correggere F2-17 ed estendere la prova 12c. |
| PO-3 (L) | `PileReinforcement.cs:241` tiene la tolleranza su N di 1 kN: è il residuo di F2-15 | Palo elastico con D ≥ 1,6 m: MRd nullo a tutte le quote, «Non verificabile»; la ricerca delle armature dice che il catalogo è insufficiente | `DomainPointAxialTolerance.Calculate`, come in F2-15. I punti non cambiano. |
| DET-1 (L) | Solette e pareti: l'interasse massimo è misurato anche attraverso lo spessore | 6Ø16 sopra e sotto a passo 185, h = 500: 424 mm «non soddisfatto». Dettagli negativi spuri oltre circa 490 mm di spessore, e la ricerca automatica scarta i candidati | Misurare l'interasse solo lungo gli strati. Stessa correzione di R12. |
| ARM-2 (A) | Fogli unificati: ogni riga SLU/SLV è verificata due volte a N–Mx–My, la seconda in assi locali | Con assi principali o personalizzati vengono escluse configurazioni valide (+49% di peso nell'esempio) e compare un controllo governante fittizio | Aggiungere le righe di taglio a rows["SLU"] solo nel contratto storico |
| PON-2 (L) | Storico non lineare e curve M–κ: il calcestruzzo teso oltre εcr è trattato come «fuori dominio» | Il metodo non lineare si ferma alla fessurazione della soletta, cioè con un momento negativo ordinario, e il messaggio parla di capacità | Nel calcestruzzo, σ = 0 oltre la fessurazione |
| SLU-3 (A, poi bassa) | M–χ: tolleranza su N fissa di 1 kN | Curva rifiutata per pali D ≥ 1,6 m e per lo stress block, mentre MRd allo stesso N esiste. Nessun numero sbagliato. | max(tolleranza utente; regola della libreria) |
| TAG-2 (A, poi bassa) | Foglio c.a. del palo con coefficienti unitari: taglio e torsione mescolano γ unitari e γ dell'input | VRd con γc = 1,5 e torsione con fyk/γs, mentre la flessione usa γ = 1 | γ e fyd dalla norma effettiva |
| PO-4 (A, non verificata) | «Sezione critica» del palo elastico sempre in testa (x = 0, M ≈ 0) | Report, riepilogo e foglio c.a. di approfondimento ricevono M ≈ 0 invece del massimo FEM (242 kNm nel caso di default) | Scegliere la critica per rapporto o per \|M\|; lo sviluppo come segnalazione separata |
| PO-5 (L+A, non verificata) | Sviluppo delle barre registrato come «NON SODDISFATTO» | Ogni palo elastico in c.a. risulta NON VERIFICATO (rosso), mentre il tratto dice «Parziale» e la guida «resta parziale» | Registrarlo come «da completare», e solo dove la domanda richiede l'armatura tesa |

## D. Gravità bassa

| Codice | Difetto | Verso | Verificata |
|---|---|---|---|
| DET-3 (L, Model) | B450A e B500A con εuk = 3% invece di 2,5% (NTC Tab. 11.3.Ic): εud 27‰ invece di 22,5‰ | non cautelativo sulla duttilità; MRd quasi invariato | sì |
| GEO-1 (L) | Meyerhof non drenato: manca iq sul termine q con carico inclinato | +4–10% su qlim; il riferimento Califfo congela «+ q», decide l'utente | sì |
| SLU-4 (L+A) | M–χ: ε0 del punto limite riferita al pivot, non al baricentro | etichetta | no |
| SLU-5 (L, Model) | Legame «Non lineare» EN 3.1.5: εc1 ed εcu1 calcolati con fcm all'età di «fck giorni» | piccolo, in tutti e due i versi | no |
| SLU-6 (L, Model) | σp dei trefoli nel resoconto: segno della sola deformazione meccanica | solo resoconto | no |
| TAG-3 (A) | Rifiuti del braccio del taglio in inglese (regressione di F4.13b) | testo | no |
| ARM-3 (L+A) | Sezione a T: peso delle staffe con la larghezza dell'ala | quantità circa ×2 | no |
| ARM-4 (A) | Nomi «plastica/elastica» delle verifiche N–Mx–My diversi dal criterio usato nei fogli unificati | testo | no |
| PV-2 (L+A) | cu > 0 richiesta anche negli strati coesivi interamente sotto la punta | rifiuto spurio | no |
| PV-3 (L) | Avviso z/D sempre presente, non distingue il troncamento non cautelativo alla punta | testo | no |
| PV-4 (L+A) | Micropalo: α > 0 richiesto anche fuori dalla zona iniettata, messaggio in inglese | rifiuto spurio | no |
| PO-6 (A) | Broms ignora gamma_acqua del foglio (usa 9,81) | +2,4% | no |
| MUR-4 (L) | Spostamento in testa: θ·H invece di θ·(H + t) | qualche mm | no |
| MUR-5 (L+A) | Sisma da sito: rifiuti con messaggi fuorvianti (kh «assegnato» per Wood, M–O del ribaltamento non validato) | testo | no |
| GEO-2 (L) | SlopeLoad.Horizontal: la documentazione dice +X, il calcolo lo tratta verso valle | contratto API | no |
| PON-3 (L+A) | Cassoncino in classe 4: sbalzi del fondo ridotti nel calcolo ma mostrati efficaci | report e disegno | no |
| PON-4 (L) | Pioli: un piolo per fila e passo trasversale vuoto bloccano il calcolo (0·NaN) | blocco | no |
| PON-5 (L) | Cumulativo: ammessa una fase «Solo acciaio» dopo il «Ritiro» | incoerenza con gli storici | no |

## Decisioni richieste all'utente

1. **Quali correggere ora.** La proposta è di correggere A, B e C nello stesso rilascio delle librerie di R1/R2.
   Le basse si possono correggere subito se il costo è minimo (testi, NaN, segni, ARM-3, PON-3/4/5, PV-2/4) e le altre
   si registrano.
2. **Scelte normative o di metodo:**
   - MUR-1: gemella con q assente sulla mensola, oppure q tolta solo da scorrimento e ribaltamento?
   - MUR-2: γR = 1,4 e limite η ≤ 0,5, con rifiuto o con avviso?
   - PON-1: Δσs secondo EN 1994-1-1 7.4.3(3), oppure solo un avviso?
   - TAG-1: correggere l'aiuto (verso il lembo teso), oppure invertire il segno passato alla libreria?
   - GEO-1: Meyerhof/USACE (iq anche con φ = 0) oppure il manuale Califfo (com'è ora)?
   - DET-3: εuk = 2,5% nel Model (release di GPCModelData; i fogli salvati con il preset cambiano)?
   - SLU-5: fcm a 28 giorni nel legame non lineare, con o senza opzione legacy?
3. **Opzioni o comportamento predefinito nella libreria.** Le correzioni che cambiano numeri nelle librerie (FES-1,
   SLU-1, MUR-x, PON-x, PO-2, PV-1) toccano anche gli altri programmi (ModelChecker, CheckerUI). Si può fare come per
   R1/R2, con opzioni spente per default e attivate da ANTHEA, così K0 e le fixture restano ferme. Oppure si correggono
   direttamente, perché sono errori e non scelte; in quel caso servono l'OK per K0 e un riscontro delle catture.
   Proposta: correzione diretta per SLU-1, PO-2, MUR-3, PO-3 e DET-1, che sono errori di calcolo senza alternativa
   normativa; opzione per le altre.

Ogni correzione segue la regola del refactoring: cattura prima, modifica, confronto dopo, voce nel registro, attesi
indipendenti (quelli Python dei verificatori, non l'uscita del codice).

## Esito delle correzioni

Stato al 16/10/2026, notte. L'utente ha approvato il piano («il piano va bene») e ha risposto ai punti della notte
([decisioni](decisioni.md), «16 ottobre (notte)»). Tutti i 40 codici sono corretti; per quattro resta aperta una parte.

Dove stanno le correzioni:
- **librerie**: rami di integrazione Checker `anthea-bug-1015` e Model `bugint-model` con `bug/model`, nel rilascio Z
  (non ancora in `lib/Checker`);
- **ANTHEA**: rami `refactoring/bug-1015` e `refactoring/bug-1015-geo`, uniti in `refactoring/integrazione-1016`.

Le catture congelate cambiate dalle correzioni sono rigenerate in Checker («Rigenera tutte»), K0 invariato. Hash del set,
profilo full e transizioni del confronto UI F3 si completano al rilascio Z.

| Codice | Esito | Voce del registro |
|---|---|---|
| SLU-1 | corretto (L+A) | BUG-SLU-1 |
| MUR-1 | corretto (L+A), gemelle anche nelle righe sismiche ed eccezionali | BUG-MUR-1 |
| PON-1 | corretto (L+A) | BUG-PON-1 |
| FES-1 | corretto (L+A), regola dell'agente confermata; due casi aperti | F2-25; BUG-FES-1b e BUG-FES-1c, da decidere |
| FES-2 | corretto (L+A), opzione della libreria attivata da ANTHEA | BUG-FES-2 |
| TAG-1 | corretto (A; verso dichiarato in libreria) | BUG-TAG-1 |
| DET-2 | corretto (A) | BUG-DET-2 |
| ARM-1 | corretto (A) | BUG-ARM-1 |
| PV-1 | corretto (L+A) | BUG-PV-1 |
| PO-1 | corretto (A), con rifiuto esplicito | BUG-PO-1 |
| PO-2 | corretto (L+A) | BUG-PO-2 |
| MUR-2 | corretto nello scorrimento (L+A); passiva nel ribaltamento aperta | BUG-MUR-2; BUG-MUR-2b, da decidere |
| MUR-3 | corretto (L+A) | BUG-MUR-3 |
| SLU-2 | corretto (L+A); «N e Mx/My costanti» fuori campo accettato in via provvisoria | F2-17, rettifica; BUG-SLU-2b, da decidere |
| PO-3 | corretto (L) | BUG-PO-3 |
| DET-1 | corretto (L), opzione legacy nominata; categoria nel riuso delle API | BUG-DET-1 |
| ARM-2 | corretto (A) | BUG-ARM-2 |
| PON-2 | corretto (L+A), con l'opzione «Tension stiffening» | BUG-PON-2 |
| SLU-3 | corretto (A) | BUG-SLU-3 |
| TAG-2 | corretto (A) | BUG-TAG-2 |
| PO-4 | corretto (A) | BUG-PO-4 |
| PO-5 | corretto (L+A); sviluppo parziale al piede aperto | BUG-PO-5; BUG-PO-5b, da decidere |
| DET-3 | corretto (Model e A) | BUG-DET-3 |
| GEO-1 | corretto (L) | BUG-GEO-1 |
| SLU-4 | corretto (L+A) | BUG-SLU-4 |
| SLU-5 | corretto (Model) | BUG-SLU-5 |
| SLU-6 | corretto (Model) | BUG-SLU-6 |
| TAG-3 | corretto (A), regressione di F4.13b | BUG-TAG-3 |
| ARM-3 | corretto (L+A) | BUG-ARM-3 |
| ARM-4 | corretto (A) | BUG-ARM-4 |
| PV-2 | corretto (L+A) | BUG-PV-2 |
| PV-3 | corretto (L), categoria nella regressione di ANTHEA | BUG-PV-3 |
| PV-4 | corretto (L+A) | BUG-PV-4 |
| PO-6 | corretto (A) | BUG-PO-6 |
| MUR-4 | corretto (L+A) | BUG-MUR-4 |
| MUR-5 | corretto (L+A), Mononobe-Okabe anche per riga sismica | BUG-MUR-5 |
| GEO-2 | corretto (L): contratto documentato e prova del segno | BUG-GEO-2 |
| PON-3 | corretto (L+A) | BUG-PON-3 |
| PON-4 | corretto (L) | BUG-PON-4 |
| PON-5 | corretto (L) | BUG-PON-5 |

**Difetti nuovi trovati durante le correzioni** («Correggili tutti»):

| Voce | Difetto | Esito |
|---|---|---|
| BUG-N1 | piano dei punti interpolati a N costante e fra campi di rottura (Checker) | corretto |
| BUG-N2 | ricerca a eccentricità costante: polo di compressione per la trazione pura nel dominio elastico (Checker) | corretto |
| BUG-N3 | overload a quattro argomenti della tensione di progetto degli acciai (Model) | corretto |
| BUG-N4 | FRP: difetto di SLU-6 e overload a quattro argomenti (Model) | corretto |
| BUG-N5 | classe del cemento assegnata dopo le proprietà meccaniche (Model) | corretto, nessun valore cambia |
| BUG-N6 | coefficienti unitari del palo in ancoraggi e dettagli (ANTHEA) | corretto |
| BUG-N7 | classe FRP modellata come un acciaio, senza CNR-DT 200 né ACI 440 (non usata) | da decidere |
| BUG-TS-1 | tension stiffening opzionale nelle analisi di risposta, su richiesta dell'utente | attuato in libreria; campo del M–χ in ANTHEA al rilascio Z |
| BUG-TS-2 | domande 1–8 sul tension stiffening | da decidere |

Fanno parte delle voci dei codici: la mappatura degli esiti di SLU-2 in ModelChecker (F2-17), Mononobe-Okabe per riga
sismica (BUG-MUR-5) e le gemelle sismiche ed eccezionali (BUG-MUR-1).

**Ancora da fare**, non nel registro:
- seguiti di ANTHEA, sul ramo `refactoring/seguiti-1016`:
  - deformazione di snervamento del M–χ con i coefficienti unitari del palo;
  - grafico N–M «proietta», che disegna sign·hypot mentre la verifica usa la proiezione;
  - nota DET-3 nella scheda dei materiali dei muri;
  - esposizione del campo «CLS teso nelle risposte» con le DLL candidate;
- frasi delle guide teorica e pratica, sul ramo `refactoring/guide-1016`;
- documenti di Checker:
  - regola della tolleranza su N in ca.momento-curvatura §7;
  - righe citate di `CalculateDesignStressCommon`;
  - conteggi del README degli storici del ponte;
  - istantanee delle API in `build/api`;
- BridgeAudit non compila contro l'ANTHEA attuale (ramo `bug/bridge-audit`). Il confronto denso con F2-pre-m4-v2 non è
  stato eseguito.

**Segnalati e non corretti**, da valutare:
- `ApproximateScore` della ricerca usa ancora la larghezza dell'ala;
- «a,c» solo nei dettagli inglesi della libreria, non nella traccia;
- il testo dell'ancoraggio «Barra rettilinea, nessuna riduzione favorevole» non dichiara l'ipotesi di barre nervate;
- lo stress block è discontinuo in compressione centrata: è la causa dei mancati calcoli del M–χ D800 con N −1500 kN;
- `ConsiderTensileConcrete` («CLS resistente a trazione» in ANTHEA) entra nel dominio SLU;
- gli overload a due argomenti degli acciai usano in compressione le proprietà di trazione;
- R12 (pilastri) resta da decidere.
