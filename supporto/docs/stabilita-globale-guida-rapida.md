# Stabilità globale: guida rapida

ANTHEA · Muri di sostegno · 30 settembre 2026

La verifica cerca una superficie lungo la quale possono scivolare **insieme il muro e il terreno sottostante**. Il cerchio viene diviso in conci: il programma confronta l’effetto delle azioni con la resistenza dei terreni attraversati. Serve quindi conoscere il terreno anche sotto la fondazione.

## Il percorso in tre passi

**1. Apri Stabilità globale**, nella barra superiore del muro. Se non hai ancora iniziato il profilo, ANTHEA lo prepara dai dati locali e attiva la verifica. Riaprire il pannello conserva il lavoro. Il pulsante Prepara dal muro sostituisce invece la proposta con i dati locali.

**2. Controlla il sito.** Completa gli strati profondi di monte e valle, correggi le superfici in base al rilievo e controlla falda e sisma. La ricerca è già proposta; i dettagli si possono aprire e modificare. Il messaggio in alto indica il primo dato mancante.

**3. Conferma i dati e premi Calcola globale.** Spunta Ho controllato profilo, strati e falda del sito. Il risultato si apre direttamente in Verifiche → Stabilità globale. Una modifica ai terreni del percorso annulla la conferma e il risultato precedente.

![Esempio stratificato: fondazione a y=0, strati profondi e limiti blu della ricerca. Il disegno è ritagliato sull’area di analisi; i fondi effettivi sono riportati nelle etichette.](../artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato-profilo.png)

**Già proposto:** geometria del muro, strati locali disponibili, attivazione di falda e sisma, area di ricerca, precisione iniziale e combinazioni automatiche.

**Da conoscere per il sito:** rilievo, stratigrafia profonda, proprietà geotecniche, falda e condizioni dell’analisi. Il programma non inventa indagini né resistenze profonde.

<!-- pagebreak -->

## Come inserire gli strati

Nella tabella scrivi **nome e spessore**, dall’alto verso il basso. Il fondo y si calcola automaticamente. Il primo spessore parte dalla superficie del proprio lato presso il muro; ogni successivo spessore parte dal fondo precedente.

**y=0 è il piano di posa. y=−2 m significa 2 m sotto la fondazione.** Nell’esempio H=3 m e t=0,45 m: la superficie di monte è a y=3,45 m, quella di valle a y=0.

| Lato | Terreno | Spessore [m] | Fondo y [m] |
| --- | --- | --- | --- |
| Monte | Riempimento | 3,45 | 0 |
| Monte | Alluvioni | 2 | −2 |
| Monte | Ghiaia | 8 | −10 |
| Valle | Alluvioni | 2 | −2 |
| Valle | Ghiaia | 8 | −10 |

Seleziona una riga per modificare le proprietà. In **Drenata** inserisci φ′k e c′k; in **Non drenata** serve cu,k per ogni strato. γ e γsat sono i pesi di volume naturale e saturo. Stesso nome significa stesso colore nelle due colonne, ma i valori restano indipendenti.

![Editor della colonna di monte con lo strato Alluvioni selezionato. Le quote derivano dagli spessori; le proprietà si modificano sotto la tabella.](../artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato-editor.png)

Per l’esempio illustrativo: Riempimento γ/γsat=18/20 kN/m³ e φ′k=30°; Alluvioni 19/21 e 28°; Ghiaia 20/22 e 36°. c′k=0 per tutti, assenza di falda e sisma. Questi valori servono a riprodurre l’esempio; non sono valori da assumere per un sito reale.

<!-- pagebreak -->

## Rilievo, falda e ricerca

Nel gruppo Rilievo e falda si possono correggere i punti **x, y**. L’origine è il bordo di valle del piano di posa. x cresce verso monte e y verso l’alto. La proposta iniziale ha superfici orizzontali sui due lati: modificarla se il terreno reale è inclinato o presenta dislivelli.

La falda globale è una linea di quote y, non una profondità misurata dal piano campagna. La condizione drenata/non drenata va scelta in base al problema geotecnico; il programma cambia i parametri di resistenza richiesti.

