# Estensioni del modulo CA · settembre 2026

La validazione precedente è assunta come riferimento, secondo l'indicazione del
progettista. I test qui descritti verificano le estensioni e le regressioni del
software; non modificano gli esempi della relazione di validazione.

## Uso delle sette schede

1. **Pannello di controllo**: dati comuni di geometria, materiali, armature e
   staffe. Rettangolare e circolare possono avere un foro centrale della stessa
   forma. La circolare usa **32 lati per contorno** come default, configurabili
   in **Geometria → Lati del contorno**: multipli di 4 fra 12 e 720, uguali per
   contorno esterno e foro. I multipli di 4 mantengono vertici sui due assi e
   quindi il diametro assegnato in entrambe le direzioni. Il valore viene
   salvato nel foglio, indicato nelle proprietà e nel report; i fogli privi del
   parametro adottano 32. La modifica ricostruisce la geometria del verificatore.
   Questo parametro è distinto dalle direzioni angolari del dominio.
   Le dimensioni interne devono essere inferiori alle esterne. Il motore
   esclude il foro da area, inerzie, mesh e integrazione; impedisce barre nel
   vuoto o che ne intersecano il bordo. L'anteprima mostra foro e quote interne.
2. **Dominio 3D** e **Dominio 2D**: nella vista **Sezione σ / ε** si sceglie
   **Azione inserita** oppure **Punto limite**. Sono disponibili mappa, piano
   delle deformazioni e valori copiabili per barre e vertici. Il punto limite
   usa il piano già ottenuto dalla ricerca di resistenza; non viene cercato di
   nuovo. L'equilibrio di Ed è calcolato al primo accesso e memorizzato per la
   combinazione corrente. Il piano usa coordinate geometriche, ε positivo a
   trazione, coefficienti in 1/mm e quote ε in ‰. Le mappe stanno nella finestra;
   il comando **Espandi** rimane disponibile.
3. **Tensioni e fessurazione**: il selettore delle zone efficaci evidenzia area e
   barre considerate. I passaggi indicano Ac,eff, As,eff, ρeff, Øeq, copriferro,
   interasse, coefficienti, distanza tra fessure, differenza di deformazione e
   apertura. Il ramo interamente teso valuta separatamente le quattro facce
   rettangolari; per la circolare valuta fasce radiali nelle direzioni delle
   barre e del gradiente. Governa l'apertura massima, senza sommare aree di facce
   diverse. k2 = (εmax + εmin)/(2 εmax). Restano esclusi wk da analisi non lineare
   e l'aderenza specifica dei trefoli. Nelle sezioni cave l'area del foro viene
   sottratta dalle fasce efficaci; il modello delle fasce è riferito al contorno
   esterno, non costituisce un controllo autonomo della superficie interna.
   Per questo caso l'esito resta da completare anche quando wk esterno è entro
   limite; un superamento esterno viene comunque segnalato come non soddisfatto.
4. **Taglio e torsione**: Vx, Vy e T nella stessa combinazione; T in kNm.
   Il valore di default dei vecchi archivi è zero. La colonna T è disponibile
   anche nello scambio Excel, che continua ad accettare i vecchi file a sette
   colonne. As,l per torsione è la quota disponibile dopo pressoflessione, da
   assegnare esplicitamente; non si riutilizza automaticamente tutta l'armatura.
5. **Dettagli costruttivi**: scegliere Trave, Pilastro, Soletta piena o Parete.
   I dati comuni restano condivisi: non vengono copiate staffe o geometria.
   Una soletta può avere **Staffe presenti = No**: la geometria delle barre e
   il modello a taglio tengono conto della scelta. Ogni controllo riporta
   valore, limite, formula/riferimento ed esito; i dati non ricavabili dalla
   sezione producono **Da completare**.
6. **Momento–curvatura**: N costante, direzione del momento, numero di passi,
   campionamento uniforme/quadratico, frazione finale di MRd, trazione del CLS,
   discretizzazione del dominio, residuo N ammesso al limite e raffinamento del
   primo snervamento. Usa i materiali comuni. Avvio e interruzione sono espliciti;
   le modifiche invalidano il risultato. Curva e punti sono esportabili in CSV,
   JSON e report. Il ramo è a momento crescente fino al limite plastico: nessun
   ramo discendente. La curvatura è il modulo del gradiente; nelle sezioni
   asimmetriche la sua direzione può differire da quella del momento.

## Schematizzazione resistente

Il taglio circolare richiede una scelta esplicita: **Pile · NTC §7.9.5.2**
(z/d = 0,75 per piena e 0,60 per cava) oppure **Parametri assegnati**. Il modello
delle pile è un'opzione del capitolo 7, non una regola generale del capitolo 4.
bw e d si possono assegnare scegliendo **Parametri geometrici = Manuali**; z/d è
modificabile nel modello assegnato. I suggerimenti geometrici sono bw = D nella
piena e D−Di nella cava, d dal baricentro delle barre dei due semicerchi,
assumendo il valore minore. I rami resistenti a Vx e Vy sono espliciti.

