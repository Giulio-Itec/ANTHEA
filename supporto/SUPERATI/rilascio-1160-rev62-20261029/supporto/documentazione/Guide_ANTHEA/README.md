# Guide globali ANTHEA

Edizione 6, 28 ottobre 2026. Guide pratica e teorica Rev61. Comprendono gestione dei modelli, importazione e risultati per fasi, reologia e dettaglio delle sezioni con contorni e offset condivisi tra 2D e 3D.

| Guida | Word | PDF | Sorgente della Wiki |
|---|---|---|---|
| Pratica | [Word](ANTHEA_Guida_pratica_ITEC_Rev61.docx) | [PDF](ANTHEA_Guida_pratica_ITEC_Rev61.pdf) | [Markdown](../../docs/guida-pratica-anthea.md) |
| Teorica | [Word](ANTHEA_Guida_teorica_ITEC_Rev61.docx) | [PDF](ANTHEA_Guida_teorica_ITEC_Rev61.pdf) | [Markdown](../../docs/guida-teorica-anthea.md) |

La Rev61 accompagna ANTHEA 1.15.0. Nella guida pratica: la scheda 08 Colonna modello della sezione c.a., con dati, imperfezione ed eccentricità minima, viscosità, calcolo e Interrompi, i due grafici, i riquadri dei risultati, Esporta CSV… e il capitolo della relazione con la scelta Momento–curvatura e colonna modello; Calcola armature diventa la scheda 09 e i documenti salvati prima si riaprono sulla stessa scheda; nel modulo Modelli i file di testo MIDAS Civil .mct e Gen .mgt, senza risultati, e l’importazione dei modelli Straus7 .st7 con i risultati statici lineari .lsa, con il controllo di file e casi, le combinazioni LSA ricostruite dalla libreria GPC, la numerazione delle aste e la convenzione di segno della copia. Nella guida teorica: il metodo della colonna modello di EN 1992-1-1 5.8.6(6) e 5.8.8.2, richiamato da NTC 2018 §4.1.2.3.9.3, con formule, imperfezione, eccentricità minima, viscosità efficace, modo di crisi, curvatura nominale come confronto, limiti e scostamenti dichiarati K-1…K-6; unità, segni e combinazioni delle copie dei modelli importati. Gli originali Rev60 sono conservati in `supporto/SUPERATI/rilascio-1150-rev61-20261028/`.

La Rev60 accompagna ANTHEA 1.14.4 e raccoglie anche le novità della 1.14.1, della 1.14.2 e della 1.14.3. Nella guida pratica: Separatore decimale nel menu Aspetto (Sistema, Virgola, Punto); profondità della testa dei pali e dei micropali sotto il piano campagna; secondo anello interno, anche di altro diametro, nei report del palo orizzontale; MRd con fyd·r nello sviluppo parziale delle barre della risposta elastica; il nuovo «Tension stiffening» della curva M–χ. Nella guida teorica: tensioni SLE dell’analisi non lineare con le leggi di progetto, come scelta dichiarata; θmin di Model Code 2010 nel taglio e nella torsione; limite di EN 1992-1-1 §6.2.2(6) senza staffe; copriferro NTC con la classe minima UNI 11104 quando manca il minimo pertinente; ramo incrudente dell’acciaio di progetto riferito a fyd; passiva a valle fuori dal ribaltamento dei muri; soglia di wk = 0 sul copriferro reale; sviluppo parziale delle barre del palo elastico; teste dei pali sotto il piano campagna con le tensioni del ricoprimento; tension stiffening sulle barre con il modello del tirante (β = 0,4); combinazioni dei muri generate da Model, con G2 favorevole 0,8 nelle righe SLU e nella stabilità globale e i γR della Tab. 7.11.III in tutte le righe sismiche. Gli originali Rev59 sono conservati in `supporto/SUPERATI/rilascio-1144-rev60-20261028/`.

