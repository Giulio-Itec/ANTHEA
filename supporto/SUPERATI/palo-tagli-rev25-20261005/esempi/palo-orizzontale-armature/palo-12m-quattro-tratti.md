# Esempio del palo con quattro tratti di armatura

Aprire **palo-12m-quattro-tratti.programma** con File → Apri in ANTHEA. La scheda iniziale è Risposta elastica · trave su molle; attendere il ricalcolo automatico. Aprire **Tavola armature e distinta ferri → Apri tavola armature** per il disegno ingrandito.

Questo esempio didattico mostra la relazione fra input condivisi, armature per tratto, sollecitazioni e tavola. I parametri e le azioni sono valori assegnati per l’esempio, senza riferimenti a un sito o a un’opera reale. Non è un progetto esecutivo verificato.

## Dati impostati

| Dato | Valore |
| --- | --- |
| Palo | D 1,00 m; lunghezza infissa e totale 12,00 m; tratto libero 0 |
| Vincoli | Rotazione in testa libera; punta libera nel modello laterale |
| Carico orizzontale | H = 100 kN; eccentricità 0 m |
| Carico assiale | N testa = +600 kN, compressione positiva; peso proprio incluso lungo il palo |
| Materiali | Calcestruzzo C35/45; acciaio B450C; coefficienti unitari disattivati |
| Sezione principale | 16 Ø24; staffe Ø10/150 mm; copriferro 70 mm |
| Durabilità | Esposizione XC2; vita 50 anni; nessun override del copriferro minimo |
| Peso c.a. | 25 kN/m³, già comprensivo dell’acciaio |
| FEM | Passo iniziale 0,50 m; EI dalla sezione integra lorda e dal materiale |
| Falda | 4,00 m sotto il piano campagna |

La falda si trova a **4 m sotto il piano campagna**. In questo esempio testa e piano campagna coincidono; x e z hanno quindi la stessa origine.

## Stratigrafia

| Strato | Quote | Addensamento | Parametro A | Pesi γ / γsat | Attrito φ |
| --- | --- | --- | --- | --- | --- |
| Sabbia sciolta | 0–4 m | Sciolto | 200, media di 100–300 | 18 / 20 kN/m³ | 30° |
| Sabbia media | 4–12 m | Medio | 650, media di 300–1000 | 18 / 20 kN/m³ | 34° |

Modalità A·γ/1,35 da tabella 14.5 nella trattazione di Viggiani: γ naturale sopra falda, γ′ = γsat − 9,81 sotto falda. A è adimensionale. La legge adottata è kh = nh·z/D, con z globale dal piano campagna. Le medie iniziali sono una convenzione del software. Aprire le tabelle dalla riga dello strato per confrontare intervalli, valori bibliografici e valore adottato.

## Armature per tratto

| Tratto | Quote | Armatura longitudinale | Collegamento |
| --- | --- | --- | --- |
| T1 | 0–3 m | 16 Ø24 | Sezione principale |
| T2 | 3–6 m | 16 Ø24 | Sezione principale |
| T3 | 6–9 m | 8 Ø24 | Personalizzata |
| T4 | 9–12 m | 8 Ø24 | Personalizzata |

Le staffe sono Ø10/150 mm. Otto barre Ø24 proseguono dalla testa alla punta attraverso tutti i tratti; le altre otto vengono interrotte oltre il cambio teorico a 6 m, dopo l’ancoraggio e la traslazione della domanda. Non si duplicano le barre comuni e non servono giunti al cambio di quantità. Le marche B identificano i pezzi reali; le marche S le quattro zone di staffatura.

Lunghezza commerciale massima 12 m; preferenze 6/8/10/12 m. Sovrapposizione iniziale 60φ: per Ø24 si arrotonda 1,44 m a 1,50 m. Il calcolo adotta almeno la lunghezza richiesta dalle verifiche disponibili. Buona aderenza assegnata; percentuale sovrapposta 100%. Sviluppi disponibili oltre testa e punta entrambi **0 m**: le estremità restano da dettagliare.

## Risultati di riferimento

- Spostamento della testa: 0.00649281 m.
- Massimo assoluto del momento: 292.005 kNm, a x = 4.70694 m.
- Massimo assoluto del taglio: 100 kN.

- T1: Parziale: dettagli da completare.
- T2: Parziale: dettagli da completare.
- T3: Non soddisfatto: dettaglio della sezione.
- T4: Non soddisfatto: dettaglio della sezione.

In T3 e T4 la disposizione 8 Ø24 non soddisfa il controllo di interasse longitudinale delle regole Pilastro adottate. È lasciata esplicitamente visibile per mostrare come leggere un esito non soddisfatto: selezionare il tratto e Dettagli pilastro / trave nel riepilogo, oppure aprire il verificatore c.a.

Le azioni sono dichiarate di progetto per l’esempio; è confermata l’ipotesi di taglio circolare equivalente con staffe chiuse a 90°. Restano da completare disposizione delle staffe a trattenimento delle barre, dettagli di estremità, sagomature/ganci, confinamento dei giunti e verifiche escluse dal modello (SLE e sisma). I rami MRd nominali non certificano tali dettagli. La distinta delle staffe fornisce quantità e passo; la loro lunghezza di taglio è da definire.

## Prove da fare nell’interfaccia

- Modificare H nella scheda principale: i diagrammi e le verifiche si aggiornano.
- Cambiare le barre della sezione principale: T1 e T2 seguono la modifica; T3 e T4 conservano le armature personalizzate.
- Cambiare le barre di T3: MRd e distinta cambiano, mentre i diagrammi N, V e M rimangono invariati.
- Selezionare Distinta ferri o Apri verificatore c.a. sul tratto per approfondire i dati e gli esiti.