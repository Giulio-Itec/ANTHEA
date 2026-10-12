## Muri di sostegno metodi disponibili e perimetro del modello

Nel modulo muri di ANTHEA le verifiche locali, la stabilità globale, le resistenze strutturali e gli spostamenti sono valutazioni separate. Un esito favorevole allo scorrimento non dimostra l’assenza di una superficie di scivolamento profonda; una fessura accettabile nel fusto non garantisce la capacità della fondazione. Questa distinzione serve anche per leggere correttamente il campo di ciascuna verifica.

### Il campo geometrico del modello locale

La configurazione attuale è una sezione piana per metro di sviluppo, con paramento di monte verticale e riempimento orizzontale granulare c′ = 0. Sono disponibili muro a mensola in c.a. e muro a gravità trapezoidale. Le stratigrafie di monte e valle descrivono terreni e falda; non abilitano automaticamente ogni geometria della superficie di spinta. Le formulazioni pseudostatiche operano con le ulteriori restrizioni indicate nel modulo.

In Rankine, per terreno granulare orizzontale e parete liscia, Ka = (1 − sin φ)/(1 + sin φ). Con φ = 30 gradi vale 1/3. Per H = 4 m e γ = 18 kN/m³, la risultante triangolare vale 48 kN/m ed è applicata a H/3 dal piede. Una falda alta 4 m aggiungerebbe, nell’idealizzazione idrostatica, circa 78,48 kN/m di pressione dell’acqua, mentre la componente del terreno andrebbe ricalcolata in tensioni efficaci. Non è corretto mantenere il peso saturo nella spinta efficace e poi aggiungere l’acqua.

### Bishop e spostamento permanente

La ricerca Bishop usa superfici circolari e un equilibrio a conci semplificato: numero dei conci, dominio di ricerca, falda e stratigrafia influenzano il risultato. Allargare il dominio e controllare superfici prossime al bordo aiuta a riconoscere un minimo condizionato dalla ricerca. Non è una verifica di cinematismi non circolari o di ogni possibile rottura progressiva.

Il Newmark implementato richiede accelerogrammi e soglia di scorrimento assegnata. Lo spostamento si accumula durante gli intervalli di moto del blocco idealizzato, con arresto coerente; non si ottiene integrando due volte l’intero accelerogramma senza condizioni di scorrimento. Soglia, orientamento, scala e stato limite del segnale vanno documentati. Questo metodo non coincide con una formula empirica di spostamento basata su parametri sintetici.

### Funzioni disponibili e limiti del modulo

| Ambito | Stato in ANTHEA |
| --- | --- |
| Muri in c.a. e a gravità, verifiche locali e globali | Presenti entro il campo del modello; Bishop richiede i propri dati e attivazione |
| Pressoflessione, taglio e fessurazione | Presenti; leggere completezza di materiali, armature e combinazioni |
| Terrapieno inclinato, geometria e parametri di spinta più generali | Copertura parziale: il modello locale resta a monte verticale, riempimento orizzontale e c′ = 0 |
| Parametri sismici ricavati dalla località e spettro | Il modulo muri usa kh e kv assegnati oppure ag/g e F0 inseriti; non integra la selezione geografica e lo spettro completo |
| Spostamento secondo Richards ed Elms | Non implementato; è disponibile Newmark con input propri |
| Esecutivi DXF e DWG | Non disponibili; tavola e distinta attuali sono un predimensionamento con esportazioni documentali e immagine |
| Documentazione di progetto e manutenzione | Report e distinta presenti; mancano relazioni specialistiche complete e piano di manutenzione |

La tabella descrive il campo del codice corrente. Il campo è dichiarato nei modelli RetainingWall; leggere anche gli avvisi del foglio corrente.

### Proseguire nella Wiki

- [Muri di sostegno con stratigrafie di monte e valle](wiki:guida-muri-di-sostegno-con-stratigrafie-di-monte-e-valle)
- [Dalle indagini geotecniche ai parametri del modello](wiki:geotecnica-parametri)
- [Cedimenti edometrici tempi di consolidazione e drenaggi](wiki:cedimenti-consolidazione)