La Rev59 accompagna ANTHEA 1.14.0, che apre la finestra nuova. Nella guida pratica: barra di navigazione sempre visibile, fogli in schede, finestre staccate e pagina intera, Wiki in una scheda; albero dei progetti in una colonna, menu dei nodi, Annulla e Ripristina con le schede aperte, revisioni consultate con più schede, controllo e avanzamento del report del ramo; le revisioni in sola lettura dei muri (Valori di calcolo e Stabilità globale); i comandi della Wiki con i loro nomi attuali. La guida teorica cambia solo nell’intestazione e nei percorsi delle figure, che stanno in `src/ANTHEA.Desktop/Assets/Wiki`. Gli originali Rev58 sono conservati in `supporto/SUPERATI/rilascio-1140-rev59-20261024/`.

La Rev57 corregge la pretensione dei tiranti fra le fasi delle paratie: una cella vuota mantiene la pretensione della fase precedente, un valore la sostituisce e 0 la rilascia; le riduzioni sono segnalate. Nella guida teorica sono aggiunti la legge del sostegno e un esempio numerico. Gli originali Rev56 sono conservati in `supporto/SUPERATI/paratie-pretensione-rev57-20261021/`.

La Rev56 documenta l’albero di lavoro richiudibile di Paratie e le armature separate della capsula, con dettagli generali del pilastro e limiti del riconoscimento automatico. Gli originali Rev55 sono conservati in `supporto/SUPERATI/paratie-albero-rev56-20261020/`.

La Rev55 aggiunge il catalogo Esempi, l’apertura come copia e il componente dell’installer. La teoria resta invariata. Gli originali Rev54 sono conservati in `supporto/SUPERATI/esempi-rev55-20261020/`.

La Rev54 integra salvataggio nativo GPC delle sezioni e dodici tipologie da wizard, con limiti delle staffature e del taglio espliciti. Gli originali Rev53 sono in `supporto/SUPERATI/sezioni-rev54-20261020/`.

La Rev53 aggiunge la sezione grafica modificabile delle paratie, con navigazione per caso e fase ed editor affiancato. I 18 originali sostituiti sono conservati con SHA-256 in `supporto/SUPERATI/paratie-grafica-rev53-20261020/`.

La Rev52 riunisce i capitoli paratie e Spettri NTC dei due rami, con le relative funzioni e i limiti documentati. Le due edizioni sorgente sono conservate in `supporto/SUPERATI/integrazione-f5-rev52-20261019/`.

La Rev51 integra storie indipendenti nel foglio, verifiche e filtrazione per caso, inviluppi comuni, ricerca dell’infissione su tutti i casi e persistenza completa. Le azioni sono assegnate dall’utente; la generazione automatica delle combinazioni resta aperta. La Rev50 è conservata byte per byte in `supporto/SUPERATI/paratie-rev51-20261019/`.

La Rev50 ha introdotto inviluppi delle fasi con provenienza, azioni concomitanti, diagrammi, persistenza e CSV, leggendo gli stessi risultati del report.

La Rev49 integra la ricerca discreta dell'infissione: geometrie e storie indipendenti, ricalcolo FEM, criteri governanti, distinzione fra candidato accettabile e minimo dimostrato, annullamento e benchmark analitico. Il modulo è in sviluppo sul ramo dedicato; le funzioni residue e il rilascio ufficiale restano aperti.

Le revisioni precedenti sono conservate in `supporto/SUPERATI/fondazioni-superficiali-rev34/` e `supporto/SUPERATI/merge-f4-modelli-rev35-20261012/`, con origine, motivo e impronte nel registro di archiviazione. Le edizioni pratica Rev35 e teorica Rev34 sono archiviate in `supporto/SUPERATI/rilascio-s-rev36-20261012/`. Il motore di portanza è disponibile con il rilascio approvato GPC S, distribuito con ANTHEA.

La Rev36 è conservata in `supporto/SUPERATI/reologia-rev37-20261012/`, con registro delle impronte e delle sostituzioni.

La Rev37 è conservata in `supporto/SUPERATI/stage-rev38-20261012/`. La Rev38 aggiunge importazione e visualizzazione dei risultati per fase, con i limiti del collaudo dichiarati.

