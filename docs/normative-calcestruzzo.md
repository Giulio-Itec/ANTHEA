# Calcestruzzo — normative collegate e lavoro rimanente

Aggiornamento 21 settembre 2026. Si collegano solo classi effettivamente presenti
nelle DLL distribuite in `lib/Checker`. Nessuna DLL o repository sorgente esterno
è stato modificato. Le prove verificano il collegamento e la coerenza con le API
native: **non costituiscono una validazione completa di ogni normativa**.

## Matrice delle implementazioni

| Scelta nell'interfaccia | Implementazione DLL | Stato e limiti |
| --- | --- | --- |
| NTC 2018 | `StandardNTC2018Concrete` | αcc=0,85; domini e tensioni nativi. Taglio/fessurazione NTC nei limiti documentati. |
| Model Code 2010 | `StandardModelCode2010` | Coefficienti base del modello 2010. Non equivale al modello 2020. |
| EN 1992-1-1 | `StandardEN1992p11` | Riferimento dichiarato dal sorgente EN 1992-1-1:2004/AC:2010; prima generazione. |
| UNI EN 1992-1-1 | `StandardUNIEN1992p11` | Riferimento DLL 2005; costruttore senza parametri nazionali distinti. Eredita EC2 base, **non è un annesso italiano completo**. |
| DIN EN 1992-1-1 | `StandardDINEN1992p11` | Modifica αcc a 0,85; resto ereditato. Non certifica la copertura completa del NA tedesco. |
| DS EN 1992-1-1 | `StandardDSEN1992p11` | γc=1,40, γs=1,20; resto ereditato. Non certifica la copertura completa del NA danese. |
| NS EN 1992-1-1 | `StandardNSEN1992p11` | Modifica αcc a 0,85; resto ereditato. Non certifica la copertura completa del NA norvegese. |
| CNR-DT 204/2006 | `StandardCNR204` | Coefficienti base Model Code; riferimento AC:2008. Interfaccia e verifiche specifiche FRC non implementate. |
| CS-TR34 | `StandardCSTR34` | αcc=αct=0,85, γF=1,00 nella DLL. Non sono implementate le verifiche specifiche delle pavimentazioni/FRC. |

Domini e analisi tensionali usano la classe scelta, con gli eventuali coefficienti
modificati dall'utente. I valori applicati e quelli predefiniti compaiono nel report.
La disponibilità di una classe nazionale o di un coefficiente non implica che ogni
regola dell'annesso sia stata implementata nel motore.

Per tutte le scelte **diverse da NTC 2018**, taglio e fessurazione sono indicati
come non implementati: non si applicano in silenzio le routine NTC cambiando etichetta.
Le proprietà dei materiali CLS continuano a derivare dal materiale EN1992 della
DLL: sono da riesaminare insieme alle edizioni normative nell'estensione futura.

## Coefficienti esposti

αcc, αct, γc, γs, γp, coefficienti accidentali di CLS/acciaio/trefoli, γcE,
γF, coefficiente della deformazione ultima dell'acciaio, limiti tensionali
CLS rara/QP, acciaio e trefoli rara. I parametri accidentali/FRC sono una
predisposizione; la loro modifica non crea automaticamente un caso accidentale/FRC.

Attenzione al parametro SLE dei trefoli: `GetServiceabilityCharacteristicStressPrestress`
applica il coefficiente a `SteelMaterial.Fyk`, che nell'adattatore è **fpyk**, non
fpk. Si mantiene il calcolo nativo, con etichetta esplicita; la corrispondenza alle
singole norme CAP va validata. Le API distinguono inoltre il trefolo tramite εp:
per σp0 nullo non viene pubblicata una verifica SLE potenzialmente classificata male.

## Da implementare o validare

1. Edizioni e pacchetti di parametri nazionali completi e versionati: UNI/DIN/DS/NS
   e gli ulteriori annessi richiesti. Esempi indipendenti per ogni variante.
2. Eurocodici di seconda generazione e Model Code 2020: nuove classi/solver o DLL
   aggiornate, confronto delle formule e validazione dedicata; non alias delle vecchie edizioni.
3. Taglio e fessurazione per EC2, Model Code e annessi nazionali, con scelte specifiche
   della combinazione, dell'ambiente e del materiale.
4. Taglio circolare e spirale, sezione generica, taglio CAP/precompressione,
   torsione, interazione biassiale e gerarchia sismica. La preview delle staffe è
   schematica e non un progetto esecutivo del sagomario.
5. Sezione generica: editor di contorno e fori, posizionamento armature/trefoli,
   controlli topologici, discretizzazione e modelli di verifica applicabili.
6. Materiali custom avanzati: curve σ–ε tabellari, CLS fibrorinforzato, profili
   metallici interni e gestione di librerie materiali condivise fra progetti.
7. Completamento SLE con trefoli, inclusi casi a predeformazione nulla, significato
   dei limiti tensionali per ciascuna norma, area/aderenza efficace nella fessurazione.
   Apertura wk da non lineare e dagli altri casi fuori campo del porting corrente.
8. Verifica normativa indipendente su esempi reali, dettagli costruttivi, ancoraggi,
   duttilità, perdite di precompressione e secondo ordine; nessun esito globale automatico.

## Riferimenti per le nuove edizioni

- [JRC — Eurocodici di seconda generazione](https://eurocodes.jrc.ec.europa.eu/second-generation-eurocodes).
- [JRC — norme nazionali e annessi](https://eurocodes.jrc.ec.europa.eu/en-eurocodes-implementation/national-standards).
- [fib — Model Code 2020](https://shop.fib-international.org/publications/model-codes/model-code-2020/).

Questi riferimenti distinguono famiglie ed edizioni; non sostituiscono i testi
normativi necessari per implementare e verificare le formule.
