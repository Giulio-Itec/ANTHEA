## Micropalo verticale

### Dati del bulbo e dell'iniezione

Assegnare diametro di perforazione, iniezione IGU o IRS, famiglia di terreno, coefficiente di espansione α e p_l utilizzato dall'abaco. L'interfaccia espone il campo «Pressione p_i = p_l» e il codice assume quell'uguaglianza. p_l è però la grandezza geotecnica dell'abaco: usare il valore della pompa richiede una giustificazione della correlazione, non la sola coincidenza delle unità. Documentare quindi quale misura o interpretazione abbia prodotto il dato inserito.

Definire l'inizio del bulbo resistente. La lunghezza libera o il tratto escluso non produce resistenza laterale del bulbo. La resistenza viene integrata soltanto nelle tratte attive, considerando il terreno attraversato. L'eventuale contributo di punta è una percentuale del laterale: attivarlo non introduce una verifica autonoma della base.

Inserire l'inclinazione rispetto alla verticale e verificare la lunghezza lungo l'asse. I passaggi di strato sono definiti in profondità verticale e vengono convertiti nella lunghezza percorsa dal micropalo. Controllare quindi sia il profilo del terreno sia il riepilogo delle tratte, soprattutto per inclinazioni elevate.

### Tubo e peso

Le dimensioni del tubo servono al calcolo dell'area di acciaio, del volume di boiacca e del peso. Nel modulo verticale non equivalgono a una verifica automatica di instabilità del tubo, della sezione composta acciaio boiacca o del collegamento in testa. Diametro esterno del tubo, diametro di perforazione e diametro espanso del bulbo sono tre misure diverse.

Verificare che p_l rientri nel campo della curva selezionata: il motore rifiuta l'estrapolazione. Un messaggio di fuori abaco va risolto cambiando la base di calcolo o il dato, non forzando il valore al bordo senza motivazione. Nel report conservare curva, tipo di iniezione, α, pressione e tratte: sono le informazioni che consentono di ricostruire la stima.

