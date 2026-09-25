# Palo, micropalo e Progetti — 15 settembre 2026

Richiesta dell'utente in otto punti. A/B: visualizzazione k, μ e Nq anche nei
coesivi; aggiornamento immediato dell'inizio aderenza nel profilo; immagine
stratigrafica nel report micropalo; pulsanti [+] nell'albero dei Progetti e
schede trascinabili sulle strutture. Clic destro per rinominare/eliminare;
doppio clic sulla scheda per aggiungerla alla struttura selezionata. Palette
scorrevole e finestra di inserimento ridimensionabile.

D — modifiche ingegneristiche esplicitamente autorizzate:

- Converse–Labarre e Feld: ηt = ηc, su entrambi i moduli. I coefficienti
  manuali rimangono distinti; resistenze caratteristiche, aderenza limite e
  peso non vengono moltiplicati per l'efficienza.
- Verifica non drenata del palo: sopra falda si usa c′ + k μ σ′v; sotto
  falda, nei coesivi, si conserva α Cu. Uno strato attraversato dalla falda
  è integrato separatamente nelle due porzioni. τu nel dettaglio è la media
  equivalente sul tratto. Alla punta z ≤ zf si usa la legge drenata, sotto
  falda la legge precedente; senza falda le due verifiche coincidono.
  Cu non è richiesto per strati interamente fuori dalla zona non drenata.
- Micropalo inclinato rettilineo in strati orizzontali: θ dalla verticale,
  0° ≤ θ < 90°. L e sb sono misurati lungo l'asse, gli spessori restano
  verticali. Coordinate assiali degli attraversamenti = z / cos θ;
  aderenza integrata sulle lunghezze effettive, limitate da sb e L.
  Le curve e le tabelle usano la coordinata assiale s, non la profondità z.
  Azioni di input assiali; componente del peso nelle azioni Gk = q L cos θ.
  Il disegno riporta z verticale e l'inclinazione effettiva. Non sono
  aggiunte verifiche trasversali o strutturali del micropalo.
- Micropalo: `laterale_attiva=false` annulla l'aderenza del singolo strato
  mantenendone la geometria. α e abaco non sono richiesti per quel tratto.
  Se abilitata, la punta resta la percentuale della laterale risultante,
  quindi anche il suo contributo può diminuire. Report e immagine indicano
  gli strati esclusi; non vengono creati abachi per tratti esclusi.

Compatibilità: nei file senza inclinazione si assume 0°; senza il flag di
aderenza lo strato resta attivo. Le chiavi storiche, compresa
`inizio_aderenza`, restano compatibili. Le convenzioni sono esplicite in UI,
nei risultati API e nel report. Le precedenti note sull'efficienza dell'11
settembre sono superate dalla richiesta del 15 settembre.

