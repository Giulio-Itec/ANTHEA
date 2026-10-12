# SEZ-1 — inventario e riuso delle sezioni

13 ottobre 2026. STG-3 concluso con 22f6a1a; confronto reale stage ancora aperto in STG-4. Questa attività precede F3.

## Dati e API esistenti

Il pacchetto salvato del modello Spalla conserva UNIT/SECT/SECTIONALL e gli attributi originali. Il campione SECT contiene Baggioli (quadrato 1,2 m) e Fittizio (quadrato 0,001 m), entrambi centrati. La sezione composta illustrata dall'utente è un caso sintetico di collaudo separato: anima 1960 × 15 mm, ali 600/800 × 20 mm, soletta 3600 × 280 mm. Non è dichiarata presente nel campione reale.

Model espone già ISectionShape, SectionAvailability, SectionCurveOutline con fori/isole, SectionBuiltUp, SectionH, SectionRectangular e mesh Delaunay gestite. Nessuna nuova formula di area, inerzia, baricentro o sezione omogeneizzata in ANTHEA. Converter condivide il parser delle sezioni fra importazione completa e catalogo; SectionShapeInspection riusa PropertyMapper e le geometrie delle librerie.

Commit Model 7fd1fe4c: Converter 2.2.0.0, Model 5.2.0.1, ModelData 0.0.2.11, Persistence 3.0.0.4. Checker 75e9adaa: revisioni tecniche dipendenti. Prima 158 test, dopo 167; intero Model 1234 PASS e 2 skip nativi noti. Candidato coerente ancora in collaudo, non distribuito.

## Contratto della vista

- Un catalogo senza WPF traduce i dati conservati e le API GPC in contorni, proprietà con origine/disponibilità, parti, punto di inserimento e diagnostica. Un'unica istanza per finestra alimenta anteprima e volumi.
- ViewModel separato con scelta della sezione, comandi di selezione/isolamento degli elementi associati e interruttori di quote, assi, baricentro e punto di inserimento.
- Finestra WPF richiamabile dall'albero o da un'asta selezionata; dimensioni reali in mm, tabella dei dati importati, materiali e rapporti elastici conservati. Finestra ridimensionabile nei tre temi.
- Estrusione per contorni reali, compresi fori; nessun riempimento a ventaglio per profili concavi. Mesh grafica distinta dalle mesh numeriche delle sezioni.
- Offset non interpretabile distinto da zero. Offset di estremità non normalizzato: conserva asse e avviso. Per composte, il baricentro fisico non sostituisce quello elastico; riferimento centrale verticale non risolto, riferimento superiore/inferiore disponibile quando univoco.

## Perimetro esplicito

Sezioni usuali già supportate dal convertitore e composte I/soletta centrate senza raccordo verticale o irrigidimenti. Altri profili, sezioni numeriche, rastremazioni e variazioni per fase conservano i dati originali e dichiarano la disponibilità effettiva. Nessun materiale resistente o modulo elastico inventato. La vista è di ispezione: non modifica la sezione del modello né avvia verifiche.

Le prove devono coprire unità, segni degli offset, fori, sezioni concave, corrispondenza 2D/3D, forme non supportate, tre temi, selezione/isolamento e riapertura. Guide globali e PDF da aggiornare con SEZ-4. Nessun atteso indipendente viene rigenerato.
