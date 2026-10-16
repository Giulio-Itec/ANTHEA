# F5 — Ripianificazione dell'interfaccia MVVM (checkpoint F5.0)

## Scopo

Il checkpoint **F5.0 [CP]** del [piano](piano.md) chiede di ripianificare F5 sul codice reale dopo F4. Questa nota
riassume per l'utente:
- lo stato della UI;
- l'architettura proposta;
- i passi F5.1–F5.14 con criteri e checkpoint;
- le domande da decidere.

I dettagli sono in due documenti:
- [f5-inventario-ui.md](f5-inventario-ui.md): fatti e conteggi dell'interfaccia attuale, con riferimenti `file:riga`;
- [f5-proposta-architettura.md](f5-proposta-architettura.md): architettura, convenzioni, prove, piano, rischi e domande
  con le motivazioni.

Analisi in sola lettura del ramo `refactoring/integrazione-1016` (punta `71fd52b7`, ANTHEA 1.9.0), il 16/10/2026;
il ramo è poi entrato in `main` con ANTHEA 1.10.0 (`8b9ce6d3`), da cui parte il ramo `refactoring/f5`.

## Stato del codice dopo F4

| Grandezza | Valore |
| --- | --- |
| `X.Desktop` | 158 file C#, 18 462 righe; nessuna vista XAML, nessun ViewModel salvo tre isole |
| Viste e documento | 2 046 controlli creati da codice; 1 686 letture e 296 scritture dirette del JSON del foglio |
| `MainWindow` | 14 file partial, 2 140 righe; un solo documento e un solo foglio, sostituiti in otto punti |
| `SheetEditor` | 696 righe con i pali verticali; smista 13 moduli ed è il contratto delle prove di equivalenza (16 costruzioni, 47 membri) |
| Prove WPF | 48 suite nello stadio `ui`; 71 file di prova compilati nell'exe, 41 partial di `MainWindow`; 171 chiamate di riflessione in 7 progetti |
| Equivalenza | `ui/f3-equivalence`: 36 fixture su 12 moduli (stratigrafia nessuna, palificata 2), 22 126 transizioni F4 ammesse (palificata 0) |
| Tema | tavolozza calcolata, 365 colori esadecimali (243 distinti) e 423 dimensioni di carattere nel codice |
| Confine | 0 riferimenti della Release a `ANTHEA.Calculations` e GPC; 2 154 residui F4 nella UI, soprattutto nei disegni |

## Architettura proposta in breve

- **Un solo progetto nuovo**, `src/ANTHEA.Presentation` (net8.0, senza WPF). Contiene ViewModel, servizi astratti,
  sessioni, `ModuleHost` e campi.
