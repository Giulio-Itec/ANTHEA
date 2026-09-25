# Curve Nq - NQ-2026-09-09

Fonte: immagini e parametri forniti dall’utente il 09/09/2026. La didascalia distingue D <= 80 cm e D > 80 cm e motiva il fattore Nq* ridotto per grandi diametri con la mobilitazione della punta a cedimenti maggiori. Non è disponibile la citazione bibliografica completa.

- `medio.png`, `grande.png`: originali invariati; SHA256 in fonte_e_coefficienti.json.
- `digitizzazione.json`: punti pixel, punti per fit e controllo, coefficienti utente e adattati, scarti. Intervalli alternati, non campioni indipendenti della fisica del problema.
- `punti_controllo.csv`: punti leggibili anche senza il programma.
- `confronto_risultati.json`: effetto sui casi di regressione rispetto a Raffinata.

La parametrizzazione per i medi conserva gli ancoraggi φ10/φ100 dell’utente. Per i grandi usa una base cubica a tratti, continua C2 a 34° e 38°. I coefficienti runtime sono in programma/nq.py. Non serve NumPy per usare il programma.

Le statistiche misurano lo scarto dalla scansione. Non attestano validità normativa, fisica o accuratezza dei parametri geotecnici. Il rapporto z/D è interpolato logarithmicamente come in precedenza; geometricamente per le ordinate Nq dei medi, aritmeticamente per Nq* dei grandi. Questa regola non è dimostrabile dalle sole tracce della fonte.

Limiti per i medi (porzioni visibili, conservativi): L/D=5, φ 23.4-38.8; 10, 23.6-40.0; 20, 23.6-41.0; 50, 24.8-41.6. Grandi: L/D 4-32, φ 26-42. Fuori campo si usa il bordo con avviso, non una estrapolazione. L’export registra i valori adottati per ciascuna curva.
