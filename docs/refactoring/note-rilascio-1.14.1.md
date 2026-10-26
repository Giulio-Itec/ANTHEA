# ANTHEA 1.14.1 — note di rilascio

Note del 26 ottobre 2026. La 1.14.1 porta la revisione delle scelte di F5 (F5.15), chiusa il 26/10, e le librerie GPC della
release AI. Il numero è quello chiesto dall'utente («fai versione 1.14.1»). Il testo è scritto per chi usa ANTHEA ed è anche
l'annotazione del tag `v1.14.1`, sul commit del setup. Il verbale tecnico del rilascio è in [installer-1.14.1.md](installer-1.14.1.md).

## Novità nell'uso

- **Separatore decimale.** Il menu «Aspetto» ha il sottomenu «Separatore decimale»: «Sistema» (l'impostazione di Windows,
  predefinita), «Virgola» o «Punto». La scelta vale dal prossimo avvio e resta per l'utente del PC. Nella sezione c.a. il
  dettaglio dei domini, la legenda della mappa SLE e i parametri del taglio seguono sempre la stessa scelta (F5-34).
- **Revisioni in sola lettura, una regola unica.** In una revisione archiviata si consultano dati, risultati, report e Wiki,
  ma nulla cambia il documento:
  - scheda scelta, filtri, gruppi aperti, curve mostrate e sezioni del report restano solo in memoria, e la revisione non
    risulta più modificata (F5-17);
  - «Esporta Excel» mostra le sollecitazioni senza «Applica» (F5-18); «Calcola curva» e «Suggerisci campate» sono spenti
    (F5-19, F5-72);
  - in Bridge Design il prezzario si consulta senza «Applica prezzario» (F5-59);
  - restano attivi i pulsanti che spostano soltanto la vista: navigazione della sezione c.a. e del progetto delle armature
    (F5-46), scelta «Sezione X / Sezione Y» della rottura delle fondazioni (F5-11).
- **Finestre di dialogo.** Si aprono con la misura della vista e il contenuto segue la finestra quando la si allarga; ognuna ha
  una misura minima (per esempio il prezzario e i «Valori di calcolo» dei muri) (F5-65).
- **Sotto-schede.** Tornando a una scheda dei fogli la sotto-scheda scelta resta quella, per esempio i risultati del palo
  verticale (F5-67).
- **Progetti.** Il pulsante «?» della barra dei progetti apre l'articolo della Wiki sui progetti (F5-64).
- **Muri.** «Ricalcola», «Calcola globale», «Calcola armature» e «Valori di calcolo…» restano spenti finché il loro lavoro è
  in corso: un secondo clic non avvia un secondo lavoro (F5-73).

## Correzioni

- **Sezione c.a.**
  - Nel dominio 2D SLV con lo stress block e N fuori dominio compare un messaggio di ANTHEA, non più quello della libreria
    (F5-16).
  - Le tabelle SLE con le sollecitazioni uniche nascondono le colonne giuste (F5-35); tutte le famiglie SLE mostrano n e φ
    formattati (F5-36).
  - Legenda della mappa, prima scheda dei dettagli SLE e messaggi della mappa σ/ε, della curva M–χ e della ricerca sono
    allineati allo stato del calcolo (F5-37, F5-38, F5-47…F5-50).
  - Le intestazioni della tabella del taglio non sono più tagliate (F5-66).
- **Risposta elastica del palo.** Con un archivio da migrare lo stato non dice più «in coda» (F5-69); quando la quota scelta si
  perde, la sezione non mostra più didascalia, staffa e assi della quota vecchia (F5-70).
- **Sezione composta acciaio-calcestruzzo.** La situazione scelta durante un calcolo si tiene subito; risultati e disegno si
  aggiornano quando il calcolo finisce (F5-71).
- **Bridge Design.** «Annulla» rimette anche il bordo rosso dei campi non validi (F5-72).

## Calcoli

Librerie GPC: release AI. Ogni differenza nei risultati di prova è registrata nel registro delle differenze e approvata
dall'utente.

