# Palo orizzontale in terreno stratificato

Sviluppo dell'approccio PileChecker - revisione 1, 30 settembre 2026.

## Scopo e scelta del metodo

Il metodo **Stratificato (PileChecker)** conserva l'impostazione del codice dell'utente: costruzione del diagramma per strato, ricerca della profondità a taglio nullo e confronto fra palo corto, intermedio e lungo. Le capacità dei singoli strati sono sostituite da un unico equilibrio del palo, con un solo momento resistente My. Il calcolo ammette anche alternanze fra terreni coesivi non drenati e granulari drenati.

In ANTHEA: **Palo orizzontale > Dati generali > Opzioni avanzate > Metodo di calcolo > Stratificato (PileChecker)**. Lo stesso motore è disponibile per il micropalo orizzontale CHS. La scelta è conservata nel file; i file precedenti continuano a usare Broms.

Il metodo resta sperimentale. Le prove verificano equazioni, casi analitici e proprietà numeriche; non costituiscono una validazione sperimentale su pali in terreni reali. La chiusura delle reazioni nella parte inferiore del palo è un'ipotesi esplicita del modello.

| Metodo | Terreni | Chiusura inferiore |
| --- | --- | --- |
| Broms | Coesivo oppure granulare; strati della stessa famiglia | Coppia distribuita nel coesivo; risultante concentrata nel granulare |
| Stratificato (PileChecker) | Coesivo, granulare o misto | Reazioni distribuite limitate dalla resistenza locale in tutti i terreni |

Nel coesivo omogeneo si recuperano i risultati di Broms implementati in ANTHEA. Nel granulare lungo si recupera il ramo superiore di Broms quando il palo è sufficientemente lungo per chiudere l'equilibrio distribuito. Nel granulare corto e intermedio la diversa ipotesi al piede produce capacità diverse: non si impone una coincidenza artificiale con la soluzione a forza concentrata.

## Parametri e unità

L e D sono lunghezza infissa e diametro in m; z è misurata verso il basso dal piano campagna; e è l'altezza della forza H sopra tale piano. H è in kN, My in kNm. N è positiva a compressione e rimane costante durante l'incremento di H. My può essere assegnato oppure ricavato dal motore della sezione c.a. o CHS.

Ogni strato conserva spessore, tipo, Cu, angolo di attrito e pesi di volume. Nei profili misti occorrono i pesi anche degli strati coesivi: contribuiscono alla tensione efficace nei granulari sottostanti. La stratigrafia deve coprire tutta L; l'ultimo strato viene troncato al piede. Nessuna interfaccia viene traslata dalla zona superficiale 1,5D.

<!-- pagebreak -->

## Diagramma limite e integrali

p_lim è una **forza per unità di lunghezza**, espressa in kN/m. Non è una pressione in kPa. Per ogni tratto coesivo:

```
p_lim(z) = 0                  se z < 1,5 D
p_lim(z) = 9 Cu_i D           se z >= 1,5 D
```

Il taglio superficiale è globale e si applica soltanto ai tratti coesivi. Un nuovo strato coesivo profondo non genera una nuova zona nulla. Per ogni tratto granulare:

```
Kp_i = (1 + sin(phi_i)) / (1 - sin(phi_i))
p_lim(z) = 3 Kp_i D sigma'_v(z)
sigma'_v(z) = integrale da 0 a z di gamma_eff(s) ds
```

Si usa gamma sopra falda e gamma_sat - 9,81 sotto falda. La tensione efficace è continua alle interfacce; p_lim può saltare quando cambiano tipo di terreno, Cu o Kp. Gli intervalli sono spezzati a interfacce, falda, 1,5D e piede.

Definendo Q(z) = integrale di p_lim da 0 a z e S(z) = integrale di s p_lim(s) da 0 a z, le integrazioni sono analitiche. In un segmento che inizia a quota a, con p_lim = p0 + k x e x = z - a:

```
DeltaQ = p0 x + k x^2 / 2
DeltaS = a DeltaQ + p0 x^2 / 2 + k x^3 / 3
```

La profondità z_f soddisfa **Q(z_f) = H**. L'inversione si esegue sul tratto che contiene la risultante richiesta, saltando gli intervalli ad area nulla. Per k diverso da zero si usa x = 2 DeltaQ / [p0 + sqrt(p0^2 + 2 k DeltaQ)], evitando la sottrazione fra numeri quasi uguali.

## Coppia inferiore

Fra z_f e la fine delle reazioni t, si assume +p_lim fino a b e -p_lim dopo b. L'equilibrio delle forze determina Q(b) = [Q(z_f) + Q(t)] / 2. Il momento della coppia resistente è:

```
C(z_f,t) = S(t) + S(z_f) - 2 S(b)
```

Questa costruzione usa le proprietà effettive di tutti gli strati attraversati. L'estensione della chiusura distribuita ai granulari e ai profili misti è una scelta di questa implementazione, aggiunta all'approccio originario PileChecker. Non è attribuita alle formule originali di Broms.

<!-- pagebreak -->

## Meccanismi e diagrammi

| Vincolo e meccanismo | Equazione candidata |
| --- | --- |
| Libera, corto | H e + S(z_f) = C(z_f,L) |
| Impedita, corto | H = Q(L); M0 = -S(L); limite S(L) <= My |
| Impedita, intermedio | -My + S(z_f) = C(z_f,L); M0 = -My |
| Libera, lungo | H e + S(z_f) = My; M0 = H e |
| Impedita, lungo | S(z_f) = 2 My; M0 = -My |

