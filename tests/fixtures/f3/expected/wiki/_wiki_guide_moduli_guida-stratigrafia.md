## Stratigrafia del terreno

Il modulo autonomo Stratigrafia del terreno si apre dal catalogo Geotecnica e può essere aggiunto come scheda a un progetto. Gestisce un profilo di strati orizzontali; non esegue verifiche geotecniche. Il comando Invia / importa terreno da ANTHEA permette di riutilizzare i dati negli altri fogli compatibili.

Assegnare nome, descrizione e, se disponibile, quota altimetrica del piano campagna. Le profondità sono espresse in metri e crescono verso il basso a partire dal piano campagna. La quota altimetrica è un dato distinto e facoltativo.

Inserire gli strati dall’alto verso il basso con + Strato. Duplica crea uno strato indipendente; Elimina conserva almeno uno strato; le frecce cambiano l’ordine. Selezionare uno strato nell’elenco o nel profilo per modificarne nome, descrizione litologica, colore e spessore. Solo l’ultimo strato può essere esteso indefinitamente: per duplicarlo o spostarlo occorre prima renderlo finito e assegnargli lo spessore corretto. Un nuovo strato viene inserito prima dello strato indefinito.

Per ciascuno strato sono disponibili γ e γsat in kN/m³, φ′ in gradi, c′ e cu in kPa. Lasciare vuoti i parametri non disponibili: zero è un valore distinto. Registrare provenienza, qualificazione e note; se i singoli parametri hanno qualificazioni diverse, specificarlo nelle note. Il programma non attribuisce valori in base alla litologia e non applica coefficienti parziali.

Per la falda scegliere Non nota, Non considerata oppure Assegnata. Nel terzo caso inserire la profondità, nulla al piano campagna o positiva al di sotto. La rappresentazione ammette un solo livello; non descrive falde sospese o condizioni artesiane. Un valore eventualmente conservato nel campo profondità è inattivo quando la falda non è assegnata.

Salva conserva il documento autonomo o il progetto. I parametri incompleti generano avvisi e non impediscono il salvataggio; geometria invalida, numeri non validi e falda assegnata senza profondità devono essere corretti. Il grafico evidenzia lo strato selezionato e la falda; la profondità visualizzata di uno strato indefinito non ne rappresenta il limite fisico. Report Word produce la scheda dei dati in DOCX e PDF affiancati, anche senza calcoli.

### Famiglie di terreno e retini

Il menu Famiglia prevalente propone Non classificato, Riporto, Ghiaia, Sabbia, Limo, Argilla, Terreno organico, Roccia e Altro. Per terreni misti scegliere la componente prevalente e conservare la denominazione completa: ad esempio Sabbia per una sabbia limosa. Il colore resta modificabile separatamente.

Il profilo usa punti per Sabbia, ciottoli per Ghiaia, tratti orizzontali brevi per Limo e continui per Argilla, crocette per Riporto, simboli vegetali per Terreno organico, tratti diagonali per Roccia e piccoli segni a più per Altro. Non classificato conserva il solo colore. I retini distinguono le famiglie anche senza colore e mantengono dimensione costante sullo schermo. Il nome della famiglia appare accanto allo strato e nella scheda dati. Sono convenzioni grafiche di ANTHEA: scegliere una famiglia non assegna parametri, falda o spessore.

L’importatore AI propone la famiglia solo quando la componente prevalente è riconoscibile nella descrizione della relazione, lasciando Non classificato nei casi incerti. Il menu permette di correggere la proposta in anteprima; non è una classificazione granulometrica automatica.

### Valori medi e minimi

I valori medi costituiscono l’insieme principale. Attivare Gestisci anche i valori minimi nello strato per inserire i minimi eventualmente forniti dalla relazione geologica; questi campi sono opzionali e non vengono riempiti copiando i medi. Le due colonne hanno fonte e qualificazione distinte. La rimozione dei minimi richiede conferma.

La scelta Parametri da usare nelle verifiche ammette Medi, Minimi oppure Entrambi. La scelta è salvata insieme alla geometria comune e riportata nella scheda dati. Non avvia calcoli: nei moduli con un solo insieme di parametri il trasferimento richiede la scelta esplicita Medi o Minimi; per entrambi creare due fogli distinti. I medi pertinenti saranno comunque richiesti dal modulo di verifica; scegliendo Minimi o Entrambi saranno richiesti anche i corrispondenti minimi. Non occorre compilare cu per una verifica che richiede soltanto parametri efficaci. Le bozze incomplete possono essere salvate, con avviso. Un minimo superiore al medio richiede controllo della fonte e del significato del dato.

