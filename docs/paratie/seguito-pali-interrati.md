# Incarico in coda: testa di pali e micropali sotto il piano di campagna

Richiesta dell'utente del 19 ottobre 2026, successiva all'autorizzazione dei merge:
aggiungere, dopo il modulo paratie, uno strato di terreno sopra palo/micropalo
mediante un «approfondimento del palo rispetto al piano di campagna», per
portanza verticale e orizzontale. Scopo: includere il terreno sovrastante nelle
tensioni litostatiche. L'utente ha chiesto di rispiegare il comportamento, che
è stato illustrato in chat e confermato esplicitamente dall'utente («perfetto.
metti questa implementazione in coda al modulo di paratie»). L'implementazione
è ancora da iniziare, dopo il modulo paratie. Il merge anticipato del 19/10
per consegnare la candidata testabile non elimina né anticipa questo seguito:
rimane nella coda dei nuovi rami di implementazione.

## Interpretazione operativa

Parametro d≥0: profondità della testa rispetto al piano di campagna. Alla
distanza s dalla testa, la profondità nel terreno è d+s. La lunghezza strutturale
L resta invariata, la punta è alla profondità d+L. La stratigrafia copre anche
il tratto superiore d, eventualmente composto da più strati. Integrare il peso
del terreno dal piano di campagna e sottrarre la pressione interstiziale coerente
con la falda per ottenere la tensione efficace.

La portanza verticale utilizza le nuove tensioni nei metodi che ne dipendono;
l'attrito riguarda soltanto il tratto effettivamente presente dalla testa alla
punta. La risposta orizzontale utilizza profondità e confinamento aggiornati
nelle proprie leggi geotecniche. Un kh assegnato direttamente conserva il suo
significato di dato esplicito. Il terreno superiore non introduce ulteriori
elementi strutturali né un carico assiale automatico in testa.

Esempio indipendente: d=2 m, terreno asciutto omogeneo con gamma=18 kN/m³:
tensione litostatica alla testa 36 kPa. Per d=0 preservare esattamente i casi
esistenti. Censire prima coordinate e convenzioni dei moduli palo verticale,
micropalo e palo orizzontale/elastico, distinguendo capacità e risposta.

## Accettazione da sviluppare

- Contratto unico nelle librerie GPC, con unità e datum espliciti; editor e
  migrazioni ANTHEA, valore iniziale zero per i documenti esistenti.
- Test indipendenti con uno/più strati, falda dentro il ricoprimento, d=0,
  valori non validi e copertura stratigrafica insufficiente.
- Controllo di sigma totale, pressione interstiziale, sigma efficace, azioni
  laterali e resistenze; nessuna estensione implicita di metodi empirici oltre
  il campo documentato.
- Cattura prima/confronto dopo per i moduli esistenti; conservare attesi,
  risultati e vincoli del rilascio coerente.
- Aggiornare guide globali, DOCX/PDF, report e test prima dell'integrazione
  finale autorizzata. Nessuna esclusione dei requisiti paratie è implicita.
