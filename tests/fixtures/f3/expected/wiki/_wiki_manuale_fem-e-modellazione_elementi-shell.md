## Elementi Shell

Una shell descrive una superficie strutturale con comportamento membranale e flessionale. È utile per solette, pareti e piastre quando lo spessore è piccolo rispetto alle dimensioni nel piano e quando la variazione delle azioni nella superficie è decisiva. La superficie media sostituisce il volume, mentre lo spessore partecipa alle rigidezze di membrana e flessione in modo differente.

### Membrana e flessione

Le risultanti membranali sono forze per unità di lunghezza; le risultanti flessionali sono momenti per unità di lunghezza. Non importare un momento di piastra espresso in kNm/m come momento totale di una trave. Occorre dichiarare la striscia resistente e integrare il risultato sul tratto interessato. La somma delle azioni sulle strisce deve restare coerente con equilibrio e percorso del carico.

### Assi e connessioni

La normale locale distingue le facce superiore e inferiore. Normali invertite possono cambiare la lettura dei segni senza cambiare la fisica. Controlla la continuità della mesh, le connessioni con beam e la rappresentazione degli appoggi. Un bordo vincolato su tutte le rotazioni può irrigidire artificialmente una soletta appoggiata.

### Errori e limiti

Un carico puntuale o un vincolo puntuale possono generare picchi che crescono al raffinarsi della mesh. La convergenza della reazione globale non garantisce la convergenza della tensione puntuale. Rappresenta la superficie di contatto reale quando la verifica locale lo richiede. Una shell non sostituisce automaticamente un modello tridimensionale dei nodi massicci.

### Riepilogo

Scegli la shell quando devi descrivere distribuzioni nella superficie; mantieni espliciti assi, spessore, unità delle risultanti e trasferimento ai dettagli. Anthea non dispone di un solutore shell generale: questa pagina aiuta a interpretare dati provenienti da un modello esterno.

### Controllo delle unità e fonte

Se il momento di piastra è uniforme e pari a 12 kNm/m, una striscia larga 2,5 m porta 30 kNm. Per una distribuzione variabile occorre integrare lungo la striscia. Le componenti di membrana vanno distinte da quelle flessionali; le convenzioni specifiche vanno lette nella documentazione del solutore di origine. Un carico concentrato può rendere la tensione locale dipendente dalla mesh.