- **Viste nuove** in XAML dentro `X.Desktop`, che diventa `ANTHEA.Desktop` a F5.14.
- **Prove nuove**: `tests/ANTHEA.Presentation.Tests` (senza WPF) e `tests/ANTHEA.Desktop.UiTests` (fuori schermo e fuori
  dall'eseguibile).
- **CommunityToolkit.Mvvm 8.3.2** (già presente e approvato) con un piccolo strato nostro. Nessun contenitore DI.
- **Il documento resta un `JsonObject`**: stesso formato salvato, stesse regole di scrittura per modulo. L'equivalenza
  resta provabile con `ui/f3-equivalence`, senza tolleranze nuove.
- **Convivenza**: `ISheetEditor` estratto da `SheetEditor`. La finestra attuale riceve dal `ModuleHost` la vista storica
  o quella nuova di ogni modulo, e ogni modulo migrato è subito in produzione.
- **Shell (F5.13)**:
  - un progetto alla volta e più file singoli, ogni foglio in una scheda, schede spostabili in altre finestre;
  - un foglio si conferma quando la sua scheda perde il fuoco, come oggi alla navigazione, quindi i dati condivisi si
    propagano con lo stesso dialogo e senza conflitti.
- **Temi**: i token sono nomi dei pennelli attuali, quindi Scuro e Molto scuro restano identici. Le viste nuove usano
  solo token; i disegni tecnici restano dove sono.
- **Prove per modulo**: confronto diretto vista vecchia/nuova a differenza zero, più il confronto con il riferimento F3.
  Ogni differenza va in `transitions-f5.json` con voce di registro e ok dell'utente (D2).
- **Regole F5.1** in `ANTHEA.Architecture.Tests`: niente WPF né calcoli in Presentation, niente stato statico mutabile,
  niente messenger statici.

## Piano raffinato

| Passo | Contenuto | Ingresso | Uscita | CP |
| --- | --- | --- | --- | --- |
| F5.0 | Questa nota, inventario e proposta; risposte Q1–Q20 | F4 chiusa, integrazione in `main` | piano e decisioni aggiornati; ramo `refactoring/f5-mvvm` da `main` | **[CP]** |
| F5.1 | Presentation e prove; base MVVM, guardia dei comandi, calcolo con scheduler; servizi; composizione; regole P1–P6 | F5.0 | quick ed equivalenza invariati; prove dei rilevatori | — |
| F5.2 | `ISheetEditor` e `ModuleHost`; campi e form (`FieldForm`); coordinatore dei dati condivisi e storia del progetto; prove su adattatore invece che su `SheetEditor` | F5.1 | equivalenza invariata; parità `FieldForm`/`InputForm`; scenari F1.4 senza WPF | — |
| F5.3 | Temi a token (colori e caratteri), dizionari separati, `--check-contrast` nel runner | F5.2 | Scuro e Molto scuro a 0 difetti, Chiara invariata | **[CP]** |
| F5.4 | Pilota palificata orizzontale (225 righe, calcolo sincrono conservato) | F5.1–F5.3, Q20 | equivalenza esatta, prove VM, schermate nei tre temi | **[CP]** |
| F5.5 | Materiali (calcestruzzo, acciaio), Reologia ospitata | pilota | ricetta del modulo | se transizioni |
| F5.6 | Palo e micropalo verticale; F5.6b Stratigrafia con le prime fixture | pilota | ricetta; `SheetEditor` ridotto allo smistamento | se transizioni (**[CP]** se secondo pilota) |
| F5.7 | Palo e micropalo orizzontale | pilota | ricetta | se transizioni |
| F5.8 | Palo elastico (sezione c.a. storica ospitata fino a F5.10) | F5.7 | ricetta | se transizioni |
| F5.9 | Muri e stabilità globale; F5.9b Fondazioni superficiali | pilota, F5.6b | ricetta; transizioni F4 dei muri identiche | se transizioni |
| F5.10 | Sezione c.a. in sottopassi | F5.5 | ricetta per sottopasso | se transizioni |
| F5.11 | Sezione composta da ponte | pilota | ricetta | se transizioni |
| F5.12 | Bridge Design e prezzari | pilota | ricetta | se transizioni |
| F5.13 | Shell: sessioni, progetti, revisioni, schede, finestre staccate, Wiki, Modelli; stato statico E5 e calcoli paralleli; ritiro di `MainWindow` e `SheetEditor` | (a)–(c) dopo F5.2; il resto dopo F5.12 | full senza nuovi fallimenti; calcoli paralleli uguali bit per bit ai seriali; guida e PDF | **[CP]** disposizione; **[CP]** sessione di prova |
| F5.14 | `ANTHEA.Desktop`; via `UiTests` e `supporto/test/Desktop`; terza revisione di `AGENTS.md` | F5.13 | full conforme, installer verificato | merge con ok |

**Ricetta del modulo**:
1. inventario della vista e fixture aggiuntive catturate prima con la vista storica;
2. ViewModel e vista nuova;
3. equivalenza diretta e con il riferimento;
4. prove spostate in `ANTHEA.Desktop.UiTests`;
5. schermate e contrasto;
6. passaggio della Release e ritiro della vista storica.

**Parallelismo** dopo il pilota, in cinque tracce:

| Traccia | Passi | Note |
| --- | --- | --- |
| A | F5.5 → F5.10 | materiali, poi sezione c.a. |
| B | F5.6 → F5.6b → F5.9 → F5.9b | geotecnica e terreni |
| C | F5.7 → F5.8 | stesso modulo orizzontale |
| D | F5.11, F5.12 | indipendenti fra loro |
| E | F5.13 (a)–(c) | parte dopo F5.2 |

## Domande per l'utente

«Blocca» = da decidere prima di F5.1–F5.2. Motivazioni nella sezione 10 della proposta.

| # | Domanda | Opzioni | Raccomandazione | Blocca |
| --- | --- | --- | --- | --- |
| Q1 | Base MVVM | CommunityToolkit + strato nostro / solo classi nostre | CommunityToolkit, senza `Messenger.Default`/`Ioc.Default` | sì |
| Q2 | Iniezione delle dipendenze | composizione a mano / Microsoft.Extensions.DI | a mano | sì |
| Q3 | Forma delle viste nuove | XAML con DataTemplate / codice come oggi | XAML; disegni in codice | sì |
| Q4 | Dove stanno le viste nuove | in `X.Desktop` / libreria WPF separata | in `X.Desktop` | sì |
| Q5 | Progetti di presentazione | uno solo / uno per modulo / accorpare anche Modelli e Reologia | uno solo, Modelli e Reologia restano separati | sì |
| Q6 | Token e tema Chiaro | un token per colore / accorpamento subito / accorpamento modulo per modulo | modulo per modulo con schermate | F5.3 |
| Q7 | Ordine moduli/shell | moduli nella finestra attuale, shell dopo / shell subito con moduli storici | moduli nella finestra attuale | sì |
| Q8 | Calcolo della palificata | sincrono come oggi / sullo sfondo | sincrono (cattura esatta, 0 transizioni) | F5.4 |
| Q9 | Dati condivisi con più schede | conferma alla perdita del fuoco / propagazione per campo con conflitti | conferma alla perdita del fuoco | F5.13 |
| Q10 | Schede staccabili | soluzione nostra / libreria di docking | soluzione nostra | F5.13 |
| Q11 | Codice delle viste storiche | eliminare con tag / `supporto/SUPERATI` non compilato / progetto non distribuito | `supporto/SUPERATI` | sì |
| Q12 | Moduli fuori dall'elenco del piano | Stratigrafia F5.6b, Fondazioni F5.9b, Wiki/Progetti/Modelli in F5.13 | approvare | sì |
| Q13 | Annulla per campo | no, resta l'annulla del progetto / sì | no | F5.13 |
| Q14 | Nomi a F5.14 | `ANTHEA.Desktop` ed exe `ANTHEA.exe` / anche tutti i namespace | solo progetto e codice nuovo | F5.14 |
| Q15 | Riferimento di equivalenza | tenere `expected` + F4 e aggiungere F5 / ricatturare ora | tenere | sì |
| Q16 | Forma delle regole («analyzer») | test di architettura / analyzer Roslyn / test + Threading.Analyzers su Presentation | test + Threading.Analyzers | sì |
| Q17 | ViewModel oggi in Application | spostarli in Presentation / lasciarli | spostarli con il loro modulo | F5.10, F5.12 |
| Q18 | Wiki nella shell | scheda / pagina come oggi / pannello laterale | scheda | F5.13 |
| Q19 | Errori dei comandi | come oggi (log solo dei non gestiti) / registrare tutto | come oggi | F5.1 |
| Q20 | Modulo pilota (nuova) | solo palificata / palificata + palo verticale come secondo pilota / palo verticale / fondazioni | palificata + secondo pilota | entro F5.2 |

Su Q20: la palificata è piccola e isolata. Non usa `InputForm` né `JsonGrid`, non ha dati condivisi, dialoghi né report
con opzioni, quindi mette alla prova poco dei punti difficili. Il secondo pilota, palo e micropalo verticale, convalida
proprio i componenti comuni prima di aprire le tracce parallele.