La torsione usa la sezione tubolare equivalente con Ak, uk e t esposti nel
dettaglio. NTC §4.1.2.3.6: TRd è il minimo delle resistenze del puntone, delle
staffe (area di un ramo) e dell'armatura longitudinale. Sono richieste staffe
chiuse a 90°, conferma della disposizione periferica e cot θ comune al taglio.
Per T+V si controllano puntone e armature. La somma
|T|/TRcd + |Vx|/VRcd,x + |Vy|/VRcd,y è un'estensione conservativa esplicita del
controllo monoassiale, non una formula normativa biassiale autonoma. Profili a T,
generici, spirali e precompressione non vengono assimilati automaticamente a
questo modello di torsione.

## Dettagli costruttivi e ancoraggio

Sono disponibili interferro e copriferro, minimi/massimi longitudinali,
diametri e passi di staffatura, armatura secondaria e interassi per solette e
pareti. Si verificano anche le distanze geometriche delle barre dal foro.
cmin,dur è assegnato dal progettista e predisposto per il collegamento alla
durabilità del modulo Materiali; vale per tutte le superfici della sezione.
Per trave/soletta i controlli delle due zone longitudinali usano le due metà
della sezione, rendendo esplicita una possibile inversione di momento.

I controlli locali degli appoggi, la traslazione delle trazioni, le legature,
le distribuzioni fra facce e il punzonamento richiedono informazioni
dell'elemento: sono indicati quando non determinabili dai dati di sezione.
Il modulo non dichiara una conformità globale se tali verifiche sono pendenti.
La gerarchia sismica del capitolo 7 non fa parte di questi dettagli.

Ancoraggi e sovrapposizioni rettilinee hanno dati separati in `ancoraggi`,
con `schema_versione = 1`. Il servizio espone fbd, lb,rqd, lunghezza richiesta
e confronto con la disponibile. Sono assunti α1…α5 = 1, senza riduzioni
favorevoli per confinamento o piegature; per le sovrapposizioni si considera
α6 e l'interferro. I dettagli esecutivi e le cautele per Ø > 32 mm restano da
verificare. Il medesimo contratto può essere richiamato da una futura scheda
dedicata, senza duplicare la formula.

## Separazione del calcolo e prove

`X.Core` contiene i contratti `IConcreteDetailingCalculator`,
`IConcreteAnchorageCalculator`, `IConcreteTorsionCalculator` e
`IMomentCurvatureCalculator`, i rispettivi input/output e l'integrazione delle
zone efficaci. Non dipendono da WPF. L'adattatore `CheckerSection` resta l'unico
collegamento all'equilibrio e ai domini della DLL; la curva riceve le funzioni
di equilibrio/resistenza. Questi confini preparano il trasferimento nella
libreria di calcolo futura. Le DLL distribuite in `lib/Checker` non sono
modificate da questa estensione.

```powershell
dotnet run --project X.Verifiche -c Release -- --ca-module
dotnet run --project X.Verifiche -c Release -- --checker
X.Desktop/bin/Release/net8.0-windows/ANTHEA.exe --smoke-ca-extensions tmp/ca_extensions/ui
X.Desktop/bin/Release/net8.0-windows/ANTHEA.exe --smoke-ca-features tmp/ca_extensions/features
dotnet run --project X.Verifiche -c Release -- --ca-benchmark tmp/ca_extensions/benchmark
```

Il benchmark conserva input, opzioni, versioni e SHA-256 delle DLL, runtime,
tempi e allocazioni per rettangolare/circolare piena/cava. Misura preparazione,
dominio 3D, 24 verifiche, tensioni lineari/non lineari, fessurazione e curva.
Run 0 è il primo uso del caso; le tre ripetizioni successive hanno mediana,
minimo e massimo separati. Confrontare le stesse condizioni della macchina e
non eseguirlo contemporaneamente ad altri calcoli. I checksum servono a
individuare cambiamenti numerici, non sostituiscono la verifica dei risultati.

## Riferimenti

