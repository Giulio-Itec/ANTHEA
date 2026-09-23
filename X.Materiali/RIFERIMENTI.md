# Riferimenti del modulo Calcestruzzo

## PDF forniti
- C:/Users/g.sciarpa/Desktop/Norme/CLS/en.1992.1.1.2004.pdf: EN 1992-1-1:2004 (edizione BS nel documento).
- C:/Users/g.sciarpa/Desktop/Norme/CLS/UNI-EN-206.pdf: UNI EN 206-1:2006, non una generica edizione corrente EN 206.

## Copriferro
EN 1992-1-1:2004 §4.4.1, pagine stampate 49-52 (PDF 51-54).
- Formula 4.1: cnom = cmin + Δcdev.
- Formula 4.2: massimo fra aderenza, durabilità corretta e 10 mm.
- Tabella 4.2: per barre isolate cmin,b = diametro; +5 mm se Dmax >32 mm.
- Tabella 4.3N: base S4/50 anni; +2 classi per 100 anni. Riduzioni facoltative per resistenza, geometria a piastra e controllo speciale della produzione; minimo S1. La soglia di resistenza di XD2 è C40/50, quella di XS2 è C45/55, nonostante la stessa colonna nella tabella 4.4N. La deroga per aria >4% non è applicata.
- Tabella 4.4N: valori raccomandati per acciaio ordinario, con selezione del maggiore contributo fra esposizioni concomitanti.
- Tutte le correzioni Δcdur sono poste a zero. Δcdev predefinito 10 mm; riduzioni richiedono motivazione del controllo di esecuzione.
- §4.4.1.2(11)-(13): superfici irregolari +5 mm; strato opzionale per abrasione XM1/XM2/XM3 di 5/10/15 mm.
- §4.4.1.3(4): cnom almeno 40 mm su terreno preparato (incluso magrone), 75 mm per getto diretto sul terreno.

Ambito: calcestruzzo normale, armatura ordinaria e barre isolate. Esclusi precompressione, fasci, riduzioni per inox/protezioni e verifica al fuoco. Verificare ogni barra/superficie, misurando il copriferro fino all'armatura più vicina, comprese le staffe. Non si determina la distanza dell'asse della barra principale.

XF/XA richiedono attenzione alla composizione: non sono convertite in una classe fittizia della tabella 4.4N. Se manca una classe X0/XC/XD/XS, il copriferro non viene calcolato. X0 esclude altre esposizioni.

## Esposizione e composizione
UNI EN 206-1:2006:
- Prospetto 1, pagine 11-12 (PDF 18-19): classi di esposizione e combinazioni.
- Prospetto 2, pagine 12-13: la selezione XA presuppone analisi chimica; il modulo non determina XA da concentrazioni.
- §3.1.29-31, pagine 8-9 (PDF 15-16): acqua totale, acqua efficace e rapporto in massa a/c.
- Prospetto 3, pagina 13 (PDF 20): S1 10-40, S2 50-90, S3 100-150, S4 160-210, S5 ≥220 mm. Il confronto con un valore di riferimento non applica le tolleranze di conformità delle prove. Metodi Vébé, compattabilità e spandimento non implementati.
- Prospetto 10, pagina 20 (PDF 27): Cl 0,20 e Cl 0,40 per armatura ordinaria. Scelta soggetta alle disposizioni nel luogo d'impiego; non viene verificato il contenuto reale di cloruri.
- Appendice F e prospetto F.1, pagine 57-58 (PDF 64-65): informativi, riferiti a 50 anni, CEM I e Dmax 20-32 mm. La classe minima di resistenza è riferita alla relazione ottenuta con cemento 32,5.
- Per esposizioni concomitanti si prende il minore limite a/c e il maggiore minimo di cemento, classe e aria. I valori restano riferimenti indicativi fuori dalle ipotesi di F.1.
- XF2/3/4: aria 4%, con alternativa prestazionale della nota a; per tutti gli XF occorre anche resistenza al gelo degli aggregati.
- XA2/3: se l'aggressività deriva dai solfati, la nota b richiede cemento resistente ai solfati, ad alta resistenza per XA3.

Acqua efficace = totale - assorbita. a/c = acqua efficace / cemento. Il contributo k delle aggiunte non è applicato. Dosaggi, designazione cemento, additivi, aggregati e consistenza sono dati della miscela, non ricavati dalla sola classe resistente. Nessun dimensionamento automatico della miscela o esito globale di conformità.