Con ricerca **Automatica**, i limiti orizzontali seguono il rilievo. La profondità va da 0,10 m al minore fra 2(H+t) e la profondità nota comune alle colonne: nell’esempio è 6,90 m. La proposta non garantisce che il dominio sia sufficiente. Per modificarla scegliere **Assegnata** e aprire Limiti e precisione. Anche in Automatica si possono modificare nodi, conci e raffinamenti.

Il sisma globale ha una propria opzione. Da sito usa βs=0,38 e i dati sismici del progetto; in alternativa si assegnano kh e il modulo di kv. Non coincide necessariamente con il coefficiente usato per le spinte del muro. Controllare l’opzione nel gruppo Falda e sisma; la precompilazione ne riprende l’attivazione dal muro.

## Come leggere l’esito

Leggi la colonna **Esito** per tutte le combinazioni. F è il fattore calcolato con i parametri di progetto; **η=γR/F** è il tasso di lavoro. La verifica è soddisfatta nel dominio esplorato quando η≤1 e i controlli della ricerca e della convergenza sono superati.

| Messaggio | Cosa controllare |
| --- | --- |
| Soddisfatta nel dominio esplorato | Risultato favorevole entro il modello e l’area analizzati. |
| Non soddisfatta | Resistenza insufficiente per quella combinazione. Riesaminare progetto e modello; non modificare arbitrariamente i terreni. |
| Minimo sul bordo | Ampliare la ricerca, con rilievo e indagini che coprano la nuova area. |
| Ricerca incompleta / discretizzazione non convergente | Esaminare i dettagli e aumentare la precisione; il tasso da solo non conclude la verifica. |

Il disegno mostra il cerchio critico e i conci. La tabella permette di interrogare terreno, pesi, pressioni interstiziali, resistenze e azioni. Il caso iniziale ha il tasso più alto; un caso privo di superficie valida ha priorità. I dettagli e le combinazioni restano modificabili.

<!-- pagebreak -->

## Esempio salvato e risultati ripercorribili

Apri `supporto/artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato.anthea`. Premi Stabilità globale e poi Calcola globale. Il file contiene già input e conferma per questo esempio didattico. Non premere Prepara dal muro, che sostituirebbe gli strati globali con quelli locali.

Muro a mensola: H=3 m, t=0,45 m, fusto 0,40/0,25 m, mensola di valle 0,80 m e di monte 1,80 m; peso del muro 25 kN/m³. Terreno di valle fino al piano di posa, qk=10 kPa uniforme a monte. Profilo da x=−13,80 m a valle a x=16,80 m a monte; confine delle colonne x=1,20 m. Strati e proprietà sono quelli della pagina 2.

Ricerca: uscite x=−13,80/−0,10 m, ingressi x=3,10/16,80 m, profondità 0,10/6,90 m; 9 nodi per direzione, 60 conci iniziali, 4 raffinamenti. Preset statico A2–M2–R2, γR=1,10. Si ottengono:

| Combinazione | F | η=γR/F | Esito |
| --- | --- | --- | --- |
| Globale A2–M2–R2 1 | 1,176315 | 0,935124 | Soddisfatta nel dominio esplorato |
| Globale A2–M2–R2 2 | 1,075737 | 1,022555 | Non soddisfatta |

![Superficie della seconda combinazione: il tasso supera 1 e il cerchio è rosso. L’esempio conserva l’esito sfavorevole per mostrare la lettura della verifica.](../artefatti/globale-guidata-20260930/offscreen-rilascio/esempio-stratificato-esito.png)

Le tabelle dei conci sono in `esempio-stratificato-conci.csv`, accanto al modello. Questo è un controllo interno ANTHEA, non un confronto numerico MAX. Cambiando precisione o dominio possono cambiare leggermente la superficie critica e i valori.

Per l’organizzazione dell’input sono stati consultati il manuale installato di MAX 16 (`manualeMAX.pdf`, §§6.5, 6.6, 6.8 e 16.7) e la [pagina ufficiale MAX](https://www.aztec.it/max-muri-di-sostegno/). L’impostazione per spessori, colori e dettagli ha ispirato l’interfaccia; ANTHEA conserva il proprio motore Bishop e la ricerca per ingresso, uscita e profondità.

Metodo, coefficienti e limiti completi: [guida del modulo](muri-sostegno.md), disponibile anche in PDF. La stabilità globale non comprende cedimenti, liquefazione o una verifica complessiva dell’opera.