Confronti riproducibili (kN; casi analitici nei test, non progetti dell'utente):

| Caso / grandezza | Prima | Dopo | Differenza | Variazione |
| --- | ---: | ---: | ---: | ---: |
| Palo coesivo senza falda, Rs non drenata calcolata | 549,778714 | 1063,979315 | +514,200600 | +93,529% |
| Stesso palo, falda a 4 m, Rs non drenata calcolata | 549,778714 | 537,803031 | −11,975684 | −2,178% |
| Micropalo Feld 2×2, Rd trazione | 1974,028984 | 1603,898549 | −370,130434 | −18,750% |
| Micropalo a 60° rispetto al verticale, Rs calcolata | 4194,811591 | 3754,988619 | −439,822972 | −10,485% |
| Primo strato escluso, Rs micropalo calcolata | 4194,811591 | 3117,245311 | −1077,566280 | −25,688% |

Il confronto completo, inclusi i casi di riferimento storici, è in
`verifiche_geo/confronto_numerico.json`, rigenerabile con
`python tests/confronto_modifiche_geo.py`. Non sono stati modificati
`tests/riferimenti_originali.json` o `tests/riferimenti_parametrizzati.json`.
Il test di confronto storico continua a controllare tutte le parti
invariate; le nuove leggi sono verificate con conti indipendenti.

Verifiche: 59 test automatici; test GUI dei [+], drag/drop valido e annullato,
visibilità pulsante Aggiungi, parametri nei coesivi, aggiornamento profilo,
salvataggio/ripristino, immagine incorporata nel DOCX, a scaling Tk 1,333 e 2.
Immagine del report controllata visivamente.

File interessati: `programma/core.py`, `data.py`, `calcolo.py`,
`bustamante_doix.py`, `geometria_micropalo.py`, `ui_palo.py`,
`ui_micropalo.py`, `pannello_grafici.py`, `report_dati.py`,
`figure_micropalo.py`, `tabelle.py`, `app.py`; test e questa documentazione.

Fonte delle nuove ipotesi: richiesta esplicita del progettista del
15 settembre 2026; non sono presentate come prescrizioni normative.
Risultati numerici modificati come sopra. Revisione ingegneristica: sì.

# Efficienza micropali — 11 settembre 2026

Sostituito il riquadro Metodo di calcolo con Efficienza, come richiesto. Disponibili Nessuna riduzione, Converse-Labarre, Feld e Definita dall'utente, con le formule già presenti nel palo. Calcolo indipendente dalla lunghezza. I fattori si applicano alle resistenze di progetto a compressione/trazione, non ad aderenza limite e peso. Converse-Labarre e Feld riducono la compressione; la trazione resta unitaria salvo definizione utente, come nella scheda palo.

Impostazioni salvate nel foglio, riportate nel report e ripristinate all'apertura. Vecchi fogli senza efficienza: Nessuna riduzione. Modifica D richiesta; con fattori unitari risultati invariati. Test: 37 automatici e GUI in 12 combinazioni, inclusi assenza lunghezza, ricalcolo, salvataggio e abachi nel report. Separato il riconoscimento del micropalo dall'abilitazione dell'efficienza per preservare le tabelle dei grafici.

# Pressione unica negli abachi — ipotesi autorizzata p_l = p_i

Modifica D esplicitamente approvata dall'utente: la pressione d'iniezione generale p_i viene usata come p_l negli abachi Bustamante–Doix. È un'ipotesi progettuale, non l'identificazione delle due grandezze nel testo di Viggiani.

Eliminato l'input p_l dalla stratigrafia e dai nuovi file. I valori locali nei vecchi file sono ignorati. La pressione generale aggiorna subito α/s visualizzate (α rimane il valore adottato) e tutte le resistenze. Restano i limiti degli abachi, senza estrapolazione.

Report, didascalie e ascissa dei grafici dichiarano p_l = p_i. Eliminato il confronto fra due pressioni indipendenti, che sotto questa ipotesi sarebbe tautologico. Per strati con precedenti p_l diversi da p_i, le resistenze cambiano. Peso CHS e scheda palo invariati.

Verifiche: 35 test, conto manuale con pressione uniforme, influenza di p_i sulle curve, irrilevanza dei p_l locali precedenti; GUI in 12 combinazioni e aggiornamento s al variare di p_i; grafico Word controllato visivamente.

# Micropali — CHS, iniezione uniforme e report degli abachi

- D, modifiche richieste: eliminato il tratto superficiale automaticamente IGU. Il tipo selezionato è applicato all'intera zona iniettata. Con IRS dalla superficie, il report esplicita la deroga alla raccomandazione Viggiani p. 396.
- Pressione di iniezione p_i unica nei Dati generali, in MPa. La pressione limite Ménard p_l rimane un parametro geotecnico per strato: è distinta da p_i e determina s. Non viene sostituita arbitrariamente con la pressione della pompa. Segnalate le condizioni esecutive p_i/p_l non rispettate, senza modificare gli abachi.
- Falda e sottospinta rimosse dalla scheda e dal calcolo del micropalo, anche aprendo dati precedenti. Il palo resta invariato.
- Catalogo CHS da dimensioni Tata Steel Celsius; massa nominale calcolata geometricamente con 7850 kg/m³ e g=9,81 m/s². Tubo pienamente riempito lungo tutta L: As=π[De²−(De−2t)²]/4, Ac=πDb²/4−As; q=As×7850×9,81/1000+Ac×γc. γc editabile, inizialmente 25 kN/m³. Il CHS deve stare dentro Db. Nessuna verifica strutturale o di copriferro è implicita nel catalogo.
- Una sola α e una sola s per strato, per il tipo scelto. Cambiando IGU/IRS, α riparte dal minimo della relativa tabella. I vecchi fogli BD importano l'α del tipo salvato e richiedono CHS e p_i; nessuna scelta viene ereditata dal foglio precedentemente aperto.
- Report: abaco per ogni strato effettivamente calcolato, curva adottata evidenziata, punto (p_l,s), riferimenti e didascalia; peso acciaio e calcestruzzo distinti.
- Verifiche: 35 test, regressione palo invariata, IRS anche nei primi 5 m, geometria e peso CHS, distinzione p_i/p_l, esclusione falda, limiti e abachi. GUI su 12 combinazioni, salvataggio/riapertura, Word con abachi incorporati e controllo visivo del grafico.

# Opzioni della scheda palo — 10 settembre 2026

- D, su richiesta: casella Laterale per ogni strato (prima colonna). Disattivandola si annullano i contributi laterali drenati e non drenati, a compressione e trazione. Lo strato conserva peso, tensioni efficaci e contributo di punta. Default attivo, anche per i file precedenti. Booleano salvato nel foglio; stato indicato nelle tabelle del report e nel dettaglio ogni mezzo metro.
- A/B: Reset nel blocco normativa: verticali=1, ξ3=ξ4=1,70, γs=1,15, γt=1,25, γb=1,35, γG sfav=1,30, γG fav=1,00. Sono i valori iniziali già presenti nel programma; nessuna revisione normativa.
- B: efficienza calcolata separatamente dalla capacità portante, senza L né stratigrafia. Converse-Labarre richiede il diametro quando necessario, oltre agli interassi; gli altri metodi richiedono soltanto i rispettivi input. Formule invariate.
- B: Report apre prima la scelta delle sezioni e dei grafici, con dati/calcoli selezionati e grafici esclusi. Si può annullare o scegliere singoli contenuti; la destinazione Word viene chiesta dopo. Il comportamento del report micropali resta invariato.
- Verifiche: 30 test automatici, regressione dei risultati con laterale attiva invariata; prove di esclusione di strato e tutti gli strati, preservazione di tensioni/punta/azioni, calcolo efficienza indipendente e selezione report. GUI in 12 combinazioni: reset, casella, salvataggio, dialogo report e geometrie. File Word predefinito privo di immagini verificato.
- Riferimenti numerici originali conservati; le resistenze cambiano soltanto quando si esclude un contributo o si ripristinano coefficienti prima modificati. La scelta degli strati da escludere resta una decisione progettuale dell'utente.

# Aggiornamento micropali — Bustamante–Doix (10 settembre 2026)

- A/B: scheda micropali uniformata al palo, testata strati compatta, tabella con p_l, α IGU/IRS e s IGU/IRS; il riquadro vuoto Efficienza ospita il metodo e le indicazioni di compilazione. Restano i nuovi comandi dei grafici e i valori delle resistenze.
- D, esplicitamente richiesto: FHWA sostituito da Bustamante–Doix secondo Viggiani, Fondazioni, §13.1.6, pp. 392–396. Fonte fornita dall'utente, tab. 13.12–13.13, eq. 13.21 e fig. 13.16–13.19.
- Rs = Σ π α Db Ls s. α adimensionale inizializzato al minimo della tabella, modificabile entro l'intervallo. Pressione limite Ménard p_l in MPa; abachi digitalizzati con interpolazione lineare, senza estrapolazione. Nessuna conversione automatica da NSPT. Tratti tratteggiati AL esclusi, R1/R2 adottate come valori inferiori.
- Se l'iniezione parte dalla superficie, primi 5 m IGU. Avviso sulla lunghezza utile inferiore ai 4 m raccomandati. Punta facoltativa, inizialmente 15% di Rs quando attivata; non implementata l'alternativa k_p. Parametri di progetto, peso e falda mantenuti.
- Ingegneria: risultati micropali modificati rispetto a FHWA; vecchi fogli apribili ma da ricompilare nei nuovi parametri, senza reinterpretare le aderenze. Necessaria revisione ingegneristica delle letture da abaco e dei parametri esecutivi.
- Numeri: regressione del palo invariata. Conto manuale BD con Db=0,25 m, zb=1,2 m, L=10,3 m, IRS, due strati: Rs=π×0,25×(1,4×2,8×250 + 1,8×6,3×350); verificati anche punta, trazione e primi 5 m IGU.
- Test: 24 test automatici; GUI in 12 combinazioni, salvataggio/riapertura e report. Riferimenti numerici originali conservati intatti. Abachi, confronto grafico e limiti in riferimenti_micropali/.

# Aggiornamento del 10 settembre 2026 — unico Nq e grafici

- A: intestazione stratigrafica ridotta da 66 a 50 unità, con minimo adattato al testo; normativa da 28% a 24% della larghezza (micropalo da 31% a 27%), spazio restituito alla verifica; descrizioni a capo.
- B: valori delle resistenze alla lunghezza del palo visibili sotto il grafico; comandi individuali e Tutte/Nessuna, indipendenti dalle curve sovrapposte. Ripristino dei controlli dopo dati non validi.
- C, richiesto dall'utente: eliminate le implementazioni Originale e Raffinata. Importazione dei vecchi fogli nel metodo Parametrizzata con provenienza e avviso di possibile variazione dei risultati.
- Formule, coefficienti, limiti e interpolazione L/D della parametrizzazione restano invariati. Nessuna nuova riduzione di phi. Fonte: parametrizzazione e riferimenti NQ-2026-09-09 già presenti nel progetto.
- Verifica numerica: sei casi congelati PRIMA della rimozione, valutati con il metodo Parametrizzata installato; curve, azioni e dettagli restano identici. Conservato intatto riferimenti_originali.json.
- Verifiche: 15 test automatici; GUI palo e micropalo, 3 risoluzioni e 2 scale caratteri, accensione/spegnimento di ogni serie e ripristino controlli; generazione report Word.
- Per i fogli precedentemente Originale/Raffinata, verificare i nuovi risultati prima dell'uso progettuale.

# Modifiche X — 8 settembre 2026

A — Interfaccia: altezza superiore adattata al contenuto, testi a capo e campi proporzionati al font; ηc e ηt sulla stessa riga; larghezze delle colonne condivise fra intestazioni e celle. Scorrimento per finestre piccole e DPI elevati.

A/B — Grafici: riepilogo esterno al disegno, legenda a più colonne quando la larghezza lo consente; tabelle Minimo/Media a destra nei blocchi larghi, sotto nei blocchi stretti, con scorrimento. Le tabelle mostrano la compressione e la condizione drenante/non drenante selezionata. L significa laterale, P punta. Rc,Ed è l’etichetta richiesta per il ramo della resistenza di progetto (Rd), non per l’azione Ed.

B — Architettura: avvio, GUI, calcolo, dati, archivio JSON, grafici e report separati. Il motore non importa Tk. Prima delle modifiche numeriche, tutte le curve e le azioni dei sei casi di riferimento sono risultate identiche dopo la sola estrazione.

B/C — Tabelle Word: ricalcolo alle quote esatte ogni 0,50 m, più quota iniziale e finale. Inclusi parametri di punta, integrazione per strato, contributi per sondaggio, rami minimi/medi, azioni, resistenze e utilizzi. Se manca copertura stratigrafica, il report lo dichiara e termina alla profondità disponibile.

C — Nq: aggiunta interpolazione cubica monotona PCHIP in φ; scala logaritmica delle ordinate per D ≤ 0,8 m, lineare per D > 0,8 m. Interpolazione in log(z/D) e punti originali conservati. Nessuna riduzione di φ nella determinazione di Nq. Il ¾φ riguarda solo μ del calcestruzzo prefabbricato battuto.

Le curve sono digitalizzazioni approssimate: i nodi e i tratti piatti originali, anche a Nq=200, non sono stati reinterpretati come dati più precisi. La raffinazione non equivale a una nuova digitalizzazione. Non sono state aggiunte fonti normative non verificate. Per i nuovi fogli è preimpostata Raffinata; i file privi di metodo_nq si aprono con Originale. Il selettore Nq è accanto alla vista dei grafici.

D — Falda interna a uno strato: corretta l’integrazione della tensione media lungo il fusto. Si integrano separatamente i tratti sopra e sotto falda. La tensione efficace alla punta resta invariata. Esempio verificabile: strato 10 m, falda a 5 m, γ=20 e γsat=19,81 kN/m³; σ′media passa da 75 a 87,5 kPa (+16,67%). Il test confronta anche lo stesso terreno diviso in due strati alla quota della falda.

C — Input: dati numerici errati o non finiti e spessori non validi sono segnalati; non vengono più saltati silenziosamente. Controllo della struttura interna dei file prima dell’importazione. Il formato contenitore .programma rimane versione 1.

## Confronto delle resistenze finali con l’originale

Questa tabella usa Nq Originale per isolare la correzione della falda. Valori in kN. I riferimenti sono test software, non casi certificati.

| Caso | Curva | Prima | Dopo | Δ kN | Δ % |
|---|---|---:|---:|---:|---:|
| palo_0 | drenante_compressione | 2985.490116 | 2985.490116 | 0.000000 | 0.0000 |
| palo_0 | drenante_trazione | 738.138626 | 738.138626 | 0.000000 | 0.0000 |
| palo_0 | non_drenante_compressione | 2985.490116 | 2985.490116 | 0.000000 | 0.0000 |
| palo_0 | non_drenante_trazione | 738.138626 | 738.138626 | 0.000000 | 0.0000 |
| palo_1 | drenante_compressione | 1681.209974 | 1681.209974 | 0.000000 | 0.0000 |
| palo_1 | drenante_trazione | 442.883176 | 442.883176 | 0.000000 | 0.0000 |
| palo_1 | non_drenante_compressione | 1723.939854 | 1723.939854 | 0.000000 | 0.0000 |
| palo_1 | non_drenante_trazione | 493.881865 | 493.881865 | 0.000000 | 0.0000 |
| palo_2 | drenante_compressione | 2252.184098 | 2261.166560 | 8.982462 | 0.3988 |
| palo_2 | drenante_trazione | 446.400469 | 456.647751 | 10.247282 | 2.2955 |
| palo_2 | non_drenante_compressione | 2328.631456 | 2328.631456 | 0.000000 | 0.0000 |
| palo_2 | non_drenante_trazione | 533.612374 | 533.612374 | 0.000000 | 0.0000 |
| palo_3 | drenante_compressione | 1709.487995 | 1719.985406 | 10.497411 | 0.6141 |
| palo_3 | drenante_trazione | 496.960183 | 507.825003 | 10.864820 | 2.1863 |
| palo_3 | non_drenante_compressione | 1709.487995 | 1719.985406 | 10.497411 | 0.6141 |
| palo_3 | non_drenante_trazione | 496.960183 | 507.825003 | 10.864820 | 2.1863 |
| micropalo_False | compressione | 641.174153 | 641.174153 | 0.000000 | 0.0000 |
| micropalo_False | trazione | 589.880221 | 589.880221 | 0.000000 | 0.0000 |
| micropalo_True | compressione | 695.792692 | 695.792692 | 0.000000 | 0.0000 |
| micropalo_True | trazione | 589.880221 | 589.880221 | 0.000000 | 0.0000 |

## Confronto Nq a parità di input

Le differenze seguenti dipendono soltanto dall’interpolazione in φ.

| D m | φ gradi | z/D | Originale | Raffinata | Δ | Δ % |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 31 | 12.3 | 16.184072 | 15.678819 | -0.505253 | -3.1219 |
| 1 | 35 | 12.3 | 25.799023 | 24.748838 | -1.050185 | -4.0706 |
| 0.6 | 31 | 20.5 | 27.306539 | 27.310787 | 0.004248 | 0.0156 |
| 0.8 | 35 | 15.375 | 60.532432 | 60.311867 | -0.220565 | -0.3644 |

## Verifiche e limiti

Test numerici: confronti con il sorgente originale per i casi senza falda interna, casi manuali per falda e micropali, nodi/monotonia/PCHIP, componenti e quote di mezzo metro, input errati, salvataggio atomico e XML Word.
Test Tk: pali e micropali a 1200×760, 1440×900, 1600×1000 e 1900×1100; modalità normale, stratigrafia estesa e grafici estesi; caratteri a scala Tk circa 1,33 e 2,0. Controllati dimensioni dei testi superiori e allineamento delle colonne. Il controllo visivo sul desktop effettivo non è stato possibile nell’ambiente di esecuzione.
Le modifiche di calcolo richiedono revisione ingegneristica prima dell’impiego professionale. Questa attività non costituisce una validazione normativa del programma. La minima di base e la minima laterale continuano a essere prese separatamente, come nell’originale; nessuna modifica di questo criterio.

Fonte originale SHA-256: `033e4ec9de48a575f4838c64f2282ce56288dbf7db1bd7b33f7a243e487a465f`.


Aggiornamento layout: blocchi inferiori più alti; barra adattata al titolo e al sottotitolo. Le schede Grafico e Tabelle e dettagli separano il disegno dai riepiloghi. Estendi dedica tutta l’area del modulo al grafico; Ripristina torna alla disposizione completa. Formule e risultati numerici invariati. Verificati intestazione e contenimento del grafico a 1600×1000 e 1366×768, per pali e micropali, scale Tk 1,33 e 2,0 (otto combinazioni). Dieci test di regressione superati.


## Proporzioni pannelli palo (8 settembre 2026)
Categoria A/B, solo interfaccia: ridotti margini verticali dei campi superiori e rimossa altezza uniforme delle righe; pannelli inferiori anticipati di 74-104 px nelle prove. Spazio minimo inferiore da 580 a 680 px. Font e calcoli invariati. Verifica geometrica su 12 combinazioni di modulo, finestra e scala, inclusa estensione grafico. Le finestre basse conservano lo scorrimento.


## 09/09/2026 - Parametrizzazione Nq (C/D, modifica numerica autorizzata)

Nuovo metodo **Parametrizzata**, versione NQ-2026-09-09, predefinito per i nuovi fogli. Vecchi file con metodo Originale/Raffinata o senza campo metodo conservano il precedente metodo; selezionare esplicitamente Parametrizzata per ricalcolarli. Avviso visibile in Tabelle e dettagli.

D <= 0.80 m: esponenziali dai punti phi10/phi100 forniti dall’utente. D > 0.80 m: cubiche a tratti con raccordi C2 a 34°/38°, adattate all’immagine più nitida. La cubica unica dell’utente resta documentata per confronto. Phi in gradi, non ridotto. Interpolazione logaritmica in z/D mantenuta; geometrica nelle ordinate dei medi e aritmetica nei grandi, esplicitamente dichiarata come scelta numerica.

Intervalli delle singole tracce e valori di bordo espliciti nel codice. Le quote fuori campo producono avvisi; export ogni 0.50 m con D, phi, z/D, valori adottati, curve e peso d’interpolazione. Figure Word generate dalle formule effettive, con distinzione Nq/Nq*.

Report di confronto: Verifica_curve_Nq.pdf. Immagini inalterate, coefficienti, punti fit/controllo e hash sorgenti in riferimenti_nq. Scarti sui 39 punti di controllo per curva grande: massimi relativi 0.85% (L/D=4), 0.77% (32). Esponenziali medie: massimi 4.45-5.53% nel tratto controllato tra circa27° e phi100; immagine con scala logaritmica non uniforme. Non è una validazione fisica/normativa indipendente, manca la citazione bibliografica completa.

Delta Rd compressione drenata a L vs Raffinata, medesimi input dei test: palo_0 +334.22 kN (+11.54%); palo_1 +58.06 (+3.46%); palo_2 +58.97 (+2.62%); palo_3 +188.20 (+11.27%). I due micropali invariati. Azioni, pesi e contributi laterali invariati. File riferimenti_originali.json non modificato.

Verifiche: 17 test automatici; 12 combinazioni di modulo/finestra/scala; export Word reale generato e XML verificato; PDF di 4 pagine renderizzato e ispezionato. Revisione ingegneristica della fonte e dei limiti necessaria prima dell’uso progettuale.


## 10/09/2026 - Pulizia cartella (categoria B)
Rimosse copie storiche e cache dalla cartella operativa dopo archivio ZIP verificato. Documenti e fonti originali raggruppati in documentazione/. Codice, launcher, test e riferimenti_nq invariati. Nessuna modifica ingegneristica o numerica.
# α micropali liberamente editabile — 11 settembre 2026

Modifica D richiesta: nella stratigrafia del micropalo α è un valore adottato liberamente editabile e può essere esterno all'intervallo consigliato della tabella Viggiani 13.12. Quando si sceglie il terreno o si cambia IGU/IRS, la casella viene inizializzata con il minimo consigliato; l'utente può poi sostituirlo con qualsiasi valore positivo. L'intervallo consigliato resta associato al parametro per la tracciabilità. Le formule Bustamante–Doix e gli abachi di aderenza non cambiano; cambiano le resistenze solo se l'utente adotta un α diverso.

## 2026-09-11 — Sezione in c.a., pressoflessione SLU (A/B)
- Integrato `CLS_Flessione.py` da Desktop/Pitone come `programma/cls_flessione.py`: fonte invariata salvo marchio visibile X.
- Scheda Strutture rinominata da Palo / Verifica strutturale a Sezione in c.a. / Pressoflessione SLU, disponibile con icona di sezione armata.
- Adattatore `ui_sezione_ca.py`: incorporamento, salvataggio nei file e nei fogli X, gestione dei callback alla chiusura. ID storico `str_palo` mantenuto per compatibilità.
- Unità proprie della fonte conservate: mm, MPa, kN, kNm. Nessuna conversione implicita verso i moduli geotecnici. Formule, coefficienti, impostazioni iniziali e prescrizioni opzionali per pali invariati.
- Report Word della sezione non disponibile: comando protetto con messaggio esplicito.
- Verifica: 40 test unitari superati, inclusi gli autotest originali CLS; smoke test GUI con calcolo, esportazione/importazione, riapertura e chiusura superato. Confronto della fonte: identica salvo due sostituzioni di marchio UI. Nessuna modifica a tests/riferimenti_originali.json.
- Il controllo di integrazione non costituisce una nuova validazione normativa del modulo fornito.

## 2026-09-11 — Layout sezione in c.a. in una schermata (A/B)
- Scheda incorporata organizzata su due fasce: sei colonne superiori (geometria, materiali, armatura, azioni SLU, SLV, SLE) e tre riquadri inferiori (sezione, dominio, verifiche).
- Eliminati intestazione duplicata e scorrimento degli input. Grafici adattati allo spazio disponibile; dettaglio verifiche scorrevole solo in caso di eccedenza per non nascondere avvisi.
- SLV e SLE indicati come non implementati, senza introdurre azioni o criteri di calcolo nuovi.
- Controllato layout a 1200x700 per sezione circolare, rettangolare e a T. Formule, unità, valori iniziali e codice del motore invariati.

## 2026-09-11 — Stile sezione allineato alla capacità portante (A)
- Riutilizzato CampoNumericoArrotondato della scheda palo, con riempimento #F8FAFC, bordi chiari e focus blu.
- Titoli scuri su schede bianche, spaziatura tra pannelli, pulsante blu X e risultati su righe chiare; esiti e avvisi distinti per colore.
- Conservata la disposizione su una schermata. Test GUI delle tre sezioni a 1200x700 superato, con importazione/esportazione e chiusura.
- Nessuna modifica al motore, agli input salvati o ai risultati numerici.

## 2026-09-11 — Rimozione marchio prima del rilascio (A/B)
- Nome pubblico provvisorio X; pacchetto programma e avvio Avvia X.cmd.
- Aggiornati sorgenti, importazioni, test, documentazione e metadati Word. Eliminato il logo incorporato e svuotate le cache precedenti.
- Nuovi archivi .programma con identificatore X. Lettura degli archivi precedenti preservata tramite identificatore storico codificato: compatibilità, non cifratura.
- 41 test superati, compreso importazione e risalvataggio degli archivi precedenti; controllo GUI superato.
- Formule e risultati numerici invariati. Intervento limitato alla cartella del progetto; copie esterne e cronologia delle conversazioni non modificate.

## 2026-09-11 — Richieste MODIFICHE.docx: sezione generale (A/B/D)
- Interfaccia: righe nome/simbolo/valore/unità, CLS e acciaio separati, classi CLS e diametri a tendina, aree per gruppi. Copriferro netto al bordo esterno della staffa (formula originale già coerente).
- SLU, SLV e SLE a destra con combinazioni multiple e viste dettagliate. Disegno indipendente da materiali/azioni: CLS compresso rosso, resto grigio, barre tese blu. Domini N-Mx (My=0) e Mx-My a N selezionato, plastici/elastici.
- Coefficienti αcc, γc, γs e fyk preservati e accessibili dal pulsante Coefficienti materiali. Formato interno sezione versione 2; importazione precedente supportata con messaggio esplicito per rimozione delle opzioni autorizzata.

### Ipotesi ingegneristiche
- Eccentricità minima e requisiti specifici rimossi dalla scheda generale come richiesto.
- SLU conserva il motore parabola-rettangolo ed elastico-perfettamente plastico originale. εsu=67,5‰ è informativo, tratto dal documento: il motore originale non limita la deformazione ultima dell'acciaio (tooltip esplicito).
- SLV: sezione piana, CLS non teso, limite al primo |σs|=fyd come chiarito dall'utente; il superamento di fcd al limite acciaio è segnalato, senza sostituire il criterio richiesto.
- SLE: metodo n, CLS non teso, n automatico Es/Ecm editabile. n automatico usato a piena precisione; tensione acciaio come massimo valore assoluto, CLS come massima compressione.
- Ecm=22000*((fck+8)/10)^0,3 MPa, modulo medio secante senza correzioni aggregati/viscosità. Fonte JRC: https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/EN1998_6_Kolias.pdf .
- Equilibrio elastico: sistema 3x3 scalato con pivoting e riduzione del passo; tolleranza relativa 1e-9, errori espliciti per singolarità/non convergenza. Unità mm, MPa, kN, kNm; Mx=ΣF*y e My=-ΣF*x.
- Domini a fibre con interpolazione lineare, senza estrapolazione. R SLU resta 1/λ del metodo direzionale originale. Mx-My visualizza le risultanti separate: coerenza del criterio direzionale per sezioni non simmetriche da sottoporre a revisione ingegneristica, senza sostituzione silenziosa del criterio.

### Risultati numerici
- Base N=2500 kN, Mx=500 kNm, My=250 kNm: MRd precedente e nuovo 1749,773286331 kNm, delta 0 (0%); R 0,270486589794, invariato.
- N=1000 kN, Mx=My=0: MRd 1387,784419979 kNm invariato; momento efficace 20 -> 0 kNm, delta -20 kNm (-100%), per rimozione eccentricità minima. R 0,057901065914 -> 0,054746955134, delta -0,003154110780 (-5,447%).
- Nuovi risultati base: MRd elastico 1625,968514755 kNm; SLE n automatico |σs|max=46,445715117 MPa, σc,max=9,104812984 MPa. Nessun precedente SLV/SLE disponibile.

### Test e questioni aperte
- 49 test superati: regressione precedente, equilibrio uniforme compressione/trazione, carico nullo, segni, omogeneità, snervamento, profili plastici confrontati con fonte e simmetria dominio.
- GUI: tre forme, combinazioni multiple, n automatico/manuale, import/export, disegno con materiali mancanti. Controllo visivo a 1600x990 con DPI di avvio normale.
- Non comprende secondo ordine, taglio, fessurazione o deformabilità. Tensioni SLE non equivalgono alla verifica completa di esercizio.
- Richiede revisione dell'ingegnere: sì, per nuovi metodi, domini e limiti dichiarati. Nessuna modifica a riferimenti_originali.json.
- Aggiunto confronto indipendente con soluzione analitica della sezione rettangolare doppiamente armata fessurata: tensioni CLS/acciaio entro 0,3% (tolleranza di discretizzazione). Totale 50 test superati.
- La vista della combinazione SLU/SLV riporta entrambi i momenti resistenti, plastico ed elastico; un eventuale errore del confronto elastico non cancella il risultato SLU disponibile.

## 2026-09-11 — Calcolo manuale e disegno immediato (B)
- Aggiunto pulsante Calcola nella scheda generale. Avvio, importazione e modifiche agli input non eseguono più analisi SLU/SLV/SLE o costruzione dei domini.
- La geometria e le armature si aggiornano al successivo ciclo grafico, senza attese temporizzate; proprietà dei materiali e aree restano aggiornate con operazioni leggere.
- Modificando gli input vengono invalidati risultati e domini precedenti e appare l'indicazione di premere Calcola. Un dominio diverso non ancora calcolato attende il pulsante; quelli in memoria si ridisegnano senza analisi.
- Durante il calcolo il pulsante è disabilitato e segnala lo stato di esecuzione; ripristino garantito alla fine.
- Test dedicato: avvio/importazione/modifica con tutte le funzioni pesanti bloccate, aggiornamento del diametro, geometria con fck mancante, invalidazione esiti e comando manuale superato.
- Nessuna modifica a formule, coefficienti, precisione o criteri di verifica. Risultati numerici invariati a parità di input.
# Punta non drenata lorda — 16 settembre 2026

Modifica D richiesta dall'utente nel programma generale Prog: per punta coesiva sotto falda, Rb,calc = Ab × (Nc × Cu + σv,L). Prima era Ab × Nc × Cu. σv,L è totale, integrata con γ sopra falda e γsat sotto; se γsat manca, si usa γ come già previsto per gli input. Gli strati con laterale esclusa conservano il peso. Nessuna variazione alle formule drenate, alle resistenze laterali, ai coefficienti o alle azioni. La casella sottospinta conserva il comportamento esistente sulle azioni; σv,L non viene ridotta dalla pressione idrostatica. Questa opzione rimane una scelta distinta da valutare nel bilancio delle azioni.

Riferimento della modifica: formulazione lorda esplicitamente richiesta nella conversazione del 16/09/2026, non una nuova prescrizione normativa. Applicazione invariata: sopra falda, senza falda o in terreno granulare, il ramo di punta non drenato usa la formula drenata. I file di calcolo e le fixture storiche restano immutati; i vecchi fogli vengono ricalcolati con la nuova formula. Report aggiornato con criterio e σv totale nel dettaglio ogni 0,50 m.

Caso Test.programma (letto soltanto): D=1,2 m, L=32 m, γ=19,3 per 18 m e 19,7 per 14 m, Cu punta=232,5 kPa, Nc=9. σv,L=623,2 kPa. Rb,calc passa da 2366,56 a 3071,38 kN, incremento 704,82 kN (+29,78%). Richiede revisione ingegneristica della scelta lorda e del bilancio delle azioni. Test: 64 superati, inclusi tensione totale con falda interna e γsat esplicito, contributo di strati esclusi dalla laterale, coefficienti, fallback drenato e sottospinta separata.
