# ANTHEA · Modulo autonomo muro di sostegno

Avviare **Avvia modulo.cmd**, oppure `App\ANTHEA.Muro.exe`.
Il programma è autonomo: non modifica e non richiede il progetto ANTHEA principale.
Richiede Windows e .NET Desktop Runtime 9, già presente sul computer su cui è stato preparato.

## Uso

1. Selezionare **Carica esempio** per provare il modello, oppure compilare le celle vuote.
2. Inserire geometria, parametri geotecnici caratteristici e sovraccarico. Sono accettati sia punto sia virgola decimale, senza separatori delle migliaia.
3. I risultati si aggiornano automaticamente. La cancellazione di un dato invalida subito i risultati precedenti.
4. Leggere i tre esiti locali e la scheda **Combinazioni e sollecitazioni**.
5. Salvare il calcolo in `.muro.json`. È possibile salvare anche un calcolo ancora incompleto.
6. Esportare la relazione HTML, aprirla in un browser e utilizzare Stampa / Salva come PDF, oppure esportare i risultati JSON.

La cartella **Esempi** contiene un calcolo dimostrativo, i suoi risultati e la relativa relazione. Non è un progetto reale.

## Campo del modello

Muro a mensola con fusto verticale a spessore costante; calcolo per metro di sviluppo.
Terreno granulare omogeneo asciutto, riempimento orizzontale e spinta attiva mobilitabile di Rankine.
La fondazione è nastriforme, orizzontale, senza incasso né terreno sulla mensola a valle.
Il terreno di riempimento arriva al piano di posa sul piano virtuale al bordo di monte.
La fondazione deve avere base ruvida: φ′f/2 ≤ δb ≤ φ′f.

Il modulo calcola scorrimento, ribaltamento, capacità portante drenata, distribuzione delle pressioni di contatto e sollecitazioni di fusto e mensole.
L'inviluppo considera otto combinazioni A1 + M1 + R3 con i coefficienti NTC 2018 descritti nella scheda Metodo.
Per eccentricità superiore a B/3 la verifica di portanza è dichiarata fuori campo.

**Esclusi:** sisma, falda, sottospinta, stabilità globale, terreni stratificati o coesivi, riempimento inclinato, compattazione, cedimenti e SLE, verifiche e armature del c.a.
Le sollecitazioni sono dati per il successivo dimensionamento strutturale, non verifiche STR.
Gli esiti locali non costituiscono una verifica completa dell'opera secondo NTC.
Le ipotesi e i parametri devono essere verificati dal progettista in relazione al caso reale.

## Convenzioni

- H: altezza del fusto sopra la soletta; t: spessore della soletta.
- a: mensola a valle; s: spessore fusto; b: mensola a monte; B = a + s + b.
- Spinta esterna calcolata su H+t; sollecitazioni del fusto calcolate su H.
- e positivo verso valle; x misurato dal bordo di valle.
- M fusto positivo: trazione a monte. M mensole positivo: trazione inferiore; negativo: superiore.
- V valle è l'integrale del carico netto verso l'alto; V monte è l'integrale del carico netto verso il basso.
- Le pressioni mostrate nel disegno sono caratteristiche; le tabelle riportano anche tutte le combinazioni SLU.

## Sorgenti e controlli

I sorgenti C# e il progetto WPF sono in **Sorgenti**. Non vi sono dipendenze NuGet esterne.
Eseguire **Compila.cmd** con SDK .NET 9 per ricompilare l'applicazione.
Eseguire **Verifica.cmd** per i benchmark numerici e i controlli di equilibrio, contatto, mensole, input e serializzazione.
La cartella **Verifiche** contiene gli esiti e le schermate della verifica eseguita.

Il motore numerico è in `Sorgenti\Engine.cs`; l'interfaccia è in `Sorgenti\MainWindow.cs`.
Il modello ha formato versionato `ANTHEA.Muro`, versione 1, distinto dagli archivi del programma principale.

## Riferimenti

- [D.M. 17/01/2018, NTC 2018](https://www.gazzettaufficiale.it/eli/id/2018/2/20/18A00716/sg): §§ 6.2.4.1, 6.5.3.1.1 e tabelle 6.2.I, 6.2.II, 6.5.I.
- [JRC 2013, Eurocode 7: Geotechnical Design – Worked examples](https://eurocodes.jrc.ec.europa.eu/sites/default/files/2022-06/2013_06_WS_GEO.pdf): capitoli 3 e 4.

Data di preparazione: 22 settembre 2026.