- [NTC 2018, testo ufficiale](https://www.gazzettaufficiale.it/eli/gu/2018/02/20/42/so/8/sg/pdf):
  §§4.1.2.3.5–6, 4.1.2.3.10, 4.1.6.1 e, per il modello circolare scelto, §7.9.5.2.
- Circolare 21 gennaio 2019 n. 7, §C4.1.2.2.4.5: fessurazione.
- [JRC, Eurocode 2 worked examples](https://eurocodes.jrc.ec.europa.eu/doc/1110_WS_EC2/report/1110_WS_EC2.pdf):
  integrazioni EC2 per copriferro, aderenza, ancoraggi, solette e pareti.

Restano rimandati il progetto automatico delle armature, i contorni poligonali
generici e la fessurazione non lineare, come richiesto.

## Esito dei controlli di questa estensione

- 52 controlli mirati del nuovo modulo: piani limite plastico/elastico,
  equilibrio della curva e primo snervamento, geometria cava, copriferro,
  fessurazione tesa, ancoraggi, torsione, scambio Excel e discretizzazione
  circolare configurabile (proprietà confrontate con il poligono analitico).
- 294 controlli precedenti del motore e dello scambio dati.
- 19 controlli integrati WPF delle estensioni (inclusi modello circolare,
  riapertura ed esportazioni) e 137 controlli WPF dei flussi precedenti.
- Schermate controllate: mappe Ed/Rd, piano ε, curve, dettagli costruttivi,
  geometrie cave, torsione e zone efficaci.

Il primo benchmark, eseguito con il precedente contorno circolare a 180 lati,
è in `tmp/ca_extensions/benchmark_baseline/benchmark.json`
e `.csv`. Mediane in millisecondi, tre ripetizioni dopo il primo uso:

| Caso | Preparazione | Dominio 3D | 24 verifiche | Curva 30 passi |
| --- | ---: | ---: | ---: | ---: |
| Rettangolare | 46,50 | 34,58 | 19,60 | 77,17 |
| Rettangolare cava | 44,22 | 29,91 | 15,46 | 73,48 |
| Circolare | 1144,08 | 167,79 | 205,15 | 665,14 |
| Circolare cava | 5705,77 | 717,49 | 923,39 | 2895,73 |

Questi tempi descrivono i casi registrati su questa macchina, non prestazioni
garantite per sezioni diverse. Le prime misure indicano la preparazione della
circolare cava come fase prioritaria da confrontare dopo l'aggiornamento DLL.

## Dettagli per argomento e resistenze rapide

I dettagli costruttivi sono disposti in fasce orizzontali: interferro/copriferro, armatura, staffe, ancoraggi/appoggi. Ogni fascia affianca dati e verifiche; copriferro e staffe condividono i dati con le altre schede.

Nel pannello di controllo, sopra il riepilogo delle verifiche, il riquadro delle resistenze accetta N (compressione negativa) e la scelta elastico/plastico. Calcola i quattro momenti con segno Mx+, Mx−, My+, My− negli assi locali, mediante ricerche iterative dirette a N costante. Non genera un dominio; N o direzioni non risolti restano senza valore e riportano il motivo. La modifica di questi input non rigenera i domini della sezione.

Le linee di verifica 3D conservano le componenti fissate dal criterio: origine (N,0,0) a N costante; (0,0,0) a eccentricità costante; (N,Mx,0), (N,0,My), (0,Mx,My) per gli altri vincoli. Il tratto dall’origine all’azione è visibile anche sopra la superficie del dominio.

Correzione ancoraggi e durabilità (24 settembre 2026): cmin,dur ora deriva dall'esposizione SLE condivisa usando Materiali.NtcCover e MinimumConcrete, con vita utile 50/100 anni e controllo qualità espliciti. Esposizione mancante lascia pendente il solo copriferro. Ancoraggio rettilineo indipendente dai dati di giunzione; lunghezza vuota non produce un esito. Sovrapposizione con esiti distinti per lunghezza e interferro; coefficienti mostrati, fctk limitato a C60/75 per l'aderenza. Conservato il minimo NTC §4.1.2.3.10 (20Ø, 150 mm). Zona di giunzione riferita ai limiti di armatura della sezione, da modellare con tutte le barre sovrapposte. Dettaglio esecutivo sostituito da riscontri manuali dichiarati (confinamento, posizione/sfalsamento, cautele Ø>32), distinti dal calcolo numerico. Verificati 78 controlli del modulo e 82 controlli interfaccia; app pubblicata in app/.

Legami e allineamento delle schede: diagrammi CLS e acciaio selezionabili anche per classi predefinite, mantenendo nome e proprietà della classe. Il cambio di classe/normativa conserva il legame scelto; i materiali personalizzati applicano il proprio legame. Trefoli: legame predefinito modificabile per nuovi cavi e menu nella riga per i cavi esistenti. Divisori di ingresso, risultati e righe sincronizzati per gruppo e salvati nel foglio; rapporto iniziale verticale 30/70, risultati 60/40, righe 3,7/2. Selettore Rara/Frequente/Quasi permanente nella colonna dati, per allineare l'origine della vista SLE alle altre schede. Pubblicazione Release riuscita, 93 controlli interfaccia superati, comprese selezione legami nativi, conservazione classe, selettore SLE e materiale trefoli.
