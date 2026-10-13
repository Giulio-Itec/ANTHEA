## Reologia del calcestruzzo

La finestra di supporto calcola il coefficiente di viscosità nel tempo, il suo limite a infinito e la maturazione del modulo elastico. Si apre da **Strumenti → Reologia del calcestruzzo…**, oppure dai materiali e dalle opzioni SLE della sezione in calcestruzzo. Il richiamo dalla sezione propone la resistenza del materiale corrente. Gli altri parametri richiedono la scelta del progettista.

### Impostare materiale ambiente ed età

Inserisci fck in MPa, classe di cemento S, N o R, umidità relativa in percentuale, dimensione nominale h₀ in mm ed età di applicazione del carico t₀ in giorni. La dimensione nominale è h₀ = 2 Ac / u, dove Ac è l'area di calcestruzzo e u il solo perimetro esposto all'essiccamento. La finestra non ricava automaticamente questo perimetro dalla sezione.

L'età t è misurata dal getto, non dall'applicazione del carico. Scegli età iniziale, età finale e risoluzione del campionamento, poi premi **Aggiorna le curve**. Sono ammessi fck fra 12 e 90 MPa, umidità fra 40 e 100 %, h₀ positivo e t₀ almeno pari a 0,5 giorni. L'intervallo deve avere età positive crescenti; la risoluzione è un intero fra 20 e 1000. Sono accettati punto e virgola come separatore decimale, senza separatori delle migliaia.

### Leggere grafico tabella e limite a infinito

Le due schede superiori mostrano **φ(∞, t₀)**, limite analitico, ed **Ecm,28**, modulo secante a 28 giorni. La curva si sceglie fra viscosità φ(t, t₀), modulo Ecm(t) e fattore Ecm(t)/Ecm,28. L'asse del tempo e quello delle ordinate possono essere logaritmici. In scala logaritmica verticale φ = 0 non può essere disegnato; questi campioni restano nella tabella.

La scheda **Lettura puntuale** valuta direttamente un'età positiva, anche se non coincide con un punto del grafico. Nella **Tabella** sono presenti età, φ, Ecm e fattore: selezionare una riga aggiorna la lettura. La risoluzione regola il campionamento grafico; sono aggiunti i punti di carico e a 28 giorni, quando interni all'intervallo. Non regola l'accuratezza delle formule.

La rotella ingrandisce il grafico; trascinando lo si sposta; il doppio clic o **Adatta** ripristina l'inquadratura. **Esporta grafico…** salva una PNG con fondo chiaro, anche se la finestra usa l'aspetto scuro. Il separatore fra grafico e letture consente di ridimensionare i pannelli.

Una modifica a materiale, ambiente, età o campionamento svuota i risultati precedenti finché non si aggiornano le curve. Un'età di lettura errata svuota soltanto la lettura puntuale. I messaggi indicano il campo da correggere; nessun valore non valido è trasformato implicitamente in zero.

### Usare il risultato nel progetto

Ecm(t) è il modulo per maturazione, non il modulo efficace ridotto per viscosità. La finestra non modifica automaticamente φ, n, materiali, combinazioni o revisioni del foglio aperto. Il coefficiente da trasferire alle verifiche richiede coerenza con l'età di carico, la durata e il modulo di riferimento descritti nella [teoria della reologia](wiki:reologia-calcestruzzo).

Esempio: fck = 25 MPa, cemento N, RH = 50 %, h₀ = 200 mm e t₀ = 7 giorni danno φ∞ = 3,442704. A 365 giorni si ottengono φ = 2,603961, Ecm = 33229,780 MPa e fattore E = 1,055724. A 28 giorni il fattore E vale esattamente 1; prima del carico φ vale zero.

Nei verificatori SLE, φ e n errati sono segnalati e i valori derivati sono svuotati. Le staffe richiedono Sì/No e, quando attivi, 2–100 rami interi. NTC espone l'interasse anche nelle sezioni interamente tese.

