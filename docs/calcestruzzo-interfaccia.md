# Calcestruzzo armato — collegamento Checker

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

### Aggiornamento automatico e scambio delle azioni

Nelle cinque schede CA non ci sono pulsanti **Calcola**. All'apertura e dopo
ogni modifica dei dati il calcolo parte automaticamente, dopo 600 ms di pausa
nella digitazione. Gli input rimangono utilizzabili; una nuova modifica annulla
la pubblicazione dei risultati precedenti e accoda l'aggiornamento. Le chiamate
interne alla DLL già avviate devono terminare prima della richiesta successiva.
Gli errori di una verifica non impediscono il calcolo delle altre; vedere gli
esiti delle righe, il riepilogo e `errori_calcolo` nell'esportazione JSON.

I domini nativi sono riutilizzati quando cambiano solo le azioni. La cache è
distinta per 2D/3D e SLU/SLV e confronta geometria, materiali, trefoli e opzioni
effettive. Filtri, selezione, trasparenza e contouring sono solo visualizzazioni:
non lanciano un nuovo calcolo. Le opzioni avanzate sono raggruppate in sezioni
apribili/richiudibili; nasconderle non ne cambia i valori.

Ogni tabella delle sollecitazioni offre **Template Excel** e **Importa Excel**.
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

### Visualizzazioni e riepiloghi

Nei domini 3D e 2D **Forze: tutte / selezionata** alterna le azioni visibili,
rispettando filtro e colonna Mostra. Il 3D ha uno slider di trasparenza 0–100%:
0% opaco, 100% superficie invisibile; assi, punti e reticolo restano leggibili.
Il riepilogo laterale distingue Ed negli assi di input da Rd negli assi locali,
mostra criterio, tasso, tensioni/deformazioni minime e massime, altezza utile,
posizione/inclinazione dell'asse neutro forniti da `CalculateStrainPlaneResult`.
Gli estremi della mesh/curva sono **campionati sull'intero dominio** e non vanno
confusi con le resistenze al N della combinazione. Lo stato tensionale riportato
nel dominio è quello **al punto resistente**, non all'azione Ed.

In SLE il dettaglio ha le viste **Riepilogo** e **Barre e trefoli**. Riporta azioni,
modello, limiti applicabili, tassi, stato tensionale/deformativo all'azione Ed,
asse neutro, condizioni ambientali, wk, limite, Ac,eff e As,eff quando disponibili.
Il selettore contouring riprende le rappresentazioni pertinenti di CheckerUI:
gradiente/bande delle tensioni CLS, scala riferita alla resistenza del CLS,
tensioni nelle barre, tasso delle barre, tasso della sezione; aggiunge
deformazioni CLS e sola geometria. Ogni mappa ha legenda e unità. I rapporti alla
resistenza del materiale **non sono gli esiti tensionali SLE**. Il campo CLS è
campionato sui punti della preview e i suoi estremi possono differire dai valori
ai vertici nel riepilogo. Le resistenze usate per normalizzare le mappe sono
positive in valore assoluto; il segno delle tensioni resta quello di Checker.
Non sono aggiunti i contouring dei profili metallici interni, non presenti nel
modello geometrico attuale di ANTHEA.

Il Taglio ha gruppi separati Vx/Vy, preview e riepilogo selezionato con VRsd,
VRcd, VRd, cot θ, elemento governante e tasso per direzione. Restano espliciti
i limiti di applicabilità già documentati; la riorganizzazione grafica non
estende il modello resistente.

### Contenuto delle schede

1. Pannello di controllo: geometria, materiali, coefficienti, armature, trefoli,
   preview e riepiloghi distinti delle verifiche, compreso il taglio.
2. Dominio 3D: SLU plastico/SLV elastico; N costante, eccentricità costante,
   Mx–My costanti, N–Mx costanti o N–My costanti; ricerca iterativa/intersezione,
   direzioni angolari, interpolazione della mesh, suddivisioni N, contributo del
   CLS teso, assi, filtri e tabella delle combinazioni.
3. Dominio 2D: N–M con direzione θ oppure Mx–My a N fissato, risoluzione,
   assi, CLS teso, filtro e proiezione esplicita delle azioni fuori piano.
4. Tensioni e fessurazione: Rara, Frequente e Quasi permanente indipendenti;
   analisi lineare/non lineare, viscosità φ per barre e trefoli nella lineare,
   CLS teso, assi; esposizione, sensibilità dell'armatura, durata, aderenza,
   copriferro e spaziatura massima delle barre tese.
5. Taglio: azioni N/Vx/Vy; modello con/senza staffe; bw, d, Asl ancorata,
   rami, inclinazione delle staffe, cot θ automatica o manuale per ogni asse.
   Ø e passo sono nei dati comuni. Vx agisce lungo x (d nella larghezza);
   Vy lungo y (d nell'altezza). Non è una verifica di interazione biassiale.

I viewport hanno zoom, adattamento, esportazione PNG e apertura ingrandita.
Il 3D è ruotabile; selezionare una riga evidenzia Ed e Rd. Le azioni SLU/SLV sono
condivise fra 2D e 3D. SLE e taglio richiedono azioni già combinate: non vengono
generati coefficienti ψ né combinazioni di carico.

Il materiale ordinario usa CLS parabola-rettangolo e acciaio elastico-perfettamente
plastico, con proprietà e coefficienti comuni espliciti. L'acciaio è marcato
`SteelTypes.Rebar`: lasciare Undefined impediva alla DLL di costruire il dominio.
Non è stato importato l'intero editor materiali/geometrie di CheckerUI:
restano le geometrie parametriche circolare, rettangolare e a T di ANTHEA.
I trefoli richiedono Ap, Ep, fpyk, fpk, εpu e σp0 (tensione iniziale positiva,
da fornire già coerente con le perdite considerate). Il motore li include
effettivamente; dati incompleti bloccano il calcolo.

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

- Taglio circolare: il fattore 0,75 della vecchia routine non è stato adottato
  senza una schematizzazione resistente dedicata.
- Taglio con precompressione: da completare, incluse le componenti dei cavi.
- Apertura delle fessure con trefoli: da completare, incluse aderenza ed area
  efficace specifica. Decompressione/formazione hanno un controllo distinto.
- Apertura wk: disponibile per analisi lineare fessurata di sezioni parzializzate;
  la non lineare calcola le tensioni ma non produce automaticamente wk.
- Nessun esito globale per dettagli costruttivi, ancoraggi, duttilità, torsione,
  interazione del taglio nelle due direzioni, gerarchia sismica, secondo ordine.
- SLV elastico è il dominio elastico della libreria, non l'intera verifica sismica.
- Per geometrie, esposizioni, materiali e carichi reali occorre una verifica
  indipendente del progettista. Le scelte fuori campo sono segnalate nella tabella.
- Il selettore operativo è NTC 2018; un archivio con altra normativa richiede
  una scelta esplicita prima del calcolo, senza rietichettatura silenziosa.

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