### Scambio del terreno fra fogli ANTHEA

Creare un foglio Stratigrafia distinto per ciascun sondaggio o profilo e assegnargli un nome riconoscibile. Aprire Invia / importa terreno da ANTHEA, scegliere il foglio sorgente nella sezione Da un altro foglio del progetto e, se presente, il sondaggio oppure Monte/Valle. Importa dal foglio selezionato carica soltanto l’anteprima: Sostituisci il terreno del foglio corrente richiede conferma prima di applicare i dati. In alternativa scegliere un modulo e Invia a un nuovo foglio. Nel progetto il nuovo foglio viene aggiunto alla sezione corrente; da un modulo autonomo si apre una nuova finestra.

Lo scambio è disponibile per Stratigrafia, palo verticale, palo orizzontale, micropalo orizzontale, muri e fondazioni. Il micropalo verticale con schema specifico non è incluso. La copia è indipendente: modificare la sorgente non aggiorna le destinazioni. Salva terreno e Carica terreno consentono anche il riutilizzo mediante file terreno ANTHEA.

Fra due fogli Stratigrafia si conservano geometria, falda, medi e minimi, fonti, descrizioni, colori e retini, anche con dati incompleti o ultimo strato indefinito. Per inviare a un modulo di verifica scegliere Medi oppure Minimi. La scelta non copia i medi nei minimi mancanti. Specificare nello strato il comportamento Granulare o Coesivo: non viene dedotto dalla famiglia grafica. Definire la falda e spessori finiti; se l’ultimo strato è indefinito, predisporre una copia con l’estensione motivata per la verifica. Non vengono prolungati automaticamente i sondaggi.

I dati mancanti restano vuoti e i moduli mantengono i propri controlli di compatibilità. Completare i parametri specifici, geometria e azioni nel foglio di destinazione. Le profondità mantengono lo stesso riferimento: nessuna traslazione automatica della quota. Nel trasferimento dai vecchi moduli a Stratigrafia verificare la qualificazione dell’insieme unico riportato nei campi principali; minimi e litologia non vengono ricostruiti.

### Importazione assistita della relazione

Il pulsante Importa relazione PDF con AI è attualmente visibile ma disattivato, in attesa della configurazione e della gestione della fatturazione aziendale. Lo scambio interno fra fogli ANTHEA resta disponibile senza servizi AI. La procedura seguente descrive la funzionalità predisposta per una successiva attivazione.

Aprire Importa relazione PDF con AI, selezionare un PDF fino a 20 MiB e inserire il nome di un modello OpenAI compatibile con PDF e output strutturato e la propria chiave API. I campi possono essere predisposti tramite le variabili di ambiente ANTHEA_AI_MODEL e OPENAI_API_KEY. La chiave non viene salvata nei documenti ANTHEA.

Il comando Invia PDF a OpenAI e prepara bozze trasmette l’intero documento al servizio remoto e può comportare costi sul proprio account API. Non effettua elaborazione AI locale. Interrompi annulla l’attesa; nessun dato viene applicato automaticamente alla scheda aperta. Errori del servizio o risposte incomplete richiedono una nuova richiesta, eventualmente con un estratto più breve della relazione.

Selezionare il sondaggio o profilo dall’elenco, controllare gli avvisi e correggere la bozza nell’editor. Profili differenti restano separati e se ne applica uno alla volta. Le fonti riportano per parametro le pagine PDF indicate dal modello: confrontarle con il documento originale. I valori non riconosciuti come medi o minimi restano nelle note da classificare. Dati assenti, geometrie ambigue e unità non determinabili non devono essere completati per supposizione; l’estrazione AI può contenere errori.

Dopo il confronto con la relazione, selezionare Ho confrontato dati, unità e pagine della bozza con la relazione e applicare il profilo revisionato. Ogni modifica o cambio di profilo richiede di ripetere questa attestazione. Il comando sostituisce la stratigrafia della scheda aperta; chiudere la finestra conserva i dati precedenti. Geometrie e valori invalidi impediscono l’applicazione; i parametri mancanti restano una bozza salvabile. Il documento conserva nome e impronta del PDF, modello, data, fonti e avvisi; non incorpora il PDF originale. La scheda Word/PDF espone medi, minimi e provenienza dell’importazione.