La Rev38 è conservata in `supporto/SUPERATI/sezioni-rev39-20261013/`, con registro delle impronte e delle sostituzioni.

La Rev39 è conservata in `supporto/SUPERATI/servizi-f3-rev40-20261013/`, con registro di origine e impronte. La Rev40 descrive il ricalcolo per revisione, le proposte dei tratti e la composizione autonoma dei capitoli; non dichiara concluso l’intero refactoring.

La Rev40 è conservata in `supporto/SUPERATI/sollecitazioni-rev41-20261013/`, con origine e SHA-256. La Rev41 introduce le sollecitazioni unificate, il nuovo Excel e la scelta indipendente del criterio resistente.

La Rev41 è conservata in `supporto/SUPERATI/report-curvatura-rev42-20261013/`, con origine e SHA-256. La Rev42 documenta il calcolo automatico della curva momento-curvatura nel report di progetto; la teoria e le formule restano invariate.

La Rev42 è conservata in `supporto/SUPERATI/f4-rev43-20261014/`, con origine e SHA-256. La Rev43 documenta prezzari sostituibili, associazioni e revisioni, traffico Schema 1, norme esplicite e servizi GPC della fase F4. L’esempio Bridge da 120 m conserva il modello uniforme e i propri attesi.

La Rev43 è conservata in `supporto/SUPERATI/f4-rev44-20261014/`, con origine e SHA-256. La Rev44 documenta i report in Word e PDF senza Microsoft Word, le analisi composte dei prezzi, il piano delle deformazioni totali con viscosità e la compressione dei minimi dei pilastri; i riferimenti restano alle sole norme e alla bibliografia dei metodi.

La Rev44 è conservata in `supporto/SUPERATI/installazione-rev45-20261015/`, con origine e SHA-256. Nella Rev45 la frase d’installazione della guida pratica non riporta più il numero di versione di ANTHEA, così non va aggiornata a ogni rilascio; la guida teorica è invariata nel contenuto e prende lo stesso numero di revisione.

La Rev45 è conservata in `supporto/SUPERATI/stratigrafia-rev46-20261015/`. La Rev46 introduce il modulo autonomo Stratigrafia del terreno; il collegamento ai motori geotecnici resta successivo.

La Rev46 è conservata in `supporto/SUPERATI/correzioni-rev47-20261016/`, con origine e SHA-256. La Rev47 descrive le regole delle verifiche corrette il 15/10/2026: dominio N–M sul meridiano della direzione del momento ed esiti a N assegnato fuori campo, ε0 della curva M–χ, copriferro della fessurazione dalle barre, barre lisce negli ancoraggi, B450A con εu 25 ‰, segno di Δe di Model Code 2010, coefficienti unitari del palo, peso del palo nelle azioni, testa impedita di Broms, efficienza di Reese e Van Impe, passiva e gemelle dei muri, spostamento in testa dei muri, Meyerhof non drenato, tension stiffening della sezione composta, punto proiettato del grafico N–M, nota dell’acciaio salvato nei materiali dei muri e «CLS teso nelle risposte» della curva M–χ.

La Rev47 è conservata in `supporto/SUPERATI/spettri-rev48-20261019/`, con origine e SHA-256. La Rev48 introduce il modulo Spettri NTC e isolamento sismico, le convenzioni di compatibilità numerica, i dati assegnati, il pendolo e la distinzione fra valutazione analitica e campione dell’esempio.
La Rev47, i suoi sorgenti e gli indici sostituiti sono conservati in `supporto/SUPERATI/paratie-rev48-20261019/`, con origine e SHA-256.

La Rev48, i sorgenti e gli indici sostituiti sono conservati in `supporto/SUPERATI/paratie-rev49-20261019/`, con origine e SHA-256.

La Rev49, i sorgenti e gli indici sostituiti sono conservati in `supporto/SUPERATI/paratie-rev50-20261019/`, con origine e SHA-256.
