# Esempio del palo da 20 m con quote di taglio assegnate

Aprire **palo-20m-tagli-12-18-20.programma** con File → Apri. Nella Risposta elastica aprire Tavola armature e distinta ferri. Ogni tratto definisce un gruppo distinto; i confini sono quote finali di taglio. L’esempio serve a leggere la geometria dei ferri e gli esiti, non è un progetto esecutivo verificato.

## Input condivisi

Palo D 1,00 m, lunghezza 20 m, testa al piano campagna; testa e punta libere nel modello laterale. H=100 kN, eccentricità zero; N testa=600 kN con compressione positiva e peso proprio 25 kN/m³. C35/45, B450C, copriferro 70 mm, staffe Ø10/150 mm; XC2, vita 50 anni. FEM con passo iniziale 0,50 m ed EI dalla sezione integra. Sabbia sciolta 0–4 m (A=200, φ=30°), sabbia media 4–20 m (A=650, φ=34°), γ=18 e γsat=20 kN/m³; falda a 4 m. Modalità Viggiani A·γ/1,35 con peso immerso sotto falda. Buona aderenza, giunti al 100%, barre commerciali massime 12 m.

## Tratti e barre effettive

| Tratto assegnato | Armatura | Inizio barra | Fine barra | Lunghezza di taglio |
| --- | --- | --- | --- | --- |
| T1 0–12 m | 20Ø20, principale | 0,00 m | 12,00 m | 12,00 m |
| T2 12–18 m | 12Ø16, personalizzata | 10,80 m | 18,00 m | 7,20 m |
| T3 18–20 m | 12Ø16, personalizzata | 17,00 m | 20,00 m | 3,00 m |

Al primo giunto governa l’iniziale 60Ø20=1,20 m; al secondo 60Ø16=0,96 m, arrotondato a 1,00 m. I valori richiesti dal verificatore in questo esempio sono inferiori. Il terzo gruppo inizia quindi a 17,00 m: le due sovrapposizioni non sono uguali perché cambiano i diametri. Ogni gruppo entra in una sola barra commerciale da 12, 8 e 6 m rispettivamente: la lunghezza di taglio resta 12, 7,20 e 3 m, senza allungamenti allo stock.

Se un gruppo supera la lunghezza commerciale, vengono creati pezzi interni con ulteriori sovrapposizioni, conservando l’inizio e la fine del gruppo. Quantità longitudinale qui: 20×12 + 12×7,20 + 12×3 = 362,40 m; sfridi esclusi.

## Controlli da leggere

- T1: Parziale: dettagli da completare.
- T2: Non soddisfatto: dettaglio della sezione.
- T3: Non soddisfatto: dettaglio della sezione.

Le corone regolari 20/12 hanno quattro coppie radiali nominali comuni su dodici richieste: il primo giunto segnala l’abbinamento da definire. Le corone 12/12 del secondo giunto sono allineate. I tagli scelti non vengono modificati per risolvere verifiche non soddisfatte. Sviluppo alle estremità, trattenimento delle barre, confinamento, staffe esecutive, SLE e sisma restano da completare. MRd nominale non equivale a dettaglio verificato; dove lo sviluppo o il giunto non sono disponibili, MRd utilizzabile non viene accreditato.

Provare a spostare la fine di T1: la barra superiore termina esattamente alla nuova quota e T2 arretra da quella quota di l₀. Riducendo la lunghezza commerciale si vedono i soli tagli interni necessari.