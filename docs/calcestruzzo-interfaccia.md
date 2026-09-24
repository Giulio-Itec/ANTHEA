# Calcestruzzo armato — collegamento Checker

Le [estensioni di settembre 2026](calcestruzzo-estensioni.md) aggiungono piani
di deformazione, dettagli costruttivi, curva M–χ, torsione, taglio circolare e
sezioni con foro centrale; integrano le funzionalità descritte in questa guida.

## Motore e convenzioni

L'interfaccia WPF usa la copia delle DLL in `lib/Checker`, non riferimenti ai progetti
sorgenti esterni. Versioni, provenienza e impronte sono in
[manifest.json](../lib/Checker/manifest.json). Il repository Checker e Rhino2Midas
non sono stati modificati.

**N negativo a compressione**, positivo a trazione. Azioni in kN/kNm; geometria in
mm e tensioni in MPa. L'adattatore converte nelle unità della libreria (N, Nmm).
Gli assi locali delle azioni riprendono CheckerUI: origine nel baricentro, assi
opposti agli assi geometrici x/y della preview. Sono disponibili assi principali
e personalizzati, con trasformazioni ed eccentricità calcolate dalla DLL.
I grafici dei domini sono negli assi locali; Ed nella tabella resta negli assi
scelti per l'input, Rd nei dettagli è negli assi locali.

Le resistenze non derivano più da intersezioni implementate in ANTHEA.
`SectionDomainMesh` contiene soltanto triangoli per il disegno WPF.

| Funzione | API della DLL |
| --- | --- |
| Dominio 3D | `GetFailureDomainResult`, `Domain.GetMesh` |
| Punto resistente e tasso 3D | `CalculateForce`, `CalculateWorkingRatio` |
| Meridiani 2D locali a multipli di 90° | `GetPlasticFailureDomainResult2d` / `GetElasticFailureDomainResult2d` |
| Altri tagli 2D | `CalculateDomainConstantMomentsRatio` / `CalculateDomainConstantAxialForce` |
| Punto resistente 2D | `FailureDomainResult2d.AddForce` e tasso nativo sul vettore locale |
| Analisi tensionale | `GetTensionAnalysisResult(force)` |
| Tensioni e limiti | getter e controlli SLE di `StressAnalysisResult` |

Nel 2D la selezione N–M identifica un piano passante per l'asse N negli assi locali
del dominio. Le eccentricità dell'origine possono quindi portare le azioni fuori
piano; la proiezione deve essere esplicitamente abilitata. Il tasso dell'azione
proiettata non è una verifica dell'azione completa.

È usato l'overload sincrono a singola azione per le tensioni: nei sorgenti esaminati
l'overload asincrono massivo senza argomenti presenta i rami lineare/non lineare
invertiti. L'interfaccia esegue il calcolo in background. Non c'è ripiego su un
solutore alternativo se Checker non converge. L'annullamento viene controllato
fra chiamate: non interrompe una chiamata interna già iniziata nella DLL.

## Schede e opzioni

### Rifiniture del 21 settembre 2026

- Le etichette dei domini sono **Plastico** ed **Elastico**. Gli identificativi
  interni e le famiglie Excel rimangono SLU/SLV per compatibilità degli archivi.
- Ø e passo delle staffe sono solo nel gruppo Staffe, non duplicati in Armature.
- Cambiare criterio di ricerca o strategia aggiorna le opzioni del solver nativo
  e ricalcola punti resistenti/tassi, mantenendo le stesse istanze di dominio e
  mesh visualizzata. Cambiando strategia in Intersezione, la DLL può comunque
  preparare una propria mesh ausiliaria interna; cambiare solo N costante/eccentricità
  ecc. non richiede questa preparazione. Senza altri aggiornamenti pendenti non vengono ricalcolate neppure le
  SLE o l'altra scheda dominio. La proiezione 2D conserva la curva nativa.
