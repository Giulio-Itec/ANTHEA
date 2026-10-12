## Avvio e scelta del modulo

### Avviare la versione aggiornata

Nel repository, Avvia ANTHEA.cmd ricompila il progetto in Release, pubblica nella cartella app e avvia l'eseguibile soltanto se i passaggi riescono. Richiede quindi l'SDK .NET e ANTHEA chiuso durante l'aggiornamento. La distribuzione già pubblicata può essere avviata direttamente da app/ANTHEA.exe. La distribuzione dipendente dal framework richiede il runtime desktop .NET 8. Il sorgente e la distribuzione sono due cose distinte: modificare un file C# non aggiorna un eseguibile già pubblicato.

La Home consente di entrare nel catalogo, riprendere il lavoro della sessione o organizzare i fogli in un progetto. Il pulsante Riprendi conserva l'editor e il suo stato finché l'applicazione resta aperta. Non salva automaticamente un archivio su disco. Prima di chiudere utilizzare Salva o Salva con nome.

### Scegliere l’aspetto

Il menu Aspetto, accanto a File, offre Chiaro, Scuro e Molto scuro. Chiaro mantiene i colori originali; Scuro usa superfici blu; Molto scuro usa grigio antracite e nero. La scelta si applica a Home, Progetti, moduli e Wiki senza chiudere il foglio o modificare i dati del calcolo. Nella Wiki la tendina dell’aspetto controlla la stessa preferenza globale.

La scelta è salvata nel profilo Windows e ripristinata al prossimo avvio. Non fa parte dei documenti o delle revisioni di progetto. Le immagini incorporate e le tavole dei grafici conservano i propri colori per mantenere leggibili legende e rappresentazioni tecniche.

### Scegliere il foglio adatto

| Foglio disponibile | Domanda alla quale risponde | Informazione da preparare |
| --- | --- | --- |
| Palo verticale | Quale resistenza assiale geotecnica si ottiene con questi terreni e questa lunghezza | Stratigrafie, falda, tecnologia, azioni e coefficienti |
| Micropalo verticale | Quale resistenza del bulbo iniettato è compatibile con l'abaco scelto | Terreni, p_l, iniezione, diametri, inclinazione e tratto attivo |
| Palo orizzontale | Quale carico limite laterale risulta dal meccanismo di Broms | Terreno, diametro, lunghezza, vincolo e momento resistente |
| Micropalo orizzontale | Quale capacità laterale risulta usando una sezione tubolare CHS | Diametro geotecnico, tubo, materiale, N e terreno |
| Palificata orizzontale | Come cambiano i fattori di riduzione con geometria e direzione del carico | Posizioni, diametro, teoria e interassi rappresentativi |
| Muri di sostegno | Quali verifiche locali e globali sono applicabili al muro | Geometria, materiali, terreni, acqua, azioni e combinazioni |
| Sezione in c a | Come risponde e si verifica una sezione assegnata | Geometria, barre, materiali e combinazioni N M V T |
| Sezione composta | Come si ripartiscono le tensioni in una sezione da ponte attraverso le fasi | Tipo e geometria, soletta, barre, fasi e metodo |
| Bridge Design | Quale configurazione preliminare, quantità, costo e CO₂ sono plausibili | Sito, campate, larghezza, famiglia, fondazioni e listino |
| Calcestruzzo e durabilità | Quali requisiti del materiale e del copriferro derivano dalle scelte assegnate | Esposizioni, vita, materiale, diametri e condizioni esecutive |
| Acciaio per armature | Quali proprietà e diagramma usare per l'armatura | Classe o dati personalizzati e coefficienti |

Le altre voci eventualmente presenti nel catalogo possono essere predisposizioni. Una scheda di materiali non verifica una sezione. La sezione composta non analizza un intero ponte; Bridge Design non trasferisce automaticamente un modello verificato alla sezione composta. Per passare dall'uno all'altro occorre scegliere la sezione locale, assegnare le azioni da un'analisi appropriata e controllare nuovamente le unità.

### Leggere e compilare i campi

Controllare sempre l'unità a fianco del valore. Nei moduli geotecnici le lunghezze sono generalmente in metri; nelle sezioni strutturali e nelle barre sono in millimetri. Una sezione alta 600 mm non va inserita come 0,60. MPa e N/mm² sono numericamente equivalenti; kPa e kN/m² lo sono altrettanto, e 1 MPa = 1000 kPa: per esprimere in kPa un valore dato in MPa occorre moltiplicarlo per 1000.

I campi vuoti servono anche a rappresentare una bozza incompleta. Non sostituire sistematicamente un dato sconosciuto con zero: in alcuni campi zero ha un significato fisico, in altri attiva un automatismo, in altri è un errore. Bridge Design indica esplicitamente «0 = auto» per i parametri che lo consentono. Quando un campo è temporaneamente incompleto, leggere il messaggio della scheda prima di considerare i risultati.

La finestra segue l'ingrandimento di Windows. I pannelli più stretti si dispongono verticalmente; le tabelle possono mantenere uno scorrimento proprio. I divisori nella composizione dei progetti permettono di allargare l'albero o il catalogo. Cercare prima la barra del pannello che contiene il dato: lo scorrimento della finestra e quello della tabella non sono necessariamente lo stesso comando.

