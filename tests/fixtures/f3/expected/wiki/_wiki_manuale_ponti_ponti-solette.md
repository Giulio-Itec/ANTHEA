## Solette da ponte e percorso locale delle azioni

La soletta distribuisce le azioni delle ruote e trasferisce le reazioni alle travi. Il suo comportamento locale non coincide con quello della trave principale: direzione dell’armatura, interasse degli appoggi, sbalzi e area caricata controllano modelli differenti. Una striscia di un metro è utile per capire l’equilibrio, ma non rappresenta automaticamente la distribuzione bidimensionale di una ruota.

### Piastra continua e mensola laterale

Tra le travi, la soletta lavora come piastra con rigidezze e vincoli da identificare. Sullo sbalzo il momento negativo tende la faccia superiore: l’armatura principale deve attraversare il collegamento con la parte interna e avere ancoraggio adeguato. La barriera può introdurre azioni concentrate, momento e torsione; la sua classe e il modo di trasferimento al cordolo determinano una verifica locale distinta dal traffico ordinario.

Le azioni da impalcato vanno separate secondo natura e fase: peso del getto, pavimentazione, cordoli, veicoli, frenatura, temperatura e azioni eccezionali non sono intercambiabili. Un modello a graticcio deve riprodurre anche la rigidezza trasversale; assegnare tutta la rigidezza flessionale alle travi longitudinali può falsare la ripartizione.

### Esempio di controllo manuale dello sbalzo

Per una striscia idealizzata larga 1 m, sbalzo a = 1,20 m, carico lineare q = 8 kN/m e forza al bordo P = 30 kN, già riferiti alla stessa combinazione, l’equilibrio al vincolo fornisce:

```math
V=qa+P
M=\frac{qa^2}{2}+Pa
```

Si ottengono V = 39,60 kN e |M| = 41,76 kNm. Con h = 250 mm, copriferro al ferro più esterno 45 mm e barra principale Ø16, se questa è effettivamente la barra esterna, d = 250 − 45 − 8 = 197 mm. Assumendo preliminarmente z = 0,9d e fyd = 391,30 MPa, As = M/(z fyd) risulta circa 602 mm²/m. La formula è una stima di armatura a flessione semplice; vanno poi verificati equilibrio della sezione, minimi, fessure, taglio, dettagli e disposizione reale dei diversi strati.

Questi carichi sono inventati per il controllo manuale e non costituiscono un modello di traffico normativo. In particolare P non rappresenta una forza di urto della barriera. La larghezza efficace di diffusione di una ruota deve provenire dal modello adottato.

### Durabilità e dettagli che cambiano il calcolo

Il copriferro maggiore può migliorare la protezione delle armature ma riduce d a spessore costante. Lo spessore del pacchetto impermeabilizzazione–pavimentazione modifica peso e quote. Ristagni presso giunti, scarichi e cordoli aumentano l’importanza del dettaglio di drenaggio. Il disegno deve mostrare continuità dell’armatura superiore, interferenze e possibilità concreta di getto.

ANTHEA consente di verificare la sezione in c.a. soggetta alle azioni ricavate dal modello. Bridge Design fornisce un’impostazione concettuale dell’impalcato: non sostituisce una verifica locale completa della piastra sotto ruote o barriera.

### Proseguire nella Wiki

- [Cos'è un ponte?](wiki:bridge)
- [Taglio nel calcestruzzo armato e scelta del traliccio](wiki:taglio-traliccio)