## Controlli eseguiti
- Proprietà del calcestruzzo C30/37 e intervallo delle classi esistenti.
- Copriferro per XC1, XC4 e combinazione XS3+XF4, vita utile 100 anni, Dmax ai due lati della soglia 32 mm, getto su terreno, rugosità e abrasione.
- Soglie di riduzione strutturale XD2/XS2 e limite S1.
- Esclusione X0 combinato; nessun risultato con sole XF/XA o valori non finiti.
- Acqua efficace, rapporto a/c sotto/sopra limite, rifiuto di acqua assorbita maggiore della totale.
- Aggiornamento delle tre schede e controllo visivo delle anteprime a due larghezze.

## Resistenza di aderenza
EN 1992-1-1:2004 §8.4.2, eq. 8.2 (PDF 135-136): fbd = 2,25 η1 η2 fctd.
fctd = αct fctk,0.05 / γc (§3.1.6). Nel calcolo di aderenza fctk,0.05 è limitata a C60/75.
η1 = 1 per buone condizioni accertate secondo figura 8.2; 0,7 negli altri casi (predefinito).
η2 = 1 per diametri fino a 32 mm; (132-φ)/100 per diametri maggiori.
Diametro condiviso con il copriferro. αct iniziale 1; γc iniziale 1,5, modificabili.
Ambito: barre nervate, calcestruzzo normale, azioni prevalentemente statiche. La resistenza di aderenza non è una verifica della lunghezza di ancoraggio.
Controlli: C30/37 φ16 in buona aderenza fbd = 3,041291 MPa; riduzioni η1/η2 per φ40; limite C60/75 per C90; invalidazione di γc nullo.

## Copriferro NTC + Circolare (criterio predefinito)
Questa sezione integra il criterio EC2 descritto sopra: il blocco con sole XF/XA vale esclusivamente in modalità EC2.
Fonti locali: Norme/NTC/NTC_2018.pdf, pagina PDF 80; Norme/NTC/CIRC_2019.pdf, pagina PDF 91.
NTC tabella 4.1.III: ordinario X0/XC1/XC2/XC3/XF1; aggressivo XC4/XD1/XS1/XA1/XA2/XF2/XF3; molto aggressivo XD2/XD3/XS2/XS3/XA3/XF4.
Circolare §C4.1.6.1.3 e tabella C4.1.IV: barre da c.a.; scelta fra piastra/soletta/parete e trave/pilastro. Soglie tabellari Cmin 25/30/35 MPa e C0 35/40/45 MPa secondo la severità.
Cmin pertinente può essere specificato in fck: la nota alla tabella rinvia alla classe minima della pertinente esposizione UNI EN 206:2016. Campo vuoto significa uso della soglia tabellare, dichiarata nei risultati, non certificazione dei requisiti della miscela.
Correzioni: +10 mm per 100 anni, +5 mm sotto Cmin, -5 mm per controllo qualità che include la verifica dei copriferri, quindi tolleranza di posa.
Si conserva il massimo con il requisito di aderenza e si applicano esplicitamente i dettagli integrativi EC2 per Dmax, superficie, abrasione e terreno. Le riduzioni di classe strutturale EC2 non intervengono nel ramo NTC.
Confronti di miscela F.1 ancora riferiti all'edizione fornita UNI EN 206-1:2006, distinti dal criterio NTC del copriferro.
Verifiche superate: XF2/C30/37/50 anni/Δ10 => 40 mm piastra, 45 mm trave; C40 => 40 mm trave; C25 con Cmin tabellare => 50 mm; vita 100 anni => 55 mm; controllo copriferri => 40 mm. Combinazioni, sole XA, diametro governante, terreno, aggiornamento al cambio NTC/EC2 e invalidazione dei dati errati verificati.

## Menu tolleranza di posa
Il campo libero e la motivazione testuale sono sostituiti da condizioni di controllo selezionabili:
- Ordinario: 10 mm, valore fisso.
- Sistema di controllo qualità con misure dei copriferri: valori da 5 a 10 mm.
- Misure molto accurate e scarto degli elementi non conformi: valori da 0 a 10 mm.
Gli intervalli sono quelli EC2 §4.4.1.3, richiamati nel quadro della Circolare §C4.1.6.1.3. Il menu offre passi interi di 1 mm come scelta dell'interfaccia; la norma definisce gli intervalli. A ogni cambio di condizione il valore torna a 10 mm; una riduzione è quindi sempre esplicita.
Verificato aggiornamento con 5 e 0 mm e ritorno a 10 mm in modalità ordinaria. La riduzione del minimo tabellare per controllo dei copriferri resta un parametro distinto.
