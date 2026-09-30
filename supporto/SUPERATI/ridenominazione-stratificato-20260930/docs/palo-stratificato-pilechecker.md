# Palo orizzontale in terreno stratificato

Sviluppo dell'approccio PileChecker - revisione 2, 30 settembre 2026. Riferimenti e diagrammi del terreno.

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

La finestra **Diagrammi e dettagli** e la relazione esportata mostrano tutta la lunghezza. Nei pali lunghi il completamento inferiore è tratteggiato su fondo grigio: visualizzarlo non lo rende una soluzione di compatibilità degli spostamenti. L'anteprima compatta del profilo nella schermata principale conserva il solo ramo superiore.

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

La nuova diagnostica `diagramma_terreno` contiene tensione totale, pressione idrostatica, tensione efficace, limite locale in kPa e kN/m, risultante Q e lato dell'interfaccia. Le stesse grandezze sono disponibili nelle tabelle e nelle colonne aggiunte al CSV. I riferimenti sono in `riferimenti`, con URL e ruolo della fonte. I controlli della revisione 2 aggiungono tensioni con falda nello strato coesivo sovrastante, salti del limite locale, conversione q=p/D e pesi mancanti nel solo coesivo. Log e verifiche grafiche della revisione 2: `supporto/artefatti/palo-diagrammi-20260930/`.

<!-- pagebreak -->

## Come leggere i grafici

La vista **Terreno e tensioni** affianca la stratigrafia e tre diagrammi sulla stessa scala delle profondità:

- **Tensioni verticali**: sigma_v totale da peso proprio, pressione interstiziale u = 9,81 max(0,z-z_w), tensione efficace sigma'_v = sigma_v-u. Non si considera suzione sopra falda. I pesi degli strati coesivi contribuiscono al confinamento dei granulari sottostanti.
- **Pressione laterale equivalente**: inviluppo positivo e negativo q_lim = p_lim/D e distribuzione q = p/D adottata all'equilibrio, in kPa. q è una pressione equivalente sulla larghezza D, non la distribuzione delle tensioni sulla circonferenza del palo.
- **Reazione lineare**: inviluppo positivo e negativo p_lim e distribuzione con segno p adottata, in kN/m. Alle interfacce possono comparire salti, anche se sigma'_v è continua.

La vista **Equilibrio del palo** affianca p, V, M e Q. Q è l'integrale della resistenza limite positiva, non della reazione con segno: Q(z_f)=Hu. Il grafico M comprende i limiti +My e -My e le cerniere. Nei diagrammi V e M resta esplicito l'effetto di eventuali forze concentrate del metodo Broms.

Falda, interfacce, taglio superficiale 1,5D, z_f, inversione b e fine delle reazioni t rendono leggibili i cambiamenti. **z_f non è b**: il primo individua il taglio nullo; il secondo cambia il verso della reazione. L'interpretazione di b come centro di rotazione vale nello schema rigido del palo corto; il programma non calcola una rotazione.

I diagrammi rappresentano **lo stato limite alla capacità Hu**, non le tensioni sotto HEd e non una verifica di esercizio. I coefficienti xi e gamma_R riducono la capacità globale e non vengono applicati alle singole pressioni del grafico. Il titolo "tensioni" non implica pertanto un ulteriore diagramma locale di progetto già fattorizzato.

Nel profilo interamente coesivo la capacità non usa gamma. Se i pesi non sono disponibili, sigma_v e sigma'_v sono dichiarate non disponibili, senza sostituirle con zero e senza impedire il calcolo della capacità da Cu. La reazione limite coesiva resta leggibile.

## Esempio con falda e tre strati

D=1 m, L=10 m, testa libera, e=0, My=900 kNm. Coesivo 0-2 m con Cu=40 kPa; granulare 2-5 m con phi'=30°; coesivo 5-10 m con Cu=65 kPa. In tutti gli strati gamma=18 e gamma_sat=20 kN/m³, falda a 1 m.

All'interfaccia z=2 m: sigma_v=38 kPa, u=9,81 kPa e sigma'_v=28,19 kPa. Il limite passa da 360 kPa nel coesivo a 253,71 kPa nel granulare, mentre la tensione verticale efficace rimane continua. Poiché D=1 m, p_lim ha lo stesso valore numerico di q_lim ma unità differenti. Con D diverso da 1 i valori numerici non coincidono.

<!-- pagebreak -->

## Riferimenti e attribuzione

**PileChecker dell'utente** - `Checker/PileCalculator.cs`, metodo orizzontale e `FindDepthForForce`. È il punto di partenza per i diagrammi per strato e la ricerca della profondità a taglio nullo. L'equilibrio globale con un unico My e la chiusura inferiore sono esplicitati nell'implementazione ANTHEA.

**C. Viggiani, Fondazioni** - testo fornito, §§13.2.2-13.2.5, pp. 400-415. Fonte operativa delle leggi locali e dei meccanismi classici. Non si attribuisce al libro l'estensione ai profili misti.

**B. B. Broms (1964), Lateral Resistance of Piles in Cohesive Soils** - ASCE, Journal of the Soil Mechanics and Foundations Division, 90(SM2). DOI: https://doi.org/10.1061/JSFEAQ.0000611. Riferimento originale coesivo, ripreso attraverso Viggiani.

**B. B. Broms (1964), Lateral Resistance of Piles in Cohesionless Soils** - stessa rivista, 90(SM3). DOI: https://doi.org/10.1061/JSFEAQ.0000614. Riferimento originale granulare, ripreso attraverso Viggiani e Wood.

**J. Wood (2021), Cantilever Pole Retaining Walls** - New Zealand Geotechnical Society, Geomechanics News, n. 101, pubblicato il 22 giugno 2021. https://www.nzgs.org/libraries/cantilever-pole-retaining-walls/

I §§2.2-2.3 confrontano Broms semplice e **Broms Modified**: il secondo distribuisce le reazioni opposte sotto l'inversione e soddisfa entrambi gli equilibri. È pertinente al nostro palo corto rigido granulare. Il contributo richiama inoltre i limiti delle distribuzioni pienamente plastiche (§§2.5 e 2.8). Non valida la nostra estensione mista o il completamento dei pali lunghi. Chen e Kulhawy (1994) sono richiamati da Wood; non sono stati usati qui come fonte direttamente consultata.

**FHWA (2018), Geotechnical Engineering Circular No. 9** - Design, Analysis, and Testing of Laterally Loaded Deep Foundations that Support Transportation Facilities, FHWA-HIF-18-031. https://www.fhwa.dot.gov/engineering/geotech/pubs/hif18031.pdf

I §§6.3 e 6.5 inquadrano p-y e Broms. I modelli p-y collegano reazione e spostamento ma richiedono curve appropriate e presentano limiti applicativi, soprattutto per pali corti rigidi e grandi diametri. Servono come riferimento per un futuro confronto dell'interazione terreno-palo; non sono implementati dal presente metodo. Questo documento tecnico statunitense non sostituisce le verifiche normative italiane.

I contributi NZGS e FHWA sono stati consultati online il 30 settembre 2026. I DOI di Broms identificano i lavori originali; le formule operative sono state controllate sul testo Viggiani fornito. Nessun riferimento è presentato come validazione sperimentale dell'intero algoritmo stratificato.
