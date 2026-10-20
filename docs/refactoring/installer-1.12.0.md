# Chiusura ANTHEA 1.12.0 — 20 ottobre 2026

## Ambito e provenienza

Persistenza nativa GPC delle sezioni c.a., dodici wizard, catalogo Esempi con
52 documenti in 15 moduli e guide globali Rev55. Set ufficiale AB da Checker
894892c1, pubblicato con autorizzazione dell’utente; 13 DLL distribuite.
La nuova richiesta dell’albero Paratie viene sviluppata dopo questo setup.

## Collaudo completo

`ci/20261020-145310-rilascio-1.12.0-full`, commit pulito 2a7740a7:
157 PASS e 15 NEW-FAIL. Gli esiti originali sono conservati. Nessuna lista
di fallimenti ammessi, tolleranza numerica o fixture indipendente modificata.
Il manifest GPC è 77966D25FE066599215B102B0DDF711C9AD5D3C30608BD189A5A3EF82F9E83D9.

Due rilievi hanno una causa applicativa/documentale identificata:

- Relazioni sintetiche c.a.: per alcuni wizard le righe delle coordinate e
  il paragrafo separato di salto pagina producevano una terza pagina.
  Tutte le coordinate sono conservate; soltanto nei wizard sono separate
  da un punto mediano nella stessa cella. L’inizio delle combinazioni usa
  `pageBreakBefore` sull’intestazione, senza paragrafo vuoto separato.
- Equivalenza F3: 155 differenze della Wiki dopo persistenza, wizard ed Esempi.
  Il registro editoriale contiene ora 257 voci: rimosse dieci posizioni di
  ricerca superate e aggiunte 145 differenze esatte. Tutte le trenta ricerche
  conservano la sequenza degli altri articoli, rimuovendo soltanto i tre ID
  modificati. Nessun risultato di calcolo coinvolto, transizioni F4 immutate.

Le altre tredici prove richiedono diagnosi o ripetizione. Trend Micro registra
esplicitamente l’interruzione dell’esportazione del palo verticale. Altre prove
riportano errori di scrittura con «Spazio su disco insufficiente» o attestazioni
finali mancanti; non sono automaticamente attribuite all’antivirus. La prova
Stratigrafia segnala inoltre il limite di larghezza della vista stretta.
L’utente ha comunicato di non poter far intervenire ora l’amministratore.
Questa risposta non viene interpretata come autorizzazione a dichiarare PASS
le prove interrotte o ad estendere la deroga della versione 1.11.0.

Le fonti integrali e i confronti sono in
`supporto/artefatti/rilascio-1.12.0/` (report-fix, f3-diagnosi,
diagnostica-protezione). Le catture antecedenti sono conservate integralmente.

## Correzioni circoscritte del 20 ottobre

La correzione del salto pagina si applica soltanto ai nuovi wizard. I report
storici conservano anche la struttura dei paragrafi. Il primo collaudo della
correzione supera 475 controlli Reports: 24 sintesi di due pagine, con contenuto
integralmente identico salvo il separatore delle coordinate. Catture e confronto
sono conservati in `report-fix/confronto-coordinate-complete.json`.

La ripetizione `ci/20261020-155439-rilascio-1.12.0-diagnosi-ui` supera nove delle
dodici suite ripetute (oltre alle tre build). Gli errori di scrittura non si
ripresentano. Il palo verticale, fermato da Trend Micro, non è ripetuto.
Restano tre rilievi con causa individuata, per i quali sono preparate correzioni:

- Stratigrafia: Width = 730 DIP, ActualWidth = 729,6, extent = 761,6,
  viewport = 700 e DPI = 1,25. Il test mantiene Width >= 730 e ammette soltanto
  mezzo pixel fisico nell’ActualWidth, oltre a verificare lo scorrimento.
- Palo orizzontale: il disegno MVVM eredita il layout rounding dal contenitore.
  A 125%, 1750 × 650 diventa 1750,4 × 649,6 DIP. Il driver delle display list
  disattiva temporaneamente il rounding durante la cattura a dimensioni esatte,
  ripristinandolo subito dopo. Baseline, hash attesi e rendering applicativo
  restano invariati. Dodici disegni sono coinvolti, non i risultati numerici.
- Sezione storica: il wizard aveva esteso la disabilitazione del gruppo foro
  anche alla T ordinaria, dove il gruppo era soltanto nascosto. Si limita la
  disabilitazione alle tipologie avanzate, conservando lo stato storico delle
  tre dimensioni del foro. Sono trenta righe di stato, nessuna differenza nei dati.

Il collaudo conclusivo e l’audit del setup devono essere registrati prima della
pubblicazione. Le modifiche successive della capsula e dell’albero Paratie sono
attività separate dal contenuto di questo installer.
