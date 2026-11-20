# Sezione DXF di prova

`sezione-forata-cm.dxf`: disegno ASCII DXF 2000 di nostra creazione, in centimetri. Contorno 60 × 80 con origine CAD (1000, 2000), foro 10 × 20 con angolo inferiore sinistro (1040, 2030), quattro barre Ø2 in (1006, 2006), (1054, 2006), (1054, 2074), (1006, 2074). Layer CONTORNO, FORI, BARRE.

Attesi indipendenti, in mm: area netta 460000; baricentro locale (135000000/460000, 400). Le prove non riscrivono questo file né questi attesi. `ConcreteDxfChecks` costruisce inoltre varianti in memoria per unità, DXF binario, archi, blocchi e dati non validi. La fixture alimenta le prove della finestra e delle relazioni WPF.
