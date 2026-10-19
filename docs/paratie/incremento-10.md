# Incremento 10 — guide globali e aiuto contestuale

## Contenuto

Le due guide globali Rev48 del 19 ottobre 2026 documentano i campi
collaudati degli incrementi 1–9: costruzione della storia, unità della
fascia e del singolo elemento, terreni, sostegni, falda, filtrazione,
verificatori, carichi distribuiti, risultati e applicabilità. Sono
esplicite le funzioni residue; questo incremento non chiude la matrice.

La Wiki contiene due nuovi capitoli, collegati al modulo `geo_paratie`,
con riferimenti ai metodi e una schermata originale. Sette argomenti
contestuali usano `HelpTopics` e il navigatore dei servizi MVVM, anche
negli editor tabellari. F1 senza argomento specifico apre la guida.
Le formule del motore non sono replicate nella presentazione.

## Conservazione e generazione

Sorgenti precedenti conservati integralmente, salvo la riga dell'edizione;
i nuovi capitoli sono aggiunti in coda. Sedici file Rev47, sorgenti e indici
sono catturati e archiviati byte per byte in
`supporto/SUPERATI/paratie-rev48-20261019/`, con origine, sostituzione e
SHA-256 nel manifest. Gli attesi numerici e le catture storiche restano
invariati.

Modello ITEC originale SHA-256
`F6F3F04B0BAFEEA09E4CCA1B19FA74E95FC540C264AA518F3D93DEE9EEBBCD92`.
Il modello sul Desktop non era disponibile: usata la copia identica
conservata negli artefatti della Rev47. Il builder accetta ora percorsi
espliciti tramite `ANTHEA_GUIDE_TEMPLATE` e `ANTHEA_GUIDE_ARTIFACTS`,
mantenendo il controllo dell'impronta. Nessuna modifica al modello.

Pipeline Word nascosta del repository, formule OMML modificabili,
aggiornamento dell'indice e finalizzazione. Il renderer alternativo della
skill Documents non dispone di LibreOffice. La prima esportazione finale
Word della sola teoria non ha prodotto il PDF; ripetizione dedicata
conclusa senza modifiche al contenuto. Evidenze in
`supporto/artefatti/paratie/guide-rev48/`.

## Verifiche eseguite

- Pratica: 89 pagine, 1007 frammenti verificati.
- Teoria: 156 pagine, 2027 frammenti e 365 formule verificate; 20 nuove
  formule. Nessun frammento mancante, struttura matematica conservata.
- Per ciascun DOCX, 26 parti opache del modello conservate byte per byte;
  stili e relazioni originali verificati dal finalizzatore.
- PDF accanto ai Markdown identici ai PDF delle edizioni Word. Copertine,
  indice e piè di pagina riportano Rev48; nessun nome di file intermedio.
- Indicizzazione Wiki: 75 capitoli e 7022 termini; 12 prove del manuale
  superate. Contenuti originali e riferimenti conformi alle regole interne.

Controllo visivo concluso: rasterizzate tutte le 245 pagine e ispezionate
le tavole di insieme; copertine, indici, nuove pagine 85–89 e 150–156
controllate in dettaglio. Verificati anche i quattro PDF degli indici/README
(dieci pagine). Nessuna sovrapposizione o taglio introdotto; impaginazione
dei capitoli preesistenti conservata.

Runner `ci/20261019-143438-paratie-guide48`, stadi build/fast/wiki/ui
filtrati: nove esiti PASS, 0 NEW-FAIL. 541 controlli di architettura,
3206 della presentazione, 40 WPF paratie e 12 prove del manuale.
Pulsante «?» effettivamente attivato, destinazioni dei sette argomenti
risolte nel catalogo, F1 e assenza di mutazioni del documento verificati.
Schermata WPF esaminata. Corsa su modifiche non ancora committate rispetto
a `8db29979`, rimaste invariate durante tutta l'esecuzione.
Nessuna nuova revisione GPC o installazione in `lib/Checker`.

## Ambito residuo

PAR-30 resta in corso: le guide vanno estese con le funzioni ancora aperte.
Restano da completare e collaudare le altre righe della matrice, il seguito
pali/micropali interrati, il rilascio coerente ufficiale e i merge finali.

Riesecuzione dopo il commit f5b3d5d2: `ci/20261019-144824-paratie-guide48-final`, architettura e Wiki filtrate, tre PASS e nessun nuovo fallimento, albero pulito e invariato.