La testa impedita richiede e = 0. Nel lungo si cerca t entro L tale che C(z_f,t) = M0 + S(z_f). La capacità è il minimo dei candidati attivabili. My viene utilizzato come limite della stessa sezione del palo, non sommato per strato.

Indicando con p la reazione con il suo segno, i diagrammi sono ottenuti dall'equilibrio:

```
V(z) = H - integrale da 0 a z di p(s) ds
M(z) = M0 + H z - integrale da 0 a z di (z-s) p(s) ds
```

Il programma controlla equilibrio finale di forze e momenti, profondità delle reazioni entro il palo e valore massimo assoluto del momento entro My. Le interfacce ordinarie possono produrre salti di p, mentre V e M restano continui. I risultati contengono entrambi i lati delle discontinuità e il diagramma limite per strato.

Il passo modifica soltanto il campionamento grafico. La ricerca delle radici usa al massimo 100 bisezioni; la tolleranza effettiva sull'intervallo è min(tolleranza richiesta, 1e-12) per max(1, ampiezza iniziale). Questo affinamento mantiene i residui entro i controlli di equilibrio anche quando si richiede una tolleranza più larga.

Come nel modulo precedente, la vista grafica dei pali lunghi si ferma alla cerniera interna. Il completamento inferiore idealizzato resta nei dati numerici e nel CSV: la mancata rappresentazione non significa assenza di reazioni.

## Limiti applicativi

Cu è usata in condizioni non drenate nei coesivi; nei granulari si assume comportamento drenato con c' = 0. L'applicabilità contemporanea di queste condizioni va valutata per il problema. Sono esclusi strati disattivati, falda sopra il piano campagna, momento applicato indipendente da H, spostamenti, ciclicità, secondo ordine e verifica della duttilità. My è costante lungo il palo e uguale nei due versi.

La capacità Hu è distinta dalla resistenza di progetto: restano le riduzioni del modulo esistente Rk = min(media(Hu)/xi3, min(Hu)/xi4) e Rd = eta Rk / 1,3. Il nuovo modello non introduce ulteriori fattori e non completa automaticamente le verifiche normative.

<!-- pagebreak -->

## Verifiche riproducibili

I nuovi test si trovano in `supporto/test/X.Verifiche/HorizontalStratifiedChecks.cs`. Comprendono quadratura indipendente delle reazioni firmate e dei relativi momenti, interfacce fittizie, strati superficiali sottili, falda interna, eccentricità, continuità di V e M e controllo della relazione esportata.

| Caso, D = 1 m | Risultato analitico Hu |
| --- | --- |
| Coesivo, Cu=50 kPa, testa impedita, L=10 m, My=450 kNm | 450 kN, lungo |
| Coesivo 0-2 m, Cu=40 kPa; granulare 2-4 m, phi=30°, gamma=18; My=10000, testa impedita | 1152 kN, corto; M0=-3339 kNm |
| Caso precedente con falda a 1 m, gamma_sat=20 in entrambi gli strati | 870,84 kN; M0=-2448,66 kNm |
| Granulare 0-2 m, phi=30°, gamma=18; coesivo 2-10 m, Cu=50; My=778,5, testa impedita | 774 kN, lungo; z_f=3 m |
| Stesso profilo, testa libera, e=1 m, My=2331 kNm | 774 kN, lungo |
| Granulare omogeneo, phi=30°, gamma=18, L=10 m, My=216, testa impedita | 324 kN, lungo |

Le prove stratificate verificano anche il corto granulare distribuito contro una soluzione analitica distinta dalla chiusura concentrata. Per p_lim=kz, testa libera ed e=0: Hu = k L^2 [2^(-2/3) - 1/2].

Esecuzione dalla radice del repository:

```
dotnet run --project supporto/test/X.Verifiche -c Release -- --horizontal
```

Il comando esegue i test storici, quelli CHS e quelli stratificati. L'opzione `--horizontal-stratified` esegue soltanto la nuova suite. I log di questa attività sono in `supporto/artefatti/palo-stratificato-20260930/`.

## Uso dal codice

Il nuovo profilo, gli integrali e l'inversione delle risultanti sono in `X.Calculations/PaloOrizzontale.Stratified.cs`. L'equilibrio dei meccanismi resta in `PaloOrizzontale.cs`, condiviso con il metodo precedente.

```
JsonObject risultato = PaloOrizzontale.CalculateStratified(dati);
```

La funzione riceve lo stesso input del modulo orizzontale, lavora su una copia e imposta `generali.metodo_calcolo` a `Stratificato (PileChecker)`. In alternativa si assegna quella chiave e si chiama `Calculate`. L'errore è restituito nella chiave `errore`; una stringa vuota indica calcolo riuscito.

I risultati espongono metodo, versione del motore, capacità, meccanismo, cerniere, residui, quota d'inversione e fine delle reazioni. Ogni sondaggio contiene `diagramma_limite`, con intervalli, tipo di terreno, tensione efficace iniziale, p iniziale, pendenza, risultante e momento primo.

Fonti: codice PileChecker dell'utente, `Checker/PileCalculator.cs`, metodo orizzontale e `FindDepthForForce`; Viggiani, *Fondazioni*, pp. 400-415 per le leggi locali e i casi classici; Broms (1964), *Lateral Resistance of Piles in Cohesive Soils*, DOI 10.1061/JSFEAQ.0000611. Le scelte aggiunte sono dichiarate in questa guida.