| DLL | 1.14.0 (AG) | 1.14.1 (AI) |
| --- | --- | --- |
| GPCModel | 5.21.0.0 | 5.21.0.1 |
| GPCModelData, GPC.Converter, GPC.Model.Persistence | 0.3.0.1, 2.6.0.1, 3.3.0.1 | 0.3.0.2, 2.6.0.2, 3.3.0.2 |
| GPCChecker.Concrete | 0.20.0.1 | 0.21.0.0 |
| GPCChecker.Steel | 2.84.0.0 | 2.98.0.1 |
| GPCChecker.CompositeBridge | 1.13.0.1 | 1.15.0.1 |
| GPC.Design | 0.36.0.0 | 0.37.0.1 |
| GPCChecker.Geotechnics, GPCChecker.Masonry | 0.19.0.1, 0.6.0.1 | 0.19.0.3, 0.6.0.3 |
| GPCUtilities, GPCGeometry, DelaunayMesh | 3.0.0.0, 2.3.0.0, 2.0.0.14 | invariate |

- **Pilastri NTC, interasse delle barre (R12).** L'interasse massimo delle barre longitudinali si misura lungo il perimetro
  della gabbia, non più attraverso il nucleo. Di solito l'interasse controllato scende: nel pilastro predefinito 600×800 passa
  da 424 a 208 mm. Con barre solo su due facce può salire: in un caso delle prove della libreria, 300×500, passa da 208 a
  406 mm.
- **Pilastri EC, staffe nelle sovrapposizioni (R13).** La riduzione 0,6 dell'interasse delle staffe si applica solo con
  Ømax > 14 mm (EN 1992-1-1, 9.5.3(4)).
- **Fessurazione DIN, hc,eff (R21).** La condizione (h − x)/3 ≥ c + 20 mm usa lo stesso copriferro della formula di wk anche
  quando il copriferro è assegnato.
- **Muri, ancoraggi (R10).** Oltre la classe C60/75 la resistenza fctk,0,05 della lunghezza di ancoraggio resta quella della
  C60/75 (EN 1992-1-1, 8.4.2(2)). Nei 56 muri di prova nessuna distinta cambia.
- **Torsione, riferimenti (R19).** Le citazioni dicono NTC 2018 [4.1.34]-[4.1.40] e, per la DS, (6.7b NA). Cambia solo il
  testo.
- **Fondazioni superficiali.** Il risultato riporta la versione nuova della libreria; nessun valore cambia (AH-FON-01,
  AI-FON-01).
- **Ponti in acciaio.** Steel, CompositeBridge e GPC.Design portano il lavoro delle librerie sulle giunzioni; nelle prove di
  ANTHEA nessun risultato cambia.

## Guide

Guide pratica e teorica **Rev59**, le stesse della 1.14.0. La guida teorica descrive già l'interasse del pilastro lungo il
perimetro. Il sottomenu «Separatore decimale» non è ancora nella guida pratica: è descritto in queste note.

## Limiti noti

- **File salvati con la 1.14.x.** Un file con una sezione in c.a. salvato con la 1.14.0 o la 1.14.1 non si apre con la 1.13.0
  o precedenti. I file delle versioni precedenti si aprono con la 1.14.1.
- **Paratie, filtrazione di uno scavo simmetrico.** Il bordo laterale del dominio è sempre a carico imposto. Se come
  estensione si inserisce la semilarghezza di uno scavo simmetrico, l'asse fa da dreno e il gradiente lungo la parete risulta
  sottostimato, a sfavore di sicurezza. Il campo del bordo a flusso nullo in ANTHEA non c'è ancora.
- **Paratie, coefficienti parziali.** Le storie marcate SLU non ricevono coefficienti parziali sulle azioni: vanno inserite già
  fattorizzate.
- **Scelte delle librerie rinviate.** Dodici scelte di calcolo delle librerie sono rinviate a dopo il refactoring, perché
  servono testi normativi, decisioni o lavori grandi (registro delle differenze, stato «rinviato»). Tra queste:
  - nel taglio EN non si controlla VEd ≤ 0,5 bw d ν fcd (R9);
  - con il solutore non lineare le tensioni di esercizio usano le leggi di progetto (R5);
  - nel profilo MC2010 θmin vale sempre 20° (R4);
  - nei profili EN e UNI la classe minima indicativa di XC3 è C25/30 invece di C30/37 (R17).