- Proietta sul piano è visibile prima del filtro azioni, fuori dalle opzioni avanzate.
- **Scala…** offre zoom percentuale e fattori indipendenti X/Y nel 2D,
  Mx/N/My nel 3D. Sono trasformazioni grafiche, non alterano sollecitazioni,
  resistenze o unità. Adatta ripristina i fattori unitari. Il fit 2D considera
  normalmente solo il dominio e mantiene l'origine al centro; includere le
  azioni è facoltativo. Lo zoom è continuo, anche in riduzione. Nel 3D il fit
  usa gli estremi effettivi, l'orientamento corrente e le proporzioni del viewport.
- Senza trefoli si nascondono n/φp/Ep, i coefficienti normativi dei trefoli,
  il relativo comando delle etichette e il pulsante materiale dedicato.
  Rimane disponibile + Trefolo; aggiungendone uno ricompaiono le impostazioni.
- Le opzioni di calcolo/verifica SLE sono comuni alle tre famiglie. Azioni,
  contouring ed etichette restano indipendenti. Al primo passaggio di un vecchio
  foglio si adottano le opzioni Rara e si conserva una copia delle tre configurazioni
  in `sle_precedenti_unificazione`. I dati comuni sono in `sle_comuni`.
- Accanto ai dettagli della combinazione, **Riepilogo verifiche** mostra il
  nome governante e η massimo per Plastico/Elastico, tensioni/fessurazione delle
  tre SLE e taglio Vx/Vy. Include tutte le righe, non solo quelle filtrate, e
  distingue gli esiti privi di tasso da quelli verificati.
- Il taglio propone **Automatici da sezione** per i parametri del wizard.
  Per rettangolare: bw,x = h e bw,y = b; per T: bw,x = hf e bw,y = bw dell'anima.
  d deriva dal baricentro delle barre di lembo; si usa il minore nei due versi.
  Si raggruppano le barre entro un diametro massimo dal centro più esterno,
  separatamente nelle due metà geometriche; Asl è il minimo delle aree dei
  due gruppi, non tutta l'armatura longitudinale. Sono stime geometriche esplicite,
  da verificare rispetto al meccanismo resistente reale; non usano il segno di V
  per dedurre il lembo teso. **Manuali** permette di sovrascriverle.
  Le precedenti impostazioni sono conservate in `parametri_precedenti`.
  Senza staffe l'Asl geometrica richiede conferma dell'ancoraggio efficace prima
  di produrre un esito; cambiare i parametri geometrici derivati annulla la
  conferma. Circolare/CAP e geometria generica restano fuori campo.

