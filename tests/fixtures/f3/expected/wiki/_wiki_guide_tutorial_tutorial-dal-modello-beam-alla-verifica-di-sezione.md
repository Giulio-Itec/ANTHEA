## Tutorial dal modello Beam alla verifica di sezione

Questo percorso unisce teoria, controllo manuale e utilizzo del software senza confondere analisi globale e verifica locale. Richiede circa venti minuti e usa solo strumenti presenti in Anthea.

### Capisci e controlla

Apri [Elementi Beam](/wiki/manuale/fem/elementi-beam#esempio-concettuale). Disegna la trave appoggiata, indica L = 8 m e q = 25 kN/m e calcola due reazioni da 100 kN e un momento di 200 kNm. Il carico è già assegnato come azione di progetto dell'esempio. Controlla separatamente che 25 kN/m equivalga a 25 N/mm.

### Calcola e modifica

Usa Apri esempio in Anthea. Nella Sezione in c.a. verifica la terna 0, 200, 0 e le dimensioni 600 × 800 mm. Leggi il dominio della combinazione attiva, poi modifica l'altezza e ripeti l'analisi. Conserva le azioni per isolare l'effetto della geometria. Inverti il segno del momento per osservare il ruolo delle armature superiori e inferiori.

### Osserva e archivia

Confronta i due risultati descrivendo quale ipotesi è cambiata e quale è rimasta assegnata. Non usare il controllo di resistenza per concludere sulla freccia o sulla fessurazione: richiedono analisi e combinazioni proprie. Salva il documento modificabile e annota il riferimento alla pagina Wiki. La [guida Sezione in c.a.](/wiki/guide/moduli/sezione-ca) descrive input, default e problemi frequenti.

