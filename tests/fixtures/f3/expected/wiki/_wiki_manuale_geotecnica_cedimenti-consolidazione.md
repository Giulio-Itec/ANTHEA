## Cedimenti edometrici tempi di consolidazione e drenaggi

La verifica di resistenza non determina il cedimento. Il cedimento dipende da distribuzione dell’incremento di tensione, compressibilità, storia di carico e tempo. La prova edometrica riproduce una compressione con deformazione laterale impedita: è coerente con una schematizzazione monodimensionale, non con ogni configurazione tridimensionale della fondazione.

### Integrazione per strati

Assumendo un modulo edometrico Eoed costante nell’intervallo di tensione del singolo strato, la deformazione è Δσ′/Eoed e il contributo al cedimento è H Δσ′/Eoed. Per uno strato di 2 m con incremento efficace medio 60 kPa ed Eoed = 12000 kPa, s = 0,010 m, cioè 10 mm. Usare lo stesso modulo su un percorso che supera la tensione di preconsolidazione può sottostimare la compressibilità: occorre seguire la curva di prova o un modello appropriato.

Gli incrementi di tensione non sono uniformi sotto una fondazione finita. Suddividere il terreno in strati serve anche a rappresentarne l’attenuazione con la profondità. La distorsione angolare dipende dai cedimenti differenziali e dalla distanza, mentre una traslazione uniforme ha effetti diversi sulla sovrastruttura.

### Il ruolo della lunghezza drenante

Nel modello classico monodimensionale con coefficiente cv costante, il fattore di tempo è Tv = cv t/Hdr². Hdr è il massimo percorso di drenaggio: per uno strato di spessore H con drenaggio sopra e sotto vale H/2, mentre con una sola faccia drenante vale H. A parità di grado medio di consolidazione, raddoppiare Hdr quadruplica il tempo. Per un incremento iniziale uniforme, al 90% si usa il valore classico Tv ≈ 0,848.

Esempio: strato H = 4 m, cv = 10⁻⁷ m²/s. Con doppio drenaggio Hdr = 2 m, t90 ≈ 33,92 milioni di secondi, circa 393 giorni. Con drenaggio da una sola faccia si passa a circa 1570 giorni. La costruzione per fasi e cv variabile modificano questa stima; il cedimento secondario non è incluso nella soluzione di consolidazione primaria.

### Acqua e opere di sostegno

La previsione del drenaggio deve restare distinta dal comportamento idraulico reale: intasamento, recapito insufficiente o guasto possono cambiare la falda. La spinta dell’acqua non si elimina scegliendo una tensione efficace del terreno. In ANTHEA i cedimenti del muro richiedono l’attivazione del calcolo e dati propri; non sono dedotti dall’esito favorevole a portanza.

Riferimento bibliografico: FHWA GEC 6, Shallow Foundations. Esempi originali con ipotesi esplicite.

### Proseguire nella Wiki

- [Dalle indagini geotecniche ai parametri del modello](wiki:geotecnica-parametri)
- [Capacità portante delle fondazioni superficiali](wiki:bearing-capacity)