Riferimento per bw, d e condizione di ancoraggio di Asl:
[NTC 2018, §4.1.2.3.5](https://www.gazzettaufficiale.it/eli/gu/2018/02/20/42/so/8/sg/pdf#page=83).

### Aggiornamento automatico e scambio delle azioni

Nelle cinque schede CA non ci sono pulsanti **Calcola**. All'apertura e dopo
ogni conferma dei dati il calcolo parte automaticamente, **senza timer**.
I campi di testo e le celle si confermano al cambio di focus o al salvataggio;
durante la digitazione rimane valido lo stato precedentemente confermato.
Selettori e caselle di spunta si confermano subito. Gli input rimangono utilizzabili; una nuova modifica annulla
la pubblicazione dei risultati precedenti e accoda l'aggiornamento. Le chiamate
interne alla DLL già avviate devono terminare prima della richiesta successiva.
Gli errori di una verifica non impediscono il calcolo delle altre; vedere gli
esiti delle righe, il riepilogo e `errori_calcolo` nell'esportazione JSON.

I domini nativi sono riutilizzati quando cambiano solo le azioni. La cache è
distinta per 2D/3D e SLU/SLV e confronta geometria, materiali, trefoli e opzioni
effettive. Filtri, selezione, trasparenza e contouring sono solo visualizzazioni:
non lanciano un nuovo calcolo. Le opzioni avanzate sono raggruppate in sezioni
apribili/richiudibili; nasconderle non ne cambia i valori.

Ogni tabella delle sollecitazioni offre **Template Excel**, **Importa Excel** e
**Esporta Excel**. Senza percorso selezionato, Importa chiede il file. Template
ed Esporta memorizzano il percorso nel foglio ANTHEA: Importa rilegge quel file.
Il percorso è visibile e **Sfoglia** permette di cambiarlo. Se il file non esiste
più viene chiesto di selezionarne un altro. Esporta include tutte le famiglie e
tutte le righe, indipendentemente dai filtri; conserva i valori double senza
arrotondare i dati. I nomi sono testi Excel anche se iniziano con `=`.
Il template `.xlsx` è incluso nel programma e non richiede Excel installato.
Un solo foglio `Azioni`, con colonne Famiglia, Nome, N [kN], Mx [kNm], My [kNm],
Vx [kN], Vy [kN]. Famiglie: SLU, SLV, Rara, Frequente, Quasi permanente, Taglio.
Le righe di Taglio usano N/Vx/Vy, le altre N/Mx/My; le componenti non pertinenti
devono essere vuote o nulle. Le azioni devono essere già combinate. N rimane
negativo a compressione: non avvengono conversioni implicite di segni o unità.

Sono predisposte 500 righe vuote, senza combinazioni dimostrative; se ne possono
aggiungere fino a 10.000 per importazione (file massimo 20 MB). Scrivere zero
quando la componente è nulla: un campo obbligatorio vuoto non vale zero.
L'importazione valida tutte le righe prima di modificare i dati e chiede se
aggiungerle o sostituire le sole famiglie presenti. Le altre famiglie restano
intatte. Si leggono numeri e testi, anche con virgola decimale. Le formule non
vengono eseguite: se hanno un risultato memorizzato viene segnalato nella
conferma; occorre aver ricalcolato e salvato il file in Excel. Formule senza
risultato salvato o celle con errori bloccano l'importazione.

**Ctrl+C** copia nome e sollecitazioni delle righe selezionate, senza le colonne
dei risultati. Ctrl/Maiusc+clic permettono la selezione multipla. **Ctrl+V**
incolla dalla riga corrente: quattro colonne includono il nome, tre sono le
azioni. Una/due colonne partono dalla cella di input corrente. Le righe eccedenti
sono aggiunte; il menu contestuale offre anche **Aggiungi dagli appunti**.
In modifica di una cella, un testo singolo mantiene il normale incolla testuale.
Un incolla tabellare non valido non modifica alcuna riga.

Tutte le tabelle CA, comprese barre, trefoli e vertici CLS, hanno filtro per
colonna con Contiene, Inizia con, Uguale a e Non contiene. Ordinamento crescente
o decrescente dalla barra oppure clic sull'intestazione: confronto numerico per
valori numerici, alfabetico per testi. Non si eliminano né si escludono azioni dal
calcolo. I filtri sono locali alla vista e non vengono salvati nell'archivio.

### Visualizzazioni e riepiloghi

Nei domini 3D e 2D **Forze: tutte / selezionata** alterna le azioni visibili,
rispettando filtro e colonna Mostra. Il 3D ha uno slider di trasparenza 0–100%:
0% opaco, 100% superficie invisibile; assi, punti e reticolo restano leggibili.
Sollecitazioni, resistenze e linee di verifica hanno interruttori indipendenti.
Colori η è facoltativo: verde sotto 0,80, ambra fino a 1, rosso oltre 1, grigio
se il tasso non è determinato. Sono soglie grafiche, non ulteriori verifiche.
Il 3D ha illuminazione con normali interpolate e griglia sul piano N=0.
Il 2D mantiene l'origine al centro anche nello zoom, senza traslazione, con
assi simmetrici e passi arrotondati 1/2/5 × 10ⁿ (multipli di 10 quando la scala
lo consente). La spezzata origine–Ed–Rd identifica la verifica selezionata;
non sostituisce il criterio resistente della DLL.
Il riepilogo laterale distingue Ed negli assi di input da Rd negli assi locali,
mostra criterio, tasso, tensioni/deformazioni minime e massime, altezza utile,
posizione/inclinazione dell'asse neutro forniti da `CalculateStrainPlaneResult`.
Gli estremi della mesh/curva sono **campionati sull'intero dominio** e non vanno
confusi con le resistenze al N della combinazione. Lo stato tensionale riportato
nel dominio è quello **al punto resistente**, non all'azione Ed.

In SLE il dettaglio ha le viste **Riepilogo**, **Barre e trefoli** e **Calcestruzzo**. Riporta azioni,
modello, limiti applicabili, tassi, stato tensionale/deformativo all'azione Ed,
asse neutro, condizioni ambientali, wk, limite, Ac,eff e As,eff quando disponibili.
Il selettore contouring riprende le rappresentazioni pertinenti di CheckerUI:
gradiente/bande delle tensioni CLS, scala riferita alla resistenza del CLS,
tensioni nelle barre, tasso delle barre, tasso della sezione; aggiunge
deformazioni CLS e sola geometria. Ogni mappa ha legenda e unità. I rapporti alla
resistenza del materiale **non sono gli esiti tensionali SLE**. Il campo CLS è
campionato con getter nativi su un raster 96×96, ritagliato al contorno della
sezione e interpolato soltanto per il disegno; non interviene nel calcolo.
La palette predefinita è blu per valori negativi, bianco allo zero e rosso per
positivi. Le legende sono verticali. Gli estremi della legenda campionata possono differire dai valori
ai vertici nel riepilogo. Le resistenze usate per normalizzare le mappe sono
positive in valore assoluto; il segno delle tensioni resta quello di Checker.
Non sono aggiunti i contouring dei profili metallici interni, non presenti nel
modello geometrico attuale di ANTHEA.

Tre comandi, spenti di default, mostrano σ/ε per tutte le barre, tutti i trefoli
e tutti i vertici del CLS; per contorni con molti vertici conviene espandere e
ingrandire la vista o usare la tabella Calcestruzzo. Le deformazioni usano le API
native `GetRebarStrain` / `GetVerticeStrain`, con φ nella lineare: sono incrementali,
senza εp iniziale. Valori calcolati a due decimali; per piccoli valori non nulli
si usa notazione scientifica invece di farli apparire nulli. I risultati e gli
esiti continuano a usare la precisione completa.

Nelle opzioni comuni delle tre SLE sono modificabili sia n armature sia n trefoli (se presenti):
`n = Eacciaio × (1 + φ) / Ec`. Modificare n aggiorna φ, modificare φ aggiorna n.
Cambiare materiale mantiene φ e aggiorna n. Si usa Ec del materiale DLL.
Ep di riferimento è il primo trefolo presente, oppure il materiale trefolo
predefinito, oppure 195000 MPa se non ci sono trefoli. Con Ep differenti,
φp è comune e ogni materiale ha un n effettivo diverso. φ deve essere non negativo;
valori n incompatibili bloccano l'analisi SLE. n/φ non governano la non lineare.

Il Taglio ha gruppi separati Vx/Vy, preview e riepilogo selezionato con VRsd,
VRcd, VRd, cot θ, elemento governante e tasso per direzione. Restano espliciti
i limiti di applicabilità già documentati; la riorganizzazione grafica non
estende il modello resistente.
Ø, passo e braccia si impostano sia nel pannello di controllo sia nel Taglio,
su dati condivisi. Rettangolare/T: braccia per Vx e Vy; circolare: staffa chiusa
o spirale e ferri interni. Il disegno è **indicativo e non esecutivo**: non include
ancoraggi, piegature e sagomario. La scelta Spirale non abilita un modello a taglio
circolare non ancora validato.

### Contenuto delle schede

1. Pannello di controllo: geometria, materiali, coefficienti, armature, trefoli,
   preview e riepiloghi distinti delle verifiche, compreso il taglio.
2. Dominio 3D: SLU plastico/SLV elastico; N costante, eccentricità costante,
   Mx–My costanti, N–Mx costanti o N–My costanti; ricerca iterativa/intersezione,
   direzioni angolari, interpolazione della mesh, suddivisioni N, contributo del
   CLS teso, assi, filtri e tabella delle combinazioni.
3. Dominio 2D: N–M con direzione θ oppure Mx–My a N fissato, risoluzione,
   assi, CLS teso, filtro e proiezione esplicita delle azioni fuori piano.
4. Tensioni e fessurazione: Rara, Frequente e Quasi permanente con azioni indipendenti e opzioni comuni;
   analisi lineare/non lineare, viscosità φ per barre e trefoli nella lineare,
   CLS teso, assi; esposizione, sensibilità dell'armatura, durata, aderenza,
   copriferro e spaziatura massima delle barre tese.
5. Taglio e torsione: azioni N/Vx/Vy/T; modello con/senza staffe; bw, d, Asl ancorata,
   rami, inclinazione delle staffe, cot θ automatica o manuale per ogni asse.
6. Dettagli costruttivi: tipo di elemento, controlli capitolo 4 / EC2, ancoraggi.
7. Momento–curvatura: percorso configurabile, primo snervamento, limite e CSV.
   Ø e passo sono nei dati comuni. Vx agisce lungo x (d nella larghezza);
   Vy lungo y (d nell'altezza). Non è una verifica di interazione biassiale.

I viewport hanno zoom, adattamento, esportazione PNG e apertura ingrandita.
Il 3D è ruotabile; selezionare una riga evidenzia Ed e Rd. Le azioni SLU/SLV sono
condivise fra 2D e 3D. SLE e taglio richiedono azioni già combinate: non vengono
generati coefficienti ψ né combinazioni di carico.

I materiali predefiniti usano CLS parabola-rettangolo e acciaio elastico-perfettamente
plastico. I pulsanti + CLS / + Acciaio / + Trefoli creano materiali custom salvati
nel foglio e riapplicabili dal relativo elenco. CLS: nome, fck (12–90 MPa) e
diagramma parabola-rettangolo, bilineare, stress block o non lineare. Moduli,
deformazioni limite e resistenza a trazione derivano dalla DLL. Acciaio: Es,
fyk, fu, εu e legge elastoplastica/incrudente. Trefoli: Ep, fpyk, fpk, εpu;
l'applicazione aggiorna i materiali dei trefoli esistenti senza cambiare Ap,
posizione o σp0, e predispone i nuovi. L'acciaio è marcato
`SteelTypes.Rebar`: lasciare Undefined impediva alla DLL di costruire il dominio.
Non è stato importato l'intero editor materiali/geometrie di CheckerUI: curve
tabellari, FRC e profili metallici interni restano da implementare. Restano le
geometrie parametriche circolare, rettangolare e a T. **Generica (da definire)**
è una scelta salvabile ma blocca esplicitamente il calcolo finché non sarà
implementata la definizione di contorni, fori e armature.
I trefoli richiedono Ap, Ep, fpyk, fpk, εpu e σp0 (tensione iniziale positiva,
da fornire già coerente con le perdite considerate). Il motore li include
effettivamente; dati incompleti bloccano il calcolo.
Si controllano valori finiti, dimensioni, copriferro/disposizione, conteggi,
sovrapposizione delle armature e contenimento dell'intera area di barre/trefoli
nel CLS. Fu e deformazioni ultime devono essere coerenti con E e fy.
La DLL distingue alcune API SLE dei trefoli tramite εp: per σp0=0 l'adattatore
non pubblica un esito SLE impropriamente classificato come armatura ordinaria.
Il coefficiente SLE dei trefoli è applicato dalla DLL a **fpyk**, non a fpk:
questo è reso esplicito nell'editor e va riesaminato nella validazione normativa CAP.

## Normative disponibili e report

Il selettore collega NTC 2018, Model Code 2010, EN 1992-1-1, UNI, DIN, DS e NS
EN 1992-1-1, CNR-DT 204/2006 e CS-TR34. Tutti i coefficienti esposti dalla classe
base sono visibili e modificabili; la scelta di una nuova normativa ripristina
i suoi valori predefiniti. I tre input storici αcc/γc/γs sono sincronizzati.
La cache dei domini include normativa, coefficienti e materiali. Taglio e
fessurazione portati da Rhino2Midas restano abilitati **solo per NTC 2018**.
Per supporto effettivo, differenze nazionali e parti mancanti vedere
[normative-calcestruzzo.md](normative-calcestruzzo.md).

Dal menu **Report Word** si selezionano geometria, materiali, coefficienti,
azioni, domini, ogni SLE, taglio, dettagli di barre/vertici e grafici. Le scelte
si salvano nel foglio. Ambito, limiti ed errori sono sempre inclusi. La struttura
è Input (geometria, armature, staffe, trefoli), Materiali (CLS, acciaio, trefoli),
Coefficienti, Sollecitazioni e Verifiche. I grafici sono nelle rispettive sezioni.
Per ciascuna famiglia SLE, di default si stampano gli estremi algebrici delle
tensioni/deformazioni con la combinazione di origine, il caso con ησ massimo e
quello con ηw massimo, distinti. Gli estremi non sono uno stato simultaneo.
Gli esiti senza tasso numerico (non applicabilità, decompressione, errori) restano
espliciti: non si inventa un caso governante. L'opzione `sle_tutte` abilita tutte
le combinazioni. I dettagli facoltativi riportano i casi da cui provengono gli
estremi e i governanti, oppure tutti i casi in modalità completa. Le immagini SLE
sono riferite ai governanti effettivi, indipendentemente dalla riga selezionata;
i domini rappresentano le opzioni correnti (3D in isometria).
Un aggiornamento in corso o dati globalmente invalidi bloccano
l'export. Il DOCX non ricalcola e non dichiara una conformità normativa globale.

La finestra CA ha scorrimento orizzontale e verticale quando lo spazio è inferiore
alla superficie minima di lavoro; i pannelli mantengono i propri scroll interni.
La rotella su un menu chiuso scorre il pannello senza cambiare la selezione.
I menu contour e filtri leggono la voce selezionata, non il testo ancora in
aggiornamento. I numeri nelle griglie sono centrati, anche durante l'editing.
Lo spessore disegnato delle staffe segue Ø × scala senza limite massimo in pixel;
asse della staffa a copriferro + Ø/2. Resta uno schema, non un disegno esecutivo
di piegatura, ancoraggio o sviluppo della spirale.

## Integrazione Rhino2Midas e correzioni autorizzate

Provenienza: `Fem2Rhino.Grasshopper.Reading/ConcreteChecker/Components`,
componenti ServiceabilityStressCheck, ServiceabilityCrackWidthControlCheck,
RectangularSectionShearCheck e RectangularSectionShearCheckNoStirrup.
Il porting è isolato in `X.Core/Ntc2018Checks.cs`; non importa Grasshopper/Rhino.

### NTC 2018

Riferimento: [capitolo 4 ufficiale](https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=18A00716&art.dataPubblicazioneGazzetta=2018-02-20&art.num=1&art.tiposerie=SG&cdimg=18A0071600100010110005&dgu=2018-02-20),
§§4.1.2.2.4–5 e 4.1.2.3.5.

- Taglio senza staffe: eliminato |N|; la trazione non incrementa la resistenza.
  In trazione non si emette un esito automatico. Compressione limitata come
  previsto nella formula; si confrontano formula principale e minimo.
- Con staffe: resistenza minima fra tirante e puntone; cot θ limitata a 1–2,5.
  Corretti l'incrocio delle inclinazioni x/y e la radice negativa nell'ottimizzazione.
- Rara: limiti CLS e acciaio dalla libreria; QP: limite CLS; frequente: solo
  tensioni, senza inventare un limite tensionale.
- L'opzione comune per elementi piani gettati in opera sotto 50 mm riduce fcd
  e i limiti tensionali del CLS; vale anche nel modello a taglio.
- Il limite di compressione viene applicato alle sole tensioni negative del CLS.
- Decompressione e formazione delle fessure sono controlli sulla sezione
  omogeneizzata integra, distinti dall'apertura: nessuna divisione per limite zero.
- La tabella di esposizione distingue formazione e decompressione per le armature
  sensibili in ambiente molto aggressivo.

### Circolare 2019

Riferimento: [capitolo C4 ufficiale](https://www.gazzettaufficiale.it/do/atto/serie_generale/caricaPdf?art.codiceRedazionale=19A00855&art.dataPubblicazioneGazzetta=2019-02-11&art.num=1&art.tiposerie=SG&cdimg=19A0085500100010110005&dgu=2019-02-11),
§C4.1.2.2.4, formule C4.1.6–10.

- Corretti i coefficienti di durata: kt = 0,6 breve; kt = 0,4 lunga.
- L'area efficace viene ottenuta tagliando la mesh della sezione con gli strumenti
  geometrici GPC; As,eff e diametro equivalente comprendono solo le barre tese
  nella fascia efficace, non tutta l'armatura.
- La spaziatura massima è esplicita, senza dedurla dal vicino più prossimo.
  Nella zona lontana dalle barre non si sceglie indiscriminatamente il minore dei
  due modelli di distanza: viene mantenuto il caso più gravoso delle zone applicabili.
  Per C4.1.10 si usa esattamente Δsm = 0,75(h−x), anziché 1,3/1,7(h−x).
- Gestiti area/armatura efficace assente e numeri non finiti.
  La sezione interamente tesa richiede un trattamento separato delle aree efficaci
  e non riceve un risultato automatico.

## Limiti ancora espliciti

Questi controlli non costituiscono una validazione integrale del software né una
certificazione della struttura. L'esportazione mantiene
`verifica_normativa_completa: false`.

- Taglio circolare: richiede la scelta esplicita del modello e dei parametri,
  descritta nelle [estensioni del modulo](calcestruzzo-estensioni.md).
- Taglio con precompressione: da completare, incluse le componenti dei cavi.
- Apertura delle fessure con trefoli: da completare, incluse aderenza ed area
  efficace specifica. Decompressione/formazione hanno un controllo distinto.
- Apertura wk: disponibile per analisi lineare fessurata, incluse le sezioni interamente tese;
  la non lineare calcola le tensioni ma non produce automaticamente wk.
- Dettagli, ancoraggi, momento–curvatura, torsione e interazione V–T hanno
  controlli dedicati nelle nuove schede; valgono le ipotesi documentate nelle
  estensioni. Gerarchia sismica e secondo ordine non sono verificati.
- SLV elastico è il dominio elastico della libreria, non l'intera verifica sismica.
- Per geometrie, esposizioni, materiali e carichi reali occorre una verifica
  indipendente del progettista. Le scelte fuori campo sono segnalate nella tabella.
- Normative e coefficienti nazionali incompleti non vengono presentati come
  implementazioni normative complete; vedere la matrice dedicata.

## Archivi e compatibilità

Il formato generale degli archivi resta versione 1; `versione_sezione` resta 2.
`workspace_ca.versione` passa a 2. In apertura, i workspace v1/privi di versione
usavano N positivo a compressione: N viene invertito una sola volta per input,
combinazioni e piano 2D; Mx/My restano invariati. Convenzione e migrazione sono
registrate nel workspace. Le vecchie SLE restano Rara, senza duplicazione.

Opzioni, trefoli e azioni a taglio si salvano con il foglio. I risultati non sono
riutilizzati come validi dopo la riapertura; le modifiche invalidano i risultati
interessati. JSON di esportazione: DLL utilizzata, norma, dati, risultati per ID
ed esiti mancanti/parziali.

Il vecchio motore `SezioneCA` resta per geometria e per i confronti storici,
oltre che nel modulo distinto del palo orizzontale; non calcola le resistenze
delle nuove schede CA. Il comando diagnostico storico `X.Verifiche --calcola`
per `str_palo` non va usato per i nuovi workspace Checker: usare l'esportazione
del foglio WPF. I confronti storici non dimostrano l'identità fra i due motori.

## Prove riproducibili

```powershell
dotnet build ANTHEA.sln -c Release
dotnet X.Verifiche/bin/Release/net8.0/ANTHEA.Verifiche.dll --checker
dotnet X.Verifiche/bin/Release/net8.0/ANTHEA.Verifiche.dll casi_confronto.json verifiche_checker/confronto.json
dotnet X.Desktop/bin/Release/net8.0-windows/ANTHEA.dll --smoke verifiche_checker/wpf casi_confronto.json
```

I controlli dedicati confrontano punti/tassi con le API native, tutti i percorsi,
SLU/SLV, lineare/non lineare, meridiani 0/90/35°, proiezioni, cambi di riferimento,
trefoli, migrazione e cancellazione. Il benchmark VCA_N_1 riprende i test Checker:
rettangolo 300×500, quattro Ø18 a 50 mm, n=15; scarto ammesso 5% sui valori
pubblicati arrotondati, non una tolleranza generale di progetto.
Sono presenti risultati analitici indipendenti per taglio, apertura delle
fessure e area efficace rettangolare. La prova WPF verifica tabelle, mappe,
invalidazione, filtri, salvataggi e viewport. Vedere anche
[esito dell'integrazione](checker-verifica.md).
