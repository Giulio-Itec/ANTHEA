# Micropalo orizzontale con CHS

Modulo `geo_micropalo_orizzontale`, con workspace condiviso con il palo orizzontale.
Il diametro geotecnico D (m, inizialmente 0,24) governa le reazioni del terreno;
De e t del CHS (mm) governano la sola sezione resistente in acciaio. De < D.
Il catalogo dimensionale è `Chs.Catalogo`, già usato dal micropalo verticale:
non è un catalogo di disponibilità commerciale né assegna la qualità dell'acciaio.
La modalità manuale ammette dimensioni arbitrarie valide, senza cambiare quelle del catalogo.

Proprietà analitiche, con di = De - 2t:
A = π(De²-di²)/4; I = π(De⁴-di⁴)/64; Wel = 2I/De;
Wpl = (De³-di³)/6. Massa = A · 0,00785 kg/m per A in mm².
Npl = A fy/γM0; Mpl = Wpl fy/γM0, con conversioni N→kN e Nmm→kNm.
My(N) = Mpl (1-|N|/Npl): interazione lineare conservativa della sezione.
Il riempimento non contribuisce alla resistenza.

Classificazione secondo i limiti tradizionali EN1993-1-1, tabella 5.2:
D/t ≤ 50ε² (1), 70ε² (2), 90ε² (3), altrimenti 4; ε² = 235/fy.
Per non attribuire duttilità a sezioni non idonee, il ramo automatico è limitato
alla classe 1. Non è una verifica completa della rotazione plastica disponibile.
Per classi successive serve una valutazione specifica: il programma non assegna
automaticamente un momento elastico a un meccanismo che presuppone cerniere plastiche.
fy (inizialmente 355 MPa) e γM0 (inizialmente 1,05) sono input del progettista,
da verificare rispetto a materiale, spessore, norma e situazione di progetto.

Riferimenti di confronto: SCI P362 (EN1993-1-1, resistenza e interazione N-M),
https://www.steelconstruction.info/images/6/6a/SCI_P362.pdf ;
Steel for Life, note alle tabelle interazione:
https://www.steelforlifebluebook.co.uk/explanatory-notes/ec3-ukna/axial-force-bending-tables .
Non si implementano le resistenze migliorate di quelle tabelle: si usa
il criterio lineare conservativo esplicitato sopra.

Limiti: nessuna verifica di instabilità globale, taglio, giunti, corrosione,
connessioni, spostamenti o risposta ciclica. Valgono inoltre i limiti del motore
Broms e dell'estensione multistrato descritti in palo-orizzontale.md.
Il momento manuale resta disponibile con indicazione obbligatoria della provenienza;
non attribuisce conformità normativa automatica.
